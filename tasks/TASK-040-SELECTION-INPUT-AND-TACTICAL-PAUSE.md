# TASK-040: Selection, input mapping, tactical pause, and command preview

Status: done (accepted by Dave 2026-09-17, "seems right")
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete)
Size: M

## Outcome (2026-09-17)

Implemented as drafted. `Core/RenderShared.fs` extracted from
`DemoRenderScene.fs` (terrain-item builder, agent colour, depth-sort key);
`SnapshotDemo.tscn`'s own `--selfcheck` hash confirmed unchanged
(`0x11B06E6EDE0C52E3`) after the extraction. `IClientScene` gained
`OnClick`/`OnHover`/`OnTogglePause` (primitives only); `DemoRenderScene`
gained no-op implementations. New `Core/CommandDemoScene.fs`: live selection
over a friendly agent, a player-built `Command.moveTo` queued as a
`RecordedCommand` for next-tick delivery through the real `Simulation.step`
(no canned command log), a `Pathfinding.find` hover preview, a selection
halo and destination marker (all reusing the existing `DrawItem Kind = 1`
value, no `FSharpSceneHost.cs` render-side change), and tactical pause
(orders composable while paused, delivered on resume). New
`CommandDemoDrive.runScriptedSelfCheck` drives the real `OnClick`/`OnHover`
methods (select agent 0, preview and issue `MoveTo(3,0)`), not a canned log
— the `DemoDrive.runFullSequence` precedent, this time exercising the input
path.

`FSharpSceneHost.cs` gained `ScreenToCell` (the exact inverse of the
existing `CellToScreen`) and `_UnhandledInput` forwarding mouse clicks,
mouse motion, and `Space` to the three new `IClientScene` members;
`RunSelfCheck` and the `--screenshot` priming both extended with a
`CommandDemoScene`-specific branch (the existing `RunSelfCheck`
scene-dispatch precedent). New `scenes/CommandDemo.tscn`, not
`run/main_scene`.

