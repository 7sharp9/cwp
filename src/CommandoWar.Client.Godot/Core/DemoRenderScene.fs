namespace CwClientCore

open CommandoWar.Sim
open CommandoWar.Headless

/// Shared, framework-neutral stepping helpers over `DemoScenario` (TASK-039,
/// backlog B-027): the confirmed first live-render source (an elevation
/// ridge, an impassable block, a movement-cost patch, an opaque wall, and
/// directional cover -- real terrain relief for depth ordering to prove
/// itself against). No `CommandoWar.Sim`/`CommandoWar.Headless` change: this
/// only drives the existing `Simulation.step` / `RenderSnapshot` contract,
/// the `DiagnosticRender.runFrames` per-tick pattern minus the
/// diagnostic/overlay machinery (production rendering reads `RenderSnapshot`,
/// never `DiagnosticFrame` -- diagnostics are observers only, ADR-0002).
[<RequireQualifiedAccess>]
module DemoDrive =

    let private commandsForTick (log: RecordedCommand[]) (t: int64) =
        log |> Array.filter (fun c -> c.Tick = t) |> Array.map (fun c -> c.Command)

    /// One authoritative step: `state` -> the next tick's `WorldState` plus
    /// its `RenderSnapshot` and state hash.
    let stepOnce (log: RecordedCommand[]) (state: WorldState) : WorldState * RenderSnapshot * uint64 =
        let r = Simulation.step SimConfig.standard (commandsForTick log (state.Tick + 1L)) state
        r.State, r.Snapshot, r.StateHash.Value

    /// The full deterministic run, headless -- `--selfcheck`'s evidence.
    /// `TickHash` (not a tuple: ADR-0004 forbids exposing F# tuples to C#).
    let runFullSequence () : TickHash[] =
        let log = DemoScenario.commandLog ()
        let mutable state = DemoScenario.initialState ()

        [| for _ in 1L .. DemoScenario.TickCount do
             let next, _, h = stepOnce log state
             state <- next
             yield { Tick = state.Tick; Hash = h } |]

