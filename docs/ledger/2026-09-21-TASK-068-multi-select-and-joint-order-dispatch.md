## 2026-09-21 - TASK-068 - Multi-select UI and joint order dispatch

**Owner:** Dave
**Source revision:** `main` at `3d4f5e4` (TASK-067 accepted and committed)
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot `4.7.2-stable_mono_win64`
**Status change:** `in progress -> review` (self-verified)

### Changes

Realises the client-side half of B-067. Scoped via two parallel research
passes (one tracing every `CommandDemoScene.fs` site touching `selected:
AgentId option`; one tracing `FSharpSceneHost.cs`'s mouse-input pipeline,
which had no button-up/`Pressed: false` handling anywhere at all) plus one
`AskUserQuestion` round resolving four product-facing forks the traces could
not answer from code alone: right-click clears the entire selection
regardless of size; the hover/route preview shows every selected agent's own
route, formation-aware once more than one agent is selected and `MoveTo` is
armed; the HUD selection line shows a count above one agent, not a list;
a Dead/Incapacitated/already-on-target selected agent is silently dropped
from a dispatched order, the rest of the (eligible) selection still receives
it. The selection gesture itself (drag rubber-band-select + shift-click) and
the formation rule (each agent's own authored `FormationOffset` only) were
already confirmed in the TASK-067 session.

No `CommandoWar.Sim`/`CommandoWar.Headless` change of any kind: the sim-side
mechanism this task exercises (`Command.moveToMany`, `ReceivedOrder.AsGroup`)
already existed, tested, and accepted since TASK-020/067.

- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`: `OnClick` gained a
  `shiftHeld: bool` parameter; new `OnDragSelect: cellXs: int[] * cellYs:
  int[] * shiftHeld: bool -> unit`.
- `src/CommandoWar.Client.Godot/Core/DemoRenderScene.fs`: mirrored with
  no-op bodies (no selection concept in this scene).
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: `selected`
  retyped `AgentId option -> Set<AgentId>`; every consumer updated
  (`syncOrderModeToSelection` now disarms only once zero `Alive` candidates
  remain anywhere in the selection; `liveOrderText`/dev-overlay per-agent
  text/LOS ray all fall back to a count/omitted form above one selected
  agent; `haloItems` folds over the set; `holdOutlineItems`/`exposedCells`
  generalised to "selection non-empty"/"union over selection"; `HudText`'s
  `selText` shows a count above one). `OnClick`'s order-dispatch branch
  rewritten: filters the selection to eligible (`Alive`, not already on the
  clicked cell) agents, then either one `Command.moveToMany` (mode 0) or N
  looped single-recipient commands (modes 1-4, since only `MoveTo` reads
  `AsGroup`/formation). New `OnDragSelect` implementation. `OnHover`'s
  `previewPath` becomes per-agent (`Cell[][]`), formation-aware
  (`Appraisal.resolveFormationTarget`, the same `occupied`-computation shape
  `Simulation.fs`'s own appraisal phase uses) whenever more than one agent is
  selected and `MoveTo` is armed.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: new drag-tracking
  state (`_isDragging`, `_dragStartScreen`, `_dragCurrentScreen`,
  `DragThresholdPixels = 6f`); `_UnhandledInput` restructured for a real
  left-button press/motion/release sequence (a left-button press starts
  tracking; on release, a drag below the threshold resolves as an ordinary
  click, otherwise the dragged rectangle is tested against every real,
  full-opacity agent's on-screen centre — `TryHitAgentCircle`'s own
  per-agent derivation, `Rect2.HasPoint` in place of its point-vs-circle
  distance test — and the hit cells are handed to `OnDragSelect`; right-click
  and the HUD-icon click stay single-press, unchanged in spirit); a
  translucent marquee (`DrawRect`, the `DrawMissionSummaryPanel` precedent)
  renders while dragging past the threshold; every existing `_scene.
  OnClick(...)` call site (screenshot priming) updated with the new
  argument; a new `--screenshot-multiselect <path>` mode (the
  `--screenshot-squad` precedent) priming a real two-agent `OnDragSelect` +
  joint `MoveTo` for evidence, paused immediately.

A real, pre-existing bug was found and fixed while generalising order
dispatch, not introduced by it: `OnClick`'s pending-order dedup,
`pending.RemoveAll(fun c -> c.Command.Agent = agentId)`, checks only the
*head* of a pending command's `Recipients` (`PlayerCommand.Agent`'s own
documented definition, `List.head Recipients`). A stale pending
single-recipient order for any non-head recipient of a new multi-recipient
command would survive this check and land in the same tick's batch alongside
the new command — two commands addressing the same agent in one tick, the
exact untested combination `OnClick`'s own pre-existing comment already
warns against. Fixed to match on `Recipients` membership against every
recipient of every command about to be added, not `.Agent` equality against
one id.

A second, pre-existing behaviour was clarified during implementation, not
fixed (it was never a defect): the "already on target cell" half of the
partial-dispatch filter turns out to be unreachable via the real click path,
both before and after this task. `friendlyAt` matches any friendly agent
standing on the clicked cell regardless of vitals, so a click that would
land exactly on a selected agent's own current cell is always resolved as a
(re-)selection click before the order-dispatch branch is ever reached.
Preserved for exact behavioural parity with the pre-existing single-agent
guard (which had the identical property), not removed as unrelated cleanup.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0 Warning(s)`, `0 Error(s)` (unaffected — no
    `CommandoWar.Sim`/`CommandoWar.Headless` file touched).
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `415/415` passed, unchanged.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus`
  - Result: `19/19` entries match, no `--regenerate` needed.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  replay-file content/replays/envelope-full.cwreplay`
  - Result: `checkpoints : OK (24 ticks match the file's committed
    hashes)`, unaffected.
- Command: `dotnet build
  src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Manual check: `Godot_v4.7.2-stable_mono_win64_console.exe --headless
  --path src/CommandoWar.Client.Godot scenes/CommandDemo.tscn --
  --selfcheck`
  - Result: `MATCH 0x2629A1FE165F94BB` at tick 90, unchanged — proves the
    default (no modifier, single-agent) click path dispatches byte-identically
    to before this task, since `CommandDemoDrive.runScriptedSelfCheck`'s
    sequence is entirely single-agent, no-shift clicks.
- Manual check: same, `scenes/SnapshotDemo.tscn` / `scenes/AppraisalDemo.tscn`
  - Result: `MATCH 0x6213D672BC36FDB8` / `MATCH 0xA1354EB998FC1B95`, both
    unaffected.
- A temporary `dotnet fsi` probe (removed after use), run twice (once
  driving the full scenario end to end, once continuing to step further to
  settlement) directly against the built `CommandoWar.Client.Godot.Core.dll`
  and `CommandoWar.Sim.dll`:
  - `agent0.FormationOffset = Some {X=0;Y=0}`, `agent1.FormationOffset =
    Some {X=1;Y=0}` (bridgehead.cwscenario's own authored fireteam-alpha
    slots 0/1).
  - Independently computing `Appraisal.resolveFormationTarget` against the
    same loaded scenario/seed for a shared anchor `(6,5)` predicts agent 0
    -> `(6,5)`, agent 1 -> `(7,5)`.
  - Driving the real scene: `OnDragSelect([|3;2|], [|5;5|], false)` ->
    `HudText()` reads `selected=2 agents`; halo-item count in `DrawList()`
    (`Kind=1 && 0.3<A<0.4 && G≈0.95`) = 2.
  - `OnClick(true, 2, 5, true)` (shift-click agent 1) -> `selected=agent 0`;
    repeating -> `selected=2 agents` (toggle confirmed both ways).
  - `OnHover(6, 5)` with both selected and `MoveTo` armed: preview-dot cells
    `[(3,5);(4,5);(4,5);(5,5);(5,5);(6,5);(6,5);(7,5)]` — the combined two
    routes (agent 0: 3 dots ending `(6,5)`; agent 1: 5 dots ending `(7,5)`),
    matching the independently-computed predictions exactly.
  - `OnClick(true, 6, 5, false)` (dispatch) then `StepTicksHeadless`: at 60
    ticks the two agents sit at `(5,5)`/`(6,5)`; stepping further to 180
    ticks in 30-tick increments shows no further change — a genuine,
    permanent settle one cell short of each agent's own predicted slot (see
    Deviations below).
  - Right-click (`OnClick(false, 0, 0, false)`) -> `selected=none`.
  - Separate scene instance: `OnDragSelect([|3;2|], [|5;5|], false)` then a
    plain click on agent 2's own (unselected) cell -> `selected=agent 2`,
    confirming a plain click always replaces an existing multi-selection
    rather than adding to it.
- `--screenshot-multiselect docs/evidence/task-068-multiselect.png` captured
  through the real Godot 4.7.2 editor (windowed): HUD reads `selected=2
  agents`, two overlapping selection halos and a leader marker visible on
  the two drag-selected agents, pending-route dots visible.
- `dotnet list CommandoWar.Sim package`: `FSharp.Core` only, unaffected.

### Evidence

- The probe's independently-computed `Appraisal.resolveFormationTarget`
  predictions matching the scene's own preview-dot route endpoints exactly
  is the direct proof that a client-issued `Command.moveToMany` genuinely
  sets `AsGroup = true` and resolves formation correctly for the first time
  through the real input path, not just a unit test.
- `docs/evidence/task-068-multiselect.png`.
- One unrelated editor-triggered reformat of
  `src/CommandoWar.Client.Godot/tools/ExportTerrainScript.cs` (spaces to
  tabs, the same recurring side effect noted in several prior tasks'
  ledgers whenever the Godot editor is run) was caught by `git status`/`git
  diff` and reverted, not committed.

### Deviations and unresolved issues

- **Honestly flagged, not smoothed over:** the probe's joint-order-to-
  settlement run shows the two ordered agents end one cell apart, not each
  at its own full resolved slot. Their routes overlap for several cells
  (agent 1's route to `(7,5)` passes through agent 0's own target `(6,5)`),
  and the trailing agent's order stalls against the leading, still-`Alive`
  agent and is abandoned (`MovementAbandoned`, `Simulation.
  StallAbandonTicks = 40`, TASK-065's own mechanism, working exactly as
  designed) one cell short, permanently (confirmed unchanged through tick
  180). This is the same live-agent chokepoint contention TASK-066/067
  already found and left as B-067's own accepted, unresolved gap — now
  reproduced for the first time through a real client-issued group order
  rather than six independent solo ones. Not a new defect, and fixing it
  (occupancy-aware routing or waypoint sequencing) is explicitly out of this
  task's scope (see the task file's Forbidden scope).
- **Not independently verified:** the raw mouse press/motion/release
  sequence, `InputEventMouseButton.ShiftPressed`, and the `Rect2` marquee
  geometry added to `FSharpSceneHost.cs` compile clean against the real
  `GodotSharp` 4.7.2 assembly (confirmed by the client `.slnx` build) and
  mechanically mirror the already-proven `TryHitAgentCircle`/
  `TryHitOrderModeIcon`/`ScreenToCell` patterns, but were only exercised
  through the F# `IClientScene` surface directly by the probe, not an actual
  click-and-drag in a live windowed Godot session — that needs Dave's own
  interactive pass, the TASK-060/061/064 precedent for anything this
  environment cannot provide a display for.

### Documents updated

- `tasks/TASK-068-MULTI-SELECT-AND-JOINT-ORDER-DISPATCH.md` (status,
  acceptance criteria, verification, evidence, review).
- `docs/11_BACKLOG.md` (B-067 row).
- `docs/12_PROGRESS_LEDGER.md` (this row).
- `PROJECT_STATE.yaml` (`active_work`, top-level `updated`).
- `src/CommandoWar.Client.Godot/README.md` (new section, layout-table line).

### Review

- Reviewer: Dave
- Accepted: yes, 2026-09-21 ("commit and lets move on"), on the
  self-verification evidence alone -- no live editor test of the drag/
  shift-click gesture requested.
