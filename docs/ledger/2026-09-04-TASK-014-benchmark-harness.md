## 2026-09-04 - TASK-014 - Headless performance and allocation benchmark harness

**Owner:** Dave with coding-agent assistance
**Source revision:** `7bb3b70` (Add deterministic line-of-sight diagnostics);
working tree carries the uncommitted TASK-013 change plus this task
**Environment:** Windows 11 Pro 26200 (25H2); .NET SDK 10.0.303; .NET 10.0.11;
AMD Ryzen 7 9700X (8c/16t); xUnit 2.9.3; BenchmarkDotNet 0.15.8
**Status change:** TASK-014 `proposed -> active -> review`; TASK-013 `review ->
done` (finalised as the precondition of this task)

### Precondition: TASK-013 finalised

TASK-013 was in review with all evidence recorded (working tree, not yet
committed). Finalised with the TASK-009 / TASK-012 pattern, no re-run of its
full verification:

- `tasks/TASK-013-PATHFINDING.md` `Status: review -> done`;
- `docs/11_BACKLOG.md`: the "Current work" TASK-013 row and the "Planned
  simulation work" B-010 row `active -> done`, task-file link kept;
- `docs/ledger/2026-09-04-TASK-013-pathfinding.md` Review block
  `Accepted: pending -> yes (2026-09-04)` with an acceptance note; the
  `docs/12` index row's Accepted cell likewise (`pending -> yes (2026-09-04)`,
  status change `-> done`).
- Check performed: `git status` scope (every changed path is TASK-013 or this
  task) plus one `dotnet test CommandoWar.slnx -c Release` = `Passed: 145`
  (unchanged). Both passed, so finalisation proceeded.

### Central decisions

- **New project `bench/CommandoWar.Benchmarks/`.** A `net10.0` console `Exe`,
  `TreatWarningsAsErrors` true, referencing `CommandoWar.Sim` +
  `CommandoWar.Headless` + BenchmarkDotNet 0.15.8 (pinned) only. New top-level
  `bench/` directory beside `src/` and `tests/`. Added to `CommandoWar.slnx`
  under a new `/bench/` solution folder - the one permitted `.slnx` edit.
- **`AssemblyName` left as the project name** (not `cwbench`). BenchmarkDotNet's
  CsProj toolchain locates the `.fsproj` by matching it to the output assembly
  name when it generates the per-benchmark runner project; a mismatch fails the
  auto-build with "Unable to find <name> ... Most probably the name of output
  exe is different than the name of the .(c/f)sproj".
- **BenchmarkDotNet, not the hand-rolled alternative.** It restores and runs
  cleanly headless in this environment; the full 10-benchmark suite completes
  in ~2 min. `[<MemoryDiagnoser>]` on every class for allocation. Job:
  `WarmupCount=3`, `IterationCount=10`, `LaunchCount=1` - short enough for CI,
  long enough for a stable median (<1% StdDev) and a meaningful P95. Custom
  `Median` and `P95` columns added via a `ManualConfig` over
  `DefaultConfig.Instance`.
- **`tests/CommandoWar.Sim.Tests/BenchmarkTests.fs` kept, not folded in.** It
  asserts only on tick progression and writes rough timing to test output;
  folding it out would drop the green count to 143. Keeping it holds 145 and
  the fast in-suite regression signal. It is unchanged and does not reference
  the new project.
- **Synthetic vectors live in the bench project** (`SyntheticWorlds.fs`): the
  ~50-agent world (built through `World.create`), the three 40x40 pathfinding
  terrains (open / serpentine-blocked / choke), and the 32x32 occluded
  line-of-sight lattice. `CommandoWar.Sim` gains no benchmark-only scaffolding.
- **`Pathfinding.find` default cap unchanged.** `cwbench expansions` records
  the closed-set size actually reached (open 39, blocked 1168, choke 780, out
  of a 1600-cell grid); a sized B-011 recommendation is written into
  `BASELINE.md`, this task does not touch the cap.
- **`docs/09` s8 diagnostic-visualisation rule does not apply.** This task adds
  no authoritative spatial or tactical state - it only measures existing
  modules - so there is no `Overlay` / `GridLayer` / golden render to add. The
  completion report confirms this instead of adding an overlay.

### Changes

- **New `bench/CommandoWar.Benchmarks/CommandoWar.Benchmarks.fsproj`.**
  `net10.0` `Exe`, `TreatWarningsAsErrors`, `InvariantGlobalization`,
  `IsPackable=false`; `PackageReference` BenchmarkDotNet 0.15.8;
  `ProjectReference` to `CommandoWar.Sim` and `CommandoWar.Headless`. Compile
  order `SyntheticWorlds.fs`, `Benchmarks.fs`, `Program.fs`.
