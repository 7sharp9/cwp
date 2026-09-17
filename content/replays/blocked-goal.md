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
| Initial hash (tick 0) | `0x3C39BF09A419FDE6` |
| Final hash (tick 5) | `0x7FCC7E7BBD0268A3` |
| Domain events | 2 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x6E84986C1232FD87` |
|    2 | `0x37580846857E1D3C` |
|    3 | `0x30920E40338CB199` |
|    4 | `0x222393B048BB7F86` |
|    5 | `0x7FCC7E7BBD0268A3` |
