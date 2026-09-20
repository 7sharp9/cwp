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
| Initial hash (tick 0) | `0xD7ACAC0DFB97F1F8` |
| Final hash (tick 12) | `0x84B5356177AF68B6` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x66E5AC07EDDA0300` |
|    2 | `0xA1CE5A006A74F011` |
|    3 | `0x5D0B5F4E7FE9BE8A` |
|    4 | `0x9BEFC2B0FEE18C37` |
|    5 | `0x3F6CA1AEC33A3BF0` |
|    6 | `0x22BD92375D12D735` |
|    7 | `0x944DBA5361DB432E` |
|    8 | `0x9A8A62AB4ACC93DD` |
|    9 | `0x33D51B2F9EFD0619` |
|   10 | `0x20FF07521FD78EC8` |
|   11 | `0xBC6A327538AD4ACF` |
|   12 | `0x84B5356177AF68B6` |
