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
| Initial hash (tick 0) | `0xAD2BAB9543DA3FBF` |
| Final hash (tick 12) | `0x7BBD60A7AC93A2FB` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xBA4CDBB7D93E0819` |
|    2 | `0x21AE3E4C828EAE48` |
|    3 | `0xB362C53E450A5684` |
|    4 | `0xFA6EF8873537822F` |
|    5 | `0x1103D0175B2373BA` |
|    6 | `0x0DC71394109B74A5` |
|    7 | `0xF6AC36E6714D67AD` |
|    8 | `0xB68EE870FB7EF82F` |
|    9 | `0xD76C82D978FB9A40` |
|   10 | `0x8A3102F67BCE0321` |
|   11 | `0x2B4745402EDA6BCA` |
|   12 | `0x7BBD60A7AC93A2FB` |
