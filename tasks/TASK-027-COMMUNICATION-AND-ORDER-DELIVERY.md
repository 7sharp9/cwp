# TASK-027: Communication constraints and order delivery

Status: review (implemented 2026-09-07 on branch
`task-027-communication-and-order-delivery`; central decisions confirmed with
Dave 2026-09-07 before the phase bodies)
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises the communication half of backlog B-016
Size: S–M (landed at S–M — no `Canonical.FormatVersion` bump, no hash re-pin,
no event-count re-pin on the nine existing entries; the corpus and fixture are
byte-identical, matching TASK-024 / TASK-025)

## Outcome (2026-09-07)

Implemented on branch `task-027-communication-and-order-delivery` (committed
locally, not pushed). All five central decisions confirmed with Dave before
the phase bodies were written, all as recommended:

- **A:** static per-agent `AgentState.CommunicationAvailable` (default `true`;
  authored `false` = comms blackout), no PRNG draw, flag **excluded from
  `Canonical.encode`** like `Terrain` (ADR-0002 amendment).
- **B:** the pending order is a `StepState`-local list, **not** canonical
  state — `Canonical.FormatVersion` stays `3`, zero hash re-pin.
- **C:** radio range / delivery delay / jamming / radio-destroyed / report
  aging split to a new **B-016b**.
- **D:** `OrderUndelivered` only; no `OrderDelivered` success event (deferred
  to B-016b — with zero delay it duplicates `CommandAccepted`). **No new event
  fires for any existing entry**, so no event-count re-pin.
- **E:** no ADR.

`commandIntake` reworked to record an accepted `(command, recipient, target)`
as a pending order instead of writing `AgentState.Destination`; the new
`Simulation.communication` phase (slot 2) delivers each pending order (writes
`Destination`) to a recipient with `CommunicationAvailable = true`, or emits
`OrderUndelivered(command, recipient, UnableToCommunicate)` and drops it for
one with `false`. `216 → 228` green. New `lost-comms` corpus entry + golden.
`cwheadless corpus` (8 pre-existing) and `cwheadless fixture` byte-identical,
no `--regenerate`; no `content/diagnostics/` golden moved;
`replay-file envelope-full.cwreplay` checkpoints OK at `canonical 3`.

Full detail:
`docs/ledger/2026-09-07-TASK-027-communication-and-order-delivery.md`.

## Objective

Turn the **Communication phase (12.2)** — currently a no-op arm in
`Simulation.runPhase` — into a real phase that decides, per accepted order and
per recipient, whether the order actually reaches the agent this tick. Split
the `docs/04` section 12.1 / 12.2 boundary the spec already draws: command
intake **validates and records** an accepted order; the Communication phase
**delivers** it (writes `AgentState.Destination`) when the recipient can be
reached, or **fails it explicitly** (emits `OrderUndelivered`, drops the
order) when it cannot.

This is the direct precondition for **B-017 staged order appraisal**, the G3
keystone: appraisal stage 1 ("Was the order received?", `docs/05` section 5)
cannot exist until "received" is a real, inspectable fact rather than an
assumption. It also realises the **"Lost communication"** vertical-slice
scenario (`docs/05` section 16): "An order is not delivered. The UI reports
communication failure rather than pretending the recipient disobeyed."

Scope is deliberately narrow: a **static, deterministic per-recipient
reachability check**. Radio range from a command origin, non-zero delivery
delay, dynamic jamming, radio-destroyed, and report aging beyond the
`StaleAfter` / `ExpireAfter` bands TASK-026 already landed are a focused
follow-up (**B-016b**), named here, not built here.

## Why this task exists

- `docs/08_ROADMAP_AND_GATES.md` section 6 P3 Required work: "implement
  communication and shared tactical knowledge". TASK-026 (B-015) landed the
  shared-tactical-knowledge half; this lands the communication half.
- `docs/04_SIMULATION_SPEC.md` section 12.2 is a written-but-unrealised phase:
  "determine which recipients receive an order this tick; voice and radio
  delay may initially be zero when in range; communication failure must be
  explicit, not silently ignored."
