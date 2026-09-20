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
| Initial hash (tick 0) | `0x672815D313E0AE51` |
| Final hash (tick 24) | `0xDF048B77995D0552` |
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

_Re-pinned by TASK-062 (backlog B-032; `Canonical.FormatVersion` 12 -> 13,
`AgentState.Extracted` / `WorldState.MissionOutcome` / `.CompletedObjectives`
/ `.ObjectiveProgress` added). This entry's initial state (`Setup.
sixAgentWorld`) authors no `Objectives`, so the whole demolition/extraction/
mission-outcome feature set stays inert for it (`MissionOutcome` stays
`InProgress` the entire run): the byte-layout change is the only
difference, the tick count (24) and domain event count (78) are unchanged._

_Re-pinned by TASK-065 (backlog B-065; `Canonical.FormatVersion` 13 -> 14,
`AgentState.StalledTicks` added). This entry's one command is an ordinary
`MoveTo` with no rival agent and no occupied route cell anywhere near it,
so the new counter stays 0 at every checkpoint: the byte-layout change is
the only difference, the tick count (24) and domain event count (78) are
unchanged._

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xEDEEE60A8D387982` |
|    2 | `0xB019F3CF1627688A` |
|    3 | `0xE8A42FF36E1C083C` |
|    4 | `0x2BC447A90AA5EDB2` |
|    5 | `0xD4C4440F6F3645C0` |
|    6 | `0xAC2928E7A02C513A` |
|    7 | `0x6BAB4B2EA8E3AEBC` |
|    8 | `0x272EF1D2801CEF62` |
|    9 | `0x0A3ADDDCC8376CA8` |
|   10 | `0x2A9CA370FBA72A4A` |
|   11 | `0x3C24F692CA2C51DC` |
|   12 | `0x61F93BE96B378EB2` |
|   13 | `0x7B3FD1D4C86DB600` |
|   14 | `0x2B5A6D8F57507F9A` |
|   15 | `0x560C8396B2A0AD3C` |
|   16 | `0x38CA548EAB702502` |
|   17 | `0xFFB0F910D0FC3458` |
|   18 | `0x38D738CF80A2BB4A` |
|   19 | `0x7FDE64A19CB5407C` |
|   20 | `0x380F778500FE44F2` |
|   21 | `0x1D8147B54DEB7F40` |
|   22 | `0x655817AF44F47D6E` |
|   23 | `0xB1656C4DB2568AF8` |
|   24 | `0xDF048B77995D0552` |
