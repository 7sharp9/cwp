## 2026-09-29 - TASK-080 - Playtest kit and per-session recording

**Owner:** Dave with coding-agent assistance
**Source revision:** TASK-079 accepted (acceptance commit precedes this task's);
branch `claude/cool-hypatia-j4rswr`
**Environment:** Linux cloud container, .NET SDK `10.0.112` (Ubuntu archive;
pinned `10.0.303` and NuGet blocked by the session's network policy); no Godot
install, no export templates; CI: windows-latest, .NET SDK `10.0.303`
**Status change:** `ready -> review` (drafted and selected on Dave's "prepare
the playtest build, update docs")

### Changes

- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: per-launch session
  recording (delivered-command capture, `.cwreplay` writer, flush on delivery,
  every 20 ticks, refusal auto-pause, pause, and mission end; HUD text on write
  failure); constructor takes an optional session directory, the
  no-argument constructor supplies `<ApplicationData>/CommandoWar/playtest/`,
  the scripted self-check passes `None`.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: `ResolveContentPath`
  prefers `content/` beside the executable, falls back to the repository path.
- `src/CommandoWar.Headless/Program.fs`: `replay-file` resolves the
  `bridgehead` label and prints an `order appraisals` section and the mission
  outcome.
- `docs/14_PLAYTEST_KIT.md` (new); `docs/09_TEST_STRATEGY.md` and
  `src/CommandoWar.Client.Godot/README.md` additions.
- No `CommandoWar.Sim` change; no format change (`Canonical.FormatVersion`
  15); no new package or project reference.

### Diagnostics (AGENTS.md)

No authoritative spatial or tactical state was added or changed, so no
`DiagnosticFrame` layer or golden is required. The recording is a client-side
observer of the accepted command stream.

### Commands and results

Local, from a copy of the tree without `global.json`, `-p:NuGetAudit=false`:

- `dotnet build src/CommandoWar.Client.Godot/Core -c Release` and
  `src/CommandoWar.Headless`: `0 Warning(s)`, `0 Error(s)`.
- Real `CommandDemoScene` scripted through `OnClick`/`OnHover` with the six
  self-check orders, `Some tempdir`, 90 ticks: file present after `Ready`;
  `ticks 81` before `OnTogglePause`, `ticks 90` after; `cwheadless replay-file`
  on it: `tick count : 90`, `final tick : 90`, `final hash : 0x300BB18492CEB355`
  (the scene's hash), initial hash `0x02EA1348C7764846` accepted.
- First version wrote `ticks 1` (flushed only on new commands). A replay would
  stop before any refusal, so the periodic and pause flushes were added and the
  run repeated.
- Failure path: session directory under a regular file: HUD contains
  `SESSION RECORDING FAILED: Could not find a part of the path ...`.
- Self-check pins: `DemoRenderScene` tick 20 `0x6213D672BC36FDB8`,
  `CommandDemoScene` tick 90 `0x300BB18492CEB355`; unchanged; scripted drive
  writes no file (`~/.config/CommandoWar` absent afterwards).
- `cwheadless replay-file content/replays/bridgehead-succeeded.cwreplay`:
  `tick 6  agent 5  command 7  Refused (RouteTooExposed (Some AgentId 102), [||])`.
- `cwheadless corpus`: `OK - all 22 entries match their committed tables`.
  Stand-in suite `TOTAL pass=374 fail=0` (unchanged; no test added).
- `git diff --stat src/CommandoWar.Sim`: empty.
- CI on the pinned SDK (windows-latest, SDK 10.0.303, run `36580095236` on `8e4f8c6`: build 0 warnings / 0 errors, `Passed: 429, Failed: 0` (unchanged: no test added), `corpus` 22/22, working tree clean after the verbs).

### Findings (not fixed; also in `docs/14` section 8)

1. The HUD status line shows the tick, the 64-bit state hash, RNG draws, and
   agent count at all times. Developer-oriented for a non-developer test.
2. The window and project title is "CommandoWar Godot Spike"; visible to
   participants; branding is out of scope.
3. The client cannot open a session file; analysis needs the .NET SDK and the
   repository. `render` accepts only legacy `.cwlog` files, so there is no
   headless map frame for a session, only the text trace.
4. No in-game restart; one session file per launch.
5. The Godot export templates for `4.7.2.stable.mono` were absent on the
   developer machine at TASK-004; the kit says to install and re-check.
6. `F1` developer overlay is reachable by a participant.
7. CI does not compile the Godot Core or the C# host, so neither the recording
   code nor the `ResolveContentPath` change is covered by CI. The recording has
   no committed test (the test project cannot reference the client Core without
   a dependency change).
8. `pending` commands get `Sequence = pending.Count` at insertion, which can
   repeat after a stale entry is removed. Harmless for delivery (order is by
   list position) and avoided for the recording by re-indexing at delivery;
   noted in case anything else starts to rely on it.

### Deviations and unresolved issues

- No distributable was produced and the Godot-side changes were not run: the
  acceptance item for that is open until Dave runs the `docs/14` section 2
  checklist with the editor and templates.
- `PROJECT_STATE.yaml` still does not parse as YAML (pre-existing).
- G4 remains `pending`; not recorded as passed.

### Documents updated

- `tasks/TASK-080-PLAYTEST-KIT-AND-SESSION-RECORDING.md`
- `docs/11_BACKLOG.md` (B-036 -> `review`, size `M`)
- `docs/14_PLAYTEST_KIT.md`; `docs/09_TEST_STRATEGY.md`;
  `src/CommandoWar.Client.Godot/README.md`
- `PROJECT_STATE.yaml` (`active_work`)
- `docs/12_PROGRESS_LEDGER.md` (index row) and this file

### Review

- Reviewer: Dave
- Accepted: not yet (status `review`)
