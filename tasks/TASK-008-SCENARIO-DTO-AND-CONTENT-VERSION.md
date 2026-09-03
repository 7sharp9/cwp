# TASK-008: Define and validate the initial scenario DTO and content version

Status: done
Owner: Dave
Phase: P2
Gate: G2 (deterministic core); realises backlog B-007
Size: S

## Objective

Define the framework-neutral authored-scenario model, its content-format
version, its one-pass validation, and the function that builds a `WorldState`
from a validated scenario. This is the ADR-0002 "Content DTOs -> validation ->
Sim setup" stage, in F#, inside `CommandoWar.Sim`.

The change is additive. It must not change the shared fixture, its hashes,
`Setup.sixAgentWorld`, `Canonical.encode`, `FixtureTests.fs`, or `cwheadless`
behaviour.

## Why this task exists

B-008 (terrain grid, elevation, passability, directional cover) and B-024 (the
selected-framework content importer) both depend on a validated, versioned
authored-scenario model. Without it, terrain and the Godot importer would each
invent their own content shape.

## Required reading

1. `PROJECT_STATE.yaml`, `AGENTS.md`
2. `decisions/ADR-0002-SIMULATION-BOUNDARY.md` (the content boundary, the
   allow/forbid lists, the compliance checks), `decisions/ADR-0004`
3. `docs/03_ARCHITECTURE.md` sections 4, 8, 16, 17;
   `docs/04_SIMULATION_SPEC.md` sections 6, 7, 10, 11, 16, 17, 20;
   `docs/06_CONTENT_AND_PRESENTATION.md` sections 3, 4, 7;
   `docs/07_VERTICAL_SLICE.md` section 3; `docs/09_TEST_STRATEGY.md`
4. `src/CommandoWar.Sim/` in full - especially `Ids.fs`, `Grid.fs`,
   `Domain.fs`, `Simulation.fs` (`World.create`, `Setup.sixAgentWorld`),
   `Replay.fs` (the `ReplayError` typed-error pattern and the `FormatVersion`
   constant), `Canonical.fs`
5. `src/CommandoWar.Headless/Fixture.fs`
6. The retained spikes as prior art only, not to change:
   `src/CommandoWar.Client.Godot/src/SpikeContent.cs` and
   `src/CommandoWar.Client.Mibo/Content.fs`
7. The TASK-003 ledger entry in `docs/12_PROGRESS_LEDGER.md`

## Dependencies

- TASK-007 accepted and `done`; ADR-0004 accepted.

## Allowed scope

- `src/CommandoWar.Sim/Scenario.fs` (or the minimal module set), `.fsproj`
  updated;
- `World.ofScenario` (or `Scenario.toWorld`) beside `World.create`;
- tests in `tests/CommandoWar.Sim.Tests/`;
- the scenario / content-version section in `docs/04_SIMULATION_SPEC.md` and
  the realisation note in `docs/06_CONTENT_AND_PRESENTATION.md` section 3;
- the required control-document updates (task, backlog, ledger,
  `PROJECT_STATE.yaml`);
- an ADR only if a real decision is made (e.g. the scenario model would need to
  leave `CommandoWar.Sim`).

## Forbidden scope

- changing the shared fixture, `Setup.sixAgentWorld`, `FixtureTests.fs` hashes,
  `Canonical.encode` / its format version, or `cwheadless` behaviour;
- a per-cell terrain / cover / opacity model (B-008); line of sight (B-009);
  pathfinding (B-010);
- a Godot or Tiled importer or the content-validation CLI command (B-024);
- objective evaluation or mission logic; interaction / trigger scripting;
- a new dependency or a new project; touching the client spikes or
  `src/_scratch`; adding a client host to `CommandoWar.slnx`;
- destructive git.

## Required work

