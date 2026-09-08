## 2026-09-07 - TASK-027 - Communication constraints and order delivery

**Owner:** Dave with coding-agent assistance
**Branch:** `task-027-communication-and-order-delivery` off `main` at `cd434b6`
(TASK-026 merge on top). Committed locally; not pushed.
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; .NET 10.0.11;
FSharp.Core 10.1.303; xUnit 2.9.3; FsCheck 3.3.4
**Status change:** TASK-027 `ready -> review`; backlog B-016 `proposed -> done`
(communication-failure half); new B-016b `proposed`

Realises the communication half of P3 Required work "implement communication
and shared tactical knowledge" (`docs/08` section 6 — TASK-026 landed the
shared-knowledge half). Turns the `docs/04` section 12.2 Communication phase
from a no-op into a real phase, and makes appraisal stage 1 "Was the order
received?" (`docs/05` section 5) a real, inspectable fact — the direct
precondition for B-017. No ADR: `AgentState.CommunicationAvailable` is static
authoritative data governed by the existing ADR-0002 amendment "Static
authoritative data and the canonical image".

### Central decisions (confirmed with Dave before the phase bodies)

- **A — static reachability check, no PRNG.** A per-recipient check that is a
  pure function of authored scenario data. New `AgentState.CommunicationAvailable:
  bool` (default `true`; an authored `false` is a "comms blackout"). Zero
  delivery delay. `CommunicationAvailable` is **excluded from
  `Canonical.encode`** exactly as `WorldState.Terrain` is — static, cannot
  diverge, and its effects still surface in the hash within one tick via
  `Position`. No PRNG draw (the stream's first gameplay consumer stays combat
  spread, B-019).
- **B — the pending order is transient, not canonical.** `commandIntake`
  records an accepted `(command, recipient, target)` as a `StepState`-local
  list; the Communication phase drains it the same tick. Never crosses a tick
  boundary, so it is not authoritative state: `WorldState` / `AgentState` gain
  no field, **`Canonical.FormatVersion` stays 3, zero hash re-pin**. Consistent
  with TASK-024 ("`WorldState` holds no command history"). Rejected the
  canonical `AgentState.PendingOrder` + `FormatVersion` 3 → 4 alternative as
  speculative (no B-016b design exists) and a TASK-026-scale re-pin for nothing.
- **C — report aging out of scope.** Split radio range, non-zero delivery
  delay, dynamic jamming, radio-destroyed, and report aging beyond the
  `StaleAfter` / `ExpireAfter` bands to a new **B-016b**.
- **D — `OrderUndelivered` only, no `OrderDelivered`.** With zero delay and
  comms available, `CommandAccepted` (at intake, carrying the destination cell)
  already records "received this tick"; a same-tick `OrderDelivered` carries no
  new information and is the "flood of low-value events" `docs/04` section 14
  forbids. Failure is the asymmetric, information-bearing case (like
  `MovementBlocked` / `MovementObstructed`). Consequence: **no new event fires
  for any of the nine existing entries**, so there is no event-count re-pin.
  B-016b adds `OrderDelivered` when delivery can lag acceptance.
- **E — no ADR.** The ADR-0002 amendment already governs the static-data
  exclusion and the future format-version bump when B-016b makes comms
  availability mutable.

### Changes

- **`src/CommandoWar.Sim/Domain.fs`.** `AgentState.CommunicationAvailable: bool`
  — doc comment: static authored data, excluded from `Canonical.encode` like
  `Terrain` (ADR-0002 amendment), becomes canonical when B-016b makes it
  mutable. `Agent.create` initialises it `true` (unchanged signature — the
  scenario path overrides with `{ Agent.create … with CommunicationAvailable =
  d.CommunicationAvailable }`, so no client-spike / test churn).
- **`src/CommandoWar.Sim/Events.fs`.** New `type DeliveryFailure =
  | UnableToCommunicate` (name matches `docs/05` section 7 `DecisionReason`).
  New `EventBody.OrderUndelivered of command: CommandId * recipient: AgentId *
  reason: DeliveryFailure`. `DomainEvent` ordering doc comment rewritten as a
  numbered list: command outcomes → `OrderUndelivered` (ascending
  `(recipient, command)`) → `ContactObserved` / `ContactExpired` → movement
  outcomes.
