module CommandoWar.Sim.Tests.DeterminismPropertyTests

open FsCheck
open FsCheck.FSharp
open FsCheck.Xunit
open CommandoWar.Sim

// Generative determinism property tests (TASK-019, backlog B-012b narrowed;
// docs/09_TEST_STRATEGY.md section 2.2). These generalise what
// CorpusTests / Corpus.checkEntry already does for 5 fixed hand-built
// scenarios to an open-ended, FsCheck-generated space of small worlds and
// command sequences. Only properties the currently implemented systems can
// support are covered here: appraisal (TASK-028) and commitments (TASK-030)
// do; combat, death, and objectives (also on the docs/09 section 2.2 list)
// still do not exist.
//
// Every generator is bounded well inside the TASK-014 per-tick budget and the
// largest scale this repository has actually run (content/benchmarks/BASELINE.md,
// ~50 agents): grid dimensions 3..7, 1..4 agents, 5..15 ticks. `WorldState` is
// built directly (not through `World.create` / `Scenario.validate`) so the
// generator has full control over terrain and can guarantee every agent
// starts on a passable cell — starting an agent on impassable terrain would
// fail property 2 for a generator reason, not a movement-phase reason.

// --- shared generators ---------------------------------------------------

let private boundsGen: Gen<GridBounds> =
    gen {
        let! w = Gen.choose (3, 7)
        let! h = Gen.choose (3, 7)
        return { Width = w; Height = h }
    }

/// Row-major cell for index `i` into a `bounds`-shaped dense array, matching
/// `Terrain`'s own `y * Width + x` indexing.
let private cellOf (bounds: GridBounds) (i: int) : Cell = { X = i % bounds.Width; Y = i / bounds.Width }

/// One cell's movement class and entry cost. Impassable is a minority of
/// cells; passable cells are mostly `Terrain.BaseMoveCost` with some costing
/// more, so both `Pathfinding`'s obstacle avoidance and cost-aware routing
/// get exercised.
let private cellClassGen: Gen<MovementClass * int> =
    Gen.frequency
        [ 1, Gen.constant (Impassable, 0)
          6, Gen.constant (Passable, Terrain.BaseMoveCost)
          2, Gen.constant (Passable, 2)
          1, Gen.constant (Passable, 3) ]

/// Random flat-elevation, uncovered, opaque-free terrain over `bounds`: only
/// passability and entry cost vary, which is all `Pathfinding` and the
/// movement phase consume today.
let private terrainGen (bounds: GridBounds) : Gen<Terrain> =
    let n = bounds.Width * bounds.Height

    Gen.arrayOfLength n cellClassGen
    |> Gen.map (fun cells ->
        { Bounds = bounds
          Elevation = Array.zeroCreate n
          Movement = cells |> Array.map fst
          MoveCost = cells |> Array.map snd
          Opaque = Array.zeroCreate n
          Cover = Array.zeroCreate (n * 4) })

// --- world + command-sequence generator (properties 1 and 2) -------------

/// One generated case: an initial world (random terrain, an agent roster
/// placed only on passable cells) plus a random `MoveTo` command sequence.
/// Targets are arbitrary in-bounds cells, deliberately not restricted to
/// passable ones, so `MovementBlocked` (docs/04 section 8 step 6) is
/// exercised alongside ordinary routing.
type private RandomCase =
    { World: WorldState
      Commands: RecordedCommand[]
      TickCount: int64 }

let private toRecordedCommands (agents: AgentState[]) (raw: (int * int * Cell)[]) : RecordedCommand[] =
    raw
    |> Array.groupBy (fun (tick, _, _) -> tick)
    |> Array.collect (fun (tick, group) ->
        group
        |> Array.mapi (fun seq (_, agentIdx, target) ->
            { Tick = int64 tick
              Sequence = seq
              Command = Command.moveTo (CommandId.ofInt (tick * 1000 + seq)) (int64 tick) agents.[agentIdx].Id target
              Issuer = "property-test" }))