- `docs/03_ARCHITECTURE.md` section 7 step 2 ("Apply communication and order
  delivery") is the one tick step with no realisation note.
- `docs/04` section 14 lists "order delivered or communication failed" as a
  minimum event category with no realisation yet.
- G3 evidence needs "scenario traces readable enough to diagnose all
  decisions" and "at least two agents appraise the same intent differently for
  inspectable reasons". An order that never arrived is the cleanest such
  reason, and B-017 consumes it.
- Risk R-023 (enemy AI cheats): the mitigation is the "same observation
  contract" and the "known-versus-authoritative overlay". Communication
  failure is part of that contract — an undelivered order is a real
  information gap, not a cheat.
- Risk R-007 (AI too broad to debug): communication delivery is an explicit,
  inspectable phase, not a hidden precondition buried inside appraisal.

Depends only on TASK-020 / TASK-024 (command intake, done) and TASK-026
(perception, done, on `main`). Directly unblocks B-017. Independent of
TASK-025 — `envelope-full.cwreplay` does not move (no canonical bump).

## Required reading

Read in this order; verify each path and inspect the source before trusting a
filename (`AGENTS.md`).

1. `PROJECT_STATE.yaml`, `AGENTS.md`
2. `docs/ledger/2026-09-07-TASK-026-perception-and-tactical-knowledge.md` — the
   `PerceptionConfig` module-literal pattern (the precedent for any new tuning
   constant) and the FS0025 discipline for adding event / overlay cases (every
   exhaustive match armed with the intended branch, never a wildcard; the
   sites listed in the ledger).
3. `decisions/ADR-0002-SIMULATION-BOUNDARY.md`, the whole file and especially
   the 2026-09-03 amendment "Static authoritative data and the canonical
   image": it governs why `AgentState.CommunicationAvailable` is **excluded**
   from `Canonical.encode` while it is static (the `Terrain` precedent) and
   why nothing in this task bumps `Canonical.FormatVersion`.
4. `docs/04_SIMULATION_SPEC.md` sections 2 (delivery tick vs issue tick,
   TASK-024), 10 (`WorldState`), 11 ("communication availability" listed as
   agent state), 12.1 (command intake, current), 12.2 (Communication — the
   no-op this task realises), 12.5 (Appraisal — B-017, the consumer), 14
   (events), 17 (canonical hashing — the derived-cache / static-data
   exclusions), 20 (invariants), 21 (authored scenario).
5. `docs/05_COMMAND_AND_AGENT_AI.md` sections 2 (the pipeline), 3 (perception),
   5 stage 1 ("Was the order received?"), 6–7 (`OrderDisposition` /
   `DecisionReason` — `UnableToCommunicate` is already in the vocabulary), 12
   (enemy AI — same rules where practical), 15 (tuning rules), 16 ("Lost
   communication"), 17 (deferred).
6. `docs/03_ARCHITECTURE.md` section 7 around "Apply communication and order
   delivery".
7. `docs/09_TEST_STRATEGY.md` sections 2.1, 2.2, 2.3 ("radio loss prevents
   immediate knowledge propagation"), 8.
8. `docs/10_RISK_REGISTER.md` R-007, R-023.
9. `src/CommandoWar.Sim/Simulation.fs` — `commandIntake` (the phase this task
   reworks), `runPhase` (`Communication` currently in the no-op list),
   `StepState` (the "mutable-but-contained accumulator" — the carrier for the
   pending order), `step`, `navigationAndMovement` (reads `Destination` at
   phase 7), the `perception` / `tacticalKnowledge` phase header-comment style
   ("Realised by TASK-NNN"), `World.build` / `World.ofScenario` /
   `Setup.sixAgentWorld` (both agent-construction paths).
10. `src/CommandoWar.Sim/Domain.fs` — `AgentState` (the canonical fields vs the
    `Route` / `VisibleContacts` derived-cache comments — the model for a
    static `CommunicationAvailable` doc comment), `WorldState`, `Agent.create`,
    `Side`.
11. `src/CommandoWar.Sim/Events.fs` — `EventBody`, the `DomainEvent` ordering
    doc comment, `TreatWarningsAsErrors`.
12. `src/CommandoWar.Sim/Commands.fs` — `PlayerCommand`, `CommandRejection`,
    `Command.moveTo` / `moveToMany`.
13. `src/CommandoWar.Sim/Canonical.fs` — `FormatVersion` (doc comment),
    `writeAgent` (the `Route` "deliberately NOT written" note — the model for
    the `CommunicationAvailable` comment), `encode`, `topLevelSections`,
    `firstDifferingSection`.
14. `src/CommandoWar.Sim/Diagnostics.fs` — `Overlay` (the doc comment
    reserving a case per backlog item), `AgentMarker` (and the `Progress`
    "omitted at 0" precedent), `EventMarker`, `eventMarker`, `frame` vs
    `frameOf`, `reservationOverlays` / `obstructionOverlays` (the derivation +
    FS0025-filter precedent), `DiagnosticFrame`.
15. `src/CommandoWar.Sim/Phases.fs` (`Phase`, `Phases.order` — `Communication`
    is already slot 2, between `CommandIntake` and `Perception`).
16. `src/CommandoWar.Sim/Scenario.fs` — `RawDeployment`, `Deployment`,
    `RawScenario`, `Scenario`, `Scenario.validate`, `toDeployments`, the
    friendly-then-enemy deployment order.
17. `src/CommandoWar.Headless/{DiagnosticRender.fs, Corpus.fs, Program.fs}` —
    the two `Overlay`-exclusion filters and the `Ascii` / `Svg` overlay loops;
    `Corpus.rawScenario` (it now takes `enemies`; a `CommunicationAvailable`
    input enters here), `Corpus.all`, `Entry`; `Program.describeReplayError`.
18. `tests/CommandoWar.Sim.Tests/{SimulationTests.fs, DeterminismPropertyTests.fs,
    CorpusTests.fs, DiagnosticsTests.fs, ScenarioTests.fs, CanonicalHashTests.fs}`
    — the perception facts, `perceptionCaseGen` / `statesOf` /
    `toRecordedCommands`, the `[<Theory>]` corpus cross-check, the
    hand-built-overlay + golden facts, and the `WorldState` literals in the
    generators (each needs `CommunicationAvailable` on its agents — compile-only).
19. `content/replays/CORPUS.md` + the eight `.md` entries; `content/diagnostics/`
    goldens and `README.md` (only the new comms-blackout golden is added — no
    pre-existing golden or table moves).

## Dependencies

- TASK-020 / TASK-024 (command intake, done), TASK-026 (perception, done, on
  `main`).

## Central decisions (confirmed with Dave 2026-09-07)

### Decision A — a static deterministic reachability check, no PRNG draw — CONFIRMED

A per-recipient reachability check that is a pure function of authored scenario
data. **No PRNG draw** — the deterministic stream's first gameplay consumer
stays combat spread (B-019).

- New `AgentState.CommunicationAvailable: bool`, default `true`. Set `false`
  only by an authored scenario "comms blackout".
- Zero delivery delay: when comms are available, the order is delivered the
  **same tick** it is accepted (in the Communication phase, one phase after
  intake).
- **`CommunicationAvailable` is static authoritative data and is excluded from
  `Canonical.encode`**, exactly as `WorldState.Terrain` is (ADR-0002
  amendment). It is a pure function of the validated scenario, identical in
  both runs at tick 0, never mutated during a run. `writeAgent` gains a
  "deliberately NOT written" comment mirroring the `Route` note.
  `Canonical.FormatVersion` is **not** bumped.
- A divergence in comms-derived behaviour surfaces in the hash **within one
  tick** through the recipient's `Position` (the same argument the ADR-0002
  amendment makes for `Terrain`), and as a behavioural test failure — not as a
  hash of the static flag.
- **Deferred to B-016b** (named, not built): radio range from a command origin
  / HQ entity; non-zero delivery delay (a delivered-order queue that survives
  across ticks); dynamic jamming; radio-destroyed; per-agent comms state that
  changes during a run; report aging beyond `StaleAfter` / `ExpireAfter`. When
  comms availability becomes per-tick mutable, B-016b bumps
  `Canonical.FormatVersion` and adds it to the canonical image — the same
  transition rule the amendment already states for `Terrain`.

**Carrier for the authored blackout.** `RawDeployment` gains
`CommunicationAvailable: bool` and `Deployment` gains the same;
`Scenario.validate`'s `toDeployments` carries it through (no new
`ScenarioError` — a `bool` cannot be malformed); `World.ofScenario` maps it
onto the agent via `Agent.create`; `World.create` / `Setup.sixAgentWorld` /
`Agent.create` default it `true`. Every existing `RawDeployment` construction
site (in `Corpus.fs`, `ScenarioTests.fs`, `DemoScenario` / `LosDemo` /
`PathDemo`) gains the field `= true`, which is behaviourally identical.
`Agent.create` gains a `?communicationAvailable` optional parameter defaulting
`true` so its many existing call sites (tests, `Setup`, the property
generators) are unaffected.

### Decision B — the pending order is transient, not canonical state — CONFIRMED

`commandIntake` stops writing `AgentState.Destination` directly. It validates
as today, emits `CommandAccepted` as today, and records each accepted
`(commandId, recipient, targetCell)` as a **pending order in a
`StepState`-local list** — the same "mutable-but-contained accumulator that
never escapes `step`" the pipeline already uses. The Communication phase then,
per pending order in ascending `(recipient, commandId)` order:

- recipient `CommunicationAvailable = true`: write `AgentState.Destination`
  (copy-before-write, the `commandIntake` idiom);
- recipient `CommunicationAvailable = false`: emit `OrderUndelivered`
  (Decision D) and drop the order. The recipient keeps whatever `Destination`
  it already held — an undelivered new order does not cancel an order already
  in progress.

**This never crosses a tick boundary** (zero delay): a pending order is
created in phase 1 and fully consumed in phase 2 of the same tick. It is
therefore **not** authoritative per-tick state — `WorldState` gains no field,
`AgentState` gains no `PendingOrder`, `Canonical.FormatVersion` stays **3**.
Consistent with TASK-024 (`WorldState` holds no command history).

**Behaviour-neutrality.** Nothing in `Phases.order` runs between
`CommandIntake` (slot 1) and `Communication` (slot 2), so moving the
`Destination` write from phase 1 to phase 2 changes no observable end-of-tick
state. Every committed scenario has `CommunicationAvailable = true` for every
agent, so every accepted order is still delivered the same tick, writing the
same `Destination`. **Per-tick state hashes, tick counts, and event counts are
byte-identical on all nine existing entries.** `cwheadless corpus` /
`cwheadless fixture` reproduce their committed tables with no `--regenerate`.

Rejected alternative (recorded): a canonical `AgentState.PendingOrder` +
`Canonical.FormatVersion` 3 → 4, to make B-016b delayed delivery "free". Too
speculative (no B-016b design exists) and it would force a TASK-026-scale hash
re-pin now. B-016b bumps the format when delayed delivery lands with a real
design.

### Decision C — report aging is out of scope; split to B-016b — CONFIRMED

TASK-026 deferred "report aging beyond `StaleAfter` / `ExpireAfter`" to B-016.
That work has no concrete design and bundling it makes this task L. A new
**B-016b** row collects radio range, non-zero delivery delay, dynamic jamming,
radio-destroyed, and report aging. TASK-027 is communication-delivery only.

### Decision D — `OrderUndelivered` only; the success event is deferred to B-016b — CONFIRMED

`OrderUndelivered of command: CommandId * recipient: AgentId * reason:
DeliveryFailure` — emitted by the Communication phase when a recipient cannot
be reached. `DeliveryFailure` is a DU; for this task's scope its only case is
`UnableToCommunicate` (the name matches `docs/05` section 7's
`DecisionReason.UnableToCommunicate`), leaving room for `OutOfRange` /
`Jammed` / `RadioDestroyed` in B-016b.

**No `OrderDelivered` success event.** With zero delay and comms available,
`CommandAccepted` (emitted at intake, carrying the destination cell) already
records "the simulation accepted this order for this recipient this tick", and
the `Destination` write records delivery. A separate `OrderDelivered` on the
very next phase of the same tick carries no information `CommandAccepted` does
not — it is the "flood of low-value events" `docs/04` section 14 warns
against, until B-016b makes delivery lag acceptance. Failure is the
asymmetric, information-bearing case (like `MovementBlocked` /
`MovementObstructed`). B-016b adds `OrderDelivered` when it becomes
meaningful. **Consequence:** no new event fires for any of the nine existing
entries (all comms-available), so there is no event-count re-pin — the corpus
and fixture stay byte-identical.

**Ordering.** `OrderUndelivered` is emitted **after** command-intake events
(`CommandAccepted` / `CommandRejected`, ascending command id) and **before**
this tick's `ContactObserved` (Communication runs before Perception in
`Phases.order`). Within the Communication phase, `OrderUndelivered` events are
emitted ascending by `(recipient, commandId)`. The `Events.fs` `DomainEvent`
ordering doc comment is updated.

