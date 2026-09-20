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
        [| { AgentId = 0; Cell = { X = 0; Y = 0 }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = "standard"; FormationId = ""; SlotIndex = 0 }
           { AgentId = 1; Cell = { X = 0; Y = 1 }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = "standard"; FormationId = ""; SlotIndex = 0 }
           { AgentId = 2; Cell = { X = 0; Y = 2 }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = "standard"; FormationId = ""; SlotIndex = 0 } |]
      EnemyDeployments = [| { AgentId = 10; Cell = { X = 15; Y = 15 }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = "standard"; FormationId = ""; SlotIndex = 0 } |]
      ObjectiveAreas = [| { AreaId = "observation"; Cell = { X = 8; Y = 8 } } |]
      ExtractionAreas = [| { AreaId = "exfil"; Cell = { X = 1; Y = 15 } } |]
      ResupplyAreas = [||]
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
             HoldTicks = 5
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
      TerrainLayer = None
      UnitTypes = [| { Id = "standard"; MoveSpeed = Agent.MoveSpeedDefault } |]
      Headquarters = None
      Jammers = [||]
      Formations = [||]
      FailOnFriendlyForceEliminated = true }

/// A well-formed authored terrain layer for the 16x16 `goodRaw` map: one
/// impassable cell, one elevated + opaque cell, and one directional cover
/// feature. None of the cells collides with a `goodRaw` deployment.
let private goodTerrainLayer () : RawTerrainLayer =
    { Width = 16
      Height = 16
      Cells =
        [| { Cell = { X = 5; Y = 5 }
             Class = "impassable"
             Elevation = 0
             MoveCost = 0
             Opaque = true }
           { Cell = { X = 8; Y = 8 }
             Class = "passable"
             Elevation = 2
             MoveCost = 3
             Opaque = true } |]
      Cover = [| { Cell = { X = 4; Y = 4 }; Direction = "north"; Level = 1 } |] }

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
    // The three version constants move independently: ScenarioContent.Version
    // is 7 (TASK-010 authored terrain layer; TASK-047 ResupplyAreas;
    // TASK-049 unit types; TASK-058 Headquarters/Jammers; TASK-059
    // formations; TASK-062 destroy's HoldTicks validation), Canonical.
    // FormatVersion is bumped by TASK-067 (TASK-018 / TASK-026 / TASK-028 /
    // TASK-032 / TASK-033 / TASK-034 / TASK-037 / TASK-044 / TASK-045 /
    // TASK-047 / TASK-058 / TASK-062 / TASK-065 / TASK-067 -- TASK-049's
    // MoveSpeed and TASK-058's Headquarters/Jammers and TASK-059's
    // FormationOffset are all static, excluded, so none of them move this
    // one; RadioDestroyed/PendingDelivery/MissionOutcome/
    // CompletedObjectives/ObjectiveProgress/Extracted/StalledTicks/AsGroup
    // do), Replay.FormatVersion is 1. This test documents the intent, not
    // an inequality.
    Assert.Equal(7, ScenarioContent.Version)
    Assert.Equal(15, Canonical.FormatVersion)
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
let ``communication availability round-trips through validation`` () =
    // TASK-027: Deployment.CommunicationAvailable defaults true and an
    // authored false (a comms blackout) survives onto the validated
    // deployment. A bool cannot be malformed, so there is no new error.
    let s = validated (goodRaw ())
    Assert.All(s.FriendlyDeployments, fun d -> Assert.True(d.CommunicationAvailable))

    let raw =
        { goodRaw () with
            FriendlyDeployments =
                (goodRaw ()).FriendlyDeployments
                |> Array.map (fun d ->
                    if d.AgentId = 1 then
                        { d with CommunicationAvailable = false }
                    else
                        d) }

    let blacked = validated raw
    let d1 = blacked.FriendlyDeployments |> Array.find (fun d -> AgentId.value d.Agent = 1)
    Assert.False(d1.CommunicationAvailable)
    Assert.All(
        blacked.FriendlyDeployments |> Array.filter (fun d -> AgentId.value d.Agent <> 1),
        fun d -> Assert.True(d.CommunicationAvailable)
    )

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
let ``a destroy objective carries its authored HoldTicks as the plant duration`` () =
    let s = validated (goodRaw ())

    Assert.True(
        s.Objectives
        |> Array.exists (function
            | Optional(DestroyTarget(_, _, ticks)) -> ticks = 5
            | _ -> false)
    )

