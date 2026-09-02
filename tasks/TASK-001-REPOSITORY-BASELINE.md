# TASK-001: Establish Repository and Build Baseline

Status: active  
Owner: Dave  
Phase: P0  
Gate: G0  
Size: S

## Objective

Establish the smallest reproducible .NET repository baseline for a framework-independent F# simulation and its tests, without selecting or installing a graphical framework.

## Why this task exists

The project currently has a design pack but no inspected code baseline. All later tasks need a known SDK, build command, test command, dependency direction, and repository layout.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `decisions/ADR-0002-SIMULATION-BOUNDARY.md`
- `docs/03_ARCHITECTURE.md`
- existing repository files, if any

## Dependencies

- project control pack reviewed sufficiently to begin setup

## Inputs and assumptions

- A target repository is available to the coding agent.
- The installed .NET SDK must be inspected rather than guessed.
- If the repository already contains a solution or conventions, preserve and extend them rather than recreating the structure.

## Allowed scope

- inspect current Git and repository state without destructive commands;
- add or update the minimal solution and project files needed for the baseline;
- add an F# simulation library;
- add a headless console or test host only if required to establish the boundary;
- add one F# test project using the repository's existing test convention, or a minimal appropriate convention when none exists;
- add a pinned SDK file only after verifying an installed and supported SDK;
- add ordinary build metadata needed for reproducibility;
- add one boundary smoke test.

## Forbidden scope

- Godot, Mibo, MonoGame, raylib, Tiled, rendering, UI, audio, gameplay, map, ECS, planner, content schema, replay implementation, or performance optimisation;
- speculative multi-project layering beyond the minimum baseline;
- dependency upgrades unrelated to making the baseline build;
- reformatting or moving existing unrelated code;
- deleting or resetting existing work.

## Required work

1. Inspect repository structure, current changes, installed SDKs, and existing build or test conventions.
2. Select the smallest compatible target framework and record the reason.
3. Establish or extend a solution containing a framework-neutral F# simulation library and test project.
4. Add a trivial public simulation-boundary type or function sufficient to prove the library can be referenced and tested. Do not implement game behaviour.
5. Add a test that references the library and passes.
6. Confirm the simulation project has no forbidden framework or platform dependency.
7. Document exact restore, build, and test commands in the repository's appropriate README or development document.
8. Record all evidence and update project control files.

## Acceptance criteria

- [ ] The existing repository was inspected and preserved.
- [ ] One documented command restores and builds the relevant solution from a clean checkout state, subject to ordinary package availability.
- [ ] One documented command runs the test suite successfully.
- [ ] The F# simulation library is referenced by tests and contains no graphical or host framework dependency.
- [ ] Target framework and SDK choices reflect installed and supported tools rather than invented versions.
- [ ] No gameplay or client framework was introduced.
- [ ] Task, backlog, state, and progress ledger are consistent.

## Required verification

Determine exact commands after inspection. At minimum:

- `dotnet --info`
- solution restore or implicit restore;
- release or normal build;
- focused test command;
- package-reference inspection for the simulation project;
- repository status check showing only intended changes.

## Evidence to capture

- detected SDK and runtime versions;
- solution and project paths;
- restore, build, and test output summaries;
- simulation project package references;
- any existing repository convention that changed the expected layout;
- unresolved warnings.

## Expected files

Likely areas, subject to existing structure:

- solution file;
- framework-neutral F# simulation project;
- F# test project;
- SDK or build metadata if justified;
- repository development instructions;
- project-control documents.

Do not invent the exact path when the repository already has a convention.

## Documentation updates

- set this task to `review` after implementation;
- update TASK-001 row in `docs/11_BACKLOG.md`;
- append evidence to `docs/12_PROGRESS_LEDGER.md`;
- update G0 and active-work fields in `PROJECT_STATE.yaml` only to the state justified by evidence;
- do not activate TASK-002 without Dave's review.

## Rollback or removal

All new baseline files should be removable without touching unrelated user code. If the repository already has an incompatible structure, adapt the task rather than creating a parallel solution.
