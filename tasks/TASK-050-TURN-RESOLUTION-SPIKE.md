# TASK-050: Disposable turn-resolution harness spike

Status: done (implemented and self-verified 2026-09-18)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); decision-support spike, not
tied to an existing backlog dependency chain -- new backlog row B-060
records it as disposable, exploratory tooling (the TASK-004/005
framework-spike precedent).

## Outcome (2026-09-18)

Implemented as scoped, no deviations. New `src/CommandoWar.Headless/TurnDemo.fs`
(the `DemoScenario.fs`/`PathDemo.fs`/`LosDemo.fs` precedent: a hand-built
`RawScenario`, validated through `Scenario.validate`, instantiated through
`World.ofScenario`; not authoritative content, needs no on-disk format) --
five friendly-vs-hostile agents on a 16x10 open grid, a hand-typed
`RecordedCommand[]` of five `MoveTo`/`Suppress` orders spread across ticks 1,
12, and 25, run for `TickCount = 45`. New `cwheadless turn` subcommand in
`Program.fs`: builds `TurnDemo`'s initial state and command log, calls the
existing `DiagnosticRender.runFrames`, and prints every tick's
`DiagnosticRender.Ascii` frame in sequence with a `tick N` separator --
exactly the same read-only mechanism `render demo`/`render los`/`render path`
already use, just looped over the full run instead of one `--tick`. No
`CommandoWar.Sim` change; no new `Diagnostics`/`Overlay` case (no new
authoritative state is added, so AGENTS.md's diagnostic-extension rule does
not apply); no `Canonical.FormatVersion`/`ScenarioContent.Version` change.

