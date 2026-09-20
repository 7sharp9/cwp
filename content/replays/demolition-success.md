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
| Initial hash (tick 0) | `0x0A2334D2C34FA797` |
| Final hash (tick 16) | `0xA5E4B19EAEDCE706` |
| Domain events | 24 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xD6C776FF979858CF` |
|    2 | `0xF7959D7E4BE15357` |
|    3 | `0xA1F3D11F470F6CAA` |
|    4 | `0x93BB5398DFB057A2` |
|    5 | `0x0E9F69EDA42B2261` |
|    6 | `0xC9B519DBA4C61A0C` |
|    7 | `0x41F863DBBB0136AB` |
|    8 | `0xC8D396CD2C00F139` |
|    9 | `0x4FBB6AE005A7FD55` |
|   10 | `0x46E488E57D3AB74D` |
|   11 | `0xC3A9778732D39969` |
|   12 | `0x314C4A539189B5E1` |
|   13 | `0xF407A9014A5AD9B5` |
|   14 | `0xD72BB2721A14C233` |
|   15 | `0xB686F1814EF8C0CB` |
|   16 | `0xA5E4B19EAEDCE706` |
