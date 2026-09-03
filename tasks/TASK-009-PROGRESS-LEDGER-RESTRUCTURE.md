# TASK-009: Restructure the progress ledger into a summary index plus per-entry detail files

Status: review
Owner: Dave
Phase: P2
Gate: G2 (deterministic core); documentation refactor, moves no gate
Size: S

## Objective

Split `docs/12_PROGRESS_LEDGER.md` (one long append-only entry per task, ~1600
lines) so a session loads a short index by default and reads full detail only
when it needs a specific entry. The governing constraint: no recorded evidence,
decision, date, hash, status change, acceptance, or deviation is lost or
altered.

## Why this task exists

The ledger only grows, one long entry per task. A session that needs one fact
(the current test count, a format version, whether an ADR is accepted) pays for
the whole file. Doing the restructure now, before the substantive P2 backlog
(B-008 onward), is the cheaper order.

## Required reading

1. `PROJECT_STATE.yaml`, `AGENTS.md` ("Required documentation update",
   "Required final report")
2. `docs/12_PROGRESS_LEDGER.md` in full
3. Every file that names the ledger or a specific entry: grep `docs/`,
   `decisions/`, `tasks/`, `README.md`, `PROJECT_STATE.yaml` for
   `12_PROGRESS_LEDGER`, `PROGRESS_LEDGER`, `ledger entry`, `progress ledger`
4. `decisions/` and `tasks/` as the naming precedent (one file per ADR, one
   per task)

## Dependencies

- TASK-008 accepted and `done`.

## Allowed scope

- create `docs/ledger/` and one file per existing ledger entry;
- rewrite `docs/12_PROGRESS_LEDGER.md` as the index;
- update `AGENTS.md` "Required documentation update" and "Required final
  report";
- fix prose references so "the TASK-NNN ledger entry" resolves;
- the required control-document updates (this task, backlog,
  `PROJECT_STATE.yaml`);
- finalise TASK-008 (task file, backlog rows, its ledger entry Review block).

## Forbidden scope

- changing the wording, evidence, hashes, dates, status changes, or acceptance
  decisions of any past entry;
- deleting or summarising away any recorded deviation or unresolved issue (they
  move verbatim into the detail files);
- touching any code, test, `.fsproj`, `.slnx`, ADR body, or `content/`;
- reorganising `decisions/` or `tasks/`;
- changing `PROJECT_STATE.yaml` `sources_of_truth.progress` (it stays pointing
  at `docs/12_PROGRESS_LEDGER.md`, the index);
- destructive git.

## Required work

1. Create `docs/ledger/` with one file per existing ledger entry, named
   `YYYY-MM-DD-<ID>-<slug>.md`. The two TASK-005 entries and the TASK-004
   interactive addendum each get their own file. Move the full current content
   of each entry verbatim; the only permitted edits are fixing an internal
   pointer the move breaks. Each detail file keeps its existing heading as its
   title.
2. Rewrite `docs/12_PROGRESS_LEDGER.md` as the index: the purpose statement and
   the append-only rule (unchanged in meaning); a "Pinned facts"
   quick-reference block (SDK and TFM; `Canonical.FormatVersion` /
   `Replay.FormatVersion` / `CommandLog.Version` / `ScenarioContent.Version`;
   the shared fixture parameters and initial/final hashes; the current green
   test count; the accepted ADRs and current gate/phase); the detail-entry
   template; a chronological index table (Date, ID, Outcome, Status change,
   Accepted, Detail link), one row per detail file.
3. Update `AGENTS.md`: a completed task adds a row to the
   `docs/12_PROGRESS_LEDGER.md` index and a detail file under `docs/ledger/`,
   and refreshes "Pinned facts" if a pinned value changed.
4. Update every prose reference so "the TASK-NNN ledger entry" resolves: link
   the detail file, or confirm the index row is enough and leave the prose. No
   dangling references.
5. Leave `PROJECT_STATE.yaml sources_of_truth.progress` on
   `docs/12_PROGRESS_LEDGER.md`.

## Acceptance criteria

- [x] `docs/ledger/` holds one file per pre-existing entry (13 files), each the
      verbatim entry text with its original heading, plus this task's own entry.
- [x] Per-entry no-loss check performed: each detail file's body equals the
      text lifted from the old ledger. Method and result recorded in the
      TASK-009 detail file.
- [x] `docs/12_PROGRESS_LEDGER.md` is the index: purpose + append-only rule,
      "Pinned facts", detail-entry template, chronological index table. Under
      ~150 lines; every row links to a file that exists.
- [x] `AGENTS.md` "Required documentation update" and "Required final report"
      describe the index-row + detail-file split.
- [x] `grep -rn "12_PROGRESS_LEDGER\|docs/ledger" docs/ decisions/ tasks/ AGENTS.md README.md PROJECT_STATE.yaml`
      — every hit resolves to an existing file or anchor.
- [x] `PROJECT_STATE.yaml sources_of_truth.progress` unchanged.
- [x] `dotnet test CommandoWar.slnx -c Release` = `Passed: 79` (untouched;
      sanity only). `cwheadless fixture` initial `0xF2F3DF0D820AD9AC` / final
      `0x838D3AE7DBFB735D` unchanged.
- [x] `git status` shows only `docs/`, `AGENTS.md`, `PROJECT_STATE.yaml`,
      `tasks/TASK-008-*.md`, `tasks/TASK-009-*.md` changed; no source, test, or
      ADR-body change.
- [x] TASK-008 finalised: task file and backlog rows `-> done`, its ledger
      entry Review `Accepted: pending -> yes (2026-09-03)`.

## Required verification

- `git show HEAD:docs/12_PROGRESS_LEDGER.md` sliced on entry-heading boundaries
  and compared byte-for-byte against each `docs/ledger/` file;
- `grep -rn "12_PROGRESS_LEDGER\|docs/ledger" ...` — all hits resolve;
- index line count;
- `dotnet test CommandoWar.slnx -c Release`;
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`;
- `git status --porcelain`.

## Evidence to capture

- the no-loss comparison method and its result;
- the grep output showing every reference resolves;
- index line count and the row-to-file mapping;
- test summary and fixture hashes;
- `git status` scope.

## Expected files

- `docs/ledger/*.md` (one per entry);
- `docs/12_PROGRESS_LEDGER.md` (rewritten);
- `AGENTS.md`, `docs/11_BACKLOG.md`, `PROJECT_STATE.yaml`,
  `tasks/TASK-008-*.md`, `tasks/TASK-009-*.md`.

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md` (TASK-009 row; TASK-008 / B-007 rows `-> done`);
- `docs/12_PROGRESS_LEDGER.md` index row + `docs/ledger/2026-09-03-TASK-009-ledger-restructure.md`;
- `PROJECT_STATE.yaml` `active_work`;
- `AGENTS.md`.

## Rollback or removal

The restructure is reversible by concatenating the `docs/ledger/` files back in
chronological order under the original preamble. No code or test depends on the
ledger's structure.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Alternative

Skip the restructure and select B-008 (terrain grid, elevation, passability,
directional cover; depends on B-007, needs a task file written first). The
ledger only grows, so TASK-009 now is the cheaper order, but B-008 is the
substantive next step and is not blocked by it.
