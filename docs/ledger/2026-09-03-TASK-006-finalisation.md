## 2026-09-03 - TASK-006 finalisation - ADR-0001 accepted (Godot), G1 passed, Mibo route retired

**Owner:** Dave with coding-agent assistance
**Source revision:** `aa48abc` (Add Mibo framework spike and evidence)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303
**Status change:** ADR-0001 `proposed -> accepted`; `gates.G1_framework_selected`
`in_progress -> passed`; `current_gate` `G1 -> G2_deterministic_core_proven`;
`current_phase` `P1_framework_selection -> P2_deterministic_core`; TASK-006
`active -> done`

### Trigger

Dave accepted the TASK-006 decision matrix and the Godot recommendation in this
session, and named keeping the C#/F# boundary low-impedance as the priority for
follow-on work. `docs/08_ROADMAP_AND_GATES.md` section 4 G1 evidence is
satisfied: both candidates ran the same simulation build, rendered six agents on
an isometric map, submitted the same typed command against the same snapshot,
displayed tick and hash, the content-edit workflow was measured (editor-GUI
iteration recorded as a known limitation and review trigger), build / packaging /
debugger / glue-code observations are recorded, and ADR-0001 selects one route.

### Changes

- `decisions/ADR-0001-FRAMEWORK-SELECTION.md`: status `accepted`, Date accepted
  2026-09-03, `Selected candidate: A. Godot .NET plus F# simulation`,
  "Decision" section written, "Qualitative decision" block finalised (was a
  pending recommendation), a named follow-up added for the low-impedance C#/F#
  boundary, "Pinned versions" finalised, "Consequences after acceptance"
  outcomes recorded.
- `PROJECT_STATE.yaml`: `framework_decision:
  godot_dotnet_fsharp_sim (ADR-0001, accepted 2026-09-03)`;
  `gates.G1_framework_selected: passed`;
  `current_gate: G2_deterministic_core_proven`;
  `current_phase: P2_deterministic_core`; `active_work.selected_task: none`
  with a note pointing at the next-session prompt (leading candidate: the
  boundary-design task; then B-007 onward for G2).
- `docs/02_TECHNOLOGY_DECISION.md`: provisional "do not commit the production
  client yet" summary replaced with the recorded decision; the pre-spike
  analysis retained as context, marked non-authoritative.
- `decisions/ADR-0003-MIBO-ADOPTION.md`: status line updated; "2026-09-03 note:
  Mibo not adopted" section added (removal from default build/CI, headless stays
  on `cwheadless`, Mibo.Adaptive prohibition unaffected, spike evidence
  preserved, reopening needs a new ADR).
- `src/CommandoWar.Client.Mibo/README.md`: "Rejected route (2026-09-03)" note
  at the top.
- `docs/10_RISK_REGISTER.md`: R-002 and R-005 `-> closed` (the controlled spike
  and the weighted evidence addressed the "language preference" and
  "engine-building" risks; Godot was R-005's own contingency); R-003
  (Mibo churn) `-> closed` (Mibo is no longer a project dependency); R-004
  (Godot C#/F# boundary friction) `-> mitigating` with the boundary-design task
  named as mitigation and the Mibo contingency removed.
- `docs/11_BACKLOG.md`: TASK-006 `active -> done`.
- `tasks/TASK-006-FRAMEWORK-DECISION.md`: all acceptance criteria checked;
  status `done`.

### Verification

- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed! - Failed: 0, Passed: 54` (unchanged; documentation-only
    session, no source touched).
- Command: `git status --porcelain`
  - Result: only documentation and the Mibo README changed; no source, test,
    fixture, `.slnx`, or spike code file modified.
- Manual check: internal references in the edited docs resolve (ADR-0001 <->
  ADR-0002 <-> ADR-0003, `docs/02`, `docs/08` section 4, `PROJECT_STATE.yaml`,
  backlog, ledger). `framework_decision`, gate, and phase now agree across
  `PROJECT_STATE.yaml`, ADR-0001, `docs/02`, and this ledger.

### Deviations and unresolved issues

- **Godot self-contained packaging remains blocked** (absent `4.7.2.stable.mono`
  export templates; no committed `export_presets.cfg`). In-session attempt
  2026-09-03: `--export-release` stops at "no `export_presets.cfg`", and the
  template directory is present but empty, so a real export cannot complete
  regardless. Accepted weakness 2 in ADR-0001; review trigger 2; unblock steps
  in the TASK-004 ledger entry. The app runs outside the editor via `--path`.
- **The Godot editor-iteration loop is still unmeasured on this project.**
  Accepted weakness 1; review trigger 1 requires the first Bridgehead authoring
  task (B-025) to record a real editor edit->visible-result measurement.
- **The low-impedance C#/F# boundary is a named open design task**, to run
  before P4 client work. It may produce ADR-0004 or an ADR-0002 amendment. It
  does not reopen ADR-0001.
- The finalisation was applied on Dave's in-session verbal acceptance; the
  edits are documentation and are open to correction on review.
- No P2 task was activated. The next task is Dave's to select.

### Documents updated

- `decisions/ADR-0001-FRAMEWORK-SELECTION.md`, `decisions/ADR-0003-MIBO-ADOPTION.md`
- `docs/02_TECHNOLOGY_DECISION.md`, `docs/10_RISK_REGISTER.md`,
  `docs/11_BACKLOG.md`
- `PROJECT_STATE.yaml`
- `src/CommandoWar.Client.Mibo/README.md`
- `tasks/TASK-006-FRAMEWORK-DECISION.md`
- `docs/12_PROGRESS_LEDGER.md` (this entry)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-03)
- Notes: Next session continues with the low-impedance C#/F# Godot boundary.
  See the next-session prompt. Accepted 2026-09-03: the framework decision,
  G1, ADR-0001, and every task since (TASK-007 through TASK-010) are built on
  these finalisation edits, so they are accepted as a body.
