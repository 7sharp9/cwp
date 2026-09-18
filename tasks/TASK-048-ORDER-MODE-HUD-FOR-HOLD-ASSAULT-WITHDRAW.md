# TASK-048: XCOM-style order-mode HUD icons for Hold, Assault, and Withdraw

Status: done (drafted 2026-09-18; central decisions confirmed with Dave
2026-09-18, two `AskUserQuestion` rounds, before drafting; implemented and
self-verified 2026-09-18; accepted by Dave 2026-09-18)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises new backlog row B-059

## Outcome (2026-09-18)

Implemented as drafted. `IClientScene` gains
`OnOrderModeClick(index: int)`/`OrderMode(): int` (the `OnClick`/
`OnTogglePause` "primitives only" precedent); `DemoRenderScene` gets trivial
no-op implementations (`OnOrderModeClick` no-ops, `OrderMode` returns `0`).

`CommandDemoScene.fs` gains a mutable `orderMode` (`0 = MoveTo`, `1 = Hold`,
`2 = Assault`, `3 = Withdraw`) armed by `OnOrderModeClick` (re-clicking the
armed index disarms back to `0`) and consumed (reset to `0`) the instant an
order is actually issued. `OnClick`'s existing empty-cell branch now
dispatches on `orderMode` to `Command.moveTo`/`.hold`/`.assault`/`.withdraw`
(all already-existing sim-side constructors, identical signature shape, no
`CommandoWar.Sim` change). `pendingItems`' route-preview match extended from
`MoveTo target` alone to `MoveTo | Hold | Assault | Withdraw` (a single
active pattern over the shared `Cell` payload); `committedItems` needed no
change at all -- every one of the four writes `AgentSnapshot.Destination`
the same way (`Commitment.fs`'s own documented precedent), so the existing
route-draws-from-Destination logic already covered the new order types.

New `holdOutlineItems`: while `Hold` is armed and a valid cell is hovered
with an agent selected, four `Kind = 2` line segments trace the perimeter of
the exact `AppraisalConfig.HoldCoverSearchRadius` (2) Chebyshev square
`Appraisal.bestCoverNear` will search at Appraisal time -- Dave's explicit
design choice ("an outline of the hold area is shown in the UI"), drawn
unsorted after the depth sort (the `devItems`/`fireEffects` "no single
meaningful depth" precedent). `HudText` gains a `mode=hold|assault|withdraw`
suffix, omitted entirely at the default `0` (the `orderSuffix` precedent).

`FSharpSceneHost.cs` gains four new `Texture2D` (`OrderModeTextures`, Kenney
"Board Game Icons") and a fixed screen-space icon bar (`OrderModeIconRect`,
bottom-left, 48px icons with an 8px gap) owned entirely in C# -- a HUD-chrome
layout concern, not world-grid content (ADR-0004's "Raw input capture | C#"
/ "Render loop | C#" rows). `_UnhandledInput` checks `TryHitOrderModeIcon`
first on every left click; a miss falls through to the existing
`TryHitAgentCircle`/`ScreenToCell` cell-click path unchanged. `_Draw` appends
`DrawOrderModeBar()` last (always on top, the `devItems` precedent):
a dark backing square, the icon texture, and a gold border around whichever
mode `_scene.OrderMode()` reports as armed.

`CommandDemoDrive.runScriptedSelfCheck` extended (after the existing
`MoveTo(3,0)` sequence) to select friendly agent 1, arm `Hold` via
`OnOrderModeClick(1)`, and issue `Hold(2,1)` -- proving the icon-click
dispatch reaches a real `Command.hold` through the identical path a player
uses, not just a direct sim-side call (`SimulationTests` already proves the
sim side, TASK-047). This is a genuine new tick-by-tick trace, so
`CommandDemo.tscn`'s own `--selfcheck` hash moved (re-pinned
`0x00D3D471EF7354BC`, confirmed `MATCH` through the real Godot 4.7.2
editor); `SnapshotDemo.tscn`/`AppraisalDemo.tscn` reconfirmed unchanged
(arming a HUD icon has no effect unless an order is actually issued through
it, and neither scripted flow touches `CommandDemoScene`).

Screenshot priming (`--screenshot`) extended to also select agent 1, arm
`Hold`, and hover `(2,1)` without clicking -- the captured frame
(`docs/evidence/task-048-order-mode-hud.png`, committed) shows the pending
orange `MoveTo` route for agent 0, the `Hold` icon's gold highlight, and the
hover-preview outline together.

New art: `hud_move.png`/`hud_hold.png`/`hud_assault.png`/`hud_withdraw.png`
(Kenney "Board Game Icons", CC0, live-checked against kenney.nl --
downloaded and unzipped to inspect actual file names/shapes, not assumed
from memory -- `art/LICENSE-THIRD-PARTY.md` updated).

`dotnet build` both `.slnx` (main Release; Godot client Debug) `0/0`;
`dotnet test` `336/336` (unaffected, no `CommandoWar.Sim` change); `--
corpus` 16/16 (unaffected); `git status --porcelain` matches this task's
allowed scope (five modified files, four new `art/hud_*.png`, one new
evidence screenshot).

Full detail: `docs/ledger/2026-09-18-TASK-048-order-mode-hud-for-hold-assault-withdraw.md`.

## Objective

Give the player a way to actually issue `Hold`/`Assault`/`Withdraw` orders
(sim-side complete since TASK-047, backlog B-030, but with no client path to
issue them -- the TASK-037 precedent) through `CommandDemoScene`, the same
scene TASK-040 gave `MoveTo` selection/click/preview to.

## Why this task exists

B-030 (Hold/Assault/Withdraw executors and ammunition) was accepted by Dave
2026-09-18 (TASK-047), but flagged its own client gap: "No client-facing UI
for issuing the three new orders ... that's the natural next client task if
Dave wants it." Chosen by Dave directly (no `AskUserQuestion` needed -- he
named it explicitly) over B-032 (demolition objective/extraction, newly
unblocked) and B-058 (agent movement speed, unscoped).

