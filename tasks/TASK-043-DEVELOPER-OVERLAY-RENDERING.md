# TASK-043: Developer overlay rendering over live input (B-029 proper)

Status: done (accepted by Dave, 2026-09-17)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete)
Size: L (backlog lists M; the full-set scope confirmed with Dave below adds
two new interop primitives, which realistically makes this L)

## Outcome (2026-09-17)

Implemented as designed below: `Diagnostics.frame`/`.frameOf` (already part
of `CommandoWar.Sim`, no change needed) is now also consumed by
`CommandDemoScene`, which keeps the latest `DiagnosticFrame` after every step
and renders its `Overlay[]` as extra `DrawItem`s plus a `[dev]` HUD line when
`F1` toggles the overlay on. No `CommandoWar.Sim`/`CommandoWar.Headless`
change; `DrawItem` gained `Kind = 2` (line) / `Kind = 3` (label); `IClientScene`
gained `OnToggleDevOverlay` (`DemoRenderScene`: no-op).

Verified: `dotnet build CommandoWar.slnx -c Release` 0/0 (unaffected); the
Godot client `.slnx` in both `-c Debug`/`-c Release` 0/0; `dotnet test
CommandoWar.slnx -c Release` 297/297 (unaffected). Both scenes'
`--selfcheck` hashes confirmed unchanged: SnapshotDemo
`0x11B06E6EDE0C52E3`, CommandDemo `0x649FA4D08E2931CA`. A headless `dotnet
fsi` scratch probe (removed after use) drove the real `CommandDemoScene`
directly: with the overlay off, `DrawList()`/`HudText()` match TASK-042's
existing shape plus the new always-on `draws=` counter; toggling the overlay
on adds 96 coordinate labels (`Kind = 3`) for the 12x8 grid and a `Kind = 2`
line-of-sight ray that correctly terminates at the traced `Blocker` cell
(not the hovered cell) when occluded, confirmed against two different hover
targets (one clear, one blocked); the `[dev]` HUD line appears only when the
overlay is on and an agent is selected, and both a new selection and
toggling the overlay off immediately clear/restore the expected text.
Evidence screenshot `docs/evidence/task-043-developer-overlay.png` captured
via a new opt-in `--dev-overlay` CLI flag (kept separate from the existing
`--screenshot` priming so a plain capture still reproduces TASK-042's
existing evidence unchanged) shows the coordinate grid and `[dev]` HUD line
live over `CommandDemoScene`.

Reserved/Obstructed/KnownContact/FireLine markers were not independently
exercised in the committed screenshot (this scenario reaches no contest,
occupancy conflict, known hostile, or shot at the scripted priming's paused
capture point, tick 0) -- each reads directly from the identical
`Diagnostics.Overlay` cases `DiagnosticRender`'s existing renderers already
exercise and pass tests against, so this is a rendering-only risk, not a
data-derivation one; flagged for Dave to eyeball live if wanted (`F1` in a
windowed run once agents make contact).

Discipline and trust deliberately excluded from the "suppression, stress,
discipline, and trust values" bullet -- see Inputs and assumptions above.
"Last appraisal factors" realised as `exposedCells` + reason text, not raw
pressure numbers (not retained state anywhere).

`docs/11_BACKLOG.md` B-029 row updated to `review`. Full detail:
`docs/ledger/2026-09-17-TASK-043-developer-overlay-rendering.md`.

## Review round 1 (2026-09-17)

Dave tried `CommandDemo.tscn` windowed and reported two real problems: a red
line (the LOS ray) was drawn "underneath the other layers" instead of on
top, and it pointed toward wherever the mouse cursor was even when that was
off the map entirely, appearing as a disconnected line trailing into empty
space below the grid.

Both diagnosed and fixed, rendering-only:

- **Z-order**: `devItems` (labels, cell markers, lines) were folded into the
  same `Array.sortBy RenderShared.depthKey` pass as terrain/agents.
  `depthKey` derives a depth from a single origin cell, which is meaningless
  for a line spanning two cells — a LOS ray from a near cell to a far one
  sorted as if it were "near" and could paint before terrain it visually
  crossed. Fixed: `devItems` is now appended *after* the depth sort, so the
  developer overlay always draws on top of everything else — appropriate
  for an overlay whose purpose is to reveal information that might
  otherwise be hidden.
