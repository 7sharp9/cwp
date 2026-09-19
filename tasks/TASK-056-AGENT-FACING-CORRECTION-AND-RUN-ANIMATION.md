# TASK-056: Correct agent facing rotation and add run-cycle animation

Status: done (accepted by Dave 2026-09-19: "ok that looks vetter" -- the
review round 2 halo/stall-freeze fix confirmed live; realises B-052 across
all three correction rounds this session)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); corrects backlog B-052 again
Size: S-M

## Outcome (2026-09-19)

Implemented as diagnosed. `RenderShared.facingBin` rewritten as a
nearest-reference-vector match (cosine similarity against the 8 primary
world directions' own screen-projected vectors), replacing the previous
`atan2`-plus-uniform-45°-sector-round approach, which had the Kenney
cardinal/diagonal pose assignment backwards (confirmed directly from
`Information.png`'s own diamond-edge legend). Run-cycle animation added:
all 80 `Human_0..7_Run0..9` frames cropped to one shared global bbox
(alongside a re-crop of the existing 8 idle frames to the same rect, so
switching between idle and running never changes the drawn figure's size);
a wall-clock `runClock` (gated to real tick advancement) drives a new
`DrawItem.Cx2` run-frame index.

Verified live through the real Godot 4.7.2 editor (a temporary,
fully-removed command-line probe), not just re-derived on paper: all 8
primary directions' rendered pose compared directly against
`Information.png`'s own reference image and found consistent (the N/W/NW
cluster reads as a back view in both, matching the legend's two "upper"
tile edges; E/S/SE reads as a front view in both, matching the two "lower"
edges); a long obstacle-clear traversal showed two genuinely different
running-frame poses mid-transit and an identical frozen idle pose across
three separate post-arrival captures. `dotnet build` both `.slnx` (main
Release; Godot client Debug) `0/0`; `dotnet test` `343/343` (unaffected);
all three scenes' `--selfcheck` hashes confirmed `MATCH` through the real
Godot editor (unaffected, as expected for a presentation-only change).

Full detail: `docs/ledger/2026-09-19-TASK-056-agent-facing-correction-and-run-animation.md`.

## Review round 1 (2026-09-19, Dave live-tested the corrected facing and new animation)

Dave's report: "its better but i dont think it always stay the correct
facing. also theres some sort of jerkyness to the movement, it also also
moving really fast too." Both findings traced to one shared root cause,
both fixed without any `CommandoWar.Sim`/`CommandoWar.Headless` change.

**Root cause**: `AgentState.Position` only changes once an entire grid
edge completes (`Simulation.navigationAndMovement`'s `Progress` accumulates
for several ticks first, TASK-018) — but the client rendering lerped
screen position between the *previous tick's* and *current tick's* discrete
`Position` alone, which are identical on every mid-edge tick. The figure
therefore sat visually frozen for most of an edge, then snapped across the
whole cell inside a single tick's real-time window (50ms at 20Hz) — jerky,
and (since the entire visual displacement compresses into that one short
window) reading as unnaturally fast. This also explains "doesn't always
stay the correct facing": `facingBin` was fed the raw crow-flies bearing to
the agent's overall final `Destination`, correct on a straight leg (where
the next step and the final bearing coincide) but visibly wrong the moment
a route bends or the player redirects mid-route, since `Destination` is the
agent's overall target, not its next path step.

**Fix**: compute, once per tick, each moving agent's actual next path cell
(`Pathfinding.find` from `Position` toward `Destination` — the
`committedItems` route-preview precedent, cached per tick instead of
recomputed every render frame) — fed into `facingBin` in place of the raw
`Destination`, and used as the lerp *target* in place of `Position` itself.
The lerp *fraction* comes from `AgentSnapshot.Progress` (already canonical
and exact) normalised against a per-agent `edgeTickEstimate`: the exact
tick-count an edge takes depends on `Terrain.moveCost` and the agent's own
`MoveSpeed` (not exposed to the client, and deliberately not added — that
would be a `CommandoWar.Sim` change, forbidden scope), so the threshold is
*learned* from the most recently completed edge instead of replicated
exactly. This self-corrects every edge regardless of a wrong guess, since
`Position` itself always snaps to the true cell the instant an edge
genuinely completes — a deliberate, reasoned client-side smoothing
heuristic, not a hack.

Verified live through the real Godot 4.7.2 editor again (a temporary,
fully-removed burst-screenshot probe, `--motion-probe`): tracked the moving
agent's on-screen centroid across a 24-shot, 0.12s-interval burst and
confirmed continuous, monotonically progressing fractional cell coordinates
(no flat stretches, no large jumps) across a whole multi-tick edge — proof
the interpolation is now smooth, not grid-locked. The same burst drove the
agent south then, partway through, redirected it east (a real mid-route
direction change, the exact case the crow-flies bug could not handle) and
confirmed the rendered facing pose visibly changes to match the new
heading, not stuck on the old one. One test-setup gotcha found and worked
around, not a product bug: the first attempt routed near enough to
`DemoScenario`'s hostile to trigger real combat mid-test (the same mistake
made once already in the main TASK-056 round) — moved the probe's route
well outside `CombatConfig.WeaponRange`.

