# TASK-004: Disposable Godot .NET Framework Spike

Status: proposed  
Owner: unassigned  
Phase: P1  
Gate: G1  
Size: M

## Objective

Measure the cost and quality of using Godot .NET as the presentation and authoring host for the same deterministic F# simulation used by the Mibo spike.

## Why this task exists

Godot is the provisional shipping-oriented favourite because it supplies integrated content, UI, animation, import, and debugging tools. That advantage must be demonstrated without allowing Godot types or lifecycle to become authoritative.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `decisions/ADR-0001-FRAMEWORK-SELECTION.md`
- `decisions/ADR-0002-SIMULATION-BOUNDARY.md`
- `docs/02_TECHNOLOGY_DECISION.md`
- `docs/06_CONTENT_AND_PRESENTATION.md`
- TASK-002 and TASK-003 evidence

## Dependencies

- TASK-003 accepted and `done`

## Inputs and assumptions

- Recheck the current stable Godot .NET release before installation.
- Research snapshot suggests 4.7.2, but the task must record the actual pinned version used.
- Use the exact simulation assembly and fixture later used by TASK-005.

## Allowed scope

- one disposable Godot .NET client project;
- one thin C# facade or adapter over the F# simulation;
- one small isometric greybox map authored in Godot;
- six placeholder agents;
- input to issue the existing typed move command;
- fixed-step scheduling and simple visual interpolation;
- tick and state-hash overlay;
- one invalid-content validation path;
- one minimal UI or debug panel;
- build and local packaging evidence;
- spike observations and screenshots.

## Forbidden scope

- production architecture commitment;
- changing simulation behaviour to suit Godot;
- authoritative Godot physics, navigation, random, or animation;
- broad UI framework, final art, audio system, gameplay, new AI, pathfinding, plugin framework, editor plugin, or generalized C# domain layer;
- comparing unrequested platforms;
- retaining the spike as production code before ADR-0001 is accepted.

## Required work

1. Pin and record the Godot .NET version and required SDK.
2. Create the smallest host that loads the existing simulation through a plain .NET boundary.
3. Author a small isometric map and framework-neutral fixture conversion.
4. Render six agents from snapshots and display current tick and state hash.
5. Submit the existing move command through the C# adapter.
6. Schedule the fixed simulation tick independently from render rate.
7. Make one content error fail visibly and actionably.
8. Perform the common content-edit exercise from `docs/06_CONTENT_AND_PRESENTATION.md`.
9. Produce a release build or local package and launch it outside the editor where supported.
10. Record all evidence required by ADR-0001, including awkwardness and failed attempts.

## Acceptance criteria

- [ ] The unmodified simulation project builds and runs through the Godot host.
- [ ] Six agents render on an isometric map from simulation snapshots.
- [ ] A user input produces the existing typed move command and deterministic state change.
- [ ] Tick and state hash are visible and agree with an equivalent headless run.
- [ ] Render rate and authoritative tick rate are visibly separate.
- [ ] One invalid map or marker produces an actionable loading failure.
- [ ] A map and marker edit has measured workflow evidence.
- [ ] A build launches outside the editor or the exact blocker is documented.
- [ ] No Godot type or package enters the simulation project.
- [ ] No production framework decision is claimed by this task.

## Required verification

- simulation and replay tests before and after host work;
- Godot project build;
- manual move-command smoke test;
- headless-versus-host final hash comparison;
- invalid-content smoke test;
- packaged-build launch;
- dependency boundary inspection.

## Evidence to capture

- exact Godot and SDK versions;
- setup, build, run, and package commands;
- screenshot of map and tick/hash overlay;
- one recorded command and matching headless/host hash;
- content edit steps and observed time;
- host-specific files and approximate glue surface;
- debugger, error, asset import, UI, and iteration observations;
- workarounds and unresolved defects;
- expected cost of extending this host to the vertical slice.

## Documentation updates

- task, backlog, progress ledger, and state as required;
- write results into ADR-0001's evidence table or an attached spike-results section without deciding the ADR;
- do not activate TASK-005 automatically.

## Rollback or removal

The entire Godot host must be removable without changing or deleting simulation, tests, command logs, or shared fixture data.
