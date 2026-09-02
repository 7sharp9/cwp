# TASK-002: Create Framework-Neutral Simulation Skeleton

Status: review  
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

- [x] A headless test can construct a six-agent world and advance it by explicit steps.
      `Setup.sixAgentWorld`; test `a six-agent world can be constructed and advanced by explicit steps`.
- [x] Authoritative time is represented only by integer ticks.
      `WorldState.Tick : int64`; no float `dt` anywhere. Test `step advances authoritative time by exactly one integer tick`.
- [x] A typed move command changes the appropriate agent through the simulation step.
      `PlayerCommand` / `PlayerIntent.MoveTo`; test `a move command moves the targeted agent one cell per tick until it arrives`.
- [x] Invalid agent or out-of-bounds target commands fail explicitly and deterministically.
      `CommandRejection.UnknownAgent` / `TargetOutOfBounds` emitted as `CommandRejected` events; tests
      `a move command for an unknown agent is rejected explicitly and deterministically`,
      `a move command to an out-of-bounds target is rejected explicitly`.
- [x] Events and snapshots use stable IDs and stable ordering.
      Command events sorted by command id, movement events and snapshot agents sorted by agent id; tests
      `command outcome events are ordered by command id regardless of submission order`,
      `movement events and snapshot agents are ordered by ascending agent id`.
- [x] Phase order is visible and tested at least by an execution trace or targeted assertion.
      `Phases.order` (single source of truth) and `StepResult.PhaseTrace`; test `every tick executes the full documented phase order`.
- [x] The simulation project has no forbidden dependency.
      `dotnet list ... package --include-transitive` shows only `FSharp.Core 10.1.303`.
- [x] No production gameplay system or general framework was introduced.
      Placeholder movement is isolated in `PlaceholderMovement` and explicitly not pathfinding; no LOS, combat, morale, ECS, async, or content parsing added.

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

## Completion notes (2026-09-02)

Implemented as F# modules under `src/CommandoWar.Sim/` in compile order:
`Ids.fs`, `Grid.fs`, `Domain.fs`, `Commands.fs`, `Events.fs`, `Snapshot.fs`,
`Phases.fs`, `Movement.fs`, `Simulation.fs`. `Baseline.fs` is left untouched
(its `ContractName` / `nextTick` remain a harmless boundary probe).

Public API: `Simulation.step : SimConfig -> PlayerCommand[] -> WorldState -> StepResult`,
with `World.create`, `Setup.sixAgentWorld`, `Agent.create`, `Command.moveTo`,
`AgentId.ofInt`, `CommandId.ofInt`, and `Phases.order`.

Phase order (`Phases.order`, folded over by `Simulation.step`): CommandIntake,
Communication, Perception, TacticalKnowledge, Appraisal, CommitmentAndLocalAction,
NavigationAndMovement, Combat, StateConsequences, Mission, Output. Only
CommandIntake, NavigationAndMovement and Output do work this milestone; the rest
are explicit no-ops (matched individually, no wildcard).

Tests: `tests/CommandoWar.Sim.Tests/SimulationTests.fs` (13 facts). Full suite
15 passed (2 baseline + 13 new). Evidence in `docs/12_PROGRESS_LEDGER.md`.

Deviation from the ADR-0002 reference boundary: `StepResult` carries an
additional `PhaseTrace` diagnostic field; `SimConfig` currently holds only
`TicksPerSecond` (non-authoritative). The `step`/`StepResult` shape otherwise
matches. Placeholder movement (`PlaceholderMovement.nextCell`) is isolated for
replacement by B-010/B-011.

Post-review (2026-09-02): idiom pass applied (phase runner is a `for` loop, not
a `List.fold` over a mutated accumulator; sorts use the id types' structural
comparison). Known future hot paths, adequate for the &lt;64-agent budget and
left unchanged until profiled: per-command linear agent lookup, per-accepted-
command `Array.copy` of the agent array, reversed-list accumulation in `step`.

## Rollback or removal

The placeholder movement implementation must be isolated so production navigation can replace it without changing host contracts unnecessarily.
