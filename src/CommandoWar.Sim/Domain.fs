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

/// One contact in the squad's shared tactical picture (TASK-026,
/// `docs/05_COMMAND_AND_AGENT_AI.md` section 3 "Squad tactical picture",
/// `docs/04_SIMULATION_SPEC.md` section 12.4). The Tactical-knowledge phase
/// merges every friendly's observations this tick into one array of these,
/// sorted ascending by `Contact` id (stable iteration, `docs/04` section 6).
///
/// This store **has memory**: `LastSeenTick` and the decaying `Confidence`
/// cannot be recomputed from the current tick's positions (a contact stays in
/// the picture, with an ageing confidence, for `PerceptionConfig.ExpireAfter`
/// ticks after it was last seen). It is therefore **genuine per-tick canonical
/// state**, not a derived cache — unlike `AgentState.VisibleContacts` — so it
/// enters `Canonical.encode` and `Canonical.FormatVersion` is `3` (the
/// ADR-0002 amendment "Static authoritative data and the canonical image").
type Contact =
    { /// The observed agent's id. Identity is always known at this stage
      /// (`docs/05` section 3 "contact ID when identity is known"); a
      /// suspected-only contact without an id is deferred.
      Contact: AgentId
      /// The cell the contact was last observed in.
      LastKnownCell: Cell
      /// The tick a friendly last saw this contact. Non-decreasing until the
      /// contact expires and is removed.
      LastSeenTick: int64
      /// Confidence on the `docs/04` section 4 `0..1000` scale:
      /// `PerceptionConfig.ConfidenceFull` while the contact is currently
      /// seen or was seen within `PerceptionConfig.StaleAfter` ticks, dropping
      /// one band (`PerceptionConfig.ConfidenceBandDrop`) once it goes stale.
      Confidence: int }

/// Minimal authoritative agent state for the simulation skeleton: identity,
/// side, logical position, movement progress within the current edge, an
/// optional movement destination, the (non-canonical, derived) path the
/// agent is following toward it, and the (non-canonical, derived) set of
/// opposing agents it can currently see. Facing and stance are deferred until
/// a real movement model exists.
type AgentState =
    { Id: AgentId
      Side: Side
      Position: Cell
      /// Integer progress toward entering the next cell along `Route`
      /// (TASK-018, docs/04 section 8: "advance movement progress by an
      /// integer amount each tick; enter the next cell when progress reaches
      /// the threshold"). The threshold is `Terrain.moveCost` of the next
      /// cell (the same value `Pathfinding` already uses as its edge weight);
      /// the per-tick increment is `Terrain.BaseMoveCost`. Always 0 at rest,
      /// on arrival, when blocked, and immediately after entering a cell —
      /// it measures progress along the *current* edge only, never carried
      /// past it. Unlike `Route`, this cannot be recomputed from `Position`
      /// alone (`Position` does not change while an edge is in progress, so
      /// nothing else records how many ticks have been spent on it): it is
      /// genuine new per-tick canonical state, part of `Canonical.encode`
      /// (`Canonical.FormatVersion` 2).
      Progress: int
      Destination: Cell option
      /// The path the agent is currently following (TASK-015). A derived
      /// cache: recomputed deterministically from `(Position, Destination,
      /// Terrain)` by the Navigation and movement phase, and excluded from
      /// `Canonical.encode`. `None` when the agent has no destination.
      Route: MovementPath option
      /// The opposing-side agents this agent can currently see, ascending by
      /// id (TASK-026, `docs/04` section 12.3 "current visible contacts").
      /// Rewritten from scratch every tick by the Perception phase.
      ///
      /// A **non-canonical derived cache**, on the identical argument that
      /// keeps `Route` out of `Canonical.encode`: it is a pure deterministic
      /// function of every agent's `Position`, the immutable `Terrain`, and
      /// the `PerceptionConfig` constants (`Sight.visible` is total, pure,
      /// integer-only). Two runs of the same inputs produce identical
      /// `VisibleContacts`, so it cannot diverge and is **excluded** from
      /// `Canonical.encode` (`docs/04` section 17). Empty at rest and for an
      /// agent with no opposing agent in sight range and line of sight.
      VisibleContacts: AgentId[] }

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
      /// The friendly squad's shared tactical picture (TASK-026, backlog
      /// B-015; `docs/04` section 10, 12.4). Ascending by contact id. The
      /// Tactical-knowledge phase upserts every contact a friendly saw this
      /// tick, ages contacts unseen for `PerceptionConfig.StaleAfter` ticks,
      /// and removes contacts unseen for `PerceptionConfig.ExpireAfter` ticks.
      ///
      /// Unlike `Terrain` and `AgentState.Route` / `VisibleContacts`, this
      /// **is** in `Canonical.encode`: it carries per-tick memory
      /// (`LastSeenTick`, the decaying `Confidence`) that no other field can
      /// reproduce (see `Contact`). Adding it bumped `Canonical.FormatVersion`
      /// 2 -> 3. A hostile squad picture and enemy doctrine reacting to it are
      /// backlog B-022; per-agent private beliefs are `docs/05` section 17.
      TacticalKnowledge: Contact[]
      /// The authoritative deterministic random stream. It is threaded through
      /// every step and is part of the canonical state hash. No gameplay phase
      /// draws from it yet (TASK-003 wires the stream; gameplay draws arrive
      /// with later combat and appraisal tasks).
      Random: RandomState }

[<RequireQualifiedAccess>]
module Agent =

    /// Creates an agent at rest (no destination, no route, no progress, no
    /// visible contacts) at the given position.
    let create (id: AgentId) (side: Side) (position: Cell) : AgentState =
        { Id = id
          Side = side
          Position = position
          Progress = 0
          Destination = None
          Route = None
          VisibleContacts = [||] }
