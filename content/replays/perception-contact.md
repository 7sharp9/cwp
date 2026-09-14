# Replay corpus entry: perception-contact

One friendly agent at (1,5) ordered east to (9,5); a stationary hostile agent 1 at (9,1) behind an opaque impassable wall at x=6, rows 0..3. The hostile is inside PerceptionConfig.SightRange from the start but line of sight is blocked; once the friendly clears the wall the Perception phase emits ContactObserved and the Tactical-knowledge phase adds the contact to the shared squad picture (WorldState.TacticalKnowledge). The first corpus entry with an enemy deployment (TASK-026, backlog B-015; the 'Unknown threat' shape, docs/05 section 16). The order is issued on tick 1, before the contact is known, so it is Accepted at appraisal and not re-judged when the contact appears (TASK-028; reappraisal on a knowledge change is B-021).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `perception-contact.cwlog` |
| Initial state | Corpus perception-contact scenario (12 x 8, seed 20260904, 1 friendly + 1 hostile) |
| Tick count | 14 |
| Initial hash (tick 0) | `0x564C9070D30D84CB` |
| Final hash (tick 14) | `0xE83642F877D1C73C` |
| Domain events | 39 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xCB12A896420FA9DD` |
|    2 | `0x16C52627B12D047F` |
|    3 | `0xBA9CAD0F53E789F5` |
|    4 | `0x0F5D9F97454116FB` |
|    5 | `0x2A29EE05276908C8` |
|    6 | `0x285159BCD1C84A65` |
|    7 | `0xCB65CFECD7A0C92E` |
|    8 | `0xD5E12679E2A4C485` |
|    9 | `0x1029D2910B658F15` |
|   10 | `0x8D2EB3E5636D0663` |
|   11 | `0x94E86751905553B6` |
|   12 | `0x506B8F1DB5EB14EA` |
|   13 | `0xAA9CAFB8FA93F410` |
|   14 | `0xE83642F877D1C73C` |
