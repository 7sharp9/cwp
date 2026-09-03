# TASK-010: Implement the authoritative terrain grid

Status: review
Owner: Dave
Phase: P2
Gate: G2 (deterministic core); realises backlog B-008
Size: M

## Objective

Add the framework-neutral authoritative terrain model to `CommandoWar.Sim`:
per-cell elevation, movement class and integer cost, opacity, and directional
low cover, plus the authored input, its one-pass validation, and the total
bounds-checked query functions over it. Data and queries only: no tick phase
consumes terrain (the TASK-008 `Objective`-algebra precedent).

This is spec sections 7 (Gameplay cell data), 9 (cover half), and 10
(`WorldState.Terrain`), and `docs/06` section 4 (Map contract, required
logical layers).

## Why this task exists

B-009 (line of sight) and B-010 (pathfinding) both depend on B-008. Terrain is
the authoritative substrate they read; without it each would invent its own
per-cell shape. There is no cheaper substitute in P2.

## Required reading

1. `PROJECT_STATE.yaml`, `AGENTS.md`
2. `decisions/ADR-0002-SIMULATION-BOUNDARY.md` (canonical-encoding rules,
   allow/forbid lists, compliance checks); `decisions/ADR-0004`
3. `docs/04_SIMULATION_SPEC.md` sections 3, 4, 6, 7, 8, 9, 10, 17, 19, 20, 21;
   `docs/03_ARCHITECTURE.md` sections 2, 4, 8, 9, 16; `docs/06` sections 3, 4,
   7; `docs/07` section 3; `docs/09` section 3
4. `src/CommandoWar.Sim/` in full, especially `Grid.fs`, `Domain.fs`,
   `Scenario.fs`, `Canonical.fs`, `Simulation.fs`
5. `tests/CommandoWar.Sim.Tests/{FixtureTests,ScenarioTests}.fs`,
   `src/CommandoWar.Headless/Fixture.fs`
6. `content/fixtures/SPIKE-FIXTURE.md`
7. `docs/ledger/2026-09-03-TASK-008-scenario-dto.md`,
   `docs/ledger/2026-09-02-TASK-003-determinism-harness.md`

## Dependencies

- TASK-008 accepted and `done`.

## Central decision: terrain and the canonical hash

Static terrain is authoritative but immutable within a run at this stage
(destruction is B-019 with combat). Putting it into `Canonical.encode` would
move every pinned fixture hash and churn `FixtureTests.fs`, `ScenarioTests.fs`,
`SPIKE-FIXTURE.md`, and the two retained spike evidence sets for zero
determinism benefit.

**Decision (taken): `WorldState` gains `Terrain`, `Canonical.encode` does NOT
include it while terrain carries no per-tick mutable state.** Recorded in spec
section 17 and in the ADR-0002 amendment "Static authoritative data and the
canonical image". `Canonical.FormatVersion` stays `1`; it bumps when
destructible terrain lands. This is not larger than an amendment, so
implementation proceeded.

## Allowed scope

- `src/CommandoWar.Sim/Terrain.fs` (new; compiled after `Grid.fs`, before
  `Domain.fs`), `Domain.fs` (`WorldState.Terrain`), `Simulation.fs`
  (`World.create` / `World.ofScenario`), `Scenario.fs` (terrain raw shape,
  validation, content version 2), `CommandoWar.Sim.fsproj`;
