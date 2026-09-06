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
| TASK-003 | P1/P2 | Establish deterministic random, hash, and replay harness | M | TASK-002 | done | `tasks/TASK-003-DETERMINISM-HARNESS.md` |
| TASK-004 | P1 | Implement disposable Godot .NET framework spike | M | TASK-003 | done | `tasks/TASK-004-GODOT-SPIKE.md` |
| TASK-005 | P1 | Implement disposable Mibo plus raylib framework spike (trimmed; pinned to Mibo 4.1.0 per ADR-0003 2026-09-02 amendment) | S | TASK-004, ADR-0003 amendment | done | `tasks/TASK-005-MIBO-SPIKE.md` |
| TASK-006 | P1 | Evaluate spikes and accept framework ADR | S | TASK-004, TASK-005 | done | `tasks/TASK-006-FRAMEWORK-DECISION.md` |
| TASK-007 | P2 | Design the low-impedance Godot C#/F# client boundary | S | TASK-006 | done | `tasks/TASK-007-GODOT-FSHARP-BOUNDARY.md` |
| TASK-008 | P2 | Define and validate the initial scenario DTO and content version | S | TASK-007 | done | `tasks/TASK-008-SCENARIO-DTO-AND-CONTENT-VERSION.md` |
| TASK-009 | P2 | Restructure the progress ledger into a summary index plus per-entry detail files | S | TASK-008 | done | `tasks/TASK-009-PROGRESS-LEDGER-RESTRUCTURE.md` |
| TASK-010 | P2 | Implement the authoritative terrain grid: elevation, passability, movement cost, directional cover | M | TASK-008 | done | `tasks/TASK-010-TERRAIN-GRID.md` |
| TASK-011 | P2 | Headless diagnostic visualisation and the framework-neutral diagnostic frame | M | TASK-010 | done | `tasks/TASK-011-DIAGNOSTIC-VISUALISATION.md` |
| TASK-012 | P2 | Deterministic line of sight and opacity | M | TASK-010 | done | `tasks/TASK-012-LINE-OF-SIGHT.md` |
| TASK-013 | P2 | Deterministic grid pathfinding with stable tie-breaking | M | TASK-010 | done | `tasks/TASK-013-PATHFINDING.md` |
| TASK-014 | P2 | Headless performance and allocation benchmark harness | S | TASK-010 | done | `tasks/TASK-014-BENCHMARK-HARNESS.md` |
| TASK-015 | P2 | Navigation and movement phase: `Pathfinding`-driven single-agent executor (scoped; formation slots and multi-agent reservation deferred to B-011b) | M | TASK-013 | done | `tasks/TASK-015-MOVEMENT-EXECUTOR.md` |
| TASK-016 | P2 | Divergence diagnostics and replay corpus infrastructure (committed multi-entry corpus, in-suite determinism check, `cwheadless corpus` verb; generative property tests and component subhashes deferred to B-012b) | M | TASK-015 | done | `tasks/TASK-016-REPLAY-CORPUS.md` |
| TASK-017 | P2 | Same-tick cell reservation and deadlock avoidance in the Navigation and movement phase (priority by remaining route length, ties broken by agent id; a new `MovementYielded` event and `Reserved` diagnostic overlay; formation slots and sub-cell movement progress deferred to B-011c) | M | TASK-015 | done | `tasks/TASK-017-CELL-RESERVATION.md` |
| TASK-018 | P2 | Sub-cell movement progress within an edge (`AgentState.Progress`, threshold = `Terrain.moveCost`, increment = `Terrain.BaseMoveCost`; `Canonical.FormatVersion` bumped to 2; reservation generalised to "would complete this tick"; new `slow-terrain` corpus entry; formation slots deferred to B-011d) | M | TASK-017 | done | `tasks/TASK-018-SUBCELL-MOVEMENT-PROGRESS.md` |
| TASK-019 | P2 | Generative determinism property tests (`FsCheck`/`FsCheck.Xunit` 3.3.4, test project only): determinism under random commands, no agent on an invalid cell after movement, pathfinding endpoints/cost on random terrain; component-level subhashes deferred, staying in B-012b | M | TASK-016, TASK-018 | done | `tasks/TASK-019-DETERMINISM-PROPERTY-TESTS.md` |
| TASK-020 | P3 | Command validation and recipient selection (`PlayerCommand.Recipients: AgentId list` generalised from a single agent, back-compatible `Command.moveTo`; `Urgency`/`RiskTolerance` envelope fields, inert until B-017; new `CommandRejection` cases `EmptyRecipients`, `UnauthorisedRecipient`, batch-scoped `DuplicateCommandId`; no `Canonical.FormatVersion` or `CommandLogFile.Version` change) | S | B-011 | ready | `tasks/TASK-020-COMMAND-VALIDATION-AND-RECIPIENTS.md` |

## 3. Planned simulation work

These items are not implementation-ready. Create and review a task file before selecting one.

