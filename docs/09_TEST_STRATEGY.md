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

**Command validation — partially realised (TASK-020, backlog B-014):**
`tests/CommandoWar.Sim.Tests/SimulationTests.fs` covers the current-scope
`docs/04` section 12.1 rules as focused facts over `Simulation.step` — a
multi-recipient accept emitting one `CommandAccepted` per recipient in
ascending `AgentId` order; a `Hostile`-side recipient rejected
`UnauthorisedRecipient` while a `Friendly` co-recipient still accepts; an
empty `Recipients` list rejected `EmptyRecipients`; a repeated recipient
rejected `DuplicateRecipient` as a whole command; two same-tick commands
sharing a `CommandId` both rejected `DuplicateCommandId` with a byte-identical
result whichever order the batch arrives in. Issue-tick eligibility is not
covered because it is not implemented (backlog B-044).

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

**Realised (TASK-019, backlog B-012b narrowed):** the invalid-cell, pathfinding
endpoint, and determinism-under-commands properties above are landed as
FsCheck properties in
`tests/CommandoWar.Sim.Tests/DeterminismPropertyTests.fs`, over generated
small worlds (random terrain, agents placed only on passable cells) and
random `MoveTo` command sequences. `FsCheck` and `FsCheck.Xunit` 3.3.4 are
referenced only by the test project, mirroring the BenchmarkDotNet
precedent. TASK-021 added a fourth property: `Pathfinding.find`'s returned
cost equals an independent relaxation-based shortest-path search over terrain
whose passable costs span the full valid `[BaseMoveCost, MaxMoveCost]` range
(the endpoint property only recomputes the returned path's own cost, so it
cannot catch a suboptimal path). TASK-022 added a fifth: after every tick of
every generated case, distinct live agents hold distinct cells — the "an
entity cannot occupy two cells at once" invariant in its pairwise form, which
property 2 (passable cells only) does not check. The remaining items name
systems not yet implemented (vehicles, commitments, death, canonical
serialization round-trip, appraisal, objectives) and stay proposed.

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

Partially realised for the choke-point item by the replay corpus
(`content/replays/`, section 2.4): `converging-routes` (TASK-017, two agents
contend for one cell and one yields) and `follow-chain` / `swap-standoff`
(TASK-022, a vacation chain that resolves each tick and a two-agent swap that
deadlocks with both agents emitting `MovementObstructed`). The rest name
systems not yet implemented.

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

Realised by TASK-016 (backlog B-012): `content/replays/` (index
`CORPUS.md`), a committed, regenerable multi-entry replay corpus over the
existing `.cwlog` v1 format (`src/CommandoWar.Headless/CommandLogFile.fs`) plus
a per-tick authoritative-hash table per entry (mirroring
`content/fixtures/SPIKE-FIXTURE.md`). Seven entries: the shared spike fixture
(cross-checked against `Fixture.run ()`, not an independent re-pin), a
single-agent wall detour and a `MovementBlocked` no-path case (both exercising
the TASK-015 executor), a two-agent "converging routes" entry that pinned the
then-current no-reservation behaviour (re-pinned by TASK-017 once
short-horizon cell reservation landed), a "slow-terrain" entry added by
TASK-018 (one cell costing more than `Terrain.BaseMoveCost`) pinning genuine
multi-tick `AgentState.Progress` accumulation — the other entries all
resolve every edge in a single tick, so none of them alone would catch a
sub-cell-progress regression — and, from TASK-022, a "follow-chain" entry (a
three-agent vacation chain that resolves every tick) and a "swap-standoff"
entry (a two-agent position swap that deadlocks, both agents emitting
`MovementObstructed` every tick). `Corpus.fs`
(`CommandoWar.Headless`) owns the name -> `WorldState` registry and the
check/regenerate logic; `cwheadless corpus [--regenerate]` is the CLI form and
`tests/CommandoWar.Sim.Tests/CorpusTests.fs` is an in-suite `[<Theory>]` over
the same entries (the `BenchmarkTests` "cheap in-suite flag" precedent), so
`dotnet test` catches a regression without an opt-in CLI run. A divergence
report names the first bad tick and the expected/actual hash; genuine
nondeterminism (two runs of the same entry disagreeing) additionally carries
the first differing canonical section and both sides' random-draw counts via
`Divergence.compare`. Generative property tests (needs a new dependency) and
component-level subhashes in the report are deferred to **B-012b**.

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

