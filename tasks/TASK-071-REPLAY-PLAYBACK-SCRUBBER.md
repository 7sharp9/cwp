# TASK-071: Replay playback scrubber in the Godot client

Status: done (accepted by Dave 2026-09-21 on the self-verification
evidence)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises B-064
Size: M

## Objective

A new, dedicated, read-only Godot scene loads a production `.cwreplay` file
(`ReplaySerialisation` format) and lets the player scrub, play, and pause
through the recorded mission's every tick -- the "replay playback" bullet
`docs/07_VERTICAL_SLICE.md` section 6 has named as required presentation for
the vertical slice since it was written, and which has never been built
inside the actual game client. No order can be issued from this scene; it
is strictly a viewer over a finished run.

## Why this task exists

`docs/07_VERTICAL_SLICE.md` section 6 ("Required presentation") lists
"replay playback" as its own bullet, distinct from and alongside "mission
completion and failure summary" (built by TASK-063) and "developer overlay"
(built by TASK-011/043). It has stayed unbuilt since B-033 was split
2026-09-20 (`docs/11_BACKLOG.md` B-033/B-064 rows): mission summary and
replay playback were found to "share no design or implementation surface,"
so B-033 narrowed to mission summary alone and replay playback was deferred
to this row, explicitly "not designed, not built, not blocking." With
TASK-070/B-069 now accepted, B-063 (scenario-authoring tooling) and B-064
are the only two open `proposed` rows left at the current gate (G4); B-063
is explicitly marked "not blocking anything," so B-064 is the one real
remaining gap against section 6's own required-presentation list.

`content/diagnostics/demo.html` already gives a full scrubber, but it is a
static, generated-in-advance HTML file (`DiagnosticRender.Html`, every
tick's frame pre-baked to inline SVG, a JS range slider just toggles
visibility) -- explicitly not a live simulation and, per B-064's own
backlog text, not reusable inside a live Godot scene. This task is
specifically the harder, still-open half: a scrubber that lives inside the
actual game client.

Two parallel research passes this session (one tracing the existing replay/
serialisation mechanism end to end -- `Replay.fs`, `ReplaySerialisation.fs`,
`Snapshot.fs`, `Diagnostics.fs`, `cwheadless replay-file`; one tracing the
Godot client's scene architecture -- `IClientScene`, `FSharpSceneHost.cs`,
the existing order-mode icon bar and TASK-068 drag-tracking precedents)
found: a `.cwreplay` file is purely a command stream (seed, scenario name,
ordered commands, sparse per-tick checkpoint *hashes*, never full state);
`Replay.run` already recomputes and holds a full `WorldState` for every
tick of a run in memory (`ReplayOutcome.TickStates: WorldState[]`) each
time it executes, exactly the same way `cwheadless replay-file` already
uses it for verification today; and `FSharpSceneHost.cs` selects an
`IClientScene` implementation purely by a Godot-exported `SceneType`
string resolved via reflection, so a fourth scene needs no host
rearchitecture, only a new `.tscn` (the `SnapshotDemo.tscn`/
`CommandDemo.tscn`/`AppraisalDemo.tscn` precedent) and a new F# type
implementing the existing, unchanged `IClientScene` interface.

## Central decisions (confirmed with Dave 2026-09-21 via `AskUserQuestion`)

1. **Build the in-engine scrubber now**, rather than treat
   `content/diagnostics/demo.html`'s existing baked scrubber as sufficient
   and close B-064 with no further client work.
2. **A new dedicated scene** (e.g. `ReplayDemoScene`/`ReplayDemo.tscn`),
   rather than a "replay mode" bolted onto `CommandDemoScene` -- matches
   the existing one-scene-per-concern precedent and keeps live order-
   issuing state completely untouched by a read-only viewing mode.
3. **Reuse `Replay.run`'s existing full `TickStates: WorldState[]` as-is**,
   rather than build the checkpoint-cadence mechanism `Replay.fs`'s own
   doc comment names as the eventual replacement "when the world grows."
   Vertical-slice scale (Bridgehead: 6 agents, well under 200 ticks) is
   well within what `Replay.run` already holds in memory today for
   verification; this matches the project's own established pattern of
   not building scale infrastructure ahead of evidence it is needed (the
   B-012b precedent -- component subhashes reopened only if a real
   benchmark or counterexample demands them).