[<Fact>]
let ``a destroy objective with non-positive HoldTicks is a typed error, no silent default`` () =
    let raw =
        { goodRaw () with
            Objectives = [| { objective 2 "destroy" with TargetRef = "bridge"; HoldTicks = 0 } |] }

    Assert.Contains(NonPositivePlantTicks(2, 0), errorsOf raw)

    let s = validated { raw with Objectives = [| { objective 2 "destroy" with TargetRef = "bridge"; HoldTicks = 5 } |] }
    Assert.Equal(1, s.Objectives.Length)

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
    Assert.Contains(UnsupportedContentVersion(99, 7), errorsOf { goodRaw () with ContentVersion = 99 })

[<Fact>]
let ``a version-1 scenario is rejected, not migrated`` () =
    // ScenarioContent.Version 1 predates the authored terrain layer. The
    // validator does not migrate it (docs/04 section 16).
    Assert.Contains(UnsupportedContentVersion(1, 7), errorsOf { goodRaw () with ContentVersion = 1 })

[<Fact>]
let ``a blank scenario id is reported`` () =
    Assert.Contains(BlankScenarioId, errorsOf { goodRaw () with Id = "   " })

[<Fact>]
let ``non-positive map dimensions are reported`` () =
    Assert.Contains(NonPositiveMapDimensions(0, 16), errorsOf { goodRaw () with Width = 0 })

[<Fact>]
let ``a duplicate deployment agent id is reported once`` () =
    let raw =
        { goodRaw () with EnemyDeployments = [| { AgentId = 1; Cell = { X = 15; Y = 15 }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = "standard"; FormationId = ""; SlotIndex = 0 } |] }

    let es = errorsOf raw
    Assert.Contains(DuplicateDeploymentId 1, es)
    Assert.Equal(1, es |> List.filter ((=) (DuplicateDeploymentId 1)) |> List.length)

[<Fact>]
let ``a negative deployment agent id is reported`` () =
    let raw =
        { goodRaw () with EnemyDeployments = [| { AgentId = -3; Cell = { X = 15; Y = 15 }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = "standard"; FormationId = ""; SlotIndex = 0 } |] }

    Assert.Contains(NegativeDeploymentId -3, errorsOf raw)

[<Fact>]
let ``a deployment outside the map is reported with its cell and the bounds`` () =
    let raw =
        { goodRaw () with EnemyDeployments = [| { AgentId = 10; Cell = { X = 99; Y = 0 }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = "standard"; FormationId = ""; SlotIndex = 0 } |] }

    Assert.Contains(
        DeploymentOutOfMap(10, { X = 99; Y = 0 }, { Width = 16; Height = 16 }),
        errorsOf raw
    )

[<Fact>]
let ``two deployments sharing a cell are reported with both agent ids`` () =
    let raw =
        { goodRaw () with EnemyDeployments = [| { AgentId = 10; Cell = { X = 0; Y = 0 }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = "standard"; FormationId = ""; SlotIndex = 0 } |] }

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
            ContentVersion = 8
            EnemyDeployments = [| { AgentId = 1; Cell = { X = 99; Y = 99 }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = "standard"; FormationId = ""; SlotIndex = 0 } |]
            Objectives = [| objective 2 "orbit" |]
            TerrainLayer =
                Some
                    { goodTerrainLayer () with
                        Cover = [| { Cell = { X = 40; Y = 40 }; Direction = "up"; Level = -1 } |] } }

    let es = errorsOf raw
    Assert.Contains(UnsupportedContentVersion(8, 7), es)
    Assert.Contains(DuplicateDeploymentId 1, es)
    Assert.Contains(DeploymentOutOfMap(1, { X = 99; Y = 99 }, { Width = 16; Height = 16 }), es)
    Assert.Contains(UnknownObjectiveKind(2, "orbit"), es)
    Assert.Contains(TerrainFeatureOutOfMap({ X = 40; Y = 40 }, { Width = 16; Height = 16 }), es)
    Assert.Contains(UnknownCoverClass({ X = 40; Y = 40 }, "up"), es)
    Assert.Contains(NegativeCoverLevel({ X = 40; Y = 40 }, -1), es)
    Assert.True(es.Length >= 7, $"expected at least 7 errors, got {es}")

