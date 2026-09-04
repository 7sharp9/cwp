## 2026-09-04 - TASK-016 - Divergence diagnostics and replay corpus infrastructure

**Owner:** Dave with coding-agent assistance
**Source revision:** `4b94eac` (Add pathfinding-based movement executor)
**Environment:** Windows 11 Pro 26200 (25H2); .NET SDK 10.0.303; .NET 10.0.11;
xUnit 2.9.3
**Status change:** TASK-016 `proposed -> active -> review`

### Precondition: TASK-015 finalised

TASK-015's task file was already `Status: done` (all acceptance criteria
checked) but not yet accepted. Finalised with the TASK-009 / TASK-013 /
TASK-014 pattern, no re-run of its full verification:

- `docs/11_BACKLOG.md`: the "Current work" TASK-015 row `active -> done`; the
  "Planned simulation work" B-011 row `active -> done` (task-file link kept);
  B-011b left `proposed`.
- `docs/ledger/2026-09-04-TASK-015-movement-executor.md` Review block
  `Accepted: pending -> yes (2026-09-04)` with an acceptance note; the
  `docs/12` index row's status change extended to `-> done`, Accepted `-> yes`.
- Check performed: `git status` scope (every changed path is TASK-015
  finalisation or this task) plus one `dotnet test CommandoWar.slnx -c
  Release` = `Passed: 153` (unchanged). Both passed, so finalisation proceeded.

### Central decisions

- **Corpus entry format: reuse `.cwlog` v1 plus a new committed Markdown hash
  table; no new format.** Each entry under `content/replays/` is `<name>.cwlog`
  (`CommandLogFile` v1, unchanged) and a generated `<name>.md` (initial state
  note, tick count, initial/final hash, domain-event count, and the per-tick
  `tick -> hash` table) — the same shape as
  `content/fixtures/SPIKE-FIXTURE.md`. `<name>.md` is fully generated
  (`Corpus.renderTable`) from the `Entry` record and a fresh replay, so
  regenerating it is idempotent and it is never hand-edited.
- **The three new entries get focused `RawScenario` builders in a new
  `Corpus.fs`, not a reuse of `PathDemo` / `DemoScenario`.** `PathDemo`'s
  terrain could have produced a wall-detour and a no-path entry with no new
  builder code, but coupling the corpus to a fixture that exists for rendering
  demos (and can change for rendering reasons) works against the corpus's
  purpose as a stable regression artifact. `Corpus.fs` mirrors the `PathDemo` /
  `LosDemo` precedent instead: a small `RawScenario` per entry, validated
  through `Scenario.validate`, instantiated through `World.ofScenario`. Not
  authoritative game content; no on-disk format (B-024).
- **Initial-state resolution: a corpus-owned name -> `WorldState` registry
  (`Corpus.all : Entry[]`), not a `--scenario` flag on `replay`.** Keeps
  `cwheadless replay` hardwired to `Fixture.initialState ()`, untouched.
- **New pins are genuinely new; no re-pin of the spike fixture.** The
  `spike-fixture` entry's 40-value table is the same sequence
  `SPIKE-FIXTURE.md` / `FixtureTests.fs` already pin.
  `CorpusTests.``the corpus includes the spike fixture and is not an
  independent re-pin of it``` cross-checks the committed table's initial hash
  (`0xF2F3DF0D820AD9AC`), final hash (`0x838D3AE7DBFB735D`), and event count
  (`33`) against `Fixture.run ()` directly, and its command log against
  `Fixture.commandLog ()`. `cwheadless fixture` output is unchanged.
- **The divergence report is honest about what the committed table can show.**
  The table stores reference hashes only (no per-tick states, no draw counts,
  no canonical sections — deferred subhashes are B-012b), so a table mismatch
  reports the first bad tick and the expected/actual hash plus the fresh run's
  draw count, with a note when the committed table itself looks stale (wrong
  tick count, event-count-only mismatch). `checkEntry` additionally replays
  every entry twice and runs `Divergence.compare` between the two independent
  runs before touching the table: a genuine-nondeterminism finding (which
  cannot happen from data the corpus already has stored) gets the full
  `DivergencePoint` report — first differing canonical section and both
  sides' random-draw counts included — because both sides are fresh
  `ReplayOutcome`s.
