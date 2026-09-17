## 2026-09-17 - TASK-043 - Developer overlay rendering over live input (B-029 proper)

### Why

B-029's dependencies (B-012a/TASK-011, B-012/TASK-016, B-017/TASK-028,
B-027/TASK-039) are all `done`. Selected as the next P4 task via
`AskUserQuestion`, over B-030 proper, B-031, and B-051 (B-052/B-053 available
via "Other", not chosen). docs/06 section 11 itself frames this task's job:
"The Godot developer overlay (backlog B-029) becomes a third renderer of the
same frame" that `DiagnosticRender.Ascii`/`.Svg`/`.Html` already render.

### Central decision (the task's own, confirmed with Dave via `AskUserQuestion`)

docs/06 section 11's developer-facing overlay list has eight items. Two need
interop primitives `DrawItem` does not have: line-of-sight rays/occluders
(needs a line-segment primitive) and logical grid coordinates (needs a
per-cell text-label primitive). Presented as a fork — a narrower first cut
reusing only existing primitives, or the full set adding both primitives now
— Dave chose the full set, accepting the L sizing this implies over the
backlog's M estimate.

### Design

`Diagnostics.frame`/`.frameOf` (`src/CommandoWar.Sim/Diagnostics.fs`) already
carry everything docs/06 section 11's developer-facing list asks for, as
`Overlay` cases: `Reserved`, `Obstructed`, `KnownContact`, `OrderAppraisal`
(with `exposedCells`), `AgentCommitment`, `AgentSuppression`, `AgentStress`,
`FireLine`. No `CommandoWar.Sim`/`CommandoWar.Headless` change was needed —
this task's entire job was to consume that existing, already-tested data from
`CommandDemoScene`, the only scene with live input.

**`Core/IClientScene.fs`**: `DrawItem` gains `Cx2: float32`, `Cy2: float32`
(meaningful only for a new `Kind = 2`, a line segment from `(Cx,Cy)` to
`(Cx2,Cy2)`, `Radius` as line width) and `Text: string` (meaningful only for
a new `Kind = 3`, a text label at `(Cx,Cy)`, `Radius` as font size). Doc
comment extended to describe both. `IClientScene` gains `abstract
OnToggleDevOverlay: unit -> unit`.

**`Core/RenderShared.fs`**: three new helpers building the two new `DrawItem`
kinds (`cellMarker` reuses the existing translucent-`Kind=1`-circle shape,
the selection-halo/route-dot precedent, for point markers — no new primitive
needed there; `lineMarker`; `cellLabel`); a developer-facing `devReasonText`/
`devCommitmentText` (private) and public `devAgentText (overlays: Overlay[])
(agent: AgentId) : string`, deliberately mirroring `DiagnosticRender`'s
existing private hyphenated wording (`"route-too-exposed threat-agent-3"`)
rather than the player-facing punctuated text TASK-042 added
(`"route too exposed (threat: agent 3)"`) — the same docs/06 player/developer
text-boundary precedent. `devAgentText` reads `AgentCommitment` (always
present per agent), `AgentSuppression`/`AgentStress` (sparse, default 0 when
absent), and `OrderAppraisal` (default `"no-order"`/0 exposed cells when the
agent holds no appraised order) directly from the `Overlay[]` passed in — no
new state anywhere.

**`Core/CommandDemoScene.fs`**:
- `devFrame: DiagnosticFrame` set from `Diagnostics.frame state` in `Ready()`
  and `Diagnostics.frameOf r` inside `stepOnce` after every step — the
  identical frame every headless developer renderer already builds and
  tests against, now built from this scene's own live state instead of a
  replayed corpus entry.
