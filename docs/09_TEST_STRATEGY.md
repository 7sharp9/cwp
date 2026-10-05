# Test and Verification Strategy

Status: draft  
Primary objective: make simulation failures reproducible, localisable, and difficult to hide

## 1. Testing principles

- Test authoritative behaviour below the graphical client whenever possible.
- Prefer small deterministic scenarios over mocks of the whole game.
- Record seeds, commands, content versions, and state hashes in failure output.
- Do not approve a task based only on a successful manual run.
- Do not use broad exception catches, retries, timing sleeps, or ignored assertions to stabilise tests.
- A test that depends on framework frame rate is not a simulation test.
- Golden data is acceptable only when its meaning and update procedure are explicit.

## 2. Test layers

### 2.1 Domain unit tests

Use for pure or tightly bounded rules:

- command validation;
- cover lookup;
- grid neighbourhoods;
- line-segment and visibility rules;
- morale and stress transitions;
- appraisal stage outcomes;
- objective state transitions;
- leadership succession;
- canonical ordering and serialization.

These areas are covered by focused facts over `Simulation.step` in
`tests/CommandoWar.Sim.Tests/SimulationTests.fs`, with canonical-image facts in
`CanonicalHashTests.fs`.

**Command validation (TASK-020, backlog B-014; `docs/04` section 12.1).** A
multi-recipient accept emits one `CommandAccepted` per recipient in ascending
`AgentId` order. A `Hostile`-side recipient is rejected `UnauthorisedRecipient`
while a `Friendly` co-recipient still accepts. An empty `Recipients` list is
rejected `EmptyRecipients`. A repeated recipient is rejected
`DuplicateRecipient` as a whole command. Two same-tick commands sharing a
`CommandId` are both rejected `DuplicateCommandId`, with a byte-identical result
whichever order the batch arrives in. Issue-tick eligibility (TASK-024, backlog
B-044): a command issued after the tick being processed, and one with a
negative `IssuedAtTick`, are rejected `IssueTickOutOfRange`; a command issued on
the running tick, and one issued several ticks earlier and delivered now, are
accepted (there is no staleness horizon). `ReplayTests.fs` covers the
cross-tick guard: a command log reusing one `CommandId` on two ticks is rejected
`DuplicateCommandIdInLog` by replay validation (`Replay.run`).

**Order delivery (TASK-027, backlog B-016; `docs/04` section 12.2).** In a
scenario that authors no `Headquarters`, an order to a recipient with
`CommunicationAvailable = true` is delivered the same tick it is accepted
(`Destination` set, no `OrderUndelivered`). An order to a recipient with
`false` emits `OrderUndelivered` (`UnableToCommunicate`), writes no
`Destination`, and the agent never moves. A multi-recipient order delivers to
the reachable recipients and reports only the cut-off one. An undelivered order
does not cancel a `Destination` the recipient already held. Delivery is
deterministic across two runs. `CanonicalHashTests.fs` pins that
`CommunicationAvailable` is outside the canonical image (flipping it alone
changes neither the encoding nor the hash), while the order it suppresses
changes the hash within one tick via `Position`. The range, delay, jamming and
radio-destroyed mechanics (TASK-058, backlog B-016b) apply only when a scenario
authors a `Headquarters` and have their own facts.

**Appraisal stage outcomes (TASK-028, backlog B-017; `docs/04` section 12.5,
`docs/05` section 5).** A clear-route enemy-free order is `Accepted` and
`Destination` is written the same tick. An order with no path is
`Unable(NoKnownRoute)` and writes no `Destination` (no `MovementBlocked`). An
order along a route exposed to a known threat is `Refused(RouteTooExposed)` for
a low-`Discipline` agent and `Accepted` for a high-`Discipline` one (the G3
divergence). Directional cover on the exposed cells drops the exposure below
the threshold, and the low-`Discipline` agent then accepts. An `Accepted` order
is not re-appraised on a later idle tick. Appraisal is deterministic.
`Discipline` is static authored data outside the canonical image
(`Canonical.fs`), and `CanonicalHashTests.fs` and `FixtureTests.fs` pin
`Canonical.FormatVersion` (currently 15).