The first agent placement (a suppressing agent held out of `CombatConfig.
WeaponRange` of the hostile) produced a dead-end run: the `Suppress` order
was accepted but nothing ever fired, so the hostile's exposure was never
relieved and the two long `MoveTo` routes stayed `Refused RouteTooExposed`
for all 45 ticks -- a legible but uninteresting outcome. Moved the
suppressing agent to (3,4), within weapon range of the hostile at (9,4), and
re-ran: this produces the intended turn-by-tick narrative end to end --
`Suppress` accepted immediately (target already a known contact from the
tick-0 deployment, within `Perception.SightRange`); real automatic
engagement (`shot-fired-hit`/`-miss` events, symmetric) starting tick 1;
`AgentState.Suppression` builds on the hostile: both the suppressing agent
and the hostile take real hits, agent 2 is `Incapacitated` (`critically
wounded`) at tick 3, the hostile is `Incapacitated` at tick 6; once the
hostile's exposure is no longer live, the reappraisal mechanism (TASK-037/038's
own suppression-band-flip trigger) reappraises agents 0 and 1's previously
`Refused` orders to `Accepted` without a new order, and `MoveTo` proceeds cell
by cell (`movement-stepped` events) exactly the way `AgentState.Progress`'s
sub-cell mechanism always has; agent 3's tick-12 order is accepted and
resolves the same way; agent 0's tick-25 reissued `MoveTo` stays `Refused
RouteTooExposed` for the rest of the run -- the incapacitated (not yet dead)
hostile still counts as a live known threat for route-exposure purposes, an
observed engine behaviour worth flagging to Dave (see Observations below),
not something this read-only spike touched. By tick 45 the run settles into
a stable, legible final state: three of five orders fully resolved (agents 0,
1, 3 all at rest at their destinations), one order permanently refused, two
casualties bleeding out but not yet dead (bleed-out has ~44 ticks; both still
short of it at tick 45).

`dotnet build CommandoWar.slnx -c Release`: `0/0`. `dotnet test`: `342/342`
(unaffected -- no `CommandoWar.Sim`/`CommandoWar.Sim.Tests` change).
`cwheadless corpus`: `16/16` (unaffected). `dotnet list
src/CommandoWar.Sim/CommandoWar.Sim.fsproj package`: `FSharp.Core` only.
`git status --porcelain`: `src/CommandoWar.Headless/{CommandoWar.Headless.fsproj,Program.fs}`
modified, `src/CommandoWar.Headless/TurnDemo.fs` new -- matches this task's
scope exactly. `cwheadless turn` run directly and its full 46-frame output
(ticks 0-45) inspected by hand to confirm the narrative above.

### Observations for Dave (not acted on; read-only spike)

1. **The feel is already there.** `Simulation.step` needs no real-time
   pacing concept to support an XCOM-style "resolve the turn" view: queuing
   several orders up front and stepping N ticks already produces exactly
   that -- appraisal/refusal, engagement, suppression, casualties, and
   incremental sub-cell movement all read as one coherent resolved turn.
   `SimFacade`'s existing separation of `QueueMove`/`Step()` in the Godot
   client is already the right shape for a client that wants to batch
   several orders and then animate the resulting tick sequence, rather than
   drive one order per real-time frame.
2. **An incapacitated (not dead) known contact still fully counts as a live
   threat for route exposure.** Agent 0's reissued order stayed refused for
   20 ticks after the hostile was incapacitated and suppression had decayed
   to near zero, because the hostile is still a `HostileTacticalKnowledge`/
   `TacticalKnowledge` entry and `Appraisal`'s exposure model does not
   distinguish an `Incapacitated` threat from an `Alive` one. This may be
   working as designed (an incapacitated agent could still be judged a risk
   until confirmed dead) or may be a gap worth a deliberate decision before
   any Overwatch/action-point work leans on exposure-driven refusal --
   flagged, not fixed.
3. Nothing here demonstrates or requires an action-point budget or an
   Overwatch commitment state; both stay explicitly out of scope (see
   Forbidden scope) and unscoped follow-on work, per this task's own
   instruction.

## Objective

Build a small console harness on top of the existing, unmodified
`CommandoWar.Sim`/`CommandoWar.Headless` to see whether queuing several
orders up front and stepping the simulation many ticks already reads as a
resolved "turn," the way an XCOM turn plays out -- before investing in any
real turn-based UI, action-point budgeting, or an Overwatch commitment state.

## Why this task exists

`Simulation.step` is a pure `SimConfig -> PlayerCommand[] -> WorldState ->
StepResult` function with no built-in real-time pacing; movement already
resolves incrementally via sub-cell progress across many ticks, not
instantly (TASK-018). Dave wants to see this "turn feel" made visible
directly, as a spike, before any follow-on design work (action-point
budgeting, Overwatch) is scoped.

## Central decisions

None needing `AskUserQuestion` -- Dave's own instruction fully specified the
harness's shape (console tool in `CommandoWar.Headless`, alongside
`Program.fs`/`AppraisalDemo.fs`; a handful of `MoveTo`/`Suppress` orders
across several agents; step 40-50 ticks; print the resulting snapshot
sequence; no `CommandoWar.Sim` change, no ADR). The one implementation-level
choice -- reusing `DiagnosticRender.runFrames`/`.Ascii` rather than hand-rolling
a new print format -- follows directly from `PathDemo`/`LosDemo`/`DemoScenario`
already being exactly this kind of disposable fixture rendered the same way.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `src/CommandoWar.Sim/Simulation.fs` (`SimConfig`, `PlayerCommand`, `StepResult`,
  `step`, TASK-018's sub-cell `AgentState.Progress` mechanism)
- `src/CommandoWar.Headless/DemoScenario.fs`, `PathDemo.fs`, `LosDemo.fs`
  (the hand-built, non-authoritative fixture-scenario precedent)
- `src/CommandoWar.Headless/DiagnosticRender.fs` (`runFrames`, `Ascii`)
- `src/CommandoWar.Headless/Program.fs` (`cmdRender`'s target dispatch, the
  precedent this task's `cmdTurn` follows)

## Dependencies

- None.

## Allowed scope

- `src/CommandoWar.Headless/TurnDemo.fs` (new): a hand-built `RawScenario` +
  `RecordedCommand[]` fixture.
- `src/CommandoWar.Headless/CommandoWar.Headless.fsproj`: one new `<Compile>`
  entry.
- `src/CommandoWar.Headless/Program.fs`: one new `turn` subcommand, its
  `usage()` line, and its `main` dispatch entry.
- `tasks/TASK-050-*.md` (this file), `docs/11_BACKLOG.md` (new B-060 row),
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Forbidden scope

- Any change to `src/CommandoWar.Sim` (this is read-only against the
  simulation contract, by Dave's own framing).
- Any change to `src/CommandoWar.Client.Godot` (scenes, `Core/`,
  `FSharpSceneHost.cs`) or the real-time demo scenes.
- A new ADR.
- Action-point budgeting or an Overwatch commitment state -- explicitly
  follow-on work, only worth pursuing if this spike shows the resolved-turn
  feel is worth it.
- Any change to the committed replay corpus, `Canonical.FormatVersion`, or
  `ScenarioContent.Version`.

## Required work

1. `TurnDemo.fs`: a small open scenario, several friendly agents, one
   hostile, no authored terrain.
2. A command log queuing a handful of `MoveTo`/`Suppress` orders across
   several agents and ticks.
3. `cwheadless turn`: build the initial state + command log, run
   `DiagnosticRender.runFrames` for the fixture's full tick count, print
   every frame's `DiagnosticRender.Ascii` output in tick order.
4. Run it; inspect the printed sequence by hand for whether it reads as a
   resolved turn; adjust the scenario if the first placement produces a
   dead-end (as it did here).
5. Verify: `dotnet build`, `dotnet test`, `cwheadless corpus`, `dotnet list`
   package boundary, `git status --porcelain`.
6. Update documentation per AGENTS.md.

## Acceptance criteria

- [x] `cwheadless turn` builds and runs, printing a per-tick diagnostic-frame
      sequence covering 40-50 ticks.
- [x] The printed sequence shows several agents' queued `MoveTo`/`Suppress`
      orders appraised, committed, and (where accepted) resolved
      incrementally tick by tick -- not instantly.
- [x] No `CommandoWar.Sim` change; no `CommandoWar.Client.Godot` change; no
      new ADR.
- [x] `dotnet build`/`dotnet test`/`cwheadless corpus` unaffected.
- [x] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet test`: `342/342` (unaffected).
- `dotnet run --project src/CommandoWar.Headless -- turn`: exit `0`, 46
  frames (ticks 0-45) printed.
