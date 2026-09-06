## 2026-09-06 - TASK-022 - Runtime cell-occupancy correctness in the Navigation and movement phase

**Owner:** Dave with coding-agent assistance
**Source revision:** `07af43a` (Add movement obstruction for occupied cells — a
mid-implementation snapshot committed during the session; this entry covers the
working tree that completes it)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; .NET 10.0.11;
xUnit 2.9.3; FsCheck / FsCheck.Xunit 3.3.4
**Status change:** TASK-022 `ready -> review`; B-047 `ready -> review`

### Precondition check

`PROJECT_STATE.yaml` `active_work.selected_task` was `TASK-020` (done, accepted
2026-09-06). TASK-020 is `done` in `docs/11_BACKLOG.md` (row) and
`tasks/TASK-020-*.md` (`Status: done (2026-09-06, accepted by Dave)`). TASK-021
reads `Status: done (2026-09-06, pending Dave's acceptance)` — left untouched.
Baseline before any edit: `dotnet build` `0/0`; `dotnet test` `Passed: 184`;
`cwheadless corpus` all 5 PASS; `cwheadless fixture` initial
`0xE13D7540912C7E25`, final `0xAFA35198CC6BD8D4`, 33 events.

### Central decisions

- **The fix is stage 2b of Pass 2, not a pre-pass or a rewrite of the rival
  rule.** Pass 1 (`intents`) and the Pass 3 loop shape are unchanged. After
  stage 2a (rival arbitration, `yieldedTo`, TASK-017, byte-for-byte unchanged)
  a new stage computes, over `M0` = the completing `Advancing` agents that did
  not lose a rival contest:
  - `occupantOf: Map<Cell,int>` — the unique pre-tick occupant of each cell
    (the `agents` array is untouched until Pass 3, so this is pre-tick);
  - `movers: Set<int>` — an additive fixpoint: seed with every `a in M0` whose
    `next a` has no occupant, then repeatedly add every `a in M0` whose
    `next a` is held by an agent already in the set. Each round consults only
    the previous round's set and pre-tick positions, so it is order-independent
    and terminates in at most n rounds;
  - `obstructedBy: Map<int,AgentId>` — every `a in M0 \ movers` mapped to
    `agents.[occupantOf next a].Id` (an `a` not in `movers` always has an
    occupant on `next a`, or round 0 would have seeded it).
- **One new Pass 3 arm.** In the `Advancing` arm, inside the existing
  `yieldedTo.TryFind idx = None` branch, `obstructedBy.TryFind idx = Some
  occupantId` freezes the agent exactly like a rival-contest loser
  (`Progress = startProgress`, `Route = Some r` written back, `Position` /
  `Destination` untouched) and emits `MovementObstructed(a.Id, a.Position,
  next, occupantId)`. `obstructedBy` and `yieldedTo` are disjoint by
  construction (`obstructedBy` only holds `M0` indices, and `M0` excludes
  `yieldedTo` keys).
- **Swaps and rotation cycles fall out with no special case.** In a 2-cycle
  neither agent is seeded or ever added, so both are obstructed. Same for any
  pure n-cycle. TASK-022 deliberately does not add simultaneous rotation: it
  needs an atomic-swap primitive and a tactical justification, neither of which
  exists. Recorded as a decision.
- **A 3-agent pure rotation cycle is geometrically impossible.** Cardinal
  4-connectivity makes the grid graph bipartite, so every cycle has even
  length; the smallest pure rotation deadlock therefore has four agents (a 2x2
  block). The acceptance criterion's "three agents in a rotation cycle" fact is
  implemented as four agents around a 2x2 with a code comment and this note
  explaining why. A three-agent all-stuck case is still covered by the
  converging-on-occupied fact (rival loser + obstructed rival winner + idle
  third).
- **New event `MovementObstructed of agent: AgentId * at: Cell * blocked: Cell
  * occupant: AgentId`** in `Events.fs`. A movement outcome, emitted in Pass 3
  in ascending agent id order. Distinct from `MovementBlocked` (no path;
  destination cleared) and `MovementYielded` (lost a rival contest to another
  mover): destination and route unchanged, retries next tick.
