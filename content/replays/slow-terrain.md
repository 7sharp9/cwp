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
| Initial hash (tick 0) | `0xBD92C9B2CBF21236` |
| Final hash (tick 8) | `0x76CD78F7F3F125BA` |
| Domain events | 6 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xC460447C2F40E1C9` |
|    2 | `0xC08E8CC7F9166D39` |
|    3 | `0x0EBDD9D3C20763B3` |
|    4 | `0x2CEF66099D148867` |
|    5 | `0x79CED0A98BBCFBBB` |
|    6 | `0xF306F6202426FC90` |
|    7 | `0x0E356767335D321D` |
|    8 | `0x76CD78F7F3F125BA` |
