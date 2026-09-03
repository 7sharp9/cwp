module CommandoWar.Sim.Tests.ScenarioTests

open Xunit
open CommandoWar.Sim
open CommandoWar.Headless

// Coverage for the authored-scenario model, its content-format version, the
// one-pass validator, and World.ofScenario (docs/04_SIMULATION_SPEC.md
// section 21, docs/06_CONTENT_AND_PRESENTATION.md section 3). The final two
// facts pin the shared six-agent fixture expressed as a Scenario to the
// existing determinism evidence without changing it.

// --- a known-good raw scenario ----------------------------------------

let private goodRaw () : RawScenario =
    { ContentVersion = ScenarioContent.Version
      Id = "unit-test"
      Width = 16
      Height = 16
      FriendlyDeployments =
        [| { AgentId = 0; Cell = { X = 0; Y = 0 } }
           { AgentId = 1; Cell = { X = 0; Y = 1 } }
           { AgentId = 2; Cell = { X = 0; Y = 2 } } |]
      EnemyDeployments = [| { AgentId = 10; Cell = { X = 15; Y = 15 } } |]
      ObjectiveAreas = [| { AreaId = "observation"; Cell = { X = 8; Y = 8 } } |]
      ExtractionAreas = [| { AreaId = "exfil"; Cell = { X = 1; Y = 15 } } |]
      StaticTargets = [| { TargetId = "bridge"; Cell = { X = 8; Y = 0 } } |]
      Objectives =
        [| { Id = 1
             Kind = "reach"
             AreaRef = "observation"
             TargetRef = ""
             HoldTicks = 0
             ExtractAgentIds = [||]
             IsOptional = false }
           { Id = 2
             Kind = "destroy"
             AreaRef = ""
             TargetRef = "bridge"
             HoldTicks = 0
             ExtractAgentIds = [||]
             IsOptional = true }
           { Id = 3
             Kind = "hold"
             AreaRef = "observation"
             TargetRef = ""
             HoldTicks = 20
             ExtractAgentIds = [||]
             IsOptional = false }
           { Id = 4
             Kind = "extract"
             AreaRef = "exfil"
             TargetRef = ""
             HoldTicks = 0
             ExtractAgentIds = [| 0; 1 |]
             IsOptional = false } |]
      FailOnFriendlyForceEliminated = true }

let private objective id kind : RawObjective =
    { Id = id
      Kind = kind
      AreaRef = ""
      TargetRef = ""
      HoldTicks = 0
      ExtractAgentIds = [||]
      IsOptional = false }

let private validated (raw: RawScenario) : Scenario =
    match Scenario.validate raw with
    | Ok s -> s
    | Error es -> failwith $"expected Ok, got {es}"

let private errorsOf (raw: RawScenario) : ScenarioError list =
    match Scenario.validate raw with
    | Ok _ -> failwith "expected Error, got Ok"
    | Error es -> es

// --- the version constant is independent -----------------------------

[<Fact>]
let ``the content version is independent of the canonical and replay versions`` () =
    Assert.Equal(1, ScenarioContent.Version)
    // Independent constants: this test documents the intent, not an inequality.
    Assert.Equal(1, Canonical.FormatVersion)
    Assert.Equal(1, Replay.FormatVersion)

// --- the happy path -------------------------------------------------

[<Fact>]
let ``a well-formed raw scenario validates to a Scenario`` () =
    let s = validated (goodRaw ())
    Assert.Equal("unit-test", ScenarioId.value s.Id)
    Assert.Equal<GridBounds>({ Width = 16; Height = 16 }, s.Map)
    Assert.Equal(3, s.FriendlyDeployments.Length)
    Assert.Equal(1, s.EnemyDeployments.Length)
    Assert.Equal(Hostile, s.EnemyDeployments.[0].Side)
    Assert.Equal(4, s.Objectives.Length)
    Assert.True(s.Rules.FailOnFriendlyForceEliminated)

[<Fact>]
let ``an optional objective keeps its Optional wrapper`` () =
    let s = validated (goodRaw ())

    Assert.True(
        s.Objectives
        |> Array.exists (function
            | Optional(DestroyTarget _) -> true
            | _ -> false)
    )

[<Fact>]
let ``an extraction with no listed agents validates to AllFriendlyAgents`` () =
    let raw =
        { goodRaw () with
            Objectives =
                [| { objective 1 "reach" with AreaRef = "observation" }
                   { objective 4 "extract" with AreaRef = "exfil" } |] }

    let s = validated raw

    Assert.True(
        s.Objectives
        |> Array.exists (function
            | ExtractAgents(_, AllFriendlyAgents, _) -> true
            | _ -> false)
    )

