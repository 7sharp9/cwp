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
| Initial hash (tick 0) | `0x2C5170B1CA685DD1` |
| Final hash (tick 12) | `0xB28D06DB60BFDD83` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xAFD77FBE2477AB03` |
|    2 | `0x78A9606E522AD0AC` |
|    3 | `0xD2DE4C22D920CD68` |
|    4 | `0x5C14E528C7DED719` |
|    5 | `0x2780A1749866D32E` |
|    6 | `0x37263459D578972F` |
|    7 | `0xC19E1EF1BAC49689` |
|    8 | `0xC7100FC73D5CC037` |
|    9 | `0x83B033282760C27C` |
|   10 | `0x72F77A0EA8322889` |
|   11 | `0x5BD994AEC602B496` |
|   12 | `0xB28D06DB60BFDD83` |
