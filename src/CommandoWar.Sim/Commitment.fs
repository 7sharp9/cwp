namespace CommandoWar.Sim

/// A commitment's move-specific payload (TASK-030, backlog B-018;
/// `docs/05_COMMAND_AND_AGENT_AI.md` section 9). `Command` identifies which
/// delivered order this commitment realises, so a superseding order (a
/// different `Command`) is distinguishable from the same order continuing;
/// `Target` mirrors `AgentState.Destination` for the duration of the
/// commitment — both are written from the same Appraisal-`Accepted` target.
type MoveCommitment = { Command: CommandId; Target: Cell }

/// A commitment's suppress-specific payload (TASK-037, a thin B-030 slice;
/// `docs/05` section 9). `Target` is the specific known contact
/// (`AgentId`, the `PlayerIntent.Suppress` payload) this agent is ordered to
/// keep under fire — not a re-derivable `AgentState.Destination`, since a
/// `Suppress` order never writes one (the agent holds position).
type SuppressCommitment = { Command: CommandId; Target: AgentId }

/// A commitment's withdraw-specific payload (TASK-047, backlog B-030
/// proper; `docs/05` section 9's own sketch, `Withdrawing of
/// WithdrawCommitment`). The `MoveCommitment` shape — a distinct case is
/// worth it here (unlike `Hold`, see Decision C/E in the task file) because
/// it backs a real behavioural difference, `AppraisalConfig.
/// WithdrawResolveBonus`, not just a label.
type WithdrawCommitment = { Command: CommandId; Target: Cell }

/// The Assault executor's current finite-state-machine stage (TASK-047,
/// backlog B-030 proper; `docs/05` section 10's staged-FSM example). A
/// **pure per-tick derivation** from `(Position, Target,
/// WorldState.TacticalKnowledge, SuppressionBand)` — no new stored per-tick
/// counter, the `Commitment` "derived, not stored" precedent extended one
/// level deeper (`TASK-047-...md` Decision F/G/H):
///
///   * `ApproachingStart`: still closing distance toward `Target` (beyond
///     `AppraisalConfig.AssaultStartRange`) — ordinary Navigation, no
///     gating.
///   * `AwaitingSupport`: within `AssaultStartRange`, and at least one known
///     threat contact within `AppraisalConfig.ThreatEngagementRange` of
///     `Target` is not currently `SuppressionBand`-latched. The executor
///     (`Simulation.commitmentAndLocalAction`) freezes `AgentState.
///     Destination` to `None` for as long as this stage holds — Navigation
///     simply sees no destination and does not move the agent. **No
///     timeout**: if the player never suppresses the blocking threat, the
///     assault stalls indefinitely (a deliberate, legible, reversible
///     outcome, not a bug needing a stored wait-counter).
///   * `Advancing`: within `AssaultStartRange`, no unsuppressed threat
///     blocking — ordinary Navigation resumes. "Cross danger area" needs no
///     special handling: the already-automatic, already-symmetric `Combat`
///     phase engages any visible hostile along the way exactly as it does
///     for any other commitment.
///   * `ClearingThreat`: `Position = Target`, and at least one known threat
///     contact within `AppraisalConfig.AssaultClearRadius` of `Target` is
///     not currently suppressed. The agent holds at the target while
///     `Combat`'s existing automatic engagement fires at any visible
///     defender. Once no such contact remains, the order is fulfilled
///     (`commitmentAndLocalAction`'s own check) — "report complete" is the
///     existing `CommitmentCompleted` event, no new event type.
type AssaultStage =
    | ApproachingStart
    | AwaitingSupport
    | Advancing
    | ClearingThreat

/// A commitment's assault-specific payload (TASK-047, backlog B-030
/// proper; `docs/05` section 9's own sketch, `Assaulting of
/// AssaultCommitment`).
type AssaultCommitment =
    { Command: CommandId
      Target: Cell
      Stage: AssaultStage }

/// What an agent is currently committed to (`docs/04_SIMULATION_SPEC.md`
/// section 12.6; `docs/05` section 9). TASK-030 realised the two cases
/// `MoveTo` could produce or act on; TASK-037 added the case `Suppress`
/// produces; TASK-047 adds `Withdrawing`/`Assaulting`. `Hold` needs no new
/// case (`TASK-047-...md` Decision C): its accepted branch writes a
/// `Destination` exactly as `MoveTo` does, so it already produces `Moving`
/// while en route and falls to bare `Holding` on arrival, with nothing
/// behaviourally distinguishing an ordered hold from an idle agent once
/// arrived — a payload case (`docs/05`'s own `Holding of HoldCommitment`
/// sketch) would carry no information a diagnostic overlay cannot already
/// read from `Order`/`Disposition` directly (`AGENTS.md` "do not build
/// speculative type machinery").
type Commitment =
    | Holding
    | Moving of MoveCommitment
    | Suppressing of SuppressCommitment
    | Withdrawing of WithdrawCommitment
    | Assaulting of AssaultCommitment

