# TASK-024: Issue-tick / submission-tick semantics

Status: ready
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises backlog B-044 — **mandatory before G3**
Size: S–M

## Objective

Give the two tick concepts the command loop carries a single, enforced
meaning, and land the minimal implementation that follows from it.

`PlayerCommand.IssueTick` (`src/CommandoWar.Sim/Commands.fs`) and
`RecordedCommand.Tick` (`src/CommandoWar.Sim/Replay.fs`) are two different
tick concepts that `CommandLogFile.parse`
(`src/CommandoWar.Headless/CommandLogFile.fs`) currently forces equal by
passing one `.cwlog` field to both. `IssueTick` is not merely unenforced —
it is **semantically undefined**: nothing in the codebase states whether it
is authored time, submission time, or acceptance time, and
`Simulation.commandIntake` never reads it. TASK-020 deferred this and named
B-044 as the mandatory owning task before G3.

This is a **decision task with a minimal implementation**, in the shape of
TASK-020: it settles the semantics in "Central decisions", then lands the
smallest code change that enforces them (field renames, one or two
`CommandRejection` cases, a replay-log uniqueness check). It does **not**
build order scheduling, a command queue, an `OrderStore`, or appraisal.

## Why this task exists

- `docs/08_ROADMAP_AND_GATES.md` section 6 P3 Required work names it directly:
  "resolve issue-tick / submission-tick semantics (B-044) ... both are
  foundations of the command loop deferred by TASK-020 and must be settled
  before this gate."
- Risk R-009 (determinism claimed but breaks through ordering / numeric
  behaviour): two tick fields with one undocumented relationship is a latent
  divergence source — the moment a caller sets them independently, replay
  behaviour depends on which one each code path reads.
- Risk R-008 (modules disagree because command meaning is duplicated): the
  `docs/04` section 13 envelope, `Replay.RecordedCommand`, and
  `CommandLogFile` each carry a tick with no shared definition.
- G3 evidence requires "scenario traces are readable enough to diagnose all
  decisions" and "repeated headless runs produce stable evidence". A command
  whose eligibility tick is undefined cannot be part of a readable, stable
  decision trace.

This depends only on TASK-020 (done). It does not depend on TASK-021,
TASK-022, or TASK-023. TASK-025 (B-045, the production replay-command
serialisation) depends on this task's decisions and must run after it.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `src/CommandoWar.Sim/Commands.fs` — `PlayerCommand` (the `IssueTick` field
  and its doc comment naming B-044), `Command.moveTo` / `Command.moveToMany`,
  `CommandRejection`
- `src/CommandoWar.Sim/Simulation.fs` — `commandIntake` (the whole phase and
  its header comment; the validation order), `step` (the batch sort by
  `c.Id`)
- `src/CommandoWar.Sim/Replay.fs` — `RecordedCommand` (the `Tick` field doc
  "the tick whose command-intake phase consumes this command"), `CommandLog`,
  `RecordedCommand.forTick`, `CommandLog.create` (the `(Tick, Sequence)`
  sort), `Replay.validate` (`NonMonotonicCommandLog`,
  `CommandOutsideReplayRange`), `Replay.run` (the per-tick
  `Array.filter (fun c -> c.Tick = tick)`), `ReplayError`
- `src/CommandoWar.Headless/CommandLogFile.fs` — `parse` (the single `tick`
  field passed as both `RecordedCommand.Tick` and `Command.moveTo`'s
  `issueTick` argument), `Version`
- `src/CommandoWar.Sim/Diagnostics.fs` — `eventMarker` (the
  `CommandRejection` arms; `TreatWarningsAsErrors` forces a new arm per case)
