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
| Initial hash (tick 0) | `0xE13D7540912C7E25` |
| Final hash (tick 24) | `0x5028174266E2BF6F` |
| Domain events | 72 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x5FABC350D63F6F3E` |
|    2 | `0x45A1FB5F0F9C8AF3` |
|    3 | `0x5A970C9946F376A1` |
|    4 | `0x9B1AEDD62235A377` |
|    5 | `0xEE082B61D4868A61` |
|    6 | `0xADB8332182B5DF33` |
|    7 | `0x249C18C9776F39E9` |
|    8 | `0xD14C0E7299F38B1F` |
|    9 | `0x4819D9644A71D3C9` |
|   10 | `0x28B213CBC8952D93` |
|   11 | `0xC9D1E55CE2849D11` |
|   12 | `0x3F7394ABA2BED6C7` |
|   13 | `0x295053F9B0379E31` |
|   14 | `0x9BFAABFCE7F76213` |
|   15 | `0x67BEA936AE60AF69` |
|   16 | `0x776AAF1B1B1AB47F` |
|   17 | `0x151B3170E9F3E209` |
|   18 | `0x48F48E3264190AB3` |
|   19 | `0xFF71F4130E42E3C1` |
|   20 | `0x577793AED300D4B7` |
|   21 | `0xC35DCC176BDB6CC1` |
|   22 | `0xAE3583ABED756CC7` |
|   23 | `0xD7A561497F041105` |
|   24 | `0x5028174266E2BF6F` |
