# TASK-054: Agent sprite facing/orientation and audio-localised threat indicator

Owner: Dave (implementing agent session)
Source revision: `main`, after TASK-053's acceptance.
Environment: `dotnet` `10.0.303`, Godot `4.7.2.stable.mono` (real editor),
Windows 11.

## Selection

No task was selected; TASK-053 had just been accepted. Two unblocked
backlog paths were open (B-016b communication mechanics, a chunky sim-only
feature needing real design work before drafting; or B-052+B-056 bundled,
two small client-polish rows each with an explicit Dave-flagged "do not
guess past this" design fork). Put to Dave via `AskUserQuestion`: he chose
the B-052/B-056 bundle.

## Central decisions

Four `AskUserQuestion` rounds before drafting, plus a fifth after the
first round's answer reopened B-052's own scope:

1. **B-052 facing source**: movement direction only (Dave's choice, the
   recommended default) over movement-plus-threat-facing.
2. **B-052 art, round 1**: live-checked kenney.nl (downloaded and unzipped
   "Isometric Miniature Prototype", `unzip -l` on the actual archive, not
   assumed from memory) — confirmed the pack's only rotation set (10
   angles) lives in the `Run` animation, `Idle0` has none. Asked whether a
   stationary agent should freeze on the nearest `Run` frame (Dave's
   answer: "perhaps we need a better asset as we are compromising" — not
   one of the two offered options, a request to look further rather than
   accept the compromise yet).
3. **B-052 art, round 2 (further research)**: downloaded and inspected
   "Isometric Miniature Dungeon" too (same `Male_0..7`/`Idle0`/`Run0-9`/
   `Pickup0-9` layout as `Prototype`'s `Human_0..7` — confirmed a
   series-wide rig, not a one-pack gap). `WebSearch` for a free CC0 pack
   combining this project's rendered art style with a genuine multi-angle
   *idle* found itch.io packs with real 8-directional idle+walk cycles
   (Hormelz, CC0), but every one found is pixel art — a visible style
   clash against the existing Kenney terrain/prop art. Put back to Dave
   with this finding: stay in-style and accept the `Run`-frame freeze, or
   switch style (a bigger, unscoped art-direction change), or drop B-052
   this task. Dave chose staying in-style.
