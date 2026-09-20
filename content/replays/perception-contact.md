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
| Initial hash (tick 0) | `0xADA49871825B75D3` |
| Final hash (tick 14) | `0xBB7B1D511953531D` |
| Domain events | 33 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x97376AD2F8350255` |
|    2 | `0xFF3334FBB47C8E87` |
|    3 | `0xAEE92EAC02CF4F6D` |
|    4 | `0xB768EF933ED0A063` |
|    5 | `0x83E8E1BE8877836D` |
|    6 | `0xA7015E0586323A07` |
|    7 | `0x8A34678BC29C6A7D` |
|    8 | `0x2121D7D25A1F3522` |
|    9 | `0x76415E64DE373D1D` |
|   10 | `0xEBDC84C096913CD1` |
|   11 | `0xDF5480EDCE752669` |
|   12 | `0xEBE4F0ABEBA1BB97` |
|   13 | `0x8C22EE63195D9008` |
|   14 | `0xBB7B1D511953531D` |