let private randomCaseGen: Gen<RandomCase> =
    gen {
        let! bounds = boundsGen
        let! terrain = terrainGen bounds
        let n = bounds.Width * bounds.Height

        let passableCells =
            [| 0 .. n - 1 |] |> Array.filter (fun i -> terrain.Movement.[i] = Passable) |> Array.map (cellOf bounds)

        let! requestedAgents = Gen.choose (1, 4)
        let agentCount = min requestedAgents passableCells.Length
        let! shuffledStarts = Gen.shuffle passableCells
        let starts = shuffledStarts |> Array.truncate agentCount

        let agents =
            starts |> Array.mapi (fun i c -> Agent.create (AgentId.ofInt i) Friendly c)

        let! seed = Gen.choose (0, System.Int32.MaxValue)

        let world: WorldState =
            { Tick = 0L
              Bounds = bounds
              Terrain = terrain
              Agents = agents
              TacticalKnowledge = [||]
              HostileTacticalKnowledge = [||]
              Random = SplitMix64.create (uint64 seed) }

        let! tickCount = Gen.choose (5, 15)

        let commandGen =
            gen {
                let! tick = Gen.choose (1, tickCount)
                let! agentIdx = Gen.choose (0, agentCount - 1)
                let! x = Gen.choose (0, bounds.Width - 1)
                let! y = Gen.choose (0, bounds.Height - 1)
                return tick, agentIdx, ({ X = x; Y = y }: Cell)
            }

        let! rawCommands =
            if agentCount = 0 then
                Gen.constant [||]
            else
                gen {
                    let! commandCount = Gen.choose (0, agentCount * 3)
                    return! Gen.arrayOfLength commandCount commandGen
                }

        return
            { World = world
              Commands = toRecordedCommands agents rawCommands
              TickCount = int64 tickCount }
    }

let private replayOf (case: RandomCase) =
    Replay.record ReplayMeta.unspecified case.World case.TickCount (CommandLog.create case.Commands)
    |> Replay.run SimConfig.standard

// --- property 1: determinism under randomly generated commands -----------

[<Property(MaxTest = 200)>]
let ``two independent replays of the same generated world and command sequence produce identical per-tick hashes`` () =
    Prop.forAll (Arb.fromGen randomCaseGen) (fun case ->
        match replayOf case, replayOf case with
        | Ok a, Ok b ->
            match Divergence.compare a b with
            | Match _ -> true
            | report -> failwith $"generated case diverged on a self-replay: {report}"
        | Error e, _
        | _, Error e -> failwith $"replay of a generated case failed: {e}")

// --- property 2: no agent occupies an invalid cell after movement --------

[<Property(MaxTest = 200)>]
let ``no agent occupies an impassable cell after any tick of the movement phase`` () =
    Prop.forAll (Arb.fromGen randomCaseGen) (fun case ->
        match replayOf case with
        | Error e -> failwith $"replay of a generated case failed: {e}"
        | Ok outcome ->
            outcome.TickStates
            |> Array.forall (fun state -> state.Agents |> Array.forall (fun a -> Terrain.passable state.Terrain a.Position)))

// --- property 2b: distinct live agents hold distinct cells ---------------
// TASK-022: the movement phase must never place two live agents on the same
// cell. `randomCaseGen` seeds every agent on a distinct passable cell, so a
// duplicate at any post-tick checkpoint is a movement-phase defect (a mover
// entering an occupied cell, a swap, or a collapsed follow chain), not a
// generator artefact. Property 2 above only checks cells are *passable*.

[<Property(MaxTest = 200)>]
let ``after every tick of every generated case, distinct live agents hold distinct cells`` () =
    Prop.forAll (Arb.fromGen randomCaseGen) (fun case ->
        match replayOf case with
        | Error e -> failwith $"replay of a generated case failed: {e}"
        | Ok outcome ->
            outcome.TickStates
            |> Array.forall (fun state ->
                let cells = state.Agents |> Array.map (fun a -> a.Position)
                Array.length (Array.distinct cells) = Array.length cells))

// --- property 3: pathfinding correctness on random terrain ---------------
// Extends PathfindingTests.fs's hand-built `propertyTerrain` check (a fixed
// terrain proves the invariant for one map, not the general case) to
// FsCheck-generated terrain and endpoints.

let private cardinallyAdjacent (a: Cell) (b: Cell) : bool = abs (a.X - b.X) + abs (a.Y - b.Y) = 1

let private recomputedCost (t: Terrain) (cells: Cell[]) : int =
    cells |> Array.skip 1 |> Array.sumBy (Terrain.moveCost t)

let private terrainAndEndpointsGen: Gen<Terrain * Cell * Cell> =
    gen {
        let! bounds = boundsGen
        let! terrain = terrainGen bounds

        let cellGen =
            gen {
                let! x = Gen.choose (0, bounds.Width - 1)
                let! y = Gen.choose (0, bounds.Height - 1)
                return ({ X = x; Y = y }: Cell)
            }

        let! start = cellGen
        let! goal = cellGen
        return terrain, start, goal
    }

[<Property(MaxTest = 200)>]
let ``whenever Pathfinding.find returns Found on random terrain the cells form an adjacent passable chain with matching cost`` () =
    Prop.forAll (Arb.fromGen terrainAndEndpointsGen) (fun (terrain, start, goal) ->
        match Pathfinding.find terrain start goal with
        | Found(cells, cost) ->
            cells.[0] = start
            && cells.[cells.Length - 1] = goal
            && (Array.pairwise cells |> Array.forall (fun (a, b) -> cardinallyAdjacent a b))
            && (cells |> Array.forall (Terrain.passable terrain))
            && cost = recomputedCost terrain cells
        | NoPath
        | BudgetExhausted _
        | InvalidEndpoint _ -> true)

