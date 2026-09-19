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
| Initial hash (tick 0) | `0x9F8780295282B2D0` |
| Final hash (tick 14) | `0xE6FD3B36EF3C275E` |
| Domain events | 45 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x200547FA01B766A8` |
|    2 | `0x8C2768BBFF0639AF` |
|    3 | `0x4D3713021EFFF972` |
|    4 | `0x0B3CBEB1FBA9A89E` |
|    5 | `0xBF4F2F59002249F0` |
|    6 | `0x5EB0D1F920FD4E19` |
|    7 | `0xFB2651E2B867CAC2` |
|    8 | `0xD14D74B101A1A7A0` |
|    9 | `0x60EA5CC672E88057` |
|   10 | `0xE9D83BA0B22EAE69` |
|   11 | `0xF0089611AF6750DE` |
|   12 | `0xF69059D8EFC457B4` |
|   13 | `0x07EBD4407F88EAE2` |
|   14 | `0xE6FD3B36EF3C275E` |
