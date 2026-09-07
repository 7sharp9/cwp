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

Realised by TASK-024 (backlog B-044): a player command carries two
independent tick values. `RecordedCommand.Tick` (`Replay.fs`) is the
**delivery tick** — the tick whose command-intake phase consumes the command,
owned by whoever schedules the run (the headless runner, the replay runner,
later a client command queue). `PlayerCommand.IssuedAtTick` (renamed from
`IssueTick`) is **when the commander issued the order** — envelope provenance
and a future appraisal input (`docs/05` section 5 stage 4 / section 14),
replayed verbatim. The two are not constrained equal; the legacy `.cwlog`
fixture format collapses them to its single tick field. Command intake
enforces one eligibility rule: `IssuedAtTick` must lie in
`[0, currentTick]` — a command issued in the future or before tick 0 is
rejected (`CommandRejection.IssueTickOutOfRange`). A command issued on an
earlier tick and delivered now is **accepted**: staleness is a tactical
judgement for agent appraisal (backlog B-017), not a command-intake rule, so
no give-up horizon is applied. Command scheduling and identity are **not**
authoritative state — `WorldState` holds no command history; cross-tick
`CommandId` uniqueness is a replay-log invariant
(`ReplayError.DuplicateCommandIdInLog`, checked by `Replay.validate`).

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

Realised by TASK-013 (step 2 only, the path query): `src/CommandoWar.Sim/Pathfinding.fs`.
`Pathfinding.find : Terrain -> Cell -> Cell -> PathResult` (and
`Pathfinding.findWithin` with an explicit expansion budget) is a pure, total,
integer-only A* query over `Terrain` passability and entry cost.

- **Algorithm:** A*, 4-connected (cardinal moves only, matching `Direction`
  and the cardinal cover model). Diagonal / 8-connected movement is a
  documented deferral (a scaled integer cost plus the `Sight` corner rule); no
  slice map needs it.
- **Cost model:** entering a cell costs `Terrain.moveCost` for that cell; the
  start cell's own cost is never counted. An `Impassable` cell is never
  expanded and never appears in a path. Path cost is the exact sum of the
  entered cells' costs. `Scenario.validate` confines a passable cell's
  authored `MoveCost` to `[Terrain.BaseMoveCost, Terrain.MaxMoveCost]`
  (TASK-021, `MaxMoveCost` = 1000): below the `Terrain.BlockedCost` sentinel,
  and small enough that accumulation stays a bounded `int`
  (`Width * Height * MaxMoveCost < Int32.MaxValue`).
- **Heuristic:** Manhattan distance times `Terrain.BaseMoveCost`. Integer,
  admissible and consistent for cardinal moves: every passable step now costs
  at least `BaseMoveCost` (the range check above), which is the precondition
  this no-reopening A* needs for an optimal path.
- **Tie-break (stable):** the frontier is ordered by the total key
  `(g + h, then h, then row-major cell index)`; neighbours are generated in
  `Direction.all` order (North, East, South, West). The expansion order and
  the returned path are fully determined regardless of any heap's behaviour
  for equal priorities. Pinned by a golden two-equal-cost-paths example
  (`PathfindingTests.fs`) and a demo golden (`content/diagnostics/path.*`).
- **Bounded work (section 19):** `findWithin` takes an explicit
  `maxExpansions` and returns `BudgetExhausted` when the closed set would
  exceed it; `find` uses a default cap `Width * Height` derived from
  `Terrain.Bounds`. No wall-clock timing.
- **Totality:** an out-of-bounds or impassable start or goal yields
  `InvalidEndpoint`; `start = goal` yields `Found([| start |], 0)`; an
  unreachable goal yields `NoPath`.

Realised by TASK-015 (backlog B-011, single-agent executor): the Navigation
and movement phase (12.7, `src/CommandoWar.Sim/Simulation.fs`) consumes
`Pathfinding`. Steps **2, 4, 5, 6**:

- **Step 2 (compute path):** on a new destination the phase calls
  `Pathfinding.findWithin terrain agent.Position destination
  (Width * Height)` — the full-grid ceiling from
  `content/benchmarks/BASELINE.md`, passed explicitly so a tighter combined
  per-tick multi-agent budget can be set later without touching
  `Pathfinding.find` (an open performance question; not claimed by any task
  yet).
- **Steps 4-5 (advance):** the agent enters the next cell when its
  accumulated progress reaches that cell's `Terrain.moveCost` (TASK-018,
  below); `MovementStepped` on entry, `MovementCompleted` on arrival.
