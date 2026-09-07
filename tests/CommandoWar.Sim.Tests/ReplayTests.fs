module CommandoWar.Sim.Tests.ReplayTests

open System
open System.IO
open Xunit
open FsCheck
open FsCheck.FSharp
open FsCheck.Xunit
open CommandoWar.Sim
open CommandoWar.Headless

let private config = SimConfig.standard
let private bounds: GridBounds = { Width = 8; Height = 8 }
let private world (seed: uint64) = Setup.sixAgentWorld bounds seed
/// A recorded move command. The agent id is taken from `sequence` so a tick's
/// commands address distinct agents (agents 0..5 exist in the test world).
let private move (tick: int64) (sequence: int) (dest: Cell) : RecordedCommand =
    { Tick = tick
      Sequence = sequence
      Command =
        Command.moveTo (CommandId.ofInt (int tick * 100 + sequence)) (tick - 1L) (AgentId.ofInt sequence) dest
      Issuer = "test" }

/// Hand-written direct stepping loop, independent of the replay runner.
let private directRun (initial: WorldState) (records: RecordedCommand[]) (tickCount: int64) =
    let commandsAt tick =
        records
        |> Array.filter (fun r -> r.Tick = tick)
        |> Array.sortBy (fun r -> r.Sequence)
        |> Array.map (fun r -> r.Command)

    let mutable state = initial
    let hashes = ResizeArray<StateHash>()
    let events = ResizeArray<DomainEvent>()

    for tick in 1L .. tickCount do
        let r = Simulation.step config (commandsAt tick) state
        state <- r.State
        hashes.Add r.StateHash
        events.AddRange r.Events

    state, hashes.ToArray(), events.ToArray()

let private replayOf (initial: WorldState) (records: RecordedCommand[]) (tickCount: int64) =
    Replay.record ReplayMeta.unspecified initial tickCount (CommandLog.create records)
    |> Replay.run config

let private okOrFail =
    function
    | Ok v -> v
    | Error e -> failwith $"unexpected replay error: {e}"

let private schedule =
    [| move 1L 0 { X = 3; Y = 0 }
       move 1L 1 { X = 3; Y = 1 }
       move 3L 0 { X = 6; Y = 2 } |]

// --- Determinism of direct runs ---------------------------------------

[<Fact>]
let ``repeated direct runs of the same inputs yield identical per-tick hashes`` () =
    let _, first, _ = directRun (world 1UL) schedule 8L
    let _, second, _ = directRun (world 1UL) schedule 8L
    Assert.Equal<StateHash[]>(first, second)

[<Fact>]
let ``a different seed changes the per-tick hashes`` () =
    let _, withSeed1, _ = directRun (world 1UL) schedule 4L
    let _, withSeed2, _ = directRun (world 2UL) schedule 4L
    Assert.NotEqual<StateHash[]>(withSeed1, withSeed2)

// --- Direct versus replay --------------------------------------------

[<Fact>]
let ``replay reconstructs the same per-tick hashes, final state and events as direct stepping`` () =
    let finalState, directHashes, directEvents = directRun (world 1UL) schedule 8L
    let outcome = replayOf (world 1UL) schedule 8L |> okOrFail

    Assert.Equal<StateHash[]>(directHashes, outcome.TickHashes |> Array.map (fun c -> c.Hash))
    Assert.Equal(Hashing.hash finalState, Hashing.hash outcome.FinalState)
    Assert.Equal(finalState, outcome.FinalState)
    Assert.Equal<DomainEvent[]>(directEvents, outcome.Events)
    Assert.Equal<int64[]>([| 1L .. 8L |], outcome.TickHashes |> Array.map (fun c -> c.Tick))

// --- Divergence -----------------------------------------------------

[<Fact>]
let ``identical command logs report a match`` () =
    match Divergence.diagnose config (world 1UL) schedule schedule 8L |> okOrFail with
    | Match n -> Assert.Equal(8, n)
    | other -> Assert.Fail($"expected Match, got {other}")

