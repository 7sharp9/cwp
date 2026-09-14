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
| Initial hash (tick 0) | `0xEDD658A35EF0D372` |
| Final hash (tick 24) | `0xB3F47BCA296B37B1` |
| Domain events | 22 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x22658F7318DC25C2` |
|    2 | `0xBA50645A6E1D01EC` |
|    3 | `0xCD621CB41F311B0A` |
|    4 | `0x9010EB9A41B06F9A` |
|    5 | `0xC79226AE91E897B6` |
|    6 | `0x28C89BEBB5715B3E` |
|    7 | `0x35B8399557BD418A` |
|    8 | `0x208751844C147D90` |
|    9 | `0xB80E0DE883D0AEEA` |
|   10 | `0x5678C48157AD7364` |
|   11 | `0xE416B1A5DAA18E5E` |
|   12 | `0x0F8AC58FDE330074` |
|   13 | `0xE4399ACD17072092` |
|   14 | `0xBFEE7DF99CFAC85C` |
|   15 | `0x9B4413D5CAEDD8AA` |
|   16 | `0x3F3AD6F19FFC1A00` |
|   17 | `0xADB4AF67DF542372` |
|   18 | `0xE0A90B436F369B5F` |
|   19 | `0xEEE15D62E93517BA` |
|   20 | `0x0E0FC31DD8D5500D` |
|   21 | `0x4122B4EB45281BF8` |
|   22 | `0x91EE81FB2A65BA93` |
|   23 | `0x0DC4449B1CE7DAFE` |
|   24 | `0xB3F47BCA296B37B1` |
