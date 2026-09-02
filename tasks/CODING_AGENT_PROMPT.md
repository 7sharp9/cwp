# Coding Agent Session Prompt

Copy the text below into a coding-agent session after replacing the task path.

```text
Work on exactly this selected task:

    tasks/TASK-NNN-TITLE.md

Before editing, read in order:

1. PROJECT_STATE.yaml
2. AGENTS.md
3. the selected task file
4. every ADR named by the task
5. only the design documents needed by the task
6. the existing implementation and tests in the affected area

Treat the repository as authoritative over assumptions. Preserve existing style and user changes. Do not perform unrelated refactors, dependency upgrades, formatting churn, file deletion, Git reset/clean, commits, or follow-on work.

First report a concise diagnosis based on inspected files and state the smallest implementation approach. Then implement only the selected task.

Make failures explicit. Do not hide them with broad catches, sleeps, retries, ignored results, unsafe casts, or weakened tests. Keep the F# authoritative simulation independent of all client frameworks and platform APIs. Follow every determinism restriction in AGENTS.md.

Run the narrowest relevant tests first, then the broader affected suite. Do not claim a check was run when it was not. On completion, update the task file, backlog, progress ledger, and project state exactly as required by AGENTS.md.

Your final report must contain:

- diagnosis and chosen approach;
- files changed;
- observable behaviour changed;
- exact test/build/run commands and results;
- acceptance-criterion evidence;
- unresolved risks or failures;
- documentation updated.

Do not begin, design, or offer the next task.
```

## Human use

Before sending the prompt:

- verify that `PROJECT_STATE.yaml` names the same task;
- verify that all dependencies are `done`;
- check that the task has objective acceptance criteria;
- attach or expose the repository to the coding agent;
- do not select two tasks to save a session.
