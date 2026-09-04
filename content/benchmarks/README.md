# Benchmark baseline

`BASELINE.md` is the recorded output of the headless performance and allocation
harness `bench/CommandoWar.Benchmarks/` (TASK-014, backlog B-013). It covers the
`docs/09_TEST_STRATEGY.md` section 2.8 list for the authoritative systems that
exist today: empty fixed tick, six- and ~50-agent placeholder movement, a
line-of-sight batch, pathfinding through open / blocked / choke maps, canonical
encode, state hash, and a replay run.

## Not a test oracle

Timings and allocations are **machine-dependent**. Nothing in
`tests/CommandoWar.Sim.Tests/` asserts on the numbers here, and this file is
**not** a byte-pinned golden. The cheap in-suite regression flag stays
`tests/CommandoWar.Sim.Tests/BenchmarkTests.fs`, which asserts only on tick
progression and writes rough timing to test output.

### Acceptance rule

A reviewer reads `BASELINE.md` for **order-of-magnitude sanity** against the
`docs/04_SIMULATION_SPEC.md` section 19 budget: **no authoritative tick should
routinely exceed 5 ms (5,000,000 ns) on the development machine, and there
should be no unbounded per-tick allocation growth.** Every per-tick row must
stay comfortably inside that budget (the current baseline is >=36x inside it,
worst case). A regression that pushes a per-tick row toward the budget, or that
introduces allocation proportional to map size on the empty or movement tick,
is a real finding; a 20% drift between machines is not.

## Regenerating `BASELINE.md`

From the repository root, after `dotnet build CommandoWar.slnx -c Release`:

```sh
# Full run (~2 min on the reference machine). Artifacts land in
# ./BenchmarkDotNet.Artifacts/ (git-ignored).
dotnet run --project bench/CommandoWar.Benchmarks -c Release -- --filter '*'

# A subset, e.g. for CI or a single system:
dotnet run --project bench/CommandoWar.Benchmarks -c Release -- --filter '*PathfindingBenchmarks*'

# Pathfinding closed-set (expansion-budget) evidence, not a timed run:
dotnet run --project bench/CommandoWar.Benchmarks -c Release -- expansions
```

Then transcribe the per-class GitHub-markdown reports from
`./BenchmarkDotNet.Artifacts/results/*-report-github.md` into the single table
in `BASELINE.md`, refresh the run-environment block (BenchmarkDotNet version,
SDK, runtime, machine, commit, date), and re-run `cwbench expansions` for the
closed-set table. Record the regeneration in `docs/12_PROGRESS_LEDGER.md` if
any pinned value moved.

## Relationship to the rest of the build

- The benchmark project is in `CommandoWar.slnx` under `/bench/` and builds in
  Release with the solution, but it has no test SDK and no `[<Fact>]`, so
  `dotnet test CommandoWar.slnx` never collects it: the determinism suite stays
  at `Passed: 145`.
- ADR-0002: the dependency arrow is Benchmarks -> Headless -> Sim and
  Benchmarks -> Sim. `CommandoWar.Sim` has no reference to the benchmark
  project or to BenchmarkDotNet.
