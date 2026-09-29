## 2026-09-29 - TASK-079 - `ExtractAgents` requires at least one extracted agent

**Owner:** Dave with coding-agent assistance
**Source revision:** `2e075fd` (TASK-078 accepted, no task selected); Dave then
selected TASK-077's finding 2 ("Fix it"); implementation commit `136d4be` on
branch `claude/cool-hypatia-j4rswr`
**Environment:** Linux cloud container, .NET SDK `10.0.112` (Ubuntu archive;
pinned `10.0.303` and NuGet blocked by the session's network policy); CI:
windows-latest, .NET SDK `10.0.303`
**Status change:** `ready -> review`

### Changes

- `src/CommandoWar.Sim/Simulation.fs`: the `ExtractAgents` arm of the `mission`
  phase completes on `satisfied && anyExtracted`. A non-`Alive` required agent
  is still excused (TASK-062, unchanged), but at least one required agent must
  have `Extracted` set. Realises B-079 (TASK-077 finding 2).
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`: new fact `ExtractAgents does
  not complete when every required agent is non-Alive and none was
  extracted`.
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`: the `bridgehead-failed`
  fact, which pinned the vacuous completion as a recorded defect, now asserts
  `Failed` with `[1]` completed and no `objective-completed:3` marker.
- Re-pins: `content/replays/bridgehead-failed.md`;
  `content/diagnostics/bridgehead-failed-tick-084.{ascii.txt,svg}`.
- `docs/04_SIMULATION_SPEC.md`: the extraction sentence gains the "and at
  least one required agent has `Extracted` set" clause.

### What moved, and why

See `tasks/TASK-079-EXTRACTION-REQUIRES-AN-EXTRACTED-AGENT.md` Evidence. One
corpus entry diverges, `bridgehead-failed` from tick 84 (the `MissionFailed`
tick); 362 -> 361 events, ticks 84-86 re-hashed, outcome/tick/deaths/refusals
unchanged. The other 21 entries and both Godot self-check pins are
byte-identical.

### Commands and results

Local, from a copy of the tree without `global.json`, `-p:NuGetAudit=false`:

- Baseline on `HEAD`: `cwheadless corpus` `OK - all 22 entries`; stand-in
  suite `TOTAL pass=373 fail=0`.
- New fact added, guard not yet applied: `pass=373 fail=1` (the new fact).
- Guard applied, before re-pinning: `cwheadless corpus` `DIVERGED
  bridgehead-failed` (first bad tick 84); stand-in suite one failure (the
  `bridgehead-failed` diagnostics fact).
- `dotnet build src/CommandoWar.Headless -c Release`: `0 Warning(s)`,
  `0 Error(s)`.
- `cwheadless corpus --regenerate` run twice; `diff -r` of the two outputs
  identical (`STABLE`). Only `bridgehead-failed.md` copied back.
- After re-pinning: `cwheadless corpus` `OK - all 22 entries match their
  committed tables`; stand-in suite `TOTAL pass=374 fail=0`.
- Godot pins via `DemoDrive.runFullSequence` / `CommandDemoDrive.runScriptedSelfCheck`
  from `CommandoWar.Client.Godot.Core`: `DemoRenderScene` tick 20
  `0x6213D672BC36FDB8`, `CommandDemoScene` tick 90 `0x300BB18492CEB355`;
  both equal the committed pins.
- `git diff --stat src/CommandoWar.Sim`: `Simulation.fs` only. No package or
  reference added.
- CI on the pinned SDK (windows-latest, SDK 10.0.303, run `36576528593` on `136d4be`: build 0 warnings / 0 errors, `Passed: 429, Failed: 0` (428 + the new fact), `corpus` 22/22, working tree clean after the verbs).

### Findings (not fixed)

1. `SpecificAgents` naming an id with no agent counts as satisfied but not as
   extracted. Scenario validation does not reject such an id and no authored
   content uses one; with the new rule an objective whose only listed ids are
   unknown never completes (previously it completed vacuously). Recorded, not
   changed.
2. The stand-in suite excludes `DeterminismPropertyTests`, `ReplayTests`,
   `ScenarioFileTests` (FsCheck) and `BenchmarkTests`; those run only on CI.
3. TASK-077's remaining findings stand: `chokepoint-detour` description drift
   (cosmetic) and `PROJECT_STATE.yaml` not parsing as YAML.

### Documents updated

- `tasks/TASK-079-EXTRACTION-REQUIRES-AN-EXTRACTED-AGENT.md`
- `docs/11_BACKLOG.md` (B-079 -> `review`)
- `docs/04_SIMULATION_SPEC.md`; `docs/07_VERTICAL_SLICE.md` section 9;
  `content/replays/CORPUS.md`
- `PROJECT_STATE.yaml` (`active_work`)
- `docs/12_PROGRESS_LEDGER.md` (index row) and this file

### Review

- Reviewer: Dave
- Accepted: not yet (status `review`)
