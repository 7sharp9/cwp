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
| Initial hash (tick 0) | `0xD63C7909BA798617` |
| Final hash (tick 24) | `0x68733B3C55995DAC` |
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

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x5B2918AF0F5886E0` |
|    2 | `0x786DD5BAA13BDD70` |
|    3 | `0x6CE8BBE167E2559A` |
|    4 | `0x3A21D9B47635E38C` |
|    5 | `0xF09F24ABE421C2D2` |
|    6 | `0x09B6DC787CAC5B30` |
|    7 | `0xB48C3242350B5D92` |
|    8 | `0x70223514AFD9B9D4` |
|    9 | `0x7A48DFB42DEC3D4A` |
|   10 | `0xA5D20135F8BC6F50` |
|   11 | `0xDA0DB344B251B36A` |
|   12 | `0x88FB6E99DDA4F0EC` |
|   13 | `0xBA76A4AD462199C2` |
|   14 | `0xA8663EC0B0425990` |
|   15 | `0x82491ACF07482C52` |
|   16 | `0x9B2DF465678C3E04` |
|   17 | `0x3359857CC1CE856A` |
|   18 | `0xC48412FC9BC68710` |
|   19 | `0x8BE281262650045A` |
|   20 | `0xCCC1566EF6E2B98C` |
|   21 | `0x46694DCA23F077B2` |
|   22 | `0xFF99FFE82C7A0EE4` |
|   23 | `0x5472C392CB697436` |
|   24 | `0x68733B3C55995DAC` |