// --- one test per invalid condition --------------------------------

[<Fact>]
let ``an unsupported content version is a typed error`` () =
    Assert.Contains(UnsupportedContentVersion(2, 1), errorsOf { goodRaw () with ContentVersion = 2 })

[<Fact>]
let ``a blank scenario id is reported`` () =
    Assert.Contains(BlankScenarioId, errorsOf { goodRaw () with Id = "   " })

[<Fact>]
let ``non-positive map dimensions are reported`` () =
    Assert.Contains(NonPositiveMapDimensions(0, 16), errorsOf { goodRaw () with Width = 0 })

[<Fact>]
let ``a duplicate deployment agent id is reported once`` () =
    let raw =
        { goodRaw () with EnemyDeployments = [| { AgentId = 1; Cell = { X = 15; Y = 15 } } |] }

    let es = errorsOf raw
    Assert.Contains(DuplicateDeploymentId 1, es)
    Assert.Equal(1, es |> List.filter ((=) (DuplicateDeploymentId 1)) |> List.length)

[<Fact>]
let ``a negative deployment agent id is reported`` () =
    let raw =
        { goodRaw () with EnemyDeployments = [| { AgentId = -3; Cell = { X = 15; Y = 15 } } |] }

    Assert.Contains(NegativeDeploymentId -3, errorsOf raw)

[<Fact>]
let ``a deployment outside the map is reported with its cell and the bounds`` () =
    let raw =
        { goodRaw () with EnemyDeployments = [| { AgentId = 10; Cell = { X = 99; Y = 0 } } |] }

    Assert.Contains(
        DeploymentOutOfMap(10, { X = 99; Y = 0 }, { Width = 16; Height = 16 }),
        errorsOf raw
    )

[<Fact>]
let ``two deployments sharing a cell are reported with both agent ids`` () =
    let raw =
        { goodRaw () with EnemyDeployments = [| { AgentId = 10; Cell = { X = 0; Y = 0 } } |] }

    Assert.Contains(DeploymentCellShared({ X = 0; Y = 0 }, [ 0; 10 ]), errorsOf raw)

[<Fact>]
let ``a duplicate objective id is reported`` () =
    let raw =
        { goodRaw () with
            Objectives = Array.append (goodRaw ()).Objectives [| { objective 1 "reach" with AreaRef = "observation" } |] }

    Assert.Contains(DuplicateObjectiveId 1, errorsOf raw)

[<Fact>]
let ``a duplicate area id across objective and extraction areas is reported`` () =
    let raw =
        { goodRaw () with ExtractionAreas = [| { AreaId = "observation"; Cell = { X = 1; Y = 15 } } |] }

    Assert.Contains(DuplicateAreaId "observation", errorsOf raw)

[<Fact>]
let ``a duplicate static target id is reported`` () =
    let raw =
        { goodRaw () with
            StaticTargets =
                [| { TargetId = "bridge"; Cell = { X = 8; Y = 0 } }
                   { TargetId = "bridge"; Cell = { X = 9; Y = 0 } } |] }

    Assert.Contains(DuplicateTargetId "bridge", errorsOf raw)

[<Fact>]
let ``an area marker outside the map is reported`` () =
    let raw =
        { goodRaw () with ObjectiveAreas = [| { AreaId = "observation"; Cell = { X = 40; Y = 1 } } |] }

    Assert.Contains(
        AreaMarkerOutOfMap("observation", { X = 40; Y = 1 }, { Width = 16; Height = 16 }),
        errorsOf raw
    )

[<Fact>]
let ``a static target outside the map is reported`` () =
    let raw =
        { goodRaw () with StaticTargets = [| { TargetId = "bridge"; Cell = { X = -1; Y = 0 } } |] }

    Assert.Contains(
        TargetMarkerOutOfMap("bridge", { X = -1; Y = 0 }, { Width = 16; Height = 16 }),
        errorsOf raw
    )

[<Fact>]
let ``an objective referencing a missing area is reported`` () =
    let raw =
        { goodRaw () with Objectives = [| { objective 7 "reach" with AreaRef = "nowhere" } |] }

    Assert.Contains(ObjectiveReferencesMissingArea(7, "nowhere"), errorsOf raw)

[<Fact>]
let ``an objective referencing a missing target is reported`` () =
    let raw =
        { goodRaw () with Objectives = [| { objective 8 "destroy" with TargetRef = "ghost" } |] }

    Assert.Contains(ObjectiveReferencesMissingTarget(8, "ghost"), errorsOf raw)

[<Fact>]
let ``an unknown objective kind is reported`` () =
    let raw = { goodRaw () with Objectives = [| objective 9 "teleport" |] }
    Assert.Contains(UnknownObjectiveKind(9, "teleport"), errorsOf raw)

