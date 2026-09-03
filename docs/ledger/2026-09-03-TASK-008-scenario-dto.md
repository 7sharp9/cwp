## 2026-09-03 - TASK-008 - Authored scenario model, content version, and one-pass validation implemented

**Owner:** Dave with coding-agent assistance
**Source revision:** `9c73b38` (Add F# scene host follow-up proof)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303 (`dotnet --version` = 10.0.303)
**Status change:** `active -> review`

### Changes

- New F# module `src/CommandoWar.Sim/Scenario.fs` (compile order: after
  `Commands.fs`, before `Events.fs`):
  - Content ids in the `Ids.fs` idiom (`[<Struct>]`, private constructor,
    smart constructor that rejects malformed input): `ScenarioId` (non-blank
    string), `ObjectiveId` (non-negative int), `AreaId` / `TargetId`
    (non-blank string), each with `ofString` / `ofInt` and `value`.
  - `module ScenarioContent` with `[<Literal>] Version = 1` - the
    authored-content-format version, independent of `Canonical.FormatVersion`
    and `Replay.FormatVersion` (it versions the authored shape and the
    validation contract, not the state encoding).
  - Validated model: `Deployment { Agent; Side; Cell }`, `Area { Id; Cell }`,
    `StaticTarget { Id; Cell }`, `AgentSelection` (`AllFriendlyAgents` /
    `SpecificAgents of AgentId[]`), `Objective` (`ReachArea` / `HoldArea` /
    `DestroyTarget` / `ExtractAgents` / `AllOf` / `Optional`; **evaluation
    deferred, data only - comment says so**), `ScenarioRules
    { FailOnFriendlyForceEliminated }`, `Scenario { Id; Map: GridBounds;
    FriendlyDeployments; EnemyDeployments; ObjectiveAreas; ExtractionAreas;
    StaticTargets; Objectives; Rules }`.
  - Raw (unvalidated) input: `RawDeployment { AgentId; Cell }`,
    `RawArea { AreaId; Cell }`, `RawTarget { TargetId; Cell }`,
    `RawObjective { Id; Kind; AreaRef; TargetRef; HoldTicks; ExtractAgentIds;
    IsOptional }`, `RawScenario { ContentVersion; Id; Width; Height;
    FriendlyDeployments; EnemyDeployments; ObjectiveAreas; ExtractionAreas;
    StaticTargets; Objectives; FailOnFriendlyForceEliminated }`. Every field
    is a primitive or an array of primitives.
  - `ScenarioError` - 20 explicit cases in the `ReplayError` style, each
    naming the offending object and the expected value:
    `UnsupportedContentVersion`, `BlankScenarioId`, `NonPositiveMapDimensions`,
    `DuplicateDeploymentId`, `NegativeDeploymentId`, `DeploymentOutOfMap`,
    `DeploymentCellShared`, `DuplicateObjectiveId`, `NegativeObjectiveId`,
    `DuplicateAreaId`, `DuplicateTargetId`, `BlankAreaId`, `BlankTargetId`,
    `AreaMarkerOutOfMap`, `TargetMarkerOutOfMap`, `UnknownObjectiveKind`,
    `ObjectiveReferencesMissingArea`, `ObjectiveReferencesMissingTarget`,
    `ExtractionSelectsUnknownAgent`, `MissingRequiredMarker`.
  - `Scenario.validate : RawScenario -> Result<Scenario, ScenarioError list>` -
    single pass, collects every fault into a `ResizeArray` and returns the
    whole list, or `Ok` with a `Scenario` in which every id, reference, and
    position is known good. No silent defaults.
- `src/CommandoWar.Sim/Simulation.fs` - `World.ofScenario : Scenario ->
  uint64 -> Result<WorldState, WorldError>` added beside `World.create`.
  Friendly then enemy deployments -> agents sorted ascending by id ->
  `World.create scenario.Map seed agents`. Reads only `Map` and the
  deployments; objectives / areas / targets / rules are not consumed
  (per-cell terrain B-008, LoS B-009, pathfinding B-010, objective evaluation
  B-032 all out of scope).
- `src/CommandoWar.Sim/CommandoWar.Sim.fsproj` - one new `<Compile>` entry
  (`Scenario.fs` after `Commands.fs`).
