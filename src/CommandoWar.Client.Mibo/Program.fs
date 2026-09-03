module CommandoWar.Client.Mibo.Program

// Host root for the disposable TASK-005 Mibo spike.
//
// Two entry paths, chosen from the command line:
//   --selfcheck [--expect 0x..] [--fixedstep]   headless hash cross-check, no window
//   (default) / --invalid / --screenshot <path>  windowed raylib host
//
// It catches exactly one thing: a content load failure, to show an actionable
// message and refuse to start the simulation. Simulation.step exceptions are
// never caught.

open System
open System.IO
open System.Numerics
open Mibo.Elmish
open Mibo.Elmish.Graphics2D
open Mibo.Input
open CommandoWar.Client.Mibo
open CommandoWar.Client.Mibo.SimBridge

// The one shared-fixture command: agent 3 -> (20,14), issued at tick 1.
let private FixtureMoveAgent = 3
let private FixtureTicks = int CommandoWar.Headless.Fixture.TickCount // 40
let private FixtureFinalHash = 0x838D3AE7DBFB735DUL

// ---------------------------------------------------------------------------
// Isometric projection (view only) and small draw helpers
// ---------------------------------------------------------------------------

let private tileW, tileH = 20.0f, 10.0f
let private origin = Vector2(540.0f, 40.0f)

let private cellToScreen (cx: float32) (cy: float32) =
    origin + Vector2((cx - cy) * tileW * 0.5f, (cx + cy) * tileH * 0.5f)

let private screenToCell (p: Vector2) =
    let d = p - origin
    let a = d.X / (tileW * 0.5f)
    let b = d.Y / (tileH * 0.5f)
    int (MathF.Round((a + b) * 0.5f)), int (MathF.Round((b - a) * 0.5f))

let private col (r, g, b, a) : Mibo.Color =
    Mibo.Color.create (byte r) (byte g) (byte b) (byte a)

let inline private ly (n: int) : int<RenderLayer> = LanguagePrimitives.Int32WithMeasure n

// ---------------------------------------------------------------------------
// Content loading
// ---------------------------------------------------------------------------

let private contentDir = Path.Combine(AppContext.BaseDirectory, "content")

let private loadScenario (invalid: bool) =
    let file = if invalid then "greybox-invalid.cwmap" else "greybox.cwmap"
    let path = Path.Combine(contentDir, file)
    if not (File.Exists path) then
        Error(file, [ $"content file not found: {path}" ])
    else
        Content.load file (File.ReadAllText path)

// ===========================================================================
// Headless self-check (Mibo classic HeadlessRunner)
// ===========================================================================

module private SelfCheck =

    type Model = { Sim: Sim; Steps: int }
    type Msg = Advance

    let private mkProgram (sim: Sim) =
        let init (_: GameContext) = struct ({ Sim = sim; Steps = 0 }, Cmd.none)
        let update Advance (m: Model) =
            m.Sim.Step() |> ignore
            struct ({ m with Steps = m.Steps + 1 }, Cmd.none)
        HeadlessProgram.mkHeadless init update

    /// Drive 40 authoritative ticks by explicit dispatch. Deterministic: one
    /// Advance -> exactly one Simulation.step, no timing involved.
    let runExplicit (sim: Sim) =
        use runner = new HeadlessRunner<Model, Msg>(mkProgram sim)
        for _ in 1..FixtureTicks do
            runner.Dispatch Advance
            runner.Step(TimeSpan.FromMilliseconds 1.0)
            printfn "tick=%d hash=0x%016X format=%d" sim.Tick sim.StateHash sim.StateHashFormat

    /// Drive the same 40 ticks through Mibo's fixed-step facility (virtual
    /// time, 20 Hz). StepUntil so float32 accumulator drift cannot under- or
    /// overshoot the authoritative tick count.
    let runFixedStep (sim: Sim) =
        let init (_: GameContext) = struct ({ Sim = sim; Steps = 0 }, Cmd.none)
        let update Advance (m: Model) =
            m.Sim.Step() |> ignore
            struct ({ m with Steps = m.Steps + 1 }, Cmd.none)
        let prog =
            HeadlessProgram.mkHeadless init update
            |> HeadlessProgram.withFixedStep
                { StepSeconds = 1.0f / 20.0f
                  MaxStepsPerFrame = 1
                  MaxFrameSeconds = ValueNone
                  Map = fun _dtSeconds -> Advance }
        use runner = new HeadlessRunner<Model, Msg>(prog)
        let reached =
            runner.StepUntil((fun m -> m.Steps >= FixtureTicks), TimeSpan.FromSeconds(1.0 / 20.0), 400)
        if not reached || sim.Tick <> int64 FixtureTicks then
            eprintfn "fixed-step reached tick %d (expected %d)" sim.Tick FixtureTicks
        printfn "fixed-step: %d authoritative ticks, final tick=%d hash=0x%016X" FixtureTicks sim.Tick sim.StateHash

