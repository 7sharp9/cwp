# Progress and Evidence Ledger

This file is append-only except for correcting factual errors. It records what was actually done, not intended progress.

## Entry template

```markdown
## YYYY-MM-DD - TASK-NNN - concise outcome

**Owner:**  
**Source revision:**  
**Environment:**  
**Status change:** `old -> new`

### Changes

- ...

### Verification

- Command: `...`
  - Result: ...
- Manual check: ...
  - Result: ...

### Evidence

- Paths, logs, screenshots, benchmark files, replay IDs, or hashes.

### Deviations and unresolved issues

- None, or explicit details.

### Documents updated

- ...

### Review

- Reviewer: Dave
- Accepted: yes/no/pending
- Notes: ...
```

## 2026-09-02 - PLAN-001 - Initial project control pack assembled

**Owner:** Dave with planning assistance  
**Source revision:** no code repository supplied  
**Environment:** documentation-only planning session  
**Status change:** `unstructured concept -> P0 governance and feasibility`

### Changes

- Narrowed the product hypothesis to explainable and reversible agent disobedience.
- Performed an adversarial review of the proposed game and architecture.
- Replaced the premature Godot commitment with a controlled Godot-versus-Mibo framework spike.
- Kept the authoritative simulation as a framework-independent F# library.
- Reduced the initial AI from hierarchical planning plus broad utility selection to explicit appraisal, typed commitment, finite execution, and ordered interrupts.
- Defined project gates, source-of-truth files, risks, backlog, ADRs, and bounded coding-agent tasks.
- Prohibited multi-era content, vehicles, multiplayer, modding, runtime LLMs, and Mibo.Adaptive before the external-playtest gate.

### Verification

- Documentation consistency and internal-link checks are required before this entry is accepted.
- No implementation, build, test, benchmark, or playtest has occurred.

### Evidence

- `README.md`
- `PROJECT_STATE.yaml`
- `docs/`
- `decisions/`
- `tasks/`

### Deviations and unresolved issues

- The production presentation framework is intentionally unresolved.
- No repository structure has been inspected because no code repository was supplied for this task.
- Version facts are a research snapshot dated 2026-09-02 and must be rechecked before dependency installation.

### Documents updated

- Initial creation of the full project pack.

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: G0 remains in review until the pack and repository baseline are accepted.

## 2026-09-02 - TASK-001 - Repository and build baseline established

**Owner:** Dave with coding-agent assistance
**Source revision:** `1cfb0ea` (Initial commit)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303 (also installed: 9.0.310, 5.0.416); runtimes include 8/9/10
**Status change:** `active -> review`

### Changes

- Added `global.json` pinning SDK `10.0.303` with `rollForward: latestPatch`.
- Added `CommandoWar.slnx` (SDK-default solution format) with two projects.
- Added `src/CommandoWar.Sim/` — F# `net10.0` class library, no host/framework/platform dependency. Package references: `FSharp.Core` only (implicit, SDK-pinned 10.1.303). `TreatWarningsAsErrors` enabled.
- Added `tests/CommandoWar.Sim.Tests/` — F# `net10.0` xUnit project (`xunit` 2.9.3, `Microsoft.NET.Test.Sdk` 17.14.1, `xunit.runner.visualstudio` 3.1.4, `coverlet.collector` 6.0.4), with a project reference to the simulation library.
- `src/CommandoWar.Sim/Baseline.fs`: module `Baseline` with `ContractName` literal and `nextTick : int -> int` (placeholder boundary function, not the simulation step). No game behaviour.
- `tests/CommandoWar.Sim.Tests/BaselineTests.fs`: two facts exercising the cross-project boundary.
- Added `.gitignore` (dotnet template) so `bin/` and `obj/` stay untracked.
- `README.md`: added an additive "Repository baseline" section with the pinned SDK, target framework rationale, layout, and restore/build/test commands.

### Framework and SDK rationale

- `net10.0` chosen over `net9.0`: both SDKs are installed and currently supported, but .NET 9 reaches end of support on 2026-11-10 while .NET 10 is LTS through November 2028. `net10.0` is also the SDK's default template TFM. .NET 5 is installed but long out of support and was excluded.
- SDK `10.0.303` pinned because it is the highest installed 10.x SDK verified via `dotnet --info`.

### Verification

- Command: `dotnet --info`
  - Result: SDK 10.0.303 confirmed; SDKs 9.0.310 and 5.0.416 also present.
- Command: `dotnet restore CommandoWar.slnx`
  - Result: both projects restored, no errors.
- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2`.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only. No Godot, Mibo, MonoGame, raylib, or Tiled reference.
- Manual check: `git status` shows only intended additions (`.gitignore`, `CommandoWar.slnx`, `global.json`, `src/`, `tests/`) plus additive edits to `README.md`, `docs/11_BACKLOG.md`, `tasks/TASK-001-*.md`, and this ledger. No existing file deleted, moved, or reformatted.

### Evidence

- Public simulation API: `CommandoWar.Sim.Baseline.ContractName : string`, `CommandoWar.Sim.Baseline.nextTick : int -> int`.
- Solution: `CommandoWar.slnx`. Projects: `src/CommandoWar.Sim/CommandoWar.Sim.fsproj`, `tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj`.
- Simulation project package references: `FSharp.Core` (implicit, 10.1.303).

### Deviations and unresolved issues

- No headless console/host project was added; it was not required to prove the boundary (the test project references the library directly). TASK-002 introduces the fixed-tick API.
- Solution uses the newer `.slnx` format (SDK default). All `dotnet` commands accept it; older Visual Studio versions may not.
- `PROJECT_STATE.yaml` gate `G0` and `active_work` were left unchanged: evidence does not justify advancing G0 (needs Dave's acceptance) or activating TASK-002.
- Clean-state build was exercised by deleting all `bin/` and `obj/` directories and re-running restore, build, and test in the working tree; not a separate fresh clone.

### Documents updated

- `tasks/TASK-001-REPOSITORY-BASELINE.md` (status `review`, acceptance criteria checked)
- `docs/11_BACKLOG.md` (TASK-001 row `active -> review`)
- `README.md` ("Repository baseline" section)
- `docs/12_PROGRESS_LEDGER.md` (this entry)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-02)
- Notes: Accepted as project inception. G0 marked `passed`; `PROJECT_STATE.yaml` and backlog advanced to activate TASK-002. TASK-002 implementation to run in a separate session.

## 2026-09-02 - TASK-002 - Framework-neutral simulation skeleton implemented

**Owner:** Dave with coding-agent assistance
**Source revision:** `05ccdff` (Establish .NET simulation/test baseline)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303 (`dotnet --version` = 10.0.303)
**Status change:** `active -> review`

### Changes

- Added F# modules to `src/CommandoWar.Sim/` (compile order):
  - `Ids.fs` — `AgentId`, `CommandId` as `[<Struct>]` single-case unions with private constructors and `ofInt` / `value` smart accessors.
  - `Grid.fs` — `Cell { X; Y }`, `GridBounds { Width; Height }`, `GridBounds.contains`.
  - `Domain.fs` — `Side`, `AgentState { Id; Side; Position; Destination }`, `WorldState { Tick: int64; Bounds; Agents: AgentState[] }`, `Agent.create`.
  - `Commands.fs` — `PlayerIntent.MoveTo`, `PlayerCommand { Id; IssueTick; Agent; Intent }`, `CommandRejection` (`UnknownAgent`, `TargetOutOfBounds`), `Command.moveTo`.
  - `Events.fs` — `EventBody` (`CommandAccepted`, `CommandRejected`, `MovementStepped`, `MovementCompleted`), `DomainEvent { Tick; Body }`.
  - `Snapshot.fs` — `AgentSnapshot`, `RenderSnapshot { Tick; Agents }`.
  - `Phases.fs` — `Phase` (11 cases) and `Phases.order`, the single source of truth for phase order.
  - `Movement.fs` — `PlaceholderMovement.nextCell`: one cardinal step per tick toward a destination (X axis then Y axis). Explicitly not pathfinding; isolated for replacement by B-010/B-011.
  - `Simulation.fs` — `SimConfig`, `StepResult { State; Events; Snapshot; PhaseTrace }`, `WorldError`, `World.create`, `Setup.sixAgentWorld`, and `Simulation.step` which folds a contained mutable accumulator over `Phases.order`.
- Added `tests/CommandoWar.Sim.Tests/SimulationTests.fs` (13 facts) and registered it in the test project.
- Registered the nine new source files in `src/CommandoWar.Sim/CommandoWar.Sim.fsproj`.
- `Baseline.fs` / `BaselineTests.fs` left unchanged.

### Design decisions

- `step` signature matches the ADR-0002 reference boundary: `SimConfig -> PlayerCommand[] -> WorldState -> StepResult`. `StepResult` adds a `PhaseTrace: Phase[]` diagnostic so the phase schedule is assertable (acceptance criterion). `SimConfig` currently carries only `TicksPerSecond` (non-authoritative scheduling hint); `step` guards it with `invalidArg` if non-positive.
- Tick is `int64`, incremented once per `step` with an explicit `invalidOp` guard at `Int64.MaxValue`. No floating-point time anywhere.
- Commands are processed at the `CommandIntake` phase, sorted by ascending command id. Movement resolves at `NavigationAndMovement`, iterating agents in ascending agent id order. Snapshot agents are sorted by agent id. No hash-map enumeration in authoritative logic.
- Internal step mutation is confined to a `private` accumulator and copied agent arrays; the input `WorldState` is never mutated (verified: `state.Agents` reference is copied before any write).
- The phase match in `runPhase` lists all 11 cases individually (no wildcard) so adding a phase forces a handling decision.
- No `System.Random`, `DateTime.Now`, `Stopwatch`, async, or host dependency introduced.

### Verification

- Command: `dotnet --version`
  - Result: `10.0.303`.
- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (simulation project has `TreatWarningsAsErrors=true`).
- Command: `dotnet test CommandoWar.slnx -c Release --filter "FullyQualifiedName~SimulationTests"`
  - Result: `Passed! - Failed: 0, Passed: 13, Skipped: 0, Total: 13`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed! - Failed: 0, Passed: 15, Skipped: 0, Total: 15` (2 baseline + 13 new).
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only. No Godot, Mibo, MonoGame, raylib, Tiled, rendering, audio, UI, or wall-clock package. No `ProjectReference` in the simulation project.
- Manual check: `dotnet fsi` script stepping the six-agent world with one move command for agent 2 from `(0,2)` to `(2,3)`.
  - Result (tick 1): phase trace = full 11-phase order; events =
    `{ Tick = 1L; Body = CommandAccepted (CommandId 1, AgentId 2, { X = 2; Y = 3 }) }`,
    `{ Tick = 1L; Body = MovementStepped (AgentId 2, { X = 0; Y = 2 }, { X = 1; Y = 2 }) }`;
    snapshot `Tick = 1`, agent 2 `Position = { X = 1; Y = 2 }`, `Destination = Some { X = 2; Y = 3 }`, all others unchanged with `Destination = None`.

