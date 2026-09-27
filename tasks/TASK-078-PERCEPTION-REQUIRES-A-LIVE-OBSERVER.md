# TASK-078: Perception requires a live observer

Status: review
Owner: coding agent (Dave to accept)
Phase: P4
Gate: G4 (defect found by TASK-077); realises B-078
Size: S

## Objective

A non-`Alive` agent (`Incapacitated` or `Dead`) perceives nothing:
`Perception.visibleContactsFor` returns no contacts for it, so it emits no
`ContactObserved`, adds nothing to either side's shared tactical picture, and
no longer gains stress from contacts it "sees".

## Why this task exists

TASK-077 found, while building the Bridgehead replay corpus, that
`Perception.visibleContactsFor` checks the *target's* vitals (TASK-055) but
never the *observer's*. A corpse keeps "seeing": dead machine gunner 100
emits `ContactObserved` from tick 77 in `bridgehead-succeeded`, and on the
friendly side a dead or bleeding-out soldier keeps refreshing
`WorldState.TacticalKnowledge` at full confidence for as long as enemies stay
in its line of sight. That changes what `Appraisal` treats as a known threat,
and so which orders are refused. TASK-055 explicitly left the observer side
out of scope as "a different, unreported question"; it is now reported.
Dave selected it next when accepting TASK-077 ("fix the perception defect
next").

## Required reading

- `src/CommandoWar.Sim/Perception.fs` (`visibleContactsFor`, `sweep`)
- `src/CommandoWar.Sim/Simulation.fs` phases `perception`,
  `tacticalKnowledge`, `combat` (candidate selection), `stateConsequences`
  (stress gain)
- `tasks/TASK-055-PERCEPTION-STOPS-OBSERVING-DOWNED-AGENTS.md`
- `docs/04_SIMULATION_SPEC.md` section 16 ("dead or incapacitated agents do
  not start new actions"); `docs/05_COMMAND_AND_AGENT_AI.md` section 11
  (interrupt priority 1, "Dead or incapacitated")
- `tasks/TASK-077-*.md` finding 1

## Dependencies

- TASK-077 (done).

## Inputs and assumptions

- `Incapacitated` counts as not perceiving, the same `Casualty.isAlive`
  predicate TASK-055 used for targets. `docs/04` and `docs/05` group "dead or
  incapacitated" together throughout, and a bleeding-out soldier spotting
  for the squad is the same defect in a milder form.
- A downed agent's existing contacts in the shared picture are not deleted:
  they stop being refreshed and age out through the existing
  `StaleAfter`/`ExpireAfter` bands, the path TASK-055 used for a downed
  target.
- The agent goes down during combat (after perception) on tick *t*, so its
  own tick-*t* contacts still count; perception clears them from tick *t+1*.
- No new state, event, or format change: `Canonical.FormatVersion` stays 15.
  Hashes move only where a downed agent had something in view.

## Allowed scope

- `Perception.visibleContactsFor`: the observer-side vitals check.
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`: a focused fact.
- Re-pinning whatever the behaviour change legitimately moves: corpus tables,
  diagnostic goldens, the Godot `--selfcheck` constants in
  `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`, and the
  `DiagnosticsTests` expectations that pin them. Each moved pin must be
  explained, not just refreshed.
- If a `bridgehead-*` corpus entry no longer reaches its recorded outcome,
  re-author its schedule so it does, and record why.

## Forbidden scope

- The extraction-objective defect (TASK-077 finding 2), or any other
  mission/combat change.
- Any change to `Appraisal`, the knowledge-decay constants, or which targets
  are visible (TASK-055's rule stays as is).
- New `AgentState`/`WorldState` fields or format bumps.

## Required work

1. Add the observer check; add a fact that fails before the fix.
2. Regenerate the corpus and goldens; for every moved entry, state which
   downed agent's perception caused it.
3. Confirm `bridgehead-succeeded` still reaches `Succeeded` and
   `bridgehead-failed` still reaches `Failed`, and that the ford refusal still
   happens; recompute the Godot self-check hashes.
4. Record.

## Acceptance criteria

- [x] A non-`Alive` observer has empty `VisibleContacts` and emits no
      `ContactObserved`; a focused fact proves it and fails without the fix.
- [x] Every moved corpus table, golden, and self-check pin is explained.
- [x] Both Bridgehead corpus outcomes and the ford refusal still hold.
- [x] `dotnet test` and `cwheadless corpus` pass (CI on the pinned SDK):
      windows-latest, SDK 10.0.303, run `36340712686` on `57cffd9`: build 0 warnings / 0 errors, `Passed: 428, Failed: 0` (427 + the new fact), `corpus` 22/22, working tree clean after the verbs.
- [x] No forbidden scope entered the change.
- [x] Documentation updated.

## Required verification

- `dotnet build src/CommandoWar.Headless -c Release`; `-- corpus`;
  `-- corpus --regenerate` twice and diff
- the new fact and changed `DiagnosticsTests` facts (stand-in harness
  locally, since NuGet is blocked here; CI for the real run)
- Godot self-check hashes via `CommandoWar.Client.Godot.Core` (no Godot
  install needed; confirmed to reproduce the current pins first)
- `git diff --stat src/CommandoWar.Sim` limited to `Perception.fs`

## Evidence

### Change

`Perception.visibleContactsFor` returns `[||]` when `not (Casualty.isAlive
observer.Vitals)`; the rest of the function is unchanged (re-indented under
the guard). `sweep` and every consumer (the tactical-knowledge merge for both
sides, combat candidates, stress gain) pick it up with no further change.

New fact, `SimulationTests`: `an Incapacitated or Dead observer perceives
nothing and stops refreshing the squad picture` (both vitals cases; a
friendly downed from the start never observes an in-range, in-sight hostile;
one downed after seeing it leaves the squad contact un-refreshed at
`LastSeenTick = 1`). It fails on the pre-fix code (`Assert.Empty` on
`VisibleContacts`) and passes after.

### What moved, and why

For each moved corpus entry, the first re-pinned tick is exactly the first
tick at which some downed agent would have had a contact under the old rule
(checked by replaying each entry and evaluating the old predicate on every
downed agent):

| Entry | First moved tick | Downed observer (had in view) |
|---|---|---|
| `perception-contact` | 9 | agent 0 (1) |
| `exposed-approach` | 6 | agent 1 (2) |
| `suppress-relieves-exposure` | 5 | agent 1 (2) |
| `canonical-refusal-and-correction` | 5 | agent 1 (2) |
| `casualties-succession-and-squad-failure` | 4 | agent 3 (0, 1) |
| `bridgehead-succeeded` | 13 | machine gunner 100 (0, 4) |
| `bridgehead-failed` | 15 | agent 0 (100) |

An old-versus-new event diff of all seven (pre-fix code built from `HEAD` in
a scratch directory): every `MissionOutcome`, final tick, death,
incapacitation, objective completion, and refusal is identical. Five entries'
event streams are byte-identical; only canonical state moved (the downed
agent's `Stress`, and its side's picture no longer refreshed by it).
`bridgehead-succeeded` loses 9 `ContactObserved` events from dead 100/101/102
and the reappraisals they triggered (the appraisal phase's `knowledgeChanged`
fires on any `ContactObserved`/`ContactExpired`, either side); in their place
the hostile picture's corpse-refreshed entries now expire (ticks 142-144),
which triggers one reappraisal at tick 143. Every one of these reappraisals is
of an already-`Accepted` order and returns `Accepted` again. 482 -> 470
events. `bridgehead-failed` loses 2 corpse sightings, 364 -> 362 events. The
ford refusal (tick 6), its reappraisal to `Accepted` (tick 83), `Succeeded`
(tick 215), and `Failed` (tick 84) are all unchanged.

Goldens moved: `casualties-succession-and-squad-failure-tick-005`,
`canonical-refusal-and-correction-tick-008`, `bridgehead-succeeded-tick-215`,
`bridgehead-failed-tick-084` (ASCII and SVG each). Every ASCII diff is a
downed agent's `stress` line dropping or disappearing, a `hostile known
contact` line ageing or disappearing, and the footer hash; every agent whose
line changed is `incapacitated` or `dead` in that frame. `...-tick-065`
(everyone dead) and every other golden are byte-identical.

Godot self-checks, computed by calling `DemoDrive.runFullSequence` and
`CommandDemoDrive.runScriptedSelfCheck` from `CommandoWar.Client.Godot.Core`
(which has no Godot dependency) after first confirming they reproduce the
current pins: `DemoRenderScene` unchanged (`0x6213D672BC36FDB8`);
`CommandDemoScene` `0x84A25E3559111E9B` -> `0x300BB18492CEB355` (the machine
gunner, down from tick 12, stops perceiving; still dead by tick 71 with all six
friendlies alive at tick 90). Fixture, exposed-approach tick 1, and
chokepoint-detour self-checks involve no downed observer and are unaffected.

One existing test changed, not just re-pinned: `Stress accumulates over
continuous contact and decays once contact is lost` placed its hostile 3
cells away, inside weapon range, so the pair exchanged fire and agent 0 was
`Incapacitated` at tick 3; it reached its expected 250 only because the
bleeding-out agent kept gaining stress (old: 150/200/250 at ticks 3-5; new:
150/120/90). Moving the hostile to Chebyshev 9 (inside sight range 10,
outside weapon range 7) restores the test's own intent, live continuous
contact, and yields 250 under both old and new code.

### Verification

- Environment: same as TASK-077 (Ubuntu SDK 10.0.112, NuGet blocked).
- `dotnet build src/CommandoWar.Headless -c Release`: 0 warnings, 0 errors.
- `-- corpus --regenerate` twice: stable; seven tables changed as above
  (`chokepoint-detour.md`'s pre-existing prose drift again not copied back).
- `-- corpus`: 22/22.
- Stand-in test run: every test file except the three that need FsCheck
  (`DeterminismPropertyTests`, `ReplayTests`, `ScenarioFileTests`) and
  `BenchmarkTests` (xunit output-helper injection), compiled against a
  stand-in `Xunit` module and run by reflection. Baseline on pre-fix `HEAD`:
  372/372. After the fix, before re-pinning: 368 pass, 5 fail (four goldens,
  the stress test). After: 373/373.
- `git diff --stat src/CommandoWar.Sim`: `Perception.fs` only.

## Documentation updates

- this file; `docs/11_BACKLOG.md` B-078; `content/replays/CORPUS.md`
  re-pin note; `docs/12_PROGRESS_LEDGER.md` index row and detail file;
  `PROJECT_STATE.yaml`.

## Rollback or removal

Revert the one-line check and restore the previous pins from Git.
