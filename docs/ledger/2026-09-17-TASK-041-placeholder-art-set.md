## 2026-09-17 - TASK-041 - Minimal coherent placeholder art set

### Why

B-034's dependency (B-027) is `done` (TASK-039). Dave flagged this directly:
the simulation now has enough tactical state worth seeing rendered as more
than flat shapes, and docs/01/docs/06 both explicitly sanction placeholder
art now that G3 has passed. TASK-041 was drafted the same session as
TASK-040 but queued, not selected, until this session. Selected this session
via `AskUserQuestion` (alongside the next-P4-task choice and the B-051
design-fork decision): run now, before B-028, since it was already drafted
and unblocked.

### Central decision (the task's own, resolved live this session, not from memory)

Downloaded and inspected two live kenney.nl candidates:
`kenney_isometric-blocks.zip` (2016, terrain-only, no characters) and
`kenney_isometric-miniature-prototype.zip` (v2.3, 2019-02-15) — both CC0,
confirmed by reading each pack's own `License.txt`. Chose **Isometric
Miniature Prototype** alone: its `Isometric/` subfolder (floor, block,
crate, wall, stairs, doorway, fence, column, slope, ...) and
`Characters/Human/` subfolder are shipped together in one pack, so the
task's "prefer two packs from the same family" fallback never applied — one
pack covers both terrain and units. Verified each candidate PNG visually
(`Read` on the extracted files) before committing to a choice, not from the
kenney.nl page text alone (the page text doesn't list exact tile dimensions
or contents).

Picked, from `Isometric Miniature Prototype`:
- `Isometric/floor_N.png` — a flat grid tile, for passable/non-opaque ground.
- `Isometric/block_N.png` — a full 1x1x1 cube, for impassable terrain (tried
  `wall_N.png` first; rejected, since it's a thin multi-cell wall segment
  sized for a perimeter, not a single full-cell fill).
- `Isometric/crate_N.png` — a windowed crate, for passable-but-opaque cover.
- `Characters/Human/Human_0_Idle0.png` — a single blank humanoid mannequin,
  one idle pose, used for both sides (tinted via the existing
  `RenderShared.agentColor` friendly/hostile colour, no second texture
  needed).

All four source PNGs are `256x512`: a tall, bottom-anchored canvas (extra
vertical room for a stacked block/wall) with the actual tile/figure content
occupying only the bottom portion. Confirmed with a quick Python/Pillow
`getbbox()` check on the extracted files (not guessed): `floor_N`'s content
spans the full canvas width at `y=383..512` (a `256x129` diamond, matching
the existing `44x22`, `2:1` `TileW:TileH` ratio); the human figure's own
bbox is `(106,324)-(151,457)`, a `45x133` region well inside the padded
canvas, cropped rather than drawn at full-canvas scale (drawing the whole
canvas at tile scale would render the figure at only a few pixels wide).

### Changes

**New `src/CommandoWar.Client.Godot/art/`**: `terrain_floor.png`,
`terrain_block.png`, `terrain_crate.png`, `agent_human.png` (renamed copies
of the four source files above, pixels unmodified), plus
`LICENSE-THIRD-PARTY.md` naming the exact pack, version, source URL, CC0
licence text, and a table mapping each local filename back to its source
file and its use — with an explicit note (per Forbidden scope) that this is
third-party placeholder dev art, not the original Commando franchise's IP,
and not intended to ship in a public release.