Verified for real through the Godot 4.7.2 editor: `SnapshotDemo.tscn
--selfcheck` unchanged; a temporary scratch console probe (removed after
use, the TASK-038/039 precedent) pinned `CommandDemoScene`'s scripted-input
hash sequence independently of Godot; `CommandDemo.tscn --selfcheck` through
Godot reproduced it byte-identically (`MATCH 0x649FA4D08E2931CA` at tick
20); a windowed `--screenshot`
(`docs/evidence/task-040-selection-and-preview.png`, committed) shows the
selection halo, route-preview dots, and `selected=agent 0` in the HUD. No
destination marker is visible in that frame because the scripted 3-cell
order had already completed by the capture tick (`Destination` correctly
clears on arrival, TASK-030's existing behaviour) — checked against the
known arrival tick, not a rendering gap.

`dotnet build CommandoWar.slnx -c Release`: unaffected, `0/0`. `dotnet test`:
unaffected, `297/297`. `dotnet build
src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`: `0/0`.

`src/CommandoWar.Client.Godot/README.md` gains a new section and an updated
file-layout table. `docs/11_BACKLOG.md` B-026 row moved to `review`.

Full detail: `docs/ledger/2026-09-17-TASK-040-selection-input-and-tactical-pause.md`.

## Review round 1 (2026-09-17)

Dave tried the scene and found selection fiddly (only registered near the
base of the agent sprite) and no visual difference between a hover preview
and an actually-set order, especially while paused. Both fixed pre-acceptance
(see the ledger detail file's "Review round 1" section): the click
hit-testing bug was a real cell-projection mismatch between how an agent's
circle is drawn and how a click was inverted back to a cell; the route
visualization now has three distinct states (hover/pending/committed, each
its own colour). A defensive replace-not-stack fix was also added to
`OnClick` (found while fixing the above, not asked for). Both scenes'
`--selfcheck` hashes are unchanged; screenshot re-captured to show the
previously-invisible paused/pending state.

Dave also raised multi-waypoint stacking with cancellation, explicitly
hedged as not yet a decision ("should fit into how we want the player to
control the agents overall"). Not built — flagged back via `AskUserQuestion`
as its own decision, since it has real sim-architecture weight (client-side
sequencing vs. an authoritative order queue) beyond this task's scope.

## Objective

Give the player real interactive control for the first time: click a friendly
agent to select it, click a cell to issue a `MoveTo` order against the live
`Simulation.step` loop, preview the proposed route before committing, and
pause/resume the fixed-step scheduler while composing orders — all through
ADR-0004's F# client-core / thin-C#-host split, reusing the
`CommandoWar.Client.Godot.Core` scaffold TASK-039 stood up.

## Why this task exists

B-026's dependencies (B-014, TASK-006) are both `done`, so this row is
selectable per `docs/11_BACKLOG.md` section 7. Chosen by Dave via
`AskUserQuestion` over B-028/B-030 proper/B-031 as the next P4 task: it builds
directly on TASK-039's new scaffold and is the natural next step now that a
live-rendering scene exists (TASK-039 built something to see; this task lets
the player act on it). It is also the first task that exercises `IClientScene`
beyond `Ready`/`Update`/`DrawList`/`HudText`, so it is the first real test of
ADR-0004's per-concern split for input, not only rendering.

## Central decisions (confirmed with Dave 2026-09-17 before drafting)

Four forks, put via `AskUserQuestion`:

1. **New scene, not an extension of `SnapshotDemo.tscn`.** TASK-039's task
   file forbade input in that scene and documents it as running unattended;
   changing that would invalidate its existing `--selfcheck`/`--screenshot`
   evidence. This task adds `scenes/CommandDemo.tscn` +
   `CwClientCore.CommandDemoScene` instead. Terrain-item construction and the
   depth-sort key move out of `DemoRenderScene.fs` into a small shared module
   both scenes use, rather than being duplicated.
2. **Command preview uses real `Pathfinding.find`**, not a straight line.
   `Pathfinding.find` (`src/CommandoWar.Sim/Pathfinding.fs`) is already pure,
   total, and deterministic — calling it read-only from the client for a
   hover preview needs no `CommandoWar.Sim` change and shows the actual route
   the sim would take, not an approximation.
3. **Single-unit selection only.** Fireteam/multi-select
   (`Command.moveToMany` already exists sim-side) is explicitly deferred to
   its own later backlog item, not folded in here.
4. **Orders can be issued while tactically paused**; the queued command
   applies once the sim resumes stepping. This matches docs/07's "tactical
   pause or slow motion while issuing orders" and docs/06's "tactical pause
   and command composition" — pause exists specifically to let the player
   compose an order without the world moving under them.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` — the per-concern table
  ("Raw input capture | C#", "Input -> typed command | F#"), the "no logic in
  C#" rule, and review trigger 1 (still open — this task does not need to
  discharge it, that needs Dave's own interactive editor session)
- `docs/04_SIMULATION_SPEC.md` section 13 (command envelope), section 16
  (recorded commands)
- `docs/05_COMMAND_AND_AGENT_AI.md` section 5 (appraisal stages a preview can
  and cannot anticipate — a preview is a route only, not an appraisal outcome)
- `docs/06_CONTENT_AND_PRESENTATION.md` sections 8 and 10 ("route and order
  previews", "clear selection and hover states", "tactical pause and command
  composition")
- `docs/07_VERTICAL_SLICE.md` section 6 ("tactical pause or slow motion",
  "unit and fireteam selection" — this task realises the unit half only,
  "proposed path and destination")
- `src/CommandoWar.Sim/Commands.fs` (`Command.moveTo`, `PlayerCommand`,
  `CommandRejection`)
- `src/CommandoWar.Sim/Pathfinding.fs` (`Pathfinding.find`, `PathResult`)
- `src/CommandoWar.Sim/Replay.fs` (`RecordedCommand` — reused as the
  pending-command queue shape, the `DemoDrive.commandsForTick` precedent)
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`,
  `Core/DemoRenderScene.fs` (the scaffold this task extends and partially
  refactors)
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs` (`CellToScreen`, the
  isometric projection constants this task adds the inverse of)
- `src/CommandoWar.Client.Godot/src/MainNode.cs` lines ~221-288 (the
  disposable TASK-004 spike's existing click-select/move/pause/`ScreenToCell`
  idiom — this task ports the *shape* of that interaction onto the ADR-0004
  split, not its C#-side logic placement)
- `src/CommandoWar.Headless/DemoScenario.fs` (starting agent positions:
  friendly 0 at (0,0), friendly 1 at (0,1), hostile 5 at (11,7))

## Dependencies

- B-014 (done), TASK-006 (done). TASK-039 (done) for the client-core scaffold.
  No other task selected.

## Allowed scope

- New shared module in `Core/` (e.g. `RenderShared.fs`) holding the
  terrain-`DrawItem` builder and the `(Cx + Cy)`-based depth-sort key,
  extracted from `DemoRenderScene.fs` so `CommandDemoScene` reuses it instead
  of duplicating it. `DemoRenderScene.fs` updated to call the shared module;
  its own behaviour, `--selfcheck` hash, and screenshot evidence unchanged.
- `Core/IClientScene.fs`: extend `IClientScene` with three new members,
  primitives only (the ADR-0004 interop idiom):
  - `OnClick: isLeftButton: bool * cellX: int * cellY: int -> unit`
  - `OnHover: cellX: int * cellY: int -> unit`
  - `OnTogglePause: unit -> unit`
  `DemoRenderScene` gains trivial no-op implementations of the three (it takes
  no input; the task file forbids changing that).
- New `Core/CommandDemoScene.fs`: `CommandDemoScene` implementing
  `IClientScene` over `DemoScenario.initialState()` (no canned command log —
  friendly agents sit still until the player orders them):
  - Selection: `OnClick` with `isLeftButton = true` selects a live friendly
    agent occupying `(cellX, cellY)` if one exists; otherwise, if an agent is
    already selected and the clicked cell differs from that agent's current
    cell, builds a `RecordedCommand` (`Command.moveTo`, a locally
    incrementing `CommandId`, `IssuedAtTick = state.Tick`, `Tick = state.Tick
    + 1L`, `Issuer = "player"`) and enqueues it for delivery on the next
    step. `OnClick` with `isLeftButton = false` clears the current selection
    (the `MainNode.cs` right-click-to-deselect precedent). A clicked cell
    outside `state.Terrain.Bounds` is ignored.
  - Preview: `OnHover` records the hovered cell and, if an agent is selected,
    calls `Pathfinding.find` from that agent's live position to the hovered
    cell; `PathResult.Found` cells (excluding the agent's own cell) become
    extra small, translucent `DrawItem`s (reusing `Kind = 1`, a smaller
    `Radius` and a distinct colour — no new `DrawItem.Kind` value, no
    `FSharpSceneHost.cs` rendering change needed). Any other `PathResult`
    case shows no preview.
  - Selection highlight: an extra `Kind = 1` item at the selected agent's
    live cell, larger `Radius`, low alpha, distinct colour — a halo behind
    the agent's own circle. Same "no new render-side Kind" approach.
  - Destination marker: once an agent's `AgentSnapshot.Destination` is
    `Some` (set by the existing Appraisal phase on `Accepted`, TASK-028 —
    no new mechanism), draw a distinct `Kind = 1` marker there.
  - `Update(deltaSeconds)`: the `DemoRenderScene` fixed-step precedent
    (20 Hz, 5-step catch-up cap), except stepping is skipped entirely while
    `OnTogglePause` has toggled the scene into a paused state; input
    (`OnClick`/`OnHover`) is still processed while paused, so an order can be
    composed and is delivered once the sim resumes.
  - A `CommandDemoDrive.runScriptedSelfCheck()` pure function (no Godot type):
    builds a `CommandDemoScene`, calls the interface directly (`Ready`,
    `OnClick(true, 0, 0)` to select friendly agent 0, `OnHover`/`OnClick(true,
    3, 0)` to issue a short move clear of the ridge and the impassable
    block, then steps for `DemoScenario.TickCount` ticks) and returns the
    tick-by-tick `TickHash[]` — the `DemoDrive.runFullSequence` precedent,
    this time exercising the input path instead of a canned log.
- `src/FSharpSceneHost.cs`: add `_UnhandledInput` forwarding a left/right
  `InputEventMouseButton` to `OnClick` and an `InputEventMouseMotion` to
  `OnHover`, both via a new `ScreenToCell` (the exact inverse of the existing
  `CellToScreen`, the `MainNode.cs` precedent) and an `InputEventKey` `Space`
  press to `OnTogglePause`. Extend `RunSelfCheck` to also recognise
  `SceneType == "CwClientCore.CommandDemoScene"` and dispatch to
  `CommandDemoDrive.runScriptedSelfCheck()`, pinning its own final hash
  (distinct from `DemoRenderScene`'s, since the scripted path issues a
  different order than `DemoScenario.commandLog()`).
- New `scenes/CommandDemo.tscn` using `FSharpSceneHost.cs`,
  `SceneType = "CwClientCore.CommandDemoScene"`. Not set as `run/main_scene`.
- `src/CommandoWar.Client.Godot/README.md` (new section + file-layout row,
  the TASK-039 precedent), including updated in-editor controls for this
  scene (left-click select/move, right-click deselect, Space pause).
- `docs/evidence/task-040-selection-and-preview.png` (committed screenshot).
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Forbidden scope

- No `CommandoWar.Sim`/`CommandoWar.Headless` change. `Command.moveTo`,
  `Simulation.step`, `Pathfinding.find`, and `AgentSnapshot.Destination`
  already carry everything this task needs.
- No fireteam/multi-select, no drag-select. One agent selected at a time.
- No rebindable-controls infrastructure (docs/06's "rebindable controls" is a
  separate, larger concern than this task's one hard-coded pause key).
- No accepted/refused/disposition reason text or UI (B-028's scope) — this
  task previews a *route*, never an appraisal outcome. A refused order still
  gets queued and delivered; the player only learns the outcome from the
  agent not moving, exactly as today's headless corpus behaves.
- No content-import pipeline / `Greybox.tscn` / Bridgehead map change
  (B-024/B-025, still blocked on B-043).
- No change to `DemoRenderScene`'s own behaviour, `--selfcheck` hash, or
  `SnapshotDemo.tscn`'s "runs unattended, no input" status beyond the
  mechanical no-op interface members and the shared-module extraction.
- No raw `Godot.InputEvent` passed into `Core/` — ADR-0004 permits it, but
  this task keeps the existing `MainNode.cs`/render-side precedent of
  resolving screen<->cell projection in C# and passing only primitives
  (button flag, cell coordinates) across the boundary, so `Core/` stays free
  of any `GodotSharp` reference.
- Nothing under `src/_scratch/`, `bench/`, `content/benchmarks/BASELINE.md`.

## Required work

1. Extract `RenderShared.fs` from `DemoRenderScene.fs`; confirm
   `DemoRenderScene`'s own `--selfcheck` hash is unchanged after the
   extraction.
2. Extend `IClientScene` with `OnClick`/`OnHover`/`OnTogglePause`; add
   no-op implementations to `DemoRenderScene`.
3. Implement `CommandDemoScene.fs` and `CommandDemoDrive.runScriptedSelfCheck`.
4. Add `ScreenToCell` and the three input-forwarding branches to
   `FSharpSceneHost.cs`; extend `RunSelfCheck`'s dispatch.
5. Create `scenes/CommandDemo.tscn`.
6. Pin `CommandDemoDrive.runScriptedSelfCheck`'s final hash from a real run.
7. Verify: `dotnet build` both `.slnx` files; run `--selfcheck` for both
   scene types through the real Godot editor; a windowed interactive session
   confirming selection, hover preview, issuing a move, pause/resume, and a
   committed screenshot.
8. Update `README.md`, backlog/ledger/state.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] Left-clicking a friendly agent selects it; left-clicking elsewhere with
      an agent selected issues a real `MoveTo` that the live sim accepts or
      refuses through the existing Appraisal phase (no bypass).
- [x] Hovering a cell with an agent selected shows the actual
      `Pathfinding.find` route, not a straight line.
- [x] `Space` pauses/resumes stepping; an order can be composed and issued
      while paused and is delivered once resumed.
- [x] `DemoRenderScene`'s own `--selfcheck` hash is unchanged after the
      `RenderShared.fs` extraction.
- [x] `CommandDemoScene`'s scripted `--selfcheck` reproduces its pinned hash
      deterministically.
- [x] `--screenshot` evidence committed under `docs/evidence/`, showing
      selection highlight and route preview (destination marker not present
      in this frame — the scripted order had already completed by the
      capture tick; confirmed correct, see Outcome).
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless` change;
      `Core/CommandoWar.Client.Godot.Core.fsproj` still has no `GodotSharp`
      reference; `SnapshotDemo.tscn`/`AppraisalDemo.tscn` unchanged.
- [x] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: unaffected, `0/0`.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`: `0/0`.
- Godot editor import + `--selfcheck` for both `SnapshotDemo.tscn` (unchanged
  hash) and `CommandDemo.tscn` (newly pinned hash).
- Windowed manual smoke test: select, hover-preview, issue a move, pause,
  resume, deselect; `--screenshot`.
- `git status --porcelain`: matches this task's allowed scope.

## Documentation updates

- This task file's Outcome/Review sections.
- `src/CommandoWar.Client.Godot/README.md`.
- `docs/11_BACKLOG.md` B-026 row.
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Additive and client-side: a new scene, a new F# scene type, three new
interface members with a no-op default elsewhere, and one shared-module
extraction with an unchanged hash to prove it was behaviour-neutral. No
`CommandoWar.Sim`/`CommandoWar.Headless` change. Revertible with `git revert`
in one step.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-17, "seems right"), after round 1 feedback (the
  click hit-testing bug and the missing hover/pending/committed route
  distinction) was addressed pre-acceptance. Multi-waypoint stacking and
  cancellation deliberately deferred to backlog row B-051, not part of this
  task's acceptance criteria. Re-verified before accepting: `dotnet build
  CommandoWar.slnx -c Release` 0/0, `dotnet test` 297/297, `dotnet build`
  the Godot client `.slnx` 0/0, `git status --porcelain` matched the
  expected scope (only the pre-existing unrelated `project.godot` edit left
  untouched).
