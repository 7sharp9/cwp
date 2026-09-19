module CommandoWar.Sim.Tests.ScenarioFileTests

open Xunit
open FsCheck
open FsCheck.FSharp
open FsCheck.Xunit
open CommandoWar.Sim

// Coverage for the `.cwscenario` content file format (TASK-060, backlog
// B-024): `ScenarioFile.parse`/`serialise` round-trip `RawScenario`
// end to end, and malformed text is a named `ScenarioFileError`. This
// module never asserts anything about *content* validity (`ScenarioError`,
// `Scenario.validate`) — `ScenarioTests.fs` already owns that; a
// `RawScenario` that round-trips here may still be rejected there.

// --- a small known-good raw scenario, every field non-default at least
//     once ------------------------------------------------------------

let private goodRaw () : RawScenario =
    { ContentVersion = ScenarioContent.Version
      Id = "unit test scenario"
      Width = 10
      Height = 8
      FriendlyDeployments =
        [| { AgentId = 0
             Cell = { X = 0; Y = 0 }
             CommunicationAvailable = true
             Discipline = 3
             UnitType = "standard"
             FormationId = "wedge"
             SlotIndex = 1 }
           { AgentId = 1
             Cell = { X = 0; Y = 1 }
             CommunicationAvailable = false
             Discipline = 0
             UnitType = "standard"
             FormationId = ""
             SlotIndex = 0 } |]
      EnemyDeployments =
        [| { AgentId = 10
             Cell = { X = 9; Y = 7 }
             CommunicationAvailable = true
             Discipline = 5
             UnitType = "heavy"
             FormationId = ""
             SlotIndex = 0 } |]
      ObjectiveAreas = [| { AreaId = "observation"; Cell = { X = 5; Y = 5 } } |]
      ExtractionAreas = [| { AreaId = "exfil"; Cell = { X = 1; Y = 7 } } |]
      ResupplyAreas = [| { AreaId = "cache"; Cell = { X = 2; Y = 2 } } |]
      StaticTargets = [| { TargetId = "bridge"; Cell = { X = 5; Y = 0 } } |]
      Objectives =
        [| { Id = 1
             Kind = "reach"
             AreaRef = "observation"
             TargetRef = ""
             HoldTicks = 0
             ExtractAgentIds = [||]
             IsOptional = false }
           { Id = 2
             Kind = "extract"
             AreaRef = "exfil"
             TargetRef = ""
             HoldTicks = 0
             ExtractAgentIds = [| 0; 1 |]
             IsOptional = true } |]
      TerrainLayer =
        Some
            { Width = 10
              Height = 8
              Cells =
                [| { Cell = { X = 3; Y = 3 }
                     Class = "impassable"
                     Elevation = 0
                     MoveCost = 0
                     Opaque = true }
                   { Cell = { X = 4; Y = 4 }
                     Class = "passable"
                     Elevation = 2
                     MoveCost = 3
                     Opaque = false } |]
              Cover = [| { Cell = { X = 4; Y = 4 }; Direction = "north"; Level = 1 } |] }
      UnitTypes =
        [| { Id = "standard"; MoveSpeed = Agent.MoveSpeedDefault }
           { Id = "heavy"; MoveSpeed = 1 } |]
      Headquarters = Some { X = 5; Y = 4 }
      Jammers = [| { Position = { X = 6; Y = 6 }; Radius = 3; ActiveFromTick = 0L; ActiveUntilTick = 40L } |]
      Formations = [| { Id = "wedge"; Offsets = [| { X = 0; Y = 0 }; { X = -1; Y = 1 } |] } |]
      FailOnFriendlyForceEliminated = true }

[<Fact>]
let ``a known-good scenario round-trips through serialise and parse`` () =
    let raw = goodRaw ()
    Assert.Equal(Ok raw, ScenarioFile.parse (ScenarioFile.serialise raw))

[<Fact>]
let ``a scenario with no terrain layer, no headquarters, and every array empty round-trips`` () =
    let raw =
        { (goodRaw ()) with
            FriendlyDeployments = [| (goodRaw ()).FriendlyDeployments.[1] |] // drop the formationed one
            EnemyDeployments = [||]
            ObjectiveAreas = [||]
            ResupplyAreas = [||]
            StaticTargets = [||]
            Objectives = [||]
            TerrainLayer = None
            UnitTypes = [||]
            Headquarters = None
            Jammers = [||]
            Formations = [||] }

    Assert.Equal(Ok raw, ScenarioFile.parse (ScenarioFile.serialise raw))

