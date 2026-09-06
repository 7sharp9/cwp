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
// support are covered here: commitments, appraisal, death, and objectives
// (also on the docs/09 section 2.2 list) do not exist yet.
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
