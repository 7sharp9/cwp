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
| Initial hash (tick 0) | `0x04912B34E23FD985` |
| Final hash (tick 12) | `0x95AFC1983F2E4825` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x96BC2C1BD27E8487` |
|    2 | `0x2DCBE6D86DB6B00E` |
|    3 | `0xA767BA291C4C99BA` |
|    4 | `0xCB65AD993BE5F0AD` |
|    5 | `0xB1337DA5B870CBC0` |
|    6 | `0x1FF6D515F36A7B2B` |
|    7 | `0xAB810FA9A0167613` |
|    8 | `0x67E06BDE37B91079` |
|    9 | `0x905778560B982A5A` |
|   10 | `0x4901ADC3F92BA25F` |
|   11 | `0x8CD9B7B62C73B7A8` |
|   12 | `0x95AFC1983F2E4825` |
