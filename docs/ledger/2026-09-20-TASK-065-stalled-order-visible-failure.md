## 2026-09-20 - TASK-065 - Turn a permanently stalled movement order into a visible failure

**Owner:** Dave
**Source revision:** `main` at `89cf16b` (TASK-064 accepted, working tree clean at start)
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot `4.7.2-stable_mono_win64`
**Status change:** `proposed -> review` (self-verified; awaiting Dave's live playtest of the on-screen order-status text)

### Changes

Realises B-065, scoped in the prior session (TASK-064's own live playtest
found it) and confirmed via `AskUserQuestion` this session to select and
implement now. Root cause: `docs/04_SIMULATION_SPEC.md`'s own movement spec
names "replan when the next path cell becomes invalid" as step 6, but
`Simulation.navigationAndMovement`'s `yieldedTo`/`obstructedBy` freeze
branches never force that replan on a same-tick reservation loss or a
stationary occupant -- so a route whose next cell is permanently occupied by
an agent that has itself stopped moving retries the identical step forever,
`docs/10_RISK_REGISTER.md` R-010 ("reservation deadlocks") materialising for
real once Bridgehead's six-agent squad shared genuinely narrow terrain.

Central decision (confirmed in the prior session): a visible-failure fix,
not occupancy-aware pathfinding. `Pathfinding.fs` and `Appraisal.fs`'s
`OrderDisposition`/`DecisionReason` vocabulary are untouched.

- `src/CommandoWar.Sim/Domain.fs`: new `AgentState.StalledTicks: int` --
  consecutive ticks the current route has been frozen by a same-tick
  reservation loss or a stationary occupant. Defaults to `0` (the `Progress`
  precedent), no scenario-authored override.
- `src/CommandoWar.Sim/Canonical.fs`: `FormatVersion` `13 -> 14`;
  `writeAgent` gained the new field (written right after `Progress`, the
  same movement-state locality).
- `src/CommandoWar.Sim/Events.fs`: new `MovementAbandoned of agent * at *
  target` -- the `MovementBlocked` precedent ("a path exists but has been
  unavailable too long" instead of "no path exists at all"), distinct from
  `MovementYielded`/`MovementObstructed` so a reader can tell "still trying"
  apart from "gave up".
- `src/CommandoWar.Sim/Simulation.fs`: a new `private [<Literal>]
  StallAbandonTicks = 40` (2 real seconds at the standard 20 ticks/second,
  first-cut and explicitly flagged as not playtest-derived). `MoveOutcome.
  Advancing` gained a `freshRoute: bool` field (`true` exactly when this
  tick did not reuse a cached `Route` -- a new order or a replan), used to
  decide whether a freeze this tick continues a prior stall or restarts the
  count at 0. Both freeze branches (`yieldedTo`/`obstructedBy`) now compute
  `nextStalledTicks` and, once it would reach the threshold, abandon
  outright (`Destination`/`Route` clear, counter resets to 0, emit
  `MovementAbandoned`) instead of freezing again; every other branch
  (`Idle`/`Arrived`/`Blocked`/mid-edge/successful entry) resets the counter
  to 0 -- genuine forward progress, or no order at all, is never a stall.
- `src/CommandoWar.Sim/Diagnostics.fs`: new `Overlay.Abandoned of agent *
  cell * abandonedTarget`, a positive `eventMarker` arm
  (`"movement-abandoned"`), a new `abandonedOverlays` derivation (the
  `obstructionOverlays` precedent) wired into `frameOf`, and the new case
  added to the four existing exclusion-list matches
  (`reservationOverlays`/`obstructionOverlays`/`undeliveredOrderOverlays`/
  `fireLineOverlays`).
- `src/CommandoWar.Headless/DiagnosticRender.fs`: `Abandoned` rendered in
  both `Ascii` (`"  abandoned (x,y): agent N gave up on (x,y)"`) and `Svg`
  (a dashed orange `#9c4221` rect + an `A<id>` label, the `Reserved`/
  `Obstructed` letter-code precedent), plus the two `sightRays`/
  `plannedPaths` exclusion-list updates. `AppraisalDemo.fs`'s own exhaustive
  `unhandled` match gained the matching arm.
- `src/CommandoWar.Headless/Corpus.fs`: new `stalled-order-abandoned` entry
  -- one friendly agent ordered onto a route with a second, permanently
  idle friendly parked on the only next cell (the `swap-standoff`
  single-sided case, but genuinely permanent), run 42 ticks so tick 41's
  give-up transition is captured.
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: new
  `heldAbandonedOrders`/`abandonedOrderHoldSeconds` (3.0s, the
  `heldHitFlashes` pattern applied to per-agent text instead of colour),
  populated from the new `Abandoned` overlay in `stepOnce`'s existing
  event-consumption loop, decayed in `Update`. The order-status text
  computation checks it ahead of the existing destination-suffix logic and
  reads `"accepted -> abandoned (route blocked)"` instead of bare
  `"accepted"` for the held duration -- an abandoned order clears
  `Destination` exactly like a fulfilled one, so without this the player
  could not tell "gave up" apart from "arrived". The `F1` developer overlay
  gained a distinct orange marker for the new overlay (the `Reserved`/
  `Obstructed` cyan/red precedent).
- `src/FSharpSceneHost.cs`/`src/AppraisalDemoScene.cs`: three `--selfcheck`
  hashes re-pinned (format 14, byte-layout only -- none of the existing
  scripted sequences sustains a freeze anywhere near 40 ticks).

### Re-pinning (Canonical.FormatVersion 13 -> 14)

Every one of the 18 pre-existing corpus entries, the shared fixture, every
`content/diagnostics/` golden, `envelope-full` (hand-maintained, not part of
`Corpus.all`), and all three Godot `--selfcheck` hashes moved -- confirmed
diff by diff to be byte-layout only (only the embedded hash token changed in
every file; no tick count, event count, or other content differs), since
`StalledTicks` stays `0` for the whole run of every one of them (none
sustains a same-tick yield or stationary-occupant freeze anywhere near 40
ticks). `content/fixtures/SPIKE-FIXTURE.md` was found already silently stale
at format 4 (since TASK-028 -- nine intervening `FormatVersion` bumps never
touched it, unlike `tests/CommandoWar.Sim.Tests/FixtureTests.fs`'s own
hashes, which stayed current) and corrected straight to format 14 rather
than left further behind or backfilled through every intervening version --
a real, pre-existing documentation-drift finding, not this task's own doing,
flagged for Dave.

Regeneration commands: `dotnet run --project src/CommandoWar.Headless -c
Release -- corpus --regenerate`; two temporary `dotnet fsi` probes (removed
after use) that called `Fixture`/`ScenarioTests`'s own `fixtureScenario`
shape and `DiagnosticRender.Ascii`/`.Svg`/`.Html` directly to recompute and
overwrite every `content/diagnostics/` golden and the `envelope-full`/
`SPIKE-FIXTURE.md` hand-maintained files; the `cwheadless replay-file`
verb's own initial-hash-mismatch error message supplied the new fixture
initial hash directly, confirmed rather than guessed.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `412/412` passed (+4: two new `SimulationTests` facts, one
    `DiagnosticsTests` fact, and one `CorpusTests` theory row).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus`
  - Result: `19/19` entries match their committed tables.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  replay-file content/replays/envelope-full.cwreplay`
  - Result: `checkpoints : OK (24 ticks match the file's committed hashes)`.
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Manual check: `Godot_v4.7.2-stable_mono_win64_console.exe --headless
  --path src/CommandoWar.Client.Godot -- --selfcheck`
  - Result: `MATCH 0x047FF3080AD3EBCB` at tick 90 (re-pinned).
- Manual check: `... --path . scenes/SnapshotDemo.tscn -- --selfcheck` (run
  from `src/CommandoWar.Client.Godot`)
  - Result: `MATCH 0x49E4CD73C85D1B47` at tick 20 (re-pinned).
- Manual check: `... --path . scenes/AppraisalDemo.tscn -- --selfcheck`
  - Result: `MATCH 0xF0169E93B40D5546` (re-pinned).
- Manual/scripted check: a temporary `dotnet fsi` probe (removed after use)
  against the real `content/scenarios/bridgehead.cwscenario`, replicating
  the exact reported repro (all six friendly agents ordered to `(4,4)`, not
  toward the bridge), run 600 ticks.
  - Result: five of six agents reach `MovementAbandoned` at tick 41 or 42
    (the fireteam leader, offset `(0,0)`, reaches its target with no
    contention and is unaffected); every agent settled with `Destination =
    None` and `StalledTicks = 0` by tick 600 -- none still frozen, versus
    the original report's freeze persisting past tick 500 with zero
    recovery.
- `git status --porcelain`: matches this task's Allowed scope (no
  `ExportTerrainScript.cs`/`project.godot` reformat side effect this
  round).

### Evidence

- `content/diagnostics/stalled-order-abandoned-tick-041.ascii.txt`/`.svg`
  (committed goldens) -- the `Abandoned` overlay rendering end to end
  (`"abandoned (1,0): agent 0 gave up on (4,0)"`).
- The stuck-agent repro's own probe transcript is not committed (temporary,
  removed after use); its findings are recorded in full above and in the
  task file so they remain reproducible from this record alone.
