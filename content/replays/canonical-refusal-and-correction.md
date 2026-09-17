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
| Initial hash (tick 0) | `0xE4CBC8602B422C0E` |
| Final hash (tick 14) | `0x66D755F444908ECB` |
| Domain events | 45 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x306202B8A9B68A1C` |
|    2 | `0xF3D274E166DD0E4D` |
|    3 | `0xFC8A62EA5E837632` |
|    4 | `0x6C99B66808F66B0C` |
|    5 | `0x2E42FE9ED9F09C51` |
|    6 | `0xAA69977530BEDD0A` |
|    7 | `0x683B885B128934AD` |
|    8 | `0x75DD1AEFA71D3C23` |
|    9 | `0xB72FED3BB1A5A784` |
|   10 | `0xAD2200E5378F09E2` |
|   11 | `0xFB63D895AB6DA737` |
|   12 | `0x85B49C75A2F37F65` |
|   13 | `0x27AF1B4E13BBF737` |
|   14 | `0x66D755F444908ECB` |
