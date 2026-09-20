## 2026-09-20 - TASK-063 - Mission summary panel in CommandDemoScene

**Owner:** Dave
**Source revision:** `main` at `07b5ada` (TASK-062 accepted, 2 commits ahead of `origin/main`, working tree clean at start)
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot `4.7.2-stable_mono_win64`
**Status change:** `proposed -> review` (self-verified; awaiting Dave's live acceptance)

### Changes

Realises backlog B-033, narrowed from "replay playback and mission summary"
to mission summary alone (see `docs/11_BACKLOG.md` B-033/B-064 rows for the
split rationale, confirmed via `AskUserQuestion`, three rounds, before
drafting). `CommandDemoScene` now shows a player-facing panel the instant
`WorldState.MissionOutcome` leaves `InProgress` -- the first Godot client
task to build any post-mission presentation.

- `Core/IClientScene.fs`: new `abstract MissionSummaryLines: unit -> string[]`
  (empty = nothing to show, the `OrderMode`/primitives-only precedent).
  `DemoRenderScene` gets a trivial `[||]` no-op (the `OnOrderModeClick`
  precedent).
- `Core/RenderShared.fs`: new `missionSummaryLines (state: WorldState) :
  string[]` -- headline (`MISSION SUCCESS`/`MISSION FAILED`), then one line
  per completed objective, one per in-progress objective (with its tick
  count), one per extracted agent. A private `objectiveLabel` derives a
  plain-language label from an `Objective`'s own `AreaId`/`TargetId` (no
  authored display name exists); a private `objectiveIdOf` duplicates
  `Simulation.mission`'s own private id-extraction recursion rather than
  widening its visibility across the assembly boundary (the
  `reasonText`/`devReasonText` "two audiences" precedent already in this
  file). `Extracted` is read directly from `WorldState.Agents` (not added to
  `AgentSnapshot`, which carries no such field, per the task's own Forbidden
  scope).
- `Core/CommandDemoScene.fs`: `stepOnce` sets `paused <- true` the tick
  `MissionOutcome` leaves `InProgress` (reusing the existing tactical-pause
  field, not a new one); `OnClick`'s order-issuing guard gained
  `&& state.MissionOutcome = InProgress`, so a click cannot issue a new
  order even if the player manually un-pauses afterward. New
  `MissionSummaryLines()` implementation delegating to `RenderShared`.
  `CommandDemoDrive.runScriptedSelfCheck` extended: after the existing
  `MoveTo(3,0)`/`Hold(2,1)` sequence and 12 ticks, agent 0 is reselected and
  sent on to `(4,4)` -- `DemoScenario`'s own sole authored objective (a
  non-optional `ReachArea` at `"ridge-top"`) -- then stepped 28 more ticks
  (40 total, up from 20), reaching a real `MissionOutcome = Succeeded`
  through the same click path a player uses.
- `src/FSharpSceneHost.cs`: `DrawMissionSummaryPanel()` (a fixed
  screen-space backing rect plus `DrawString` lines, the `DrawOrderModeBar`
  precedent), called last in `_Draw` whenever `MissionSummaryLines()` is
  non-empty. A second screenshot mode, `--screenshot-mission <path>`,
  because the existing `--screenshot` priming immediately re-pauses to hold
  a pending-order/hover-preview state (the opposite of what this needed) --
  selects agent 0 and sends it straight to `(4,4)`, left unpaused so ticks
  keep advancing until the objective completes; captures at a longer,
  mode-specific frame threshold (400 vs. 45) to give the 8-cell walk time to
  finish.
- Re-pinned `CommandDemoScene`'s `--selfcheck` expected hash (behaviour
  genuinely changed: the scripted run now reaches `Succeeded`).

### Deviation found and fixed: two other scenes' self-check pins were already stale

