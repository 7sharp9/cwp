namespace CommandoWar.Sim

/// A logical grid coordinate. The authoritative world is a logical grid;
/// isometric or screen coordinates exist only in clients
/// (docs/03_ARCHITECTURE.md section 11). Elevation and per-cell terrain
/// data (movement cost, opacity, cover) are deferred to later terrain tasks.
[<Struct>]
type Cell = { X: int; Y: int }

/// Rectangular logical bounds. The valid cells are those with
/// 0 &lt;= X &lt; Width and 0 &lt;= Y &lt; Height.
[<Struct>]
type GridBounds = { Width: int; Height: int }

[<RequireQualifiedAccess>]
module GridBounds =

    /// True when the cell lies inside the bounds.
    let contains (cell: Cell) (bounds: GridBounds) : bool =
        cell.X >= 0 && cell.Y >= 0 && cell.X < bounds.Width && cell.Y < bounds.Height
