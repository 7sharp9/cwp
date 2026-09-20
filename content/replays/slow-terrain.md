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
| Initial hash (tick 0) | `0xEA292AEB436D143C` |
| Final hash (tick 8) | `0xF0A010B8A403B158` |
| Domain events | 9 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x6FACA55C54E22258` |
|    2 | `0xBDE39807CCDD8F58` |
|    3 | `0xAFE4004C43703B7E` |
|    4 | `0x4AC0165F5DCB2DE2` |
|    5 | `0x14F7988D641316D6` |
|    6 | `0x0CFBEBD2689BED9F` |
|    7 | `0xFF6D63F4FC262BD9` |
|    8 | `0xF0A010B8A403B158` |
