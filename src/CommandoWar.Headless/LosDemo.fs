namespace CommandoWar.Headless

open CommandoWar.Sim

/// A hand-built line-of-sight demonstration fixture (TASK-012, backlog B-009).
/// Its only purpose is to give `cwheadless render los --los ...` and
/// `SightTests` a fixed, well-understood terrain that exercises every branch
/// of `Sight.trace`:
///
///   * a clear ray across open ground;
///   * a ray blocked by an opaque wall cell;
///   * a ray grazing a single opaque wall cell at a diagonal corner
///     (the corner rule: one wall does not block);
///   * a ray blocked by an elevation ridge higher than both endpoints;
///   * a ray blocked by two opaque cells forming a solid diagonal corner
///     (the corner rule: two walls do block).
///
/// It is NOT authoritative game content and needs no on-disk format (backlog
/// B-024). It is built as a `RawScenario`, validated through
/// `Scenario.validate`, and instantiated through `World.ofScenario`.
[<RequireQualifiedAccess>]
module LosDemo =

    /// A 12 x 12 logical grid.
    let bounds: GridBounds = { Width = 12; Height = 12 }

    /// Deterministic seed. No gameplay phase draws from the stream, and line
    /// of sight never touches it; the value only has to be stable.
    [<Literal>]
    let Seed = 20260904UL

    /// The demo is a single static frame (tick 0): line of sight is a pure
    /// query over immutable terrain, so there is nothing to animate.
    [<Literal>]
    let TickCount = 0L

    let private cell x y elevation opaque : RawTerrainCell =
        { Cell = { X = x; Y = y }
          Class = "passable"
          Elevation = elevation
          MoveCost = 1
          Opaque = opaque }

    let private terrainLayer: RawTerrainLayer =
        { Width = bounds.Width
          Height = bounds.Height
          Cells =
            [|
               // A single opaque wall cell on row 4: blocks a horizontal ray.
               cell 5 4 0 true
               // A lone opaque cell: a diagonal ray grazes its corner and is
               // NOT blocked (one wall at a corner lets sight past).
               cell 9 3 0 true
               // Two opaque cells forming a solid diagonal corner: a diagonal
               // ray squeezing between them IS blocked.
               cell 9 7 0 true
               cell 8 8 0 true
               // A diagonal elevation ridge peaking at (3,7): the peak is
               // higher than both endpoints of a horizontal ray on row 7, so
               // it occludes.
               cell 2 6 2 false
               cell 3 7 6 false
               cell 4 8 2 false |]
          Cover = [||] }

    let private raw: RawScenario =
        { ContentVersion = ScenarioContent.Version
          Id = "line-of-sight-demo"
          Width = bounds.Width
          Height = bounds.Height
          FriendlyDeployments =
            [| { AgentId = 0; Cell = { X = 0; Y = 0 } }
               { AgentId = 1; Cell = { X = 0; Y = 11 } } |]
          EnemyDeployments = [||]
          ObjectiveAreas = [| { AreaId = "observation-point"; Cell = { X = 11; Y = 0 } } |]
          ExtractionAreas = [| { AreaId = "exit"; Cell = { X = 11; Y = 11 } } |]
          StaticTargets = [||]
          Objectives =
            [| { Id = 1
                 Kind = "reach"
                 AreaRef = "observation-point"
                 TargetRef = ""
                 HoldTicks = 0
                 ExtractAgentIds = [||]
                 IsOptional = false } |]
          TerrainLayer = Some terrainLayer
          FailOnFriendlyForceEliminated = true }

    /// The validated scenario. Fails hard: this is a fixed test vector, not
    /// user content, so a validation error here is a bug in this file.
    let scenario () : Scenario =
        match Scenario.validate raw with
        | Ok s -> s
        | Error es -> failwith $"LOS demo scenario is malformed: {es}"

    /// The authoritative world at tick 0.
    let initialState () : WorldState =
        match World.ofScenario (scenario ()) Seed with
        | Ok w -> w
        | Error e -> failwith $"LOS demo world build failed: {e}"

    /// The five demo rays, as origin/target pairs. `cwheadless render los`
    /// with these `--los` values regenerates the golden renders.
    let rays: (Cell * Cell)[] =
        [| ({ X = 1; Y = 1 }, { X = 10; Y = 1 }) // clear
           ({ X = 1; Y = 4 }, { X = 10; Y = 4 }) // blocked by the opaque wall at (5,4)
           ({ X = 7; Y = 2 }, { X = 10; Y = 5 }) // grazes the corner of the lone wall (9,3): visible
           ({ X = 1; Y = 7 }, { X = 6; Y = 7 }) // blocked by the ridge at (3,7)
           ({ X = 6; Y = 5 }, { X = 11; Y = 10 }) |] // blocked by the (9,7)/(8,8) corner
