namespace CommandoWar.Sim

/// First-divergence diagnosis for two authoritative runs
/// (docs/04_SIMULATION_SPEC.md section 17, docs/09_TEST_STRATEGY.md section 2.4).
///
/// The comparison is over per-tick canonical hashes. It never hides a
/// mismatch: a differing hash, a differing run length, and a differing final
/// state are all reported.

/// Where and how two runs first differ.
type DivergencePoint =
    { /// The first tick whose authoritative hash differs.
      Tick: int64
      /// The hash from the reference run at that tick.
      Expected: StateHash
      /// The hash from the candidate run at that tick.
      Actual: StateHash
      /// Best-effort label for the first differing canonical section, when
      /// both per-tick states are available.
      Section: string option
      /// Random-stream draw count on each side at the divergent tick. Equal
      /// draw counts with differing hashes point away from the random stream.
      ExpectedRandomDraws: uint64
      ActualRandomDraws: uint64 }

/// The outcome of comparing two runs.
type DivergenceReport =
    /// Every compared tick matched. Carries the number of ticks compared.
    | Match of ticksCompared: int
    /// The runs agreed on every overlapping tick but ran for different
    /// lengths.
    | TruncatedRun of lastAgreedTick: int64 * expectedTicks: int * actualTicks: int
    /// The runs first disagreed at `DivergencePoint`.
    | Diverged of point: DivergencePoint * expectedTicks: int * actualTicks: int

[<RequireQualifiedAccess>]
module Divergence =

    /// Compares two reconstructed runs and reports the first divergence.
    let compare (expected: ReplayOutcome) (actual: ReplayOutcome) : DivergenceReport =
        let expectedHashes = expected.TickHashes
        let actualHashes = actual.TickHashes
        let overlap = min expectedHashes.Length actualHashes.Length

        let firstDiff =
            seq { 0 .. overlap - 1 }
            |> Seq.tryFind (fun i -> expectedHashes.[i].Hash <> actualHashes.[i].Hash)

        match firstDiff with
        | Some i ->
            let section =
                if i < expected.TickStates.Length && i < actual.TickStates.Length then
                    Canonical.firstDifferingSection expected.TickStates.[i] actual.TickStates.[i]
                else
                    None

            let drawsAt (states: WorldState[]) =
                if i < states.Length then states.[i].Random.Draws else 0UL

            Diverged(
                { Tick = expectedHashes.[i].Tick
                  Expected = expectedHashes.[i].Hash
                  Actual = actualHashes.[i].Hash
                  Section = section
                  ExpectedRandomDraws = drawsAt expected.TickStates
                  ActualRandomDraws = drawsAt actual.TickStates },
                expectedHashes.Length,
                actualHashes.Length
            )
        | None ->
            if expectedHashes.Length <> actualHashes.Length then
                let lastAgreedTick =
                    if overlap = 0 then 0L else expectedHashes.[overlap - 1].Tick

                TruncatedRun(lastAgreedTick, expectedHashes.Length, actualHashes.Length)
            else
                Match overlap

    /// Runs a reference and a candidate command log from the same initial
    /// state and reports the first tick at which their authoritative hashes
    /// diverge. A typed `ReplayError` from either run is surfaced, not
    /// swallowed.
    let diagnose
        (config: SimConfig)
        (initial: WorldState)
        (reference: RecordedCommand[])
        (candidate: RecordedCommand[])
        (tickCount: int64)
        : Result<DivergenceReport, ReplayError> =

        let toRecord (commands: RecordedCommand[]) =
            Replay.record ReplayMeta.unspecified initial tickCount (CommandLog.create commands)

        match Replay.run config (toRecord reference), Replay.run config (toRecord candidate) with
        | Ok referenceRun, Ok candidateRun -> Ok(compare referenceRun candidateRun)
        | Error err, _ -> Error err
        | _, Error err -> Error err