- **`src/CommandoWar.Sim/Simulation.fs`.** `StepState.PendingOrders:
  (CommandId * AgentId * Cell) list`, seeded `[]` in `step`. `commandIntake`
  reworked: reads `s.Agents` (no `Array.copy` — it no longer mutates agents),
  emits `CommandAccepted` at acceptance, and `pending.Add(cmd.Id, recipient,
  target)` instead of writing `Destination`; sets `s.PendingOrders`. New
  `communication` phase (header comment "Realised by TASK-027"): copies the
  agent array, processes `s.PendingOrders` ascending `(recipient, command)` —
  `CommunicationAvailable = true` → write `Destination`; `false` → emit
  `OrderUndelivered`; clears `s.PendingOrders`. `runPhase` — `Communication`
  moved from the no-op list to a real arm. `step` doc comment updated.
  `World.ofScenario` maps `Deployment.CommunicationAvailable` onto the agent.
- **`src/CommandoWar.Sim/Canonical.fs`.** `writeAgent` comment only:
  `CommunicationAvailable` deliberately not written (the `Route` precedent, the
  `Terrain` argument). `FormatVersion` (3) and `encode` unchanged.
- **`src/CommandoWar.Sim/Scenario.fs`.** `RawDeployment.CommunicationAvailable:
  bool` and `Deployment.CommunicationAvailable: bool`; `toDeployments`
  carry-through. No new `ScenarioError` (a `bool` cannot be malformed).
- **`src/CommandoWar.Sim/Diagnostics.fs`.** `AgentMarker.CommunicationAvailable:
  bool` (doc comment: rendered only when `false`); `agentMarkers` populates it.
  `Overlay.UndeliveredOrder of recipient: AgentId * at: Cell * command:
  CommandId` + doc-comment reservation line ("B-016 communication ->
  `UndeliveredOrder`"). `eventMarker` gains `OrderUndelivered _ -> { Kind =
  "order-undelivered"; Cells = [||] }`. New `undeliveredOrderOverlays (result)`
  — one per distinct recipient from this tick's `OrderUndelivered` events, cell
  from the post-step world; wired into `frameOf` only (`frame` never emits one,
  the `Reserved` / `Obstructed` precedent). `reservationOverlays` /
  `obstructionOverlays` `e.Body` filters gain `OrderUndelivered _ -> None`.
- **`src/CommandoWar.Headless/DiagnosticRender.fs`.** `Ascii`: `no-comms`
  suffix on the roster line when `not a.CommunicationAvailable`;
  `undelivered order (x,y): agent N  command M  (communication unavailable)`
  overlay text line. `Svg`: a dashed red (`#e53e3e`) ring around a blacked-out
  agent; `UndeliveredOrder` → a red dashed box with a struck-through diagonal
  and an `!<id>` label. FS0025 sites (each fixed with the intended branch):
  | # | site | fix |
  |---|---|---|
  | 1 | `Ascii` `sightRays` filter | `\| UndeliveredOrder _ -> None` |
  | 2 | `Ascii` `plannedPaths` filter | `\| UndeliveredOrder _ -> None` |
  | 3 | `Ascii` overlay-text `match o` | `\| UndeliveredOrder(recipient, at, command) -> line (…)` |
  | 4 | `Svg` overlay `match o` | `\| UndeliveredOrder(recipient, at, _) -> <rect …> + <line …> + <text>!N</text>` |
  | 5 | `DiagnosticsTests` `convergingRoutes` `tryPick` | `\| UndeliveredOrder _ -> None` |
  | 6 | `DiagnosticsTests` `swapStandoff` `choose` | `\| UndeliveredOrder _ -> None` |
  | 7 | `DiagnosticsTests` `perceptionContact` `tryPick` | `\| UndeliveredOrder _ -> None` |
  (Items 1–4 were `error FS0025` under `TreatWarningsAsErrors` in the two source
  projects; 5–7 surfaced as warnings in the test project and were fixed the
  same way.)
- **`src/CommandoWar.Headless/Corpus.fs`.** `rawScenario` gains a
  `commsBlackout: int list` parameter; a local `deployment` helper sets
  `CommunicationAvailable = not (List.contains a commsBlackout)`. The seven
  existing `rawScenario` calls gain `[]`. New `lostCommsWorld` (8×8, friendly 0
  at (1,4), `commsBlackout = [ 0 ]`) and its `lost-comms` `Entry` (4 ticks).
- **`src/CommandoWar.Headless/{DemoScenario,LosDemo,PathDemo}.fs`.** The
  `RawDeployment` literals gain `CommunicationAvailable = true` (compile-only,
  behaviourally identical).