[<Fact>]
let ``a well-formed file with content Scenario.validate rejects still parses successfully`` () =
    // ScenarioFile is a grammar reader only — it does not know or care that
    // ContentVersion 99 is unsupported, or that "observation" is not a
    // known area id for objective 1. Both are Scenario.validate's job.
    let raw = { (goodRaw ()) with ContentVersion = 99 }
    Assert.Equal(Ok raw, ScenarioFile.parse (ScenarioFile.serialise raw))

// --- malformed text -----------------------------------------------------

[<Fact>]
let ``an empty file is EmptyFile`` () =
    Assert.Equal(Error ScenarioFile.EmptyFile, ScenarioFile.parse "")

[<Fact>]
let ``a file of only blank lines and comments is EmptyFile`` () =
    Assert.Equal(Error ScenarioFile.EmptyFile, ScenarioFile.parse "\n  \n# a comment\n\t\n")

[<Fact>]
let ``a file not starting with version is ExpectedVersionFirst`` () =
    match ScenarioFile.parse "content-version 6\nid x\n" with
    | Error(ScenarioFile.ExpectedVersionFirst(1, "content-version 6")) -> ()
    | other -> failwith $"expected ExpectedVersionFirst, got {other}"

[<Fact>]
let ``an unsupported format version is UnsupportedFormatVersion`` () =
    let text = ScenarioFile.serialise (goodRaw ()) |> fun s -> s.Replace("version 1\n", "version 2\n")

    match ScenarioFile.parse text with
    | Error(ScenarioFile.UnsupportedFormatVersion(1, "2", 1)) -> ()
    | other -> failwith $"expected UnsupportedFormatVersion, got {other}"

[<Fact>]
let ``a missing required header directive is MissingDirective`` () =
    match ScenarioFile.parse "version 1\n" with
    | Error(ScenarioFile.MissingDirective "content-version") -> ()
    | other -> failwith $"expected MissingDirective \"content-version\", got {other}"

[<Fact>]
let ``a malformed map directive is MalformedDirective`` () =
    match ScenarioFile.parse "version 1\ncontent-version 6\nid x\nmap 10\n" with
    | Error(ScenarioFile.MalformedDirective(4, "10", "map <width> <height>")) -> ()
    | other -> failwith $"expected MalformedDirective on line 4, got {other}"

[<Fact>]
let ``a non-integer field is NonIntegerField`` () =
    match ScenarioFile.parse "version 1\ncontent-version six\nid x\n" with
    | Error(ScenarioFile.NonIntegerField(2, "content-version", "six")) -> ()
    | other -> failwith $"expected NonIntegerField, got {other}"

[<Fact>]
let ``an unknown bool value is UnknownBoolValue`` () =
    let header = "version 1\ncontent-version 6\nid x\nmap 4 4\n"
    match ScenarioFile.parse (header + "fail-on-friendly-eliminated maybe\n") with
    | Error(ScenarioFile.UnknownBoolValue(5, "fail-on-friendly-eliminated", "maybe")) -> ()
    | other -> failwith $"expected UnknownBoolValue, got {other}"

[<Fact>]
let ``a terrain-cell before any terrain directive is OrphanedTerrainDirective`` () =
    let header = "version 1\ncontent-version 6\nid x\nmap 4 4\nfail-on-friendly-eliminated false\n"
    match ScenarioFile.parse (header + "terrain-cell 0 0 passable 0 1 false\n") with
    | Error(ScenarioFile.OrphanedTerrainDirective(6, "terrain-cell")) -> ()
    | other -> failwith $"expected OrphanedTerrainDirective, got {other}"

[<Fact>]
let ``an unknown directive keyword is UnknownDirective`` () =
    let header = "version 1\ncontent-version 6\nid x\nmap 4 4\nfail-on-friendly-eliminated false\n"
    match ScenarioFile.parse (header + "wat 1 2 3\n") with
    | Error(ScenarioFile.UnknownDirective(6, "wat")) -> ()
    | other -> failwith $"expected UnknownDirective, got {other}"