- **`WorldError.AgentsShareCell of cell: Cell * agents: AgentId[]`** added to
  `World.build` (the optional base-case guard — taken). The vacation-chain
  fixpoint's correctness argument (pre-tick uniqueness -> post-tick
  uniqueness, by induction) needs the base case to hold, and `World.build`
  itself did not enforce it (only `Scenario.validate` did, and the property
  generator / `World.create` callers do it by convention). The guard runs
  after the out-of-bounds check, groups agents by position, and reports the
  lowest row-major shared cell with its occupants in ascending id order. No
  existing `WorldError` match is exhaustive (tests use specific patterns with
  an `| other ->` fallback; the benchmark `unwrap` uses a wildcard), so the
  case addition compiles clean.
- **Diagnostics: `Overlay.Obstructed of cell: Cell * occupant: AgentId`**,
  derived in `frameOf` by a new `obstructionOverlays` (the `reservationOverlays`
  precedent, one entry per distinct blocked cell from this tick's
  `MovementObstructed` events). `eventMarker` gains a `movement-obstructed`
  arm. A separate case, not folded into `Reserved`: `Reserved` carries
  `untilTick` and a contest `winner`, an obstruction's blocker is a stationary
  occupant with no booking window — the shape the `Overlay` doc comment's "a
  new system adds a case here" anticipated.
