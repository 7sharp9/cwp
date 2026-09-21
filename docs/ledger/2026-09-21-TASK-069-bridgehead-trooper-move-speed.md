## 2026-09-21 - TASK-069 - Bridgehead trooper movement is twice as fast as intended

**Owner:** Dave
**Source revision:** working tree on top of `main` at `3d4f5e4`, alongside TASK-068's own uncommitted diff (not yet accepted)
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot `4.7.2-stable_mono_win64`
**Status change:** `none -> review` (self-verified)

### Changes

Realises new backlog row B-068. Raised live by Dave immediately after
reviewing TASK-068: "the game does run really quickly though, its probably
at least twice as fast as it should be, the simulation should be running at
20hz? but the game should be running smoothly, currently its fast and
choppy like it running fast forward."

Investigated directly rather than assumed. A temporary diagnostic added to
`FSharpSceneHost.cs`'s `_Process` (a `--diag-timing` flag logging real
wall-clock elapsed time against the scene's own reported tick, parsed from
`HudText()`'s `"tick %d"` prefix, once per second; removed after use, never
committed) was run windowed for 8 seconds:

```
real=1.00s tick=20     real=5.07s tick=101
real=2.02s tick=40     real=6.08s tick=121
real=3.03s tick=60     real=7.10s tick=142
real=4.05s tick=81     real=8.12s tick=162
```

Exactly 20 ticks/second throughout, with a stable 60fps render loop
(`delta=0.0167` every frame). This rules out a frame-pacing or engine bug:
`CommandDemoScene.fs`'s fixed-timestep accumulator (`Update`) is doing
precisely what it should, and nothing in TASK-068 touches it.

