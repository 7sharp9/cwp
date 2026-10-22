# Replay corpus entry: suppress-relieves-exposure

The exposed-approach geometry (friendly 0, Discipline 1, at (1,3) -> (11,3), hostile 2 at (10,4)) plus a second friendly at (10,1) given a Suppress order against hostile 2 on the same tick 1. Friendly 0 is Refused RouteTooExposed at tick 1, exactly as in exposed-approach; friendly 1's Suppressing commitment keeps hostile 2 under fire every tick, and by tick 6 even in the worst case (every shot a miss) hostile 2's SuppressionBand latches, the threat-suppression-change reappraisal trigger fires, Appraisal.routeExposure zeroes hostile 2's contribution, and friendly 0's order reappraises Accepted and starts walking. Proves docs/07 section 8 steps 5-6 end to end (TASK-037; docs/07 section 9 criterion 4, 'a player action can predictably change an appraisal outcome'). Friendly 1 and hostile 2 also trade real fire at close range and both take wounds; hostile 2's SuppressionBand latches from incoming fire whichever friendly lands it.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `suppress-relieves-exposure.cwreplay` |
| Initial state | Corpus suppress-relieves-exposure scenario (12 x 8, seed 20260904, 2 friendlies Discipline 1 / default + 1 hostile) |
| Tick count | 10 |
| Initial hash (tick 0) | `0xF55656DDA9247A8F` |
| Final hash (tick 10) | `0x17260DFE422F9018` |
| Domain events | 37 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xAD48481B7B3195F3` |
|    2 | `0x552E34EF678BE1A4` |
|    3 | `0x8577C832FE4DDE73` |
|    4 | `0x2509839936BC996B` |
|    5 | `0xDD584F2546A34E65` |
|    6 | `0xB5E9427C20274385` |
|    7 | `0xD6FD8469EFCEEA36` |
|    8 | `0x9E7D920D2B2790E7` |
|    9 | `0xB8CB73F38334F9BE` |
|   10 | `0x17260DFE422F9018` |
