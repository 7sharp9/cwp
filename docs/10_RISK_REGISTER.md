# Risk Register

Status scale: `open`, `watch`, `mitigating`, `accepted`, `closed`  
Probability and impact: 1 low to 5 high

| ID | Risk | P | I | Early trigger | Mitigation | Contingency | Status |
|---|---|---:|---:|---|---|---|---|
| R-001 | Agent refusal feels like broken input or arbitrary loss of control | 4 | 5 | testers cannot predict or correct a refusal | structured reasons, order preview, reversible canonical scenario, limited variables | replace refusal with delay/adaptation or reduce autonomy | open |
| R-002 | Framework choice reflects language preference rather than delivery evidence | 4 | 4 | candidate-specific requirements or unequal spikes | same simulation, map, task, metrics, and time-boxed implementation | accept ADR using observed costs; stop maintaining losing host | closed |
| R-003 | Mibo changes rapidly or exposes correctness defects | 3 | 4 | breaking upgrade, stale-read issue, adaptive graph inconsistency; Mibo recorded as a live reconsideration option (ADR-0003 2026-09-06) | pin exact version; use classic MVU (`Mibo.Mvu`) only; keep simulation independent | replace Mibo host with direct backend host or Godot | watch |
| R-004 | Godot C# to F# boundary creates friction and debugging opacity | 3 | 3 | excessive DTOs, reflection, hot-reload failures, stack traces crossing layers | ADR-0004 (2026-09-03, TASK-007): thin C# shim per scene entry point over an F# client-core library; sim facade in F#; only primitives / Nullable / [<CLIMutable>] records cross; C#->F# stack traces verified readable. F# types are not scene entry points (proven). | keep C# shim minimal and push logic into F#; if friction is severe, revisit at G4 with measured evidence | mitigating |
| R-005 | Raw MonoGame or raylib path turns game work into engine and tooling work | 4 | 4 | custom editor, UI framework, asset pipeline, or scene system appears early | use Mibo and Tiled for the spike; count infrastructure code | choose Godot or sharply reduce presentation scope | closed |
| R-006 | Architecture becomes the hobby and delays playable evidence | 5 | 5 | new abstractions without vertical-slice acceptance criteria | one active task, forbidden scope list, evidence gates, review checklist | remove unused abstractions and return to canonical scenario | mitigating |
| R-007 | AI design is too broad to debug or tune | 4 | 5 | HTN, GOAP, broad utility, large behaviour tree, or per-agent world model enters early | explicit staged appraisal, typed commitments, finite executors, shared tactical knowledge | reduce outcomes to accept/delay and rebuild incrementally | mitigating |
| R-008 | Different modules disagree because order and state semantics are duplicated | 4 | 4 | UI, tests, and simulation each define their own command meaning | one authoritative type and behavioural specification per concern | stop feature work and consolidate before proceeding | open |
| R-009 | Determinism is claimed but breaks through ordering, random, numeric, or concurrency behaviour | 4 | 5 | replay hash differs without an intentional change | stable ordering, project PRNG, golden vectors, integer ticks, canonical hashes | narrow the determinism contract and localise divergence before adding features | open |
| R-010 | Pathfinding, formation, and local avoidance dominate development | 4 | 4 | deadlocks, oscillation, or repeated full-map replanning | grid A*, squad corridor, fixed formation slots, short reservation horizon | reduce formation fidelity and cap agents while preserving command test | open |
| R-011 | Isometric art and animation cost exceeds product value | 4 | 4 | one acceptable soldier consumes disproportionate cleanup time | placeholders, one-directional asset spike, shared rigs, measured pre-render pipeline | simplify style, use fewer facings, or retain 3D rendered at low resolution | open |
| R-012 | Retro presentation harms readability or accessibility | 3 | 4 | players cannot distinguish cover, selection, threat, or text | native-resolution UI, non-colour cues, optional effects, scale tests | relax palette and resolution constraints | open |
| R-013 | Content pipeline lacks useful validation and creates opaque runtime bugs | 3 | 4 | bad marker or property fails during gameplay | typed import boundary, explicit schema version, load-time errors | add a separate validation command before further content | open |
| R-014 | Performance work starts too early or allocations become entrenched | 3 | 3 | speculative data-oriented rewrite or unexplained per-tick allocation | benchmark first, array-backed hot stores only where measured | optimise isolated stores without changing domain API | watch |
| R-015 | Coding agents cause architecture and documentation drift | 5 | 4 | multiple active tasks, duplicated status, unrequested refactors | AGENTS.md, bounded task files, source-of-truth table, progress evidence | reject change and restore consistency without accepting partial architecture | mitigating |
| R-016 | Dependency upgrades destabilise the project | 3 | 3 | unplanned major-version update or unlocked restore changes output | pin SDK and package versions after baseline; upgrade in dedicated task | roll back dependency change, not unrelated source work | open |
| R-017 | Save or replay compatibility becomes a hidden permanent burden | 3 | 3 | every state change requires migration machinery | version formats; support current development corpus only until production decision | fail explicitly on unsupported versions and regenerate fixtures | accepted |
| R-018 | The historical title or visual material creates intellectual-property exposure | 2 | 5 | public branding or copied asset resembles original material | original title, setting details, code, art, maps, text, and audio | legal review or rebrand before public release | open |
| R-019 | Research ambitions distort the game before it is enjoyable | 4 | 4 | instrumentation or formal claims drive mechanics before playtest | ordinary telemetry only before G5; separate research fork after stable game branch | remove study-specific code and reassess product | mitigating |
| R-020 | Small external playtest produces false confidence | 3 | 4 | strong conclusion drawn from five convenient testers | treat threshold as heuristic, record dissent and behaviour, broaden later | run a larger structured study only after production decision | accepted |
| R-021 | Scope expands to vehicles, campaign, multiple eras, multiplayer, or modding | 5 | 5 | task appears before core gates pass | prohibited-scope list and ADR requirement | reject or park in post-G6 ideas document | mitigating |
| R-022 | Single-developer continuity is lost across long gaps or agent sessions | 4 | 4 | undocumented decisions, unreproducible setup, unclear active work | project state, locked tasks, progress ledger, scripted build/test commands | perform a rehydration audit before continuing implementation | open |
| R-023 | Enemy AI cheats, undermining the value of perception and command | 3 | 4 | enemy targets unseen agents or reacts to hidden commands | same observation contract, scenario tests, known-versus-authoritative overlay | simplify enemy behaviour rather than grant global state | open |
| R-024 | The easiest strategy is always choosing the most obedient soldier | 4 | 5 | personality becomes a sortable compliance stat | capability trade-offs, situational appraisal, team-level commands, test observation | remove direct individual selection for some intents or rebalance traits | open |
| R-025 | Explanation UI exposes numbers but not causality | 4 | 4 | players read scores yet cannot choose a correction | primary reason plus suggested tactical lever; trace remains developer-only | redesign explanation around counterfactual action | open |

## Review cadence

Review this register:

- at every gate;
- when a task uncovers a new architectural or product threat;
- before dependency or framework upgrades;
- after each external playtest round.

Do not lower probability or impact merely because mitigation work has started. Change the status and record evidence in the progress ledger.
