## 2026-09-17 - TASK-040 - Selection, input mapping, tactical pause, and command preview

### Why

B-026's dependencies (B-014, TASK-006) are both `done`; TASK-039's
client-core scaffold (B-027) is also `done`. Of the P4 items now selectable
(B-026, B-028, B-030 proper, B-031), Dave chose B-026 via `AskUserQuestion`:
the natural next step now that a live-rendering scene exists, and it builds
directly on TASK-039's `FSharpSceneHost`/`CwClientCore` scaffold rather than
being sim-side (B-030/B-031) or blocked on B-026 itself (B-028).

### Central decisions (confirmed with Dave via `AskUserQuestion` before drafting)

1. New scene (`CommandDemo.tscn`), not an extension of `SnapshotDemo.tscn` —
   that scene's task file documents it as running unattended with no input.
2. Command preview uses real `Pathfinding.find` (already pure/total/
   deterministic), not a straight line.
3. Single-unit selection only; fireteam/multi-select deferred.
4. Orders can be issued while tactically paused, delivered once resumed.

### Changes

**New `Core/RenderShared.fs`**: `buildTerrainItems`, `agentColor`, and
`depthKey` extracted out of `DemoRenderScene.fs` verbatim, so
`CommandDemoScene` reuses them instead of duplicating them.
`DemoRenderScene.fs` updated to call `RenderShared.*`; its own `--selfcheck`
hash confirmed unchanged after the extraction (see Verification).

**`Core/IClientScene.fs`**: three new members, primitives only (the
ADR-0004 interop idiom) — `OnClick(isLeftButton: bool, cellX: int, cellY:
int)`, `OnHover(cellX: int, cellY: int)`, `OnTogglePause()`.
`DemoRenderScene` gets trivial no-op implementations (that scene still takes
no input).

