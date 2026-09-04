## 2026-09-04 - TASK-015 - Navigation and movement phase: `Pathfinding`-driven single-agent executor

**Owner:** Dave with coding-agent assistance
**Source revision:** `0003932` (Add pathfinding and benchmark harness)
**Environment:** Windows 11 Pro 26200 (25H2); .NET SDK 10.0.303; .NET 10.0.11;
AMD Ryzen 7 9700X; xUnit 2.9.3
**Status change:** TASK-015 `proposed -> active -> review`; TASK-014 `review ->
done` (finalised as the precondition of this task)

### Precondition: TASK-014 finalised

TASK-014 was committed (in `0003932`) but not accepted. Finalised with the
TASK-009 / TASK-013 pattern, no re-run of its full verification:

- `tasks/TASK-014-BENCHMARK-HARNESS.md` `Status: review -> done`;
- `docs/11_BACKLOG.md`: the "Current work" TASK-014 row and the "Planned
  simulation work" B-013 row `active -> done`, task-file link kept;
- `docs/ledger/2026-09-04-TASK-014-benchmark-harness.md` Review block
  `Accepted: pending -> yes (2026-09-04)` with an acceptance note; the
  `docs/12` index row's Accepted cell likewise, status change `-> done`.
- Check performed: `git status` scope (every changed path is TASK-014 or this
  task) plus one `dotnet test CommandoWar.slnx -c Release` = `Passed: 145`
  (unchanged). Both passed, so finalisation proceeded.

### Central decisions

- **`AgentState.Route` is a non-canonical derived cache; `FormatVersion` stays
  1.** `AgentState` gains `Route: MovementPath option`
  (`MovementPath = { Cells: Cell[]; Cursor: int; Cost: int }`). It is per-tick
  mutable but is a pure deterministic function of `(Position, Destination,
  Terrain)` at every tick, so it falls under `docs/04` section 17 "derived
  caches either excluded or normalised" and is **excluded** from
  `Canonical.encode`. Two runs of the same inputs produce identical caches
  (`Pathfinding.findWithin` is total, pure, integer-only, deterministic -
  TASK-013), so it cannot diverge. Including it would re-pin `FixtureTests.fs`,
  `ScenarioTests.fs`, `SPIKE-FIXTURE.md`, and the two spike evidence sets to
  encode a function of data already in the image - the churn the ADR-0002
  amendment rejected for `Terrain`. Pinned by
  `CanonicalHashTests.the followed-path cache is outside the canonical image`.
- **The shared fixture hashes and 33-event count do not move.** Fixture agent 3
  travels `(0,3) -> (20,14)` on empty terrain. A* with the total-order frontier
  key `(f, h, row-major idx)` and N/E/S/W neighbour order expands the
  lowest-index cell among equal `f`/`h`, which advances the X axis before the Y
  axis - the identical sequence the retired `PlaceholderMovement` produced
  (20 east then 11 south). With `Route` out of the canonical image every
  per-tick encoded image is byte-identical to the placeholder run. Verified by
  `cwheadless fixture` (`0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`, 33 events,
  format 1) and the unchanged `FixtureTests` / `PathfindingTests` pins.
- **Per-agent query budget = `Width * Height`.** `Pathfinding.findWithin` is
  called with the full-grid ceiling, passed explicitly (not via
  `Pathfinding.find`) so the executor owns the number and B-011b can add a
  tighter combined per-tick multi-agent budget without touching
  `Pathfinding.find`. `content/benchmarks/BASELINE.md` records that a single
  legitimate query on an adversarial 40x40 map already closes ~73% of the grid,
  so a single-agent per-query budget must not be cut below the full grid.
- **`PlaceholderMovement` (`Movement.fs`) removed.** Fully superseded; the
  module and its `CommandoWar.Sim.fsproj` compile entry are deleted, and the
  two doc-comment references in `Terrain.fs` / `Pathfinding.fs` updated.
- **`Diagnostics.frameOf` emits a `PlannedPath` overlay per following agent.**
  The followed path is authoritative-derived spatial state, so the `docs/09`
  section 8 rule wants it visible. `Diagnostics.frame` still emits nothing.
