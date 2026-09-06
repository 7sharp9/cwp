# TASK-021: Minimum passable movement cost and overflow-safe pathfinding cost

Status: ready
Owner: Dave
Phase: P3
Gate: G3 (corrects a G2 deliverable: terrain grid + pathfinding, TASK-010 / TASK-013)
Size: S

## Objective

Close two pathfinding-correctness holes in the deterministic core, both
reachable from currently-valid authored content:

1. **A passable cell with `MoveCost` below `Terrain.BaseMoveCost` is valid
   authored content today** (`Scenario.validate` only rejects `MoveCost < 0`).
   `Pathfinding.find`'s A* heuristic (`Manhattan * Terrain.BaseMoveCost`) is
   only admissible and consistent when every step costs at least
   `Terrain.BaseMoveCost`; the module comment asserts this precondition
   ("authored passable cells respect this") and it is false. A passable
   `MoveCost = 0` corridor can make `find` return a path that is not the
   lowest-cost path.
2. **`Pathfinding` accumulates `g + Terrain.moveCost` in unchecked `int` with
   no upper bound on authored `MoveCost`.** A long path over large authored
   costs can overflow `System.Int32`, and a passable cell authored at
   `System.Int32.MaxValue` makes `Terrain.moveCost` return exactly the
   `Terrain.BlockedCost` sentinel for a passable cell.

## Why this task exists

Risk R-009 (determinism claimed but breaks through numeric behaviour) and
R-010 (pathfinding dominates development). This is a correctness defect in a
G2 deliverable found after the gate passed, by the standing review. The
TASK-019 pathfinding property (`DeterminismPropertyTests.fs`) recomputes the
*returned* path's own cost, so it cannot detect a suboptimal path whose cost
is summed correctly; its generator (`cellClassGen`) only produces passable
costs of 1, 2, 3, so it never exercises the sub-`BaseMoveCost` case.

This must land before B-015 (perception) and B-019 (combat) build on movement
output. It does not depend on TASK-020 and can run before or after it.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `src/CommandoWar.Sim/Pathfinding.fs` (heuristic, `findWithin` cost
  accumulation, the `## Algorithm` / `## Bounded work` comments)
- `src/CommandoWar.Sim/Terrain.fs` (`BaseMoveCost`, `BlockedCost`, `moveCost`,
  `empty`, `build`)
- `src/CommandoWar.Sim/Scenario.fs` (`ScenarioError`, the terrain-layer
  validation loop around `NegativeMoveCost`, `RawTerrainCell`)
- `src/CommandoWar.Sim/Simulation.fs` (`wouldComplete`: the sub-cell
  `Progress` threshold `startProgress + Terrain.BaseMoveCost >= Terrain.moveCost next`)
- `tests/CommandoWar.Sim.Tests/PathfindingTests.fs`,
  `tests/CommandoWar.Sim.Tests/DeterminismPropertyTests.fs` (properties 3 and
  its `terrainGen` / `cellClassGen`), `tests/CommandoWar.Sim.Tests/ScenarioTests.fs`
- `docs/04_SIMULATION_SPEC.md` section 8, `docs/06_CONTENT_AND_PRESENTATION.md`
  section 4 (authored `MoveCost`)

## Dependencies

- none

## Central decisions

- **Passable `MoveCost` must lie in `[Terrain.BaseMoveCost, Terrain.MaxMoveCost]`.**
  A new `Terrain.MaxMoveCost` literal is added — a ceiling far below
  `BlockedCost` that keeps `Width * Height * MaxMoveCost` inside
  `System.Int32.MaxValue` for any grid this project will author. **Recommended
  value `1000`** (a single cell costing 1000x the base step is already an
  extreme "deep obstacle" value; `1000 * Width * Height` stays inside `int32`
  for grids up to ~1400 on a side, far beyond a desktop tactical map). The
  implementing agent may pick a different value with a one-line justification,
  but it must satisfy `int64 MaxMoveCost * int64 Width * int64 Height <
  int64 System.Int32.MaxValue` for the largest bounds `Scenario.validate`
  admits, and must be `< Terrain.BlockedCost`.

- **New typed validation error(s) on the passable-cost range.** Add
  `MoveCostOutOfRange of cell: Cell * cost: int * min: int * max: int` (one
  case carrying both bounds) to `ScenarioError`. `Scenario.validate` reports
  it for a **passable** authored cell whose `MoveCost` is `< Terrain.BaseMoveCost`
  or `> Terrain.MaxMoveCost`. `NegativeMoveCost` is kept for `MoveCost < 0` as
  the more specific message (report `NegativeMoveCost` for `< 0`,
  `MoveCostOutOfRange` for `0 <= cost < BaseMoveCost` or `cost > MaxMoveCost`);
  or, if cleaner in the validation loop, fold `NegativeMoveCost` into
  `MoveCostOutOfRange` and delete the former — the implementing agent decides,
  but every negative and every out-of-range passable cost must produce exactly
  one actionable error naming the cell. **An impassable cell's `MoveCost` is
  unchanged: still documented as ignored, still not range-checked here** (its
  `Terrain.moveCost` reports `BlockedCost`).

