## 2026-09-21 - TASK-070 - Route around a permanently parked agent instead of stalling and abandoning

**Owner:** Dave
**Source revision:** working tree on top of `main` at `d1ee72e` (TASK-068/
TASK-069, both accepted and committed)
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot `4.7.2-stable_mono_win64`
**Status change:** `none -> review` (self-verified)

### Changes

Realises new backlog row B-069: the live-agent chokepoint jam TASK-066,
TASK-067, and TASK-068 each independently found and left open, most
recently TASK-068's own recorded evidence -- two jointly-ordered,
formationed agents resolve to distinct cells exactly as `Appraisal.
resolveFormationTarget` predicts, but the trailing agent's route to its
own resolved slot runs through the leading agent's own, and the leading
agent settles there first and never vacates, so the trailing agent's
cached route retries the identical blocked step forever until TASK-065's
`StalledTicks`/`StallAbandonTicks` mechanism abandons the order one cell
short. Exactly `docs/10_RISK_REGISTER.md` R-010 (4/4, `open`) and
`docs/01_ADVERSARIAL_REVIEW.md`'s own pre-mortem on B-011b, materialising
for real.

Scoped via two parallel research agents, the pattern that worked well for
TASK-067/068: one tracing `Simulation.navigationAndMovement`'s full
occupancy/reservation/movement-yield logic end to end (the same-tick
reservation-loss handling from TASK-015/017, `Pathfinding.fs`'s own
complete lack of occupancy awareness, TASK-059's formation-slot
resolution, TASK-065's `StalledTicks`/`StallAbandonTicks` mechanism); one
researching known techniques for multi-agent route coordination in a
fixed-tick deterministic simulation specifically (cooperative A*/
reservation tables, windowed hierarchical cooperative A*, priority-based
replanning, formation march-order queuing), evaluated against this
project's actual constraints (~6-agent squads, deterministic replay, no
floating point, stable entity ordering, the existing bounded-search/
literal-fallback/never-hard-fail idiom `Appraisal.resolveFormationTarget`/
`bestCoverNear` already establish).

Key finding that reframed the two-way fork Dave was originally asked to
scope: **staggered/ordered departure alone does not fix this bug.** The
code trace showed the blocking agent is not transiting through the
contested cell -- it has arrived at its own permanent formation-slot
destination and will never move again, so a "wait for your formation-mate
to pass" scheme waits forever; only the existing 40-tick stall-abandon
still fires. The multi-agent-coordination research's own top
recommendation (formation queuing) assumed a transiting blocker and did
not apply cleanly; its own fallback recommendation (priority-based
detour) was closer to what was actually needed. The code trace also
surfaced a third option better than the two originally named: instead of
changing `Pathfinding.fs`'s public signature (adding an occupancy
parameter, propagated to every caller including `Appraisal.moveLike`),
`Simulation.navigationAndMovement` could call the *existing, unchanged*
`Pathfinding.findWithin` with a locally patched `Terrain` that marks a
settled blocker's cell impassable for that one query. `Pathfinding.fs`'s
contract (pure function of `Terrain` + two cells) stays literally
untouched -- the constraint held to since TASK-065 -- while still
producing a genuinely different, occupancy-aware route for the specific
stuck agent.

Confirmed with Dave via `AskUserQuestion` (four options: the local
occupancy-patch middle path, recommended; a real `Pathfinding.fs`
contract change; staggered departure only, flagged as insufficient for
this repro; park it again). Dave chose the recommended local-patch
option.

**Implementation.**

- `src/CommandoWar.Sim/Terrain.fs`: new `Terrain.withImpassable (cells:
  Cell seq) (t: Terrain) : Terrain`, additive and pure -- returns a copy
  of `t` with the given cells' `Movement` forced `Impassable`, every other
  field (`MoveCost`/`Elevation`/`Opaque`/`Cover`) untouched, an
  out-of-bounds cell ignored. `Pathfinding.fs` itself gains nothing; its
  own module doc comment ("a pure function of `Terrain` and two `Cell`s")
  stays true verbatim.
- `src/CommandoWar.Sim/Simulation.fs`: a new `parkedCells: Cell[]`
  computed once before Pass 3 (every currently-`Alive` agent with
  `Destination = None`, read from `s.Agents`, the pre-tick array --
  confirmed `s.Agents` is never reassigned until the function's final
  line, `s.Agents <- agents`, so reading it mid-Pass-3 is safe regardless
  of which index Pass 3 has already mutated). `navigationAndMovement`'s
  `obstructedBy` freeze branch, before counting a stall, now checks
  whether the specific blocking occupant is itself parked
  (`s.Agents.[occ].Destination.IsNone`); if so, it calls `Pathfinding.
  findWithin` once against `Terrain.withImpassable parkedCells terrain`
  from the agent's current position to its unchanged destination. A
  `Found` result with at least two cells is adopted as a fresh route
  (`Cursor = 0`, `StalledTicks` reset to 0, no movement this tick -- the
  reroute itself consumes the tick, the existing freeze discipline
  applied to a successful outcome) and emits the new `MovementRerouted`
  event; any other result (`NoPath`/`BudgetExhausted`/`InvalidEndpoint`,
  or the blocker not parked at all) falls through to the pre-existing
  freeze-then-abandon logic completely unchanged. The `yieldedTo`
  (same-tick rival contest) branch is untouched.
