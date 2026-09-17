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

## Interactive command demo (TASK-040)

`scenes/CommandDemo.tscn` + `src/FSharpSceneHost.cs` is the first scene where
the player, not a canned command log, drives `Simulation.step`: left-click a
friendly agent to select it, left-click a cell to issue a real `Command.
moveTo` (queued for delivery on the next tick, going through the same
Communication/Appraisal pipeline as any other order); hovering a cell with an
agent selected previews the actual route `Pathfinding.find` would take, not a
straight line; right-click deselects; `Space` toggles tactical pause — an
order can be composed and issued while paused, delivered once resumed.
Backlog B-026.

`CommandDemoScene` (`Core/CommandDemoScene.fs`) reuses TASK-039's
`RenderShared` terrain/depth-sort helpers rather than duplicating them, and
draws the selection halo, route preview, and destination marker as ordinary
`DrawItem`s (reusing the existing `Kind = 1` agent-circle rendering — no
`FSharpSceneHost.cs` render-side change). `IClientScene` gained three new
primitives-only members for this task: `OnClick`, `OnHover`,
`OnTogglePause`; `FSharpSceneHost.cs` resolves screen position to a grid cell
(`ScreenToCell`, the exact inverse of `CellToScreen`) and forwards mouse/key
input to them.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
"$GODOT" --editor --headless --quit --path .          # one-time import
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# windowed: click an agent, click a cell, watch it move; Space to pause
"$GODOT" --path . scenes/CommandDemo.tscn

# headless smoke: scripted select-agent-0 + MoveTo(3,0), prints each tick's
# hash, asserts the final hash vs the pinned golden
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck   # MATCH 0x649FA4D08E2931CA, exit 0

# committed evidence screenshot (scripted selection/preview, since --screenshot
# mode injects no real mouse input)
"$GODOT" --path . scenes/CommandDemo.tscn -- --screenshot <abs-path>.png
```

Committed screenshot: `docs/evidence/task-040-selection-and-preview.png`.

## Placeholder art (TASK-041)

`SnapshotDemo.tscn` and `CommandDemo.tscn` draw real sprite art instead of
flat diamonds/circles: Kenney's "Isometric Miniature Prototype" pack (v2.3,
CC0 — `art/LICENSE-THIRD-PARTY.md` names the exact files and licence).
Backlog B-034. Render-only: no `CommandoWar.Sim`/`CommandoWar.Headless`
change, no `--selfcheck` hash moved.

`DrawItem` gained one new primitive field, `TextureId` (ADR-0004's
primitives-only interop idiom): for a terrain item (`Kind = 0`) it selects
`art/terrain_floor.png` (passable, elevation-tinted), `art/terrain_block.png`
(impassable), or `art/terrain_crate.png` (passable-but-opaque cover) — shape,
not colour alone, now carries that distinction. `FSharpSceneHost.cs` loads
all four textures once (static fields) and keys the terrain draw on
`TextureId`; a real, full-opacity agent (`Kind = 1`, `A >= 0.99`) draws
`art/agent_human.png` cropped to its own figure and tinted per
`DrawItem.R/G/B` (`RenderShared.agentColor`, unchanged); a translucent
`Kind = 1` item (halo, route-preview dot) still draws the original plain
circle — it was never meant to look like an agent.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
"$GODOT" --editor --headless --quit --path .          # re-import after adding art/
dotnet build CommandoWar.Client.Godot.slnx -c Debug    # AND -c Release if exporting/measuring release

# both existing scenes' --selfcheck hashes are unaffected (render-only)
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0x11B06E6EDE0C52E3
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0x649FA4D08E2931CA

"$GODOT" --path . scenes/SnapshotDemo.tscn -- --screenshot <abs-path>.png
"$GODOT" --path . scenes/CommandDemo.tscn -- --screenshot <abs-path>.png
```

Committed screenshots: `docs/evidence/task-041-placeholder-art-snapshot.png`,
`docs/evidence/task-041-placeholder-art-command.png`.

## Order acknowledgement, disposition, and reason (TASK-042)

`CommandDemo.tscn`'s HUD now shows the selected agent's order status —
docs/06 section 8/11's "order acknowledgement and disposition" / "concise
explanations for refusal" requirements. Backlog B-028. `AgentSnapshot`
gained one new values-only field, `Disposition: OrderDisposition option`
(a copy of the already-canonical `AgentState.Disposition`, the
`Destination`/`Progress` precedent); `RenderShared.dispositionText` maps it
to player-facing text (e.g. `"refused: route too exposed (threat: agent
3)"`), deliberately separate from `DiagnosticRender`'s private
developer-facing text (docs/06 draws that distinction explicitly).
`HudText()` appends `order=<text>` for the selected agent only, omitted when
nothing is selected. No `FSharpSceneHost.cs`/`_Draw` change — text only.

