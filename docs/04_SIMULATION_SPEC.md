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

Realised by TASK-003: `SplitMix64` version 1 in `src/CommandoWar.Sim/Random.fs`, exposed through `IDeterministicRandom`. Single 64-bit additive counter, wrapping arithmetic, seed initialises the counter directly. State (`RandomState`) is a value record carrying the algorithm, its version, the counter word, and a draw counter. One stream, held on `WorldState.Random`; no gameplay draws yet.

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

Realised by TASK-010: `src/CommandoWar.Sim/Terrain.fs`. `Terrain` is a dense
row-major integer grid for one `GridBounds` carrying, per cell: elevation
level (`Elevation`), a `MovementClass` (`Passable` / `Impassable`) plus an
integer entry cost (`MoveCost`), an opacity flag (`Opaque`, the "high
occlusion" layer), and a directional low-cover level per cardinal direction
(`Cover`, indexed `cellIndex * 4 + Direction.index d`). Occupancy capacity,
water/hazard classification, and destruction state are not implemented (no
mission behaviour exercises them yet; destruction is B-019). Queries
(`Terrain.passable` / `moveCost` / `elevation` / `opaque` / `cover`) are total
and bounds-checked. `WorldState` gains `Terrain`; `World.create` builds an
empty (flat, passable, transparent, uncovered) grid and `World.ofScenario`
builds it from the validated authored layer. **No tick phase reads terrain
yet** (backlog B-009 line of sight, B-010 pathfinding, B-011 movement are the
first consumers), exactly as the TASK-008 `Objective` algebra is authored but
not evaluated.

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

Realised by TASK-010 (cover data only): directional low cover is authored and
stored per cell per cardinal direction as an integer level
(`Terrain.cover : Terrain -> Cell -> Direction -> int`,
`src/CommandoWar.Sim/Terrain.fs`), and the opacity flag that a sight
algorithm will read is `Terrain.opaque`.

Realised by TASK-012 (line of sight): `src/CommandoWar.Sim/Sight.fs`.
`Sight.trace : Terrain -> Cell -> Cell -> LineOfSight` (and `Sight.visible`,
defined from it) is a pure, total, integer-only query over `Terrain` opacity
and elevation returning `{ Visible; Path: Cell[]; Blocker: Cell option }`.

- **Algorithm:** the classic integer supercover grid walk
  (`decision = (1 + 2*ix)*ny - (1 + 2*iy)*nx`; `< 0` step x, `> 0` step y,
  `= 0` a single diagonal step). No floating point, no `System.Math`.
- **Corner rule:** a diagonal step is blocked only when *both* shared-edge
  neighbours of that step are `Terrain.opaque` (no sight through a solid
  inner corner; sight passes a single wall cell at a diagonal corner).
- **Elevation rule:** an intermediate cell blocks when it is `Terrain.opaque`
  OR its elevation is strictly greater than the elevation of *both* endpoints
  (a ridge occludes). Endpoint elevation otherwise neither grants nor denies
  sight; eye-height / height-field reasoning is deferred.
- **Symmetry:** `Sight.visible t a b = Sight.visible t b a` for every pair
  (the supercover set is direction-independent; the blocking rule is over
  sets identical in both directions). Pinned by a property test and golden
  examples (`SightTests.fs`, `content/diagnostics/los.*`).
- **Totality:** an out-of-bounds endpoint sees nothing (`Visible = false`,
  `Blocker = None`).

No tick phase consumes it: like `Terrain` and the `Objective` algebra it is
authored and queryable but not evaluated. Perception (phase 12.3, backlog
B-015) is the first consumer. `Canonical.encode` and `Canonical.FormatVersion`
are unchanged; `Sight.fs` is a leaf nothing authoritative references.

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

Realised by TASK-003: `src/CommandoWar.Sim/Replay.fs`. `ReplayRecord` (format version 1) carries the canonical-format version, provenance metadata, seed, tick-0 initial state, tick count, and a `CommandLog` (version 1) of `RecordedCommand { Tick; Sequence; Command; Issuer }`. `Replay.run` rejects unsupported replay, command-log, and canonical-format versions, a non-tick-0 initial state, a seed inconsistent with the initial stream, and out-of-range or non-monotonic commands, each with a typed `ReplayError`.

## 17. State hashing

The hash input must be canonical:

- fields in fixed order;
- entities in ascending ID;
- collections sorted explicitly;
- integers encoded with fixed width and endianness;
- presentation state excluded;
- derived caches either excluded or normalised.

A divergence report should identify the first bad tick. Component-level subhashes are desirable once the world grows.

Realised by TASK-003: `Canonical.encode` (`src/CommandoWar.Sim/Canonical.fs`, format version 1) produces a big-endian fixed-width byte image of `WorldState` only, agents sorted ascending by id, `Destination` carrying an explicit present/absent tag; events, snapshots, and phase traces are excluded. `Hashing.hash` (`Hashing.fs`) is FNV-1a-64 over that image, exposed through `IStateHasher`; it is not a cryptographic primitive. `Simulation.step` records `StepResult.StateHash` after the Output phase without any authoritative output depending on it. `Divergence.compare` reports the first tick whose hash differs, the expected and actual hash, the first differing canonical section, and the random draw count on each side.

TASK-010 note: `WorldState.Terrain` is authoritative but is **excluded** from
`Canonical.encode` while it carries no per-tick mutable state. Static
authoritative data that is a pure function of the validated scenario cannot
diverge tick to tick, so hashing it every tick would move every pinned
fixture hash for no determinism benefit. `Canonical.FormatVersion` stays `1`;
it bumps and the canonical image gains a terrain section when destructible
terrain lands (backlog B-019) or any phase otherwise mutates terrain. This is
recorded in the ADR-0002 amendment "Static authoritative data and the
canonical image".

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

## 21. Authored scenario and content version

An authored scenario is framework-neutral typed data: a scenario id, map
dimensions, friendly and enemy deployments (agent id, side, cell), an objective
algebra, objective and extraction areas, static targets, scenario-wide rules,
and an optional authored terrain layer (TASK-010; elevation, passability and
movement cost, opacity, directional low cover). Line of sight and pathfinding
are still not part of it (sections 8 to 9; backlog B-009, B-010), and objective
evaluation and mission success/failure are deferred (backlog B-032) so the
objective algebra is a data-only type at this stage; the terrain grid the
layer produces is likewise not consumed by any tick phase.

The authored input is versioned by a content-format version that is independent
of the canonical-state format version (section 17) and the replay container
version (section 16): it versions the authored shape and the validation
contract, not the state encoding. An unsupported version is a typed error, not
a guessed migration.

Validation is one pass over the raw input and returns either a validated
scenario or the full list of faults. Each fault names the offending object and,
where relevant, the expected value; no silent default is supplied for invalid
data. It reports: an unsupported content version; a blank scenario id;
non-positive map dimensions; a duplicate, negative, out-of-map, or
cell-sharing deployment; a duplicate or blank area or target id; an area or
target marker outside the map; a duplicate or negative objective id; an unknown
objective class; an objective referencing a missing area or target; an
extraction selecting an unknown agent; a missing required marker (no
friendly deployment, no objective, no extraction area); and, for the terrain
layer, a layer whose dimensions disagree with the map, an out-of-map terrain
cell or cover feature, a duplicate terrain cell or cover feature, an unknown
terrain or cover class, a negative elevation, move cost, or cover level, and a
deployment on an authored impassable cell.

An absent terrain layer is legal and means empty terrain (flat, fully
passable, transparent, uncovered).

A validated scenario builds the authoritative world by deploying its agents
(friendly then enemy, ordered ascending by id) through the same construction
path as any other world.

Realised by TASK-008 and extended by TASK-010: `src/CommandoWar.Sim/Scenario.fs`.
`ScenarioContent.Version` = 2 (TASK-010 bumped it from 1 for the authored
terrain layer; version 1 is rejected, not migrated), independent of
`Canonical.FormatVersion` and `Replay.FormatVersion`. `Scenario.validate :
RawScenario -> Result<Scenario, ScenarioError list>` collects every fault in
one pass (`ScenarioError`, 30 explicit cases in the `ReplayError` style). The
`Objective` algebra is `ReachArea` / `HoldArea` / `DestroyTarget` /
`ExtractAgents` / `AllOf` / `Optional`, data only, with evaluation deferred.
`RawScenario.TerrainLayer : RawTerrainLayer option` carries the optional
authored terrain; `Scenario.Terrain : Terrain` is the validated grid
(`Terrain.empty Map` when no layer was authored). `World.ofScenario : Scenario
-> uint64 -> Result<WorldState, WorldError>` builds the world and carries
`scenario.Terrain` onto it through the same construction core as
`World.create`. A pinning test builds the six-agent shared fixture as a
`Scenario` (no terrain layer), runs it through `World.ofScenario` with seed
20260902 to the pinned initial hash `0xF2F3DF0D820AD9AC`, and steps 40 ticks
with the fixture command to the pinned final hash `0x838D3AE7DBFB735D`, tying
the model to the existing determinism evidence without changing the fixture,
`Canonical.encode`, or `Setup.sixAgentWorld`.
