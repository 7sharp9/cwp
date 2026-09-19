# TASK-056: Correct agent facing rotation and add run-cycle animation

Owner: Dave (implementing agent session)
Source revision: `main`, mid-review of TASK-054/TASK-055 (both uncommitted at
the time this task started).
Environment: `dotnet` `10.0.303`, Godot `4.7.2.stable.mono` (real editor),
Windows 11.

## Selection

Not a scoped-in-advance task: Dave live-tested TASK-054's B-052 facing
feature (already corrected once this session, both a projection bug and a
wrong art-source assumption) and found two further problems: the facing
was still roughly 30 degrees off, and a moving agent never animated,
freezing on a single static idle-rotation frame for its whole transit.
Both issues, and the instruction to verify against a real Godot view this
time rather than re-deriving the math again on paper, were specified in
enough detail to draft directly without an `AskUserQuestion` round.

## Central decisions

See the task file's own Central decisions section -- all four were
specified directly by Dave's instruction, not resolved via
`AskUserQuestion`.

## Investigation before drafting

- Re-read `RenderShared.facingBin` and its own doc comment, which
  documented the *previous* (buggy) assignment directly: `world NW -> bin 0
  (N)`, `world NE -> bin 2 (E)`, `world SE -> bin 4 (S)`, `world SW -> bin 6
  (W)` -- i.e. the four Kenney cardinal poses assigned to the four
  world-diagonal moves.
- Re-examined `Information.png` directly (`C:\Users\Dave\Downloads\
  kenney_isometric-miniature-prototype\Information.png`), not from memory:
  its diamond legend labels the tile's four *edges* `N` (upper-right), `E`
  (lower-right), `S` (lower-left), `W` (upper-left) -- not the diamond's
  vertices. Worked out that the centre-to-`N`-neighbour displacement is
  exactly twice the centre-to-edge-midpoint vector, i.e. a
  `(half-width, -half-height)` screen step -- which this project's own
  `CellToScreen` (`screenX = (cx-cy)*TileW/2`, `screenY = (cx+cy)*TileH/2`)
  produces for a **world pure-axis** move (`dx=0,dy=-1`), not a
  world-diagonal one. This directly contradicted the previous assignment.
- Computed the *true* screen bearing (via the existing `atan2`-based
  formula) for each of the 8 primary world directions and found every one
  landed exactly one 45-degree bin past its correct target under uniform
  45-degree-sector rounding -- explains why the bug read as "close but off"
  (Dave's own "roughly 30 degrees" estimate) rather than random, and why a
  simple constant-angle-offset patch was tempting but wrong: the 8 correct
  target angles are not evenly 45-degrees apart under this anisotropic
  (2:1) projection (they cluster at alternating ~26.6/~63.4-degree gaps), so
  a uniform shift only happens to repair the 8 exact test vectors, not a
  general `(dx,dy)` heading. Confirmed `AgentSnapshot.Destination` is the
  agent's overall target cell, not its next path step (`committedItems`
  already reads it this way), so `facingBin` is frequently called with a
  non-primary-direction delta in practice.
