# TASK-053: Selection/order-mode deactivation on casualty

Owner: Dave (implementing agent session)
Source revision: `main`, after TASK-052's acceptance this session.
Environment: `dotnet` `10.0.303`, Godot `4.7.2.stable.mono` (real editor),
Windows 11.

## Selection

Dave asked directly for backlog B-061 to be scoped and implemented as the
next task, naming the nit he raised on accepting TASK-052 -- no
`AskUserQuestion` needed for the selection itself, the TASK-048/052
precedent for a task Dave names explicitly.

## Central decisions (two `AskUserQuestion` rounds before drafting)

Round 1 (three questions): both `Dead` and `Incapacitated` should disable
action, not `Dead` only (Dave's choice, the recommended default);
`orderMode` should reset to `0` alongside any clear (recommended, chosen);
a third question asked whether `friendlyAt`/`OnClick` should refuse to
(re-)select a non-`Alive` agent at the source. Dave's answer to the third
question did not match either offered option -- free text: "you should be
able to select to view status when that is an option, but all action
commands would be disabled, e.g. you could only view status."

That answer implicitly contradicts round 1's first question (if a
dead/incapacitated agent must stay selectable for status viewing, then
`selected` cannot also auto-clear the instant it goes non-`Alive`) --
flagged directly rather than guessed past, and a **second** round asked
three follow-ups to pin down the reconciled design: `selected` never
auto-clears from a vitals change at all, superseding round 1's first
answer (Dave's choice, recommended); `orderMode` still auto-disarms to `0`
the instant the selected agent is not `Alive` (recommended, chosen); the
hover route preview is suppressed while a non-`Alive` agent is selected
(recommended, chosen).

Net effect: `friendlyAt`'s existing `Side = Friendly`-only filter (already
allowing a click to select a non-`Alive` friendly's own cell) needed no
change at all -- round 1's third question had framed this as the "second
gap" the backlog row named, but the resolved design confirms it as the
*intended* status-view mechanism instead.

## Investigation before drafting

Confirmed `AgentSnapshot` carries no `Vitals` field (`Domain.fs`; docs/03
section 12's values-only rule) -- the only client-side source of an
agent's live `VitalStatus` is `devFrame`'s always-on `AgentVitals` overlay,
the exact lookup `renderVitals` (`DrawList`) already performs inline.
Confirmed `Simulation.output`'s `RenderSnapshot` construction (`Agents =
s.Agents |> Array.map ...`) is unconditional -- a `Dead` agent is never
removed from `state.Agents`/`AgentSnapshot[]`, so `agentPosition`/
`friendlyAt`/`vitalsOf` all keep finding it after death, which is what
makes status-view mode possible with zero `CommandoWar.Sim` change.
Confirmed `Casualty.isAlive (vitals: VitalStatus) : bool` (`Casualty.fs`)
is the exact predicate already used sim-side for the identical "can this
agent act" question -- reused directly rather than re-deriving the
`Alive`/`Incapacitated`/`Dead` match by hand, the `Pathfinding.find`/
`Sight.trace` precedent for the client freely calling a pure `Sim`
function.

## Changes

- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`:
  - New `vitalsOf (id: AgentId) : VitalStatus`, extracted from
    `renderVitals`'s previously-inline `devFrame.Overlays`/`AgentVitals`
    lookup (same default-to-`Alive` fallback); `renderVitals` now calls it
    instead of duplicating the lookup.
  - New `syncOrderModeToSelection ()`: sets `orderMode <- 0` when
    `selected` points at an agent for which `Casualty.isAlive (vitalsOf
    id)` is `false`. Called from `Update` right after the tick catch-up
    loop (vitals only ever change from `stepOnce`) and from `OnClick`'s
    `friendlyAt`-hit branch right after `selected <- Some a.Id` (a direct
    selection of an already-non-`Alive` friendly must not carry over a
    stale armed icon).
  - `OnClick`'s order-issuing match arm (`Some agentId when ... -> ...`)
    gained `&& Casualty.isAlive (vitalsOf agentId)` to its guard -- a
    click while the selected agent is not `Alive` now falls through to
    the no-op `| _ -> ()` arm, leaving any armed `orderMode` unconsumed
    (visibly still armed, since arming itself is intentionally not
    gated -- see Verification).
  - `OnHover`'s `previewPath` computation now matches on `selected` first
    (`Some id when Casualty.isAlive (vitalsOf id) -> ...`), falling back
    to `None` otherwise, instead of unconditionally binding through
    `agentPosition`.

No `CommandoWar.Sim`/`CommandoWar.Headless` file touched. No
`IClientScene.fs` change (no new interface member needed).

## Verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet test`: `342/342` (unaffected).
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: `0/0`.
- A temporary `dotnet fsi` scratch probe (`task053_probe.fsx`, session
  scratchpad, removed after use) drove `CommandDemoScene` directly (no
  Godot process): selected friendly agent 0, issued a plain `MoveTo(9,6)`
  -- within `CombatConfig.WeaponRange` (7, Chebyshev) and clear line of
  sight of the hostile at `(11,7)` -- so Combat's automatic symmetric
  engagement (fires whenever in range + LOS, no order needed on either
  side) produced a genuine wound sequence with no scripted combat.
  Confirmed at tick 24 (`Incapacitated`, detected via the `renderVitals`
  "down" `Kind = 3` text badge appearing in `DrawList()`):
  - `OrderMode()` had already auto-disarmed to `0`.
  - Re-arming via `OnOrderModeClick(2)` (Assault) still succeeds --
    arming is deliberately not gated, since `OnClick`'s own guard
    independently blocks any order that would result regardless of
    `orderMode`'s value.
  - A subsequent `OnHover` computed no preview dots (checked specifically
    for the `Kind = 1`, radius-5, alpha-0.35 preview-dot signature from
    `routeDots`, distinguished from the structurally similar-coloured but
    radius-34 selection halo, which legitimately keeps rendering per the
    "selection never clears" decision -- an initial probe draft's looser
    colour-only filter false-positived on the halo, caught by dumping the
    actual matching `DrawItem`s and fixed before trusting the result).
  - A click at `(5, 6)` attempting to issue the still-armed Assault order
    left `OrderMode()` at `2` (unconsumed) -- proof the order-issuing
    branch was never entered, not just that no visible order appeared.
  - `HudText()` still read `selected=agent 0` throughout (never `none`).
  At tick 83 (`Dead`, two `Kind = 2` dead-cross items in `DrawList()`,
  `CasualtyConfig.BleedOutTicks` = 60 ticks after Incapacitated, exactly
  as `Casualty.tickBleedOut`'s deterministic countdown predicts):
  `selected` still `agent 0`; `OrderMode()` still `0`; hover preview still
  absent. Re-selecting `Alive` friendly agent 1 (`(0,1)`) immediately
  restored both a working hover preview and a working `MoveTo` issue.
- A second temporary probe (`task053_probe2.fsx`, removed after use)
  specifically isolated the "agent dies while armed" acceptance criterion
  (the first probe only armed an icon *after* detecting `Incapacitated`):
  armed `Hold` (`OnOrderModeClick(1)`) while agent 0 was still `Alive` and
  mid-route, then stepped until `Incapacitated`. `OrderMode()` first read
  `0` at tick 24, the identical tick `Incapacitated` was first observed --
  the disarm is genuinely same-tick, not a one-tick-late catch-up.
- Godot editor `--selfcheck`, headless, through the real 4.7.2 editor (all
  three scenes, since this touches shared `CommandDemoScene.fs` code paths
  even though `CommandDemoDrive.runScriptedSelfCheck`'s own scripted
  sequence never triggers a casualty):
  - `SnapshotDemo.tscn`: `MATCH 0xF422ACB8D5A86FF0`, exit 0.
  - `CommandDemo.tscn`: `MATCH 0x00D3D471EF7354BC`, exit 0.
  - `AppraisalDemo.tscn`: `MATCH 0x194805888CBE240D` (format 11), exit 0.
  All three unchanged from TASK-052's pins, as expected -- no
  `Simulation.step`/canonical-state change, and the scripted self-check
  sequence itself never reaches a casualty.
- `git status --porcelain`: matches the task's allowed scope --
  `CommandDemoScene.fs` further modified (already modified going into this
  session from TASK-052's own pending state); no
  `CommandoWar.Sim`/`CommandoWar.Headless`/`IClientScene.fs` file touched.

## Documents updated

- `tasks/TASK-053-SELECTION-DEACTIVATION-ON-CASUALTY.md` (created,
  `Outcome` filled in).
- `docs/11_BACKLOG.md` (B-061 row: `proposed -> done`).
- `docs/12_PROGRESS_LEDGER.md` (this detail file; index row added).
- `PROJECT_STATE.yaml` (`active_work` updated).

## Review

- Reviewer: Dave.
- Accepted: yes (2026-09-18). No changes requested.
