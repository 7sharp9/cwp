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
| Initial hash (tick 0) | `0xBDB4025E40BBFA28` |
| Final hash (tick 40) | `0xA5AE4AE969862EA1` |
| Domain events | 36 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x479F2E40822004A6` |
|    2 | `0x5596E9782F4D4988` |
|    3 | `0x8ADAEEEABBF0F7CA` |
|    4 | `0x8BAB8EEC2670AF98` |
|    5 | `0xCD705ED8F129D48E` |
|    6 | `0x1562EBA3DEE673B8` |
|    7 | `0x89BAA3841FC8522A` |
|    8 | `0x5776A27AB6AE7588` |
|    9 | `0x3FEA38B0E857B746` |
|   10 | `0xA88815C800629AD8` |
|   11 | `0xE814A7024173F9FA` |
|   12 | `0xA740C1538E576808` |
|   13 | `0x7E55929A877A48DE` |
|   14 | `0x83431100091CB148` |
|   15 | `0x66B7CB75FB3CAF7A` |
|   16 | `0x41EE8D04BCEEBA58` |
|   17 | `0xCABC417FAE1F6486` |
|   18 | `0x6450903CBB9EEDC8` |
|   19 | `0xBEE9F2515726C24A` |
|   20 | `0xA78815D00D1AFC78` |
|   21 | `0xC5592001739D6DA8` |
|   22 | `0xA17901AEC4904E8C` |
|   23 | `0xE5E914ECDAAAF38C` |
|   24 | `0xCE6D371DC886CB00` |
|   25 | `0x7E479522CB2DB4A0` |
|   26 | `0xD8469174CF3D796C` |
|   27 | `0xCBB77B6697B838BC` |
|   28 | `0xCF59962B054BF408` |
|   29 | `0x433E0749B3701198` |
|   30 | `0x9551ED54FA03E67C` |
|   31 | `0x0D8AF5499ED159C1` |
|   32 | `0x86DD7E8500345A69` |
|   33 | `0xE5998737D4FE621E` |
|   34 | `0xAF4165DB913EE5AB` |
|   35 | `0x3081B4037F411D50` |
|   36 | `0x64B3D8CDDBADDBED` |
|   37 | `0xF6CAEAFF4DEB8312` |
|   38 | `0x417E717C920B3ADF` |
|   39 | `0x0E580E4C5ABA9ED4` |
|   40 | `0xA5AE4AE969862EA1` |
