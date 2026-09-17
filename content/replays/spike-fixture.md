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
| Initial hash (tick 0) | `0xA2726329BB740614` |
| Final hash (tick 40) | `0xC9694E97A7210117` |
| Domain events | 36 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xC4AA7ADBCA44BFE0` |
|    2 | `0x2B656E5920408440` |
|    3 | `0x71B98F53920498BC` |
|    4 | `0x844F8624FB11D45C` |
|    5 | `0x53F80F747AD80FD0` |
|    6 | `0x2084307FF67C7300` |
|    7 | `0x4DF2267AB6DD5F44` |
|    8 | `0xE7CFC1528F5F9B44` |
|    9 | `0x2496618603E04DC0` |
|   10 | `0xACF09439572F6F60` |
|   11 | `0x10651CD9E7E3F59C` |
|   12 | `0x0ACDC7BB01DC06BC` |
|   13 | `0x702A0F1FE8C9C910` |
|   14 | `0x7B26B1C53D7F3BE0` |
|   15 | `0x680698AED304E1B4` |
|   16 | `0x01E43386AB871DB4` |
|   17 | `0x21F0E4FA6CA98080` |
|   18 | `0x6B6956339410F0A0` |
|   19 | `0x33C5C5289AEC06DC` |
|   20 | `0x9D6AC9647EF61E7C` |
|   21 | `0x7C6B2023942FFA92` |
|   22 | `0xDAA7E09AE2C9C21C` |
|   23 | `0x560E8F9E090D9FCE` |
|   24 | `0xEC7C9085D85DE62C` |
|   25 | `0xE61EFC106A90BEA2` |
|   26 | `0x87BFB9E5490636BC` |
|   27 | `0x77FEA3AA5FF924C6` |
|   28 | `0x0B43FDF62408189C` |
|   29 | `0x3226C994C0913712` |
|   30 | `0x1CA554DBC81452BC` |
|   31 | `0x4F3EC0681DAB75D9` |
|   32 | `0x1B782242D0412EAF` |
|   33 | `0x4D46746BE6572948` |
|   34 | `0x5D3C111796889F01` |
|   35 | `0xEBA69D54FF136F3A` |
|   36 | `0x2661BF9E7A4EAD1B` |
|   37 | `0x3EBCD25CE036A124` |
|   38 | `0xAA56D8D708F22B0D` |
|   39 | `0x1ADA1B8F5F232EA6` |
|   40 | `0xC9694E97A7210117` |