- **Minimum-cost model, not a zeroed heuristic.** Changing the heuristic to a
  zero lower bound would also restore admissibility, but it discards the A*
  guidance that makes pathfinding bounded work (R-010), and it leaves the
  sub-cell `Progress` threshold degenerate (`Simulation.wouldComplete`:
  `startProgress + BaseMoveCost >= 0` is always true, so a 0-cost cell is
  entered every tick regardless of progress). A "passable cell that costs
  nothing to traverse" has no coherent movement meaning in this model.
  Requiring `MoveCost >= BaseMoveCost` is the cleaner fix and makes the
  existing heuristic provably consistent (every step costs `>= BaseMoveCost`,
  and Manhattan distance changes by exactly 1 per cardinal step, so
  `h(n) - h(n') <= BaseMoveCost <= cost(n, n')`), which is what this
  no-reopening A* needs for optimality.

- **`Pathfinding` gets no algorithm change, only an explicit non-overflow
  argument.** With passable `moveCost <= MaxMoveCost` and the closed set
  bounded by `Width * Height` (the `find` default cap), every `g` value and
  every `tentative = g + moveCost` is `<= Width * Height * MaxMoveCost`, which
  the `MaxMoveCost` choice keeps inside `int32`. State this bound in the
  `## Bounded work` comment. Add a defensive guard only if it can be written
  without a branch that normal inputs reach (e.g. an assertion in a debug
  build, or treating an out-of-range `tentative` as "no improvement"); do not
  widen the cost model to `int64` (the integer-only rule, and the bound makes
  it unnecessary).

- **No committed scenario is expected to violate the new rule.** Authored
  terrain costs so far are 1, 2, 3 (`DeterminismPropertyTests.cellClassGen`,
  the demo scenario, corpus inputs). The implementing agent must grep every
  committed scenario / terrain layer for an authored passable `MoveCost`
  outside `[BaseMoveCost, MaxMoveCost]`. If one exists, **stop and report it**
  — re-authoring committed content and re-pinning hashes is out of this
  task's scope and means the defect already shipped in a fixture.

- **No `Canonical.FormatVersion` change.** `MoveCost` is authored-layer data
  validated before `Terrain.build`; `Canonical.encode` is untouched. Fixture
  hashes (empty terrain, all `BaseMoveCost`) do not move.

## Diagnostics

`AGENTS.md`'s diagnostic-extension rule does not require a new overlay: this
task adds no new authoritative spatial state, it tightens validation of
existing state. `DiagnosticFrame` already carries per-cell `MoveCost`
(`Diagnostics.fs`, `Cells = Array.copy t.MoveCost`). If a scenario-validation
test anywhere lists `ScenarioError` cases exhaustively, extend it for the new
case there rather than add a golden.

## Allowed scope

- `src/CommandoWar.Sim/Terrain.fs` (`MaxMoveCost` literal + doc for the
  `[BaseMoveCost, MaxMoveCost]` passable range);
- `src/CommandoWar.Sim/Scenario.fs` (`ScenarioError` new case; the passable
  `MoveCost` range check in the terrain-layer validation loop;
  `RawTerrainCell` / `AuthoredCell` doc comments);
- `src/CommandoWar.Sim/Pathfinding.fs` (the `## Bounded work` non-overflow
  comment; an optional non-reachable defensive guard — no algorithm change);
- `tests/CommandoWar.Sim.Tests/ScenarioTests.fs` (reject passable cost 0,
  reject passable cost above the ceiling, reject the review's zero-cost
  corridor grid, impassable cell with odd `MoveCost` still accepted);
- `tests/CommandoWar.Sim.Tests/PathfindingTests.fs` and/or
  `DeterminismPropertyTests.fs` (a Dijkstra reference shortest-path over the
  same generated valid terrain, asserting `Pathfinding.find`'s `Found` cost is
  truly minimal and that `Found` occurs exactly when a path exists within
  budget; a near-`MaxMoveCost` long-path cost test);