`dotnet build` both `.slnx` (main Release; Godot client Debug) `0/0`;
`dotnet test` `343/343` (unaffected); all three scenes' `--selfcheck`
hashes reconfirmed `MATCH` through the real Godot editor (unaffected, as
expected — no `Simulation.step` change). Evidence:
`docs/evidence/task-056-round1-facing-on-turn.png`,
`docs/evidence/task-056-round1-smoothness-sequence.png`.

**Not addressed, flagged for Dave**: whether the underlying grid movement
pace itself (`DemoScenario`'s `TrooperMoveSpeed`, half of
`Agent.MoveSpeedDefault`, TASK-049) is still "too fast" once the jerkiness
is gone is Dave's own call to make live — that constant lives in
`CommandoWar.Headless`, outside this task's presentation-only scope, and
changing it is content authoring, not a rendering fix.

Full detail:
`docs/ledger/2026-09-19-TASK-056-agent-facing-correction-and-run-animation.md`
("Review round 1" section).

## Review round 2 (2026-09-19, Dave tried the round-1 fix live again: "seemed the same")

Dave's report: "i just tried again and it seemed the same, did you not fix
this?" Plus two new, specific observations that round 1's own instrumented
probe never looked at: "the issue seems to be the circle thats drawn as
selection indicator, it jumps between cells", and "the animation runs
continously is the agents get stuck on the same cell etc."

**First, ruled out a stale build**: rebuilt both `.slnx` from the exact
round-1-committed source before touching anything further, confirming the
build was clean and current (not the cause).

**Root cause, this round**: round 1's fix was real but incomplete. It made
the agent *figure* itself lerp smoothly using a genuine next-path-cell
target, but two other things were never updated to match:

1. **The selection halo** (`haloItems` in `CommandDemoScene.fs`) still read
   `agentPosition`'s raw, discrete `Cell` directly -- it never adopted
   round 1's `nextStepCell`/`edgeTickEstimate` lerp at all. Since the halo
   is the single largest, most attention-grabbing element while watching a
   selected agent move, a smoothed figure sitting under a still-snapping
   halo reads as "no fix at all" -- exactly what Dave reported.
2. **An agent whose route is stalled by a reservation contest**
   (`Simulation.navigationAndMovement` freezes such an agent's `Progress`
   at `startProgress` rather than incrementing it, while `Destination`
   stays unmet) was never distinguished from a genuinely advancing one.
   `isMoving` was still just `Destination <> Position`, true forever for a
   stalled agent, so the run-cycle animation played indefinitely on a
   figure that was not actually moving at all -- Dave's second report.
   The same gap also meant the per-render-frame `alpha` ramp kept getting
   credited to an edge that would never actually complete, producing a
   small forward-creep-then-snap-back sawtooth on a stalled agent even
   though `Position` never moved -- a second, subtler source of "jumps
   between cells."