Before touching anything, `SnapshotDemo.tscn`/`AppraisalDemo.tscn`
`--selfcheck` mismatched their committed pins through the real Godot
editor. Verified this predates this task, not caused by it: stashed every
TASK-063 change, rebuilt, and reran both self-checks against committed
`main` -- identical mismatch. Root cause: TASK-062's `Canonical.
FormatVersion` 12 -> 13 bump changed every canonical byte layout (adding
`MissionOutcome`/`CompletedObjectives`/`ObjectiveProgress`/`Extracted`),
which necessarily reshuffles the state hash for every scenario including
ones with no gameplay behaviour difference -- exactly the "byte-layout-only
re-pin" every `CommandoWar.Sim`-side corpus/fixture/diagnostics golden
already got from TASK-062, but TASK-062 never touched
`CommandoWar.Client.Godot` (accepted "Sim-side only, no live client review
needed") and so never re-ran or re-pinned these two Godot-side self-check
constants. Fixed as part of this task (both are one-line constant
corrections, zero behaviour risk, and left broken they would have made
TASK-063's own "SnapshotDemo/AppraisalDemo unaffected" acceptance criterion
unverifiable): `FSharpSceneHost.cs`'s `SnapshotDemo` pin
(`0xF422ACB8D5A86FF0 -> 0xEC8F01D781AB2122`) and `AppraisalDemoScene.cs`'s
`_expectHash` (`0x194805888CBE240D -> 0xC382CACA830CCC35`).

A second, smaller bug found while capturing evidence: `DrawMissionSummaryPanel`'s
first `DrawString` call passed the screen centre (`640f`) as the text box's
left edge instead of the panel's own left edge, so `HorizontalAlignment.
Center` centred every line a full half-panel-width to the right of the
panel itself (visible immediately in the first screenshot). Fixed by
anchoring the box to `origin.X` (the panel's actual left edge) instead;
re-verified by recapturing the screenshot.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Command: `dotnet test`
  - Result: `408/408` passed, unaffected (no `CommandoWar.Sim` change).
- Command: `dotnet run --project src/CommandoWar.Headless -- corpus`
  - Result: `18/18` entries match their committed tables, unaffected.
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Manual check: `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . scenes/CommandDemo.tscn -- --selfcheck`
  - Result: `MATCH` at tick 40, `0xED5437A8773C92B2`.
- Manual check: `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck`
  - Result: `MATCH` at tick 20, `0xEC8F01D781AB2122` (the re-pinned, previously-stale value).
- Manual check: `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . scenes/AppraisalDemo.tscn -- --selfcheck`
  - Result: `MATCH`, `0xC382CACA830CCC35` (the re-pinned, previously-stale value), agent 0 Refused / agent 1 Accepted text unchanged.
- Manual check: a temporary `printfn`-based probe (removed after use) in
  `CommandDemoDrive.runScriptedSelfCheck`, run through the real Godot
  editor, printed `MissionSummaryLines()` at the end of the scripted run.
  - Result: `["MISSION SUCCESS"; "completed: reach ridge-top"]` -- confirms
    the mission genuinely reaches `Succeeded` and the panel's headline plus
    completed-objective line are correct, not merely that the hash changed.
- Manual check: a second temporary probe added a further `OnClick` attempt
  (reselect agent 0, click an empty cell) after the mission had already
  ended, and exposed `nextCommandId` before/after.
  - Result: `3` before and `3` after -- confirms no order is queued once
    `MissionOutcome <> InProgress`, i.e. the order-issuing gate actually
    blocks a real click, not just a `paused` flag that could be bypassed.
- Manual check: windowed `--screenshot-mission` capture of `CommandDemo.tscn`.
  - Result: `docs/evidence/task-063-mission-summary.png` -- shows the panel
    ("MISSION SUCCESS" / "completed: reach ridge-top"), the HUD reading
    `PAUSED`, and agent 0 at `(4,4)` with `order=accepted`.
- `git status --porcelain`: matches this task's allowed scope (six modified
  source files under `Core`/`src`, `AppraisalDemoScene.cs`, plus
  `PROJECT_STATE.yaml`/`docs/11_BACKLOG.md`/this ledger entry/the task file
  and its evidence screenshot).

Not exercised live (flagged, not silently assumed correct): `HoldArea`/
`DestroyTarget`/`ExtractAgents`/`AllOf`/`Optional` objective-label
formatting and the extracted-agent line. `DemoScenario` (the only scenario
`CommandDemoScene` loads) authors a single `ReachArea` objective and no
`ExtractAgents`/extraction-reachable content, so no in-scope scripted
sequence reaches those branches. They are exhaustively matched (the F#
compiler enforces this) and mechanically identical in shape to the
verified `ReachArea` case, but their exact wording is confirmed by code
inspection only, not a live run -- worth a look next time a scenario
authoring those objective kinds (e.g. `bridgehead.cwscenario`) is driven
through this scene.

### Evidence

- `docs/evidence/task-063-mission-summary.png` (committed).
- Self-check transcripts above (tick-by-tick hashes available by re-running
  the commands; not separately committed, the existing project convention).

### Deviations and unresolved issues

- Two pre-existing stale self-check pins (`SnapshotDemo.tscn`,
  `AppraisalDemo.tscn`), caused by TASK-062, found and fixed here -- see
  above. Flagged for Dave since it means these two pins went unverified
  through an entire prior task's acceptance.
- `HoldArea`/`DestroyTarget`/`ExtractAgents`/`AllOf`/`Optional` label
  formatting not exercised live -- see above.
- No live Godot-editor review from Dave yet; this task's evidence is
  entirely self-verified (headless self-checks plus one windowed
  screenshot capture), the recent precedent for tasks touching the client
  where Dave has not yet sat down with the editor himself.
- Replay playback (originally bundled into B-033) is deferred to new
  backlog row B-064, not designed or built here.

### Documents updated

- `tasks/TASK-063-MISSION-SUMMARY-SCREEN.md` (status, outcome).
- `docs/11_BACKLOG.md` (B-033 row narrowed; B-064 row added, both already
  present from drafting).
- `docs/12_PROGRESS_LEDGER.md` (this row).
- `PROJECT_STATE.yaml` (`active_work`).
- `src/CommandoWar.Client.Godot/README.md` (new section).

### Review round 1 (2026-09-20, live)

Dave opened the real Godot editor, ran `CommandDemo.tscn` interactively,
selected agent 0, and moved it near the ridge-top area -- but at tick 1141
the HUD still read `order=no order`, `running` (not `PAUSED`), no panel.

Root cause: `WorldState.ObjectiveAreas`/`.ExtractionAreas` were never
rendered anywhere in `CommandDemoScene` -- confirmed by inspection, the
only references to either field in the whole client are in
`Core/TerrainAuthoring.fs` (the terrain export/import tool), not any play
scene. The objective cell `(4,4)` therefore looked exactly like any other
patch of terrain, with nothing to click toward precisely; Dave had almost
certainly landed adjacent to it rather than on it. This made the panel
this task adds effectively unreachable without first turning on the `F1`
developer overlay to read raw coordinates -- not a reasonable ask of a
player.

Confirmed via `AskUserQuestion`: fix now, in this task, rather than file a
separate follow-up, since without it the feature is not actually
testable/playable. Added:

- `CommandDemoScene.fs`: a new `objectiveMarkerItems: DrawItem[]`, built
  once in `Ready` from `state.ObjectiveAreas`/`.ExtractionAreas` (static
  authored content, the `state.Terrain`/`.Bounds` "read `WorldState`
  directly" precedent) -- a `Kind = 5` hollow ring (`RenderShared.
  cellRing`) plus a text label of the area's own `AreaId` (`RenderShared.
  cellLabel`) at each cell, included in the existing depth-sorted draw
  list alongside terrain/agents/halos.
- No `IClientScene` interface change, no interaction/gating change --
  purely additive, always-on rendering (not gated behind `F1`, since this
  is genuinely player-facing information, not a developer diagnostic).

A second issue surfaced while capturing the re-verification screenshot,
before showing it to Dave: the first colour choice for the objective
marker (gold, `(1.0, 0.85, 0.25)`) was visually near-identical to the
selection halo's own gold ring (`(1.0, 0.95, 0.30)`) exactly where it
matters most -- a selected agent standing on the objective cell, i.e. the
moment of success. Changed to violet (`(0.75, 0.35, 1.0)`); the extraction
marker's cyan (`(0.3, 0.85, 1.0)`) was already visually distinct and left
unchanged.

#### Verification

- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Manual check: all three scenes' `--selfcheck` through the real Godot
  editor.
  - Result: `CommandDemo.tscn` `MATCH 0xED5437A8773C92B2` (unchanged);
    `SnapshotDemo.tscn` `MATCH 0xEC8F01D781AB2122` (unchanged);
    `AppraisalDemo.tscn` `MATCH 0xC382CACA830CCC35` (unchanged) --
    confirms the new markers are genuinely render-only.
- Manual check: recaptured `--screenshot-mission` evidence.
  - Result: `docs/evidence/task-063-mission-summary.png` (updated) shows a
    violet ring around agent 0 at the objective cell (distinct from the
    gold selection halo around it) and a cyan ring labelled `exit` at the
    extraction area.
- `dotnet build CommandoWar.slnx -c Release` / `dotnet test` / `--corpus`:
  reconfirmed `0/0` / `408/408` / `18/18`, all unaffected.

#### Evidence

- `docs/evidence/task-063-mission-summary.png` (recaptured, committed).

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-20, "ok it works", after round 1's objective-marker
  fix).
- Notes: round 1 found a real, blocking gap (no visual marker for
  objective/extraction areas); fixed and re-verified same session; accepted
  on the next live try with no further changes requested.
