# TASK-051: Player-facing fog of war

Status: done (implemented and self-verified 2026-09-18; Godot `--selfcheck`
independently confirmed `MATCH` through the real editor 2026-09-18; accepted
by Dave 2026-09-18)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises backlog B-055

## Outcome (2026-09-18)

Implemented smaller than backlog B-055's own text anticipated: **no
`CommandoWar.Sim` change was needed at all**. `CommandDemoScene.fs` already
builds a `Diagnostics.frameOf` frame every tick regardless of the `F1`
dev-overlay toggle (the TASK-046 `AgentVitals` precedent, used to promote
casualty markers to player-facing without a Sim change), and that frame's
existing `KnownContact` overlay already carries exactly `WorldState.
TacticalKnowledge` (last-known cell, `Confidence`, `LastSeenTick`) per
hostile contact. Fog of war reads that overlay instead of adding a new
`RenderSnapshot` field.

`agentItems`'s per-agent draw-item construction now branches on
`(a.Side, hostileKnownContacts.TryFind agentId)`:

- a hostile with no entry (never contacted, or its contact has fully
  expired -- `PerceptionConfig.ExpireAfter`, 60 ticks unseen) draws nothing;
- a hostile with an entry whose `LastSeenTick` equals the current tick
  (seen this tick) draws exactly as before -- true live position, full
  vitals (`Dead`/`Incapacitated`/`Alive` wound dot);
- a hostile with an entry whose `LastSeenTick` is older (known, not
  currently visible) draws a new `Kind = 5` hollow ring plus a small `"?"`
  text label at `Contact.LastKnownCell`, not its true position, at an
  opacity of `0.9 * Confidence / PerceptionConfig.ConfidenceFull` --
  `1000/1000` (0.9) while merely stale, `750/1000` (0.675) once
  `PerceptionConfig.StaleAfter` (20 ticks unseen) drops the confidence band,
  matching the existing wound-dot-severity-scales-opacity idiom;
- a friendly agent is always rendered as before (fog only ever hides
  hostiles).

Central decisions confirmed with Dave via `AskUserQuestion` before drafting
(all three the recommended default): scope to `CommandDemoScene` only, not
`SnapshotDemo.tscn` (a passive rendering tech-demo with no player to fog
anything from); a distinct hollow-ring-plus-label shape for the "last known"
marker, not a translucent recolour of the existing halo/wound-dot family
(docs/06 "status indicators that do not rely on colour alone"); the ring's
opacity follows `Contact.Confidence`'s band drop.

**A real design gap found during implementation, fixed before it shipped**:
the first draft applied fog-of-war unconditionally, including while the
`F1` developer overlay is on. That silently broke TASK-043's own debugging
tool: the dev overlay's whole point (`docs/06` section 11,
"known-versus-authoritative") is to show the real agent at its true
position *and* a separate yellow known-contact marker, so a developer can
see the two diverge. With fog applied inside the overlay too, the real
hostile could vanish behind fog exactly when a developer most needs to see
ground truth. Fixed by gating fog behind `not devOverlay`: with the overlay
on, every agent renders via a new `renderVitals` helper unconditionally
(the pre-TASK-051 behaviour, unchanged); fog only ever applies to the normal
player view. `agentItems`'s vitals-rendering logic was factored out into
`renderVitals` so both branches share it without duplication.

New `RenderShared.cellRing` (`Kind = 5`) and a matching `case 5` in
`FSharpSceneHost.cs`'s `_Draw` (`DrawArc` with no fill, at the same vertical
offset an agent figure draws at, so the ring lines up exactly where the real
figure would reappear).

Verified with a temporary `dotnet fsi` scratch probe (removed after use, the
TASK-042 precedent) driving `CommandDemoScene` directly -- `IClientScene`
carries no `Godot.*` type, so this needs no Godot process. `Ready()`, then
`Update(1/20.0)` once per tick (an exact-tick step, no accumulator
rounding), inspecting `DrawList()`'s `DrawItem[]` for `Kind = 1`
full-opacity figures and `Kind = 5` ghost rings. Selected agent 0 and sent
it to `(3,7)` -- within `Perception.SightRange` (10) of the hostile at
`(11,7)` but outside `CombatConfig.WeaponRange` (7), so it makes contact
without drawing fire -- then back to `(0,0)`. Confirmed the complete
sequence tick by tick: hidden through tick 20 (never contacted); a live
figure at `(11,7)` from tick 21 (in range); after the retreat order takes
effect, a `Kind = 5` ghost at `(11,7)`, `alpha = 0.90`; `alpha` drops to
`0.68` (`0.675`, rounded) exactly `PerceptionConfig.StaleAfter` (20) ticks
after the contact was last seen; the ghost disappears entirely exactly
`PerceptionConfig.ExpireAfter` (60) ticks after last seen, fully hidden
again. A second probe confirmed the `F1` bypass: with the dev overlay
toggled on from `Ready()`, the same never-contacted hostile draws live from
tick 1.

