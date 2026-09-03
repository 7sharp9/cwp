## 2026-09-02 - TASK-003 - Determinism, hash, and replay harness implemented

**Owner:** Dave with coding-agent assistance
**Source revision:** `65ccdb4` (Refine simulation phase loop and movement updates)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303 (`dotnet --version` = 10.0.303)
**Status change:** `active -> review`

### Changes

- New F# modules in `src/CommandoWar.Sim/` (compile order shown):
  - `Random.fs` (after `Ids.fs`) - `RandomAlgorithm` enum, `[<Struct>]`
    `RandomState { Algorithm; AlgorithmVersion; Word; Draws }`,
    `IDeterministicRandom` contract, `module SplitMix64` (v1: single 64-bit
    additive counter, gamma `0x9E3779B97F4A7C15`, wrapping arithmetic, seed
    initialises the counter directly), `SplitMix64.generator`, `module
    RandomStream`. `SplitMix64.next` `invalidArg`s on state from another
    algorithm.
  - `Canonical.fs` (after `Snapshot.fs`) - `Canonical.FormatVersion = 1`,
    `Canonical.encode : WorldState -> byte[]` (private big-endian fixed-width
    `Writer`; field order: format version, tick, bounds, random state, agent
    count, then agents sorted ascending by id, each with an explicit
    present/absent byte for `Destination`; no events/snapshot/trace),
    `Canonical.firstDifferingSection` diagnostic.
  - `Hashing.fs` (after `Canonical.fs`) - `[<Struct>] StateHash { Format; Value }`,
    `IStateHasher` contract, `module Hashing` (FNV-1a-64, offset
    `0xCBF29CE484222325`, prime `0x100000001B3`, over `Canonical.encode`),
    `Hashing.canonicalHasher`, `Hashing.digest`. Comment states it is not a
    cryptographic primitive.
  - `Replay.fs` (after `Simulation.fs`) - `RecordedCommand { Tick; Sequence;
    Command; Issuer }`, `CommandLog { Version; Commands }` (`CommandLog.Version
    = 1`, `CommandLog.create` sorts by `(Tick, Sequence)`), `Checkpoint`,
    `ReplayMeta`, `ReplayRecord { Version; CanonicalFormat; Meta; Seed;
    InitialState; TickCount; Log; Checkpoints }` (`Replay.FormatVersion = 1`),
    `ReplayOutcome`, `ReplayError` (8 explicit cases), `Replay.record`,
    `Replay.run` (validates versions/canonical format/tick-0/seed
    consistency/monotonic log/in-range command ticks, then regroups the log by
    tick and re-steps).
  - `Divergence.fs` (after `Replay.fs`) - `DivergencePoint`, `DivergenceReport`
    (`Match` / `TruncatedRun` / `Diverged`), `Divergence.compare` (first tick
    whose `StateHash` differs, with first differing canonical section and
    per-side random draw counts), `Divergence.diagnose`.
- `src/CommandoWar.Sim/Domain.fs` - `WorldState` gains `Random: RandomState`.
- `src/CommandoWar.Sim/Simulation.fs`:
  - `World.create` and `Setup.sixAgentWorld` take an explicit `seed: uint64`
    and build `WorldState.Random` via `SplitMix64.create`.
  - `StepResult` gains `StateHash: StateHash`, computed after the phase loop
    from the already-final state via `Hashing.canonicalHasher.Hash`.
    Authoritative `State`/`Events`/`Snapshot` are built first and do not depend
    on the hash value.
  - `StepState` accumulator gains `mutable Random`, carried through untouched
    (seam for future gameplay draws; none exist yet).
  - `SimConfig` doc comment updated (seed lives on `WorldState.Random`).
- `src/CommandoWar.Sim/CommandoWar.Sim.fsproj` - five new `<Compile>` entries.
- Tests: `tests/CommandoWar.Sim.Tests/{RandomTests,CanonicalHashTests,ReplayTests,BenchmarkTests}.fs`
  (34 facts) registered in the test project. `SimulationTests.fs` updated for
  the new `seed` parameter (one helper line + two `World.create` call sites).
  `Baseline.fs` / `BaselineTests.fs` untouched.

### Design decisions

- **One PRNG, SplitMix64.** Smallest well-documented generator with fixed-width
  64-bit arithmetic, a one-word serialisable state, and a publicly verifiable
  output vector. No per-entity or named streams (spec section 5 discourages
  them without evidence). No gameplay draws this task; the stream is threaded so
  replay can reconstruct it.
