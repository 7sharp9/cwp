## 2026-09-21 - TASK-071 - Replay playback scrubber in the Godot client

**Owner:** Dave
**Source revision:** working tree on top of `main` at `96bb1f7` (TASK-071
drafted and selected)
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot `4.7.2-stable_mono_win64`
**Status change:** `proposed -> review` (self-verified)

### Changes

Realises backlog row B-064: a new, read-only Godot scene
(`CwClientCore.ReplayDemoScene`, `scenes/ReplayDemo.tscn`) loads a
production `.cwreplay` file and lets the player scrub, play, pause, and
step through every recorded tick -- the "replay playback" bullet
`docs/07_VERTICAL_SLICE.md` section 6 has named as required presentation
since it was written, distinct from and never covered by
`content/diagnostics/demo.html`'s existing static, headless-only scrubber.

`ReplayDrive.load` (`ReplayDemoScene.fs`) reuses the exact pipeline
`cwheadless replay-file` (`Program.cmdReplayFile`) already uses for
verification -- `ReplaySerialisation.parse`, resolve `Meta.Scenario`
against `Corpus.all` (the `Program.resolveScenario` pattern, `private`
there so re-derived rather than exposed across the assembly boundary),
`ReplaySerialisation.toReplayRecord`, `Replay.run SimConfig.standard` --
and retains the resulting `ReplayOutcome.TickStates: WorldState[]` for the
scene's lifetime (central decision 3 from the drafting session: reuse the
existing full in-memory state, not the checkpoint-cadence mechanism
`Replay.fs`'s own doc comment names as a future scale replacement). No
`CommandoWar.Sim`/`CommandoWar.Headless` change of any kind.

`IClientScene` gained four new members
(`TickCount`/`CurrentTick`/`SetTick`/`CurrentHash`), each with a
mechanical no-op/zero default added to the two existing implementers
(`CommandDemoScene`, `DemoRenderScene`) -- neither scene's actual
behaviour changed. `FSharpSceneHost.cs` gained a fixed screen-space scrub
bar (`ScrubBarRect`, the `OrderModeIconRect`/`DrawOrderModeBar` chrome
precedent): a click or drag inside it calls `SetTick` continuously;
`Left`/`Right` arrow keys step one tick; `Space` (the existing
`OnTogglePause` wiring, reused rather than adding a second toggle)
starts/stops "playing" -- `ReplayDemoScene.Update` then advances
`currentTick` forward at a fixed 4 ticks/second while playing, stopping
automatically at the final tick. Every scrub-bar hit-test/draw is gated on
`_scene.TickCount() > 0`, so it never appears on `CommandDemoScene`/
`DemoRenderScene`. `RenderShared.fs` gained one new shared helper
(`deadCross`, extracted from `CommandDemoScene.renderVitals`'s own inline
`Dead` case rather than touching that file, which this task's forbidden
scope excludes). `Ready`'s existing `scenarioContentPath: string`
parameter is reused, for this scene only, to carry a `.cwreplay` path
instead of a `.cwscenario` one -- a fixed, committed fixture
(`content/replays/chokepoint-detour.cwreplay`, the existing 10-tick
corpus entry, no new fixture authored) rather than an in-scene file
picker, the smallest thing that loads real content.

Rendering per scrubbed tick: terrain built once from the initial
`WorldState.Terrain` (terrain is static across a run); agents mapped
directly from that tick's own `WorldState.Agents: AgentState[]` (`Vitals`
is a genuine `AgentState` field, so no `Diagnostics.frame` call is needed
the way `CommandDemoScene`'s live vitals rendering uses) -- `Alive` draws
the full Kenney figure exactly as the live scenes do, `Dead` draws the
`deadCross` marker, `Incapacitated` draws a translucent circle (reusing
the existing "translucent `Kind = 1` = not a full living agent" convention
`TryHitAgentCircle` already establishes, rather than reproducing
`CommandDemoScene`'s fuller badge-text vitals presentation -- a deliberate
simplification, documented, not smoothed over). No inter-tick
interpolation: scrubbing can jump to any tick in either direction, so
there is no well-defined "previous facing" to freeze on or lerp toward
the way a continuously-live scene has; each tick's facing is derived
fresh from that tick's own `Position`/`Destination` alone.

### Verification