## Required reading

- `docs/07_VERTICAL_SLICE.md` section 6 ("Required presentation") in full,
  especially the "replay playback" bullet and how "mission completion and
  failure summary" (built, TASK-063) is described alongside it -- confirm
  what "playback" is actually expected to show (tick, world state, events)
  versus what TASK-063's summary panel already covers, so this task does
  not duplicate it.
- `docs/04_SIMULATION_SPEC.md` section 16 (or wherever the replay/
  determinism contract is specified) -- the `.cwreplay` format contract
  this task must not alter in any way.
- `docs/11_BACKLOG.md` B-033 and B-064 rows in full -- the original split
  rationale and every design fork B-064's own text already named.
- `src/CommandoWar.Sim/Replay.fs` in full: `ReplayOutcome`/`TickStates`'
  own doc comment (the "does not scale... checkpoint cadence replaces it
  when the world grows" caveat this task deliberately does not act on
  yet, per central decision 3), `run`'s exact signature and behaviour on
  an already-validated `ReplayRecord`.
- `src/CommandoWar.Sim/ReplaySerialisation.fs` in full: `ReplayCommandFile`
  (`Meta`, `InitialHash`, `Checkpoints`, `Commands`), `parse`,
  `toReplayRecord`, and every `ParseError` case -- this task's scene must
  handle a malformed or missing `.cwreplay` file with a real, readable
  error state, not a crash.
- `src/CommandoWar.Headless/Program.fs`: `resolveScenario` (204-207) and
  `cmdReplayFile` (215-329) in full -- the exact existing pipeline
  (`ReplaySerialisation.parse` -> resolve `Meta.Scenario` against
  `Corpus.all` -> `ReplaySerialisation.toReplayRecord` -> `Replay.run
  SimConfig.standard`) this task's scene reuses client-side, confirmed
  already reachable: `CommandoWar.Client.Godot.Core.fsproj` already
  references `CommandoWar.Headless.fsproj` (the TASK-029 precedent, its
  own doc comment names `DemoScenario`; `Corpus.all` is exposed by the
  same project) -- no new project reference is expected, confirm this
  during implementation rather than assuming the comment is still current.
- `src/CommandoWar.Sim/Snapshot.fs`: `RenderSnapshot`/`AgentSnapshot` --
  the lightweight per-tick read model `CommandDemoScene`/`DemoRenderScene`
  already build `DrawItem[]` from; decide during implementation whether
  this scene renders from a `RenderSnapshot` derived per-tick the same
  way, or from the richer `Diagnostics.frame`/`DiagnosticFrame` (more
  detail -- overlays, events -- but not what any existing scene's
  `DrawList` already knows how to turn into `DrawItem[]`).
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs` in full: the
  `DrawItem` vocabulary (`Kind` 0-5) and the full `IClientScene` member
  list (`Ready`, `Update`, `DrawList`, `HudText`, `OnClick`, `OnHover`,
  `OnDragSelect`, `OnTogglePause`, `OnOrderModeClick`/`OrderMode`,
  `OnToggleDevOverlay`, `MissionSummaryLines`, `Dispose`) -- confirm which
  members a read-only scrubber scene can implement as true no-ops
  (`OnClick`/`OnHover`/`OnDragSelect`/`OnOrderModeClick` -- no orders to
  issue) versus which it must give real behaviour
  (`Update`/`DrawList`/`HudText`).
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: `Ready`'s
  existing `ScenarioFile.parse`/`Scenario.validate`/`World.ofScenario`
  pipeline (596-605+) -- the shape this task's `Ready(path)` follows, but
  parsing a `.cwreplay` instead of a `.cwscenario`; also its `simHz`/
  `stepOnce`/`alpha`/`renderPos` interpolation mechanism -- confirm by
  inspection whether this task needs any interpolation at all (scrubbing
  to a specific tick index and rendering that tick's `RenderSnapshot`
  directly has no "partial tick" concept the way live real-time stepping
  does) or whether smooth inter-tick motion during `Play` is wanted
  (decide via `AskUserQuestion` if genuinely ambiguous once the concrete
  UI is drafted, rather than assumed either way here).
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: the `SceneType`
  reflection-based scene selection (`[Export] SceneType`, `Activator.
  CreateInstance`) -- confirm a fourth scene needs no host change beyond a
  new `.tscn`; the order-mode icon bar (`OrderModeBarOrigin`,
  `OrderModeIconRect`, `TryHitOrderModeIcon`, `DrawOrderModeBar`) as the
  direct precedent for a new scrub-bar's own hit-test/draw pair, owned in
  C# as HUD chrome, not a `DrawItem`; the existing drag-tracking fields
  (`_isDragging`, `_dragStartScreen`, `_dragCurrentScreen`,
  `DragThresholdPixels`) from TASK-068's rubber-band select as the nearest
  existing "drag a screen-space UI element" precedent to adapt for a
  scrub-handle drag, though a new, scrubber-specific field set is expected
  rather than literally reusing these particular fields (they are already
  owned by the marquee-select gesture).
- `src/CommandoWar.Client.Godot/scenes/SnapshotDemo.tscn` (or any existing
  scene file) -- the minimal `.tscn` shape (`[gd_scene]`, one
  `ext_resource` for `FSharpSceneHost.cs`, one `node` with `SceneType`
  set) this task's new scene file follows exactly.
- `AGENTS.md`'s diagnostics mandate (a task that adds or changes
  authoritative spatial/tactical state must extend `DiagnosticFrame` and
  add a golden) -- confirm this task adds no new authoritative state at
  all (pure read-only playback of already-existing recorded state), so
  the mandate does not apply; document that conclusion rather than
  silently skip it.

## Dependencies

- B-012 (done -- command recording/replay format this task reads, never
  writes). No other task selected.

## Inputs and assumptions

- `.cwreplay` format, `ReplaySerialisation.fs`, and `Replay.fs` are
  untouched by this task -- purely client-side, additive consumption of
  an existing, stable contract. No `Canonical.FormatVersion`,
  `ScenarioContent.Version`, or replay-file-format version change.
- The new scene is read-only: `OnClick`/`OnHover`/`OnDragSelect`/
  `OnOrderModeClick` are no-ops (or, if genuinely useful, `OnClick`/
  `OnHover` may support inspecting an agent's state at the current
  scrub position without issuing any order -- decide during
  implementation whether this is worth the scope, default to true no-ops
  if ambiguous, per Forbidden scope below).
- `IClientScene.Ready`'s existing `scenarioContentPath: string` parameter
  is reused to carry a `.cwreplay` file path for this scene specifically
  -- the interface itself does not change; each scene already interprets
  this parameter as whatever content it loads (`CommandDemoScene`/
  `DemoRenderScene` read a `.cwscenario`, `AppraisalDemoScene` reads
  something else again). Confirm this reuse is consistent with
  `IClientScene.fs`'s own doc comments before relying on it, and correct
  a stale comment if the parameter's doc text assumes only a scenario
  path.
- On `Ready`, the scene parses the `.cwreplay` once
  (`ReplaySerialisation.parse`), resolves `Meta.Scenario` via `Corpus.all`
  (the `resolveScenario` precedent), builds the `ReplayRecord`
  (`ReplaySerialisation.toReplayRecord`), and runs `Replay.run
  SimConfig.standard` once, retaining the full `ReplayOutcome.TickStates`
  array for the scene's lifetime (central decision 3) -- not re-run per
  frame, not re-run per scrub.
- A malformed file, an unresolvable scenario name, an initial-hash
  mismatch, or a `Replay.run` error must produce a real, on-screen error
  state (at minimum, `HudText` reporting the failure) rather than a
  silent blank scene or an unhandled exception -- the
  `resolveScenario`/`cmdReplayFile` error-message precedent, adapted for
  a scene that cannot `eprintfn`/exit.
- The scrub control is new HUD chrome in `FSharpSceneHost.cs` (a
  horizontal bar plus a draggable handle, play/pause and step-forward/
  step-back affordances), the `OrderModeIconRect`/`DrawOrderModeBar`
  precedent -- owned entirely in C#, computed from screen-space rects, not
  a new `DrawItem.Kind`. `IClientScene` needs new members (or reuses
  existing ones in a new way) to receive scrub position changes and
  report the current tick/total-tick count for the bar's own draw
  extent -- design the minimal interface addition during implementation
  rather than pre-committing to an exact shape here; keep it primitives-
  only per ADR-0004.
- Play/pause reuses `OnTogglePause`'s existing wiring where its semantics
  genuinely match (advancing the scrub position at a fixed real-time
  rate while "playing"); a dedicated Play/Pause is acceptable if the
  existing single-purpose toggle does not fit cleanly -- decide by what
  keeps the interface smallest, document the choice either way.
- No live simulation stepping occurs in this scene -- `Simulation.step`
  is never called; every tick's `WorldState` already exists in
  `TickStates`, looked up by scrub index.

## Allowed scope

- `src/CommandoWar.Client.Godot/Core/CwClientCore.Core.fsproj`: one new
  compiled file, the new scene type (e.g. `ReplayDemoScene.fs`).
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`: additive members
  only if genuinely required for scrub-position/tick-count communication
  (documented, minimal, primitives-only); no change to any existing
  member's signature or semantics.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: new scrub-bar
  hit-test/draw methods (the `OrderModeIconRect`/`TryHitOrderModeIcon`/
  `DrawOrderModeBar` precedent), new input handling in
  `_UnhandledInput` for the scrub gesture, wiring to call any new
  `IClientScene` members added above.