[<Fact>]
let ``a duplicate headquarters directive is DuplicateDirective`` () =
    let header = "version 1\ncontent-version 6\nid x\nmap 4 4\nfail-on-friendly-eliminated false\n"
    match ScenarioFile.parse (header + "headquarters 0 0\nheadquarters 1 1\n") with
    | Error(ScenarioFile.DuplicateDirective(7, "headquarters")) -> ()
    | other -> failwith $"expected DuplicateDirective, got {other}"

[<Fact>]
let ``comment and blank lines are ignored between directives`` () =
    let raw = goodRaw ()
    let withNoise = ScenarioFile.serialise raw |> fun s -> s.Replace("\n", "\n# a comment\n\n")
    Assert.Equal(Ok raw, ScenarioFile.parse withNoise)

[<Fact>]
let ``serialise rejects an id token equal to the reserved blank sentinel`` () =
    let raw = { (goodRaw ()) with UnitTypes = [| { Id = "-"; MoveSpeed = 1 } |] }
    Assert.Throws<System.ArgumentException>(fun () -> ScenarioFile.serialise raw |> ignore) |> ignore

[<Fact>]
let ``serialise rejects an id token containing whitespace`` () =
    let raw = { (goodRaw ()) with UnitTypes = [| { Id = "two words"; MoveSpeed = 1 } |] }
    Assert.Throws<System.ArgumentException>(fun () -> ScenarioFile.serialise raw |> ignore) |> ignore

// --- round-trip property --------------------------------------------------
// Bounded to the subset of RawScenario values this line-based grammar can
// represent at all: single-token id/reference fields have no whitespace and
// are never the reserved "-" sentinel (serialise itself refuses those, by
// design — see the two facts above), matching the same "representable
// subset" discipline ReplaySerialisation.fs's issuer field already applies.

// Every "array of records" below is built as several parallel flat arrays
// (one `Gen.arrayOfLength` per field) zipped together in plain code, not as
// an array of per-element `Gen<'a>` sequenced monadically — this project's
// existing property tests (`DeterminismPropertyTests.fs`) never needed a
// `Gen<'a> array -> Gen<'a array>` combinator, so none is assumed to exist
// here either.

let private idTokenGen: Gen<string> =
    gen {
        let! first = Gen.elements [ 'a' .. 'z' ]
        let! n = Gen.choose (0, 6)
        let! rest = Gen.arrayOfLength n (Gen.elements ([ 'a' .. 'z' ] @ [ '0' .. '9' ]))
        return System.String(Array.append [| first |] rest)
    }

let private cellGen (w: int) (h: int) : Gen<Cell> =
    gen {
        let! x = Gen.choose (0, w - 1)
        let! y = Gen.choose (0, h - 1)
        return { X = x; Y = y }
    }

let private areaArrayGen (w: int) (h: int) (n: int) : Gen<RawArea[]> =
    gen {
        let! ids = Gen.arrayOfLength n idTokenGen
        let! cells = Gen.arrayOfLength n (cellGen w h)
        return Array.map2 (fun id cell -> { AreaId = id; Cell = cell }) ids cells
    }