**Fix**: a new `stalled` map (recomputed fresh every tick in `stepOnce`/
`advanceOneTick`, comparing an agent's `Progress` entering the tick against
its `Progress` after -- unchanged means the tick made no real progress) and
a new shared `renderPos` helper (`CommandDemoScene.fs`) that both the real
figure and the selection halo now call, so every consumer of "where is this
agent right now" agrees. While `stalled`, `renderPos` stops crediting
`alpha`'s per-frame ramp (freezing at `Progress / estTicks` exactly) and
`isMoving` (hence the run-cycle) goes false. A `Dead`/`Incapacitated`
selection is excluded from the halo's `renderPos` call and pinned to raw
`Position` instead, matching `renderVitals`'s own frozen (never-lerped)
figure for those states. Also fixed a one-tick blind spot in the new
`stalled` check itself: `prevProgress` now seeds from each agent's real
starting `Progress` in `Ready()` (previously empty, defaulting to a `-1`
sentinel that could misread a stall on the very first tick an order is
issued). Applied identically to `DemoRenderScene.fs` (no halo there, but
the same `stalled` freeze, the "one `FSharpSceneHost` draw path, both
scenes behave the same" precedent).

**Verified live** through the real Godot 4.7.2 editor (a temporary,
fully-removed `--stall-probe` burst-screenshot probe -- not a repeat of
round 1's single-agent redirect probe, but a scenario purpose-built to
reproduce a genuine route-reservation stall: agent 0 issued a real
`MoveTo(5,0)` on open terrain, redirected mid-transit into agent 1's own
permanently-occupied cell `(0,1)`, the same contest round 1's own
"Deviations found" section hit by accident): the halo visibly tracks the
running figure through the unblocked leg of the route (composite evidence,
top panel); once genuinely stalled against agent 1's cell, a pixel-diff
across 16 consecutive captures spanning 2.4 real seconds (`t=1.95s` through
`t=4.35s`, excluding the HUD text strip) shows **zero changed pixels** --
proof the figure, the halo, and the run-cycle frame are all completely
frozen, not sawtoothing or still animating (the pre-fix code would
necessarily have shown ~40 distinct run-cycle frame changes and continuous
sub-pixel sawtooth drift across that same span). Composite saved as
`docs/evidence/task-056-round2-halo-and-stall-freeze.png`.

**Flagged, not resolved**: one transient, single-frame (of 30) muted/grey
rendering of the moving figure was observed right around the redirect
tick, self-correcting within 1-2 frames and never recurring through the
entire 2.4s frozen-stall window that follows. The `R`/`G`/`B` fields feeding
that draw call are a compile-time constant (`RenderShared.agentColor`,
untouched by this round's diff) and `AgentVitals` is confirmed
unconditional per agent per tick (`Diagnostics.agentVitalsOverlays`), so a
wound/incapacitation misread was ruled out; `state.Random.Draws` stayed `0`
throughout (no combat occurred). Cause not identified -- likely a one-off
engine/texture-pipeline timing artifact unrelated to the fields this diff
touches, not reproduced elsewhere in the capture. Recorded for awareness,
not blocking: it does not affect either of Dave's two reported defects, and
this task's own diff cannot explain it.

`dotnet build` both `.slnx` (main Release; Godot client Debug) `0/0`;
`dotnet test` `343/343` (unaffected -- no `CommandoWar.Sim`/
`CommandoWar.Headless` file touched). All three scenes' `--selfcheck`
hashes reconfirmed `MATCH` through the real Godot 4.7.2 editor, unchanged
from every prior pin. `git status --porcelain`: the temporary
`--stall-probe` hook is not present in the final diff.

Full detail:
`docs/ledger/2026-09-19-TASK-056-agent-facing-correction-and-run-animation.md`
("Review round 2" section).

## Objective

Dave live-tested TASK-054's B-052 facing feature (already corrected once this
session, from a mis-binned world angle and from a wrong art-source
assumption) and found two more problems, both presentation-only
(`src/CommandoWar.Client.Godot` only, ADR-0002: no `CommandoWar.Sim`/
`CommandoWar.Headless` change):

1. **Facing is still wrong, roughly 30 degrees off.** The current
   `RenderShared.facingBin` was derived and checked only analytically
   (`dotnet fsi` pure-function probes) and against static composited
   comparison images (`PIL`), never against the real running game. This task
   must confirm the fix against an actual Godot screenshot/view, not just
   re-derive the math again on paper.
2. **No animation while running.** A moving agent freezes on a single static
   idle-rotation frame (`agent_human_facing0..7.png`) for its whole
   transit. Dave wants it to actually animate through Kenney's own
   `Run0..9` running cycle (10 frames per direction, already in the
   downloaded pack) while moving, freezing back to that direction's `Idle0`
   pose the instant it stops.

B-052 stays at `review` (not `done`) until Dave confirms both fixes live.

## Diagnosis (before implementing)

### Facing rotation bug

Re-examined the pack's own `Information.png` (`C:\Users\Dave\Downloads\
kenney_isometric-miniature-prototype\Information.png`) directly, alongside
this project's own `CellToScreen` (`FSharpSceneHost.cs`:
`screenX = (cx-cy)*TileW/2`, `screenY = (cx+cy)*TileH/2`).
`Information.png`'s diamond legend labels the terrain tile's own FOUR EDGES
`N` (upper-right edge), `E` (lower-right edge), `S` (lower-left edge), `W`
(upper-left edge) -- not the diamond's vertices. The displacement from a
tile's centre to its `N`-neighbour's centre is exactly twice the
centre-to-edge-midpoint vector, i.e. a `(half-width, -half-height)` screen
step -- which is exactly what a **world pure-axis** move produces under
`CellToScreen` (e.g. `dx=0,dy=-1`), not a world-diagonal move.