Review round 1: at the default 20 Hz sim rate, `DemoScenario`'s short routes
complete in a handful of ticks (well under 200ms wall-clock), so the
`order=` text could flash past unreadably before reverting to `order=no
order` the instant an order was fulfilled. `CommandDemoScene` now holds a
meaningful message on screen for `orderTextHoldSeconds` (1.5s) past when it
would otherwise clear — a client-side, presentation-only fix tracked in
`Update(deltaSeconds)`; a genuinely new message (a fresh order, a
reappraisal flip, or a new selection) still overrides immediately.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# both existing scenes' --selfcheck hashes are unaffected (values-only)
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0x11B06E6EDE0C52E3
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0x649FA4D08E2931CA

# windowed: select an agent, issue an order, watch the HUD's order= field
"$GODOT" --path . scenes/CommandDemo.tscn
```

Committed screenshot: `docs/evidence/task-042-reason-and-disposition.png`
(the scripted `--screenshot` capture point is paused before delivery, so it
shows `order=no order`; the `accepted`/`refused`/`unable` states are proven
by a headless probe instead — see the task file's Outcome section).

## Developer overlay (TASK-043)

`CommandDemo.tscn`'s `F1` key toggles a developer overlay, independent of
tactical pause (`Space`) -- backlog B-029 proper, the full docs/06 section 11
developer-facing list realised over **live** input for the first time (vs.
TASK-029's read-only, corpus-scoped `AppraisalDemoScene`). The overlay is a
fourth renderer of the exact same `Diagnostics.DiagnosticFrame` every other
developer renderer (`DiagnosticRender.Ascii`/`.Svg`/`.Html`) already consumes
-- `CommandDemoScene` calls `Diagnostics.frame`/`.frameOf` after every step and
draws its `Overlay[]` directly; no `CommandoWar.Sim`/`CommandoWar.Headless`
change of any kind.

With the overlay on: every terrain cell gets a small coordinate label; a
this-tick `Reserved` (cyan) or `Obstructed` (red) cell is marked; each
`TacticalKnowledge` contact's last-known cell gets a pale-yellow "ghost"
marker, distinct from the real (always-rendered, no fog-of-war) agent marker
it may now diverge from; the selected agent's exposed-route cells (orange)
and a `[dev]` HUD line (`commitment=`/`suppression=`/`stress=`/`reason=`,
`DiagnosticRender`'s own developer vocabulary, not the player-facing `order=`
text) appear; hovering a cell with an agent selected draws a line-of-sight
ray (`Sight.trace`) from the agent to that cell, green if visible or red up
to the blocking cell (the occluder) if not. The HUD's first line always shows
the random-draw counter (`draws=`) alongside tick and hash, overlay or not.

`DrawItem` gained two new `Kind`s for this: `2` (a line segment, `Cx2`/`Cy2`,
`Radius` as width -- line-of-sight rays, fire lines) and `3` (a text label,
`Text`, `Radius` as font size -- coordinate labels). `IClientScene` gained
`OnToggleDevOverlay` (`DemoRenderScene`, which has no live input to overlay,
implements it as a no-op -- the `OnTogglePause` precedent).

Discipline and trust are not shown: trust has no backing state anywhere in
`CommandoWar.Sim`; discipline exists but has no `Diagnostics.Overlay` case,
and adding one would require a `CommandoWar.Sim`/`CommandoWar.Headless`
change and a golden regeneration -- left as a follow-up, not built here.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# both existing scenes' --selfcheck hashes are unaffected (render-only)
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0x11B06E6EDE0C52E3
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0x649FA4D08E2931CA

# windowed: select an agent, press F1, hover cells to see the LOS ray
"$GODOT" --path . scenes/CommandDemo.tscn

# committed evidence screenshot with the overlay pre-toggled on
"$GODOT" --path . scenes/CommandDemo.tscn -- --dev-overlay --screenshot <abs-path>.png
```

Committed screenshot: `docs/evidence/task-043-developer-overlay.png`.

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
scenes/CommandDemo.tscn     TASK-040: selection, input mapping, tactical pause, command preview
src/SimFacade.cs            thin C# facade over CommandoWar.Sim  (NO Godot types, TASK-004 spike)
src/SpikeContent.cs         framework-neutral content DTOs + validation  (NO Godot types, TASK-004 spike)
src/GreyboxScene.cs         reads the authored scene -> RawContent  (import boundary, TASK-004 spike)
src/SpikeMarker.cs          typed marker node (TASK-004 spike)
src/MainNode.cs             fixed-step scheduling, input, isometric render, overlay (TASK-004 disposable spike)
src/AppraisalDemoScene.cs   TASK-029's thin renderer (a scoped ADR-0004 deviation, see the file header)
src/FSharpSceneHost.cs      TASK-039/040: the generic ADR-0004 C# host over Core/ -- every future
                            production scene's `.tscn` uses this, not a new per-scene C# file
Core/                       TASK-039/040: the ADR-0004 F# client-core library (CwClientCore.*)
Core/RenderShared.fs        TASK-040: terrain-item + depth-sort helpers shared by every render scene
Core/CommandDemoScene.fs    TASK-040: live selection, input-mapped MoveTo, route preview, pause;
                            TASK-043: F1 developer overlay over live Diagnostics.DiagnosticFrame
art/                        TASK-041: Kenney CC0 placeholder terrain/agent textures + licence file
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
