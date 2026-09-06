# TASK-022: Runtime cell-occupancy correctness in the Navigation and movement phase

Status: review (implemented 2026-09-06, pending Dave's acceptance)
Owner: Dave
Phase: P3
Gate: G3 (corrects a G2 deliverable: navigation and movement, TASK-015 / TASK-017)
Size: M

## Outcome

Implemented 2026-09-06 in a single headless session.
`Simulation.navigationAndMovement` gains a stage-2b vacation-chain resolution
between rival arbitration (2a, TASK-017, unchanged) and Pass 3: an additive
fixpoint over the completing non-yielded movers (`M0`) computes `movers` (may
enter this tick) and `obstructedBy: Map<int, AgentId>` (blocked by a
non-vacating occupant). One new Pass 3 arm freezes an obstructed `Advancing`
agent exactly like a rival-contest loser (`Progress = startProgress`,
`Route` written back, `Position` / `Destination` untouched) and emits the new
`MovementObstructed of agent * at * blocked * occupant` event. Swaps and
n-agent rotation cycles fall out as all-obstructed with no special case; a
3-agent pure cycle is geometrically impossible on the bipartite 4-connected
grid, so the rotation-deadlock test uses four agents around a 2x2 block.
Persistent obstruction is named for B-015 / B-017, not solved.

`World.create` / `World.ofScenario` gained `WorldError.AgentsShareCell`
(the optional base-case guard, taken: the vacation-chain algorithm relies on
pre-tick one-agent-per-cell uniqueness). `Diagnostics` gained
`Overlay.Obstructed of cell * occupant`, an `eventMarker` arm, and a `frameOf`
derivation; `DiagnosticRender` renders it in ASCII (`obstructed (x,y): held by
agent N`) and SVG (red dashed box `B<id>`). Two new corpus entries
(`follow-chain`, `swap-standoff`) and one golden pair
(`content/diagnostics/swap-standoff-tick-001.*`).

`Canonical.FormatVersion` stays 2; no `AgentState` / `WorldState` field added;
no existing fixture / corpus / golden hash moved (`-- corpus` and `-- fixture`
byte-identical before and after, with and without `--regenerate`). Tests
`184 -> 194`. One pre-existing `SimulationTests` fact
(`an agent routes around an impassable wall`) had its wall repositioned: it
used the six-agent world and its detour ran the moving agent straight through
two parked friendly agents — exactly the defect this task fixes — so it could
not pass unmodified. Full evidence in
`docs/ledger/2026-09-06-TASK-022-cell-occupancy.md`.

## Objective

Close a movement-correctness hole in the deterministic core: **the Navigation
and movement phase lets two live agents occupy the same cell.**

`Simulation.navigationAndMovement` resolves same-tick contention only between
agents that would *complete* movement into the *same* next cell on the same
tick (TASK-017, Pass 2 `yieldedTo`). A mover with no rival claimant for its
next cell takes the `Advancing(... ) -> yieldedTo.TryFind idx -> None` branch
in Pass 3 and unconditionally assigns `Position = next`, with **no check that
`next` is already held by a stationary agent** — one that is idle, blocked,
arrived this tick, still mid-edge, or itself a contest loser. `Pathfinding`
plans over `Terrain` passability only and never sees agent positions, so a
route legitimately runs through a cell another agent is standing on.

This task defines and enforces the runtime occupancy policy **before**
perception, cover, and combat build on movement output (ambiguous LoS,
directional cover, suppression, casualty positions, and overlays all assume
one agent per cell):

- a stationary occupied cell blocks entry;
- a two-agent position swap is blocked (no agent may pass through another);
- an n-agent rotation cycle is blocked (no first mover exists);
- a follow chain advances **this tick** only when the whole chain resolves to
  a free cell;
- a destination cell held by an agent that *will* vacate it this tick is
  entered under one deterministic rule (the vacation chain), never guessed.

The fix is a deterministic dependency-resolution stage over the current tick's
movement intents. It is **not** a cooperative or agent-aware pathfinder.

## Why this task exists

Risk R-010 (pathfinding, formation, and local avoidance dominate development —
"deadlocks, oscillation") and R-009 (determinism claimed but breaks through
ordering behaviour). This is a correctness defect in a G2 deliverable
(B-011 / B-011b, TASK-015 / TASK-017), found after the gate passed, by the
standing review.

The TASK-017 ledger already recorded the gap explicitly under "Deviations":
reservation "does not resolve an agent moving onto a cell held by a stationary
agent" and "does not defend against a rotation deadlock", and flagged both for
"whoever designs B-011c or a later multi-agent scenario". No later task picked
them up; B-011c (TASK-018) and B-011d were narrowed away from it. The property
suite (`DeterminismPropertyTests.fs` property 2) only asserts every agent stays
on a *passable* cell after each tick — never that two live agents hold distinct
cells. `docs/04_SIMULATION_SPEC.md` section 20 lists "one live agent has one
authoritative position" but nothing enforces the pairwise form.

This must land before B-015 (perception) and B-019 (combat). It does not depend
on TASK-020 or TASK-021 and can run before or after either.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `src/CommandoWar.Sim/Simulation.fs`, the whole Navigation and movement phase
  (roughly lines 159-374): the phase header comment, `MoveOutcome`, Pass 1
  `intents`, `wouldComplete`, Pass 2 `yieldedTo` / `remaining`, the Pass 3
  apply loop and its five match arms
- `src/CommandoWar.Sim/Domain.fs` (`AgentState`, `MovementPath`, `Agent.create`)
- `src/CommandoWar.Sim/Events.fs` (`EventBody`: `MovementStepped`,
  `MovementCompleted`, `MovementBlocked`, `MovementYielded`; the event-ordering
  doc comment on `DomainEvent`)
- `src/CommandoWar.Sim/Diagnostics.fs` (`Overlay`: `Reserved` and its doc
  comment reserving cases per backlog item; `EventMarker`; `frameOf` vs
  `frame`; `reservationOverlays`)
- `src/CommandoWar.Headless/DiagnosticRender.fs` (the two `Overlay`-exclusion
  filters, the `Ascii` overlay text loop, the `Svg` overlay drawing loop —
  `TreatWarningsAsErrors` forces every match site when a case is added)
- `src/CommandoWar.Headless/Corpus.fs` (`Entry`, `all`, `convergingRoutesWorld`
  and the other world builders, `rawScenario` / `worldOf` / `wall` / `costly`)
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs` ("Multi-agent movement"
  section, `twoAgentWorld`), `DeterminismPropertyTests.fs` (properties 1 and 2,
  `randomCaseGen`, `cellOf`), `CorpusTests.fs`, `DiagnosticsTests.fs`
- `docs/ledger/2026-09-04-TASK-017-cell-reservation.md` (the three-pass design,
  the termination argument, and the two "Deviations" this task closes)
- `docs/04_SIMULATION_SPEC.md` sections 8 (all "Realised by" blocks), 12.7, 20;
  `docs/09_TEST_STRATEGY.md` sections 2.2, 2.3

## Dependencies

- none

## Central decisions

### Where the fix goes

**Extend `navigationAndMovement`'s existing Pass 2, do not add a pre-pass and
do not rewrite the rival rule.** Pass 1 (`intents`) and Pass 3 (apply) are
unchanged in shape. Pass 2 gains a second resolution stage that runs **after**
the same-cell rival arbitration (`yieldedTo`) and **before** Pass 3:

1. **Stage 2a — rival arbitration (unchanged, TASK-017):** among agents that
   `wouldComplete` into the same next cell this tick, one wins (fewest
   remaining route steps, ties broken by ascending agent id); the rest are
   recorded in `yieldedTo` and will emit `MovementYielded`. Result: at most one
   *candidate mover* per target cell.
2. **Stage 2b — vacation-chain resolution (new):** decide which candidate
   movers may actually enter their next cell, given what its current occupant
   does. Produces an `obstructedBy: Map<int, AgentId>` (agent array index →
   the id of the agent blocking it), consumed by a new Pass 3 arm.

Pass 3 gains one arm: an `Advancing` agent found in `obstructedBy` freezes
exactly like a `yieldedTo` loser (`Progress = startProgress`, `Route = Some r`
written back, `Position` / `Destination` unchanged) and emits the new
`MovementObstructed` event instead of `MovementYielded`.

Only agents that `wouldComplete` their edge this tick are entering a cell, so
only they can be candidate movers, be obstructed, or free a cell. An agent
still mid-edge is a static occupant for this tick (it cannot vacate) and is not
a claimant — the exact TASK-018 framing.

### The vacation-chain rule

Let `M0` be the candidate movers after stage 2a (≤ 1 per target cell). At tick
start every cell holds at most one agent (the invariant this task enforces,
true by construction for every world builder — see "Base case" below). Define
`occupant(c)` = the unique agent whose `Position = c`, or none.

An agent `a ∈ M0` may move iff, following the chain

```
a  ->  occupant(next(a))  ->  occupant(next(occupant(next(a))))  ->  ...
```

the chain terminates at an agent whose next cell has **no occupant** — i.e. the
chain neither reaches a cycle nor reaches an agent that is not itself a
candidate mover.

Compute it as an **additive fixpoint** (monotone, order-independent, ≤ n
rounds, n = agent count):

- round 0: `S = { a ∈ M0 : occupant(next(a)) is none }` (target cell empty of
  every agent);
- round k+1: `S := S ∪ { a ∈ M0 : occupant(next(a)) ∈ S }` (the agent holding
  my target cell is already known to move);
- movers `= S` at the fixpoint; `obstructedBy = { a ∈ M0 \ S }`, each mapped to
  `occupant(next(a)).Id`.

A memoised depth-first walk with visiting-set cycle detection is an acceptable
equivalent implementation; the fixpoint is the specification because its
determinism is obvious (round k+1 consults only round k's `S` and pre-tick
positions, never the iteration order within a round).

**Swaps and rotation cycles fall out of this rule with no special case.** In a
2-cycle (`next(a) = b.Position`, `next(b) = a.Position`) neither agent is ever
seeded and neither is ever added, so both end up obstructed. Same for any pure
n-cycle. **This task deliberately does not add simultaneous rotation** (a
reviewer might expect a 3-cycle to rotate in place); rotation needs an
atomic-swap primitive and a tactical justification, and neither exists. Record
this as a decision, not an omission.

**A converging-on-occupied contest** (two agents want a cell held by a
stationary third): stage 2a already reduces it to one candidate, then stage 2b
obstructs that candidate. The rival loser gets `MovementYielded`, the rival
winner gets `MovementObstructed`, both in the same tick. This is the natural
layering of the new rule on the old one. If the implementing agent finds it
cleaner to run a *static-occupant* check (occupant is `Idle` / `Arrived` /
`Blocked` / mid-edge — definitely not moving) before stage 2a so that **every**
converging claimant gets `MovementObstructed`, that is an acceptable
refinement — but it must not change any other case's events and must stay
deterministic.

### Termination

The vacation-chain stage terminates trivially (finite monotone fixpoint). It
does **not** guarantee an obstructed agent ever completes: a mover whose route
is permanently blocked by an agent that never moves retries every tick,
emitting `MovementObstructed` each time, forever. That is correct behaviour for
this layer — the blocker might move later, and routing *around* a live agent is
the cooperative pathfinder this task forbids. Persistent obstruction is a
perception / appraisal concern (the stalled agent should notice and re-appraise
its order): flag it for B-015 / B-017, do not solve it here and do not add a
give-up-after-N-ticks constant.

### New event

Add `MovementObstructed of agent: AgentId * at: Cell * blocked: Cell *
occupant: AgentId` to `EventBody`. Distinct from:

- `MovementBlocked` — no traversable path exists; destination is **cleared**;
- `MovementYielded` — lost a same-tick rival contest for a cell to another
  *mover*; destination and route unchanged.

`MovementObstructed` means the next route cell is currently held by another
agent that did not vacate it this tick; destination and route are **unchanged**
and the agent retries next tick. It is a movement outcome, emitted in Pass 3 in
ascending agent id order (the `DomainEvent` ordering guarantee).

### No `Canonical.FormatVersion` bump, no new state, no hash re-pin

`Canonical.encode` takes `WorldState` and encodes `Agents` (`Position`,
`Progress`, `Destination`), `Random`, `Tick`, `Bounds`. This task adds **no
field** to `AgentState` or `WorldState`. The vacation-chain resolution is a
same-tick pure function of already-canonical pre-tick positions plus this
tick's intents (`Position`, `Destination`, `Terrain` via the derived `Route`,
`Progress`) — the identical argument that kept reservation out of the canonical
image in TASK-017. `Canonical.FormatVersion` stays **2**; `Canonical.encode` is
untouched.

**No committed scenario is expected to move a hash.** The shared fixture moves
one agent among five stationary ones and its A* route (X-before-Y tie-break)
never crosses column `x = 0`; `wall-detour` / `blocked-goal` / `slow-terrain`
are single-agent; `converging-routes` resolves its contest with agent 0 always
in motion, so agent 1 only ever enters `(3,3)` after agent 0 has left it (a
vacation chain that already produces today's result). The implementing agent
must run `-- corpus` and `-- fixture` before and after and **stop and report**
if any committed hash moves — a moved hash means the defect already shipped in
a fixture and re-pinning is out of this task's scope.

### Base case (pre-tick uniqueness)

`Scenario.validate` rejects a cell-sharing deployment (`docs/04` section 21),
and `Setup.sixAgentWorld` / `World.create` / the property generator place
agents on distinct cells. `World.build` itself does **not** enforce it. Adding
a `WorldError.AgentsShareCell` guard there would complete the invariant at the
construction boundary; the implementing agent **may** add it with a one-line
justification if it stays contained (one `WorldError` case, one guard, the
matching test), otherwise leave it and note in the ledger that construction is
covered by the scenario validator only.

## Diagnostics

`AGENTS.md`'s diagnostic rule applies: this changes authoritative spatial
behaviour (an agent that would have moved now stays), the same trigger
TASK-017's `Reserved` overlay answered.

- Add `Overlay.Obstructed of cell: Cell * occupant: AgentId`, derived in
  `Diagnostics.frameOf` from this tick's `MovementObstructed` events (the
  `reservationOverlays` precedent). `Diagnostics.frame` (no `StepResult`)
  emits none.
- Add an `eventMarker` arm for `MovementObstructed`.
- Wire `Obstructed` into `DiagnosticRender.fs`: both `Overlay`-exclusion
  filters, the `Ascii` overlay text loop, the `Svg` drawing loop. Expect four
  `FS0025` incomplete-match errors on first build and fix them — that confirms
  every existing `Overlay` match site was exhaustive.
- Commit a golden (`content/diagnostics/`) for the new corpus entry's obstructed
  tick, byte-compared by `DiagnosticsTests.fs`, plus a hand-built `Obstructed`
  overlay unit test (the `Reserved` precedent). Record the regeneration command
  in `content/diagnostics/README.md`.

Folding obstruction into the existing `Reserved` case instead of a new one is
the implementer's call if it is genuinely cleaner, but `Reserved` carries
`untilTick` and a contest `winner`, and an obstruction's blocker is a
stationary occupant, not a contest winner — a separate case is the expected
shape and matches the `Overlay` doc comment's "a new system adds a case here".

## Allowed scope

- `src/CommandoWar.Sim/Simulation.fs` — the stage-2b vacation-chain resolution,
  the `obstructedBy` map, one new Pass 3 arm, and the phase header comment
  (extend it: a "Realised by TASK-022" paragraph describing the occupancy rule,
  the swap/cycle decision, and the persistent-obstruction gap);
- `src/CommandoWar.Sim/Events.fs` — `MovementObstructed`;
- `src/CommandoWar.Sim/Domain.fs` — only if the optional `World.build`
  cell-sharing guard is taken (then also `Simulation.fs` `WorldError`
  consumers);
- `src/CommandoWar.Sim/Diagnostics.fs` — `Obstructed` overlay case,
  `eventMarker` arm, `frameOf` derivation, `Overlay` doc comment;
- `src/CommandoWar.Headless/DiagnosticRender.fs` — the four `Obstructed` match
  branches;
- `src/CommandoWar.Headless/Corpus.fs` — at least two new `Entry` values and
  their world builders;
- `content/replays/` — the new entries' `.cwlog` + `.md` (generated via
  `cwheadless corpus --regenerate`), `CORPUS.md` rows;
- `content/diagnostics/` — the new golden(s), `README.md`;
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs` — facts for mover vs
  stationary occupant, two-agent swap standoff, three-agent rotation cycle,
  three-agent follow chain, multiple agents converging on one occupied cell;
- `tests/CommandoWar.Sim.Tests/DeterminismPropertyTests.fs` — a new property
  (≥ 200 cases): after every tick of every generated case, distinct live agents
  hold distinct cells;
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs` — the `Obstructed` overlay
  unit test and the new golden comparison;
- `docs/04_SIMULATION_SPEC.md` (section 8 "Realised by TASK-022" block, section
  12.7 realisation note, section 20 invariant), `docs/09_TEST_STRATEGY.md`
  (section 2.2 realised list, section 2.3 scenario list if it names this);
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml` if
  it becomes active).

