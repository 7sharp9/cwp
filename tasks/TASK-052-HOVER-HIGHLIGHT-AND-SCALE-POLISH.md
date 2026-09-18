# TASK-052: Hover highlight and a larger fixed scale

Status: done (implemented and self-verified 2026-09-18; Godot `--selfcheck`
independently confirmed `MATCH` through the real editor 2026-09-18; accepted
by Dave 2026-09-18)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises backlog B-053 and B-054

## Outcome (2026-09-18)

Both bundled, per Dave's own instruction ("pick B-053/B-054"), the same
kind of merge TASK-037 used when two small rows unblock more together than
either alone. Both are presentation-only client polish; neither touches
`CommandoWar.Sim`/`CommandoWar.Headless`.

**B-053, hover highlight**: hovering a friendly agent's own rendered circle
(without clicking) now draws a thin hollow ring around it, reusing
TASK-051's `Kind = 5` primitive with a light neutral tint instead of the
fog-of-war ghost's hostile-coloured one -- the same shape family, a
different colour role. Added `CommandDemoScene.fs`'s `hoverHighlightItems`
(`hoveredCell |> Option.bind friendlyAt`, drawn only for a friendly, never
a hostile, fogged or not). `FSharpSceneHost.cs`'s mouse-motion handler
previously called `ScreenToCell` directly, unlike the click handler (which
already tries `TryHitAgentCircle` first, the TASK-040 fix for the same
projection mismatch) -- a real correctness gap found while implementing:
hovering near the top of a visible agent's circle would have missed it and
resolved to the wrong cell, exactly the bug TASK-040 fixed for clicks two
tasks after this scene was introduced, just never carried over to hover.
Fixed by giving `OnHover`'s dispatch the identical `TryHitAgentCircle`-first
fallback.

