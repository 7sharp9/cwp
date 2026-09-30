# TASK-081: G4 known-defect triage

Status: review (self-verified 2026-09-30; not yet accepted)
Owner: coding agent
Phase: P4
Gate: G4 (evidence bullet "known defects are triaged by severity", `docs/08_ROADMAP_AND_GATES.md` section 7); realises B-080
Size: S

## Objective

One committed document, `docs/15_G4_DEFECT_TRIAGE.md`, lists every known open
defect and limitation in the current build with a severity, a recorded source,
evidence that it is still open, and a proposed disposition. It supplies the one
G4 evidence bullet that has no committed artefact. It does not call the gate.

## Why this task exists

`docs/08` section 7 requires "known defects are triaged by severity" for G4.
TASK-077 named that bullet as the remaining G4 input and left it to Dave. The
defects are scattered across `docs/10`, `docs/14` section 8, roughly ten ledger
detail files, backlog rows, and task files, and several have since been fixed.
A reader cannot tell today which are open. Dave selected this on 2026-09-30
("A: G4 defect triage") after being told no `ready` task existed and that B-037
is a human-run playtest.

## Required reading

- `PROJECT_STATE.yaml` (`active_work`; `gates`)
- `AGENTS.md`
- `docs/08_ROADMAP_AND_GATES.md` section 7 (G4 evidence)
- `docs/07_VERTICAL_SLICE.md` section 9 (criteria) and section 12
- `docs/10_RISK_REGISTER.md`
- `docs/14_PLAYTEST_KIT.md` sections 7 and 8 (severity scale, pre-session findings)
- `docs/ledger/2026-09-27-TASK-077-*.md`, `2026-09-29-TASK-079-*.md`,
  `2026-09-29-TASK-080-*.md` (the findings lists)

## Dependencies

- TASK-080 (done).

## Inputs and assumptions

- Severity reuses the `docs/14` section 7 scale (blocker, major, minor), with
  the definitions restated in the new document so it stands alone.
- Each entry is verified against the current code or a committed test where a
  code claim carries a severity. An entry that could not be verified is marked
  `unconfirmed`, not dropped and not asserted.
- Dispositions are **proposals** for Dave (`fix before G5`, `accept for G5`,
  `defer past G5`). Nothing here selects a follow-up task or records G4 as
  passed.
- The build environment has no Godot editor, no export templates, and no live
  playtest; items that need them are listed as `unverified here`, not triaged as
  defects.

## Allowed scope

- New `docs/15_G4_DEFECT_TRIAGE.md`.
- One pointer line under the G4 evidence bullet in `docs/08`.
- Correcting the one factual error found while verifying (TASK-079 ledger
  finding 1, see Required work 3), per the ledger's own correction rule.
- The required task, backlog, ledger, and `PROJECT_STATE.yaml` documentation.

## Forbidden scope

- Any change under `src/`, `tests/`, `content/`, or `.github/`.
- Fixing any listed defect, including the `PROJECT_STATE.yaml` YAML parse error
  and the `chokepoint-detour` prose drift. They are recorded, not repaired.
- Selecting or drafting a follow-up task, or calling G4.
- Lowering a risk in `docs/10`.

## Required work

1. Collect candidates from the sources under Required reading and the
   backlog and ledger; discard anything a later task fixed, citing the task.
2. Verify each retained entry that makes a code claim against the current
   source or tests; record the file and line.
3. Correct the TASK-079 ledger finding that says scenario validation does not
   reject an unknown `SpecificAgents` id (it does:
   `Scenario.fs` `ExtractionSelectsUnknownAgent`, `ScenarioTests.fs`).
4. Write `docs/15_G4_DEFECT_TRIAGE.md`: scale, method, table of open defects,
   table of unverified-here items, table of fixed items excluded, summary
   counts, and the decisions left to Dave.
5. Link it from `docs/08` section 7.

## Acceptance criteria

- [x] Every entry has an ID, severity, category, source, evidence of being open,
      and a proposed disposition.
- [x] Every entry making a code claim cites a file and line read this session,
      or is marked `unconfirmed`.
- [x] Fixed defects are listed as excluded with the task that fixed them.
- [x] The document states that it does not record G4 as passed.
- [x] `git diff --stat` shows no path under `src/`, `tests/`, `content/`, or
      `.github/`.
- [x] `dotnet build`, `dotnet test`, and `-- corpus` are unaffected (recorded as
      not run: pinned SDK absent, NuGet blocked; diff scope is the evidence).
- [x] Required documentation was updated.

## Required verification

- diff scope: `git diff --stat` and `git status --short`.
- build and tests: `dotnet build CommandoWar.slnx`, `dotnet test`,
  `dotnet run --project src/CommandoWar.Headless -- corpus`, only to show the
  docs-only change moved nothing.
- dependency boundary: no source file changed, so no reference can have moved.

## Evidence to capture

- The commands above with results.
- The spot-check commands used to verify each code claim.
- Unresolved concerns, chiefly the `unconfirmed` entries.

## Expected files

- `docs/15_G4_DEFECT_TRIAGE.md` (new)
- `docs/08_ROADMAP_AND_GATES.md` (pointer)
- `docs/ledger/2026-09-29-TASK-079-*.md` (one correction)
- this task file, `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md` and a new
  `docs/ledger/2026-09-30-TASK-081-g4-defect-triage.md`, `PROJECT_STATE.yaml`

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md` (new B-080 row);
- `docs/12_PROGRESS_LEDGER.md` (index row) and the ledger detail file;
- `PROJECT_STATE.yaml` (`selected_task`, `task_file`, `note`).

## Rollback or removal

Delete `docs/15_G4_DEFECT_TRIAGE.md`, the `docs/08` pointer line, the ledger
correction, and the B-080 row. Nothing else depends on them.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next task.

## Review

Detail, commands and unresolved concerns: `docs/ledger/2026-09-30-TASK-081-g4-defect-triage.md`.
The `dotnet build`, `dotnet test` and `-- corpus` checks were **not run** (pinned SDK
absent, NuGet blocked); the diff has no `src/`, `tests/`, `content/` or `.github/` path.

Accepted: not yet.
