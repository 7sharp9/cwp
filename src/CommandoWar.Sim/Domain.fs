namespace CommandoWar.Sim

/// Which force an agent belongs to. Squads, weapons, wounds, morale, trust
/// and the other agent dimensions in docs/04_SIMULATION_SPEC.md section 11
/// are added by later tasks.
type Side =
    | Friendly
    | Hostile

/// Minimal authoritative agent state for the simulation skeleton: identity,
/// side, logical position, and an optional movement destination. Movement
/// progress within an edge, facing and stance are deferred until a real
/// movement model exists.
type AgentState =
    { Id: AgentId
      Side: Side
      Position: Cell
      Destination: Cell option }

/// Minimal authoritative world state: an integer tick, the logical grid
/// bounds, and the agents ordered by ascending id. Fields are added only
/// when an implemented behaviour requires them
/// (docs/04_SIMULATION_SPEC.md section 10).
type WorldState =
    { Tick: int64
      Bounds: GridBounds
      Agents: AgentState[] }

[<RequireQualifiedAccess>]
module Agent =

    /// Creates an agent at rest (no destination) at the given position.
    let create (id: AgentId) (side: Side) (position: Cell) : AgentState =
        { Id = id; Side = side; Position = position; Destination = None }
