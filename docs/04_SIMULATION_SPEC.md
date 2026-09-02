# Authoritative Simulation Specification

Status: baseline for tasks after TASK-002  
Last revised: 2026-09-02

## 1. Scope

This document specifies the authoritative tactical simulation for the vertical slice. It does not specify rendering, animation, audio, menus, or editor behaviour.

## 2. Time model

- Nominal rate: 20 ticks per second.
- Tick is an unsigned or non-negative integer value with checked progression.
- All durations are expressed in ticks.
- A command becomes eligible on a specified tick.
- The host may pause, single-step, or catch up, but cannot partially execute an authoritative tick.

Do not use floating-point `dt` in authoritative systems.

## 3. Initial determinism contract

For the same:

- committed source revision;
- target framework and architecture;
- validated scenario bytes;
- simulation configuration;
- initial seed;
- accepted command stream;

…the simulation must produce the same authoritative state hash at every recorded checkpoint.

This does not initially promise bit-identical results across different CPU architectures, target frameworks, or compiler versions.

## 4. Stable numeric representation

Use integers for:

- cells and elevation;
- ticks and durations;
- health or wound severity bands;
- ammunition;
- suppression, stress, trust, and discipline scales;
- movement progress within an edge;
- probability thresholds and random draws.

A suggested convention is a bounded integer scale such as 0 to 1000 for normalised values. The exact scale is an implementation decision within TASK-002 or a dedicated ADR.

Rendering may convert values to `float32` after taking a snapshot.

## 5. Randomness

The project owns the deterministic PRNG implementation and interface.

Requirements:

- fixed-width integer arithmetic;
- documented algorithm and seed expansion;
- golden output vectors in tests;
- serialisable random state;
- no use of `System.Random` in authoritative code;
- no random draw from presentation code;
- stable draw order within a system.

Prefer independent named streams only when they prevent unrelated features from perturbing one another. Do not create a stream per entity without evidence that it improves replay stability or testability.

## 6. Identity and ordering

- Every entity receives a stable ID at creation.
- IDs are never reused during one session.
- System iteration order is explicit, normally ascending ID.
- Equal-priority decisions use a stable tie-breaker.
- Hash-map enumeration order is never authoritative.
- Removed entities remain representable in events and replay diagnostics through their IDs.

## 7. Grid

### Cell coordinates

```fsharp
type Cell =
    { X: int
      Y: int
      Level: int }
```

### Gameplay cell data

A cell may include:

- movement class and cost;
- opacity;
- elevation;
- directional cover;
- occupancy capacity;
- water or hazard classification;
- destruction state if used by the mission.

The vertical slice needs only the data exercised by the bridge mission. Do not implement unused terrain categories.

## 8. Position and movement

An agent position consists of:

- current cell;
- optional next cell;
- integer progress along the current movement edge;
- facing direction;
- stance or posture if gameplay-relevant.

Movement resolution is discrete and deterministic. The renderer interpolates between current and next cells.

### Initial movement progression

1. Validate destination.
2. Compute deterministic A* path with stable neighbour order and tie-breaks.
3. Reserve only the immediate next destination where required.
4. Advance movement progress by an integer amount each tick.
5. Enter the next cell when progress reaches the threshold.
6. Replan when the next path cell becomes invalid.

Complex formation maintenance is deferred.

## 9. Line of sight and cover

- Line of sight operates on logical cells and elevation.
- The algorithm and corner rules must be documented and covered by golden examples.
- Directional cover depends on the attack vector.
- Presentation occlusion does not define simulation visibility.
- Contacts are observations, not direct references to all enemy state.

The initial implementation may use a supercover line algorithm or another deterministic grid method. The chosen corner and blocking semantics require tests before combat tuning.

## 10. World state

A minimal world contains:

```fsharp
type WorldState =
    { Tick: int64
      Terrain: Terrain
      Agents: AgentStore
      Squads: SquadStore
      TacticalKnowledge: TacticalKnowledge
      Orders: OrderStore
      Projectiles: ProjectileStore
      Mission: MissionState
      Random: RandomState }
```

Fields should be added only when an implemented behaviour requires them.

## 11. Agent state

The vertical-slice agent needs:

- identity, side, and squad;
- life and wound state;
- logical position and movement state;
- weapon and ammunition;
- current visible contacts;
- current order and commitment;
- discipline;
- trust in commander;
- stress;
- suppression;
- communication availability;
- current execution state.

Fatigue, persistent personality dimensions, interpersonal relations, inventory grids, and skill trees are deferred.

## 12. Tick phases

### 12.1 Command intake

- sort commands by command ID after validating issue tick;
- reject malformed, unauthorised, impossible-to-address, or duplicate commands;
- record accepted commands before effects are applied.

