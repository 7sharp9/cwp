module CommandoWar.Sim.Tests.ReplayTests

open Xunit
open CommandoWar.Sim

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
