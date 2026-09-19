# TASK-058: Communication range, delivery delay, jamming, and radio-destroyed

Owner: Dave (implementing agent session)
Source revision: `main`, after TASK-057's implementation (self-verified,
awaiting acceptance).
Environment: `dotnet` `10.0.303`, Windows 11 (no Godot editor step needed --
Sim-side only, no client file touched).

## Selection

Chosen via `AskUserQuestion` over B-016b, B-043 (the launch context's three
named candidates), with Dave explicitly asking for B-050 (done first,
TASK-057) then B-016b, both in this session.

## Central decisions

Five `AskUserQuestion` rounds before drafting, all recommended options
chosen:

1. **Scope**: the full bundle (range, delay, jamming, radio-destroyed) in
   one task, over three narrower slices.
2. **Range origin**: a new authored HQ entity (`Scenario.Headquarters: Cell
   option`), over the current squad leader or squad centroid.
3. **Delivery delay**: a single flat authored constant
   (`CommsConfig.DeliveryDelayTicks`), over a distance-banded or
   RNG-drawn delay.
4. **Jamming source**: a new authored Jammer entity (position, radius, an
   authored active-tick window), over a static jammed-cell list or tying
   jamming to Hostile-agent proximity.
5. **Radio-destroyed trigger**: a new independent `AgentState.
   RadioDestroyed: bool`, set by a fixed-chance combat-hit roll, over
   reusing `Vitals` (the TASK-055 precedent) or dropping it from scope.

A sixth round, after investigating the implementation consequences of
"full bundle": making delay/jamming/radio-destroyed unconditionally active
for every scenario would make every existing combat hit draw an extra
`RandomStream.next` (shifting every subsequent roll) and make delivery
stop being same-tick everywhere, re-pinning all 16 corpus entries with a
genuine behavioural shift, not a byte-layout one -- a much larger
verification burden than "full bundle" was understood to mean. Put to Dave
directly: gate the whole feature set behind a scenario authoring an HQ
(opt-in, old scenarios fully unaffected) or leave it unconditional. Dave
chose opt-in.

## Investigation before drafting

- Read `docs/04_SIMULATION_SPEC.md` section 12.2 and `docs/05_COMMAND_AND_
  AGENT_AI.md` section 16 -- confirmed no existing design text beyond the
  backlog row's own wording; "report aging" resolved as already covered by
  the existing `StaleAfter`/`ExpireAfter` bands (Perception/tactical-
  knowledge do not gate on comms today and this task does not add that
  gate -- a different, unscoped question).