- `docs/04_SIMULATION_SPEC.md` (section 8 terrain cost range),
  `docs/06_CONTENT_AND_PRESENTATION.md` (section 4 authored `MoveCost` range),
  `docs/09_TEST_STRATEGY.md` if section 3 lists the pathfinding properties;
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml`).

## Forbidden scope

- Diagonal / 8-connected movement.
- Any A* change beyond the non-overflow argument — no reopening-closed-nodes
  rework (unnecessary once the heuristic is consistent), no frontier-key
  change, no neighbour-order change.
- Changing `Terrain.BlockedCost` or the `moveCost` sentinel design.
- Widening the cost model to `int64` or floating point.
- Touching movement / reservation logic in `Simulation.fs` (the
  `wouldComplete` degeneracy is *resolved* by the minimum-cost rule; no code
  change there).
- Re-authoring any committed scenario or `.cwlog` file, or re-pinning any
  fixture / corpus / golden hash. A moved hash is a stop-and-report finding.
- A `Canonical.FormatVersion` bump or any `Canonical.encode` change.
- `ScenarioContent.Version` bump (this is a stricter check on existing
  version-2 data, not a new field).

## Acceptance criteria

- [ ] `Scenario.validate` rejects a passable authored cell with `MoveCost = 0`
      and one with `MoveCost > Terrain.MaxMoveCost`, each with one actionable
      error naming the cell; an impassable cell with an out-of-range
      `MoveCost` is still accepted (its cost is ignored) (`ScenarioTests.fs`).
- [ ] A fixed regression test builds the review's zero-cost-corridor grid (a
      passable `MoveCost = 0` shortcut) and asserts `Scenario.validate`
      rejects it (`ScenarioTests.fs`).
- [ ] A property (>= 200 cases) compares `Pathfinding.find`'s returned `cost`
      against an independent Dijkstra / uniform-cost-search shortest path over
      the same generated terrain (costs drawn only from the valid
      `[BaseMoveCost, MaxMoveCost]` range): when `find` returns `Found`, its
      `cost` equals the reference minimum; `find` returns `Found` iff the
      reference finds a path within the same budget
      (`DeterminismPropertyTests.fs` or `PathfindingTests.fs`).
- [ ] `Terrain.moveCost` never returns a value `>= Terrain.BlockedCost` for a
      passable cell, and `Pathfinding` cost accumulation cannot exceed
      `System.Int32.MaxValue` for any terrain that passes validation — stated
      as the bound `Width * Height * Terrain.MaxMoveCost < Int32.MaxValue` in
      the `Pathfinding` comment, and exercised by a test with a near-ceiling
      authored cost over a long path (`PathfindingTests.fs`).
- [ ] No committed scenario, terrain layer, or `.cwlog` file authors a
      passable `MoveCost` outside `[BaseMoveCost, MaxMoveCost]` (grep
      evidence); `-- corpus` and `-- fixture` reproduce their committed hashes
      with no `--regenerate`.
- [ ] Every pre-existing `PathfindingTests.fs`, `ScenarioTests.fs`,
      `DeterminismPropertyTests.fs`, `CorpusTests.fs`, `ReplayTests.fs`,
      `FixtureTests.fs` fact passes unmodified.
- [ ] `dotnet build CommandoWar.slnx -c Release` = 0 warnings, 0 errors;
      `dotnet list src/CommandoWar.Sim package --include-transitive` =
      `FSharp.Core` only; source scan of `src/CommandoWar.Sim` clean.
- [ ] `docs/04` section 8 (and `docs/06` section 4 if it states the range),
      backlog row, ledger index row + detail file, `PROJECT_STATE.yaml`, task
      status updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`
- `dotnet test CommandoWar.slnx -c Release` before and after (state new count)
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus` and
  `-- fixture` before and after — byte-identical committed hashes
- grep committed `content/` scenarios and terrain layers for authored
  `MoveCost` values; record that none are out of the new range
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
- source scan of `src/CommandoWar.Sim`
- `git status`

## Evidence to capture

- test summary (before/after counts), the new property's case count;
- the grep output showing no committed scenario violates the range;
- the `-- corpus` / `-- fixture` hash lines before and after;
- the chosen `Terrain.MaxMoveCost` value and its non-overflow arithmetic.

## Rollback or removal

The `MaxMoveCost` constant and the `ScenarioError` case are additive.
Reverting is deleting the new validation branch, the constant, and the new
tests. No data migration: no committed scenario carries an out-of-range cost,
so nothing needs re-authoring on either apply or revert.

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md` (new row, e.g. B-046);
- `docs/12_PROGRESS_LEDGER.md` index row + `docs/ledger/` detail file;
- `PROJECT_STATE.yaml` only if this becomes the active task;
- no ADR (this enforces an existing implicit contract, it does not decide a
  new one).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