- **In-suite and CLI, both.** `CorpusTests.fs`'s `[<Theory>]` (one row per
  `Corpus.all` entry, `content/replays/*` copied next to the test assembly —
  the `content/diagnostics/*` precedent) is the regression guard that runs
  under plain `dotnet test`; `cwheadless corpus [--regenerate]` is for humans
  and CI localisation of a failure (matches the `BenchmarkTests` "cheap
  in-suite flag" precedent noted in `docs/09` section 2.8).
- **Scope held to the committed corpus, the theory, and the verb.**
  Generative / FsCheck-style property tests (a new dependency) and
  component-level subhashes in the divergence report (would touch
  `Canonical.encode`) are split to **B-012b**; a general on-disk
  scenario+replay bundle format stays **B-024**.

### Changes

- **`src/CommandoWar.Headless/Corpus.fs` (new).** `Corpus.Entry` (name,
  description, initial-state note, `unit -> WorldState`, tick count);
  `Corpus.all` (`spike-fixture`, `wall-detour`, `blocked-goal`,
  `converging-routes`); `loadLog` / `run` (parse + replay an entry);
  `CommittedTable`, `renderTable`, `parseTable` (the generated `<name>.md`
  shape and its parser); `EntryCheck` (`Passed` / `LogError` / `ReplayFailed`
  / `Nondeterministic of DivergenceReport` / `TableError` / `Mismatch of
  Divergence`) and `checkEntry`; `RegenResult` and `regenerateEntry`.
- **`src/CommandoWar.Headless/CommandoWar.Headless.fsproj`.** New compile
  entry `Corpus.fs` (after `PathDemo.fs`, before `DiagnosticRender.fs`).
- **`src/CommandoWar.Headless/Program.fs`.** New `cmdCorpus` (`corpus
  [--regenerate] [--dir PATH]`): checks every `Corpus.all` entry and prints a
  divergence report on the first mismatch (exit `Exit.diverged`), or
  regenerates every table (exit `Exit.replayError` only on a load/replay
  failure); wired into `main` and the `usage` text. New `hxv` helper (hex a
  bare `uint64`, alongside the existing `hx: StateHash -> string`).
- **`content/replays/` (new).** `CORPUS.md` (index: entry table, regeneration
  and checking commands, format notes); `spike-fixture.cwlog` /
  `wall-detour.cwlog` / `blocked-goal.cwlog` / `converging-routes.cwlog`
  (hand-written); `spike-fixture.md` / `wall-detour.md` / `blocked-goal.md` /
  `converging-routes.md` (generated by `corpus --regenerate`).
- **`tests/CommandoWar.Sim.Tests/CorpusTests.fs` (new).** `entryNames`
  (`[<MemberData>]` source, one row per `Corpus.all` entry); the `[<Theory>]`
  asserting `Corpus.checkEntry` is `Passed` for each; a fact cross-checking the
  `spike-fixture` entry against `Fixture.commandLog ()` / `Fixture.run ()`; a
  fact perturbing an in-memory copy of the `wall-detour` command log and
  asserting `Divergence.compare` reports the divergence at tick 1 (proves the
  detection path without touching the committed file on disk).
- **`tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj`.** New compile
  entry `CorpusTests.fs`; new copy-glob item for `content/replays/*` (the
  `content/diagnostics/*` precedent).
- **`.gitattributes`.** `content/replays/** text eol=lf` (the
  `content/diagnostics/**` precedent — generated tables must byte-compare
  identically on Windows).
- **`docs/09_TEST_STRATEGY.md`.** Section 2.4 "Realised by TASK-016" note.
- **Control.** `tasks/TASK-016-REPLAY-CORPUS.md` (new); `docs/11_BACKLOG.md`
  (TASK-016 "Current work" row `active`; B-012 `-> active` with the task-file
  link; new B-012b row `proposed`; TASK-015 / B-011 `-> done`);
  `PROJECT_STATE.yaml` (`active_work -> TASK-016`; gates / gate / phase /
  framework_decision unchanged); `docs/12_PROGRESS_LEDGER.md` (index rows for
  TASK-016 and the TASK-015 acceptance; "Green tests" pinned row `153 -> 159`);
  `docs/ledger/2026-09-04-TASK-015-movement-executor.md` (Review block
  accepted); this entry.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (all four projects).
- Command: `dotnet test CommandoWar.slnx -c Release` (before the corpus work,
  after the TASK-015 finalisation edits)
  - Result: `Passed! - Failed: 0, Passed: 153, Skipped: 0, Total: 153`.
- Command: `dotnet test CommandoWar.slnx -c Release` (after the full TASK-016
  change)
  - Result: `Passed! - Failed: 0, Passed: 159, Skipped: 0, Total: 159`. The six
    added facts: four `[<Theory>]` rows (one per corpus entry) plus two facts
    in `CorpusTests.fs`. Every previously green test stayed green.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus`
  - Result: `PASS` for all four entries, `OK - all 4 entries match their
    committed tables`, exit `0`.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus --regenerate`, then `git add content/replays` and the same command
  again, then `git status --porcelain content/replays` / `git diff --stat
  content/replays`
  - Result: regeneration is idempotent — after staging, a second regeneration
    reports the same four `wrote ...` lines and leaves every file byte-for-byte
    identical (`git status` shows the same four staged `A` entries with no
    working-tree modification, `git diff --stat` empty).
- Manual check: perturbed `content/replays/wall-detour.cwlog`'s move target
  from `(10,3)` to `(9,3)`, then `dotnet run --project src/CommandoWar.Headless
  -c Release -- corpus`
  - Result: `DIVERGED wall-detour: this build disagrees with the committed
    table`, `first bad tick : 1`, `expected hash : 0xCBB8C5B28AB3AC38`,
    `actual hash : 0xA5AC223312C57741`; the other three entries still `PASS`;
    exit `3` (`Exit.diverged`). Reverted with `git checkout --
    content/replays/wall-detour.cwlog`; `corpus` returned to `OK`, exit `0`.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  fixture`
  - Result: initial `0xF2F3DF0D820AD9AC`, final (tick 40) `0x838D3AE7DBFB735D`
    (format 1), 33 events, per-tick sequence byte-identical to
    `SPIKE-FIXTURE.md` — unchanged.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
  - Result: `FSharp.Core 10.1.303` only; no transitive package.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj reference`
  - Result: "There are no Project to Project references."
- Command: source scan of `src/CommandoWar.Sim/*.fs` for
  `float|stopwatch|datetime|system\.random|godot|monogame|raylib`
  - Result: only pre-existing doc-comment prose (unchanged by this task, which
    touches no file under `src/CommandoWar.Sim/`). No type, API, or value
    match.
- Command: `git status --porcelain`
  - Result: TASK-015 finalisation (`docs/11`, `docs/12`,
    `docs/ledger/2026-09-04-TASK-015-*`, `PROJECT_STATE.yaml`) plus, for this
    task: new `src/CommandoWar.Headless/Corpus.fs`; modified
    `CommandoWar.Headless.fsproj`, `Program.fs`; new `content/replays/*` (nine
    files); modified `tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj`;
    new `CorpusTests.fs`; modified `.gitattributes`, `docs/09`; new
    `tasks/TASK-016-REPLAY-CORPUS.md`, this file. Nothing under
    `src/CommandoWar.Sim/`, the client spikes, `src/_scratch`, `bench/`,
    `content/fixtures/`, `content/diagnostics/`, or any existing `cwheadless`
    verb's output.

### Evidence

- **Fixture pin unmoved:** `0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`, 33
  events, format 1 (`cwheadless fixture`, `dotnet test`).
- **New pins are genuinely new:** `content/replays/spike-fixture.md`'s table
  is the same 40-value sequence as `SPIKE-FIXTURE.md`; `CorpusTests` asserts
  this directly against `Fixture.run ()` rather than trusting the committed
  file alone.
- **Divergence detection:** the perturbed-`.cwlog` manual check above (exit 3,
  first bad tick 1, expected/actual hash) plus
  `CorpusTests.``a perturbed command log is reported as a table mismatch at
  the first divergent tick```.
- **Idempotence:** regenerating a staged corpus produces zero `git diff`.
- **Green count:** `dotnet test` `153 -> 159`.

### Deviations and unresolved issues

- **`converging-routes` pins today's no-reservation behaviour, as flagged in
  the task.** Agent 0 (3,0)->(3,7) and agent 1 (0,3)->(7,3) occupy the same
  cell `(3,3)` on the same tick with no collision handling. B-011b must re-pin
  this one entry's `.md` table (`corpus --regenerate` after that change,
  reviewed like any other intentional hash move) when it lands reservation.