**`Core/IClientScene.fs`**: `DrawItem` gains one new primitive field,
`TextureId: int` (ADR-0004's interop idiom — primitives only). Doc comment
updated: meaningful only for `Kind = 0` (terrain) — `0` floor, `1` block/
impassable, `2` crate/passable-but-opaque; for `Kind = 1` the field is
carried but unused by the host (agent vs. overlay-marker rendering is
decided by `A >= 0.99`, not `TextureId`).

**`Core/RenderShared.fs`** (`buildTerrainItems`): the existing
`Terrain.passable`/`Terrain.opaque` branches now also select `TextureId`
(`0`/`1`/`2`) alongside the colour they already computed. No new terrain
state or new branch — same three cases as before. Block/crate now pass
`R=G=B=1` (their own Kenney colour reads clearly without a tint); only the
floor case keeps its existing elevation-based shade.

**`Core/DemoRenderScene.fs`, `Core/CommandDemoScene.fs`**: every remaining
`DrawItem` literal (agent items in both scenes, the selection halo, and
`routeDots`) gets `TextureId = 0` (unused for `Kind = 1`, but the record is
no longer constructible without it) — mechanical, no behaviour change.

**`src/FSharpSceneHost.cs`**:
- Four `static readonly Texture2D` fields (three terrain + one agent),
  loaded once via `GD.Load<Texture2D>("res://art/...")` — never per-frame,
  shared across every `FSharpSceneHost` instance.
- `AgentSourceRect` — a `static readonly Rect2(106, 324, 45, 133)`, the
  human figure's own bounds found above, not guessed.
- `_Draw`'s terrain branch: new `DrawTerrainTile(pos, textureId, color)` —
  draws the whole padded canvas at a fixed on-screen width (`TileW`), height
  derived from the texture's own aspect ratio (`tex.GetSize()`), anchored so
  the canvas's own bottom edge lands at `pos.Y + TileH * 0.5f` (the same
  point `DrawDiamond` used for the diamond's bottom vertex) and centred on
  `pos.X`. The flat floor content (bottom ~25% of the canvas) therefore
  lands at exactly the old `TileW x TileH` diamond footprint; a block/crate's
  extra height rises above it into the cell behind, the expected isometric
  look for a raised object.
- `_Draw`'s agent branch: unchanged for a translucent item (`A < 0.99`,
  `TryHitAgentCircle`'s own existing "is this a real agent" threshold) —
  still `DrawCircle` + `DrawArc`. A real, full-opacity agent now calls new
  `DrawAgentFigure(agentPos, radius, color)`: crops `AgentSourceRect` from
  `AgentTexture` via `DrawTextureRectRegion`, sized from `radius * 2` (the
  old circle's diameter) with height from the crop's own aspect ratio,
  foot-anchored at `agentPos.Y + radius` (the old circle's bottom edge) so
  the figure stands where the circle used to sit. `TryHitAgentCircle` itself
  is untouched — it hit-tests against `DrawItem.Cx/Cy/Radius` geometry, not
  what is actually drawn, so click behaviour is unaffected by this task.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0/0` (unaffected).
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Release`
  - Result: `0/0`.
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: `0/0` (needed a second time after the screenshot deviation below).
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `297/297` (unaffected — no `CommandoWar.Sim`/`CommandoWar.Headless`
    change).
- Command: `"$GODOT" --editor --headless --quit --path .` (re-import after
  adding `art/`)
  - Result: clean; all four PNGs imported, `.import` sidecar files written.
- Command: `"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck`
  - Result: `MATCH expected final hash 0x11B06E6EDE0C52E3 at tick 20`, exit `0`
    — unchanged from TASK-039/040.
- Command: `"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck`
  - Result: `MATCH expected final hash 0x649FA4D08E2931CA at tick 20`, exit `0`
    — unchanged from TASK-040.
- Command: `"$GODOT" --path . scenes/SnapshotDemo.tscn -- --screenshot <path>`
  - Result: `docs/evidence/task-041-placeholder-art-snapshot.png` committed
    — shows a 3D-shaded orange block, three windowed crates, elevation-tinted
    floor, and blue/red human figures, all clearly distinct shapes.
- Command: `"$GODOT" --path . scenes/CommandDemo.tscn -- --screenshot <path>`
  - Result: `docs/evidence/task-041-placeholder-art-command.png` committed —
    same terrain/agent art, plus the selection halo and route-preview dots
    still rendering as plain circles (unchanged, as intended).
- Manual check: `git status --porcelain`
  - Result: matches this task's allowed scope (new `art/` directory, the
    `Core/`/`FSharpSceneHost.cs` render-side files, docs), plus the
    pre-existing unrelated `project.godot` `run/main_scene` diff, left
    untouched.

### Evidence

- `docs/evidence/task-041-placeholder-art-snapshot.png`.
- `docs/evidence/task-041-placeholder-art-command.png`.
- Both scenes' `--selfcheck` hash sequences, unchanged from TASK-039/040.

### Deviations and unresolved issues

- The first `--screenshot` attempt (windowed, non-`--headless` run of
  `SnapshotDemo.tscn`) still showed the old flat diamonds/circles. Cause:
  Godot's windowed/editor run loads the `Debug`-configuration build by
  default, not `Release` — only `-c Release` had been rebuilt at that point
  (for the `dotnet build`/`dotnet test` verification steps above), so the
  running scene was loading a stale pre-TASK-041 `Debug` DLL
  (`.godot/mono/temp/bin/Debug/CommandoWar.Client.Godot.dll`, timestamped
  before this session's edits). Fixed by also running `dotnet build ... -c
  Debug` before capturing; the README's run instructions for this section
  now build both configurations explicitly so this doesn't recur.
- Not built (Forbidden scope, as drafted): animation, directional facings, a
  sprite-atlas import pipeline, and B-029-proper overlay rendering
  (line-of-sight/fire in Godot) — all explicitly out of scope for this task.

### Documents updated

- `tasks/TASK-041-PLACEHOLDER-ART-SET.md` (`ready -> review`, Outcome
  section, acceptance criteria checked).
- `src/CommandoWar.Client.Godot/README.md` (new section, layout table).
- `docs/11_BACKLOG.md` B-034 row (`ready -> review`); B-051 row updated
  separately this session with the resolved sim-side-order-queue design
  decision (not part of this task's own scope).
- `docs/12_PROGRESS_LEDGER.md` (this row).
- `PROJECT_STATE.yaml`.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-17, "seems to work")
- Notes: two follow-ups raised, neither blocking this task's own acceptance
  criteria (both fall outside TASK-041's scope, which explicitly excluded
  directional facings and never touched selection UI): agents don't always
  visually face the direction they last moved (new backlog row B-052, not
  designed); selection still feels fiddly, and Dave suggested — hedged,
  "possibly" — a hover highlight on a selectable agent before clicking (new
  backlog row B-053, not designed). Recorded, not implemented, per Dave's
  explicit instruction to move to the next task afterward.
