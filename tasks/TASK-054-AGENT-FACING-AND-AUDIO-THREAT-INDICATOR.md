# TASK-054: Agent sprite facing/orientation and audio-localised threat indicator

Status: done (implemented, self-verified, and corrected through one live
review round with Dave 2026-09-19; Godot `--selfcheck` independently
confirmed `MATCH` through the real editor after every change; accepted by
Dave 2026-09-19)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises backlog B-052 and B-056
Size: S-M (bundled, the TASK-037/052 precedent)

## Outcome (2026-09-19)

Implemented as scoped in Central decisions 1-5. One real correction found
during implementation, not anticipated when the task was drafted:

**B-056's trigger check must read the friendly squad's known-contact set
from *before* the tick's own step, not the post-tick `devFrame`.**
`Simulation.step`'s phase order runs Perception ahead of Combat, so by the
time a `FireLine` overlay exists in the post-tick `devFrame`, that same
tick's Perception phase has already had the chance to register the shooter
as a `KnownContact` if any friendly agent's own sight trace reached it —
which, given `CombatConfig.WeaponRange` (7) is strictly inside
`PerceptionConfig.SightRange` (10) and both sides trace the same
terrain-symmetric line, is the common case exactly on the tick a hostile
first opens fire. Checking the post-tick set would have suppressed the cue
on precisely the "just came into view and fired" tick B-056 exists to
cover, leaving only already-redundant cases reachable. `stepOnce` now
captures `hostileKnownContactIdsBefore` from the *prior* tick's `devFrame`
before calling `Simulation.step`, and checks a firing shooter against that
snapshot instead.

Verified with a temporary `dotnet fsi` scratch probe (removed after use,
the TASK-042/051/052/053 precedent): `RenderShared.facingBin` checked
directly as a pure function across all 8 primary world directions (`East`
-> bin 0, `South` -> bin 2, `West` -> bin 5, `North` -> bin 8, `SE`/`NW`
diagonals -> bins 1/6 — 10 bins over 8 directions don't divide evenly, so
exact bin numbers are a cosmetic artifact of `System.Math.Round`'s
banker's-rounding at the `.5`-sector boundaries, not a bug) plus the
"no active destination" and "already arrived" freeze cases (both correctly
return the unchanged `prevBin`). A live `CommandDemoScene` run confirmed a
scripted `MoveTo(3,0)` from `(0,0)` (pure `+X`) renders `TextureId = 0`
throughout the approach and after arrival, matching the assumed
bin-0-is-`+X` convention self-consistently (this assumption — which
Kenney `Run` frame index corresponds to which real-world facing — is not
independently verifiable against 3D camera metadata and is documented as
cosmetic-only in `RenderShared.facingBin`'s own comment).

A second probe drove friendly agent 0 from `(0,0)` toward `(9,6)` (the
TASK-053 precedent: within `CombatConfig.WeaponRange` and clear line of
sight of the hostile at `(11,7)`) for 30 ticks. `state.Random.Draws`
confirmed real, unscripted combat fired on ticks 21-24 (two draws per tick,
symmetric engagement) then stopped (both sides' engagement conditions
lapsed). Exactly one audio-cue `"!"` label appeared, first on tick 21 (the
same tick the first shot landed) and unchanged in count through tick 30 —
proving the cue fired on the shooter's first, previously-unknown shot and
was correctly *not* re-added on any of the three further ticks (22-24) the
still-firing, now-known hostile kept shooting. No cue appeared on ticks
1-20 (out of range) or attributable to the friendly's own return fire (the
`Side = Hostile` filter).

`dotnet build CommandoWar.slnx -c Release` and
`CommandoWar.Client.Godot.slnx -c Debug`: `0/0`. `dotnet test`: `342/342`
(unaffected — no `CommandoWar.Sim`/`CommandoWar.Headless` file touched).
`cwheadless corpus`: `16/16` (unaffected). All three scenes' `--selfcheck`
hashes independently re-run through the real Godot 4.7.2 editor and
confirmed `MATCH` (unaffected, as expected for a presentation-only
change): `SnapshotDemo.tscn` `0xF422ACB8D5A86FF0`, `CommandDemo.tscn`
`0x00D3D471EF7354BC`, `AppraisalDemo.tscn` `0x194805888CBE240D`.
`git status --porcelain` matches this task's allowed scope exactly (no
`CommandoWar.Sim`/`CommandoWar.Headless` file touched). Screenshot
`docs/evidence/task-054-agent-facing.png` captured through the real
editor's existing `--screenshot` scripted priming, showing both friendly
figures rendering a running-cycle facing frame in place of the old single
idle pose (the priming sequence does not reach combat range, so it does
not show the audio cue — that feature's evidence is the scratch-probe
trace above, the TASK-053 precedent for a logic-only feature with no
dedicated screenshot).

