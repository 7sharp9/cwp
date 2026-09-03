## 2026-09-04 - TASK-012 - Deterministic point-to-point line of sight and opacity

**Owner:** Dave with coding-agent assistance
**Source revision:** `cf5df10` (Add deterministic diagnostic frame and renders); working tree, not yet committed
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; `net10.0`; xUnit 2.9.3
**Status change:** TASK-012 `proposed -> active -> review`; TASK-010 `review -> done`; TASK-011 `review -> done` (both finalised as the precondition of this task)

### Precondition: TASK-010 and TASK-011 finalised

Both were in review with all evidence recorded. Finalised with the TASK-009
pattern, no re-run of either task's full verification:

- task files `Status: review -> done`;
- `docs/11_BACKLOG.md`: the "Current work" rows and the "Planned simulation
  work" rows B-008 (TASK-010) and B-012a (TASK-011) `review`/`active -> done`,
  task-file links kept;
- ledger detail files' Review blocks `Accepted: pending -> yes (2026-09-04)`
  with a one-line note; the `docs/12` index rows' Accepted cells likewise.
- Check performed: `git status` scope (only the finalisation doc files) plus
  one `dotnet test CommandoWar.slnx -c Release` = `Passed: 115` (unchanged).
  Both passed, so finalisation proceeded.

### Central decisions

- **Module placement.** `src/CommandoWar.Sim/Sight.fs`, compiled after
  `Terrain.fs`, before `Domain.fs`. Depends only on `Grid` and `Terrain`;
  nothing authoritative references it (a leaf, like `Terrain` and the
  `Objective` algebra). `Canonical.encode` / `Canonical.FormatVersion`
  untouched; the pinned fixture hashes `0xF2F3DF0D820AD9AC` /
  `0x838D3AE7DBFB735D` and the 33-event count do not move.
- **Algorithm.** The classic integer supercover grid walk,
  `decision = (1 + 2*ix)*ny - (1 + 2*iy)*nx` (`< 0` step x, `> 0` step y,
  `= 0` one diagonal step, guarded so a diagonal step only happens while both
  axes have progress left). Integer-only: `abs` / `sign` on `int`, no
  `System.Math`, no float.
- **Symmetry** (required, `docs/04` s9). `Sight.visible t a b =
  Sight.visible t b a` for every pair. The supercover cell set of a segment is
  defined by the segment, not a direction; walking from the far end negates
  `decision` at each corresponding step, swapping the step-x / step-y branches
  and keeping `decision = 0` a diagonal step, so the reverse walk is the
  forward path reversed. The blocking rule is then over sets identical in both
  directions (intermediate path cells; the unordered shared-edge-neighbour
  pair of each diagonal step). `Blocker` is not required symmetric and is not
  (first blocker from the origin end); `visible` is, pinned by a property
  test over a 10x10 grid of endpoint pairs (including out-of-bounds).
- **Corner rule.** A diagonal step is blocked only when *both* shared-edge
  neighbours are `Terrain.opaque`. A single wall cell at a diagonal corner
  does not block. Endpoints never block.
- **Elevation rule.** An intermediate cell blocks when it is `Terrain.opaque`
  OR its elevation is strictly greater than the elevation of *both* endpoints
  (a ridge occludes). Minimal and documented; eye-height / height-field
  reasoning deferred. The task's TASK-012b split was not needed: the rule is
  symmetric and sufficient for the bridge mission.
- **Totality.** Out-of-bounds endpoint -> `Visible = false`, `Blocker = None`,
  `Path = [| a; b |]` (the walk is not run).
- **Overlay shape.** `Overlay` gains `SightRay of from: Cell * target: Cell *
  cells: Cell[] * blocked: Cell option` (`cells` = `Sight.trace`'s `Path`, so
  the renderer draws the real traced line, not its own approximation).

### Changes

- **New `src/CommandoWar.Sim/Sight.fs`.** `LineOfSight { Visible: bool; Path:
  Cell[]; Blocker: Cell option }`; `Sight.trace : Terrain -> Cell -> Cell ->
  LineOfSight`; `Sight.visible : Terrain -> Cell -> Cell -> bool` (from
  `trace`). Private `walk` (the supercover walk). Module comment carries the
  algorithm, both blocking rules, the symmetry argument, totality, and "no
  phase calls this yet; first consumer is Perception (B-015)".
