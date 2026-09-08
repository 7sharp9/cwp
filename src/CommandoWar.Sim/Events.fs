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
    /// `contact` was removed from the friendly squad's shared
    /// `TacticalKnowledge` this tick, having gone unseen for
    /// `PerceptionConfig.ExpireAfter` ticks (TASK-026, `docs/04` section 12.4
    /// "decay or expire stale contacts"). `lastKnownCell` is where it was last
    /// observed. Emitted by the Tactical-knowledge phase on removal only.
    | ContactExpired of contact: AgentId * lastKnownCell: Cell

/// An immutable domain event tagged with the tick it occurred on. Within a
/// single step, events are emitted in a stable order:
///   1. command outcomes, ascending command id (`CommandAccepted` /
///      `CommandRejected`, from the Command-intake phase);
///   2. order-delivery failures, ascending `(recipient, command)` id
///      (`OrderUndelivered`, from the Communication phase — TASK-027);
///   3. this tick's perception events — every `ContactObserved` ascending
///      `(observer, contact)`, then every `ContactExpired` ascending contact
///      id (from the Perception / Tactical-knowledge phases);
///   4. movement outcomes, ascending agent id.
/// The order follows `Phases.order` (Command intake, Communication,
/// Perception, Tactical knowledge, Navigation and movement), so a contact is
/// observed at its start-of-tick position and a delivered order takes effect
/// the same tick.
type DomainEvent =
    { Tick: int64
      Body: EventBody }