- **Two new corpus entries.** `follow-chain` (12x9, agents 0/1/2 at (1,3),
  (2,3), (3,3) all ordered to (11,3), 6 ticks): the lead always has a free
  cell, so the vacation chain resolves and all three step every tick, no
  `MovementObstructed`, no arrival within the run. `swap-standoff` (8x8,
  agents at (3,3)/(4,3) each ordered onto the other's cell, 4 ticks): the swap
  is blocked, both emit `MovementObstructed` every tick, neither leaves its
  start cell (only the tick counter advances). One golden pair
  (`content/diagnostics/swap-standoff-tick-001.*`), generated by the same
  `DiagnosticRender.runFrames` path `DiagnosticsTests.fs` uses.
- **No `Canonical.FormatVersion` bump, no new state field, no hash re-pin.**
  The resolution is a same-tick pure function of already-canonical pre-tick
  positions and this tick's intents — the identical argument that kept
  reservation out of the canonical image in TASK-017. Every scenario pinned
  before this task already keeps one agent per cell, so no committed hash
  moves.

### The vacation-chain rule as implemented

`movers` is an imperative fixpoint (`while changed`), equivalent to the spec's
additive rounds: it repeatedly scans `candidateMovers` and adds any index whose
`next` cell is unoccupied or held by an already-added index, until a full scan
adds nothing. `obstructedBy` is then `candidateMovers` minus `movers`, each
mapped to the blocking agent's id. Determinism: the scan reads only the
accumulator from prior additions and the immutable pre-tick `occupantOf`, never
the position of `idx` within the scan.

### The swap / cycle decision

Blocked, not rotated. A two-agent swap and any pure n-agent cycle end with
every participant obstructed and re-emitting `MovementObstructed` each tick.
Simultaneous rotation (rotate-in-place) is explicitly out of scope: it needs an
atomic multi-agent swap primitive and a tactical reason to exist. Persistent
obstruction (a blocker that never moves) is left for B-015 / B-017
(perception / appraisal should notice the stall and re-appraise the order); no
give-up-after-N-ticks constant was added.

### One-tick trace of the `follow-chain` corpus entry

Tick 1, agents at 0:(1,3) 1:(2,3) 2:(3,3), all with destination (11,3):

- Pass 1: all three `Advancing`, next cells (2,3), (3,3), (4,3), each
  `wouldComplete` (cost 1, startProgress 0).
- Stage 2a: three distinct target cells, no rival contest, `yieldedTo` empty,
  `M0 = {0,1,2}`.
- Stage 2b: `occupantOf = {(1,3)->0, (2,3)->1, (3,3)->2}`. Fixpoint:
  round adds 2 (its next (4,3) is unoccupied); next round adds 1 (its next
  (3,3) is held by 2, now a mover); next round adds 0 (its next (2,3) is held
  by 1). `movers = {0,1,2}`, `obstructedBy` empty.
- Pass 3 (ascending id): 0 -> (2,3) `MovementStepped(0,(1,3),(2,3))`;
  1 -> (3,3) `MovementStepped(1,(2,3),(3,3))`; 2 -> (4,3)
  `MovementStepped(2,(3,3),(4,3))`.
- End of tick 1: positions (2,3), (3,3), (4,3) — three distinct cells, the
  whole train advanced one cell. Every later tick is the same shift; after 6
  ticks the train is at (7,3), (8,3), (9,3) and no agent has reached (11,3).
  Total events: 3 `CommandAccepted` + 18 `MovementStepped` = 21.

### The FS0025 incomplete-match errors and their fixes

Adding `MovementObstructed` to `EventBody` and `Obstructed` to `Overlay` forced
six match sites (both `TreatWarningsAsErrors` projects); all were fixed with the
intended semantic branch, not a wildcard:

1. `Diagnostics.eventMarker` (`EventBody` match) — new arm
   `MovementObstructed(_, at, blocked, _) -> { Kind = "movement-obstructed";
   Cells = [| at; blocked |] }`.
2. `Diagnostics.reservationOverlays` (`EventBody` match) — explicit
   `| MovementObstructed _ -> None` added to the existing case list (this
   function derives `Reserved` overlays only; the new `obstructionOverlays`
   handles `MovementObstructed`).
3. `DiagnosticRender.Ascii` `sightRays` `Array.choose` (`Overlay` match) —
   `| Obstructed _ -> None`.
4. `DiagnosticRender.Ascii` `plannedPaths` `Array.choose` (`Overlay` match) —
   `| Obstructed _ -> None`.
5. `DiagnosticRender.Ascii` overlay text loop (`Overlay` match) —
   `| Obstructed(cell, occupant) -> line "  obstructed (x,y): held by agent N"`.
6. `DiagnosticRender.Svg` overlay drawing loop (`Overlay` match) —
   `| Obstructed(cell, occupant) -> ` red dashed box `#c53030` + text label
   `B<occupant id>`.

`DiagnosticsTests.fs` (test project, no `TreatWarningsAsErrors`) produced one
FS0025 *warning* on a `tryPick` in the converging-routes fact; fixed with
`| Obstructed _ -> None` for consistency.

### Changes

- **`src/CommandoWar.Sim/Events.fs`.** New `MovementObstructed` case + doc
  comment (part of the `07af43a` snapshot).
- **`src/CommandoWar.Sim/Simulation.fs`.** New `WorldError.AgentsShareCell`
  case + `World.build` guard; phase header comment extended with a "Realised by
  TASK-022" paragraph; stage 2b (`occupantOf`, `candidateMovers`, `movers`
  fixpoint, `obstructedBy`); one new Pass 3 arm. (Stage 2b + the comment are in
  the `07af43a` snapshot; the Pass 3 arm and the `WorldError` guard are the
  completion.)
- **`src/CommandoWar.Sim/Diagnostics.fs`.** `Overlay.Obstructed` case + doc
  comment; `eventMarker` arm; explicit `MovementObstructed _ -> None` in
  `reservationOverlays`; new `obstructionOverlays`; `frameOf` now
  `Array.concat [ routeOverlays; reservationOverlays; obstructionOverlays ]`.
- **`src/CommandoWar.Headless/DiagnosticRender.fs`.** Four `Obstructed`
  branches (two `Array.choose` filters, the `Ascii` text loop, the `Svg`
  drawing loop).
- **`src/CommandoWar.Headless/Corpus.fs`.** `followChainWorld`,
  `swapStandoffWorld`, and their two `Entry` values in `all`.