- **FNV-1a-64 over an explicit canonical encoding.** Dependency-free, defined in
  wrapping 64-bit integer arithmetic, adequate to detect replay divergence. Not
  collision-resistant; no security claim. `GetHashCode` is never used for
  authoritative or canonical hashing.
- **Hash computed in `step`, after Output, as a read-only checkpoint.** Every
  tick during development (spec section 12.11). A checkpoint cadence can replace
  per-tick hashing later without contract change.
- **Contracts isolate serialisation.** `IStateHasher`, `IDeterministicRandom`,
  and versioned record types mean the digest algorithm or byte layout can be
  swapped without touching `Simulation`.
- **Every replay failure is typed.** Unsupported replay/command-log/canonical
  versions, initial state past tick 0, seed inconsistent with the initial
  stream, negative tick count, non-monotonic log, and out-of-range command
  ticks each have a `ReplayError` case. `Divergence` never returns `Match` when
  hashes or run lengths differ.

### Verification

- Command: `dotnet --version`
  - Result: `10.0.303`.
- Command: `dotnet test CommandoWar.slnx -c Release --filter "FullyQualifiedName~RandomTests"`
  - Result: `Passed! - Failed: 0, Passed: 7`.
- Command: `dotnet test CommandoWar.slnx -c Release --filter "FullyQualifiedName~CanonicalHashTests"`
  - Result: `Passed! - Failed: 0, Passed: 11`.
