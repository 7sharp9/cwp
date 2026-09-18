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
| `agent_human.png`        | `Characters/Human/Human_0_Idle0.png`                         | Agents, both sides (tinted per `DrawItem.R/G/B`, friendly/hostile from `RenderShared.agentColor`) |

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
