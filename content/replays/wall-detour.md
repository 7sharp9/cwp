# Replay corpus entry: wall-detour

One friendly agent at (1,3) ordered to (10,3) with an impassable wall at x=5, rows 0..6. Pathfinding.findWithin routes it around the gap at rows 7..8 and the executor advances one cell per tick (docs/04 section 8 steps 2, 4, 5).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `wall-detour.cwlog` |
| Initial state | Corpus wall-detour scenario (12 x 9, seed 20260904) |
| Tick count | 24 |
| Initial hash (tick 0) | `0xD33F1F626C50BD78` |
| Final hash (tick 24) | `0x299F40A7C31AFBE3` |
| Domain events | 19 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x5FF45ABE4F230A2E` |
|    2 | `0x7A78C22EA07085CC` |
|    3 | `0x122ADC954398EF4E` |
|    4 | `0x1C8A9FAFB05DA42E` |
|    5 | `0xB763B1468C04F3EA` |
|    6 | `0xD55D42EB9CBDAE4A` |
|    7 | `0xD78E0D24545677B6` |
|    8 | `0x4190D0EC673D90B8` |
|    9 | `0x3EE44A741CD2A266` |
|   10 | `0x3945ADC3E1DE66B4` |
|   11 | `0x3834026E863748C2` |
|   12 | `0x2F49C14BC2B08A24` |
|   13 | `0x8872DF37931F5EA6` |
|   14 | `0x525B277011EE80B4` |
|   15 | `0x2B5526850511DF26` |
|   16 | `0xD0FF6AB72CAB9418` |
|   17 | `0x10214129EA331D2E` |
|   18 | `0xAD3E713BE1B872B5` |
|   19 | `0x5FD78E09D23BB5F8` |
|   20 | `0xF5D7D36CF5D32897` |
|   21 | `0x5D2A86CA7EFF1FCA` |
|   22 | `0x93928586472911E1` |
|   23 | `0x4C7D42D50F791764` |
|   24 | `0x299F40A7C31AFBE3` |
