# TASK-052: Hover highlight and a larger fixed scale

Owner: Dave (implementing agent session)
Source revision: `main`, after TASK-051's acceptance this session.
Environment: `dotnet` `10.0.303`, Godot `4.7.2.stable.mono` (real editor),
Windows 11.

## Selection

Dave named both backlog rows directly ("pick B-053/B-054") after TASK-051's
acceptance -- no `AskUserQuestion` needed for the selection itself, the
TASK-048 precedent for a task Dave names explicitly.

## Central decision

One `AskUserQuestion` round: B-054's own backlog text named an undecided
fork ("likely a camera zoom/pan or a larger fixed scale"). Dave chose the
recommended default -- a larger fixed scale, not interactive zoom/pan.

## Investigation before drafting

Read `FSharpSceneHost.cs`'s `_UnhandledInput` in full and found the mouse
motion branch (`OnHover`) calls `ScreenToCell` directly, while the mouse
*button* branch (`OnClick`) already tries `TryHitAgentCircle` first,
falling back to `ScreenToCell` only on a miss -- the TASK-040 fix for an
agent's circle drawing `TileH/2` above `ScreenToCell`'s own diamond-centre
assumption. This meant a straightforward implementation of B-053 (highlight
whatever `OnHover` resolves to, if it's a friendly) would silently inherit
the exact bug TASK-040 fixed for clicks, just for hover instead -- the
highlight would fail to arm near the top of a visible agent's circle. Found
by reading the existing code side by side, not by testing first.

Also read `DrawTerrainTile`/`DrawAgentFigure` to confirm which draw
primitives auto-scale with `TileW`/`TileH` (terrain tiles, since
`DrawTerrainTile` uses `TileW` directly as its own width) and which do not
(every `Kind = 1`/`5` pixel radius, and `Kind = 3` font size, are literal
screen pixels set from F# `DrawItem.Radius`, independent of the isometric
projection constants). This is why doubling `TileW`/`TileH` alone would
have left agent figures and markers looking disproportionately small
against the now-bigger terrain tiles.

## Changes

- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`:
  - New `agentRadius = 20.0f` and `haloRadius = 34.0f` (class-level `let`
    bindings, doubling the previous `10.0f`/`17.0f` literals, `haloRadius`
    keeping the original 1.7x ratio).
  - `renderVitals`'s `Incapacitated`/`Alive` branches and `haloItems` now
    reference `agentRadius`/`haloRadius` instead of the literals.
  - The TASK-051 fog-of-war ghost ring/label (`RenderShared.cellRing`/
    `cellLabel` in the `Hostile, Some(...) when lastSeenTick < devFrame.Tick`
    branch) now sizes from `agentRadius` (`agentRadius` for the ring,
    `agentRadius * 0.45f` for the label) instead of the literals `10.0f`/
    `9.0f`.
  - New `hoverHighlightItems`: `hoveredCell |> Option.bind friendlyAt |>
    Option.map (fun a -> RenderShared.cellRing a.Position (0.95f, 0.95f,
    1.0f) 0.8f (agentRadius + 3.0f))`, folded into `sorted`'s `Array.concat`
    alongside the existing per-cell item lists.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`:
  - `TileW` `44f -> 88f`, `TileH` `22f -> 44f`, `Origin` `(450f, 60f) ->
    (552f, 110f)`.
  - Mouse-motion handler: `Vector2I cell = TryHitAgentCircle(mm.Position,
    out Vector2I hit) ? hit : ScreenToCell(mm.Position);` -- the exact
    fallback the click handler already used.
  - `case 5`'s `DrawArc` stroke width `2.5f -> 3.5f` (a small legibility
    bump alongside the doubled scale; this case now serves two markers --
    the fog-of-war ghost and the new hover ring -- so its comment was
    generalised rather than naming only TASK-051).