### Evidence

- Public simulation API:
  - `Simulation.step : SimConfig -> PlayerCommand[] -> WorldState -> StepResult`
  - `World.create : GridBounds -> AgentState list -> Result<WorldState, WorldError>`
  - `Setup.sixAgentWorld : GridBounds -> WorldState`
  - `Agent.create : AgentId -> Side -> Cell -> AgentState`
  - `Command.moveTo : CommandId -> int64 -> AgentId -> Cell -> PlayerCommand`
  - `AgentId.ofInt`, `AgentId.value`, `CommandId.ofInt`, `CommandId.value`
  - `GridBounds.contains`, `Phases.order`, `SimConfig.standard`
  - `PlaceholderMovement.nextCell : Cell -> Cell -> Cell`
- Phase order (`Phases.order`): `CommandIntake; Communication; Perception; TacticalKnowledge; Appraisal; CommitmentAndLocalAction; NavigationAndMovement; Combat; StateConsequences; Mission; Output`.
- Simulation project package references: `FSharp.Core` (implicit, 10.1.303) only.
- New/changed files: `src/CommandoWar.Sim/{Ids,Grid,Domain,Commands,Events,Snapshot,Phases,Movement,Simulation}.fs`, `src/CommandoWar.Sim/CommandoWar.Sim.fsproj`, `tests/CommandoWar.Sim.Tests/SimulationTests.fs`, `tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj`.

### Deviations and unresolved issues

- `StepResult` extends the ADR-0002 reference record with `PhaseTrace`; `SimConfig` is not yet the architecture's `SessionConfig` (no scenario/seed). Both are expected to converge in TASK-003. Dependency direction and semantics are unchanged, so no ADR supersession.
- `WorldState.Agents` and snapshot arrays are exposed as plain `AgentState[]` / `AgentSnapshot[]`. Callers could mutate the returned array; the contract treats them as read-only. Tightening to `IReadOnlyList` is deferred as it adds F# ceremony not required by the task.
- Placeholder movement has no obstacle, occupancy, or reservation handling. Safe only because the skeleton grid is an empty rectangle and destinations are validated in-bounds at intake. Real movement is B-010/B-011.
- Issue-tick eligibility is not enforced; `IssueTick` is carried but unused by `step`. Command validation and recipient selection are B-014.
- State hashing and replay are not implemented (TASK-003 scope).
- `PROJECT_STATE.yaml` left unchanged: TASK-002 is `review`, not `done`; gate `G1` and task selection remain Dave's to advance, consistent with the TASK-001 precedent.
- Clean-build was run in the working tree (existing `bin/`/`obj/`), not a fresh clone.
- Data-structure choices are adequate for the &lt;64-agent budget but were flagged in review as future hot paths, not to be changed until profiled (docs/04_SIMULATION_SPEC.md section 19): agent lookup in `commandIntake` is a linear `Array.tryFindIndex` per command; each accepted command does an `Array.copy` of the agent array; per-tick command/event/trace lists are built reversed then `List.rev |> List.toArray`. A dense id-indexed agent store or a batched update map would replace these when a benchmark justifies it.

### Post-review idiom pass (2026-09-02, after Dave's review)

- `Simulation.step` now iterates `Phases.order` with a `for` loop instead of `List.fold` over a mutable accumulator (the fold was threading a value it also mutated in place). `runPhase` returns `unit`.
- `navigationAndMovement` builds one record per moved agent instead of up to three successive copy-updates.
- Sorts use the id types' built-in structural comparison (`sortBy (fun a -> a.Id)`) rather than projecting through `AgentId.value` / `CommandId.value`.
- Re-verified: `dotnet build CommandoWar.slnx -c Release` -> `0 Warning(s) 0 Error(s)`; `dotnet test CommandoWar.slnx -c Release` -> `Passed! Failed: 0, Passed: 15`.

### Documents updated

- `tasks/TASK-002-SIMULATION-SKELETON.md` (status `review`, acceptance criteria checked with evidence, completion notes)
- `docs/11_BACKLOG.md` (TASK-002 row `active -> review`)
- `docs/12_PROGRESS_LEDGER.md` (this entry)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-02)
- Notes: Idiom pass and future-perf note requested and applied. Accepted after review; backlog row `review -> done`, task file status `done`. TASK-003 activated in `PROJECT_STATE.yaml` for a separate session.

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

## 2026-09-02 - TASK-004 - Disposable Godot .NET framework spike + shared headless reference

**Owner:** Dave with coding-agent assistance
**Source revision:** `f363174` (Add deterministic random/hash/replay harness)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303 (`dotnet --version` = 10.0.303);
Godot `4.7.2.stable.mono.official.ed1daf0bf` at
`C:\Users\Dave\Documents\GitHub\Godot_v4.7.2-stable_mono_win64\` (not on PATH);
GPU NVIDIA RTX 5070 Ti, Vulkan 1.4.329 Forward+
**Status change:** `active -> review`

### Changes

**Framework-neutral (in `CommandoWar.slnx`):**

- New project `src/CommandoWar.Headless/` - F# `net10.0` console exe
  (`AssemblyName` `cwheadless`), `FSharp.Core` + a `ProjectReference` to
  `CommandoWar.Sim` only, `TreatWarningsAsErrors`. This is the shared headless
  reference for both framework spikes.
  - `CommandLogFile.fs` - minimal versioned (`v1`) line-based command-log text
    format (`<tick> <agentId> move <x> <y>`), typed `ParseError` naming the
    source line, `describeError`.
  - `Fixture.fs` - the pinned shared logical fixture: `Setup.sixAgentWorld` on
    a 32x32 grid, seed `20260902`, one move command (tick 1, agent 3 ->
    (20,14), `CommandId 1`), 40 ticks. Depends only on `CommandoWar.Sim`.
  - `Program.fs` - subcommands `step <N>`, `replay <log> [--ticks N]`,
    `compare <a> <b> [--ticks N]`, `fixture`. Exit codes: 0 ok, 1 usage/IO,
    2 replay error, 3 divergence detected. Uses `Simulation.step`,
    `Replay.record`/`Replay.run`, `Divergence.diagnose`.
- Registered `CommandoWar.Headless` in `CommandoWar.slnx`.
- `content/fixtures/SPIKE-FIXTURE.md` - committed framework-neutral reference:
  fixture parameters + the 40-row per-tick `StateHash` table + final agent
  positions. Regenerated by `cwheadless fixture`.
- `content/fixtures/spike-fixture.cwlog` - the canonical one-command log.
- `tests/CommandoWar.Sim.Tests/` - `FixtureTests.fs` (5 facts) pinning the
  fixture parameters, the initial hash `0xF2F3DF0D820AD9AC`, the full 40-tick
  hash sequence, and the command-log parser. Added a `ProjectReference` to
  `CommandoWar.Headless` (framework-neutral) in the test `.fsproj`.
- `CommandoWar.Sim` and its existing tests: **unchanged** (verified byte-identical
  `CommandoWar.Sim.dll`, md5 `9168def701ba9bdeadafc1bd7918e9e6`, consumed by
  the Godot host).

**Godot spike (its own `src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx`,
deliberately NOT in `CommandoWar.slnx`):**

- Godot 4.7.2 .NET project targeting `net10.0` (Godot's template default is
  `net8.0`, but a C# project cannot reference the `net10.0` `CommandoWar.Sim`;
  `net10.0` builds and runs under SDK 10.0.303). `nuget.config` adds the Godot
  editor's offline `Tools/nupkgs` feed.
- `src/SimFacade.cs` + `src/SpikeContent.cs` - the thin C# facade and the
  framework-neutral content DTO + validator. **No Godot types.** Only
  `SimFacade` calls `CommandoWar.Sim`; everything across the boundary is a
  primitive/array/F# value. World is built via `World.create` +
  `ListModule.OfArray`; F# module functions reached as `AgentIdModule.ofInt`
  etc.
- `src/GreyboxScene.cs` / `src/SpikeMarker.cs` - the authored-content import
  boundary (touch Godot types, return `RawContent`).
- `src/MainNode.cs` - host-owned fixed-step scheduler (accumulator in
  `_Process`, `1/simHz`, catch-up capped at 5 steps/frame; the sim only ever
  receives an integer tick), input -> `Command.moveTo`, `_Draw` isometric
  render with view-only prev/curr interpolation, tick/hash/rate overlay,
  `--selfcheck` / `--invalid` / `--screenshot` / `--expect` launch args.
- `scenes/Greybox.tscn` - authored 32x32 isometric greybox: terrain as a
  multiline `.` / `#` string, 6 `FriendlySpawn` markers (ids 0..5 at column 0),
  1 `ObjectiveArea` marker, wall block + scattered cover. Reproduces
  `Setup.sixAgentWorld { 32; 32 } 20260902` exactly.
- `scenes/GreyboxInvalid.tscn` - spawn on a wall + duplicate id + missing
  objective (three errors in one load).
- `README.md`, `.gitignore` (`.godot/`, `bin/`, `obj/`, `export/`, editor
  `.sln`).
- `docs/evidence/task-004-godot-overlay.png` - self-captured overlay screenshot.

### Design decisions

- **Terrain is client-side only.** `CommandoWar.Sim` has no terrain model yet
  (B-008), so walls are authored, validated against (spawn-on-wall is
  rejected), and rendered, but never cross the boundary. The sim world is
  exactly 6 agents + bounds + seed.
