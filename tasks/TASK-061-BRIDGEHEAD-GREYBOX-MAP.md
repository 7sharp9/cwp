# TASK-061: Bridgehead greybox map

Status: done (accepted by Dave 2026-09-19, "this looks ok", after a live
review round in the real Godot 4.7.2 editor -- see "Implementation outcome"
below and docs/ledger/2026-09-19-TASK-061-bridgehead-greybox-map.md's
"Review round 1" for the full report, including a real bug found and fixed
live: `ImportTerrainScript` was overlaying onto stale pre-existing scene
content instead of clearing first)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature complete); realises backlog B-025
Size: M-L (see "Why this task exists" — the backlog's M likely undercounts
the added terrain-importer tooling, the B-027 precedent for a task that
also stands up new capability)

## Objective

A new `content/scenarios/bridgehead.cwscenario` exists: a greybox terrain
layout and friendly/enemy deployment for the docs/07 "Bridgehead" mission
(rail bridge and depot at dusk), valid under `cwheadless import` (exit 0,
no faults). It is a physical-space and force-placement greybox only — no
authored `Objectives[]` mission-sequence content, since no runtime
objective-evaluation logic exists in `Simulation.fs` yet (confirmed by
inspection: no `Objective`/`MissionOutcome`/`MissionStatus` case anywhere
in it). That belongs to B-032 ("Implement demolition objective,
extraction, success, and failure"), which already depends on this task.

A second, smaller deliverable closes a real tooling gap surfaced while
scoping this task: TASK-060's terrain-authoring tool only exports (a
painted `TileMapLayer` -> `.cwscenario`), never imports. Without an import
direction, a hand-authored draft's terrain layer cannot actually be loaded
back into the Godot editor for Dave to visually inspect and hand-tweak —
the workflow B-043's closing note itself asked for ("generate a draft,
then hand-tweak it with this tool"). This task adds a minimal
`tools/ImportTerrainScript.cs`, the reverse of `tools/ExportTerrainScript.cs`,
that reads a `.cwscenario`'s terrain layer and paints it onto
`TerrainAuthoring.tscn`'s `TileMapLayer`.

## Why this task exists

B-025 was the next ready item once B-024 (TASK-060) and B-043 (the ADR-0001
review-trigger-1 measurement) both closed 2026-09-19 — see
`PROJECT_STATE.yaml`'s `active_work` note and `docs/11_BACKLOG.md`'s B-043
row. B-043's own closing note flagged that later authoring tasks "should
scope around" a generate-then-hand-tweak shape, not paint-from-empty — but
left open *how* the generate half works. Scoped this session via three
`AskUserQuestion` rounds with Dave before drafting (see "Design decisions"
below), directly informed by `docs/07_VERTICAL_SLICE.md` section 3 (the
Bridgehead setting/forces/objective-sequence description) and section 7
(procedural maps are explicitly excluded from the slice).

## Design decisions (confirmed with Dave via `AskUserQuestion`, three rounds,
before drafting)

