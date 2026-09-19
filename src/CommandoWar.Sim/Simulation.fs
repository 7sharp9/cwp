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
        (resupplyAreas: Cell[])
        (headquarters: Cell option)
        (jammers: Jammer[])
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
                              TacticalKnowledge = [||]
                              HostileTacticalKnowledge = [||]
                              ResupplyAreas = resupplyAreas
                              Headquarters = headquarters
                              Jammers = jammers
                              Random = SplitMix64.create seed }

    /// Builds a validated world at tick 0 with a SplitMix64 random stream
    /// seeded by `seed` and empty (flat, fully passable, transparent,
    /// uncovered) terrain for `bounds`. Agents are sorted by ascending id.
    /// Fails explicitly on an empty grid, duplicate ids, or an agent placed
    /// outside the grid.
    let create (bounds: GridBounds) (seed: uint64) (agents: AgentState list) : Result<WorldState, WorldError> =
        build bounds (Terrain.empty bounds) seed agents [||] None [||]

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
            |> Array.map (fun d ->
                { Agent.create d.Agent d.Side d.Cell with
                    CommunicationAvailable = d.CommunicationAvailable
                    Discipline = d.Discipline
                    MoveSpeed = d.MoveSpeed
                    FormationOffset = d.FormationOffset })
            |> Array.toList

        build
            scenario.Map
            scenario.Terrain
            seed
            agents
            (scenario.ResupplyAreas |> Array.map (fun a -> a.Cell))
            scenario.Headquarters
            scenario.Jammers

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

    /// What one accepted-but-not-yet-delivered command asks for (TASK-044,
    /// backlog B-051): either a `ReceivedOrder` envelope, carrying the
    /// `QueueMode` that decides whether `communication` replaces or appends
    /// it, or a cancellation naming another command's id. Unified into one
    /// list (`StepState.PendingCommands`), not two, so both kinds of pending
    /// command for one recipient stay ordered by their own `CommandId`
    /// relative to each other — the "same-tick batch is order-independent
    /// only across recipients, ascending within one" precedent already
    /// governing `PendingOrders` before this task.
    type private PendingBody =
        | PendingOrder of order: ReceivedOrder * mode: QueueMode
        | PendingCancel of target: CommandId

    /// Mutable-but-contained accumulator for one step. It never escapes
    /// `step`; the input `WorldState` is never mutated (command intake,
    /// communication, and movement copy the agent array before writing).
    type private StepState =
        { Tick: int64
          Bounds: GridBounds
          /// The authoritative terrain for this run. Immutable within a run;
          /// the Navigation and movement phase reads it for pathfinding.
          Terrain: Terrain
          /// Authored resupply-cache cells, carried in from the input
          /// `WorldState` (TASK-047, backlog B-030 proper). Static within a
          /// run, the `Terrain` precedent — never reassigned during a tick.
          ResupplyAreas: Cell[]
          /// The authored command-origin cell, carried in from the input
          /// `WorldState` (TASK-058, backlog B-016b). Static within a run,
          /// the `ResupplyAreas` precedent. `None` is the opt-in-out state:
          /// the whole range/delay/jamming/radio-destroyed feature set is
          /// inert for a run that never sets this.
          Headquarters: Cell option
          /// Authored jammers, carried in from the input `WorldState`
          /// (TASK-058, backlog B-016b). Static within a run, the
          /// `ResupplyAreas` precedent.
          Jammers: Jammer[]
          mutable Agents: AgentState[]
          /// Commands accepted by Command intake this tick and not yet
          /// applied to their recipients, as `(recipient, commandId, body)`
          /// (TASK-027, backlog B-016; the `ReceivedOrder` payload added by
          /// TASK-028; `PendingBody`/`Cancel` added by TASK-044, backlog
          /// B-051). Populated by `commandIntake`, drained by
          /// `communication` in the same tick: for a reachable recipient, a
          /// `PendingOrder` writes `AgentState.Order` (`Replace`) or appends
          /// to `OrderQueue` (`Append`), and a `PendingCancel` clears/splices
          /// the named `CommandId`; an unreachable recipient gets one
          /// `OrderUndelivered` event and no state change either way. Zero
          /// delivery delay, so this list never survives past the
          /// Communication phase and is not canonical state (`docs/04`
          /// section 12.2; TASK-024 "`WorldState` holds no command
          /// history"). Delayed delivery is backlog B-016b.
          mutable PendingCommands: (AgentId * CommandId * PendingBody) list
          /// The friendly squad's shared tactical picture, carried in from the
          /// input `WorldState` and rewritten by the Tactical-knowledge phase
          /// (TASK-026). Genuine per-tick canonical state.
          mutable TacticalKnowledge: Contact[]
          /// The Hostile side's own shared tactical picture, carried in from
          /// the input `WorldState` and rewritten by the Tactical-knowledge
          /// phase (TASK-034, backlog B-022, partial). Genuine per-tick
          /// canonical state, the `TacticalKnowledge` precedent.
          mutable HostileTacticalKnowledge: Contact[]
          /// The deterministic stream for this tick. No phase draws from it
          /// yet; a future gameplay phase reassigns it after each draw so the
          /// advanced state is carried forward.
          mutable Random: RandomState
          mutable EventsRev: DomainEvent list
          mutable Snapshot: RenderSnapshot
          mutable TraceRev: Phase list
          /// The derived squad leader (TASK-045, backlog B-031;
          /// `Casualty.currentLeader`) as it stood at the very START of this
          /// tick, before ANY phase ran — captured once, immutable for the
          /// rest of the tick. `stateConsequences` needs this, not its own
          /// `s.Agents` read at its own entry: Combat (phase 8) runs
          /// *before* StateConsequences (phase 9) and already applies a
          /// wound that can incapacitate the leader, so by the time
          /// StateConsequences starts, `s.Agents` already reflects that
          /// transition — comparing "before StateConsequences" against
          /// "after StateConsequences" would silently miss any leadership
          /// change Combat itself caused, only ever catching one
          /// StateConsequences's own bleed-out-to-Dead transitions. This
          /// field is the fix: the TRUE start-of-tick baseline, spanning
          /// every phase's mutations, not just StateConsequences's own.
          InitialLeader: AgentId option
          /// Whether at least one Friendly agent was `Alive` at the very
          /// START of this tick (TASK-045, backlog B-031) — the identical
          /// "must span every phase, not just StateConsequences's own"
          /// reasoning as `InitialLeader` above, for the `SquadFailure`
          /// latch.
          InitialFriendlyAlive: bool }

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
    //      accept. CommandAccepted is emitted here, at acceptance, BEFORE the
    //      destination reaches the agent, per 12.1.
    //
    // TASK-027 (backlog B-016): command intake no longer writes
    // AgentState.Destination. An accepted order is RECORDED as a pending order
    // (s.PendingOrders); the Communication phase (12.2), which runs next,
    // DELIVERS it. TASK-028 (backlog B-017): the pending order carries the
    // whole ReceivedOrder envelope (command, intent, issue tick, urgency, risk
    // tolerance), and the Communication phase writes AgentState.Order (not
    // Destination) for a reachable recipient; the Appraisal phase (12.5) then
    // judges it and writes Destination on Accepted. An accepted order takes
    // effect three phases later, still the SAME tick when communication is
    // available and the order is Accepted (nothing between Command intake and
    // Appraisal reads Destination).
    //
    // The delivery tick (RecordedCommand.Tick in Replay.fs) and the envelope's
    // IssuedAtTick are independent concepts: the caller / replay runner owns
    // when a command reaches this phase; IssuedAtTick only records when it was
    // issued. The legacy .cwlog format collapses the two to its single field.
    //
    // No agent array copy: this phase reads s.Agents (for the AgentId -> index
    // map and the hostile-side check) but never mutates it. Agent identity and
    // order are stable, so one index map is valid for the whole batch.
    // Cancel handling (TASK-044, backlog B-051; extended by TASK-058, backlog
    // B-016b): whether `agent` currently holds `target` as its active Order,
    // somewhere in its OrderQueue, or in flight in its PendingDelivery,
    // checked against the START-of-tick s.Agents snapshot (this phase reads
    // but never mutates it, the existing precedent) — so a same-tick
    // "queue order, then cancel it" sequence cannot anticipate the queue
    // effect its own batch has not been applied yet (the identical
    // "same-tick batch cannot see its own not-yet-applied effects" rule
    // DuplicateCommandId / UnauthorisedRecipient already observe).
    let private holdsCommand (agent: AgentState) (target: CommandId) : bool =
        (agent.Order |> Option.exists (fun o -> o.Command = target))
        || (agent.OrderQueue |> List.exists (fun o -> o.Command = target))
        || (agent.PendingDelivery |> Option.exists (fun (o, _, _) -> o.Command = target))

    let private commandIntake (commands: PlayerCommand list) (s: StepState) =
        let agents = s.Agents

        let indexOf: Map<AgentId, int> =
            agents |> Array.mapi (fun i a -> a.Id, i) |> Map.ofArray

        let duplicatedIds: Set<CommandId> =
            commands
            |> List.countBy (fun c -> c.Id)
            |> List.choose (fun (id, n) -> if n > 1 then Some id else None)
            |> Set.ofList

        let pending = ResizeArray<AgentId * CommandId * PendingBody>()

        for cmd in commands do
            if Set.contains cmd.Id duplicatedIds then
                emit (CommandRejected(cmd.Id, DuplicateCommandId cmd.Id)) s
            elif cmd.IssuedAtTick < 0L || cmd.IssuedAtTick > s.Tick then
                emit (CommandRejected(cmd.Id, IssueTickOutOfRange(cmd.IssuedAtTick, s.Tick))) s
            else
                match cmd.Recipients with
                | [] -> emit (CommandRejected(cmd.Id, EmptyRecipients)) s
                | recipients ->
                    match firstDuplicate recipients with
                    | Some repeated -> emit (CommandRejected(cmd.Id, DuplicateRecipient repeated)) s
                    | None ->
                        // The bounds check (TASK-020) applies to every
                        // `Cell`-targeted intent: `MoveTo`, and (TASK-047,
                        // backlog B-030 proper) `Hold`/`Assault`/`Withdraw`,
                        // whose targets are equally bounds-checkable areas.
                        // A Suppress target (TASK-037, a thin B-030 slice)
                        // is an AgentId, not a bounds-checkable Cell —
                        // whether it names a contact the recipient actually
                        // knows about is Appraisal's stage-2 TargetNotKnown
                        // check, never authoritative hostile state at
                        // intake (risk R-023). A Cancel (TASK-044) has no
                        // bounds-checkable target either.
                        let cellTarget =
                            match cmd.Body with
                            | Order(MoveTo target, _)
                            | Order(Hold target, _)
                            | Order(Assault target, _)
                            | Order(Withdraw target, _) -> Some target
                            | Order(Suppress _, _)
                            | Cancel _ -> None

                        let outOfBounds =
                            match cellTarget with
                            | Some target when not (GridBounds.contains target s.Bounds) -> Some target
                            | _ -> None

                        match outOfBounds with
                        | Some target -> emit (CommandRejected(cmd.Id, TargetOutOfBounds target)) s
                        | None ->
                            match cmd.Body with
                            | Order(intent, mode) ->
                                for recipient in recipients |> List.sortBy AgentId.value do
                                    match Map.tryFind recipient indexOf with
                                    | None -> emit (CommandRejected(cmd.Id, UnknownAgent recipient)) s
                                    | Some idx when agents.[idx].Side = Hostile ->
                                        emit (CommandRejected(cmd.Id, UnauthorisedRecipient recipient)) s
                                    | Some idx ->
                                        // CommandAccepted's Cell reports the
                                        // move-shaped target, or (Suppress
                                        // has none) the recipient's own
                                        // unmoving position.
                                        let cell =
                                            match intent with
                                            | MoveTo target
                                            | Hold target
                                            | Assault target
                                            | Withdraw target -> target
                                            | Suppress _ -> agents.[idx].Position

                                        emit (CommandAccepted(cmd.Id, recipient, cell)) s

                                        let order: ReceivedOrder =
                                            { Command = cmd.Id
                                              Intent = intent
                                              IssuedAtTick = cmd.IssuedAtTick
                                              Urgency = cmd.Urgency
                                              RiskTolerance = cmd.RiskTolerance }

                                        pending.Add(recipient, cmd.Id, PendingOrder(order, mode))
                            | Cancel target ->
                                for recipient in recipients |> List.sortBy AgentId.value do
                                    match Map.tryFind recipient indexOf with
                                    | None -> emit (CommandRejected(cmd.Id, UnknownAgent recipient)) s
                                    | Some idx when agents.[idx].Side = Hostile ->
                                        emit (CommandRejected(cmd.Id, UnauthorisedRecipient recipient)) s
                                    | Some idx when not (holdsCommand agents.[idx] target) ->
                                        emit (CommandRejected(cmd.Id, UnknownTargetCommand(recipient, target))) s
                                    | Some idx ->
                                        // No natural target Cell for a
                                        // Cancel — the Suppress
                                        // CommandAccepted precedent.
                                        emit (CommandAccepted(cmd.Id, recipient, agents.[idx].Position)) s
                                        pending.Add(recipient, cmd.Id, PendingCancel target)

        s.PendingCommands <- List.ofSeq pending

    // --- Phase: communication --------------------------------------------
    // Realised by TASK-027 (backlog B-016); reworked by TASK-028 (backlog
    // B-017); extended by TASK-044 (backlog B-051) for queueing and
    // cancellation. Turns the docs/04 section 12.2 no-op into a real phase:
    // "determine which recipients receive an order this tick ... communication
    // failure must be explicit, not silently ignored". For every command
    // intake accepted this tick (s.PendingCommands), in ascending
    // (recipient, command) id order:
    //
    //   * recipient CommunicationAvailable = false -> emit OrderUndelivered
    //     (reason UnableToCommunicate) and DROP the command, whether it was
    //     an order or a cancel. An Order the recipient already held (and any
    //     Destination it produced) is left untouched: an undelivered new
    //     order does not cancel an order in progress (docs/05 section 16
    //     "Lost communication" — the trace shows communication failure, not
    //     disobedience).
    //   * recipient CommunicationAvailable = true:
    //     - PendingOrder(order, Replace): write AgentState.Order (the whole
    //       ReceivedOrder), RESET Disposition to None, and CLEAR OrderQueue
    //       — a bare new order still fully supersedes both the active order
    //       and anything already stacked behind it (Central decision 2, the
    //       pre-TASK-044 behaviour preserved exactly). NOT Destination —
    //       that is the Appraisal phase's job (TASK-028). Zero delivery
    //       delay for what becomes the active order: Appraisal, three
    //       phases later this same tick, judges it. No success event
    //       (backlog B-016b).
    //     - PendingOrder(order, Append): if the recipient holds no active
    //       Order, identical to Replace (an idle agent's first queued order
    //       starts immediately). Otherwise append to the tail of
    //       OrderQueue and emit OrderQueued — the active Order/Disposition
    //       are untouched.
    //     - PendingCancel target, target = the active Order's Command:
    //       clear it and promote the queue head into Order (Disposition
    //       reset to None, so THIS tick's Appraisal judges it) or go idle if
    //       the queue is empty; emit OrderCancelled(target, recipient,
    //       wasActive = true).
    //     - PendingCancel target, target found in OrderQueue instead: splice
    //       out that one entry, leaving the active order and every other
    //       queued entry untouched; emit OrderCancelled(..., wasActive =
    //       false).
    //     - PendingCancel target, found in neither: a same-tick race only —
    //       intake validated target's existence against the START-of-tick
    //       snapshot, but an EARLIER-processed command this same tick (lower
    //       CommandId, e.g. a Replace) already superseded it. No state
    //       change, no event: the superseding command's own events already
    //       explain the trace.
    //
    // Multiple pending commands for one recipient are processed in ascending
    // CommandId order against the incrementally-mutated `agents` array (this
    // loop already reads its own prior iterations' writes), so a same-tick
    // "issue three waypoints" or "issue then cancel" sequence composes
    // correctly with no extra bookkeeping.
    //
    // CommunicationAvailable is STATIC authored scenario data, excluded from
    // Canonical.encode (ADR-0002 amendment). This phase draws nothing from
    // the deterministic stream.
    //
    // TASK-058 (backlog B-016b) layered dynamic range/jamming/radio-destroyed
    // checks on top via `Communication.available`, gated behind whether
    // `s.Headquarters` is authored at all (the opt-in gate): when it is
    // `None`, every branch below behaves exactly as before this task
    // (same-tick delivery, `PendingDelivery` never written). When it is
    // `Some _`, a `PendingOrder` that clears the availability check is
    // stashed in `AgentState.PendingDelivery` instead of taking effect
    // immediately, and a second pass below (after this loop) delivers or
    // drops it once its due tick arrives. A `PendingCancel` is never
    // delayed — cancelling an order that has not even arrived yet needs no
    // radio round-trip to model.
    /// Applies a delivered order to `agent`, returning the updated agent and
    /// whether it joined `OrderQueue` behind an already-active order (the
    /// caller emits `OrderQueued` for that case — this function is pure, so
    /// it cannot emit itself, and its two call sites need different
    /// additional events around it: a zero-delay delivery emits nothing
    /// else, a delayed one also emits `OrderDelivered`).
    let private applyDelivered (agent: AgentState) (order: ReceivedOrder) (mode: QueueMode) : AgentState * bool =
        match mode with
        | Replace ->
            { agent with
                Order = Some order
                Disposition = None
                OrderQueue = [] },
            false
        | Append ->
            match agent.Order with
            | None -> { agent with Order = Some order; Disposition = None }, false
            | Some _ -> { agent with OrderQueue = agent.OrderQueue @ [ order ] }, true

    let private communication (s: StepState) =
        match s.PendingCommands with
        | [] -> ()
        | items ->
            let agents = Array.copy s.Agents

            let indexOf: Map<AgentId, int> =
                agents |> Array.mapi (fun i a -> a.Id, i) |> Map.ofArray

            let ordered =
                items
                |> List.sortBy (fun (recipient, cmdId, _) -> AgentId.value recipient, CommandId.value cmdId)

            for recipient, cmdId, body in ordered do
                // Command intake already rejected an unknown recipient
                // (UnknownAgent) against this same array, and no phase between
                // adds or removes an agent, so the lookup always succeeds.
                match Map.tryFind recipient indexOf with
                | None -> ()
                | Some idx when not (Communication.available s.Headquarters s.Jammers s.Tick agents.[idx]) ->
                    let reason =
                        Communication.reason s.Headquarters s.Jammers s.Tick agents.[idx]
                        |> Option.defaultValue UnableToCommunicate

                    emit (OrderUndelivered(cmdId, recipient, reason)) s
                | Some idx ->
                    let a = agents.[idx]

                    match body with
                    | PendingOrder(order, mode) when s.Headquarters.IsSome ->
                        // A new pending command always supersedes an
                        // in-flight one outright (Central decision,
                        // TASK-058): the previous PendingDelivery, if any,
                        // is silently overwritten, the same "no event for
                        // the superseded state" precedent Replace already
                        // uses for an active Order.
                        agents.[idx] <-
                            { a with
                                PendingDelivery = Some(order, mode, s.Tick + CommsConfig.DeliveryDelayTicks) }
                    | PendingOrder(order, mode) ->
                        let updated, queued = applyDelivered a order mode
                        agents.[idx] <- updated

                        if queued then
                            emit (OrderQueued(order.Command, recipient)) s
                    | PendingCancel target ->
                        // TASK-058 (backlog B-016b): a cancel targeting an
                        // order still in flight (not yet Order/OrderQueue)
                        // needs no radio round-trip to model -- it is
                        // simply dropped, reported the same "not active"
                        // way a queued-entry cancel is (it never took
                        // effect). Checked ahead of the pre-existing
                        // active/queue cases below, which are otherwise
                        // completely unchanged from before this task.
                        let cancelledInFlight =
                            match a.PendingDelivery with
                            | Some(order, _, _) when order.Command = target ->
                                agents.[idx] <- { a with PendingDelivery = None }
                                emit (OrderCancelled(target, recipient, false)) s
                                true
                            | _ -> false

                        if not cancelledInFlight then
                            match a.Order with
                            | Some active when active.Command = target ->
                                // Destination is cleared here regardless of
                                // promotion: it belongs to the CANCELLED order
                                // (a MoveTo target), and a promoted order has not
                                // been appraised yet (Disposition = None) to
                                // write its own — leaving the stale value would
                                // have Navigation keep driving the agent toward
                                // an order it no longer holds. The Appraisal
                                // precedent for a superseding Replace order,
                                // applied explicitly here since a Cancel-driven
                                // promotion bypasses Appraisal this tick.
                                match a.OrderQueue with
                                | head :: tail ->
                                    agents.[idx] <-
                                        { a with
                                            Order = Some head
                                            Disposition = None
                                            Destination = None
                                            OrderQueue = tail }
                                | [] ->
                                    agents.[idx] <-
                                        { a with
                                            Order = None
                                            Disposition = None
                                            Destination = None }

                                emit (OrderCancelled(target, recipient, true)) s
                            | _ ->
                                if a.OrderQueue |> List.exists (fun o -> o.Command = target) then
                                    agents.[idx] <-
                                        { a with
                                            OrderQueue = a.OrderQueue |> List.filter (fun o -> o.Command <> target) }

                                    emit (OrderCancelled(target, recipient, false)) s
                                // else: superseded earlier this same tick by a
                                // lower-CommandId command (see the phase comment
                                // above) — nothing to cancel any more.

            s.Agents <- agents

        s.PendingCommands <- []

        // Second pass (TASK-058, backlog B-016b): deliver or drop every
        // agent whose PendingDelivery has come due. Only ever non-empty
        // when s.Headquarters is authored (the opt-in gate) — the loop body
        // is a no-op for every one of the 16 pre-existing corpus entries.
        // A freshly-set PendingDelivery from the loop above can never be due
        // this same tick (CommsConfig.DeliveryDelayTicks >= 1), so this pass
        // never double-processes a command the first pass just queued.
        if s.Headquarters.IsSome then
            let agents = Array.copy s.Agents

            for idx in 0 .. agents.Length - 1 do
                match agents.[idx].PendingDelivery with
                | Some(order, mode, dueTick) when dueTick <= s.Tick ->
                    let a = agents.[idx]

                    if Communication.available s.Headquarters s.Jammers s.Tick a then
                        let updated, queued = applyDelivered a order mode
                        agents.[idx] <- { updated with PendingDelivery = None }
                        emit (OrderDelivered(order.Command, a.Id)) s

                        if queued then
                            emit (OrderQueued(order.Command, a.Id)) s
                    else
                        let reason =
                            Communication.reason s.Headquarters s.Jammers s.Tick a
                            |> Option.defaultValue UnableToCommunicate

                        agents.[idx] <- { a with PendingDelivery = None }
                        emit (OrderUndelivered(order.Command, a.Id, reason)) s
                | _ -> ()

            s.Agents <- agents

    // --- Phase: perception -------------------------------------------------
    // Realised by TASK-026 (backlog B-015). The first phase consumer of the
    // `Sight` module (TASK-012): "NOTHING in Simulation.step or any tick phase
    // calls this module yet" is no longer true. For every agent, in ascending
    // id order, `AgentState.VisibleContacts` is re-derived from scratch — the
    // opposing-side agents within `PerceptionConfig.SightRange` Chebyshev cells
    // and in `Sight.visible` line of sight over the immutable terrain (docs/04
    // section 12.3: "clear current visibility ... update current
    // visible-contact state"). Both sides are swept (docs/05 section 12).
    //
    // A `ContactObserved` event is emitted only when a contact enters an
    // observer's visibility this tick — a new sighting, not every tick it
    // stays visible (docs/04 section 14: "Do not emit a flood of low-value
    // events"). Events are ordered ascending by `(observer, contact)` id.
    //
    // `VisibleContacts` is a pure deterministic function of every agent's
    // `Position`, the immutable `Terrain`, and the `PerceptionConfig`
    // constants, so — like `AgentState.Route` — it is a derived cache
    // EXCLUDED from `Canonical.encode` (docs/04 section 17). This phase alone
    // moves no pinned hash. The input `WorldState` is never mutated (the
    // `commandIntake` / `navigationAndMovement` copy-before-write idiom).
    let private perception (s: StepState) =
        let agents = Array.copy s.Agents
        let swept = Perception.sweep s.Terrain agents

        for i in 0 .. agents.Length - 1 do
            let a = agents.[i]
            let seenNow = swept.[i]
            let seenBefore = Set.ofArray a.VisibleContacts

            for contactId in seenNow do
                if not (Set.contains contactId seenBefore) then
                    let contactCell = (agents |> Array.find (fun x -> x.Id = contactId)).Position
                    emit (ContactObserved(a.Id, contactId, contactCell)) s

            agents.[i] <- { a with VisibleContacts = seenNow }

        s.Agents <- agents

    // --- Phase: tactical knowledge ---------------------------------------
    // Realised by TASK-026 (backlog B-015). Merges this tick's friendly
    // observations into the one shared squad picture
    // (`WorldState.TacticalKnowledge`, docs/04 section 12.4: "merge reports
    // into squad contacts; retain last known position, confidence, and
    // observation tick; decay or expire stale contacts according to explicit
    // rules").
    //
    // Instant squad sharing (docs/05 section 3): every `Friendly` agent IS the
    // squad (still no `SquadStore` -- TASK-059 realised B-011d narrowly, as a
    // movement-slot grouping only, `Scenario.Formations`/`AgentState.
    // FormationOffset`, not a knowledge-sharing container), so every
    // friendly's `VisibleContacts` from the Perception phase is immediately in
    // the shared store. `Perception.mergeKnowledge` upserts every contact seen
    // this tick at `PerceptionConfig.ConfidenceFull`, drops one band after
    // `PerceptionConfig.StaleAfter` unseen ticks, and removes a contact after
    // `PerceptionConfig.ExpireAfter` unseen ticks — emitting `ContactExpired`
    // on removal, ascending by contact id.
    //
    // The store IS genuine per-tick canonical state: `Contact.LastSeenTick`
    // and the decaying `Contact.Confidence` carry memory the current tick's
    // positions cannot reproduce, so under the ADR-0002 amendment it is in
    // `Canonical.encode` and `Canonical.FormatVersion` is 3. A HOSTILE squad
    // picture, enemy doctrine reacting to it, and communication constraints
    // are B-016 / B-022.
    let private tacticalKnowledge (s: StepState) =
        let byId (id: AgentId) =
            s.Agents |> Array.tryFind (fun x -> x.Id = id)

        let seenBy (side: Side) =
            s.Agents
            |> Array.filter (fun a -> a.Side = side)
            |> Array.collect (fun a -> a.VisibleContacts)
            |> Array.distinct
            |> Array.choose (fun id -> byId id |> Option.map (fun x -> id, x.Position))
            |> Map.ofArray

        let store, expired = Perception.mergeKnowledge s.Tick s.TacticalKnowledge (seenBy Friendly)

        for c in expired |> Array.sortBy (fun c -> c.Contact) do
            emit (ContactExpired(c.Contact, c.LastKnownCell)) s

        s.TacticalKnowledge <- store

        // TASK-034 (backlog B-022, partial): the Hostile side's own shared
        // tactical picture, symmetric to the friendly one above and built by
        // calling the identical side-agnostic `Perception.mergeKnowledge` a
        // second time, filtered to `Side = Hostile` instead. `ContactObserved`
        // already fires for a Hostile agent's new sighting (TASK-026); this is
        // the first phase that retains it. `ContactExpired` for a contact
        // dropping out of THIS store reuses the identical event shape — a
        // reader distinguishes which picture an expiry came from by the
        // contact's own `AgentState.Side`, not by the event.
        let hostileStore, hostileExpired =
            Perception.mergeKnowledge s.Tick s.HostileTacticalKnowledge (seenBy Hostile)

        for c in hostileExpired |> Array.sortBy (fun c -> c.Contact) do
            emit (ContactExpired(c.Contact, c.LastKnownCell)) s

        s.HostileTacticalKnowledge <- hostileStore

    // --- Phase: appraisal -------------------------------------------------
    // Realised by TASK-028 (backlog B-017). Turns the docs/04 section 12.5
    // no-op into a real phase: "appraise newly received orders; reappraise
    // only on material triggers, not every tick without need; emit outcome and
    // structured reasons". Runs after Perception / Tactical knowledge (so it
    // judges the order against this tick's known picture) and before
    // Navigation (so an Accepted order's Destination is followed the same
    // tick).
    //
    // For every agent, in ascending id order:
    //
    //   1. Fast path. Order present and Disposition already Some -> nothing.
    //      This is "reappraise only on material triggers": an unchanged,
    //      already-appraised order is not re-judged and emits no event. The
    //      only in-scope trigger is "a new order is received", which the
    //      Communication phase encodes by resetting Disposition to None. This
    //      also covers a FULFILLED order (Accepted, no Destination, standing
    //      on the target) — its housekeeping clear is relocated to
    //      commitmentAndLocalAction (TASK-030, backlog B-018): a commitment
    //      ending is a 12.6 concern, not a 12.5 one.
    //   2. Appraise. Order present and Disposition None (fresh, or reset by a
    //      superseding order): run Appraisal.appraise over the terrain, the
    //      shared TacticalKnowledge, and the agent's static Discipline. Clear
    //      any Destination a superseded order left, then on Accepted write
    //      Destination (unless already at the target); on Refused / Unable
    //      write none. Emit OrderAppraised(agent, command, disposition) — for
    //      EVERY outcome, including the mundane Accepted (the G3 developer
    //      trace must explain any appraisal, docs/07 section 9 criterion 11).
    //
    // The Appraisal phase does NOT write the AgentState.Route cache: the
    // Navigation phase recomputes the route from Destination exactly as it
    // does today (a small pure duplication of the stage-2 Pathfinding call,
    // preferred over coupling the phases). No PRNG draw (B-019 owns the
    // stream's first gameplay consumer). Commitments and the finite move/hold
    // executor are realised by TASK-030 (backlog B-018, the
    // commitmentAndLocalAction phase immediately below); stage-5 safer
    // adaptation stays deferred (a TASK-030 follow-up).
    //
    // Stress and the suppression-band / knowledge-change reappraisal
    // triggers realised by TASK-033 (backlog B-021). Two material triggers
    // are added on top of "a new order is received", both resetting an
    // already-appraised order's Disposition back to None so the fast path
    // below is skipped and it is re-judged this tick:
    //
    //   * knowledge-change: any ContactObserved / ContactExpired event
    //     emitted earlier this tick (Perception / TacticalKnowledge both run
    //     before Appraisal) — global across every agent with a live order,
    //     not filtered to "was this agent's route affected" (the
    //     exposure-band trigger that WOULD do that is deferred; this is the
    //     R-023 "same observation contract" precedent of a broad, simple
    //     trigger over a precise, expensive one);
    //   * suppression-band: AgentState.SuppressionBand (a hysteresis latch
    //     over AgentState.Suppression, docs/05 sections 14/15) flips this
    //     tick, read and updated here against last tick's finalised
    //     Suppression (Combat / State consequences for THIS tick have not
    //     run yet).
    //
    // A third trigger is added by TASK-037 (a thin B-030 slice):
    //
    //   * threat-suppression-change: ANY agent's SuppressionBand flips this
    //     tick (not just the appraising agent's own) — global scope, the
    //     knowledge-change precedent. This is what lets a Suppress order (or
    //     incidental automatic engagement) against one agent's threat
    //     reappraise a DIFFERENT agent's Refused/Unable order once
    //     Appraisal.routeExposure (Decision F) stops charging that threat's
    //     pressure — docs/07 section 8 step 6, "tactical knowledge and
    //     exposure are recalculated". Precomputed for every agent up front
    //     (newBands below), not inline, so a later-id threat's flip is known
    //     before an earlier-id agent's order is judged.
    //
    // Both Appraisal.resolveThreshold terms (a continuous Stress drag, a
    // discrete SuppressionBand penalty) are read only when an appraisal
    // actually runs, exactly like Discipline — the fast path still emits
    // nothing and reads neither. Appraisal.appraise also takes
    // suppressedThreats (TASK-037): the ids of every agent currently
    // SuppressionBand-latched, used only by a MoveTo order's stage-3
    // exposure (Decision F) — a Suppress order's own stage-2
    // TargetNotKnown check reads `threats` directly, not this set.
    // Appraisal.appraise also takes occupied/formationOffset (TASK-059,
    // backlog B-011d): every other agent's current Position and this
    // agent's own static formation slot, used only by a MoveTo order's
    // target resolution (Appraisal.resolveFormationTarget) — None
    // reproduces every pre-TASK-059 order exactly.
    let private appraisal (s: StepState) =
        let terrain = s.Terrain
        let threats = s.TacticalKnowledge
        let budget = terrain.Bounds.Width * terrain.Bounds.Height
        let agents = Array.copy s.Agents

        let knowledgeChanged =
            s.EventsRev
            |> List.exists (fun e ->
                match e.Body with
                | ContactObserved _
                | ContactExpired _ -> true
                | _ -> false)

        // Precomputed for every agent up front (TASK-037), not inline per
        // iteration as before TASK-037: the threatSuppressionChanged /
        // suppressedThreats triggers below need to know whether a threat
        // LATER in ascending-id order just flipped its SuppressionBand while
        // still processing an EARLIER agent's order, which inline
        // computation cannot see.
        let newBands =
            agents
            |> Array.map (fun a ->
                if a.Suppression >= AppraisalConfig.SuppressionBandEnter then true
                elif a.Suppression <= AppraisalConfig.SuppressionBandExit then false
                else a.SuppressionBand)

        // Decision G (TASK-037, backlog B-030 thin slice): has any agent's
        // SuppressionBand flipped this tick? Global scope, the
        // knowledgeChanged precedent (docs/05 section 14) — reappraises
        // every non-fulfilled Refused/Unable order, not only the ones whose
        // recorded topThreat is the specific agent that flipped. This is
        // what realises docs/07 section 8 step 6 ("tactical knowledge and
        // exposure are recalculated") once a `Suppress` order (or incidental
        // automatic engagement) drives a threat's Suppression into its band.
        let threatSuppressionChanged =
            Array.zip agents newBands |> Array.exists (fun (a, newBand) -> newBand <> a.SuppressionBand)

        // Decision F (TASK-037): the ids of every agent currently suppressed
        // (this tick's newBand, not last tick's), for
        // Appraisal.routeExposure/.appraise's stage-3 pressure zeroing.
        let suppressedThreats =
            Array.zip agents newBands
            |> Array.choose (fun (a, newBand) -> if newBand then Some a.Id else None)

        for i in 0 .. agents.Length - 1 do
            let a = agents.[i]
            let newBand = newBands.[i]

            // TASK-059 (backlog B-011d): every other agent's current
            // `Position` -- read only by a formationed agent's `MoveTo`
            // resolution (`Appraisal.resolveFormationTarget`'s occupancy
            // check). `Position` does not change within this phase (only
            // Navigation, later, mutates it), so this is stable regardless
            // of how far the loop has progressed.
            let occupied =
                agents |> Array.choose (fun x -> if x.Id = a.Id then None else Some x.Position)

            match a.Order with
            | None -> agents.[i] <- { a with SuppressionBand = newBand; RecentlyWounded = false }
            | Some o ->
                // A fulfilled MoveTo order (Accepted, arrived, Destination
                // already cleared to None) must NOT be reset here:
                // commitmentAndLocalAction (immediately after this phase)
                // relies on seeing Disposition = Some Accepted with
                // Destination = None to recognise completion and clear both
                // fields. Resetting it here would re-run
                // Appraisal.appraise's fromCell = target short-circuit,
                // which re-writes Destination = Some target and silently
                // defeats that clear. A Suppress order (TASK-037) has no
                // fulfilled state at all (Commitment.fs Decision D/E) — it
                // only ends by supersession, so it is never "fulfilled" here.
                // TASK-047 (backlog B-030 proper): Hold/Assault/Withdraw use
                // the identical MoveTo-shaped fulfilled test — Assault's own
                // extra "is the target actually clear" nuance is deliberately
                // NOT checked here (that lives in commitmentAndLocalAction,
                // which owns the real fulfilment decision): treating
                // "physically arrived" as fulfilled for THIS phase's own
                // fast-path purposes just means appraisal leaves Destination
                // alone once the agent is standing at the target, exactly as
                // it already does for MoveTo, letting commitmentAndLocalAction
                // be the sole owner of Destination from that point on.
                let fulfilled =
                    match o.Intent with
                    | MoveTo target ->
                        a.Disposition = Some Accepted
                        && a.Destination = None
                        && a.Position = Appraisal.resolveFormationTarget terrain occupied a.FormationOffset target
                    | Hold area ->
                        a.Disposition = Some Accepted
                        && a.Destination = None
                        && a.Position = Appraisal.bestCoverNear terrain threats suppressedThreats area
                    | Assault target
                    | Withdraw target ->
                        a.Disposition = Some Accepted && a.Destination = None && a.Position = target
                    | Suppress _ -> false

                // TASK-045 (backlog B-031): "the agent is wounded" (docs/05
                // section 14) — a.RecentlyWounded is the SuppressionBand-latch
                // precedent's own trick applied to a one-shot signal instead of
                // a hysteresis band (see AgentState.RecentlyWounded's own doc
                // comment for why a persisted flag is needed at all: Combat
                // runs after Appraisal, so a wound taken this tick cannot be
                // seen by this tick's Appraisal the way knowledge-change can).
                let triggered =
                    a.Disposition.IsSome
                    && not fulfilled
                    && (knowledgeChanged
                        || newBand <> a.SuppressionBand
                        || threatSuppressionChanged
                        || a.RecentlyWounded)

                match (if triggered then None else a.Disposition) with
                | Some _ ->
                    // Already appraised, order unchanged, no trigger fired: no
                    // re-appraisal. This also covers a fulfilled order (Accepted,
                    // Destination = None, Position = target) — its housekeeping
                    // clear moved to commitmentAndLocalAction (TASK-030, backlog
                    // B-018): a commitment ending is a 12.6 concern, not a 12.5
                    // one. RecentlyWounded is still consumed here even though it
                    // did not (or could not, for a fulfilled order) trigger a
                    // fresh appraisal — "reappraise only on material triggers"
                    // governs the OUTCOME, not whether the one-shot flag is spent.
                    agents.[i] <- { a with SuppressionBand = newBand; RecentlyWounded = false }
                | None ->
                    let disposition, _ =
                        Appraisal.appraise
                            terrain
                            threats
                            suppressedThreats
                            a.Discipline
                            a.Stress
                            newBand
                            a.Vitals
                            a.Ammo
                            occupied
                            a.FormationOffset
                            o
                            a.Position
                            budget

                    // On Accepted, hand the target to Navigation as the
                    // Destination (which clears any Destination a superseded
                    // order left). An order to the agent's own cell is
                    // Accepted with Destination = the cell; Navigation then
                    // emits MovementCompleted and clears it, exactly as the
                    // pre-TASK-028 Communication write did. On Refused / Unable
                    // the agent holds no Destination. A Suppress order
                    // (TASK-037) never writes a Destination even when
                    // Accepted — it does not move (Commitment.fs Decision D).
                    // TASK-047 (backlog B-030 proper): `Assault`/`Withdraw`
                    // pass their target through unchanged, the `MoveTo`
                    // precedent; `Hold` writes `Appraisal.bestCoverNear`'s
                    // resolved cell, not the literal authored `area` —
                    // recomputed here (pure, deterministic, identical inputs
                    // to the call inside `Appraisal.appraise` above) rather
                    // than threading it back out of `appraise`'s return
                    // value, so `Destination` always matches the cell
                    // `appraise` actually routed to. TASK-059 (backlog
                    // B-011d): `MoveTo` writes `Appraisal.
                    // resolveFormationTarget`'s resolved cell the identical
                    // way -- `None` reproduces the literal `target` exactly.
                    let destination =
                        match disposition, o.Intent with
                        | Accepted, MoveTo target ->
                            Some(Appraisal.resolveFormationTarget terrain occupied a.FormationOffset target)
                        | Accepted, Hold area -> Some(Appraisal.bestCoverNear terrain threats suppressedThreats area)
                        | Accepted, Assault target -> Some target
                        | Accepted, Withdraw target -> Some target
                        | Accepted, Suppress _
                        | Refused _, _
                        | Unable _, _ -> None

                    agents.[i] <-
                        { a with
                            Disposition = Some disposition
                            Destination = destination
                            SuppressionBand = newBand
                            RecentlyWounded = false }

                    emit (OrderAppraised(a.Id, o.Command, disposition)) s

        s.Agents <- agents

    // --- Phase: commitment and local action ---------------------------------
    // Realised by TASK-030 (backlog B-018). Turns the docs/04 section 12.6
    // no-op into a real phase: "accepted orders create or update a
    // commitment; the executor chooses the next finite action within that
    // commitment; a small ordered interrupt table may supersede the normal
    // action." Runs immediately after Appraisal (so a commitment begins or
    // ends the same tick its order is judged) and before Navigation (which is
    // unchanged: it still drives movement from AgentState.Destination).
    //
    // Commitment (docs/05 section 9) is NOT new AgentState — it is a pure
    // derived value over Order / Disposition / Destination (Commitment.fs,
    // the AgentState.Route precedent), so this phase writes no new field. Its
    // job is entirely to notice the two transitions those fields can now
    // produce and name them with an event:
    //
    //   1. Fulfilled (MoveTo only). Order = Some o, Disposition = Some
    //      Accepted, Destination = None, Position = o's target -> the order
    //      was completed last tick (Navigation cleared Destination on
    //      arrival). Clear Order and Disposition (relocated, unchanged, from
    //      the Appraisal phase's prior housekeeping branch — TASK-028) and
    //      emit CommitmentCompleted(agent, command, at). A Suppress order
    //      (TASK-037) has no fulfilled state — it never reaches this branch.
    //   2. Established. Order = Some o, Disposition = Some Accepted, and this
    //      tick's Appraisal just emitted OrderAppraised(agent, o.Command,
    //      Accepted) (checked via this tick's already-emitted events, not
    //      persisted state) -> a fresh commitment begins. Emit
    //      CommitmentEstablished(agent, command, target) — target is the
    //      MoveTo destination, or (Suppress, TASK-037) the named contact's
    //      last-known cell. This single check covers both "from Holding" and
    //      "supersedes an in-progress Moving/Suppressing commitment"
    //      (docs/05 section 11 priority 6, "new higher-priority command" —
    //      the only interrupt priority with a live signal today; priorities
    //      1-4 need combat/suppression state that does not exist, B-019/
    //      B-020, and priority 5 "route invalidated" needs a stall counter
    //      TASK-028 already assigned to B-021): because Commitment is
    //      derived, not stored, the prior commitment simply stops being
    //      produced the instant Order/Disposition/Destination change: there
    //      is nothing to interrupt as a side effect, and no event reports the
    //      old commitment's end.
    //   3. Otherwise (an unchanged Moving commitment continuing, a Refused /
    //      Unable order, or no order at all) -> no change, no event, exactly
    //      the "reappraise only on material triggers" sparseness Appraisal
    //      already observes.
    //
    // Stage-5 safer adaptation (OrderDisposition.Adapted, an exposure-aware
    // reroute) is deliberately deferred — it needs a second, separate
    // route-search algorithm and a Navigation change to follow a pinned
    // route, which does not belong in this task. No PRNG draw.
    let private commitmentAndLocalAction (s: StepState) =
        let terrain = s.Terrain
        let threats = s.TacticalKnowledge

        // This tick's already-finalised SuppressionBand latch — Appraisal,
        // which runs immediately before this phase, wrote the final value
        // onto s.Agents this tick. The identical projection
        // Simulation.appraisal / Diagnostics.orderAppraisalOverlays already
        // compute (TASK-047, backlog B-030 proper: needed for Hold's
        // `bestCoverNear` and Assault's stage derivation below).
        let suppressedThreats =
            s.Agents |> Array.filter (fun a -> a.SuppressionBand) |> Array.map (fun a -> a.Id)

        let acceptedThisTick =
            s.EventsRev
            |> List.choose (function
                | { Body = OrderAppraised(agent, _, Accepted) } -> Some agent
                | _ -> None)
            |> Set.ofList

        let agents = Array.copy s.Agents

        // Shared fulfilled-completion mechanics for every order shape that
        // reaches a target cell and stops (MoveTo, Hold, Withdraw, Assault):
        // relocated from the Appraisal phase's prior housekeeping
        // (TASK-028). TASK-044 (backlog B-051): promote the queue head into
        // Order instead of just clearing it, when one is stacked behind this
        // order. The promoted order is judged by NEXT tick's Appraisal, not
        // this one — Appraisal already ran earlier this tick, before
        // commitmentAndLocalAction — a deliberate one-tick gap before a
        // chained waypoint's next leg begins, not a new interrupt mechanism
        // duplicating Appraisal.appraise inline.
        let completeOrder (i: int) (a: AgentState) (command: CommandId) =
            match a.OrderQueue with
            | head :: tail ->
                agents.[i] <-
                    { a with
                        Order = Some head
                        Disposition = None
                        OrderQueue = tail }
            | [] -> agents.[i] <- { a with Order = None; Disposition = None }

            emit (CommitmentCompleted(a.Id, command, a.Position)) s

        for i in 0 .. agents.Length - 1 do
            let a = agents.[i]

            match a.Order, a.Disposition with
            | Some o, Some Accepted ->
                match o.Intent with
                | MoveTo target ->
                    if a.Destination = None && a.Position = target then
                        completeOrder i a o.Command
                    elif Set.contains a.Id acceptedThisTick then
                        // Freshly accepted this tick: a commitment begins.
                        emit (CommitmentEstablished(a.Id, o.Command, target)) s
                    // else: a Moving commitment continues unchanged; no event.
                | Hold area ->
                    // TASK-047 (backlog B-030 proper): the MoveTo shape
                    // exactly, redirected through `bestCoverNear`'s resolved
                    // cell (Decision D) rather than the literal authored
                    // `area` — recomputed here on the identical inputs
                    // `Simulation.appraisal` used when it wrote
                    // `Destination`, so it always agrees with what the agent
                    // is actually walking toward. No distinct `Commitment`
                    // case (Decision C): this produces `Moving` via
                    // `Commitment.ofAgent`'s generic accepted-with-
                    // destination arm.
                    let target = Appraisal.bestCoverNear terrain threats suppressedThreats area

                    if a.Destination = None && a.Position = target then
                        completeOrder i a o.Command
                    elif Set.contains a.Id acceptedThisTick then
                        emit (CommitmentEstablished(a.Id, o.Command, target)) s
                | Withdraw target ->
                    // TASK-047 (backlog B-030 proper): the MoveTo shape
                    // exactly; `Commitment.ofAgent` reports this as
                    // `Withdrawing`, not `Moving` (Decision E).
                    if a.Destination = None && a.Position = target then
                        completeOrder i a o.Command
                    elif Set.contains a.Id acceptedThisTick then
                        emit (CommitmentEstablished(a.Id, o.Command, target)) s
                | Assault target ->
                    // TASK-047 (backlog B-030 proper; Decisions F-H). Fulfilled
                    // needs one more clause than the MoveTo shape: no known,
                    // currently-unsuppressed threat contact within
                    // AssaultClearRadius of `target` — an agent standing at
                    // the target with a defender still known nearby is
                    // `ClearingThreat`, not done.
                    let clear =
                        not (
                            Commitment.hasUnsuppressedThreatWithin
                                threats
                                suppressedThreats
                                AppraisalConfig.AssaultClearRadius
                                target
                        )

                    if a.Destination = None && a.Position = target && clear then
                        completeOrder i a o.Command
                    else
                        // Not yet fulfilled: drive the stage-dependent
                        // Destination freeze/unfreeze (Decision G) through
                        // the ordinary, unchanged Navigation pipeline —
                        // `AwaitingSupport` clears it so Navigation sees no
                        // destination and does not move the agent this tick;
                        // `ApproachingStart`/`Advancing` (re)assert it so
                        // Navigation resumes. `ClearingThreat` needs no write
                        // here: the agent is already at `target` and
                        // Navigation already cleared `Destination` on
                        // arrival. Re-asserting an unchanged value every tick
                        // is a harmless no-op — `MovementBlocked`'s "does not
                        // retry" concern does not apply to an already-
                        // Accepted order under this codebase's static
                        // terrain (docs/05 section 11: "an Appraisal-Accepted
                        // route cannot later become unreachable").
                        match Commitment.assaultStage threats suppressedThreats a.Position target with
                        | AwaitingSupport -> if a.Destination <> None then agents.[i] <- { a with Destination = None }
                        | ApproachingStart
                        | Advancing ->
                            if a.Destination = None then
                                agents.[i] <- { a with Destination = Some target }
                        | ClearingThreat -> ()

                        if Set.contains a.Id acceptedThisTick then
                            emit (CommitmentEstablished(a.Id, o.Command, target)) s
                | Suppress target ->
                    // A Suppress commitment (TASK-037) has no fulfilled state
                    // (Commitment.fs Decision D/E) — it holds position
                    // indefinitely and ends only by supersession (a new Order
                    // overwriting this one), which needs no event of its own
                    // (Commitment is derived, not stored — the prior
                    // commitment simply stops being produced). Report the
                    // establishment at the target contact's last-known cell,
                    // when still known (the CommitmentEstablished Cell field
                    // precedent), or the agent's own position as a fallback
                    // (only reachable if the contact expired the same tick it
                    // was accepted).
                    if Set.contains a.Id acceptedThisTick then
                        let at =
                            s.TacticalKnowledge
                            |> Array.tryFind (fun c -> c.Contact = target)
                            |> Option.map (fun c -> c.LastKnownCell)
                            |> Option.defaultValue a.Position

                        emit (CommitmentEstablished(a.Id, o.Command, at)) s
            | _ -> ()
            // Order = None, or Disposition = Some (Refused | Unable): Holding.
            // Nothing to establish or complete; no event.

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
    // increment is the universal `Terrain.BaseMoveCost`, exactly as before
    // TASK-049. TASK-049 (backlog B-058) adds a per-agent `AgentState.
    // MoveSpeed`: the completion *comparison* alone is cross-multiplied by
    // it against `Agent.MoveSpeedDefault` (`wouldComplete`, below), so a
    // mover at that default speed reproduces the pre-TASK-049 comparison
    // byte-for-byte (multiplying both sides of an inequality by the same
    // constant does not change it) while a smaller `MoveSpeed` genuinely
    // needs proportionally more ticks to cross the same cell. Progress is
    // scoped to the current edge only: it resets to 0 whenever that edge
    // changes (a fresh route is computed, the agent enters a cell, arrives,
    // or is blocked), and is genuinely new canonical state
    // (`AgentState.Progress`, `Canonical.FormatVersion` 2) because — unlike
    // `Route` — it cannot be recomputed from `Position` alone.
    //
    // Formation slots (B-011d, split from B-011c by TASK-018, which landed
    // sub-cell progress only) are realised by TASK-059: a formationed
    // agent's `MoveTo` target is redirected by `Appraisal.
    // resolveFormationTarget` before this pass ever sees it (Appraisal
    // phase, above) -- Navigation itself is unchanged, still driven purely
    // by whatever `Destination` it is handed. `AgentState.Route` is still a
    // non-canonical derived cache (see `MovementPath`).

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
        /// resolution: an agent whose `startProgress + Terrain.BaseMoveCost`,
        /// cross-multiplied by its own `AgentState.MoveSpeed` (TASK-049),
        /// reaches the next cell's threshold this tick is a claimant in Pass
        /// 2; one that does not simply accumulates progress in Pass 3 with
        /// no contention possible.
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
                // TASK-045 (backlog B-031): a non-Alive agent never starts a
                // new action (docs/04 section 20) — Idle unconditionally,
                // regardless of a stale Destination it can no longer act on
                // (left as-is, not cleared: nothing reads it while non-Alive,
                // the "an undelivered order does not clear a Destination it
                // didn't produce" idiom).
                if not (Casualty.isAlive a.Vitals) then
                    Idle
                else
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
        // since only they are actually entering one. The per-tick increment
        // stays the universal `Terrain.BaseMoveCost` for every agent (so
        // `AgentState.Progress`'s stored trajectory is exactly the elapsed
        // real-tick sequence 0, 1, 2, ... it always has been); only the
        // *comparison* scales per-agent via cross-multiplication against
        // `moveSpeed` (TASK-049, backlog B-058), so a mover at
        // `Agent.MoveSpeedDefault` reproduces the pre-TASK-049 threshold
        // check byte-for-byte (multiplying both sides of the old inequality
        // by the same constant does not change it), while a smaller
        // `moveSpeed` genuinely needs proportionally more ticks to cross the
        // same cell.
        let wouldComplete (next: Cell) (startProgress: int) (moveSpeed: int) =
            (startProgress + Terrain.BaseMoveCost) * moveSpeed
            >= Terrain.moveCost terrain next * Agent.MoveSpeedDefault

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
                | Advancing(r, next, _, startProgress) when wouldComplete next startProgress agents.[idx].MoveSpeed ->
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
                    wouldComplete next startProgress agents.[idx].MoveSpeed
                    && not (Map.containsKey idx yieldedTo)
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
            | Advancing(r, next, _, startProgress) when not (wouldComplete next startProgress a.MoveSpeed) ->
                // Still mid-edge: accumulate progress, no cell change, no
                // event (a continuous fact fully recoverable from the
                // resulting `AgentState.Progress`, like an idle agent's tick).
                // `Route = Some r` must still be written back — otherwise
                // next tick's cache check finds no route, recomputes one,
                // and `startProgress` resets to 0 every tick forever. The
                // increment is always `Terrain.BaseMoveCost` (TASK-049): only
                // `wouldComplete`'s threshold check scales with `MoveSpeed`.
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

    // --- Phase: combat -------------------------------------------------------
    // Realised by TASK-031 (backlog B-019). Turns the docs/04 section 12.8
    // no-op into a real phase: deterministic hitscan combat with directional
    // cover mitigation. Runs after Navigation (so a shot resolves against this
    // tick's post-movement positions) and before StateConsequences (TASK-032)
    // / the still no-op Mission.
    //
    // Suppression realised by TASK-032 (backlog B-020): a qualifying shot
    // also raises the target's AgentState.Suppression (Suppression.gain,
    // independent of a hit, mitigated by the same directional Terrain.cover
    // geometry as hit chance). This phase therefore now writes AgentState and
    // copies s.Agents like every other writing phase, instead of aliasing it.
    //
    // For every agent, in ascending id order (both sides — combat is
    // symmetric, docs/05 section 12 "enemy agents use the same perception and
    // combat rules where practical"):
    //
    //   1. Candidates are the agent's own AgentState.VisibleContacts (TASK-026)
    //      resolved to full AgentState — never a scan of every agent, per risk
    //      R-023 "same observation contract". Because Combat runs three phases
    //      after Perception, a candidate's position may have moved since it
    //      was observed; Combat.chooseTarget re-verifies line of fire fresh
    //      against each candidate's CURRENT cell. A Suppressing agent
    //      (TASK-037, backlog B-030 thin slice) narrows this to its one named
    //      contact instead of every VisibleContacts entry — Combat.chooseTarget
    //      itself is unchanged, still re-verifying range/line-of-fire fresh.
    //   2. Combat.chooseTarget picks the nearest candidate within
    //      CombatConfig.WeaponRange and current Sight.visible line of fire,
    //      ties broken by ascending AgentId. No candidate qualifies -> no
    //      shot, no event (the sparse-event precedent every other phase
    //      observes).
    //   3. On a qualifying target, Combat.hitChance (range + directional
    //      Terrain.cover on the edge the shot arrives from) gives a 0..1000
    //      chance; one RandomStream.next draw decides hit or miss. This is the
    //      deterministic stream's first real gameplay consumer —
    //      WorldState.Random is already part of Canonical.encode (TASK-003),
    //      so no Canonical.FormatVersion bump: only the values a draw produces
    //      are new, not what is hashed.
    //   4. Emit ShotFired(shooter, target, hit), then raise the target's
    //      Suppression (TASK-032, backlog B-020) via Suppression.gain — no
    //      wound, death, or other consequence yet (B-031, deliberately out of
    //      scope).
    //
    // TASK-045 (backlog B-031) adds wounds: a non-Alive agent never shoots
    // (excluded from the shooter loop entirely) and is never a valid
    // candidate (docs/04 section 20: "dead or incapacitated agents do not
    // start new actions" — extended here to "and are not shot at again");
    // Combat.chooseTarget itself is unchanged, still re-verifying range/
    // line-of-fire fresh, just over an already-Alive-filtered candidate set.
    // On a qualifying HIT only (not a miss — a wound is physical, unlike
    // Suppression.gain, which docs/04 section 12.8 explicitly wants
    // independent of a hit), Casualty.wound is applied to the target's
    // Vitals and RecentlyWounded is set true (the "agent is wounded"
    // reappraisal trigger, read and cleared by the following tick's
    // Appraisal phase). The tick health first reaches zero emits
    // AgentIncapacitated — a genuine state transition, the
    // CommitmentEstablished precedent (emitted once, not every tick the
    // agent stays down).
    let private combat (s: StepState) =
        let terrain = s.Terrain
        let threats = s.TacticalKnowledge
        let candidateSource = s.Agents // ascending by id; candidate lookup only, never mutated

        // This tick's already-finalised SuppressionBand latch, the
        // Simulation.appraisal / commitmentAndLocalAction precedent — needed
        // only for Commitment.ofAgent's Assaulting-stage derivation below,
        // which this phase itself never reads (Decision K: ammo, not stage,
        // gates firing for every commitment alike).
        let suppressedThreats =
            candidateSource |> Array.filter (fun a -> a.SuppressionBand) |> Array.map (fun a -> a.Id)

        let agents = Array.copy s.Agents
        let mutable random = s.Random

        for shooter in candidateSource do
            if Casualty.isAlive shooter.Vitals && Ammo.canFire shooter.Ammo then
                let candidates =
                    // A Suppressing agent (TASK-037, backlog B-030 thin slice)
                    // pins its named contact as the only candidate — a
                    // deliberate Suppress order does not silently retarget onto
                    // whatever else wanders into view — still gated by this
                    // tick's actual VisibleContacts, range, and line of fire via
                    // the unchanged Combat.chooseTarget below (Decision E).
                    // Every other commitment (Holding, Moving, Withdrawing,
                    // Assaulting — TASK-047, backlog B-030 proper) engages
                    // ordinarily: "cross danger area" and "clear immediate
                    // threat" both rely on this unmodified automatic
                    // engagement, not special targeting logic.
                    match
                        Commitment.ofAgent
                            threats
                            suppressedThreats
                            shooter.Position
                            shooter.Order
                            shooter.Disposition
                            shooter.Destination
                    with
                    | Suppressing sc ->
                        shooter.VisibleContacts
                        |> Array.filter (fun id -> id = sc.Target)
                        |> Array.choose (fun id ->
                            candidateSource |> Array.tryFind (fun a -> a.Id = id && Casualty.isAlive a.Vitals))
                    | Holding
                    | Moving _
                    | Withdrawing _
                    | Assaulting _ ->
                        shooter.VisibleContacts
                        |> Array.choose (fun id ->
                            candidateSource |> Array.tryFind (fun a -> a.Id = id && Casualty.isAlive a.Vitals))

                match Combat.chooseTarget terrain shooter candidates with
                | None -> ()
                | Some target ->
                    let chance = Combat.hitChance terrain shooter.Position target.Position
                    let struct (draw, next) = RandomStream.next random
                    random <- next
                    let hit = (draw % 1000UL) < uint64 chance
                    emit (ShotFired(shooter.Id, target.Id, hit)) s

                    // TASK-047 (backlog B-030 proper, Decision K): one round
                    // consumed per qualifying shot, every commitment alike.
                    let shooterIdx = agents |> Array.findIndex (fun a -> a.Id = shooter.Id)
                    agents.[shooterIdx] <- { agents.[shooterIdx] with Ammo = Ammo.fire shooter.Ammo }

                    let gain = Suppression.gain terrain shooter.Position target.Position hit
                    let idx = agents |> Array.findIndex (fun a -> a.Id = target.Id)
                    let t = agents.[idx]

                    let vitals, recentlyWounded =
                        if hit then Casualty.wound t.Vitals, true else t.Vitals, t.RecentlyWounded

                    // TASK-058 (backlog B-016b): a qualifying hit also has a
                    // CommsConfig.RadioDestroyChanceOnHit chance to
                    // permanently destroy the target's radio -- an
                    // independent RandomStream draw, alongside the existing
                    // hit-chance roll, rolled only when the world authors a
                    // Headquarters (the opt-in gate: every one of the 16
                    // pre-existing corpus entries draws nothing extra here)
                    // and the radio is not already destroyed (sticky, no
                    // re-roll, the Vitals.Dead one-way-door precedent).
                    let radioDestroyed =
                        if hit && s.Headquarters.IsSome && not t.RadioDestroyed then
                            let struct (draw, next) = RandomStream.next random
                            random <- next
                            draw % 1000UL < uint64 CommsConfig.RadioDestroyChanceOnHit
                        else
                            t.RadioDestroyed

                    agents.[idx] <-
                        { t with
                            Suppression = Suppression.raise t.Suppression gain
                            Vitals = vitals
                            RecentlyWounded = recentlyWounded
                            RadioDestroyed = radioDestroyed }

                    if hit then
                        match t.Vitals, vitals with
                        | Alive _, Incapacitated _ -> emit (AgentIncapacitated(target.Id, target.Position)) s
                        | _ -> ()

                        if radioDestroyed && not t.RadioDestroyed then
                            emit (AgentRadioDestroyed(target.Id, target.Position)) s

        s.Agents <- agents
        s.Random <- random

    // --- Phase: state consequences ------------------------------------------
    // Realised by TASK-032 (backlog B-020) for "update suppression decay":
    // every agent's Suppression drops by SuppressionConfig.DecayPerTick,
    // floored at 0, unconditionally (whether or not it was shot at this
    // tick — a same-tick hit's gain and this decay both apply, in that
    // order).
    //
    // "Update stress from recent events" realised by TASK-033 (backlog
    // B-021): every agent's Stress rises by StressConfig.GainPerTick this
    // tick when its (Perception-phase, this-tick) VisibleContacts is
    // non-empty (Stress.gain), then always decays by
    // StressConfig.DecayPerTick, floored at 0 (Stress.decay) — gain then
    // decay, the Suppression precedent, both steps here since nothing else
    // produces stress yet.
    //
    // "Apply deaths and incapacitation" and "update command succession"
    // realised by TASK-045 (backlog B-031):
    //   * every Incapacitated agent's bleed-out countdown ticks down
    //     (Casualty.tickBleedOut); reaching Dead emits AgentDied — no rescue
    //     mechanic, the countdown always runs to completion (Central
    //     decision 3);
    //   * leadership is a pure derived rule (the lowest-id living Friendly
    //     agent, Domain.Agent's own AgentId ordering — no stored field, the
    //     Commitment.ofAgent precedent), computed once from the agents array
    //     as it stands after this phase's own bleed-out mutations and
    //     compared against s.InitialLeader — the TRUE start-of-tick
    //     baseline, captured once in Simulation.step before any phase ran
    //     (StepState's own doc comment explains why: Combat runs before this
    //     phase and can already have incapacitated the leader, so a
    //     before/after pair taken from THIS phase's own entry state would
    //     silently miss that transition, only ever catching this phase's own
    //     bleed-out-to-Dead one). A difference emits LeadershipTransferred;
    //   * squad failure — s.InitialFriendlyAlive true but no Friendly agent
    //     Alive after this phase's mutations — emits SquadFailure exactly
    //     once, the tick it first becomes true (health never regenerates, so
    //     this is a one-way latch, not a hysteresis band). A **signal event
    //     only**: it does not halt Simulation.step, reject further commands,
    //     or write any new WorldState field — consuming it into an actual
    //     mission-failure outcome is B-032's job (docs/04 section 12.10's
    //     Mission phase, still a no-op). Scoped to Friendly only (docs/04
    //     section 10: "every Friendly agent is the one squad" — no
    //     equivalent mission concept exists for Hostile).
    let private stateConsequences (s: StepState) =
        let agents = Array.copy s.Agents

        for i in 0 .. agents.Length - 1 do
            let a = agents.[i]

            let suppression =
                if a.Suppression > 0 then
                    Suppression.decay a.Suppression
                else
                    a.Suppression

            let stress =
                Stress.gain (a.VisibleContacts.Length > 0)
                |> Stress.raise a.Stress
                |> Stress.decay

            let vitals = Casualty.tickBleedOut a.Vitals

            // TASK-047 (backlog B-030 proper, Decisions M/N): an agent
            // standing on an authored resupply cell is refilled instantly,
            // short-circuiting any in-progress reload — checked first, so a
            // resupplied agent never also runs Ammo.tick the same tick.
            // Otherwise Ammo.tick's own reload bookkeeping runs: an empty
            // magazine with reserve remaining starts reloading
            // (ReloadStarted), a Reloading state counts down and refills on
            // completion (ReloadCompleted), anything else is unchanged.
            let ammo =
                if s.ResupplyAreas |> Array.contains a.Position then
                    if Ammo.isFull a.Ammo then
                        a.Ammo
                    else
                        emit (AgentResupplied a.Id) s
                        Ammo.resupply a.Ammo
                else
                    let next, justStarted, justCompleted = Ammo.tick a.Ammo

                    if justStarted then
                        emit (ReloadStarted a.Id) s
                    elif justCompleted then
                        emit (ReloadCompleted a.Id) s

                    next

            if
                suppression <> a.Suppression
                || stress <> a.Stress
                || vitals <> a.Vitals
                || ammo <> a.Ammo
            then
                agents.[i] <-
                    { a with
                        Suppression = suppression
                        Stress = stress
                        Vitals = vitals
                        Ammo = ammo }

            match a.Vitals, vitals with
            | Incapacitated _, Dead -> emit (AgentDied(a.Id, a.Position)) s
            | _ -> ()

        // Compared against s.InitialLeader / .InitialFriendlyAlive — the
        // TRUE start-of-tick baseline, not this phase's own entry state —
        // see StepState's own doc comment for why (Combat, which runs
        // first, can already have caused the transition this phase would
        // otherwise miss).
        let newLeader = Casualty.currentLeader agents

        if newLeader <> s.InitialLeader then
            emit (LeadershipTransferred(s.InitialLeader, newLeader)) s

        let friendlyAliveAfter =
            agents |> Array.exists (fun a -> a.Side = Friendly && Casualty.isAlive a.Vitals)

        if s.InitialFriendlyAlive && not friendlyAliveAfter then
            emit SquadFailure s

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
                      Destination = a.Destination
                      Disposition = a.Disposition })
                |> Array.sortBy (fun a -> a.Id) }

    // Runs one phase against the accumulator and appends it to the trace.
    // No-op phases are listed individually (no wildcard) so that adding a
    // phase forces a decision here.
    let private runPhase (commands: PlayerCommand list) (s: StepState) (phase: Phase) : unit =
        match phase with
        | CommandIntake -> commandIntake commands s
        | Communication -> communication s
        | Perception -> perception s
        | TacticalKnowledge -> tacticalKnowledge s
        | Appraisal -> appraisal s
        | CommitmentAndLocalAction -> commitmentAndLocalAction s
        | NavigationAndMovement -> navigationAndMovement s
        | Combat -> combat s
        | Output -> output s
        | StateConsequences -> stateConsequences s
        | Mission -> ()

        s.TraceRev <- phase :: s.TraceRev

    /// Advances the world by exactly one integer tick. Runs every phase in
    /// `Phases.order`: accepting commands at `CommandIntake`, delivering the
    /// accepted orders to reachable recipients at `Communication`, judging them
    /// at `Appraisal` (writing `Destination` on `Accepted`), and resolving
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
              ResupplyAreas = state.ResupplyAreas
              Headquarters = state.Headquarters
              Jammers = state.Jammers
              Agents = state.Agents
              PendingCommands = []
              TacticalKnowledge = state.TacticalKnowledge
              HostileTacticalKnowledge = state.HostileTacticalKnowledge
              Random = state.Random
              EventsRev = []
              Snapshot = { Tick = nextTick; Agents = [||] }
              TraceRev = []
              // TASK-045 (backlog B-031): captured from `state.Agents` — the
              // pristine, pre-tick input — before any phase runs. See
              // StepState's own doc comments for why `stateConsequences`
              // needs this instead of its own entry-state snapshot.
              InitialLeader = Casualty.currentLeader state.Agents
              InitialFriendlyAlive =
                state.Agents |> Array.exists (fun a -> a.Side = Friendly && Casualty.isAlive a.Vitals) }

        for phase in Phases.order do
            runPhase ordered acc phase

        let finalState =
            { state with
                Tick = nextTick
                Agents = acc.Agents
                TacticalKnowledge = acc.TacticalKnowledge
                HostileTacticalKnowledge = acc.HostileTacticalKnowledge
                Random = acc.Random }

        // Hashing runs strictly after the phase loop. `finalState` is already
        // fully determined; the hash is a read-only checkpoint and no
        // authoritative output depends on its value.
        { State = finalState
          Events = acc.EventsRev |> List.rev |> List.toArray
          Snapshot = acc.Snapshot
          PhaseTrace = acc.TraceRev |> List.rev |> List.toArray
          StateHash = Hashing.canonicalHasher.Hash finalState }
