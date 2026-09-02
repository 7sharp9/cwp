namespace CommandoWar.Sim.Tests

open System.Diagnostics
open Xunit
open Xunit.Abstractions
open CommandoWar.Sim

/// Indicative stepping-throughput baseline for empty and six-agent worlds
/// (docs/09_TEST_STRATEGY.md section 2.8, docs/04_SIMULATION_SPEC.md section 19).
///
/// This is a rough in-process micro-measurement, not a BenchmarkDotNet
/// harness: it exists to record an order-of-magnitude baseline against the
/// 5 ms per-tick budget and to flag a gross regression. The tests assert only
/// on correctness (tick progression); the timing is written to test output and
/// copied into the progress ledger from a local Release run.
type BenchmarkTests(output: ITestOutputHelper) =

    let bounds: GridBounds = { Width = 32; Height = 32 }
    let config = SimConfig.standard

    /// Steps `initial` for `ticks` ticks, refreshing a long move order on any
    /// agent that has stopped so the movement phase keeps doing work.
    let run (label: string) (initial: WorldState) (ticks: int) =
        // Warm up the JIT and canonical/hash paths.
        let mutable warm = initial
        for _ in 1..1000 do
            warm <- (Simulation.step config [||] warm).State

        let sw = Stopwatch.StartNew()
        let mutable state = initial
        for t in 1..ticks do
            let commands =
                state.Agents
                |> Array.filter (fun a -> a.Destination.IsNone)
                |> Array.mapi (fun i a ->
                    let target = { X = (t + i) % bounds.Width; Y = (t * 2 + i) % bounds.Height }
                    Command.moveTo (CommandId.ofInt (t * 100 + i)) (int64 t - 1L) a.Id target)

            state <- (Simulation.step config commands state).State
        sw.Stop()

        let nsPerTick = sw.Elapsed.TotalMilliseconds * 1_000_000.0 / float ticks
        output.WriteLine(
            $"%s{label}: %d{ticks} ticks in %.1f{sw.Elapsed.TotalMilliseconds} ms "
            + $"(~%.0f{nsPerTick} ns/tick, budget 5_000_000 ns/tick)"
        )
        state

    [<Fact>]
    member _.``baseline: empty-world stepping``() =
        let empty =
            match World.create bounds 1UL [] with
            | Ok w -> w
            | Error e -> failwith $"{e}"

        let final = run "empty world" empty 50_000
        Assert.Equal(50_000L, final.Tick)

    [<Fact>]
    member _.``baseline: six-agent-world stepping``() =
        let final = run "six agents" (Setup.sixAgentWorld bounds 1UL) 50_000
        Assert.Equal(50_000L, final.Tick)