Realised by TASK-014 (backlog B-013): `bench/CommandoWar.Benchmarks/`, a
dev-only `net10.0` console project referencing `CommandoWar.Sim`,
`CommandoWar.Headless`, and BenchmarkDotNet (pinned) only. It is in
`CommandoWar.slnx` under `/bench/` and builds in Release with the solution, but
has no test SDK and no `[<Fact>]`, so `dotnet test` never collects it. Every
benchmark is `[<MemoryDiagnoser>]` and steps the real deterministic simulation
over a fixed `SyntheticWorlds` vector with no renderer or diagnostic-frame
work. The committed baseline is `content/benchmarks/BASELINE.md` (median, P95,
allocation per operation, run environment, commit) with a documented
regeneration command; it is a recorded baseline read for order-of-magnitude
sanity against the section 19 budget, not a test oracle, and no test asserts on
its numbers.

- **Covered now** (systems that exist): empty fixed tick; agent movement under
  the placeholder rule (six-agent and a synthetic ~50-agent world; real
  movement phase is B-011); line-of-sight batch (`Sight.trace`); pathfinding
  through open / blocked / choke-point maps (`Pathfinding.find`, plus
  `findWithin` closed-set-size evidence for the B-011 expansion budget);
  canonical encode; state hash; replay run.
- **Deferred**: 50-agent perception (backlog B-015), appraisal batch (B-017),
  combat, and a full synthetic 50-agent tick (B-019) attach to a commented
  slot in `bench/CommandoWar.Benchmarks/Benchmarks.fs` when those systems land.

The cheap in-suite regression flag remains
`tests/CommandoWar.Sim.Tests/BenchmarkTests.fs` (asserts only on tick
progression; rough timing to test output). It is kept, not folded into the
harness, so the green count stays 145.

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

Before framework selection, CI should build and test only the framework-neutral projects plus any explicitly disposable spike projects.

After selection, the minimum matrix should include:

- restore with locked or pinned dependencies;
- release build;
- headless unit and property tests;
- deterministic scenario and replay tests;
- selected client compile;
- content validation;
- package smoke test where the environment supports it.

**Framework-neutral items realised by TASK-023 (backlog B-048):**
`.github/workflows/ci.yml` runs on `windows-latest` on every push and pull
request and covers the first four rows — pinned-dependency restore (a
cold-cache `dotnet restore CommandoWar.slnx`; the exact `PackageReference`
pins plus the `global.json` SDK pin are the "pinned" half of row 1, so no
lockfile is added), Release build of `CommandoWar.slnx` (`bench/` builds with
it, benchmarks are not run), the full `dotnet test` suite (headless unit +
FsCheck properties), and the deterministic scenario / replay tests
(`FixtureTests`, the `CorpusTests` theory, and an explicit `cwheadless corpus`
CLI run that fails the job on any divergence from the committed
`content/replays/*.md` tables). `windows-latest` because the determinism
contract is per-environment (section 3) and every committed hash was pinned on
Windows x64 / .NET `10.0.303`.

**Still pending P4:** selected client compile, content validation, and package
smoke test. Client integration is P4, gated on G3 (`docs/08` section 7); these
rows have nothing to guard until a P4 client task adds client code, and that
task owns adding them to the same workflow.

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
participate in `Simulation.step` (ADR-0002); the golden renders have an
explicit regeneration command, recorded in `content/diagnostics/README.md`.

Realised by TASK-011.

TASK-015 applied this rule: the Navigation and movement phase adds the path an
agent is following as authoritative-derived spatial state, so
`Diagnostics.frameOf` now emits a `PlannedPath` overlay per following agent
(the `PlannedPath` case itself is from TASK-013). Goldens
`content/diagnostics/fixture-mid-route.ascii.txt` / `.svg` (fixture agent 3 at
tick 25, mid-route) are committed and `demo.html` is regenerated;
`content/diagnostics/README.md` carries the regeneration commands.
`Diagnostics.frame` still emits no overlay and nothing in a phase constructs a
frame.