// --- property 4: pathfinding optimality vs an independent shortest path --
// Property 3 recomputes the *returned* path's own cost, so it cannot catch a
// path that is suboptimal but summed correctly (the TASK-021 defect). This
// compares Pathfinding.find's cost to a plain relaxation-to-fixed-point
// shortest-path search that shares no code or ordering with A*, over terrain
// whose passable costs span the whole valid [BaseMoveCost, MaxMoveCost]
// range, including values near the ceiling.

/// One cell for the optimality property: passable costs cover the low range,
/// exactly `BaseMoveCost`, and a band near `MaxMoveCost`; a minority impassable.
let private validCellGen: Gen<MovementClass * int> =
    Gen.frequency
        [ 1, Gen.constant (Impassable, 0)
          4, Gen.constant (Passable, Terrain.BaseMoveCost)
          3, Gen.choose (Terrain.BaseMoveCost, 15) |> Gen.map (fun c -> Passable, c)
          1, Gen.choose (Terrain.MaxMoveCost - 25, Terrain.MaxMoveCost) |> Gen.map (fun c -> Passable, c) ]

let private validTerrainAndEndpointsGen: Gen<Terrain * Cell * Cell> =
    gen {
        let! bounds = boundsGen
        let n = bounds.Width * bounds.Height
        let! cells = Gen.arrayOfLength n validCellGen

        let terrain =
            { Bounds = bounds
              Elevation = Array.zeroCreate n
              Movement = cells |> Array.map fst
              MoveCost = cells |> Array.map snd
              Opaque = Array.zeroCreate n
              Cover = Array.zeroCreate (n * 4) }

        let cellGen =
            gen {
                let! x = Gen.choose (0, bounds.Width - 1)
                let! y = Gen.choose (0, bounds.Height - 1)
                return ({ X = x; Y = y }: Cell)
            }

        let! start = cellGen
        let! goal = cellGen
        return terrain, start, goal
    }

/// Independent shortest path: least cost to enter `goal` from `start`, or
/// `None` when no traversable path exists or an endpoint is invalid. A dense
/// distance array relaxed until it stops changing (Bellman-Ford style) — no
/// heuristic, no frontier, no dependence on expansion order, so agreement
/// with A* is real evidence rather than a restatement.
let private referenceShortestPath (terrain: Terrain) (start: Cell) (goal: Cell) : int option =
    let b = terrain.Bounds
    let idx (c: Cell) = c.Y * b.Width + c.X

    if
        not (GridBounds.contains start b)
        || not (GridBounds.contains goal b)
        || not (Terrain.passable terrain start)
        || not (Terrain.passable terrain goal)
    then
        None
    elif start = goal then
        Some 0
    else
        let dist = Array.create (b.Width * b.Height) System.Int32.MaxValue
        dist.[idx start] <- 0
        let mutable changed = true

        while changed do
            changed <- false

            for y in 0 .. b.Height - 1 do
                for x in 0 .. b.Width - 1 do
                    let here = dist.[y * b.Width + x]

                    if here < System.Int32.MaxValue then
                        for nb in
                            [ { X = x; Y = y - 1 }
                              { X = x + 1; Y = y }
                              { X = x; Y = y + 1 }
                              { X = x - 1; Y = y } ] do
                            if GridBounds.contains nb b && Terrain.passable terrain nb then
                                let cand = here + Terrain.moveCost terrain nb

                                if cand < dist.[idx nb] then
                                    dist.[idx nb] <- cand
                                    changed <- true

        if dist.[idx goal] = System.Int32.MaxValue then
            None
        else
            Some dist.[idx goal]

[<Property(MaxTest = 200)>]
let ``Pathfinding.find on valid random terrain returns the true minimum cost and finds a path exactly when one exists`` () =
    Prop.forAll (Arb.fromGen validTerrainAndEndpointsGen) (fun (terrain, start, goal) ->
        let reference = referenceShortestPath terrain start goal

        match Pathfinding.find terrain start goal with
        // Default cap Width * Height closes every cell once, so on these
        // (<= 7x7) grids the budget is never the limiting factor: Found iff a
        // path exists, and the cost is exactly the reference minimum.
        | Found(_, cost) -> reference = Some cost
        | NoPath -> reference = None
        | InvalidEndpoint _ ->
            not (GridBounds.contains start terrain.Bounds)
            || not (GridBounds.contains goal terrain.Bounds)
            || not (Terrain.passable terrain start)
            || not (Terrain.passable terrain goal)
        | BudgetExhausted _ -> true)

