## 2026-09-30 - TASK-081 - G4 known-defect triage

**Owner:** Dave with coding-agent assistance
**Source revision:** TASK-080 accepted (`a3607ac`); branch
`claude/cool-hypatia-j4rswr`
**Environment:** Linux cloud container, .NET SDK `10.0.112` only (pinned
`10.0.303` not installed); no Godot install, no export templates
**Status change:** `ready -> review` (drafted and selected on Dave's "A: G4
defect triage", chosen after no `ready` task existed and B-037 was found to be
a human-run playtest)

### Changes

- New `docs/15_G4_DEFECT_TRIAGE.md`: severity scale (the `docs/14` section 7
  scale), 12 open defects (1 major, 11 minor), 6 deliberate limitations, 6
  unverified-here items, 13 excluded fixed defects, one correction, and the
  decisions left to Dave.
- `docs/08_ROADMAP_AND_GATES.md` section 7: the G4 bullet points at it.
- `docs/ledger/2026-09-29-TASK-079-*.md` finding 1 corrected (see below).
- No file under `src/`, `tests/`, `content/`, or `.github/` changed.

### Method

A read-only sweep of the sources listed in the task file produced a candidate
list. The sweep's claims were not taken as written; each entry that carries a
code claim was re-read. Results of that re-verification:

- Confirmed: `PROJECT_STATE.yaml` fails `yaml.safe_load` at line 8, column
  2906; `Corpus.fs:1235` says "tick 7" and `chokepoint-detour.md:3` says "tick
  6"; `occupantOf` filters on `Casualty.isAlive` only (`Simulation.fs:1574`);
  `Extracted` is a sticky flag that leaves position alone
  (`Simulation.fs:2199`); the HUD format string (`CommandDemoScene.fs:1597`);
  the project title (`project.godot:13`); the unconditional `F1` binding
  (`FSharpSceneHost.cs:681`); `Sequence = pending.Count`
  (`CommandDemoScene.fs:1756`); no restart handling in the client source.
- **Rejected:** TASK-079's ledger finding 1 (unknown `SpecificAgents` id is not
  validated). `Scenario.fs:680` reports `ExtractionSelectsUnknownAgent` and
  `ScenarioTests.fs:339` asserts it. Corrected in the TASK-079 detail file.
- **Rejected as evidence:** the sweep cited `Appraisal.fs:34` and
  `Simulation.fs:1121` for the pathing lag. Both describe the stage-5
  deferral. D-01 now cites `Appraisal.fs:21-22` (stage 2 is
  `Pathfinding.findWithin`) plus the TASK-073 and TASK-075 ledgers, which
  reproduced the late refusal against the real pipeline.
- **Rejected as a defect:** the `docs/07` section 10 "no client yet" text is
  stale, but the ADR-0001 2026-09-19 note the sweep pointed at measures editor
  authoring, not frame rate. Recorded as U-05, not as a defect.
- Left out for lack of evidence: "order-status text is single-selection only",
  "no in-UI reason an order stalls beyond `MovementAbandoned`". The sweep marked
  both unconfirmed and neither could be tied to a record.

### Commands and results

| Command | Result |
|---|---|
| `git status --short` | 4 modified (`PROJECT_STATE.yaml`, `docs/08`, `docs/11`, TASK-079 ledger), 2 new (`docs/15`, `TASK-081` task file) at that point |
| `git status --short \| grep -E ' (src\|tests\|content\|\.github)/'` | no match |
| `python3 -c "yaml.safe_load(...PROJECT_STATE.yaml)"` | fails at line 8, column 2906, as before; this task did not touch line 8 |
| `dotnet build` / `dotnet test` / `-- corpus` | **not run.** The pinned SDK `10.0.303` is not installed here (`global.json` refuses `10.0.112`) and NuGet is blocked by the session's network policy. The diff contains no source, test, content or project file, so nothing they cover can have changed; CI on push is the check. |

### Acceptance criteria

- Every entry has ID, severity, category, source, evidence, disposition: yes,
  sections 2 to 4 of `docs/15`.
- Code claims cite file and line, or are marked `unconfirmed`: yes. Three open
  defects rest on a record and are marked `unconfirmed` or "not re-read": D-03,
  D-08 and D-09; so does U-04 in the unverified table.
- Fixed defects listed with the fixing task: section 5.
- States it does not record G4 as passed: first paragraph.
- No `src/`, `tests/`, `content/`, `.github/` path in the diff: shown above.
- Build, tests, corpus unaffected: **not demonstrated by running them**; argued
  from the diff scope only.

### Unresolved concerns

1. The list is only as complete as the records. The sweep found no blocker, but
   U-01 to U-03 (export, host lookup, session recording) have never run and any
   of them could be one. Running the `docs/14` section 2 checklist is the
   cheapest way to learn.
2. D-01 is rated major on judgement: it bears on whether the playtest can show
   the refusal loop at all. Dave may rate it differently.
3. D-12: which of "tick 6" and "tick 7" is correct was not checked.
4. `PROJECT_STATE.yaml` line 8 (`project.updated`) was not refreshed for this
   task; only `active_work` was. Editing that line is what D-11 would touch.

### Documents updated

- `tasks/TASK-081-G4-DEFECT-TRIAGE.md` (`active -> review`)
- `docs/11_BACKLOG.md` (B-080 row, `active -> review`)
- `docs/15_G4_DEFECT_TRIAGE.md` (new); `docs/08_ROADMAP_AND_GATES.md`
- `docs/ledger/2026-09-29-TASK-079-*.md` (correction)
- `PROJECT_STATE.yaml` (`active_work`)
- `docs/12_PROGRESS_LEDGER.md` (index row) and this file. No "Pinned facts"
  value changed.

### Review

Accepted: yes (2026-10-03, "accept"), on the self-verification evidence. Not covered:
the docs-only diff was never built or tested locally (pinned SDK absent, NuGet blocked),
and acceptance does not call G4, which stays `pending`.
