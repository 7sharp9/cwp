# CommandoWar Godot .NET framework spike (TASK-004)

**Disposable.** This host exists only to produce ADR-0001 evidence. It is not
production code and must be removable without touching `CommandoWar.Sim`,
`CommandoWar.Sim.Tests`, `CommandoWar.Headless`, `content/`, or the shared
fixture. It is deliberately **not** in `CommandoWar.slnx`.

## Appraisal-divergence demo (TASK-029)

`scenes/AppraisalDemo.tscn` + `src/AppraisalDemoScene.cs` is the current
`run/main_scene` (the TASK-004 greybox spike below still builds and runs from
`Main.tscn`). It is a **read-only, corpus-scoped** first realisation of the
Godot `DiagnosticFrame` renderer (backlog B-029, pulled forward as P3
decision-support): it loads the committed `content/replays/exposed-approach`
corpus entry, builds its per-tick `DiagnosticFrame` sequence via
`DiagnosticRender.runFrames`, and renders the state plus the `KnownContact` /
`PlannedPath` / `OrderAppraisal` overlays with a 13-frame tick slider and a
tick / hash / format / draws HUD, mirroring the `DiagnosticRender.Svg` colours
and glyphs. The divergence lands on tick 1: agent 0 (`Discipline 1`) `Refused
RouteTooExposed threat-agent-2`, agent 1 (`Discipline 6`) `Accepted`.

All non-trivial logic is in the framework-neutral F# helper
`src/CommandoWar.Headless/AppraisalDemo.fs`; this C# scene is a thin renderer (a
scoped deviation from ADR-0004's "F# client-core / no logic in C#" rule for this
disposable demo — see the TASK-029 task file and ledger). No `CommandoWar.Sim`
change; adds one `ProjectReference` to `CommandoWar.Headless`.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
"$GODOT" --editor --headless --quit --path .          # one-time import
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# windowed: drag the slider or press Left / Right to scrub ticks 0..12
"$GODOT" --path .

# headless smoke: prints the tick-1 dispositions + hash, asserts vs the golden
"$GODOT" --headless --path . -- --selfcheck            # MATCH 0x5D5A30C0DF64AC93, exit 0

# committed evidence screenshot (windowed; headless has no viewport texture)
"$GODOT" --path . --resolution 1000x620 -- --screenshot <abs-path>.png
```

Committed screenshot: `docs/evidence/task-029-appraisal-demo.png`.

## Live snapshot-rendering demo (TASK-039)

`scenes/SnapshotDemo.tscn` + `src/FSharpSceneHost.cs` is the first
production Godot scene: it live-steps the terrain-demo scenario
(`src/CommandoWar.Headless/DemoScenario.fs` — an elevation ridge, an
impassable block, a movement-cost patch, an opaque wall, and directional
cover) via `Simulation.step` and renders it isometrically with one unified,
depth-sorted draw list (terrain interleaved with agents by screen depth, not
"all terrain then all agents" — the disposable `MainNode.cs` spike's actual
gap). Backlog B-027. No player input; the scene runs unattended once
launched.

`FSharpSceneHost.cs` is the first real use of ADR-0004's accepted
architecture: a generic C# host (`[Export] SceneType`) resolves an F#
`CwClientCore.IClientScene` implementation by name and forwards
`_Ready`/`_Process`/`_Draw`/`_ExitTree`; every non-trivial concern (the
fixed-step scheduler, stepping the simulation, building the depth-sorted
`DrawItem[]`) lives in the new `Core/CommandoWar.Client.Godot.Core.fsproj`
project (`CwClientCore.DemoRenderScene`). The C# host only does cell<->screen
projection arithmetic and issues `Draw*` calls — the one thing ADR-0004
explicitly allows there.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
"$GODOT" --editor --headless --quit --path .          # one-time import
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# windowed: agents cross the ridge/wall/movement-cost patch over ~1 second
"$GODOT" --path . scenes/SnapshotDemo.tscn

# headless smoke: replays DemoScenario tick 1..20, prints each tick's hash,
# asserts the final hash vs the pinned golden
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0x11B06E6EDE0C52E3, exit 0

# committed evidence screenshot (windowed; headless has no viewport texture)
"$GODOT" --path . scenes/SnapshotDemo.tscn -- --screenshot <abs-path>.png
```

