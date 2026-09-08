# TASK-028: Staged order appraisal and typed reasons

Status: review (implemented 2026-09-08 on branch `task-028-order-appraisal`;
central decisions A–H confirmed with Dave 2026-09-08 before the phase bodies)
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises backlog B-017 and is the G3 keystone
Size: M

## Outcome (2026-09-08)

Implemented on branch `task-028-order-appraisal` off the TASK-027 merge on
`main` (committed locally, not pushed). TASK-027 was accepted and merged first
(recorded acceptance, `--no-ff` merge, branch deleted). All eight central
decisions confirmed with Dave and implemented as recommended (Decision B: Dave
asked for the recommendation — excluded `Discipline` from the canonical image,
matching the `CommunicationAvailable` precedent).

The Appraisal phase (12.5) is a real phase over a new `Appraisal` leaf module.
The Communication phase writes a new canonical `AgentState.Order` and resets
`AgentState.Disposition` instead of writing `Destination`; the Appraisal phase
runs stages 1–4 (stage 1 guaranteed upstream; stage 2 `Pathfinding`
feasibility; stage 3 route exposure to *known* threats; stage 4 exposure vs a
`Discipline` / `RiskTolerance` / `Urgency` threshold), produces an
`OrderDisposition` (`Accepted | Refused of reasons | Unable of reasons`), emits
`OrderAppraised`, and on `Accepted` writes `Destination`. New static
`AgentState.Discipline` (excluded from `Canonical.encode`). `AppraisalConfig`
holds all thresholds as integer literals; no PRNG draw.

`Canonical.FormatVersion` **3 → 4** with a full re-pin — behaviour-neutral for
movement (tick counts unchanged on all ten entries; event counts move by one
`OrderAppraised` per order), except `blocked-goal` (now `Unable(NoKnownRoute)`
at appraisal instead of `MovementBlocked` at navigation — same tick/event
count) and `lost-comms` (order dropped at Communication, no appraisal). New
`exposed-approach` corpus entry + golden (the G3 divergence: two friendlies,
`Discipline` 1 and 6, same exposed order → one `Refused`, one `Accepted`); new
`blocked-goal-tick-001` golden.

`228 → 242` green (+9 `SimulationTests`, +3 `DiagnosticsTests`, +1
`DeterminismPropertyTests` property 8 at 200 cases, +1 `CorpusTests` theory
case). `dotnet build` 0/0;
`-- corpus` 10/10; `-- fixture` format 4, 34 events; `-- replay-file
envelope-full` OK at canonical 4; `src/CommandoWar.Sim` packages `FSharp.Core`
only. No ADR.

Full detail:
`docs/ledger/2026-09-08-TASK-028-order-appraisal-and-typed-reasons.md`.

## Objective

