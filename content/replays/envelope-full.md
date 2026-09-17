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
| Initial hash (tick 0) | `0xA2726329BB740614` |
| Final hash (tick 24) | `0x60DE16C440B25F5F` |
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

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xF63F9CFCAB193C03` |
|    2 | `0x56A62BB1E992B717` |
|    3 | `0x9109BE33E753DA75` |
|    4 | `0x9C716BCB0435B75F` |
|    5 | `0xA08967EDF89F2C99` |
|    6 | `0x81FB03556DE6CD3F` |
|    7 | `0x6CD63FF530D320AD` |
|    8 | `0x25F6B5AD6B2A00F7` |
|    9 | `0xD319A3FEC369FDA9` |
|   10 | `0xE3EEFC46D47B18A7` |
|   11 | `0x831E9DF433ED0915` |
|   12 | `0xE274D878A241733F` |
|   13 | `0xCF812B98A3456D99` |
|   14 | `0x58D33FAEB55ECF1F` |
|   15 | `0x1AE3EA8CB1603FDD` |
|   16 | `0xFFCAEA5DC5E53667` |
|   17 | `0xF578F16C1C18C089` |
|   18 | `0x07CC0957A2D252B7` |
|   19 | `0x91A95DE6D93088B5` |
|   20 | `0xE6A177250B4398BF` |
|   21 | `0x3289EAEA86BB4AB9` |
|   22 | `0xF5BCF6382CFFB543` |
|   23 | `0x0D167CAABA5C3351` |
|   24 | `0x60DE16C440B25F5F` |
