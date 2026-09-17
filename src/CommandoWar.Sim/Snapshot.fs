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
      Destination: Cell option
      /// This agent's current order-appraisal outcome (TASK-028's
      /// `AgentState.Disposition`, exposed to clients for the first time by
      /// TASK-042/backlog B-028: "order acknowledgement and disposition" /
      /// "concise explanations for refusal" are player-facing requirements,
      /// docs/06 section 8/11). `None` when the agent holds no current order
      /// or it has not yet been appraised this tick.
      Disposition: OrderDisposition option }

/// Framework-neutral render snapshot for one tick. Agents are ordered by
/// ascending id. The snapshot contains values, not references to
/// authoritative stores.
type RenderSnapshot =
    { Tick: int64
      Agents: AgentSnapshot[] }
