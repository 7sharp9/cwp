# Technology Decision: Godot, MonoGame, raylib, and Mibo

Status: decided; see ADR-0001 (accepted 2026-09-03)  
Evidence date: 2026-09-02; decision date: 2026-09-03

## Decision summary

**Godot .NET for content, presentation, input, UI, and packaging, with the
framework-independent F# `CommandoWar.Sim` behind a thin C# facade.** Headless
execution stays on the project-owned `cwheadless` runner.

This was decided by the TASK-004 and TASK-005 spikes and ADR-0001. Both
disposable hosts drove the byte-identical simulation and reproduced the shared
41-hash fixture exactly. Godot won on the weighted evidence (4.13 vs 3.62)
because the project is content- and UX-iteration-bound before it is
rendering-bound, and Godot ships the scene / tilemap / inspector / animation /
debugger tooling that the code-first route would have to build. Mibo's real
advantages (no C#/F# interop tax, native headless stepping, out-of-the-box
`dotnet publish`) were genuine but lower-leverage, and Mibo 4.2.0-4.5.3 could not be
adopted without the prohibited `Mibo.Adaptive` package (ADR-0003 2026-09-02
amendment). That coupling was re-separated upstream in Mibo 5.0.0 (ADR-0003
2026-09-06 amendment), which records Mibo 5.x classic MVU as a live
reconsideration option; the Godot decision itself is unchanged.

The analysis below is the pre-spike research snapshot, kept for context. Where
it is provisional or hedged, ADR-0001 and the 2026-09-03 progress-ledger entry
are authoritative.

Mibo is **not** a runtime dependency. `src/CommandoWar.Client.Mibo/` is retained
as spike evidence only, outside `CommandoWar.slnx` and every default build, test,
and CI path. Do not combine Godot with Mibo's rendering runtime, MonoGame, or
raylib in one production path, and do not build an abstraction over them.

## Verified current facts

### Godot

- Godot 4.7.2 was the current stable maintenance release on 18 August 2026.
- Official .NET builds support C# on Windows, Linux, and macOS.
- Godot 4 C# projects still cannot export to the web platform.
- Godot includes a visual 2D editor, TileMapLayer, isometric demos, UI tooling, animation, runtime inspection, debugger, and profilers.
- Godot is MIT licensed.

### MonoGame

- MonoGame 3.8.5.1 was the current release in the official repository at the evidence date.
- MonoGame describes itself as a `bring your own tools` framework, not an engine with a scene editor.
- It supports .NET languages including F#.
- It provides 2D and 3D rendering, input, audio, and an extensible content pipeline.
- It targets desktop, mobile, and restricted console platforms.

### raylib

- raylib 6.0 was released on 23 April 2026.
- raylib intentionally remains a small programming library rather than a full engine.
- It provides rendering, input, audio, models, shaders, and broad platform support, but no integrated scene or level editor.
- It uses the permissive zlib license.

### Mibo

- Mibo 4.5.3 was current in its changelog on 31 August 2026.
- It is an F# MVU game framework with Mibo.Raylib and Mibo.MonoGame backends.
- Mibo.Core contains fixed-step support, headless runners, virtual time, observers, input abstractions, and system-pipeline helpers.
- Its headless runner can Step, StepN, StepUntil, Run, and RunAsync without a graphics backend.
- Mibo.Adaptive is explicitly marked experimental.
- Mibo intentionally provides no integrated editor or wizard-driven content workflow.
- Mibo 5.0.0 (2026-09-04) split the framework into independent MVU (`Mibo.Mvu`) and adaptive (`Mibo.Adaptive.Mibo`) packages over a shared kernel; `Mibo.Core` no longer depends on `Mibo.Adaptive`. This removes the packaging blocker recorded on 2026-09-02. See ADR-0003 2026-09-06 amendment.

### Tiled

- Tiled 1.12.2 was current on 27 May 2026.
- It supports isometric maps, object layers, templates, custom typed properties, terrain tools, and JSON export.
- It is a credible external map-authoring tool for MonoGame, raylib, or Mibo.

## Why Godot was proposed

Godot was not selected because the simulation needs an engine. It was proposed because the rest of the game does.

A solo developer must repeatedly perform work that a pure rendering framework does not solve:

- paint and revise isometric maps;
- place cover, spawns, objectives, triggers, and occluders;
- inspect layering and Y-sort problems;
- compose tactical overlays and menus;
- preview animations;
- import and reimport assets;
- inspect scene state while running;
- tune particles, audio, lighting, and camera behaviour;
- create debug visualisations without building a separate tool.

Godot turns much of that into editor work. MonoGame and raylib turn it into code or third-party-tool integration.

The project is likely to be content-iteration bound before it is rendering-performance bound. That is the strongest argument for Godot.

## Why not raw MonoGame

MonoGame is mature and gives excellent control. It can be used directly from F#, and its content pipeline is valuable. It is a strong choice when:

- the team wants to build a custom engine layer;
- level data already comes from an external editor;
- exact rendering control matters more than integrated authoring;
- console paths may later matter;
- a long-lived, widely used framework is preferred.

For this project, raw MonoGame leaves several responsibilities unowned:

- scene organisation;
- tile and object editing;
- UI layout;
- animation tooling;
- debug overlays;
- content validation beyond the pipeline;
- application architecture.

Mibo exists partly to fill that architecture gap for F#. Therefore the meaningful code-first candidate is Mibo on MonoGame or raylib, not raw MonoGame plus a new home-grown framework.

## Why not raw raylib

