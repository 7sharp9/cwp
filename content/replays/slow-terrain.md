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
| Initial hash (tick 0) | `0x46C7E80BB44131E1` |
| Final hash (tick 8) | `0x668DBD1E33D5BC0D` |
| Domain events | 9 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x105A522988ECE35D` |
|    2 | `0xBB521155F0F9043D` |
|    3 | `0x068BEB21EB7DD5CB` |
|    4 | `0x0AE828416E44757F` |
|    5 | `0xAD42F2E3C7B6F2E3` |
|    6 | `0x60331FE8EDCDEBC0` |
|    7 | `0xCE12EEDD69FD7902` |
|    8 | `0x668DBD1E33D5BC0D` |
