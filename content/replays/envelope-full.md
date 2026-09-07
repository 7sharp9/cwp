# Replay corpus entry: envelope-full

The first committed replay in the production replay-command format
(`src/CommandoWar.Sim/ReplaySerialisation.fs`, replay-command format v1,
TASK-025 / backlog B-045). It carries a full accepted-command envelope the
legacy `.cwlog` cannot express: a three-recipient `MoveTo` (agents 3, 4, 5),
`Urgency = Immediate`, `RiskTolerance = Aggressive`, issued on tick 1 and
delivered on tick 2. Initial state is the shared spike fixture
(`Setup.sixAgentWorld`, 32 x 32, seed 20260902), resolved by name from
`Corpus.all` rather than serialised.

This table is committed determinism evidence, in the `content/replays/<name>.md`
shape. The source of truth is `envelope-full.cwreplay`'s own `checkpoint`
lines; `tests/CommandoWar.Sim.Tests/ReplayTests.fs` cross-checks this table,
the file's checkpoints, and a fresh `Replay.run` against one another. Replay it:

    dotnet run --project src/CommandoWar.Headless -c Release -- replay-file content/replays/envelope-full.cwreplay

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command file | `envelope-full.cwreplay` |
| Initial state | Shared spike fixture (`Setup.sixAgentWorld`, 32 x 32, seed 20260902) |
| Tick count | 24 |
| Initial hash (tick 0) | `0x50BFA007EDFC42FE` |
| Final hash (tick 24) | `0x4E5963A2C8C83660` |
| Domain events | 72 |

_Re-pinned by TASK-026 (`Canonical.FormatVersion` 2 -> 3, tactical-knowledge
section). The spike fixture is enemy-free, so the tick count (24) and the
domain-event count (72) are unchanged: this is a byte-layout re-pin._

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x7F61018E60700D55` |
|    2 | `0xE5224F773E774224` |
|    3 | `0xA5BD2FE4613A885A` |
|    4 | `0x5F55F90A0B5331C8` |
|    5 | `0xAF4CC50EF3E2E382` |
|    6 | `0x3CCD22AD382F041C` |
|    7 | `0x7ADA46F638E12FF2` |
|    8 | `0x93A0402A1EAF94B8` |
|    9 | `0x1D4D494F97500E2A` |
|   10 | `0x9688354C5999AB54` |
|   11 | `0x5A1D6613ED23300A` |
|   12 | `0x409EF2EC77CB3CD8` |
|   13 | `0x37161F6C64AFA472` |
|   14 | `0x3DBA1BC5D1C9737C` |
|   15 | `0xF11EFDE85DC60752` |
|   16 | `0x10B544C849B13748` |
|   17 | `0xBC74FEBD78A4170A` |
|   18 | `0xA58A1A075B699A44` |
|   19 | `0xBD3C728769ADC77A` |
|   20 | `0x4B745CF8769DE6A8` |
|   21 | `0x135AECE475C9EEC2` |
|   22 | `0x652559E69CAB70A8` |
|   23 | `0x07ADF960581EC576` |
|   24 | `0x4E5963A2C8C83660` |
