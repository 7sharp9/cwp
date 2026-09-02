# Adversarial Review

Status: completed against the initial architecture proposal  
Review posture: assume the project fails unless each load-bearing claim earns its place  
Last revised: 2026-09-02

## Executive verdict

The concept is viable, but the initial proposal was too ready to design the finished architecture before proving the interaction. Its strongest elements survive:

- a framework-independent F# simulation;
- fixed-tick headless execution;
- typed commands and outcomes;
- explicit decision reasons;
- a small isometric vertical slice;
- replay and test instrumentation.

Several attractive elements do not survive as initial requirements:

- a general hierarchical task planner;
- broad utility AI;
- behaviour trees as an organising principle;
- one belief model per soldier for every kind of information;
- complex relationship graphs;
- vehicles;
- a reusable scenario platform;
- Mibo.Adaptive;
- a full content compiler before one map exists.

The correct first project is not an engine for autonomous squads. It is a small game experiment that can kill the idea cheaply.

## 1. The fun premise is unproven

### Attack

The defining mechanic removes certainty from the player's commands. Strategy games normally reward precise intention-to-action mapping. A soldier who refuses may be believable and still be irritating. Explanation does not automatically repair the loss of agency.

The previous proposal assumed that visible reasons would make disobedience satisfying. That is a hypothesis, not an established design fact.

### Required correction

The first prototype must demonstrate a reversible refusal loop:

1. The player orders an exposed assault.
2. One soldier refuses or delays for a specific reason.
3. The player suppresses the machine gun or chooses a covered route.
4. The same soldier now accepts.

If this does not feel better than direct control, the project should pivot toward competent adaptation without explicit refusal, or toward doctrine-based squad control.

## 2. The architecture risks becoming the actual hobby

### Attack

The project is unusually tempting to a compiler and F# programmer. Typed orders, discriminated unions, event logs, deterministic stepping, incremental computation, headless hosts, and interchangeable clients are all enjoyable architecture work. None proves that the mission is fun.

A coding agent will amplify this risk by generating plausible abstractions faster than they can be validated.

### Required correction

- Use one F# simulation project at the start, not a project per layer.
- Use plain modules before interfaces.
- Use a small explicit phase schedule before a general system framework.
- Ban new dependencies and extension points without an ADR.
- Make each task produce observable gameplay or verification evidence.
- Treat a new abstraction with no second concrete use as suspect.

## 3. The original AI stack was over-specified

### Attack

The combination of hierarchical planning, belief-state reasoning, utility scoring, behaviour trees, reactive interruption, personality, morale, relationships, and leadership succession creates several overlapping sources of control. When a soldier behaves badly, it becomes unclear which layer is responsible.

Utility scores are especially dangerous. They are easy to add, hard to tune, and often weak explanations of behaviour. A generated explanation can claim one reason while the weighted interaction actually depended on several hidden terms.

### Required correction

The vertical-slice AI should be:

```text
Observations
  -> compact tactical knowledge
  -> explicit order appraisal rules
  -> commitment state
  -> finite action executor
  -> small, ordered interrupt table
```

Use hard constraints and decision tables first. Add utility ranking only for a small set of equivalent local choices, such as selecting among nearby cover cells, after profiling actual design pressure.

## 4. Determinism was asserted too loosely

### Attack

A fixed timestep is not sufficient for deterministic replay. Results can diverge through:

- floating-point variation;
- dictionary iteration order;
- unstable sorting of equal keys;
- wall-clock access;
- framework callback order;
- asynchronous completion order;
- use of the platform random implementation;
- presentation callbacks feeding authoritative state.

Cross-platform bit-identical determinism is substantially harder than repeatability on one runtime and architecture.

### Required correction

Define the first determinism boundary narrowly:

> Same committed code, target framework, architecture, scenario, seed, and accepted command stream must produce the same state hashes.

Use integer ticks, stable ordering, a project-owned PRNG with golden vectors, and canonical state hashing. Treat broader cross-platform lockstep as a later decision.