## Central decisions (confirmed with Dave 2026-09-18 before drafting)

Two rounds, put via `AskUserQuestion`:

1. **Order-mode selection UI: XCOM-style HUD icons**, not keyboard chords or
   modifier-click. Dave's explicit direction ("Im thinking icons on the hub
   similar to xcom games etc"), overriding the recommended
   keyboard-armed-mode default.
2. **`Hold`'s target: click a cell** (not the agent's own current cell, no
   click) -- **and an outline of the hold area is shown in the UI**, Dave's
   own addition beyond the original either/or question. This became the
   `holdOutlineItems` Chebyshev-square perimeter.
3. **Icon art: real Kenney icon art** (a live kenney.nl check, the
   TASK-041/046 precedent), not drawn glyphs/letters reusing existing
   primitives -- Dave's explicit choice over the recommended lower-scope
   default.
4. **Click routing: a new `OnOrderModeClick`/`OrderMode` pair on
   `IClientScene`**, with the C# host owning the fixed icon rects and their
   hit-testing entirely (checked first, falling through to the existing
   world-cell `OnClick` on a miss) -- Dave's confirmed choice over F# owning
   generic screen-space hit-testing too.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` -- the per-concern table
  ("Raw input capture | C#", "Render loop | C# `_Draw`, view model from F#")
- `src/CommandoWar.Sim/Commands.fs` (`Command.hold`/`.assault`/`.withdraw`,
  already implemented by TASK-047, identical shape to `Command.moveTo`)
- `src/CommandoWar.Sim/Domain.fs` (`PlayerIntent.Hold`/`.Assault`/`.Withdraw`,
  all bare `Cell` targets, the `MoveTo` precedent)
- `src/CommandoWar.Sim/Appraisal.fs` (`AppraisalConfig.HoldCoverSearchRadius`,
  `Appraisal.bestCoverNear` -- the Chebyshev search the Hold outline previews)
- `src/CommandoWar.Sim/Commitment.fs` (confirms every one of the four order
  types writes `AgentSnapshot.Destination` the same way `MoveTo` does, so
  `committedItems` needed no change)
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`,
  `Core/CommandDemoScene.fs` (TASK-040's selection/click/preview scaffold
  this task extends)
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs` (`CellToScreen`,
  `ScreenToCell`, `TryHitAgentCircle`, `_UnhandledInput`, `_Draw` -- the
  existing click-routing and immediate-mode draw precedents this task's icon
  bar and hit-test extend)
- `src/CommandoWar.Client.Godot/art/LICENSE-THIRD-PARTY.md` (the
  TASK-041/046 live-checked-art precedent)

## Dependencies

- B-030 (done, TASK-047). No other task selected.

## Allowed scope

- `Core/IClientScene.fs`: two new `IClientScene` members,
  `OnOrderModeClick: index: int -> unit` and `OrderMode: unit -> int`
  (primitives only). `DemoRenderScene` gains trivial no-op implementations.
- `Core/CommandDemoScene.fs`: `orderMode` state, `OnOrderModeClick`/
  `OrderMode` implementations, `OnClick`'s order-dispatch extension,
  `hoveredCell` tracking, `holdOutlineItems`, `pendingItems`' pattern
  extension, `HudText`'s `mode=` suffix, and
  `CommandDemoDrive.runScriptedSelfCheck`'s extended scripted sequence.
- `src/FSharpSceneHost.cs`: `OrderModeTextures`, `OrderModeIconRect`,
  `TryHitOrderModeIcon`, the `_UnhandledInput` icon-click branch (checked
  first), `DrawOrderModeBar` (called from `_Draw`), the re-pinned
  `CommandDemoScene` `--selfcheck` expected hash, and the extended
  `--screenshot` priming sequence.
- New `src/CommandoWar.Client.Godot/art/hud_move.png`/`hud_hold.png`/
  `hud_assault.png`/`hud_withdraw.png` (Kenney "Board Game Icons", CC0) plus
  `art/LICENSE-THIRD-PARTY.md` update.
- `src/CommandoWar.Client.Godot/README.md` (new section, the TASK-040/046
  precedent).
- `docs/evidence/task-048-order-mode-hud.png` (committed screenshot).
- `docs/11_BACKLOG.md` (new B-059 row), `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- No `CommandoWar.Sim`/`CommandoWar.Headless` change. `Command.hold`/
  `.assault`/`.withdraw`, `Appraisal.bestCoverNear`, and
  `AppraisalConfig.HoldCoverSearchRadius` already carry everything this task
  needs.