### 12.2 Communication

- determine which recipients receive an order this tick;
- voice and radio delay may initially be zero when in range;
- communication failure must be explicit, not silently ignored.

### 12.3 Perception

- clear current visibility;
- evaluate visible enemy agents and relevant hazards;
- emit observations;
- update current visible-contact state.

### 12.4 Tactical knowledge

- merge reports into squad contacts;
- retain last known position, confidence, and observation tick;
- decay or expire stale contacts according to explicit rules.

### 12.5 Appraisal

- appraise newly received orders;
- reappraise only on material triggers, not every tick without need;
- emit outcome and structured reasons.

### 12.6 Commitment and local action

- accepted orders create or update a commitment;
- the executor chooses the next finite action within that commitment;
- a small ordered interrupt table may supersede the normal action.

### 12.7 Navigation and movement

- compute or repair path;
- resolve reservations in stable order;
- advance movement;
- emit movement and blockage events where useful.

### 12.8 Combat

- validate line of fire and ammunition;
- resolve weapon readiness;
- obtain deterministic spread or hit draw;
- apply cover and impact;
- create suppression independent of a hit where intended;
- emit shot, impact, wound, and suppression events.

### 12.9 State consequences

- update suppression decay;
- update stress from recent events;
- apply deaths and incapacitation;
- update command succession only if included in the current milestone.

### 12.10 Mission

- evaluate objective conditions;
- emit completion or failure once;
- prevent accidental repeated completion events.

### 12.11 Output

- finalise ordered events;
- create a render snapshot;
- compute state hash at configured checkpoints or every tick during development.

## 13. Commands

Initial commands:

```fsharp
type PlayerIntent =
    | MoveTo of target: Cell * posture: MovementPosture
    | Hold of area: AreaId
    | Suppress of target: TargetArea
    | Assault of target: TargetArea * approach: Approach option
    | WithdrawTo of target: Cell
```

An envelope supplies command ID, issuer, recipients, issue tick, urgency, and risk tolerance.

A command can be syntactically valid but tactically refused by an agent. Command validation and agent appraisal are separate concepts.

## 14. Events

Events are immutable, ordered, and tagged with tick.

Minimum categories:

- command accepted or rejected by the simulation;
- order delivered or communication failed;
- order appraisal outcome;
- contact observed or reported;
- movement started, blocked, or completed;
- shot fired and impact resolved;
- suppression and stress changed at meaningful thresholds;
- wound, incapacitation, or death;
- objective state changed;
- replay or invariant failure in development builds.

Do not emit a flood of low-value events solely because every field changed.

## 15. Render snapshot

The snapshot includes:

- current tick;
- visible or presentation-eligible entities;
- cell and movement interpolation endpoints;
- facing and coarse animation state;
- current selection-relevant status;
- recent appraisal summaries;
- objective state;
- optional development overlays such as path, line of sight, exposure, and contact confidence.

The snapshot contains values, not references to authoritative stores.

## 16. Replay

A replay file records:

- format version;
- source revision or build identifier;
- scenario content hash;
- simulation configuration;
- seed;
- ordered accepted commands;
- optional checkpoint hashes;
- optional human-readable metadata excluded from authority.

Playback rejects incompatible versions clearly. It does not guess migrations.

## 17. State hashing

The hash input must be canonical:

- fields in fixed order;
- entities in ascending ID;
- collections sorted explicitly;
- integers encoded with fixed width and endianness;
- presentation state excluded;
- derived caches either excluded or normalised.

A divergence report should identify the first bad tick. Component-level subhashes are desirable once the world grows.

## 18. Save state

Save-state support is not required for the first command-loop proof. Replay from the start is sufficient for short scenarios. If load times become material, add periodic snapshots through a versioned format and ADR.

## 19. Performance budget

The initial target is intentionally conservative:

- 20 authoritative ticks per second;
- fewer than 64 active agents;
- a map small enough for straightforward grid algorithms;
- no authoritative tick should routinely exceed 5 ms on the development machine;
- no unbounded allocation growth;
- pathfinding work must have a per-tick cap or predictable upper bound once dynamic replanning exists.

Do not optimise immutable F# structures pre-emptively. Profile first, then move measured hot paths to arrays, structs, spans, or controlled mutation.

## 20. Invariants

At minimum:

- one live agent has one authoritative position;
- dead or incapacitated agents do not start new actions;
- an order references existing recipients at acceptance time;
- a path contains traversable adjacent cells;
- ammunition cannot become negative;
- mission completion is emitted at most once;
- entity IDs are unique;
- state hash is independent of presentation state;
- every refusal and adaptation contains at least one structured reason;
- same replay inputs reproduce recorded checkpoints within the supported boundary.
