## 2026-09-20 - TASK-066 - A dead agent's corpse stops blocking movement

**Owner:** Dave
**Source revision:** `main` at `89cf16b` (TASK-064 accepted; TASK-065's own
work uncommitted in the working tree at the time this task started)
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot `4.7.2-stable_mono_win64`
**Status change:** `proposed -> review` (self-verified)

### Changes

Realises B-066. Raised live: after TASK-065 shipped, Dave asked to be able
to actually watch a scripted playthrough rather than only read a headless
hash, so a new `--screenshot-squad <frameCount> <path>` capture mode was
added to `FSharpSceneHost.cs` (reuses `CommandDemoDrive.
runScriptedSelfCheck`'s own six-agent click sequence through the real
`OnClick`/`OnHover` UI path, stays unpaused, captures at any requested
frame). Watching the resulting filmstrip (ticks 8, 14, 30, 90, 150, 400)
showed most of the squad jamming at the bridge chokepoint and the scene
staying frozen for the rest of the run. Dave's diagnosis: "the agents
should work like real ones, they would move over a dead body, or just
around it if there is space" -- exactly TASK-064's own already-flagged,
already-accepted-as-a-known-gap finding, now selected to fix.

Root cause, confirmed by inspection: `Simulation.navigationAndMovement`'s
`occupantOf` map (`agents |> Array.mapi (fun i a -> a.Position, i) |>
Map.ofArray`) includes every agent regardless of `Vitals`. `Pathfinding.fs`
itself has no occupancy concept at all -- the "cannot enter" behaviour is
entirely local to this one map, so the fix needed no `Pathfinding` change.

- `src/CommandoWar.Sim/Simulation.fs`: `occupantOf` now excludes any
  non-`Alive` agent (`Casualty.isAlive`), so a corpse's cell reads as free
  and a live agent walks through/onto it exactly as if empty. A dead agent
  can never itself be a *mover* either (the pre-existing `if not
  (Casualty.isAlive a.Vitals) then Idle` branch in the intents
  computation), so this filter only ever changes what OTHER agents may
  enter, never what the corpse itself does.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: new
  `--screenshot-squad` mode (built to investigate this, documented here
  since TASK-065 shipped it without documentation).

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `414/414` passed (+2: the two new `SimulationTests` facts).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus` (no `--regenerate`)
  - Result: `19/19` entries match their already-committed tables
    unchanged -- no existing entry combines a death with a subsequent move
    through that exact cell, so this fix moves no existing hash at all.
- Manual check: `Godot_v4.7.2-stable_mono_win64_console.exe --headless
  --path src/CommandoWar.Client.Godot -- --selfcheck`
  - Result: `MATCH 0x047FF3080AD3EBCB` at tick 90, unchanged (no friendly
    dies within that scripted window).
- Manual check: `--screenshot-squad` re-run at ticks 54/104/207/404 against
  the real `bridgehead.cwscenario`.
  - Result: byte-identical composition to the pre-fix filmstrip at every
    tick checked -- see Deviations below.

### Evidence

- The two new `SimulationTests` facts are the direct proof of the fix
  itself (a live agent enters a `Dead`/`Incapacitated` agent's cell without
  ever obstructing).
- The `--screenshot-squad` filmstrip comparison showed no visible
  difference for this specific repro; recorded honestly rather than
  claimed as visual proof the fix lacks.

### Deviations and unresolved issues

- **This task's own fix does not resolve the squad-jam Dave actually
  watched.** That jam happens among five of six agents while all are still
  `Alive` -- pure formation-offset contention at the bridge chokepoint, not
  a corpse block. No agent dies within the 600-tick window captured. The
  fix is correct and complete for what it was scoped to do (a dead agent's
  cell no longer blocks), but the visible "nothing moves" complaint needs
  the separate, larger, explicitly-parked B-067 (multi-select + joint
  squad orders, formation no longer auto-applying to a lone individual
  order) to actually change.
- `--screenshot-squad` is new, general-purpose tooling, not
  task-065-specific or task-066-specific evidence; documented in
  `src/CommandoWar.Client.Godot/README.md` as its own small section rather
  than folded into either task's own narrative.

### Documents updated

- `tasks/TASK-066-CORPSES-STOP-BLOCKING-MOVEMENT.md` (status, acceptance
  criteria, verification, evidence, review).
- `docs/11_BACKLOG.md` (B-066 row `proposed -> review`; B-067 added,
  proposed, parked).
- `docs/12_PROGRESS_LEDGER.md` (this row).
- `PROJECT_STATE.yaml` (`active_work`, top-level `updated`).
- `src/CommandoWar.Client.Godot/README.md` (new `--screenshot-squad`
  section).

### Review

- Reviewer: Dave
- Accepted: yes, 2026-09-20, on the self-verification evidence alone --
  confirmed via `AskUserQuestion` in the same round as TASK-065's
  acceptance and the decision to commit the combined working tree. Correct
  and complete for its own narrow scope; explicitly does not fix the
  live-agent contention Dave actually watched -- accepted as a known,
  tracked gap, not pursued further here (B-067, parked).