let private runSelfCheck (scenario: Content.Scenario) (expect: uint64 option) (fixedStep: bool) : int =
    let sim = Sim scenario
    printfn "# mibo-host self-check: %dx%d, seed %d, %d ticks%s"
        scenario.Width scenario.Height scenario.Seed FixtureTicks
        (if fixedStep then " (Mibo fixed-step)" else "")
    printfn "tick=0 hash=0x%016X format=%d" sim.StateHash sim.StateHashFormat

    sim.QueueMove(FixtureMoveAgent, 20, 14) |> ignore

    if fixedStep then SelfCheck.runFixedStep sim else SelfCheck.runExplicit sim

    printfn "final tick=%d hash=0x%016X draws=%d" sim.Tick sim.StateHash sim.RandomDraws
    printfn "accepted-command-log:"
    for line in sim.AcceptedCommandLog do
        printfn "  %s" line

    match expect with
    | None -> 0
    | Some want when want = sim.StateHash ->
        printfn "MATCH expected final hash 0x%016X" want
        0
    | Some want ->
        printfn "MISMATCH expected 0x%016X, got 0x%016X" want sim.StateHash
        1

// ===========================================================================
// Windowed raylib host (classic MVU)
// ===========================================================================

type private Run =
    { Sim: Sim
      Scenario: Content.Scenario
      Selected: int option
      LastCommanded: (int * int) option
      Paused: bool
      RenderFrames: int
      SimSteps: int
      Window: float
      RenderFps: int
      SimTps: int
      ScreenshotPath: string option
      FrameCount: int }

type private Model =
    | Failed of source: string * errors: string list
    | Running of Run

type private Msg =
    | FixedTick of float32 // authoritative cadence (Mibo withFixedStep)
    | RenderTick of GameTime // per render frame (Mibo withTick): measurement only
    | LeftClick of Vector2
    | RightClick of Vector2
    | Key of KeyCode

let private simHzRef = ref 20.0f

let private mkInit (invalid: bool) (screenshot: string option) : GameContext -> struct (Model * Cmd<Msg>) =
    fun _ctx ->
        match loadScenario invalid with
        | Error(src, errs) -> struct (Failed(src, errs), Cmd.none)
        | Ok scenario ->
            let sim = Sim scenario
            if screenshot.IsSome then
                sim.QueueMove(FixtureMoveAgent, 20, 14) |> ignore
            struct (Running
                        { Sim = sim
                          Scenario = scenario
                          Selected = None
                          LastCommanded = None
                          Paused = false
                          RenderFrames = 0
                          SimSteps = 0
                          Window = 0.0
                          RenderFps = 0
                          SimTps = 0
                          ScreenshotPath = screenshot
                          FrameCount = 0 },
                    Cmd.none)

