# TASK-006: Evaluate Framework Spikes and Accept ADR-0001

Status: done  
Owner: Dave  
Phase: P1  
Gate: G1  
Size: S

## Objective

Select one production presentation route, or stop the project, using the recorded Godot and Mibo spike evidence.

## Why this task exists

Maintaining multiple clients would multiply work and conceal indecision. The project needs one content, presentation, packaging, and debugging path before gameplay systems expand.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `decisions/ADR-0001-FRAMEWORK-SELECTION.md`
- `decisions/ADR-0002-SIMULATION-BOUNDARY.md`
- `decisions/ADR-0003-MIBO-ADOPTION.md`
- `docs/02_TECHNOLOGY_DECISION.md`
- TASK-004 and TASK-005 complete evidence
- source and build artefacts for both spikes

## Dependencies

- TASK-004 accepted and `done`
- TASK-005 accepted and `done`

## Allowed scope

- rerun comparable smoke checks when evidence is unclear;
- complete the predeclared weighted and qualitative comparison;
- inspect dependency, package, and glue boundaries;
- accept ADR-0001 with one route or a stop decision;
- pin selected framework dependencies;
- remove the losing host from default solution, build, CI, and delivery assumptions while preserving its evidence or isolated source if useful;
- update project control documents.

## Forbidden scope

- adding features to make one candidate catch up;
- changing comparison weights after seeing results without recording and justifying the change;
- retaining both as production clients;
- creating a common framework abstraction;
- beginning vertical-slice implementation;
- deciding based only on lines of code, benchmark speed, or language preference;
- deleting evidence needed to audit the decision.

## Required work

1. Confirm both spikes met equivalent minimum requirements or mark any invalid comparison.
2. Normalise and inspect the evidence, including failed attempts and workarounds.
3. Complete the decision-driver table using the predeclared weights.
4. Make a qualitative assessment of the largest future cost: content and UX iteration, F# and host integration, debugging, packaging, and dependency risk.
5. Select Godot, select Mibo, or stop/re-scope.
6. Record accepted weaknesses and explicit review triggers.
7. Accept ADR-0001 and update any provisional technology language.
8. Remove the losing route from default builds without destructive deletion of unreviewed work.
9. Update G1 and project state only after Dave accepts the decision evidence.

## Acceptance criteria

- [x] Both candidates were judged against equivalent minimum behaviour and
      content work. Equivalence confirmed in the 2026-09-03 ledger entry; every
      non-equivalent dimension (editor vs text authoring, Mibo 4.1.0 pin,
      packaging) recorded in ADR-0001.
- [x] Every weighted driver contains evidence-based scores and notes. ADR-0001
      "Scored decision-driver table (TASK-006)": 9 drivers, predeclared weights
      unchanged, per-candidate score + basis note. Godot 4.13, Mibo 3.62.
- [x] The qualitative decision explains why the winner is more likely to ship
      the vertical slice. ADR-0001 "Qualitative decision" block (recommendation,
      pending acceptance).
- [x] Language preference and editor preference are distinguished from observed
      productivity. ADR-0001 "Observed vs asserted vs preference" paragraph.
- [x] One and only one production client route is selected. **Godot .NET plus
      F# simulation** (ADR-0001 accepted 2026-09-03).
- [x] Accepted weaknesses, dependency versions, and review triggers are
      recorded. ADR-0001 "Qualitative decision" block + "Pinned versions".
- [x] The losing host is absent from default production build and CI paths.
      `src/CommandoWar.Client.Mibo/` is not in `CommandoWar.slnx` or any default
      build/test/CI path; "Rejected route" note added to its README;
      `decisions/ADR-0003` 2026-09-03 note records Mibo not adopted.
- [x] ADR-0001, state, backlog, progress ledger, and technology document agree.
      ADR-0001 `accepted` + Selected candidate; `docs/02` decision recorded;
      `PROJECT_STATE.yaml` `framework_decision` / `gates.G1` (`passed`) /
      `current_gate` (`G2`) / `current_phase` (`P2`) moved; backlog + ledger
      updated.
- [x] No vertical-slice feature was implemented as part of the decision. No P2
      task activated; no `CommandoWar.Sim` / test / fixture change.

## Progress notes (2026-09-03)

Precondition applied (TASK-005 accepted as evidence; TASK-006 activated). Both
spikes re-verified this session: 54 tests green and framework-neutral; both
hosts build `0/0`, reproduce the 41-hash fixture headless (`MATCH`, exit 0),
render windowed with fixture-correct hashes, and fail invalid content with
actionable errors and exit 2; `CommandoWar.Sim` package tree is `FSharp.Core`
only; the Mibo host carries no `Mibo.Adaptive`; the Godot editor opens the spike
project headless without error. The Godot editor GUI content-edit exercise could
not be driven (no interactive display) and is accepted as a known limitation on
Dave's instruction, recorded as ADR-0001 review trigger 1. Godot self-contained
packaging remains blocked on absent export templates (accepted weakness 2,
review trigger 2).

The completed decision matrix and the Godot recommendation are in
`decisions/ADR-0001-FRAMEWORK-SELECTION.md`. Remaining steps are gated on Dave's
acceptance of the evidence in this session.

## Required verification

- build and launch the selected host from documented commands;
- run the full headless suite and one selected-host hash comparison;
- confirm simulation dependency boundary;
- confirm default solution or build does not require the losing host;
- check documentation consistency and internal links.

## Evidence to capture

- completed decision matrix;
- decisive observations;
- selected and rejected routes;
- exact pinned versions;
- default build and test commands;
- removal or isolation of the losing route;
- unresolved framework risks.

## Documentation updates

- set ADR-0001 to `accepted` or `rejected/stopped` with full decision;
- update `docs/02_TECHNOLOGY_DECISION.md`;
- update `PROJECT_STATE.yaml`, including G1;
- update task and backlog status;
- append final framework decision to the progress ledger;
- do not activate a P2 task without Dave's separate selection.

## Rollback or removal

If later evidence invalidates the selected host, open a new ADR with measured failure evidence. Do not silently resurrect the losing spike or maintain parallel production clients.
