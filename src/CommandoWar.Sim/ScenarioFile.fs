namespace CommandoWar.Sim

/// The `.cwscenario` content file format (TASK-060, backlog B-024): a
/// deterministic, line-based text grammar for `RawScenario`, in the
/// `ReplaySerialisation.fs` style — not JSON, not any general-purpose
/// serialisation library, matching this project's established convention
/// for authored/recorded content (`.cwlog`, `.cwreplay`).
///
/// This module is a *reader/writer* for `RawScenario` only. It performs no
/// content validation itself — a parsed `RawScenario` still goes through
/// `Scenario.validate` exactly as a code-constructed one does. Malformed
/// *grammar* (a bad directive, a non-integer field, a stray line) is a
/// `ScenarioFileError`; a well-formed but semantically invalid scenario
/// (an unresolved reference, an out-of-map cell) is a `ScenarioError`,
/// reported only after a successful parse.
///
/// Grammar (UTF-8, one directive per line; blank lines and `#` comment
/// lines are ignored). Five fixed header directives appear first, in this
/// order, then any number of body directives in any order:
///
///   version <n>                                -- required, first
///                                                  non-comment line; this
///                                                  file grammar's own
///                                                  version, independent of
///                                                  `ScenarioContent.Version`
///                                                  (docs/04 section 16: no
///                                                  migration is attempted)
///   content-version <n>                        -- `RawScenario.ContentVersion`
///   id <text>                                  -- rest of line, trimmed
///   map <width> <height>
///   fail-on-friendly-eliminated <true|false>
///
///   headquarters <x> <y>                       -- optional, at most once
///   terrain <width> <height>                   -- optional, at most once;
///                                                  opens the terrain block
///   terrain-cell <x> <y> <class> <elevation> <moveCost> <opaque>
///                                               -- only after `terrain`
///   terrain-cover <x> <y> <direction> <level>  -- only after `terrain`
///   unit-type <id> <moveSpeed>
///   formation <id> <dx,dy;dx,dy;...>           -- semicolon-separated
///                                                  comma pairs, >= 1 pair
///   friendly <agentId> <x> <y> <comms> <discipline> <unitType> <formationId> <slotIndex>
///   enemy <agentId> <x> <y> <comms> <discipline> <unitType> <formationId> <slotIndex>
///   objective-area <areaId> <x> <y>
///   extraction-area <areaId> <x> <y>
///   resupply-area <areaId> <x> <y>
///   target <targetId> <x> <y>
///   objective <id> <kind> <areaRef> <targetRef> <holdTicks> <optional> <extractAgentIds>
///                                               -- <extractAgentIds> is
///                                                  `-` (empty) or a
///                                                  comma-separated int list
///   jammer <x> <y> <radius> <activeFromTick> <activeUntilTick>
///
/// `<comms>`/`<opaque>`/`<optional>` are `true`|`false`. `<class>` is
/// `passable`|`impassable`|any other token (an unknown class is legal
/// *grammar*, only invalid *content* — `Scenario.validate` reports it, not
/// this parser). `<direction>` is `north`|`east`|`south`|`west`|any other
/// token, the same way. A single-token id/reference field (`<areaId>`,
/// `<targetId>`, `<unitType>`, `<formationId>`, `<kind>`, `RawTerrainCell.
/// Class`, `RawCoverFeature.Direction`) must be one whitespace-free token
/// and must not be the literal `-` (reserved for "absent"/"blank" —
/// `<formationId>`, `<areaRef>`, `<targetRef>` use it that way explicitly).
/// `id <text>` is the one field allowed spaces, since it is always the
/// last thing on its own line.
///
/// Output is deterministic: fixed header order, then body directives in
/// `RawScenario`'s own field/array order (so parsing back reproduces
/// identical arrays), integers only (culture-invariant), `\n` line
/// endings. `serialise >> parse` and `parse >> serialise` are identity on
/// every value `serialise` accepts without `invalidArg` (an unrepresentable
/// value — a reserved-token id, a whitespace-bearing single-token field, a
/// newline in `Id` — throws immediately, the `ReplaySerialisation.
/// serialise` precedent).
[<RequireQualifiedAccess>]
module ScenarioFile =

    /// The `.cwscenario` file-format version. Owned by this module,
    /// independent of `ScenarioContent.Version` (the authored-shape/
    /// validation-contract version `Scenario.validate` checks once parsing
    /// succeeds). Bumping it is an explicit, rejectable change: `parse`
    /// refuses any other value with `UnsupportedFormatVersion` and does not
    /// guess a migration.
    [<Literal>]
    let FormatVersion = 1

    /// Why a `.cwscenario` file could not be parsed. Every case names the
    /// source line (1-based) or the missing directive, in the
    /// `ReplaySerialisation.ParseError` style. This is a grammar fault, not
    /// a content fault — a well-formed file with invalid scenario content
    /// parses successfully and is rejected later by `Scenario.validate`.
    type ScenarioFileError =
        | EmptyFile
        | ExpectedVersionFirst of line: int * found: string
        | UnsupportedFormatVersion of line: int * found: string * supported: int
        | MissingDirective of keyword: string
        | MalformedDirective of line: int * text: string * expected: string
        | DuplicateDirective of line: int * keyword: string
        | OrphanedTerrainDirective of line: int * keyword: string
        | UnknownDirective of line: int * keyword: string
        | NonIntegerField of line: int * field: string * value: string
        | UnknownBoolValue of line: int * field: string * value: string
        | InvalidToken of line: int * field: string * value: string

    let private inv = System.Globalization.CultureInfo.InvariantCulture
    let private ws = [| ' '; '\t' |]

    /// A reserved sentinel meaning "absent"/"blank" for an optional
    /// single-token reference field (`FormationId`, `AreaRef`, `TargetRef`).
    [<Literal>]
    let private Blank = "-"

    // --- serialise -----------------------------------------------------

    /// A single-token field (an id or reference) as written to the file.
    /// Throws `invalidArg` if `s` contains whitespace or is the reserved
    /// `-` sentinel — that value cannot be represented in this grammar
    /// (the `ReplaySerialisation.serialise` issuer-token precedent).
    let private token (field: string) (s: string) : string =
        if s = Blank then
            invalidArg field $"'{field}' must not be the reserved token '-'"
        elif s |> Seq.exists System.Char.IsWhiteSpace then
            invalidArg field $"'{field}' must be a single whitespace-free token, got '{s}'"
        else
            s

    /// An optional single-token reference: blank means "absent", written as
    /// the reserved `-` sentinel; anything else goes through `token`.
    let private optionalToken (field: string) (s: string) : string =
        if System.String.IsNullOrEmpty s then Blank else token field s

    let private boolText b = if b then "true" else "false"

    let private cellText (c: Cell) = $"{c.X} {c.Y}"

    let private intListText (xs: int[]) : string =
        if Array.isEmpty xs then
            Blank
        else
            xs |> Array.map string |> String.concat ","

    let private offsetsText (offsets: Cell[]) : string =
        offsets |> Array.map (fun c -> $"{c.X},{c.Y}") |> String.concat ";"

    /// Serialises a `RawScenario` to `.cwscenario` text. Deterministic and
    /// idempotent under `parse`. Throws `invalidArg` on data the grammar
    /// cannot represent on one line (a reserved-token or whitespace-bearing
    /// single-token field, a newline in `Id`).
    let serialise (raw: RawScenario) : string =
        if raw.Id.Contains '\n' then
            invalidArg (nameof raw) "RawScenario.Id must be single-line"

        let sb = System.Text.StringBuilder()
        let line (s: string) = sb.Append(s).Append('\n') |> ignore

        line $"version {FormatVersion}"
        line $"content-version {raw.ContentVersion}"
        line $"id {raw.Id.Trim()}"
        line $"map {raw.Width} {raw.Height}"
        line $"fail-on-friendly-eliminated {boolText raw.FailOnFriendlyForceEliminated}"

        match raw.Headquarters with
        | Some c -> line $"headquarters {cellText c}"
        | None -> ()

        match raw.TerrainLayer with
        | Some layer ->
            line $"terrain {layer.Width} {layer.Height}"

            for tc in layer.Cells do
                let cls = token "Class" tc.Class
                line $"terrain-cell {cellText tc.Cell} {cls} {tc.Elevation} {tc.MoveCost} {boolText tc.Opaque}"

            for cf in layer.Cover do
                let dir = token "Direction" cf.Direction
                line $"terrain-cover {cellText cf.Cell} {dir} {cf.Level}"
        | None -> ()

        for u in raw.UnitTypes do
            let id = token "UnitType.Id" u.Id
            line $"unit-type {id} {u.MoveSpeed}"

        for f in raw.Formations do
            let id = token "FormationId" f.Id
            line $"formation {id} {offsetsText f.Offsets}"

        let deploymentLine keyword (d: RawDeployment) =
            let unitType = token "UnitType" d.UnitType
            let formationId = optionalToken "FormationId" d.FormationId

            line
                $"{keyword} {d.AgentId} {cellText d.Cell} {boolText d.CommunicationAvailable} {d.Discipline} {unitType} {formationId} {d.SlotIndex}"

        for d in raw.FriendlyDeployments do
            deploymentLine "friendly" d

        for d in raw.EnemyDeployments do
            deploymentLine "enemy" d

        for a in raw.ObjectiveAreas do
            let id = token "AreaId" a.AreaId
            line $"objective-area {id} {cellText a.Cell}"

        for a in raw.ExtractionAreas do
            let id = token "AreaId" a.AreaId
            line $"extraction-area {id} {cellText a.Cell}"

        for a in raw.ResupplyAreas do
            let id = token "AreaId" a.AreaId
            line $"resupply-area {id} {cellText a.Cell}"

        for t in raw.StaticTargets do
            let id = token "TargetId" t.TargetId
            line $"target {id} {cellText t.Cell}"

        for o in raw.Objectives do
            let kind = token "Kind" o.Kind
            let areaRef = optionalToken "AreaRef" o.AreaRef
            let targetRef = optionalToken "TargetRef" o.TargetRef

            line
                $"objective {o.Id} {kind} {areaRef} {targetRef} {o.HoldTicks} {boolText o.IsOptional} {intListText o.ExtractAgentIds}"

        for j in raw.Jammers do
            line $"jammer {cellText j.Position} {j.Radius} {j.ActiveFromTick} {j.ActiveUntilTick}"

        sb.ToString()

    // --- parse -----------------------------------------------------------

    let inline private (>>=) (m: Result<'a, ScenarioFileError>) (f: 'a -> Result<'b, ScenarioFileError>) =
        Result.bind f m

    let private parseI32 lineNo field (v: string) =
        match System.Int32.TryParse(v, inv) with
        | true, n -> Ok n
        | _ -> Error(NonIntegerField(lineNo, field, v))

    let private parseI64 lineNo field (v: string) =
        match System.Int64.TryParse(v, inv) with
        | true, n -> Ok n
        | _ -> Error(NonIntegerField(lineNo, field, v))

    let private parseBool lineNo field (v: string) =
        match v with
        | "true" -> Ok true
        | "false" -> Ok false
        | _ -> Error(UnknownBoolValue(lineNo, field, v))

    let private parseCell lineNo field (xTok: string) (yTok: string) : Result<Cell, ScenarioFileError> =
        parseI32 lineNo (field + ".X") xTok
        >>= fun x -> parseI32 lineNo (field + ".Y") yTok >>= fun y -> Ok { X = x; Y = y }

    /// A single-token field: rejects an empty token or the reserved `-`
    /// sentinel (the caller decides whether `-` is legal for this field by
    /// using `parseOptionalToken` instead).
    let private parseToken lineNo field (v: string) : Result<string, ScenarioFileError> =
        if v = "" || v = Blank then
            Error(InvalidToken(lineNo, field, v))
        else
            Ok v

    let private parseOptionalToken lineNo field (v: string) : Result<string, ScenarioFileError> =
        if v = Blank then Ok "" else parseToken lineNo field v

    let private parseIntList lineNo field (v: string) : Result<int[], ScenarioFileError> =
        if v = Blank then
            Ok [||]
        else
            let parts = v.Split(',')

            (Ok [], parts)
            ||> Array.fold (fun acc p -> acc >>= fun xs -> parseI32 lineNo field p >>= fun n -> Ok(n :: xs))
            >>= fun rev -> Ok(rev |> List.rev |> List.toArray)

    let private parseOffsets lineNo (v: string) : Result<Cell[], ScenarioFileError> =
        let expected = "<dx,dy;dx,dy;...>"

        if v = "" then
            Error(MalformedDirective(lineNo, "formation", expected))
        else
            let pairs = v.Split ';'

            (Ok [], pairs)
            ||> Array.fold (fun acc pair ->
                acc
                >>= fun xs ->
                    match pair.Split ',' with
                    | [| xt; yt |] -> parseCell lineNo "Offset" xt yt >>= fun c -> Ok(c :: xs)
                    | _ -> Error(MalformedDirective(lineNo, pair, "dx,dy")))
            >>= fun rev -> Ok(rev |> List.rev |> List.toArray)

    /// Splits a significant line into its keyword and the (untrimmed
    /// leading-space-stripped) remainder. `id`'s remainder is used as-is
    /// (allowed to contain internal spaces); every other directive splits
    /// its remainder on whitespace.
    let private split (t: string) =
        let i = t.IndexOfAny ws
        if i < 0 then t, "" else t.Substring(0, i), t.Substring(i + 1).Trim()

    let private headerLine idx keyword (significant: (int * string)[]) : Result<int * string, ScenarioFileError> =
        if idx >= significant.Length then
            Error(MissingDirective keyword)
        else
            let lineNo, t = significant.[idx]
            let kw, rest = split t

            if kw = keyword then Ok(lineNo, rest)
            elif idx = 0 then Error(ExpectedVersionFirst(lineNo, t))
            else Error(MalformedDirective(lineNo, t, $"{keyword} <value>"))

    /// The body loop: every directive after the five-line fixed header, in
    /// any order (`headquarters`/`terrain` at most once each; `terrain-cell`/
    /// `terrain-cover` only once `terrain` has been seen).
    let private parseBody
        (significant: (int * string)[])
        (contentVersion: int)
        (scenarioId: string)
        (width: int)
        (height: int)
        (failOnEliminated: bool)
        : Result<RawScenario, ScenarioFileError> =
        let mutable i = 5
        let mutable err: ScenarioFileError option = None
        let mutable headquarters: Cell option = None
        let mutable terrainDims: (int * int) option = None
        let terrainCells = ResizeArray<RawTerrainCell>()
        let terrainCover = ResizeArray<RawCoverFeature>()
        let unitTypes = ResizeArray<RawUnitType>()
        let formations = ResizeArray<RawFormation>()
        let friendly = ResizeArray<RawDeployment>()
        let enemy = ResizeArray<RawDeployment>()
        let objectiveAreas = ResizeArray<RawArea>()
        let extractionAreas = ResizeArray<RawArea>()
        let resupplyAreas = ResizeArray<RawArea>()
        let targets = ResizeArray<RawTarget>()
        let objectives = ResizeArray<RawObjective>()
        let jammers = ResizeArray<RawJammer>()

        let setErr e =
            if err.IsNone then
                err <- Some e

        let parseDeployment lineNo (rest: string) : Result<RawDeployment, ScenarioFileError> =
            match rest.Split(ws, System.StringSplitOptions.RemoveEmptyEntries) with
            | [| idTok; xTok; yTok; commsTok; discTok; unitTok; formTok; slotTok |] ->
                parseI32 lineNo "AgentId" idTok
                >>= fun agentId ->
                    parseCell lineNo "Cell" xTok yTok
                    >>= fun cell ->
                        parseBool lineNo "CommunicationAvailable" commsTok
                        >>= fun comms ->
                            parseI32 lineNo "Discipline" discTok
                            >>= fun discipline ->
                                parseToken lineNo "UnitType" unitTok
                                >>= fun unitType ->
                                    parseOptionalToken lineNo "FormationId" formTok
                                    >>= fun formationId ->
                                        parseI32 lineNo "SlotIndex" slotTok
                                        >>= fun slotIndex ->
                                            Ok
                                                { AgentId = agentId
                                                  Cell = cell
                                                  CommunicationAvailable = comms
                                                  Discipline = discipline
                                                  UnitType = unitType
                                                  FormationId = formationId
                                                  SlotIndex = slotIndex }
            | _ ->
                Error(
                    MalformedDirective(
                        lineNo,
                        rest,
                        "<agentId> <x> <y> <comms> <discipline> <unitType> <formationId> <slotIndex>"
                    )
                )

        let parseArea lineNo keyword (rest: string) : Result<RawArea, ScenarioFileError> =
            match rest.Split(ws, System.StringSplitOptions.RemoveEmptyEntries) with
            | [| idTok; xTok; yTok |] ->
                parseToken lineNo "AreaId" idTok
                >>= fun areaId ->
                    parseCell lineNo "Cell" xTok yTok >>= fun cell -> Ok({ AreaId = areaId; Cell = cell }: RawArea)
            | _ -> Error(MalformedDirective(lineNo, rest, $"{keyword} <areaId> <x> <y>"))

        while err.IsNone && i < significant.Length do
            let lineNo, t = significant.[i]
            let kw, rest = split t

            match kw with
            | "headquarters" when headquarters.IsSome -> setErr (DuplicateDirective(lineNo, kw))
            | "headquarters" ->
                match rest.Split(ws, System.StringSplitOptions.RemoveEmptyEntries) with
                | [| xTok; yTok |] ->
                    match parseCell lineNo "Headquarters" xTok yTok with
                    | Ok c -> headquarters <- Some c
                    | Error e -> setErr e
                | _ -> setErr (MalformedDirective(lineNo, rest, "headquarters <x> <y>"))
            | "terrain" when terrainDims.IsSome -> setErr (DuplicateDirective(lineNo, kw))
            | "terrain" ->
                match rest.Split(ws, System.StringSplitOptions.RemoveEmptyEntries) with
                | [| wTok; hTok |] ->
                    match parseI32 lineNo "Width" wTok, parseI32 lineNo "Height" hTok with
                    | Ok w, Ok h -> terrainDims <- Some(w, h)
                    | Error e, _
                    | _, Error e -> setErr e
                | _ -> setErr (MalformedDirective(lineNo, rest, "terrain <width> <height>"))
            | "terrain-cell" when terrainDims.IsNone -> setErr (OrphanedTerrainDirective(lineNo, kw))
            | "terrain-cell" ->
                match rest.Split(ws, System.StringSplitOptions.RemoveEmptyEntries) with
                | [| xTok; yTok; classTok; elevTok; costTok; opaqueTok |] ->
                    match
                        parseCell lineNo "Cell" xTok yTok,
                        parseToken lineNo "Class" classTok,
                        parseI32 lineNo "Elevation" elevTok,
                        parseI32 lineNo "MoveCost" costTok,
                        parseBool lineNo "Opaque" opaqueTok
                    with
                    | Ok cell, Ok cls, Ok elev, Ok cost, Ok opaque ->
                        terrainCells.Add
                            { Cell = cell
                              Class = cls
                              Elevation = elev
                              MoveCost = cost
                              Opaque = opaque }
                    | Error e, _, _, _, _
                    | _, Error e, _, _, _
                    | _, _, Error e, _, _
                    | _, _, _, Error e, _
                    | _, _, _, _, Error e -> setErr e
                | _ ->
                    setErr (
                        MalformedDirective(lineNo, rest, "terrain-cell <x> <y> <class> <elevation> <moveCost> <opaque>")
                    )
            | "terrain-cover" when terrainDims.IsNone -> setErr (OrphanedTerrainDirective(lineNo, kw))
            | "terrain-cover" ->
                match rest.Split(ws, System.StringSplitOptions.RemoveEmptyEntries) with
                | [| xTok; yTok; dirTok; levelTok |] ->
                    match
                        parseCell lineNo "Cell" xTok yTok, parseToken lineNo "Direction" dirTok, parseI32 lineNo "Level" levelTok
                    with
                    | Ok cell, Ok dir, Ok level ->
                        terrainCover.Add
                            { Cell = cell
                              Direction = dir
                              Level = level }
                    | Error e, _, _
                    | _, Error e, _
                    | _, _, Error e -> setErr e
                | _ -> setErr (MalformedDirective(lineNo, rest, "terrain-cover <x> <y> <direction> <level>"))
            | "unit-type" ->
                match rest.Split(ws, System.StringSplitOptions.RemoveEmptyEntries) with
                | [| idTok; speedTok |] ->
                    match parseToken lineNo "UnitType.Id" idTok, parseI32 lineNo "MoveSpeed" speedTok with
                    | Ok id, Ok speed -> unitTypes.Add { Id = id; MoveSpeed = speed }
                    | Error e, _
                    | _, Error e -> setErr e
                | _ -> setErr (MalformedDirective(lineNo, rest, "unit-type <id> <moveSpeed>"))
            | "formation" ->
                match rest.IndexOfAny ws with
                | idx when idx > 0 ->
                    let idTok = rest.Substring(0, idx)
                    let offsetsTok = rest.Substring(idx + 1).Trim()

                    match parseToken lineNo "FormationId" idTok, parseOffsets lineNo offsetsTok with
                    | Ok id, Ok offsets -> formations.Add { Id = id; Offsets = offsets }
                    | Error e, _
                    | _, Error e -> setErr e
                | _ -> setErr (MalformedDirective(lineNo, rest, "formation <id> <dx,dy;dx,dy;...>"))
            | "friendly" ->
                match parseDeployment lineNo rest with
                | Ok d -> friendly.Add d
                | Error e -> setErr e
            | "enemy" ->
                match parseDeployment lineNo rest with
                | Ok d -> enemy.Add d
                | Error e -> setErr e
            | "objective-area" ->
                match parseArea lineNo kw rest with
                | Ok a -> objectiveAreas.Add a
                | Error e -> setErr e
            | "extraction-area" ->
                match parseArea lineNo kw rest with
                | Ok a -> extractionAreas.Add a
                | Error e -> setErr e
            | "resupply-area" ->
                match parseArea lineNo kw rest with
                | Ok a -> resupplyAreas.Add a
                | Error e -> setErr e
            | "target" ->
                match rest.Split(ws, System.StringSplitOptions.RemoveEmptyEntries) with
                | [| idTok; xTok; yTok |] ->
                    match parseToken lineNo "TargetId" idTok, parseCell lineNo "Cell" xTok yTok with
                    | Ok id, Ok cell -> targets.Add { TargetId = id; Cell = cell }
                    | Error e, _
                    | _, Error e -> setErr e
                | _ -> setErr (MalformedDirective(lineNo, rest, "target <targetId> <x> <y>"))
            | "objective" ->
                match rest.Split(ws, System.StringSplitOptions.RemoveEmptyEntries) with
                | [| idTok; kindTok; areaTok; targetTok; holdTok; optTok; extractTok |] ->
                    match
                        parseI32 lineNo "Id" idTok,
                        parseToken lineNo "Kind" kindTok,
                        parseOptionalToken lineNo "AreaRef" areaTok,
                        parseOptionalToken lineNo "TargetRef" targetTok,
                        parseI32 lineNo "HoldTicks" holdTok,
                        parseBool lineNo "IsOptional" optTok,
                        parseIntList lineNo "ExtractAgentIds" extractTok
                    with
                    | Ok oid, Ok kind, Ok areaRef, Ok targetRef, Ok hold, Ok isOpt, Ok extractIds ->
                        objectives.Add
                            { Id = oid
                              Kind = kind
                              AreaRef = areaRef
                              TargetRef = targetRef
                              HoldTicks = hold
                              ExtractAgentIds = extractIds
                              IsOptional = isOpt }
                    | Error e, _, _, _, _, _, _
                    | _, Error e, _, _, _, _, _
                    | _, _, Error e, _, _, _, _
                    | _, _, _, Error e, _, _, _
                    | _, _, _, _, Error e, _, _
                    | _, _, _, _, _, Error e, _
                    | _, _, _, _, _, _, Error e -> setErr e
                | _ ->
                    setErr (
                        MalformedDirective(
                            lineNo,
                            rest,
                            "objective <id> <kind> <areaRef> <targetRef> <holdTicks> <optional> <extractAgentIds>"
                        )
                    )
            | "jammer" ->
                match rest.Split(ws, System.StringSplitOptions.RemoveEmptyEntries) with
                | [| xTok; yTok; radTok; fromTok; untilTok |] ->
                    match
                        parseCell lineNo "Position" xTok yTok,
                        parseI32 lineNo "Radius" radTok,
                        parseI64 lineNo "ActiveFromTick" fromTok,
                        parseI64 lineNo "ActiveUntilTick" untilTok
                    with
                    | Ok pos, Ok radius, Ok fromTick, Ok untilTick ->
                        jammers.Add
                            { Position = pos
                              Radius = radius
                              ActiveFromTick = fromTick
                              ActiveUntilTick = untilTick }
                    | Error e, _, _, _
                    | _, Error e, _, _
                    | _, _, Error e, _
                    | _, _, _, Error e -> setErr e
                | _ ->
                    setErr (MalformedDirective(lineNo, rest, "jammer <x> <y> <radius> <activeFromTick> <activeUntilTick>"))
            | "version"
            | "content-version"
            | "id"
            | "map"
            | "fail-on-friendly-eliminated" -> setErr (DuplicateDirective(lineNo, kw))
            | other -> setErr (UnknownDirective(lineNo, other))

            i <- i + 1

        match err with
        | Some e -> Error e
        | None ->
            let terrainLayer =
                match terrainDims with
                | None -> None
                | Some(w, h) ->
                    Some
                        { Width = w
                          Height = h
                          Cells = terrainCells.ToArray()
                          Cover = terrainCover.ToArray() }

            Ok
                { ContentVersion = contentVersion
                  Id = scenarioId
                  Width = width
                  Height = height
                  FriendlyDeployments = friendly.ToArray()
                  EnemyDeployments = enemy.ToArray()
                  ObjectiveAreas = objectiveAreas.ToArray()
                  ExtractionAreas = extractionAreas.ToArray()
                  ResupplyAreas = resupplyAreas.ToArray()
                  StaticTargets = targets.ToArray()
                  Objectives = objectives.ToArray()
                  TerrainLayer = terrainLayer
                  UnitTypes = unitTypes.ToArray()
                  Headquarters = headquarters
                  Jammers = jammers.ToArray()
                  Formations = formations.ToArray()
                  FailOnFriendlyForceEliminated = failOnEliminated }

    /// Parses `.cwscenario` text into a `RawScenario`, or the first
    /// structural error encountered (the `ReplaySerialisation.parse`
    /// short-circuit style). Performs no content validation — the caller
    /// runs `Scenario.validate` on the result.
    let parse (text: string) : Result<RawScenario, ScenarioFileError> =
        let normalised = text.Replace("\r\n", "\n").Replace("\r", "\n")

        let significant =
            normalised.Split('\n')
            |> Array.mapi (fun i raw -> i + 1, raw.Trim())
            |> Array.filter (fun (_, t) -> t <> "" && not (t.StartsWith "#"))

        if significant.Length = 0 then
            Error EmptyFile
        else

            headerLine 0 "version" significant
            >>= fun (l0, r0) ->
                (match System.Int32.TryParse(r0, inv) with
                 | true, v when v = FormatVersion -> Ok()
                 | _ -> Error(UnsupportedFormatVersion(l0, r0, FormatVersion)))
                >>= fun () ->
                    headerLine 1 "content-version" significant
                    >>= fun (l1, r1) ->
                        parseI32 l1 "content-version" r1
                        >>= fun contentVersion ->
                            headerLine 2 "id" significant
                            >>= fun (l2, r2) ->
                                (if r2 = "" then
                                     Error(MalformedDirective(l2, "id", "id <text>"))
                                 else
                                     Ok r2)
                                >>= fun scenarioId ->
                                    headerLine 3 "map" significant
                                    >>= fun (l3, r3) ->
                                        (match r3.Split(ws, System.StringSplitOptions.RemoveEmptyEntries) with
                                         | [| wTok; hTok |] ->
                                             parseI32 l3 "Width" wTok
                                             >>= fun w -> parseI32 l3 "Height" hTok >>= fun h -> Ok(w, h)
                                         | _ -> Error(MalformedDirective(l3, r3, "map <width> <height>")))
                                        >>= fun (width, height) ->
                                            headerLine 4 "fail-on-friendly-eliminated" significant
                                            >>= fun (l4, r4) ->
                                                parseBool l4 "fail-on-friendly-eliminated" r4
                                                >>= fun failOnEliminated ->
                                                    parseBody significant contentVersion scenarioId width height failOnEliminated

    /// Human-readable rendering of a parse error, including the line number.
    let describeError (e: ScenarioFileError) : string =
        match e with
        | EmptyFile -> "the file has no directives"
        | ExpectedVersionFirst(line, found) -> $"line {line}: expected 'version {FormatVersion}' first, found '{found}'"
        | UnsupportedFormatVersion(line, found, supported) ->
            $"line {line}: unsupported .cwscenario format version '{found}', this build supports {supported} (no migration is attempted)"
        | MissingDirective keyword -> $"missing required '{keyword}' directive"
        | MalformedDirective(line, text, expected) -> $"line {line}: malformed directive '{text}', expected '{expected}'"
        | DuplicateDirective(line, keyword) -> $"line {line}: '{keyword}' directive may appear at most once"
        | OrphanedTerrainDirective(line, keyword) -> $"line {line}: '{keyword}' appears before a 'terrain' directive"
        | UnknownDirective(line, keyword) -> $"line {line}: unknown directive '{keyword}'"
        | NonIntegerField(line, field, value) -> $"line {line}: field '{field}' is not an integer: '{value}'"
        | UnknownBoolValue(line, field, value) -> $"line {line}: field '{field}' must be 'true' or 'false', got '{value}'"
        | InvalidToken(line, field, value) -> $"line {line}: field '{field}' has an invalid value '{value}'"
