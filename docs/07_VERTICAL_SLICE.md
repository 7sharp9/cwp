# Vertical Slice Definition

Status: draft  
Working scenario: Bridgehead

## 1. What the slice must prove

The slice exists to answer one product question:

> Can an agent's legible, reversible refusal make squad command more satisfying rather than less controllable?

It is not intended to prove a reusable engine, a campaign, a full AI architecture, or commercial demand.

## 2. Player experience target

A new player should be able to complete the mission in roughly 10 to 20 minutes after a short embedded tutorial. During that mission, the player should encounter at least one dangerous order that a soldier delays, adapts, or refuses for a visible tactical reason.

The player must be able to change that reason, for example by suppressing a machine-gun position or selecting a covered route, then reissue the order and obtain a predictable different response.

## 3. Scenario

### Setting

A compact industrial rail bridge and depot at dusk. The setting and visual identity must be original.

### Friendly force

- one squad leader controlled directly;
- five additional soldiers;
- two fireteams of three including the leader;
- one support weapon suitable for suppression;
- one demolition interaction owned by the squad rather than a detailed inventory system.

### Enemy force

- one machine-gun team controlling an exposed approach;
- four riflemen in prepared positions;
- no reinforcements;
- no vehicles;
- no omniscient knowledge.

### Objective sequence

1. Reach a position from which the squad can observe the bridge area.
2. Neutralise, bypass, or suppress the machine-gun threat.
3. Move a demolition-capable soldier to the bridge objective.
4. Hold the position while the charge is planted.
5. Withdraw surviving required personnel to extraction.
6. Detonate and complete the mission.

The implementation may simplify detonation timing if it distracts from the command loop.

## 4. Required player commands

Only these commands are required:

- `MoveTo`
- `HoldArea`
- `SuppressArea`
- `AssaultArea`
- `WithdrawTo`

Direct leader movement may use a separate immediate input command, but it must pass through the same authoritative simulation boundary.

## 5. Required simulation systems

- fixed integer tick;
- typed command validation;
- logical grid and directional cover;
- deterministic line of sight;
- shared squad tactical knowledge;
- basic enemy perception;
- pathfinding and local cell reservation;
- movement and formation slots;
- hitscan small-arms combat;
- suppression;
- stress, discipline, and leader trust;
- explicit order appraisal;
- accepted, delayed, adapted, refused, and broken outcomes;
- finite execution states;
- ordered reactive interrupts;
- leader succession;
- mission objectives and extraction;
- command recording, seeded replay, and canonical state hash;
- render snapshots and domain events.

## 6. Required presentation

- isometric map and sprites, placeholders permitted until the integration gate;
- tactical pause or slow motion while issuing orders;
- unit and fireteam selection;
- proposed path and destination;
- known threat presentation;
- acknowledgement and disposition feedback;
- concise refusal or adaptation reason;
- objective and extraction indicators;
- developer overlay for perception, appraisal, movement, and deterministic state;
- mission completion and failure summary;
- replay playback.

### Realised by TASK-011 (developer overlay foundation, headless)

The framework-neutral per-tick diagnostic model
(`src/CommandoWar.Sim/Diagnostics.fs`, `DiagnosticFrame`) and its deterministic
ASCII / SVG / HTML renderers (`src/CommandoWar.Headless/DiagnosticRender.fs`,
driven by `cwheadless render`) give the developer overlay its data foundation
before any client work. It covers the terrain grid, directional cover, agents
and destinations, this-tick events, and the tick / state-hash / random-draw
trio; perception, appraisal, commitment, and reservation state attach as
`Overlay` cases when those systems land (B-009 to B-011, B-019). The HTML
scrubber is the headless form of "the developer overlay can explain any
appraisal and major state transition" (functional acceptance criterion 11) for
the systems that currently exist. The Godot overlay (B-029) renders the same
frame.

## 7. Deliberate exclusions

The slice must not include:

- vehicles;
- multiplayer;
- procedural maps;
- campaign progression;
- inventory management;
- pairwise social relationships;
- civilians;
- stealth takedowns;
- destructible buildings;
- fire propagation;
- arbitrary scenario scripting;
- a public modding API;
- a general HTN, GOAP, or machine-learned planner;
- runtime LLMs;
- multiple factions or eras;
- final production art for content not present in the mission.

## 8. Canonical refusal sequence

The mission layout must reliably support this test sequence:

1. The player orders a cautious or stressed soldier across an exposed approach.
2. The soldier identifies a known machine-gun lane.
3. The order is delayed, adapted, or refused with `RouteTooExposed` or `UnsupportedAssault`.
4. The UI explains the main reason without exposing a meaningless raw score.
5. The player orders another fireteam to suppress the machine-gun position or selects a covered route.
6. Tactical knowledge and exposure are recalculated.
7. The player reissues the original intent.
8. The soldier accepts or adapts it consistently.

This sequence must be testable headlessly before presentation polish begins.

## 9. Functional acceptance criteria

The slice is feature-complete only when:

1. Every required command can be issued and resolved.
2. At least two soldiers can appraise the same order differently for traceable reasons.
3. The canonical refusal sequence passes with a fixed scenario and seed.
4. A player action can predictably change an appraisal outcome.
5. Enemy decisions use perceived or reported information rather than authoritative player positions.
6. Leader death transfers command according to an explicit rule.
7. The mission can succeed and fail without developer intervention.
8. Invalid content fails before the simulation begins with actionable diagnostics.
9. A recorded command stream reproduces the same final authoritative state under the stated determinism contract.
10. The headless runner can execute the mission scenario repeatedly without a graphical client.
11. The developer overlay can explain any appraisal and major state transition.
12. Placeholder-art play is understandable before final visual production.

## 10. Performance budgets

These are initial budgets and may be revised only with measured evidence.

- 20 authoritative simulation ticks per second.
- 60 graphical updates per second on the development workstation under normal conditions.
- 50 friendly and enemy agents in a synthetic stress scene, even though the slice uses fewer.
- no routine full-heap allocation proportional to total map size on each tick;
- no unbounded pathfinding or appraisal work in a single tick;
- stable frame pacing during command preview and debug overlays;
- headless execution at substantially faster than real time.

Exact millisecond and allocation budgets should be set after TASK-003 establishes a benchmark harness.

## 11. External playtest gate

Use at least five participants who did not implement the game. This is not a formal research study and must not be represented as one.

Record:

- whether they understood why the agent resisted;
- whether they knew what action could change the decision;
- whether the changed battlefield condition produced the expected result;
- mission completion and restart count;
- moments of surprise or perceived unfairness;
- commands that were misunderstood;
- whether the autonomy felt meaningful, arbitrary, or cosmetic;
- whether they would voluntarily replay the mission.

### Minimum continuation signal

Proceed toward production only if:

- at least four of five participants can explain the principal refusal reason without developer explanation;
- at least four can identify a corrective tactical action;
- at least three voluntarily complete or replay the mission;
- no recurring issue shows that refusal is perceived as random or as input failure;
- deterministic replay and technical gates remain intact.

These thresholds are product heuristics, not statistical evidence.

## 12. Kill or redesign conditions

Stop or redesign the core interaction if any of these persists after two focused iterations:

- players consistently prefer direct control with appraisal disabled;
- refusal reasons are understandable only through developer overlays;
- the easiest solution is always selecting the most obedient soldier;
- suppression and route changes do not create meaningful command choices;
- agent variation feels random rather than learnable;
- the command loop requires so much pausing that real-time play adds no value;
- the technical architecture cannot reproduce and inspect failures;
- content production cost makes a second mission implausible.