`dotnet build CommandoWar.slnx -c Release` and `CommandoWar.Client.Godot.slnx
-c Debug`: `0/0`. `dotnet test`: `342/342` (unaffected -- no
`CommandoWar.Sim`/`CommandoWar.Headless` change). Both scenes' `--selfcheck`
hashes independently re-run through the real Godot 4.7.2 editor and
confirmed `MATCH` (render-only, as expected): `SnapshotDemo.tscn`
`0xF422ACB8D5A86FF0`, `CommandDemo.tscn` `0x00D3D471EF7354BC`,
`AppraisalDemo.tscn` `0x194805888CBE240D` (format 11). Committed screenshot
`docs/evidence/task-051-fog-of-war.png`: the default screenshot-priming
sequence (which never approaches the hostile) now shows only the two
friendly agents -- no red hostile figure anywhere in frame, unlike every
prior task's screenshot of the same scene.

Full detail: `docs/ledger/2026-09-18-TASK-051-player-facing-fog-of-war.md`.

## Objective

Give the player a real fog of war in `CommandDemoScene`: hide a hostile the
friendly squad has never made contact with; show a last-known-position
marker, not the true position, once contact is lost.

## Why this task exists

Backlog B-055, raised by Dave during TASK-043 review (2026-09-17): today
every scene draws every agent, hostile included, at its true position
unconditionally -- there is no fog-of-war in the player's own view (the
developer-only `KnownContact` ghost-marker-vs-real-agent comparison is by
design a debugging tool, not player-facing). `WorldState.TacticalKnowledge`'s
existing decaying-confidence contact store already has everything needed;
only the rendering side of it doesn't exist yet. Selected via
`AskUserQuestion` over the smaller client-polish rows (B-053/B-054) and
continuing straight into an AP-budget layer, as the largest ready item,
flagged by Dave multiple times.

## Central decisions (confirmed with Dave 2026-09-18 before drafting)

Three rounds, put via `AskUserQuestion`, Dave choosing the recommended
default each time:

1. **Scope: `CommandDemoScene` only**, not `SnapshotDemo.tscn` -- the latter
   is a passive rendering tech-demo (canned log, no player input), so
   leaving it showing everything is a documented, deliberate divergence,
   not a gap.