**`CommandAccepted` stays at intake** — the order was accepted into the
pipeline by the simulation; delivery to the recipient is the separate
Communication-phase fact.

### Decision E — no ADR — CONFIRMED

Decision A adds static authoritative data governed by the existing ADR-0002
amendment (excluded from the canonical image while static; the amendment's
transition rule covers B-016b). Decision B introduces no new canonical state.
`docs/08` section 6 already lists communication under P3 Required work, so no
gate-obligation edit. The design is recorded in this task file and the ledger.

## Diagnostics

`AGENTS.md`'s diagnostic-extension rule **applies**: this task adds
authoritative state that affects agent behaviour (`CommunicationAvailable`)
and a new failure fact (`OrderUndelivered`). Required, both parts:

- **`AgentMarker.CommunicationAvailable: bool`** — carried on every agent
  marker, printed in the `Ascii` roster line and drawn in `Svg` **only when
  `false`** (the `AgentMarker.Progress` "omitted at 0" precedent).
- **`Overlay.UndeliveredOrder of recipient: AgentId * at: Cell * command:
  CommandId`** — `Diagnostics.frameOf` derives one per `OrderUndelivered`
  event this tick (one per distinct recipient cell, the `obstructionOverlays`
  precedent). `Diagnostics.frame` (bare `WorldState`) emits none — an
  undelivered order is a this-tick event fact, not standing state, exactly
  like `Reserved` / `Obstructed`.
