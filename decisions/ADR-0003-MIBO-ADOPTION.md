# ADR-0003: Conditional and Limited Use of Mibo

Status: accepted as spike policy, production adoption pending ADR-0001  
Date: 2026-09-02  
Decision owner: Dave

## Context

Mibo is directly relevant to this project. It provides:

- an F# Elmish/MVU application model;
- a headless runtime with explicit stepping and virtual time;
- an ordered system pipeline;
- raylib-cs and MonoGame presentation backends;
- command-buffer rendering and input or asset support;
- an optional adaptive architecture.

This overlaps with some infrastructure the project would otherwise write. It also introduces dependency and abstraction risk because it is a smaller, actively changing framework. Its adaptive integration is explicitly described as experimental, and current issue reports show edge cases involving threading and adaptive collection correctness.

The project does not need Mibo to define the simulation. A minimal project-owned headless runner can call the simulation step directly.

## Decision

### During the framework spike

- Mibo may be used for TASK-005 as the candidate graphical host and headless application shell.
- Use **classic MVU**, not Mibo.Adaptive.
- Use the raylib backend for the first complete graphical spike.
- A MonoGame backend compile or launch smoke test is optional and must not delay comparable evidence.
- Pin the exact Mibo package version selected at task start.
- Record every workaround and any framework behaviour that affects stepping, ordering, input, or diagnostics.

### In all cases

- `CommandoWar.Sim` must not depend on Mibo.
- Mibo may schedule calls to the simulation but may not define authoritative domain state or rules.
- Mibo messages and models belong in the host project.
- The project-owned command, event, snapshot, random, and replay contracts remain authoritative.
- Do not design a general abstraction over Godot, Mibo, MonoGame, and raylib.

### Production adoption

Mibo becomes a production dependency only if ADR-0001 selects the Mibo route after equivalent spike evidence.

If Godot is selected, remove Mibo from default production build and CI. Do not retain it solely for headless execution; use the small project-owned runner instead.

## Mibo.Adaptive decision

Mibo.Adaptive is prohibited before G5.

After G5 it may be reconsidered only through a new ADR demonstrating:

- a measured problem in classic MVU or the current presentation architecture;
- why ordinary incremental caching or data-oriented stores are insufficient;
- deterministic and threading tests for the proposed usage;
- an explicit ownership model for foreign-thread reads and updates;
- a migration and removal path;
- version-pinned evidence against current open correctness concerns.

Framework novelty is not sufficient justification.

## Why Mibo can help

It can reduce work in a code-first F# route by supplying:

- host loop and lifecycle;
- explicit fixed-step or headless stepping facilities;
- ordered systems;
- backend-level rendering abstraction;
- familiar Elmish update structure;
- a path from simulation tests to a small graphical host without moving to C#.

This is useful for the complete application shell. It does not materially simplify the domain simulation's hardest parts:

- perception;
- tactical knowledge;
- order appraisal;
- deterministic pathfinding;
- suppression;
- commitment and execution;
- replay and canonical hashing.

Those remain project code.

## Risks accepted during spike

- package API churn;
- sparse ecosystem compared with Godot or MonoGame;
- the need to integrate an external map editor and custom UI conventions;
- temptation to use backend portability before there is a second-backend requirement;
- confusion between Elmish host state and authoritative world state.

The spike is deliberately disposable and bounded to contain those risks.

## Review triggers

Revisit this policy if:

- Mibo introduces a breaking change before TASK-005 begins;
- the pinned version cannot build on the selected .NET SDK;
- headless stepping behaves differently from direct simulation stepping;
- host-system ordering obscures the authoritative phase order;
- the framework requires Mibo types in simulation contracts;
- a framework defect blocks comparable evidence;
- ADR-0001 chooses Godot.