- `src/CommandoWar.Sim/Events.fs`: new `MovementRerouted of agent: AgentId
  * at: Cell * newNext: Cell * avoided: AgentId`; `MovementObstructed`'s
  own doc comment corrected -- it used to say persistent obstruction
  "is a perception / appraisal concern (B-015 / B-017), not resolved
  here"; that is now only true when the blocker is not parked or no
  detour exists.
- `src/CommandoWar.Sim/Diagnostics.fs`, `src/CommandoWar.Headless/
  DiagnosticRender.fs`: new `Overlay.Rerouted` case, its derivation
  function `reroutedOverlays` (the `abandonedOverlays` precedent, wired
  into `frameOf`), every existing exhaustive-match exclusion-list site
  updated (`eventMarker`, `reservationOverlays`, `obstructionOverlays`,
  `abandonedOverlays`, `undeliveredOrderOverlays`, `fireLineOverlays`, and
  both ascii/svg renderers in `DiagnosticRender.fs`), ascii text line and
  an SVG green dashed box + arrow + `D<id>` label (deliberately distinct
  from `Reserved`'s pink `R<id>`). `src/CommandoWar.Headless/
  AppraisalDemo.fs`'s own exhaustive `Overlay` match (found by the
  compiler, not anticipated at drafting time) gained the matching
  "unhandled, disposable P3 demo" arm, the `Abandoned` precedent exactly.

**A real, genuine consequence found while re-verifying, not smoothed
over.** On `occWorld`'s fully open 8x8 test terrain, a detour around one
parked blocker essentially always exists, so three pre-existing
`SimulationTests` facts changed outcome for real:

- "a mover whose only route runs through a permanently idle agent..." and
  "an order permanently obstructed by a stationary agent is abandoned
  after `StallAbandonTicks` ticks" both specifically exist to prove the
  *no-detour-possible* give-up path -- moved onto a new `corridorWorld`
  fixture (row `y = 1` forced `Impassable` the full map width) so that
  claim is genuine again rather than incidentally true.
- "two agents converging on a cell held by a stationary third never enter
  it and never collide" still passed completely unmodified, but for a
  materially different reason, traced with a temporary `dotnet fsi`
  probe: each obstructed agent successfully reroutes around the parked
  third agent in turn (`MovementRerouted`), but their independently
  computed detours are not coordinated with each other and happen to
  prefer the same alternate cell -- producing a *new*, genuine mutual
  obstruction between the two rerouted agents themselves (the
  `swap-standoff` shape). Neither of *their* blockers is itself parked at
  that point (both hold active orders), so this task's detour
  deliberately does not fire again for it, and the original assertions
  (neither ever enters the third agent's cell; a rival yield and an
  eventual stationary-occupant obstruction both occur) still hold -- the
  fact's doc comment and assertions were updated to name the real
  mechanism directly (asserting a `MovementRerouted` for each agent, then
  the genuine mutual `MovementObstructed`) rather than leave a stale
  description of the pre-TASK-070 mechanism.

**Known limitation, found and recorded, not resolved further:** two
agents independently detouring around the *same* single parked blocker
can converge on each other instead of the original blocker. This is
bounded (the pre-existing give-up path still applies, no infinite loop)
but real -- flagged for `docs/10_RISK_REGISTER.md` R-010, left
deliberately `open` rather than silently reassessed.

New tests added: two `TerrainTests` facts for `withImpassable` plus one
for the empty-set no-op case; a `SimulationTests` fact proving the detour
mechanism directly (an agent obstructed by a parked agent with a real
detour reroutes and reaches its original destination, never abandoned); a
`SimulationTests` fact reproducing the TASK-068 repro shape directly (two
formationed agents jointly ordered through a shared chokepoint both reach
`(5,4)`/`(7,4)`, their own full resolved slots, zero `MovementAbandoned`);
one `DiagnosticsTests` fact for the new `Rerouted` overlay against a new
`chokepoint-detour` corpus entry, with committed ascii/svg goldens at
tick 2 (the tick the reroute fires; arrival follows at tick 7).
`src/CommandoWar.Headless/Corpus.fs`: `stalledOrderAbandonedSpec` gained
the same walled corridor (`for x in 0..7 -> wall x 1`), its description
and `content/replays/stalled-order-abandoned.md`/`CORPUS.md` updated; a
new `chokepointDetourSpec`/`chokepoint-detour` entry (open terrain, no
wall) proves the fix end to end, picked up automatically by
`CorpusTests.fs`'s existing `Corpus.all`-driven `[<Theory>]`.

No new canonical field and no `Canonical.FormatVersion` bump (stayed at
`15`) -- confirmed deliberately, the TASK-066 behaviour-only precedent:
`AgentState.Route` remains the pre-existing non-canonical derived cache,
and no field was added to `AgentState`/`WorldState`.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `422/422` passed (+7 over the pre-task 415).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus`
  - Result: `20/20` entries match, after `--regenerate` re-pinned
    `stalled-order-abandoned` (byte-identical hashes, description-text
    diff only) and wrote the new `chokepoint-detour` entry.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  replay-file content/replays/envelope-full.cwreplay`
  - Result: `checkpoints : OK (24 ticks match the file's committed
    hashes)`, unaffected.
- Command: `dotnet build
  src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Manual check: `Godot_v4.7.2-stable_mono_win64_console.exe --headless
  --path src/CommandoWar.Client.Godot scenes/CommandDemo.tscn --
  --selfcheck`
  - Result: `MATCH 0x84A25E3559111E9B` at tick 90 -- unchanged from
    TASK-069's own pin, not re-pinned.
- Manual check: same, `scenes/SnapshotDemo.tscn` / `scenes/
  AppraisalDemo.tscn`
  - Result: `MATCH 0x6213D672BC36FDB8` / `MATCH 0xA1354EB998FC1B95`, both
    unaffected.
- Command: temporary `dotnet fsi` probes (removed after use):
  (1) the TASK-068 repro shape directly against a hand-built two-agent
  formation world -- confirmed `MovementRerouted` fires for the trailing
  agent and both reach their own resolved slots by tick 7 (in the
  `chokepoint-detour` shape) rather than one stalling to abandonment;
  (2) the "two agents converging" `SimulationTests` fixture, traced
  tick by tick to discover and confirm the real new swap-standoff
  mechanism before updating that fact's assertions; (3) the
  `chokepoint-detour` corpus entry's own frame-by-frame trace, confirming
  the exact tick of the `MovementRerouted` event (tick 2) and of arrival
  (tick 7) before committing the corpus entry's description.
- Command: `git status --porcelain`
  - Result: matches this task's Allowed scope exactly; no
    `Pathfinding.fs`/`Appraisal.fs` entry.

### Evidence

- The TASK-068 repro, reproduced directly: two formationed agents starting
  adjacent, jointly ordered east, resolve to distinct destinations one
  cell apart along the line of travel; before this task the trailing
  agent stalled against the leading agent's settled cell and was
  abandoned one cell short (TASK-068's own recorded finding); after, a new
  `SimulationTests` fact proves both reach their own full resolved slots.
- `content/diagnostics/chokepoint-detour-tick-002.ascii.txt`/`.svg`
  (committed goldens) show the `Rerouted` overlay directly: agent 0 at
  `(1,0)` detours to `(1,1)`, avoiding agent 1 parked at `(2,0)`.
- The swap-standoff known limitation (above) was found by tracing, not
  assumed, and is recorded in this task's own completion evidence, the
  updated `SimulationTests` fact's doc comment, and
  `docs/04_SIMULATION_SPEC.md`'s new detour-mechanism paragraph.

### Deviations and unresolved issues

- **Known limitation, not resolved further:** two agents independently
  detouring around the same single parked blocker can obstruct each
  other instead (see Changes above) -- bounded by the pre-existing
  give-up path, a real residual gap for R-010, not this task's scope to
  resolve (would need either coordinated multi-agent replanning or a
  priority/yield rule neither of which exists in the codebase today).
- `docs/10_RISK_REGISTER.md` was deliberately **not edited**: R-010's
  table row carries no prose/notes column this project's convention can
  extend without inventing new document structure, so its residual-risk
  reassessment is recorded here and in `docs/11_BACKLOG.md`'s B-069 row
  instead, left as `open` for Dave's own decision at review time.
- All three Godot self-checks matched unchanged rather than needing a
  re-pin, because `CommandDemoDrive.runScriptedSelfCheck` is single-agent/
  no-shift (TASK-068's own finding) and never issues a joint group order
  -- the fix is real and independently verified via the `fsi` probes and
  the new `SimulationTests`/corpus evidence above, just not yet exercised
  through the real click path in a live editor session with a genuine
  multi-agent group order. That remains an open path for Dave's own
  interactive pass if he wants to see it directly (the TASK-060/061/064
  precedent for anything this environment cannot provide).

### Documents updated

- `tasks/TASK-070-CHOKEPOINT-DETOUR-AROUND-PARKED-AGENTS.md` (status,
  acceptance criteria, verification, evidence, review).
- `docs/04_SIMULATION_SPEC.md` (movement step 6's realisation note, new
  detour-mechanism paragraph).
- `docs/11_BACKLOG.md` (new B-069 row).
- `docs/12_PROGRESS_LEDGER.md` (this row + Pinned facts green-test count).
- `content/replays/CORPUS.md` (`stalled-order-abandoned` row updated,
  `chokepoint-detour` row added).
- `PROJECT_STATE.yaml` (`active_work`, top-level `updated`).

### Review

- Reviewer: Dave
- Accepted: not yet.
