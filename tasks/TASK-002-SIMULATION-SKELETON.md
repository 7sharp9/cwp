# TASK-002: Create Framework-Neutral Simulation Skeleton

Status: active  
Owner: Dave  
Phase: P1  
Gate: G1 prerequisite  
Size: S

## Objective

Implement the minimal authoritative fixed-tick simulation API and ordered phase skeleton that both framework spikes can run, without gameplay systems.

## Why this task exists

The framework comparison is invalid unless both hosts consume the same simulation. The skeleton must establish ownership, tick semantics, commands, events, and snapshots before either presentation route begins.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `decisions/ADR-0002-SIMULATION-BOUNDARY.md`
- `docs/03_ARCHITECTURE.md`
- relevant sections of `docs/04_SIMULATION_SPEC.md`
- completed TASK-001 evidence and existing projects

## Dependencies

- TASK-001 accepted and `done`

## Allowed scope

- framework-neutral simulation contracts;
- minimal world state with tick and six placeholder agents;
- fixed-step function;
- explicit ordered phase runner with empty or trivial phase implementations;
- typed move command sufficient for the spikes;
- ordered domain events;
- render snapshot containing stable agent IDs and logical positions;
- headless test construction helpers;
- focused tests.

## Forbidden scope

- pathfinding, line of sight, combat, morale, appraisal, content parsing, random behaviour, graphical hosts, Mibo, Godot, broad generic system framework, ECS, async processing, save-game compatibility.

## Required work

1. Inspect the baseline and retain its naming and test conventions.
2. Define stable ID, tick, logical position, minimal agent state, player command, domain event, render snapshot, and step result types.
3. Implement a fixed authoritative step that increments one integer tick and processes commands at a documented phase.
4. Make phase order explicit in one location even where phases are currently no-ops.
5. Implement a simple deterministic placeholder movement rule adequate for the spike, such as one logical step toward a valid in-bounds destination per tick. Do not call it production pathfinding.
6. Emit events and a snapshot from authoritative state.
7. Add focused tests for tick progression, command acceptance or rejection, stable agent order, and snapshot consistency.
8. Verify no host dependency entered the simulation.

## Acceptance criteria

- [ ] A headless test can construct a six-agent world and advance it by explicit steps.
- [ ] Authoritative time is represented only by integer ticks.
- [ ] A typed move command changes the appropriate agent through the simulation step.
- [ ] Invalid agent or out-of-bounds target commands fail explicitly and deterministically.
- [ ] Events and snapshots use stable IDs and stable ordering.
- [ ] Phase order is visible and tested at least by an execution trace or targeted assertion.
- [ ] The simulation project has no forbidden dependency.
- [ ] No production gameplay system or general framework was introduced.

## Required verification

- focused simulation tests;
- full headless test suite;
- release build;
- dependency boundary inspection.

## Evidence to capture

- public simulation API;
- phase order;
- test commands and summaries;
- example event and snapshot for one move;
- package references of the simulation project.

## Documentation updates

Follow `AGENTS.md`. Do not activate TASK-003 without Dave's review.

## Rollback or removal

The placeholder movement implementation must be isolated so production navigation can replace it without changing host contracts unnecessarily.