- `dotnet build CommandoWar.slnx -c Release`: `0 Warning(s)`, `0 Error(s)`.
- `dotnet test CommandoWar.slnx -c Release`: `422/422` passed, unaffected
  (no `CommandoWar.Sim` change).
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`:
  `OK - all 20 entries match their committed tables`, unaffected.
- `dotnet run --project src/CommandoWar.Headless -c Release --
  replay-file content/replays/chokepoint-detour.cwreplay`: ground truth
  re-derived directly (not guessed) -- initial hash `0x745B1AE1EC2F01C1`,
  final hash `0x5876C1280DDAE2CB` at tick 10, ten intermediate hashes
  recorded.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: `0 Warning(s)`, `0 Error(s)`.
- `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path
  src/CommandoWar.Client.Godot scenes/ReplayDemo.tscn -- --selfcheck`:
  scrubs tick 0 through 10 via the scene's own real `SetTick`/
  `CurrentHash` path, reproducing the ground-truth hash chain above
  exactly at every tick -- `MATCH expected final hash 0x5876C1280DDAE2CB
  at tick 10`.
- Same command against `CommandDemo.tscn`: `MATCH 0x84A25E3559111E9B` at
  tick 90, unchanged. Against `SnapshotDemo.tscn`: `MATCH
  0x6213D672BC36FDB8` at tick 20, unchanged. Neither re-pinned.
  `AppraisalDemo.tscn` has no `--selfcheck` path at all (see Deviations
  below) -- nothing to reconfirm there.
- A windowed (non-`--headless`) `--screenshot` capture of `ReplayDemo.tscn`
  (auto-"playing" via a new screenshot-priming conditional, the existing
  per-scene priming precedent in `FSharpSceneHost._Ready`) confirmed real
  rendering: terrain, both agents, the HUD line reading `replay
  chokepoint-detour tick 3 / 10 hash 0x06093857E6AC7B18 agents 2 PLAYING`
  (hash matching the ground-truth table exactly), and a scrub bar filled
  ~30% with its handle at the matching screen position.
- `git status --short`/`git diff --stat` against this task's Allowed
  scope: confirmed no `CommandoWar.Sim`/`CommandoWar.Headless` entry. One
  unrelated editor-triggered reformat of
  `src/CommandoWar.Client.Godot/tools/ExportTerrainScript.cs` (the
  recurring, already-documented wart -- TASK-063/064 both caught and
  reverted the same thing) was caught and reverted, not committed.

### Evidence

- `docs/evidence/task-071-replay-scrubber.png`.
- All command output above.

### Deviations and unresolved issues

- **Wrong assumption found and corrected during implementation, not
  silently carried through:** this task's own drafting (Required reading,
  Acceptance criteria) assumed `AppraisalDemoScene` was a third
  `IClientScene`-implementing scene with its own `--selfcheck` hash to
  reconfirm. Inspection found it is a standalone `src/
  AppraisalDemoScene.cs` script predating `FSharpSceneHost`/ADR-0004
  entirely -- `AppraisalDemo.tscn`'s own `ext_resource` points at it
  directly, never at `FSharpSceneHost.cs` -- so it was never covered by
  `RunSelfCheck`'s `SceneType` switch and has no hash. Only
  `CommandDemoScene`/`DemoRenderScene` actually needed reconfirming; both
  are unchanged.
- `docs/07_VERTICAL_SLICE.md` was **not edited**, contrary to this task's
  own drafting plan (a per-bullet "realised by TASK-NNN" note). Checking
  the actual precedent during implementation found TASK-063 (mission
  summary, the neighbouring "mission completion and failure summary"
  bullet in the same section-6 list) never annotated that bullet inline
  either -- the realisation record lives in `docs/11_BACKLOG.md`/
  `docs/12_PROGRESS_LEDGER.md` instead. Followed that actual, established
  convention rather than the assumption this task file made at drafting
  time.
- **Honest caveat, the TASK-068 drag-gesture precedent:** the scrub bar's
  raw mouse press/motion/release handling in `FSharpSceneHost.
  _UnhandledInput` compiles clean and is exercised structurally (the
  self-check drives `SetTick` directly -- exactly what the drag handler
  itself calls -- and the windowed screenshot confirms auto-play advances
  `currentTick` and redraws the bar correctly), but an actual mouse
  press-drag-release across the bar in a live windowed session was not
  independently exercised in this environment. Flagged for Dave's own
  interactive pass, the same gap TASK-068's own rubber-band-select drag
  gesture was left with at that task's own review.
- Vitals presentation is deliberately simplified relative to
  `CommandDemoScene.renderVitals`: `Incapacitated` renders as a
  translucent circle (the existing "not a full living agent" convention),
  not the fuller darkened-figure-plus-text-badge treatment the live scene
  uses. A reasonable follow-up if Dave wants full presentation parity, not
  required for a working scrubber and not part of this task's forbidden
  scope (no change to `CommandDemoScene.fs` itself).
- No inter-tick interpolation was added -- confirmed a deliberate choice,
  not an oversight: scrubbing can jump to any tick in either direction,
  so there is no well-defined "in-progress edge" the way a continuously
  forward-stepping live scene has. Every tick renders each agent snapped
  to its own authoritative `Position`.
- No `Canonical.FormatVersion`, `ScenarioContent.Version`, or replay-file
  format-version change -- confirmed, this task is a pure client-side
  consumer of the existing, unchanged `ReplaySerialisation`/`Replay`
  contract.

### Documents updated

- `tasks/TASK-071-REPLAY-PLAYBACK-SCRUBBER.md` (`proposed -> review`, full
  self-verification evidence);
- `docs/11_BACKLOG.md` B-064 row (`proposed -> review`);
- `docs/12_PROGRESS_LEDGER.md` (this row + Pinned facts unaffected -- no
  `CommandoWar.Sim` test count changed);
- `PROJECT_STATE.yaml` `active_work`;
- `docs/evidence/task-071-replay-scrubber.png` (new).
- `docs/07_VERTICAL_SLICE.md` deliberately **not** edited -- see
  Deviations above.

### Review

- Reviewer: Dave
- Accepted: not yet.
