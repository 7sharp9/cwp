# TASK-079: `ExtractAgents` requires at least one extracted agent

Status: review
Owner: coding agent (Dave to accept)
Phase: P4
Gate: G4 (defect found by TASK-077, finding 2); realises B-079
Size: S

## Objective

An `ExtractAgents` objective no longer completes when every required agent is
non-`Alive` and none was extracted. It completes only when every required,
still-`Alive` agent is `Extracted` **and** at least one required agent has
`Extracted` set.

## Why this task exists

TASK-077 found that `Simulation.mission` treats a non-`Alive` required agent as
satisfied (`docs/04` section "evaluate objective conditions": excluded from the
requirement, not a blocker). That exclusion is intended and stays. The gap is
the corner where *every* required agent is down: the requirement is then empty,
`Array.forall` over it is `true`, and `ObjectiveCompleted` fires with nobody
extracted. With `FailOnFriendlyForceEliminated` on, `Failed` still wins and
only `CompletedObjectives` is wrong (`bridgehead-failed` listed objective 3 at
tick 84). With the rule off and extraction as the only objective, a total wipe
would report `Succeeded`. TASK-077 proposed severity low (no shipped content
disables the rule); Dave asked for it to be fixed ("Fix it", 2026-09-29) after
accepting TASK-078.

## Required reading

- `src/CommandoWar.Sim/Simulation.fs` `mission` phase (`ExtractAgents` arm and
  the outcome block that follows)
- `docs/04_SIMULATION_SPEC.md` "evaluate objective conditions"
- `tasks/TASK-062-DEMOLITION-OBJECTIVE-EXTRACTION-AND-MISSION-OUTCOME.md`
- `tasks/TASK-077-*.md` finding 2

## Dependencies

- TASK-078 (done).

## Inputs and assumptions

- "At least one required agent extracted" is the smallest rule that closes the
  hole and keeps TASK-062's exclusion of downed agents: `Extracted` is sticky,
  so an extraction that happened is never revoked by a later casualty.
- An id in `SpecificAgents` that names no agent still counts as satisfied but
  not as extracted (unchanged validation surface; no authored content does
  this).
- No new state, event, or format change: `Canonical.FormatVersion` stays 15.
  Hashes move only where an extraction objective was completing vacuously.

## Allowed scope

