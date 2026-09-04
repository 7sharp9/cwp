# CommandoWar.Sim benchmark baseline

Recorded output of the headless performance and allocation harness
(`bench/CommandoWar.Benchmarks/`, TASK-014, backlog B-013), covering the
`docs/09_TEST_STRATEGY.md` section 2.8 list for the authoritative systems that
exist today.

**This is a recorded baseline, not a test oracle.** Timings are
machine-dependent and no test asserts on them. See `README.md` in this
directory for the acceptance rule and the regeneration command.

## Run environment

| Field | Value |
|---|---|
| Harness | BenchmarkDotNet v0.15.8 (`[<MemoryDiagnoser>]`; `IterationCount=10`, `WarmupCount=3`, `LaunchCount=1`) |
| .NET SDK | 10.0.303 |
| Runtime | .NET 10.0.11, X64 RyuJIT x86-64-v4 (workload job optimised) |
| Build configuration | Release |
| Machine | AMD Ryzen 7 9700X 3.80 GHz, 8 physical / 16 logical cores; Windows 11 Pro 10.0.26200 (25H2) |
| Commit | `7bb3b70` + uncommitted TASK-013 / TASK-014 working tree |
| Recorded | 2026-09-04 |

## Results

`Mean` and `P95` are per operation; `Alloc/op` is managed heap allocation per
operation (`MemoryDiagnoser`). The per-tick budget in `docs/04_SIMULATION_SPEC.md`
section 19 is **5 ms = 5,000,000 ns**; the last column is that budget divided by
the mean for the per-tick rows.

| Benchmark | Op | Mean | P95 | Alloc/op | Headroom vs 5 ms/tick |
|---|---|---:|---:|---:|---:|
| empty fixed tick | one `Simulation.step`, no agents, no commands | 362 ns | 384 ns | 2.25 KB | ~13,800x |
| canonical encode | `Canonical.encode` of the fixture tick-0 state | 320 ns | 330 ns | 1.51 KB | n/a (sub-step) |
| state hash | `Hashing.hash` (encode + FNV-1a-64) of the fixture state | 442 ns | 448 ns | 1.51 KB | n/a (sub-step) |
| 6-agent movement tick | one `Simulation.step`, 6 fresh `MoveTo` + placeholder movement | 1.33 us | 1.34 us | 8.19 KB | ~3,760x |
| 50-agent movement tick | one `Simulation.step`, 50 fresh `MoveTo` + placeholder movement | 12.82 us | 13.14 us | 86.57 KB | ~390x |
| line-of-sight batch | 1024 `Sight.visible` traces over a 32x32 occluded lattice | 90.65 us | 93.16 us | 842.7 KB | n/a (batch) |
| pathfinding: open | `Pathfinding.find` across an open 40x40 grid | 10.33 us | 10.37 us | 19.75 KB | ~484x |
| pathfinding: blocked | `Pathfinding.find` through 4 serpentine walls, 40x40 | 139.29 us | 140.68 us | 23.83 KB | ~36x |
| pathfinding: choke | `Pathfinding.find` through a 1-cell gap in a full wall, 40x40 | 75.32 us | 75.83 us | 21.07 KB | ~66x |
| replay run | `Replay.run` of the shared fixture (40 ticks from tick 0) | 29.46 us | 29.81 us | 148.09 KB | n/a (40 ticks) |

Every per-tick row is at least ~36x inside the 5 ms budget, including a
full-grid A* query run once per tick. No allocation is proportional to total
map size on the empty or movement tick; the pathfinding and line-of-sight
allocations are per query / per batch, not per tick, and no phase runs them
yet.

### Observations

- **50-agent movement allocates ~86 KB/tick.** `Simulation.commandIntake`
  copies the agent array once per accepted command (`Array.copy s.Agents`
  inside the loop), so a 50-command tick does 50 copies of a 50-element array:
  O(n^2). Harmless at 50 agents and well inside budget, but it is the first
  measured hot spot and B-011 (the real movement phase) should take the whole
  batch in one copy.
- **Line-of-sight batch allocates ~0.82 MB for 1024 traces** (~0.8 KB/trace):
  `Sight.trace` builds a `ResizeArray` path and a `LineOfSight` record per
  call. Acceptable for point-to-point queries; a Perception phase (B-015)
  tracing every agent pair every tick would want a reused buffer or a
  `visible`-only fast path that skips the `Path` allocation.
- **`[Host]` reports DEBUG in the BenchmarkDotNet banner** but the workload
  job (`Job-*`) is the optimised Release build; the sub-1% standard deviations
  confirm optimised code. Cosmetic F#/SDK labelling only.

## Pathfinding expansion-budget evidence (for B-011)

`cwbench expansions` reports the size of the closed set A* actually reaches on
each pathfinding benchmark query, `find (0,20) -> (39,20)` on a 40x40 grid:

| Map | Closed set reached | Fraction of grid |
|---|---:|---:|
| open | 39 | 2.4% |
| blocked (serpentine) | 1168 | 73% |
| choke (1-cell gap, 19 rows off-line) | 780 | 49% |
| `Pathfinding.find` default cap (`Width * Height`) | 1600 | 100% |

**Recommendation for B-011 (do not change `Pathfinding.find` this task):** a
single legitimate query on an adversarial small map already closes ~73% of the
grid, so the `Width * Height` default cap is correctly sized as a safety
ceiling and must not be cut close to a typical closed set. When B-011 adds a
*per-tick* pathfinding budget (many agents, dynamic replanning), size it from a
multiple of the *expected* closed set (open-map ~O(path length), a few hundred
on a blocked map) with `Width * Height` retained as the hard per-query ceiling,
and re-run this harness on the real greybox map (B-025) before fixing a number.

## Not yet covered

`docs/09` section 2.8 also lists 50-agent perception, appraisal batch, and a
full synthetic 50-agent tick. Perception, appraisal, and combat do not exist
yet: those rows are added when the systems land (backlog B-015 / B-017 /
B-019), against a commented slot already present in
`bench/CommandoWar.Benchmarks/Benchmarks.fs`.
