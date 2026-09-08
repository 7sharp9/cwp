## 2026-09-08 - TASK-028 - Staged order appraisal and typed reasons

**Owner:** Dave with coding-agent assistance
**Branch:** `task-028-order-appraisal` off `main` at the TASK-027 merge
(`Merge TASK-027: communication constraints and order delivery (B-016)`).
Committed locally; not pushed.
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; .NET 10.0.11;
FSharp.Core 10.1.303; xUnit 2.9.3; FsCheck 3.3.4
**Status change:** TASK-028 drafted `-> ready -> review`; backlog B-017
`proposed -> done`

Realises P3 Required work "implement explicit order appraisal and typed
reasons" (`docs/08` section 6) and is the G3 keystone: G3 evidence requires
"at least two agents appraise the same intent differently for inspectable
reasons" (`docs/07` section 9 criterion 2). Turns the `docs/04` section 12.5
Appraisal phase from a no-op into a real phase over a new `Appraisal` leaf
module. No ADR — the ADR-0002 amendment "Static authoritative data and the
canonical image" governs both the `Canonical.FormatVersion` 3 -> 4 bump (for
the newly-canonical `AgentState.Order` / `.Disposition`) and the `Discipline`
exclusion (static authored data).

### Prelude: TASK-027 accepted and merged

TASK-027 (backlog B-016) was committed on its branch but not accepted/merged.
Confirmed with Dave, then: recorded acceptance (`tasks/TASK-027-*.md`,
`docs/ledger/2026-09-07-TASK-027-*.md` Review block, `docs/11`, `docs/12`),
`Accept TASK-027; record acceptance and merge`, and
`Merge TASK-027 ... (--no-ff)` into `main`; branch deleted. `main` build 0/0,
228 green, `-- corpus` 9/9, `-- fixture` 33 events unchanged before branching.

### Central decisions (confirmed with Dave 2026-09-08 before the phase bodies)

- **A — the delivered order lives on `AgentState.Order`; appraisal state is
  canonical; `Canonical.FormatVersion` 3 -> 4.** The Communication phase
  (TASK-027) stops writing `AgentState.Destination`; it writes a new canonical
  `AgentState.Order: ReceivedOrder option` (`{ Command; Intent; IssuedAtTick;
  Urgency; RiskTolerance }`) and resets `AgentState.Disposition` to `None`. The
  new Appraisal phase reads `Order` + `TacticalKnowledge` + terrain +
  `Discipline`, produces an `OrderDisposition`, and on `Accepted` writes
  `Destination`. `Order` + `Disposition` are genuine per-tick canonical memory
  (they survive ticks so appraisal is not re-run every tick, and carry
  provenance / structured reasons no other field reproduces), so under the
  amendment they enter `Canonical.encode` and `FormatVersion` bumps 3 -> 4.
- **B — Discipline only; static per-deployment integer; EXCLUDED from
  `Canonical.encode`.** `RawDeployment.Discipline` / `Deployment.Discipline` /
  `AgentState.Discipline` (default `AppraisalConfig.DisciplineDefault = 3`),
  `ScenarioError.NegativeDiscipline` guard. Read only by the stage-4 resolve
  threshold. Excluded from the canonical image, matching the
  `CommunicationAvailable` precedent (TASK-027) — the 3 -> 4 bump is justified
  by `Order` + `Disposition` alone, and it would be inconsistent to hash one
  static per-deployment trait and not the other. Trust / stress / suppression
  deferred to B-020 / B-021 (no field, no init value). *(Dave asked for the
  recommendation; recommended and confirmed: OUT.)*
- **C — stages 1-4 partial, stage 5 deferred; three outcomes.** Stage 1
  trivially passed (command intake + TASK-027 guarantee it). Stage 2:
  `Pathfinding.findWithin` -> `Unable(NoKnownRoute)` on failure. Stage 3: route
  exposure to the known threats in `WorldState.TacticalKnowledge` only
  (engagement range, `Sight.visible` from the last-known cell, directional
  `Terrain.cover`). Stage 4: exposure vs a threshold from `Discipline` +
  `RiskTolerance` + `Urgency` -> `Accepted` or `Refused(RouteTooExposed)`.
  `OrderDisposition = Accepted | Refused of primary * supporting | Unable of
  primary * supporting`; `DecisionReason = NoKnownRoute | RouteTooExposed of
  threat: AgentId option`. `Adapted` (stage 5) and `Delayed` deferred to
  B-018 / B-021 — no DU cases (no speculative type machinery).
