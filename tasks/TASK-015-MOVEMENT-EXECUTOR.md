# TASK-015: Navigation and movement phase — `Pathfinding`-driven single-agent executor

Status: done
Owner: Dave
Phase: P2
Gate: G2 (deterministic core); realises backlog B-011 (single-agent executor only)
Size: M

## Objective

Replace `PlaceholderMovement` inside the Navigation and movement phase
(`Simulation.fs`, phase 12.7) with an executor that consumes the TASK-013
`Pathfinding` module (`docs/04_SIMULATION_SPEC.md` section 8 "Initial movement
progression", steps 2, 4, 5, 6):

- on a new destination, compute a path with `Pathfinding.findWithin` over
  `WorldState.Terrain`;
- each tick advance the agent exactly one cell along that path;
- recompute the path when the next path cell is no longer passable;
- emit `MovementStepped` / `MovementCompleted` as today, plus a new
  `MovementBlocked` event when no path exists.

The agent's current path and cursor are stored on `AgentState` as a
**non-canonical derived cache** (see "Central decisions"). It stays 4-connected
and integer-only.

## Scope down (deferred to B-011b)

This task is the **single-agent executor only**. Deferred, with a note in
`docs/11` and here:

- cell reservation / short-horizon deadlock avoidance between agents;
- formation slots;
- movement progress within an edge (this task stays at one cell per tick).

This mirrors how TASK-012 / TASK-013 stayed size M by deferring their consumers.

## Dependencies

- TASK-013 (`Pathfinding`) accepted and `done` (realises B-010).
- TASK-010 (`Terrain`) accepted and `done`.
- TASK-011 (`DiagnosticFrame`, `PlannedPath` overlay) accepted and `done`.
- Finalisation of TASK-014 / B-013 to `done` (precondition only).

## Central decisions

### The path cache is not in the canonical image; `Canonical.FormatVersion` stays 1

`AgentState` gains `Route: MovementPath option` where
`MovementPath = { Cells: Cell[]; Cursor: int; Cost: int }`. This is per-tick
mutable, but it is a **derived cache**: at every tick it is a pure
deterministic function of `(agent.Position, agent.Destination,
WorldState.Terrain)`. `Terrain` is immutable within a run (ADR-0002 amendment);
`Position` and `Destination` are already in the canonical image; and
`Pathfinding.findWithin` is total, pure, integer-only, and deterministic
(TASK-013). Two runs of the same inputs therefore produce identical caches, so
the cache cannot diverge.

`docs/04_SIMULATION_SPEC.md` section 17 already says "derived caches either
excluded or normalised". The cache is **excluded** from `Canonical.encode`.
Including it would re-pin `FixtureTests.fs`, `ScenarioTests.fs`,
`content/fixtures/SPIKE-FIXTURE.md`, and the two retained framework-spike
evidence sets to encode a function of data already in the image — the same
churn the ADR-0002 amendment rejected for `Terrain`. `FormatVersion` stays 1;
it bumps only when a genuinely independent piece of per-tick authoritative
state is added.

### The shared fixture hashes and event count do not move

The fixture agent travels `(0,3) -> (20,14)` on empty terrain. A* with the
documented total-order frontier key and N/E/S/W neighbour order expands the
lowest-row-major cell among equal `f`/`h`, which advances the X axis before the
Y axis — the identical cell sequence `PlaceholderMovement` produces (X first,
then Y). With the path field excluded from the canonical image, every per-tick
encoded image is byte-identical to the placeholder run. The pinned hashes
`0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D` and the 33-event count are
unchanged. Verified by `cwheadless fixture` and `FixtureTests` /
`PathfindingTests`.

### Per-agent query budget

The executor calls `Pathfinding.findWithin terrain pos dest budget` with
`budget = Terrain.Bounds.Width * Terrain.Bounds.Height` — the full-grid
ceiling. `content/benchmarks/BASELINE.md` records that a single legitimate
query on an adversarial 40x40 map already closes ~73% of the grid, so the
`Width * Height` ceiling must not be cut close to a typical closed set for a
single-agent query. The number is passed explicitly (not via
`Pathfinding.find`) so the executor owns it and B-011b can introduce a tighter
*combined per-tick* multi-agent budget without touching `Pathfinding.find`'s
default cap.

### Replan branch

When the agent's cached next path cell is no longer `Terrain.passable`, or the
cache no longer matches `(Position, Destination)`, the path is recomputed from
the current cell. With static terrain and no agent-agent collision (B-011b),
this branch is currently unreachable in practice: it is written and tested
against a terrain vector so the structure B-011b hooks into exists.

### `MovementBlocked`

New `EventBody` case `MovementBlocked of agent: AgentId * at: Cell * target:
Cell`, emitted when `Pathfinding.findWithin` returns `NoPath`,
`BudgetExhausted`, or `InvalidEndpoint` (the latter covers a `MoveTo` to an
impassable cell, which command intake accepts because it only range-checks the
target). The destination and the path cache are cleared so the agent does not
retry every tick.

## Diagnostics

This task adds authoritative spatial state (the path an agent is following), so
the `docs/09` section 8 standing rule applies. The `PlannedPath` overlay case
already exists (TASK-013). `Diagnostics.frameOf` now emits one per agent that is
following a route (`from = Cells.[0]`, `target = destination`, full `Cells`,
`Cost`, `reached = true`). `Diagnostics.frame` still emits nothing (a bare
`WorldState` has no per-tick history and the overlay describes a followed
route, which reads naturally from a completed step). New goldens
`content/diagnostics/fixture-mid-route.ascii.txt` / `.svg` show fixture agent 3
mid-route at tick 25 with its planned path; `demo.html` is regenerated (its
`runFrames` frames now carry the overlay on the moving ticks). The frame stays
an observer: nothing in a phase constructs it.

## Allowed scope

- `src/CommandoWar.Sim/Events.fs` (`MovementBlocked`), `Domain.fs`
  (`MovementPath` type, `AgentState.Route`, `Agent.create`), `Simulation.fs`
  (the executor), `Diagnostics.fs` (`eventMarker`, `frameOf` overlay,
  module-doc note), `Canonical.fs` (doc note only, no encoding change);
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`, `DiagnosticsTests.fs`,
  `PathfindingTests.fs` (executor + overlay coverage);
- `content/diagnostics/fixture-mid-route.ascii.txt` / `.svg` (new),
  `content/diagnostics/demo.html` (regenerated), `content/diagnostics/README.md`;
- `docs/04` sections 8 / 12.7 / 17 realisation notes; `docs/09` section 8 note;
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml`).

