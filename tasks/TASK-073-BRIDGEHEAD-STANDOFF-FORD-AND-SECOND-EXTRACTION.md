# TASK-073: A second, non-elevated river crossing and a second extraction cell on Bridgehead

Status: done (accepted by Dave 2026-09-21, "accept both, commit")
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises B-073, folds in B-071's extraction-jam finding

## Objective

Edit `content/scenarios/bridgehead.cwscenario` (content only) to add:

1. A second, elevation-0 river crossing south of the existing bridge deck,
   positioned so a friendly agent approaching along it gains line of sight
   to depot rifleman `102` at a genuine stand-off distance (inside
   `PerceptionConfig.SightRange`/`AppraisalConfig.ThreatEngagementRange`,
   outside `CombatConfig.WeaponRange`) before it is possible to close to
   engagement range -- giving the Appraisal phase's route-exposure stage
   something to appraise, and refuse against, before contact happens. This
   is what the existing bridge deck structurally cannot do (see Why, below).
2. A second `extraction-area` cell, distinct from the existing sticky
   `(1,9)` extraction cell -- resolving B-071's extraction-jam finding at
   its source (a real second valid destination) rather than leaving it as a
   UX-explanation problem.

No `CommandoWar.Sim`/`Scenario.fs` change is required or permitted -- both
changes are pure scenario-content edits, confirmed by this session's
research pass (see Required reading).

## Why this task exists

A same-day charter-alignment investigation and an independent
tactical-game-design-expert review both concluded Bridgehead's *geometry*,
not presentation, is why `docs/07_VERTICAL_SLICE.md` criterion 3 (canonical
refusal) has essentially never fired on this map, and why the
TASK-059-through-TASK-070 run of pathfinding/formation/contention fixes
consumed as much effort as it did (`docs/00_PROJECT_CHARTER.md` section 10's
own "pathfinding and formation work consume the project" stop condition,
materialising on this content specifically).