- **Fixed step is host-owned, not `_PhysicsProcess`.** A manual accumulator in
  `_Process` makes "scheduled independently of render rate" unambiguous and
  lets the rate be changed at runtime (keys `1`/`2`). No Godot physics,
  navigation, random, or animation is authoritative.
- **The facade never catches `Simulation.step`.** The one catch is
  `ContentException` at load, to render the actionable failure screen.
- **Screenshot mode slows the scheduler to 6 Hz** purely so the still shows a
  mid-move agent with real rate numbers; per-tick hashes are pace-independent.
- **`Godot.NET.Sdk` MSBuild SDK + offline feed** so the client restores without
  a network round-trip against the pinned editor.

### Verification

- Command: `dotnet test CommandoWar.slnx -c Release`
  - Before: `Passed! - Failed: 0, Passed: 49`.
  - After: `Passed! - Failed: 0, Passed: 54` (49 + 5 `FixtureTests`).
- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Error(s)` (Sim `TreatWarningsAsErrors=true`).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  - Result: initial hash `0xF2F3DF0D820AD9AC`; per-tick table ticks 1..40;
    final `0x838D3AE7DBFB735D` (format 1); 33 events; final agent 3 at (20,14).
- Command: `... -- replay content/fixtures/spike-fixture.cwlog`
  - Result: final hash `0x838D3AE7DBFB735D`, 33 events (matches `fixture`).
- Command: `... -- compare content/fixtures/spike-fixture.cwlog <alt>` (alt =
  agent 3 -> (20,9))
  - Result: `DIVERGED at tick 1`, reference `0xC848D905A9CAD13F` vs
    `0x373896AEDE8FE99E`, first section `Agent[3]`, exit 3.
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Release`
  - Result: `Build succeeded. 0 Error(s)`. Output to
    `.godot/mono/temp/bin/Release/`.
- Command: `<godot> --headless --path src/CommandoWar.Client.Godot -- --selfcheck --expect 0x838D3AE7DBFB735D`
  - Result: exit 0, `MATCH`. Per-tick `tick=N hash=0x...` lines for ticks
    1..40 are **identical** to `cwheadless fixture` (41-hash diff, initial +
    40 ticks, no differences). `accepted-command-log: 1 3 move 20 14`.
- Command: `<godot> ... -- --selfcheck --expect 0xDEADBEEFDEADBEEF`
  - Result: exit 1, `MISMATCH` (exit-code path works).
- Command: `<godot> --headless --path src/CommandoWar.Client.Godot -- --selfcheck --invalid`
  - Result: exit 2, `CONTENT LOAD FAILED` listing all three authored errors,
    each naming the node / id / cell / reason. Simulation not started.
- Command: `<godot> --path src/CommandoWar.Client.Godot -- --screenshot <abs>.png`
  - Result: windowed run outside the editor (Vulkan/NVIDIA), self-captured
    `docs/evidence/task-004-godot-overlay.png` at tick 14, hash
    `0x04343D056D0BAC45` (matches fixture tick 14). Overlay shows tick, state
    hash + format, random draws, sim rate 6 Hz target / 6 measured, render
    rate 59 fps (Engine 60), interp alpha 0.34 - render and tick rates
    visibly separate.
- Command: `dotnet list src/CommandoWar.Sim/... package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only; no `ProjectReference`;
    `CommandoWar.Sim.deps.json` libraries = `CommandoWar.Sim`, `FSharp.Core`.
- Source search in `src/CommandoWar.Sim` and `src/CommandoWar.Headless` for
  `godot|monogame|raylib|mibo|Vector2|Node2D|_PhysicsProcess|System.Drawing|Tiled`:
  no matches (outside `obj/` NuGet metadata, which records unrelated version
  constraints from FSharp.Core's graph, not references).
- Content-edit exercise (docs/06 section 12), performed on `scenes/Greybox.tscn`
  then reverted: moved one cover wall, added a 7th `FriendlySpawn` marker,
  changed `SeedText` 20260902 -> 777. One file touched, zero code, zero manual
  conversion steps. Edit -> visible authoritative hash via headless relaunch:
  ~0.38 s (warm import cache; 3 runs 397/377/384 ms). Seed 777 gave a distinct
  deterministic result (`0xFA8699E031761975`). Invalid follow-up (spawn moved
  onto the relocated wall) -> exit 2,
  `FriendlySpawn #6 ('Friendly6') at (14,9) lies on an impassable (wall) cell`.
  Revert -> `MATCH 0x838D3AE7DBFB735D`.

### Evidence

- **Godot / SDK:** Godot `4.7.2.stable.mono.official.ed1daf0bf`; .NET SDK
  `10.0.303`; `Godot.NET.Sdk` / `GodotSharp` / `Godot.SourceGenerators` `4.7.2`;
  client TFM `net10.0`.
- **Shared fixture:** 32x32, seed `20260902` (`0x0000000001352826`),
  `Setup.sixAgentWorld` (6 friendly, column 0, rows 0..5), command tick 1 /
  agent 3 -> (20,14) / `CommandId 1`, 40 ticks. Initial hash
  `0xF2F3DF0D820AD9AC`. Final hash `0x838D3AE7DBFB735D`. Full table in
  `content/fixtures/SPIKE-FIXTURE.md`. Godot host and `cwheadless` produce the
  identical 41-hash sequence.
- **Screenshot:** `docs/evidence/task-004-godot-overlay.png`.
- **Setup/build/run/package commands:** in `src/CommandoWar.Client.Godot/README.md`.
- **Host-specific files:** `src/CommandoWar.Client.Godot/` (16 tracked files).
  Approximate glue surface: `SimFacade.cs` ~150 lines (the entire
  sim boundary), `SpikeContent.cs` ~170 (DTOs + validation, reusable by the
  Mibo spike), `GreyboxScene.cs` + `SpikeMarker.cs` ~80 (Godot import
  boundary), `MainNode.cs` ~330 (scheduling + input + iso render + overlay).
