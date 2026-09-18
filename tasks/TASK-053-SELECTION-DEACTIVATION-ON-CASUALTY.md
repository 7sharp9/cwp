# TASK-053: Selection/order-mode deactivation on casualty

Status: done (implemented and self-verified 2026-09-18; Godot `--selfcheck`
independently confirmed `MATCH` through the real editor 2026-09-18; accepted
by Dave 2026-09-18)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises backlog B-061
Size: S

## Outcome (2026-09-18)

Second `AskUserQuestion` round overrode the first's initial "clear
`selected`" default. Dave's own framing on the third question ("should
`friendlyAt`/`OnClick` refuse to re-select a non-`Alive` agent"): "you
should be able to select to view status when that is an option, but all
action commands would be disabled, e.g. you could only view status." A
follow-up round confirmed the consequence directly: `selected` never
auto-clears from a vitals change; `orderMode` auto-disarms to `0` the
instant its selected agent is not `Alive`; the hover route preview is
suppressed while a non-`Alive` agent is selected.

`CommandDemoScene.fs` gained a `vitalsOf (id: AgentId) : VitalStatus`
helper, extracted from `renderVitals`'s existing `devFrame.Overlays`/
`AgentVitals` lookup (`AgentSnapshot` itself carries no vitals field --
docs/03 section 12's values-only rule) so `renderVitals` and the new
gating share one source. A new `syncOrderModeToSelection` disarms
`orderMode` to `0` when `selected` points at a non-`Alive` agent; called
after every `stepOnce` in `Update` (vitals only ever change there) and
after a selection change in `OnClick` (selecting an already-non-`Alive`
friendly directly must not carry over a stale armed icon). `OnClick`'s
order-issuing match arm gained `Casualty.isAlive (vitalsOf agentId)` to
its guard. `OnHover`'s `previewPath` now only computes when the selected
agent is `Alive`. `friendlyAt` itself is unchanged: TASK-053's central
decision confirmed its existing `Side = Friendly`-only filter (allowing a
non-`Alive` friendly to be selected) is the *intended* status-view
mechanism, not the "second gap" the backlog row originally described it
as.

Verified with a temporary `dotnet fsi` scratch probe (the
TASK-042/051/052 precedent, removed after use) driving `CommandDemoScene`
through real combat in `DemoScenario`: moved friendly agent 0 to (9,6),
within `CombatConfig.WeaponRange` and clear line of sight of the hostile
at (11,7), so Combat's automatic symmetric engagement (no order needed on
either side) produced a genuine wound sequence. Confirmed at
Incapacitated (tick 24): `OrderMode()` auto-disarmed to `0`; re-arming an
icon via `OnOrderModeClick` still succeeds (arming itself is not gated --
a deliberate choice, since `OnClick`'s own guard independently blocks any
resulting order regardless of `orderMode`'s value); a click attempting to
issue that armed order left `orderMode` unconsumed, proving the
order-issuing branch was never entered; the hover route preview (a
distinct `Kind = 1`, radius-5 item, checked separately from the
radius-34 selection halo which legitimately keeps rendering) was absent.
Confirmed at Dead (tick 83, deterministic 60-tick bleed-out after
Incapacitated per `CasualtyConfig.BleedOutTicks`): `selected` still read
`agent 0` (`HudText()` = `"...selected=agent 0..."`, never `"none"`);
`OrderMode()` stayed `0`; hover preview still absent. Re-selecting a
different, `Alive` friendly (agent 1) immediately restored both normal
order-issuing and hover-preview behaviour.

`dotnet build CommandoWar.slnx -c Release` and `CommandoWar.Client.Godot.slnx
-c Debug`: `0/0`. `dotnet test`: `342/342` (unaffected -- no
`CommandoWar.Sim`/`CommandoWar.Headless` file touched). All three scenes'
`--selfcheck` hashes independently re-run through the real Godot 4.7.2
editor and confirmed `MATCH` (unaffected, as expected for a
presentation-only change): `SnapshotDemo.tscn` `0xF422ACB8D5A86FF0`,
`CommandDemo.tscn` `0x00D3D471EF7354BC`, `AppraisalDemo.tscn`
`0x194805888CBE240D`.

Full detail: `docs/ledger/2026-09-18-TASK-053-selection-deactivation-on-casualty.md`.

## Objective

`CommandDemoScene.fs`'s selection and order-mode UI stops offering actions
against a `selected` agent the instant it is no longer `Alive` (`Dead` or
`Incapacitated`), while still allowing the player to keep it selected to
view its status. Selection itself is not force-cleared.

## Why this task exists

Raised by Dave on accepting TASK-052 (2026-09-18): "the cursor which has
move active or whatever should be deactivated" when the selected agent is
removed by death. Concretely: an armed Hold/Assault/Withdraw icon
(`orderMode`), the hover route preview, and a follow-up click's order-issue
path can all keep acting as if the selected agent could still receive
orders, even though `Simulation.step` already silently no-ops any order
addressed to a non-`Alive` recipient (TASK-045's `navigationAndMovement`/
`Combat` skip). Nothing currently tells the player their selection can no
longer act. Backlog row B-061.

## Central decisions (confirmed with Dave 2026-09-18 via `AskUserQuestion`,
two rounds)

