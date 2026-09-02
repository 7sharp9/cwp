namespace CommandoWar.Sim

/// Non-authoritative host configuration. `TicksPerSecond` is a scheduling
/// hint for hosts and does not affect authoritative outcomes
/// (docs/03_ARCHITECTURE.md section 6). Seed and scenario configuration
/// arrive with the determinism harness task (backlog TASK-003).
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
      PhaseTrace: Phase[] }

/// Why a headless world could not be constructed.
type WorldError =
    | EmptyGrid of bounds: GridBounds
    | DuplicateAgentId of agent: AgentId
    | AgentOutOfBounds of agent: AgentId * position: Cell

[<RequireQualifiedAccess>]
module World =

    /// Builds a validated world at tick 0. Agents are sorted by ascending
    /// id. Fails explicitly on an empty grid, duplicate ids, or an agent
    /// placed outside the grid.
    let create (bounds: GridBounds) (agents: AgentState list) : Result<WorldState, WorldError> =
        if bounds.Width <= 0 || bounds.Height <= 0 then
            Error(EmptyGrid bounds)
        else
            let sorted = agents |> List.sortBy (fun a -> AgentId.value a.Id)

            let duplicate =
                sorted
                |> List.pairwise
                |> List.tryPick (fun (a, b) -> if a.Id = b.Id then Some a.Id else None)

            match duplicate with
            | Some id -> Error(DuplicateAgentId id)
            | None ->
                match sorted |> List.tryFind (fun a -> not (GridBounds.contains a.Position bounds)) with
                | Some a -> Error(AgentOutOfBounds(a.Id, a.Position))
                | None -> Ok { Tick = 0L; Bounds = bounds; Agents = List.toArray sorted }

[<RequireQualifiedAccess>]
module Setup =

    /// A deterministic six-agent friendly world for headless tests and the
    /// framework spikes. Agents 0..5 occupy column x = 0, rows y = 0..5, so
    /// the grid must be at least 1 wide and 6 tall.
    let sixAgentWorld (bounds: GridBounds) : WorldState =
        let agents =
            [ for i in 0..5 -> Agent.create (AgentId.ofInt i) Friendly { X = 0; Y = i } ]

        match World.create bounds agents with
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
        s.Agents <- agents

        for idx in 0 .. agents.Length - 1 do
            match agents.[idx].Destination with
            | None -> ()
            | Some dest ->
                let id = agents.[idx].Id
                let pos = agents.[idx].Position

                if pos = dest then
                    agents.[idx] <- { agents.[idx] with Destination = None }
                    emit (MovementCompleted(id, pos)) s
                else
                    let next = PlaceholderMovement.nextCell pos dest
                    agents.[idx] <- { agents.[idx] with Position = next }
                    emit (MovementStepped(id, pos, next)) s

                    if next = dest then
                        agents.[idx] <- { agents.[idx] with Destination = None }
                        emit (MovementCompleted(id, next)) s

    // --- Phase: output -----------------------------------------------------
    // Build the render snapshot from authoritative state. Events are already
    // in stable order; they are reversed to chronological order after the
    // fold completes.
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
                |> Array.sortBy (fun a -> AgentId.value a.Id) }

    let private runPhase (commands: PlayerCommand list) (s: StepState) (phase: Phase) : StepState =
        match phase with
        | CommandIntake -> commandIntake commands s
        | NavigationAndMovement -> navigationAndMovement s
        | Output -> output s
        // Explicit no-ops for this milestone. Listed individually (no
        // wildcard) so that adding a phase forces a decision here.
        | Communication
        | Perception
        | TacticalKnowledge
        | Appraisal
        | CommitmentAndLocalAction
        | Combat
        | StateConsequences
        | Mission -> ()

        s.TraceRev <- phase :: s.TraceRev
        s

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
            commands |> Array.toList |> List.sortBy (fun c -> CommandId.value c.Id)

        let acc =
            { Tick = nextTick
              Bounds = state.Bounds
              Agents = state.Agents
              EventsRev = []
              Snapshot = { Tick = nextTick; Agents = [||] }
              TraceRev = [] }

        let final = List.fold (runPhase ordered) acc Phases.order

        { State =
            { state with
                Tick = nextTick
                Agents = final.Agents }
          Events = final.EventsRev |> List.rev |> List.toArray
          Snapshot = final.Snapshot
          PhaseTrace = final.TraceRev |> List.rev |> List.toArray }
