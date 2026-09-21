# TASK-070: Route around a permanently parked agent instead of stalling and abandoning

Status: review (implemented and self-verified 2026-09-21, awaiting Dave's
acceptance)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises B-069

## Objective

When a moving agent's cached route is obstructed by another agent that has
itself already come to rest with no active order (`Destination = None`), and
a genuinely different route to the same destination exists that avoids every
currently-parked agent's cell, `Simulation.navigationAndMovement` computes
and adopts that route instead of freezing in place until
`Simulation.StallAbandonTicks` gives up. `Pathfinding.fs`'s public contract
(`findWithin`/`find`, pure functions of `Terrain` and two `Cell`s) does not
change at all. If no such route exists, behaviour is unchanged: freeze, then
abandon after the existing threshold, exactly as TASK-065 left it.

## Why this task exists

The live-agent chokepoint jam has now been found and left open independently
three times: TASK-066 (a corpse blocking a narrow crossing, fixed
separately), TASK-067 and TASK-068 (formation-slot destinations that are
individually correct but whose *routes* to those destinations overlap at a
chokepoint). TASK-068's own recorded evidence is the concrete repro: two
jointly-ordered, formationed agents resolve to distinct cells `(6,5)`/`(7,5)`
exactly as `Appraisal.resolveFormationTarget` predicts, but the trailing
agent's route to `(7,5)` runs through `(6,5)` -- the leading agent's own
resolved, permanent destination. The leading agent settles there first and
never moves again; the trailing agent's cached route retries the identical
blocked step forever until TASK-065's `StalledTicks`/`StallAbandonTicks`
mechanism abandons the order one cell short. This is exactly
`docs/10_RISK_REGISTER.md` R-010 ("Pathfinding, formation, and local
avoidance dominate development... deadlocks, oscillation", impact/
probability 4/4, `open`) and `docs/01_ADVERSARIAL_REVIEW.md`'s own
pre-mortem on B-011b, materialising for real.

Two parallel research passes this session (one tracing
`Simulation.navigationAndMovement`/`Pathfinding.fs`/`Appraisal.
resolveFormationTarget`/TASK-065's stall mechanism end to end; one
researching multi-agent route-coordination techniques for fixed-tick
deterministic simulation specifically) converged on why the two directions
Dave was first asked to choose between are each incomplete on their own:

- **Staggered/ordered departure alone does not fix this bug.** The blocking
  agent is not transiting through the contested cell -- it has *arrived* at
  its own permanent destination and will never vacate. A "wait for your
  formation-mate to pass" scheme waits forever; only the existing 40-tick
  stall-abandon eventually fires, unchanged from today.
- **A full `Pathfinding.fs` contract change** (an occupancy parameter
  threaded through `findWithin`/`find` and every caller, including
  `Appraisal.moveLike`) is the "real" fix but reverses three separate,
  explicit prior decisions (TASK-059, TASK-065, TASK-066) that each declined
  exactly this, and carries the largest blast radius.

Dave confirmed via `AskUserQuestion` (2026-09-21) a third option the code
trace surfaced: `Simulation.navigationAndMovement` itself calls the
*existing, unchanged* `Pathfinding.findWithin` against a **locally patched
`Terrain`** (a cheap, throwaway value with specific cells marked
`Impassable` for one query) whenever an agent is obstructed by a parked
formation-mate. `Pathfinding.fs`'s signature and contract never change; the
occupancy signal lives entirely in `Simulation.fs`, matching every prior
"(a) over (b)" choice on this exact fork (TASK-065, TASK-066) while
actually resolving the bug rather than only converting it into a
faster-surfacing visible failure.

## Central decision (confirmed with Dave 2026-09-21 via `AskUserQuestion`)

**Local occupancy patch inside `navigationAndMovement`, `Pathfinding.fs`
untouched**, over (a) a full `Pathfinding.fs` occupancy parameter and (b)
staggered/ordered departure alone (confirmed insufficient by the code trace,
since the blocker in the actual repro never vacates).

## Required reading

