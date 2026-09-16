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
| Initial hash (tick 0) | `0x2F5B138AC4197A25` |
| Final hash (tick 8) | `0x433A6F571125E8D1` |
| Domain events | 9 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xCAC8D863BDF8CAF1` |
|    2 | `0x3ED8EA6AA1DF1389` |
|    3 | `0x88CF4C5784C3C187` |
|    4 | `0xE0280DC5EE1555DB` |
|    5 | `0xC02F1E929E26BFD7` |
|    6 | `0x13CDA325B5C44A8E` |
|    7 | `0xC3517E35F2608A4C` |
|    8 | `0x433A6F571125E8D1` |
