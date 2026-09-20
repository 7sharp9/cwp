# Replay corpus entry: stalled-order-abandoned

One friendly agent at (0,0) ordered east to (4,0); a second friendly agent sits idle, permanently, on the only route at (2,0) (TASK-065, backlog B-065). Agent 0 advances to (1,0) on tick 1, then freezes every tick against the stationary occupant (MovementObstructed) -- swap-standoff's own single-sided case, but genuinely permanent. Run long enough to reach Simulation.StallAbandonTicks (40): at tick 41 the order is abandoned outright (MovementAbandoned), Destination/Route clear, and agent 0 settles one cell short of the blocker for good instead of retrying forever.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `stalled-order-abandoned.cwreplay` |
| Initial state | Corpus stalled-order-abandoned scenario (8 x 8, seed 20260904, 2 friendlies) |
| Tick count | 42 |
| Initial hash (tick 0) | `0xE5208FA49F892E3A` |
| Final hash (tick 42) | `0x1D000D120260EF46` |
| Domain events | 44 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x8C3577B38369EAF6` |
|    2 | `0xEB712E46856871D0` |
|    3 | `0x5B6769BBFF9A357A` |
|    4 | `0x9B5EE26955166060` |
|    5 | `0x614DAFD5FB0CBF36` |
|    6 | `0x8E77DCE4347FECE0` |
|    7 | `0x5A95D8BF6C90B642` |
|    8 | `0xF1239587C8891C80` |
|    9 | `0x643A03EFF2D88E56` |
|   10 | `0x9C2DF37984C4C290` |
|   11 | `0x3592B501A2DE8ADA` |
|   12 | `0xE0C9801BA0E34940` |
|   13 | `0xBD265F6106C9E956` |
|   14 | `0xC0BE6DC3F058B740` |
|   15 | `0x2E36BE2266213572` |
|   16 | `0x9F6DF8A35451DE40` |
|   17 | `0x662CB5274C242416` |
|   18 | `0xEE0899336C162110` |
|   19 | `0x7E125AFAC6D7039A` |
|   20 | `0x4DB996DCD5FF45C0` |
|   21 | `0xBF19109860157F16` |
|   22 | `0x2DAE84852574B3C0` |
|   23 | `0x6D63EC39FDB35FE2` |
|   24 | `0x2CE28ED26CF5A8E0` |
|   25 | `0xF333F41BE7ACB136` |
|   26 | `0xE33AD5162CBFC290` |
|   27 | `0xCCAFC08F047FB6BA` |
|   28 | `0x09857B68B7B91220` |
|   29 | `0x324BB12CBE708576` |
|   30 | `0x886A813B8424D8A0` |
|   31 | `0x56603A51B33EEA32` |
|   32 | `0x4111D8D3252983A0` |
|   33 | `0x8884A9611F637036` |
|   34 | `0xCEE9572568AAE910` |
|   35 | `0x6ADAA83C2B87D93A` |
|   36 | `0xF67AB0A0FD542360` |
|   37 | `0x8FE6007053C31BF6` |
|   38 | `0xD66F9E494CC99160` |
|   39 | `0xB46A507B71542E42` |
|   40 | `0x32BEE41FAED63CC0` |
|   41 | `0x5306D6C09EBAD2B3` |
|   42 | `0x1D000D120260EF46` |