A dedicated research pass this session (not this task) confirmed the exact
mechanism, with a temporary `dotnet fsi` probe against the built
`CommandoWar.Sim.dll` (deleted after use): `Sight.trace` blocks an
intermediate cell whenever that cell's elevation exceeds *both* endpoints'
(`Sight.fs`, the supercover walk's own opacity/elevation check). The
existing bridge deck (`terrain-cell 8 5|9 5|8 6|9 6 passable 1 1 false` --
elevation 1, `content/scenarios/bridgehead.cwscenario` lines 59-62) sits at
elevation 1 against elevation-0 banks on both sides, so it acts as a berm:
every west-bank cell has **zero** line of sight to any east-bank threat
(confirmed directly: `(3,5)`/`(6,5)`/`(7,5)`/`(7,6)` all blocked, `Blocker =
(8,5)`, against machine-gun `100` at `(11,5)`, Chebyshev distance 8). Line of
sight only opens once an agent is standing *on* the deck itself, at which
point every threat visible at all is already within `CombatConfig.
WeaponRange` (confirmed `7`, `Combat.fs:33`). A full scan of the reachable
map found exactly two cells anywhere with a genuine stand-off sightline
(distance 8, clear LOS, no engagement) -- `(13,1)` and `(13,10)` -- both
unreachable dead ends adjacent to the *opposite* rifleman's own position, on
the far side of the map from any plausible friendly approach.

This is not incidental map dressing: it is the direct, mechanical reason a
`MoveTo` order on Bridgehead is either wholly unappraised-against-a-threat
(nothing known yet) or already in contact (too late to refuse) -- there is
no third state. TASK-038's separate synthetic fixture already proves the
appraisal/refusal mechanism itself works correctly once a stand-off state
exists; Bridgehead alone has never offered one.

The research pass also confirmed (`Perception.fs:60`,
`AppraisalConfig.ThreatEngagementRange = 8`, `Appraisal.fs:90`, read
alongside `Appraisal.cellPressure`, `Appraisal.fs:238-248`) the real
load-bearing band is distance **exactly 8**: `ThreatEngagementRange` (8) is
what stage-3 route exposure actually checks against a known threat with
clear `Sight.visible`, not the wider `SightRange` (10) the initiating
investigation assumed -- a stand-off cell must therefore land at distance 8
from a threat, with clear LOS and no elevation blocker, not merely "under
10".

## Central decisions (confirmed with Dave 2026-09-21 via `AskUserQuestion`)

1. **A second, non-elevated ford south of the existing bridge**, at
   approximately `x=8-9, y=9-10` (replacing the currently impassable/opaque
   river cells there), mirroring the existing deck's own passable/moveCost
   authoring but at **elevation 0** (not 1) -- the deliberate difference
   that avoids reproducing the elevation-berm LOS-blocking mechanism this
   task exists to fix. Confirmed against a real distance calculation: a
   friendly agent around `(5,9)` would gain clear horizontal LOS to
   rifleman `102` at `(13,9)` at Chebyshev distance exactly 8 (the row `y=9`
   is otherwise unobstructed -- no crate/building entry exists on that row
   between `x=5` and `x=13`), closing inside `WeaponRange` (7) only by
   continuing further east. This is a concrete starting proposal, not a
   guaranteed-exact final coordinate set -- confirm the real distances and
   LOS with a probe during implementation (Required work item 2) and adjust
   the exact cells if the first attempt does not land a genuine
   distance-8-with-clear-LOS approach cell.
2. **A second `extraction-area` cell, folded into this same task** (not a
   separate follow-up) -- confirmed as a pure content change, no
   `CommandoWar.Sim` change needed (see Required reading,
   `Simulation.fs:2136,2146,2222`).

## Required reading

- `docs/00_PROJECT_CHARTER.md` section 10 (stop conditions, specifically
  "pathfinding and formation work consume the project while producing no
  distinct gameplay") and `docs/10_RISK_REGISTER.md` R-010.
- `docs/07_VERTICAL_SLICE.md` section 3 (scenario/objective sequence),
  section 8 (canonical refusal sequence), section 9 criterion 3 and its
  "Criterion 7" update subsections -- the exact findings this task closes.
- `docs/11_BACKLOG.md` B-071's row in full (the extraction-jam finding this
  task folds in) and B-073's own row.
- `content/scenarios/bridgehead.cwscenario` in full (119 lines) --
  especially the terrain block (lines 39-91: the river at `x=8-9` for
  `y=0-4`/`y=7-11`, the bridge deck at `y=5-6`, the two building clusters at
  `x=14-16,y=2-3`/`x=14-16,y=8-9`, the scattered crates and `terrain-cover`
  lines), the friendly/enemy deployments (lines 102-112: friendly 0-5 at
  `x=2-4,y=5-8`; enemy 100 (MG) at `(11,5)`, 101 at `(13,2)`, 102 at
  `(13,9)`, 103 at `(17,3)`, 104 at `(17,8)`), and the `Objectives` block
  (lines 113-119: `objective-area observation 4 4`, `extraction-area
  extraction 1 9`, `target bridge-charge 9 5`, `target mg-position 11 5`,
  the three `objective` lines). The scenario file's own `terrain-cell`
  field order is `X Y (passable|impassable) ELEVATION MOVECOST OPAQUE`
  (confirmed against the file's own crate rows, e.g. `terrain-cell 10 4
  passable 0 3 true` = elevation 0, moveCost 3, opaque true, matching the
  file's own header comment "crate (passable, moveCost 3, opaque true)") --
  confirm this reading against `ScenarioFile.fs`'s `parseArea`/terrain-line
  parser directly before editing, do not assume it from this task file
  alone.
- `src/CommandoWar.Sim/Sight.fs` in full -- the elevation-blocking rule this
  task's fix depends on, and `Sight.trace`'s determinism section (a pure
  function of `Terrain` and two `Cell`s, freely usable in a throwaway
  verification probe).
- `src/CommandoWar.Sim/Perception.fs`: `PerceptionConfig.SightRange` (line
  60, confirmed `10`).
- `src/CommandoWar.Sim/Combat.fs`: `CombatConfig.WeaponRange` (line 33,
  confirmed `7`).
- `src/CommandoWar.Sim/Appraisal.fs`: `AppraisalConfig.ThreatEngagementRange`
  (line 90, confirmed `8` -- the actual load-bearing stand-off distance) and
  `cellPressure` (lines 238-248, stage-3 route exposure) -- confirm exactly
  how a route cell's exposure is computed against a known threat before
  finalising which cell(s) the redesigned approach must pass through.
- `src/CommandoWar.Sim/Scenario.fs`: the `AreaId` validation (lines 354,595
  -- one shared, must-be-unique namespace across objective/extraction/
  resupply areas) -- the new extraction cell's id must not collide with
  `observation`, `extraction`, `bridge-charge`, or `mg-position`.
- `src/CommandoWar.Sim/Simulation.fs`: the extraction check (lines
  2136,2146 -- `Array.contains a.Position extractionCells` over **all** of
  `WorldState.ExtractionAreas`, not filtered by the `ExtractAgents`
  objective's own `AreaId`) and line 2222 (`ExtractAgents`'s `AreaId` field
  bound to `_`, never read) -- the exact confirmation that a second
  `extraction-area` line needs no `Simulation.fs`/`Scenario.fs` code change,
  only a second declared area.
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`:
  `CommandDemoDrive.runScriptedSelfCheck` (lines 1699-1732) -- six literal
  select/target cell pairs, all near the existing bridge deck (`x=8-9,
  y=5-6`) and the friendly start cells (`x=2-4,y=5-8`); none currently
  route anywhere near `y=9-10`, so this task's ford is not expected to
  change its outcome -- confirm this by actually re-running `--selfcheck`,
  not by this reasoning alone.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: the
  `_screenshotMode` click sequence (lines ~300-313) and the always-on
  observation-objective auto-order (lines ~315-327) -- both use literal
  coordinates from the *current* Bridgehead layout (agent 0 starts `(3,5)`,
  agent 1 `(2,5)`, a still-capture click at `(5,5)`, a hover at `(2,4)`).
  This task does not change any friendly starting position, so these should
  be unaffected -- confirm, do not assume.
- `docs/06_CONTENT_AND_PRESENTATION.md` and TASK-061/062's own backlog rows
  (B-025, B-032) for this project's established greybox-terrain-authoring
  conventions (the three fixed `TerrainTileSet.Sources` combinations: floor/
  block/crate) -- the new ford must stay within those same conventions, not
  invent a fourth terrain look.

## Dependencies

- B-070 (done -- the review that produced this task), B-025 (TASK-061,
  done -- original greybox terrain), B-032 (TASK-062, done -- original
  objective/extraction content). No other task selected.

## Inputs and assumptions

- The ford replaces the currently impassable/opaque river cells at (at
  minimum) `(8,9)`/`(9,9)`, and likely `(8,10)`/`(9,10)` to match the
  existing deck's own two-row width convention, with `passable 0 1 false`
  (elevation 0, moveCost 1, not opaque) -- ordinary floor, mirroring the
  file's own header vocabulary, deliberately not elevation 1 (see Central
  decision 1). Exact cell set to be confirmed by the LOS probe in Required
  work item 2, not assumed from this proposal alone.
- The second extraction cell is a new `extraction-area <id> <x> <y>` line
  with a fresh, unique `AreaId` (e.g. `extraction-south` or `extraction2`),
  placed on the safe west bank, reachable without requiring an agent to
  cross back through contested ground unnecessarily, and far enough from
  `(1,9)` that the two are genuinely separate destinations a player can
  choose between (not adjacent cells that functionally recreate one sticky
  area). A concrete starting proposal: `(4,9)` -- confirm it is open,
  passable, and not already used by any other declared feature in the
  edited file before committing to it.
- This task does not change any friendly or hostile starting position, unit
  type, formation, or the `bridge-charge`/`mg-position`/`observation`
  objective cells -- scope is the terrain grid (the new ford) and the
  `Objectives` block (the new extraction area) only.
- `content-version` in the scenario file's own header should be bumped
  (currently `7`) per this project's content-versioning convention --
  confirm the exact convention (is it scenario-file-local, or tied to
  anything else) by inspection of `ScenarioFile.fs`/`Scenario.fs` before
  assuming a bare increment is sufficient.

  **Confirmed wrong, by inspection, exactly as this item asked.**
  `content-version` is not a per-edit content counter: `Scenario.validate`
  (`Scenario.fs:467-468`) rejects any file whose `content-version` does not
  **exactly equal** the code-level `ScenarioContent.Version` literal
  (`Scenario.fs:90`, currently `7`, bumped only when `ScenarioFile.fs`'s own
  grammar/schema gains a new directive -- TASK-010/049/058/059/062's own
  history). Since this task adds no new grammar (more `terrain-cell` lines
  and a second `extraction-area` line both use existing directives) and
  makes no `CommandoWar.Sim`/`ScenarioFile.fs` change (Forbidden scope),
  `ScenarioContent.Version` itself stays `7` -- so `content-version` in the
  file **must also stay `7`**. Bumping it to `8` would make
  `Scenario.validate` reject the file outright
  (`UnsupportedContentVersion(8, 7)`), the opposite of this item's intent.
  Left unchanged at `7`; not bumped.
- No `CommandoWar.Sim`/`Scenario.fs`/`Simulation.fs` change of any kind is
  expected or permitted (see Forbidden scope) -- if implementation finds a
  real necessity for one (e.g. `AreaId` uniqueness validation rejecting the
  new id for an unanticipated reason), stop and report it rather than work
  around it silently.

## Allowed scope

- `content/scenarios/bridgehead.cwscenario`: the new ford's `terrain-cell`
  lines (replacing the current impassable river entries at those
  coordinates), one new `extraction-area` line, `content-version` bump, and
  updated header comments describing the new geometry (the file's own
  existing convention of documenting its layout in a header comment block).
- `docs/evidence/`: new screenshot(s) demonstrating the stand-off refusal
  actually firing on the redesigned map.
- Re-authoring `CommandDemoDrive.runScriptedSelfCheck`'s coordinates and/or
  `FSharpSceneHost.cs`'s `_screenshotMode`/auto-order coordinates **only if
  verification in Required work shows they are actually affected** -- do
  not pre-emptively change them.
- Godot import/export tooling files that reference `bridgehead.cwscenario`
  generically (confirmed by this session's research pass to operate on
  whatever `.cwscenario` is given, not hardcoded to its current content) --
  no change expected, confirm by running them.

## Forbidden scope

- No `CommandoWar.Sim`/`CommandoWar.Headless`/`Scenario.fs`/`Simulation.fs`
  change of any kind -- this task is content-only, confirmed feasible as
  such by this session's own research pass.
- No change to any friendly/hostile starting position, unit type, or
  formation.
- No change to the `bridge-charge`, `mg-position`, or `observation`
  objective cells or their `objective` lines' own parameters (plant
  duration, optional/non-optional).
