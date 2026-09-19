## 2026-09-19 - TASK-060 - scenario content format, importer, and Godot terrain authoring

**Owner:** implementing agent (Dave selecting/steering via conversation, not `AskUserQuestion` rounds)
**Source revision:** `main` at `ce2b676` (TASK-059 accepted)
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot `4.7.2.stable.mono.official.ed1daf0bf`
(`Godot_v4.7.2-stable_mono_win64_console.exe`, confirmed reachable in this
implementing environment — unlike earlier sessions, no Godot install was
needed here since the binary was found on disk and run directly).
**Status change:** `proposed -> review`

### Changes

Reached via a multi-round conversation, not a single `AskUserQuestion`. The
ready-backlog scan found every P4 row `done` except B-043 (Mibo 5.x
reconsideration, gated on ADR-0001 review trigger 1 — an editor
edit->visible-result measurement never actually recorded, since TASK-039
through TASK-059 were code-side feature work verified by running the
editor, not content authored in it) and its downstream chain
(B-024/025/032/033/035), blocked transitively on B-043. Dave declined to
fabricate the review-trigger-1 verdict from indirect evidence ("we didnt
really use the editor, other than launching it to just hit the play
button") and asked instead whether Godot's own editor tooling could assist
content authoring, having flagged manual block placement as wasteful.

Investigation found: (1) Godot 4.7's Terrain Sets autotiling is real but
does not fit this project's art (three discrete single-variant blocks from
a "prototype" pack, nothing to blend between); (2) the actual pain point is
`RawTerrainLayer`'s hand-typed record shape (`Scenario.fs:307-331`); (3) no
scenario file format exists at all — every scenario is F# source, the
exact gap ADR-0001 named ("either a Tiled importer or a richer text
format", `decisions/ADR-0001-FRAMEWORK-SELECTION.md:274`) and never built
— backlog B-024, blocked behind B-043, a circular dependency against
ADR-0001's own review-trigger-1 text (which expects a Bridgehead-authoring
task to generate the B-043 measurement, not wait behind it).

The task file was first drafted around JSON/`System.Text.Json`, then
corrected before any code was written: `ReplaySerialisation.fs` (read only
after the first draft) turned out to already establish this project's real
convention for exactly this kind of content — a hand-rolled, deterministic,
line-based text grammar with a typed `ParseError`-shaped union, the same
style `.cwlog`/`.cwreplay` both use — and no general-purpose serialisation
library appears anywhere in the solution. Redrafted to follow that
precedent instead.

**`.cwscenario` content format** (`src/CommandoWar.Sim/ScenarioFile.fs`,
new): a `ReplaySerialisation.fs`-style grammar — five fixed header
directives (`version`, `content-version`, `id`, `map`,
`fail-on-friendly-eliminated`), then any number of body directives in any
order (`headquarters`/`terrain` at most once each; `terrain-cell`/
`terrain-cover` only after `terrain`; `unit-type`/`formation`/`friendly`/
`enemy`/`objective-area`/`extraction-area`/`resupply-area`/`target`/
`objective`/`jammer` repeated). `parse`/`serialise` read/write
`RawScenario`'s own fields directly (no DTO layer — F# `option` fields map
to an optional single-line directive, the `ReplaySerialisation.
ReplayCommandFile.InitialHash` precedent). A single-token id/reference
field must be whitespace-free and never the literal `-` (reserved for
"absent"); `serialise` throws `invalidArg` on a value that cannot be
represented, matching `ReplaySerialisation.serialise`'s own discipline for
its `issuer` field. A new `ScenarioFileError` union names the line and
field for every grammar fault, independent of `ScenarioError` (content
faults are `Scenario.validate`'s job, unchanged).

**`cwheadless import <path>`** (`src/CommandoWar.Headless/Program.fs`): a
new verb parsing a `.cwscenario` file, then running the existing
`Scenario.validate`. A new `describeScenarioError` renders every
`ScenarioError` case in the `describeReplayError` style. A new
`Exit.scenarioInvalid = 4` exit code distinguishes "well-formed grammar,
invalid content" from `Exit.replayError = 2` ("malformed grammar", the
`cmdReplayFile` precedent) and `Exit.usage = 1` (missing file/CLI mistake).
Success prints a summary (dimensions, deployment/objective/area counts,
headquarters, jammer count).