// --- World.ofScenario --------------------------------------------

[<Fact>]
let ``World.ofScenario deploys friendly then enemy agents ordered ascending by id`` () =
    match World.ofScenario (validated (goodRaw ())) 1UL with
    | Error e -> Assert.Fail($"World.ofScenario failed: {e}")
    | Ok w ->
        Assert.Equal<int[]>([| 0; 1; 2; 10 |], w.Agents |> Array.map (fun a -> AgentId.value a.Id))
        Assert.Equal(Hostile, (w.Agents |> Array.find (fun a -> AgentId.value a.Id = 10)).Side)

// --- authored terrain layer (ScenarioContent.Version 2) --------------

[<Fact>]
let ``an absent terrain layer validates to empty terrain`` () =
    let s = validated (goodRaw ())
    // Flat, fully passable, transparent, uncovered across the whole map.
    Assert.True(Terrain.passable s.Terrain { X = 5; Y = 5 })
    Assert.Equal(0, Terrain.elevation s.Terrain { X = 8; Y = 8 })
    Assert.False(Terrain.opaque s.Terrain { X = 8; Y = 8 })
    Assert.Equal(0, Terrain.cover s.Terrain { X = 4; Y = 4 } North)

[<Fact>]
let ``a well-formed terrain layer validates to a populated Terrain`` () =
    let s =
        validated { goodRaw () with TerrainLayer = Some(goodTerrainLayer ()) }

    Assert.Equal<GridBounds>({ Width = 16; Height = 16 }, s.Terrain.Bounds)
    Assert.False(Terrain.passable s.Terrain { X = 5; Y = 5 })
    Assert.Equal(Terrain.BlockedCost, Terrain.moveCost s.Terrain { X = 5; Y = 5 })
    Assert.Equal(2, Terrain.elevation s.Terrain { X = 8; Y = 8 })
    Assert.Equal(3, Terrain.moveCost s.Terrain { X = 8; Y = 8 })
    Assert.True(Terrain.opaque s.Terrain { X = 8; Y = 8 })
    Assert.Equal(1, Terrain.cover s.Terrain { X = 4; Y = 4 } North)
    Assert.Equal(0, Terrain.cover s.Terrain { X = 4; Y = 4 } East)

[<Fact>]
let ``World.ofScenario carries the authored terrain onto the world`` () =
    let s =
        validated { goodRaw () with TerrainLayer = Some(goodTerrainLayer ()) }

    match World.ofScenario s Fixture.Seed with
    | Error e -> Assert.Fail($"World.ofScenario failed: {e}")
    | Ok w ->
        Assert.False(Terrain.passable w.Terrain { X = 5; Y = 5 })
        Assert.Equal(2, Terrain.elevation w.Terrain { X = 8; Y = 8 })

[<Fact>]
let ``a terrain layer whose dimensions disagree with the map is reported`` () =
    let raw =
        { goodRaw () with TerrainLayer = Some { goodTerrainLayer () with Width = 20 } }

    Assert.Contains(
        TerrainLayerDimensionsMismatch({ Width = 20; Height = 16 }, { Width = 16; Height = 16 }),
        errorsOf raw
    )

[<Fact>]
let ``a terrain cell outside the map is reported`` () =
    let raw =
        { goodRaw () with
            TerrainLayer =
                Some
                    { goodTerrainLayer () with
                        Cells =
                            [| { Cell = { X = 99; Y = 0 }
                                 Class = "passable"
                                 Elevation = 0
                                 MoveCost = 1
                                 Opaque = false } |] } }

    Assert.Contains(TerrainFeatureOutOfMap({ X = 99; Y = 0 }, { Width = 16; Height = 16 }), errorsOf raw)