- `eventMarker` gains an `OrderUndelivered` arm — `Cells = [| recipientCell |]`.
- Wire every new case through `DiagnosticRender.fs`: the `sightRays` /
  `plannedPaths` exclusion filters in `Ascii`, the `Ascii` overlay text loop,
  the `Svg` drawing loop; and in `Diagnostics.fs` the `reservationOverlays` /
  `obstructionOverlays` `e.Body` filters. Expect FS0025 on first build; fix
  each with the intended branch, never a wildcard; list the sites and fixes in
  the ledger (TASK-026 style).
- Commit a golden (`content/diagnostics/`) for the new comms-blackout corpus
  entry at the tick the order is issued and fails: the `order-undelivered`
  event marker, the `UndeliveredOrder` overlay, and the blacked-out agent
  shown not moving (and, if a second friendly is in the entry, its
  `CommunicationAvailable = true` roster contrast). Byte-compared by
  `DiagnosticsTests.fs`, plus a hand-built overlay unit test (the `Reserved` /
  `Obstructed` / `KnownContact` precedent). Add the regeneration command to
  `content/diagnostics/README.md`.

## Allowed scope

- `src/CommandoWar.Sim/Domain.fs` — `AgentState.CommunicationAvailable: bool`
  (static, doc comment distinguishing it from the canonical fields and from
  `Destination`); `Agent.create` gains `?communicationAvailable` defaulting
  `true`.
