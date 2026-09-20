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
| Initial hash (tick 0) | `0x0F0AB334397C28EC` |
| Final hash (tick 6) | `0xF1EF5DFCAB80A727` |
| Domain events | 27 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x60EC4A2814FD8C7F` |
|    2 | `0x2DF77F0D4B7B35A7` |
|    3 | `0x78A2FF12728DEBF7` |
|    4 | `0x0197521154E49C0F` |
|    5 | `0xA61947CD0707BC67` |
|    6 | `0xF1EF5DFCAB80A727` |
