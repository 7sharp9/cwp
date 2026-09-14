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
| Initial hash (tick 0) | `0xD722709B2F996EF8` |
| Final hash (tick 12) | `0xA10873C78C5B4F94` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x1253CB1B2049E50A` |
|    2 | `0xF1CBC3CFFD1F2E6F` |
|    3 | `0xD8FE3D872AF27B33` |
|    4 | `0x98803B854B240908` |
|    5 | `0x531E7703EDF3A921` |
|    6 | `0x5877F51DA351331E` |
|    7 | `0x336AA903B0FC6ECA` |
|    8 | `0xC0AFDD43794D0354` |
|    9 | `0xF55E7FB7172900B7` |
|   10 | `0xC2CA4BDD440A9E4A` |
|   11 | `0x726DC684F87D1331` |
|   12 | `0xA10873C78C5B4F94` |