- `src/CommandoWar.Sim/Simulation.fs` — a real `communication` phase function
  in its `Phases.order` slot; the `runPhase` `Communication` arm; a
  "Realised by TASK-027" header comment. `commandIntake` reworked to append to
  a `StepState`-local pending-order list instead of writing `Destination`; its
  phase comment updated to state the behaviour change precisely (an accepted
  order now takes effect one phase later — still the same tick when comms are
  available). `StepState` gains the pending-order list field; `step` seeds it
  empty.
- `src/CommandoWar.Sim/Events.fs` — `OrderUndelivered`; the `DeliveryFailure`
  DU; the `DomainEvent` ordering doc comment.
- `src/CommandoWar.Sim/Canonical.fs` — a `writeAgent` comment only
  (`CommunicationAvailable` deliberately not written, the `Route` precedent).
  `FormatVersion` and `encode` **unchanged**.
- `src/CommandoWar.Sim/Scenario.fs` — `RawDeployment.CommunicationAvailable`,
  `Deployment.CommunicationAvailable`, `toDeployments` carry-through; no new
  `ScenarioError`.
- `src/CommandoWar.Sim/Diagnostics.fs` — `AgentMarker.CommunicationAvailable`;
  `Overlay.UndeliveredOrder`; `eventMarker` arm; the `frameOf` derivation; the
  `Overlay` doc comment.