- Chose a nearest-reference-vector match (cosine similarity against the 8
  primary directions' own screen-projected vectors) over any angle-sector
  scheme: correct by construction for the 8 primary directions (each is
  trivially its own best match) and correct in general, since it compares
  real projected vectors rather than assuming uniform angular spacing.
- `PIL` bounding-box scan across all 8 directions' `Idle0` + all 80 `Run`
  frames (`python3`, `Image.getbbox()` per frame, unioned): a per-direction
  shared bbox varies in size (largest ~124x145px, a running gait's
  silhouette extent varies more than a static idle pose's), larger than the
  existing idle crop rect (`(99,323,58,138)`, TASK-054's own crop). Chose
  one global shared crop rect across all 88 frames,
  `(66,309)-(190,466)` (124x157px), so every texture (idle and every run
  frame, every direction) is pixel-identical in size/aspect ratio --
  `DrawAgentFigure` derives drawn height from each texture's own aspect
  ratio at a fixed on-screen width, so mismatched aspect ratios would pop
  the figure's size the instant it starts/stops moving.
- Confirmed `DrawItem.Cx2`/`Cy2` are unused (always `0.0f`) for every
  existing `Kind = 1` real-agent item, so repurposing `Cx2` as a run-frame
  index (a `-1` sentinel for idle) needed no new `DrawItem` field -- the
  established "a spare field means something different per `Kind`"
  precedent (`Kind = 4`'s `TextureId`, `Kind = 2`'s `Cx2`/`Cy2` as a second
  endpoint).

## Changes

- `src/CommandoWar.Client.Godot/art/`: `agent_human_facing0..7.png`
  re-cropped in place (same paths, same source `Human_N_Idle0.png`, larger
  shared rect); 80 new `agent_human_run{0-7}_{0-9}.png` files (`Human_N_
  RunF.png` cropped to the identical rect); `LICENSE-THIRD-PARTY.md`
  updated with the new crop rect and file table.
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`: `facingBin` rewritten
  from an `atan2`-plus-uniform-bin-round to a nearest-reference-vector
  match against a new `worldUnitDirections` table, via a new private
  `toScreenVector` helper (factoring out the existing iso-skew projection).
  New `RunFrameSeconds` literal (`0.06`, a full 10-frame loop in 0.6s) and
  pure `runFrameIndex (runClock: float) : int`.
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`: `DrawItem`'s doc
  comment extended for `Cx2`'s new meaning on a full-opacity `Kind = 1`
  item (a run-frame index, `< 0` = frozen idle).
- `src/CommandoWar.Client.Godot/Core/DemoRenderScene.fs`,
  `CommandDemoScene.fs`: a new `mutable runClock: float`, advanced by real
  elapsed time in `Update` (`CommandDemoScene`: only `if not paused`,
  alongside the existing tick catch-up loop, so the run cycle never
  animates while the sim itself is frozen; `DemoRenderScene`: unconditional,
  since that scene has no pause concept). `agentItems`/`renderVitals`'s
  `Alive` branch compute `isMoving = a.Destination |> Option.exists (fun d
  -> d <> a.Position)` and populate `DrawItem.Cx2` with
  `RenderShared.runFrameIndex runClock` while moving, `-1.0f` otherwise;
  `renderVitals`'s `Incapacitated` branch always passes `-1.0f` (frozen
  idle regardless of any stale `Destination`).
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: `AgentFacingTextures`
  renamed `AgentIdleTextures` (comment records the rename); new flat
  `AgentRunTextures` (80 entries, `dir*10+frame`, loaded via
  `Enumerable.Range(0,80).Select(...)`, the existing `using System.Linq`
  already in scope); `DrawAgentFigure` takes a new `runFrame` parameter
  (`< 0` selects the idle texture, `0..9` the matching run frame) and the
  `_Draw` call site passes `Mathf.RoundToInt(item.Cx2)`.
- A temporary command-line probe (`--facing-probe-dir <dx> <dy>`, plus a
  one-off hardcoded variant for the animation check) added to
  `FSharpSceneHost.cs` for live Godot verification, then **fully removed**
  once both fixes were confirmed (see Verification) -- the `dotnet fsi`
  scratch-probe precedent, applied to a real windowed Godot view since
  that is what this task's own diagnosis required.

No `CommandoWar.Sim`/`CommandoWar.Headless` file touched.

## Deviations found during implementation

- **Test-setup gotcha, not a product bug**: the first animation-check
  traversal (straight down the `x=0` column) ran directly through agent 1's
  cell -- agent 1 never moves in this scene, and the two agents' route
  reservation/contention logic stalled agent 0 indefinitely a few cells in,
  never reaching its destination even after many seconds of wall-clock
  time. Confirmed by inspection (the `pending`/route-dot overlay showed the
  full intended path, `order=accepted`, but `Position` never advanced past
  the contested cell) before concluding it was a test-path choice, not a
  facing/animation defect. Switched the probe to an obstacle- and
  agent-clear row.
- The first combat-adjacent probe target (the original 8-direction test
  base cell, `(5,3)`, `+/-2` radius) put several of the 8 targets within
  `CombatConfig.WeaponRange` of `DemoScenario`'s lone hostile, so two of the
  eight captured screenshots showed the agent `Incapacitated` mid-test
  (still usable evidence -- the `Incapacitated` branch still renders the
  same frozen facing texture -- but noisier to read). Moved the probe's
  base cell to `(2,2)` (Chebyshev distance 9-11 from the hostile, outside
  `WeaponRange` but still inside `SightRange`, so no combat) for the
  cleaner second pass whose composite is the committed evidence.

## Verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet test`: `343/343` (unaffected -- no `CommandoWar.Sim`/
  `CommandoWar.Headless` file touched).
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: `0/0`.
- Real Godot 4.7.2 editor, headless `--import`, to pick up the 80 new art
  files (needed once before any windowed run could load them -- the first
  attempt without it crashed `DrawAgentFigure` on a null texture).
- Real Godot 4.7.2 editor, windowed `--screenshot` through the temporary
  probe, for all 8 primary directions from `(2,2)`: each capture's pose
  compared directly against a fresh crop of `Information.png`'s own 8-pose
  reference grid. Confirmed a consistent, coherent pattern matching the
  diamond legend exactly -- the `N`/`W`/`NW` cluster (the legend's two
  "upper" edges plus their shared diagonal) reads as a back view in both
  the pack's own reference image and every one of this project's matching
  captures; the `E`/`S`/`SE` cluster (the two "lower" edges plus their
  diagonal) reads as a front view in both; the crossing diagonals (`NE`,
  `SW`) read as transitional side views in both. Composite saved as
  `docs/evidence/task-056-facing-compass-check.png`.
- Real Godot 4.7.2 editor, windowed `--screenshot`, a long (9-cell)
  obstacle-clear traversal: two screenshots at different elapsed times
  during transit (tick 5, tick 11) show genuinely different run-cycle limb
  positions (verified by direct pixel comparison, not just visual
  impression); three further screenshots well after arrival (ticks 25, 35,
  99) show the identical frozen standing pose each time, confirming the
  freeze-on-arrival behaviour holds and does not drift or re-animate.
  Composite saved as `docs/evidence/task-056-run-animation-check.png`.
- Godot editor `--selfcheck`, headless, through the real 4.7.2 editor, all
  three scenes:
  - `SnapshotDemo.tscn`: `MATCH 0xF422ACB8D5A86FF0`, exit 0.
  - `CommandDemo.tscn`: `MATCH 0x00D3D471EF7354BC`, exit 0.
  - `AppraisalDemo.tscn`: `MATCH 0x194805888CBE240D` (format 11), exit 0.
  All three unchanged from TASK-054/055's pins, as expected -- no
  `Simulation.step`/canonical-state change.
- `git status --porcelain`: matches this task's allowed scope exactly (the
  temporary probe hook is not present in the final diff -- confirmed via a
  direct `grep` for its field/method names after removal, zero matches).

