namespace CommandoWar.Sim

/// What a command asks an agent to do. Only movement is needed for the
/// framework spikes; Hold, Suppress, Assault and Withdraw
/// (docs/04_SIMULATION_SPEC.md section 13) are added by later tasks.
type PlayerIntent =
    | MoveTo of target: Cell

/// How urgently an order should be acted on, relative to an agent's current
/// activity. Inert envelope data (TASK-020): `docs/05_COMMAND_AND_AGENT_AI.md`
/// section 5 stage 4 names "urgency ... encoded by the order" as a
/// resolve-threshold input, but order appraisal (backlog B-017) does not exist
/// yet and nothing reads this value.
type Urgency =
    | Routine
    | Immediate

/// How much risk an order sanctions. Inert envelope data (TASK-020):
/// `docs/05_COMMAND_AND_AGENT_AI.md` section 5 stage 4 names "risk tolerance
/// encoded by the order" as a resolve-threshold input; carried through the
/// envelope, not consumed (backlog B-017).
type RiskTolerance =
    | Cautious
    | Standard
    | Aggressive

/// A player command envelope. This is a **partial** realisation of the
/// `docs/04_SIMULATION_SPEC.md` section 13 envelope (TASK-020): command id,
/// recipients, issue tick, urgency and risk tolerance are present. Issuer
/// identity is deliberately not modelled (the vertical slice has one player).
///
/// `IssuedAtTick` (TASK-024, renamed from `IssueTick`) is the tick on which
/// the order was issued by the commander. It is **envelope provenance and a
/// future appraisal input** (`docs/05_COMMAND_AND_AGENT_AI.md` section 5
/// stage 4 / section 14: an order is aged from when it was issued), carried
/// inside the authoritative command envelope and replayed verbatim. It is a
/// **different concept** from `RecordedCommand.Tick` (`Replay.fs`), the
/// caller-owned tick on which the command is delivered to command intake; the
/// two are independent, and the legacy `.cwlog` fixture-script format
/// collapses them to its single tick field. Command intake enforces one rule
/// on it: `IssuedAtTick` must be in `[0, currentTick]` — a command cannot
/// have been issued in the future or before tick 0
/// (`CommandRejection.IssueTickOutOfRange`). Staleness (a long-delayed
/// order) is not a validation concern; whether to follow an outdated order is
/// agent appraisal's judgement (backlog B-017).
type PlayerCommand =
    { Id: CommandId
      /// The tick the commander issued this order on. Provenance and a future
      /// appraisal input; independent of the delivery tick. Must lie in
      /// `[0, currentTick]` at command intake.
      IssuedAtTick: int64
      /// The agents this one command addresses. Generalised from a single
      /// `Agent` field by TASK-020. `Command.moveTo` builds a one-element
      /// list; `Command.moveToMany` builds a multi-recipient one. Command
      /// intake rejects an empty list (`EmptyRecipients`) and a list naming
      /// the same `AgentId` twice (`DuplicateRecipient`), and emits one
      /// accept/reject event per (command, recipient) pair.
      Recipients: AgentId list
      Urgency: Urgency
      RiskTolerance: RiskTolerance
      Intent: PlayerIntent }

    /// The first recipient. Back-compatible read accessor for the
    /// pre-TASK-020 single-`Agent` shape, kept so existing single-recipient
    /// call sites and tests read unchanged. Throws on an empty `Recipients`
    /// list, which the `Command` builders never produce.
    member this.Agent: AgentId = List.head this.Recipients

/// Why the simulation refused a command during command intake. Refusal is
/// explicit and deterministic. A syntactically valid command may still be
/// declined later by agent appraisal, which is a separate concept
/// (docs/04_SIMULATION_SPEC.md section 13).
type CommandRejection =
    | UnknownAgent of agent: AgentId
    | TargetOutOfBounds of target: Cell
    /// Malformed: the command named zero recipients. Whole-command rejection,
    /// one `CommandRejected`, no per-recipient events.
    | EmptyRecipients
    /// Malformed: the same `AgentId` appears more than once in one command's
    /// `Recipients`. Whole-command rejection naming the first repeated id.
    /// Not silently normalised — a duplicate would emit two `CommandAccepted`
    /// events for one agent and double-count the order in any later
    /// per-agent appraisal or action-cost pass.
    | DuplicateRecipient of agent: AgentId
    /// Unauthorised: `agent` is on the `Hostile` side. The vertical slice's
    /// one player commands `Friendly`-side agents only
    /// (`docs/05_COMMAND_AND_AGENT_AI.md` section 12: enemies use a
    /// non-player-commanded doctrine). Per-recipient rejection — a `Friendly`
    /// co-recipient of the same command still accepts. This is the whole of
    /// "authorisation" here: no issuer identity or per-agent permission model.
    | UnauthorisedRecipient of agent: AgentId
    /// Duplicate: `command` appeared more than once in this tick's incoming
    /// command batch. Every command sharing that id is rejected and none is
    /// processed, so the outcome does not depend on batch order (the batch is
    /// stably sorted by id in `Simulation.step`). Scoped to one tick's batch;
    /// cross-tick duplicate-id tracking is a **replay-log** invariant, not
    /// authoritative state — `Replay.validate` rejects a log that reuses a
    /// `CommandId` on two ticks (`ReplayError.DuplicateCommandIdInLog`), and
    /// `WorldState` holds no command history (TASK-024).
    | DuplicateCommandId of command: CommandId
    /// Ineligible: `issuedAtTick` is outside `[0, tick]` — the command claims
    /// to have been issued in the future (after the tick being processed) or
    /// before tick 0 (TASK-024, `docs/04_SIMULATION_SPEC.md` section 2 "a
    /// command becomes eligible on a specified tick"). Whole-command
    /// rejection, one `CommandRejected`. A command issued on an *earlier*
    /// tick and delivered now is accepted: staleness is appraisal's concern
    /// (backlog B-017), not command intake's.
    | IssueTickOutOfRange of issuedAtTick: int64 * tick: int64

[<RequireQualifiedAccess>]
module Command =

    /// Builds a single-recipient move command with the default envelope
    /// (`Urgency = Routine`, `RiskTolerance = Standard`). The four-argument
    /// signature is unchanged since TASK-003, so every existing call site is
    /// unaffected by the TASK-020 generalisation or the TASK-024
    /// `issueTick -> issuedAtTick` parameter rename (callers pass it
    /// positionally).
    let moveTo (id: CommandId) (issuedAtTick: int64) (agent: AgentId) (target: Cell) : PlayerCommand =
        { Id = id
          IssuedAtTick = issuedAtTick
          Recipients = [ agent ]
          Urgency = Routine
          RiskTolerance = Standard
          Intent = MoveTo target }

    /// Builds a move command addressing several agents, with an explicit
    /// urgency and risk tolerance. Exercised only from code (TASK-020's unit
    /// tests): the legacy `.cwlog` fixture grammar is one recipient per line
    /// and cannot express multi-recipient addressing, urgency, or risk
    /// tolerance (backlog B-045).
    let moveToMany
        (id: CommandId)
        (issuedAtTick: int64)
        (agents: AgentId list)
        (target: Cell)
        (urgency: Urgency)
        (riskTolerance: RiskTolerance)
        : PlayerCommand =
        { Id = id
          IssuedAtTick = issuedAtTick
          Recipients = agents
          Urgency = urgency
          RiskTolerance = riskTolerance
          Intent = MoveTo target }
