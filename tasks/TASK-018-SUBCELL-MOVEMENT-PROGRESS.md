# TASK-018: Sub-cell movement progress within an edge

Status: done
Owner: Dave
Phase: P2
Gate: G2 (deterministic core); realises backlog B-011c (narrowed)
Size: M

## Objective

Give the Navigation and movement phase docs/04 section 8 steps 4-5 ("advance
movement progress by an integer amount each tick; enter the next cell when
progress reaches the threshold"): agents no longer advance a flat one cell per
tick regardless of terrain; crossing a cell with an above-base
`Terrain.moveCost` now takes proportionally more ticks.

## Scope down (split to B-011d)

Formation slots are **not** realised by this task. B-011c originally bundled
formation slots with sub-cell progress because both were flagged as needing a
`Canonical.FormatVersion` bump, but on inspection they share no design
surface: sub-cell progress is a bounded, well-specified mechanical change,
while formation slots need a squad/formation domain concept that does not
exist anywhere in `Domain.fs` or `Scenario.fs` yet (closer to
`docs/05_COMMAND_AND_AGENT_AI.md` than to movement mechanics). Split to
**B-011d**.

## Central decisions (recorded in the ledger)

- **Threshold and increment reuse existing concepts — no new constant.** The
  threshold to enter a cell is `Terrain.moveCost` of that cell, the same value
  `Pathfinding` already uses as its A* edge weight; the per-tick increment is
  `Terrain.BaseMoveCost` (1). On every scenario pinned before this task, every
  traversed passable cell already costs exactly `BaseMoveCost`, so threshold =
  increment = 1 everywhere and agents still advance exactly one cell per tick,
  byte-for-byte the same position/event sequence as before.
- **`AgentState.Progress: int` is genuine new canonical state, not a derived
  cache.** Unlike `Route`, it cannot be recomputed from `Position` alone:
  `Position` does not change while an edge is in progress, so nothing else
  records how many ticks have been spent on it. `Canonical.encode` gains it
  (right after `Position`) and `Canonical.FormatVersion` bumps 1 -> 2.
- **Every pinned hash moves; this is a byte-layout artifact, not a behaviour
  change.** The fixture, all five replay-corpus entries (four pre-existing
  plus the new `slow-terrain`), and every `content/diagnostics/*` golden that
  embeds a hash/format footer are regenerated. Tick counts and event counts
  are unchanged across every pre-existing entry — confirmed by diff, not
  assumed — because `Progress` is 0 at every one of their post-tick
  checkpoints.
- **Reservation (TASK-017) generalises without new state.** Pass 2's
  contention test changes from "every `Advancing` intent" to "every
  `Advancing` intent that would *complete* this tick"
  (`startProgress + BaseMoveCost >= Terrain.moveCost next`). An agent still
  mid-edge cannot contend, since it is not entering a cell yet. A claimant
  that loses a contest freezes its progress (does not accumulate) rather than
  resetting or advancing, mirroring how it already freezes `Position` and
  `Route`. No cross-tick booking is added; the TASK-017 FormatVersion
  argument for reservation itself is untouched.
- **Progress resets to 0 whenever the current edge changes** — entering a
  cell, arriving, being blocked, or a replan starting a fresh route — even if
  the agent's stored `Progress` was nonzero from the edge it just left.
  `startProgress` in `MoveOutcome.Advancing` encodes this: 0 whenever `cached`
  fails (a fresh route was computed this tick), `a.Progress` otherwise.
- **No new event for pure progress accumulation.** A tick where an agent's
  progress increases but it does not enter a new cell emits nothing — a
  continuous fact fully recoverable from the resulting `AgentState.Progress`,
  the same treatment an idle agent's tick already gets. `MovementStepped` /
  `MovementCompleted` still fire only on an actual cell entry.
- **A new `slow-terrain` corpus entry, not a hand-built test alone.** None of
  the four existing entries author a `Terrain.moveCost` above
  `BaseMoveCost` on any traversed cell, so none of them can regress-test
  multi-tick accumulation. `slow-terrain` (one cell costing 3) is a genuine
  new regression vector, mirroring the `PathDemo` "costly cell" precedent for
  its terrain authoring.
- **`Progress` is exposed on `AgentSnapshot` and `Diagnostics.AgentMarker`.**
  Cheap and directly justified: `docs/04` section 8 says "the renderer
  interpolates between current and next cells," and both are already built
  straight from `AgentState` with no other transformation. Sub-pixel /
  interpolated rendering in `DiagnosticRender.Svg` is explicitly **not**
  attempted — that is client-renderer polish for a client that does not exist
  yet; the diagnostic renderers show progress as a plain integer.

## Diagnostics

Applies (`docs/09` section 8): `Progress` is new authoritative per-tick
agent state. `AgentMarker` gained a `Progress: int` field (no new `Overlay`
case needed — this is a scalar on an existing marker, not a new spatial
relationship); both `DiagnosticRender.Ascii` (roster line: `progress N`,
omitted at 0) and `.Svg` (a small number beside the agent's circle, omitted at
0) show it. Golden: `content/diagnostics/slow-terrain-tick-002.*` (the new
corpus entry's own mid-accumulation tick, `progress 2` visible) — none of the
pre-existing goldens can show a nonzero value, since `Progress` is 0 at every
one of their checkpoints.

## Allowed scope

- `src/CommandoWar.Sim/Domain.fs` (`AgentState.Progress`, `Agent.create`);
- `src/CommandoWar.Sim/Canonical.fs` (`FormatVersion` 1 -> 2, `writeAgent`);
- `src/CommandoWar.Sim/Simulation.fs` (`navigationAndMovement`: `startProgress`
  on `MoveOutcome.Advancing`, the `wouldComplete` contention test, the
  mid-edge accumulation branch);
- `src/CommandoWar.Sim/Snapshot.fs` (`AgentSnapshot.Progress`) and the
  `output` phase in `Simulation.fs`;
- `src/CommandoWar.Sim/Diagnostics.fs` (`AgentMarker.Progress`,
  `agentMarkers`);
- `src/CommandoWar.Headless/DiagnosticRender.fs` (roster-line and SVG-label
  progress display);
- `src/CommandoWar.Headless/Corpus.fs` (new `costly` helper, new
  `slow-terrain` entry);
- `content/fixtures/SPIKE-FIXTURE.md` (re-pin); `content/replays/*`
  (re-pin four entries; new `slow-terrain.{cwlog,md}`; `CORPUS.md` row);
  every `content/diagnostics/*` golden that embeds a hash/format footer
  (re-pin), plus new `slow-terrain-tick-002.*` and `README.md`;
- `tests/CommandoWar.Sim.Tests/*.fs` (new facts; the pinned fixture-hash and
  `Canonical.FormatVersion` literals scattered across otherwise-unrelated
  test files — `CanonicalHashTests`, `CorpusTests`, `DiagnosticsTests`,
  `FixtureTests`, `PathfindingTests`, `ScenarioTests`, `SightTests`,
  `TerrainTests` — mechanically updated to the new values);
- `docs/04_SIMULATION_SPEC.md` (section 8, 12.7), `docs/09_TEST_STRATEGY.md`
  (section 2.4 entry count and note);
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml`).

## Forbidden scope

- Formation slots (**B-011d**).
- Combat, suppression, appraisal, or any B-014/B-017/B-019+ behaviour.
- A new package or project.
- Sub-pixel / interpolated rendering in `DiagnosticRender.Svg`.
- Touching the client spikes, `src/_scratch`, or `bench/CommandoWar.Benchmarks/`
  — their embedded pre-format-2 fixture-hash comments (TASK-004/005 spike
  artifacts, disposable and frozen since ADR-0001) go stale as a documented,
  expected side effect of this bump, not a defect to fix here.
- A new `cwheadless` verb or a change to an existing verb's behaviour beyond
  what the format bump mechanically requires.

## Acceptance criteria

- [x] An agent crossing a cell whose `Terrain.moveCost` exceeds
      `Terrain.BaseMoveCost` accumulates `AgentState.Progress` over multiple
      ticks before entering it, then resets (`SimulationTests.fs`).
- [x] A completing agent that loses a contested cell freezes its progress and
      resumes correctly next tick — one tick behind, not re-accumulating from
      0 (`SimulationTests.fs`, the frozen-progress fact this task added).
- [x] Every scenario pinned before this task replays with an unchanged
      tick-by-tick position/event sequence; only the hash/format footer
      moves — confirmed by diff on all four pre-existing corpus entries and
      the fixture.
- [x] `Canonical.FormatVersion` is 2; `cwheadless fixture` / `cwheadless
      corpus` reflect it.
- [x] `slow-terrain` corpus entry committed and passing; `Reserved` /
      `Progress` diagnostics golden committed for it.
- [x] `dotnet build CommandoWar.slnx -c Release` = 0 warnings, 0 errors.
- [x] `dotnet list src/CommandoWar.Sim package --include-transitive` =
      `FSharp.Core` only; source scan of `src/CommandoWar.Sim` clean.
- [x] `docs/04` section 8 / 12.7, `docs/09`, backlog rows, ledger index row +
      detail file, `PROJECT_STATE.yaml`, task status updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`
- `dotnet test CommandoWar.slnx -c Release` before and after
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus` before
  and after; `--regenerate` then stage and regenerate again (idempotence)
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  (format, hash, event count)
- `git diff --stat` on every regenerated corpus `.md` — confirm tick counts
  and event counts unchanged on the four pre-existing entries
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
- source scan of `src/CommandoWar.Sim`
- `git status`

## Alternative

Taken: sub-cell progress alone, landed as TASK-018 / B-011c narrowed.
Formation slots split to a new **B-011d** — an unstarted domain concept with
no shared design surface with progress, per this task's own "Scope down."

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
