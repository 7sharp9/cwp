## 2026-09-06 - TASK-021 - Minimum passable movement cost and overflow-safe pathfinding cost

**Owner:** Dave with coding-agent assistance
**Source revision:** `bb02990` (Revise TASK-020 and draft TASK-021), plus this
session's uncommitted control-plane work (TASK-022 / TASK-023 drafts, tooling
note)
**Environment:** Windows 11 Pro 26200 (25H2); .NET SDK 10.0.303
**Status change:** `tasks/TASK-021-MOVEMENT-COST-BOUNDS.md` `ready -> done`
(pending Dave's acceptance); `docs/11_BACKLOG.md` TASK-021 row + B-046
`ready -> done`; `PROJECT_STATE.yaml` `active_work.selected_task` `none ->
TASK-021`. No `Canonical.FormatVersion` change (stays `2`); no committed hash
moved.

### Diagnosis

Two pathfinding-correctness holes reachable from currently-valid authored
content, both confirmed against source:

1. `Scenario.validate` rejected only `MoveCost < 0` for a terrain cell. A
   passable cell with `MoveCost = 0` (or any value in `[0, BaseMoveCost)`)
   validated. `Pathfinding.find`'s heuristic `Manhattan * Terrain.BaseMoveCost`
   is admissible/consistent only when every step costs `>= BaseMoveCost`, and
   the no-reopening A* (`if not closed.[ni]`) needs consistency for an optimal
   path. A free passable corridor could therefore make `find` return a
   non-lowest-cost path. The module comment already *asserted* the precondition
   ("authored passable cells respect this") and it was false.
2. `Pathfinding` accumulates `g + Terrain.moveCost` in unchecked `int` with no
   authored upper bound on `MoveCost`. A passable cell authored at
   `System.Int32.MaxValue` makes `Terrain.moveCost` return exactly the
   `Terrain.BlockedCost` sentinel for a passable cell; a long path over large
   costs can overflow `int32`.

### Approach

The minimum-cost model, not a zeroed heuristic (task Central decisions): a
"passable cell that costs nothing to enter" has no coherent meaning in this
model and a zeroed heuristic also degrades A* to unguided search (R-010) and
leaves the `Simulation.wouldComplete` sub-cell threshold degenerate. Bounding
passable cost on *both* sides fixes hole 1 (floor) and hole 2 (ceiling below
the sentinel, accumulation inside `int32`) with no algorithm change.

### Changes

- `src/CommandoWar.Sim/Terrain.fs`: new `[<Literal>] MaxMoveCost = 1000`
  (`1000 * Width * Height < Int32.MaxValue` for any grid up to ~1460 cells on
  a side, and `< BlockedCost`). Doc on `MaxMoveCost`, the `Terrain.MoveCost`
  field, and the `moveCost` query recording the `[BaseMoveCost, MaxMoveCost]`
  passable range.
- `src/CommandoWar.Sim/Scenario.fs`: new `ScenarioError` case
  `MoveCostOutOfRange of cell: Cell * cost: int * min: int * max: int`.
  `Scenario.validate`'s terrain-cell loop now reports it for a **passable**
  authored cell whose `MoveCost` is `>= 0` but `< Terrain.BaseMoveCost` or
  `> Terrain.MaxMoveCost`. `NegativeMoveCost` is kept, unchanged, as the more
  specific message for `MoveCost < 0` (any class). An impassable cell's
  `MoveCost` is still ignored and never range-checked. `RawTerrainCell` doc
  updated.
- `src/CommandoWar.Sim/Pathfinding.fs`: **comment only.** The `## Algorithm`
  heuristic bullet now states admissibility *and consistency* and cites the
  new validation rule as what enforces the `>= BaseMoveCost` precondition; a
  new `## Bounded work` "No cost overflow" paragraph states the bound
  `Width * Height * Terrain.MaxMoveCost < Int32.MaxValue`. No code change: no
  algorithm change, no defensive guard (unreachable for validated terrain, so
  it would be an untestable branch).
- `tests/CommandoWar.Sim.Tests/ScenarioTests.fs`: +5 facts — passable cost
  below `BaseMoveCost` rejected naming the cell; passable cost above
  `MaxMoveCost` rejected naming the cell; passable costs of exactly
  `BaseMoveCost` and exactly `MaxMoveCost` both validate; an impassable cell
  with an out-of-range cost still validates (cost ignored, `moveCost` reports
  `BlockedCost`); the standing review's zero-cost corridor grid is rejected
  (one `MoveCostOutOfRange` per corridor cell).
- `tests/CommandoWar.Sim.Tests/PathfindingTests.fs`: +1 fact — a one-cell-wide
  corridor of `MaxMoveCost` cells, route cost `7 * MaxMoveCost = 7000`, exact
  and inside `int32` (exercises the `## Bounded work` bound).
- `tests/CommandoWar.Sim.Tests/DeterminismPropertyTests.fs`: +1 FsCheck
  property (`MaxTest = 200`) — `Pathfinding.find`'s returned `cost` equals an
  independent relaxation-to-fixed-point (Bellman-Ford-style) shortest-path
  search over terrain whose passable costs span `[BaseMoveCost, MaxMoveCost]`
  including a near-ceiling band; `find` returns `Found` exactly when the
  reference finds a path (the default `Width * Height` cap is never the
  limiting factor on the `<= 7x7` generated grids). This is strictly stronger
  than property 3, which only recomputes the returned path's own cost.
- `docs/04_SIMULATION_SPEC.md` section 8, `docs/06_CONTENT_AND_PRESENTATION.md`
  section 4, `docs/09_TEST_STRATEGY.md` section 2.2: passable move-cost range
  and the new property recorded.
- Control docs: this file, the `docs/12` index row + "Green tests" pinned fact
  (`171 -> 178`), `docs/11` rows, `PROJECT_STATE.yaml`, the task file.

### Verification

All from the repository root.

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (all four projects).
- Command: `dotnet test CommandoWar.slnx -c Release` (before)
  - Result: `Passed: 171, Failed: 0`.
- Command: `dotnet test CommandoWar.slnx -c Release --no-build` (after)
  - Result: `Passed: 178, Failed: 0` (+7; the new property ran at 200 cases).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --no-build -- corpus`
  - Result: `OK - all 5 entries match their committed tables`, exit `0`
    (`spike-fixture`, `wall-detour`, `blocked-goal`, `converging-routes`,
    `slow-terrain` all `PASS`).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --no-build -- fixture`
  - Result: agent 3 at `(20,14)`, `events: 33`, exit `0`.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  - Result: `FSharp.Core` only (see Evidence).
- Manual check: grep every authored terrain layer in the repository for a
  passable `MoveCost` outside `[1, 1000]`.
  - Result: authored passable costs are `1` (`LosDemo`, `DemoScenario` ridge /
    opaque wall), `3` (`Corpus` `slow-terrain`, `DemoScenario` cost patch),
    and `5` (`PathDemo` cost patch). Impassable cells author `MoveCost = 0`,
    which is ignored. No committed scenario, terrain layer, or `.cwlog`
    violates the new range; nothing to re-author, no hash to re-pin.
- Manual check: source scan of `src/CommandoWar.Sim` for forbidden
  dependencies / `DateTime` / `System.Random` / floating point in the new
  code.
  - Result: clean; the additions are an `int` literal, one DU case, one
    integer comparison, and comments.

### Evidence

- Chosen ceiling `Terrain.MaxMoveCost = 1000`. Non-overflow arithmetic:
  `Pathfinding` closes each of `Width * Height` cells at most once, each
  passable step costs `<= 1000`, so every `g` / `tentative` is
  `<= Width * Height * 1000`. `int64 1000 * 1460 * 1460 = 2_131_600_000 <
  2_147_483_647 = Int32.MaxValue`, and `1000 < Int32.MaxValue = BlockedCost`.
  Desktop tactical maps are far below 1460 on a side.
- `dotnet list ... package --include-transitive` (CommandoWar.Sim):
  `FSharp.Core  10.1.303  10.1.303` — no other package, transitive or direct.
- `dotnet test` after: `Passed!  - Failed: 0, Passed: 178, Skipped: 0,
  Total: 178, Duration: 2 s`.
- Corpus / fixture hashes: unchanged. `content/replays/*.md` and
  `content/fixtures/SPIKE-FIXTURE.md` are byte-identical (the `corpus` verb
  compares per-tick hash, tick count, and event count and reported `OK`);
  `git status --porcelain` shows no `content/` file modified.

### Acceptance criteria

- Passable cost `0` and passable cost `> MaxMoveCost` each rejected with one
  actionable `MoveCostOutOfRange` naming the cell; impassable out-of-range
  cost still accepted — met (`ScenarioTests.fs`).
- Zero-cost-corridor regression grid rejected — met (`ScenarioTests.fs`,
  "the zero-cost corridor grid the standing review found is rejected").
- Property (>= 200 cases) comparing `find`'s cost to an independent
  shortest-path search over valid-range terrain, `Found` iff the reference
  finds a path — met (`DeterminismPropertyTests.fs`, property 4).
- `Terrain.moveCost` never `>= BlockedCost` for a passable cell; `Pathfinding`
  accumulation cannot exceed `Int32.MaxValue` for validated terrain, stated as
  the bound in the `Pathfinding` comment and exercised by a near-ceiling
  long-path test — met (`Terrain.fs` doc + validation ceiling;
  `PathfindingTests.fs` near-ceiling fact).
- No committed scenario / terrain layer / `.cwlog` authors an out-of-range
  passable cost (grep evidence above); `-- corpus` / `-- fixture` reproduce
  committed hashes with no `--regenerate` — met.
- Every pre-existing `PathfindingTests` / `ScenarioTests` /
  `DeterminismPropertyTests` / `CorpusTests` / `ReplayTests` / `FixtureTests`
  fact passes unmodified — met (only new facts added; `171` of `178` are the
  prior suite).
- `dotnet build` `0/0`; `CommandoWar.Sim` transitive packages = `FSharp.Core`
  only; source scan clean — met.
- `Canonical.FormatVersion` unchanged (`2`); no committed hash moved;
  `ScenarioContent.Version` unchanged (`2`) — met (this is a stricter check on
  existing version-2 data, not a new authored field).

### Deviations and unresolved issues

- `NegativeMoveCost` was **kept**, not folded into `MoveCostOutOfRange` (the
  task left this to the implementer). Keeping it is the smaller change and
  preserves the existing `a negative move cost is reported` fact unmodified,
  as acceptance criterion 6 requires. `NegativeMoveCost` still fires for a
  negative cost on a cell of any class; `MoveCostOutOfRange` fires only for a
  passable cell with a non-negative but out-of-range cost. Every bad value
  yields exactly one error naming the cell.
- No defensive per-step overflow guard was added to `Pathfinding`. For
  validated terrain the documented bound already guarantees no overflow, so a
  guard would be a branch normal inputs never reach and could not be tested
  without constructing an already-invalid `Terrain` directly. The task
  permits either choice; the bound-in-a-comment route is the smaller one.
- `Scenario.validate` still places no upper bound on `raw.Width` / `raw.Height`
  themselves (pre-existing). For grids beyond ~1460 on a side, `Width * Height`
  and hence accumulation could still overflow. No scenario approaches this and
  it is out of this task's scope; if a map-size cap is ever wanted it is a
  separate one-line validation add.
- `DeterminismPropertyTests.terrainGen` (properties 1-3) still draws passable
  costs from `{1, 2, 3}` only and was left unchanged; property 4 uses its own
  wider generator.

### Documents updated

- `tasks/TASK-021-MOVEMENT-COST-BOUNDS.md` (status + evidence)
- `docs/11_BACKLOG.md` (TASK-021 row + B-046 `ready -> done`)
- `docs/12_PROGRESS_LEDGER.md` (index row; "Green tests" `171 -> 178`)
- `docs/04_SIMULATION_SPEC.md` section 8, `docs/06_CONTENT_AND_PRESENTATION.md`
  section 4, `docs/09_TEST_STRATEGY.md` section 2.2
- `PROJECT_STATE.yaml` (`active_work.selected_task`, `note`, `updated`)
- this file
- no ADR (this enforces an existing implicit contract; it decides no new
  architecture, framework, determinism contract, or gate)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-06)
- Notes: single headless session; no GitHub round-trip. Acceptance recorded
  2026-09-06 alongside TASK-022 and TASK-023
  (`docs/ledger/2026-09-06-reconcile-021-022-023-acceptances.md`).
  `NegativeMoveCost` kept alongside `MoveCostOutOfRange` as the implementer
  chose; the map-area upper-bound and `int64` path-cost questions from the
  standing review of `07af43a` remain untriaged backlog candidates.
