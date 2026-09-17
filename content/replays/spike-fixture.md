# Replay corpus entry: spike-fixture

The framework-spike shared fixture (Setup.sixAgentWorld, 32 x 32, seed 20260902): agent 3 is ordered to (20,14) at tick 1. Mirrors content/fixtures/spike-fixture.cwlog and SPIKE-FIXTURE.md; CorpusTests cross-checks this table against Fixture.run () so it is not an independent re-pin.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `spike-fixture.cwlog` |
| Initial state | Fixture.initialState () (Setup.sixAgentWorld, 32 x 32, seed 20260902) |
| Tick count | 40 |
| Initial hash (tick 0) | `0xD63C7909BA798617` |
| Final hash (tick 40) | `0xF0CEAD6CE48BA07E` |
| Domain events | 36 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xF9CE751D91E92259` |
|    2 | `0xE9D32A3221AAFDDF` |
|    3 | `0x1AA9E6BBEAEA8C95` |
|    4 | `0x21B0BF55A8CE1A7F` |
|    5 | `0x5B45A1FF53272C49` |
|    6 | `0xF4FABB3F5B1D59C7` |
|    7 | `0x303451AFDD0FB63D` |
|    8 | `0xC09D0DE37B2F73E7` |
|    9 | `0x455A5CF31FC69219` |
|   10 | `0x8BF6B9A720B59BEF` |
|   11 | `0x573D9D45CBCE3755` |
|   12 | `0xCDBE2F50FF4860CF` |
|   13 | `0x88469E82EF92F609` |
|   14 | `0xB53F9CE37D5EE027` |
|   15 | `0xB433ABA382B41C6D` |
|   16 | `0x4035948348D38147` |
|   17 | `0xEDD7EAEED004B079` |
|   18 | `0xD4C344B27BAA30BF` |
|   19 | `0x44F38AB50F669F15` |
|   20 | `0xDB80D5749346EE1F` |
|   21 | `0x412C04ECC1B77D27` |
|   22 | `0x164E67453DB52F2B` |
|   23 | `0x06DD4F0BE3C165D3` |
|   24 | `0x7822A1A21E0C525F` |
|   25 | `0x14B7750A1E6A3F57` |
|   26 | `0xC943A466881919D3` |
|   27 | `0x285EC58C067D5A8B` |
|   28 | `0x2C23AD532CD2524F` |
|   29 | `0x32EA83D717DED127` |
|   30 | `0x51D11270085E984B` |
|   31 | `0xA0476B2AC0889596` |
|   32 | `0x790DD6EC76B31AF6` |
|   33 | `0x686FC545F439BD81` |
|   34 | `0x63FADE879BF23B5C` |
|   35 | `0xFC02E3F65184F0BF` |
|   36 | `0xE7A8B623784F4422` |
|   37 | `0xF9F862E27807C48D` |
|   38 | `0x4E5732C8F9227FF8` |
|   39 | `0x187F9236DD0BF76B` |
|   40 | `0xF0CEAD6CE48BA07E` |