1. A framework-neutral `Scenario` model (F# records / DUs) carrying only what
   the Bridgehead slice needs: scenario id, map dimensions (width, height;
   elevation flat), friendly and enemy deployments (agent id, side, cell),
   objective definitions, extraction areas, static targets, and scenario rules.
2. A `ScenarioContentVersion` constant, independent of `Canonical.FormatVersion`
   and `Replay.FormatVersion`. Validation rejects an unsupported version with a
   typed error, in the `ReplayError` style.
3. An unvalidated "raw" input shape and
   `Scenario.validate : Raw -> Result<Scenario, ScenarioError list>` that
   reports every error in one pass: duplicate ids, deployment outside the map,
   deployment on a shared cell, objective referencing a missing area or entity,
   missing required marker, unknown class, unsupported content version. Each
   error names the offending object and the expected value. No silent defaults.
4. `World.ofScenario : Scenario -> uint64 -> Result<WorldState, WorldError>`,
   building the authoritative world from deployments (agents ordered ascending
   by id), reusing `World.create`. Per-cell terrain, line of sight, and
   pathfinding are not in scope.
5. The objective algebra: define the type for the minimal set the bridge
   mission needs. Objective evaluation stays deferred - type only, with a
   comment saying so.
6. Tests: a valid scenario -> `Scenario`; each invalid condition -> its
   specific typed error; the one-pass property; a pinning test tying the
   six-agent fixture to `0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`.
7. Register the new module(s) in `CommandoWar.Sim.fsproj`. Keep
   `TreatWarningsAsErrors` clean.

## Acceptance criteria

- [x] `Scenario` model carries scenario id, map dimensions, friendly/enemy
      deployments, objectives, extraction areas, static targets, and rules, and
      no field the slice does not exercise. `src/CommandoWar.Sim/Scenario.fs`
      `type Scenario`.
- [x] `ScenarioContent.Version` exists, is independent of
      `Canonical.FormatVersion` / `Replay.FormatVersion`, and an unsupported
      version is a typed error (`ScenarioError.UnsupportedContentVersion`) in
      the `ReplayError` style. Test: `an unsupported content version is a typed
      error`.
- [x] `Scenario.validate : RawScenario -> Result<Scenario, ScenarioError list>`
      reports every listed fault in one pass (`ScenarioError`, 20 cases). Test:
      `validation reports every fault in one pass` plus one test per condition.
- [x] `World.ofScenario : Scenario -> uint64 -> Result<WorldState, WorldError>`
      reuses `World.create` and orders agents ascending by id. Test:
      `World.ofScenario deploys friendly then enemy agents ordered ascending by
      id`.
- [x] The `Objective` algebra type exists (`ReachArea` / `HoldArea` /
      `DestroyTarget` / `ExtractAgents` / `AllOf` / `Optional`); evaluation
      deferred with a comment (B-032).
- [x] Tests: a valid scenario, one test per invalid condition, the one-pass
      property, and the fixture pinning test all pass (25 facts).
- [x] `dotnet test CommandoWar.slnx -c Release` = `Passed: 79` (54 + 25).
- [x] `cwheadless fixture` unchanged: initial `0xF2F3DF0D820AD9AC`, final
      `0x838D3AE7DBFB735D`.
- [x] `dotnet list ... package --include-transitive` = `FSharp.Core 10.1.303`
      only; source scan of `src/CommandoWar.Sim` for
      `godot|mibo|monogame|raylib|DateTime|Stopwatch|System.Random` matches
      only two doc-comment lines in `Scenario.fs` naming a future Godot content
      reader as prose.
- [x] `docs/04_SIMULATION_SPEC.md` section 21 added with a "Realised by
      TASK-008" note; `docs/06` section 3 realisation note added.
- [x] Backlog rows, ledger entry, `PROJECT_STATE.yaml`, and task status
      updated. No forbidden scope entered.

## Completion notes (2026-09-03)

`src/CommandoWar.Sim/Scenario.fs` (new, compiled after `Commands.fs`): content
ids in the `Ids.fs` idiom (`ScenarioId`, `ObjectiveId`, `AreaId`, `TargetId`),
`ScenarioContent.Version = 1`, the validated model (`Deployment`, `Area`,
`StaticTarget`, `AgentSelection`, `Objective`, `ScenarioRules`, `Scenario`),
the raw input (`RawDeployment`, `RawArea`, `RawTarget`, `RawObjective`,
`RawScenario`), `ScenarioError` (20 cases), and `Scenario.validate` (single
pass, sort-based de-duplication, `Set` for reference lookups, no silent
defaults). `World.ofScenario` added beside `World.create` in `Simulation.fs`.
No ADR: `docs/03` section 8 already places `Scenario.fs` in the sim. Fixture
hashes, `Canonical.encode`, `Setup.sixAgentWorld`, `FixtureTests.fs`, and
`cwheadless` are untouched. Evidence and exact commands in the 2026-09-03
TASK-008 ledger entry.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`
- `dotnet test CommandoWar.slnx -c Release` before and after
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
- source scan of `src/CommandoWar.Sim` for framework / wall-clock / RNG APIs

## Evidence to capture

- the `Scenario` / `RawScenario` / `ScenarioError` shapes and the version
  constant;
- a sample validation failure list;
- the pinning-test hashes;
- exact commands and results.

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md` TASK-008 / B-007 rows;
- `docs/12_PROGRESS_LEDGER.md` entry;
- `PROJECT_STATE.yaml` `active_work`;
- `docs/04_SIMULATION_SPEC.md`, `docs/06_CONTENT_AND_PRESENTATION.md`.

## Rollback or removal

`Scenario.fs` and `World.ofScenario` must be removable without changing or
deleting the shared fixture, `Canonical.fs`, `Simulation.step`, `Replay.fs`, or
their tests. The scenario model is isolated: nothing in the phase pipeline or
the canonical encoding depends on it.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Alternative

B-008 (terrain grid, elevation, passability, directional cover) depends on
B-007, so B-007 is genuinely next. If Dave would rather do client groundwork,
B-024 (the selected-framework content importer) also depends on B-007.