- No removal or narrowing of the existing bridge deck -- this task adds a
  second crossing, it does not replace or simplify the first.
- No corpus/fixture/`SimulationTests` change -- this session's research
  pass confirmed no test corpus entry references `bridgehead.cwscenario` by
  name (all synthetic fixtures are independent); if implementation finds
  otherwise, that is a new finding to report, not an assumption to silently
  correct around.
- No change to `docs/07_VERTICAL_SLICE.md`'s required force composition,
  objective sequence, or required commands (section 3/4) -- the mission's
  scale, setting, and forces stay exactly as specified; only the terrain
  geometry and the number of valid extraction cells change.

## Required work

1. Confirm the `terrain-cell` field order and the `AreaId`/extraction
   mechanics in Required reading by inspection, not assumption.
2. Author the ford edit in a working copy; verify the resulting stand-off
   distance and LOS with a temporary `dotnet fsi` probe (`ScenarioFile.parse
   -> Scenario.validate -> World.ofScenario -> Sight.trace`, deleted after
   use, the B-071/this-session precedent) against rifleman `102` (and, if
   relevant, `101`) from the intended approach cell(s) -- confirm distance
   is exactly `AppraisalConfig.ThreatEngagementRange` (8) or otherwise
   genuinely inside it while remaining outside `CombatConfig.WeaponRange`
   (7), with clear LOS and no unintended elevation blocker. Adjust the
   exact cells if the first attempt does not land this.
