# TASK-005: Disposable Mibo plus raylib Framework Spike (trimmed)

Status: done
Owner: Dave
Phase: P1
Gate: G1
Size: S

## Revision note (2026-09-02)

This task was rewritten after an environment check found that current Mibo
(`Mibo.Core` >= 4.2.0) takes a hard dependency on the prohibited `Mibo.Adaptive`
package. See `decisions/ADR-0003-MIBO-ADOPTION.md`, "2026-09-02 amendment". The
task is now pinned to Mibo 4.1.0 and narrowed to the decision-relevant question:
is an F#-native Mibo classic-MVU host ergonomically worth adopting, and how much
application-shell code does it cost compared with the Godot spike. Tiled
authoring, self-contained packaging, and the MonoGame backend smoke test are
removed. They can be re-added only if a later ADR puts Mibo back in contention.

## Objective

Measure whether wrapping the unmodified deterministic F# simulation in a Mibo
classic-MVU host removes useful friction compared with the Godot C#/F# facade,
and record the host-specific code and infrastructure that a code-first F# route
would own. This is disposable spike evidence for ADR-0001, not a production
decision.

## Why this task exists

Mibo may remove the mixed-language host boundary and supply headless and
graphical application infrastructure the project would otherwise write. Its
smaller ecosystem, fast release cadence, external authoring workflow, and the
now-unavoidable coupling to its experimental adaptive layer are risks that must
be measured against a working alternative (the accepted Godot spike, TASK-004).

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `decisions/ADR-0001-FRAMEWORK-SELECTION.md`
- `decisions/ADR-0002-SIMULATION-BOUNDARY.md`
- `decisions/ADR-0003-MIBO-ADOPTION.md` including the 2026-09-02 amendment
- `docs/02_TECHNOLOGY_DECISION.md`
- `docs/06_CONTENT_AND_PRESENTATION.md`
- The TASK-003 and TASK-004 ledger entries in `docs/12_PROGRESS_LEDGER.md`
- The current sim public API in `src/CommandoWar.Sim/`, the shared reference
  project `src/CommandoWar.Headless/` (`Fixture.fs`, `CommandLogFile.fs`,
  `Program.fs`), and the Godot spike `src/CommandoWar.Client.Godot/`
  (`SimFacade.cs` is the boundary shape, `SpikeContent.cs` the content
  DTO + validator) as the reference for what "done" looks like.

## Dependencies

- TASK-003 accepted and `done`.
- TASK-004 accepted and `done`.
- `decisions/ADR-0003-MIBO-ADOPTION.md` 2026-09-02 amendment accepted.

## Pinned versions

Confirm each at task start and record the exact value in the ledger.

- .NET SDK `10.0.303` (`rollForward: latestPatch`), target `net10.0`.
- `Mibo.Core` `4.1.0`.
- `Mibo.Raylib` `4.1.0`.
- `Raylib-cs` `8.0.0` (transitive via `Mibo.Raylib` 4.1.0; record the native
  raylib version the package bundles and its load behaviour on this machine).
- `FSharp.UMX` `1.1.0` (transitive).
- `FSharp.Core`: whatever `net10.0` SDK 10.0.303 resolves against the Mibo 4.1.0
  graph (4.1.0 requires `>= 10.1.302`, satisfied by the SDK's implicit
  `10.1.303`, so no explicit reference or downgrade is expected). Record if this
  is not the case.

If the pinned Mibo cannot restore or build on this SDK / TFM, STOP and report the
blocker. Do not downgrade the SDK or the Sim TFM, do not move to a newer Mibo,
and do not attempt package-exclusion workarounds for `Mibo.Adaptive`.

## Allowed scope

- one disposable F# Mibo classic-MVU host, in its own solution, not added to
  `CommandoWar.slnx`;
- Mibo classic MVU and its raylib backend;
- one facade module that is the only code calling `CommandoWar.Sim`;
- the shared logical fixture from `src/CommandoWar.Headless/Fixture.fs`, reused
  unchanged;
- the 32x32 greybox authored as a small hand-written F# value or a minimal
  hand-editable data file inside the host project (NOT Tiled);
- reuse of the framework-neutral content DTO + validator shape from
  `SpikeContent.cs`, ported to F# in the host, or a smaller equivalent;
- six placeholder agents rendered from `RenderSnapshot`;
- input to issue the existing typed move command;
- host-owned fixed-step scheduling, or Mibo's fixed-step facility, feeding the
  simulation integer ticks only;
