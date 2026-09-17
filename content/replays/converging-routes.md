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
| Initial hash (tick 0) | `0xB20044E30331D6D6` |
| Final hash (tick 12) | `0xFA0279DE6D717A60` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xCE3B4DE80A28A45C` |
|    2 | `0x407DB746A313B803` |
|    3 | `0x2A14626D674B5AC7` |
|    4 | `0x86794CD53007B336` |
|    5 | `0x28C06FD5A8BB0F09` |
|    6 | `0x7C3D2E192F311D58` |
|    7 | `0x8261E06435672046` |
|    8 | `0xDBB9CF20FCCFEC7C` |
|    9 | `0x33725E55CABA6DE7` |
|   10 | `0x692A1461A1CAA8AE` |
|   11 | `0xD1F73CA989C39831` |
|   12 | `0xFA0279DE6D717A60` |
