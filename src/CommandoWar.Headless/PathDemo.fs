namespace CommandoWar.Headless

open CommandoWar.Sim

/// A hand-built pathfinding demonstration fixture (TASK-013, backlog B-010).
/// Its only purpose is to give `cwheadless render path --path ...` and
/// `PathfindingTests` a fixed, well-understood terrain that exercises every
/// branch of `Pathfinding.find`:
///
///   * a straight clear route across open ground;
///   * a route detouring around an impassable wall;
///   * a route that prefers a cheap detour over a movement-cost patch;
///   * a no-path case: a goal cell walled off by impassable cells.
///
/// `DemoScenario` cannot show the no-path case (nothing on it is walled off),
/// and adding a pocket to it would move the committed `demo.*` goldens, so
/// this is a focused fixture of its own, exactly as `LosDemo` is for line of
/// sight. It is NOT authoritative game content and needs no on-disk format
/// (backlog B-024). It is built as a `RawScenario`, validated through
/// `Scenario.validate`, and instantiated through `World.ofScenario`.
[<RequireQualifiedAccess>]
module PathDemo =

    /// A 16 x 12 logical grid: wide enough to separate the wall detour, the
    /// cost patch, and the walled-off pocket; small enough for an ASCII render.
    let bounds: GridBounds = { Width = 16; Height = 12 }

    /// Deterministic seed. No gameplay phase draws from the stream, and
    /// pathfinding never touches it; the value only has to be stable.
    [<Literal>]
    let Seed = 20260904UL

    /// The demo is a single static frame (tick 0): pathfinding is a pure query
    /// over immutable terrain, so there is nothing to animate.
    [<Literal>]
    let TickCount = 0L

    let private wall x y : RawTerrainCell =
        { Cell = { X = x; Y = y }
          Class = "impassable"
          Elevation = 0
          MoveCost = 0
          Opaque = false }

    let private costly x y : RawTerrainCell =
        { Cell = { X = x; Y = y }
          Class = "passable"
          Elevation = 0
          MoveCost = 5
          Opaque = false }

    /// Cells the terrain layer overrides.
    ///
    ///   * a vertical impassable wall at x = 5, rows 0..7 (rows 8..11 open, so
    ///     a route from the left detours around the bottom);
    ///   * a 2 x 3 movement-cost patch (cost 5) at x = 10..11, rows 2..4 (a
    ///     straight route through it is dearer than a one-row detour);
    ///   * a walled-off pocket: (14, 9) stays passable but its four
    ///     neighbours are impassable, so nothing can reach it.
    let private authoredCells: RawTerrainCell[] =
        [| for y in 0..7 do
               yield wall 5 y
           yield costly 10 2
           yield costly 10 3
           yield costly 10 4
           yield costly 11 2
           yield costly 11 3
           yield costly 11 4
           yield wall 13 9
           yield wall 15 9
           yield wall 14 8
           yield wall 14 10 |]

    let private terrainLayer: RawTerrainLayer =
        { Width = bounds.Width
          Height = bounds.Height
          Cells = authoredCells
          Cover = [||] }

    let private raw: RawScenario =
        { ContentVersion = ScenarioContent.Version
          Id = "pathfinding-demo"
          Width = bounds.Width
          Height = bounds.Height
          FriendlyDeployments =
            [| { AgentId = 0; Cell = { X = 0; Y = 0 } }
               { AgentId = 1; Cell = { X = 0; Y = 11 } } |]
          EnemyDeployments = [||]
          ObjectiveAreas = [| { AreaId = "crossing"; Cell = { X = 15; Y = 0 } } |]
          ExtractionAreas = [| { AreaId = "exit"; Cell = { X = 15; Y = 11 } } |]
          StaticTargets = [||]
          Objectives =
            [| { Id = 1
                 Kind = "reach"
                 AreaRef = "crossing"
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
        | Error es -> failwith $"pathfinding demo scenario is malformed: {es}"

    /// The authoritative world at tick 0.
    let initialState () : WorldState =
        match World.ofScenario (scenario ()) Seed with
        | Ok w -> w
        | Error e -> failwith $"pathfinding demo world build failed: {e}"

    /// The four demo routes, as start/goal pairs. `cwheadless render path`
    /// with these `--path` values regenerates the golden renders.
    let routes: (Cell * Cell)[] =
        [| ({ X = 1; Y = 1 }, { X = 4; Y = 1 }) // straight clear route
           ({ X = 2; Y = 6 }, { X = 9; Y = 6 }) // detour around the x = 5 wall
           ({ X = 9; Y = 3 }, { X = 13; Y = 3 }) // cheap detour around the cost patch
           ({ X = 1; Y = 10 }, { X = 14; Y = 9 }) |] // no path: the pocket is walled off
