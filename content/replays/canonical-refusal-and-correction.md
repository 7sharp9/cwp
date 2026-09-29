# Replay corpus entry: canonical-refusal-and-correction

The full docs/07 section 8 sequence in one run: the suppress-relieves-exposure geometry (friendly 0, Discipline 1, at (1,3) -> (11,3), hostile 2 at (10,4), friendly 1 at (10,1) suppressing hostile 2) plus a reissue of friendly 0's original (11,3) order on tick 8, once it is already mid-route under the automatic reappraisal. Steps 1-3: friendly 0 is Refused RouteTooExposed threat-agent-2 at tick 1. Step 4: the refusal's DecisionReason is a named, structured reason, never a raw score. Steps 5-6: friendly 1's Suppressing order latches hostile 2's SuppressionBand, Appraisal.routeExposure zeroes its contribution, and friendly 0's order reappraises Accepted at tick 4. Step 7: the player reissues the identical (11,3) intent at tick 8. Step 8: it is Accepted again, with a fresh CommitmentEstablished and no event for the superseded commitment -- consistent with the automatic reappraisal's earlier Accepted (the Adapted/stage-5 half of 'accepts or adapts' is out of scope, no such disposition exists yet). G3's headline evidence item (docs/07 section 9; TASK-038, backlog B-023). Re-pinned by TASK-045 (backlog B-031; Canonical.FormatVersion 9 -> 10, AgentState.Vitals added): the same friendly-1/hostile-2 close-range fire as suppress-relieves-exposure now wounds both -- friendly 1 is Incapacitated by tick 4 (its own Suppressing order then appraises Unable(CriticallyWounded)), hostile 2 follows at tick 5. Steps 1-4 and 7-8 (the reissue, the fresh CommitmentEstablished, arrival at tick 13) are unaffected: hostile 2's Suppression/SuppressionBand still latches from either friendly's fire, still zeroes its contribution for friendly 0 (steps 5-6), and friendly 0 stays Alive and unwounded throughout.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `canonical-refusal-and-correction.cwreplay` |
| Initial state | Corpus canonical-refusal-and-correction scenario (12 x 8, seed 20260904, 2 friendlies Discipline 1 / default + 1 hostile) |
| Tick count | 14 |
| Initial hash (tick 0) | `0xF55656DDA9247A8F` |
| Final hash (tick 14) | `0xFC03C6DC0AFD699F` |
| Domain events | 45 |

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
|    8 | `0x1960EEE94E38280C` |
|    9 | `0xB59869B20A7D21D5` |
|   10 | `0xB4D2C10BC7DEE8FB` |
|   11 | `0xC2C0C4D1A5447AEB` |
|   12 | `0xEBB4DC0343234E6F` |
|   13 | `0x65F7F0215125F7CD` |
|   14 | `0xFC03C6DC0AFD699F` |
