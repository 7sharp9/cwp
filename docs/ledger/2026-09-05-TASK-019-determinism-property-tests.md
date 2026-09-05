## 2026-09-05 - TASK-019 - Generative determinism property tests

**Owner:** Dave with coding-agent assistance
**Source revision:** `65f337c` (Add sub-cell movement progress)
**Environment:** Windows 11 Pro 26200 (25H2); .NET SDK 10.0.303; .NET 10.0.11;
xUnit 2.9.3; FsCheck / FsCheck.Xunit 3.3.4
**Status change:** TASK-019 `proposed -> active -> review`

### Scope-down confirmed, not just carried over

B-012b (split from B-012 by TASK-016) bundled generative property tests with
component-level per-agent subhashes in the divergence report on the shared
justification "both touch determinism evidence." On inspection they share no
design surface: `Canonical.firstDifferingSection` already reports which
specific agent differs first (`Agent[id]`) by direct value comparison, and the
subhashes' stated reason (`docs/04` section 17, "desirable once the world
grows") has no evidence behind it — nothing in this repository has run past
~50 agents (`content/benchmarks/BASELINE.md`). This task lands the property
tests alone; subhashes stay proposed under B-012b.

### Dependency version confirmation

`FsCheck` and `FsCheck.Xunit` 3.3.4 confirmed against the NuGet
flat-container index (both list 3.3.4, with 3.4.0 newer but not requested)
and `FsCheck.Xunit`'s own nuspec: it depends on
`xunit.extensibility.execution [2.4.1, 3.0.0)`, matching this project's
pinned `xunit` 2.9.3 (a v2 test project) — this is the `FsCheck.Xunit`
package, not `FsCheck.Xunit.v3` (which targets `xunit.v3`). FsCheck 3.x's
F#-idiomatic API (`Gen`, `Arb`, `Prop`, the `gen` builder) lives under
`FsCheck.FSharp`, confirmed against the published API reference before
writing any generator.

### Central decisions

- **Only the three docs/09 section 2.2 properties current systems can support
  are landed**: determinism under generated commands, no agent on an invalid
  cell after movement, and pathfinding endpoints/cost on random terrain. The
  rest of that list (commitment references, dead-agent command rejection,
  serialization round-trip, appraisal, objective monotonicity) name systems
  that do not exist yet (P3/P4) and are out of scope.
- **`WorldState` built directly, not through `World.create` / `Scenario.validate`.**
  The generator needs full control over terrain and needs to guarantee every
  agent starts on a passable cell (an agent placed on impassable terrain
  would fail property 2 for a generator reason, not a movement-phase reason);
  `Scenario.validate` also requires objectives/areas/extraction markers that
  add no coverage here. `WorldState` is a plain record, so this is direct
  construction of an already-valid value, the same discipline
  `PathfindingTests.fs` uses for hand-built `Terrain`.
- **Determinism reuses `Replay` / `Divergence` exactly as `Corpus.checkEntry`
  does**: `Replay.run` twice on the same generated `ReplayRecord`, compared
  with `Divergence.compare`, expecting `Match`. This generalises the
  self-double-run pattern from 5 fixed corpus entries to an open-ended
  FsCheck-generated space.
- **Command targets are arbitrary in-bounds cells, not restricted to
  passable ones.** Restricting them would only exercise successful routing;
  leaving them unrestricted also exercises `MovementBlocked` (docs/04 section
  8 step 6) when a target has no path.
- **Property 2 did not need scoping down.** The task file's stated risk — a
  counterexample from an agent walking onto a cell held by a stationary agent
  (the documented TASK-017/018 gap) — did not materialise: 200 generated
  cases passed. On reflection this gap is orthogonal to what the property
  actually checks (`Terrain.passable`, a pure terrain query); the gap is
  about an agent's target cell being occupied by another *agent*, not about
  terrain validity, so it was never going to surface through this predicate.
  No generator narrowing applied.
- **Bounds kept small** (grid 3..7 per side, 1..4 agents, 5..15 ticks,
  0..agentCount*3 commands): well inside the TASK-014 per-tick budget and the
  largest scale this repository has actually run (~50 agents), per
  `AGENTS.md` "do not build speculative machinery" — no evidence yet
  justifies exercising larger worlds here.

### Changes

- **`tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj`.** Two new
  `PackageReference`s (`FsCheck`, `FsCheck.Xunit`, both 3.3.4); one new
  compile entry.