## Review round 1 (2026-09-19, Dave live-tested the corrected facing and new animation)

Dave's report, verbatim: "its better but i dont think it always stay the
correct facing. also theres some sort of jerkyness to the movement, it
also also moving really fast too."

### Diagnosis

Re-read `Simulation.navigationAndMovement`'s own doc comments (already
quoted in this file's Investigation section above): `AgentState.Position`
only changes once an entire grid edge completes; `Progress` accumulates
across several mid-edge ticks first (TASK-018). The rendering in both
scenes (`renderVitals`'s `Alive` branch, `DemoRenderScene.agentItems`)
lerped screen position between `prevAgents` (the *previous tick's* discrete
`Position`) and the *current tick's* discrete `Position`, using a
render-frame-local `alpha` in `[0,1]`. On every mid-edge tick these two
values are identical (`Position` has not moved yet), so the lerp is a
no-op; only on the one tick an edge actually completes do the two values
differ, and the entire cell's worth of on-screen displacement gets
compressed into that single tick's `alpha` window (50ms at the 20Hz sim
rate). Visually: hold still for most of an edge's real duration, then
snap across the whole cell in one short burst, repeat -- textbook jerky,
and because the whole displacement happens in such a short window, it also
reads as unnaturally fast even though the underlying tick-by-tick pace
(`DemoScenario`'s Trooper `MoveSpeed`, TASK-049) had not changed.

The same root cause explains the facing complaint. `facingBin` was fed
`a.Destination` directly -- the agent's *overall final target cell*, not
its immediate next step. `AgentSnapshot.Destination` stays the far-off
final cell for the whole route (`committedItems`'s own doc comment already
established this); on a straight leg the crow-flies bearing to that far
cell happens to equal the true immediate walking direction, so facing
looked right, but the moment the route bends around terrain, or the
player redirects mid-route to a genuinely different direction, the raw
bearing to the still-distant original target diverges from the cell the
agent is actually stepping into -- exactly "doesn't always stay the
correct facing."

### Fix

Both fixes share one new per-tick computation: `nextStepCell`, the agent's
true immediate next path cell, found via `Pathfinding.find state.Terrain
a.Position d` (the `committedItems` route-preview precedent, just cached
once per tick in `stepOnce`/`advanceOneTick` instead of recomputed every
render frame -- cheaper than what the route-preview code already does at
render rate).

- **Facing**: `facingBin`'s `destination` argument is now `nextStepCell`,
  not `a.Destination` -- `facingBin` itself needed no change, since it
  already treats "destination" generically as "the cell to face toward."
- **Smoothness**: the render-time lerp target is now `nextStepCell` (not
  `a.Position`); the lerp fraction is `(a.Progress + alpha) / edgeTickEstimate`,
  clamped to `[0,1]`. `a.Progress` is already exact and canonical, so no
  new state is derived -- only its *denominator* is a heuristic:
  `Terrain.moveCost` and the agent's own `MoveSpeed` together determine the
  exact tick-count an edge takes (`Simulation.fs`'s `wouldComplete`), but
  `MoveSpeed` is not exposed to the client (deliberately not added here --
  that would be a `CommandoWar.Sim` change, outside this task's forbidden
  scope) and adding it was never asked for. Instead, `edgeTickEstimate` is
  *learned*: the tick a `prevAgents` comparison detects an edge just
  completed, the just-crossed threshold (`prevProgress + 1`) becomes the
  estimate for that agent's *next* edge. A `defaultEdgeTickEstimate = 2`
  (the Trooper half-speed ratio this file already assumes throughout, via
  `DemoScenario`) seeds the very first edge before any data exists. This
  self-corrects every edge regardless of a wrong guess, since `Position`
  itself always snaps to the true cell the instant an edge genuinely
  completes -- a wrong estimate only ever causes one edge's pacing to be
  slightly off, never an incorrect end state.

Applied identically to `CommandDemoScene` and `DemoRenderScene` (the
established "one `FSharpSceneHost` draw path, both scenes behave the
same" precedent). No `CommandoWar.Sim`/`CommandoWar.Headless` change.

### Verification

- A temporary command-line burst-screenshot probe (`--motion-probe
  <dir>`, `FSharpSceneHost.cs`, fully removed before this round finished):
  selects agent 0, issues `MoveTo(0,7)`, and after 1 real second redirects
  to `MoveTo(3,7)` -- a genuine mid-route direction change, south to east.
  Captures a screenshot every 0.12 real seconds for 24 shots (~2.9s).
- **First attempt** routed near enough to `DemoScenario`'s lone hostile
  (target `(10,4)`, close to the hostile at `(11,7)`) to trigger real
  combat partway through -- the agent was shot and went `Incapacitated`,
  confirmed by inspecting the captured frames directly (`order=unable:
  critically wounded`, a `"down"` badge visible). The exact same mistake
  as the main TASK-056 round's first facing-compass attempt. Fixed by
  moving the whole route to `(0,0) -> (0,7) -> (3,7)`, whose every point is
  Chebyshev distance >= 8 from the hostile, outside `CombatConfig.
  WeaponRange` (7).
- **Smoothness**: a Python/`PIL` script tracked the moving agent's
  on-screen centroid (blue-tinted pixels, agent 1's fixed position
  excluded) across all 24 frames and converted each to an approximate
  grid-cell coordinate via the inverse of `FSharpSceneHost.cs`'s own
  `CellToScreen`. Confirmed continuously, monotonically increasing
  fractional coordinates across the whole south-then-east traversal (e.g.
  `cy`: `-0.18, 0.78, 1.83, 3.18, 4.21, 5.29` over six consecutive 0.12s
  samples -- no flat stretches, no large jumps), the opposite of the old
  hold-then-snap pattern. Saved as
  `docs/evidence/task-056-round1-smoothness-sequence.png`.
- **Facing on a real direction change**: cropped and compared the frame
  just before the redirect (heading south) against a frame shortly after
  (heading east) -- visibly different poses, confirming `facingBin` tracks
  the actual current heading rather than staying stuck on the pre-redirect
  bearing. Saved as `docs/evidence/task-056-round1-facing-on-turn.png`.
- `dotnet build` both `.slnx` (main Release; Godot client Debug) `0/0`;
  `dotnet test` `343/343` (unaffected).
- Godot editor `--selfcheck`, headless, through the real 4.7.2 editor, all
  three scenes reconfirmed `MATCH`, unchanged from every prior pin
  (`SnapshotDemo.tscn` `0xF422ACB8D5A86FF0`, `CommandDemo.tscn`
  `0x00D3D471EF7354BC`, `AppraisalDemo.tscn` `0x194805888CBE240D`) -- as
  expected, this round is render-interpolation-only, no `Simulation.step`
  change.
- `git status --porcelain`: the temporary `--motion-probe` hook is not
  present in the final diff (`grep` for its field/method names after
  removal, zero matches).

### Not addressed, flagged for Dave

Whether the underlying grid movement pace itself (`DemoScenario`'s
`TrooperMoveSpeed`, half of `Agent.MoveSpeedDefault`, TASK-049) is still
"too fast" once the jerkiness is gone was not evaluated further -- that
constant lives in `CommandoWar.Headless` (content authoring, not
rendering), outside this task's declared forbidden scope, and changing it
is Dave's own call to make after trying the smoothed version live.

## Review round 2 (2026-09-19, Dave live-tested round 1's fix again: "seemed the same")

Dave's report, verbatim: "i just tried again and it seemed the same, did
you not fix this?" -- plus two new, specific observations round 1's own
probe never checked: "the issue seems to be the circle thats drawn as
selection indicator, it jumps between cells" and "i also noticed the
animation runs continously is the agents get stuck on the same cell etc."

### Ruling out a stale build

Rebuilt both `.slnx` (`dotnet build CommandoWar.slnx -c Release`; `dotnet
build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`)
from the exact round-1-committed source before making any further change,
confirming a clean, current build was not the explanation.

### Diagnosis

Read `CommandDemoScene.fs` end to end again rather than trusting round 1's
own reasoning. Found two real, concrete gaps, both explaining Dave's exact
words:

1. **`haloItems` (the selection halo)** built its `Cx`/`Cy` from
   `selected |> Option.bind agentPosition` -- `agentPosition` returns the
   raw, discrete `AgentSnapshot.Position` `Cell`, completely bypassing
   round 1's `nextStepCell`/`edgeTickEstimate` lerp that the real figure
   (`renderVitals`'s `Alive` branch) already used. The halo is the single
   largest, most visually dominant element while watching a selected agent
   move (translucent, `haloRadius = 34.0f` vs. the figure's own
   `agentRadius = 20.0f`) -- a correctly-smoothed figure sitting under a
   halo that still snaps a whole cell at a time on every edge completion
   reads, at a glance, as if nothing changed at all.
2. **`isMoving`** (both scenes) was still exactly
   `a.Destination |> Option.exists (fun d -> d <> a.Position)` -- true for
   as long as an order is unmet, regardless of whether the agent is
   actually advancing. `Simulation.navigationAndMovement`'s own doc
   comments (already read in this file's Investigation section above)
   describe a route-reservation contest loser as `Progress = startProgress`
   -- frozen, not incremented -- while `Destination` stays set
   indefinitely. Such an agent's `isMoving` therefore stayed `true`
   forever, so `RenderShared.runFrameIndex runClock` kept advancing the
   run-cycle frame forever too: a stuck agent visibly "runs in place"
   without end. The identical gap also meant the render-time
   `edgeFrac = (Progress + alpha) / estTicks` kept crediting the
   per-render-frame `alpha` ramp (`[0,1]` every tick period) to an edge
   that would never actually complete -- a small forward-creep, then a
   snap back to the same start fraction when the next tick confirmed no
   real progress, repeating every tick period. A second, subtler source of
   "jumps between cells", on top of the halo gap above.

### Fix

A new `stalled: Map<int, bool>` (both scenes), recomputed fresh every tick
in `stepOnce`/`advanceOneTick` (not accumulated): for each agent, compares
its `Progress` after this tick's `Simulation.step` against the value it
held entering the tick (`priorProgress`, the same snapshot
`edgeTickEstimate` already reads) -- unequal means real progress happened;
equal while `Destination` is still unmet means a route-reservation contest
(or any other cause) produced zero real advancement this tick, whatever the
specific reason. Also seeded `prevProgress` from each agent's real starting
`Progress` in `Ready()` (previously left at `Map.empty`, defaulting every
lookup to a `-1` sentinel) -- without this, `stalled`'s very first tick ever
compares against `-1`, misreading a route that stalls immediately (as this
round's own verification scenario does) as "made progress" (`0 <> -1`)
purely from the sentinel, not the agent's real behaviour.

`CommandDemoScene.fs` gained a new shared `renderPos (a: AgentSnapshot) :
float32 * float32`, factored out of `renderVitals`'s own inline lerp: same
`nextStepCell`/`edgeTickEstimate` computation as round 1, but the `edgeFrac`
drops the `alpha` term entirely while `stalled` (frozen at
`Progress / estTicks` exactly, no ramp). Both the real figure (`renderVitals`'s
`Alive` branch) and `haloItems` now call this one function for the same
agent, so they cannot disagree. `haloItems` guards on `vitalsOf a.Id`:
`Alive` uses `renderPos`; `Dead`/`Incapacitated` uses the agent's raw
`Position` instead, matching `renderVitals`'s own never-lerped figure for
those states (a `renderPos` call on a stale, never-cleared `Destination`
left over from before a casualty went down would otherwise pull the halo
off of a figure that itself never moves). `isMoving` (both scenes) gained
`&& not stalled`. `DemoRenderScene.fs` got the identical `stalled` freeze
(no halo there to fix, but the same run-cycle-forever and sawtooth gaps
existed identically) -- the established "one `FSharpSceneHost` draw path,
both scenes behave the same" precedent.

The now-dead local `lerp` helper inside `CommandDemoScene.DrawList()` (its
only caller was the inline figure computation this round replaced with
`renderPos`) was removed rather than left unused.

### Verification

A temporary, fully-removed `--stall-probe <dir>` burst-screenshot probe
(`FSharpSceneHost.cs`, the `--motion-probe`/`--facing-probe-dir`
precedent) -- deliberately NOT a repeat of round 1's single-agent redirect
probe (which already "looked clean" once and did not match Dave's actual
experience): selects agent 0, issues a real `MoveTo(5,0)` (open terrain,
unblocked -- proves the halo tracks correctly during genuine motion first),
then after 1.2 real seconds redirects to `MoveTo(0,3)`, a route that must
enter agent 1's own permanently-occupied cell `(0,1)` -- the exact
route-reservation contest round 1's own "Deviations found" section hit by
accident on this identical scenario, reproduced here on purpose. Captured
30 screenshots at a 0.15s real-time interval (4.5s total) through the real
Godot 4.7.2 editor, windowed.

- **Unblocked motion** (`t=0.15s` through arrival at `(5,0)`): the halo
  visibly sits directly around the running figure at every captured
  instant, moving together -- no independent snap. (composite top panel)
- **Genuine stall**: after the redirect, agent 0 approaches `(0,1)` and
  stalls just short of it (confirmed via the HUD's `order=accepted` text
  and `draws 0`, i.e. no combat explains any of this). A pixel-diff across
  all 16 captures from `t=1.95s` (frame 13) through `t=4.35s` (frame 29),
  excluding the top HUD-text strip (which legitimately changes every tick,
  tick counter and hash), shows **zero changed pixels** across the entire
  2.4-second span -- the figure, the halo, and the run-cycle frame are all
  completely static. This is a strong, not just visual-impression, proof:
  the pre-fix code's `runClock`-driven `runFrameIndex` cycles a full
  `Run0..9` loop every `RunFrameSeconds * 10 = 0.6s`, so across 2.4s of
  wall-clock time with an agent genuinely `isMoving = true` the whole way
  (as the old code would have kept reporting), at least several dozen
  distinct texture swaps would necessarily have shown up as pixel changes;
  observing none over 16 consecutive captures rules out both the
  run-forever bug and the sawtooth recreping-and-snapping bug at once.
  Composite (both phases, labelled): `docs/evidence/task-056-round2-halo-and-stall-freeze.png`.

**Flagged, not resolved, not blocking**: one single frame (frame 10 of 30,
`t=1.5s`, right around the redirect) showed a transient, muted/grey render
of the moving figure instead of its normal blue tint; it self-corrected by
the next captured frame and never recurred through the entire 2.4s frozen
window that follows. Investigated directly rather than waved off: the
`R`/`G`/`B` values feeding that specific `DrawTextureRect` call are a
compile-time constant (`RenderShared.agentColor Friendly`), untouched by
this round's diff; `Diagnostics.agentVitalsOverlays` confirmed
unconditional (one `AgentVitals` entry per agent every tick, never sparse),
ruling out a stale-default vitals misread; `state.Random.Draws` stayed `0`
for the whole probe (`hud10.png`), ruling out an actual, unnoticed combat
hit. No mechanism in this round's own diff can explain a transient colour
change (the halo/figure tie-break sort order is provably stable and
identical-key every frame once both read the same `renderPos`). Left
unresolved and reported honestly rather than guessed at -- possibly an
engine/texture-load timing artifact orthogonal to this fix; does not affect
either of Dave's two reported defects and was not observed to recur.

`dotnet build CommandoWar.slnx -c Release`: `0/0`. `dotnet test`: `343/343`
(unaffected -- no `CommandoWar.Sim`/`CommandoWar.Headless` file touched).
`dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c
Debug`: `0/0`. Godot editor `--selfcheck`, headless, through the real 4.7.2
editor, all three scenes reconfirmed `MATCH`, unchanged from every prior
pin (`SnapshotDemo.tscn` `0xF422ACB8D5A86FF0`, `CommandDemo.tscn`
`0x00D3D471EF7354BC`, `AppraisalDemo.tscn` `0x194805888CBE240D`) -- as
expected, render-interpolation-only, no `Simulation.step` change.
`git status --porcelain`: the temporary `--stall-probe` hook is not present
in the final diff (`grep` for its field/method names after removal, zero
matches).

### Not addressed, flagged for Dave

Same as round 1: whether `DemoScenario`'s underlying grid movement pace
itself (`TrooperMoveSpeed`, TASK-049) still feels right once both rounds'
fixes are in is Dave's own call to make live -- outside this task's
presentation-only scope. Also carried forward: the single unexplained
transient colour frame above.

## Documents updated

- `tasks/TASK-056-AGENT-FACING-CORRECTION-AND-RUN-ANIMATION.md` (created,
  drafted before implementation per `AGENTS.md`'s normal workflow; "Review
  round 1" and "Review round 2" sections added for each round's fixes).
- `docs/11_BACKLOG.md` (B-052 row extended with this third correction
  round; stays `review`, not `done`).
- `docs/12_PROGRESS_LEDGER.md` (this detail file; index row added).
- `docs/evidence/task-056-facing-compass-check.png`,
  `docs/evidence/task-056-run-animation-check.png`,
  `docs/evidence/task-056-round1-facing-on-turn.png`,
  `docs/evidence/task-056-round1-smoothness-sequence.png`,
  `docs/evidence/task-056-round2-halo-and-stall-freeze.png` (new).
- `PROJECT_STATE.yaml` (`active_work` updated).

## Review

- Reviewer: Dave.
- Round 1: facing-on-bend and movement jerkiness/perceived-speed found and
  fixed live (see above); underlying grid movement pace flagged, not
  changed.
- Round 2: live retest read as unchanged; the selection halo's own
  cell-snapping (never covered by round 1's fix) and the run-cycle
  animation playing forever on a stalled agent found and fixed, verified
  by a zero-pixel-diff frozen-state capture over 2.4 real seconds; one
  unexplained single-frame colour transient flagged, not resolved, not
  blocking.
- Accepted: pending -- all fixes across all three rounds need Dave's own
  live confirmation before B-052 can move to `done`.
