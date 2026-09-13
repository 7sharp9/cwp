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
| Initial hash (tick 0) | `0x55F43D66C7AECB7F` |
| Final hash (tick 40) | `0x7737282578E821C6` |
| Domain events | 36 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x23FA85415EE0A275` |
|    2 | `0xDA3DC8626CCFE94F` |
|    3 | `0xB77FC590A55481A5` |
|    4 | `0x67937588E323C583` |
|    5 | `0x4C0263ACC3F24A95` |
|    6 | `0x0245A6CDD1E1916F` |
|    7 | `0x95B0F132A95662D5` |
|    8 | `0x534A7BC93D469A3B` |
|    9 | `0x93737E122F456385` |
|   10 | `0xB9BFB830C5DBC77F` |
|   11 | `0xA718E9B5C398C835` |
|   12 | `0x2F29D546948736D3` |
|   13 | `0xC352D3C550EB7AC5` |
|   14 | `0xC42FC7F0831D3BBF` |
|   15 | `0x153446DCBE647EA5` |
|   16 | `0x832D13F0D5D5501B` |
|   17 | `0x6ECB9E744009BF15` |
|   18 | `0x6FB3891C7EF8B7AF` |
|   19 | `0xB99D64F9055720E5` |
|   20 | `0x49D63F9E519D4383` |
|   21 | `0xE9975C686BB7C743` |
|   22 | `0x25494D683C4C6E33` |
|   23 | `0x96B2DE2AC10D48E3` |
|   24 | `0xF5B0F1E38A5CE783` |
|   25 | `0x8DB6A62B58BB64D3` |
|   26 | `0xB10211A1D0AFBC63` |
|   27 | `0x4985793356CDDE03` |
|   28 | `0xCDD0993A2CAD08B3` |
|   29 | `0x5FB77AF78CE0A4D3` |
|   30 | `0xB079FEC2B1D483A3` |
|   31 | `0xB34D44EB81F19872` |
|   32 | `0xB96C3175923DAA6E` |
|   33 | `0x89EA477670CDF419` |
|   34 | `0x91986E5FE0C03A20` |
|   35 | `0x3BAAE0A5153FA59B` |
|   36 | `0x83C343F7B1AD4BE2` |
|   37 | `0xC801C1012DF69D9D` |
|   38 | `0xCFAFE7EA9DE8E3A4` |
|   39 | `0x6B66A57F7D2A1F4F` |
|   40 | `0x7737282578E821C6` |
