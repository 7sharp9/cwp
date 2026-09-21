namespace CwClientCore

open CommandoWar.Sim
open CommandoWar.Headless

/// Loads and reconstructs a production `.cwreplay` file (TASK-071, backlog
/// B-064) through the exact pipeline `cwheadless replay-file`
/// (`src/CommandoWar.Headless/Program.fs`, `cmdReplayFile`) already uses for
/// verification -- proving this scene's own use of it is the same
/// contract, not a bespoke reimplementation. No `CommandoWar.Sim`/
/// `CommandoWar.Headless` change: `ReplaySerialisation`/`Replay` are
/// consumed exactly as published.
[<RequireQualifiedAccess>]
module ReplayDrive =

    /// Resolves a `.cwreplay` file's named scenario against `Corpus.all` --
    /// the `Program.resolveScenario` pattern (`private` there, so
    /// re-derived here rather than exposed across the assembly boundary,
    /// the `RenderShared.reasonText`/`devReasonText` "two audiences, two
    /// call sites" precedent).
    let private resolveScenario (name: string) : (unit -> WorldState) option =
        Corpus.all
        |> Array.tryFind (fun e -> e.Name = name)
        |> Option.map (fun e -> e.InitialState)

    /// What `Ready` loaded, or why it could not.
    type LoadResult =
        | Loaded of scenario: string * initial: WorldState * outcome: ReplayOutcome
        | LoadFailed of message: string

    /// Parses, resolves, and runs a `.cwreplay` file end to end. A
    /// malformed file, an unreadable path, an unresolvable scenario name,
    /// an initial-state hash mismatch, or a `Replay.run` failure all
    /// surface as `LoadFailed` with a readable message rather than an
    /// unhandled exception -- this is a scene `Ready` call, which has no
    /// `eprintfn`/exit-code error channel the way `cwheadless replay-file`
    /// does.
    let load (path: string) : LoadResult =
        let text =
            try
                Ok(System.IO.File.ReadAllText path)
            with ex ->
                Error(sprintf "cannot read '%s': %s" path ex.Message)

        match text with
        | Error m -> LoadFailed m
        | Ok text ->
            match ReplaySerialisation.parse text with
            | Error e -> LoadFailed(sprintf "parse error in '%s': %s" path (ReplaySerialisation.describeError e))
            | Ok file ->
                match resolveScenario file.Meta.Scenario with
                | None -> LoadFailed(sprintf "unknown scenario '%s' (not in Corpus.all)" file.Meta.Scenario)
                | Some build ->
                    let initial = build ()
                    let initialHash = (Hashing.hash initial).Value

                    match file.InitialHash with
                    | Some pinned when pinned <> initialHash ->
                        LoadFailed(
                            sprintf
                                "initial-state hash mismatch for '%s': file pins 0x%016X, scenario '%s' builds 0x%016X"
                                path
                                pinned
                                file.Meta.Scenario
                                initialHash
                        )
                    | _ ->
                        let record = ReplaySerialisation.toReplayRecord initial file

                        match Replay.run SimConfig.standard record with
                        | Error e -> LoadFailed(sprintf "replay error in '%s': %A" path e)
                        | Ok outcome -> Loaded(file.Meta.Scenario, initial, outcome)