Turn the **Appraisal phase (12.5)** — currently a no-op arm in
`Simulation.runPhase` — into a real phase that decides, per delivered order,
whether the agent will carry it out, and records **why** in a typed vocabulary.
Split the `docs/04` section 12.2 / 12.5 boundary the spec already draws:
the Communication phase (TASK-027) **delivers** an order; the Appraisal phase
**judges** it, producing an `OrderDisposition` with structured reasons and, on
`Accepted`, writing `AgentState.Destination` (the executor is still "move
toward `Destination`" until B-018).

This realises `docs/08` section 6 P3 Required work "implement explicit order
appraisal and typed reasons" and is the direct precondition for the G3 gate:
G3 evidence requires "at least two agents appraise the same intent differently
for inspectable reasons" (`docs/08` section 6, `docs/07` section 9 criterion 2)
and "scenario traces readable enough to diagnose all decisions". It also
delivers steps 1–3 of the `docs/07` section 8 canonical refusal sequence
(order across an exposed approach → the agent identifies a known machine-gun
lane → the order is refused with `RouteTooExposed`); the full sequence
(suppress / reroute / reissue / accept) is B-023.

Scope is deliberately narrow. Stages 1 (comprehension / authority — already
guaranteed upstream), 2 (physical feasibility — `Pathfinding` route existence),
3 (tactical viability — **route exposure to *known* threats only**), and 4
(resolve — exposure vs a bounded threshold from `Discipline` + the order's
`RiskTolerance` / `Urgency`). Stage 5 (safer adaptation) is deferred. Outcomes
are `Accepted`, `Refused RouteTooExposed`, `Unable NoKnownRoute` — `Adapted`
and `Delayed` are B-018 / B-021 and get no DU cases. Discipline is the only
psychological dimension; trust, stress, and suppression have no sources without
combat and stay B-020 / B-021.

## Why this task exists

- `docs/08_ROADMAP_AND_GATES.md` section 6 P3 Required work: "implement
  explicit order appraisal and typed reasons". Separate Required-work line
  "implement suppression, stress, discipline, trust, and reappraisal triggers"
  is **B-021** — this task lands only the static-`Discipline` slice and the
  "new order received" reappraisal trigger.
- `docs/04_SIMULATION_SPEC.md` section 12.5 is a written-but-unrealised phase:
  "appraise newly received orders; reappraise only on material triggers, not
  every tick without need; emit outcome and structured reasons."
- `docs/03_ARCHITECTURE.md` section 7 step 5 ("Appraise new or materially
  changed orders") has no realisation note.
- `docs/04` section 14 lists "order appraisal outcome" as a minimum event
  category with no realisation yet.
- `docs/05_COMMAND_AND_AGENT_AI.md` sections 5–7 specify the staged appraisal,
  the `OrderDisposition` algebra, and the `DecisionReason` vocabulary; none
  exists in code.
- G3 evidence (`docs/08` section 6): "at least two agents appraise the same
  intent differently for inspectable reasons" — Discipline-driven divergence
  on the same order is the cleanest such demonstration, and TASK-026 already
  provided the other half (different *knowledge*).
- Risk R-007 (AI too broad to debug): the mitigation is "explicit staged
  appraisal, typed commitments, finite executors". This is the "explicit
  staged appraisal" half — five named stages, integer thresholds in one
  config structure, one typed outcome per order, no utility sum, no planner.
- Risk R-023 (enemy AI cheats): appraisal reads `WorldState.TacticalKnowledge`
  (the friendly squad's *known* picture), never authoritative hostile
  positions, so a refusal is grounded in what the squad has actually observed.

Depends only on TASK-020 / TASK-024 (command intake, done), TASK-026
(perception, done, on `main`), and TASK-027 (communication and order delivery,
accepted and merged to `main` 2026-09-08). Directly unblocks B-018
(commitments / executors), B-021 (stress / trust / reappraisal), B-023
(canonical refusal sequence).

## Required reading

Read in this order; verify each path and inspect the source before trusting a
filename (`AGENTS.md`).

1. `PROJECT_STATE.yaml`, `AGENTS.md`.
2. `docs/ledger/2026-09-07-TASK-027-communication-and-order-delivery.md` — the
   phase-split precedent (Communication writes `AgentState.Destination` from a
   `StepState`-local pending order; TASK-028 reworks that so the delivered
   order goes to `AgentState.Order` and Appraisal writes `Destination`), and
   the FS0025 discipline (every exhaustive match armed with the intended
   branch, never a wildcard; sites listed in the ledger).
3. `docs/ledger/2026-09-07-TASK-026-perception-and-tactical-knowledge.md` — the
   `Canonical.FormatVersion` bump + full **behaviour-neutral** re-pin
   procedure (the old/new hash + tick/event-count table), the `PerceptionConfig`
   module-literal tuning precedent, and the FS0025 site table.
4. `decisions/ADR-0002-SIMULATION-BOUNDARY.md` — the 2026-09-03 amendment
   "Static authoritative data and the canonical image": it governs why
   `AgentState.Order` / `AgentState.Disposition` **enter** `Canonical.encode`
   (genuine per-tick memory — the `TacticalKnowledge` precedent) and why
   `Discipline` **stays out** (static authored data — the
   `CommunicationAvailable` precedent), and why `Canonical.FormatVersion`
   bumps 3 → 4.
5. `docs/04_SIMULATION_SPEC.md` sections 2 (`IssuedAtTick` as "a future
   appraisal input"), 9 (line of sight and cover — the exposure input), 11
   (agent state — "current order and commitment", "discipline"), 12.2
   (Communication — TASK-027, the phase before), 12.5 (Appraisal — the no-op
   this task realises), 12.6 (Commitment — B-018, the consumer), 12.7
   (Navigation — reads `Destination`), 13 (commands — `Urgency` /
   `RiskTolerance`, "inert beyond validation until B-017"), 14 (events —
   "order appraisal outcome"), 17 (canonical hashing), 20 (invariants —
   "every refusal and adaptation contains at least one structured reason").
6. `docs/05_COMMAND_AND_AGENT_AI.md` sections 2 (the pipeline), 3 (the
   knowledge appraisal reads), 4 (orders — Move postures quick / cautious), 5
   (the five appraisal stages), 6 (`OrderDisposition`), 7 (`DecisionReason` —
   the doc's `ContactId` is `AgentId` in the code; `Capability` /
   `TacticalAdaptation` / `ResumeCondition` do not exist and are not created),
   8 (minimal psych model — Discipline), 13 (developer trace), 14 (reappraisal
   triggers), 15 (tuning rules — integer thresholds, one config structure,
   hysteresis), 16 ("Exposed road" / "Covered alternative" / "Unknown threat"
   / "Physical inability" scenarios), 17 (deferred — utility selection).
7. `docs/07_VERTICAL_SLICE.md` sections 8 (the canonical refusal sequence,
   steps 1–8; TASK-028 delivers steps 1–3), 9 (functional acceptance criteria
   2, 3, 11).
8. `docs/08_ROADMAP_AND_GATES.md` section 6 (P3 Required work + G3 evidence).
9. `docs/09_TEST_STRATEGY.md` sections 2.1 ("appraisal stage outcomes"), 2.2
   ("appraisal never returns `Accepted` after a hard feasibility failure"),
   2.3 ("exposed-road refusal", "suppression reverses refusal", "covered
   route adapts accepted order"), 8.
10. `docs/10_RISK_REGISTER.md` R-007, R-023.
11. `src/CommandoWar.Sim/Simulation.fs` — the `communication` phase (reworked
    here), `commandIntake`, `runPhase` (`Appraisal` currently in the no-op
    list), `StepState`, `Phases.order` (`Appraisal` is slot 5, after
    Perception / Tactical knowledge, before Commitment and Navigation),
    `navigationAndMovement` (reads `Destination`), the "Realised by TASK-NNN"
    phase-header-comment style.
12. `src/CommandoWar.Sim/Domain.fs` — `AgentState` (the `Route` /
    `VisibleContacts` derived-cache doc-comment style vs the `Progress`
    genuine-canonical-state style; `Contact`), `WorldState`, `Agent.create`,
    `Side`.
13. `src/CommandoWar.Sim/Commands.fs` — `PlayerIntent`, `Urgency`,
    `RiskTolerance` (moved to `Domain.fs` by this task — see Decision A),
    `PlayerCommand` (`Urgency` / `RiskTolerance` / `IssuedAtTick`), `Command.moveTo`
    / `moveToMany`, `CommandRejection`.
14. `src/CommandoWar.Sim/Events.fs` — `EventBody`, `DeliveryFailure`, the
    `DomainEvent` ordering doc comment, `TreatWarningsAsErrors`.
15. `src/CommandoWar.Sim/Canonical.fs` — `FormatVersion` (the TASK-018 /
    TASK-026 bump doc-comment precedents), `writeAgent`, `writeContact`,
    `encode`, `topLevelSections`, `firstDifferingSection`.
16. `src/CommandoWar.Sim/{Sight.fs, Terrain.fs}` — `Sight.visible` /
    `Sight.trace`; `Terrain.cover` (directional low cover — the exposure
    mitigation), `Terrain.moveCost`; `src/CommandoWar.Sim/Pathfinding.fs` —
    `findWithin` (`Found` / `NoPath` / `BudgetExhausted` / `InvalidEndpoint` —
    stage-2 feasibility); `src/CommandoWar.Sim/Perception.fs` —
    `PerceptionConfig` (the tuning-module precedent), `Perception.chebyshev`,
    `visibleContactsFor`, `WorldState.TacticalKnowledge` shape.
17. `src/CommandoWar.Sim/Diagnostics.fs` — `Overlay` (the doc comment
    reserving a case per backlog item; the `KnownContact` derived-in-both-
    `frame`-and-`frameOf` precedent; the `undeliveredOrderOverlays` +
    FS0025-filter precedent), `AgentMarker`, `EventMarker`, `eventMarker`,
    `frame` vs `frameOf`, `DiagnosticFrame`.
18. `src/CommandoWar.Sim/Scenario.fs` — `RawDeployment` /
    `Deployment.CommunicationAvailable` (the per-deployment trait precedent for
    `Discipline`), `Scenario.validate`, `toDeployments`.
19. `src/CommandoWar.Headless/{DiagnosticRender.fs, Corpus.fs, Program.fs}` —
    the two `Overlay`-exclusion filters and the `Ascii` / `Svg` overlay loops;
    `Corpus.rawScenario` (takes `enemies` and `commsBlackout` — a `discipline`
    input enters here), `Corpus.all`, `Entry`; `Program.describeReplayError`
    and any event-describe.
20. `tests/CommandoWar.Sim.Tests/{SimulationTests.fs, DeterminismPropertyTests.fs,
    CorpusTests.fs, DiagnosticsTests.fs, ScenarioTests.fs, CanonicalHashTests.fs,
    ReplayTests.fs, FixtureTests.fs}` — the perception / communication facts,
    `perceptionCaseGen` / `commsCaseGen` / `statesOf`, the `[<Theory>]` corpus
    cross-check, the hand-built-overlay + golden facts, the `FormatVersion`
    re-pin literal sites, the `envelopeFullHashes` / fixture per-tick arrays.
21. `content/replays/CORPUS.md` + the nine `.md` entries +
    `envelope-full.{cwreplay,md}`; `content/fixtures/SPIKE-FIXTURE.md`; every
    `content/diagnostics/*` golden with a `(format 3)` / hash footer
    (all re-pinned by the 3 → 4 bump).

## Dependencies

- TASK-020 / TASK-024 (command intake, done), TASK-026 (perception, done, on
  `main`), TASK-027 (communication and order delivery, done, on `main`).

## Central decisions (confirmed with Dave 2026-09-08)

### Decision A — the delivered order lives on `AgentState.Order`; appraisal state is canonical; `Canonical.FormatVersion` 3 → 4 — CONFIRMED

The Communication phase (TASK-027) **stops writing `AgentState.Destination`**.
It writes a new canonical `AgentState.Order: ReceivedOrder option` from the
accepted command and resets `AgentState.Disposition` to `None`. The new
Appraisal phase reads `Order` + `WorldState.TacticalKnowledge` +
`AgentState.VisibleContacts` + `WorldState.Terrain` + the agent's `Discipline`,
produces an `OrderDisposition`, and on `Accepted` writes `Destination`. The
executor is unchanged — still "move toward `Destination`" (B-018 builds the
commitment store; this task does not).

```fsharp
// moved to Domain.fs from Commands.fs (needed before AgentState; Commands.fs
// keeps PlayerCommand / CommandRejection / Command and references these):
type PlayerIntent = MoveTo of target: Cell
type Urgency = Routine | Immediate
type RiskTolerance = Cautious | Standard | Aggressive

/// An order delivered to an agent, awaiting or holding an appraisal outcome
/// (TASK-028; docs/04 section 11 "current order"). Written by the Communication
/// phase from the accepted PlayerCommand, read by the Appraisal phase.
type ReceivedOrder =
    { Command: CommandId
      Intent: PlayerIntent
      IssuedAtTick: int64
      Urgency: Urgency
      RiskTolerance: RiskTolerance }

/// A typed reason for an appraisal outcome (docs/05 section 7). TASK-028
/// subset: only the two its staged checks can produce. The doc's `ContactId`
/// is `AgentId` in the code.
type DecisionReason =
    | NoKnownRoute
    | RouteTooExposed of threat: AgentId option

/// The agent's appraisal of its current order (docs/05 section 6). TASK-028
/// subset: Adapted / Delayed are B-018 / B-021 and get no case. Refused and
/// Unable carry a primary reason by construction, so the docs/04 section 20
/// invariant "every refusal contains at least one structured reason" holds
/// without a side check.
type OrderDisposition =
    | Accepted
    | Refused of primary: DecisionReason * supporting: DecisionReason[]
    | Unable of primary: DecisionReason * supporting: DecisionReason[]
```

`AgentState` gains:

```fsharp
      /// The order currently delivered to this agent (TASK-028). Written by
      /// the Communication phase (which also resets Disposition to None), read
      /// by the Appraisal phase. Genuine canonical per-tick state: it carries
      /// IssuedAtTick provenance and survives ticks so appraisal is not re-run
      /// every tick (docs/04 section 12.5 "reappraise only on material
      /// triggers"). Cleared when the order is fulfilled (the agent reaches
      /// the target) or superseded by a new order.
      Order: ReceivedOrder option
      /// This agent's appraisal outcome for Order (TASK-028). None until the
      /// Appraisal phase has run on the current Order. Genuine canonical
      /// per-tick state — the "already appraised, unchanged" fast path reads
      /// it.
      Disposition: OrderDisposition option
```

`Order` and `Disposition` **are genuine per-tick canonical memory** — they
survive ticks and cannot be recomputed from the current tick's positions (a
`Refused` disposition persists with the same reasons for an idle agent; an
`Accepted` order's `IssuedAtTick` is only in the envelope). Under the ADR-0002
amendment they **enter `Canonical.encode`**, exactly as
`WorldState.TacticalKnowledge` did (TASK-026). `Canonical.FormatVersion` bumps
**3 → 4** with a full re-pin.

**The re-pin is behaviour-neutral for movement.** Every committed scenario
issues at most one order per agent along a clear, enemy-free route (except
`blocked-goal` — Decision H) — so every such order is still `Accepted` and the
same `Destination` is written, one phase later but the same tick (nothing in
`Phases.order` between Communication at slot 2 and Appraisal at slot 5 reads
`Destination`; Perception and Tactical knowledge read `Position` only).
**Tick counts do not move on any entry**; a moved tick count is a
**stop-and-report** finding. **Event counts move by +1 `OrderAppraised` per
order** (every appraisal emits one, including the mundane `Accepted` — G3
needs the full trace), which is expected and re-pinned. `blocked-goal` is the
one entry whose event *content* changes (Decision H).

`Canonical.FormatVersion` bump chosen now rather than a transient design that
re-appraises every tick and keeps no memory: the memory is what makes
`docs/04` section 12.5's "reappraise only on material triggers" real, and a
transient design would still need the format bump for `Order`'s `IssuedAtTick`.

### Decision B — Discipline only; static per-deployment integer; excluded from `Canonical.encode` — CONFIRMED

The only psychological dimension in scope. `RawDeployment.Discipline: int` and
`Deployment.Discipline: int`; `Scenario.validate`'s `toDeployments` carries it
through (a new `ScenarioError.NegativeDiscipline of agent: int * value: int`
guards it — Discipline must be `>= 0`); `World.ofScenario` maps it onto a new
`AgentState.Discipline: int` via the `{ Agent.create … with … }` copy;
`Agent.create` / `World.create` / `Setup.sixAgentWorld` default it to
`AppraisalConfig.DisciplineDefault`.

Used only in the stage-4 resolve threshold. **Excluded from
`Canonical.encode`**, matching the `AgentState.CommunicationAvailable`
precedent (TASK-027): it is static authored scenario data, set once at tick 0,
never mutated during a run, identical in both runs — hashing it would move
every fixture hash for a constant, and it would be inconsistent to hash one
static per-deployment trait and not the other. The 3 → 4 bump is fully
justified by `Order` + `Disposition` alone. `writeAgent` gains a "deliberately
NOT written" comment (the `CommunicationAvailable` note). A Discipline-driven
behaviour difference still surfaces in the hash within one tick via the
recipient's `Position` and `Disposition`.

When B-021 makes Discipline (and stress / trust) dynamic, that task moves it
into the canonical image and bumps `Canonical.FormatVersion` — the same
transition rule the amendment states for `Terrain` and TASK-027 recorded for
`CommunicationAvailable`.

**Trust** — deferred entirely to B-021 (no `AgentState` field, no init value).
`docs/05` section 8: "the vertical slice may initialise trust and leave
dynamic trust changes minimal" — even the static init is out; the stage-4
threshold uses `Discipline` + the order's `RiskTolerance` / `Urgency` only.
**Stress / suppression** — B-020 / B-021 (no sources without combat).

### Decision C — stages 1–4 partial, stage 5 deferred; three outcomes — CONFIRMED

- **Stage 1 (comprehension / authority)** — trivially passed. Command intake
  already guarantees the recipient is `Friendly` (`UnauthorisedRecipient`
  rejects a `Hostile` one) and the target is in bounds (`TargetOutOfBounds`);
  TASK-027 guarantees "received" (an undelivered order writes no `Order`, so
  Appraisal never sees it). No stage-1 code, no stage-1 `DecisionReason`.
- **Stage 2 (physical feasibility)** — `Pathfinding.findWithin terrain
  agent.Position target budget` (`budget = Bounds.Width * Bounds.Height`, the
  `navigationAndMovement` value). `NoPath` / `BudgetExhausted` /
  `InvalidEndpoint` (a `MoveTo` onto an impassable cell) → `Unable(NoKnownRoute,
  [||])`. `Found` with `< 2` cells means the agent is already at the target →
  `Accepted` with no `Destination` write. Alive / conscious / mobile,
  capability, and ammunition all trivially pass — no models yet.
- **Stage 3 (tactical viability) — route exposure to *known* threats only.**
  Over the stage-2 route cells, sum the pressure from every contact in
  `WorldState.TacticalKnowledge` (the friendly squad's known picture — never
  authoritative hostile state, R-023): `exposure = Σ_cells Σ_threats
  threatPressure`, where a threat contributes only when the route cell is
  within `AppraisalConfig.ThreatEngagementRange` Chebyshev cells of the
  threat's `LastKnownCell` **and** `Sight.visible terrain threat.LastKnownCell
  routeCell`; the per-cell contribution is `max 0
  (AppraisalConfig.ExposedCellWeight - Terrain.cover terrain routeCell
  attackDir * AppraisalConfig.CoverMitigationPerLevel)`, where `attackDir` is
  the dominant cardinal of `(threat.LastKnownCell - routeCell)`. Track the
  single highest-contributing threat for `RouteTooExposed`'s `threat: AgentId
  option`. Suppression, wound severity, ally proximity, and "prerequisite
  active" — all B-019 / B-020 / B-021.
- **Stage 4 (resolve).** `threshold = AppraisalConfig.BaseResolve +
  AppraisalConfig.DisciplineResolveWeight * discipline + riskMod + urgencyMod`
  where `riskMod` is `RiskCautious` / `0` / `RiskAggressive` and `urgencyMod`
  is `0` / `UrgencyImmediate`. `exposure <= threshold` → `Accepted`; otherwise
  → `Refused(RouteTooExposed topThreat, [||])`. Personality (`Discipline`)
  moves the decision near the threshold; it never overrides stage 2.
- **Stage 5 (safer adaptation)** — deferred. No `Adapted` case, no route
  recomputation. `docs/05` section 5 stage 5 and `OrderDisposition.Adapted`
  are B-018.
- **`Delayed`** — deferred (needs a `ResumeCondition` mechanism — B-021). No
  case.

`DecisionReason` subset is exactly `NoKnownRoute | RouteTooExposed of threat:
AgentId option`. `TargetNotKnown` / `IssuerNotRecognised` / `RouteBlocked` /
the rest are **not** added — nothing in scope can trigger them, and
`AGENTS.md` forbids speculative type machinery. `UnableToCommunicate` stays a
`DeliveryFailure` case (TASK-027) — an undelivered order never reaches
Appraisal.

### Decision D — `AppraisalConfig` module of integer literals; integer exposure metric; no PRNG — CONFIRMED

A `PerceptionConfig`-style `[<RequireQualifiedAccess>] module AppraisalConfig`
of `[<Literal>]` integers in a new leaf `src/CommandoWar.Sim/Appraisal.fs`
(after `Perception.fs` / the moved intent types, before `Scenario.fs`). Module
literals not a `WorldState` field: the values affect authoritative outcomes
(so not `SimConfig`) but no scenario tunes them yet (`docs/05` section 15
"record every threshold in one configuration structure").

| constant | provisional value | meaning |
|---|---:|---|
| `DisciplineDefault` | `3` | Discipline of an agent from a deployment that authored none, and every non-scenario construction path. |
| `ThreatEngagementRange` | `8` | Chebyshev cells: a known threat within this of a route cell can put fire on it. Below `PerceptionConfig.SightRange` (10) — a threat can be seen before it is in range. |
| `ExposedCellWeight` | `10` | Tactical pressure one exposed route cell adds before cover. |
| `CoverMitigationPerLevel` | `4` | Pressure removed per authored `Terrain.cover` level on the covered edge. |
| `BaseResolve` | `20` | Pressure an agent of `Discipline 0` tolerates. |
| `DisciplineResolveWeight` | `15` | Added tolerance per Discipline point. |
| `RiskCautious` | `-15` | `RiskTolerance.Cautious` lowers the threshold. |
| `RiskAggressive` | `20` | `RiskTolerance.Aggressive` raises it. |
| `UrgencyImmediate` | `20` | `Urgency.Immediate` raises the threshold (act despite pressure). |

Final values are fixed during implementation and recorded (with rationale) in
the ledger as a table, tied to making the Decision H divergence and the
`docs/09` section 2.3 "exposed-road refusal" / "covered alternative" test
shapes land. All arithmetic is integer; **no PRNG draw** (the deterministic
stream's first gameplay consumer stays combat spread, B-019 — assert
`Random.Draws` unchanged in the property).

`Appraisal.fs` exposes pure, total, integer-only helpers used by **both** the
Appraisal phase and `Diagnostics` (the `Perception.fs` precedent):
`Appraisal.routeExposure : Terrain -> Contact[] -> Cell[] -> int * AgentId
option` (total pressure + the top threat) and `Appraisal.appraise : Terrain ->
Contact[] -> discipline:int -> ReceivedOrder -> from:Cell -> budget:int ->
OrderDisposition * exposedCells:Cell[]` (the whole staged pipeline).

**Hysteresis** (`docs/05` section 15) — **deferred, named.** Hysteresis on the
`Accepted → Refused` edge only bites when an already-`Accepted` order is
re-appraised because its exposure changed; TASK-028's only reappraisal trigger
is "new order received" (Decision E), so a hysteresis band has nothing to act
on. B-021 adds the exposure-band trigger and the hysteresis constant with it.

### Decision E — reappraisal trigger in scope: "a new order is received" only — CONFIRMED

`docs/05` section 14 lists eight triggers. In scope for TASK-028:

- **A new order is received.** The Communication phase, when it writes `Order =
  Some newOrder`, sets `Disposition = None`. The Appraisal phase appraises
  exactly the agents where `Order.IsSome && Disposition.IsNone` — so a fresh
  order supersedes any prior disposition, and an already-appraised order
  (`Disposition.IsSome`) is the **fast path**: skipped, no `OrderAppraised`
  event. A superseding order also clears any `Destination` the prior
  (now-replaced) order left, before Appraisal re-decides.

Deferred, named, not built:

- **"the route becomes blocked"** — under static terrain a route verified at
  stage 2 cannot later disappear, so `MovementBlocked` cannot fire for an
  appraisal-`Accepted` order; persistent `MovementObstructed` (a parked agent)
  needs a stall counter (new state). Both B-021.
- knowledge change / exposure-band / suppression-band / wounded / support /
  leadership triggers — B-021 (no sources).

**Order lifecycle housekeeping (Appraisal phase, before the appraise loop):**
an agent with `Order = Some o`, `Disposition = Some Accepted`, `Destination =
None`, and `Position = o.Intent target` has **fulfilled** its order (movement
completed last tick) → clear `Order` and `Disposition`, no event. Every other
state is left for the fast path or the appraise branch.

### Decision F — `OrderDisposition` carries the reasons; `OrderAppraised of agent * command * disposition` — CONFIRMED

The reasons live on the `OrderDisposition` DU (Decision A) — `Refused` and
`Unable` carry `primary` + `supporting` by construction. The event is slim:

```fsharp
| OrderAppraised of agent: AgentId * command: CommandId * disposition: OrderDisposition
```

Emitted by the Appraisal phase for every appraisal, **including `Accepted`**
(G3 needs the full trace — `docs/07` section 9 criterion 11 "explain any
appraisal"). Never emitted on a fast-path (unchanged) tick or a
housekeeping-only tick.

**Ordering.** `OrderAppraised` events are a new group in the `DomainEvent`
ordering, emitted **after** this tick's `ContactObserved` / `ContactExpired`
(Appraisal runs after Perception / Tactical knowledge) and **before** movement
outcomes (Appraisal runs before Navigation), ascending by agent id. The
`Events.fs` ordering doc comment is renumbered.

### Decision G — no ADR — CONFIRMED

The ADR-0002 amendment "Static authoritative data and the canonical image"
governs both the `Canonical.FormatVersion` 3 → 4 bump (`Order` / `Disposition`
are newly-canonical authoritative state) and the `Discipline` exclusion
(static authored data). `docs/08` section 6 already lists appraisal under P3
Required work, so no gate-obligation edit. Phase order is unchanged (`Appraisal`
is already slot 5 in `Phases.order`). Design recorded in this task file and the
ledger.

### Decision H — the G3 evidence scenario, and `blocked-goal` repurposed — CONFIRMED

**New corpus entry `exposed-approach`** (the `docs/07` section 9 criterion 2
deliverable): an open ~12×8 map, one stationary hostile 2 (a machine-gun
position) at roughly `(10,4)`, two friendlies on open ground in line of sight
of it — agent 0 with **low `Discipline`** at `(1,3)`, agent 1 with **high
`Discipline`** at `(1,5)`. The `.cwlog` orders agent 0 to `(11,3)` and agent 1
to `(11,5)` on tick 1 (the `.cwlog` grammar cannot vary `Urgency` /
`RiskTolerance` per recipient, so the divergence is Discipline-driven — the
routes have near-identical exposure). Tick 1: Perception (phase 3) observes
hostile 2, Tactical knowledge (phase 4) adds it to
`WorldState.TacticalKnowledge`, Appraisal (phase 5) sees the known threat when
it judges each order — agent 0 `Refused RouteTooExposed (Some 2)` (no
`Destination`, never moves), agent 1 `Accepted` (`Destination` written, moves).
Committed `.cwlog` + `.md` + `CORPUS.md` row + `CorpusTests` `[<Theory>]` +
`cwheadless corpus`. Golden `content/diagnostics/exposed-approach-tick-001.*`:
two `order-appraised` event markers, two `OrderAppraisal` overlays showing the
divergence, the `KnownContact` overlay for contact 2, the exposed route cells.

**`blocked-goal` repurposed** (Dave's call): its target `(5,4)` is ringed by
impassable cells, so stage 2 now returns `Unable(NoKnownRoute)` at Appraisal
instead of the movement phase emitting `MovementBlocked` — which is exactly
`docs/09` section 2.2 ("appraisal never returns `Accepted` after a hard
feasibility failure") and `docs/05` section 16 "Physical inability". Tick count
(5) and event count (2) are unchanged; the tick-1 event becomes
`OrderAppraised(Unable(NoKnownRoute, [||]))` instead of `MovementBlocked`, and
per-tick hashes carry the new `Order` + `Disposition`. Its `.md` description
and golden (`content/diagnostics/lost-comms-tick-001.*` shares its tick-1 hash
— re-pin both) are updated. Direct `MovementBlocked` coverage stays in
`SimulationTests` (the movement-phase logic is unchanged, just no longer
reached by this corpus path).

## Phase bodies

### `commandIntake` (12.1) — one change

`StepState.PendingOrders` changes from `(CommandId * AgentId * Cell) list` to
`(AgentId * ReceivedOrder) list`. At acceptance, `pending.Add(recipient, {
Command = cmd.Id; Intent = cmd.Intent; IssuedAtTick = cmd.IssuedAtTick;
Urgency = cmd.Urgency; RiskTolerance = cmd.RiskTolerance })`. `CommandAccepted`
is still emitted here, unchanged. No other change.

### `communication` (12.2) — reworked write target

For each `(recipient, order)` in ascending `(recipient, order.Command)` id
order:

- `CommunicationAvailable = true` → `agents.[idx] <- { agents.[idx] with Order
  = Some order; Disposition = None }` (copy-before-write). **Not** `Destination`
  — that is the Appraisal phase's job now. A `Destination` the recipient
  already held from a prior Accepted order is left in place until Appraisal
  re-decides (a superseding order); the Appraisal phase clears it if the new
  order is not `Accepted`.
- `CommunicationAvailable = false` → `emit (OrderUndelivered(order.Command,
  recipient, UnableToCommunicate))` and drop (unchanged from TASK-027).

The `OrderUndelivered` ordering and the "an undelivered new order does not
cancel an order in progress" rule are unchanged. The phase header comment is
updated: "Realised by TASK-027; reworked by TASK-028 — a delivered order
populates `AgentState.Order` (and resets `Disposition`); the Appraisal phase
writes `Destination`."

### `appraisal` (12.5) — new phase, header "Realised by TASK-028"

Copy the agent array. For each agent in ascending id order:

1. **Housekeeping.** `Order = Some o`, `Disposition = Some Accepted`,
   `Destination = None`, `Position = <o.Intent target>` → `{ a with Order =
   None; Disposition = None }`. No event. (Continue to the next agent.)
2. **Fast path.** `Order = Some`, `Disposition = Some _` → no change, no event.
3. **Appraise.** `Order = Some o`, `Disposition = None`:
   - `let disposition, exposedCells = Appraisal.appraise s.Terrain
     s.TacticalKnowledge a.Discipline o a.Position budget`
   - clear any stale `Destination` from a superseded order, then:
     - `Accepted` and `a.Position <> target` → `Destination = Some target`;
       `Accepted` and already there → `Destination = None`.
     - `Refused _` / `Unable _` → `Destination = None`.
   - `{ a with Disposition = Some disposition; Destination = … }`
   - `emit (OrderAppraised(a.Id, o.Command, disposition)) s`
4. `Order = None` → nothing.

`runPhase` — `Appraisal -> appraisal s`; remove `Appraisal` from the no-op
list. `CommitmentAndLocalAction` stays a no-op (B-018).

`Appraisal` does **not** write the `AgentState.Route` cache — the Navigation
phase recomputes the route from `Destination` exactly as it does today (a
small, pure duplication of the stage-2 `Pathfinding` call; coupling the phases
via `Route` is not worth it).

## Diagnostics

`AGENTS.md`'s diagnostic-extension rule **applies**: new authoritative state
(`Order`, `Disposition`) and a new event (`OrderAppraised`). Required:

- **`Overlay.OrderAppraisal of agent: AgentId * at: Cell * disposition:
  OrderDisposition * exposedCells: Cell[]`** — one per agent with `Disposition
  = Some`, derived by **both** `Diagnostics.frame` and `frameOf` (the
  `KnownContact` precedent — `Order` / `Disposition` are on `WorldState`, so a
  bare state carries them). `at` is the agent's current cell; `exposedCells`
  is recomputed via `Appraisal.routeExposure` over the candidate route
  (`Pathfinding.findWithin` from the agent's current `Position` to the order
  target) — the cells with non-zero threat pressure. `[||]` for `Unable` or an
  agent with no known threat. Doc-comment reservation line: "B-017 appraisal
  → `OrderAppraisal` (realised by TASK-028)".
- `eventMarker` gains `OrderAppraised _ -> { Kind = "order-appraised"; Cells =
  [||] }` (no world access in `eventMarker`, the `OrderUndelivered` precedent).
- `DiagnosticRender.Ascii`: an `order appraisal (x,y): agent N <accepted |
  refused route-too-exposed threat M | unable no-known-route>` overlay text
  line, plus the exposed cells listed; the `sightRays` / `plannedPaths`
  exclusion filters gain `| OrderAppraisal _ -> None`.
- `DiagnosticRender.Svg`: a disposition-coloured marker at the agent's cell
  (green `#2f855a` accepted / red `#c53030` refused / grey `#718096` unable),
  a `A`/`R`/`U` glyph, and the `exposedCells` drawn as translucent red cells,
  deliberately distinct from a `PlannedPath` polyline or a `KnownContact`
  ring.
- Every exhaustive `EventBody` / `Overlay` match armed with the intended
  branch (never a wildcard): `Diagnostics` (`reservationOverlays`,
  `obstructionOverlays`, `undeliveredOrderOverlays` `e.Body` filters),
  `DiagnosticRender` (both `Ascii` filters, `Ascii` text, `Svg`),
  `Program.describeReplayError` / any event-describe if it matches `EventBody`,
  and the test-project overlay matches (`DiagnosticsTests`). FS0025 sites +
  fixes listed in the ledger (TASK-026 / TASK-027 style).
- Goldens: the new `exposed-approach-tick-001.*` (Decision H) + the updated
  `blocked-goal` / `lost-comms` tick-1 goldens; a hand-built `OrderAppraisal`
  overlay unit test (`Reserved` / `Obstructed` / `KnownContact` precedent);
  the regeneration note in `content/diagnostics/README.md`.

## The `Canonical.FormatVersion` 3 → 4 re-pin

Behaviour-neutral for movement; +1 `OrderAppraised` event per order.

- `Canonical.fs`: `FormatVersion` `3 → 4` with a doc-comment paragraph
  (TASK-018 / TASK-026 precedent). `writeAgent` gains, after `Destination`:
  the `Order` option (present tag; `Command` id, `Intent` tag + target x/y,
  `IssuedAtTick` i64, `Urgency` i32, `RiskTolerance` i32) and the `Disposition`
  option (present tag; outcome tag i32; for `Refused` / `Unable` the primary
  `DecisionReason` — reason tag i32, and for `RouteTooExposed` a threat option
  tag + `AgentId` i32 — then a supporting count + each supporting reason). A
  "`Discipline` deliberately NOT written" comment (the `CommunicationAvailable`
  note). `topLevelSections` unchanged (agent fields diff per-agent in
  `firstDifferingSection`).
- Re-pin, recording old/new initial + final hash and old/new event count per
  entry in a ledger table (TASK-026 style):
  - `content/fixtures/SPIKE-FIXTURE.md` — "Canonical format version" `3 → 4`,
    the format-3 prose, initial + final hash, 40 per-tick rows, "Domain
    events" `33 → 34` (+1 `OrderAppraised` at tick 1).
  - `content/replays/*.md` — all ten (`spike-fixture`, `wall-detour`,
    `blocked-goal`, `converging-routes`, `slow-terrain`, `follow-chain`,
    `swap-standoff`, `perception-contact`, `lost-comms`, and the new
    `exposed-approach`): initial + final hash + every per-tick row; "Domain
    events" +1 per order (`blocked-goal` and `lost-comms` unchanged at 2).
    Prose lines mentioning `Canonical.FormatVersion 3`.
  - `content/replays/envelope-full.cwreplay` — `canonical 3 → 4`,
    `initial-hash`, 24 `checkpoint` lines, comment prose;
    `envelope-full.md` — hashes + 24 rows + "Domain events" `72 → 75` (+3, one
    per recipient of the 3-recipient order).
  - `content/replays/CORPUS.md` — the version prose (the `lost-comms` bullet,
    the `envelope-full` re-pin note).
  - `content/diagnostics/*.ascii.txt` (12) — `(format 3) → (format 4)` +
    footer hash; `*.svg` (12) + `demo.html` (21 embedded frames) — footer
    hash only. `demo` scenario: `DemoScenario` has agent 5 hostile — its
    friendlies appraise once when their order is issued; the demo order is
    enemy-free at appraisal time (the hostile is far / behind cover), so
    `demo.html` gains one `order-appraised` marker + an `OrderAppraisal`
    overlay per ordered friendly (a genuine behaviour addition, allowed —
    `DemoScenario` is not a `.cwlog` corpus entry or the fixture; flag it in
    the ledger like TASK-026 flagged `demo.html`'s `KnownContact`).
  - `content/benchmarks/BASELINE.md` — glance at the "canonical encode" /
    "state hash" ns rows after regeneration; no `0x` hash or version token to
    re-pin.
- Test literal sites: `FixtureTests.fs` (`h.Format 3 → 4`, initial hash, the
  40-value array), `ReplayTests.fs` (`canonical 3 → 4` inline strings, the
  fixture initial hash, the 24-value `envelopeFullHashes`),
  `CanonicalHashTests.fs` (`Assert.Equal(4, Canonical.FormatVersion)` + the
  new sections), `CorpusTests.fs` / `ScenarioTests.fs` / `PathfindingTests.fs`
  / `SightTests.fs` / `TerrainTests.fs` / `DiagnosticsTests.fs` — any
  `FormatVersion` assertion or fixture-hash literal.

**Stop-and-report:** a changed **tick count** on any of the ten pre-existing
entries or the fixture, or an event-count change that is not exactly `+1 per
order appraised` (`blocked-goal` / `lost-comms` at 2 unchanged), means the
`communication` rework or the Appraisal phase changed movement behaviour it
must not. Re-running `corpus --regenerate` must be a zero diff.

## Allowed scope

- `src/CommandoWar.Sim/Domain.fs` — move `PlayerIntent` / `Urgency` /
  `RiskTolerance` in from `Commands.fs`; add `ReceivedOrder`, `DecisionReason`,
  `OrderDisposition`; `AgentState.Order` / `.Disposition` / `.Discipline`;
  `Agent.create` defaults (`Order = None`, `Disposition = None`, `Discipline =
  AppraisalConfig.DisciplineDefault` — needs `Appraisal.fs` before `Domain.fs`?
  **No** — `DisciplineDefault` is a plain literal; put it on a tiny
  `[<Literal>]` in `Domain.fs` or inline `3` with a comment pointing at
  `AppraisalConfig`. Prefer: `AppraisalConfig` owns it and `Agent.create` uses
  a local `[<Literal>] DisciplineDefault = 3` with a doc comment tying the two,
  OR move `AppraisalConfig` literals ahead of `Domain.fs`. Decide during
  implementation; the simplest that keeps one source of truth wins.)
- `src/CommandoWar.Sim/Appraisal.fs` (**new leaf**) — `AppraisalConfig`
  literals; `Appraisal.routeExposure`; `Appraisal.appraise`. Pure, total,
  integer-only, `Terrain` + `Sight` + `Pathfinding` + `Perception` + `Domain`
  only, no event emission, no mutation. Inserted in the fsproj after
  `Perception.fs` (and after wherever the intent types land).
- `src/CommandoWar.Sim/Commands.fs` — `PlayerCommand` / `CommandRejection` /
  `Command` reference the moved intent types from `Domain.fs`; the three type
  definitions leave this file.
- `src/CommandoWar.Sim/Simulation.fs` — `StepState.PendingOrders` type change;
  `commandIntake` `pending.Add`; `communication` write target + header
  comment; new `appraisal` phase function + `runPhase` arm + "Realised by
  TASK-028" comment; `step` carries no new `StepState` field beyond the
  `PendingOrders` retype (`Order` / `Disposition` ride on `Agents`).
- `src/CommandoWar.Sim/Events.fs` — `OrderAppraised`; the `DomainEvent`
  ordering doc comment renumber.
- `src/CommandoWar.Sim/Canonical.fs` — `FormatVersion` `3 → 4`; `writeAgent`
  `Order` / `Disposition` encoding + the `Discipline` "not written" comment.
- `src/CommandoWar.Sim/Scenario.fs` — `RawDeployment.Discipline`,
  `Deployment.Discipline`, `toDeployments` carry-through,
  `ScenarioError.NegativeDiscipline` + its validation.
- `src/CommandoWar.Sim/Diagnostics.fs` — `Overlay.OrderAppraisal`; the
  `frame` + `frameOf` derivation (`orderAppraisalOverlays`); `eventMarker`
  arm; the `Overlay` doc comment; the FS0025 filter arms.
- `src/CommandoWar.Headless/DiagnosticRender.fs` — the new `Ascii` / `Svg`
  branches + filter arms.
- `src/CommandoWar.Headless/Corpus.fs` — a `discipline` input on `rawScenario`
  (a `disciplines: (int * int) list` or a per-agent map; default
  `AppraisalConfig.DisciplineDefault`); the new `exposed-approach` `Entry`
  (with a hostile and two differently-disciplined friendlies); the
  `blocked-goal` description note. `Corpus.all` row, `CORPUS.md` row.
- `src/CommandoWar.Headless/{DemoScenario,LosDemo,PathDemo}.fs` — the
  `RawDeployment` literals gain `Discipline = <default>` (compile-only).
- `src/CommandoWar.Headless/Program.fs` — an `OrderAppraised` arm only if an
  exhaustive `EventBody` match exists there.
- `content/replays/` — the 3 → 4 re-pin of all ten `.md` +
  `envelope-full.{cwreplay,md}` + `CORPUS.md`; the new `exposed-approach.{cwlog,md}`.
- `content/fixtures/SPIKE-FIXTURE.md` — the 3 → 4 re-pin.
- `content/diagnostics/` — the 3 → 4 footer re-pin of every golden; new
  `exposed-approach-tick-001.*`; updated `blocked-goal` (if a golden exists) /
  `lost-comms-tick-001.*`; `README.md`.
- `tests/CommandoWar.Sim.Tests/` — see "Acceptance criteria" and "the re-pin".
- Docs — see "Documentation updates".

## Forbidden scope

- Combat / line of fire / suppression / the deterministic hit or spread draw —
  **B-019**. **Any PRNG draw.**
- Suppression, stress, trust dynamics, and the exposure/suppression-band,
  knowledge-change, wounded, support, and leadership reappraisal triggers —
  **B-021**. Hysteresis constants (nothing to act on in scope).
- Commitments, the finite action executor, the interrupt table — **B-018**.
  Appraisal writes `Destination`; it does **not** build a commitment store.
- `OrderDisposition.Adapted` / `Delayed`, a `ResumeCondition` / `Capability` /
  `TacticalAdaptation` type, route recomputation for a safer path (stage 5) —
  **B-018 / B-021**.
- A hostile squad tactical picture, enemy doctrine reacting to appraisal —
  **B-022**.
- New `PlayerIntent` cases (`Hold` / `Suppress` / `Assault` / `Withdraw`) —
  **B-030**.
- Any `Sight.fs` / `Perception.fs` / `Pathfinding.fs` / `Terrain.fs` change —
  consume them.
- A `SquadStore`, formation grouping, per-agent private beliefs
  (`docs/05` section 17; B-011d).
- Editing the client spikes, `src/_scratch`, `bench/`,
  `content/benchmarks/BASELINE.md` (a post-regeneration glance only).
- An ADR (Decision G).

## Acceptance criteria

- [x] The Appraisal phase (12.5) is a real phase function in `Simulation.fs`,
      in its `Phases.order` slot 5, with a "Realised by TASK-028" header
      comment; `runPhase` has a real `Appraisal` arm and `Appraisal` is out of
      the no-op list.
- [x] The Communication phase writes `AgentState.Order` (and resets
      `Disposition`) instead of `Destination`; its header comment states the
      change. `commandIntake` records a `ReceivedOrder` in `PendingOrders`.
- [x] `AgentState.Order: ReceivedOrder option`, `AgentState.Disposition:
      OrderDisposition option` (both in `Canonical.encode`), `AgentState.Discipline:
      int` (**not** in `Canonical.encode`); `PlayerIntent` / `Urgency` /
      `RiskTolerance` moved to `Domain.fs`; `Agent.create` defaults; doc
      comments distinguishing canonical memory from static data.
- [x] `OrderDisposition` (`Accepted | Refused of primary * supporting | Unable
      of primary * supporting`) and `DecisionReason` (`NoKnownRoute |
      RouteTooExposed of threat: AgentId option`) DUs; `AppraisalConfig`
      module literals in the new `Appraisal.fs` leaf; `Appraisal.routeExposure`
      / `Appraisal.appraise` pure/total/integer-only.
- [x] `RawDeployment.Discipline` / `Deployment.Discipline`; `Scenario.validate`
      rejects a negative Discipline (`NegativeDiscipline`) and round-trips a
      valid one (`ScenarioTests` fact); `World.ofScenario` wires it.
- [x] `OrderAppraised of agent * command * disposition` event; the `Events.fs`
      ordering doc comment renumbered (after `ContactObserved` / `ContactExpired`,
      before movement, ascending agent id). Every exhaustive `EventBody` match
      armed; FS0025 sites + fixes in the ledger.
- [x] `Canonical.FormatVersion` `3 → 4`; `writeAgent` encodes `Order` +
      `Disposition`; `Discipline` deliberately not written (comment).
- [x] `SimulationTests` facts:
  - a clear-route, enemy-free order is `Accepted` and `Destination` is written
    the same tick (movement begins that tick, exactly the pre-TASK-028
    behaviour for a delivered order);
  - an order with no path is `Unable(NoKnownRoute, [||])` and no `Destination`
    is written;
  - an order along a route exposed to a known threat is `Refused(RouteTooExposed
    (Some threatId), [||])` for a low-`Discipline` agent;
  - the same order is `Accepted` for a high-`Discipline` agent (the G3
    divergence as a focused fact);
  - directional cover on the exposed route cells drops exposure below the
    threshold and the low-`Discipline` agent now `Accepted` (the "Covered
    alternative" shape, `docs/05` section 16);
  - an `Accepted` order is not re-appraised on an idle tick (exactly one
    `OrderAppraised` across the run);
  - appraisal never returns `Accepted` after a stage-2 failure (`docs/09`
    section 2.2), as a focused fact;
  - two runs of the same world + commands emit byte-identical events + hashes.
- [x] A `DeterminismPropertyTests` property (`MaxTest >= 200`; generator places
      1–2 known threats and varies `Discipline`): every `AgentState.Disposition`
      after a tick equals a fresh `Appraisal.appraise` over the pre-tick state
      (no `Accepted` when stage 2 failed; a `Refused` / `Unable` carries a
      primary reason; `Destination` is `Some` iff the disposition is
      `Accepted` and the agent is not already at the target); `Random.Draws`
      unchanged (no PRNG draw). Properties 1–7 unmodified.
- [x] New corpus entry `exposed-approach` (two friendlies, different
      `Discipline`, same exposed approach past a known hostile — one `Refused`,
      one `Accepted`): `CORPUS.md` row, committed `.cwlog` + `.md`, passes
      `CorpusTests` `[<Theory>]` and `cwheadless corpus`.
- [x] `blocked-goal` repurposed: its `.md` description and its tick-1 golden
      (and `lost-comms-tick-001.*`, sharing the hash) reflect
      `OrderAppraised(Unable(NoKnownRoute, [||]))` at tick 1; tick count 5,
      event count 2, both unchanged.
- [x] Diagnostics: `Overlay.OrderAppraisal` derived in `frame` and `frameOf`,
      rendered in `Ascii` + `Svg`, covered by a hand-built `DiagnosticsTests`
      fact and the committed `exposed-approach-tick-001.*` golden.
- [x] `Canonical.FormatVersion` 3 → 4 re-pin complete and behaviour-neutral:
      `cwheadless corpus` 10/10 (nine re-pinned + `exposed-approach`),
      `cwheadless fixture` `format 4`, `cwheadless replay-file
      envelope-full.cwreplay` checkpoints OK at `canonical 4`; `git diff
      content/replays` shows only hash-column + event-count changes on the ten
      + the new entry; re-running `corpus --regenerate` is a zero diff; every
      `content/diagnostics/` golden shows only footer/hash/appraisal-marker
      changes; **no tick count moved**.
- [x] `dotnet build CommandoWar.slnx -c Release` = 0/0; `dotnet list
      src/CommandoWar.Sim package --include-transitive` = `FSharp.Core` only;
      source scan of `src/CommandoWar.Sim` clean (`float` / `Stopwatch` /
      `DateTime` / `System.Random` / `godot`).
- [x] `dotnet test CommandoWar.slnx -c Release` green — state the new count;
      name each added fact and the property's case count.
- [x] Docs updated (see below).

## Required verification

- `dotnet build CommandoWar.slnx -c Release` (0/0).
- `dotnet test CommandoWar.slnx -c Release` before any edit (`228`) and after
  (new count; each added fact + the property's case count named).
- `cwheadless corpus` + `cwheadless fixture` before any edit (record hashes /
  event counts) and after `--regenerate` (new hashes; **tick counts
  unchanged** for the ten existing entries; event counts moved by
  `OrderAppraised` only — `+1` per order, `blocked-goal` / `lost-comms` at 2
  unchanged); `git diff content/replays` shows only hash-column + event-count
  changes on the ten + the new entry; re-running `--regenerate` is a zero diff.
- Regenerate every affected `content/diagnostics/` golden; confirm only
  footer/hash/appraisal-marker changes on pre-existing ones.
- `cwheadless replay-file content/replays/envelope-full.cwreplay` — checkpoints
  OK at `canonical 4`.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive` — `FSharp.Core` only.
- source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`.
- `git status --porcelain` — matches "Allowed scope"; nothing under the client
  spikes, `src/_scratch`, `bench/`, `content/benchmarks/BASELINE.md`.

## Evidence to capture

- test summary (before/after counts); the new `SimulationTests` facts and the
  property by name + case count;
- the `Canonical.FormatVersion` 3 → 4 re-pin table (old/new initial + final
  hash + old/new event count for all ten entries + the fixture);
- the `AppraisalConfig` final constant table with rationale;
- the FS0025 sites with their fixes;
- the new `exposed-approach` entry's hashes / tick / event count and the
  divergence it exercises (agent 0 `Refused`, agent 1 `Accepted`);
- the `blocked-goal` behaviour change (event content, unchanged counts);
- the diagnostics golden;
- confirmation that no tick count moved and `--regenerate` is idempotent.

## Rollback or removal

`Order` / `Disposition` / `Discipline`, `ReceivedOrder` / `OrderDisposition` /
`DecisionReason`, `Appraisal.fs`, the Appraisal phase, the `communication`
write-target change, `OrderAppraised`, the overlay / renderer branches, the
`exposed-approach` entry, and the tests are additive. Reverting: restore
`communication`'s `Destination` write, return `appraisal` to a no-op arm,
`FormatVersion` `4 → 3` and re-pin back, restore `blocked-goal`'s description /
golden, delete the field / event / overlay / entry / goldens / tests, move the
intent types back to `Commands.fs`.

## Documentation updates

- this task file (Outcome, Status, acceptance boxes);
- `docs/11_BACKLOG.md`: TASK-028 row; B-017 → `done`; B-018 / B-021 / B-023
  unblocked notes;
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
  "Pinned facts" `Canonical.FormatVersion` `3 → 4` + shared-fixture hashes +
  "Green tests" count;
- `docs/04_SIMULATION_SPEC.md` sections 2 (`IssuedAtTick` is now stored on
  `AgentState.Order` — staleness still deferred), 11 (`Order` / `Disposition`
  / `Discipline` realised), 12.2 (Communication writes `Order`), 12.5
  (Appraisal realisation block — the main edit), 12.6 (`Destination` is
  Appraisal's until B-018 commitments), 13 (`Urgency` / `RiskTolerance` no
  longer inert), 14 (`OrderAppraised` realised), 17 (the format-4 note), 20
  (new invariants: `Destination` set iff disposition `Accepted`; appraisal
  never `Accepted` after a hard feasibility failure);
- `docs/05_COMMAND_AND_AGENT_AI.md` sections 5 (stages 2–4 realised), 6
  (`OrderDisposition` subset), 7 (`DecisionReason` subset; `ContactId` =
  `AgentId`), 8 (`Discipline` realised as a static per-deployment int), 13
  (developer trace — `OrderAppraised` carries the disposition + reasons), 14
  ("new order received" trigger realised; the rest deferred), 15
  (`AppraisalConfig` realises "one configuration structure");
- `docs/03_ARCHITECTURE.md` section 7 step 5 realisation note;
- `docs/07_VERTICAL_SLICE.md` section 8 partial-realisation note (steps 1–3);
- `docs/09_TEST_STRATEGY.md` sections 2.1 (appraisal stage outcomes realised),
  2.2 (the never-`Accepted`-after-feasibility-failure property realised), 2.3
  ("exposed-road refusal" realised; "covered route adapts" partial — cover
  mitigation only, no `Adapted`; "suppression reverses refusal" deferred);
- `PROJECT_STATE.yaml` `active_work`;
- no ADR (Decision G).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