- **`content/replays/`.** New `lost-comms.cwlog` (hand-written, format v1) +
  `lost-comms.md` (`corpus --regenerate`-generated). `CORPUS.md` gains the
  `lost-comms` row. **No other file in the directory moved.**
- **`content/diagnostics/`.** New `lost-comms-tick-001.{ascii.txt,svg}`
  (generated by `DiagnosticRender.runFrames` over the `lost-comms` entry at
  tick 1, via a throwaway `dotnet fsi` script mirroring `lostCommsFrames ()`).
  `README.md` gains the two golden rows and the regeneration note. **No
  pre-existing golden moved.**
- **`tests/CommandoWar.Sim.Tests/`.**
  - `SimulationTests.fs`: `commsBlackoutWorld` / `undeliveredIn` helpers and
    five facts — reachable recipient delivered the same tick it is accepted
    (`Destination` set, no `OrderUndelivered`, movement begins **this** tick
    since Navigation runs at phase 7); unreachable recipient →
    `OrderUndelivered(UnableToCommunicate)`, no `Destination`, never moves;
    multi-recipient `MoveTo` delivers to the reachable recipients and reports
    only the cut-off one; an undelivered order does not cancel a `Destination`
    the recipient already held; delivery deterministic across two runs.
  - `DeterminismPropertyTests.fs`: new `commsCaseGen` (`randomCaseGen` with a
    random 1-in-4 subset of agents comms-blacked-out); property 7
    (`MaxTest = 200`): a blacked-out agent never holds a `Destination` and
    never leaves its start cell, every `OrderUndelivered` names a blacked-out
    recipient and pairs with a same-tick `CommandAccepted`, no comms-available
    agent is ever reported undelivered. Properties 1–6 unmodified.
  - `CanonicalHashTests.fs`: "communication availability is static authored
    data outside the canonical image" — flipping every agent's
    `CommunicationAvailable` leaves `Canonical.encode` and the hash unchanged
    (`FormatVersion` still 3), but the suppressed order changes the hash within
    one tick via `Position`.
  - `DiagnosticsTests.fs`: hand-built `UndeliveredOrder` overlay fact;
    `AgentMarker.CommunicationAvailable` fact (`no-comms` only when `false`,
    the dashed ring in SVG); `lostCommsFrames ()` helper + the
    `lost-comms-tick-001.*` golden fact (overlay recipient/cell/command, the
    `order-undelivered` event marker, every frame's agents have `Destination =
    None`).
  - `ScenarioTests.fs`: the 10 `RawDeployment` literals gain
    `CommunicationAvailable = true`; new fact "communication availability
    round-trips through validation" (default `true`; an authored `false`
    survives onto the `Deployment`).
- **Docs.** `docs/03` section 7 (step 2 realisation note), `docs/04` sections
  11 / 12.1 / 12.2 / 14 / 17 / 20, `docs/05` section 5 (stage 1 realisation
  note), `docs/09` sections 2.1 / 2.2 / 2.3. `docs/11` TASK-027 row, B-016
  `-> done`, new B-016b row, B-017 unblocked note. `docs/12` index row + this
  file + "Green tests" `216 -> 228`. `PROJECT_STATE.yaml` `active_work`.
  `tasks/TASK-027-*.md` (Outcome, Status, acceptance boxes).

### The zero-re-pin proof

Every committed scenario has `CommunicationAvailable = true` for every agent,
so every accepted order is delivered the same tick, writing the same
`Destination`. Nothing in `Phases.order` runs between `CommandIntake` (slot 1)
and `Communication` (slot 2), so moving the `Destination` write between them
changes no observable end-of-tick state. `CommunicationAvailable` is not in
`Canonical.encode`. No new event fires for a comms-available agent.

Result: `cwheadless corpus` (the eight pre-existing entries) and `cwheadless
fixture` reproduce their committed tables **byte-identically** — no
`--regenerate` needed. `git diff content/replays` after `corpus --regenerate`
shows only `lost-comms.{cwlog,md}` added and the `CORPUS.md` row; re-running
`--regenerate` is a zero diff. `git diff content/diagnostics` is empty for
every pre-existing golden. `envelope-full.cwreplay` checkpoints OK at
`canonical 3`. `Canonical.FormatVersion` stays `3`.

### New corpus entry: `lost-comms`

8×8, seed 20260904, 1 friendly, `commsBlackout = [ 0 ]`. Agent 0 at (1,4)
ordered east to (6,4) on tick 1. Command intake accepts (`CommandAccepted`);
the Communication phase emits `OrderUndelivered(1, agent 0,
UnableToCommunicate)` and drops the order. The agent never gets a `Destination`
and never leaves (1,4).

- Initial hash `0x9D83E88BAF8CC5C6` (identical to `blocked-goal`'s — same grid,
  agent id, position, and seed, and terrain / `CommunicationAvailable` are both
  outside the canonical image; a useful built-in check that the flag is not
  hashed), final hash `0x19CDEA8038A8C122`.
- 4 ticks, 2 domain events (1 `CommandAccepted` + 1 `OrderUndelivered`).
- Golden `content/diagnostics/lost-comms-tick-001.{ascii.txt,svg}`.

### Verification

- `dotnet build CommandoWar.slnx -c Release` — `Build succeeded. 0 Warning(s)
  0 Error(s)`.
- `dotnet test CommandoWar.slnx -c Release` before any edit — `Passed: 216`.
- `dotnet test CommandoWar.slnx -c Release` after — `Passed: 228` (`+12`).
  New: five `SimulationTests` facts (named above); one `DeterminismPropertyTests`
  property 7 (`MaxTest = 200`); one `CanonicalHashTests` fact; two hand-built
  `DiagnosticsTests` facts + one `lost-comms` golden fact; one `ScenarioTests`
  round-trip fact; the `lost-comms` `CorpusTests` `[<Theory>]` case.
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
  before any edit — `OK - all 8 entries match their committed tables`; fixture
  `canonical format : 3`, initial `0x50BFA007EDFC42FE`, final
  `0xD9D6EC3DDC1D602F`, 33 events.
- `-- corpus` after — `OK - all 9 entries match their committed tables`;
  fixture unchanged (`0x50BFA007EDFC42FE` / `0xD9D6EC3DDC1D602F`, 33 events).
- `-- corpus --regenerate` then `git diff content/replays` — only
  `lost-comms.{cwlog,md}` added + the `CORPUS.md` row; `git diff --stat --
  'content/replays/*.md'` shows only `CORPUS.md | 1 +`. `git add` + re-run
  `--regenerate` = zero diff.
- `-- replay-file content/replays/envelope-full.cwreplay` —
  `format : replay-command v1, canonical 3`; `checkpoints : OK (24 ticks match
  the file's committed hashes)`; final `0x4E5963A2C8C83660`.
- `git diff content/diagnostics` — empty (no pre-existing golden moved); only
  `lost-comms-tick-001.*` + the `README.md` rows are new.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive` — `FSharp.Core 10.1.303` only.
- Source scan of `src/CommandoWar.Sim/*.fs` for
  `float|stopwatch|datetime|system\.random|godot` (case-insensitive,
  comment lines filtered) — no matches; `communication` code contributes none.
- `git status --porcelain` — matches the Changes list. Nothing under the
  client spikes, `src/_scratch`, `bench/`, or `content/benchmarks/`.

### Evidence

- **`Canonical.FormatVersion` stays 3 and no hash re-pinned:** `cwheadless
  corpus` / `fixture` / `replay-file` all still report `3` and reproduce every
  committed table byte-identically with no `--regenerate`; `CanonicalHashTests`
  asserts flipping `CommunicationAvailable` leaves `encode` / the hash /
  `FormatVersion` unchanged.
- **Delivery gates on `CommunicationAvailable`:** `SimulationTests` "an order
  to an unreachable recipient emits `OrderUndelivered` and sets no destination";
  property 7 recomputes the invariant over 200 generated cases.
- **The phase split is behaviour-neutral for delivered orders:**
  `SimulationTests` "an order to a reachable recipient is delivered the same
  tick it is accepted" (agent steps one cell that tick, exactly as before);
  the byte-identical corpus / fixture.
- **Failure is inspectable:** the `lost-comms` corpus entry (2 events, agent
  never moves); the `lost-comms-tick-001.*` golden (`no-comms` roster line,
  `undelivered order` overlay, `order-undelivered` event marker); the
  hand-built `DiagnosticsTests` facts.
- **Green count:** `dotnet test` `216 -> 228`.

### Deviations and unresolved issues

- **`RawDeployment` gained a required field, touching 15 construction sites**
  (`Corpus.rawScenario` funnel, the three headless demo scenarios,
  `ScenarioTests`). All set `CommunicationAvailable = true` and are
  behaviourally identical. An `RawScenario`-level `commsBlackout: int[]` would
  have touched a similar count; the per-deployment field is the conceptually
  right home (`docs/04` section 11 lists communication availability as agent
  state) and is where B-016b's mutable version lives.
- **`Agent.create` signature left unchanged.** F# module-level `let` functions
  cannot take optional parameters, and the function is called from the Godot
  (`SimFacade.cs`) and Mibo (`SimBridge.fs`) client spikes (forbidden scope).
  `World.ofScenario` overrides `CommunicationAvailable` with a `{ … with … }`
  copy instead. Every non-scenario path (tests, `Setup`, the property
  generators) takes the `true` default.
- **No `OrderDelivered` success event** (Decision D). B-016b adds it when
  delivery can lag acceptance; until then `CommandAccepted` + the `Destination`
  write fully record a delivery.
- **`OrderUndelivered` carries no cell** (matches `CommandRejected`). The
  `UndeliveredOrder` overlay supplies the spatial info (recipient's cell, read
  from the post-step world in `frameOf`).
- **`lost-comms` shares `blocked-goal`'s tick-0 hash.** Same grid / agent /
  seed; terrain and `CommunicationAvailable` are both outside the canonical
  image. Not a collision to worry about — it is positive evidence the flag is
  not hashed — but a reader diffing tick-0 hashes across entries should know.

### Documents updated

- `tasks/TASK-027-COMMUNICATION-AND-ORDER-DELIVERY.md` (Outcome, Status
  `ready -> review`, acceptance boxes)
- `src/CommandoWar.Sim/{Domain,Events,Simulation,Canonical,Scenario,
  Diagnostics}.fs`
- `src/CommandoWar.Headless/{DiagnosticRender,Corpus,DemoScenario,LosDemo,
  PathDemo}.fs`
- `content/replays/` (new `lost-comms.{cwlog,md}`, `CORPUS.md` row)
- `content/diagnostics/` (new `lost-comms-tick-001.*`, `README.md`)
- `tests/CommandoWar.Sim.Tests/{SimulationTests,DeterminismPropertyTests,
  CanonicalHashTests,DiagnosticsTests,ScenarioTests}.fs`
- `docs/03_ARCHITECTURE.md` (section 7),
  `docs/04_SIMULATION_SPEC.md` (sections 11, 12.1, 12.2, 14, 17, 20),
  `docs/05_COMMAND_AND_AGENT_AI.md` (section 5),
  `docs/09_TEST_STRATEGY.md` (sections 2.1, 2.2, 2.3)
- `docs/11_BACKLOG.md` (TASK-027 row; B-016 `-> done`; new B-016b row;
  B-017 unblocked note)
- `docs/12_PROGRESS_LEDGER.md` (index row; this file; "Green tests"
  `216 -> 228`; no "Pinned facts" change)
- `PROJECT_STATE.yaml` (`active_work`)
- this entry

### AGENTS.md / docs/09 section 8 standing rule

Applies. `AgentState.CommunicationAvailable` is new authoritative state that
affects agent behaviour, and `OrderUndelivered` a new failure fact. Both
surface through the diagnostic frame: `AgentMarker.CommunicationAvailable`
(rendered only when `false`) and `Overlay.UndeliveredOrder` (derived in
`frameOf`), rendered in `DiagnosticRender.Ascii` and `.Svg`, pinned by two
hand-built `DiagnosticsTests` facts and the committed
`content/diagnostics/lost-comms-tick-001.*` golden. Regeneration is in
`content/diagnostics/README.md`.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-08)
- Notes: the Communication phase (12.2) is a real phase; `commandIntake`
  records an accepted order as a `StepState`-local pending order and the
  Communication phase delivers it (`Destination` written) to a
  `CommunicationAvailable = true` recipient or emits
  `OrderUndelivered(UnableToCommunicate)` and drops it. New static
  `AgentState.CommunicationAvailable` excluded from `Canonical.encode` (the
  `Terrain` precedent), so `Canonical.FormatVersion` stays 3 and every pinned
  hash / tick count / event count on the nine pre-existing entries is
  byte-identical (confirmed: `-- corpus` 9/9, `-- fixture` unchanged with and
  without `--regenerate`, `-- replay-file envelope-full` OK at canonical 3).
  New `OrderUndelivered` event + `DeliveryFailure` DU, new
  `Overlay.UndeliveredOrder` + `AgentMarker.CommunicationAvailable`, new
  `lost-comms` corpus entry + golden. `216 -> 228` green. No ADR. Merged to
  `main` (`--no-ff`, branch deleted; not pushed).
