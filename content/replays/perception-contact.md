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
| Final hash (tick 14) | `0xFFA3433CE5932F42` |
| Domain events | 35 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x5F262D8C12960BC3` |
|    2 | `0x57B5AF8272ACFF29` |
|    3 | `0xE25C02B68384FDCB` |
|    4 | `0xFA52F62A0F041E15` |
|    5 | `0xCE177ECEB4D1F71F` |
|    6 | `0x8AA135678DAD07F7` |
|    7 | `0xD3508C28ED5FC8A8` |
|    8 | `0x24072F488C31F1D5` |
|    9 | `0xC1A67224F26C713F` |
|   10 | `0x65F26D184E51C89D` |
|   11 | `0x1274BD5FD265CA00` |
|   12 | `0xC94536CBE93C2EC4` |
|   13 | `0x45D10A039D3EB2F6` |
|   14 | `0xFFA3433CE5932F42` |