**Commitment establish, complete and supersede (TASK-030, backlog B-018;
`docs/04` section 12.6, `docs/05` section 9).** A fresh `Accepted` order emits
`CommitmentEstablished` the same tick as `OrderAppraised`. An agent that
arrives emits `CommitmentCompleted` and clears `Order` and `Disposition`. A
second order delivered mid-route emits a fresh `CommitmentEstablished` for the
new command with no event for the first (supersession). A `Refused` or `Unable`
order never emits `CommitmentEstablished`. An unchanged, already-appraised
order emits neither event on an idle tick. Commitment events are deterministic
and draw no randomness. A `Commitment.ofAgent` unit fact covers every reachable
`(Order, Disposition, Destination)` combination directly.

Later mechanisms (movement, perception, combat, casualties, formation,
objectives) have facts in the same file; pathfinding, line of sight and terrain
have `PathfindingTests.fs`, `SightTests.fs` and `TerrainTests.fs`.

### 2.2 Property tests

Use FsCheck or an equivalent F# property-testing library for invariants such as:

- no living agent occupies an invalid cell after a completed movement phase;
- an entity cannot occupy two vehicles or cells at once;
- dead agents do not issue or accept new commands;
- every accepted commitment references existing entities and areas;
- path output begins and ends at the expected cells when a path exists;
- state serialization followed by deserialization preserves canonical state;
- identical initial state, commands, and random stream produce identical hashes;
- appraisal never returns `Accepted` after a hard feasibility failure;
- objective completion is monotonic where the objective definition requires it.

The properties the implemented systems can support are FsCheck properties in
`tests/CommandoWar.Sim.Tests/DeterminismPropertyTests.fs` (TASK-019, backlog
B-012b narrowed), each run at `MaxTest = 200`. `FsCheck` and `FsCheck.Xunit`
3.3.4 are referenced only by the test project, mirroring the BenchmarkDotNet
precedent. The generators build small worlds (grid 3..7 by 3..7, 5..15 ticks,
random terrain, agents placed only on passable cells) and random `MoveTo`
command sequences. Properties 1 to 3 and 7 place 1-4 friendlies; properties 6
and 8 to 13 place 1-3 friendlies and 1-2 hostiles, and properties 8 to 13 also
give each friendly a random `Discipline`. Implemented:

1. Determinism under commands: two independent replays of one generated world
   and command sequence produce identical per-tick hashes.
2. No agent occupies an impassable cell after any tick of the movement phase.
3. After every tick, distinct live agents hold distinct cells (TASK-022): the
   pairwise form of "an entity cannot occupy two cells at once", which property
   2 does not check. The vehicle half has no system to test yet.
4. Whenever `Pathfinding.find` returns `Found`, the cells form an adjacent
   passable chain from start to goal with matching cost.
5. `Pathfinding.find`'s cost equals an independent relaxation-based
   shortest-path search over terrain whose passable costs span the full valid
   `[BaseMoveCost, MaxMoveCost]` range, and a path is found exactly when one
   exists (TASK-021; property 4 only recomputes the returned path's own cost, so
   it cannot catch a suboptimal path).
6. Every contact in `WorldState.TacticalKnowledge` after a tick was genuinely
   visible to some friendly on its own `LastSeenTick` (recomputed independently
   through `Perception.visibleContactsFor` over the pre-tick state), is within
   `PerceptionConfig.ExpireAfter` ticks of that sighting, carries one of the two
   valid confidence bands, and has a `LastSeenTick` that never decreases while
   the contact survives (TASK-026). The identical check runs on
   `WorldState.HostileTacticalKnowledge` against Hostile observers (TASK-034).
