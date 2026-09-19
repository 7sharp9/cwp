# Replay corpus entry: suppress-relieves-exposure

The exposed-approach geometry (friendly 0, Discipline 1, at (1,3) -> (11,3), hostile 2 at (10,4)) plus a second friendly at (10,1) given a Suppress order against hostile 2 on the same tick 1. Friendly 0 is Refused RouteTooExposed at tick 1, exactly as in exposed-approach; friendly 1's Suppressing commitment keeps hostile 2 under fire every tick, and by tick 6 (worst case: every shot a miss) hostile 2's SuppressionBand latches, the new threat-suppression-change reappraisal trigger fires, Appraisal.routeExposure zeroes hostile 2's contribution, and friendly 0's order reappraises Accepted and starts walking. Proves docs/07 section 8 steps 5-6 end to end (TASK-037, a thin B-030 slice pulled forward as P3 decision-support; docs/07 section 9 criterion 4, 'a player action can predictably change an appraisal outcome'; Canonical.FormatVersion 8). Re-pinned by TASK-045 (backlog B-031; Canonical.FormatVersion 9 -> 10, AgentState.Vitals added): friendly 1 and hostile 2 trade real fire at close range and both take genuine wound consequences -- the exposed-approach re-pin note's own analysis applies here identically (same geometry). The docs/07 section 8 steps 5-6 mechanic this entry proves is unaffected: hostile 2's own Suppression/SuppressionBand still latches from incoming fire regardless of which friendly lands it, still zeroes its route-exposure contribution, and friendly 0's order still reappraises Accepted.

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
| Initial hash (tick 0) | `0x9F8780295282B2D0` |
| Final hash (tick 10) | `0x55A43B40223B647A` |
| Domain events | 37 |

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
|    8 | `0x12583A06656885AF` |
|    9 | `0x7AE58AC05F3407B0` |
|   10 | `0x55A43B40223B647A` |
