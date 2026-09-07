namespace CommandoWar.Headless

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open CommandoWar.Sim

/// The committed replay corpus (TASK-016, backlog B-012;
/// `docs/09_TEST_STRATEGY.md` section 2.4 "Maintain a small replay corpus").
///
/// Each entry is a named initial `WorldState`, a committed command-log file
/// (`content/replays/<Name>.cwlog`, the existing v1 format), and a committed
/// per-tick authoritative-hash table (`content/replays/<Name>.md`, mirroring
/// `content/fixtures/SPIKE-FIXTURE.md`). `cwheadless corpus` and the in-suite
/// `CorpusTests` theory both replay every entry and compare its per-tick hashes
/// to the committed table: a mismatch is a determinism regression that fails a
/// build instead of rotting silently.
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
        { /// File stem: `<Name>.cwlog` and `<Name>.md` under `content/replays/`.
          Name: string
          /// One-paragraph explanation, copied verbatim into the generated
          /// `<Name>.md` so regeneration is byte-stable.
          Description: string
          /// Free-text note for the generated table's "Initial state" row.
          InitialStateNote: string
          /// The authoritative world at tick 0 for this entry.
          InitialState: unit -> WorldState
          /// Ticks the entry's replay covers.
          TickCount: int64 }

    // --- hand-built scenarios for the non-fixture entries -----------------

    /// Deterministic seed for the three built scenarios. No gameplay phase
    /// draws from the stream; the value only has to be stable.
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

    /// Fills the common `RawScenario` fields: one "reach" objective on an
    /// objective area, one extraction area, no targets. `enemies` is `[]` for
    /// every entry except `perception-contact` (TASK-026): `rawScenario` used
    /// to hard-wire no enemy deployments, so the first enemy-bearing entry
    /// needed this generalisation. `Scenario.validate` and `World.ofScenario`
    /// already deploy friendly-then-enemy in ascending id order.
    let private rawScenario
        (id: string)
        (width: int)
        (height: int)
        (friendly: (int * Cell) list)
        (enemies: (int * Cell) list)
        (terrain: RawTerrainCell list)
        (objective: Cell)
        (extraction: Cell)
        : RawScenario =
        { ContentVersion = ScenarioContent.Version
          Id = id
          Width = width
          Height = height
          FriendlyDeployments =
            friendly
            |> List.map (fun (a, c) -> { AgentId = a; Cell = c })
            |> List.toArray
          EnemyDeployments =
            enemies
            |> List.map (fun (a, c) -> { AgentId = a; Cell = c })
            |> List.toArray
          ObjectiveAreas = [| { AreaId = "objective"; Cell = objective } |]
          ExtractionAreas = [| { AreaId = "exit"; Cell = extraction } |]
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
            match terrain with
            | [] -> None
            | cs ->
                Some
                    { Width = width
                      Height = height
                      Cells = List.toArray cs
                      Cover = [||] }
          FailOnFriendlyForceEliminated = true }

    /// Validates and instantiates a corpus scenario. Fails hard: these are
    /// fixed test vectors, so a validation or build error here is a bug in
    /// this file.
    let private worldOf (raw: RawScenario) : WorldState =
        match Scenario.validate raw with
        | Error es -> failwith $"corpus scenario '{raw.Id}' is malformed: {es}"
        | Ok scenario ->
            match World.ofScenario scenario Seed with
            | Ok w -> w
            | Error e -> failwith $"corpus world '{raw.Id}' build failed: {e}"

    /// One friendly agent at (1,3) with an impassable wall at x=5, rows 0..6
    /// (rows 7..8 open). A `MoveTo (10,3)` forces a detour around the gap.
    let private wallDetourWorld () : WorldState =
        worldOf (
            rawScenario "corpus-wall-detour" 12 9 [ 0, { X = 1; Y = 3 } ] [] [ for y in 0..6 -> wall 5 y ] { X = 11; Y = 0 } { X = 0; Y = 8 }
        )

    /// One friendly agent at (1,4); the target (5,4) is passable but its four
    /// cardinal neighbours are impassable, so `Pathfinding` returns `NoPath`
    /// and the executor emits `MovementBlocked`.
    let private blockedGoalWorld () : WorldState =
        worldOf (
            rawScenario
                "corpus-blocked-goal"
                8
                8
                [ 0, { X = 1; Y = 4 } ]
                []
                [ wall 4 4; wall 6 4; wall 5 3; wall 5 5 ]
                { X = 7; Y = 0 }
                { X = 0; Y = 7 }
        )

    /// Two friendly agents on open terrain: agent 0 at (3,0) -> (3,7) crosses
    /// agent 1 at (0,3) -> (7,3), both computing (3,3) as their next cell at
    /// tick 3. TASK-017 (B-011b) reservation resolves the contest: agent 0
    /// wins (tied remaining route length, lower agent id) and agent 1 yields
    /// one tick, then both reach their destinations by tick 12.
    let private convergingRoutesWorld () : WorldState =
        worldOf (
            rawScenario "corpus-converging-routes" 8 8 [ 0, { X = 3; Y = 0 }; 1, { X = 0; Y = 3 } ] [] [] { X = 7; Y = 7 } { X = 0; Y = 0 }
        )

    /// One friendly agent at (0,0) ordered to (4,0), open terrain except
    /// (1,0), which costs 3 to enter (`Terrain.BaseMoveCost` elsewhere is 1).
    /// Crossing that one cell takes 3 ticks of accumulated `AgentState.Progress`
    /// (TASK-018 sub-cell movement progress) before the agent enters it; every
    /// other cell is entered in the usual single tick.
    let private slowTerrainWorld () : WorldState =
        worldOf (rawScenario "corpus-slow-terrain" 8 8 [ 0, { X = 0; Y = 0 } ] [] [ costly 1 0 3 ] { X = 7; Y = 7 } { X = 0; Y = 7 })

    /// Three friendly agents in a line at (1,3), (2,3), (3,3), all ordered east
    /// to (11,3). Each tick the lead agent has a free cell ahead, so the
    /// TASK-022 vacation chain resolves and all three step together; no agent
    /// reaches (11,3) within the run, so the chain flows every tick with no
    /// obstruction.
    let private followChainWorld () : WorldState =
        worldOf (
            rawScenario
                "corpus-follow-chain"
                12
                9
                [ 0, { X = 1; Y = 3 }; 1, { X = 2; Y = 3 }; 2, { X = 3; Y = 3 } ]
                []
                []
                { X = 11; Y = 3 }
                { X = 0; Y = 8 }
        )

    /// Two friendly agents at (3,3) and (4,3), each ordered onto the other's
    /// cell. A two-agent position swap is blocked (TASK-022): neither is ever a
    /// first mover, so both emit `MovementObstructed` every tick and neither
    /// agent ever leaves its start cell.
    let private swapStandoffWorld () : WorldState =
        worldOf (
            rawScenario
                "corpus-swap-standoff"
                8
                8
                [ 0, { X = 3; Y = 3 }; 1, { X = 4; Y = 3 } ]
                []
                []
                { X = 7; Y = 7 }
                { X = 0; Y = 0 }
        )

    /// One friendly agent at (1,5) ordered east to (9,5), and a stationary
    /// hostile agent 1 at (9,1) behind an opaque impassable wall at x = 6,
    /// rows 0..3. While the friendly is west of the wall its line of sight to
    /// the hostile is blocked, even though the hostile is well inside
    /// `PerceptionConfig.SightRange`; once the friendly clears the wall the
    /// Perception phase records the contact (`ContactObserved`) and the
    /// Tactical-knowledge phase puts it in the shared squad picture
    /// (`WorldState.TacticalKnowledge`). The first corpus entry with an enemy
    /// deployment and the "Unknown threat" shape (`docs/05` section 16).
    let private perceptionContactWorld () : WorldState =
        worldOf (
            rawScenario
                "corpus-perception-contact"
                12
                8
                [ 0, { X = 1; Y = 5 } ]
                [ 1, { X = 9; Y = 1 } ]
                [ for y in 0..3 -> opaqueWall 6 y ]
                { X = 9; Y = 5 }
                { X = 0; Y = 7 }
        )

    /// Every corpus entry, in a fixed order.
    let all: Entry[] =
        [| { Name = "spike-fixture"
             Description =
               "The framework-spike shared fixture (Setup.sixAgentWorld, 32 x 32, seed 20260902): agent 3 is "
               + "ordered to (20,14) at tick 1. Mirrors content/fixtures/spike-fixture.cwlog and SPIKE-FIXTURE.md; "
               + "CorpusTests cross-checks this table against Fixture.run () so it is not an independent re-pin."
             InitialStateNote = "Fixture.initialState () (Setup.sixAgentWorld, 32 x 32, seed 20260902)"
             InitialState = Fixture.initialState
             TickCount = Fixture.TickCount }
           { Name = "wall-detour"
             Description =
               "One friendly agent at (1,3) ordered to (10,3) with an impassable wall at x=5, rows 0..6. "
               + "Pathfinding.findWithin routes it around the gap at rows 7..8 and the executor advances one "
               + "cell per tick (docs/04 section 8 steps 2, 4, 5)."
             InitialStateNote = "Corpus wall-detour scenario (12 x 9, seed 20260904)"
             InitialState = wallDetourWorld
             TickCount = 24L }
           { Name = "blocked-goal"
             Description =
               "One friendly agent at (1,4) ordered to (5,4), a passable cell ringed by impassable cells. "
               + "Pathfinding returns NoPath, so the executor emits MovementBlocked and clears the destination; "
               + "the remaining ticks are rest (the agent does not retry)."
             InitialStateNote = "Corpus blocked-goal scenario (8 x 8, seed 20260904)"
             InitialState = blockedGoalWorld
             TickCount = 5L }
           { Name = "converging-routes"
             Description =
               "Agent 0 at (3,0) -> (3,7) crosses agent 1 at (0,3) -> (7,3); both compute (3,3) as their next "
               + "cell at tick 3. TASK-017 reservation resolves the contest (tied remaining route length, lower "
               + "agent id wins): agent 0 enters (3,3), agent 1 yields one tick and catches up."
             InitialStateNote = "Corpus converging-routes scenario (8 x 8, seed 20260904)"
             InitialState = convergingRoutesWorld
             TickCount = 12L }
           { Name = "slow-terrain"
             Description =
               "One friendly agent at (0,0) ordered to (4,0); cell (1,0) costs 3 to enter (elsewhere "
               + "Terrain.BaseMoveCost = 1). AgentState.Progress accumulates 1, 2, then reaches the threshold "
               + "and the agent enters the cell on the third tick (TASK-018); every other cell is entered in "
               + "the usual single tick."
             InitialStateNote = "Corpus slow-terrain scenario (8 x 8, seed 20260904)"
             InitialState = slowTerrainWorld
             TickCount = 8L }
           { Name = "follow-chain"
             Description =
               "Three friendly agents in a line at (1,3), (2,3), (3,3), all ordered east to (11,3). Each tick the "
               + "lead agent has a free cell ahead, so TASK-022's vacation-chain resolution lets the whole chain "
               + "advance on the same tick; no agent reaches (11,3) within the run, so the chain flows every tick "
               + "with no MovementObstructed."
             InitialStateNote = "Corpus follow-chain scenario (12 x 9, seed 20260904)"
             InitialState = followChainWorld
             TickCount = 6L }
           { Name = "swap-standoff"
             Description =
               "Two friendly agents at (3,3) and (4,3), each ordered onto the other's cell. A two-agent position "
               + "swap is blocked (TASK-022): neither agent is ever a first mover, so both emit MovementObstructed "
               + "every tick and neither agent ever leaves its start cell (only the tick counter advances)."
             InitialStateNote = "Corpus swap-standoff scenario (8 x 8, seed 20260904)"
             InitialState = swapStandoffWorld
             TickCount = 4L }
           { Name = "perception-contact"
             Description =
               "One friendly agent at (1,5) ordered east to (9,5); a stationary hostile agent 1 at (9,1) behind an "
               + "opaque impassable wall at x=6, rows 0..3. The hostile is inside PerceptionConfig.SightRange from "
               + "the start but line of sight is blocked; once the friendly clears the wall the Perception phase "
               + "emits ContactObserved and the Tactical-knowledge phase adds the contact to the shared squad "
               + "picture (WorldState.TacticalKnowledge, Canonical.FormatVersion 3). The first corpus entry with an "
               + "enemy deployment (TASK-026, backlog B-015; the 'Unknown threat' shape, docs/05 section 16)."
             InitialStateNote = "Corpus perception-contact scenario (12 x 8, seed 20260904, 1 friendly + 1 hostile)"
             InitialState = perceptionContactWorld
             TickCount = 14L } |]

    // --- entry paths and loading ----------------------------------------

    let private logPathIn (dir: string) (e: Entry) = Path.Combine(dir, e.Name + ".cwlog")
    let private tablePathIn (dir: string) (e: Entry) = Path.Combine(dir, e.Name + ".md")

    /// Parses an entry's committed command-log file.
    let loadLog (dir: string) (e: Entry) : Result<RecordedCommand[], string> =
        let p = logPathIn dir e

        if not (File.Exists p) then
            Error $"command-log file not found: {p}"
        else
            match CommandLogFile.parse "corpus" (File.ReadAllText p) with
            | Ok cmds -> Ok cmds
            | Error err -> Error $"{p}: {CommandLogFile.describeError err}"

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
        line $"| Command log | `{e.Name}.cwlog` |"
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
        match loadLog dir e with
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

    /// Rewrites an entry's `<Name>.md` from a fresh replay outcome.
    let regenerateEntry (dir: string) (e: Entry) : RegenResult =
        match loadLog dir e with
        | Error m -> RegenLogError m
        | Ok cmds ->
            match run e cmds with
            | Error err -> RegenReplayError err
            | Ok outcome ->
                let p = tablePathIn dir e
                File.WriteAllText(p, renderTable e outcome)
                Wrote p
