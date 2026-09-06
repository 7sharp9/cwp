namespace CommandoWar.Sim

/// The authoritative per-cell terrain grid (docs/04_SIMULATION_SPEC.md
/// sections 7 and 9, docs/03_ARCHITECTURE.md section 11,
/// docs/06_CONTENT_AND_PRESENTATION.md section 4).
///
/// Terrain carries elevation, traversability and movement cost, opacity
/// (high occlusion), and directional low cover. Integers only, no
/// floating point. Static terrain is authoritative but immutable within a
/// run at this stage: destruction and damage state are deferred to combat
/// (backlog B-019).
///
/// The Navigation and movement phase reads this module (via `Pathfinding`,
/// TASK-015) for passability and entry cost; line of sight (`Sight`,
/// TASK-012) and pathfinding (`Pathfinding`, TASK-013) query it directly.
/// Perception, appraisal, and combat do not consume it yet.
///
/// Because terrain carries no per-tick mutable state, it is deliberately
/// excluded from `Canonical.encode` and the state hash (see
/// `docs/04_SIMULATION_SPEC.md` section 17 and the ADR-0002 amendment
/// "Static authoritative data and the canonical image"). The canonical
/// format version bumps when destructible terrain lands.

/// A cardinal direction. Movement and the cover model are cardinal-only at
/// this stage: the Navigation and movement phase advances one cell per tick
/// along a 4-connected `Pathfinding` path (`Simulation.fs`, TASK-015), and
/// line of sight is a cardinal-plus-diagonal supercover walk (`Sight.fs`).
/// Extend this DU to eight only when a consumer needs diagonal movement.
type Direction =
    | North
    | East
    | South
    | West

[<RequireQualifiedAccess>]
module Direction =

    /// The four cardinals in the fixed order the dense cover store is indexed
    /// by. Iterating this is stable and never enumerates a hash map.
    let all: Direction[] = [| North; East; South; West |]

    /// The dense-store index for a direction: North 0, East 1, South 2,
    /// West 3.
    let index (d: Direction) : int =
        match d with
        | North -> 0
        | East -> 1
        | South -> 2
        | West -> 3

/// How a cell may be traversed on foot. The bridge mission needs only the
/// distinction "can an agent enter this cell"; vehicles and multiple
/// movement classes are out of the vertical slice (docs/07 exclusions).
type MovementClass =
    | Passable
    | Impassable

/// One fully validated terrain cell, as `Scenario.validate` hands it to
/// `Terrain.build`. This is not authored content (that is `RawTerrainLayer`
/// in `Scenario.fs`); it is the checked hand-off shape, so `build` does no
/// validation of its own.
type AuthoredCell =
    { Cell: Cell
      Movement: MovementClass
      Elevation: int
      MoveCost: int
      Opaque: bool }

/// One fully validated directional low-cover value: `Cell` gains cover of
/// `Level` against attacks arriving from `Direction`.
type AuthoredCover =
    { Cell: Cell
      Direction: Direction
      Level: int }

/// The authoritative terrain grid for one `GridBounds`.
///
/// Stored row-major in dense integer arrays indexed by
/// `y * Bounds.Width + x`, never a map keyed by `Cell`, so no query
/// enumerates a hash map (docs/09 section 3). `Cover` has one slot per cell
/// per cardinal direction: `cellIndex * 4 + Direction.index d`.
type Terrain =
    { Bounds: GridBounds
      /// Elevation level per cell. Flat terrain is all zero.
      Elevation: int[]
      /// Traversability class per cell.
      Movement: MovementClass[]
      /// Integer cost to enter each cell. Meaningful only where `Movement`
      /// is `Passable`, where `Scenario.validate` has confined it to
      /// `[BaseMoveCost, MaxMoveCost]`; `moveCost` reports `BlockedCost` for
      /// an `Impassable` cell regardless of this value.
      MoveCost: int[]
      /// High-occlusion flag per cell: blocks line of sight when set.
      Opaque: bool[]
      /// Directional low-cover level, `Bounds.Width * Bounds.Height * 4`
      /// entries, indexed `cellIndex * 4 + Direction.index d`.
      Cover: int[] }

