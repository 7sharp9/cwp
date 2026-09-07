## 2026-09-07 - TASK-025 - Production replay-command serialisation

**Owner:** Dave with coding-agent assistance
**Source revision:** `6d3e4a1` (Accept TASK-024; TASK-025/026 ready) on `main`
— TASK-024 / B-044 is merged to `main`, so this task branches off `main`
(`git log --oneline -3` confirmed `34294fe Implement TASK-024` is an ancestor
of HEAD). Branch `task-025-replay-command-serialisation`.
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; .NET 10.0.11;
xUnit 2.9.3; FsCheck / FsCheck.Xunit 3.3.4
**Status change:** `tasks/TASK-025-REPLAY-COMMAND-SERIALISATION.md`
`ready -> done` (implemented 2026-09-07, pending Dave's acceptance);
`docs/11_BACKLOG.md` TASK-025 row + B-045 `ready -> done`, B-049 precondition
note; `PROJECT_STATE.yaml` `active_work.selected_task` `none -> TASK-025 -> none`.
No `Canonical.FormatVersion` change (stays `2`); no committed hash moved.

### Precondition check

`PROJECT_STATE.yaml` `active_work.selected_task` was `none` (TASK-024 accepted;
TASK-025/026 ready). TASK-025's dependencies B-014 / TASK-020 and B-044 /
TASK-024 are both `done` and accepted (TASK-024 accepted by Dave 2026-09-07,
`docs/ledger/2026-09-07-TASK-024-issue-tick-semantics.md`). The task file's two
selection decisions were put to Dave and confirmed:

- **Format:** deterministic line-based **text**, not binary (Central decision 3
  recommendation).
- **Scope:** header + `RecordedCommand[]`; the initial `WorldState` stays a
  named scenario / builder reference, **not** serialised (Central decision 2
  recommendation). Confirmed sound against `Corpus.fs` — corpus initial states
  are `unit -> WorldState` builders in code, not serialisable content, and
  `Replay.record` already takes `initial` separately.

Baseline before any edit: `dotnet build CommandoWar.slnx -c Release` `0/0`;
`dotnet test` `Passed: 199`; `cwheadless corpus` 7x PASS; `cwheadless fixture`
`0xAFA35198CC6BD8D4`, 33 events, canonical format 2.

### Diagnosis and chosen approach

After TASK-020 / TASK-024 the legacy `.cwlog` grammar
(`<tick> <agentId> move <x> <y>`, frozen at v1) cannot represent a full
accepted command: no multi-recipient addressing, no `Urgency` / `RiskTolerance`,
no `IssuedAtTick` distinct from the delivery tick. `docs/04` section 16 already
mandates a versioned replay format that fails loudly on an unknown version;
this task realises it for the command log.

**New module `src/CommandoWar.Sim/ReplaySerialisation.fs`** (compiled after
`Replay.fs`, before `Divergence.fs`). `FSharp.Core` only — hand-rolled like
`CommandLogFile.parse` and `Canonical.encode`; `System.Text.Json` / YAML / a
binary serialiser package are all avoided.

- **`FormatVersion = 1`** literal, owned by this module, independent of
  `Replay.FormatVersion`, `CommandLog.Version`, `Canonical.FormatVersion`.
- **`ReplayCommandFile`** record: `Version`, `Seed`, `TickCount`,
  `CanonicalFormat`, `Meta: ReplayMeta`, `InitialHash: uint64 option`,
  `Checkpoints: Checkpoint[]`, `Commands: RecordedCommand[]`. Not a
  `ReplayRecord` — no `InitialState`.
- **Grammar** (UTF-8, one directive per line; `#` / blank ignored). Six header
  directives once each in fixed order — `version`, `seed`, `ticks`,
  `canonical`, `build`, `scenario` — then optional `initial-hash 0x<16hex>`,
  then strictly-ascending `checkpoint <tick> 0x<16hex>` lines, then
  strictly-ascending `command <deliveryTick> <seq> <id> <issuedAtTick>
  <urgency> <risk> <issuer> <recipients> move <x> <y>` lines. `<urgency>` =
  `routine|immediate`; `<risk>` = `cautious|standard|aggressive` (lowercase);
  `<issuer>` a whitespace-free token; `<recipients>` a non-empty
  comma-separated id list. `move` is the only intent; the grammar has room for
  `hold` / `suppress` / `assault` / `withdraw` as later intent keywords with no
  version bump.
- **`serialise`** is deterministic: fixed field order, integers only via
  culture-invariant `sprintf "%d"` / `sprintf "0x%016X"`, `\n` endings,
  commands emitted in `(Tick, Sequence)` order, checkpoints in tick order.
  Guards (`invalidArg`) an empty / whitespace-bearing issuer and a newline in
  the metadata — data the one-line grammar cannot round-trip.
- **`parse`** is strict about directive order so `parse >> serialise` and
  `serialise >> parse` are identity on valid input. Integer parsing forced to
  `CultureInfo.InvariantCulture`. Typed `ParseError` DU (16 cases) + a
  `describeError`, every case naming the source line or the missing field, in
  the `CommandLogFile.ParseError` / `ReplayError` style. An unknown
  `FormatVersion` -> `UnsupportedFormatVersion` (no migration attempted); a
  `canonical` line that does not equal `Canonical.FormatVersion` ->
  `CanonicalFormatMismatch`; an out-of-order command / checkpoint block ->
  `CommandsOutOfOrder` / `CheckpointsOutOfOrder`.
- **`ofRecord` / `toReplayRecord`** bridge to `ReplayRecord`. `ofRecord`
  records `Hashing.hash initial` as `InitialHash`. `toReplayRecord initial file`
  plugs the caller-supplied initial state back in (container / log / canonical
  versions from the current build) and hands the result to `Replay.run`, which
  owns every existing validation (seed vs. initial stream, tick-0, monotonic /
  in-range / unique-`CommandId` log).

**`cwheadless replay-file <path>`** (new verb; the existing `replay` verb keeps
its `.cwlog`-against-the-fixture meaning). Parses, resolves `Meta.Scenario`
against `Corpus.all` by name (`resolveScenario`, no `Corpus.fs` change),
checks `InitialHash` against the built state, `Replay.run`, prints the per-tick
hash table and the ordered accepted commands, then compares `file.Checkpoints`
to the run. Exit codes reuse the `Exit` module: `2` (`replayError`) on a
parse / scenario / initial-hash / `Replay.run` failure, `3` (`diverged`) on a
checkpoint mismatch, `0` otherwise.

**Committed fixture `content/replays/envelope-full.cwreplay` + `.md`.** A
three-recipient `MoveTo` (agents 3, 4, 5), `Urgency = Immediate`,
`RiskTolerance = Aggressive`, issued on tick 1 and delivered on tick 2, over
the shared spike-fixture initial state (`Setup.sixAgentWorld`, 32 x 32, seed
20260902), 24 ticks. Initial hash `0xE13D7540912C7E25`, final hash
`0x5028174266E2BF6F`, 72 domain events. The `.cwreplay` file carries its own
`checkpoint` lines; `envelope-full.md` is the same per-tick table in the
`content/replays/<name>.md` shape (`Corpus.parseTable`-readable). Not added to
`Corpus.all` (that path is `.cwlog` + `.md` only); the `ReplayTests` fixture
fact is the determinism guard and cross-checks all four hash sources.

### Changes

- **`src/CommandoWar.Sim/ReplaySerialisation.fs`** — new file (module
  `ReplaySerialisation`): `FormatVersion`, `ReplayCommandFile`, `ParseError`,
  `serialise`, `parse`, `describeError`, `ofRecord`, `toReplayRecord`.
- **`src/CommandoWar.Sim/CommandoWar.Sim.fsproj`** — `<Compile
  Include="ReplaySerialisation.fs" />` between `Replay.fs` and `Divergence.fs`.
- **`src/CommandoWar.Headless/Program.fs`** — `resolveScenario`,
  `cmdReplayFile`, `"replay-file" :: rest` dispatch arm, `usage ()` verb line +
  a `.cwreplay` format note. No change to `cmdReplay` / `cmdCorpus` /
  `cmdFixture`.
- **`src/CommandoWar.Headless/CommandLogFile.fs`** — module doc-comment only:
  points to `ReplaySerialisation` as the production format and states the
  `.cwlog` grammar / `Version` are frozen. No grammar or `Version` change.
- **`content/replays/envelope-full.cwreplay`**, **`content/replays/envelope-full.md`**
  — new committed fixture + its per-tick hash table.
- **`tests/CommandoWar.Sim.Tests/ReplayTests.fs`** — `open` FsCheck + System.IO
  + `CommandoWar.Headless`; a new "Production replay-command serialisation"
  section: generators (`replayFileGen` etc.), the round-trip property
  (`MaxTest = 200`), the full-field-matrix round-trip fact, unknown-version /
  canonical-mismatch / commands-out-of-order rejection facts, a hand-written
  full-envelope parse fact, and the committed-`envelope-full` cross-check fact.
  No pre-existing fact modified.
- **`docs/04_SIMULATION_SPEC.md`** — section 13 "On-disk form realised by
  TASK-025" block; section 16 realisation note naming the format and its
  version and its blast radius.
- **`docs/09_TEST_STRATEGY.md`** — section 2.4 "Extended by TASK-025" note.
- **`docs/11_BACKLOG.md`** — TASK-025 row `ready -> done`, B-045 `ready ->
  done`, B-049 precondition note.
- **`docs/12_PROGRESS_LEDGER.md`** — this index row; "Green tests" pinned
  `199 -> 206`; new `ReplaySerialisation.FormatVersion` pinned-facts row.
- **`PROJECT_STATE.yaml`** — `active_work`, both `updated` lines.
- this entry. No ADR.

### Verification

All from the repository root, on branch `task-025-replay-command-serialisation`.

- `dotnet build CommandoWar.slnx -c Release` -> `Build succeeded. 0 Warning(s)
  0 Error(s)` (all four projects; `src/CommandoWar.Sim` has
  `TreatWarningsAsErrors`).
- `dotnet test CommandoWar.slnx -c Release --no-build` before -> `Passed: 199`.
  After -> `Passed: 206, Failed: 0` (+7 `ReplayTests`; every pre-existing fact
  green, unmodified). Round-trip property:
  `ReplaySerialisation round-trips every envelope field over generated command
  logs`, `[<Property(MaxTest = 200)>]` — 200 cases, asserting
  `parse (serialise f) = Ok f` and `serialise (that) = serialise f`.
- `dotnet run --project src/CommandoWar.Headless -c Release --no-build --
  corpus` -> `OK - all 7 entries match their committed tables`, exit `0`,
  before and after (no `--regenerate`).
- `dotnet run ... -- corpus --regenerate` then `git status --porcelain` -> the
  seven existing `content/replays/*.md` are byte-identical (unchanged); only
  `envelope-full.cwreplay` / `envelope-full.md` (untracked) and the source /
  test / doc files appear.
- `dotnet run ... -- fixture` -> `final hash 0xAFA35198CC6BD8D4 (format 2)`,
  `events 33`, `canonical format 2`, before and after — byte-identical.
- `dotnet run ... -- replay-file content/replays/envelope-full.cwreplay` ->
  prints the 24-row hash table + the accepted command +
  `checkpoints  : OK (24 ticks match the file's committed hashes)`, exit `0`.
- Same verb against a copy with `version 1` -> `version 2` -> `parse error:
  ... unsupported replay-command format version '2', this build supports 1 (no
  migration is attempted)`, exit `2`.
- Same verb against a copy with one `checkpoint` hash perturbed -> `DIVERGED
  ...  first bad tick : 5  expected hash : 0xDEADBEEFDEADBEEF  actual hash :
  0xEE082B61D4868A61`, exit `3`.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive` -> `FSharp.Core 10.1.303` only.
- Source scan `git diff src/CommandoWar.Sim` for
  `float|Stopwatch|DateTime|System.Random|godot|System.Text.Json|Dictionary|async`
  -> clean. The new file uses only `System.Text.StringBuilder`,
  `System.{Int32,Int64,UInt64}.TryParse` (invariant-culture overload),
  `System.Uri.IsHexDigit`, `System.Convert.ToUInt64(_, 16)`, `System.Char`,
  `System.StringSplitOptions` — the same BCL-primitive category as
  `CommandLogFile.fs`.
- `git status --porcelain` -> `ReplaySerialisation.fs`, `CommandoWar.Sim.fsproj`,
  `Program.fs`, `CommandLogFile.fs`, `ReplayTests.fs`, `envelope-full.cwreplay`,
  `envelope-full.md`, plus the docs / control files. Matches "Allowed scope".

### Evidence

- **Format:** replay-command file format v1, deterministic line-based text.
  Grammar in the `ReplaySerialisation.fs` module doc and `docs/04` section 16.
- **Round-trip property:** `MaxTest = 200`. Generator covers 1..5 distinct
  recipients (multi-recipient), both `Urgency` values, all three
  `RiskTolerance` values, and `IssuedAtTick` biased strictly below the delivery
  tick (with the equal case included). A separate explicit fact pins every
  point in that matrix.
- **New fixture:** `content/replays/envelope-full.cwreplay`. Initial hash
  `0xE13D7540912C7E25` (tick 0), final hash `0x5028174266E2BF6F` (tick 24), 72
  domain events. Envelope `.cwlog` cannot express: `Recipients = [3; 4; 5]`,
  `Urgency = Immediate`, `RiskTolerance = Aggressive`, `IssuedAtTick = 1` with
  delivery tick `2`.
- **No existing table moved:** `-- corpus` 7x PASS and `-- fixture`
  `0xAFA35198CC6BD8D4` before and after; `corpus --regenerate` leaves the seven
  `.md` files byte-identical.
- **Version blast radius (Central decision 5):** `Canonical.FormatVersion` `2`,
  `CommandLogFile.Version` `1`, `Replay.FormatVersion` `1`, `CommandLog.Version`
  `1`, `ScenarioContent.Version` `2` — all unchanged. New literal:
  `ReplaySerialisation.FormatVersion = 1`.

### Deviations and unresolved issues

- **Verb name is `replay-file`, not `replay`.** The task's Decision 6 example
  was `cwheadless replay <file>`, but `replay` is already the verb that
  replays a `.cwlog` against the shared fixture. `replay-file` is the new
  verb; the alternative (extending `cmdCorpus`) was not taken because
  `cmdCorpus` is hard-wired to the `.cwlog` + `.md` per-entry shape.
- **The committed fixture is not in `Corpus.all`.** Registering it there would
  require teaching `Corpus.loadLog` / `Corpus.renderTable` a second file format
  (they assume `<name>.cwlog` + generated `<name>.md`), churning the seven
  existing entries' `Entry` shape. Instead the fixture is guarded by the new
  `ReplayTests` fact, which cross-checks the `.cwreplay` `checkpoint` lines, the
  `.md` table, a pinned hash array, and a fresh `Replay.run` — a four-way
  agreement, stronger than the `.cwlog` corpus entries' single committed table.
  `cwheadless corpus` (and therefore CI's explicit corpus step) does not
  exercise it, but `dotnet test` (and therefore CI) does. Adding a
  `replay-file` step to `.github/workflows/ci.yml` is a clean follow-up; not
  done here because `.github/` is outside this task's Allowed scope.
- **`ReplayTests.fs` gains `open CommandoWar.Headless`** (for `Corpus.parseTable`
  in the fixture cross-check and `Setup` is already in `CommandoWar.Sim`).
  `FixtureTests.fs` / `CorpusTests.fs` already do this; the test project
  already references `CommandoWar.Headless`.
- **Culture sensitivity:** integer parse/format in `ReplaySerialisation` is
  forced to `CultureInfo.InvariantCulture` / F# `printf` (already invariant),
  so the format is host-culture-independent even though `CommandoWar.Sim` (unlike
  `CommandoWar.Headless`) does not set `InvariantGlobalization`.
- **B-049** (shared F# fixture builder) now has its precondition:
  `ReplaySerialisation.serialise` is the in-memory command-stream emitter that
  builder targets.

### Documents updated

- `tasks/TASK-025-REPLAY-COMMAND-SERIALISATION.md` (`## Outcome`, `Status`,
  acceptance boxes)
