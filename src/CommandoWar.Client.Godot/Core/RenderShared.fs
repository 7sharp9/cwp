namespace CwClientCore

open CommandoWar.Sim

/// Terrain-`DrawItem` construction and the depth-sort key, shared by every
/// `IClientScene` that renders a `WorldState.Terrain` (TASK-040, extracted
/// from `DemoRenderScene` so `CommandDemoScene` does not duplicate it).
/// Framework-neutral: no `Godot.*` type, only `DrawItem` (primitives).
[<RequireQualifiedAccess>]
module RenderShared =

    /// One `DrawItem` per terrain cell. `TextureId` (0 = floor, 1 = block,
    /// 2 = crate) carries the passable/impassable/opaque distinction as
    /// texture shape, not colour alone (TASK-041); `R`/`G`/`B` still shade
    /// open ground by elevation (block/crate render at full, untinted
    /// colour -- their own Kenney art already reads clearly).
    let buildTerrainItems (t: Terrain) : DrawItem[] =
        [| for y in 0 .. t.Bounds.Height - 1 do
             for x in 0 .. t.Bounds.Width - 1 do
                 let c = { X = x; Y = y }
                 let textureId, r, g, b =
                     if not (Terrain.passable t c) then
                         1, 1.0f, 1.0f, 1.0f
                     elif Terrain.opaque t c then
                         2, 1.0f, 1.0f, 1.0f
                     else
                         let elevation = Terrain.elevation t c
                         let shade = 0.30f + 0.06f * float32 (min elevation 6)
                         0, shade, shade + 0.05f, shade - 0.05f

                 yield
                     { Kind = 0
                       TextureId = textureId
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

    /// Player-facing text for a `DecisionReason` (TASK-042, backlog B-028;
    /// docs/06 section 8 "concise explanations for refusal, delay,
    /// adaptation, and panic"). Structured values only, per docs/05 section 7
    /// -- no free prose the model does not carry. Deliberately a separate,
    /// public mapping from `DiagnosticRender`'s private developer-facing one
    /// (docs/06 section 11 draws that distinction explicitly): this one is
    /// shown to the player, not a developer overlay.
    let private reasonText (r: DecisionReason) : string =
        match r with
        | NoKnownRoute -> "no known route"
        | RouteTooExposed None -> "route too exposed"
        | RouteTooExposed(Some id) -> sprintf "route too exposed (threat: agent %d)" (AgentId.value id)
        | TargetNotKnown -> "target not known"

    /// Player-facing text for an agent's current order disposition (TASK-042,
    /// backlog B-028; docs/06 section 11 "order acknowledgement and
    /// disposition"). `None` covers both "no current order" and "not yet
    /// appraised this tick" -- the client has no way to distinguish those two
    /// from `AgentSnapshot` alone, and neither is worth surfacing to the
    /// player as a distinct state.
    let dispositionText (d: OrderDisposition option) : string =
        match d with
        | None -> "no order"
        | Some Accepted -> "accepted"
        | Some(Refused(primary, _)) -> sprintf "refused: %s" (reasonText primary)
        | Some(Unable(primary, _)) -> sprintf "unable: %s" (reasonText primary)
