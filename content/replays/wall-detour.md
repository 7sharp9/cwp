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
| Initial hash (tick 0) | `0x5010D7A06758BFB3` |
| Final hash (tick 24) | `0x9446D7E488BDDD0A` |
| Domain events | 22 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x802942907DCB85C5` |
|    2 | `0xEE570863FBBE5397` |
|    3 | `0x578F823F58ACFE6D` |
|    4 | `0x560861B7602985FD` |
|    5 | `0x1365F499BD9DC9D1` |
|    6 | `0x4486804A06648E81` |
|    7 | `0x9896A04D328C5D65` |
|    8 | `0xD77D4F23874FF45B` |
|    9 | `0xD39788028A8335CD` |
|   10 | `0xC9F4A4215875912F` |
|   11 | `0x51232402AD2F88A1` |
|   12 | `0x68FD6763E38A19E7` |
|   13 | `0x2F3B26EC617CC54D` |
|   14 | `0x9F84EF7EEA6004EF` |
|   15 | `0x0D998891CB38BA05` |
|   16 | `0x274F6A5D3579CC8B` |
|   17 | `0x4822E94BC08E1F25` |
|   18 | `0xE75B0653A4581CF4` |
|   19 | `0x29A5D1CBCA6D40D3` |
|   20 | `0x261DF584179FB6E6` |
|   21 | `0x04D2CCBF9EF47B85` |
|   22 | `0x31CE6BE2EB4920A8` |
|   23 | `0x5AFD1F81264DC027` |
|   24 | `0x9446D7E488BDDD0A` |
