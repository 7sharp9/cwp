namespace CommandoWar.Sim

/// Why the Communication phase could not deliver an accepted order to a
/// recipient (TASK-027, backlog B-016; `docs/04_SIMULATION_SPEC.md` section
/// 12.2 "communication failure must be explicit, not silently ignored").
///
/// One case for this task's scope: the recipient's
/// `AgentState.CommunicationAvailable` is `false` (an authored comms
/// blackout). `UnableToCommunicate` matches the
/// `docs/05_COMMAND_AND_AGENT_AI.md` section 7 `DecisionReason` name so order
/// appraisal (backlog B-017) can carry it through unchanged. Radio range
/// (`OutOfRange`), dynamic jamming (`Jammed`), and a destroyed radio
/// (`RadioDestroyed`) are backlog B-016b.
type DeliveryFailure =
    | UnableToCommunicate

/// What happened during a tick. Events state facts, not renderer actions
/// (docs/03_ARCHITECTURE.md section 12). Audio and visual effects are client
/// interpretations of these events.
type EventBody =
    | CommandAccepted of command: CommandId * agent: AgentId * destination: Cell
    | CommandRejected of command: CommandId * reason: CommandRejection
    /// The Communication phase could not deliver order `command` to
    /// `recipient` this tick — the recipient's
    /// `AgentState.CommunicationAvailable` is `false` (TASK-027, backlog
    /// B-016; `docs/04` section 12.2, section 14 "order delivered or
    /// communication failed"). The order is dropped: no `Destination` is
    /// written, and a `Destination` the recipient already held is left
    /// untouched (an undelivered new order does not cancel an order in
    /// progress). `docs/05` section 16 "Lost communication": the trace shows
    /// communication failure, not disobedience. A successful zero-delay
    /// delivery emits no event — `CommandAccepted` (at intake, carrying the
    /// destination) already records it; an `OrderDelivered` success event
    /// arrives with delayed delivery (B-016b).
    | OrderUndelivered of command: CommandId * recipient: AgentId * reason: DeliveryFailure
    | MovementStepped of agent: AgentId * from: Cell * into: Cell
    | MovementCompleted of agent: AgentId * at: Cell
    /// The agent holds a destination but no traversable path connects its
    /// current cell to it (`Pathfinding` returned `NoPath`, `BudgetExhausted`,
    /// or `InvalidEndpoint` — the last covers a `MoveTo` onto an impassable
    /// cell, which command intake accepts because it only range-checks the
    /// target). The Navigation and movement phase clears the destination when
    /// it emits this, so the agent does not retry every tick.
    | MovementBlocked of agent: AgentId * at: Cell * target: Cell
    /// `agent`, at `at`, held a destination and computed a path whose next
    /// cell (`contested`) another agent also computed as its own next cell
    /// this tick; `winner` — chosen by fewer remaining route steps, ties
    /// broken by ascending agent id (docs/04 section 8 step 3) — took it
    /// instead. `agent`'s destination and route are unchanged, so it retries
    /// the same next cell next tick, once `winner` has vacated it. A
    /// same-tick reservation only: nothing is booked across ticks (TASK-017).
    | MovementYielded of agent: AgentId * at: Cell * contested: Cell * winner: AgentId
    /// `agent`, at `at`, would have completed its edge into `blocked` this
    /// tick, but `blocked` is currently held by `occupant` — an agent that did
    /// not vacate it this tick (idle, arrived, blocked, still mid-edge, a rival
    /// contest loser, or itself obstructed further down the chain). `agent`'s
    /// destination and route are unchanged, so it retries the same next cell
    /// next tick. Distinct from `MovementBlocked` (no traversable path exists;
    /// the destination is cleared) and `MovementYielded` (lost a same-tick
    /// rival contest for the cell to another *mover*). The vacation-chain
    /// resolution (TASK-022) is a same-tick pure function of pre-tick positions
    /// and this tick's intents; nothing is booked across ticks. Persistent
    /// obstruction (a blocker that never moves) is a perception / appraisal
    /// concern (B-015 / B-017), not resolved here.
    | MovementObstructed of agent: AgentId * at: Cell * blocked: Cell * occupant: AgentId
    /// `observer` newly sees `contact` (an opposing-side agent) at `at` this
    /// tick (TASK-026, `docs/04` section 14 "contact observed or reported").
    /// Emitted by the Perception phase on a *new* sighting only — the tick a
    /// contact enters `observer`'s `VisibleContacts`, not every tick it stays
    /// there (`docs/04` section 14 "Do not emit a flood of low-value events").
    /// Both sides observe; only friendly observations reach the shared
    /// `WorldState.TacticalKnowledge` (a hostile squad picture is B-022).
    | ContactObserved of observer: AgentId * contact: AgentId * at: Cell
    /// The Appraisal phase judged `agent`'s current order `command` this tick
    /// (TASK-028, backlog B-017; `docs/04` section 14 "order appraisal
    /// outcome", section 12.5). `disposition` carries the outcome and, for
    /// `Refused` / `Unable`, the primary and supporting `DecisionReason`s (the
    /// `docs/04` section 20 invariant "every refusal contains at least one
    /// structured reason" holds by construction). Emitted for **every**
    /// appraisal, including the mundane `Accepted` — the G3 developer trace
    /// (`docs/07` section 9 criterion 11) must be able to explain any
    /// appraisal. Never emitted on a tick where the order is unchanged and
    /// already appraised (the "reappraise only on material triggers" rule,
    /// `docs/04` section 12.5). On `Accepted` the phase also writes
    /// `AgentState.Destination`; on `Refused` / `Unable` it writes none.
    | OrderAppraised of agent: AgentId * command: CommandId * disposition: OrderDisposition
    /// A fresh commitment began for `agent` this tick (TASK-030, backlog
    /// B-018; `docs/04` section 12.6, `docs/05` section 9). Emitted by
    /// `commitmentAndLocalAction` exactly when this tick's `OrderAppraised`
    /// for `(agent, command)` was `Accepted` — covers both "from `Holding`"
    /// and "supersedes an in-progress `Moving` commitment" with the same
    /// event: the prior commitment, if any, simply stops being derived (it is
    /// not stored state), so no separate event reports its end.
    | CommitmentEstablished of agent: AgentId * command: CommandId * target: Cell
    /// `agent`'s `Moving` commitment for `command` ended this tick because it
    /// reached `at` (TASK-030, backlog B-018; `docs/04` section 12.6).
    /// Relocated from the Appraisal phase's fulfilled-order housekeeping
    /// (TASK-028) — same condition, same fields cleared, now named and
    /// inspectable. Never emitted for a superseded or refused commitment —
    /// nothing is reported when a commitment ends any other way (docs/05
    /// section 11 priority 6 "new higher-priority command": the new
    /// `CommitmentEstablished` for the superseding order is the complete
    /// trace).
    | CommitmentCompleted of agent: AgentId * command: CommandId * at: Cell
    /// `contact` was removed from the friendly squad's shared
    /// `TacticalKnowledge` this tick, having gone unseen for
    /// `PerceptionConfig.ExpireAfter` ticks (TASK-026, `docs/04` section 12.4
    /// "decay or expire stale contacts"). `lastKnownCell` is where it was last
    /// observed. Emitted by the Tactical-knowledge phase on removal only.
    | ContactExpired of contact: AgentId * lastKnownCell: Cell
    /// `shooter` fired a deterministic hitscan shot at `target` this tick
    /// (TASK-031, backlog B-019; `docs/04` section 12.8), resolving `hit`
    /// (`Combat.hitChance`, mitigated by directional `Terrain.cover`,
    /// compared to a `RandomStream` draw — the stream's first real gameplay
    /// consumer). Emitted by the Combat phase for every qualifying shot only
    /// — no event when no candidate in `AgentState.VisibleContacts` is
    /// within `CombatConfig.WeaponRange` and current line of fire. `hit`
    /// carries no consequence yet: no wound, death, or suppression follows
    /// (B-020 / B-031).
    | ShotFired of shooter: AgentId * target: AgentId * hit: bool
    /// An `Order(_, Append)` command for `recipient` joined the tail of
    /// `AgentState.OrderQueue` this tick (TASK-044, backlog B-051) — the
    /// recipient already held an active order, so this one is stacked
    /// rather than taking effect immediately (`Simulation.communication`).
    /// Not emitted when `Append` targets an idle recipient: it starts
    /// immediately instead, the "zero-delay delivery emits no event"
    /// precedent.
    | OrderQueued of command: CommandId * recipient: AgentId
    /// A `Cancel target` command withdrew `target` for `agent` this tick
    /// (TASK-044, backlog B-051; `Simulation.communication`).
    /// `wasActive = true`: `target` was the active `AgentState.Order`,
    /// which is cleared and the queue head (if any) promoted in its place.
    /// `wasActive = false`: `target` was a queued entry, spliced out of
    /// `AgentState.OrderQueue` alone, leaving the active order and every
    /// other queued entry untouched.
    | OrderCancelled of command: CommandId * agent: AgentId * wasActive: bool

