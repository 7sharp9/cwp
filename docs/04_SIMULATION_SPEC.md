# Authoritative Simulation Specification

Status: baseline for tasks after TASK-002  
Last revised: 2026-10-05 (rewritten as a current-state specification; per-task history is in the ledger)

## 1. Scope

This document specifies the authoritative tactical simulation for the vertical slice. It does not specify rendering, animation, audio, menus, or editor behaviour.

## 2. Time model

- Nominal rate: 20 ticks per second.
- Tick is an unsigned or non-negative integer value with checked progression.
- All durations are expressed in ticks.
- A command becomes eligible on a specified tick.
- The host may pause, single-step, or catch up, but cannot partially execute an authoritative tick.

Do not use floating-point `dt` in authoritative systems.

A player command carries two independent tick values (TASK-024, backlog
B-044). `RecordedCommand.Tick` (`Replay.fs`) is the **delivery tick**: the tick
whose command-intake phase consumes the command, owned by whoever schedules the
run (the headless runner, the replay runner, a client command queue).
`PlayerCommand.IssuedAtTick` is **when the commander issued the order**:
envelope provenance and a future appraisal input (`docs/05` section 5 stage 4 /
section 14), replayed verbatim. The two are not constrained equal; the legacy
`.cwlog` fixture format collapses them to its single tick field. Command intake
enforces one eligibility rule: `IssuedAtTick` must lie in `[0, currentTick]`; a
command issued in the future or before tick 0 is rejected
(`CommandRejection.IssueTickOutOfRange`). A command issued on an earlier tick
and delivered now is **accepted**: staleness is a tactical judgement for agent
appraisal, not a command-intake rule, so no give-up horizon is applied. Command scheduling and identity are **not** authoritative state:
`WorldState` holds no command history, and cross-tick `CommandId` uniqueness is
a replay-log invariant (`ReplayError.DuplicateCommandIdInLog`, checked by
`Replay.validate`).

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

The implementation is `SplitMix64` version 1 (`src/CommandoWar.Sim/Random.fs`, TASK-003), exposed through `IDeterministicRandom`. It uses a single 64-bit additive counter with wrapping arithmetic, and the seed initialises the counter directly. State (`RandomState`) is a value record carrying the algorithm, its version, the counter word, and a draw counter. There is one stream, held on `WorldState.Random`. The Combat phase (section 12.8) is its only gameplay consumer: one draw per engaging agent, in ascending shooter id, plus one further independent draw for the radio-destroy chance when a hit lands on a target whose radio is not already destroyed in a world that authors a Headquarters (TASK-058). Draw order is therefore stable within the one system that draws.

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

`src/CommandoWar.Sim/Terrain.fs` (TASK-010) implements the data the slice
needs. `Terrain` is a dense row-major integer grid for one `GridBounds`
carrying, per cell: elevation level (`Elevation`), a `MovementClass`
(`Passable` / `Impassable`) plus an integer entry cost (`MoveCost`), an opacity
flag (`Opaque`, the "high occlusion" layer), and a directional low-cover level
per cardinal direction (`Cover`, indexed `cellIndex * 4 + Direction.index d`).
Occupancy capacity, water/hazard classification, and destruction state are not
implemented, because no mission behaviour exercises them (destructible buildings
are a deliberate slice exclusion, `docs/07` section 7). Queries
(`Terrain.passable` / `moveCost` / `elevation` / `opaque` / `cover`) are total
and bounds-checked. `WorldState.Terrain` holds the grid; `World.create` builds
an empty (flat, passable, transparent, uncovered) grid and `World.ofScenario`
builds it from the validated authored layer. Terrain is static within a run.
The Navigation and movement, Perception, Appraisal, and Combat phases read it
(through `Pathfinding`, `Sight`, and `Terrain.cover`; sections 8 and 9).

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

Complex formation maintenance is deferred. Formation slots (TASK-059, backlog
B-011d) are resolved before this phase: for a group `MoveTo` order,
`Appraisal.resolveFormationTarget` (section 12.5) redirects the order's target
to the agent's slot, and Navigation is driven purely by whatever `Destination`
it is handed.

The path query (step 2) is `src/CommandoWar.Sim/Pathfinding.fs` (TASK-013).
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
  (`BaseMoveCost` = 1, `MaxMoveCost` = 1000; TASK-021): below the
  `Terrain.BlockedCost` sentinel, and small enough that accumulation stays a
  bounded `int` (`Width * Height * MaxMoveCost < Int32.MaxValue`).
- **Heuristic:** Manhattan distance times `Terrain.BaseMoveCost`. Integer,
  admissible and consistent for cardinal moves: every passable step costs at
  least `BaseMoveCost` (the range check above), which is the precondition this
  no-reopening A* needs for an optimal path.
- **Tie-break (stable):** the frontier is ordered by the total key
  `(g + h, then h, then row-major cell index)`; neighbours are generated in
  `Direction.all` order (North, East, South, West). The expansion order and
  the returned path are fully determined regardless of any heap's behaviour
  for equal priorities. On empty terrain the path has a single turn: it runs
  along the X axis first when the goal lies at a greater Y than the start, and
  along the Y axis first when it lies at a smaller Y. Pinned by a golden
  two-equal-cost-paths example (`PathfindingTests.fs`) and a demo golden
  (`content/diagnostics/path.*`).
- **Bounded work (section 19):** `findWithin` takes an explicit
  `maxExpansions` and returns `BudgetExhausted` when the closed set would
  exceed it; `find` uses a default cap `Width * Height` derived from
  `Terrain.Bounds`. No wall-clock timing.
- **Totality:** an out-of-bounds or impassable start or goal yields
  `InvalidEndpoint`; `start = goal` yields `Found([| start |], 0)`; an
  unreachable goal yields `NoPath`.

The Navigation and movement phase (12.7, `Simulation.navigationAndMovement` in
`src/CommandoWar.Sim/Simulation.fs`; TASK-015, backlog B-011) consumes
`Pathfinding` and implements steps 2 to 6. It works in three passes. Pass 1
computes every agent's movement intent for the tick (no mutation, no event).
Pass 2 resolves same-tick contention over shared next cells: rival
arbitration, then occupancy resolution. Pass 3 applies the surviving moves and
emits events in ascending agent id order. An agent that is not `Alive` never
starts an action (section 20): its intent is idle.

- **Step 2 (compute path):** on a new destination the phase calls
  `Pathfinding.findWithin terrain agent.Position destination
  (Width * Height)`, the full-grid ceiling from
  `content/benchmarks/BASELINE.md`. The budget is passed explicitly so a
  tighter combined per-tick multi-agent budget can be set later without
  touching `Pathfinding.find`; that is an open performance question (section
  19).
- **Steps 4-5 (advance):** the threshold to enter a cell is `Terrain.moveCost`
  of that cell, the same value `Pathfinding` uses as its A* edge weight, and
  the per-tick increment is `Terrain.BaseMoveCost` for every agent
  (TASK-018, backlog B-011c). `AgentState.Progress` accumulates toward the next
  cell; the agent enters it once its progress reaches the threshold (the
  speed-scaled comparison below), emitting `MovementStepped` on entry and
  `MovementCompleted` on arrival. Progress is scoped to the current edge and
  resets to 0 whenever the edge changes: a fresh route is computed, the agent
  enters a cell, arrives, or is blocked. An agent still mid-edge only
  accumulates progress and emits no event.
- **Per-agent speed (TASK-049, backlog B-058):** `AgentState.MoveSpeed` is
  resolved from an authored `RawScenario.UnitTypes` table; each
  `RawDeployment.UnitType` references one entry by id, there is no silent
  default, and `Scenario.validate` rejects an unknown reference. The
  per-tick increment stays the universal `Terrain.BaseMoveCost`, so
  `AgentState.Progress` is always the elapsed real-tick sequence 0, 1, 2, ...;
  only the edge-completion comparison scales, cross-multiplied against
  `Agent.MoveSpeedDefault` (2): `(startProgress + Terrain.BaseMoveCost) *
  MoveSpeed >= Terrain.moveCost next * Agent.MoveSpeedDefault`. An agent whose
  `MoveSpeed` equals `Agent.MoveSpeedDefault` is unscaled (multiplying both
  sides of an inequality by the same positive constant does not change it); a
  smaller `MoveSpeed` needs proportionally more ticks to cross the same cell.
  `Pathfinding`'s route cost is unaffected, since it measures
  `Terrain.moveCost` only, never real-time ticks. Below, an agent whose
  comparison holds this tick is said to complete its edge.
- **Step 6 (replan):** the path (`AgentState.Route`) is recomputed from the
  current cell when the cached next cell is no longer `Terrain.passable` or the
  cache no longer matches `(Position, Destination)`. With static terrain this
  branch is rare. A reservation loss or an occupant freeze does not invalidate
  the route (the route and cursor are left untouched), so multi-agent
  contention does not exercise it either.
- **No path:** `MovementBlocked (agent, at, target)` is emitted, the
  destination cleared and progress reset when `Pathfinding` returns `NoPath`,
  `BudgetExhausted`, or `InvalidEndpoint`.
- **Step 3 (reserve only the immediate next destination; TASK-017, backlog
  B-011b):** only an agent that completes its edge this tick is a claimant of
  its next cell; one still mid-edge is not entering anything and cannot
  contend. When two or more claimants compute the same next cell, the agent
  with the fewest remaining route steps wins (ties broken by ascending agent
  id). Every other claimant emits `MovementYielded`, freezes its progress
  (neither accumulates nor resets), and retries the same next cell next tick
  once the winner has vacated it. Reservation is a same-tick derived
  resolution, not persisted state: it is computed fresh every tick from
  canonical or derived fields only (`Position`, `Progress`, `Destination`,
  `Terrain` via `Route`). It terminates for a shared-target-cell contest: the
  winner always advances, so the sum of every completing agent's remaining
  route length strictly decreases each tick a contest is resolved, which bounds
  the wait. This does not cover an agent moving onto a cell held by a
  stationary agent, a distinct, chain-dependent problem handled by the three
  items below. No negotiation protocol between agents is built.
- **Cell occupancy (TASK-022, backlog B-047):** rival arbitration only decides
  which completing agent may claim a *contested* cell; a second stage of Pass
  2, after arbitration and before apply, decides whether that cell is free.
  - *Policy.* A stationary-occupied cell blocks entry; a two-agent position
    swap is blocked (no agent may pass through another); an n-agent rotation
    cycle is blocked (no first mover; on a 4-connected grid, which is
    bipartite, the smallest pure cycle has four agents); a follow chain
    advances this tick only when the whole chain resolves to a free cell. A
    non-`Alive` agent is not an occupant: it never vacates on its own and
    would otherwise block its cell forever (TASK-066, backlog B-066), and it
    can never itself be a mover, so the exclusion changes only what other
    agents may enter. `Pathfinding` has no occupancy concept and needs none
    for this: a live agent treats a corpse's cell as free.
  - *Vacation chain.* Let `M0` be the completing agents that did not lose a
    rival contest (at most one per target cell) and `occupant(c)` the unique
    agent whose pre-tick `Position` is `c`. An agent `a in M0` may move iff the
    chain `a -> occupant(next a) -> occupant(next (occupant (next a))) -> ...`
    terminates at an agent whose next cell has no occupant: it neither reaches
    a cycle nor an agent that is not itself a moving candidate. It is computed
    as an additive fixpoint (monotone, order-independent, at most n rounds):
    seed with every `a` whose `next a` is unoccupied, then repeatedly add every
    `a` whose `next a` is held by an agent already known to move.
    `obstructedBy` maps every remaining `a` to `occupant(next a).Id`. Swaps and
    pure cycles fall out as all-obstructed with no special case. A follow
    chain resolves in a single tick, and Pass 3 applies the moves from
    precomputed decisions, so its ascending-id order cannot create a transient
    collision. No atomic multi-agent swap or rotate-in-place primitive is
    built: it needs a tactical justification that does not exist.
  - *`MovementObstructed of agent * at * blocked * occupant`.* A movement
    outcome emitted in Pass 3 in ascending agent id order. Distinct from
    `MovementBlocked` (no traversable path; destination cleared) and
    `MovementYielded` (lost a same-tick rival contest to another *mover*): the
    obstructed agent's destination and route are unchanged and it retries next
    tick. It freezes exactly like a rival-contest loser (`Progress =
    startProgress`, `Route` written back, `Position` and `Destination`
    untouched).
  - The resolution is a same-tick pure function of pre-tick positions plus this
    tick's intents. `World.create` and `World.ofScenario` reject a cell-sharing
    construction with `WorldError.AgentsShareCell`, so the one-live-agent-per-
    cell base case (section 20) holds from tick 0 on the direct path too, not
    only through `Scenario.validate`.