[<Fact>]
let ``a mutated command destination is reported as a divergence at the changed tick`` () =
    let mutated =
        schedule
        |> Array.map (fun r ->
            if r.Tick = 1L && r.Sequence = 0 then
                { r with Command = Command.moveTo (CommandId.ofInt 100) 0L (AgentId.ofInt 0) { X = 3; Y = 5 } }
            else
                r)

    let reference = replayOf (world 1UL) schedule 8L |> okOrFail
    let candidate = replayOf (world 1UL) mutated 8L |> okOrFail

    match Divergence.compare reference candidate with
    | Diverged(point, e, a) ->
        Assert.Equal(1L, point.Tick)
        Assert.Equal(8, e)
        Assert.Equal(8, a)
        Assert.NotEqual(point.Expected, point.Actual)
        Assert.Equal(reference.TickHashes.[0].Hash, point.Expected)
        Assert.Equal(candidate.TickHashes.[0].Hash, point.Actual)
        Assert.Equal(Some "Agent[0]", point.Section)
        Assert.Equal(point.ExpectedRandomDraws, point.ActualRandomDraws)
    | other -> Assert.Fail($"expected Diverged at tick 1, got {other}")

[<Fact>]
let ``an added command diverges at that command's tick and not before`` () =
    let reference = [| move 1L 0 { X = 3; Y = 0 } |]
    let candidate = Array.append reference [| move 3L 0 { X = 0; Y = 5 } |]

    match Divergence.diagnose config (world 1UL) reference candidate 6L |> okOrFail with
    | Diverged(point, _, _) ->
        Assert.Equal(3L, point.Tick)
        Assert.NotEqual(point.Expected, point.Actual)
    | other -> Assert.Fail($"expected Diverged at tick 3, got {other}")

[<Fact>]
let ``a shorter candidate run is reported, not silently accepted`` () =
    let reference = replayOf (world 1UL) schedule 8L |> okOrFail
    let candidate = replayOf (world 1UL) schedule 5L |> okOrFail

    match Divergence.compare reference candidate with
    | TruncatedRun(lastAgreed, e, a) ->
        Assert.Equal(5L, lastAgreed)
        Assert.Equal(8, e)
        Assert.Equal(5, a)
    | other -> Assert.Fail($"expected TruncatedRun, got {other}")

// --- Version and format rejection -----------------------------------

[<Fact>]
let ``replay rejects an unsupported replay format version`` () =
    let record =
        { Replay.record ReplayMeta.unspecified (world 1UL) 4L (CommandLog.create schedule) with Version = 999 }

    match Replay.run config record with
    | Error(UnsupportedReplayVersion(found, supported)) ->
        Assert.Equal(999, found)
        Assert.Equal(Replay.FormatVersion, supported)
    | other -> Assert.Fail($"expected UnsupportedReplayVersion, got {other}")

[<Fact>]
let ``replay rejects an unsupported command log version`` () =
    let record = Replay.record ReplayMeta.unspecified (world 1UL) 4L { Version = 999; Commands = [||] }

    match Replay.run config record with
    | Error(UnsupportedCommandLogVersion(found, supported)) ->
        Assert.Equal(999, found)
        Assert.Equal(CommandLog.Version, supported)
    | other -> Assert.Fail($"expected UnsupportedCommandLogVersion, got {other}")

[<Fact>]
let ``replay rejects an unsupported canonical format version`` () =
    let record =
        { Replay.record ReplayMeta.unspecified (world 1UL) 4L (CommandLog.create schedule) with CanonicalFormat = 42 }

    match Replay.run config record with
    | Error(UnsupportedCanonicalFormat(found, supported)) ->
        Assert.Equal(42, found)
        Assert.Equal(Canonical.FormatVersion, supported)
    | other -> Assert.Fail($"expected UnsupportedCanonicalFormat, got {other}")

[<Fact>]
let ``replay rejects an initial state that is not at tick zero`` () =
    let stepped = (Simulation.step config [||] (world 1UL)).State
    let record = Replay.record ReplayMeta.unspecified stepped 4L (CommandLog.create [||])

    match Replay.run config record with
    | Error(InitialStateNotAtTickZero tick) -> Assert.Equal(1L, tick)
    | other -> Assert.Fail($"expected InitialStateNotAtTickZero, got {other}")

[<Fact>]
let ``replay rejects a seed that disagrees with the initial random stream`` () =
    let record =
        { Replay.record ReplayMeta.unspecified (world 1UL) 4L (CommandLog.create schedule) with Seed = 999UL }

    match Replay.run config record with
    | Error(SeedInconsistentWithInitialState(seed, _)) -> Assert.Equal(999UL, seed)
    | other -> Assert.Fail($"expected SeedInconsistentWithInitialState, got {other}")

