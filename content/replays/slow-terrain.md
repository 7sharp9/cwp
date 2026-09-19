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
| Initial hash (tick 0) | `0xDB5F3B1C33E48A11` |
| Final hash (tick 8) | `0xF004964D0E51024D` |
| Domain events | 9 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xD9009C0BBA4909B5` |
|    2 | `0x6C74D4CDBF75635D` |
|    3 | `0xF296AEB75C841BBF` |
|    4 | `0x6568DEC790D53EB3` |
|    5 | `0x918FB19AF53DE647` |
|    6 | `0xC6BF3948A777E6FC` |
|    7 | `0x9C5F0D4AD27285CE` |
|    8 | `0xF004964D0E51024D` |