| ID | Phase | Work item | Size | Dependencies | Gate | Status |
|---|---|---|---|---|---|---|
| B-007 | P2 | Define and validate initial scenario DTO and content version | S | TASK-006 | G2 | done (`tasks/TASK-008-SCENARIO-DTO-AND-CONTENT-VERSION.md`) |
| B-008 | P2 | Implement terrain grid, elevation, passability, and directional cover | M | B-007 | G2 | done (`tasks/TASK-010-TERRAIN-GRID.md`) |
| B-012a | P2 | Headless diagnostic visualisation and the framework-neutral diagnostic frame | M | B-008 | G2 | done (`tasks/TASK-011-DIAGNOSTIC-VISUALISATION.md`) |
| B-009 | P2 | Implement deterministic line of sight and opacity | M | B-008 | G2 | done (`tasks/TASK-012-LINE-OF-SIGHT.md`) |
| B-010 | P2 | Implement deterministic grid pathfinding with stable tie-breaking | M | B-008 | G2 | done (`tasks/TASK-013-PATHFINDING.md`) |
| B-011 | P2 | Implement movement, formation slots, and short-horizon cell reservation. First phase consumer of the TASK-013 `Pathfinding` module: the Navigation and movement phase (12.7) replaces `PlaceholderMovement` with a `Pathfinding`-driven executor. | L | B-010 | G2 | done (`tasks/TASK-015-MOVEMENT-EXECUTOR.md`; single-agent executor only) |
| B-011b | P2 | Multi-agent movement: short-horizon cell reservation and deadlock avoidance. Split from B-011 by TASK-015 (single-agent executor only); narrowed to reservation alone by TASK-017, which split formation slots and sub-cell movement progress out to B-011c. | M | B-011 | G2 | done (`tasks/TASK-017-CELL-RESERVATION.md`) |
| B-011c | P2 | Sub-cell movement progress within an edge (`AgentState.Progress`; threshold `Terrain.moveCost`, increment `Terrain.BaseMoveCost`). New per-tick authoritative state with no derivation path from `Position` alone. Narrowed from "formation slots and sub-cell progress" to progress alone by TASK-018, which split formation slots out to B-011d. | M | B-011b | G2 | done (`tasks/TASK-018-SUBCELL-MOVEMENT-PROGRESS.md`) |
| B-011d | P2 | Formation slots: a squad/formation grouping and per-agent slot assignment do not exist anywhere yet (closer to `docs/05_COMMAND_AND_AGENT_AI.md` than to movement mechanics). Split from B-011c by TASK-018, which landed sub-cell movement progress only. | M | B-011c | G2 | proposed |
| B-012 | P2 | Add divergence diagnostics and replay corpus infrastructure: a committed multi-entry replay corpus, an in-suite determinism check, and a `cwheadless corpus` localisation verb. | M | B-008 | G2 | done (`tasks/TASK-016-REPLAY-CORPUS.md`) |
| B-012b | P2 | Component-level per-agent subhashes in the divergence report. Split from B-012 by TASK-016; narrowed by TASK-019, which landed the generative/FsCheck determinism property tests alone — `Canonical.firstDifferingSection` already reports the first differing `Agent[id]` by direct comparison, and the stated justification for subhashes (desirable "once the world grows") has no evidence yet (nothing has run past ~50 agents, `content/benchmarks/BASELINE.md`). Reopen only if a future benchmark or property-test counterexample shows the existing per-agent diff is inadequate. | S | B-012 | G2 | proposed |
| B-013 | P2 | Add headless performance and allocation benchmark harness | S | B-008 | G2 | done (`tasks/TASK-014-BENCHMARK-HARNESS.md`) |
| B-014 | P3 | Define command validation and recipient selection | S | B-011 | G3 | ready (`tasks/TASK-020-COMMAND-VALIDATION-AND-RECIPIENTS.md`) |
| B-015 | P3 | Implement observations and shared squad tactical knowledge (first phase consumer of the TASK-012 `Sight` module: Perception, phase 12.3, reads `Sight.trace` / `Sight.visible`) | M | B-009 | G3 | proposed |
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
| B-043 | P4 | Mibo 5.x production reconsideration spike (ADR-0001 decision review). Re-score the framework comparison for the Mibo classic-MVU route now that Mibo 5.0.0 re-separates `Mibo.Mvu` from `Mibo.Adaptive.Mibo` (ADR-0003 2026-09-06 amendment) and the packaging blocker is gone. Gated on the ADR-0001 review-trigger-1 editor edit-to-visible-result measurement. Outcome: an ADR-0001 amendment keeping Godot, or a new ADR selecting Mibo. Resolve before B-024 commits framework-specific client work. | S | ADR-0003 2026-09-06 amendment; ADR-0001 review trigger 1 | G4 | proposed |
| B-024 | P4 | Implement selected-framework content importer and validation command | M | B-007, TASK-006, B-043 | G4 | proposed |
| B-025 | P4 | Build Bridgehead greybox map | M | B-024 | G4 | proposed |
| B-026 | P4 | Implement selection, input mapping, tactical pause, and command preview | M | B-014, TASK-006 | G4 | proposed |
| B-027 | P4 | Implement snapshot rendering and isometric depth ordering | M | B-011, TASK-006 | G4 | proposed |
| B-028 | P4 | Implement player-facing reason and disposition UI | M | B-017, B-026 | G4 | proposed |
| B-029 | P4 | Render the DiagnosticFrame in Godot (developer perception, appraisal, reservation, and hash overlays) | M | B-012a, B-012, B-017, B-027 | G4 | proposed |
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
