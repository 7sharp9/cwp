module CommandoWar.Headless.Program

open System
open System.IO
open CommandoWar.Sim
open CommandoWar.Headless

/// Exit codes. Kept explicit so a script (or a framework spike's comparison
/// step) can branch on the outcome.
[<RequireQualifiedAccess>]
module Exit =
    let ok = 0
    let usage = 1
    let replayError = 2
    let diverged = 3

let private hx (h: StateHash) = sprintf "0x%016X" h.Value
let private hxv (v: uint64) = sprintf "0x%016X" v

let private describeReplayError (e: ReplayError) : string =
    match e with
    | UnsupportedReplayVersion(found, supported) -> $"unsupported replay format version {found}, supported {supported}"
    | UnsupportedCommandLogVersion(found, supported) -> $"unsupported command-log version {found}, supported {supported}"
    | UnsupportedCanonicalFormat(found, supported) -> $"unsupported canonical format {found}, supported {supported}"
    | InitialStateNotAtTickZero tick -> $"initial state is at tick {tick}, expected 0"
    | SeedInconsistentWithInitialState(seed, word) -> $"recorded seed {seed} is inconsistent with initial stream word {word}"
    | InvalidTickCount n -> $"invalid tick count {n}"
    | NonMonotonicCommandLog(index, struct (pt, ps), struct (ct, cs)) ->
        $"command log not strictly ascending at index {index}: ({pt},{ps}) then ({ct},{cs})"
    | CommandOutsideReplayRange(index, tick, tickCount) ->
        $"command {index} at tick {tick} is outside the replay range 1..{tickCount}"

let private agentLine (a: AgentState) =
    let dest =
        match a.Destination with
        | Some c -> $"-> ({c.X},{c.Y})"
        | None -> "at rest"
    sprintf "    agent %d  (%d,%d)  %s" (AgentId.value a.Id) a.Position.X a.Position.Y dest

let private printOutcomeTail (outcome: ReplayOutcome) =
    let final = outcome.FinalState
    let finalHash = Hashing.hash final
    printfn "final tick   : %d" final.Tick
    printfn "final hash   : %s (format %d)" (hx finalHash) finalHash.Format
    printfn "random draws : %d" final.Random.Draws
    printfn "events       : %d" outcome.Events.Length
    printfn "final agents :"
    for a in final.Agents |> Array.sortBy (fun a -> a.Id) do
        printfn "%s" (agentLine a)

/// Reads a command-log file and parses it, or prints an actionable error and
/// returns None.
let private loadLog (path: string) : RecordedCommand[] option =
    if not (File.Exists path) then
        eprintfn "error: command-log file not found: %s" path
        None
    else
        match CommandLogFile.parse Fixture.Issuer (File.ReadAllText path) with
        | Ok cmds -> Some cmds
        | Error e ->
            eprintfn "error: %s: %s" path (CommandLogFile.describeError e)
            None

let private optTicks (rest: string list) : Result<int64, string> =
    match rest with
    | [] -> Ok Fixture.TickCount
    | [ "--ticks"; n ] ->
        match Int64.TryParse n with
        | true, v when v >= 0L -> Ok v
        | _ -> Error $"invalid --ticks value '{n}'"
    | _ -> Error "expected optional '--ticks N'"

// --- subcommands ---------------------------------------------------------------

let private cmdStep (args: string list) : int =
    match args with
    | [ n ] ->
        match Int32.TryParse n with
        | true, ticks when ticks >= 0 ->
            let mutable state = Fixture.initialState ()
            printfn "# step %d tick(s) from the shared fixture (grid %dx%d, seed %d), no commands"
                ticks Fixture.bounds.Width Fixture.bounds.Height Fixture.Seed
            printfn "tick=%d hash=%s format=%d" state.Tick (hx (Hashing.hash state)) (Hashing.hash state).Format
            for _ in 1 .. ticks do
                let r = Simulation.step SimConfig.standard [||] state
                state <- r.State
                printfn "tick=%d hash=%s format=%d" r.State.Tick (hx r.StateHash) r.StateHash.Format
            Exit.ok
        | _ ->
            eprintfn "error: step requires a non-negative integer tick count"
            Exit.usage
    | _ ->
        eprintfn "usage: cwheadless step <N>"
        Exit.usage

