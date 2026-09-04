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
| Initial hash (tick 0) | `0xF2F3DF0D820AD9AC` |
| Final hash (tick 40) | `0x838D3AE7DBFB735D` |
| Domain events | 33 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xC848D905A9CAD13F` |
|    2 | `0x19234AC6465E2F05` |
|    3 | `0x90B080305E382443` |
|    4 | `0xE008C6578B0117F5` |
|    5 | `0xF8119436FD242507` |
|    6 | `0x09ECD0A79A9D2E55` |
|    7 | `0xBB613E8ED1CA1D43` |
|    8 | `0xE8B6D914A2926D85` |
|    9 | `0x2CA1EF76113BC17F` |
|   10 | `0x8455F41604132775` |
|   11 | `0xB12F3560E7A16D83` |
|   12 | `0x981418895730F065` |
|   13 | `0x2B70655288477397` |
|   14 | `0x04343D056D0BAC45` |
|   15 | `0xD8029633060B5D63` |
|   16 | `0xE276C8464FB10855` |
|   17 | `0x8FDADCC37477541F` |
|   18 | `0x22D0B640903DE885` |
|   19 | `0x9C70657E45A3E2A3` |
|   20 | `0x2FE3DACE9E2B5595` |
|   21 | `0xB577EFF6B20A97B5` |
|   22 | `0xA11CD0A9CFA11981` |
|   23 | `0xD3D5AE64EB7CEE39` |
|   24 | `0x08879506597DB88D` |
|   25 | `0xD0FBA852FE8E758D` |
|   26 | `0xD01017A5D95737E1` |
|   27 | `0xCFB1B389277D8239` |
|   28 | `0x1A1D9BB7613B6D65` |
|   29 | `0xA76081C2C6A32DE5` |
|   30 | `0xA3E0E517D26FED91` |
|   31 | `0x25315447F9D0E230` |
|   32 | `0xA9905D83030ECF85` |
|   33 | `0xE84D22152E175E0A` |
|   34 | `0x329E1078823EA257` |
|   35 | `0x7911F8633F9416EC` |
|   36 | `0xF6D282ED1457EA01` |
|   37 | `0xF744976403CDB056` |
|   38 | `0x656789B91AB43663` |
|   39 | `0xC6541DCD50DD3168` |
|   40 | `0x838D3AE7DBFB735D` |