[<Fact>]
let ``a duplicate terrain cell is reported once`` () =
    let cell: RawTerrainCell =
        { Cell = { X = 3; Y = 3 }
          Class = "passable"
          Elevation = 0
          MoveCost = 1
          Opaque = false }

    let raw =
        { goodRaw () with TerrainLayer = Some { goodTerrainLayer () with Cells = [| cell; cell |] } }

    let es = errorsOf raw
    Assert.Contains(DuplicateTerrainCell { X = 3; Y = 3 }, es)
    Assert.Equal(1, es |> List.filter ((=) (DuplicateTerrainCell { X = 3; Y = 3 })) |> List.length)

[<Fact>]
let ``a duplicate cover feature for the same cell and direction is reported`` () =
    let feature: RawCoverFeature =
        { Cell = { X = 3; Y = 3 }; Direction = "north"; Level = 1 }

    let raw =
        { goodRaw () with
            TerrainLayer = Some { goodTerrainLayer () with Cover = [| feature; feature |] } }

    Assert.Contains(DuplicateCoverFeature({ X = 3; Y = 3 }, "north"), errorsOf raw)

[<Fact>]
let ``an unknown terrain class is reported`` () =
    let raw =
        { goodRaw () with
            TerrainLayer =
                Some
                    { goodTerrainLayer () with
                        Cells =
                            [| { Cell = { X = 3; Y = 3 }
                                 Class = "swamp"
                                 Elevation = 0
                                 MoveCost = 1
                                 Opaque = false } |] } }

    Assert.Contains(UnknownTerrainClass({ X = 3; Y = 3 }, "swamp"), errorsOf raw)

[<Fact>]
let ``an unknown cover class is reported`` () =
    let raw =
        { goodRaw () with
            TerrainLayer =
                Some
                    { goodTerrainLayer () with
                        Cover = [| { Cell = { X = 3; Y = 3 }; Direction = "up"; Level = 1 } |] } }

    Assert.Contains(UnknownCoverClass({ X = 3; Y = 3 }, "up"), errorsOf raw)

[<Fact>]
let ``a negative elevation is reported`` () =
    let raw =
        { goodRaw () with
            TerrainLayer =
                Some
                    { goodTerrainLayer () with
                        Cells =
                            [| { Cell = { X = 3; Y = 3 }
                                 Class = "passable"
                                 Elevation = -1
                                 MoveCost = 1
                                 Opaque = false } |] } }

    Assert.Contains(NegativeElevation({ X = 3; Y = 3 }, -1), errorsOf raw)

[<Fact>]
let ``a negative move cost is reported`` () =
    let raw =
        { goodRaw () with
            TerrainLayer =
                Some
                    { goodTerrainLayer () with
                        Cells =
                            [| { Cell = { X = 3; Y = 3 }
                                 Class = "passable"
                                 Elevation = 0
                                 MoveCost = -5
                                 Opaque = false } |] } }

    Assert.Contains(NegativeMoveCost({ X = 3; Y = 3 }, -5), errorsOf raw)

// --- passable move-cost range (TASK-021) --------------------------------
// A passable cell must cost within [Terrain.BaseMoveCost, Terrain.MaxMoveCost]:
// a cheaper step breaks the A* heuristic's admissibility, a dearer one risks
// the Terrain.BlockedCost sentinel and Pathfinding cost overflow.

[<Fact>]
let ``a passable cell costing below BaseMoveCost is rejected naming the cell`` () =
    let raw =
        { goodRaw () with
            TerrainLayer =
                Some
                    { goodTerrainLayer () with
                        Cells =
                            [| { Cell = { X = 3; Y = 3 }
                                 Class = "passable"
                                 Elevation = 0
                                 MoveCost = 0
                                 Opaque = false } |] } }

    Assert.Contains(
        MoveCostOutOfRange({ X = 3; Y = 3 }, 0, Terrain.BaseMoveCost, Terrain.MaxMoveCost),
        errorsOf raw
    )