**Godot terrain-authoring pipeline** (`src/CommandoWar.Client.Godot/`):

- `tools/TerrainTileSet.cs`: builds a `TileSet` from the three TASK-041
  placeholder sprites (`terrain_floor.png`/`terrain_block.png`/
  `terrain_crate.png`) as atlas sources, four Custom Data Layers
  (`Class`/`MoveCost`/`Opaque`/`Elevation`), Elevation via three alternates
  (0/1/2) per source since it is the one `RawTerrainCell` field that varies
  per placed instance rather than per terrain type. `Cover`
  (`RawCoverFeature`) is deliberately **not** authored through this
  TileSet — a single tile carries one Custom Data set, but cover is up to
  four independent `(direction, level)` pairs per cell, a genuine modelling
  mismatch with a per-tile-type property, not something the "per-alternate
  Custom Data" design-decision note anticipated. Left for a later task;
  `Cover` stays hand-authorable directly in a `.cwscenario` file. Real bug
  found and fixed while building this: `TileData.SetCustomData` needs its
  atlas source already attached to the `TileSet` (`AddSource`) before
  `GetTileData`/`SetCustomData` — calling it in the opposite order (source
  fully configured, then added) fails with `Parameter "tile_set" is null`
  and silently produces empty/default custom data on every tile. Confirmed
  by the first proof run printing `class= elevation=0 moveCost=0
  opaque=false` for every painted cell; fixed by reordering, reconfirmed
  correct on rerun (see Evidence).
- `src/CommandoWar.Client.Godot/Core/TerrainAuthoring.fs` (new, in the
  ADR-0004 `CwClientCore` F# client-core project — no `Godot.*` type
  crosses into it, primitive arrays only, the existing IClientScene
  boundary contract): `exportScenario`, converting a painted layer's cells
  (as parallel primitive arrays) plus three placement cells into a full
  `RawScenario` — terrain-layer authoring only, every other field a small
  fixed skeleton (one friendly agent, one unit type, one "reach" objective,
  one extraction area) just sufficient for `Scenario.validate` to accept
  the result — then calls `ScenarioFile.serialise` and writes the file.
  Both the headless proof and the in-editor tool call this one function,
  so neither duplicates the skeleton or the format logic.
- `tools/ExportTerrainScript.cs`: the real `[Tool] EditorScript` Dave runs
  from the Script Editor against a scene with a painted `TileMapLayer`.
  **Not independently verified against the real interactive editor in this
  session** — Godot's C# `EditorScript` execution has documented friction
  outside a live editor GUI, and this implementing environment has no
  interactive display. Flagged for Dave to confirm before relying on it;
  `TerrainRoundTripProof.cs` (below) proves the underlying TileSet/Custom-
  Data/export pipeline itself, headless, independent of whether
  `EditorScript` specifically runs cleanly.