- **New `bench/CommandoWar.Benchmarks/SyntheticWorlds.fs`.** Fixed seed
  `20260904`. `emptyWorld` / `sixAgentWorld` / `manyAgentWorld` (50 agents,
  alternating sides, via `World.create`); `moveOrders` (a fresh full `MoveTo`
  batch aimed at the mirrored cell); `openTerrain` / `blockedTerrain`
  (4 serpentine walls, alternating gap edge) / `chokeTerrain` (full wall at
  x=20, 1-cell gap at the bottom edge, 19 rows off the direct line);
  `reachedExpansions` (exponential-then-binary probe of `findWithin` for the
  crossover between `BudgetExhausted` and `Found`); `occludedTerrain` (opaque
  pillar lattice) and `sightBatch` (1024 left-edge -> right-edge pairs).
- **New `bench/CommandoWar.Benchmarks/Benchmarks.fs`.** Five
  `[<MemoryDiagnoser>]` classes: `CoreTickBenchmarks` (empty fixed tick,
  `Canonical.encode`, `Hashing.hash`), `MovementBenchmarks` (6- and 50-agent
  placeholder-movement tick, with a commented B-015 perception slot),
  `SightBenchmarks` (1024-trace batch), `PathfindingBenchmarks` (open /
  blocked / choke `Pathfinding.find`), `ReplayBenchmarks` (`Replay.run` of the
  shared fixture). Every benchmark returns a value so results are not
  dead-code-eliminated; fixed seeds and scenarios; no renderer or
  diagnostic-frame work.
- **New `bench/CommandoWar.Benchmarks/Program.fs`.** `BenchmarkSwitcher` over
  the five types with the `ManualConfig` (job + Median + P95 columns); a
  `cwbench expansions` subcommand that prints the pathfinding closed-set
  evidence and exits without a timed run.
- **`CommandoWar.slnx`.** New `<Folder Name="/bench/">` with the one project
  entry. No other change.
- **`.gitignore`.** Added `bench/**/BenchmarkDotNet.Artifacts/` beside the
  existing repo-wide `BenchmarkDotNet.Artifacts/` (which already matches at any
  depth; the explicit line is for clarity).
- **New `content/benchmarks/BASELINE.md`.** Run-environment block
  (BenchmarkDotNet 0.15.8, SDK 10.0.303, Release, Ryzen 7 9700X, commit,
  2026-09-04), the single results table (Mean / P95 / Alloc-per-op / headroom
  vs 5 ms), observations (the O(n^2) command-intake array copy; the
  ~0.8 KB/trace `Sight.trace` allocation; the cosmetic `[Host] DEBUG`
  banner), the pathfinding expansion-budget evidence table and the B-011
  recommendation, and the "not yet covered" list (B-015 / B-017 / B-019).
- **New `content/benchmarks/README.md`.** The "not a test oracle" statement,
  the acceptance rule (order-of-magnitude sanity vs the section 19 budget), the
  regeneration commands, and the build-relationship notes (in the solution,
  not in `dotnet test`; ADR-0002 arrow).
- **`docs/09_TEST_STRATEGY.md` section 2.8.** "Realised by TASK-014" note:
  what the harness is, covered-now vs deferred rows, and that
  `BenchmarkTests.fs` is kept as the cheap flag so the count stays 145.
- **`docs/04_SIMULATION_SPEC.md` section 19.** "Realised by TASK-014" note: the
  harness exists, the baseline keeps every per-tick row >=36x inside the 5 ms
  budget with no map-size-proportional allocation, so the 5 ms and
  no-unbounded-allocation budgets stand; a tighter per-subsystem number is a
  follow-up once B-015 / B-017 / B-019 and B-025 exist; the
  `Pathfinding.find` cap is left unchanged with the closed-set evidence.
- **`docs/07_VERTICAL_SLICE.md` section 10.** "Realised by TASK-014" note under
  the performance budgets: what the harness covers now and later, and that the
  slice budgets stand with per-subsystem numbers still a follow-up.
- **Control.** `tasks/TASK-014-BENCHMARK-HARNESS.md` (new; status `review`);
  `docs/11_BACKLOG.md` (TASK-014 "Current work" row `active`, B-013 `-> active`
  with the task-file link; TASK-013 / B-010 `-> done`);
  `PROJECT_STATE.yaml` (`active_work -> TASK-014`, gates / gate / phase /
  framework_decision unchanged); `docs/12_PROGRESS_LEDGER.md` (index rows for
  TASK-014 and the TASK-013 acceptance; "Pinned facts" gains a BenchmarkDotNet
  0.15.8 row, Green tests "Confirmed by" -> TASK-014, still 145);
  `docs/ledger/2026-09-04-TASK-013-pathfinding.md` (Review block accepted); this
  entry.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (all four projects,
    benchmark project included).
