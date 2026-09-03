namespace CommandoWar.Sim

/// Deterministic point-to-point line of sight over the authoritative terrain
/// grid (docs/04_SIMULATION_SPEC.md section 9 "Line of sight and cover";
/// docs/07_VERTICAL_SLICE.md section 5 "deterministic line of sight";
/// docs/03_ARCHITECTURE.md section 11).
///
/// `Sight.trace` decides whether one logical cell can see another and returns
/// the traced cell path plus the first blocking cell. It is a pure function of
/// `Terrain` (opacity and elevation) and two `Cell`s. Integer-only: an integer
/// supercover grid walk, no floating point, no `System.Math` on doubles.
///
/// NOTHING in `Simulation.step` or any tick phase calls this module yet. It is
/// authored and queryable but not consumed, exactly as `Terrain` (TASK-010)
/// and the `Objective` algebra (TASK-008) are. The first consumer is
/// Perception (phase 12.3, backlog B-015), which clears visibility each tick
/// and re-derives visible contacts by tracing sight between agents.
///
/// Because it is a leaf that nothing authoritative references, it moves no
/// pinned fixture hash and does not touch `Canonical.encode`.
///
/// ## Algorithm
///
/// The walk is the classic integer supercover line. With
/// `nx = |bx - ax|`, `ny = |by - ay|`, per-axis step signs `sx`, `sy`, and
/// per-axis progress counters `ix`, `iy`, each step chooses:
///
/// ```text
/// decision = (1 + 2*ix) * ny - (1 + 2*iy) * nx
///   decision < 0            -> step x   (ix + 1)
///   decision > 0            -> step y   (iy + 1)
///   decision = 0            -> step x and y together (a single diagonal step)
/// ```
///
/// This visits the geometrically-defined supercover set of the segment: every
/// cell whose interior the segment crosses, and at an exact lattice-corner
/// crossing a single diagonal step (never the two flanking cells).
///
/// ## Symmetry (a required property, docs/04 section 9)
///
/// `Sight.visible t a b = Sight.visible t b a` for every pair. The supercover
/// set of a segment is defined purely by the segment, so it is
/// direction-independent; walking from the b-end negates `decision` at each
/// corresponding step, which swaps the "step x" / "step y" branches
/// consistently and keeps `decision = 0` a diagonal step, so the reverse walk
/// is the forward path reversed. The blocking rule is then evaluated over
/// sets that are identical in both directions: the intermediate path cells,
/// and the unordered pair of shared-edge neighbours of each diagonal step.
/// `Blocker` is NOT required to be symmetric (it is the first blocker seen
/// from the origin end); `visible` is, and `SightTests` pins it with a
/// property test over a grid of endpoint pairs including out-of-bounds ones.
///
/// ## Blocking rules
///
///   * Endpoints never block.
///   * An intermediate cell blocks when it is `Terrain.opaque`, OR its
///     elevation is strictly greater than the elevation of BOTH endpoints
///     (a ridge occludes). Endpoint elevation differences do not otherwise
///     grant or deny sight at this stage; real height-field / eye-height
///     reasoning is deferred.
///   * A diagonal step is blocked only when BOTH shared-edge neighbours of
///     that step are `Terrain.opaque` (no sight through a solid inner corner;
///     sight passes a single wall cell at a diagonal corner).
///
/// ## Totality
///
/// Any input pair yields a defined result. An out-of-bounds endpoint sees
/// nothing (`Visible = false`, `Blocker = None`); the walk is not run in that
/// case and `Path` is just the two endpoints.

/// The result of tracing sight from an origin cell to a target cell.
type LineOfSight =
    { /// Whether the target is visible from the origin.
      Visible: bool
      /// The traced cells from origin to target in order. `Path.[0]` is the
      /// origin and `Path.[Path.Length - 1]` is the target. For an
      /// out-of-bounds endpoint this is just `[| origin; target |]`.
      Path: Cell[]
      /// The first cell that blocks sight when `Visible` is false, else
      /// `None`. For an opacity / elevation block this is an intermediate
      /// path cell; for a diagonal-corner block it is one of the two opaque
      /// shared-edge neighbours (the one on the origin side's row), which is
      /// not itself on `Path`.
      Blocker: Cell option }

[<RequireQualifiedAccess>]
module Sight =

    /// The integer supercover cell walk from `a` to `b`, inclusive of both.
    /// Pure integer arithmetic; `a = b` yields `[| a |]`.
    let private walk (a: Cell) (b: Cell) : Cell[] =
        let dx = b.X - a.X
        let dy = b.Y - a.Y
        let nx = abs dx
        let ny = abs dy
        let sx = sign dx
        let sy = sign dy

        let points = ResizeArray<Cell>(nx + ny + 1)
        let mutable x = a.X
        let mutable y = a.Y
        points.Add { X = x; Y = y }

        let mutable ix = 0
        let mutable iy = 0

        while ix < nx || iy < ny do
            let decision = (1 + 2 * ix) * ny - (1 + 2 * iy) * nx

            if ix < nx && iy < ny && decision = 0 then
                x <- x + sx
                y <- y + sy
                ix <- ix + 1
                iy <- iy + 1
            elif iy >= ny || (ix < nx && decision < 0) then
                x <- x + sx
                ix <- ix + 1
            else
                y <- y + sy
                iy <- iy + 1

            points.Add { X = x; Y = y }

        points.ToArray()

    /// Traces sight from `a` to `b` over `terrain`. Total, pure,
    /// deterministic, integer-only.
    let trace (terrain: Terrain) (a: Cell) (b: Cell) : LineOfSight =
        if not (GridBounds.contains a terrain.Bounds) || not (GridBounds.contains b terrain.Bounds) then
            { Visible = false; Path = [| a; b |]; Blocker = None }
        elif a = b then
            { Visible = true; Path = [| a |]; Blocker = None }
        else
            let path = walk a b
            let ea = Terrain.elevation terrain a
            let eb = Terrain.elevation terrain b

            let blocksCell (c: Cell) : bool =
                Terrain.opaque terrain c
                || (let e = Terrain.elevation terrain c in e > ea && e > eb)

            let mutable blocker = None
            let mutable i = 1

            while Option.isNone blocker && i < path.Length do
                let prev = path.[i - 1]
                let cur = path.[i]

                // Diagonal-corner rule for the step prev -> cur.
                if prev.X <> cur.X && prev.Y <> cur.Y then
                    let n1 = { X = cur.X; Y = prev.Y }
                    let n2 = { X = prev.X; Y = cur.Y }

                    if Terrain.opaque terrain n1 && Terrain.opaque terrain n2 then
                        blocker <- Some n1

                // `cur` is an intermediate cell unless it is the target.
                if Option.isNone blocker && i < path.Length - 1 && blocksCell cur then
                    blocker <- Some cur

                i <- i + 1

            match blocker with
            | Some c -> { Visible = false; Path = path; Blocker = Some c }
            | None -> { Visible = true; Path = path; Blocker = None }

    /// Whether `b` is visible from `a` over `terrain`. Defined in terms of
    /// `trace`; symmetric in `a` and `b` (see the module comment).
    let visible (terrain: Terrain) (a: Cell) (b: Cell) : bool = (trace terrain a b).Visible