- `src/CommandoWar.Headless/DiagnosticRender.fs` — the new match branches
  (both `Ascii` filters, `Ascii` text, `Svg` drawing, the roster/circle
  marker rendering).
- `src/CommandoWar.Headless/Corpus.fs` — a `CommunicationAvailable`-per-agent
  input on `rawScenario` (or a `commsBlackout: int list` parameter — the
  smaller change wins; default no blackout for the eight existing entries,
  behaviourally identical), and one new `Entry`: a friendly with
  `CommunicationAvailable = false` ordered to move, whose order is never
  delivered. `Corpus.all` row, `CORPUS.md` row, committed `.cwlog` + `.md`.
- `src/CommandoWar.Headless/{DemoScenario,LosDemo,PathDemo}.fs` — the
  `RawDeployment` literals gain `CommunicationAvailable = true` (compile-only,
  behaviourally identical).
- `content/replays/` — the new entry's `.cwlog` + `.md` + `CORPUS.md` row.
  **No other file in this directory moves.**
- `content/diagnostics/` — the new comms-blackout golden(s) + `README.md`.
  **No pre-existing golden moves.**
- `tests/CommandoWar.Sim.Tests/` —
  - `SimulationTests.fs`: an order to a reachable recipient is delivered the
    same tick it is accepted (`Destination` set after the tick, no
    `OrderUndelivered`, movement begins the next tick); an order to an
    unreachable recipient emits `OrderUndelivered` (reason
    `UnableToCommunicate`) and sets no `Destination` (the agent never moves);
    a multi-recipient `MoveTo` with one unreachable recipient delivers to the
    reachable ones and emits `OrderUndelivered` only for the cut-off one; an
    undelivered order does not cancel a `Destination` the recipient already
    held; determinism (two runs byte-identical events + hashes).
  - `DeterminismPropertyTests.fs`: `CommunicationAvailable = true` added to the
    `Agent.create` calls / `WorldState` literals in the existing generators
    (compile-only — `Agent.create`'s default covers it, so likely no change);
    a new generator (or an extension of `perceptionCaseGen`) that sometimes
    marks a friendly recipient unreachable; a property (`MaxTest ≥ 200`):
    every agent holding a `Destination` after a tick either started with it or
    received it from a `CommandAccepted` with no matching `OrderUndelivered`
    on a tick ≤ now, and **no agent with `CommunicationAvailable = false` ever
    holds a `Destination` that a command wrote this run**. Properties 1–6
    unmodified.
  - `ScenarioTests.fs`: `CommunicationAvailable` round-trips through
    `Scenario.validate` — default `true`, an authored `false` survives onto
    the `Deployment`.
  - `CanonicalHashTests.fs`: flipping one agent's `CommunicationAvailable`
    leaves `Canonical.encode` and the hash **unchanged** (the static-data
    exclusion), but the order it suppresses changes the hash within one tick
    via `Position`.
  - `DiagnosticsTests.fs`: the hand-built `UndeliveredOrder` overlay fact, the
    `AgentMarker.CommunicationAvailable` fact, and the new golden comparison;
    the FS0025 fixes at the `Overlay` match sites.
  - `CorpusTests.fs`: the new comms-blackout entry theory case (additive — the
    `[<Theory>]` `MemberData` is `Corpus.all`).

## Forbidden scope

- Radio range, a command origin / HQ entity, non-zero delivery delay, a
  delivered-order queue that survives across ticks, dynamic jamming,
  radio-destroyed, an `OrderDelivered` success event — **B-016b**.
- Report aging beyond the TASK-026 `StaleAfter` / `ExpireAfter` bands —
  **B-016b**.
- Order appraisal, `OrderDisposition`, staged reasons, reappraisal triggers —
  **B-017**. The Communication phase decides *delivery*, not *acceptance by
  the agent*.
- Combat, line of fire, suppression — **B-019**. Any PRNG draw.
- Enemy doctrine reacting to communication — **B-022**.
- Any `Sight.fs` / `Perception.fs` change — consume `TacticalKnowledge` /
  `VisibleContacts`, do not touch the modules.
- A `SquadStore`, formation grouping, or per-agent private beliefs
  (`docs/05` section 17; B-011d).
- Any `Canonical.FormatVersion` bump, any hash re-pin, any change to
  `content/replays/*` beyond the new entry, any change to
  `content/fixtures/SPIKE-FIXTURE.md`, any pre-existing
  `content/diagnostics/` golden.
- Editing the client spikes, `src/_scratch`, `bench/`,
  `content/benchmarks/BASELINE.md`.
- A moved per-tick hash, tick count, or event count on any of the nine
  existing entries (`spike-fixture`, the seven `.cwlog` entries,
  `envelope-full`) is a **stop-and-report** finding — it means the
  `commandIntake` rework changed comms-available behaviour, which it must not.

## Acceptance criteria

- [x] The Communication phase (12.2) is a real phase function in
      `Simulation.fs`, in its existing `Phases.order` slot, with a
      "Realised by TASK-027" header comment; `runPhase` has a real
      `Communication` arm. `commandIntake` records an accepted order as a
      pending order (`StepState.PendingOrders`) rather than writing
      `Destination`; its phase comment states the behaviour change precisely.
- [x] `AgentState.CommunicationAvailable: bool` (default `true`), a static
      authoritative field **excluded from `Canonical.encode`** (`writeAgent`
      unchanged bar a comment); `Agent.create` defaults it; a doc comment
      distinguishes it from the canonical fields.
- [x] `RawDeployment` / `Deployment` carry `CommunicationAvailable`;
      `Scenario.validate` round-trips it (default `true`, authored `false`
      survives — `ScenarioTests` "communication availability round-trips
      through validation"); `World.ofScenario` wires it onto the agent; no new
      `ScenarioError`.
- [x] A `SimulationTests` fact: an order to a reachable recipient is delivered
      the same tick it is accepted — `Destination` set, no `OrderUndelivered`,
      and (Navigation runs at phase 7) the agent steps one cell that tick,
      exactly the pre-TASK-027 behaviour.
- [x] A `SimulationTests` fact: an order to an unreachable recipient
      (`CommunicationAvailable = false`) emits `OrderUndelivered` (reason
      `UnableToCommunicate`) and sets no `Destination`; the agent never moves.
- [x] A `SimulationTests` fact: a multi-recipient `MoveTo` where one recipient
      is unreachable delivers to the reachable recipients and emits
      `OrderUndelivered` only for the cut-off one.
- [x] A `SimulationTests` fact: an undelivered order does not cancel a
      `Destination` the recipient already held.
- [x] A `SimulationTests` fact: two runs of the same world + commands emit
      byte-identical events and hashes ("communication delivery is
      deterministic across two runs").
- [x] `OrderUndelivered of command * recipient * reason` and the
      `DeliveryFailure` DU exist; the `DomainEvent` ordering doc comment names
      the Communication events' position (after command-intake events, before
      `ContactObserved`, ascending `(recipient, command)`). No `OrderDelivered`
      event.
- [x] A `DeterminismPropertyTests` property 7 (`MaxTest = 200`, `commsCaseGen`
      marks a random subset of agents unreachable): a blacked-out agent never
      holds a `Destination` and never leaves its start cell; every
      `OrderUndelivered` names a blacked-out recipient and pairs with a
      same-tick `CommandAccepted`; no comms-available agent is ever reported
      undelivered. Properties 1–6 unmodified.
- [x] A `CanonicalHashTests` fact: `CommunicationAvailable` is not in the
      canonical image (flipping it alone leaves `Canonical.encode`, the hash,
      and `FormatVersion` unchanged), but the order it suppresses changes the
      hash within one tick via `Position`.
- [x] New corpus entry `lost-comms` (a friendly with
      `CommunicationAvailable = false` ordered to move; `OrderUndelivered`
      fires, the agent never moves): `CORPUS.md` row, committed `.cwlog` +
      `.md`, passes `CorpusTests.fs` `[<Theory>]` and `cwheadless corpus`
      (9/9 PASS).
- [x] Diagnostics: `AgentMarker.CommunicationAvailable` (rendered only when
      `false`) + `Overlay.UndeliveredOrder` derived in `frameOf`, rendered in
      `Ascii` and `Svg`, covered by two hand-built `DiagnosticsTests` facts and
      the committed `content/diagnostics/lost-comms-tick-001.*` golden; FS0025
      sites listed in the ledger with their fixes.
- [x] `cwheadless corpus` (the eight pre-existing entries) and
      `cwheadless fixture` are **byte-identical** before and after, with and
      without `--regenerate`; `git diff content/replays` shows only the new
      entry + the `CORPUS.md` row; `cwheadless replay-file
      content/replays/envelope-full.cwreplay` checkpoints OK at `canonical 3`.
- [x] `dotnet build CommandoWar.slnx -c Release` = 0/0;
      `dotnet list src/CommandoWar.Sim package --include-transitive` =
      `FSharp.Core` only; source scan of `src/CommandoWar.Sim` clean.
- [x] `dotnet test CommandoWar.slnx -c Release` green — `216 → 228`; the added
      facts and property 7 (`MaxTest = 200`) named in the ledger.
- [x] Docs updated: `docs/03` section 7, `docs/04` sections 11 / 12.1 / 12.2 /
      14 / 17 / 20, `docs/05` section 5, `docs/09` sections 2.1 / 2.2 / 2.3,
      `docs/11` (TASK-027 row, B-016 → done, new B-016b row, B-017 unblocked
      note), `docs/12` (index row + detail file, "Green tests" `216 → 228`),
      `PROJECT_STATE.yaml`.

## Required verification

- `dotnet build CommandoWar.slnx -c Release` (0/0).
- `dotnet test CommandoWar.slnx -c Release` before any edit (record the count)
  and after (state the new count, name each added fact and the property's case
  count).
- `cwheadless corpus` + `cwheadless fixture` before any edit (record the
  current hashes / event counts) and after: byte-identical for the eight
  pre-existing entries and the fixture, no `--regenerate` needed; running
  `corpus --regenerate` then `git diff content/replays` shows only the new
  entry added; re-running `--regenerate` is a zero diff.
- Generate the new `content/diagnostics/` golden; confirm no pre-existing
  golden moved (`git status` shows only the new files + `README.md`).
- `cwheadless replay-file content/replays/envelope-full.cwreplay` — checkpoints
  OK, `canonical 3`.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive` — `FSharp.Core` only.
- source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`.
- `git status --porcelain` — matches "Allowed scope"; nothing under the client
  spikes, `src/_scratch`, `bench/`, `content/benchmarks/`.

## Evidence to capture

- test summary (before/after counts); the new `SimulationTests` facts and the
  property by name and case count;
- the new corpus entry's initial/final hash, tick count, event count, and the
  failure it exercises;
- the diagnostics golden and the FS0025 sites with their fixes;
- confirmation that `cwheadless corpus` / `fixture` are byte-identical for the
  nine pre-existing entries (no re-pin of any kind).

## Rollback or removal

`CommunicationAvailable`, the Communication phase body, the `commandIntake`
rework, `OrderUndelivered`, the `DeliveryFailure` DU, the overlay / marker, the
new corpus entry, and the tests are additive. Reverting: restore
`commandIntake`'s direct `Destination` write, return `communication` to a
no-op arm, delete the field / event / overlay / renderer branches / entry /
goldens / tests. Nothing else changes — no hash, table, or golden was
re-pinned.

## Documentation updates

- this task file (Outcome, status, acceptance boxes);
- `docs/11_BACKLOG.md`: TASK-027 row; B-016 → `done` (with a note that the
  communication-constraint half closed it and range/delay/jamming/aging moved
  to B-016b); a new **B-016b** row; B-017 unblocked note;
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
  "Green tests" count. No "Pinned facts" change (no format bump, no hash
  re-pin);
- `docs/04_SIMULATION_SPEC.md` sections 12.1 (intake records a pending order),
  12.2 (realisation block), 14 (`OrderUndelivered`), 20 (if an invariant is
  added);
- `docs/05_COMMAND_AND_AGENT_AI.md` section 3 or 5 realisation note (delivery
  is now a real fact stage 1 reads);
- `docs/03_ARCHITECTURE.md` section 7 realisation note;
- `docs/09_TEST_STRATEGY.md` sections 2.1 (delivery facts), 2.3 (the "radio
  loss" / "Lost communication" partial-realisation note);
- `PROJECT_STATE.yaml` `active_work` when this becomes the active task;
- no ADR (Decision E).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
