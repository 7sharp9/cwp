# Replay corpus entry: converging-routes

Agent 0 at (3,0) -> (3,7) crosses agent 1 at (0,3) -> (7,3); their routes meet at (3,3) on the same tick with no cell reservation. Pins today's no-reservation behaviour: B-011b re-pins this entry's hashes.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `converging-routes.cwlog` |
| Initial state | Corpus converging-routes scenario (8 x 8, seed 20260904) |
| Tick count | 12 |
| Initial hash (tick 0) | `0x1EDFC83E4A3E9C4D` |
| Final hash (tick 12) | `0x978ABAE727665277` |
| Domain events | 18 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xBF6D32ABC3B4A24E` |
|    2 | `0x942B69B9C0FCDBA5` |
|    3 | `0x2883EF41908099FC` |
|    4 | `0xDCADD796B4C8EC23` |
|    5 | `0x895F3DA4BD287712` |
|    6 | `0xD82A3DDE1CB3A0A9` |
|    7 | `0x36C415A0EFB2B292` |
|    8 | `0x56F077D522186D8B` |
|    9 | `0xB9379A100F540688` |
|   10 | `0x67E93EEEDD5325D9` |
|   11 | `0x6E7AE54D5A0D878E` |
|   12 | `0x978ABAE727665277` |