1. **Trigger**: both `Dead` and `Incapacitated` disable action, not `Dead`
   only — an `Incapacitated` agent already cannot act per TASK-045.
2. **Selection itself**: does **not** auto-clear on a vitals change or on
   selecting a non-`Alive` friendly directly. Dave's own framing: "you
   should be able to select to view status when that is an option, but all
   action commands would be disabled, e.g. you could only view status."
   `selected` stays wherever the player put it (own click, or another
   agent's own click) regardless of `Alive`/`Incapacitated`/`Dead`.
3. **`orderMode`**: auto-disarms to `0` (`MoveTo`) the instant the agent it
   is armed against is not `Alive` — the TASK-048 "ability consumed"
   idiom, applied to "no longer a valid actor" instead of "order issued."
4. **Hover route preview**: suppressed while the selected agent is not
   `Alive` — previewing a route implies a click there would move it, which
   is no longer true.
5. **`friendlyAt`/`OnClick`'s selection path**: unchanged. It already
   selects any `Side = Friendly` cell regardless of vitals — per decision 2
   this is now confirmed *intended* (status viewing), not the "gap" the
   backlog row originally flagged it as.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` (`selected`,
  `orderMode`, `friendlyAt`, `agentPosition`, `renderVitals`'s
  `devFrame.Overlays`/`AgentVitals` lookup, `OnClick`, `OnHover`, `Update`)
- `src/CommandoWar.Sim/Casualty.fs` (`VitalStatus`, `Casualty.isAlive`)
- `src/CommandoWar.Sim/Diagnostics.fs` (`AgentVitals` overlay — the only
  source of a live agent's `VitalStatus` on the client side; `AgentSnapshot`
  itself carries no vitals field, docs/03 section 12's values-only rule)

## Dependencies

- B-031 (TASK-045, `AgentState.Vitals`) — done.
- B-026 (TASK-040, selection/hover input) — done.
- B-059 (TASK-048, `orderMode`) — done.

## Allowed scope

- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: a small
  `vitalsOf`/equivalent helper reused by `renderVitals` and the new gating;
  gating in `OnClick`'s order-issuing branch, `OnHover`'s `previewPath`,
  and an `orderMode` auto-disarm check driven from `Update`/`OnClick`'s
  selection branch.
- `docs/11_BACKLOG.md` (B-061 row), `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- Any change to `src/CommandoWar.Sim`/`src/CommandoWar.Headless`
  (presentation-only, the TASK-046/051/052 precedent — `AgentVitals` already
  exposes everything needed).
- Clearing `selected` on a vitals change, or blocking selection of a
  non-`Alive` friendly (Central decision 2).
- Any change to the selection halo, fog-of-war, or vitals rendering
  (`renderVitals`'s `Dead`/`Incapacitated`/`Alive` draw branches) beyond the
  `vitalsOf` extraction needed to avoid duplicating the same lookup.
- Any new client-facing HUD text describing casualty status beyond what
  already renders (`renderVitals`'s existing "down" badge/dead cross).

## Required work

1. Extract a `vitalsOf (id: AgentId) : VitalStatus` helper from
   `renderVitals`'s existing `devFrame.Overlays` / `AgentVitals` lookup (same
   default-to-`Alive` fallback), so both `renderVitals` and the new gating
   share one lookup.
2. Gate `OnClick`'s order-issuing match arm on
   `Casualty.isAlive (vitalsOf agentId)`.
3. Gate `OnHover`'s `previewPath` computation on the selected agent's
   liveness.
4. Add an `orderMode` auto-disarm check (`orderMode <- 0` when `selected`
   points at a non-`Alive` agent) invoked after each `stepOnce` in `Update`
   and after a selection change in `OnClick`.
5. Verify with a temporary `dotnet fsi` scratch probe (the TASK-042/051/052
   precedent, removed after use): drive a scripted scenario where a
   selected agent takes enough hits to go `Incapacitated` then `Dead`,
   confirming the order-issue path is blocked, the hover preview stops, and
   an armed `orderMode` disarms — at each transition, and confirming
   `selected` itself never clears.
6. Confirm both scenes' `--selfcheck` hashes unaffected through the real
   Godot editor (no `Simulation.step`/canonical change, so no hash move is
   expected — confirm rather than assume).
7. Update backlog/ledger/state.

## Acceptance criteria

- [x] Selecting a `Dead` or `Incapacitated` friendly's own cell still
      selects it (status-view mode); `selected` never auto-clears from a
      vitals change alone.
- [x] A click elsewhere while a non-`Alive` agent is selected issues no
      order (the sim is never handed a command addressed to a non-`Alive`
      recipient from this path).
- [x] Hovering a target cell while a non-`Alive` agent is selected draws no
      route preview.
- [x] An armed Hold/Assault/Withdraw icon (`orderMode <> 0`) disarms to `0`
      the instant its selected agent stops being `Alive`, whether that
      happens mid-tick (the agent dies while armed) or by selecting an
      already-non-`Alive` agent directly.
- [x] Re-selecting a different, `Alive` friendly restores normal
      order-issuing and hover-preview behaviour immediately.
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless` change; both scenes'
      `--selfcheck` hashes unaffected.
- [x] `dotnet build`/`dotnet test` unaffected; Godot client builds.
- [x] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet test`: `342/342` (unaffected).
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: `0/0`.
- Two temporary `dotnet fsi` scratch probes (removed after use), driving
  real combat in `DemoScenario` (friendly agent 0 moved to `(9,6)`, within
  `CombatConfig.WeaponRange` and LOS of the hostile at `(11,7)`) until the
  selected agent went `Incapacitated` (tick 24) then `Dead` (tick 83):
  order-issue blocked (armed `orderMode` left unconsumed by a click);
  hover preview suppressed at both states; `orderMode` armed *before* the
  transition (mid-route, while `Alive`) disarmed in the exact same tick
  the agent went `Incapacitated`; `selected` still read `agent 0` at
  `Dead`; re-selecting `Alive` agent 1 immediately restored normal
  behaviour.
- Godot editor `--selfcheck` for `SnapshotDemo.tscn`/`CommandDemo.tscn`/
  `AppraisalDemo.tscn` through the real Godot 4.7.2 editor: all three
  `MATCH`, hashes unaffected (see Outcome).
- `git status --porcelain`: matches this task's allowed scope.

## Evidence to capture

- Scratch-probe output showing each transition (order-issue blocked,
  hover-preview suppressed, `orderMode` disarmed same-tick as the vitals
  change, `selected` still set) -- see Outcome section above.
- `--selfcheck` hashes for all three scenes, unchanged from TASK-052's
  pins.

## Expected files

- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`.
- `tasks/TASK-053-SELECTION-DEACTIVATION-ON-CASUALTY.md`.
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md` (+ new
  `docs/ledger/` detail file), `PROJECT_STATE.yaml`.

## Documentation updates

- This task file's Outcome section.
- `docs/11_BACKLOG.md` (B-061 row: `proposed -> done`).
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Fully additive gating over existing client-only state (`orderMode`,
`previewPath`, `OnClick`'s order-issue branch); no `CommandoWar.Sim`/
`CommandoWar.Headless` change, no hash format change, no new `AgentSnapshot`
field. Revertible with `git revert` in one step; both scenes' `--selfcheck`
hashes are expected unaffected either way, so nothing needs re-pinning on
rollback.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-18). No changes requested.
