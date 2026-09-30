# 15. G4 known-defect triage

Realises backlog B-080 (TASK-081). Supplies the G4 evidence bullet "known
defects are triaged by severity" (`docs/08_ROADMAP_AND_GATES.md` section 7).
**This document does not record G4 as passed.** The gate call is Dave's.
Dispositions below are proposals for Dave, not decisions.

Snapshot: 2026-09-30, at the TASK-080 acceptance commit (`a3607ac`).

## 1. Scale and method

Severity reuses the `docs/14` section 7 scale:

- **blocker**: crash, data loss, or the mission cannot proceed or be completed.
- **major**: recurs, or blocks a goal, or could make a tester misread the core
  interaction.
- **minor**: everything else.

Disposition: **fix before G5**, **accept for G5** (observe it in the playtest),
or **defer past G5**.

Sources searched: `docs/10`, `docs/14` section 8, `docs/07` section 9, the
`docs/11` backlog rows, `docs/12` and every `docs/ledger/` detail file from
TASK-070 to TASK-080, and the task files `TASK-070` to `TASK-080`. A defect
that no record mentions is not in this list; the external playtest is expected
to add to it.

Each entry that makes a code claim cites a file and line read on the snapshot
commit. An entry marked `unconfirmed` was taken from a record and not re-read.

Result: **no blocker is recorded in any source searched.** That is the absence
of a record, not proof that none exists (see section 4).

## 2. Open defects