[<Fact>]
let ``replay rejects a non-monotonic command log`` () =
    let duplicated =
        { Version = CommandLog.Version
          Commands = [| move 2L 0 { X = 1; Y = 0 }; move 2L 0 { X = 2; Y = 0 } |] }

    let record = Replay.record ReplayMeta.unspecified (world 1UL) 4L duplicated

    match Replay.run config record with
    | Error(NonMonotonicCommandLog(index, _, _)) -> Assert.Equal(1, index)
    | other -> Assert.Fail($"expected NonMonotonicCommandLog, got {other}")

[<Fact>]
let ``replay rejects a command scheduled outside the run`` () =
    let record =
        Replay.record ReplayMeta.unspecified (world 1UL) 3L (CommandLog.create [| move 9L 0 { X = 1; Y = 0 } |])

    match Replay.run config record with
    | Error(CommandOutsideReplayRange(_, tick, tickCount)) ->
        Assert.Equal(9L, tick)
        Assert.Equal(3L, tickCount)
    | other -> Assert.Fail($"expected CommandOutsideReplayRange, got {other}")

[<Fact>]
let ``replay rejects a command log that reuses a command id across ticks`` () =
    // The delivery tick and the envelope issue tick are independent (TASK-024):
    // each command is issued the tick before it is delivered, and the two
    // records still share CommandId 42, which is the malformed part.
    let reused (tick: int64) (agentIdx: int) (dest: Cell) : RecordedCommand =
        { Tick = tick
          Sequence = 0
          Command = Command.moveTo (CommandId.ofInt 42) (tick - 1L) (AgentId.ofInt agentIdx) dest
          Issuer = "test" }

    let log =
        CommandLog.create [| reused 1L 0 { X = 3; Y = 0 }; reused 3L 1 { X = 3; Y = 1 } |]

    let record = Replay.record ReplayMeta.unspecified (world 1UL) 4L log

    match Replay.run config record with
    | Error(DuplicateCommandIdInLog(id, first, second)) ->
        Assert.Equal(CommandId.ofInt 42, id)
        Assert.True(first < second)
    | other -> Assert.Fail($"expected DuplicateCommandIdInLog, got {other}")

// --- Production replay-command serialisation (TASK-025, backlog B-045) ------
// ReplaySerialisation: a versioned line-based text format for the accepted
// command log plus a small header. The legacy .cwlog cannot carry a
// multi-recipient envelope, Urgency / RiskTolerance, or an IssuedAtTick
// distinct from the delivery tick (TASK-024); this format can.

module RS = ReplaySerialisation

let private urgencyGen = Gen.elements [ Routine; Immediate ]
let private riskGen = Gen.elements [ Cautious; Standard; Aggressive ]
let private issuerGen = Gen.elements [ "fixture:spike"; "corpus"; "test"; "cwheadless"; "replay-runner" ]
let private metaTextGen = Gen.elements [ "cwheadless"; "spike build 7"; "corpus-gen"; "b" ]

/// A full-width pseudo-random uint64 from two 32-bit halves.
let private u64Gen: Gen<uint64> =
    Gen.map2
        (fun (a: int) (b: int) -> (uint64 (uint32 a) <<< 32) ||| uint64 (uint32 b))
        (Gen.choose (Int32.MinValue, Int32.MaxValue))
        (Gen.choose (Int32.MinValue, Int32.MaxValue))

let private recipientsGen: Gen<AgentId list> =
    gen {
        let! k = Gen.choose (1, 5)
        let! shuffled = Gen.shuffle [| 0..29 |]
        return shuffled |> Array.take k |> Array.toList |> List.map AgentId.ofInt
    }

/// One command's envelope core, before (Tick, Sequence) are assigned.
type private CmdCore =
    { DeliveryTick: int64
      IssuedAtTick: int64
      Id: int
      Urgency: Urgency
      Risk: RiskTolerance
      Recipients: AgentId list
      Target: Cell
      Issuer: string }

