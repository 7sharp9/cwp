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
| Initial hash (tick 0) | `0xBB129AC148DFA754` |
| Final hash (tick 14) | `0x8902E1B6014E5611` |
| Domain events | 39 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x86B503C9F989BB9E` |
|    2 | `0x053F4C948D61E430` |
|    3 | `0x185F5DB824B95EBE` |
|    4 | `0x933B4EC3D55394C4` |
|    5 | `0xB0072D4A340AE028` |
|    6 | `0x570338D6230C4D45` |
|    7 | `0x026CAEA07F8AE75E` |
|    8 | `0x8B9355057978EF0D` |
|    9 | `0xCFF5C2158BF06BA9` |
|   10 | `0xC42C9AE1843B14E6` |
|   11 | `0xBB4EEAE297466320` |
|   12 | `0x9B8A4379F3A6E801` |
|   13 | `0xFFEA4B9FF22DAD5C` |
|   14 | `0x8902E1B6014E5611` |
