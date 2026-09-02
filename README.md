# Commando War Spiritual Successor - Project Control Pack

Status: discovery and architecture validation  
Public title: not selected  
Current framework decision: pending ADR-0001  
Next controlled task: `tasks/TASK-001-REPOSITORY-BASELINE.md`

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

The initial dependency chain is:

```text
TASK-001 repository baseline
    |
    v
TASK-002 simulation skeleton
    |
    v
TASK-003 determinism harness
    |
    +-------------------+
    |                   |
    v                   v
TASK-004 Godot spike    TASK-005 Mibo spike
    |                   |
    +---------+---------+
              |
              v
TASK-006 framework decision
```

No production client work should proceed until TASK-006 records the framework decision.

## Architectural position

The simulation is an ordinary F# library with no dependency on Godot, MonoGame, raylib, Mibo, rendering, audio, or an editor. A client submits typed commands and receives domain events plus a render snapshot.

During the framework spike, two disposable presentation hosts exercise the same simulation:

- Godot .NET through a thin C# adapter.
- Mibo classic MVU, initially using its raylib backend and Tiled for map authoring.

Mibo.Adaptive is excluded until after the vertical-slice gate. It is currently experimental and adds invalidation and threading behaviour that the small initial simulation does not need.

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

A graphical framework is deliberately not selected yet; see ADR-0001.

### Restore, build, and test

From a clean checkout with the pinned SDK installed:

```text
dotnet build CommandoWar.slnx -c Release
dotnet test CommandoWar.slnx -c Release
```

`dotnet build` performs an implicit restore. Package restore needs network access on
first run. `dotnet test` builds and then runs the full suite.
