# TASK-074: Weapon/engagement-range display and HUD marker legibility

Status: done
Owner: unassigned
Phase: P4
Gate: G4 (vertical slice feature-complete); realises B-074 (B-070's original,
still-unbuilt "show firearm ranges on the map" ask) and folds in a marker-
legibility fix the same review surfaced

## Objective

Render a `CombatConfig.WeaponRange`-radius indicator around (a) the selected
friendly agent, when exactly one is selected, and (b) every currently known
hostile contact -- so a player can see an engagement envelope on the map
before committing an order, closing docs/06 section 8's "modern usability
requirements" bullet "status indicators that do not rely on colour alone" as
it applies to range, and B-070's own original request. While in the same
render path, address the accompanying finding (confirmed independently by
this session's own live-play UX review, not merely inferred): the existing
threat/objective/hover/leader rings are still collectively busy and
differentiated mostly by colour and radius alone -- do not let the new range
indicator add a fifth near-identical ring to that cluster; give it a
genuinely distinct shape (see Central decisions).

This is one observable outcome (the map now shows engagement envelopes,
legibly, without worsening HUD clutter), not two independent features
bundled for convenience -- both parts touch the same render path and files
(`RenderShared.fs`, `CommandDemoScene.fs`), so splitting them across two
tasks would force two parallel worktrees to collide on the same files.

## Why this task exists

Dave raised "having firearms ranges shown on the map would make this more
intuitive" directly (2026-09-21, recorded in `docs/11_BACKLOG.md`'s B-070 row
and `PROJECT_STATE.yaml`) while reviewing the newly-confirmed depot-rifleman
threat on the bridge-charge cell. B-072 (refusal salience) and B-073
(Bridgehead terrain redesign) both shipped since and made a real stand-off
refusal reachable on Bridgehead for the first time -- but neither built the
range indicator itself; grep confirms `WeaponRange`/`EngagementRange` appear
nowhere in `src/CommandoWar.Client.Godot`, only in a README, live-confirmed
again this session (both a dedicated scoping pass and an independent
UX-expert-persona review of the *current* build, screenshotting the real F1
developer overlay, found zero range indicator anywhere, live or dev-only).

The UX review's own top-ranked recommendation, made independently of the
scoping pass and arrived at the same answer: refusal is now reachable
(B-072/B-073) but still not *legible* before the fact -- a refusal like
"route too exposed (threat: agent 102)" fires with no way to see that
agent's engagement envelope on the map ahead of ordering into it. This is
the more consequential of the two remaining UI gaps that review found (the
other being marker-cluster legibility, folded in here to avoid a file
collision -- see Objective).

The marker-legibility finding: the same review confirmed, via real
screenshots of the live scripted playthrough (not code-reading alone), that
the objective ring (violet), extraction ring (cyan), fog-of-war "last-known
hostile" ring (hostile's own colour), hover ring (near-white), and leader
ring (green, with a "LEADER" text label) all render as the same thin
`Kind = 5` circle shape at nearly the same radius (`agentRadius * 0.9` to
`agentRadius + 6`), differentiated by hue and a few pixels alone in the
cases without an accompanying text label (hover ring; the selection halo,
`Kind = 1` filled). Screenshots at ticks 11/24/25 showed a green LEADER ring,
white hover ring, gold selection halo, and a red fog-of-war ring all
clustered within roughly 150px of each other at the depot corner --
"collectively busy," confirming the original B-070 review's own framing
still holds and neither B-072 nor B-073 touched the renderer at all.

## Required reading

- `docs/06_CONTENT_AND_PRESENTATION.md` line 234 ("status indicators that do
  not rely on colour alone") and the surrounding "Modern usability
  requirements" list (lines 228-238).
- `docs/11_BACKLOG.md` B-070's row in full (the original request and both
  expert reviews it already summarises) and B-072/B-073's rows (what has and
  has not changed in the renderer since).
- `src/CommandoWar.Sim/Combat.fs`: `CombatConfig.WeaponRange` (line 33,
  confirmed `7`) -- the radius to render for a friendly's own weapon.
- `src/CommandoWar.Sim/Appraisal.fs`: `AppraisalConfig.ThreatEngagementRange`
  (line 90, confirmed `8`) and `Perception.chebyshev` -- confirm distance is
  Chebyshev (grid, not Euclidean) throughout before choosing a shape.
