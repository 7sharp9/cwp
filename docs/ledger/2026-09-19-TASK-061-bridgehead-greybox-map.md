## 2026-09-19 - TASK-061 - Bridgehead greybox map (terrain + deployments) and a terrain-layer importer

**Owner:** implementing agent (self-verified; Dave's acceptance pending)
**Source revision:** working tree on `main`, on top of `07bb07f` (TASK-060 accepted)
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot `4.7.2-stable_mono_win64`
(`Godot_v4.7.2-stable_mono_win64_console.exe`, found under
`C:\Users\Dave\Documents\GitHub\Godot_v4.7.2-stable_mono_win64\`)
**Status change:** `tasks/TASK-061-BRIDGEHEAD-GREYBOX-MAP.md` `proposed -> review`;
`docs/11_BACKLOG.md` B-025 row `proposed -> review`

### Changes

- `content/scenarios/bridgehead.cwscenario` (new): an 18x12 greybox for the
  docs/07 section 3 Bridgehead mission. A two-cell-wide impassable river
  runs the full map height at `x=8..9`, crossed by one two-cell rail-bridge
  deck (`x=8..9, y=5..6`, elevation 1) -- the single exposed approach. Two
  depot building clusters (impassable blocks) sit on the east bank at
  `x=14..16, y=2..3` and `x=14..16, y=8..9`, with seven scattered crates
  (passable, opaque, moveCost 3) for cover, plus seven `RawCoverFeature`
  directional-cover entries around the depot and the west-bank approach.
  Six friendly deployments (agent 0 leader + five; `fireteam-alpha` =
  agents 0/1/2, `fireteam-bravo` = agents 3/4/5 via TASK-059 `Formations`)
  stage on the west bank; five enemy deployments (agent 100, the
  machine-gun team, positioned to control the open lane immediately east
  of the bridge exit; agents 101-104, four riflemen in prepared positions
  around the depot buildings with west-facing cover). One shared `trooper`
  `UnitType` (`MoveSpeed = 2 = Agent.MoveSpeedDefault`, the "reuse the
  default unless there's a real reason to diverge" instruction).
  `ObjectiveAreas` = `observation` (west-bank overwatch point);
  `StaticTargets` = `bridge-charge` (the demolition target, on the bridge
  deck) and `mg-position` (marks the MG emplacement); `ExtractionAreas` =
  `extraction` (west-bank rally point). `Headquarters = None`,
  `Jammers = [||]`, `ResupplyAreas = [||]`, as specified.
- `src/CommandoWar.Client.Godot/Core/TerrainAuthoring.fs`: added
  `ImportedTerrainLayer` (a `[<CLIMutable>]` flat record -- the
  `AppraisalDemo.FrameView` "no F# tuple crosses to C#" precedent) and
  `importTerrainLayer (path: string)`, the reverse of `exportScenario`.
  Parses with `ScenarioFile.parse`, validates with `Scenario.validate`
  (throws on either failure -- the `DemoScenario` "fails hard, this is a
  bug" precedent, an editor tool run against bad content is exactly that),
  then returns the *authored, sparse* `RawTerrainLayer.Cells` as parallel
  arrays -- not `Scenario.validate`'s dense `Terrain` grid, so only cells
  actually typed in the file get painted (matching `ExportTerrainScript`'s
  `GetUsedCells()` sparse read-back on the way out) and `Class` stays the
  raw string token `TerrainTileSet.Sources` is keyed on.
- `src/CommandoWar.Client.Godot/tools/TerrainTileSet.cs`: added
  `Resolve(string cls, int moveCost, bool opaque, int elevation)`, a
  lookup over the existing, unchanged `Sources` table returning
  `(sourceId, alternateId)` or `null` for no match / elevation outside
  `[0, 2]`. `Sources`/`Build()` themselves are untouched (forbidden scope).
- `src/CommandoWar.Client.Godot/tools/ImportTerrainScript.cs` (new):
  `[Tool] EditorScript`, the reverse of `ExportTerrainScript.cs`. Reads
  `content/scenarios/bridgehead.cwscenario` via `importTerrainLayer`,
  resolves each cell via `TerrainTileSet.Resolve`, and calls
  `TileMapLayer.SetCell` for each; a cell that fails to resolve is reported
  via `GD.PrintErr` and counted, never silently dropped or mis-painted.
  Not run through the real interactive editor in this session (no display
  in the implementing environment -- see "Deviations" below).
- `src/CommandoWar.Client.Godot/tools/BridgeheadTerrainImportProof.cs`
  (new): the `TerrainRoundTripProof.cs` precedent, reversed. A headless
  `SceneTree` script: builds/saves the terrain `TileSet`, imports
  `bridgehead.cwscenario` via `importTerrainLayer`, paints a fresh
  `TileMapLayer` through the identical `TerrainTileSet.Resolve` +
  `SetCell` path `ImportTerrainScript` uses, reads the painted layer back
  via `GetUsedCells()`/`GetCellTileData()` (the `ExportTerrainScript`
  read-back), and diffs the re-exported cell set against the original
  authored cell set for an exact, order-independent match.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (before and after
    all source changes).
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (includes the new
    `ImportTerrainScript.cs`, `BridgeheadTerrainImportProof.cs`,
    `TerrainTileSet.Resolve`, `TerrainAuthoring.importTerrainLayer`).
- Command: `dotnet build src/CommandoWar.Client.Mibo/CommandoWar.Client.Mibo.slnx -c Debug`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (disposable spike,
    untouched; checked as the third `.slnx` for completeness).
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed: 395, Failed: 0, Skipped: 0` both before and after --
    unchanged, as expected (no `CommandoWar.Sim`/`.Headless` source
    touched).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- import content/scenarios/bridgehead.cwscenario`
  - Result: exit code `0`. `ok: content/scenarios/bridgehead.cwscenario`;
    `friendly agents : 6`; `enemy agents : 5`; `objective areas : 1`;
    `extraction areas : 1`; `static targets : 2`; `objectives : 1`;
    `headquarters : none`; `jammers : 0`. No reported faults.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
  - Result: `OK - all 17 entries match their committed tables`, both before
    and after -- unaffected.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only.
- Manual check: source scan of `src/CommandoWar.Sim` for `Godot|MonoGame|raylib|Mibo|System.Random|DateTime.Now|Stopwatch`
  - Result: three doc-comment mentions only (`Diagnostics.fs`, `Scenario.fs`,
    `Pathfinding.fs`, each explaining what the module does *not* reference);
    `CommandoWar.Sim` itself was not touched by this task at all.
- Command: `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path src/CommandoWar.Client.Godot --script res://tools/BridgeheadTerrainImportProof.cs`
  - Result: real run through the installed Godot 4.7.2 editor (not
    simulated). Output: `43 authored cell(s) read from the .cwscenario
    terrain layer`; `painted 43 cell(s), 0 unresolved`; `original cell
    count: 43, re-exported cell count: 43`; `TERRAIN_ROUND_TRIP_MATCH: true
    (exact, order-independent match)`; `BRIDGEHEAD_TERRAIN_IMPORT_PROOF_DONE`.
    (Trailing `RID`/`ObjectDB`/"resources still in use" lines are Godot's
    own headless `SceneTree` shutdown noise -- confirmed identical when
    re-running the pre-existing, already-accepted `TerrainRoundTripProof.cs`
    for comparison, not something this task introduced.)
- Manual check: `git status` after the Godot run
  - Result: `src/CommandoWar.Client.Godot/art/terrain.tres` came back
    modified (both proof scripts call `TerrainTileSet.BuildAndSave` before
    painting, the `TerrainRoundTripProof.cs` precedent). Diffed it: the
    regenerated file differs from the committed one only in
    Godot-resource-serialisation details (a missing `uid=` header line, a
    `texture_origin` line per alternate the committed file lacks) that
    predate this task -- `TerrainTileSet.Build()`/`Sources` are byte-for-
    byte unchanged by this task (confirmed by `git diff`; only the new
    `Resolve` method was appended). This is pre-existing drift between the
    committed generated resource and its current generator, unrelated to
    TASK-061's scope; reverted with `git checkout --
    src/CommandoWar.Client.Godot/art/terrain.tres` rather than committing
    an out-of-scope regeneration. Flagged for Dave under "Deviations".
    `content/scenarios/godot-painted-test.cwscenario` (regenerated by the
    pre-existing `TerrainRoundTripProof.cs`, run for comparison) came back
    byte-identical -- no diff, confirming that generator is still
    deterministic.
- Manual check: an ad hoc, not-committed ASCII render of the new scenario
  (`ScenarioFile.parse` -> `Scenario.validate` -> `World.ofScenario` ->
  `DiagnosticRender.Ascii`, run from a throwaway console project in the
  scratchpad directory referencing `CommandoWar.Sim`/`CommandoWar.Headless`
  by `ProjectReference`, not part of the repo) -- a direct visual sanity
  check independent of Godot, since `cwheadless render` has no
  `.cwscenario` target (see "Deviations").
  - Result: the river, bridge deck (elevation `1`), both depot buildings,
    all seven crates, and all 11 agents rendered exactly where authored;
    cover-edge markers matched the seven authored `RawCoverFeature`
    entries; move-cost annotations matched the seven crate cells.

### Evidence

- `content/scenarios/bridgehead.cwscenario` itself.
- `cwheadless import` output (above) -- exit 0, no faults, force
  composition matches docs/07 section 3 (6 friendly incl. two fireteams of
  3, 5 enemy incl. one MG-team agent + four riflemen).
- The Godot headless round-trip proof's full console output (above):
  `TERRAIN_ROUND_TRIP_MATCH: true`, 43/43 cells, 0 unresolved.
- The ad hoc ASCII render (above), reproduced in full in this task's
  implementation transcript.
- `dotnet build`/`dotnet test`/`-- corpus`/`dotnet list --include-transitive`
  outputs (above).

### Deviations and unresolved issues

- **`Objectives` is not literally empty.** The task file's own design
  decision states `Objectives = [||]` and lists "the map contains no
  `Objectives[]` entries" as a stand-alone acceptance criterion. Discovered
  during implementation: `Scenario.validate` (`src/CommandoWar.Sim/
  Scenario.fs`) unconditionally reports `MissingRequiredMarker "Objective"`
  when `raw.Objectives` is empty, regardless of content --
  `cwheadless import` would then exit non-zero with a fault, directly
  contradicting the task's own headline requirement ("valid under
  `cwheadless import`, exit 0, no faults" -- the first sentence of the
  Objective). Both a literally empty `Objectives[]` and passing
  `cwheadless import` cleanly cannot hold simultaneously under the current
  format; the task's forbidden scope also rules out changing
  `Scenario.validate` to relax this. Resolved by authoring exactly one
  placeholder `reach` objective (id 1, `IsOptional = true`, referencing the
  `observation` landmark) -- not the docs/07 mission sequence, still
  entirely B-032's job, and nothing in `Simulation.fs` reads `Objectives`
  regardless of count. This follows the exact precedent already in the
  codebase: `CwClientCore.TerrainAuthoring.exportScenario`'s own doc
  comment describes its skeleton's single "reach" objective as existing
  "just sufficient for `Scenario.validate` to accept the result", and
  `content/scenarios/godot-painted-test.cwscenario` does the same.
  Prioritised the task's own headline import/exit-0 requirement over the
  literal empty-array instruction; the "no `Objectives[]` entries"
  acceptance criterion is marked unmet below, not silently checked off.
- **No `cwheadless render` target for an arbitrary `.cwscenario` file.**
  The task's "Required work"/"Required verification" both assume
  `cwheadless render` can render a `.cwscenario` file directly ("render
  bridgehead", "for a direct visual sanity check independent of Godot").
  Confirmed by inspecting `Program.fs`: `render` only accepts
  `fixture | demo | los | path | <command-log path>` targets; any other
  string is treated as a `.cwlog` command log against the shared
  `Fixture` world, not a scenario file, and TASK-060 only added `import`
  (parse + validate + summary print), never a scenario-to-diagnostic-frame
  renderer. Adding one is a `CommandoWar.Headless` CLI change, outside this
  task's "Allowed scope" list. Resolved with the ad hoc, not-committed
  ASCII-render harness described above, which exercises the identical
  `ScenarioFile`/`Scenario`/`World`/`DiagnosticRender` API the CLI itself
  uses -- a genuine visual sanity check independent of Godot, just not
  through a permanent `cwheadless` verb. Flagging the CLI gap itself as a
  possible small follow-up (not filed as a new backlog row -- out of this
  task's scope to add one unprompted).
- **`art/terrain.tres` regeneration reverted, not committed.** See the
  "Manual check" above -- a pre-existing drift between the committed
  generated `TileSet` resource and `TerrainTileSet.Build()`'s current
  output, unrelated to this task (which only appended a new `Resolve`
  method, no change to `Build()`/`Sources`). Left reverted in the working
  tree; regenerating it for real is a one-line `TerrainTileSet.BuildAndSave`
  call whenever someone wants to, but doing so here would have mixed an
  unrelated, unreviewed resource-format change into this task's diff.
- **Interactive-editor run of `ImportTerrainScript.cs` not performed.**
  Explicitly flagged, not resolved, per the task file's own step 6 -- no
  display in this implementing environment, the identical gap TASK-060
  flagged for `ExportTerrainScript.cs`. The headless round-trip proof
  exercises the same `importTerrainLayer` + `TerrainTileSet.Resolve` +
  `TileMapLayer.SetCell` code path for real, but does not itself prove the
  interactive painting/inspection experience is good -- that is Dave's own
  call to make live in the editor.
- Map geometry (the 18x12 layout, exact cell positions) is this task's own
  first-pass design output, per "Inputs and assumptions" -- explicitly
  provisional, meant to be refined live by Dave via the new importer, not
  final here.

### Documents updated

- `content/scenarios/bridgehead.cwscenario` (new)
- `src/CommandoWar.Client.Godot/Core/TerrainAuthoring.fs`
- `src/CommandoWar.Client.Godot/tools/TerrainTileSet.cs`
- `src/CommandoWar.Client.Godot/tools/ImportTerrainScript.cs` (new)
- `src/CommandoWar.Client.Godot/tools/BridgeheadTerrainImportProof.cs` (new)
- `tasks/TASK-061-BRIDGEHEAD-GREYBOX-MAP.md` (status, acceptance criteria,
  verification, evidence, deviations)
- `docs/11_BACKLOG.md` (B-025 row: `proposed -> review`, implementation
  summary appended)
- `docs/12_PROGRESS_LEDGER.md` (this index row)
- `PROJECT_STATE.yaml` (`active_work.note`, new dated entry prepended)

### Review round 1 (2026-09-19, live)

Dave opened the real interactive Godot 4.7.2 editor and ran
`ImportTerrainScript.cs` himself, closing the one gap this task's own
implementation had flagged as outstanding (no display in the implementing
environment).

**Real bug found and fixed:** the first run painted Bridgehead's 43 cells
correctly, but the committed `TerrainAuthoring.tscn` already had a large
leftover hand-painted test area baked into it from TASK-060's own live
review rounds (visible once Dave screenshotted the result -- a large
diamond floor/border shape mixed in with the new content). `SetCell` only
adds/overwrites individual cells; it never clears what was already there,
so "import" was silently overlaying onto stale content instead of
replacing it. Fixed with one `layerNode.Clear()` call at the top of
`ImportTerrainScript._Run()`, added and explained inline
(`ImportTerrainScript.cs`) -- an import now always replaces the layer's
content. Dave manually cleared the layer once himself before the fix was
rebuilt and picked up; after that, re-running never needs a manual clear.

**A second, harder-to-pin-down symptom:** two further screenshots, even
after zooming out enough that Dave reported "nothing beyond" the painted
area, showed only two small depot-sized blobs and a handful of crates --
nowhere near the full 43-cell layout, and critically missing the 24-cell
river band that should be the single largest, most visually dominant
shape on the map. Rather than keep guessing from screenshots, this was
run to ground directly: a temporary headless diagnostic
(`BridgeheadDebugProof.cs`, loading the *actual* committed
`res://art/terrain.tres` rather than a freshly rebuilt one) confirmed the
resource and the import/resolve logic were both correct in isolation; a
second temporary diagnostic (`BridgeheadSceneCheck.cs`) then loaded the
*actual saved* `res://scenes/TerrainAuthoring.tscn` from disk -- the exact
file Dave's own editor had open -- via `PackedScene.Instantiate()` and read
its `TileMapLayer` back through Godot's own `GetUsedCells()`/
`GetCellTileData()` API. Result: all 43 cells present, at exactly the
authored coordinates, with exactly the authored `Class`/`Elevation`/
`MoveCost`/`Opaque` values -- a perfect match against
`bridgehead.cwscenario`. The data was correct throughout; the screenshots
most likely caught a transient or not-yet-refreshed viewport state rather
than reflecting a real rendering or logic defect. Not independently
re-confirmed with one more live screenshot after this finding (Dave moved
straight to closing out the session), but the direct API-level read of his
own saved file is stronger evidence than a screenshot would have added.
Both temporary diagnostic scripts were removed after use (the established
project convention for this kind of probe).

**One unrelated side effect caught by `git diff` and reverted, not
committed** (the TASK-060 precedent): opening/running scripts in the
editor reformatted `ExportTerrainScript.cs` from spaces to tabs with no
functional change, and touched `project.godot`/`art/terrain.tres` with
line-ending-only noise (empty `git diff`, `git status` still flagged them
dirty). All three reverted with `git checkout --` to keep this task's diff
to what it actually changed.

**Scene state decided with Dave (`AskUserQuestion`):** after the fix was
confirmed, `TerrainAuthoring.tscn` was left with Dave's live-tested,
correct Bridgehead layout saved into it. Since that scene is documented as
a shared, reusable "empty starter" scratch canvas (not a per-map
artifact -- `bridgehead.cwscenario` is the authoritative file regardless),
Dave chose to reset it back to empty for reuse rather than commit the
painted state; done with `git checkout --` after his explicit
confirmation.

Dave's verdict: "this looks ok" -- accepted, no further changes requested.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-19)
- Notes: both deviations flagged at self-verification time (the non-empty
  `Objectives` array; no `cwheadless render <scenario-file>` capability)
  were implicitly accepted -- Dave's review focused entirely on the live
  terrain-import experience (see "Review round 1" above) and raised no
  objection to either. One real defect (`ImportTerrainScript` not clearing
  before painting) was found live and fixed same-session, the TASK-060
  precedent for this kind of tool.
