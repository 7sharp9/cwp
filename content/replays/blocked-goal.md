# Replay corpus entry: blocked-goal

One friendly agent at (1,4) ordered to (5,4), a passable cell ringed by impassable cells. Pathfinding returns NoPath, so the executor emits MovementBlocked and clears the destination; the remaining ticks are rest (the agent does not retry).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `blocked-goal.cwlog` |
| Initial state | Corpus blocked-goal scenario (8 x 8, seed 20260904) |
| Tick count | 5 |
| Initial hash (tick 0) | `0xA0938349D85E1A14` |
| Final hash (tick 5) | `0x76ABFA0FFEE03CD5` |
| Domain events | 2 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xEDB6797FCE7DC1C1` |
|    2 | `0x03D8271EB149346E` |
|    3 | `0xB1CFA70B079FC7F3` |
|    4 | `0x8EF2333B2559D6D8` |
|    5 | `0x76ABFA0FFEE03CD5` |
