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
| Initial hash (tick 0) | `0x749D0E7BE45B8D90` |
| Final hash (tick 12) | `0xA817BB8DD7AF58FA` |
| Domain events | 19 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x9C990B865A0B1B2F` |
|    2 | `0xB8F53A04C36B4F84` |
|    3 | `0xB4D9F38804F79D68` |
|    4 | `0xE1F37B57C5D14D4D` |
|    5 | `0x4C557A0DB426A8FE` |
|    6 | `0xD7D11FEB7E52B90B` |
|    7 | `0x16EAE29C2F3A6C15` |
|    8 | `0x9D4F3E43E813A57E` |
|    9 | `0x683C75692371F609` |
|   10 | `0x0DF8B84C2E65BEB8` |
|   11 | `0x08DF8CE901733C7B` |
|   12 | `0xA817BB8DD7AF58FA` |