- No UI for `Suppress` (already sim-side complete since TASK-037; not named
  in Dave's task selection, a separate follow-up if wanted).
- No rebindable-controls infrastructure, no icon tooltips/labels beyond the
  icon art itself.
- No change to `SnapshotDemo.tscn`/`AppraisalDemo.tscn` behaviour or their
  own `--selfcheck` hashes.
- No content-import pipeline / `Greybox.tscn` / Bridgehead map change.
- Nothing under `src/_scratch/`, `bench/`, `content/benchmarks/BASELINE.md`.

## Required work

1. Extend `IClientScene` and `DemoRenderScene`'s no-op implementations.
2. Implement `CommandDemoScene.fs`'s order-mode state, dispatch, Hold
   outline, and HUD suffix; extend `CommandDemoDrive.runScriptedSelfCheck`.
3. Live-check kenney.nl for a suitable icon pack (downloaded, unzipped,
   inspected); copy the four chosen files into `art/`; update
   `LICENSE-THIRD-PARTY.md`.
4. Extend `FSharpSceneHost.cs`: icon textures, fixed rects, hit-testing,
   drawing, re-pinned `--selfcheck` hash, extended `--screenshot` priming.
5. Verify: `dotnet build` both `.slnx`; Godot editor asset re-import; all
   three scenes' `--selfcheck` through the real Godot editor; a windowed
   `--screenshot`.
6. Update `README.md`, backlog (new B-059 row)/ledger/state.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] Four order-mode icons (Move/Hold/Assault/Withdraw) render as a fixed
      HUD bar; clicking one arms that mode (gold highlight), clicking it
      again disarms back to `MoveTo`.
- [x] With a non-default mode armed, clicking a valid target cell with an
      agent selected issues the corresponding real `Command.hold`/`.assault`/
      `.withdraw` through the live `Simulation.step` (no bypass); the armed
      mode resets to `MoveTo` after issuing.
- [x] While `Hold` is armed and hovering, an outline of the
      `HoldCoverSearchRadius` search area is shown.
- [x] `CommandDemoScene`'s scripted `--selfcheck` reproduces its re-pinned
      hash deterministically through the real Godot editor;
      `SnapshotDemo.tscn`/`AppraisalDemo.tscn` confirmed unchanged.
- [x] `--screenshot` evidence committed showing the icon bar, the armed-icon
      highlight, and the Hold outline.
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless` change; `dotnet test`
      unaffected (`336/336`); `-- corpus` unaffected (`16/16`).
- [x] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: unaffected, `0/0`.
- `dotnet test`: unaffected, `336/336`.
- `dotnet run --project src/CommandoWar.Headless -- corpus`: unaffected,
  `16/16`.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`: `0/0`.
- Godot editor `--editor --headless --quit` asset re-import (new `art/hud_*.png`).
- `--selfcheck` for all three scene types through the real Godot 4.7.2
  editor: `CommandDemo.tscn` newly pinned `MATCH`;
  `SnapshotDemo.tscn`/`AppraisalDemo.tscn` unchanged `MATCH`.
- Windowed `--screenshot` evidence.
- `git status --porcelain`: matches this task's allowed scope.

## Documentation updates

- This task file's Outcome section.
- `src/CommandoWar.Client.Godot/README.md`.
- `src/CommandoWar.Client.Godot/art/LICENSE-THIRD-PARTY.md`.
- `docs/11_BACKLOG.md` (new B-059 row).
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Additive and entirely client-side: two new `IClientScene` members with a
no-op default elsewhere, new HUD-chrome drawing/hit-testing in the existing
C# host, and four new licensed art assets. No `CommandoWar.Sim`/
`CommandoWar.Headless` change. Revertible with `git revert` in one step.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-18).