let private rawScenarioGen: Gen<RawScenario> =
    gen {
        let! w = Gen.choose (2, 12)
        let! h = Gen.choose (2, 12)
        let! scenarioId = idTokenGen

        let! unitTypeIdsRaw = Gen.arrayOfLength 2 idTokenGen
        let unitTypeIds = unitTypeIdsRaw |> Array.distinct
        let unitTypeIds = if Array.isEmpty unitTypeIds then [| "u" |] else unitTypeIds
        let! unitTypeSpeeds = Gen.arrayOfLength unitTypeIds.Length (Gen.choose (1, 4))
        let unitTypes = Array.map2 (fun id s -> { Id = id; MoveSpeed = s }) unitTypeIds unitTypeSpeeds

        let! formationCount = Gen.choose (0, 2)
        let! formationIdsRaw = Gen.arrayOfLength formationCount idTokenGen
        let formationIds = formationIdsRaw |> Array.distinct
        // Each formation gets exactly 2 offsets — a fixed arity sidesteps
        // needing a variable-length-per-element sequence combinator.
        let! formationOffsetsFlat = Gen.arrayOfLength (formationIds.Length * 2) (cellGen 7 7 |> Gen.map (fun c -> { X = c.X - 3; Y = c.Y - 3 }))

        let formations =
            formationIds
            |> Array.mapi (fun i id ->
                { Id = id
                  Offsets = [| formationOffsetsFlat.[i * 2]; formationOffsetsFlat.[i * 2 + 1] |] })

        let deploymentArrayGen (baseId: int) (n: int) : Gen<RawDeployment[]> =
            gen {
                let! cells = Gen.arrayOfLength n (cellGen w h)
                let! comms = Gen.arrayOfLength n (Gen.elements [ true; false ])
                let! disciplines = Gen.arrayOfLength n (Gen.choose (0, 10))
                let! unitTypesChosen = Gen.arrayOfLength n (Gen.elements unitTypeIds)
                let! formationIdsChosen = Gen.arrayOfLength n (Gen.elements (Array.append [| "" |] formationIds))
                let! slotIndices = Gen.arrayOfLength n (Gen.choose (0, 3))

                return
                    [| 0 .. n - 1 |]
                    |> Array.map (fun i ->
                        { AgentId = baseId + i
                          Cell = cells.[i]
                          CommunicationAvailable = comms.[i]
                          Discipline = disciplines.[i]
                          UnitType = unitTypesChosen.[i]
                          FormationId = formationIdsChosen.[i]
                          SlotIndex = slotIndices.[i] })
            }

        let! friendlyCount = Gen.choose (1, 3)
        let! enemyCount = Gen.choose (0, 2)
        let! friendly = deploymentArrayGen 0 friendlyCount
        let! enemy = deploymentArrayGen 100 enemyCount

        let! objectiveAreaCount = Gen.choose (0, 2)
        let! objectiveAreas = areaArrayGen w h objectiveAreaCount
        let! extractionAreas = areaArrayGen w h 1
        let! resupplyAreaCount = Gen.choose (0, 2)
        let! resupplyAreas = areaArrayGen w h resupplyAreaCount

        let! targetCount = Gen.choose (0, 2)
        let! targetIds = Gen.arrayOfLength targetCount idTokenGen
        let! targetCells = Gen.arrayOfLength targetCount (cellGen w h)
        let targets = Array.map2 (fun id cell -> ({ TargetId = id; Cell = cell }: RawTarget)) targetIds targetCells

        let! objectiveCount = Gen.choose (0, 2)
        let! objIds = Gen.arrayOfLength objectiveCount (Gen.choose (0, 100))
        let! objKinds = Gen.arrayOfLength objectiveCount (Gen.elements [ "reach"; "hold"; "destroy"; "extract" ])
        let! objAreaRefUse = Gen.arrayOfLength objectiveCount (Gen.elements [ true; false ])
        let! objAreaRefTok = Gen.arrayOfLength objectiveCount idTokenGen
        let! objTargetRefUse = Gen.arrayOfLength objectiveCount (Gen.elements [ true; false ])
        let! objTargetRefTok = Gen.arrayOfLength objectiveCount idTokenGen
        let! objHoldTicks = Gen.arrayOfLength objectiveCount (Gen.choose (0, 50))
        let! objIsOptional = Gen.arrayOfLength objectiveCount (Gen.elements [ true; false ])
        // Fixed arity (0..2 ids per objective, flattened) — the same
        // sequence-avoidance trick as `formations` above.
        let! objExtractFlatA = Gen.arrayOfLength objectiveCount (Gen.choose (0, 20))
        let! objExtractFlatB = Gen.arrayOfLength objectiveCount (Gen.choose (0, 20))
        let! objExtractUseB = Gen.arrayOfLength objectiveCount (Gen.elements [ true; false ])

        let objectives =
            [| 0 .. objectiveCount - 1 |]
            |> Array.map (fun i ->
                { Id = objIds.[i]
                  Kind = objKinds.[i]
                  AreaRef = if objAreaRefUse.[i] then objAreaRefTok.[i] else ""
                  TargetRef = if objTargetRefUse.[i] then objTargetRefTok.[i] else ""
                  HoldTicks = objHoldTicks.[i]
                  ExtractAgentIds =
                    if objExtractUseB.[i] then
                        [| objExtractFlatA.[i]; objExtractFlatB.[i] |]
                    else
                        [||]
                  IsOptional = objIsOptional.[i] })

        let! headquartersPresent = Gen.elements [ true; false ]
        let! headquartersCell = cellGen w h
        let headquarters = if headquartersPresent then Some headquartersCell else None

        let! jammerCount = Gen.choose (0, 2)
        let! jammerPositions = Gen.arrayOfLength jammerCount (cellGen w h)
        let! jammerRadii = Gen.arrayOfLength jammerCount (Gen.choose (0, 5))
        let! jammerFromTicks = Gen.arrayOfLength jammerCount (Gen.choose (0, 50))
        let! jammerDurations = Gen.arrayOfLength jammerCount (Gen.choose (0, 50))

        let jammers =
            [| 0 .. jammerCount - 1 |]
            |> Array.map (fun i ->
                ({ Position = jammerPositions.[i]
                   Radius = jammerRadii.[i]
                   ActiveFromTick = int64 jammerFromTicks.[i]
                   ActiveUntilTick = int64 (jammerFromTicks.[i] + jammerDurations.[i]) }: RawJammer))

        let! includeTerrain = Gen.elements [ true; false ]

        let! terrainLayer =
            if not includeTerrain then
                Gen.constant None
            else
                gen {
                    let! cellCount = Gen.choose (0, 4)
                    let! cells = Gen.arrayOfLength cellCount (cellGen w h)
                    let! classes = Gen.arrayOfLength cellCount (Gen.elements [ "passable"; "impassable"; "lava" ])
                    let! elevations = Gen.arrayOfLength cellCount (Gen.choose (0, 3))
                    let! costs = Gen.arrayOfLength cellCount (Gen.choose (1, 5))
                    let! opaques = Gen.arrayOfLength cellCount (Gen.elements [ true; false ])

                    let rawCells =
                        [| 0 .. cellCount - 1 |]
                        |> Array.map (fun i ->
                            { Cell = cells.[i]
                              Class = classes.[i]
                              Elevation = elevations.[i]
                              MoveCost = costs.[i]
                              Opaque = opaques.[i] })
                        |> Array.distinctBy (fun c -> c.Cell)

                    let! coverCount = Gen.choose (0, 3)
                    let! coverCells = Gen.arrayOfLength coverCount (cellGen w h)
                    let! coverDirs = Gen.arrayOfLength coverCount (Gen.elements [ "north"; "east"; "south"; "west" ])
                    let! coverLevels = Gen.arrayOfLength coverCount (Gen.choose (0, 3))

                    let rawCover =
                        [| 0 .. coverCount - 1 |]
                        |> Array.map (fun i ->
                            ({ Cell = coverCells.[i]
                               Direction = coverDirs.[i]
                               Level = coverLevels.[i] }: RawCoverFeature))
                        |> Array.distinctBy (fun c -> c.Cell, c.Direction)

                    return
                        Some
                            { Width = w
                              Height = h
                              Cells = rawCells
                              Cover = rawCover }
                }

        let! failOnEliminated = Gen.elements [ true; false ]
        let! contentVersion = Gen.choose (0, 10)

        return
            { ContentVersion = contentVersion
              Id = scenarioId
              Width = w
              Height = h
              FriendlyDeployments = friendly
              EnemyDeployments = enemy
              ObjectiveAreas = objectiveAreas |> Array.distinctBy (fun a -> a.AreaId)
              ExtractionAreas = extractionAreas |> Array.distinctBy (fun a -> a.AreaId)
              ResupplyAreas = resupplyAreas |> Array.distinctBy (fun a -> a.AreaId)
              StaticTargets = targets |> Array.distinctBy (fun t -> t.TargetId)
              Objectives = objectives
              TerrainLayer = terrainLayer
              UnitTypes = unitTypes
              Headquarters = headquarters
              Jammers = jammers
              Formations = formations
              FailOnFriendlyForceEliminated = failOnEliminated }
    }

[<Property(MaxTest = 200)>]
let ``an arbitrary representable RawScenario round-trips through serialise and parse`` () =
    Prop.forAll (Arb.fromGen rawScenarioGen) (fun raw -> ScenarioFile.parse (ScenarioFile.serialise raw) = Ok raw)