- `src/CommandoWar.Sim/Canonical.fs` — confirm `encode` takes `WorldState`
  only; `PlayerCommand` is not encoded, so `FormatVersion` does not move
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs` (the TASK-020 command
  validation section), `ReplayTests.fs`, `CorpusTests.fs`
- `docs/04_SIMULATION_SPEC.md` sections 2 ("A command becomes eligible on a
  specified tick"), 12.1, 13 (the envelope; the partial-realisation note),
  16 (replay record fields)
- `docs/05_COMMAND_AND_AGENT_AI.md` section 5 stage 4, section 14
  (reappraisal triggers — "a new order is received"), section 13 (developer
  trace: "order and appraisal version")
- `docs/09_TEST_STRATEGY.md` sections 2.1, 2.4
- `tasks/TASK-020-COMMAND-VALIDATION-AND-RECIPIENTS.md` (the issue-tick and
  `.cwlog` decisions and their deferral to B-044 / B-045),
  `docs/ledger/2026-09-06-TASK-020-revised.md` (why the deferral became
  mandatory)

## Dependencies

- TASK-020 (done)

## Central decisions

These are the decisions the task settles. Each carries a **recommended
resolution**; the implementing agent confirms with Dave before implementing
and records the final answer in the ledger. Where a decision is genuinely
Dave's product call it is flagged.

### Decision 1 — the two concepts, named and separated

There are two ticks and they are **independent**:

- **`SubmitAtTick`** (rename of `RecordedCommand.Tick`) — the tick whose
  command-intake phase consumes the command. It is a **scheduling fact owned
  by the caller** (the headless runner, the replay runner, later the client
  command queue), recorded for replay. `Replay.run` already keys on it
  (`Array.filter (fun c -> c.Tick = tick)`); `CommandLog` already sorts and
  range-checks on it.
- **`IssuedAtTick`** (rename of `PlayerCommand.IssueTick`) — the tick on
  which the order was issued by the commander. It is **envelope provenance
  and a future appraisal input** (`docs/05` section 5 stage 4, section 14:
  an order is aged from when it was issued), carried inside the authoritative
  command envelope and replayed verbatim.

**Recommended:** rename both. `IssuedAtTick` on `PlayerCommand`,
`SubmitAtTick` on `RecordedCommand`. The rename is the point — it stops the
two concepts sharing the ambiguous word "tick" / "issue tick".

The `RecordedCommand.Tick -> SubmitAtTick` rename has the wider blast radius
(`Replay.run`, `Replay.validate`, `RecordedCommand.forTick`,
`CommandLog.create`, `CommandLogFile.parse`, `ReplayTests`). The implementer
**may** keep `RecordedCommand.Tick` as-is and only rename
`PlayerCommand.IssueTick -> IssuedAtTick`, adding a doc comment on
`RecordedCommand.Tick` that pins it as the submission/delivery tick, **if**
the rename fan-out proves noisy enough to threaten the size. State the choice
in the ledger. Renaming only one of the two is acceptable; renaming neither
is not — the ambiguity must be removed in the types or their doc comments.

### Decision 2 — `IssuedAtTick` eligibility at command intake

`commandIntake` gains one whole-command check: a command whose
`IssuedAtTick` is **after the tick being processed** is rejected. A command
cannot have been issued in the future.

- Add `CommandRejection` case
  `IssuedInFuture of issuedAt: int64 * tick: int64` (the offending value and
  the current tick).
- Also reject `IssuedAtTick < 0` — either a second case
  `NegativeIssueTick of issuedAt: int64`, or fold both into one
  `InvalidIssueTick of issuedAt: int64 * tick: int64` carrying the running
  tick and letting the message distinguish "negative" from "in the future".
  Implementer's call; every bad value must produce exactly one actionable
  `CommandRejected`.
- Position in the validation order: a **whole-command** check (no single
  recipient is implicated), evaluated with `EmptyRecipients` /
  `DuplicateRecipient` / `TargetOutOfBounds`, after the batch-level
  duplicate-`CommandId`-group rejection. Recommended first among the
  whole-command checks (a future-dated envelope is malformed before its
  recipients matter). First failure short-circuits with one event.

### Decision 3 — stale commands are **not** rejected at intake

A command whose `IssuedAtTick` is many ticks before `SubmitAtTick` (issued
long ago, only now delivered) is **accepted**. `commandIntake` applies no
staleness horizon.

Rationale: the vertical slice has no communication-delay model
(`docs/04` section 12.2: "voice and radio delay may initially be zero"), so
there is no principled tick count that makes an order "too old". Whether an
outdated order should be followed is a **tactical judgment** — the agent may
decline it through appraisal (`docs/05` section 5, B-017), with a structured
reason — not a validation one. Adding a give-up-after-N-ticks constant here
would be an arbitrary tuning value with no evidence behind it.

Record this explicitly as a decision, not an omission. B-016 (communication
constraints and report aging) and B-017 (appraisal) are where staleness
becomes a modelled concept; note it for whoever drafts them.

### Decision 4 — replay tick and issue tick are independent, and the legacy `.cwlog` collapses them

`SubmitAtTick` and `IssuedAtTick` are not constrained to be equal. The
replay runner schedules on `SubmitAtTick`; `IssuedAtTick` rides inside the
envelope.

The legacy `.cwlog` grammar has one tick field per line and cannot express
two. `CommandLogFile.parse` keeps mapping that one field to **both**
`SubmitAtTick` and `IssuedAtTick` (the degenerate case: an order issued on
the same tick it is delivered). This is correct for a fixture script and
keeps every committed corpus hash unmoved. The format that carries the two
ticks separately is **TASK-025 / B-045**; note the dependency in this task's
completion report.

### Decision 5 — command identity / scheduling is **not** authoritative state

`CommandId` stays a **batch-local dedup key plus replay provenance**. It does
**not** become part of `WorldState`, and `commandIntake` does **not** hold a
cross-tick set of seen ids.

- Within-tick duplicate-`CommandId` rejection (TASK-020, `DuplicateCommandId`)
  is unchanged.
- Cross-tick uniqueness — a `CommandId` reused on a later tick — is enforced
  at the **replay-log** level, which is non-authoritative: add a
  `ReplayError` case (e.g. `DuplicateCommandIdInLog of id: CommandId * firstIndex: int * secondIndex: int`)
  and a check in `Replay.validate` (and, cheaply, in `CommandLog.create` or
  `CommandLogFile.parse` where ids are minted). A reused id in a replay log
  is a malformed log, rejected with a typed error, not a guessed repair.
- `WorldState` gains **no** command-history field. A per-tick-growing
  authoritative set would be unbounded state (`docs/04` section 19: "no
  unbounded allocation growth") for a guarantee the replay layer already
  gives, and `docs/04` section 10 says fields are added only when an
  implemented behaviour requires them. Reopen only if B-017 needs the
  simulation itself to detect "this exact order was already appraised" —
  which the staged-appraisal design (one appraisal per accepted order,
  tracked on the future `OrderStore`) does not obviously need.

Consequence: `Canonical.FormatVersion` does **not** move.

### Decision 6 — no `Canonical.FormatVersion` bump, no hash re-pin, no ADR

`Canonical.encode` takes `WorldState` and encodes `Agents` / `Random` /
`Tick` / `Bounds`. `PlayerCommand` and `RecordedCommand` are not in the
canonical image; renaming their fields and adding a rejection case touches no
encoded byte. `Canonical.FormatVersion` stays **2**. Every committed fixture,
corpus, and diagnostics hash is unmoved: the legacy `.cwlog` collapse
(Decision 4) keeps `IssuedAtTick == SubmitAtTick == tick` for every existing
entry, and `IssuedAtTick <= tick` always holds, so no existing command is
newly rejected. The implementer must run `-- corpus` / `-- fixture` before
and after and **stop and report** if any committed hash moves.

No ADR: this enforces an existing implicit contract (`docs/04` section 2, "a
command becomes eligible on a specified tick") and settles a deferral the
gate already requires. It does not change product scope, architecture,
framework, the determinism contract, or a gate. `docs/08` section 6 already
lists B-044; no gate-obligation edit is needed.

## Diagnostics

`AGENTS.md`'s diagnostic-extension rule does **not** apply: this task adds no
authoritative spatial or tactical state to `AgentState` / `WorldState` /
`DiagnosticFrame`. The new `CommandRejection` case(s) reuse the existing
`"command-rejected"` event kind in `Diagnostics.eventMarker`, which matches
`CommandRejection` exhaustively and therefore needs one new arm per case
(`Cells = [||]`, the `UnknownAgent` / `EmptyRecipients` treatment — no cell
is implicated by a future-dated or negative issue tick). No new golden. If a
test enumerates `CommandRejection` cases exhaustively, extend it there.

## Allowed scope

- `src/CommandoWar.Sim/Commands.fs` — rename `PlayerCommand.IssueTick ->
  IssuedAtTick` and its doc comment; the `issueTick` parameter of
  `Command.moveTo` / `Command.moveToMany`; new `CommandRejection` case(s) per
  Decision 2.
- `src/CommandoWar.Sim/Simulation.fs` — `commandIntake`: the `IssuedAtTick`
  eligibility check and the phase header comment (add a "Realised by
  TASK-024" paragraph stating the two-tick model, the future/negative
  rejection, and the no-staleness-horizon decision).
- `src/CommandoWar.Sim/Replay.fs` — the `RecordedCommand.Tick -> SubmitAtTick`
  rename (if taken, Decision 1) across `RecordedCommand`, `RecordedCommand.forTick`,
  `CommandLog.create`, `Replay.validate`, `Replay.run`; the
  `DuplicateCommandIdInLog` `ReplayError` case and its check (Decision 5).
- `src/CommandoWar.Sim/Diagnostics.fs` — `eventMarker` arm(s) for the new
  `CommandRejection` case(s).
- `src/CommandoWar.Headless/CommandLogFile.fs` — update the call site for the
  renamed parameter(s); a doc-comment line stating the one `.cwlog` tick
  field maps to both `SubmitAtTick` and `IssuedAtTick` (Decision 4).
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs` — new facts: a command
  with `IssuedAtTick` after the running tick rejected `IssuedInFuture`; a
  command issued on the running tick accepted; a command issued several ticks
  earlier and delivered now accepted (no staleness rejection); a negative
  `IssuedAtTick` rejected.