// --- property 6: the shared tactical picture is grounded in observation ----
// TASK-026. Extends the fixed-scenario SimulationTests perception facts to a
// generated space of small worlds with a few hostiles. For every post-tick
// state, every contact in `WorldState.TacticalKnowledge` (a) was genuinely
// visible to some friendly on its own `LastSeenTick` (recomputed independently
// through `Perception.visibleContactsFor`), (b) is within `ExpireAfter` ticks
// of that sighting, (c) carries one of the two valid confidence bands, and (d)
// has a `LastSeenTick` that never moves backwards while the contact survives.
//
// TASK-034 (backlog B-022, partial): extended in place to also run the
// identical check against `WorldState.HostileTacticalKnowledge`, grounded in
// a Hostile agent's own observation instead — the same generator already
// deploys hostiles, and `Perception.mergeKnowledge` is the identical
// side-agnostic function underneath both pictures.

let private perceptionCaseGen: Gen<RandomCase> =
    gen {
        let! bounds = boundsGen
        let! terrain = terrainGen bounds
        let n = bounds.Width * bounds.Height

        let passableCells =
            [| 0 .. n - 1 |]
            |> Array.filter (fun i -> terrain.Movement.[i] = Passable)
            |> Array.map (cellOf bounds)

        let! shuffled = Gen.shuffle passableCells
        let! friendlyCount = Gen.choose (1, 3)
        let! hostileCount = Gen.choose (1, 2)
        let want = friendlyCount + hostileCount
        let taken = shuffled |> Array.truncate (min want passableCells.Length)
        let fCount = min friendlyCount taken.Length

        let agents =
            taken
            |> Array.mapi (fun i c ->
                let side = if i < fCount then Friendly else Hostile
                Agent.create (AgentId.ofInt i) side c)

        let! seed = Gen.choose (0, System.Int32.MaxValue)

        let world: WorldState =
            { Tick = 0L
              Bounds = bounds
              Terrain = terrain
              Agents = agents
              TacticalKnowledge = [||]
              HostileTacticalKnowledge = [||]
              Random = SplitMix64.create (uint64 seed) }

        let! tickCount = Gen.choose (5, 15)

        // Commands only ever address friendlies (a hostile recipient is
        // rejected at intake anyway); targets are arbitrary in-bounds cells.
        let commandGen =
            gen {
                let! tick = Gen.choose (1, tickCount)
                let! agentIdx = Gen.choose (0, max 0 (fCount - 1))
                let! x = Gen.choose (0, bounds.Width - 1)
                let! y = Gen.choose (0, bounds.Height - 1)
                return tick, agentIdx, ({ X = x; Y = y }: Cell)
            }

        let! raw =
            gen {
                let! count = Gen.choose (0, fCount * 3)
                return! Gen.arrayOfLength count commandGen
            }

        return
            { World = world
              Commands = toRecordedCommands agents raw
              TickCount = int64 tickCount }
    }

/// Replays a case into the ordered post-tick states, index 0 = tick 0.
let private statesOf (case: RandomCase) : WorldState[] =
    let commandsForTick t =
        case.Commands |> Array.filter (fun c -> c.Tick = t) |> Array.map (fun c -> c.Command)

    let acc = ResizeArray<WorldState>()
    acc.Add case.World
    let mutable s = case.World

    for tick in 1L .. case.TickCount do
        s <- (Simulation.step SimConfig.standard (commandsForTick tick) s).State
        acc.Add s

    acc.ToArray()

