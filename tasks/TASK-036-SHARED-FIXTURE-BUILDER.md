# TASK-036: Shared F# fixture builder for the test corpus

Status: ready (drafted 2026-09-15; central decisions confirmed with Dave via
`AskUserQuestion` before this file was written — see "Central decisions")
Owner: Dave, implemented by coding-agent assistance
Phase: P3
Gate: G3
Size: M

## Objective

One `Corpus.fs` builder authors a scenario (grid, friendly/enemy deployments,
terrain runs, cover, objective/extraction) and its per-agent command
schedule as a single F# value, replacing the eleven hand-written
`…World ()` functions and their hand-authored `.cwlog` command files with
one generator and a regenerated, committed `.cwreplay` file per entry.

## Why this task exists

Realises backlog B-049 (`docs/11_BACKLOG.md` §3), proposed by
`docs/notes/2026-09-06-tooling-and-debug-display.md` §1. Every corpus entry
today is authored twice: the deployment/terrain geometry lives in
`Corpus.fs`, the command schedule lives in a separately hand-typed
`content/replays/<name>.cwlog`, and nothing checks the two stay in sync
except a hash moving. B-045 (the production replay-command serialisation,
`ReplaySerialisation`/`.cwreplay`) landed 2026-09-07 specifically so a
builder could emit the real format instead of the frozen `.cwlog` v1
grammar (`src/CommandoWar.Headless/CommandLogFile.fs`, which cannot express
multi-recipient addressing, `Urgency`, `RiskTolerance`, or a distinct issue
tick — exactly what B-021's remaining triggers and B-023's canonical
refusal sequence will need from future corpus entries).

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md` (Scope: "do not perform unrelated refactoring")
- `docs/notes/2026-09-06-tooling-and-debug-display.md` §1 (the B-049
  proposal this task realises)
- `src/CommandoWar.Headless/Corpus.fs` (`rawScenario`, `worldOf`, `wall`,
  `opaqueWall`, `costly`, the eleven `…World ()` functions, `Entry`,
  `loadLog`, `run`, `checkEntry`, `regenerateEntry`)
- `src/CommandoWar.Sim/Scenario.fs` (`RawScenario`, `RawDeployment`,
  `RawTerrainCell`, `Scenario.validate`)
- `src/CommandoWar.Sim/ReplaySerialisation.fs` (`ReplayCommandFile`,
  `serialise`/`parse`, the production grammar)
- `src/CommandoWar.Headless/CommandLogFile.fs` (the frozen legacy grammar
  being retired for corpus entries — read its doc comment, do not extend it)
- `src/CommandoWar.Sim/Commands.fs` (`PlayerCommand`, `Command.moveTo`/
  `.moveToMany`)
- `tests/CommandoWar.Sim.Tests/CorpusTests.fs`

## Dependencies

- B-045 / TASK-025 (done — `ReplaySerialisation` is the format this task
  targets)
- TASK-022 (done — `Corpus.fs`'s current shape, precondition already met
  per the backlog row)

## Inputs and assumptions

- `content/replays/spike-fixture.cwlog` and the `spike-fixture` `Entry`
  (backed by `Fixture.fs` / `Setup.sixAgentWorld`, not `rawScenario`) are
  explicitly **out of scope** — it is the framework-spike shared fixture,
  cross-checked against `content/fixtures/SPIKE-FIXTURE.md` by
  `CorpusTests`, not a corpus-authored scenario.
- The eleven other entries (`wall-detour`, `blocked-goal`,
  `converging-routes`, `slow-terrain`, `follow-chain`, `swap-standoff`,
  `perception-contact`, `lost-comms`, `exposed-approach`,
  `reissued-order`, `open-engagement`) are all `rawScenario`-built (or, for
  `exposed-approach`, a direct `RawScenario` literal for per-agent
  `Discipline`) and are this task's migration target.
- This is a pure refactor: every migrated entry's committed
  `content/replays/<name>.md` hash table (initial hash, final hash,
  per-tick hashes, event count) must stay byte-identical — confirms the
  builder reproduces today's `WorldState` and command stream exactly.
- No gameplay, phase, or `Canonical.encode` change of any kind.

## Allowed scope

- `src/CommandoWar.Headless/Corpus.fs`: add the builder type(s) and
  function(s); replace the eleven `…World ()` functions and `rawScenario`/
  `worldOf` call sites with builder calls; change `Entry` to carry its
  command schedule as an in-memory value instead of reading a `.cwlog` file
  at `run`/`checkEntry`/`regenerateEntry` time; regenerate committed
  `.cwreplay` files from that value (the `<name>.md` table's own
  regeneration precedent) instead of `loadLog` reading `.cwlog`.
- `content/replays/*.cwlog` -> `content/replays/*.cwreplay` for the eleven
  migrated entries (delete the `.cwlog`, add the generated `.cwreplay`);
  `spike-fixture.cwlog` untouched.
- `content/replays/CORPUS.md` if it names the per-entry file extension.
- `tests/CommandoWar.Sim.Tests/CorpusTests.fs` only if the entry-loading
  API it calls changes shape (the test bodies' assertions should not need
  to change — this is a refactor, not a behaviour change).
- `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`, `docs/11_BACKLOG.md`.

Nothing under `src/CommandoWar.Sim/` except reading it (no source change —
`ReplaySerialisation` and `Scenario` already have everything this task
needs). No change to `tests/CommandoWar.Sim.Tests/SimulationTests.fs` (see
Decision E). No new corpus entry. No `Canonical.FormatVersion` change. No
`Diagnostics.fs` / `DiagnosticRender.fs` change (no new authoritative or
diagnostic state — this is test/tooling infrastructure, `Corpus.fs`'s own
doc comment: "not authoritative game content").

## Central decisions

Two decisions were confirmed with Dave directly (`AskUserQuestion`, two
questions) before this file was drafted, since both change the shape and
blast radius of the change materially:

### Decision A — commit `.cwreplay` (`ReplaySerialisation`), not `.cwlog`, for migrated entries

The frozen `.cwlog` v1 grammar cannot express multi-recipient commands,
`Urgency`, `RiskTolerance`, or a distinct issue tick. `.cwreplay`
(`ReplaySerialisation`, already production per B-045) can, and is exactly
the format the original B-049 proposal anticipated the builder emitting.
Confirmed over keeping `.cwlog` regenerated-but-format-frozen. The
`ReplayCommandFile.InitialHash` and `.Checkpoints` fields are left empty
(`None` / `[||]`) for corpus entries — `content/replays/<name>.md` stays
the one committed hash-table artefact; duplicating hashes into `.cwreplay`
too would be a second source of the same truth for no reader benefit.

### Decision B — migrate all eleven existing entries, not just future ones

Confirmed over "build the builder, apply it only to the next new entry."
Migrating all eleven is what actually removes the `Corpus.fs` ↔ `.cwlog`
drift and duplication B-049 names as the problem — leaving eleven
hand-authored entries in place alongside a parallel, unused builder would
prove nothing and fix nothing. Every migrated entry's committed `.md` hash
table must stay byte-identical (Decision C below is the mechanism).

### Decision C — the builder's authored value is the sole runtime source of truth; `.cwreplay` is a generated, checked artefact, not read back

Today `Corpus.run`/`checkEntry`/`regenerateEntry` call `loadLog` to parse
`.cwlog` from disk before replaying — the actual drift risk B-049 names
(the file can silently diverge from `Corpus.fs`). After this task, an
`Entry`'s command schedule is a value the builder produces directly in
memory; `run`/`checkEntry` use that value, not a file read. The committed
`.cwreplay` becomes purely a generated, human-reviewable artefact
(`ReplaySerialisation.serialise` of that same value) checked byte-identical
on regeneration — the `<name>.md` precedent — so there is no longer a
second file that can drift, only one that can go stale and be caught by
the existing `--regenerate`-and-diff discipline.

### Decision D — no retrofit of `tests/CommandoWar.Sim.Tests/SimulationTests.fs`

`SimulationTests.fs` does not use `rawScenario`/`Corpus` today — it builds
`WorldState` directly via `Setup.sixAgentWorld` and record updates. The
"duplication with `SimulationTests.fs`" the B-049 note names is that a
corpus entry and a `SimulationTests` fact over similar geometry are
authored twice independently (e.g. TASK-022's swap/follow-chain entries and
facts), not a literal shared code path. Rewriting ~280 existing
`SimulationTests` facts onto the new builder is unrelated-refactor scope
(`AGENTS.md` Scope) for a stated "S–M" backlog item and is explicitly
out of bounds here. The builder's authored value is shaped so a *future*
`SimulationTests` fact for a new scenario *can* reuse it (it already
exposes the same `WorldState` the corpus entry gets); this task does not
force that reuse onto existing tests.

### Decision E — `wall` / `opaqueWall` / `costly` migrate as-is; no new geometry-solving helper

The B-049 note's "cost 1: geometry by hand" (working out which cells force
a route to cross, or a wall to block a path, by trial and error) is
explicitly not addressed by this task — only the authoring/drift/
duplication problem is. The three terrain-cell helpers move into the
builder's terrain-authoring surface unchanged.

## Required work

1. Design the builder type: one record capturing grid `Width`/`Height`,
   friendly and enemy deployments (agent id, cell, optional per-agent
   `Discipline`/`CommunicationAvailable` overriding
   `AppraisalConfig.DisciplineDefault`/`true`), terrain cells (reusing
   `wall`/`opaqueWall`/`costly`), objective/extraction cells, and a
   per-agent command schedule (`(tick, recipients, PlayerIntent, Urgency,
   RiskTolerance) list` or equivalent, reusing `Command.moveTo`/
   `.moveToMany` construction) — general enough for every one of the
   eleven entries' actual shapes (single order, `exposed-approach`'s two
   same-tick orders to different agents, `reissued-order`'s two orders to
   the same agent on different ticks).
2. Implement the builder function(s): produce the `WorldState` (via
   `RawScenario/Scenario.validate/World.ofScenario`, unchanged) and the
   `RecordedCommand[]` (via the command schedule, assigning `CommandId`/
   `Sequence` the same way `CommandLogFile.parse` does today — order of
   appearance / order within a tick) from one authored value.
3. Rewrite each of the eleven `…World ()` functions as a builder value;
   delete `rawScenario`/`worldOf` once nothing calls them, or fold their
   logic into the builder if still needed internally.
4. Change `Entry` to carry the builder's produced `RecordedCommand[]`
   directly (or a thunk producing it); update `run`/`checkEntry`/
   `regenerateEntry` to use it instead of `loadLog dir e`.
5. Add `.cwreplay` regeneration: extend `regenerateEntry` (or add a
   sibling function) to also write `content/replays/<name>.cwreplay` via
   `ReplaySerialisation.serialise`, with `InitialHash = None`,
   `Checkpoints = [||]` (Decision A); wire it into `cwheadless corpus
   --regenerate`.
6. Delete the eleven now-stale hand-authored `.cwlog` files; commit the
   generated `.cwreplay` files in their place. Leave `spike-fixture.cwlog`
   untouched.
7. Regenerate every migrated entry's `.md` table and confirm byte-identical
   output against the currently committed files (the proof this is a pure
   refactor).
8. Update `content/replays/CORPUS.md` if it names `.cwlog` explicitly for
   the migrated entries.
9. Build, run the full suite, run `-- corpus` and `-- fixture`.
10. Update documentation per "Documentation updates" below.

This is an outcome checklist, not permission to invent scenario content, a
richer command grammar, or new geometry beyond what the eleven existing
entries already need.

## Acceptance criteria

- [ ] One builder in `Corpus.fs` authors deployment/terrain/command
      schedule as a single value; all eleven non-fixture entries use it.
- [ ] No `.cwlog` file is read at runtime by `run`/`checkEntry`/
      `regenerateEntry` for a migrated entry (`loadLog` either removed or
      unused for these entries).
- [ ] Every migrated entry's committed `content/replays/<name>.md` is
      byte-identical before and after the refactor.
- [ ] Every migrated entry has a committed `content/replays/<name>.cwreplay`
      generated by `ReplaySerialisation.serialise`; the corresponding
      `.cwlog` is deleted.
- [ ] `spike-fixture` entry and `spike-fixture.cwlog` untouched.
- [ ] `tests/CommandoWar.Sim.Tests/SimulationTests.fs` untouched (Decision D).
- [ ] No `Canonical.FormatVersion` change; no `src/CommandoWar.Sim/` source
      change.
- [ ] Full test suite green at the same count as before this task
      (refactor only — no new `[<Fact>]` expected unless `CorpusTests.fs`
      needs one for `.cwreplay` regeneration idempotency).
- [ ] Required documentation updated.

## Required verification

- unit tests: `dotnet test CommandoWar.slnx -c Release` — full suite, same
  count as before.
- scenario/replay tests: `dotnet run --project src/CommandoWar.Headless -c
  Release -- corpus` (all 12 entries, including the untouched
  `spike-fixture`); `--regenerate` twice and confirm the second run is a
  no-op diff (idempotency, the TASK-034 precedent).
- build: `dotnet build CommandoWar.slnx -c Release`.
- manual smoke test: diff every migrated `content/replays/<name>.md`
  against its pre-task committed version (must be empty diff); read one
  generated `.cwreplay` (`open-engagement`, since it has both sides
  stationary) and confirm it parses back via `ReplaySerialisation.parse`
  to the same `RecordedCommand[]` the builder produced.
- dependency boundary check: not applicable — no `.fsproj` touched, no
  package reference added.

## Evidence to capture

- `dotnet build` / `dotnet test` output.
- `-- corpus` output before and after, and the `--regenerate` idempotency
  check.
- `git diff --stat content/replays/` showing only `.cwlog` deletions,
  `.cwreplay` additions, and zero changes to any `.md` file.

## Expected files

- `src/CommandoWar.Headless/Corpus.fs`
- `content/replays/*.cwreplay` (new, eleven files)
- `content/replays/*.cwlog` (deleted, eleven files; `spike-fixture.cwlog`
  kept)
- `content/replays/CORPUS.md` (if it names `.cwlog`)
- `tests/CommandoWar.Sim.Tests/CorpusTests.fs` (only if the loading API
  changes shape)
- `docs/12_PROGRESS_LEDGER.md`, `docs/ledger/2026-09-15-TASK-036-*.md`
- `docs/11_BACKLOG.md`, `PROJECT_STATE.yaml`

## Documentation updates

- this task file (status, outcome, evidence);
- `docs/11_BACKLOG.md`: new TASK-036 row, B-049 `proposed -> done`;
- `docs/12_PROGRESS_LEDGER.md`: index row + detail file;
- `PROJECT_STATE.yaml`: `active_work.note` updated; no phase/gate/decision
  change (tooling refactor, not a gameplay system);
- no ADR (no decision an ADR owns — `ReplaySerialisation` was already the
  accepted production format from B-045).

## Rollback or removal

Fully reversible: revert `Corpus.fs`, restore the eleven `.cwlog` files
from git history, delete the `.cwreplay` files. No consumer outside
`Corpus.fs`/`CorpusTests.fs` reads the corpus command files directly.

## Completion report

See the chat response for the AGENTS.md-format completion report
accompanying this task file. Do not start or offer the next task.
