namespace CommandoWar.Sim

/// Non-authoritative host configuration. `TicksPerSecond` is a scheduling
/// hint for hosts and does not affect authoritative outcomes
/// (docs/03_ARCHITECTURE.md section 6). The deterministic seed lives on
/// `WorldState.Random` (set at construction), not here; scenario
/// configuration arrives with later content tasks.
type SimConfig = { TicksPerSecond: int }

[<RequireQualifiedAccess>]
module SimConfig =

    /// The nominal 20 ticks per second baseline.
    let standard: SimConfig = { TicksPerSecond = 20 }

/// The result of advancing the simulation by exactly one integer tick.
type StepResult =
    { State: WorldState
      Events: DomainEvent[]
      Snapshot: RenderSnapshot
      /// The phases executed this tick, in execution order. Diagnostic:
      /// lets tests and replay assert the phase schedule was honoured.
      PhaseTrace: Phase[]
      /// Canonical authoritative-state hash of `State`, computed after the
      /// Output phase (docs/04_SIMULATION_SPEC.md section 12.11). The
      /// authoritative outputs above are finalised before this is computed and
      /// do not depend on its value; it is a read-only checkpoint for replay
      /// and divergence diagnosis.
      StateHash: StateHash }

/// Why a headless world could not be constructed.
type WorldError =
    | EmptyGrid of bounds: GridBounds
    | DuplicateAgentId of agent: AgentId
    | AgentOutOfBounds of agent: AgentId * position: Cell
    /// Two or more agents were placed on the same cell at construction
    /// (TASK-022). `cell` is the shared cell and `agents` its occupants in
    /// ascending id order. `Scenario.validate` already rejects a cell-sharing
    /// deployment (docs/04 section 21); this closes the same gap on the direct
    /// `World.create` / `World.ofScenario` path, so the one-live-agent-per-cell
    /// invariant the Navigation and movement phase relies on (docs/04 section
    /// 20) holds from tick 0 by construction, not only by convention. When two
    /// cells are shared the lowest row-major cell is reported.
    | AgentsShareCell of cell: Cell * agents: AgentId[]

[<RequireQualifiedAccess>]
module World =

    /// Shared construction core for `create` and `ofScenario`. Applies the
    /// empty-grid, duplicate-id, and in-bounds guards, then builds the world
    /// at tick 0 with the given terrain and a SplitMix64 stream seeded by
    /// `seed`. Agents are sorted by ascending id.
    let private build
        (bounds: GridBounds)
        (terrain: Terrain)
        (seed: uint64)
        (agents: AgentState list)
        : Result<WorldState, WorldError> =
        if bounds.Width <= 0 || bounds.Height <= 0 then
            Error(EmptyGrid bounds)
        else
            let sorted = agents |> List.sortBy (fun a -> a.Id)

            let duplicate =
                sorted
                |> List.pairwise
                |> List.tryPick (fun (a, b) -> if a.Id = b.Id then Some a.Id else None)

            match duplicate with
            | Some id -> Error(DuplicateAgentId id)
            | None ->
                match sorted |> List.tryFind (fun a -> not (GridBounds.contains a.Position bounds)) with
                | Some a -> Error(AgentOutOfBounds(a.Id, a.Position))
                | None ->
                    let shared =
                        sorted
                        |> List.groupBy (fun a -> a.Position)
                        |> List.filter (fun (_, occ) -> List.length occ > 1)
                        |> List.sortBy (fun (c, _) -> c.Y, c.X)
                        |> List.tryHead

                    match shared with
                    | Some(cell, occ) ->
                        Error(AgentsShareCell(cell, occ |> List.map (fun a -> a.Id) |> List.sort |> List.toArray))
                    | None ->
                        Ok
                            { Tick = 0L
                              Bounds = bounds
                              Terrain = terrain
                              Agents = List.toArray sorted
                              Random = SplitMix64.create seed }

    /// Builds a validated world at tick 0 with a SplitMix64 random stream
    /// seeded by `seed` and empty (flat, fully passable, transparent,
    /// uncovered) terrain for `bounds`. Agents are sorted by ascending id.
    /// Fails explicitly on an empty grid, duplicate ids, or an agent placed
    /// outside the grid.
    let create (bounds: GridBounds) (seed: uint64) (agents: AgentState list) : Result<WorldState, WorldError> =
        build bounds (Terrain.empty bounds) seed agents

    /// Builds the authoritative world at tick 0 from a validated scenario
    /// (docs/04_SIMULATION_SPEC.md section 21). Friendly then enemy deployments
    /// become agents ordered ascending by id; the scenario's validated
    /// terrain grid (`scenario.Terrain`, empty when the scenario authored no
    /// terrain layer) becomes `WorldState.Terrain`. Both are handed to the
    /// shared construction core, which owns the empty-grid, duplicate-id, and
    /// in-bounds guards. The scenario's objectives, areas, targets, and rules
    /// are not consumed here, and no tick phase reads the terrain yet: line of
    /// sight and pathfinding are out of scope (backlog B-009, B-010) and
    /// objective evaluation is deferred (B-032).
    let ofScenario (scenario: Scenario) (seed: uint64) : Result<WorldState, WorldError> =
        let agents =
            Array.append scenario.FriendlyDeployments scenario.EnemyDeployments
            |> Array.sortBy (fun d -> d.Agent)
            |> Array.map (fun d -> Agent.create d.Agent d.Side d.Cell)
            |> Array.toList

        build scenario.Map scenario.Terrain seed agents