4. **B-056 trigger**: `FireLine` (`ShotFired`), only when the shooter is
   `Hostile` and has no `KnownContact` entry at all for the friendly side
   (Dave's choice, the recommended default) — matches "audio-localised
   even without line of sight," never redundant with the existing
   fire-line/fog-of-war feedback for an already-visible-or-tracked
   shooter.
5. **B-056 visual**: a screen-edge-ish directional cue, bearing only, no
   distance/exact position, reusing `Kind = 2`/`3` primitives (Dave's
   choice, the recommended default) over a per-agent compass ring.

Texture unification across `DemoRenderScene`/`CommandDemoScene` (both draw
`Kind = 1` agents through the one shared `FSharpSceneHost` host) was a
judgement call, not put to `AskUserQuestion` separately — maintaining two
parallel agent-texture regimes for the same draw primitive would itself be
an inconsistency, not a genuine design fork.

## Investigation before drafting

- Confirmed `Simulation.step`'s phase order (`commandIntake ->
  communication -> perception -> appraisal -> commitmentAndLocalAction ->
  combat -> stateConsequences`) — Perception runs strictly before Combat
  every tick.
- Confirmed `CombatConfig.WeaponRange` (7) sits strictly inside
  `PerceptionConfig.SightRange` (10), per `Combat.fs`'s own comment ("so a
  target can be seen before it can be shot"). Combined with the phase
  order above, this means a hostile within weapon range of a friendly is
  essentially always already within that friendly's own sight range too,
  so the friendly squad's shared `TacticalKnowledge` (TASK-026) very
  commonly registers a new contact the *same* tick the hostile first
  fires — this is what made the pre-tick-vs-post-tick distinction below
  load-bearing rather than a rare edge case.
- Confirmed `DiagnosticFrame.Overlays`' `FireLine of shooter: AgentId *
  from: Cell * target: AgentId * at: Cell * hit: bool` and `KnownContact
  of cell * contact: AgentId * confidence: int * lastSeenTick: int64`
  (`Diagnostics.fs`) already carry everything needed — no
  `CommandoWar.Sim` change required for either B-052 or B-056.
- Confirmed `AgentSnapshot.Destination: Cell option` (already read by
  `committedItems`) is sufficient to derive a facing heading directly,
  with no need for the `prevAgents`-cell-delta tracking `DemoRenderScene`
  already had for interpolation.
- Extracted the shared alpha-channel bounding box across all ten
  `Human_0_RunN.png` frames (`PIL.Image.getbbox()` on each, unioned):
  `(80, 314, 87, 144)` — a per-frame crop would have misaligned the
  figure's anchor point frame to frame, since a running stride's own
  silhouette bounds shift with the pose (confirmed by inspecting each
  frame's own bbox: `x` ranged `80..106`, `y` ranged `314..331`, bottom
  edge `436..458`).
- Visually inspected a comparison strip of all ten cropped `Run` frames
  (`PIL`, composited side by side) to confirm they are genuinely ten
  rotations of one running pose (not ten unrelated animation frames) —
  right-facing, through front, through back, through left, back to right
  — before committing to the 10-bin, `36°`-apart facing scheme.

## Changes

- `src/CommandoWar.Client.Godot/art/`: `agent_human_facing0.png` ..
  `facing9.png` added (each `Human_0_RunN.png` cropped to the shared rect
  above, `87x144`); `agent_human.png` (`Idle0`) and its `.import` removed.
  `LICENSE-THIRD-PARTY.md` updated with the new table and the research
  trail above.
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`: new pure
  `facingBin (prevBin: int) (position: Cell) (destination: Cell option) :
  int` — bins the world-grid heading into one of 10 `36°` sectors when an
  active `Destination` differs from `Position`; otherwise returns
  `prevBin` unchanged. Documents the bin-0-is-`+X` assumption as cosmetic
  and unverifiable against real 3D camera metadata.
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`: `DrawItem`'s
  `Kind = 1` doc comment extended for `TextureId`'s new facing-bin meaning
  on a full-opacity item.
- `src/CommandoWar.Client.Godot/Core/DemoRenderScene.fs`: new `mutable
  facing: Map<int,int>`, updated once per `advanceOneTick` (not per render
  frame); `agentItems`' `TextureId` now reads it instead of the constant
  `0`.
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`:
  - Same `facing` map, updated once per `stepOnce`; `renderVitals`'s
    `Alive`/`Incapacitated` figures both read it (a downed agent keeps its
    last active-movement facing rather than resetting).
  - New `squadCentroid ()` (mean cell of every currently `Alive` friendly)
    and `audioCueAnchor` (an 8-sector, `45°`-wide boundary-point lookup
    around `state.Bounds`) helpers.
  - `stepOnce` now captures `hostileKnownContactIdsBefore` from `devFrame`
    **before** calling `Simulation.step` (see Deviation below), then, for
    each `FireLine` overlay in the *new* `devFrame`, adds a `heldAudioCues`
    entry (anchor + a short inward-pointing tip toward the squad centroid)
    when the shooter is `Hostile` and was not in that pre-tick set.
  - `Update` decays `heldAudioCues` by wall-clock time (the
    `heldFireLines` precedent).
  - `DrawList` renders each held cue as one fractional-coordinate `Kind =
    2` line plus one `Kind = 3` `"!"` label (built as literal `DrawItem`
    records, not through `RenderShared.cellLabel`/`lineMarker`, since both
    of those are `Cell`-typed/integer-only and the cue's anchor points are
    off-grid floats), always appended alongside `fireEffects`/`devItems`
    (unconditional on `devOverlay`).
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: `AgentTexture` (a
  single `Texture2D`) replaced with `AgentFacingTextures: Texture2D[10]`;
  `AgentSourceRect`/`DrawTextureRectRegion` removed (the new art is
  already tightly cropped to its own bounds — no runtime crop needed,
  `DrawTerrainTile`'s own pattern); `DrawAgentFigure` takes a
  `facingIndex` parameter, clamped, selecting the texture and sizing from
  its own aspect ratio.

No `CommandoWar.Sim`/`CommandoWar.Headless` file touched.

## Deviation found during implementation

The first draft checked `hostileKnownContactIds` from the *post*-tick
`devFrame` (built after `Simulation.step` returns, inside the same
`for overlay in devFrame.Overlays` loop that already scans for
`FireLine`). Because Perception runs before Combat in `Simulation.step`'s
phase order, and `CombatConfig.WeaponRange` sits inside
`PerceptionConfig.SightRange` (see Investigation above), this check would
have been true (already known) on almost every tick a hostile actually
opens fire — since that same tick's own Perception phase, running first,
had already registered the contact. That would have suppressed the audio
cue for precisely the "just came into view and fired" case B-056 exists
to cover, leaving only an already-redundant case (a hostile the squad knew
about several ticks earlier, for some reason not yet firing, that then
fires) reachable in practice.

Fixed by capturing the known-contact set from the *prior* tick's
`devFrame` — read before `Simulation.step` is called, not after — and
checking a firing shooter against that snapshot instead. This makes the
check a genuine "was this a surprise" transition test rather than a static
"is this currently a stranger" one, and is what the scratch-probe evidence
below actually exercises (the cue fires on the exact tick contact and
first fire coincide).

## Verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet test`: `342/342` (unaffected).
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: `0/0`.
- `cwheadless corpus`: `16/16` (unaffected).
- A temporary `dotnet fsi` scratch probe (`facing_pure.fsx`, session
  scratchpad, removed after use) called `RenderShared.facingBin` directly:
  `East (+X) -> bin 0`, `South (+Y) -> bin 2`, `West (-X) -> bin 5`,
  `North (-Y) -> bin 8`, `SE diag -> bin 1`, `NW diag -> bin 6` (the
  non-round numbers for the cardinal directions are `System.Math.Round`'s
  default banker's-rounding at exact `.5`-sector boundaries — `90° /
  36° = 2.5 -> 2`, `270° / 36° = 7.5 -> 8` — a cosmetic artefact, not a
  bug, since 10 bins don't divide evenly into 8 primary directions); "no
  destination" and "already arrived" both correctly returned the supplied
  `prevBin` unchanged.
- A second temporary probe (`probe_task054.fsx`, removed after use) drove
  `CommandDemoScene` live: selecting friendly agent 0 at `(0,0)` and
  issuing `MoveTo(3,0)` (pure `+X`) showed the emitted `TextureId` at `0`
  for every tick of the approach and after arrival, self-consistent with
  the `East -> bin 0` pure-function result above.
- A third probe drove friendly agent 0 from `(0,0)` toward `(9,6)` — the
  TASK-053 precedent, within `CombatConfig.WeaponRange` and clear line of
  sight of the hostile at `(11,7)` in `DemoScenario`. `state.Random.Draws`
  (from `HudText()`) confirmed real, unscripted combat: `0` through tick
  20, then `2, 4, 6, 8` on ticks 21-24 (two draws per tick, symmetric
  engagement), then flat at `8` through tick 30 (engagement lapsed).
  Exactly one `Kind = 3`, `Text = "!"` audio-cue item appeared, first on
  tick 21 — the same tick the first shot landed — and the count stayed at
  exactly `1` through tick 30, proving the cue was not re-added on any of
  the three further ticks (22-24) the still-firing, now-known hostile kept
  shooting, and that it did not fire for the friendly's own return shots
  (filtered by `Side = Hostile`). No cue appeared on ticks 1-20 (out of
  range).
- Godot editor `--selfcheck`, headless, through the real 4.7.2 editor, all
  three scenes:
  - `SnapshotDemo.tscn`: `MATCH 0xF422ACB8D5A86FF0`, exit 0.
  - `CommandDemo.tscn`: `MATCH 0x00D3D471EF7354BC`, exit 0.
  - `AppraisalDemo.tscn`: `MATCH 0x194805888CBE240D` (format 11), exit 0.
  All three unchanged from TASK-053's pins, as expected — no
  `Simulation.step`/canonical-state change.
- A windowed screenshot capture (`--screenshot`, the existing
  `CommandDemo.tscn` scripted priming, unmodified) through the real
  editor: `docs/evidence/task-054-agent-facing.png`, both friendly figures
  now visibly rendering a running-cycle pose instead of the old single
  standing idle figure.
- `git status --porcelain`: matches the task's allowed scope — no
  `CommandoWar.Sim`/`CommandoWar.Headless` file touched; `agent_human.png`
  removed, ten `agent_human_facing*.png` added.

## Review round 1 (2026-09-19, Dave live-tested B-052/B-056 together)

Two real defects found and fixed in the facing implementation; B-056 was
untouched and needed no changes.

**Report 1**: "facing direction doesnt seem to be any better either."
Root cause: `RenderShared.facingBin` binned the raw world-grid `atan2`
angle. This game's isometric projection (`FSharpSceneHost.cs`'s
`CellToScreen`) is a skew, not a rotation — a world-diagonal move (e.g.
`dx=1,dy=-1`) projects to a pure horizontal screen move, so binning the
unprojected angle put diagonal movement at the wrong on-screen rotation
entirely. Fixed by projecting `(dx,dy)` through the identical skew
(`screenDx = dx - dy`, `screenDy = (dx + dy) * 0.5`) before computing the
angle to bin. Re-verified: pure-function probe across all 8 world
directions now shows an internally consistent, evenly-progressing mapping
(`E->1, SE->2, S->4, SW->5, W->6, NW->8, N->9, NE->0` in the still-10-bin
scheme at that point); `dotnet test` `342/342`; both scenes' `--selfcheck`
still `MATCH`.

**Report 2**: sent Dave a compass-comparison image (each of the 8 primary
world directions' assigned texture, composited directly from the art
files via `PIL`, no Godot needed) built on the just-fixed projection.
Dave's response: "the image you posted above looks like part of a running
animation, not different angles of ONE frame of animation." Correct, and
the real problem: `Human_0_Run0..Run9` (what TASK-054's original design
session used) is genuinely a running *animation* — orientation and gait
phase change together across the sequence, so freezing on any one frame
can only ever show an arbitrary snapshot of a run cycle, never a clean
static facing pose, regardless of which frame the (now-correct) angle math
picks.

Dave then pointed directly at his own local copy of the pack
(`kenney_isometric-miniature-prototype`) and its `Information.png`, which
documents the convention neither this session's nor TASK-041's kenney.nl
inspection caught: `Human_0..7` **are** 8 rotations of one idle pose
(`0 = N`, `1 = NE`, `2 = E`, `3 = SE`, `4 = S`, `5 = SW`, `6 = W`, `7 = NW`,
clockwise, screen-relative) — each `Human_N`'s own `Run0..Run9`/
`Pickup0..Pickup9` is that *direction's* animation set, not further
rotations. Confirmed by cropping and comparing all eight
`Human_N_Idle0.png` side by side (`PIL`): a clean, consistent rotating
idle figure, none of the running-animation confound.

**Fix**: replaced all ten `agent_human_facing*.png` files in place (same
paths, `LICENSE-THIRD-PARTY.md` documents both the withdrawn and corrected
source) with the eight true `Human_N_Idle0.png` rotations, cropped to
their own shared alpha-bbox union (`(99, 323, 58, 138)` px, the
`Run`-cycle crop's own precedent). `RenderShared.facingBin` reduced from
10 bins/`36°` to 8 bins/`45°`; its angle zero-reference changed from
`atan2 screenDy screenDx` to `atan2 screenDx (-screenDy)` so the result
**is** a `Human_N` index directly (no separate remapping table) — verified
by a pure-function probe reproducing the pack's own compass exactly
(`world NW -> bin 0 (N)`, `world NE -> bin 2 (E)`, `world SE -> bin 4
(S)`, `world SW -> bin 6 (W)`, the four world-diagonal moves landing
exactly on the four screen-cardinal bins, as the isometric geometry
predicts). `FSharpSceneHost.cs`'s `AgentFacingTextures` shrunk from 10 to
8 entries. `IClientScene.fs`'s doc comment and `LICENSE-THIRD-PARTY.md`
both updated to `0..7`.

Re-verified: `dotnet build` both `.slnx` (main Release; Godot client
Debug) `0/0`; `dotnet test` `342/342` (unaffected); both `CommandDemo.tscn`
and `SnapshotDemo.tscn` `--selfcheck` reconfirmed `MATCH` through the real
Godot 4.7.2 editor. Two comparison images generated directly from the art
files (`PIL`, no Godot) and shown to Dave inline during the session (not
committed as separate evidence files — the corrected `agent_human_facing0
..7.png` files themselves are the durable evidence): a labelled strip of
all 8 corrected poses, and a per-world-direction compass check confirming
each of the 8 primary movement directions resolves to a distinct pose
consistent with a real rotation.

## Documents updated

- `tasks/TASK-054-AGENT-FACING-AND-AUDIO-THREAT-INDICATOR.md` (created,
  `Outcome` filled in, then a "Review round 1" section added for the two
  live-review corrections above).
- `docs/11_BACKLOG.md` (B-052 and B-056 rows: `proposed -> review`).
- `docs/12_PROGRESS_LEDGER.md` (this detail file; index rows added).
- `PROJECT_STATE.yaml` (`active_work` updated).
- `src/CommandoWar.Client.Godot/art/LICENSE-THIRD-PARTY.md` (records both
  the withdrawn `Run`-cycle approach and the corrected `Idle`-rotation one,
  so the mistake and its correction stay traceable).

## Review

- Reviewer: Dave.
- Round 1: two defects found and fixed live (see above), B-056 untouched.
- Accepted: yes (2026-09-19). Both B-052 (facing, corrected through round
  1) and B-056 (audio-localised threat cue, unchanged since first
  implementation) confirmed live in the running game; no changes
  requested.