[<Fact>]
let ``a passable cell costing above MaxMoveCost is rejected naming the cell`` () =
    let raw =
        { goodRaw () with
            TerrainLayer =
                Some
                    { goodTerrainLayer () with
                        Cells =
                            [| { Cell = { X = 3; Y = 3 }
                                 Class = "passable"
                                 Elevation = 0
                                 MoveCost = Terrain.MaxMoveCost + 1
                                 Opaque = false } |] } }

    Assert.Contains(
        MoveCostOutOfRange({ X = 3; Y = 3 }, Terrain.MaxMoveCost + 1, Terrain.BaseMoveCost, Terrain.MaxMoveCost),
        errorsOf raw
    )

[<Fact>]
let ``a passable cell costing exactly BaseMoveCost and one costing exactly MaxMoveCost both validate`` () =
    let s =
        validated
            { goodRaw () with
                TerrainLayer =
                    Some
                        { goodTerrainLayer () with
                            Cells =
                                [| { Cell = { X = 3; Y = 3 }
                                     Class = "passable"
                                     Elevation = 0
                                     MoveCost = Terrain.BaseMoveCost
                                     Opaque = false }
                                   { Cell = { X = 4; Y = 3 }
                                     Class = "passable"
                                     Elevation = 0
                                     MoveCost = Terrain.MaxMoveCost
                                     Opaque = false } |] } }

    Assert.Equal(Terrain.BaseMoveCost, Terrain.moveCost s.Terrain { X = 3; Y = 3 })
    Assert.Equal(Terrain.MaxMoveCost, Terrain.moveCost s.Terrain { X = 4; Y = 3 })

[<Fact>]
let ``an impassable cell with an out-of-range move cost is still accepted`` () =
    // MoveCost is ignored for an impassable cell (Terrain.moveCost reports
    // BlockedCost), so a wild value there is not a fault.
    let s =
        validated
            { goodRaw () with
                TerrainLayer =
                    Some
                        { goodTerrainLayer () with
                            Cells =
                                [| { Cell = { X = 3; Y = 3 }
                                     Class = "impassable"
                                     Elevation = 0
                                     MoveCost = 999_999
                                     Opaque = false } |] } }

    Assert.False(Terrain.passable s.Terrain { X = 3; Y = 3 })
    Assert.Equal(Terrain.BlockedCost, Terrain.moveCost s.Terrain { X = 3; Y = 3 })

[<Fact>]
let ``the zero-cost corridor grid the standing review found is rejected`` () =
    // A passable MoveCost = 0 shortcut down column x = 4: the exact shape the
    // review flagged as able to make Pathfinding.find return a non-minimal
    // path. Scenario.validate must reject the content before it reaches
    // Terrain.build.
    let corridor =
        [| for y in 3..5 ->
               { Cell = { X = 4; Y = y }
                 Class = "passable"
                 Elevation = 0
                 MoveCost = 0
                 Opaque = false } |]

    let es =
        errorsOf { goodRaw () with TerrainLayer = Some { goodTerrainLayer () with Cells = corridor } }

    for y in 3..5 do
        Assert.Contains(
            MoveCostOutOfRange({ X = 4; Y = y }, 0, Terrain.BaseMoveCost, Terrain.MaxMoveCost),
            es
        )

[<Fact>]
let ``a negative cover level is reported`` () =
    let raw =
        { goodRaw () with
            TerrainLayer =
                Some
                    { goodTerrainLayer () with
                        Cover = [| { Cell = { X = 3; Y = 3 }; Direction = "south"; Level = -2 } |] } }

    Assert.Contains(NegativeCoverLevel({ X = 3; Y = 3 }, -2), errorsOf raw)

[<Fact>]
let ``a deployment on an authored impassable cell is reported`` () =
    let raw =
        { goodRaw () with
            TerrainLayer =
                Some
                    { goodTerrainLayer () with
                        Cells =
                            [| { Cell = { X = 0; Y = 0 } // friendly agent 0 deploys here
                                 Class = "impassable"
                                 Elevation = 0
                                 MoveCost = 0
                                 Opaque = false } |] } }

    Assert.Contains(DeploymentOnImpassableCell(0, { X = 0; Y = 0 }), errorsOf raw)

// --- unit types (ScenarioContent.Version 4, TASK-049, backlog B-058) ---

