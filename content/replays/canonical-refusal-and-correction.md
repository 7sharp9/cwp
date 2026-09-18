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
| Initial hash (tick 0) | `0x24C7DF529357546B` |
| Final hash (tick 14) | `0x7EF8660B22C3497E` |
| Domain events | 45 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x331261EBE2DC1FA5` |
|    2 | `0x11F83D4CAFE5162A` |
|    3 | `0x77041258355387BF` |
|    4 | `0x792DFAF7494A3909` |
|    5 | `0x80539EA6E9B8F558` |
|    6 | `0x952B0D5AC83FA65B` |
|    7 | `0x21465D4CA259452C` |
|    8 | `0x4A1D0E1A9348AC06` |
|    9 | `0x7632E240715F8F59` |
|   10 | `0x644423BD9FD77BC7` |
|   11 | `0xF67CA91F0841D136` |
|   12 | `0xC32040325686D784` |
|   13 | `0x54C71E6DBE73BA5E` |
|   14 | `0x7EF8660B22C3497E` |
