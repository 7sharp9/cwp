namespace CommandoWar.Sim

/// Production replay-command serialisation (TASK-025, backlog B-045;
/// docs/04_SIMULATION_SPEC.md section 16).
///
/// A versioned, lossless, line-based text format for a run's accepted command
/// log plus a small header. It exists because the legacy `.cwlog` fixture
/// script (`CommandoWar.Headless.CommandLogFile`, frozen at v1) cannot express
/// a full accepted command: it has no syntax for multi-recipient addressing
/// (`PlayerCommand.Recipients`), `Urgency`, `RiskTolerance`, or an
/// `IssuedAtTick` distinct from the delivery tick (TASK-024).
///
/// Scope (TASK-025 Central decision 2): the header carries the file-format
/// version, seed, tick count, canonical-format version, provenance metadata, an
/// optional initial-state hash, and optional per-tick checkpoint hashes; the
/// body is the ordered `RecordedCommand[]`. It does **not** serialise
/// `WorldState` — the initial state stays a named scenario / builder reference
/// (`ReplayMeta.Scenario`), the `.cwlog` model. `toReplayRecord` plugs a
/// caller-supplied initial state back in and hands the result to `Replay.run`,
/// which owns every existing validation (seed vs. initial stream, tick-0,
/// monotonic / in-range / unique-id command log).
///
/// Grammar (UTF-8, one directive per line; blank lines and `#` comment lines
/// are ignored). The six header directives appear once each, in this fixed
/// order, then an optional `initial-hash`, then zero or more `checkpoint`
/// lines, then zero or more `command` lines:
///
///   version <n>                     -- required, first non-comment line; an
///                                      unknown value is a typed error, never a
///                                      guessed migration (docs/04 section 16)
///   seed <uint64>
///   ticks <int64 >= 0>
///   canonical <n>                   -- must equal Canonical.FormatVersion
///   build <text>                    -- rest of line, trimmed, non-empty
///   scenario <text>                 -- rest of line, trimmed, non-empty
///   initial-hash 0x<16 hex>         -- optional
///   checkpoint <tick> 0x<16 hex>    -- zero or more, strictly ascending tick
///   command <deliveryTick> <sequence> <id> <issuedAtTick> <urgency> <risk> <issuer> <recipients> move <x> <y>
///                                   -- zero or more, strictly ascending
///                                      (deliveryTick, sequence)
///
/// `<urgency>` is `routine` | `immediate`; `<risk>` is `cautious` | `standard`
/// | `aggressive` (lowercase, culture-invariant). `<issuer>` is a single
/// whitespace-free token. `<recipients>` is a non-empty comma-separated list of
/// non-negative agent ids with no spaces. `move <x> <y>` is the only intent
/// today; the grammar has room for `hold` / `suppress` / `assault` / `withdraw`
/// as later intent keywords without a version bump.
///
/// Output is deterministic: fixed field order, integers only, uppercase hex,
/// `\n` line endings, commands emitted in `(RecordedCommand.Tick,
/// RecordedCommand.Sequence)` order and checkpoints in tick order. `parse` is
/// strict about that order, so `parse >> serialise` and `serialise >> parse`
/// are identity on valid input.
[<RequireQualifiedAccess>]
module ReplaySerialisation =

    /// The replay-command file-format version. Owned by this module and
    /// independent of `Replay.FormatVersion`, `CommandLog.Version`, and
    /// `Canonical.FormatVersion`. Bumping it is an explicit, rejectable change:
    /// `parse` refuses any other value with `UnsupportedFormatVersion` and does
    /// not guess a migration.
    [<Literal>]
    let FormatVersion = 1

    /// The parsed contents of a replay-command file: the header plus the
    /// ordered command log. Not a `ReplayRecord` — the initial `WorldState` is
    /// not in the file (Central decision 2); `toReplayRecord` supplies it.
    type ReplayCommandFile =
        { Version: int
          Seed: uint64
          TickCount: int64
          CanonicalFormat: int
          Meta: ReplayMeta
          /// The committed initial-state hash, when the file records one. A
          /// caller checks it against the hash of the initial state it built
          /// for `Meta.Scenario` before replaying.
          InitialHash: uint64 option
          /// Per-tick reference hashes, in ascending tick order. Empty when the
          /// file records none.
          Checkpoints: Checkpoint[]
          /// Accepted commands, ascending by (Tick, Sequence).
          Commands: RecordedCommand[] }

    /// Why a replay-command file could not be parsed. Every case names the
    /// source line (1-based) or the missing field, in the
    /// `CommandLogFile.ParseError` / `ReplayError` style. Nothing is silently
    /// repaired or migrated.
    type ParseError =
        | EmptyFile
        | ExpectedVersionFirst of line: int * found: string
        | UnsupportedFormatVersion of line: int * found: string * supported: int
        | MissingDirective of keyword: string
        | MalformedDirective of line: int * text: string * expected: string
        | MisplacedDirective of line: int * keyword: string
        | UnknownDirective of line: int * keyword: string
        | NonIntegerField of line: int * field: string * value: string
        | FieldOutOfRange of line: int * field: string * value: string
        | UnknownEnumValue of line: int * field: string * value: string
        | MalformedHash of line: int * value: string
        | EmptyRecipientList of line: int
        | CanonicalFormatMismatch of line: int * found: int * expected: int
        | CheckpointsOutOfOrder of line: int * previous: int64 * current: int64
        | CommandsOutOfOrder of line: int * previous: struct (int64 * int) * current: struct (int64 * int)
        | UnknownIntent of line: int * keyword: string

    let private hx (v: uint64) : string = sprintf "0x%016X" v

    let private urgencyText =
        function
        | Routine -> "routine"
        | Immediate -> "immediate"

    let private riskText =
        function
        | Cautious -> "cautious"
        | Standard -> "standard"
        | Aggressive -> "aggressive"

    let private intentText (intent: PlayerIntent) : string =
        match intent with
        | MoveTo target -> sprintf "move %d %d" target.X target.Y

    /// Serialises a parsed-file view to the canonical text form. Deterministic
    /// and idempotent under `parse`. Throws `invalidArg` on data the grammar
    /// cannot represent on one line: an empty or whitespace-bearing issuer, or
    /// a newline in the build / scenario metadata.
    let serialise (file: ReplayCommandFile) : string =
        if file.Meta.Build.Contains '\n' || file.Meta.Scenario.Contains '\n' then
            invalidArg (nameof file) "replay metadata (Build / Scenario) must be single-line"

        let sb = System.Text.StringBuilder()
        let line (s: string) = sb.Append(s).Append('\n') |> ignore

        line (sprintf "version %d" file.Version)
        line (sprintf "seed %d" file.Seed)
        line (sprintf "ticks %d" file.TickCount)
        line (sprintf "canonical %d" file.CanonicalFormat)
        line (sprintf "build %s" (file.Meta.Build.Trim()))
        line (sprintf "scenario %s" (file.Meta.Scenario.Trim()))

        match file.InitialHash with
        | Some h -> line (sprintf "initial-hash %s" (hx h))
        | None -> ()

        for cp in file.Checkpoints |> Array.sortBy (fun c -> c.Tick) do
            line (sprintf "checkpoint %d %s" cp.Tick (hx cp.Hash.Value))

        for c in file.Commands |> Array.sortBy (fun c -> c.Tick, c.Sequence) do
            let issuer = c.Issuer

            if issuer = "" || issuer |> Seq.exists System.Char.IsWhiteSpace then
                invalidArg (nameof file) $"command issuer must be a non-empty whitespace-free token, got '{issuer}'"

            let recipients =
                c.Command.Recipients
                |> List.map (fun a -> string (AgentId.value a))
                |> String.concat ","

            line (
                sprintf
                    "command %d %d %d %d %s %s %s %s %s"
                    c.Tick
                    c.Sequence
                    (CommandId.value c.Command.Id)
                    c.Command.IssuedAtTick
                    (urgencyText c.Command.Urgency)
                    (riskText c.Command.RiskTolerance)
                    issuer
                    recipients
                    (intentText c.Command.Intent)
            )

        sb.ToString()

    let private knownHeaderKeywords =
        set [ "version"; "seed"; "ticks"; "canonical"; "build"; "scenario"; "initial-hash" ]

    /// Parses replay-command text into a `ReplayCommandFile`, or the first
    /// structural error. Strict about directive order so the round-trip is
    /// idempotent.
    let parse (text: string) : Result<ReplayCommandFile, ParseError> =
        let normalised = text.Replace("\r\n", "\n").Replace("\r", "\n")

        let significant =
            normalised.Split('\n')
            |> Array.mapi (fun i raw -> i + 1, raw.Trim())
            |> Array.filter (fun (_, t) -> t <> "" && not (t.StartsWith "#"))

        if significant.Length = 0 then
            Error EmptyFile
        else

        let inline (>>=) (m: Result<'a, ParseError>) (f: 'a -> Result<'b, ParseError>) = Result.bind f m

        /// Splits "keyword rest-of-line" (rest trimmed; "" when keyword-only).
        let split (t: string) =
            let i = t.IndexOfAny [| ' '; '\t' |]
            if i < 0 then t, "" else t.Substring(0, i), t.Substring(i + 1).Trim()

        let ws = [| ' '; '\t' |]

        // Integer fields are parsed and (in `serialise`) formatted culture-
        // invariantly: the format is a determinism artefact and must not depend
        // on the host's current culture.
        let inv = System.Globalization.CultureInfo.InvariantCulture

        let parseI64 lineNo field (v: string) =
            match System.Int64.TryParse(v, inv) with
            | true, n -> Ok n
            | _ -> Error(NonIntegerField(lineNo, field, v))

        let parseU64 lineNo field (v: string) =
            match System.UInt64.TryParse(v, inv) with
            | true, n -> Ok n
            | _ -> Error(NonIntegerField(lineNo, field, v))

        let parseI32 lineNo field (v: string) =
            match System.Int32.TryParse(v, inv) with
            | true, n -> Ok n
            | _ -> Error(NonIntegerField(lineNo, field, v))

        let parseHash lineNo (v: string) =
            let hexPart = if v.Length >= 2 then v.Substring 2 else ""

            if
                v.Length = 18
                && (v.StartsWith "0x" || v.StartsWith "0X")
                && hexPart |> Seq.forall System.Uri.IsHexDigit
            then
                Ok(System.Convert.ToUInt64(hexPart, 16))
            else
                Error(MalformedHash(lineNo, v))

        let parseUrgency lineNo (v: string) =
            match v with
            | "routine" -> Ok Routine
            | "immediate" -> Ok Immediate
            | _ -> Error(UnknownEnumValue(lineNo, "urgency", v))

        let parseRisk lineNo (v: string) =
            match v with
            | "cautious" -> Ok Cautious
            | "standard" -> Ok Standard
            | "aggressive" -> Ok Aggressive
            | _ -> Error(UnknownEnumValue(lineNo, "risk", v))

        let parseRecipients lineNo (v: string) : Result<AgentId list, ParseError> =
            let parts = v.Split(',')

            if v = "" || parts |> Array.exists (fun p -> p = "") then
                Error(EmptyRecipientList lineNo)
            else
                (Ok [], parts)
                ||> Array.fold (fun acc p ->
                    acc
                    >>= fun ids ->
                        parseI32 lineNo "recipient" p
                        >>= fun id ->
                            if id < 0 then
                                Error(FieldOutOfRange(lineNo, "recipient", p))
                            else
                                Ok(AgentId.ofInt id :: ids))
                >>= fun rev -> Ok(List.rev rev)

        let parseIntent lineNo (toks: string[]) : Result<PlayerIntent, ParseError> =
            match toks with
            | [| "move"; xTok; yTok |] ->
                parseI32 lineNo "x" xTok
                >>= fun x -> parseI32 lineNo "y" yTok >>= fun y -> Ok(MoveTo { X = x; Y = y })
            | [||] -> Error(UnknownIntent(lineNo, ""))
            | _ -> Error(UnknownIntent(lineNo, toks.[0]))

        let commandShape =
            "command <tick> <seq> <id> <issuedAtTick> <urgency> <risk> <issuer> <recipients> move <x> <y>"

        let parseCommand lineNo (rest: string) : Result<RecordedCommand, ParseError> =
            let p = rest.Split(ws, System.StringSplitOptions.RemoveEmptyEntries)

            if p.Length < 9 then
                Error(MalformedDirective(lineNo, "command " + rest, commandShape))
            else
                parseI64 lineNo "tick" p.[0]
                >>= fun deliveryTick ->
                    parseI32 lineNo "seq" p.[1]
                    >>= fun sequence ->
                        parseI32 lineNo "id" p.[2]
                        >>= fun id ->
                            parseI64 lineNo "issuedAtTick" p.[3]
                            >>= fun issuedAt ->
                                parseUrgency lineNo p.[4]
                                >>= fun urgency ->
                                    parseRisk lineNo p.[5]
                                    >>= fun risk ->
                                        parseRecipients lineNo p.[7]
                                        >>= fun recipients ->
                                            parseIntent lineNo p.[8..]
                                            >>= fun intent ->
                                                if sequence < 0 then
                                                    Error(FieldOutOfRange(lineNo, "seq", p.[1]))
                                                elif id < 0 then
                                                    Error(FieldOutOfRange(lineNo, "id", p.[2]))
                                                else
                                                    Ok
                                                        { Tick = deliveryTick
                                                          Sequence = sequence
                                                          Command =
                                                            { Id = CommandId.ofInt id
                                                              IssuedAtTick = issuedAt
                                                              Recipients = recipients
                                                              Urgency = urgency
                                                              RiskTolerance = risk
                                                              Intent = intent }
                                                          Issuer = p.[6] }

        let headerLine idx (keyword: string) : Result<int * string, ParseError> =
            if idx >= significant.Length then
                Error(MissingDirective keyword)
            else
                let lineNo, t = significant.[idx]
                let kw, rest = split t

                if kw = keyword then Ok(lineNo, rest)
                elif idx = 0 then Error(ExpectedVersionFirst(lineNo, t))
                else Error(MalformedDirective(lineNo, t, sprintf "%s <value>" keyword))

        let requireText idx keyword : Result<int * string, ParseError> =
            headerLine idx keyword
            >>= fun (lineNo, rest) ->
                if rest = "" then
                    Error(MalformedDirective(lineNo, keyword, sprintf "%s <text>" keyword))
                else
                    Ok(lineNo, rest)

        /// The optional `initial-hash`, then `checkpoint*`, then `command*`.
        let parseBody () : Result<uint64 option * Checkpoint[] * RecordedCommand[], ParseError> =
            let mutable i = 6
            let mutable err: ParseError option = None
            let mutable initialHash: uint64 option = None
            let checkpoints = ResizeArray<Checkpoint>()
            let commands = ResizeArray<RecordedCommand>()
            let mutable lastCp: int64 option = None
            let mutable lastCmd: struct (int64 * int) option = None

            if i < significant.Length then
                let lineNo, t = significant.[i]
                let kw, rest = split t

                if kw = "initial-hash" then
                    match parseHash lineNo rest with
                    | Ok h ->
                        initialHash <- Some h
                        i <- i + 1
                    | Error e -> err <- Some e

            while err.IsNone && i < significant.Length do
                let lineNo, t = significant.[i]
                let kw, rest = split t

                match kw with
                | "checkpoint" when commands.Count > 0 ->
                    err <- Some(MisplacedDirective(lineNo, "checkpoint"))
                | "checkpoint" ->
                    match rest.Split(ws, System.StringSplitOptions.RemoveEmptyEntries) with
                    | [| tickTok; hashTok |] ->
                        match parseI64 lineNo "checkpoint tick" tickTok, parseHash lineNo hashTok with
                        | Ok tick, Ok h ->
                            match lastCp with
                            | Some p when tick <= p -> err <- Some(CheckpointsOutOfOrder(lineNo, p, tick))
                            | _ ->
                                lastCp <- Some tick

                                checkpoints.Add
                                    { Tick = tick
                                      Hash = { Format = Canonical.FormatVersion; Value = h } }
                        | Error e, _
                        | _, Error e -> err <- Some e
                    | _ -> err <- Some(MalformedDirective(lineNo, t, "checkpoint <tick> 0x<hash>"))
                | "command" ->
                    match parseCommand lineNo rest with
                    | Error e -> err <- Some e
                    | Ok rc ->
                        let key = struct (rc.Tick, rc.Sequence)

                        match lastCmd with
                        | Some(struct (pt, ps)) when not ((pt, ps) < (rc.Tick, rc.Sequence)) ->
                            err <- Some(CommandsOutOfOrder(lineNo, struct (pt, ps), key))
                        | _ ->
                            lastCmd <- Some key
                            commands.Add rc
                | "initial-hash" -> err <- Some(MisplacedDirective(lineNo, "initial-hash"))
                | other when knownHeaderKeywords.Contains other -> err <- Some(MisplacedDirective(lineNo, other))
                | other -> err <- Some(UnknownDirective(lineNo, other))

                i <- i + 1

            match err with
            | Some e -> Error e
            | None -> Ok(initialHash, checkpoints.ToArray(), commands.ToArray())

        headerLine 0 "version"
        >>= fun (l0, r0) ->
            (match System.Int32.TryParse(r0, inv) with
             | true, v when v = FormatVersion -> Ok()
             | _ -> Error(UnsupportedFormatVersion(l0, r0, FormatVersion)))
            >>= fun () ->
                headerLine 1 "seed"
                >>= fun (l1, r1) ->
                    parseU64 l1 "seed" r1
                    >>= fun seed ->
                        headerLine 2 "ticks"
                        >>= fun (l2, r2) ->
                            parseI64 l2 "ticks" r2
                            >>= fun ticks ->
                                (if ticks < 0L then
                                     Error(FieldOutOfRange(l2, "ticks", r2))
                                 else
                                     Ok ticks)
                                >>= fun ticks ->
                                    headerLine 3 "canonical"
                                    >>= fun (l3, r3) ->
                                        parseI32 l3 "canonical" r3
                                        >>= fun canon ->
                                            (if canon <> Canonical.FormatVersion then
                                                 Error(CanonicalFormatMismatch(l3, canon, Canonical.FormatVersion))
                                             else
                                                 Ok canon)
                                            >>= fun canon ->
                                                requireText 4 "build"
                                                >>= fun (_, build) ->
                                                    requireText 5 "scenario"
                                                    >>= fun (_, scenario) ->
                                                        parseBody ()
                                                        >>= fun (initialHash, checkpoints, commands) ->
                                                            Ok
                                                                { Version = FormatVersion
                                                                  Seed = seed
                                                                  TickCount = ticks
                                                                  CanonicalFormat = canon
                                                                  Meta = { Build = build; Scenario = scenario }
                                                                  InitialHash = initialHash
                                                                  Checkpoints = checkpoints
                                                                  Commands = commands }

    /// Human-readable rendering of a parse error, including the line number.
    let describeError (e: ParseError) : string =
        match e with
        | EmptyFile -> "the file has no directives"
        | ExpectedVersionFirst(line, found) -> $"line {line}: expected 'version {FormatVersion}' first, found '{found}'"
        | UnsupportedFormatVersion(line, found, supported) ->
            $"line {line}: unsupported replay-command format version '{found}', this build supports {supported} (no migration is attempted)"
        | MissingDirective keyword -> $"missing required '{keyword}' directive"
        | MalformedDirective(line, text, expected) -> $"line {line}: malformed directive '{text}', expected '{expected}'"
        | MisplacedDirective(line, keyword) -> $"line {line}: '{keyword}' directive is out of place"
        | UnknownDirective(line, keyword) -> $"line {line}: unknown directive '{keyword}'"
        | NonIntegerField(line, field, value) -> $"line {line}: field '{field}' is not an integer: '{value}'"
        | FieldOutOfRange(line, field, value) -> $"line {line}: field '{field}' is out of range: '{value}'"
        | UnknownEnumValue(line, field, value) -> $"line {line}: field '{field}' has an unknown value '{value}'"
        | MalformedHash(line, value) -> $"line {line}: expected a 0x-prefixed 16-digit hex hash, got '{value}'"
        | EmptyRecipientList line -> $"line {line}: the recipient list is empty"
        | CanonicalFormatMismatch(line, found, expected) ->
            $"line {line}: canonical format {found} does not match this build's {expected}"
        | CheckpointsOutOfOrder(line, previous, current) ->
            $"line {line}: checkpoint ticks must strictly ascend: {previous} then {current}"
        | CommandsOutOfOrder(line, struct (pt, ps), struct (ct, cs)) ->
            $"line {line}: commands must strictly ascend by (tick, sequence): ({pt},{ps}) then ({ct},{cs})"
        | UnknownIntent(line, keyword) -> $"line {line}: unknown intent '{keyword}', only 'move <x> <y>' is supported"

    /// The serialisable view of a replay record, recording the hash of its
    /// initial state so a reader can detect a mismatched scenario builder.
    let ofRecord (record: ReplayRecord) : ReplayCommandFile =
        { Version = FormatVersion
          Seed = record.Seed
          TickCount = record.TickCount
          CanonicalFormat = record.CanonicalFormat
          Meta = record.Meta
          InitialHash = Some (Hashing.hash record.InitialState).Value
          Checkpoints = record.Checkpoints
          Commands = record.Log.Commands }

    /// Builds a `ReplayRecord` from a parsed file plus a caller-supplied initial
    /// state (resolved from `file.Meta.Scenario`). The container / log /
    /// canonical versions come from the current build; `Replay.run` then
    /// validates the seed against the initial stream and the command log. The
    /// caller is responsible for checking `file.InitialHash` against `initial`.
    let toReplayRecord (initial: WorldState) (file: ReplayCommandFile) : ReplayRecord =
        { Version = Replay.FormatVersion
          CanonicalFormat = Canonical.FormatVersion
          Meta = file.Meta
          Seed = file.Seed
          InitialState = initial
          TickCount = file.TickCount
          Log = { Version = CommandLog.Version; Commands = file.Commands }
          Checkpoints = file.Checkpoints }
