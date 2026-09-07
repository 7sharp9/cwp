## 2026-09-07 - TASK-024 - Issue-tick / submission-tick semantics

**Owner:** Dave with coding-agent assistance
**Source revision:** `d4c06b5` (Reconcile 021/022/023 acceptances; draft
TASK-024/025/026) on branch `task-024-025-026-drafts`; TASK-024 implementation
on branch `task-024-issue-tick-semantics` off that commit
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; .NET 10.0.11;
xUnit 2.9.3; FsCheck / FsCheck.Xunit 3.3.4
**Status change:** `tasks/TASK-024-ISSUE-TICK-SEMANTICS.md` `ready -> review`
(implemented 2026-09-07, pending Dave's acceptance); `docs/11_BACKLOG.md`
TASK-024 row + B-044 `ready -> review`; `PROJECT_STATE.yaml`
`active_work.selected_task` `none -> TASK-024`. No `Canonical.FormatVersion`
change (stays `2`); no committed hash moved.

### Precondition check

`PROJECT_STATE.yaml` `active_work.selected_task` was `none` (021/022/023
accepted, 024/025/026 drafted). Dave: "proceed with recommendation" — the
report's recommendation was to select and implement TASK-024 first, then
TASK-025, then TASK-026. TASK-024's only dependency, B-014 / TASK-020, is
`done` and accepted. Baseline before any edit: `dotnet build` `0/0`;
`dotnet test` `Passed: 194`; `cwheadless corpus` 7x PASS; `cwheadless
fixture` `0xAFA35198CC6BD8D4`, 33 events, format 2.

### Diagnosis and chosen approach

`PlayerCommand.IssueTick` was semantically undefined (its own doc comment
said so) and unread by `commandIntake`. `RecordedCommand.Tick` was already
well-defined ("the tick whose command-intake phase consumes this command")
but `CommandLogFile.parse` forced the two equal by passing one `.cwlog`
field to both. The task settles the semantics and lands the minimal
implementation, in the TASK-020 shape.

**Decision 1 — rename `PlayerCommand.IssueTick` only, keep
`RecordedCommand.Tick`.** The task file offered the fallback; taken. The
ambiguity was only ever in the undefined `IssueTick` name plus the
`CommandLogFile` collapse — `RecordedCommand.Tick` is already unambiguous and
sits in replay-log plumbing that TASK-025 (B-045) reworks. Renaming it too
would be ~15 compiler-checked sites of pure churn (`Replay.fs` x6,
`CommandLogFile.fs` x2, `DiagnosticRender.fs`, `Fixture.fs`,
`DemoScenario.fs`, `ReplayTests.fs`, `DeterminismPropertyTests.fs`,
`FixtureTests.fs`) for no clarity gain. Instead `RecordedCommand.Tick` got an
expanded doc comment pinning it as the delivery / submission tick and stating
its independence from `IssuedAtTick`.

**Decision 2 — one rejection case, `IssueTickOutOfRange of issuedAtTick:
int64 * tick: int64`.** Valid range `[0, currentTick]`. Named after
`ScenarioError.MoveCostOutOfRange` (TASK-021). One whole-command check in
`commandIntake`, as an `elif` right after the batch-level
duplicate-`CommandId`-group rejection and before `match cmd.Intent` — first
among the whole-command checks, since it is `Intent`-independent. First
failure short-circuits with one `CommandRejected` and no per-recipient
events.

**Decision 3 — no staleness horizon.** A command issued on an earlier tick
and delivered now is accepted. Recorded in the `commandIntake` comment,
`Commands.fs` doc, `docs/04` section 2/12.1, and flagged for B-016 / B-017.

**Decision 4 — the two ticks are independent.** `CommandLogFile.parse` still
maps its one tick field to both (`RecordedCommand.Tick = tick` and, via the
positional `Command.moveTo ... tick ...` call, `IssuedAtTick = tick`) — the
degenerate "issued on the tick it is delivered" case. Documented in the
`CommandLogFile` module comment.

**Decision 5 — command identity is not authoritative state.** New
`ReplayError.DuplicateCommandIdInLog of id: CommandId * firstIndex: int *
secondIndex: int`, checked in `Replay.validate` after the monotonic and
range checks via `Array.groupBy (fun c -> c.Command.Id)` (deterministic:
`groupBy` keeps keys in first-appearance order and members in original
order). `WorldState` / `AgentState` gain no field; `commandIntake` holds no
cross-tick set. `Canonical.FormatVersion` stays `2`.

**Decision 6 — no bump, no hash re-pin, no ADR.** Confirmed against source
and by running the verbs.

### Changes

- **`src/CommandoWar.Sim/Commands.fs`.** `PlayerCommand.IssueTick ->
  IssuedAtTick` (field + type doc comment rewritten to state the two-tick
  model, the `[0, currentTick]` rule, and the no-staleness decision; field
  doc added). `Command.moveTo` / `Command.moveToMany` parameter
  `issueTick -> issuedAtTick` (positional callers unaffected). New
  `CommandRejection.IssueTickOutOfRange of issuedAtTick: int64 * tick: int64`
  with a doc comment. `DuplicateCommandId` doc comment updated to point at
  the new replay-log invariant.
- **`src/CommandoWar.Sim/Simulation.fs`.** `commandIntake`: new
  `elif cmd.IssuedAtTick < 0L || cmd.IssuedAtTick > s.Tick then emit
  (CommandRejected(cmd.Id, IssueTickOutOfRange(cmd.IssuedAtTick, s.Tick)))`
  between the duplicate-id-group check and `match cmd.Intent`. Phase header
  comment extended: validation order now lists step 2a (issue-tick
  eligibility) and 2b (the recipient checks); a closing paragraph states the
  delivery-tick / `IssuedAtTick` independence and the `.cwlog` collapse.
- **`src/CommandoWar.Sim/Replay.fs`.** `RecordedCommand.Tick` doc comment
  expanded. New `ReplayError.DuplicateCommandIdInLog` case + doc comment.
  `Replay.validate` restructured: `monotonicError`, `rangeError`,
  `duplicateIdError` computed, then
  `monotonicError |> Option.orElse rangeError |> Option.orElse duplicateIdError`.
- **`src/CommandoWar.Sim/Diagnostics.fs`.** `eventMarker` gains
  `| CommandRejected(_, IssueTickOutOfRange _) -> { Kind = "command-rejected";
  Cells = [||] }` (the `EmptyRecipients` treatment).
- **`src/CommandoWar.Headless/Program.fs`.** `describeReplayError` gains a
  `DuplicateCommandIdInLog` arm (forced by the exhaustive match under
  `TreatWarningsAsErrors`).
- **`src/CommandoWar.Headless/CommandLogFile.fs`.** Module doc comment: it is
  a legacy fixture-script format (points to B-045); its one tick field maps
  to both `RecordedCommand.Tick` and `IssuedAtTick`. No grammar / `Version`
  change; the `Command.moveTo` call is positional and unchanged.
- **`tests/CommandoWar.Sim.Tests/CorpusTests.fs`.** One token:
  `c.Command.IssueTick -> c.Command.IssuedAtTick` (line 76; a field access,
  not positional). No behavioural change to the fact.
- **`tests/CommandoWar.Sim.Tests/SimulationTests.fs`.** New "Issue-tick
  eligibility (TASK-024)" section: `cmdIssuedAt` helper and four facts —
  future-issue rejected `IssueTickOutOfRange(2,1)`; issued-on-the-running-tick
  accepted; issued two ticks earlier and delivered now accepted with no
  `IssueTickOutOfRange`; negative `IssuedAtTick` rejected
  `IssueTickOutOfRange(-1,1)`.
- **`tests/CommandoWar.Sim.Tests/ReplayTests.fs`.** New fact: a `CommandLog`
  with `CommandId 42` on tick 1 and tick 3 is rejected
  `DuplicateCommandIdInLog(42, 0, 1)` by `Replay.run` (via `validate`).
- **`docs/04_SIMULATION_SPEC.md`.** Section 2 "Realised by TASK-024" block
  (the two-tick model, the eligibility rule, no-staleness, identity not
  authoritative); section 12.1 realisation note extended; section 13 envelope
  note (`IssueTick -> IssuedAtTick`, B-044 done, B-045 still mandatory);
  section 16 replay-record note (`DuplicateCommandIdInLog`).
- **`docs/09_TEST_STRATEGY.md`.** Section 2.1: issue-tick eligibility and the
  cross-tick `DuplicateCommandIdInLog` guard are now covered.
- **Control.** `tasks/TASK-024-ISSUE-TICK-SEMANTICS.md` (`## Outcome`,
  `Status`, acceptance boxes ticked); `tasks/TASK-025-*.md` (`Status` note —
  B-044 implemented); `docs/11_BACKLOG.md` (TASK-024 row + B-044 + B-045
  rows); `PROJECT_STATE.yaml` (`active_work`); `docs/12_PROGRESS_LEDGER.md`
  (this index row; "Green tests" `194 -> 199`); this entry. No ADR.

### Verification

All from the repository root, on branch `task-024-issue-tick-semantics`.

- `dotnet build CommandoWar.slnx -c Release` -> `Build succeeded. 0
  Warning(s) 0 Error(s)` (all four projects).
- `dotnet test CommandoWar.slnx -c Release --no-build` before any edit ->
  `Passed: 194`. After -> `Passed: 199, Failed: 0` (+4 `SimulationTests`,
  +1 `ReplayTests`; every pre-existing fact green, only the one-token
  `CorpusTests` rename touched).
- `dotnet run --project src/CommandoWar.Headless -c Release --no-build --
  corpus` -> `OK - all 7 entries match their committed tables`, exit `0`,
  before and after (no `--regenerate`).
- `dotnet run --project src/CommandoWar.Headless -c Release --no-build --
  fixture` -> `final hash 0xAFA35198CC6BD8D4 (format 2)`, `events 33`,
  `canonical format 2`, before and after — byte-identical.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive` -> `FSharp.Core 10.1.303` only.
- Source scan of `git diff src/CommandoWar.Sim` for
  `float|Stopwatch|DateTime|System.Random|godot|Dictionary|async` -> clean;
  the additions are one DU case, one `int64` comparison, `Array.groupBy` /
  `Option.orElse` (FSharp.Core), and comments.
- `git status --porcelain` -> nine files: `src/CommandoWar.Sim/{Commands,
  Simulation,Replay,Diagnostics}.fs`,
  `src/CommandoWar.Headless/{Program,CommandLogFile}.fs`,
  `tests/CommandoWar.Sim.Tests/{SimulationTests,ReplayTests,CorpusTests}.fs`,
  plus the docs/control files. No `.fsproj`, no `content/`, no `.github/`,
  nothing under the client spikes / `bench/` / `src/_scratch`.

### Evidence

- **No existing command is newly rejected:** every `.cwlog` / fixture /
  generated command has `IssuedAtTick == RecordedCommand.Tick == deliveryTick`
  (or, in `ReplayTests`, `deliveryTick - 1`), so `IssuedAtTick <= s.Tick` and
  `>= 0` always held; `-- corpus` / `-- fixture` and the 194 pre-existing
  tests confirm byte-identical output.
- **`Canonical.FormatVersion` unmoved:** `encode` takes `WorldState`;
  `PlayerCommand` / `RecordedCommand` are not encoded; `-- fixture` prints
  `format 2`.
- Acceptance criteria: every box in `tasks/TASK-024-*.md` is checked with the
  test or command that supports it.

### Deviations and unresolved issues

- **Decision 1: only one field renamed.** `RecordedCommand.Tick` kept. The
  task file explicitly permitted this with a stated fallback; the reasoning
  (churn vs. clarity, TASK-025 reworks that plumbing) is above. If Dave wants
  the symmetric `SubmitAtTick` rename it is a mechanical follow-up.
- **`CorpusTests.fs` one-token edit** (`.IssueTick -> .IssuedAtTick`) is
  beyond the task's literal Allowed-scope list but is the mechanical
  consequence of a field rename — the same category as TASK-020 keeping a
  `.Agent` accessor to avoid it, except here the honest one-token change was
  cheaper than another back-compat wart. No fact semantics changed.
- **`Program.fs` `describeReplayError` arm** is likewise a forced consequence
  of an additive `ReplayError` case under `TreatWarningsAsErrors`; added with
  the intended message, not a wildcard.
- **`Fixture.CommandIssueTick`** (a headless-fixture constant name, not the
  renamed field) was left as-is: it means "the tick the fixture's command is
  issued/delivered on", still accurate since the fixture keeps them equal.
  Renaming it would touch `Fixture.fs`, `Program.fs`, `ScenarioTests.fs` for
  a constant that is not ambiguous in context.
- **Staleness** is deliberately not modelled at intake (Decision 3). Whoever
  drafts B-016 / B-017 should confirm the stalled/outdated-order case is
  handled by appraisal with a structured reason, not silently.
- **B-045 / TASK-025** now has all its inputs: the envelope carries an
  `IssuedAtTick` distinct from the delivery tick that `.cwlog` cannot
  express. B-045 moves to `ready` on Dave's acceptance of this task.

### Documents updated

- `tasks/TASK-024-ISSUE-TICK-SEMANTICS.md`, `tasks/TASK-025-REPLAY-COMMAND-SERIALISATION.md`
- `src/CommandoWar.Sim/{Commands,Simulation,Replay,Diagnostics}.fs`
- `src/CommandoWar.Headless/{Program,CommandLogFile}.fs`
- `tests/CommandoWar.Sim.Tests/{SimulationTests,ReplayTests,CorpusTests}.fs`
- `docs/04_SIMULATION_SPEC.md` (sections 2, 12.1, 13, 16),
  `docs/09_TEST_STRATEGY.md` (section 2.1)
- `docs/11_BACKLOG.md` (TASK-024, B-044, B-045 rows)
- `docs/12_PROGRESS_LEDGER.md` (this index row; "Green tests" `194 -> 199`)
- `PROJECT_STATE.yaml` (`active_work`; both `updated` lines)
- this entry
- no ADR (settles a gate-required TASK-020 deferral and enforces `docs/04`
  section 2's existing "a command becomes eligible on a specified tick";
  decides no new architecture, framework, determinism contract, or gate —
  `docs/08` section 6 already lists B-044)

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: implemented on the recommended resolutions per "proceed with
  recommendation". Decision 1 (one field renamed, not both) is the call most
  worth confirming.