- **Packaging blocker (exact):** `--export-release "Windows Desktop"` fails with
  `No export template found at the expected path:
  C:/Users/Dave/AppData/Roaming/Godot/export_templates/4.7.2.stable.mono/windows_release_x86_64.exe`
  (and `windows_debug_x86_64.exe`). `%APPDATA%\Godot\export_templates\` is
  empty. `--export-pack` produces a 37 KB resource `.pck` but a standalone run
  segfaults because the .NET assemblies are not embedded without a real export.
  Unblock: install the `4.7.2.stable.mono` export templates (editor Template
  Manager or drop the `.tpz` contents into that folder), then re-run
  `--export-release`. The app was demonstrated running **outside the editor
  UI** via `<godot> --path <project>` (game loop, Vulkan render, screenshot).

### Deviations and unresolved issues

- **Packaging: full self-contained executable not produced** (export templates
  absent). Documented above; acceptance criterion allows the documented
  blocker. "Launches outside the editor" satisfied by the non-editor game run.
- **Visual TileMap editor not exercised interactively.** This session cannot
  drive the Godot editor GUI, so the greybox was authored as text (`.tscn` is a
  text format) and the content-edit exercise was measured via headless
  relaunch (~0.38 s), not editor hot-reload / F5. Godot's live scene reload and
  inspector drag-editing are a real strength this measurement understates; the
  headless number is a lower bound on iteration friction (editor round-trip
  would add window focus + reload, still interactive).
- **Interactive mouse input not literally exercised** (headless session). The
  click handler (`HandleClick` -> `_sim.QueueMove` -> `Command.moveTo`) shares
  the exact path that `--selfcheck` verifies end to end (typed command ->
  accepted -> deterministic state change -> hash), and the on-screen control
  hints render (screenshot).
- **Enemies deferred.** Adding an `EnemySpawn` to the canonical greybox would
  add a `Hostile` agent and break fixture-hash parity, so the canonical scene
  has friendly spawns + objective only. `SimFacade` already maps `EnemySpawn`
  markers to `Side.Hostile` agents for a later scene.
- **`net10.0` client vs Godot's `net8.0` template default** - forced by the F#
  reference; builds and runs clean, but is off the Godot beaten path.
- Godot's `SceneTree.Quit(code)` only honours the exit code once the main loop
  iterates, so the headless exit is deferred one `_Process` frame.
- `PROJECT_STATE.yaml`: `active_work.selected_task` = `TASK-004`; gates,
  `current_gate`, and `framework_decision` unchanged (Dave's to advance on
  acceptance). ADR-0001 remains `proposed`; only the Godot column of its
  evidence table + a spike-results section were filled.
- Clean build run in the working tree (existing `bin/`/`obj/`/`.godot/`), not a
  fresh clone.

### Documents updated

- `tasks/TASK-004-GODOT-SPIKE.md` (status `active -> review`, acceptance
  criteria checked with evidence, completion notes)
- `tasks/TASK-003-DETERMINISM-HARNESS.md` (status `review -> done`)
- `docs/11_BACKLOG.md` (TASK-003 `review -> done`, TASK-004
  `ready after dependency -> active -> review`)
- `decisions/ADR-0001-FRAMEWORK-SELECTION.md` (Godot column of the evidence
  table; new "TASK-004 Godot spike results" section; decision still `TBD`)
- `PROJECT_STATE.yaml` (`active_work` -> TASK-004)
- `CommandoWar.slnx` (added `CommandoWar.Headless`)
- `content/fixtures/` (new), `docs/evidence/` (new),
  `src/CommandoWar.Client.Godot/` (new), `src/CommandoWar.Headless/` (new)
- `docs/12_PROGRESS_LEDGER.md` (this entry; TASK-003 review finalised)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-02)
- Notes: Godot spike accepted. Backlog row `review -> done`, task file status
  `done`. Gates, `current_gate`, and `framework_decision` unchanged; ADR-0001
  stays `proposed` / `TBD`. TASK-005 activated for a separate session (see the
  TASK-005 entries below).

## 2026-09-02 - TASK-005 - Environment check: current Mibo cannot satisfy the Mibo.Adaptive prohibition; task rescoped

**Owner:** Dave with coding-agent assistance
**Source revision:** `f363174` (Add deterministic random/hash/replay harness); working tree with TASK-004 spike present
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; NuGet queried at api.nuget.org on 2026-09-02
**Status change:** none (TASK-005 stays `ready after dependency`; not started)

### What was done

Pre-implementation environment check only. No Mibo host was created. No
simulation, test, or shared-fixture file was changed.

### Finding

Current stable Mibo cannot be used within TASK-005's constraints. `Mibo.Core`
takes a hard package and assembly dependency on the prohibited `Mibo.Adaptive`
from version 4.2.0 (2026-08-11) onward, and the classic-MVU `Program` /
`HeadlessProgram` / `HeadlessRunner` types live in `Mibo.Core`, so no
classic-MVU Mibo host can be built without `Mibo.Adaptive` being restored,
shipped, and referenced. This violates the task's forbidden scope
("Mibo.Adaptive (any package or API)"), the acceptance criterion ("no
Mibo.Adaptive package or API appears"), and ADR-0003. It fires the ADR-0003
review trigger "Mibo introduces a breaking change before TASK-005 begins".

### Verification

- Command: `curl api.nuget.org/v3-flatcontainer/mibo.core/{version}/mibo.core.nuspec`
  - Result: 4.0.0 and 4.1.0 depend on `FSharp.Core` + `FSharp.UMX` only; 4.2.0
    through 4.5.3 add `Mibo.Adaptive` (1.0.0, then 1.0.1 from 4.5.1) to the
    net8.0 and net10.0 dependency groups. Latest `Mibo.Core` / `Mibo.Raylib` /
    `Mibo.MonoGame` = 4.5.3; `Mibo.Adaptive` = 1.0.1.
- Command: throwaway `net10.0` F# exe referencing `Mibo.Raylib` 4.5.3, `dotnet
  restore` + `dotnet build -c Release`
  - Result: `Build succeeded, 0 Error(s)`. Warning NU1605 (FSharp.Core
    10.1.400 -> 10.1.303 downgrade). Output contains `Mibo.Adaptive.dll`;
    `*.deps.json` lists `"Mibo.Adaptive/1.0.1"`; `dotnet list package
    --include-transitive` lists `Mibo.Adaptive 1.0.1`.
- Command: reflection over the restored `Mibo.Core.dll` 4.5.3
  - Result: referenced assemblies include `Mibo.Adaptive`; namespaces include
    `Mibo.Adaptive` and `<StartupCode$Mibo-Core>.$Adaptive`.
- Command: throwaway `net10.0` F# exe referencing `Mibo.Raylib` 4.1.0
  - Result: `Build succeeded, 0 Error(s)`, no NU1605, no `Mibo.Adaptive.dll` in
    output. Reflection confirms `Mibo.Elmish.HeadlessProgram` /
    `Mibo.Elmish.HeadlessRunner` present. `Mibo.Raylib` 4.1.0 -> `Mibo.Core`
    4.1.0, `FSharp.Core >= 10.1.302`, `FSharp.UMX` 1.1.0, `Raylib-cs` 8.0.0.
- Mibo CHANGELOG 4.2.0 entry: "the Mibo integration
  (`AdaptiveProgram`/`AdaptiveHeadless`) lives in Mibo.Core". `Mibo.Adaptive`
  1.0.1 release note: "chained derived maps no longer freeze after certain read
  and write orders".

### Decision

- `decisions/ADR-0003-MIBO-ADOPTION.md`: added the "2026-09-02 amendment". The
  production prohibition on Mibo.Adaptive is unchanged and now makes the current
  Mibo release line (>= 4.2.0) not production-eligible before G5. For the
  disposable TASK-005 spike only, Mibo is pinned to 4.1.0 (last release with no
  `Mibo.Adaptive` dependency); the prohibition is preserved by version pinning,
  not package exclusion.
- `tasks/TASK-005-MIBO-SPIKE.md`: rewritten and trimmed (M -> S). Removed Tiled
  authoring + importer, self-contained packaging, and the MonoGame backend
  smoke test. Kept the decision-relevant core: a Mibo classic-MVU headless
  self-check reproducing the shared fixture's 41-hash sequence, a windowed
  raylib host rendering six agents with a tick/hash overlay and the move
  command, fixed-step vs render-rate separation, one invalid-content failure, a
  deliberate-mismatch non-zero exit, and an honest host line count next to the
  Godot spike's ~410.
- `docs/11_BACKLOG.md`: TASK-005 row size `M -> S`, dependencies now
  `TASK-004, ADR-0003 amendment`.
- Not raw MonoGame: `Mibo.MonoGame` also depends on `Mibo.Core` and hits the
  same coupling; raw MonoGame means owning the application-shell gap that
  `docs/02` already reasoned against. If the Mibo spike does not show a clear
  F#-ergonomics win, the route is Godot.

### Evidence

- Probe projects under the session scratchpad (not committed).
- ADR-0003 amendment section; rewritten `tasks/TASK-005-MIBO-SPIKE.md`.

### Deviations and unresolved issues

- The session precondition (mark TASK-004 accepted/done, flip TASK-005 to
  active, update `PROJECT_STATE.yaml`) was not performed: TASK-004 acceptance is
  Dave's review call and TASK-005 hit this blocker before host work.
  `PROJECT_STATE.yaml` is unchanged; `active_work.selected_task` is still
  TASK-004.
- The 4.1.0 spike measures Mibo ergonomics and application-shell cost only. The
  dependency-and-maintenance-risk question is already answered against Mibo by
  this finding, independent of the spike result, and belongs in the ADR-0001
  Mibo column.
- ADR-0001 remains `proposed`, `Selected candidate: TBD`. No evidence column was
  filled by this entry.

### Documents updated

- `decisions/ADR-0003-MIBO-ADOPTION.md` (2026-09-02 amendment)
- `tasks/TASK-005-MIBO-SPIKE.md` (rewritten, trimmed)
- `docs/11_BACKLOG.md` (TASK-005 row)
- `docs/12_PROGRESS_LEDGER.md` (this entry)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-02)
- Notes: ADR-0003 amendment and trimmed TASK-005 scope signed off. TASK-004
  accepted in the same review. TASK-005 activated in `PROJECT_STATE.yaml`;
  implementation follows in the next entry. ADR-0001 is not decided.

## 2026-09-02 - TASK-005 - Disposable Mibo + raylib framework spike (trimmed, pinned to Mibo 4.1.0)

**Owner:** Dave with coding-agent assistance
**Source revision:** `f363174` (Add deterministic random/hash/replay harness); working tree with the TASK-004 Godot spike present
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303 (`dotnet --version` = 10.0.303); raylib/OpenGL windowed run on the local display
**Status change:** `active -> review`

### Changes

**Framework-neutral / shared:** none. `CommandoWar.Sim`, `CommandoWar.Sim.Tests`,
`CommandoWar.Headless`, `content/fixtures/`, and `CommandoWar.slnx` are unchanged
(`git status` confirms; `CommandoWar.Sim.dll` md5 `82418ffbdf316b2f2e5124105b6f9e5e`
is identical in the library build and the host output).

**Mibo spike (own `src/CommandoWar.Client.Mibo/CommandoWar.Client.Mibo.slnx`,
deliberately NOT in `CommandoWar.slnx`):**

- F# `net10.0` exe (`AssemblyName` `cwmibo`), `RuntimeIdentifier` `win-x64`.
  `PackageReference` `Mibo.Raylib` `4.1.0` (pulls `Mibo.Core` 4.1.0,
  `Raylib-cs` 8.0.0, `FSharp.UMX` 1.1.0). `ProjectReference` to
  `CommandoWar.Sim` and `CommandoWar.Headless` (the latter for the shared
  `Fixture` constants).
- `Content.fs` (192 lines) - framework-neutral `.cwmap` v1 parser + validator.
  No Mibo / raylib / Tiled / `CommandoWar.Sim` types. Errors name the line
  (parse) or the marker/kind/cell/reason (validate), all in one pass.
- `SimBridge.fs` (117 lines) - the only module that opens `CommandoWar.Sim`.
  `Sim` owns one `WorldState`, advances it with `Simulation.step`, exposes
  `[<Struct>]` value views (`AgentView`, `TickInfo`) and an accepted-command log
  in `cwheadless` format. Never catches a step exception; never mutates
  authoritative state.
- `Program.fs` (403 lines) - `--selfcheck` headless path (Mibo classic
  `HeadlessProgram` / `HeadlessRunner`, explicit-dispatch and `withFixedStep`
  variants) + the windowed raylib classic-MVU host (`Program.mkProgram` /
  `RaylibGame`, `withFixedStep` authoritative cadence, `withTick` render
  measurement, `Mouse`/`Keyboard` subscriptions, command-buffer iso render,
  tick/hash overlay, `--screenshot` mode). CLI: `--selfcheck [--expect [0x..]]
  [--fixedstep] [--invalid]`, `--screenshot <path>`, `--invalid`.
- `content/greybox.cwmap` - authored 32x32 iso greybox reproducing
  `Setup.sixAgentWorld { 32; 32 } 20260902` (6 friendly spawns col 0 rows 0-5,
  1 objective, a wall block + scattered cover). `content/greybox-invalid.cwmap`
  - three seeded errors (spawn out of bounds, spawn on a wall, duplicate id,
  missing objective).
- `README.md`, `.gitignore` (`bin/`, `obj/`, `mibo-shot.png`).
- `docs/evidence/task-005-mibo-overlay.png` - self-captured overlay screenshot
  (tick 24, hash `0x08879506597DB88D`).

### Design decisions

- **Pinned to Mibo 4.1.0** per the ADR-0003 2026-09-02 amendment: 4.2.0+
  `Mibo.Core` hard-depends on the prohibited `Mibo.Adaptive`. 4.1.0 restores
  clean on SDK 10.0.303 / `net10.0` (no `FSharp.Core` downgrade) and keeps the
  classic `Mibo.Elmish.HeadlessProgram` / `HeadlessRunner` surface.
- **The canonical self-check drives the sim by explicit `Advance` dispatch**
  (one message -> exactly one `Simulation.step`), so the hash sequence has zero
  dependence on timing. A separate `--fixedstep` path drives the same 40 ticks
  through `Program.withFixedStep` + `StepUntil` (virtual time) to show the
  framework's fixed-step facility reaches the identical authoritative result;
  `StepUntil` avoids float32 accumulator under/overshoot.
- **`SimBridge.Sim` is a stateful object held by reference in the Mibo model.**
  It encapsulates the `WorldState`; the model holds a handle, not authoritative
  state (ADR-0002). This mirrors the Godot spike's `SimFacade`.
- **Windowed host renders exact authoritative cells** (no view interpolation) -
  sufficient for inspection; sim-vs-render separation is shown by the overlay
  numbers and the once-per-tick agent motion against a 60 fps counter.
- **Terrain is client-only** (`CommandoWar.Sim` has no terrain model, B-008):
  authored, validated against, rendered, never sent across the boundary.

### Verification

- Command: `dotnet test CommandoWar.slnx -c Release` (before and after host work)
  - Result: `Passed! - Failed: 0, Passed: 54, Skipped: 0, Total: 54`. The Mibo
    host is not in `CommandoWar.slnx`; the suite stays framework-neutral.
- Command: `dotnet build src/CommandoWar.Client.Mibo/CommandoWar.Client.Mibo.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (clean `obj/`/`bin/`).
- Command: `dotnet run --project src/CommandoWar.Client.Mibo -c Release -- --selfcheck --expect`
  - Result: initial hash `0xF2F3DF0D820AD9AC`; per-tick `tick=N hash=0x...` for
    ticks 1..40; tick 1 `0xC848D905A9CAD13F`, tick 14 `0x04343D056D0BAC45`,
    tick 31 `0x25315447F9D0E230`, tick 40 `0x838D3AE7DBFB735D` (format 1);
    `draws=0`; `accepted-command-log: 1 3 move 20 14`; `MATCH`; exit 0.
