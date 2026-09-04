## 2026-09-04 - TASK-017 - Same-tick cell reservation and deadlock avoidance

**Owner:** Dave with coding-agent assistance
**Source revision:** `632a577` (Mark TASK-016 replay corpus as done)
**Environment:** Windows 11 Pro 26200 (25H2); .NET SDK 10.0.303; .NET 10.0.11;
xUnit 2.9.3
**Status change:** TASK-017 `proposed -> active -> review`

### Precondition: TASK-016 finalised

Already committed independently (`632a577`, this session's base revision):
`docs/11_BACKLOG.md` TASK-016 row and B-012 row `active -> done`; ledger detail
Review block `Accepted: pending -> yes`; ledger index row `Accepted -> yes`,
status extended to `-> done`. Checked at the start of this task: `git status`
scope (every changed path was TASK-016 finalisation only) and one
`dotnet test CommandoWar.slnx -c Release` = `Passed: 159` (unchanged from
TASK-016's own count). Both passed, so TASK-017 proceeded without re-running
TASK-016's full verification.

### Central decisions

- **Reservation is a same-tick derived resolution, not persisted state.**
  `Simulation.navigationAndMovement` now runs in three passes: Pass 1 computes
  every agent's movement intent (`MoveOutcome`: `Idle` / `Arrived` / `Blocked`
  / `Advancing of route * next * destination`) with no mutation or event.
  Pass 2 groups every `Advancing` intent by its contested next cell and picks
  a winner — fewest remaining route steps (`Cells.Length - 1 - Cursor`), ties
  broken by ascending agent id — mapping every other claimant's array index to
  the winner's id. Pass 3 applies, in ascending agent id order (the standing
  movement-event ordering guarantee): winners and uncontested movers advance
  and emit `MovementStepped` / `MovementCompleted`; losers emit the new
  `MovementYielded` and are left untouched (`Position`, `Destination`, `Route`
  unchanged), so they retry the same next cell next tick once the winner has
  vacated it. Every input to Pass 2 is already canonical (`Position`,
  `Destination`) or an already-excluded derived cache (`AgentState.Route`,
  TASK-015); nothing new is computed once and reused across ticks.
  `Canonical.FormatVersion` stays **1**. This mirrors the exact argument that
  kept `Route` a derived cache in TASK-015 — reservation is not inherently
  "every agent's plan at once and needs cross-tick memory," it is a per-tick
  pure function of already-canonical state for the deadlock-avoidance rule
  actually chosen.
- **Deadlock-avoidance rule: priority by fewest remaining route steps, ties
  broken by ascending agent id.** Not raw agent id — a mover closer to (or
  already at) the contested cell wins over one merely passing through it.
  Provably terminates for a shared-target-cell contest: in any tick, the
  highest-priority claimant among all agents still moving is never blocked by
  a lower-priority one, so it always advances; the sum of every moving agent's
  remaining route length (`Cells.Length - 1 - Cursor` over all agents with a
  destination) is therefore strictly decreasing whenever a contest is
  resolved, which bounds the wait. Verified directly by
  `SimulationTests.fs`'s three new facts (see Changes) rather than asserted
  only from the corpus.
- **Scope of the rule: shared-target-cell contention only.** An agent moving
  onto a cell currently held by a *stationary* agent (idle, arrived, blocked,
  or itself a contest loser this tick) is not resolved — whether that cell is
  free depends on whether its occupant moves, which can chain through further
  agents, and solving that in general is exactly the "negotiation protocol"
  the task was scoped away from (`docs/11` prohibited-list gauge: general
  HTN/GOAP, procedural anything). No corpus entry exercises this case. Left as
  a documented, not silently dropped, gap — see "Deviations."
- **`MovementYielded` is a new event, not a silent no-op.** Carries
  `(agent, at, contested, winner)`: an observable fact for later systems
  (an AI reacting to being stalled) and the direct source the new `Reserved`
  diagnostic overlay derives from.
- **`content/replays/converging-routes` is re-pinned, not replaced** — built
  by TASK-016 specifically to pin the pre-reservation collision. Regenerated
  via `cwheadless corpus --regenerate`; its `Corpus.fs` `Description`, the
  `.cwlog` header comment, and its `CORPUS.md` row are updated to describe the
  resolved contest instead of the old collision. `spike-fixture`,
  `wall-detour`, `blocked-goal` (single-agent, no interaction) regenerate
  byte-identical — confirmed by `git diff --stat`, not assumed.