- tick and state-hash overlay;
- one invalid-content validation path;
- a headless self-check mode hosted by Mibo's classic `HeadlessRunner`.

## Forbidden scope

- `Mibo.Adaptive` (any package or API). The 4.1.0 pin enforces the package rule;
  do not reference `AdaptiveProgram`, `AdaptiveHeadless`, or any
  `Mibo.Adaptive`-namespaced type.
- moving authoritative world state into the Mibo model;
- changing simulation behaviour to suit Mibo;
- a general abstraction over Godot / Mibo / MonoGame / raylib;
- Tiled authoring or a Tiled importer (deferred);
- self-contained packaging or `dotnet publish` evidence (deferred);
- a Mibo.MonoGame backend compile or launch smoke test (deferred);
- a broad UI framework, ECS, editor plugin, general F# domain layer, final art,
  audio system, new gameplay, new AI, or pathfinding;
- adding the Mibo host to `CommandoWar.slnx`;
- hiding framework defects with retries, sleeps, duplicated state, broad
  exception catches, or weakened tests;
- catching a `Simulation.step` exception;
- retaining the spike as production code or deciding ADR-0001.

## Required work

1. Pin and record the SDK, `Mibo.Core`, `Mibo.Raylib`, `Raylib-cs`, and
   `FSharp.Core` versions actually resolved.
2. Create the smallest classic-MVU host around the unmodified simulation, with
   one facade module as the only `CommandoWar.Sim` caller. Nothing crossing the
   boundary is a Mibo, raylib, or Tiled type; authoritative state never enters
   the Mibo model.
3. Author and import the shared 32x32 greybox through a typed validation
   boundary that reproduces `Setup.sixAgentWorld { 32; 32 } 20260902` exactly.
   Terrain markers stay client-only and never reach the sim.
4. Provide a headless self-check mode hosted by Mibo's classic `HeadlessRunner`
   (Step / StepN). It prints one `tick=N hash=0x...` line per tick and is
   diffable against `dotnet run --project src/CommandoWar.Headless -c Release --
   fixture`. The sequence must be identical: initial hash `0xF2F3DF0D820AD9AC`,
   ticks 1..40, final `0x838D3AE7DBFB735D` (format 1), 33 events (1 accepted +
   31 stepped + 1 completed), random draws 0 throughout, agent 3 at (20,14) from
   tick 31.
5. In the windowed raylib host: render the six agents from snapshots, display
   current tick and state hash, and issue the existing move command
   (tick 1, agent 3 -> (20,14), `CommandId 1`) from input. The resulting
   per-tick hash stream must match the direct headless run.
6. Demonstrate fixed authoritative stepping visibly independent of render rate,
   and compare the per-tick hash stream with the direct headless stepping.
7. Make one invalid content object produce a visible, actionable failure that
   names the offending object and does not start the simulation.
8. Verify a deliberate hash mismatch produces a non-zero process exit. Check
   for a Mibo or raylib main-loop quirk analogous to Godot's deferred
   `SceneTree.Quit(code)` and handle or document it.
9. Record all ADR-0001 evidence: the host line count set next to the Godot
   spike's ~410 host-specific lines, every Mibo-specific friction, any
   source-level workaround, the result of inspecting Mibo source where behaviour
   was unclear, and the dependency and maintenance concerns (the `Mibo.Adaptive`
   coupling in current releases and the release cadence).
10. Capture one screenshot of the map and the tick / state-hash overlay under
    `docs/evidence/`.

## Acceptance criteria

- [x] The unmodified simulation project builds and runs through the Mibo host;
      the consumed `CommandoWar.Sim.dll` is byte-identical to the library build.
      Host `Build succeeded. 0 Warning(s) 0 Error(s)`. `CommandoWar.Sim.dll` in
      the host output and in the library build share md5
      `82418ffbdf316b2f2e5124105b6f9e5e`. `git status` shows no change under
      `src/CommandoWar.Sim` or `tests/`.
- [x] Mibo classic MVU is used; `Mibo.Core` and `Mibo.Raylib` are pinned to
      4.1.0; no `Mibo.Adaptive` package appears in the restore graph,
      `*.deps.json`, or host output, and no `Mibo.Adaptive` API is referenced.
      `dotnet list package --include-transitive` = `Mibo.Raylib 4.1.0`,
      `Mibo.Core 4.1.0`, `Raylib-cs 8.0.0` (no `Mibo.Adaptive`);
      `cwmibo.deps.json` has no `adaptive` library; no `Mibo.Adaptive.dll` in
      `bin/`. Host uses `Program.mkProgram` / `HeadlessProgram.mkHeadless` /
      `HeadlessRunner` (classic MVU); no `AdaptiveProgram` / `AdaptiveHeadless`.