- **`content/replays/`.** New `follow-chain.{cwlog,md}`,
  `swap-standoff.{cwlog,md}`; `CORPUS.md` two new rows. The five existing
  `.md` tables regenerate byte-identical (`git diff --stat` after
  `--regenerate`: only `CORPUS.md`).
- **`content/diagnostics/`.** New `swap-standoff-tick-001.{ascii.txt,svg}`;
  `README.md` two new file rows + the regeneration note.
- **`tests/CommandoWar.Sim.Tests/SimulationTests.fs`.** New "Runtime
  cell-occupancy correctness (TASK-022)" section: `occWorld` / `distinctCells`
  / `obstructedBodies` helpers and five facts (mover vs idle occupant;
  two-agent swap standoff; four-agent 2x2 rotation deadlock, run twice for
  determinism; three-agent follow chain resolves same tick; two agents
  converging on a stationary third). One pre-existing fact (`an agent routes
  around an impassable wall`) had its wall moved from `[1,0; 1,1]` to
  `[2,0; 2,1; 2,2]`: the old wall forced agent 0's detour through (0,1)/(0,2),
  the cells of parked friendly agents 1 and 2 — the exact defect this task
  fixes, so the fact could not pass unmodified. Its intent (one-cell-per-tick
  detour around impassable terrain) is unchanged.
- **`tests/CommandoWar.Sim.Tests/DeterminismPropertyTests.fs`.** New property
  (`MaxTest = 200`): after every tick of every `randomCaseGen` case, distinct
  live agents hold distinct cells.
- **`tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`.** Hand-built `Obstructed`
  overlay fact (ASCII + SVG, the `Reserved` precedent); `swapStandoffFrames`
  helper + a golden fact for the `swap-standoff` entry's tick 1; the
  converging-routes `tryPick` gains `| Obstructed _ -> None`.
- **`docs/04_SIMULATION_SPEC.md`.** Section 8 new "Realised by TASK-022" block;
  the TASK-015/017/018 realisation paragraph and the 12.7 note extended;
  section 20 invariant "one live agent has one authoritative position"
  annotated with the pairwise enforcement.
- **`docs/09_TEST_STRATEGY.md`.** Section 2.2 realised list (fifth property);
  section 2.3 choke-point item partial-realisation note; section 2.4 "Five
  entries" -> "Seven entries" with the two new ones described.
- **Control.** `tasks/TASK-022-CELL-OCCUPANCY.md` (`Status`, `## Outcome`,
  acceptance boxes ticked); `docs/11_BACKLOG.md` (TASK-022 row + B-047
  `ready -> review`); `PROJECT_STATE.yaml` (`active_work.selected_task
  TASK-020 -> TASK-022`, note, `updated`); `docs/12_PROGRESS_LEDGER.md` (this
  index row; "Green tests" pinned `184 -> 194`); this entry. No ADR (enforces
  an existing invariant, `docs/04` section 20).

### Verification

All from the repository root.

- `dotnet build CommandoWar.slnx -c Release` -> `Build succeeded. 0 Warning(s)
  0 Error(s)` (all four projects). The FS0025 sites above were fixed as part of
  the edit, so the first clean build already had them handled.