- `tests/CommandoWar.Sim.Tests/ScenarioTests.fs` (25 facts) + registered in
  the test `.fsproj`. No change to `CommandoWar.Sim`'s other modules,
  `CommandoWar.Headless`, `FixtureTests.fs`, `Canonical.fs`,
  `Setup.sixAgentWorld`, the client spikes, `src/_scratch`, or
  `CommandoWar.slnx`.

### Design decisions

- **`Scenario.fs` inside `CommandoWar.Sim`, no ADR.** `docs/03` section 8
  already lists `Scenario.fs` there and ADR-0002's arrows put "Content DTOs ->
  validation -> Sim setup" ahead of the sim. No decision is made that would
  need an ADR (the model does not need to leave the sim); a ledger note is the
  right instrument.
- **Content version is one bare `[<Literal>]`, deliberately separate from the
  canonical and replay versions.** The three version numbers happen to all be
  `1` today; they are independent constants and a bump to one does not imply a
  bump to the others.
- **`Map : GridBounds`, reusing the existing type.** Elevation is flat and
  per-cell terrain is B-008, so a dedicated `MapDefinition` would be an empty
  wrapper. A comment records why.
- **The `Objective` algebra is the documented six-case set (docs/06 s3), type
  only.** Nothing reads it; the slice's mission is the implicit all-of of the
  `Objectives` array, with `AllOf` / `Optional` available for later nested
  composition. `Optional` is preserved through validation.