- `src/CommandoWar.Sim/Sight.fs` -- confirm the range indicator is a pure
  distance envelope, not LOS-aware; note this as a known simplification (see
  Inputs and assumptions), since Bridgehead's elevation-berm LOS blocking
  (B-073's own finding) means a naive distance ring can overclaim what is
  actually visible/engageable.
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs` in full, especially
  `cellMarker` (line 50, `Kind = 1` filled circle), `cellRing` (line 70,
  `Kind = 5` hollow ring, and its own doc comment already citing the
  docs/06 colour-alone rule for the existing fog-of-war ring), `lineMarker`
  (line 86, `Kind = 2` line segment between two cells -- the primitive this
  task's range indicator should reuse, see Central decisions).
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: the existing ring
  sites -- `objectiveAreaColor`/`extractionAreaColor` and
  `buildObjectiveMarkerItems` (lines 51-68, includes an `AreaId` text label
  per ring via `RenderShared.cellLabel`), the fog-of-war ring (lines
  949-971, includes a `"?"` label), the hover ring (lines 1030-1034, *no*
  label), the leader ring (lines 1051-1085, `Kind = 5` ring plus a `Kind = 3`
  `"LEADER"` text label), the selection halo (`haloRadius`, line 34, `Kind =
  1` filled, gold). Also `hostileKnownContacts` (line 783) -- the existing
  fog-of-war map giving a known hostile's `lastKnownCell`, the same source
  this task's hostile-side range indicator should read (do not introduce a
  second contact-tracking mechanism).
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: `CellToScreen`
  (lines 489-490, `Origin + ((cx-cy)*TileW/2, (cx+cy)*TileH/2)`, confirmed
  affine/linear) and the existing `DrawItem.Kind` values 0-5 (terrain, filled
  circle, line segment, text label, effect sprite, hollow ring) -- confirm
  whether a new `Kind` is actually needed (see Central decisions; this
  session's own scoping pass found it is not: the range indicator can be
  built from four `Kind = 2` line segments, no C#/`FSharpSceneHost.cs`
  render-side change).

## Dependencies

- B-070 (done -- the review that raised this), B-072 and B-073 (both done --
  confirmed this session, via live screenshots, to have made refusal
  reachable but not touched the renderer at all). No other task selected.

## Central decisions

1. **Shape: the true Chebyshev-range square's own outline, not an inscribed
   diamond and not a circle.** Confirm by inspection before implementing:
   the set of cells within `WeaponRange` R of a centre `(cx, cy)` under
   Chebyshev distance is the axis-aligned square with corners `(cx-R,cy-R)`,
   `(cx+R,cy-R)`, `(cx+R,cy+R)`, `(cx-R,cy+R)`. Because `CellToScreen` is
   affine, that square's own four straight edges map to four straight
   screen-space line segments -- draw those four corner-to-corner edges with
   `RenderShared.lineMarker` (reusing the existing `Kind = 2` primitive, the
   fire-line/LOS-ray precedent), **not** a `cellRing`/circle (which would
   misrepresent a square envelope as round) and **not** a diamond connecting
   only the four edge-midpoints `(cx±R,cy)`/`(cx,cy±R)` (which understates
   the true range along the diagonals -- a real risk this task's own initial
   scoping pass proposed and which must be corrected here, not carried
   through uncritically). Verify the resulting screen-space shape with a
   real screenshot before treating this as done, not by this reasoning
   alone.
2. **Trigger: always-on for a single friendly selection, always-on for every
   known hostile contact.** Mirrors the existing single-selection
   dev-detail precedent (`CommandDemoScene.fs:1432`) for the friendly side;
   the hostile side reads the same `hostileKnownContacts` fog-of-war map
   already gating the last-known-position ring, so a range indicator never
   appears for an un-contacted hostile (consistent with existing fog-of-war
   behaviour, no new intel leak). No new toggle key -- `F1` is already the
   developer overlay, and this indicator is deliberately player-facing,
   always visible when its trigger condition holds, not developer-only.
3. **Distinct visual treatment from the existing ring cluster.** The range
   outline (four line segments forming a square) is already a genuinely
   different shape from every existing marker (which are all either a
   filled circle or a hollow circular ring) -- use a low-alpha, thin,
   dashed-reading colour (e.g. a desaturated orange/amber distinct from every
   existing marker hue: violet/cyan/agent-colour/near-white/green/gold are
   all taken) so it reads as a background envelope, not another foreground
   marker competing with the selection halo or hover ring. Do not remove or
   restyle any existing marker in this task -- the marker-cluster
   legibility finding is addressed by making the *new* indicator visually
   distinct, not by reworking the five that already exist (a materially
   larger task, out of scope here; if the range indicator alone does not
   read as sufficiently distinct once screenshotted, stop and report rather
   than starting to redesign the existing five).

## Inputs and assumptions

- Render-only: confirmed by this session's own scoping pass (citing
  `Combat.fs:33`/`Appraisal.fs:90` as already-public, static, compile-time
  config, and `hostileKnownContacts` as already-derived client state) that
  this task needs **no** `CommandoWar.Sim` change, **no** `Diagnostics.fs`/
  `DiagnosticFrame` extension, and **no** `Canonical.FormatVersion` bump --
  AGENTS.md's diagnostics mandate does not apply, since no new authoritative
  spatial/tactical state is added. If implementation finds this assumption
  wrong (e.g. a genuine need for new canonical state), stop and report
  rather than silently adding it.
- The indicator is a pure distance envelope; it does not account for LOS
  blocking (Sight.fs's elevation rule, the exact mechanism B-073 found
  causing Bridgehead's berm effect). This is a known, deliberate
  simplification -- document it plainly in the code comment and in this
  task's evidence, not as a silently-shipped LOS-accurate claim.
- Whether to also show the indicator for a multi-agent selection (not just
  exactly one) is left to the smallest-scope reading of this task: exactly
  one selected friendly only, matching the existing single-selection
  dev-detail precedent this task reuses. If that reads as insufficient once
  screenshotted, report it rather than silently expanding scope.

## Allowed scope

- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`: a new helper (e.g.
  `rangeSquare`) building the four `lineMarker` segments for a given centre
  cell and radius.
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: wiring the new
  helper into `DrawList` for the selected-friendly and known-hostile cases
  described above.
- `docs/evidence/`: a new screenshot demonstrating the range indicator live
  (both the friendly and hostile cases, ideally in the same frame) and,
  separately, confirming it reads as visually distinct from the existing
  marker cluster.

## Forbidden scope

- No `CommandoWar.Sim`/`CommandoWar.Headless` change of any kind.
- No new `DrawItem.Kind` -- this session's scoping pass confirmed four
  existing `Kind = 2` line segments suffice; if implementation finds this
  wrong, stop and report rather than adding a new primitive silently.
- No restyling of the five existing markers (objective/extraction/fog-of-war/
  hover/leader rings) or the selection halo -- see Central decision 3.
- No LOS-awareness for the range indicator (a materially larger feature --
  would need per-cell `Sight.trace` calls against live terrain every frame,
  not a static radius).
- No toggle key or settings UI.

## Required work

1. Confirm the Chebyshev-square screen-space geometry (Central decision 1)
   against the real `CellToScreen` formula before writing render code --
   compute the four corner cells for a representative centre/radius and
   sanity-check their projected screen positions by hand or with a small
   throwaway script.
2. Implement `RenderShared.rangeSquare` and wire it into `CommandDemoScene`'s
   `DrawList` for both trigger cases (Central decision 2).
3. Run the real Godot 4.7.2 editor and capture a live screenshot (the
   `--screenshot-squad`/`--dev-overlay --screenshot` precedent) showing the
   range indicator around a selected friendly and around a known hostile
   contact, in real Bridgehead content.
4. Re-run all three scenes' `--selfcheck` (`CommandDemo.tscn`,
   `SnapshotDemo.tscn`, `AppraisalDemo.tscn`); this is render-only so hashes
   are expected unchanged -- confirm, do not assume.