- `dotnet test CommandoWar.slnx -c Release` before any edit -> `Passed: 184`.
- `dotnet test CommandoWar.slnx -c Release` after -> `Passed: 194, Failed: 0`
  (`184 -> 194`: +5 `SimulationTests`, +1 `DeterminismPropertyTests` property,
  +2 `DiagnosticsTests`, +2 `CorpusTests` `[<Theory>]` cases from the two new
  entries). New tests by name:
  - `a mover whose only route runs through a permanently idle agent never
    enters that cell and emits MovementObstructed`
  - `two adjacent agents each ordered onto the other's cell are both
    obstructed indefinitely and never swap`
  - `four agents rotating around a 2x2 block are all obstructed, with no first
    mover, deterministically`
  - `a three-agent follow chain into a free cell advances the whole chain on
    the same tick`
  - `two agents converging on a cell held by a stationary third never enter it
    and never collide`
  - `after every tick of every generated case, distinct live agents hold
    distinct cells` (property, 200 cases)
  - `a hand-built Obstructed overlay renders through the ASCII and SVG
    renderers`
  - `frameOf derives an Obstructed overlay for the swap-standoff entry's
    blocked tick (byte-equal to the goldens)`
  - `corpus entry replays deterministically and matches its committed table`
    theory cases for `follow-chain` and `swap-standoff`
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus` before
  any edit -> all 5 PASS. After -> all 7 PASS (`follow-chain`, `swap-standoff`
  added). The 5 pre-existing tables reproduce their committed hashes with no
  `--regenerate`, before and after: `spike-fixture` `0xE13D7540912C7E25` ->
  `0xAFA35198CC6BD8D4`; `wall-detour` `0xF499BD05E1570D2D` ->
  `0x23A672AC1206B006`; `blocked-goal` `0x63DE59EA977B1B9B` ->
  `0x2498DD43D5A6BC62`; `converging-routes` `0x749D0E7BE45B8D90` ->
  `0xA817BB8DD7AF58FA`; `slow-terrain` `0xBD92C9B2CBF21236` ->
  `0x76CD78F7F3F125BA`.
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture` before
  and after -> initial `0xE13D7540912C7E25`, final `0xAFA35198CC6BD8D4`
  (format 2), 33 events — byte-unchanged.
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus
  --regenerate`, then `git diff --stat content/replays` -> only `CORPUS.md`
  changed (+2 rows); the five existing `.md` tables byte-identical, the two new
  ones written. New entry hashes: `follow-chain` tick count 6, initial
  `0xC34E382E3968F165`, final `0x6D9DCBB66731E8E4`, 21 events (3
  `CommandAccepted` + 18 `MovementStepped`, 0 `MovementObstructed`);
  `swap-standoff` tick count 4, initial `0xB5CCF07F2E62B941`, final
  `0x883E04E8894D97E0`, 10 events (2 `CommandAccepted` + 8 `MovementObstructed`,
  2 per tick). Re-running `--regenerate` after staging is a zero diff
  (idempotent).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive` -> `FSharp.Core 10.1.303` only.
- Source scan of `src/CommandoWar.Sim/*.fs` for
  `float|Stopwatch|DateTime|System\.Random|godot` (case-insensitive) -> only
  pre-existing comment prose ("Godot developer overlay", "NO floating point",
  "no Stopwatch"); nothing in this task's new code, which is int / `Cell` /
  `Set` / `Map` / `Array` only.
- `git status --porcelain` -> `src/CommandoWar.Sim/{Simulation.fs,Diagnostics.fs}`
  (Events.fs already in `07af43a`), `src/CommandoWar.Headless/{Corpus.fs,DiagnosticRender.fs}`,
  `content/replays/CORPUS.md`, four new `content/replays/{follow-chain,swap-standoff}.{cwlog,md}`,
  two new `content/diagnostics/swap-standoff-tick-001.{ascii.txt,svg}`,
  `content/diagnostics/README.md`, the three test files,
  `docs/{04_SIMULATION_SPEC.md,09_TEST_STRATEGY.md,11_BACKLOG.md,12_PROGRESS_LEDGER.md}`,
  `tasks/TASK-022-CELL-OCCUPANCY.md`, `PROJECT_STATE.yaml`, this ledger file.
  Nothing under the client spikes, `bench/`, `src/_scratch`, or
  `content/fixtures/`. Matches the "Allowed scope" list.

### Evidence

- **`Canonical.FormatVersion` unmoved:** fixture hashes / event count
  byte-identical; all five pre-existing corpus tables byte-identical after
  `--regenerate`.
- **The invariant holds generatively:** the new property passes 200 generated
  multi-agent cases; properties 1 and 2 still pass unmodified at 200 each.
- **The policy is pinned directly, not only via the corpus:** five focused
  `SimulationTests` facts cover mover-vs-idle-occupant, swap standoff, rotation
  deadlock (with a determinism re-run), same-tick follow chain, and
  converging-on-occupied.
