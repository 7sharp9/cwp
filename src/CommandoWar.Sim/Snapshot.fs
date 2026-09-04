namespace CommandoWar.Sim

/// Per-agent read model for clients. Values only, never references to
/// authoritative storage (docs/03_ARCHITECTURE.md section 12).
type AgentSnapshot =
    { Id: AgentId
      Side: Side
      Position: Cell
      /// Integer progress toward entering the next cell along the agent's
      /// route (TASK-018, `AgentState.Progress`). 0 at rest. A client
      /// renderer interpolates between `Position` and its next cell using
      /// this and the terrain movement-cost threshold (docs/04 section 8).
      Progress: int
      Destination: Cell option }

/// Framework-neutral render snapshot for one tick. Agents are ordered by
/// ascending id. The snapshot contains values, not references to
/// authoritative stores.
type RenderSnapshot =
    { Tick: int64
      Agents: AgentSnapshot[] }
