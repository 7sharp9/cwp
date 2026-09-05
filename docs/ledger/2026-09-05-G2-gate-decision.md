## 2026-09-05 - G2 gate decision - deterministic core proven, ambiguity resolved

**Owner:** Dave with coding-agent assistance
**Source revision:** `3bc9cd9` (Add FsCheck determinism property tests)
**Environment:** Windows 11 Pro 26200 (25H2); .NET SDK 10.0.303
**Status change:** `gates.G2_deterministic_core_proven` `pending -> passed`;
`current_gate` `G2_deterministic_core_proven -> G3_command_loop_proven_headless`;
`current_phase` `P2_deterministic_core -> P3_command_loop_headless`

### Precondition: TASK-018 and TASK-019 finalised

Both were left `review` at the end of their own sessions. TASK-019 was
accepted explicitly this session ("look good"); TASK-018 had never been
formally closed out and was finalised now on the same evidence, following
the TASK-006/TASK-007 acceptance precedent. For each: task file
`review -> done`; ledger detail file `Review > Accepted: pending -> yes
(2026-09-05)`; ledger index row status extended to `-> done`, `Accepted ->
yes`. `docs/11_BACKLOG.md`: TASK-018 row and B-011c row `active -> done`;
TASK-019 row `review -> done` (B-012b, which TASK-019 narrows but does not
realise, stays `proposed` — it now tracks component subhashes only, with no
design surface or concrete need). `git status` scope checked: control-file
edits only (`PROJECT_STATE.yaml`, `docs/11_BACKLOG.md`,
`docs/12_PROGRESS_LEDGER.md`, the two ledger detail files, the two task
files).

### The ambiguity

`docs/08_ROADMAP_AND_GATES.md` section 5 listed six G2 evidence bullets
(deterministic unit/property tests pass; replay reproduces canonical
hashes; a divergence report identifies the first mismatching tick and
section; no wall-clock or framework random source affects authoritative
state; the 50-agent synthetic scene meets budget; the client stays
replaceable) — every one of which now has landed evidence (TASK-003,
TASK-014, TASK-016, TASK-019). The same section's "Required work" list also
named "tactical knowledge," which realises as Perception (B-015), a P3 item
and still a no-op phase in `Simulation.step`. Section 6's own P3 Required
work list separately repeats "shared squad tactical knowledge" under
B-015/B-016. It was unclear whether the P2 "tactical knowledge" line was a
hard G2 blocker or a forward-reference that belonged to P3, and this was put
to Dave directly rather than resolved by inference.

### Decision

Dave chose: G2 passes now, on the six evidence bullets alone. `docs/08`
section 5's Required work list is corrected — "tactical knowledge" removed
from the P2 list, with a dated resolution note explaining why (no G2 evidence
bullet exercises it; Perception/B-015 is P3 scope; section 6 already names
the same concept under P3's own Required work). `current_gate` and
`current_phase` advance to G3 / P3.

### Rejected alternative

Treat "tactical knowledge" as a literal G2 blocker and land a minimal
Perception task (`Sight.trace`/`Sight.visible` wired into the Perception
phase, no shared-knowledge propagation) before selecting a P3 task. Not
taken: no G2 evidence bullet names or depends on tactical knowledge, and
Perception is already tracked as P3's own first item (B-015) with its own
dependency (B-009, done) and required work.

### Changes

- `docs/08_ROADMAP_AND_GATES.md`: section 5 Required work list corrected
  (tactical knowledge removed); dated resolution note added.
- `PROJECT_STATE.yaml`: `gates.G2_deterministic_core_proven: passed`;
  `current_gate: G3_command_loop_proven_headless`; `current_phase:
  P3_command_loop_headless`; `active_work.selected_task: none` (task
  selection deferred to the next session action, per the recommended next
  task below); `updated: 2026-09-05`.
- `docs/12_PROGRESS_LEDGER.md`: "Pinned facts" Current gate / Current phase
  rows refreshed; this index row.
- Precondition finalisation of TASK-018 / TASK-019 (see above).

### Verification

- Manual check: every G2 evidence bullet in `docs/08` section 5 cross-checked
  against a landed task — deterministic unit/property tests (TASK-003
  `CanonicalHashTests`/`FixtureTests`, TASK-019 `DeterminismPropertyTests`);
  replay reproduces canonical hashes (TASK-003 `Replay`, TASK-016 corpus,
  TASK-018 re-pin); divergence report identifies first mismatching tick/section
  (TASK-003 `Divergence.compare`, TASK-016 `Canonical.firstDifferingSection`);
  no wall-clock/framework random source (TASK-003 `AGENTS.md` determinism
  rules, enforced by every task's source scan since); 50-agent synthetic scene
  meets budget (TASK-014 `content/benchmarks/BASELINE.md`); client stays
  replaceable (ADR-0002, unchanged by any P2 task; no client-only type ever
  entered `CommandoWar.Sim`).
- Command: `git status --porcelain`
  - Result: only the control files listed above changed; no source, test,
    fixture, or content file touched by this decision.

### Deviations and unresolved issues

- No task was activated in this session. Backlog section 7's selection rule
  (all dependencies done, a complete task file exists) is not yet satisfied
  for any P3 item — a task file for the next P3 unit of work is the expected
  next step, not started here.

### Documents updated

- `docs/08_ROADMAP_AND_GATES.md` (section 5)
- `PROJECT_STATE.yaml`
- `docs/12_PROGRESS_LEDGER.md` (Pinned facts; this index row)
- `docs/11_BACKLOG.md`, `tasks/TASK-018-SUBCELL-MOVEMENT-PROGRESS.md`,
  `tasks/TASK-019-DETERMINISM-PROPERTY-TESTS.md`,
  `docs/ledger/2026-09-04-TASK-018-subcell-movement-progress.md`,
  `docs/ledger/2026-09-05-TASK-019-determinism-property-tests.md`
  (TASK-018/TASK-019 finalisation, precondition to this decision)
- this entry

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-05)
- Notes: G2 passed on the evidence-bullets reading; tactical knowledge
  confirmed as P3 scope (B-015/B-016). Next: draft a task file for B-014
  (command validation and recipient selection) as the first P3 unit of work.
