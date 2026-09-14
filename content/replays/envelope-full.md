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
| Initial hash (tick 0) | `0xBDB4025E40BBFA28` |
| Final hash (tick 24) | `0x1403E503D36A5813` |
| Domain events | 78 |

_Re-pinned by TASK-028 (`Canonical.FormatVersion` 3 -> 4, `AgentState.Order` /
`AgentState.Disposition` sections). The spike fixture is enemy-free, so all
three recipients Accept at appraisal: the tick count (24) is unchanged and the
only behaviour change is one `OrderAppraised` event per recipient
(72 -> 75 domain events)._

_Domain event count only, updated by TASK-030 (backlog B-018): the new
`commitmentAndLocalAction` phase emits one `CommitmentEstablished` per
recipient when its order is Accepted (75 -> 78); all three recipients are
still mid-route at tick 24, so no `CommitmentCompleted` fires. No hash on this
table moved — `Commitment` is a derived value, not canonical state (Decision
B), and `Canonical.FormatVersion` stays 4._

_Re-pinned by TASK-032 (backlog B-020; `Canonical.FormatVersion` 4 -> 5,
`AgentState.Suppression` added). The spike fixture is enemy-free, so
`Suppression` stays 0 at every checkpoint: the tick count (24) and domain
event count (78) are unchanged, a byte-layout-only re-pin._

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x813B59B6058BB6FF` |
|    2 | `0xD6CBDABD53ACF18F` |
|    3 | `0x1B0A6ACCED8C6F11` |
|    4 | `0xFC22F9419F2DFE1F` |
|    5 | `0x6CE48B0F0A1BB7F5` |
|    6 | `0x62109E2642191C1F` |
|    7 | `0xFC9A3A83E2F249E9` |
|    8 | `0x6DB6139CEEB2240F` |
|    9 | `0xD6E88CDDEC90DDC5` |
|   10 | `0xE5FAABD535439BFF` |
|   11 | `0xCEB8D6B2EEDA0AD1` |
|   12 | `0xA65E0F60831B02BF` |
|   13 | `0x1F69AA84AA7BA665` |
|   14 | `0xFDE8DF79CE19E58F` |
|   15 | `0xAEE39DDDA2181999` |
|   16 | `0x1EC504A05D219D0F` |
|   17 | `0x94D5BB3795F55375` |
|   18 | `0x4C829F0B48785A2F` |
|   19 | `0x5E374FBEA2473611` |
|   20 | `0x71AC2898C155529F` |
|   21 | `0x613573A6F11B4DF5` |
|   22 | `0xD6E26896B334BE5F` |
|   23 | `0xCD84ECAFE5CDF82D` |
|   24 | `0x1403E503D36A5813` |
