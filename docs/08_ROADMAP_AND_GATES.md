# Roadmap and Evidence Gates

Status: active planning baseline

## 1. Operating rule

Work advances through evidence gates, not through a calendar promise. Do not start a later phase because an earlier phase feels mostly complete.

A gate passes only when:

- its required tasks are done;
- the specified evidence exists;
- known failures are recorded;
- affected decisions and risks are updated;
- Dave accepts the evidence.

## 2. Phase summary

| Phase | Purpose | Exit gate |
|---|---|---|
| P0 | establish project control and boundaries | G0 Project control ready |
| P1 | select presentation framework with a controlled spike | G1 Framework selected |
| P2 | prove deterministic simulation foundations | G2 Deterministic core proven |
| P3 | prove the command and reversible-refusal loop headlessly | G3 Command loop proven headless |
| P4 | integrate one playable mission | G4 Vertical slice feature-complete |
| P5 | test comprehension and enjoyment externally | G5 External playtest passed |
| P6 | decide whether to enter production | G6 Production go/no-go |

## 3. P0: governance and feasibility

### Required work

- approve the charter and adversarial review;
- create the repository baseline;
- establish authoritative files and task workflow;
- record the simulation boundary as an ADR;
- prepare deterministic and framework-spike task definitions;
- keep public naming and intellectual property unresolved but bounded.

### G0 evidence

- project pack exists and contains no broken internal references;
- repository baseline builds and tests on the development machine;
- `PROJECT_STATE.yaml` names exactly one active task;
- no client framework has entered the authoritative simulation project;
- Dave has reviewed the stop conditions and non-goals.

## 4. P1: framework selection

### Required work

- create the minimal simulation skeleton;
- create the determinism harness needed by both hosts;
- implement a disposable Godot .NET host;
- implement a disposable Mibo classic MVU plus raylib host;
- use equivalent map, input, rendering, snapshot, and debug requirements;
- record objective and subjective evidence;
- accept ADR-0001.

### What the spike must not become

- a production renderer;
- a complete map pipeline;
- a polished user interface;
- a broad performance competition;
- a reason to put framework types into the simulation;
- a three-framework abstraction layer.

### G1 evidence

- both viable candidates run the same simulation build;
- both render six moving agents on an isometric map;
- both submit the same typed move command and consume the same snapshot;
- both display tick and state hash;
- content-edit workflow has been measured;
- build, packaging, debugger, and glue-code observations are recorded;
- ADR-0001 selects one production client route or explicitly stops the project.

## 5. P2: deterministic core

### Required work

- finalise integer tick and ordered phase loop;
- establish stable entity ordering;
- implement project-owned random generator and golden vectors;
- implement canonical state hashing;
- implement command recording and replay;
- build grid, terrain, movement, reservations, line of sight, and tactical knowledge;
- establish headless benchmark and allocation measurements.

### G2 evidence

- deterministic unit and property tests pass;
- replay reproduces canonical hashes under the stated scope;
- a divergence report identifies the first mismatching tick and state section;
- no wall clock or framework random source affects authoritative state;
- synthetic 50-agent scene meets the initial measured budget or has an accepted corrective plan;
- the graphical client remains replaceable without changing simulation behaviour.

## 6. P3: headless command loop

### Required work

- implement the five vertical-slice intents;
- implement communication and shared tactical knowledge;
- implement explicit order appraisal and typed reasons;
- implement commitment and finite execution states;
- implement suppression, stress, discipline, trust, and reappraisal triggers;
- implement simple enemy hold-and-defend doctrine;
- implement the canonical refusal sequence as a deterministic scenario test.

### G3 evidence

- the canonical refusal test passes;
- at least two agents appraise the same intent differently for inspectable reasons;
- suppression or a route change reverses one outcome predictably;
- no broad utility system, general planner, or framework callback controls cognition;
- scenario traces are readable enough to diagnose all decisions;
- repeated headless runs produce stable evidence.

## 7. P4: playable integration

### Required work

- implement validated scenario loading;
- complete the Bridgehead map;
- integrate selection, command preview, tactical pause, and overlays;
- integrate combat, casualties, succession, objectives, extraction, and mission completion;
- add placeholder or limited production-quality art sufficient for comprehension;
- add save or replay playback required for testing;
- conduct internal playthrough and defect passes.

### G4 evidence

- all functional acceptance criteria in `07_VERTICAL_SLICE.md` pass;
- mission can be completed from a clean build without developer commands;
- failure states and invalid content are explicit;
- replay of a completed mission reproduces its authoritative result;
- known defects are triaged by severity;
- no prohibited pre-G5 scope has entered the build.

## 8. P5: external playtest

### Required work

- prepare a clean distributable build;
- prepare a short neutral test script;
- run at least five non-developer sessions;
- collect event logs, outcome data, and notes;
- distinguish comprehension problems from balance and presentation problems;
- make at most two focused interaction revisions before reassessing the premise.

### G5 evidence

- continuation thresholds in `07_VERTICAL_SLICE.md` are met;
- recurring failures and fixes are documented;
- the project can identify why an agent resisted from recorded evidence;
- testers can act on explanations without developer coaching;
- no critical accessibility, control, or data-loss defect remains.

## 9. P6: production decision

### Questions

- Is the core interaction enjoyable enough to justify more content?
- Can a second mission be built without redesigning the architecture?
- Is the selected framework still reducing rather than creating work?
- Is the art pipeline sustainable?
- Is the product distinct enough to communicate without relying on the cancelled game's name?
- Is this primarily a commercial game, an experimental game, a research platform, or none of those?

### G6 outcomes

One of:

- **Proceed:** fund and plan a constrained production milestone.
- **Re-scope:** retain the simulation or interaction but change the game form.
- **Research fork:** preserve a stable game branch and create a separate research-instrumentation plan.
- **Stop:** archive code, evidence, and lessons without continuing sunk-cost development.

## 10. Change control

Any proposal that changes product scope, architecture, framework, determinism contract, or a gate must:

1. identify the current source of truth;
2. state the evidence motivating the change;
3. describe rejected alternatives;
4. update or add an ADR where appropriate;
5. update risks, backlog, and affected acceptance criteria;
6. avoid mixing the decision with unrelated implementation work.
