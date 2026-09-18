# 2026-09-18: TASK-048 order-mode HUD for Hold, Assault, Withdraw

## Context

B-030 proper (TASK-047: Hold/Assault/Withdraw executors and ammunition) was
accepted by Dave 2026-09-18, but shipped sim-side only. Its own row flagged
"no client-facing UI for issuing the three new orders ... that's the
natural next client task if Dave wants it." Dave chose this directly over
B-032 (demolition objective/extraction, newly unblocked) and B-058 (agent
movement speed, unscoped).

## Central decisions (via `AskUserQuestion`, two rounds, 2026-09-18)

1. Order-mode selection UI: XCOM-style HUD icons (Dave's explicit direction:
   "Im thinking icons on the hub similar to xcom games etc"), overriding the
   recommended keyboard-armed-mode default.
2. `Hold` target: click a cell, **and show an outline of the hold area in
   the UI** (Dave's own addition beyond the original either/or question).
3. Icon art: real Kenney icon art (a live kenney.nl check), not drawn
   glyphs/letters — Dave's explicit choice over the lower-scope default.
4. Click routing: a new `OnOrderModeClick`/`OrderMode` pair on
   `IClientScene`, with C# owning the fixed icon rects and hit-testing
   entirely, checked first, falling through to the existing world-cell
   `OnClick` on a miss.

## Live art check (2026-09-18)

Checked kenney.nl live, not assumed from memory (the TASK-041/046
precedent): neither existing pack in the project (Isometric Miniature
Prototype: terrain/character art; Particle Pack: fire effects) has
command-vocabulary icons. Fetched the "Board Game Icons" (v1.1, CC0) preview
image, cropped/zoomed it to identify candidate shapes, then downloaded and
unzipped the actual pack (`kenney_board-game-icons.zip`, found via the
page's own donate-flow `Continue without donating...` link) to confirm real
file names: `shield.png` (Hold), `sword.png` (Assault), `arrow_right.png`
(Move), `arrow_counterclockwise.png` (Withdraw — reads as "pull back").
Copied the 64px variants into `src/CommandoWar.Client.Godot/art/` as
`hud_move.png`/`hud_hold.png`/`hud_assault.png`/`hud_withdraw.png`;
`LICENSE-THIRD-PARTY.md` updated with the pack's CC0 license and file
mapping.

## Implementation (2026-09-18)

`IClientScene` gains `OnOrderModeClick(index: int)`/`OrderMode(): int`
(primitives only, the `OnClick`/`OnTogglePause` precedent); `DemoRenderScene`
gets no-op implementations.

`CommandDemoScene.fs`: a mutable `orderMode` (`0 = MoveTo`, `1 = Hold`,
`2 = Assault`, `3 = Withdraw`), armed by `OnOrderModeClick` (re-clicking the
armed index disarms back to `0`) and reset to `0` the instant an order is
issued (an XCOM ability-consumed-on-use idiom, a design call made without
asking Dave since it is a sensible, low-risk UX default consistent with the
XCOM framing he set). `OnClick`'s existing empty-cell branch dispatches on
`orderMode` to `Command.moveTo`/`.hold`/`.assault`/`.withdraw` — all four
already existed sim-side since TASK-047, identical signature shape to
`moveTo`, so this is a pure client-side dispatch with zero
`CommandoWar.Sim` change. `pendingItems`'s route-preview match extended from
`MoveTo target` alone to a single active pattern over
`MoveTo | Hold | Assault | Withdraw`'s shared `Cell` payload;
`committedItems` needed **no change at all** — every one of the four writes
`AgentSnapshot.Destination` identically (`Commitment.fs`'s own documented
precedent), so the existing Destination-driven route draw already covered
the new order types for free.

New `holdOutlineItems`: while `Hold` is armed, an agent is selected, and a
valid cell is hovered, four `Kind = 2` line segments trace the perimeter of
the exact `AppraisalConfig.HoldCoverSearchRadius` (2) Chebyshev square
`Appraisal.bestCoverNear` will search at Appraisal time — an honest preview
of the candidate region (the actual resolved cell depends on live threat
pressure, unknowable client-side before Appraisal runs), not a guess at the
final answer. Drawn unsorted after the depth sort, the `devItems`/
`fireEffects` "no single meaningful depth for a multi-cell shape" precedent.
`HudText` gains a `mode=hold|assault|withdraw` suffix, omitted entirely at
the default `0` (the `orderSuffix` precedent).

`FSharpSceneHost.cs`: four new `Texture2D` (`OrderModeTextures`) loaded the
same way every other art asset is (static, loaded once); a fixed
screen-space icon bar (`OrderModeIconRect` — bottom-left, 48px icons, 8px
gap, computed from a shared origin/size/gap, not per-icon magic numbers) —
owned entirely in C# per Decision 4 (HUD chrome, not world-grid content,
ADR-0004's "Raw input capture | C#" / "Render loop | C#" rows).
`TryHitOrderModeIcon` is checked first in `_UnhandledInput` on every left
click; a miss falls through to the existing `TryHitAgentCircle`/
`ScreenToCell` cell-click path completely unchanged. `_Draw` appends
`DrawOrderModeBar()` last (the `devItems` "always on top" precedent): a
dark backing square per icon for legibility over any terrain colour, the
icon texture, and a gold 3px border around whichever mode
`_scene.OrderMode()` reports armed (a thin white border on the other three).

`CommandDemoDrive.runScriptedSelfCheck` extended (after the existing
`MoveTo(3,0)` sequence, unchanged) to select friendly agent 1 (at `(0,1)`),
arm `Hold` via `OnOrderModeClick(1)`, hover, and click `(2,1)` — issuing a
real `Command.hold` through the identical icon-click path a player uses,
not a direct sim-side call. `SimulationTests` (TASK-047) already proves the
sim side; this proves the client dispatch wiring on top of it, the
strongest evidence bar this project uses (a pinned, regenerable
`--selfcheck` hash) rather than a one-off scratch probe.

Screenshot priming (`--screenshot`) extended identically minus the final
click (select agent 1, arm `Hold`, hover `(2,1)`, no click) so the captured
frame shows the pending orange `MoveTo` route for agent 0 *and* the armed
`Hold` icon's gold highlight *and* the hover-preview outline together — this
task's two new pieces of visible evidence alongside the pre-existing
pending-route proof.

## Verification (2026-09-18)

```
$ dotnet build CommandoWar.slnx -c Release
Build succeeded. 0 Warning(s), 0 Error(s)

$ dotnet test --no-build -c Release
Passed! - Failed: 0, Passed: 336, Skipped: 0, Total: 336

$ dotnet run --project src/CommandoWar.Headless -c Release --no-build -- corpus
OK - all 16 entries match their committed tables

$ dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug
Build succeeded. 0 Warning(s), 0 Error(s)
```

Godot 4.7.2 editor: `--editor --headless --quit` re-imported the four new
`art/hud_*.png`. Real `--selfcheck` runs, all `MATCH`, exit 0:

```
$ "$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck
# command-demo-scene self-check (scripted MoveTo(3,0) + Hold(2,1) via order-mode icon)
...
tick=20 hash=0x00D3D471EF7354BC
MATCH expected final hash 0x00D3D471EF7354BC at tick 20

$ "$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck
tick=20 hash=0x8E93B48D07AE9CBD
MATCH expected final hash 0x8E93B48D07AE9CBD at tick 20

$ "$GODOT" --headless --path . scenes/AppraisalDemo.tscn -- --selfcheck
hash 0x194805888CBE240D (format 11)
MATCH expected hash 0x194805888CBE240D, agent 0 Refused / agent 1 Accepted
```

A windowed `--screenshot` (`docs/evidence/task-048-order-mode-hud.png`,
committed) confirms visually: the four-icon bar bottom-left, `Hold` armed
with a gold border, `mode=hold` in the HUD text line, and the hover-preview
outline square around the hovered cell.

`git status --porcelain`: five modified files
(`Core/CommandDemoScene.fs`, `Core/DemoRenderScene.fs`,
`Core/IClientScene.fs`, `art/LICENSE-THIRD-PARTY.md`,
`src/FSharpSceneHost.cs`), five new files (four `art/hud_*.png` plus the
evidence screenshot) — matches the task file's allowed scope exactly.

### Evidence

- `dotnet build`/`dotnet test`/`-- corpus` command output above.
- Godot `--selfcheck` output for all three scenes above.
- `docs/evidence/task-048-order-mode-hud.png`.

### Deviations and unresolved issues

None found during implementation. `committedItems` needing zero change (see
Implementation above) was a pleasant confirmation that TASK-047's
`Commitment.fs` "Destination exactly as MoveTo does" design held up exactly
as documented, not a deviation.

### Documents updated

- `tasks/TASK-048-ORDER-MODE-HUD-FOR-HOLD-ASSAULT-WITHDRAW.md` created,
  implemented, and self-verified (`drafted -> review`).
- `docs/11_BACKLOG.md`: new B-059 row (`proposed -> review`).
- `src/CommandoWar.Client.Godot/README.md`: new section.
- `src/CommandoWar.Client.Godot/art/LICENSE-THIRD-PARTY.md`: new pack entry.
- `docs/12_PROGRESS_LEDGER.md`: this row.
- `PROJECT_STATE.yaml`: `active_work` updated.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-18).