7. With a random subset of agents comms-blacked-out, a
   `CommunicationAvailable = false` agent never holds a `Destination` and never
   leaves its start cell, every `OrderUndelivered` names a blacked-out recipient
   and pairs with a same-tick `CommandAccepted`, and no comms-available agent is
   ever reported undelivered (TASK-027).
8. Every `AgentState.Disposition` after a tick is consistent (TASK-028):
   `Refused` and `Unable` write no `Destination`; `Accepted` writes `Destination`
   (or the agent is on the target) and its route is still reachable; an
   `Unable(NoKnownRoute)` target is still unreachable; a `Disposition` is `Some`
   only when the agent holds an `Order`; every `OrderAppraised` event matches the
   emitting tick's stored disposition.
9. Every agent's derived `Commitment` matches its `Order`, `Disposition` and
   `Destination` (TASK-030).
10. Every `ShotFired` outcome is reproducible by recomputing `Combat.hitChance`
    and redrawing from the pre-tick random state, with the reconstructed draw
    count matching `Random.Draws` exactly (TASK-031).
11. Every agent's post-tick `Suppression` is reproducible: from its pre-tick
    value, fold `Suppression.raise` with `Suppression.gain` (recomputed from
    each `ShotFired` that targeted it this tick), then apply one
    `Suppression.decay` (TASK-032).
