## 2026-09-04 - TASK-013 - Deterministic grid pathfinding with stable tie-breaking

**Owner:** Dave with coding-agent assistance
**Source revision:** `7bb3b70` (Add deterministic line-of-sight diagnostics); working tree, not yet committed
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; `net10.0`; xUnit 2.9.3
**Status change:** TASK-013 `proposed -> active -> review`; TASK-012 `review -> done` (finalised as the precondition of this task)

### Precondition: TASK-012 finalised

TASK-012 was committed (`7bb3b70`) and in review with all evidence recorded.
Finalised with the TASK-009 / TASK-012 pattern, no re-run of its full
verification:

- `tasks/TASK-012-LINE-OF-SIGHT.md` `Status: review -> done`;
- `docs/11_BACKLOG.md`: the "Current work" TASK-012 row and the "Planned
  simulation work" B-009 row `active -> done`, task-file link kept;
- `docs/ledger/2026-09-04-TASK-012-line-of-sight.md` Review block
  `Accepted: pending -> yes (2026-09-04)` with an acceptance note; the
  `docs/12` index row's Accepted cell likewise (`pending -> yes (2026-09-04)`,
  status change `-> done`).
- Check performed: `git status` (clean working tree) plus one
  `dotnet test CommandoWar.slnx -c Release` = `Passed: 128` (unchanged). Both
  passed, so finalisation proceeded.

### Central decisions

- **Module placement.** `src/CommandoWar.Sim/Pathfinding.fs`, compiled after
  `Sight.fs`, before `Domain.fs`. Depends only on `Grid` and `Terrain`;
  nothing authoritative references it (a leaf, like `Terrain`, `Sight`, and
  the `Objective` algebra). `Canonical.encode` / `Canonical.FormatVersion`
  untouched; the pinned fixture hashes `0xF2F3DF0D820AD9AC` /
  `0x838D3AE7DBFB735D` and the 33-event count do not move.
- **Algorithm.** A*, 4-connected (cardinal only). Cost of entering a cell is
  `Terrain.moveCost` for that cell; the start cell's own cost is never
  counted. Every neighbour is gated on `Terrain.passable` first, so
  `Terrain.BlockedCost` arithmetic is never reached. Heuristic: Manhattan
  distance times `Terrain.BaseMoveCost`, integer, admissible for the
  minimum-step-cost-is-`BaseMoveCost` case. `abs` on `int`, no `System.Math`,
  no float.
- **Determinism.** Frontier ordered by the total key `(f, h, idx)` =
  `(g + h`, then `h`, then row-major cell index `y * Width + x)`. Distinct
  live entries for one cell differ in `f` (`h` is fixed per cell), distinct
  cells differ in `idx`, so the key never has a genuine tie. The frontier is a
  hand-rolled binary min-heap (`Frontier` in `Pathfinding.fs`), not
  `System.Collections.Generic.PriorityQueue`, so the pop sequence depends only
  on that comparison. Neighbours generated in `Direction.all` order (N, E, S,
  W). The `g` / `closed` / `cameFrom` stores are dense row-major arrays, never
  a `Dictionary` / `HashSet` (docs/09 section 3). Tie-break documented on the
  module and pinned by a golden two-equal-cost-paths example.
- **Bounded work (docs/04 section 19).** `Pathfinding.findWithin` takes an
  explicit `maxExpansions: int`; when the search would close a cell beyond
  that budget it returns `BudgetExhausted expansions`. `Pathfinding.find`
  wraps it with the default cap `Terrain.Bounds.Width * Height` (enough to
  close every cell once, so `find` returns `BudgetExhausted` only for a
  pathological call). No `Stopwatch`, no wall-clock read.
- **Totality.** Out-of-bounds or impassable start / goal ->
  `InvalidEndpoint cell` (start reported first when both are invalid).
  `start = goal` -> `Found([| start |], 0)`. Unreachable goal -> `NoPath`.
- **Result shape.** `PathResult = Found of cells: Cell[] * cost: int | NoPath
  | BudgetExhausted of expansions: int | InvalidEndpoint of cell: Cell`.
