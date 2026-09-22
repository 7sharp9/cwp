# TASK-075: Re-verify and demonstrate the `Failed` mission direction on Bridgehead

Status: done
Owner: unassigned
Phase: P4
Gate: G4 (vertical slice feature-complete); realises B-075, closes the
`Failed`-direction half of docs/07_VERTICAL_SLICE.md section 9 criterion 7

## Objective

Determine, conclusively and with direct evidence (not inference from old
findings), whether `WorldState.MissionOutcome = Failed` is reachable on real
`content/scenarios/bridgehead.cwscenario` today, and either demonstrate it
end to end, or -- only if investigation proves a genuinely unreachable safe
pocket exists, not merely a play-skill gap -- make the smallest content
change that closes it. Update docs/07 section 9's criterion 7 record with
the re-verified finding either way.

## Why this task exists

TASK-064 (2026-09-20) found a full `Failed` run (all six friendly agents
eliminated) "geometrically capped well short of all six friendly agents by
the bridge's own two-lane chokepoint" -- friendlies simply couldn't all
become exposed at once. `docs/07_VERTICAL_SLICE.md` section 9's own
"Criterion 7 (`Succeeded` direction): met, with caveat" update (2026-09-21)
explicitly flagged this as unresolved: "The `Failed` direction was not
re-attempted this round and remains as TASK-064 left it ... not re-verified
against TASK-066/070's fixes." Since then, TASK-066 (a corpse no longer
blocks movement), TASK-070 (an agent now detours around a permanently parked
ally instead of stalling), and TASK-073 (a second ford plus a second
extraction cell) have all changed how agents move through Bridgehead's
contested terrain -- none of the three re-tested the `Failed` direction
specifically.

A dedicated research pass this session re-tested it directly against the
built `CommandoWar.Sim.dll` (a temporary `dotnet fsi` probe, the established
project precedent, removed after use): the old geometric cap is confirmed
gone -- a worst-case push (all six friendlies, no cover/suppression tactics)
reached **5 of 6 friendlies dead**, something TASK-064 explicitly found
impossible. The 6th survivor stalled at full health, having found a pocket
outside the remaining two riflemen's (`103` at `(17,3)`, `104` at `(17,8)`)
line of sight, and correctly **refused** (`RouteTooExposed`, TASK-072's own
mechanism working as intended) further pushes into their known engagement
zone. The research pass explicitly reported this as **inconclusive** on
full six-for-six reachability -- it did not exhaust the effort budget tuning
a smarter push sequence, and did not determine whether the last survivor's
safe pocket is genuinely unreachable by any order sequence or just required
more deliberate play than a single spammed probe attempted.

This task exists to settle that open question properly, the way TASK-064's
own investigation into the `Succeeded` direction was settled (deliberate,
non-obvious tactics found by further investigation, not a map rebalance) --
and to update the one piece of documentation (`docs/07` section 9) that
still names this as an open gap.

## Required reading

- `docs/07_VERTICAL_SLICE.md` section 9 in full, especially the original
  criterion-3/7 findings and the "Criterion 7 (`Succeeded` direction): met,
  with caveat -- update 2026-09-21" subsection (the exact "Failed direction
  ... not re-attempted" sentence this task addresses).
- `tasks/TASK-064-INTEGRATE-AND-VERIFY-VERTICAL-SLICE.md` (the original
  "geometrically capped" finding, full context).
- `docs/11_BACKLOG.md` B-071's row (the extraction-jam finding, resolved by
  B-073 -- relevant if this task's own demonstration run needs to extract
  survivors, though a `Failed` run by definition need not reach extraction).
- `src/CommandoWar.Sim/Simulation.fs`: the mission-evaluation function
  (search `MissionOutcome`, `friendlyForceEliminated`) -- confirm the exact
  `Failed` trigger (`Rules.FailOnFriendlyForceEliminated` and every friendly
  agent `not Casualty.isAlive`) directly from the code, not from this task
  file's paraphrase.
- `content/scenarios/bridgehead.cwscenario` in full (146 lines, already
  extensively documented in its own header comment block) -- especially
  riflemen `103`/`104`'s positions (`(17,3)`/`(17,8)`, deep in the two
  building clusters at `x=14-16,y=2-3`/`x=14-16,y=8-9`) and their cover
  (`terrain-cover 17 3 south 1`, `terrain-cover 17 8 north 1`).
