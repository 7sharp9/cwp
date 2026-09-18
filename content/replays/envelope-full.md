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
| Initial hash (tick 0) | `0xF762ECD4377B5E68` |
| Final hash (tick 24) | `0xC8AF4FBD8A648EF7` |
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

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xD8D6EA029CE786FF` |
|    2 | `0xE1258286D1525A23` |
|    3 | `0x8ED9BE573FAF4BDD` |
|    4 | `0xB6671535A9B163C7` |
|    5 | `0x88AB633616EA8F95` |
|    6 | `0xCC11085DFF43BA83` |
|    7 | `0xD85317A38254EB2D` |
|    8 | `0xBA81D866991F716F` |
|    9 | `0xA4E5ECE9F803CF75` |
|   10 | `0x4F0CFDD88A73EAA3` |
|   11 | `0xB87F63CFA8A9FB7D` |
|   12 | `0x31B8B4F39A9D7FA7` |
|   13 | `0x9EEC68C3548D7F85` |
|   14 | `0xC5C859A3D8E440E3` |
|   15 | `0x972DAFCD9AC8C26D` |
|   16 | `0xC60296D3C52D00DF` |
|   17 | `0xB4DFC1E9CC615605` |
|   18 | `0xA0212A20E6DFD563` |
|   19 | `0x2B0A80D1FBF6B5DD` |
|   20 | `0xF47B40977E56D407` |
|   21 | `0xB25CD157382A5A95` |
|   22 | `0xB14B38A299CD65D7` |
|   23 | `0x50410EC67BF7A651` |
|   24 | `0xC8AF4FBD8A648EF7` |