let private cmdReplay (args: string list) : int =
    match args with
    | path :: rest ->
        match optTicks rest with
        | Error msg ->
            eprintfn "error: %s" msg
            Exit.usage
        | Ok ticks ->
            match loadLog path with
            | None -> Exit.usage
            | Some cmds ->
                let record =
                    Replay.record
                        { Build = "cwheadless"; Scenario = Path.GetFileName path }
                        (Fixture.initialState ())
                        ticks
                        (CommandLog.create cmds)

                match Replay.run SimConfig.standard record with
                | Error e ->
                    eprintfn "replay error: %s" (describeReplayError e)
                    Exit.replayError
                | Ok outcome ->
                    printfn "# replay of %s against the shared fixture, %d tick(s), %d command(s)"
                        path ticks cmds.Length
                    for cp in outcome.TickHashes do
                        printfn "tick=%d hash=%s format=%d" cp.Tick (hx cp.Hash) cp.Hash.Format
                    printfn ""
                    printOutcomeTail outcome
                    Exit.ok
    | _ ->
        eprintfn "usage: cwheadless replay <command-log> [--ticks N]"
        Exit.usage

let private cmdCompare (args: string list) : int =
    match args with
    | pathA :: pathB :: rest ->
        match optTicks rest with
        | Error msg ->
            eprintfn "error: %s" msg
            Exit.usage
        | Ok ticks ->
            match loadLog pathA, loadLog pathB with
            | Some a, Some b ->
                match Divergence.diagnose SimConfig.standard (Fixture.initialState ()) a b ticks with
                | Error e ->
                    eprintfn "replay error: %s" (describeReplayError e)
                    Exit.replayError
                | Ok report ->
                    printfn "# compare %s (reference) vs %s (candidate), %d tick(s)" pathA pathB ticks
                    match report with
                    | Match n ->
                        printfn "MATCH: %d tick(s) compared, all authoritative hashes identical" n
                        Exit.ok
                    | TruncatedRun(lastAgreed, expTicks, actTicks) ->
                        printfn "TRUNCATED: agreed through tick %d, then lengths differ (reference %d, candidate %d)"
                            lastAgreed expTicks actTicks
                        Exit.diverged
                    | Diverged(p, expTicks, actTicks) ->
                        printfn "DIVERGED at tick %d" p.Tick
                        printfn "  reference hash : %s" (hx p.Expected)
                        printfn "  candidate hash : %s" (hx p.Actual)
                        printfn "  first section  : %s" (defaultArg p.Section "(unavailable)")
                        printfn "  random draws   : reference %d, candidate %d" p.ExpectedRandomDraws p.ActualRandomDraws
                        printfn "  run lengths    : reference %d, candidate %d" expTicks actTicks
                        Exit.diverged
            | _ -> Exit.usage
    | _ ->
        eprintfn "usage: cwheadless compare <command-log-a> <command-log-b> [--ticks N]"
        Exit.usage

let private cmdFixture () : int =
    let initial = Fixture.initialState ()
    match Fixture.run () with
    | Error e ->
        eprintfn "replay error: %s" (describeReplayError e)
        Exit.replayError
    | Ok outcome ->
        printfn "# CommandoWar framework-spike shared fixture"
        printfn "# framework-neutral reference. Regenerate with: cwheadless fixture"
        printfn ""
        printfn "grid              : %d x %d" Fixture.bounds.Width Fixture.bounds.Height
        printfn "seed              : %d (0x%016X)" Fixture.Seed Fixture.Seed
        printfn "prng              : %s v%d" SplitMix64.Name SplitMix64.Version
        printfn "canonical format  : %d" Canonical.FormatVersion
        printfn "state hash        : %s over canonical encoding" Hashing.Algorithm
        printfn "replay format     : %d   command-log format : %d" Replay.FormatVersion CommandLog.Version
        printfn "tick count        : %d" Fixture.TickCount
        printfn "agents (tick 0)   : 6 friendly at column x=0, rows y=0..5"
        printfn "command           : tick %d, agent %d -> (%d,%d)  [CommandId 1]"
            Fixture.CommandIssueTick (AgentId.value Fixture.MovedAgent) Fixture.MoveTarget.X Fixture.MoveTarget.Y
        printfn "initial hash      : %s" (hx (Hashing.hash initial))
        printfn ""
        printfn "per-tick authoritative state hash:"
        printfn ""
        printfn "| tick | state hash          |"
        printfn "|-----:|---------------------|"
        for cp in outcome.TickHashes do
            printfn "| %4d | %s |" cp.Tick (hx cp.Hash)
        printfn ""
        printOutcomeTail outcome
        Exit.ok