/// A read-only scrubber over a recorded `.cwreplay` file (TASK-071, backlog
/// B-064): `Ready` loads and runs the whole replay once via `ReplayDrive.
/// load` (central decision 3 -- reuses `Replay.run`'s existing full
/// `TickStates: WorldState[]` as-is, no checkpoint-cadence mechanism), then
/// every tick's authoritative `WorldState` is already in memory and the
/// scene only ever indexes into it. `states.[0]` is the initial (tick 0)
/// state; `states.[i]` for `i > 0` is `outcome.TickStates.[i - 1]` (`
/// TickStates` itself never includes tick 0, `Replay.fs`'s own contract).
/// No order can be issued: `OnClick`/`OnHover`/`OnDragSelect`/
/// `OnOrderModeClick` are true no-ops, a strict viewer, never a second
/// live-play surface. `Simulation.step` is never called here.
type ReplayDemoScene() =
    let mutable terrainItems: DrawItem[] = [||]
    let mutable states: WorldState[] = [||]
    let mutable hashes: uint64[] = [||]
    let mutable scenarioName = ""
    let mutable errorText: string option = None
    let mutable currentTick = 0L
    let mutable playing = false
    let mutable accum = 0.0

    // "Play" advance rate: one recorded tick per this many wall-clock
    // seconds -- the `DemoRenderScene`/`CommandDemoScene` `simHz`
    // constant-naming precedent, though here it paces the scrub index
    // forward through already-computed `states`, never `Simulation.step`.
    let playHz = 4.0

    let tickCount () = if states.Length = 0 then 0L else int64 states.Length - 1L

    interface IClientScene with
        // `scenarioContentPath` carries a `.cwreplay` file path for this
        // scene specifically (TASK-071's own documented reuse of the
        // parameter -- see the task file's Inputs and assumptions), not a
        // `.cwscenario` the way `CommandDemoScene`/`DemoRenderScene` read
        // it.
        member _.Ready(replayFilePath: string) =
            match ReplayDrive.load replayFilePath with
            | ReplayDrive.LoadFailed msg -> errorText <- Some msg
            | ReplayDrive.Loaded(name, initial, outcome) ->
                scenarioName <- name
                terrainItems <- RenderShared.buildTerrainItems initial.Terrain
                states <- Array.append [| initial |] outcome.TickStates

                hashes <-
                    Array.append
                        [| (Hashing.hash initial).Value |]
                        (outcome.TickHashes |> Array.map (fun c -> c.Hash.Value))

                currentTick <- 0L

        member _.Update(deltaSeconds: float) =
            if playing then
                let total = tickCount ()

                if total = 0L || currentTick >= total then
                    playing <- false
                    accum <- 0.0
                else
                    accum <- accum + deltaSeconds
                    let stepSeconds = 1.0 / playHz

                    while accum >= stepSeconds && currentTick < total do
                        currentTick <- currentTick + 1L
                        accum <- accum - stepSeconds

                    if currentTick >= total then
                        playing <- false
                        accum <- 0.0

        member _.DrawList() =
            if errorText.IsSome || states.Length = 0 then
                [||]
            else
                let state = states.[int currentTick]

                let agentItems =
                    state.Agents
                    |> Array.collect (fun a ->
                        let r, g, b = RenderShared.agentColor a.Side

                        match a.Vitals with
                        | Dead -> RenderShared.deadCross a.Position
                        | Incapacitated _ -> [| RenderShared.cellMarker a.Position (r, g, b) 0.5f 20.0f |]
                        | Alive _ ->
                            // No inter-tick interpolation and no
                            // continuity-tracked "previous facing":
                            // scrubbing can jump to any tick in either
                            // direction, so there is no well-defined "last
                            // active-movement facing" to freeze on the way
                            // a continuously-live scene has. Each tick's
                            // facing is derived fresh from that tick's own
                            // Position/Destination alone (`prevBin = 0`
                            // when at rest) -- a deliberate simplification
                            // for a read-only viewer, not a defect.
                            [| { Kind = 1
                                 TextureId = RenderShared.facingBin 0 a.Position a.Destination
                                 Cx = float32 a.Position.X
                                 Cy = float32 a.Position.Y
                                 Cx2 = -1.0f
                                 Cy2 = 0.0f
                                 Text = ""
                                 R = r
                                 G = g
                                 B = b
                                 A = 1.0f
                                 Radius = 20.0f } |])

                Array.append terrainItems agentItems |> Array.sortBy RenderShared.depthKey

        member _.HudText() =
            match errorText with
            | Some msg -> sprintf "replay load FAILED: %s" msg
            | None ->
                sprintf
                    "replay %s   tick %d / %d   hash 0x%016X   agents %d   %s"
                    scenarioName
                    currentTick
                    (tickCount ())
                    hashes.[int currentTick]
                    states.[int currentTick].Agents.Length
                    (if playing then "PLAYING" else "PAUSED")

        // A pure, read-only viewer: no order can be issued from this
        // scene (TASK-071's forbidden scope).
        member _.OnClick(_isLeftButton: bool, _cellX: int, _cellY: int, _shiftHeld: bool) = ()
        member _.OnHover(_cellX: int, _cellY: int) = ()
        member _.OnDragSelect(_cellXs: int[], _cellYs: int[], _shiftHeld: bool) = ()
        member _.OnOrderModeClick(_index: int) = ()
        member _.OrderMode() = 0
        member _.OnToggleDevOverlay() = ()
        member _.MissionSummaryLines() = [||]

        // Play/pause reuses the existing single-purpose toggle (central
        // decision, this task's own assumption): "playing" advances
        // `currentTick` forward at `playHz` in `Update`; pausing freezes
        // it wherever it is, exactly like `CommandDemoScene`'s tactical
        // pause freezes live stepping.
        member _.OnTogglePause() =
            if tickCount () > 0L then
                if currentTick >= tickCount () then
                    currentTick <- 0L

                playing <- not playing
                accum <- 0.0

        member _.TickCount() = tickCount ()
        member _.CurrentTick() = currentTick

        member _.SetTick(tick: int64) =
            playing <- false
            accum <- 0.0
            currentTick <- System.Math.Clamp(tick, 0L, tickCount ())

        member _.CurrentHash() =
            if hashes.Length = 0 then 0UL else hashes.[int currentTick]

        member _.Dispose() = ()
