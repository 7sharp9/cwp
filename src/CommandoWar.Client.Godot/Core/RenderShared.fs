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

    /// A `Kind = 5` hollow (unfilled) ring at a cell centre -- a hostile's
    /// last-known position once contact is lost (TASK-051, backlog B-055): a
    /// distinct outline shape, not a translucent fill, so it reads as stale
    /// intel rather than a dim real agent (docs/06 "status indicators that
    /// do not rely on colour alone" -- the same reasoning `CommandDemoScene`
    /// already applies to the `Dead`/`Incapacitated` vitals markers).
    let cellRing (cell: Cell) (r: float32, g: float32, b: float32) (a: float32) (radius: float32) : DrawItem =
        { Kind = 5
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

    /// The fixed width:height ratio of this game's isometric tile diamond
    /// (`FSharpSceneHost.cs`'s `TileW`/`TileH`, `88:44` -- a `2:1` isometric
    /// proportion that has held across every scale change so far, TASK-052's
    /// doubling included). Not literal screen pixels (`Origin`/`TileW`/
    /// `TileH` themselves stay a C# concern, ADR-0004), just the shape of
    /// the projection -- needed by `facingBin` below to translate a
    /// world-grid heading into the screen-relative compass bearing the
    /// Kenney rig's own rotation set is authored against.
    [<Literal>]
    let private IsoAspect = 0.5 // TileH / TileW

    /// The eight world-grid unit directions in Kenney `Human_N` clockwise
    /// order (`0=N,1=NE,2=E,3=SE,4=S,5=SW,6=W,7=NW`). Not a guess: the
    /// pack's own `Information.png` labels the FOUR EDGES of one terrain
    /// tile diamond (not its vertices) `N` (upper-right edge), `E`
    /// (lower-right edge), `S` (lower-left edge), `W` (upper-left edge), and
    /// the displacement from a tile's centre to its `N`-neighbour's centre
    /// is exactly twice the centre-to-edge-midpoint vector -- i.e. a
    /// `(half-width, -half-height)` screen step, which is exactly what a
    /// **world pure-axis** move (`dx=0,dy=-1`) produces under this
    /// project's own `CellToScreen`, not a world-diagonal move. TASK-056
    /// (a second live-review correction of TASK-054's B-052): the previous
    /// `facingBin` had this backwards, assigning the four Kenney cardinal
    /// poses to the four world-DIAGONAL moves instead -- confirmed directly
    /// from its own prior doc comment (`world NW -> bin 0 (N)`, etc.), a
    /// mistake that read as "close but off" rather than random because
    /// every one of the 8 primary directions happened to land exactly one
    /// 45-degree bin past its correct target under the old uniform-sector
    /// rounding.
    let private worldUnitDirections: (int * int)[] =
        [| (0, -1) // 0 = N
           (1, -1) // 1 = NE
           (1, 0) // 2 = E
           (1, 1) // 3 = SE
           (0, 1) // 4 = S
           (-1, 1) // 5 = SW
           (-1, 0) // 6 = W
           (-1, -1) |] // 7 = NW

    /// Projects a world-grid delta through the same isometric skew
    /// `FSharpSceneHost.cs`'s `CellToScreen` applies -- a raw (not unit)
    /// screen-space direction vector.
    let private toScreenVector (dx: float, dy: float) : float * float = dx - dy, (dx + dy) * IsoAspect

    /// The agent-facing bin (TASK-054, backlog B-052; corrected TASK-056) an
    /// agent's figure should render at: one of 8 pre-rendered `45°`
    /// rotations of the Kenney "Isometric Miniature" **idle** pose (`art/
    /// agent_human_facingN.png`, `N = 0..7`, cropped from `Characters/Human/
    /// Human_N_Idle0.png`).
    ///
    /// Pure and framework-neutral: derived from already-existing
    /// `AgentSnapshot` fields, never touches
    /// `CommandoWar.Sim`/`CommandoWar.Headless`. When the agent has an
    /// active `Destination` different from its current `Position`, projects
    /// the world-grid heading through `toScreenVector`, then picks whichever
    /// of the 8 `worldUnitDirections` -- projected through the identical
    /// skew -- is closest by cosine similarity: this is correct by
    /// construction for the 8 primary directions themselves (each is
    /// trivially its own best match) and, unlike a fixed-angle-sector
    /// round, also handles an arbitrary heading correctly (`Destination` is
    /// the agent's overall target cell, not its next path step, so the
    /// delta fed in here is frequently not a primary direction at all --
    /// e.g. partway along a `MoveTo(10,6)` route). The 8 target screen
    /// angles are NOT evenly 45-degrees apart under this anisotropic (2:1)
    /// projection, so comparing directly against the real projected
    /// reference vectors (rather than rounding to a uniform angular sector)
    /// is what makes this correct in general, not just for the 8 exact
    /// tested directions. The reference vector's own magnitude (not the
    /// query's, which is constant across all 8 comparisons for one call and
    /// so cannot change which one wins) is divided out, since a world
    /// pure-axis reference and a world-diagonal reference do not project to
    /// the same screen length under this skew -- an unnormalised dot
    /// product would bias the choice toward whichever reference happens to
    /// be longer on screen. Otherwise (no destination, or already arrived)
    /// returns `prevBin` unchanged -- freezing on the last active-movement
    /// facing while stationary.
    ///
    /// Verified against a real Godot view, not just re-derived on paper
    /// (TASK-056; the prior two correction rounds this session were each
    /// caught only by Dave live-testing a version checked solely against
    /// pure-function probes and static composited comparison images).
    let facingBin (prevBin: int) (position: Cell) (destination: Cell option) : int =
        match destination with
        | Some d when d <> position ->
            let screenDx, screenDy = toScreenVector (float (d.X - position.X), float (d.Y - position.Y))

            worldUnitDirections
            |> Array.mapi (fun bin (rdx, rdy) ->
                let rsx, rsy = toScreenVector (float rdx, float rdy)
                let dot = screenDx * rsx + screenDy * rsy
                let refMag = sqrt (rsx * rsx + rsy * rsy)
                bin, dot / refMag)
            |> Array.maxBy snd
            |> fst
        | _ -> prevBin

    /// One full `Run0..9` loop's real-time duration (TASK-056, backlog
    /// B-052): a presentation-only judgement call, not gameplay-affecting --
    /// a plausible sprite run-cycle pace (~16.7 fps over the pack's own
    /// 10-frame cycle).
    [<Literal>]
    let RunFrameSeconds = 0.06

    /// Which of the 10 `Run0..9` frames a moving agent should render,
    /// derived from a scene-wide, wall-clock `runClock` that only advances
    /// while ticks themselves are advancing (TASK-056: gated the same way
    /// `CommandDemoScene`'s own tactical pause gates tick catch-up, so the
    /// run cycle never animates while the sim itself is frozen). Pure.
    let runFrameIndex (runClock: float) : int = int (runClock / RunFrameSeconds) % 10

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
        | InsufficientAmmunition -> "out of ammunition"

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
        | InsufficientAmmunition -> "insufficient-ammunition"

    /// Developer-facing text for a `Commitment` (TASK-043; `DiagnosticRender.
    /// commitmentText`'s wording).
    let private devCommitmentText (c: Commitment) : string =
        match c with
        | Holding -> "holding"
        | Moving mc -> sprintf "moving-to-(%d,%d)" mc.Target.X mc.Target.Y
        | Suppressing sc -> sprintf "suppressing-agent-%d" (AgentId.value sc.Target)
        | Withdrawing wc -> sprintf "withdrawing-to-(%d,%d)" wc.Target.X wc.Target.Y
        | Assaulting ac ->
            let stage =
                match ac.Stage with
                | ApproachingStart -> "approaching-start"
                | AwaitingSupport -> "awaiting-support"
                | Advancing -> "advancing"
                | ClearingThreat -> "clearing-threat"

            sprintf "assaulting-(%d,%d)-%s" ac.Target.X ac.Target.Y stage

    /// Developer-facing text for an `AgentAmmo` overlay's fields (TASK-047,
    /// backlog B-030 proper; `DiagnosticRender.ammoText`'s wording).
    let private ammoText (magazine: int) (reserve: int) (reloading: bool) : string =
        if reloading then
            sprintf "reloading(reserve=%d)" reserve
        else
            sprintf "%d/%d" magazine reserve

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

        // TASK-047 (backlog B-030 proper): AgentAmmo is sparse (only an
        // agent not at a full default magazine+reserve carries one, the
        // AgentSuppression/AgentStress precedent) -- falls back to the
        // full-ammo default text rather than a bare "absent" reading.
        let ammo =
            overlays
            |> Array.tryPick (function
                | AgentAmmo(a, _, magazine, reserve, reloading) when a = agent -> Some(ammoText magazine reserve reloading)
                | _ -> None)
            |> Option.defaultValue (ammoText AmmoConfig.MagazineSize AmmoConfig.ReserveStart false)

        sprintf
            "commitment=%s suppression=%d stress=%d reason=%s exposed=%dcells ammo=%s"
            commitment
            suppression
            stress
            reason
            exposedCount
            ammo

    /// The developer-overlay legend (TASK-043 review round 2, Dave's live
    /// feedback: the overlay has no legend, and green/red are each reused
    /// for two different meanings -- LOS-visible vs FireLine-hit both green,
    /// Obstructed vs LOS-blocked both red). Static text, grouped by draw
    /// shape (cell marker vs line) since that is what actually disambiguates
    /// the reused hues on screen.
    let devLegendText: string =
        "[legend] cells: cyan=reserved red=obstructed yellow=known-contact orange=exposed-route | lines: green=visible/hit red=blocked grey=miss"

    /// A player-facing plain-language label for one objective (TASK-063,
    /// backlog B-033 narrowed): `Objective` carries no authored display
    /// name, so the label is derived from its own `AreaId`/`TargetId`
    /// string and DU case. `AllOf`/`Optional` recurse into their own
    /// parts/inner objective.
    let rec private objectiveLabel (o: Objective) : string =
        match o with
        | ReachArea(_, area) -> sprintf "reach %s" (AreaId.value area)
        | HoldArea(_, area, ticks) -> sprintf "hold %s for %d ticks" (AreaId.value area) ticks
        | DestroyTarget(_, target, ticks) -> sprintf "destroy %s (%d ticks)" (TargetId.value target) ticks
        | ExtractAgents(_, _, area) -> sprintf "extract via %s" (AreaId.value area)
        | AllOf(_, parts) -> parts |> Array.map objectiveLabel |> String.concat " and "
        | Optional inner -> sprintf "%s (optional)" (objectiveLabel inner)

    /// The same `ObjectiveId` extraction `Simulation.mission`'s own private
    /// recursive helper performs -- duplicated here rather than exposed
    /// across the assembly boundary, the `devReasonText`/`reasonText` "two
    /// audiences, not one shared function" precedent above.
    let rec private objectiveIdOf (o: Objective) : ObjectiveId =
        match o with
        | ReachArea(id, _)
        | HoldArea(id, _, _)
        | DestroyTarget(id, _, _)
        | ExtractAgents(id, _, _)
        | AllOf(id, _) -> id
        | Optional inner -> objectiveIdOf inner

    /// The mission-summary panel's lines (TASK-063, backlog B-033
    /// narrowed), or an empty array while `WorldState.MissionOutcome` is
    /// still `InProgress` -- `IClientScene.MissionSummaryLines`'s own
    /// "nothing to show yet" contract. First line is always the outcome
    /// headline; `Extracted` is read directly from `WorldState.Agents`
    /// (not added to `AgentSnapshot`, which carries no such field).
    let missionSummaryLines (state: WorldState) : string[] =
        match state.MissionOutcome with
        | InProgress -> [||]
        | outcome ->
            let headline = if outcome = Succeeded then "MISSION SUCCESS" else "MISSION FAILED"

            let labelOf (id: ObjectiveId) : string option =
                state.Objectives |> Array.tryFind (fun o -> objectiveIdOf o = id) |> Option.map objectiveLabel

            let completedLines =
                state.CompletedObjectives
                |> Array.choose (fun id -> labelOf id |> Option.map (sprintf "completed: %s"))

            let inProgressLines =
                state.ObjectiveProgress
                |> Array.choose (fun (id, ticks) ->
                    labelOf id |> Option.map (fun label -> sprintf "in progress: %s (%d ticks)" label ticks))

            let extractedLines =
                state.Agents
                |> Array.filter (fun a -> a.Extracted)
                |> Array.map (fun a -> sprintf "extracted: agent %d" (AgentId.value a.Id))

            Array.concat [ [| headline |]; completedLines; inProgressLines; extractedLines ]
