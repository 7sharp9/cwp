# TASK-006: Evaluate Framework Spikes and Accept ADR-0001

Status: proposed  
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

- [ ] Both candidates were judged against equivalent minimum behaviour and content work.
- [ ] Every weighted driver contains evidence-based scores and notes.
- [ ] The qualitative decision explains why the winner is more likely to ship the vertical slice.
- [ ] Language preference and editor preference are distinguished from observed productivity.
- [ ] One and only one production client route is selected, or the project explicitly stops.
- [ ] Accepted weaknesses, dependency versions, and review triggers are recorded.
- [ ] The losing host is absent from default production build and CI paths.
- [ ] ADR-0001, state, backlog, progress ledger, and technology document agree.
- [ ] No vertical-slice feature was implemented as part of the decision.

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
