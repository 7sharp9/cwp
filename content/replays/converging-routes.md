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
| Initial hash (tick 0) | `0x825C2119A5855CC9` |
| Final hash (tick 12) | `0xD34F1F26089B0103` |
| Domain events | 25 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xA6F1B95318062527` |
|    2 | `0xD3D79DD6758FAE54` |
|    3 | `0xB4D95356B6FCCB70` |
|    4 | `0x565F399D22411BB9` |
|    5 | `0x0E0D807059D5E282` |
|    6 | `0x0F26A4B2E6F05263` |
|    7 | `0x4C5C98F12462B3F1` |
|    8 | `0x10E36C059E28DE23` |
|    9 | `0x7A77B399EE8A387C` |
|   10 | `0x472A8DDA164556AD` |
|   11 | `0x1A1A1C44D7AD1232` |
|   12 | `0xD34F1F26089B0103` |
