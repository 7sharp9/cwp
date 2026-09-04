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
| Initial hash (tick 0) | `0x1EDFC83E4A3E9C4D` |
| Final hash (tick 12) | `0x978ABAE727665277` |
| Domain events | 19 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xBF6D32ABC3B4A24E` |
|    2 | `0x942B69B9C0FCDBA5` |
|    3 | `0x6DEF7272B16C81B1` |
|    4 | `0xBE724AEA97CBBEBC` |
|    5 | `0x99B8EB7DB30AF75F` |
|    6 | `0x0051AF94AAA59BC2` |
|    7 | `0x020675FBFE786394` |
|    8 | `0x56F077D522186D8B` |
|    9 | `0xB9379A100F540688` |
|   10 | `0x67E93EEEDD5325D9` |
|   11 | `0x6E7AE54D5A0D878E` |
|   12 | `0x978ABAE727665277` |