- `src/CommandoWar.Client.Godot/scenes/ReplayDemo.tscn` (or equivalent
  name) -- new scene file, the `SnapshotDemo.tscn` precedent.
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`: reused, not
  necessarily changed -- extend only if a genuinely shared helper (e.g.
  building a `DrawItem[]` from a `RenderSnapshot`/`WorldState` at an
  arbitrary tick) is missing and would otherwise be duplicated across
  scenes.
- A `--selfcheck`-equivalent self-test mode for the new scene, matching
  every existing scene's own precedent (a deterministic scripted
  sequence -- e.g. load a known committed `.cwreplay`, scrub to a fixed
  tick, hash or otherwise assert the rendered state -- proving the scene
  reaches a reproducible state through its own real input path, not just
  by inspection).
- `content/replays/`: a new, small, committed `.cwreplay` fixture if the
  self-check needs one not already covered by an existing corpus replay
  file (check `content/replays/CORPUS.md` first for a reusable entry
  before authoring a new one).
- `docs/07_VERTICAL_SLICE.md` (section 6's "replay playback" bullet gains
  its own realised-by note, the `TASK-063`/mission-summary precedent),
  `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Forbidden scope

- No change to `Replay.fs`, `ReplaySerialisation.fs`, `Simulation.fs`, or
  any other `CommandoWar.Sim` authoritative-state or determinism code --
  this task is a pure client-side consumer of an existing, stable
  contract.
- No checkpoint-cadence mechanism, no partial/incremental replay, no
  disk-persisted per-tick state -- central decision 3 explicitly defers
  this; `TickStates` is held in memory for this scene's lifetime only.
- No order issuing of any kind from this scene (no `Command.*`
  dispatch, no `IClientScene.OnClick`/`OnOrderModeClick` real behaviour)
  -- a pure viewer, never a second live-play surface.
- No change to `CommandDemoScene.fs`'s own behaviour, `--selfcheck`
  sequence, or pinned hash -- this task adds a wholly new, independent
  scene and must not touch the live-play scene at all.
- No new art assets -- reuse existing terrain/agent textures and
  primitive `DrawItem` kinds exactly as `DemoRenderScene`/`CommandDemoScene`
  already do; the scrub bar itself is drawn with Godot primitives (rects/
  lines), the `DrawOrderModeBar` precedent, not a new texture.
- No mission-summary duplication -- if `MissionOutcome`/objective state at
  the final tick is worth surfacing, point to TASK-063's existing
  `MissionSummaryLines` derivation rather than re-deriving it.

## Required work

1. Confirm the exact client-side replay-loading pipeline (parse -> resolve
   scenario via `Corpus.all` -> `toReplayRecord` -> `Replay.run`) compiles
   and runs from within `CwClientCore.Core.fsproj` as expected, before
   writing any scene logic.
2. Design and add the minimal `IClientScene` surface (or confirm existing
   members suffice) for: current scrub tick, total tick count, a way to
   set the scrub position, and play/pause state.
3. Implement `ReplayDemoScene` (`Ready` loads and runs the replay once;
   `DrawList` renders the `TickStates[currentTick]` world exactly as an
   existing scene renders a live tick; `HudText` reports tick/total and
   any load error).
4. Implement the scrub-bar HUD chrome in `FSharpSceneHost.cs`: draw, hit-
   test/drag-to-scrub, play/pause and step affordances, wired to the new
   `IClientScene` members.
5. Add `scenes/ReplayDemo.tscn`.
6. Add a `--selfcheck`-equivalent deterministic self-test proving the
   scene reaches a reproducible rendered state through its own real input
   path (load a fixed `.cwreplay`, scrub to a fixed tick, assert/hash).
7. Verify: Godot client builds; the new scene's self-check passes through
   the real Godot 4.7.2 editor; all three pre-existing scenes'
   `--selfcheck` hashes reconfirmed `MATCH` **unchanged** (this task must
   not touch them); `dotnet build`/`test`/`corpus`/`replay-file` on the
   main `.slnx` unaffected (no `CommandoWar.Sim` change expected).
8. Update documentation per Documentation updates below.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] A new Godot scene (`CwClientCore.ReplayDemoScene`,
      `scenes/ReplayDemo.tscn`) loads a committed `.cwreplay` file
      (`content/replays/chokepoint-detour.cwreplay`, an existing corpus
      fixture reused rather than authoring a new one) and renders its
      recorded world state at an arbitrary scrubbed tick, proven through a
      deterministic self-check: the scene's own real `IClientScene.
      SetTick`/`CurrentHash` path, driven tick by tick from 0 through 10,
      reproduces the exact per-tick canonical hash chain `cwheadless
      replay-file` independently prints for the same fixture (ground
      truth re-derived directly by running that command, not guessed or
      recalled).
- [x] Scrubbing (drag), play/pause, and step affordances all work through
      real input handling in `FSharpSceneHost.cs` (`ScrubBarRect` hit-test
      and continuous-drag tracking, `OnTogglePause`'s reused play/pause
      semantics, `Left`/`Right` arrow-key stepping), not only through the
      self-check's direct `SetTick` calls. **Partially verified only** --
      see Evidence to capture below for the honest caveat on the raw drag
      gesture specifically.
- [x] No order can be issued from this scene (confirmed by inspection:
      `OnClick`/`OnHover`/`OnDragSelect`/`OnOrderModeClick` are all literal
      no-ops in `ReplayDemoScene.fs`, no `Command.*` construction anywhere
      in the file).
- [x] `CommandDemoScene`/`DemoRenderScene`'s own `--selfcheck` hashes are
      reconfirmed `MATCH`, byte-identical to before this task, through the
      real Godot 4.7.2 editor. (`AppraisalDemoScene` corrected out of this
      criterion during implementation: it is a standalone `AppraisalDemoScene.
      cs` script, not an `IClientScene` implementer at all -- it was never
      covered by `RunSelfCheck`'s `SceneType` switch and has no hash to
      reconfirm; this was a wrong assumption in this task's own drafting,
      found and corrected, not silently carried through.)
- [x] No `CommandoWar.Sim` file changed (confirmed by `git diff` against
      Allowed scope); `dotnet build`/`test`/`-- corpus`/`-- replay-file`
      on the main `.slnx` unaffected.
- [x] `AGENTS.md`'s diagnostics mandate confirmed not applicable (no new
      authoritative state -- a pure read-only viewer over already-existing
      recorded state), documented rather than silently skipped.
- [x] Required documentation updated (see Documentation updates below).

## Required verification

- unit or property tests: `dotnet test CommandoWar.slnx -c Release` ->
  `422/422` passed, unaffected (no `CommandoWar.Sim` change).
- scenario or replay tests: `dotnet run --project src/CommandoWar.Headless
  -c Release -- corpus` -> `OK - all 20 entries match their committed
  tables`, unaffected; `dotnet run --project src/CommandoWar.Headless -c
  Release -- replay-file content/replays/chokepoint-detour.cwreplay` ->
  the ground-truth run this task's self-check pins against (final hash
  `0x5876C1280DDAE2CB` at tick 10).
- build: `dotnet build CommandoWar.slnx -c Release` -> `0 Warning(s)`,
  `0 Error(s)`. `dotnet build
  src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug` ->
  `0 Warning(s)`, `0 Error(s)`.
- manual smoke test: the real Godot 4.7.2 editor
  (`Godot_v4.7.2-stable_mono_win64_console.exe`, sibling directory, not on
  `PATH`) --
  `--headless ... scenes/ReplayDemo.tscn -- --selfcheck` ->
  `MATCH expected final hash 0x5876C1280DDAE2CB at tick 10`;
  `--headless ... scenes/CommandDemo.tscn -- --selfcheck` -> `MATCH
  0x84A25E3559111E9B` at tick 90, unchanged; `--headless ...
  scenes/SnapshotDemo.tscn -- --selfcheck` -> `MATCH 0x6213D672BC36FDB8`
  at tick 20, unchanged; a windowed (non-`--headless`) `--screenshot`
  capture of `ReplayDemo.tscn` confirmed the scene renders terrain, both
  agents, the HUD line, and a correctly-filled/positioned scrub bar/handle
  while auto-"playing" (`docs/evidence/task-071-replay-scrubber.png`, tick
  3/10, HUD hash matching the ground-truth table exactly).
- dependency boundary check: `git status --short`/`git diff --stat`
  against this task's Allowed scope -- confirmed no
  `CommandoWar.Sim`/`CommandoWar.Headless` entry; one unrelated
  editor-triggered reformat of `tools/ExportTerrainScript.cs` (the
  recurring, already-documented wart from several prior tasks) was caught
  and reverted, not committed.

## Evidence to capture

- All command output above.
- The new scene's self-check reproduces the ground-truth `cwheadless
  replay-file` hash chain exactly, tick by tick (0 through 10):
  `0x745B1AE1EC2F01C1` (tick 0, the initial state -- not covered by
  `ReplayOutcome.TickStates`, sourced from `record.InitialState` directly)
  through `0x5876C1280DDAE2CB` (tick 10, final) -- proving this scene's
  own `SetTick`/`CurrentHash` plumbing is byte-identical to the existing,
  independently-verified CLI tool, not a parallel reimplementation that
  might silently diverge.
- `CommandDemoScene`/`DemoRenderScene`'s `--selfcheck` hashes reconfirmed
  unchanged -- the mechanical, no-op `IClientScene` interface additions
  (`TickCount`/`CurrentTick`/`SetTick`/`CurrentHash`, all `0`/no-op for
  both) altered no observable behaviour, exactly as expected.
- **Wrong assumption found and corrected, not silently carried through:**
  this task's own drafting (Required reading/Acceptance criteria) assumed
  `AppraisalDemoScene` was a third `IClientScene`-implementing scene with
  its own `--selfcheck` hash. Inspection during implementation found it is
  a standalone `src/AppraisalDemoScene.cs` script predating
  `FSharpSceneHost`/ADR-0004 entirely (`AppraisalDemo.tscn`'s own
  `ext_resource` points at it directly, not at `FSharpSceneHost.cs`) --
  never covered by `RunSelfCheck`'s `SceneType` switch, no hash to
  reconfirm. Only two pre-existing scenes actually needed reconfirming
  (`CommandDemoScene`, `DemoRenderScene`); both are unchanged.
- **Honest caveat on the raw drag gesture, the TASK-068 precedent:** the
  scrub bar's `_UnhandledInput` press/motion/release handling compiles
  clean and is exercised structurally (the self-check drives `SetTick`
  directly, which is exactly what the drag handler itself calls, and the
  windowed screenshot confirms `OnTogglePause`-driven auto-play advances
  `currentTick` and redraws the bar correctly), but an actual mouse
  press-drag-release across the bar in a live windowed session was not
  independently exercised in this environment -- flagged for Dave's own
  interactive pass, the same gap TASK-068's own drag gesture was left
  with.
- No deviation from this task's other stated assumptions: `Ready`'s
  `scenarioContentPath` parameter reused for a `.cwreplay` path as
  planned; `Replay.run`'s existing full `TickStates` reused as-is, no
  checkpoint cadence; no inter-tick interpolation added (each tick snaps
  directly to its own authoritative `Position`/facing -- documented in
  `ReplayDemoScene.fs` as a deliberate simplification, not a defect); a
  load failure surfaces through `HudText()` (`"replay load FAILED:
  <message>"`) rather than a silent blank scene or an exception, though
  this specific path was verified by inspection/type-checking only (the
  committed fixture always loads successfully, so no failing `.cwreplay`
  was exercised live).