**B-054, larger fixed scale**: `FSharpSceneHost.cs`'s isometric tile pitch
doubled (`TileW`/`TileH`: `44x22 -> 88x44`); `Origin` moved `(450,60) ->
(552,110)`, recentred for the new scale and raised on `Y` with real margin
past the HUD label's worst-case three-line height (`F1` on, an agent
selected) -- the two halves of Dave's TASK-043 review complaint ("the
isometric play area occupies a small fraction of the window, and the
corner HUD Label overlaps the tick counter and agent sprites"), both fixed
by the one change (a bigger, better-placed play area no longer needs the
HUD to move out of its way). `DrawTerrainTile` already scales terrain tiles
directly from `TileW`/`TileH`; only the figure/marker pixel radii needed a
matching bump, which they had been carrying as independent literals.
Introduced `CommandDemoScene.fs`'s `agentRadius` (`10.0f -> 20.0f`) and
`haloRadius` (`17.0f -> 34.0f`, keeping the halo's original 1.7x ratio) as
named, shared values -- not just because two more literals needed the same
number, but because the fog-of-war ghost ring (TASK-051) and B-053's new
hover ring both have to line up with exactly the same footprint a real
agent figure draws at; letting three call sites carry that number
independently is exactly the kind of drift a shared constant exists to
prevent. Confirmed via `AskUserQuestion`: a larger fixed scale, not
interactive zoom/pan (a materially bigger feature, explicitly not this
task's scope).

Verified with a temporary `dotnet fsi` scratch probe (the TASK-042/051
precedent, removed after use) driving `CommandDemoScene` directly:
confirmed a `Kind = 5` ring (radius `23.0` = `agentRadius + 3`, alpha
`0.80`, light-neutral colour) appears only while hovering a friendly
agent's own cell, not empty terrain and not a hostile's cell (contacted or
not -- fog of war is unaffected), and coexists correctly with the selection
halo when the same agent is both selected and hovered. Re-ran the TASK-051
fog-of-war probe unchanged and confirmed the identical tick-by-tick
sequence, proving the shared `agentRadius` refactor changed no behaviour.

`dotnet build CommandoWar.slnx -c Release` and `CommandoWar.Client.Godot.slnx
-c Debug`: `0/0`. `dotnet test`: `342/342` (unaffected). Both scenes'
`--selfcheck` hashes independently re-run through the real Godot 4.7.2
editor and confirmed `MATCH` (render-only, as expected): `SnapshotDemo.tscn`
`0xF422ACB8D5A86FF0`, `CommandDemo.tscn` `0x00D3D471EF7354BC`,
`AppraisalDemo.tscn` `0x194805888CBE240D`. Committed screenshot
`docs/evidence/task-052-scale-and-hover.png`, captured with `--dev-overlay`
on to show the HUD's worst-case three-line height alongside the new scale
-- visibly no overlap, and the play area now fills most of the window.

Full detail: `docs/ledger/2026-09-18-TASK-052-hover-highlight-and-scale-polish.md`.

## Objective

Two small, previously-flagged client-polish items: highlight a friendly
agent on hover so the player knows a click will select it (B-053); enlarge
the fixed isometric scale and clear the HUD-overlap so the play area
actually uses the window (B-054).

## Why this task exists

- B-053: raised by Dave on accepting TASK-041 (2026-09-17), hedged
  ("possibly") -- selection still felt fiddly even after TASK-040's own
  click-projection fix.
- B-054: raised by Dave during TASK-043 review (2026-09-17) -- "general dev
  experience needs a bit of love," not a single specific fix.

Selected by Dave directly ("pick B-053/B-054"), bundled into one task since
both are small, presentation-only, and touch the same file.

## Central decisions (confirmed with Dave 2026-09-18 before drafting)

One round, put via `AskUserQuestion`: B-054's own backlog text named an
undecided fork ("likely a camera zoom/pan or a larger fixed scale") --
**a larger fixed scale** (Dave's choice, the recommended default): no new
input handling or camera-state concept, matching the row's own S-M size
estimate.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` (`agentItems`,
  `haloItems`, `OnHover`, `friendlyAt`, the TASK-051 fog-of-war `Kind = 5`
  precedent)
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs` (`cellRing`)
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs` (`TileW`/`TileH`/
  `Origin`, `CellToScreen`/`ScreenToCell`, `TryHitAgentCircle`,
  `_UnhandledInput`'s mouse-motion branch, `DrawTerrainTile`,
  `DrawAgentFigure`)
- `src/CommandoWar.Client.Godot/project.godot` (`window/size/viewport_width`/
  `viewport_height`)

## Dependencies

- B-026 (TASK-040, selection/hover input) -- done.
- B-027 (TASK-039, the Godot client-core scaffold) -- done.

## Allowed scope

- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: `agentRadius`/
  `haloRadius` constants; `hoverHighlightItems`; wiring both into the
  existing draw-item construction.
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`: no change expected
  (`Kind = 5` already documented by TASK-051); confirm during
  implementation.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: `TileW`/`TileH`/
  `Origin`; the mouse-motion `TryHitAgentCircle` fallback; the `case 5`
  stroke-width comment/tweak.
- `src/CommandoWar.Client.Godot/README.md` (new section).
- `docs/evidence/task-052-scale-and-hover.png` (new screenshot).
- `docs/11_BACKLOG.md` (B-053/B-054 rows), `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- Any change to `src/CommandoWar.Sim`/`src/CommandoWar.Headless`.
- Interactive camera zoom or pan, or any new camera-state concept (Central
  decision 1).
- Any change to `SnapshotDemo.tscn`/`DemoRenderScene.fs` (neither backlog
  row names it, and it has no hover/selection concept to highlight).
- Action-point budgeting, an Overwatch commitment state, or any other
  unrelated follow-on work.

## Required work

1. `agentRadius`/`haloRadius` named constants; replace the three literals
   they consolidate (agent figure x2, halo x1) plus the two fog-of-war
   ghost-marker literals from TASK-051 that must track the same footprint.
2. `hoverHighlightItems`, gated to a friendly agent only.
3. Fix the mouse-motion handler's cell resolution to match the click
   handler's `TryHitAgentCircle`-first fallback.
4. Double `TileW`/`TileH`; recompute `Origin` for the new scale, clearing
   the HUD label's worst-case height.
5. Verify via a temporary `dotnet fsi` scratch probe (hover ring
   appears/disappears correctly; the TASK-051 fog-of-war sequence is
   unaffected by the shared-constant refactor).
6. Confirm both scenes' `--selfcheck` hashes unaffected through the real
   Godot editor.
7. Capture a screenshot; update `README.md`, backlog/ledger/state.

## Acceptance criteria

- [x] Hovering a friendly agent's own circle highlights it distinctly from
      the selection halo; hovering empty terrain or a hostile does not.
- [x] The hover highlight arms correctly near the top of a visible agent's
      circle (the same projection point clicks already resolve correctly).
- [x] The play area occupies materially more of the 1280x800 window than
      before (~34%/~28% width/height -> ~62%/~50%).
- [x] The HUD label's worst case (three lines, `F1` on, an agent selected)
      no longer overlaps the play area.
- [x] Fog of war (TASK-051) is unaffected by the shared `agentRadius`
      refactor.
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless` change; both scenes'
      `--selfcheck` hashes unaffected.
- [x] `dotnet build`/`dotnet test` unaffected; Godot client builds.
- [x] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet test`: `342/342` (unaffected).
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: `0/0`.
- A `dotnet fsi` scratch probe confirming the hover-ring gating and the
  unaffected fog-of-war sequence (see Outcome for the exact results).
- Godot editor `--selfcheck` for all three scenes through the real Godot
  4.7.2 editor: confirmed `MATCH` -- `SnapshotDemo.tscn`
  `0xF422ACB8D5A86FF0`, `CommandDemo.tscn` `0x00D3D471EF7354BC`,
  `AppraisalDemo.tscn` `0x194805888CBE240D` (all unchanged), all exit 0.
- Windowed screenshot `docs/evidence/task-052-scale-and-hover.png`.
- `git status --porcelain`: matches this task's allowed scope.

## Documentation updates

- This task file's Outcome section.
- `src/CommandoWar.Client.Godot/README.md`.
- `docs/11_BACKLOG.md` (B-053 and B-054 rows: proposed -> done).
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Fully additive and render-only for B-053 (one new `agentItems`-sibling
computation, one C# fallback added to an existing handler); a constant
change plus a reused shape for B-054 (`TileW`/`TileH`/`Origin`, `agentRadius`/
`haloRadius`). No `CommandoWar.Sim`/`CommandoWar.Headless` change, no hash
format change. Revertible with `git revert` in one step; both scenes'
`--selfcheck` hashes are unaffected either way, so nothing needs re-pinning
on rollback.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-18). No changes requested. A separate nit raised on
  acceptance ("when an agent is removed due to death, the selection/order
  UI should deactivate rather than stay pointing at it") recorded as a new
  backlog row, B-061, not fixed by this task.
