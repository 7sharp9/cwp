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
| Initial hash (tick 0) | `0x50BFA007EDFC42FE` |
| Final hash (tick 40) | `0xD9D6EC3DDC1D602F` |
| Domain events | 33 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x49B5D8933477075D` |
|    2 | `0x9B7976EEAD0CF14B` |
|    3 | `0x2AC8DFDF044E6F49` |
|    4 | `0xD4BB26B9DAD3F94B` |
|    5 | `0x3219F84813D2CD9D` |
|    6 | `0x83DD96A38C68B78B` |
|    7 | `0x5947FF1393C23D51` |
|    8 | `0x75E1D96B4C135DAB` |
|    9 | `0x06EA316E0847421D` |
|   10 | `0x38C44D4994B0B95B` |
|   11 | `0x1E022A3B5CB6E109` |
|   12 | `0x4BBEBC1FD441DABB` |
|   13 | `0xAC647FAEF4D6F13D` |
|   14 | `0x0348425CCC72CB7B` |
|   15 | `0xF329C5C5106FC021` |
|   16 | `0x47D35A76EAEFF03B` |
|   17 | `0x53E2903512F26ADD` |
|   18 | `0x5350B7222CE2AFAB` |
|   19 | `0x50A59D79B0B0CF49` |
|   20 | `0x46CAE398829007EB` |
|   21 | `0xC43F76166D58364F` |
|   22 | `0xE4630408EBD6CAA7` |
|   23 | `0xB6A7315607E265BB` |
|   24 | `0xE4C7CBD07E91F453` |
|   25 | `0x85211392EFD8A68F` |
|   26 | `0x9A44CD31131334D7` |
|   27 | `0x63BD517A2A364BD3` |
|   28 | `0x9A088554D5FA94BB` |
|   29 | `0xBFE9F82658F4CDAF` |
|   30 | `0x3C9EAC6F1AE744B7` |
|   31 | `0xF670BB2CC2DDC64E` |
|   32 | `0x45E777B5589C6EF7` |
|   33 | `0xB56D5AFC5752237C` |
|   34 | `0xAEF6C5B115E9BD45` |
|   35 | `0xA2C4B26CFB1CEABA` |
|   36 | `0x33F933880D9A5843` |
|   37 | `0x7599080132EE3238` |
|   38 | `0x6F2272B5F185CC01` |
|   39 | `0x7B1341849D9FED86` |
|   40 | `0xD9D6EC3DDC1D602F` |
