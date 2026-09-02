namespace CommandoWar.Sim

/// Per-agent read model for clients. Values only, never references to
/// authoritative storage (docs/03_ARCHITECTURE.md section 12).
type AgentSnapshot =
    { Id: AgentId
      Side: Side
      Position: Cell
      Destination: Cell option }

/// Framework-neutral render snapshot for one tick. Agents are ordered by
/// ascending id. The snapshot contains values, not references to
/// authoritative stores.
type RenderSnapshot =
    { Tick: int64
      Agents: AgentSnapshot[] }
