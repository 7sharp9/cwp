## 2026-09-03 - TASK-009 - Progress ledger restructured into a summary index plus per-entry detail files

**Owner:** Dave with coding-agent assistance
**Source revision:** `2010d07` (Add scenario DTO validation and world builder)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303 (`dotnet --version` = 10.0.303)
**Status change:** `active -> review`

### Changes

- **TASK-008 finalised** (precondition): `tasks/TASK-008-*.md` status
  `review -> done`; `docs/11_BACKLOG.md` TASK-008 row `review -> done` and B-007
  row `active -> done` (task-file link kept); the 2026-09-03 TASK-008 detail
  file Review block `Accepted: pending -> yes (2026-09-03)` with a one-line
  acceptance note. No ADR (docs/03 s8 already places `Scenario.fs` in the sim).
  TASK-008 tests not re-run as part of finalisation.
- **`docs/ledger/` created** with one file per existing ledger entry, named
  `YYYY-MM-DD-<ID>-<slug>.md`, each keeping its original heading as its title.
  Thirteen files: PLAN-001, TASK-001, TASK-002, TASK-003, TASK-004 (Godot
  spike), the two TASK-005 entries (environment check, Mibo spike), the
  TASK-004 interactive addendum, TASK-006 (spikes evaluated), TASK-006
  finalisation, TASK-007 (boundary designed), TASK-007 acceptance, TASK-008.
  The full text of every entry was moved verbatim. The only edits made during
  the move were four directional pointers that the split broke ("see the
  TASK-005 entries below", "the next entry", "the addendum below", 'the
  "TASK-007 acceptance" note below') repointed at the relevant detail file by
  name. No evidence, decision, date, hash, status change, acceptance, or
  recorded deviation was altered.
- **`docs/12_PROGRESS_LEDGER.md` rewritten as the index:** the purpose
  statement and the append-only rule (unchanged in meaning, extended to
  describe the index + detail-file split); a "Pinned facts" quick-reference
  block (SDK, TFM, FSharp.Core, the four format/content version constants, PRNG
  and hash, green test count, accepted ADRs, current gate and phase, and the
  shared-fixture parameters and initial/final hashes); the detail-entry
  template (replacing the old top-of-file template); and a chronological index
  table, one row per detail file (Date, ID, Outcome, Status change, Accepted,
  relative link). 118 lines.
- **`AGENTS.md` updated:** "Required documentation update" and "Required final
  report" now say a completed task adds a row to the
  `docs/12_PROGRESS_LEDGER.md` index and a detail file under `docs/ledger/`,
  and refreshes the "Pinned facts" block if a pinned value changed.
- **`PROJECT_STATE.yaml`:** `active_work.selected_task: TASK-009`,
  `task_file: tasks/TASK-009-PROGRESS-LEDGER-RESTRUCTURE.md`. `sources_of_truth.progress`
  still points at `docs/12_PROGRESS_LEDGER.md` (the index is the entry point).
  Gates, `current_gate` (`G2_deterministic_core_proven`), `current_phase`
  (`P2_deterministic_core`), and `framework_decision` unchanged.
- **`docs/11_BACKLOG.md`:** new "Current work" row TASK-009 (P2, S, deps
  TASK-008, active).
- New `tasks/TASK-009-PROGRESS-LEDGER-RESTRUCTURE.md`.
- No code, test, `.fsproj`, `.slnx`, ADR body, or `content/` file touched;
  `decisions/` and `tasks/` not reorganised.

### Verification

- Per-entry no-loss check: `git show HEAD:docs/12_PROGRESS_LEDGER.md` was split
  on the same `^## \d{4}-\d{2}-\d{2}` entry-heading boundaries and each slice
  compared byte-for-byte against its new `docs/ledger/` file (with the four
  known directional-pointer edits reverted for the comparison).
  - Result: all 13 entries byte-identical. A prior check also confirmed the
    concatenation of the 13 files equals the original file from its first entry
    heading to EOF (exact match).
- Command: `grep -rn "12_PROGRESS_LEDGER\|docs/ledger" docs/ decisions/ tasks/ AGENTS.md README.md PROJECT_STATE.yaml`
  - Result: every hit resolves. References to `docs/12_PROGRESS_LEDGER.md`
    resolve to the index (still present at that path). ADR-0001 and
    `content/fixtures/SPIKE-FIXTURE.md` references (ADR body / `content/`, both
    out of edit scope) point at `docs/12_PROGRESS_LEDGER.md` or "the ledger"
    generally and resolve to the index. Prose in done-task files naming "the
    TASK-NNN ledger entry" resolves through the matching index row; left as
    prose per the task's "confirm the index row is enough" option.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed! - Failed: 0, Passed: 79, Skipped: 0, Total: 79` (untouched;
    sanity only).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  - Result: initial hash `0xF2F3DF0D820AD9AC`, final tick 40 hash
    `0x838D3AE7DBFB735D` (format 1), 33 events (unchanged).
