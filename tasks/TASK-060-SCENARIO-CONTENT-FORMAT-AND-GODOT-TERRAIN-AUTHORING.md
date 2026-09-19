# TASK-060: Scenario content format, importer, and Godot terrain authoring

Status: done (accepted by Dave 2026-09-19, after three live review rounds —
tile-shape checkerboard bug, Y-sort/texture-anchor overlap and mouse-offset
bug, and map-sizing/negative-coordinate handling, all found live and fixed
same-session; see
`docs/ledger/2026-09-19-TASK-060-scenario-content-format-and-godot-terrain-authoring.md`)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature complete); realises backlog B-024; realises
B-043 / ADR-0001 review trigger 1 (Dave's own live verdict, recorded in
`decisions/ADR-0001-FRAMEWORK-SELECTION.md`'s 2026-09-19 note — see "Why
this task exists")

Size: L

## Objective

Two things that only have meaning together:

1. A framework-neutral, on-disk scenario content format for `RawScenario`
   (extension `.cwscenario`, the `.cwlog`/`.cwreplay` naming precedent),
   plus a parser/serialiser in `CommandoWar.Sim` and a new `cwheadless
   import <path>` verb that parses a file, runs it through the existing
   `Scenario.validate`, and reports success (a summary) or every validation
   error with the same object/field-naming discipline `ScenarioError`
   already provides. Nothing here exists today — every scenario in the
   repo is hand-written F# record literals (`DemoScenario.fs`, `Corpus.fs`,
   `TurnDemo.fs`, `PathDemo.fs`, `LosDemo.fs`); there is no reader that "a
   Godot `.tscn` reader, a Tiled importer, or a test" (`Scenario.fs`'s own
   `RawScenario` doc comment) could actually feed. **Format: a hand-rolled,
   deterministic, line-based text grammar in the `ReplaySerialisation.fs`
   style** — line-based directives, an explicit `ParseError`-shaped union
   naming the line/field, deterministic `serialise`/`parse` round-trip —
   not JSON and not any general-purpose serialisation library.
