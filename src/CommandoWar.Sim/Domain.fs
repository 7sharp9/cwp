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
/// bounds, the authoritative terrain grid, the agents ordered by ascending
/// id, and the deterministic random stream. Fields are added only when an
/// implemented behaviour requires them (docs/04_SIMULATION_SPEC.md section
/// 10).
type WorldState =
    { Tick: int64
      Bounds: GridBounds
      /// The authoritative per-cell terrain grid (elevation, passability,
      /// movement cost, opacity, directional cover). Authored and queryable
      /// but not yet consumed: no tick phase reads it (backlog B-009 to
      /// B-011). It is immutable within a run at this stage and is
      /// deliberately excluded from `Canonical.encode` and the state hash
      /// (docs/04_SIMULATION_SPEC.md section 17; ADR-0002 amendment).
      Terrain: Terrain
      Agents: AgentState[]
      /// The authoritative deterministic random stream. It is threaded through
      /// every step and is part of the canonical state hash. No gameplay phase
      /// draws from it yet (TASK-003 wires the stream; gameplay draws arrive
      /// with later combat and appraisal tasks).
      Random: RandomState }

[<RequireQualifiedAccess>]
module Agent =

    /// Creates an agent at rest (no destination) at the given position.
    let create (id: AgentId) (side: Side) (position: Cell) : AgentState =
        { Id = id; Side = side; Position = position; Destination = None }