Root cause, found by inspection: `Terrain.BaseMoveCost = 1` (`Terrain.fs`)
and Trooper's authored `MoveSpeed = 2` (`content/scenarios/
bridgehead.cwscenario`'s `unit-type trooper 2`, equal to `Agent.
MoveSpeedDefault` in `Domain.fs`) satisfy `Simulation.fs`'s edge-completion
inequality, `(startProgress + BaseMoveCost) * moveSpeed >= moveCost *
MoveSpeedDefault`, at `startProgress = 0` whenever `moveCost = 1` (open
terrain): `(0 + 1) * 2 >= 1 * 2` is true immediately. A Trooper therefore
crosses a whole grid cell in exactly one 20Hz tick — 50ms. `Domain.fs`'s own
doc comment on `MoveSpeedDefault` confirms this reproduces the sim's
original pre-unit-type pace exactly (TASK-049 introduced unit types without
changing the default rate), so this has been true since well before this
session and is not a regression from TASK-068 or anything recent.

This single fact explains both halves of Dave's complaint together: 50ms/
cell genuinely is very fast for a human to track, and it also starves
`CommandDemoScene.fs`'s render-interpolation (`renderPos`/`alpha`) of any
real window to smooth over — `edgeTickEstimate` learns down to 1 tick per
edge, leaving only about 3 render frames (at 60fps) to blend a figure across
before it has to jump to the next cell, reading as choppy rather than a
walk cycle.

Confirmed via `AskUserQuestion` (three options offered: lower Trooper's
authored `MoveSpeed`, recommended; scale down `simHz`; something else) —
Dave chose lowering `MoveSpeed`, confirming `simHz` should stay at its
documented 20Hz target. `MoveSpeed = 1` was chosen directly (not asked as a
separate question) as the value most literally matching Dave's own framing
("twice as fast as it should be"): it doubles ticks-per-cell from 1 to 2
(100ms/cell), exactly halving the current rate. `RawUnitType.MoveSpeed`
must be a positive integer (`NonPositiveUnitTypeMoveSpeed`, `Scenario.fs`),
so `1` is the floor, not an arbitrary pick.

- `content/scenarios/bridgehead.cwscenario`: `unit-type trooper 2` ->
  `unit-type trooper 1`. Every `friendly`/`enemy` deployment line in the
  file references this one unit type (confirmed by inspection), so this
  uniformly halves movement for the whole squad and both sides, not a
  partial fix.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: `RunSelfCheck`'s
  pinned `CommandDemoScene` expected hash re-pinned
  (`0x2629A1FE165F94BB -> 0x84A25E3559111E9B` at tick 90) and its comment
  updated to record the reason.

No other content was touched. `DemoScenario.fs`, `Corpus.fs`, `LosDemo.fs`,
`PathDemo.fs`, `TurnDemo.fs`, every test fixture, and
`godot-painted-test.cwscenario` each author their own unit types
independently and are never loaded by the actually-played scene
(`CommandDemoScene` loads `bridgehead.cwscenario` specifically) — confirmed
by `grep -rln "bridgehead"` across `Corpus.fs`/`tests/CommandoWar.Sim.
Tests/*.fs` returning nothing. Touching any of them would be unrelated
scope creep, re-pinning dozens of unconnected corpus/test hashes for zero
player-facing benefit. No `CommandoWar.Sim`/`CommandoWar.Headless` code
change of any kind — this is a content-only fix.

### Verification

- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  import content/scenarios/bridgehead.cwscenario`
  - Result: `ok`, 6 friendly / 5 enemy agents, 3 objectives, unaffected
    shape.
- Command: a temporary `dotnet fsi` probe (removed after use), driving
  `CommandDemoScene` through the identical six-agent order sequence
  `CommandDemoDrive.runScriptedSelfCheck` issues, with the dev overlay
  enabled (`OnToggleDevOverlay`, ground truth, ignoring fog of war)
  - Result at tick 90: the machine-gun team (cell `(11,5)`) is `Dead`;
    exactly one agent total has died since world start (11 agents alive ->
    10); all six friendly agents remain rendered as full-opacity `Alive`
    figures. A first probe run (dev overlay off, the default player view)
    independently counted exactly 6 full-opacity friendly figures at every
    30-tick checkpoint from tick 30 through tick 300, cross-confirming no
    friendly death went undetected. "No friendly casualties, machine gun
    neutralised" is unchanged at half speed — reached the same way, just
    twice as slowly.
- Manual check: `Godot_v4.7.2-stable_mono_win64_console.exe --headless
  --path src/CommandoWar.Client.Godot scenes/CommandDemo.tscn --
  --selfcheck`
  - Result: `MATCH 0x84A25E3559111E9B` at tick 90 (re-pinned, genuine
    change confirmed above, not accepted on trust).
- Manual check: same, `scenes/SnapshotDemo.tscn` / `scenes/
  AppraisalDemo.tscn`
  - Result: `MATCH 0x6213D672BC36FDB8` / `MATCH 0xA1354EB998FC1B95`, both
    unaffected (neither loads Bridgehead).
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `415/415` passed, unchanged.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus`
  - Result: `19/19` entries match, unaffected.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  replay-file content/replays/envelope-full.cwreplay`
  - Result: `checkpoints : OK (24 ticks match the file's committed
    hashes)`, unaffected.
- Command: `dotnet build` (main `.slnx` and the Godot client `.slnx`)
  - Result: `0 Warning(s)`, `0 Error(s)` both times.

### Evidence

- The temporary timing diagnostic's own output (above) is the direct
  evidence ruling out a frame-pacing/engine bug before attributing the
  complaint to content tuning.
- The temporary dev-overlay probe's output is the direct evidence the
  re-pinned `--selfcheck` hash represents the same demonstrated outcome as
  before this task, not merely a different, unexamined one.

### Deviations and unresolved issues

- This does not address the live-agent chokepoint contention TASK-066/067
  already found and TASK-068 reproduced through a real multi-select group
  order — a separate, already-tracked, unrelated finding. Halving movement
  speed does not change which cells agents contend for, only how many ticks
  contention lasts.
- Not independently re-measured live: whether 100ms/cell (`MoveSpeed = 1`)
  is itself the right target feel, or whether Dave will want it slower or
  faster still once he plays it — `MoveSpeed = 1` was chosen as the most
  directly justified value from his own "twice as fast" framing, not
  independently tuned through iterative playtesting.

### Documents updated

- `tasks/TASK-069-BRIDGEHEAD-TROOPER-MOVE-SPEED.md` (status, acceptance
  criteria, verification, evidence, review).
- `docs/11_BACKLOG.md` (new B-068 row).
- `docs/12_PROGRESS_LEDGER.md` (this row).
- `PROJECT_STATE.yaml` (`active_work`, top-level `updated`).
- `src/CommandoWar.Client.Godot/README.md` (pinned-hash note).

### Review

- Reviewer: Dave
- Accepted: yes, 2026-09-21 ("seems just as fast, no matter, commit and
  lets move on"). Honestly flagged, not smoothed over: this is acceptance
  despite the fix not resolving Dave's own felt complaint -- the mechanism
  change is independently verified correct (ticks-per-cell doubled), but he
  reported no perceptible difference and chose not to pursue further tuning
  now. Left as a one-line, fully reversible follow-up if he revisits it.