raylib is excellent for a small graphical experiment. It has a compact API, little ceremony, and strong retro fit. It is also the easiest option with which to accidentally build an engine instead of a game.

Raw raylib would require the project to choose or build:

- an F# loop architecture;
- input mapping;
- asset caching;
- camera and layering conventions;
- UI components;
- map loading;
- animation systems;
- fixed-step and replay orchestration;
- diagnostics.

Mibo already supplies much of that. There is little reason to use raylib-cs directly unless Mibo itself proves unsuitable.

## What Mibo helps with

Mibo is a good fit for the headless and deterministic-development goals in four specific ways.

### 1. Shared execution shape

The classic MVU host and HeadlessRunner can run the same update model with or without graphics. Virtual time and explicit stepping support repeatable scenario tests.

### 2. Fixed-step control

Mibo can accumulate host time and dispatch a fixed-step message. For this project, the authoritative simulation should still consume integer ticks rather than arbitrary `dt`, but Mibo can own the host scheduling.

### 3. Observers

Headless observers can record snapshots, state hashes, telemetry, and replay evidence after each step without modifying the simulation.

### 4. F#-native client architecture

The project can remain in F# from input through rendering when using Mibo.Raylib or Mibo.MonoGame. That removes the Godot C# facade and reduces language-boundary friction.

## What Mibo must not own

Mibo must not become the domain model.

The following stay in `CommandoWar.Sim`:

- agent and squad state;
- commands and appraisals;
- navigation and movement;
- combat and morale;
- random state;
- authoritative events;
- state hashing and replay semantics.

A Mibo client may wrap `Simulation.step` in an Elmish message loop. If Mibo is replaced, the game rules remain.

Do not use Mibo.Adaptive for the vertical slice. The initial entity count is small, explicit recomputation is cheap, and the experimental adaptive layer introduces invalidation and threading rules that are unnecessary for the proof.

## Option comparison

Scores are provisional, from 1 (poor) to 5 (strong), and exist to expose assumptions rather than manufacture certainty.

| Criterion | Weight | Godot + F# core | Mibo + raylib + Tiled | Mibo + MonoGame + Tiled | Raw MonoGame + Tiled | Raw raylib-cs + Tiled |
|---|---:|---:|---:|---:|---:|---:|
| Level and content authoring | 20 | 5.0 | 3.5 | 3.5 | 3.5 | 3.0 |
| UI, animation, runtime debugging | 15 | 5.0 | 2.5 | 3.0 | 2.5 | 2.0 |
| F# and domain-model fit | 15 | 3.0 | 5.0 | 5.0 | 4.0 | 3.5 |
| Headless testing and replay | 15 | 5.0 | 5.0 | 5.0 | 4.0 | 4.0 |
| First-prototype speed | 10 | 4.0 | 4.0 | 3.5 | 3.0 | 4.0 |
| Ecosystem maturity and replacement risk | 10 | 5.0 | 2.5 | 3.0 | 4.5 | 4.5 |
| Retro rendering control | 5 | 4.0 | 5.0 | 5.0 | 5.0 | 5.0 |
| Distribution options | 5 | 4.0 | 4.0 | 5.0 | 5.0 | 5.0 |
| Long-term maintenance burden | 5 | 4.0 | 3.0 | 3.0 | 3.0 | 2.5 |
| Weighted result out of 5 | 100 | **4.45** | **3.83** | **3.93** | **3.78** | **3.58** |

These scores favour a polished solo-developed game. If the primary outcome becomes an F# research prototype, increase the F# and headless weights and reduce the editor weight; Mibo will likely win.

## Framework spike

Both candidates must use the same `CommandoWar.Sim` assembly, initial scenario, six agents, command type, and snapshot contract.

### Godot spike

Prove:

- C# can create and step the F# session through one facade;
- a 32 by 32 isometric map can be edited and loaded;
- six agents can be selected and displayed from snapshots;
- a move order can be submitted;
- interpolation does not mutate simulation state;
- tick, state hash, path, and other framework-neutral diagnostics can be shown in an overlay;
- a map change can be made and tested quickly.

### Mibo spike

Use classic MVU and pin the exact package version. Prove:

- the same simulation can be wrapped by Mibo's fixed-step host;
- the same session runs through Mibo's classic headless runner and the graphical host;
- a Tiled isometric map with object markers and custom properties loads;
- six agents render and accept the same move order;
- tick, state hash, path, and content errors can be shown without a home-grown UI framework becoming necessary;
- an optional Mibo.MonoGame compile or launch smoke test can be performed without simulation changes, after the raylib evidence is complete.

### Measurements

Record for each candidate:

- setup and build failures;
- glue-code size;
- time from map edit to visible result;
- debugger usefulness;
- quality of runtime state inspection;
- ease of drawing paths, cover, line of sight, and appraisal overlays;
- asset-import friction;
- test-runner friction;
- packaging smoke test;
- number of framework-specific concepts leaking toward the simulation.

## Selection rule

Choose Godot when its authoring and debugging advantage is clear and the F# boundary stays narrow and stable.

Choose Mibo when it reaches near-parity for content iteration and UI while materially simplifying the F# execution, replay, and debugging path.

Do not choose raw MonoGame or raw raylib unless the Mibo spike fails for a specific, recorded reason that direct use fixes.

## Update policy

- Pin all selected versions.
- Use a lock file where supported.
- Upgrade only in a dedicated task with release-note review and regression evidence.
- Do not follow Mibo.Adaptive releases during vertical-slice work.
- Re-run a packaging smoke test after framework upgrades.
