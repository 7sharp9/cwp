# TASK-004: Disposable Godot .NET Framework Spike

Status: done  
Owner: Dave  
Phase: P1  
Gate: G1  
Size: M

## Objective

Measure the cost and quality of using Godot .NET as the presentation and authoring host for the same deterministic F# simulation used by the Mibo spike.

## Why this task exists

Godot is the provisional shipping-oriented favourite because it supplies integrated content, UI, animation, import, and debugging tools. That advantage must be demonstrated without allowing Godot types or lifecycle to become authoritative.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `decisions/ADR-0001-FRAMEWORK-SELECTION.md`
- `decisions/ADR-0002-SIMULATION-BOUNDARY.md`
- `docs/02_TECHNOLOGY_DECISION.md`
- `docs/06_CONTENT_AND_PRESENTATION.md`
- TASK-002 and TASK-003 evidence

## Dependencies

- TASK-003 accepted and `done`

## Inputs and assumptions

- Recheck the current stable Godot .NET release before installation.
- Research snapshot suggests 4.7.2, but the task must record the actual pinned version used.
- Use the exact simulation assembly and fixture later used by TASK-005.

## Allowed scope

- one disposable Godot .NET client project;
- one thin C# facade or adapter over the F# simulation;
- one small isometric greybox map authored in Godot;
- six placeholder agents;
- input to issue the existing typed move command;
- fixed-step scheduling and simple visual interpolation;
- tick and state-hash overlay;
- one invalid-content validation path;
- one minimal UI or debug panel;
- build and local packaging evidence;
- spike observations and screenshots.

## Forbidden scope

- production architecture commitment;
- changing simulation behaviour to suit Godot;
- authoritative Godot physics, navigation, random, or animation;
- broad UI framework, final art, audio system, gameplay, new AI, pathfinding, plugin framework, editor plugin, or generalized C# domain layer;
- comparing unrequested platforms;
- retaining the spike as production code before ADR-0001 is accepted.

## Required work

1. Pin and record the Godot .NET version and required SDK.
2. Create the smallest host that loads the existing simulation through a plain .NET boundary.
3. Author a small isometric map and framework-neutral fixture conversion.
4. Render six agents from snapshots and display current tick and state hash.
5. Submit the existing move command through the C# adapter.
6. Schedule the fixed simulation tick independently from render rate.
7. Make one content error fail visibly and actionably.
8. Perform the common content-edit exercise from `docs/06_CONTENT_AND_PRESENTATION.md`.
9. Produce a release build or local package and launch it outside the editor where supported.
10. Record all evidence required by ADR-0001, including awkwardness and failed attempts.

## Acceptance criteria

- [x] The unmodified simulation project builds and runs through the Godot host.
      `CommandoWar.Sim.dll` consumed by the host is byte-identical to the
      library build (md5 `9168def701ba9bdeadafc1bd7918e9e6`); host build
      `0 Error(s)`; `--selfcheck` runs 40 authoritative ticks.
- [x] Six agents render on an isometric map from simulation snapshots.
      `MainNode._Draw` renders the 32x32 iso greybox + agents from
      `SimFacade.Agents` (value views of `RenderSnapshot`);
      `docs/evidence/task-004-godot-overlay.png`.
- [x] A user input produces the existing typed move command and deterministic
      state change. `HandleClick` / `--selfcheck` both call
      `SimFacade.QueueMove -> Command.moveTo`; accepted-command-log
      `1 3 move 20 14`; agent 3 reaches (20,14) at tick 31; per-tick hashes
      change deterministically.
- [x] Tick and state hash are visible and agree with an equivalent headless
      run. Overlay shows `tick` and `state hash` (format 1); the Godot host's
      41-hash sequence (initial + ticks 1..40) is identical to
      `cwheadless fixture`; final `0x838D3AE7DBFB735D`.
- [x] Render rate and authoritative tick rate are visibly separate.
      Host-owned accumulator in `_Process` (`1/simHz`, catch-up capped at 5);
      overlay screenshot: sim 6 Hz / 6 measured vs render 59 fps, interp alpha
      0.34. Rate adjustable at runtime (`1`/`2`).
