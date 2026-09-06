## 2026-09-06 - Control-plane reconciliation - README and PROJECT_STATE brought into line with actual state

**Owner:** Dave with coding-agent assistance
**Source revision:** `e44986a` (Control-file scope only, no source touched), plus
the same session's Mibo 5.0.0 amendment
(`docs/ledger/2026-09-06-mibo-5-repackaging-recorded.md`)
**Environment:** Windows 11 Pro 26200 (25H2); .NET SDK 10.0.303. Working tree
clean on `main`, up to date with `origin/main` - no divergent active branch to
reconcile, only the documents.
**Status change:** none. No task, phase, gate, or decision changed; this removes
stale text that contradicted the current state.

### Why this change

The root `README.md` still described the project as it stood before the framework
spike: "Current framework decision: pending ADR-0001", "Next controlled task:
`tasks/TASK-001-REPOSITORY-BASELINE.md`", "A graphical framework is deliberately
not selected yet", and an "Immediate sequence" section whose dependency chain
ended at TASK-006. `PROJECT_STATE.yaml`'s `active_work.note` said the next task
was "pending Dave's answer on whether G2 is proven", a question the same file's
`gates` block and `docs/ledger/2026-09-05-G2-gate-decision.md` had already
resolved (G2 passed, phase/gate advanced to P3/G3).

The README's own "Sources of truth" rule ("When files conflict, an accepted ADR
overrides an older design document. The conflict must then be removed in the same
change") and R-015 (coding-agent-driven documentation drift) both call for
clearing this.

### Changes

`README.md`:

- Header block: "framework decision pending" -> Godot decided (ADR-0001);
  "Next controlled task: TASK-001" -> pointer to `PROJECT_STATE.yaml`; status
  line points to `PROJECT_STATE.yaml` for phase and gate rather than restating a
  stale phase.
- "Immediate sequence" section: the pre-spike TASK-001..TASK-006 chain replaced
  with a statement that P0-P2 are complete and G0/G1/G2 passed, a pointer to the
  progress ledger for the task record, and a pointer to `PROJECT_STATE.yaml` /
  `docs/11_BACKLOG.md` for current position; client/presentation work is P4 and
  gated on G3.
- "Architectural position": the present-tense "During the framework spike, two
  disposable presentation hosts exercise the same simulation" rewritten to past
  tense with the ADR-0001 outcome (Godot selected, Mibo host is spike evidence,
  Mibo 5.x is a recorded reconsideration option per the 2026-09-06 ADR-0003
  amendment). `Mibo.Adaptive` exclusion kept, gate named (G5).
- "Repository baseline": "A graphical framework is deliberately not selected yet"
  -> Godot .NET (ADR-0001); the simulation library has no framework dependency.

`PROJECT_STATE.yaml`:

- `project.updated`: `2026-09-05` -> `2026-09-06`.
- `active_work.note`: rewritten. Records that G2 passed 2026-09-05 with the
  ledger reference, that no task is selected, that TASK-020 (B-014) is drafted
  and `ready` pending selection, and the 2026-09-06 Mibo reconsideration option
  (B-043). Framework decision unchanged.
- `candidate_stack_as_researched_2026_09_02` left untouched - it is a
  date-stamped research snapshot, not a live field.

`docs/12_PROGRESS_LEDGER.md`: this index row. No "Pinned facts" value changed.

### Verification

- Manual check: `git status` - clean on `main`, up to date with `origin/main`;
  `PROJECT_STATE.yaml` `active_work.selected_task: none`; `tasks/TASK-020-*.md`
  exists; `docs/11_BACKLOG.md` TASK-020 / B-014 rows are `ready`. The README's
  new claims match all of these.
  - Result: consistent.
- Manual check: re-read the edited `README.md` end to end for internal
  consistency and for any remaining pre-spike reference.
  - Result: none remaining. `Established by TASK-001` in the baseline section is
    correct history and was kept.
- Manual check: `docs/08_ROADMAP_AND_GATES.md` phase/gate names match the README
  text (`P0`-`P6`, `G0`-`G6`; P4 is client integration, gated on G3).
  - Result: consistent.
- Not run: no build or test - documentation only, no source, project, or
  solution file touched.

### Evidence

- `README.md`, `PROJECT_STATE.yaml` diffs.
- `docs/ledger/2026-09-05-G2-gate-decision.md` (the resolved G2 question the old
  note still referenced).

### Deviations and unresolved issues

- `PROJECT_STATE.yaml` `project.status: discovery` was left as-is. It has no
  documented enum and the value predates P1; changing it would be inventing
  vocabulary. If Dave wants a lifecycle value that tracks the phase, that is a
  separate decision.
- There is still no CI workflow; the "171 passing tests" figure is locally
  recorded, not independently reproducible. Out of scope here; tracked as a
  recommendation in the standing review and unaddressed in the backlog (no
  B-item yet).
- The `README` "Immediate sequence" heading is now slightly misnamed (it
  describes completed sequence, not an immediate one). Left rather than
  restructure the section layout in a reconciliation change.

### Documents updated

- `README.md`
- `PROJECT_STATE.yaml`
- `docs/12_PROGRESS_LEDGER.md` (this index row)
- this entry

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: stale-text removal only, triggered by the standing review's
  "control-plane maintenance" section and done alongside the Mibo 5.0.0
  amendment. No task file - not implementation work.
