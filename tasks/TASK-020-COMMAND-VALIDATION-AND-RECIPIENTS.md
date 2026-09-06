# TASK-020: Command validation and recipient selection

Status: review (implemented 2026-09-06, pending Dave's acceptance)
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises backlog B-014
Size: S

## Outcome

Implemented 2026-09-06 in a single headless session. `PlayerCommand` now
carries `Recipients: AgentId list` (generalised from the single `Agent`
field — a back-compatible `member .Agent = List.head Recipients` read
accessor is kept so every pre-existing single-recipient call site and
`CorpusTests` fact compiles unchanged), inert `Urgency` (`Routine |
Immediate`) and `RiskTolerance` (`Cautious | Standard | Aggressive`) envelope
fields, and `Command.moveTo` builds a one-element list with the `Routine` /
`Standard` defaults. New `Command.moveToMany : CommandId -> int64 -> AgentId
list -> Cell -> Urgency -> RiskTolerance -> PlayerCommand`. Four new
`CommandRejection` cases: `EmptyRecipients`, `DuplicateRecipient of AgentId`,
`UnauthorisedRecipient of AgentId` (a `Hostile`-side recipient — per-recipient,
a `Friendly` co-recipient still accepts), `DuplicateCommandId of CommandId`
(rejects **every** command in a duplicated-id group within one tick's batch,
so the outcome is independent of batch order). `Simulation.commandIntake`:
batch-level duplicate-id-group rejection first; per-command whole-command
checks (`EmptyRecipients` -> `DuplicateRecipient` -> `TargetOutOfBounds`,
first failure short-circuits); per-recipient checks in ascending `AgentId`
order; `CommandAccepted` emitted before the destination is written; exactly
one `Array.copy s.Agents` + one `AgentId -> index` `Map` per phase invocation
(was one copy per accepted command — `BASELINE.md` O(n^2) hot spot).
`Diagnostics.eventMarker` gains one arm per new case (`Cells = [||]`, the
`UnknownAgent` treatment). This is a **partial** section 13 envelope: issuer
identity and issue-tick eligibility stay out, with mandatory-before-G3
follow-ups B-044 and B-045. `178 -> 184` green (+6 `SimulationTests` facts).
`dotnet build` `0/0`; `-- corpus` / `-- fixture` reproduce every committed
hash with no `--regenerate`; no `Canonical.FormatVersion` or
`CommandLogFile.Version` change. Detail:
`docs/ledger/2026-09-06-TASK-020-command-validation-and-recipients.md`.

## Objective

Generalise command addressing from a single agent to multiple recipients, add
inert `Urgency` and `RiskTolerance` envelope fields, and implement the
current-scope `docs/04_SIMULATION_SPEC.md` section 12.1 command-intake
validation rules ("reject malformed, unauthorised, impossible-to-address, or
duplicate commands"). `src/CommandoWar.Sim/Commands.fs` flags this gap:
"Recipients beyond a single agent, urgency and risk tolerance are deferred to
the command-validation task (backlog B-014)."

This is a **partial** realisation of the section 13 envelope, not the whole of
it. Two named parts of that envelope are explicitly out of scope and each has a
mandatory follow-up (see Central decisions): **issuer identity** (no issuer or
commander-authority model is introduced; "authorisation" here is only the
friendly/hostile-side check) and **issue-tick eligibility** (`IssueTick` stays
carried-but-unenforced; its semantics are unresolved and B-044 must settle them
before G3). The completion record must describe the result as a partial
envelope, not claim section 13 is landed "in full".

## Central decisions

- **`PlayerCommand.Recipients: AgentId list` replaces the single `Agent`
  field; existing single-agent call sites are unaffected.** `Command.moveTo`
  keeps its current four-argument signature
  (`id -> issueTick -> agent -> target -> PlayerCommand`), now building a
  one-element `Recipients` list with `Urgency = Routine` and `RiskTolerance =
  Standard` defaults. Every existing call site (10 test files, `Fixture.fs`,
  `DemoScenario.fs`, `CommandLogFile.fs`, `Corpus.fs` inputs) needs no change.
  A new `Command.moveToMany : CommandId -> int64 -> AgentId list -> Cell ->
  Urgency -> RiskTolerance -> PlayerCommand` is added for the multi-recipient
  and non-default urgency/risk-tolerance cases this task's own tests exercise.
- **`Urgency` and `RiskTolerance` are inert envelope data, not new
  behaviour.** `docs/05_COMMAND_AND_AGENT_AI.md` section 5 stage 4 names them
  as appraisal inputs ("urgency and risk tolerance encoded by the order"), but
  appraisal (B-017) does not exist yet. This task only carries the values
  through the envelope; nothing reads them. Minimal vocabulary, no design
  work beyond what section 5 already names:
  `type Urgency = Routine | Immediate` and
  `type RiskTolerance = Cautious | Standard | Aggressive`.
- **One accept/reject event per (command, recipient) pair, not one per
  command.** `CommandAccepted` / `CommandRejected` keep their existing
  per-agent shapes (`CommandAccepted of command * agent * destination`); a
  command naming three recipients can produce up to three events. This is a
  strict generalisation: every existing scenario has exactly one recipient
  per command, so it emits exactly the one event it always did, byte-for-byte
  the same event sequence and state hash as before. Whole-command failures
  (empty recipients, duplicate recipient, duplicate command ID) that have no
  single associated agent emit one `CommandRejected` for the command instead
  of per-recipient events.
- **New `CommandRejection` cases, each scoped to the exact 12.1 wording it
  answers, with no invented authority model.**
  - `EmptyRecipients` — "malformed": a command naming zero recipients.
    Whole-command rejection, one `CommandRejected`.
  - `DuplicateRecipient of AgentId` — "malformed": the same `AgentId` listed
    more than once in one command's `Recipients`. Whole-command rejection,
    one `CommandRejected` naming the first repeated id. A malformed envelope
    is refused with evidence rather than silently normalised: accepting it
    would emit duplicate `CommandAccepted` events, double-count command
    workload in any later telemetry, and let a later appraisal or
    action-cost pass (B-017/B-018) run twice for one agent. The caller
    should learn it built an invalid envelope.
  - `UnauthorisedRecipient of AgentId` — "unauthorised": the vertical slice
    has one player commanding all `Friendly`-side agents only (`docs/05`
    section 12: enemies use a simpler, non-player-commanded doctrine). A
    command naming a `Hostile`-side agent as a recipient is rejected on that
    basis. This is the whole scope of "authorisation" landed here — no
    issuer identity, commander hierarchy, or per-agent permission model is
    introduced; nothing in the current design needs one yet.
  - `DuplicateCommandId of CommandId` — "duplicate": scoped to duplicate
    `CommandId`s within the same tick's incoming command batch only. When a
    `CommandId` appears more than once in the batch, **every** command in
    that group is rejected and none is processed — one `CommandRejected` per
    command in the group. Rejecting all of them, rather than processing the
    first and rejecting the rest, is what makes the outcome independent of
    batch order: `Simulation.step` sorts the batch by `Id` with a stable
    sort (`List.sortBy`), so a "first wins" rule would let a caller change
    the authoritative result by reordering an invalid batch. No command
    should gain authority from malformed identity data. Cross-tick duplicate
    tracking stays out — B-044 (issue-tick semantics) is the task that
    decides whether command identity/scheduling becomes authoritative state
    at all.
  - `UnknownAgent` / `TargetOutOfBounds` (existing) are unchanged.
- **Validation order.** Batch-level first: find every `CommandId` that
  appears more than once and reject every command in each such group
  (`DuplicateCommandId`), before any other check or effect. Then, per
  surviving command, the whole-command checks in order — empty recipients
  (`EmptyRecipients`) -> repeated recipient (`DuplicateRecipient`) -> target
  in bounds (`TargetOutOfBounds`, checked once per command, not per
  recipient: a `MoveTo` target is equally out-of-bounds for every recipient)
  — the first failing check short-circuits with one `CommandRejected` and no
  per-recipient events. Then the per-recipient checks, in ascending `AgentId`
  order regardless of authoring order in `Recipients` (existing "stable
  entity ordering" rule): unknown-agent (`UnknownAgent`), then
  unauthorised-recipient (`UnauthorisedRecipient`), then accept.
- **A repeated `AgentId` within one command's `Recipients` rejects the whole
  command `DuplicateRecipient`.** It is malformed input, not a harmless
  idempotent re-application: the audit trail, any command-workload
  telemetry, and later per-agent appraisal/action-cost passes all treat two
  `CommandAccepted` events for one agent as two orders.
- **Command intake records before it applies**, per 12.1's exact wording
  ("record accepted commands before effects are applied"): `commandIntake`
  emits `CommandAccepted` before writing `Destination`, reversing the current
  emit-after-apply order. Not observable in any test that only inspects the
  final event list and end-of-tick state, but it is the correct fix per spec.
- **Command intake copies the agent array once per batch, not once per
  accepted recipient.** The current `commandIntake` does `Array.copy s.Agents`
  inside its per-command loop; `content/benchmarks/BASELINE.md` (2026-09-04)
  records this as the first measured hot spot — the 50-agent / 50-command
  tick does 50 copies of a 50-element array, ~86 KB/tick, O(n^2). Widening
  the loop to recipients would multiply it. The revised phase copies
  `s.Agents` once at the start, builds one `AgentId -> index` lookup once
  (agent identity and array order are stable within command intake — only
  `Destination` is written, no agent is added or removed), applies every
  accepted recipient to that one working array, and assigns `s.Agents` once
  at the end. A later command in the same batch still sees an earlier
  command's applied destination, as today. Contained fix with recorded
  evidence, not speculative optimisation.
- **Issue-tick eligibility/scheduling is out of scope and now has a
  mandatory owning task, B-044.** `IssueTick` stays carried-but-unenforced
  here. It is not merely unimplemented, it is *semantically undefined*: there
  are two tick concepts — `RecordedCommand.Tick` (the tick whose
  command-intake phase replays the command) and `PlayerCommand.IssueTick`
  (carried on the envelope) — and `CommandLogFile.parse` currently forces
  them equal by passing the same value to both. B-044 must decide, before G3:
  whether `IssueTick` means authored, submission, or acceptance time; whether
  future-dated commands queue or reject; whether stale commands are rejected;
  whether the replay tick and the issue tick must match; and whether command
  scheduling/identity is authoritative state (which also bounds the
  cross-tick duplicate-ID question above). B-044 is likely to rename the
  fields (e.g. `IssuedAtTick` / `SubmitAtTick`) so the two concepts stop
  sharing a name. Not "reopen if evidence requires" — required before the
  gate.
- **No `Canonical.FormatVersion` or `CommandLogFile.Version` change**, and
  `.cwlog` is hereby designated a **legacy fixture-script format, not the
  production replay-command format.** `PlayerCommand` is not part of
  `Canonical.encode` (only `WorldState` is), so no format version moves. The
  `.cwlog` grammar stays `<tick> <agentId> move <x> <y>`;
  `CommandLogFile.parse` (in `CommandoWar.Headless`, not `CommandoWar.Sim`)
  keeps calling the unchanged `Command.moveTo`, so every existing fixture and
  corpus `.cwlog` file and every hash they pin is untouched. After this task
  `.cwlog` can no longer represent an accepted command in full —
  multi-recipient addressing, `Urgency`, and `RiskTolerance` exist in the
  type and not in the grammar — which is acceptable only because `.cwlog` is
  test-input shorthand that is not expected to round-trip commands. The real
  decision (version `.cwlog` to carry the whole envelope, or a separate
  versioned replay-command serialisation) is **B-045, mandatory before G3**,
  since G3 requires replay to reproduce the complete decision trace over
  commands this format cannot express. `Command.moveToMany` is exercised only
  from code here (new unit tests), the same discipline
  `PathfindingTests.fs`'s hand-built `Terrain` uses.

## Diagnostics

`AGENTS.md`'s diagnostic-extension rule does not apply: this task adds no
new authoritative spatial or tactical state to `AgentState` / `WorldState` /
`DiagnosticFrame` — `Recipients`, `Urgency`, and `RiskTolerance` live on the
transient `PlayerCommand` input, not on persisted state. The new
`CommandRejection` cases do reuse the existing `"command-rejected"` event
kind in `Diagnostics.eventMarker`, which pattern-matches `CommandRejection`
exhaustively and therefore needs a new arm per case (`Cells = [||]`, the same
treatment `UnknownAgent` already gets — no cell is meaningfully implicated by
an empty-recipients, duplicate-recipient, duplicate-ID, or
unauthorised-recipient rejection). No
new golden is required; if `DiagnosticsTests.fs` already exhaustively lists
`CommandRejection` cases anywhere, extend it there rather than add new
golden output.

## Allowed scope

- `src/CommandoWar.Sim/Commands.fs` (`Urgency`, `RiskTolerance`,
  `PlayerCommand.Recipients` / `.Urgency` / `.RiskTolerance`, new
  `CommandRejection` cases `EmptyRecipients` / `DuplicateRecipient` /
  `UnauthorisedRecipient` / `DuplicateCommandId`, `Command.moveTo` defaults,
  new `Command.moveToMany`);
- `src/CommandoWar.Sim/Simulation.fs` (`commandIntake`: batch-level
  duplicate-`CommandId`-group rejection first; per-command whole-command
  checks — empty recipients, duplicate recipient, target bounds; the
  per-recipient loop in ascending `AgentId` order; emit-before-apply
  ordering; a single `Array.copy s.Agents` + one `AgentId -> index` lookup
  for the whole batch instead of a copy per accepted command);
- `src/CommandoWar.Sim/Diagnostics.fs` (`eventMarker` new
  `CommandRejection` match arms);
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs` (new facts: multi-recipient
  accept, hostile-recipient rejection with a friendly co-recipient still
  accepted, empty-recipients rejection, repeated-recipient whole-command
  rejection, duplicate-command-ID rejection of every command in the group
  within one tick, and order-independence of that rejection) and
  `DiagnosticsTests.fs` only if it already exhaustively matches
  `CommandRejection`;
- `docs/04_SIMULATION_SPEC.md` (section 12.1 realisation note, section 13
  envelope realisation note), `docs/09_TEST_STRATEGY.md` (section 2.1
  "command validation" realisation note);
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml`).

## Forbidden scope

- Appraisal, commitment, or any B-017/B-018+ behaviour — `Urgency` and
  `RiskTolerance` are carried, not consumed.
- An issuer identity or commander-authority model beyond the
  friendly/hostile-side check above.
- Issue-tick eligibility or scheduling enforcement.
- Cross-tick duplicate-command-ID tracking.
- A `.cwlog` text-format change or `CommandLogFile.Version` bump.
- A `Canonical.FormatVersion` bump or any change to `Canonical.encode`.
- Squad or formation concepts (`B-011d`, still unscoped).
- Editing the client spikes, `src/_scratch`, or
  `bench/CommandoWar.Benchmarks/` (running the benchmark read-only for the
  optional allocation evidence is fine; do not modify it or re-pin
  `content/benchmarks/BASELINE.md`).

## Acceptance criteria

- [x] A command naming multiple `Friendly` recipients accepts each
      individually addressable recipient and emits one `CommandAccepted` per
      recipient (`SimulationTests.fs`).
- [x] A command naming a `Hostile`-side recipient is rejected
      `UnauthorisedRecipient`; a `Friendly` co-recipient in the same command
      still accepts (`SimulationTests.fs`).
- [x] A command with an empty `Recipients` list is rejected `EmptyRecipients`
      with no per-recipient events (`SimulationTests.fs`).
- [x] A command listing the same `AgentId` twice in `Recipients` is rejected
      `DuplicateRecipient` as a whole command, with no `CommandAccepted` for
      that agent (`SimulationTests.fs`).
- [x] When two commands in one tick's batch share a `CommandId`, **both** are
      rejected `DuplicateCommandId` and neither destination is applied; the
      emitted events and end-of-tick state are identical whether the batch is
      submitted in one order or the reverse (`SimulationTests.fs`).
- [x] `commandIntake` performs exactly one `Array.copy s.Agents` per phase
      invocation regardless of how many commands or recipients are accepted
      (code review of `Simulation.fs`: the single `let agents = Array.copy
      s.Agents` is now above the `for cmd in commands` loop, with one
      `AgentId -> index` `Map` built once; the benchmark row was not
      re-run — evidence only, optional).
- [x] Backlog items B-044 (issue-tick semantics) and B-045 (command
      serialisation / `.cwlog` decision) exist as `proposed`, each marked
      mandatory before G3 (`docs/11_BACKLOG.md` section 3; `docs/08` section 6
      P3 Required work — added by the 2026-09-06 revision, unchanged here).
- [x] Every pre-existing `SimulationTests.fs`, `CorpusTests.fs`,
      `ReplayTests.fs`, `DeterminismPropertyTests.fs`, `FixtureTests.fs`, and
      `ScenarioTests.fs` fact passes unmodified (`Command.moveTo`'s signature
      is unchanged; `CorpusTests`' `.Command.Agent` reads resolve to the new
      back-compatible `PlayerCommand.Agent` accessor).
- [x] `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
      and `-- fixture` reproduce their committed hashes exactly (no re-pin).
- [x] `dotnet build CommandoWar.slnx -c Release` = 0 warnings, 0 errors.
- [x] `dotnet list src/CommandoWar.Sim package --include-transitive` =
      `FSharp.Core` only; source scan of `src/CommandoWar.Sim` clean.
- [x] `docs/04` sections 12.1 / 13, `docs/09` section 2.1, backlog rows,
      ledger index row + detail file, `PROJECT_STATE.yaml`, task status
      updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`
- `dotnet test CommandoWar.slnx -c Release` before and after (state new count)
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus` and
  `-- fixture` before and after — confirm byte-identical committed hashes
  (no `--regenerate` needed; a diff here would mean the "no behaviour change
  for existing single-recipient commands" decision was violated)
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
- source scan of `src/CommandoWar.Sim`
- `git status`

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
