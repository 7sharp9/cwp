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
| Initial hash (tick 0) | `0xC0A53D46AE5D7C80` |
| Final hash (tick 40) | `0x447C32A5D599EAB3` |
| Domain events | 36 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xCD1C3578BFB0F320` |
|    2 | `0xDF6BAA3629E6E1CC` |
|    3 | `0x8AD488B567B67378` |
|    4 | `0x9E239C8E0D2D18EC` |
|    5 | `0x658F90E3F5B4C120` |
|    6 | `0x9C9B93BB731F3974` |
|    7 | `0xBECD10E0293787D8` |
|    8 | `0x82DA58DF1D0F9704` |
|    9 | `0xFB1E55FC6A366D80` |
|   10 | `0x06CB4A3BAD7134BC` |
|   11 | `0xBD8AD5CD8B360248` |
|   12 | `0xE3FAFC8E328E223C` |
|   13 | `0xA1C35C4CAE6D99E0` |
|   14 | `0x2C0CF82ECDAFDF14` |
|   15 | `0xDFE4D41A6F7B6C68` |
|   16 | `0x9BE4B762D775E6C4` |
|   17 | `0x7E6CA6120778A700` |
|   18 | `0x2B07F5A03CA8CDEC` |
|   19 | `0x96DB4B0C6A83C0D8` |
|   20 | `0x951A2ECAB348164C` |
|   21 | `0x8D09C1F476F5CB46` |
|   22 | `0xB07922DEB7AB64C0` |
|   23 | `0x961ACE9C3E673EDE` |
|   24 | `0xCE2AC7A8920D77DC` |
|   25 | `0x81B017A77E96B926` |
|   26 | `0x4CFC18A72044AE48` |
|   27 | `0x7E46E093F803EAAE` |
|   28 | `0xC66789C7E16FAF7C` |
|   29 | `0x882921E58192C1C6` |
|   30 | `0x3D46D3B2D7B34C20` |
|   31 | `0x8814DF097CC85A89` |
|   32 | `0xFAF2A8BC7038032B` |
|   33 | `0xBA37563CD1B33264` |
|   34 | `0x9CBFC071234528FD` |
|   35 | `0xFFD60812D29AD256` |
|   36 | `0x71F9DB27867DB69F` |
|   37 | `0x9BE7C5592D3044A8` |
|   38 | `0xC184B4458E90E0A1` |
|   39 | `0x5DE12A8F954AB6DA` |
|   40 | `0x447C32A5D599EAB3` |