- **The committed table cannot carry a canonical-section or draw-count
  comparison against itself** (deferred component subhashes, B-012b), so a
  table mismatch report shows the fresh run's draw count only, with a note
  that the committed side stores hashes alone. The two-independent-runs
  determinism check (`Divergence.compare`) does carry the full report,
  because both sides there are fresh `ReplayOutcome`s.
- **`cwheadless corpus`'s printed paths use Windows path separators**
  (`Path.Combine` on this platform, e.g. `content/replays\wall-detour.md`).
  Cosmetic only: nothing byte-compares this output (unlike `render --out`,
  which writes files that are byte-compared), and it matches how every other
  verb already prints host paths.

### Documents updated

- `tasks/TASK-016-REPLAY-CORPUS.md` (new)
- `tasks/TASK-015-MOVEMENT-EXECUTOR.md` (unchanged; already `done`)
- `src/CommandoWar.Headless/Corpus.fs` (new), `CommandoWar.Headless.fsproj`,
  `Program.fs`
- `content/replays/CORPUS.md`, `*.cwlog` x4, `*.md` x4 (new)
- `tests/CommandoWar.Sim.Tests/CorpusTests.fs` (new),
  `CommandoWar.Sim.Tests.fsproj`
- `.gitattributes`
- `docs/09_TEST_STRATEGY.md` (section 2.4)
- `docs/11_BACKLOG.md` (TASK-016 row; B-012 `-> active`; B-012b new; TASK-015 /
  B-011 `-> done`)
