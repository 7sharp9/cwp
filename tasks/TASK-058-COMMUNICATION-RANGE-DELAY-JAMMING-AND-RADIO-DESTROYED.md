# TASK-058: Communication range, delivery delay, jamming, and radio-destroyed

Status: done (implemented and self-verified 2026-09-19; accepted by Dave
2026-09-19)
Owner: Dave
Phase: P3
Gate: G3 (retroactive)
Size: L in practice (the backlog's M undercounts the four-mechanic bundle);
realises backlog B-016b

## Outcome (2026-09-19)

Implemented as scoped, including the Central decision 6 opt-in gate. New
leaf `Communication.fs` (`CommsConfig.Range`/`.DeliveryDelayTicks`/
`.RadioDestroyChanceOnHit`, `Communication.available`/`.jammed`/`.reason`).
New `Scenario.Headquarters: Cell option`/`.Jammers: Jammer[]` (opt-in,
`None`/`[||]` for every existing scenario; `ScenarioContent.Version` 4 -> 5)
and matching validation (`HeadquartersOutOfMap`, `JammerOutOfMap`,
`NegativeJammerRadius`, `InvalidJammerWindow`). New `AgentState.
RadioDestroyed: bool` and `.PendingDelivery: (ReceivedOrder * QueueMode *
int64) option`, both genuinely canonical (`Canonical.FormatVersion` 11 ->
12, full re-pin). `QueueMode` moved from `Commands.fs` into `Domain.fs`
(the `PlayerIntent`/`Urgency`/`RiskTolerance` TASK-028 precedent) so
`PendingDelivery` can reference it; `Jammer` likewise defined in `Domain.fs`
ahead of `Scenario.fs` so `WorldState.Jammers` can reference it.

`Simulation.communication` rewritten: a pending command's availability
check now goes through `Communication.available` (static blackout **and**,
only when `WorldState.Headquarters` is authored, range/jamming/radio-
destroyed); an in-range order under an authored `Headquarters` is stashed
in `PendingDelivery` (due tick = accept tick + `DeliveryDelayTicks`) instead
of taking effect immediately, and a new second pass each tick delivers or
drops every due `PendingDelivery`, re-checking `Communication.available` at
delivery time (an agent may have moved out of range/into a jammer/lost its
radio since issue). A new pending command for a recipient with one already
in flight silently supersedes it (no double delivery). `commandIntake`'s
`holdsCommand` gained a `PendingDelivery` check — **a real gap found while
writing the cancel-in-flight-order test**: without it, `Command.cancel`
targeting an in-flight order was rejected `UnknownTargetCommand` before
`communication` ever ran, since intake only checked `Order`/`OrderQueue`.
`Simulation.combat` gained the radio-destroy roll immediately after the
existing wound application, gated on `hit && Headquarters.IsSome && not
RadioDestroyed` — a second, independent `RandomStream.next` draw per
qualifying hit, only consumed when a `Headquarters` is authored (so no
existing corpus entry's RNG stream shifts).

New `Overlay.AgentRadioLost`/`.AgentPendingDelivery` (`Diagnostics.fs`,
named distinctly from the `EventBody.AgentRadioDestroyed` transition event
they're adjacent to, the `OrderAppraisal`/`OrderDisposition` case-name
precedent) satisfy AGENTS.md's diagnostics-extension rule for the two
genuinely new per-tick canonical fields; both are sparse (the
`AgentSuppression`/`AgentAmmo` precedent) and derived by both `frame` and
`frameOf`. Rendered in `Ascii` (a text line each) and `Svg` (a dark-red
cross over a radio-lost agent; a dashed blue ring plus the due tick over a
pending-delivery agent). `WorldState.Headquarters`/`.Jammers` themselves get
no overlay — static authored geometry, the `ResupplyAreas` precedent (TASK-
047 added `ResupplyAreas` with no overlay either; confirmed by inspection
before drafting, not assumed). New events `OrderDelivered`/
`AgentRadioDestroyed`; `DeliveryFailure` gained `OutOfRange`/`Jammed`/
`RadioDestroyed`, reported in that fixed priority order (behind
`UnableToCommunicate`) when more than one condition holds.

**Central decision 6 (the opt-in gate) verified end to end, not just
asserted**: every one of the 16 pre-existing corpus entries, `Fixture`, and
`envelope-full` re-pinned byte-layout-only — `cwheadless corpus
--regenerate` then inspected `git diff --stat` (every changed file 2 lines,
hash-only) plus the handful of larger diffs (all fully explained by
TASK-055's own already-uncommitted change earlier this session, confirmed
line by line, not new TASK-058 behaviour). No new corpus entry: full
feature coverage instead via 10 new `SimulationTests` facts (range, delay,
jamming-then-window-closes, radio-destroyed-blocks-orders, supersession,
in-flight cancellation, the no-`Headquarters` reduction, and a two-run
determinism check on the radio-destroy roll itself) and 8 new
`ScenarioTests` validation facts — a deliberate deviation from the task
file's original "one new corpus entry" plan, made because authoring a
single scenario cleanly exercising all four mechanics with correct tick
timing would have been fragile, and the project has precedent (TASK-032,
TASK-033) for direct phase-level tests over a corpus entry when they prove
the mechanism just as directly. One new `DiagnosticsTests` fact proves both
new overlays render correctly in `Ascii`/`Svg` on a hand-built frame (no
corpus entry authors a `Headquarters`, so neither overlay ever appears in
any committed golden).

`dotnet build` all three `.slnx` (main Release; Godot client Debug) `0/0`;
`dotnet test` `362/362` (+18: 10 `SimulationTests`, 8 `ScenarioTests`, plus
the divergence-overlay-rendering fact folded in); `cwheadless corpus`
`16/16` (unaffected); `dotnet list CommandoWar.Sim package` `FSharp.Core`
only.

Full detail:
`docs/ledger/2026-09-19-TASK-058-communication-range-delay-jamming-and-radio-destroyed.md`.

## Objective

Turns the static-only comms-blackout model TASK-027 left in place
(`AgentState.CommunicationAvailable`, authored per-agent, never changes
mid-run) into a real dynamic model: an order fails or is delayed based on
distance from a new authored command post (HQ), a new authored jammer can
block a zone for an authored tick window, and a combat hit can permanently
knock out an agent's radio without incapacitating the agent itself. The
existing static blackout mechanism is untouched and still works exactly as
before (`AND`ed with the new checks, not replaced).

## Why this task exists

TASK-027 split `B-016b` off explicitly: "Radio range / delay / jamming /
radio-destroyed / report aging → B-016b." `docs/04` section 12.2 says the
same: "Radio range from a command origin, non-zero delivery delay, dynamic
jamming, radio-destroyed, and report aging beyond the section 12.4 bands are
backlog B-016b; that task makes comms availability per-tick mutable and
bumps `Canonical.FormatVersion` then." Selected by Dave via `AskUserQuestion`
over B-050 (done first, TASK-057) and B-043, to be done the same session.

## Central decisions (confirmed with Dave 2026-09-19 via `AskUserQuestion`)

1. **Scope**: the full bundle — range, delay, jamming, and radio-destroyed
   all in this one task (over three narrower options: range+delay only;
   range+delay+radio-destroyed; range-only-no-delay).
2. **Range origin**: a new authored per-side HQ entity (a static `Cell`,
   the `ResupplyAreas` precedent) — range = Chebyshev distance from HQ to
   the recipient — over the current squad leader or squad centroid.
3. **Delivery delay**: a single flat authored constant
   (`CommsConfig.DeliveryDelayTicks`) applied to any in-range order — over a
   distance-banded delay or an RNG-drawn delay.
4. **Jamming source**: a new authored Jammer entity (position, radius, an
   authored active-tick window — the "dynamic" part, even though nothing
   can destroy it yet this task) — over a static jammed-cell list or tying
   jamming to Hostile-agent proximity.
5. **Radio-destroyed trigger**: a new independent per-agent
   `AgentState.RadioDestroyed: bool`, permanently set by a new combat
   outcome (a fixed chance on a qualifying hit, alongside the existing
   wound roll) — over reusing `Vitals`/`Casualty.isAlive` (TASK-055
   precedent) or dropping radio-destroyed from this task's scope.
6. **Blast radius / opt-in gate (confirmed via a follow-up `AskUserQuestion`
   round)**: the whole feature set is gated behind a scenario authoring an
   HQ. When `Scenario.Headquarters = None` (every one of the 16 existing
   corpus entries, `Fixture`, `DemoScenario`, `PathDemo`, `LosDemo`,
   `TurnDemo`), `Communication.available` reduces to exactly the existing
   static-blackout check, delivery stays zero-delay, and Combat draws no
   extra radio-destroy roll — so every existing entry is behaviour-neutral
   (only a byte-layout re-pin from the `Canonical.FormatVersion` bump), and
   none of the mass-RNG-shift or mass-behaviour-change risk flagged in that
   round materialises. Only a scenario that opts in by authoring an HQ gets
   range/delay/jamming/radio-destroyed. Over making all four mechanics
   unconditionally active everywhere, which Dave explicitly declined once
   the corpus-wide re-verification cost was named.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`.
- `docs/04_SIMULATION_SPEC.md` section 12.2 (Communication phase) and
  section 12.4 (tactical-knowledge stale/expire bands) — the "report aging"
  language this task's Central decisions do not add a new mechanic for (see
  Required work item 7).
- `docs/05_COMMAND_AND_AGENT_AI.md` section 16 "Lost communication".
- `src/CommandoWar.Sim/Simulation.fs`: the `communication` phase (its own
  doc comment already anticipates this task: "Zero delivery delay ... not
  canonical state ... Delayed delivery is backlog B-016b"); the `combat`
  phase (exactly where `Casualty.wound`/`Suppression.gain`/`Suppression.raise`
  are applied per qualifying hit — the radio-destroy roll's precedent
  location); `StepState` (the `ResupplyAreas: Cell[]` "static within a run"
  field precedent for `Headquarters`/`Jammers`); `World.build`/`.ofScenario`/
  `.create`.
- `src/CommandoWar.Sim/Scenario.fs`: `RawScenario`/`Scenario`,
  `ScenarioError`, `Scenario.validate` — the `ResupplyAreas`/`RawArea`
  precedent for a new optional single-cell field and a new authored entity
  list.
- `src/CommandoWar.Sim/Perception.fs`: `Perception.chebyshev` — reused
  directly, not duplicated.
- `src/CommandoWar.Sim/Suppression.fs`/`Casualty.fs` — the leaf-module
  (`XConfig` + pure functions) precedent this task's new `Communication.fs`
  follows.
- `src/CommandoWar.Sim/Events.fs`/`Domain.fs`: `DeliveryFailure`,
  `OrderUndelivered`, `AgentState` — the fields/cases this task extends.
- `src/CommandoWar.Sim/Diagnostics.fs`,
  `src/CommandoWar.Headless/DiagnosticRender.fs`: the `UndeliveredOrder`
  overlay precedent, and this task's own AGENTS.md diagnostics-extension
  obligation (new authoritative state needs a visible overlay).
- `src/CommandoWar.Headless/Corpus.fs`: `rawOf`, `ScenarioSpec`,
  `ScenarioAgent` — the builder this task's new `lost-comms-dynamic` (or
  similarly named) corpus entry authors through.

## Dependencies

- B-016 (TASK-027, static comms blackout / `OrderUndelivered`) — done.
- B-051 (TASK-044, `OrderQueue`/`QueueMode`) — done (the `Replace`/`Append`
  semantics a delayed-in-flight order composes with).

## Inputs and assumptions

- Only `Friendly` agents can ever be a command recipient
  (`UnauthorisedRecipient` already rejects a `Hostile` one at intake) — HQ
  and range/delay/jamming/radio-destroyed only ever matter for the Friendly
  side in practice, though the mechanism itself is symmetric (a `Hostile`
  agent can still carry `RadioDestroyed`/be in `Jammers` range for free,
  the `Casualty`/`TASK-055` "symmetric costs nothing" precedent) since
  nothing gates it to one side.
- "Report aging beyond the section 12.4 bands" (the backlog's own phrase)
  does not need a new config value or mechanic: once an agent's
  `Communication.available` is `false` (for any reason — static blackout,
  out of range, jammed, radio-destroyed), it simply cannot receive an
  order; whether its own *observations* still feed the squad's shared
  `TacticalKnowledge` is unrelated to order delivery and already ages via
  the existing `StaleAfter`/`ExpireAfter` bands regardless of comms state
  (Perception/tactical-knowledge do not currently gate on
  `CommunicationAvailable` at all, and this task does not add that gate —
  a `Perception`/`TacticalKnowledge` change is a different, unscoped
  question the backlog row's own wording does not clearly ask for, and
  changing it would touch a phase this task otherwise leaves untouched).

## Allowed scope

- `src/CommandoWar.Sim/Scenario.fs`: `RawScenario.Headquarters: Cell option`,
  `RawScenario.Jammers: RawJammer[]`; `Scenario.Headquarters: Cell option`,
  `Scenario.Jammers: Jammer[]`; new `RawJammer`/`Jammer` types; new
  `ScenarioError` cases for an out-of-map HQ/jammer, a negative jammer
  radius, and an invalid jammer tick window; `Scenario.validate` extended;
  `ScenarioContent.Version` 4 -> 5.
- `src/CommandoWar.Sim/Simulation.fs`: `WorldState.Headquarters`/`.Jammers`
  (excluded from `Canonical.encode`, the `ResupplyAreas`/`Terrain`
  precedent); `World.build`/`.ofScenario`/`.create` threaded; `StepState`
  gains `Headquarters`/`Jammers` (static within a run); the `communication`
  phase rewritten per Required work; the `combat` phase gains the
  radio-destroy roll.
- `src/CommandoWar.Sim/Domain.fs` (or wherever `AgentState` lives): new
  `AgentState.RadioDestroyed: bool` (canonical) and
  `AgentState.PendingDelivery: (ReceivedOrder * QueueMode * int64) option`
  (canonical — the tick it is due to arrive).
- `src/CommandoWar.Sim/Canonical.fs`: encode the two new `AgentState`
  fields; `Canonical.FormatVersion` 11 -> 12.
- `src/CommandoWar.Sim/Communication.fs` (new leaf): `CommsConfig`
  (`Range`, `DeliveryDelayTicks`, `RadioDestroyChanceOnHit`),
  `Communication.available`, `.jammed`.
- `src/CommandoWar.Sim/Events.fs`: `DeliveryFailure` gains `OutOfRange` /
  `Jammed` / `RadioDestroyed` cases; new `OrderDelivered of command *
  recipient` event; new `AgentRadioDestroyed of agent * at: Cell` event.
- `src/CommandoWar.Sim/Diagnostics.fs`: `UndeliveredOrder`'s doc comment
  extended for the new `DeliveryFailure` cases (no shape change — it
  already carries no reason field, only `recipient`/`at`/`command`; add
  one only if Required work finds the overlay needs to show *which*
  failure, see Required work item 6); new `EventMarker` cases for
  `OrderDelivered`/`AgentRadioDestroyed` in `Diagnostics.eventMarker`.
- `src/CommandoWar.Headless/DiagnosticRender.fs`: `annotations`'s
  `eventNarration` gains the two new event kinds (a wildcard match, no
  exhaustiveness change elsewhere).
- `src/CommandoWar.Headless/Corpus.fs`: `ScenarioSpec` gains
  `Headquarters: Cell option` / `Jammers: (Cell * int * int64 * int64) list`
  (default `None`/`[]` for every existing entry — no behaviour change to
  them); one new corpus entry demonstrating the full feature (a friendly
  ordered out of HQ range, then a jammer, then a radio-destroying hit).
- `tests/CommandoWar.Sim.Tests/*.fs`: `ScenarioTests` (new validation
  facts), `SimulationTests` (new `Communication.available`/phase facts, the
  radio-destroy roll), `DiagnosticsTests` (the new corpus entry's golden),
  `CanonicalHashTests` (the format-12 section).
- `src/CommandoWar.Headless/DemoScenario.fs`, `LosDemo.fs`, `PathDemo.fs`,
  `TurnDemo.fs`, and `tests/CommandoWar.Sim.Tests/ScenarioTests.fs`'s
  `goodRaw()`: add `Headquarters = None; Jammers = [||]` to each hand-built
  `RawScenario` value (mechanical, no behaviour change).
- `docs/11_BACKLOG.md` (B-016b row), `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- No change to `Perception.fs`/`TacticalKnowledge` merge/decay (Inputs and
  assumptions above) — "report aging" is answered by the existing
  `StaleAfter`/`ExpireAfter` bands, not a new mechanic.
- No jammer-destruction mechanic (a jammer's own authored active window is
  the only way it turns off this task — Central decision 4).
- No radio-repair mechanic — `RadioDestroyed` is permanently sticky once
  set, the same one-way-door precedent as `Vitals.Dead`.
- No behaviour change to any of the 16 existing corpus entries, `Fixture`,
  or `envelope-full` beyond the `Canonical.FormatVersion` byte-layout
  re-pin (Central decision 6) — every one of them keeps `Headquarters =
  None`.
- No `CommandoWar.Client.Godot` change (a Sim-side task; no client UI for
  authoring or viewing HQ/jammers is in scope, the TASK-037/047
  sim-only-first precedent).
- No change to who may be a command recipient (`UnauthorisedRecipient`
  stays Hostile-blocking, unchanged).

## Required work

1. `Scenario.fs`: add `RawJammer`/`Jammer`, `RawScenario.Headquarters`/
   `.Jammers`, `Scenario.Headquarters`/`.Jammers`; validation (HQ in bounds
   when authored; each jammer in bounds, non-negative radius, `ActiveFromTick
   <= ActiveUntilTick`, both `>= 0`); `ScenarioContent.Version` 4 -> 5.
2. `Domain.fs`: `AgentState.RadioDestroyed: bool` (default `false`),
   `AgentState.PendingDelivery: (ReceivedOrder * QueueMode * int64) option`
   (default `None`); `Canonical.fs` encodes both; `Canonical.FormatVersion`
   11 -> 12, full re-pin.
3. New `Communication.fs`: `CommsConfig.Range`/`.DeliveryDelayTicks`/
   `.RadioDestroyChanceOnHit` (literal integers, the `PerceptionConfig`/
   `CombatConfig` precedent — pick values that read clearly in a corpus
   entry, e.g. a delay a handful of ticks, a chance in the 10-20% band on
   the existing `0..1000` scale); `Communication.jammed (jammers: Jammer[])
   (tick: int64) (cell: Cell) : bool`; `Communication.available (hq: Cell
   option) (jammers: Jammer[]) (tick: int64) (agent: AgentState) : bool` =
   `agent.CommunicationAvailable && not agent.RadioDestroyed && (match hq
   with None -> true | Some h -> Perception.chebyshev h agent.Position <=
   CommsConfig.Range) && not (Communication.jammed jammers tick
   agent.Position)`.
4. `Simulation.fs` `World.build`/`.ofScenario`/`.create`: thread
   `Headquarters: Cell option` and `Jammers: Jammer[]` onto `WorldState`
   (`create` passes `None`/`[||]`); `Canonical.fs` excludes both (the
   `ResupplyAreas`/`Terrain` precedent, a doc-comment line each).
5. `Simulation.fs` `communication` phase rewrite:
   - `StepState` gains `Headquarters`/`Jammers` (static within a run,
     copied from the input `WorldState` at `step`'s entry — the
     `ResupplyAreas` precedent).
   - For each `ordered` pending command this tick, replace the single
     `not agents.[idx].CommunicationAvailable` branch with: if
     `Communication.available s.Headquarters s.Jammers s.Tick agents.[idx]`
     is `false`, emit `OrderUndelivered` with the *specific* failure reason
     (`UnableToCommunicate` for the static-blackout case,
     `OutOfRange`/`Jammed`/`RadioDestroyed` for the new ones — whichever
     applies; if more than one applies, report in that fixed priority
     order, documented, so the result is deterministic and explainable)
     and drop the command (unchanged from today). Otherwise: if
     `s.Headquarters = None` (the opt-in gate, Central decision 6), deliver
     immediately exactly as today (Replace/Append/Cancel unchanged — every
     existing corpus entry's trace is untouched). If `s.Headquarters =
     Some _`, instead of writing `Order` immediately, clear any existing
     `PendingDelivery` for the recipient (a new pending command always
     supersedes an in-flight one, Central decision extension of the
     existing Replace-supersedes precedent) and set a new
     `PendingDelivery = Some(order, mode, s.Tick +
     CommsConfig.DeliveryDelayTicks)` (a `PendingCancel` still applies
     immediately, unaffected by delay — cancelling an order that has not
     even arrived yet is instant, no radio round-trip to model).
   - A second pass, later in the same phase (after the pending-command
     loop, so a same-tick issue-then-immediately-due edge case is
     impossible since `DeliveryDelayTicks >= 1`): for every agent whose
     `PendingDelivery = Some(order, mode, dueTick)` with `dueTick <=
     s.Tick`, re-check `Communication.available` *now* (the agent may have
     moved out of range, into a jammer, or lost its radio since the order
     was issued) — if still available, apply the order (Replace/Append,
     the existing logic) and emit `OrderDelivered(order.Command,
     recipient)`, clearing `PendingDelivery`; if not, emit
     `OrderUndelivered` with the current failure reason and clear
     `PendingDelivery` (the order is lost, not retried — the
     `docs/05` "communication failure is explicit, not silent" rule
     applies here too).
6. `Simulation.fs` `combat` phase: immediately after the existing wound
   application (`if hit then Casualty.wound t.Vitals ...`), when `s.Headquarters
   <> None` (Central decision 6's opt-in gate) and `hit` and
   `not t.RadioDestroyed`, draw one more `RandomStream.next`, compare
   `draw % 1000UL < uint64 CommsConfig.RadioDestroyChanceOnHit`; on a hit,
   set `RadioDestroyed = true` on the target and emit
   `AgentRadioDestroyed(target.Id, target.Position)`. No draw at all when
   `s.Headquarters = None` — every existing corpus entry's RNG stream is
   untouched.
7. `Events.fs`/`Diagnostics.fs`/`DiagnosticRender.fs`: the new event/overlay
   plumbing per Allowed scope.
8. `Corpus.fs`: extend `ScenarioSpec`/`rawOf`/`ScenarioAgent` as needed; add
   one new corpus entry (name TBD while drafting, e.g.
   `lost-comms-dynamic`) whose scenario authors an HQ, a jammer with an
   authored active window, and a schedule that demonstrates: an order
   delivered late (in range, past `DeliveryDelayTicks`), an order refused
   `OutOfRange`, an order refused `Jammed` while the jammer is active then
   delivered once its window closes, and a radio-destroying hit (driven by
   deterministic combat the same way `open-engagement` is) followed by a
   further order failing `RadioDestroyed`.
9. Update every hand-built `RawScenario` site (Allowed scope) with
   `Headquarters = None; Jammers = [||]`; confirm every one of the 16
   existing corpus entries plus `Fixture`/`envelope-full` re-pin
   byte-layout-only (`--regenerate` then inspect that no tick/event count
   changed anywhere).
10. Tests per Allowed scope; verify per Required verification.
11. Update backlog/ledger/state.

## Acceptance criteria

- [x] A scenario with no authored HQ behaves identically to before this
      task: same-tick delivery when the static `CommunicationAvailable`
      flag is `true`, `OrderUndelivered(UnableToCommunicate)` when it is
      `false`, no new event, no RNG draw beyond what already existed.
- [x] A scenario with an authored HQ: an order to a recipient beyond
      `CommsConfig.Range` from HQ is refused `OutOfRange`, never delivered.
- [x] An order to a recipient within range is delivered exactly
      `CommsConfig.DeliveryDelayTicks` ticks after acceptance (not the same
      tick), emitting `OrderDelivered`.
- [x] A recipient inside an active jammer's radius is refused `Jammed`;
      once the jammer's authored active window ends, an identical order
      succeeds.
- [x] A combat hit has a real, deterministic chance to set
      `RadioDestroyed = true` on the target, emitting `AgentRadioDestroyed`;
      a radio-destroyed agent's further orders are refused `RadioDestroyed`
      even though it may still be `Alive` and fighting.
- [x] A new pending command for a recipient with an order already in
      flight supersedes the in-flight one (no double-delivery, no stale
      delivery of a superseded order).
- [x] Every one of the 16 existing corpus entries, `Fixture`, and
      `envelope-full` re-pins byte-layout-only: unchanged tick counts,
      unchanged event counts and kinds, unchanged `Random.Draws` at every
      tick.
- [x] `dotnet build`/`dotnet test` green; `cwheadless corpus` `16/16`
      after `--regenerate`, every diff inspected. (No new corpus entry --
      see the Outcome section's documented deviation; full feature
      coverage via direct `SimulationTests`/`ScenarioTests` facts instead,
      the TASK-032/033 precedent.)
- [x] `CommandoWar.Client.Godot` still builds unchanged (no client file
      touched).
- [x] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: expect `0/0`.
- `dotnet test`: expect unaffected-plus-new-facts count.
- `cwheadless corpus --regenerate` then `cwheadless corpus`: expect
  `17/17`; `git diff --stat content/replays/` inspected entry by entry —
  every existing entry's diff must be explainable purely by the format-12
  byte-layout change (hash values move; tick counts, event counts, and
  event kinds do not).
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: expect `0/0`, no source file under it touched.
- `git status --porcelain`: matches this task's allowed scope.

## Evidence to capture

- The new corpus entry's golden ASCII/SVG (if a `DiagnosticsTests` fact
  pins one) and its `content/replays/<name>.md` table.
- `cwheadless corpus --regenerate` diff summary, with each existing entry's
  change explained.
- New `SimulationTests`/`ScenarioTests` facts' pass.

## Expected files

- `src/CommandoWar.Sim/Scenario.fs`, `Simulation.fs`, `Domain.fs`,
  `Canonical.fs`, `Communication.fs` (new), `Events.fs`, `Diagnostics.fs`.
- `src/CommandoWar.Headless/Corpus.fs`, `DiagnosticRender.fs`,
  `DemoScenario.fs`, `LosDemo.fs`, `PathDemo.fs`, `TurnDemo.fs`.
- `tests/CommandoWar.Sim.Tests/ScenarioTests.fs`, `SimulationTests.fs`,
  `DiagnosticsTests.fs`, `CanonicalHashTests.fs`, `CorpusTests.fs` (a new
  theory case, automatic via `Corpus.all`).
- `content/replays/*.md` (every entry re-pinned; one new).
- `content/diagnostics/*` (any golden the new corpus entry needs).
- `tasks/TASK-058-COMMUNICATION-RANGE-DELAY-JAMMING-AND-RADIO-DESTROYED.md`.
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md` (+ new `docs/ledger/`
  detail file), `PROJECT_STATE.yaml`.

## Documentation updates

- This task file's Outcome section.
- `docs/11_BACKLOG.md` (B-016b row: `proposed -> done`).
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file;
  refresh the "Pinned facts" `Canonical.FormatVersion`/`ScenarioContent.
  Version`/green-test-count rows).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Every new field is additive and the opt-in gate means every existing
scenario's runtime behaviour is unaffected; a full re-pin is needed either
way for the `Canonical.FormatVersion` bump, so rollback means reverting the
commit and re-running `cwheadless corpus --regenerate` back to the
format-11 values (the inverse of this task's own regeneration step).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave.
- Accepted: yes (2026-09-19). No client UI in this task (Sim-side only,
  Forbidden scope excluded it); accepted on the self-verification evidence
  (build/test/corpus green across all three `.slnx`, every corpus/golden
  re-pin diff inspected, two real bugs caught and fixed by the test suite
  itself), the TASK-023/036/050 precedent for Sim/tooling-only work.