/// Live-steps `DemoScenario` and exposes a depth-sorted `DrawItem[]` each
/// frame. No player input, no content import (see the task file's forbidden
/// scope) -- the scene runs unattended once launched.
type DemoRenderScene() =
    // Fixed-step scheduling constants -- the `MainNode.cs` disposable-spike
    // precedent (`src/CommandoWar.Client.Godot/src/MainNode.cs`).
    let simHz = 20.0
    let maxCatchUpStepsPerFrame = 5

    let log = DemoScenario.commandLog ()

    let mutable state = Unchecked.defaultof<WorldState>
    let mutable prevAgents: Map<int, Cell> = Map.empty
    let mutable currAgents: AgentSnapshot[] = [||]
    let mutable terrainItems: DrawItem[] = [||]
    let mutable accum = 0.0
    let mutable alpha = 0.0
    let mutable hash = 0UL

    // Agent-facing bins (TASK-054, backlog B-052), one authoritative tick at
    // a time -- not per render frame, so a stationary agent's facing does
    // not get recomputed (and trivially reconfirmed) on every `_Draw` call.
    let mutable facing: Map<int, int> = Map.empty

    // Run-cycle animation clock (TASK-056, backlog B-052): this scene has no
    // tactical pause (unlike `CommandDemoScene`, it steps unattended once
    // launched), so real elapsed time and "ticks are advancing" already
    // coincide -- advanced unconditionally in `Update` below.
    let mutable runClock = 0.0

    // TASK-056 review round 1: the `CommandDemoScene.nextStepCell`/
    // `edgeTickEstimate`/`prevProgress` precedent (its own field comment
    // has the full root-cause explanation) -- correct facing and smooth,
    // continuous position across a whole multi-tick edge, applied
    // identically here since both scenes share the one `FSharpSceneHost`
    // draw path.
    let mutable nextStepCell: Map<int, Cell> = Map.empty
    let mutable prevProgress: Map<int, int> = Map.empty
    let defaultEdgeTickEstimate = 2
    let mutable edgeTickEstimate: Map<int, int> = Map.empty

    // TASK-056 review round 2 (backlog B-052): the `CommandDemoScene.
    // stalled` precedent (its own field comment has the full root-cause
    // explanation) -- an agent whose route is stalled by a reservation
    // contest keeps an unmet `Destination` with a frozen `Progress`, which
    // without this both sawtoothed the render-time lerp and kept the
    // run-cycle animation playing forever on a stationary figure. Applied
    // identically here since both scenes share the one `FSharpSceneHost`
    // draw path.
    let mutable stalled: Map<int, bool> = Map.empty

    let advanceOneTick () =
        let next, snapshot, h = DemoDrive.stepOnce log state
        prevAgents <- currAgents |> Array.map (fun a -> AgentId.value a.Id, a.Position) |> Map.ofArray
        let priorProgress = prevProgress
        state <- next
        currAgents <- snapshot.Agents
        hash <- h

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

        // TASK-056 review round 2: see the field comment on `stalled`.
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

    interface IClientScene with
        // `scenarioContentPath` is irrelevant here (TASK-064): this scene's
        // whole purpose is exercising the diagnostic renderers against
        // `DemoScenario`'s own hand-built terrain, not real mission content.
        member _.Ready(_scenarioContentPath: string) =
            state <- DemoScenario.initialState ()
            terrainItems <- RenderShared.buildTerrainItems state.Terrain
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

            // TASK-056 review round 2: the `CommandDemoScene` precedent --
            // seeds `stalled`'s first-tick comparison from each agent's real
            // starting `Progress` instead of an empty-map `-1` sentinel.
            prevProgress <- currAgents |> Array.map (fun a -> AgentId.value a.Id, a.Progress) |> Map.ofArray

        member _.Update(deltaSeconds: float) =
            if state.Tick < DemoScenario.TickCount then
                let simStep = 1.0 / simHz
                accum <- accum + deltaSeconds
                let mutable steps = 0

                while accum >= simStep && steps < maxCatchUpStepsPerFrame && state.Tick < DemoScenario.TickCount do
                    advanceOneTick ()
                    accum <- accum - simStep
                    steps <- steps + 1

                runClock <- runClock + deltaSeconds

            alpha <- System.Math.Clamp(accum * simHz, 0.0, 1.0)

        member _.DrawList() =
            let lerp (a: int) (b: int) (t: float) = float32 a + (float32 (b - a)) * float32 t

            let agentItems =
                currAgents
                |> Array.map (fun a ->
                    let id = AgentId.value a.Id
                    let r, g, b = RenderShared.agentColor a.Side
                    let facingBin = facing |> Map.tryFind id |> Option.defaultValue 0

                    // TASK-056, backlog B-052: the `CommandDemoScene.
                    // renderVitals` `Alive`-branch precedent (this scene
                    // does not track vitals at all -- a pre-existing,
                    // unaffected gap, since `DemoScenario` has no combat
                    // within its 20-tick run).
                    // TASK-056 review round 2: also freezes while `stalled`
                    // -- see the field comment on `stalled`.
                    let isStalled = stalled |> Map.tryFind id |> Option.defaultValue false
                    let isMoving = (a.Destination |> Option.exists (fun d -> d <> a.Position)) && not isStalled
                    let runFrame = if isMoving then RenderShared.runFrameIndex runClock else -1

                    // TASK-056 review round 1/2: the `CommandDemoScene`
                    // precedent -- smooth, continuous position across a
                    // whole multi-tick edge instead of a single-tick snap,
                    // frozen (no `alpha` credit) instead of sawtoothing
                    // while genuinely stalled.
                    let target = nextStepCell |> Map.tryFind id |> Option.defaultValue a.Position
                    let estTicks = edgeTickEstimate |> Map.tryFind id |> Option.defaultValue defaultEdgeTickEstimate |> max 1

                    let edgeFrac =
                        if isStalled then
                            System.Math.Clamp(float a.Progress / float estTicks, 0.0, 1.0)
                        else
                            System.Math.Clamp((float a.Progress + alpha) / float estTicks, 0.0, 1.0)

                    { Kind = 1
                      TextureId = facingBin
                      Cx = lerp a.Position.X target.X edgeFrac
                      Cy = lerp a.Position.Y target.Y edgeFrac
                      Cx2 = float32 runFrame
                      Cy2 = 0.0f
                      Text = ""
                      R = r
                      G = g
                      B = b
                      A = 1.0f
                      Radius = 10.0f })

            Array.append terrainItems agentItems |> Array.sortBy RenderShared.depthKey

        member _.HudText() =
            sprintf
                "tick %d / %d   hash 0x%016X   agents %d"
                state.Tick
                DemoScenario.TickCount
                hash
                currAgents.Length

        // No input this scene (TASK-039's task file forbids it -- unattended
        // once launched). Real handling is CommandDemoScene's job (TASK-040).
        member _.OnClick(_isLeftButton: bool, _cellX: int, _cellY: int, _shiftHeld: bool) = ()
        member _.OnHover(_cellX: int, _cellY: int) = ()
        member _.OnDragSelect(_cellXs: int[], _cellYs: int[], _shiftHeld: bool) = ()
        member _.OnTogglePause() = ()
        member _.OnToggleDevOverlay() = ()
        member _.OnOrderModeClick(_index: int) = ()
        member _.OrderMode() = 0
        member _.MissionSummaryLines() = [||]

        member _.Dispose() = ()
