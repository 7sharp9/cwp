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
| Initial hash (tick 0) | `0x89815823A8A4D2BF` |
| Final hash (tick 24) | `0x746AE3F9173619DC` |
| Domain events | 20 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x72665F149FE6265B` |
|    2 | `0x3834BB11E2666D3D` |
|    3 | `0x4A942F7438980EBB` |
|    4 | `0x6166D6477441CADB` |
|    5 | `0x7AFDB9CB720B7A7F` |
|    6 | `0x6A6B07D22BB128EF` |
|    7 | `0xF37A65F7640A9843` |
|    8 | `0x8FB0B2470F847B91` |
|    9 | `0x7A8BE3F7850E54C3` |
|   10 | `0xB9E5C3BA62435BD5` |
|   11 | `0x70AC936917D37FF7` |
|   12 | `0x7F2F515C8DDD0895` |
|   13 | `0x110D5407FBFE2ED3` |
|   14 | `0xA9743E1DC246BA85` |
|   15 | `0xAC90067DBB214E73` |
|   16 | `0x6B1C512649900B71` |
|   17 | `0x24F3C1624DBDEE3B` |
|   18 | `0xC14C81717AD9F07A` |
|   19 | `0x20B381508B73BBCF` |
|   20 | `0xDF5331171E621858` |
|   21 | `0xE20CFF9744E4991D` |
|   22 | `0x6777B404ADE1CCFE` |
|   23 | `0xD16A5710457EB643` |
|   24 | `0x746AE3F9173619DC` |
