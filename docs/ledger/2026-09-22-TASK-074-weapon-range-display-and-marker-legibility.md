## 2026-09-22 - TASK-074 - Weapon/engagement-range display and HUD marker legibility

**Owner:** Dave
**Source revision:** working tree on top of `main` at `e22a20c` (TASK-072/073,
implemented and accepted)
**Environment:** Windows x64, .NET SDK, Godot `4.7.2-stable_mono_win64` --
a real GPU-backed windowed session was available and used directly for the
required live screenshot (no synthetic-render substitute needed, unlike
TASK-073's own environment).
**Status change:** `ready -> review` (self-verified)

### Changes

Realises B-074 (B-070's original "show firearm ranges on the map" ask) --
render-only, two files:

1. **`RenderShared.rangeSquare`** (`RenderShared.fs`): a new helper building
   the four `Kind = 2` `lineMarker` segments outlining the true
   Chebyshev-range square around a centre cell -- corners at
   `(cx-R,cy-R)`, `(cx+R,cy-R)`, `(cx+R,cy+R)`, `(cx-R,cy+R)` joined edge to
   edge, **not** the smaller diamond joining only the four edge-midpoints
   (`(cx±R,cy)`/`(cx,cy±R)`), per the task's own Central decision 1.
2. **Two call sites in `CommandDemoScene.DrawList`**:
   - Friendly: exactly one selected agent (`Set.count selected = 1`, the
     existing `HudText` single-selection-detail precedent), drawn at the
     agent's raw `Position` at `CombatConfig.WeaponRange` (7).
   - Hostile: every entry in the existing `hostileKnownContacts` fog-of-war
     map (both a currently-visible contact and a stale last-known one),
     drawn at each entry's own cell, same radius -- an un-contacted hostile
     is absent from that map, so it gets no indicator (no new intel leak).
   Both feed into the existing "two-cell item, no single meaningful depth"
   unsorted-append group (`fireEffects`/`audioCueItems`/`holdOutlineItems`
   precedent), not the depth-sorted `sorted` array.
3. Colour: `(0.75, 0.45, 0.15)` (desaturated amber), alpha `0.45`, width
   `2.0px` -- distinct from every existing marker hue on this screen (see
   Evidence/Deviations below for why the first-pass `0.3`/`1.5px` choice was
   revised after an actual screenshot check).

Pure distance envelope, deliberately not LOS-aware (documented in both the
`rangeSquare` doc comment and the call-site comment) -- no
`CommandoWar.Sim`/`CommandoWar.Headless` change, no new `DrawItem.Kind`, no
toggle key, matching every item in the task's Forbidden scope.

### Geometry verification (Required work item 1)

Computed by hand against the real `CellToScreen` formula
(`FSharpSceneHost.cs`: `Origin (552,110)`, `TileW 88`, `TileH 44`) before
implementing, for a representative centre `(10,10)` and `R = 7`:

- True corners project to `(552,242)`, `(1168,550)`, `(552,858)`,
  `(-64,550)` -- i.e. exactly the four screen-cardinal extreme points
  (top/right/bottom/left) of a diamond `616px` wide and `308px` tall
  centred on the agent, giving each of the diamond's four edges a constant
  screen-space slope of exactly `±0.5` (`TileH/TileW`).
- The rejected edge-midpoint diamond's four points project to `(860,704)`,
  `(244,704)`, `(244,396)`, `(860,396)` -- an axis-aligned, **upright**
  square (horizontal/vertical edges, not `±0.5`-slope diagonals) at roughly
  half the true extent (`308px`/`154px` half-widths).

This is a materially different on-screen shape, not just a smaller version
of the same one -- confirmed both by this hand calculation and, after
implementing, by direct pixel inspection of the live screenshot (see
Evidence): every amber pixel sampled from the rendered lines has screen
slope `±0.5`, matching the true-corner prediction, not `0`/`∞` (which the
rejected diamond would have produced).

### Verification

- `dotnet build CommandoWar.slnx -c Debug`: `0 Warning(s)`, `0 Error(s)`.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: `0 Warning(s)`, `0 Error(s)` (after a one-time
  `"$GODOT" --editor --headless --quit --path src/CommandoWar.Client.Godot`
  asset import -- this worktree had no `.godot` cache yet).
- `dotnet test CommandoWar.slnx -c Debug`: `422/422` passed (unchanged from
  the task's stated baseline; this task touches no `CommandoWar.Sim` code,
  so no new test was expected or added).
- `dotnet run --project src/CommandoWar.Headless -- corpus`: `OK - all 20
  entries match their committed tables` (unchanged from the task's stated
  baseline).
- `"$GODOT" --headless --path src/CommandoWar.Client.Godot
  scenes/CommandDemo.tscn -- --selfcheck`: `MATCH expected final hash
  0x84A25E3559111E9B at tick 90` -- unchanged, confirmed by actually
  running it both before and after the colour/alpha revision below.
- `"$GODOT" --headless --path src/CommandoWar.Client.Godot
  scenes/SnapshotDemo.tscn -- --selfcheck`: `MATCH expected final hash
  0x6213D672BC36FDB8 at tick 20` -- unchanged.
- `"$GODOT" --headless --path src/CommandoWar.Client.Godot
  scenes/AppraisalDemo.tscn -- --selfcheck`: `MATCH expected hash
  0xA1354EB998FC1B95, agent 0 Refused / agent 1 Accepted` -- unchanged.
- `git status`/`git diff --stat`: `RenderShared.fs`/`CommandDemoScene.fs`
  modified, `docs/evidence/task-074-weapon-range-display.png` and this
  task's own files new -- matches Allowed scope exactly; no
  `CommandoWar.Sim`/`CommandoWar.Headless` file touched.
- Live windowed screenshot: `"$GODOT" --path src/CommandoWar.Client.Godot
  scenes/CommandDemo.tscn -- --screenshot-squad 300
  docs/evidence/task-074-weapon-range-display.png` -- a real GPU-backed
  windowed session (confirmed available this session), not a synthetic
  substitute.

### Evidence

- `docs/evidence/task-074-weapon-range-display.png`: tick 25 of the
  `--screenshot-squad` scripted sequence, agent 5 selected (one friendly),
  hostile rifleman `agent 102` a known (stale) contact showing its
  fog-of-war ring near the depot. Two amber `±0.5`-slope diagonal line
  segments are visible crossing the frame (top-right and toward the bottom
  edge) -- the friendly's and hostile's range-square edges, both centred
  close together so their large diamonds mostly overlap on screen; neither
  diamond's own far corner fits inside the `1280x800` viewport at
  `WeaponRange = 7` and this map's camera scale (each diamond spans
  `1232x616px`, larger than the viewport in one axis) -- see Deviations.
  Pixel-sampled from the raw (unedited) PNG: every amber-hued pixel found
  lies on a line of slope `±0.5`, confirming the rendered shape is the true
  corner-to-corner square, not the rejected edge-midpoint diamond (which
  would render as horizontal/vertical lines instead).
- Colour/alpha choice: first implemented at `alpha 0.3`/`width 1.5px` per
  Central decision 3's "low-alpha, thin" wording; confirmed by a real
  screenshot to be present (pixel-sampled) but too faint to read by eye
  against Bridgehead's busy terrain without artificially boosting contrast.
  Revised to `alpha 0.45`/`width 2.0px` and re-screenshotted (this task's
  own "verify with a real screenshot before treating this as done"
  instruction, Central decision 1) -- legible without post-processing in
  the evidence PNG, still visibly thinner/dimmer than every other marker on
  screen (`objective`/`extraction` rings at alpha `0.85`, `hover` at `0.8`,
  `holdOutlineItems`/pending-route golds at `0.5-0.7`).

### Deviations and unresolved issues

- **The full diamond does not fit inside one screenshot frame at
  `WeaponRange = 7`.** Not assumed -- found directly from the hand
  calculation above and confirmed on screen: each range indicator's own
  extent (`1232x616px`) exceeds the `1280x800` viewport's height and
  leaves only `~24px` margin on width if centred, so any agent not sitting
  very near the exact screen centre has its indicator's corner(s) fall off
  one or more edges. The evidence screenshot shows two visible edges (a
  partial "corner" shape), not a fully closed diamond. This is a real,
  reportable finding, not a bug in this task's own geometry (Central
  decision 1's math is confirmed correct both by hand and by the on-screen
  slope check above) -- it is a consequence of `WeaponRange`'s own value
  relative to this project's fixed camera scale (`Origin`/`TileW`/`TileH`
  in `FSharpSceneHost.cs`, outside this task's Allowed scope). No camera
  zoom/pan exists in this client to work around it, and adding one would be
  a materially larger, out-of-scope change. Recorded here rather than
  silently worked around (e.g. by shrinking the drawn radius below the real
  `WeaponRange`, which would misrepresent the actual envelope) -- whoever
  scopes a future camera/zoom task should know this indicator's own extent
  is one more reason a wider view would help.
