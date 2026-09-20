# Replay corpus entry: wall-detour

One friendly agent at (1,3) ordered to (10,3) with an impassable wall at x=5, rows 0..6. Pathfinding.findWithin routes it around the gap at rows 7..8 and the executor advances one cell per tick (docs/04 section 8 steps 2, 4, 5).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `wall-detour.cwreplay` |
| Initial state | Corpus wall-detour scenario (12 x 9, seed 20260904) |
| Tick count | 24 |
| Initial hash (tick 0) | `0x8789C9DBCF895C69` |
| Final hash (tick 24) | `0x753BB46D5A6365EC` |
| Domain events | 22 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x43F7F434BFD77319` |
|    2 | `0x8EFAD984BDF44C4B` |
|    3 | `0x1343412B6A6ADC01` |
|    4 | `0xA26754BB6B0AC2D1` |
|    5 | `0x5CBFC8E6BE0F012D` |
|    6 | `0xB7DCC6C70C8080F5` |
|    7 | `0xC3349F0D235AF741` |
|    8 | `0x254095F7EC8509FF` |
|    9 | `0x0446BFCB33CF30B1` |
|   10 | `0x6002111AF76E55B3` |
|   11 | `0x1D01934AA0804E25` |
|   12 | `0xDBB8BF6EAD250903` |
|   13 | `0x922BB480437FB359` |
|   14 | `0x12F26668860A146B` |
|   15 | `0x584C144749FB0721` |
|   16 | `0xADB3AF007537AF0F` |
|   17 | `0x175B36B7B34C28A9` |
|   18 | `0x47164426C8BCB912` |
|   19 | `0x51AA8A0ACDD09421` |
|   20 | `0x236212B3622B5DB8` |
|   21 | `0x742BA7602B610CE7` |
|   22 | `0xDB4CFF8394A56556` |
|   23 | `0x8F4D8EC991EC7DE5` |
|   24 | `0x753BB46D5A6365EC` |