- **Scope held to the single-agent executor.** Cell reservation, deadlock
  avoidance, formation slots, and sub-cell movement progress are split to a new
  backlog row **B-011b**; B-011 stays `active` pointing at this task.

### Changes

- **`src/CommandoWar.Sim/Events.fs`.** New `EventBody` case
  `MovementBlocked of agent: AgentId * at: Cell * target: Cell`.
- **`src/CommandoWar.Sim/Domain.fs`.** New `MovementPath` record; `AgentState`
  gains `Route: MovementPath option`; `Agent.create` initialises it to `None`.
- **`src/CommandoWar.Sim/Simulation.fs`.** `StepState` gains an immutable
  `Terrain` field (set from `state.Terrain` in `step`). `navigationAndMovement`
  rewritten: reuse the cached `Route` when its cursor tracks the agent, it
  still targets the current destination, and its next cell is still passable;
  otherwise recompute with `Pathfinding.findWithin terrain pos dest
  (Width*Height)`; advance one cell; emit `MovementStepped` /
  `MovementCompleted`, or `MovementBlocked` (clearing the destination and
  route) on `NoPath` / `BudgetExhausted` / `InvalidEndpoint`.
- **`src/CommandoWar.Sim/Canonical.fs`.** Comment on `writeAgent` recording
  that `Route` is deliberately not encoded. No byte-layout change.
- **`src/CommandoWar.Sim/Diagnostics.fs`.** `eventMarker` gains the
  `MovementBlocked -> "movement-blocked"` case; new private `routeOverlays`;
  `frameOf` sets `Overlays = routeOverlays result.State`; module and `Overlay`
  doc comments updated (B-011 -> B-011b for the reservation overlay).
- **`src/CommandoWar.Sim/Movement.fs`.** Deleted;
  `CommandoWar.Sim.fsproj` compile entry removed.
- **`src/CommandoWar.Sim/Terrain.fs`, `Pathfinding.fs`.** Doc comments updated
  to reflect that the movement phase now consumes `Terrain` / `Pathfinding` and
  that `PlaceholderMovement` is gone.
- **`tests/CommandoWar.Sim.Tests/SimulationTests.fs`.** Five new facts: route
  around an impassable wall one passable cell per tick; the agent follows
  exactly the `Pathfinding.find` cell sequence; `MovementBlocked` + destination
  cleared for a walled-off goal; a `MoveTo` onto an impassable cell is accepted
  at intake then blocked; a mid-route `Route` cache that clears on arrival.
- **`tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`.** Two new facts: the
  mid-route fixture frame carries one `PlannedPath` overlay and is byte-equal
  to `fixture-mid-route.ascii.txt` / `.svg`; `frameOf` emits no overlay once
  every agent is at rest.
- **`tests/CommandoWar.Sim.Tests/CanonicalHashTests.fs`.** One new fact: a
  state with a `Route` cache and the same state with `Route = None` encode and
  hash identically; `FormatVersion` is 1.
- **`content/diagnostics/fixture-mid-route.ascii.txt` / `.svg`.** New goldens
  (fixture tick 25, agent 3 at `(20,8)` en route to `(20,14)`).
- **`content/diagnostics/demo.html`.** Regenerated: `runFrames` frames now
  carry the `PlannedPath` overlay on the moving ticks.
- **`content/diagnostics/README.md`.** New golden rows, updated `demo.html`
  row, regeneration commands for the two new files.
- **`docs/04_SIMULATION_SPEC.md`.** Section 8 "Realised by TASK-015" note
  (steps 2/4/5/6, the `Route` cache, the fixture argument, B-011b split);
  section 12.7 realisation note; section 17 "TASK-015 note" (derived cache
  excluded).
- **`docs/09_TEST_STRATEGY.md`.** Section 8: "TASK-015 applied this rule" note.
- **Control.** `tasks/TASK-015-MOVEMENT-EXECUTOR.md` (new); `docs/11_BACKLOG.md`
  (TASK-015 "Current work" row `active`; B-011 `-> active` with the task-file
  link, single-agent note; new B-011b row `proposed`; TASK-014 / B-013
  `-> done`); `PROJECT_STATE.yaml` (`active_work -> TASK-015`, gates / gate /
  phase / framework_decision unchanged); `docs/12_PROGRESS_LEDGER.md` (index
  rows for TASK-015 and the TASK-014 acceptance; no "Pinned facts" change - the
  green count moves 145 -> 153 but that pinned row is owned by TASK-014 and is
  refreshed here);
  `docs/ledger/2026-09-04-TASK-014-benchmark-harness.md` (Review block
  accepted); this entry.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (all four projects,
    benchmark project included).