let private cmdCoreGen (tickCount: int64) : Gen<CmdCore> =
    gen {
        let! deliveryTick = Gen.choose (1, int tickCount)
        // Bias toward an issue tick strictly before delivery, but include the
        // "issued on the delivery tick" degenerate case.
        let! offset = Gen.frequency [ 1, Gen.constant 0; 4, Gen.choose (1, deliveryTick) ]
        let! id = Gen.choose (0, 100000)
        let! urgency = urgencyGen
        let! risk = riskGen
        let! recipients = recipientsGen
        let! x = Gen.choose (-5, 40)
        let! y = Gen.choose (-5, 40)
        let! issuer = issuerGen

        return
            { DeliveryTick = int64 deliveryTick
              IssuedAtTick = int64 (deliveryTick - offset)
              Id = id
              Urgency = urgency
              Risk = risk
              Recipients = recipients
              Target = { X = x; Y = y }
              Issuer = issuer }
    }

/// A canonically ordered `ReplayCommandFile`: commands ascending by
/// (Tick, Sequence), checkpoints ascending by tick, exactly what `serialise`
/// emits, so `parse (serialise f) = Ok f` is a real round-trip.
let private replayFileGen: Gen<RS.ReplayCommandFile> =
    gen {
        let! seed = u64Gen
        let! tickCount = Gen.choose (1, 40) |> Gen.map int64
        let! build = metaTextGen
        let! scenario = metaTextGen
        let! initialHash = Gen.optionOf u64Gen

        let! nCheckpoints = Gen.choose (0, 10)
        let! cpTicks = Gen.listOfLength nCheckpoints (Gen.choose (1, int tickCount + 3))
        let! cpHashes = Gen.listOfLength nCheckpoints u64Gen

        let checkpoints =
            List.zip cpTicks cpHashes
            |> List.distinctBy fst
            |> List.sortBy fst
            |> List.map (fun (t, h) ->
                { Tick = int64 t
                  Hash = { Format = Canonical.FormatVersion; Value = h } })
            |> List.toArray

        let! nCmds = Gen.choose (0, 12)
        let! cores = Gen.listOfLength nCmds (cmdCoreGen tickCount)

        let commands =
            cores
            |> List.sortBy (fun c -> c.DeliveryTick)
            |> List.groupBy (fun c -> c.DeliveryTick)
            |> List.collect (fun (tick, group) ->
                group
                |> List.mapi (fun seq c ->
                    { Tick = tick
                      Sequence = seq
                      Command =
                        { Id = CommandId.ofInt c.Id
                          IssuedAtTick = c.IssuedAtTick
                          Recipients = c.Recipients
                          Urgency = c.Urgency
                          RiskTolerance = c.Risk
                          Intent = MoveTo c.Target }
                      Issuer = c.Issuer }))
            |> List.toArray

        return
            { Version = RS.FormatVersion
              Seed = seed
              TickCount = tickCount
              CanonicalFormat = Canonical.FormatVersion
              Meta = { Build = build; Scenario = scenario }
              InitialHash = initialHash
              Checkpoints = checkpoints
              Commands = commands }
    }

[<Property(MaxTest = 200)>]
let ``ReplaySerialisation round-trips every envelope field over generated command logs`` () =
    Prop.forAll (Arb.fromGen replayFileGen) (fun file ->
        let text = RS.serialise file

        match RS.parse text with
        | Error e -> failwith $"parse of serialised output failed: {RS.describeError e}"
        | Ok parsed ->
            // serialise >> parse is identity ...
            if parsed <> file then
                failwith "serialise >> parse changed the value"
            // ... and parse >> serialise is identity on that valid input.
            RS.serialise parsed = text)