/// An immutable domain event tagged with the tick it occurred on. Within a
/// single step, events are emitted in a stable order:
///   1. command outcomes, ascending command id (`CommandAccepted` /
///      `CommandRejected`, from the Command-intake phase);
///   2. order-delivery and queue outcomes, ascending `(recipient, command)`
///      id (`OrderUndelivered` — TASK-027; `OrderQueued` / `OrderCancelled`
///      — TASK-044, backlog B-051; all from the Communication phase);
///   3. this tick's perception events — every `ContactObserved` ascending
///      `(observer, contact)`, then every `ContactExpired` ascending contact
///      id (from the Perception / Tactical-knowledge phases);
///   4. order appraisal outcomes, ascending agent id (`OrderAppraised`, from
///      the Appraisal phase — TASK-028; runs after Perception / Tactical
///      knowledge, before movement);
///   5. commitment outcomes, ascending agent id (`CommitmentCompleted` before
///      `CommitmentEstablished` for the same agent — impossible in practice,
///      since completion requires `Order = None` this tick and establishment
///      requires `Order = Some`; from `commitmentAndLocalAction` — TASK-030;
///      runs after Appraisal, before movement);
///   6. movement outcomes, ascending agent id;
///   7. combat outcomes, ascending shooter agent id (`ShotFired`, from the
///      Combat phase — TASK-031; runs after movement, resolving against
///      post-movement positions).
/// The order follows `Phases.order` (Command intake, Communication,
/// Perception, Tactical knowledge, Appraisal, Commitment and local action,
/// Navigation and movement, Combat), so a contact is observed at its
/// start-of-tick position, an order is appraised against this tick's
/// tactical picture, a commitment begins or ends the same tick its order is
/// appraised, a delivered-and-accepted order takes effect the same tick, and
/// a shot is resolved against this tick's post-movement positions.
type DomainEvent =
    { Tick: int64
      Body: EventBody }
