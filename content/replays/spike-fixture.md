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
| Initial hash (tick 0) | `0xF1A703A752C0F6B9` |
| Final hash (tick 40) | `0x507D041E109404B6` |
| Domain events | 36 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x150D89093BC9C641` |
|    2 | `0x3C84741325F8A8C1` |
|    3 | `0x57E8F85C95140235` |
|    4 | `0x41D748A18F9B49E5` |
|    5 | `0x8CA1EBD72D3AAC01` |
|    6 | `0x37862CBC2AD98B31` |
|    7 | `0x2111C2150C221B8D` |
|    8 | `0xEA85FFAAC658199D` |
|    9 | `0xE80EE4D9CF5FE9E1` |
|   10 | `0x1F280FA49C98A181` |
|   11 | `0x368F4741DA362755` |
|   12 | `0xEB6755F0337A2905` |
|   13 | `0x8A488DBC994415E1` |
|   14 | `0xD57146F61D2C68D1` |
|   15 | `0x282B7249BB53691D` |
|   16 | `0xAA2DA0EBE95A908D` |
|   17 | `0x871F8A5EC05789A1` |
|   18 | `0x67D775B13745CF61` |
|   19 | `0xF5C24EDA81E10815` |
|   20 | `0x91D758C4242CE4A5` |
|   21 | `0x1570C920BE41E3A7` |
|   22 | `0xAEC7D965387F39E5` |
|   23 | `0x92B4D29865C54A7B` |
|   24 | `0x76807D006F01B995` |
|   25 | `0xD66421CB943DB947` |
|   26 | `0x71880F0882A9AB15` |
|   27 | `0xA44DEEC6B77B10E3` |
|   28 | `0x1E1C0818E3B97CC5` |
|   29 | `0xD6CBBA0C1F11C1C7` |
|   30 | `0x46B17BCEA997D385` |
|   31 | `0xB340A8EFC5EB97A4` |
|   32 | `0xC4235060A1BFF05E` |
|   33 | `0x2BBE91A830226A55` |
|   34 | `0xF6F7A35660F42E3C` |
|   35 | `0x0CD3C8E1A0801AC3` |
|   36 | `0x00E6A0A37F11BF92` |
|   37 | `0x8D8411A745C8CD49` |
|   38 | `0xFBAD4EA87A0C0F80` |
|   39 | `0x1FB1BFF52F1268B7` |
|   40 | `0x507D041E109404B6` |
