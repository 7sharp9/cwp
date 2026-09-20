# TASK-065: Turn a permanently stalled movement order into a visible failure

Status: proposed
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

- [ ] A `SimulationTests` fact proves a same-tick yield that clears
      within the threshold never aborts (no regression on ordinary,
      brief multi-agent contention).
- [ ] A `SimulationTests` fact proves a genuinely permanent block reaches
      the threshold and aborts: `Destination`/`Route` cleared, the new
      event emitted, the counter reset.
- [ ] The exact fsi-probe scenario that found this (six agents, ordinary
      squad-movement orders) reaches a real abort within a bounded,
      short number of ticks when re-run, instead of freezing past tick
      500.
- [ ] A player sees *something* distinguishing "this order failed" from
      "this order is still in progress" or "this agent arrived" --
      confirmed live or via a screenshot, not assumed from the event
      existing alone.
- [ ] No `Pathfinding.fs` change; no `Appraisal.fs` `OrderDisposition`/
      `DecisionReason` change.
- [ ] `dotnet test`/`corpus` pass with the full, expected re-pin from the
      `Canonical.FormatVersion` bump (every existing entry byte-layout
      only, unless one genuinely exercises a stall -- check, do not
      assume).
- [ ] Required documentation updated.

## Required verification

Fill exact commands/results during implementation.

- `dotnet build CommandoWar.slnx -c Release`:
- `dotnet test`:
- `dotnet run --project src/CommandoWar.Headless -- corpus [--regenerate]`:
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`:
- `--selfcheck` for all three scene types through the real Godot 4.7.2 editor:
- manual/scripted replay of the original stuck-agent repro:

## Evidence to capture

- command output or test summary;
- the re-run stuck-agent repro's tick-by-tick outcome (abort tick, event);
- screenshot or HUD-text evidence of the player-facing signal;
- the tuned threshold value and why, flagged as first-cut/not
  playtest-derived;
- unresolved failures.

## Expected files

- `src/CommandoWar.Sim/Domain.fs`, `Canonical.fs`, `Events.fs`,
  `Simulation.fs`
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs` (and
  `DeterminismPropertyTests.fs`/`CorpusTests.fs` if a new corpus entry is
  added)
- `content/replays/` (new entry, if added)
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`,
  `src/FSharpSceneHost.cs`
- `docs/04_SIMULATION_SPEC.md`, `docs/11_BACKLOG.md`,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`

## Documentation updates

- this task file's status and evidence;
- `docs/04_SIMULATION_SPEC.md` (step 6's realisation note);
- `docs/11_BACKLOG.md` (B-065 row);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
- `PROJECT_STATE.yaml`.

## Rollback or removal

A new canonical field plus one new phase-local counter and one new event
is additive and revertible with `git revert` in one step, but the
`Canonical.FormatVersion` bump re-pins every corpus/fixture/diagnostics
golden -- reverting after those are re-pinned means re-reverting every
pinned hash back too, not just the code. Prefer fixing forward once this
lands.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