[<Property(MaxTest = 200)>]
let ``every squad contact was seen by a friendly within ExpireAfter, with a non-decreasing LastSeenTick and a valid confidence band`` () =
    Prop.forAll (Arb.fromGen perceptionCaseGen) (fun case ->
        let states = statesOf case
        let bands =
            Set.ofList
                [ PerceptionConfig.ConfidenceFull
                  PerceptionConfig.ConfidenceFull - PerceptionConfig.ConfidenceBandDrop ]

        // Recompute, from the state as it stood at the START of `tick`
        // (= post-tick state `tick - 1`), whether some agent on `side` could
        // see `contact` — the same inputs the Tactical-knowledge phase used
        // that tick, for either side's own picture.
        let seenAt (side: Side) (tick: int64) (contact: AgentId) =
            if tick < 1L || int tick > states.Length - 1 then
                false
            else
                let pre = states.[int tick - 1]

                pre.Agents
                |> Array.exists (fun a ->
                    a.Side = side
                    && Perception.visibleContactsFor pre.Terrain a pre.Agents |> Array.contains contact)

        let groundedPicture (side: Side) (pictureOf: WorldState -> Contact[]) =
            seq { 1 .. states.Length - 1 }
            |> Seq.forall (fun i ->
                let cur = states.[i]
                let prev = states.[i - 1]

                pictureOf cur
                |> Array.forall (fun c ->
                    let withinExpiry = cur.Tick - c.LastSeenTick < int64 PerceptionConfig.ExpireAfter
                    let seenNotFuture = c.LastSeenTick >= 1L && c.LastSeenTick <= cur.Tick
                    let validBand = Set.contains c.Confidence bands
                    let grounded = seenAt side c.LastSeenTick c.Contact

                    let nonDecreasing =
                        match pictureOf prev |> Array.tryFind (fun p -> p.Contact = c.Contact) with
                        | Some p -> c.LastSeenTick >= p.LastSeenTick
                        | None -> true

                    withinExpiry && seenNotFuture && validBand && grounded && nonDecreasing))

        // TASK-034 (backlog B-022, partial): HostileTacticalKnowledge is the
        // identical mergeKnowledge machinery, filtered to Hostile observers —
        // the same grounding property must hold for it too.
        groundedPicture Friendly (fun s -> s.TacticalKnowledge)
        && groundedPicture Hostile (fun s -> s.HostileTacticalKnowledge))

// --- property 7: communication constraints gate order delivery -------------
// TASK-027. `commsCaseGen` is `randomCaseGen` with a random subset of the
// agents comms-blacked-out (`CommunicationAvailable = false`). For every
// post-tick state: (a) a blacked-out agent never holds a `Destination` and
// never leaves its start cell (Command intake accepts its orders, but the
// Communication phase drops them); (b) every `OrderUndelivered` event names a
// blacked-out recipient and follows a `CommandAccepted` for the same
// (command, recipient) on the same tick; (c) no `OrderUndelivered` is ever
// emitted for a comms-available agent.

let private commsCaseGen: Gen<RandomCase> =
    gen {
        let! case = randomCaseGen
        let n = case.World.Agents.Length

        let! flags =
            Gen.arrayOfLength n (Gen.frequency [ 3, Gen.constant true; 1, Gen.constant false ])

        let agents =
            case.World.Agents
            |> Array.mapi (fun i a -> { a with CommunicationAvailable = flags.[i] })

        return { case with World = { case.World with Agents = agents } }
    }

[<Property(MaxTest = 200)>]
let ``a comms-blacked-out agent never receives a command destination and never moves`` () =
    Prop.forAll (Arb.fromGen commsCaseGen) (fun case ->
        match replayOf case with
        | Error e -> failwith $"replay of a generated case failed: {e}"
        | Ok outcome ->
            let startOf =
                case.World.Agents
                |> Array.filter (fun a -> not a.CommunicationAvailable)
                |> Array.map (fun a -> a.Id, a.Position)
                |> Map.ofArray

            // (a) blacked-out agents are inert.
            let inert =
                outcome.TickStates
                |> Array.forall (fun st ->
                    st.Agents
                    |> Array.forall (fun a ->
                        match Map.tryFind a.Id startOf with
                        | Some start -> a.Destination = None && a.Position = start
                        | None -> true))

            let blackedOut = startOf |> Map.toSeq |> Seq.map fst |> Set.ofSeq

            // (b) + (c): every OrderUndelivered names a blacked-out recipient
            // and pairs with a same-tick CommandAccepted; no comms-available
            // agent is ever reported undelivered.
            let byTick =
                outcome.Events |> Array.groupBy (fun e -> e.Tick) |> Map.ofArray

            let deliveryEventsOk =
                outcome.Events
                |> Array.forall (fun e ->
                    match e.Body with
                    | OrderUndelivered(cmd, recipient, UnableToCommunicate) ->
                        Set.contains recipient blackedOut
                        && (Map.tryFind e.Tick byTick
                            |> Option.defaultValue [||]
                            |> Array.exists (fun a ->
                                match a.Body with
                                | CommandAccepted(c, r, _) -> c = cmd && r = recipient
                                | _ -> false))
                    | _ -> true)

            inert && deliveryEventsOk)


