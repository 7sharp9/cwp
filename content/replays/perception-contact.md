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
| Initial hash (tick 0) | `0x7832BEFCB2632ACD` |
| Final hash (tick 14) | `0x94E97C162F865F75` |
| Domain events | 32 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x5E46E005E57C22B7` |
|    2 | `0x60231E935B4EB651` |
|    3 | `0x42A30AA231D7C1D7` |
|    4 | `0x4A1982E24485970D` |
|    5 | `0xDC021A6CE3303543` |
|    6 | `0x8D43C72DEDC9ABEB` |
|    7 | `0x00F0196F3BFDFBA3` |
|    8 | `0xE8857547393578FE` |
|    9 | `0x8A23A4045EBA2939` |
|   10 | `0xD13F5623A86968DF` |
|   11 | `0x584117E9436895BA` |
|   12 | `0x1BB47148301A8BA1` |
|   13 | `0xBD0B42B97FD03A71` |
|   14 | `0x94E97C162F865F75` |
