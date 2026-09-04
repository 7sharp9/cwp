## 2026-09-04 - TASK-018 - Sub-cell movement progress within an edge

**Owner:** Dave with coding-agent assistance
**Source revision:** `7777ea2` (Add same-tick cell reservation handling)
**Environment:** Windows 11 Pro 26200 (25H2); .NET SDK 10.0.303; .NET 10.0.11;
xUnit 2.9.3
**Status change:** TASK-018 `proposed -> active -> review`

### Precondition: TASK-017 finalised

`tasks/TASK-017-CELL-RESERVATION.md` `review -> done`; `docs/11_BACKLOG.md`
TASK-017 row and B-011b row `active -> done` (task-file link kept); ledger
detail Review block `Accepted: pending -> yes (2026-09-04)`; ledger index row
status extended to `-> done`, `Accepted -> yes`. Checked: `git status` scope
(the four finalisation files only) and `dotnet test CommandoWar.slnx -c
Release` = `Passed: 164` (unchanged). Both passed, so TASK-018 proceeded
without re-running TASK-017's full verification.

### Central decisions

- **Threshold and increment reuse existing concepts.** The threshold to enter
  a cell is `Terrain.moveCost` of that cell — the same value `Pathfinding`
  already uses as its A* edge weight — and the per-tick increment is
  `Terrain.BaseMoveCost` (1). No new constant. On every scenario pinned
  before this task every traversed passable cell already costs exactly
  `BaseMoveCost`, so threshold = increment = 1 everywhere: agents still
  advance exactly one cell per tick, byte-for-byte the same position/event
  sequence as before. Confirmed by diff on every pre-existing corpus entry
  (see Evidence), not assumed.
- **`AgentState.Progress: int` is genuine new canonical state.** Unlike
  `Route` (TASK-015), it cannot be recomputed from `Position` alone —
  `Position` does not change while an edge is in progress, so nothing else
  records how many ticks have been spent on it. Added to `Canonical.encode`
  right after `Position`; `Canonical.FormatVersion` bumped 1 -> 2.
- **Reservation (TASK-017) generalises without new state.**
  `navigationAndMovement`'s Pass 2 contention test changed from "every
  `Advancing` intent" to "every `Advancing` intent that would *complete* this
  tick" (`startProgress + BaseMoveCost >= Terrain.moveCost next`). An agent
  still mid-edge cannot contend, since it is not entering a cell yet. A
  claimant that loses a contest freezes its progress (does not accumulate)
  rather than resetting or advancing — the same treatment `Position` and
  `Route` already get on a yield. No cross-tick booking added; the TASK-017
  FormatVersion argument for reservation itself is unchanged.
- **`startProgress` resets to 0 whenever the current edge changes.** Encoded
  in `MoveOutcome.Advancing`'s new `startProgress` field: 0 whenever `cached`
  fails (a fresh route was computed this tick — a new destination or a
  replan), `a.Progress` otherwise. Prevents stale progress from a since-left
  edge leaking into a new one.
- **No new event for pure progress accumulation.** A tick where an agent's
  progress increases but it does not enter a new cell emits nothing — treated
  like an idle agent's tick, a continuous fact fully recoverable from the
  resulting `AgentState.Progress`. `MovementStepped` / `MovementCompleted`
  still fire only on an actual cell entry.
- **A new `slow-terrain` corpus entry.** None of the four existing entries
  authors a `Terrain.moveCost` above `BaseMoveCost` on any traversed cell, so
  none of them can regress-test multi-tick accumulation; `slow-terrain` (one
  cell costing 3, the `PathDemo` "costly cell" precedent) is a genuine new
  regression vector.
- **`Progress` exposed on `AgentSnapshot` and `Diagnostics.AgentMarker`** —
  both already built straight from `AgentState` with no other transformation,
  and `docs/04` section 8 says the client renderer interpolates using it.
  Sub-pixel / interpolated SVG rendering explicitly not attempted: renderer
  polish for a client that does not exist yet. The diagnostic renderers show
  a plain integer, omitted at 0.

### Changes

- **`src/CommandoWar.Sim/Domain.fs`.** `AgentState.Progress: int` (new
  field, doc comment explaining why it is canonical, not derived);
  `Agent.create` initialises it to 0.
- **`src/CommandoWar.Sim/Canonical.fs`.** `FormatVersion` 1 -> 2 (doc comment
  records why); `writeAgent` writes `Progress` after `Position`.