- **Detour around a parked blocker (TASK-070, backlog B-069; TASK-076, backlog
  B-076):** before freezing against a stationary occupant that itself holds no
  active order (`Destination = None`, so it will never vacate on its own), the
  phase tries one detour. An actively-ordered blocker that merely did not
  vacate this exact tick is left to the freeze and give-up path. The phase
  calls `Pathfinding.findWithin`, whose own contract is unchanged (a pure
  function of `Terrain` and two `Cell`s), against a locally patched, throwaway
  `Terrain` (`Terrain.withImpassable`) that marks as impassable: every `Alive`
  agent's cell where `Destination = None`; the next cell of every agent that is
  advancing this tick (from Pass 1); and the first cell of every detour adopted
  earlier in the same Pass 3. If a genuine alternate route is found it is
  adopted (`MovementRerouted of agent * at * newNext * avoided`, `Route`
  replaced, `Progress` and `StalledTicks` reset to 0, no movement that tick);
  if not, the freeze-then-abandon path applies. `Pathfinding.fs` gains no
  occupancy parameter and no space-time search: the occupancy signal lives
  entirely in this phase, and `s.Terrain` is never written. The advancing and
  claimed cells keep two agents that detour around the same blocker from
  independently choosing the same alternate cell and obstructing each other
  (the `swap-standoff` shape, `docs/10_RISK_REGISTER.md` R-010). A detour
  claims only its first cell; later cells of a multi-cell detour are handled by
  the ordinary rival-contest logic on later ticks. All three exclusion sets are
  local to one tick and are not persisted.
- **Give up on a permanent freeze (TASK-065, backlog B-065):** because step 6's
  replan is never forced by a same-tick reservation loss or a stationary
  occupant, a `MovementYielded`/`MovementObstructed` freeze against an occupant
  that never vacates would repeat every tick forever (`docs/10_RISK_REGISTER.md`
  R-010, deadlocks). `AgentState.StalledTicks` counts consecutive
  freezes against the same route. A fresh route computed this tick (a new
  order, or a replan) restarts the count at 0 before this tick's own freeze is
  counted, so a new order is never abandoned on its first unlucky tick and a
  different destination never inherits a stale count. The count also resets to
  0 on any real advance (including mid-edge accumulation), on arrival, on
  `MovementBlocked`, and on a reroute. Once a freeze would bring the count to
  `Simulation.StallAbandonTicks` (40), the phase abandons the order instead:
  `Destination` and `Route` clear, `Progress` and the counter reset to 0, and
  `MovementAbandoned of agent * at * target` is emitted in place of that tick's
  `MovementYielded`/`MovementObstructed`. This turns a silent, permanent freeze
  (including one against an actively-ordered occupant that never moves) into a
  visible failure. Making `Pathfinding` occupancy-aware, a larger direction
  that would change its contract, was not chosen.

`AgentState.Route` (the followed path, cursor and cost) is a **non-canonical
derived cache**, excluded from `Canonical.encode` (section 17). A route found by
the ordinary query is a function of `(Position, Destination, Terrain)`, but a
detour route (above) also depends on where other agents were on the tick it was
adopted, so a `Route` cannot be rebuilt from a canonical image alone. Two runs
of the same inputs still produce identical routes, and replay always starts
from tick 0 (sections 16 and 18), so the exclusion does not weaken the
determinism contract. `AgentState.Progress` and `AgentState.StalledTicks` are canonical state:
neither can be recomputed from `Position` alone, since `Position` does not
change while an edge is in progress or an agent is frozen, so nothing else
records how long. `AgentState.MoveSpeed` is static authored data (the
`Discipline` / `CommunicationAvailable` precedent) and is excluded from
`Canonical.encode`; the authored `UnitTypes` table and `RawDeployment.UnitType`
field are part of the scenario content (section 21). The canonical format
version is given in section 17.

## 9. Line of sight and cover

- Line of sight operates on logical cells and elevation.
- The algorithm and corner rules must be documented and covered by golden examples.
- Directional cover depends on the attack vector.
- Presentation occlusion does not define simulation visibility.
- Contacts are observations, not direct references to all enemy state.

The implementation is `Sight` (`src/CommandoWar.Sim/Sight.fs`, TASK-012), an
integer supercover grid walk covered by golden examples and a symmetry property
test. Directional low cover is authored and stored per cell per cardinal
direction as an integer level (`Terrain.cover : Terrain -> Cell -> Direction ->
int`, `src/CommandoWar.Sim/Terrain.fs`, TASK-010), and the opacity flag that
sight reads is `Terrain.opaque`.

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

`Sight` holds no state. Perception (12.3) uses `Sight.visible` for observation
and Combat (12.8) for line of fire, and Appraisal (12.5) evaluates exposure
with it. Directional cover (`Terrain.cover`) reduces hit chance, suppression
gain and appraised exposure on the edge the fire arrives from.

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

The implemented record (`src/CommandoWar.Sim/Domain.fs`) departs from the
sketch as follows.

- **Canonical state.** `Tick`, `Bounds`, `Random`, `Agents` (ascending by id),
  `TacticalKnowledge`, `HostileTacticalKnowledge`, and the mission state
  (`MissionOutcome`, `CompletedObjectives`, `ObjectiveProgress`, section
  12.10) are in `Canonical.encode`. The layout version is
  `Canonical.FormatVersion`, currently 15 (section 17).
- **Static authored data.** `Terrain` (TASK-010), `ResupplyAreas`,
  `Headquarters`, `Jammers`, `Objectives`, `ObjectiveAreas`,
  `ExtractionAreas`, `StaticTargets`, and `Rules` are set once from the
  scenario and never change during a run. Both runs of a scenario load the
  identical values at tick 0, so they cannot diverge and are excluded from the
  canonical image (section 17; the ADR-0002 amendment). A behaviour that
  depends on one of them still shows in the hash within a tick, through the
  agent state it changes.
- **`TacticalKnowledge: Contact[]`** (TASK-026, backlog B-015) is the friendly
  squad's shared contact picture, ascending by contact id, each
  `{ Contact: AgentId; LastKnownCell: Cell; LastSeenTick: int64;
  Confidence: int }`. It is genuine per-tick canonical state: it carries
  memory (`LastSeenTick`, the decaying `Confidence`) that the current
  positions cannot reproduce. **`HostileTacticalKnowledge: Contact[]`**
  (TASK-034, backlog B-022, partial) has the identical shape and the same
  status for the Hostile side. Section 12.4 describes how both are built.
- **No `SquadStore`.** Every `Friendly` agent is the one squad. A formation is
  a per-agent slot offset (`AgentState.FormationOffset`, section 12.5), not a
  grouping container (TASK-059, backlog B-011d).
- **No `OrderStore`.** Orders are per-agent state (`AgentState.Order`,
  `.OrderQueue`, `.PendingDelivery`, section 11).
- **No `ProjectileStore`.** Combat is hitscan (section 12.8).

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

`AgentState` (`src/CommandoWar.Sim/Domain.fs`) realises these as follows.

- **Identity and side.** `Id` and `Side` (`Friendly | Hostile`). There is no
  squad field (section 10).
- **Position and movement.** `Position`; `Progress`, the integer progress
  toward entering the next cell along the current edge, always 0 at rest and
  after entering a cell; `Destination: Cell option`; `StalledTicks`, the
  consecutive ticks the current movement has been frozen (section 12.7);
  `MoveSpeed`, static (section 8); and `Route`, the followed path, a derived
  cache that Navigation rebuilds whenever it no longer matches
  `(Position, Destination)` (section 8).
- **Visible contacts.** `VisibleContacts: AgentId[]` (TASK-026) holds the
  opposing-side agents this agent can currently see, rewritten from scratch
  each tick by the Perception phase (section 12.3). Like `Route`, it is a
  derived cache.