- Command: `diff <(cwheadless fixture hashes) <(mibo --selfcheck hashes)`
  - Result: no difference across the initial state + ticks 1..40.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- replay content/fixtures/spike-fixture.cwlog`
  - Result: tick 1 `0xC848D905A9CAD13F`, tick 31 `0x25315447F9D0E230`, final
    `0x838D3AE7DBFB735D`, `events: 33` - identical to the Mibo host.
- Command: `... -- --selfcheck --fixedstep --expect`
  - Result: `fixed-step: 40 authoritative ticks, final tick=40 hash=0x838D3AE7DBFB735D`;
    `MATCH`; exit 0.
- Command: `... -- --selfcheck --expect 0xDEADBEEFDEADBEEF`
  - Result: `MISMATCH expected 0xDEADBEEFDEADBEEF, got 0x838D3AE7DBFB735D`; exit 1.
- Command: `... -- --selfcheck --invalid`
  - Result: exit 2; `CONTENT LOAD FAILED (greybox-invalid.cwmap):` then
    `FriendlySpawn #2 at (40,3) lies outside the 32x32 grid`,
    `FriendlySpawn #4 at (0,10) lies on an impassable (wall) cell`,
    `FriendlySpawn #3 at (0,5) reuses id 3, already used by FriendlySpawn at (0,4)`,
    `no 'objective' marker: the scenario has no objective`. No `tick=` lines.
- Command: `... -- --screenshot <abs>.png`
  - Result: real raylib/OpenGL window opens (INFO log clean, default font
    loaded, audio device initialised), `TakeScreenshot` at frame 240 = tick 24,
    hash `0x08879506597DB88D` (matches fixture tick 24), `Cmd.signalExit` quits
    the same frame (exit 0, no deferred-quit quirk). Overlay:
    `docs/evidence/task-005-mibo-overlay.png` - tick / state hash + format /
    random draws 0 / sim 6 Hz fixed, 6 steps/s measured / render 60 fps.
- Command: `dotnet publish src/CommandoWar.Client.Mibo -c Release -r win-x64 --self-contained -o bin/publish`
  - Result: succeeds; `bin/publish` ~84 MB; no `Mibo.Adaptive.dll`. Running
    `bin/publish/cwmibo.exe --selfcheck --expect` from `C:\...\Temp\mibopub`
    (unrelated cwd): `MATCH ... 0x838D3AE7DBFB735D`, exit 0.
- Command: `dotnet list src/CommandoWar.Client.Mibo/... package --include-transitive`
  - Result: `Mibo.Raylib 4.1.0`, `Mibo.Core 4.1.0`, `Raylib-cs 8.0.0`,
    `FSharp.UMX 1.1.0`. **No `Mibo.Adaptive`.** `cwmibo.deps.json` has no
    `adaptive` library.
- Command: `dotnet list src/CommandoWar.Sim/... package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only; no `ProjectReference`;
    `CommandoWar.Sim.deps.json` libraries = `CommandoWar.Sim`, `FSharp.Core`.
- Command: source scan of `src/CommandoWar.Sim` + `src/CommandoWar.Headless` for
  `mibo|raylib|tiled|monogame|godot|Vector2|System.Drawing`
  - Result: two doc-comment lines in `Fixture.fs` ("framework spikes (TASK-004
    Godot, TASK-005 Mibo)"); no type or API.
- Content-edit exercise (`docs/06` section 12), on `content/greybox.cwmap`,
  reverted: changed `seed 20260902 -> 777` (1 file, 1 line, 0 code, 0
  conversion) -> edit-to-visible authoritative hash ~1.2 s warm (incremental
  build + content copy + 40-tick headless); seed 777 gave a distinct
  deterministic final hash `0x7D44ABDD084C1A25`. Broke one marker (moved a
  friendly spawn onto an authored wall) -> exit 2,
  `FriendlySpawn #4 at (6,10) lies on an impassable (wall) cell`. Revert ->
  `MATCH 0x838D3AE7DBFB735D`. Tiled editor and Mibo hot-reload NOT exercised
  (see deviations).

### Evidence

- **Versions:** SDK `10.0.303`; `net10.0`; `Mibo.Core` / `Mibo.Raylib` `4.1.0`;
  `Raylib-cs` `8.0.0`; `FSharp.UMX` `1.1.0`. 4.1.0 pinned because
  `Mibo.Core` >= 4.2.0 hard-depends on the prohibited `Mibo.Adaptive` (ADR-0003
  2026-09-02 amendment).
- **Shared fixture reproduced:** initial `0xF2F3DF0D820AD9AC`, ticks 1..40
  identical to `cwheadless fixture`, final `0x838D3AE7DBFB735D`, 33 events,
  random draws 0, agent 3 at (20,14) from tick 31. Both the explicit-dispatch
  and Mibo `withFixedStep` self-check paths.
- **Screenshot:** `docs/evidence/task-005-mibo-overlay.png` (tick 24, hash
  `0x08879506597DB88D`).
- **Host-specific files:** `src/CommandoWar.Client.Mibo/` (8 tracked files).
  Source: `Content.fs` 192, `SimBridge.fs` 117, `Program.fs` 403 (712 total F#).
  vs Godot `SimFacade.cs` ~150 + `SpikeContent.cs` ~170 +
  `GreyboxScene.cs`/`SpikeMarker.cs` ~80 + `MainNode.cs` ~330 (~730). Comparable
  total; Mibo's host layer is a little larger (verbose command-buffer view,
  hand-rolled self-check MVU program), all in one language.
- **Setup/build/run/publish commands:** `src/CommandoWar.Client.Mibo/README.md`.
- **Mibo API discovery:** the docs site (`program.md`, `headless.md`) is thin;
  the working API was recovered from reflection dumps of `Mibo.Core.dll` /
  `Mibo.Raylib.dll` 4.1.0, `dotnet new mibo-2d`, and the repo's
  `src/Mibo.Core.Tests/HeadlessTests.fs`.

### Deviations and unresolved issues

- **Trimmed scope (per the ADR-0003 amendment):** no Tiled authoring/importer,
  no self-contained-packaging *requirement* (done anyway, and it works), no
  MonoGame backend smoke test. Re-add only if a later ADR puts Mibo back in
  contention.
- **Tiled editor and Mibo content hot-reload NOT exercised.** Tiled is out of
  scope; the map is a hand-edited `.cwmap` text file; Mibo has no content
  hot-reload (edit -> relaunch). TASK-004 likewise did not exercise the Godot
  editor GUI, so the authoring-tool comparison is still open on both sides.
- **Interactive mouse input not literally exercised** (headless session). The
  `LeftClick` handler shares the exact `SimBridge.Sim.QueueMove ->
  Command.moveTo` path that `--selfcheck` verifies end to end (typed command ->
  accepted -> deterministic state change -> hash).
- **`withFixedStep` rate is fixed at program construction.** Runtime sim-rate
  adjustment (the Godot spike's `1`/`2` keys) would need a host-owned
  accumulator instead.
- **Referencing the F# `CommandoWar.Headless` exe project** for `Fixture`
  constants also copies `cwheadless.exe` into the Mibo host's publish output.
  Cosmetic; production would extract fixture constants to a library.
- **The dependency-risk driver is answered against Mibo** independent of this
  spike's ergonomic results: current stable Mibo is unusable under the
  prohibition, the pin is already stale, and the release cadence is very high.
- `PROJECT_STATE.yaml`: `active_work.selected_task` = `TASK-005`; gates,
  `current_gate`, and `framework_decision` unchanged. ADR-0001 remains
  `proposed`, `Selected candidate: TBD`; only the Mibo evidence column and a
  spike-results section were filled.
- Clean build run in the working tree (deleted `bin/`/`obj/`), not a fresh clone.

### Documents updated

- `tasks/TASK-005-MIBO-SPIKE.md` (status `active -> review`, acceptance criteria
  checked with evidence, completion notes)
- `docs/11_BACKLOG.md` (TASK-005 row `active -> review`)
- `decisions/ADR-0001-FRAMEWORK-SELECTION.md` (Mibo column of the evidence
  table; new "TASK-005 Mibo spike results" section; decision still `TBD`)
- `src/CommandoWar.Client.Mibo/` (new), `docs/evidence/task-005-mibo-overlay.png`
  (new)
- `docs/12_PROGRESS_LEDGER.md` (this entry)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-03)
- Notes: Mibo spike accepted as evidence. Backlog row `review -> done`, task file
  status `done`. Gates, `current_gate`, and `framework_decision` unchanged;
  ADR-0001 stays `proposed` / `TBD`. TASK-006 activated for the framework
  decision. ADR-0001 is not decided by this task.

## 2026-09-03 - TASK-004 interactive addendum - Godot editor GUI still not drivable in-session

