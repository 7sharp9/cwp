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
- Accepted: pending
- Notes: Do not activate TASK-004 or TASK-005 without Dave's review.
