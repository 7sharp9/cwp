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
| Initial hash (tick 0) | `0xF499BD05E1570D2D` |
| Final hash (tick 24) | `0x23A672AC1206B006` |
| Domain events | 19 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xA1C1B524A5B0E4CB` |
|    2 | `0x51BD87987A4C3421` |
|    3 | `0xFCEEA4466F97F2E3` |
|    4 | `0x8BCEE125F1AE4953` |
|    5 | `0x9FD78F96D3A53207` |
|    6 | `0x0A7A62042CF0EB8F` |
|    7 | `0xED36887755C97533` |
|    8 | `0x898001D83861230D` |
|    9 | `0x823455B4F0EDB083` |
|   10 | `0x451E1792211178E9` |
|   11 | `0xCBF3B5A3A6AE2A6F` |
|   12 | `0x5D1D4A8540949229` |
|   13 | `0xB1DCB011401075AB` |
|   14 | `0xD916A7A4AF0C85E1` |
|   15 | `0xD901F2997787ABD3` |
|   16 | `0xCD92B39F7BFDD0FD` |
|   17 | `0x94E181B43E62B69B` |
|   18 | `0x59E3A48679B71158` |
|   19 | `0x6F80EA16B4450045` |
|   20 | `0xD4CF3927F8373A8A` |
|   21 | `0x6830F7B4668F4887` |
|   22 | `0x3D344E7783486504` |
|   23 | `0xC1DFEDB7A3C33EB1` |
|   24 | `0x23A672AC1206B006` |