2. A Godot-editor authoring surface for terrain: a `TileSet` built on the
   three existing placeholder sprites (`terrain_floor.png`,
   `terrain_block.png`, `terrain_crate.png`, TASK-041) with Custom Data
   Layers carrying `RawTerrainCell.Class`/`.MoveCost`/`.Opaque`, painted onto
   a plain `TileMapLayer` (no Terrain Sets / autotiling — see "Design
   decisions", it doesn't fit this art), plus a `@tool` editor script that
   walks the painted layer and writes it out in format (1)'s text shape.

The two together let a person click-paint a small terrain layout in the real
Godot editor and get a validated `RawScenario.TerrainLayer` out the other
end, instead of hand-typing `RawTerrainCell` records.

## Why this task exists

Dave flagged that hand-authoring a real map (not a 3-agent demo) by typing
`RawTerrainCell`/`RawCoverFeature` records in F# — the *only* way to author
terrain today — is wasteful, and asked whether Godot's own editor tooling
could assist ("i dont want to have to drive this side of things unless it
was an actual map editor etc"). Investigating that surfaced three findings,
in order:

- Godot 4.7's `TileSet` "Terrain Sets" autotiling is real and does what he
  described for *blended* terrain (grass/dirt/path edges) — but this
  project's placeholder art is three discrete, single-variant blocks from a
  "prototype" pack, with nothing to blend between. Autotiling doesn't apply
  here regardless of its own isometric-specific reliability history
  (community reports, and a third-party `Terrain Autotiler` plugin built to
  address it).
- The actual pain point is `RawTerrainLayer`'s hand-typed record shape
  (`Scenario.fs:307-331`), not autotiling.
- There is no scenario *file format* at all to paint terrain "into" even if
  Godot's plain `TileMapLayer` paint tool were used directly — every
  scenario is F# source. `ADR-0001` itself names this gap ("either a Tiled
  importer or a richer text format", `decisions/ADR-0001-FRAMEWORK-SELECTION.md:274`)
  and it was never built. This is backlog `B-024` ("content importer and
  validation command"), currently `proposed` and blocked on `B-043`.

Separately, `B-043` (Mibo 5.x reconsideration) is gated on ADR-0001 review
trigger 1: "the first Bridgehead authoring task (B-025) must record an
actual editor edit->visible-result measurement" (`decisions/ADR-0001-FRAMEWORK-SELECTION.md:392-395`).
No task since ADR-0001's acceptance has actually *authored content* in the
Godot editor's own visual tools — TASK-039 through TASK-059 were all
code-side feature work, verified by running the editor, not authored in it.
That measurement is still outstanding. Building and personally using this
task's terrain-painting surface is the cheapest real way to generate it —
cheaper than building the full B-025 Bridgehead map first, and it directly
answers the question Dave actually asked (is the editor's assistance worth
using) rather than a synthetic exercise.

This task does **not** itself close B-043: it builds the tool and proves the
round trip technically (parses, validates, matches what was painted). B-043
closes only once Dave has actually painted something in the real editor and
rendered his own felt-experience verdict — expected as a short follow-up
immediately after this task is accepted, not invented here on his behalf.

`B-024`'s backlog row currently lists a dependency ordering
(`B-007, TASK-006, B-043`) that is circular against ADR-0001's own trigger
text (which needs B-025-or-earlier authoring work to produce the B-043
evidence, but B-024 was gated *behind* B-043). Correcting that ordering is
part of this task's documentation updates.

## Design decisions (confirmed with Dave in conversation before drafting)

- **A hand-rolled deterministic text grammar (the `ReplaySerialisation.fs`
  precedent), not JSON and no serialisation library.** No new external
  application, no new NuGet package, and no new BCL serialisation surface
  either. This corrects the task's first draft, which specified
  `System.Text.Json` before `ReplaySerialisation.fs` had been read —
  `ReplaySerialisation.fs` shows this project's actual, deliberate
  convention for exactly this kind of content (`.cwlog`/`.cwreplay` both
  use it too): no general-purpose serialisation library appears anywhere
  in the solution, consistent with the project's determinism/typed-error
  discipline. Follow that precedent: line-based `keyword <fields>`
  directives (one directive kind per `Raw*` record, repeated lines for
  each array entry, exactly `ReplaySerialisation.fs`'s
  `checkpoint`/`command` pattern), `#`-comment and blank-line handling, a
  `version <n>` directive first (the `ReplaySerialisation.FormatVersion`
  precedent — a *file*-format version, independent of
  `ScenarioContent.Version`, which stays what `Scenario.validate` checks
  once the file is parsed), and an explicit `ScenarioFileError` union
  naming the line and field, in the same style as
  `ReplaySerialisation.ParseError`. `serialise >> parse` and
  `parse >> serialise` are identity on valid input, proven the same way
  `ReplaySerialisation.fs`'s own doc comment claims it.
- **A plain `TileMapLayer`, not Terrain Sets.** Autotiling is the wrong tool
  for discrete single-variant blocks (see "Why this task exists"). Custom
  Data Layers on the same `TileSet` carry the authored meaning per tile
  type; painting is still click-to-place, just without inter-tile matching.
- **`RawScenario`'s F# `option` fields (`TerrainLayer`, `Headquarters`) map
  onto this format exactly as `ReplaySerialisation.ReplayCommandFile.
  InitialHash: uint64 option` already does** — an optional single-line
  directive (present or absent), not a sentinel value. No separate DTO
  layer: the grammar reads/writes `RawScenario`'s own fields directly, the
  same way `ReplaySerialisation.parse`/`serialise` already read/write
  `ReplayCommandFile`/`RecordedCommand` directly with no DTO indirection.
- **Terrain-layer authoring only, this task.** The Godot painting surface
  and export script cover `RawTerrainLayer` (`Cells`/`Cover`) — the concrete
  pain point Dave named. `RawScenario`'s other tables (deployments,
  objectives, unit types, formations, jammers) round-trip through the new
  text format (so a hand-written or generated file can already carry them)
  but gain no Godot-side painting UI in this task. A later task can extend
  authoring to those if wanted.
- **`Elevation` and `Cover.Direction`/`.Level` authoring mechanism is an
  implementation-time decision, not fixed here.** Godot's per-alternate
  Custom Data override (multiple tile alternates per atlas source, each
  with its own Custom Data values) is the likely mechanism for `Elevation`
  bands and directional cover, but confirm it actually behaves as expected
  against the real 4.7.2 editor before committing — record whatever is
  chosen and why in the ledger. Do not invent a second authoring surface
  (e.g. a sidecar text list) unless the per-alternate approach is
  confirmed unworkable.
- **No Godot-side runtime loading.** The Godot client does not gain a
  "load a `.cwscenario` at runtime" feature in this task — the import path
  is `cwheadless import`, a build/content-time validation step. Wiring an
  imported scenario into `DemoScenario`/`CommandDemoScene` for live play is
  separate, later work, once a real map exists to load.
- **Proof is a small test area, not the Bridgehead map.** This task's
  round-trip evidence is a hand-painted handful of cells exercising every
  `RawTerrainCell`/`RawCoverFeature` field, not B-025's actual greybox map.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `decisions/ADR-0001-FRAMEWORK-SELECTION.md` (review trigger 1, the
  "richer text format" line at `:274`, the 2026-09-06 amendment)
- `decisions/ADR-0002-SIMULATION-BOUNDARY.md` (diagnostics/observer rule —
  confirm this task adds no new authoritative spatial/tactical state, so the
  `AGENTS.md` diagnostics-extension rule does not apply)
- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` section on shim size/
  boundary discipline — the export tool script is Godot-editor tooling, not
  a runtime client shim, but keep it thin and free of game-state logic
  regardless
- `docs/06_CONTENT_AND_PRESENTATION.md` sections on the content boundary and
  validation ("The adapter must fail with explicit field and object names
  when content is invalid. It may not pass Godot nodes, vectors, resources,
  or object references into the F# simulation.")
- `src/CommandoWar.Sim/Scenario.fs` in full — every `Raw*` type, the full
  `ScenarioError` union, and `Scenario.validate`'s exact behaviour and
  error-reporting shape (mirror it, do not diverge)
- `src/CommandoWar.Sim/ReplaySerialisation.fs` in full — the grammar/
  parser/serialiser style to mirror exactly (header directives, repeated-
  line array entries, `Result`-returning `parse`, `ParseError` union naming
  line and field, deterministic `serialise`)
- `src/CommandoWar.Headless/Program.fs` — the existing verb-dispatch
  pattern (`corpus`/`render`/`fixture`/`replay-file`, manual `--flag value`
  arg parsing, `Exit.usage`/`eprintfn` error convention) to mirror for
  `import`
- `src/CommandoWar.Headless/DemoScenario.fs` and `Corpus.fs` — concrete
  `RawScenario` construction examples, useful as round-trip fixtures
- `tasks/TASK-041-PLACEHOLDER-ART-SET.md` and
  `src/CommandoWar.Client.Godot/art/LICENSE-THIRD-PARTY.md` — exactly which
  sprites exist and what they represent (`floor_N`/`block_N`/`crate_N`)
- `tasks/TASK-049-AGENT-MOVEMENT-SPEED.md` /
  `tasks/TASK-059-FORMATION-SLOTS.md` — the "new optional authored table,
  resolve-and-validate, no silent default on an unresolved reference"
  pattern already established twice; this format is a sibling discipline,
  not a new philosophy
- Godot 4.7 `TileSet`/`TileMapLayer`/Custom Data Layers documentation (live
  check against the actual installed
  `Godot_v4.7.2-stable_mono_win64.exe` behaviour, not assumed from memory —
  confirm Custom Data Layers and per-alternate overrides behave as the
  design decisions above assume)

## Dependencies

- none blocking (`RawScenario`/`Scenario.validate` already exist;
  TASK-041's art is already imported). Not gated on B-043 — see "Why this
  task exists" for the corrected ordering.

## Inputs and assumptions

- `ScenarioContent.Version` is 6 (TASK-059); `Canonical.FormatVersion` is
  12. This task adds a *reader* for `RawScenario`, not a new field on it —
  no bump to either expected, unless the grammar design surfaces a genuine
  gap in the existing model (flag, do not silently patch `Scenario.fs`'s
  validated types to fit the format).
- The Godot editor is reachable in the implementing environment this
  session (`Godot_v4.7.2-stable_mono_win64.exe` present on disk) — use it
  directly for the round-trip proof rather than deferring verification to
  Dave, consistent with how TASK-056 onward already ran real headless/
  scripted editor checks.
- No existing corpus/fixture/replay entry is affected — this task adds a
  new content *input* path, it does not touch `Simulation.step`,
  `Canonical`, or any existing scenario.

## Allowed scope

- `src/CommandoWar.Sim/` — a new module (e.g. `ScenarioFile.fs`) holding
  the `.cwscenario` grammar, `parse: string -> Result<RawScenario,
  ScenarioFileError list>` (or the closest idiomatic equivalent — a single
  first structural error, matching `ReplaySerialisation.parse`'s
  short-circuit style, is also acceptable and should be picked by mirroring
  that module rather than inventing a new error-accumulation style), and
  `serialise: RawScenario -> string`; `CommandoWar.Sim.fsproj` gains no new
  `PackageReference` and no new BCL namespace beyond ordinary text/string
  handling already used by `ReplaySerialisation.fs`;
- `src/CommandoWar.Headless/Program.fs` — a new `import` verb: parse the
  named file, run `Scenario.validate`, print either a success summary
  (dimensions, deployment/objective counts) or every `ScenarioError`,
  mirroring the existing verbs' argument and exit-code conventions;
- `src/CommandoWar.Client.Godot/` — a new `TileSet` resource using the
  existing three terrain sprites with Custom Data Layers for `Class`/
  `MoveCost`/`Opaque` (and `Elevation`/cover per the design-decision note
  above); a small authoring scene/`TileMapLayer` to paint into; a `@tool`
  editor script exporting the painted layer to this task's `.cwscenario`
  text shape; none of this is wired into `CommandDemoScene`/`DemoScenario`
  at runtime;
- `tests/CommandoWar.Sim.Tests/` — new facts (including an FsCheck
  round-trip property: generate a `RawScenario`, serialise, parse, compare)
  and new `import`-verb-adjacent parse/validation-error facts;
- `content/scenarios/` (new directory) — the hand-painted test area's
  exported `.cwscenario` file, committed as the round-trip evidence;
- control-document updates (this task, `docs/11_BACKLOG.md` B-024 row and
  its dependency correction, `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`).

## Forbidden scope

- Terrain Sets / autotiling of any kind (wrong fit for this art — see
  "Why this task exists").
- A Tiled importer, JSON, or any general-purpose serialisation library or
  new external application dependency.
- Runtime scenario loading in the Godot client (`CommandDemoScene`,
  `DemoScenario`, or any scene) — this task's `import` path is a
  build/content-time CLI verb only.
- Building B-025's actual Bridgehead greybox map. This task proves the
  pipeline on a small test area only.
- Painting UI for deployments, objectives, unit types, formations, or
  jammers — terrain (`RawTerrainLayer`) only. The text format may still
  carry those fields (hand-written or generated) since they already exist
  on `RawScenario`.
- Rendering the imported/painted terrain inside a live Godot scene — no
  new `DrawItem`/render-path change.
- Rendering or Diagnostics changes: this task adds no new authoritative
  spatial/tactical state to `Simulation.step`, so the `AGENTS.md`
  diagnostics-extension rule (new `GridLayer`/`EdgeMarker`/`Overlay` plus a
  golden) does not apply — confirm this reading is correct during
  implementation rather than skipping the rule by assumption.
- Resolving B-043 / amending ADR-0001. This task generates the raw material
  for that verdict; it does not render the verdict.

## Required work

1. Inspect `Scenario.fs` (every `Raw*` field) and `ReplaySerialisation.fs`
   (the grammar/parser/serialiser style to mirror) in full.
2. Design the `.cwscenario` grammar: one directive per `Raw*` record kind,
   repeated lines for array entries, optional single-line directives for
   `TerrainLayer`/`Headquarters`, a leading `version <n>` directive. Write
   the grammar as a doc comment the way `ReplaySerialisation.fs` does.
3. Implement `parse`/`serialise` in `CommandoWar.Sim`; explicit, named
   failure (line and field) on a malformed file or missing required
   directive — no silent default (the `UnitTypes`/`Formations` precedent).
4. Add the `cwheadless import <path>` verb mirroring existing verb
   conventions; wire it through `Scenario.validate` for the
   already-established error-reporting shape.
5. Add FsCheck round-trip property tests plus explicit parse/validation-
   error facts.
6. Build the Godot `TileSet` (three existing sprites) with Custom Data
   Layers for `Class`/`MoveCost`/`Opaque`; resolve the `Elevation`/cover
   authoring mechanism against the real 4.7.2 editor (per the design-
   decision note) and record what was chosen and why.
7. Build a small authoring scene/`TileMapLayer` and the `@tool` export
   script writing this task's `.cwscenario` shape.
8. Round-trip proof: paint a small test area (covering every
   `RawTerrainCell`/`RawCoverFeature` field at least once) in the real
   Godot editor, export it, run `cwheadless import` against the result,
   confirm it validates and matches what was painted. Commit the exported
   file under `content/scenarios/`.
9. Correct `docs/11_BACKLOG.md`'s B-024 row dependency ordering (remove the
   circular `B-043` gate; note the corrected B-043 relationship instead).
10. Update `docs/06_CONTENT_AND_PRESENTATION.md` if its content-boundary
    description needs a concrete pointer to the new format/importer.
11. Update backlog, ledger, `PROJECT_STATE.yaml`.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] `RawScenario` (every field) round-trips through the new
      `.cwscenario` format: an FsCheck property (`MaxTest = 200`, 0
      failures) proves `parse(serialise(x)) = Ok x` for a constrained
      "representable" generator (single-token id/reference fields, no
      reserved `-` sentinel — `serialise` itself refuses any value it
      cannot represent, proven by two dedicated facts).
- [x] `cwheadless import <path>` on a malformed or invalid file prints a
      named error identifying the line/field (file-grammar fault, exit 2)
      or every `ScenarioError` with the same object/field-naming
      discipline the type already documents (content fault, exit 4) —
      proven by 12 explicit `ScenarioFileTests` facts plus a manual check
      of both exit paths against hand-written files.
- [x] `cwheadless import <path>` on a valid file exits success (0) with a
      summary; proven against the real Godot-painted export (see below),
      not only a code-constructed fixture.
- [x] A test terrain area, painted in the real Godot 4.7.2 editor's
      `TileMapLayer` using the new `TileSet`/Custom Data Layers (headless,
      via `tools/TerrainRoundTripProof.cs` — the same `SetCell`/`TileData`
      API an interactive paint click produces), exports via the shared
      exporter to `content/scenarios/godot-painted-test.cwscenario`, which
      `cwheadless import` accepts, with the resulting `RawTerrainLayer`
      matching what was painted, confirmed cell by cell (5/5 cells, both
      terrain classes, two non-zero elevations).
- [x] `CommandoWar.Sim.fsproj` gained no new `PackageReference`; `dotnet
      list src/CommandoWar.Sim package --include-transitive` reports
      `FSharp.Core` only.
- [x] No pre-existing scenario, corpus entry, fixture, or `Canonical`/
      `ScenarioContent` version changed (`cwheadless corpus`: 17/17
      unaffected; `dotnet test`: 395/395, +18, nothing pre-existing
      touched).
- [x] `docs/11_BACKLOG.md`'s B-024 row is corrected (no circular B-043
      dependency) and marked with this task's evidence.
- [x] `dotnet build` (main `.slnx` Release; Godot client Debug) = 0
      warnings, 0 errors.
- [x] Required documentation updated (this file, `docs/11_BACKLOG.md`,
      `docs/12_PROGRESS_LEDGER.md` index + detail file, `PROJECT_STATE.yaml`).

## Required verification

- `dotnet build CommandoWar.slnx -c Release`
- `dotnet build` the Godot client `.slnx` (Debug)
- `dotnet test CommandoWar.slnx -c Release` before and after
- `dotnet run --project src/CommandoWar.Headless -c Release -- import
  content/scenarios/<the-test-file>.cwscenario`
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
  (confirm unaffected)
- The real Godot 4.7.2 editor, run directly
  (`Godot_v4.7.2-stable_mono_win64.exe`), to build the `TileSet`, paint the
  test area, and run the export script — not simulated or assumed
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
- `git status`

## Evidence to capture

- command output or test summary, including the FsCheck round-trip
  property's result;
- the painted test area's exported `content/scenarios/*.cwscenario`, and
  the `cwheadless import` output against it;
- a screenshot or description of the painted `TileMapLayer` in the real
  Godot editor;
- whatever was decided for `Elevation`/cover authoring and why;
- unresolved failures, if any.

## Expected files

- `src/CommandoWar.Sim/ScenarioFile.fs` (new), `Scenario.fs` (read, likely
  unchanged)
- `src/CommandoWar.Headless/Program.fs`
- `src/CommandoWar.Client.Godot/art/*.tres` or similar (new `TileSet`),
  a new authoring scene, a new `@tool` script
- `content/scenarios/*.cwscenario` (new)
- `tests/CommandoWar.Sim.Tests/*.fs`
- `docs/11_BACKLOG.md`, `docs/06_CONTENT_AND_PRESENTATION.md` (if touched),
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`, this task file

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md` (B-024 row: `proposed` -> `done` or `review`,
  dependency ordering corrected; a note on B-043's now-decoupled status —
  do not mark B-043 itself `done`, only note that this task supplies its
  evidence);
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file);
- `PROJECT_STATE.yaml` (`active_work`; note that B-043 still needs Dave's
  own live-authoring verdict as an immediate follow-up).

## Rollback or removal

Purely additive: a new `CommandoWar.Sim` module, a new CLI verb, new Godot-
side content files, and a new `content/scenarios/` directory. Nothing
existing is modified except `Program.fs`'s verb dispatch (one new match
arm) and `docs/11_BACKLOG.md`'s B-024 dependency text. Revertible by
reverting the commit.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
