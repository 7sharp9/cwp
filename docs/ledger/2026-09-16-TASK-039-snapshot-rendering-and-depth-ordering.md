## 2026-09-16 - TASK-039 - Snapshot rendering and isometric depth ordering

### Why

B-027's dependencies (B-011, TASK-006) are both `done`; G3 passed this
session, so this is the first P4 task. Of the P4 items now selectable
(B-026, B-027, B-030 proper, B-031), Dave chose B-027 via `AskUserQuestion`:
build something to see before building input on top of it, and it is the
real Godot editor-authoring work that resolves ADR-0001 review trigger 1
(unmeasured since acceptance — no real client work had progressed beyond
the disposable TASK-004 spike and the read-only TASK-029 demo).

### Central decision (confirmed with Dave via `AskUserQuestion` before drafting)

The real Bridgehead map (B-025) isn't built; the content-importer path
(B-024) is blocked on B-043. Dave chose `DemoScenario` (the terrain-demo
scenario: an elevation ridge, an impassable block, a movement-cost patch, an
opaque wall, directional cover) over a multi-agent corpus entry or the
existing `Greybox.tscn` authored map — real terrain relief for depth
ordering to prove itself against, referenced directly from the new F#
client-core (the TASK-029 precedent), no content-import pipeline needed.

### Size correction

`docs/11`'s B-027 row lists `M`. In practice this task also stood up
ADR-0004's "thin C# host over an F# client-core library" for the first
time — no prior task had. Recorded as `L` on the task file; every later P4
client task (B-026, B-028, B-029 proper) reuses the scaffold rather than
repeating the cost.

### Changes

