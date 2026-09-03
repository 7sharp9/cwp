namespace CommandoWar.Client.Mibo

// Framework-neutral spike content: parse + validate.
//
// This module contains NO Mibo, raylib, Tiled, or CommandoWar.Sim types. It is
// the "Content DTOs -> validation -> Sim setup" stage from ADR-0002: authored
// text (content/greybox.cwmap) is parsed into plain records, validated here,
// and only then handed to SimBridge.
//
// Terrain/passability is CLIENT-SIDE only: CommandoWar.Sim has no terrain model
// yet (backlog B-008), so walls are authored, validated against, and rendered,
// but never sent across the simulation boundary.

module Content =

    type MarkerKind =
        | FriendlySpawn
        | ObjectiveArea

    /// One authored marker, reduced to primitives.
    type Marker = { Kind: MarkerKind; Id: int; X: int; Y: int }

    /// Raw authored content before validation.
    type RawMap =
        { Source: string
          Width: int
          Height: int
          Seed: uint64
          /// Row-major [y].[x]. true = passable floor, false = wall.
          Passable: bool[][]
          Markers: Marker list }

    /// A validated scenario, safe to hand to SimBridge.
    type Scenario =
        { Width: int
          Height: int
          Seed: uint64
          Passable: bool[][]
          FriendlySpawns: Marker list
          Objectives: Marker list }

        member this.IsPassable(x, y) =
            x >= 0 && y >= 0 && x < this.Width && y < this.Height && this.Passable.[y].[x]

    // --- parse ---------------------------------------------------------------

    let private tokens (s: string) =
        s.Split([| ' '; '\t' |], System.StringSplitOptions.RemoveEmptyEntries)

    let private stripComment (s: string) =
        match s.IndexOf '#' with
        | -1 -> s
        | h -> s.Substring(0, h)

    /// Parses the v1 .cwmap text. Returns every structural error found, each
    /// naming the offending line.
    let parse (source: string) (text: string) : Result<RawMap, string list> =
        let errors = ResizeArray<string>()
        let fail lineNo msg = errors.Add $"line {lineNo}: {msg}"
        let lines = text.Replace("\r\n", "\n").Split('\n')

        let mutable width = 0
        let mutable height = 0
        let mutable seed = 0UL
        let mutable seedSet = false
        let terrain = ResizeArray<bool[]>()
        let markers = ResizeArray<Marker>()
        // parser is line-oriented; terrain rows are consumed greedily right
        // after the `terrain` directive until `height` rows are read.
        let mutable expectTerrainRows = 0

        for i in 0 .. lines.Length - 1 do
            let lineNo = i + 1
            let line = (stripComment lines.[i]).Trim()

            if expectTerrainRows > 0 then
                // Inside the terrain block every line is a raw row (walls are '#',
                // so '#' is NOT a comment here). Trailing whitespace only is trimmed.
                let row = lines.[i].TrimEnd()
                if row.Length <> width then
                    fail lineNo $"terrain row {terrain.Count} has {row.Length} cells, expected {width}"
                terrain.Add(Array.init width (fun x -> x >= row.Length || row.[x] <> '#'))
                expectTerrainRows <- expectTerrainRows - 1
            elif line = "" then
                ()
            else
                match tokens line with
                | [| "version"; v |] ->
                    if v <> "1" then fail lineNo $"unsupported content version '{v}', expected 1"
                | [| "size"; w; h |] ->
                    match System.Int32.TryParse w, System.Int32.TryParse h with
                    | (true, w'), (true, h') when w' > 0 && h' > 0 ->
                        width <- w'
                        height <- h'
                    | _ -> fail lineNo $"size expects two positive integers, got '{line}'"
                | [| "seed"; s |] ->
                    match System.UInt64.TryParse s with
                    | true, s' ->
                        seed <- s'
                        seedSet <- true
                    | _ -> fail lineNo $"seed expects a uint64, got '{s}'"
                | [| "terrain" |] ->
                    if width <= 0 || height <= 0 then
                        fail lineNo "terrain block appears before a valid 'size' directive"
                    else
                        expectTerrainRows <- height
                | [| "marker"; kindStr; idStr; xStr; yStr |] ->
                    let kind =
                        match kindStr with
                        | "friendly" -> Some FriendlySpawn
                        | "objective" -> Some ObjectiveArea
                        | _ -> None

                    match kind, System.Int32.TryParse idStr, System.Int32.TryParse xStr, System.Int32.TryParse yStr with
                    | Some k, (true, id), (true, x), (true, y) -> markers.Add { Kind = k; Id = id; X = x; Y = y }
                    | None, _, _, _ -> fail lineNo $"unknown marker kind '{kindStr}' (expected 'friendly' or 'objective')"
                    | _ -> fail lineNo $"marker expects integer '<kind> <id> <x> <y>', got '{line}'"
                | _ -> fail lineNo $"unrecognised directive '{line}'"

        if width <= 0 || height <= 0 then errors.Add "missing or invalid 'size' directive"
        if not seedSet then errors.Add "missing 'seed' directive"
        if width > 0 && height > 0 && terrain.Count <> height then
            errors.Add $"terrain has {terrain.Count} rows, expected {height}"

        if errors.Count > 0 then
            Error(List.ofSeq errors)
        else
            Ok
                { Source = source
                  Width = width
                  Height = height
                  Seed = seed
                  Passable = terrain.ToArray()
                  Markers = List.ofSeq markers }

    // --- validate ----------------------------------------------------------

    /// Content is invalid when a spawn is out of bounds, on a wall, shares a
    /// cell, or reuses an id; or when there is no friendly spawn or no
    /// objective. Returns every offending object by name.
    let validate (raw: RawMap) : Result<Scenario, string list> =
        let errors = ResizeArray<string>()
        let inBounds x y = x >= 0 && y >= 0 && x < raw.Width && y < raw.Height
        let seenId = System.Collections.Generic.Dictionary<MarkerKind * int, Marker>()
        let seenCell = System.Collections.Generic.Dictionary<int * int, Marker>()

        for m in raw.Markers do
            let label = $"{m.Kind} #{m.Id} at ({m.X},{m.Y})"

            if not (inBounds m.X m.Y) then
                errors.Add $"{label} lies outside the {raw.Width}x{raw.Height} grid"
            else
                match seenId.TryGetValue((m.Kind, m.Id)) with
                | true, other ->
                    errors.Add $"{label} reuses id {m.Id}, already used by {other.Kind} at ({other.X},{other.Y})"
                | _ -> seenId.[(m.Kind, m.Id)] <- m

                if m.Kind = FriendlySpawn then
                    if not raw.Passable.[m.Y].[m.X] then
                        errors.Add $"{label} lies on an impassable (wall) cell"

                    match seenCell.TryGetValue((m.X, m.Y)) with
                    | true, other -> errors.Add $"{label} shares its cell with {other.Kind} #{other.Id}"
                    | _ -> seenCell.[(m.X, m.Y)] <- m

        let friendly = raw.Markers |> List.filter (fun m -> m.Kind = FriendlySpawn)
        let objectives = raw.Markers |> List.filter (fun m -> m.Kind = ObjectiveArea)

        if List.isEmpty friendly then
            errors.Add "no 'friendly' marker: the scenario has no deployable agents"
        if List.isEmpty objectives then
            errors.Add "no 'objective' marker: the scenario has no objective"

        if errors.Count > 0 then
            Error(List.ofSeq errors)
        else
            Ok
                { Width = raw.Width
                  Height = raw.Height
                  Seed = raw.Seed
                  Passable = raw.Passable
                  FriendlySpawns = friendly |> List.sortBy (fun m -> m.Id)
                  Objectives = objectives }

    /// Parse then validate. `Error` carries the source name and every problem.
    let load (source: string) (text: string) : Result<Scenario, string * string list> =
        match parse source text with
        | Error es -> Error(source, es)
        | Ok raw ->
            match validate raw with
            | Error es -> Error(source, es)
            | Ok scenario -> Ok scenario