- **Diagnostics: `Reserved of cell * winner * untilTick`, derived in
  `frameOf` from this tick's `MovementYielded` events** — the exact shape
  `Diagnostics.fs`'s `Overlay` doc comment reserved ("B-011b reservation -> a
  reserved-cell case (cell, agent, until tick)"), and the same
  events-to-overlay derivation pattern `eventMarker` already uses.
  `untilTick` is always the tick the contest was resolved on: reservation is
  same-tick only, never a persisted multi-tick booking, so "until tick" means
  "as of this tick," not a forward booking window. Wired into both
  `DiagnosticRender.Ascii` (an `overlays:` text line) and `.Svg` (a pink
  dashed box labelled `R<winner id>`), which forced every other exhaustive
  `Overlay` match in that file (2 extraction filters, `Ascii`'s text loop,
  `Svg`'s drawing loop) to add a case — `TreatWarningsAsErrors` caught all
  four at build time.

### Changes

- **`src/CommandoWar.Sim/Events.fs`.** New `MovementYielded of agent: AgentId
  * at: Cell * contested: Cell * winner: AgentId`.
- **`src/CommandoWar.Sim/Simulation.fs`.** `navigationAndMovement` restructured
  into the three-pass shape above; new private `MoveOutcome` type
  (`Idle` / `Arrived` / `Blocked` / `Advancing`). Phase header comment
  rewritten to describe steps 2-6 plus the new step 3 resolution and its
  termination argument. `AgentState.Route` still a non-canonical derived
  cache; no `Domain.fs` or `Canonical.fs` change.
- **`src/CommandoWar.Sim/Diagnostics.fs`.** New `Reserved` `Overlay` case;
  `eventMarker` case for `MovementYielded`; new private `reservationOverlays`
  (the `routeOverlays` precedent, sourced from `StepResult.Events`); wired
  into `frameOf` (`Array.append (routeOverlays ...) (reservationOverlays ...)`).
  Overlay doc comment updated (B-011b slot realised; only B-019 still
  undefined).
- **`src/CommandoWar.Headless/DiagnosticRender.fs`.** `Reserved` handling
  added to both `Overlay`-exclusion filters (`sightRays`, `plannedPaths`
  extraction), `Ascii`'s overlay text loop (`reserved (x,y): agent N (until
  tick T)`), and `Svg`'s overlay drawing loop (dashed box `#d53f8c`, text
  label `R<id>`). Overlay-list doc comment updated.
- **`src/CommandoWar.Headless/Corpus.fs`.** `convergingRoutesWorld` doc
  comment and the `converging-routes` `Entry.Description` updated to describe
  the resolved contest instead of the pre-reservation collision.
- **`content/replays/`.** `converging-routes.cwlog` header comment updated
  (command bytes unchanged); `converging-routes.md` regenerated (hashes move
  ticks 3-7 only, final hash and tick count unchanged, events `18 -> 19`);
  `CORPUS.md` entry row updated. `spike-fixture.md` / `wall-detour.md` /
  `blocked-goal.md` regenerated byte-identical.
- **`content/diagnostics/`.** New `converging-routes-tick-003.ascii.txt` /
  `.svg` (the corpus entry's own contested tick, produced by the same
  `DiagnosticRender.runFrames` construction `DiagnosticsTests.fs` uses, not by
  the `render` CLI verb — its initial state is corpus-owned, not the shared
  fixture or demo scenario `render` knows about). `README.md`: new table rows
  and a regeneration note explaining why `render` doesn't apply here.
- **`tests/CommandoWar.Sim.Tests/SimulationTests.fs`.** New "Multi-agent
  movement" section: a `twoAgentWorld` helper plus three facts — no
  simultaneous occupancy and eventual completion (with a bounded retry loop
  and a `MovementYielded` assertion); the closer agent wins despite a higher
  id; a tied contest is won by the lower id.
- **`tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`.** A hand-built
  `Reserved` overlay unit test (the `SightRay` / `PlannedPath` precedent); a
  golden test loading the `converging-routes` corpus entry via `Corpus.loadLog`
  / `Corpus.all`, asserting the derived `Reserved` overlay's fields and the
  `movement-yielded` event marker, then byte-comparing both renders against
  the new goldens.
- **`docs/04_SIMULATION_SPEC.md`.** Section 8: new "Realised by TASK-017"
  block (step 3, the pass structure, the termination argument, the explicit
  scope boundary); the step-6 replan note corrected (a yield does not
  invalidate the route); the deferred-work line replaced with a "Scope down
  (split to B-011c)" note naming the `FormatVersion` consequence. Section
  12.7: realisation note extended to TASK-017.
