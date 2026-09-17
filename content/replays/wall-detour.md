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
| Initial hash (tick 0) | `0x1F5FD0A0A92FB7CA` |
| Final hash (tick 24) | `0x963C71BAD5A35267` |
| Domain events | 22 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x5208BDBEFEE1B49A` |
|    2 | `0x839350892EF8DB88` |
|    3 | `0xC3C4CF7142C438BA` |
|    4 | `0xFB7913F26CA5275A` |
|    5 | `0x5D2C9F3F58CB535E` |
|    6 | `0xE214000FACBC33F6` |
|    7 | `0x6E1662124B07D37A` |
|    8 | `0x6F7370D7134030B4` |
|    9 | `0x8887C1F933A4D182` |
|   10 | `0xF3A538A54E4B4280` |
|   11 | `0xE384B52A9C5B833E` |
|   12 | `0x057469DAEEE5B988` |
|   13 | `0x2499E96AAFB8682A` |
|   14 | `0xD10660CA4C8DF438` |
|   15 | `0x6264C1BF7F8747DA` |
|   16 | `0x9B35EFA95D8066A4` |
|   17 | `0xB9B0832D2E26B53A` |
|   18 | `0xE1D1B32CF7AF76A1` |
|   19 | `0x611B0BBBBB5B8942` |
|   20 | `0x6797926C01BC9453` |
|   21 | `0x1CB8C315E051C434` |
|   22 | `0xAFFFE62EDC45BEE5` |
|   23 | `0xB6A893A181323A36` |
|   24 | `0x963C71BAD5A35267` |