- **Diagnostics standing rule satisfied:** `Overlay.Obstructed` derived in
  `frameOf`, rendered in ASCII and SVG, covered by a hand-built unit test and a
  committed `content/diagnostics/swap-standoff-tick-001.*` golden byte-compared
  by `DiagnosticsTests.fs`.
- **Green count:** `dotnet test` `184 -> 194`.

### Deviations and unresolved issues

- **A 3-agent pure rotation cycle is geometrically impossible** on the
  bipartite 4-connected grid (even cycles only). The "three agents in a
  rotation cycle" acceptance fact is implemented with four agents around a 2x2
  block; a three-agent all-stuck case is covered by the converging-on-occupied
  fact instead. Documented in the task file, the test comment, and above.
- **One pre-existing `SimulationTests` fact was modified** (`an agent routes
  around an impassable wall`): its wall was repositioned because the old
  geometry ran the moving agent through two parked friendly agents — the exact
  behaviour this task corrects. The fact's intent is unchanged. All other
  pre-existing `SimulationTests` / `CorpusTests` / `DiagnosticsTests` /
  `ReplayTests` / `FixtureTests` / `PathfindingTests` facts pass unmodified.
- **`07af43a` is a mid-implementation commit** made during the session (only
  `PROJECT_STATE.yaml`, `Events.fs`, `Simulation.fs` stage 2b — before the
  Pass 3 arm consumed `obstructedBy`). A standing review of that snapshot
  correctly flagged the unconsumed result. The working tree this entry
  describes completes it; the commit history can be squashed on acceptance.
- **Persistent obstruction is not resolved** (an agent permanently blocked by
  one that never moves retries and re-emits `MovementObstructed` forever). This
  is correct for this layer; noticing the stall and re-appraising the order is
  B-015 / B-017. No give-up timer added.
- **Out-of-scope review findings (not acted on here):** the standing review of
  `07af43a` also raised items against TASK-021 (bound map area or widen path
  cost to `int64`; make `BudgetExhausted` fail the optimality property; the
  `ScenarioContent.Version` policy) and TASK-020 (the partial `PlayerCommand.Agent`
  accessor; a commandless-tick fast path in `commandIntake`). Those touch
  `Terrain` / `Pathfinding` / command intake and pinned test files, all in
  TASK-022's "Forbidden scope", and belong to already-implemented tasks. Left
  for Dave to triage as new backlog items.

### Documents updated

- `tasks/TASK-022-CELL-OCCUPANCY.md`
- `src/CommandoWar.Sim/Events.fs` (via `07af43a`), `Simulation.fs`,
  `Diagnostics.fs`
- `src/CommandoWar.Headless/Corpus.fs`, `DiagnosticRender.fs`
- `content/replays/{follow-chain,swap-standoff}.{cwlog,md}` (new), `CORPUS.md`
- `content/diagnostics/swap-standoff-tick-001.{ascii.txt,svg}` (new),
  `README.md`
- `tests/CommandoWar.Sim.Tests/{SimulationTests,DeterminismPropertyTests,DiagnosticsTests}.fs`
- `docs/04_SIMULATION_SPEC.md` (sections 8, 12.7, 20),
  `docs/09_TEST_STRATEGY.md` (sections 2.2, 2.3, 2.4)
- `docs/11_BACKLOG.md` (TASK-022 row; B-047 `ready -> review`)
- `docs/12_PROGRESS_LEDGER.md` (this index row; "Green tests" pinned `184 -> 194`)
- `PROJECT_STATE.yaml` (`active_work`)
- this entry

### AGENTS.md / docs/09 section 8 standing rule

Applies. The vacation-chain stage changes authoritative spatial behaviour (an
agent that would have moved now stays), so `Overlay.Obstructed` was added to
`Diagnostics.fs`, wired into both `DiagnosticRender.Ascii` and `.Svg`, and
pinned by a new committed golden (`content/diagnostics/swap-standoff-tick-001.*`)
plus a hand-built-overlay unit test — the `Reserved` precedent from TASK-017.

### Review

- Reviewer: Dave
- Accepted: pending
- Notes:
