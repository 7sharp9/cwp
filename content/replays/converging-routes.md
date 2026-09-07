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
| Initial hash (tick 0) | `0xBE15331B04AEBE03` |
| Final hash (tick 12) | `0x385CEB7D96FB1415` |
| Domain events | 19 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x8531642DD2A25744` |
|    2 | `0xEF53EC62A29A4C67` |
|    3 | `0xA05326D276394923` |
|    4 | `0x71420DB5D8E73E56` |
|    5 | `0xA5FFE0335C909A4D` |
|    6 | `0xC7CB715D05DFC150` |
|    7 | `0xDBF4313C289DDEA6` |
|    8 | `0xE34132EF69AE2B79` |
|    9 | `0x5AC00C94685C533E` |
|   10 | `0x1CC1C6777862118B` |
|   11 | `0xF10AD4EEFB5E5A98` |
|   12 | `0x385CEB7D96FB1415` |
