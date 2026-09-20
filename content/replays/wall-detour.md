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
| Initial hash (tick 0) | `0x6C54A74A1EA36EFC` |
| Final hash (tick 24) | `0x5DD5FE598B55B889` |
| Domain events | 22 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x2044904040B89FFC` |
|    2 | `0xD44914C3E018E3DA` |
|    3 | `0x93F5DE2F05603B9C` |
|    4 | `0xBC0046F3EA69B5BC` |
|    5 | `0x229155F647BB8C90` |
|    6 | `0x43B807D1B0D8EEC8` |
|    7 | `0x2DC568553E2A314C` |
|    8 | `0x22B95A6923A29816` |
|    9 | `0x1370C835E6BB1194` |
|   10 | `0xEA017995C65F9602` |
|   11 | `0x282131B18DCD46A0` |
|   12 | `0xB17978AFBAB02ECA` |
|   13 | `0x93E755797880011C` |
|   14 | `0xDF1523BD9784279A` |
|   15 | `0xDDDB36D8EC7080CC` |
|   16 | `0x650E0C18859A97A6` |
|   17 | `0x9AB0C041885B9C7C` |
|   18 | `0x6C4F0AD90D5EC0B3` |
|   19 | `0xAE8DBC97BA4E7C54` |
|   20 | `0x077DE5D30ECDD765` |
|   21 | `0x68C72E915A9136C6` |
|   22 | `0x283CA00DF01D0287` |
|   23 | `0x055BE5E2530F6A08` |
|   24 | `0x5DD5FE598B55B889` |
