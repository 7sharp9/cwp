# Replay corpus entry: wall-detour

One friendly agent at (1,3) ordered to (10,3) with an impassable wall at x=5, rows 0..6. Pathfinding.findWithin routes it around the gap at rows 7..8 and the executor advances one cell per tick (docs/04 section 8 steps 2, 4, 5).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `wall-detour.cwreplay` |
| Initial state | Corpus wall-detour scenario (12 x 9, seed 20260904) |
| Tick count | 24 |
| Initial hash (tick 0) | `0xBD5BD766CE0F2B95` |
| Final hash (tick 24) | `0xE69D55765291CB7A` |
| Domain events | 22 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x112A91FB3BD3C581` |
|    2 | `0x0524E2A5FE1AB293` |
|    3 | `0x2677BDF877CEBDE1` |
|    4 | `0x2EFE90857254F901` |
|    5 | `0xD18EF521DC07C915` |
|    6 | `0x4AF19F54B59944F5` |
|    7 | `0x7B2C04AFF7EED869` |
|    8 | `0x61F345AA7606CB87` |
|    9 | `0x2931DF1506AD5EC9` |
|   10 | `0xB656B53A78500E8B` |
|   11 | `0x17A8172A599C85BD` |
|   12 | `0x71D5A20A14DE3A9B` |
|   13 | `0x151E6EEBFE4EED49` |
|   14 | `0xA75E49A288F4BF2B` |
|   15 | `0xF60B4A52B542FDF9` |
|   16 | `0xC6ACA71201EABF07` |
|   17 | `0xC1F580A37B2566A1` |
|   18 | `0xC8AAE3218834B6E8` |
|   19 | `0x5645777DD2933F85` |
|   20 | `0x6AA27A45D7F00056` |
|   21 | `0xCF4D3F6DF471B393` |
|   22 | `0x20E47D9AE48E040C` |
|   23 | `0x6D416C443B681109` |
|   24 | `0xE69D55765291CB7A` |
