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
| Initial hash (tick 0) | `0xE13D7540912C7E25` |
| Final hash (tick 40) | `0xAFA35198CC6BD8D4` |
| Domain events | 33 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x5D8C8970F39771B2` |
|    2 | `0x986FAB7690854608` |
|    3 | `0x78C52861050C9DC2` |
|    4 | `0xD40695B8FAFB561C` |
|    5 | `0xBDF65E0D29AE9A42` |
|    6 | `0xB0256E0D8E8C2E90` |
|    7 | `0xAB9AF8816810B182` |
|    8 | `0xF2F8B000576FE69C` |
|    9 | `0x919F42D04CF1B922` |
|   10 | `0xE2A8F9194D7A34C8` |
|   11 | `0x7EED1056CB502CD2` |
|   12 | `0xEF595793C31AF83C` |
|   13 | `0x329F3AB2B2CBBEB2` |
|   14 | `0xC767132B1E2EBDE0` |
|   15 | `0x27D271EBF3B18B72` |
|   16 | `0x3B2FA9658EB10ADC` |
|   17 | `0xE60587050481C8B2` |
|   18 | `0x0F62C2D1BBA02B88` |
|   19 | `0x27C80E5B13FB6D22` |
|   20 | `0xEED0A7B655AC8D1C` |
|   21 | `0x82A85615AAE4D800` |
|   22 | `0x95D3F98DB887CB64` |
|   23 | `0xDF031A9CEAE912E0` |
|   24 | `0xB5810624FE66B394` |
|   25 | `0xC2CF53891C27B820` |
|   26 | `0x73EBD725AB4924BC` |
|   27 | `0xA03BA02B75A0DAD0` |
|   28 | `0xE2FE27F5CD67C07C` |
|   29 | `0x9CE2A92D40948A50` |
|   30 | `0xE683753245907FD4` |
|   31 | `0x92FF4C99EC571279` |
|   32 | `0x0744CA8C28A3C39C` |
|   33 | `0x9E14C1BE3A6A6367` |
|   34 | `0xCB8897ABEC49C97A` |
|   35 | `0xB22633B283235655` |
|   36 | `0xB20CE024173DD318` |
|   37 | `0xADCFC43BA52AD3F3` |
|   38 | `0xA2AE579826CB3E86` |
|   39 | `0x5CEE494A71BD65D1` |
|   40 | `0xAFA35198CC6BD8D4` |
