# Controlled Backlog

Status: authoritative work tracker  
Rule: only one task may be `active`

## 1. Status definitions

- `proposed`: idea recorded but not yet bounded for implementation.
- `ready`: dependencies satisfied and a complete task file exists.
- `active`: the single task currently selected in `PROJECT_STATE.yaml`.
- `blocked`: cannot proceed; blocker is named.
- `review`: implementation complete but evidence not yet accepted by Dave.
- `done`: acceptance criteria and evidence accepted.
- `rejected`: deliberately not proceeding.
- `parked`: outside the current gate or scope.

A backlog row is not sufficient authority for an agent to implement work. A `ready` or `active` item must have a task file under `tasks/`.

## 2. Current work

| ID | Phase | Task | Size | Dependencies | Status | Task file |
|---|---|---|---|---|---|---|
| TASK-001 | P0 | Establish repository and build baseline | S | project pack | done | `tasks/TASK-001-REPOSITORY-BASELINE.md` |
| TASK-002 | P1 | Create framework-neutral simulation skeleton | S | TASK-001 | done | `tasks/TASK-002-SIMULATION-SKELETON.md` |
| TASK-003 | P1/P2 | Establish deterministic random, hash, and replay harness | M | TASK-002 | review | `tasks/TASK-003-DETERMINISM-HARNESS.md` |
| TASK-004 | P1 | Implement disposable Godot .NET framework spike | M | TASK-003 | ready after dependency | `tasks/TASK-004-GODOT-SPIKE.md` |
| TASK-005 | P1 | Implement disposable Mibo plus raylib framework spike | M | TASK-003 | ready after dependency | `tasks/TASK-005-MIBO-SPIKE.md` |
| TASK-006 | P1 | Evaluate spikes and accept framework ADR | S | TASK-004, TASK-005 | ready after dependencies | `tasks/TASK-006-FRAMEWORK-DECISION.md` |

## 3. Planned simulation work

These items are not implementation-ready. Create and review a task file before selecting one.

| ID | Phase | Work item | Size | Dependencies | Gate | Status |
|---|---|---|---|---|---|---|
| B-007 | P2 | Define and validate initial scenario DTO and content version | S | TASK-006 | G2 | proposed |
| B-008 | P2 | Implement terrain grid, elevation, passability, and directional cover | M | B-007 | G2 | proposed |
| B-009 | P2 | Implement deterministic line of sight and opacity | M | B-008 | G2 | proposed |
| B-010 | P2 | Implement deterministic grid pathfinding with stable tie-breaking | M | B-008 | G2 | proposed |
| B-011 | P2 | Implement movement, formation slots, and short-horizon cell reservation | L | B-010 | G2 | proposed |
| B-012 | P2 | Add divergence diagnostics and replay corpus infrastructure | M | B-008 | G2 | proposed |
| B-013 | P2 | Add headless performance and allocation benchmark harness | S | B-008 | G2 | proposed |
| B-014 | P3 | Define command validation and recipient selection | S | B-011 | G3 | proposed |
| B-015 | P3 | Implement observations and shared squad tactical knowledge | M | B-009 | G3 | proposed |
| B-016 | P3 | Implement communication constraints and report aging | M | B-015 | G3 | proposed |
| B-017 | P3 | Implement staged order appraisal and typed reasons | M | B-014, B-016 | G3 | proposed |
| B-018 | P3 | Implement commitments and finite movement/hold executors | M | B-011, B-017 | G3 | proposed |
| B-019 | P3 | Implement basic hitscan combat and directional cover effects | M | B-009, B-011 | G3 | proposed |
| B-020 | P3 | Implement suppression and exposure model | M | B-019 | G3 | proposed |
| B-021 | P3 | Implement stress, discipline, trust, and bounded reappraisal | M | B-017, B-020 | G3 | proposed |
| B-022 | P3 | Implement simple enemy hold-and-defend doctrine | M | B-015, B-019 | G3 | proposed |
| B-023 | P3 | Implement canonical refusal and correction scenario | M | B-018, B-020, B-021, B-022 | G3 | proposed |