- No screenshot captured this round (see Deviations) -- the golden files
  above and this ledger's own probe transcript are the self-verification
  evidence in its place.

### Deviations and unresolved issues

- **No live confirmation yet.** The on-screen order-status text
  (`"accepted -> abandoned (route blocked)"`) has not been confirmed in the
  running Godot editor by an actual player click -- self-verified only
  (the diagnostics golden proves the underlying overlay/event render
  correctly; the fsi probe proves the sim-side fix against the real repro;
  neither exercises `CommandDemoScene`'s own `Update`/text logic through a
  real frame). Flagged for Dave's next live try, the established pattern
  for every client-facing change in this project.
- **No screenshot evidence.** Capturing one would have needed a new
  `--screenshot`-family capture mode (the existing one pauses immediately
  after a scripted click and does not let ticks advance far enough to reach
  a 40-tick stall); judged out of proportion for this task's own minimal
  scope, so HUD-text evidence (the golden files, the probe transcript) was
  used in its place instead, per the task's own "confirmed live or via a
  screenshot" wording (either, not both).
- **Pre-existing, unrelated staleness found while re-pinning**:
  `content/fixtures/SPIKE-FIXTURE.md` had been silently stuck at
  `Canonical.FormatVersion` 4 since TASK-028 -- nine intervening bumps
  (TASK-032/033/034/037/044/045/047/058/062) never touched it, even though
  `FixtureTests.fs`'s own hardcoded hashes stayed correctly current the
  whole time. Corrected straight to format 14 in this task; not
  investigated further (why it drifted, or whether `docs/12_PROGRESS_
  LEDGER.md`'s own "Shared fixture" pinned-facts table has the identical
  gap -- it does, also still showing format-4 values) -- flagged for Dave,
  not silently backfilled through every intervening version or treated as
  this task's own responsibility to fully reconcile.

