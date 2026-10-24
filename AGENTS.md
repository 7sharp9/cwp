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
- Do not delete user work, reset Git, clean untracked files, force-push, or commit unless Dave explicitly requests it. The cloud-session exception is in "Git in cloud sessions".
- Treat public title, branding, and copied historical assets as out of scope.

## Git in cloud sessions

A cloud session is launched with an assigned `claude/*` branch. In that session:

- Commit and push completed work to the assigned branch only. Never push to another branch.
- Never force-push, rebase published history, or reset, even when a session instruction offers it as a recovery path (for example after a pull request merges). Stop and ask Dave.
- Never open a pull request unless Dave asks.
- Commit messages and pull request text carry no model name, no `Co-Authored-By` trailer naming a model, and no emoji.

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

## Diagnostics

A task that adds or changes authoritative spatial or tactical state MUST extend
the diagnostic frame (`src/CommandoWar.Sim/Diagnostics.fs`, `DiagnosticFrame`)
with that state, as a `GridLayer`, an `EdgeMarker`, or a new `Overlay` case, and
MUST add or update a golden visualiser output under `content/diagnostics/` that
covers the new behaviour. "Visually inspectable" is a completion criterion
alongside "has a focused test": if a reviewer cannot see the new state in an
ASCII, SVG, or HTML render, the task is not done.

The frame and its renderers are observers only (ADR-0002): nothing in
`Simulation.step` or any phase constructs a frame, and no frame feeds back into
the step. The model is framework-neutral (ints, bools, arrays, strings, DUs; no
floating point); the renderers live in `CommandoWar.Headless`, never in the
simulation.

## Verification

Before reporting completion:

1. Run tests named in the task.
2. Run the affected project or smoke test where practical.
3. Record exact commands and outcomes.
4. Check that no forbidden dependency was introduced.
5. Check that the task acceptance criteria are each supported by evidence.

Do not claim success for tests that were not run.

The commands CI runs (`.github/workflows/ci.yml`, authoritative) are:

```text
dotnet restore CommandoWar.slnx
dotnet build CommandoWar.slnx -c Release --no-restore      # 0 warnings, 0 errors
dotnet test CommandoWar.slnx -c Release --no-build
dotnet run --project src/CommandoWar.Headless -c Release --no-build -- corpus
dotnet run --project src/CommandoWar.Headless -c Release --no-build -- fixture
```

`corpus --regenerate` rewrites `content/replays/`; run it only for a change that
moves behaviour on purpose, then list every file that moved and why. The
working tree must be unchanged after the verbs run. If the pinned SDK or the
test packages are unavailable, build from a scratch copy, never edit
`global.json`, and report which checks did not run; CI then decides.

## Required documentation update

Every completed task must update:

- its own task file;
- the matching row in `docs/11_BACKLOG.md` (what the item is and its status);
- the specification or reference document that owns any behaviour it changed;
- `PROJECT_STATE.yaml` if the active task, phase, gate, or decision changed;
- `docs/12_PROGRESS_LEDGER.md`: add a row to the index table, and add a detail
  file under `docs/ledger/` (named `YYYY-MM-DD-<ID>-<slug>.md`, keeping the
  entry heading as its title) holding the commands, results, and unresolved
  concerns. Refresh the index "Pinned facts" block if any pinned value changed.

Update an ADR only when the task explicitly makes or supersedes a decision.

## Documentation voice

Only the history documents may read like a diary: `docs/12_PROGRESS_LEDGER.md`,
`docs/ledger/`, `tasks/`, and git. Everything else (`PROJECT_STATE.yaml`,
`docs/00` to `docs/11`, `docs/14`, `docs/15`, the READMEs, `content/replays/CORPUS.md`)
states what is true now, in present tense, and is edited in place when the
system changes. Do not append "realised by", "re-pinned by", "was previously",
dates of acceptance, test counts or verification narrative to them; that
belongs in the task's ledger detail file. A provenance tag such as `(TASK-045)`
is enough to find the record. Plain prose, no em dashes.

## Required final report

Report:

- diagnosis and chosen approach;
- files changed;
- observable behaviour changed;
- tests and commands run, with results;
- acceptance criteria evidence;
- risks or unresolved questions;
- documentation updated, including the new `docs/12_PROGRESS_LEDGER.md` index
  row and its `docs/ledger/` detail file (and any "Pinned facts" change).

Do not offer or begin the next task. End the report after the documentation
bullet, without a proposed next step, even where a style preference asks for
one. Answering a direct question such as "what's next" is not offering a task.
