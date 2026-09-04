# TASK-014: Headless performance and allocation benchmark harness

Status: done
Owner: Dave
Phase: P2
Gate: G2 (deterministic core); realises backlog B-013
Size: S

## Objective

Add a headless, framework-neutral performance and allocation benchmark harness
for `CommandoWar.Sim` as a new dev-only project `bench/CommandoWar.Benchmarks/`.
It covers the `docs/09_TEST_STRATEGY.md` section 2.8 list for the systems that
exist today: empty fixed tick; agent movement under the placeholder rule
(six-agent and a synthetic ~50-agent world); line-of-sight batch (`Sight.trace`);
pathfinding through open / blocked / choke-point maps (`Pathfinding.find` and
`findWithin`); and canonical encode + state hash + replay run. It records
median, P95 tail, allocation per operation, runtime, build configuration,
machine, and commit into a committed results document
(`content/benchmarks/BASELINE.md`) with a documented regeneration command.

It changes no authoritative behaviour, is never referenced by
`CommandoWar.Sim`, and does not run inside the normal `dotnet test` path. Its
immediate purpose is to size the B-011 pathfinding expansion budget against
real profiling evidence (the `Pathfinding.find` default cap `Width * Height`
was deferred to this task in the TASK-013 alternative).

## Dependencies

- TASK-010 (terrain grid) accepted and `done` (realises B-008).
- Uses TASK-012 `Sight` and TASK-013 `Pathfinding` as measurement subjects;
  both accepted and `done`.
- Finalisation of TASK-013 / B-010 to `done` (precondition only).

## Central decisions

- New project `bench/CommandoWar.Benchmarks/CommandoWar.Benchmarks.fsproj`: a
  `net10.0` console exe, `OutputType` `Exe`, `TreatWarningsAsErrors` true,
  referencing `CommandoWar.Sim` and `CommandoWar.Headless` (for the fixture and
  the demo scenarios) and BenchmarkDotNet only. BenchmarkDotNet is pinned
  explicitly and recorded in the ledger "Pinned facts". New top-level `bench/`
  directory beside `src/` and `tests/`.
- Added to `CommandoWar.slnx` under a new `/bench/` solution folder. This is
  the one permitted `.slnx` edit. The benchmark project builds in Release with
  the solution but is NOT collected by `dotnet test` (no test SDK, no
  `[<Fact>]`); the determinism suite stays at `Passed: 145` exactly.
- ADR-0002 compliance: the arrow is Benchmarks -> Headless -> Sim and
  Benchmarks -> Sim, never the reverse. `CommandoWar.Sim` gains no
  `PackageReference` and no `ProjectReference`. BenchmarkDotNet types never
  cross into `CommandoWar.Sim`.
- Determinism: every benchmark steps the real deterministic simulation with
  fixed seeds and fixed scenarios. BenchmarkDotNet only measures; it introduces
  no randomness into authoritative state, no wall-clock read inside the sim,
  and no `System.Random`. The harness does not mix debug overlays, diagnostic
  frame construction, or renderer work into a core-sim measurement.
- Perception, appraisal, and combat do not exist yet: a commented harness slot
  and a `docs/09` note record that those rows are added by B-015 / B-017 /
  B-019; they are not stubbed.
- Output: a committed Markdown results table at `content/benchmarks/BASELINE.md`
  (new directory), one row per benchmark with Mean, P95, Allocated, Ops, and a
  header block carrying BenchmarkDotNet version, .NET SDK, build configuration
  (Release), machine description, and commit. It is a recorded baseline, NOT a
  byte-pinned golden and NOT a test oracle: no test asserts on timings. A short
  `content/benchmarks/README.md` explains that, names the regeneration command,
  and states the acceptance rule (a reviewer reads the table for
  order-of-magnitude sanity against the 5 ms / 5_000_000 ns per-tick budget in
  `docs/04` section 19).