// --- property 8: order appraisal outcomes are consistent -----------------
// TASK-028. `appraisalCaseGen` is `perceptionCaseGen` (1-3 friendlies, 1-2
// hostiles on an open-ish grid) with a random non-negative `Discipline` per
// friendly. For every post-tick state and every appraised order:
//   (a) `Refused` / `Unable` write no `Destination`;
//   (b) `Accepted` writes `Destination = Some target`, unless the agent is
//       already on the target (the arrival tick, before the fulfilment
//       housekeeping clears the order);
//   (c) an `Accepted` order's target is still reachable from the agent's
//       current cell, and an `Unable(NoKnownRoute)` order's target is still
//       unreachable — both threat-independent, so robust across contact decay;
//   (d) a `Disposition` is only ever `Some` when the agent holds an `Order`;
//   (e) every `OrderAppraised(a, _, disp)` event matches agent `a`'s stored
//       `Disposition` at the end of the tick that emitted it.
//
// TASK-031 note: this property no longer asserts zero `Random.Draws` — a
// generated friendly/hostile pair can now legitimately end up within
// `CombatConfig.WeaponRange` and line of sight, and the Combat phase draws
// deterministically when that happens (property 10 covers Combat's own
// correctness). `Appraisal.appraise` itself still draws nothing; this
// property was never actually exercising that narrower claim (it observes
// the whole tick, not the leaf function), so nothing is lost by dropping it.

let private appraisalCaseGen: Gen<RandomCase> =
    gen {
        let! case = perceptionCaseGen
        let! disciplines = Gen.arrayOfLength case.World.Agents.Length (Gen.choose (0, 8))

        let agents =
            case.World.Agents
            |> Array.mapi (fun i a ->
                if a.Side = Friendly then { a with Discipline = disciplines.[i] } else a)

        return { case with World = { case.World with Agents = agents } }
    }

[<Property(MaxTest = 200)>]
let ``every appraisal outcome is consistent with a fresh recompute`` () =
    Prop.forAll (Arb.fromGen appraisalCaseGen) (fun case ->
        match replayOf case with
        | Error e -> failwith $"replay of a generated case failed: {e}"
        | Ok outcome ->
            let budget = case.World.Bounds.Width * case.World.Bounds.Height

            let reachable (w: WorldState) (from: Cell) (target: Cell) =
                from = target
                || (match Pathfinding.findWithin w.Terrain from target budget with
                    | Found(cells, _) -> cells.Length >= 2
                    | _ -> false)

            let dispositionsOk =
                outcome.TickStates
                |> Array.forall (fun st ->
                    st.Agents
                    |> Array.forall (fun a ->
                        match a.Order, a.Disposition with
                        | Some { Intent = MoveTo target }, Some d ->
                            match d with
                            | Accepted ->
                                (a.Destination = Some target || a.Position = target)
                                && reachable st a.Position target
                            | Refused(RouteTooExposed _, _) -> a.Destination = None
                            | Refused(NoKnownRoute, _)
                            | Refused(TargetNotKnown, _)
                            | Refused(CriticallyWounded, _) -> false
                            | Unable(NoKnownRoute, _) ->
                                a.Destination = None && not (reachable st a.Position target)
                            | Unable _ -> a.Destination = None
                        // This generator (appraisalCaseGen) only ever issues
                        // MoveTo orders (TASK-037's Suppress is untested by
                        // this property).
                        | Some { Intent = Suppress _ }, Some _ -> false
                        | None, Some _ -> false
                        | _ -> true))

            // (e): re-derive each tick's events and check the OrderAppraised
            // ones against the post-tick disposition.
            let mutable prev = case.World
            let mutable eventsOk = true

            for tick in 1L .. case.TickCount do
                let cmds =
                    case.Commands
                    |> Array.filter (fun c -> c.Tick = tick)
                    |> Array.map (fun c -> c.Command)

                let r = Simulation.step SimConfig.standard cmds prev

                for e in r.Events do
                    match e.Body with
                    | OrderAppraised(agentId, _, disp) ->
                        if
                            not (
                                r.State.Agents
                                |> Array.exists (fun a -> a.Id = agentId && a.Disposition = Some disp)
                            )
                        then
                            eventsOk <- false
                    | _ -> ()

                prev <- r.State

            dispositionsOk && eventsOk)

// --- property 9: commitment derivation is consistent ----------------------
// TASK-030. Reuses `appraisalCaseGen` (property 8's generator: 1-3
// friendlies with a random Discipline, 1-2 hostiles). For every post-tick
// state and every agent: `Commitment.ofAgent` applied to that agent's
// (Order, Disposition, Destination) is `Moving` iff `Order = Some _ &&
// Disposition = Some Accepted && Destination = Some _` — the derivation
// `commitmentAndLocalAction` relies on can never disagree with the fields it
// reads, because it is a pure function of them, not independent state.
//
// TASK-031 note: `Commitment.ofAgent` itself still draws nothing (it is
// still a pure function of three already-canonical fields), but the
// generated world as a whole can now legitimately draw when a friendly and
// hostile end up within `CombatConfig.WeaponRange` and line of sight — see
// property 8's identical note.

