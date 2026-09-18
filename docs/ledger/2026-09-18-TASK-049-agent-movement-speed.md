# 2026-09-18: TASK-049 agent movement speed

## Context

Backlog B-058, raised by Dave during TASK-046 review (2026-09-18): on open
ground `Terrain.BaseMoveCost = 1`, so an agent enters a new cell every tick
-- 20 cells/second at the client's 20Hz rate, which read as "twice as fast
as I thought it would." A client-only `simHz` halving was tried live and
works but slows every tick-denominated system uniformly (combat, decay,
bleed-out), not just walking; Dave explicitly deferred it: "let's leave it
for later, adding an actual speed to the agent would solve that
discrepancy." Selected this session over B-053/B-054 (small client-polish
rows) and a not-yet-filed Suppress-order UI slice: B-058 has been deferred
twice, has no dependencies, and affects every scene's pacing rather than one
screen.

## Central decisions (via `AskUserQuestion`, three rounds, 2026-09-18)

1. **Location: a per-unit-type stat table**, not a bare `AgentState` field
   (Dave's choice over the recommended smaller default).
2. **Static, non-canonical** (Dave's choice, matching the recommended
   default) — the `Discipline`/`CommunicationAvailable` precedent.
3. **Default reproduces today's pace exactly; only demo content gets
   slower** (Dave's choice, matching the recommended default).

## Implementation (2026-09-18)

`AgentState.MoveSpeed: int` (new, static, excluded from `Canonical.encode`)
and `Agent.MoveSpeedDefault = 2` (`Domain.fs`, the `DisciplineDefault`
literal precedent). Authored via a new `RawScenario.UnitTypes:
RawUnitType[]` table (`{ Id: string; MoveSpeed: int }`) referenced by each
`RawDeployment.UnitType`; `Scenario.validate` gains four new `ScenarioError`
cases (`BlankUnitTypeId`, `DuplicateUnitTypeId`,
`NonPositiveUnitTypeMoveSpeed`, `DeploymentReferencesUnknownUnitType`) and
rejects an unresolved reference outright — no silent default, the project's
existing validation philosophy. Only the resolved `Deployment.MoveSpeed`
scalar survives validation (the `RawTerrainCell.Class` -> `Terrain` baking
precedent); the table itself is discarded. `ScenarioContent.Version` bumps
3 -> 4.

`World.ofScenario` carries `Deployment.MoveSpeed` onto `AgentState.MoveSpeed`
(the `Discipline`/`CommunicationAvailable` precedent exactly).

**Design correction found mid-implementation.** The first `wouldComplete`
design scaled the per-tick *increment* directly by the mover's own
`MoveSpeed` (default agents got `Agent.MoveSpeedDefault` as their increment
instead of `Terrain.BaseMoveCost`), with the threshold scaled by the same
constant. This reproduces the *number of ticks* to complete any edge
correctly for a default-speed agent (the math cancels), which is why
`dotnet build` was clean and most of the suite passed — but it changes the
*stored* intermediate `AgentState.Progress` values on any multi-tick edge
crossing, since the increment itself is no longer `1`. Caught immediately
by `dotnet test`: `slow-terrain`'s corpus entry mismatched at tick 1, and
two `SimulationTests` facts pinning exact `Progress` values after N ticks
failed (`Expected: 1, Actual: 2`; `Expected: 2, Actual: 4`). Root cause:
`AgentState.Progress` is genuinely observed/pinned mid-edge (not just at
completion), so "same tick count" is a necessary but not sufficient
condition for behavioural neutrality.

Fixed by keeping the increment universal (`Terrain.BaseMoveCost` for every
agent, unconditionally) and cross-multiplying only the *comparison*:

```fsharp
let wouldComplete (next: Cell) (startProgress: int) (moveSpeed: int) =
    (startProgress + Terrain.BaseMoveCost) * moveSpeed
    >= Terrain.moveCost terrain next * Agent.MoveSpeedDefault
```

For `moveSpeed = Agent.MoveSpeedDefault` this reduces to the pre-TASK-049
comparison exactly (multiplying both sides of an inequality by the same
positive constant does not change it), so a default-speed agent's `Progress`
trajectory is now byte-for-byte identical to before at every tick, not just
at completion. Re-verified: `dotnet test` 342/342, `cwheadless corpus`
16/16, `corpus --regenerate` byte-identical.

`Corpus.fs`, `PathDemo.fs`, `LosDemo.fs` (all builder-authored content) each
author one implicit `"standard"` unit type at `Agent.MoveSpeedDefault` --
none of the 16 committed corpus/fixture entries needs a non-default speed.
`DemoScenario.fs` (the scenario both `SnapshotDemo.tscn` and
`CommandDemo.tscn` live-step) authors a `"trooper"` unit type at
`Agent.MoveSpeedDefault / 2` for all three of its agents — a genuine
per-agent slowdown, resolving the original B-058 complaint with no change
to `Terrain.BaseMoveCost` or any other content's authored speed.
`content/diagnostics/demo.html` regenerated (`cwheadless render demo
--format html`) to reflect the new, slower trace.

Five new `ScenarioTests` facts (a blank unit-type id, a duplicate id, a
non-positive `MoveSpeed`, a deployment referencing an unknown type, and a
bake-through fact proving `UnitType -> Deployment.MoveSpeed` resolves
correctly). One new `SimulationTests` fact
(`a half-speed agent takes twice as many ticks to cross an ordinary cell as
a default-speed agent`) proves the genuinely new behaviour end to end.

## Verification (2026-09-18)

```
$ dotnet build CommandoWar.slnx -c Release
Build succeeded. 0 Warning(s), 0 Error(s)

$ dotnet test -c Release
Passed! - Failed: 0, Passed: 342, Skipped: 0, Total: 342

$ dotnet run --project src/CommandoWar.Headless -c Release -- corpus
OK - all 16 entries match their committed tables

$ dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate
(git status --porcelain content/replays/ empty afterward -- byte-identical)

$ dotnet run --project src/CommandoWar.Headless -c Release -- replay-file content/replays/envelope-full.cwreplay
checkpoints  : OK (24 ticks match the file's committed hashes)

$ dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
FSharp.Core 10.1.303 only

$ dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug
Build succeeded. 0 Warning(s), 0 Error(s)
```

No Godot install in the implementing environment initially (the
TASK-039/047 precedent). Computed the new `SnapshotDemo.tscn` self-check
hash via `dotnet fsi` directly against the built
`CommandoWar.Client.Godot.Core.dll`:

```
DemoRenderScene final tick=20 hash=0xF422ACB8D5A86FF0   (was 0x8E93B48D07AE9CBD)
CommandDemoScene final tick=20 hash=0x00D3D471EF7354BC  (unchanged)
```

`CommandDemoScene`'s hash is unchanged because its scripted `MoveTo(3,0)`/
`Hold(2,1)` both complete within a handful of ticks even at half speed,
well inside the 20-tick self-check window, so the tick-20 rest state is
identical either way. `SnapshotDemo.tscn`'s hash moves because
`DemoScenario`'s own command log sends an agent on a longer route that does
not complete within 20 ticks even at full speed (it gets blocked partway,
a pre-existing, unrelated pathfinding/terrain interaction — confirmed
unrelated to this change: `MoveSpeed` only affects tick *pacing*, never
route validity, since `Pathfinding` never reads it); at half speed the
tick-20 snapshot simply catches it further back along the same route.
`FSharpSceneHost.cs`'s `RunSelfCheck` expected hash updated for
`DemoRenderScene`.

Dave then confirmed Godot 4.7.2 is installed at
`C:\Users\Dave\Documents\GitHub\Godot_v4.7.2-stable_mono_win64` (not
"unavailable" as prior sessions had assumed). Re-imported
(`--headless --path . --editor --quit`, clean, no errors) and ran
`--selfcheck` for all three scenes through the real editor:

```
$ "$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck
tick=20 hash=0xF422ACB8D5A86FF0
MATCH expected final hash 0xF422ACB8D5A86FF0 at tick 20

$ "$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck
tick=20 hash=0x00D3D471EF7354BC
MATCH expected final hash 0x00D3D471EF7354BC at tick 20

$ "$GODOT" --headless --path . scenes/AppraisalDemo.tscn -- --selfcheck
hash 0x194805888CBE240D (format 11)
MATCH expected hash 0x194805888CBE240D, agent 0 Refused / agent 1 Accepted
```

All three `MATCH` the `dotnet fsi` computation exactly, closing out the one
outstanding item from the first implementation pass.

`git status --porcelain`: modified
`src/CommandoWar.Sim/{Domain,Scenario,Simulation,Canonical}.fs`,
`src/CommandoWar.Headless/{Corpus,DemoScenario,PathDemo,LosDemo}.fs`,
`tests/CommandoWar.Sim.Tests/{ScenarioTests,SimulationTests}.fs`,
`content/diagnostics/demo.html`,
`src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`; new
`tasks/TASK-049-AGENT-MOVEMENT-SPEED.md` — matches the task file's allowed
scope.

### Evidence

- `dotnet build`/`dotnet test`/`-- corpus`/`-- corpus --regenerate`/
  `-- replay-file` command output above.
- `dotnet fsi` self-check probe output above, confirmed `MATCH` through the
  real Godot 4.7.2 editor for all three scenes.

### Deviations and unresolved issues

- The `wouldComplete` cross-multiplication design correction (see
  Implementation above) — found and fixed within this session, before
  presenting for review.
- `DemoScenario`'s agent 0 does not actually reach its authored
  `MoveTo (10, 6)` destination within the demo's 20-tick window at either
  speed (it gets blocked partway, at `(10, 2)`) — a pre-existing
  discrepancy against the file's own doc comment ("Agent 0 reaches (10, 6)
  at tick 16"), confirmed unrelated to this task (`MoveSpeed` cannot affect
  route validity) and out of this task's scope to fix.

### Documents updated

- `tasks/TASK-049-AGENT-MOVEMENT-SPEED.md` created, implemented, and
  self-verified (`drafted -> review`).
- `docs/11_BACKLOG.md`: B-058 row (`proposed -> review`).
- `docs/04_SIMULATION_SPEC.md`: movement section, section 21.
- `src/CommandoWar.Client.Godot/README.md`: new section.
- `docs/12_PROGRESS_LEDGER.md`: this row, `ScenarioContent.Version` pinned
  fact.
- `PROJECT_STATE.yaml`: `active_work` updated.

### Review

- Reviewer: Dave
- Accepted: pending.
