# TASK-049: Per-agent movement speed

Status: done (implemented and self-verified 2026-09-18; re-pinned Godot
`--selfcheck` hash independently confirmed `MATCH` through the real editor
2026-09-18; accepted by Dave 2026-09-18)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises backlog B-058

## Outcome (2026-09-18)

Implemented as drafted, with one correction found during implementation
(below). `CommandoWar.Sim` gains a real per-agent `AgentState.MoveSpeed`
(new, static authored data, the `Discipline`/`CommunicationAvailable`
precedent -- excluded from `Canonical.encode`, no `Canonical.FormatVersion`
bump). It is authored via a small unit-type table (Dave's chosen location
over a bare per-agent field, `AskUserQuestion`): `RawScenario.UnitTypes:
RawUnitType[]` (`{ Id; MoveSpeed }`), referenced by each `RawDeployment.
UnitType`; `Scenario.validate` rejects a blank or duplicate unit-type id, a
non-positive `MoveSpeed`, or a deployment referencing an undefined type (no
silent default, the project's own validation philosophy). Only the resolved
`Deployment.MoveSpeed` scalar survives validation -- the `RawTerrainCell.
Class` precedent: the table itself does not become part of `Scenario`.
`ScenarioContent.Version` bumps 3 -> 4.

`Simulation.navigationAndMovement`'s edge-completion check
(`wouldComplete`) is the one behavioural change, and it needed a real
design correction mid-implementation (see below): the per-tick progress
increment stays the universal `Terrain.BaseMoveCost` for every agent
regardless of speed (so `AgentState.Progress`'s stored trajectory is
exactly the elapsed real-tick sequence 0, 1, 2, ... it has always been);
only the completion *comparison* is cross-multiplied against a new
`Agent.MoveSpeedDefault` (= 2) constant: `(startProgress + Terrain.
BaseMoveCost) * MoveSpeed >= Terrain.moveCost next * Agent.MoveSpeedDefault`.
An agent at `MoveSpeed = Agent.MoveSpeedDefault` reproduces the
pre-TASK-049 comparison byte-for-byte (multiplying both sides of an
inequality by the same positive constant does not change it); a smaller
`MoveSpeed` (the "trooper" unit type below uses half) genuinely needs
proportionally more ticks to cross the same cell. `Pathfinding` is
unaffected -- it measures `Terrain.moveCost` only, never real-time ticks.

`content/diagnostics/demo.html`'s `DemoScenario` (the scenario both
`SnapshotDemo.tscn` and `CommandDemo.tscn` live-step) now authors a
`"trooper"` unit type at half `Agent.MoveSpeedDefault` for all three of its
agents -- a genuine per-agent slowdown, resolving Dave's "movement feels
twice as fast as I thought it would" complaint raised on TASK-046 review
(backlog B-058) without the client-only `simHz` halving or the shared
`Terrain.BaseMoveCost` bump both tried and reverted there (either would have
rippled every corpus entry's tick counts; this demo-local unit type touches
none of them). `content/diagnostics/demo.html` regenerated (`cwheadless
render demo --format html`) to reflect the new, slower trace.

**Correction found during implementation**: the first design scaled the
per-tick *increment* itself by `MoveSpeed` (a default agent's increment
became `Agent.MoveSpeedDefault` instead of `Terrain.BaseMoveCost`). This
kept tick *counts* to completion identical for a default-speed agent (the
math cancels), but changed the *stored* intermediate `AgentState.Progress`
values on any multi-tick edge crossing -- caught by `slow-terrain`'s corpus
mismatch and two `SimulationTests` failures (`dotnet test`). Fixed by
switching to the cross-multiplied comparison above, which keeps the
increment universal and only scales the threshold check; re-verified
byte-identical against all 16 corpus/fixture entries.

