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
| Initial hash (tick 0) | `0xC0FCC13D8A85DEBE` |
| Final hash (tick 12) | `0x93AEA8F89A458552` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x1E8F64A9235746E4` |
|    2 | `0x19F26441CA578015` |
|    3 | `0xE15440E7EBE55029` |
|    4 | `0x87F0B376D5B2DEE6` |
|    5 | `0xDAB680FCAB38CA9B` |
|    6 | `0xE367F439A926F418` |
|    7 | `0x70273008CCB557D0` |
|    8 | `0xC9E2847E4CDB531E` |
|    9 | `0xD61F675D1C46FE9D` |
|   10 | `0xFF2116170F6C5DDC` |
|   11 | `0xAE280DF9B2E2E3E3` |
|   12 | `0x93AEA8F89A458552` |
