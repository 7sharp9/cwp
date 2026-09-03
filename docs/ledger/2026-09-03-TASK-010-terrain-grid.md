## 2026-09-03 - TASK-010 - Authoritative terrain grid implemented (data and queries, not consumed)

**Owner:** Dave with coding-agent assistance
**Source revision:** `94abf56` (Restructure progress ledger into index format)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303 (`dotnet --version` = 10.0.303)
**Status change:** `active -> review`

### Central decision: terrain and the canonical hash

`WorldState` gains `Terrain`, and `Canonical.encode` does **not** include it
while terrain carries no per-tick mutable state. Static authoritative data that
is a pure function of the validated scenario cannot diverge tick to tick;
hashing it every tick would move every pinned fixture hash and churn
`FixtureTests.fs`, `ScenarioTests.fs`, `content/fixtures/SPIKE-FIXTURE.md`, and
the two retained spike evidence sets for zero determinism benefit.
`Canonical.FormatVersion` stays `1`; it bumps and the canonical image gains a
terrain section when destructible terrain lands (B-019) or any phase otherwise
mutates terrain. Recorded in `decisions/ADR-0002` amendment "Static
authoritative data and the canonical image" and `docs/04` section 17. The
decision was not larger than an amendment, so implementation proceeded (the
task's stop-and-ask alternative was not taken).

### Changes

- **New `src/CommandoWar.Sim/Terrain.fs`** (compile order: after `Grid.fs`,
  before `Domain.fs`, so `WorldState` can reference it):
  - `Direction` (`North` / `East` / `South` / `West`) + `module Direction`
    (`all`, `index` 0..3). Four cardinals: placeholder movement is cardinal
    and line of sight, which would want eight, is B-009.
  - `MovementClass` (`Passable` / `Impassable`).
  - `AuthoredCell` / `AuthoredCover`: the checked hand-off shape
    `Scenario.validate` builds for `Terrain.build` (not authored content).
  - `Terrain { Bounds; Elevation: int[]; Movement: MovementClass[];
    MoveCost: int[]; Opaque: bool[]; Cover: int[] }` - dense row-major
    integer arrays indexed `y * Width + x`; `Cover` is `W*H*4`, indexed
    `cellIndex * 4 + Direction.index d`. No map keyed by `Cell`.
  - `Terrain.BaseMoveCost = 1`, `Terrain.BlockedCost = Int32.MaxValue`.
  - `Terrain.empty bounds` (flat, `Passable`, cost `BaseMoveCost`,
    transparent, uncovered) and `Terrain.build bounds cells cover` (contained
    mutable builder over freshly allocated arrays; ADR-0002 "Mutation
    policy"; does no validation - `Scenario.validate` guarantees clean
    input).
  - Queries, each total and bounds-checked: `elevation` (0 out of bounds),
    `passable` (false out of bounds), `moveCost` (`BlockedCost` out of bounds
    or on an impassable cell, authored cost otherwise), `opaque` (false out
    of bounds), `cover : Terrain -> Cell -> Direction -> int` (0 out of
    bounds or where no cover authored). Module comment states nothing in
    `Simulation.step` or any phase reads them yet (the TASK-008
    `Objective`-algebra precedent).
- **`src/CommandoWar.Sim/Domain.fs`** - `WorldState` gains `Terrain: Terrain`
  (after `Bounds`), with a comment that it is authored/queryable but not
  consumed and excluded from `Canonical.encode`.
- **`src/CommandoWar.Sim/Simulation.fs`**:
  - `World.create` / `World.ofScenario` refactored onto a shared private
    `build bounds terrain seed agents` core (the empty-grid / duplicate-id /
    in-bounds guards are unchanged).
  - `World.create` passes `Terrain.empty bounds`; existing callers and
    `Setup.sixAgentWorld` keep working and keep their hashes.
  - `World.ofScenario` passes `scenario.Terrain` (empty when the scenario
    authored no layer). Friendly-then-enemy deployment ordering unchanged.
  - `Canonical.encode` untouched.
- **`src/CommandoWar.Sim/Scenario.fs`**:
  - `ScenarioContent.Version` `1 -> 2`. Version 1 is rejected
    (`UnsupportedContentVersion(1, 2)`), not migrated (`docs/04` s16).
  - New raw types: `RawTerrainCell { Cell; Class; Elevation; MoveCost;
    Opaque }`, `RawCoverFeature { Cell; Direction; Level }`,
    `RawTerrainLayer { Width; Height; Cells; Cover }`.
  - `RawScenario` gains `TerrainLayer: RawTerrainLayer option`. `None` is
    legal and means empty terrain; enforced explicitly (the validator only
    builds a non-empty `Terrain` when a layer is present and fault-free).
  - `Scenario` gains `Terrain: Terrain` (built by `validate`;
    `Terrain.empty Map` when no layer).
  - `ScenarioError` 20 -> 30 cases: `TerrainLayerDimensionsMismatch`,
    `TerrainFeatureOutOfMap`, `DuplicateTerrainCell`, `DuplicateCoverFeature`,
    `UnknownTerrainClass`, `UnknownCoverClass`, `NegativeElevation`,
    `NegativeMoveCost`, `NegativeCoverLevel`, `DeploymentOnImpassableCell`.
  - `Scenario.validate` extended in the same one pass: sort-based duplicate
    detection (the existing `repeated` helper, no hash set), `parseMovementClass`
    / `parseDirection` token parsers, and the deployment-on-impassable check
    against the parsed impassable set. In the fault-free case the parse is
    reused to build the `Terrain` in the `Ok` branch.
- **`src/CommandoWar.Sim/CommandoWar.Sim.fsproj`** - one new `<Compile>`
  entry (`Terrain.fs` between `Grid.fs` and `Domain.fs`).
- **Tests**:
  - New `tests/CommandoWar.Sim.Tests/TerrainTests.fs` (9 facts): `Direction`
    order/index; empty terrain flat/passable/transparent/uncovered across the
    whole grid; each query on a hand-built terrain (in-bounds, out-of-bounds,
    impassable, elevated, opaque, covered-from-one-direction-only); adding
    terrain to a world leaves `Canonical.encode` and `Hashing.hash`
    unchanged and the fixture initial hash unmoved; the fixture still reaches
    both pinned hashes.
  - `tests/CommandoWar.Sim.Tests/ScenarioTests.fs` (+14 facts): version 2
    independent / version 1 rejected / unsupported version now `(99, 2)`;
    `goodTerrainLayer` helper; `TerrainLayer = None` -> empty terrain;
    well-formed layer -> populated `Terrain` (queried through the five
    functions); `World.ofScenario` carries terrain onto the world; one test
    per new `ScenarioError`; the one-pass test extended with three terrain
    faults.
  - `tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj` - `TerrainTests.fs`
    registered before `ScenarioTests.fs`.
- **Docs**: `decisions/ADR-0002` amendment; `docs/04` sections 7, 9, 17, 21
  realisation / decision notes; `docs/06` sections 3 and 4 realisation notes.
- **Control**: `tasks/TASK-010-TERRAIN-GRID.md` (new), `docs/11_BACKLOG.md`
  (TASK-010 "Current work" row `active`, B-008 `-> active` with the task-file
  link, TASK-009 `-> done`), `PROJECT_STATE.yaml` (`active_work` -> TASK-010),
  `docs/12_PROGRESS_LEDGER.md` (index rows + "Pinned facts"
  `ScenarioContent.Version` -> 2, Green tests -> 102), this entry.
- **TASK-009 finalised** (precondition): task file `review -> done`; backlog
  row `active -> done`; its ledger detail file Review `Accepted: pending ->
  yes (2026-09-03)` with a one-line note; the index row Accepted cell
  `pending -> yes (2026-09-03)`. Split verification not re-run (`git status`
  scope check only).
- **TASK-006 finalisation entry accepted**: `docs/ledger/2026-09-03-TASK-006-finalisation.md`
  Review `Accepted: pending (...) -> yes (2026-09-03)` with a one-line note;
  the `docs/12` index row Accepted cell changed likewise. No other past entry
  touched.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (`CommandoWar.Sim` has
    `TreatWarningsAsErrors=true`).
- Command: `dotnet test CommandoWar.slnx -c Release` (before)
  - Result: `Passed! - Failed: 0, Passed: 79, Skipped: 0, Total: 79`.
- Command: `dotnet test CommandoWar.slnx -c Release` (after)
  - Result: `Passed! - Failed: 0, Passed: 102, Skipped: 0, Total: 102`
    (79 + 9 TerrainTests + 14 ScenarioTests).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  - Result: initial hash `0xF2F3DF0D820AD9AC`, final tick 40 hash
    `0x838D3AE7DBFB735D` (format 1), 33 events - unchanged.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only. No transitive package, no
    `ProjectReference`.
- Command: source scan of `src/CommandoWar.Sim/*.fs` for
  `godot|mibo|monogame|raylib|DateTime|Stopwatch|System.Random|Dictionary|HashSet|groupBy`
  (case-insensitive)
  - Result: two pre-existing doc-comment lines in `Scenario.fs` naming a
    future Godot `.tscn` content reader as prose (unchanged from TASK-008); no
    type, API, or new match. `float` / `double` in `Terrain.fs`: one
    doc-comment occurrence of "floating point"; no floating-point value.
- Command: `git status --porcelain`
  - Result: modified `PROJECT_STATE.yaml`, `decisions/ADR-0002-SIMULATION-BOUNDARY.md`,
    `docs/04_SIMULATION_SPEC.md`, `docs/06_CONTENT_AND_PRESENTATION.md`,
    `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`,
    `docs/ledger/2026-09-03-TASK-006-finalisation.md`,
    `docs/ledger/2026-09-03-TASK-009-ledger-restructure.md`,
    `src/CommandoWar.Sim/{CommandoWar.Sim.fsproj,Domain.fs,Scenario.fs,Simulation.fs}`,
    `tasks/TASK-009-PROGRESS-LEDGER-RESTRUCTURE.md`,
    `tests/CommandoWar.Sim.Tests/{CommandoWar.Sim.Tests.fsproj,ScenarioTests.fs}`;
    new `src/CommandoWar.Sim/Terrain.fs`, `tasks/TASK-010-TERRAIN-GRID.md`,
    `tests/CommandoWar.Sim.Tests/TerrainTests.fs`,
    `docs/ledger/2026-09-03-TASK-010-terrain-grid.md`. Nothing under
    `content/`, the client spikes, `src/_scratch`, or `CommandoWar.slnx`;
    `Canonical.fs`, `FixtureTests.fs`, `Fixture.fs`, and `Setup.sixAgentWorld`
    untouched.

### Evidence

- **Content-format version:** `ScenarioContent.Version = 2`, independent of
  `Canonical.FormatVersion = 1` and `Replay.FormatVersion = 1`. Version 1
  input -> `UnsupportedContentVersion(1, 2)`.
- **`Terrain` shape:** `{ Bounds: GridBounds; Elevation: int[]; Movement:
  MovementClass[]; MoveCost: int[]; Opaque: bool[]; Cover: int[] }`, dense
  row-major, `Cover` length `Width*Height*4`.
- **Query contract:** `passable` / `opaque` false out of bounds; `elevation`
  0 out of bounds; `moveCost` = `Terrain.BlockedCost` (`Int32.MaxValue`) out
  of bounds or on an `Impassable` cell, authored cost otherwise; `cover` 0
  out of bounds or where unauthored.
- **New `ScenarioError` cases (10):** `TerrainLayerDimensionsMismatch`,
  `TerrainFeatureOutOfMap`, `DuplicateTerrainCell`, `DuplicateCoverFeature`,
  `UnknownTerrainClass`, `UnknownCoverClass`, `NegativeElevation`,
  `NegativeMoveCost`, `NegativeCoverLevel`, `DeploymentOnImpassableCell`.
  Total 30.
- **Sample one-pass failure list** (`ScenarioTests` "validation reports every
  fault in one pass"): version 3, an enemy re-using friendly id 1 at
  out-of-map `(99,99)`, an objective of kind `"orbit"`, plus a terrain layer
  with a cover feature at `(40,40)` direction `"up"` level `-1` returns (among
  others) `UnsupportedContentVersion(3, 2)`, `DuplicateDeploymentId 1`,
  `DeploymentOutOfMap(1, {X=99;Y=99}, ...)`, `UnknownObjectiveKind(2,
  "orbit")`, `TerrainFeatureOutOfMap({X=40;Y=40}, {Width=16;Height=16})`,
  `UnknownCoverClass({X=40;Y=40}, "up")`, `NegativeCoverLevel({X=40;Y=40},
  -1)` and never an `Ok`.
- **Hand-built terrain queries** (`TerrainTests`): `(2,2)` impassable ->
  `passable` false, `moveCost` `BlockedCost`; `(3,3)` elevation 4, cost 7,
  opaque; `(5,5)` cover 2 from `North` only, 0 from `East` / `South` / `West`;
  all queries at out-of-bounds cells return the documented default.
- **Canonical exclusion pin** (`TerrainTests` "adding terrain to a world does
  not change its canonical encoding or hash"):
  `Canonical.encode world = Canonical.encode { world with Terrain =
  handBuilt() }` (byte-equal) and `Hashing.hash` equal; the shared-fixture
  initial hash is `0xF2F3DF0D820AD9AC` either way.
- **Fixture pinning** (`FixtureTests` unchanged + `TerrainTests` /
  `ScenarioTests`): `Setup.sixAgentWorld` and the fixture `Scenario` (no
  terrain layer) still hash to `0xF2F3DF0D820AD9AC` (tick 0) and
  `0x838D3AE7DBFB735D` (tick 40).

### Deviations and unresolved issues

- **Duplicate detection beyond the task's listed conditions.**
  `DuplicateTerrainCell` and `DuplicateCoverFeature` were added (the task
  named "unknown terrain or cover class" and "a negative cost or level" but
  not duplicates). A validator whose purpose is "no silent anything" should
  not let a duplicated cell resolve to last-wins; this follows the TASK-008
  precedent of `NegativeDeploymentId` / `BlankAreaId` beyond that task's
  list.
- **`NegativeElevation` added.** The task said "a negative cost or level";
  "level" is ambiguous between elevation level and cover level, so all three
  (elevation, cost, cover level) are rejected when negative.
- **`Terrain` is a public record with mutable arrays.** Like the TASK-008
  `Scenario`, the "construct only through `Terrain.empty` / `Terrain.build`"
  guarantee is a convention plus the fact that those are the only exposed
  builders. `Terrain.build` mutates the arrays `empty` allocates, before the
  record is returned - a contained builder, not observable mutation (ADR-0002
  "Mutation policy").
- **`WorldState` now has structural equality over `Terrain` arrays.**
  `ReplayTests` "replay reconstructs ... final state" asserts
  `finalState = outcome.FinalState`; both derive from stepping the same
  `Setup.sixAgentWorld` initial state, whose terrain array object is carried
  by reference through `{ state with ... }`, so equality is unaffected. F#
  structural array equality would also make two separately-built empty grids
  equal.
- **`MovementClass` is two cases.** The slice needs only "can an agent enter
  this cell"; vehicles and multiple movement classes are out of the vertical
  slice (docs/07). More classes are added when a task needs them.
- **No consumer.** Line of sight (B-009), pathfinding (B-010), and movement
  (B-011) are the first readers; nothing in `Simulation.step` touches
  `Terrain` this task.
- Clean build run in the working tree (existing `bin/`/`obj/`), not a fresh
  clone.

### Documents updated

- `tasks/TASK-010-TERRAIN-GRID.md` (new; status `review`)
- `tasks/TASK-009-PROGRESS-LEDGER-RESTRUCTURE.md` (`review -> done`)
- `decisions/ADR-0002-SIMULATION-BOUNDARY.md` (amendment "Static authoritative
  data and the canonical image")
- `docs/04_SIMULATION_SPEC.md` (sections 7, 9, 17, 21)
- `docs/06_CONTENT_AND_PRESENTATION.md` (sections 3, 4)
- `docs/11_BACKLOG.md` (TASK-010 row; B-008 `-> active`; TASK-009 `-> done`)
- `docs/12_PROGRESS_LEDGER.md` ("Pinned facts": `ScenarioContent.Version` 2,
  Green tests 102; index rows for TASK-010, TASK-009 acceptance, TASK-006
  finalisation acceptance)
- `docs/ledger/2026-09-03-TASK-009-ledger-restructure.md` (Review accepted)
- `docs/ledger/2026-09-03-TASK-006-finalisation.md` (Review accepted)
- `PROJECT_STATE.yaml` (`active_work` -> TASK-010)
- `docs/12_PROGRESS_LEDGER.md` / this entry

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-04)
- Notes: additive terrain model + authored layer + one-pass validation +
  total bounds-checked queries; no tick phase consumes terrain;
  `Canonical.encode` and all five pinned files unmoved (ADR-0002 amendment
  records why); `ScenarioContent.Version` 2, version 1 rejected; 102 tests
  green. Accepted at TASK-012 start; `git status` scope check plus one
  `dotnet test` (`Passed: 115`) confirmed the tree still clean and green.
