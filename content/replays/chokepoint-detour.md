# Replay corpus entry: chokepoint-detour

The identical setup to stalled-order-abandoned, but on open terrain (no wall) -- one friendly agent at (0,0) ordered east to (4,0), a second idle, permanently, at (2,0) (TASK-070, backlog B-069; docs/10 R-010, the live-agent chokepoint jam TASK-066/067/068 each independently found and left open). A genuine alternate route exists, so agent 0 detours around agent 1 (MovementRerouted) instead of stalling toward eventual abandonment, and reaches (4,0) for real by tick 6.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `chokepoint-detour.cwreplay` |
| Initial state | Corpus chokepoint-detour scenario (8 x 8, seed 20260904, 2 friendlies, open terrain) |
| Tick count | 10 |
| Initial hash (tick 0) | `0x745B1AE1EC2F01C1` |
| Final hash (tick 10) | `0x5876C1280DDAE2CB` |
| Domain events | 12 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x43C19B0BB8C9100B` |
|    2 | `0xF79B1CF77362D804` |
|    3 | `0x06093857E6AC7B18` |
|    4 | `0xAC6748FC325C2014` |
|    5 | `0x0F27FE4262B2E568` |
|    6 | `0x03047636E484DF6A` |
|    7 | `0x59AFB8A586409D33` |
|    8 | `0x35A90F5A6D8C1EDD` |
|    9 | `0xFE1FC948625E369E` |
|   10 | `0x5876C1280DDAE2CB` |
