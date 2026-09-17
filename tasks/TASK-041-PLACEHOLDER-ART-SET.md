# TASK-041: Minimal coherent placeholder art set (Kenney, CC0)

Status: done (accepted by Dave 2026-09-17, "seems to work")
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete)
Size: M

## Review

Dave accepted the art as working. Two follow-ups raised, neither blocking
this task's own acceptance criteria (both are new behaviour, not part of
TASK-041's scope, which explicitly forbade animation/directional facings and
didn't touch selection UI):

- Agents don't always visually face the direction they last moved — expected
  given TASK-041's single static idle pose (Forbidden scope explicitly
  excluded directional facings). Recorded as new backlog row **B-052**, not
  designed here.
- Selection still feels fiddly; Dave suggested (hedged, "possibly") a hover
  highlight on a selectable agent before clicking, so it's clear a click
  will select it. Recorded as new backlog row **B-053**, not designed here.

## Outcome (2026-09-17)

Live-checked kenney.nl (not assumed from memory, per the task's own central
decision): downloaded and inspected both `isometric-blocks` and
`isometric-miniature-prototype`. Chose **Isometric Miniature Prototype**
(v2.3, 2019-02-15, CC0) alone — its `Isometric/` subfolder (floor, block,
crate, wall, stairs, doorway, fence, ...) and `Characters/Human/` subfolder
are shipped together as one coherent family, so no second pack was needed
(the task's fallback "two packs from the same family" didn't apply). Picked
`floor_N.png` (open ground), `block_N.png` (a full 1x1 cube — impassable),
`crate_N.png` (a windowed crate — passable-but-opaque cover), and
`Human_0_Idle0.png` (one idle pose, both sides — no directional facings or
animation, per Forbidden scope). Copied into new
`src/CommandoWar.Client.Godot/art/` as `terrain_floor.png`,
`terrain_block.png`, `terrain_crate.png`, `agent_human.png`, plus
`LICENSE-THIRD-PARTY.md` naming the exact pack, version, source URL, and CC0
text, with an explicit note that this is third-party placeholder dev art,
not the original Commando IP.

