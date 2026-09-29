# TASK-077: Bridgehead full-mission replay corpus entries (`Succeeded` and `Failed`)

Status: done (accepted by Dave 2026-09-27)
Owner: coding agent
Phase: P4
Gate: G4 (vertical slice feature-complete); realises B-077
Size: M

## Objective

Commit two replay-corpus entries that play the real
`content/scenarios/bridgehead.cwscenario` from tick 0 to a final
`MissionOutcome`, one `Succeeded` and one `Failed`, using only ordinary
player `MoveTo` orders, so that `cwheadless corpus` and `CorpusTests` re-verify
a completed mission's authoritative result on every build.

## Why this task exists

Every `docs/07_VERTICAL_SLICE.md` section 9 criterion is now recorded as met,
which leaves `docs/08_ROADMAP_AND_GATES.md` section 7's G4 evidence list as the
remaining step before a G4 gate decision. One G4 evidence bullet has no
committed proof: "replay of a completed mission reproduces its authoritative
result". Criterion 7's `Succeeded` run (TASK-064 follow-up, B-071) and
`Failed` run (TASK-075), and criterion 3's ford refusal (TASK-073), were all
demonstrated by temporary `dotnet fsi` probes that were removed after use. No
committed corpus entry references `bridgehead.cwscenario` (TASK-073 and
TASK-075 both confirmed this), so nothing in the build would notice if a later
change made the mission unwinnable or unlosable, or broke the ford refusal.

The same bullet list also asks that the mission "can be completed from a clean
build without developer commands". A corpus entry whose command stream holds
only `MoveTo` orders a player can issue through the client is direct evidence
for that bullet as well.

## Required reading