- Index length: 118 lines (under ~150); every index row links to a file that
  exists under `docs/ledger/`.
- Command: `git status --porcelain`
  - Result: modified `AGENTS.md`, `PROJECT_STATE.yaml`, `docs/11_BACKLOG.md`,
    `docs/12_PROGRESS_LEDGER.md`, `tasks/TASK-008-SCENARIO-DTO-AND-CONTENT-VERSION.md`;
    new `docs/ledger/*.md` (14 files), `tasks/TASK-009-PROGRESS-LEDGER-RESTRUCTURE.md`.
    No source, test, `.fsproj`, `.slnx`, ADR body, or `content/` change.

### Evidence

- Detail files: `docs/ledger/2026-09-02-PLAN-001-initial-control-pack.md`,
  `...TASK-001-repository-baseline.md`, `...TASK-002-simulation-skeleton.md`,
  `...TASK-003-determinism-harness.md`, `...TASK-004-godot-spike.md`,
  `...TASK-005-mibo-environment-check.md`, `...TASK-005-mibo-spike.md`,
  `2026-09-03-TASK-004-interactive-addendum.md`,
  `...TASK-006-spikes-evaluated.md`, `...TASK-006-finalisation.md`,
  `...TASK-007-godot-fsharp-boundary.md`, `...TASK-007-acceptance.md`,
  `...TASK-008-scenario-dto.md`, `...TASK-009-ledger-restructure.md` (this
  file).
- Index: `docs/12_PROGRESS_LEDGER.md` (purpose + append-only rule, "Pinned
  facts", detail-entry template, 14-row index table).

### Deviations and unresolved issues

- The four directional pointers listed under Changes were the only in-move
  edits. Each was a "see X below / next / addendum" phrase made wrong by the
  split; each now names the target detail file. No factual content changed.
- Prose references in `decisions/ADR-0001-FRAMEWORK-SELECTION.md` and
  `content/fixtures/SPIKE-FIXTURE.md` to "the ledger" / "the TASK-004 ledger"
  were not edited (ADR body and `content/` are out of edit scope). They
  resolve to the index, which resolves onward to the detail file via the
  matching row.
- Prose in completed task files (e.g. `tasks/TASK-007`, `tasks/TASK-008`
  required-reading lists) still says "the TASK-NNN ledger entry in
  `docs/12_PROGRESS_LEDGER.md`". Left as prose: the index row is the sanctioned
  resolution and rewriting done-task files is out of scope for this refactor.
- `sources_of_truth.progress` deliberately unchanged: the index is the entry
  point to the ledger.
- Gate, phase, and `framework_decision` unchanged: this is a documentation
  refactor and moves nothing.

### Documents updated

- `tasks/TASK-009-PROGRESS-LEDGER-RESTRUCTURE.md` (new; status `review`)
- `tasks/TASK-008-SCENARIO-DTO-AND-CONTENT-VERSION.md` (`review -> done`)
- `docs/12_PROGRESS_LEDGER.md` (rewritten as the index)
- `docs/ledger/*.md` (13 entry files moved verbatim + this entry)
- `AGENTS.md` ("Required documentation update", "Required final report")
- `docs/11_BACKLOG.md` (TASK-008 / B-007 rows `-> done`; TASK-009 row added)
- `PROJECT_STATE.yaml` (`active_work` -> TASK-009)

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: documentation-only restructure; no evidence, decision, date, hash, or
  acceptance of any past entry altered; 79 tests green and fixture hashes
  unchanged as a sanity check.
