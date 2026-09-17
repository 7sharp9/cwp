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
| Initial hash (tick 0) | `0x6D96573C2F78B86F` |
| Final hash (tick 8) | `0xD70F3BDBF21A0A2B` |
| Domain events | 9 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x7FC1A79F141FF913` |
|    2 | `0x9D86F22D84903383` |
|    3 | `0x06C851565FE9CE1D` |
|    4 | `0xED1B8D753E0DE7D1` |
|    5 | `0x69AB34C64391446D` |
|    6 | `0x6E2F259E28936684` |
|    7 | `0x2707E7C2E09D26A2` |
|    8 | `0xD70F3BDBF21A0A2B` |
