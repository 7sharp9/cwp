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
| Initial hash (tick 0) | `0x70DE45B0EA19DF19` |
| Final hash (tick 12) | `0xAA82FF57497F6853` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xFD01FE9798196F4D` |
|    2 | `0xE2FC2668C7546ECC` |
|    3 | `0x0DF17A880EED2DAB` |
|    4 | `0x03614553DF17964E` |
|    5 | `0xAD9F6503E6DD96AD` |
|    6 | `0xCD6BB8D5C1F954B8` |
|    7 | `0xFAD8A4BFB78410C7` |
|    8 | `0xA97A00AB50993974` |
|    9 | `0x3A2BD23968A2FD08` |
|   10 | `0xD29ADD6DFB0EE3F9` |
|   11 | `0x8171283DD32A45F2` |
|   12 | `0xAA82FF57497F6853` |
