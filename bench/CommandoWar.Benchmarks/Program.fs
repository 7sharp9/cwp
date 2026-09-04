module CommandoWar.Benchmarks.Program

open BenchmarkDotNet.Columns
open BenchmarkDotNet.Configs
open BenchmarkDotNet.Jobs
open BenchmarkDotNet.Running
open CommandoWar.Benchmarks

/// The benchmark job: a single launch, 3 warmup and 10 measured iterations.
/// Short enough to run headless in CI, long enough for a stable median and a
/// meaningful P95. The exact counts are recorded in
/// `content/benchmarks/BASELINE.md` alongside the results.
let private benchConfig () : IConfig =
    let job = Job.Default.WithWarmupCount(3).WithIterationCount(10).WithLaunchCount(1)

    ManualConfig
        .Create(DefaultConfig.Instance)
        .AddJob(job)
        .AddColumn(StatisticColumn.Median)
        .AddColumn(StatisticColumn.P95)

/// The benchmark classes, in report order.
let private benchmarkTypes =
    [| typeof<CoreTickBenchmarks>
       typeof<MovementBenchmarks>
       typeof<SightBenchmarks>
       typeof<PathfindingBenchmarks>
       typeof<ReplayBenchmarks> |]

/// `cwbench expansions`: print the size of the closed set A* actually reaches
/// on each pathfinding benchmark query, plus the `Pathfinding.find` default
/// cap. Evidence for sizing the B-011 expansion budget; recorded in
/// `content/benchmarks/BASELINE.md` and the ledger. Not a timed measurement.
let private printExpansions () =
    let start = SyntheticWorlds.pathStart
    let goal = SyntheticWorlds.pathGoal
    let reached t = SyntheticWorlds.reachedExpansions t start goal

    printfn "pathfinding closed-set size reached, find (%d,%d) -> (%d,%d) on 40x40:" start.X start.Y goal.X goal.Y
    printfn "  open            : %d" (reached (SyntheticWorlds.openTerrain ()))
    printfn "  blocked         : %d" (reached (SyntheticWorlds.blockedTerrain ()))
    printfn "  choke           : %d" (reached (SyntheticWorlds.chokeTerrain ()))
    printfn "  find default cap : %d (Width * Height)" (SyntheticWorlds.pathBounds.Width * SyntheticWorlds.pathBounds.Height)

[<EntryPoint>]
let main argv =
    match Array.toList argv with
    | "expansions" :: _ ->
        printExpansions ()
        0
    | _ ->
        BenchmarkSwitcher.FromTypes(benchmarkTypes).Run(argv, benchConfig ()) |> ignore
        0
