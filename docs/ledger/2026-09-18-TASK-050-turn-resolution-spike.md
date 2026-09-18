# TASK-050: Disposable turn-resolution harness spike

Owner: Dave (implementing agent session)
Source revision: `main` at TASK-049's implementation commit (uncommitted at
session start; TASK-049 status `review`, awaiting Dave's acceptance).
Environment: `dotnet` `10.0.303` (`global.json`), Windows 11, no Godot
install needed (no client change).

## Request

Dave asked directly, in full detail, for a spike: a console harness in
`CommandoWar.Headless` (alongside `Program.fs`, `AppraisalDemo.fs`) that
queues a handful of `MoveTo`/`Suppress` orders across several agents, steps
the simulation forty or fifty ticks, and prints the resulting snapshot
sequence -- enough to see a "turn" resolve the way an XCOM turn plays out.
Explicitly: no `CommandoWar.Sim` change, no new ADR, read-only against the
simulation contract; not the Godot client or real-time demo scenes; not
action-point budgeting or an Overwatch commitment state (follow-on work,
only if the spike shows the resolved-turn feel worth pursuing).

## Task-selection sequencing note

TASK-049 (backlog B-058, agent movement speed) was still `review` --
implemented and self-verified, Godot `--selfcheck` hashes independently
confirmed `MATCH`, but awaiting Dave's final acceptance -- when this session
began. The project's established discipline up to this point was always
accept-then-select-next (every prior task selection followed the previous
one's acceptance). This task was selected anyway, directly from Dave's own
fully-specified instruction in this session, which functions as an explicit
override of that ordering, not a silent departure from it. Flagged in the
task file itself; `PROJECT_STATE.yaml active_work.selected_task` now names
this task, and TASK-049's own `review` status is untouched, still needing
Dave's acceptance separately.

## Changes

- New `src/CommandoWar.Headless/TurnDemo.fs`: a hand-built `RawScenario` (16x10
  open grid, no authored terrain) with four friendly agents and one hostile,
  validated through `Scenario.validate` and instantiated through
  `World.ofScenario` -- the exact `DemoScenario.fs`/`PathDemo.fs`/`LosDemo.fs`
  precedent (not authoritative content, needs no on-disk format). A hand-typed
  `RecordedCommand[]` queues five orders: tick 1 -- agent 0 `MoveTo (12,0)`,
  agent 1 `MoveTo (9,1)`, agent 2 `Suppress` agent 4 (the hostile); tick 12 --
  agent 3 `MoveTo (9,8)`; tick 25 -- agent 0 reissued `MoveTo (3,9)`.
  `TickCount = 45`.
- `src/CommandoWar.Headless/CommandoWar.Headless.fsproj`: one new
  `<Compile Include="TurnDemo.fs" />` between `PathDemo.fs` and `Corpus.fs`.
- `src/CommandoWar.Headless/Program.fs`: new `cmdTurn` (no args) -- builds
  `TurnDemo`'s initial state and command log, calls the existing
  `DiagnosticRender.runFrames w cmds TurnDemo.TickCount`, and prints every
  frame's `DiagnosticRender.Ascii` in tick order with a `==== tick N ====`
  separator; a new `turn` line in `usage()`; a new `"turn" :: rest -> cmdTurn
  rest` dispatch arm in `main`.

No `CommandoWar.Sim` file touched. No new `Diagnostics`/`Overlay` case (no
new authoritative state is added by this task, so AGENTS.md's
diagnostic-extension rule does not apply). No `Canonical.FormatVersion` or
`ScenarioContent.Version` change.

## Deviation found during implementation

The first `TurnDemo` placement put the suppressing agent (2) at (0,6),
Chebyshev distance 9 from the hostile at (9,4) -- inside `Perception.
SightRange` (10, so the `Suppress` order was accepted as a known contact)
but outside `CombatConfig.WeaponRange` (7, so `Combat`'s automatic
engagement never actually fired). The run was legible but static: zero
combat events for all 45 ticks, the two long `MoveTo` routes staying
`Refused RouteTooExposed` the entire time because the hostile's exposure
contribution was never relieved. Fixed by moving agent 2 to (3,4), Chebyshev
distance 6 from the hostile -- within weapon range -- which produces the
intended narrative (see Verification below). This is exactly the kind of
thing a spike is for: caught by reading the actual printed output, not
assumed.

## Verification

- `dotnet build CommandoWar.slnx -c Release`: `0 Warning(s)`, `0 Error(s)`.
- `dotnet test`: `342/342` passed (unaffected -- confirms no
  `CommandoWar.Sim`/test change).
- `dotnet run --project src/CommandoWar.Headless -- turn`: exit `0`; printed
  46 frames (ticks 0 through 45). Inspected by hand:
  - tick 1: `command-accepted@(12,0)`, `command-accepted@(9,1)`,
    `command-accepted@(3,4)`; agent 0/1's `MoveTo` orders `Refused
    RouteTooExposed threat-agent-4` (every route cell flagged exposed on the
    fully open map); agent 2's `Suppress` `Accepted` (target already a known
    contact from the tick-0 deployment); `commitment-established@(9,4)`
    (agent 2 now `Suppressing` agent 4); two `shot-fired-miss` (real
    engagement, both directions, starting immediately since agent 2 is
    within weapon range).
  - ticks 2-3: real hits both ways (`shot-fired-hit`); `AgentState.
    Suppression` climbs on both agent 2 and agent 4 (100 -> 450 -> 800/1000
    by tick 3); tick 3 also lands `agent-incapacitated@(3,4)` -- agent 2
    critically wounded, its own `Suppress` order becomes `Unable
    critically-wounded` from that tick on.
  - tick 4: agent 0 and agent 1's `MoveTo` orders both flip to `Accepted`
    with no new order issued -- the TASK-037/038 suppression-band-flip
    reappraisal trigger firing once the hostile's `SuppressionBand` latches
    -- and `movement-stepped` events begin (`(0,0)->(1,0)`, `(0,3)->(0,2)`,
    real per-tick sub-cell progress, not an instant jump).
  - tick 6: `agent-incapacitated@(9,4)` -- the hostile itself goes down.
  - ticks 5-22: agents 0, 1, and (from tick 12) 3 all walk their full routes
    cell by cell (`movement-stepped` every tick they are still en route,
    `commitment-completed` on arrival); agent 0 reaches `(12,0)` around tick
    9-10, agent 1 reaches `(9,1)` around tick 10, agent 3 reaches `(9,8)`
    around tick 22.
  - tick 25: agent 0's reissued `MoveTo (3,9)` is queued
    (`command-accepted@(3,9)`) but appraised `Refused RouteTooExposed
    threat-agent-4` immediately -- the hostile is `Incapacitated`, not
    `Dead`, and still counts as a live `HostileTacticalKnowledge` entry for
    route exposure; suppression on it has already decayed to 50/1000 by
    then, but exposure does not key off suppression level once a threat's
    band is not currently latched high, and `Appraisal` does not distinguish
    `Incapacitated` from `Alive` for this purpose. The order stays refused
    for the rest of the run (through tick 45); no further reappraisal
    trigger fires because nothing about the hostile's tactical-knowledge
    entry or suppression band changes again.
  - tick 45 (final): agents 0, 1, 3 all at rest at their resolved
    destinations; agent 2 and the hostile both `Incapacitated (bleeding out,
    18/21 tick(s) respectively)` -- not yet dead (bleed-out duration is
    ~44 ticks from when it starts, around tick 3/6, so both still have
    ~18-21 ticks left at tick 45); ammo consumed on both combatants; ammo
    unaffected on the three agents that never fired.
- `dotnet run --project src/CommandoWar.Headless -- corpus`: `16/16`, `OK`
  (unaffected -- confirms the new module changes no existing behaviour).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package`:
  `FSharp.Core` `10.1.303` only (unaffected).
- `git status --porcelain`: `M src/CommandoWar.Headless/CommandoWar.Headless.fsproj`,
  `M src/CommandoWar.Headless/Program.fs`, `?? src/CommandoWar.Headless/TurnDemo.fs`
  -- matches the task's allowed scope exactly; no `CommandoWar.Sim` or
  Godot-client file touched.

## Evidence

Full 46-frame `cwheadless turn` output inspected in the session's scratchpad
(not committed -- console output, not a golden; no new `Overlay`/
`DiagnosticFrame` case was added, so AGENTS.md's diagnostic-extension rule
does not require one). The narrative summarised above is transcribed
directly from that output, not inferred.

## Observations recorded for Dave, not acted on

1. The resolved-turn feel already exists in the unmodified simulation:
   queuing several orders and stepping N ticks produces refusal,
   engagement, suppression, casualties, reappraisal, and incremental
   sub-cell movement as one coherent sequence, with no real-time pacing
   concept needed. `SimFacade`'s existing `QueueMove`/`Step()` separation in
   the Godot client is already shaped for a client that wants to batch
   orders and animate the resulting tick sequence.
2. An `Incapacitated` (not `Dead`) known contact still fully counts as a
   live threat for `Appraisal`'s route-exposure model -- confirmed by direct
   observation (tick 25's stayed-refused reissue above), not assumed. May be
   intentional (a downed-but-not-confirmed-dead threat is still a risk) or a
   gap worth a deliberate decision before any exposure-driven Overwatch
   design leans on it. Not fixed -- outside this task's read-only scope.
3. Action-point budgeting and an Overwatch commitment state remain
   unscoped, deliberately not started here.

## Documents updated

- `tasks/TASK-050-TURN-RESOLUTION-SPIKE.md` (created, `Outcome` filled in).
- `docs/11_BACKLOG.md` (new B-060 row, `done`).
- `docs/12_PROGRESS_LEDGER.md` (this detail file; index row added).
- `PROJECT_STATE.yaml` (`active_work` updated).

## Review

- Reviewer: Dave.
- Accepted: pending.