**Review round 1 (2026-09-19, Dave live-tested B-052/B-056 together):** two
real defects found and fixed, superseding the facing implementation above
in full (B-056 unaffected throughout).

1. Dave tried the build live and reported the facing "doesn't seem any
   better" than before the task. Root cause: `facingBin` binned the raw
   world-grid `atan2` angle, but this game's isometric projection is a
   skew, not a rotation — a world-diagonal move (e.g. `dx=1,dy=-1`)
   projects to a pure horizontal screen move, so binning the unprojected
   angle put diagonal movement at the wrong on-screen rotation entirely.
   Fixed by projecting the heading through the same skew
   `FSharpSceneHost.cs`'s `CellToScreen` applies before binning it.
2. A follow-up compass-comparison image (all 8 primary world directions'
   assigned texture, sent to Dave) still drew the correct response: "looks
   like part of a running animation, not different angles of one frame."
   Investigating why surfaced a genuine misreading of the source pack from
   the original design session: `Human_0..7` were assumed to be 8
   colour/character variants (each with its own single `Idle0` and a full
   `Run0..Run9`/`Pickup0..Pickup9` animation set), when they are actually
   **8 rotations of one idle pose** — the pack's own `Information.png`
   (which Dave located and pointed at directly, in his own local copy of
   the download) documents this explicitly (`0 = N`, `1 = NE`, ... `7 =
   NW`, clockwise, screen-relative), and cropping/comparing all eight
   `Human_N_Idle0.png` side by side confirmed it: a clean rotating idle
   figure, not animation-frame variation. `Human_0_Run0..Run9` (what the
   first cut used) is direction-`0`'s own run *animation*, unrelated to
   facing. `Idle0` was never a dead end at all — the first two design
   passes (this session and TASK-041's own art survey) both missed it.
   Corrected: all ten `agent_human_facing*.png` files replaced in place
   (paths unchanged) with the eight true `Human_N_Idle0.png` rotations,
   `facingBin` reduced from 10 bins/`36°` to 8 bins/`45°`, and its
   zero-reference changed to align directly with the pack's own `N`
   (`atan2 screenDx (-screenDy)`, not `atan2 screenDy screenDx`) so a bin
   number **is** a `Human_N` index with no separate remapping table.
   `AgentFacingTextures` (`FSharpSceneHost.cs`) shrunk from 10 to 8
   entries to match. `LICENSE-THIRD-PARTY.md` rewritten to record both the
   superseded first cut and the corrected version, so the mistake and its
   correction stay traceable rather than silently overwritten.

Re-verified after both fixes: `dotnet build` both `.slnx` (main Release;
Godot client Debug) `0/0`; `dotnet test` `342/342` (unaffected); all three
scenes' `--selfcheck` hashes reconfirmed `MATCH` through the real Godot
4.7.2 editor (unaffected both times, as expected for a presentation-only
change). Two new comparison images generated directly from the art files
(no Godot needed) and shown to Dave: a per-world-direction compass check
(`docs/evidence/task-054-facing-compass-check.png`, superseded by the
`Information.png` correction) and, after the correction, a second
world-direction check confirming each of the 8 primary movement directions
now resolves to a distinct, clean idle pose consistent with a real
rotation (not sent as a separate committed file — inspected inline during
the session; the corrected `agent_human_facing0..7.png` files themselves
are the durable evidence).

B-056 (the audio-localised threat cue) was not touched by either
correction and needed none — its own logic reads `FireLine`/`KnownContact`
overlays, unrelated to agent-figure texture selection.

Full detail: `docs/ledger/2026-09-19-TASK-054-agent-facing-and-audio-threat-indicator.md`.

## Objective

Two small, presentation-only client features, bundled because both were
raised on the same TASK-043 review and both needed a design fork resolved
before drafting:

1. **B-052**: the Kenney placeholder human figure turns to face the
   direction it is actually moving toward, instead of always rendering the
   same fixed pose regardless of movement.
2. **B-056**: a friendly squad that hears (but has no known contact on) a
   hostile's shot gets a coarse, bearing-only visual cue at the edge of the
   play area — "audio-localised" in the sense that it reveals rough
   direction, never the shooter's true position.

No `CommandoWar.Sim`/`CommandoWar.Headless` change. Both features are
purely derived from already-existing `AgentSnapshot`/`Diagnostics.Overlay`
data on the client; neither is authoritative or tactical state, so this is
not a diagnostic-frame-extension task (AGENTS.md's diagnostics rule applies
to authoritative spatial/tactical state added to `CommandoWar.Sim`, which
this task does not touch).

## Why this task exists

- B-052 raised by Dave on accepting TASK-041 (2026-09-17): the single idle
  pose doesn't turn to face the direction an agent last moved. A 2026-09-17
  addendum added a second open question: should facing also track a
  perceived threat, separately from movement direction?
- B-056 raised by Dave during TASK-043 review (2026-09-17): a shot fired
  could be "audio-localised" by the player even without line of sight to
  the shooter, shown as a rough-bearing symbol, not the shooter's exact
  position. Never designed (no symbol, direction-fidelity, or trigger set).

Both rows carried an explicit "do not guess past this" instruction from
Dave. Resolved this session via `AskUserQuestion`, four rounds (see
Central decisions).

## Central decisions (confirmed with Dave 2026-09-18/19 via `AskUserQuestion`)

1. **B-052 facing source**: movement direction only (recommended option).
   No threat-facing; a stationary agent mid-firefight keeps facing wherever
   it last had an active `Destination`.
2. **B-052 art**: live-checked kenney.nl (downloaded and unzipped both the
   "Isometric Miniature Prototype" and "Isometric Miniature Dungeon" packs,
   not assumed from memory) — the whole Kenney "Isometric Miniature" family
   shares one character rig: a single non-rotating `Idle0` pose plus a
   10-angle `Run0..Run9` rotation cycle (confirmed identical in both packs:
   `Human_0..7`/`Male_0..7`, same `Idle0`/`Run0-9`/`Pickup0-9` file
   layout). A further web search for a free CC0 pack combining this
   project's existing rendered/smooth isometric art style with genuine
   multi-angle idle poses found none — the only true 8-directional-idle CC0
   packs (e.g. Hormelz on itch.io) are pixel art, which would visibly clash
   with the existing Kenney terrain/prop art. Dave's confirmed resolution:
   **stay in-style** — freeze on the nearest `Run`-cycle frame while
   stationary, accepting the mid-stride look at rest as the cost of
   always-correct facing, rather than switch art style or drop the feature.
3. **B-052 texture unification**: both `CommandDemoScene` and
   `DemoRenderScene` share one C# host (`FSharpSceneHost.cs`) and draw
   `Kind = 1` agents through the same code path. Maintaining two parallel
   agent-texture regimes (one scene idle-only, one faced) would be a less
   consistent design than sharing one 10-texture facing set across both —
   a judgement call, not asked separately, since it follows directly from
   "one host draws every scene's agents the same way" (ADR-0004) rather
   than being a new design fork. `agent_human.png` (`Idle0`) is retired.
4. **B-056 trigger**: `ShotFired`/`FireLine` only, and only when the
   shooter is `Hostile` and has **no** `KnownContact` entry at all for the
   friendly side (fully fogged — not even a stale ghost) — matches Dave's
   original framing ("audio-localised even without line of sight") and
   never fires redundantly alongside the ordinary fire-line/fog-of-war
   feedback for a shooter already visible or tracked.
5. **B-056 visual**: a screen-edge-ish directional cue, bearing only, no
   distance or exact position — reuses the existing `Kind = 2`/`Kind = 3`
   line-and-label primitives (no new `DrawItem.Kind`) and the
   `fireEffectHoldSeconds` fade-timer precedent, rather than a per-agent
   compass ring.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`.
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` (`stepOnce`,
  `renderVitals`, `DrawList`, `heldFireLines`/`fireEffects` precedent).
- `src/CommandoWar.Client.Godot/Core/DemoRenderScene.fs` (`advanceOneTick`,
  `agentItems`).
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`.
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs` (`DrawItem`,
  `Kind = 1`/`TextureId` semantics).
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs` (`DrawAgentFigure`,
  `AgentTexture`, `AgentSourceRect`, the `_Draw` switch).
- `src/CommandoWar.Sim/Diagnostics.fs` (`FireLine`, `KnownContact` overlay
  shapes — the only source of "is this shooter a known contact" on the
  client side).
- `src/CommandoWar.Client.Godot/art/LICENSE-THIRD-PARTY.md`.

## Dependencies

- B-034 (TASK-041, placeholder art) — done.
- B-019 (TASK-031, `ShotFired`/`FireLine`) — done.
- B-015 (TASK-026, `KnownContact`/`TacticalKnowledge`) — done.
- B-055 (TASK-051, fog-of-war `hostileKnownContacts` map precedent) — done.

## Allowed scope

- `src/CommandoWar.Client.Godot/art/`: ten new files
  `agent_human_facing0.png` .. `agent_human_facing9.png` (cropped from
  Kenney's `Characters/Human/Human_0_Run0.png` .. `Run9.png`, shared crop
  rect); removal of the now-unused `agent_human.png`.
  `LICENSE-THIRD-PARTY.md` updated to match.
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`: a new pure
  `facingBin` helper.
- `src/CommandoWar.Client.Godot/Core/DemoRenderScene.fs`: per-agent facing
  tracking, `agentItems`' `TextureId` populated from it.
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: per-agent facing
  tracking (`renderVitals`'s `Alive`/`Incapacitated` figures); a new
  held-audio-cue list populated from `stepOnce`'s existing
  `devFrame.Overlays` scan (the `heldFireLines` precedent), decayed in
  `Update`, rendered in `DrawList`.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: `AgentTexture` ->
  `AgentFacingTextures: Texture2D[10]`; `DrawAgentFigure` selects by
  `TextureId` and drops the now-unnecessary `AgentSourceRect` crop (the new
  art is already tightly cropped, the `DrawTerrainTile` precedent).
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`: doc-comment update
  for `Kind = 1`'s `TextureId` meaning.
- `docs/11_BACKLOG.md` (B-052, B-056 rows), `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- Any `CommandoWar.Sim`/`CommandoWar.Headless` change (both features are
  presentation-only derivations of already-canonical/already-emitted data).
- Threat-facing (Central decision 1) — movement direction only.
- Any new art style/pack beyond the existing Kenney "Isometric Miniature
  Prototype" family (Central decision 2).
- A new `DrawItem.Kind` for the audio cue (Central decision 5) — reuse
  `Kind = 2`/`3`.
- Triggering the audio cue for a friendly shooter, or for a hostile shooter
  already known (even a stale ghost contact) — Central decision 4.
- `SnapshotDemo.tscn`/`AppraisalDemo.tscn` are unaffected by anything but
  the shared facing-texture change (DemoRenderScene only draws
  `DemoScenario`'s agents; no audio-cue/B-056 change reaches those scenes,
  `AppraisalDemoScene.cs` predates `FSharpSceneHost` entirely per ADR-0004's
  documented scoped deviation and is out of scope here).

## Required work

1. Download and inspect the Kenney "Isometric Miniature Prototype" zip
   (already done this session); crop `Human_0_Run0..9.png` to the shared
   alpha-bbox union rect `(80, 314, 87, 144)` (found by inspecting the
   source PNGs' alpha channels, not guessed — matches TASK-041's own
   `AgentSourceRect`-by-inspection precedent); save as
   `art/agent_human_facing0.png` .. `facing9.png`; delete `agent_human.png`;
   update `LICENSE-THIRD-PARTY.md`.
2. `RenderShared.facingBin (prevBin: int) (position: Cell)
   (destination: Cell option) : int` — pure. When `destination = Some d`
   and `d <> position`, computes `atan2` of the world-grid delta and bins
   it into one of 10 `36°` sectors (documented assumption: bin `0` aligns
   to due-`+X` world movement, based on the visually observed `Run0..9`
   rotation order — cosmetic only, not verifiable against real 3D camera
   metadata, does not affect any hash). Otherwise returns `prevBin`
   unchanged (freeze while stationary or arrived).
3. `DemoRenderScene`/`CommandDemoScene`: add `mutable facing: Map<int,int>`,
   updated once per authoritative tick (in `advanceOneTick`/`stepOnce`, not
   per render frame) via `facingBin`; `agentItems`/`renderVitals`'s
   `Alive`/`Incapacitated` figures read `TextureId` from it (default `0`
   for an unseen id).
4. `FSharpSceneHost.cs`: `AgentFacingTextures: Texture2D[10]` replacing
   `AgentTexture`; `DrawAgentFigure` selects by `Mathf.Clamp(facingIndex, 0,
   9)` and sizes from the texture's own aspect ratio (no crop rect needed —
   the new art is pre-cropped); `IClientScene.fs`'s `Kind = 1` doc comment
   updated.
5. `CommandDemoScene.stepOnce`: alongside the existing `FireLine` ->
   `heldFireLines` scan, detect a qualifying shot (`shooter` is `Hostile`
   and has no `KnownContact` overlay entry at all this tick) and, if the
   friendly squad has at least one living agent, compute a bearing from the
   squad's centroid to the shooter's true cell, bin it into one of 8
   `45°` sectors, and add a held audio-cue (a boundary anchor point just
   outside `state.Bounds` in that sector, plus a short inward-pointing tip)
   to a new `heldAudioCues` list.
6. `Update`: decay `heldAudioCues` by wall-clock time (the `heldFireLines`
   precedent), dropping expired entries.
7. `DrawList`: render each held audio cue as one `Kind = 2` line (anchor to
   tip) plus one `Kind = 3` `"!"` label at the anchor, faded by remaining
   time, always-on (not gated behind `F1`, the `fireEffects` precedent —
   dev overlay is a ground-truth comparison tool, not a suppressor of
   player-facing feedback).
8. Verify with a temporary `dotnet fsi` scratch probe (removed after use):
   drive `CommandDemoScene` through a scripted `MoveTo` in a known
   direction and confirm the emitted `TextureId` matches the expected
   facing bin and freezes on arrival; drive a scenario where a hostile
   fires at a friendly with no prior contact and confirm exactly one
   audio-cue item appears, fades over `audioCueHoldSeconds`, and never
   appears once the hostile becomes a known contact.
9. Confirm both scenes' `--selfcheck` hashes unaffected through the real
   Godot editor (presentation-only; no `Simulation.step` change).
10. Capture an updated screenshot showing a moving agent's changed facing
    and, if reachable in the scripted `--screenshot` sequence, the audio
    cue.
11. Update backlog/ledger/state.

## Acceptance criteria

- [x] An agent moving toward a `Destination` renders one of 10 facing
      textures matching its direction of travel, not a single fixed pose.
- [x] A stationary agent (no `Destination`, or arrived) keeps rendering its
      last active-movement facing texture, never reverting to a separate
      "idle" pose.
- [x] Facing applies identically in both `DemoRenderScene`
      (`SnapshotDemo.tscn`) and `CommandDemoScene` (`CommandDemo.tscn`)
      through the shared `FSharpSceneHost` draw path.
- [x] A hostile shot fired at a friendly, where the shooter has no
      `KnownContact` entry for the friendly side, produces exactly one
      bearing-only visual cue near the edge of the play area, never at the
      shooter's true position, fading out over `audioCueHoldSeconds`.
- [x] The same shot produces **no** cue once the shooter has any
      `KnownContact` entry (even stale), and a friendly-side shot never
      produces a cue.
- [x] The audio cue renders identically regardless of the `F1` dev-overlay
      toggle (unconditional in `DrawList`, not gated on `devOverlay`).
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless` change; both scenes'
      `--selfcheck` hashes unaffected.
- [x] `dotnet build`/`dotnet test` unaffected; Godot client builds.
- [x] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet test`: unaffected count.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: `0/0`.
- Temporary `dotnet fsi` scratch probes (removed after use) proving facing
  bins and the audio-cue trigger/suppression conditions directly, per
  Required work item 8.
- Godot editor `--selfcheck` for `SnapshotDemo.tscn`/`CommandDemo.tscn`/
  `AppraisalDemo.tscn` through the real Godot 4.7.2 editor: all three
  `MATCH`, hashes unaffected.
- `git status --porcelain`: matches this task's allowed scope.

## Evidence to capture

- Scratch-probe output showing facing-bin transitions and freeze-on-arrival.
- Scratch-probe output showing the audio-cue trigger/no-trigger conditions.
- `--selfcheck` hashes for all three scenes, confirmed unchanged.
- Updated/new screenshot(s) under `docs/evidence/`.

## Expected files

- `src/CommandoWar.Client.Godot/art/agent_human_facing0.png` ..
  `facing9.png` (new), `agent_human.png` (removed),
  `LICENSE-THIRD-PARTY.md`.
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`,
  `DemoRenderScene.fs`, `CommandDemoScene.fs`, `IClientScene.fs`.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`.
- `tasks/TASK-054-AGENT-FACING-AND-AUDIO-THREAT-INDICATOR.md`.
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md` (+ new
  `docs/ledger/` detail file), `PROJECT_STATE.yaml`.

## Documentation updates

- This task file's Outcome section.
- `docs/11_BACKLOG.md` (B-052, B-056 rows: `proposed -> done`).
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Fully additive/presentation-only: no `CommandoWar.Sim`/`CommandoWar.Headless`
change, no hash format change, no new `AgentSnapshot` field. Revertible with
`git revert` in one step; both scenes' `--selfcheck` hashes are expected
unaffected either way, so nothing needs re-pinning on rollback.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-19). Confirmed live in the running game; no
  changes requested.
