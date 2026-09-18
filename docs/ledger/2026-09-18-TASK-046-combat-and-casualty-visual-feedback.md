## 2026-09-18 - TASK-046 - Player-facing combat and casualty visual feedback

### Why

Dave raised this directly on accepting TASK-045 (2026-09-17): "seems to work
but there is no visual indicator of fire without the overlay... perhaps we
need better graphics, maybe another free graphics pack." Confirmed by
inspection, not assumption: `CommandDemoScene.DrawList()` only computed
`FireLine`/`AgentVitals` rendering inside the `devOverlay` branch. Selected
this session via `AskUserQuestion` over B-030 proper and the smaller
client-polish rows (B-052/053/054/055/056): a direct, freshly-raised
follow-up naming a real gap in what was just shipped.

### Central decision (the task's own, resolved live this session)

**Fork 1 (`AskUserQuestion`, before any implementation)**: reuse existing
draw primitives, or source new sprite art. Dave chose new art.

**Fork 2 (`AskUserQuestion`, after a live kenney.nl check)**: downloaded and
unzipped (not assumed from memory) both the already-used "Isometric
Miniature Prototype" pack and two candidates for combat effects. Findings:
Isometric Miniature Prototype's `Characters/Human/` only has `Idle0`,
ten `Pickup*`, and ten `Run*` poses per colour variant — no wound/death pose,
and the pack has no muzzle-flash/spark/tracer art at all. Kenney's CC0
"Particle Pack" (v1.1, 2019-11-14) has real `muzzle_01..05.png`,
`spark_01..07.png`, `trace_01..07.png`, `smoke_01..10.png`. Kenney's CC0
"Game Icons" base + expansion packs have only a generic `cross.png` (medical
cross) — no skull, no distinct incapacitated icon; grep across both zips for
`skull|cross|heart|health|dead|bandage|first.?aid|bone` confirmed this.
Presented this finding to Dave with three options (Particle Pack + primitives
for vitals / Particle Pack + cross-icon-for-wounded / keep searching).
Confirmed: **Particle Pack for fire feedback, primitives for vitals** — a
mismatched cross badge on a casualty state it wasn't designed for was
explicitly rejected rather than force-fit.

### Changes

**New `src/CommandoWar.Client.Godot/art/`**: `effect_muzzle_flash.png`
(from `flare_01.png`), `effect_impact_hit.png` (from `spark_02.png`),
`effect_impact_miss.png` (from `smoke_03.png`) — resized from the pack's
native 512x512 to 96x96 (ImageMagick `-resize 96x96 -strip`; the on-screen
sprite draws at ~20-32px, the `terrain_*`/`agent_human.png` precedent of
keeping source art close to display scale) and alpha-boosted
(`-channel A -level 0%,45%`) since the pack's soft particle gradients read
very faintly under plain alpha blending (no additive blend mode exists in
this renderer) — confirmed necessary by direct visual inspection before and
after. `LICENSE-THIRD-PARTY.md` gains a second pack section naming the exact
source files, version, URL, and CC0 text, plus a note on why a second pack
was needed (the first has no combat-effect sprites) and the resize/alpha
note.