- **Current order.** `Order: ReceivedOrder option` (TASK-028, backlog B-017)
  is the delivered order: command id, intent, issue tick, urgency, risk
  tolerance, and `AsGroup` (whether the originating command addressed more
  than one recipient, section 12.1). `Disposition: OrderDisposition option` is
  the Appraisal phase's outcome (`Accepted | Refused of reasons | Unable of
  reasons`), `None` until the order is appraised. `OrderQueue:
  ReceivedOrder list` (TASK-044, backlog B-051) holds orders stacked behind
  `Order`. It is kept in insertion order, because the queue order is the
  meaningful state; it is the one deliberate exception to the rule that
  collections are sorted before encoding. `PendingDelivery: (ReceivedOrder *
  QueueMode * int64) option` (TASK-058, backlog B-016b) is an order in flight
  with its due tick (section 12.2).
- **Commitment and execution state.** These are not stored. `Commitment`
  (`Holding | Moving | Suppressing | Withdrawing | Assaulting`,
  `Commitment.ofAgent`) is derived from `Order`, `Disposition`, and
  `Destination`, plus `WorldState.TacticalKnowledge` and `SuppressionBand` for
  an assault's stage (section 12.6). It is fully recoverable at every tick (the
  `Route` precedent, section 17), so it needs no memory of its own and is not
  in `Canonical.encode` (TASK-030, backlog B-018).
- **Discipline.** `Discipline: int`, the stage-4 resolve trait, is static
  authored data (`Deployment.Discipline`, default 3). Dynamic discipline and
  trust are not built (backlog B-021 descoped them; they remain unassigned
  future work), and there is no trust field.
- **Communication.** `CommunicationAvailable: bool` (TASK-027), whether an
  order issued this tick reaches the agent, is static authored data
  (`Deployment.CommunicationAvailable`, default `true`; an authored `false` is
  a comms blackout). `RadioDestroyed: bool` is genuine per-tick state: once
  set by Combat it stays `true` for the rest of the run. The effective
  availability, including range and jamming, is `Communication.available`
  (section 12.2).
- **Suppression.** `Suppression: int` on the `0..1000` scale (TASK-032,
  backlog B-020; `docs/05` section 8, "immediate effect of hostile fire and
  impacts") is raised by the Combat phase (12.8) on a qualifying shot and
  decayed every tick by State consequences (12.9). `SuppressionBand: bool`
  (TASK-033, backlog B-021) is a hysteresis latch over `Suppression`, computed
  by the Appraisal phase (12.5). It is genuine state because it also depends
  on which side of the band the agent was already on.
- **Stress.** `Stress: int` on the `0..1000` scale (TASK-033) is raised while
  `VisibleContacts` is non-empty and decayed every tick, both in State
  consequences (12.9). Threat is the one `docs/05` section 8 stress source
  with a system behind it; casualty-, wound-, explosion-, and isolation-driven
  stress are not built. Stress and the suppression band feed the stage-4
  resolve threshold and two reappraisal triggers (12.5).
- **Life and wounds.** `Vitals: VitalStatus` (TASK-045, backlog B-031) is
  `Alive of health` (health on the `0..CasualtyConfig.MaxHealth` scale, 1000),
  `Incapacitated of bleedOutRemaining`, or `Dead`. Reaching zero health moves
  an agent to `Incapacitated`, never straight to `Dead`, and the bleed-out
  countdown (`CasualtyConfig.BleedOutTicks`, 60) runs to `Dead` with no rescue
  mechanic. A single field makes "dead with remaining health" unconstructable.
  `RecentlyWounded: bool` is a one-shot flag, set by Combat when a hit reduces
  an `Alive` agent's health and read and cleared by the following tick's
  Appraisal. Combat runs after Appraisal, so a wound taken this tick cannot
  otherwise reach this tick's appraisal.
- **Weapon and ammunition.** `Ammo: AmmoState` (TASK-047, backlog B-030) is
  `Ready of magazine * reserve` or `Reloading of reserve * ticksRemaining`
  (`src/CommandoWar.Sim/Ammo.fs` owns every transition). There is no further
  per-agent weapon model; weapon characteristics are global `CombatConfig`
  constants.
- **Mission.** `Extracted: bool` (TASK-062, backlog B-032) records that the
  agent, if `Friendly`, has ever been `Alive` on an authored extraction cell
  and stays `true` afterwards (section 12.10).
- **Formation.** `FormationOffset: Cell option` is static authored data
  (`Deployment.FormationOffset`, resolved from `Scenario.Formations`),
  `None` for an unformationed agent (section 12.5).

Each field falls into one of three classes. Canonical state, in
`Canonical.encode`, is identity plus per-tick memory that the current positions
cannot reproduce: `Id`, `Side`, `Position`, `Progress`, `StalledTicks`,
`Destination`, `Order`, `OrderQueue`, `Disposition`, `PendingDelivery`,
`RadioDestroyed`, `Suppression`, `SuppressionBand`, `Stress`, `Vitals`,
`RecentlyWounded`, `Ammo`, and `Extracted`. Static authored data (`Discipline`,
`CommunicationAvailable`, `MoveSpeed`, `FormationOffset`) is set once from the
deployment and excluded from the image, like `Terrain` (section 17; the
ADR-0002 amendment). Derived caches (`Route`, `VisibleContacts`) are excluded
on the same argument as `Commitment`: each is a pure function of canonical or
static inputs.

Agents created without an authored value start at rest: no destination, route,
order, or pending delivery; `Progress` 0; `StalledTicks` 0; `Suppression` 0;
`SuppressionBand` `false`; `Stress` 0; `Vitals` `Alive 1000`; `RecentlyWounded`
`false`; `RadioDestroyed` `false`; `Extracted` `false`; `Ammo`
`Ready(30, 90)`. None of these has a scenario-authored override (the
`Progress` precedent). `World.ofScenario` sets the static fields from the
deployment; their defaults are `Discipline` 3, `CommunicationAvailable`
`true`, `MoveSpeed` `Agent.MoveSpeedDefault` (2), and `FormationOffset`
`None`.

## 12. Tick phases

### 12.1 Command intake

- sort commands by command ID after validating issue tick;
- reject malformed, unauthorised, impossible-to-address, or duplicate commands;
- record accepted commands before effects are applied.

`Simulation.commandIntake` (TASK-020, TASK-024, TASK-044) implements this. The
batch is stably sorted by command id in `Simulation.step`. A command body is
`Order of PlayerIntent * QueueMode` or `Cancel of CommandId` (section 13).
Then, in order:

- every command in a group that shares a `CommandId` within this tick's batch
  is rejected (`DuplicateCommandId`), order-independently, and none is
  processed;
- each surviving command is checked whole: `IssueTickOutOfRange` when
  `IssuedAtTick` is outside `[0, currentTick]`, then `EmptyRecipients`, then
  `DuplicateRecipient`, then `TargetOutOfBounds`. The first failure
  short-circuits with one `CommandRejected` and no per-recipient events. The
  bounds check applies to every `Cell`-targeted intent (`MoveTo`, `Hold`,
  `Assault`, `Withdraw`). A `Suppress` target is an `AgentId`; whether it
  names a contact the recipient knows is an appraisal question (12.5, stage
  2), because intake never reads authoritative hostile state (risk R-023). A
  `Cancel` has no target cell;
- each recipient is then checked in ascending `AgentId` order: `UnknownAgent`,
  then `UnauthorisedRecipient` for a `Hostile`-side agent, then, for a
  `Cancel`, `UnknownTargetCommand` when the named command is not the
  recipient's active `Order`, a member of its `OrderQueue`, or its in-flight
  `PendingDelivery`. That last check reads the start-of-tick agent array, so a
  same-tick batch cannot see its own not-yet-applied effects. Otherwise
  `CommandAccepted` is emitted at acceptance.

One accept/reject event is emitted per (command, recipient) pair.

`IssuedAtTick` is distinct from the delivery tick (section 2). It must be
`>= 0` and not after the tick being processed. A stale (long-delayed) command
is still accepted, since following an outdated order is an appraisal decision,
not a validation one. "Unauthorised" is only the friendly/hostile-side
check: there is no issuer identity or commander model.

Intake writes no agent state. An accepted `(command, recipient)` is recorded as
a pending command: a `ReceivedOrder` envelope (command, intent, issue tick,
urgency, risk tolerance, `AsGroup`) with its `QueueMode`, or a cancellation.
The Communication phase (section 12.2), which runs next, drains the list in the
same tick, so no pending command is canonical state and `WorldState` holds no
command history. `AsGroup` is `Recipients.Length > 1`, captured once at intake
(TASK-067, backlog B-067), so appraisal can tell one joint group order from
several coincident solo orders.

### 12.2 Communication

- determine which recipients receive an order this tick;
- voice and radio delay may initially be zero when in range;
- communication failure must be explicit, not silently ignored.

`Simulation.communication` (TASK-027, backlog B-016; TASK-044, TASK-058,
backlog B-016b) processes every pending command from intake in ascending
`(recipient, command)` id order, against the incrementally updated agent
array, so several same-tick commands for one recipient compose in command-id
order. The phase draws nothing from the deterministic stream.

An agent can receive an order when `Communication.available` holds: the static
`AgentState.CommunicationAvailable` is `true` and, only when the world authors
`WorldState.Headquarters` (the opt-in gate for everything below), the agent is
within `CommsConfig.Range` (15) Chebyshev cells of it, is outside the radius of
every currently active `Jammer` (active over `[ActiveFromTick,
ActiveUntilTick]`, both inclusive), and is not `RadioDestroyed`. Without a
`Headquarters`, availability is exactly the static flag and delivery is
same-tick.

- **Recipient unavailable.** Emit `OrderUndelivered` with a `DeliveryFailure`
  reason, chosen in the fixed priority `UnableToCommunicate`, `OutOfRange`,
  `Jammed`, `RadioDestroyed`, and drop the command, whether it was an order or
  a cancel. An `Order` (and any `Destination` it produced) the recipient
  already held is left untouched: an undelivered new order does not cancel an
  order in progress (`docs/05` section 16 "Lost communication").
- **`Order(_, Replace)`.** Write `AgentState.Order` (the whole
  `ReceivedOrder`), reset `Disposition` to `None`, and clear `OrderQueue`. Not
  `Destination`: the Appraisal phase (12.5) writes that on an `Accepted`
  order, the same tick. A zero-delay delivery emits no event beyond the
  `CommandAccepted` from intake.
- **`Order(_, Append)`.** For a recipient with no active `Order`, identical to
  `Replace`. Otherwise append to the tail of `OrderQueue` and emit
  `OrderQueued`; the active `Order` and `Disposition` are untouched.
- **`Cancel` of the active `Order`.** Clear `Order`, `Disposition`, and
  `Destination` (the destination belongs to the cancelled order), then promote
  the queue head into `Order` with `Disposition = None`, so this tick's
  Appraisal judges it, or go idle if the queue is empty. Emit `OrderCancelled`
  with `wasActive = true`.
- **`Cancel` of a queued entry or an in-flight order.** Splice out that one
  entry (or clear `PendingDelivery`), leaving everything else untouched, and
  emit `OrderCancelled` with `wasActive = false`.
- **`Cancel` of a command found in none of these places.** This is a same-tick
  race: an earlier command in the batch already superseded the target. No state
  change and no event; the superseding command's own events explain the trace.
  A cancel is never delayed.

When a `Headquarters` is authored, an order that passes the availability check
is not applied at once. It is stored in `AgentState.PendingDelivery` with a due
tick of the current tick plus `CommsConfig.DeliveryDelayTicks` (3), a flat
delay regardless of distance. A newer pending order for the same recipient, of
either `QueueMode`, overwrites an in-flight one without an event. After the
main loop, a second pass delivers every pending order whose due tick has
arrived, re-checking `Communication.available` at delivery, since the agent may
have moved out of range or into a jammer. On success the order is applied as
`Replace` or `Append` above and `OrderDelivered` is emitted (plus
`OrderQueued` for an append behind an active order). On failure the order is
dropped with `OrderUndelivered` and the current reason.

The Combat phase (12.8) sets `RadioDestroyed`: with a `Headquarters` authored,
a qualifying hit has a `CommsConfig.RadioDestroyChanceOnHit` (150, on the
`0..1000` scale) chance to destroy the target's radio permanently, emitting
`AgentRadioDestroyed`. The agent may stay `Alive` and keep fighting but can no
longer receive orders. A scenario without a `Headquarters` draws no extra
random number for this. Report aging uses the section 12.4 stale and expire
bands; it has no further mechanism.

### 12.3 Perception

- clear current visibility;
- evaluate visible enemy agents and relevant hazards;
- emit observations;
- update current visible-contact state.

`Simulation.perception` (TASK-026, backlog B-015) is the first phase consumer
of the `Sight` module (section 9). For every agent, in ascending id order,
`AgentState.VisibleContacts` is re-derived from scratch: the `Alive`
opposing-side agents within `PerceptionConfig.SightRange` (an integer
**Chebyshev** cell radius, `10`) **and** in `Sight.visible` line of sight over
the immutable terrain. Both sides are swept (`docs/05_COMMAND_AND_AGENT_AI.md`
section 12). A non-`Alive` agent sees nothing and is never seen (TASK-055,
backlog B-062; TASK-078, backlog B-078). A corpse stays in `WorldState.Agents`,
so without this rule it would stay visible at full confidence indefinitely;
excluding it lets an existing contact on it age and expire through the section
12.4 bands.

A `ContactObserved` event is emitted only when a contact *enters* an
observer's visibility (a new sighting), not every tick it stays visible
(section 14), ascending by `(observer, contact)`. `VisibleContacts` is a
non-canonical derived cache (section 17, section 11). The phase observes
agents only: hazards, muzzle flashes, and impact observations are not built.

### 12.4 Tactical knowledge

- merge reports into squad contacts;
- retain last known position, confidence, and observation tick;
- decay or expire stale contacts according to explicit rules.

`Simulation.tacticalKnowledge` (TASK-026) treats every `Friendly` agent as the
squad, so every friendly's observations this tick are immediately in the one
shared `WorldState.TacticalKnowledge` (`docs/05` section 3 "may share contacts
instantly"). Sharing is instantaneous and is not subject to the
communication constraints of section 12.2, which apply to orders only.
`Perception.mergeKnowledge` upserts every contact seen this tick with the
observed cell, `LastSeenTick = tick`, and
`Confidence = PerceptionConfig.ConfidenceFull` (`1000` on the section 4
`0..1000` scale). A contact unseen for
`PerceptionConfig.StaleAfter` (`20`) ticks drops one band
(`ConfidenceBandDrop`, `250`). A contact unseen for
`PerceptionConfig.ExpireAfter` (`60`) ticks is removed, emitting
`ContactExpired`, ascending by contact id. The store is kept ascending by
contact id. It is genuine per-tick canonical state (section 10, section 17).

The Hostile side (TASK-034, backlog B-022, partial) is symmetric. The same
phase calls the same side-agnostic `Perception.mergeKnowledge` a second time,
filtered to `Side = Hostile`, folding into `WorldState.HostileTacticalKnowledge`.
`ContactExpired` has the same shape for either store; which picture an expiry
came from is told by the contact's own `Side`. No Hostile agent reads this
store. `Simulation.combat` draws a shooter's candidates from its own same-tick
`AgentState.VisibleContacts` only, which is strictly tighter than anything the
stale-tolerant store could provide, so a Hostile agent never fires on an
unobserved position (risk R-023). Enemy doctrine reacting to this picture
(suppress likely routes, seek cover, scripted fallback) is still open on B-022
and unassigned. Per-agent private beliefs are `docs/05` section 17.

### 12.5 Appraisal

- appraise newly received orders;
- reappraise only on material triggers, not every tick without need;
- emit outcome and structured reasons.

`Simulation.appraisal` (TASK-028, backlog B-017) consumes the `Appraisal` leaf
module (`src/CommandoWar.Sim/Appraisal.fs`: pure, integer-only, no event
emission, no PRNG draw). For every agent, in ascending id order, it judges an
agent whose `Order` is set and `Disposition` is `None`: a fresh order, one a
superseding order reset, or one a reappraisal trigger below reset. An
already-appraised order is not re-judged and emits nothing. Stages (`docs/05`
section 5):

- **stage 1** (comprehension / authority) is guaranteed upstream: command
  intake rejects a `Hostile` recipient and an out-of-bounds target, and the
  Communication phase only writes `Order` for a recipient it reached. It has no
  code and no `DecisionReason`;
- **stage 2** (feasibility) first rejects a non-`Alive` agent for any intent:
  `Unable` with `DecisionReason.CriticallyWounded` (TASK-045, backlog B-031;
  `docs/05` section 16, "a critically wounded agent reports unable rather than
  refused"). Then, for `MoveTo`, `Hold`, `Assault`, and `Withdraw`, it is
  `Pathfinding.findWithin` to the resolved target; no route gives `Unable` with
  `NoKnownRoute`. A `Suppress` order is checked on this stage alone: is the
  named contact in `WorldState.TacticalKnowledge`, with `Unable(TargetNotKnown)`
  otherwise, never reading authoritative hostile state (risk R-023). `Suppress`
  and `Assault`, the two intents that plan to initiate fire, first give
  `Unable(InsufficientAmmunition)` when the agent's `Ammo` is entirely empty
  (`Ready(0, 0)`, TASK-047, backlog B-030); that check precedes the target
  check for `Suppress` and the route search for `Assault`. A partial or
  mid-reload state still appraises `Accepted`, and `MoveTo`, `Hold`, and
  `Withdraw` never check ammunition, since an unarmed agent can still walk,
  hold, or retreat;
- **stage 3** (tactical viability) is route exposure to the *known* threats in
  `WorldState.TacticalKnowledge` only (never authoritative hostile positions):
  a sum, over the stage-2 route cells, of per-threat pressure. A threat
  contributes pressure to a cell only where the cell is within
  `AppraisalConfig.ThreatEngagementRange` (8) Chebyshev cells of the threat's
  last-known cell and in `Sight.visible` line of sight from it. The pressure is
  `ExposedCellWeight` (10) reduced by `CoverMitigationPerLevel` (4) per
  `Terrain.cover` level on the edge the fire arrives from, floored at 0, so
  cover level 3 negates a cell. The arrival edge is the dominant axis of
  `threatCell - cell`, with the X axis winning a tie. A threat whose own
  `AgentState.SuppressionBand` is latched contributes nothing, whatever raised
  it (an ordered `Suppress` or incidental automatic engagement, section 12.8).
  The single highest-contributing threat, ties by ascending id, is the
  `RouteTooExposed` reason's threat (`None` only if no known threat
  contributes any pressure, which a refusal cannot have);
- **stage 4** (resolve) compares that exposure to a bounded integer threshold.
  The threshold starts at `BaseResolve` (20), adds `DisciplineResolveWeight`
  (15) per point of `AgentState.Discipline`, and adds the order's risk term
  (`Cautious` -15, `Standard` 0, `Aggressive` +20) and urgency term
  (`Immediate` +20, `Routine` 0). It subtracts `AgentState.Stress /
  StressDivisor` (25, a continuous drag), `SuppressionBandPenalty` (30) while
  `AgentState.SuppressionBand` is latched, and a wound drag of
  `(1000 - health) / WoundDivisor` (25) while `Alive` but wounded. The result
  is floored at 0, so a fully unexposed route is always
  `Accepted` whatever the agent's stress, suppression, or wounds. An `Assault`
  order subtracts `AssaultResolvePenalty` (30) and a `Withdraw` order adds
  `WithdrawResolveBonus` (30), re-floored at 0 (`docs/05` section 4). Stress,
  `SuppressionBand`, and `Vitals` are read at the top of the tick, before this
  tick's Combat and State consequences change them. `<=` gives `Accepted`;
  over gives `Refused` with `DecisionReason.RouteTooExposed`;
- **stage 5** (safer adaptation) remains deferred (a TASK-030 follow-up, no
  B-item number assigned yet): there is no `Adapted` outcome and no route
  recomputation. It needs a second, separate exposure-aware route-search
  algorithm distinct from `Pathfinding.findWithin`'s shortest-path search.

`Refused` and `Unable` carry a primary `DecisionReason` by construction. The
reasons built are `NoKnownRoute`, `RouteTooExposed of threat: AgentId option`,
`TargetNotKnown`, `CriticallyWounded`, and `InsufficientAmmunition`; the rest of
the `docs/05` section 7 vocabulary has no producing system yet.

An order's stage-2 target depends on its intent. `MoveTo` resolves through
`Appraisal.resolveFormationTarget` (below). `Hold` resolves to
`Appraisal.bestCoverNear`: among the passable, in-bounds cells within
`AppraisalConfig.HoldCoverSearchRadius` (2) Chebyshev cells of the authored
area (including the area itself), the one with the lowest total threat
pressure, ties broken by nearest to the area and then ascending `(Y, X)`; with
no known threat nearby, the area itself. `Assault` and `Withdraw` use the
literal target cell.

On `Accepted` the phase writes `AgentState.Destination` (clearing any left by a
superseded order), which the Navigation phase then follows, the same tick: the
resolved target for `MoveTo`, the `bestCoverNear` cell for `Hold`, the literal
target for `Assault` and `Withdraw`. A `Suppress` order writes none, since the
agent holds position. On `Refused` and `Unable` the agent is left with no
destination. Every appraisal emits one `OrderAppraised` event (section 14),
including the mundane `Accepted`.

Reappraisal triggers. A new order is received (Communication resets
`Disposition`). In addition, the phase resets an already-appraised,
non-fulfilled order's `Disposition` to `None`, so it is re-judged the same
tick, on any of four material triggers (TASK-033, backlog B-021; TASK-037;
TASK-045):

- knowledge-change: any `ContactObserved` or `ContactExpired` event emitted
  earlier this tick, global rather than per-route;
- suppression-band: the agent's own `AgentState.SuppressionBand` flips this
  tick. The band is a hysteresis latch over `AgentState.Suppression`, computed
  here against last tick's finalised value: it turns `true` at or above
  `AppraisalConfig.SuppressionBandEnter` (500) and back to `false` at or below
  `SuppressionBandExit` (300);
- threat-suppression-change (TASK-037): the identical band check, but across
  every agent rather than only the appraising one. When any agent's band flips,
  every already-appraised, non-fulfilled order of every agent is re-judged, not
  only an order that names the agent whose band flipped (`docs/05` section 14).
  The motivating case is a threat suppressed by a `Suppress` order, which lets
  another agent's `Refused` order be re-judged and `Accepted` (`docs/07`
  section 8 step 6). Every agent's new band is computed before any order is
  judged, so a later-id threat's flip is known to an earlier-id agent;
- wounded: `AgentState.RecentlyWounded` is set. The flag is consumed here,
  cleared on every tick this phase processes the agent, whether or not it
  triggered a reappraisal.

All of these exclude a fulfilled order (`Disposition = Some Accepted,
Destination = None, Position` at the order's resolved target). `Suppress`
orders are never fulfilled. Resetting a fulfilled order would re-run the
`fromCell = target` short-circuit and defeat `commitmentAndLocalAction`'s
completion clear. The exposure-band, support, and leadership triggers and
dynamic trust remain unbuilt (explicitly descoped, `docs/11` B-021's row; they
are unassigned future work), and "the route becomes blocked" has no producer
for an `Accepted` order under static terrain. See `docs/05` section 14.

Formation slots (TASK-059, backlog B-011d; TASK-067, backlog B-067). A
`MoveTo` order's stage-2 target is resolved through
`Appraisal.resolveFormationTarget` before the route search runs, but only when
`ReceivedOrder.AsGroup` is `true`, that is, the originating `PlayerCommand`
addressed more than one recipient (`Command.moveToMany` with two or more
recipients). A single-recipient `MoveTo` always resolves to the literal ordered
cell, whatever the recipient's own `FormationOffset`: formation membership (a
static, order-independent scenario fact) is not the same as an order being a
coordinated group move, and a solo order goes exactly where ordered. For a
group order to an unformationed agent (`AgentState.FormationOffset = None`) the
literal cell is also used. For a formationed agent the literal cell is treated
as the formation's anchor, and the agent aims for the anchor plus its own
authored slot offset. If that cell is blocked or occupied by another agent, the
target is redirected to the nearest passable, in-bounds cell not occupied by
another agent within `AppraisalConfig.FormationSlotSearchRadius` (2) Chebyshev
cells, ties broken by nearest to the exact offset cell and then ascending
`(Y, X)` (the `bestCoverNear` precedent). If nothing in radius qualifies, the
literal anchor is used, so crowding or terrain at a slot alone never fails an
order. `Hold`, `Assault`, `Withdraw`, and `Suppress` targets are unaffected
regardless of formation membership. `AgentState.FormationOffset` is static
authored data and stays out of the canonical image, the `Discipline` and
`MoveSpeed` precedent.

### 12.6 Commitment and local action

- accepted orders create or update a commitment;
- the executor chooses the next finite action within that commitment;
- a small ordered interrupt table may supersede the normal action.

`Simulation.commitmentAndLocalAction` (TASK-030, backlog B-018) is its own
phase in `Phases.order` between Appraisal and Navigation. `Commitment`
(`Commitment.fs`) is a pure derived value, not a stored commitment store, since
`Order`, `Disposition`, and `Destination` already carry every bit of memory a
commitment needs. `Commitment.ofAgent` derives one of:

- `Holding`: no order, a `Refused` or `Unable` order, or an `Accepted` order
  already fulfilled. A `Hold` order needs no case of its own: it produces
  `Moving` en route and `Holding` on arrival, and nothing distinguishes an
  ordered hold from an idle agent once arrived;
- `Moving of MoveCommitment`: an `Accepted` `MoveTo` or `Hold` order with a
  destination outstanding;
- `Suppressing of SuppressCommitment` (TASK-037): an `Accepted` `Suppress`
  order, which never has a destination. Its executor has no complete state: it
  holds position indefinitely and ends only by supersession. In the Combat
  phase (12.8) it pins its named contact as the shooter's sole candidate
  rather than the nearest `VisibleContacts` entry, `Combat.chooseTarget`
  itself unchanged;
- `Withdrawing of WithdrawCommitment` (TASK-047, backlog B-030): an `Accepted`
  `Withdraw` order with a destination outstanding;
- `Assaulting of AssaultCommitment` (TASK-047): an `Accepted` `Assault` order,
  carrying an `AssaultStage` derived afresh each tick from position, target,
  `WorldState.TacticalKnowledge`, and `SuppressionBand`, with no stored
  counter. `ApproachingStart`: beyond `AppraisalConfig.AssaultStartRange` (3)
  Chebyshev cells of the target. `AwaitingSupport`: within that range, and a
  known threat contact within `ThreatEngagementRange` (8) of the target is not
  `SuppressionBand`-latched. `Advancing`: within that range with no such
  blocking threat. `ClearingThreat`: at the target, with a known unsuppressed
  threat contact within `AssaultClearRadius` (2) of the target.

The finite executor, run per agent holding an `Accepted` order, is
correspondingly thin:

- establish: the order was freshly `Accepted` this tick (an `OrderAppraised`
  with `Accepted` already emitted this tick) -> emit `CommitmentEstablished`
  carrying the order's target cell (for `Hold`, the `bestCoverNear` cell), or
  for `Suppress` the named contact's last-known cell (the agent's own position
  if the contact expired the same tick);
- continue: an unchanged commitment, no event;
- complete: a `MoveTo`, `Hold`, or `Withdraw` order whose agent stands at its
  target cell (for `Hold`, the `bestCoverNear` cell) with `Destination = None`,
  or an `Assault` order at its target with `Destination = None` and no known
  unsuppressed threat contact within `AssaultClearRadius`. The phase clears
  `Order` and `Disposition`, or promotes the head of `OrderQueue` into `Order`
  with `Disposition = None`, and emits `CommitmentCompleted`. The promoted
  order is judged by the next tick's Appraisal, since Appraisal has already run
  this tick, so a chained waypoint's next leg begins one tick later. For a
  group `MoveTo` (`ReceivedOrder.AsGroup`) the target cell is the agent's own
  formation slot, resolved by `Appraisal.resolveFormationTarget` over the other
  agents' current positions, the same expression Appraisal's fulfilled check
  uses; a solo `MoveTo` compares with the literal target;
- for `Assault` not yet fulfilled, the executor drives the `Destination`
  freeze through the ordinary Navigation pipeline: `AwaitingSupport` clears it,
  so Navigation does not move the agent, and `ApproachingStart` and
  `Advancing` re-assert it. There is no timeout: if the player never
  suppresses the blocking threat the assault stalls indefinitely, a deliberate,
  legible outcome in place of a stored wait counter. Crossing the danger area
  needs no special handling, since the automatic, symmetric Combat phase
  engages any visible hostile along the way.

Of the `docs/05` section 11 seven-priority interrupt table, only priority 6
("new higher-priority command") has a live signal: a superseding order's fresh
`CommitmentEstablished` is the complete trace, with no separate event
reporting the superseded commitment's end (nothing is lost, since `Commitment`
is derived, not stored). No other priority produces an interrupt in this phase.
A non-`Alive` agent is kept from starting new actions by Appraisal, Combat, and
Navigation (section 20), not by an interrupt event. Priority 5 ("route
invalidated") has a stall counter, `AgentState.StalledTicks`, but it feeds
Navigation's abandonment of the order (12.7), not an interrupt here.

### 12.7 Navigation and movement

- compute or repair path;
- resolve reservations in stable order;
- advance movement;
- emit movement and blockage events where useful.

`Simulation.navigationAndMovement` (TASK-015, TASK-017, TASK-018, TASK-022,
TASK-049, TASK-065, TASK-070) computes or repairs a `Pathfinding` path per
agent with a destination. A non-`Alive` agent never moves (section 20). The
phase resolves same-tick contention over a shared next cell in stable order
(fewest remaining route steps among agents that would complete the edge this
tick, ties broken by ascending agent id), then resolves the vacation chain so
no mover enters a cell a stationary agent still holds (swaps and rotation
cycles blocked, follow chains advanced only when they clear). A non-`Alive`
agent's cell does not count as occupied, since a corpse never vacates
(TASK-066, backlog B-066). It advances `AgentState.Progress` by
`Terrain.BaseMoveCost` per tick toward the next cell's `Terrain.moveCost`
threshold and enters the cell once reached; the completion comparison is scaled
per agent by `AgentState.MoveSpeed` against `Agent.MoveSpeedDefault` (2)
(section 8). The events are `MovementStepped`, `MovementCompleted`,
`MovementBlocked`, `MovementYielded`, `MovementObstructed`, `MovementRerouted`
(a detour around a parked blocker), and `MovementAbandoned` (an order given up
after `Simulation.StallAbandonTicks` (40) consecutive freezes, tracked by
`AgentState.StalledTicks`). Reservation and vacation-chain resolution are both
resolved fresh every tick, not stored. `Progress` and `StalledTicks` are
genuine per-tick canonical state; `Route` is a non-canonical derived cache.
Full detail, including the detour, stall, and abandonment rules, is in
section 8.

### 12.8 Combat

- validate line of fire and ammunition;
- resolve weapon readiness;
- obtain deterministic spread or hit draw;
- apply cover and impact;
- create suppression independent of a hit where intended;
- emit shot, impact, wound, and suppression events.

`Simulation.combat` (TASK-031, backlog B-019) is its own phase in
`Phases.order`, between Navigation and State consequences. Agents are
processed in ascending id order, both sides, symmetrically. An agent that is
not `Alive` (`Casualty.isAlive`) never shoots and is never a candidate (section
20: dead or incapacitated agents do not start new actions, and are not shot at
again). Liveness is read from the agents as they stood on entry to the phase,
so an agent incapacitated by an earlier shooter this tick still fires, and can
still be targeted, until the next tick.

**Eligibility and target choice.** A shooter engages only if `Ammo.canFire
shooter.Ammo` holds (a `Ready` state with at least one round in the magazine);
an unarmed or mid-reload agent does not engage, with no event (TASK-047,
backlog B-030). Candidates are the shooter's own same-tick
`AgentState.VisibleContacts` resolved to full `AgentState`, never a scan of
every agent (risk R-023, the "same observation contract"). `Combat.chooseTarget`
picks the nearest candidate within `CombatConfig.WeaponRange` (7 Chebyshev
cells) that is in current `Sight.visible` line of fire, ties by ascending
`AgentId`. Line of fire is re-verified against each candidate's current cell,
because a candidate may have moved since Perception observed it. If no
candidate qualifies there is no shot and no event.

**Hit resolution.** `Combat.hitChance` gives a chance on the `0..1000` scale:
`CombatConfig.BaseHitChance` (700), minus `RangePenaltyPerCell` (40) per
Chebyshev cell of range, minus `CoverMitigationPerLevel` (150) per
`Terrain.cover` level on the edge the shot arrives from, clamped to
`MinHitChance` (50) .. `MaxHitChance` (950). The edge is the `Appraisal` stage-3
geometry (`Appraisal.attackDirection`, which `Combat` reuses). One
`RandomStream.next` draw decides the shot: it hits when `draw % 1000` is below
the chance. `ShotFired(shooter, target, hit)` is emitted for every qualifying
shot. Combat is the only gameplay consumer of `WorldState.Random` (section 5). Ammunition is a firing gate applied by the
phase, not a term in the hit-chance formula: `Combat.hitChance` and
`Combat.chooseTarget` do not read it. A qualifying shot consumes one round
(`Ammo.fire`), whatever the shooter's commitment.

`AgentState.Ammo: AmmoState` (`Ready of magazine * reserve | Reloading of
reserve * ticksRemaining`, `src/CommandoWar.Sim/Ammo.fs`) is canonical state.
Reload and resupply are State consequences (12.9) concerns.

**Suppression (TASK-032, backlog B-020).** Every qualifying shot also raises
the target's `AgentState.Suppression` through `Suppression.gain`, independent of
whether it hit: `GainOnHit` (400) for a hit or `GainOnMiss` (150) for a miss,
reduced by `SuppressionConfig.CoverMitigationPerLevel` (100) per cover level on
the same directional edge `Combat.hitChance` uses, floored at 0, and the total
is clamped at `MaxSuppression` (1000). A miss therefore suppresses less than a
hit.
There is no separate event: the rise is a deterministic function of the emitted
`ShotFired`.

**Wounds (TASK-045, backlog B-031).** On a qualifying hit only, not a miss,
`Casualty.wound` is applied to the target's `Vitals`: `Alive health` becomes
`Alive (health - WoundPerHit)` with `WoundPerHit` = 350, or goes straight to
`Incapacitated BleedOutTicks` (60) when health would reach zero or below. A hit
never moves an agent directly to `Dead`. The wound amount is not mitigated by
cover a second time, since cover already gated whether the hit connected. A hit
also sets the target's `RecentlyWounded` to `true`, the "agent is wounded"
reappraisal trigger, which the next tick's Appraisal phase reads and clears. The
tick health first reaches zero emits `AgentIncapacitated(agent, at)`, once, not
every tick the agent stays down.

**Radio destruction (TASK-058, backlog B-016b).** Only when the world authors a
`Headquarters`, a hit on a target whose radio is not already destroyed makes an
independent `RandomStream` draw against `CommsConfig.RadioDestroyChanceOnHit`
(150 on the same `0..1000` scale; it succeeds when `draw % 1000` is below it).
On success `AgentState.RadioDestroyed` becomes `true` (sticky, never re-rolled)
and `AgentRadioDestroyed(agent, at)` is emitted. A world without a
`Headquarters` draws nothing extra.

"Apply cover and impact" is thus covered by directional-cover mitigation of
hit chance and suppression gain, and the wound applied on a hit. There is no
projectile or separate impact event: `ShotFired.hit` carries the outcome, and a
wound that does not incapacitate emits no event of its own (health is canonical
agent state).

**Observed-only targeting (TASK-034, backlog B-022 partial; `docs/09`
section 2.3).** The enemy does not target an unobserved player position.
Candidates come only from the shooter's own same-tick
`AgentState.VisibleContacts`, which is strictly tighter than anything
`WorldState.HostileTacticalKnowledge`'s stale-tolerant memory could provide. A
Hostile shooter with a stale tactical-knowledge entry for a friendly it can no
longer see never fires on it. A `SimulationTests` fact pins this.

**`Suppress` executor (TASK-037, backlog B-030).** A shooter with a
`Suppressing` commitment (12.6) narrows its candidate set from every
`VisibleContacts` entry to just its one named contact before the same
nearest-candidate `Combat.chooseTarget` call runs. It either fires on that
contact (still gated by this tick's actual range and line of fire) or not at
all this tick, and never silently retargets onto something else that wanders
into view. Every other commitment (`Holding`, `Moving`, `Withdrawing`,
`Assaulting`) engages ordinarily; "cross danger area" and "clear immediate
threat" rely on this unmodified automatic engagement rather than special
targeting logic. The target's `Suppression` still rises through the same
`ShotFired` -> `Suppression.gain` path.

### 12.9 State consequences

- update suppression decay;
- update stress from recent events;
- apply deaths and incapacitation;
- update command succession only if included in the current milestone.

`Simulation.stateConsequences` is its own phase in `Phases.order`, after
Combat. It processes each agent in ascending id order (suppression, stress,
bleed-out and ammunition), then derives the squad-wide leadership and failure
facts.

**Suppression decay (TASK-032, backlog B-020).** `AgentState.Suppression`
drops by `SuppressionConfig.DecayPerTick` (50), floored at `0`, unconditionally
every tick (`Suppression.decay`), including the tick a hit raised it: that
tick's gain and this decay both apply, in that order. Silent: no event, the
`Progress` precedent.

**Stress (TASK-033, backlog B-021), partially.** Of `docs/05` section 8's five
stress sources (casualties, wounds, isolation, explosions, threat), only
"threat" has a system behind it, `AgentState.VisibleContacts` (TASK-026).
`AgentState.Stress` rises by `StressConfig.GainPerTick` (80) when this tick's
`VisibleContacts` is non-empty (`Stress.gain`), then always decays by
`StressConfig.DecayPerTick` (30), floored at `0` (`Stress.decay`): gain then
decay, the `Suppression` precedent, with both steps in this phase since nothing
else produces stress. The raised value is clamped at `StressConfig.MaxStress`
(1000) before the decay. Silent: no event.

**Deaths, incapacitation and succession (TASK-045, backlog B-031; see
`Casualty.fs`).**

- Every `Incapacitated` agent's bleed-out countdown ticks down
  (`Casualty.tickBleedOut`): `Incapacitated n` becomes `Incapacitated (n - 1)`,
  and `Dead` once `n` reaches 1, so `Incapacitated 0` is never a persisted
  value. Reaching `Dead` emits `AgentDied(agent, at)`. There is no rescue
  mechanic; the countdown always runs to `Dead`.
- Squad leadership is a pure derived rule, not stored: the lowest-`AgentId`
  `Alive` `Friendly` agent (`Casualty.currentLeader`), or none if every
  friendly is down. Once the lowest id is no longer `Alive`, the next lowest
  survivor is the leader the next time it is computed. The phase computes it
  once from the agents array after its own bleed-out mutations and compares it
  with the start-of-tick leader (`StepState.InitialLeader`, captured once in
  `Simulation.step` before any phase runs, because Combat runs earlier and may
  already have incapacitated the leader). A difference emits
  `LeadershipTransferred(previous, current)`; `None` on either side covers "no
  leader".
- `SquadFailure` is emitted exactly once, the tick on which a Friendly agent
  was `Alive` at the start of the tick (`StepState.InitialFriendlyAlive`) and
  none is after this phase. Health never regenerates, so this is a one-way
  latch. In this phase it is a signal event only: it does not halt
  `Simulation.step`, reject further commands, or write any `WorldState` field.
  The Mission phase (12.10) consumes the same condition into an outcome. It is
  scoped to Friendly only (section 10: every Friendly agent is the one squad;
  Hostile has no equivalent mission concept).

**Ammunition reload and resupply (TASK-047, backlog B-030).** For every agent,
first an authored `WorldState.ResupplyAreas` cell check: an agent standing on
one is refilled to a full magazine and reserve (`Ammo.resupply`; `AmmoConfig`
magazine 30, reserve 90) and emits `AgentResupplied`, short-circuiting any
in-progress reload, unless it is already full (no event for an already-full
agent, the `Suppression`/`Stress` silent-decay precedent for avoiding
every-tick no-op events). Otherwise `Ammo.tick`: an empty magazine with reserve
remaining starts a reload (`ReloadStarted`, `AmmoConfig.ReloadTicks` = 30), and
a `Reloading` state counts down and refills on completion
(`ReloadCompleted`), both emitted once, on the transition, the
`AgentIncapacitated`/`AgentDied` precedent. `WorldState.ResupplyAreas: Cell[]`
is static authored scenario data (`Scenario.ResupplyAreas`), excluded from
`Canonical.encode`, the `Terrain`/`CommunicationAvailable` precedent.

### 12.10 Mission

The Mission phase (TASK-062, backlog B-032) runs after State consequences,
against this tick's final `Agents`. It is a no-op once
`WorldState.MissionOutcome` is not `InProgress`: a one-way transition, the
`Vitals.Dead`/`SquadFailure` "cannot become false again" precedent generalised
to the whole mission.

- **evaluate objective conditions**: every Friendly agent's sticky
  `AgentState.Extracted` flag is updated first (`Alive` on an authored
  `WorldState.ExtractionAreas` cell, emitting `AgentExtracted` once, on the
  transition); then the scenario's `Objectives` algebra is resolved
  recursively, bottom-up (`AllOf`'s own completion depends on its parts'):
  `ReachArea` completes the instant a qualifying agent occupies its area;
  `HoldArea`/`DestroyTarget` advance a per-objective consecutive-tick
  occupancy counter (`WorldState.ObjectiveProgress`, the `Suppression`/
  `Stress` decay shape: reset to 0, not sticky, on a tick with no
  qualifying occupant) until it reaches the authored tick count;
  `ExtractAgents` completes once every required, still-Alive agent's own
  `Extracted` flag is `true` (a `Dead`/`Incapacitated` agent is excluded
  from the requirement, not a blocker) **and at least one required agent has
  `Extracted` set** (TASK-079: with every required agent down the
  requirement is empty, and an empty requirement never completes, the same
  rule as an empty `Objectives` array); `Optional` tracks/completes exactly
  like its wrapped objective, using the same id, but never gates success;
- **emit completion or failure once**: a newly-satisfied objective's id is
  added to `WorldState.CompletedObjectives` and `ObjectiveCompleted` is
  emitted, ascending by `ObjectiveId`. Failure is checked first: every
  Friendly agent non-`Alive` and `ScenarioRules.
  FailOnFriendlyForceEliminated` (re-derived from `WorldState.Agents`, the
  same computation `StateConsequences`'s own signal-only `SquadFailure`
  event already makes) sets `MissionOutcome = Failed` and emits
  `MissionFailed`. Otherwise, when every non-`Optional` top-level `Objectives`
  entry has its id in `CompletedObjectives` (vacuously false for an empty
  `Objectives` array: no win condition authored means never won), it sets
  `MissionOutcome = Succeeded` and emits `MissionSucceeded`;
- **prevent accidental repeated completion events**: `CompletedObjectives`
  is sticky (an id, once added, is never removed) and `MissionOutcome` is a
  one-way latch, so every event above fires at most once per run.

`WorldState.Objectives`/`.ObjectiveAreas`/`.ExtractionAreas`/
`.StaticTargets`/`.Rules` are static authored scenario data (the
`ResupplyAreas`/`Headquarters`/`Jammers` precedent), excluded from
`Canonical.encode`. `AgentState.Extracted`, `WorldState.MissionOutcome`,
`.CompletedObjectives`, and `.ObjectiveProgress` are genuine per-tick
canonical state.

### 12.11 Output

- finalise ordered events;
- create a render snapshot;
- compute state hash at configured checkpoints or every tick during development.

The Output phase builds the render snapshot from authoritative state.
`Simulation.step` computes `StepResult.StateHash` for every tick, strictly
after the phase loop, as a read-only checkpoint that no authoritative output
depends on.

## 13. Commands

Initial commands, as specified:

```fsharp
type PlayerIntent =
    | MoveTo of target: Cell * posture: MovementPosture
    | Hold of area: AreaId
    | Suppress of target: TargetArea
    | Assault of target: TargetArea * approach: Approach option
    | WithdrawTo of target: Cell