[<Fact>]
let ``a blank unit type id is reported`` () =
    let raw =
        { goodRaw () with UnitTypes = [| { Id = "   "; MoveSpeed = Agent.MoveSpeedDefault } |] }

    Assert.Contains(BlankUnitTypeId, errorsOf raw)

[<Fact>]
let ``a duplicate unit type id is reported once`` () =
    let raw =
        { goodRaw () with
            UnitTypes =
                [| { Id = "standard"; MoveSpeed = Agent.MoveSpeedDefault }
                   { Id = "standard"; MoveSpeed = 1 } |] }

    let es = errorsOf raw
    Assert.Contains(DuplicateUnitTypeId "standard", es)
    Assert.Equal(1, es |> List.filter ((=) (DuplicateUnitTypeId "standard")) |> List.length)

[<Fact>]
let ``a non-positive unit type move speed is reported`` () =
    let raw =
        { goodRaw () with UnitTypes = [| { Id = "standard"; MoveSpeed = 0 } |] }

    Assert.Contains(NonPositiveUnitTypeMoveSpeed("standard", 0), errorsOf raw)

[<Fact>]
let ``a deployment referencing an unknown unit type is reported`` () =
    let raw =
        { goodRaw () with
            EnemyDeployments =
                [| { AgentId = 10
                     Cell = { X = 15; Y = 15 }
                     CommunicationAvailable = true
                     Discipline = AppraisalConfig.DisciplineDefault
                     UnitType = "ghost"
                     FormationId = ""
                     SlotIndex = 0 } |] }

    Assert.Contains(DeploymentReferencesUnknownUnitType(10, "ghost"), errorsOf raw)

[<Fact>]
let ``a deployment's UnitType resolves to Deployment.MoveSpeed`` () =
    let raw =
        { goodRaw () with
            UnitTypes = [| { Id = "standard"; MoveSpeed = Agent.MoveSpeedDefault }; { Id = "slow"; MoveSpeed = 1 } |]
            EnemyDeployments =
                [| { AgentId = 10
                     Cell = { X = 15; Y = 15 }
                     CommunicationAvailable = true
                     Discipline = AppraisalConfig.DisciplineDefault
                     UnitType = "slow"
                     FormationId = ""
                     SlotIndex = 0 } |] }

    let s = validated raw
    let enemy = s.EnemyDeployments |> Array.find (fun d -> AgentId.value d.Agent = 10)
    Assert.Equal(1, enemy.MoveSpeed)

    Assert.All(
        s.FriendlyDeployments,
        fun d -> Assert.Equal(Agent.MoveSpeedDefault, d.MoveSpeed)
    )

// --- headquarters and jammers (ScenarioContent.Version 5, TASK-058, backlog B-016b) ---

[<Fact>]
let ``an absent Headquarters and empty Jammers validate cleanly, the pre-TASK-058 default`` () =
    let s = validated (goodRaw ())
    Assert.Equal(None, s.Headquarters)
    Assert.Empty(s.Jammers)

[<Fact>]
let ``an authored Headquarters and Jammer round-trip onto the validated Scenario`` () =
    let raw =
        { goodRaw () with
            Headquarters = Some { X = 2; Y = 2 }
            Jammers =
                [| { Position = { X = 4; Y = 4 }
                     Radius = 3
                     ActiveFromTick = 5L
                     ActiveUntilTick = 40L } |] }

    let s = validated raw
    Assert.Equal(Some { X = 2; Y = 2 }, s.Headquarters)
    let jammer = Assert.Single s.Jammers
    Assert.Equal({ X = 4; Y = 4 }, jammer.Position)
    Assert.Equal(3, jammer.Radius)
    Assert.Equal(5L, jammer.ActiveFromTick)
    Assert.Equal(40L, jammer.ActiveUntilTick)

[<Fact>]
let ``a Headquarters outside the map is reported`` () =
    let raw = { goodRaw () with Headquarters = Some { X = 99; Y = 99 } }
    Assert.Contains(HeadquartersOutOfMap({ X = 99; Y = 99 }, { Width = 16; Height = 16 }), errorsOf raw)

