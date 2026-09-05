# TASK-019: Generative determinism property tests

Status: review
Owner: Dave
Phase: P2
Gate: G2 (deterministic core); realises backlog B-012b (narrowed)
Size: M

## Objective

Add FsCheck property tests over an open-ended, FsCheck-generated space of small
worlds and command sequences (`docs/09_TEST_STRATEGY.md` section 2.2), rather
than the fixed hand-built vectors `CorpusTests` already covers.

## Scope down (narrowed from B-012b)

B-012b (split from B-012 by TASK-016) originally bundled two concerns that
share no design surface on inspection: generative property tests, and
component-level per-agent subhashes in the divergence report. This task lands
the property tests only. Subhashes stay proposed under B-012b: their stated
justification (`docs/04` section 17, "desirable once the world grows") has no
evidence behind it yet — nothing in this repository has run past ~50 agents
(`content/benchmarks/BASELINE.md`) — and `Canonical.firstDifferingSection`
already reports which specific agent differs first (`Agent[id]`) by direct
value comparison, with no hash tree needed to do that. Reopen subhashes only if
a future benchmark or a property-test counterexample actually shows the
existing per-agent diff is too slow or too verbose.

Of the docs/09 section 2.2 list, only the three properties below are landed;
the rest name systems that do not exist yet (commitments, appraisal, death,
objectives — P3/P4) and are out of scope.

## Dependencies

- TASK-016 (replay corpus infrastructure: `Corpus`, `Replay`, `Divergence`)
- TASK-018 (sub-cell movement progress: `Canonical.FormatVersion` 2)

## Inputs and assumptions

- `FsCheck` 3.3.4 and `FsCheck.Xunit` 3.3.4 (not `FsCheck.Xunit.v3`), versions
  confirmed against the NuGet flat-container index and `FsCheck.Xunit`'s own
  nuspec, not recalled from memory: `FsCheck.Xunit.v3.nuspec` targets
  `xunit.v3`, while `FsCheck.Xunit` 3.3.4 depends on
  `xunit.extensibility.execution [2.4.1, 3.0.0)`, matching this project's
  pinned `xunit` 2.9.3 (a v2 test project). This mirrors the
  BenchmarkDotNet-in-`bench/`-only precedent: a new package, added only to
  `tests/CommandoWar.Sim.Tests.fsproj`, never to `CommandoWar.Sim` or
  `CommandoWar.Headless`. This task file is the explicit permission
  `AGENTS.md`'s dependency rule requires; no ADR needed for a test-only
  package.
- FsCheck 3.x's F#-idiomatic API lives under `FsCheck.FSharp` (`Gen`, `Arb`,
  `Prop`, the `gen` computation-expression builder), separate from the root
  `FsCheck` namespace; confirmed against the published API reference, not
  assumed from FsCheck 2.x usage.

## Allowed scope

- `tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj` (the two new
  `PackageReference`s, one new compile entry);
- `tests/CommandoWar.Sim.Tests/DeterminismPropertyTests.fs` (new file: the
  generators and the three properties below);
- `docs/09_TEST_STRATEGY.md` section 2.2 (realisation note);
- `docs/11_BACKLOG.md` (B-012b row), `docs/12_PROGRESS_LEDGER.md` (index row +
  detail file), `PROJECT_STATE.yaml`, this task file.

## Forbidden scope

- Component-level or per-agent subhashes in the divergence report, or any
  change to `Canonical.encode` / `Canonical.FormatVersion` (stays proposed
  under B-012b).
- Any docs/09 section 2.2 property naming an unimplemented system:
  commitments, appraisal, death/vehicles, objectives.
- A new dependency beyond `FsCheck` / `FsCheck.Xunit`.
- Touching the client spikes, `src/_scratch`, or `bench/CommandoWar.Benchmarks/`.
- Changing `CommandoWar.Sim` or `CommandoWar.Headless` production code — this
  task adds tests over existing behaviour, not new behaviour. A genuine defect
  the properties uncover is reported as a finding and scoped to a follow-up
  task, not fixed silently mid-task.