[<Property(MaxTest = 200)>]
let ``every agent's derived Commitment matches its Order, Disposition, and Destination`` () =
    Prop.forAll (Arb.fromGen appraisalCaseGen) (fun case ->
        match replayOf case with
        | Error e -> failwith $"replay of a generated case failed: {e}"
        | Ok outcome ->
            let commitmentsOk =
                outcome.TickStates
                |> Array.forall (fun st ->
                    st.Agents
                    |> Array.forall (fun a ->
                        let expectedMoving =
                            match a.Order, a.Disposition, a.Destination with
                            | Some _, Some Accepted, Some _ -> true
                            | _ -> false

                        match Commitment.ofAgent a.Order a.Disposition a.Destination with
                        | Moving _ -> expectedMoving
                        | Holding -> not expectedMoving
                        // This generator (appraisalCaseGen) only ever issues
                        // MoveTo orders (TASK-037's Suppress is untested by
                        // this property) — Suppressing here is unreachable,
                        // and false fails loudly rather than silently passing
                        // if that ever stops being true.
                        | Suppressing _ -> false))

            commitmentsOk)

// --- property 10: hitscan combat outcomes are deterministic --------------
// TASK-031. Reuses `appraisalCaseGen` (properties 8/9's generator — a
// friendly/hostile pair can now legitimately end up within
// `CombatConfig.WeaponRange` and line of sight). For every tick and every
// `ShotFired` event, in emission order (ascending shooter agent id):
// recomputing `Combat.hitChance` from the shooter's and target's post-tick
// `Position` (unchanged between the Combat phase and `Output` — nothing
// after Combat moves an agent) and redrawing from the pre-tick `Random`
// state reproduces the same `hit` outcome, and the reconstructed draw count
// matches `Random.Draws` exactly — no extra or missing draws.

[<Property(MaxTest = 200)>]
let ``every ShotFired outcome is consistent with a fresh recompute and the exact draw count`` () =
    Prop.forAll (Arb.fromGen appraisalCaseGen) (fun case ->
        let mutable prev = case.World
        let mutable ok = true

        for tick in 1L .. case.TickCount do
            let cmds =
                case.Commands
                |> Array.filter (fun c -> c.Tick = tick)
                |> Array.map (fun c -> c.Command)

            let r = Simulation.step SimConfig.standard cmds prev

            let shots =
                r.Events
                |> Array.choose (fun e ->
                    match e.Body with
                    | ShotFired(shooter, target, hit) -> Some(shooter, target, hit)
                    | _ -> None)

            let mutable random = prev.Random

            for (shooter, target, hit) in shots do
                match
                    r.State.Agents |> Array.tryFind (fun a -> a.Id = shooter),
                    r.State.Agents |> Array.tryFind (fun a -> a.Id = target)
                with
                | Some s, Some t ->
                    let chance = Combat.hitChance r.State.Terrain s.Position t.Position
                    let struct (draw, next) = RandomStream.next random
                    random <- next
                    if ((draw % 1000UL) < uint64 chance) <> hit then
                        ok <- false
                | _ -> ok <- false

            if r.State.Random.Draws <> random.Draws then
                ok <- false

            prev <- r.State

        ok)

// --- property 11: suppression is a pure function of this tick's shots -----
// TASK-032. Reuses `appraisalCaseGen` unchanged (property 10's generator
// already produces `ShotFired` events some of the time). For every tick and
// every agent: starting from that agent's pre-tick `Suppression`, folding
// `Suppression.raise` with `Suppression.gain` (recomputed from each targeting
// `ShotFired` event's post-tick shooter/target `Position` — the property 10
// precedent that positions are unchanged between Combat and Output) for
// every shot that targeted it this tick, then applying one `Suppression.decay`
// (State consequences, unconditional every tick), reproduces the actual
// post-tick `Suppression` exactly.

[<Property(MaxTest = 200)>]
let ``every agent's post-tick Suppression is reproducible from a fresh recompute`` () =
    Prop.forAll (Arb.fromGen appraisalCaseGen) (fun case ->
        let mutable prev = case.World
        let mutable ok = true

        for tick in 1L .. case.TickCount do
            let cmds =
                case.Commands
                |> Array.filter (fun c -> c.Tick = tick)
                |> Array.map (fun c -> c.Command)

            let r = Simulation.step SimConfig.standard cmds prev

            let shots =
                r.Events
                |> Array.choose (fun e ->
                    match e.Body with
                    | ShotFired(shooter, target, hit) -> Some(shooter, target, hit)
                    | _ -> None)

            for a in prev.Agents do
                let gains =
                    shots
                    |> Array.filter (fun (_, target, _) -> target = a.Id)
                    |> Array.choose (fun (shooter, target, hit) ->
                        match
                            r.State.Agents |> Array.tryFind (fun x -> x.Id = shooter),
                            r.State.Agents |> Array.tryFind (fun x -> x.Id = target)
                        with
                        | Some s, Some t -> Some(Suppression.gain r.State.Terrain s.Position t.Position hit)
                        | _ -> None)

                let expected =
                    gains |> Array.fold Suppression.raise a.Suppression |> Suppression.decay

                match r.State.Agents |> Array.tryFind (fun x -> x.Id = a.Id) with
                | Some post when post.Suppression = expected -> ()
                | _ -> ok <- false

            prev <- r.State

        ok)