- **`docs/09_TEST_STRATEGY.md`.** TASK-016 section 2.4 note: "pins today's
  no-reservation behaviour (B-011b re-pins it)" corrected to past tense with
  "(re-pinned by TASK-017 ...)".
- **Control.** `tasks/TASK-017-CELL-RESERVATION.md` (new); `docs/11_BACKLOG.md`
  (new TASK-017 "Current work" row; B-011b `proposed -> active` with the
  task-file link, description narrowed; new B-011c row `proposed`);
  `PROJECT_STATE.yaml` (`active_work -> TASK-017`; gates / gate / phase /
  framework_decision unchanged); `docs/12_PROGRESS_LEDGER.md` (this index
  row; "Green tests" pinned row `159 -> 164`); this entry.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (all four projects).
    Reaching this required fixing four `FS0025` incomplete-match errors in
    `DiagnosticRender.fs` (`TreatWarningsAsErrors`) once `Reserved` was added
    to `Overlay` — expected, and confirms every existing `Overlay` match site
    was actually exhaustive before this change.
- Command: `dotnet test CommandoWar.slnx -c Release` (precondition check,
  before any TASK-017 edit)
  - Result: `Passed! - Failed: 0, Passed: 159, Skipped: 0, Total: 159`.
- Command: `dotnet test CommandoWar.slnx -c Release` (after the reservation
  code change, before regenerating `converging-routes`)
  - Result: `Failed: 1, Passed: 158, Total: 159` — the expected
    `converging-routes` corpus-table mismatch (`FirstBadTick = 3`), nothing
    else regressed.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus`
  - Result: `PASS` for `spike-fixture`, `wall-detour`, `blocked-goal`;
    `DIVERGED converging-routes` (`first bad tick: 3`, expected
    `0x2883EF41908099FC`, actual `0x6DEF7272B16C81B1`) — the intended,
    understood divergence.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus --regenerate`, then `git diff --stat content/replays`
  - Result: all four tables rewritten; only `converging-routes.md` changed
    (`spike-fixture`, `wall-detour`, `blocked-goal` byte-identical, confirming
    the single-agent entries are untouched by the reservation change).
- Command: `dotnet test CommandoWar.slnx -c Release` (after regeneration, plus
  the new tests)
  - Result: `Passed! - Failed: 0, Passed: 164, Skipped: 0, Total: 164`
    (`159 -> 164`: 3 `SimulationTests` facts + 2 `DiagnosticsTests` facts).
