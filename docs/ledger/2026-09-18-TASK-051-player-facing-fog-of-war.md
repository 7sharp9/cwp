# TASK-051: Player-facing fog of war

Owner: Dave (implementing agent session)
Source revision: `main`, after TASK-049/TASK-050's acceptance this session.
Environment: `dotnet` `10.0.303`, Godot `4.7.2.stable.mono` (real editor,
`C:\Users\Dave\Documents\GitHub\Godot_v4.7.2-stable_mono_win64`), Windows 11.

## Selection

Chosen via `AskUserQuestion` over the smaller client-polish rows
(B-053/B-054) and continuing straight into an AP-budget layer, as the
largest ready item, flagged by Dave multiple times (raised on TASK-043
review 2026-09-17).

## Central decisions

Three `AskUserQuestion` rounds before drafting, Dave choosing the
recommended default each time:

1. Scope: `CommandDemoScene` only, not `SnapshotDemo.tscn`.
2. Ghost visual: a hollow outlined ring plus a small text label, not a
   translucent filled circle.
3. Confidence fade: yes, the ring's opacity scales with `Contact.
   Confidence`.

## Investigation before drafting

Read `src/CommandoWar.Sim/Snapshot.fs` (`AgentSnapshot`/`RenderSnapshot`)
expecting a new `RenderSnapshot` field would be needed to expose
`WorldState.TacticalKnowledge` to the client. Found instead that
`CommandDemoScene.fs` already builds a `Diagnostics.frameOf` frame every
tick regardless of the `F1` dev-overlay toggle (`devFrame`, used
unconditionally since TASK-046 for the `AgentVitals` overlay lookup that
drives player-facing wound/death rendering) -- and that frame's
`KnownContact` overlay already carries exactly `WorldState.
TacticalKnowledge`'s contacts (`LastKnownCell`, `Confidence`,
`LastSeenTick`), built by `Diagnostics.knownContactOverlays`. This meant the
whole task could be implemented without touching `CommandoWar.Sim` at all --
confirmed against the actual overlay definitions in `Diagnostics.fs` before
drafting the task file's Allowed scope, not assumed.

Also read `src/CommandoWar.Sim/Perception.fs`'s `PerceptionConfig` for the
exact confidence-decay mechanics: `ConfidenceFull = 1000` while seen this
tick or within `StaleAfter` (20 ticks); drops one band to `750`
(`ConfidenceBandDrop`) after that; the contact is removed entirely
(`ContactExpired`) after `ExpireAfter` (60 ticks unseen). This confirmed
"currently visible" can be read directly off `Contact.LastSeenTick =
devFrame.Tick`, with no need for a separate visibility flag.

## Changes

- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`:
  - New `hostileKnownContacts: Map<int, Cell * int * int64>` built from
    `devFrame.Overlays`'s `KnownContact` cases, keyed by contact `AgentId`.
  - `agentItems`'s vitals-rendering logic (the `Dead`/`Incapacitated`/
    `Alive` match, previously inline) extracted into a new `renderVitals`
    helper, unchanged in behaviour.
  - `agentItems`'s `Array.collect` now branches: `if devOverlay then
    renderVitals a else match a.Side, Map.tryFind ... with | Hostile, None
    -> [||] | Hostile, Some(cell, confidence, lastSeenTick) when
    lastSeenTick < devFrame.Tick -> <ghost ring + "?" label> | _ ->
    renderVitals a`.
  - Corrected the `F1` dev overlay's own `knownContacts` comment (it
    previously said "this demo has no fog-of-war", now stale).
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`: new `cellRing`
  helper, a `Kind = 5` `DrawItem` constructor (the `cellMarker`/`cellLabel`
  precedent).
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`: `DrawItem`'s doc
  comment extended to describe `Kind = 5`.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: new `case 5` in
  `_Draw`'s `Kind` switch -- `DrawArc(pos - new Vector2(0, TileH * 0.5f),
  item.Radius, 0, Mathf.Tau, 24, color, 2.5f)`, unfilled, at the same
  vertical offset an agent figure draws at.
- `src/CommandoWar.Client.Godot/README.md`: new section.
- `docs/evidence/task-051-fog-of-war.png`: new screenshot.

No `CommandoWar.Sim`/`CommandoWar.Headless` file touched. No `SnapshotDemo.
tscn`/`DemoRenderScene.fs` change (Central decision 1).

## Deviation found during implementation, fixed before it shipped

The first draft applied the fog-of-war branch unconditionally in
`agentItems`, including while `devOverlay` (the `F1` toggle) is on. This
silently broke TASK-043's own developer tool: `docs/06` section 11's
"known-versus-authoritative" comparison exists precisely so a developer can
see the real agent's true position *and* the squad's separate known-contact
belief about it side by side, to catch exactly the kind of divergence this
task's own fog now introduces for the player. With the first draft, turning
on `F1` would have hidden or ghosted a hostile the same way the normal view
does, defeating the purpose of the debugging overlay outright.

Fixed by gating the fog-of-war branch behind `if devOverlay then
renderVitals a else <fog logic>`: with the overlay on, every agent renders
exactly as before TASK-051 (ground truth, unconditional); fog only applies
to the normal player view. `renderVitals` was factored out as a named
helper specifically so both branches could share it without duplicating the
`Dead`/`Incapacitated`/`Alive` rendering. Caught by re-reading the existing
dev-overlay code and its own doc comment (`docs/06` section 11 reference)
before finishing the change, not by a test failure -- there is no automated
coverage of `DrawList()`'s actual rendering decisions, only of the
authoritative simulation, so this was a design-review catch, not a
regression-test catch.

## Verification

- `dotnet build CommandoWar.slnx -c Release`: `0 Warning(s)`, `0 Error(s)`.
- `dotnet test`: `342/342` passed (unaffected -- confirms no
  `CommandoWar.Sim`/`CommandoWar.Sim.Tests` change).
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: `0 Warning(s)`, `0 Error(s)`.
- A temporary `dotnet fsi` scratch probe (two files, both removed after use
  -- `fog_probe.fsx` and `fog_probe_devoverlay.fsx` in the session
  scratchpad, never committed) referenced the built `CommandoWar.Sim.dll`,
  `cwheadless.dll`, and `CommandoWar.Client.Godot.Core.dll` directly and
  drove `CommandDemoScene()` as a plain `IClientScene` -- no Godot process
  needed, since the type carries no `Godot.*` reference (ADR-0004). Each
  probe tick called `Update(1/20.0)` exactly once (an exact-tick step, no
  real-time accumulator rounding) and inspected `DrawList()`'s returned
  `DrawItem[]`.
  - First probe: selected agent 0, issued `MoveTo (3,7)` (within
    `Perception.SightRange` (10) of the hostile at `(11,7)` but outside
    `CombatConfig.WeaponRange` (7), so it makes contact without either side
    firing), then retreated it to `(0,0)`. Observed, printing only on
    change:
    - ticks 0-20: `live=2@(0,0),(0,1) ghost=0` -- hostile fully hidden,
      never contacted.
    - tick 21: `live=3@(0,1),(3,7),(11,7) ghost=0` -- agent 0 arrives at
      `(3,7)`, contact established, hostile drawn live at its true
      position `(11,7)`.
    - tick 63 (after the retreat order is issued, delivered, and takes
      effect): `live=2@(0,1),(3,6) ghost=1@(11,7)a=0.90` -- hostile no
      longer drawn as a live figure; a `Kind = 5` ghost ring appears at its
      last-known cell `(11,7)`, `alpha = 0.90` (`0.9 * 1000 / 1000`).
    - tick 82: `ghost=1@(11,7)a=0.68` -- alpha drops to `0.675` (printed
      rounded to `0.68`), exactly `PerceptionConfig.StaleAfter` (20) ticks
      after the contact was last seen (tick 62 was the last tick the
      hostile was actually visible, per the arrival/retreat timing above);
      `0.9 * 750 / 1000 = 0.675`, matching `ConfidenceBandDrop` exactly.
    - tick 122: `live=2@(0,0),(0,1) ghost=0` -- the ghost disappears
      entirely, exactly `PerceptionConfig.ExpireAfter` (60) ticks after
      last seen (62 + 60 = 122); the hostile is fully hidden again, the
      contact having expired out of `WorldState.TacticalKnowledge`
      entirely.
  - Second probe: called `OnToggleDevOverlay()` immediately after `Ready()`
    (before any contact), then stepped 10 ticks. Confirmed the never-
    contacted hostile at `(11,7)` still draws as a full-opacity `Kind = 1`
    figure from tick 1 -- the `F1` ground-truth bypass works as intended.
- Godot editor `--selfcheck`, headless, through the real 4.7.2 editor (this
  machine has it installed):
  - `SnapshotDemo.tscn`: `MATCH 0xF422ACB8D5A86FF0` at tick 20, exit 0
    (unchanged from TASK-049's pin).
  - `CommandDemo.tscn`: `MATCH 0x00D3D471EF7354BC` at tick 20, exit 0
    (unchanged).
  - `AppraisalDemo.tscn`: `MATCH 0x194805888CBE240D` (format 11), agent 0
    Refused / agent 1 Accepted, exit 0 (unchanged; steps the committed
    `exposed-approach` corpus entry, not `DemoScenario`, and this scene was
    never in scope).
  All three unaffected, exactly as expected for a render-only change with
  no `CommandoWar.Sim` modification.
- Windowed screenshot: `"$GODOT" --path . scenes/CommandDemo.tscn --
  --screenshot docs/evidence/task-051-fog-of-war.png`, using the existing
  scripted screenshot-priming sequence (`FSharpSceneHost.cs`, unchanged --
  select agent 0, pause, preview/issue a move for agent 1, select it, arm
  `Hold`, hover a preview). That priming never approaches the hostile at
  `(11,7)`, so the captured frame shows only the two friendly agents and
  the armed-`Hold` preview outline -- no red hostile figure anywhere in
  frame, unlike every prior task's screenshot of this same scene and
  scripted sequence. Inspected the saved PNG directly to confirm.
- `git status --porcelain`: matches the task's allowed scope exactly --
  `CommandDemoScene.fs`, `RenderShared.fs`, `IClientScene.fs`,
  `FSharpSceneHost.cs`, `README.md` modified; `docs/evidence/
  task-051-fog-of-war.png` new; no `CommandoWar.Sim`/`CommandoWar.Headless`
  file touched.

## Documents updated

- `tasks/TASK-051-PLAYER-FACING-FOG-OF-WAR.md` (created, `Outcome` filled
  in).
- `docs/11_BACKLOG.md` (B-055 row: `proposed -> done`).
- `src/CommandoWar.Client.Godot/README.md` (new section).
- `docs/12_PROGRESS_LEDGER.md` (this detail file; index row added).
- `PROJECT_STATE.yaml` (`active_work` updated).

## Review

- Reviewer: Dave.
- Accepted: pending.
