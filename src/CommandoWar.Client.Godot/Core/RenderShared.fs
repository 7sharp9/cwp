namespace CwClientCore

open CommandoWar.Sim

/// Terrain-`DrawItem` construction and the depth-sort key, shared by every
/// `IClientScene` that renders a `WorldState.Terrain` (TASK-040, extracted
/// from `DemoRenderScene` so `CommandDemoScene` does not duplicate it).
/// Framework-neutral: no `Godot.*` type, only `DrawItem` (primitives).
[<RequireQualifiedAccess>]
module RenderShared =

    /// One `DrawItem` per terrain cell: elevation lightens open ground,
    /// impassable cells are dark, opaque-but-passable cells (walls) get a
    /// warm tint distinct from the ridge.
    let buildTerrainItems (t: Terrain) : DrawItem[] =
        [| for y in 0 .. t.Bounds.Height - 1 do
             for x in 0 .. t.Bounds.Width - 1 do
                 let c = { X = x; Y = y }
                 let r, g, b =
                     if not (Terrain.passable t c) then
                         0.20f, 0.22f, 0.27f
                     elif Terrain.opaque t c then
                         0.55f, 0.38f, 0.20f
                     else
                         let elevation = Terrain.elevation t c
                         let shade = 0.30f + 0.06f * float32 (min elevation 6)
                         shade, shade + 0.05f, shade - 0.05f

                 yield
                     { Kind = 0
                       Cx = float32 x
                       Cy = float32 y
                       R = r
                       G = g
                       B = b
                       A = 1.0f
                       Radius = 0.0f } |]

    let agentColor (side: Side) : float32 * float32 * float32 =
        match side with
        | Friendly -> 0.35f, 0.75f, 1.0f
        | Hostile -> 1.0f, 0.40f, 0.35f

    /// Ascending screen depth: `(Cx + Cy)`, terrain (`Kind = 0`) drawn
    /// immediately before an agent (`Kind = 1`) occupying the same cell, so
    /// terrain and agents interleave correctly instead of "all terrain then
    /// all agents".
    let depthKey (item: DrawItem) : float32 = (item.Cx + item.Cy) * 2.0f + float32 item.Kind
