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
| Initial hash (tick 0) | `0x66E6821517F73AF5` |
| Final hash (tick 14) | `0xCF052F4E1331FFB2` |
| Domain events | 15 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x5F262D8C12960BC3` |
|    2 | `0x57B5AF8272ACFF29` |
|    3 | `0xE25C02B68384FDCB` |
|    4 | `0xFA52F62A0F041E15` |
|    5 | `0xF72BE90D2ACE0BBF` |
|    6 | `0xD566A75AEEE36A6C` |
|    7 | `0xB291993BEC378659` |
|    8 | `0x7A8B99A37939D523` |
|    9 | `0x64A1C885A7EEA15A` |
|   10 | `0x4831BA88E392C47A` |
|   11 | `0xB94E94F6DA7411BE` |
|   12 | `0x13AF84D5192C3C26` |
|   13 | `0x4BC0C141CB5CEDE2` |
|   14 | `0xCF052F4E1331FFB2` |
