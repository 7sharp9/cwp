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
| Initial hash (tick 0) | `0x0B605BD91AE8373E` |
| Final hash (tick 8) | `0x18F07827715919C2` |
| Domain events | 9 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x47DFC87775F5CDF2` |
|    2 | `0xEE553FB18BE159D2` |
|    3 | `0x9A48A9851B3E3DA0` |
|    4 | `0x3EC8BA90FC45DAEC` |
|    5 | `0x198DFF7A06CC5E80` |
|    6 | `0xD53EBCC231F6F479` |
|    7 | `0x79F7A81049C5A0AB` |
|    8 | `0x18F07827715919C2` |
