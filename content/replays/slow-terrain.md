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
| Initial hash (tick 0) | `0x44B4162107FAD641` |
| Final hash (tick 8) | `0x89B1E195CC131ADD` |
| Domain events | 9 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xA514574AE1F60CBD` |
|    2 | `0x80FD64EB92CB5DBD` |
|    3 | `0x5C5A2474E06C27FF` |
|    4 | `0x194A6E8DB989A903` |
|    5 | `0xD81629259E1EADDF` |
|    6 | `0x0BCF487A103F9346` |
|    7 | `0x371BEE1C3C625BA4` |
|    8 | `0x89B1E195CC131ADD` |