**Owner:** Dave with coding-agent assistance
**Source revision:** `aa48abc` (Add Mibo framework spike and evidence)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; Godot
`4.7.2.stable.mono.official.ed1daf0bf` at
`C:\Users\Dave\Documents\GitHub\Godot_v4.7.2-stable_mono_win64\`; terminal
session, no interactive display
**Status change:** none (TASK-004 stays `done`)

### Why this entry exists

TASK-006 requires the `docs/06` section 12 content-edit exercise to be run
**interactively in the Godot editor** (visual TileMap edit, inspector marker
add, sim-property change, scene hot-reload / F5 / debugger, invalid-edit
failure) so that Godot's core claimed advantage - editor iteration speed - is
measured rather than asserted. The TASK-004 session recorded this as not done
(headless session, understating Godot).

### What was attempted and what happened

- `Godot_..._console.exe --editor --headless --quit --path src/CommandoWar.Client.Godot`
  -> exit 0. The editor imports the spike project, loads global class names,
  verifies GDExtensions, initialises plugins, loads the editor layout, and quits
  cleanly. **New evidence:** the spike project opens in the 4.7.2 editor with no
  import or script error.
- Driving the editor GUI itself (painting a TileMap cell, dragging a
  `Marker2D` in the inspector, editing an exported property, triggering scene
  hot-reload, pressing F5, stepping in the debugger, watching a live remote
  scene tree) requires a human at an interactive display. It cannot be done
  from this terminal session, exactly as in the TASK-004 session.
- `--export-release "Windows Desktop"` now fails earlier than in TASK-004:
  `This project doesn't have an export_presets.cfg file at its root` (the
  TASK-004 preset was transient and never committed). The underlying packaging
  blocker is unchanged: `%APPDATA%\Godot\export_templates\4.7.2.stable.mono\`
  exists but is empty, so no self-contained export can complete regardless of
  the preset.

### Decision on the evidence gap

Per Dave's instruction for this session: do not block indefinitely on the
editor-GUI measurement; accept the decision on the evidence in hand with the
authoring-tool comparison recorded as a known limitation, since the direction of
the conclusion is not in doubt. The ADR-0001 evidence-table cells for Godot
content authoring are therefore **left as "editor hot-reload / inspector
drag-edit NOT measured this session (no GUI)"**; they are not upgraded to a
measured claim. The ADR-0001 scored table docks Godot's content-authoring driver
0.5 for this, and adds a review trigger (B-025 must record a real editor
edit->visible-result measurement).

### Documents updated

- `decisions/ADR-0001-FRAMEWORK-SELECTION.md` (scored driver table notes; review
  trigger 1)
- `docs/12_PROGRESS_LEDGER.md` (this entry)

## 2026-09-03 - TASK-006 - Framework spikes evaluated; ADR-0001 decision matrix completed and put to Dave

**Owner:** Dave with coding-agent assistance
**Source revision:** `aa48abc` (Add Mibo framework spike and evidence)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303 (`dotnet --version` =
10.0.303); Godot `4.7.2.stable.mono.official.ed1daf0bf`; raylib/OpenGL windowed
run on the local display
**Status change:** TASK-005 `review -> done`; TASK-006 `proposed -> active`;
ADR-0001 unchanged (`proposed`, `Selected candidate: TBD`)

### What was done

- **Precondition:** accepted TASK-005 as evidence (backlog `review -> done`,
  task file `done`, ledger review `pending -> yes (2026-09-03)`); activated
  TASK-006 (backlog `-> active`, task file `proposed -> active`,
  `PROJECT_STATE.yaml active_work` -> TASK-006). Gates, `current_gate`,
  `framework_decision`, and ADR-0001 status left unchanged - those move only on
  Dave's acceptance of the decision evidence in this session.
- Confirmed both spikes met equivalent minimum behaviour and recorded every
  non-equivalent dimension (see below).
- Completed the ADR-0001 scored decision-driver table using the predeclared
  weights (unchanged). Weighted result: **Godot 4.13, Mibo 3.62.**
- Filled the ADR-0001 "Qualitative decision" block as a **recommendation pending
  acceptance** (Godot), with decisive evidence, the honest case for Mibo and the
  condition under which it would win, accepted weaknesses, the Mibo removal
  plan, review triggers, and pinned versions.
- Re-verified both hosts in this session (commands and results below).

### Equivalence check

Both spikes proved, against the byte-identical `CommandoWar.Sim.dll`: six agents
from snapshots on a 32x32 isometric greybox; one typed move command
(tick 1, agent 3 -> (20,14), `CommandId 1`) producing a deterministic state
change; fixed authoritative stepping visibly independent of render rate;
on-screen tick / state-hash overlay; one actionable invalid-content failure that
does not start the sim; and the exact 41-hash fixture sequence (initial
`0xF2F3DF0D820AD9AC`, final `0x838D3AE7DBFB735D`, 33 events, 0 random draws).

Not equivalent, recorded in the ADR:

- **Authoring tool:** Godot has a native visual scene/TileMap/inspector editor
  (confirmed to open the project headless; GUI iteration still not driven in any
  session). Mibo has none; its map is a hand-edited `.cwmap` text file and Tiled
  was removed from scope. The authoring-tool comparison is text-edit vs
  text-edit on both sides.
- **Dependency baseline:** the Mibo host is pinned to `Mibo.Core` /
  `Mibo.Raylib` 4.1.0 because 4.2.0+ hard-couples to the prohibited
  `Mibo.Adaptive` (ADR-0003 2026-09-02 amendment). Godot is on current stable
  4.7.2.
- **Packaging:** Mibo produced a working self-contained `dotnet publish`; Godot
  self-contained export is blocked on absent export templates.

### Verification (this session)

- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed! - Failed: 0, Passed: 54, Skipped: 0, Total: 54`.
    Framework-neutral; neither client host is in `CommandoWar.slnx` (verified:
    the `.slnx` lists only `CommandoWar.Sim`, `CommandoWar.Headless`,
    `CommandoWar.Sim.Tests`).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  - Result: initial `0xF2F3DF0D820AD9AC`, final `0x838D3AE7DBFB735D` (format 1).
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Release`
  - Result: `0 Warning(s) 0 Error(s)`.
- Command: `dotnet build src/CommandoWar.Client.Mibo/CommandoWar.Client.Mibo.slnx -c Release`
  - Result: `0 Warning(s) 0 Error(s)`.
- Command: `<godot> --headless --path src/CommandoWar.Client.Godot -- --selfcheck --expect 0x838D3AE7DBFB735D`
  - Result: `tick=0 hash=0xF2F3DF0D820AD9AC` ... `tick=40 hash=0x838D3AE7DBFB735D`,
    `accepted-command-log: 1 3 move 20 14`, `MATCH`, exit 0.
- Command: `dotnet run --project src/CommandoWar.Client.Mibo -c Release -- --selfcheck --expect`
  - Result: identical 41-hash sequence, `MATCH`, exit 0.
- Command: `dotnet run --project src/CommandoWar.Client.Mibo -c Release -- --selfcheck --fixedstep --expect`
  - Result: `fixed-step: 40 authoritative ticks, final tick=40 hash=0x838D3AE7DBFB735D`,
    `MATCH`, exit 0.
- Command: `<godot> --headless --path src/CommandoWar.Client.Godot -- --selfcheck --invalid`
  - Result: `CONTENT LOAD FAILED` - 3 errors, each naming node / id / cell /
    reason; simulation not started; exit 2.
- Command: `dotnet run --project src/CommandoWar.Client.Mibo -c Release -- --selfcheck --invalid`
  - Result: `CONTENT LOAD FAILED (greybox-invalid.cwmap)` - 4 errors, each
    naming marker / kind / cell / reason; no `tick=` lines; exit 2.
- Command: `<godot> --path src/CommandoWar.Client.Godot -- --screenshot <tmp>.png`
  - Result: windowed run outside the editor, screenshot at tick 14 hash
    `0x04343D056D0BAC45` (matches fixture tick 14).
- Command: `dotnet run --project src/CommandoWar.Client.Mibo -c Release -- --screenshot <tmp>.png`
  - Result: real raylib/OpenGL window, screenshot at tick 24 hash
    `0x08879506597DB88D` (matches fixture tick 24), clean exit 0.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only; no `ProjectReference`.
- Command: `dotnet list src/CommandoWar.Client.Mibo/*.fsproj package --include-transitive`
  - Result: `Mibo.Raylib 4.1.0`, `Mibo.Core 4.1.0`, `Raylib-cs 8.0.0`,
    `FSharp.UMX 1.1.0`. No `Mibo.Adaptive`.
- Command: source scan of `src/CommandoWar.Sim` + `src/CommandoWar.Headless` for
  `godot|monogame|raylib|mibo|tiled|Vector2|Node2D|System.Drawing`
  - Result: two doc-comment lines in `Fixture.fs`; no type or API.
- Command: `<godot> --editor --headless --quit --path src/CommandoWar.Client.Godot`
  - Result: exit 0, project imports and editor layout loads with no error (see
    the TASK-004 interactive addendum).
- Manual check: `git status` shows only the intended doc edits
  (`PROJECT_STATE.yaml`, `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`,
  `tasks/TASK-005-*.md`, `tasks/TASK-006-*.md`, `decisions/ADR-0001-*.md`); no
  source, test, fixture, or spike file changed; transient screenshots cleaned
  up.

### Recommendation put to Dave

**Godot** (candidate A), weighted 4.13 vs Mibo 3.62. The project is
content- and UX-iteration-bound before rendering-bound; Godot leads the two
highest-weighted drivers (authoring 20, presentation 15) because it ships mature
integrated tooling the Mibo route must build as code; and Mibo has a confirmed
production-eligibility problem (the `Mibo.Adaptive` coupling) that Godot does
not. Mibo's real wins - no interop tax, native headless, out-of-box packaging,
comparable host code size - are genuine but lower-leverage. The recommendation
carries two accepted weaknesses with review triggers: Godot's editor iteration
loop is still unmeasured on this project, and self-contained packaging is
blocked on absent export templates.

### Deviations and unresolved issues

- The Godot editor-GUI content-edit exercise could not be driven in this session
  (no interactive display). Accepted as a known limitation on Dave's
  instruction; recorded in the TASK-004 interactive addendum and as ADR-0001
  review trigger 1. Not sealed as a measured result.
- Godot self-contained packaging remains blocked (export templates absent).
  Recorded as accepted weakness 2 with review trigger 2 and exact unblock steps
  in the TASK-004 ledger.
- ADR-0001 status, Selected candidate, `framework_decision`, `gates.G1`, and
  `current_gate` are **unchanged**. They move only after Dave accepts this
  evidence in this session. `docs/02_TECHNOLOGY_DECISION.md` provisional
  language is likewise unchanged pending acceptance.

### Documents updated

- `docs/11_BACKLOG.md` (TASK-005 `review -> done`; TASK-006 `-> active`)
- `tasks/TASK-005-MIBO-SPIKE.md` (`review -> done`)
- `tasks/TASK-006-FRAMEWORK-DECISION.md` (`proposed -> active`)
- `PROJECT_STATE.yaml` (`active_work` -> TASK-006; `updated` -> 2026-09-03)
- `decisions/ADR-0001-FRAMEWORK-SELECTION.md` (scored decision-driver table;
  "Qualitative decision" block filled as a recommendation pending acceptance;
  status and Selected candidate unchanged)
- `docs/12_PROGRESS_LEDGER.md` (this entry + the TASK-004 interactive addendum;
  TASK-005 review finalised)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-03)
- Notes: Dave accepted the Godot recommendation in-session ("if you think godot
  is the best bet lets go with that"), with the priority that the C#/F# boundary
  be kept low-impedance. Finalisation applied in the same session (see the
  addendum below). ADR-0001 is `accepted`; G1 passed.

## 2026-09-03 - TASK-006 finalisation - ADR-0001 accepted (Godot), G1 passed, Mibo route retired

**Owner:** Dave with coding-agent assistance
**Source revision:** `aa48abc` (Add Mibo framework spike and evidence)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303
**Status change:** ADR-0001 `proposed -> accepted`; `gates.G1_framework_selected`
`in_progress -> passed`; `current_gate` `G1 -> G2_deterministic_core_proven`;
`current_phase` `P1_framework_selection -> P2_deterministic_core`; TASK-006
`active -> done`

### Trigger

Dave accepted the TASK-006 decision matrix and the Godot recommendation in this
session, and named keeping the C#/F# boundary low-impedance as the priority for
follow-on work. `docs/08_ROADMAP_AND_GATES.md` section 4 G1 evidence is
satisfied: both candidates ran the same simulation build, rendered six agents on
an isometric map, submitted the same typed command against the same snapshot,
displayed tick and hash, the content-edit workflow was measured (editor-GUI
iteration recorded as a known limitation and review trigger), build / packaging /
debugger / glue-code observations are recorded, and ADR-0001 selects one route.

### Changes

- `decisions/ADR-0001-FRAMEWORK-SELECTION.md`: status `accepted`, Date accepted
  2026-09-03, `Selected candidate: A. Godot .NET plus F# simulation`,
  "Decision" section written, "Qualitative decision" block finalised (was a
  pending recommendation), a named follow-up added for the low-impedance C#/F#
  boundary, "Pinned versions" finalised, "Consequences after acceptance"
  outcomes recorded.
- `PROJECT_STATE.yaml`: `framework_decision:
  godot_dotnet_fsharp_sim (ADR-0001, accepted 2026-09-03)`;
  `gates.G1_framework_selected: passed`;
  `current_gate: G2_deterministic_core_proven`;
  `current_phase: P2_deterministic_core`; `active_work.selected_task: none`
  with a note pointing at the next-session prompt (leading candidate: the
  boundary-design task; then B-007 onward for G2).
- `docs/02_TECHNOLOGY_DECISION.md`: provisional "do not commit the production
  client yet" summary replaced with the recorded decision; the pre-spike
  analysis retained as context, marked non-authoritative.
- `decisions/ADR-0003-MIBO-ADOPTION.md`: status line updated; "2026-09-03 note:
  Mibo not adopted" section added (removal from default build/CI, headless stays
  on `cwheadless`, Mibo.Adaptive prohibition unaffected, spike evidence
  preserved, reopening needs a new ADR).
- `src/CommandoWar.Client.Mibo/README.md`: "Rejected route (2026-09-03)" note
  at the top.
- `docs/10_RISK_REGISTER.md`: R-002 and R-005 `-> closed` (the controlled spike
  and the weighted evidence addressed the "language preference" and
  "engine-building" risks; Godot was R-005's own contingency); R-003
  (Mibo churn) `-> closed` (Mibo is no longer a project dependency); R-004
  (Godot C#/F# boundary friction) `-> mitigating` with the boundary-design task
  named as mitigation and the Mibo contingency removed.
- `docs/11_BACKLOG.md`: TASK-006 `active -> done`.
- `tasks/TASK-006-FRAMEWORK-DECISION.md`: all acceptance criteria checked;
  status `done`.

### Verification

- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed! - Failed: 0, Passed: 54` (unchanged; documentation-only
    session, no source touched).
- Command: `git status --porcelain`
  - Result: only documentation and the Mibo README changed; no source, test,
    fixture, `.slnx`, or spike code file modified.
- Manual check: internal references in the edited docs resolve (ADR-0001 <->
  ADR-0002 <-> ADR-0003, `docs/02`, `docs/08` section 4, `PROJECT_STATE.yaml`,
  backlog, ledger). `framework_decision`, gate, and phase now agree across
  `PROJECT_STATE.yaml`, ADR-0001, `docs/02`, and this ledger.

### Deviations and unresolved issues

- **Godot self-contained packaging remains blocked** (absent `4.7.2.stable.mono`
  export templates; no committed `export_presets.cfg`). In-session attempt
  2026-09-03: `--export-release` stops at "no `export_presets.cfg`", and the
  template directory is present but empty, so a real export cannot complete
  regardless. Accepted weakness 2 in ADR-0001; review trigger 2; unblock steps
  in the TASK-004 ledger entry. The app runs outside the editor via `--path`.
- **The Godot editor-iteration loop is still unmeasured on this project.**
  Accepted weakness 1; review trigger 1 requires the first Bridgehead authoring
  task (B-025) to record a real editor edit->visible-result measurement.
- **The low-impedance C#/F# boundary is a named open design task**, to run
  before P4 client work. It may produce ADR-0004 or an ADR-0002 amendment. It
  does not reopen ADR-0001.
- The finalisation was applied on Dave's in-session verbal acceptance; the
  edits are documentation and are open to correction on review.
- No P2 task was activated. The next task is Dave's to select.

### Documents updated

- `decisions/ADR-0001-FRAMEWORK-SELECTION.md`, `decisions/ADR-0003-MIBO-ADOPTION.md`
- `docs/02_TECHNOLOGY_DECISION.md`, `docs/10_RISK_REGISTER.md`,
  `docs/11_BACKLOG.md`
- `PROJECT_STATE.yaml`
- `src/CommandoWar.Client.Mibo/README.md`
- `tasks/TASK-006-FRAMEWORK-DECISION.md`
- `docs/12_PROGRESS_LEDGER.md` (this entry)

### Review

- Reviewer: Dave
- Accepted: pending (finalisation edits; the framework decision itself is
  accepted)
- Notes: Next session continues with the low-impedance C#/F# Godot boundary.
  See the next-session prompt.

## 2026-09-03 - TASK-007 - Godot C#/F# client boundary designed; ADR-0004 proposed; disposable proof built

**Owner:** Dave with coding-agent assistance
**Source revision:** `ad45bf0` (Accept ADR-0001 and close framework selection)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303 (`dotnet --version` =
10.0.303); Godot `4.7.2.stable.mono.official.ed1daf0bf` at
`C:\Users\Dave\Documents\GitHub\Godot_v4.7.2-stable_mono_win64\` (not on PATH);
GPU NVIDIA RTX 5070 Ti, Vulkan 1.4.329 Forward+
**Status change:** TASK-007 `-> active -> review`; ADR-0004 created (`proposed`);
gates, `current_gate` (`G2`), `current_phase` (`P2`), `framework_decision`
unchanged

### What was done

- **Precondition applied:** `PROJECT_STATE.yaml active_work.selected_task:
  none -> TASK-007`, `task_file -> tasks/TASK-007-GODOT-FSHARP-BOUNDARY.md`, the
  `active_work.note` removed; gates / `current_gate` / `current_phase` /
  `framework_decision` left unchanged. Created
  `tasks/TASK-007-GODOT-FSHARP-BOUNDARY.md`. Added the backlog "Current work"
  row (TASK-007, P2, S, deps TASK-006).
- Built a disposable proof under `src/_scratch/godot-fsharp-boundary/` (**not**
  in any `.slnx`): a `Godot.NET.Sdk` 4.7.2 project with one C# shim
  (`src/MainShim.cs`) over an F# `Microsoft.NET.Sdk` library `ClientCore`
  (`Boundary.fs` = `[<CLIMutable>]` view records; `Host.fs` = `SimHost` +
  `ClientHost`, the client logic; `FSharpNode.fs` = an F# `Node2D` subclass,
  the question-1 experiment). `ClientCore` references `CommandoWar.Sim` +
  `CommandoWar.Headless` (`Fixture`) + the `GodotSharp` 4.7.2 package.
- Answered TASK-007 questions 1-5 with recorded evidence (below).
- Wrote `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` (`proposed`): a thin C#
  host over an F# client-core library - form 1, one generic `FSharpSceneHost`
  for the whole client (preferred, no per-scene C#); form 2, a per-scene shim
  only where typed inspector `[Export]` / `[Signal]` is needed. F# types are
  not scene entry points; per-concern split table; interop idiom; a "Myriad and
  the shim" section; ADR-0002 compliance check; four review triggers.
- Updated `docs/03_ARCHITECTURE.md` section 14 and `docs/10_RISK_REGISTER.md`
  R-004.

### Evidence - question 1 (can an F# type be a Godot node?)

- Command: `dotnet build ClientCore/ClientCore.fsproj -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)`. F# `type
    FSharpHostNode() = inherit Godot.Node2D()` with overrides for `_EnterTree`,
    `_Ready`, `_Notification`, `_Process`, `_UnhandledInput`, `_Draw` and a
    `[<Export>] member val Caption` compiles against `GodotSharp` 4.7.2 in a
    plain `Microsoft.NET.Sdk` library (no `Godot.NET.Sdk`, no source generator).
- Command: `<godot> --headless --path . -- --probe`
  - Result: `[shim] added F# FSharpHostNode child; IsInsideTree=True`;
    `[shim] fnode.HasMethod("_process")=False  GetScript.Obj=<CSharpScript#...>`.
    Across 6 engine `_Process` frames **no** `[fsharp-node]` lifecycle line
    printed (no `_EnterTree` / `_Ready` / `_Notification` / `_Process` /
    `_UnhandledInput` / `_Draw`). A direct C# call `fnode._Ready()` then ran the
    F# body: `[fsharp-node] _Ready #1  name=FSharpProbe  Caption=assigned-from-C#`
    and `[fsharp-node] GetPropertyList() caption entry: ABSENT`. `AddUserSignal`
    + `Connect` + `EmitSignal` round-trip worked
    (`[shim] received fsharp_pinged from F# node`). Exit 0.
  - Conclusion: Godot does not drive an F# node's lifecycle (its managed
    virtual dispatch is populated by `Godot.SourceGenerators`, which is
    C#-only); `[<Export>]` is invisible; editor attach / `[GlobalClass]` /
    hot-reload depend on the same generator and are therefore unavailable. F#
    types are not viable as scene entry points. The C# shim per scene is the
    answer.

### Evidence - questions 2-4 (shim, per-concern split, interop idiom)

- Command: `dotnet build GodotFSharpBoundary.csproj -c Debug` (Godot's `--path`
  game run loads the Debug assembly; one `--editor --headless --quit` import
  pass was needed first)
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (also `-c Release`).
- Command: `<godot> --headless --path . -- --selfcheck --expect 0x838D3AE7DBFB735D`
  - Result: `tick=0 hash=0xF2F3DF0D820AD9AC` ... `tick=40
    hash=0x838D3AE7DBFB735D`, `tick 14 = 0x04343D056D0BAC45`, `tick 24 =
    0x08879506597DB88D`, `tick 31 = 0x25315447F9D0E230`,
    `accepted-command-log: 1 3 move 20 14`, `MATCH expected final hash
    0x838D3AE7DBFB735D`, exit 0. All stepping, the fixture command, and the
    self-check body are in F# (`ClientCore/Host.fs`); the C# shim only parsed
    args and made one call `ClientHost.SelfCheck(expect)`.
- Command: `<godot> --headless --path . -- --selfcheck --expect 0xDEADBEEFDEADBEEF`
  - Result: `MISMATCH expected 0xDEADBEEFDEADBEEF, got 0x838D3AE7DBFB735D`,
    exit 1.
- Command: `<godot> --path . -- --screenshot docs/evidence/task-007-godot-fsharp-boundary.png`
  - Result: windowed run (Vulkan/NVIDIA), `[shim] screenshot written ... (tick
    12, hash 0x981418895730F065)` (matches the self-check tick-12 line). The
    overlay in the still is `ClientHost.OverlayText` (F#).
- Interop idiom: putting `SimHost` in F# deletes the spike's
  `AgentIdModule.ofInt` / `World.create` via `ListModule.OfArray` /
  `FSharpOption<Cell>.get_IsSome`. C# sees `SimHost.Step() : TickView`,
  `SimHost.AgentViews : AgentView[]` (both `[<CLIMutable>]`),
  `ClientHost.SelfCheck(System.Nullable<uint64>)`. Per-concern C#/F# table in
  ADR-0004.
- Follow-up (Dave asked whether F# can be "more core" / whether Myriad helps):
  added `src/FSharpSceneHost.cs` (~35 lines, one generic `Node2D` for the whole
  client) + `ClientCore/Scene.fs` (`IClientScene`, `FixtureSelfCheckScene`) +
  `scenes/SceneHost.tscn`. Command: `<godot> --headless --path .
  --main-scene res://scenes/SceneHost.tscn` -> `[fixture-scene] Ready`,
  `tick=0 hash=0xF2F3DF0D820AD9AC` ... `final tick=40
  hash=0x838D3AE7DBFB735D`, `MATCH`, `[fixture-scene] ExitTree`, exit 0. The
  generic host resolved `CwClientCore.FixtureSelfCheckScene` from `[Export]
  SceneType` and forwarded lifecycle - **zero scene-specific C#**. ADR-0004
  "The split" now lists this as form 1 (preferred); a per-scene shim (form 2)
  only where typed inspector `[Export]` / `[Signal]` is genuinely needed.
- Follow-up 2 (Dave asked: can F# hold the members + `[Export]` / `[Signal]` /
  `[GlobalClass]` intent, with Myriad emitting only the C# forwarder?): added
  `ClientCore/NodeLogic.fs` (`PatrolMarkerLogic`, `[<GodotExport>]` /
  `[<GodotSignal>]` markers) + `src/GeneratedStyleNode.cs` (hand-written to be
  exactly what such a Myriad plugin would emit: `[Export]` properties + a
  `[Signal]` delegate forwarding to a composed F# instance; `[GlobalClass]`).
  Command: `<godot> --headless --path . -- --forward-test` ->
  `[patrol-logic] OnReady #1  Waypoints=7  Label=north-ridge`;
  `Waypoints in property list: True`; `Label in property list: True`;
  `HasSignal(PatrolCompleted): True`; `received PatrolCompleted(3)`;
  `set Waypoints=7 via property -> F# logic reads 7`. Godot's own generator
  builds the full bridge over a pure-forwarding C# stub; the inspector /
  `.tscn` round-trip works; the Myriad plugin would never touch
  `godot_variant` / `NativeVariantPtrArgs`. ADR-0004 "Myriad and the shim"
  option 1 is now **proven feasible** with this evidence; option 2 (reimplement
  Godot's generators in F#) stays a research project pinned to the engine
  interop ABI.

### Evidence - question 5 (debugging)

- Command: `<godot> --headless --path . -- --trace-test` (raises
  `System.Exception` in F# `ClientHost.ForceFSharpFault`, caught in C#
  `MainShim._Ready`, `ex.ToString()` printed)
  - Result:
    ```
    System.Exception: deliberate fault raised inside ClientHost (F#)
       at <StartupCode$ClientCore>.$Host.inner@170.Invoke(Unit unitVar0) in ...\ClientCore\Host.fs:line 170
       at CwClientCore.ClientHost.ForceFSharpFault() in ...\ClientCore\Host.fs:line 171
       at MainShim._Ready() in ...\src\MainShim.cs:line 58
    ```
    C#->F# stack traces stay readable with exact F# file/line; closure frames
    carry a compiler-mangled name but the source location is precise. Portable
    PDBs for `ClientCore.dll` / `CommandoWar.Sim.dll` are in Godot's output.
- Interactive F# breakpoint from an editor / F5-launched run: **not verified**
  (no interactive debugger in-session). Godot's C# debugger is IL-level SDB;
  F# emits IL with sequence points and portable PDBs, so F# breakpoints should
  behave as C# ones do. ADR-0004 review trigger 1.

### Verification

- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed! - Failed: 0, Passed: 54` before and after (no source,
    test, or fixture file under `CommandoWar.Sim` / `tests/` changed).
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only.
- Command: source scan of `src/CommandoWar.Sim` + `src/CommandoWar.Headless` for
  `godot|node2d|vector2|_process|monogame|raylib`
  - Result: one doc-comment line in `Fixture.fs`; no type or API.
- Command: `git status --porcelain`
  - Result: modified `PROJECT_STATE.yaml`, `docs/03_ARCHITECTURE.md`,
    `docs/10_RISK_REGISTER.md`, `docs/11_BACKLOG.md`,
    `docs/12_PROGRESS_LEDGER.md`; new `decisions/ADR-0004-*.md`,
    `tasks/TASK-007-*.md`, `docs/evidence/task-007-godot-fsharp-boundary.png`,
    `src/_scratch/godot-fsharp-boundary/`. No change under `src/CommandoWar.Sim`,
    `src/CommandoWar.Headless`, `tests/`, the retained spikes, `content/`, or
    `CommandoWar.slnx`.

### Deviations and unresolved issues

- Godot's `--path` game run loads the **Debug** assembly, not Release; the
  first headless run needs a prior `--editor --headless --quit` import pass.
  Recorded in the proof README.
- Interactive Godot editor GUI and interactive debugger are not drivable in
  this session (same limitation class as TASK-004 / TASK-006). Question 1 is
  answered from a headless load + code inspection; the F# breakpoint check is
  an ADR-0004 review trigger, not a sealed result.
- The proof's screenshot projection is crude (agents overlap at the origin). It
  is evidence of the pattern, not a rendering result.
- ADR-0004 is `proposed`. `PROJECT_STATE.yaml` gates, `current_gate`,
  `current_phase`, and `framework_decision` are unchanged - this task does not
  move the gate. `active_work.selected_task` stays `TASK-007` pending review.
- No P4 client task (B-024+) was started. The TASK-004 spike's `SimFacade.cs` /
  `SpikeContent.cs` are not yet ported to F#; ADR-0004 says B-024 / B-026 do
  that.

### Documents updated

- `tasks/TASK-007-GODOT-FSHARP-BOUNDARY.md` (new; status `review`, criteria
  checked, completion notes)
- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` (new, `proposed`)
- `docs/03_ARCHITECTURE.md` (section 14 rewritten; header revision date)
- `docs/10_RISK_REGISTER.md` (R-004 mitigation)
- `docs/11_BACKLOG.md` (TASK-007 row added, `active -> review`)
- `PROJECT_STATE.yaml` (`active_work` -> TASK-007; note removed)
- `src/_scratch/godot-fsharp-boundary/` (new, disposable, not in any `.slnx`;
  includes follow-up 1 `FSharpSceneHost.cs` / `Scene.fs` / `SceneHost.tscn` and
  follow-up 2 `NodeLogic.fs` / `GeneratedStyleNode.cs`)
- `docs/evidence/task-007-godot-fsharp-boundary.png` (new)
- `docs/12_PROGRESS_LEDGER.md` (this entry)

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: ADR-0004 is proposed; the disposable proof and question 1-5 evidence
  are for review. The F# breakpoint check and the C# shim hot-reload check are
  deferred to the first P4 client task as ADR-0004 review triggers.