- Command: `dotnet test CommandoWar.slnx -c Release` (before, after the
  TASK-014 finalisation edits)
  - Result: `Passed! - Failed: 0, Passed: 145, Skipped: 0, Total: 145`.
- Command: `dotnet test CommandoWar.slnx -c Release` (after the full TASK-015
  change)
  - Result: `Passed! - Failed: 0, Passed: 153, Skipped: 0, Total: 153`. The
    eight added facts: 5 in `SimulationTests`, 2 in `DiagnosticsTests`, 1 in
    `CanonicalHashTests`. Every previously green test stayed green; the only
    golden that moved is `demo.html` (regenerated - the demo run's moving
    ticks now carry the `PlannedPath` overlay).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  - Result: initial `0xF2F3DF0D820AD9AC`, tick 1 `0xC848D905A9CAD13F`, tick 31
    `0x25315447F9D0E230`, final (tick 40) `0x838D3AE7DBFB735D` (format 1),
    33 events - unchanged. The A* executor reproduces the placeholder cell
    sequence and `Route` is outside the canonical image.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- render
  fixture --tick 25 --format ascii` (and `--format svg`)
  - Result: byte-identical to the committed
    `content/diagnostics/fixture-mid-route.ascii.txt` / `.svg`
    (`DiagnosticsTests` pins the same render). The tick-25 hash printed in the
    footer, `0xD0FBA852FE8E758D`, matches `SPIKE-FIXTURE.md` row 25.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
  - Result: `FSharp.Core 10.1.303` only; no transitive package.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj reference`
  - Result: "There are no Project to Project references".
- Command: source scan of `src/CommandoWar.Sim/*.fs` for
  `float|stopwatch|datetime|system\.random|godot|monogame|raylib`
  - Result: only doc-comment prose ("no floating point", "no `Stopwatch`",
    Godot named as a future content reader / renderer). No type, API, or value
    match.
- Command: `git status --porcelain`
  - Result: TASK-014 finalisation (`tasks/TASK-014-*`,
    `docs/ledger/2026-09-04-TASK-014-*`) plus, for this task: modified
    `src/CommandoWar.Sim/{Events,Domain,Simulation,Canonical,Diagnostics,
    Terrain,Pathfinding}.fs`, `CommandoWar.Sim.fsproj`; deleted `Movement.fs`;
    modified `tests/CommandoWar.Sim.Tests/{SimulationTests,DiagnosticsTests,
    CanonicalHashTests}.fs`; new `content/diagnostics/fixture-mid-route.*`;
    modified `content/diagnostics/demo.html`, `README.md`; modified `docs/04`,
    `docs/09`, `docs/11`, `docs/12`, `PROJECT_STATE.yaml`; new
    `tasks/TASK-015-MOVEMENT-EXECUTOR.md`, this file. Nothing under the client
    spikes, `src/_scratch`, `bench/`, `Fixture.fs`, `SPIKE-FIXTURE.md`, or any
    `cwheadless` verb output beyond the two new render targets' goldens.

### Evidence

- **Fixture pin:** `0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`, 33 events,
  `Canonical.FormatVersion` 1 - unmoved (`cwheadless fixture`, `dotnet test`).
- **Canonical exclusion:** `CanonicalHashTests` - a `Route`-carrying state and
  the same state with `Route = None` encode byte-identically and hash equally.
- **Mid-route golden:** `content/diagnostics/fixture-mid-route.ascii.txt` shows
  agent 3 (`@`) at `(20,8)` on the `+` path between `S (0,3)` and `G (20,14)`,
  overlay line `path (0,3) -> (20,14): reached, cost 31`.
- **Executor behaviour:** `SimulationTests` - detour around a wall, exact
  `Pathfinding` sequence, `MovementBlocked` on no path and on an impassable
  target, `Route` cleared on arrival.