The existing `facingBin` has this backwards: it assigns the four Kenney
*cardinal* poses (`0=N,2=E,4=S,6=W`) to the four **world-diagonal** moves
(confirmed directly from its own doc comment: `world NW -> bin 0 (N)`,
`world NE -> bin 2 (E)`, etc.), and the four *in-between* poses (`1,3,5,7`)
to the four world pure-axis moves -- exactly backwards from the geometry
above. Working out the true (non-uniform, because the iso projection is
anisotropic 2:1, not 1:1) target angle for each of the 8 world unit
directions under the existing `atan2`-based formula shows every one of them
lands exactly one 45 degree bin past its correct target (a uniform
`+1`-bin error) -- consistent with Dave's "roughly 30 degrees off, most
likely shape is a constant rotation error" framing, and explains why the
error reads as "close but off" rather than random.

A uniform `+1`-bin shift is not the chosen fix, though, because the 8
target angles are **not** evenly 45-degrees apart under this anisotropic
projection (they cluster at alternating ~26.6/~63.4-degree gaps) -- a
constant shift only happens to repair the 8 exact primary-direction test
vectors, not a general `(dx,dy)` heading (which `facingBin` must handle:
`AgentSnapshot.Destination` is the agent's overall target cell, not its next
path step, so the delta fed into `facingBin` is frequently not a primary
direction at all, e.g. a `MoveTo(10,6)` from partway along the route).

Chosen fix: replace the `atan2`-plus-uniform-bin-round approach with a
direct **nearest-reference-vector match**. Project the movement delta
through the identical iso skew, then compare it (cosine similarity) against
the 8 *actual* screen vectors that each of the 8 primary world directions
themselves project to (computed through the same skew) -- i.e. pick
whichever of the 8 rendered poses' own real movement direction is closest,
in true screen angle, to the current movement's real screen angle. This is
correct by construction for the 8 primary directions (each is trivially
its own best match, cosine = 1) and handles an arbitrary `(dx,dy)` heading
correctly too, unlike a constant-offset patch.

**Still requires live confirmation** (the whole reason this task exists):
verified with a temporary Godot screenshot probe, see Required work.

### Run animation

