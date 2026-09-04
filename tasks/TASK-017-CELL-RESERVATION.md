# TASK-017: Same-tick cell reservation and deadlock avoidance

Status: done
Owner: Dave
Phase: P2
Gate: G2 (deterministic core); realises backlog B-011b (narrowed)
Size: M

## Objective

Give the Navigation and movement phase docs/04 section 8 step 3 ("reserve
only the immediate next destination where required"): when two or more
agents compute the same next cell in the same tick, resolve the contest
deterministically instead of letting them silently occupy the same cell
(`docs/04_SIMULATION_SPEC.md` section 8; the `content/replays/converging-routes`
corpus entry TASK-016 built specifically to pin the pre-reservation collision).

## Scope down (split to B-011c)

Formation slots and sub-cell movement progress within an edge are **not**
realised by this task. Both are new per-tick state with no derivation path
from `Position` alone (unlike reservation — see "Central decisions"), so
landing either forces `Canonical.FormatVersion` to 2 and every pinned hash to
move; folding them into this task would have pushed it well past size M for a
format change reservation alone does not need. Split to **B-011c**.

## Central decisions (recorded in the ledger)

- **Reservation is a same-tick derived resolution, not persisted state.**
  `Simulation.navigationAndMovement` computes every agent's movement intent
  for the tick (Pass 1, no mutation/event), resolves contention over any
  shared next cell (Pass 2), then applies the surviving moves in ascending
  agent id order (Pass 3, the standing event-ordering guarantee). Every input
  to Pass 2 (`Position`, `Destination`, `Terrain` via `Route`) is already
  canonical or an already-excluded derived cache (`AgentState.Route`,
  TASK-015), and the resolution itself is recomputed fresh every tick — no
  claim is booked across ticks. `Canonical.FormatVersion` stays **1**; no
  pinned hash outside the two-agent `converging-routes` entry moves.
- **Deadlock-avoidance rule: priority by fewest remaining route steps, ties
  broken by ascending agent id — not raw agent id.** A mover closer to (or
  already at) the contested cell wins over one merely passing through it,
  which also happens to make the rule symmetric under the tie case a
  single-cell, two-claimant contest actually produces. Provably terminates
  for a shared-target-cell contest: the winner is never itself blocked by a
  lower-priority claimant, so it always advances, which strictly decreases
  the sum of every moving agent's remaining route length each tick a contest
  is resolved. This is a proof, not an empirical property of the corpus.
- **Scope of the rule: shared-target-cell contention only, not "walking onto
  a cell held by a stationary agent."** The latter is a distinct,
  chain-dependent problem (whether a cell is free depends on whether its
  occupant moves, which can depend on further agents) whose general solution
  is exactly the "negotiation protocol" this task is scoped away from. No
  corpus entry exercises it. Documented as a known gap, not silently
  dropped — see "Deviations" in the ledger detail.
- **A new `MovementYielded` event, not a silent no-op, for the losing
  claimant.** `agent` at `at` deferred entering `contested` to `winner`; its
  destination and route are untouched, so it retries the same next cell next
  tick once `winner` has vacated it. Needed both as an observable fact (a
  later AI system can react to being stalled) and as the source the new
  `Reserved` diagnostic overlay derives from.
- **`content/replays/converging-routes` is re-pinned, not replaced.** Built by
  TASK-016 specifically to pin the pre-reservation collision at `(3,3)`; this
  task regenerates its table (`cwheadless corpus --regenerate`) and updates
  its description text (`Corpus.fs`, the `.cwlog` header, `CORPUS.md`) to
  describe the resolved contest. `spike-fixture` / `wall-detour` /
  `blocked-goal` (single-agent, no interaction) regenerate byte-identical —
  confirmed, not assumed.

## Diagnostics

Applies (`docs/09` section 8): reservation is new authoritative-derived
spatial/tactical state, and `Diagnostics.fs`'s `Overlay` doc comment already
reserved the slot ("B-011b reservation -> a reserved-cell case (cell, agent,
until tick)"). Added `Reserved of cell * winner * untilTick`; `Diagnostics.frameOf`
derives one per `MovementYielded` event this tick (the `routeOverlays`
precedent, sourced from events instead of `AgentState`); both `DiagnosticRender.Ascii`
and `.Svg` render it (an `overlays:` text line; a pink dashed box labelled
with the winning agent id). Golden: `content/diagnostics/converging-routes-tick-003.*`
(the corpus entry's own contested tick), plus a hand-built-overlay unit test
(the `SightRay`/`PlannedPath` precedent).

## Allowed scope

- `src/CommandoWar.Sim/Simulation.fs` (`navigationAndMovement`, restructured
  into intent / reservation / apply passes; new private `MoveOutcome` type);
- `src/CommandoWar.Sim/Events.fs` (new `MovementYielded` case);
- `src/CommandoWar.Sim/Diagnostics.fs` (new `Reserved` overlay case,
  `reservationOverlays`, `eventMarker` case, doc comments);
- `src/CommandoWar.Headless/DiagnosticRender.fs` (`Reserved` handling in every
  exhaustive `Overlay` match: `Ascii`, `Svg`);
- `src/CommandoWar.Headless/Corpus.fs` (description text only, for
  `converging-routes`);
- `content/replays/converging-routes.{cwlog,md}`, `content/replays/CORPUS.md`
  (re-pin and description update; the other three entries are regenerated as
  a regression check, not edited);
- `content/diagnostics/converging-routes-tick-003.{ascii.txt,svg}` (new),
  `content/diagnostics/README.md`;
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`,
  `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`;
- `docs/04_SIMULATION_SPEC.md` section 8 and 12.7 realisation notes,
  `docs/09_TEST_STRATEGY.md` TASK-016 note (re-pin cross-reference);
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml`).

## Forbidden scope

- Formation slots, sub-cell movement progress, or any `Canonical.FormatVersion`
  bump (**B-011c**).
- Combat, suppression, appraisal, or any B-014/B-017/B-019+ behaviour.
- A new package, project, or general reservation/negotiation framework;
  resolving an agent moving onto a cell held by a stationary agent (documented
  gap, not solved here).
- A new `cwheadless` verb or a change to an existing verb's behaviour (the new
  `Reserved` overlay rendering through the existing renderers is the one
  explicitly allowed exception).
