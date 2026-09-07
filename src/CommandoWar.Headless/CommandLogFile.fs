namespace CommandoWar.Headless

open CommandoWar.Sim

/// Minimal, versioned, line-based command-log text format for the framework
/// spikes and the test corpus. It is deliberately tiny: it is a **frozen
/// legacy fixture-script format, not the production replay-command format**
/// (TASK-020). It cannot express multi-recipient addressing, `Urgency`,
/// `RiskTolerance`, or an `IssuedAtTick` distinct from the delivery tick. The
/// production, lossless serialisation of the full accepted envelope is
/// `CommandoWar.Sim.ReplaySerialisation` (replay-command format v1, TASK-025 /
/// backlog B-045); this `.cwlog` grammar and its `Version` are frozen and are
/// not extended to carry those fields. Replay/divergence semantics live in
/// `CommandoWar.Sim`; this module only turns text into the typed
/// `RecordedCommand[]` those functions already accept.
///
/// Grammar (UTF-8, one directive per line):
///
///   # ...            comment, ignored
///   version 1        optional; rejected if not 1
///   <tick> <agentId> move <x> <y>
///
/// Blank lines are ignored. `tick` is the 1-based tick whose command-intake
/// phase consumes the command; this one field is mapped to **both**
/// `RecordedCommand.Tick` (the delivery tick) and the envelope's
/// `PlayerCommand.IssuedAtTick` — the degenerate case of an order issued on
/// the tick it is delivered (TASK-024). Commands are assigned a `CommandId`
/// from their order of appearance in the file (first directive = id 1) and a
/// per-tick `Sequence` from their order within that tick.
[<RequireQualifiedAccess>]
module CommandLogFile =

    /// The only supported command-log text-format version.
    [<Literal>]
    let Version = 1

    /// Why a command-log file could not be parsed. Every case names the source
    /// line so the failure is actionable (docs/03_ARCHITECTURE.md section 17).
    type ParseError =
        | UnsupportedFormatVersion of line: int * found: string
        | MalformedLine of line: int * text: string * expected: string
        | UnknownDirective of line: int * verb: string
        | TickNotPositive of line: int * tick: int64
        | NonIntegerField of line: int * field: string * value: string

    /// Parses command-log text into recorded commands sorted into canonical
    /// (Tick, Sequence) order, or the first structural error found.
    let parse (issuer: string) (text: string) : Result<RecordedCommand[], ParseError> =
        let lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')

        let mutable error: ParseError option = None
        let acc = ResizeArray<int64 * int * PlayerCommand>()  // tick, appearance index, command
        let mutable appearance = 0

        let mutable i = 0
        while error.IsNone && i < lines.Length do
            let lineNo = i + 1
            let raw = lines.[i]
            let trimmed = raw.Trim()

            if trimmed = "" || trimmed.StartsWith "#" then
                ()
            else
                let parts = trimmed.Split([| ' '; '\t' |], System.StringSplitOptions.RemoveEmptyEntries)

                match parts with
                | [| "version"; v |] ->
                    if v <> string Version then
                        error <- Some(UnsupportedFormatVersion(lineNo, v))
                | [| tickTok; agentTok; "move"; xTok; yTok |] ->
                    let parseField name tok =
                        match System.Int64.TryParse(tok: string) with
                        | true, v -> Ok v
                        | _ -> Error(NonIntegerField(lineNo, name, tok))

                    match parseField "tick" tickTok, parseField "agent" agentTok, parseField "x" xTok, parseField "y" yTok with
                    | Ok tick, Ok agent, Ok x, Ok y ->
                        if tick < 1L then
                            error <- Some(TickNotPositive(lineNo, tick))
                        else
                            appearance <- appearance + 1
                            let cmd =
                                Command.moveTo
                                    (CommandId.ofInt appearance)
                                    tick
                                    (AgentId.ofInt (int agent))
                                    { X = int x; Y = int y }
                            acc.Add(tick, appearance, cmd)
                    | Error e, _, _, _
                    | _, Error e, _, _
                    | _, _, Error e, _
                    | _, _, _, Error e -> error <- Some e
                | [| _; _; verb; _; _ |] -> error <- Some(UnknownDirective(lineNo, verb))
                | _ ->
                    error <- Some(MalformedLine(lineNo, trimmed, "<tick> <agentId> move <x> <y>"))

            i <- i + 1

        match error with
        | Some e -> Error e
        | None ->
            // Assign per-tick Sequence from appearance order, then sort into
            // canonical (Tick, Sequence) order for the replay runner.
            let byTick = acc |> Seq.groupBy (fun (t, _, _) -> t)

            let recorded =
                [| for _tick, group in byTick do
                       let ordered = group |> Seq.sortBy (fun (_, appIdx, _) -> appIdx) |> Seq.toArray
                       for seq, (tick, _appIdx, cmd) in Array.indexed ordered ->
                           { Tick = tick; Sequence = seq; Command = cmd; Issuer = issuer } |]
                |> Array.sortBy (fun c -> c.Tick, c.Sequence)

            Ok recorded

    /// Human-readable rendering of a parse error, including the line number.
    let describeError (e: ParseError) : string =
        match e with
        | UnsupportedFormatVersion(line, found) ->
            $"line {line}: unsupported command-log version '{found}', expected {Version}"
        | MalformedLine(line, text, expected) -> $"line {line}: malformed directive '{text}', expected '{expected}'"
        | UnknownDirective(line, verb) -> $"line {line}: unknown command verb '{verb}', only 'move' is supported"
        | TickNotPositive(line, tick) -> $"line {line}: tick must be >= 1, got {tick}"
        | NonIntegerField(line, field, value) -> $"line {line}: field '{field}' is not an integer: '{value}'"