- **Step 6 (replan):** the path (`AgentState.Route`) is recomputed from the
  current cell when the cached next cell is no longer `Terrain.passable` or the
  cache no longer matches `(Position, Destination)`. With static terrain this
  branch stays rare in practice; TASK-017's reservation does not force a
  replan on a yield (the route and cursor are left untouched, not
  invalidated), so multi-agent contention does not exercise it either.
- **No path:** `MovementBlocked (agent, at, target)` is emitted and the
  destination cleared when `Pathfinding` returns `NoPath`, `BudgetExhausted`,
  or `InvalidEndpoint`.

`AgentState.Route` (the followed path + cursor + cost) is a **non-canonical
derived cache**: a pure deterministic function of `(Position, Destination,
Terrain)` at every tick, so it is excluded from `Canonical.encode` (section 17)
and `Canonical.FormatVersion` stays 1. On empty terrain A*'s N/E/S/W tie-break
reproduces the exact cell sequence the retired `PlaceholderMovement` rule
produced (X axis before Y), so the shared fixture's pinned hashes and 33-event
count are unmoved.

Realised by TASK-017 (backlog B-011b, narrowed to reservation and deadlock
avoidance — see "Scope down" below): **step 3**. The phase computes every
agent's movement intent for the tick first (no mutation, no event — Pass 1),
then resolves same-tick contention over a shared next cell (Pass 2) before
applying the surviving moves in ascending agent id order (Pass 3):

- **Step 3 (reserve only the immediate next destination):** when two or more
  agents compute the same next cell for the tick, the agent with the fewest
  remaining route steps wins (ties broken by ascending agent id); every other
  claimant emits `MovementYielded` and does not advance, retrying the same
  next cell next tick once the winner has vacated it.
- **Deadlock avoidance:** provable, not merely tested, for a shared-target-cell
  contest — the winner always advances, so the sum of every moving agent's
  remaining route length strictly decreases each tick a contest is resolved,
  which bounds the wait. This does **not** cover an agent moving onto a cell
  currently held by a stationary agent (a distinct, chain-dependent problem);
  no corpus entry exercises that case, and solving it in general is the
  "negotiation protocol" this task was scoped away from (TASK-017 ledger).
- Reservation is a same-tick derived resolution, not persisted state: computed
  fresh every tick from already-canonical/derived fields only (`Position`,
  `Destination`, `Terrain` via `Route`). `Canonical.FormatVersion` stays 1.

Realised by TASK-018 (backlog B-011c, narrowed to sub-cell movement progress —
formation slots split to **B-011d**): **steps 4-5**. The threshold to enter a
cell is `Terrain.moveCost` of that cell — the same value `Pathfinding` already
uses as its A* edge weight, not a new concept — and the per-tick increment is
`Terrain.BaseMoveCost`. `AgentState.Progress` accumulates toward the next
cell; the agent enters it once progress reaches the threshold, and progress
resets to 0 (a fresh edge starts, whether by entering a cell, arriving,
blocking, or replanning). Reservation (step 3, TASK-017) generalises without
new state: only an agent whose progress *would reach* the threshold this tick
is a claimant of its next cell; one still mid-edge cannot contend, since it is
not entering anything yet, and a claimant that loses a contest freezes its
progress (does not accumulate) rather than resetting or advancing.

`AgentState.Progress` is genuine new canonical state — unlike `Route`, it
cannot be recomputed from `Position` alone, since `Position` does not change
while an edge is in progress, so nothing else records how many ticks have
been spent on it. `Canonical.FormatVersion` bumps to **2**
(`Canonical.encode`, section 17) and every pinned hash moves: the fixture,
every replay-corpus entry, and every `content/diagnostics/*` golden that
embeds a hash/format footer. On every scenario pinned before this task, every
traversed cell already costs exactly `Terrain.BaseMoveCost` (threshold =
increment = 1), so `Progress` is 0 at every post-tick checkpoint: the moved
hashes are a byte-layout artifact, not a behaviour change (content/replays'
tick counts and event counts are unchanged; TASK-018 ledger). A fifth corpus
entry, `slow-terrain`, pins genuine multi-tick accumulation (one cell costing
3) as a determinism regression guard the first four entries cannot provide.

Formation slots remain **B-011d** (new domain concept — a squad/formation
grouping does not exist anywhere yet — closer to
`docs/05_COMMAND_AND_AGENT_AI.md` than to movement mechanics; not attempted
by TASK-018).

Realised by TASK-022 (backlog B-047, a post-gate correctness fix to
TASK-015 / TASK-017): **runtime cell-occupancy correctness**. Rival
arbitration (step 3, TASK-017) only decides which completing agent may claim a
*contested* cell; it never checks whether that cell is already held by a
stationary agent, so a mover with no rival for its next cell used to enter it
unconditionally — over an idle, arrived, blocked, or still-mid-edge occupant.
TASK-022 adds a second resolution stage to Pass 2, after rival arbitration and
before apply:

- **The occupancy policy.** A stationary-occupied cell blocks entry; a
  two-agent position swap is blocked (no agent may pass through another); an
  n-agent rotation cycle is blocked (no first mover — and on a 4-connected
  grid, which is bipartite, the smallest pure cycle has four agents); a follow
  chain advances **this tick** only when the whole chain resolves to a free
  cell.
- **The vacation chain.** Let `M0` be the completing agents that did not lose a
  rival contest (at most one per target cell) and `occupant(c)` the unique
  agent whose pre-tick `Position` is `c`. An agent `a in M0` may move iff the
  chain `a -> occupant(next a) -> occupant(next (occupant (next a))) -> ...`
  terminates at an agent whose next cell has no occupant — it neither reaches a
  cycle nor an agent that is not itself a moving candidate. Computed as an
  additive fixpoint (monotone, order-independent, at most n rounds): seed with
  every `a` whose `next a` is unoccupied, then repeatedly add every `a` whose
  `next a` is held by an agent already known to move. `obstructedBy` maps every
  remaining `a` to `occupant(next a).Id`.
- **No simultaneous rotation.** Swaps and pure cycles fall out of the fixpoint
  as all-obstructed with no special case. TASK-022 deliberately does not add an
  atomic multi-agent swap / rotate-in-place primitive: it needs a tactical
  justification that does not exist yet.
- **New event: `MovementObstructed of agent * at * blocked * occupant`.** A
  movement outcome, emitted in Pass 3 in ascending agent id order. Distinct
  from `MovementBlocked` (no traversable path; destination cleared) and
  `MovementYielded` (lost a same-tick rival contest to another *mover*): the
  obstructed agent's destination and route are unchanged and it retries next
  tick. The obstructed agent freezes exactly like a rival-contest loser
  (`Progress = startProgress`, `Route` written back, `Position` /
  `Destination` untouched).
- **Persistent obstruction is not resolved here.** An agent permanently blocked
  by one that never moves retries — and re-emits `MovementObstructed` — every
  tick, forever. Routing around a live agent is the cooperative pathfinder
  TASK-022 forbids; noticing a persistent stall and re-appraising the order is
  a perception / appraisal concern (**B-015 / B-017**).
- The resolution is a same-tick pure function of already-canonical pre-tick
  positions plus this tick's intents — the identical argument that kept
  reservation out of the canonical image in TASK-017. `Canonical.FormatVersion`
  stays **2**; no `AgentState` / `WorldState` field is added; no pinned hash
  moves (every scenario pinned before this task already keeps one agent per
  cell). `World.create` / `World.ofScenario` gain a `WorldError.AgentsShareCell`
  guard so the one-agent-per-cell base case (section 20) holds from tick 0 by
  construction on the direct path too, not only through `Scenario.validate`.

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

Partially realised by TASK-020 and extended by TASK-024
(`Simulation.commandIntake`): the batch is stably sorted by command id in
`Simulation.step`, then, in order — every command in a group that shares a
`CommandId` within this tick's batch is rejected (`DuplicateCommandId`,
order-independent, none processed); each surviving command is checked whole
(`IssueTickOutOfRange` when `IssuedAtTick` is outside `[0, currentTick]` ->
`EmptyRecipients` -> `DuplicateRecipient` -> `TargetOutOfBounds`, first
failure short-circuits with one `CommandRejected` and no per-recipient
events); then each recipient is checked in ascending `AgentId` order
(`UnknownAgent` -> `UnauthorisedRecipient` for a `Hostile`-side agent ->
accept), emitting `CommandAccepted` **before** the destination is written.
One accept/reject event per (command, recipient) pair. The agent array is
copied once per phase invocation. Issue-tick eligibility is now enforced
(TASK-024): `IssuedAtTick` — renamed from `IssueTick`, distinct from the
delivery tick (section 2) — must be `>= 0` and not after the tick being
processed; a stale (long-delayed) command is still accepted, since following
an outdated order is an appraisal decision (B-017), not a validation one.
"Unauthorised" here is still only the friendly/hostile-side check — no issuer
identity or commander model.

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