- **`src/CommandoWar.Sim/Simulation.fs`.** `MoveOutcome.Advancing` gained
  `startProgress: int`; a `wouldComplete` predicate
  (`startProgress + BaseMoveCost >= Terrain.moveCost next`); Pass 2's
  contention grouping now filters to `wouldComplete` claimants only; Pass 3
  gained a mid-edge branch (accumulate `Progress`, no event) before the
  existing yield/advance branches, both of which now also persist
  `Route = Some r` (see "Deviations" — a real bug this surfaced and fixed
  during implementation, not part of the original design). Phase header
  comment rewritten for steps 3-5 together.
- **`src/CommandoWar.Sim/Snapshot.fs`.** `AgentSnapshot.Progress: int`.
- **`src/CommandoWar.Sim/Diagnostics.fs`.** `AgentMarker.Progress: int`;
  `agentMarkers` populates it. No new `Overlay` case — a scalar on an
  existing marker, not a new spatial relationship.
- **`src/CommandoWar.Headless/DiagnosticRender.fs`.** `Ascii` roster line
  appends `progress N` when nonzero; `Svg` draws a small number beside the
  agent's circle when nonzero.
- **`src/CommandoWar.Headless/Corpus.fs`.** New `costly` terrain-cell helper
  (the `PathDemo` precedent); new `slowTerrainWorld` / `"slow-terrain"` entry
  (one friendly agent, (0,0) -> (4,0), (1,0) costs 3).
- **`content/fixtures/SPIKE-FIXTURE.md`.** Re-pinned (format 2; initial hash
  `0xE13D7540912C7E25`, final hash `0xAFA35198CC6BD8D4`; 33 events unchanged).
- **`content/replays/`.** `spike-fixture.md`, `wall-detour.md`,
  `blocked-goal.md`, `converging-routes.md` re-pinned (tick counts and event
  counts unchanged — confirmed by diff); new `slow-terrain.cwlog` /
  `slow-terrain.md`; `CORPUS.md` gained the `slow-terrain` row.
- **`content/diagnostics/`.** Every existing golden that embeds a hash/format
  footer regenerated (`fixture-tick-000/040`, `fixture-mid-route`, `demo`,
  `los`, `path`, and TASK-017's `converging-routes-tick-003`, all
  `.ascii.txt`/`.svg`/`.html`); new `slow-terrain-tick-002.{ascii.txt,svg}`
  (tick 2: `progress 2` visible, the first golden able to show a nonzero
  value); `README.md` updated (new table rows, a regeneration note explaining
  why `render` doesn't reach corpus-owned initial states).
- **`tests/CommandoWar.Sim.Tests/`.** `FixtureTests.fs` (initial/final hash
  literals, the full 40-value table, `h.Format` 1 -> 2); the same two hash
  literals mechanically updated across `CorpusTests.fs`, `DiagnosticsTests.fs`,
  `PathfindingTests.fs`, `ScenarioTests.fs`, `SightTests.fs`, `TerrainTests.fs`
  (all pin the shared fixture as a sanity-check literal, not just the
  fixture/corpus/diagnostics suites — a wider blast radius than anticipated,
  see "Deviations"); `Canonical.FormatVersion` literal 1 -> 2 in
  `CanonicalHashTests.fs`, `PathfindingTests.fs`, `SightTests.fs`,
  `ScenarioTests.fs` (one test's name, which asserted "stays 1," renamed to
  not claim a specific number); new facts in `SimulationTests.fs` (progress
  accumulation and reset; frozen progress on a lost contest, which proves the
  route-persistence fix) and `DiagnosticsTests.fs` (`AgentMarker.Progress` +
  the `slow-terrain` golden).
- **`docs/04_SIMULATION_SPEC.md`.** Section 8: steps 4-5 rewritten (threshold
  = `Terrain.moveCost`, increment = `Terrain.BaseMoveCost`); new "Realised by
  TASK-018" block (the generalised reservation test, the FormatVersion
  argument, the `slow-terrain` entry, formation slots left to B-011d).
  Section 12.7 realisation note extended.
- **`docs/09_TEST_STRATEGY.md`.** Section 2.4: entry count four -> five,
  `slow-terrain` note.
