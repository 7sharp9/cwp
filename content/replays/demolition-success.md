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
| Initial hash (tick 0) | `0x1A9E1DE486ABF1C0` |
| Final hash (tick 16) | `0x00774FE0B0EA0E15` |
| Domain events | 24 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xFDA57B56529D8ECE` |
|    2 | `0x5D13B199CA379A5E` |
|    3 | `0xFB1D4A0DB392C987` |
|    4 | `0xD6AF6DB929A18641` |
|    5 | `0x60DE82C060B37002` |
|    6 | `0x4B0EBCAFB176348B` |
|    7 | `0x0C5C1E3FB14865EC` |
|    8 | `0x94F84A0110F5D0E6` |
|    9 | `0xEBD28AB6335005F2` |
|   10 | `0xAD2BE3001BE25B42` |
|   11 | `0x008A18D24C02B7AE` |
|   12 | `0x4491A498D2EE3F06` |
|   13 | `0x98F27EDC9D6F592A` |
|   14 | `0xFE18CD54464B09EE` |
|   15 | `0x11C104B891AFB0AC` |
|   16 | `0x00774FE0B0EA0E15` |
