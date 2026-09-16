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
| Initial hash (tick 0) | `0xC170703911B965D8` |
| Final hash (tick 8) | `0x450BF0F166B44B2C` |
| Domain events | 9 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x8E28A03E2FAD6D9C` |
|    2 | `0x0DC6629A2A5E7264` |
|    3 | `0x2A32E97C91FADA2E` |
|    4 | `0x1B9C8EFB88582E4A` |
|    5 | `0xDBFF408508B4AA8E` |
|    6 | `0xE25101EE2EE37C1F` |
|    7 | `0x4BB705E09C2B5831` |
|    8 | `0x450BF0F166B44B2C` |