- **`src/CommandoWar.Sim/Diagnostics.fs`.** `Overlay` gains the `SightRay`
  case; the DU comment no longer lists B-009 as pending (B-010/B-011/B-019
  still do not exist). `Diagnostics.frame` / `frameOf` unchanged - they still
  never emit an overlay.
- **`src/CommandoWar.Sim/CommandoWar.Sim.fsproj`.** One `<Compile>` entry,
  `Sight.fs` between `Terrain.fs` and `Domain.fs`.
- **`src/CommandoWar.Headless/DiagnosticRender.fs`.** `Ascii`: `SightRay`
  path cells drawn as `*` and blocking cells as `x` on the composite grid
  (below agents, above terrain), plus a `sight A -> B: clear | blocked at C`
  line in the overlays section; the incomplete `for Cells(...)` loop replaced
  with an exhaustive `match`. `Svg`: a dashed ray line, small dots on the
  traced cells, and a red box + cross on the blocker; `Cells` overlays drawn
  as faint boxes. `Html` inherits (it embeds `Svg` per tick). Every
  overlay-absent render is byte-identical (verified against the committed
  demo/fixture goldens).
- **New `src/CommandoWar.Headless/LosDemo.fs`.** A 12 x 12 `RawScenario`
  validated through `Scenario.validate` + `World.ofScenario`: an opaque wall
  cell `(5,4)`, a lone opaque cell `(9,3)`, a solid opaque corner
  `(9,7)`/`(8,8)`, and a diagonal elevation ridge peaking at `(3,7)`=6; two
  friendly agents out of the way. `LosDemo.rays` is the five demo rays.
- **`src/CommandoWar.Headless/CommandoWar.Headless.fsproj`.** `LosDemo.fs`
  between `DemoScenario.fs` and `DiagnosticRender.fs`.