- `dotnet run --project src/CommandoWar.Headless -- corpus`: `16/16`
  (unaffected).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package`:
  `FSharp.Core` only (unaffected).
- `git status --porcelain`: matches this task's allowed scope exactly.

## Documentation updates

- This task file's Outcome section.
- `docs/11_BACKLOG.md` (new B-060 row).
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Fully additive and disposable: one new Headless-only file, one `.fsproj`
line, and one new subcommand in `Program.fs`. Deleting `TurnDemo.fs`,
reverting the `.fsproj` line, and reverting the `Program.fs` addition
removes it completely with no effect on `CommandoWar.Sim`, any existing
corpus entry, or any client.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task -- particularly not action-point budgeting or an Overwatch commitment
state, both explicitly deferred follow-on work pending Dave's own read of
this spike.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-18). No changes requested; the resolved-turn feel
  and the incapacitated-contact-still-exposed observation are noted for a
  later decision, not acted on now.

## Note on task-selection sequencing

TASK-049 (backlog B-058) is still `review`, awaiting Dave's acceptance, when
this task was selected -- the established project discipline (accept, then
select the next task) was not followed here. This task was selected instead
directly from Dave's own explicit, fully-specified instruction in the same
session, functioning as an explicit override of that ordering rather than a
silent departure from it. `PROJECT_STATE.yaml active_work.selected_task` is
set to this task for the duration of its own implementation; TASK-049's own
review status is untouched and still needs Dave's acceptance separately.
