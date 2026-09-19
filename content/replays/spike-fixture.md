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
| Initial hash (tick 0) | `0xB25FE816BCB67A11` |
| Final hash (tick 40) | `0x0A822498317E0958` |
| Domain events | 36 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x44201FB70CF4A883` |
|    2 | `0xA0656574757B4E71` |
|    3 | `0x62AF431172CBF8C7` |
|    4 | `0x2E9BCE24165D2159` |
|    5 | `0x59D8A91703DE078B` |
|    6 | `0xEEE9E7B3845B8BD9` |
|    7 | `0xE3E6E8427A5F6887` |
|    8 | `0xEBF46A8B6AB59191` |
|    9 | `0x27535F3014B08023` |
|   10 | `0xC85436CD8FCDE0F1` |
|   11 | `0xD5D2657384D4EF37` |
|   12 | `0xE3E262C2EB0C52E9` |
|   13 | `0xBC04E6BDCAFF319B` |
|   14 | `0xF10E88E6A6958EA9` |
|   15 | `0x44D2141FE96344F7` |
|   16 | `0x56D3515F85559C31` |
|   17 | `0x4A1820AE41521DC3` |
|   18 | `0x8993DE4D294BD971` |
|   19 | `0x7055E4FC1157C687` |
|   20 | `0x8A35C6CC6B346839` |
|   21 | `0x43E9D37C9D45FD65` |
|   22 | `0xB75EC359E4FB45B5` |
|   23 | `0x9A7F52C2EAB4C399` |
|   24 | `0xB5DA2F0D2B3ED469` |
|   25 | `0x2ACDCCE47319B9DD` |
|   26 | `0xD7A2C8CA659037AD` |
|   27 | `0x5E1FC3F921EFC829` |
|   28 | `0x4E41F484176365A9` |
|   29 | `0x593B579942AB34D5` |
|   30 | `0x76F821A2A34D8DE5` |
|   31 | `0x0F16D19D3CAC0678` |
|   32 | `0x8E62504B18AFF3F0` |
|   33 | `0x14C94D9E51B9858B` |
|   34 | `0x93CDE88A9C07CFF2` |
|   35 | `0x8171A9D56107DE3D` |
|   36 | `0xEA06BE07C668A184` |
|   37 | `0xA31DF373B2FEF19F` |
|   38 | `0x8602807C50A6F686` |
|   39 | `0x0700CEF08E6053B1` |
|   40 | `0x0A822498317E0958` |
