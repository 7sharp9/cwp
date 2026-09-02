# TASK-005: Disposable Mibo plus raylib Framework Spike

Status: proposed  
Owner: unassigned  
Phase: P1  
Gate: G1  
Size: M

## Objective

Measure the cost and quality of using Mibo classic MVU with its raylib backend and Tiled authoring as the F#-native presentation host for the same deterministic simulation used by the Godot spike.

## Why this task exists

Mibo may remove the mixed-language host boundary and provide headless and graphical application infrastructure. Its smaller ecosystem, external authoring workflow, active evolution, and experimental adaptive path create risk that must be measured rather than assumed.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `decisions/ADR-0001-FRAMEWORK-SELECTION.md`
- `decisions/ADR-0002-SIMULATION-BOUNDARY.md`
- `decisions/ADR-0003-MIBO-ADOPTION.md`
- `docs/02_TECHNOLOGY_DECISION.md`
- `docs/06_CONTENT_AND_PRESENTATION.md`
- TASK-002 and TASK-003 evidence

## Dependencies

- TASK-003 accepted and `done`

## Inputs and assumptions

- Recheck Mibo, raylib-cs, and Tiled versions before installation.
- Research snapshot suggests Mibo 4.5.3 and Tiled 1.12.2, but the task must pin and record actual versions.
- Use Mibo classic MVU. Mibo.Adaptive is prohibited.
- Use the exact simulation assembly and logical fixture used by TASK-004.

## Allowed scope

- one disposable F# Mibo host;
- Mibo classic MVU and its raylib backend;
- one small Tiled-authored isometric map;
- a small importer to framework-neutral fixture DTOs;
- six placeholder agents;
- input to issue the existing typed move command;
- fixed-step host scheduling or direct authoritative step integration;
- tick and state-hash overlay;
- one invalid-content validation path;
- build and local packaging evidence;
- optional MonoGame backend compile or launch smoke test only after common evidence is complete.

## Forbidden scope

- Mibo.Adaptive;
- moving authoritative world state into the Mibo model;
- changing simulation behaviour to suit Mibo;
- broad backend abstraction or maintaining two rendering implementations;
- custom general-purpose editor, UI framework, ECS, gameplay, new AI, pathfinding, final art, or production commitment;
- hiding framework defects with retries, sleeps, or duplicated state.

## Required work

1. Pin and record the Mibo, backend, Tiled, and SDK versions.
2. Create the smallest classic-MVU host around the existing simulation.
3. Author and import the common isometric fixture through a typed validation boundary.
4. Render six agents from snapshots and display current tick and state hash.
5. Submit the existing move command.
6. Demonstrate fixed authoritative stepping and compare it with direct headless stepping.
7. Make one Tiled content error fail visibly and actionably.
8. Perform the common content-edit exercise from `docs/06_CONTENT_AND_PRESENTATION.md`.
9. Produce a release executable or local package and launch it.
10. Record all ADR-0001 evidence, including Mibo-specific friction, source inspection, workarounds, and dependency concerns.
11. Only if it is a small bounded check, verify whether the same host surface compiles or starts with Mibo's MonoGame backend. Do not build a third comparable client.

## Acceptance criteria

- [ ] The unmodified simulation project builds and runs through the Mibo host.
- [ ] Mibo classic MVU is used and no Mibo.Adaptive package or API appears.
- [ ] Six agents render on an isometric map imported from Tiled.
- [ ] A user input produces the existing typed move command and deterministic state change.
- [ ] Tick and state hash are visible and agree with an equivalent direct headless run.
- [ ] One invalid map object or property produces an actionable loading failure.
- [ ] A map and marker edit has measured workflow evidence.
- [ ] A release executable or package launches, or the exact blocker is documented.
- [ ] No Mibo, raylib, MonoGame, or Tiled type enters the simulation project.
- [ ] Framework version and any workarounds are explicit.
- [ ] No production framework decision is claimed by this task.

## Required verification

- simulation and replay tests before and after host work;
- Mibo host build;
- direct headless-versus-Mibo hash comparison;
- manual move-command smoke test;
- invalid-content smoke test;
- packaged executable launch;
- dependency boundary inspection;
- optional MonoGame backend smoke result clearly separated.

## Evidence to capture

- exact SDK, Mibo, raylib-cs, Tiled, and optional MonoGame versions;
- setup, build, run, and package commands;
- screenshot of map and tick/hash overlay;
- one command and matching direct/Mibo hash;
- content edit steps and observed time;
- host-specific files and approximate infrastructure surface;
- quality of debugging, input, rendering, UI, content import, and errors;
- framework issues, source-level workarounds, and likely maintenance cost;
- expected cost of extending this host to the vertical slice.

## Documentation updates

- task, backlog, progress ledger, and state as required;
- write results into ADR-0001's evidence table or an attached spike-results section without deciding it;
- do not activate TASK-006 automatically.

## Rollback or removal

The entire Mibo host and Tiled importer must be removable without changing or deleting simulation, tests, command logs, or shared logical fixture data.
