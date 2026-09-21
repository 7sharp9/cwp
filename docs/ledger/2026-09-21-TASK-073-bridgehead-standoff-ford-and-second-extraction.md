## 2026-09-21 - TASK-073 - A second, non-elevated river crossing and a second extraction cell on Bridgehead

**Owner:** Dave
**Source revision:** working tree on top of `main` at `aabc46a` (TASK-071,
accepted and committed)
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot
`4.7.2-stable_mono_win64` (headless only -- no windowed session available in
this environment, see Deviations)
**Status change:** `proposed -> review` (self-verified)

### Changes

Realises B-073 and folds in B-071's extraction-jam finding, both content-only
edits to `content/scenarios/bridgehead.cwscenario`:

1. **A second, elevation-0 ford** south of the existing bridge deck:
   `terrain-cell 8 9 / 9 9 / 8 10 / 9 10` changed from
   `impassable 0 0 true` to `passable 0 1 false` -- ordinary floor,
   deliberately elevation 0 (not 1, matching the existing bridge deck)
   since `Sight.fs`'s own blocking rule occludes only when an intermediate
   cell's elevation exceeds *both* endpoints': the existing deck sits at
   elevation 1 against elevation-0 banks and acts as a berm, giving every
   west-bank cell zero line of sight to the east bank until standing on the
   deck itself, at which point any visible threat is already inside
   `CombatConfig.WeaponRange` (7). The ford avoids reproducing that.
