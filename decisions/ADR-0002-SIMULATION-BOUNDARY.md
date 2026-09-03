# ADR-0002: Framework-Independent Authoritative Simulation

Status: accepted for project baseline  
Date: 2026-09-02  
Decision owner: Dave  
Amended: 2026-09-03 (TASK-010, "Static authoritative data and the canonical image")

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

## Amendment 2026-09-03 (TASK-010): static authoritative data and the canonical image

### Context

TASK-010 adds `WorldState.Terrain`: an authored, authoritative per-cell grid
(elevation, passability, movement cost, opacity, directional cover). It is
authoritative data the simulation owns, but at this stage it carries **no
per-tick mutable state**: terrain is fixed for the life of a run (terrain
destruction and damage state are deferred to backlog B-019, with combat).

`Canonical.encode` (docs/04 section 17) is the hash input and the divergence
oracle. The question is whether static authoritative data must be inside it.

### Decision

Static authoritative data is **excluded from `Canonical.encode` and the
state hash while it carries no per-tick mutable state.** `WorldState.Terrain`
is authoritative and owned by the simulation, but it does not enter the
per-tick canonical image until it becomes mutable.

`Canonical.FormatVersion` stays `1`. It bumps, and the canonical image gains a
terrain section, when destructible terrain lands (B-019) or any other
per-tick mutation of terrain is introduced.

### Why

- The canonical image exists to detect **divergence between two runs of the
  same inputs**. Immutable data that is a pure function of the validated
  scenario cannot diverge tick to tick: both runs load the identical grid at
  tick 0 and never change it. Hashing it every tick adds cost and moves every
  pinned fixture hash for zero determinism benefit.
- Including it would re-pin every hash in `FixtureTests.fs`, `ScenarioTests.fs`,
  `content/fixtures/SPIKE-FIXTURE.md`, and the two retained framework-spike
  evidence sets, churning five files to encode a constant.
- The determinism contract (docs/09 section 3) is unweakened: identical
  validated scenario bytes are already a precondition of the contract, and the
  terrain grid is derived from them by `Scenario.validate` with no random draw
  and no wall-clock read.

### Constraints this places on future work

- The moment any phase **mutates** terrain (destruction, cratering, a dropped
  obstacle), that task MUST bump `Canonical.FormatVersion`, add a terrain
  section to `Canonical.encode` in canonical (row-major, fixed-width) order,
  re-pin the five files above, and record the re-pin in the ledger.
- The same rule applies to any other static authoritative store added later
  (a fixed objective layout, immutable squad rosters): in the canonical image
  when mutable, out of it while constant, format version bumps at the
  transition.
- A divergence in terrain-derived behaviour before B-019 is a **scenario
  validation or `Terrain.build` bug**, not a hash mismatch; it surfaces as a
  behavioural test failure, which is why `Terrain.build` is total and
  `Scenario.validate` rejects every malformed layer in one pass.

### ADR-0002 status

Unchanged. Dependency direction, ownership split, and the allow/forbid lists
are all satisfied: `Terrain` is `int`/`bool` arrays and DUs (allow-list
"immutable or serializable records ... explicit enums or discriminated-union
DTOs"), no framework type crosses any boundary, and the authored terrain
layer follows the existing "Content DTOs -> validation -> Sim setup" path
(`RawTerrainLayer` -> `Scenario.validate` -> `Terrain`). This amendment only
records where the canonical-image boundary sits for static authoritative
data.
