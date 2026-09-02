namespace CommandoWar.Sim

/// What happened during a tick. Events state facts, not renderer actions
/// (docs/03_ARCHITECTURE.md section 12). Audio and visual effects are client
/// interpretations of these events.
type EventBody =
    | CommandAccepted of command: CommandId * agent: AgentId * destination: Cell
    | CommandRejected of command: CommandId * reason: CommandRejection
    | MovementStepped of agent: AgentId * from: Cell * into: Cell
    | MovementCompleted of agent: AgentId * at: Cell

/// An immutable domain event tagged with the tick it occurred on. Within a
/// single step, events are emitted in a stable order: command outcomes
/// first, in ascending command id, then movement outcomes in ascending
/// agent id.
type DomainEvent =
    { Tick: int64
      Body: EventBody }