Kenney's pack ships a genuine 10-frame `Run0..Run9` animation per direction
(`Characters/Human/Human_N_RunF.png`, `N=0..7`, `F=0..9` -- 80 files),
already downloaded. Checked with a `PIL` bounding-box scan across all 8
directions' `Idle0` + all 80 `Run` frames: a per-direction shared bbox
varies in size (largest around 124x145px, since a running gait's own
silhouette extent varies more than a static idle pose's does), and the
existing idle crop rect (`(99,323,58,138)`, TASK-054's own crop) is smaller
than any of the run frames' bounds. A **single global shared crop rect**
across all 88 frames (8 idle + 80 run), `(66,309)-(190,466)` (124x157px),
keeps every texture at the *identical* pixel size and aspect ratio, so
switching between the frozen idle pose and any running frame never changes
the agent figure's on-screen size (`DrawAgentFigure` derives height from
each texture's own aspect ratio at a fixed on-screen width, so mismatched
source aspect ratios would otherwise pop the figure's size the instant it
starts/stops moving). This means the existing 8
`agent_human_facing0..7.png` files are re-cropped to this new rect too (same
paths, same source `Idle0` images, just a larger, shared bounding box).

## Central decisions

Both fixes were specified in enough detail by Dave's own instruction this
session that no `AskUserQuestion` round was needed before drafting:

1. Verify the facing fix against a real Godot view/screenshot, not another
   paper/`PIL` derivation -- explicit instruction.
2. Animate the full `Run0..9` cycle by elapsed time while
   `Destination <> Some Position` (renamed here "is moving"), freeze to that
   direction's `Idle0` the instant it stops (arrival or no destination) --
   explicit instruction. The existing freeze-on-arrival/incapacitation
   behaviour (TASK-054) stays; only what it freezes back to needed to become
   explicit (idle pose, never a mid-stride running frame).
3. Run-cycle timing: a presentation-only judgement call, not gameplay-
   affecting -- `RunFrameSeconds = 0.06` (a full 10-frame loop in 0.6s,
   ~16.7 fps), a plausible sprite run-cycle pace given how briefly
   `DemoScenario`/scripted routes are typically in motion.
4. A single global shared crop rect across all 88 source frames (idle +
   run, all directions), not a per-direction rect -- avoids any figure-size
   pop switching between idle and running (see Diagnosis above); a
   deliberate, reasoned choice, not the default "smallest possible crop"
   the TASK-054 precedent used, because that precedent never had to keep
   two different pose sets visually interchangeable.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`.
- `tasks/TASK-054-AGENT-FACING-AND-AUDIO-THREAT-INDICATOR.md` and its ledger
  detail (the feature and both prior correction rounds this task follows).
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs` (`facingBin`,
  `IsoAspect`).
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` (`renderVitals`,
  `stepOnce`'s facing-map update).
- `src/CommandoWar.Client.Godot/Core/DemoRenderScene.fs` (`advanceOneTick`,
  `agentItems`).
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs` (`DrawAgentFigure`,
  `AgentFacingTextures`, the `_Draw` switch, `ParseCommandLine`).
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs` (`DrawItem`'s
  `Kind = 1`/`Cx2` semantics).
- `C:\Users\Dave\Downloads\kenney_isometric-miniature-prototype\
  Information.png` (the pack's own compass legend) and `Characters/Human/`.

## Dependencies

- TASK-054 (this session, uncommitted) -- B-052/B-056, the facing feature
  and art this task corrects.

## Allowed scope

- `src/CommandoWar.Client.Godot/art/`: `agent_human_facing0..7.png`
  re-cropped (same paths); 80 new `agent_human_run{0-7}_{0-9}.png` files;
  `LICENSE-THIRD-PARTY.md` updated.
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`: `facingBin`
  rewritten; a new pure run-frame-index helper.
- `src/CommandoWar.Client.Godot/Core/DemoRenderScene.fs`,
  `CommandDemoScene.fs`: a run-cycle clock and the "is moving" -> frame
  index plumbing into `DrawItem.Cx2`.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: idle/run texture
  arrays, `DrawAgentFigure` frame selection; a temporary, removed-before-
  finishing command-line probe hook for live screenshot verification (the
  `dotnet fsi` scratch-probe precedent, applied to a real Godot view since
  that is what this task requires).
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`: doc-comment update
  for `Cx2`'s new `Kind = 1` meaning.
- `docs/11_BACKLOG.md` (B-052 row stays `review`, note added),
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`,
  `tasks/TASK-054-...md` (cross-reference only, no `Outcome` rewrite).

## Forbidden scope

- Any `CommandoWar.Sim`/`CommandoWar.Headless` change.
- B-056 (the audio cue) -- untouched, not implicated by either defect.
- Threat-facing, a new art pack/style, a new `DrawItem.Kind` -- all still
  out of scope per TASK-054's own Forbidden scope, unchanged here.
- Marking B-052 `done` -- stays `review` until Dave confirms live.

## Required work

1. Re-crop `Human_0..7_Idle0.png` and all 80 `Human_0..7_Run0..9.png` frames
   to the shared global bbox `(66,309)-(190,466)` via `PIL`; write
   `agent_human_facing0..7.png` (overwrite in place) and 80 new
   `agent_human_run{dir}_{frame}.png` files; update
   `LICENSE-THIRD-PARTY.md`.
2. Rewrite `RenderShared.facingBin` to the nearest-reference-vector match
   described in Diagnosis; add a pure `runFrameIndex` (or equivalent) helper
   plus a `RunFrameSeconds` constant.
3. `FSharpSceneHost.cs`: rename `AgentFacingTextures` to
   `AgentIdleTextures`; add `AgentRunTextures` (80 entries, `dir*10+frame`);
   `DrawAgentFigure` takes a run-frame index (`< 0` = idle) and selects the
   matching texture; both texture sets share the same crop rect so no
   aspect-ratio jump is possible when switching between them.
4. `DemoRenderScene`/`CommandDemoScene`: a `runClock` accumulator, advanced
   by real elapsed time while ticks are actually advancing (gated the same
   way as `CommandDemoScene`'s own `paused` gate, so the run cycle never
   animates while the sim itself is frozen); `agentItems`/`renderVitals`'s
   `Alive` branch compute `isMoving` from `Destination <> Some Position` and
   populate `DrawItem.Cx2` with the run-frame index while moving, `-1`
   otherwise; `Incapacitated` always passes `-1` (frozen idle, regardless of
   any stale `Destination`).
5. Add a temporary, clearly-marked command-line probe to
   `FSharpSceneHost.cs` (removed before this task finishes) to drive
   `CommandDemoScene` through a scripted move in each of the 8 primary
   world directions from an open interior cell of `DemoScenario`'s terrain,
   screenshotting each. Launch the real Godot 4.7.2 editor (windowed, not
   `--headless` -- screenshot capture needs a real framebuffer) for each of
   the 8 runs; inspect each image directly (`Read`) against
   `Information.png`'s own compass, adjust `facingBin`/the crop/the texture
   index if any direction reads wrong, and re-run until confirmed.
6. Same probe (or a small variant), or direct visual inspection of a
   windowed run: confirm the run-cycle animation visibly advances through
   multiple distinct frames while an agent is mid-route, and freezes back to
   the plain standing idle pose the instant it arrives.
7. Remove the temporary probe hook entirely once both are confirmed.
8. Full verification per Required verification below.
9. Update backlog/ledger/state; do not mark B-052 `done`.

## Acceptance criteria

- [x] Each of the 8 primary world movement directions renders the Kenney
      rotation pose that visually matches that direction on screen,
      confirmed against a real Godot screenshot compared to
      `Information.png`'s own compass -- not just self-consistent pure-
      function output.
- [x] A moving agent (`Destination <> Some Position`) visibly cycles through
      multiple distinct running-animation frames over time, not a single
      static pose.
- [x] The instant an agent stops (arrival, order cleared, or becomes
      `Incapacitated`), it freezes on that direction's plain standing idle
      pose -- never a mid-stride running frame.
- [x] Switching between the idle pose and any running frame produces no
      visible jump in the agent figure's on-screen size (shared crop rect).
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless` change; all three scenes'
      `--selfcheck` hashes unaffected.
- [x] `dotnet build`/`dotnet test` unaffected; Godot client builds.
- [x] The temporary screenshot-probe hook is fully removed from the final
      diff.
- [x] Required documentation updated; B-052 stays `review`.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet test`: unaffected count.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: `0/0`.
- Real Godot 4.7.2 editor, windowed, screenshot evidence for all 8 primary
  directions, inspected directly against `Information.png`.
- Real Godot 4.7.2 editor, windowed, evidence the run cycle animates and
  freezes correctly.
- Godot editor `--selfcheck` for `SnapshotDemo.tscn`/`CommandDemo.tscn`/
  `AppraisalDemo.tscn` through the real editor: all three `MATCH`, hashes
  unaffected.
- `git status --porcelain`: matches this task's allowed scope (no temporary
  probe code left behind).

