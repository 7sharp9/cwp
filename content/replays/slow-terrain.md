# Replay corpus entry: slow-terrain

One friendly agent at (0,0) ordered to (4,0); cell (1,0) costs 3 to enter (elsewhere Terrain.BaseMoveCost = 1). AgentState.Progress accumulates 1, 2, then reaches the threshold and the agent enters the cell on the third tick (TASK-018); every other cell is entered in the usual single tick.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `slow-terrain.cwlog` |
| Initial state | Corpus slow-terrain scenario (8 x 8, seed 20260904) |
| Tick count | 8 |
| Initial hash (tick 0) | `0x3C60E54D7AFA43FB` |
| Final hash (tick 8) | `0xE3F93C765A20C547` |
| Domain events | 6 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x0235E0D9450275C0` |
|    2 | `0xEF4FDD23DB5254A8` |
|    3 | `0x70E12A9014A94542` |
|    4 | `0x0EC6E1745468F57E` |
|    5 | `0x800035963B90BCEA` |
|    6 | `0x7C6F35F811BF208D` |
|    7 | `0x34786DB5054233B0` |
|    8 | `0xE3F93C765A20C547` |