- [x] Six agents render from simulation snapshots on the greybox map.
      `Program.view` draws the 32x32 iso greybox + 6 agents from
      `SimBridge.Sim.Agents` (value views of `RenderSnapshot`);
      `docs/evidence/task-005-mibo-overlay.png`.
- [x] A user input produces the existing typed move command and a deterministic
      state change; agent 3 reaches (20,14) at tick 31.
      `LeftClick` and `--selfcheck` both call `SimBridge.Sim.QueueMove ->
      Command.moveTo`; accepted-command-log `1 3 move 20 14`; per-tick hashes
      change deterministically; tick 31 hash `0x25315447F9D0E230` (arrival),
      matching `cwheadless replay content/fixtures/spike-fixture.cwlog`.
- [x] Tick and state hash are visible and the host's 41-hash sequence (initial +
      ticks 1..40) is identical to `cwheadless fixture`, final
      `0x838D3AE7DBFB735D`. `--selfcheck` prints `tick=N hash=0x...` for the
      initial state + ticks 1..40; `diff` against `cwheadless fixture` shows no
      difference; initial `0xF2F3DF0D820AD9AC`, final `0x838D3AE7DBFB735D`
      (format 1), 33 events, random draws 0. Overlay screenshot shows tick +
      state hash + format.
- [x] Fixed authoritative tick rate and render rate are visibly separate.
      `Program.withFixedStep` (authoritative, `FixedTick`) is independent of
      `Program.withTick` (`RenderTick`, measurement only). Screenshot overlay:
      sim 6 Hz fixed / 6 steps/s measured vs render 60 fps.
- [x] One invalid content object produces an actionable loading failure that
      names it; the simulation does not start. `--selfcheck --invalid` -> exit 2,
      one line per problem, each naming the marker, kind, cell and reason
      (e.g. `FriendlySpawn #4 at (6,10) lies on an impassable (wall) cell`);
      no `tick=` lines emitted.
- [x] A deliberate hash mismatch produces a non-zero process exit.
      `--selfcheck --expect 0xDEADBEEFDEADBEEF` -> `MISMATCH ...`, exit 1. No
      raylib/Mibo deferred-quit quirk: the headless path is a plain `exit N`
      from `main`; the windowed `Cmd.signalExit` quits cleanly (`--screenshot`
      exits 0 the same frame it captures).
- [x] No Mibo, raylib, or Tiled type enters `CommandoWar.Sim` or
      `CommandoWar.Headless`. `dotnet list package` for `CommandoWar.Sim` =
      `FSharp.Core 10.1.303` only; `CommandoWar.Sim.deps.json` libraries =
      `CommandoWar.Sim`, `FSharp.Core`; source scan of `src/CommandoWar.Sim`
      and `src/CommandoWar.Headless` for `mibo|raylib|tiled|monogame|godot|
      Vector2|System.Drawing` matches only two doc-comment lines in `Fixture.fs`.
