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
| Initial hash (tick 0) | `0xF762ECD4377B5E68` |
| Final hash (tick 40) | `0xAF1FB68EF486CB39` |
| Domain events | 36 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xAB44071A1EF65B7A` |
|    2 | `0x514A11EBF8061FE8` |
|    3 | `0x99C1EF2E2FBD222A` |
|    4 | `0xDFDB9ED1372B4ACC` |
|    5 | `0x67D60F333BADC832` |
|    6 | `0x63B04633D194B120` |
|    7 | `0xEBCEEE29D03DC002` |
|    8 | `0x2F61E3FD7E2D223C` |
|    9 | `0xA527D814D910619A` |
|   10 | `0x977D9E5B5D42F878` |
|   11 | `0x3085CB8CAC4751CA` |
|   12 | `0x03CA8F1AEE0A677C` |
|   13 | `0x1FBD939C873F98E2` |
|   14 | `0x0871C58B79035800` |
|   15 | `0xC12C277298886252` |
|   16 | `0xED139A0CC58ABFCC` |
|   17 | `0x283BCDBAE264A3FA` |
|   18 | `0x53285175D7952068` |
|   19 | `0x538E7801AAB7B30A` |
|   20 | `0x16FE52B196771F4C` |
|   21 | `0xFF439742CB9E599C` |
|   22 | `0xE028973C5329EF64` |
|   23 | `0x0F2C55BC9411582C` |
|   24 | `0x4D13D5A98BB7A724` |
|   25 | `0x0B68392FEFB3B3C4` |
|   26 | `0xC04FF57C155E111C` |
|   27 | `0x8BA8BC60053D2234` |
|   28 | `0x588CB1CA3848C39C` |
|   29 | `0xCC92930EA3B8632C` |
|   30 | `0x16902DAA944DDE44` |
|   31 | `0x46D6DE844EBC9F35` |
|   32 | `0x6B98BD749F7B2C51` |
|   33 | `0x20486DB6D3F138D6` |
|   34 | `0xDE48FD1F74DA655F` |
|   35 | `0x189B17755CA3AE14` |
|   36 | `0x7D7FA6B88E5CE2DD` |
|   37 | `0x36C2DC93D32B0522` |
|   38 | `0xCE44C436BF3BC00B` |
|   39 | `0xCB886C55AF694840` |
|   40 | `0xAF1FB68EF486CB39` |
