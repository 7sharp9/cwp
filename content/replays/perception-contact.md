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
| Initial hash (tick 0) | `0x696D03C1F5FDCF46` |
| Final hash (tick 14) | `0xAA93F856AC12585A` |
| Domain events | 32 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x22E4A655637D2600` |
|    2 | `0x8C3728CD064E5E02` |
|    3 | `0xBD070F2652444188` |
|    4 | `0xBF2E8A52561122F6` |
|    5 | `0xC91AE1498E022EFA` |
|    6 | `0x4C2E953E90031BF8` |
|    7 | `0x9EC565FAB121874A` |
|    8 | `0x764E6CA9F20EE649` |
|    9 | `0x6DE71FED7C1533FE` |
|   10 | `0x419799524EC84028` |
|   11 | `0x33A6EF8C1F4B293D` |
|   12 | `0x82ACF0DD94BC3BB6` |
|   13 | `0xAE58E9BC738C259E` |
|   14 | `0xAA93F856AC12585A` |