- `docs/10_RISK_REGISTER.md` R-010; `docs/01_ADVERSARIAL_REVIEW.md`'s
  pathfinding/reservation paragraph.
- `docs/11_BACKLOG.md` B-065, B-066, B-067, B-068 rows in full -- what each
  of TASK-065/066/067/068 actually found, fixed, and explicitly left open.
- `tasks/TASK-065-STALLED-ORDER-VISIBLE-FAILURE.md` in full -- the
  `StalledTicks`/`StallAbandonTicks` mechanism this task extends, and its
  own "Forbidden scope" (no `Pathfinding.fs` change) that this task must
  still honour.
- `src/CommandoWar.Sim/Simulation.fs`: `navigationAndMovement` in full --
  Pass 1 (`intents`, the route-cache validity check at the `cached` binding,
  terrain-only), Pass 2a (`yieldedTo`, same-tick rival contest -- untouched
  by this task), Pass 2b (`occupantOf`/`candidateMovers`/`movers`/
  `obstructedBy`, TASK-022's vacation-chain fixpoint, TASK-066's `Casualty.
  isAlive` filter), and Pass 3's `obstructedBy` freeze branch (the one this
  task extends) versus its `yieldedTo` freeze branch (left alone -- see
  Forbidden scope).
- `src/CommandoWar.Sim/Pathfinding.fs` in full, especially its own module
  doc comment's "pure function of `Terrain` and two `Cell`s" claim and its
  determinism section -- confirm this task's local patch never changes that
  claim's truth (the patched `Terrain` is a new, ordinary value passed into
  the unchanged function, not a new parameter or code path inside it).
- `src/CommandoWar.Sim/Terrain.fs` in full -- the `Terrain` record shape
  (dense `Movement: MovementClass[]` array, row-major, `Bounds.Width`),
  `Terrain.passable`, and the existing query functions' style (total,
  bounds-checked, no exceptions) to match for the new helper.
- `src/CommandoWar.Sim/Appraisal.fs`: `resolveFormationTarget` (339-363) and
  `bestCoverNear` (307-320) -- the "bounded search, literal fallback, never
  a hard failure" idiom already established, and confirm this task's
  detour attempt follows the same shape (try once, fall back cleanly, never
  raise).
- `src/CommandoWar.Sim/Events.fs`: `MovementObstructed`'s own doc comment,
  which currently states "Persistent obstruction (a blocker that never
  moves) is a perception / appraisal concern (B-015 / B-017), not resolved
  here" -- this task partially resolves it in the navigation phase instead,
  and that comment needs correcting, not left stale.
