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
| Initial hash (tick 0) | `0xF1A703A752C0F6B9` |
| Final hash (tick 24) | `0x920216777D7DA54A` |
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

_Re-pinned by TASK-033 (backlog B-021; `Canonical.FormatVersion` 5 -> 6,
`AgentState.SuppressionBand` / `AgentState.Stress` added). The spike fixture
is enemy-free, so `SuppressionBand` stays `false` and `Stress` stays 0 at
every checkpoint: the tick count (24) and domain event count (78) are
unchanged, a byte-layout-only re-pin._

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x60D06677F4AC74BA` |
|    2 | `0x126C767D58E0E972` |
|    3 | `0x27D0CD35A17276BC` |
|    4 | `0xF6049A2D434654D2` |
|    5 | `0xD0EAF5929863A358` |
|    6 | `0x56CABEDA0AE277C2` |
|    7 | `0x84D25D9D6C9B904C` |
|    8 | `0x87625094D6BED932` |
|    9 | `0x0D8261A47247A480` |
|   10 | `0xD39B789B332CDFF2` |
|   11 | `0x181FABC7E468EEEC` |
|   12 | `0x7BA7E8AF93296DF2` |
|   13 | `0x7CA36D1D3722EB78` |
|   14 | `0x0FF216E546D0DB22` |
|   15 | `0x3AA09D4BCF53201C` |
|   16 | `0xDFBBEB2FA4CD8692` |
|   17 | `0xB116B21896E4CB10` |
|   18 | `0x6AF6C4B8F5EFF1D2` |
|   19 | `0xF3F21437F1CCD65C` |
|   20 | `0xD729DAB1F1A3A9B2` |
|   21 | `0x8C552F65DE312978` |
|   22 | `0x933D7BE34CCE103E` |
|   23 | `0x810EC513FB1711B8` |
|   24 | `0x920216777D7DA54A` |
