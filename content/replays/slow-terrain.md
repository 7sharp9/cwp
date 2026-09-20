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
| Initial hash (tick 0) | `0x19C6246CCA0FEE1A` |
| Final hash (tick 8) | `0xFBBA35F29C43C69E` |
| Domain events | 9 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x0DC12036718F476E` |
|    2 | `0x781BF46DCDD28086` |
|    3 | `0xE23E64D78DB0832C` |
|    4 | `0x8FD1FE34704F4300` |
|    5 | `0x9A4BF92C2D2D82BC` |
|    6 | `0xB8225BA638A3BB83` |
|    7 | `0xC92D9BA2E79D8B63` |
|    8 | `0xFBBA35F29C43C69E` |