- `tests/CommandoWar.Sim.Tests/BenchmarkTests.fs` is KEPT unchanged as the
  cheap in-suite regression flag (it asserts only on tick progression and
  writes timing to test output). Folding it out would drop the green count
  below 145; keeping it holds the count and the fast regression signal. It is
  not slowed and does not depend on the new project.
- Pathfinding budget evidence: the actual `maxExpansions` reached for the
  blocked and choke maps is recorded in `BASELINE.md` and the ledger. This task
  does NOT change `Pathfinding.find`'s default cap; a sized recommendation for
  B-011 is recorded as an explicit note.

## Allowed scope

- `bench/CommandoWar.Benchmarks/` (new project: `.fsproj`, `Program.fs`,
  synthetic-world builder, benchmark modules);
- `CommandoWar.slnx` (the one `/bench/` folder + project entry);
- `content/benchmarks/BASELINE.md` and `content/benchmarks/README.md` (new);
- `.gitignore` (confirm / add BenchmarkDotNet artefact directories);
- `docs/09` section 2.8, `docs/04` section 19, `docs/07` section 10
  realisation notes;
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml`);
- finalisation of TASK-013 / B-010 to `done` (precondition only).

## Forbidden scope

- Any reference from `CommandoWar.Sim` to the benchmark project,
  BenchmarkDotNet, or a new package; any framework, graphics, or UI dependency
  anywhere.
- Wiring anything into `Simulation.step` or any phase; changing authoritative
  behaviour, `Canonical.encode` / `Canonical.FormatVersion`,
  `Setup.sixAgentWorld`, the shared fixture parameters, the pinned hashes
  (`0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`, 33 events), the
  `Diagnostics.frame` / `frameOf` signatures, or any existing `cwheadless`
  verb's output.
- Changing `Pathfinding.find`'s default budget cap (record a recommendation for
  B-011 instead); implementing perception / appraisal / combat benchmarks or
  stubs for systems that do not exist.
- A benchmark that runs in the normal `dotnet test` path or slows the
  determinism suite; byte-pinning timing output as a golden; asserting on
  wall-clock numbers in a test.
- New authoritative content or scenarios (reuse `Fixture` / `DemoScenario` /
  `LosDemo` / `PathDemo` plus synthetic in-bench worlds); an on-disk content
  format (B-024).
- Touching the client spikes, `src/_scratch`; reorganising `decisions/` or
  `tasks/`; destructive git.

## Required work

1. `bench/CommandoWar.Benchmarks/`: the `.fsproj` (pinned BenchmarkDotNet,
   project references, warnings-as-errors), a `Program.fs` entry point running
   `BenchmarkSwitcher` over the benchmark classes, and benchmark modules
   covering the list above. Fixed seeds and scenarios; `[<MemoryDiagnoser>]`
   for allocation. A synthetic ~50-agent world builder and the synthetic
   pathfinding / line-of-sight terrains live here, never in `CommandoWar.Sim`.
2. `CommandoWar.slnx`: the new `/bench/` folder and project entry. No other
   solution change.
3. `content/benchmarks/BASELINE.md` and `content/benchmarks/README.md`: the
   results table from a local Release run on the development machine, the
   header block, the regeneration command, and the "not a test oracle"
   acceptance rule.
4. `.gitignore`: confirm / add `BenchmarkDotNet.Artifacts/` (and
   `bench/**/BenchmarkDotNet.Artifacts/`) so run output is not committed.
5. `docs/09` section 2.8: a "Realised by TASK-014" note listing which rows are
   covered now and which are deferred to B-015 / B-017 / B-019, pointing at
   `content/benchmarks/`. `docs/04` section 19 and `docs/07` section 10: note
   that the harness now exists and the numeric budgets can be set from
   `BASELINE.md` (set them only if the evidence is unambiguous; otherwise
   record a follow-up).
6. `AGENTS.md` / `docs/09` section 8: the standing diagnostic-visualisation
   rule does not apply (this task adds no authoritative spatial or tactical
   state); the completion report confirms this rather than adding an overlay.
7. Control docs: this task; `docs/11_BACKLOG.md`; `docs/12_PROGRESS_LEDGER.md`
   index row + `docs/ledger/2026-09-04-TASK-014-benchmark-harness.md`; the
   TASK-013 ledger detail Review block and index Accepted cell;
   `PROJECT_STATE.yaml`. Refresh the index "Pinned facts" with the
   BenchmarkDotNet version and .NET SDK; the green test count stays 145.

## Acceptance criteria

- [x] `bench/CommandoWar.Benchmarks/` exists: a `net10.0` `Exe` referencing
      `CommandoWar.Sim` + `CommandoWar.Headless` + BenchmarkDotNet (pinned)
      only, `TreatWarningsAsErrors` true.
- [x] Benchmarks cover: empty fixed tick; six-agent and ~50-agent placeholder
      movement; a `Sight.trace` batch; `Pathfinding.find` over open / blocked /
      choke maps; one `findWithin` recording the closed-set size reached;
      `Canonical.encode`; `Hashing.hash`; `Replay.run` over the shared fixture.
      `[<MemoryDiagnoser>]` on every class.
- [x] `CommandoWar.slnx` gains one `/bench/` folder + project entry; nothing
      else changes.
- [x] `dotnet build CommandoWar.slnx -c Release` = 0 warnings, 0 errors
      (benchmark project included).
- [x] `dotnet test CommandoWar.slnx -c Release` before and after = `Passed:
      145` (unchanged; the benchmark project contributes no tests).
- [x] `dotnet run --project bench/CommandoWar.Benchmarks -c Release` completes
      and produces the table committed to `content/benchmarks/BASELINE.md`.
- [x] `cwheadless fixture` unchanged: initial `0xF2F3DF0D820AD9AC`, final
      `0x838D3AE7DBFB735D`, 33 events.
- [x] `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
      --include-transitive` = `FSharp.Core` only, no `ProjectReference`, no
      BenchmarkDotNet.
- [x] Source scan of `src/CommandoWar.Sim` for
      `benchmarkdotnet|Stopwatch|DateTime|System.Random|godot|monogame|raylib`
      = no code matches.
- [x] `content/benchmarks/BASELINE.md` + `README.md` committed with the header
      block, regeneration command, and "not a test oracle" acceptance rule.
- [x] `docs/09` section 2.8, `docs/04` section 19, `docs/07` section 10 notes
      written. Backlog rows, ledger index row + detail file, "Pinned facts",
      `PROJECT_STATE.yaml`, and task status updated. No forbidden scope
      entered.

## Required verification

- `dotnet build CommandoWar.slnx -c Release` (0 warnings, 0 errors)
- `dotnet test CommandoWar.slnx -c Release` before and after
- `dotnet run --project bench/CommandoWar.Benchmarks -c Release` (full run or a
  `--filter` subset), byte-compared against the committed `BASELINE.md` table
  shape; state the machine and commit
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
- source scan of `src/CommandoWar.Sim`
- `git status`

## Rollback or removal

`bench/CommandoWar.Benchmarks/` is a leaf: removing the project, the
`CommandoWar.slnx` `/bench/` entry, `content/benchmarks/`, the `.gitignore`
line, and the doc notes restores the pre-task state without touching
`Simulation.step`, `Canonical.encode`, the shared fixture, `Pathfinding`, or
any test.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Alternative

If BenchmarkDotNet proves heavy or flaky headless in this environment, land a
hand-rolled harness in the same `bench/CommandoWar.Benchmarks/` project using
`System.Diagnostics.Stopwatch` plus `GC.GetAllocatedBytesForCurrentThread()`
with explicit warmup and iteration counts and percentile arithmetic, producing
the identical `content/benchmarks/BASELINE.md` table and no new package
dependency. Do not add BenchmarkDotNet to the test project as a workaround. If
the `docs/09` section 2.8 list cannot be fully covered for the existing systems
in size S, land the empty-tick, movement, line-of-sight, pathfinding, and
hash/replay benchmarks as TASK-014 and split the remaining rows into TASK-014b
with a note; do not land the harness without the pathfinding open/blocked/choke
measurement.
