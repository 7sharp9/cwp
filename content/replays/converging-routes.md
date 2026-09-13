# Replay corpus entry: converging-routes

Agent 0 at (3,0) -> (3,7) crosses agent 1 at (0,3) -> (7,3); both compute (3,3) as their next cell at tick 3. TASK-017 reservation resolves the contest (tied remaining route length, lower agent id wins): agent 0 enters (3,3), agent 1 yields one tick and catches up.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `converging-routes.cwlog` |
| Initial state | Corpus converging-routes scenario (8 x 8, seed 20260904) |
| Tick count | 12 |
| Initial hash (tick 0) | `0xBCB81DADED9E631E` |
| Final hash (tick 12) | `0xB05B497906697E40` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x7A89F4BACAC82C68` |
|    2 | `0x57375F37AEA53C93` |
|    3 | `0x741146CF0E0B2827` |
|    4 | `0x0CDC384AA622F646` |
|    5 | `0xD374830264B52ECD` |
|    6 | `0xC44CB72B89FA1A5C` |
|    7 | `0xE7461F75D77C0A7E` |
|    8 | `0x24ABED2C93058800` |
|    9 | `0x7C9367D291BD39BF` |
|   10 | `0x7E74D29440D9EEC2` |
|   11 | `0xA13614B7BF6042FD` |
|   12 | `0xB05B497906697E40` |
