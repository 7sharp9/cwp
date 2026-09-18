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
                       Cx2 = 0.0f
                       Cy2 = 0.0f
                       Text = ""
                       R = r
                       G = g
                       B = b
                       A = 1.0f
                       Radius = 0.0f } |]

    /// A translucent `Kind = 1` circle marker at a cell centre -- the
    /// selection-halo/route-dot shape (TASK-040/041), reused for TASK-043's
    /// developer-overlay cell highlights (reserved/obstructed/known-contact/
    /// exposed-route cells) so no new draw primitive is needed for a
    /// point-shaped marker.
    let cellMarker (cell: Cell) (r: float32, g: float32, b: float32) (a: float32) (radius: float32) : DrawItem =
        { Kind = 1
          TextureId = 0
          Cx = float32 cell.X
          Cy = float32 cell.Y
          Cx2 = 0.0f
          Cy2 = 0.0f
          Text = ""
          R = r
          G = g
          B = b
          A = a
          Radius = radius }

    /// A `Kind = 2` line segment between two cells (TASK-043: line-of-sight
    /// rays, fire lines).
    let lineMarker (from: Cell) (target: Cell) (r: float32, g: float32, b: float32) (a: float32) (width: float32) : DrawItem =
        { Kind = 2
          TextureId = 0
          Cx = float32 from.X
          Cy = float32 from.Y
          Cx2 = float32 target.X
          Cy2 = float32 target.Y
          Text = ""
          R = r
          G = g
          B = b
          A = a
          Radius = width }

    /// A `Kind = 4` one-shot effect sprite centred at a cell (TASK-046,
    /// backlog B-057: a muzzle flash or bullet-impact effect), keyed by
    /// `textureId` -- the `buildTerrainItems`/`DrawItem.TextureId` precedent.
    /// `Radius` is on-screen sprite size; there is no click interaction for
    /// these, unlike an agent's `Radius` (its hit-test radius).
    let effectSprite
        (cell: Cell)
        (textureId: int)
        (r: float32, g: float32, b: float32)
        (a: float32)
        (radius: float32)
        : DrawItem =
        { Kind = 4
          TextureId = textureId
          Cx = float32 cell.X
          Cy = float32 cell.Y
          Cx2 = 0.0f
          Cy2 = 0.0f
          Text = ""
          R = r
          G = g
          B = b
          A = a
          Radius = radius }

    /// A `Kind = 3` text label at a cell (TASK-043: grid coordinates).
    let cellLabel (cell: Cell) (text: string) (r: float32, g: float32, b: float32) (a: float32) (fontSize: float32) : DrawItem =
        { Kind = 3
          TextureId = 0
          Cx = float32 cell.X
          Cy = float32 cell.Y
          Cx2 = 0.0f
          Cy2 = 0.0f
          Text = text
          R = r
          G = g
          B = b
          A = a
          Radius = fontSize }

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
        | CriticallyWounded -> "critically wounded"

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

    /// Developer-facing text for a `DecisionReason` (TASK-043, backlog
    /// B-029, docs/06 section 11 "last appraisal factors and selected
    /// reason"). Deliberately mirrors `DiagnosticRender.reasonText`'s exact
    /// hyphenated wording (a separate `private` function there, not directly
    /// reusable across the assembly boundary as written) rather than
    /// inventing a second developer vocabulary -- kept visibly distinct from
    /// `reasonText` above's player-facing punctuation.
    let private devReasonText (r: DecisionReason) : string =
        match r with
        | NoKnownRoute -> "no-known-route"
        | RouteTooExposed None -> "route-too-exposed"
        | RouteTooExposed(Some id) -> sprintf "route-too-exposed threat-agent-%d" (AgentId.value id)
        | TargetNotKnown -> "target-not-known"
        | CriticallyWounded -> "critically-wounded"

    /// Developer-facing text for a `Commitment` (TASK-043; `DiagnosticRender.
    /// commitmentText`'s wording).
    let private devCommitmentText (c: Commitment) : string =
        match c with
        | Holding -> "holding"
        | Moving mc -> sprintf "moving-to-(%d,%d)" mc.Target.X mc.Target.Y
        | Suppressing sc -> sprintf "suppressing-agent-%d" (AgentId.value sc.Target)

    /// The developer-overlay HUD line for one agent (TASK-043, backlog
    /// B-029): commitment, suppression, stress, and the appraisal reason plus
    /// exposed-cell count, read from the same `Diagnostics.Overlay[]` every
    /// other developer renderer (`DiagnosticRender.Ascii`/`.Svg`/`.Html`)
    /// consumes -- no bespoke per-agent state is added to the client. Every
    /// overlay case below is sparse or agent-scoped (`AgentCommitment` is the
    /// only one guaranteed present for every agent -- `AgentSuppression`/
    /// `AgentStress`/`OrderAppraisal` are absent when zero / not appraised),
    /// so each falls back to its own zero/absent value rather than the whole
    /// line disappearing.
    let devAgentText (overlays: Overlay[]) (agent: AgentId) : string =
        let commitment =
            overlays
            |> Array.tryPick (function
                | AgentCommitment(a, _, c) when a = agent -> Some(devCommitmentText c)
                | _ -> None)
            |> Option.defaultValue "holding"

        let suppression =
            overlays
            |> Array.tryPick (function
                | AgentSuppression(a, _, s) when a = agent -> Some s
                | _ -> None)
            |> Option.defaultValue 0

        let stress =
            overlays
            |> Array.tryPick (function
                | AgentStress(a, _, s) when a = agent -> Some s
                | _ -> None)
            |> Option.defaultValue 0

        let reason, exposedCount =
            overlays
            |> Array.tryPick (function
                | OrderAppraisal(a, _, d, exposed) when a = agent ->
                    let text =
                        match d with
                        | Accepted -> "accepted"
                        | Refused(primary, _) -> sprintf "refused %s" (devReasonText primary)
                        | Unable(primary, _) -> sprintf "unable %s" (devReasonText primary)

                    Some(text, exposed.Length)
                | _ -> None)
            |> Option.defaultValue ("no-order", 0)

        sprintf
            "commitment=%s suppression=%d stress=%d reason=%s exposed=%dcells"
            commitment
            suppression
            stress
            reason
            exposedCount

    /// The developer-overlay legend (TASK-043 review round 2, Dave's live
    /// feedback: the overlay has no legend, and green/red are each reused
    /// for two different meanings -- LOS-visible vs FireLine-hit both green,
    /// Obstructed vs LOS-blocked both red). Static text, grouped by draw
    /// shape (cell marker vs line) since that is what actually disambiguates
    /// the reused hues on screen.
    let devLegendText: string =
        "[legend] cells: cyan=reserved red=obstructed yellow=known-contact orange=exposed-route | lines: green=visible/hit red=blocked grey=miss"