[<RequireQualifiedAccess>]
module Commitment =

    /// Whether a known threat contact, not currently `SuppressionBand`-
    /// latched, sits within `range` Chebyshev cells of `target` (TASK-047) —
    /// the shared predicate behind `AwaitingSupport` (`range =
    /// AppraisalConfig.ThreatEngagementRange`) and `ClearingThreat` (`range
    /// = AppraisalConfig.AssaultClearRadius`). Reuses the identical
    /// known-contact epistemic model `Appraisal.cellPressure` reads from
    /// (never authoritative hostile state, risk R-023) and the identical
    /// `SuppressionBand` latch `Suppress`/TASK-037 already reads.
    let hasUnsuppressedThreatWithin
        (threats: Contact[])
        (suppressedThreats: AgentId[])
        (range: int)
        (target: Cell)
        : bool =
        threats
        |> Array.exists (fun t ->
            not (suppressedThreats |> Array.contains t.Contact)
            && Perception.chebyshev t.LastKnownCell target <= range)

    /// The Assault executor's stage for an agent at `position` assaulting
    /// `target` — see `AssaultStage`'s own doc comment for each boundary's
    /// reasoning. The `position = target` branch's `Advancing` fallback is
    /// unreachable in practice: a fulfilled Assault's `Order` is already
    /// cleared by `commitmentAndLocalAction` before this is ever called
    /// again for that agent (the `Commitment.ofAgent` totality precedent —
    /// a pure leaf stays total on every input, even one its own caller
    /// never actually produces).
    let assaultStage
        (threats: Contact[])
        (suppressedThreats: AgentId[])
        (position: Cell)
        (target: Cell)
        : AssaultStage =
        if position = target then
            if hasUnsuppressedThreatWithin threats suppressedThreats AppraisalConfig.AssaultClearRadius target then
                ClearingThreat
            else
                Advancing
        elif Perception.chebyshev position target > AppraisalConfig.AssaultStartRange then
            ApproachingStart
        elif hasUnsuppressedThreatWithin threats suppressedThreats AppraisalConfig.ThreatEngagementRange target then
            AwaitingSupport
        else
            Advancing

    /// Derives the agent's current commitment from its order-appraisal state
    /// (`docs/04` section 12.6). Pure and total. `threats`/
    /// `suppressedThreats`/`position` (TASK-047) are needed only for
    /// `Assaulting`'s `Stage`; every other case ignores them, the identical
    /// argument `Appraisal.appraise` already makes for taking more context
    /// than every branch uses. `Moving` iff the agent holds an `Accepted`
    /// order (any intent — `MoveTo` or `Hold`, Decision C) with a
    /// destination still outstanding; `Suppressing` iff `Accepted Suppress`
    /// (never writes a `Destination`); `Withdrawing` iff `Accepted Withdraw`
    /// with a destination outstanding (checked ahead of the generic `Moving`
    /// arm so it is not shadowed); `Assaulting` iff `Accepted Assault`,
    /// regardless of `destination` (which this task's executor may freeze to
    /// `None` mid-assault, Decision G — checked ahead of the generic
    /// `Suppressing`-shaped "no destination" reading, since it uses `_`, not
    /// `None`, for that slot); `Holding` otherwise (no order, a `Refused` /
    /// `Unable` order, or an `Accepted` order already fulfilled —
    /// `Destination` cleared by `commitmentAndLocalAction`'s housekeeping).
    /// `Suppressing`/`Assaulting`'s `AwaitingSupport` stage have no
    /// fulfilled state of their own beyond what `commitmentAndLocalAction`
    /// computes — `Suppressing` ends only by supersession.
    ///
    /// This is fully recoverable from fields already in `Canonical.encode`
    /// (`Order`, `Disposition`, `Destination`) plus already-canonical world
    /// context (`WorldState.TacticalKnowledge`, `AgentState.
    /// SuppressionBand`) — the identical argument that keeps
    /// `AgentState.Route` out of the canonical image (`docs/04` section
    /// 17 "derived caches either excluded or normalised"). `Commitment` is
    /// therefore deliberately **not** a stored `AgentState` field: storing it
    /// again would duplicate state that can silently drift from its source
    /// fields, where deriving it on demand cannot drift by construction.
    let ofAgent
        (threats: Contact[])
        (suppressedThreats: AgentId[])
        (position: Cell)
        (order: ReceivedOrder option)
        (disposition: OrderDisposition option)
        (destination: Cell option)
        : Commitment =
        match order, disposition, destination with
        | Some { Command = command; Intent = Withdraw _ }, Some Accepted, Some dest ->
            Withdrawing { Command = command; Target = dest }
        | Some { Command = command; Intent = Assault target }, Some Accepted, _ ->
            Assaulting
                { Command = command
                  Target = target
                  Stage = assaultStage threats suppressedThreats position target }
        | Some o, Some Accepted, Some target -> Moving { Command = o.Command; Target = target }
        | Some { Command = command; Intent = Suppress target }, Some Accepted, None ->
            Suppressing { Command = command; Target = target }
        | _ -> Holding
