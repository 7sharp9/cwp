# TASK-041: Minimal coherent placeholder art set (Kenney, CC0)

Status: ready (drafted 2026-09-17; not selected — TASK-040/B-026 is the
active task. Queued next per Dave's request to get real placeholder art in
soon, since the simulation now has enough tactical state worth seeing.)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete)
Size: M

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

- [ ] Terrain and agents render with real sprite art, not flat shapes, in
      both `SnapshotDemo.tscn` and `CommandDemo.tscn` (or whichever exists
      at implementation time).
- [ ] Passable/impassable/opaque terrain remain visually distinguishable
      (docs/06 "status indicators that do not rely on colour alone" —
      texture shape, not colour alone, should carry this).
- [ ] Friendly/hostile agents remain visually distinguishable.
- [ ] Licence file names the exact pack(s), version, source, and licence
      text; confirms CC0 (or equivalent permissive) status.
- [ ] `--selfcheck` hashes for existing scenes are unchanged (render-only
      change, no state change).
- [ ] `--screenshot` evidence committed.
- [ ] No `CommandoWar.Sim`/`CommandoWar.Headless` change.
- [ ] Required documentation updated.

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