- **Control.** `tasks/TASK-018-SUBCELL-MOVEMENT-PROGRESS.md` (new);
  `docs/11_BACKLOG.md` (new TASK-018 row; B-011c narrowed to progress alone
  with the task-file link; new B-011d row `proposed`); `PROJECT_STATE.yaml`
  (`active_work -> TASK-018`); `docs/12_PROGRESS_LEDGER.md`
  (`Canonical.FormatVersion` pinned fact `1 -> 2`; shared-fixture hash rows;
  "Green tests" `164 -> 168`; this index row); this entry.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` at every intermediate
    step (no incomplete-match errors this time — `AgentMarker`/`AgentSnapshot`
    gained a field, not `Overlay` a case, so no exhaustive match needed
    updating).
- Command: `dotnet test CommandoWar.slnx -c Release` (precondition check,
  before any TASK-018 edit)
  - Result: `Passed! - Failed: 0, Passed: 164, Skipped: 0, Total: 164`.
- Command: `dotnet test CommandoWar.slnx -c Release` (after the `Progress`
  field, `Canonical.FormatVersion` bump, and `navigationAndMovement` change,
  before any golden/literal was updated)
  - Result: `Failed: 27, Passed: 137, Total: 164` — every failure a
    hash/format mismatch (pinned literals or committed goldens), none a
    logic error. Confirmed by inspecting every failure name before touching
    anything (see Deviations for the one genuine bug this surfaced).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus --regenerate`, then `dotnet run ... -- corpus`
  - Result: five `wrote ...` lines (including the new `slow-terrain.md`);
    `OK - all 5 entries match their committed tables`.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  fixture`
  - Result: `canonical format : 2`; initial `0xE13D7540912C7E25`, final
    `0xAFA35198CC6BD8D4`; 33 events; per-tick sequence hand-transcribed into
    `SPIKE-FIXTURE.md` and cross-checked against `FixtureTests.fs`'s own
    fresh-run assertion.
- Command: `git diff --stat` on `wall-detour.md`, `blocked-goal.md`,
  `converging-routes.md`, `spike-fixture.md` after regeneration
  - Result: only hash lines and the `Canonical format version` prose line
    changed on each; `Tick count` and `Domain events` rows byte-identical to
    the pre-TASK-018 committed values (`19`, `5`(*), `19`, `33` respectively —
    (*) `blocked-goal`'s own count unchanged, verified the same way) —
    confirms the re-pin is purely a byte-layout artifact.
- Command: `dotnet test CommandoWar.slnx -c Release` (after every golden and
  literal was updated, including the new `slow-terrain` entry and facts)
  - Result: `Passed! - Failed: 0, Passed: 168, Skipped: 0, Total: 168`.
- Command: `git add content/replays content/diagnostics content/fixtures`,
  then `dotnet run ... -- corpus --regenerate` again, then `git diff --stat
  content/replays`
  - Result: empty diff — idempotent.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
  - Result: `FSharp.Core 10.1.303` only.
- Command: source scan of `src/CommandoWar.Sim/*.fs` for
  `float|Stopwatch|DateTime|System\.Random|godot` (case-insensitive)
  - Result: two matches, both pre-existing `Diagnostics.fs` module-doc prose
    ("the Godot developer overlay", "NO floating point"), unchanged by this
    task, same as every prior task's scan.
- Command: `git status --porcelain`
  - Result: matches the "Changes" list above exactly. Nothing under the
    client spikes, `src/_scratch`, `bench/`, or any existing `cwheadless`
    verb's target/format behaviour beyond the format-bump content it produces.

### Evidence

- **`Canonical.FormatVersion` is 2:** `cwheadless fixture` / `cwheadless
  corpus` both report it; `CanonicalHashTests` / `PathfindingTests` /
  `SightTests` / `ScenarioTests` all assert it directly.
- **The re-pin is behaviour-neutral for every pre-existing entry:** tick
  counts and event counts unchanged (see Verification); the new
  `slow-terrain` entry is the only one whose behaviour actually depends on
  `Progress` being nonzero at some checkpoint.
- **Reservation composes correctly with progress:**
  ``a completing agent's frozen progress on a lost contest resumes correctly
  next tick`` (`SimulationTests.fs`) proves both the generalised contention
  test and the route-persistence fix — without the fix this test's tick-4
  assertion fails (the agent would still be re-accumulating from 0, not
  entering the cell).
- **Diagnostics standing rule satisfied:** `AgentMarker.Progress` wired into
  both renderers; golden `content/diagnostics/slow-terrain-tick-002.*`
  committed and byte-compared, the only golden in the repository showing a
  nonzero progress value.
- **Idempotence:** regenerating a staged corpus produces zero `git diff`.
- **Green count:** `dotnet test` `164 -> 168`.

### Deviations and unresolved issues

- **A real bug was found and fixed during implementation, not anticipated in
  the task's central decisions.** The first `navigationAndMovement` draft
  updated `Progress` in the mid-edge and yield branches but never wrote
  `Route = Some r` back onto the agent in either. Since `Route` was left
  `None` (or stale) after such a tick, the *next* tick's cache check failed,
  forcing a fresh `Pathfinding.findWithin` call and — critically —
  `startProgress` resetting to 0 (a fresh route means a fresh edge, by
  design). An agent approaching a costly cell therefore accumulated
  `Progress` to 1, then never advanced past 1: caught immediately by the
  first hand-built `slow-terrain` debug run (see Verification), well before
  it reached the test suite, and fixed by writing `Route = Some r` in both
  branches. Every corpus entry regenerated after the fix; the four
  pre-existing entries were unaffected by the bug in the first place (none of
  them ever has `wouldComplete = false`, since none authors a non-base
  `moveCost` on a traversed cell), confirmed by inspecting `git diff` before
  and after the fix for those four specifically.
- **Reservation still does not resolve an agent moving onto a cell held by a
  stationary agent** (TASK-017's documented gap). Unaffected and unextended
  by this task; the new `frozen progress` test was deliberately built so
  neither agent's final destination coincides with the contested cell, to
  avoid exercising that gap and getting a confusing result from an unrelated,
  already-known limitation.
- **The client spikes and `src/_scratch/godot-fsharp-boundary` embed the
  pre-format-2 fixture hash as a comment/reference value** (`src/CommandoWar.Client.Godot/README.md`,
  `src/CommandoWar.Client.Mibo/Program.fs` and `README.md`,
  `src/_scratch/godot-fsharp-boundary/*`). These are disposable, frozen
  TASK-004/005 spike artifacts (Mibo retired at TASK-006; nothing in
  `CommandoWar.slnx` builds or tests against them), so their stale comments
  are expected fallout, not a defect — explicitly out of this task's allowed
  scope (forbidden: touching the client spikes / `src/_scratch`).
- **Formation slots (B-011d) remain unscoped.** No design work toward a
  squad/formation domain concept was done here; whoever picks up B-011d
  starts from the same blank slate this task found it in.

### Documents updated

- `tasks/TASK-018-SUBCELL-MOVEMENT-PROGRESS.md` (new)
- `src/CommandoWar.Sim/Domain.fs`, `Canonical.fs`, `Simulation.fs`,
  `Snapshot.fs`, `Diagnostics.fs`
- `src/CommandoWar.Headless/DiagnosticRender.fs`, `Corpus.fs`
- `content/fixtures/SPIKE-FIXTURE.md`
- `content/replays/{spike-fixture,wall-detour,blocked-goal,converging-routes,slow-terrain}.md`,
  new `slow-terrain.cwlog`, `CORPUS.md`
- `content/diagnostics/*` (every hash-footer golden regenerated), new
  `slow-terrain-tick-002.{ascii.txt,svg}`, `README.md`
- `tests/CommandoWar.Sim.Tests/{CanonicalHashTests,CorpusTests,DiagnosticsTests,FixtureTests,PathfindingTests,ScenarioTests,SightTests,SimulationTests,TerrainTests}.fs`
- `docs/04_SIMULATION_SPEC.md` (section 8, 12.7), `docs/09_TEST_STRATEGY.md`
  (section 2.4)
- `docs/11_BACKLOG.md` (TASK-018 row; B-011c narrowed; new B-011d)
- `docs/12_PROGRESS_LEDGER.md` (`Canonical.FormatVersion` pinned fact; shared
  fixture hash rows; "Green tests" pinned row; this index row)
- `PROJECT_STATE.yaml` (`active_work -> TASK-018`)
- this entry

### AGENTS.md / docs/09 section 8 standing rule

Applies. `Progress` is new authoritative per-tick agent state, exposed as
`AgentMarker.Progress` (a scalar addition, not a new `Overlay` case — no new
spatial relationship to draw) and rendered in both `DiagnosticRender.Ascii`
and `.Svg`; pinned by a new committed golden
(`content/diagnostics/slow-terrain-tick-002.*`), the only one in the
repository that can show a nonzero value.

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: sub-cell movement progress for `Simulation.navigationAndMovement`
  (`AgentState.Progress`, threshold `Terrain.moveCost`, increment
  `Terrain.BaseMoveCost`); reservation generalised to "would complete this
  tick" claimants with no new state; `Canonical.FormatVersion` bumped to 2
  (every pinned hash re-pinned, confirmed behaviour-neutral for every
  pre-existing scenario); new `slow-terrain` corpus entry and diagnostics
  golden; a route-persistence bug found and fixed during implementation
  (see Deviations); formation slots split to B-011d, unscoped.