- `tests/CommandoWar.Sim.Tests/ReplayTests.fs` — new facts: a command log
  with a `CommandId` reused across two ticks rejected `DuplicateCommandIdInLog`;
  the renamed field(s) compile and the existing replay facts pass unmodified.
- `docs/04_SIMULATION_SPEC.md` (section 2 command eligibility; section 12.1
  realisation note; section 13 envelope note; section 16 replay-record note),
  `docs/09_TEST_STRATEGY.md` (section 2.1 command-validation realisation
  note).
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml`
  if it becomes the active task).

## Forbidden scope

- Order scheduling, a command queue, deferred-execution / "issue this N ticks
  from now" behaviour. The caller owns `SubmitAtTick`; the simulation only
  ever sees the current tick's batch.
- An `OrderStore`, `Squads`, appraisal, commitment, or any B-016 / B-017 /
  B-018 behaviour. `IssuedAtTick` is carried and range-checked, not consumed
  by any agent.
- A staleness horizon / give-up-after-N-ticks constant at command intake
  (Decision 3).
- A cross-tick duplicate-`CommandId` set in `WorldState` or `commandIntake`;
  a `Canonical.FormatVersion` bump; any `Canonical.encode` change (Decision 5,
  6).
- An issuer-identity or commander-authority model (still B-017+ territory;
  TASK-020's `UnauthorisedRecipient` friendly/hostile check is the whole of
  "authorisation" for now).
- The production replay-command serialisation / a `.cwlog` grammar change /
  `CommandLogFile.Version` bump — that is **TASK-025 / B-045**.
- Re-pinning any fixture / corpus / golden hash. A moved hash is a
  stop-and-report finding.
- Editing the client spikes, `src/_scratch`, `bench/`, or
  `content/diagnostics/`.

## Acceptance criteria

- [ ] `PlayerCommand.IssueTick` is renamed to `IssuedAtTick` with a doc
      comment stating it is issue-time provenance and a future appraisal
      input, distinct from the submission/delivery tick; `Command.moveTo` and
      `Command.moveToMany` keep their argument order with the renamed
      parameter (`Commands.fs`).
- [ ] Either `RecordedCommand.Tick` is renamed to `SubmitAtTick`, or it
      carries a new doc comment pinning it as the submission/delivery tick
      and stating its independence from `IssuedAtTick`; the choice and its
      reason are in the ledger (`Replay.fs`).
- [ ] `commandIntake` rejects a command whose `IssuedAtTick` is greater than
      the tick being processed with one `CommandRejected` (`IssuedInFuture`
      or equivalent), and rejects `IssuedAtTick < 0`; a command issued on the
      running tick and a command issued several ticks earlier both accept
      (`SimulationTests.fs`).
- [ ] `Replay.validate` (and/or `CommandLog.create` / `CommandLogFile.parse`)
      rejects a command log in which one `CommandId` appears on two different
      ticks, with a typed `ReplayError`; `WorldState` gains no command-history
      field (`ReplayTests.fs`; code review of `Domain.fs` / `Simulation.fs`).
- [ ] `CommandLogFile.parse` maps the single `.cwlog` tick field to both
      `SubmitAtTick` and `IssuedAtTick`; the `.cwlog` grammar and
      `CommandLogFile.Version` are unchanged (`CommandLogFile.fs`).
- [ ] `Canonical.FormatVersion` unchanged (`2`); `-- corpus` and
      `-- fixture` reproduce every committed hash with no `--regenerate`;
      every pre-existing `SimulationTests` / `ReplayTests` / `CorpusTests` /
      `FixtureTests` / `DeterminismPropertyTests` fact passes unmodified.
- [ ] `dotnet build CommandoWar.slnx -c Release` = 0 warnings, 0 errors;
      `dotnet list src/CommandoWar.Sim package --include-transitive` =
      `FSharp.Core` only; source scan of `src/CommandoWar.Sim` clean.
- [ ] `docs/04` sections 2 / 12.1 / 13 / 16, `docs/09` section 2.1, backlog
      row, ledger index row + detail file, `PROJECT_STATE.yaml`, task status
      updated. "Green tests" pinned fact refreshed with the new count.
- [ ] The completion report names TASK-025 / B-045 as the follow-up that
      carries the two ticks in a real on-disk format and states that B-045
      can move to `ready` now that this task is done.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`
