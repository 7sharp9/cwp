# TASK-012: Deterministic line of sight and opacity

Status: review
Owner: Dave
Phase: P2
Gate: G2 (deterministic core); realises backlog B-009
Size: M

## Objective

Add a framework-neutral, deterministic line-of-sight module to
`CommandoWar.Sim` (`Sight.fs`): a pure query over `Terrain` (opacity and
elevation) that decides whether one cell can see another, plus the traced cell
path and the first blocking cell. This is `docs/04` section 9 ("Line of sight
and cover") and a `docs/07` section 5 required system.

Like `Terrain` and the `Objective` algebra, the module is authored and
queryable but **not consumed by any tick phase yet**: Perception (phase 12.3)
is B-015 in P3. It does not change authoritative behaviour, does not run inside
`Simulation.step`, and does not enter `Canonical.encode`.

## Why this task exists

B-009 (this task) and B-010 (pathfinding) both need the terrain substrate
TASK-010 delivered. Line of sight is the first real consumer of the opacity
layer and the first spatial query whose corner and blocking semantics must be
pinned by golden examples before combat tuning (`docs/04` section 9). B-015
(observations and shared squad tactical knowledge) is the first phase consumer.

## Dependencies

- TASK-010 (terrain grid) accepted and `done`.
- TASK-011 (diagnostic frame + renderers) accepted and `done` (this task adds
  the `SightRay` overlay case and a renderer branch).

## Central decisions

- The module lives in `CommandoWar.Sim` (`Sight.fs`, compiled after
  `Terrain.fs`, before `Domain.fs`). Integer-only: an integer supercover grid
  walk, no floating point, no `System.Math` on doubles. Pure function of
  `Terrain` and two `Cell`s. Total: any input pair yields a defined result,
  including out-of-bounds endpoints (an out-of-bounds endpoint sees nothing).
- **Symmetry is a required property:** `Sight.visible t a b = Sight.visible t
  b a` for every pair. The walk is the classic integer supercover line
  (`decision = (1 + 2*ix) * ny - (1 + 2*iy) * nx`), which visits the
  geometrically-defined supercover set of the segment and is therefore
  direction-independent; an exact lattice-corner crossing is always resolved
  as a single diagonal step regardless of walk direction. The blocking rule is
  evaluated over sets (intermediate path cells; unordered diagonal-step
  neighbour pairs) that are identical in both directions. `Blocker` is
  *not* required to be symmetric (it is the first blocker from the origin end);
  `visible` is.
- **Corner rule:** a diagonal step between two cells is blocked only when
  *both* shared-edge neighbours are opaque (no sight through a solid inner
  corner; sight passes a single wall cell at a diagonal corner). Endpoints
  never block.
- **Elevation rule (minimal, documented):** an intermediate cell blocks sight
  when it is `Terrain.opaque`, OR its elevation is strictly greater than the
  elevation of *both* endpoints (a ridge occludes). Endpoint elevation
  differences do not otherwise grant or deny sight at this stage.

`Canonical.encode` and `Canonical.FormatVersion` are unchanged; `Sight.fs` is a
leaf that nothing authoritative references. The pinned fixture hashes
(`0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`, 33 events) do not move.

## Allowed scope

- `src/CommandoWar.Sim/Sight.fs` (new), `Diagnostics.fs` (the `SightRay`
  `Overlay` case + comment), `CommandoWar.Sim.fsproj`;
- `src/CommandoWar.Headless/DiagnosticRender.fs` (`SightRay` drawing in Ascii
  and Svg; Html inherits), `Program.fs` (`--los` option on the `render` verb +
  a `los` render target), `LosDemo.fs` (new), `CommandoWar.Headless.fsproj`;
- `content/diagnostics/` new golden outputs + README entries;
- tests in `tests/CommandoWar.Sim.Tests/` (`SightTests.fs` new + `.fsproj`;
  any `DiagnosticsTests.fs` additions for the overlay);
- `docs/04` section 9 and `docs/06` section 4 realisation notes;
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml`).

## Forbidden scope

- Any rendering, graphics, or UI framework in `CommandoWar.Sim`; a
  Godot/MonoGame/raylib reference anywhere.
- Floating-point anywhere in `Sight.fs` or the renderer additions.
- Wiring line of sight into `Simulation.step` or any phase; observations,
  contacts, or tactical knowledge (B-015); pathfinding (B-010); combat or
  cover evaluation (B-019); enemy AI (B-022).
- Field-of-view / shadowcasting to all cells (only point-to-point is in
  scope); a lighting or vision-cone model; eye-height or 3D raycasting beyond
  the documented single elevation rule.
- Changing `Simulation.step`, `Canonical.encode` or its format version,
  `Setup.sixAgentWorld`, the shared fixture parameters, the pinned hashes, the
  `Diagnostics.frame` / `frameOf` signatures, or any existing `cwheadless`
  verb's data output (overlay-absent renders included).
- A new dependency or project; an on-disk content format (B-024); `Overlay`
  cases for B-010 / B-011 / B-019.
- Touching the client spikes, `src/_scratch`, `.slnx`; reorganising
  `decisions/` or `tasks/`; destructive git.

## Required work

1. `src/CommandoWar.Sim/Sight.fs`: `LineOfSight { Visible: bool; Path: Cell[];
   Blocker: Cell option }`; `Sight.trace : Terrain -> Cell -> Cell ->
   LineOfSight` and `Sight.visible : Terrain -> Cell -> Cell -> bool` (the
   latter defined in terms of the former). Both total, pure, deterministic,
   integer-only. Module comment: the algorithm, the corner rule, the elevation
   rule, the symmetry guarantee, and "nothing in `Simulation.step` or any
   phase calls this yet; the first consumer is Perception (B-015)".
2. `Diagnostics.fs`: add `| SightRay of from: Cell * target: Cell * cells:
   Cell[] * blocked: Cell option` to the `Overlay` DU and update the DU
   comment so it no longer lists B-009 as pending. `Diagnostics.frame` /
   `frameOf` still never emit an overlay.
3. `DiagnosticRender.fs`: extend `Ascii` and `Svg` to draw a `SightRay`
   overlay (a line of `*` with a distinct blocker glyph `x` on the ASCII
   composite; a dashed line plus a blocker marker in SVG). Html inherits.
   Every existing render is byte-identical when no overlay is present.
4. `src/CommandoWar.Headless/Program.fs`: a repeatable `--los AX,AY:BX,BY`
   option on the `render` verb that attaches a `SightRay` overlay (computed
   via `Sight.trace` over the target's terrain) to the rendered frame(s), plus
   a `los` render target for the demo. No new verb.
5. `src/CommandoWar.Headless/LosDemo.fs`: a focused 12 x 12 LOS fixture built
   through `Scenario.validate` + `World.ofScenario`, showing a clear ray, a
   ray blocked by an opaque wall, a ray grazing a single wall cell at a
   diagonal corner (visible), a ray blocked by an elevation ridge, and a ray
   blocked by a two-wall diagonal corner.
6. Golden outputs under `content/diagnostics/` (`los.ascii.txt`, `los.svg`)
   with the exact regeneration commands in the README. Existing demo/fixture
   goldens unchanged.
7. `tests/CommandoWar.Sim.Tests/SightTests.fs` (new, registered in the
   `.fsproj`): clear sight over horizontal / vertical / diagonal lines with
   `Path` starting at the origin and ending at the target; an opaque cell
   between endpoints blocks and is the reported `Blocker`; the corner rule
   (single wall does not block, two walls do), golden-pinned; the elevation
   rule (ridge higher than both blocks; not when an endpoint is at or above);
   a symmetry property test over a hand-built terrain and a grid of endpoint
   pairs; out-of-bounds endpoints yield `Visible = false` with no exception;
   determinism (two traces structurally equal); a pin that producing LOS
   diagnostics leaves the shared fixture at `0xF2F3DF0D820AD9AC` /
   `0x838D3AE7DBFB735D`, 33 events, `Canonical.FormatVersion` 1; a renderer
   test that a `SightRay` overlay appears distinctly in ASCII and SVG and
   matches the committed golden, with the overlay-absent render byte-unchanged.
8. Register `Sight.fs`, `LosDemo.fs`, `SightTests.fs` in their `.fsproj`.
   `TreatWarningsAsErrors` clean.
9. `docs/04` section 9: a "Realised by TASK-012" note. `docs/06` section 4:
   note the opacity layer now has a consumer. `AGENTS.md` / `docs/09` section
   8: no rule change; the completion report confirms the standing rule was
   honoured (Overlay case + golden visualiser output added).

## Acceptance criteria

- [x] `src/CommandoWar.Sim/Sight.fs` exists with `LineOfSight`, `Sight.trace`,
      `Sight.visible`, all total, pure, deterministic, integer-only. Module
      comment states no phase calls it.
- [x] `Sight.visible t a b = Sight.visible t b a` for every pair (property
      test over a hand-built terrain and a grid of endpoint pairs, incl.
      out-of-bounds).
- [x] Corner rule and elevation rule implemented as documented and pinned by
      golden examples.
- [x] `Diagnostics.fs` gains the `SightRay` `Overlay` case; `Diagnostics.frame`
      / `frameOf` signatures and behaviour unchanged; `Canonical.encode`
      unchanged; `Canonical.FormatVersion` still 1.
- [x] `DiagnosticRender` draws `SightRay` in ASCII and SVG; overlay-absent
      renders byte-identical (existing demo/fixture goldens unchanged).
- [x] `cwheadless render ... --los AX,AY:BX,BY` (repeatable) + `los` target
      added; `step` / `replay` / `compare` / `fixture` and overlay-absent
      `render` output unchanged.
- [x] `content/diagnostics/los.ascii.txt` + `los.svg` goldens committed with
      README regeneration commands.
- [x] `dotnet test CommandoWar.slnx -c Release` = `Passed: 128` (115 + 12
      `SightTests` + 1 `DiagnosticsTests`).
- [x] `cwheadless fixture` unchanged: initial `0xF2F3DF0D820AD9AC`, final
      `0x838D3AE7DBFB735D`, 33 events.
- [x] `dotnet list ... package --include-transitive` = `FSharp.Core` only, no
      `ProjectReference`; source scan of `src/CommandoWar.Sim` clean of
      `godot|mibo|monogame|raylib|DateTime|Stopwatch|System.Random|Dictionary|HashSet|groupBy|float`
      (doc comments only); `src/CommandoWar.Headless` clean of a graphics
      framework.
- [x] `docs/04` section 9 and `docs/06` section 4 realisation notes written.
- [x] Backlog rows, ledger index row + detail file, `PROJECT_STATE.yaml`, and
      task status updated. No forbidden scope entered.

## Required verification

- `dotnet build CommandoWar.slnx -c Release` (0 warnings, 0 errors)
- `dotnet test CommandoWar.slnx -c Release` before and after
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
- `dotnet run --project src/CommandoWar.Headless -c Release -- render los
  --los ... --format ascii|svg`, byte-compared against the committed goldens;
  one `render demo` without `--los` byte-compared against the existing golden
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
- source scan of `src/CommandoWar.Sim` and `src/CommandoWar.Headless`
- `git status`

## Rollback or removal

`Sight.fs` is a leaf: removing it, the `SightRay` `Overlay` case and its
renderer branches, `LosDemo.fs`, the `--los` option and `los` target, the
`content/diagnostics/los.*` goldens, and `SightTests.fs` restores the
pre-task state without touching `Simulation.step`, `Canonical.encode`, the
shared fixture, or any other verb.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Alternative

If the corner rule or the elevation rule could not be made both symmetric and
tactically sensible with an integer grid walk, the split was: land `Sight.fs`
with opacity blocking + the symmetric corner rule + the `Overlay` case +
goldens + tests as TASK-012, and take the elevation-occlusion rule as
TASK-012b with a decision note. It was not needed: the recommended supercover
walk is symmetric (pinned by the property test) and the ridge-occlusion
elevation rule is minimal, symmetric, and sufficient for the bridge mission.