/// Runs, or regenerates, the committed replay corpus (`content/replays/`,
/// TASK-016). `corpus` replays every entry, checks it reproduces its own
/// hashes on a second independent run, then compares the per-tick hashes to
/// the committed table, printing a divergence report and exiting non-zero on
/// the first mismatch. `corpus --regenerate` rewrites every table instead.
let private cmdCorpus (args: string list) : int =
    let mutable dir = Corpus.DefaultDir
    let mutable regenerate = false
    let mutable optErr: string option = None

    let rec parseOpts xs =
        match xs with
        | [] -> ()
        | "--regenerate" :: t ->
            regenerate <- true
            parseOpts t
        | "--dir" :: v :: t ->
            dir <- v
            parseOpts t
        | other :: _ -> optErr <- Some $"unexpected argument '{other}'"

    parseOpts args

    match optErr with
    | Some m ->
        eprintfn "error: %s" m
        eprintfn "usage: cwheadless corpus [--regenerate] [--dir PATH]"
        Exit.usage
    | None when regenerate ->
        let mutable failed = false

        for e in Corpus.all do
            match Corpus.regenerateEntry dir e with
            | Corpus.Wrote p -> printfn "wrote %s" p
            | Corpus.RegenLogError m ->
                eprintfn "error: %s: %s" e.Name m
                failed <- true
            | Corpus.RegenReplayError err ->
                eprintfn "error: %s: replay error: %s" e.Name (describeReplayError err)
                failed <- true

        if failed then Exit.replayError else Exit.ok
    | None ->
        printfn "# cwheadless corpus - %d entries in %s" Corpus.all.Length dir
        let mutable worst = Exit.ok

        for e in Corpus.all do
            match Corpus.checkEntry dir e with
            | Corpus.Passed -> printfn "PASS  %-20s %d tick(s)" e.Name e.TickCount
            | Corpus.LogError m ->
                eprintfn "ERROR %-20s %s" e.Name m
                worst <- max worst Exit.replayError
            | Corpus.ReplayFailed err ->
                eprintfn "ERROR %-20s replay error: %s" e.Name (describeReplayError err)
                worst <- max worst Exit.replayError
            | Corpus.TableError m ->
                eprintfn "ERROR %-20s %s" e.Name m
                worst <- max worst Exit.replayError
            | Corpus.Nondeterministic report ->
                eprintfn "DIVERGED %s: two independent replays of the same entry produced different hashes" e.Name

                match report with
                | Diverged(p, expTicks, actTicks) ->
                    eprintfn "  first bad tick : %d" p.Tick
                    eprintfn "  hash run A     : %s" (hx p.Expected)
                    eprintfn "  hash run B     : %s" (hx p.Actual)
                    eprintfn "  first section  : %s" (defaultArg p.Section "(unavailable)")
                    eprintfn "  random draws   : run A %d, run B %d" p.ExpectedRandomDraws p.ActualRandomDraws
                    eprintfn "  run lengths    : %d, %d" expTicks actTicks
                | TruncatedRun(lastAgreed, expTicks, actTicks) ->
                    eprintfn "  agreed through tick %d, then run lengths differ (%d, %d)" lastAgreed expTicks actTicks
                | Match _ -> ()

                worst <- max worst Exit.diverged
            | Corpus.Mismatch d ->
                eprintfn "DIVERGED %s: this build disagrees with the committed table" e.Name
                eprintfn "  first bad tick : %d" d.FirstBadTick
                eprintfn "  expected hash  : %s  (committed %s/%s.md)" (hxv d.Expected) dir e.Name
                eprintfn "  actual hash    : %s  (this build)" (hxv d.Actual)
                eprintfn "  random draws   : %d (this build; the committed table stores hashes only)" d.ActualDraws

                if d.Note <> "" then
                    eprintfn "  note           : %s" d.Note

                worst <- max worst Exit.diverged

        if worst = Exit.ok then
            printfn "OK - all %d entries match their committed tables" Corpus.all.Length

        worst