[<Fact>]
let ``a jammer outside the map is reported`` () =
    let raw =
        { goodRaw () with
            Jammers = [| { Position = { X = 99; Y = 99 }; Radius = 1; ActiveFromTick = 0L; ActiveUntilTick = 10L } |] }

    Assert.Contains(JammerOutOfMap(0, { X = 99; Y = 99 }, { Width = 16; Height = 16 }), errorsOf raw)

[<Fact>]
let ``a negative jammer radius is reported`` () =
    let raw =
        { goodRaw () with
            Jammers = [| { Position = { X = 4; Y = 4 }; Radius = -1; ActiveFromTick = 0L; ActiveUntilTick = 10L } |] }

    Assert.Contains(NegativeJammerRadius(0, -1), errorsOf raw)

[<Fact>]
let ``a jammer window with ActiveFromTick after ActiveUntilTick is reported`` () =
    let raw =
        { goodRaw () with
            Jammers = [| { Position = { X = 4; Y = 4 }; Radius = 1; ActiveFromTick = 10L; ActiveUntilTick = 5L } |] }

    Assert.Contains(InvalidJammerWindow(0, 10L, 5L), errorsOf raw)

[<Fact>]
let ``a jammer with a negative tick bound is reported`` () =
    let raw =
        { goodRaw () with
            Jammers = [| { Position = { X = 4; Y = 4 }; Radius = 1; ActiveFromTick = -1L; ActiveUntilTick = 5L } |] }

    Assert.Contains(InvalidJammerWindow(0, -1L, 5L), errorsOf raw)

[<Fact>]
let ``the jammer index in a reported fault matches its position in the authored array`` () =
    let raw =
        { goodRaw () with
            Jammers =
                [| { Position = { X = 4; Y = 4 }; Radius = 1; ActiveFromTick = 0L; ActiveUntilTick = 10L }
                   { Position = { X = 6; Y = 6 }; Radius = -2; ActiveFromTick = 0L; ActiveUntilTick = 10L } |] }

    Assert.Contains(NegativeJammerRadius(1, -2), errorsOf raw)

// --- formations (ScenarioContent.Version 6, TASK-059, backlog B-011d) ---

[<Fact>]
let ``a blank FormationId validates cleanly to no FormationOffset, the pre-TASK-059 default`` () =
    let s = validated (goodRaw ())
    Assert.All(s.FriendlyDeployments, fun d -> Assert.Equal(None, d.FormationOffset))
    Assert.All(s.EnemyDeployments, fun d -> Assert.Equal(None, d.FormationOffset))

[<Fact>]
let ``a deployment's FormationId/SlotIndex resolves to Deployment.FormationOffset`` () =
    let raw =
        { goodRaw () with
            Formations = [| { Id = "wedge"; Offsets = [| { X = 0; Y = 0 }; { X = -1; Y = 1 } |] } |]
            FriendlyDeployments =
                (goodRaw ()).FriendlyDeployments
                |> Array.map (fun d ->
                    if d.AgentId = 1 then
                        { d with FormationId = "wedge"; SlotIndex = 1 }
                    else
                        d) }

    let s = validated raw
    let d1 = s.FriendlyDeployments |> Array.find (fun d -> AgentId.value d.Agent = 1)
    Assert.Equal(Some { X = -1; Y = 1 }, d1.FormationOffset)

    Assert.All(
        s.FriendlyDeployments |> Array.filter (fun d -> AgentId.value d.Agent <> 1),
        fun d -> Assert.Equal(None, d.FormationOffset)
    )

[<Fact>]
let ``a blank formation id is reported`` () =
    let raw = { goodRaw () with Formations = [| { Id = "   "; Offsets = [| { X = 0; Y = 0 } |] } |] }
    Assert.Contains(BlankFormationId, errorsOf raw)

[<Fact>]
let ``a duplicate formation id is reported`` () =
    let raw =
        { goodRaw () with
            Formations =
                [| { Id = "wedge"; Offsets = [| { X = 0; Y = 0 } |] }
                   { Id = "wedge"; Offsets = [| { X = 1; Y = 0 } |] } |] }

    Assert.Contains(DuplicateFormationId "wedge", errorsOf raw)