- `src/CommandoWar.Sim/ReplaySerialisation.fs` (new),
  `src/CommandoWar.Sim/CommandoWar.Sim.fsproj`
- `src/CommandoWar.Headless/Program.fs`, `src/CommandoWar.Headless/CommandLogFile.fs`
- `content/replays/envelope-full.cwreplay` (new),
  `content/replays/envelope-full.md` (new)
- `tests/CommandoWar.Sim.Tests/ReplayTests.fs`
- `docs/04_SIMULATION_SPEC.md` (sections 13, 16),
  `docs/09_TEST_STRATEGY.md` (section 2.4)
- `docs/11_BACKLOG.md` (TASK-025, B-045, B-049 rows)
- `docs/12_PROGRESS_LEDGER.md` (this index row; "Green tests" `199 -> 206`;
  new `ReplaySerialisation.FormatVersion` pinned fact)
- `PROJECT_STATE.yaml` (`active_work`; both `updated` lines)
- this entry
- no ADR (realises `docs/04` section 16's existing versioned-replay
  requirement; decides no new architecture, framework, determinism contract, or
  gate — the TASK-021 / TASK-022 / TASK-023 / TASK-024 "no ADR — realises an
  existing requirement" precedent)

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: both selection decisions (text format; header + command log) were
  confirmed with Dave before the parser was written.