5. Update documentation per Documentation updates below.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] A selected friendly agent (exactly one selected) shows a range
      envelope at `CombatConfig.WeaponRange`, screenshot-confirmed. See
      `docs/evidence/task-074-weapon-range-display.png` (agent 5 selected)
      and `docs/ledger/2026-09-22-TASK-074-*.md`.
- [x] Every currently known hostile contact shows the same envelope at its
      `lastKnownCell`, screenshot-confirmed; an un-contacted hostile shows
      none. Same screenshot: rifleman agent 102's fog-of-war ring, reading
      the existing `hostileKnownContacts` map (no new tracking mechanism).
- [x] The envelope's screen-space shape is confirmed, not assumed, to be the
      true Chebyshev-square outline (Central decision 1), not an inscribed
      diamond or a circle. Confirmed both by hand (`CellToScreen` applied to
      a representative centre/radius before implementing) and by direct
      pixel inspection of the live screenshot afterward: the rendered lines
      have screen slope exactly `±0.5` (`TileH/TileW`), matching the true
      corners' prediction -- the rejected edge-midpoint diamond would have
      rendered as horizontal/vertical lines instead. See ledger.
- [x] The envelope is visually distinguishable from every existing marker in
      a live screenshot with several markers present simultaneously.
      Revised from the first-pass `alpha 0.3`/`width 1.5px` to
      `alpha 0.45`/`width 2.0px` after a real screenshot showed the first
      pass too faint to read without artificial contrast boosting -- see
      ledger's Evidence/Deviations sections for the before/after.
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless` file touched. Confirmed by
      `git status`/`git diff --stat`: only `RenderShared.fs`,
      `CommandDemoScene.fs`, and `docs/evidence/`/this task's own files.
- [x] All three Godot `--selfcheck` hashes unchanged (render-only).
      `CommandDemo.tscn` 0x84A25E3559111E9B@90,
      `SnapshotDemo.tscn` 0x6213D672BC36FDB8@20,
      `AppraisalDemo.tscn` 0xA1354EB998FC1B95 -- all re-confirmed after the
      colour/alpha revision, not just before it.
- [x] `dotnet build`/`dotnet test`/`-- corpus`: unaffected counts. Build
      `0 Warning(s)`/`0 Error(s)` (both `CommandoWar.slnx` and
      `CommandoWar.Client.Godot.slnx`); tests `422/422`; corpus `20/20`.
- [x] Required documentation updated (this task's own status/evidence, and
      the new `docs/ledger/` detail file -- `docs/11_BACKLOG.md`,
      `docs/12_PROGRESS_LEDGER.md`'s index, and `PROJECT_STATE.yaml` are
      deliberately deferred to the orchestrating session, per this task's
      own dispatch instructions covering three parallel worktrees).

## Required verification

- `dotnet build CommandoWar.slnx -c Debug`: expect `0 Warning(s)`, `0
  Error(s)`.
- `dotnet test CommandoWar.slnx -c Debug`: expect unaffected count (422/422
  at this task's start -- confirm the exact baseline at implementation
  time).
- `dotnet run --project src/CommandoWar.Headless -- corpus`: expect
  unaffected count (20/20 at this task's start).
- `"$GODOT" --headless --path src/CommandoWar.Client.Godot
  scenes/CommandDemo.tscn -- --selfcheck`: expect the unchanged pinned hash.
- `"$GODOT" --headless --path src/CommandoWar.Client.Godot
  scenes/SnapshotDemo.tscn -- --selfcheck` and `scenes/AppraisalDemo.tscn --
  --selfcheck`: expect unchanged pinned hashes.
- A live windowed screenshot (this environment has a real GPU-backed
  windowed Godot session available, confirmed this session -- use it
  directly, do not assume it is unavailable) via
  `"$GODOT" --path src/CommandoWar.Client.Godot scenes/CommandDemo.tscn --
  --screenshot-squad <n> <path>.png` at a frame where at least one friendly
  is selected and at least one hostile is a known contact.

## Evidence to capture

- Command output for all of the above.
- The chosen colour/alpha values and why they were picked distinct from the
  existing five markers.
- The screenshot(s), with a description of what they show.

## Expected files

- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`.
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`.
- `docs/evidence/task-074-weapon-range-display.png` (or similar).

## Documentation updates

- this task's own status and evidence;
- `docs/11_BACKLOG.md`: new B-074 row, and B-070's row noting its original
  range-display request is now realised;
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
- `PROJECT_STATE.yaml` if the active task/phase/gate changes.

Reconciled centrally by the orchestrating session, not by this task's own
implementing agent, if dispatched alongside other parallel tasks touching
these same shared files.

## Rollback or removal

Render-only, additive: removing the new `RenderShared` helper and its two
call sites in `CommandDemoScene.DrawList` fully reverts this task with no
`CommandoWar.Sim` involvement and no hash re-pin required elsewhere.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-22, "accept all three, commit"), on the
  implementation-plus-independent-re-verification evidence recorded here and
  in `docs/ledger/2026-09-22-TASK-074-weapon-range-display-and-marker-legibility.md`
  (dispatched as one of three parallel implementation agents, each in an
  isolated worktree, and separately rebuilt/retested/re-`--selfcheck`ed by
  the orchestrating session -- including combined with TASK-075/TASK-076's
  own changes in the same tree -- before being reported as ready for
  review).
- Self-verification: implemented and verified in an isolated worktree
  (`agent-ae9c0e68a8c9ffae6`), not committed -- left for the orchestrating
  session to review and combine with TASK-075/TASK-076. See
  `docs/ledger/2026-09-22-TASK-074-weapon-range-display-and-marker-legibility.md`
  for full commands/results/evidence.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
