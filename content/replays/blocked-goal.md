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
| Initial hash (tick 0) | `0x4A1F96854EEAA69D` |
| Final hash (tick 5) | `0x79684033E39983B6` |
| Domain events | 2 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x9AD75BA07A1005EA` |
|    2 | `0xF9D4B72488DD0D83` |
|    3 | `0x260786249A64962C` |
|    4 | `0x8DBC063D8F2A69B5` |
|    5 | `0x79684033E39983B6` |
