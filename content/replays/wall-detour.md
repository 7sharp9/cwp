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
| Initial hash (tick 0) | `0x77593FB729C860DC` |
| Final hash (tick 24) | `0xFDCF1B143A5D36BB` |
| Domain events | 22 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x17F02119C62EB04C` |
|    2 | `0x4F9C1DF24F7C205E` |
|    3 | `0x5ABD2F66EDBB2E04` |
|    4 | `0x0C8978AA41471D54` |
|    5 | `0xB83E9A49E95D28B8` |
|    6 | `0x8821ED4580C68D60` |
|    7 | `0xF564CDB7BF22BBB4` |
|    8 | `0x150F5F7929CBC44A` |
|    9 | `0x615FAF99C32F2434` |
|   10 | `0x35D8479A44FF20F6` |
|   11 | `0x7AFC3849E3DF9990` |
|   12 | `0x3DA9B851C3F9B976` |
|   13 | `0xF209F6D5F5DD9A5C` |
|   14 | `0xF97CA78C07CA7A4E` |
|   15 | `0xA9625A34692684D4` |
|   16 | `0x6379D0AC5A39B93A` |
|   17 | `0x9324142D9A7DC71C` |
|   18 | `0x20D6940717B891E9` |
|   19 | `0xDC532BB3591A92B4` |
|   20 | `0x293D4A1C111D4B2F` |
|   21 | `0x6EE8EBDA2F35FCC2` |
|   22 | `0x97FC6826D78E5C05` |
|   23 | `0x17C8AE9F7DAD1080` |
|   24 | `0xFDCF1B143A5D36BB` |
