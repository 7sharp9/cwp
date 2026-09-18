namespace CwClientCore

open CommandoWar.Sim
open CommandoWar.Headless

/// Live selection, input-mapped `MoveTo` orders, a real-`Pathfinding.find`
/// route preview, and tactical pause over `DemoScenario` (TASK-040, backlog
/// B-026) -- the first scene where the player, not a canned command log,
/// drives `Simulation.step`. Reuses TASK-039's terrain-item/depth-sort
/// helpers (`RenderShared`) rather than duplicating them.
type CommandDemoScene() =
    let simHz = 20.0
    let maxCatchUpStepsPerFrame = 5

    let mutable state = Unchecked.defaultof<WorldState>
    let mutable prevAgents: Map<int, Cell> = Map.empty
    let mutable currAgents: AgentSnapshot[] = [||]
    let mutable terrainItems: DrawItem[] = [||]
    let mutable accum = 0.0
    let mutable alpha = 0.0
    let mutable hash = 0UL
    let mutable paused = false
    let mutable selected: AgentId option = None
    let mutable previewPath: Cell[] option = None
    let mutable hoveredCell: Cell option = None

    // XCOM-style HUD order-mode icons (TASK-048, backlog B-059): which order
    // type the next non-agent left-click issues -- `0 = MoveTo` (the
    // default, unarmed state), `1 = Hold`, `2 = Assault`, `3 = Withdraw`.
    // Armed by `OnOrderModeClick` (a HUD-icon click, resolved by the C#
    // host's fixed icon rects, the `OnClick`/`ScreenToCell` "primitives
    // only" precedent); consumed and reset back to `0` the instant an order
    // is actually issued (an XCOM ability-consumed-on-use idiom), so the
    // player re-arms explicitly for each non-default order rather than it
    // silently staying armed across multiple orders.
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

    /// One authoritative step consuming any orders queued for delivery at
    /// `state.Tick + 1`. Used by both `Update` (wall-clock-paced) and the
    /// scripted headless self-check (paced by direct calls, no wall clock).
    let stepOnce () =
        let r = Simulation.step SimConfig.standard (commandsForTick (state.Tick + 1L)) state
        prevAgents <- currAgents |> Array.map (fun a -> AgentId.value a.Id, a.Position) |> Map.ofArray
        state <- r.State
        currAgents <- r.Snapshot.Agents
        hash <- r.StateHash.Value
        devFrame <- Diagnostics.frameOf r

        for overlay in devFrame.Overlays do
            match overlay with
            | FireLine(_, from, _, at, hit) -> heldFireLines.Add(from, at, hit, fireEffectHoldSeconds)
            | _ -> ()

    let friendlyAt (cell: Cell) : AgentSnapshot option =
        currAgents |> Array.tryFind (fun a -> a.Side = Friendly && a.Position = cell)

    let agentPosition (id: AgentId) : Cell option =
        currAgents |> Array.tryFind (fun a -> a.Id = id) |> Option.map (fun a -> a.Position)

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
        member _.Ready() =
            state <- DemoScenario.initialState ()
            terrainItems <- RenderShared.buildTerrainItems state.Terrain
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

        member _.Update(deltaSeconds: float) =
            if not paused then
                let simStep = 1.0 / simHz
                accum <- accum + deltaSeconds
                let mutable steps = 0

                while accum >= simStep && steps < maxCatchUpStepsPerFrame do
                    stepOnce ()
                    accum <- accum - simStep
                    steps <- steps + 1

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

            // Hold a meaningful order-disposition message on screen for at
            // least `orderTextHoldSeconds` after it appears, even once the
            // underlying `Disposition` clears (order fulfilled) -- see the
            // field comment above. A genuinely new message (a fresh order,
            // or a reappraisal flipping the outcome) always overrides
            // immediately; only the fall-back to "no order" is delayed.
            let liveOrderText =
                selected
                |> Option.bind (fun id -> currAgents |> Array.tryFind (fun a -> a.Id = id))
                |> Option.map (fun a -> RenderShared.dispositionText a.Disposition)
                |> Option.defaultValue ""

            if liveOrderText <> "" && liveOrderText <> "no order" then
                heldOrderText <- liveOrderText
                orderTextHoldRemaining <- orderTextHoldSeconds
            elif orderTextHoldRemaining > 0.0 then
                orderTextHoldRemaining <- max 0.0 (orderTextHoldRemaining - deltaSeconds)
            else
                heldOrderText <- liveOrderText

        member _.DrawList() =
            let lerp (a: int) (b: int) (t: float) = float32 a + (float32 (b - a)) * float32 t

            // Player-facing casualty markers (TASK-046, backlog B-057):
            // promotes `AgentVitals` from developer-only to always-on --
            // `devFrame` is recomputed every tick regardless of the F1
            // dev-overlay toggle (see its own field comment), so the data
            // already exists. Mirrors `DiagnosticRender.Svg`'s own vitals
            // vocabulary (a wound dot, a status badge, a dead cross) rather
            // than a colour-only recolour of the agent figure (docs/06
            // "status indicators that do not rely on colour alone").
            let agentItems =
                currAgents
                |> Array.collect (fun a ->
                    let vitals =
                        devFrame.Overlays
                        |> Array.tryPick (function
                            | AgentVitals(id, _, v) when id = a.Id -> Some v
                            | _ -> None)
                        |> Option.defaultValue (Alive Agent.MaxHealth)

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
                        // -- the badge, not just the tint, is the signal.
                        let r, g, b = RenderShared.agentColor a.Side

                        [| { Kind = 1
                             TextureId = 0
                             Cx = float32 a.Position.X
                             Cy = float32 a.Position.Y
                             Cx2 = 0.0f
                             Cy2 = 0.0f
                             Text = ""
                             R = r * 0.5f
                             G = g * 0.5f
                             B = b * 0.5f
                             A = 1.0f
                             Radius = 10.0f }
                           RenderShared.cellLabel a.Position "down" (0.9f, 0.9f, 0.9f) 0.9f 9.0f |]
                    | Alive health ->
                        let from = prevAgents |> Map.tryFind (AgentId.value a.Id) |> Option.defaultValue a.Position
                        let r, g, b = RenderShared.agentColor a.Side

                        let figure =
                            { Kind = 1
                              TextureId = 0
                              Cx = lerp from.X a.Position.X alpha
                              Cy = lerp from.Y a.Position.Y alpha
                              Cx2 = 0.0f
                              Cy2 = 0.0f
                              Text = ""
                              R = r
                              G = g
                              B = b
                              A = 1.0f
                              Radius = 10.0f }

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
                               RenderShared.cellMarker a.Position (0.9f, 0.15f, 0.1f) (0.4f + severity * 0.5f) 4.0f |])

            // Selection halo: a larger, translucent Kind = 1 item at the
            // selected agent's own cell, inserted before its real circle so
            // the stable depth-sort tie-break draws it underneath.
            let haloItems =
                match selected |> Option.bind agentPosition with
                | Some pos ->
                    [| { Kind = 1
                         TextureId = 0
                         Cx = float32 pos.X
                         Cy = float32 pos.Y
                         Cx2 = 0.0f
                         Cy2 = 0.0f
                         Text = ""
                         R = 1.0f
                         G = 0.95f
                         B = 0.30f
                         A = 0.35f
                         Radius = 17.0f } |]
                | None -> [||]

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
                    // ghost marker at the friendly squad's last-known cell for
                    // each hostile contact, distinct from the real agent
                    // marker (`agentItems` above draws every agent, including
                    // hostiles, at its true position unconditionally -- this
                    // demo has no fog-of-war -- so the two markers visibly
                    // diverge once a contact's knowledge goes stale).
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

            // `devItems` is deliberately appended *after* the depth sort, not
            // folded into it: `RenderShared.depthKey` derives a line's depth
            // from its origin cell alone, which is meaningless for an item
            // that spans two cells (a LOS ray, a fire line) -- sorted in, it
            // could land behind terrain partway along its own length. A
            // developer overlay exists to reveal information that might
            // otherwise be hidden, so every dev item always draws on top,
            // unsorted among themselves. `fireEffects` follows the identical
            // reasoning for the same underlying overlay, now player-facing.
            let sorted =
                Array.concat [ terrainItems; haloItems; agentItems; previewItems; pendingItems; committedItems ]
                |> Array.sortBy RenderShared.depthKey

            Array.concat [ sorted; fireEffects; holdOutlineItems; devItems ]

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
                | None ->
                    match selected with
                    | Some agentId when GridBounds.contains cell state.Bounds && agentPosition agentId <> Some cell ->
                        // Dispatch on the armed HUD order mode (TASK-048,
                        // backlog B-059) -- `0 = MoveTo` is both the default
                        // unarmed state and an explicit icon, so a plain
                        // click with nothing armed keeps issuing `MoveTo`
                        // exactly as before this task.
                        let cmd =
                            match orderMode with
                            | 1 -> Command.hold (CommandId.ofInt nextCommandId) state.Tick agentId cell
                            | 2 -> Command.assault (CommandId.ofInt nextCommandId) state.Tick agentId cell
                            | 3 -> Command.withdraw (CommandId.ofInt nextCommandId) state.Tick agentId cell
                            | _ -> Command.moveTo (CommandId.ofInt nextCommandId) state.Tick agentId cell

                        nextCommandId <- nextCommandId + 1

                        // Replace, not stack: an agent has at most one
                        // undelivered order at a time today (no waypoint
                        // queue yet -- see the TASK-040 review follow-up).
                        // Without this, two clicks before the next delivery
                        // tick (easy while paused) would hand Simulation.step
                        // two different commands both addressing the same
                        // agent in one tick's batch, an untested combination.
                        pending.RemoveAll(fun c -> c.Command.Agent = agentId) |> ignore

                        pending.Add
                            { Tick = state.Tick + 1L
                              Sequence = pending.Count
                              Command = cmd
                              Issuer = "player" }

                        // An armed non-default mode is consumed by issuing
                        // one order (see the field comment on `orderMode`).
                        orderMode <- 0
                    | _ -> ()

        member _.OnHover(cellX: int, cellY: int) =
            let cell = { X = cellX; Y = cellY }
            hoveredCell <- if GridBounds.contains cell state.Bounds then Some cell else None

            previewPath <-
                selected
                |> Option.bind agentPosition
                |> Option.bind (fun pos ->
                    match Pathfinding.find state.Terrain pos cell with
                    | Found(cells, _) -> Some cells
                    | NoPath
                    | BudgetExhausted _
                    | InvalidEndpoint _ -> None)

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

    /// Selects friendly agent 0 (at (0,0)), previews and issues a short
    /// `MoveTo(3,0)` clear of the ridge and the impassable block; then
    /// selects friendly agent 1 (at (0,1)), arms the `Hold` HUD order mode
    /// (TASK-048, backlog B-059, the same `OnOrderModeClick` an icon click
    /// resolves to) and issues `Hold(2,1)` -- proving the order-mode icon
    /// dispatch reaches a real, non-`MoveTo` `Command.hold` through the
    /// identical click path a player uses, not just a direct sim-side call
    /// (the TASK-037/047 `SimulationTests`-only precedent already proves the
    /// sim side; this proves the click wiring on top of it). Then steps the
    /// full `DemoScenario` run.
    let runScriptedSelfCheck () : TickHash[] =
        let scene = CommandDemoScene()
        let asScene = scene :> IClientScene
        asScene.Ready()
        asScene.OnClick(true, 0, 0)
        asScene.OnHover(3, 0)
        asScene.OnClick(true, 3, 0)
        asScene.OnClick(true, 0, 1)
        asScene.OnOrderModeClick(1)
        asScene.OnHover(2, 1)
        asScene.OnClick(true, 2, 1)
        scene.StepTicksHeadless(DemoScenario.TickCount)