```

The implemented type (`src/CommandoWar.Sim/Domain.fs`) is leaner:

```fsharp
type PlayerIntent =
    | MoveTo of target: Cell
    | Suppress of target: AgentId
    | Hold of area: Cell
    | Assault of target: Cell
    | Withdraw of target: Cell
```

`MoveTo` has no posture because no movement-posture model exists. `Suppress`
(TASK-037, a thin slice of backlog B-030) names a specific known contact by
`AgentId`, not a `TargetArea`: it never names an area or a "suspected"
(id-less) target, since no suspected-threat model exists. `Hold`, `Assault` and
`Withdraw` (TASK-047, backlog B-030) take a bare `Cell`, the `MoveTo`
precedent, since no `AreaId`/`TargetArea`/`Approach` model exists either
(`AGENTS.md` "no speculative type machinery"). `Command.moveTo`,
`.suppress`, `.hold`, `.assault` and `.withdraw` build single-recipient
commands with the default envelope. `docs/05` section 4 documents each order's
actual behaviour.

An envelope supplies command ID, issuer, recipients, issue tick, urgency, and risk tolerance.

A command can be syntactically valid but tactically refused by an agent. Command validation and agent appraisal are separate concepts.

**Envelope (TASK-020, extended by TASK-024 and TASK-044).** `PlayerCommand`
(`src/CommandoWar.Sim/Commands.fs`) carries:

- `Id: CommandId`;
- `Recipients: AgentId list`; `Command.moveTo` builds a one-element list and
  `Command.moveToMany` a multi-recipient one;
- `IssuedAtTick`, the tick the order was issued, distinct from the delivery
  tick and range-checked at command intake (section 2 / section 12.1);
- `Urgency` (`Routine | Immediate`) and `RiskTolerance` (`Cautious | Standard
  | Aggressive`), which the Appraisal phase's stage-4 resolve threshold reads
  (TASK-028; `AppraisalConfig`) and which are carried onto `AgentState.Order`;
- `Body: PlayerCommandBody`, either `Order of intent: PlayerIntent * mode:
  QueueMode` (`Replace | Append`) or `Cancel of target: CommandId` (TASK-044,
  backlog B-051). `Append` (`Command.queued`) stacks the order behind the
  recipient's active order in `AgentState.OrderQueue`; `Cancel`
  (`Command.cancel`) withdraws a specific queued or active order by its own
  `CommandId`. `Cancel` is consumed inside command intake and Communication and
  never becomes a stored `AgentState.Order` or `OrderQueue` entry, so
  `PlayerIntent` and `ReceivedOrder.Intent` never represent it.

`IssuedAtTick` is stored on the order as provenance; staleness of a
long-delayed order is not appraised, and no backlog item owns it. `PlayerIntent`,
`Urgency`, `RiskTolerance` and `QueueMode` live in `Domain.fs` so that
`AgentState.Order` and `AgentState.PendingDelivery` can reference them.
Issuer identity is not part of `PlayerCommand` (not modelled: one player);
`RecordedCommand.Issuer` (section 16) carries it as non-authoritative
provenance only.

**On-disk form (TASK-025, backlog B-045).** The full accepted envelope has a
lossless serialisation, `src/CommandoWar.Sim/ReplaySerialisation.fs`
(replay-command format v1; section 16), a versioned line-based text format
carrying `Id`, the delivery and issue ticks, `Sequence`, `Issuer`, the
`Recipients` list, `Urgency`, `RiskTolerance`, and the body. The legacy
`.cwlog` fixture grammar (frozen at v1) cannot express multi-recipient
addressing, the two enum fields, a distinct `IssuedAtTick`, queueing or
cancellation; `.cwreplay` can.

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

The events defined (`src/CommandoWar.Sim/Events.fs`), by category:

- **Command outcome**: `CommandAccepted`, `CommandRejected` (TASK-020,
  TASK-024).
- **Order delivered or communication failed**: `OrderUndelivered of command *
  recipient * reason` (TASK-027, backlog B-016) is emitted by the
  Communication phase when an order cannot be delivered; the order is dropped.
  The reason is a `DeliveryFailure`: `UnableToCommunicate` (the recipient's
  `CommunicationAvailable` is `false`), `OutOfRange`, `Jammed` or
  `RadioDestroyed` (the last three only when the world authors a
  `Headquarters`, TASK-058, backlog B-016b). When more than one holds, the
  reported reason follows that priority order. A zero-delay delivery has no
  success event: the `CommandAccepted` at intake records it. A delayed delivery
  emits `OrderDelivered of command * recipient` on the tick the order arrives.
  `OrderQueued of command * recipient` (an `Append` order that joined a busy
  recipient's `OrderQueue`) and `OrderCancelled of command * agent * wasActive`
  (TASK-044, backlog B-051) report the queue operations; `Append` onto an idle
  recipient starts at once and emits no `OrderQueued`.
- **Order appraisal outcome**: `OrderAppraised of agent * command *
  disposition` (TASK-028) is emitted by the Appraisal phase for every
  appraisal, including a mundane `Accepted`, and never on a tick where an
  already-appraised order is unchanged.
- **Current execution state** (not itself a listed category, but the nearest
  fit): `CommitmentEstablished of agent * command * target` and
  `CommitmentCompleted of agent * command * at` (TASK-030, backlog B-018),
  emitted by the `commitmentAndLocalAction` phase. The former is emitted
  exactly when an order is freshly `Accepted` this tick (from `Holding` or
  superseding a prior commitment); the latter when an order that ends on
  reaching a target cell (`MoveTo`, `Hold`, `Withdraw`, or `Assault` once no
  unsuppressed threat is known near the target) is fulfilled, at which point
  the head of `AgentState.OrderQueue`, if any, is promoted into `Order`. A
  `Suppress` commitment has no fulfilled state. A superseded commitment emits
  no event of its own: `Commitment` is a derived value (section 17), so there
  is nothing separate to report ending.
- **Movement**: `MovementStepped`, `MovementCompleted`, `MovementBlocked`,
  `MovementYielded`, `MovementObstructed`, `MovementRerouted`,
  `MovementAbandoned` (TASK-015, TASK-017, TASK-022, TASK-065, TASK-070).
- **Contact observed or reported**: `ContactObserved of observer * contact *
  at` and `ContactExpired of contact * lastKnownCell` (TASK-026).
  `ContactObserved` fires on a new sighting only, the tick a contact enters an
  observer's `VisibleContacts`, never per tick while it stays visible.
  `ContactExpired` fires only when a contact is removed from a shared picture
  (the friendly squad's `TacticalKnowledge` or the Hostile side's
  `HostileTacticalKnowledge`) after `PerceptionConfig.ExpireAfter` unseen
  ticks; the event does not name the side, which is the contact's own
  `AgentState.Side`.
- **Shot fired**: `ShotFired of shooter * target * hit` (12.8).
- **Wound, incapacitation, or death**: `AgentIncapacitated`,
  `AgentRadioDestroyed`, `AgentDied`, `LeadershipTransferred`, `SquadFailure`
  (12.8, 12.9).
- **Ammunition**: `ReloadStarted`, `ReloadCompleted`, `AgentResupplied`
  (12.9).
- **Objective state**: `AgentExtracted`, `ObjectiveCompleted`,
  `MissionSucceeded`, `MissionFailed` (12.10).
- Suppression and stress changes emit no event; their values are canonical
  agent state (12.9).

Within a tick, events are ordered as follows, following the `Phases.order`
sequence:

1. command outcomes, ascending command id (`CommandAccepted`,
   `CommandRejected`);
2. communication outcomes, ascending `(recipient, command)`:
   `OrderUndelivered`, `OrderQueued`, `OrderCancelled`. A delayed order's
   `OrderDelivered`, or its `OrderUndelivered` re-checked at delivery, is
   emitted in a second pass after every this-tick command's own outcome,
   ascending recipient id;
3. `ContactObserved`, ascending `(observer, contact)`, then `ContactExpired`,
   ascending contact id within the friendly picture followed by ascending
   contact id within the Hostile picture;
4. `OrderAppraised`, ascending agent id;
5. `CommitmentCompleted` / `CommitmentEstablished`, ascending agent id (at
   most one of the two per agent per tick);
6. movement outcomes, ascending agent id;
7. combat outcomes, ascending shooter id: `ShotFired` then, for a qualifying
   hit, `AgentIncapacitated` if it reaches zero health, then
   `AgentRadioDestroyed` if the same hit's independent radio roll also
   succeeds;
8. state-consequences outcomes: for each agent in ascending id order, at most
   one ammunition event (`ReloadStarted`, `ReloadCompleted` or
   `AgentResupplied`; resupply short-circuits reload) followed by `AgentDied`
   if the agent's bleed-out ended; then at most one `LeadershipTransferred`
   (a squad-wide fact), then at most one `SquadFailure`;
9. mission outcomes: every `AgentExtracted` ascending agent id, then every
   `ObjectiveCompleted` ascending objective id, then at most one of
   `MissionSucceeded` / `MissionFailed` (failure checked first).

So a contact is observed at its start-of-tick position, an order is appraised
against this tick's tactical picture, a commitment begins or ends the same tick
its order is appraised, a delivered-and-accepted order takes effect the same
tick, a shot is resolved against this tick's post-movement positions,
casualty, leadership and squad-failure consequences are resolved after that
shot's own `Suppression` gain and decay, and the mission outcome is resolved
last, against this tick's final `Agents`.

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

**In-memory record (TASK-003).** `src/CommandoWar.Sim/Replay.fs`.
`ReplayRecord` (format version 1) carries the canonical-format version,
provenance metadata, seed, tick-0 initial state, tick count, and a `CommandLog`
(version 1) of `RecordedCommand { Tick; Sequence; Command; Issuer }`, where
`Tick` is the delivery tick, independent of the envelope's
`Command.IssuedAtTick` (section 2; TASK-024). `Replay.run` rejects unsupported
replay, command-log, and canonical-format versions, a non-tick-0 initial state,
a seed inconsistent with the initial stream, a negative tick count,
out-of-range or non-monotonic commands, and a log that reuses one `CommandId`
on two ticks (`DuplicateCommandIdInLog`), each with a typed `ReplayError`.

**On-disk form (TASK-025, backlog B-045).**
`src/CommandoWar.Sim/ReplaySerialisation.fs`, **replay-command file format
version 1**, is a hand-rolled, deterministic, line-based text format
(`FSharp.Core` only, no new package). It serialises the header (its own format
version, seed, tick count, canonical-format version, `ReplayMeta`, an optional
initial-state hash, optional per-tick checkpoint hashes) plus the ordered
`RecordedCommand[]`; it does **not** serialise `WorldState`: the initial state
stays a named scenario / builder reference (`Meta.Scenario`), resolved by the
caller. The command line forms are `move`, `suppress`, `hold`, `assault` and
`withdraw`, their `queue-` prefixed forms (`Order(_, Append)`), and `cancel`.
`parse` rejects an unknown file-format version with a typed `ParseError` and
attempts no migration; a canonical-format mismatch, an out-of-order command or
checkpoint block, and every malformed field are typed errors naming the source
line. `parse` and `serialise` are mutual inverses on valid input. This
file-format version is independent of `Replay.FormatVersion`,
`CommandLog.Version`, and `Canonical.FormatVersion`.

`cwheadless replay-file <path>` parses, replays, prints the per-tick hash table
and the ordered accepted commands, and exits 2 on a parse/validate failure or 3
on a checkpoint divergence. The replay corpus entries are `.cwreplay` files
(TASK-036, backlog B-049), most of them generated from the same authored
`ScenarioSpec` value as the entry's geometry; the legacy `.cwlog` grammar stays
frozen and is used only by `spike-fixture`.

## 17. State hashing

The hash input must be canonical:

- fields in fixed order;
- entities in ascending ID;
- collections sorted explicitly;
- integers encoded with fixed width and endianness;
- presentation state excluded;
- derived caches either excluded or normalised.

A divergence report should identify the first bad tick. Component-level subhashes are desirable once the world grows.

`Canonical.encode` (`src/CommandoWar.Sim/Canonical.fs`, TASK-003) produces a
big-endian fixed-width byte image of `WorldState` only. Events, snapshots, and
phase traces are excluded. Optional values carry an explicit present/absent
tag. The format version, `Canonical.FormatVersion` (currently **15**), is the
first field of the image, so a reader can reject an unknown layout. Any change
to the byte layout bumps it; every bump changes every state hash and re-pins
the corpus, the shared fixture and the self-checks, and a `StateHash` records
the format it was computed over, so two hashes are comparable only within one
format version. The per-bump history is in `docs/12_PROGRESS_LEDGER.md`
("Format version history"). The version is independent of
`ScenarioContent.Version` (section 21) and `Replay.FormatVersion` (section 16).

`Hashing.hash` (`Hashing.fs`) is FNV-1a-64 over that image, exposed through
`IStateHasher`. It is not a cryptographic primitive and claims no collision
resistance or tamper evidence; it exists to detect replay divergence.
`Simulation.step` records `StepResult.StateHash` after the Output phase
without any authoritative output depending on it. `Divergence.compare`
reports the first tick whose hash differs, the expected and actual hash, the
first differing canonical section, and the random draw count on each side; a
run that agrees on every overlapping tick but has a different length is
reported as truncated, not as a match.

### 17.1 Image layout

The image holds, in order:

| Section | Content |
|---|---|
| Header | format version (u32), tick (i64), grid width and height |
| Random | algorithm, algorithm version, 64-bit word, draw count |
| Agents | agent count, then each agent in ascending id order |
| Tactical knowledge | contact count, then each `Contact` of `WorldState.TacticalKnowledge` in ascending contact-id order: contact id, last-known cell x and y, `LastSeenTick`, `Confidence` |
| Hostile tactical knowledge | the same shape and ordering for `WorldState.HostileTacticalKnowledge` |
| Mission | `MissionOutcome`; count, then `CompletedObjectives` ascending by `ObjectiveId`; count, then `ObjectiveProgress` as (objective id, ticks) ascending by `ObjectiveId` |

Each agent is written in this field order: id, side, position, `Progress`,
`StalledTicks`, `Destination` (tagged), `Order` (tagged), `Disposition`
(tagged), `OrderQueue` (count, then each order), `Suppression`,
`SuppressionBand`, `Stress`, `Vitals` (`Alive` health, `Incapacitated`
remaining ticks, or `Dead`), `RecentlyWounded`, `Ammo` (`Ready` magazine and
reserve, or `Reloading` reserve and ticks remaining), `RadioDestroyed`,
`PendingDelivery` (tagged: order, queue mode, due tick), `Extracted`.

A `ReceivedOrder` is written as: command id, intent tag and target (`MoveTo`,
`Suppress`, `Hold`, `Assault`, `Withdraw`), the `AsGroup` flag, `IssuedAtTick`,
urgency, risk tolerance. An `OrderDisposition` is `Accepted`, or `Refused` /
`Unable` followed by the primary `DecisionReason` and a count and list of
supporting reasons. The reasons are `NoKnownRoute`, `RouteTooExposed` (with an
optional threat id), `TargetNotKnown`, `CriticallyWounded` and
`InsufficientAmmunition`. `OrderQueue` is written in list order, not sorted:
the queue's order is itself the authoritative sequencing, the one deliberate
exception to the explicit-sort rule. The tactical-knowledge and mission
collections are sorted explicitly when written, not trusted to be sorted
already.

When two images differ, `Canonical.firstDifferingSection` names the first
differing top-level section (`Tick`, `Bounds`, `Random`, `AgentCount`,
`TacticalKnowledge`, `HostileTacticalKnowledge`, `Mission`), then the first
differing agent as `Agent[N]` by agent id, with `Agents` as the final
fallback. A difference inside an order or disposition therefore surfaces as
`Agent[N]`, not as a section of its own. The label is a diagnostic aid, not an
authoritative output.

### 17.2 What is in the image and what is not

The rule is the ADR-0002 amendment "Static authoritative data and the
canonical image". The image exists to detect divergence between two runs of
the same inputs, so a field is in it exactly when it is per-tick state that no
other canonical field reproduces. Derived caches and static authored data are
excluded: hashing them would move every pinned hash for no determinism
benefit.

In the image, because the state is per-tick memory:

- `Progress`: `Position` does not change while an edge is in progress, so
  nothing else records how long it has been (section 8);
- `StalledTicks`: how long the current movement freeze has persisted;
- `Order` and `Disposition`: an order's `IssuedAtTick` lives only in the
  command envelope, a `Refused` outcome persists with its structured reasons
  for an idle agent, and `AsGroup` records whether the originating command
  addressed more than one recipient, which decides whether a `MoveTo` order
  is redirected to the recipient's formation slot and which no other field
  reproduces;
- `OrderQueue`: orders stacked behind the active order (backlog B-051);
- `Suppression` and `Stress`: both change every tick from gameplay events;
- `SuppressionBand`: a hysteresis latch that depends on which side of the band
  the agent already was, so it cannot be recomputed from `Suppression`;
- `Vitals` and `RecentlyWounded`: `RecentlyWounded` is a one-shot flag consumed
  the following tick, but it affects behaviour (the wounded reappraisal
  trigger) and `Vitals` records only the result of a wound, not when it
  landed, so omitting it would let two states hash identically while diverging
  on whether a reappraisal fires;
- `Ammo`: changes from firing and from State consequences;
- `RadioDestroyed` and `PendingDelivery`: a radio destroyed by a combat hit,
  and an in-flight order's own envelope and due tick;
- `Extracted`;
- both tactical-knowledge stores: `LastSeenTick` and the decaying `Confidence`
  cannot be recomputed from the current tick's positions (section 12.4);
- `MissionOutcome`, `CompletedObjectives` and `ObjectiveProgress`: the Mission
  phase's own one-way outcome, its sticky completed set, and its per-objective
  consecutive-tick occupancy counters (section 12.10).

Excluded as derived caches, each a deterministic, integer-only function of the
run's inputs, so two runs of the same inputs produce identical values and a
defect in producing them still surfaces through a canonical field within a
tick:

- `AgentState.Route` (the path an agent is following, its cursor, and its
  cost): built by the Navigation and movement phase through
  `Pathfinding.findWithin`, which is total and pure. A route-following bug
  surfaces through `Position`;
- `AgentState.VisibleContacts`: rewritten from scratch each tick by the
  Perception phase from positions, vitals (a non-`Alive` observer sees
  nothing and a non-`Alive` agent is never seen), terrain and
  `PerceptionConfig` (section 12.3);
- `Commitment` (`Holding`, `Moving`, `Suppressing`, `Withdrawing`,
  `Assaulting`; section 12.6): derived on demand from `Order`,
  `Disposition` and `Destination` (plus, for the `Assaulting` stage, `Position`,
  the known threat contacts and `SuppressionBand`), all canonical. It is never
  stored in `AgentState`. The Combat phase derives it to pin a `Suppress`
  order's named contact, and `Diagnostics` derives it for the
  `AgentCommitment` overlay.

Excluded as static authored data, set once from the validated scenario and
never mutated during a run, so both runs load identical values at tick 0 and a
behavioural difference surfaces within one tick through canonical state:

- `WorldState.Terrain`, `ResupplyAreas`, `Headquarters`, `Jammers`,
  `Objectives`, `ObjectiveAreas`, `ExtractionAreas`, `StaticTargets` and
  `Rules`;
- `AgentState.CommunicationAvailable` (a recipient's authored comms blackout;
  the per-tick comms state, `RadioDestroyed` and `PendingDelivery`, is in the
  image), `Discipline` (it never mutates; the per-tick pressure state is
  `Stress`), `MoveSpeed` and `FormationOffset`.

A field of the second group enters the image, and `Canonical.FormatVersion`
bumps, the moment any phase mutates it per tick. No phase mutates terrain
today; destructible terrain is not built.

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

The benchmark harness `bench/CommandoWar.Benchmarks/` exists (TASK-014, backlog
B-013), and `content/benchmarks/BASELINE.md` is the recorded baseline (median,
P95, allocation per operation, run environment, commit) with a documented
regeneration command. On the reference machine every per-tick measurement is at
least ~36x inside the 5 ms budget, including a full-grid A* query run once per
tick, and the empty and movement ticks show no allocation proportional to map
size. The **5 ms per-tick** and **no unbounded per-tick allocation** budgets
therefore stand as written. The recorded baseline predates the Perception,
Appraisal and Combat phases (B-015 / B-017 / B-019) and the Bridgehead map
(B-025), which are now built; a tighter per-subsystem number is a follow-up
that needs the harness re-run against them.

The `Pathfinding.find` default expansion cap (`Width * Height`) is a
correctly-sized safety ceiling: `BASELINE.md` records that a single legitimate
query on an adversarial 40x40 map already closes ~73% of the grid. The
Navigation and movement phase passes the same ceiling as each agent's
per-query budget; no tighter per-tick pathfinding budget is set.

## 20. Invariants

At minimum:

- one live agent has one authoritative position, enforced pairwise: the
  Navigation and movement phase never places two live agents on the same cell
  (vacation-chain resolution, TASK-022), and `World.create` / `World.ofScenario`
  reject a cell-sharing construction (`WorldError.AgentsShareCell`);
- dead or incapacitated agents do not start new actions;
- an order references existing recipients at acceptance time;
- a path contains traversable adjacent cells;
- ammunition cannot become negative;
- mission completion is emitted at most once;
- entity IDs are unique;
- state hash is independent of presentation state;
- every refusal and adaptation contains at least one structured reason;
- same replay inputs reproduce recorded checkpoints within the supported boundary;
- every contact in `WorldState.TacticalKnowledge` was observed by a friendly
  on its own `LastSeenTick` and is within `PerceptionConfig.ExpireAfter` ticks
  of that sighting; a surviving contact's `LastSeenTick` never decreases
  (TASK-026; `DeterminismPropertyTests` property 6); the identical invariant
  holds for `WorldState.HostileTacticalKnowledge` against a Hostile agent's
  own observation (TASK-034; the same property 6, extended);
- an accepted order writes `AgentState.Destination` only for a recipient with
  `CommunicationAvailable = true`; an order to a recipient with `false` writes
  no `Destination`, leaves any existing one untouched, and emits exactly one
  `OrderUndelivered` (TASK-027; `DeterminismPropertyTests` property 7). When a
  headquarters is authored, range, jamming and a destroyed radio also gate
  delivery (section 12.2);
- the Appraisal phase writes `AgentState.Destination` for an order iff its
  `Disposition` is `Accepted`; it never returns `Accepted` after a stage-2
  feasibility failure; a `Refused` or `Unable` disposition carries at least one
  structured `DecisionReason` (by construction: the DU cases hold it), and
  an already-appraised, unchanged order is not re-appraised
  (TASK-028; `DeterminismPropertyTests` property 8).

## 21. Authored scenario and content version

An authored scenario is framework-neutral typed data: a scenario id, map
dimensions, friendly and enemy deployments (agent id, side, cell,
communication availability, discipline, a unit-type reference, and an optional
formation slot reference), an objective algebra, objective, extraction, and
resupply areas, static targets, an authored unit-type table, an authored
formation table, an optional headquarters cell, jammers, scenario-wide rules,
and an optional authored terrain layer (TASK-010; elevation, passability and
movement cost, opacity, directional low cover). The model holds data only:
line of sight and pathfinding are queries over the terrain it produces
(sections 8 to 9), and the Perception, Combat and Navigation and movement
phases consume that grid.

`Objectives`, `ObjectiveAreas`, `ExtractionAreas`, `StaticTargets`, and `Rules`
are carried onto `WorldState` by `World.ofScenario` and consumed by the Mission
phase (section 12.10; TASK-062, backlog B-032). `ResupplyAreas` (TASK-047,
backlog B-030) are consumed by `Simulation.stateConsequences`'s ammo-resupply
check (12.9). `Headquarters` and `Jammers` (TASK-058, backlog B-016b) feed the
Communication phase (12.2); a scenario without `Headquarters` opts out of the
range, delay, jamming and radio-destroyed features. `UnitTypes` (TASK-049,
backlog B-058) is a small table of `{ Id; MoveSpeed }`, referenced by each
deployment's `UnitType`; every deployment must reference a defined entry (no
silent default). `Formations` (TASK-059, backlog B-011d) is a table of
`{ Id; Offsets }`, referenced by a deployment's optional
`FormationId` / `SlotIndex`; a blank `FormationId` opts the deployment out of
any formation, and a non-blank one must resolve (no silent default). Only the
baked results survive validation, `Deployment.MoveSpeed` and
`Deployment.FormationOffset` (the offset cell of the referenced slot, `None`
when the deployment has no formation), the `RawTerrainCell.Class` / `Terrain`
precedent; the tables themselves do not.

The authored input is versioned by a content-format version that is independent
of the canonical-state format version (section 17) and the replay container
version (section 16): it versions the authored shape and the validation
contract, not the state encoding. `ScenarioContent.Version` is currently 7;
the per-version history is in `docs/12_PROGRESS_LEDGER.md`. An unsupported
version is a typed error (`UnsupportedContentVersion`), not a guessed
migration: any version other than the current one is rejected, not migrated.

Validation is one pass over the raw input and returns either a validated
scenario or the full list of faults. Each fault names the offending object and,
where relevant, the expected value; no silent default is supplied for invalid
data. It reports: an unsupported content version; a blank scenario id;
non-positive map dimensions; a duplicate, negative, out-of-map, or
cell-sharing deployment, and a negative discipline; a duplicate or blank area
or target id (objective, extraction, and resupply markers share one area id
namespace); an area or target marker outside the map; a duplicate or negative
objective id; an unknown objective class; an objective referencing a missing
area or target; an extraction selecting an unknown agent; a `"destroy"`
objective whose `HoldTicks` is not positive (it is the fixed plant duration);
a missing required marker (no friendly deployment, no objective, no extraction
area); a blank or duplicate unit-type id, a non-positive unit-type move speed,
or a deployment referencing an unknown unit type; a blank or duplicate
formation id, a formation with no slots, a deployment referencing an unknown
formation, or a slot index outside the referenced formation's slot count; a
headquarters or jammer outside the map, a negative jammer radius, or a jammer
window that is negative or runs backwards; and, for the terrain layer, a layer
whose dimensions disagree with the map, an out-of-map terrain cell or cover
feature, a duplicate terrain cell or cover feature, an unknown terrain or cover
class, a negative elevation or cover level, a negative move cost, a passable
cell whose move cost lies outside `[Terrain.BaseMoveCost, Terrain.MaxMoveCost]`
(an impassable cell's cost is ignored), and a deployment on an authored
impassable cell.

An absent terrain layer is legal and means empty terrain (flat, fully
passable, transparent, uncovered).

A validated scenario builds the authoritative world by deploying its agents
(friendly then enemy, ordered ascending by id) through the same construction
path as any other world.

The implementation is `src/CommandoWar.Sim/Scenario.fs` (TASK-008).
`Scenario.validate : RawScenario -> Result<Scenario, ScenarioError list>`
collects every fault in one pass (`ScenarioError`, the `ReplayError` style).
The `Objective` algebra is `ReachArea` / `HoldArea` / `DestroyTarget` /
`ExtractAgents` / `AllOf` / `Optional`; a `RawObjective` expresses the four
leaf kinds with an optional flag, and `AllOf` is not expressible in the raw
form (the mission is the implicit all-of of the list).
`RawScenario.TerrainLayer : RawTerrainLayer option` carries the optional
authored terrain; `Scenario.Terrain : Terrain` is the validated grid
(`Terrain.empty Map` when no layer was authored). `World.ofScenario : Scenario
-> uint64 -> Result<WorldState, WorldError>` builds the world and carries
`scenario.Terrain` onto it through the same construction core as
`World.create`, which owns the empty-grid, duplicate-id, out-of-bounds and
cell-sharing guards. `ScenarioFile` (`ScenarioFile.fs`, TASK-060, backlog
B-024) reads and writes `RawScenario` as the `.cwscenario` text format; it
performs no content validation, has its own format version independent of
`ScenarioContent.Version`, and a parsed scenario still goes through
`Scenario.validate`.

A pinning test builds the six-agent shared fixture as a `Scenario` (no terrain
layer, one `reach` objective on agent 3's destination), runs it through
`World.ofScenario` with seed 20260902 to the pinned initial hash
`0x5049F6F0E9FCA1E2` (format 15, equal to the fixture's own initial hash), and
steps 40 ticks with the fixture command to `0x8C6057A96F267515`. That final
hash differs from the fixture's own because agent 3 reaches the objective area
at tick 31, completing the objective and setting `MissionOutcome` to
`Succeeded`. This ties the model to the existing determinism evidence without
changing the fixture, `Canonical.encode` or `Setup.sixAgentWorld`.
