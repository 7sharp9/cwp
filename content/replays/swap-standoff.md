# Replay corpus entry: swap-standoff

Two friendly agents at (3,3) and (4,3), each ordered onto the other's cell. A two-agent position swap is blocked (TASK-022): neither agent is ever a first mover, so both emit MovementObstructed every tick and neither agent ever leaves its start cell (only the tick counter advances).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `swap-standoff.cwlog` |
| Initial state | Corpus swap-standoff scenario (8 x 8, seed 20260904) |
| Tick count | 4 |
| Initial hash (tick 0) | `0x36334E44259BB6D6` |
| Final hash (tick 4) | `0xAEBB18C290AF465F` |
| Domain events | 10 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x7EFEFA56056A1994` |
|    2 | `0xA6DD946C5BEA055D` |
|    3 | `0x27D3E5463438875E` |
|    4 | `0xAEBB18C290AF465F` |