[<RequireQualifiedAccess>]
module Setup =

    /// A deterministic six-agent friendly world for headless tests and the
    /// framework spikes, with a SplitMix64 stream seeded by `seed`. Agents
    /// 0..5 occupy column x = 0, rows y = 0..5, so the grid must be at least
    /// 1 wide and 6 tall.
    let sixAgentWorld (bounds: GridBounds) (seed: uint64) : WorldState =
        let agents =
            [ for i in 0..5 -> Agent.create (AgentId.ofInt i) Friendly { X = 0; Y = i } ]

        match World.create bounds seed agents with
        | Ok world -> world
        | Error err -> invalidArg (nameof bounds) $"grid too small for the six-agent world: {err}"

/// The authoritative fixed-tick simulation step and its phase runner.
[<RequireQualifiedAccess>]
module Simulation =

    /// Mutable-but-contained accumulator for one step. It never escapes
    /// `step`; the input `WorldState` is never mutated (both command intake
    /// and movement copy the agent array before writing).
    type private StepState =
        { Tick: int64
          Bounds: GridBounds
          /// The authoritative terrain for this run. Immutable within a run;
          /// the Navigation and movement phase reads it for pathfinding.
          Terrain: Terrain
          mutable Agents: AgentState[]
          /// The deterministic stream for this tick. No phase draws from it
          /// yet; a future gameplay phase reassigns it after each draw so the
          /// advanced state is carried forward.
          mutable Random: RandomState
          mutable EventsRev: DomainEvent list
          mutable Snapshot: RenderSnapshot
          mutable TraceRev: Phase list }

    let private emit (body: EventBody) (s: StepState) =
        s.EventsRev <- { Tick = s.Tick; Body = body } :: s.EventsRev

    /// The first `AgentId` that appears more than once in `ids`, in list
    /// order, or `None` if every id is distinct.
    let private firstDuplicate (ids: AgentId list) : AgentId option =
        let rec go seen =
            function
            | [] -> None
            | a :: rest -> if Set.contains a seen then Some a else go (Set.add a seen) rest

        go Set.empty ids

    // --- Phase: command intake ------------------------------------------------
    // Validate and apply move commands (docs/04 section 12.1: "reject
    // malformed, unauthorised, impossible-to-address, or duplicate commands;
    // record accepted commands before effects are applied"; docs/04 section 2:
    // "a command becomes eligible on a specified tick"). Commands arrive
    // already sorted by command id (Simulation.step). Validation order:
    //
    //   1. batch-level: a CommandId that appears more than once in this tick's
    //      batch rejects EVERY command in that group (DuplicateCommandId), none
    //      processed — order-independent, since the batch sort is stable and a
    //      "first wins" rule would let a caller change the result by reordering
    //      an invalid batch. Cross-tick duplicate-id tracking is a replay-log
    //      invariant (Replay.validate / DuplicateCommandIdInLog), NOT
    //      authoritative state — WorldState holds no command history (TASK-024).
    //   2. per surviving command, whole-command checks that short-circuit with
    //      one CommandRejected and no per-recipient events:
    //        a. issue-tick eligibility (TASK-024): PlayerCommand.IssuedAtTick
    //           must lie in [0, s.Tick] — a command issued in the future or
    //           before tick 0 is rejected IssueTickOutOfRange. A command issued
    //           on an earlier tick and delivered now IS accepted: staleness is
    //           an appraisal judgement (B-017), not a validation rule, so no
    //           give-up horizon is applied here. IssuedAtTick is otherwise
    //           inert until appraisal reads it.
    //        b. empty recipients (EmptyRecipients) -> repeated recipient
    //           (DuplicateRecipient) -> target in bounds (TargetOutOfBounds,
    //           checked once — a MoveTo target is equally out of bounds for
    //           every recipient).
    //   3. per-recipient checks in ascending AgentId order regardless of
    //      authoring order (stable entity ordering): unknown agent
    //      (UnknownAgent) -> hostile-side agent (UnauthorisedRecipient) ->
    //      accept. CommandAccepted is emitted BEFORE the destination is
    //      written, per 12.1.
    //
    // The delivery tick (RecordedCommand.Tick in Replay.fs) and the envelope's
    // IssuedAtTick are independent concepts: the caller / replay runner owns
    // when a command reaches this phase; IssuedAtTick only records when it was
    // issued. The legacy .cwlog format collapses the two to its single field.
    //
    // The agent array is copied once per phase invocation, not once per
    // accepted command (content/benchmarks/BASELINE.md recorded the old
    // per-command Array.copy as an O(n^2) allocation hot spot). Agent identity
    // and array order are stable within command intake — only Destination is
    // written, no agent is added or removed — so one AgentId -> index map
    // stays valid for the whole batch, and a later command still sees an
    // earlier command's applied destination.
    let private commandIntake (commands: PlayerCommand list) (s: StepState) =
        let agents = Array.copy s.Agents

        let indexOf: Map<AgentId, int> =
            agents |> Array.mapi (fun i a -> a.Id, i) |> Map.ofArray

        let duplicatedIds: Set<CommandId> =
            commands
            |> List.countBy (fun c -> c.Id)
            |> List.choose (fun (id, n) -> if n > 1 then Some id else None)
            |> Set.ofList

        for cmd in commands do
            if Set.contains cmd.Id duplicatedIds then
                emit (CommandRejected(cmd.Id, DuplicateCommandId cmd.Id)) s
            elif cmd.IssuedAtTick < 0L || cmd.IssuedAtTick > s.Tick then
                emit (CommandRejected(cmd.Id, IssueTickOutOfRange(cmd.IssuedAtTick, s.Tick))) s
            else
                match cmd.Intent with
                | MoveTo target ->
                    match cmd.Recipients with
                    | [] -> emit (CommandRejected(cmd.Id, EmptyRecipients)) s
                    | recipients ->
                        match firstDuplicate recipients with
                        | Some repeated -> emit (CommandRejected(cmd.Id, DuplicateRecipient repeated)) s
                        | None when not (GridBounds.contains target s.Bounds) ->
                            emit (CommandRejected(cmd.Id, TargetOutOfBounds target)) s
                        | None ->
                            for recipient in recipients |> List.sortBy AgentId.value do
                                match Map.tryFind recipient indexOf with
                                | None -> emit (CommandRejected(cmd.Id, UnknownAgent recipient)) s
                                | Some idx when agents.[idx].Side = Hostile ->
                                    emit (CommandRejected(cmd.Id, UnauthorisedRecipient recipient)) s
                                | Some idx ->
                                    emit (CommandAccepted(cmd.Id, recipient, target)) s
                                    agents.[idx] <- { agents.[idx] with Destination = Some target }

        s.Agents <- agents

    // --- Phase: navigation and movement -------------------------------------
    // Consumes the TASK-013 `Pathfinding` module (docs/04 section 8 "Initial
    // movement progression", steps 2, 3, 4, 5, 6). For every agent with a
    // destination, in ascending agent id order, first computes a movement
    // intent (`MoveOutcome`) — reusing the cached `Route` when its cursor
    // still tracks the agent, it still targets the current destination, and
    // its next cell is still passable; otherwise recomputing a path with
    // `Pathfinding.findWithin` over the authoritative terrain (a new
    // destination, or step 6, replan on an invalidated next cell) — then
    // resolves same-tick contention over a shared next cell (step 3,
    // TASK-017) before applying the surviving moves, in ascending agent id
    // order:
    //   * an agent still mid-edge (its `Progress` plus this tick's increment
    //     has not yet reached the next cell's threshold, TASK-018) simply
    //     accumulates progress; it is not a claimant of anything this tick,
    //     since it is not entering a cell;
    //   * a mover that WOULD complete its edge this tick, with no rival for
    //     its next cell or the winner of one, enters it and emits
    //     `MovementStepped`, then `MovementCompleted` on the arrival tick,
    //     resetting progress for the next edge and clearing the destination
    //     and the route on arrival;
    //   * a mover that would complete its edge but loses a contested cell to
    //     another agent this tick stays put and emits `MovementYielded`; its
    //     progress freezes (it does not advance, so it does not accumulate),
    //     so it retries with the same progress next tick, once the winner has
    //     vacated the cell;
    //   * a destination with no path emits `MovementBlocked`, clears the
    //     destination, and resets progress.
    //
    // Reservation is a same-tick derived resolution, not persisted state: the
    // winner of a contested cell is the mover with the fewest remaining route
    // steps (closest to its destination), ties broken by ascending agent id —
    // computed fresh every tick from already-canonical fields (`Position`,
    // `Progress`, `Destination`, `Terrain` via `Route`), so nothing new is
    // booked across ticks (TASK-017 ledger note; TASK-018 extends the
    // contention *test* to "would complete this tick" without changing this
    // argument — `Progress` is already canonical, not newly derived). It
    // provably terminates for a shared-target-cell contest between two
    // agents that both complete the same tick: the winner always advances, so
    // the sum of every completing agent's remaining route length strictly
    // decreases each tick a contest is resolved.
    //
    // Realised by TASK-022 (backlog B-047): runtime cell-occupancy correctness.
    // Rival arbitration (above) only decides *which* completing agent may claim
    // a contested cell; it never checks whether that cell is already held by a
    // stationary agent. A second stage 2b resolution — the "vacation chain" —
    // runs after rival arbitration and before Pass 3:
    //   * `M0` = the completing agents that did not lose a rival contest (at
    //     most one per target cell). `occupant(c)` = the unique agent whose
    //     pre-tick `Position` is `c` (pre-tick uniqueness is the invariant this
    //     stage preserves: true by construction for every world builder and
    //     `Scenario.validate`, and preserved tick to tick by this rule).
    //   * an agent `a` in `M0` may move iff the chain
    //         a -> occupant(next a) -> occupant(next (occupant (next a))) -> ...
    //     terminates at an agent whose next cell has no occupant — i.e. it
    //     neither hits a cycle nor an agent that is not itself a moving
    //     candidate. Computed as an additive fixpoint (monotone,
    //     order-independent, <= n rounds): seed `S` with every `a in M0` whose
    //     `next a` is unoccupied, then repeatedly add every `a in M0` whose
    //     `next a` is held by an agent already in `S`. Movers = `S`;
    //     `obstructedBy` maps every `a in M0 \ S` to `occupant(next a).Id`.
    //   * two-agent swaps and n-agent rotation cycles fall out with no special
    //     case: no member of a pure cycle is ever seeded or added, so all
    //     freeze. TASK-022 deliberately does NOT add simultaneous rotation /
    //     atomic multi-agent swap — that needs an atomic-swap primitive and a
    //     tactical justification, neither of which exists.
    //   * a follow chain (each agent's next cell is the one ahead, the lead
    //     cell free) resolves the whole chain in a single tick: the lead is
    //     seeded, then each follower in turn, and Pass 3 applies the moves from
    //     precomputed decisions so its ascending-id application order cannot
    //     create a transient collision.
    // An `Advancing` agent found in `obstructedBy` freezes exactly like a
    // `yieldedTo` loser (`Progress = startProgress`, `Route = Some r` written
    // back, `Position` / `Destination` unchanged) and emits `MovementObstructed`.
    // Termination of the fixpoint is trivial (finite monotone). It does NOT
    // guarantee an obstructed agent ever completes: an agent permanently
    // blocked by one that never moves retries — and emits `MovementObstructed`
    // — every tick, forever. Routing *around* a live agent is the cooperative
    // pathfinder TASK-022 forbids; noticing a persistent stall and
    // re-appraising the order is a perception / appraisal concern (B-015 /
    // B-017), named here, not built here.
    //
    // Sub-cell movement progress (TASK-018, step 3 "reserve only the
    // immediate next destination", steps 4-5 "advance movement progress by an
    // integer amount each tick... enter the next cell when progress reaches
    // the threshold"): the threshold for entering a cell is
    // `Terrain.moveCost` of that cell — the same value `Pathfinding` already
    // uses as its A* edge weight, not a new concept — and the per-tick
    // increment is `Terrain.BaseMoveCost`. Progress is scoped to the current
    // edge only: it resets to 0 whenever that edge changes (a fresh route is
    // computed, the agent enters a cell, arrives, or is blocked), and is
    // genuinely new canonical state (`AgentState.Progress`,
    // `Canonical.FormatVersion` 2) because — unlike `Route` — it cannot be
    // recomputed from `Position` alone.
    //
    // Formation slots are B-011d (split from B-011c by TASK-018, which lands
    // sub-cell progress only). `AgentState.Route` is still a non-canonical
    // derived cache (see `MovementPath`).

    /// One agent's movement outcome for this tick, computed in Pass 1 before
    /// same-tick contention resolution (Pass 2). Not persisted: recomputed
    /// fresh every tick from already-canonical/derived fields only.
    type private MoveOutcome =
        /// No destination.
        | Idle
        /// Already at the destination.
        | Arrived of at: Cell
        /// No path to the destination.
        | Blocked of at: Cell * target: Cell
        /// Following `route` toward `next`, with `startProgress` toward it
        /// already accumulated (0 when this tick started a fresh edge — a
        /// new route, or a replan — regardless of the agent's prior
        /// `Progress`, which belonged to a different edge). Pending
        /// resolution: an agent whose `startProgress + Terrain.BaseMoveCost`
        /// reaches the next cell's `Terrain.moveCost` threshold this tick is
        /// a claimant in Pass 2; one that does not simply accumulates
        /// progress in Pass 3 with no contention possible.
        | Advancing of route: MovementPath * next: Cell * destination: Cell * startProgress: int

    let private navigationAndMovement (s: StepState) =
        let terrain = s.Terrain

        // The full-grid ceiling from content/benchmarks/BASELINE.md: a single
        // legitimate query on an adversarial map can close most of the grid, so
        // a per-agent budget must not be cut below it.
        let budget = terrain.Bounds.Width * terrain.Bounds.Height

        let agents = Array.copy s.Agents

        // Pass 1: each agent's movement intent, computed independently (no
        // mutation, no event) from already-canonical/derived fields only.
        // `startProgress` is 0 whenever this tick starts a fresh edge (a new
        // route, or a replan): the agent's prior `Progress` belonged to a
        // *different* edge and does not carry over.
        let intents =
            agents
            |> Array.map (fun a ->
                match a.Destination with
                | None -> Idle
                | Some dest when a.Position = dest -> Arrived a.Position
                | Some dest ->
                    let cached =
                        match a.Route with
                        | Some r when
                            r.Cursor >= 0
                            && r.Cursor + 1 < r.Cells.Length
                            && r.Cells.[r.Cursor] = a.Position
                            && r.Cells.[r.Cells.Length - 1] = dest
                            && Terrain.passable terrain r.Cells.[r.Cursor + 1]
                            ->
                            Some r
                        | _ -> None

                    let route =
                        match cached with
                        | Some r -> Some r
                        | None ->
                            match Pathfinding.findWithin terrain a.Position dest budget with
                            | Found(cells, cost) when cells.Length >= 2 ->
                                Some { Cells = cells; Cursor = 0; Cost = cost }
                            | Found _
                            | NoPath
                            | BudgetExhausted _
                            | InvalidEndpoint _ -> None

                    match route with
                    | None -> Blocked(a.Position, dest)
                    | Some r ->
                        let startProgress = if cached.IsSome then a.Progress else 0
                        Advancing(r, r.Cells.[r.Cursor + 1], dest, startProgress))

        // An `Advancing` agent whose progress reaches the next cell's
        // threshold this tick — the only agents that can contend for a cell,
        // since only they are actually entering one.
        let wouldComplete (next: Cell) (startProgress: int) =
            startProgress + Terrain.BaseMoveCost >= Terrain.moveCost terrain next

        // Pass 2: reservation, over completing agents only. Group by
        // contested next cell; the mover with the fewest remaining route
        // steps wins, ties broken by ascending agent id; every other
        // claimant yields this tick (see the phase comment above for the
        // termination argument).
        let remaining (r: MovementPath) = r.Cells.Length - 1 - r.Cursor

        let yieldedTo: Map<int, AgentId> =
            intents
            |> Array.indexed
            |> Array.choose (fun (idx, intent) ->
                match intent with
                | Advancing(r, next, _, startProgress) when wouldComplete next startProgress ->
                    Some(idx, agents.[idx].Id, r, next)
                | Advancing _
                | Idle
                | Arrived _
                | Blocked _ -> None)
            |> Array.groupBy (fun (_, _, _, next) -> next)
            |> Array.collect (fun (_, claims) ->
                let winnerIdx, winnerId, _, _ = claims |> Array.minBy (fun (_, id, r, _) -> (remaining r, id))

                claims
                |> Array.filter (fun (idx, _, _, _) -> idx <> winnerIdx)
                |> Array.map (fun (idx, _, _, _) -> idx, winnerId))
            |> Map.ofArray

        // Stage 2b: vacation-chain resolution (TASK-022). Rival arbitration
        // above yields at most one candidate mover per target cell; this stage
        // decides which of those may actually enter, given what the cell's
        // current occupant does. `obstructedBy` maps an agent array index to
        // the id of the stationary agent blocking it, consumed by a new Pass 3
        // arm. The fixpoint specification is in the phase comment above.
        let occupantOf: Map<Cell, int> =
            agents |> Array.mapi (fun i a -> a.Position, i) |> Map.ofArray

        // Candidate movers: completing `Advancing` agents that did not lose a
        // rival contest (at most one per target cell), as `(idx, next cell)`.
        let candidateMovers: (int * Cell)[] =
            intents
            |> Array.indexed
            |> Array.choose (fun (idx, intent) ->
                match intent with
                | Advancing(_, next, _, startProgress) when
                    wouldComplete next startProgress && not (Map.containsKey idx yieldedTo)
                    ->
                    Some(idx, next)
                | Advancing _
                | Idle
                | Arrived _
                | Blocked _ -> None)

        // Additive fixpoint: an index joins `movers` once its next cell is free
        // of every agent, or is held by an agent already known to move. Each
        // round consults only the previous round's set and pre-tick positions,
        // never the iteration order within a round.
        let movers: Set<int> =
            let mutable acc = Set.empty
            let mutable changed = true

            while changed do
                changed <- false

                for idx, next in candidateMovers do
                    if not (Set.contains idx acc) then
                        let free =
                            match Map.tryFind next occupantOf with
                            | None -> true
                            | Some occ -> Set.contains occ acc

                        if free then
                            acc <- Set.add idx acc
                            changed <- true

            acc

        let obstructedBy: Map<int, AgentId> =
            candidateMovers
            |> Array.choose (fun (idx, next) ->
                if Set.contains idx movers then
                    None
                else
                    // Not a mover: `next` therefore has an occupant that does
                    // not vacate this tick (an unoccupied `next` would have
                    // seeded `idx` in round 0).
                    Map.tryFind next occupantOf |> Option.map (fun occ -> idx, agents.[occ].Id))
            |> Map.ofArray

        // Pass 3: apply, in ascending agent id order — the standing
        // movement-event ordering guarantee (Events.fs).
        for idx in 0 .. agents.Length - 1 do
            let a = agents.[idx]

            match intents.[idx] with
            | Idle -> ()
            | Arrived at ->
                agents.[idx] <- { a with Progress = 0; Destination = None; Route = None }
                emit (MovementCompleted(a.Id, at)) s
            | Blocked(at, target) ->
                agents.[idx] <- { a with Progress = 0; Destination = None; Route = None }
                emit (MovementBlocked(a.Id, at, target)) s
            | Advancing(r, next, _, startProgress) when not (wouldComplete next startProgress) ->
                // Still mid-edge: accumulate progress, no cell change, no
                // event (a continuous fact fully recoverable from the
                // resulting `AgentState.Progress`, like an idle agent's tick).
                // `Route = Some r` must still be written back — otherwise
                // next tick's cache check finds no route, recomputes one,
                // and `startProgress` resets to 0 every tick forever.
                agents.[idx] <- { a with Progress = startProgress + Terrain.BaseMoveCost; Route = Some r }
            | Advancing(r, next, dest, startProgress) ->
                match yieldedTo.TryFind idx with
                | Some winnerId ->
                    // Frozen at `startProgress`, not incremented — the agent
                    // did not advance this tick. `startProgress` (not
                    // `a.Progress`) so a replan that starts a fresh edge and
                    // is contested in the same tick still freezes at 0, not a
                    // stale value from the edge it just left. `Route = Some r`
                    // is written back for the same reason as the mid-edge
                    // branch above: otherwise next tick recomputes from
                    // scratch and `startProgress` wrongly resets to 0.
                    agents.[idx] <- { a with Progress = startProgress; Route = Some r }
                    emit (MovementYielded(a.Id, a.Position, next, winnerId)) s
                | None ->
                    match Map.tryFind idx obstructedBy with
                    | Some occupantId ->
                        // The next route cell is held by an agent that did not
                        // vacate it this tick (stage 2b). Freeze exactly like a
                        // rival-contest loser above — `Progress = startProgress`
                        // (not incremented, not reset), `Route = Some r` written
                        // back, `Position` / `Destination` untouched — and retry
                        // the same next cell next tick. Persistent obstruction
                        // is B-015 / B-017 scope, not resolved here.
                        agents.[idx] <- { a with Progress = startProgress; Route = Some r }
                        emit (MovementObstructed(a.Id, a.Position, next, occupantId)) s
                    | None ->
                        let arrived = next = dest

                        agents.[idx] <-
                            { a with
                                Position = next
                                Progress = 0
                                Destination = (if arrived then None else Some dest)
                                Route = (if arrived then None else Some { r with Cursor = r.Cursor + 1 }) }

                        emit (MovementStepped(a.Id, a.Position, next)) s

                        if arrived then
                            emit (MovementCompleted(a.Id, next)) s

        s.Agents <- agents

    // --- Phase: output -----------------------------------------------------
    // Build the render snapshot from authoritative state. Agents are already
    // held in ascending id order; the sort is a cheap defensive guarantee for
    // the contract. Events are reversed to chronological order after the run.
    let private output (s: StepState) =
        s.Snapshot <-
            { Tick = s.Tick
              Agents =
                s.Agents
                |> Array.map (fun a ->
                    { Id = a.Id
                      Side = a.Side
                      Position = a.Position
                      Progress = a.Progress
                      Destination = a.Destination })
                |> Array.sortBy (fun a -> a.Id) }

    // Runs one phase against the accumulator and appends it to the trace.
    // No-op phases are listed individually (no wildcard) so that adding a
    // phase forces a decision here.
    let private runPhase (commands: PlayerCommand list) (s: StepState) (phase: Phase) : unit =
        match phase with
        | CommandIntake -> commandIntake commands s
        | NavigationAndMovement -> navigationAndMovement s
        | Output -> output s
        | Communication
        | Perception
        | TacticalKnowledge
        | Appraisal
        | CommitmentAndLocalAction
        | Combat
        | StateConsequences
        | Mission -> ()

        s.TraceRev <- phase :: s.TraceRev

    /// Advances the world by exactly one integer tick. Runs every phase in
    /// `Phases.order`, processing commands at `CommandIntake` and resolving
    /// movement at `NavigationAndMovement`. Emits ordered domain events and a
    /// render snapshot from authoritative state.
    let step (config: SimConfig) (commands: PlayerCommand[]) (state: WorldState) : StepResult =
        if config.TicksPerSecond <= 0 then
            invalidArg (nameof config) "TicksPerSecond must be positive"

        let nextTick =
            if state.Tick = System.Int64.MaxValue then
                invalidOp "authoritative tick counter overflow"
            else
                state.Tick + 1L

        let ordered =
            commands |> Array.toList |> List.sortBy (fun c -> c.Id)

        let acc =
            { Tick = nextTick
              Bounds = state.Bounds
              Terrain = state.Terrain
              Agents = state.Agents
              Random = state.Random
              EventsRev = []
              Snapshot = { Tick = nextTick; Agents = [||] }
              TraceRev = [] }

        for phase in Phases.order do
            runPhase ordered acc phase

        let finalState =
            { state with
                Tick = nextTick
                Agents = acc.Agents
                Random = acc.Random }

        // Hashing runs strictly after the phase loop. `finalState` is already
        // fully determined; the hash is a read-only checkpoint and no
        // authoritative output depends on its value.
        { State = finalState
          Events = acc.EventsRev |> List.rev |> List.toArray
          Snapshot = acc.Snapshot
          PhaseTrace = acc.TraceRev |> List.rev |> List.toArray
          StateHash = Hashing.canonicalHasher.Hash finalState }