3. While probing, check for unintended consequences: does the new ford open
   an unwanted sightline for the machine gun (`100`) or another rifleman
   sooner than intended; does it change the existing MG-neutralisation
   opening's own tactical shape. Report findings honestly, adjust the
   ford's exact placement if a genuinely worse tactical shape results.
4. Author the second `extraction-area` line; confirm `Scenario.validate`
   accepts the edited file (`cwheadless import content/scenarios/
   bridgehead.cwscenario`, TASK-060's own verb) with no error.
5. Re-run `CommandDemoDrive.runScriptedSelfCheck` through the real Godot
   4.7.2 editor (`--selfcheck` on `CommandDemo.tscn`); confirm whether the
   pinned hash changes. If it does, determine why (an actually different
   tick-by-tick outcome, not merely a hash artefact -- `Terrain` is excluded
   from `Canonical.encode`, so a hash change would mean agent behaviour
   itself genuinely changed) and re-pin with a clear explanation, following
   the TASK-070/`chokepoint-detour` precedent for honestly recording a
   changed fixture rather than silently re-pinning.
6. Manually drive a full playthrough in a live Godot session (or via a
   temporary probe replicating B-071's own successful `Succeeded` sequence)
   confirming: (a) an agent approaching via the new ford triggers a real
   `OrderAppraised(Refused(RouteTooExposed(...)))` before entering
   `WeaponRange` of rifleman 102, and (b) sending survivors to the two
   distinct extraction cells (or verifying a single-cell send still works
   as before) no longer requires the one-at-a-time B-071 shepherding
   workaround when a second cell is used.
7. Update documentation per Documentation updates below.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] `cwheadless import content/scenarios/bridgehead.cwscenario`: exit 0,
      no validation error.
- [x] A temporary probe (evidence captured, not just asserted) confirms at
      least one real approach cell has clear LOS to a known rifleman at
      distance inside `ThreatEngagementRange` (8) and outside `WeaponRange`
      (7). Confirmed: `(5,9) -> (13,9)` (rifleman 102), Chebyshev distance
      exactly 8, `Sight.trace` visible, no blocker.
- [x] A live or probed playthrough demonstrates a real `RouteTooExposed`
      refusal firing on an ordinary `MoveTo` order that continues past the
      stand-off cell toward engagement range, *before* the agent has taken
      any fire -- the canonical-refusal-sequence shape `docs/07` section 8
      describes, now reachable on Bridgehead itself for the first time
      through the flagship map's own real content (not only TASK-038's
      separate synthetic fixture). Confirmed via a direct `Simulation.step`
      probe (evidence below) -- zero `ShotFired` events across 60 ticks.