- Command: `git add content/replays`, then `dotnet run --project
  src/CommandoWar.Headless -c Release -- corpus --regenerate` again, then
  `git status --porcelain content/replays` / `git diff --stat content/replays`
  - Result: idempotent — the same four `wrote ...` lines, zero working-tree
    diff after staging.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  fixture`
  - Result: initial `0xF2F3DF0D820AD9AC`, final `0x838D3AE7DBFB735D`
    (format 1), 33 events — byte-unchanged, confirming
    `Canonical.FormatVersion` stayed 1.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
  - Result: `FSharp.Core 10.1.303` only.
- Command: source scan of `src/CommandoWar.Sim/*.fs` for
  `float|Stopwatch|DateTime|System\.Random|godot` (case-insensitive)
  - Result: two matches, both pre-existing `Diagnostics.fs` module-doc prose
    ("the Godot developer overlay", "NO floating point") predating this task.
    No type, API, or value match; nothing introduced by this task's edits.
- Command: `git status --porcelain`
  - Result: matches exactly the "Changes" list above — `content/diagnostics/README.md`,
    `content/replays/{CORPUS.md,converging-routes.cwlog,converging-routes.md}`,
    `docs/{04_SIMULATION_SPEC.md,09_TEST_STRATEGY.md}`,
    `src/CommandoWar.Headless/{Corpus.fs,DiagnosticRender.fs}`,
    `src/CommandoWar.Sim/{Diagnostics.fs,Events.fs,Simulation.fs}`,
    `tests/CommandoWar.Sim.Tests/{DiagnosticsTests.fs,SimulationTests.fs}`,
    two new `content/diagnostics/converging-routes-tick-003.*` files. Nothing
    under the client spikes, `src/_scratch`, `bench/`, `content/fixtures/`, or
    any existing `cwheadless` verb's target/format output.

### Evidence

- **`Canonical.FormatVersion` unmoved:** fixture hashes/event-count byte
  identical (`cwheadless fixture`); `wall-detour` / `blocked-goal` /
  `spike-fixture` corpus tables byte-identical after regeneration.
- **The re-pin is exactly the expected one:** `converging-routes` hashes move
  only at ticks 3-7 (the resolution window); tick 12's final hash
  (`0x978ABAE727665277`) and the 12-tick count are unchanged; domain events
  `18 -> 19` (exactly one `MovementYielded`).
- **Termination is proved, not just observed:** `SimulationTests.fs`'s
  ``the agent closer to its destination wins a contested cell even with a
  higher agent id`` and ``a tied contest (equal remaining route length) is won
  by the lower agent id`` pin the priority rule directly; ``two agents
  converging on the same cell never occupy it simultaneously, and the loser
  catches up`` runs a bounded (30-tick) loop asserting non-collision every
  tick and completion of both agents.
- **Diagnostics standing rule satisfied:** `Reserved` overlay wired into both
  renderers; golden `content/diagnostics/converging-routes-tick-003.*`
  committed and byte-compared by `DiagnosticsTests.fs`.
- **Idempotence:** regenerating a staged corpus produces zero `git diff`.
- **Green count:** `dotnet test` `159 -> 164`.

### Deviations and unresolved issues

- **Reservation resolves shared-target-cell contention only,** not an agent
  moving onto a cell currently held by a stationary agent. Solving the latter
  in general requires knowing whether the occupant vacates, which can chain
  through further agents' own contests — exactly the "negotiation protocol"
  this task was scoped away from. No entry in the current corpus exercises
  it. If a future scenario needs it, it is new scope, not an oversight here.
- **The rule does not defend against a rotation deadlock** (three or more
  agents each wanting the cell the next one currently occupies, all blocked
  simultaneously) — the Φ-potential termination argument assumes a
  shared-target-cell contest, not a cyclic swap. Neither corpus entry is a
  cycle. Flagged for whoever designs B-011c or a later multi-agent scenario
  to check before assuming the current rule generalises.
- **B-011b is left `active`, not `done`,** matching the TASK-015 / TASK-016
  precedent: the backlog row for the task just landed stays `active` until
  Dave reviews and a later task's precondition step finalises it (as TASK-017
  itself did for TASK-016 this session, on Dave's own already-committed
  finalisation).

### Documents updated

- `tasks/TASK-017-CELL-RESERVATION.md` (new)
- `src/CommandoWar.Sim/Events.fs`, `Simulation.fs`, `Diagnostics.fs`
- `src/CommandoWar.Headless/DiagnosticRender.fs`, `Corpus.fs`
- `content/replays/converging-routes.{cwlog,md}`, `CORPUS.md`
- `content/diagnostics/converging-routes-tick-003.{ascii.txt,svg}` (new),
  `README.md`
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`, `DiagnosticsTests.fs`
- `docs/04_SIMULATION_SPEC.md` (section 8, 12.7), `docs/09_TEST_STRATEGY.md`
  (section 2.4 note)
- `docs/11_BACKLOG.md` (TASK-017 row; B-011b `-> active`; new B-011c)
- `docs/12_PROGRESS_LEDGER.md` (this index row; "Green tests" pinned row)
- `PROJECT_STATE.yaml` (`active_work -> TASK-017`)
- this entry

### AGENTS.md / docs/09 section 8 standing rule

Applies. Reservation is new authoritative-derived spatial/tactical state, so
the `Reserved` overlay was added to `Diagnostics.fs`'s `Overlay` type (the
slot its doc comment already reserved), wired into both
`DiagnosticRender.Ascii` and `.Svg`, and pinned by a new committed golden
(`content/diagnostics/converging-routes-tick-003.*`) plus a hand-built-overlay
unit test — the `SightRay` / `PlannedPath` precedent from TASK-012 / TASK-013.

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: same-tick cell reservation and deadlock avoidance for
  `Simulation.navigationAndMovement` (priority by remaining route length,
  ties broken by agent id; provably terminating for a shared-target-cell
  contest); new `MovementYielded` event; new `Reserved` diagnostic overlay;
  `content/replays/converging-routes` re-pinned as anticipated by TASK-016;
  `Canonical.FormatVersion` unchanged at 1; formation slots and sub-cell
  movement progress split to B-011c (both need the format bump this task
  avoided).
