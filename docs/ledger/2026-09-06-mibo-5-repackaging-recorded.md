## 2026-09-06 - ADR-0003 amendment - Mibo 5.0.0 recorded as a live reconsideration option

**Owner:** Dave with coding-agent assistance
**Source revision:** `e44986a` (Control-file scope only, no source touched)
**Environment:** Windows 11 Pro 26200 (25H2); .NET SDK 10.0.303. Outbound package
restore and repository clone are unavailable in this session, so nothing was
restored or built against Mibo 5.x; the finding rests on the published changelog.
**Status change:** ADR-0003 `spike complete; not adopted` -> same, plus a
2026-09-06 amendment; ADR-0001 gains a 2026-09-06 note; R-003 `closed -> watch`;
backlog B-043 created (`proposed`). `PROJECT_STATE.yaml` `framework_decision`
unchanged.

### Why this change

Dave flagged the Mibo 5.0.0 release (2026-09-04) and asked that it be recorded as
an option now, observing that the Godot client work has not materially progressed
since ADR-0001 was accepted. Mibo 5.0.0 re-separates the classic MVU runtime from
the adaptive runtime, which is exactly the "upstream change that re-separates the
packages" that the ADR-0003 2026-09-02 amendment and ADR-0001 decisive-evidence
point 2 both named as the condition for reconsidering Mibo. Recording it keeps the
decision record consistent with current external facts (README source-of-truth
rule: an accepted ADR overrides an older design doc and the conflict is removed in
the same change).

### Changes

- `decisions/ADR-0003-MIBO-ADOPTION.md`: new `## 2026-09-06 amendment` section
  (trigger, finding with changelog quote, what it changes, what it does not
  change, recorded status); status line updated to list the 2026-09-06 amendment.
- `decisions/ADR-0001-FRAMEWORK-SELECTION.md`: new `## 2026-09-06 note` after the
  Consequences section. Records that decisive-evidence point 2 no longer holds on
  the 5.x line, quantifies the weighted-score effect (dependency driver re-score
  1.5 -> ~3.0 is about +0.075 to Mibo's total, new gap ~0.43), states the
  decision stands, and points to backlog B-043.
- `docs/02_TECHNOLOGY_DECISION.md`: decision-summary sentence corrected
  (`current Mibo cannot be adopted` -> `Mibo 4.2.0-4.5.3 could not be adopted`,
  with the 5.0.0 re-separation noted); new bullet in "Verified current facts /
  Mibo".
- `docs/10_RISK_REGISTER.md`: R-003 `closed -> watch`; early-trigger and
  mitigation cells updated (classic MVU is now the `Mibo.Mvu` package).
- `docs/11_BACKLOG.md`: new row B-043 (P4, G4, `proposed`) - a Mibo 5.x
  reconsideration spike gated on the ADR-0001 review-trigger-1 editor-authoring
  measurement; B-024 dependency list gains B-043.
- `docs/12_PROGRESS_LEDGER.md`: this index row; "Pinned facts" Accepted ADRs row
  refreshed to mention both amendments.

No source, test, `CommandoWar.slnx`, or `PROJECT_STATE.yaml` change.

### Verification

- Manual check: Mibo changelog entry `[5.0.0] - 2026-09-04`
  (`https://github.com/AngelMunoz/Mibo/blob/main/CHANGELOG.md`).
  - Result: confirms `Mibo.Core` no longer depends on `Mibo.Adaptive`; classic
    Elmish/MVU (`Cmd`, `Sub`, `Program`, loops, headless) moved to a new
    `Mibo.Mvu` package; adaptive moved to `Mibo.Adaptive.Mibo`; MVU host packages
    `Mibo.Raylib.Mvu` / `Mibo.MonoGame.Mvu`; namespaces/types/members unchanged;
    migration is a recompile, not a source edit; templates pin `5.*`.
- Manual check: does the amendment contradict any still-binding rule?
  - Result: no. The `Mibo.Adaptive` prohibition before G5
    (`PROJECT_STATE.yaml` `prohibited_before_G5`, `docs/11_BACKLOG.md` parked
    ideas) is unchanged and is now satisfied by not referencing
    `Mibo.Adaptive.Mibo` / `*.Adaptive` host packages. ADR-0001's rollback rule
    (a new ADR with measured failure evidence) still governs any actual switch.
- Not run: `dotnet restore`/`build` against Mibo 5.x packages (no outbound
  restore in this session). Deferred to the B-043 spike.

### Evidence

- `decisions/ADR-0003-MIBO-ADOPTION.md` 2026-09-06 amendment (quoted changelog
  lines and the "what this does not change" analysis).
- Mibo changelog `[5.0.0] - 2026-09-04`.

### Deviations and unresolved issues

- The 5.x packages' target frameworks are not stated in the release notes. The
  4.x line published `net8.0` and `net10.0` dependency groups; this project is
  `net10.0`. To be confirmed by B-043.
- Headless-runner parity with the Mibo 4.1.0 surface used in TASK-005 is
  unverified for 5.x.
- Mibo's release cadence (1.0 -> 5.0 in ~4 months, a major restructure on
  2026-09-04) keeps the churn sub-argument in the ADR-0001 Mibo column alive; the
  reconsideration does not retract it.
- B-043 is gated on the ADR-0001 review-trigger-1 measurement, which depends on
  the first Godot editor-authoring task (B-025). If that measurement is still not
  available when B-024 comes up for selection, that absence is itself a finding
  for the B-043 review.

### Documents updated

- `decisions/ADR-0003-MIBO-ADOPTION.md`
- `decisions/ADR-0001-FRAMEWORK-SELECTION.md`
- `docs/02_TECHNOLOGY_DECISION.md`
- `docs/10_RISK_REGISTER.md`
- `docs/11_BACKLOG.md`
- `docs/12_PROGRESS_LEDGER.md` (this index row + "Pinned facts" refresh)
- this entry

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: recorded as an option only, per Dave's instruction. The framework
  decision is unchanged; selecting B-043 (or amending ADR-0001) is a separate
  deliberate step. No coding-agent task file is created - this is an ADR
  amendment triggered by external release news, handled the same way as the
  ADR-0003 2026-09-02 coupling amendment.