Realised by TASK-015 (single-agent executor), TASK-017 (reservation),
TASK-018 (sub-cell progress), and TASK-022 (cell-occupancy correctness):
`Simulation.navigationAndMovement` computes / repairs a `Pathfinding` path per
agent with a destination, resolves same-tick contention over a shared next
cell in stable order (fewest remaining route steps among agents that would
complete the edge this tick, ties broken by ascending agent id), then resolves
the vacation chain so no mover enters a cell a stationary agent still holds
(swaps and rotation cycles blocked, follow chains advanced only when they
clear), advances `AgentState.Progress` toward the next cell's `Terrain.moveCost`
threshold and enters it once reached, and emits `MovementStepped` /
`MovementCompleted` / `MovementBlocked` / `MovementYielded` / `MovementObstructed`.
Reservation and vacation-chain resolution are both resolved fresh every tick,
not stored; `Progress` is genuine per-tick canonical state
(`Canonical.FormatVersion` 2). Full detail is in section 8.

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

**Partially realised by TASK-020, extended by TASK-024.** `PlayerCommand`
carries `Id`, `Recipients: AgentId list` (generalised from a single agent;
`Command.moveTo` builds a one-element list, `Command.moveToMany` a
multi-recipient one), `IssuedAtTick` (renamed from `IssueTick` — the tick the
order was issued, distinct from the delivery tick and range-checked at
command intake, section 2 / section 12.1), `Urgency` (`Routine | Immediate`)
and `RiskTolerance` (`Cautious | Standard | Aggressive`). `Urgency`,
`RiskTolerance`, and `IssuedAtTick` are **inert beyond validation** —
`docs/05` section 5 stage 4 / section 14 name them as appraisal inputs, but
appraisal (B-017) does not exist and nothing reads them yet. Still **out of
this partial envelope**: issuer identity (not modelled — one player). The
remaining mandatory-before-G3 follow-up is **B-045** (the production
replay-command serialisation — after TASK-020 the legacy `.cwlog` fixture
grammar can no longer express a full accepted command; it now also cannot
express an `IssuedAtTick` distinct from the delivery tick). B-044 (issue-tick
semantics) is done.

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

Realised by TASK-003: `src/CommandoWar.Sim/Replay.fs`. `ReplayRecord` (format version 1) carries the canonical-format version, provenance metadata, seed, tick-0 initial state, tick count, and a `CommandLog` (version 1) of `RecordedCommand { Tick; Sequence; Command; Issuer }`, where `Tick` is the delivery tick — independent of the envelope's `Command.IssuedAtTick` (section 2; TASK-024). `Replay.run` rejects unsupported replay, command-log, and canonical-format versions, a non-tick-0 initial state, a seed inconsistent with the initial stream, out-of-range or non-monotonic commands, and (TASK-024) a log that reuses one `CommandId` on two ticks (`DuplicateCommandIdInLog`), each with a typed `ReplayError`.

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

TASK-015 note: `AgentState.Route` (the path an agent is following, its cursor,
and its cost) is per-tick mutable but is a **derived cache** — a pure
deterministic function of `(Position, Destination, Terrain)` recomputable at
any tick — so it is **excluded** from `Canonical.encode` under the "derived
caches either excluded or normalised" rule above. Two runs of the same inputs
produce identical caches (`Pathfinding.findWithin` is total, pure, integer-only,
deterministic), so it cannot diverge. `Canonical.FormatVersion` stays `1`. A
route-following bug still surfaces in the hash within one tick because the
agent's `Position` is canonical.

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

Realised by TASK-014 (backlog B-013): the benchmark harness
`bench/CommandoWar.Benchmarks/` exists, and `content/benchmarks/BASELINE.md` is
the recorded baseline (median, P95, allocation per operation, run environment,
commit) with a documented regeneration command. On the reference machine every
per-tick measurement is at least ~36x inside the 5 ms budget, including a
full-grid A* query run once per tick, and the empty and movement ticks show no
allocation proportional to map size. The **5 ms per-tick** and **no unbounded
per-tick allocation** budgets therefore stand as written; a tighter
per-subsystem number is a follow-up once the real Perception / Appraisal /
Combat phases exist (B-015 / B-017 / B-019) and the greybox map is built
(B-025), re-running the harness against them. The `Pathfinding.find` default
expansion cap (`Width * Height`) is left unchanged: `BASELINE.md` records that a
single legitimate query on an adversarial 40x40 map already closes ~73% of the
grid, so the cap is a correctly-sized safety ceiling and B-011 owns any
per-tick pathfinding budget.

## 20. Invariants

At minimum:

- one live agent has one authoritative position — enforced pairwise: the
  Navigation and movement phase never places two live agents on the same cell
  (TASK-022 vacation-chain resolution), and `World.create` / `World.ofScenario`
  reject a cell-sharing construction (`WorldError.AgentsShareCell`);
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