- **Generation method: agent-authored `.cwscenario` text, not a script or
  runtime procedural system.** `docs/07` section 7 explicitly excludes
  "procedural maps" from the slice, and Bridgehead is one fixed, bespoke
  layout anyway (a specific bridge/depot, specific MG-team and rifleman
  positions per section 3) — nothing here benefits from proc-gen variety.
  "Generate a draft" means: read the natural-language mission description
  (`docs/07` plus Dave's own verbal elaboration) and hand-write a first-pass
  `.cwscenario` directly, the same way `DemoScenario.fs` or
  `godot-painted-test.cwscenario` were authored — not a new authoring
  script, not a new `CommandoWar.Sim` code-generation feature. Automating
  this description-to-draft step (a helper LLM or tooling bridging it) is
  Dave's own explicitly deferred future idea, not built now — recorded as
  new backlog row B-063, unblocked, not gating anything.
- **Scope split with B-032: terrain + deployment only, no authored
  `Objectives[]`.** Confirmed by inspecting `Simulation.fs`: no objective or
  mission-outcome evaluation exists anywhere in the simulation today (every
  `RawObjective`/`RawArea`/`RawTarget` in the codebase is validated content
  with nothing that reads it at runtime). Authoring a `Kind`
  sequence now would be unverifiable, untested content pre-empting a design
  B-032 (which depends on this task, per `docs/11_BACKLOG.md`) has not yet
  made. `ObjectiveAreas`/`ExtractionAreas`/`StaticTargets` position markers
  (a bridge/observation area, the MG position, an extraction rally point) —
  pure landmark data, not mission logic — are in scope, since B-032 will
  want them to already exist rather than re-deriving map coordinates itself.
- **A new minimal terrain-layer importer, reversing `ExportTerrainScript.cs`,
  is in scope.** Without it, "hand-tweak the draft visually with the tool"
  is not actually possible — confirmed by inspecting
  `tools/ExportTerrainScript.cs`/`Core/TerrainAuthoring.fs`: only an export
  path (painted `TileMapLayer` -> `.cwscenario`) exists. Terrain-layer only
  (`Class`/`Elevation`/`MoveCost`/`Opaque`), mirroring TASK-060's own
  terrain-only scope for the export side; deployments/areas/targets stay
  text-edited, not paintable (no painting UI exists for them either, and
  building one is not this task's job).
- **No Godot client scene integration.** `bridgehead.cwscenario` is
  validated and inspected headlessly (`cwheadless import`, `cwheadless
  render`) and visually via the new importer loading its terrain layer into
  `TerrainAuthoring.tscn` — it is not wired into a new or existing playable
  scene (`SnapshotDemo.tscn`/`CommandDemo.tscn` keep running `DemoScenario`
  unchanged). Loading the real Bridgehead map into a playable scene is
  B-035's integration job ("Integrate and verify complete vertical slice"),
  not this task's.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `docs/07_VERTICAL_SLICE.md` sections 3 (scenario setting/forces/objective
  sequence), 4 (required player commands), 7 (deliberate exclusions —
  procedural maps), 9 (functional acceptance criteria)
- `decisions/ADR-0001-FRAMEWORK-SELECTION.md` 2026-09-19 note (the
  generate-then-hand-tweak shape this task follows)
- `tasks/TASK-060-SCENARIO-CONTENT-FORMAT-AND-GODOT-TERRAIN-AUTHORING.md`
  (the `.cwscenario` format, the export tool, the terrain TileSet)
- `src/CommandoWar.Sim/Scenario.fs` (`RawScenario` and every `Raw*` field —
  the full authoring surface; `RawTerrainCell`/`RawCoverFeature` doc
  comments; `Scenario.validate`)
- `src/CommandoWar.Sim/ScenarioFile.fs` (the `.cwscenario` line-based
  grammar to hand-write against)
- `src/CommandoWar.Headless/DemoScenario.fs` (the hand-built `RawScenario`
  authoring precedent to follow directly)
- `content/scenarios/godot-painted-test.cwscenario` (an existing `.cwscenario`
  file, for the on-disk grammar)
- `src/CommandoWar.Client.Godot/tools/ExportTerrainScript.cs`,
  `src/CommandoWar.Client.Godot/tools/TerrainTileSet.cs`,
  `src/CommandoWar.Client.Godot/Core/TerrainAuthoring.fs` (the export
  direction to mirror; `TerrainTileSet.Sources` is the exact, currently
  fixed vocabulary of `(Class, MoveCost, Opaque)` combinations the TileSet
  can represent — floor `(passable, 1, false)`, block `(impassable, 0,
  true)`, crate `(passable, 3, true)` — the authored terrain must stay
  within these three or the new importer cannot paint the cell)
- `src/CommandoWar.Client.Godot/scenes/TerrainAuthoring.tscn`
- `src/CommandoWar.Sim/Simulation.fs` (confirm directly: no
  objective/mission-outcome evaluation exists yet, the basis for excluding
  `Objectives[]` from this task's scope)
- `docs/11_BACKLOG.md` (B-025, B-032, B-035 rows; B-043's closing note)

## Dependencies

- B-024 (TASK-060) — done, provides the `.cwscenario` format, `cwheadless
  import`, and the terrain TileSet/export tool this task extends.

## Inputs and assumptions

- `ScenarioContent.Version` is 6 (TASK-059). This task adds no new
  `RawScenario` field, so no version bump — content authoring only, no
  format change.
- The Bridgehead map's exact dimensions and tile-by-tile layout are not
  fixed by any prior document; they are this task's own design output,
  informed by `docs/07` section 3 (compact rail bridge and depot; one
  squad — leader plus five, two fireteams of three; one MG team on an
  exposed approach, four riflemen in prepared positions) and section 2
  (10-20 minute playtime target). Treat the first-pass geometry as
  deliberately provisional — it is refined live by Dave via the new
  importer/terrain tool, not required to be final here.
- The terrain vocabulary is constrained to the three existing
  `TerrainTileSet.Sources` combinations (see "Required reading"). A rail
  bridge/river greybox fits this without new art: `impassable` water/gap,
  `passable` bridge deck and open ground, `passable`+`opaque` crates/walls
  for depot cover and the MG emplacement. Do not add new source art or
  TileSet sources in this task — flag it as a follow-up if the greybox
  genuinely needs a fourth terrain flavour Dave should decide on.
- `RawCoverFeature` (directional low cover) is not paintable through the
  TileSet (TASK-060's known gap) — author it directly in the `.cwscenario`
  text, same as `DemoScenario.fs` does.
- "Support weapon" and "demolition interaction owned by the squad" (`docs/07`
  section 3) have no distinct simulation mechanic yet (no support-weapon
  suppression bonus, no demolition/charge-planting interaction exists in
  `CommandoWar.Sim`). Represent the support-weapon soldier as an ordinary
  deployment for now — content labelling only, not a new mechanic; the
  demolition interaction itself is explicitly B-032's job.

## Allowed scope

- `content/scenarios/bridgehead.cwscenario` (new file): terrain layer
  (bridge, depot, approach lane, cover), `FriendlyDeployments` (six agents:
  leader plus five), `EnemyDeployments` (MG team plus four riflemen),
  `RawScenario.Formations`/`RawDeployment.FormationId`/`.SlotIndex` (the two
  three-agent fireteams, TASK-059's mechanism), `UnitTypes` (the
  TASK-049 mechanism; reuse `Agent.MoveSpeedDefault`-equivalent values
  unless a real reason to diverge surfaces), `ObjectiveAreas`/
  `ExtractionAreas`/`StaticTargets` landmark markers. `Objectives = [||]`
  (deliberately empty — see "Design decisions"). `Headquarters = None`,
  `Jammers = [||]` (no comms-degradation authored for a greybox pass).
- `src/CommandoWar.Client.Godot/tools/ImportTerrainScript.cs` (new
  `[Tool] EditorScript`, the `ExportTerrainScript.cs` precedent reversed):
  reads a `.cwscenario` path, resolves its terrain layer's cells to
  `(sourceId, alternateId)` pairs via the fixed `TerrainTileSet.Sources`
  vocabulary, and calls `TileMapLayer.SetCell` on `TerrainAuthoring.tscn`'s
  `TileMapLayer` for each. Reports (does not silently drop) any cell whose
  `(Class, MoveCost, Opaque)` does not match a known source.
- `src/CommandoWar.Client.Godot/Core/TerrainAuthoring.fs`: a new
  `importTerrainLayer` function (or equivalent), the `exportScenario`
  precedent reversed — takes a scenario file path (or already-parsed
  primitive arrays), returns parallel primitive arrays
  (`cellX`/`cellY`/`class`/`elevation`/`moveCost`/`opaque`) for the C# side
  to paint from. `ScenarioFile.parse`/`Scenario.validate` already exist and
  do the actual parsing/validation — this is a thin adapter, not a new
  parser.
- A headless proof (the `tools/TerrainRoundTripProof.cs` precedent) proving
  import -> re-export round-trips a terrain layer unchanged, run for real
  through the installed Godot 4.7.2 editor, not simulated.
- `docs/11_BACKLOG.md` (B-025 row; new B-063 row for the deferred
  generation-automation idea), `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`, this task file.

## Forbidden scope

- Any `RawScenario.Objectives[]` entry, or any new mission-outcome/
  objective-evaluation logic in `CommandoWar.Sim` — B-032's job.
- Any procedural or runtime map-generation code (`docs/07` section 7's
  exclusion applies to authoring tooling too, not only shipped gameplay —
  this task hand-authors one fixed map).
- Any change to `RawTerrainCell`/`RawCoverFeature`/`Scenario.validate` or
  `ScenarioContent.Version` — this task authors content within the
  existing format, it does not extend the format.
- New TileSet source art, a fourth terrain flavour, or any change to
  `TerrainTileSet.cs`'s `Sources` table.
- A demolition/charge-planting interaction, a support-weapon suppression
  bonus, or any other new `CommandoWar.Sim` gameplay mechanic.
- Wiring `bridgehead.cwscenario` into `SnapshotDemo.tscn`, `CommandDemo.tscn`,
  or any new playable scene — B-035's job.
- A deployment/area/objective painting UI in Godot (only the terrain-layer
  importer is in scope).
- A new package, project, or dependency.

## Required work

1. Design the Bridgehead physical layout from `docs/07` section 3: a
   compact rail bridge crossing an impassable gap/river, a depot cluster
   (impassable/opaque blocks, passable/opaque crates for cover) near one
   bank, an open approach lane overwatched by the MG team's position, and
   directional low cover (`RawCoverFeature`) around the depot and approach.
   Record the chosen map dimensions and zone layout in the ledger.
2. Hand-author `content/scenarios/bridgehead.cwscenario`: terrain layer,
   six friendly deployments (leader + five, two three-agent fireteams via
   `Formations`), five enemy deployments (MG team + four riflemen in
   covered positions), landmark `ObjectiveAreas`/`ExtractionAreas`/
   `StaticTargets`, `UnitTypes`. `Objectives = [||]`.
3. Validate: `cwheadless import content/scenarios/bridgehead.cwscenario`
   (exit 0, no faults) and `cwheadless render` (ascii/svg/html) for a
   direct visual sanity check independent of Godot.
4. Build `ImportTerrainScript.cs` and `TerrainAuthoring.importTerrainLayer`,
   mirroring the export direction. Resolve each terrain cell's
   `(Class, MoveCost, Opaque)` to the matching `TerrainTileSet.Sources`
   entry's `(sourceId, alternateId matching Elevation)`; report clearly any
   cell that cannot be resolved (should not occur for this task's own
   content, given the vocabulary constraint in "Inputs and assumptions",
   but must not silently mis-paint one either).
5. Prove the importer for real: a headless round-trip (the
   `TerrainRoundTripProof.cs` precedent) — import `bridgehead.cwscenario`'s
   terrain layer into a `TileMapLayer`, re-export it, and diff the
   resulting `RawTerrainLayer` cells against the original for an exact
   match (order-independent). Run through the real installed Godot 4.7.2
   editor headless, not simulated.
6. Flag for Dave, not resolved in this task: opening `TerrainAuthoring.tscn`
   in the real interactive editor, running `ImportTerrainScript.cs`, and
   confirming the painted result visually matches intent — the same
   "not run through the actual interactive GUI in this session" gap
   TASK-060 itself flagged for its own export tool, for the identical
   environment reason (no display in the implementing environment).
7. Update `docs/11_BACKLOG.md` (B-025 row; add B-063 for the deferred
   generation-automation idea, `P4` or later, no blocking dependency),
   `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] `content/scenarios/bridgehead.cwscenario` exists, matches `docs/07`
      section 3's force composition (6 friendly incl. 2 fireteams of 3, 5
      enemy incl. an MG team), and passes `cwheadless import` with exit 0
      and no reported faults. **Met.** `cwheadless import` output: exit
      code `0`, `friendly agents : 6`, `enemy agents : 5`, no reported
      faults.
- [ ] The map contains no `Objectives[]` entries (confirmed by inspection —
      deliberate, per "Design decisions"). **Not met — deviation, see
      "Implementation outcome" below.** `Scenario.validate` unconditionally
      rejects an empty `Objectives` array (`MissingRequiredMarker
      "Objective"`), which would fail the criterion directly above this one
      (exit 0, no faults). The file carries exactly one placeholder
      `IsOptional=true` "reach" objective instead of a literal `[||]` —
      not the docs/07 mission sequence, still entirely B-032's job.
- [x] `ImportTerrainScript.cs` painting `bridgehead.cwscenario`'s terrain
      layer into a `TileMapLayer`, then re-exporting it, reproduces the
      original `RawTerrainLayer` cells exactly — proven by a headless
      round-trip run through the real Godot 4.7.2 editor, not simulated.
      **Met**, via `tools/BridgeheadTerrainImportProof.cs` (the identical
      import code path `ImportTerrainScript.cs` uses, exercised headlessly
      since this environment has no display for the interactive editor):
      43/43 cells resolved and painted, re-export matched the original
      exactly (order-independent). `ImportTerrainScript.cs` itself was
      built but not run through the interactive editor — see
      "Implementation outcome".
- [x] No pre-existing `.cwscenario`/corpus/fixture entry, Godot scene, or
      `--selfcheck` hash is affected (purely additive content + a new,
      unused-by-existing-scenes tool). **Met.** `-- corpus` `17/17`
      unaffected; `dotnet test` `395/395` unaffected; no scene wired to the
      new content; `godot-painted-test.cwscenario` re-generated
      byte-identical when `TerrainRoundTripProof.cs` was re-run for
      comparison. (`art/terrain.tres` did come back regenerated as a side
      effect of running the Godot proofs — reverted, not committed; see
      "Implementation outcome", pre-existing drift unrelated to this task.)
- [x] `dotnet build` all three `.slnx` (main Release; Godot client Debug)
      = 0 warnings, 0 errors. **Met** for all three `.slnx` (main Release,
      Godot client Debug, and Mibo Debug — checked for completeness).
- [x] `dotnet test` unaffected (no `CommandoWar.Sim`/`.Headless` behaviour
      change). **Met.** `395/395`, unchanged before and after.
- [x] `dotnet list src/CommandoWar.Sim package --include-transitive` =
      `FSharp.Core` only; no forbidden dependency. **Met.**
      `CommandoWar.Sim` itself was not touched by this task.
- [x] `docs/11_BACKLOG.md` (B-025 row done, new B-063 row added),
      `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`, this task file
      all updated. **Met** (B-025 row `proposed -> review -> done` across
      self-verification then Dave's live-review acceptance; B-063 row was
      added during this session's earlier scoping pass, before
      implementation began).

## Required verification

All run for real; exact commands and results (full transcript in
`docs/ledger/2026-09-19-TASK-061-bridgehead-greybox-map.md`):

- `dotnet build CommandoWar.slnx -c Release` → `0 Warning(s), 0 Error(s)`.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  → `0 Warning(s), 0 Error(s)` (includes every new/changed file this task
  added).
- `dotnet build src/CommandoWar.Client.Mibo/CommandoWar.Client.Mibo.slnx -c Debug`
  → `0 Warning(s), 0 Error(s)` (the third `.slnx`, untouched, checked for
  completeness).
- `dotnet test CommandoWar.slnx -c Release` before and after → `395/395`
  both times, unchanged.
- `dotnet run --project src/CommandoWar.Headless -c Release -- import content/scenarios/bridgehead.cwscenario`
  → exit code `0`; `friendly agents : 6`, `enemy agents : 5`,
  `objective areas : 1`, `extraction areas : 1`, `static targets : 2`,
  `objectives : 1`, `headquarters : none`, `jammers : 0`; no faults.
- **Deviation from this line's literal text:** `cwheadless render <target>`
  has no `.cwscenario`-file target — confirmed by inspecting
  `Program.fs`/`cmdRender`: it only accepts
  `fixture | demo | los | path | <command-log path>`, and any other string
  is read as a `.cwlog` command log against the shared `Fixture` world, not
  a scenario file. Adding a scenario-render verb is a `CommandoWar.Headless`
  CLI change, outside this task's "Allowed scope". Used instead: an ad hoc,
  not-committed console harness (`ScenarioFile.parse` →
  `Scenario.validate` → `World.ofScenario` → `DiagnosticRender.Ascii`,
  referencing `CommandoWar.Sim`/`CommandoWar.Headless` by
  `ProjectReference` from the scratchpad directory) — a genuine visual
  sanity check independent of Godot, reproduced in full in the ledger
  detail file.
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
  before and after → `OK - all 17 entries match their committed tables`,
  both times.
- The Godot headless round-trip proof, run for real through
  `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path
  src/CommandoWar.Client.Godot --script res://tools/BridgeheadTerrainImportProof.cs`
  → `43 authored cell(s)`; `painted 43 cell(s), 0 unresolved`; `original
  cell count: 43, re-exported cell count: 43`; `TERRAIN_ROUND_TRIP_MATCH:
  true (exact, order-independent match)`.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  → `FSharp.Core 10.1.303` only.
- Source scan of `src/CommandoWar.Sim` for a forbidden dependency → three
  doc-comment-only mentions (`Diagnostics.fs`, `Scenario.fs`,
  `Pathfinding.fs`, each describing what the module does *not* reference);
  `CommandoWar.Sim` was not touched by this task.
- `git status` → clean except the files this task changed (see
  "Implementation outcome"); nothing committed.

## Evidence to capture

- `cwheadless import` command output (above) — met.
- The round-trip proof's full console output (above) — met,
  `TERRAIN_ROUND_TRIP_MATCH: true`.
- `content/scenarios/bridgehead.cwscenario` itself — present.
- Unresolved failures: none. Two deviations (non-empty `Objectives`; no
  `cwheadless render` scenario-file target) are documented above and in
  the ledger detail file, not silently resolved.
- Explicit note for Dave: the real interactive-editor run of
  `ImportTerrainScript.cs` (opening `TerrainAuthoring.tscn` in the actual
  Godot editor GUI, running the script, and visually confirming the
  painted result matches intent) was **not performed** — no display in
  this implementing environment, the identical gap TASK-060 flagged for
  `ExportTerrainScript.cs`. The headless proof exercises the identical
  import code path for real, but does not itself prove the interactive
  painting/inspection experience is good.

## Expected files

- `content/scenarios/bridgehead.cwscenario`
- `src/CommandoWar.Client.Godot/tools/ImportTerrainScript.cs`
- `src/CommandoWar.Client.Godot/Core/TerrainAuthoring.fs`
- a headless round-trip proof under `src/CommandoWar.Client.Godot/tools/`
  (or wherever `TerrainRoundTripProof.cs` already lives — confirm by
  inspection)
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`,
  this task file

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md` (B-025 row: `proposed` -> `done` or `review`; new
  B-063 row for the deferred generation-automation idea);
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file);
- `PROJECT_STATE.yaml` (`active_work`).

## Rollback or removal

Purely additive: one new content file nothing else references yet, one new
Godot editor tool script, and one new F# function alongside the existing
`exportScenario`. Revertible by reverting the commit; no existing scenario,
corpus entry, or scene depends on any of it.

## Implementation outcome (2026-09-19, self-verified)

Full evidence, exact commands, and full deviation writeups live in
`docs/ledger/2026-09-19-TASK-061-bridgehead-greybox-map.md`; this section is
the short version.

**Files changed:**
`content/scenarios/bridgehead.cwscenario` (new);
`src/CommandoWar.Client.Godot/Core/TerrainAuthoring.fs` (+`importTerrainLayer`,
+`ImportedTerrainLayer`);
`src/CommandoWar.Client.Godot/tools/TerrainTileSet.cs` (+`Resolve`, `Sources`/
`Build` untouched);
`src/CommandoWar.Client.Godot/tools/ImportTerrainScript.cs` (new);
`src/CommandoWar.Client.Godot/tools/BridgeheadTerrainImportProof.cs` (new).
`CommandoWar.Sim`/`CommandoWar.Headless` untouched.

**Observable behaviour changed:** none for existing content — purely
additive. A new `.cwscenario` file and a new opt-in Godot editor tool exist;
nothing existing references either.

**Two deviations from the task file's literal text**, both found during
implementation, both documented in full in the ledger detail file and in
the acceptance-criteria checkboxes above:

1. `Scenario.validate` rejects an empty `Objectives` array unconditionally
   (`MissingRequiredMarker "Objective"`), which conflicts with this task's
   own literal `Objectives = [||]` instruction and its "no `Objectives[]`
   entries" acceptance criterion — the empty array cannot coexist with the
   task's own headline "valid under `cwheadless import`, exit 0, no
   faults" requirement. Resolved with one placeholder `IsOptional=true`
   "reach" objective, following the exact precedent already set by
   `TerrainAuthoring.exportScenario`'s own skeleton and
   `godot-painted-test.cwscenario`. The corresponding acceptance criterion
   is marked **not met**, not silently checked off.
2. `cwheadless render` has no target for an arbitrary `.cwscenario` file
   (confirmed by inspecting `Program.fs`), so the "visual sanity check
   independent of Godot" verification step used an ad hoc, not-committed
   console harness instead of the literal `cwheadless render bridgehead`
   command — adding a new CLI verb was outside this task's allowed scope.

**Risks / open questions for Dave:**

- Whether the placeholder `Objectives` entry is an acceptable resolution,
  or whether Dave would rather see `Scenario.validate`'s
  `MissingRequiredMarker "Objective"` check itself revisited in a future
  task (out of this task's forbidden scope to change).
- The map's exact geometry is a first-pass design output, explicitly
  provisional per "Inputs and assumptions" — meant to be refined live by
  Dave via `ImportTerrainScript.cs` in the interactive editor, which has
  not yet happened (no display in this environment).
- Whether `cwheadless render` should eventually gain a `.cwscenario`-file
  target — flagged, not filed as a new backlog row (out of this task's
  scope to add one unprompted).

## Review round 1 (2026-09-19, live) and acceptance

Dave ran `ImportTerrainScript.cs` for real in the interactive Godot 4.7.2
editor -- the one gap flagged above as outstanding. Full writeup in the
ledger detail file's "Review round 1" section; short version:

- **Real bug found and fixed:** the script never cleared the `TileMapLayer`
  before painting, so it silently overlaid Bridgehead's 43 cells onto a
  large leftover hand-painted test area still baked into the committed
  `TerrainAuthoring.tscn` from TASK-060's own review sessions. Fixed with
  one `layerNode.Clear()` call; an import now always replaces, never
  overlays.
- **A confusing intermediate symptom** (screenshots appearing to show only
  a small fraction of the map even after zooming out) was run to ground
  with two temporary headless diagnostics, not left unresolved: loading
  the *actual saved* `TerrainAuthoring.tscn` from disk and reading it back
  through Godot's own `GetUsedCells()`/`GetCellTileData()` API confirmed
  all 43 cells were present and correct the whole time -- a screenshot/
  viewport-timing artefact, not a real data or rendering defect.
- `TerrainAuthoring.tscn` was reset back to its documented empty
  starter-scene state after the live check (Dave's explicit choice, since
  it is a shared scratch canvas, not a per-map artifact).

Accepted by Dave: "this looks ok." No further changes requested.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
