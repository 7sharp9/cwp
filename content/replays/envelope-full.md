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
| Initial hash (tick 0) | `0x55F43D66C7AECB7F` |
| Final hash (tick 24) | `0xDC87FE7A73395380` |
| Domain events | 75 |

_Re-pinned by TASK-028 (`Canonical.FormatVersion` 3 -> 4, `AgentState.Order` /
`AgentState.Disposition` sections). The spike fixture is enemy-free, so all
three recipients Accept at appraisal: the tick count (24) is unchanged and the
only behaviour change is one `OrderAppraised` event per recipient
(72 -> 75 domain events)._

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xA9374E4F0EA2E448` |
|    2 | `0xB7FA12180E9F3DEC` |
|    3 | `0xCC477258D4A33766` |
|    4 | `0xB982733B127A2424` |
|    5 | `0x3E676CBC567678DA` |
|    6 | `0x9F4DF5F7F021DED4` |
|    7 | `0xBC62DD4E1B6DFD0E` |
|    8 | `0x8B228541DF2D4ADC` |
|    9 | `0x671C7D9FEA09DECA` |
|   10 | `0xB2D8D135C2B7083C` |
|   11 | `0xF4D945ECA6049E16` |
|   12 | `0x09C4DB25916C5044` |
|   13 | `0xF4C82C0D8581FE2A` |
|   14 | `0x18E4B9B9A66EE034` |
|   15 | `0x72E74D047DA5856E` |
|   16 | `0xC64C854B7895BDCC` |
|   17 | `0xF06200294309CE9A` |
|   18 | `0x8C205832A675C28C` |
|   19 | `0x6C30FAFD9CDAA926` |
|   20 | `0x10ADDDCB7664EAC4` |
|   21 | `0xBF5DC6ABE857C6DA` |
|   22 | `0x796A76415012C5EC` |
|   23 | `0xBAB47363532D9032` |
|   24 | `0xDC87FE7A73395380` |
