# Replay corpus entry: perception-contact

One friendly agent at (1,5) ordered east to (9,5); a stationary hostile agent 1 at (9,1) behind an opaque impassable wall at x=6, rows 0..3. The hostile is inside PerceptionConfig.SightRange from the start but line of sight is blocked; once the friendly clears the wall the Perception phase emits ContactObserved and the Tactical-knowledge phase adds the contact to the shared squad picture (WorldState.TacticalKnowledge). The first corpus entry with an enemy deployment (TASK-026, backlog B-015; the 'Unknown threat' shape, docs/05 section 16). The order is issued on tick 1, before the contact is known, so it is Accepted at appraisal and not re-judged when the contact appears (TASK-028; reappraisal on a knowledge change is B-021). Re-pinned by TASK-045 (backlog B-031; Canonical.FormatVersion 9 -> 10, AgentState.Vitals added): the same wall gap that opens line of sight for ContactObserved also brings both agents into CombatConfig.WeaponRange, so Combat (phase 8, after Perception) engages the same tick and both take real fire -- the friendly is Incapacitated by tick 8, and since it is the squad's only agent, leadership transfers to None the same tick (LeadershipTransferred). The entry's own subject, ContactObserved/KnownContact at tick 5, is unaffected (Perception always runs before Combat); the tick-5 golden simply gains the friendly's already-wounded AgentVitals entry alongside it, from that same tick's own combat.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `perception-contact.cwreplay` |
| Initial state | Corpus perception-contact scenario (12 x 8, seed 20260904, 1 friendly + 1 hostile) |
| Tick count | 14 |
| Initial hash (tick 0) | `0x7675113FF643B1BB` |
| Final hash (tick 14) | `0x28E605A58B6971FD` |
| Domain events | 32 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x978F63C7319879D9` |
|    2 | `0x1F4058AB23D0C71F` |
|    3 | `0x9EB6BDD25450B601` |
|    4 | `0x06781C64A3B5B81B` |
|    5 | `0x4F81E9CDB7E18E2B` |
|    6 | `0x2BD63075AA3DF6BD` |
|    7 | `0x0F67CB7F6A7F82EF` |
|    8 | `0xBD1B5F3DCB639770` |
|    9 | `0x67985B934A83209B` |
|   10 | `0x57AAA1B1A53E709F` |
|   11 | `0x764934A0188C5855` |
|   12 | `0x70F8367C9C66137F` |
|   13 | `0x3CF899D2AAAF5C58` |
|   14 | `0x28E605A58B6971FD` |