// --- property 12: stress and the suppression-band latch are pure functions
// --- of this tick's inputs -------------------------------------------------
// TASK-033. Reuses `appraisalCaseGen` unchanged. For every tick and every
// agent:
//
//   * Stress: starting from the agent's pre-tick Stress, one Stress.gain
//     (this tick's post-Perception VisibleContacts non-empty — Perception
//     runs before State consequences and nothing after it touches
//     VisibleContacts) folded via Stress.raise, then one Stress.decay (State
//     consequences, unconditional every tick), reproduces the actual
//     post-tick Stress exactly — the property 11 Suppression precedent.
//   * SuppressionBand: the hysteresis latch computed from the agent's
//     pre-tick Suppression and pre-tick SuppressionBand (both read by the
//     Appraisal phase before Combat / State consequences can change them)
//     reproduces the actual post-tick SuppressionBand exactly.

[<Property(MaxTest = 200)>]
let ``every agent's post-tick Stress and SuppressionBand are reproducible from a fresh recompute`` () =
    Prop.forAll (Arb.fromGen appraisalCaseGen) (fun case ->
        let mutable prev = case.World
        let mutable ok = true

        for tick in 1L .. case.TickCount do
            let cmds =
                case.Commands
                |> Array.filter (fun c -> c.Tick = tick)
                |> Array.map (fun c -> c.Command)

            let r = Simulation.step SimConfig.standard cmds prev

            for a in prev.Agents do
                match r.State.Agents |> Array.tryFind (fun x -> x.Id = a.Id) with
                | Some post ->
                    let inContact = post.VisibleContacts.Length > 0
                    let expectedStress = Stress.gain inContact |> Stress.raise a.Stress |> Stress.decay

                    let expectedBand =
                        if a.Suppression >= AppraisalConfig.SuppressionBandEnter then true
                        elif a.Suppression <= AppraisalConfig.SuppressionBandExit then false
                        else a.SuppressionBand

                    if post.Stress <> expectedStress || post.SuppressionBand <> expectedBand then
                        ok <- false
                | None -> ok <- false

            prev <- r.State

        ok)

// --- property 13: wounds and bleed-out are pure functions of this tick's
// --- shots -------------------------------------------------------------
// TASK-045 (backlog B-031). The property 11 Suppression precedent, folded
// instead of summed: Simulation.combat mutates its working `agents` array in
// place as it iterates shooters in ascending id order, so a target hit by
// more than one shooter the same tick accumulates each `Casualty.wound` in
// that same order (never a single combined delta) -- reproduced here by
// folding one `Casualty.wound` per qualifying `ShotFired(_, target, true)`
// this tick over the agent's pre-tick `Vitals`, then applying one
// `Casualty.tickBleedOut` (State consequences, unconditional every tick,
// a no-op on `Alive`). A pre-tick non-`Alive` agent is never a valid Combat
// target at all (Simulation.combat's own Alive-only candidate filter), so
// it never has a qualifying hit to fold in this property either -- this
// also indirectly re-proves that invariant across every generated case.

[<Property(MaxTest = 200)>]
let ``every agent's post-tick Vitals is reproducible from a fresh recompute`` () =
    Prop.forAll (Arb.fromGen appraisalCaseGen) (fun case ->
        let mutable prev = case.World
        let mutable ok = true

        for tick in 1L .. case.TickCount do
            let cmds =
                case.Commands
                |> Array.filter (fun c -> c.Tick = tick)
                |> Array.map (fun c -> c.Command)

            let r = Simulation.step SimConfig.standard cmds prev

            // Qualifying hits against a target this tick, in the same
            // ascending-shooter-id order Simulation.combat's own loop applies
            // them (ShotFired events are emitted in that same order).
            let hitsAgainst (id: AgentId) =
                r.Events
                |> Array.filter (fun e ->
                    match e.Body with
                    | ShotFired(_, target, true) -> target = id
                    | _ -> false)

            for a in prev.Agents do
                let afterCombat = hitsAgainst a.Id |> Array.fold (fun v _ -> Casualty.wound v) a.Vitals
                let expected = Casualty.tickBleedOut afterCombat

                match r.State.Agents |> Array.tryFind (fun x -> x.Id = a.Id) with
                | Some post when post.Vitals = expected -> ()
                | _ -> ok <- false

            prev <- r.State

        ok)