[<RequireQualifiedAccess>]
module Terrain =

    /// The movement cost of an ordinary passable cell, and the least an
    /// authored passable cell may cost. Empty terrain uses this everywhere.
    [<Literal>]
    let BaseMoveCost = 1

    /// The greatest entry cost an authored passable cell may carry.
    /// `Scenario.validate` rejects a passable cell outside
    /// `[BaseMoveCost, MaxMoveCost]` (TASK-021). The ceiling exists for two
    /// independent reasons, not just to pair with the floor:
    ///
    ///   * it keeps a passable cell's `moveCost` strictly below the
    ///     `BlockedCost` sentinel, so "cannot enter" and "expensive to enter"
    ///     can never collide;
    ///   * it bounds `Pathfinding` cost accumulation. Every `g` there is at
    ///     most `Width * Height * MaxMoveCost` (the closed set holds each cell
    ///     once), and `1000 * Width * Height` stays inside
    ///     `System.Int32.MaxValue` for any grid up to ~1460 cells on a side,
    ///     far beyond a desktop tactical map. A single cell costing 1000x the
    ///     base step is already an extreme "deep obstacle" value.
    [<Literal>]
    let MaxMoveCost = 1000

    /// The cost `moveCost` reports for a cell that cannot be entered: out of
    /// bounds, or `Impassable`. A large sentinel, not an arithmetic
    /// infinity; a future pathfinder treats it as "no edge". Strictly above
    /// `MaxMoveCost`, so it is never a valid passable-cell cost.
    let BlockedCost = System.Int32.MaxValue

    let private area (b: GridBounds) : int = b.Width * b.Height

    /// Row-major index of an in-bounds cell. Callers guard with
    /// `GridBounds.contains` first.
    let private indexOf (t: Terrain) (c: Cell) : int = c.Y * t.Bounds.Width + c.X

    /// Flat, fully passable, transparent terrain with no cover for `bounds`.
    /// Every `World` starts on this; an authored scenario replaces it
    /// (`World.ofScenario`). `bounds` must be non-empty (width and height
    /// both positive); `World.create` and `Scenario.validate` own that
    /// guard.
    let empty (bounds: GridBounds) : Terrain =
        let n = area bounds

        { Bounds = bounds
          Elevation = Array.zeroCreate n
          Movement = Array.create n Passable
          MoveCost = Array.create n BaseMoveCost
          Opaque = Array.zeroCreate n
          Cover = Array.zeroCreate (n * 4) }

    /// Builds terrain from validated authored data. Every argument is
    /// already range-checked by `Scenario.validate`; this function performs
    /// no validation. It uses a contained mutable builder (ADR-0002
    /// "Mutation policy"): the arrays are freshly allocated by `empty` and
    /// do not escape until the finished record is returned. Later authored
    /// cells and cover entries overwrite earlier ones for the same slot;
    /// `Scenario.validate` rejects duplicates so that never happens in
    /// practice.
    let build (bounds: GridBounds) (cells: AuthoredCell[]) (cover: AuthoredCover[]) : Terrain =
        let t = empty bounds

        for cell in cells do
            let i = indexOf t cell.Cell
            t.Elevation.[i] <- cell.Elevation
            t.Movement.[i] <- cell.Movement
            t.MoveCost.[i] <- cell.MoveCost
            t.Opaque.[i] <- cell.Opaque

        for c in cover do
            t.Cover.[indexOf t c.Cell * 4 + Direction.index c.Direction] <- c.Level

        t

    // --- queries ---------------------------------------------------------
    // Each query is total and bounds-checked: an out-of-bounds cell yields a
    // defined result, never an exception or an out-of-range array read.
    // Nothing in `Simulation.step` or any phase calls these yet (the
    // TASK-008 `Objective`-algebra precedent).

    /// Elevation level of a cell. Out of bounds: `0` (the flat default).
    let elevation (t: Terrain) (c: Cell) : int =
        if GridBounds.contains c t.Bounds then
            t.Elevation.[indexOf t c]
        else
            0

    /// Whether an agent may enter a cell. Out of bounds: `false`.
    let passable (t: Terrain) (c: Cell) : bool =
        GridBounds.contains c t.Bounds
        && (match t.Movement.[indexOf t c] with
            | Passable -> true
            | Impassable -> false)

    /// Integer cost to enter a cell. `BlockedCost` for an out-of-bounds or
    /// `Impassable` cell; the authored cost otherwise, which
    /// `Scenario.validate` has kept within `[BaseMoveCost, MaxMoveCost]` for
    /// a passable cell, so this never returns `BlockedCost` for a passable
    /// one.
    let moveCost (t: Terrain) (c: Cell) : int =
        if passable t c then t.MoveCost.[indexOf t c] else BlockedCost

    /// Whether a cell blocks line of sight (high occlusion). Out of bounds:
    /// `false`.
    let opaque (t: Terrain) (c: Cell) : bool =
        GridBounds.contains c t.Bounds && t.Opaque.[indexOf t c]

    /// Directional low-cover level for a cell against an attack arriving
    /// from `d`. Out of bounds, or no authored cover: `0`.
    let cover (t: Terrain) (c: Cell) (d: Direction) : int =
        if GridBounds.contains c t.Bounds then
            t.Cover.[indexOf t c * 4 + Direction.index d]
        else
            0