- tests in `tests/CommandoWar.Sim.Tests/`;
- `decisions/ADR-0002` amendment;
- `docs/04` sections 7 / 9 / 17 / 21, `docs/06` sections 3 / 4 realisation
  notes;
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml`);
- finalise TASK-009 and record acceptance of the TASK-006 finalisation entry.

## Forbidden scope

- line of sight or a sight algorithm (B-009); pathfinding or A* (B-010);
  movement, reservations, or formation logic consuming terrain (B-011);
  terrain destruction or damage state (B-019); a per-phase terrain read in
  `Simulation.step`; objective evaluation or mission logic; a Godot/Tiled
  terrain importer or the content CLI (B-024);
- floating-point terrain values; a new dependency or project;
- changing `Setup.sixAgentWorld`, the shared fixture parameters, or the pinned
  fixture hashes;
- touching the client spikes, `src/_scratch`, `content/`, `.slnx`;
  reorganising `decisions/` or `tasks/`; destructive git.

## Required work

1. A terrain module: `Direction` (four cardinals), `MovementClass`
   (`Passable` / `Impassable`), `Terrain` (dense row-major integer arrays,
   never a map keyed by `Cell`), `Terrain.empty`, `Terrain.build` (from
   validated authored data, contained mutable builder).
2. Total bounds-checked queries: `Terrain.passable` / `moveCost` / `elevation`
   / `opaque` / `cover : Terrain -> Cell -> Direction -> int`. No consumer; a
   module comment says so.
3. `WorldState.Terrain`; `World.create` builds empty terrain; `Canonical.encode`
   unchanged.
4. `RawScenario.TerrainLayer : RawTerrainLayer option`; `ScenarioContent.Version`
   1 -> 2; `Scenario.validate` extended with typed cases (dimensions mismatch,
   out-of-map feature, duplicate cell / cover feature, unknown terrain / cover
   class, negative elevation / cost / cover level, deployment on an impassable
   cell). One pass, no silent defaults. Absent layer is legal and means empty
   terrain.
5. `World.ofScenario` carries `scenario.Terrain` onto the world.
6. Tests: valid terrain scenario, every new invalid condition, the one-pass
   property, each query on a hand-built terrain, version-2 acceptance and
   version-1 rejection, and a pin that the shared fixture still hashes to
   `0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`.
7. Register `Terrain.fs` in `CommandoWar.Sim.fsproj`. `TreatWarningsAsErrors`
   clean.
8. Spec / `docs/06` realisation notes and the ADR-0002 amendment written.

## Acceptance criteria

- [x] `src/CommandoWar.Sim/Terrain.fs` exists with `Direction`,
      `MovementClass`, `Terrain` (dense arrays), `Terrain.empty`,
      `Terrain.build`, and the five queries, each total and bounds-checked.
      No tick phase calls them (comment says so).
- [x] `WorldState` gains `Terrain: Terrain`. `Canonical.encode` unchanged;
      `Canonical.FormatVersion` still `1`.
- [x] `RawScenario.TerrainLayer : RawTerrainLayer option`;
      `ScenarioContent.Version = 2`; version 1 rejected with
      `UnsupportedContentVersion(1, 2)`.
- [x] `Scenario.validate` reports every new terrain fault in one pass
      (`ScenarioError`, 30 cases). One test per condition.
- [x] `World.ofScenario` carries the authored terrain onto the world.
- [x] `dotnet test CommandoWar.slnx -c Release` = `Passed: 102` (79 + 23).
- [x] `cwheadless fixture` unchanged: initial `0xF2F3DF0D820AD9AC`, final
      `0x838D3AE7DBFB735D`, 33 events.
- [x] `dotnet list ... package --include-transitive` = `FSharp.Core 10.1.303`
      only; source scan clean (two pre-existing Godot doc-comment lines in
      `Scenario.fs` only).
- [x] `decisions/ADR-0002` amendment written; `docs/04` sections 7 / 9 / 17 /
      21 and `docs/06` sections 3 / 4 carry realisation notes.
- [x] Backlog rows, ledger index row + detail file, `PROJECT_STATE.yaml`, and
      task status updated. No forbidden scope entered.

## Required verification

- `dotnet build CommandoWar.slnx -c Release` (0 warnings, 0 errors)
- `dotnet test CommandoWar.slnx -c Release` before and after
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
- source scan of `src/CommandoWar.Sim` for
  `godot|mibo|monogame|raylib|DateTime|Stopwatch|System.Random|Dictionary|HashSet|groupBy|float`
- `git status`

## Evidence to capture

- the `Terrain` / `RawTerrainLayer` / new `ScenarioError` shapes;
- a sample terrain validation failure list;
- the query results on the hand-built terrain;
- the pinning-test hashes;
- exact commands and results.

## Expected files

- `src/CommandoWar.Sim/Terrain.fs` (new), `Domain.fs`, `Simulation.fs`,
  `Scenario.fs`, `CommandoWar.Sim.fsproj`;
- `tests/CommandoWar.Sim.Tests/TerrainTests.fs` (new), `ScenarioTests.fs`,
  `CommandoWar.Sim.Tests.fsproj`;
- `decisions/ADR-0002-SIMULATION-BOUNDARY.md` (amendment);
- `docs/04_SIMULATION_SPEC.md`, `docs/06_CONTENT_AND_PRESENTATION.md`;
- `tasks/TASK-010-TERRAIN-GRID.md`, `tasks/TASK-009-*.md`,
  `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`,
  `docs/ledger/2026-09-03-TASK-010-terrain-grid.md`, `PROJECT_STATE.yaml`.

## Rollback or removal

`Terrain.fs` and the `WorldState.Terrain` / `Scenario.Terrain` /
`RawScenario.TerrainLayer` fields are removable without touching the shared
fixture, `Canonical.encode`, `Simulation.step`, `Replay.fs`, or their tests:
nothing in the phase pipeline or the canonical encoding depends on terrain.
Reverting `ScenarioContent.Version` to 1 and dropping the new `ScenarioError`
cases restores the TASK-008 validator.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Alternative

If the terrain/canonical decision had proved larger than an ADR-0002 amendment
(terrain must be mutable now, or the format bump is unavoidable), the task was
to stop after writing this file and the decision analysis and put it to Dave.
It did not: the amendment route is clean and all five pinned files are
unmoved.
