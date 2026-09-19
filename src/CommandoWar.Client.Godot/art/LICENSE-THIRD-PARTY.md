# Third-party art

## Kenney "Isometric Miniature Prototype" (v2.3, 2019-02-15)

Source: https://kenney.nl/assets/isometric-miniature-prototype
License: Creative Commons Zero (CC0) — https://creativecommons.org/publicdomain/zero/1.0/
No attribution required; credited here anyway per Kenney's request.

Files used (renamed on import, otherwise unmodified):

| File in this directory  | Source file (pack's `Isometric/` or `Characters/Human/`) | Used for |
|--------------------------|------------------------------------------------------------|----------|
| `terrain_floor.png`      | `Isometric/floor_N.png`                                     | Passable, non-opaque terrain (elevation-tinted) |
| `terrain_block.png`      | `Isometric/block_N.png`                                     | Impassable terrain |
| `terrain_crate.png`      | `Isometric/crate_N.png`                                     | Passable but opaque terrain (cover) |

### Agent facing (TASK-054, backlog B-052)

`agent_human.png` (`Characters/Human/Human_0_Idle0.png`) is retired: Dave's
original complaint was that the placeholder figure never turns to face its
direction of travel.

**First cut (superseded, kept here for the record):** live-checked kenney.nl
(downloaded and unzipped both this pack and "Isometric Miniature Dungeon")
and misread `Human_0..7` as 8 colour/character variants, each shipping its
own `Idle0` (no rotation) plus a `Run0..Run9`/`Pickup0..Pickup9` set —
concluding the pack had no rotating idle pose at all, and repurposing
`Human_0`'s 10 `Run` frames as 10 rotation bins instead, freezing on the
nearest one while stationary. Dave tried this live and reported it looked
no better; a second look then found the *rotation math* itself was also
wrong (binning the raw world-grid angle instead of the actual
screen-projected one). After fixing that too, Dave still correctly read
the result as "part of a running animation, not different angles of one
frame" — because that is exactly what `Human_0_Run0..Run9` are: one
direction's run *animation*, not a rotation set.

**Corrected (current):** Dave pointed directly at his own local copy of the
pack's `Information.png`, which documents the real convention neither
kenney.nl inspection caught: `Human_0..7` **are** 8 rotations of one idle
pose (`0 = N`, `1 = NE`, `2 = E`, `3 = SE`, `4 = S`, `5 = SW`, `6 = W`,
`7 = NW`, clockwise, screen-relative) — confirmed by cropping and comparing
all 8 `Human_N_Idle0.png` side by side, which show a clean rotating idle
figure, not animation-frame variation. Each `Human_N`'s own `Run0..Run9`
is that *direction's* run-cycle animation, unrelated to facing. Files below
replace the first cut's ten `Run`-cycle crops one-for-one at the same
paths.

Each file was `Human_N_Idle0.png` cropped to the shared rect
`(99, 323, 58, 138)` px (the union of all 8 frames' own alpha-channel
bounding boxes, found by inspection — a per-frame crop would misalign the
figure's anchor point frame to frame).

| File in this directory       | Source file                     | Used for |
|-------------------------------|----------------------------------|----------|
| `agent_human_facing0.png`     | `Characters/Human/Human_0_Idle0.png` | Facing bin 0 / `N` (`RenderShared.facingBin`) |
| `agent_human_facing1.png`     | `Characters/Human/Human_1_Idle0.png` | Facing bin 1 / `NE` |
| `agent_human_facing2.png`     | `Characters/Human/Human_2_Idle0.png` | Facing bin 2 / `E` |
| `agent_human_facing3.png`     | `Characters/Human/Human_3_Idle0.png` | Facing bin 3 / `SE` |
| `agent_human_facing4.png`     | `Characters/Human/Human_4_Idle0.png` | Facing bin 4 / `S` |
| `agent_human_facing5.png`     | `Characters/Human/Human_5_Idle0.png` | Facing bin 5 / `SW` |
| `agent_human_facing6.png`     | `Characters/Human/Human_6_Idle0.png` | Facing bin 6 / `W` |
| `agent_human_facing7.png`     | `Characters/Human/Human_7_Idle0.png` | Facing bin 7 / `NW` |

### Facing correction and run-cycle animation (TASK-056, backlog B-052)

Dave live-tested the corrected facing above and found it still roughly 30
degrees off, and a moving agent never animated (frozen on the idle pose
above for its whole transit). Re-examined `Information.png` directly again:
its diamond legend labels the tile's four *edges* `N`/`E`/`S`/`W` (not
vertices), and the geometry shows the previous rotation math had the
world-direction-to-pose assignment backwards (see `RenderShared.facingBin`'s
own doc comment and the task's ledger detail for the full derivation) —
fixed in `RenderShared.facingBin` alone, no art change needed for the
rotation fix itself.

For the run-cycle animation, all 80 `Human_0..7_Run0..9.png` frames were
added, and — because a moving agent must switch between the idle pose above
and a running frame without any visible jump in size — **all 88 files
(the 8 `agent_human_facing*.png` above and the 80 new
`agent_human_run*_*.png` below) were re-cropped to one shared, larger
global rect, `(66, 309, 124, 157)` px** (`x, y, width, height`; the union
of all 88 source frames' own alpha-channel bounding boxes), superseding the
smaller idle-only rect above. `agent_human_facing0..7.png` are unchanged in
content/meaning (same source `Human_N_Idle0.png`, same paths), only their
crop rect (and therefore pixel dimensions) changed.

| File in this directory        | Source file                          | Used for |
|--------------------------------|----------------------------------------|----------|
| `agent_human_run{0-7}_{0-9}.png` | `Characters/Human/Human_{N}_Run{F}.png` (`N` = direction 0-7, `F` = frame 0-9) | The `N` direction's run-cycle frame `F`, selected by `DrawItem.Cx2` (`RenderShared.runFrameIndex`) while an agent is moving |

## Kenney "Particle Pack" (v1.1, 2019-11-14)

Source: https://kenney.nl/assets/particle-pack
License: Creative Commons Zero (CC0) — https://creativecommons.org/publicdomain/zero/1.0/
No attribution required; credited here anyway per Kenney's request.

Checked live against kenney.nl (not assumed from memory): the Isometric
Miniature Prototype pack above has no combat-effect sprites and no wound/
death character pose (only Idle/Run/Pickup), so a second pack was needed for
backlog B-057's player-facing fire/impact feedback. Files resized from the
pack's original 512x512 to 96x96 (the on-screen sprite is ~20-26px; the
`terrain_*`/`agent_human` precedent already keeps source art close to its
display scale) and stripped of metadata, otherwise unmodified.

| File in this directory        | Source file (`PNG (Transparent)/`) | Used for |
|--------------------------------|-------------------------------------|----------|
| `effect_muzzle_flash.png`      | `flare_01.png`                      | Shooter-side flash for a fired shot (`DrawItem.Kind = 4`, `TextureId = 0`) |
| `effect_impact_hit.png`        | `spark_02.png`                      | Target-side effect for a shot that hit (`TextureId = 1`) |
| `effect_impact_miss.png`       | `smoke_03.png`                      | Target-side effect for a shot that missed (`TextureId = 2`) — a distinct shape, not a colour-only hit/miss tint |

## Kenney "Board Game Icons" (1.1, 2024-07-22)

Source: https://kenney.nl/assets/board-game-icons
License: Creative Commons Zero (CC0) — https://creativecommons.org/publicdomain/zero/1.0/
No attribution required; credited here anyway per Kenney's request.

Checked live against kenney.nl (downloaded and unzipped, not assumed from
memory) for TASK-048's XCOM-style order-mode HUD icons (backlog B-059).
Neither existing pack has command-vocabulary icons (the Isometric Miniature
Prototype pack is terrain/character art; the Particle Pack is fire effects
only), so a third pack was needed. Used at its native 64x64 (`PNG/Default
(64px)/`), unmodified.

| File in this directory   | Source file       | Used for |
|----------------------------|--------------------|----------|
| `hud_move.png`      | `arrow_right.png`         | `MoveTo` order-mode icon (the default mode) |
| `hud_hold.png`      | `shield.png`               | `Hold` order-mode icon |
| `hud_assault.png`   | `sword.png`                | `Assault` order-mode icon |
| `hud_withdraw.png`  | `arrow_counterclockwise.png` | `Withdraw` order-mode icon |