### Deviations and unresolved issues

- **`demo.html` golden regenerated.** `Diagnostics.frameOf` now emits the
  `PlannedPath` overlay, so every moving tick of the demo run's SVG gains a
  polyline. This is the intended `docs/09` section 8 outcome; the golden is
  updated and `README.md` notes it.
- **Step 6 (replan) is currently unreachable in practice.** Terrain is
  immutable within a run and agent-agent collision is B-011b, so an agent's
  cached next cell can never become impassable mid-route today. The branch is
  written and covered against a terrain vector (the `impassable` wall tests
  force the recompute path on the *first* query) so the structure B-011b hooks
  into exists; a test that mutates terrain mid-run to trigger the in-flight
  replan is deferred with B-011b (nothing can mutate terrain yet).
- **Command intake still range-checks the target only.** A `MoveTo` onto an
  impassable cell is accepted, then the executor emits `MovementBlocked`
  (`InvalidEndpoint`). Rejecting it at intake instead is a `docs/04` section
  12.1 question left for B-014 (command validation); the current behaviour is
  explicit and tested.
- **50-agent movement allocation.** The `Array.copy s.Agents` per accepted
  command in `commandIntake` (BASELINE.md observation) is unchanged; this task
  did not touch command intake. B-011b / a later pass owns it.
- **`bench/CommandoWar.Benchmarks/` comments say "placeholder movement".** The
  benchmark project is out of scope for this task; its `MovementBenchmarks`
  still build and run (they drive `Simulation.step`, not the removed module).
  The stale label is noted for whoever re-runs the harness for B-011b.

### Documents updated

- `tasks/TASK-015-MOVEMENT-EXECUTOR.md` (new)
- `tasks/TASK-014-BENCHMARK-HARNESS.md` (`review -> done`)
- `src/CommandoWar.Sim/{Events,Domain,Simulation,Canonical,Diagnostics,Terrain,Pathfinding}.fs`,
  `CommandoWar.Sim.fsproj`; `Movement.fs` deleted
- `tests/CommandoWar.Sim.Tests/{SimulationTests,DiagnosticsTests,CanonicalHashTests}.fs`
- `content/diagnostics/fixture-mid-route.ascii.txt` / `.svg` (new),
  `demo.html` (regenerated), `README.md`
- `docs/04_SIMULATION_SPEC.md` (sections 8, 12.7, 17)
- `docs/09_TEST_STRATEGY.md` (section 8)
- `docs/11_BACKLOG.md` (TASK-015 row; B-011 `-> active`; B-011b new;
  TASK-014 / B-013 `-> done`)
- `docs/12_PROGRESS_LEDGER.md` (index rows for TASK-015 and the TASK-014
  acceptance)
- `docs/ledger/2026-09-04-TASK-014-benchmark-harness.md` (Review block accepted)
- `PROJECT_STATE.yaml` (`active_work -> TASK-015`)
- this entry

### AGENTS.md / docs/09 section 8 standing rule

Applied, not changed. TASK-015 adds authoritative-derived spatial state (the
path an agent follows), so `Diagnostics.frameOf` now emits a `PlannedPath`
overlay for it and `content/diagnostics/fixture-mid-route.*` is the golden a
reviewer reads to see an agent mid-route. The frame and renderers stay
observers: nothing in `Simulation.step` constructs a frame.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-04). Single-agent executor accepted as the realisation
  of B-011; multi-agent reservation / formation slots / sub-cell progress carried
  forward as B-011b. Finalised as the predecessor of TASK-016.
- Notes: `Pathfinding`-driven single-agent Navigation and movement executor
  replacing `PlaceholderMovement` (`docs/04` section 8 steps 2/4/5/6);
  `AgentState.Route` a non-canonical derived cache, `Canonical.FormatVersion`
  stays 1, shared fixture hashes and 33-event count unmoved; `MovementBlocked`
  event; `Diagnostics.frameOf` emits a `PlannedPath` overlay per following
  agent with new `fixture-mid-route.*` goldens and a regenerated `demo.html`;
  `dotnet test` 145 -> 153; cell reservation / formation slots / sub-cell
  progress split to B-011b.