- Touching the client spikes, `src/_scratch`, or `bench/CommandoWar.Benchmarks/`.

## Acceptance criteria

- [x] Two agents whose paths cross the same cell on the same tick never both
      occupy it: the higher-priority agent enters, the other emits
      `MovementYielded` and retries next tick (`SimulationTests.fs`, three new
      facts: non-collision + termination, priority by remaining route length
      overriding agent id, and the tied-priority id tie-break).
- [x] `content/replays/converging-routes` is re-pinned (hashes move only
      across the resolution window, ticks 3-7; the final hash at tick 12 and
      the tick count are unchanged); `wall-detour`, `blocked-goal`,
      `spike-fixture` regenerate byte-identical.
- [x] `cwheadless fixture` unchanged: `0xF2F3DF0D820AD9AC` /
      `0x838D3AE7DBFB735D`, 33 events, format 1 — confirming
      `Canonical.FormatVersion` stayed 1.
- [x] `Reserved` diagnostic overlay: wired into both renderers, golden
      committed (`converging-routes-tick-003.*`), hand-built-overlay unit
      test.
- [x] `dotnet build CommandoWar.slnx -c Release` = 0 warnings, 0 errors.
- [x] `dotnet list src/CommandoWar.Sim package --include-transitive` =
      `FSharp.Core` only; source scan of `src/CommandoWar.Sim` clean (matches
      only pre-existing doc-comment prose).
- [x] `docs/04` section 8 / 12.7, `docs/09`, backlog rows, ledger index row +
      detail file, `PROJECT_STATE.yaml`, task status updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`
- `dotnet test CommandoWar.slnx -c Release` before and after
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus` before
  and after (which entries move, and why)
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate`
  then stage and regenerate again — idempotence
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  byte-compared
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
- source scan of `src/CommandoWar.Sim`
- `git status`

## Alternative

Taken: item 1 (reservation + deadlock avoidance) alone, landed as TASK-017 /
B-011b narrowed. Formation slots and sub-cell movement progress split to a
new **B-011c**, per the task prompt's own "Alternative" clause — reservation
turned out not to need the `Canonical.FormatVersion` bump at all (a materially
different, and smaller, shape than either deferred concern), so combining them
into one task would have mixed a zero-format-impact change with one that
necessarily re-pins every fixture and replay hash in the repository.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