- Command: `dotnet test CommandoWar.slnx -c Release` (before, after the
  TASK-013 finalisation edits)
  - Result: `Passed! - Failed: 0, Passed: 145, Skipped: 0, Total: 145`.
- Command: `dotnet test CommandoWar.slnx -c Release` (after the full TASK-014
  change)
  - Result: `Passed! - Failed: 0, Passed: 145, Skipped: 0, Total: 145` -
    unchanged; the benchmark project contributes no tests.
- Command: `dotnet run --project bench/CommandoWar.Benchmarks -c Release --
  --filter '*'`
  - Result: 10 benchmarks executed in `00:02:07`, no failures. Per-class
    GitHub-markdown reports written to
    `./BenchmarkDotNet.Artifacts/results/*-report-github.md` (git-ignored) and
    transcribed into `content/benchmarks/BASELINE.md`. Key numbers
    (Ryzen 7 9700X, Release): empty tick 318 ns / 2.25 KB; state hash 385 ns;
    6-agent movement tick 1.34 us / 8.19 KB; 50-agent movement tick 12.65 us /
    86.57 KB; line-of-sight 1024-trace batch 92.7 us / 843 KB; pathfinding open
    10.3 us, blocked 139.4 us, choke 77.5 us (~20 KB each); replay run
    (40 ticks) 29.5 us / 148 KB. Every per-tick row >=36x inside the 5 ms
    budget.
- Command: `dotnet run --project bench/CommandoWar.Benchmarks -c Release --
  expansions`
  - Result: closed set reached - open 39, blocked 1168, choke 780; default cap
    `Width * Height` = 1600.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  - Result: initial `0xF2F3DF0D820AD9AC`, final `0x838D3AE7DBFB735D`
    (format 1), 33 events - unchanged.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
  - Result: `FSharp.Core 10.1.303` only; no transitive package, no
    BenchmarkDotNet.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj reference`
  - Result: "There are no Project to Project references".
- Command: source scan of `src/CommandoWar.Sim/*.fs` for
  `benchmarkdotnet|Stopwatch|DateTime|System.Random|godot|monogame|raylib`
  - Result: only doc-comment prose (`Stopwatch` named in `Pathfinding.fs` as
    what it does NOT use; Godot named in `Scenario.fs` / `Diagnostics.fs` as a
    future content reader / renderer). No type, API, or value match.
- Command: `git status --porcelain`
  - Result: TASK-013 working-tree change (as recorded in its ledger) plus, for
    this task: new `bench/CommandoWar.Benchmarks/*`, `content/benchmarks/*`,
    `tasks/TASK-014-BENCHMARK-HARNESS.md`, this file; modified
    `CommandoWar.slnx`, `.gitignore`, `docs/04`, `docs/07`, `docs/09`,
    `docs/11`, `docs/12`, `docs/ledger/2026-09-04-TASK-013-pathfinding.md`,
    `PROJECT_STATE.yaml`. Nothing under the client spikes, `src/_scratch`,
    `tests/`, `Simulation.fs`, `Canonical.fs`, `Pathfinding.fs`, `Sight.fs`,
    `Fixture.fs`, or any `cwheadless` verb.

### Evidence

- **Results table:** `content/benchmarks/BASELINE.md` (single table, run
  environment, observations, expansion-budget evidence).
- **Fixture pin:** `0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`, 33 events,
  `Canonical.FormatVersion` 1 - unmoved (`cwheadless fixture`, `dotnet test`).
- **Dependency direction:** Benchmarks -> Headless -> Sim and Benchmarks -> Sim
  only; `CommandoWar.Sim` has no `PackageReference` beyond `FSharp.Core` and no
  `ProjectReference` (`dotnet list ... package` / `... reference`).
- **Determinism suite:** `Passed: 145` before and after; the benchmark project
  is in `CommandoWar.slnx` but has no test SDK and no `[<Fact>]`, so
  `dotnet test` does not collect it.
- **Pathfinding budget:** `cwbench expansions` - a single legitimate query
  closes up to 73% of a 40x40 grid; the `Width * Height` cap is a
  correctly-sized safety ceiling and is left unchanged (B-011 owns any
  per-tick budget).

### Deviations and unresolved issues

- **Artifacts land in `./BenchmarkDotNet.Artifacts/` at the repo root** (cwd of
  `dotnet run`), not under `bench/`. Both `.gitignore` patterns cover it; the
  README documents it. Setting a fixed artifacts path would need a
  runtime-relative path from `AppContext.BaseDirectory`; not worth it.
- **`[Host]` shows `DEBUG` in the BenchmarkDotNet banner.** The workload job
  (`Job-*`) is the optimised Release build and the sub-1% standard deviations
  confirm optimised code; this is a cosmetic F#/SDK assembly-attribute
  labelling quirk, noted in `BASELINE.md`.
- **`docs/09` s2.8 rows not covered:** 50-agent perception (B-015), appraisal
  batch (B-017), full synthetic 50-agent tick (B-019). Those systems do not
  exist; a commented slot in `Benchmarks.fs` and a `BASELINE.md` note record
  where they attach. No stub was written (task Alternative: TASK-014b was not
  needed - every row for an existing system is covered).
- **50-agent movement allocates ~86 KB/tick** from `Simulation.commandIntake`
  copying the agent array once per accepted command (O(n^2)). Well inside
  budget; flagged in `BASELINE.md` as the first measured hot spot for B-011.
- **`Sight.trace` allocates ~0.8 KB/call** (a `ResizeArray` path + a record).
  Flagged for B-015: a `visible`-only fast path or a reused buffer if
  Perception traces every agent pair every tick.
- **`docs/09` s8 diagnostic-visualisation rule:** does not apply. TASK-014 adds
  no authoritative spatial or tactical state (it only measures existing
  modules), so there is no diagnostic frame extension or golden render to add.
- **Numeric per-subsystem budgets not set.** `docs/04` s19 / `docs/07` s10
  keep the 5 ms per-tick and no-unbounded-allocation budgets; a tighter
  per-system number waits for the real phases (B-015 / B-017 / B-019) and the
  greybox map (B-025). Recorded as a follow-up in both docs.
- Clean build in the working tree (existing `bin/`/`obj/`), not a fresh clone.

### Documents updated

- `tasks/TASK-014-BENCHMARK-HARNESS.md` (new; status `review`)
- `tasks/TASK-013-PATHFINDING.md` (`review -> done`)
- `docs/04_SIMULATION_SPEC.md` (section 19 "Realised by TASK-014")
- `docs/07_VERTICAL_SLICE.md` (section 10 "Realised by TASK-014")
- `docs/09_TEST_STRATEGY.md` (section 2.8 "Realised by TASK-014")
- `docs/11_BACKLOG.md` (TASK-014 row + B-013 `-> active`; TASK-013 / B-010
  `-> done`)
- `docs/12_PROGRESS_LEDGER.md` ("Pinned facts": new BenchmarkDotNet 0.15.8
  row, Green tests "Confirmed by" -> TASK-014 (still 145); index rows for
  TASK-014 and the TASK-013 acceptance)
- `docs/ledger/2026-09-04-TASK-013-pathfinding.md` (Review block accepted
  2026-09-04)
- `content/benchmarks/BASELINE.md`, `content/benchmarks/README.md` (new)
- `.gitignore` (bench artefact path)
- `CommandoWar.slnx` (`/bench/` folder + project)
- `PROJECT_STATE.yaml` (`active_work -> TASK-014`)
- this entry

### AGENTS.md / docs/09 section 8 standing rule

No rule change and the rule does not apply. TASK-014 adds no authoritative
spatial or tactical state: it is a dev-only harness that measures `Simulation`,
`Sight`, `Pathfinding`, `Canonical`, `Hashing`, and `Replay` over fixed
synthetic vectors. There is no new `GridLayer`, `EdgeMarker`, or `Overlay`
case and no golden visualiser output, because there is no new state to
visualise. `Diagnostics.frame` / `frameOf` and every `content/diagnostics/`
golden are untouched.

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: dev-only BenchmarkDotNet 0.15.8 harness in `bench/CommandoWar.Benchmarks/`
  covering the `docs/09` s2.8 list for the systems that exist (empty tick,
  6-/50-agent placeholder movement, line-of-sight batch, pathfinding
  open/blocked/choke, canonical encode, state hash, replay run); committed
  `content/benchmarks/BASELINE.md` baseline (not a test oracle) with a
  regeneration command; every per-tick measurement >=36x inside the 5 ms
  budget; `Pathfinding.find` cap left unchanged with closed-set evidence for
  B-011; `CommandoWar.Sim` gains no dependency; `dotnet test` stays at 145;
  fixture hashes unmoved.
