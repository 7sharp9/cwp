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
| Initial hash (tick 0) | `0xC0A406D7498F3EA7` |
| Final hash (tick 5) | `0x1F2041AE01BE7EE4` |
| Domain events | 2 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xD241785870DC7E20` |
|    2 | `0xBDCA82607D9D457D` |
|    3 | `0xB60303F48FFD72CE` |
|    4 | `0x3EF7F47D85D548CB` |
|    5 | `0x1F2041AE01BE7EE4` |