`DrawItem` (`Core/IClientScene.fs`) gained one new primitive field,
`TextureId` (ADR-0004's interop idiom: primitives only). Meaningful only for
`Kind = 0` (terrain): `0` = floor (still elevation-tinted via `R`/`G`/`B`,
unchanged logic), `1` = block (impassable), `2` = crate (passable-but-opaque)
— `RenderShared.buildTerrainItems` sets it from the same
`Terrain.passable`/`Terrain.opaque` branches that already existed, no new
terrain state. Block/crate now render at full colour (`R=G=B=1`) since their
own Kenney art already reads distinctly; only the floor still needs a tint
for elevation. `Kind = 1` items keep `TextureId = 0` (unused) — friendly/
hostile is still read from `R`/`G`/`B` via `RenderShared.agentColor`,
unchanged.

`FSharpSceneHost.cs`: loads all four textures once via `static readonly`
fields (`GD.Load<Texture2D>`), never per-frame. `_Draw`'s terrain branch
(`DrawTerrainTile`) draws the whole padded Kenney canvas at a fixed on-screen
width (`TileW`) with height derived from the texture's own aspect ratio,
bottom-anchored at the same point the old `DrawDiamond` used — the flat floor
tile still lands at exactly `TileW x TileH`; a block/crate's extra height
rises above it, into the cell behind. The agent branch only replaces a real,
full-opacity agent (`A >= 0.99`, `TryHitAgentCircle`'s own existing
precedent for "this is a real agent, not an overlay marker"): the figure is
cropped from its own padded canvas (`AgentSourceRect`, found from the PNG's
own alpha bounds, not guessed) and drawn foot-anchored at the same point,
scaled from `DrawItem.Radius` the same way the old circle's diameter was — so
click hit-testing (`TryHitAgentCircle`, unchanged) still matches what is
drawn. A translucent `Kind = 1` item (selection halo, hover/pending/committed
route dots) still draws the original plain circle + white ring — it was
never meant to look like an agent, and reskinning it wasn't asked for.

Verified for real through the Godot 4.7.2 editor: re-imported the four new
PNGs (`--editor --headless --quit --path .`); both `SnapshotDemo.tscn` and
`CommandDemo.tscn` `--selfcheck` hashes unchanged (`0x11B06E6EDE0C52E3`,
`0x649FA4D08E2931CA`) — confirms this is genuinely render-only. Windowed
`--screenshot` evidence for both scenes shows a 3D-shaded block, a windowed
crate, elevation-tinted floor, and blue/red human figures, all clearly
distinct — committed as `docs/evidence/task-041-placeholder-art-snapshot.png`
and `docs/evidence/task-041-placeholder-art-command.png`. One deviation found
during implementation, not in the drafted plan: the first screenshot attempt
used a stale `Debug`-config build (Godot's windowed/editor run uses `Debug`
by default, not `Release`) and showed the old flat shapes — caught by
inspecting the screenshot, fixed by also running `dotnet build ... -c Debug`
before capturing (now in the README's run instructions for this task).

`dotnet build CommandoWar.slnx -c Release`: unaffected, `0/0`. `dotnet build
src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx` in both `-c
Release` and `-c Debug`: `0/0`. `dotnet test CommandoWar.slnx -c Release`:
unaffected, `297/297`. `git status --porcelain`: matches this task's allowed
scope (new `art/` directory, the four `Core/`/`src/FSharpSceneHost.cs`
render-side files, docs).

`src/CommandoWar.Client.Godot/README.md` gains a new section and an updated
file-layout table. `docs/11_BACKLOG.md` B-034 row moved to `review`
(pending Dave's acceptance, the TASK-040 precedent for the review label while
awaiting sign-off).

Full detail: `docs/ledger/2026-09-17-TASK-041-placeholder-art-set.md`.

## Objective

Replace the flat-colour diamonds and circles `FSharpSceneHost.cs` currently
draws for terrain and agents with a small, coherent set of free (CC0) Kenney
isometric assets, so the client's visual presentation can be judged for real
(does the isometric layering read correctly, does the viewport and camera
work at a realistic tile count, do units read clearly against terrain) ahead
of committing to a production art pipeline.

## Why this task exists

B-034's dependency (B-027) is `done` (TASK-039). docs/01_ADVERSARIAL_REVIEW.md
section 13 explicitly sanctions this: "Use crude geometric or purchased
placeholder assets for the graybox." docs/06_CONTENT_AND_PRESENTATION.md
section 9: "Use placeholders until the command loop passes its headless
gate" — G3 (command loop headless) passed 2026-09-16, so placeholders are
explicitly in scope now, not deferred further. Dave flagged this directly
(2026-09-17): the simulation is advanced enough that seeing it rendered with
real-looking tiles and units, rather than primitive shapes, gives useful
feedback and confidence, separate from and not blocking whichever P4 task is
active.

## Scope clarification: what this task does not deliver

Dave's request also mentioned wanting to see "line of sight/fire working" in
the Godot view. That is **not** this task's scope — it is B-029 proper
("Render the `DiagnosticFrame` in Godot"), still `proposed`, blocked on
nothing but not yet built. This task only replaces the *look* of terrain and
agents already drawn by `SnapshotDemo.tscn`/`CommandDemo.tscn`; it adds no
new overlay rendering. Flagged here so the screenshot evidence isn't
mistaken for more than it is.

## Central decision needed before implementation

**Not yet confirmed — resolve at implementation time, not from memory.**
Kenney's catalogue changes; which specific pack (and its exact CC0 licence
text / attribution file, if Kenney's site names one) best fits the existing
44x22 diamond-tile isometric projection needs a live check of kenney.nl
(or Kenney's itch.io page) rather than a name recalled here. Candidates to
evaluate: an isometric tile/block pack for terrain, a separate top-down or
isometric miniature/army-style pack for unit sprites. If no single pack
covers both terrain and units coherently, prefer two packs from the same
Kenney "family" (consistent line weight and palette) over mixing sources.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `docs/01_ADVERSARIAL_REVIEW.md` section 13
- `docs/06_CONTENT_AND_PRESENTATION.md` sections 8-9
- `docs/11_BACKLOG.md` row B-034
- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` per-concern table ("Render
  loop | C# `_Draw`, view model from F#") — texture selection and `Draw*`
  calls stay in the C# host; F# still only supplies `DrawItem`s
- `src/FSharpSceneHost.cs` (`_Draw`, `DrawDiamond`, the `Kind` dispatch this
  task extends to draw a texture instead of/alongside a flat shape)
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`,
  `DrawItem` (whether `Kind`/existing fields are enough to select a texture,
  or a new field is needed — see "Allowed scope")
- Kenney's licence page (CC0 — verify current wording live, do not assume)

## Dependencies

- B-027 (done, TASK-039).

## Allowed scope

- New `content/art/` (or `src/CommandoWar.Client.Godot/art/`, whichever this
  repo's Godot import conventions favour on inspection) holding the imported
  Kenney texture files, plus a `content/art/LICENSE-THIRD-PARTY.md` (or
  equivalent) naming the exact pack(s), version, source URL, and licence.
- `DrawItem` may gain one new primitive field (e.g. `TextureId: int`) if
  `Kind` alone can't distinguish enough visual variants (e.g. passable vs.
  impassable vs. cover terrain, friendly vs. hostile agent) — primitives
  only, the existing interop idiom.
- `FSharpSceneHost.cs` `_Draw`: replace `DrawDiamond`/plain `DrawCircle` with
  `DrawTextureRect`/`DrawTexture` calls keyed on `Kind`/`TextureId`, loaded
  once (e.g. in `_Ready` or a static cache), not per-frame. This is exactly
  the "screen<->cell projection arithmetic + `Draw*` calls" ADR-0004 already
  permits in the shim.
- `DemoRenderScene.fs` / `CommandDemoScene.fs` (or the shared `RenderShared.fs`
  from TASK-040, if merged first): update terrain/agent `DrawItem`
  construction to set the new field(s) distinguishing visual variants
  already computed today (passable/impassable/opaque/elevation, friendly/
  hostile) — no new terrain or agent *state*, only richer draw-item tagging
  of state that already exists.
- `docs/evidence/task-041-placeholder-art.png` (committed screenshot).
- `src/CommandoWar.Client.Godot/README.md`, `docs/11_BACKLOG.md`,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Forbidden scope

- No new terrain classes, agent types, or authoritative state — this is a
  render-only reskin of state that already exists.
- No `CommandoWar.Sim`/`CommandoWar.Headless` change.
- No B-029-proper overlay rendering (developer perception/appraisal/fire
  overlays in Godot) — see "Scope clarification" above.
- No animation, no directional facings, no sprite atlas pipeline (docs/06
  section 9's "production hypothesis" pipeline stays out of scope; this task
  uses static Kenney sprites as-is).
- No change to the original-title/branding constraint
  (`PROJECT_STATE.yaml fixed_constraints`, `AGENTS.md` "copied historical
  assets are out of scope"): Kenney/CC0 packs are third-party *placeholder*
  dev assets, not the original Commando franchise's IP, and are not intended
  to ship in a public release — record this distinction explicitly in the
  new licence file so it isn't ambiguous later.
- Nothing under `src/_scratch/`, `bench/`, `content/benchmarks/BASELINE.md`.

## Required work

1. Identify a specific Kenney pack (or two) live from kenney.nl, matching the
   existing isometric diamond-tile projection; record the exact pack name,
   version, and licence text.
2. Import the needed subset of textures (not the whole pack) into the Godot
   project; write the licence/attribution file.
3. Extend `DrawItem`/`_Draw` to draw textures keyed on the existing terrain/
   agent state already distinguished today.
4. Verify visually: passable/impassable/opaque terrain and friendly/hostile
   agents are each visually distinct and legible at the current camera scale.
5. Update `README.md`, backlog/ledger/state.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] Terrain and agents render with real sprite art, not flat shapes, in
      both `SnapshotDemo.tscn` and `CommandDemo.tscn` (or whichever exists
      at implementation time).
- [x] Passable/impassable/opaque terrain remain visually distinguishable
      (docs/06 "status indicators that do not rely on colour alone" —
      texture shape, not colour alone, should carry this).
- [x] Friendly/hostile agents remain visually distinguishable.
- [x] Licence file names the exact pack(s), version, source, and licence
      text; confirms CC0 (or equivalent permissive) status.
- [x] `--selfcheck` hashes for existing scenes are unchanged (render-only
      change, no state change).
- [x] `--screenshot` evidence committed.
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless` change.
- [x] Required documentation updated.

## Required verification

- `dotnet build` both `.slnx` files: unaffected, `0/0`.
- Godot editor import + `--selfcheck` for every existing scene (hash
  unchanged) + `--screenshot`.
- `git status --porcelain`: matches this task's allowed scope.

## Documentation updates

- This task file's Outcome/Review sections.
- `src/CommandoWar.Client.Godot/README.md`.
- `docs/11_BACKLOG.md` B-034 row.
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Additive and render-only: new art files, a licence file, and a `_Draw`
change keyed on state that already exists. No authoritative state or hash
change (confirmed by the unchanged `--selfcheck` hashes). Revertible with
`git revert` in one step; deleting `content/art/`/the licence file and
reverting `_Draw` fully removes it.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
