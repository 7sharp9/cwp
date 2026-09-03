namespace CommandoWar.Sim

/// A framework-neutral per-tick diagnostic model of authoritative spatial and
/// tactical state (docs/06_CONTENT_AND_PRESENTATION.md section 11
/// "Developer-facing"; docs/07_VERTICAL_SLICE.md section 6 "developer overlay",
/// functional acceptance criterion 11; docs/03_ARCHITECTURE.md section 15
/// "optional development overlays"; docs/09_TEST_STRATEGY.md section 8).
///
/// This module is an OBSERVER, exactly as `Canonical.fs` and `Divergence.fs`
/// are: NOTHING in `Simulation.step` or any tick phase constructs a
/// `DiagnosticFrame`, and no frame ever feeds back into the step. It exists
/// for tests, replay inspection, and the headless / developer renderers
/// (`CommandoWar.Headless.DiagnosticRender`, and later the Godot developer
/// overlay, backlog B-029).
///
/// It is framework-neutral in the ADR-0002 sense: ints, bools, arrays,
/// strings, and discriminated unions only. There is NO floating point
/// anywhere in the model (SVG coordinates in the renderers are integer pixels
/// at an integer px-per-cell scale). `Diagnostics.frame` and
/// `Diagnostics.frameOf` are total, pure, and deterministic: they do not
/// mutate, do not read `Random`, do not touch wall-clock time, and are not
/// part of the phase pipeline. Building a frame must not move any pinned
/// fixture hash and must not change `Canonical.encode`; this module is a leaf
/// that nothing authoritative references.

/// The canonical layer names carried by `Diagnostics.frame`. Renderers key on
/// these strings; `--layer` on the `cwheadless render` verb filters
/// `DiagnosticFrame.Layers` by one of them.
[<RequireQualifiedAccess>]
module LayerName =

    /// Elevation level per cell (`Terrain.Elevation`). Flat terrain is all 0.
    [<Literal>]
    let Elevation = "elevation"

    /// 1 where an agent may enter the cell, 0 where it may not
    /// (`Terrain.MovementClass`).
    [<Literal>]
    let Passability = "passability"

    /// Authored integer entry cost per cell (`Terrain.MoveCost`). Meaningful
    /// only where `passability` is 1; an impassable cell keeps its authored
    /// value here even though `Terrain.moveCost` reports `Terrain.BlockedCost`.
    [<Literal>]
    let MovementCost = "movement-cost"

    /// 1 where the cell blocks line of sight (high occlusion), 0 otherwise
    /// (`Terrain.Opaque`).
    [<Literal>]
    let Opacity = "opacity"

/// One dense row-major integer grid for a `GridBounds`. `Cells` has
/// `Bounds.Width * Bounds.Height` entries, indexed `y * Bounds.Width + x`.
/// The meaning of a cell value is fixed by `Name` (see `LayerName`).
type GridLayer =
    { Name: string
      Bounds: GridBounds
      Cells: int[] }

/// One directional low-cover marker: `Cell` has cover of `Value` against an
/// attack arriving from `Direction`. Emitted only for cells with a non-zero
/// authored cover level.
type EdgeMarker =
    { Cell: Cell
      Direction: Direction
      Value: int }

/// One agent as the frame sees it: identity, side, logical cell, and the
/// movement destination if it has one.
type AgentMarker =
    { Id: AgentId
      Side: Side
      Cell: Cell
      Destination: Cell option }

/// A small framework-neutral summary of one `DomainEvent` emitted this tick:
/// a kind label and the cells the event concerns. This lets a frame show
/// what happened, not only the end state. `Diagnostics.frame` carries none
/// (it has no `StepResult`); `Diagnostics.frameOf` carries one per event.
type EventMarker =
    { Kind: string
      Cells: Cell[] }

/// Open extension point for overlays that later tactical systems attach.
/// Every renderer draws an overlay through the cells it names, so a new
/// system adds a case here and a branch in each renderer:
///
///   * B-009 line of sight  -> `SightRay` (realised by TASK-012);
///   * B-010 pathfinding     -> a planned-path case (agent, ordered cells);
///   * B-011 reservation     -> a reserved-cell case (cell, agent, until tick);
///   * B-019 combat          -> a fire-line case (shooter, target).
///
/// B-010 / B-011 / B-019 do not exist yet: no such case is defined.
/// `Diagnostics.frame` and `Diagnostics.frameOf` never produce an overlay of
/// any kind; `DiagnosticFrame.Overlays` is populated only by a caller (a test,
/// or `cwheadless render --los`). `Cells` is the generic non-speculative
/// shape: a labelled set of cells a renderer can always fall back to.
type Overlay =
    /// A labelled set of cells.
    | Cells of label: string * cells: Cell[]
    /// A traced line of sight: origin, target, the traced cell path
    /// (`Sight.trace`'s `Path`), and the first blocking cell when the target
    /// is not visible. Supplied by a caller; `Diagnostics` never emits one.
    | SightRay of from: Cell * target: Cell * cells: Cell[] * blocked: Cell option

