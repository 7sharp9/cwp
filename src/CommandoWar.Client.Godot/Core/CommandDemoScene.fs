namespace CwClientCore

open CommandoWar.Sim

/// Live selection, input-mapped `MoveTo` orders, a real-`Pathfinding.find`
/// route preview, and tactical pause (TASK-040, backlog B-026) -- the first
/// scene where the player, not a canned command log, drives
/// `Simulation.step`. Loads the real vertical-slice content
/// (`content/scenarios/bridgehead.cwscenario`, TASK-064, backlog B-035) via
/// `Ready`'s `scenarioContentPath`, not the `DemoScenario` diagnostic
/// fixture `DemoRenderScene` still uses. Reuses TASK-039's terrain-item/
/// depth-sort helpers (`RenderShared`) rather than duplicating them.
type CommandDemoScene() =
    let simHz = 20.0
    let maxCatchUpStepsPerFrame = 5

    // No seed is authored inside a `.cwscenario` file itself (confirmed by
    // inspection of `ScenarioFile.fs`'s grammar) -- `World.ofScenario` always
    // takes one from its caller, the `DemoScenario.Seed` precedent. Value
    // only has to be stable; nothing in this scene's own behaviour depends
    // on which draws the RNG stream produces.
    let bridgeheadSeed = 20260920UL

    // The on-screen radius (pixels) a real agent figure draws at (TASK-052,
    // backlog B-054: doubled from the original `10.0f` alongside
    // `FSharpSceneHost.cs`'s isometric tile scale, so the figure still fills
    // its tile at the larger fixed scale). Named and shared, rather than
    // repeated as a literal, because the fog-of-war ghost ring (TASK-051)
    // and the new hover-highlight ring (TASK-052, backlog B-053) both have
    // to line up with exactly the same footprint a real figure draws at --
    // drifting independently would visibly misalign them. `haloRadius`
    // keeps the halo's original ratio to the figure (1.7x).
    let agentRadius = 20.0f
    let haloRadius = 34.0f

    let mutable state = Unchecked.defaultof<WorldState>
    let mutable prevAgents: Map<int, Cell> = Map.empty
    let mutable currAgents: AgentSnapshot[] = [||]
    let mutable terrainItems: DrawItem[] = [||]

    // Objective/extraction area markers (TASK-063, backlog B-033 narrowed;
    // raised live by Dave testing the mission-summary panel -- with nothing
    // marking `WorldState.ObjectiveAreas`/`.ExtractionAreas` on the map, a
    // player has no way to tell where an objective actually is short of the
    // `F1` developer overlay, so the panel this task adds is unreachable in
    // practice). Static authored content, the `state.Terrain`/`.Bounds`
    // precedent for reading `WorldState` fields directly rather than
    // routing through a diagnostic overlay -- built once in `Ready`, not
    // recomputed every tick, since neither array ever changes after world
    // creation.
    let mutable objectiveMarkerItems: DrawItem[] = [||]
    let mutable coverIndicatorItems: DrawItem[] = [||]
    // Violet, not gold: the selection halo already draws a near-identical
    // gold/yellow ring (R=1.0,G=0.95,B=0.30) around whichever agent is
    // selected, so an objective marker that colour would be indistinguishable
    // from it the moment a selected agent stands on the objective cell --
    // exactly the case that matters most (an agent that just arrived).
    let objectiveAreaColor = (0.75f, 0.35f, 1.0f)
    let extractionAreaColor = (0.3f, 0.85f, 1.0f)

    let buildObjectiveMarkerItems (state: WorldState) : DrawItem[] =
        let markersFor (areas: Area[]) (color: float32 * float32 * float32) : DrawItem[] =
            areas
            |> Array.collect (fun area ->
                [| RenderShared.cellRing area.Cell color 0.85f (agentRadius * 0.9f)
                   RenderShared.cellLabel area.Cell (AreaId.value area.Id) color 0.9f 9.0f |])

        Array.append (markersFor state.ObjectiveAreas objectiveAreaColor) (markersFor state.ExtractionAreas extractionAreaColor)

    // Cover indicator (TASK-064 review, backlog B-035): Dave's live
    // feedback -- "no cover" -- `Terrain.Cover` (directional low cover,
    // mitigating hit chance and route-exposure) has never been rendered
    // anywhere, player-facing or developer overlay, since it was found
    // unpaintable through the current tileset at TASK-060. Static
    // authored content, the `objectiveMarkerItems`/`terrainItems`
    // precedent -- built once in `Ready`, never recomputed per tick. A
    // short spoke from the cell centre toward the covered direction (the
    // isometric projection already turns a cardinal `Cell` offset into the
    // correct on-screen edge), thicker for a higher `Level` -- shape
    // (which edge, how thick) carries the meaning, not colour alone.
    let coverColor = (0.55f, 0.85f, 1.0f)

    let buildCoverIndicatorItems (terrain: Terrain) : DrawItem[] =
        let offsetFor (d: Direction) : float32 * float32 =
            match d with
            | North -> 0.0f, -0.5f
            | East -> 0.5f, 0.0f
            | South -> 0.0f, 0.5f
            | West -> -0.5f, 0.0f

        [| for y in 0 .. terrain.Bounds.Height - 1 do
               for x in 0 .. terrain.Bounds.Width - 1 do
                   for d in Direction.all do
                       let level = Terrain.cover terrain { X = x; Y = y } d

                       if level > 0 then
                           let dx, dy = offsetFor d
                           let r, g, b = coverColor

                           yield
                               { Kind = 2
                                 TextureId = 0
                                 Cx = float32 x
                                 Cy = float32 y
                                 Cx2 = float32 x + dx
                                 Cy2 = float32 y + dy
                                 Text = ""
                                 R = r
                                 G = g
                                 B = b
                                 A = 0.85f
                                 Radius = 1.5f + float32 level } |]

    let mutable accum = 0.0
    let mutable alpha = 0.0
    let mutable hash = 0UL
    let mutable paused = false
    let mutable selected: AgentId option = None
    let mutable previewPath: Cell[] option = None
    let mutable hoveredCell: Cell option = None

    // XCOM-style HUD order-mode icons (TASK-048, backlog B-059; `4 =
    // Suppress` added by TASK-064, backlog B-035): which order type the
    // next non-agent left-click issues -- `0 = MoveTo` (the default,
    // unarmed state), `1 = Hold`, `2 = Assault`, `3 = Withdraw`,
    // `4 = Suppress` (targets the clicked cell's occupant, not the cell
    // itself -- see `OnClick`). Armed by `OnOrderModeClick` (a HUD-icon
    // click, resolved by the C# host's fixed icon rects, the
    // `OnClick`/`ScreenToCell` "primitives only" precedent); consumed and
    // reset back to `0` the instant an order is actually issued (an XCOM
    // ability-consumed-on-use idiom), so the player re-arms explicitly for
    // each non-default order rather than it silently staying armed across
    // multiple orders.
    let mutable orderMode = 0

    // Developer overlay (TASK-043, backlog B-029 proper): the same
    // `Diagnostics.DiagnosticFrame` every other developer renderer
    // (`DiagnosticRender.Ascii`/`.Svg`/`.Html`) consumes, built from this
    // scene's own live `Simulation.step` output instead of a replayed corpus
    // entry (TASK-029's read-only `AppraisalDemoScene`). Toggled by `F1`,
    // independent of tactical pause; off by default.
    let mutable devOverlay = false
    let mutable devFrame = Unchecked.defaultof<DiagnosticFrame>

    // The selected agent's traced line of sight to the currently hovered
    // cell (TASK-043: "line-of-sight rays and occluders"), recomputed on
    // every `OnHover` -- `Sight.trace` is a pure function over `Terrain` plus
    // two cells, already freely readable client-side (the `Pathfinding.find`
    // precedent; `Terrain` is static scenario geometry, not per-agent
    // authoritative state, so docs/03 section 12's `RenderSnapshot`-only rule
    // does not apply to it). `None` when nothing is selected.
    let mutable losRay: (Cell * Cell * bool) option = None

    // How long a meaningful order-disposition message stays on screen after
    // its underlying state clears, so it can actually be read (Dave's
    // feedback trying TASK-042 live: at the default 20 Hz sim rate,
    // DemoScenario's short routes complete in a handful of ticks -- well
    // under 200ms wall-clock -- so "accepted" flashed past unreadably before
    // reverting to "no order" the instant the order was fulfilled). Purely
    // presentational (AGENTS.md: "rendering ... are non-authoritative");
    // does not affect `Simulation.step`, `Disposition`, or any hash.
    let orderTextHoldSeconds = 1.5
    let mutable heldOrderText = ""
    let mutable orderTextHoldRemaining = 0.0

    // How long a fire-feedback effect (muzzle flash / impact sprite) stays
    // on screen after the tick it fired (TASK-046, backlog B-057; Dave's
    // live feedback: at the default 20 Hz sim rate a `FireLine` exists for a
    // single tick -- 50ms -- which read as nothing at all, the identical
    // "too fast to read" problem `orderTextHoldSeconds` above already solved
    // for order text). Purely presentational; does not affect
    // `Simulation.step`, `FireLine`, or any hash. Held items fade out
    // (`remaining / fireEffectHoldSeconds`) rather than vanishing abruptly.
    let fireEffectHoldSeconds = 0.4
    let heldFireLines = ResizeArray<Cell * Cell * bool * float>() // from, at, hit, remaining

    // Hit-flash (TASK-064 review, backlog B-035): Dave's live feedback --
    // "no reaction under fire" -- a hit target's own wound dot (a small,
    // low-opacity marker in `renderVitals`) reads as no reaction at all.
    // A brief bright tint on the figure itself, on every landed hit
    // (either side, the `heldFireLines`/`hostileKnownContactIdsBefore`
    // precedent of reacting to combat regardless of side), makes "you are
    // being shot at right now" legible without needing to read a health
    // bar. Same wall-clock hold/fade shape as `heldFireLines`.
    let hitFlashHoldSeconds = 0.3
    let heldHitFlashes = ResizeArray<int * float>() // agent id, remaining

    // Agent-facing bins (TASK-054, backlog B-052), one authoritative tick at
    // a time (the `heldFireLines`/`devFrame` precedent: computed once per
    // `stepOnce`, not per render frame).
    let mutable facing: Map<int, int> = Map.empty

    // TASK-056 review round 1 (backlog B-052): Dave live-tested the
    // corrected facing and run animation and found the facing still wrong
    // some of the time, and movement jerky and too fast. Root cause of both:
    // `AgentSnapshot.Position` only changes once an entire grid edge
    // completes (`Simulation.navigationAndMovement`'s `Progress`
    // accumulates for several ticks first, TASK-018) -- but this scene used
    // to lerp screen position between the *previous tick's* and *current
    // tick's* discrete `Position` alone, which are equal on every mid-edge
    // tick. The figure therefore sat visually frozen for most of an edge,
    // then snapped across the whole cell inside a single tick's real-time
    // window (50ms at 20Hz) -- reading as jerky, and (since the whole visual
    // displacement is compressed into that one short window) as
    // unnaturally fast. It also explains "doesn't always stay the correct
    // facing": `facingBin` was fed the raw crow-flies bearing to the
    // agent's overall final `Destination`, not its immediate next path
    // step -- correct on a straight leg (next step and final bearing
    // coincide) but visibly wrong the moment a route bends around terrain
    // (`Pathfinding.find`'s route, not a straight line, is what the agent
    // actually walks).
    //
    // Fixed by computing, once per tick alongside `facing`, each moving
    // agent's actual next path cell (`Pathfinding.find` from `Position`
    // toward `Destination`, the `committedItems` route-preview precedent,
    // just cached per tick instead of recomputed every render frame) --
    // fed into `facingBin` in place of the raw `Destination`, and used as
    // the *target* of the render-time lerp in place of `Position` itself.
    // The lerp *fraction* comes from `AgentSnapshot.Progress` (already
    // canonical and exact, not re-derived) normalised against
    // `edgeTickEstimate`: the exact tick-count an edge takes depends on
    // `Terrain.moveCost` and the agent's own (non-canonical, not exposed to
    // the client) `MoveSpeed`, neither fully available client-side, so the
    // threshold is *learned* from the most recently completed edge instead
    // of replicated exactly -- self-corrects every edge regardless of a
    // wrong guess, since `Position` itself always snaps to the true cell
    // the instant an edge genuinely completes.
    let mutable nextStepCell: Map<int, Cell> = Map.empty
    let mutable prevProgress: Map<int, int> = Map.empty

    // Default guess before any edge has completed (the Trooper half-speed
    // ratio both `DemoScenario` and `bridgehead.cwscenario` author for their
    // own "trooper" unit type, TASK-049/TASK-061 -- `MoveSpeed = 2` in both).
    // Only ever used for the very first tick of an agent's very first move
    // this session; every edge after that uses its own learned estimate.
    let defaultEdgeTickEstimate = 2
    let mutable edgeTickEstimate: Map<int, int> = Map.empty

    // TASK-056 review round 2 (backlog B-052): Dave live-tested again and
    // reported "seemed the same", plus two new specifics -- the selection
    // halo (`haloItems` below) jumps between cells, and the run animation
    // keeps playing when an agent is stuck on the same cell. Both trace to
    // the same gap: round 1's fix computed a smooth `edgeFrac` and a
    // genuine `nextStepCell` target, but never accounted for an agent whose
    // route is stalled by a reservation contest -- `Simulation.
    // navigationAndMovement` freezes such an agent's `Progress` at
    // `startProgress` (does not increment it) while `Destination` stays
    // set. Recomputed fresh every tick in `stepOnce`: `true` when the agent
    // still has an unmet `Destination` but its `Progress` did not change
    // this tick (compared against the value it held entering the tick, the
    // same `priorProgress` snapshot `edgeTickEstimate` above already
    // reads). Used by `renderPos` (below) to stop crediting `alpha`'s
    // per-render-frame ramp to an edge that is not actually advancing (the
    // old code produced a forward-creep-then-snap-back sawtooth on a
    // stalled agent even though `Position` never moved), and by
    // `renderVitals`'s `isMoving` to stop the run cycle the instant an
    // agent stops making real progress, not just when it lacks a
    // `Destination`.
    let mutable stalled: Map<int, bool> = Map.empty

    // Run-cycle animation clock (TASK-056, backlog B-052): real elapsed
    // time, advanced only while `not paused` (the same gate tick catch-up
    // itself uses, in `Update` below) so a moving agent's run cycle never
    // animates while the sim is tactically frozen -- unlike
    // `heldFireLines`/`heldAudioCues`, which deliberately decay even while
    // paused, this must track genuine simulation progress, not wall clock
    // alone. `RenderShared.runFrameIndex` turns it into a `0..9` frame.
    let mutable runClock = 0.0

    // How long an audio-localised threat cue (TASK-054, backlog B-056)
    // stays on screen after the qualifying shot that raised it -- the
    // `fireEffectHoldSeconds` precedent, held longer since it is the
    // player's only cue that anything happened at all (no accompanying
    // fire-line/muzzle-flash draws for a shooter this fogged). Anchor and
    // tip are resolved once, at trigger time, from the squad centroid and
    // the grid bounds then in effect -- not recomputed every frame.
    let audioCueHoldSeconds = 1.5
    let heldAudioCues = ResizeArray<(float32 * float32) * (float32 * float32) * float>() // anchor, tip, remaining

    // Player-issued orders awaiting delivery. `RecordedCommand` (the
    // `DemoDrive.commandsForTick` precedent) rather than a bespoke type --
    // its `Tick` is the delivery tick, independent of `PlayerCommand.
    // IssuedAtTick` (TASK-024). Never persisted; this scene keeps no replay
    // log.
    let pending = ResizeArray<RecordedCommand>()
    let mutable nextCommandId = 0

    let commandsForTick (t: int64) : PlayerCommand[] =
        let cmds = pending |> Seq.filter (fun c -> c.Tick = t) |> Seq.map (fun c -> c.Command) |> Array.ofSeq
        pending.RemoveAll(fun c -> c.Tick = t) |> ignore
        cmds

    /// An agent's current `VitalStatus`, read from `devFrame`'s always-on
    /// `AgentVitals` overlay (TASK-053, backlog B-061) -- `AgentSnapshot`
    /// itself carries no vitals field (docs/03 section 12: values only), so
    /// this is the same lookup `renderVitals` (`DrawList`) already performs
    /// inline, shared here so the selection/order-mode gating below reads
    /// the identical source rather than duplicating the lookup. Defaults to
    /// full-health `Alive` if not found, the `renderVitals` precedent.
    let vitalsOf (id: AgentId) : VitalStatus =
        devFrame.Overlays
        |> Array.tryPick (function
            | AgentVitals(aid, _, v) when aid = id -> Some v
            | _ -> None)
        |> Option.defaultValue (Alive Agent.MaxHealth)

    /// The listening squad's reference point for an audio-cue bearing
    /// (TASK-054, backlog B-056): the mean cell position of every currently
    /// `Alive` friendly agent. `None` when the squad has no living member
    /// left to hear anything.
    let squadCentroid () : (float32 * float32) option =
        let alive = currAgents |> Array.filter (fun a -> a.Side = Friendly && Casualty.isAlive (vitalsOf a.Id))

        if alive.Length = 0 then
            None
        else
            let n = float32 alive.Length
            Some((alive |> Array.sumBy (fun a -> float32 a.Position.X)) / n, (alive |> Array.sumBy (fun a -> float32 a.Position.Y)) / n)

    /// A boundary anchor point just outside `state.Bounds`, one of 8 `45°`
    /// sectors around `angle` (radians, world-grid space) -- the audio
    /// cue's own coarser sibling to `RenderShared.facingBin`'s 10-bin
    /// scheme (TASK-054, backlog B-056: "a rough bearing, not the shooter's
    /// exact position", so 8 wide sectors rather than a precise line to the
    /// shooter's true cell). `margin` keeps the anchor clear of the terrain
    /// itself.
    let audioCueAnchor (bounds: GridBounds) (angle: float) : float32 * float32 =
        let margin = 1.0f
        let w = float32 (bounds.Width - 1)
        let h = float32 (bounds.Height - 1)
        let sectorRaw = int (System.Math.Round(angle / (System.Math.PI / 4.0)))
        let sector = ((sectorRaw % 8) + 8) % 8

        match sector with
        | 0 -> (w + margin, h * 0.5f) // East
        | 1 -> (w + margin, h + margin) // South-East
        | 2 -> (w * 0.5f, h + margin) // South
        | 3 -> (-margin, h + margin) // South-West
        | 4 -> (-margin, h * 0.5f) // West
        | 5 -> (-margin, -margin) // North-West
        | 6 -> (w * 0.5f, -margin) // North
        | _ -> (w + margin, -margin) // North-East

    /// One authoritative step consuming any orders queued for delivery at
    /// `state.Tick + 1`. Used by both `Update` (wall-clock-paced) and the
    /// scripted headless self-check (paced by direct calls, no wall clock).
    let stepOnce () =
        // The friendly squad's known-contact set as of *before* this tick's
        // own Perception phase runs (TASK-054, backlog B-056). Perception
        // runs ahead of Combat in phase order (`Simulation.step`'s own
        // phase sequence), so the *post*-tick `devFrame` below already
        // reflects any contact this same tick's shot itself caused the
        // squad to newly register -- checking against that would suppress
        // the audio cue on exactly the "just came into view and fired"
        // tick B-056 exists to cover, leaving only already-redundant cases
        // reachable. Checking the *pre*-tick set instead correctly still
        // fires the cue the instant a previously-unknown hostile shoots.
        let hostileKnownContactIdsBefore =
            devFrame.Overlays
            |> Array.choose (function
                | KnownContact(_, contact, _, _) -> Some(AgentId.value contact)
                | _ -> None)
            |> Set.ofArray

        let r = Simulation.step SimConfig.standard (commandsForTick (state.Tick + 1L)) state
        prevAgents <- currAgents |> Array.map (fun a -> AgentId.value a.Id, a.Position) |> Map.ofArray
        let priorProgress = prevProgress
        state <- r.State
        currAgents <- r.Snapshot.Agents
        hash <- r.StateHash.Value
        devFrame <- Diagnostics.frameOf r

        // Mission summary panel (TASK-063, backlog B-033 narrowed): the
        // instant `MissionOutcome` leaves `InProgress` (a one-way
        // transition, `Simulation.mission`'s own precedent), tactical
        // pause engages on its own -- the same field `Space` toggles --
        // so the sim stops advancing and `OnClick`'s order-issuing guard
        // below (which also checks `MissionOutcome` directly, in case the
        // player un-pauses) has nothing further to resolve.
        if state.MissionOutcome <> InProgress then
            paused <- true

        // The immediate next path cell (not the far-off final `Destination`)
        // for every currently-moving agent -- see the field comment on
        // `nextStepCell` above. `Array.skip 1` on the returned route would
        // also work; indexing `cells.[1]` directly makes "the cell right
        // after the one we're standing on" explicit.
        nextStepCell <-
            currAgents
            |> Array.fold
                (fun m a ->
                    let id = AgentId.value a.Id
                    match a.Destination with
                    | Some d when d <> a.Position ->
                        match Pathfinding.find state.Terrain a.Position d with
                        | Found(cells, _) when cells.Length > 1 -> Map.add id cells.[1] m
                        | _ -> Map.add id a.Position m
                    | _ -> Map.add id a.Position m)
                nextStepCell

        // Learn this edge's real tick-count the instant it completes (see
        // the field comment on `edgeTickEstimate`): `priorProgress` is the
        // value `Progress` held the tick *before* this one, i.e. the last
        // tick still mid-edge, so the threshold just crossed is one past it.
        edgeTickEstimate <-
            currAgents
            |> Array.fold
                (fun m a ->
                    let id = AgentId.value a.Id
                    match prevAgents |> Map.tryFind id with
                    | Some p when p <> a.Position ->
                        let justCrossed = (priorProgress |> Map.tryFind id |> Option.defaultValue 0) + 1
                        Map.add id justCrossed m
                    | _ -> m)
                edgeTickEstimate

        prevProgress <- currAgents |> Array.map (fun a -> AgentId.value a.Id, a.Progress) |> Map.ofArray

        // TASK-056 review round 2: see the field comment on `stalled` above.
        // `priorProgress` is this same agent's `Progress` entering this
        // tick (captured before `state <- r.State` overwrote it), so
        // comparing it against the post-tick value directly tells us
        // whether this tick's own attempt actually advanced the edge.
        stalled <-
            currAgents
            |> Array.fold
                (fun m a ->
                    let id = AgentId.value a.Id
                    let tryingToMove = a.Destination |> Option.exists (fun d -> d <> a.Position)
                    let madeProgress = a.Progress <> (priorProgress |> Map.tryFind id |> Option.defaultValue -1)
                    Map.add id (tryingToMove && not madeProgress) m)
                Map.empty

        facing <-
            currAgents
            |> Array.fold
                (fun m a ->
                    let id = AgentId.value a.Id
                    let prevBin = m |> Map.tryFind id |> Option.defaultValue 0
                    let step = nextStepCell |> Map.tryFind id
                    Map.add id (RenderShared.facingBin prevBin a.Position step) m)
                facing

        for overlay in devFrame.Overlays do
            match overlay with
            | FireLine(shooter, from, target, at, hit) ->
                heldFireLines.Add(from, at, hit, fireEffectHoldSeconds)

                if hit then
                    let targetId = AgentId.value target
                    let idx = heldHitFlashes.FindIndex(fun (id, _) -> id = targetId)
                    if idx >= 0 then heldHitFlashes.[idx] <- (targetId, hitFlashHoldSeconds)
                    else heldHitFlashes.Add(targetId, hitFlashHoldSeconds)

                let shooterWasUnknownHostile =
                    currAgents
                    |> Array.exists (fun a -> a.Id = shooter && a.Side = Hostile)
                    && not (Set.contains (AgentId.value shooter) hostileKnownContactIdsBefore)

                if shooterWasUnknownHostile then
                    match squadCentroid () with
                    | Some(ccx, ccy) ->
                        let angle = atan2 (float from.Y - float ccy) (float from.X - float ccx)
                        let anchor = audioCueAnchor state.Bounds angle
                        let ax, ay = anchor
                        let tip = (ax + 0.3f * (ccx - ax), ay + 0.3f * (ccy - ay))
                        heldAudioCues.Add(anchor, tip, audioCueHoldSeconds)
                    | None -> ()
            | _ -> ()

    let friendlyAt (cell: Cell) : AgentSnapshot option =
        currAgents |> Array.tryFind (fun a -> a.Side = Friendly && a.Position = cell)

    /// `Suppress`'s own target-resolution helper (TASK-064, backlog B-035):
    /// `Command.suppress` takes an `AgentId`, not a `Cell`, so arming
    /// Suppress and clicking a cell needs to resolve whichever agent (any
    /// side -- the enemy being suppressed) occupies it. `Appraisal.appraise`
    /// itself is what actually gates this on the target being a real known
    /// contact (`Unable(TargetNotKnown)` otherwise, `Appraisal.fs`); this
    /// helper only resolves "who is standing here", the same
    /// already-rendered `currAgents` `friendlyAt` reads.
    let enemyAt (cell: Cell) : AgentSnapshot option =
        currAgents |> Array.tryFind (fun a -> a.Side = Hostile && a.Position = cell)

    let agentPosition (id: AgentId) : Cell option =
        currAgents |> Array.tryFind (fun a -> a.Id = id) |> Option.map (fun a -> a.Position)

    /// The on-screen position a moving agent's own figure renders at (the
    /// `nextStepCell`/`edgeTickEstimate` smoothing, folding in the `stalled`
    /// freeze above) -- shared so every other consumer of "where is this
    /// agent right now" (the selection halo below) agrees with it, instead
    /// of the figure alone reading smoothly while everything else still
    /// jumps between raw grid cells (Dave's review round 2 report: "the
    /// circle thats drawn as selection indicator ... jumps between
    /// cells" -- `haloItems` used to read `agentPosition`'s raw discrete
    /// `Cell` directly). Only meaningful for a currently-`Alive` agent;
    /// callers with a non-`Alive` selection must use the agent's raw
    /// `Position` instead, matching `renderVitals`'s own `Dead`/
    /// `Incapacitated` branches, which never lerp.
    let renderPos (a: AgentSnapshot) : float32 * float32 =
        let id = AgentId.value a.Id
        let target = nextStepCell |> Map.tryFind id |> Option.defaultValue a.Position
        let estTicks = edgeTickEstimate |> Map.tryFind id |> Option.defaultValue defaultEdgeTickEstimate |> max 1
        let isStalled = stalled |> Map.tryFind id |> Option.defaultValue false

        let edgeFrac =
            if isStalled then
                // No `alpha` credit while genuinely stalled -- see the
                // field comment on `stalled`: crediting the render-frame
                // ramp to an edge that will not actually complete this
                // tick is exactly the sawtooth bug being fixed here.
                System.Math.Clamp(float a.Progress / float estTicks, 0.0, 1.0)
            else
                System.Math.Clamp((float a.Progress + alpha) / float estTicks, 0.0, 1.0)

        float32 a.Position.X + float32 (target.X - a.Position.X) * float32 edgeFrac,
        float32 a.Position.Y + float32 (target.Y - a.Position.Y) * float32 edgeFrac

    /// Auto-disarms the HUD order mode back to `0` the instant the selected
    /// agent is no longer `Alive` (TASK-053, backlog B-061; Dave's own
    /// words on accepting TASK-052: "the cursor which has move active or
    /// whatever should be deactivated" when the selected agent is removed
    /// by death). Deliberately does NOT clear `selected` itself -- Dave's
    /// follow-up clarified viewing a dead/incapacitated agent's status
    /// stays allowed, only issuing it new orders does not; see `OnClick`'s
    /// order-issuing guard and `OnHover`'s `previewPath` gating for where
    /// that is actually enforced. Called after every tick (vitals only ever
    /// change from `stepOnce`) and after a selection change (selecting an
    /// already-non-`Alive` friendly directly must disarm just as promptly).
    let syncOrderModeToSelection () =
        match selected with
        | Some id when orderMode <> 0 && not (Casualty.isAlive (vitalsOf id)) -> orderMode <- 0
        | _ -> ()

    /// A route's cells excluding the traveller's own starting cell, drawn as
    /// small `Kind = 1` dots in the given colour/alpha/radius -- the shared
    /// shape behind the hover preview, the pending-order marker, and the
    /// committed route (Dave's review feedback on TASK-040: each state needs
    /// its own colour so "is this order actually set" is legible).
    let routeDots (cells: Cell[]) (r: float32, g: float32, b: float32) (a: float32) (radius: float32) : DrawItem[] =
        cells
        |> Array.skip (min 1 cells.Length)
        |> Array.map (fun c ->
            { Kind = 1
              TextureId = 0
              Cx = float32 c.X
              Cy = float32 c.Y
              Cx2 = 0.0f
              Cy2 = 0.0f
              Text = ""
              R = r
              G = g
              B = b
              A = a
              Radius = radius })

    interface IClientScene with
        // TASK-064 (backlog B-035): loads the real vertical-slice content
        // (`content/scenarios/bridgehead.cwscenario`) instead of
        // `DemoScenario` -- this is the first task to run the docs/07
        // mission itself in a play scene, not a hand-authored diagnostic
        // fixture. Fails hard (`failwith`, the `DemoScenario.initialState()`
        // precedent) on a parse/validation/world-build error: this is
        // fixed, already-validated project content (`cwheadless import`
        // exits 0), not user-supplied input needing graceful degradation.
        member _.Ready(scenarioContentPath: string) =
            let scenario =
                match ScenarioFile.parse (System.IO.File.ReadAllText scenarioContentPath) with
                | Error e ->
                    failwith $"CommandDemoScene: scenario parse failed for '{scenarioContentPath}': {ScenarioFile.describeError e}"
                | Ok raw ->
                    match Scenario.validate raw with
                    | Error es -> failwith $"CommandDemoScene: scenario invalid for '{scenarioContentPath}': {es}"
                    | Ok s -> s

            state <-
                match World.ofScenario scenario bridgeheadSeed with
                | Ok w -> w
                | Error e -> failwith $"CommandDemoScene: scenario world build failed for '{scenarioContentPath}': {e}"

            terrainItems <- RenderShared.buildTerrainItems state.Terrain
            objectiveMarkerItems <- buildObjectiveMarkerItems state
            coverIndicatorItems <- buildCoverIndicatorItems state.Terrain
            devFrame <- Diagnostics.frame state
            currAgents <-
                state.Agents
                |> Array.map (fun a ->
                    { Id = a.Id
                      Side = a.Side
                      Position = a.Position
                      Progress = a.Progress
                      Destination = a.Destination
                      Disposition = a.Disposition })
            prevAgents <- currAgents |> Array.map (fun a -> AgentId.value a.Id, a.Position) |> Map.ofArray

            // TASK-056 review round 2: without this, `stalled`'s very first
            // `stepOnce` call would compare against an empty `prevProgress`
            // (the `-1` sentinel default), misreading a genuinely-blocked
            // agent's first tick (`Progress` frozen at its own starting `0`)
            // as "made progress" (`0 <> -1`) -- a one-tick blind spot right
            // at the moment an order that stalls immediately is issued.
            // Seeding from each agent's real starting `Progress` (always `0`
            // for a fresh `DemoScenario`) makes the very first comparison
            // exact instead of sentinel-driven.
            prevProgress <- currAgents |> Array.map (fun a -> AgentId.value a.Id, a.Progress) |> Map.ofArray

        member _.Update(deltaSeconds: float) =
            if not paused then
                let simStep = 1.0 / simHz
                accum <- accum + deltaSeconds
                let mutable steps = 0

                while accum >= simStep && steps < maxCatchUpStepsPerFrame do
                    stepOnce ()
                    accum <- accum - simStep
                    steps <- steps + 1

                // TASK-056, backlog B-052: only advances while ticks
                // themselves are advancing -- see the field comment on
                // `runClock`.
                runClock <- runClock + deltaSeconds

                // The selected agent's vitals can only change from a
                // `stepOnce` (TASK-053, backlog B-061) -- disarm any order
                // mode it invalidated as soon as it happens, not on the
                // next click.
                syncOrderModeToSelection ()

            alpha <- System.Math.Clamp(accum * simHz, 0.0, 1.0)

            // Decrement every held fire effect by real wall-clock time
            // (unlike the tick catch-up above, this runs even while
            // `paused`, so a flash already showing when the player pauses
            // does not get stuck on screen forever) and drop expired ones.
            for i in heldFireLines.Count - 1 .. -1 .. 0 do
                let from, at, hit, remaining = heldFireLines.[i]
                let remaining' = remaining - deltaSeconds
                if remaining' <= 0.0 then heldFireLines.RemoveAt(i)
                else heldFireLines.[i] <- (from, at, hit, remaining')

            for i in heldHitFlashes.Count - 1 .. -1 .. 0 do
                let id, remaining = heldHitFlashes.[i]
                let remaining' = remaining - deltaSeconds
                if remaining' <= 0.0 then heldHitFlashes.RemoveAt(i)
                else heldHitFlashes.[i] <- (id, remaining')

            // Audio-localised threat cues (TASK-054, backlog B-056) decay by
            // real wall-clock time the same way, independent of `paused`.
            for i in heldAudioCues.Count - 1 .. -1 .. 0 do
                let anchor, tip, remaining = heldAudioCues.[i]
                let remaining' = remaining - deltaSeconds
                if remaining' <= 0.0 then heldAudioCues.RemoveAt(i)
                else heldAudioCues.[i] <- (anchor, tip, remaining')

            // Hold a meaningful order-disposition message on screen for at
            // least `orderTextHoldSeconds` after it appears, even once the
            // underlying `Disposition` clears (order fulfilled) -- see the
            // field comment above. A genuinely new message (a fresh order,
            // or a reappraisal flipping the outcome) always overrides
            // immediately; only the fall-back to "no order" is delayed.
            // TASK-064 review (backlog B-035): `dispositionText` alone says
            // only "accepted", which reads as "your soldier is doing what
            // you clicked" -- not true for a formationed agent redirected
            // by `Appraisal.resolveFormationTarget` (see `OnHover`'s own
            // comment above). Appending the agent's real, already-canonical
            // `Destination` whenever one is active tells the player exactly
            // where the soldier is actually headed, regardless of whether
            // that matches the clicked cell.
            let liveOrderText =
                selected
                |> Option.bind (fun id -> currAgents |> Array.tryFind (fun a -> a.Id = id))
                |> Option.map (fun a ->
                    let baseText = RenderShared.dispositionText a.Disposition
                    match a.Disposition, a.Destination with
                    | Some Accepted, Some dest -> sprintf "%s -> (%d,%d)" baseText dest.X dest.Y
                    | _ -> baseText)
                |> Option.defaultValue ""

            if liveOrderText <> "" && liveOrderText <> "no order" then
                heldOrderText <- liveOrderText
                orderTextHoldRemaining <- orderTextHoldSeconds
            elif orderTextHoldRemaining > 0.0 then
                orderTextHoldRemaining <- max 0.0 (orderTextHoldRemaining - deltaSeconds)
            else
                heldOrderText <- liveOrderText

        member _.DrawList() =
            // Player-facing fog of war (TASK-051, backlog B-055): a hostile
            // this squad has never made contact with (or whose contact has
            // fully expired, `PerceptionConfig.ExpireAfter` ticks after it
            // was last seen) is hidden entirely; one known but not currently
            // visible this tick draws only a last-known-position marker,
            // never its true live position or vitals. `devFrame` already
            // carries `WorldState.TacticalKnowledge` as a `KnownContact`
            // overlay per contact regardless of the F1 dev-overlay toggle
            // (the `AgentVitals` precedent just below), so no
            // `CommandoWar.Sim` change is needed to read it here.
            let hostileKnownContacts =
                devFrame.Overlays
                |> Array.choose (function
                    | KnownContact(cell, contact, confidence, lastSeenTick) ->
                        Some(AgentId.value contact, (cell, confidence, lastSeenTick))
                    | _ -> None)
                |> Map.ofArray

            // Player-facing casualty markers (TASK-046, backlog B-057):
            // promotes `AgentVitals` from developer-only to always-on --
            // `devFrame` is recomputed every tick regardless of the F1
            // dev-overlay toggle (see its own field comment), so the data
            // already exists. Mirrors `DiagnosticRender.Svg`'s own vitals
            // vocabulary (a wound dot, a status badge, a dead cross) rather
            // than a colour-only recolour of the agent figure (docs/06
            // "status indicators that do not rely on colour alone").
            let renderVitals (a: AgentSnapshot) : DrawItem[] =
                let vitals = vitalsOf a.Id

                match vitals with
                | Dead ->
                    // A small black cross where the figure would be --
                    // shape, not colour, carries "no longer active" (the
                    // `DiagnosticRender.Svg` dead-cross precedent). No
                    // figure at all: a corpse is not a coloured variant
                    // of a living agent.
                    let cx, cy = float32 a.Position.X, float32 a.Position.Y
                    let d = 0.28f

                    [| { Kind = 2
                         TextureId = 0
                         Cx = cx - d
                         Cy = cy - d
                         Cx2 = cx + d
                         Cy2 = cy + d
                         Text = ""
                         R = 0.05f
                         G = 0.05f
                         B = 0.05f
                         A = 0.9f
                         Radius = 2.5f }
                       { Kind = 2
                         TextureId = 0
                         Cx = cx - d
                         Cy = cy + d
                         Cx2 = cx + d
                         Cy2 = cy - d
                         Text = ""
                         R = 0.05f
                         G = 0.05f
                         B = 0.05f
                         A = 0.9f
                         Radius = 2.5f } |]
                | Incapacitated _ ->
                    // Darkened figure plus a plain-language text badge
                    // (the `RenderShared.reasonText` player-vocabulary
                    // precedent -- no bleed-out tick count, that is
                    // developer detail, `devReasonText`'s own distinction)
                    // -- the badge, not just the tint, is the signal. Keeps
                    // its last active-movement facing (TASK-054, backlog
                    // B-052) rather than snapping to bin 0 -- a downed agent
                    // isn't moving, so nothing should visually reset it.
                    // Always frozen on the idle pose (Cx2 = -1, TASK-056),
                    // never a run-cycle frame, regardless of any stale
                    // `Destination` left over from before it went down --
                    // `NavigationAndMovement` skips a non-`Alive` agent
                    // (TASK-045), so `Destination` is never cleared for one.
                    let r, g, b = RenderShared.agentColor a.Side
                    let facingBin = facing |> Map.tryFind (AgentId.value a.Id) |> Option.defaultValue 0

                    [| { Kind = 1
                         TextureId = facingBin
                         Cx = float32 a.Position.X
                         Cy = float32 a.Position.Y
                         Cx2 = -1.0f
                         Cy2 = 0.0f
                         Text = ""
                         R = r * 0.5f
                         G = g * 0.5f
                         B = b * 0.5f
                         A = 1.0f
                         Radius = agentRadius }
                       RenderShared.cellLabel a.Position "down" (0.9f, 0.9f, 0.9f) 0.9f 9.0f |]
                | Alive health ->
                    let id = AgentId.value a.Id
                    let r, g, b = RenderShared.agentColor a.Side
                    let facingBin = facing |> Map.tryFind id |> Option.defaultValue 0

                    // TASK-056, backlog B-052: a `-1` `Cx2` freezes on the
                    // idle pose the instant `Destination` clears or is
                    // reached (the existing facing-freeze precedent above);
                    // otherwise cycles through `Run0..9` by `runClock`.
                    // TASK-056 review round 2: also freezes while `stalled`
                    // -- Dave's report that the run animation keeps playing
                    // when an agent is stuck on the same cell was a real
                    // gap, since a route-reservation contest leaves
                    // `Destination` unmet indefinitely with no actual
                    // motion. See the field comment on `stalled`.
                    let isMoving =
                        (a.Destination |> Option.exists (fun d -> d <> a.Position))
                        && not (stalled |> Map.tryFind id |> Option.defaultValue false)

                    let runFrame = if isMoving then RenderShared.runFrameIndex runClock else -1

                    // TASK-056 review round 1/2: smooth, continuous screen
                    // position across a whole multi-tick edge, frozen
                    // instead of sawtoothing while genuinely stalled -- see
                    // `renderPos`'s own doc comment.
                    let cx, cy = renderPos a

                    // Hit flash (TASK-064 review, backlog B-035): Dave's
                    // live feedback -- "no reaction under fire" -- blends
                    // the figure's own colour toward white, fading back
                    // over `hitFlashHoldSeconds`, the instant a `FireLine`
                    // lands on this agent (either side). A colour blend on
                    // the existing figure, not a new draw item, so it never
                    // competes with the wound dot/selection halo/etc. for
                    // depth-sort or screen space.
                    let flash =
                        heldHitFlashes
                        |> Seq.tryPick (fun (fid, remaining) -> if fid = id then Some remaining else None)
                        |> Option.map (fun remaining -> float32 (remaining / hitFlashHoldSeconds))
                        |> Option.defaultValue 0.0f

                    let blend (c: float32) = c + (1.0f - c) * flash

                    let figure =
                        { Kind = 1
                          TextureId = facingBin
                          Cx = cx
                          Cy = cy
                          Cx2 = float32 runFrame
                          Cy2 = 0.0f
                          Text = ""
                          R = blend r
                          G = blend g
                          B = blend b
                          A = 1.0f
                          Radius = agentRadius }

                    if health >= Agent.MaxHealth then
                        [| figure |]
                    else
                        // A small red wound dot -- its presence is the
                        // signal, not a colour-only tint on the agent
                        // itself (the `Dead`-cross reasoning above);
                        // opacity scales with severity, the
                        // `DiagnosticRender.Svg` wound-dot precedent.
                        let severity = float32 (Agent.MaxHealth - health) / float32 Agent.MaxHealth

                        [| figure
                           RenderShared.cellMarker a.Position (0.9f, 0.15f, 0.1f) (0.4f + severity * 0.5f) 4.0f |]

            // Fog of war (TASK-051, backlog B-055) applies only to the
            // normal player view. The `F1` developer overlay keeps its
            // TASK-043 ground-truth behaviour unchanged: every agent
            // renders via `renderVitals` at its true position regardless of
            // contact, so `devItems`'s own separate known-contact marker
            // below still visibly diverges from it for comparison -- the
            // reason that overlay exists in the first place.
            let agentItems =
                currAgents
                |> Array.collect (fun a ->
                    if devOverlay then
                        renderVitals a
                    else
                        match a.Side, Map.tryFind (AgentId.value a.Id) hostileKnownContacts with
                        | Hostile, None ->
                            // Never contacted, or fully expired -- fog of
                            // war hides it entirely.
                            [||]
                        | Hostile, Some(lastKnownCell, confidence, lastSeenTick) when lastSeenTick < devFrame.Tick ->
                            // Known, but not currently visible this tick: a
                            // hollow ring at its last-known position, not
                            // its true live position -- a distinct outline
                            // shape, not a translucent fill, so it reads as
                            // stale intel rather than a dim real agent (the
                            // "status indicators that do not rely on colour
                            // alone" principle `renderVitals`'s `Dead`/
                            // `Incapacitated` branches also follow). Opacity
                            // follows `Contact.Confidence`'s own band drop
                            // (`PerceptionConfig.ConfidenceBandDrop`), the
                            // wound-dot-severity-scales-opacity precedent in
                            // `renderVitals`'s `Alive` branch above.
                            let r, g, b = RenderShared.agentColor a.Side
                            let alpha = 0.9f * float32 confidence / float32 PerceptionConfig.ConfidenceFull

                            [| RenderShared.cellRing lastKnownCell (r, g, b) alpha agentRadius
                               RenderShared.cellLabel lastKnownCell "?" (r, g, b) alpha (agentRadius * 0.45f) |]
                        | _ -> renderVitals a)

            // Selection halo: a larger, translucent Kind = 1 item at the
            // selected agent's own cell, inserted before its real circle so
            // the stable depth-sort tie-break draws it underneath.
            //
            // TASK-056 review round 2: used to read `agentPosition`'s raw
            // discrete `Cell` directly, so it kept snapping between grid
            // cells exactly like before round 1's smoothing fix -- Dave's
            // own report ("the circle thats drawn as selection indicator,
            // it jumps between cells") pointed straight at this, and
            // explains "seemed the same": the halo is the dominant visual
            // cue while watching a selected agent move, so a smoothed
            // figure sitting under a still-snapping halo reads as no fix at
            // all. Now shares `renderPos` with the real figure -- but only
            // for a currently-`Alive` agent; a `Dead`/`Incapacitated`
            // selection must stay pinned to its raw `Position`, matching
            // `renderVitals`'s own frozen (never-lerped) figure for those
            // states, or the halo would visibly drift off a figure that
            // itself never moves.
            let haloItems =
                match selected |> Option.bind (fun id -> currAgents |> Array.tryFind (fun a -> a.Id = id)) with
                | Some a ->
                    let cx, cy =
                        match vitalsOf a.Id with
                        | Alive _ -> renderPos a
                        | _ -> float32 a.Position.X, float32 a.Position.Y

                    [| { Kind = 1
                         TextureId = 0
                         Cx = cx
                         Cy = cy
                         Cx2 = 0.0f
                         Cy2 = 0.0f
                         Text = ""
                         R = 1.0f
                         G = 0.95f
                         B = 0.30f
                         A = 0.35f
                         Radius = haloRadius } |]
                | None -> [||]

            // Hover highlight for a selectable agent (TASK-052, backlog
            // B-053): a thin ring around a friendly agent's own figure when
            // the mouse is over it but has not clicked, so the player knows
            // a click there will select it -- mirroring the hover-preview
            // pattern this scene already uses for routes (Dave's own
            // wording), but a shape distinct from both the bigger
            // translucent selection halo above and the fog-of-war ghost
            // ring (TASK-051), so none of the three is mistaken for another.
            // Uses `agentRadius` (not a literal) for the same reason the
            // fog-of-war ghost ring does: it must hug exactly the footprint
            // the real figure draws at.
            let hoverHighlightItems =
                hoveredCell
                |> Option.bind friendlyAt
                |> Option.map (fun a ->
                    RenderShared.cellRing a.Position (0.95f, 0.95f, 1.0f) 0.8f (agentRadius + 3.0f))
                |> Option.map (fun item -> [| item |])
                |> Option.defaultValue [||]

            // Leader marker (TASK-064 review, backlog B-035): Dave's live
            // feedback -- "you cant visually tell who is a leader" -- every
            // agent renders identically regardless of role.
            // `Casualty.currentLeader` (the TASK-045 succession rule:
            // lowest-id `Alive` `Friendly` agent) is a pure function over
            // already-held `state.Agents`, so no `CommandoWar.Sim` change
            // is needed to read it here; it also updates the instant
            // leadership actually transfers, for free. A distinct green
            // ring plus a ground-level text label -- shape and colour both
            // (docs/06's "status indicators that do not rely on colour
            // alone"), not easily confused with the gold selection halo,
            // the near-white hover highlight, or the violet/cyan
            // objective/extraction markers.
            let leaderMarkerItems : DrawItem[] =
                Casualty.currentLeader state.Agents
                |> Option.bind (fun leaderId -> currAgents |> Array.tryFind (fun a -> a.Id = leaderId))
                |> Option.map (fun a ->
                    // Both items share `renderPos`, not raw `Position` --
                    // the TASK-056 review 2 "halo jumps between cells"
                    // lesson applies identically to a second marker on a
                    // moving agent.
                    let cx, cy = renderPos a

                    [| { Kind = 5
                         TextureId = 0
                         Cx = cx
                         Cy = cy
                         Cx2 = 0.0f
                         Cy2 = 0.0f
                         Text = ""
                         R = 0.25f
                         G = 0.95f
                         B = 0.45f
                         A = 0.9f
                         Radius = agentRadius + 6.0f }
                       { Kind = 3
                         TextureId = 0
                         Cx = cx
                         Cy = cy
                         Cx2 = 0.0f
                         Cy2 = 0.0f
                         Text = "LEADER"
                         R = 0.25f
                         G = 0.95f
                         B = 0.45f
                         A = 0.9f
                         Radius = 8.0f } |])
                |> Option.defaultValue [||]

            // Three distinct route states, each its own colour (Dave's review
            // feedback: a hover is not a queued order is not a confirmed
            // one):
            //   - hover preview (yellow, dim): what a click right now would
            //     target, live under the mouse, not yet committed to anything.
            let previewItems =
                previewPath
                |> Option.map (fun cells -> routeDots cells (1.0f, 0.9f, 0.3f) 0.35f 5.0f)
                |> Option.defaultValue [||]

            //   - pending / queued (orange): a `RecordedCommand` already
            //     issued but not yet delivered to command intake -- normally
            //     one frame, but held open indefinitely while paused, which
            //     is exactly when Dave asked to still see "it is set".
            let pendingItems =
                pending
                |> Seq.choose (fun c ->
                    // `Hold`/`Assault`/`Withdraw` (TASK-048, backlog B-059)
                    // all carry a bare `Cell` target the identical shape as
                    // `MoveTo`'s (`Domain.fs`'s own "`MoveTo` precedent"
                    // doc comment) -- the client previews a route to the
                    // literal clicked cell for all four; it has no way to
                    // anticipate a `Hold` order's possible server-side
                    // `bestCoverNear` redirect before Appraisal actually
                    // runs (see `holdOutlineItems` below for that case).
                    match c.Command.Body, agentPosition c.Command.Agent with
                    | Order((MoveTo target | Hold target | Assault target | Withdraw target), _), Some pos ->
                        match Pathfinding.find state.Terrain pos target with
                        | Found(cells, _) -> Some(routeDots cells (1.0f, 0.65f, 0.15f) 0.5f 6.0f)
                        | _ -> None
                    | _ -> None)
                |> Array.concat

            //   - committed / en route (green): the order was delivered and
            //     Appraisal accepted it -- `AgentSnapshot.Destination` is
            //     populated (TASK-028's existing mechanism). The full route,
            //     not just the endpoint, so "set" reads as a path, not a dot.
            //     (this already covers `Hold`/`Assault`/`Withdraw` too, with
            //     zero change: every one of the four writes `Destination`
            //     the same way, `Commitment.fs`'s own "`Destination` exactly
            //     as `MoveTo` does" precedent -- `Assault`'s `AwaitingSupport`
            //     stage freezes it back to `None` mid-assault, which simply
            //     stops drawing a route while frozen, the correct reading.)
            let committedItems =
                currAgents
                |> Array.choose (fun a ->
                    a.Destination
                    |> Option.bind (fun d ->
                        match Pathfinding.find state.Terrain a.Position d with
                        | Found(cells, _) -> Some(routeDots cells (0.35f, 1.0f, 0.45f) 0.6f 7.0f)
                        | _ -> None))
                |> Array.concat

            // Hold-area outline (TASK-048, backlog B-059; Dave's design
            // choice: "an outline of the hold area is shown in the UI"):
            // while `Hold` is armed and an in-bounds cell is hovered with an
            // agent selected, trace the perimeter of the exact
            // `AppraisalConfig.HoldCoverSearchRadius` (2) Chebyshev square
            // `Appraisal.bestCoverNear` will actually search -- an honest
            // preview of the candidate region, not the (unknowable
            // client-side, pre-Appraisal) resolved cell itself. Four
            // `Kind = 2` line segments, the `losRay`/`FireLine` precedent for
            // a multi-cell shape with no single meaningful depth (see
            // `devItems`'s own reasoning) -- drawn unsorted after the depth
            // sort alongside `devItems`/`fireEffects` below, not folded into
            // `sorted`.
            let holdOutlineItems =
                match orderMode, selected, hoveredCell with
                | 1, Some _, Some c ->
                    let r = AppraisalConfig.HoldCoverSearchRadius
                    let nw = { X = c.X - r; Y = c.Y - r }
                    let ne = { X = c.X + r; Y = c.Y - r }
                    let se = { X = c.X + r; Y = c.Y + r }
                    let sw = { X = c.X - r; Y = c.Y + r }
                    let color = (1.0f, 0.85f, 0.2f)

                    [| RenderShared.lineMarker nw ne color 0.7f 1.5f
                       RenderShared.lineMarker ne se color 0.7f 1.5f
                       RenderShared.lineMarker se sw color 0.7f 1.5f
                       RenderShared.lineMarker sw nw color 0.7f 1.5f |]
                | _ -> [||]

            // Developer overlay (TASK-043, backlog B-029 proper): renders
            // `devFrame.Overlays` -- the identical `Diagnostics` data every
            // other developer renderer consumes -- plus a coordinate grid and
            // a hover-driven line-of-sight ray, entirely additive and gated
            // behind the `F1` toggle (off by default, matching the "with the
            // overlay off, behaviour is unchanged from TASK-042" acceptance
            // criterion).
            let devItems =
                if not devOverlay then
                    [||]
                else
                    let coordLabels =
                        [| for y in 0 .. state.Bounds.Height - 1 do
                             for x in 0 .. state.Bounds.Width - 1 do
                                 yield
                                     RenderShared.cellLabel
                                         { X = x; Y = y }
                                         (sprintf "%d,%d" x y)
                                         (0.8f, 0.8f, 0.8f)
                                         0.6f
                                         8.0f |]

                    let reservedAndObstructed =
                        devFrame.Overlays
                        |> Array.choose (function
                            | Reserved(cell, _, _) -> Some(RenderShared.cellMarker cell (0.2f, 0.9f, 0.9f) 0.45f 14.0f)
                            | Obstructed(cell, _) -> Some(RenderShared.cellMarker cell (1.0f, 0.2f, 0.2f) 0.45f 14.0f)
                            | _ -> None)

                    // Known-versus-authoritative (docs/06 section 11): a
                    // ghost marker at the friendly squad's last-known cell
                    // for each hostile contact, distinct from the real
                    // agent marker. While this `F1` overlay is on,
                    // `agentItems` deliberately bypasses the TASK-051
                    // fog-of-war gating and draws every agent at its true
                    // position unconditionally (see `agentItems`'s own
                    // comment), so this marker still visibly diverges from
                    // it once a contact's knowledge goes stale -- the
                    // ground-truth-versus-known comparison this overlay is
                    // for.
                    let knownContacts =
                        devFrame.Overlays
                        |> Array.choose (function
                            | KnownContact(cell, _, _, _) ->
                                Some(RenderShared.cellMarker cell (1.0f, 1.0f, 0.4f) 0.4f 12.0f)
                            | _ -> None)

                    // Last appraisal factors (docs/06 section 11): the
                    // selected agent's current exposed-route cells.
                    let exposedCells =
                        match selected with
                        | None -> [||]
                        | Some id ->
                            devFrame.Overlays
                            |> Array.choose (function
                                | OrderAppraisal(a, _, _, exposed) when a = id -> Some exposed
                                | _ -> None)
                            |> Array.concat
                            |> Array.map (fun cell -> RenderShared.cellMarker cell (1.0f, 0.55f, 0.0f) 0.4f 9.0f)

                    let fireLines =
                        devFrame.Overlays
                        |> Array.choose (function
                            | FireLine(_, from, _, at, hit) ->
                                let color = if hit then (0.2f, 1.0f, 0.2f) else (0.6f, 0.6f, 0.6f)
                                Some(RenderShared.lineMarker from at color 0.8f 2.0f)
                            | _ -> None)

                    let losItems =
                        match losRay with
                        | Some(from, target, visible) ->
                            let color = if visible then (0.2f, 1.0f, 0.4f) else (1.0f, 0.3f, 0.2f)
                            [| RenderShared.lineMarker from target color 0.85f 1.5f |]
                        | None -> [||]

                    Array.concat
                        [ coordLabels; reservedAndObstructed; knownContacts; exposedCells; fireLines; losItems ]

            // Player-facing fire feedback (TASK-046, backlog B-057):
            // promotes `FireLine` from developer-only (`fireLines` above) to
            // always-on -- a muzzle-flash sprite at the shooter and a
            // distinct hit-spark or miss-puff sprite at the target (Kenney
            // "Particle Pack", CC0; `art/LICENSE-THIRD-PARTY.md`), not a
            // colour-only hit/miss tint, plus a thin connecting tracer using
            // the existing line primitive. Unsorted and appended after the
            // depth sort, the `devItems` precedent immediately below: a
            // two-cell line has no single meaningful depth. Reads from
            // `heldFireLines`, not `devFrame.Overlays` directly, so each
            // effect stays visible (fading out) for `fireEffectHoldSeconds`
            // of real time rather than the single tick it actually fired.
            let fireEffects =
                heldFireLines
                |> Seq.collect (fun (from, at, hit, remaining) ->
                    let fade = float32 (remaining / fireEffectHoldSeconds)
                    let tint = if hit then (1.0f, 0.85f, 0.55f) else (0.75f, 0.75f, 0.75f)

                    [| RenderShared.lineMarker from at tint (0.5f * fade) 1.5f
                       RenderShared.effectSprite from 0 (1.0f, 1.0f, 0.9f) fade 16.0f
                       RenderShared.effectSprite at (if hit then 1 else 2) tint fade 16.0f |])
                |> Array.ofSeq

            // Audio-localised threat cue (TASK-054, backlog B-056): a short
            // inward-pointing line plus a `"!"` label at a boundary anchor
            // point outside `state.Bounds`, in the rough bearing sector of
            // an unseen hostile's shot -- never the shooter's true position
            // (`audioCueAnchor`/`stepOnce`'s own reasoning). Fractional,
            // off-grid coordinates, so built directly rather than through
            // `RenderShared.cellLabel`/`lineMarker` (both `Cell`-typed,
            // i.e. integer-only). Always-on, the `fireEffects` precedent
            // (a player-feedback item, not a developer-overlay one): dev
            // overlay exists for ground-truth comparison, not to gate
            // player-facing cues.
            let audioCueItems =
                heldAudioCues
                |> Seq.collect (fun ((ax, ay), (tx, ty), remaining) ->
                    let fade = float32 (remaining / audioCueHoldSeconds)
                    let cr, cg, cb = 1.0f, 0.45f, 0.1f

                    [| { Kind = 2
                         TextureId = 0
                         Cx = ax
                         Cy = ay
                         Cx2 = tx
                         Cy2 = ty
                         Text = ""
                         R = cr
                         G = cg
                         B = cb
                         A = 0.85f * fade
                         Radius = 2.5f }
                       { Kind = 3
                         TextureId = 0
                         Cx = ax
                         Cy = ay
                         Cx2 = 0.0f
                         Cy2 = 0.0f
                         Text = "!"
                         R = cr
                         G = cg
                         B = cb
                         A = fade
                         Radius = 14.0f } |])
                |> Array.ofSeq

            // `devItems` is deliberately appended *after* the depth sort, not
            // folded into it: `RenderShared.depthKey` derives a line's depth
            // from its origin cell alone, which is meaningless for an item
            // that spans two cells (a LOS ray, a fire line) -- sorted in, it
            // could land behind terrain partway along its own length. A
            // developer overlay exists to reveal information that might
            // otherwise be hidden, so every dev item always draws on top,
            // unsorted among themselves. `fireEffects`/`audioCueItems`
            // follow the identical reasoning for player-facing items with
            // no single meaningful depth.
            let sorted =
                Array.concat
                    [ terrainItems; coverIndicatorItems; objectiveMarkerItems; haloItems; agentItems
                      leaderMarkerItems; hoverHighlightItems; previewItems; pendingItems; committedItems ]
                |> Array.sortBy RenderShared.depthKey

            Array.concat [ sorted; fireEffects; audioCueItems; holdOutlineItems; devItems ]

        member _.HudText() =
            let selText =
                match selected with
                | Some id -> sprintf "agent %d" (AgentId.value id)
                | None -> "none"

            // Order acknowledgement/disposition + a concise refusal reason
            // for the selected agent (TASK-042, backlog B-028; docs/06
            // section 8/11), read from the held (not live) text so it stays
            // readable -- see `Update`. Omitted entirely when nothing is
            // selected, the existing selText = "none" precedent.
            let orderSuffix = if heldOrderText = "" then "" else sprintf "   order=%s" heldOrderText

            // Armed HUD order mode (TASK-048, backlog B-059): omitted
            // entirely at the default `0` (`MoveTo`), the `orderSuffix`
            // precedent -- a plain click keeps reading exactly as before
            // this task unless the player has actually armed something.
            let modeSuffix =
                match orderMode with
                | 1 -> "   mode=hold"
                | 2 -> "   mode=assault"
                | 3 -> "   mode=withdraw"
                | _ -> ""

            let line1 =
                sprintf
                    "tick %d   hash 0x%016X   draws %d   agents %d   %s   selected=%s%s%s"
                    state.Tick
                    hash
                    state.Random.Draws
                    currAgents.Length
                    (if paused then "PAUSED" else "running")
                    selText
                    orderSuffix
                    modeSuffix

            // Developer-facing commitment/suppression/stress/reason line
            // (TASK-043, backlog B-029, docs/06 section 11), gated behind the
            // `F1` overlay toggle and shown only for the selected agent (the
            // TASK-042 single-selection precedent).
            match devOverlay, selected with
            | true, Some id ->
                sprintf "%s\n[dev] %s\n%s" line1 (RenderShared.devAgentText devFrame.Overlays id) RenderShared.devLegendText
            | true, None -> sprintf "%s\n[dev] no agent selected\n%s" line1 RenderShared.devLegendText
            | false, _ -> line1

        member _.OnClick(isLeftButton: bool, cellX: int, cellY: int) =
            if not isLeftButton then
                selected <- None
                heldOrderText <- ""
                orderTextHoldRemaining <- 0.0
                losRay <- None
            else
                let cell = { X = cellX; Y = cellY }

                match friendlyAt cell with
                | Some a ->
                    selected <- Some a.Id
                    // A different agent's held message must not leak onto
                    // the newly selected one (or the same one re-clicked) --
                    // start from its own live state, not a stale hold.
                    heldOrderText <- ""
                    orderTextHoldRemaining <- 0.0
                    // Same precedent for the developer-overlay LOS ray: a
                    // stale ray from the previously selected agent must not
                    // linger until the next `OnHover`.
                    losRay <- None
                    // Selecting a Dead/Incapacitated friendly directly is
                    // allowed (status-view mode, TASK-053/B-061), but an
                    // armed order-mode icon carried over from a previous,
                    // still-`Alive` selection must not survive onto it.
                    syncOrderModeToSelection ()
                | None ->
                    match selected with
                    | Some agentId when
                        GridBounds.contains cell state.Bounds
                        && agentPosition agentId <> Some cell
                        && Casualty.isAlive (vitalsOf agentId)
                        && state.MissionOutcome = InProgress
                        ->
                        // Dispatch on the armed HUD order mode (TASK-048,
                        // backlog B-059; index 4 added by TASK-064, backlog
                        // B-035) -- `0 = MoveTo` is both the default unarmed
                        // state and an explicit icon, so a plain click with
                        // nothing armed keeps issuing `MoveTo` exactly as
                        // before this task. `4 = Suppress` targets whichever
                        // agent occupies the clicked cell instead of the
                        // bare cell itself -- `None` (no agent there) leaves
                        // Suppress armed rather than issuing a meaningless
                        // command, the "need a valid target" idiom.
                        let cmd =
                            match orderMode with
                            | 1 -> Some(Command.hold (CommandId.ofInt nextCommandId) state.Tick agentId cell)
                            | 2 -> Some(Command.assault (CommandId.ofInt nextCommandId) state.Tick agentId cell)
                            | 3 -> Some(Command.withdraw (CommandId.ofInt nextCommandId) state.Tick agentId cell)
                            | 4 ->
                                enemyAt cell
                                |> Option.map (fun target ->
                                    Command.suppress (CommandId.ofInt nextCommandId) state.Tick agentId target.Id)
                            | _ -> Some(Command.moveTo (CommandId.ofInt nextCommandId) state.Tick agentId cell)

                        match cmd with
                        | Some cmd ->
                            nextCommandId <- nextCommandId + 1

                            // Replace, not stack: an agent has at most one
                            // undelivered order at a time today (no waypoint
                            // queue yet -- see the TASK-040 review follow-up).
                            // Without this, two clicks before the next
                            // delivery tick (easy while paused) would hand
                            // Simulation.step two different commands both
                            // addressing the same agent in one tick's batch,
                            // an untested combination.
                            pending.RemoveAll(fun c -> c.Command.Agent = agentId) |> ignore

                            pending.Add
                                { Tick = state.Tick + 1L
                                  Sequence = pending.Count
                                  Command = cmd
                                  Issuer = "player" }

                            // An armed non-default mode is consumed by
                            // issuing one order (see the field comment on
                            // `orderMode`).
                            orderMode <- 0
                        | None -> ()
                    | _ -> ()

        member _.OnHover(cellX: int, cellY: int) =
            let cell = { X = cellX; Y = cellY }
            hoveredCell <- if GridBounds.contains cell state.Bounds then Some cell else None

            // Suppressed while the selected agent is not `Alive` (TASK-053,
            // backlog B-061): previewing a route implies a click there
            // would move it, which is no longer true once it is
            // Dead/Incapacitated -- see `OnClick`'s matching order-issue
            // guard.
            //
            // TASK-064 review (backlog B-035): live-testing on Bridgehead,
            // Dave reported issuing orders across the bridge that "seemed
            // to register" but produced no visible movement or combat.
            // Root cause: a `MoveTo` order (`orderMode = 0`) for a
            // formationed agent (any non-zero `AgentState.FormationOffset`
            // -- every slot but each fireteam's own leader) does not target
            // the clicked cell at all; `Appraisal.resolveFormationTarget`
            // resolves the *real* destination sim-side, which can land far
            // short of what was clicked (its own bounded-radius fallback,
            // TASK-059) with no client-side indication this happened --
            // the agent then reports `Accepted` and genuinely arrives, just
            // not where the player thought. Previewing the same resolution
            // here, using the identical `occupied`/tie-break inputs
            // `Simulation.appraisal` itself uses (`Simulation.fs`'s own
            // `occupied` computation, mirrored), means the hover route now
            // shows the truth before the player commits to a click. `Hold`/
            // `Assault`/`Withdraw`/`Suppress` are unaffected: only `MoveTo`
            // resolves through `resolveFormationTarget` at all
            // (`Appraisal.appraise`'s own `Intent` match).
            previewPath <-
                match selected with
                | Some id when Casualty.isAlive (vitalsOf id) ->
                    agentPosition id
                    |> Option.bind (fun pos ->
                        let target =
                            if orderMode = 0 then
                                match state.Agents |> Array.tryFind (fun a -> a.Id = id) with
                                | Some agent ->
                                    let occupied =
                                        state.Agents
                                        |> Array.choose (fun a -> if a.Id = id then None else Some a.Position)

                                    Appraisal.resolveFormationTarget state.Terrain occupied agent.FormationOffset cell
                                | None -> cell
                            else
                                cell

                        match Pathfinding.find state.Terrain pos target with
                        | Found(cells, _) -> Some cells
                        | NoPath
                        | BudgetExhausted _
                        | InvalidEndpoint _ -> None)
                | _ -> None

            // Developer-overlay line of sight and occluders (TASK-043):
            // traced from the selected agent to the hovered cell regardless
            // of whether the overlay is currently shown -- cheap, and keeps
            // `losRay` correct the instant `F1` is pressed rather than one
            // hover-move stale. A blocked trace ends at its own `Blocker`
            // cell, not the hovered cell, so the ray visibly stops at the
            // occluder rather than passing through it in red. Gated on
            // `GridBounds.contains` (the `OnClick` precedent): `Sight.trace`
            // treats an out-of-bounds endpoint as simply not visible with no
            // `Blocker`, which without this check drew a ray chasing the
            // mouse arbitrarily far off the map the moment the cursor left
            // the grid (Dave's review feedback).
            losRay <-
                if not (GridBounds.contains cell state.Bounds) then
                    None
                else
                    selected
                    |> Option.bind agentPosition
                    |> Option.map (fun pos ->
                        let los = Sight.trace state.Terrain pos cell
                        let endCell = if los.Visible then cell else los.Blocker |> Option.defaultValue cell
                        pos, endCell, los.Visible)

        member _.OnTogglePause() = paused <- not paused
        member _.OnToggleDevOverlay() = devOverlay <- not devOverlay

        // A HUD order-mode icon click (TASK-048, backlog B-059): clicking
        // the already-armed icon disarms back to `0` (`MoveTo`) -- an
        // explicit way to cancel an armed order without issuing one, the
        // XCOM "click the ability again to cancel" idiom.
        member _.OnOrderModeClick(index: int) = orderMode <- (if orderMode = index then 0 else index)
        member _.OrderMode() = orderMode

        // Mission summary panel (TASK-063, backlog B-033 narrowed): reads
        // the live `state` directly (`RenderShared.missionSummaryLines`
        // returns `[||]` while still `InProgress`), the same "read
        // supplementary state once per frame" shape `OrderMode` above
        // already establishes.
        member _.MissionSummaryLines() = RenderShared.missionSummaryLines state

        member _.Dispose() = ()

    /// Steps exactly `count` ticks with no wall clock involved -- the
    /// scripted headless self-check's pacing, distinct from `Update`'s
    /// frame-delta accumulator.
    member _.StepTicksHeadless(count: int64) : TickHash[] =
        [| for _ in 1L .. count do
             stepOnce ()
             yield { Tick = state.Tick; Hash = hash } |]

/// The scripted headless self-check driver for `CommandDemoScene`
/// (`--selfcheck`'s evidence path, the `DemoDrive.runFullSequence`
/// precedent) -- exercises real `OnClick`/`OnHover` input mapping instead of
/// a canned command log.
[<RequireQualifiedAccess>]
module CommandDemoDrive =

    /// TASK-064 (backlog B-035): drives all six friendly agents from their
    /// own authored `bridgehead.cwscenario` starting cells toward the
    /// bridge, exercising formation-slot resolution (TASK-059) for all six
    /// at once against real terrain rather than a hand-built fixture.
    /// Investigated live (a temporary `dotnet fsi` probe against this exact
    /// scene/content, removed after use, the TASK-042/051/052/053
    /// precedent) before settling on this sequence: sending every agent at
    /// once gives the machine-gun team (`AgentId 100`) more simultaneous
    /// targets than a lone agent would, so real automatic engagement
    /// (TASK-031/032) brings it down to `Dead` (through
    /// `Incapacitated`/bleed-out) while every friendly agent stays `Alive`
    /// -- a real, reproducible, casualty-free neutralisation of the
    /// scenario's central threat through the click path, not a direct
    /// sim-side call. `ObjectiveId 1` (the optional `reach observation`
    /// objective) also completes for free, since agent 0's own route
    /// passes through (4,4).
    ///
    /// This does not reach `MissionOutcome <> InProgress`: reaching the
    /// `destroy`/`extract` objectives (`ObjectiveId 2`/`3`) turned out to
    /// need more than "the machine gun is dead" -- something else (most
    /// likely one of the depot riflemen, `AgentId 101`-`104`) also has a
    /// clear shot at the `bridge-charge` target cell (9,5) itself, which
    /// the same investigation found but did not resolve within this task
    /// (see the task file's own findings; flagged for Dave, not silently
    /// dropped). A friendly agent's own corpse also turned out to make a
    /// cell permanently unenterable (an occupied cell is never vacated,
    /// `Simulation.navigationAndMovement`'s vacation-chain rule, and a
    /// dead friendly's cell is still `friendlyAt`-selectable, so a click
    /// there re-selects it for status view rather than ever issuing a new
    /// order) -- avoided here by never routing an agent's *final*
    /// destination onto (9,5)/(9,6) themselves, only adjacent to them.
    let runScriptedSelfCheck (scenarioContentPath: string) : TickHash[] =
        let scene = CommandDemoScene()
        let asScene = scene :> IClientScene
        asScene.Ready(scenarioContentPath)

        // Each click pair is (select at the agent's own authored starting
        // cell, target cell) -- formation-slot offsets (TASK-059) resolve
        // each agent's *actual* destination from here, not the literal
        // clicked cell; see the task file for the offset arithmetic behind
        // each choice.
        let order (selectCell: Cell) (targetCell: Cell) =
            asScene.OnClick(true, selectCell.X, selectCell.Y)
            asScene.OnHover(targetCell.X, targetCell.Y)
            asScene.OnClick(true, targetCell.X, targetCell.Y)

        order { X = 3; Y = 5 } { X = 8; Y = 5 } // agent 0 (fireteam-alpha, slot 0)
        order { X = 2; Y = 5 } { X = 7; Y = 5 } // agent 1 (fireteam-alpha, slot 1)
        order { X = 2; Y = 6 } { X = 8; Y = 5 } // agent 2 (fireteam-alpha, slot 2)
        order { X = 3; Y = 7 } { X = 9; Y = 6 } // agent 3 (fireteam-bravo, slot 0)
        order { X = 4; Y = 7 } { X = 9; Y = 6 } // agent 4 (fireteam-bravo, slot 1)
        order { X = 4; Y = 8 } { X = 8; Y = 6 } // agent 5 (fireteam-bravo, slot 2)

        scene.StepTicksHeadless(90L)