[<Fact>]
let ``a formation with no slot offsets is reported`` () =
    let raw = { goodRaw () with Formations = [| { Id = "empty"; Offsets = [||] } |] }
    Assert.Contains(FormationHasNoSlots "empty", errorsOf raw)

[<Fact>]
let ``a deployment referencing an unknown formation is reported`` () =
    let raw =
        { goodRaw () with
            FriendlyDeployments =
                (goodRaw ()).FriendlyDeployments
                |> Array.map (fun d -> if d.AgentId = 0 then { d with FormationId = "ghost" } else d) }

    Assert.Contains(DeploymentReferencesUnknownFormation(0, "ghost"), errorsOf raw)

[<Fact>]
let ``a deployment's SlotIndex outside its formation's slot count is reported`` () =
    let raw =
        { goodRaw () with
            Formations = [| { Id = "pair"; Offsets = [| { X = 0; Y = 0 }; { X = 1; Y = 0 } |] } |]
            FriendlyDeployments =
                (goodRaw ()).FriendlyDeployments
                |> Array.map (fun d ->
                    if d.AgentId = 0 then
                        { d with FormationId = "pair"; SlotIndex = 2 }
                    else
                        d) }

    Assert.Contains(DeploymentSlotIndexOutOfRange(0, "pair", 2, 2), errorsOf raw)

// --- pinning: the six-agent fixture as a Scenario ---------------

/// The shared spike fixture (src/CommandoWar.Headless/Fixture.fs,
/// content/fixtures/SPIKE-FIXTURE.md) expressed as an authored scenario. The
/// extraction area is never reached (agent 3's own MoveTo target, (20,14),
/// is the objective area, not the extraction area at (0,0)) and so never
/// affects the hash. The `reach observation` objective, however, DOES now
/// affect it (TASK-062, backlog B-032): agent 3 reaches (20,14) at tick 31
/// (SPIKE-FIXTURE.md's own math), which is that objective's own area, so
/// `Simulation.mission` completes it and `MissionOutcome` becomes
/// `Succeeded` the same tick -- both pinned hashes below reflect this real
/// behaviour, not just Canonical.FormatVersion's byte-layout change.
let private fixtureScenario () : Scenario =
    { ContentVersion = ScenarioContent.Version
      Id = "spike-fixture"
      Width = Fixture.bounds.Width
      Height = Fixture.bounds.Height
      FriendlyDeployments = [| for i in 0..5 -> { AgentId = i; Cell = { X = 0; Y = i }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = "standard"; FormationId = ""; SlotIndex = 0 } |]
      EnemyDeployments = [||]
      ObjectiveAreas = [| { AreaId = "observation"; Cell = { X = 20; Y = 14 } } |]
      ExtractionAreas = [| { AreaId = "exfil"; Cell = { X = 0; Y = 0 } } |]
      ResupplyAreas = [||]
      StaticTargets = [||]
      Objectives = [| { objective 1 "reach" with AreaRef = "observation" } |]
      TerrainLayer = None
      UnitTypes = [| { Id = "standard"; MoveSpeed = Agent.MoveSpeedDefault } |]
      Headquarters = None
      Jammers = [||]
      Formations = [||]
      FailOnFriendlyForceEliminated = true }
    |> validated

[<Fact>]
let ``the six-agent fixture as a Scenario reproduces the pinned initial hash`` () =
    match World.ofScenario (fixtureScenario ()) Fixture.Seed with
    | Error e -> Assert.Fail($"World.ofScenario failed: {e}")
    | Ok world -> Assert.Equal(0x5049F6F0E9FCA1E2UL, (Hashing.hash world).Value)

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

    // TASK-062 (backlog B-032): agent 3 arrives at tick 31, completing the
    // scenario's own `reach observation` objective and reaching
    // `MissionOutcome = Succeeded` the same tick -- see fixtureScenario's
    // own doc comment.
    Assert.Equal(Succeeded, state.MissionOutcome)
    Assert.Equal(0x8C6057A96F267515UL, (Hashing.hash state).Value)
