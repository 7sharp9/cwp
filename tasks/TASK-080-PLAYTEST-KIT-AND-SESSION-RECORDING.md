# TASK-080: Playtest kit and per-session recording

Status: done (accepted by Dave 2026-09-30; the export checklist, `docs/14` section 2, is still Dave's to run and its acceptance criterion below stays unticked)
Owner: coding agent (export checklist to be run by Dave)
Phase: P5 (preparation)
Gate: G5 (playtest prerequisites); realises B-036
Size: M (B-036 was `S`; widened, see "Why this task exists")

## Objective

Make a distributable playtest build possible and make the sessions it produces
usable as G5 evidence: a build procedure, a neutral facilitator script, a
participant controls card, an observation sheet, an issue form, per-session
command recording in the play scene, and a headless way to read why an agent
refused from a recorded session.

## Why this task exists

Dave asked, on accepting TASK-079, to "prepare the playtest build, update
docs". B-036 as written ("clean build, neutral playtest script, and issue form")
assumed the game could already be exported and could already produce the
evidence G5 needs. Reading the code showed neither holds:

- `FSharpSceneHost.ResolveContentPath` resolves the mission as
  `res://` + `../../content/...`, which only exists inside a repository
  checkout; an exported build cannot find `bridgehead.cwscenario`.
- `CommandDemoScene` recorded nothing ("Never persisted; this scene keeps no
  replay log"), so a session leaves no event log, outcome, or restart count, and
  G5's "identify why an agent resisted from recorded evidence" and P5's
  "collect event logs, outcome data" were unmeetable.
- `cwheadless replay-file` printed commands and hashes but no dispositions or
  refusal reasons, so a recorded session could not answer "why".
- No Godot install or export templates exist in this environment, so no binary
  can be produced or the Godot-side changes run here.

The G4 gate itself is still `pending` and remains Dave's call; this task does
not record G4 as passed.

## Required reading

- `docs/07_VERTICAL_SLICE.md` sections 9 and 11; `docs/08_ROADMAP_AND_GATES.md`
  sections 7 and 8
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`,
  `src/FSharpSceneHost.cs` (`ResolveContentPath`), the client README "Packaging"
- `src/CommandoWar.Sim/ReplaySerialisation.fs` (file format);
  `src/CommandoWar.Headless/Program.fs` (`replay-file`)
- `AGENTS.md` (scope, determinism, diagnostics)

## Dependencies

- TASK-079 (done); B-035 (done).

## Inputs and assumptions

- Recording lives in the client (`CommandDemoScene`), is non-authoritative, and
  reads nothing back into the sim. Commands are captured where
  `commandsForTick` hands them to `Simulation.step`, so the file holds the
  accepted stream, not superseded clicks.
- File location is `<ApplicationData>/CommandoWar/playtest/` written with
  `System.IO` from the F# Core (the `TerrainAuthoring` precedent), not Godot's
  `user://`, so it needs no host change and can be exercised without Godot.
- The file is rewritten in full (a few KB) on delivery, every 20 ticks, and on
  every pause and mission end, so a closed run loses at most one second.
- Wall-clock time appears only in the file name (client side, never read by
  the sim). `Canonical.FormatVersion` stays 15; the replay-command format and
  `CommandoWar.Sim` are unchanged.
- The `bridgehead` scenario label written into a session resolves in
  `cwheadless` to the initial state of the `bridgehead-*` corpus entries
  (same file, same seed `20260920`).
- Disk-write failure is narrow-caught (`IOException`,
  `UnauthorizedAccessException` only) and shown on the HUD; a hidden failure
  would silently lose the evidence, a fatal one would end a participant's run.

## Allowed scope

- `CommandDemoScene.fs`: recording, HUD failure text, secondary constructor.
- `FSharpSceneHost.cs`: `ResolveContentPath` only.
- `CommandoWar.Headless/Program.fs`: the `bridgehead` label and an `order
  appraisals` section in `replay-file`.
- `docs/14_PLAYTEST_KIT.md` (new: nothing existing holds a facilitator script,
  forms, or a build procedure), and short additions to `docs/09` and the client
  README.

## Forbidden scope

- Any `CommandoWar.Sim` change; the replay format; the canonical format.
- A committed `export_presets.cfg`, a CI export job, or any new package or tool.
- Changing the HUD's developer line, the window title, or the `F1` overlay
  (recorded as findings for Dave instead).
- Running the playtest itself (B-037) or recording the G4 gate.

## Required work

1. Record delivered commands and write one session file per launch.
2. Make the content lookup work from an exported build.
3. Let `replay-file` resolve the session's scenario label and print
   dispositions and refusal reasons.
4. Write the kit; verify what can be verified here; record what cannot.

## Acceptance criteria

- [x] A session recorded by the real scene (scripted through its click path)
      replays headlessly to the same tick and final hash the scene held:
      `0x300BB18492CEB355` at tick 90.
- [x] The file follows a pause (tick 81 -> 90) and is current after a refusal
      or mission end by the same flush; a failed write is visible on the HUD.
- [x] `cwheadless replay-file` prints each appraisal's disposition and typed
      refusal reason and the mission outcome; the Bridgehead corpus replay shows
      the tick-6 ford refusal (`RouteTooExposed (Some AgentId 102)`).
- [x] The scripted self-check writes nothing and its pins are unchanged
      (`DemoRenderScene 0x6213D672BC36FDB8`, `CommandDemoScene
      0x300BB18492CEB355`).
- [x] Kit written: script, controls card, observation sheet, issue form, build
      procedure, G5 mapping, findings.
- [x] `dotnet test` and `cwheadless corpus` pass on CI (pinned SDK):
      windows-latest, SDK 10.0.303, run `36580095236` on `8e4f8c6`: build 0 warnings / 0 errors, `Passed: 429, Failed: 0` (unchanged: no test added), `corpus` 22/22, working tree clean after the verbs.
- [ ] **Not verified here:** the Godot export builds, launches from a folder
      outside the repository, finds `content/` beside the executable, and
      writes a session file (checklist in `docs/14` section 2). Needs the
      editor and the `4.7.2.stable.mono` export templates.
- [x] No forbidden scope entered the change.
- [x] Documentation updated.

## Required verification

- Build `CommandoWar.Client.Godot.Core` and `CommandoWar.Headless`; corpus;
  stand-in suite.
- Script the real scene with recording on, replay its file with `replay-file`.
- Failure path: an unwritable session directory shows on the HUD.
- `git diff --stat src/CommandoWar.Sim` empty.

## Evidence

### Change

- `CommandDemoScene(sessionDirectory: string option)`; a no-argument
  constructor supplies the per-user folder (the host builds the scene with
  `Activator.CreateInstance`, no arguments). `CommandDemoDrive` passes `None`.
- `commandsForTick` appends each delivered command to `recorded` with a
  per-tick sequence index; `sessionText`/`writeSession`/`flushSession` write it
  as a `ReplayCommandFile` (seed 20260920, scenario `bridgehead`, initial-state
  hash, no checkpoints) preceded by a `#` outcome line. Flush points: after a
  step that delivered a command, every 20 ticks, refusal auto-pause, mission
  end, and `OnTogglePause` into pause.
- `FSharpSceneHost.ResolveContentPath`: `<exe dir>/content/<path>` if the file
  exists, else the previous repository-relative path.
- `cwheadless replay-file`: `bridgehead` label; new `order appraisals` section
  and `mission outcome` line after the existing output (existing output is
  unchanged and nothing pins it).

### Verification

- Environment: as TASK-077/078/079 (Ubuntu SDK 10.0.112, NuGet blocked; build
  from a copy without `global.json`, `-p:NuGetAudit=false`).
- Builds: Core and Headless `0 Warning(s)`, `0 Error(s)`.
- Round trip: real scene, six scripted orders, 90 ticks, recording to a temp
  directory: file exists after `Ready`; `ticks 81` before pausing, `ticks 90`
  after `OnTogglePause`; `cwheadless replay-file` on that file gives tick count
  90, final tick 90, final hash `0x300BB18492CEB355` (equal to the scene's),
  initial hash `0x02EA1348C7764846` accepted. A first version wrote `ticks 1`
  (only on new commands); it would have cut a replay off before any refusal, so
  periodic and pause flushes were added and this run repeated.
- Failure path: a session directory whose parent is a file: HUD contains
  `SESSION RECORDING FAILED: Could not find a part of the path ...`.
- Self-check pins via `CommandoWar.Client.Godot.Core`: unchanged, no file
  written by the scripted drive.
- Appraisal trace on `content/replays/bridgehead-succeeded.cwreplay`: `tick 6
  agent 5 command 7 Refused (RouteTooExposed (Some AgentId 102), [||])`.
- `cwheadless corpus`: 22/22. Stand-in suite: 374/374 (unchanged; no test
  added).
- `git diff --stat src/CommandoWar.Sim`: empty.
- CI on the pinned SDK: windows-latest, SDK 10.0.303, run `36580095236` on `8e4f8c6`: build 0 warnings / 0 errors, `Passed: 429, Failed: 0` (unchanged: no test added), `corpus` 22/22, working tree clean after the verbs. CI
  builds only Sim, Headless, Tests and Benchmarks: it does not compile the
  Godot Core or the C# host.

### Not verified, and why

- The C# `ResolveContentPath` change and the export: no Godot SDK here.
- The recording code has no committed test: the test project references only
  Sim and Headless, and referencing the client Core would be a dependency
  change outside this task. Its evidence is the scripted run above.
- Session recording on macOS.

## Documentation updates

- this file; `docs/11_BACKLOG.md` B-036; `docs/14_PLAYTEST_KIT.md` (new);
  `docs/09_TEST_STRATEGY.md` (`replay-file` sentence);
  `src/CommandoWar.Client.Godot/README.md`; `docs/12_PROGRESS_LEDGER.md` index
  row and detail file; `PROJECT_STATE.yaml`.

## Rollback or removal

Revert the recording block and constructor in `CommandDemoScene.fs`, the
`ResolveContentPath` change, and the `Program.fs` additions; delete
`docs/14_PLAYTEST_KIT.md`.
