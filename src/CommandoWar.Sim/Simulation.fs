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

[<RequireQualifiedAccess>]
module World =

    /// Builds a validated world at tick 0 with a SplitMix64 random stream
    /// seeded by `seed`. Agents are sorted by ascending id. Fails explicitly
    /// on an empty grid, duplicate ids, or an agent placed outside the grid.
    let create (bounds: GridBounds) (seed: uint64) (agents: AgentState list) : Result<WorldState, WorldError> =
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
                    Ok
                        { Tick = 0L
                          Bounds = bounds
                          Agents = List.toArray sorted
                          Random = SplitMix64.create seed }

    /// Builds the authoritative world at tick 0 from a validated scenario
    /// (docs/04_SIMULATION_SPEC.md section 21). Friendly then enemy deployments
    /// become agents ordered ascending by id and are handed to `create`, which
    /// owns the empty-grid, duplicate-id, and in-bounds guards. The scenario's
    /// objectives, areas, targets, and rules are not consumed here: per-cell
    /// terrain, line of sight, and pathfinding are out of scope (backlog B-008
    /// to B-010) and objective evaluation is deferred (B-032).
    let ofScenario (scenario: Scenario) (seed: uint64) : Result<WorldState, WorldError> =
        let agents =
            Array.append scenario.FriendlyDeployments scenario.EnemyDeployments
            |> Array.sortBy (fun d -> d.Agent)
            |> Array.map (fun d -> Agent.create d.Agent d.Side d.Cell)
            |> Array.toList

        create scenario.Map seed agents

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

    // --- Phase: command intake ------------------------------------------------
    // Validate and apply move commands, processed in ascending command id.
    // Accepted commands set the target agent's destination; invalid agent or
    // out-of-bounds target commands are rejected explicitly. This is the
    // documented phase at which commands are processed.
    let private commandIntake (commands: PlayerCommand list) (s: StepState) =
        for cmd in commands do
            match cmd.Intent with
            | MoveTo target ->
                match s.Agents |> Array.tryFindIndex (fun a -> a.Id = cmd.Agent) with
                | None -> emit (CommandRejected(cmd.Id, UnknownAgent cmd.Agent)) s
                | Some _ when not (GridBounds.contains target s.Bounds) ->
                    emit (CommandRejected(cmd.Id, TargetOutOfBounds target)) s
                | Some idx ->
                    let agents = Array.copy s.Agents
                    agents.[idx] <- { agents.[idx] with Destination = Some target }
                    s.Agents <- agents
                    emit (CommandAccepted(cmd.Id, cmd.Agent, target)) s

    // --- Phase: navigation and movement -------------------------------------
    // Advance every agent with a destination by one placeholder step, in
    // ascending agent id order. Emits a step event, then a completion event
    // on the tick the destination is reached, and clears the destination.
    let private navigationAndMovement (s: StepState) =
        let agents = Array.copy s.Agents

        for idx in 0 .. agents.Length - 1 do
            let a = agents.[idx]

            match a.Destination with
            | None -> ()
            | Some dest when a.Position = dest ->
                agents.[idx] <- { a with Destination = None }
                emit (MovementCompleted(a.Id, a.Position)) s
            | Some dest ->
                let next = PlaceholderMovement.nextCell a.Position dest
                let arrived = next = dest
                agents.[idx] <- { a with Position = next; Destination = (if arrived then None else Some dest) }
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