let private update (msg: Msg) (model: Model) : struct (Model * Cmd<Msg>) =
    match model with
    | Failed _ -> struct (model, Cmd.none)
    | Running r ->
        match msg with
        | FixedTick _ ->
            if r.Paused then
                struct (model, Cmd.none)
            else
                let info = r.Sim.Step() // exceptions propagate on purpose
                let cleared = if info.AcceptedThisTick = 0 then None else r.LastCommanded
                struct (Running { r with SimSteps = r.SimSteps + 1; LastCommanded = cleared }, Cmd.none)

        | RenderTick gt ->
            let dt = gt.ElapsedGameTime.TotalSeconds
            let w = r.Window + dt
            let r' =
                if w >= 1.0 then
                    { r with
                        RenderFps = r.RenderFrames + 1
                        SimTps = r.SimSteps
                        RenderFrames = 0
                        SimSteps = 0
                        Window = w - 1.0 }
                else
                    { r with RenderFrames = r.RenderFrames + 1; Window = w }

            match r'.ScreenshotPath with
            | Some _ when r'.FrameCount = 240 ->
                // raylib writes the PNG at end of this frame's draw; main copies
                // it to the requested path after the loop exits.
                Raylib_cs.Raylib.TakeScreenshot("mibo-shot.png")
                eprintfn "[spike] screenshot at tick %d hash %s" r'.Sim.Tick r'.Sim.StateHashHex
                struct (Running { r' with FrameCount = r'.FrameCount + 1 }, Cmd.signalExit)
            | _ -> struct (Running { r' with FrameCount = r'.FrameCount + 1 }, Cmd.none)

        | LeftClick pos ->
            let cx, cy = screenToCell pos
            match r.Sim.Agents |> Array.tryFind (fun a -> a.Friendly && a.X = cx && a.Y = cy) with
            | Some a -> struct (Running { r with Selected = Some a.Id }, Cmd.none)
            | None ->
                match r.Selected with
                | Some id ->
                    r.Sim.QueueMove(id, cx, cy) |> ignore
                    struct (Running { r with LastCommanded = Some(cx, cy) }, Cmd.none)
                | None -> struct (model, Cmd.none)

        | RightClick _ -> struct (Running { r with Selected = None }, Cmd.none)
        | Key KeyCode.Space -> struct (Running { r with Paused = not r.Paused }, Cmd.none)
        | Key KeyCode.Escape -> struct (model, Cmd.signalExit)
        | Key _ -> struct (model, Cmd.none)

// ---------------------------------------------------------------------------
// View
// ---------------------------------------------------------------------------

let private diamond (cx: float32) (cy: float32) =
    let c = cellToScreen cx cy
    // counter-clockwise in screen space (y down): top, left, bottom, right
    [| c + Vector2(0.0f, -tileH * 0.5f)
       c + Vector2(-tileW * 0.5f, 0.0f)
       c + Vector2(0.0f, tileH * 0.5f)
       c + Vector2(tileW * 0.5f, 0.0f) |]

let private hud (r: Run) =
    let sel = match r.Selected with Some i -> string i | None -> "none"
    let cmd = match r.LastCommanded with Some(x, y) -> $"MoveTo({x},{y})" | None -> "-"
    let paused = if r.Paused then "   [PAUSED]" else ""
    let hz = simHzRef.Value
    String.concat "\n"
        [ $"tick            {r.Sim.Tick}"
          $"state hash      {r.Sim.StateHashHex}  (format {r.Sim.StateHashFormat})"
          $"random draws    {r.Sim.RandomDraws}"
          $"sim rate        %.0f{hz} Hz fixed   {r.SimTps} steps/s measured{paused}"
          $"render rate     {r.RenderFps} fps"
          $"selected agent  {sel}     last command  {cmd}"
          ""
          "L-click agent = select   L-click cell = move   R-click = deselect"
          "[Space] pause   [Esc] quit" ]

let private view (_ctx: GameContext) (model: Model) (buffer: RenderBuffer2D) =
    let font = Raylib_cs.Raylib.GetFontDefault()

    match model with
    | Failed(src, errs) ->
        let text =
            $"CONTENT LOAD FAILED  ({src})\n\n- "
            + String.Join("\n- ", errs)
            + "\n\nThe simulation was not started. Fix the file and relaunch."
        buffer.AddText(font, text, Vector2(30.0f, 30.0f), 18.0f, 1.0f, col (255, 210, 210, 255), ly 10)

    | Running r ->
        for y in 0 .. r.Scenario.Height - 1 do
            for x in 0 .. r.Scenario.Width - 1 do
                let d = diamond (float32 x) (float32 y)
                let c = if r.Scenario.IsPassable(x, y) then col (74, 82, 70, 255) else col (44, 48, 60, 255)
                buffer.AddTriangle(d.[0], d.[1], d.[2], c, ly 1)
                buffer.AddTriangle(d.[0], d.[2], d.[3], c, ly 1)
                buffer.AddLineStrip([| d.[0]; d.[1]; d.[2]; d.[3]; d.[0] |], col (0, 0, 0, 60), ly 1)

        for o in r.Scenario.Objectives do
            let d = diamond (float32 o.X) (float32 o.Y)
            buffer.AddTriangle(d.[0], d.[1], d.[2], col (215, 165, 40, 200), ly 2)
            buffer.AddTriangle(d.[0], d.[2], d.[3], col (215, 165, 40, 200), ly 2)

        for a in r.Sim.Agents do
            let p = cellToScreen (float32 a.X) (float32 a.Y) - Vector2(0.0f, tileH)
            let c = if a.Friendly then col (90, 190, 255, 255) else col (255, 100, 90, 255)
            match a.Dest with
            | Some(struct (dx, dy)) ->
                buffer.AddLine(p, cellToScreen (float32 dx) (float32 dy) - Vector2(0.0f, tileH), col (255, 255, 255, 120), ly 3)
            | None -> ()
            buffer.AddFillCircle(p, 6.0f, c, ly 4)
            buffer.AddCircleOutline(p, 6.0f, col (0, 0, 0, 255), ly 4)
            if r.Selected = Some a.Id then
                buffer.AddCircleOutline(p, 10.0f, col (255, 255, 60, 255), ly 5)

        buffer.AddText(font, hud r, Vector2(12.0f, 10.0f), 14.0f, 1.0f, col (235, 235, 235, 255), ly 10)

