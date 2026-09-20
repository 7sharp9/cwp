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
| Initial hash (tick 0) | `0x2D47069683095E0C` |
| Final hash (tick 12) | `0xFF4D191E2EE900BC` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x03A0DAD387ECAC02` |
|    2 | `0xFD4D4B524E60113B` |
|    3 | `0x10B445A22298CDF4` |
|    4 | `0x772DE9D36B2BCB84` |
|    5 | `0x6014A7E0AB4FA9F1` |
|    6 | `0xCBAE08702C9B629E` |
|    7 | `0x9AB930C8BD51D896` |
|    8 | `0xD596E262CB54CA40` |
|    9 | `0x796B07A22BCD913F` |
|   10 | `0xF8231EF754B8211A` |
|   11 | `0x966A75F9DDF62101` |
|   12 | `0xFF4D191E2EE900BC` |
