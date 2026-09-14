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
| Initial hash (tick 0) | `0x56632ADE6C5A6A0D` |
| Final hash (tick 4) | `0x451A91F69ADDBD96` |
| Domain events | 14 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x37808D1CA6B2A021` |
|    2 | `0x5D42A35C2BD3F6EC` |
|    3 | `0xA1020090C8287A13` |
|    4 | `0x451A91F69ADDBD96` |
