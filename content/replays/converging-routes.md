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
| Initial hash (tick 0) | `0x1E0AFA0B8562AF65` |
| Final hash (tick 12) | `0x5BBC2F00F8F41159` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xEA3D472E014EF013` |
|    2 | `0x8E8883770AFE716E` |
|    3 | `0x76B2F2E5D1120FFA` |
|    4 | `0x16D3C2C0B341BE8D` |
|    5 | `0x859A6CB126027D54` |
|    6 | `0x6E8B221AA682240F` |
|    7 | `0xDBE6F5796B6C3CBB` |
|    8 | `0xA6301345CBB7B989` |
|    9 | `0x7ACDCA65ED59C886` |
|   10 | `0x92B9D9487A004933` |
|   11 | `0xD107B0E99F2FBFAC` |
|   12 | `0x5BBC2F00F8F41159` |
