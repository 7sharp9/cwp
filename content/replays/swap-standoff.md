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
| Initial hash (tick 0) | `0x46783D8AC15EDFC9` |
| Final hash (tick 4) | `0xCE51F750A096D9B6` |
| Domain events | 14 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x7DF28FDFA0DEB9D9` |
|    2 | `0xBC3F3E640967179C` |
|    3 | `0x82C2E34C2B80CE03` |
|    4 | `0xCE51F750A096D9B6` |
