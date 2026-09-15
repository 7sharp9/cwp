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
| Initial hash (tick 0) | `0xF7CA7481CDB8C29C` |
| Final hash (tick 24) | `0x6BA08637D30F9DD5` |
| Domain events | 22 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x23BBCC8EE8C91C28` |
|    2 | `0x7027FA7E7CFC08F2` |
|    3 | `0x98A36FF9BF2109B0` |
|    4 | `0x54CCDD4805A01CA0` |
|    5 | `0xB6543589EA729354` |
|    6 | `0x92728371EB625E84` |
|    7 | `0x89DBC7E0B6C4E988` |
|    8 | `0x58E44FA36C3E4E06` |
|    9 | `0x02AFC9025A94A800` |
|   10 | `0xCD478834164088DA` |
|   11 | `0x8C5E3EDC26BDF174` |
|   12 | `0x364277CA90197112` |
|   13 | `0x9FB9BE6919325940` |
|   14 | `0x092A00A3A753223A` |
|   15 | `0x01F3DF4789861CE8` |
|   16 | `0xD8E8AE1080FC67D6` |
|   17 | `0x35D51E0FB4B338E8` |
|   18 | `0x473A7BF783BCC15B` |
|   19 | `0xCE4F061468FB7A8C` |
|   20 | `0x36D69D9AB9176E29` |
|   21 | `0x887C2D672044A81A` |
|   22 | `0xFB4A07A95866C237` |
|   23 | `0x6A57831179DDB6A8` |
|   24 | `0x6BA08637D30F9DD5` |