- **Overlay shape.** `Overlay` gains `PlannedPath of from: Cell * target:
  Cell * cells: Cell[] * cost: int * reached: bool` (`cells` = `Found`'s cell
  path, `[||]` for the not-reached cases, so the renderer draws the real
  path). Parallel to `SightRay`.

### Changes

- **New `src/CommandoWar.Sim/Pathfinding.fs`.** `PathResult`;
  `Pathfinding.findWithin : Terrain -> Cell -> Cell -> int -> PathResult`;
  `Pathfinding.find : Terrain -> Cell -> Cell -> PathResult`. Private `step`
  (one cardinal move), private `Frontier` (the deterministic binary min-heap).
  Module comment carries the algorithm, cost model, heuristic, the total-order
  frontier key and neighbour order, the budget, the cardinal-only decision and
  its deferral, totality, and "nothing in `Simulation.step` or any phase calls
  this yet; the first consumer is the Navigation and movement phase via
  B-011".
- **`src/CommandoWar.Sim/Diagnostics.fs`.** `Overlay` gains the `PlannedPath`
  case; the DU comment now lists B-010 as realised by TASK-013 and drops it
  from "do not exist yet" (B-011 / B-019 still there). `Diagnostics.frame` /
  `frameOf` unchanged - they still never emit an overlay. The "populated only
  by a caller" line names `--path` alongside `--los`.
- **`src/CommandoWar.Sim/CommandoWar.Sim.fsproj`.** One `<Compile>` entry,
  `Pathfinding.fs` between `Sight.fs` and `Domain.fs`.
- **`src/CommandoWar.Headless/DiagnosticRender.fs`.** `Ascii`: `PlannedPath`
  path cells drawn as `+`, start as `S`, goal as `G` on the composite grid
  (glyph priority agent > sight blocker `x` > `S` > `G` > sight ray `*` >
  path `+`), plus a `path A -> B: reached, cost N | no path` line in the
  overlays section. `Svg`: a solid orange polyline through the path-cell
  centres (only when >= 2 cells), a green start disc, an orange goal box; the
  three `match o with` sites gain the `PlannedPath` arm. `Html` inherits (it
  embeds `Svg` per tick). Every overlay-absent render is byte-identical
  (verified against the committed demo / los / fixture goldens).
- **New `src/CommandoWar.Headless/PathDemo.fs`.** A 16 x 12 `RawScenario`
  validated through `Scenario.validate` + `World.ofScenario`: an impassable
  wall at `x = 5` rows 0..7 (bottom open), a 2 x 3 movement-cost patch
  (cost 5) at `x = 10..11` rows 2..4, and a walled-off pocket `(14,9)`; two
  friendly agents out of the way. `PathDemo.routes` is the four demo routes.
- **`src/CommandoWar.Headless/CommandoWar.Headless.fsproj`.** `PathDemo.fs`
  between `LosDemo.fs` and `DiagnosticRender.fs`.