Every builder-authored corpus entry (`Corpus.fs`, `PathDemo.fs`,
`LosDemo.fs`, `ScenarioTests.fs`'s `goodRaw`/fixture-as-scenario helpers)
authors one implicit `"standard"` unit type at `Agent.MoveSpeedDefault` --
none of the 16 committed entries needs a non-default speed. Five new
`ScenarioTests` facts cover the four new validation errors plus the
`UnitType -> Deployment.MoveSpeed` bake-through; one new `SimulationTests`
fact proves a half-speed agent takes twice the ticks of a default-speed one
across an ordinary cell.

`dotnet build` `CommandoWar.slnx` (`-c Release`) `0/0`; `dotnet test`
`342/342` (+6); `cwheadless corpus` `16/16` unchanged; `corpus --regenerate`
byte-identical (`git status --porcelain content/replays/` empty);
`cwheadless replay-file envelope-full.cwreplay` unchanged (24 ticks, 78
events, checkpoints OK); `dotnet list CommandoWar.Sim package` `FSharp.Core`
only. `dotnet build` the Godot client `.slnx` (`-c Debug`) `0/0`.

Godot: `SnapshotDemo.tscn`'s `--selfcheck` hash moves (`DemoScenario`'s
agents are genuinely slower, so the tick-20 snapshot catches them mid-route
rather than past where they'd have been at the old pace) -- re-pinned
`0xF422ACB8D5A86FF0`, originally computed via `dotnet fsi` against the built
`CommandoWar.Client.Godot.Core.dll` (no Godot install in the implementing
environment, the TASK-039/047 precedent), then **independently confirmed
`MATCH` through the real Godot 4.7.2 editor** (Dave has Godot installed at
`C:\Users\Dave\Documents\GitHub\Godot_v4.7.2-stable_mono_win64`).
`CommandDemo.tscn`'s hash is unchanged (`0x00D3D471EF7354BC`, `MATCH`
reconfirmed): both its scripted orders complete within a handful of ticks
even at half speed, well inside the 20-tick window. `AppraisalDemoScene`
(`0x194805888CBE240D`, format 11) is unaffected and reconfirmed `MATCH` --
it steps the committed `exposed-approach` corpus entry, not `DemoScenario`.

Full detail: `docs/ledger/2026-09-18-TASK-049-agent-movement-speed.md`.

## Objective

Give agents a real per-agent movement speed, so a slower unit is possible
without the two workarounds already tried and reverted on TASK-046 review
(a client-only `simHz` halving; a shared `Terrain.BaseMoveCost` bump).

## Why this task exists

Backlog B-058, raised by Dave during TASK-046 review (2026-09-18): on open
ground `Terrain.BaseMoveCost = 1`, so an agent enters a new cell every tick
-- 20 cells/second at the client's 20Hz rate, which read as "twice as fast
as I thought it would." Dave explicitly deferred both workarounds tried
live and asked for "an actual speed on the agent" instead. Selected this
session over B-053/B-054 (small client-polish rows) and a not-yet-filed
Suppress-order UI slice, since it has been deferred twice, has no
dependencies, and affects every scene's pacing, not one screen.

## Central decisions (confirmed with Dave 2026-09-18 before drafting)

Three rounds, put via `AskUserQuestion`:

1. **Location: a per-unit-type stat table**, not a bare `AgentState` field
   (Dave's choice over the smaller default) -- a new authored
   `RawScenario.UnitTypes` table referenced by `RawDeployment.UnitType`,
   the `RawTerrainCell.Class` / `Terrain` baking precedent.
2. **Static, non-canonical** (Dave's choice, matching the recommended
   default) -- the `Discipline`/`CommunicationAvailable` precedent; a future
   dynamic-speed mechanic (wounds, terrain fatigue, suppression) can promote
   it later, as B-021 did for Discipline's own eventual dynamic-trait
   siblings.
3. **Default reproduces today's pace exactly; only demo content gets
   slower** (Dave's choice, matching the recommended default) -- every
   existing corpus/fixture entry stays behaviour-neutral; `DemoScenario`
   alone authors a slower unit type.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `decisions/ADR-0002*.md` (the static-authored-data-vs-canonical-image
  amendment; `Discipline`/`CommunicationAvailable` precedent)
- `src/CommandoWar.Sim/Domain.fs` (`AgentState.Discipline`/
  `.CommunicationAvailable`, `Agent.create`/`.DisciplineDefault`)
- `src/CommandoWar.Sim/Scenario.fs` (`RawTerrainCell.Class` -> `Terrain`
  baking; `RawDeployment`/`Deployment`; `Scenario.validate`'s "no silent
  default" philosophy)
- `src/CommandoWar.Sim/Simulation.fs` (`navigationAndMovement`,
  `wouldComplete`, the `MoveOutcome.Advancing` sub-cell progress mechanism,
  TASK-018)
- `src/CommandoWar.Headless/DemoScenario.fs`, `Corpus.fs` (the shared
  `ScenarioSpec` builder, TASK-036)
- `docs/04_SIMULATION_SPEC.md` section 21 (authored scenario/content
  version) and its movement/sub-cell-progress section (12.6 area)

## Dependencies

- None (backlog: `none`). No other task selected.

## Allowed scope

- `src/CommandoWar.Sim/Domain.fs`: `AgentState.MoveSpeed: int`,
  `Agent.MoveSpeedDefault`, `Agent.create` wiring.
- `src/CommandoWar.Sim/Scenario.fs`: `RawUnitType`, `RawScenario.UnitTypes`,
  `RawDeployment.UnitType`, `Deployment.MoveSpeed`, four new `ScenarioError`
  cases and their validation, `ScenarioContent.Version` 3 -> 4.
- `src/CommandoWar.Sim/Simulation.fs`: `World.ofScenario`'s deployment
  mapping; `navigationAndMovement`'s `wouldComplete` and its three call
  sites; doc-comment updates.
- `src/CommandoWar.Sim/Canonical.fs`: a doc-comment note (no code change --
  `MoveSpeed` is excluded, the `Discipline` precedent).
- `src/CommandoWar.Headless/Corpus.fs`, `DemoScenario.fs`, `PathDemo.fs`,
  `LosDemo.fs`: `UnitType`/`UnitTypes` wiring for every authored
  `RawDeployment`/`RawScenario`; `DemoScenario` additionally authors the
  slower `"trooper"` unit type.
- `content/diagnostics/demo.html` (regenerated golden).
- `tests/CommandoWar.Sim.Tests/ScenarioTests.fs`,
  `SimulationTests.fs`: compile-site field additions plus new facts for the
  unit-type validation errors, the bake-through, and the genuine
  half-speed-takes-twice-the-ticks behaviour.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: the re-pinned
  `SnapshotDemo.tscn` `--selfcheck` expected hash.
- `src/CommandoWar.Client.Godot/README.md` (new section, the TASK-041/046/048
  precedent).
- `docs/04_SIMULATION_SPEC.md` (movement section, section 21).
- `docs/11_BACKLOG.md` (B-058 row), `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- No change to `Terrain.BaseMoveCost`, `Terrain.MaxMoveCost`, or any
  existing corpus/fixture entry's authored speed (every one stays at
  `Agent.MoveSpeedDefault`, behaviour-neutral).
- No unit-type stats beyond `MoveSpeed` (no weapon/armor/vision stats --
  not asked for, avoid inventing a general unit-type system prematurely).
- No player-facing speed readout or UI (this is a Sim/content-authoring
  task, the `Suppress`/`Ammo` precedent for a mechanic landing without a
  client surface).
- No `CommandDemoScene`/`SnapshotDemo` input or rendering change beyond the
  `DemoScenario` content itself and the re-pinned expected hash.
- No content-import pipeline / `Greybox.tscn` / Bridgehead map change.
- Nothing under `src/_scratch/`, `bench/`, `content/benchmarks/BASELINE.md`.

## Required work

1. `AgentState.MoveSpeed` / `Agent.MoveSpeedDefault` / `Agent.create`.
2. `Scenario.fs`: `RawUnitType`, table validation, `Deployment.MoveSpeed`
   bake-through, `ScenarioContent.Version` bump.
3. `Simulation.fs`: cross-multiplied `wouldComplete`; verify byte-identical
   default-speed behaviour against every corpus entry before treating the
   design as settled (this is where the mid-implementation correction was
   caught).
4. Wire `UnitType`/`UnitTypes` through every existing `RawDeployment`/
   `RawScenario` construction site; give `DemoScenario` a slower `"trooper"`
   type.
5. Regenerate `content/diagnostics/demo.html`; compute the new Godot
   `--selfcheck` pin via `dotnet fsi` (no Godot install in this session).
6. New tests: unit-type validation errors, bake-through, genuine
   half-speed behaviour.
7. Verify: `dotnet build`/`test`/`corpus`/`corpus --regenerate`/
   `replay-file envelope-full`; Godot client `.slnx` build.
8. Update `README.md`, `docs/04`, backlog/ledger/state.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] `AgentState.MoveSpeed` exists, authored via a unit-type table, static
      and non-canonical.
- [x] Every one of the 16 committed corpus/fixture entries is
      behaviour-neutral (`cwheadless corpus` 16/16; `--regenerate`
      byte-identical).
- [x] A non-default `MoveSpeed` genuinely changes tick-by-tick pacing,
      proved by a dedicated `SimulationTests` fact.
- [x] `DemoScenario`'s agents move at half pace, fixing the "feels twice as
      fast" complaint, with no change to any other content's authored speed.
- [x] `dotnet test` green (342/342); `dotnet build` both `.slnx` (main;
      Godot client) `0/0`.
- [x] Godot `--selfcheck` hashes independently re-run through the real
      Godot 4.7.2 editor and confirmed `MATCH` (all three scenes, 2026-09-18).
- [x] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet test`: `342/342`.
- `dotnet run --project src/CommandoWar.Headless -- corpus`: `16/16`.
- `dotnet run --project src/CommandoWar.Headless -- corpus --regenerate`:
  byte-identical (`git status --porcelain content/replays/` empty).
- `dotnet run --project src/CommandoWar.Headless -- replay-file
  content/replays/envelope-full.cwreplay`: unchanged.
- `dotnet run --project src/CommandoWar.Headless -- render demo --format
  html --out content/diagnostics/demo.html`: regenerated golden committed.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package`:
  `FSharp.Core` only.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`: `0/0`.
- Godot editor `--selfcheck` for all three scenes through the real Godot
  4.7.2 editor: confirmed `MATCH` -- `SnapshotDemo.tscn`
  `0xF422ACB8D5A86FF0`, `CommandDemo.tscn` `0x00D3D471EF7354BC` (unchanged),
  `AppraisalDemo.tscn` `0x194805888CBE240D` (unchanged), all exit 0.
- `git status --porcelain`: matches this task's allowed scope.

## Documentation updates

- This task file's Outcome section.
- `src/CommandoWar.Client.Godot/README.md`.
- `docs/04_SIMULATION_SPEC.md` (movement section, section 21).
- `docs/11_BACKLOG.md` (B-058 row: proposed -> review, pending Dave's
  Godot re-check).
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Additive: one new `AgentState`/`Deployment` field pair (static,
non-canonical -- no hash-format change), one new authored content table
with its own validation, and a cross-multiplied comparison in
`navigationAndMovement` that is a no-op at the default speed (proved
byte-identical against every committed corpus/fixture entry). Revertible
with `git revert` in one step; `DemoScenario`'s slower pace and the
regenerated `demo.html`/Godot pin would revert with it.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-18). Godot `--selfcheck` re-run through the real
  editor confirmed `MATCH` before this acceptance; no further changes
  requested.
