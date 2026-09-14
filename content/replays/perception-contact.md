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
| Initial hash (tick 0) | `0x786718B458D07686` |
| Final hash (tick 14) | `0x69EDF50137F892C3` |
| Domain events | 35 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xB286FCBDC6E17A94` |
|    2 | `0x78B3FA90137844FA` |
|    3 | `0x64FB7071E79C0274` |
|    4 | `0x412415E9E55CB12E` |
|    5 | `0xFB2123BDFA1E4BF1` |
|    6 | `0x4BDE12876BC2F232` |
|    7 | `0x76ECB589445E30C5` |
|    8 | `0xB037C29CDD31BEE6` |
|    9 | `0x25A7AA469D76E202` |
|   10 | `0x657C00F637D66A74` |
|   11 | `0x7322603429A75F99` |
|   12 | `0xAAA188688314F3E1` |
|   13 | `0x7D997E2A3ECAE653` |
|   14 | `0x69EDF50137F892C3` |
