## 2026-09-16 - G3 gate decision - command loop proven headless, ambiguity resolved

**Owner:** Dave with coding-agent assistance
**Source revision:** `bd385fb` (Implement and accept TASK-038: canonical refusal-and-correction scenario (B-023))
**Environment:** Windows 11 Pro 26200 (25H2); .NET SDK 10.0.303
**Status change:** `gates.G3_command_loop_proven_headless` `pending -> passed`;
`current_gate` `G3_command_loop_proven_headless -> G4_vertical_slice_feature_complete`;
`current_phase` `P3_command_loop_headless -> P4_playable_integration`

### Precondition: TASK-038 accepted

TASK-038 (the canonical refusal-and-correction scenario, backlog B-023 — G3's
headline evidence item) was implemented, self-verified, and accepted by Dave
this session (see `docs/ledger/2026-09-16-TASK-038-canonical-refusal-and-
correction.md`), landing the last of `docs/08` section 6's G3 evidence
bullets.

### The ambiguity

`docs/08_ROADMAP_AND_GATES.md` section 6 listed six G3 evidence bullets (the
canonical refusal test passes; at least two agents appraise the same intent
differently for inspectable reasons; suppression or a route change reverses
one outcome predictably; no broad utility system, general planner, or
framework callback controls cognition; scenario traces are readable enough
to diagnose all decisions; repeated headless runs produce stable evidence) —
every one of which now has landed evidence (TASK-028's `exposed-approach`,
TASK-037's `suppress-relieves-exposure`, TASK-038's `canonical-refusal-and-
correction`, and the whole corpus/replay determinism machinery since
TASK-003/016). The same section's Required work list also named "the five
vertical-slice intents" (`docs/05` section 4: Move, Hold, Suppress, Assault,
Withdraw) — `PlayerIntent` has only `MoveTo` and `Suppress`; `Hold`,
`Assault`, and `Withdraw` are B-030 proper, explicitly re-scoped to P4/G4 by
TASK-037's own backlog row ("B-030 proper... stays P4"). It was unclear
whether "the five vertical-slice intents" was a hard G3 blocker or a
forward-reference to P4 scope already agreed at TASK-037 — the exact shape
of the G2 "tactical knowledge" ambiguity — and this was put to Dave directly
via `AskUserQuestion` rather than resolved by inference.

### Decision

Dave chose: G3 passes now, on the six evidence bullets alone. `docs/08`
section 6's Required work list is corrected — a dated resolution note added
explaining why "the five vertical-slice intents" is not a literal blocker: no
G3 evidence bullet names or depends on the missing three intents, and the
one evidence item that does depend on an intent (the canonical refusal test)
needs only `MoveTo`/`Suppress`, both of which exist. `current_gate` and
`current_phase` advance to G4 / P4. Dave separately confirmed the next step:
scope the first P4 client task.

### Rejected alternative

Hold G3 open and treat "the five vertical-slice intents" as a literal
requirement, building B-030 proper (`Hold`/`Assault`/`Withdraw`, an L-sized
task) before declaring the gate. Not taken: no G3 evidence bullet names or
depends on the missing three intents, and TASK-037's own backlog row already
recorded B-030 proper as P4/G4 scope, confirmed with Dave at that time —
holding G3 open for it now would relitigate a decision already made.

### Changes

- `docs/08_ROADMAP_AND_GATES.md`: section 6 Required work list corrected
  (dated resolution note on "the five vertical-slice intents"); no bullet
  text removed (unlike the G2 precedent), since the intents genuinely are
  not all built — the note narrows scope rather than striking the line.
- `PROJECT_STATE.yaml`: `gates.G3_command_loop_proven_headless: passed`;
  `current_gate: G4_vertical_slice_feature_complete`; `current_phase:
  P4_playable_integration`; `active_work.selected_task: none` (task
  selection deferred to the next session action); `updated: 2026-09-16`.
- `docs/12_PROGRESS_LEDGER.md`: "Pinned facts" Current gate / Current phase
  rows refreshed; this index row.

### Verification

- Manual check: every G3 evidence bullet in `docs/08` section 6 cross-checked
  against a landed task — canonical refusal test passes (TASK-038
  `canonical-refusal-and-correction`); two agents appraise the same intent
  differently for inspectable reasons (TASK-028 `exposed-approach`, the G3
  divergence); suppression/route change reverses an outcome predictably
  (TASK-037 `suppress-relieves-exposure`, TASK-038's automatic reappraisal +
  reissue); no broad utility system/general planner/framework callback
  controls cognition (the staged `Appraisal.appraise` pipeline, unchanged in
  shape since TASK-028, `AGENTS.md`'s prohibited-scope list enforced by every
  task's source scan since); scenario traces are readable enough to diagnose
  all decisions (the `DiagnosticFrame`/`DiagnosticRender` ASCII/SVG/HTML
  renderers and the committed golden corpus, TASK-011 onward); repeated
  headless runs produce stable evidence (`cwheadless corpus --regenerate`
  run twice byte-identical on every corpus-touching task since TASK-016).
- Command: `git status --porcelain`
  - Result: only the control files listed above changed; no source, test,
    fixture, or content file touched by this decision.

### Deviations and unresolved issues

- No task was activated in this session. Backlog section 7's selection rule
  is not yet satisfied for any P4 item — scoping the first P4 client task is
  the expected next step, not started here.
- `Hold`/`Assault`/`Withdraw` (B-030 proper) remain unbuilt, tracked at P4/G4
  as already recorded in the backlog; this decision does not change B-030's
  scope or status, only confirms it was never a G3 blocker.

### Documents updated

- `docs/08_ROADMAP_AND_GATES.md` (section 6)
- `PROJECT_STATE.yaml`
- `docs/12_PROGRESS_LEDGER.md` (Pinned facts; this index row)
- this entry

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-16)
- Notes: G3 passed on the evidence-bullets reading; the five-intents line
  confirmed as partly P4 scope (B-030 proper). Next: scope the first P4
  client task.
