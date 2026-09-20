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
| Initial hash (tick 0) | `0x80C81B9A3C991853` |
| Final hash (tick 12) | `0xF57F801C82D590F3` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x1FB9F58CDF6A36C3` |
|    2 | `0xE033EC2C13B71440` |
|    3 | `0x727183EA8C64B5D9` |
|    4 | `0x2BAAF92A8F2286B9` |
|    5 | `0x94029076D68EFDE2` |
|    6 | `0x22C430D45B0F7D3F` |
|    7 | `0x97AA5FF9507E51B5` |
|    8 | `0x8C8C8AEADEDC37F1` |
|    9 | `0xD7644C2085E04B48` |
|   10 | `0xE8A2EAB90C357061` |
|   11 | `0xC5ED06A75736BBFA` |
|   12 | `0xF57F801C82D590F3` |
