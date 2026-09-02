# ADR-0002: Framework-Independent Authoritative Simulation

Status: accepted for project baseline  
Date: 2026-09-02  
Decision owner: Dave

## Context

The project needs rich presentation tooling, but its distinctive value lies in inspectable command, perception, appraisal, and tactical state. Those systems must run without graphics for deterministic tests, replay, benchmarks, and coding-agent verification.

Binding authoritative state to Godot nodes, MonoGame components, raylib handles, Mibo models, engine physics, or frame callbacks would make framework comparison invalid and simulation failures harder to reproduce.

## Decision

The authoritative game simulation will be an ordinary F# library with no dependency on:

- Godot;
- Mibo;
- MonoGame;
- raylib or raylib-cs;
- Tiled parsing types;
- rendering, audio, UI, input, animation, or editor APIs;
- wall-clock time;
- framework physics or random generators.

The simulation accepts framework-neutral typed commands and produces:

- a new or internally advanced authoritative state;
- ordered domain events;
- a framework-neutral render snapshot;
- deterministic diagnostic data required by tests and replay.

A client or headless runner owns scheduling and transport. It does not own game rules.

## Reference boundary

```fsharp
type StepResult =
    {
        State: WorldState
        Events: DomainEvent array
        Snapshot: RenderSnapshot
    }

val step:
    config: SimConfig ->
    commands: PlayerCommand array ->
    state: WorldState ->
    StepResult
```

The exact API may change through implementation evidence, but the dependency direction may not change without replacing this ADR.

## Dependency direction

```text
Client.Godot  -----\
                    -> ClientFacade -> Contracts -> Sim
Client.Mibo   -----/                         |
Headless ------------------------------------/

Content.Godot ----\
                    -> Content DTOs -> validation -> Sim setup
Content.Tiled -----/
```

No arrow may point from `Sim` toward a host or content tool.

## Authoritative ownership

The simulation owns:

- tick and phase order;
- entity identity and lifecycle;
- grid position, movement, and occupancy;
- perception and tactical knowledge;
- commands, appraisal, commitments, and execution;
- combat, wounds, suppression, stress, and morale-related state;
- objective and mission state;
- deterministic random stream;
- replay-relevant events and state hash.

The client owns:

- raw input capture;
- camera;
- interpolation;
- sprites, animation, particles, lighting, and sound;
- view-only selection and hover state;
- UI composition;
- asset handles;
- operating-system integration and packaging.

## Data crossing the boundary

Allowed:

- integers, booleans, strings where necessary, arrays, immutable or serializable records, stable IDs, explicit enums or discriminated-union DTOs;
- logical coordinates and presentation values defined by project contracts;
- versioned content and replay records.

Forbidden:

- Godot `Node`, `Resource`, `Vector2`, signal, or physics objects;
- MonoGame `Game`, `Texture2D`, `Vector2`, content handles, or component objects;
- raylib texture, sound, window, or input handles;
- Mibo host, backend, adaptive, or command-buffer objects;
- Tiled parser object graphs;
- delegates that permit the simulation to call host behaviour;
- asynchronous callbacks that mutate authoritative state.

## Mutation policy

The boundary is functional even if the implementation uses controlled mutation internally. Performance-sensitive stores may use arrays, structs, pooled buffers, or mutable builders when benchmarks justify them.

Internal mutation must remain:

- owned by one simulation step;
- ordered by documented phases;
- unavailable to the client;
- deterministic under the current contract;
- covered by state and replay tests.

## Consequences

### Positive

- both framework spikes exercise the same game rules;
- headless tests can run faster than real time;
- replay and divergence diagnosis remain possible;
- framework replacement does not imply game-rule replacement;
- F# domain modelling remains useful rather than cosmetic;
- presentation bugs and simulation bugs can be separated.

### Costs

- explicit DTO and adapter work is required;
- visual editor data must be imported and validated;
- some convenience engine systems cannot be authoritative;
- duplicated view state may exist for interpolation and animation;
- debugging sometimes crosses the host-to-core boundary.

These costs are accepted because the simulation is the project’s distinctive and highest-risk component.

## Compliance checks

A task touching architecture must fail review if:

- a framework package appears in the simulation project;
- authoritative state is stored only in presentation entities;
- simulation outcomes depend on render delta or animation completion;
- client code writes directly into world stores;
- tests require a window to execute core rules;
- a host random source or physics query affects authoritative state;
- content parser objects survive beyond the import boundary.
