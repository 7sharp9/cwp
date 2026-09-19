# Replay corpus entry: converging-routes

Agent 0 at (3,0) -> (3,7) crosses agent 1 at (0,3) -> (7,3); both compute (3,3) as their next cell at tick 3. TASK-017 reservation resolves the contest (tied remaining route length, lower agent id wins): agent 0 enters (3,3), agent 1 yields one tick and catches up.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `converging-routes.cwreplay` |
| Initial state | Corpus converging-routes scenario (8 x 8, seed 20260904) |
| Tick count | 12 |
| Initial hash (tick 0) | `0xA8B539547E2D0C94` |
| Final hash (tick 12) | `0x5411360F837096F6` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x1F455D026489E51E` |
|    2 | `0x1B1A4B9988306209` |
|    3 | `0x18CC73F65EC333FD` |
|    4 | `0x5394D03C52E9B2E4` |
|    5 | `0xC654877C34BAC5BB` |
|    6 | `0x0C1B16E8C17CFDF2` |
|    7 | `0x522B47F0B5FDF6EC` |
|    8 | `0x1B9F6513BAC09546` |
|    9 | `0x067D1A47BDDB0BF5` |
|   10 | `0x8745348E8483DC38` |
|   11 | `0xB731527E86162403` |
|   12 | `0x5411360F837096F6` |