**`Core/IClientScene.fs`**: `DrawItem.Kind` doc comment extended for a new
value `4` — a one-shot effect sprite centred at `(Cx,Cy)`, `Radius` as
on-screen size, `TextureId` selecting the sprite (`0` muzzle flash, `1`
impact hit, `2` impact miss — a distinct shape per outcome, not a
colour-only hit/miss tint, docs/06 "status indicators that do not rely on
colour alone"). No new `DrawItem` fields — `TextureId`/`R`/`G`/`B`/`A`/
`Radius` already existed and sufficed.

**`Core/RenderShared.fs`**: new `effectSprite` (the `cellMarker`/
`lineMarker` precedent — builds a `Kind = 4` `DrawItem`).

**`Core/CommandDemoScene.fs`** (`DrawList`):
- `agentItems` rewritten from `Array.map` to `Array.collect`, now reading
  each agent's `AgentVitals` from `devFrame.Overlays` (already computed every
  tick regardless of the F1 toggle — confirmed by reading `Ready`/`stepOnce`,
  neither gates `devFrame` on `devOverlay`) and branching: `Dead` draws a
  small black `Kind = 2` cross instead of the figure (two crossed line
  segments, `d = 0.28` cell-units, the `DiagnosticRender.Svg` dead-cross
  precedent adapted to fractional isometric coordinates since `Cell` itself
  is integer-only); `Incapacitated` draws the figure at 50% RGB plus a
  `"down"` `Kind = 3` text badge (`cellLabel`, the player-vocabulary
  precedent — no bleed-out tick count, that stays developer-only per
  `devReasonText`'s own distinction); `Alive` below max health keeps the
  normal figure and adds a small red `Kind = 1` wound dot via `cellMarker`,
  opacity scaled by wound severity — the dot's *presence*, not a tint on the
  agent itself, is the signal (satisfies "not colour alone" the same way the
  `Dead` cross does).
- New `fireEffects`: derived from `FireLine` overlays (already computed every
  tick, `frameOf`'s own doc comment), unsorted and appended after the depth
  sort — the existing `devItems` rationale applies identically (a two-cell
  line has no single meaningful depth). Builds a thin tracer (`lineMarker`,
  reused) plus two `effectSprite` calls (muzzle at the shooter's cell, hit or
  miss sprite at the target's cell).
- Final composition changed from `Array.append sorted devItems` to
  `Array.concat [ sorted; fireEffects; devItems ]` — `fireEffects` always
  renders; `devItems`' own separately-gated `fireLines`/`losItems` etc. are
  untouched and still only render with F1 on (some visual redundancy when
  both are on simultaneously — the dev overlay's own thicker/differently
  -coloured line plus the new player-facing tracer+sprites — accepted as
  harmless, not worth suppressing one).

**`src/FSharpSceneHost.cs`**:
- New `static readonly Texture2D[] EffectTextures` (three entries, loaded
  once via `GD.Load<Texture2D>`, the `TerrainTextures`/`AgentTexture`
  precedent).
- `_Draw`'s switch gains `case 4: DrawEffectSprite(pos, item.TextureId,
  item.Radius, color); break;`.
- New `DrawEffectSprite`: centred (not foot-anchored, unlike an agent
  figure) at `pos - (0, TileH * 0.5f)` — the same anchor `cellMarker`/agent
  items use, so an effect lines up with whatever agent/marker occupies that
  cell. `Mathf.Clamp` on `textureId` mirrors `DrawTerrainTile`'s existing
  guard.

No `CommandoWar.Sim`/`CommandoWar.Headless` change; no new canonical state;
`Canonical.FormatVersion` untouched.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0/0` (unaffected).
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug` and `-c Release`
  - Result: `0/0` both.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `319/319` (unaffected — no `CommandoWar.Sim`/`CommandoWar.Headless`
    change).
- Command: `"$GODOT" --editor --headless --quit --path .` (re-import after
  adding the three new PNGs)
  - Result: clean; `.import` sidecars generated, `compress/mode=0` (Lossless),
    matching the existing `terrain_*`/`agent_human` import settings.
- Command: `"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck`
  - Result: `MATCH expected final hash 0x44B29B73E8F107EF at tick 20`, exit
    `0` — unchanged from TASK-045, re-confirmed a second time after the
    evidence-gathering deviation below was fully reverted.
- Command: `"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck`
  - Result: `MATCH expected final hash 0xF1027A36B36BC3DF at tick 20`, exit
    `0` — unchanged from TASK-045, likewise re-confirmed after revert.
- Command: `"$GODOT" --path . scenes/CommandDemo.tscn -- --screenshot <path>`
  (standard, paused-at-tick-0 evidence path)
  - Result: `docs/evidence/task-046-combat-visual-feedback-regression.png`
    committed — pixel-identical composition to the pre-existing TASK-041/043
    evidence (halo, route preview, pending order dots all unchanged),
    confirming the shipped change is inert until a shot/wound actually
    occurs.
- Manual: `git status --porcelain`
  - Result: matches this task's allowed scope exactly (the five `Core/`/
    `src/FSharpSceneHost.cs`/`LICENSE-THIRD-PARTY.md` render-side files, three
    new `art/*.png`, two new `docs/evidence/*.png`).

### Evidence

- `docs/evidence/task-046-combat-visual-feedback-regression.png` (standard
  paused capture, regression check).
- `docs/evidence/task-046-fire-and-wound-feedback.png` (see Deviations below
  for how this was captured).
- Both scenes' `--selfcheck` hash sequences, unchanged from TASK-045.

### Deviations and unresolved issues

- **The standard `--screenshot` evidence path cannot show combat.**
  `CommandDemoScene`'s screenshot priming (`FSharpSceneHost.cs`) selects an
  agent, issues one order, and immediately pauses — by design, so the
  orange "pending" route stays visible for the whole capture window
  (TASK-040's own precedent). Paused at tick 0 forever, no shot can ever
  fire. To get real evidence of the new rendering, a **temporary, local-only**
  modification was made and fully reverted before finishing: removed the
  pause, retargeted the scripted click from `(3,0)` to `(11,6)` (adjacent to
  `DemoScenario`'s hostile at `(11,7)`, `CombatConfig.WeaponRange = 7`), and
  changed the capture trigger from a fixed frame count to "one frame after
  `DrawList()` first reports a `Kind = 4` item" (`GetViewport().
  GetTexture().GetImage()` reads the last **completed** `_Draw`, not the one
  `QueueRedraw()` scheduled this same frame — confirmed via a temporary
  `GD.Print` inside `DrawEffectSprite` showing its calls landing strictly
  *after* the "screenshot written" log line on the naive capture, and
  strictly *before* it once delayed by one frame). Direct instrumentation
  during this pass additionally confirmed, at the Godot API level (not just
  by eye), that `DrawEffectSprite` receives a valid non-null `96x96`
  `Texture2D`, the correct on-screen rect for each effect's cell (matching
  hand-computed `CellToScreen` values), and the correct tint for each of the
  three `TextureId`s. None of this temporary code is in the shipped diff —
  confirmed by `git diff`/`git status --porcelain` after reverting, and by
  re-running both `--selfcheck`s a second time afterward.
- **Known limitation, not blocking**: the Particle Pack's soft
  alpha-gradient sprites read as a fairly subtle blob against this terrain's
  dark-green palette under plain alpha blending — this renderer has no
  additive/glow blend mode. Boosted the alpha curve and enlarged the sprites
  once already (11px/13px radius -> 16px; alpha `-level 0%,45%`); a further
  pass (an additive `CanvasItemMaterial`, or brighter/harder-edged
  replacement art) would sharpen this further. Flagged as a possible
  fast-follow, not a functional gap — the effect is genuinely present and
  correctly positioned/tinted, confirmed above.
- Not built (Forbidden scope, as drafted): B-030 executors, fog-of-war
  (B-055), hover highlight (B-053), directional facing (B-052), audio
  indicator (B-056) — all stay their own proposed rows.

### Review round 1 (2026-09-18)

Dave tried it live and raised three points.

**"Are the agents firing through the scenery?"** Confirmed real, not a
rendering artifact — traced `Sight.trace` directly against `DemoScenario`'s
terrain for the review session's own shooter/target cells `(7,0)`/`(11,7)`:
the path runs through `(8,2)`, `(8,3)`, `(9,3)`, the impassable 2x2 block,
authored `Opaque = false` (only the separate opaque-wall feature at `x = 6`
was authored correctly). `Sight.trace`/`Combat.chooseTarget` gate solely on
`opaque`, so the engine behaved exactly as authored — a demo-content
authoring gap, not an engine bug. Confirmed with Dave via `AskUserQuestion`
to fix now rather than file separately, accepting the wider blast radius.
Fixed: the block's four cells' `Opaque` flag flipped `false -> true` in
`src/CommandoWar.Headless/DemoScenario.fs`. Consequences: `SnapshotDemo`'s
`--selfcheck` hash changed (its scripted route passes near the block) and
was re-pinned in `FSharpSceneHost.cs`
(`0x44B29B73E8F107EF -> 0x0DDADF2AD356E674`); `CommandDemo`'s did not (its
own scripted click never reaches the block). Two `DiagnosticsTests` goldens
(`demo.svg`/`demo.html` — the opaque-cell hatch stroke colour, `#cccccc ->
#333333`) failed and were regenerated via `content/diagnostics/README.md`'s
documented commands; `demo.ascii.txt` was byte-identical (ASCII already
renders impassable distinctly regardless of opacity). `dotnet test` back to
`319/319`. No `Canonical.FormatVersion` change (an existing field's
authored value, not new state).

**"This is still too fast... it flashes too quick, I didn't really notice
any new graphics."** The fire effect was visible for exactly the tick it
fired — 50ms at 20Hz. Fixed with the identical pattern
`orderTextHoldSeconds` already established for this exact "too fast to
read" problem (TASK-042 review): new `heldFireLines: ResizeArray<Cell * Cell
* bool * float>` and `fireEffectHoldSeconds = 0.4` in `CommandDemoScene.fs`.
`stepOnce` appends each tick's `FireLine`s into the held buffer;
`Update(deltaSeconds)` decrements every entry's remaining time by real
wall-clock delta (not gated on `paused`, so a flash already showing does not
freeze forever if the player pauses mid-flash) and drops expired entries;
`DrawList`'s `fireEffects` now iterates the held buffer instead of
`devFrame.Overlays` directly, scaling each effect's alpha by `remaining /
fireEffectHoldSeconds` so it fades rather than cutting off. Purely
presentational; both scenes' `--selfcheck` hashes reconfirmed unchanged.

**"I'm guessing movement is performed too fast?" / "do we need the
simulation slowing down or just the movement rate?"** Confirmed:
`Terrain.BaseMoveCost = 1` and open ground defaults to `MoveCost = 1`, so an
agent enters a new cell every tick — 20 cells/second at the client's 20Hz
rate, matching Dave's own "twice as fast as I thought" framing closely.
Walked through the distinction directly (not via `AskUserQuestion` — a
direct technical exchange, not a scope fork needing a formal choice):
halving `CommandDemoScene`'s own `simHz` (client-only, slows this scene's
*entire* real-time pacing uniformly — movement, combat, suppression/stress
decay, bleed-out — together; `StepTicksHeadless`/`--selfcheck` bypass
`Update`/`simHz` entirely, so zero hash risk) versus raising `Terrain.
BaseMoveCost` itself (decouples walking from every other tick-denominated
system, but `MoveCost = 1` is the default for every scenario in the
project, so it would ripple through most corpus entries and re-pin many
hashes well beyond this task). Tried the `simHz` halving (`20.0 -> 10.0`)
live, then **Dave explicitly deferred the whole discrepancy**: "let's leave
it for later, adding an actual speed to the agent would solve that
discrepancy" — a real per-agent movement-speed value is the correct fix,
not either stopgap discussed. Reverted `simHz` back to `20.0` (unchanged
from before this task). Recorded as new backlog row **B-058** (agent
movement speed), not designed or implemented here.

Re-verification after the LOS fix and fire-effect hold (the two changes
that stayed) plus the `simHz` revert: `dotnet build CommandoWar.slnx -c
Release` `0/0`; `dotnet build` the Godot client `.slnx` (Debug/Release)
`0/0`; `dotnet test` `319/319`; `SnapshotDemo` `--selfcheck` `MATCH
0x0DDADF2AD356E674`; `CommandDemo` `--selfcheck` `MATCH 0xF1027A36B36BC3DF`
(unchanged throughout, including across the `simHz` round-trip). `git
status --porcelain` now additionally includes
`src/CommandoWar.Headless/DemoScenario.fs` and the two regenerated
`content/diagnostics/demo.{svg,html}` goldens, beyond the task's originally
drafted Allowed scope for the LOS fix specifically — an authorised,
in-session widening, not scope creep.

### Documents updated

- `tasks/TASK-046-COMBAT-AND-CASUALTY-VISUAL-FEEDBACK.md` (`proposed ->
  review`, Outcome section, Review round 1 section, acceptance criteria
  checked).
- `docs/11_BACKLOG.md` B-057 row (`proposed -> review`); new B-058 row filed
  (agent movement speed, not designed).
- `content/diagnostics/README.md`: no row change (existing `demo.*` entries
  regenerated in place, not new files).
- `docs/12_PROGRESS_LEDGER.md` (this row).
- `PROJECT_STATE.yaml`.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-18)