- `src/CommandoWar.Sim/Sight.fs` and `src/CommandoWar.Sim/Appraisal.fs`
  (route-exposure stage, `cellPressure`) -- understand why the last survivor
  refused rather than assuming it is a bug: refusal here is the *correct*
  mechanism (TASK-072) protecting an agent from a known kill zone, not a
  malfunction to work around.

## Dependencies

- TASK-066, TASK-070, TASK-073 (all done -- the three fixes this task
  re-verifies against). No other task selected.

## Central decision

**Investigate first; only edit content if investigation proves it necessary.**
The primary path is a deliberate, non-spammed order sequence (mirroring
TASK-064's own successful `Succeeded`-direction investigation) that
systematically eliminates riflemen `103`/`104` in turn rather than relying on
a single broad push. Only pursue a `content/scenarios/bridgehead.cwscenario`
edit (e.g. repositioning `103`/`104` slightly closer to the central corridor,
or extending their effective coverage) if this investigation finds a
genuinely unreachable safe pocket -- a cell or set of cells with no possible
route that ever brings a friendly agent within both riflemen's combined
sightlines, regardless of tactics. Do not edit the scenario file
pre-emptively or as a first resort; this task's own research pass found the
6-of-6 gap plausibly closable through play alone, and an unnecessary content
edit risks destabilising the same file B-073 already tuned carefully (the
ford's LOS/range measurements, the extraction cells).

## Inputs and assumptions

- A `Failed` outcome, once reached, is not required to also complete any
  objective or extract -- `Rules.FailOnFriendlyForceEliminated` fires purely
  on universal friendly death, independent of objective/extraction state.
  Confirm this from `Simulation.fs` directly before relying on it.
- If a content edit does prove necessary, it must not change friendly
  starting positions, unit types, or formations, and must not touch
  `bridge-charge`/`mg-position`/`observation`/either extraction area's own
  cells or parameters (the B-073 precedent's own Forbidden-scope shape) --
  any change is confined to riflemen `103`/`104`'s position and/or cover
  parameters alone.
- `content-version` stays `7` unless `ScenarioFile.fs`'s own grammar changes
  (it will not, for a deployment-coordinate or cover-value edit) -- the
  B-073 precedent's own corrected finding applies identically here; confirm
  by inspection, do not assume a bump is needed.

## Allowed scope

- A temporary `dotnet fsi` probe (or several), removed after use, driving
  the real `Simulation.step` pipeline to investigate and demonstrate the
  `Failed` direction.
- `content/scenarios/bridgehead.cwscenario`: only riflemen `103`/`104`'s
  `enemy` deployment line(s) and/or their `terrain-cover` line(s), and only
  if Central decision's investigation proves it necessary.
- `docs/evidence/`: a screenshot or rendered diagnostic-frame evidence of
  the demonstrated `Failed` run.
- `docs/07_VERTICAL_SLICE.md` section 9: the criterion 7 `Failed`-direction
  update.

## Forbidden scope

- No `CommandoWar.Sim`/`CommandoWar.Headless` code change of any kind -- this
  is investigation plus, at most, a content edit.
- No change to friendly deployments, unit types, or formations.
- No change to `bridge-charge`/`mg-position`/`observation`/either
  `extraction-area`'s own cells or parameters.
- No weakening of the refusal mechanism (`Appraisal.fs`) to force a `Failed`
  result artificially -- the demonstration must go through ordinary,
  legitimate player-issued orders exactly as a real player would issue them,
  refusals and all. If refusal itself is what prevents 6-of-6 forever under
  any tactic, that is the honest finding to report, not an obstacle to
  route around.
- No corpus/fixture/`SimulationTests` change -- `bridgehead.cwscenario` is
  not referenced by any committed corpus entry (confirmed by TASK-073's own
  prior research); if this task finds otherwise, that is a new finding to
  report, not an assumption to silently correct around.

## Required work

1. Confirm the exact `Failed` trigger condition from `Simulation.fs`
   directly.
2. Drive a deliberate, staged order sequence (not a single broad push)
   against real Bridgehead content via a temporary `dotnet fsi` probe:
   eliminate threats progressively, expose each remaining friendly agent to
   riflemen `103`/`104`'s combined engagement zone in turn, over enough
   ticks to reach a stable outcome (a few hundred, matching this project's
   own past full-run precedent).
3. If 6-of-6 is reached: capture the evidence (event trace, final
   `MissionOutcome`, tick count) and proceed to Required work item 5.
4. If 6-of-6 is still not reached after genuine staged-tactic attempts:
   determine whether a truly unreachable safe pocket exists (no route from
   any reachable cell ever enters both riflemen's combined sightline) or
   whether further tactical variation was simply not exhausted; only then
   consider the smallest content edit (Central decision) and re-verify with
   a fresh probe.
5. Capture a live windowed Godot screenshot or equivalent diagnostic-frame
   evidence of the demonstrated `Failed` outcome (this environment has a
   real GPU-backed windowed Godot session available, confirmed this
   session -- use it directly).
6. Update `docs/07_VERTICAL_SLICE.md` section 9 with the re-verified
   finding, whichever way it lands -- report honestly if the answer remains
   inconclusive after a genuine effort, rather than forcing a result.
7. Update documentation per Documentation updates below.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] The exact `Failed` trigger condition is confirmed from `Simulation.fs`
      and cited with a line reference. (`Simulation.fs:2255-2257`/`2273-2274`:
      `friendlyForceEliminated = s.Rules.FailOnFriendlyForceEliminated && not
      (agents |> Array.exists (fun a -> a.Side = Friendly &&
      Casualty.isAlive a.Vitals))`, then `MissionOutcome <- Failed`.)
- [x] A direct probe demonstrates either a genuine 6-of-6 `Failed` outcome
      reached through ordinary, legitimate order sequencing, or a clearly
      documented, evidence-backed reason why it cannot be. **Reached**: a
      staged, two-order sequence (a broad six-agent Standard/Routine push,
      then an ordinary follow-up `MoveTo` for the two survivors straight
      onto riflemen 103/104's own cells) hit `MissionOutcome = Failed` at
      tick 416, state hash `0xE76F42B2E1AE4237`. No content edit was needed
      or made -- the Central decision's precondition for one (a genuinely
      unreachable pocket) was never met.
- [x] If a content edit was made, it is the smallest possible... -- **N/A,
      no content edit made**; `bridgehead.cwscenario` is byte-identical to
      the TASK-073 state.
- [x] `docs/07_VERTICAL_SLICE.md` section 9's criterion 7 record is updated
      with this task's finding.
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless` file touched. Confirmed by
      `git status`/`git diff` -- only `docs/`/`tasks/` files changed.
- [x] `dotnet build`/`dotnet test`/`-- corpus`: unaffected counts (`0
      Warning(s) 0 Error(s)` / `422/422` / `20/20`, all identical to the
      TASK-073 baseline).
- [x] All three Godot `--selfcheck` hashes unaffected -- no content edit was
      made, so no change was expected; confirmed by actually running all
      three (see Required verification below).
- [x] Required documentation updated (this task's own scope: this file,
      `docs/07_VERTICAL_SLICE.md` section 9, the new `docs/ledger/` detail
      file -- `docs/11_BACKLOG.md`/`docs/12_PROGRESS_LEDGER.md`'s
      index/`PROJECT_STATE.yaml` reconciled centrally per this task's own
      dispatch instructions).

## Required verification

- `dotnet build CommandoWar.slnx -c Debug`: `0 Warning(s)`, `0 Error(s)`.
- `dotnet test CommandoWar.slnx -c Debug`: `422/422` passed, unaffected.
- `dotnet run --project src/CommandoWar.Headless -c Debug -- corpus`: `OK -
  all 20 entries match their committed tables`, unaffected.
- `dotnet run --project src/CommandoWar.Headless -- import
  content/scenarios/bridgehead.cwscenario`: not run -- no scenario edit was
  made, so this criterion's own precondition ("only if the scenario file was
  edited") does not apply. The file's unchanged status is confirmed instead
  by `git status`/`git diff` showing zero difference from the TASK-073
  baseline.
- `"$GODOT" --headless --path src/CommandoWar.Client.Godot -- --selfcheck`
  (`CommandDemo.tscn`): `MATCH 0x84A25E3559111E9B` at tick 90 -- unchanged
  from the TASK-073 pin.
- `"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck`:
  `MATCH 0x6213D672BC36FDB8` at tick 20 -- unchanged.
- `"$GODOT" --headless --path . scenes/AppraisalDemo.tscn -- --selfcheck`:
  `MATCH 0xA1354EB998FC1B95`, agent 0 Refused / agent 1 Accepted --
  unchanged. All three confirm no content or code was touched by this
  investigation-only task.
- The temporary probes' own output (event trace, tick-by-tick casualty
  progression, final `MissionOutcome`), captured as evidence before
  deletion -- recorded in full in the ledger detail file.

## Evidence to capture

- The full order sequence used and why it was chosen: Stage 1 (tick 1) --
  a single simultaneous six-agent `MoveTo` push (Standard/Routine, no
  cover/suppression), targeting the machine-gun/rifleman-101/102 approach
  lanes (`(10,5)`/`(10,6)`/`(11,6)`/`(13,2)`) plus the two cells nearest
  riflemen 103/104 (`(16,4)`/`(16,7)`), chosen to reproduce this session's
  own prior research pass's "worst-case push" shape. This killed the
  machine gun (tick 71) and rifleman 102 (tick 78) and cost four of six
  friendlies (agents 0, 3, 4, 5; dead by tick 183), leaving agents 1 and 2
  alive and outside riflemen 103/104's combined engagement zone -- the same
  "safe survivor" shape the task's Why section describes. Stage 2 (tick
  401) -- an ordinary follow-up `MoveTo` sending each survivor directly onto
  the one remaining rifleman's own cell (`(17,8)`/`(17,3)`), chosen because
  the two riflemen were the only living threats left and their own cells
  are the shortest, most direct route into their engagement zone.
- The tick-by-tick casualty count and the final `MissionOutcome`: see the
  ledger detail file's full transcript; final state at tick 416 is
  `Failed`, agents 0/3/4/5 `Dead`, agents 1/2 `Incapacitated` (none
  `Alive`), riflemen 100/102 `Dead`, 101 `Incapacitated`, 103/104 still
  `Alive` (the fatal hits landed before either rifleman went down).
- Not inconclusive -- a genuine `Failed` outcome was reached and
  reproduced independently across three separate probe runs (an
  Aggressive/Immediate-envelope escalation variant and a plain
  Standard/Routine variant both reach the identical tick-416 outcome).
- The live/rendered evidence: `docs/evidence/task-075-bridgehead-failed-
  direction.png`, a `DiagnosticRender.Svg`-derived PNG of the tick-416
  frame (converted with ImageMagick) -- the TASK-073 precedent, used here
  because the live windowed Godot client's `CommandDemoScene` has no UI
  path for issuing a non-default command envelope or for replaying this
  investigation's specific multi-hundred-tick order sequence; a live
  capture would not actually demonstrate this finding faithfully.

## Expected files

- `content/scenarios/bridgehead.cwscenario` (only if a content edit proves
  necessary).
- `docs/07_VERTICAL_SLICE.md` (section 9 update).
- `docs/evidence/task-075-bridgehead-failed-direction.png` (or similar).

## Documentation updates

- this task's own status and evidence;
- `docs/07_VERTICAL_SLICE.md` section 9 (the Failed-direction finding);
- `docs/11_BACKLOG.md`: new B-075 row;
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
- `PROJECT_STATE.yaml` if the active task/phase/gate changes.

Reconciled centrally by the orchestrating session, not by this task's own
implementing agent, if dispatched alongside other parallel tasks.

## Rollback or removal

Investigation-only unless a content edit proves necessary; if one is made,
it is confined to two `enemy`/`terrain-cover` lines in one file, cleanly
`git revert`-able with no `CommandoWar.Sim` involvement.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-22, "accept all three, commit"), on the
  implementation-plus-independent-re-verification evidence recorded here and
  in `docs/ledger/2026-09-22-TASK-075-bridgehead-failed-direction-verification.md`
  (dispatched as one of three parallel implementation agents, each in an
  isolated worktree, and separately rebuilt/retested/re-`--selfcheck`ed by
  the orchestrating session -- including combined with TASK-074/TASK-076's
  own changes in the same tree -- before being reported as ready for
  review).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
