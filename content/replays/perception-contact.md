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
| Initial hash (tick 0) | `0x58203DF069E0C29C` |
| Final hash (tick 14) | `0x1464E0E4D378DC26` |
| Domain events | 33 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x6F1C4724A7D90334` |
|    2 | `0xB2F245A988F6364A` |
|    3 | `0xC46701E7BDE03194` |
|    4 | `0xF04808BBF8620AA6` |
|    5 | `0x6A8212E861916C1C` |
|    6 | `0x7615CA41B0D0D144` |
|    7 | `0x56F90D0C34AB3F7E` |
|    8 | `0xA12A5CB6B14D57A9` |
|    9 | `0x351B5E7232A2D3CA` |
|   10 | `0xEE8A80E53D2A1C62` |
|   11 | `0x571C907998034742` |
|   12 | `0x657F463DD13312C8` |
|   13 | `0xF88DC2595540474F` |
|   14 | `0x1464E0E4D378DC26` |
