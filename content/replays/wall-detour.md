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
| Initial hash (tick 0) | `0x193B61A00C539853` |
| Final hash (tick 24) | `0xBF1E646AF4F53C56` |
| Domain events | 22 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xA8116A74FA5FC833` |
|    2 | `0x7AC30D43CD2A1B95` |
|    3 | `0x161D1664661F4913` |
|    4 | `0xA2F3030F87605BF3` |
|    5 | `0x39C2B3A62F86572F` |
|    6 | `0x9186C667B801CC57` |
|    7 | `0x0300BCF6DB14C9A3` |
|    8 | `0xDA84A53411B5A5C9` |
|    9 | `0xCEE7DC9C02B3256B` |
|   10 | `0x8B00C64E7C19DA1D` |
|   11 | `0xB73F2D1B9EF054AF` |
|   12 | `0x8A9555BE67D84DB5` |
|   13 | `0xE1576F06C76F9B83` |
|   14 | `0x1937B90AF5AB4785` |
|   15 | `0x404F2414513082A3` |
|   16 | `0x9796743B0B3A2B99` |
|   17 | `0x0C8B611F24436E53` |
|   18 | `0x0E67B747865EB66C` |
|   19 | `0x50509581081CAD4B` |
|   20 | `0xD7848844DEFF0A1A` |
|   21 | `0x08DA58231F4EA839` |
|   22 | `0xE604A32DDD111DC8` |
|   23 | `0xB854E9367AFFFA67` |
|   24 | `0xBF1E646AF4F53C56` |