let private parseCell (s: string) : Cell option =
    match s.Split(',') with
    | [| xs; ys |] ->
        match Int32.TryParse xs, Int32.TryParse ys with
        | (true, x), (true, y) -> Some { X = x; Y = y }
        | _ -> None
    | _ -> None

/// Parses an `AX,AY:BX,BY` cell-pair spec, used by `--los` and `--path`.
let private parseCellPair (s: string) : (Cell * Cell) option =
    match s.Split(':') with
    | [| l; r |] ->
        match parseCell l, parseCell r with
        | Some a, Some b -> Some(a, b)
        | _ -> None
    | _ -> None

let private cmdRender (args: string list) : int =
    // render <fixture|demo|los|path|command-log> [--tick N] [--layer NAME]
    //        [--los AX,AY:BX,BY]... [--path AX,AY:BX,BY]...
    //        [--format ascii|svg|html] [--out PATH]
    match args with
    | [] ->
        eprintfn
            "usage: cwheadless render <fixture|demo|los|path|command-log> [--tick N] [--layer NAME] [--los AX,AY:BX,BY]... [--path AX,AY:BX,BY]... [--format ascii|svg|html] [--out PATH]"
        Exit.usage
    | target :: rest ->
        let mutable tick: int64 option = None
        let mutable layerName: string option = None
        let mutable format = "ascii"
        let mutable out: string option = None
        let mutable losSpecs: (Cell * Cell) list = []
        let mutable pathSpecs: (Cell * Cell) list = []
        let mutable optErr: string option = None

        let rec parseOpts xs =
            match xs with
            | [] -> ()
            | "--tick" :: v :: t ->
                match Int64.TryParse v with
                | true, n when n >= 0L ->
                    tick <- Some n
                    parseOpts t
                | _ -> optErr <- Some $"invalid --tick value '{v}'"
            | "--layer" :: v :: t ->
                layerName <- Some v
                parseOpts t
            | "--los" :: v :: t ->
                match parseCellPair v with
                | Some pair ->
                    losSpecs <- losSpecs @ [ pair ]
                    parseOpts t
                | None -> optErr <- Some $"invalid --los value '{v}', expected AX,AY:BX,BY"
            | "--path" :: v :: t ->
                match parseCellPair v with
                | Some pair ->
                    pathSpecs <- pathSpecs @ [ pair ]
                    parseOpts t
                | None -> optErr <- Some $"invalid --path value '{v}', expected AX,AY:BX,BY"
            | "--format" :: v :: t ->
                match v with
                | "ascii"
                | "svg"
                | "html" ->
                    format <- v
                    parseOpts t
                | _ -> optErr <- Some $"invalid --format '{v}', expected ascii|svg|html"
            | "--out" :: v :: t ->
                out <- Some v
                parseOpts t
            | other :: _ -> optErr <- Some $"unexpected argument '{other}'"

        parseOpts rest

        match optErr with
        | Some m ->
            eprintfn "error: %s" m
            Exit.usage
        | None ->
            // Each target resolves to its diagnostic frames plus the terrain
            // `--los` rays are traced over.
            let resolved: (DiagnosticFrame[] * Terrain) option =
                match target with
                | "fixture" ->
                    let w = Fixture.initialState ()
                    Some(DiagnosticRender.runFrames w (Fixture.commandLog ()) Fixture.TickCount, w.Terrain)
                | "demo" ->
                    let w = DemoScenario.initialState ()
                    Some(DiagnosticRender.runFrames w (DemoScenario.commandLog ()) DemoScenario.TickCount, w.Terrain)
                | "los" ->
                    let w = LosDemo.initialState ()
                    Some(DiagnosticRender.runFrames w [||] LosDemo.TickCount, w.Terrain)
                | "path" ->
                    let w = PathDemo.initialState ()
                    Some(DiagnosticRender.runFrames w [||] PathDemo.TickCount, w.Terrain)
                | path ->
                    match loadLog path with
                    | None -> None
                    | Some cmds ->
                        let w = Fixture.initialState ()
                        Some(DiagnosticRender.runFrames w cmds Fixture.TickCount, w.Terrain)

            match resolved with
            | None -> Exit.usage
            | Some(frames, terrain) ->
                let losOverlays: Overlay[] =
                    losSpecs
                    |> List.map (fun (a, b) ->
                        let r = Sight.trace terrain a b
                        SightRay(a, b, r.Path, r.Blocker))
                    |> List.toArray

                let pathOverlays: Overlay[] =
                    pathSpecs
                    |> List.map (fun (a, b) ->
                        match Pathfinding.find terrain a b with
                        | Found(cells, cost) -> PlannedPath(a, b, cells, cost, true)
                        | NoPath
                        | BudgetExhausted _
                        | InvalidEndpoint _ -> PlannedPath(a, b, [||], 0, false))
                    |> List.toArray

                let extraOverlays = Array.append losOverlays pathOverlays

                let attach (f: DiagnosticFrame) =
                    if Array.isEmpty extraOverlays then
                        f
                    else
                        { f with Overlays = Array.append f.Overlays extraOverlays }

                let emit (text: string) =
                    match out with
                    | Some p ->
                        File.WriteAllText(p, text)
                        printfn "wrote %s (%d bytes)" p (Text.Encoding.UTF8.GetByteCount text)
                    | None -> printf "%s" text

                match format with
                | "html" ->
                    if tick.IsSome then
                        eprintfn "note: --tick is ignored for --format html (all %d frames are embedded)" frames.Length

                    emit (DiagnosticRender.Html(frames |> Array.map attach))
                    Exit.ok
                | _ ->
                    let idx = defaultArg (tick |> Option.map int) 0

                    if idx < 0 || idx >= frames.Length then
                        eprintfn "error: --tick %d is outside 0..%d" idx (frames.Length - 1)
                        Exit.usage
                    else
                        let baseFrame = frames.[idx]

                        match layerName with
                        | Some name when not (baseFrame.Layers |> Array.exists (fun l -> l.Name = name)) ->
                            let names = baseFrame.Layers |> Array.map (fun l -> l.Name) |> String.concat ", "
                            eprintfn "error: unknown --layer '%s' (available: %s)" name names
                            Exit.usage
                        | _ ->
                            let selected =
                                attach (
                                    match layerName with
                                    | None -> baseFrame
                                    | Some name ->
                                        { baseFrame with
                                            Layers = baseFrame.Layers |> Array.filter (fun l -> l.Name = name) }
                                )

                            let text =
                                if format = "svg" then
                                    DiagnosticRender.Svg selected
                                else
                                    DiagnosticRender.Ascii selected

                            emit text
                            Exit.ok