- [x] One invalid map or marker produces an actionable loading failure.
      `scenes/GreyboxInvalid.tscn` -> exit 2, three errors in one pass, each
      naming node / id / cell / reason; simulation not started.
- [x] A map and marker edit has measured workflow evidence. docs/06 section 12
      exercise on `Greybox.tscn` (reverted): 1 file, 0 code, 0 conversion
      steps, edit -> visible hash ~0.38 s headless; invalid follow-up -> exit 2
      with the offending node named. Editor hot-reload not measured (no GUI in
      the session) - see ledger deviation.
- [x] A build launches outside the editor or the exact blocker is documented.
      Runs outside the editor UI via `<godot> --path <project>` (Vulkan render,
      screenshot). Full self-contained export blocked: exact error is the
      missing `4.7.2.stable.mono` Windows export templates (see ledger).
- [x] No Godot type or package enters the simulation project.
      `CommandoWar.Sim` package tree = `FSharp.Core` only, no `ProjectReference`;
      `deps.json` libraries = `CommandoWar.Sim` + `FSharp.Core`; source search
      for engine/Godot tokens in `src/CommandoWar.Sim` + `src/CommandoWar.Headless`
      clean. Godot client is its own `.slnx`, not in `CommandoWar.slnx`.
- [x] No production framework decision is claimed by this task. ADR-0001 stays
      `proposed`, `Selected candidate: TBD`; only the Godot evidence column and
      a spike-results section were filled. `framework_decision` unchanged.

## Completion notes (2026-09-02)

Framework-neutral additions (in `CommandoWar.slnx`): new F# project
`src/CommandoWar.Headless/` (`cwheadless`: `step` / `replay` / `compare` /
`fixture`), `content/fixtures/SPIKE-FIXTURE.md` + `.cwlog`, and
`tests/CommandoWar.Sim.Tests/FixtureTests.fs` (5 facts pinning the fixture and
its 40-tick hash sequence). Full suite 49 -> 54 passing. `CommandoWar.Sim` and
its existing tests unchanged.

Godot spike (own `src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx`,
**not** in `CommandoWar.slnx`): Godot `4.7.2.stable.mono` .NET project,
`net10.0`. Thin C# facade (`SimFacade.cs`) is the only code that calls
`CommandoWar.Sim`; framework-neutral content DTO + validator (`SpikeContent.cs`);
Godot import boundary (`GreyboxScene.cs` / `SpikeMarker.cs`); host scheduling +
input + isometric render + overlay (`MainNode.cs`). Authored greybox
`scenes/Greybox.tscn` reproduces `Setup.sixAgentWorld { 32; 32 } 20260902`
exactly.

Evidence written to `decisions/ADR-0001-FRAMEWORK-SELECTION.md` (Godot column +
"TASK-004 Godot spike results" section) without deciding the ADR. Full command
log in `docs/12_PROGRESS_LEDGER.md`.

Open blocker: full self-contained packaging needs the Godot export templates
for `4.7.2.stable.mono` (absent). App demonstrated running outside the editor
via the non-editor game run.

## Required verification

- simulation and replay tests before and after host work;
- Godot project build;
- manual move-command smoke test;
- headless-versus-host final hash comparison;
- invalid-content smoke test;
- packaged-build launch;
- dependency boundary inspection.

## Evidence to capture

- exact Godot and SDK versions;
- setup, build, run, and package commands;
- screenshot of map and tick/hash overlay;
- one recorded command and matching headless/host hash;
- content edit steps and observed time;
- host-specific files and approximate glue surface;
- debugger, error, asset import, UI, and iteration observations;
- workarounds and unresolved defects;
- expected cost of extending this host to the vertical slice.

## Documentation updates

- task, backlog, progress ledger, and state as required;
- write results into ADR-0001's evidence table or an attached spike-results section without deciding the ADR;
- do not activate TASK-005 automatically.

## Rollback or removal

The entire Godot host must be removable without changing or deleting simulation, tests, command logs, or shared fixture data.
