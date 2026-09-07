# TASK-025: Production replay-command serialisation

Status: proposed (complete task file; TASK-024 / B-044 implemented 2026-09-07
and pending Dave's acceptance — this moves to `ready` on that acceptance)
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises backlog B-045 — **mandatory before G3**
Size: M

## Objective

Give the project one **versioned, lossless on-disk format for accepted
commands** so that a replay reproduces the complete decision trace G3
evidence requires.

After TASK-020, `.cwlog` (`src/CommandoWar.Headless/CommandLogFile.fs`,
grammar `<tick> <agentId> move <x> <y>`) is a legacy fixture-script format
that **cannot represent an accepted command in full**: it has no syntax for
multi-recipient addressing (`PlayerCommand.Recipients: AgentId list`),
`Urgency`, or `RiskTolerance`, and — after TASK-024 — no syntax for an
`IssuedAtTick` distinct from the submission tick. TASK-020 designated
`.cwlog` "a legacy fixture-script format, not the production replay-command
format" and made this task the mandatory-before-G3 follow-up.

This task chooses and implements the production format. It does **not**
migrate the existing corpus, build a scenario-authoring format (that stays
B-024), or build the shared F# fixture builder (that is B-049, which sits
after this task).

## Why this task exists

- `docs/08_ROADMAP_AND_GATES.md` section 6 P3 Required work: "resolve ... the
  production replay-command serialisation (B-045) ... must be settled before
  this gate."
- `docs/04_SIMULATION_SPEC.md` section 16 lists what a replay file records —
  "ordered accepted commands" among them. The only on-disk command format
  today (`.cwlog`) can no longer hold an accepted command.
- G3 evidence: "the canonical refusal test passes", "at least two agents
  appraise the same intent differently for inspectable reasons", "scenario
  traces are readable enough to diagnose all decisions". A refusal scenario
  turns on `Urgency` / `RiskTolerance` (`docs/05` section 5 stage 4) and on
  multi-recipient orders; if the replay format cannot carry them, the G3
  evidence run cannot be replayed.
- Fixed project constraint: "Recorded commands, deterministic random source,
  and replay evidence."
- Risk R-017 (save/replay compatibility becomes a hidden burden): the answer
  is an explicitly versioned format that fails loudly on an unknown version,
  which is exactly what this task builds.

Depends on **TASK-024 / B-044**: the format must serialise whatever tick
fields TASK-024 settles (`IssuedAtTick`, and `SubmitAtTick` if that rename is
taken). B-045 stays `proposed` until TASK-024 is `done`, then moves to
`ready` (backlog section 1: `ready` requires satisfied dependencies).
Independent of TASK-022, TASK-023, TASK-026.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `tasks/TASK-024-ISSUE-TICK-SEMANTICS.md` (Central decisions — the tick
  model this format serialises) and its ledger detail file once TASK-024 is
  done
- `src/CommandoWar.Sim/Commands.fs` — `PlayerCommand` (the full envelope:
  `Id`, `IssuedAtTick`, `Recipients`, `Urgency`, `RiskTolerance`, `Intent`),
  `PlayerIntent` (only `MoveTo` today; `Hold` / `Suppress` / `Assault` /
  `Withdraw` are later tasks), `Urgency`, `RiskTolerance`
- `src/CommandoWar.Sim/Replay.fs` — `RecordedCommand`, `CommandLog`
  (`Version`), `ReplayRecord` (`Version`, `CanonicalFormat`, `Meta`, `Seed`,
  `InitialState`, `TickCount`, `Log`, `Checkpoints`), `ReplayMeta`,
  `ReplayError`, `Replay.record` / `Replay.run` / `Replay.validate`
- `src/CommandoWar.Sim/Canonical.fs` — the fixed-width big-endian
  `Writer` discipline (a model for a binary option; note it does **not**
  round-trip — it is write-only for hashing)
- `src/CommandoWar.Headless/CommandLogFile.fs` — the whole module:
  `Version`, `parse`, `ParseError`, `describeError`, the appearance-order
  `CommandId` minting, the `(Tick, Sequence)` sort. This is the format being
  frozen, and the closest precedent for the new parser's error style.
