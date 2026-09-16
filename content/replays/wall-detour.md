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
| Initial hash (tick 0) | `0x173E0F94C0D31FE1` |
| Final hash (tick 24) | `0xEC11F5CCEE317788` |
| Domain events | 22 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xFBC6481D82944695` |
|    2 | `0xFB64117F290A690B` |
|    3 | `0x15B84E8743B0B67D` |
|    4 | `0xB4E48BDC9D35144D` |
|    5 | `0x46CFF9B5C244F9A9` |
|    6 | `0x19E64467A03373C9` |
|    7 | `0xB5DA90BDCB96E105` |
|    8 | `0x4F90DEE1D1551AD7` |
|    9 | `0xC88E41A0FEBF569D` |
|   10 | `0x05E395628CCBC683` |
|   11 | `0x70BD584555926429` |
|   12 | `0x60081B62ABCD1ECB` |
|   13 | `0x26FFE6447777C50D` |
|   14 | `0xC2826077885A9953` |
|   15 | `0xE22EF5C3D4406D25` |
|   16 | `0x30B3E73862E74F27` |
|   17 | `0xF160888F822ED445` |
|   18 | `0x857DABF83E331842` |
|   19 | `0x38DB0E8FE1AAE531` |
|   20 | `0xB21676B6D8D5EF14` |
|   21 | `0xCF364E9DA0FC6D43` |
|   22 | `0x619237E1043D0DF6` |
|   23 | `0xED5AF8F347635EB5` |
|   24 | `0xEC11F5CCEE317788` |
