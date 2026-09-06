# Commando War Spiritual Successor - Project Control Pack

Status: discovery / architecture validation (current phase and gate: `PROJECT_STATE.yaml`)  
Public title: not selected  
Framework: Godot .NET client over a framework-independent F# simulation (ADR-0001, accepted 2026-09-03)  
Current phase, gate, and selected task: see `PROJECT_STATE.yaml`

## Purpose

This pack turns the game concept into a controlled, inspectable development programme. It is designed for a human project owner directing coding agents without allowing the agents to invent scope, architecture, or evidence.

The product hypothesis is deliberately narrow:

> A real-time isometric squad game can make bounded, explainable disobedience enjoyable by letting the player change battlefield conditions until a risky order becomes acceptable.

Everything else is subordinate to proving or disproving that statement.

## Start here

Read these files in order:

1. `docs/00_PROJECT_CHARTER.md`
2. `docs/01_ADVERSARIAL_REVIEW.md`
3. `docs/02_TECHNOLOGY_DECISION.md`
4. `docs/07_VERTICAL_SLICE.md`
5. `docs/08_ROADMAP_AND_GATES.md`
6. `docs/11_BACKLOG.md`
7. `AGENTS.md`
8. The active file under `tasks/`

The detailed simulation and AI specifications are reference material for tasks that touch those areas.

## Sources of truth

Do not duplicate status or decisions across files.

| Concern | Authoritative file |
|---|---|
| Current phase, active task, gates | `PROJECT_STATE.yaml` |
| Product boundaries | `docs/00_PROJECT_CHARTER.md` |
| Accepted architectural decisions | `decisions/ADR-*.md` |
| Work status and dependencies | `docs/11_BACKLOG.md` |
| Desired runtime behaviour | `docs/04_SIMULATION_SPEC.md` and `docs/05_COMMAND_AND_AGENT_AI.md` |
| Completed work and evidence | `docs/12_PROGRESS_LEDGER.md` |
| Known threats | `docs/10_RISK_REGISTER.md` |

When files conflict, an accepted ADR overrides an older design document. The conflict must then be removed in the same change.

## Controlled workflow

1. Dave selects exactly one ready task from `docs/11_BACKLOG.md`.
2. The coding agent reads `AGENTS.md`, `PROJECT_STATE.yaml`, the task file, and only the relevant specifications.
3. The agent inspects the repository before proposing edits.
4. The agent implements only the task's stated outcome.
5. The agent runs the narrowest relevant tests, then the broader affected suite.
6. The agent updates the task, backlog status, project state, and progress ledger with evidence.
7. Dave reviews the change against `tasks/REVIEW_CHECKLIST.md`.
8. A task is marked done only after the evidence is accepted.

Use `tasks/CODING_AGENT_PROMPT.md` to start an implementation session.

## Immediate sequence

P0 through P2 are complete and gates G0, G1, and G2 have passed: the repository
baseline, the framework-neutral simulation skeleton, the determinism harness,
both framework spikes, ADR-0001 (Godot selected), and the deterministic core
(terrain, line of sight, pathfinding, movement, same-tick reservation, sub-cell
progress, replay corpus, benchmarks, determinism property tests) are all done.
See `docs/12_PROGRESS_LEDGER.md` for the task-by-task record.

Current position, the active gate (G3), and the next task are in
`PROJECT_STATE.yaml` and `docs/11_BACKLOG.md`. Client and presentation work
belongs to P4 and must not begin before G3 passes.

## Architectural position

The simulation is an ordinary F# library with no dependency on Godot, MonoGame, raylib, Mibo, rendering, audio, or an editor. A client submits typed commands and receives domain events plus a render snapshot.

The framework spike (TASK-004 Godot, TASK-005 Mibo) built two disposable
presentation hosts over the same simulation. ADR-0001 selected Godot .NET through
a thin C# adapter; the Mibo host is retained as spike evidence only. Mibo 5.x is
recorded as a reconsideration option (ADR-0003 2026-09-06 amendment), not an
active route.

Mibo.Adaptive is excluded until after the vertical-slice gate (G5). It is
experimental and adds invalidation and threading behaviour that the initial
simulation does not need.

## Intellectual property boundary

`Commando War` is a historical reference and internal working description, not a proposed public title. Do not copy original art, maps, text, names, logos, screenshots, or code. The shipped product must use an original title, original setting details, and original assets unless rights are explicitly established.

## Definition of project success

The project has succeeded at the first meaningful level when an external player can:

- issue a dangerous order;
- understand why one soldier delays, adapts, or refuses;
- change the tactical situation using cover or suppression;
- reissue the order and obtain a different, predictable response;
- complete one short mission;
- replay the same command stream and seed with the same authoritative result.

A technically sophisticated simulation without that player experience is not success.

## Repository baseline

Established by TASK-001. The repository is a standard .NET solution.

| Concern | Value |
|---|---|
| Solution | `CommandoWar.slnx` |
| Pinned SDK | .NET SDK `10.0.303` via `global.json` (`rollForward: latestPatch`) |
| Target framework | `net10.0` (installed and LTS; supported to November 2028) |
| Simulation library | `src/CommandoWar.Sim/` (F#, no host or framework dependency) |
| Tests | `tests/CommandoWar.Sim.Tests/` (F#, xUnit) |

The graphical framework is Godot .NET (ADR-0001, accepted 2026-09-03). The
simulation library itself has no framework dependency.

### Restore, build, and test

From a clean checkout with the pinned SDK installed:

```text
dotnet build CommandoWar.slnx -c Release
dotnet test CommandoWar.slnx -c Release
```

`dotnet build` performs an implicit restore. Package restore needs network access on
first run. `dotnet test` builds and then runs the full suite.
