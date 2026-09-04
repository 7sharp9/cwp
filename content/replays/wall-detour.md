# Replay corpus entry: wall-detour

One friendly agent at (1,3) ordered to (10,3) with an impassable wall at x=5, rows 0..6. Pathfinding.findWithin routes it around the gap at rows 7..8 and the executor advances one cell per tick (docs/04 section 8 steps 2, 4, 5).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `wall-detour.cwlog` |
| Initial state | Corpus wall-detour scenario (12 x 9, seed 20260904) |
| Tick count | 24 |
| Initial hash (tick 0) | `0x730C715C6DE01E16` |
| Final hash (tick 24) | `0x9B25511EA8D1AE8D` |
| Domain events | 19 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xCBB8C5B28AB3AC38` |
|    2 | `0x59BD978759AB0212` |
|    3 | `0xC8868DEB7AC12B18` |
|    4 | `0x933AC56ACE87F6F8` |
|    5 | `0x0D81DB3E9F082614` |
|    6 | `0x57CA0EFEB792322C` |
|    7 | `0xE8391CA38E3CC898` |
|    8 | `0x774861FA80492A86` |
|    9 | `0x6F802CCC4E22E770` |
|   10 | `0xC39F872D91FCD81A` |
|   11 | `0x26BA5C7E8BA2DAC4` |
|   12 | `0xD3A0528DDFF97B62` |
|   13 | `0x4C1E450E6F317178` |
|   14 | `0x44703BC9253180F2` |
|   15 | `0xDD01DA6A5B3200F8` |
|   16 | `0x03E42829A1207A16` |
|   17 | `0x25038A4E0AB35938` |
|   18 | `0xD958CB95E6BFA44B` |
|   19 | `0x22B1A0B3D48F15AE` |
|   20 | `0xD84F66B28D4A8201` |
|   21 | `0x785AEC5512B50524` |
|   22 | `0x29A409C6DC05FDF7` |
|   23 | `0x1600C102E3081CCA` |
|   24 | `0x9B25511EA8D1AE8D` |