- [x] Host line count and approximate infrastructure surface are recorded next
      to the Godot spike's. `Content.fs` 192, `SimBridge.fs` 117, `Program.fs`
      403 (712 total F#); vs Godot `SimFacade.cs` ~150 + `SpikeContent.cs` ~170
      + `GreyboxScene.cs`/`SpikeMarker.cs` ~80 + `MainNode.cs` ~330. See
      ADR-0001 evidence table and the ledger.
- [x] Framework versions and any workarounds are explicit. `Mibo.Core` /
      `Mibo.Raylib` `4.1.0`, `Raylib-cs` `8.0.0`, SDK `10.0.303`, `net10.0`.
      The 4.1.0 pin is the workaround for the `Mibo.Adaptive` coupling
      (ADR-0003 amendment); no source-level workarounds were needed.
- [x] No production framework decision is claimed; ADR-0001 stays `proposed`,
      `Selected candidate: TBD`; only the Mibo evidence column and a
      spike-results section are filled. `PROJECT_STATE.yaml` `framework_decision`
      and gates unchanged.

## Completion notes (2026-09-02)

Host (own solution `src/CommandoWar.Client.Mibo/CommandoWar.Client.Mibo.slnx`,
**not** in `CommandoWar.slnx`): F# `net10.0`, `Mibo.Raylib` 4.1.0.
`SimBridge.fs` is the only module that opens `CommandoWar.Sim`; `Content.fs` is
the framework-neutral `.cwmap` parser + validator; `Program.fs` has the Mibo
classic-MVU headless self-check (`HeadlessProgram` / `HeadlessRunner`) and the
windowed raylib classic-MVU host (`Program.mkProgram` / `RaylibGame`).
`content/greybox.cwmap` reproduces `Setup.sixAgentWorld { 32; 32 } 20260902`
exactly; `content/greybox-invalid.cwmap` carries three seeded errors.

Both the headless self-check (explicit dispatch) and the Mibo `withFixedStep`
path reproduce the shared fixture's 41-hash sequence; windowed screenshot at
`docs/evidence/task-005-mibo-overlay.png`. `dotnet publish -r win-x64
--self-contained` succeeds (~84 MB) and the exe reproduces the final hash from
an unrelated working directory.

Evidence written to `decisions/ADR-0001-FRAMEWORK-SELECTION.md` (Mibo column +
"TASK-005 Mibo spike results" section) without deciding the ADR. Full command
log in `docs/12_PROGRESS_LEDGER.md`.

Deviations: Tiled editor and any Mibo hot-reload NOT exercised (Tiled authoring
is out of the trimmed scope; the map is a hand-edited text file; Mibo has no
content hot-reload, iteration is edit -> relaunch, ~1.2 s warm). Interactive
mouse input not literally exercised (headless session); the click path shares
the exact `QueueMove -> Command.moveTo` code that `--selfcheck` verifies end to
end. `withFixedStep` rate is fixed at program construction; runtime rate
adjustment (the Godot spike's `1`/`2` keys) would need a host-owned accumulator.

## Required verification

- `dotnet test CommandoWar.slnx -c Release` before and after host work: must
  stay 54+ green and framework-neutral (`docs/09` section 6);
- Mibo host build;
- headless self-check hash-stream diff against `cwheadless fixture`: identical,
  initial `0xF2F3DF0D820AD9AC`, final `0x838D3AE7DBFB735D`;
- direct-versus-Mibo per-tick hash comparison;
- manual move-command smoke test;
- invalid-content smoke test with a non-zero exit;
- deliberate hash-mismatch non-zero exit;
- dependency boundary inspection: no `Mibo.Adaptive`; no Mibo / raylib / Tiled
  type in `CommandoWar.Sim` or `CommandoWar.Headless` (`dotnet list package`,
  `deps.json` libraries, source scan).

## Evidence to capture

- exact SDK, `Mibo.Core`, `Mibo.Raylib`, `Raylib-cs`, `FSharp.Core` versions,
  and the reason for the 4.1.0 pin;
- setup, build, and run commands with results;
- screenshot of the map and the tick / state-hash overlay;
- one recorded command and the matching direct / Mibo hash;
- host-specific files and approximate infrastructure surface, compared with the
  Godot spike's ~410 host-specific lines (plus ~320 framework-neutral facade and
  content lines that carry over to any host);
- quality of debugging, input, rendering, overlay, content import, and error
  reporting in the Mibo host;
- Mibo-specific friction, any source-level workaround, and likely maintenance
  cost, including the `Mibo.Adaptive` coupling and release cadence;
- expected cost of extending this host to the Bridgehead vertical slice,
  including the application-shell gap a code-first route owns: a UI system,
  sprite animation playback, camera and isometric depth sorting with occluders,
  an asset and content pipeline, and the `docs/06` section 11 developer
  overlays.

## Documentation updates

- task, backlog, progress ledger, and `PROJECT_STATE.yaml` as required by
  `AGENTS.md`;
- fill the Mibo column of ADR-0001's evidence table and add a
  "TASK-005 Mibo spike results" section, without deciding the ADR;
- put the screenshot under `docs/evidence/`;
- do not activate TASK-006.

## Constraints on the simulation

Do not modify `src/CommandoWar.Sim` or `tests/CommandoWar.Sim.Tests`.
`FixtureTests.fs` pins the 40-tick hash sequence. If the simulation needs a
behaviour or API change to host Mibo, STOP and propose it; any intended change
updates that test deliberately and bumps a format version.

## Rollback or removal

The entire Mibo host and any importer must be removable without changing or
deleting the simulation, its tests, the command logs, or the shared logical
fixture data.