- **`tests/CommandoWar.Sim.Tests/DeterminismPropertyTests.fs`** (new).
  Shared generators (`boundsGen`, `terrainGen`, `randomCaseGen`,
  `terrainAndEndpointsGen`) and three `[<Property>]` tests, each run at 200
  cases (`MaxTest = 200`, above FsCheck's 100-case default).
- **`docs/09_TEST_STRATEGY.md`.** Section 2.2 realisation note.
- **Control.** `tasks/TASK-019-DETERMINISM-PROPERTY-TESTS.md` (new);
  `docs/11_BACKLOG.md` (new TASK-019 row; B-012b narrowed to subhashes alone);
  `PROJECT_STATE.yaml` (`active_work -> TASK-019`); `docs/12_PROGRESS_LEDGER.md`
  ("Green tests" pinned row, new FsCheck pinned row, this index row); this
  entry.
- No change to `src/CommandoWar.Sim` or `src/CommandoWar.Headless` production
  code — this task adds tests over existing behaviour only.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release` (precondition check)
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)`.
- Command: `dotnet test CommandoWar.slnx -c Release` (precondition check)
  - Result: `Passed! - Failed: 0, Passed: 168, Skipped: 0, Total: 168`.
- Command: `dotnet build tests/CommandoWar.Sim.Tests -c Release` (first build
  after adding the two packages and the new file)
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` — compiled clean on the
    first attempt; the `FsCheck.FSharp` API surface used
    (`Gen.choose`/`elements`/`frequency`/`shuffle`/`arrayOfLength`/`map`, the
    `gen` computation expression, `Arb.fromGen`, `Prop.forAll`) matched what
    the published reference described.
- Command: `dotnet test tests/CommandoWar.Sim.Tests -c Release --filter
  FullyQualifiedName~DeterminismPropertyTests`
  - Result: `Passed! - Failed: 0, Passed: 3, Skipped: 0, Total: 3`.
- Command: `dotnet test CommandoWar.slnx -c Release` (after)
  - Result: `Passed! - Failed: 0, Passed: 171, Skipped: 0, Total: 171` (168 +
    3 new property tests).
- Command: `dotnet build CommandoWar.slnx -c Release` (after)
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)`.
- Deliberately-broken run: inverted property 2's predicate
  (`Terrain.passable` -> `not (Terrain.passable ...)`), ran
  `dotnet test ... --filter FullyQualifiedName~DeterminismPropertyTests`,
  reverted.
  - Result: `Falsifiable, after 1 test (0 shrinks)
    (3294591959483012982,13607628419313544529). Last step was invoked with
    size of 1 and seed of (4480945700292264294,5241962837254238335):` followed
    by the full generated `RandomCase` (world, terrain, agents, commands) —
    confirms FsCheck's failure output carries a replayable seed and the
    complete counterexample. Reverted; `dotnet test CommandoWar.slnx -c
    Release` confirmed back to `Passed: 171`.
- Command: `dotnet list tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj
  package --include-transitive`
  - Result: top-level gains exactly `FsCheck 3.3.4` and `FsCheck.Xunit
    3.3.4`; no new transitive package beyond what those two pull in
    (`FSharp.Core` already present at the SDK-pinned version).
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive` and the same for `CommandoWar.Headless`
  - Result: `FSharp.Core 10.1.303` only, both projects — unaffected.
- Command: source scan of `src/CommandoWar.Sim` and `src/CommandoWar.Headless`
  for `godot|monogame|raylib|mibo|System\.Random|DateTime\.Now|Stopwatch`
  (case-insensitive)
  - Result: seven matches, all pre-existing module-doc prose (Godot mentions
    in `Diagnostics.fs`/`Scenario.fs`/`DiagnosticRender.fs`/`Fixture.fs`, a
    "no `Stopwatch`" comment in `Pathfinding.fs`), unchanged by this task.
- Command: `git status --short`
  - Result: exactly the fsproj edit plus the two new files (task file, test
    file) before documentation updates; matches the allowed scope.

### Evidence

- **Determinism property**: 200 generated (world, command-sequence) cases,
  each replayed twice and compared via `Divergence.compare`, all `Match`.
- **Invalid-cell property**: 200 generated cases, every post-tick agent
  position passable; no counterexample from the known stationary-occupancy
  gap (see Central decisions).
- **Pathfinding property**: 200 generated (terrain, start, goal) triples;
  every `Found` result begins/ends at the requested cells, forms an adjacent
  passable chain, and its cost matches the recomputed sum — the same
  invariant `PathfindingTests.fs`'s hand-built `propertyTerrain` case checks,
  now over an open-ended terrain space.
- **Counterexample/seed reporting verified**: the deliberately-broken run
  above.
- **Dependency boundary**: `dotnet list --include-transitive` on all three
  points above.
- **Green count**: `dotnet test` `168 -> 171`.

### Deviations and unresolved issues

- None. No production-code change was needed; no property required
  generator narrowing.

### Documents updated

- `tasks/TASK-019-DETERMINISM-PROPERTY-TESTS.md` (new)
- `tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj`,
  `DeterminismPropertyTests.fs` (new)
- `docs/09_TEST_STRATEGY.md` (section 2.2)
- `docs/11_BACKLOG.md` (new TASK-019 row; B-012b narrowed)
- `docs/12_PROGRESS_LEDGER.md` ("Green tests" pinned row; new FsCheck pinned
  row; this index row)
- `PROJECT_STATE.yaml` (`active_work -> TASK-019`)
- this entry

### AGENTS.md / docs/09 section 8 standing rule

Does not apply: this task adds no authoritative spatial or tactical state —
it is test infrastructure over existing, already-diagnosed behaviour (the
TASK-016 precedent for the same rule).

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-05)
- Notes: three FsCheck property tests landed (determinism under generated
  commands, no agent on an invalid cell post-movement, pathfinding
  endpoints/cost on random terrain); `FsCheck`/`FsCheck.Xunit` 3.3.4 in the
  test project only; component subhashes stay proposed under B-012b with a
  narrowed justification; no production-code change. Accepted explicitly
  ("look good") at the start of the following session; TASK-019
  `review -> done`.
