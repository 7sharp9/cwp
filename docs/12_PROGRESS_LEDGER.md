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

### Documents updated

- `tasks/TASK-002-SIMULATION-SKELETON.md` (status `review`, acceptance criteria checked with evidence, completion notes)
- `docs/11_BACKLOG.md` (TASK-002 row `active -> review`)
- `docs/12_PROGRESS_LEDGER.md` (this entry)

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: TASK-003 must not be activated without Dave's review.