[<Fact>]
let ``ReplaySerialisation round-trips the full field matrix: multi-recipient, every urgency and risk, a distinct issue tick`` () =
    // The generated property above covers this space open-endedly; this fact
    // pins every point in the matrix the acceptance criteria name explicitly.
    let recipientSets =
        [ [ 0 ]; [ 2; 5 ]; [ 3; 4; 5 ]; [ 9; 1; 4; 7 ] ] |> List.map (List.map AgentId.ofInt)

    let cmd tick issuedAt seq id urgency risk recipients : RecordedCommand =
        { Tick = tick
          Sequence = seq
          Command =
            { Id = CommandId.ofInt id
              IssuedAtTick = issuedAt
              Recipients = recipients
              Urgency = urgency
              RiskTolerance = risk
              Intent = MoveTo { X = 7; Y = -2 } }
          Issuer = "matrix" }

    let commands =
        [ for u in [ Routine; Immediate ] do
              for r in [ Cautious; Standard; Aggressive ] do
                  for recipients in recipientSets -> u, r, recipients ]
        |> List.mapi (fun i (u, r, recipients) ->
            let tick = int64 (i + 1)
            // half the commands issued strictly before delivery, half on it
            let issuedAt = if i % 2 = 0 then tick - 1L else tick
            cmd tick (max 0L issuedAt) 0 (100 + i) u r recipients)
        |> List.toArray

    let file: RS.ReplayCommandFile =
        { Version = RS.FormatVersion
          Seed = 12345678UL
          TickCount = int64 commands.Length + 1L
          CanonicalFormat = Canonical.FormatVersion
          Meta = { Build = "matrix build"; Scenario = "matrix scenario" }
          InitialHash = Some 0xABCDEF0123456789UL
          Checkpoints = [| { Tick = 1L; Hash = { Format = Canonical.FormatVersion; Value = 42UL } } |]
          Commands = commands }

    let text = RS.serialise file

    match RS.parse text with
    | Error e -> Assert.Fail(RS.describeError e)
    | Ok parsed ->
        Assert.Equal(file, parsed)
        Assert.Equal(text, RS.serialise parsed)

    // the matrix really does contain every point
    Assert.Contains(commands, (fun c -> List.length c.Command.Recipients > 1))
    Assert.Equal<Set<Urgency>>(
        Set.ofList [ Routine; Immediate ],
        commands |> Array.map (fun c -> c.Command.Urgency) |> Set.ofArray
    )
    Assert.Equal<Set<RiskTolerance>>(
        Set.ofList [ Cautious; Standard; Aggressive ],
        commands |> Array.map (fun c -> c.Command.RiskTolerance) |> Set.ofArray
    )
    Assert.Contains(commands, (fun c -> c.Command.IssuedAtTick <> c.Tick))

[<Fact>]
let ``ReplaySerialisation.parse rejects an unknown format version and attempts no migration`` () =
    let bumped = "version 999\nseed 1\nticks 4\ncanonical 3\nbuild b\nscenario s\n"

    match RS.parse bumped with
    | Error(RS.UnsupportedFormatVersion(_, found, supported)) ->
        Assert.Equal("999", found)
        Assert.Equal(RS.FormatVersion, supported)
    | other -> Assert.Fail($"expected UnsupportedFormatVersion, got {other}")

[<Fact>]
let ``ReplaySerialisation.parse rejects a canonical-format mismatch`` () =
    let text = "version 1\nseed 1\nticks 4\ncanonical 99\nbuild b\nscenario s\n"

    match RS.parse text with
    | Error(RS.CanonicalFormatMismatch(_, found, expected)) ->
        Assert.Equal(99, found)
        Assert.Equal(Canonical.FormatVersion, expected)
    | other -> Assert.Fail($"expected CanonicalFormatMismatch, got {other}")

[<Fact>]
let ``ReplaySerialisation.parse rejects a command log that is not in (tick, sequence) order`` () =
    let text =
        String.concat
            "\n"
            [ "version 1"
              "seed 1"
              "ticks 8"
              "canonical 3"
              "build b"
              "scenario s"
              "command 5 0 1 5 routine standard test 0 move 1 1"
              "command 3 0 2 3 routine standard test 0 move 2 2"
              "" ]

    match RS.parse text with
    | Error(RS.CommandsOutOfOrder _) -> ()
    | other -> Assert.Fail($"expected CommandsOutOfOrder, got {other}")

[<Fact>]
let ``ReplaySerialisation parses a hand-written full envelope the legacy cwlog cannot express`` () =
    let text =
        String.concat
            "\n"
            [ "# a full envelope"
              "version 1"
              "seed 20260902"
              "ticks 12"
              "canonical 3"
              "build cwheadless"
              "scenario spike-fixture"
              "initial-hash 0x50BFA007EDFC42FE"
              "command 2 0 1 1 immediate aggressive fixture:spike 3,4,5 move 20 14"
              "" ]

    match RS.parse text with
    | Error e -> Assert.Fail(RS.describeError e)
    | Ok file ->
        Assert.Equal(20260902UL, file.Seed)
        Assert.Equal(12L, file.TickCount)
        Assert.Equal(Some 0x50BFA007EDFC42FEUL, file.InitialHash)
        let c = Assert.Single file.Commands
        Assert.Equal(2L, c.Tick)
        Assert.Equal(1L, c.Command.IssuedAtTick)
        Assert.Equal<AgentId list>([ 3; 4; 5 ] |> List.map AgentId.ofInt, c.Command.Recipients)
        Assert.Equal(Immediate, c.Command.Urgency)
        Assert.Equal(Aggressive, c.Command.RiskTolerance)
        Assert.Equal(MoveTo { X = 20; Y = 14 }, c.Command.Intent)
        Assert.Equal("fixture:spike", c.Issuer)
        // parse >> serialise is identity on this canonical input (the comment
        // and the trailing blank line are the only difference).
        let stripped =
            text.Split('\n')
            |> Array.filter (fun l -> l <> "" && not (l.StartsWith "#"))
            |> String.concat "\n"

        Assert.Equal(stripped + "\n", RS.serialise file)