- **`src/CommandoWar.Headless/Program.fs`.** `render` verb gains a repeatable
  `--los AX,AY:BX,BY` (each attaches a `SightRay` computed via `Sight.trace`
  over the target's terrain) and a `los` render target for `LosDemo`. `--los`
  attaches to the selected `ascii`/`svg` frame and to every `html` frame.
  Existing verbs and overlay-absent `render` output unchanged; `usage ()`
  gains the `los` target and the `--los` option.
- **`content/diagnostics/`.** New goldens `los.ascii.txt` (1092 B) and
  `los.svg` (19181 B); `README.md` gains their rows and the regeneration
  commands (idempotent). Existing demo/fixture goldens unchanged.
- **`tests/CommandoWar.Sim.Tests/SightTests.fs` (new, 12 facts).** Clear
  sight (horizontal / vertical / diagonal); `Path` endpoints; opaque block +
  `Blocker`; the corner rule (single wall visible, two walls blocked,
  golden-pinned `Path` + `Blocker`); the elevation rule (ridge blocks; not
  when an endpoint is at or above; not when equal to the higher endpoint);
  the symmetry property over a hand-built terrain and a 10x10 grid of endpoint
  pairs; out-of-bounds endpoints; determinism; the fixture pin
  (`0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`, 33 events,
  `Canonical.FormatVersion` 1); the LOS demo ASCII + SVG golden byte-equality;
  `SightRay` appears distinctly in ASCII and SVG with the overlay-absent
  render unchanged.
- **`tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs` (+1 fact).** A
  hand-built `SightRay` overlay renders through ASCII and SVG; the
  overlay-absent SVG is byte-identical.
- **`tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj`.**
  `SightTests.fs` registered after `TerrainTests.fs`.
- **Docs.** `docs/04` section 9 "Realised by TASK-012"; `docs/06` section 4
  (the High occlusion row now has a consumer).
- **Control.** `tasks/TASK-012-LINE-OF-SIGHT.md` (new); `docs/11_BACKLOG.md`
  (TASK-012 "Current work" row `active`, B-009 `-> active` with the task-file
  link, B-015 dependency note, TASK-010/TASK-011/B-008/B-012a `-> done`);
  `PROJECT_STATE.yaml` (`active_work -> TASK-012`); `docs/12_PROGRESS_LEDGER.md`
  (index rows + "Pinned facts" Green tests `115 -> 128`); this entry.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)`.
- Command: `dotnet test CommandoWar.slnx -c Release` (before)
  - Result: `Passed: 115` (after the TASK-010/011 finalisation doc edits;
    scope check clean).
- Command: `dotnet test CommandoWar.slnx -c Release` (after)
  - Result: `Passed! - Failed: 0, Passed: 128, Skipped: 0, Total: 128`
    (115 + 12 `SightTests` + 1 `DiagnosticsTests`).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  - Result: initial `0xF2F3DF0D820AD9AC`, final `0x838D3AE7DBFB735D` (format
    1), 33 events - unchanged.
- Command: `dotnet run ... -- render los --los 1,1:10,1 --los 1,4:10,4 --los
  7,2:10,5 --los 1,7:6,7 --los 6,5:11,10 --format ascii|svg --out ...`
  then re-run and `diff` against the committed files
  - Result: byte-identical (regeneration idempotent).
- Command: `dotnet run ... -- render demo --format ascii|svg|html --out ...`
  (no `--los`), `diff` against `content/diagnostics/demo.*`
  - Result: byte-identical - overlay-absent output unchanged.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
  - Result: `FSharp.Core 10.1.303` only; no transitive package, no
    `ProjectReference`.
- Command: source scan of `src/CommandoWar.Sim/*.fs` for
  `godot|mibo|monogame|raylib|DateTime|Stopwatch|System.Random|Dictionary|HashSet|groupBy|float`
  - Result: only doc-comment prose ("floating point" in `Sight.fs`,
    `Terrain.fs`, `Diagnostics.fs`; a future Godot `.tscn` reader named in
    `Scenario.fs` / `Diagnostics.fs`). No type, API, or value match.
- Command: source scan of `src/CommandoWar.Headless/*.fs` for a graphics
  framework
  - Result: only doc-comment mentions (Godot / Mibo). No code match.
- Command: `git status --porcelain`
  - Result: modified `PROJECT_STATE.yaml`, `content/diagnostics/README.md`,
    `docs/04`, `docs/06`, `docs/11`, `docs/12`,
    `docs/ledger/2026-09-03-TASK-010-*`, `docs/ledger/2026-09-03-TASK-011-*`,
    `src/CommandoWar.Sim/{CommandoWar.Sim.fsproj,Diagnostics.fs}`,
    `src/CommandoWar.Headless/{CommandoWar.Headless.fsproj,DiagnosticRender.fs,Program.fs}`,
    `tasks/TASK-010-*`, `tasks/TASK-011-*`,
    `tests/CommandoWar.Sim.Tests/{CommandoWar.Sim.Tests.fsproj,DiagnosticsTests.fs}`;
    new `src/CommandoWar.Sim/Sight.fs`, `src/CommandoWar.Headless/LosDemo.fs`,
    `tests/CommandoWar.Sim.Tests/SightTests.fs`,
    `content/diagnostics/los.ascii.txt`, `content/diagnostics/los.svg`,
    `tasks/TASK-012-LINE-OF-SIGHT.md`, this file. Nothing under the client
    spikes, `src/_scratch`, or `CommandoWar.slnx`; `Canonical.fs`,
    `Simulation.fs`, `Fixture.fs`, `FixtureTests.fs`, and `Setup.sixAgentWorld`
    untouched.

### Evidence

- **`LineOfSight` shape:** `{ Visible: bool; Path: Cell[]; Blocker: Cell
  option }`. `Path.[0]` = origin, `Path.[^0]` = target.
- **Corner-rule pin** (`SightTests`): terrain with opaque `(4,3)` and `(3,4)`,
  ray `(2,2)->(5,5)` -> `Path` `[(2,2);(3,3);(4,4);(5,5)]`, `Visible = false`,
  `Blocker = Some (4,3)`. With only one of the two opaque -> `Visible = true`.
- **Elevation-rule pin:** `(4,4)` at elevation 3, endpoints at 0 -> blocked at
  `(4,4)`; move one endpoint onto an elevation-3 cell -> `3 > 3` false ->
  visible.
- **Symmetry:** `Sight.visible t a b = Sight.visible t b a` held for all 400
  ordered pairs over `x,y in -1..8` on the mixed hand-built terrain.
- **LOS demo footer** (`los.ascii.txt`): `tick 0 | hash 0x176C8FDC85077840
  (format 1) | draws 0 | agents 2 | cover-edges 0 | events 0`. The five
  overlay lines: `sight (1,1) -> (10,1): clear`; `sight (1,4) -> (10,4):
  blocked at (5,4)`; `sight (7,2) -> (10,5): clear`; `sight (1,7) -> (6,7):
  blocked at (3,7)`; `sight (6,5) -> (11,10): blocked at (9,7)`.
- **Fixture pin:** `0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`, 33 events,
  `Canonical.FormatVersion` 1 - unmoved (`SightTests`, `FixtureTests`,
  `DiagnosticsTests`).

### Deviations and unresolved issues

- **`SightRay` carries the traced `cells`** (not just `from`/`target`/
  `blocked` as the task sketched). The renderer needs the real supercover path
  to draw `*`; recomputing a line in the renderer would diverge from
  `Sight.trace`. Consistent with `Cells` carrying its own cell array.
- **`Blocker` for a corner block is the origin-side row neighbour** (`n1 =
  { X = cur.X; Y = prev.Y }`), which is not on `Path`. Documented on the
  record. `visible` symmetry is unaffected (it does not depend on `Blocker`).
- **`--los` over the `fixture` / command-log target** works (empty terrain ->
  every in-bounds ray visible) but is not golden-pinned; only the `los`
  target is.
- **The LOS demo cover grid is empty** (no cover authored) so the ASCII
  render carries an all-`.` cover block. Harmless; kept for renderer
  consistency.
- **No consumer.** Perception (B-015) is the first reader; nothing in
  `Simulation.step` touches `Sight` this task.
- Clean build in the working tree (existing `bin/`/`obj/`), not a fresh clone.

### Documents updated

- `tasks/TASK-012-LINE-OF-SIGHT.md` (new; status `review`)
- `tasks/TASK-010-TERRAIN-GRID.md`, `tasks/TASK-011-DIAGNOSTIC-VISUALISATION.md`
  (`review -> done`)
- `docs/04_SIMULATION_SPEC.md` (section 9 "Realised by TASK-012")
- `docs/06_CONTENT_AND_PRESENTATION.md` (section 4: High occlusion consumer)
- `docs/11_BACKLOG.md` (TASK-012 row + B-009 `-> active` + B-015 dep note;
  TASK-010/TASK-011/B-008/B-012a `-> done`)
- `docs/12_PROGRESS_LEDGER.md` ("Pinned facts" Green tests `115 -> 128`,
  "Confirmed by" -> TASK-012; index rows for TASK-012 and the TASK-010/011
  acceptance)
- `docs/ledger/2026-09-03-TASK-010-terrain-grid.md`,
  `docs/ledger/2026-09-03-TASK-011-diagnostic-visualisation.md` (Review blocks
  accepted 2026-09-04)
- `content/diagnostics/README.md` (los goldens + regeneration commands)
- `PROJECT_STATE.yaml` (`active_work -> TASK-012`)
- this entry

### AGENTS.md / docs/09 section 8 standing rule

No rule change. The rule was honoured: TASK-012 adds spatial capability
(point-to-point line of sight), so it extends the diagnostic frame with an
`Overlay` case (`SightRay`) and adds golden visualiser output
(`content/diagnostics/los.ascii.txt`, `los.svg`) with an explicit regeneration
command in `content/diagnostics/README.md`. The frame and its renderers stay
observers only - `Diagnostics.frame` / `frameOf` never emit an overlay, and
nothing in `Simulation.step` calls `Sight`.

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: integer supercover LOS with a symmetric corner rule and a minimal
  ridge-occlusion elevation rule; symmetry pinned by a property test and
  golden examples; `SightRay` overlay + `--los` render option + LOS demo
  goldens; `Canonical.encode` and all pinned fixture values unmoved; no tick
  phase consumes it; 128 tests green.