Committed screenshot: `docs/evidence/task-039-snapshot-rendering.png`.

## Pinned versions

| Component | Version |
|---|---|
| Godot | `4.7.2.stable.mono.official` (.NET/Mono build) |
| .NET SDK | `10.0.303` (repo `global.json`) |
| Target framework | `net10.0` (must match `CommandoWar.Sim`; Godot's template default is `net8.0`) |
| `Godot.NET.Sdk` / `GodotSharp` | `4.7.2` (offline feed in `nuget.config`) |

Editor path (not on `PATH` in the dev environment used):
`C:\Users\Dave\Documents\GitHub\Godot_v4.7.2-stable_mono_win64\`

## Layout

```
project.godot              Godot project (run/main_scene = AppraisalDemo.tscn)
Main.tscn                   root: MainNode + the two content scenes (TASK-004 spike)
scenes/Greybox.tscn         authored 32x32 isometric greybox + 6 spawn markers + objective
scenes/GreyboxInvalid.tscn  deliberately broken content for the failure path
scenes/AppraisalDemo.tscn   TASK-029: read-only corpus-scoped appraisal-divergence demo
scenes/SnapshotDemo.tscn    TASK-039: live snapshot rendering + isometric depth ordering
src/SimFacade.cs            thin C# facade over CommandoWar.Sim  (NO Godot types, TASK-004 spike)
src/SpikeContent.cs         framework-neutral content DTOs + validation  (NO Godot types, TASK-004 spike)
src/GreyboxScene.cs         reads the authored scene -> RawContent  (import boundary, TASK-004 spike)
src/SpikeMarker.cs          typed marker node (TASK-004 spike)
src/MainNode.cs             fixed-step scheduling, input, isometric render, overlay (TASK-004 disposable spike)
src/AppraisalDemoScene.cs   TASK-029's thin renderer (a scoped ADR-0004 deviation, see the file header)
src/FSharpSceneHost.cs      TASK-039: the generic ADR-0004 C# host over Core/ -- every future
                            production scene's `.tscn` uses this, not a new per-scene C# file
Core/                       TASK-039: the ADR-0004 F# client-core library (CwClientCore.*)
```

In the TASK-004 spike, only `SimFacade.cs` calls `CommandoWar.Sim`, and
everything it passes across the C#/F# boundary is a primitive, an array, or
an F# value type. Since TASK-039, `Core/` (the ADR-0004 F# client-core
library) also references `CommandoWar.Sim`/`CommandoWar.Headless` directly —
expected under ADR-0002 ("the client references the simulation"); no C# file
outside `FSharpSceneHost.cs` (which only resolves `Core`'s `IClientScene` by
name) touches either.

## Build

```
dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug
```

The Godot editor also builds it on open / on run. Output lands in
`.godot/mono/temp/bin/<config>/`.

## Run outside the editor

```
# windowed game (not the editor UI)
<godot> --path src/CommandoWar.Client.Godot

# headless hash cross-check against CommandoWar.Headless
<godot> --headless --path src/CommandoWar.Client.Godot -- --selfcheck --expect 0x838D3AE7DBFB735D

# deliberately invalid content -> exit 2, actionable errors
<godot> --headless --path src/CommandoWar.Client.Godot -- --selfcheck --invalid

# self-captured evidence screenshot (slows the scheduler to 6 Hz for a mid-move still)
<godot> --path src/CommandoWar.Client.Godot -- --screenshot <abs-path>.png
```

`--selfcheck` runs the shared fixture (`content/fixtures/SPIKE-FIXTURE.md`):
32x32, seed 20260902, agent 3 -> (20,14) at tick 1, 40 ticks. It prints one
`tick=N hash=0x...` line per tick; the sequence is identical to
`dotnet run --project src/CommandoWar.Headless -- fixture`.

## In-editor controls

- left-click an agent to select, left-click a cell to issue `MoveTo`
- right-click to deselect
- `1` / `2` change the authoritative sim rate; `Space` pauses
- `S` writes `user://godot-run.cwlog` (feed it to `cwheadless compare`)

## Packaging

`--export-release` / `--export-pack` presets work, but a self-contained
executable needs the Godot **export templates** for `4.7.2.stable.mono`, which
are not installed (`%APPDATA%\Godot\export_templates\4.7.2.stable.mono\` is
empty). See the TASK-004 ledger entry for the exact error and the unblock path.