- **`src/CommandoWar.Headless/Program.fs`.** `render` verb gains a repeatable
  `--path AX,AY:BX,BY` (each attaches a `PlannedPath` computed via
  `Pathfinding.find` over the target's terrain) and a `path` render target for
  `PathDemo`. `--los` and `--path` compose (both appended to the frame's
  overlays). The private `parseLos` is renamed `parseCellPair` (used by both
  options). Existing verbs and overlay-absent `render` output unchanged;
  `cmdRender` header comment and `usage ()` gain the `path` target and the
  `--path` option.
- **`content/diagnostics/`.** New goldens `path.ascii.txt` (1220 B) and
  `path.svg` (22308 B); `README.md` gains their rows and the regeneration
  commands. Existing demo / los / fixture goldens unchanged.
- **`tests/CommandoWar.Sim.Tests/PathfindingTests.fs` (new, 16 facts).**
  Straight and L-shaped paths on empty terrain (endpoints, adjacency,
  step-count cost); `start = goal`; obstacle avoidance (impassable wall forces
  an adjacent passable detour); cost-aware routing (golden-pinned path +
  cost 6 vs the straight-line cost 31); `NoPath` for a fully walled-off goal;
  `InvalidEndpoint` for out-of-bounds and impassable endpoints (start first);
  determinism; tie-break stability (golden-pinned `(0,0)->(2,2)` on empty
  terrain -> east along row 0 then south down column 2); `BudgetExhausted` on
  `maxExpansions = 5`; a path-shape property test over a hand-built terrain and
  all 64x64 endpoint pairs (adjacent, passable, recomputed cost matches); the
  fixture pin (`0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`, 33 events,
  `Canonical.FormatVersion` 1); the pathfinding demo ASCII + SVG golden
  byte-equality; `PlannedPath` appears distinctly with the overlay-absent
  render unchanged; a frame carrying both a `SightRay` and a `PlannedPath`
  renders both.
- **`tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs` (+1 fact).** A
  hand-built `PlannedPath` overlay renders through ASCII and SVG; the
  overlay-absent SVG is byte-identical.
- **`tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj`.**
  `PathfindingTests.fs` registered after `SightTests.fs`.
- **Docs.** `docs/04` section 8 "Realised by TASK-013" (algorithm, cost model,
  heuristic, tie-break, budget, cardinal-only, no phase consumer yet);
  `docs/06` section 4 (the Terrain / Elevation rows now have a pathfinding
  consumer).
- **Control.** `tasks/TASK-013-PATHFINDING.md` (new);
  `docs/11_BACKLOG.md` (TASK-013 "Current work" row `active`, B-010 `-> active`
  with the task-file link, B-011 dependency note; TASK-012 / B-009
  `-> done`); `PROJECT_STATE.yaml` (`active_work -> TASK-013`, gates / gate /
  phase / framework_decision unchanged); `docs/12_PROGRESS_LEDGER.md` (index
  rows + "Pinned facts" Green tests `128 -> 145`); this entry.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)`.
- Command: `dotnet test CommandoWar.slnx -c Release` (before)
  - Result: `Passed: 128` (after the TASK-012 finalisation doc edits; scope
    check clean, working tree otherwise unchanged).
- Command: `dotnet test CommandoWar.slnx -c Release` (after)
  - Result: `Passed! - Failed: 0, Passed: 145, Skipped: 0, Total: 145`
    (128 + 16 `PathfindingTests` + 1 `DiagnosticsTests`).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  - Result: initial `0xF2F3DF0D820AD9AC`, final `0x838D3AE7DBFB735D` (format
    1), 33 events - unchanged.
- Command: `dotnet run ... -- render path --path 1,1:4,1 --path 2,6:9,6
  --path 9,3:13,3 --path 1,10:14,9 --format ascii|svg --out ...` then re-run
  and `diff` against the committed files
  - Result: byte-identical (regeneration idempotent). ASCII overlay lines:
    `path (1,1) -> (4,1): reached, cost 3`; `path (2,6) -> (9,6): reached,
    cost 11`; `path (9,3) -> (13,3): reached, cost 8`; `path (1,10) ->
    (14,9): no path`. Footer `tick 0 | hash 0xE574581C6EEA41F4 (format 1) |
    draws 0 | agents 2 | cover-edges 0 | events 0`.
- Command: `dotnet run ... -- render demo --format ascii|svg|html` (no
  `--path`) and `render los --los ... --format ascii|svg`, `diff` against
  `content/diagnostics/demo.*` / `los.*`
  - Result: byte-identical - overlay-absent and `SightRay` output unchanged.
- Command: `dotnet run ... -- render demo --los 0,0:11,7 --path 0,0:10,6
  --format ascii`
  - Result: both overlay lines present (`sight (0,0) -> (11,7): blocked at
    (2,2)`, `path (0,0) -> (10,6): reached, cost 16`) - `--los` and `--path`
    compose.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
  - Result: `FSharp.Core 10.1.303` only; no transitive package, no
    `ProjectReference`.
- Command: source scan of `src/CommandoWar.Sim/*.fs` for
  `godot|mibo|monogame|raylib|DateTime|Stopwatch|System.Random|Dictionary|HashSet|groupBy|float`
  - Result: only doc-comment prose ("floating point" in `Pathfinding.fs`,
    `Sight.fs`, `Terrain.fs`, `Diagnostics.fs`; `Dictionary` / `HashSet` /
    `Stopwatch` named in the `Pathfinding.fs` comment as what it does NOT
    use). No type, API, or value match.
- Command: source scan of `src/CommandoWar.Headless/*.fs` for a graphics
  framework
  - Result: only doc-comment mentions (Godot). No code match.
- Command: `git status --porcelain`
  - Result: modified `PROJECT_STATE.yaml`, `content/diagnostics/README.md`,
    `docs/04`, `docs/06`, `docs/11`, `docs/12`,
    `docs/ledger/2026-09-04-TASK-012-line-of-sight.md`,
    `src/CommandoWar.Sim/{CommandoWar.Sim.fsproj,Diagnostics.fs}`,
    `src/CommandoWar.Headless/{CommandoWar.Headless.fsproj,DiagnosticRender.fs,Program.fs}`,
    `tasks/TASK-012-LINE-OF-SIGHT.md`,
    `tests/CommandoWar.Sim.Tests/{CommandoWar.Sim.Tests.fsproj,DiagnosticsTests.fs}`;
    new `src/CommandoWar.Sim/Pathfinding.fs`,
    `src/CommandoWar.Headless/PathDemo.fs`,
    `tests/CommandoWar.Sim.Tests/PathfindingTests.fs`,
    `content/diagnostics/path.ascii.txt`, `content/diagnostics/path.svg`,
    `tasks/TASK-013-PATHFINDING.md`, this file. Nothing under the client
    spikes, `src/_scratch`, or `CommandoWar.slnx`; `Canonical.fs`,
    `Simulation.fs`, `Sight.fs`, `Fixture.fs`, `FixtureTests.fs`,
    `Setup.sixAgentWorld`, and the `SightRay` renderer output untouched.

### Evidence

- **`PathResult` shape:** `Found of cells: Cell[] * cost: int | NoPath |
  BudgetExhausted of expansions: int | InvalidEndpoint of cell: Cell`.
  `Found.cells.[0]` = start, `Found.cells.[^0]` = goal.
- **Tie-break pin** (`PathfindingTests`): `Pathfinding.find (Terrain.empty
  8x8) (0,0) (2,2)` -> `Found([(0,0);(1,0);(2,0);(2,1);(2,2)], 4)` - east
  along row 0, then south down column 2, per the `(f, h, row-major idx)`
  key and N/E/S/W neighbour order.
- **Cost-aware pin:** row `y = 3` cells `(2,3)`,`(3,3)`,`(4,3)` at cost 10;
  `find (1,3) (5,3)` -> the row-2 detour `Found([(1,3);(1,2);(2,2);(3,2);
  (4,2);(5,2);(5,3)], 6)`, not the straight line (cost 31).
- **Budget pin:** `findWithin (Terrain.empty 20x20) (0,0) (19,19) 5` ->
  `BudgetExhausted 5`; `find` (cap 400) -> `Found(_, 38)`.
- **Property test:** over all 4096 ordered endpoint pairs on a hand-built
  8x8 terrain, every `Found(cells, cost)` had cardinally-adjacent passable
  cells starting at the start and ending at the goal, and
  `sum (moveCost cells.[1..]) = cost`.
- **Pathfinding demo golden** (`path.ascii.txt`): footer `tick 0 | hash
  0xE574581C6EEA41F4 (format 1) | draws 0 | agents 2 | cover-edges 0 |
  events 0`; four overlay lines as quoted above.
- **Fixture pin:** `0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`, 33 events,
  `Canonical.FormatVersion` 1 - unmoved (`PathfindingTests`, `SightTests`,
  `FixtureTests`, `DiagnosticsTests`).

### Deviations and unresolved issues

- **`Frontier` is a hand-rolled binary min-heap**, not
  `System.Collections.Generic.PriorityQueue`. The task permitted either
  (with the total key it would also be deterministic); the hand-rolled heap
  removes any dependence on a library heap's internal layout and keeps the
  determinism argument entirely local. ~40 lines.
- **`PlannedPath` carries the traced `cells`** (not just `from`/`target`/
  `cost`/`reached`). The renderer needs the real path to draw `+` and the
  SVG polyline; recomputing in the renderer would be a second pathfinder.
  Consistent with `SightRay` carrying `Sight.trace`'s `Path`.
- **A 0-cost passable cell would break heuristic admissibility.**
  `Scenario.validate` allows `MoveCost = 0` for a passable cell, and the
  Manhattan * `BaseMoveCost` heuristic would then overestimate. No slice map
  authors one (empty terrain and every demo use `>= BaseMoveCost` for
  passable cells); noted in the module comment. If a future map needs
  0-cost cells, clamp the heuristic or switch that query to Dijkstra.
- **`--path` over the `fixture` / `demo` / command-log targets** works
  (`render demo --path ...`, `render fixture --path ...`) but only the
  `path` target's overlay output is golden-pinned.
- **The PathDemo cover grid is empty** (no cover authored), so the ASCII
  render carries an all-`.` cover block, as `LosDemo` does. Harmless.
- **No consumer.** The Navigation and movement phase (B-011) is the first
  reader; nothing in `Simulation.step` touches `Pathfinding` this task.
- Clean build in the working tree (existing `bin/`/`obj/`), not a fresh
  clone.

### Documents updated

- `tasks/TASK-013-PATHFINDING.md` (new; status `review`)
- `tasks/TASK-012-LINE-OF-SIGHT.md` (`review -> done`)
- `docs/04_SIMULATION_SPEC.md` (section 8 "Realised by TASK-013")
- `docs/06_CONTENT_AND_PRESENTATION.md` (section 4: Terrain / Elevation
  pathfinding consumer)
- `docs/11_BACKLOG.md` (TASK-013 row + B-010 `-> active` + B-011 dep note;
  TASK-012 / B-009 `-> done`)
- `docs/12_PROGRESS_LEDGER.md` ("Pinned facts" Green tests `128 -> 145`,
  "Confirmed by" -> TASK-013; index rows for TASK-013 and the TASK-012
  acceptance)
- `docs/ledger/2026-09-04-TASK-012-line-of-sight.md` (Review block accepted
  2026-09-04)
- `content/diagnostics/README.md` (path goldens + regeneration commands)
- `PROJECT_STATE.yaml` (`active_work -> TASK-013`)
- this entry

### AGENTS.md / docs/09 section 8 standing rule

No rule change. The rule was honoured: TASK-013 adds spatial capability
(deterministic grid pathfinding), so it extends the diagnostic frame with an
`Overlay` case (`PlannedPath`) and adds golden visualiser output
(`content/diagnostics/path.ascii.txt`, `path.svg`) with an explicit
regeneration command in `content/diagnostics/README.md`. The frame and its
renderers stay observers only - `Diagnostics.frame` / `frameOf` never emit an
overlay, and nothing in `Simulation.step` calls `Pathfinding`.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-04)
- Acceptance note: pathfinding module, `PlannedPath` overlay, `--path` render
  option, and pathfinding demo goldens accepted; `Canonical.encode` and every
  pinned fixture value unmoved; 145 tests green. Finalised as the precondition
  of TASK-014 with a `git status` scope check plus one
  `dotnet test CommandoWar.slnx -c Release` (`Passed: 145`).
- Notes: 4-connected integer A* with a total-order frontier key `(f, h,
  row-major idx)` and N/E/S/W neighbour order over a hand-rolled deterministic
  heap; explicit `maxExpansions` budget with a `Width * Height` default;
  tie-break and cost-aware routing pinned by golden examples; `PlannedPath`
  overlay + `--path` render option + pathfinding demo goldens;
  `Canonical.encode` and all pinned fixture values unmoved; no tick phase
  consumes it; 145 tests green.
