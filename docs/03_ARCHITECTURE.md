# Architecture

Status: baseline; framework-neutral core accepted by ADR-0002  
Last revised: 2026-09-03 (section 14: Godot client boundary, ADR-0004)

## 1. Architectural objective

Create the smallest architecture that supports:

- a deterministic, headless tactical simulation;
- one graphical client selected after a spike;
- typed player commands;
- structured agent decisions;
- replay and state-hash verification;
- rapid revision of one mission.

The architecture is not intended to support arbitrary games, third-party scripts, or multiple historical settings.

## 2. Dependency rule

```text
Client / Host / Tools
        |
        v
CommandoWar.Sim
```

`CommandoWar.Sim` references only ordinary .NET and explicitly approved simulation dependencies. It does not reference a graphics framework.

Forbidden dependency directions:

```text
CommandoWar.Sim -> Godot
CommandoWar.Sim -> Mibo
CommandoWar.Sim -> MonoGame
CommandoWar.Sim -> raylib
CommandoWar.Sim -> client DTOs
CommandoWar.Sim -> editor files at runtime
```

## 3. Initial solution structure

Do not split further before a task proves the need.

```text
src/
  CommandoWar.Sim/             F# authoritative simulation library
  CommandoWar.Headless/        F# CLI and replay runner
  CommandoWar.Client.Godot/    disposable framework spike
  CommandoWar.Client.Mibo/     disposable framework spike

tests/
  CommandoWar.Sim.Tests/       unit, property, scenario, replay tests

content/
  scenarios/                   validated source scenario data
  maps/                        framework-neutral or source map files

assets/
  source/                      editable source assets
  generated/                   reproducible generated artifacts

docs/
decisions/
tasks/
```

After ADR-0001, retain only the selected production client. Preserve the rejected spike as evidence in a tagged branch or archive rather than maintaining both clients indefinitely.

## 4. Simulation boundary

The public simulation API should remain small. Exact F# representation may evolve, but the semantics are:

```fsharp
type Session

type SessionConfig =
    { TicksPerSecond: int
      Scenario: Scenario
      Seed: uint64 }

type StepInput =
    { Commands: PlayerCommand array }

type StepOutput =
    { Events: DomainEvent array
      Snapshot: RenderSnapshot
      StateHash: StateHash }

val createSession : SessionConfig -> Session
val step : StepInput -> Session -> StepOutput
```

The session may use controlled mutation internally. Mutation must not escape the call boundary or become observable through a client-held reference.

For C# interoperability, the Godot adapter may expose an object-oriented facade and DTOs. Those live outside `CommandoWar.Sim` or in a boundary module with no Godot types.

## 5. Runtime data flow

```text
Raw input
  -> client input mapping
  -> proposed player command
  -> simulation command validation
  -> accepted command log
  -> deterministic simulation step
  -> domain events + render snapshot + state hash
  -> client interpolation, animation, audio, UI, debug overlays
```

Only accepted commands are authoritative. A rejected input command returns an explicit validation result and is not added to replay.

## 6. Fixed tick

The authoritative simulation runs at 20 ticks per second initially.

- Hosts may render at any rate.
- Hosts may call several simulation ticks to catch up, within a configured limit.
- Hosts may pause or single-step the simulation.
- The simulation receives a tick count, not a floating-point frame delta.
- Slow motion is a host scheduling concern unless game mechanics explicitly model it.

## 7. Explicit phase schedule

Every tick uses a visible, fixed order:

1. Validate and enqueue commands accepted for this tick.
2. Apply communication and order delivery.
3. Update current visibility and observations.
4. Update squad tactical knowledge.
5. Appraise new or materially changed orders.
6. Select or continue individual commitments.
7. Plan or repair short movement paths.
8. Reserve destinations and resolve movement.
9. Resolve weapon actions, projectiles, and impacts.
10. Apply wounds, suppression, stress, and death.
11. Process interrupts and leadership state.
12. Update mission objectives.
13. Emit domain events.
14. Create the render snapshot and state hash.

Changing phase order is an architectural decision because it changes outcomes. Record it in an ADR and update replay fixtures.

## 8. Module layout inside the simulation project

F# compile order should make dependency direction explicit. A likely starting order is:

```text
Ids.fs
Numbers.fs
Random.fs
Grid.fs
Domain.fs
Commands.fs
Scenario.fs
Knowledge.fs
Orders.fs
Navigation.fs
Movement.fs
Combat.fs
Morale.fs
Mission.fs
Events.fs
Snapshot.fs
Hashing.fs
Simulation.fs
```

This is a starting point, not a command to create empty files. TASK-002 should create only the modules needed for its behaviour.

## 9. Data ownership

### Authoritative

- tick number;
- entity identity and life state;
- logical positions and movement progress;
- terrain and cover state;
- observations and tactical knowledge;
- orders and commitments;
- wounds, suppression, stress, discipline, and trust;
- ammunition and weapon cooldowns;
- objectives;
- deterministic random state.

### Presentation-only

- sprite nodes or draw handles;
- animation frame and blend progress;
- camera position;
- particles and decals;
- audio voices;
- hover and selection decoration;
- interpolation state;
- damage-number text;
- screen shake;
- temporary debug geometry.

