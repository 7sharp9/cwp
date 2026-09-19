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
| Initial hash (tick 0) | `0x2C29CABDBCEE14B1` |
| Final hash (tick 12) | `0x5B42A4AD5A9A144F` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xD5CBD9A9DD1058F3` |
|    2 | `0x1A4487421FD83B80` |
|    3 | `0xBE9C467C7DCAB1DD` |
|    4 | `0x2270F924883859BE` |
|    5 | `0xA66EBB6C84FE223B` |
|    6 | `0x344D15F0E8046814` |
|    7 | `0x4F4511A457474641` |
|    8 | `0x0664463608AF78B0` |
|    9 | `0x65B1FEE0B3C1B734` |
|   10 | `0x91535A88D10814E1` |
|   11 | `0xA6E20817D3F47ECA` |
|   12 | `0x5B42A4AD5A9A144F` |
