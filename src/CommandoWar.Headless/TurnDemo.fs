namespace CommandoWar.Headless

open CommandoWar.Sim

/// TASK-050 spike: a hand-built scenario for the `cwheadless turn` harness,
/// which steps the simulation for a full "turn" (tens of ticks) and prints
/// the resulting per-tick diagnostic frames in sequence -- enough to see
/// several agents' queued `MoveTo`/`Suppress` orders resolve incrementally,
/// the way an XCOM turn plays out, over the existing continuous-tick sim.
///
/// It is NOT authoritative game content and needs no on-disk format (the
/// `DemoScenario`/`PathDemo`/`LosDemo` precedent). It is built as a
/// `RawScenario`, validated through `Scenario.validate`, and instantiated
/// through `World.ofScenario`. This module and its `turn` subcommand are
/// read-only against `CommandoWar.Sim`: no simulation behaviour changes.
[<RequireQualifiedAccess>]
module TurnDemo =

    /// A 16 x 10 open grid: no authored terrain (flat, passable, non-opaque
    /// everywhere) so the spike stays focused on order/tick resolution, not
    /// terrain interaction.
    let bounds: GridBounds = { Width = 16; Height = 10 }

    /// Deterministic seed. Only has to be stable.
    [<Literal>]
    let Seed = 20260918UL

    /// Long enough to watch multiple orders resolve one after another: agent
    /// 0's first `MoveTo` (12 cells) completes around tick 13, well before
    /// its reissued order at tick 25.
    [<Literal>]
    let TickCount = 45L

    [<Literal>]
    let private Issuer = "turn-demo"

    /// Every agent here moves at the default pace (TASK-049) -- this spike
    /// is about order/tick resolution, not movement speed.
    [<Literal>]
    let private StandardUnitType = "standard"

    let private raw: RawScenario =
        { ContentVersion = ScenarioContent.Version
          Id = "turn-resolution-demo"
          Width = bounds.Width
          Height = bounds.Height
          FriendlyDeployments =
            [| { AgentId = 0; Cell = { X = 0; Y = 0 }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = StandardUnitType; FormationId = ""; SlotIndex = 0 }
               { AgentId = 1; Cell = { X = 0; Y = 3 }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = StandardUnitType; FormationId = ""; SlotIndex = 0 }
               { AgentId = 2; Cell = { X = 3; Y = 4 }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = StandardUnitType; FormationId = ""; SlotIndex = 0 }
               { AgentId = 3; Cell = { X = 0; Y = 9 }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = StandardUnitType; FormationId = ""; SlotIndex = 0 } |]
          EnemyDeployments =
            [| { AgentId = 4; Cell = { X = 9; Y = 4 }; CommunicationAvailable = true; Discipline = AppraisalConfig.DisciplineDefault; UnitType = StandardUnitType; FormationId = ""; SlotIndex = 0 } |]
          ObjectiveAreas = [| { AreaId = "objective"; Cell = { X = 12; Y = 0 } } |]
          ExtractionAreas = [| { AreaId = "exit"; Cell = { X = 15; Y = 9 } } |]
          ResupplyAreas = [||]
          StaticTargets = [||]
          Objectives =
            [| { Id = 1
                 Kind = "reach"
                 AreaRef = "objective"
                 TargetRef = ""
                 HoldTicks = 0
                 ExtractAgentIds = [||]
                 IsOptional = false } |]
          TerrainLayer = None
          UnitTypes = [| { Id = StandardUnitType; MoveSpeed = Agent.MoveSpeedDefault } |]
          Headquarters = None
          Jammers = [||]
          Formations = [||]
          FailOnFriendlyForceEliminated = true }

    /// The validated scenario. Fails hard: this is a fixed test vector, not
    /// user content, so a validation error here is a bug in this file.
    let scenario () : Scenario =
        match Scenario.validate raw with
        | Ok s -> s
        | Error es -> failwith $"turn demo scenario is malformed: {es}"

    /// The authoritative world at tick 0.
    let initialState () : WorldState =
        match World.ofScenario (scenario ()) Seed with
        | Ok w -> w
        | Error e -> failwith $"turn demo world build failed: {e}"

    /// A handful of `MoveTo`/`Suppress` orders spread across ticks and
    /// agents:
    ///
    ///   * tick 1 -- agent 0 crosses the open ground to (12,0) (the
    ///     objective); agent 1 advances to (9,1); agent 2 holds at (3,4),
    ///     within `CombatConfig.WeaponRange` of the hostile, and `Suppress`es
    ///     it -- already a known contact from the tick-0 deployment (within
    ///     `Perception.SightRange` of every friendly), so the order is
    ///     accepted immediately and Combat's automatic engagement actually
    ///     exchanges fire every tick both agents remain in range and visible;
    ///   * tick 12 -- agent 3 moves up to (9,8), once agent 0's first route
    ///     has had time to resolve;
    ///   * tick 25 -- agent 0 is reissued a fresh `MoveTo` to (3,9), well
    ///     after its first order has completed, so the printed sequence
    ///     shows a second order landing mid-turn on an already-resting agent.
    let commandLog () : RecordedCommand[] =
        [| { Tick = 1L
             Sequence = 0
             Command = Command.moveTo (CommandId.ofInt 1) 1L (AgentId.ofInt 0) { X = 12; Y = 0 }
             Issuer = Issuer }
           { Tick = 1L
             Sequence = 1
             Command = Command.moveTo (CommandId.ofInt 2) 1L (AgentId.ofInt 1) { X = 9; Y = 1 }
             Issuer = Issuer }
           { Tick = 1L
             Sequence = 2
             Command = Command.suppress (CommandId.ofInt 3) 1L (AgentId.ofInt 2) (AgentId.ofInt 4)
             Issuer = Issuer }
           { Tick = 12L
             Sequence = 0
             Command = Command.moveTo (CommandId.ofInt 4) 12L (AgentId.ofInt 3) { X = 9; Y = 8 }
             Issuer = Issuer }
           { Tick = 25L
             Sequence = 0
             Command = Command.moveTo (CommandId.ofInt 5) 25L (AgentId.ofInt 0) { X = 3; Y = 9 }
             Issuer = Issuer } |]
