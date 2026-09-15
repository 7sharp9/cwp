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
| Initial hash (tick 0) | `0xBE2636723F99F53A` |
| Final hash (tick 40) | `0x56395A49904D017D` |
| Domain events | 36 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x0BBDD2A32CE88786` |
|    2 | `0x35DBCDE0E4F48FEE` |
|    3 | `0x36D0EDD8D163AE8A` |
|    4 | `0x2689F8DB132F53CA` |
|    5 | `0x15A4C3EC76FC219E` |
|    6 | `0xFFC8F6491D1D8E46` |
|    7 | `0x66DFD1A019AC6EFA` |
|    8 | `0x365DFF65492963AA` |
|    9 | `0x8C185EFAD1344916` |
|   10 | `0xAA830EE8778BD20E` |
|   11 | `0xC0CEC2EAC1973DCA` |
|   12 | `0x960935410A8BA52A` |
|   13 | `0x22369B4F34CD293E` |
|   14 | `0xB376D53638C4DF96` |
|   15 | `0x73368EBA4339A59A` |
|   16 | `0xD1524188E851716A` |
|   17 | `0xEAA1A03F24B02EC6` |
|   18 | `0xDBB808A4C79AB94E` |
|   19 | `0x1B2F2F31627FA0AA` |
|   20 | `0x30FD7764A9B3B2EA` |
|   21 | `0xECD2A66735B33274` |
|   22 | `0x7D3F3761E988C522` |
|   23 | `0x8FAB4343DD35C2D8` |
|   24 | `0x06E21303595009F2` |
|   25 | `0x0C535972100B914C` |
|   26 | `0x2FCD7DAF3AD3914A` |
|   27 | `0x33FAE4823B812F88` |
|   28 | `0xB9BC48842C5A710A` |
|   29 | `0x77003C009E690B94` |
|   30 | `0x0A73CEE90B7F1F12` |
|   31 | `0x140F6468D58DFCDF` |
|   32 | `0xB91918166FD008C5` |
|   33 | `0xAAE9267DF334E9DE` |
|   34 | `0xB2ECCF6F33E16413` |
|   35 | `0x2FDC38406561C69C` |
|   36 | `0x5135C3C589C03AF9` |
|   37 | `0xB10463F6772828D2` |
|   38 | `0x1C0F1E3638316DC7` |
|   39 | `0x901E021E648FDF20` |
|   40 | `0x56395A49904D017D` |