12. Every agent's post-tick `Stress` and `SuppressionBand` are reproducible:
    one `Stress.gain` (this tick's `VisibleContacts` non-empty) folded with
    `Stress.raise` then one `Stress.decay` gives `Stress`, and the hysteresis
    latch over the pre-tick `Suppression` and `SuppressionBand`
    (`AppraisalConfig.SuppressionBandEnter` and `SuppressionBandExit`) gives
    `SuppressionBand` (TASK-033).
13. Every agent's post-tick `Vitals` is reproducible: from its pre-tick value,
    fold one `Casualty.wound` per `ShotFired` hit on it this tick, in ascending
    shooter id order, then apply one `Casualty.tickBleedOut` (TASK-045).

Round-trip properties of the same style cover the replay-command file
(`ReplayTests.fs`, section 2.4) and the scenario content file
(`ScenarioFileTests.fs`).

Not covered as generated properties: the vehicle half of the occupancy
invariant; canonical-state deserialisation (the canonical image is encode-only,
so there is no decoder to round-trip); dead agents refusing commands; and
objective-completion monotonicity. Focused facts cover the last two.

Randomly generated cases must print the reduced counterexample and seed.

### 2.3 Deterministic scenario tests

Create named, compact maps for behaviours that cross modules:

- exposed-road refusal;
- suppression reverses refusal;
- covered route adapts accepted order;
- stale enemy report decays;
- radio loss prevents immediate knowledge propagation;
- leader death transfers authority;
- destroyed crossing invalidates route;
- two agents reserve a choke point without permanent deadlock;
- enemy does not target an unobserved player position;
- extraction completes only for the required agents.

Scenarios are realised as replay-corpus entries (`content/replays/`, section
2.4) and as `SimulationTests.fs` facts:

- **Exposed-road refusal** (TASK-028): the `exposed-approach` entry and the
  appraisal facts of section 2.1. An order along a route past a known
  machine-gun position is `Refused(RouteTooExposed)` for a low-`Discipline`
  agent.
- **Suppression reverses refusal** (TASK-037, a thin slice of B-030; TASK-038,
  backlog B-023): in `suppress-relieves-exposure` a low-`Discipline` friendly is
  `Refused RouteTooExposed` against a known hostile, a second friendly's
  `Suppress` order drives that hostile's `AgentState.SuppressionBand` into its
  latched band, and the first friendly's order reappraises `Accepted` the same
  tick `Appraisal.routeExposure` starts zeroing the suppressed threat's
  contribution. `canonical-refusal-and-correction` plays the whole `docs/07`
  section 8 sequence.
- **Covered route adapts accepted order**: partial. Directional `Terrain.cover`
  drops the exposure below the threshold so the order is `Accepted` (the cover
  mitigation term), but there is no `Adapted` outcome or route recomputation
  (stage 5 of `docs/04` section 12.5 is not built, and no backlog item
  owns it).
- **Stale enemy report decays**: a fact in which a lost contact drops a
  confidence band after `StaleAfter` and expires with `ContactExpired` after
  `ExpireAfter`.
- **Radio loss prevents immediate knowledge propagation**: partial (TASK-027).
  The `lost-comms` entry (`content/replays/lost-comms.{cwreplay,md}`) orders a
  friendly with `CommunicationAvailable = false` to move: command intake accepts
  the order, the Communication phase emits `OrderUndelivered` and drops it, and
  the agent never moves. The tactical-knowledge half (a delivered report
  reaching the squad only through a working radio) is not exercised, because
  `Perception` does not read communication state, so the squad picture is not
  radio-gated.
- **Leader death transfers authority**: the
  `casualties-succession-and-squad-failure` entry.
- **Destroyed crossing invalidates route**: not built.
- **Two agents reserve a choke point without permanent deadlock**:
  `converging-routes` (TASK-017; two agents contend for one cell and one yields),
  `follow-chain` and `swap-standoff` (TASK-022; a vacation chain that resolves
  each tick, and a two-agent swap that deadlocks with both agents emitting
  `MovementObstructed`), `stalled-order-abandoned` (a permanently obstructed
  order is abandoned at `Simulation.StallAbandonTicks`) and `chokepoint-detour`
  (an obstructed agent adopts a detour around a parked agent).
- **Enemy does not target an unobserved player position** and the "Unknown
  threat" shape (`docs/05` section 16): observation (TASK-026) is covered by the
  `perception-contact` entry, which places a hostile inside
  `PerceptionConfig.SightRange` but behind an opaque wall, so it is not in
  `WorldState.TacticalKnowledge` until the friendly clears the wall, and by
  `SimulationTests` facts that an opaque cell blocks the observation entirely
  and that a hostile beyond `SightRange` is not observed. Targeting (TASK-034,
  backlog B-022, partial): `Simulation.combat` draws its candidate list from a
  shooter's own same-tick `AgentState.VisibleContacts` only, a strictly tighter
  check than the stale-tolerant `WorldState.HostileTacticalKnowledge` memory
  (the R-023 "same observation contract" mitigation), and a fact pins that a
  Hostile agent holding a stale `HostileTacticalKnowledge` entry for a friendly
  it can no longer see never appears as a shooter in that tick's `ShotFired`
  events. Enemy doctrine that reacts to either picture (suppress likely routes,
  seek cover under pressure, scripted fall-back) is descoped and unassigned
  (B-022).
- **Extraction completes only for the required agents**: `ExtractAgents` facts
  (a non-`Alive` agent is excluded from the requirement; the objective does not
  complete when every required agent is non-`Alive` and none was extracted) and
  the `demolition-success` entry.

Each scenario should specify:

- content version;
- initial authoritative state;
- command schedule;
- random seed;
- expected significant events;
- expected final hash or targeted assertions;
- a concise explanation of what regression it catches.

### 2.4 Replay tests

Maintain a small replay corpus:

- shortest successful mission path;
- canonical refusal and correction;
- leader death and succession;
- mission failure by squad loss;
- deliberately invalid or truncated replay;
- a long synthetic stress run.

Replay verification must identify:

- first divergent tick;
- expected and actual state hash;
- first differing canonical state section;
- command consumed at the tick;
- random draw count;
- simulation and content versions.

The corpus is `content/replays/` (index `CORPUS.md`, TASK-016, backlog B-012): a
committed, regenerable set of named entries, each a command log plus a per-tick
authoritative-hash table `<name>.md` (initial state, tick count, initial and
final hash, domain-event count, and the tick-to-hash column). The shared spike
fixture entry reads its commands from a hand-authored legacy command log,
`spike-fixture.cwlog` (`.cwlog` v1, `src/CommandoWar.Headless/CommandLogFile.fs`),
and is cross-checked against `Fixture.run ()` rather than being an independent
re-pin. Every other entry (TASK-036, backlog B-049) authors its geometry and
command schedule in `src/CommandoWar.Headless/Corpus.fs` (`Entry.Commands`),
which is the sole runtime source of truth. Most entries author both together as
one `ScenarioSpec` value; `order-queue-stacking-and-cancellation` keeps its
command array beside its spec, and `demolition-success` is a hand-built
`RawScenario`. Each entry's `<name>.cwreplay` (the production format, below) is
a generated, reviewable artefact that is never read back, so it cannot drift
from the geometry. The two `bridgehead-*` entries (TASK-077, backlog B-077)
start from the real `content/scenarios/bridgehead.cwscenario`, embedded into the
`cwheadless` assembly at build time, so any Bridgehead content edit re-runs them
and must regenerate them and re-check that their recorded `Succeeded` and
`Failed` stories still hold. A `Canonical.FormatVersion` bump moves every hash and so
re-pins every entry.

Against the list above: `demolition-success` is a short end-to-end run to
`MissionOutcome = Succeeded` and `bridgehead-succeeded` is the full-mission
replay (G4, "replay of a completed mission reproduces its authoritative
result"); `canonical-refusal-and-correction` is the refusal and correction
entry; `casualties-succession-and-squad-failure` covers leader death and squad
loss (`LeadershipTransferred`, `SquadFailure`) and `bridgehead-failed` ends
`MissionOutcome = Failed`. The invalid and truncated cases are `ReplayTests.fs`
facts rather than corpus files: replay validation rejects an unsupported replay,
command-log or canonical format version, an initial state not at tick 0, a seed
that disagrees with the initial random stream, a non-monotonic command log, a
command outside the run, and a reused `CommandId`, each as a typed error, and a
candidate run shorter than the reference is reported as `TruncatedRun`. A long
synthetic stress run is not built (the longest entry is `bridgehead-succeeded`
at 217 ticks). The other entries each isolate one mechanism and are described
in `CORPUS.md`.

`Corpus.fs` (`CommandoWar.Headless`) owns the name-to-`WorldState` registry
(`Corpus.all`) and the check and regenerate logic. `cwheadless corpus
[--regenerate]` is the CLI form. `tests/CommandoWar.Sim.Tests/CorpusTests.fs` is
an in-suite `[<Theory>]` over the same entries, so `dotnet test` catches a
regression without an opt-in CLI run. For each entry the check replays from the
initial state, verifies that a second independent run reproduces its own
hashes, and compares per-tick hashes, tick count and event count to the
committed `<name>.md`. A divergence report names the first bad tick and the
expected and actual hash. `Divergence.compare` additionally reports the first
differing canonical section (`Canonical.firstDifferingSection`) and both sides'
random-draw counts; `cwheadless compare` prints it for two command logs, and
`cwheadless render-divergence` (TASK-057, backlog B-050) renders the reference
and candidate `DiagnosticFrame` at the first differing tick, with an
`Overlay.Divergence` marking the divergent agent. The report does not carry the
command consumed at the tick or the simulation and content versions: the replay
container checks its format versions before the run and rejects a mismatch as a
typed error. Component-level per-agent subhashes in the report are deferred to
**B-012b**.

The production replay-command serialisation is
`src/CommandoWar.Sim/ReplaySerialisation.fs` (replay-command file format v1,
TASK-025, backlog B-045): a versioned, lossless, line-based text format that
carries the whole accepted-command envelope the legacy `.cwlog` cannot,
including multi-recipient addressing, `Urgency`, `RiskTolerance`, and an
`IssuedAtTick` distinct from the delivery tick. `ReplayTests.fs` covers it with
a `parse` and `serialise` round-trip property (`MaxTest = 200`) over generated
command logs, an explicit full-field-matrix round-trip fact, typed rejection of
an unknown file-format version (no guessed migration), of a canonical-format
mismatch and of an out-of-order command block, and one committed fixture,
`content/replays/envelope-full.cwreplay` (a three-recipient `MoveTo`,
`Urgency = Immediate`, `RiskTolerance = Aggressive`, issued a tick before
delivery), replayed and cross-checked against its own `checkpoint` lines, the
committed `envelope-full.md` table and a fresh `Replay.run`. `envelope-full` is
not in `Corpus.all`, so `cwheadless corpus` does not touch it.
`cwheadless replay-file <path>` is the CLI form: it exits 2 on a parse or
validate failure and 3 on checkpoint divergence, and prints the per-tick hash
table, the ordered accepted commands, and an `order appraisals` section (one
line per `OrderAppraised` event with its disposition and typed refusal reasons,
plus the mission outcome, so a recorded playtest session can answer why an agent
resisted; TASK-080). The `bridgehead` scenario label the Godot client writes
into a session file resolves to the same initial state as the `bridgehead-*`
entries.

Do not promise replay compatibility across arbitrary future versions. Version the format and fail explicitly when migration is unavailable.

### 2.5 Content tests

- schema and version validation;
- reference integrity;
- bounds and traversability checks;
- duplicate IDs;
- required objective and extraction markers;
- unknown terrain or object classes;
- scenario-specific smoke load;
- equivalent fixture import for both framework spikes.

`ScenarioTests.fs` covers `Scenario.validate` (typed errors, with no migration
of an earlier content version) and `World.ofScenario`; `ScenarioFileTests.fs`
covers the `.cwscenario` text format, including a round-trip property;
`TerrainTests.fs` covers terrain lookups. `cwheadless import <path.cwscenario>`
parses and validates a content file (TASK-060, B-024). The shared spike fixture
is reproduced as a `Scenario` and pinned to its initial and final hashes, and
the `bridgehead-*` corpus entries load the real Bridgehead content on every
`dotnet test`.

### 2.6 Client contract tests

The client boundary should be testable without asserting pixels:

- input adapter produces the expected typed command;
- snapshot adapter does not mutate authoritative state;
- interpolation does not alter simulation coordinates;
- domain event maps to the expected presentation request;
- pause stops command-time progression according to the selected rule;
- one client frame may consume zero, one, or several fixed simulation ticks correctly;
- invalid content reaches a visible failure state.

### 2.7 Visual smoke tests

After framework selection, use a small set of captured reference scenes for regressions in:

- isometric projection;
- depth sorting;
- selection and route overlays;
- roof or high-occluder handling;
- text legibility at supported scales;
- known-threat distinction;
- reason panel layout.

Do not use image snapshots as a substitute for behavioural assertions.

### 2.8 Performance and allocation tests

Benchmark at least:

- empty fixed tick;
- 50-agent movement;
- 50-agent perception;
- line-of-sight batch;
- pathfinding through open, blocked, and choke-point maps;
- appraisal batch;
- state hash and replay serialization;
- full synthetic 50-agent tick.

Record median, tail latency, allocation, runtime, build configuration, machine, and commit. Reject benchmarks that mix debug overlays or editor overhead with the core result unless that is the explicit subject.

The harness is `bench/CommandoWar.Benchmarks/` (TASK-014, backlog B-013), a
dev-only `net10.0` console project referencing `CommandoWar.Sim`,
`CommandoWar.Headless` and BenchmarkDotNet (pinned) only. It is in
`CommandoWar.slnx` under `/bench/` and builds in Release with the solution, but
has no test SDK and no `[<Fact>]`, so `dotnet test` never collects it. Every
benchmark is `[<MemoryDiagnoser>]` and steps the real deterministic simulation
over a fixed `SyntheticWorlds` vector with no renderer or diagnostic-frame work.
The committed baseline is `content/benchmarks/BASELINE.md` (median, P95,
allocation per operation, run environment, commit) with a documented
regeneration command. It is a recorded baseline read for order-of-magnitude
sanity against the `docs/04` section 19 budget, not a test oracle, and no test
asserts on its numbers.

- **Covered:** empty fixed tick; agent movement (a six-agent and a synthetic
  ~50-agent world, each stepping one `Simulation.step` with a fresh batch of
  `MoveTo` commands); line-of-sight batch (`Sight.trace`); pathfinding through
  open, blocked and choke-point maps (`Pathfinding.find`, plus the program's
  `expansions` argument, which prints the closed-set size A* reaches on each
  query as evidence for sizing an expansion budget); canonical encode; state
  hash; replay run.
- **Not covered:** 50-agent perception, appraisal batch, combat, and a full
  synthetic 50-agent tick. Those phases now exist but have no benchmark rows,
  and the committed baseline (recorded 2026-09-04) predates them, so its numbers
  exclude their cost. New rows attach to the commented slot in
  `bench/CommandoWar.Benchmarks/Benchmarks.fs`.

The cheap in-suite regression flag is
`tests/CommandoWar.Sim.Tests/BenchmarkTests.fs` (asserts only on tick
progression; rough timing goes to test output). It is kept separate from the
harness.

## 3. Determinism verification

### Required controls

- integer tick count as authoritative time;
- project-owned deterministic random generator;
- golden random vectors;
- stable iteration order for all authoritative collections;
- canonical sorting before hashing or serialization;
- explicit numeric rules;
- no framework physics or random source;
- no wall-clock reads in the simulation;
- no dependence on dictionary or hash-set enumeration order;
- no asynchronous mutation of authoritative state.

### Initial contract

The first contract is deterministic replay for the same:

- source revision;
- target framework and runtime version;
- architecture and operating environment;
- content version;
- initial state;
- command stream;
- seed.

Cross-platform lockstep is not claimed. Strengthening that contract requires an ADR and cross-target evidence.

## 4. Framework-spike verification

Both client spikes must use:

- the same compiled simulation assembly where practical;
- the same map dimensions and logical content;
- the same six agents;
- the same movement command;
- the same tick rate;
- the same snapshot fields;
- the same tick and state-hash overlay;
- the same content-change exercise;
- release-build packaging evidence.

Record failures rather than compensating with candidate-specific simulation changes.

## 5. Test execution order for agents

1. Run tests closest to the changed module.
2. Run affected deterministic scenarios.
3. Run replay tests if authoritative state changed.
4. Run the full headless suite.
5. Run client or content tests if the boundary changed.
6. Run benchmarks only when performance-sensitive code changed or a task requires evidence.

The completion report must list exact commands and outcomes. “Tests pass” is insufficient.

## 6. Continuous integration

CI builds and tests the framework-neutral solution only. `CommandoWar.slnx`
contains `CommandoWar.Sim`, `CommandoWar.Headless`, `CommandoWar.Sim.Tests` and
`CommandoWar.Benchmarks`; the Godot and Mibo client projects and `src/_scratch/`
are not in it, so CI never restores, builds or tests them.

With a client framework selected (Godot, ADR-0001), the minimum matrix is:

- restore with locked or pinned dependencies;
- release build;
- headless unit and property tests;
- deterministic scenario and replay tests;
- selected client compile;
- content validation;
- package smoke test where the environment supports it.

`.github/workflows/ci.yml` (TASK-023, backlog B-048) runs one job on
`windows-latest` on every push and on pull requests to `main`, and covers the
first four rows:

- Restore is a cold-cache `dotnet restore CommandoWar.slnx`. The exact
  `PackageReference` pins plus the `global.json` SDK pin (`10.0.303`, roll
  forward `latestPatch`) are the "pinned" half of the first row, so there is no
  lockfile and no NuGet cache (a warm cache would weaken the check that the pins
  resolve).
- Build is a Release build of `CommandoWar.slnx`. `Sim`, `Headless` and
  `Benchmarks` set `TreatWarningsAsErrors` per project; the test project
  deliberately does not (xUnit and FsCheck analyzer noise). The benchmarks are
  built, never run.
- Test is the full `dotnet test CommandoWar.slnx -c Release`, which includes
  `FixtureTests`, the FsCheck properties and the `CorpusTests` theory.
- Corpus verification is an explicit `cwheadless corpus` run (no
  `--regenerate`) that fails the job with exit 3 on any divergence from the
  committed `content/replays/*.md` tables. It duplicates the in-suite theory so
  the determinism claim is independently reproducible on a clean runner.
- A `cwheadless fixture` smoke step proves the verb runs; it does not compare
  against `content/fixtures/SPIKE-FIXTURE.md`, whose hashes are pinned by
  `FixtureTests.fs` and the `spike-fixture` corpus entry. A final step fails the
  job if the verb runs left the working tree dirty.

The runner is `windows-latest` only because the determinism contract is
per-environment (section 3) and every committed hash was pinned on Windows x64
with .NET SDK `10.0.303`.

The remaining rows (selected client compile, content validation, package smoke
test) are not in the workflow; client integration is P4 (`docs/08` section 7)
and the deferral is recorded in B-048. Client compile needs the Godot .NET SDK
at a pinned Godot version, a CI toolchain of its own, and the client projects
are outside the solution. The client task that adds them owns extending the same
workflow.

Do not expand the matrix to unsupported platforms without a delivery requirement.

## 7. Defect evidence

A simulation defect report should include:

- scenario or replay ID;
- source revision;
- content version;
- seed;
- command sequence or attached replay;
- first incorrect tick if known;
- expected behaviour;
- actual events and state hash;
- relevant appraisal or perception trace;
- smallest reproduction found.

A client defect report should additionally include framework version, renderer/backend, resolution, scaling mode, and input device.

## 8. Completion standard

A task that changes authoritative behaviour is complete only when:

- the intended behaviour has a focused test;
- a relevant invariant or regression test exists where practical;
- deterministic replay remains valid or its intentional format change is documented;
- test commands and outputs are recorded in the progress ledger;
- no failing test is disabled or weakened without an explicit decision.

### Diagnostic visualisation is a completion criterion

A task that adds or changes authoritative spatial or tactical state MUST also
extend the diagnostic frame (`src/CommandoWar.Sim/Diagnostics.fs`,
`DiagnosticFrame`: a `GridLayer`, an `EdgeMarker`, or an `Overlay` case) with
that state and add or update a golden visualiser output under
`content/diagnostics/` that covers the new behaviour. "Visually inspectable"
stands beside "has a focused test": a reviewer must be able to see the new
state in an ASCII, SVG, or HTML render. The diagnostic frame and its renderers
(`src/CommandoWar.Headless/DiagnosticRender.fs`) are observers only and never
participate in `Simulation.step` (ADR-0002): nothing in a phase constructs a
frame, and no frame feeds back into the step. The model is framework-neutral
(ints, bools, arrays, strings and DUs, no floating point), and the renderers
live in `CommandoWar.Headless`, never in the simulation (TASK-011).

`Diagnostics.frame` derives the layers, cover edges, agent markers and the
overlays that can be read from a `WorldState`; `Diagnostics.frameOf` adds the
this-tick event markers and the overlays that depend on the step's events or on
non-canonical caches, such as the `PlannedPath` overlay emitted for each agent
following a route (TASK-015). Overlays that only a caller can supply (`SightRay`,
`Divergence`) are never emitted by either.

The goldens in `content/diagnostics/` are byte-compared against fresh renderer
output in `DiagnosticsTests.fs`; the test project copies them next to the test
assembly. `content/diagnostics/README.md` lists each file and records the
explicit regeneration commands; the corpus-owned goldens are regenerated with
the same helpers the tests use, so a regeneration and its test cannot silently
disagree. A diff in a golden is either an intentional renderer or behaviour
change, regenerated and noted in the progress ledger, or a regression.
