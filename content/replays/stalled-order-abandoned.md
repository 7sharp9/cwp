# Replay corpus entry: stalled-order-abandoned

One friendly agent at (0,0) ordered east to (4,0); a second friendly agent sits idle, permanently, on the only route at (2,0) (TASK-065, backlog B-065). Agent 0 advances to (1,0) on tick 1, then freezes every tick against the stationary occupant (MovementObstructed) -- swap-standoff's own single-sided case, but genuinely permanent. Run long enough to reach Simulation.StallAbandonTicks (40): at tick 41 the order is abandoned outright (MovementAbandoned), Destination/Route clear, and agent 0 settles one cell short of the blocker for good instead of retrying forever.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `stalled-order-abandoned.cwreplay` |
| Initial state | Corpus stalled-order-abandoned scenario (8 x 8, seed 20260904, 2 friendlies) |
| Tick count | 42 |
| Initial hash (tick 0) | `0x745B1AE1EC2F01C1` |
| Final hash (tick 42) | `0xD584A5D6F6CCC8BF` |
| Domain events | 44 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x43C19B0BB8C9100B` |
|    2 | `0x12535F044A284869` |
|    3 | `0x3B0C1CE4DA6FB527` |
|    4 | `0x44145164AA3B9929` |
|    5 | `0xA3908AB610BC1403` |
|    6 | `0x3139CBEC501709C1` |
|    7 | `0xC7F730357E848217` |
|    8 | `0x8B95AFCFD2AA90C1` |
|    9 | `0x3AB62570A5F8E6DB` |
|   10 | `0x087917C2152E4509` |
|   11 | `0x96E4C1E4283DC2E7` |
|   12 | `0x9FEF330575C63A09` |
|   13 | `0xF84794E5871D54A3` |
|   14 | `0xAB5CC5409BE0B8D1` |
|   15 | `0x34179F5E3BAEF5B7` |
|   16 | `0x57C6799220E79931` |
|   17 | `0xF554BBAFD09AE0CB` |
|   18 | `0x1F00842860D5BD49` |
|   19 | `0x31BD454A123981E7` |
|   20 | `0x18CDDDBCE7F1A4C9` |
|   21 | `0xFD4856B0EAF336A3` |
|   22 | `0xE1EF37D045024521` |
|   23 | `0x391861299F84D7B7` |
|   24 | `0xFB7D507FFF4CFF61` |
|   25 | `0xC8C2A10E67780B7B` |
|   26 | `0x53AD506E2D034EA9` |
|   27 | `0x4CC81F1597BEC2E7` |
|   28 | `0xAA55A07DC55276E9` |
|   29 | `0xDFE3722D823CA843` |
|   30 | `0x4D2C285F4BF20411` |
|   31 | `0x044A17ACF0051657` |
|   32 | `0x05B1EEBA3FB4E5F1` |
|   33 | `0x3B5BF5EABEAD7CCB` |
|   34 | `0xA888CD8DA7374029` |
|   35 | `0xDA1BBDD626D01067` |
|   36 | `0xAC951D262E1BC7E9` |
|   37 | `0x958E22232FC9BB03` |
|   38 | `0xC9BA577037F221C1` |
|   39 | `0xCBD17DF98A0BFC97` |
|   40 | `0x0CACD65A73BC75C1` |
|   41 | `0xA35517BD105CB608` |
|   42 | `0xD584A5D6F6CCC8BF` |