// ---------------------------------------------------------------------------
// Program wiring
// ---------------------------------------------------------------------------

let private subscribe (ctx: GameContext) (_model: Model) =
    Sub.batch
        [ Mouse.onLeftClick LeftClick ctx
          Mouse.onRightClick RightClick ctx
          Keyboard.onPressed Key ctx ]

let private runWindowed (invalid: bool) (screenshot: string option) (simHz: float32) : int =
    simHzRef.Value <- simHz
    let program =
        Program.mkProgram (mkInit invalid screenshot) update
        |> Program.withConfig (fun cfg ->
            { cfg with
                Width = 1024
                Height = 640
                TargetFPS = ValueSome 60
                Title = "CommandoWar - Mibo spike (TASK-005)" })
        |> Program.withInput
        |> Program.withSubscription subscribe
        |> Program.withTick RenderTick
        |> Program.withFixedStep
            { StepSeconds = 1.0f / simHz
              MaxStepsPerFrame = 5
              MaxFrameSeconds = ValueSome 0.25f
              Map = FixedTick }
        |> Program.withRenderer (fun () ->
            let clear = Raylib_cs.Color(byte 18, byte 20, byte 26, byte 255)
            Renderer2D.createWith { Renderer2DConfig.defaults with ClearColor = ValueSome clear } view)

    let game = new RaylibGame<Model, Msg>(program)
    game.Run()

    match screenshot with
    | Some dest ->
        // raylib TakeScreenshot writes relative to the current working directory.
        let candidates =
            [ Path.Combine(Directory.GetCurrentDirectory(), "mibo-shot.png")
              Path.Combine(AppContext.BaseDirectory, "mibo-shot.png") ]
        match candidates |> List.tryFind File.Exists with
        | Some src ->
            Directory.CreateDirectory(Path.GetDirectoryName dest) |> ignore
            File.Copy(src, dest, true)
            File.Delete src
            printfn "[spike] screenshot written: %s" (Path.GetFullPath dest)
        | None -> eprintfn "[spike] screenshot not produced (no window frame captured)"
    | None -> ()
    0

// ---------------------------------------------------------------------------
// Entry point
// ---------------------------------------------------------------------------

let private parseHex (raw: string) =
    let s = raw.Trim()
    let s = if s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) then s.Substring 2 else s
    match UInt64.TryParse(s, Globalization.NumberStyles.HexNumber, Globalization.CultureInfo.InvariantCulture) with
    | true, v -> Some v
    | _ -> None

[<EntryPoint>]
let main argv =
    let args = List.ofArray argv
    let has f = List.contains f args
    let valueOf f =
        match List.tryFindIndex ((=) f) args with
        | Some i when i + 1 < args.Length -> Some args.[i + 1]
        | _ -> None

    let invalid = has "--invalid"

    if has "--selfcheck" then
        let expect =
            match valueOf "--expect" with
            | Some h -> parseHex h
            | None when has "--expect" -> Some FixtureFinalHash
            | None -> None
        match loadScenario invalid with
        | Error(src, errs) ->
            eprintfn "CONTENT LOAD FAILED (%s):" src
            for e in errs do eprintfn "  - %s" e
            2
        | Ok scenario -> runSelfCheck scenario expect (has "--fixedstep")
    else
        let shot = valueOf "--screenshot"
        let hz = if shot.IsSome then 6.0f else 20.0f
        // --invalid also echoes to stderr so a headless caller sees the failure,
        // then opens the window showing the failure panel.
        if invalid then
            match loadScenario true with
            | Error(src, errs) ->
                eprintfn "CONTENT LOAD FAILED (%s):" src
                for e in errs do eprintfn "  - %s" e
            | Ok _ -> ()
        runWindowed invalid shot hz