[<Fact>]
let ``an extraction selecting an unknown agent is reported`` () =
    let raw =
        { goodRaw () with
            Objectives =
                [| { objective 4 "extract" with
                       AreaRef = "exfil"
                       ExtractAgentIds = [| 0; 77 |] } |] }

    Assert.Contains(ExtractionSelectsUnknownAgent(4, 77), errorsOf raw)

[<Fact>]
let ``a missing required marker is reported for each of the three kinds`` () =
    let es =
        errorsOf
            { goodRaw () with
                FriendlyDeployments = [||]
                ExtractionAreas = [||]
                Objectives = [||] }

    Assert.Contains(MissingRequiredMarker "FriendlySpawn", es)
    Assert.Contains(MissingRequiredMarker "ExtractionArea", es)
    Assert.Contains(MissingRequiredMarker "Objective", es)

// --- the one-pass property ----------------------------------------

[<Fact>]
let ``validation reports every fault in one pass`` () =
    let raw =
        { goodRaw () with
            ContentVersion = 3
            EnemyDeployments = [| { AgentId = 1; Cell = { X = 99; Y = 99 } } |]
            Objectives = [| objective 2 "orbit" |] }

    let es = errorsOf raw
    Assert.Contains(UnsupportedContentVersion(3, 1), es)
    Assert.Contains(DuplicateDeploymentId 1, es)
    Assert.Contains(DeploymentOutOfMap(1, { X = 99; Y = 99 }, { Width = 16; Height = 16 }), es)
    Assert.Contains(UnknownObjectiveKind(2, "orbit"), es)
    Assert.True(es.Length >= 4, $"expected at least 4 errors, got {es}")

// --- World.ofScenario --------------------------------------------

[<Fact>]
let ``World.ofScenario deploys friendly then enemy agents ordered ascending by id`` () =
    match World.ofScenario (validated (goodRaw ())) 1UL with
    | Error e -> Assert.Fail($"World.ofScenario failed: {e}")
    | Ok w ->
        Assert.Equal<int[]>([| 0; 1; 2; 10 |], w.Agents |> Array.map (fun a -> AgentId.value a.Id))
        Assert.Equal(Hostile, (w.Agents |> Array.find (fun a -> AgentId.value a.Id = 10)).Side)

// --- pinning: the six-agent fixture as a Scenario ---------------

/// The shared spike fixture (src/CommandoWar.Headless/Fixture.fs,
/// content/fixtures/SPIKE-FIXTURE.md) expressed as an authored scenario. The
/// token objective and extraction area exist only to satisfy the required
/// markers; World.ofScenario reads neither, so they cannot affect the hash.
let private fixtureScenario () : Scenario =
    { ContentVersion = ScenarioContent.Version
      Id = "spike-fixture"
      Width = Fixture.bounds.Width
      Height = Fixture.bounds.Height
      FriendlyDeployments = [| for i in 0..5 -> { AgentId = i; Cell = { X = 0; Y = i } } |]
      EnemyDeployments = [||]
      ObjectiveAreas = [| { AreaId = "observation"; Cell = { X = 20; Y = 14 } } |]
      ExtractionAreas = [| { AreaId = "exfil"; Cell = { X = 0; Y = 0 } } |]
      StaticTargets = [||]
      Objectives = [| { objective 1 "reach" with AreaRef = "observation" } |]
      FailOnFriendlyForceEliminated = true }
    |> validated

[<Fact>]
let ``the six-agent fixture as a Scenario reproduces the pinned initial hash`` () =
    match World.ofScenario (fixtureScenario ()) Fixture.Seed with
    | Error e -> Assert.Fail($"World.ofScenario failed: {e}")
    | Ok world -> Assert.Equal(0xF2F3DF0D820AD9ACUL, (Hashing.hash world).Value)

[<Fact>]
let ``the fixture Scenario stepped 40 ticks with the fixture command reaches the pinned final hash`` () =
    let world =
        match World.ofScenario (fixtureScenario ()) Fixture.Seed with
        | Ok w -> w
        | Error e -> failwith $"{e}"

    let command =
        Command.moveTo (CommandId.ofInt 1) Fixture.CommandIssueTick Fixture.MovedAgent Fixture.MoveTarget

    let mutable state = world

    for tick in 1L .. Fixture.TickCount do
        let cmds = if tick = Fixture.CommandIssueTick then [| command |] else [||]
        state <- (Simulation.step SimConfig.standard cmds state).State

    Assert.Equal(0x838D3AE7DBFB735DUL, (Hashing.hash state).Value)
