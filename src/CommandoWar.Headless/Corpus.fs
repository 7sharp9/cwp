namespace CommandoWar.Headless

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open CommandoWar.Sim

/// The committed replay corpus (TASK-016, backlog B-012;
/// `docs/09_TEST_STRATEGY.md` section 2.4 "Maintain a small replay corpus").
///
/// Each entry is a named initial `WorldState`, a committed command file, and a
/// committed per-tick authoritative-hash table (`content/replays/<Name>.md`,
/// mirroring `content/fixtures/SPIKE-FIXTURE.md`). Since TASK-036 (backlog
/// B-049) every entry but `spike-fixture` authors its geometry and command
/// schedule as one `ScenarioSpec` value (below); the committed
/// `content/replays/<Name>.cwreplay` (`ReplaySerialisation`) is a generated,
/// human-reviewable artefact derived from that value, not read back at
/// runtime. `spike-fixture` alone still reads its commands from a
/// hand-authored `content/replays/spike-fixture.cwlog` (the legacy v1
/// grammar, `CommandLogFile`). `cwheadless corpus` and the in-suite
/// `CorpusTests` theory both replay every entry and compare its per-tick
/// hashes to the committed table: a mismatch is a determinism regression that
/// fails a build instead of rotting silently.
///
/// This module is an OBSERVER over `Replay` / `Divergence`, exactly as
/// `DiagnosticRender` is over `Diagnostics`: it adds no authoritative state and
/// nothing in `Simulation.step` references it. The non-fixture entries' initial
/// states are focused `RawScenario` vectors (the `PathDemo` / `LosDemo`
/// precedent), validated through `Scenario.validate` and instantiated through
/// `World.ofScenario`; they are not authoritative game content and need no
/// on-disk format (backlog B-024).
[<RequireQualifiedAccess>]
module Corpus =

    /// Repository-relative directory holding the committed corpus entries.
    [<Literal>]
    let DefaultDir = "content/replays"

    /// One corpus entry.
    type Entry =
        { /// File stem under `content/replays/`: `<Name>.md` always, plus
          /// `<Name>.cwreplay` (builder-authored entries) or `<Name>.cwlog`
          /// (`spike-fixture` only).
          Name: string
          /// One-paragraph explanation, copied verbatim into the generated
          /// `<Name>.md` so regeneration is byte-stable.
          Description: string
          /// Free-text note for the generated table's "Initial state" row.
          InitialStateNote: string
          /// The authoritative world at tick 0 for this entry.
          InitialState: unit -> WorldState
          /// Ticks the entry's replay covers.
          TickCount: int64
          /// The entry's accepted commands, when authored in F# by the
          /// `ScenarioSpec` builder (TASK-036, backlog B-049): this value is
          /// the sole runtime source of truth, and the committed
          /// `<name>.cwreplay` is a generated, diff-checked artefact derived
          /// from it, never read back. `None` only for `spike-fixture`,
          /// whose commands still come from its hand-authored
          /// `content/replays/spike-fixture.cwlog` (out of this task's
          /// scope — it is the framework-spike shared fixture, not a
          /// `ScenarioSpec`).
          Commands: RecordedCommand[] option }

    // --- shared fixture builder (TASK-036, backlog B-049) ------------------

    /// Deterministic seed for the eleven builder-authored scenarios. No
    /// gameplay phase draws from the stream; the value only has to be stable.
    [<Literal>]
    let private Seed = 20260904UL

    let private wall (x: int) (y: int) : RawTerrainCell =
        { Cell = { X = x; Y = y }
          Class = "impassable"
          Elevation = 0
          MoveCost = 0
          Opaque = false }

    /// An impassable cell that also blocks line of sight — for `perception-contact`,
    /// the wall a friendly must clear before it can see the hostile beyond it
    /// (the "Unknown threat" shape, `docs/05` section 16).
    let private opaqueWall (x: int) (y: int) : RawTerrainCell =
        { Cell = { X = x; Y = y }
          Class = "impassable"
          Elevation = 0
          MoveCost = 0
          Opaque = true }

    /// A passable cell whose entry cost is above `Terrain.BaseMoveCost` (the
    /// `PathDemo` "costly" precedent) — for `slow-terrain`, the one cell
    /// whose crossing takes more than one tick (TASK-018 sub-cell progress).
    let private costly (x: int) (y: int) (moveCost: int) : RawTerrainCell =
        { Cell = { X = x; Y = y }
          Class = "passable"
          Elevation = 0
          MoveCost = moveCost
          Opaque = false }

    /// One agent's authored deployment.
    type private ScenarioAgent =
        { Id: int
          Cell: Cell
          Discipline: int
          CommunicationAvailable: bool }

    /// One authored `MoveTo` order, delivered on `Tick` to `Agent`. Every
    /// corpus entry today needs only a single-recipient move with the
    /// default envelope (`Command.moveTo`'s `Routine`/`Standard`); a richer
    /// shape is deferred until an entry actually needs one (`AGENTS.md`:
    /// prefer the smallest change).
    type private ScenarioOrder = { Tick: int64; Agent: int; Target: Cell }

    /// One corpus scenario authored as a single value: geometry and its
    /// command schedule together, so the two cannot independently drift the
    /// way a `Corpus.fs` deployment and a hand-typed `.cwlog` could
    /// (TASK-036, backlog B-049; `docs/notes/2026-09-06-tooling-and-debug-
    /// display.md` section 1). `worldOfSpec` and `commandsOfSpec` are this
    /// value's two projections — the corpus `WorldState` and its
    /// `RecordedCommand[]` — and are never authored independently.
    type private ScenarioSpec =
        { Id: string
          Width: int
          Height: int
          Friendly: ScenarioAgent list
          Enemies: ScenarioAgent list
          Terrain: RawTerrainCell list
          Objective: Cell
          Extraction: Cell
          Orders: ScenarioOrder list }

    let private agent (id: int) (cell: Cell) : ScenarioAgent =
        { Id = id
          Cell = cell
          Discipline = AppraisalConfig.DisciplineDefault
          CommunicationAvailable = true }

    /// An agent with a non-default `Discipline` (`exposed-approach`).
    let private agentWith (id: int) (cell: Cell) (discipline: int) : ScenarioAgent =
        { agent id cell with Discipline = discipline }

    /// An agent with an authored comms blackout (`lost-comms`): the
    /// Communication phase cannot reach it, so any order to it is dropped.
    let private blackedOut (id: int) (cell: Cell) : ScenarioAgent =
        { agent id cell with CommunicationAvailable = false }

    let private order (tick: int64) (agentId: int) (target: Cell) : ScenarioOrder =
        { Tick = tick; Agent = agentId; Target = target }

    let private rawOf (spec: ScenarioSpec) : RawScenario =
        let deployment (a: ScenarioAgent) : RawDeployment =
            { AgentId = a.Id
              Cell = a.Cell
              CommunicationAvailable = a.CommunicationAvailable
              Discipline = a.Discipline }

        { ContentVersion = ScenarioContent.Version
          Id = spec.Id
          Width = spec.Width
          Height = spec.Height
          FriendlyDeployments = spec.Friendly |> List.map deployment |> List.toArray
          EnemyDeployments = spec.Enemies |> List.map deployment |> List.toArray
          ObjectiveAreas = [| { AreaId = "objective"; Cell = spec.Objective } |]
          ExtractionAreas = [| { AreaId = "exit"; Cell = spec.Extraction } |]
          StaticTargets = [||]
          Objectives =
            [| { Id = 1
                 Kind = "reach"
                 AreaRef = "objective"
                 TargetRef = ""
                 HoldTicks = 0
                 ExtractAgentIds = [||]
                 IsOptional = false } |]
          TerrainLayer =
            match spec.Terrain with
            | [] -> None
            | cs ->
                Some
                    { Width = spec.Width
                      Height = spec.Height
                      Cells = List.toArray cs
                      Cover = [||] }
          FailOnFriendlyForceEliminated = true }

    /// Validates and instantiates a corpus scenario's initial `WorldState`.
    /// Fails hard: these are fixed test vectors, so a validation or build
    /// error here is a bug in this file.
    let private worldOfSpec (spec: ScenarioSpec) : WorldState =
        let raw = rawOf spec

        match Scenario.validate raw with
        | Error es -> failwith $"corpus scenario '{raw.Id}' is malformed: {es}"
        | Ok scenario ->
            match World.ofScenario scenario Seed with
            | Ok w -> w
            | Error e -> failwith $"corpus world '{raw.Id}' build failed: {e}"

    /// Derives the entry's accepted commands from the same authored value
    /// `worldOfSpec` reads. `CommandId` is assigned from order of appearance
    /// in `spec.Orders` (1-based) and `Sequence` from order of appearance
    /// within a tick — the `CommandLogFile.parse` numbering, preserved here
    /// so a migrated entry's canonical hashes (which encode `CommandId`,
    /// `Canonical.fs`) do not move.
    let private commandsOfSpec (spec: ScenarioSpec) : RecordedCommand[] =
        spec.Orders
        |> List.mapi (fun i o -> i + 1, o)
        |> List.groupBy (fun (_, o) -> o.Tick)
        |> List.collect (fun (tick, group) ->
            group
            |> List.sortBy fst
            |> List.mapi (fun seq (appearanceId, o) ->
                { Tick = tick
                  Sequence = seq
                  Command = Command.moveTo (CommandId.ofInt appearanceId) tick (AgentId.ofInt o.Agent) o.Target
                  Issuer = "corpus" }))
        |> List.sortBy (fun c -> c.Tick, c.Sequence)
        |> List.toArray

    /// One friendly agent at (1,3) with an impassable wall at x=5, rows 0..6
    /// (rows 7..8 open). A `MoveTo (10,3)` forces a detour around the gap.
    let private wallDetourSpec: ScenarioSpec =
        { Id = "corpus-wall-detour"
          Width = 12
          Height = 9
          Friendly = [ agent 0 { X = 1; Y = 3 } ]
          Enemies = []
          Terrain = [ for y in 0..6 -> wall 5 y ]
          Objective = { X = 11; Y = 0 }
          Extraction = { X = 0; Y = 8 }
          Orders = [ order 1L 0 { X = 10; Y = 3 } ] }

    /// One friendly agent at (1,4); the target (5,4) is passable but its four
    /// cardinal neighbours are impassable, so `Pathfinding` returns `NoPath`
    /// and the executor emits `MovementBlocked`.
    let private blockedGoalSpec: ScenarioSpec =
        { Id = "corpus-blocked-goal"
          Width = 8
          Height = 8
          Friendly = [ agent 0 { X = 1; Y = 4 } ]
          Enemies = []
          Terrain = [ wall 4 4; wall 6 4; wall 5 3; wall 5 5 ]
          Objective = { X = 7; Y = 0 }
          Extraction = { X = 0; Y = 7 }
          Orders = [ order 1L 0 { X = 5; Y = 4 } ] }

    /// Two friendly agents on open terrain: agent 0 at (3,0) -> (3,7) crosses
    /// agent 1 at (0,3) -> (7,3), both computing (3,3) as their next cell at
    /// tick 3. TASK-017 (B-011b) reservation resolves the contest: agent 0
    /// wins (tied remaining route length, lower agent id) and agent 1 yields
    /// one tick, then both reach their destinations by tick 12.
    let private convergingRoutesSpec: ScenarioSpec =
        { Id = "corpus-converging-routes"
          Width = 8
          Height = 8
          Friendly = [ agent 0 { X = 3; Y = 0 }; agent 1 { X = 0; Y = 3 } ]
          Enemies = []
          Terrain = []
          Objective = { X = 7; Y = 7 }
          Extraction = { X = 0; Y = 0 }
          Orders = [ order 1L 0 { X = 3; Y = 7 }; order 1L 1 { X = 7; Y = 3 } ] }

    /// One friendly agent at (0,0) ordered to (4,0), open terrain except
    /// (1,0), which costs 3 to enter (`Terrain.BaseMoveCost` elsewhere is 1).
    /// Crossing that one cell takes 3 ticks of accumulated `AgentState.Progress`
    /// (TASK-018 sub-cell movement progress) before the agent enters it; every
    /// other cell is entered in the usual single tick.
    let private slowTerrainSpec: ScenarioSpec =
        { Id = "corpus-slow-terrain"
          Width = 8
          Height = 8
          Friendly = [ agent 0 { X = 0; Y = 0 } ]
          Enemies = []
          Terrain = [ costly 1 0 3 ]
          Objective = { X = 7; Y = 7 }
          Extraction = { X = 0; Y = 7 }
          Orders = [ order 1L 0 { X = 4; Y = 0 } ] }

    /// Three friendly agents in a line at (1,3), (2,3), (3,3), all ordered east
    /// to (11,3). Each tick the lead agent has a free cell ahead, so the
    /// TASK-022 vacation chain resolves and all three step together; no agent
    /// reaches (11,3) within the run, so the chain flows every tick with no
    /// obstruction.
    let private followChainSpec: ScenarioSpec =
        { Id = "corpus-follow-chain"
          Width = 12
          Height = 9
          Friendly = [ agent 0 { X = 1; Y = 3 }; agent 1 { X = 2; Y = 3 }; agent 2 { X = 3; Y = 3 } ]
          Enemies = []
          Terrain = []
          Objective = { X = 11; Y = 3 }
          Extraction = { X = 0; Y = 8 }
          Orders =
            [ order 1L 0 { X = 11; Y = 3 }
              order 1L 1 { X = 11; Y = 3 }
              order 1L 2 { X = 11; Y = 3 } ] }

    /// Two friendly agents at (3,3) and (4,3), each ordered onto the other's
    /// cell. A two-agent position swap is blocked (TASK-022): neither is ever a
    /// first mover, so both emit `MovementObstructed` every tick and neither
    /// agent ever leaves its start cell.
    let private swapStandoffSpec: ScenarioSpec =
        { Id = "corpus-swap-standoff"
          Width = 8
          Height = 8
          Friendly = [ agent 0 { X = 3; Y = 3 }; agent 1 { X = 4; Y = 3 } ]
          Enemies = []
          Terrain = []
          Objective = { X = 7; Y = 7 }
          Extraction = { X = 0; Y = 0 }
          Orders = [ order 1L 0 { X = 4; Y = 3 }; order 1L 1 { X = 3; Y = 3 } ] }

    /// One friendly agent at (1,5) ordered east to (9,5), and a stationary
    /// hostile agent 1 at (9,1) behind an opaque impassable wall at x = 6,
    /// rows 0..3. While the friendly is west of the wall its line of sight to
    /// the hostile is blocked, even though the hostile is well inside
    /// `PerceptionConfig.SightRange`; once the friendly clears the wall the
    /// Perception phase records the contact (`ContactObserved`) and the
    /// Tactical-knowledge phase puts it in the shared squad picture
    /// (`WorldState.TacticalKnowledge`). The first corpus entry with an enemy
    /// deployment and the "Unknown threat" shape (`docs/05` section 16).
    let private perceptionContactSpec: ScenarioSpec =
        { Id = "corpus-perception-contact"
          Width = 12
          Height = 8
          Friendly = [ agent 0 { X = 1; Y = 5 } ]
          Enemies = [ agent 1 { X = 9; Y = 1 } ]
          Terrain = [ for y in 0..3 -> opaqueWall 6 y ]
          Objective = { X = 9; Y = 5 }
          Extraction = { X = 0; Y = 7 }
          Orders = [ order 1L 0 { X = 9; Y = 5 } ] }

    /// One friendly agent 0 at (1,4) with `CommunicationAvailable = false`
    /// (an authored comms blackout), ordered east to (6,4) on tick 1. Command
    /// intake accepts the order (`CommandAccepted`), but the Communication
    /// phase cannot reach the recipient, so it emits `OrderUndelivered` and
    /// drops the order: no `Destination` is ever written and the agent never
    /// moves. The "Lost communication" vertical-slice scenario
    /// (`docs/05` section 16; TASK-027, backlog B-016).
    let private lostCommsSpec: ScenarioSpec =
        { Id = "corpus-lost-comms"
          Width = 8
          Height = 8
          Friendly = [ blackedOut 0 { X = 1; Y = 4 } ]
          Enemies = []
          Terrain = []
          Objective = { X = 7; Y = 0 }
          Extraction = { X = 0; Y = 7 }
          Orders = [ order 1L 0 { X = 6; Y = 4 } ] }

    /// Two friendlies on open ground ordered along the same exposed approach
    /// past a stationary hostile (a machine-gun position) the squad can see
    /// from the start. Agent 0 (`Discipline 1`) at (1,3) is ordered to (11,3);
    /// agent 1 (`Discipline 6`) at (1,5) is ordered to (11,5). Both routes run
    /// the same distance past the known threat at (10,4), so exposure is
    /// near-identical — the divergence is discipline alone: on tick 1 the
    /// Appraisal phase `Refuses` agent 0's order (`RouteTooExposed`, no
    /// `Destination`, it never moves) and `Accepts` agent 1's (`Destination`
    /// written, it walks the approach). The G3 evidence scenario
    /// (`docs/07` section 9 criterion 2: "at least two soldiers appraise the
    /// same order differently for traceable reasons"; TASK-028, backlog B-017).
    let private exposedApproachSpec: ScenarioSpec =
        { Id = "corpus-exposed-approach"
          Width = 12
          Height = 8
          Friendly = [ agentWith 0 { X = 1; Y = 3 } 1; agentWith 1 { X = 1; Y = 5 } 6 ]
          Enemies = [ agent 2 { X = 10; Y = 4 } ]
          Terrain = []
          Objective = { X = 11; Y = 4 }
          Extraction = { X = 0; Y = 7 }
          Orders = [ order 1L 0 { X = 11; Y = 3 }; order 1L 1 { X = 11; Y = 5 } ] }

    /// One friendly agent at (1,4), open ground, no threats: ordered east to
    /// (14,4) on tick 1 (Accepted, `CommitmentEstablished`), then re-ordered
    /// south to (14,8) on tick 3 while still mid-route toward the first
    /// target. The second order supersedes the first: Communication resets
    /// `Disposition`, Appraisal re-accepts against the new target, and
    /// `commitmentAndLocalAction` emits a fresh `CommitmentEstablished` for
    /// the second command with no event reporting the first commitment's end
    /// (TASK-030, backlog B-018, Decision D/H — the one interrupt priority
    /// current systems can source, `docs/05` section 11 priority 6 "new
    /// higher-priority command"; `docs/07` section 8 step 7, "the player
    /// reissues the original intent").
    let private reissuedOrderSpec: ScenarioSpec =
        { Id = "corpus-reissued-order"
          Width = 16
          Height = 9
          Friendly = [ agent 0 { X = 1; Y = 4 } ]
          Enemies = []
          Terrain = []
          Objective = { X = 14; Y = 0 }
          Extraction = { X = 0; Y = 8 }
          Orders = [ order 1L 0 { X = 14; Y = 4 }; order 3L 0 { X = 14; Y = 8 } ] }

    /// A friendly and a hostile within `CombatConfig.WeaponRange` and clear
    /// line of sight from tick 1, both stationary (no orders) — the Combat
    /// phase alone drives the trace, proving the phase wiring end-to-end
    /// (TASK-031, backlog B-019).
    let private openEngagementSpec: ScenarioSpec =
        { Id = "corpus-open-engagement"
          Width = 10
          Height = 10
          Friendly = [ agent 0 { X = 2; Y = 2 } ]
          Enemies = [ agent 1 { X = 7; Y = 2 } ]
          Terrain = []
          Objective = { X = 9; Y = 0 }
          Extraction = { X = 0; Y = 9 }
          Orders = [] }

    /// Every corpus entry, in a fixed order.
    let all: Entry[] =
        [| { Name = "spike-fixture"
             Description =
               "The framework-spike shared fixture (Setup.sixAgentWorld, 32 x 32, seed 20260902): agent 3 is "
               + "ordered to (20,14) at tick 1. Mirrors content/fixtures/spike-fixture.cwlog and SPIKE-FIXTURE.md; "
               + "CorpusTests cross-checks this table against Fixture.run () so it is not an independent re-pin."
             InitialStateNote = "Fixture.initialState () (Setup.sixAgentWorld, 32 x 32, seed 20260902)"
             InitialState = Fixture.initialState
             TickCount = Fixture.TickCount
             Commands = None }
           { Name = "wall-detour"
             Description =
               "One friendly agent at (1,3) ordered to (10,3) with an impassable wall at x=5, rows 0..6. "
               + "Pathfinding.findWithin routes it around the gap at rows 7..8 and the executor advances one "
               + "cell per tick (docs/04 section 8 steps 2, 4, 5)."
             InitialStateNote = "Corpus wall-detour scenario (12 x 9, seed 20260904)"
             InitialState = fun () -> worldOfSpec wallDetourSpec
             TickCount = 24L
             Commands = Some(commandsOfSpec wallDetourSpec) }
           { Name = "blocked-goal"
             Description =
               "One friendly agent at (1,4) ordered to (5,4), a passable cell ringed by impassable cells. "
               + "Pathfinding returns NoPath, so the executor emits MovementBlocked and clears the destination; "
               + "the remaining ticks are rest (the agent does not retry)."
             InitialStateNote = "Corpus blocked-goal scenario (8 x 8, seed 20260904)"
             InitialState = fun () -> worldOfSpec blockedGoalSpec
             TickCount = 5L
             Commands = Some(commandsOfSpec blockedGoalSpec) }
           { Name = "converging-routes"
             Description =
               "Agent 0 at (3,0) -> (3,7) crosses agent 1 at (0,3) -> (7,3); both compute (3,3) as their next "
               + "cell at tick 3. TASK-017 reservation resolves the contest (tied remaining route length, lower "
               + "agent id wins): agent 0 enters (3,3), agent 1 yields one tick and catches up."
             InitialStateNote = "Corpus converging-routes scenario (8 x 8, seed 20260904)"
             InitialState = fun () -> worldOfSpec convergingRoutesSpec
             TickCount = 12L
             Commands = Some(commandsOfSpec convergingRoutesSpec) }
           { Name = "slow-terrain"
             Description =
               "One friendly agent at (0,0) ordered to (4,0); cell (1,0) costs 3 to enter (elsewhere "
               + "Terrain.BaseMoveCost = 1). AgentState.Progress accumulates 1, 2, then reaches the threshold "
               + "and the agent enters the cell on the third tick (TASK-018); every other cell is entered in "
               + "the usual single tick."
             InitialStateNote = "Corpus slow-terrain scenario (8 x 8, seed 20260904)"
             InitialState = fun () -> worldOfSpec slowTerrainSpec
             TickCount = 8L
             Commands = Some(commandsOfSpec slowTerrainSpec) }
           { Name = "follow-chain"
             Description =
               "Three friendly agents in a line at (1,3), (2,3), (3,3), all ordered east to (11,3). Each tick the "
               + "lead agent has a free cell ahead, so TASK-022's vacation-chain resolution lets the whole chain "
               + "advance on the same tick; no agent reaches (11,3) within the run, so the chain flows every tick "
               + "with no MovementObstructed."
             InitialStateNote = "Corpus follow-chain scenario (12 x 9, seed 20260904)"
             InitialState = fun () -> worldOfSpec followChainSpec
             TickCount = 6L
             Commands = Some(commandsOfSpec followChainSpec) }
           { Name = "swap-standoff"
             Description =
               "Two friendly agents at (3,3) and (4,3), each ordered onto the other's cell. A two-agent position "
               + "swap is blocked (TASK-022): neither agent is ever a first mover, so both emit MovementObstructed "
               + "every tick and neither agent ever leaves its start cell (only the tick counter advances)."
             InitialStateNote = "Corpus swap-standoff scenario (8 x 8, seed 20260904)"
             InitialState = fun () -> worldOfSpec swapStandoffSpec
             TickCount = 4L
             Commands = Some(commandsOfSpec swapStandoffSpec) }
           { Name = "perception-contact"
             Description =
               "One friendly agent at (1,5) ordered east to (9,5); a stationary hostile agent 1 at (9,1) behind an "
               + "opaque impassable wall at x=6, rows 0..3. The hostile is inside PerceptionConfig.SightRange from "
               + "the start but line of sight is blocked; once the friendly clears the wall the Perception phase "
               + "emits ContactObserved and the Tactical-knowledge phase adds the contact to the shared squad "
               + "picture (WorldState.TacticalKnowledge). The first corpus entry with an "
               + "enemy deployment (TASK-026, backlog B-015; the 'Unknown threat' shape, docs/05 section 16). The "
               + "order is issued on tick 1, before the contact is known, so it is Accepted at appraisal and not "
               + "re-judged when the contact appears (TASK-028; reappraisal on a knowledge change is B-021)."
             InitialStateNote = "Corpus perception-contact scenario (12 x 8, seed 20260904, 1 friendly + 1 hostile)"
             InitialState = fun () -> worldOfSpec perceptionContactSpec
             TickCount = 14L
             Commands = Some(commandsOfSpec perceptionContactSpec) }
           { Name = "lost-comms"
             Description =
               "One friendly agent 0 at (1,4) with CommunicationAvailable = false (an authored comms blackout), "
               + "ordered east to (6,4) on tick 1. Command intake accepts the order (CommandAccepted), but the "
               + "Communication phase cannot reach the recipient, so it emits OrderUndelivered and drops the order: "
               + "no Destination is written and the agent never moves. The 'Lost communication' vertical-slice "
               + "scenario (docs/05 section 16; TASK-027, backlog B-016). Because the order is dropped, no "
               + "AgentState.Order is written and the Appraisal phase (TASK-028) never runs on it: this is the one "
               + "entry with no OrderAppraised event."
             InitialStateNote = "Corpus lost-comms scenario (8 x 8, seed 20260904, 1 friendly, comms blackout)"
             InitialState = fun () -> worldOfSpec lostCommsSpec
             TickCount = 4L
             Commands = Some(commandsOfSpec lostCommsSpec) }
           { Name = "exposed-approach"
             Description =
               "Two friendlies ordered along the same exposed approach past a stationary hostile (a machine-gun "
               + "position) the squad sees from the start: agent 0 (Discipline 1) at (1,3) -> (11,3), agent 1 "
               + "(Discipline 6) at (1,5) -> (11,5), both on tick 1, both routes the same distance past the known "
               + "threat at (10,4). The divergence is discipline alone: on tick 1 the Appraisal phase (12.5) "
               + "Refuses agent 0's order (RouteTooExposed, no Destination, it never moves) and Accepts agent 1's "
               + "(Destination written, it walks the approach). The G3 evidence scenario (docs/07 section 9 "
               + "criterion 2; TASK-028, backlog B-017; Canonical.FormatVersion 4)."
             InitialStateNote =
               "Corpus exposed-approach scenario (12 x 8, seed 20260904, 2 friendlies Discipline 1 / 6 + 1 hostile)"
             InitialState = fun () -> worldOfSpec exposedApproachSpec
             TickCount = 12L
             Commands = Some(commandsOfSpec exposedApproachSpec) }
           { Name = "reissued-order"
             Description =
               "One friendly agent at (1,4) ordered east to (14,4) on tick 1 (Accepted, CommitmentEstablished), "
               + "then re-ordered south to (14,8) on tick 3 while still mid-route. The second order supersedes "
               + "the first: Appraisal re-accepts against the new target and commitmentAndLocalAction emits a "
               + "fresh CommitmentEstablished for the second command, with no event reporting the first "
               + "commitment's end (TASK-030, backlog B-018)."
             InitialStateNote = "Corpus reissued-order scenario (16 x 9, seed 20260904, 1 friendly)"
             InitialState = fun () -> worldOfSpec reissuedOrderSpec
             TickCount = 6L
             Commands = Some(commandsOfSpec reissuedOrderSpec) }
           { Name = "open-engagement"
             Description =
               "A friendly at (2,2) and a hostile at (7,2), open ground, no orders on either side: the Combat "
               + "phase alone drives the trace. Both are within CombatConfig.WeaponRange and clear line of sight "
               + "from tick 1, so the deterministic hitscan mechanic (range + directional-cover-mitigated hit "
               + "chance, the stream's first real gameplay draw) fires every tick, symmetric both ways "
               + "(TASK-031, backlog B-019)."
             InitialStateNote = "Corpus open-engagement scenario (10 x 10, seed 20260904, 1 friendly + 1 hostile)"
             InitialState = fun () -> worldOfSpec openEngagementSpec
             TickCount = 3L
             Commands = Some(commandsOfSpec openEngagementSpec) } |]

    // --- entry paths and loading ----------------------------------------

    let private logPathIn (dir: string) (e: Entry) = Path.Combine(dir, e.Name + ".cwlog")
    let private replayPathIn (dir: string) (e: Entry) = Path.Combine(dir, e.Name + ".cwreplay")
    let private tablePathIn (dir: string) (e: Entry) = Path.Combine(dir, e.Name + ".md")

    /// Parses an entry's hand-authored command-log file. Only `spike-fixture`
    /// still uses this (`content/replays/spike-fixture.cwlog`, TASK-036 out
    /// of scope); every builder-authored entry carries `Entry.Commands`
    /// directly and never reaches this function.
    let loadLog (dir: string) (e: Entry) : Result<RecordedCommand[], string> =
        let p = logPathIn dir e

        if not (File.Exists p) then
            Error $"command-log file not found: {p}"
        else
            match CommandLogFile.parse "corpus" (File.ReadAllText p) with
            | Ok cmds -> Ok cmds
            | Error err -> Error $"{p}: {CommandLogFile.describeError err}"

    /// An entry's accepted commands: `Entry.Commands` when the builder
    /// authored them (the sole runtime source of truth, TASK-036), otherwise
    /// `loadLog` (`spike-fixture` only).
    let commandsOf (dir: string) (e: Entry) : Result<RecordedCommand[], string> =
        match e.Commands with
        | Some cmds -> Ok cmds
        | None -> loadLog dir e

    /// Replays an entry from its initial state and parsed command log.
    let run (e: Entry) (cmds: RecordedCommand[]) : Result<ReplayOutcome, ReplayError> =
        Replay.record
            { Build = "cwheadless"; Scenario = e.Name }
            (e.InitialState ())
            e.TickCount
            (CommandLog.create cmds)
        |> Replay.run SimConfig.standard

    // --- committed hash table -----------------------------------------

    /// The machine-readable content of a committed `<Name>.md` table.
    type CommittedTable =
        { TickCount: int64
          InitialHash: uint64
          FinalHash: uint64
          EventCount: int
          /// (tick, hash) rows in tick order.
          TickHashes: (int64 * uint64)[] }

    let private hx (v: uint64) = sprintf "0x%016X" v

    /// Renders an entry's committed table from a fresh replay outcome.
    /// Byte-deterministic: no timestamps, `\n` line endings.
    let renderTable (e: Entry) (outcome: ReplayOutcome) : string =
        let sb = StringBuilder()
        let line (s: string) = sb.Append(s).Append('\n') |> ignore

        let initial = (Hashing.hash (e.InitialState ())).Value

        let final =
            if outcome.TickHashes.Length = 0 then
                initial
            else
                outcome.TickHashes.[outcome.TickHashes.Length - 1].Hash.Value

        line $"# Replay corpus entry: {e.Name}"
        line ""
        line e.Description
        line ""
        line "Regenerate every corpus hash table from the repository root:"
        line ""
        line "    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate"
        line ""
        line "An unexplained change to the numbers below is a determinism regression."
        line "See `content/replays/CORPUS.md`."
        line ""
        line "## Parameters"
        line ""
        line "| Parameter | Value |"
        line "|---|---|"
        let commandFileExt = if e.Commands.IsSome then "cwreplay" else "cwlog"
        line $"| Command log | `{e.Name}.{commandFileExt}` |"
        line $"| Initial state | {e.InitialStateNote} |"
        line $"| Tick count | {e.TickCount} |"
        line $"| Initial hash (tick 0) | `{hx initial}` |"
        line $"| Final hash (tick {e.TickCount}) | `{hx final}` |"
        line $"| Domain events | {outcome.Events.Length} |"
        line ""
        line "## Per-tick authoritative state hash"
        line ""
        line "| tick | state hash          |"
        line "|-----:|---------------------|"

        for cp in outcome.TickHashes do
            line (sprintf "| %4d | `%s` |" cp.Tick (hx cp.Hash.Value))

        sb.ToString()

    let private rowRegex =
        Regex(@"^\|\s*(\d+)\s*\|\s*`0x([0-9A-Fa-f]{16})`\s*\|\s*$")

    let private hexRegex = Regex(@"0x([0-9A-Fa-f]{16})")

    /// Parses the machine-readable parts of a committed `<Name>.md` table.
    /// Prose and the "Initial state" row are ignored.
    let parseTable (text: string) : Result<CommittedTable, string> =
        let lines = text.Replace("\r\n", "\n").Split('\n')

        let mutable tickCount: int64 option = None
        let mutable initial: uint64 option = None
        let mutable final: uint64 option = None
        let mutable events: int option = None
        let rows = ResizeArray<int64 * uint64>()

        let hexOn (line: string) =
            let m = hexRegex.Match line
            if m.Success then Some(Convert.ToUInt64(m.Groups.[1].Value, 16)) else None

        let intCell (label: string) (line: string) =
            let m = Regex.Match(line, @"\|\s*" + Regex.Escape label + @"[^|]*\|\s*(\d+)\s*\|")
            if m.Success then Some(Int64.Parse m.Groups.[1].Value) else None

        for raw in lines do
            let line = raw.TrimEnd()
            let m = rowRegex.Match line

            if m.Success then
                rows.Add(Int64.Parse m.Groups.[1].Value, Convert.ToUInt64(m.Groups.[2].Value, 16))
            elif line.Contains "| Tick count " then
                tickCount <- intCell "Tick count" line
            elif line.Contains "Initial hash" then
                initial <- hexOn line
            elif line.Contains "Final hash" then
                final <- hexOn line
            elif line.Contains "Domain events" then
                events <- intCell "Domain events" line |> Option.map int

        match tickCount, initial, final, events with
        | Some tc, Some i, Some f, Some ev when rows.Count > 0 ->
            Ok
                { TickCount = tc
                  InitialHash = i
                  FinalHash = f
                  EventCount = ev
                  TickHashes = rows.ToArray() }
        | _ -> Error "committed table is missing the tick count, initial/final hash, event count, or per-tick rows"

    // --- checking -----------------------------------------------------

    /// How a fresh replay of an entry disagrees with its committed table.
    type Divergence =
        { FirstBadTick: int64
          Expected: uint64
          Actual: uint64
          /// Random-stream draw count on the fresh run at the divergent tick.
          /// The committed table stores hashes only, so there is no committed
          /// side to compare.
          ActualDraws: uint64
          Note: string }

    /// The outcome of checking one entry.
    type EntryCheck =
        | Passed
        | LogError of string
        | ReplayFailed of ReplayError
        /// Two identical replays produced different hashes: genuine
        /// nondeterminism in this build. Carries the full `Divergence` report.
        | Nondeterministic of DivergenceReport
        | TableError of string
        /// This build's hashes disagree with the committed table.
        | Mismatch of Divergence

    let private compareToTable (e: Entry) (outcome: ReplayOutcome) (table: CommittedTable) : EntryCheck =
        let initialNow = (Hashing.hash (e.InitialState ())).Value

        let drawsAt (i: int) =
            if i < outcome.TickStates.Length then
                outcome.TickStates.[i].Random.Draws
            else
                0UL

        if initialNow <> table.InitialHash then
            Mismatch
                { FirstBadTick = 0L
                  Expected = table.InitialHash
                  Actual = initialNow
                  ActualDraws = 0UL
                  Note = "initial state (tick 0)" }
        elif table.TickCount <> e.TickCount then
            Mismatch
                { FirstBadTick = 0L
                  Expected = uint64 table.TickCount
                  Actual = uint64 e.TickCount
                  ActualDraws = 0UL
                  Note = "committed tick count differs from the entry definition; run corpus --regenerate" }
        else
            let committed = table.TickHashes
            let actual = outcome.TickHashes
            let overlap = min committed.Length actual.Length

            let firstDiff =
                seq { 0 .. overlap - 1 }
                |> Seq.tryFind (fun i -> snd committed.[i] <> actual.[i].Hash.Value)

            match firstDiff with
            | Some i ->
                Mismatch
                    { FirstBadTick = fst committed.[i]
                      Expected = snd committed.[i]
                      Actual = actual.[i].Hash.Value
                      ActualDraws = drawsAt i
                      Note = "" }
            | None when committed.Length <> actual.Length ->
                Mismatch
                    { FirstBadTick = int64 (overlap + 1)
                      Expected = uint64 committed.Length
                      Actual = uint64 actual.Length
                      ActualDraws = 0UL
                      Note = "run length differs from the committed table" }
            | None when outcome.Events.Length <> table.EventCount ->
                Mismatch
                    { FirstBadTick = 0L
                      Expected = uint64 table.EventCount
                      Actual = uint64 outcome.Events.Length
                      ActualDraws = 0UL
                      Note = "domain-event count differs though the per-tick hashes match" }
            | None -> Passed

    /// Replays an entry, checks it replays deterministically, and compares its
    /// per-tick hashes to the committed table.
    let checkEntry (dir: string) (e: Entry) : EntryCheck =
        match commandsOf dir e with
        | Error m -> LogError m
        | Ok cmds ->
            match run e cmds, run e cmds with
            | Error err, _
            | _, Error err -> ReplayFailed err
            | Ok outcome, Ok outcome2 ->
                match Divergence.compare outcome outcome2 with
                | Match _ ->
                    let tp = tablePathIn dir e

                    if not (File.Exists tp) then
                        TableError $"committed table not found: {tp}"
                    else
                        match parseTable (File.ReadAllText tp) with
                        | Error m -> TableError $"{tp}: {m}"
                        | Ok table -> compareToTable e outcome table
                | report -> Nondeterministic report

    // --- regeneration -----------------------------------------------

    /// The outcome of regenerating one entry's committed table.
    type RegenResult =
        | Wrote of path: string
        | RegenLogError of string
        | RegenReplayError of ReplayError

    /// The builder-authored `ReplayCommandFile` view of an entry's commands,
    /// for the committed `<name>.cwreplay` artefact (TASK-036). `InitialHash`
    /// and `Checkpoints` are left empty: `<name>.md` is already the one
    /// committed hash-table artefact, and duplicating hashes here would be a
    /// second source of the same truth.
    let private replayFileOf (e: Entry) (cmds: RecordedCommand[]) : ReplaySerialisation.ReplayCommandFile =
        { Version = ReplaySerialisation.FormatVersion
          Seed = Seed
          TickCount = e.TickCount
          CanonicalFormat = Canonical.FormatVersion
          Meta = { Build = "cwheadless"; Scenario = e.Name }
          InitialHash = None
          Checkpoints = [||]
          Commands = cmds }

    /// Rewrites an entry's `<Name>.md` from a fresh replay outcome, and, for a
    /// builder-authored entry, its committed `<Name>.cwreplay` from the same
    /// authored commands — a generated, human-reviewable artefact, not read
    /// back at runtime (Entry.Commands is authoritative; TASK-036).
    let regenerateEntry (dir: string) (e: Entry) : RegenResult =
        match commandsOf dir e with
        | Error m -> RegenLogError m
        | Ok cmds ->
            match run e cmds with
            | Error err -> RegenReplayError err
            | Ok outcome ->
                let p = tablePathIn dir e
                File.WriteAllText(p, renderTable e outcome)

                match e.Commands with
                | Some builderCmds -> File.WriteAllText(replayPathIn dir e, ReplaySerialisation.serialise (replayFileOf e builderCmds))
                | None -> ()

                Wrote p
