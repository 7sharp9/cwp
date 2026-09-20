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
| Initial hash (tick 0) | `0x5049F6F0E9FCA1E2` |
| Final hash (tick 40) | `0xD2A6A1AE46AD76A5` |
| Domain events | 36 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x77ECA4A6947C3342` |
|    2 | `0x0572D3D1E15322A6` |
|    3 | `0xA337EE31F2A8DA3A` |
|    4 | `0x855F7CB4911738D6` |
|    5 | `0x5BA922E646352FE2` |
|    6 | `0x57ACCC895E0CBD1E` |
|    7 | `0xAD2673422C2FBE3A` |
|    8 | `0xB7F911EFB050562E` |
|    9 | `0x8FD6E3C055E270F2` |
|   10 | `0x8D9DA671CB539DA6` |
|   11 | `0xF73D6BCC22548D5A` |
|   12 | `0x1ABFAAB93D168776` |
|   13 | `0x625BC5FBF5E83A92` |
|   14 | `0xBD0C4494915A874E` |
|   15 | `0x6958C2E9A7709B7A` |
|   16 | `0x53B58CCB2D321B9E` |
|   17 | `0x68C7314DD67FDC62` |
|   18 | `0x8FFAE7964D1017C6` |
|   19 | `0xDE515CDCA63C9C5A` |
|   20 | `0xE5CAEAAB0466DB96` |
|   21 | `0xAF65423BD2CF8F68` |
|   22 | `0xEACFBA8DBF18CE12` |
|   23 | `0x314B80E7340FEFE0` |
|   24 | `0x4EFA0DA2805A8B06` |
|   25 | `0xDEF9B2B149745FD8` |
|   26 | `0x876B3B2EE49AF6FA` |
|   27 | `0x40A533323E701A00` |
|   28 | `0x3D7DCF0506435CF6` |
|   29 | `0x1EFAEA1B1AB8DC38` |
|   30 | `0xC078730259340122` |
|   31 | `0x46ED110085AC43D1` |
|   32 | `0xC4B75F7DCF0C05FD` |
|   33 | `0xDB03594AF35A69D6` |
|   34 | `0x322D994F91A5432B` |
|   35 | `0x296AADAF16EBFE64` |
|   36 | `0xCBF1A0CC4B375121` |
|   37 | `0xE4FE34308693355A` |
|   38 | `0xA7A293834FFD7A9F` |
|   39 | `0x752C7261B0AD7928` |
|   40 | `0xD2A6A1AE46AD76A5` |
