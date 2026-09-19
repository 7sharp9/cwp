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
| Initial hash (tick 0) | `0x030D633EA769BBE6` |
| Final hash (tick 24) | `0x588D1B76BC421D09` |
| Domain events | 22 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x0DBAB0594E1171AA` |
|    2 | `0x9C94EA9CCC2855F8` |
|    3 | `0xF64C60A537E28A4A` |
|    4 | `0x095B6E6A873DC4AA` |
|    5 | `0x878B1DFB66AC4866` |
|    6 | `0x0F2F1F6EFC9229B6` |
|    7 | `0x16CEA4DAA669E5C2` |
|    8 | `0x4E20BE67ED128944` |
|    9 | `0xA4410377E4557D72` |
|   10 | `0x59E0F231CE8EA390` |
|   11 | `0xAF36D885CC2C1FBE` |
|   12 | `0xAEBEAF6E28441F10` |
|   13 | `0x8C5DDE81F6625DE2` |
|   14 | `0x33EC0F5929A5D580` |
|   15 | `0xB4CB8B27A0C18052` |
|   16 | `0x893576EA7E89E104` |
|   17 | `0x4D1EE6E0C9B72F2A` |
|   18 | `0x3DF26B3F9C928E6B` |
|   19 | `0x76C817D46504A826` |
|   20 | `0x4613AD556D6C342D` |
|   21 | `0x07FCAE8CA17FAD08` |
|   22 | `0x3086C6022152AC57` |
|   23 | `0x5AA993C0B219DA12` |
|   24 | `0x588D1B76BC421D09` |
