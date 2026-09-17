# Replay corpus entry: slow-terrain

One friendly agent at (0,0) ordered to (4,0); cell (1,0) costs 3 to enter (elsewhere Terrain.BaseMoveCost = 1). AgentState.Progress accumulates 1, 2, then reaches the threshold and the agent enters the cell on the third tick (TASK-018); every other cell is entered in the usual single tick.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `slow-terrain.cwreplay` |
| Initial state | Corpus slow-terrain scenario (8 x 8, seed 20260904) |
| Tick count | 8 |
| Initial hash (tick 0) | `0x1624A49B7259102B` |
| Final hash (tick 8) | `0x128EDB3D5820291F` |
| Domain events | 9 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x204F1272B0C7DC47` |
|    2 | `0xA4C27FFE6527E5B7` |
|    3 | `0xA1269F21CD779A6D` |
|    4 | `0xAD7DEF5C8587D419` |
|    5 | `0xF5DE6F252B3FFAC5` |
|    6 | `0xB3381E409F30AD6A` |
|    7 | `0x0BA1BAEFC148284C` |
|    8 | `0x128EDB3D5820291F` |