let private usage () =
    printfn "cwheadless - framework-neutral headless reference for CommandoWar.Sim"
    printfn ""
    printfn "usage:"
    printfn "  cwheadless step <N>                              step N ticks from the fixture, print tick + hash"
    printfn "  cwheadless replay <command-log> [--ticks N]      replay a command log against the fixture"
    printfn "  cwheadless compare <log-a> <log-b> [--ticks N]   report the first authoritative divergence"
    printfn "  cwheadless fixture                               emit the pinned shared fixture + per-tick hashes"
    printfn "  cwheadless render <target> [opts]                render diagnostic frames (target: fixture | demo | los | path | <command-log>)"
    printfn "        [--tick N] [--layer NAME] [--los AX,AY:BX,BY]... [--path AX,AY:BX,BY]... [--format ascii|svg|html] [--out PATH]"
    printfn "  cwheadless corpus [--regenerate] [--dir PATH]    check (or regenerate) the committed replay corpus (content/replays/)"
    printfn ""
    printfn "exit codes: %d ok, %d usage/IO, %d replay error, %d divergence detected"
        Exit.ok Exit.usage Exit.replayError Exit.diverged
    printfn ""
    printfn "command-log format (v%d), one directive per line:" CommandLogFile.Version
    printfn "  # comment"
    printfn "  version 1"
    printfn "  <tick> <agentId> move <x> <y>"

[<EntryPoint>]
let main argv =
    match Array.toList argv with
    | [] | [ "help" ] | [ "--help" ] | [ "-h" ] ->
        usage ()
        Exit.ok
    | "step" :: rest -> cmdStep rest
    | "replay" :: rest -> cmdReplay rest
    | "compare" :: rest -> cmdCompare rest
    | [ "fixture" ] -> cmdFixture ()
    | "render" :: rest -> cmdRender rest
    | "corpus" :: rest -> cmdCorpus rest
    | other :: _ ->
        eprintfn "error: unknown subcommand '%s'" other
        usage ()
        Exit.usage