2. **A hollow outlined ring plus a small text label** for the last-known
   marker, not a translucent recolour of the real agent -- a distinct
   shape, not just a tint (docs/06 "status indicators that do not rely on
   colour alone"), so it reads unambiguously as stale intel.
3. **The ring's opacity fades with `Contact.Confidence`'s own band drop** --
   cheap (`Confidence` is already in the `KnownContact` overlay tuple) and
   mirrors the existing wound-dot-severity-scales-opacity precedent already
   in this file.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `src/CommandoWar.Sim/Perception.fs` (`Contact`, `PerceptionConfig.
  SightRange`/`ConfidenceFull`/`ConfidenceBandDrop`/`StaleAfter`/
  `ExpireAfter`, `mergeKnowledge`)
- `src/CommandoWar.Sim/Diagnostics.fs` (`Overlay.KnownContact`,
  `knownContactOverlays`, `frame`/`frameOf`)
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` (`devFrame`'s
  always-computed-regardless-of-toggle precedent, `agentItems`'s existing
  `AgentVitals`-driven vitals rendering, the `F1` dev-overlay's own
  `knownContacts` ghost-marker comparison)
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`
  (`cellMarker`/`cellLabel`/`agentColor`, the primitive-helper precedent)
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs` (`DrawItem`, the
  `Kind` vocabulary and its doc comment)
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs` (`_Draw`'s `Kind`
  switch)
- `docs/06_CONTENT_AND_PRESENTATION.md` section 11 ("known-versus-
  authoritative", "status indicators that do not rely on colour alone")

## Dependencies

- B-015 (TASK-026, `WorldState.TacticalKnowledge`) -- done.
- B-029 (TASK-043, developer overlay rendering, the `KnownContact` overlay
  precedent) -- done.

## Allowed scope

- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: fog-of-war
  gating in `agentItems`, a `not devOverlay` bypass, `renderVitals`
  extracted as a shared helper, a corrected comment on the `F1` overlay's
  own known-contact marker.
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`: new `cellRing`
  helper (`Kind = 5`).
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`: `DrawItem`'s doc
  comment, documenting `Kind = 5`.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: a new `case 5` in
  `_Draw`'s `Kind` switch.
- `src/CommandoWar.Client.Godot/README.md` (new section, the TASK-041/046/
  048/049 precedent).
- `docs/evidence/task-051-fog-of-war.png` (new screenshot).
- `docs/11_BACKLOG.md` (B-055 row), `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- Any change to `src/CommandoWar.Sim` or `src/CommandoWar.Headless` (not
  needed -- the existing `KnownContact` overlay already carries everything).
- Any change to `SnapshotDemo.tscn`/`DemoRenderScene.fs` (out of scope per
  Central decision 1).
- Any change to the Hostile side's own tactical picture
  (`HostileTacticalKnowledge`) or AI targeting -- this task is purely
  player-facing rendering.
- Action-point budgeting or an Overwatch commitment state (unrelated,
  separately deferred follow-on work).

## Required work

1. Read `WorldState.TacticalKnowledge` via the existing `devFrame.Overlays`'
   `KnownContact` case -- no new `CommandoWar.Sim` exposure.
2. Gate `agentItems`'s hostile rendering on contact status: hidden / live /
   last-known ghost, as described above.
3. Preserve the `F1` dev overlay's ground-truth behaviour (a real
   regression risk found and fixed during implementation -- see Outcome).
4. New `Kind = 5` primitive: `RenderShared.cellRing`, `IClientScene.fs` doc
   comment, `FSharpSceneHost.cs`'s `_Draw` case.
5. Verify via a temporary `dotnet fsi` scratch probe driving the scene
   directly (no Godot process needed, `IClientScene` is Godot-free);
   confirm the full hidden -> live -> ghost -> faded -> expired sequence
   against the exact `PerceptionConfig` tick constants.
6. Confirm both scenes' `--selfcheck` hashes unaffected through the real
   Godot editor (render-only change).
7. Capture a screenshot; update `README.md`, backlog/ledger/state.

## Acceptance criteria

- [x] A hostile the friendly squad has never contacted is not drawn.
- [x] A hostile currently visible this tick renders exactly as before (true
      position, full vitals).
- [x] A hostile known but not currently visible renders only a last-known-
      position marker, distinct in shape from a real agent, never its true
      live position.
- [x] The marker's opacity reflects `Contact.Confidence`'s decay, and the
      marker disappears once the contact fully expires.
- [x] The `F1` developer overlay's ground-truth rendering is unaffected by
      fog of war.
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless` change; both scenes'
      `--selfcheck` hashes unaffected.
- [x] `dotnet build`/`dotnet test` unaffected; Godot client builds.
- [x] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet test`: `342/342` (unaffected).
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: `0/0`.
- A `dotnet fsi` scratch probe driving `CommandDemoScene` directly,
  confirming the full hidden/live/ghost/faded/expired sequence and the `F1`
  bypass (see Outcome for the exact tick-by-tick trace).
- Godot editor `--selfcheck` for all three scenes through the real Godot
  4.7.2 editor: confirmed `MATCH` -- `SnapshotDemo.tscn`
  `0xF422ACB8D5A86FF0`, `CommandDemo.tscn` `0x00D3D471EF7354BC`,
  `AppraisalDemo.tscn` `0x194805888CBE240D` (all unchanged), all exit 0.
- Windowed screenshot `docs/evidence/task-051-fog-of-war.png`.
- `git status --porcelain`: matches this task's allowed scope.

## Documentation updates

- This task file's Outcome section.
- `src/CommandoWar.Client.Godot/README.md`.
- `docs/11_BACKLOG.md` (B-055 row: proposed -> done).
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Fully additive and render-only: one new `DrawItem.Kind` value, one new
`RenderShared` helper, one new `_Draw` case, and a gating branch in
`agentItems`. No `CommandoWar.Sim`/`CommandoWar.Headless` change, no hash
format change, no corpus entry touched. Revertible with `git revert` in one
step; both scenes' `--selfcheck` hashes are unaffected either way, so
nothing needs re-pinning on rollback.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-18). No changes requested.
