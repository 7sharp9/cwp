## 2026-09-16 - TASK-036 - Shared F# fixture builder for the test corpus

**Owner:** Dave with coding-agent assistance
**Source revision:** `8f6cd33` (Draft TASK-036: Shared F# fixture builder
for the test corpus (B-049))
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; xUnit 2.9.3
**Status change:** `tasks/TASK-036-SHARED-FIXTURE-BUILDER.md` `ready ->
review`; `docs/11_BACKLOG.md` B-049 `ready -> review`. No
`current_phase`/`current_gate`/`selected_task` structural change in
`PROJECT_STATE.yaml` (still names `TASK-036`, unchanged from the draft).

### Changes

- `src/CommandoWar.Headless/Corpus.fs`:
  - New private `ScenarioAgent` (id, cell, discipline, comms-available),
    `ScenarioOrder` (tick, agent, target), and `ScenarioSpec` (grid,
    friendly/enemy deployments, terrain, objective/extraction, an
    `Orders: ScenarioOrder list` command schedule) types, plus
    `agent`/`agentWith`/`blackedOut`/`order` constructor helpers.
  - `worldOfSpec` (the old `worldOf (rawScenario ...)` path, generalised to
    read a `ScenarioSpec`) and `commandsOfSpec` (new): the latter derives
    `RecordedCommand[]` from `spec.Orders`, assigning `CommandId` from
    order of appearance (1-based) and per-tick `Sequence` from order of
    appearance within a tick — deliberately mirroring
    `CommandLogFile.parse`'s numbering exactly, since `CommandId` is part
    of `Canonical.encode` (`Canonical.fs:219`) and a numbering mismatch
    would have silently re-pinned every migrated entry's hashes.
  - All eleven `…World ()` functions (`wallDetourWorld` through
    `openEngagementWorld`) replaced by `…Spec` values built from the new
    types; `rawScenario`/the old `worldOf` deleted.
  - `Entry` gains `Commands: RecordedCommand[] option` — `Some
    (commandsOfSpec spec)` for every migrated entry, `None` for
    `spike-fixture` (whose commands still come from
    `content/replays/spike-fixture.cwlog` via `loadLog`, unchanged).
  - New `commandsOf (dir: string) (e: Entry) : Result<RecordedCommand[],
    string>`: returns `e.Commands` directly when `Some`, falls back to
    `loadLog dir e` otherwise. `run`/`checkEntry`/`regenerateEntry` now
    call `commandsOf` instead of `loadLog` directly.
  - `regenerateEntry` additionally writes `content/replays/<name>.cwreplay`
    (via a new `replayFileOf` building a `ReplaySerialisation.
    ReplayCommandFile` with `InitialHash = None`, `Checkpoints = [||]`)
    for every entry with `Commands = Some _`.
  - `renderTable`'s "Command log" row now prints `.cwreplay` for a
    builder-authored entry and `.cwlog` for `spike-fixture` (previously
    hardcoded to `.cwlog` for every entry — this is the one line that
    changes in every migrated entry's committed `.md`).
- `src/CommandoWar.Headless/AppraisalDemo.fs`: `loadExposedApproachFrames`'s
  `Corpus.loadLog contentDir entry` -> `Corpus.commandsOf contentDir
  entry` (deviation from the drafted "Allowed scope" — this Godot-demo
  helper loads `exposed-approach`, now migrated, and would otherwise fail
  on the deleted `.cwlog`).
- `tests/CommandoWar.Sim.Tests/CorpusTests.fs`: the perturbation-test's
  `Corpus.loadLog corpusDir entry` (for `wall-detour`) -> `Corpus.
  commandsOf corpusDir entry` (same reason).
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`: nine call sites (one
  per migrated entry with a dedicated `…Frames ()` golden-render helper:
  `converging-routes`, `slow-terrain`, `swap-standoff`,
  `perception-contact`, `lost-comms`, `exposed-approach`, `blocked-goal`,
  `reissued-order`, `open-engagement`) — same `loadLog` -> `commandsOf`
  swap. No assertion or golden changed.
- `content/replays/*.cwlog` -> `content/replays/*.cwreplay` for the eleven
  migrated entries; `spike-fixture.cwlog` untouched.
- `content/replays/*.md`: every migrated entry's "Command log" row
  corrected to name the new `.cwreplay` file (one line each; every hash,
  tick count, and event count byte-identical).
- `content/replays/CORPUS.md`: "Entries" section rewritten to describe the
  two command-file paths (`.cwreplay` builder-authored, `.cwlog`
  `spike-fixture`-only); the `envelope-full` / regeneration / format-notes
  sections corrected where they described the pre-migration state; a new
  "Migrated by TASK-036" note alongside the existing per-task re-pin notes.
- `docs/04_SIMULATION_SPEC.md`, `docs/09_TEST_STRATEGY.md`: targeted
  corrections to two claims this task's migration made stale ("the legacy
  `.cwlog` stays the frozen format for the seven existing corpus entries"
  and "a replay corpus over the existing `.cwlog` v1 format") — both now
  note the TASK-036 migration. No other prose in either file touched
  (both documents have pre-existing staleness unrelated to this task,
  e.g. `docs/09`'s corpus-entry list not mentioning TASK-027/028/030/031/034's
  entries — out of scope here, not caused by this task).

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed: 287, Failed: 0, Total: 287` — unchanged from before
    this task; no new fact added (the loading-API swap reused every
    existing assertion).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus`
  - Result: `OK - all 12 entries match their committed tables`.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus --regenerate`, run twice
  - Result: identical `wrote ...` output both times; `git status` showed
    no further change after the second run (idempotent).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  fixture`
  - Result: final hash `0x56395A49904D017D` (format 7), 36 events —
    unchanged (spike-fixture untouched, as expected).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  replay-file content/replays/envelope-full.cwreplay`
  - Result: `checkpoints : OK (24 ticks match the file's committed
    hashes)`, 78 events — unchanged (`envelope-full` is not in
    `Corpus.all`, confirmed unaffected).
- Manual check: `git diff --stat content/replays/*.md`
  - Result: eleven files, `1 insertion(+), 1 deletion(-)` each — exactly
    the "Command log" row (confirmed by reading the `wall-detour.md` diff
    directly: `| Command log | `wall-detour.cwlog` |` -> `` `wall-detour.
    cwreplay` ``, every other line unchanged).
- Manual check: read `content/replays/exposed-approach.cwreplay` and
  `content/replays/open-engagement.cwreplay`
  - Result: `exposed-approach` carries `command 1 0 1 1 routine standard
    corpus 0 move 11 3` / `command 1 1 2 1 routine standard corpus 1 move
    11 5` — `CommandId` 1 then 2, matching the deleted `.cwlog`'s two-line
    order exactly (agent 0's order first, agent 1's second); `open-
    engagement` carries zero `command` lines, matching its empty `.cwlog`.
- Manual check: `git status --porcelain content/replays/`
  - Result: eleven `.cwlog` deletions, eleven `.cwreplay` additions
    (untracked, pending `git add`), `spike-fixture.{cwlog,md}` and
    `envelope-full.{cwreplay,md}` untouched.

### Evidence

See "Evidence to capture" in `tasks/TASK-036-SHARED-FIXTURE-BUILDER.md` for
the full command list and outputs.

### Deviations and unresolved issues

- Two files outside the drafted "Allowed scope" needed the same
  `loadLog` -> `commandsOf` swap to avoid an outright runtime failure:
  `src/CommandoWar.Headless/AppraisalDemo.fs` and
  `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs` (nine call sites). Both
  are mechanical, no behaviour change; flagged in the task file for Dave's
  review since they were not anticipated when central decisions were
  confirmed.
- The "Command log" row change in every migrated `.md` was not explicitly
  named in the original acceptance criteria's "byte-identical" wording —
  corrected in the task file (Acceptance criteria) to name it as an
  intended, `parseTable`-invisible correction rather than a silent
  deviation.
- `docs/04_SIMULATION_SPEC.md` / `docs/09_TEST_STRATEGY.md` each had one
  claim this task's migration made stale, corrected; both documents have
  additional pre-existing staleness (e.g. `docs/09`'s corpus description
  missing several later tasks' entries) that predates this task and is
  out of scope here.
- Implemented directly on `main`, no branch — a deliberate departure from
  the TASK-030–034 M-sized-task branch precedent, justified by this task's
  much lower risk profile (a fully-verified, behaviour-neutral refactor
  with every hash re-confirmed unchanged) and the DIAG-001/TASK-035
  direct-to-main precedent for low-risk work. Flagged to Dave before
  committing.

### Documents updated

`tasks/TASK-036-SHARED-FIXTURE-BUILDER.md` (Outcome, Acceptance criteria,
Evidence, Review), `docs/11_BACKLOG.md` (TASK-036 row, B-049 row),
`docs/12_PROGRESS_LEDGER.md` (this row), `PROJECT_STATE.yaml`
(`active_work.note`), `content/replays/CORPUS.md`,
`docs/04_SIMULATION_SPEC.md`, `docs/09_TEST_STRATEGY.md`. No ADR.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-16)
- Notes: re-verified before accepting: `dotnet build` 0/0, `dotnet test`
  287/287, `-- corpus` 12/12, `-- fixture` format 7 / 36 events unchanged,
  `git status` matched expected scope. Both deviations (`AppraisalDemo.fs`,
  `DiagnosticsTests.fs`) and the "Command log" row correction accepted
  as-is; see the task file's Review section for the same notes.