## Expected files

- `src/CommandoWar.Client.Godot/Core/ReplayDemoScene.fs` (new).
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs` (four new members:
  `TickCount`/`CurrentTick`/`SetTick`/`CurrentHash`).
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`,
  `DemoRenderScene.fs` (mechanical no-op implementations of the four new
  members only -- no other line changed, confirmed by `git diff`).
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs` (new `deadCross`
  helper, extracted for `ReplayDemoScene`'s use rather than shared with
  `CommandDemoScene`'s own inline copy, which is out of this task's
  allowed scope).
- `src/CommandoWar.Client.Godot/Core/CommandoWar.Client.Godot.Core.fsproj`
  (new `<Compile>` entry).
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs` (scrub-bar
  chrome/hit-test/drag handling, step-key handling, the new self-check
  case, the `Ready`-path and `--screenshot`-priming conditionals).
- `src/CommandoWar.Client.Godot/scenes/ReplayDemo.tscn` (new).
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`. `docs/07_VERTICAL_SLICE.md` deliberately **not**
  edited -- see Documentation updates below. No `content/replays/` change
  (the existing `chokepoint-detour.cwreplay` corpus fixture was reused
  as-is).

## Documentation updates

- this task file's status and evidence;
- `docs/07_VERTICAL_SLICE.md`: **not edited**. This task's own drafting
  assumed a per-bullet "realised by TASK-NNN" note (the `docs/11_
  BACKLOG.md`/section-6 precedent it named), but checking the actual
  precedent during implementation found TASK-063 (mission summary,
  section 6's neighbouring "mission completion and failure summary"
  bullet) did not annotate section 6 inline either -- the realisation
  record lives in `docs/11_BACKLOG.md`/`docs/12_PROGRESS_LEDGER.md`
  instead, matching that actual, established convention rather than the
  one this task file assumed at drafting time;
- `docs/11_BACKLOG.md` (B-064 row: `proposed -> review` with full
  self-verification evidence, the recent-task precedent);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
- `PROJECT_STATE.yaml`.

## Rollback or removal

Fully additive: one new scene type, one new `.tscn`, new HUD-chrome
methods in `FSharpSceneHost.cs`, and (if needed) new `IClientScene`
members that no existing scene is required to implement meaningfully
(a no-op default is acceptable for the other three). No `CommandoWar.Sim`
change, no canonical or replay-format version change expected -- a
`git revert` of this task's commit should not require re-pinning anything.
Confirm this expectation holds before relying on it.

## Review

- Reviewer: Dave
- Accepted: yes, 2026-09-21, on the self-verification evidence alone. The
  scrub bar's raw mouse drag gesture (flagged as not independently
  exercised live in this environment) was not separately re-tested before
  acceptance.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