- **D — `AppraisalConfig` module of integer literals; integer exposure metric;
  no PRNG.** New leaf `src/CommandoWar.Sim/Appraisal.fs`. Final constants:
  | constant | value | reason |
  |---|---:|---|
  | `DisciplineDefault` | `3` | Discipline of an agent from a deployment that authored none. Also mirrored as `Agent.DisciplineDefault` (module ordering). |
  | `ThreatEngagementRange` | `8` | Chebyshev cells: a known threat within this of a route cell can put fire on it. Below `PerceptionConfig.SightRange` (10) — a threat is seen before it is in range. |
  | `ExposedCellWeight` | `10` | Pressure one exposed route cell adds, before cover. |
  | `CoverMitigationPerLevel` | `4` | Pressure removed per authored `Terrain.cover` level on the edge fire arrives from (`max 0 (10 - level*4)`; level 3 fully negates a cell). |
  | `BaseResolve` | `20` | Pressure an agent of `Discipline 0` tolerates. |
  | `DisciplineResolveWeight` | `15` | Added tolerance per Discipline point (`Discipline 3` tolerates 65). |
  | `RiskCautious` | `-15` | `RiskTolerance.Cautious` lowers the threshold. |
  | `RiskAggressive` | `20` | `RiskTolerance.Aggressive` raises it. `Standard` is 0. |
  | `UrgencyImmediate` | `20` | `Urgency.Immediate` raises the threshold. `Routine` is 0. |
  Exposure = Σ over the stage-2 route cells of Σ over known threats of `max 0
  (ExposedCellWeight - cover * CoverMitigationPerLevel)` where the cell is in
  range and `Sight.visible` from the threat. `attackDirection` = the dominant
  cardinal of `(threatCell - cell)`. Integer-only, no PRNG. **Hysteresis
  deferred** (Decision D allows it, but the only in-scope reappraisal trigger
  is "new order received", so a hysteresis band has nothing to act on until
  B-021's exposure-band trigger).
- **E — reappraisal trigger in scope: "a new order is received" only.** The
  Communication phase resets `Disposition` to `None` when it writes a fresh
  `Order`; the Appraisal phase judges exactly the agents whose `Disposition` is
  `None` — the fast path is `Order.IsSome && Disposition.IsSome` (skip, no
  event). Fulfilment housekeeping: an `Accepted` order with no `Destination`
  and the agent on the target clears `Order` + `Disposition` (no event). "Route
  becomes blocked" cannot fire for an appraisal-`Accepted` order under static
  terrain; knowledge-change / exposure-band / suppression / wounded / support /
  leadership triggers are B-021.
- **F — `OrderDisposition` carries the reasons; `OrderAppraised of agent *
  command * disposition`.** `Refused` / `Unable` carry `primary` + `supporting`
  by construction, so the `docs/04` section 20 "every refusal carries a
  structured reason" invariant holds without a side check. The event is slim.
  Emitted for every appraisal including `Accepted` (G3 trace); ordered after
  `ContactObserved` / `ContactExpired`, before movement, ascending agent id.
- **G — no ADR.** The ADR-0002 amendment covers the format bump and the
  `Discipline` exclusion; `docs/08` section 6 already lists appraisal under P3
  Required work; phase order unchanged (`Appraisal` was already slot 5).
- **H — the G3 evidence scenario, and `blocked-goal` repurposed.** New
  `exposed-approach` corpus entry: two friendlies of `Discipline` 1 and 6
  ordered along the same exposed approach past a stationary hostile the squad
  sees from the start — near-identical exposure, so the divergence is
  discipline alone (`Refused` vs `Accepted` on tick 1). `blocked-goal`'s
  unreachable-target order is now `Unable(NoKnownRoute)` at appraisal instead
  of `MovementBlocked` at navigation — same tick count (5), same event count
  (2), the docs/09 section 2.2 realisation; its `.md` description and a new
  `blocked-goal-tick-001.*` golden reflect it. *(Dave: repurpose blocked-goal.)*

### Changes

- **`src/CommandoWar.Sim/Domain.fs`.** `PlayerIntent` / `Urgency` /
  `RiskTolerance` moved in from `Commands.fs` (needed before `AgentState`).
  New `ReceivedOrder`, `DecisionReason`, `OrderDisposition`.
  `AgentState.Order` / `.Disposition` / `.Discipline` with doc comments
  distinguishing canonical memory from static data. `Agent.DisciplineDefault`
  literal; `Agent.create` defaults `Order = None`, `Disposition = None`,
  `Discipline = 3`.
- **`src/CommandoWar.Sim/Appraisal.fs` (new leaf, after `Perception.fs`).**
  `AppraisalConfig` literals; `Appraisal.routeExposure` (total pressure + top
  threat), `Appraisal.resolveThreshold`, `Appraisal.appraise` (the staged
  pipeline -> `OrderDisposition * exposedCells`). Pure, total, integer-only,
  `Terrain` + `Sight` + `Pathfinding` + `Perception` + `Domain` only.
- **`src/CommandoWar.Sim/Commands.fs`.** The three intent types removed (now in
  `Domain.fs`); a comment records the move and that `Urgency` / `RiskTolerance`
  are no longer inert.
- **`src/CommandoWar.Sim/Simulation.fs`.** `StepState.PendingOrders` retyped
  `(AgentId * ReceivedOrder) list`. `commandIntake` records the full
  `ReceivedOrder`. `communication` writes `Order = Some order; Disposition =
  None` (not `Destination`); header comment reworked. New `appraisal` phase
  function ("Realised by TASK-028"): housekeeping -> fast path -> appraise
  (clear stale `Destination`, then `Accepted` -> `Some target`, else `None`;
  emit `OrderAppraised`). `runPhase` -> real `Appraisal` arm, removed from the
  no-op list. `World.ofScenario` maps `Deployment.Discipline`. `step` doc
  comment updated.
- **`src/CommandoWar.Sim/Events.fs`.** `OrderAppraised of agent * command *
  disposition`. `DomainEvent` ordering doc comment renumbered (group 4 =
  appraisal outcomes ascending agent id; movement -> group 5).
- **`src/CommandoWar.Sim/Canonical.fs`.** `FormatVersion` `3 -> 4` (doc
  comment). New `reasonCode` / `writeReason` / `writeDisposition` /
  `writeOrder`. `writeAgent` gains the `Order` and `Disposition` option
  sections after `Destination`; a "`Discipline` deliberately NOT written"
  comment (the `CommunicationAvailable` precedent). `topLevelSections`
  unchanged (agent fields diff per-agent).
- **`src/CommandoWar.Sim/Scenario.fs`.** `RawDeployment.Discipline` /
  `Deployment.Discipline`; `toDeployments` carry-through;
  `ScenarioError.NegativeDiscipline` + its validation.
- **`src/CommandoWar.Sim/Diagnostics.fs`.** `Overlay.OrderAppraisal of agent *
  at * disposition * exposedCells` + doc-comment reservation line.
  `orderAppraisalOverlays` derived in **both** `frame` and `frameOf` (the
  `KnownContact` precedent — `Order` / `Disposition` are on `WorldState`),
  recomputing `exposedCells` via `Appraisal.appraise`. `eventMarker` gains
  `order-appraised`. FS0025 filter arms.
- **`src/CommandoWar.Headless/DiagnosticRender.fs`.** `reasonText` /
  `dispositionText` / `dispositionGlyph` shared helpers. `Ascii`: an
  `order appraisal (x,y): agent N <accepted|refused ...|unable ...>` line with
  the exposed cells; the `sightRays` / `plannedPaths` filter arms. `Svg`:
  translucent-red exposed cells + a disposition-coloured `A`/`R`/`U` glyph at
  the agent's cell.
- **`src/CommandoWar.Headless/Corpus.fs`.** `rawScenario`'s `deployment` helper
  defaults `Discipline = AppraisalConfig.DisciplineDefault`. New bespoke
  `exposedApproachWorld` (12x8, friendly 0 `Discipline 1` at (1,3), friendly 1
  `Discipline 6` at (1,5), hostile 2 at (10,4)) and its `exposed-approach`
  `Entry` (12 ticks). `blocked-goal` / `lost-comms` / `perception-contact`
  `Description` prose updated for the appraisal-era behaviour.
- **`src/CommandoWar.Headless/{DemoScenario,LosDemo,PathDemo}.fs`.** The
  `RawDeployment` literals gain `Discipline = AppraisalConfig.DisciplineDefault`
  (compile-only).
- **`content/replays/`.** New `exposed-approach.{cwlog,md}`. Re-pinned all ten
  `.md` (hash columns + `Domain events`), `envelope-full.cwreplay`
  (`canonical 3 -> 4`, `initial-hash`, 24 `checkpoint` lines, comment) and
  `envelope-full.md` (hashes, 24 rows, note). `CORPUS.md` version prose + the
  new entry row.
- **`content/fixtures/SPIKE-FIXTURE.md`.** Re-pinned: format `4`, initial
  `0x55F43D66C7AECB7F`, final `0x7737282578E821C6`, the 40-row table, 33 -> 34
  events, prose.
- **`content/diagnostics/`.** Every hash-footer golden regenerated
  (`(format 3) -> (format 4)` + hash; the `.svg` footers carry no format
  number). `fixture-mid-route.ascii.txt` and the four corpus goldens
  legitimately gain an `order appraisal` overlay line. New
  `exposed-approach-tick-001.*` (the divergence: agent 0 `refused
  route-too-exposed threat-agent-2`, agent 1 `accepted`) and
  `blocked-goal-tick-001.*` (`unable no-known-route`). `demo.html` gains an
  `order-appraised` marker + `OrderAppraisal` overlay per ordered friendly (a
  genuine behaviour addition — `DemoScenario` is not a `.cwlog` corpus entry;
  the TASK-026 `demo.html` precedent).
- **`tests/CommandoWar.Sim.Tests/`.**
  - `SimulationTests.fs`: `appraisalWorld` / `dispositionOf` / `appraisedIn`
    helpers and eight facts — clear-route enemy-free `Accepted` + `Destination`
    same tick; no-path `Unable(NoKnownRoute)` + no `Destination` + no
    `MovementBlocked`; never `Accepted` after a hard feasibility failure;
    exposed route `Refused(RouteTooExposed (Some threat))` for a low-Discipline
    agent; the same order `Accepted` for a high-Discipline agent (the G3
    divergence); directional cover drops exposure below threshold; not
    re-appraised on a later idle tick; deterministic + zero draws. The TASK-027
    delivery facts still pass unchanged (enemy-free `world ()` -> `Accepted`
    same tick).
  - `DeterminismPropertyTests.fs`: `appraisalCaseGen` (`perceptionCaseGen` +
    random `Discipline 0..8` per friendly) and property 8 (`MaxTest = 200`) —
    every `Disposition` consistent (no `Destination` for `Refused` / `Unable`;
    `Accepted` reachable and holds `Destination` or on-target;
    `Unable(NoKnownRoute)` still unreachable; `Disposition` only `Some` with an
    `Order`; every `OrderAppraised` matches the tick's stored disposition; no
    PRNG draw). Properties 1-7 unmodified.
  - `CanonicalHashTests.fs`: `Canonical.FormatVersion` `3 -> 4` (x2); comments
    refreshed.
  - `DiagnosticsTests.fs`: the FS0025 overlay-match arms; fixture hash
    literals + `33 -> 34`; the mid-route fact now `tryPick`s the `PlannedPath`
    and asserts an `OrderAppraisal(Accepted)`; the "no overlay at rest" fact
    now expects the `OrderAppraisal` overlay to linger one tick (31) past
    arrival and clear at 32. Three new facts: a hand-built `OrderAppraisal`
    overlay render (ASCII text + SVG `R` glyph + translucent exposed cells),
    an `exposedApproachFrames ()` golden fact (agent 0 `Refused`, agent 1
    `Accepted`, two `order-appraised` markers, byte-equal to
    `exposed-approach-tick-001.*`), and a `blocked-goal` golden fact
    (`Unable(NoKnownRoute)` overlay, no `movement-blocked`, byte-equal to
    `blocked-goal-tick-001.*`).
  - `FixtureTests.fs`: `h.Format` `3 -> 4`; initial hash; the 40-value array.
  - `ReplayTests.fs`: `canonical 3 -> 4` in three inline strings; the fixture
    initial hash; the 24-value `envelopeFullHashes`.
  - `CorpusTests.fs` / `PathfindingTests.fs` / `ScenarioTests.fs` /
    `SightTests.fs`: fixture hash / event-count literals; `FormatVersion`
    `3 -> 4` assertions; the 15 `RawDeployment` literals in `ScenarioTests`
    gain `Discipline = AppraisalConfig.DisciplineDefault`.
- **Docs.** `docs/03` section 7 (step 5 realised), `docs/04` sections 2 / 11 /
  12.2 / 12.5 / 12.6 / 13 / 14 / 17 / 20, `docs/05` sections 5 / 6 / 7 / 8 /
  13 / 14 / 15 / 16, `docs/07` section 8 (steps 1-3), `docs/09` sections 2.1 /
  2.2 / 2.3. `docs/11` new TASK-028 row + B-017 `-> done` + B-018 / B-021 /
  B-023 unblocked notes. `docs/12` index row + this file + "Pinned facts"
  (`Canonical.FormatVersion` `3 -> 4`, shared-fixture hashes, "Green tests").
  `PROJECT_STATE.yaml` `active_work`. `tasks/TASK-028-*.md` (Outcome, Status,
  acceptance boxes).

### The `Canonical.FormatVersion` 3 -> 4 re-pin (behaviour-neutrality)

Every committed scenario issues at most one order per agent along a clear,
enemy-free route (so it is `Accepted` with the same `Destination`, one phase
later but the same tick), except `blocked-goal` (unreachable target -> `Unable`
at appraisal instead of `MovementBlocked` at navigation — same tick count, same
event count) and `lost-comms` (order dropped at Communication -> no
appraisal). **Tick counts are unchanged for every entry**; event counts move
by exactly one `OrderAppraised` per order. Confirmed by `git diff
content/replays` (no `Tick count` line moved; only hash columns, `Domain
events`, and prose) and `cwheadless corpus` 10/10.

| entry | ticks | old ev | new ev | old initial (fmt 3) | new initial (fmt 4) | old final | new final |
|---|---:|---:|---:|---|---|---|---|
| `spike-fixture` | 40 | 33 | 34 | `0x50BFA007EDFC42FE` | `0x55F43D66C7AECB7F` | `0xD9D6EC3DDC1D602F` | `0x7737282578E821C6` |
| `wall-detour` | 24 | 19 | 20 | `0xD33F1F626C50BD78` | `0x89815823A8A4D2BF` | `0x299F40A7C31AFBE3` | `0x746AE3F9173619DC` |
| `blocked-goal` | 5 | 2 | 2 | `0x9D83E88BAF8CC5C6` | `0x08367596DD235E51` | `0x5A7EE35462931F17` | `0xA93CF33FCF105BB8` |
| `converging-routes` | 12 | 19 | 21 | `0xBE15331B04AEBE03` | `0xBCB81DADED9E631E` | `0x385CEB7D96FB1415` | `0xB05B497906697E40` |
| `slow-terrain` | 8 | 6 | 7 | `0x3C60E54D7AFA43FB` | `0x8E466E50354728D4` | `0xE3F93C765A20C547` | `0x1899BC0BD5F36318` |
| `follow-chain` | 6 | 21 | 24 | `0xEC12A1D3F1E445C8` | `0xD8CBA9C6AD9D1B77` | `0x49C721DD840E675D` | `0x01FED06094D3C358` |
| `swap-standoff` | 4 | 10 | 12 | `0x36334E44259BB6D6` | `0xBDA81D3710BEDB3B` | `0xAEBB18C290AF465F` | `0xEC251E4F0B81AA62` |
| `perception-contact` | 14 | 12 | 13 | `0xDAA3BCA323164178` | `0x66E6821517F73AF5` | `0x320C6FE6BC2544DF` | `0xCF052F4E1331FFB2` |
| `lost-comms` | 4 | 2 | 2 | `0x9D83E88BAF8CC5C6` | `0x08367596DD235E51` | `0x19CDEA8038A8C122` | `0x29DB050174E1C5E5` |
| `envelope-full` (`.cwreplay`) | 24 | 72 | 75 | `0x50BFA007EDFC42FE` | `0x55F43D66C7AECB7F` | `0x4E5963A2C8C83660` | `0xDC87FE7A73395380` |
| `exposed-approach` (new) | 12 | — | 19 | — | `0xA131B427F023899A` | — | `0xFA048FA702798874` |

`SPIKE-FIXTURE.md` shares `spike-fixture`'s hashes. `blocked-goal` and
`lost-comms` again share a tick-0 hash (same grid / agent / seed; `Discipline`
and `CommunicationAvailable` are both outside the image, and neither holds an
`Order` at tick 0). Every hash-footer golden under `content/diagnostics/` was
regenerated; on pre-existing ones only the footer hash / `(format N)` moved,
plus a legitimate `order appraisal` overlay line on `fixture-mid-route.*` and
the four corpus goldens, plus `demo.html` (see Deviations).

### FS0025 incomplete-match sites and their fixes

| # | site | fix |
|---|---|---|
| 1 | `Diagnostics.fs` `eventMarker` (`EventBody` match) | `\| OrderAppraised _ -> { Kind = "order-appraised"; Cells = [||] }` |
| 2 | `Diagnostics.fs` `reservationOverlays` (`e.Body` filter) | `\| OrderAppraised _ -> None` |
| 3 | `Diagnostics.fs` `obstructionOverlays` (`e.Body` filter) | `\| OrderAppraised _ -> None` |
| 4 | `Diagnostics.fs` `undeliveredOrderOverlays` (`e.Body` filter) | `\| OrderAppraised _ -> None` |
| 5 | `DiagnosticRender.fs` `Ascii` `sightRays` filter | `\| OrderAppraisal _ -> None` |
| 6 | `DiagnosticRender.fs` `Ascii` `plannedPaths` filter | `\| OrderAppraisal _ -> None` |
| 7 | `DiagnosticRender.fs` `Ascii` overlay-text `match o` | `\| OrderAppraisal(agent, at, disposition, exposedCells) -> line (…)` |
| 8 | `DiagnosticRender.fs` `Svg` overlay `match o` | `\| OrderAppraisal(_, at, disposition, exposedCells) -> <rect …> + <text>A/R/U</text>` |
| 9-12 | `DiagnosticsTests.fs` four overlay `tryPick` / `choose` matches | `\| OrderAppraisal _ -> None` |

Items 1-8 were `error FS0025` under `TreatWarningsAsErrors` in the two source
projects; 9-12 surfaced as warnings in the test project. Each fixed with the
intended branch, never a wildcard.

### New corpus entry: `exposed-approach`

12x8, seed 20260904, 2 friendlies + 1 hostile. Friendly 0 (`Discipline 1`) at
(1,3) ordered to (11,3); friendly 1 (`Discipline 6`) at (1,5) ordered to
(11,5); stationary hostile 2 at (10,4), seen from the start. Tick 1: Perception
observes contact 2, Tactical knowledge adds it, Appraisal judges both orders
against it — exposure 100 for each route (10 cells within
`ThreatEngagementRange` x `ExposedCellWeight` 10, no cover); threshold 35 for
agent 0 (`BaseResolve 20 + 15*1`) -> `Refused(RouteTooExposed (Some 2))`, no
`Destination`, never moves; threshold 110 for agent 1 (`20 + 15*6`) ->
`Accepted`, `Destination = (11,5)`, walks the approach (arrives tick ~10).

- Initial hash `0xA131B427F023899A`, final hash `0xFA048FA702798874`.
- 12 ticks, 19 domain events (2 `CommandAccepted` + 4 `ContactObserved` +
  2 `OrderAppraised` + 10 `MovementStepped` + 1 `MovementCompleted`).
- Golden `content/diagnostics/exposed-approach-tick-001.{ascii.txt,svg}`.

### Verification

- `dotnet build CommandoWar.slnx -c Release` — `Build succeeded. 0 Warning(s)
  0 Error(s)`.
- `dotnet test CommandoWar.slnx -c Release` before any edit — `Passed: 228`.
- `dotnet test CommandoWar.slnx -c Release` after — `Passed: 242` (`+14`:
  eight `SimulationTests` appraisal facts + one `SimulationTests` fact pinning
  the Navigation-phase `MovementBlocked` path directly (the two former
  direct-`MovementBlocked` order tests became `Unable(NoKnownRoute)` appraisal
  tests — net +9 `SimulationTests`), three `DiagnosticsTests` facts (a
  hand-built `OrderAppraisal` overlay render, the `exposed-approach` golden
  showing the divergence, the `blocked-goal` golden showing `Unable` at
  appraisal), one `DeterminismPropertyTests` property 8 (`MaxTest = 200`), one
  `CorpusTests` `[<Theory>]` case for the new `exposed-approach` entry).
- `dotnet run … -- corpus` before any edit — `OK - all 9 entries`; fixture
  `canonical format : 3`, initial `0x50BFA007EDFC42FE`, 33 events.
- `dotnet run … -- corpus --regenerate` then `git diff content/replays` — only
  hash-column / `Domain events` / prose changes on the ten + the new
  `exposed-approach.{cwlog,md}`; **no `Tick count` line moved**. Re-running
  `--regenerate` is a byte-identical (idempotent).
- `dotnet run … -- corpus` after — `OK - all 10 entries match their committed
  tables`; fixture `canonical format : 4`, initial `0x55F43D66C7AECB7F`, final
  `0x7737282578E821C6`, 34 events.
- `dotnet run … -- replay-file content/replays/envelope-full.cwreplay` —
  `format : replay-command v1, canonical 4`; `checkpoints : OK (24 ticks
  match)`; 75 events.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive` — `FSharp.Core 10.1.303` only.
- Source scan of `src/CommandoWar.Sim/*.fs` for
  `float|stopwatch|datetime|system\.random|godot` — only pre-existing
  doc-comment prose; `Appraisal.fs` contributes none.
- `git status --porcelain` — matches the Changes list. Nothing under the client
  spikes, `src/_scratch`, `bench/`, `content/benchmarks/`.

### Evidence

- **`Canonical.FormatVersion` is 4:** `cwheadless fixture` / `corpus` /
  `replay-file` all report it; `CanonicalHashTests` / `FixtureTests` /
  `PathfindingTests` / `ScenarioTests` / `SightTests` assert it.
- **The re-pin is behaviour-neutral for movement:** the table above — tick
  counts identical for all ten pre-existing entries; event counts move by one
  `OrderAppraised` per order; `blocked-goal` / `lost-comms` unchanged at 2.
- **Appraisal is grounded in known threats, not omniscience:** stage 3 reads
  `WorldState.TacticalKnowledge`; the `exposed-approach` entry and
  `SimulationTests` "an order along a route exposed to a known threat is
  Refused …"; `perception-contact`'s order is `Accepted` at tick 1 because the
  threat is not known then.
- **Two agents appraise the same order differently for inspectable reasons:**
  the `exposed-approach` golden (agent 0 `refused route-too-exposed
  threat-agent-2`, agent 1 `accepted`) and `SimulationTests` "the same exposed
  order is Accepted for a high-discipline agent".
- **Appraisal never Accepts after a hard feasibility failure:** `SimulationTests`
  focused fact + `blocked-goal`; property 8's `Unable(NoKnownRoute)` check.
- **Diagnostics standing rule satisfied:** `Overlay.OrderAppraisal` derived in
  both `frame` and `frameOf`, rendered in `Ascii` + `Svg`, pinned by a
  hand-built fact and the committed `exposed-approach-tick-001.*` golden.
- **No PRNG draw:** property 8 asserts `Random.Draws = 0`; `SimulationTests`
  "order appraisal is deterministic … and draws no randomness".

### Deviations and unresolved issues

- **`blocked-goal` changed behaviour** (not just byte layout): its order is now
  `Unable(NoKnownRoute)` at appraisal, not `Accepted`-then-`MovementBlocked`.
  Tick count (5) and event count (2) are unchanged; the tick-1 event is now
  `OrderAppraised(Unable …)`. This is the intended realisation of `docs/09`
  section 2.2 and was confirmed with Dave (Decision H). Direct `MovementBlocked`
  coverage stays in `SimulationTests` (the movement-phase logic is unchanged,
  just no longer reached by this corpus path).
- **`demo.html` gains appraisal markers.** `DemoScenario`'s two friendlies are
  ordered, so `demo.html` gains an `order-appraised` event marker and an
  `OrderAppraisal` overlay per friendly across the run. `DemoScenario` is not a
  `.cwlog` corpus entry or the fixture, so this is not a stop-and-report
  finding (the TASK-026 `demo.html` `KnownContact` precedent). `demo.ascii.txt`
  / `demo.svg` render `Diagnostics.frame` at tick 0 (before any order), so they
  moved only in the footer.
- **`PlayerIntent` / `Urgency` / `RiskTolerance` moved `Commands.fs ->
  Domain.fs`.** Necessary — `AgentState.Order` carries a `PlayerIntent` and
  `Domain.fs` compiles before `Commands.fs`. Minimal (three type definitions,
  no fsproj reorder); `Commands.fs` keeps `PlayerCommand` / `CommandRejection`
  / `Command` and references them from `Domain`. Same namespace, so no
  client-spike or test churn.
- **`AppraisalConfig.DisciplineDefault` is duplicated as `Agent.DisciplineDefault`.**
  `Agent.create` (in `Domain.fs`) needs the default but `Appraisal.fs` compiles
  after `Domain.fs`. Both are `[<Literal>] = 3` with cross-referencing
  comments; a module reorder to unify them was judged not worth it.
- **Appraisal recomputes the stage-2 route that Navigation then recomputes.**
  A small pure duplication of one `Pathfinding.findWithin` call, preferred over
  coupling the phases through the `AgentState.Route` cache.
- **Hysteresis constant not added** (Decision D permits it). The only in-scope
  reappraisal trigger is "new order received", so a hysteresis band on the
  `Accepted -> Refused` edge has nothing to act on; B-021 adds it with the
  exposure-band trigger.
- **`bench/` appraisal slot not filled.** No read-only appraisal benchmark run;
  the commented slot is for whoever next touches the benchmark harness.

### Documents updated

- `tasks/TASK-028-ORDER-APPRAISAL-AND-TYPED-REASONS.md` (Outcome, Status
  `ready -> review`, acceptance boxes)
- `src/CommandoWar.Sim/{Domain,Appraisal (new),Commands,Simulation,Events,
  Canonical,Scenario,Diagnostics}.fs`, `CommandoWar.Sim.fsproj`
- `src/CommandoWar.Headless/{DiagnosticRender,Corpus,DemoScenario,LosDemo,
  PathDemo}.fs`
- `content/replays/` (ten `.md` re-pinned, new `exposed-approach.{cwlog,md}`,
  `envelope-full.{cwreplay,md}` re-pinned, `CORPUS.md`)
- `content/fixtures/SPIKE-FIXTURE.md`
- `content/diagnostics/` (every hash-footer golden, new
  `exposed-approach-tick-001.*` + `blocked-goal-tick-001.*`, `README.md`)
- `tests/CommandoWar.Sim.Tests/{SimulationTests,DeterminismPropertyTests,
  CanonicalHashTests,DiagnosticsTests,FixtureTests,ReplayTests,CorpusTests,
  PathfindingTests,ScenarioTests,SightTests}.fs`
- `docs/03_ARCHITECTURE.md` (section 7),
  `docs/04_SIMULATION_SPEC.md` (sections 2, 11, 12.2, 12.5, 12.6, 13, 14, 17, 20),
  `docs/05_COMMAND_AND_AGENT_AI.md` (sections 5, 6, 7, 8, 13, 14, 15, 16),
  `docs/07_VERTICAL_SLICE.md` (section 8),
  `docs/09_TEST_STRATEGY.md` (sections 2.1, 2.2, 2.3)
- `docs/11_BACKLOG.md` (new TASK-028 row; B-017 `-> done`; B-018 / B-021 /
  B-023 unblocked notes)
- `docs/12_PROGRESS_LEDGER.md` (index row; this file; "Pinned facts"
  `Canonical.FormatVersion` `3 -> 4`, shared-fixture hashes, "Green tests")
- `PROJECT_STATE.yaml` (`active_work`)
- also: `tasks/TASK-027-*.md`, `docs/ledger/2026-09-07-TASK-027-*.md`,
  `docs/11`, `docs/12` (the TASK-027 acceptance commit)
- this entry

### AGENTS.md / docs/09 section 8 standing rule

Applies. `AgentState.Order` / `.Disposition` are new authoritative tactical
state and `OrderAppraised` a new outcome fact. Both surface through
`Overlay.OrderAppraisal` (a new `Overlay` case, derived in `Diagnostics.frame`
**and** `frameOf`), rendered in `DiagnosticRender.Ascii` and `.Svg`, and pinned
by a hand-built `DiagnosticsTests` fact and the committed
`content/diagnostics/exposed-approach-tick-001.*` golden (plus
`blocked-goal-tick-001.*` and `demo.html`). Regeneration commands are in
`content/diagnostics/README.md`.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-08)
- Notes: the Appraisal phase (12.5) is a real phase in `Phases.order` slot 5
  over the new `Appraisal.fs` leaf. The Communication phase writes the new
  canonical `AgentState.Order` (and resets `Disposition`) instead of
  `Destination`; the Appraisal phase runs stages 1-4, produces an
  `OrderDisposition`, emits `OrderAppraised`, and on `Accepted` writes
  `Destination`. `AgentState.Order` / `.Disposition` enter `Canonical.encode`;
  static `AgentState.Discipline` stays out (the `CommunicationAvailable`
  precedent); `Canonical.FormatVersion` `3 -> 4` with a full re-pin that is
  behaviour-neutral for movement — confirmed: build 0/0, 242 green, `-- corpus`
  10/10, `-- fixture` format 4 / 34 events, `-- replay-file envelope-full` OK at
  canonical 4, `corpus --regenerate` a byte-identical zero diff, `git status`
  clean, `src/CommandoWar.Sim` packages `FSharp.Core` only, source scan clean.
  New `exposed-approach` corpus entry (the G3 divergence: agent 0 `Refused
  RouteTooExposed (Some 2)`, agent 1 `Accepted`), `blocked-goal` repurposed to
  `Unable(NoKnownRoute)` at appraisal (tick/event count unchanged). New
  `Overlay.OrderAppraisal` (`frame` + `frameOf`), ASCII + SVG renderers, new
  `exposed-approach-tick-001.*` and `blocked-goal-tick-001.*` goldens. No ADR.
  Merged to `main` (`--no-ff`, branch `task-028-order-appraisal` deleted; not
  pushed).