- `AGENTS.md`, `PROJECT_STATE.yaml`
- `docs/07_VERTICAL_SLICE.md` section 9 (criteria 3, 4, 7 updates)
- `docs/08_ROADMAP_AND_GATES.md` section 7 (G4 evidence)
- `content/replays/CORPUS.md`
- `src/CommandoWar.Headless/Corpus.fs` (the `demolition-success` bespoke-scenario
  precedent)
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` (`bridgeheadSeed`)
- `src/CommandoWar.Sim/Simulation.fs` `mission`
- `tests/CommandoWar.Sim.Tests/CorpusTests.fs`, `DiagnosticsTests.fs`
  (demolition-success golden facts)

## Dependencies

- TASK-073, TASK-075 (done). No other task selected.

## Inputs and assumptions

- Seed `20260920` (`CommandDemoScene.bridgeheadSeed`), so the corpus world is
  byte-identical to the one the play scene builds.
- The scenario text is read from the committed content file at build time, not
  copied into F#: a Bridgehead content edit then re-runs these entries and
  fails the corpus if the recorded outcome no longer holds. That is the point
  of the entries, and it means every future Bridgehead content edit must
  regenerate them.
- The order schedules are found by a temporary `dotnet fsi` probe (removed
  after use) and then pinned. They need not reproduce the probes of
  TASK-064/075 byte for byte; TASK-075 did not record its agent-to-target
  mapping.

## Allowed scope

- `src/CommandoWar.Headless/Corpus.fs`: two new entries.
- `src/CommandoWar.Headless/CommandoWar.Headless.fsproj`: embed
  `content/scenarios/bridgehead.cwscenario` as a resource so the entries work
  from `cwheadless`, the test assembly, and the Godot replay scene alike.
- `content/replays/`: the two new generated `.md`/`.cwreplay` pairs and
  `CORPUS.md` rows.
- `content/diagnostics/`: golden frames for the refusal tick and both outcome
  ticks, plus README rows; `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`
  facts pinning them.
- Documentation listed below.

## Forbidden scope

- No `CommandoWar.Sim` change and no `bridgehead.cwscenario` change.
- No fix for defects found while probing; record them for triage instead.
- No G4 gate decision: that is Dave's, on the full evidence list.
- No change to any existing corpus entry's table.

## Required work

1. Probe schedules against the real pipeline (`ScenarioFile.parse ->
   Scenario.validate -> World.ofScenario -> Simulation.step`).
2. Add the entries; regenerate the corpus; confirm every existing entry is
   byte-identical.
3. Add diagnostic goldens and facts for the key ticks.
4. Record findings, commands, and results.

## Acceptance criteria

- [x] `bridgehead-succeeded` replays the real Bridgehead content to
      `MissionOutcome = Succeeded`, and includes a genuine
      `Refused(RouteTooExposed)` before contact (criterion 3) that later
      flips to `Accepted` when the threat is removed (criterion 4).
- [x] `bridgehead-failed` replays the real Bridgehead content to
      `MissionOutcome = Failed`.
- [x] Both command streams contain only `MoveTo` orders.
- [x] `cwheadless corpus` passes for all 22 entries; the 20 existing tables
      are byte-identical after `--regenerate`.
- [x] Golden frames for the refusal, `Succeeded`, and `Failed` ticks exist and
      match fresh renderer output.
- [x] `dotnet test CommandoWar.slnx -c Release` passes: not runnable in this
      environment (NuGet blocked), run by CI on push instead -- windows-latest,
      SDK 10.0.303, run `36339461637` on `4aabb0b`: `Passed: 427, Failed: 0`
      (422 + 2 `CorpusTests` theory rows + 3 `DiagnosticsTests` facts),
      `corpus` 22/22, build 0 warnings / 0 errors.
- [x] No forbidden dependency or scope entered the change.
- [x] Required documentation updated.

## Required verification

- `dotnet build src/CommandoWar.Headless -c Release`
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
- `-- corpus --regenerate` twice, `git diff --stat` on `content/replays`
- a scratch `dotnet fsi` check that the goldens equal fresh
  `DiagnosticRender.Ascii`/`Svg` output for the pinned frames
- `dotnet test CommandoWar.slnx -c Release` (CI)
- dependency boundary: `git diff --stat src/CommandoWar.Sim` empty

## Evidence

### Environment

Linux cloud container, Ubuntu 24.04. The pinned SDK (10.0.303) and every NuGet
host (`dot.net`, `builds.dotnet.microsoft.com`, `api.nuget.org`) are refused by
the environment's egress policy. The Ubuntu archive's `dotnet-sdk-10.0`
(10.0.112, bundled FSharp.Core 10.0.112) was installed instead and the
solution was built from a scratch copy without `global.json` (which pins the
3xx feature band), with `-p:NuGetAudit=false`. `CommandoWar.Sim` and
`CommandoWar.Headless` have no NuGet dependencies and build cleanly; the xunit
test project cannot restore.

Toolchain check before any change: `cwheadless corpus` reproduced all 20
committed tables (pinned on Windows x64 / SDK 10.0.303) exactly, so this
toolchain produces the same authoritative hashes for the existing corpus.

### Schedules found

`bridgehead-succeeded` (17 `MoveTo` commands, outcome tick 215):

| Tick | Orders | What happens |
|---|---|---|
| 1 | agents 0/1 -> (8,5), 2/4 -> (8,6), 3 -> (9,6) (TASK-064's scripted-self-check bridge push); agent 5 -> (5,9) (the ford stand-off cell) | machine gun 100 incapacitated tick 12, dead tick 71, no friendly loss |
| 6 | agent 5 -> (12,9) | `Refused(RouteTooExposed(Some 102))` the same tick, zero shots; reappraised and still refused through tick 81 |
| 75 | agent 0 -> (10,5), 4 -> (10,6), 2 -> (11,6) | the B-071 engagement cells: riflemen 102 (incapacitated 82) and 101 (84) go down, at the cost of agents 0 and 4 |
| 83 | (none) | agent 5's standing tick-6 order is reappraised `Accepted` once 102 is down, and it advances to (12,9) |
| 150 | agent 1 -> (9,5) | 10-tick plant, `ObjectiveCompleted 2` tick 160 |
| 165 | agent 1 -> (4,9), 3 -> (1,9) | extracted ticks 182/186 |
| 195 | agent 1 -> (5,8), 3 -> (2,8) | step off the extraction cells |
| 196 | agent 2 -> (4,9), 5 -> (1,9) | agent 5 extracted passing through (4,9) at 211, agent 2 at 215; `ObjectiveCompleted 3`, `MissionSucceeded` tick 215 |

`bridgehead-failed` (6 `MoveTo` commands, outcome tick 84): a single reckless
frontal charge at tick 1, agent 0 -> (16,4), 1 -> (10,6), 2 -> (11,6),
3 -> (16,7), 4 -> (13,2), 5 -> (10,5), the same target set as TASK-075's probe
3/7. Several orders are refused mid-route once threats are known
(`RouteTooExposed` against 100, 101, 102) but those agents are already inside
weapon range; every friendly is down by tick 84 and `MissionFailed` fires.
Chosen over a TASK-075-shaped two-wave run because it is shorter; a search of
all 720 agent-to-target mappings of that target set found 46 that reach
`Failed`, none reproducing TASK-075's exact hash (its mapping and command ids
were not recorded).

### Defects found while probing (not fixed; for G4 triage)

1. Perception ignores the observer's own vitals.
   `Perception.visibleContactsFor` excludes a non-alive *target* (TASK-055) but
   never checks the *observer*, so a dead or incapacitated agent keeps emitting
   `ContactObserved` and, on the friendly side, keeps feeding
   `WorldState.TacticalKnowledge` as a permanent sensor. Seen in both runs
   (dead machine gunner 100 "observing" friendlies from tick 77 on).
   Proposed severity: medium (it changes what the squad knows, and so what
   `Appraisal` refuses).
2. The extraction objective completes vacuously when the whole squad is down.
   `Simulation.mission`'s `ExtractAgents` check treats a non-alive agent as
   satisfied, so in `bridgehead-failed` `ObjectiveCompleted 3` fires in the same
   tick as `MissionFailed`. The outcome is still correctly `Failed`
   (`friendlyForceEliminated` is checked first), but `CompletedObjectives` lists
   an extraction that never happened. Proposed severity: low.

Both are pinned as current behaviour in the new entries; a fix re-pins them.

3. Pre-existing, cosmetic: `Corpus.fs`'s `chokepoint-detour` description says
   "reaches (4,0) for real by tick 7" while the committed
   `content/replays/chokepoint-detour.md` says "by tick 6", so
   `corpus --regenerate` rewrites that file's prose. Hashes are unaffected;
   left alone per Forbidden scope.

## Documentation updates

- this file; `docs/11_BACKLOG.md` B-077; `content/replays/CORPUS.md`;
  `content/diagnostics/README.md`; `docs/07_VERTICAL_SLICE.md` section 9;
  `docs/12_PROGRESS_LEDGER.md` index row and detail file; `PROJECT_STATE.yaml`.

## Rollback or removal

Delete the two `Corpus.all` entries, the `EmbeddedResource` item, their
`content/replays` and `content/diagnostics` files, and the matching
`DiagnosticsTests` facts. Nothing in the simulation depends on them.
