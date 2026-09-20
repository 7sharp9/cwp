# TASK-065: Turn a permanently stalled movement order into a visible failure

Status: done (implemented and self-verified 2026-09-20; accepted by Dave
2026-09-20 on the self-verification evidence alone, his explicit choice not
to live-test the on-screen order-status text first)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises B-065

## Objective

When a `MoveTo`/`Hold`/`Assault`/`Withdraw` order's cached route is blocked
by another agent that has itself already stopped moving (so the block can
never clear on its own), stop freezing the ordering agent silently forever.
After a bounded number of consecutive stalled ticks, abandon the order the
same way an unreachable destination already fails: clear
`Destination`/`Route`, reset the stall counter, and emit a new event the
client can show the player.

## Why this task exists

Raised by Dave live-playtesting TASK-064/B-035 (2026-09-20): "i expected
the ai to be more autonomous, they all seem to just get stuck ... you
should ply test it." Confirmed by directly driving the real click path
with a `dotnet fsi` probe (removed after use): an ordinary six-agent
squad-movement order (not even toward the bridge) left agents 2 and 5
permanently stalled from tick ~30 through tick 500 (25 real seconds at
20Hz) with zero recovery and zero player-visible indication anything was
wrong.

Root cause, already documented and predicted, not a new design gap:
`docs/04_SIMULATION_SPEC.md`'s own movement spec names "replan when the
next path cell becomes invalid" as initial-movement step 6, but its own
realisation note says outright: "TASK-017's reservation does not force a
replan on a yield (the route and cursor are left untouched, not
invalidated), so multi-agent contention does not exercise it [replan]
either." `Pathfinding.fs` is terrain-only -- it has no concept of agent
occupancy at all -- so a cached `AgentState.Route` whose next cell is
occupied by an agent that has itself finished moving (`Destination =
None`, never vacating) is a dead end forever; the existing
`yieldedTo`/`obstructedBy` freeze branches in `Simulation.navigationAndMovement`
retry the identical blocked step every tick with no escape. This is
exactly `docs/10_RISK_REGISTER.md` R-010 ("reservation deadlocks", impact/
probability 4/4, status `open`) and `docs/01_ADVERSARIAL_REVIEW.md`'s own
pre-mortem ("reservation deadlocks can dominate development") -- B-011b's
title promised "deadlock avoidance" but TASK-017 narrowed it to same-tick
reservation alone, and no follow-up row ever picked the rest back up.
Small synthetic test fixtures never had enough agents sharing enough
narrow terrain to trigger it; Bridgehead's real six-agent squad is the
first content that does.

## Central decisions (confirmed with Dave 2026-09-20 via `AskUserQuestion`)

