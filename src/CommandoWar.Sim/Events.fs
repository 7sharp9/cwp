namespace CommandoWar.Sim

/// What happened during a tick. Events state facts, not renderer actions
/// (docs/03_ARCHITECTURE.md section 12). Audio and visual effects are client
/// interpretations of these events.
type EventBody =
    | CommandAccepted of command: CommandId * agent: AgentId * destination: Cell
    | CommandRejected of command: CommandId * reason: CommandRejection
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

/// An immutable domain event tagged with the tick it occurred on. Within a
/// single step, events are emitted in a stable order: command outcomes
/// first, in ascending command id, then movement outcomes in ascending
/// agent id.
type DomainEvent =
    { Tick: int64
      Body: EventBody }