## Required work

1. Generators (`DeterminismPropertyTests.fs`): a bounded random `GridBounds`
   (small enough to stay well inside the TASK-014 per-tick budget and the
   observed ~50-agent scale), a random `Terrain` over it (a mix of impassable
   cells and passable cells at `Terrain.BaseMoveCost` and above), a random
   agent roster placed only on passable cells (placing an agent on impassable
   terrain would fail property 2 for a generator reason, not a movement-phase
   reason), and a random `MoveTo` command sequence over arbitrary in-bounds
   targets (deliberately not restricted to passable cells, to also exercise
   the `MovementBlocked` path).
2. Property 1 — determinism under randomly generated commands: for a
   generated world and command sequence, two independent `Replay.run` calls
   from the same input produce a `Match` from `Divergence.compare`. This
   generalises what `CorpusTests` / `Corpus.checkEntry` already does for 5
   fixed scenarios (the self-double-run pattern) to the open-ended space
   FsCheck explores and shrinks.
3. Property 2 — no agent occupies an invalid cell after any tick of the
   movement phase: every `ReplayOutcome.TickStates` entry satisfies
   `Terrain.passable state.Terrain agent.Position` for every agent.
4. Property 3 — pathfinding correctness on random terrain: extends
   `PathfindingTests.fs`'s hand-built `propertyTerrain` check (adjacent
   passable chain, endpoints, recomputed cost matches) to FsCheck-generated
   terrain and endpoints, since a hand-built terrain proves the invariant for
   one map, not the general case.
5. Verify, record a deliberately-broken run to confirm counterexample/seed
   reporting, and update documentation.

## Acceptance criteria

- [x] `FsCheck` 3.3.4 and `FsCheck.Xunit` 3.3.4 referenced only in
      `tests/CommandoWar.Sim.Tests.fsproj`; `dotnet list ... --include-transitive`
      on `CommandoWar.Sim` and `CommandoWar.Headless` shows no new package.
- [x] The three properties above pass under `dotnet test`, each run at least
      100 cases by default (`MaxTest = 200`).
- [x] A deliberately-broken property (temporarily corrupted, then reverted)
      prints a shrunk counterexample and a replayable seed; captured in the
      ledger detail file.
- [x] `dotnet build CommandoWar.slnx -c Release` = 0 warnings, 0 errors.
- [x] `dotnet test CommandoWar.slnx -c Release` count grows from 168 to 171.
- [x] No change to `CommandoWar.Sim` or `CommandoWar.Headless` production code.
- [x] `docs/09` section 2.2, `docs/11_BACKLOG.md` B-012b row,
      `docs/12_PROGRESS_LEDGER.md` index row + detail file,
      `PROJECT_STATE.yaml`, this task file, all updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release` before and after
- `dotnet test CommandoWar.slnx -c Release` before and after (state new count
  vs 168)
- each property run with a fixed `--seed`/`Replay` for reproducibility
- one property temporarily corrupted to confirm shrinking/reporting works,
  then reverted
- `dotnet list tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj package --include-transitive`
  (FSharp.Core, FsCheck, FsCheck.Xunit, plus the existing xunit/test-SDK
  packages — nothing else new)
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  and the same for `CommandoWar.Headless` (unaffected)
- source scan of `src/CommandoWar.Sim` and `src/CommandoWar.Headless`
- `git status`

## Alternative

If property 2 (no agent on an invalid cell) produces a genuine counterexample
from an agent walking onto a cell held by a stationary agent — the documented
TASK-017/018 deviation ("does not resolve an agent moving onto a cell held by
a stationary agent") — that is a real finding to report, not a bug to fix
mid-task: narrow the generator for that property alone (e.g. single-agent
runs) and record the finding in the ledger for a follow-up task.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