## 5. Custom navigation can consume the project

### Attack

A* on a grid is easy. Squad movement is not. Exposure-aware costs, formations, dynamic occupancy, doorways, replanning, local avoidance, and reservation deadlocks can dominate development.

The earlier design proposed squad routes, formation slots, and local reservations before proving that ordinary movement worked.

### Required correction

Progress through these levels only when the preceding level is stable:

1. One agent follows a deterministic grid path.
2. Several agents follow independent paths with no collision guarantee.
3. A simple destination-slot assignment prevents identical goals.
4. A short-horizon reservation prevents head-on deadlocks.
5. Loose fireteam formation is added only if players miss it.

Do not model a military formation system for the first refusal experiment.

## 6. Per-agent knowledge can be expensive without adding gameplay

### Attack

Giving every soldier a full independent belief state multiplies memory, update logic, explanation paths, and test cases. Most differences may be invisible because squad members remain close and share radio information.

### Required correction

Start with:

- objective world state;
- a squad tactical picture containing reported contacts;
- per-agent current visibility and communication state;
- timestamps and confidence for squad contacts.

Add persistent private beliefs only when a mission creates meaningful separation or communication loss.

## 7. Morale can become disguised randomness

### Attack

A large psychological model can produce behaviour that is internally defensible but externally unreadable. Pairwise relationships, courage, fatigue, cohesion, leadership, wounds, suppression, stress, morale, discipline, and trust create too many interacting knobs.

### Required correction

Use four initial variables:

- discipline: stable willingness to follow a valid order;
- trust: relationship to current command, slowly changing;
- stress: recent threat and casualties, decays over time;
- suppression: immediate combat impairment.

Wounds and capability are physical constraints, not morale terms. Add fatigue, cohesion, or relationships only after a concrete playtest need.

## 8. Direct leader control may conflict with intent command

### Attack

The proposal combines action-game control of the leader with tactical command of subordinates. That can create two games competing for attention. During direct combat, the player cannot inspect appraisal reasons or compose orders. During tactical pause, direct action may feel unnecessary.

### Required correction

Before G4, a bounded interaction task in the selected client must test at least two input modes:

- commander selected like any other unit, with tactical pause;
- direct leader movement plus pause-based squad orders.

Choose from play evidence. Do not make both permanent by default. Keep this out of the framework-comparison spike, whose job is to compare tooling and host integration rather than settle the control design.

## 9. Godot adds a mixed-language boundary

### Attack

Godot gives strong authoring tools, but the proposed Godot plus C# plus F# arrangement introduces:

- two application languages;
- a generated Godot API and scene lifecycle;
- wrapper DTOs across the boundary;
- separate debugging experiences;
- potential hot-reload limitations;
- temptation to let node state become authoritative.

### Required correction

If Godot wins the spike, expose one narrow F# facade to C#:

```text
CreateSession(scenarioBytes, seed)
Submit(commands)
Step(ticks)
ReadSnapshot()
DrainEvents()
ReadStateHash()
```

No Godot type may cross into the simulation assembly. No simulation type that is awkward for C# needs to be exposed directly; use stable boundary DTOs.

## 10. MonoGame and raylib shift cost into tooling

### Attack

A code-first stack feels clean to a programmer, but the game requires map authoring, object placement, UI layout, animation inspection, asset import, debug overlays, and rapid mission iteration. Building these tools is not free.

Raw raylib is especially likely to turn basic editor and UI work into project work. MonoGame has a content pipeline but no scene editor. Mibo provides architecture and rendering conveniences but intentionally no editor or wizard-driven workflow.

### Required correction

Any code-first client must use Tiled for the first map and define an edit-export-run workflow. The spike must measure content revision time, not merely lines of rendering code.

## 11. Mibo is relevant but carries maturity risk

### Strength

Mibo directly addresses several requirements:

- F#-first MVU architecture;
- fixed timestep;
- frame-bounded dispatch;
- a headless runner with virtual time and Step/StepN/StepUntil;
- observers suitable for replay and telemetry;
- interchangeable raylib and MonoGame backends;
- a typed system-pipeline concept.

This makes it a serious candidate, not a novelty dependency.

### Attack

Mibo is a small and rapidly changing project. Its 4.5.3 changelog was current on 31 August 2026. Its Adaptive integration is explicitly experimental, and current issue reports describe stale-value and threading failure modes in Mibo.Adaptive. Even if those defects do not affect classic MVU, they demonstrate that the newest layer is still settling.

A single-maintainer or small-maintainer dependency is acceptable for a prototype only when replacement cost is bounded.

### Required correction

- Keep the simulation independent of Mibo.
- Pin the exact Mibo version.
- Use classic MVU, not Adaptive, for the spike.
- Put Mibo behind the presentation/host boundary.
- Keep a minimal headless runner owned by the project or trivially replaceable.
- Record the package update policy in ADR-0003.

## 12. Godot remains defensible for a shipping-oriented project

### Attack on the attack

Rejecting Godot merely to keep everything in F# would optimise for language preference rather than production. Godot provides a mature visual editor, isometric tilemap support, UI composition, animation, asset importing, runtime inspection, navigation visualisation, and profiling. Those facilities target the likely solo-development bottleneck: content and presentation.

### Conclusion

The earlier Godot recommendation was reasonable, but premature. Mibo materially improves the code-first alternatives. The framework must be selected through a controlled spike rather than identity or aesthetics.

## 13. The content pipeline is a second product

### Attack

Pre-rendering eight-direction sprites from 3D models sounds efficient but requires rigs, cameras, naming conventions, atlas packing, palettes, pivots, occlusion rules, and revision automation. Building a reusable pipeline before gameplay proof can absorb months.

### Required correction

Use crude geometric or purchased placeholder assets for the graybox. Prove only one representative sprite pipeline after the command loop works.

## 14. Research ambition can contaminate design

### Attack

The project can be framed as bounded autonomy, explainable AI, command semantics, or human-agent interaction. That does not mean those should drive the first architecture. Instrumentation built for a future study may create abstraction and data-governance work before there is a game worth studying.

### Required correction

Record commands, decisions, and outcomes because replay and debugging need them. Defer participant studies, hypotheses, consent processes, and research-specific metrics until after external playtest success.

## 15. Document proliferation can create false control

### Attack

Splitting work into documents can make a project look organised while decisions diverge. Coding agents are particularly likely to update one design description and ignore the others.

### Required correction

This pack assigns one source of truth to each concern. Status appears only in the backlog and project-state file. Decisions appear in ADRs. The progress ledger records evidence rather than restating plans. A task that changes a decision must update all conflicting text in the same change.

## Revised minimum architecture

```text
CommandoWar.Sim
  - domain types
  - deterministic tick
  - grid and line of sight
  - simple navigation
  - order appraisal
  - finite execution states
  - combat and suppression
  - events, snapshots, replay hash

CommandoWar.Headless
  - scenario runner
  - command replay
  - diagnostics

Temporary client A
  - Godot .NET plus C# boundary

Temporary client B
  - Mibo classic MVU plus raylib
  - Tiled map import

CommandoWar.Sim.Tests
  - unit, property, scenario, and replay tests
```

No additional project is added until a task demonstrates the need.

## What would falsify the project

The concept is falsified for the current design if, after two focused gameplay iterations:

- players cannot predict whether the same soldier will accept a materially safer order;
- players prefer a mode that disables adaptation and refusal;
- explanations are read only after failure and do not change the next decision;
- the system needs so many exceptions that the order model cannot be stated simply;
- one small mission cannot be authored and revised without specialised tooling work.

A falsified concept is a useful result. The fallback is a tighter squad-action game with autonomous adaptation but no explicit refusal, retaining the simulation and retro presentation work.
