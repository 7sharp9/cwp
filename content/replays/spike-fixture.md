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
| Initial hash (tick 0) | `0x68F435EF0364DC03` |
| Final hash (tick 40) | `0x06E4E1CD02EEA0C0` |
| Domain events | 36 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x6E4B77F8E1F92AA3` |
|    2 | `0x590B82E756D3511B` |
|    3 | `0x47E59F687DE9A52F` |
|    4 | `0x36BF8AAD1F763ACF` |
|    5 | `0x57FB28C91FAC79AB` |
|    6 | `0x3DEF2F4B1BBF2323` |
|    7 | `0x51BE97AB775CB92F` |
|    8 | `0xA5FF20EDDF47D77F` |
|    9 | `0x954DF7B603467C13` |
|   10 | `0xB638F3EBE81B6FFB` |
|   11 | `0x6EA71A9B37742D4F` |
|   12 | `0x433D877C746D560F` |
|   13 | `0x83E4A89D7471F28B` |
|   14 | `0x617389291C246FD3` |
|   15 | `0x13E8CF187D090B8F` |
|   16 | `0x5E86F5F9DE0AF45F` |
|   17 | `0x78BEF682787D89C3` |
|   18 | `0xEBC4D62143CA1E5B` |
|   19 | `0xEDC1DA2C608FCE8F` |
|   20 | `0x992CD4834CAF7BAF` |
|   21 | `0x80073D3650267D7D` |
|   22 | `0x79F1D2AC8909FB07` |
|   23 | `0x373E1170DCA590A9` |
|   24 | `0xF5A3D1E9F5B0CC17` |
|   25 | `0x7E482E6DFB499045` |
|   26 | `0x49BC74677118D63F` |
|   27 | `0x97644FA1BE001109` |
|   28 | `0x2CC0E32C6975142F` |
|   29 | `0x92B1285119A1899D` |
|   30 | `0xBB3E2FAC964D2A17` |
|   31 | `0x5CC76739198FA61E` |
|   32 | `0x765A299F52037278` |
|   33 | `0x44FE3B5201AD4E7F` |
|   34 | `0xA98AD8A97A3805AA` |
|   35 | `0x3888A09E4B9FA411` |
|   36 | `0x621D2B902281A594` |
|   37 | `0x20892C6B7F75EAEB` |
|   38 | `0xB972CC2906322C56` |
|   39 | `0x146B27E0A5C8461D` |
|   40 | `0x06E4E1CD02EEA0C0` |