- **Colour/alpha revised mid-task from the Central-decision-3 first guess**
  (`0.3`/`1.5px` -> `0.45`/`2.0px`), exactly as that decision's own
  "verify... not by this reasoning alone" instruction anticipates -- see
  Evidence above for the before/after screenshot comparison that drove it.
- This worktree's own branch was found 12 commits behind local `main`
  (stuck at TASK-060) before any of the above work started --
  fast-forwarded (`git merge --ff-only main`, clean tree first, ancestry
  verified) as a safe, non-destructive catch-up, per this task's own
  dispatch instructions. Not a finding about this task's own content.
- This worktree also had no `.godot` import cache yet (a fresh worktree, no
  prior Godot editor run) -- resolved with the one-time
  `--editor --headless --quit` pass noted under Verification above, the
  same gap a prior session's own note anticipated.

### Documents updated

- `tasks/TASK-074-WEAPON-RANGE-DISPLAY-AND-MARKER-LEGIBILITY.md` (status,
  acceptance criteria, required verification, evidence, review).
- This ledger detail file.
- **Deliberately not edited in this pass** (this dispatch's own scope
  guardrails, per the orchestrating session's explicit instruction, since
  TASK-075 and TASK-076 are running in parallel worktrees over the same
  shared files at the same time): `docs/11_BACKLOG.md`,
  `docs/12_PROGRESS_LEDGER.md`'s own index table/Pinned facts,
  `PROJECT_STATE.yaml`. The orchestrating session reconciles these
  centrally once all three parallel tasks report back.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-22, "accept all three, commit"), after independent
  re-verification by the orchestrating session (rebuild, `dotnet test`
  422/422, `-- corpus` 20/20, all three Godot `--selfcheck` hashes unchanged
  with TASK-075/TASK-076's own changes combined in the same tree).
