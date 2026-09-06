## 2026-09-06 - TASK-020 - Command validation and recipient selection (partial section 13 envelope)

**Owner:** Dave with coding-agent assistance
**Source revision:** `bb02990` (Revise TASK-020 and draft TASK-021), plus this
session's uncommitted control-plane state
**Environment:** Windows 11 Pro 26200 (25H2); .NET SDK 10.0.303; .NET 10.0.11 X64
**Status change:** `tasks/TASK-020-COMMAND-VALIDATION-AND-RECIPIENTS.md`
`ready -> review -> done` (accepted by Dave 2026-09-06 in the implementation
session); `docs/11_BACKLOG.md` TASK-020 row and B-014 `ready -> done`;
`PROJECT_STATE.yaml active_work.selected_task` `TASK-021 -> TASK-020`. TASK-021's
own `done`/acceptance state is untouched (Dave's to set).

### Diagnosis and chosen approach

`src/CommandoWar.Sim/Commands.fs` already named the gap: "Recipients beyond a
single agent, urgency and risk tolerance are deferred to the command-validation
task (backlog B-014)." The task file (drafted 2026-09-05, revised 2026-09-06)
fixes the design: generalise addressing to `Recipients: AgentId list` while
keeping `Command.moveTo`'s four-argument signature and every pinned hash, carry
`Urgency` / `RiskTolerance` as inert envelope data, and land the current-scope
`docs/04` section 12.1 intake-validation rules with no invented authority model.

Blast radius was genuinely small: only `Simulation.commandIntake` reads
`PlayerCommand.Agent`, and only `Commands.fs` constructs the record literally.
`CorpusTests.fs` (lines 41, 76) reads `.Command.Agent` and must pass unmodified,
so a back-compatible `member this.Agent = List.head this.Recipients` read
accessor is kept on `PlayerCommand` — this is the only addition beyond the
task's literal Allowed-scope list and is what reconciles "`Recipients` replaces
the single `Agent` field" with "every pre-existing `CorpusTests` fact passes
unmodified".

### Changes

- `src/CommandoWar.Sim/Commands.fs`:
  - `type Urgency = Routine | Immediate`, `type RiskTolerance = Cautious |
    Standard | Aggressive` — inert, documented as B-017 appraisal inputs that
    nothing reads yet.
  - `PlayerCommand`: `Agent: AgentId` field replaced by `Recipients: AgentId
    list`; `Urgency` and `RiskTolerance` fields added; `member this.Agent =
    List.head this.Recipients` back-compat read accessor.
  - `CommandRejection`: `EmptyRecipients`, `DuplicateRecipient of agent`,
    `UnauthorisedRecipient of agent`, `DuplicateCommandId of command` added;
    `UnknownAgent` / `TargetOutOfBounds` unchanged.
  - `Command.moveTo` unchanged signature, now builds `Recipients = [agent]`,
    `Urgency = Routine`, `RiskTolerance = Standard`.
  - `Command.moveToMany (id) (issueTick) (agents) (target) (urgency)
    (riskTolerance)` added.
- `src/CommandoWar.Sim/Simulation.fs` `commandIntake`:
  - private `firstDuplicate : AgentId list -> AgentId option` helper.
  - one `Array.copy s.Agents` + one `AgentId -> int` `Map` built once, above
    the command loop (was `Array.copy` per accepted command).
  - batch-level: `duplicatedIds` set; each command whose id is in it emits one
    `CommandRejected(id, DuplicateCommandId id)` and is not processed.
  - per surviving command: `EmptyRecipients` -> `DuplicateRecipient` ->
    `TargetOutOfBounds` (first failure short-circuits, one event, no
    per-recipient events).
  - per recipient, `recipients |> List.sortBy AgentId.value`: `UnknownAgent`
    (not in the map) -> `UnauthorisedRecipient` (`Side = Hostile`) ->
    `CommandAccepted` emitted, then `Destination = Some target` written.
  - phase comment block rewritten to state the validation order and the
    once-per-invocation copy.
- `src/CommandoWar.Sim/Diagnostics.fs` `eventMarker`: one arm per new
  `CommandRejection` case, all `{ Kind = "command-rejected"; Cells = [||] }`
  (the `UnknownAgent` treatment — no cell is meaningfully implicated).
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`: `mixedWorld` helper (agents
  0/1 Friendly, agent 2 Hostile), `moveMany` / `acceptedAgents` helpers, and 6
  new facts (multi-recipient accept; ascending-id ordering regardless of
  authoring order; hostile recipient rejected with friendly co-recipient
  accepted; empty recipients; repeated recipient; duplicate command id
  reject-all + batch-order independence via equal event lists and equal state
  hash).
- `docs/04_SIMULATION_SPEC.md`: section 12.1 and section 13 realisation notes
  (partial envelope; issue-tick and issuer out; B-044 / B-045).
- `docs/09_TEST_STRATEGY.md`: section 2.1 "command validation" realisation note.
- Control docs: this entry + index row; `PROJECT_STATE.yaml`;
  `tasks/TASK-020-*.md` (Outcome section, Status, acceptance boxes);
  `docs/11_BACKLOG.md` (TASK-020 + B-014 rows).
- `DiagnosticsTests.fs` **not** touched — it matches event kinds by string, not
  `CommandRejection` exhaustively.
- `Events.fs` **not** touched — `CommandAccepted` / `CommandRejected` are
  already per-agent shapes; only the number of events per command changed.
- `content/benchmarks/BASELINE.md` **not** re-pinned (out of scope; the
  50-agent benchmark row was not re-run).

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result before: `Passed: 178`. Result after: `Passed: 184` (+6 new
    `SimulationTests` facts; every pre-existing fact green, unmodified).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
  - Result: `OK - all 5 entries match their committed tables` (no
    `--regenerate`), before and after.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  - Result: `final hash 0xAFA35198CC6BD8D4 (format 2)`, `random draws 0`,
    `events 33` — byte-identical to baseline.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
  - Result: `FSharp.Core 10.1.303` only.
- Manual: source scan of `src/CommandoWar.Sim` — the diff adds only
  `Map` / `Set` / `List` (FSharp.Core) operations; no `System.*` platform API,
  no wall-clock, no `System.Random`, no framework reference.
- Manual: `git status --porcelain` — no file deleted; changes confined to the
  Allowed-scope set plus this ledger file.

### Evidence

- Acceptance criteria: every box in `tasks/TASK-020-*.md` is checked with the
  test or command that supports it.
- `commandIntake` single-copy: `src/CommandoWar.Sim/Simulation.fs`, the `let
  agents = Array.copy s.Agents` line is now outside (above) the `for cmd in
  commands` loop; `s.Agents <- agents` runs once after it.
- No behaviour change for existing single-recipient commands: `-- corpus` and
  `-- fixture` reproduce every committed hash with no `--regenerate`, and the
  178 pre-existing tests pass unmodified.

### Deviations and unresolved issues

- **Back-compat `PlayerCommand.Agent` accessor.** Not in the task's literal
  Allowed-scope list for `Commands.fs`, but required to satisfy "every
  pre-existing `CorpusTests.fs` fact passes unmodified" once the `Agent` field
  is replaced. It is a one-line read-only `member`, no new type machinery.
  Flagged for Dave's review.
- **Backlog status `review`, not `done`.** Per `docs/11_BACKLOG.md` section 1
  (`review` = "implementation complete but evidence not yet accepted by Dave").
  TASK-021's row was set to `done (pending acceptance)` by its own session;
  this task uses the defined `review` status instead and leaves `done` for
  Dave's acceptance.
- **Partial envelope.** Issuer identity is not modelled and `IssueTick` is
  carried-but-unenforced with undefined semantics. B-044 (issue-tick
  semantics) and B-045 (production replay-command serialisation) are the
  mandatory-before-G3 follow-ups, already in `docs/11_BACKLOG.md` section 3 and
  `docs/08` section 6 from the 2026-09-06 task revision.
- **`UnauthorisedRecipient` is per-recipient**, not whole-command (a hostile
  co-recipient is rejected individually; friendly co-recipients accept). This
  is the task's stated design; the alternative (hostile recipient makes the
  whole envelope malformed) is defensible but was not chosen.
- Task sizing stayed `S`: the change is contained to one phase function plus
  two additive types; no wide call-site churn.

### Documents updated

- `tasks/TASK-020-COMMAND-VALIDATION-AND-RECIPIENTS.md` (Outcome, Status,
  acceptance boxes)
- `docs/11_BACKLOG.md` (TASK-020 row and B-014 row `ready -> review`)
- `docs/04_SIMULATION_SPEC.md` (sections 12.1, 13)
- `docs/09_TEST_STRATEGY.md` (section 2.1)
- `PROJECT_STATE.yaml` (`active_work` selected task + note; `project.updated`)
- `docs/12_PROGRESS_LEDGER.md` (index row)
- this entry

No "Pinned facts" value changed except the green-test count, refreshed to
`184` in the index.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-06)
- Notes: partial section 13 envelope. Do not describe this as "section 13
  landed in full". Issue-tick semantics (B-044) and the replay-command
  serialisation (B-045) remain mandatory before G3. Accepted with the
  `PlayerCommand.Agent` accessor as a known wart — a later task should migrate
  `CorpusTests` to `Recipients` and delete it.
