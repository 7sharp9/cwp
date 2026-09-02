# TASK-NNN: Task title

Status: proposed  
Owner: unassigned  
Phase: P?  
Gate: G?  
Size: S/M/L

## Objective

One observable outcome. Avoid combining independent outcomes.

## Why this task exists

State the risk, dependency, or acceptance criterion it addresses.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- relevant ADRs
- minimum relevant specifications
- existing source and tests to be changed

## Dependencies

- task IDs or `none`

## Inputs and assumptions

- explicit inputs
- assumptions that may be tested
- versions already accepted

## Allowed scope

- files, projects, dependencies, and behaviour this task may change

## Forbidden scope

- adjacent features and refactors that must not be included

## Required work

1. Inspect ...
2. Implement ...
3. Verify ...
4. Record ...

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [ ] Criterion with observable evidence.
- [ ] Criterion with test or build evidence.
- [ ] No forbidden dependency or scope entered the change.
- [ ] Required documentation was updated.

## Required verification

Run the narrowest applicable commands, then affected broader checks. Fill exact commands only after inspecting the repository.

- unit or property tests:
- scenario or replay tests:
- build:
- manual smoke test:
- dependency boundary check:

## Evidence to capture

- command output or test summary;
- paths to logs, screenshots, benchmark results, or replay files;
- relevant hashes and versions;
- unresolved failures.

## Expected files

List expected areas, not invented exact paths unless the repository already establishes them.

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md`;
- `docs/12_PROGRESS_LEDGER.md`;
- `PROJECT_STATE.yaml` when active task, phase, gate, or decision changes;
- ADR only when this task explicitly owns a decision.

## Rollback or removal

Describe how experimental code or a failed approach can be removed without damaging the simulation.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next task.