**New `src/CommandoWar.Client.Godot/Core/CommandoWar.Client.Godot.Core.fsproj`**
(F#, net10.0, `TreatWarningsAsErrors`): references `CommandoWar.Sim.fsproj`
and `CommandoWar.Headless.fsproj` (for `DemoScenario`). No `GodotSharp`
reference — nothing marshals a Godot type this task (no player input; that
crosses the boundary starting at B-026).

**`Core/IClientScene.fs`**: `DrawItem` `[<CLIMutable>]` record (`Kind: int`
— `0` terrain, `1` agent — plus cell coordinates, colour components,
`Radius`; primitives only, ADR-0004's interop idiom). `TickHash`
`[<CLIMutable>]` record (`Tick: int64; Hash: uint64`) — an early draft used
an F# tuple here and the C# build failed with `CS0029` (F# tuples compile to
`System.Tuple<T1,T2>`, not the `(T1,T2)` value-tuple syntax C# expects;
ADR-0004 already names tuples as forbidden across the boundary, this is the
concrete failure mode). `IClientScene`:
`Ready`/`Update(deltaSeconds)`/`DrawList()`/`HudText()`/`Dispose()`.

**`Core/DemoRenderScene.fs`**: `DemoDrive.stepOnce`/`.runFullSequence` (the
`DiagnosticRender.runFrames` per-tick `Simulation.step` pattern, minus the
diagnostic/overlay machinery — production rendering reads `RenderSnapshot`,
never `DiagnosticFrame`, ADR-0002's diagnostics-are-observers rule).
`DemoRenderScene`: owns the fixed-step accumulator (20 Hz, 5-step catch-up
cap — the `MainNode.cs` disposable-spike constants); `Ready` builds
`terrainItems` once from `WorldState.Terrain` (`Terrain.elevation`/
`.passable`/`.opaque`, coloured: dark blue-grey impassable, warm
orange-brown opaque-but-passable, elevation-shaded open ground); `Update`
advances the accumulator and calls `Simulation.step` 0+ times per frame;
`DrawList` merges cached terrain items with per-tick `RenderSnapshot.Agents`
items (positions linearly interpolated between the previous and current
tick using the accumulator's fractional remainder — not the fuller
`Progress`/move-cost interpolation `docs/04` section 15 describes, see
"Considered and rejected"), sorted ascending by `(Cx + Cy) * 2 +
Kind` — terrain drawn immediately before an agent on the same cell, the
general-purpose fix for the disposable spike's actual bug ("all terrain then
all agents", no unified sort).

**New `src/FSharpSceneHost.cs`** (ADR-0004 form 1, the first real one):
`[Export] SceneType` resolved via `AppDomain.CurrentDomain.GetAssemblies()`
+ `Activator.CreateInstance`, cast to `IClientScene`. `_Process` forwards to
`Update`/redraws; `_Draw` iterates `DrawList()` (already sorted) doing the
isometric `CellToScreen` projection (`TileW=44, TileH=22`, scaled up from
the disposable spike's `22x11` for legibility at `DemoScenario`'s 12x8 grid)
and issuing `DrawColoredPolygon`/`DrawCircle` calls — the "screen<->cell
projection arithmetic" ADR-0004 explicitly allows in the C# shim.
`--selfcheck` and `--screenshot` command-line modes (the
`MainNode.cs`/`AppraisalDemoScene.cs` precedent); `--selfcheck` is
scene-specific for now (checks `SceneType == "CwClientCore.DemoRenderScene"`
before running `DemoDrive.runFullSequence()`) — a generic self-check hook is
a natural refinement once a second self-checkable scene exists, not built
speculatively here.

**New `scenes/SnapshotDemo.tscn`**: `FSharpSceneHost.cs` root,
`SceneType = "CwClientCore.DemoRenderScene"`. Not `run/main_scene`
(`AppraisalDemo.tscn` unchanged, launched by path instead, the
`Greybox.tscn`/`GreyboxInvalid.tscn` precedent).

**`CommandoWar.Client.Godot.csproj`/`.slnx`**: new `ProjectReference` /
`<Project Path>` entries for `Core/CommandoWar.Client.Godot.Core.fsproj`.

**`README.md`**: new "Live snapshot-rendering demo (TASK-039)" section with
the run commands; file-layout table extended; the "only `SimFacade.cs`
calls `CommandoWar.Sim`" line corrected (`Core/` now also does, expected
under ADR-0002).

**`decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md`**: dated note recording the
scaffold now exists for real; explicit statement that its own review
trigger 1 (an F# breakpoint hit from an interactive editor/F5-launched run)
is **not** discharged by this task (everything here ran headless/CLI or one
windowed non-interactive screenshot capture).

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0/0` (unaffected — the new project isn't referenced by it).
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `297/297` (unaffected — no `CommandoWar.Sim`/`CommandoWar.Headless`
    change).
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: `0/0`.
- Command: `"$GODOT" --editor --headless --quit --path .` (one-time import)
  - Result: clean, no errors.
- Command: `"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck`
  - Result: prints `tick=1..20 hash=0x...`, exactly matching a `dotnet test`
    scratch probe's independently computed sequence outside Godot (a
    cross-runtime determinism cross-check); `MATCH expected final hash
    0x11B06E6EDE0C52E3 at tick 20`; exit code `0`.
- Command: `"$GODOT" --path . scenes/SnapshotDemo.tscn --resolution 900x520 -- --screenshot <path>`
  - Result: `docs/evidence/task-039-snapshot-rendering.png` committed —
    shows the elevation ridge, impassable block, and opaque wall correctly
    tinted and outlined, two friendly + one hostile agent mid-route (tick
    17/20).
- Manual check: `git status --porcelain` (excluding the gitignored `.godot/`
  cache and `bin/`/`obj/`)
  - Result: matches this task's allowed scope, plus the pre-existing
    unrelated `project.godot` line-ending-only diff (empty under `git
    diff`), left untouched.

### Evidence

- `docs/evidence/task-039-snapshot-rendering.png`.
- Selfcheck hash sequence (tick 1-20), pinned in `FSharpSceneHost.cs` and
  cross-checked against an independent `dotnet test` computation.

### Deviations and unresolved issues

- An early draft exposed `TickHash` as an F# tuple; corrected to a
  `[<CLIMutable>]` record after the C# build failed (`CS0029`) — see
  "Changes" above.
- ADR-0004's own review trigger 1 (interactive F# debugging: breakpoint hit,
  hot-reload) is not discharged by this task — needs Dave's own interactive
  editor session, not something a headless/CLI implementation session can
  produce.
- No true `Progress`/move-cost sub-cell interpolation (`docs/04` section
  15's fuller guidance) — `RenderSnapshot.AgentSnapshot` has no next-cell
  field, only `Destination`; adding one is a small `CommandoWar.Sim` change,
  deliberately deferred (client-only scope this task).
- No vertical screen-height offset for elevated terrain (a visual "raised
  ridge" look) — a separate rendering-polish item from depth-order
  correctness.

### Documents updated

- `tasks/TASK-039-SNAPSHOT-RENDERING-AND-DEPTH-ORDERING.md` (drafted ->
  review, Outcome section).
- `src/CommandoWar.Client.Godot/README.md`.
- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md`.
- `docs/11_BACKLOG.md` B-027 row.
- `docs/12_PROGRESS_LEDGER.md` (this row).
- `PROJECT_STATE.yaml`.

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: —
