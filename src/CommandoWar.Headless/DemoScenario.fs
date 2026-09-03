namespace CommandoWar.Headless

open CommandoWar.Sim

/// A hand-built demonstration scenario whose only purpose is to exercise
/// every layer of the diagnostic renderers (`DiagnosticRender`): a diagonal
/// ridge of elevation, an impassable block, a movement-cost patch, an opaque
/// wall, and directional cover on a few edges, plus agents that move so the
/// HTML scrubber has something to show.
///
/// It is NOT authoritative game content and needs no on-disk format (backlog
/// B-024 owns content files). It is built as a `RawScenario`, validated
/// through `Scenario.validate`, and instantiated through `World.ofScenario`,
/// so it also proves the terrain-layer validation path end to end.
[<RequireQualifiedAccess>]
module DemoScenario =

    /// A 12 x 8 logical grid: small enough that an ASCII render fits on a
    /// screen, large enough to separate the terrain features.
    let bounds: GridBounds = { Width = 12; Height = 8 }

    /// Deterministic seed. No gameplay phase draws from the stream yet, so
    /// the value only has to be stable.
    [<Literal>]
    let Seed = 20260903UL

    /// Ticks the demo run covers. Agent 0 reaches (10, 6) at tick 16 under
    /// the placeholder movement rule (10 steps on X then 6 on Y); the run
    /// continues to 20 so a post-arrival rest state is also rendered.
    [<Literal>]
    let TickCount = 20L

    [<Literal>]
    let private Issuer = "demo:terrain"

    /// One authored terrain cell.
    let private cell x y className elevation moveCost opaque : RawTerrainCell =
        { Cell = { X = x; Y = y }
          Class = className
          Elevation = elevation
          MoveCost = moveCost
          Opaque = opaque }

    let private terrainLayer: RawTerrainLayer =
        { Width = bounds.Width
          Height = bounds.Height
          Cells =
            [|
               // A diagonal ridge of elevation.
               cell 2 2 "passable" 1 1 false
               cell 3 3 "passable" 2 1 false
               cell 4 4 "passable" 3 1 false
               cell 5 5 "passable" 2 1 false
               cell 6 6 "passable" 1 1 false
               // An impassable 2 x 2 block.
               cell 8 2 "impassable" 0 0 false
               cell 9 2 "impassable" 0 0 false
               cell 8 3 "impassable" 0 0 false
               cell 9 3 "impassable" 0 0 false
               // A movement-cost patch.
               cell 2 5 "passable" 0 3 false
               cell 3 5 "passable" 0 3 false
               cell 4 5 "passable" 0 3 false
               // An opaque wall (passable, blocks sight).
               cell 6 1 "passable" 0 1 true
               cell 6 2 "passable" 0 1 true
               cell 6 3 "passable" 0 1 true |]
          Cover =
            [| { Cell = { X = 1; Y = 4 }; Direction = "north"; Level = 1 }
               { Cell = { X = 10; Y = 5 }; Direction = "west"; Level = 2 }
               { Cell = { X = 5; Y = 6 }; Direction = "south"; Level = 1 } |] }

    let private raw: RawScenario =
        { ContentVersion = ScenarioContent.Version
          Id = "diagnostic-demo"
          Width = bounds.Width
          Height = bounds.Height
          FriendlyDeployments =
            [| { AgentId = 0; Cell = { X = 0; Y = 0 } }
               { AgentId = 1; Cell = { X = 0; Y = 1 } } |]
          EnemyDeployments = [| { AgentId = 5; Cell = { X = 11; Y = 7 } } |]
          ObjectiveAreas = [| { AreaId = "ridge-top"; Cell = { X = 4; Y = 4 } } |]
          ExtractionAreas = [| { AreaId = "exit"; Cell = { X = 0; Y = 7 } } |]
          StaticTargets = [||]
          Objectives =
            [| { Id = 1
                 Kind = "reach"
                 AreaRef = "ridge-top"
                 TargetRef = ""
                 HoldTicks = 0
                 ExtractAgentIds = [||]
                 IsOptional = false } |]
          TerrainLayer = Some terrainLayer
          FailOnFriendlyForceEliminated = true }

    /// The validated scenario. Fails hard (this is a fixed test vector, not
    /// user content): a validation error here is a bug in this file.
    let scenario () : Scenario =
        match Scenario.validate raw with
        | Ok s -> s
        | Error es -> failwith $"demo scenario is malformed: {es}"

    /// The authoritative world at tick 0.
    let initialState () : WorldState =
        match World.ofScenario (scenario ()) Seed with
        | Ok w -> w
        | Error e -> failwith $"demo scenario world build failed: {e}"

    /// The demo command log: agent 0 crosses to (10, 6), agent 1 moves onto
    /// the south-covered cell (5, 6). Both issued at tick 1.
    let commandLog () : RecordedCommand[] =
        [| { Tick = 1L
             Sequence = 0
             Command = Command.moveTo (CommandId.ofInt 1) 1L (AgentId.ofInt 0) { X = 10; Y = 6 }
             Issuer = Issuer }
           { Tick = 1L
             Sequence = 1
             Command = Command.moveTo (CommandId.ofInt 2) 1L (AgentId.ofInt 1) { X = 5; Y = 6 }
             Issuer = Issuer } |]
