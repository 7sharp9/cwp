# Replay corpus entry: swap-standoff

Two friendly agents at (3,3) and (4,3), each ordered onto the other's cell. A two-agent position swap is blocked (TASK-022): neither agent is ever a first mover, so both emit MovementObstructed every tick and neither agent ever leaves its start cell (only the tick counter advances).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `swap-standoff.cwreplay` |
| Initial state | Corpus swap-standoff scenario (8 x 8, seed 20260904) |
| Tick count | 4 |
| Initial hash (tick 0) | `0x80BD9424CA4FD07F` |
| Final hash (tick 4) | `0xC8CD45FCB9335FE2` |
| Domain events | 14 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xAAA74409809B75B9` |
|    2 | `0xE746C7701A6011C0` |
|    3 | `0x42491E65AC8BD84B` |
|    4 | `0xC8CD45FCB9335FE2` |