## Forbidden scope

- Any agent-aware or cooperative pathfinder: no re-routing around a live agent,
  no shared reservation table across ticks, no negotiation protocol, no
  push/follow intent inference. `Pathfinding` stays terrain-only and unchanged.
- Simultaneous rotation / atomic multi-agent swap primitives.
- A persistent-obstruction resolution (give-up timer, forced replan, re-appraisal
  hook) — that is B-015 / B-017 scope; name it, do not build it.
- New `AgentState` or `WorldState` fields; a `Canonical.FormatVersion` bump; any
  `Canonical.encode` change.
- Re-pinning any fixture / corpus / golden hash for an *existing* entry. A moved
  hash on `spike-fixture` / `wall-detour` / `blocked-goal` / `converging-routes`
  / `slow-terrain` is a stop-and-report finding.
- Changing the TASK-017 rival-arbitration rule (priority by remaining route
  steps, ties by agent id), the three-pass structure, or the neighbour order.
- `ScenarioContent.Version` or `CommandLogFile.Version` changes.
- Touching `Terrain`, `Pathfinding`, command intake, or any non-movement phase.

## Acceptance criteria

- [x] A `SimulationTests.fs` fact builds a world where agent A's only route
      runs through a cell held by a permanently idle agent B, and asserts: A
      never enters B's cell, A emits `MovementObstructed` (occupant = B), the
      two agents never share a cell on any tick.
