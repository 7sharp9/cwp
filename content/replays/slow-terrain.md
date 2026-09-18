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
| Initial hash (tick 0) | `0xBEB19062818DB3E2` |
| Final hash (tick 8) | `0x3EB16FFB01C13786` |
| Domain events | 9 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xAE21CE7E3A828A2E` |
|    2 | `0xE9C71BD0FCD0FD86` |
|    3 | `0xBC3917C8AC97452C` |
|    4 | `0x5451A799D42FA888` |
|    5 | `0xC92E655747FA6C24` |
|    6 | `0x6CC240EF24F2AFCF` |
|    7 | `0x9A9A355F5ABE0B3D` |
|    8 | `0x3EB16FFB01C13786` |