### Documents updated

- `tasks/TASK-065-STALLED-ORDER-VISIBLE-FAILURE.md` (status, acceptance
  criteria, verification, evidence, review).
- `docs/04_SIMULATION_SPEC.md` (step 6's own realisation note, a new
  "Give up on a permanent freeze" bullet).
- `docs/11_BACKLOG.md` (B-065 row `proposed -> review`).
- `docs/12_PROGRESS_LEDGER.md` (this row).
- `PROJECT_STATE.yaml` (`active_work`, top-level `updated`).
- `src/CommandoWar.Client.Godot/README.md` (new section, the TASK-040/048/
  063 precedent).
- `content/replays/CORPUS.md` (new entry row).

### Review

- Reviewer: Dave
- Accepted: yes, 2026-09-20, on the self-verification evidence alone --
  confirmed via `AskUserQuestion` in the same round as TASK-066's
  acceptance and the decision to commit the combined working tree. Dave's
  explicit choice was to accept without first live-testing the on-screen
  "accepted -> abandoned (route blocked)" text in the running editor.
- Notes: the sim-side fix (the counter, the threshold, the new event) and
  the re-pin are complete and directly verified against the real original
  repro; the client-side text is self-verified by inspection and by the
  diagnostics golden, and was accepted without a live click-through.
