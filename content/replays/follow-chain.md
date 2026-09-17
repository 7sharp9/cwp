# Replay corpus entry: follow-chain

Three friendly agents in a line at (1,3), (2,3), (3,3), all ordered east to (11,3). Each tick the lead agent has a free cell ahead, so TASK-022's vacation-chain resolution lets the whole chain advance on the same tick; no agent reaches (11,3) within the run, so the chain flows every tick with no MovementObstructed.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `follow-chain.cwreplay` |
| Initial state | Corpus follow-chain scenario (12 x 9, seed 20260904) |
| Tick count | 6 |
| Initial hash (tick 0) | `0x16C3C457582FC726` |
| Final hash (tick 6) | `0xF9B5FE30D6BE558D` |
| Domain events | 27 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x59DE6789729E0E13` |
|    2 | `0x1263928AC695E48D` |
|    3 | `0x5749A51C794D2B2B` |
|    4 | `0xC4B09F34932D6621` |
|    5 | `0xD32AB36B6F5ADFE3` |
|    6 | `0xF9B5FE30D6BE558D` |