- [x] A second `extraction-area` cell exists, is distinct from `(1,9)`, and
      a probe/live test confirms a friendly agent reaching it satisfies
      `ObjectiveId 3` exactly as reaching `(1,9)` does. Confirmed:
      `extraction-south` at `(4,9)`, `AgentExtracted` fires identically to
      `(1,9)`, and both cells extract in parallel with zero contention when
      used by different agents.
- [x] `CommandDemoDrive.runScriptedSelfCheck`'s outcome is re-verified
      through the real Godot 4.7.2 editor; any pinned-hash change is
      explained and justified, not silently re-pinned. Confirmed unchanged:
      `MATCH 0x84A25E3559111E9B` at tick 90, identical to the pre-task pin
      (the scripted sequence never touches `y=9-10`).
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless`/`Scenario.fs`/
      `Simulation.fs` file touched (`git diff --stat` confirms). Confirmed:
      only `content/scenarios/bridgehead.cwscenario` (content) and a new
      `docs/evidence/...png` changed.
- [x] `dotnet build`/`dotnet test`/`-- corpus`: unaffected counts, confirmed
      by running them. `dotnet test`: 422/422 (unchanged from the TASK-070
      baseline); `-- corpus`: 20/20 (unchanged; `bridgehead.cwscenario` is
      not a corpus entry).
- [x] Required documentation updated (see Documentation updates below).

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: **`0 Warning(s)`, `0
  Error(s)`.**
- `dotnet run --project src/CommandoWar.Headless -c Release -- import
  content/scenarios/bridgehead.cwscenario`: **exit 0**
  (`ok: content/scenarios/bridgehead.cwscenario`, `extraction areas : 2`).
- `dotnet test CommandoWar.slnx -c Release`: **422/422 passed, 0 failed**
  (unaffected; matches the pre-task TASK-070 baseline).
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`:
  **`OK - all 20 entries match their committed tables`** (unaffected;
  `bridgehead.cwscenario` is not part of the committed test corpus).