## 4. Planned client and mission work

| ID | Phase | Work item | Size | Dependencies | Gate | Status |
|---|---|---|---|---|---|---|
| B-024 | P4 | Implement selected-framework content importer and validation command | M | B-007, TASK-006 | G4 | proposed |
| B-025 | P4 | Build Bridgehead greybox map | M | B-024 | G4 | proposed |
| B-026 | P4 | Implement selection, input mapping, tactical pause, and command preview | M | B-014, TASK-006 | G4 | proposed |
| B-027 | P4 | Implement snapshot rendering and isometric depth ordering | M | B-011, TASK-006 | G4 | proposed |
| B-028 | P4 | Implement player-facing reason and disposition UI | M | B-017, B-026 | G4 | proposed |
| B-029 | P4 | Implement developer perception, appraisal, reservation, and hash overlays | M | B-012, B-017, B-027 | G4 | proposed |
| B-030 | P4 | Implement suppress and assault command executors | L | B-018, B-020 | G4 | proposed |
| B-031 | P4 | Implement casualties, leadership succession, and squad failure | M | B-019, B-021 | G4 | proposed |
| B-032 | P4 | Implement demolition objective, extraction, success, and failure | M | B-025, B-030, B-031 | G4 | proposed |
| B-033 | P4 | Implement replay playback and mission summary | M | B-012, B-032 | G4 | proposed |
| B-034 | P4 | Produce minimal coherent placeholder or test art set | M | B-027 | G4 | proposed |
| B-035 | P4 | Integrate and verify complete vertical slice | L | B-025 through B-034 | G4 | proposed |

## 5. Planned playtest and decision work

| ID | Phase | Work item | Size | Dependencies | Gate | Status |
|---|---|---|---|---|---|---|
| B-036 | P5 | Prepare clean build, neutral playtest script, and issue form | S | B-035 | G5 | proposed |
| B-037 | P5 | Run first external comprehension and control playtest | M | B-036 | G5 | proposed |
| B-038 | P5 | Analyse logs, observations, and recurring failures | S | B-037 | G5 | proposed |
| B-039 | P5 | Implement first focused interaction revision | M | B-038 | G5 | proposed |
| B-040 | P5 | Run second test only if first-gate failures justify it | M | B-039 | G5 | proposed |
| B-041 | P6 | Evaluate product, technical, content, and research evidence | M | G5 result | G6 | proposed |
| B-042 | P6 | Record proceed, re-scope, research-fork, or stop decision | S | B-041 | G6 | proposed |

## 6. Parked ideas

These are intentionally unavailable before G6 unless the charter and gates are changed through an ADR.

| Idea | Reason parked |
|---|---|
| vehicles | multiplies movement, occupancy, animation, combat, and AI states |
| multiplayer | requires a stronger determinism and networking contract |
| multiple eras or Action Concept-style packs | repeats the original project's platform-before-game mistake |
| campaign progression | cannot be designed responsibly before one mission is enjoyable |
| pairwise relationships | adds state and explanations without proving the core loop |
| procedural maps | conflicts with controlled tactical and appraisal testing |
| general mod scripting | weakens validation and expands compatibility obligations |
| general HTN or GOAP | unnecessary search and debugging surface for five intents |
| Mibo.Adaptive | experimental complexity not needed for the slice |
| runtime LLM agents | nondeterministic, difficult to test, and irrelevant to the first product hypothesis |
| formal human-subject research instrumentation | requires a stable game and separate ethics/method plan |

## 7. Selecting the next task

Dave should select the next task only when:

1. all dependencies are `done`;
2. a complete task file exists;
3. acceptance criteria are objective;
4. prohibited scope is explicit;
5. the required evidence can be produced locally;
6. `PROJECT_STATE.yaml` is updated to name that task and no other.
