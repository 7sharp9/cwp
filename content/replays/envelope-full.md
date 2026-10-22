# Replay corpus entry: envelope-full

A committed replay in the production replay-command format
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
| Initial hash (tick 0) | `0x5049F6F0E9FCA1E2` |
| Final hash (tick 24) | `0xA99BAC28A2BF7296` |
| Domain events | 78 |

The one command addresses three recipients (agents 3, 4, 5) through
`Command.moveToMany`, but none of them authors a `FormationOffset` (the shared
spike fixture has no formation), so `ReceivedOrder.AsGroup` makes no difference
to the resolved target. The fixture is enemy-free, so all three recipients
Accept at appraisal.

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x38F956EF2922EFB1` |
|    2 | `0x6D80C63D50E94D3E` |
|    3 | `0xD7C7A94E8E366EDE` |
|    4 | `0x101615A573F46652` |
|    5 | `0xD80BFF89A689D592` |
|    6 | `0x387AD441DFD84906` |
|    7 | `0x2D9DEEF28607B26E` |
|    8 | `0x78F48930CF1B5AA2` |
|    9 | `0x3454E9D586F36F4A` |
|   10 | `0x224D85ADFE79D83E` |
|   11 | `0xC1E0585D6374098E` |
|   12 | `0x6230FC63BE34D5B2` |
|   13 | `0xDF7C1225C3D57822` |
|   14 | `0x785771D5FE9D4C76` |
|   15 | `0xCC747A7BD6C8A47E` |
|   16 | `0x12179943729F0662` |
|   17 | `0xDC979B576A4EDBAA` |
|   18 | `0xDB772A8A292167DE` |
|   19 | `0xFC26B4223453F5BE` |
|   20 | `0x7B4BAE35F1974CD2` |
|   21 | `0x267AF7E96D2B5AF2` |
|   22 | `0x3E13748C0DCA2E3E` |
|   23 | `0xE51651C869CD3B4A` |
|   24 | `0xA99BAC28A2BF7296` |