## Evidence to capture

- Per-direction screenshots (or a composite) proving the corrected facing,
  compared against `Information.png`.
- Evidence (screenshots and/or a short description of what was observed
  live) that the run cycle animates and freezes correctly.
- `--selfcheck` hashes for all three scenes, confirmed unchanged.

## Expected files

- `src/CommandoWar.Client.Godot/art/agent_human_facing0..7.png` (re-cropped
  in place), `agent_human_run0_0.png` .. `run7_9.png` (new, 80 files),
  `LICENSE-THIRD-PARTY.md`.
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`, `DemoRenderScene.fs`,
  `CommandDemoScene.fs`, `IClientScene.fs`.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`.
- `tasks/TASK-056-AGENT-FACING-CORRECTION-AND-RUN-ANIMATION.md`.
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md` (+ new
  `docs/ledger/` detail file), `PROJECT_STATE.yaml`.

## Documentation updates

- This task file's Outcome section.
- `docs/11_BACKLOG.md` (B-052 row: stays `review`, note this second
  correction round).
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Fully additive/presentation-only: no `CommandoWar.Sim`/`CommandoWar.Headless`
change, no hash format change. Revertible with `git revert` in one step;
both scenes' `--selfcheck` hashes are expected unaffected either way.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave.
- Round 1: facing-on-bend and movement jerkiness/perceived-speed found and
  fixed live; underlying grid movement pace flagged, not changed.
- Round 2: selection-halo cell-snapping and run-animation-plays-forever-
  while-stalled found and fixed; verified with a zero-pixel-diff frozen-state
  capture; one unexplained single-frame colour transient flagged, not
  blocking.
- Accepted: yes, 2026-09-19 ("ok that looks vetter"). B-052 now `done`.