- `src/CommandoWar.Client.Godot/README.md`: new section.
- `docs/evidence/task-052-scale-and-hover.png`: new screenshot.

No `CommandoWar.Sim`/`CommandoWar.Headless` file touched. No
`SnapshotDemo.tscn`/`DemoRenderScene.fs` change.

## Verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet test`: `342/342` (unaffected).
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: `0/0`.
- A temporary `dotnet fsi` scratch probe (`hover_probe.fsx`, session
  scratchpad, removed after use) drove `CommandDemoScene` directly and
  confirmed:
  - no hover: 0 rings.
  - `OnHover(0, 0)` (friendly agent 0's own cell): 1 ring at `(0,0)`,
    `radius = 23.0` (`agentRadius + 3`), `alpha = 0.80`, colour
    `(0.95, 0.95, 1.00)` -- the light neutral tint, distinct from the
    fog-of-war ghost's hostile-coloured ring.
  - `OnHover(5, 5)` (empty terrain): 0 rings.
  - `OnHover(11, 7)` (the hostile's own cell, never contacted): 0 rings --
    confirms the highlight never bypasses fog of war (a hostile is never
    highlighted, contacted or not, since only `friendlyAt` matches).
  - Selecting *and* hovering the same agent: both the halo and the hover
    ring render together with no conflict.
- Re-ran the TASK-051 `fog_probe.fsx` unchanged and confirmed the identical
  tick-by-tick sequence (hidden through tick 20; live from tick 21; ghost
  `alpha=0.90` from tick 63; `alpha=0.68` at tick 82; expired at tick 122)
  -- proves the `agentRadius`-based refactor of the ghost ring's sizing
  changed no observable behaviour.
- Godot editor `--selfcheck`, headless, through the real 4.7.2 editor:
  - `SnapshotDemo.tscn`: `MATCH 0xF422ACB8D5A86FF0`, exit 0.
  - `CommandDemo.tscn`: `MATCH 0x00D3D471EF7354BC`, exit 0.
  - `AppraisalDemo.tscn`: `MATCH 0x194805888CBE240D` (format 11), exit 0.
  All three unaffected, as expected for a render-only change.
- Windowed screenshot: `"$GODOT" --path . scenes/CommandDemo.tscn --
  --screenshot docs/evidence/task-052-scale-and-hover.png --dev-overlay`.
  `--dev-overlay` was added deliberately (not part of the default
  screenshot-priming sequence) to capture the HUD's worst-case three-line
  height alongside the new scale -- the exact case this task's `Origin.Y`
  change targets. Inspected the saved PNG directly: the play area now
  fills most of the window, and the three HUD lines (ending around
  `y=70`) sit with clear margin above the grid's own topmost point
  (around `y=95-100`) -- no overlap, unlike before this task. The
  never-contacted hostile is visible in this capture (red figure, bottom
  right) because `--dev-overlay` deliberately bypasses fog of war
  (TASK-051's own ground-truth precedent) -- expected, not a regression.
- `git status --porcelain`: matches the task's allowed scope exactly --
  `CommandDemoScene.fs`, `FSharpSceneHost.cs`, `README.md` modified;
  `docs/evidence/task-052-scale-and-hover.png` new; no
  `CommandoWar.Sim`/`CommandoWar.Headless`/`IClientScene.fs` file touched
  (`Kind = 5` needed no further doc-comment change beyond TASK-051's).

## Documents updated

- `tasks/TASK-052-HOVER-HIGHLIGHT-AND-SCALE-POLISH.md` (created, `Outcome`
  filled in).
- `docs/11_BACKLOG.md` (B-053 and B-054 rows: `proposed -> done`).
- `src/CommandoWar.Client.Godot/README.md` (new section).
- `docs/12_PROGRESS_LEDGER.md` (this detail file; index row added).
- `PROJECT_STATE.yaml` (`active_work` updated).

## Review

- Reviewer: Dave.
- Accepted: pending.
