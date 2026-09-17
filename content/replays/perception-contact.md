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
| Initial hash (tick 0) | `0x74D748C3738329C2` |
| Final hash (tick 14) | `0x5D4C2167AAB83C3F` |
| Domain events | 39 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xDA44E8A1E038B1C0` |
|    2 | `0x42AAEAEED7AC06F6` |
|    3 | `0xF2C04857206C5BB8` |
|    4 | `0xBBAE5914901DF6DA` |
|    5 | `0xBD3111D0FB5CBA5A` |
|    6 | `0xE62C326580ECB173` |
|    7 | `0x5A0426401226E768` |
|    8 | `0x4F36E08C45B5D5FB` |
|    9 | `0xBE79BE1522AFD613` |
|   10 | `0xDF5A7F3D8BFC59D8` |
|   11 | `0x961D0871B311A6AE` |
|   12 | `0x80D3CB5EA0B8A137` |
|   13 | `0x63DB27945E99B7B2` |
|   14 | `0x5D4C2167AAB83C3F` |