- **One-pass validation, sort-based de-duplication.** Duplicate detection is
  `Array.sort |> Array.pairwise |> Array.choose`, not a hash set, so the error
  list order never depends on hash enumeration (docs/09 s3). `Set` is used for
  reference-existence lookups (F# `Set` is an ordered immutable tree).
- **Objects are built during the pass and only kept when fault-free.** When
  the error list is empty every objective and deployment was built, so the
  `Ok` branch's arrays are complete and the smart constructors in it are only
  called on already-validated inputs (they would otherwise `invalidArg` on a
  negative id or blank string).
- **`NegativeDeploymentId` / `NegativeObjectiveId` / `BlankAreaId` /
  `BlankTargetId` beyond the task's listed conditions.** These inputs would
  make the id smart constructors throw; a typed error is the "explicit failure
  mode, no silent default" answer (AGENTS.md).
- **Required markers: friendly deployment, objective, extraction area.** Not
  enemies - a friendly-only scenario (the six-agent fixture) must validate.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (`CommandoWar.Sim` has
    `TreatWarningsAsErrors=true`).
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed! - Failed: 0, Passed: 79, Skipped: 0, Total: 79`
    (54 pre-existing + 25 new).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  - Result: initial hash `0xF2F3DF0D820AD9AC`, final tick 40 hash
    `0x838D3AE7DBFB735D` (format 1), 33 events - unchanged.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only. No `ProjectReference`.
- Command: source scan of `src/CommandoWar.Sim` for
  `godot|mibo|monogame|raylib|DateTime|Stopwatch|System.Random` (case-insensitive)
  - Result: two doc-comment lines in `Scenario.fs` naming a future Godot
    `.tscn` content reader as prose; no type or API. `System.Random`,
    `DateTime`, `Stopwatch`: no matches.
- Command: `git status --porcelain`
  - Result: modified `PROJECT_STATE.yaml`, `decisions/ADR-0004-*.md`,
    `docs/04_SIMULATION_SPEC.md`, `docs/06_CONTENT_AND_PRESENTATION.md`,
    `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`,
    `src/CommandoWar.Sim/{CommandoWar.Sim.fsproj,Simulation.fs}`,
    `tasks/TASK-007-*.md`, `tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj`;
    new `src/CommandoWar.Sim/Scenario.fs`,
    `tasks/TASK-008-SCENARIO-DTO-AND-CONTENT-VERSION.md`,
    `tests/CommandoWar.Sim.Tests/ScenarioTests.fs`. Nothing under
    `CommandoWar.Headless`, `Canonical.fs`, `FixtureTests.fs`, the client
    spikes, `src/_scratch`, `content/`, or `CommandoWar.slnx`.

### Evidence

- **Content-format version:** `ScenarioContent.Version = 1`
  (`src/CommandoWar.Sim/Scenario.fs`), independent of `Canonical.FormatVersion
  = 1` and `Replay.FormatVersion = 1`.
- **Validator signature:** `Scenario.validate : RawScenario -> Result<Scenario,
  ScenarioError list>`; `World.ofScenario : Scenario -> uint64 ->
  Result<WorldState, WorldError>`.
- **Sample one-pass failure list** (`ScenarioTests`
  "validation reports every fault in one pass"): a raw scenario with
  `ContentVersion = 3`, an enemy re-using friendly id 1 at an out-of-map cell
  `(99,99)`, and an objective of kind `"orbit"` returns
  `[ UnsupportedContentVersion (3, 1); DuplicateDeploymentId 1;
  DeploymentOutOfMap (1, {X=99;Y=99}, {Width=16;Height=16});
  UnknownObjectiveKind (2, "orbit") ]` (and never an `Ok`).
- **Pinning test** (`ScenarioTests`): the six-agent shared fixture expressed as
  a `RawScenario` (6 friendly deployments at column 0, a token objective area
  and extraction area to satisfy required markers), validated, then
  `World.ofScenario ... 20260902` -> `Hashing.hash` = `0xF2F3DF0D820AD9AC`
  (identical to `Setup.sixAgentWorld` / `FixtureTests`), and stepped 40 ticks
  with the fixture command (agent 3 -> (20,14) at tick 1) -> `0x838D3AE7DBFB735D`.
  The token objective / area are not read by `World.ofScenario` and cannot
  affect the hash (`Canonical.encode` covers tick, bounds, random, agents
  only).
- **New/changed files:** `src/CommandoWar.Sim/Scenario.fs` (new),
  `src/CommandoWar.Sim/Simulation.fs` (`World.ofScenario`),
  `src/CommandoWar.Sim/CommandoWar.Sim.fsproj`,
  `tests/CommandoWar.Sim.Tests/ScenarioTests.fs` (new),
  `tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj`.

### Deviations and unresolved issues

- `Scenario` is a public record. Like the framework spikes' validated
  `Scenario` / `ScenarioDto`, the "construct only through `validate`"
  guarantee is a convention plus the fact that `validate` is the only exposed
  builder, not a private representation (a private record would block
  `World.ofScenario` and the tests, which are in other files).
- `ScenarioRules` has one field. It is the documented `Rules` slot (docs/06
  s3) with only the squad-loss failure condition the slice exercises; more
  rules are added when a task needs them.
- The validator uses `Array.countBy`-free de-duplication and F# `Set` for
  lookups. It is content-boundary code, runs once before the simulation, and
  its output does not participate in the state hash, so the
  hash-enumeration-order discipline is satisfied by construction (sorted
  de-dup, ordered `Set`).
- Objective evaluation, mission success/failure, the plant action, terrain,
  line of sight, and pathfinding are all out of scope (B-008 to B-010, B-032).
- `PROJECT_STATE.yaml`: `active_work.selected_task` set to `TASK-008`; gates,
  `current_gate` (`G2_deterministic_core_proven`), `current_phase`
  (`P2_deterministic_core`), and `framework_decision` unchanged.
- Clean build run in the working tree (existing `bin/`/`obj/`), not a fresh
  clone.

### Documents updated

- `tasks/TASK-008-SCENARIO-DTO-AND-CONTENT-VERSION.md` (new; status `active`,
  criteria to be checked on review)
- `docs/04_SIMULATION_SPEC.md` (new section 21, "Realised by TASK-008" note)
- `docs/06_CONTENT_AND_PRESENTATION.md` (section 3 realisation note)
- `docs/11_BACKLOG.md` (TASK-008 "Current work" row; B-007 `proposed -> active`
  with the task-file link)
- `PROJECT_STATE.yaml` (`active_work` -> TASK-008)
- `docs/12_PROGRESS_LEDGER.md` (this entry)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-03)
- Notes: additive scenario model + content version + one-pass validator +
  `World.ofScenario`; fixture hashes unchanged; no ADR (docs/03 s8 already
  places `Scenario.fs` in the sim). Accepted 2026-09-03: backlog TASK-008 /
  B-007 rows `review -> done`, task file `review -> done`; no ADR required.
