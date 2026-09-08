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
| Initial hash (tick 0) | `0x8E466E50354728D4` |
| Final hash (tick 8) | `0x1899BC0BD5F36318` |
| Domain events | 7 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x8DF6701600067440` |
|    2 | `0x7422ADF25AD5B738` |
|    3 | `0xF15F75350C514CA6` |
|    4 | `0xF5A963517003F8E2` |
|    5 | `0x10C2B1B60660BE5E` |
|    6 | `0x44CC840BB2B54689` |
|    7 | `0x8C5512FF786A7C57` |
|    8 | `0x1899BC0BD5F36318` |