- `src/CommandoWar.Sim/Diagnostics.fs`: the `Obstructed`/`Abandoned` overlay
  cases and their derivation functions (`obstructedOverlays`-shaped,
  `abandonedOverlays`) -- the precedent for the new overlay this task adds,
  including every exclusion-list site those two cases already appear in
  (`Diagnostics.fs` and `DiagnosticRender.fs` both).
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`: the existing
  `occWorld`/`bounds` (8x8, fully open/passable) fixtures and every fact
  built on "a mover whose only route runs through a permanently idle agent"
  (631), "two agents converging on a cell held by a stationary third" (755),
  and "an order permanently obstructed by a stationary agent is abandoned
  after `StallAbandonTicks` ticks" (807) -- **read these closely before
  writing any code**. On fully open 8x8 terrain a detour around one parked
  blocker cell always exists, so this task's fix changes every one of
  these three facts' outcomes for real, not just their internals. The
  "two adjacent agents onto each other's cell" (656) and "four agents
  rotating around a 2x2 block" (673) facts are NOT affected -- confirm why
  during implementation (every agent in both has an active `Destination`,
  i.e. is not "parked", so this task's gate never fires for them) rather
  than assuming.
- `src/CommandoWar.Headless/Corpus.fs`: `stalledOrderAbandonedSpec`/
  `formationSlotsSpec` and their corpus entries -- `stalled-order-abandoned`
  is built on the exact same open-8x8/single-idle-blocker shape as the
  `SimulationTests` fact above and needs the same honest reckoning: does it
  still demonstrate abandonment after this fix, or does it now demonstrate
  a successful detour, and either way is a *new* fixture needed to keep a
  genuine "no possible detour, must still abandon" case on record.
- `content/scenarios/bridgehead.cwscenario` and TASK-068's own probe
  description (`PROJECT_STATE.yaml`'s active_work note, TASK-068 entry) --
  the real repro this task must re-verify end to end.

## Dependencies

- B-065 (TASK-065, done -- the stall/abandon mechanism this task extends,
  not replaces), B-067/B-068 (done -- the mechanism that first exercised
  this bug through a real client-issued group order). No other task
  selected.

## Inputs and assumptions

- The "is this occupant parked" signal is `not (Some? occupant.Destination)`
  i.e. `occupant.Destination = None`, read from the **pre-tick** agent
  array (`s.Agents`, not the `agents` array Pass 3 is mutating in place --
  confirm during implementation that `s.Agents` is genuinely untouched
  until the final `s.Agents <- agents` assignment at the end of the
  function, so reading it mid-Pass-3 is safe and deterministic regardless
  of iteration order). An agent with an active order (`Destination = Some
  _`) that simply didn't vacate its cell *this specific tick* is not
  "parked" -- the existing freeze-and-retry behaviour is correct for it
  and this task must not change it (it may still vacate next tick or the
  tick after).
- The patch set is every currently-parked, `Alive` agent's cell other than
  the agent being routed (not just the one specific occupant on the very
  next route cell) -- so a recomputed route also avoids routing through a
  *different* parked squadmate further along, which is the realistic shape
  of the actual bug (a settled formation, not a single stray blocker).
  Confirm this against the TASK-068 repro specifically: does patching only
  the immediate blocker suffice there, or does the wider set matter? Decide
  by evidence, not assumption, and document the choice either way.
- One replan attempt per tick an agent is found in `obstructedBy`, not a
  persistent reservation system and not a search over multiple future
  ticks -- if `Pathfinding.findWithin` against the patched terrain returns
  `Found` with at least two cells, adopt it as a fresh route this tick
  (`Cursor = 0`, `StalledTicks` reset to 0, `Progress` unchanged since the
  agent does not move this same tick -- confirm the exact `Progress`
  handling against the existing freeze branch's own `startProgress`
  convention rather than inventing a new one); if not (`NoPath`,
  `BudgetExhausted`, or `InvalidEndpoint` -- the last case naturally covers
  "the destination cell itself is held by a parked agent", since patching
  it impassable makes the goal fail `Terrain.passable`), fall through to
  the existing freeze-then-abandon logic completely unchanged. Once a
  detour route is adopted, ordinary Pass 1 cache validity on later ticks
  keeps following it without re-attempting a patched search every tick --
  confirm this rather than assume it (the cache check only tests
  `Terrain.passable` on the real, unpatched `s.Terrain`, so a genuinely
  found detour cell is always cache-valid on subsequent ticks unless it
  becomes obstructed again itself, at which point the same logic
  re-triggers naturally).
- This applies only to the `obstructedBy` freeze branch (a stationary
  occupant that did not vacate), never the `yieldedTo` branch (a same-tick
  rival contest between two simultaneously-moving agents, which already
  resolves itself within a tick or two once the winner clears the cell --
  attempting a detour there would be wasted work and could disrupt the
  existing, already-correct rival-arbitration tie-break). Confirm this
  scope boundary is honoured by inspection of the diff, not just intent.
- `Terrain.fs` gains one new, additive, pure helper (e.g.
  `Terrain.withImpassable`) that returns a new `Terrain` value with the
  given cells' `Movement` set to `Impassable`, leaving every other field
  (including `MoveCost`, `Opaque`, `Cover`) untouched. This is the only new
  surface on `Terrain.fs`; `Pathfinding.fs` itself gains nothing and its
  own module doc comment's claims must all still hold true after this task
  (re-read it and confirm, don't just assume the claims survive
  unmodified).
- No new canonical field and no `Canonical.FormatVersion` bump: this task
  changes *behaviour* (which route an agent computes and follows, which
  events fire, in what tick-by-tick sequence), not the *shape* of canonical
  state -- `AgentState.Route` is already documented as a non-canonical
  derived cache (`Pathfinding.fs`'s own module doc comment), and no field
  is added to `AgentState`/`WorldState`. This is the TASK-066 precedent
  (behaviour-only change, no format bump) rather than the TASK-065/067
  precedent (new field, format bump) -- confirm this holds once the
  concrete diff exists, since a wrong assumption here would be a real
  correctness bug in the corpus/replay-checkpoint story.
- A new event is required, not a silent behaviour change: per `AGENTS.md`'s
  diagnostics rule ("a task that adds or changes authoritative spatial or
  tactical state MUST extend the diagnostic frame... and MUST add or
  update a golden visualiser output"), and matching the existing
  one-event-per-distinct-outcome idiom (`MovementYielded`/
  `MovementObstructed`/`MovementAbandoned`), add a new event (e.g.
  `MovementRerouted of agent: AgentId * at: Cell * newNext: Cell * avoided:
  AgentId`) distinct from all three existing movement events, emitted
  exactly when a detour route is adopted. Extend `Diagnostics.fs`'s
  `Overlay` DU and its derivation function (the `Obstructed`/`Abandoned`
  precedent, including every exclusion-list site those two already appear
  in) and `DiagnosticRender.fs`'s ascii/svg renderers, plus at least one
  committed golden under `content/diagnostics/` exercising it.

## Allowed scope

- `src/CommandoWar.Sim/Terrain.fs`: one new additive pure helper (cell-set
  -> patched `Terrain`), plus its own doc comment.
- `src/CommandoWar.Sim/Events.fs`: one new event case; `MovementObstructed`'s
  doc comment corrected to reflect that persistent obstruction against a
  *parked* agent is now partly resolved here, not left entirely to a future
  perception/appraisal concern.
- `src/CommandoWar.Sim/Simulation.fs`: `navigationAndMovement`'s
  `obstructedBy` freeze branch extended with the patched-terrain replan
  attempt and its fallback to existing behaviour.
- `src/CommandoWar.Sim/Diagnostics.fs`, `src/CommandoWar.Headless/
  DiagnosticRender.fs`: new overlay case, derivation function, ascii/svg
  rendering, every existing exclusion-list site updated.
- `tests/CommandoWar.Sim.Tests/TerrainTests.fs`: new facts for the
  `Terrain.fs` helper in isolation.
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`: the three existing
  facts named in Required reading, re-examined and either updated in place
  (if the fixture's *purpose* survives with a corrected expected outcome)
  or replaced/supplemented (if a genuinely-no-detour-possible fixture is
  needed to keep proving the abandonment path still works when it should);
  new facts proving the detour mechanism itself (a parked blocker with a
  real open detour is routed around; a parked blocker with the destination
  cell itself occupied, or genuinely walled in with no detour, still
  freezes and abandons exactly as before); a fact reproducing TASK-068's
  own two-formationed-agents-through-a-chokepoint repro reaching both
  agents' full resolved slots rather than one abandoning.
- `src/CommandoWar.Headless/Corpus.fs`: `stalledOrderAbandonedSpec` and its
  entry re-examined the same way (likely needs a walled-corridor variant to
  keep demonstrating genuine abandonment, since its current open-8x8 shape
  now has a detour); `formationSlotsSpec` checked for any behaviour change
  within its existing `TickCount`; a new corpus entry proving the
  chokepoint-detour fix end to end if one can be built compactly (the
  `demolition-success`/`stalled-order-abandoned` "one new corpus entry per
  genuinely new behaviour" precedent).
- `content/diagnostics/`: new/updated goldens for the new overlay.
- `content/replays/`: re-pinned as needed if any existing entry's tick-by-
  tick sequence changes (check by inspection, not assumption -- a
  behaviour-only change with no format bump does not automatically re-pin
  everything the way a format bump does, only entries whose actual outcome
  changes).
- `src/CommandoWar.Client.Godot/`: no source change expected (this is a
  pure sim-side behavioural improvement, invisible to the client's own
  code), but all three scenes' `--selfcheck` hashes must be re-verified
  through the real Godot 4.7.2 editor and re-pinned if `CommandDemo.tscn`'s
  scripted sequence (which loads real Bridgehead and issues formation
  orders) produces a different tick-by-tick outcome.
- `docs/04_SIMULATION_SPEC.md` (movement step 6's realisation note, updated
  again), `docs/10_RISK_REGISTER.md` (R-010 -- flag the residual-risk
  reassessment for Dave's own decision at review time, do not silently
  change its status), `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- No change to `Pathfinding.fs`'s public signature, parameters, or
  algorithm -- `findWithin`/`find` must remain pure functions of `Terrain`
  and two `Cell`s plus a budget, called by `Simulation.fs` exactly as
  today, just occasionally with a different (patched) `Terrain` value.
- No change to `Appraisal.fs`'s `resolveFormationTarget`, `bestCoverNear`,
  `OrderDisposition`, or `DecisionReason` -- this is a mid-execution
  navigation-phase mechanism, not an appraisal-time one.
- No change to the `yieldedTo` (same-tick rival contest) freeze branch or
  its tie-break rule.
- No persistent, cross-tick reservation table, no space-time search, no
  windowed/hierarchical cooperative pathfinding -- the coordination-
  technique research this session explicitly ruled these out as solving a
  scale problem (dozens of agents, long horizons) this project does not
  have at squad-of-6, in favour of a bounded, one-shot, local detour
  attempt.
- No new client UI, HUD element, or animation -- if the new event needs any
  player-facing signal at all, it is a strict subset of the existing `F1`
  developer-overlay marker mechanism (the `MovementObstructed`/
  `MovementAbandoned` precedent), not a new system. A client reaction is
  not required by this task's acceptance criteria; the diagnostic overlay
  and golden are the mandatory part.
- No corpse-occupancy or formation-offset-resolution changes (TASK-066's
  and TASK-059's own separate, already-closed mechanisms).

## Required work

1. Confirm the exact `s.Agents` mutation-timing question in Required
   reading (is it safe to read pre-tick data mid-Pass-3) by inspection, not
   assumption.
2. Add `Terrain.withImpassable` (or equivalent) to `Terrain.fs`; unit facts
   in `TerrainTests.fs`.
3. Add the new `MovementRerouted` event to `Events.fs`; correct
   `MovementObstructed`'s doc comment.
4. Extend `Simulation.navigationAndMovement`'s `obstructedBy` freeze branch:
   on a parked-occupant obstruction, attempt the patched-terrain replan;
   adopt on success (emit `MovementRerouted`, reset `StalledTicks`, fresh
   route, no movement this tick); fall through to the existing freeze/
   stall-count/abandon logic unchanged on failure.
5. Extend `Diagnostics.fs`/`DiagnosticRender.fs` with the new overlay case,
   its derivation function, every exclusion-list site, and ascii/svg
   rendering; add or update a golden under `content/diagnostics/`.
6. Re-examine and update the three named `SimulationTests` facts; add new
   facts for the detour mechanism itself and for the TASK-068 repro
   reaching full resolution.
7. Re-examine `stalledOrderAbandonedSpec`/`formationSlotsSpec` in
   `Corpus.fs`; author a walled/no-detour-possible variant if needed to
   keep a genuine abandonment demonstration; add a new corpus entry for the
   chokepoint-detour fix if practical.
8. Verify: `dotnet build` both `.slnx`; `dotnet test`; `cwheadless corpus`;
   `replay-file` checkpoints; Godot client build and all three scenes'
   `--selfcheck` through the real editor, re-pinning only what actually
   changed (confirmed, not assumed).
9. Re-run the exact TASK-068 repro (two selected, formationed agents jointly
   ordered to a shared cell, overlapping routes) via a temporary probe;
   confirm both agents now reach their own full resolved formation slots
   rather than one abandoning one cell short.
10. Update documentation per Documentation updates below.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] A `SimulationTests` fact proves the TASK-068 repro resolves for real:
      `` `two jointly-ordered formationed agents through a shared
      chokepoint both reach their own resolved slots` `` -- two formationed
      agents jointly ordered (`Command.moveToMany`) through a shared
      chokepoint both reach their own distinct, fully resolved
      formation-slot destinations `(5,4)`/`(7,4)`, with no
      `MovementAbandoned` for either.
- [x] A `SimulationTests` fact proves the detour mechanism directly:
      `` `an agent obstructed by a parked agent with a real detour
      available reroutes instead of stalling` `` -- `MovementRerouted`
      fires naming the parked blocker, the agent reaches its original
      destination, never abandoned.
- [x] A `SimulationTests` fact proves the fallback is unchanged when no
      detour exists: `` `an order permanently obstructed by a stationary
      agent is abandoned after StallAbandonTicks ticks` ``, now on a new
      `corridorWorld` fixture (row `y=1` walled off) so the "no possible
      detour" case is genuine rather than incidentally true on open
      terrain -- freezes and abandons exactly as before this task.
- [x] The `yieldedTo` (same-tick rival contest) path is confirmed
      unaffected by inspection (the code change is scoped entirely to the
      `obstructedBy` branch) and by the "two adjacent agents onto each
      other's cell" / "four agents rotating" facts continuing to pass
      unmodified (both involve only actively-ordered agents, never a
      parked one, so this task's gate never fires for them).
- [x] No `Pathfinding.fs` change of any kind. Confirmed by `git diff`
      against the Allowed-scope file list (`Terrain.fs`, `Events.fs`,
      `Simulation.fs`, `Diagnostics.fs`, `DiagnosticRender.fs`,
      `Corpus.fs`, `AppraisalDemo.fs`, tests, docs, content -- no
      `Pathfinding.fs`/`Appraisal.fs` entry) and by re-reading
      `Pathfinding.fs`'s own module doc comment, still accurate verbatim.
- [x] No `Canonical.FormatVersion` bump -- confirmed deliberately: stayed
      at 15 throughout, no new field added to `AgentState`/`WorldState`,
      `AgentState.Route` remains the pre-existing non-canonical derived
      cache. The TASK-066 behaviour-only precedent held as predicted.
- [x] `dotnet test`/`corpus` pass; every corpus/diagnostics golden that
      changed was re-pinned and the diff inspected: `stalled-order-
      abandoned.md`'s hashes are byte-identical (only its description/
      `InitialStateNote` text changed -- `Terrain` is excluded from
      `Canonical.encode`, so the added wall cannot move a hash);
      `stalled-order-abandoned-tick-041.ascii.txt`/`.svg` changed by
      exactly the walled row's glyphs, nothing else; `chokepoint-detour`
      (corpus entry + two new diagnostics goldens) is a wholly new,
      genuinely-new-behaviour entry, the `demolition-success` precedent.
      `formation-slots` confirmed byte-identical and unaffected (its
      12-tick window never reaches the contention this task fixes).
- [x] All three Godot `--selfcheck` hashes reconfirmed `MATCH` through the
      real Godot 4.7.2 editor, **unchanged**, no re-pin needed:
      `CommandDemo.tscn` `0x84A25E3559111E9B` at tick 90 (identical to
      TASK-069's own pin), `SnapshotDemo.tscn` `0x6213D672BC36FDB8`,
      `AppraisalDemo.tscn` `0xA1354EB998FC1B95`. Confirmed by inspection,
      not assumed: `CommandDemoDrive.runScriptedSelfCheck` is single-agent/
      no-shift (TASK-068's own recorded finding), so it never issues a
      joint group order and never exercises the contention this task
      fixes -- the demonstrated Bridgehead outcome is therefore identical,
      not merely coincidentally unaffected.
- [x] AGENTS.md diagnostics rule satisfied: new `Overlay.Rerouted` case,
      its derivation function (`reroutedOverlays`), ascii/svg rendering,
      and two committed goldens under `content/diagnostics/`
      (`chokepoint-detour-tick-002.ascii.txt`/`.svg`).
- [x] Required documentation updated (see Documentation updates below).

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0 Warning(s)`, `0 Error(s)`.
- `dotnet test CommandoWar.slnx -c Release`: `422/422` passed (+7 over the
  pre-task 415: two new `TerrainTests` facts for `withImpassable`, plus one
  empty-set fact `+3`; two new `SimulationTests` facts (detour mechanism,
  TASK-068 repro) `+2`; one new `DiagnosticsTests` fact (`Rerouted`
  overlay/goldens) `+1`; one new `CorpusTests` theory row
  (`chokepoint-detour`, parametrised over `Corpus.all`) `+1`).
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`:
  `20/20` entries match (`--regenerate` first re-pinned `stalled-order-
  abandoned` -- byte-identical hashes, description-only diff -- and wrote
  the new `chokepoint-detour` entry).
- `dotnet run --project src/CommandoWar.Headless -c Release --
  replay-file content/replays/envelope-full.cwreplay`: `checkpoints : OK
  (24 ticks match the file's committed hashes)`, unaffected.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`:
  `0 Warning(s)`, `0 Error(s)`.
- `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path
  src/CommandoWar.Client.Godot scenes/CommandDemo.tscn -- --selfcheck`:
  `MATCH 0x84A25E3559111E9B` at tick 90 (unchanged from TASK-069's pin).
  Same command against `SnapshotDemo.tscn`: `MATCH 0x6213D672BC36FDB8`.
  Against `AppraisalDemo.tscn`: `MATCH 0xA1354EB998FC1B95`. None re-pinned.
- Temporary `dotnet fsi` probes (removed after use, `/tmp/probe*.fsx`):
  (1) the original TASK-068-shape repro (two formationed agents starting
  adjacent, jointly ordered east) -- confirmed `MovementRerouted` fires for
  the trailing agent and both reach their own resolved slots; (2) the "two
  agents converging on a stationary third" `SimulationTests` fixture,
  traced tick by tick to discover the real new mechanism (each agent
  reroutes around the parked blocker in turn, then the two rerouted agents
  obstruct *each other* -- the swap-standoff finding below); (3) the
  `chokepoint-detour` corpus entry's own frame-by-frame trace, confirming
  the exact tick of the `MovementRerouted` event (tick 2) and of arrival
  (tick 7), used to correct an initial off-by-one guess in this task's own
  first draft of the corpus entry's description before committing it.
- `git status --porcelain`: matches this task's Allowed scope exactly (see
  Expected files below); no `Pathfinding.fs`/`Appraisal.fs` entry.

## Evidence to capture

- All command output above.
- The TASK-068 repro, reproduced directly: before this task, the trailing
  agent's route to its own resolved slot ran through the leading agent's
  own settled cell and stalled to abandonment one cell short (the
  mechanism TASK-068 itself recorded); after, a new `SimulationTests` fact
  proves both agents reach `(5,4)`/`(7,4)`, their own full resolved slots,
  zero `MovementAbandoned`.
- **Known limitation, found and honestly recorded, not smoothed over:**
  two agents independently detouring around the *same* single parked
  blocker are not coordinated with each other and can converge on the same
  alternate cell, producing a *new*, genuine mutual obstruction between
  themselves (the `swap-standoff` shape) that this task does not resolve
  further -- discovered while re-verifying the pre-existing "two agents
  converging on a cell held by a stationary third" `SimulationTests` fact,
  which still passed unmodified but for a materially different reason than
  before. The fact's doc comment and assertions were updated to name this
  directly (asserting a `MovementRerouted` for each agent, then a genuine
  `MovementObstructed` between them) rather than left describing the old,
  no-longer-accurate mechanism. Neither agent's blocker is itself parked
  at that point, so this task's gate correctly does not fire again --
  bounded by the pre-existing give-up path, not an infinite loop, but a
  real residual gap for `docs/10_RISK_REGISTER.md` R-010, left deliberately
  `open` rather than silently reassessed.
- `stalled-order-abandoned` needed the new `corridorWorld`/walled-corridor
  variant (both in `SimulationTests.fs` and `Corpus.fs`): on `occWorld`'s
  open 8x8 terrain a detour around one parked blocker now always exists,
  so the entry's whole purpose (proving genuine abandonment) required
  walling row `y=1` off to remove that detour, confirmed identical
  behaviour and byte-identical hashes to before this task (`Terrain` is
  excluded from `Canonical.encode`).
- No deviation from this task's stated assumptions: `s.Agents` mid-Pass-3
  read confirmed pre-tick throughout (never reassigned until the
  function's final line); the patch set is every currently-parked agent's
  cell, not just the specific blocker (matches the stated plan, and is
  exactly what the TASK-068 repro needed -- patching only the immediate
  blocker would not by itself change the outcome here, since the
  contention is genuinely one blocker at a time in that repro, but the
  broader set is the documented, deliberate choice for the general case);
  no `Canonical.FormatVersion` bump was needed.

## Expected files

- `src/CommandoWar.Sim/Terrain.fs`, `Events.fs`, `Simulation.fs`,
  `Diagnostics.fs`.
- `src/CommandoWar.Headless/DiagnosticRender.fs`, `Corpus.fs`,
  `AppraisalDemo.fs` (exhaustive `Overlay` match, the `Abandoned`
  "unhandled, disposable P3 demo" precedent -- not anticipated at drafting
  time, found by the compiler's own exhaustiveness check).
- `tests/CommandoWar.Sim.Tests/TerrainTests.fs`, `SimulationTests.fs`,
  `DiagnosticsTests.fs` (`CorpusTests.fs` needed no edit -- its
  `[<Theory>]`/`[<MemberData>]` already iterates `Corpus.all`, so the new
  entry is covered automatically); `ReplayTests.fs` unaffected.
- `content/diagnostics/` (`stalled-order-abandoned-tick-041.ascii.txt`/
  `.svg` re-pinned; two new `chokepoint-detour-tick-002.*` goldens);
  `content/replays/` (`stalled-order-abandoned.md` description-only
  re-pin; new `chokepoint-detour.cwreplay`/`.md`; `CORPUS.md` two rows).
- `docs/04_SIMULATION_SPEC.md`, `docs/11_BACKLOG.md`,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`. `docs/10_RISK_
  REGISTER.md` deliberately **not** edited -- see Documentation updates.

## Documentation updates

- this task file's status and evidence;
- `docs/04_SIMULATION_SPEC.md` (movement step 6's realisation note, a new
  bullet for the detour mechanism and its known swap-standoff limitation);
- `docs/10_RISK_REGISTER.md`: **not edited**. R-010's table row carries no
  prose/notes column this project's convention can extend without
  inventing new document structure (`AGENTS.md`'s own scope discipline);
  its residual-risk reassessment is recorded instead in this task's own
  evidence, `docs/11_BACKLOG.md`'s B-069 row, and this completion report,
  left as R-010 `open` for Dave's own decision rather than silently
  changed;
- `docs/11_BACKLOG.md` (B-069 row updated `proposed -> done` with full
  self-verification evidence);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
- `PROJECT_STATE.yaml`;
- `content/replays/CORPUS.md` (`stalled-order-abandoned` row updated,
  `chokepoint-detour` row added).

## Rollback or removal

Additive: one new `Terrain.fs` helper, one new event case, one new
`Diagnostics.Overlay` case, and a new branch inside one existing function
(`navigationAndMovement`'s `obstructedBy` handling). No canonical field, no
`FormatVersion` bump expected, so a `git revert` of this task's commit
should not require re-pinning anything that this task's own commit didn't
itself pin. Confirm this expectation holds before relying on it.

## Review

- Reviewer: Dave
- Accepted: not yet.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