- Read `Simulation.fs`'s `communication` phase doc comment directly: it
  already named this task by number ("Delayed delivery is backlog
  B-016b"), confirming the zero-delay/no-`PendingDelivery` design was a
  deliberate placeholder, not an oversight.
- Read `Simulation.fs`'s `combat` phase to find exactly where `Casualty.
  wound`/`Suppression.gain`/`.raise` are applied per qualifying hit -- the
  precedent location for the new radio-destroy roll, so it composes with
  the existing wound roll rather than living somewhere else in the phase
  order.
- Read `Scenario.fs` end to end (`RawScenario`/`Scenario`/`ScenarioError`/
  `Scenario.validate`) for the `ResupplyAreas`/`RawArea` precedent a new
  optional single-cell field (`Headquarters`) and a new authored entity
  list (`Jammers`) should follow.
- Grepped for an existing Chebyshev-distance helper before writing a new
  one: found `Perception.chebyshev`, reused directly (`Communication.fs`
  never duplicates it).
- Confirmed only `Friendly` agents can ever be a command recipient
  (`UnauthorisedRecipient` already rejects `Hostile`) -- HQ/jamming/radio-
  destroyed only ever matter for the Friendly side in practice, though the
  mechanism itself stays symmetric (a `Hostile` agent can carry
  `RadioDestroyed`/sit in a jammer's radius too, at zero extra code cost,
  the TASK-055 "symmetric costs nothing" precedent).
- Grepped for `ResupplyAreas` in `Diagnostics.fs`/`DiagnosticRender.fs`
  before deciding whether `Headquarters`/`Jammers` need their own overlay:
  found zero matches, confirming TASK-047 (which added `ResupplyAreas`)
  never gave it one -- the established precedent that purely static
  authored geometry does not need a diagnostic overlay, only genuinely
  new per-tick canonical state does.

## Changes

- `src/CommandoWar.Sim/Scenario.fs`: `RawJammer`/`Jammer` types (`Jammer`
  moved to `Domain.fs`, see below); `RawScenario.Headquarters: Cell
  option`/`.Jammers: RawJammer[]`; `Scenario.Headquarters`/`.Jammers`;
  `ScenarioError` gains `HeadquartersOutOfMap`/`JammerOutOfMap`/
  `NegativeJammerRadius`/`InvalidJammerWindow`; `Scenario.validate`
  extended; `ScenarioContent.Version` 4 -> 5.
- `src/CommandoWar.Sim/Domain.fs`: `QueueMode` moved in from `Commands.fs`
  (compile-order requirement -- `AgentState.PendingDelivery` needs it, and
  `Domain.fs` compiles before `Commands.fs`, the `PlayerIntent`/`Urgency`/
  `RiskTolerance` TASK-028 precedent exactly); new `Jammer` type (moved
  ahead of `Scenario.fs` for the same reason -- `WorldState.Jammers`
  references it); `AgentState.RadioDestroyed: bool`/`.PendingDelivery:
  (ReceivedOrder * QueueMode * int64) option` (both default
  `false`/`None` in `Agent.create`); `WorldState.Headquarters: Cell
  option`/`.Jammers: Jammer[]`.
- `src/CommandoWar.Sim/Canonical.fs`: `writeAgent` gains `RadioDestroyed`/
  `PendingDelivery` sections; `Headquarters`/`Jammers` explicitly NOT
  written (the `ResupplyAreas`/`Terrain` precedent); `FormatVersion` 11 ->
  12.
- `src/CommandoWar.Sim/Communication.fs` (new leaf): `CommsConfig.Range`
  (15), `.DeliveryDelayTicks` (3), `.RadioDestroyChanceOnHit` (150, a 15%
  chance on the `0..1000` scale); `Communication.jammed`/`.available`/
  `.reason`.
- `src/CommandoWar.Sim/Events.fs`: `DeliveryFailure` gains `OutOfRange`/
  `Jammed`/`RadioDestroyed`; new `OrderDelivered`/`AgentRadioDestroyed`
  events; the `DomainEvent` ordering doc comment extended for both.
- `src/CommandoWar.Sim/Simulation.fs`:
  - `World.build`/`.ofScenario`/`.create` thread `Headquarters`/`Jammers`
    onto `WorldState` (`create` passes `None`/`[||]`).
  - `StepState` gains `Headquarters`/`Jammers` (static within a run, the
    `ResupplyAreas` precedent), populated at `step`'s entry.
  - `commandIntake`'s `holdsCommand` gains a `PendingDelivery` check (see
    Deviation/bug below).
  - `communication` rewritten: a new `applyDelivered` (pure, returns the
    updated agent and whether it queued, since the two call sites need
    different additional events); the main loop's availability check now
    calls `Communication.available`/`.reason`; a `PendingOrder` under an
    authored `Headquarters` is stashed in `PendingDelivery` instead of
    applied immediately (a new pending command silently supersedes an
    in-flight one); `PendingCancel` checks `PendingDelivery` first (via
    `holdsCommand`'s fix, reachable at all); a new second pass after the
    main loop delivers or drops every due `PendingDelivery`, re-checking
    availability at delivery time.
  - `combat` gains the radio-destroy roll immediately after the existing
    wound application, gated `hit && Headquarters.IsSome && not
    RadioDestroyed`.
- `src/CommandoWar.Sim/Diagnostics.fs`: new sparse `Overlay.AgentRadioLost`
  (named distinctly from `EventBody.AgentRadioDestroyed`, the
  `OrderAppraisal`/`OrderDisposition` case-name-vs-type-name precedent) and
  `Overlay.AgentPendingDelivery`; both derived by `frame` and `frameOf`;
  new `EventMarker` cases for `OrderDelivered`/`AgentRadioDestroyed`.
- `src/CommandoWar.Headless/DiagnosticRender.fs`: `Ascii` gains a line each
  for the two new overlays; `Svg` gains a dark-red cross (radio-lost, drawn
  ON the agent, unlike `AgentAmmo`'s ring-one-radius-out) and a dashed blue
  ring plus the due-tick text (pending-delivery); four pre-existing
  exhaustive `Overlay` matches (two in `Ascii`, one in `Svg`, all inherited
  from TASK-057's `Divergence` case) needed one arm each for the two new
  cases.
- `src/CommandoWar.Headless/AppraisalDemo.fs`: two new `unhandled.Add(...)`
  arms (this disposable demo's committed frame never produces either, since
  `exposed-approach` authors no `Headquarters`).
- `src/CommandoWar.Headless/Corpus.fs`: `ScenarioSpec` gains `Headquarters:
  Cell option`/`Jammers: (Cell * int * int64 * int64) list`; `rawOf`
  threads them; every one of the 16 existing `ScenarioSpec` literals gets
  `Headquarters = None; Jammers = []` (mechanical, no behaviour change).
- `src/CommandoWar.Headless/DemoScenario.fs`, `LosDemo.fs`, `PathDemo.fs`,
  `TurnDemo.fs`, `tests/CommandoWar.Sim.Tests/ScenarioTests.fs`'s
  `goodRaw()`, `DeterminismPropertyTests.fs` (two inline `WorldState`
  literals): the same mechanical `Headquarters = None`/`Jammers = [||]`
  addition, required by the compiler once the fields were added to
  `RawScenario`/`WorldState`.
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`: 10 new facts (see
  Verification).
- `tests/CommandoWar.Sim.Tests/ScenarioTests.fs`: 8 new validation facts
  (see Verification); version-literal updates (`ScenarioContent.Version`
  4 -> 5, `Canonical.FormatVersion` 11 -> 12, the "unsupported version"
  probe values bumped from 5 to 6 now that 5 is valid).
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`: one new fact proving
  both new overlays render in `Ascii`/`Svg`; four exhaustive-match arms
  matching `DiagnosticRender.fs`'s own (inherited from TASK-057).
- `tests/CommandoWar.Sim.Tests/CanonicalHashTests.fs`,
  `FixtureTests.fs`, `PathfindingTests.fs`, `ReplayTests.fs`, `SightTests.fs`,
  `TerrainTests.fs`, `CorpusTests.fs`: hash-literal and format-version
  re-pins throughout (see Verification).
- `content/replays/*.md`/`*.cwreplay` (all 16 + `envelope-full`),
  `content/diagnostics/*` (every golden): re-pinned. No corpus entry added
  or removed.

## Deviations found during implementation

1. **A real bug in `commandIntake.holdsCommand`, found by the cancel-
   in-flight-order test.** `holdsCommand` (used by `commandIntake` to
   validate a `Cancel` command's target before it ever reaches
   `communication`) checked only `agent.Order` and `agent.OrderQueue`. An
   order stashed in the new `PendingDelivery` field is neither, so a
   `Cancel` targeting an in-flight order was rejected
   `UnknownTargetCommand` at intake, before my new in-flight-cancel logic
   in `communication` could ever run -- the logic was correct but
   unreachable. Fixed by adding a `PendingDelivery` check to
   `holdsCommand`. Caught immediately by the new `SimulationTests` fact,
   not discovered later.
2. **`applyDelivered`'s first draft silently dropped the `OrderQueued`
   event.** Refactoring the zero-delay delivery logic into a shared pure
   function (reused by both the immediate and the delayed-delivery paths)
   initially lost the `emit (OrderQueued(...))` call the original inline
   code had for an `Append` landing behind an already-active order --
   caught by the existing (pre-TASK-058) `order-queue-stacking-and-
   cancellation` corpus entry's event count dropping from 2 to 0 during
   the first `cwheadless corpus --regenerate`. Fixed by having
   `applyDelivered` return whether it queued, letting each of its two call
   sites emit the right event around it.
3. **The Overlay/EventBody case-name collision.** The first draft named
   the new overlay `AgentRadioDestroyed`, identical to the existing
   `EventBody.AgentRadioDestroyed` transition event -- compiled fine in
   isolation but broke four *other* exhaustive `EventBody` matches
   elsewhere in `Diagnostics.fs` (inherited from TASK-057's `Divergence`
   overlay) with "expected `EventBody` but here has type `Overlay`" errors,
   since the bare case name now resolved to the wrong type in some
   contexts. Renamed the overlay to `AgentRadioLost`, matching the
   project's own existing precedent for this exact situation
   (`OrderAppraisal`/`OrderDisposition` are named distinctly for the
   identical reason, per that case's own doc comment).

## Verification

- `dotnet build src/CommandoWar.Sim/CommandoWar.Sim.fsproj -c Release`,
  then the full `CommandoWar.slnx -c Release`, then `src/CommandoWar.Client.
  Godot/CommandoWar.Client.Godot.slnx -c Debug`: all `0/0`, run repeatedly
  after each file's edit throughout implementation to isolate each
  exhaustive-match/type error to its cause immediately rather than batching
  them.
- `dotnet test CommandoWar.slnx -c Release`: `362/362` (+18 over the
  344 baseline after TASK-057: 10 new `SimulationTests` facts -- an
  out-of-range refusal, an in-range order stashed as `PendingDelivery` not
  delivered same-tick, delivery exactly `DeliveryDelayTicks` ticks later
  (not before), a jammer refusing then releasing once its window closes,
  a forced-`RadioDestroyed` agent's orders refused while still `Alive`, a
  superseding order dropping the original in-flight one with no double
  delivery, cancelling an in-flight order with no radio round-trip,
  `Communication.available` reducing to the static check alone with no
  `Headquarters`, and a two-run determinism check on the radio-destroy
  roll itself; 8 new `ScenarioTests` validation facts (see Changes); the
  remaining net movement is existing facts' hash/version literals updated
  in place, not new facts).
- `cwheadless corpus --regenerate` then `cwheadless corpus`: `16/16`.
  `git diff --stat content/replays/` inspected entry by entry: every file
  a 2-line hash-only change except the handful TASK-055 (already
  uncommitted, earlier this same session) had itself changed -- re-checked
  those specific diffs line by line and confirmed every non-hash line
  matches TASK-055's own documented outcome exactly (a `seen tick`/stress
  value freezing, or a `ContactExpired` pair at tick 65), nothing new from
  TASK-058. `demo.html`'s diff similarly confirmed every changed line
  contains only `hash 0x`/`format 1` text, no event/count change --
  `DemoScenario` authors no `Headquarters`.
- `envelope-full.cwreplay`/`.md`: canonical/initial-hash/all 24 checkpoints
  regenerated via `cwheadless replay-file` (which reports the actual
  values in its own `DIVERGED` report against the stale committed ones);
  re-verified `checkpoints : OK` after the update.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package`:
  `FSharp.Core` only.
- `git status --porcelain`: matches this task's allowed scope (Scenario.fs,
  Domain.fs, Canonical.fs, Communication.fs (new), Events.fs, Simulation.fs,
  Diagnostics.fs, the fsproj; Corpus.fs, DiagnosticRender.fs,
  AppraisalDemo.fs, the four hand-built `RawScenario` demo files; the nine
  touched test files; `content/replays/`/`content/diagnostics/` re-pins;
  no `CommandoWar.Client.Godot` source file).

## Documents updated

- `tasks/TASK-058-COMMUNICATION-RANGE-DELAY-JAMMING-AND-RADIO-DESTROYED.md`
  (created, `Outcome` filled in, acceptance criteria checked).
- `docs/11_BACKLOG.md` (B-016b row: `proposed -> review`).
- `docs/12_PROGRESS_LEDGER.md` (this detail file; index row added; "Pinned
  facts" `Canonical.FormatVersion`/`ScenarioContent.Version`/green-test-
  count refreshed).
- `PROJECT_STATE.yaml` (`active_work` updated).

## Review

- Reviewer: Dave.
- Accepted: yes (2026-09-19), on the self-verification evidence -- no
  client UI in this task (Sim-side only).
