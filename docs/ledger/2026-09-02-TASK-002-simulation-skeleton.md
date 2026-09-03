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