| ID | Severity | Category | Defect | Source | Evidence it is open | Proposed disposition |
|---|---|---|---|---|---|---|
| D-01 | major | balance / comprehension | Pathfinding is threat-blind and a new contact is reappraised one tick after it is observed, so a single un-staged deep `MoveTo` on Bridgehead can be shot before the refusal lands. A staged two-step order avoids it. | TASK-073 ledger "Deviations"; TASK-075 ledger (probe 6) | `Appraisal.fs:21-22`: stage 2 is `Pathfinding.findWithin`, no exposure input. Both tasks reproduced the late refusal against the real pipeline. | accept for G5; this is the main thing the playtest should observe against R-001 |
| D-02 | minor | gameplay | An `Extracted` agent stays `Alive` and keeps its cell, and an extraction cell holds one agent, so a further survivor ordered to an occupied extraction cell stalls to `MovementAbandoned`. Two extraction cells mitigate it. | TASK-073 ledger; B-071 row | `Simulation.fs:1574` (`occupantOf` excludes only non-`Alive`), `Simulation.fs:2199` (`Extracted` is a sticky flag, position untouched) | accept for G5 |
| D-03 | minor | gameplay | Reappraisal fires on any `ContactObserved` / `ContactExpired`, including events in the hostile picture, so a hostile noticing a friendly reappraises friendly orders. No outcome changes are known. | TASK-078 ledger finding 2 | recorded by TASK-078 as observation only; not re-read (`unconfirmed`) | defer past G5 |
| D-04 | minor | presentation | The HUD status line always shows tick, 64-bit state hash, random draws and agent count. Testers may ask what it means, and the facilitator must not answer. | `docs/14` section 8 item 1 | `CommandDemoScene.fs:1597` | fix before G5 (Dave's presentation call) |
| D-05 | minor | presentation | The window and project title reads "CommandoWar Godot Spike" and is visible to participants. Public title and branding are out of scope under `AGENTS.md`. | `docs/14` section 8 item 2 | `project.godot:13` | Dave decides; fix before G5 only if a neutral working title is chosen |
| D-06 | minor | control / validity | The `F1` developer overlay is reachable by any participant. It shows the trace behind a refusal, which G5 must test testers can do without. | `docs/14` section 8 item 6 | `FSharpSceneHost.cs:681` binds `Key.F1` unconditionally | fix before G5 (facilitator script forbids mentioning it, which a discovery would defeat) |
| D-07 | minor | control | No in-game restart; a participant retries by relaunching. | `docs/14` section 8 item 4 | grep of `src/CommandoWar.Client.Godot` for restart/reload found nothing | accept for G5 |
| D-08 | minor | analysis | The client cannot open a session file; analysing a session needs the .NET SDK and this repository. | `docs/14` section 8 item 3; TASK-080 ledger | `render` accepts only the legacy `.cwlog` (recorded by TASK-080; not re-read, `unconfirmed`) | accept for G5 |
| D-09 | minor | presentation | The weapon-range square (`WeaponRange = 7`) is 1232 x 616 px against a 1280 x 800 viewport with no zoom or pan, so its corners clip for most agents, and it is dimmer (alpha 0.45, 2 px) than the other markers. | TASK-074 ledger "Deviations" | measured by TASK-074 with a real screenshot; camera scale not re-checked since (`unconfirmed`) | accept for G5; check in the playtest whether the range reads |
| D-10 | minor | tooling | `pending` commands get `Sequence = pending.Count`, which can repeat after a stale entry is removed. Delivery order is by list position and the recording re-indexes, so nothing is affected today. | TASK-080 ledger item 8 | `CommandDemoScene.fs:1756` | defer past G5 |
| D-11 | minor | documentation | `PROJECT_STATE.yaml` does not parse as YAML: PyYAML fails at line 8, column 2906 ("mapping values are not allowed here"), inside the long `updated:` value. No tool reads it today. | TASK-077 and TASK-080 ledgers | re-run on the snapshot: `yaml.safe_load` raises the same error | defer past G5; a one-line quote would fix it |
| D-12 | minor | documentation | `chokepoint-detour` prose disagrees between its two sources: `Corpus.fs:1235` says the agent reaches (4,0) "by tick 7", `content/replays/chokepoint-detour.md:3` says "by tick 6". Hashes are unaffected, but `corpus --regenerate` would rewrite the prose. Which tick is right was not checked. | found by this triage | both lines read | defer past G5 |

## 3. Deliberate limitations (scope decisions, not defects)

Recorded so a tester finding is not mistaken for a regression.

| ID | Limitation | Where decided |
|---|---|---|
| L-01 | No stage-5 "safer adaptation": an order is `Accepted` or `Refused`, never `Adapted`. | `Appraisal.fs:34`; B-018 |
| L-02 | Hostiles hold and shoot. Suppress-likely-routes, seek-cover and fall-back doctrine were descoped. | B-022 row; TASK-037 |
| L-03 | Reappraisal covers a new order, knowledge change, suppression band and squad-wide suppression change. Exposure-band, wounded, support and leadership triggers and dynamic trust are out. | B-021 row; TASK-033 |
| L-04 | No slow motion while issuing orders; pause and the auto-pause on refusal exist. | `docs/07` section 6 |
| L-05 | Bridgehead is the only mission. Reaching `Succeeded` needs both depot riflemen engaged from off-objective cells, and a refusal needs a staged order. Discoverability by a first-time player is untested. | `docs/07` section 9; B-071 |
| L-06 | Risks R-001, R-024 and R-025 (refusal feels arbitrary, obedient-soldier strategy, explanation shows numbers not causes) cannot be judged before G5. | `docs/10` |

## 4. Unverified in this environment

Not triaged as defects: they could not be exercised in the session that wrote
this list (no Godot editor, no export templates, no pinned SDK). Each one
could hide a blocker.

| ID | Item | Why unverified |
|---|---|---|
| U-01 | The Godot export builds, launches from a folder outside the repository and writes a session file. | No distributable was ever produced; the `docs/14` section 2 checklist is Dave's and `TASK-080` line 117 is unticked. |
| U-02 | `FSharpSceneHost.ResolveContentPath` finds `content/` beside an exported executable. | Compiles only under the Godot SDK; never run. |
| U-03 | Per-launch session recording in the play scene, including on macOS. | Code is `CommandDemoScene.fs:318` and `:353`; CI does not compile the Godot Core or the C# host, and no committed test covers it. |
| U-04 | Raw mouse gestures: rubber-band multi-select (TASK-068) and replay scrubber drag (TASK-071); TASK-072's windowed screenshot criterion is unticked. | Accepted on probe evidence; no windowed session recorded for them (`unconfirmed` for the current build). |
| U-05 | The 60 fps and frame-pacing budgets in `docs/07` section 10. | The text still says "no client yet". A stable 60 fps was seen once by a temporary diagnostic in TASK-069 that was removed; nothing is committed. |
| U-06 | "The mission can be completed from a clean build without developer commands" (G4 evidence). | `bridgehead-succeeded` proves a `MoveTo`-only replay reaches `Succeeded`; a human doing it in the exported build is U-01. |

## 5. Excluded: recorded and since fixed

| Defect | Fixed by |
|---|---|
| Corpses blocked movement forever | TASK-066 |
| Frozen movement retried forever | TASK-065 |
| A solo order was pulled to a formation slot | TASK-067 |
| Multi-select and joint order dispatch | TASK-068 |
| Bridgehead troopers moved at twice the intended speed | TASK-069 |
| Chokepoint jam behind a parked agent | TASK-070 |
| Two detouring agents obstructing each other (R-010 gap) | TASK-076; R-010 now `watch` |
| A refusal was easy to miss | TASK-072 |
| No stand-off cell on Bridgehead; single extraction cell | TASK-073 |
| `Failed` direction thought geometrically capped | TASK-075 |
| A dead or incapacitated observer kept perceiving | TASK-078 |
| `ExtractAgents` completed with nobody extracted | TASK-079 |
| No weapon-range display | TASK-074 (see D-09 for what remains) |

## 6. Correction to an earlier record

`docs/ledger/2026-09-29-TASK-079-*.md` finding 1 says scenario validation does
not reject an `ExtractAgents` id that names no agent. It does:
`Scenario.fs:680` reports `ExtractionSelectsUnknownAgent`, and
`ScenarioTests.fs:339` asserts it. The finding was wrong and is corrected in
place. It is not an open defect.

## 7. Counts and what is left for Dave

Open defects: 0 blocker, 1 major (D-01), 11 minor. Unverified: 6 items.

Decisions for Dave:

1. Accept or change the dispositions in section 2.
2. Whether to select a test-build presentation task for D-04, D-05 and D-06.
3. The neutral working title, if D-05 is to be fixed.
4. Whether to repair D-11 and D-12 now or leave them.
5. When to run the `docs/14` section 2 checklist (U-01 to U-03), since a failure
   there would outrank everything in section 2.
6. The G4 gate call itself.
