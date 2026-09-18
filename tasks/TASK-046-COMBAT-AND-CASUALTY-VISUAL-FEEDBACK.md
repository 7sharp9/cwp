# TASK-046: Player-facing combat and casualty visual feedback

Status: done
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete)
Size: S-M

## Outcome (2026-09-17/18)

Selected via `AskUserQuestion` over B-030 proper and the smaller client-polish
rows (B-052/053/054/055/056): B-057 was a direct, freshly-raised follow-up
naming a real gap in TASK-045's own acceptance ("seems to work but there is
no visual indicator of fire without the overlay").

**Central decision (art vs. primitives), confirmed with Dave via
`AskUserQuestion` before implementation**: reuse existing draw primitives, or
source new sprite art. Dave chose new art. Live-checked kenney.nl (downloaded
and unzipped both candidate packs, not assumed from memory, the TASK-041
precedent): the existing "Isometric Miniature Prototype" pack (`art/`) has
**no** combat-effect sprites and **no** wound/death character pose (only
`Idle`/`Run`/`Pickup`). Kenney's CC0 "Particle Pack" has real `muzzle_*`/
`spark_*`/`smoke_*` sprites; Kenney's CC0 "Game Icons" (base + expansion) has
only a generic medical cross, no skull or distinct incapacitated icon. Put
this finding back to Dave via a second `AskUserQuestion`: confirmed
**Particle Pack for fire feedback, primitives for vitals** (a mismatched
cross icon on a casualty state it wasn't designed for was rejected).

### Fire feedback (new art)

`FireLine` (TASK-031/043's existing per-shot overlay, already computed every
tick regardless of the F1 dev-overlay toggle) is promoted from
developer-only to always-on. New `DrawItem.Kind = 4`: a one-shot effect
sprite centred at a cell, `TextureId` selecting `effect_muzzle_flash.png` (at
the shooter), `effect_impact_hit.png` (at the target, on a hit), or
`effect_impact_miss.png` (at the target, on a miss) -- a distinct **shape**
per outcome, not a colour-only hit/miss tint (docs/06 "status indicators that
do not rely on colour alone"). A thin connecting tracer still uses the
existing `Kind = 2` line primitive. `CommandDemoScene.fs`'s new `fireEffects`
list is unsorted and appended after the depth sort, the existing `devItems`
precedent (a two-cell-spanning item has no single meaningful depth).

### Casualty feedback (primitives only)

`AgentVitals` (TASK-045's existing per-agent overlay, likewise always
computed) is promoted the same way, mirroring
`DiagnosticRender.Svg`'s own established vitals vocabulary rather than a
colour-only recolour of the agent figure:

- `Alive` at full health: unchanged.
- `Alive` wounded: the figure unchanged plus a small red dot (`Kind = 1`,
  opacity scaled by wound severity) -- the dot's *presence*, not a tint on
  the agent itself, is the signal.
- `Incapacitated`: the figure darkened (50% tint) plus a plain-language
  `"down"` text badge (`Kind = 3`, the `RenderShared.reasonText`
  player-vocabulary precedent -- no bleed-out tick count, that stays
  developer detail per `devReasonText`'s own distinction).
- `Dead`: the figure replaced entirely by a small black cross (`Kind = 2`,
  the `DiagnosticRender.Svg` dead-cross precedent) -- a corpse is not a
  coloured variant of a living agent.

### Files

New `RenderShared.effectSprite` (the `cellMarker`/`lineMarker` precedent).
`IClientScene.fs`'s `DrawItem.Kind` doc comment extended for `4`.
`FSharpSceneHost.cs`: a third static `EffectTextures` array (loaded once,
the `TerrainTextures`/`AgentTexture` precedent), a `case 4` in `_Draw`'s
switch, and `DrawEffectSprite` (centred, not foot-anchored, at the same
`pos - TileH/2` anchor every other point marker uses). New
`src/CommandoWar.Client.Godot/art/effect_muzzle_flash.png`,
`effect_impact_hit.png`, `effect_impact_miss.png` (Kenney "Particle Pack",
CC0, resized from the pack's native 512x512 to 96x96 and alpha-boosted for
legibility at on-screen sprite scale; `art/LICENSE-THIRD-PARTY.md` gains a
second pack section naming the exact source files). No `CommandoWar.Sim`
engine change and no new canonical state or `Canonical.FormatVersion` bump
as originally drafted; Review round 1 below added one authorised
`CommandoWar.Headless` **content** change (`DemoScenario.fs`'s terrain
authoring, not engine logic) to fix a real LOS bug Dave found live.

### Verification

`dotnet build` both `.slnx` (main; Godot client Debug and Release): `0/0`.
`dotnet test CommandoWar.slnx`: unaffected, `319/319`. Both Godot scenes'
`--selfcheck` hashes confirmed **unchanged**
(`SnapshotDemo` `0x44B29B73E8F107EF`, `CommandDemo` `0xF1027A36B36BC3DF`) --
this is genuinely render-only, no state/hash change, re-confirmed a second
time after the evidence-gathering pass below was fully reverted.

The standard `--screenshot` evidence path (paused at tick 0, the TASK-041/042/
043 precedent) cannot itself show combat: `CommandDemoScene`'s screenshot
priming pauses immediately after issuing an order, so no tick ever advances.
Committed as `docs/evidence/task-046-combat-visual-feedback-regression.png`
(confirms the existing halo/route-preview/pending-order visuals are
byte-for-byte unaffected).

To verify the new fire/wound rendering for real, a **temporary, local-only**
modification to `FSharpSceneHost.cs`'s screenshot priming was used (removed
the pause, retargeted the scripted click toward the hostile at (11,7), and
captured one frame after `DrawList()` first reported a `Kind = 4` item --
`GetViewport().GetTexture().GetImage()` reads the last *completed* `_Draw`,
not the one `QueueRedraw()` just scheduled this frame, an existing quirk of
the capture helper unrelated to this task). This is **not** part of the
shipped diff (`git diff` after reverting is empty for this file's temporary
lines; confirmed via `git status --porcelain` and a second `--selfcheck` run
above). Direct instrumentation during that pass additionally confirmed, at
the Godot API level, that `DrawEffectSprite` receives a valid non-null 96x96
`Texture2D`, the correct screen rect for each effect's cell, and the correct
tint for each of the three `TextureId`s. Captured evidence committed as
`docs/evidence/task-046-fire-and-wound-feedback.png`: shows a muzzle flash at
the shooter's cell, a warm impact-hit splash at the target's cell, and (a
second capture, same session) the wound dot's `Radius = 4` marker coincident
with that same impact.

Known limitation, not blocking: the Particle Pack's soft alpha-gradient
sprites read as a fairly subtle blob against this terrain palette under
plain alpha blending (no additive/glow blend mode exists in this renderer).
Boosted the alpha curve and enlarged the sprites once already; a further
pass (an additive `CanvasItemMaterial`, or brighter/harder-edged replacement
art) would sharpen this further but is presentation polish, not a functional
gap -- flagged for a possible fast-follow rather than blocking this task.

Full detail: `docs/ledger/2026-09-18-TASK-046-combat-and-casualty-visual-feedback.md`.

## Review round 1 (2026-09-18)

Dave tried it live and raised three points.

**"Are the agents firing through the scenery?"** Yes — a real, confirmed bug,
not a rendering artifact. Traced `Sight.trace` directly against
`DemoScenario`'s terrain for the shooter/target cells from the review
session's own engagement: the line of fire passes through `(8,2)`, `(8,3)`,
`(9,3)` — the impassable 2x2 block — which were authored `Opaque = false`.
`Sight.trace`/`Combat.chooseTarget` correctly gate only on `opaque` (a solid
block that stops movement was simply never told to stop sight, unlike the
separate opaque-wall feature at `x = 6`, which was authored correctly). Fixed
by marking the block's four cells `Opaque = true` in
`src/CommandoWar.Headless/DemoScenario.fs`. This is `CommandoWar.Headless`
demo *content*, not `CommandoWar.Sim` engine logic, but Dave confirmed via
`AskUserQuestion` he wanted it fixed in this task rather than filed
separately, accepting the wider re-verification: `SnapshotDemo`'s
`--selfcheck` hash changed (its scripted route passes near the block) and was
re-pinned (`0x44B29B73E8F107EF -> 0x0DDADF2AD356E674`); `CommandDemo`'s did
not (its own scripted click never reaches the block). Two `DiagnosticsTests`
goldens (`demo.svg`/`demo.html`, the opaque-cell hatch stroke colour) failed
and were regenerated via the documented `content/diagnostics/README.md`
commands; `demo.ascii.txt` was unaffected (ASCII already renders impassable
distinctly regardless of opacity). `dotnet test` back to `319/319` after
regeneration. No `Canonical.FormatVersion` change (an existing field's
authored value, not new state).

**"This is still too fast... it flashes too quick, I didn't really notice
any new graphics."** The fire effect was only ever visible for the tick it
fired — 50ms at the sim's 20Hz rate. Fixed with the identical pattern
`orderTextHoldSeconds` already established for the same "too fast to read"
problem (TASK-042 review): new `heldFireLines`/`fireEffectHoldSeconds` (0.4s)
in `CommandDemoScene.fs` — each `FireLine` is captured into a held,
fading-out buffer in `stepOnce` and drained by real wall-clock
`deltaSeconds` in `Update`, independent of tick rate; `DrawList`'s
`fireEffects` now reads the held buffer (with a `remaining /
fireEffectHoldSeconds` alpha fade) instead of the current tick's overlays
directly. Purely presentational; both scenes' `--selfcheck` hashes
reconfirmed unchanged.

**"I'm guessing movement is performed too fast?"** Confirmed: `Terrain.
BaseMoveCost = 1` and open ground defaults to `MoveCost = 1`, so an agent
enters a new cell every tick — at the 20Hz client rate, 20 cells/second,
matching Dave's own "twice as fast as I thought" framing almost exactly.
Weighed two fixes: halving `CommandDemoScene`'s own `simHz` (a client-only
knob slowing this scene's *entire* real-time pacing uniformly — movement,
combat, suppression/stress decay, bleed-out — together) versus raising
`Terrain.BaseMoveCost` itself (decouples walking speed from every other
tick-denominated system, but is the default for every scenario in the
project, so it would ripple through most corpus entries and re-pin many
hashes well beyond this task). Tried the `simHz` halving live, then **Dave
explicitly deferred the whole discrepancy**: "let's leave it for later,
adding an actual speed to the agent would solve that discrepancy" — a real
per-agent movement-speed value (`AgentState`, presumably canonical, decoupled
from both the fixed tick rate and a client-only pacing knob) is the correct
fix, not either stopgap. The `simHz` change was reverted (`CommandDemoScene`
is back to `20.0`, unchanged from before this task); recorded as new backlog
row **B-058** (agent movement speed), not designed or implemented here.

Re-verification after the LOS fix and fire-effect hold (the two changes that
stayed) plus the `simHz` revert: `dotnet build` both `.slnx` (main; Godot
client Debug/Release) `0/0`; `dotnet test` `319/319`; `SnapshotDemo`
`--selfcheck` `MATCH 0x0DDADF2AD356E674`; `CommandDemo` `--selfcheck` `MATCH
0xF1027A36B36BC3DF` (unchanged throughout, including across the `simHz`
round-trip — confirms it never touched `StepTicksHeadless`). `git status
--porcelain` now also includes `src/CommandoWar.Headless/DemoScenario.fs`
and the two regenerated `content/diagnostics/demo.{svg,html}` goldens,
beyond this task's originally drafted Allowed scope for the LOS fix — an
authorised, in-session widening (via `AskUserQuestion`), not scope creep.

## Objective

Give the player a visible cue when a shot is fired or an agent is wounded,
incapacitated, or dies, without requiring the developer overlay (F1) to be
on.

## Why this task exists

Dave raised this directly on accepting TASK-045 (2026-09-17): "seems to work
but there is no visual indicator of fire without the overlay... perhaps we
need better graphics, maybe another free graphics pack." Confirmed by
inspection: `CommandDemoScene.DrawList()` only computed `FireLine`/wound-state
rendering inside the dev-overlay branch -- normal play had zero visual cue
for a shot or a wound. docs/06 section 8 lists "status indicators that do
not rely on colour alone" as a modern usability requirement this task must
satisfy, not just "add some colour."

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `docs/06_CONTENT_AND_PRESENTATION.md` sections 8-9
- `docs/11_BACKLOG.md` row B-057
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` (`DrawList`,
  the existing `devItems` `FireLine`/`AgentVitals` branches this task
  promotes to always-on)
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`, `IClientScene.fs`
  (`DrawItem.Kind` dispatch)
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs` (`_Draw`'s `Kind`
  switch, the TASK-041 texture-loading precedent)
- `src/CommandoWar.Headless/DiagnosticRender.fs` (`AgentVitals`'s SVG
  rendering -- the vocabulary this task mirrors client-side)
- `src/CommandoWar.Client.Godot/art/LICENSE-THIRD-PARTY.md`

## Dependencies

- B-019 (TASK-031, `FireLine`), B-031 (TASK-045, `AgentVitals`) -- both done.

## Allowed scope

- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`,
  `RenderShared.fs`, `IClientScene.fs`: promote `FireLine`/`AgentVitals`
  from developer-only to always-on rendering; `DrawItem` may gain one new
  `Kind` value (primitives only, ADR-0004's interop idiom).
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: a new static
  texture array and one new `_Draw` case, the TASK-041 precedent.
- New art under `src/CommandoWar.Client.Godot/art/` plus
  `LICENSE-THIRD-PARTY.md`, contingent on a live kenney.nl check finding
  suitable CC0 assets -- confirmed with Dave before committing to which pack.
- `docs/evidence/`, `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- No `CommandoWar.Sim`/`CommandoWar.Headless` change; no new canonical
  state; no `Canonical.FormatVersion` change -- this is a render-only
  promotion of overlay data that already exists every tick.
- No B-030 (Assault/Withdraw/Hold, ammo/cooldown model) work.
- No fog-of-war (B-055), hover highlight (B-053), facing (B-052), or
  audio-localised indicator (B-056) -- those stay their own proposed rows.
- No original-title/branding IP: any new art must be CC0 (or equivalent
  permissive) placeholder dev art, named as such in `LICENSE-THIRD-PARTY.md`
  (the TASK-041 precedent), not intended to ship in a public release.

## Required work

1. Confirm with Dave (design fork): reuse existing primitives, or source new
   art for fire/casualty feedback.
2. If new art: live-check kenney.nl for a suitable CC0 pack; confirm the
   specific choice with Dave once the existing pack's gap (or lack of one)
   is established by inspection, not assumption.
3. Promote `FireLine` to always-on player-facing rendering.
4. Promote `AgentVitals` to always-on player-facing rendering, satisfying
   "not colour alone" for each state.
5. Verify: `--selfcheck` hashes unchanged (render-only); visual confirmation
   that fire/wound/incapacitated/dead all render distinctly.
6. Update backlog/ledger/state.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] A fired shot is visible without the F1 dev overlay.
- [x] A hit and a miss are visually distinguishable by shape, not colour
      alone.
- [x] A wounded, incapacitated, and dead agent are each visually distinct
      from a healthy one and from each other, by shape/text, not colour
      alone.
- [x] Both scenes' `--selfcheck` hashes are unchanged by this task's own
      rendering/hold changes; `SnapshotDemo`'s was re-pinned once, for the
      Review round 1 LOS content fix below, unrelated to the rendering work
      itself.
- [x] No `CommandoWar.Sim` engine change. `CommandoWar.Headless` **content**
      (`DemoScenario.fs`'s terrain authoring) changed in Review round 1,
      confirmed with Dave, to fix a real LOS bug found live.
- [x] New art (if any) is CC0-licensed and documented in
      `LICENSE-THIRD-PARTY.md`.
- [x] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet build` the Godot client `.slnx` (Debug and Release): `0/0`.
- `dotnet test CommandoWar.slnx -c Release`: unaffected.
- Godot editor re-import + `--selfcheck` for both scenes: hash unchanged.
- Visual confirmation of the new rendering (screenshot evidence).
- `git status --porcelain`: matches this task's allowed scope.

## Evidence to capture

- `dotnet test` summary.
- Both scenes' `--selfcheck` output.
- Screenshot(s) showing the new rendering.

## Expected files

- `src/CommandoWar.Client.Godot/Core/{CommandDemoScene.fs,RenderShared.fs,IClientScene.fs}`
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`
- `src/CommandoWar.Client.Godot/art/` (new effect sprites),
  `LICENSE-THIRD-PARTY.md`
- `docs/evidence/`

## Documentation updates

- This task file's Outcome section.
- `docs/11_BACKLOG.md` B-057 row.
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Additive and render-only: new art files, a licence-file addition, one new
`DrawItem.Kind` value, and `_Draw`/`DrawList` changes keyed on overlay data
that already existed every tick. No authoritative state or hash change
(confirmed by the unchanged `--selfcheck` hashes). Revertible with `git
revert` in one step; deleting the three new `art/` files and reverting
`_Draw`/`DrawList` fully removes it.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