/// A framework-neutral snapshot of authoritative spatial and tactical state
/// for one tick, plus the determinism trio (tick, state hash, random draw
/// count). Values only, never references to authoritative storage.
type DiagnosticFrame =
    { Tick: int64
      Bounds: GridBounds
      Layers: GridLayer[]
      Edges: EdgeMarker[]
      Agents: AgentMarker[]
      Events: EventMarker[]
      Overlays: Overlay[]
      Hash: StateHash
      RandomDraws: uint64 }

[<RequireQualifiedAccess>]
module Diagnostics =

    /// The four terrain layers, in a fixed order. Each is a fresh array; the
    /// authoritative terrain store is never aliased into the frame.
    let private terrainLayers (world: WorldState) : GridLayer[] =
        let t = world.Terrain
        let b = world.Bounds

        [| { Name = LayerName.Elevation
             Bounds = b
             Cells = Array.copy t.Elevation }
           { Name = LayerName.Passability
             Bounds = b
             Cells =
               t.Movement
               |> Array.map (function
                   | Passable -> 1
                   | Impassable -> 0) }
           { Name = LayerName.MovementCost
             Bounds = b
             Cells = Array.copy t.MoveCost }
           { Name = LayerName.Opacity
             Bounds = b
             Cells = t.Opaque |> Array.map (fun o -> if o then 1 else 0) } |]

    /// Directional cover markers in a stable order: cells row-major, then the
    /// four cardinals in `Direction.all` order. Only non-zero levels appear.
    let private coverEdges (world: WorldState) : EdgeMarker[] =
        let t = world.Terrain
        let b = world.Bounds

        [| for y in 0 .. b.Height - 1 do
               for x in 0 .. b.Width - 1 do
                   let c = { X = x; Y = y }

                   for d in Direction.all do
                       let v = Terrain.cover t c d

                       if v > 0 then
                           { Cell = c; Direction = d; Value = v } |]

    let private agentMarkers (world: WorldState) : AgentMarker[] =
        world.Agents
        |> Array.sortBy (fun a -> a.Id)
        |> Array.map (fun a ->
            { Id = a.Id
              Side = a.Side
              Cell = a.Position
              Destination = a.Destination })

    let private eventMarker (e: DomainEvent) : EventMarker =
        match e.Body with
        | CommandAccepted(_, _, dest) -> { Kind = "command-accepted"; Cells = [| dest |] }
        | CommandRejected(_, UnknownAgent _) -> { Kind = "command-rejected"; Cells = [||] }
        | CommandRejected(_, TargetOutOfBounds target) -> { Kind = "command-rejected"; Cells = [| target |] }
        | MovementStepped(_, from, into) -> { Kind = "movement-stepped"; Cells = [| from; into |] }
        | MovementCompleted(_, at) -> { Kind = "movement-completed"; Cells = [| at |] }

    /// The diagnostic frame for a world state. Total, pure, deterministic:
    /// no mutation, no random draw, no wall-clock read. `Events` is empty
    /// (a bare `WorldState` carries no per-tick event history); use
    /// `frameOf` for the this-tick event markers.
    let frame (world: WorldState) : DiagnosticFrame =
        { Tick = world.Tick
          Bounds = world.Bounds
          Layers = terrainLayers world
          Edges = coverEdges world
          Agents = agentMarkers world
          Events = [||]
          Overlays = [||]
          Hash = Hashing.hash world
          RandomDraws = world.Random.Draws }

    /// The diagnostic frame for a completed step: the frame of the resulting
    /// world, plus this tick's event markers and the post-step canonical
    /// hash recorded on the `StepResult`. Total, pure, deterministic.
    let frameOf (result: StepResult) : DiagnosticFrame =
        { frame result.State with
            Events = result.Events |> Array.map eventMarker
            Hash = result.StateHash }