- [x] A fact where two adjacent agents are each ordered onto the other's cell
      asserts both are obstructed indefinitely (a bounded loop: neither moves,
      neither shares a cell, both emit `MovementObstructed`).
- [x] A fact with three agents in a rotation cycle asserts all three are
      obstructed (no first mover) and no two share a cell.
- [x] A fact with three agents in a line — the lead ordered into a free cell,
      the two behind ordered into the cell ahead — asserts all three advance on
      the same tick (the follow chain resolves same-tick) and the invariant
      holds every tick.
- [x] A fact with two agents converging on a cell held by a stationary third
      asserts neither converging agent enters it and the invariant holds.
- [x] A new property (≥ 200 generated cases, over `randomCaseGen`): for every
      tick state of every case, `state.Agents` mapped to `Position` has no
      duplicate among live agents (`DeterminismPropertyTests.fs`). Print the
      reduced counterexample and seed on failure.
- [x] Property 1 (determinism under random commands) and property 2 (passable
      cells only) still pass unmodified at 200 cases each.
- [x] At least two new corpus entries: one follow / vacation chain that
      resolves each tick, and one swap or converging-on-occupied standoff that
      does not. Each has a `CORPUS.md` row, a committed hash table, and passes
      `CorpusTests.fs` and `cwheadless corpus`.