- `tools/TerrainRoundTripProof.cs`: a headless `SceneTree` script (run via
  `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path
  src/CommandoWar.Client.Godot --script res://tools/TerrainRoundTripProof.cs`
  — confirmed working directly in this environment after first hitting
  "Cannot instantiate C# script" until the project was `dotnet build`'d,
  which populates the assembly Godot's C# host loads scripts from). Builds
  and saves `art/terrain.tres`, paints a 6x6 `TileMapLayer` via `SetCell`
  (the same `TileData` a human clicking the same palette in the editor
  would produce — this proves the pipeline mechanically; it does not prove
  the *interactive painting experience* is good, which stays Dave's own
  call), reads every painted cell's Custom Data back, and calls
  `TerrainAuthoring.exportScenario`.
- `art/terrain.tres` (generated by `TerrainTileSet.BuildAndSave` — "re-run
  whenever the source art changes", not hand-edited) and
  `scenes/TerrainAuthoring.tscn` (an empty starter scene: a `TileMapLayer`
  named "TileMapLayer" using `terrain.tres`, matching `ExportTerrainScript.
  TerrainLayerPath` — built via a one-off scratch script, removed after
  use, the project's own established precedent for this kind of one-time
  content generation).

**Backlog correction**: `docs/11_BACKLOG.md`'s B-024 row listed
`B-007, TASK-006, B-043` as dependencies — circular against ADR-0001's own
review-trigger-1 text. Corrected to `none`; the row's prose now explains
the correction and that B-024 does not itself close B-043.

### Verification

- Command: `dotnet build src/CommandoWar.Sim/CommandoWar.Sim.fsproj -c Release`
  - Result: 0 warnings, 0 errors.
- Command: `dotnet build tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj -c Release`
  - Result: 0 warnings, 0 errors.
- Command: `dotnet test tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj -c Release --filter "FullyQualifiedName~ScenarioFileTests"`
  - Result: `Passed: 18, Failed: 0` (17 explicit facts + the 200-case FsCheck round-trip property).
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed: 395, Failed: 0` (377 + 18; full suite, unaffected outside the new file).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
  - Result: `OK - all 17 entries match their committed tables` (unaffected).
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only — no new dependency, no new NuGet package, no new BCL serialisation surface.
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: 0 warnings, 0 errors.
- Manual check: `dotnet run --project src/CommandoWar.Headless -c Release -- import content/scenarios/godot-painted-test.cwscenario`
  - Result: exit 0; summary matches the painted content (6x6 map, 1 friendly agent, 1 objective area, 1 extraction area, 1 objective, 0 headquarters/jammers).
- Manual check: a hand-written malformed file (`missing required 'content-version' directive`) and a well-formed-but-invalid file (`objective 1 references unknown area 'nowhere'`)
  - Result: exit 2 (grammar fault) and exit 4 (`Exit.scenarioInvalid`, content fault) respectively, each printing the expected named error.
- Manual check (real Godot editor, headless):
  `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path src/CommandoWar.Client.Godot --script res://tools/TerrainRoundTripProof.cs`
  - Result: `art/terrain.tres` written; 5 painted cells' Custom Data printed and
    matched exactly what was painted (`(1,1) passable elev=1 cost=1
    opaque=false`, `(2,2) impassable elev=0 cost=0 opaque=true`, `(3,3)
    passable elev=0 cost=3 opaque=true`, `(4,1) passable elev=2 cost=1
    opaque=false`, `(0,4) impassable elev=0 cost=0 opaque=true`);
    `content/scenarios/godot-painted-test.cwscenario` written; re-run a
    second time after removing the temporary scratch scripts (`HelloTool.cs`,
    `ScratchBuildAuthoringScene.cs`) to confirm the surviving files alone
    reproduce the identical result.

### Evidence

- `content/scenarios/godot-painted-test.cwscenario`: the real Godot-painted
  round-trip proof, committed.
- `src/CommandoWar.Client.Godot/art/terrain.tres`,
  `src/CommandoWar.Client.Godot/scenes/TerrainAuthoring.tscn`: the
  generated TileSet and starter authoring scene, committed.
- FsCheck round-trip property (`tests/CommandoWar.Sim.Tests/
  ScenarioFileTests.fs`), `MaxTest = 200`, 0 failures.
- `git status --porcelain` after the session matches scope: new
  `ScenarioFile.fs`, `TerrainAuthoring.fs`, `ScenarioFileTests.fs`,
  `tools/`, `art/terrain.tres`, `scenes/TerrainAuthoring.tscn`,
  `content/scenarios/`, this task file; modified `Program.fs` and three
  `.fsproj`/`.slnx`-adjacent project files (new `<Compile>` entries) plus
  `docs/11_BACKLOG.md`/`PROJECT_STATE.yaml`. Nothing under
  `CommandoWar.Sim`'s existing modules, `Simulation.fs`, `Canonical.fs`, or
  `Diagnostics.fs` touched.

### Review round 1 (2026-09-19, live)

Dave opened the real interactive editor, found `scenes/TerrainAuthoring.tscn`,
and could paint — but only every other grid cell took a tile, a checkerboard
pattern. Real bug, not a usage mistake: `TerrainTileSet.Build()` left
`TileSet.TileShape` at its default `Square` while placing 256x512 isometric
sprites, so painting only landed correctly on alternating logical cells.
Confirmed and fixed without needing Dave to keep testing blind: a temporary
render-to-PNG probe (`tools/IsometricShapeProbe.cs`, removed after use) built
a 4x4 grid with `TileShape = Isometric`, `TileOffsetAxis = Horizontal`,
`TileSize = (256, 128)` (the Kenney pack's own documented "base floor
height" — the diamond footprint, not the full 512px-tall sprite bounds) and
rendered it to a real screenshot, inspected directly: a continuous, gapless
isometric diamond grid, no checkerboard. Applied the same configuration to
`TerrainTileSet.Build()`; `art/terrain.tres` regenerated
(`TerrainRoundTripProof.cs` rerun); `content/scenarios/
godot-painted-test.cwscenario` reconfirmed byte-identical output and still
accepted by `cwheadless import`; `dotnet test` 395/395 and `cwheadless
corpus` 17/17 reconfirmed unaffected.

One unrelated side effect caught and reverted: running the probe/proof
scripts left `src/CommandoWar.Client.Godot/project.godot`'s
`run/main_scene` pointing at `TerrainAuthoring.tscn` instead of
`CommandDemo.tscn` (most likely Godot's editor recording whichever scene
was last open/focused) — `git diff` caught it before it could silently
break "run the project" for the actual game; reverted with `git checkout --`.

### Review round 2 (2026-09-19, live)

Dave tried again after the shape fix: painting now filled every cell, but
reported two further real defects, not usage mistakes — (1) "the orange
blocks dont lay down properly... presumably as there is only one image when
there should be multiple to describe the overlapped connected blocks", and
(2) "the mouse is quite offset to the tile when placing." Both traced to
one cause each, confirmed visually via a second temporary render-to-PNG
probe (`tools/IsometricAnchorProbe.cs`, removed after use) before touching
the real files again:

- (1) is Y-sort, not missing art. `FSharpSceneHost.cs`'s own
  `DrawTerrainTile` doc comment already documents that this Kenney art is
  "a tall, bottom-anchored canvas" — a block/crate sprite extends well
  above its own footprint, so adjacent tall tiles need to be drawn in
  actual depth order, not TileMapLayer's default storage-order draw.
  `TileMapLayer.YSortEnabled` was never set. Enabled on both the committed
  `scenes/TerrainAuthoring.tscn` (`y_sort_enabled = true`) and
  `TerrainRoundTripProof.cs`'s in-memory layer.
- (2) is texture anchoring. Godot's default `TileData.TextureOrigin`
  positions a texture region taller than `TileSize` in a way that does not
  match this pack's own "bottom-anchored" convention (the same doc comment:
  "its own bottom edge is the tile's near/south corner"). Fixed by setting
  `TextureOrigin = (0, -384)` on every tile/alternate in
  `TerrainTileSet.Build()` (512px sprite height minus the 128px diamond
  footprint), shifting the drawn texture up so its own bottom edge lands on
  the tile's own bottom edge instead of the texture's top-left corner
  landing there.

A 3x3 block grid rendered with both fixes showed a continuous, correctly
stacked isometric surface under direct zoomed inspection (grid lines align
across tile boundaries; no clipping/z-fighting between adjacent blocks).
`art/terrain.tres` regenerated; `content/scenarios/
godot-painted-test.cwscenario` reconfirmed byte-identical and still
accepted by `cwheadless import`; `dotnet test` 395/395 and `cwheadless
corpus` 17/17 reconfirmed unaffected. `project.godot`'s `run/main_scene`
touched again by running the probe, reverted again.

### Review round 3 (2026-09-19, live) — B-043 closed

Dave ran `ExportTerrainScript.cs` from the real editor after review round 2's
fixes. It worked — `content/scenarios/bridgehead-test.cwscenario` written,
646 lines, a real painted shape (not test noise; coordinate range roughly
x:1..26, y:-10..37) — but he "could not tell if anything happened," since
the script only prints to the Output panel and writes a file with no
dialog. Confirmed directly from the file's presence/timestamp/content
rather than asking him to go hunting for a print statement.

`cwheadless import` against it reported 302 `TerrainFeatureOutOfMap`
faults — expected, not a bug: `ExportTerrainScript.cs`'s first cut hardcoded
a 20x20 map, and Dave had painted a ~26x48 area including negative Y
coordinates, which `RawScenario`'s `[0, Width) x [0, Height)` map cannot
represent at all without translation. Asked Dave for a target size; he said
"bump it to fit what I painted * 2 or something." Rewrote
`ExportTerrainScript.cs` to compute the painted footprint from
`GetUsedCells()` at runtime, size the exported map to `SizeMultiplier * ` that
footprint (2x), translate every painted coordinate so the footprint sits
centred inside it, and place the three fixed-skeleton placement cells
(friendly start, objective area, exfil) at the map's four corners — always
outside the centred footprint's margin regardless of its actual shape, so
no per-scenario placement tuning is needed. Verified by hand against Dave's
actual painted coordinate range (footprint 26x48 -> map 52x96, margin
13/24, corners well clear of the translated footprint) rather than only by
inspection of the code.

Dave then gave the actual review-trigger-1 verdict, unprompted, after using
the fixed tool: **"its definitly easer to paint wi the mouse rather than
typinf cells."** Followed immediately by an important qualification, not
to be dropped: his intended real workflow is **generate a draft, then
hand-tweak it** with this tool — not hand-paint an entire map from an
empty canvas. This still answers review trigger 1 (the editor tooling is
worth using) but scopes *what for* — the confirmed value is in the
correction/refinement pass, not necessarily full from-scratch authoring.
Recorded in `decisions/ADR-0001-FRAMEWORK-SELECTION.md`'s 2026-09-19 note
and `docs/11_BACKLOG.md`'s B-043 row, both now `done`. The Godot decision
itself was never seriously in doubt at this point in the project, but it
is now backed by the direct measurement ADR-0001 always said it needed.

### Deviations and unresolved issues

- **Format corrected mid-drafting** (JSON -> the `.cwscenario` text
  grammar) before any code was written, once `ReplaySerialisation.fs`'s
  existing convention was actually read. See "Changes" above.
- **`Cover` (`RawCoverFeature`) is not paintable through the Godot TileSet**
  — a genuine per-tile-type-vs-per-cell modelling mismatch discovered while
  building `TerrainTileSet.cs`, not anticipated by the task's own
  design-decision note (which expected per-alternate Custom Data to cover
  it). Deferred; recorded as a known gap, not silently dropped.
- **`ExportTerrainScript.cs` (the real in-editor `[Tool]`) was not run
  through the actual interactive Godot editor GUI in this session** — no
  display in this implementing environment. **Resolved in review round 3**:
  Dave ran it live from the real editor; it worked (wrote a real,
  substantial `content/scenarios/bridgehead-test.cwscenario`), confirming
  `EditorScript.Run()` itself works in his environment, not only the
  underlying export call.
- **B-043 / ADR-0001 review trigger 1 was not resolved by this task alone
  — now resolved, in review round 3, below.**
- **`Elevation` via alternates was chosen and confirmed working**
  (the design-decision note's "confirm... before committing" condition);
  `Cover`'s deferral (above) is the one part of that note's plan that did
  not survive contact with the real API.

### Documents updated

- `tasks/TASK-060-SCENARIO-CONTENT-FORMAT-AND-GODOT-TERRAIN-AUTHORING.md`
  (status, acceptance criteria evidence);
- `docs/11_BACKLOG.md` (B-024 row: `proposed -> done`, dependency
  correction, evidence);
- `docs/12_PROGRESS_LEDGER.md` (Pinned facts "Green tests" 377 -> 395;
  index row; this detail file);
- `PROJECT_STATE.yaml` (`active_work`, to be updated alongside acceptance).

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-19)
- Notes: three live review rounds (see above), not a paper self-verification
  — Dave drove the real interactive editor himself throughout, which is why
  this task's evidence trail looks different from the TASK-058/059
  Sim-side-only precedent it was first expected to follow. Every reported
  defect (tile-shape checkerboard; Y-sort/texture-anchor overlap and mouse
  offset; fixed-size/negative-coordinate export gap) was root-caused,
  fixed, and re-verified same-session, ending with Dave's own live
  review-trigger-1 verdict closing B-043. Accepted on that basis, no
  changes requested beyond what the three rounds already produced.