**New `Core/CommandDemoScene.fs`**: `CommandDemoScene` implementing
`IClientScene` over `DemoScenario.initialState()` with no canned command
log — friendly agents sit still until ordered. `OnClick(true, ...)` selects
a live friendly agent at the clicked cell, or (if one is already selected
and the click lands elsewhere in bounds) builds a `RecordedCommand`
(`Command.moveTo`, a locally incrementing `CommandId`, `IssuedAtTick =
state.Tick`, delivery `Tick = state.Tick + 1L`, `Issuer = "player"`) and
queues it; `OnClick(false, ...)` deselects (the `MainNode.cs` right-click
precedent). `OnHover` calls `Pathfinding.find` from the selected agent's
live position to the hovered cell and caches the resulting route (or `None`
for `NoPath`/`BudgetExhausted`/`InvalidEndpoint`). `OnTogglePause` flips a
`paused` flag that `Update` checks before accumulating/stepping; input is
still processed while paused, so an order composed during a pause is
delivered once resumed. `DrawList` adds three overlay layers on top of
`RenderShared`'s terrain/agent items, all reusing the existing `DrawItem`
`Kind` values (no new `Kind`, no `FSharpSceneHost.cs` render-side change):
a translucent oversized `Kind = 1` halo at the selected agent's cell
(inserted before its real circle so the stable sort draws it underneath), a
row of small translucent `Kind = 1` dots along the previewed route
(excluding the agent's own starting cell), and a small green `Kind = 1`
marker at any agent's `AgentSnapshot.Destination` once Appraisal accepts an
order (TASK-028's existing mechanism — no new state). A `StepTicksHeadless`
member (not part of `IClientScene`) steps a fixed tick count with no wall
clock, used only by the scripted self-check.

**New `CommandDemoDrive.runScriptedSelfCheck`**: builds a `CommandDemoScene`,
calls `Ready`, then the real `OnClick(true,0,0)` / `OnHover(3,0)` /
`OnClick(true,3,0)` sequence (select friendly agent 0 at (0,0), preview and
issue a short move to (3,0), clear of the ridge and the impassable block),
then `StepTicksHeadless(DemoScenario.TickCount)` — the `DemoDrive.
runFullSequence` precedent, this time exercising the input path instead of a
canned log.

**`src/FSharpSceneHost.cs`**: new `ScreenToCell` (exact inverse of the
existing `CellToScreen`, the `MainNode.cs` precedent) and `_UnhandledInput`
forwarding a left/right `InputEventMouseButton` to `OnClick`, an
`InputEventMouseMotion` to `OnHover`, and a `Space` `InputEventKey` press to
`OnTogglePause` — all via primitives (button flag, projected cell
coordinates), never a raw `Godot.InputEvent` into `Core/`. `RunSelfCheck`
extended (`switch` on `SceneType`) to also dispatch
`CwClientCore.CommandDemoScene` to `CommandDemoDrive.runScriptedSelfCheck()`,
pinning its own hash. `--screenshot` mode also runs the same scripted
`OnClick`/`OnHover` sequence right after `_scene.Ready()` when `SceneType ==
"CwClientCore.CommandDemoScene"` — without it, the capture would show idle
agents with no selection, since `--screenshot` mode injects no real input.

**New `scenes/CommandDemo.tscn`**: `FSharpSceneHost.cs` root, `SceneType =
"CwClientCore.CommandDemoScene"`. Not `run/main_scene`.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0/0` (unaffected).
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `297/297` (unaffected — no `CommandoWar.Sim`/`CommandoWar.Headless`
    change).
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: `0/0`.
- Command: `"$GODOT" --editor --headless --quit --path .` (re-import)
  - Result: clean.
- Command: `"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck`
  - Result: `MATCH expected final hash 0x11B06E6EDE0C52E3 at tick 20`, exit
    `0` — confirms the `RenderShared.fs` extraction is behaviour-neutral for
    the existing scene.
- Command: a temporary scratch console probe
  (`CommandDemoDrive.runScriptedSelfCheck()`, referencing `Core/` directly,
  removed after use — the TASK-038/039 precedent for pinning a new hash)
  - Result: tick 1-20 hash sequence computed independently of Godot; final
    hash `0x649FA4D08E2931CA`.
- Command: `"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck`
  - Result: byte-identical tick-by-tick sequence to the scratch probe;
    `MATCH expected final hash 0x649FA4D08E2931CA at tick 20`, exit `0` — a
    cross-runtime determinism cross-check, the TASK-039 precedent.
- Command: `"$GODOT" --path . scenes/CommandDemo.tscn -- --screenshot <path>`
  - Result: `docs/evidence/task-040-selection-and-preview.png` committed —
    shows `selected=agent 0` in the HUD, a yellow selection halo around
    agent 0 (already arrived near (3,0) by the capture frame), two small
    route-preview dots along the vacated path, and the other two agents
    unaffected. No destination marker visible in this frame because the
    order had already completed (`Destination` cleared on arrival,
    TASK-030's commitment-completion behaviour) — confirmed correct, not a
    rendering gap, by checking the HUD tick against `DemoScenario`'s known
    3-cell/17-tick arrival window.
- Manual check: `git status --porcelain`
  - Result: matches this task's allowed scope, plus the pre-existing
    unrelated `project.godot` `run/main_scene` diff (present at session
    start, documented in `PROJECT_STATE.yaml`, left untouched — see
    "Deviations").

### Evidence

- `docs/evidence/task-040-selection-and-preview.png`.
- `SnapshotDemo.tscn` selfcheck: unchanged `0x11B06E6EDE0C52E3` (tick 20).
- `CommandDemo.tscn` selfcheck: newly pinned `0x649FA4D08E2931CA` (tick 20),
  cross-checked against an independent scratch computation.

### Deviations and unresolved issues

- `src/CommandoWar.Client.Godot/project.godot`'s `run/main_scene` was
  already pointing at `SnapshotDemo.tscn` instead of the documented
  `AppraisalDemo.tscn` at session start (likely leftover from TASK-039's
  windowed screenshot capture). Not touched by this task; flagged again
  here since it remains uncommitted.
- `--screenshot` mode now contains one scene-specific branch
  (`SceneType == "CwClientCore.CommandDemoScene"`) injecting a fixed,
  scripted interaction before capture. This mirrors the existing
  scene-specific `RunSelfCheck` dispatch precedent rather than introducing a
  new kind of branching in the generic host.
- ADR-0004 review trigger 1 (an F# breakpoint hit from an interactive
  editor/F5-launched run) remains undischarged — untouched by this task,
  still needs Dave's own interactive editor session.

### Documents updated

- `tasks/TASK-040-SELECTION-INPUT-AND-TACTICAL-PAUSE.md` (`proposed ->
  review`, Outcome section).
- `src/CommandoWar.Client.Godot/README.md`.
- `docs/11_BACKLOG.md` B-026 row.
- `docs/12_PROGRESS_LEDGER.md` (this row).
- `PROJECT_STATE.yaml`.

### Review round 1 (2026-09-17): Dave's feedback, addressed pre-acceptance

Dave tried the scene and reported: selection only registers over a tiny area
"at the base" of the agent sprite and isn't reliable; a set order isn't
visually distinguishable from a hover preview, especially while paused; and
raised (without committing to it yet) wanting multi-waypoint stacking with
cancellation, flagging it should fit "how we want the player to control the
agents overall" first.

**Fixed (bugs/UX in the delivered work, not new scope):**

- **Selection hit-testing bug**: `FSharpSceneHost.cs`'s `_Draw` renders an
  agent's circle `TileH/2` (11px) above `CellToScreen`'s diamond-centre, but
  `ScreenToCell` inverted the unshifted diamond formula — a click only
  resolved to the agent's true cell near the bottom of the visible circle,
  exactly Dave's "very small area at the base" symptom. Fixed with
  `TryHitAgentCircle`: hit-tests the click against every currently-drawn
  agent's actual rendered circle (`item.Kind == 1 && item.A >= 0.99f` picks
  out real agents, excluding the halo/preview/pending/committed overlays
  which are also `Kind = 1` but always translucent), falling back to the
  plain diamond-based `ScreenToCell` only for a miss (a move-to-cell click).
- **No visual difference between hover, queued, and confirmed orders**:
  `CommandDemoScene.DrawList` now renders three distinct route states —
  hover preview (dim yellow, transient), pending/queued-but-undelivered
  (orange, persists through a pause since delivery is what's blocked), and
  committed/en-route (green, the full `Pathfinding.find` path, not just the
  destination cell, once `AgentSnapshot.Destination` is populated). The
  `--screenshot` priming sequence now pauses immediately after issuing an
  order specifically to make the orange pending state — previously
  invisible — actually appear in the evidence.
- **Defensive fix, not asked for but found while fixing the above**: `OnClick`
  now removes any existing pending command for the clicked agent before
  queuing a new one. Without it, two clicks before the next delivery tick
  (trivial while paused) would hand `Simulation.step` two different commands
  both addressing the same agent in one tick's batch — untested, ambiguous
  input no corpus entry covers. Replace-not-stack is the safe default until
  real waypoint stacking (see below) is deliberately designed.

**Not built — flagged back to Dave via `AskUserQuestion` rather than guessed:**
multi-waypoint stacking and cancellation. Dave's own phrasing ("that said it
should fit into how we want the player to control the agents overall")
already names this as a product-direction question, and it has real
sim-architecture weight: `WorldState`/`AgentState`/`Commitment` today model
exactly one live order per agent, no queue. Whether stacking should be
purely client-side sequencing (no sim change, simpler, but the "plan" only
exists in the Godot client and evaporates if the client restarts) or an
authoritative multi-waypoint order in the sim (bigger, touches
`Canonical.encode`/replay, but stays true to "recorded commands and replay
evidence" as a fixed constraint) is exactly the kind of fork this project
resolves with Dave before drafting, not after.

**Re-verified after the fixes**: `dotnet build` both `.slnx` (`0/0`),
`dotnet test` (`297/297`), both scenes' `--selfcheck` hashes unchanged
(`0x11B06E6EDE0C52E3` / `0x649FA4D08E2931CA` — the fixes are render/input-only,
no stepping or command-construction change reachable by the scripted
self-check), new `docs/evidence/task-040-selection-and-preview.png` showing
`PAUSED`, the selection halo, and the orange pending route together.

Dave's answer to the stacking-scope `AskUserQuestion`: keep single-order for
now (the recommended option) — TASK-040 accepted as single-order-only,
multi-waypoint stacking and cancellation parked as its own backlog item,
**B-051**, to design deliberately later rather than folded in here.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-17, "seems right")
- Notes: accepted after round 1 feedback (the click hit-testing bug and the
  missing hover/pending/committed route distinction) was fixed and
  re-verified. Multi-waypoint stacking and cancellation deliberately not
  part of this acceptance — deferred to backlog row B-051.
