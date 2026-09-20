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
| Initial hash (tick 0) | `0x672815D313E0AE51` |
| Final hash (tick 40) | `0x27FC9F2AA2CA441E` |
| Domain events | 36 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xD355A8FE9C651965` |
|    2 | `0xB5D1029C4EA8F319` |
|    3 | `0x896F7B9FA74BFF3D` |
|    4 | `0xFAB7308909D7B911` |
|    5 | `0xDA0058EEAE335F8D` |
|    6 | `0xE6DAC708C3C6FE19` |
|    7 | `0x0AC818DA82020365` |
|    8 | `0xDEEF3A08898BD701` |
|    9 | `0x8A5D0F8E375260D5` |
|   10 | `0xAAFD1A41DB9A23F9` |
|   11 | `0x7FC11AA806B3493D` |
|   12 | `0xD4EF636F2695D061` |
|   13 | `0x572BC05982ED116D` |
|   14 | `0x2F77D3B209A875D9` |
|   15 | `0xB60DD62C872B68B5` |
|   16 | `0xEBB2DC1C863D8091` |
|   17 | `0x11D9D095A2DE3EE5` |
|   18 | `0xB5A33865E5B2BDF9` |
|   19 | `0x1727FCE22514731D` |
|   20 | `0xC23A4F95CE757E71` |
|   21 | `0x2536293F3D7433BF` |
|   22 | `0x506B8B72BAC9CDFD` |
|   23 | `0x25B9C269298E4197` |
|   24 | `0x4960B2B85B11BFE9` |
|   25 | `0xF8E45FB61F47D207` |
|   26 | `0x92DB691EC159A1DD` |
|   27 | `0x75443077F6BAA52F` |
|   28 | `0x6374EB7E01143FE1` |
|   29 | `0x874DB43C615FF4DF` |
|   30 | `0xDB4F015E7E3B5A7D` |
|   31 | `0x171B8349205C6650` |
|   32 | `0x9F5E66E7D25BD636` |
|   33 | `0xE5A4DF413B6FB8DD` |
|   34 | `0x3F75CD16932AA8C4` |
|   35 | `0x3D7830F82E9AB58B` |
|   36 | `0xE2A0199D42419ABA` |
|   37 | `0x900E0F2C60498401` |
|   38 | `0x6443ADCA3031EC88` |
|   39 | `0xB99D446DD800037F` |
|   40 | `0x27FC9F2AA2CA441E` |
