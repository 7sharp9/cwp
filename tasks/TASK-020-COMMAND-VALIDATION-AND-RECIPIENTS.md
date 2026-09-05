# TASK-020: Command validation and recipient selection

Status: ready
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises backlog B-014
Size: S

## Objective

Land the `docs/04_SIMULATION_SPEC.md` section 13 command envelope in full
("command ID, issuer, recipients, issue tick, urgency, and risk tolerance")
and the section 12.1 Command intake validation rules ("reject malformed,
unauthorised, impossible-to-address, or duplicate commands") that the current
single-agent, single-reason `PlayerCommand` / `commandIntake` do not yet
cover. `src/CommandoWar.Sim/Commands.fs` already flags this exact gap: "Recipients
beyond a single agent, urgency and risk tolerance are deferred to the
command-validation task (backlog B-014)."

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
  the same event sequence and state hash as before. Command-level failures
  (empty recipients, duplicate command ID) that have no single associated
  agent emit exactly one `CommandRejected` for the whole command instead.
- **New `CommandRejection` cases, each scoped to the exact 12.1 wording it
  answers, with no invented authority model.**
  - `EmptyRecipients` — "malformed": a command naming zero recipients.
  - `UnauthorisedRecipient of AgentId` — "unauthorised": the vertical slice
    has one player commanding all `Friendly`-side agents only (`docs/05`
    section 12: enemies use a simpler, non-player-commanded doctrine). A
    command naming a `Hostile`-side agent as a recipient is rejected on that
    basis. This is the whole scope of "authorisation" landed here — no
    issuer identity, commander hierarchy, or per-agent permission model is
    introduced; nothing in the current design needs one yet.
  - `DuplicateCommandId of CommandId` — "duplicate": scoped to duplicate
    `CommandId`s within the same tick's incoming command batch only. Every
    existing content author and test helper already assigns unique,
    monotonically increasing command IDs; cross-tick duplicate tracking
    would require persisting every previously-accepted command ID forever,
    an unbounded, unevidenced cost. Reopen only if replay evidence shows a
    real cross-tick collision.
  - `UnknownAgent` / `TargetOutOfBounds` (existing) are unchanged.
- **Validation order per command:** duplicate-ID check (batch-level, first)
  -> empty-recipients check -> target-in-bounds check (once per command, not
  per recipient — a `MoveTo` target is equally out-of-bounds for every
  recipient) -> per-recipient checks, in ascending `AgentId` order regardless
  of authoring order in `Recipients` (existing "stable entity ordering" rule):
  unknown-agent, then unauthorised-recipient, then accept. A command's first
  failing whole-command check short-circuits the rest (no per-recipient
  events emitted for a command already rejected as duplicate, empty, or
  out-of-bounds).
- **A repeated `AgentId` within one command's `Recipients` is not
  deduplicated or rejected.** Each occurrence is validated and, if valid,
  emits its own `CommandAccepted` and reapplies the same `Destination`
  (idempotent, not incorrect). No evidence motivates rejecting it.
- **Command intake now records before it applies**, per 12.1's exact wording
  ("record accepted commands before effects are applied"): `commandIntake`
  emits `CommandAccepted` before writing `Destination`, reversing the current
  emit-after-apply order. Not observable in any test that only inspects the
  final event list and end-of-tick state (both are unaffected by the
  intra-tick emit/apply order), but it is the correct fix per spec.
- **Issue-tick eligibility/scheduling is explicitly out of scope.** The
  `Commands.fs` comment that names B-014 attributes only recipients, urgency,
  and risk tolerance to it; the separate comment "`IssueTick` is carried for
  replay; eligibility/scheduling by issue tick is not yet enforced" names no
  owning task. Enforcing it (rejecting a command whose `IssueTick` does not
  match the tick it is being processed on, or building a future-tick queue)
  is a real, separately-scoped feature, not inferred into this one.
- **No `Canonical.FormatVersion` or `CommandLogFile.Version` change.**
  `PlayerCommand` is not part of `Canonical.encode` (only `WorldState` is);
  the `.cwlog` text grammar stays `<tick> <agentId> move <x> <y>` (one
  recipient per line) — `CommandLogFile.parse` calls the unchanged
  `Command.moveTo`, so every existing fixture and corpus `.cwlog` file, and
  every hash they pin, is untouched. Multi-recipient authoring from a
  `.cwlog` file (a `<tick> <agentId>[,<agentId>...] move <x> <y>` grammar) is
  not implemented here — `Command.moveToMany` is exercised only from code
  (new unit tests), the same discipline `PathfindingTests.fs`'s hand-built
  `Terrain` uses. Reopen as a follow-up only if content authoring needs it.

## Diagnostics

`AGENTS.md`'s diagnostic-extension rule does not apply: this task adds no
new authoritative spatial or tactical state to `AgentState` / `WorldState` /
`DiagnosticFrame` — `Recipients`, `Urgency`, and `RiskTolerance` live on the
transient `PlayerCommand` input, not on persisted state. The new
`CommandRejection` cases do reuse the existing `"command-rejected"` event
kind in `Diagnostics.eventMarker`, which pattern-matches `CommandRejection`
exhaustively and therefore needs a new arm per case (`Cells = [||]`, the same
treatment `UnknownAgent` already gets — no cell is meaningfully implicated by
an empty-recipients, duplicate-ID, or unauthorised-recipient rejection). No
new golden is required; if `DiagnosticsTests.fs` already exhaustively lists
`CommandRejection` cases anywhere, extend it there rather than add new
golden output.

## Allowed scope

- `src/CommandoWar.Sim/Commands.fs` (`Urgency`, `RiskTolerance`,
  `PlayerCommand.Recipients` / `.Urgency` / `.RiskTolerance`, new
  `CommandRejection` cases, `Command.moveTo` defaults, new
  `Command.moveToMany`);
- `src/CommandoWar.Sim/Simulation.fs` (`commandIntake`: duplicate-ID
  tracking, empty-recipients and target-bounds whole-command checks, the
  per-recipient loop in ascending `AgentId` order, emit-before-apply
  ordering);
- `src/CommandoWar.Sim/Diagnostics.fs` (`eventMarker` new
  `CommandRejection` match arms);
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs` (new facts: multi-recipient
  accept, hostile-recipient rejection, empty-recipients rejection,
  duplicate-command-ID rejection within one tick, repeated-recipient
  idempotence) and `DiagnosticsTests.fs` only if it already exhaustively
  matches `CommandRejection`;
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
- Touching the client spikes, `src/_scratch`, or
  `bench/CommandoWar.Benchmarks/`.

## Acceptance criteria

- [ ] A command naming multiple `Friendly` recipients accepts each
      individually addressable recipient and emits one `CommandAccepted` per
      recipient (`SimulationTests.fs`).
- [ ] A command naming a `Hostile`-side recipient is rejected
      `UnauthorisedRecipient`; a `Friendly` co-recipient in the same command
      still accepts (`SimulationTests.fs`).
- [ ] A command with an empty `Recipients` list is rejected `EmptyRecipients`
      with no per-recipient events (`SimulationTests.fs`).
- [ ] Two commands sharing a `CommandId` within the same tick's batch: the
      first is processed normally; the second is rejected
      `DuplicateCommandId` (`SimulationTests.fs`).
- [ ] Every pre-existing `SimulationTests.fs`, `CorpusTests.fs`,
      `ReplayTests.fs`, `DeterminismPropertyTests.fs`, `FixtureTests.fs`, and
      `ScenarioTests.fs` fact passes unmodified (`Command.moveTo`'s signature
      is unchanged).
- [ ] `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
      and `-- fixture` reproduce their committed hashes exactly (no re-pin).
- [ ] `dotnet build CommandoWar.slnx -c Release` = 0 warnings, 0 errors.
- [ ] `dotnet list src/CommandoWar.Sim package --include-transitive` =
      `FSharp.Core` only; source scan of `src/CommandoWar.Sim` clean.
- [ ] `docs/04` sections 12.1 / 13, `docs/09` section 2.1, backlog rows,
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