// --- the committed new-format fixture --------------------------------------

let private replaysDir = Path.Combine(AppContext.BaseDirectory, "replays")

/// The tick-1..24 hashes committed in both envelope-full.cwreplay (as
/// `checkpoint` lines) and envelope-full.md (as the per-tick table). Pinned
/// here a third way so a determinism regression fails this fact directly.
let private envelopeFullHashes =
    [| 0x7F61018E60700D55UL; 0xE5224F773E774224UL; 0xA5BD2FE4613A885AUL; 0x5F55F90A0B5331C8UL
       0xAF4CC50EF3E2E382UL; 0x3CCD22AD382F041CUL; 0x7ADA46F638E12FF2UL; 0x93A0402A1EAF94B8UL
       0x1D4D494F97500E2AUL; 0x9688354C5999AB54UL; 0x5A1D6613ED23300AUL; 0x409EF2EC77CB3CD8UL
       0x37161F6C64AFA472UL; 0x3DBA1BC5D1C9737CUL; 0xF11EFDE85DC60752UL; 0x10B544C849B13748UL
       0xBC74FEBD78A4170AUL; 0xA58A1A075B699A44UL; 0xBD3C728769ADC77AUL; 0x4B745CF8769DE6A8UL
       0x135AECE475C9EEC2UL; 0x652559E69CAB70A8UL; 0x07ADF960581EC576UL; 0x4E5963A2C8C83660UL |]

[<Fact>]
let ``the committed envelope-full replay parses, replays, and matches its file checkpoints, its md table, and a fresh run`` () =
    let file =
        match RS.parse (File.ReadAllText(Path.Combine(replaysDir, "envelope-full.cwreplay"))) with
        | Ok f -> f
        | Error e -> failwith $"envelope-full.cwreplay did not parse: {RS.describeError e}"

    // The envelope carries what .cwlog cannot.
    let c = Assert.Single file.Commands
    Assert.True(List.length c.Command.Recipients > 1, "expected a multi-recipient command")
    Assert.Equal(Immediate, c.Command.Urgency)
    Assert.Equal(Aggressive, c.Command.RiskTolerance)
    Assert.NotEqual(c.Tick, c.Command.IssuedAtTick)

    // Replay it from the scenario the file names.
    let initial = Setup.sixAgentWorld { Width = 32; Height = 32 } 20260902UL
    Assert.Equal(Some (Hashing.hash initial).Value, file.InitialHash)

    let outcome = RS.toReplayRecord initial file |> Replay.run config |> okOrFail
    let runHashes = outcome.TickHashes |> Array.map (fun cp -> cp.Hash.Value)

    // 1. fresh run == the pinned array.
    Assert.Equal<uint64[]>(envelopeFullHashes, runHashes)

    // 2. fresh run == the file's own checkpoint lines.
    let fileHashes = file.Checkpoints |> Array.map (fun cp -> cp.Hash.Value)
    Assert.Equal<uint64[]>(runHashes, fileHashes)

    // 3. fresh run == the committed envelope-full.md table.
    match Corpus.parseTable (File.ReadAllText(Path.Combine(replaysDir, "envelope-full.md"))) with
    | Error m -> Assert.Fail($"envelope-full.md: {m}")
    | Ok table ->
        Assert.Equal(24L, table.TickCount)
        Assert.Equal((Hashing.hash initial).Value, table.InitialHash)
        Assert.Equal(runHashes.[runHashes.Length - 1], table.FinalHash)
        Assert.Equal(outcome.Events.Length, table.EventCount)

        Assert.Equal<(int64 * uint64)[]>(
            outcome.TickHashes |> Array.map (fun cp -> cp.Tick, cp.Hash.Value),
            table.TickHashes
        )
