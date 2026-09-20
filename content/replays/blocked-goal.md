# Replay corpus entry: blocked-goal

One friendly agent at (1,4) ordered to (5,4), a passable cell ringed by impassable cells. Pathfinding returns NoPath, so the executor emits MovementBlocked and clears the destination; the remaining ticks are rest (the agent does not retry).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `blocked-goal.cwreplay` |
| Initial state | Corpus blocked-goal scenario (8 x 8, seed 20260904) |
| Tick count | 5 |
| Initial hash (tick 0) | `0x4CDCE0037E91B932` |
| Final hash (tick 5) | `0xD785EB611DACA269` |
| Domain events | 2 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xEB12F1DDFE03AAE5` |
|    2 | `0xFF844CC6AB95274C` |
|    3 | `0xDC4BBEDB943E3443` |
|    4 | `0xDD370FA4CA0D72FA` |
|    5 | `0xD785EB611DACA269` |
