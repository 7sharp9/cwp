## 2026-09-22 - TASK-075 - Re-verify and demonstrate the `Failed` mission direction on Bridgehead

**Owner:** Dave
**Source revision:** `main` at `e22a20c` (TASK-072/073 accepted), fast-forwarded
into this worktree before any other change
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot `4.7.2-stable_mono_win64`
(windowed session available; not used directly -- see Deviations)
**Status change:** `ready -> review` (self-verified)

### Changes

Investigation-only; **no `content/scenarios/bridgehead.cwscenario` edit was
made**. Realises B-075, closing the `Failed`-direction half of
`docs/07_VERTICAL_SLICE.md` section 9 criterion 7 that the 2026-09-21
"Criterion 7 (`Succeeded` direction)" update left open ("The `Failed`
direction was not re-attempted this round").

1. Confirmed the exact `Failed` trigger directly from `Simulation.fs`
   (Required work item 1): lines 2255-2257 compute
   `friendlyForceEliminated = s.Rules.FailOnFriendlyForceEliminated && not
   (agents |> Array.exists (fun a -> a.Side = Friendly &&
   Casualty.isAlive a.Vitals))`; lines 2273-2274 then set `s.MissionOutcome
   <- Failed` and emit `MissionFailed`. `Casualty.isAlive` (`Casualty.fs:
   88-92`) is `true` only for `Alive _` -- `Incapacitated` (mid bleed-out)
   and `Dead` both count as "eliminated" for this check, confirming the
   task file's own "Inputs and assumptions": `Failed` fires purely on every
   Friendly agent leaving `Alive`, independent of objective/extraction
   state, and does not require every agent to be literally `Vitals = Dead`
   at the moment it fires.
2. A series of temporary `dotnet fsi` probes (in the session scratchpad,
   removed after use -- this session's own established precedent for this
   investigation, matching TASK-064/073's prior use of the same technique)
   drove the real `ScenarioFile.parse -> Scenario.validate ->
   World.ofScenario -> Simulation.step` pipeline directly against
   unmodified `bridgehead.cwscenario` (seed `20260920UL`, the
   `CommandDemoScene.bridgeheadSeed` precedent).
3. `docs/07_VERTICAL_SLICE.md` section 9 gained a new "Criterion 7 (`Failed`
   direction): met" subsection recording the finding below.

### Investigation

**Probe 1 (static LOS sweep).** Before attempting any order sequence,
mapped exactly which cells are outside both riflemen 103 (`(17,3)`)/104
(`(17,8)`)'s combined `Sight.visible`/`ThreatEngagementRange` (8) coverage,
using `Sight.visible`/`Perception.chebyshev` directly against
`WorldState.Terrain`. Along the open corridor between the two building
clusters (`x=14..17, y=4..7`), exactly two cells are shadowed from both --
`(14,4)` and `(14,7)` -- shielded by the crate at `(17,5)` blocking the
riflemen's own line-of-sight past each other, and by the building walls
blocking the west approach. Every cell adjacent to these two pocket cells
(`(15,4)`/`(16,4)`/`(15,7)`/`(16,7)`, etc.) is visible to at least one
rifleman with full, uncovered `ExposedCellWeight` (10) pressure (no
`terrain-cover` is authored anywhere in this band). This matches, and gives
a precise geometric account of, the "safe pocket" the task's Why section
describes a prior research pass finding empirically. Critically, this sweep
found no cell within engagement range of Bridgehead's depot that is
shielded from both riflemen except these two narrow shadow cells -- the
pocket is real but is a firing-line shadow, not a genuinely unreachable
island; nothing about its geometry prevents a friendly agent from later
stepping out of it.

**Probe 2 (two-wave staged push).** A first attempt split the push into two
temporally separated waves (kill the machine gun/101/102 first, then
separately push two more agents at 103/104). This let the friendlies
actually **win** the second engagement -- agents 3 and 4 died, but so did
riflemen 103 and 104, in a roughly even 1v1 exchange -- leaving three
friendlies alive and zero living threats, making `Failed` permanently
unreachable from that state. Recorded as a real, informative negative
result: staggering threats one at a time does not reproduce the reported
"threats survive, friendlies die" asymmetry; it was **only reproduced** by
sending overwhelming numbers at fewer threats simultaneously (probe 3).

**Probe 3 (single simultaneous six-agent push + escalation).** A single
`MoveTo` order for all six agents at tick 1 (Standard/Routine, no cover or
suppression), targeting `(10,5)`/`(10,6)`/`(11,6)`/`(13,2)` (machine
gun/101/102's approach lanes) and `(16,4)`/`(16,7)` (adjacent to
103/104), reproduced the reported asymmetry: the machine gun (tick 71) and
rifleman 102 (tick 78) died, and four of six friendlies died in the
process (agents 0, 3, 4, 5 -- dead by tick 183), leaving agents 1 and 2
alive at tick 400, outside 103/104's engagement zone, with rifleman 101
alive too. A follow-up escalation round -- re-ordering the two survivors
directly onto `(17,3)`/`(17,8)` using `RiskTolerance.Aggressive`/
`Urgency.Immediate` (ordinary `PlayerCommand` envelope fields, not a
mechanism change) -- reached `MissionOutcome = Failed` at **tick 416**,
state hash `0x4B94440E449E4D2D` (`Format = 15`).

**Probe 5/6 (isolating what actually did the work).** Inspecting
`WorldState.TacticalKnowledge` at the tick-400 branch point found riflemen
103/104 were **not yet known contacts at all** (only rifleman 101 was, from
the first wave) -- so the Aggressive/Immediate escalation's higher resolve
threshold was never actually tested against a real refusal at that point;
an identical Standard/Routine order was accepted too. Re-running two
independent branches from the identical tick-400 state -- one
Standard/Routine, one Aggressive/Immediate, both ordering agents 1/2
straight onto `(17,8)`/`(17,3)` -- found **both reach `MissionOutcome =
Failed` at the identical tick 416**. The Standard/Routine branch does
produce a genuine `Refused(RouteTooExposed(Some AgentId 103|104))` for both
agents, but only at tick 414, one tick after `ContactObserved` (tick 413)
already put them on a `Pathfinding`-found, threat-blind route already
within weapon range -- `Combat`'s own independent within-range check lands
the fatal hit the same tick contact is established, before the reappraisal
can turn the agent back. This is the **identical one-tick contact/
reappraisal lag TASK-073 already found and documented** for a single
un-staged `MoveTo` order deep into the depot ("Pathfinding is deliberately
threat-blind... a one-tick lag between a new contact being observed and the
Appraisal phase's reappraisal reacting to it... Combat's own independent
within-range check already fires that same tick"), now confirmed to apply
symmetrically in the `Failed` direction. The refusal mechanism is real and
does fire -- it is simply, mechanically, one tick too late once the agent
is already on a fatal route, exactly as already documented; nothing was
bypassed, weakened, or gamed.

**Probe 7 (final, cleanest reproduction, used for evidence).** Re-ran the
whole sequence using **only** ordinary Standard/Routine `MoveTo` orders
throughout (no Aggressive/Immediate at all, since probe 6 showed it is not
actually load-bearing): the identical six-agent wave, then an ordinary
follow-up `MoveTo` for agents 1 and 2 onto `(17,8)`/`(17,3)`. Reached
`MissionOutcome = Failed` at tick 416, state hash `0xE76F42B2E1AE4237`
(`Format = 15`) -- a fully ordinary, legitimate two-stage order sequence any
player could issue through the existing `MoveTo` command, no non-default
envelope needed.

### Verification

- `dotnet build CommandoWar.slnx -c Debug`: `0 Warning(s)`, `0 Error(s)`.
- `dotnet test CommandoWar.slnx -c Debug`: `422/422` passed, unaffected
  (unchanged from the TASK-073 baseline -- no `CommandoWar.Sim` file
  touched).
- `dotnet run --project src/CommandoWar.Headless -c Debug -- corpus`: `OK -
  all 20 entries match their committed tables`, unaffected.
- `git status --porcelain` / `git diff --stat`: `content/scenarios/
  bridgehead.cwscenario` shows **zero** difference from the TASK-073 state
  -- confirms no content edit was made, satisfying this session's own
  Central decision (only edit content if investigation proves a genuinely
  unreachable pocket exists; probe 1 found none).
- `"$GODOT" --headless --path src/CommandoWar.Client.Godot -- --selfcheck`
  (`CommandDemo.tscn`): `MATCH expected final hash 0x84A25E3559111E9B at
  tick 90` -- unchanged from the TASK-073 pin, exactly as expected (no
  content or code touched).
- `"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck`
  (run from `src/CommandoWar.Client.Godot`): `MATCH expected final hash
  0x6213D672BC36FDB8 at tick 20` -- unchanged.
- `"$GODOT" --headless --path . scenes/AppraisalDemo.tscn -- --selfcheck`:
  `MATCH expected hash 0xA1354EB998FC1B95, agent 0 Refused / agent 1
  Accepted` -- unchanged.
- Five temporary `dotnet fsi` probes against the built `CommandoWar.Sim.dll`/
  `cwheadless.dll` (removed after use): a static `Sight.visible` LOS sweep;
  a two-wave staged push (informative negative result); a single
  simultaneous six-agent push plus Aggressive/Immediate escalation; a
  Standard-vs-Aggressive branch comparison from an identical mid-run state;
  a final all-Standard/Routine reproduction used to render the evidence
  frame.

### Evidence

- **Failed trigger, cited from code**: `Simulation.fs:2255-2257`/
  `2273-2274` (see Changes above).
- **6-of-6 `Failed` reached**, tick-by-tick: machine gun incapacitated tick
  12, dead tick 71; rifleman 102 incapacitated tick 19, dead tick 78; agent
  4 incapacitated tick 16 (at `(9,6)`), dead tick 75; agent 0 incapacitated
  tick 19 (at `(9,5)`), dead tick 78; agent 5 incapacitated tick 90 (at
  `(10,5)`), dead tick 149; agent 3 incapacitated tick 124 (at `(13,5)`),
  dead tick 183; agent 2 incapacitated tick 415 (at `(16,4)`, near rifleman
  103); agent 1 incapacitated tick 416 (at `(17,7)`, near rifleman 104);
  `MissionFailed` emitted tick 416. Final state: agents 0/3/4/5 `Dead`,
  agents 1/2 `Incapacitated` (none `Alive` -- the exact `Failed` condition);
  riflemen 100/102 `Dead`, 101 `Incapacitated (50)`, 103 `Alive (650)`, 104
  `Alive (300)` -- the fatal hits landed before either remaining rifleman
  went down.
- `docs/evidence/task-075-bridgehead-failed-direction.png`: a
  `DiagnosticRender.Svg`-derived PNG (ImageMagick conversion) of the
  probe-7 (all-Standard/Routine) tick-416 frame, showing the six friendly
  markers (four dead, two incapacitated near riflemen 103/104), the two
  living riflemen, and the `mission: failed completed 3` status line.

### Deviations and unresolved issues

- **The first `--selfcheck` attempt hung indefinitely** (killed after
  ~13 minutes of near-idle CPU use). The real cause, found after killing it
  and re-running with output redirected to a file instead of piped through
  `tail` (which had been silently swallowing all output until process
  exit): `ERROR: Cannot instantiate C# script ... 'res://
  src/FSharpSceneHost.cs'` -- this fresh worktree's `CommandoWar.Client.
  Godot.slnx` had never been built (only the root `CommandoWar.slnx`, which
  does not include the Godot client project). Building it (`dotnet build
  src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`, `0
  Warning(s)`/`0 Error(s)`) before retrying fixed it; all three `--selfcheck`
  runs then completed in seconds with the unchanged pinned hashes recorded
  above. The one-time editor asset-import pass (`--editor --headless
  --quit`, the TASK-073 precedent for a fresh worktree's missing `.godot/
  imported` texture cache) was also needed and run first, separately.
- **No live windowed Godot capture was used, though a session was
  available.** `CommandDemoScene.fs`'s click-issued orders always build
  through `Command.moveTo`/`hold`/`assault`/`withdraw`/`suppress`, whose
  envelope is hard-coded `Urgency = Routine, RiskTolerance = Standard` --
  confirmed by inspection (`Commands.fs:131-179`) and by grep (no
  `RiskTolerance`/`Urgency`/`Aggressive`/`Immediate` reference anywhere in
  `CommandDemoScene.fs`). Replaying this investigation's exact multi-
  hundred-tick, precisely-targeted order sequence through live mouse clicks
  is not practically reproducible, and this task's own Forbidden/Allowed
  scope does not permit adding a new scripted self-check sequence to the
  client project to do it programmatically (`CommandDemoScene.fs` is not in
  Allowed scope). The `DiagnosticRender.Svg`-to-PNG path (`docs/evidence/`,
  explicitly Allowed) is therefore not a fallback here but the only means
  that actually demonstrates the finding faithfully -- the same reasoning
  TASK-073 used when no live session existed at all, extended here to a
  case where a live session exists but cannot express the demonstration.
- **The Aggressive/Immediate envelope escalation used in probe 3/4 turned
  out not to be load-bearing** (probe 6) -- an honest correction to this
  session's own working assumption partway through the investigation, not
  smoothed over. The final, cleanest reproduction (probe 7) needed no
  envelope tuning at all.
- **A genuine, working `RouteTooExposed` refusal was observed firing one
  tick too late to prevent the fatal engagement** (probe 6) -- not a defect
  in this task's own scope to fix (Forbidden scope explicitly excludes any
  `Appraisal.fs` change), and not a new finding: it is the identical
  mechanism TASK-073 already documented for the `Succeeded` direction's own
  un-staged `MoveTo` order, now confirmed to apply the same way here.
  Recorded, not treated as a bug to route around.
- **Probe 2's two-wave staged approach is a real, informative negative
  result**, not a dead end silently discarded: it shows the `Failed`
  outcome is sensitive to *how* threats are engaged (concentrated
  simultaneous pressure vs. spread-out sequential 1v1s can flip which side
  a given engagement favours), which is why probe 3's single-wave shape,
  closer to the task's own "no cover/suppression tactics" description, was
  needed to reproduce the reported asymmetry at all.
- This worktree's own checked-out branch was found 11 commits behind local
  `main` (still at TASK-060) before any of the above work started --
  fast-forwarded (`git merge --ff-only main`) since the branch was a strict
  ancestor of `main` and the working tree was clean; `tasks/TASK-075-*.md`
  itself did not exist in this worktree until fast-forwarded, and was
  additionally copied in from the parallel main checkout (an untracked file
  an orchestrating session had placed there for a separate parallel-tasks
  dispatch), per this task's own dispatch instructions.

### Documents updated

- `tasks/TASK-075-BRIDGEHEAD-FAILED-DIRECTION-VERIFICATION.md` (status,
  acceptance criteria, required verification, evidence).
- `docs/07_VERTICAL_SLICE.md` (section 9: new "Criterion 7 (`Failed`
  direction): met" subsection).
- This ledger detail file.
- `docs/evidence/task-075-bridgehead-failed-direction.png` (new).
- **Deliberately not edited in this pass** (this task's own explicit
  dispatch instructions, to avoid conflicting with parallel TASK-074/076
  sessions working in separate worktrees on shared files at the same time):
  `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`'s own index table/
  Pinned facts, `PROJECT_STATE.yaml`. The orchestrating session reconciles
  these centrally once all three parallel tasks report back.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-22, "accept all three, commit"), after independent
  re-verification by the orchestrating session (rebuild, `dotnet test`
  422/422, `-- corpus` 20/20, `bridgehead.cwscenario` confirmed byte-identical
  to the TASK-073 state, all three Godot `--selfcheck` hashes unchanged with
  TASK-074/TASK-076's own changes combined in the same tree).
