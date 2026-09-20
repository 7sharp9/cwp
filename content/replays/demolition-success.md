# Replay corpus entry: demolition-success

One friendly agent at (0,0), a static target at (3,0), and an extraction area at (10,0) (TASK-062, backlog B-032): MoveTo (3,0) at tick 1 reaches the target at tick 3, a 2-tick occupancy plant completes the DestroyTarget objective at tick 4; MoveTo (10,0) at tick 8 reaches the extraction area at tick 14, completing ExtractAgents and reaching MissionOutcome = Succeeded the same tick -- the Mission phase's first end-to-end corpus proof.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `demolition-success.cwreplay` |
| Initial state | Corpus demolition-success scenario (12 x 3, seed 20260904, 1 friendly) |
| Tick count | 16 |
| Initial hash (tick 0) | `0x77DA63783F562515` |
| Final hash (tick 16) | `0x2438D78A773487F0` |
| Domain events | 24 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xC68E2FBFFBE44EAB` |
|    2 | `0x4C716A869FF1921B` |
|    3 | `0xF3E4DF77758EE69E` |
|    4 | `0xBA1C66D97FC0805C` |
|    5 | `0x64F74C090E6F81AB` |
|    6 | `0x957D539D1FEB86D2` |
|    7 | `0x3EF358CC44A40081` |
|    8 | `0xB48EF402CB78DBEB` |
|    9 | `0x8455E4ABD444FF1F` |
|   10 | `0x28B4C06B80282F4F` |
|   11 | `0x7E41AAE533387883` |
|   12 | `0x0E7520D21F6BC8DB` |
|   13 | `0x21619357638C0FE7` |
|   14 | `0x36E7D9658C65FF8F` |
|   15 | `0x9AAF3973314EE091` |
|   16 | `0x2438D78A773487F0` |
