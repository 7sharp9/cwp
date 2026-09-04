# TASK-013: Deterministic grid pathfinding with stable tie-breaking

Status: done
Owner: Dave
Phase: P2
Gate: G2 (deterministic core); realises backlog B-010
Size: M

## Objective

Add a framework-neutral, deterministic grid pathfinding module to
`CommandoWar.Sim` (`Pathfinding.fs`): a pure A* query over `Terrain`
(passability, movement cost) that returns the lowest-cost cell path between two
cells, its integer cost, and a typed result for the no-path / out-of-bounds /
budget-exhausted cases. This is `docs/04` section 8 ("Initial movement
progression": "Compute deterministic A* path with stable neighbour order and
tie-breaks") and a `docs/07` section 5 required system.

Like `Terrain`, `Sight`, and the `Objective` algebra, it is authored and
queryable but **not consumed by any tick phase yet**: movement (phase 12.7)
still uses `PlaceholderMovement`; the first consumer is B-011 in P2/P3. It does
not change authoritative behaviour, does not run inside `Simulation.step`, and
does not enter `Canonical.encode`.

## Dependencies

- TASK-010 (terrain grid) accepted and `done`.
- TASK-012 (line of sight + `SightRay` overlay + `--los`) accepted and `done`
  (this task adds the parallel `PlannedPath` overlay and `--path` option).

## Central decisions

- The module lives in `CommandoWar.Sim` (`Pathfinding.fs`, compiled after
  `Sight.fs`, before `Domain.fs`). Integer-only: integer costs and an integer
  heuristic, no floating point, no `System.Math` on doubles. Pure function of
  `Terrain` and two `Cell`s plus an expansion budget. Total: any input pair
  yields a defined typed result, including out-of-bounds or impassable
  endpoints, with no exception.
- **Connectivity:** 4-connected (cardinal moves only), matching `Direction`,
  `PlaceholderMovement`, and the cardinal cover model. Diagonal movement is a
  documented deferral (a scaled integer cardinal-vs-diagonal cost and the
  `Sight` "no cut through an impassable corner" rule); no slice map needs it.
  If the greybox map (B-025) later proves diagonals are needed, that is a
  small extension task, not a rework.
- **Cost model:** entering a cell costs `Terrain.moveCost` for that cell (the
  start cell's own cost is never counted). An `Impassable` cell is never
  expanded and never appears in a path; every neighbour is gated on
  `Terrain.passable` first, so `Terrain.BlockedCost` arithmetic is never
  special-cased. Path cost is the exact sum of the entered cells' costs.
- **Heuristic:** Manhattan distance times `Terrain.BaseMoveCost` (admissible
  for cardinal moves whose minimum step cost is `BaseMoveCost`). Integer.
- **Determinism is a required property.** The frontier is ordered by a TOTAL
  key `(f, h, idx)` = `(g + h`, then `h`, then row-major cell index), so the
  expansion order and the returned path are fully determined regardless of any
  heap implementation's behaviour for equal priorities. Neighbours are
  generated in the fixed order `Direction.all` (North, East, South, West). The
  frontier is a hand-rolled binary min-heap, not
  `System.Collections.Generic.PriorityQueue`; the visited / cost / predecessor
  stores are dense row-major arrays, never a `Dictionary` / `HashSet`
  (docs/09 section 3). The tie-break is documented in the module comment and
  pinned by a golden two-equal-cost-paths example.
- **Bounded work (docs/04 section 19):** `Pathfinding.findWithin` takes an
  explicit `maxExpansions: int` and returns a typed budget-exhausted result
  when the closed set would exceed it. `Pathfinding.find` wraps it with a
  documented default cap derived from `Terrain.Bounds` (`Width * Height`). No
  wall-clock timing, no `Stopwatch`.
- `Canonical.encode` and `Canonical.FormatVersion` are unchanged;
  `Pathfinding.fs` is a leaf that nothing authoritative references. The pinned
  fixture hashes (`0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`, 33 events) do
  not move.

## Allowed scope

- `src/CommandoWar.Sim/Pathfinding.fs` (new), `Diagnostics.fs` (the
  `PlannedPath` `Overlay` case + comment), `CommandoWar.Sim.fsproj`;
- `src/CommandoWar.Headless/DiagnosticRender.fs` (`PlannedPath` drawing in
  Ascii and Svg; Html inherits), `Program.fs` (`--path` option on the `render`
  verb + a `path` render target), `PathDemo.fs` (new),
  `CommandoWar.Headless.fsproj`;
- `content/diagnostics/` new golden outputs + README entries;
- tests in `tests/CommandoWar.Sim.Tests/` (`PathfindingTests.fs` new + `.fsproj`;
  any `DiagnosticsTests.fs` additions for the overlay);
- `docs/04` section 8 and `docs/06` section 4 realisation notes;
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml`);
- finalisation of TASK-012 / B-009 to `done` (precondition only).

## Forbidden scope

- Wiring pathfinding into `Simulation.step`, the Navigation and movement phase,
  or any replacement of `PlaceholderMovement` (B-011); cell reservation,
  formation slots, movement progress within an edge, or dynamic replanning
  (B-011); combat or cover-aware routing (B-019); enemy AI (B-022).
- Diagonal / 8-connected movement; a non-integer cost or heuristic; any
  floating point in `Pathfinding.fs` or the renderer additions.
- Jump Point Search, hierarchical / HPA* pathfinding, flow fields, navmesh, or
  any precomputed visibility / distance structure; a per-entity path cache.
- Any rendering, graphics, or UI framework in `CommandoWar.Sim`; a
  Godot/MonoGame/raylib reference anywhere.
- Changing `Simulation.step`, `Canonical.encode` or its format version,
  `Setup.sixAgentWorld`, the shared fixture parameters, the pinned hashes, the
  `Diagnostics.frame` / `frameOf` signatures, the `SightRay` overlay or its
  renderer output, or any existing `cwheadless` verb's output (overlay-absent
  renders included).
- A new dependency or project; an on-disk content format (B-024); `Overlay`
  cases for B-011 / B-019.
- Touching the client spikes, `src/_scratch`, `.slnx`; reorganising
  `decisions/` or `tasks/`; destructive git.

## Required work

1. `src/CommandoWar.Sim/Pathfinding.fs`: `PathResult` (`Found of cells: Cell[] *
   cost: int | NoPath | BudgetExhausted of expansions: int | InvalidEndpoint
   of cell: Cell`); `Pathfinding.findWithin : Terrain -> Cell -> Cell -> int ->
   PathResult` and `Pathfinding.find : Terrain -> Cell -> Cell -> PathResult`.
   All total, pure, deterministic, integer-only. `Found.cells` starts at the
   start cell and ends at the goal; `start = goal` yields `Found([| start |],
   0)`. Module comment: the algorithm, cost model, heuristic, the total-order
   frontier key and neighbour order, the budget, the cardinal-only decision
   and its deferral, and "nothing in `Simulation.step` or any phase calls this
   yet; the first consumer is the Navigation and movement phase via B-011".
2. `Diagnostics.fs`: add `| PlannedPath of from: Cell * target: Cell * cells:
   Cell[] * cost: int * reached: bool` to the `Overlay` DU and update the DU
   comment so it no longer lists B-010 as pending. `Diagnostics.frame` /
   `frameOf` still never emit an overlay.
3. `DiagnosticRender.fs`: extend `Ascii` and `Svg` to draw a `PlannedPath`
   overlay (a line of `+` with distinct `S` / `G` glyphs on the ASCII
   composite; a solid polyline plus start / goal markers in SVG). Html
   inherits. Every existing render byte-identical when no overlay is present
   (the `--los` / `SightRay` output included).
4. `src/CommandoWar.Headless/Program.fs`: a repeatable `--path AX,AY:BX,BY`
   option on the `render` verb that attaches a `PlannedPath` overlay (computed
   via `Pathfinding.find` over the target's terrain), plus a `path` render
   target. It composes with `--los`. No new verb.
5. `src/CommandoWar.Headless/PathDemo.fs`: a focused pathfinding fixture built
   through `Scenario.validate` + `World.ofScenario`, showing a straight clear
   route, a route detouring around an impassable wall, a route preferring a
   cheap detour over a movement-cost patch, and a no-path case (goal walled
   off).
6. Golden outputs under `content/diagnostics/` (`path.ascii.txt`, `path.svg`)
   with the exact regeneration commands in the README. Existing demo / los /
   fixture goldens unchanged.
7. `tests/CommandoWar.Sim.Tests/PathfindingTests.fs` (new, registered in the
   `.fsproj`): a straight and an L-shaped path on empty terrain (endpoints,
   adjacency, step-count cost); obstacle avoidance (impassable wall forces a
   detour of adjacent passable cells); cost-aware routing (golden-pinned); no
   path (`NoPath`); `InvalidEndpoint` for an out-of-bounds or impassable start
   / goal with no exception; `start = goal` -> `Found([| start |], 0)`;
   determinism; tie-break stability (golden-pinned); budget (`BudgetExhausted`
   on a tiny `maxExpansions`); a path-shape property test over a hand-built
   terrain and a grid of endpoint pairs; the fixture pin
   (`0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`, 33 events,
   `Canonical.FormatVersion` 1); a renderer test (a `PlannedPath` overlay
   appears distinctly in ASCII and SVG and matches the committed golden;
   overlay-absent renders byte-unchanged; a frame carrying both a `SightRay`
   and a `PlannedPath` renders both).
8. Register `Pathfinding.fs`, `PathDemo.fs`, `PathfindingTests.fs` in their
   `.fsproj`. `TreatWarningsAsErrors` clean.
9. `docs/04` section 8: a "Realised by TASK-013" note. `docs/06` section 4:
   note the Terrain / Elevation rows' movement data now have a pathfinding
   consumer. `AGENTS.md` / `docs/09` section 8: no rule change; the completion
   report confirms the standing rule was honoured (`Overlay` case + golden
   visualiser output added for the new spatial capability).

## Acceptance criteria

- [x] `src/CommandoWar.Sim/Pathfinding.fs` exists with `PathResult`,
      `Pathfinding.findWithin`, `Pathfinding.find`, all total, pure,
      deterministic, integer-only. Module comment states no phase calls it.
- [x] 4-connected A*; entering-cell cost model; Manhattan * `BaseMoveCost`
      heuristic; total-order frontier key `(f, h, row-major idx)`; N/E/S/W
      neighbour order. Tie-break documented and golden-pinned.
- [x] Explicit `maxExpansions` budget with a typed `BudgetExhausted` result;
      `find` default cap `Width * Height`.
- [x] `Diagnostics.fs` gains the `PlannedPath` `Overlay` case;
      `Diagnostics.frame` / `frameOf` signatures and behaviour unchanged;
      `Canonical.encode` unchanged; `Canonical.FormatVersion` still 1.
- [x] `DiagnosticRender` draws `PlannedPath` in ASCII and SVG; overlay-absent
      renders byte-identical (existing demo / los / fixture goldens
      unchanged).
- [x] `cwheadless render ... --path AX,AY:BX,BY` (repeatable) + `path` target
      added; `step` / `replay` / `compare` / `fixture` and overlay-absent
      `render` output unchanged; `--los` and `--path` compose.
- [x] `content/diagnostics/path.ascii.txt` + `path.svg` goldens committed with
      README regeneration commands.
- [x] `dotnet test CommandoWar.slnx -c Release` = `Passed: 145` (128 + 16
      `PathfindingTests` + 1 `DiagnosticsTests`).
- [x] `cwheadless fixture` unchanged: initial `0xF2F3DF0D820AD9AC`, final
      `0x838D3AE7DBFB735D`, 33 events.
- [x] `dotnet list ... package --include-transitive` = `FSharp.Core` only, no
      `ProjectReference`; source scan of `src/CommandoWar.Sim` clean of
      `godot|mibo|monogame|raylib|DateTime|Stopwatch|System.Random|Dictionary|HashSet|groupBy|float`
      (doc comments only); `src/CommandoWar.Headless` clean of a graphics
      framework.
- [x] `docs/04` section 8 and `docs/06` section 4 realisation notes written.
- [x] Backlog rows, ledger index row + detail file, `PROJECT_STATE.yaml`, and
      task status updated. No forbidden scope entered.

## Required verification

- `dotnet build CommandoWar.slnx -c Release` (0 warnings, 0 errors)
- `dotnet test CommandoWar.slnx -c Release` before and after
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
- `dotnet run --project src/CommandoWar.Headless -c Release -- render path
  --path ... --format ascii|svg`, byte-compared against the committed goldens;
  one `render demo` without `--path` and one `render los --los ...`
  byte-compared against the existing goldens
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
- source scan of `src/CommandoWar.Sim` and `src/CommandoWar.Headless`
- `git status`

## Rollback or removal

`Pathfinding.fs` is a leaf: removing it, the `PlannedPath` `Overlay` case and
its renderer branches, `PathDemo.fs`, the `--path` option and `path` target,
the `content/diagnostics/path.*` goldens, and `PathfindingTests.fs` restores
the pre-task state without touching `Simulation.step`, `Canonical.encode`, the
shared fixture, the `SightRay` overlay, or any other verb.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Alternative

If a deterministic A* with a total-order frontier key could not be made to
return a stable path without disproportionate machinery, the split was: land a
deterministic Dijkstra / uniform-cost search with the same neighbour order and
tie-break (drop the heuristic) as TASK-013 and take the heuristic as
TASK-013b. It was not needed: the total key `(f, h, row-major idx)` plus a
hand-rolled heap makes the A* path fully determined, pinned by the tie-break
golden and the demo goldens. The per-tick expansion budget is an explicit
`maxExpansions` parameter with a `Width * Height` default; sizing it against
real profiling evidence is deferred to B-013 (the benchmark harness).