- Command: `dotnet test CommandoWar.slnx -c Release --filter "FullyQualifiedName~ReplayTests"`
  - Result: `Passed! - Failed: 0, Passed: 14`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed! - Failed: 0, Passed: 49, Skipped: 0, Total: 49` (15 pre-existing + 34 new).
- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (`TreatWarningsAsErrors=true` on the sim project).
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only. No `ProjectReference`. No Godot, Mibo,
    MonoGame, raylib, Tiled, rendering, audio, UI, serialization, or wall-clock
    package.
- Command: source search in `src/CommandoWar.Sim` for
  `System.Random`, `DateTime.(Now|UtcNow|Today)`, `Stopwatch`,
  `Environment.TickCount`, `Guid.NewGuid`, `.GetHashCode()`,
  `System.Threading`, `Task.Run`, `async`, `Dictionary`, `HashSet`, `groupBy`.
  - Result: no matches.
- Benchmark (local Release run, `--filter "FullyQualifiedName~BenchmarkTests"`,
  50 000 ticks each after a 1 000-tick warm-up):
  - empty world: 48.0 ms, ~961 ns/tick.
  - six agents: 139.7 ms, ~2794 ns/tick.
  - Budget is 5 000 000 ns/tick (spec section 19); ~1800x headroom for six agents.

### Evidence

- **PRNG algorithm:** SplitMix64, version 1. `Name = "SplitMix64"`,
  `Version = 1`, `RandomState.Algorithm = RandomAlgorithm.SplitMix64` (enum `1`).
  Reference: Steele/Lea/Flood, "Fast Splittable Pseudorandom Number
  Generators", OOPSLA 2014; <https://prng.di.unimi.it/splitmix64.c>.
- **PRNG golden vector (seed 0, first 10 outputs), matches the published
  SplitMix64(0) sequence:**
  `0xE220A8397B1DCDAF 0x6E789E6AA1B965F4 0x06C45D188009454F 0xF88BB8A8724C81EC`
  `0x1B39896A51A8749B 0x53CB9F0C747EA2EA 0x2C829ABE1F4532E1 0xC584133AC916AB3C`
  `0x3EE5789041C98AC3 0xF3B8488C368CB0A6`.
- **PRNG golden vector (seed `0x123456789ABCDEF0`, first 8 outputs), captured
  from SplitMix64 v1:**
  `0x161922C645CE50E8 0xAD760CAFA1697B60 0x3501FF44902CA50D 0x417CB9A826D831DF`
  `0x99AF6F9B0C4476B6 0x5D51F5F75B762C59 0x66239E8C309A282B 0x53E01F580916C5CB`.
- **Canonical state format version:** `1`. Encoding is 150 bytes for the seeded
  six-agent world (44 header + 4 agent-count + 6x17 agent records).
- **Sample command record** (`RecordedCommand`):
  `{ Tick = 1L; Sequence = 0; Command = { Id = CommandId 100; IssueTick = 0L;
  Agent = AgentId 0; Intent = MoveTo { X = 3; Y = 0 } }; Issuer =
  "scenario:bench" }`.
- **Sample matching per-tick hashes** (`Setup.sixAgentWorld { 8; 8 } seed 1UL`,
  one move command for agent 0 to `(3,0)` at tick 1, six ticks; identical on
  repeat and via `Replay.run`):
  `tick 1 = 0xDFE3E93FA9B74E99`, `tick 2 = 0x48E22380006170B9`,
  `tick 3 = 0xC6D4A12B470BDE9B`, `tick 4 = 0xB4E8ED83DFBC5512`,
  `tick 5 = 0xD50CDDDB549D3B9D`, `tick 6 = 0xF238BE75FE54C0BC`
  (all `Format = 1`).
- **Sample divergence report** (same setup, candidate changes the tick-1
  command destination to `(3,5)`):
  `Diverged ({ Tick = 1L; Expected = { Format = 1; Value =
  16132994749810822809 }; Actual = { Format = 1; Value = 16857242920666251108 };
  Section = Some "Agent[0]"; ExpectedRandomDraws = 0; ActualRandomDraws = 0 },
  6, 6)`.
- **Public API added:** `Simulation.step` unchanged in shape but `StepResult`
  gains `StateHash: StateHash`; `World.create : GridBounds -> uint64 ->
  AgentState list -> Result<WorldState, WorldError>`; `Setup.sixAgentWorld :
  GridBounds -> uint64 -> WorldState`; `SplitMix64.create/next/generator`;
  `RandomStream.create/next/DefaultSeed`; `Canonical.encode/FormatVersion/
  firstDifferingSection`; `Hashing.hash/digest/canonicalHasher/Algorithm`;
  `Replay.record/run/FormatVersion`; `CommandLog.create/Version`;
  `RecordedCommand.forTick`; `ReplayMeta.unspecified`;
  `Divergence.compare/diagnose`.

### Deviations and unresolved issues

- `WorldState` gains a `Random` field and `World.create` / `Setup.sixAgentWorld`
  gain a `seed` parameter. This is the expected convergence flagged in the
  TASK-002 ledger entry; dependency direction and `step` semantics are
  unchanged, so no ADR supersession. `SimConfig` still holds only
  `TicksPerSecond`; the seed lives on the world, not the config.
- Hash is computed every tick. Spec section 12.11 allows "configured
  checkpoints or every tick during development"; a checkpoint cadence is a
  later refinement and needs no contract change.
- `Replay.run` filters the pre-sorted command log once per tick
  (O(commands x ticks)); `ReplayOutcome.TickStates` retains every tick's full
  `WorldState` for small-state divergence diagnosis. Both are adequate for the
  <64-agent, short-scenario budget and are noted as future work, not
  pre-optimised (spec section 19).
- `RecordedCommand` has no standalone version field; its schema is versioned
  through `CommandLog.Version`. Documented in `Replay.fs`.
- `IssueTick` on `PlayerCommand` is still carried but not enforced by `step`
  (unchanged from TASK-002; B-014). The replay runner groups by
  `RecordedCommand.Tick`, not `IssueTick`.
- Determinism contract is unchanged from `docs/09_TEST_STRATEGY.md` section 3;
  no cross-platform, bit-identical, or cryptographic claim is made.
- `PROJECT_STATE.yaml`: `active_work.selected_task` set to `TASK-003`; gate
  `G2_deterministic_core_proven` left `pending` (Dave's to advance on
  acceptance, consistent with TASK-001/002).
- Clean build run in the working tree (existing `bin/`/`obj/`), not a fresh
  clone.

### Documents updated

- `tasks/TASK-003-DETERMINISM-HARNESS.md` (status `active -> review`,
  acceptance criteria checked with evidence, completion notes)
- `tasks/TASK-002-SIMULATION-SKELETON.md` (status `review -> done`)
- `docs/11_BACKLOG.md` (TASK-002 `review -> done`, TASK-003
  `ready after dependency -> review`)
- `docs/04_SIMULATION_SPEC.md` (sections 5, 16, 17: realisation notes naming
  the concrete algorithm, format version, and modules)
- `PROJECT_STATE.yaml` (`active_work` -> TASK-003)
- `docs/12_PROGRESS_LEDGER.md` (this entry; TASK-002 review finalised)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-02)
- Notes: Determinism, hash, and replay harness accepted. Backlog row
  `review -> done`, task file status `done`. `G2_deterministic_core_proven`
  left `pending` (advanced only at the deterministic-core gate review, not by
  this harness task). TASK-004 (Godot spike) activated in `PROJECT_STATE.yaml`
  for a separate session; TASK-005 remains `ready after dependency`.
