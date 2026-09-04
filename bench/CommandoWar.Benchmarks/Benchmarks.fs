namespace CommandoWar.Benchmarks

open BenchmarkDotNet.Attributes
open CommandoWar.Sim
open CommandoWar.Headless

/// The `docs/09_TEST_STRATEGY.md` section 2.8 performance and allocation
/// benchmarks, for the authoritative systems that exist today (TASK-014,
/// backlog B-013).
///
/// Each class is `[<MemoryDiagnoser>]` so BenchmarkDotNet reports allocation
/// per operation alongside the timings. Every benchmark runs the real
/// deterministic simulation over a fixed `SyntheticWorlds` vector: no renderer
/// work, no diagnostic-frame construction, no `System.Random`, no wall-clock
/// read inside the sim (docs/09 section 2.8 last sentence: do not mix debug
/// overlays or editor overhead into a core result).
///
/// The fixed vectors are built once in each class's field initialisers - they
/// are pure functions of integer constants and cost microseconds, so there is
/// no `[<GlobalSetup>]`. BenchmarkDotNet constructs the class once per run and
/// the initialisers are not part of any measured region.
///
/// Perception, appraisal, and combat do not exist yet. The commented slots
/// below mark where their rows attach; they are added by backlog B-015 /
/// B-017 / B-019, not stubbed here.

/// Empty fixed tick and canonical-image cost.
[<MemoryDiagnoser>]
type CoreTickBenchmarks() =

    let config = SimConfig.standard
    let emptyWorld = SyntheticWorlds.emptyWorld ()
    let fixtureWorld = Fixture.initialState ()

    /// The per-tick cost of the phase loop, event and snapshot construction,
    /// and the post-Output canonical hash with no command or movement work.
    [<Benchmark(Description = "empty fixed tick")>]
    member _.EmptyFixedTick() : StepResult = Simulation.step config [||] emptyWorld

    /// `Canonical.encode` alone: the big-endian fixed-width byte image that is
    /// the single input to state hashing and any future serialisation.
    [<Benchmark(Description = "canonical encode (fixture state)")>]
    member _.CanonicalEncode() : byte[] = Canonical.encode fixtureWorld

    /// `Hashing.hash`: `Canonical.encode` plus the FNV-1a-64 digest.
    [<Benchmark(Description = "state hash (fixture state)")>]
    member _.StateHash() : StateHash = Hashing.hash fixtureWorld

/// Agent movement under the placeholder rule (pending B-011). Each operation
/// issues a fresh full batch of `MoveTo` commands and steps one tick, so
/// Command-intake and Navigation-and-movement both do work.
[<MemoryDiagnoser>]
type MovementBenchmarks() =

    let config = SimConfig.standard
    let sixAgentWorld = SyntheticWorlds.sixAgentWorld ()
    let manyAgentWorld = SyntheticWorlds.manyAgentWorld ()

    [<Benchmark(Description = "6-agent placeholder-movement tick")>]
    member _.SixAgentMovementTick() : StepResult =
        Simulation.step config (SyntheticWorlds.moveOrders sixAgentWorld) sixAgentWorld

    [<Benchmark(Description = "50-agent placeholder-movement tick")>]
    member _.ManyAgentMovementTick() : StepResult =
        Simulation.step config (SyntheticWorlds.moveOrders manyAgentWorld) manyAgentWorld

    // Perception (phase 12.3) is backlog B-015: a "50-agent perception" row
    // and a "full synthetic 50-agent tick" row attach here once Sight-driven
    // observations run inside the phase loop.

/// Line-of-sight batch: `Sight.trace` over a fixed grid of origin/target
/// pairs on a synthetic occluded map.
[<MemoryDiagnoser>]
type SightBenchmarks() =

    let occludedTerrain = SyntheticWorlds.occludedTerrain ()

    /// 1024 traces (every left-edge cell to every right-edge cell). The
    /// visible-count accumulator is returned so the results are not
    /// dead-code-eliminated.
    [<Benchmark(Description = "line-of-sight batch (1024 Sight.trace)")>]
    member _.SightTraceBatch() : int =
        let mutable visible = 0

        for (from, target) in SyntheticWorlds.sightBatch do
            if Sight.visible occludedTerrain from target then
                visible <- visible + 1

        visible

/// Pathfinding through open, blocked, and choke-point maps: `Pathfinding.find`
/// (default `Width * Height` cap) over the three synthetic 40 x 40 terrains.
[<MemoryDiagnoser>]
type PathfindingBenchmarks() =

    let openTerrain = SyntheticWorlds.openTerrain ()
    let blockedTerrain = SyntheticWorlds.blockedTerrain ()
    let chokeTerrain = SyntheticWorlds.chokeTerrain ()
    let start = SyntheticWorlds.pathStart
    let goal = SyntheticWorlds.pathGoal

    [<Benchmark(Description = "pathfinding: open 40x40")>]
    member _.FindOpen() : PathResult = Pathfinding.find openTerrain start goal

    [<Benchmark(Description = "pathfinding: blocked (serpentine walls)")>]
    member _.FindBlocked() : PathResult = Pathfinding.find blockedTerrain start goal

    [<Benchmark(Description = "pathfinding: choke (single-cell gap)")>]
    member _.FindChoke() : PathResult = Pathfinding.find chokeTerrain start goal

/// Canonical encode + state hash + replay run. `Replay.run` over the shared
/// fixture re-steps 40 ticks from tick 0, so this is the heaviest single
/// operation in the harness.
[<MemoryDiagnoser>]
type ReplayBenchmarks() =

    let fixtureRecord = Fixture.replayRecord ()

    [<Benchmark(Description = "replay run (shared fixture, 40 ticks)")>]
    member _.ReplayRun() : Result<ReplayOutcome, ReplayError> =
        Replay.run SimConfig.standard fixtureRecord
