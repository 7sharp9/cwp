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
| Initial hash (tick 0) | `0xB25FE816BCB67A11` |
| Final hash (tick 24) | `0x4B8E7BF14716A306` |
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

_Re-pinned by TASK-034 (backlog B-022, partial; `Canonical.FormatVersion` 6 ->
7, `WorldState.HostileTacticalKnowledge` added). The spike fixture has zero
enemy deployments, so the new section is empty at every checkpoint: the tick
count (24) and domain event count (78) are unchanged, a byte-layout-only
re-pin._

_Re-pinned by TASK-037 (a thin B-030 slice; `Canonical.FormatVersion` 7 -> 8,
`PlayerIntent.Suppress` / `DecisionReason.TargetNotKnown` added inside the
already-canonical `Order` / `Disposition` sections). This entry's one command
is a `MoveTo`, so the byte-layout change is the only difference: the tick
count (24) and domain event count (78) are unchanged._

_Re-pinned by TASK-044 (backlog B-051; `Canonical.FormatVersion` 8 -> 9,
`AgentState.OrderQueue` added to `writeAgent`). This entry's one command is a
Replace-mode `MoveTo` with an always-empty `OrderQueue`, so the byte-layout
change is the only difference: the tick count (24) and domain event count
(78) are unchanged._

_Re-pinned by TASK-045 (backlog B-031; `Canonical.FormatVersion` 9 -> 10,
`AgentState.Vitals` added). This entry's one command has no combat anywhere
near it, so every agent stays `Alive` at full health throughout: the
byte-layout change is the only difference, the tick count (24) and domain
event count (78) are unchanged._

_Re-pinned by TASK-047 (backlog B-030 proper; `Canonical.FormatVersion` 10
-> 11, `AgentState.Ammo` added and three new `PlayerIntent` cases). This
entry's one command is a `MoveTo` with no combat anywhere near it, so no
shot is ever fired and every agent's ammo stays at its full default
throughout: the byte-layout change is the only difference, the tick count
(24) and domain event count (78) are unchanged._

_Re-pinned by TASK-058 (backlog B-016b; `Canonical.FormatVersion` 11 -> 12,
`AgentState.RadioDestroyed` / `.PendingDelivery` added). This entry's
initial state authors no `Headquarters`, so the whole range/delay/jamming/
radio-destroyed feature set stays inert for it: the byte-layout change is
the only difference, the tick count (24) and domain event count (78) are
unchanged._

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xB2018106DE02877A` |
|    2 | `0xBE2B82EA9C56EF4A` |
|    3 | `0xDA95EB0AD29D4A10` |
|    4 | `0xBFE8E9C643861A86` |
|    5 | `0x581BA3867F126218` |
|    6 | `0x6B4B5C395033F45A` |
|    7 | `0x29F2642C45C92338` |
|    8 | `0xF5D5AD41B97E748E` |
|    9 | `0x17060AB849B4B760` |
|   10 | `0xE0EC5EFE2458ABFA` |
|   11 | `0xB08843C392405780` |
|   12 | `0x916D0F181549C9A6` |
|   13 | `0xC27BD74CA5EBC8A8` |
|   14 | `0x8C97E9C6697508EA` |
|   15 | `0xF88F969801BEFC58` |
|   16 | `0x4DE5E53B5446631E` |
|   17 | `0xCB8C1D42F17BE160` |
|   18 | `0x659BAB47B92D288A` |
|   19 | `0x39248E2096BDC890` |
|   20 | `0x5914F75955DD7006` |
|   21 | `0x42A7E64E4E340D98` |
|   22 | `0x4EE7F0B568693906` |
|   23 | `0x81DF645C45A3E46C` |
|   24 | `0x4B8E7BF14716A306` |