- **Runaway ray off the map**: `OnHover` traced `Sight.trace` against
  whatever cell the cursor mapped to with no bounds check. Off the grid,
  `Sight.trace` returns not-visible with no `Blocker`, and the ray's
  fallback-to-`Blocker`-or-target logic then drew all the way out to that
  far, meaningless cell. Fixed: `losRay` is now `None` whenever the hovered
  cell fails `GridBounds.contains` (the existing `OnClick` precedent for the
  same check), so leaving the grid simply hides the ray instead of chasing
  the cursor into the void.

Verified with a new headless scratch probe (removed after use): hovering an
in-bounds cell still produces exactly one `Kind = 2` line item, positioned
last in the sorted `DrawList()` (confirmed on top); hovering `(500,500)` and
`(-3,-3)` (both out of bounds) produces zero line items; returning to an
in-bounds cell restores the ray immediately. Both scenes' `--selfcheck`
hashes reconfirmed unchanged; `dotnet build` both `.slnx` (Debug and
Release) 0/0; `dotnet test` 297/297. Evidence screenshot re-captured
(same command, same file).

Not investigated further: Dave also described a "green flashing line from
another agent" toward a hostile, seen at a different moment than the
screenshot. This is very likely the same hover-driven LOS ray working as
designed (green = visible) while hovering near the hostile with a different
friendly selected, toggling rapidly ("flashing") as the mouse crossed a
partial-occlusion boundary between adjacent cells — an accurate reflection
of `Sight.trace`'s per-cell terrain result, not obviously a defect. Flagged
for Dave to confirm live now that the two concrete bugs above are fixed,
since it may simply have been a symptom of the same off-grid/z-order issues.

## Review round 2 (2026-09-17) — accepted

Dave's live re-check after the round 1 fixes surfaced three more points:

- **The "flashing" line is `FireLine`, not the LOS ray.** The round 1 theory
  above was wrong. `Diagnostics.frameOf` emits one `FireLine` per shot fired
  *that tick only* (`Diagnostics.fs`: "a shot is a this-tick event, not
  standing state"), coloured green on hit, grey `(0.6,0.6,0.6)` on miss
  (`CommandDemoScene.fs`'s `fireLines` binding). `devFrame` is replaced every
  tick, so during active combat the line appears for roughly one tick's
  worth of render frames, then disappears until the next shot — reading
  exactly as "flash grey at a timed interval" once agents are actually
  exchanging fire, independent of mouse position. Working as designed; no
  code change.
- **No legend for overlay colours**, and two hues are each reused for a
  different meaning depending on draw shape (LOS-visible green vs.
  `FireLine`-hit green; `Obstructed` red vs. LOS-blocked red). Fixed:
  `RenderShared.devLegendText`, a static line grouping colours by draw shape
  (cell marker vs. line), appended as a third `HudText()` line whenever the
  overlay is on (`CommandDemoScene.fs`).
- **Agent facing** (doesn't turn to face movement) and **sim speed feeling
  too fast** (`simHz = 20.0`) are both out of this task's scope: facing is
  already the amended B-052 row; sim speed is a pre-existing TASK-040 tuning
  constant, untouched here. Neither is a TASK-043 regression.

Verified: `dotnet build CommandoWar.slnx -c Release` 0/0; the Godot client
`.slnx` in both `-c Debug`/`-c Release` 0/0; `dotnet test CommandoWar.slnx -c
Release` 297/297 — all unaffected (the change is one new string-literal HUD
line, unreachable from `CommandDemoDrive.runScriptedSelfCheck`/
`DemoDrive.runFullSequence`, the only code paths `--selfcheck` exercises).

**Known gap, not blocking:** both scenes' `--selfcheck` hashes were not
independently re-run this round. Headless Godot launches (`--headless
--path . <scene> -- --selfcheck`) hung indefinitely on this machine for
every scene tried, both in the automation sandbox and in Dave's own
terminal, while a normal windowed launch of the same project worked
correctly — an environment issue unrelated to this change (ruled out: a
stray build-server process, and a stale `.godot` cache; neither fix
resolved the headless hang). Windowed `--selfcheck` also did not trigger
(the flag is read the same way regardless of `--headless`, but the windowed
console.exe launch did not pick it up either — a second, apparently
separate quirk). Risk from skipping this is negligible: the round 2 change
touches only `RenderShared.devLegendText`/`HudText()`'s string formatting,
which the self-check driver never calls, and the build/test run above
already exercises the full compiled assembly.

Dave confirmed live in a normal windowed run: the `[legend]` HUD line
appears with F1 on; the LOS ray draws on top of terrain and clears off-grid
(round 1 fix holds); the flashing green/grey line reads as combat shots, not
the mouse. Accepted.

## Objective

Render `CommandoWar.Sim`'s existing `Diagnostics.DiagnosticFrame` as a
toggleable developer overlay inside `CommandDemoScene` -- the first Godot
scene where the frame is built from **live player input**, not a replayed
corpus entry (TASK-029's `AppraisalDemoScene`, which is read-only and
corpus-scoped). Realises docs/06 section 11's developer-facing overlay list
against a moving, player-driven simulation for the first time.

## Why this task exists

B-029's dependencies (B-012a/TASK-011, B-012/TASK-016, B-017/TASK-028,
B-027/TASK-039) are all `done`. Selected as the next P4 task via
`AskUserQuestion` this session, over B-030 proper, B-031, and B-051.
docs/06 section 11 itself says: "The Godot developer overlay (backlog
B-029) becomes a third renderer of the same frame" -- i.e. this task's job
is to reuse `Diagnostics.frame`/`.frameOf`, not invent a parallel data path.

## Central decision (confirmed with Dave via `AskUserQuestion` before drafting)

docs/06 section 11 lists eight developer-facing overlay items. Two of them
need interop primitives that do not exist today: `DrawItem` only supports a
textured terrain cell or an agent/marker circle, with no line-segment or
per-cell text primitive (needed for line-of-sight rays/occluders and for
logical-grid coordinate labels respectively). Presented as a fork: a
narrower first cut reusing only existing primitives, or the full set adding
both new primitives now. **Dave chose the full set now**, accepting the L
sizing this implies.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `docs/06_CONTENT_AND_PRESENTATION.md` section 11 (the exact developer-facing
  list this task realises) and its "Realised by TASK-011" / "Realised by
  TASK-029" notes (this task is explicitly named as the next realisation)
- `docs/03_ARCHITECTURE.md` section 12 (`RenderSnapshot` values-only
  contract) -- **does not apply here**: `Diagnostics.DiagnosticFrame` is the
  established developer-facing read path, distinct from and already
  precedented for exactly this purpose (ADR-0002; TASK-011/029)
- `src/CommandoWar.Sim/Diagnostics.fs` (`Overlay`, `DiagnosticFrame`,
  `frame`, `frameOf`) -- read fully; no changes are planned here
- `src/CommandoWar.Sim/Sight.fs` (`Sight.trace`, `LineOfSight`)
- `src/CommandoWar.Headless/DiagnosticRender.fs` (`reasonText`/
  `dispositionText`/`commitmentText` -- the existing private developer-facing
  vocabulary this task's Godot-side mapping mirrors, per docs/06's
  player/developer text-boundary precedent already established in TASK-042)
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`, `RenderShared.fs`,
  `CommandDemoScene.fs`, `DemoRenderScene.fs`
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs` (`_Draw`,
  `_UnhandledInput`, the isometric cell<->screen projection)

## Dependencies

- B-012a (TASK-011), B-012 (TASK-016), B-017 (TASK-028), B-027 (TASK-039),
  all `done`.

## Inputs and assumptions

- `CommandDemoScene` is the only scene this task changes behaviourally: it
  is the only scene with live input/selection (the "live input" half of
  B-029's own description). `DemoRenderScene` gains only a mechanical no-op
  interface-conformance method (the `OnTogglePause` precedent: "a scene that
  has nothing to pause may no-op").
- Toggle key: `F1`, independent of the existing pause key (`Space`) --
  an implementation detail, not a product-direction fork.
- Line-of-sight ray/occluder presentation: traced from the selected friendly
  agent to the currently **hovered** cell (reusing the existing `OnHover`
  mechanic already wired for route preview), coloured green when
  `Sight.trace` reports visible, red up to the blocking cell when not --
  this is the concrete way "rays and occluders" becomes observable without
  adding new input.
- Discipline and trust are **excluded** from the "suppression, stress,
  discipline, and trust values" bullet:
  - Trust has no backing state anywhere in `CommandoWar.Sim` (TASK-033's own
    note: "dynamic trust ... stay out"). Nothing exists to render.
  - Discipline exists (`AgentState.Discipline`, static) but has no
    `Diagnostics.Overlay` case today. Adding one would require a
    `CommandoWar.Sim`/`CommandoWar.Headless` change (a new `Overlay` case,
    every `DiagnosticRender` renderer updated to handle it exhaustively, and
    a `content/diagnostics/` golden regeneration) -- out of proportion for
    one static integer, and it reaches outside the Godot client project this
    task is otherwise confined to. Left as a follow-up, not built here.
- "Last appraisal factors" is realised as `OrderAppraisal`'s `exposedCells`
  (the one concrete factor `Diagnostics` actually retains) plus the
  `DecisionReason`/`OrderDisposition` text -- the underlying per-candidate
  pressure numbers are not retained state anywhere (`Appraisal.appraise` is
  recomputed on demand, its intermediate values are not stored), so they
  cannot be surfaced as-is.
- No `CommandoWar.Sim` or `CommandoWar.Headless` change of any kind. No
  `Canonical.FormatVersion` bump, no golden regeneration, no `--selfcheck`
  hash change -- this is a Godot-client-only, `DiagnosticFrame`-consuming,
  purely observational addition (ADR-0002: frames never feed back into the
  step).

## Allowed scope

- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`:
  - `DrawItem` gains `Cx2: float32`, `Cy2: float32` (meaningful only for a
    new `Kind = 2`, a line segment from `(Cx,Cy)` to `(Cx2,Cy2)`; `Radius`
    doubles as line width) and `Text: string` (meaningful only for a new
    `Kind = 3`, a text label drawn at `(Cx,Cy)`; `Radius` doubles as font
    size; empty string for every other `Kind`).
  - `IClientScene` gains `abstract OnToggleDevOverlay: unit -> unit`.
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`: new developer-facing
  text helpers (commitment/suppression/stress/reason), mirroring
  `DiagnosticRender`'s existing private vocabulary and wording, kept
  distinct from the player-facing `dispositionText` added by TASK-042 (the
  same docs/06 player/developer split).
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: keep the latest
  `Diagnostics.DiagnosticFrame` (via `Diagnostics.frame`/`.frameOf`) each
  tick; `OnToggleDevOverlay`; extra `DrawItem`s and `HudText()` content when
  the overlay is on.
- `src/CommandoWar.Client.Godot/Core/DemoRenderScene.fs`: mechanical
  `OnToggleDevOverlay` no-op only.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: `_Draw` dispatch for
  `Kind = 2`/`Kind = 3`; `_UnhandledInput` gains the `F1` key.
- `docs/evidence/task-043-*.png`, `README.md`, `docs/11_BACKLOG.md`,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Forbidden scope

- No `CommandoWar.Sim`/`CommandoWar.Headless` change (see Inputs and
  assumptions) -- in particular, no new `Overlay` case for discipline, no
  golden regeneration.
- No trust value (does not exist).
- No `DemoRenderScene` behavioural change (no selection, no input; a no-op
  method only).
- No changes to the player-facing HUD text or route-preview colours added by
  TASK-040/042.
- No work on B-030/B-031/B-051/B-052/B-053 -- separate backlog rows.

## Required work

1. Extend `IClientScene.fs`'s `DrawItem` (`Kind = 2` line, `Kind = 3` label)
   and `IClientScene` (`OnToggleDevOverlay`).
2. Extend `FSharpSceneHost.cs`'s `_Draw` to render the two new kinds, and
   `_UnhandledInput` to wire `F1`.
3. Add developer-facing text helpers to `RenderShared.fs`.
4. In `CommandDemoScene.fs`: retain the latest `DiagnosticFrame`
   (`Diagnostics.frame` at `Ready()`, `Diagnostics.frameOf` after each step);
   add the overlay toggle and its draw items (grid coordinate labels;
   `Reserved`/`Obstructed` cell markers; `KnownContact` ghost markers;
   `OrderAppraisal.exposedCells` markers for the selected agent; a
   hover-driven line-of-sight ray/occluder line; `FireLine` markers when
   present); extend `HudText()` with the random-draw counter (always) and a
   developer line (commitment/suppression/stress/reason for the selected
   agent, only when the overlay is on).
5. Verify both scenes' `--selfcheck` hashes are unchanged.
6. Verify the new overlay actually renders (screenshot with the overlay
   toggled on).
7. Update documentation.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] `F1` toggles a developer overlay in `CommandDemoScene`, independent of
      pause.
- [x] With the overlay on: grid coordinates are visible; any this-tick
      `Reserved`/`Obstructed` cell is visibly marked; any `TacticalKnowledge`
      contact's last-known cell is visibly marked, distinct from the real
      agent marker; the selected agent's current commitment, suppression,
      stress, and appraisal reason/exposed-cells are visible; a
      line-of-sight ray from the selected agent to the hovered cell is drawn,
      distinguishing visible from occluded. (Reserved/Obstructed/KnownContact/
      FireLine confirmed by shared derivation + code inspection, not
      independently screenshotted -- see Outcome.)
- [x] Tick, state hash, and random-draw counter are all visible in the HUD.
- [x] With the overlay off, rendering and HUD text are unchanged from
      TASK-042's committed behaviour.
- [x] `--selfcheck` hashes for both scenes are unchanged.
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless` change; no forbidden
      dependency.
- [x] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: expect `0/0`, unaffected.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx`
  in both `-c Debug` and `-c Release`: expect `0/0`.
- `dotnet test CommandoWar.slnx -c Release`: expect `297/297`, unaffected.
- `"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck`:
  expect unchanged hash.
- `"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck`:
  expect unchanged hash.
- A headless scratch probe (removed after use, the TASK-038/042 precedent)
  driving `CommandDemoScene` directly: toggle the overlay, select an agent,
  hover a cell, print the extra `DrawList()`/`HudText()` content to confirm
  it is real and non-empty.
- A windowed `--screenshot` of `CommandDemoScene` with the overlay toggled on
  (needs a scripted `OnToggleDevOverlay()` call in `FSharpSceneHost.cs`'s
  existing screenshot-priming block, or a manual windowed capture -- decide
  during implementation).
- `git status --porcelain`: matches this task's allowed scope.

## Evidence to capture

- `docs/evidence/task-043-developer-overlay.png`.
- The scratch probe's printed output (recorded in the ledger detail file).
- Both scenes' `--selfcheck` hash sequences, confirmed unchanged.

## Expected files

- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`, `RenderShared.fs`,
  `CommandDemoScene.fs`, `DemoRenderScene.fs`.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`.
- `docs/evidence/`, `README.md`, `docs/11_BACKLOG.md`,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Documentation updates

- This task file's Outcome section.
- `src/CommandoWar.Client.Godot/README.md`.
- `docs/11_BACKLOG.md` B-029 row.
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Purely additive and observational: two new `DrawItem` fields/kinds meaningful
only when used, one new interface method with a trivial no-op default
elsewhere, and new draw/HUD logic gated entirely behind a toggle that
defaults off. No `CommandoWar.Sim` change, no hash change. Revertible with
`git revert` in one step.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
