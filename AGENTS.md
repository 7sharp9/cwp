# Coding Agent Operating Rules

These rules apply to every coding-agent task in this repository.

## Read before editing

Read, in this order:

1. `PROJECT_STATE.yaml`
2. The selected `tasks/TASK-*.md`
3. The accepted ADRs named by that task
4. The minimum relevant design documents
5. The files and tests that will actually be changed

Do not infer architecture from filenames alone. Inspect the implementation first.

## Scope

- Work on one selected task only.
- Do not implement follow-on tasks because they appear convenient.
- Do not add a framework, package, service, project, abstraction, or code generator unless the task explicitly permits it.
- Do not perform unrelated refactoring, formatting churn, renaming, or directory reorganisation.
- Do not delete user work, reset Git, clean untracked files, force-push, or commit unless Dave explicitly requests it.
- Treat public title, branding, and copied historical assets as out of scope.

## Dependency rules

- `CommandoWar.Sim` must not reference Godot, MonoGame, raylib, Mibo, UI, audio, file dialogs, wall-clock time, or platform APIs.
- Client projects may reference the simulation. The simulation may not reference clients.
- Authoritative state changes happen only inside the simulation step.
- Rendering, animation, audio, particles, and interpolation are non-authoritative.
- New dependencies require either an accepted ADR or explicit permission in the active task.
- Pin framework and package versions. Do not silently upgrade them.

## Determinism rules

- Advance authoritative time in integer ticks.
- Do not use `DateTime.Now`, `Stopwatch`, frame delta, thread scheduling, or `System.Random` inside authoritative logic.
- Use stable entity ordering. Do not depend on hash-map enumeration order.
- Do not use floating-point values for state that participates in deterministic outcomes unless an accepted ADR changes this rule.
- All randomness must enter through the project-owned deterministic random interface.
- Every accepted player command must be recordable and replayable.

## Implementation quality

- Prefer the smallest change that satisfies the task.
- Make invalid states hard to construct, but do not build speculative type machinery.
- Use explicit errors for invalid content and invalid commands.
- Do not hide failures with broad exception catches, retries, sleeps, ignored results, or unchecked casts.
- Keep system order visible and testable.
- Add comments only for non-obvious constraints and reasons.

## Verification

Before reporting completion:

1. Run tests named in the task.
2. Run the affected project or smoke test where practical.
3. Record exact commands and outcomes.
4. Check that no forbidden dependency was introduced.
5. Check that the task acceptance criteria are each supported by evidence.

Do not claim success for tests that were not run.

## Required documentation update

Every completed task must update:

- its own task file;
- the matching row in `docs/11_BACKLOG.md`;
- `PROJECT_STATE.yaml` if the active task, phase, gate, or decision changed;
- `docs/12_PROGRESS_LEDGER.md` with commands, results, and unresolved concerns.

Update an ADR only when the task explicitly makes or supersedes a decision.

## Required final report

Report:

- diagnosis and chosen approach;
- files changed;
- observable behaviour changed;
- tests and commands run, with results;
- acceptance criteria evidence;
- risks or unresolved questions;
- documentation updated.

Do not offer or begin the next task.