- `src/CommandoWar.Headless/Corpus.fs` — `Corpus.all`, `Corpus.DefaultDir`,
  `checkEntry` / `compareToTable` / `renderTable` / `parseTable`, the world
  builders and `rawScenario` / `worldOf` / `wall` / `costly`
- `src/CommandoWar.Headless/Program.fs` — verb dispatch, the `Exit` module
  (`ok = 0`, `usage = 1`, `replayError = 2`, `diverged = 3`), `cmdCorpus`,
  `cmdFixture`
- `content/replays/CORPUS.md` and one `content/replays/<name>.md` (the hash
  table format — **not** a command log, unaffected here),
  `content/fixtures/SPIKE-FIXTURE.md`, `content/fixtures/spike-fixture.cwlog`
- `tests/CommandoWar.Sim.Tests/ReplayTests.fs`, `CorpusTests.fs`,
  `FixtureTests.fs`
- `docs/04_SIMULATION_SPEC.md` sections 13 (envelope), 16 (replay record),
  17 (canonical encoding rules — the discipline a binary format would follow)
- `docs/09_TEST_STRATEGY.md` sections 2.4 (replay tests — the required
  divergence-report fields), 6 (CI — the new format's files run through the
  same `dotnet test` / `cwheadless` path)
- `docs/notes/2026-09-06-tooling-and-debug-display.md` section 1 (why the
  B-049 fixture builder waits for this format; what "emit the command stream
  in memory" means for that builder)
- `decisions/ADR-0002-SIMULATION-BOUNDARY.md` (allow/forbid lists — the
  format is framework-neutral typed data; the parser must not add a
  dependency)

## Dependencies

- TASK-024 (B-044) — must be `done` before this task is selected

## Central decisions

Recommended resolutions; the implementer confirms the format shape with Dave
before writing the parser and records the final decision in the ledger.

### Decision 1 — a separate versioned replay-command format, not an extended `.cwlog`

Add a **new format**, do not grow the `.cwlog` line grammar.

- `.cwlog` is deliberately terse fixture-script shorthand, hand-authored for
  corpus entries. Extending its lines to carry recipient lists, two enum
  fields, and two ticks makes hand-authoring hostile and destroys the one
  property worth keeping.
- The new format's job is different: a **lossless, machine-written**
  serialisation of accepted commands (later, whole `ReplayRecord`s) that
  round-trips every envelope field. B-049's fixture builder emits it; the G3
  evidence run replays from it.
- Keep `.cwlog` **frozen at `Version = 1`**, explicitly a legacy
  fixture-script format. It maps a line to the degenerate envelope
  (one recipient, `Routine` / `Standard`, `IssuedAtTick == SubmitAtTick`).

### Decision 2 — the format lives in `CommandoWar.Sim`, serialises the typed replay model, adds no dependency

- New module, e.g. `src/CommandoWar.Sim/ReplaySerialisation.fs` (or an
  addition to `Replay.fs`), owning `serialise` / `parse` over
  `RecordedCommand[]` — and, recommended, over the whole `ReplayRecord`
  (`Meta`, `Seed`, `InitialState`-or-a-scenario-reference, `TickCount`,
  `Log`, `Checkpoints`), so a single file is a complete replay.
- **`CommandoWar.Sim`, not `CommandoWar.Headless`.** `.cwlog` sits in
  Headless because it is a spike convenience; the production replay format is
  a first-class part of the determinism contract (`docs/04` sections 16–17)
  and belongs with `Replay.fs`. `CommandLogFile` stays in Headless.
- **No new package.** Hand-rolled, like `CommandLogFile.parse` and
  `Canonical.encode`. `System.Text.Json`, a YAML library, a binary
  serialiser package — all forbidden (`dotnet list src/CommandoWar.Sim
  package` must stay `FSharp.Core` only). ADR-0002 allow-list: "integers,
  booleans, strings ... arrays, immutable or serializable records".

### Decision 3 — a deterministic line-based text format, versioned, typed errors

**Recommended: text, not binary.** G3 evidence wants "readable" traces
(`docs/08` section 6); a diffable text file also makes divergence review and
code review tractable, and matches the `.cwlog` / `content/replays/*.md`
precedent. A binary format modelled on `Canonical.Writer` is the alternative
if Dave prefers compactness; it would still need a reader (`Canonical` has
none) and lose readability. Decide with Dave.

Text format requirements (whichever concrete grammar is chosen):

- A leading version directive; an unknown version is a typed error, never a
  guessed migration (`docs/04` section 16: "Playback rejects incompatible
  versions clearly. It does not guess migrations.").
- Deterministic output: fixed field order, integers only, no floats, no
  culture-sensitive formatting, stable line order (the existing
  `(SubmitAtTick, Sequence)` sort). Round-tripping is idempotent —
  `parse >> serialise` and `serialise >> parse` are identity on valid input.
- Every accepted-command field representable: `Id`, `IssuedAtTick`,
  `SubmitAtTick`, `Sequence`, `Issuer`, `Recipients` (a list), `Urgency`,
  `RiskTolerance`, `Intent`. `Intent` is `MoveTo target` today; the grammar
  must have room for the later intents (`Hold` / `Suppress` / `Assault` /
  `Withdraw`) without a version bump for each — e.g. an intent keyword plus
  named arguments.
- A `ParseError` / error DU in the `CommandLogFile.ParseError` /
  `ReplayError` style, every case naming the offending line or field, plus a
  `describeError`.
- The `Replay.run` / `Divergence` divergence report (`docs/09` section 2.4:
  first divergent tick, expected/actual hash, first differing canonical
  section, command consumed at the tick, random draw count, sim/content
  versions) is unchanged — this task only changes how the command log is
  read from disk, not how replay diagnoses divergence.

### Decision 4 — the corpus is **not** migrated in this task

The seven `content/replays/*.cwlog` entries and `content/fixtures/spike-fixture.cwlog`
stay `.cwlog`. Migrating them would couple a format decision to a
hash-sensitive regeneration of every committed table for no behavioural gain
(they all use single-recipient default-envelope `move` commands the legacy
format expresses fine).

Instead, prove the new format with:

- **round-trip property / unit tests** over generated and hand-built
  `RecordedCommand[]` covering multi-recipient, every `Urgency`, every
  `RiskTolerance`, `IssuedAtTick != SubmitAtTick`, and a non-`move` intent
  placeholder if the grammar admits one;
- **at least one committed replay fixture in the new format** exercising a
  full envelope (e.g. a 3-recipient `MoveTo` with `Urgency = Immediate`,
  `RiskTolerance = Aggressive`, issued a tick before delivery), with its own
  committed per-tick hash table (the `content/replays/<name>.md` shape) and a
  `CorpusTests` / `ReplayTests` case that replays it and checks the table.
  This is the concrete G3 artefact: a replay whose decision trace the legacy
  format could not hold.

Corpus migration to the new format, and the shared builder that would make it
cheap, are **B-049** (`docs/notes/2026-09-06-tooling-and-debug-display.md`
section 1), after this task.

### Decision 5 — version-field blast radius, stated explicitly

| Version literal | Current | This task |
|---|---|---|
| `CommandLogFile.Version` (`.cwlog`) | `1` | **unchanged** — `.cwlog` frozen as legacy |
| `CommandLog.Version` (typed in-memory log schema, `Replay.fs`) | `1` | **unchanged** unless TASK-024 already moved it; the types are serialised, not redefined |
| `Replay.FormatVersion` (`ReplayRecord.Version`) | `1` | **unchanged** — same reasoning |
| **new** replay-command file-format version | — | **new literal, starts at `1`**, owned by the new module; independent of the three above |
| `Canonical.FormatVersion` | `2` | **unchanged** — command serialisation is not authoritative-state encoding; `PlayerCommand` is not in `Canonical.encode` |
| `ScenarioContent.Version` | `2` | **unchanged** — unrelated |

No committed fixture / corpus / diagnostics hash moves: `.cwlog` and the
`content/replays/*.md` tables are untouched, and the new fixture's hashes are
new, not a re-pin. Confirm with `-- corpus` / `-- fixture` before and after.

### Decision 6 — `cwheadless` surface

Add a minimal verb to run and inspect a replay file in the new format, e.g.
`cwheadless replay <file>` — parse, `Replay.run`, print the per-tick hash
table and the ordered accepted commands (the readable decision trace). Reuse
the `Exit` codes (`replayError = 2` on a parse/validate failure,
`diverged = 3` if a checkpoint table is present and disagrees, `ok = 0`).
This is where B-050 (divergence visualisation) later attaches; do **not**
build B-050 here. Extending `cmdCorpus` to also accept the new format is an
acceptable alternative to a new verb — implementer's call, stated in the
ledger.

### Decision 7 — no ADR

`docs/04` section 16 already mandates a versioned replay format that fails
explicitly on unknown versions; this task realises that requirement. It does
not change the determinism contract (`docs/09` section 3), the architecture,
the framework, or a gate. `docs/08` section 6 already lists B-045. Record the
format choice in this task file and the ledger, not an ADR — the TASK-021 /
TASK-022 / TASK-023 "no ADR — realises an existing requirement" precedent.

## Diagnostics

`AGENTS.md`'s diagnostic-extension rule does **not** apply: this task adds no
authoritative spatial or tactical state. It adds a serialisation format and,
optionally, a `cwheadless` verb. No `DiagnosticFrame` change, no new golden.
The new committed replay fixture's hash table (Decision 4) is determinism
evidence in the `content/replays/` sense, not a diagnostic-frame golden.

## Allowed scope

- **new** `src/CommandoWar.Sim/ReplaySerialisation.fs` (or additions to
  `Replay.fs`) — `serialise` / `parse` over `RecordedCommand[]` and
  (recommended) `ReplayRecord`, the file-format version literal, the error
  DU, `describeError`.
- `src/CommandoWar.Sim/Replay.fs` — only if the serializer is folded in here
  rather than a new file, and any small helper `Replay.run` needs to accept a
  parsed log.
- `src/CommandoWar.Headless/Program.fs` — the new `replay` verb (or the
  `cmdCorpus` extension), help text, exit-code wiring.
- `src/CommandoWar.Headless/Corpus.fs` — only if a new-format entry is added
  to `Corpus.all` (Decision 4's committed fixture); otherwise untouched.
- `src/CommandoWar.Headless/CommandLogFile.fs` — a doc-comment line only,
  stating it is the frozen legacy fixture-script format and pointing to the
  new module. No grammar or `Version` change.
- `content/replays/` — the new-format fixture file(s) and their committed
  hash table + a `CORPUS.md` row (or a sibling `content/replays/` note if the
  new format warrants its own index section). The seven existing `.cwlog`
  entries and their `.md` tables are **not** touched.
- `tests/CommandoWar.Sim.Tests/ReplayTests.fs` — round-trip facts,
  unknown-version rejection, per-field coverage, the new fixture replayed.
- `tests/CommandoWar.Sim.Tests/CorpusTests.fs` — a theory case for the new
  fixture if it is registered in `Corpus.all`.
- `docs/04_SIMULATION_SPEC.md` (section 16 realisation note naming the new
  format and its version; section 13 note that the envelope now has a
  lossless on-disk form), `docs/09_TEST_STRATEGY.md` (section 2.4 realisation
  note).
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml`
  if it becomes the active task).

## Forbidden scope

- Changing the `.cwlog` grammar or `CommandLogFile.Version`; migrating the
  seven existing corpus entries or the spike fixture to the new format
  (B-049).
- A scenario-authoring / map text format (B-024); the shared F# fixture
  builder (B-049).
- Any new package (`System.Text.Json`, YAML, protobuf, MessagePack, …). The
  parser is hand-rolled; `src/CommandoWar.Sim` stays `FSharp.Core` only.
- A `Canonical.FormatVersion` bump or any `Canonical.encode` change; a
  `ScenarioContent.Version` bump; re-pinning any existing fixture / corpus /
  golden hash (a moved hash is a stop-and-report finding).
- The divergence-visualisation renderer (B-050); the Godot developer overlay
  (B-029).
- New `PlayerIntent` cases (`Hold` / `Suppress` / `Assault` / `Withdraw`) —
  the grammar must leave room for them, but implementing them is later work.
- Editing the client spikes, `src/_scratch`, `bench/`, or
  `content/diagnostics/`.

## Acceptance criteria

- [ ] A new versioned replay-command format exists in `CommandoWar.Sim`, with
      a hand-rolled parser and serializer; `dotnet list src/CommandoWar.Sim
      package --include-transitive` is `FSharp.Core` only.
- [ ] `serialise` then `parse` is identity, and `parse` then `serialise` is
      identity, on valid input — proven over generated `RecordedCommand[]`
      that includes multi-recipient commands, every `Urgency`, every
      `RiskTolerance`, and `IssuedAtTick != SubmitAtTick`
      (`ReplayTests.fs`, a property at >= 200 cases).
- [ ] An unknown format version is rejected with a typed error naming the
      version; no migration is attempted (`ReplayTests.fs`).
- [ ] At least one committed replay fixture in the new format carries a full
      envelope the legacy `.cwlog` cannot express (multi-recipient, non-default
      `Urgency` / `RiskTolerance`, distinct issue tick), has a committed
      per-tick hash table, and is replayed and checked by `dotnet test` (and
      by `cwheadless` if registered in `Corpus.all`).
- [ ] `cwheadless` can run a replay file in the new format and print its
      per-tick hash table and ordered accepted commands; a parse/validate
      failure exits `2`, a checkpoint divergence exits `3`.
- [ ] `.cwlog` grammar, `CommandLogFile.Version`, `Replay.FormatVersion`,
      `CommandLog.Version`, `Canonical.FormatVersion` (`2`), and
      `ScenarioContent.Version` are all unchanged; the seven existing corpus
      `.md` tables and `content/fixtures/SPIKE-FIXTURE.md` are byte-identical
      (`-- corpus` / `-- fixture` with no `--regenerate`).
- [ ] Every pre-existing `ReplayTests` / `CorpusTests` / `FixtureTests` /
      `DeterminismPropertyTests` / `SimulationTests` fact passes unmodified.
- [ ] `dotnet build CommandoWar.slnx -c Release` = 0 warnings, 0 errors;
      source scan of `src/CommandoWar.Sim` clean (no `float`, no new
      dependency, no `System.Text.Json`).
- [ ] `docs/04` sections 13 / 16, `docs/09` section 2.4, backlog row (B-045
      `-> done`), ledger index row + detail file, `PROJECT_STATE.yaml`, task
      status updated. "Green tests" pinned fact refreshed.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`
- `dotnet test CommandoWar.slnx -c Release` before and after (state the new
  count; name the round-trip property and its case count)
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus` and
  `-- fixture` before and after — byte-identical committed hashes for the
  seven `.cwlog` entries and the fixture
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus
  --regenerate` then `git diff --stat content/replays` — only the new
  fixture's files (and `CORPUS.md`) appear
- the new `replay` verb against the committed new-format fixture, exit `0`;
  against a version-bumped copy, exit `2`
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
- source scan of `src/CommandoWar.Sim`
- `git status --porcelain` — matches "Allowed scope"

## Evidence to capture

- the chosen format (text or binary), its grammar / layout, and its version
  literal;
- the round-trip property's case count and the field matrix it covers;
- the new committed replay fixture's initial/final hash, tick count, event
  count, and the envelope it carries that `.cwlog` cannot;
- the `-- corpus` / `-- fixture` hash lines before and after (no existing
  table moved);
- the `cwheadless replay` output for the new fixture (the readable decision
  trace) and the exit code on an unknown version.

## Rollback or removal

The new module, verb, tests, and committed fixture are additive; `.cwlog`,
`Replay.fs`'s typed model, and every existing hash are untouched. Reverting
is deleting the new files and the verb wiring. No data migration on apply or
revert. If the format choice proves wrong in B-049, the version literal makes
a v2 an explicit, rejectable change rather than a silent one.

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md` (TASK-025 row; B-045 `proposed -> ready` once TASK-024
  is done, `-> done` on completion);
- `docs/12_PROGRESS_LEDGER.md` index row + `docs/ledger/` detail file;
  refresh "Green tests"; add the new format's version to the "Pinned facts"
  block;
- `docs/04_SIMULATION_SPEC.md` sections 13, 16;
- `docs/09_TEST_STRATEGY.md` section 2.4;
- `PROJECT_STATE.yaml` only if this becomes the active task;
- no ADR (realises `docs/04` section 16's existing versioned-replay
  requirement; decides no new architecture, framework, determinism contract,
  or gate).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