**Visible failure, not real dynamic avoidance.** Two directions were
weighed: (a) make a stalled order fail visibly after a bound, without
teaching `Pathfinding`/`Appraisal` anything new about occupancy, or (b)
make pathfinding itself occupancy-aware so a blocked agent finds a
genuinely different route. Dave chose (a) -- smaller, safer, does not
touch `Pathfinding`'s pure-terrain contract, far less likely to ripple
into every corpus/determinism hash. (b) remains open, larger, deferred
follow-up work if this alone is not enough (full "deadlock avoidance",
B-011b's own original, never-fully-delivered promise) -- not designed or
scoped here.

## Required reading

- `docs/04_SIMULATION_SPEC.md`: the "Initial movement progression" list
  (step 6) and its own "Realised by TASK-015" note on step 6's narrow
  terrain-only trigger.
- `docs/10_RISK_REGISTER.md` R-010; `docs/01_ADVERSARIAL_REVIEW.md`'s
  pathfinding/reservation paragraph.
- `docs/11_BACKLOG.md` B-011b/B-011c/B-011d rows (what TASK-017/018/059
  actually delivered vs. B-011b's own original "deadlock avoidance"
  wording).
- `src/CommandoWar.Sim/Simulation.fs`: `navigationAndMovement` in full --
  the `intents` computation (`Idle`/`Arrived`/`Blocked`/`Advancing`), the
  `yieldedTo` rival-contest map, the `occupantOf`/`candidateMovers`/
  `movers`/`obstructedBy` vacation-chain fixpoint (TASK-022), and Pass 3's
  application (the `yieldedTo`/`obstructedBy` freeze branches this task
  must extend, and the existing `Blocked` branch this task's own new
  ending should mirror).
- `src/CommandoWar.Sim/Events.fs`: `MovementBlocked`, `MovementYielded`,
  `MovementObstructed` (the shapes and doc comments to follow for a new
  event).
- `src/CommandoWar.Sim/Domain.fs`: `AgentState` (where a new canonical
  field is added -- the `Suppression`/`Stress`/`RecentlyWounded`
  precedent for a small new per-agent integer/flag).
- `src/CommandoWar.Sim/Canonical.fs`: how a prior `AgentState` field
  addition bumped `Canonical.FormatVersion` and extended `writeAgent`
  (TASK-032/033/045 are recent examples).
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: `stepOnce`'s
  `devFrame.Overlays`/event-consumption loop (the `FireLine` ->
  `heldFireLines`/`heldHitFlashes` precedent this task's own minimal
  client reaction should follow), `liveOrderText`/`heldOrderText`.

## Dependencies

- B-011b (TASK-017, done), B-025 (Bridgehead map, done -- the content that
  first exercises this at scale). No other task selected.

## Inputs and assumptions

- The stall counter must be genuinely canonical (it changes deterministic
  outcomes -- when an order is abandoned) rather than a client-derived
  guess: a new `AgentState.StalledTicks: int` (or equivalent), incremented
  in the `yieldedTo`/`obstructedBy` freeze branches, reset to 0 whenever
  the agent actually advances (`Progress` changes) or its `Order`/
  `Destination` changes. `Canonical.FormatVersion` bump required.
- The exact stall threshold (how many consecutive frozen ticks before
  giving up) is a first-cut, tunable number, not derived from any
  playtest -- flag it for Dave explicitly in the completion report, the
  `plant-duration`/`bleed-out-tick` precedent elsewhere in this project.
  Long enough that an ordinary brief same-tick yield (which usually
  clears in 1-3 ticks once the winner passes) never trips it; short
  enough that a genuine dead end resolves within a few real-time seconds
  at 20 ticks/second, not tens of seconds of silent standing.
- A new event (not a reuse of `MovementBlocked`, which already means "no
  path exists at appraisal/replan time" -- a different failure mode) gives
  the client and any future diagnostics a distinct, correctly-labelled
  reason to show, the `MovementYielded`/`MovementObstructed` precedent of
  one event per distinct cause.
- This applies to whichever order intents actually route through
  `navigationAndMovement`'s `Advancing` case -- confirm during
  implementation whether that is `MoveTo` only or also `Hold`/`Assault`/
  `Withdraw` (all four resolve to a `moveLike`-shaped target in
  `Appraisal.appraise`, so all four are expected to share this fix; verify
  rather than assume).
- A minimal client reaction is required, not optional: "visible failure,
  not silent freeze" is the whole point of this task, so some
  player-facing signal (at minimum, extending `CommandDemoScene`'s
  existing order-status text the way the TASK-064 review's formation-
  redirect fix did) must ship in the same task, kept deliberately small --
  not a new UI system.

## Allowed scope

- `src/CommandoWar.Sim/Domain.fs`: new `AgentState` field.
- `src/CommandoWar.Sim/Canonical.fs`: `Canonical.FormatVersion` bump,
  `writeAgent` extended.
- `src/CommandoWar.Sim/Events.fs`: one new event case.
- `src/CommandoWar.Sim/Simulation.fs`: `navigationAndMovement`'s freeze
  branches extended with the counter and the give-up transition.
- New `SimulationTests` facts (a same-tick yield that clears within the
  threshold never aborts; a permanent block reaches the threshold and
  aborts with the new event, `Destination`/`Route` cleared, counter reset;
  a `DeterminismPropertyTests` extension if the existing generator can
  exercise this deterministically).
- A new or extended corpus entry proving a genuine stall-and-abort end to
  end (the `demolition-success`/`formation-slots` "one new corpus entry
  per genuinely new behaviour" precedent), if one can be constructed
  compactly; a `SimulationTests`-only proof is acceptable if not.
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` /
  `src/FSharpSceneHost.cs`: the minimal player-facing reaction to the new
  event (text and/or a brief marker, the `heldHitFlashes` precedent).
- `docs/04_SIMULATION_SPEC.md` (step 6's own note, updated to describe the
  new trigger), `docs/10_RISK_REGISTER.md` (R-010, only if this task
  meaningfully changes its residual-risk assessment -- Dave's call at
  review time, not assumed), `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- No change to `Pathfinding.fs`'s pure-terrain contract and no
  occupancy-aware route computation of any kind -- that is the explicitly
  deferred, larger "real dynamic avoidance" direction Dave did not choose.
- No change to `Appraisal.appraise`'s own stage-2/3/4 logic or
  `OrderDisposition`/`DecisionReason` vocabulary -- this is a
  mid-execution movement-phase failure, not an appraisal-time one; do not
  conflate the two models.
- No formation-offset or corpse-occupancy changes (TASK-064's own
  separate, still-open findings) -- unrelated mechanisms, not this task's
  job.
- No new client UI system, HUD redesign, or animation beyond the minimal
  reaction named above.

## Required work

1. Confirm exactly which order intents (`MoveTo`/`Hold`/`Assault`/
   `Withdraw`) reach the `Advancing`/freeze branches this task changes.
2. Add the canonical stall counter; bump `Canonical.FormatVersion`.
3. Extend the `yieldedTo`/`obstructedBy` freeze branches: increment on
   freeze, reset on real progress or a new order; abandon (clear
   `Destination`/`Route`, reset counter, emit the new event) once the
   threshold is reached.
4. Add the new event; wire a minimal client reaction.
5. Tests: the two `SimulationTests` facts above at minimum; a corpus
   entry if practical.
6. Verify: `dotnet build` all `.slnx`; `dotnet test`; `cwheadless corpus`
   (re-pin expected, `Canonical.FormatVersion` bump reshuffles every
   hash); Godot client build and all three scenes' `--selfcheck`
   (`CommandDemo.tscn`'s scripted sequence may need extending to actually
   exercise a stall, or may already incidentally reach one -- check
   before assuming either way).
7. Update documentation per Documentation updates below.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] A `SimulationTests` fact proves a same-tick yield that clears
      within the threshold never aborts (no regression on ordinary,
      brief multi-agent contention). `` `an same-tick yield that clears
      within a few ticks never aborts the order` ``.
- [x] A `SimulationTests` fact proves a genuinely permanent block reaches
      the threshold and aborts: `Destination`/`Route` cleared, the new
      event emitted, the counter reset. `` `an order permanently
      obstructed by a stationary agent is abandoned after
      StallAbandonTicks ticks` ``.
- [x] The exact fsi-probe scenario that found this (six agents, ordinary
      squad-movement orders) reaches a real abort within a bounded,
      short number of ticks when re-run, instead of freezing past tick
      500. Re-run against real `bridgehead.cwscenario` content (temporary
      probe, removed after use): five of six agents reach
      `MovementAbandoned` by tick 42, all settled (`Destination = None`,
      `StalledTicks = 0`) at tick 600, none still frozen.
- [x] A player sees *something* distinguishing "this order failed" from
      "this order is still in progress" or "this agent arrived" --
      `CommandDemoScene`'s order-status text now reads `"accepted ->
      abandoned (route blocked)"` for `abandonedOrderHoldSeconds` (3.0s)
      after the event, ahead of the existing destination-suffix logic;
      the `F1` developer overlay also gets a distinct orange marker.
      Accepted by Dave (2026-09-20) on this self-verification evidence
      without a live editor confirmation (his explicit choice); the
      `docs/evidence/` screenshot precedent was not captured this round --
      the diagnostics golden and the direct fsi-probe re-run above are the
      accepted evidence in its place.
- [x] No `Pathfinding.fs` change; no `Appraisal.fs` `OrderDisposition`/
      `DecisionReason` change. Confirmed by inspection and by `git diff`
      against the Allowed-scope file list.
- [x] `dotnet test`/`corpus` pass with the full, expected re-pin from the
      `Canonical.FormatVersion` bump (13 -> 14): every one of the 18
      pre-existing corpus/fixture/diagnostics goldens re-pinned
      byte-layout only (confirmed diff by diff -- only the embedded hash
      token changed in each), plus one genuinely new corpus entry
      (`stalled-order-abandoned`) exercising the new behaviour for real.
      `dotnet test` `412/412` (+4); `-- corpus` `19/19` (+1).
- [x] Required documentation updated (see Documentation updates below).

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0` Warning(s), `0` Error(s).
- `dotnet test CommandoWar.slnx -c Release`: `412/412` passed (+4: the two
  new `SimulationTests` facts, one `DiagnosticsTests` fact, and one
  `CorpusTests` theory row).
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`:
  `19/19` entries match (`--regenerate` first re-pinned all 18 pre-existing
  entries byte-layout only, plus wrote the one new `stalled-order-abandoned`
  entry).
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`:
  `0` Warning(s), `0` Error(s).
- `--selfcheck` through the real Godot 4.7.2 editor
  (`Godot_v4.7.2-stable_mono_win64_console.exe --headless`): `CommandDemo.tscn`
  `MATCH 0x047FF3080AD3EBCB` at tick 90 (re-pinned); `SnapshotDemo.tscn`
  `MATCH 0x49E4CD73C85D1B47` at tick 20 (re-pinned); `AppraisalDemo.tscn`
  `MATCH 0xF0169E93B40D5546` (re-pinned) -- all three format-14 byte-layout
  re-pins, confirmed against the real editor, not assumed.
- Manual/scripted replay of the original stuck-agent repro: a temporary
  `dotnet fsi` probe (removed after use) against the real
  `content/scenarios/bridgehead.cwscenario`, replicating the exact reported
  order (all six friendly agents ordered to `(4,4)`, not toward the bridge),
  run 600 ticks. Result: five of six agents (all but the fireteam leader,
  which had no formation-offset contention) reach `MovementAbandoned`
  between tick 41 and 42, every one settled with `Destination = None` and
  `StalledTicks = 0` by tick 600 -- no agent still frozen, versus the
  original report's freeze persisting past tick 500 with zero recovery.
- `dotnet run --project src/CommandoWar.Headless -c Release -- replay-file content/replays/envelope-full.cwreplay`:
  `checkpoints : OK (24 ticks match the file's committed hashes)` after
  re-pinning (unaffected scenario, byte-layout-only re-pin).
- `git status --porcelain`: matches this task's Allowed scope.

## Evidence to capture

- `dotnet build`/`test`/`corpus`/`replay-file` command output (above);
- the re-run stuck-agent repro's tick-by-tick outcome (five agents abort at
  tick 41-42; above);
- `content/diagnostics/stalled-order-abandoned-tick-041.ascii.txt`/`.svg`
  (committed golden), the HUD-text evidence in place of a screenshot this
  round (see Acceptance criteria);
- the tuned threshold value and why it is a first-cut, not
  playtest-derived: `Simulation.StallAbandonTicks = 40` (2 real seconds at
  the standard 20 ticks/second), chosen to comfortably exceed an ordinary
  same-tick yield's 1-3-tick clearing time (confirmed by the "never aborts"
  `SimulationTests` fact) while still resolving a genuine dead end within a
  few real-time seconds rather than tens of seconds of silent standing --
  not measured against any real playtest session, flagged for Dave to
  retune if a live session feels off;
- unresolved: the on-screen order-status text has not been confirmed live
  in the running editor (self-verified only, see Acceptance criteria).

## Expected files

- `src/CommandoWar.Sim/Domain.fs` (`AgentState.StalledTicks`), `Canonical.fs`
  (`FormatVersion` 13 -> 14, `writeAgent`), `Events.fs` (`MovementAbandoned`),
  `Simulation.fs` (`StallAbandonTicks`, `navigationAndMovement`'s freeze
  branches), `Diagnostics.fs` (new `Abandoned` overlay, `abandonedOverlays`,
  `eventMarker`, the four exclusion-list updates).
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs` (two new facts),
  `DiagnosticsTests.fs` (new fact + the five exclusion-list updates + every
  re-pinned golden reference), `CanonicalHashTests.fs`, `ScenarioTests.fs`,
  `TerrainTests.fs`, `PathfindingTests.fs`, `SightTests.fs`, `CorpusTests.fs`,
  `FixtureTests.fs`, `ReplayTests.fs` (`FormatVersion` 13 -> 14 re-pins).
- `src/CommandoWar.Headless/Corpus.fs` (new `stalled-order-abandoned` entry),
  `DiagnosticRender.fs` (`Abandoned` ascii/svg render + two exclusion
  lists), `AppraisalDemo.fs` (`unhandled` arm).
- `content/replays/` (18 pre-existing entries re-pinned, one new entry:
  `stalled-order-abandoned.cwreplay`/`.md`; `envelope-full.cwreplay`/`.md`
  re-pinned by hand, not part of `Corpus.all`; `CORPUS.md` new entry row);
  `content/fixtures/SPIKE-FIXTURE.md` re-pinned (found already silently
  stale since format 4/TASK-028, corrected straight to format 14 rather
  than left further behind -- flagged for Dave, not this task's own doing);
  `content/diagnostics/` (every existing golden re-pinned byte-layout only,
  two new goldens for `stalled-order-abandoned-tick-041`).
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` (`heldAbandonedOrders`,
  the order-status text override, the `F1` overlay marker),
  `src/FSharpSceneHost.cs`/`src/AppraisalDemoScene.cs` (three re-pinned
  `--selfcheck` hashes), `README.md` (new section).
- `docs/04_SIMULATION_SPEC.md`, `docs/11_BACKLOG.md`,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Documentation updates

- this task file's status and evidence;
- `docs/04_SIMULATION_SPEC.md` (step 6's realisation note);
- `docs/11_BACKLOG.md` (B-065 row);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
- `PROJECT_STATE.yaml`;
- `src/CommandoWar.Client.Godot/README.md` (new section, the TASK-040/048/
  063 precedent);
- `content/replays/CORPUS.md` (new entry row).

## Rollback or removal

A new canonical field plus one new phase-local counter and one new event
is additive and revertible with `git revert` in one step, but the
`Canonical.FormatVersion` bump re-pins every corpus/fixture/diagnostics
golden -- reverting after those are re-pinned means re-reverting every
pinned hash back too, not just the code. Prefer fixing forward once this
lands.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-20), on the self-verification evidence alone --
  confirmed via `AskUserQuestion` at the same time as TASK-066's
  acceptance. Dave's explicit choice was to accept without first live-
  testing the on-screen "accepted -> abandoned (route blocked)" text in
  the running editor; that live confirmation remains outstanding as an
  unexercised path, not a blocking gap.
- Notes: full detail in `docs/ledger/2026-09-20-TASK-065-stalled-order-
  visible-failure.md`. A pre-existing, unrelated staleness was found and
  fixed while re-pinning: `content/fixtures/SPIKE-FIXTURE.md` had been
  silently stuck at `Canonical.FormatVersion` 4 since TASK-028 (nine
  intervening bumps never touched it), unlike `FixtureTests.fs`'s own
  hashes, which stayed current -- corrected straight to format 14, flagged
  for Dave rather than left further behind or silently backfilled through
  every intervening version.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
