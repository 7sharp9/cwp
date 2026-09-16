# Replay corpus entry: perception-contact

One friendly agent at (1,5) ordered east to (9,5); a stationary hostile agent 1 at (9,1) behind an opaque impassable wall at x=6, rows 0..3. The hostile is inside PerceptionConfig.SightRange from the start but line of sight is blocked; once the friendly clears the wall the Perception phase emits ContactObserved and the Tactical-knowledge phase adds the contact to the shared squad picture (WorldState.TacticalKnowledge). The first corpus entry with an enemy deployment (TASK-026, backlog B-015; the 'Unknown threat' shape, docs/05 section 16). The order is issued on tick 1, before the contact is known, so it is Accepted at appraisal and not re-judged when the contact appears (TASK-028; reappraisal on a knowledge change is B-021).

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
| Initial hash (tick 0) | `0x8B0369638C46B259` |
| Final hash (tick 14) | `0xBFB62138CA829FDC` |
| Domain events | 39 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x6CE4872EB4EAED07` |
|    2 | `0xAA305D4D84074FED` |
|    3 | `0xF5FB677EF7DD1AE7` |
|    4 | `0xDF948279AAFB1ED9` |
|    5 | `0x48653BB02D3F0F29` |
|    6 | `0x32252D7BB1B63C60` |
|    7 | `0x641E8E72F2671D13` |
|    8 | `0xDD16948B98FB01F0` |
|    9 | `0x0A23E8ED067C8B4C` |
|   10 | `0xC66B896D24696FE3` |
|   11 | `0xA83869D5BAF6821D` |
|   12 | `0xDA4BED2F9829A5DC` |
|   13 | `0x6E20E77F939456C1` |
|   14 | `0xBFB62138CA829FDC` |