## Forbidden scope

- Cell reservation, formation slots, sub-cell movement progress, dynamic
  multi-agent deadlock handling (all B-011b); combat / cover-aware routing
  (B-019); enemy AI (B-022).
- Diagonal / 8-connected movement; any float, `Stopwatch`, `DateTime`,
  `System.Random`, or framework type in the executor.
- A new dependency or project; touching the client spikes, `src/_scratch`, or
  `bench/CommandoWar.Benchmarks/`.
- Changing `Setup.sixAgentWorld`, the shared fixture parameters, the pinned
  fixture hashes / event count, `Canonical.encode` / `Canonical.FormatVersion`,
  or `Pathfinding.find` / `findWithin`'s default cap and signature.

## Acceptance criteria

- [x] Navigation and movement phase computes a `Pathfinding.findWithin` path on
      a new destination, advances one cell per tick along it, and recomputes it
      when the next cell is impassable.
- [x] `AgentState.Route` carries the path + cursor + cost; it is excluded from
      `Canonical.encode`; `Canonical.FormatVersion` stays 1.
- [x] `MovementBlocked` is emitted (and destination + route cleared) when no
      path exists; `MovementStepped` / `MovementCompleted` behave as before.
- [x] `dotnet build CommandoWar.slnx -c Release` = 0 warnings, 0 errors.
- [x] `dotnet test CommandoWar.slnx -c Release` grows from 145 by the new
      executor / overlay tests; every previously green test stays green.
- [x] `cwheadless fixture` unchanged: `0xF2F3DF0D820AD9AC` /
      `0x838D3AE7DBFB735D`, 33 events.
- [x] `Diagnostics.frameOf` emits a `PlannedPath` overlay for a following
      agent; `content/diagnostics/fixture-mid-route.*` committed; `demo.html`
      regenerated; both byte-compared.
- [x] `dotnet list src/CommandoWar.Sim package --include-transitive` =
      `FSharp.Core` only.
- [x] Source scan of `src/CommandoWar.Sim` for
      `float|Stopwatch|DateTime|System.Random|godot` = no code matches.
- [x] `docs/04` sections 8 / 12.7 / 17, `docs/09` section 8, backlog rows,
      ledger index row + detail file, `PROJECT_STATE.yaml`, task status updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`
- `dotnet test CommandoWar.slnx -c Release` before and after
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
- `dotnet run --project src/CommandoWar.Headless -c Release -- render fixture
  --tick 25 --format ascii/svg` byte-compared against the committed goldens
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
- source scan of `src/CommandoWar.Sim`
- `git status`

## Alternative

If threading the path onto `AgentState` forces a canonical-format change wider
than expected, land the executor with the path in a separate per-tick
`MovementState` store keyed by `AgentId` as TASK-015 and take the `AgentState`
merge as later cleanup. If replan-on-invalid cannot be made deterministic and
testable in size M, land compute + follow (steps 2, 4, 5) as TASK-015 and split
replanning (step 6) into TASK-015b.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