- The temporary LOS-verification probe's output (distances, `Sight.trace`
  results), captured as evidence (above), then the probe file deleted.
  Also a second temporary probe drove a real `Simulation.step` loop
  (two-order sequence, event trace) and a third rendered a diagnostic-frame
  SVG for the evidence screenshot; both deleted after use.
- `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path
  src/CommandoWar.Client.Godot scenes/CommandDemo.tscn -- --selfcheck`:
  **`MATCH expected final hash 0x84A25E3559111E9B at tick 90`** -- unchanged
  from the TASK-070 pin, not re-pinned (the scripted sequence's six literal
  select/target cell pairs stay within `x=8-9,y=5-6` and the friendly start
  cells `x=2-4,y=5-8`, never touching `y=9-10`, exactly as the task file's
  own Required reading predicted -- confirmed by actually running it, not
  by that reasoning alone). Required a one-time
  `"$GODOT" --editor --headless --quit --path .` asset (re)import first
  (the client project had not been imported in this worktree).
- A live windowed Godot session or an equivalent direct-pipeline probe
  demonstrating the stand-off refusal and the second extraction cell
  working, screenshotted to `docs/evidence/task-073-bridgehead-standoff-
  and-second-extraction.png`.

  **No live windowed Godot session is possible in this environment** --
  confirmed, not assumed: `FSharpSceneHost.cs`'s own `--screenshot` capture
  path (`GetViewport()?.GetTexture()?.GetImage()`) explicitly logs
  `"run windowed, not --headless"` on failure, and this environment has no
  attached display for a windowed Godot process to render into. Used the
  equivalent direct-pipeline probe instead: the same `Simulation.step`-
  driven playthrough above, captured through the project's own existing
  `Diagnostics.frameOf` / `CommandoWar.Headless.DiagnosticRender.Svg` (the
  identical machinery every committed `content/diagnostics/*.svg` golden
  output already uses), rendered to SVG and converted to PNG (ImageMagick,
  on the environment's own `PATH`). Written to `docs/evidence/task-073-
  bridgehead-standoff-and-second-extraction.png`.
- `git status --porcelain` / `git diff --stat`: matches this task's Allowed
  scope exactly -- **confirmed**: only `content/scenarios/
  bridgehead.cwscenario` modified (35 insertions, 9 deletions) and
  `docs/evidence/task-073-bridgehead-standoff-and-second-extraction.png`
  added (untracked). No `CommandoWar.Sim`/`CommandoWar.Headless`/client
  file touched.

## Evidence to capture

- All command output above.
- The exact final ford coordinates and the measured stand-off distance/LOS
  result, including any deviation from the `x=8-9,y=9-10` starting proposal
  and why.

  **No deviation was needed.** The starting proposal landed on the first
  attempt: `terrain-cell 8 9 / 9 9 / 8 10 / 9 10` changed from
  `impassable 0 0 true` to `passable 0 1 false` (elevation 0, matching the
  file's own "floor" convention, TASK-060's three fixed
  `TerrainTileSet.Sources` combinations). A `Sight.trace`/`Sight.visible`
  probe against the built `CommandoWar.Sim.dll` confirmed `(5,9) -> (13,9)`
  (rifleman 102) is Chebyshev distance exactly 8, `Visible = true`,
  `Blocker = None` -- inside `AppraisalConfig.ThreatEngagementRange` (8),
  outside `CombatConfig.WeaponRange` (7). Scanning the whole approach row
  (`x=3..12, y=9`) against rifleman 101 and machine gun 100 found no
  unintended new sightline anywhere along the approach (all blocked by the
  depot buildings/crates); the only cells visible to 100/101 are `(11,9)`/
  `(12,9)`, already past the intended stand-off band and immediately
  adjacent to the depot itself, not a materially new exposure.
- The exact second extraction cell coordinate chosen and why.

  `extraction-area extraction-south 4 9` -- the proposal's own suggested
  coordinate, confirmed passable/elevation 0/not opaque, not colliding with
  any other declared feature (friendly deployments, objective/target
  markers, the original `extraction` area), and on the safe west bank.
- The live/probed demonstration of a real `RouteTooExposed` refusal firing
  on Bridgehead before contact, for the first time.

  A direct `Simulation.step` probe (`ScenarioFile.parse -> Scenario.validate
  -> World.ofScenario -> Simulation.step` in a loop, the B-071/this-session
  precedent) drove a real two-order sequence for friendly agent 5: order 1,
  `MoveTo (5,9)` (a reconnaissance halt at the stand-off cell), issued tick
  1, arrives tick 4; contact with rifleman 102 registers tick 5
  (`ContactObserved`, both directions, symmetric). Order 2, `MoveTo (12,9)`
  (continuing past the stand-off cell toward the depot), issued tick 6 once
  the threat was already known: appraised and **immediately Refused**
  (`RouteTooExposed(Some AgentId 102)`) the same tick it was issued, before
  the agent took a single step toward it. The agent then held at `(5,9)`
  for the remaining 54 ticks probed (through tick 60) with **zero
  `ShotFired` events** -- genuine "refused before contact," not merely
  "refused before death." Rendered as a diagnostic-frame SVG (the project's
  own `DiagnosticRender.Svg`, converted to PNG) at
  `docs/evidence/task-073-bridgehead-standoff-and-second-extraction.png`:
  shows the new ford's open cells, the exposed row highlighted red from
  `x=5` to the depot, agent 5 at the stand-off cell marked `R` (Refused),
  and the known-contact marker on rifleman 102.

  The second extraction cell was verified the same way: agents 0 and 5
  ordered simultaneously to the two distinct cells (`(1,9)` and `(4,9)`)
  both extract cleanly (`AgentExtracted`) with zero contention between
  them -- the new cell satisfies `ObjectiveId 3`'s per-agent check
  identically to the original.
- Any unintended tactical consequence found while probing (item 3 in
  Required work), reported honestly whether or not it required a design
  adjustment.

  Two real findings, neither requiring a coordinate change:

  1. **A single, un-staged `MoveTo` order aimed deep into the depot does
     not reliably get the stand-off protection.** Ordering agent 5 directly
     to `(12,9)` from its start cell `(4,8)` (skipping the `(5,9)`
     waypoint) produces a Chebyshev-optimal path that hugs row `y=8` and
     only descends onto the ford at `x=7` -- already Chebyshev distance 6
     from rifleman 102, inside `WeaponRange` (7). Contact and the first
     `ShotFired` exchange (a real hit) both occurred at tick 9, with the
     order still `Accepted`; the reappraisal only flipped to `Refused` at
     tick 10, one tick after first contact. `Pathfinding` is deliberately
     threat-blind (stage 2, `docs/05` section 5) and always finds the
     geometrically shortest route, which does not have to pass through the
     `x=5..6` stand-off band on its way to a deep target -- and there is a
     one-tick lag between a new contact being observed and the
     Appraisal-phase reappraisal reacting to it, during which Combat's own
     independent within-range check already fires. This is not a
     regression the ford introduces: before this task the same map had
     *zero* stand-off cells at all (every west-bank cell was LOS-blocked by
     the bridge berm), so contact and engagement were already always
     simultaneous. The ford is a strict improvement for a staged approach
     (see below) but does not, by itself, protect a reckless direct order
     into the depot -- recorded here rather than smoothed over, since
     adjusting the ford's placement would not fix it (the same
     Chebyshev-shortest-path behaviour applies to any single-elevation
     crossing).
  2. **The second extraction cell provides genuine parallelism, not an
     unlimited number of simultaneous lanes.** `Simulation.fs`'s
     `occupantOf` excludes only non-`Alive` agents (TASK-066); an
     `Extracted` agent remains `Alive` and keeps physically occupying its
     cell. So each single-cell `extraction-area` can host exactly one agent
     at a time -- a pre-existing mechanic, not introduced by this task.
     Confirmed directly: agents 0 and 5 sent to the two *different* cells
     both extract without contention, but sending two more agents (1 and
     4) to the *same already-occupied* cells reproduces the exact B-071
     shepherding shape (`MovementAbandoned` after the `StallAbandonTicks`
     timeout). The second cell genuinely doubles simultaneous extraction
     throughput (2 agents in parallel instead of 1) but does not eliminate
     the need to shepherd survivors beyond that -- an honest characterisation
     of what this task actually resolves, not a claim that B-071 is fully
     eliminated.

  Neither finding required changing the ford's placement or the extraction
  cell's coordinate; both are recorded as known behaviour for whoever plans
  the next task touching this map.

## Expected files

- `content/scenarios/bridgehead.cwscenario`.
- `docs/evidence/task-073-bridgehead-standoff-and-second-extraction.png`.
- `docs/11_BACKLOG.md` (B-073 row, and B-071's row noting the fold-in is
  now realised), `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.
- `docs/07_VERTICAL_SLICE.md` section 9 (criterion 3's status, if this task
  closes it for real on Bridgehead).
- Possibly `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` and/or
  `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`, only if
  verification (Required work item 5) shows the scripted self-check or the
  screenshot-mode/auto-order coordinates are actually affected.

  **Neither was touched.** `--selfcheck` reconfirmed `MATCH
  0x84A25E3559111E9B`, byte-identical to the pre-task pin (see Required
  verification) -- the scripted sequence's coordinates genuinely don't
  reach `y=9-10`, confirmed by running it, not assumed from the task file's
  own reasoning.

## Documentation updates

- this task file's status and evidence -- **done, this pass**;
- `docs/07_VERTICAL_SLICE.md` section 9 (criterion 3 realisation note, if
  closed) -- **not done in this pass**: a parallel TASK-072 agent is working
  in a separate worktree at the same time, and this dispatch's own scope
  guardrails deliberately excluded this shared file so the two worktrees
  cannot conflict on it. The orchestrating session reconciles it centrally;
  the finding to fold in is that criterion 3 is now reachable on
  Bridgehead's own real content (see Evidence above);
- `docs/11_BACKLOG.md` (B-073 row `proposed -> review`/`done`; B-071's row
  noting its extraction-jam finding is now resolved at the source, not just
  worked around) -- **not done in this pass**, same reason (shared-file
  guardrail);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file --
  the detail file was added
  (`docs/ledger/2026-09-21-TASK-073-bridgehead-standoff-ford-and-second-
  extraction.md`); the index row in `docs/12_PROGRESS_LEDGER.md` itself was
  **not** added in this pass (same shared-file guardrail -- the file's own
  index table is explicitly out of scope for this dispatch);
- `PROJECT_STATE.yaml` -- **not done in this pass**, same reason.

## Rollback or removal

Content-only: a `.cwscenario` text file and (possibly) two Godot client
literal-coordinate call sites, no `CommandoWar.Sim` canonical shape change,
no `Canonical.FormatVersion` bump expected (content is not part of the
canonical hash). A `git revert` of this task's commit should be clean.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-21, "accept both, commit and update any docs so we
  know where progress is it"), on the implementation-plus-independent-
  re-verification evidence recorded here and in
  `docs/ledger/2026-09-21-TASK-073-bridgehead-standoff-ford-and-second-extraction.md`
  (dispatched as one of two parallel implementation agents, each in an
  isolated worktree, and separately rebuilt/retested/re-`--selfcheck`ed by
  the orchestrating session -- including combined with TASK-072's own
  change in the same tree -- before being reported as ready for review).
  The corrected `content-version` assumption (left at 7, not bumped, per
  `Scenario.validate`'s exact-match requirement) and the single-un-staged-
  order caveat were reviewed and accepted as-is.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