- The `ExtractAgents` arm of `Simulation.mission`.
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`: a focused fact.
- The `docs/04` sentence describing the rule.
- Re-pinning what the change legitimately moves (corpus tables, diagnostic
  goldens, the `DiagnosticsTests` assertion that pinned the old behaviour),
  each explained.

## Forbidden scope

- The other objective kinds, the failure rule, the outcome latch, or any
  combat/perception change.
- New `AgentState`/`WorldState` fields or format bumps.
- The `chokepoint-detour` prose drift (TASK-077 finding 3).

## Required work

1. Add the guard; add a fact that fails before the fix.
2. Regenerate the corpus and goldens; explain every moved pin.
3. Confirm the Godot self-check pins and both Bridgehead outcomes hold.
4. Record.

## Acceptance criteria

- [x] With every required agent non-`Alive` and none extracted, the objective
      does not complete (mission stays `InProgress` with the fail rule off;
      `Failed` with an empty `CompletedObjectives` with it on); a focused fact
      proves it and fails without the fix.
- [x] The existing exclusion of a downed agent alongside an extracted one still
      holds (`ExtractAgents excludes a non-Alive agent from its requirement`).
- [x] Every moved corpus table and golden is explained; the Godot self-check
      pins are unchanged.
- [x] `dotnet test` and `cwheadless corpus` pass on CI (pinned SDK):
      windows-latest, SDK 10.0.303, run `36576528593` on `136d4be`: build 0 warnings / 0 errors, `Passed: 429, Failed: 0` (428 + the new fact), `corpus` 22/22, working tree clean after the verbs.
- [x] No forbidden scope entered the change.
- [x] Documentation updated.

## Required verification

- `dotnet build src/CommandoWar.Headless -c Release`; `-- corpus`;
  `-- corpus --regenerate` twice and diff
- the new fact and the changed `DiagnosticsTests` fact (stand-in harness
  locally, since NuGet is blocked here; CI for the real run)
- Godot self-check hashes via `CommandoWar.Client.Godot.Core`
- `git diff --stat src/CommandoWar.Sim` limited to `Simulation.fs`

## Evidence

### Change

In the `ExtractAgents` arm of `Simulation.mission`, completion now requires
`satisfied && anyExtracted`, where `anyExtracted` is true when some required
agent has `Extracted` set (an unknown id counts as not extracted). Nothing
else in the phase changes.

New fact, `SimulationTests`: `ExtractAgents does not complete when every
required agent is non-Alive and none was extracted` (a `Dead` and an
`Incapacitated` friendly; `AllFriendlyAgents` and `SpecificAgents` variants;
fail rule off leaves `CompletedObjectives` empty, no `ObjectiveCompleted`, and
`MissionOutcome = InProgress`; fail rule on gives `Failed` with empty
`CompletedObjectives`). It fails on the pre-fix code (first assertion) and
passes after.

### What moved, and why

`cwheadless corpus` on the fixed build diverges on exactly one entry:
`bridgehead-failed`, first bad tick 84 (the tick of `MissionFailed`, where the
vacuous `ObjectiveCompleted 3` used to fire). Regenerated tables change ticks
84-86 only; events 362 -> 361 (the one `ObjectiveCompleted 3`); final hash
`0xE2411D3A4F4EE4C4` -> `0x298CDA351D4244C4`; outcome, final tick 86, deaths,
and refusals unchanged. The other 21 entries, including `bridgehead-succeeded`
(which genuinely extracts, `Succeeded` at 215) and `demolition-success`, are
byte-identical.

Goldens moved: `bridgehead-failed-tick-084` (ASCII and SVG). The diff is only
`mission: failed  completed 1,3` -> `completed 1`, the
`objective-completed:3` event marker disappearing, the event count 7 -> 6, and
the hash. Every other golden is byte-identical.

`DiagnosticsTests`: the `bridgehead-failed` fact asserted the old behaviour as
"recorded as a defect"; it now asserts `Failed` with `[1]` completed and no
`objective-completed:3` marker.

Godot self-checks (computed via `CommandoWar.Client.Godot.Core` on the fixed
build): `DemoRenderScene` `0x6213D672BC36FDB8` and `CommandDemoScene`
`0x300BB18492CEB355`, both unchanged.

### Verification

- Environment: same as TASK-077/078 (Ubuntu SDK 10.0.112, NuGet blocked; build
  from a copy of the tree without `global.json`, `-p:NuGetAudit=false`).
- Baseline on `HEAD` (before any change): corpus 22/22; stand-in suite 373/373.
- New fact before the fix: 373 pass, 1 fail (the new fact).
- After the fix, before re-pinning: 1 fail (`bridgehead-failed` diagnostics
  fact); corpus diverged on `bridgehead-failed` only.
- `dotnet build src/CommandoWar.Headless -c Release`: 0 warnings, 0 errors.
- `-- corpus --regenerate` twice: stable; only `bridgehead-failed.md` copied
  back (`chokepoint-detour.md`'s pre-existing prose drift not copied).
- After re-pinning: `-- corpus` 22/22; stand-in suite 374/374 (373 + the new
  fact; the stand-in excludes the FsCheck/`BenchmarkTests` files, as in
  TASK-078).
- `git diff --stat src/CommandoWar.Sim`: `Simulation.fs` only.
- CI on the pinned SDK: windows-latest, SDK 10.0.303, run `36576528593` on `136d4be`: build 0 warnings / 0 errors, `Passed: 429, Failed: 0` (428 + the new fact), `corpus` 22/22, working tree clean after the verbs.

## Documentation updates

- this file; `docs/11_BACKLOG.md` B-079; `docs/04_SIMULATION_SPEC.md`
  extraction sentence; `docs/07_VERTICAL_SLICE.md` section 9 note;
  `content/replays/CORPUS.md` re-pin note and `bridgehead-failed` row;
  `docs/12_PROGRESS_LEDGER.md` index row and detail file;
  `PROJECT_STATE.yaml`.

## Rollback or removal

Revert the `anyExtracted` guard and restore the previous pins from Git.
