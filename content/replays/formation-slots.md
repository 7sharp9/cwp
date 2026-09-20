# Replay corpus entry: formation-slots

Two friendly agents in a two-slot "wedge" formation, both ordered to the identical nominal cell (5,5) at tick 1. Each resolves its own real MoveTo destination as that shared anchor plus its own authored slot offset (Appraisal.resolveFormationTarget), landing on (4,5) and (6,5) instead of colliding on (5,5).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `formation-slots.cwreplay` |
| Initial state | Corpus formation-slots scenario (8 x 8, seed 20260904, 2 friendlies, 1 formation) |
| Tick count | 12 |
| Initial hash (tick 0) | `0xC16CF876AC742EE2` |
| Final hash (tick 12) | `0x2C1E10709A627773` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xE5C8701DFE1A464F` |
|    2 | `0x08838D76CF735490` |
|    3 | `0x96ED07ED5193FFD5` |
|    4 | `0xABCA82F8FA6D2352` |
|    5 | `0x342632DF5AFA3097` |
|    6 | `0x8098D5F49FEA48C4` |
|    7 | `0x3A8CCA2E6CF627C9` |
|    8 | `0x7FD6346020E896C8` |
|    9 | `0x4BEAF2C215700B94` |
|   10 | `0x503EEFF95995777D` |
|   11 | `0xEFB4DC796670949A` |
|   12 | `0x2C1E10709A627773` |
