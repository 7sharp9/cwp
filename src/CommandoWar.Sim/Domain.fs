namespace CommandoWar.Sim

/// Which force an agent belongs to. Squads, weapons, wounds, morale, trust
/// and the other agent dimensions in docs/04_SIMULATION_SPEC.md section 11
/// are added by later tasks.
type Side =
    | Friendly
    | Hostile

/// The cardinal path an agent is following toward its destination, with the
/// index in it of the agent's current cell.
///
/// This is a **non-canonical derived cache** (TASK-015): at every tick it is a
/// pure deterministic function of `(AgentState.Position, AgentState.Destination,
/// WorldState.Terrain)`. `Terrain` is immutable within a run (ADR-0002
/// amendment); `Position` and `Destination` are already in the canonical
/// image; and `Pathfinding.findWithin` is total, pure, integer-only, and
/// deterministic (TASK-013). Two runs of the same inputs therefore produce
/// identical caches, so it cannot diverge and is **excluded** from
/// `Canonical.encode` (`docs/04_SIMULATION_SPEC.md` section 17, "derived caches
/// either excluded or normalised"). `Canonical.FormatVersion` stays 1.
///
/// `Cells.[0]` is the origin the path was planned from, `Cells.[Cells.Length -
/// 1]` is the destination, `Cells.[Cursor]` is the agent's current cell, and
/// `Cost` is the `Pathfinding` integer cost of the whole path. Only ever `Some`
/// while the agent is mid-route; cleared on arrival or when blocked.
type MovementPath =
    { Cells: Cell[]
      Cursor: int
      Cost: int }

/// Minimal authoritative agent state for the simulation skeleton: identity,
/// side, logical position, an optional movement destination, and the
/// (non-canonical, derived) path the agent is following toward it. Movement
/// progress within an edge, facing and stance are deferred until a real
/// movement model exists.
type AgentState =
    { Id: AgentId
      Side: Side
      Position: Cell
      Destination: Cell option
      /// The path the agent is currently following (TASK-015). A derived
      /// cache: recomputed deterministically from `(Position, Destination,
      /// Terrain)` by the Navigation and movement phase, and excluded from
      /// `Canonical.encode`. `None` when the agent has no destination.
      Route: MovementPath option }

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

    /// Creates an agent at rest (no destination, no route) at the given
    /// position.
    let create (id: AgentId) (side: Side) (position: Cell) : AgentState =
        { Id = id
          Side = side
          Position = position
          Destination = None
          Route = None }
