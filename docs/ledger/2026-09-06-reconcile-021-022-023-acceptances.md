## 2026-09-06 - Reconcile 021/022/023 acceptances - control plane

**Owner:** Dave with coding-agent assistance
**Source revision:** `019e4e6` (Merge pull request #1 from
7sharp9/task-023-simulation-ci) — local tip of `main`; work done on branch
`task-024-025-026-drafts`
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303
**Status change:** TASK-021 / TASK-022 / TASK-023 acceptance state
`pending -> accepted (2026-09-06)`; `docs/11_BACKLOG.md` B-046 / B-047 / B-048
`-> done`; `PROJECT_STATE.yaml` `active_work.selected_task TASK-023 -> none`,
`task_file -> null`. No source, no gate change.

### Why

Dave accepted TASK-021 (backlog B-046, passable move-cost bounds), TASK-022
(B-047, runtime cell-occupancy correctness), and TASK-023 (B-048,
framework-neutral CI). TASK-023 was pushed and merged to `main` as PR #1
(commits `3ee4090` workflow, `019e4e6` merge); its first CI run on a real
`windows-latest` runner was green, which discharges the acceptance criterion
the drafting session could not (it could not push). The control plane still
recorded all three as `review` / pending, and `PROJECT_STATE.yaml` still
named TASK-023 as the selected task. This entry brings the control documents
into line and clears the active-work slot before Dave selects the next task.

### Changes

- `tasks/TASK-021-MOVEMENT-COST-BOUNDS.md` — `Status:` `done (..., pending
  Dave's acceptance)` -> `done (2026-09-06, accepted by Dave)`.
- `tasks/TASK-022-CELL-OCCUPANCY.md` — `Status:` `review (..., pending Dave's
  acceptance)` -> `done (2026-09-06, accepted by Dave)`.
- `tasks/TASK-023-SIMULATION-CI.md` — the multi-line `review` status replaced
  with `done (2026-09-06, accepted by Dave; merged via PR #1 ...)`; the two
  green-run / red-run acceptance boxes moved from `[~]` (not executed here) to
  `[x]` (confirmed on PR #1's `windows-latest` run; the red-run configuration
  is byte-identical to the one exercised locally).
- `docs/ledger/2026-09-06-TASK-021-movement-cost-bounds.md`,
  `-TASK-022-cell-occupancy.md`, `-TASK-023-simulation-ci.md` — `### Review`
  blocks: `Accepted: pending` -> `Accepted: yes (2026-09-06)`, each with a
  note pointing here and recording the still-open out-of-scope items
  (TASK-021: `NegativeMoveCost` kept, the map-area / `int64`-path-cost review
  points; TASK-022: the modified `routes around an impassable wall` fact, the
  `07af43a` mid-implementation commit, the out-of-scope `07af43a` review
  findings; TASK-023: PR #1 merge, the `revert-1-task-023-simulation-ci`
  branch that was not merged).
- `docs/12_PROGRESS_LEDGER.md` — TASK-021 / TASK-022 / TASK-023 index rows:
  `Accepted` cell `pending` -> `yes (2026-09-06)`; the status-change cell for
  each gains `-> done` (021 and 022 per the reconciliation instruction; 023
  too, for consistency with its new `Accepted: yes` — its cell also updates
  `B-048 ready -> review` to `ready -> done`). "Pinned facts" unchanged (no
  pinned value moved: `Green tests` `194`, `Canonical.FormatVersion` `2`,
  gate `G3` pending all stand).
- `docs/11_BACKLOG.md` — section 2: TASK-022 row `review -> done`, TASK-023
  row `review -> done`. Section 3: B-047 `review -> done`, B-048
  `review -> done`. (B-046 and the TASK-021 row were already `done`.)
- `PROJECT_STATE.yaml` — `active_work.selected_task: none`,
  `task_file: null`; `active_work.note` rewritten to a clean current-state
  summary (021/022/023 accepted; TASK-024/025/026 drafted this session;
  nothing selected; recommended selection order); `project.updated` refreshed
  and a new `active_work.updated: 2026-09-06` line added (the "two updated
  lines").

### Verification

- `git status --porcelain` — only control documents and `tasks/` files
  touched; no source, `.fsproj`, test, `content/`, or `.github/` file.
- No build or test run: this entry records acceptances and rewrites control
  prose only. The green-test figure (`194`), the corpus (7 entries), and the
  fixture are as TASK-023's ledger recorded them and as CI verifies on every
  push; nothing in this session changes them.
- `docs/08_ROADMAP_AND_GATES.md` section 6 P3 Required work re-read: it
  already carries the "resolve issue-tick / submission-tick semantics (B-044)
  and the production replay-command serialisation (B-045)" bullet (added by
  the 2026-09-06 TASK-020 revision). CI already lives in `docs/09` section 6.
  **No `docs/08` edit is needed** and none was made.

### Evidence

- `git log --oneline`: `019e4e6` merge, `3ee4090` TASK-023 workflow,
  `37824e8` cell-occupancy, `07af43a` movement obstruction, `f94fedf` accept
  TASK-020, `ac0bd67` command validation + cost bounds.
- `git branch -a`: `main`, `task-023-simulation-ci` (local),
  `remotes/origin/main`, `remotes/origin/revert-1-task-023-simulation-ci`
  (unmerged), and this session's `task-024-025-026-drafts`.

### Deviations and unresolved issues

- The `docs/12` TASK-023 row's status-change cell was updated beyond the
  literal instruction (which named only 021 and 022 for the `review -> done`
  edit): appending `-> done` and changing its `B-048 ready -> review`
  sub-clause to `ready -> done` keeps the row internally consistent with its
  new `Accepted: yes`. Flagged here.
- `PROJECT_STATE.yaml` had only one `updated:` line before this session (on
  `project`). A second `updated: 2026-09-06` was added under `active_work` to
  satisfy "refresh the two 'updated' lines" and to give the active-work block
  its own timestamp.
- The out-of-scope standing-review findings against TASK-020 / TASK-021 that
  the TASK-022 `07af43a` review raised (partial `PlayerCommand.Agent`
  accessor; commandless-tick fast path; map-area bound / `int64` path cost;
  `ScenarioContent.Version` policy) are **not** triaged here — they remain
  for Dave to convert to backlog rows.
- `revert-1-task-023-simulation-ci` exists on the remote but was not merged;
  the CI workflow stands on `main`. Not acted on.

### Documents updated

- `tasks/TASK-021-MOVEMENT-COST-BOUNDS.md`,
  `tasks/TASK-022-CELL-OCCUPANCY.md`, `tasks/TASK-023-SIMULATION-CI.md`
- `docs/ledger/2026-09-06-TASK-021-movement-cost-bounds.md`,
  `docs/ledger/2026-09-06-TASK-022-cell-occupancy.md`,
  `docs/ledger/2026-09-06-TASK-023-simulation-ci.md`
- `docs/11_BACKLOG.md` (sections 2 and 3)
- `docs/12_PROGRESS_LEDGER.md` (three index rows + this row)
- `PROJECT_STATE.yaml`
- this entry

### Review

- Reviewer: Dave
- Accepted: n/a (this entry records Dave's acceptances of TASK-021 / TASK-022
  / TASK-023; it makes no new technical decision)
- Notes: paired with the three `2026-09-06-TASK-02{4,5,6}-drafted.md` entries
  from the same session.