- `devOverlay: bool` (default `false`), flipped by `OnToggleDevOverlay()`.
- `losRay: (Cell * Cell * bool) option`, recomputed on every `OnHover`
  (regardless of whether the overlay is currently shown, so it is correct
  the instant `F1` is pressed) via `Sight.trace state.Terrain pos cell` —
  `Sight.trace` is a pure function over `Terrain` plus two cells, already
  freely readable client-side (the existing `Pathfinding.find` precedent;
  `Terrain` is static scenario geometry, not per-agent authoritative state,
  so docs/03 section 12's `RenderSnapshot`-only rule does not apply to it).
  A blocked trace's stored end cell is the traced `Blocker`, not the hovered
  cell, so the rendered ray visibly stops at the occluder. Reset to `None`
  on any selection change (the `heldOrderText`/TASK-042 precedent), so a
  stale ray from a previously selected agent never lingers.
- `DrawList()`, when `devOverlay` is true, additionally emits: one
  `cellLabel` per terrain cell (`"x,y"`, dim grey); one `cellMarker` per
  `Reserved` (cyan) / `Obstructed` (red) overlay entry; one `cellMarker` per
  `KnownContact` entry (pale yellow, at `LastKnownCell` — a genuine "known
  vs. authoritative" comparison, since this demo's `agentItems` already
  draws every agent, hostile included, at its true position unconditionally,
  no fog-of-war); one `cellMarker` per exposed cell from the *selected*
  agent's `OrderAppraisal` (orange); one `lineMarker` per `FireLine` entry
  (green if hit, grey if miss); one `lineMarker` for `losRay` if present
  (green if visible, red if occluded).
- `HudText()`'s first line gains an always-visible `draws=<state.Random.
  Draws>` field (not gated behind the toggle — cheap, and satisfies "tick,
  state hash, and random draw counter" as a baseline HUD fact). A second
  `"\n[dev] ..."` line, built by `RenderShared.devAgentText devFrame.
  Overlays id`, appears only when `devOverlay` is true and an agent is
  selected (`"no agent selected"` when the overlay is on with nothing
  selected); omitted entirely when the overlay is off, so TASK-042's
  existing `HudText()` shape (plus `draws=`) is otherwise unchanged.

**`Core/DemoRenderScene.fs`**: `OnToggleDevOverlay() = ()` — no live input to
overlay, the `OnTogglePause` precedent exactly.

**`src/FSharpSceneHost.cs`**: `_Draw`'s dispatch becomes a `switch` on
`item.Kind`: `0` terrain (unchanged), `2` `DrawLine(pos, CellToScreen(item.
Cx2, item.Cy2), color, item.Radius)`, `3` `DrawString(ThemeDB.FallbackFont,
pos, item.Text, ..., (int)item.Radius, color)`, `default` the existing
agent/marker branch (unchanged). `_UnhandledInput` gains an `InputEventKey`
case for `Key.F1` calling `_scene.OnToggleDevOverlay()`, alongside the
existing `Space` -> `OnTogglePause()` case. A new opt-in `--dev-overlay` CLI
flag (`ParseCommandLine`), checked once in `_Ready()` right after the
existing `CommandDemoScene`-only screenshot-priming block, calls `_scene.
OnToggleDevOverlay()` when present — kept as a *separate* flag rather than
folded into the priming, so a plain `--screenshot` capture still reproduces
TASK-042's existing evidence unchanged.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0/0` (unaffected — no `CommandoWar.Sim`/`Headless` change).
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: `0/0`.
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Release`
  - Result: `0/0`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `297/297` (unaffected).
- Command: `"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck`
  - Result: `MATCH expected final hash 0x11B06E6EDE0C52E3 at tick 20`, exit `0`
    — unchanged.
- Command: `"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck`
  - Result: `MATCH expected final hash 0x649FA4D08E2931CA at tick 20`, exit
    `0` — unchanged.
- Command: a temporary scratch `dotnet fsi` script (removed after use, the
  TASK-038/042 precedent) referencing the built `Core`/`Sim`/`Headless` DLLs
  directly, instantiating the real `CommandDemoScene`, exercising selection,
  hover, the overlay toggle, and `StepTicksHeadless`.
  - Result (abridged):
    ```
    -- overlay off, no selection --
    hud: tick 0   hash 0x0...0   draws 0   agents 3   running   selected=none
    drawlist count: 99

    -- selected agent 0, hovered (5,3), overlay still off --
    hud: tick 0   ...   selected=agent 0
    drawlist count: 108

    -- overlay ON, selected agent 0, hovered (5,3) --
    hud: tick 0   ...   selected=agent 0
    [dev] commitment=holding suppression=0 stress=0 reason=no-order exposed=0cells
    drawlist count: 205
    by kind: [(0, 96); (1, 12); (2, 1); (3, 96)]
    line (0,0)->(5,3) color=(0.2,1.0,0.4,0.85)      -- clear LOS, green

    -- overlay ON, hovered far cell (11,7) --
    line (0,0)->(2,2) color=(1.0,0.3,0.2,0.85)      -- blocked, red, ends at the traced Blocker cell (2,2), not (11,7)

    -- overlay OFF again --
    hud: tick 6   hash 0x5922...   draws 0   agents 3   running   selected=agent 0
    ```
    96 = the 12x8 grid's exact cell count, confirming every terrain cell got
    a coordinate label; the blocked-hover line terminating at `(2,2)` rather
    than the hovered `(11,7)` confirms the occluder (not just "some red
    line") is what gets drawn. `draws=0` throughout is expected, not a bug:
    this probe's short scripted route never reaches combat/suppression, the
    only current RNG consumer.
- Manual: `git status --porcelain`
  - Result: matches this task's allowed scope.

### Evidence

- `docs/evidence/task-043-developer-overlay.png` — captured via `"$GODOT"
  --path . scenes/CommandDemo.tscn -- --dev-overlay --screenshot <path>`,
  showing the full coordinate grid and the `[dev]` HUD line live over
  `CommandDemoScene`.
- The scratch probe's printed output (above).
- Both scenes' `--selfcheck` hash sequences, unchanged.

### Deviations and unresolved issues

- `Reserved`/`Obstructed`/`KnownContact`/`FireLine` markers were not
  independently exercised in the committed screenshot: at the scripted
  priming's paused, tick-0 capture point, this scenario has no cell
  contest, no occupancy conflict, no known hostile, and no shot fired yet.
  Each reads directly from the identical `Diagnostics.Overlay` cases every
  `DiagnosticRender` renderer already exercises and is tested against
  (TASK-011/026/027/031/034), so this is a rendering-arithmetic risk only,
  not a data-derivation one — flagged for Dave to eyeball live if wanted
  (`F1` in a windowed run once agents make contact, or step past the
  scripted priming point).
- Discipline and trust excluded from "suppression, stress, discipline, and
  trust values": trust has no backing state anywhere in `CommandoWar.Sim`
  (TASK-033's own note); discipline exists (`AgentState.Discipline`, static)
  but has no `Diagnostics.Overlay` case, and adding one would require a
  `CommandoWar.Sim`/`CommandoWar.Headless` change (a new `Overlay` case,
  every `DiagnosticRender` renderer updated, a `content/diagnostics/` golden
  regeneration) — out of proportion for one static integer and reaching
  outside the Godot-client-only scope this task otherwise held to. Left as
  a follow-up, not built here.
- "Last appraisal factors" realised as `OrderAppraisal.exposedCells` (the
  one concrete factor `Diagnostics` actually retains) plus the disposition/
  reason text, not raw per-candidate pressure numbers — `Appraisal.appraise`
  recomputes those on demand and never stores its intermediate values.
- Not built (Forbidden scope, as drafted): any `CommandoWar.Sim`/
  `CommandoWar.Headless` change; any `DemoRenderScene` behavioural change;
  changes to TASK-040/042's player-facing HUD/route-preview; B-030/B-031/
  B-051/B-052/B-053 work.

### Documents updated

- `tasks/TASK-043-DEVELOPER-OVERLAY-RENDERING.md` (`proposed -> review ->
  done`, Outcome section, Review round 2 section, acceptance criteria
  checked).
- `src/CommandoWar.Client.Godot/README.md` (new section, layout table).
- `docs/11_BACKLOG.md` B-029 row (`proposed -> review -> done`).
- `docs/12_PROGRESS_LEDGER.md` (this row, plus round 2 + accepted rows).
- `PROJECT_STATE.yaml` (`active_work.selected_task -> none`).

### Review round 1 (2026-09-17)

Dave tried `CommandDemo.tscn` windowed and reported two real problems: a red
line (the LOS ray) drawn "underneath the other layers" instead of on top,
and pointing toward the mouse cursor even off the map, appearing as a
disconnected line trailing into empty space below the grid.

Diagnosed and fixed, both rendering-only, no `Simulation.step`/hash change:

- **Z-order**: `devItems` were folded into the same `Array.sortBy
  RenderShared.depthKey` pass as terrain/agents. `depthKey` derives a depth
  from one origin cell, meaningless for a two-cell line — it could sort as
  "near" and paint before terrain it visually crossed. Fixed:
  `CommandDemoScene.DrawList()` now appends `devItems` *after* the depth
  sort (`Array.append sorted devItems`), so the developer overlay always
  draws on top.
- **Runaway ray off the map**: `OnHover` traced `Sight.trace` with no bounds
  check on the hovered cell. Off the grid, `Sight.trace` returns
  not-visible with no `Blocker`, and the existing
  fallback-to-`Blocker`-or-target logic then drew all the way to that
  far, meaningless cell. Fixed: `losRay` is now `None` whenever `not
  (GridBounds.contains cell state.Bounds)` (the `OnClick` precedent for the
  same check).

Verified with a new headless scratch probe (removed after use): an
in-bounds hover produces exactly one `Kind = 2` line, positioned last in
`DrawList()` (on top); hovering `(500,500)` and `(-3,-3)` produces zero
lines; returning in-bounds restores the ray immediately. Both scenes'
`--selfcheck` hashes reconfirmed unchanged (`0x11B06E6EDE0C52E3`,
`0x649FA4D08E2931CA`); `dotnet build` both `.slnx` (Debug/Release) 0/0;
`dotnet test` 297/297. `docs/evidence/task-043-developer-overlay.png`
re-captured.

Not investigated: Dave also described a "green flashing line from another
agent" toward a hostile, seen separately from the screenshot moment — most
likely the same hover-driven LOS ray behaving as designed (green =
visible) while hovering near the hostile with a different agent selected,
toggling as the mouse crossed a partial-occlusion cell boundary. Flagged
for Dave to re-check live now both concrete bugs are fixed, since it may
have simply been a symptom of the same issues.

### Review round 2 (2026-09-17)

Dave's live re-check after the round 1 fixes surfaced three more points, plus
confirmation the round 1 fixes hold:

- **The "flashing green line" is `FireLine`, not the LOS ray** — the round 1
  theory above was wrong. `Diagnostics.frameOf` emits one `FireLine` per shot
  fired *that tick only* (a this-tick event, not standing state), coloured
  green on hit, grey `(0.6,0.6,0.6)` on miss. `devFrame` is replaced every
  tick, so during active combat the line appears for roughly one tick's
  worth of render frames then disappears until the next shot — reading
  exactly as "flash grey at a timed interval" once agents exchange fire,
  independent of mouse position. Working as designed; no code change.
- **No legend for overlay colours**, and two hues are each reused for a
  different meaning depending on draw shape (LOS-visible green vs.
  `FireLine`-hit green; `Obstructed` red vs. LOS-blocked red). Fixed: a new
  `RenderShared.devLegendText`, grouping colours by draw shape, appended as a
  third `HudText()` line whenever the overlay is on.
- **Agent facing** (doesn't turn to face movement direction) and **sim speed
  feeling too fast** (`simHz = 20.0`) are both out of this task's scope:
  facing is the already-amended B-052 row; sim speed is a pre-existing
  TASK-040 tuning constant, untouched here. Neither is a TASK-043
  regression.

Verified: `dotnet build CommandoWar.slnx -c Release` 0/0; the Godot client
`.slnx` in both `-c Debug`/`-c Release` 0/0; `dotnet test CommandoWar.slnx -c
Release` 297/297 — all unaffected (the change is one new string-literal HUD
line, unreachable from `CommandDemoDrive.runScriptedSelfCheck`/
`DemoDrive.runFullSequence`, the only code paths `--selfcheck` exercises).

**Known gap, not blocking:** both scenes' `--selfcheck` hashes were not
independently re-run this round. Every headless Godot launch (`--headless
--path . <scene> -- --selfcheck`) hung indefinitely — no output past the
engine banner, near-zero CPU — for both scenes, repeatedly, in the
automation sandbox and separately in Dave's own terminal, while a normal
windowed launch of the same project worked correctly. Ruled out as causes:
a stray `VBCSCompiler`/`dotnet` build-server process (killed via `dotnet
build-server shutdown`, hang persisted) and a stale `.godot` cache (cleared
and let it reimport fresh, hang persisted). A windowed `--selfcheck` was
also tried as a workaround; the flag did not take effect there either (it
just ran the scene interactively) — an apparently separate quirk, not
pursued further. Risk from skipping the hash re-check is negligible: the
round 2 diff touches only `RenderShared.devLegendText` and one `sprintf` in
`HudText()`, string formatting the self-check driver never calls, and the
build/test run above already exercises the full compiled assembly this
change is part of.

Dave confirmed live in a normal windowed run: the `[legend]` HUD line
appears with F1 on; the LOS ray still draws on top of terrain and clears
off-grid (round 1 fix holds); the flashing green/grey line reads as combat
shots landing/missing, not the mouse. "I confirm the features work."

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-17)