Presentation-only state may be reconstructed after loading a save.

## 10. Entity model

Use stable typed IDs and explicit stores. Do not begin with a full ECS.

```fsharp
type AgentId = private AgentId of int
type SquadId = private SquadId of int
type WeaponId = private WeaponId of int
type ObjectiveId = private ObjectiveId of int
```

Stores may begin as arrays or maps. Hot-path data can move to indexed arrays after profiling. Domain concepts such as orders, squads, knowledge, and commitments should remain explicit rather than dissolved into generic components.

## 11. Grid and projection

The authoritative world is a logical grid with optional elevation level. Isometric screen coordinates exist only in clients.

A cell records only gameplay-relevant facts:

- traversability by movement class;
- movement cost;
- elevation;
- opacity;
- directional cover;
- material or destruction state where needed.

Art-layer tiles and decoration are not authoritative unless content compilation maps them to gameplay data.

## 12. Commands, events, and snapshots

### Commands

Commands express requested intent and include:

- unique command ID;
- issue tick;
- issuer and recipients;
- target position, area, or entity;
- posture and risk tolerance where relevant.

### Events

Events state what happened, not what a renderer should do. Examples:

- `OrderAccepted`
- `OrderAdapted`
- `OrderDelayed`
- `OrderRefused`
- `ContactObserved`
- `ShotFired`
- `AgentSuppressed`
- `AgentWounded`
- `LeaderChanged`
- `ObjectiveCompleted`

Audio and visual effects are client interpretations of these events.

### Snapshots

A render snapshot is a compact read model containing only what clients need for the current frame. Clients must not retain references to mutable simulation storage.

## 13. Headless host

The headless executable must support:

- create scenario and seed;
- submit command files;
- step one tick or N ticks;
- run until a predicate or maximum tick;
- emit events;
- save periodic state hashes;
- compare a replay with expected hashes;
- output useful failure diagnostics.

If Mibo wins, its classic HeadlessRunner may host this loop. `CommandoWar.Sim` still owns all game semantics.

## 14. Godot host

Selected by ADR-0001 (accepted 2026-09-03). Client structure fixed by ADR-0004.

- The client is a thin C# shim per scene entry point over an F# client-core
  library. The shim (the scene root) holds `[Export]` refs and editor
  integration and forwards lifecycle callbacks; it contains no client logic.
  The F# client-core library owns the simulation facade, fixed-step scheduling,
  input-to-command mapping, view-model and overlay preparation, and
  replay-relevant messages. It may reference `GodotSharp`; it is a separate
  project from `CommandoWar.Sim`, which never references a framework.
- F# types are not scene entry points: Godot 4.7.2's script source generators
  are C#-only, so Godot does not drive an F# node's lifecycle callbacks and
  does not see its `[<Export>]` fields (ADR-0004 evidence).
- The C# <-> F# boundary carries primitives, `System.Nullable<T>`, arrays of
  `[<CLIMutable>]` records, and `Godot.InputEvent` into F# client methods.
  F# `option` / `list` / `Result` / DU cases / module functions do not cross to
  C#.
- Content authoring: a C# reader at the Godot edge (TileMapLayer / imported map
  data / typed marker nodes) emits framework-neutral DTOs; validation and
  compiled gameplay cells are F# and are passed to the simulation. No Godot
  type crosses past the DTO.
- Godot physics, navigation, and random are not authoritative.
- A host-owned fixed-step accumulator schedules ticks from the wall-clock
  `_Process` delta; the simulation receives an integer tick, never a float.
- Snapshots drive node creation and interpolation.

## 15. Mibo host

If selected:

- use Mibo classic MVU, fixed step, and frame-bounded dispatch where useful;
- keep `Simulation.step` as the only domain transition;
- use Mibo messages for host input and effects, not as a replacement for domain commands;
- use Tiled or another agreed editor for map content;
- pin Mibo version;
- do not use Mibo.Adaptive before a post-vertical-slice ADR.

## 16. Content boundary

Source map/editor data is not trusted runtime content. A validation step produces a `Scenario` value or a list of exact errors.

Validation covers:

- map dimensions and supported orientation;
- known tile and object classes;
- unique IDs and names;
- valid spawns and objective references;
- traversable start cells;
- cover and opacity values;
- required extraction and objective markers;
- no references to missing assets or entities.

A general scripting language is excluded. Mission logic uses a constrained set of typed triggers and objectives.

## 17. Failure handling

- Invalid player commands return typed rejections.
- Invalid content prevents scenario start and lists all actionable errors practical in one pass.
- Missing presentation assets may use explicit development placeholders, but release builds fail validation.
- Replay divergence reports the first divergent tick and, where practical, a component-level state diff.
- The client must not catch and suppress simulation errors.

## 18. Explicit non-architecture

Do not add these by default:

- service locator;
- dependency-injection container;
- message broker inside the simulation;
- one actor or mailbox per soldier;
- generic plugin system;
- reflection-based component discovery;
- arbitrary runtime scripting;
- network protocol;
- distributed simulation;
- data-oriented rewrite before profiling;
- client-specific types in domain records.