2. **A second `extraction-area` cell**, `extraction-south` at `(4,9)`,
   alongside the existing `extraction` at `(1,9)`. `Simulation.fs`'s
   extraction check (`mission`, lines ~2136/2146) accepts any cell in
   `WorldState.ExtractionAreas`, not filtered by the `ExtractAgents`
   objective's own `AreaId` (bound to `_` at line ~2222, never read) -- so a
   second declared area needed no `CommandoWar.Sim`/`Scenario.fs` change,
   confirmed by inspection before editing (task's own Required work item 1).
3. Header comment block rewritten to describe the new two-crossing
   geometry and the berm mechanism, matching this file's own existing
   convention of documenting its layout in comments.

No `content-version` bump (see Deviations -- this was a genuine, confirmed
factual correction to the task file's own assumption).

The ford's exact placement and the extraction cell's coordinate are both the
task file's own starting proposal, unchanged: neither needed adjustment.
A `Sight.trace` probe (Required work item 2) confirmed the proposed approach
lands the intended stand-off distance on the first attempt, and a real
`Simulation.step`-driven playthrough (Required work item 6) confirmed the
mechanism actually fires as designed. Both are detailed in Evidence below.

### Verification

- `dotnet build CommandoWar.slnx -c Release`: `0 Warning(s)`, `0 Error(s)`.
- `dotnet run --project src/CommandoWar.Headless -c Release -- import
  content/scenarios/bridgehead.cwscenario`: exit 0
  (`extraction areas : 2`, everything else unchanged from the pre-task
  file).
- `dotnet test CommandoWar.slnx -c Release`: `422/422` passed (unchanged
  from the TASK-070 baseline -- this task touches no `CommandoWar.Sim`
  code, so no new test was expected or added).
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`:
  `OK - all 20 entries match their committed tables` (unaffected;
  `bridgehead.cwscenario` is not a corpus entry, confirmed by inspection
  before editing).
- `"$GODOT" --editor --headless --quit --path src/CommandoWar.Client.Godot`:
  one-time asset (re)import (this worktree had not imported the client
  project yet).
- `"$GODOT" --headless --path src/CommandoWar.Client.Godot
  scenes/CommandDemo.tscn -- --selfcheck`: `MATCH expected final hash
  0x84A25E3559111E9B at tick 90` -- unchanged from the TASK-070 pin, not
  re-pinned. Confirmed by actually running it, not just by the task file's
  own reasoning that the scripted sequence's coordinates (`x=8-9,y=5-6`
  and the friendly start cells `x=2-4,y=5-8`) never reach `y=9-10`.
- `git status --porcelain` / `git diff --stat`: `content/scenarios/
  bridgehead.cwscenario` (35 insertions, 9 deletions) and a new
  `docs/evidence/task-073-bridgehead-standoff-and-second-extraction.png`
  only -- matches the task's Allowed scope exactly; no
  `CommandoWar.Sim`/`CommandoWar.Headless`/client file touched.
- Three temporary `dotnet fsi` probes against the built
  `CommandoWar.Sim.dll`/`cwheadless.dll` (removed after use, the
  B-071/this-session precedent):
  1. A static `Sight.trace`/`Sight.visible` sweep of the approach row
     (`x=3..12, y=9`) against rifleman 102, rifleman 101, and machine gun
     100, before and after the edit.
  2. A real `Simulation.step` loop driving a two-order sequence (recon
     halt at the stand-off cell, then a follow-up order continuing past
     it) and a parallel two-cell extraction test, tracing every emitted
     event tick by tick.
  3. A `Diagnostics.frameOf` / `DiagnosticRender.Svg` render of the
     refusal-tick frame, converted to PNG (ImageMagick) for the evidence
     screenshot in place of a live windowed Godot capture (see Deviations).

### Evidence

- **Stand-off distance/LOS**, static probe: `(5,9) -> (13,9)` (rifleman
  102), Chebyshev distance exactly 8, `Sight.trace.Visible = true`,
  `Blocker = None`. Scanning the whole approach row against rifleman 101
  and machine gun 100 found no unintended new sightline within the
  intended stand-off band (the only cells visible to 100/101 are `(11,9)`/
  `(12,9)`, already at the depot's edge, not a materially new exposure).
- **Refusal-before-contact**, `Simulation.step` probe: friendly agent 5
  ordered `MoveTo (5,9)` (tick 1, arrives tick 4); `ContactObserved` with
  rifleman 102 fires tick 5; a follow-up order `MoveTo (12,9)` issued tick
  6 (threat already known) is **immediately Refused**
  (`RouteTooExposed(Some AgentId 102)`) the same tick, before the agent
  takes a single step. Held at `(5,9)` through tick 60 with **zero
  `ShotFired` events** -- a genuine "refused before contact," the
  canonical-refusal-sequence shape `docs/07` section 8 describes, now
  reachable on Bridgehead's own real content for the first time (not only
  TASK-038's separate synthetic fixture).
- **Second extraction cell**: agents 0 and 5 ordered simultaneously to the
  two distinct cells (`(1,9)` and `(4,9)`) both `AgentExtracted` cleanly,
  zero contention between them -- the new cell satisfies `ObjectiveId 3`'s
  per-agent check identically to the original.
- `docs/evidence/task-073-bridgehead-standoff-and-second-extraction.png`:
  a diagnostic-frame SVG render (converted to PNG) of the refusal-tick
  frame -- shows the new ford's open cells, the exposed row highlighted
  red from `x=5` to the depot, agent 5 at the stand-off cell marked `R`
  (Refused), and the known-contact marker on rifleman 102.

### Deviations and unresolved issues

- **`content-version` was NOT bumped, contrary to the task file's own
  "Inputs and assumptions" text.** Confirmed wrong by inspection, exactly
  as that item asked: `Scenario.validate` (`Scenario.fs:467-468`) requires
  `content-version` to **exactly equal** the code-level
  `ScenarioContent.Version` literal (`Scenario.fs:90`, `7`), which only
  changes when `ScenarioFile.fs`'s own grammar gains a new directive. This
  task adds no new grammar and makes no `CommandoWar.Sim` change, so
  `ScenarioContent.Version` stays `7` -- bumping the file's
  `content-version` to `8` would make `Scenario.validate` reject it
  outright (`UnsupportedContentVersion(8, 7)`). Left at `7`.
- **A single, un-staged `MoveTo` order aimed deep into the depot does not
  reliably get the stand-off protection**, found while probing (Required
  work item 3). Ordering agent 5 directly to `(12,9)` from `(4,8)`
  (skipping the `(5,9)` waypoint) produces a Chebyshev-optimal path that
  hugs row `y=8` and only descends onto the ford at `x=7` -- already
  distance 6 from rifleman 102, inside `WeaponRange` (7). Contact and the
  first `ShotFired` exchange (a real hit) both occurred the same tick the
  order was still `Accepted`; the reappraisal only flipped to `Refused`
  one tick later. `Pathfinding` is deliberately threat-blind (stage 2) and
  always finds the geometrically shortest route, which does not have to
  pass through the `x=5..6` stand-off band on its way to a deep target; a
  one-tick lag between a new contact being observed and the Appraisal
  phase's reappraisal reacting to it compounds this, since Combat's own
  independent within-range check already fires that same tick. Not a
  regression: before this task the map had zero stand-off cells at all, so
  contact and engagement were already always simultaneous. Not fixed
  further here -- no ford coordinate change would fix it (the same
  Chebyshev-shortest-path behaviour applies to any single-elevation
  crossing); recorded as known behaviour for whoever scopes the next task
  touching this map or `Pathfinding.fs`'s threat-awareness.
- **The second extraction cell provides genuine parallelism (2x), not
  unlimited simultaneous lanes**, found while probing (Required work item
  6). `Simulation.fs`'s `occupantOf` excludes only non-`Alive` agents
  (TASK-066); an `Extracted` agent remains `Alive` and keeps occupying its
  cell, so each single-cell `extraction-area` still hosts exactly one
  agent at a time -- a pre-existing mechanic (B-071's own finding),
  unchanged by this task. Confirmed: two more agents sent to the *same*
  already-occupied cells reproduce the exact B-071 shepherding shape
  (`MovementAbandoned` after `StallAbandonTicks`). This task resolves
  B-071 "at its source" in the sense the task file states -- a real second
  destination doubling throughput -- not by eliminating shepherding
  altogether.
- **No live windowed Godot session was possible in this environment.**
  Confirmed, not assumed: `FSharpSceneHost.cs`'s own `--screenshot`
  capture path (`GetViewport()?.GetTexture()?.GetImage()`) explicitly logs
  `"run windowed, not --headless"` on failure, and this environment has no
  attached display. Used the task file's own allowed alternative -- an
  equivalent direct-pipeline probe -- for both the refusal/extraction
  demonstration and the evidence screenshot (the project's own existing
  `Diagnostics.frameOf`/`DiagnosticRender.Svg` machinery, the same one
  every committed `content/diagnostics/*.svg` golden already uses,
  rendered to SVG and converted to PNG).
- This worktree's own branch was found significantly behind local `main`
  (stuck at TASK-060, `main` at TASK-071) before any of the above work
  started -- fast-forwarded (`git merge --ff-only main`) since the
  worktree's tip was a strict ancestor of `main` and its working tree was
  clean (nothing unique to lose). Not a finding about this task's own
  content, but recorded since it was a real, unexpected blocker at the
  start of this session.

### Documents updated

- `tasks/TASK-073-BRIDGEHEAD-STANDOFF-FORD-AND-SECOND-EXTRACTION.md`
  (status, acceptance criteria, required verification, evidence, the
  `content-version` correction, review).
- This ledger detail file.
- **Deliberately not edited in this pass** (this dispatch's own scope
  guardrails, to avoid conflicting with a parallel TASK-072 agent working
  in a separate worktree on shared files at the same time):
  `docs/11_BACKLOG.md`, `PROJECT_STATE.yaml`, `docs/07_VERTICAL_SLICE.md`,
  `docs/12_PROGRESS_LEDGER.md`'s own index table/Pinned facts. The
  orchestrating session reconciles these centrally once both parallel
  tasks report back.

### Review

- Reviewer: Dave
- Accepted: yes, 2026-09-21 ("accept both, commit and update any docs so we
  know where progress is it"), on the self-verification evidence above plus
  the orchestrating session's own independent re-run of the build/test/
  corpus/import/`--selfcheck` commands (including combined with TASK-072's
  own change in the same tree) before reporting this task as ready for
  review.