- [x] `Obstructed` overlay: derived in `frameOf`, rendered in `Ascii` and
      `Svg`, covered by a hand-built unit test and a committed
      `content/diagnostics/` golden byte-compared by `DiagnosticsTests.fs`.
- [x] `-- corpus` and `-- fixture` reproduce every **pre-existing** committed
      hash with no `--regenerate` (evidence: the before/after hash lines).
- [x] Every pre-existing `SimulationTests.fs`, `CorpusTests.fs`,
      `DiagnosticsTests.fs`, `ReplayTests.fs`, `FixtureTests.fs`,
      `PathfindingTests.fs` fact passes unmodified.
- [x] `dotnet build CommandoWar.slnx -c Release` = 0 warnings, 0 errors;
      `dotnet list src/CommandoWar.Sim package --include-transitive` =
      `FSharp.Core` only; source scan of `src/CommandoWar.Sim` clean
      (no `float` / `Stopwatch` / `DateTime` / `System.Random` / `godot`).
- [x] `docs/04` sections 8 / 12.7 / 20, `docs/09` section 2.2, backlog row,
      ledger index row + detail file, `PROJECT_STATE.yaml` (if active), task
      status updated. "Green tests" pinned fact refreshed with the new count.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`
- `dotnet test CommandoWar.slnx -c Release` before and after (state the new
  count and what each added test is)
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus` and
  `-- fixture` before any edit and after — byte-identical hashes for the five
  existing entries and the fixture
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus
  --regenerate` then `git diff --stat content/replays` — only the new entries'
  files appear; regenerating a staged corpus is idempotent (zero diff)
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
- source scan of `src/CommandoWar.Sim`
- `git status --porcelain` — matches the "Allowed scope" list exactly, nothing
  under client spikes, `bench/`, `src/_scratch`, or `content/fixtures/`

## Evidence to capture

- test summary (before/after counts), the new property's case count and the
  five new `SimulationTests` facts by name;
- the `-- corpus` / `-- fixture` hash lines before and after (proving the five
  existing entries and the fixture did not move);
- the new corpus entries' initial/final hashes, tick counts, event counts;
- the four `FS0025` errors from adding `Obstructed` to `Overlay`, then their
  resolution (confirming the match sites were exhaustive);
- the vacation-chain rule as implemented, and the swap/cycle behaviour, with
  the one-tick trace of the follow-chain corpus entry.

## Rollback or removal

`MovementObstructed`, `Overlay.Obstructed`, and the stage-2b resolution are
additive. Reverting is deleting the new Pass 3 arm and stage 2b (movers again
enter occupied cells), the event and overlay cases, the new corpus entries and
goldens, and the new tests. No data migration: no `Canonical.FormatVersion`
move, no existing hash re-pin, so nothing needs re-authoring on apply or
revert.

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md` (new TASK-022 row, new B-047 row);
- `docs/12_PROGRESS_LEDGER.md` index row + `docs/ledger/` detail file; refresh
  the "Green tests" pinned fact;
- `docs/04_SIMULATION_SPEC.md` sections 8, 12.7, 20;
- `docs/09_TEST_STRATEGY.md` section 2.2 (and 2.3 if it lists this scenario);
- `PROJECT_STATE.yaml` only if this becomes the active task;
- no ADR (this enforces an existing invariant — "one live agent has one
  authoritative position", `docs/04` section 20 — it does not decide a new
  architecture).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