- `dotnet test CommandoWar.slnx -c Release` before and after (state the new
  count and what each added fact checks)
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus` and
  `-- fixture` before and after — byte-identical committed hashes, no
  `--regenerate`
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
- source scan of `src/CommandoWar.Sim`
- `git status --porcelain` — matches the "Allowed scope" list exactly

## Evidence to capture

- test summary (before/after counts), the new `SimulationTests` /
  `ReplayTests` facts by name;
- the `-- corpus` / `-- fixture` hash lines before and after (proving no
  committed hash moved);
- the final wording of Decisions 1–5 as implemented, and, for any decision
  where Dave chose differently from the recommendation, what changed and why;
- the rename fan-out (files touched by `RecordedCommand.Tick -> SubmitAtTick`,
  or the reason it was kept).

## Rollback or removal

The field renames are mechanical and reversible. The new `CommandRejection`
and `ReplayError` cases and the `commandIntake` / `Replay.validate` branches
are additive — reverting is deleting them and reversing the renames. No data
migration: no `Canonical.FormatVersion` move, no committed hash re-pin, and
the legacy `.cwlog` files are untouched, so nothing needs re-authoring on
apply or revert.

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md` (TASK-024 row; B-044 `proposed -> ready` at draft,
  `-> done` on completion; note that B-045 becomes `ready` once this lands);
- `docs/12_PROGRESS_LEDGER.md` index row + `docs/ledger/` detail file;
  refresh the "Green tests" pinned fact;
- `docs/04_SIMULATION_SPEC.md` sections 2, 12.1, 13, 16;
- `docs/09_TEST_STRATEGY.md` section 2.1;
- `PROJECT_STATE.yaml` only if this becomes the active task;
- no ADR (enforces an existing implicit contract and settles a gate-required
  deferral; decides no new architecture, framework, determinism contract, or
  gate).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