- `docs/12_PROGRESS_LEDGER.md` (index rows for TASK-016 and the TASK-015
  acceptance; "Green tests" pinned row)
- `docs/ledger/2026-09-04-TASK-015-movement-executor.md` (Review block
  accepted)
- `PROJECT_STATE.yaml` (`active_work -> TASK-016`)
- this entry

### AGENTS.md / docs/09 section 8 standing rule

Does not apply. TASK-016 adds no authoritative spatial or tactical state — it
is evidence infrastructure over the existing `Simulation` / `Replay` /
`Divergence` output — so no `DiagnosticFrame` extension or golden render is
added; `content/replays/CORPUS.md` records this explicitly rather than adding
an overlay (the TASK-014 precedent).

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: committed, regenerable four-entry replay corpus
  (`content/replays/`: spike fixture, wall detour, `MovementBlocked` no-path,
  two-agent converging routes) over the existing `.cwlog` v1 format;
  `Corpus.fs` name -> `WorldState` registry and check/regenerate logic;
  `cwheadless corpus [--regenerate]` verb; in-suite `CorpusTests.fs`
  `[<Theory>]`; spike-fixture entry cross-checked against `Fixture.run ()`, not
  an independent re-pin; `converging-routes` pins today's no-reservation
  behaviour for B-011b to re-pin; generative property tests and component
  subhashes split to B-012b.
