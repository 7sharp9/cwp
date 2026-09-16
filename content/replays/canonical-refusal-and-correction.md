# Replay corpus entry: canonical-refusal-and-correction

The full docs/07 section 8 sequence in one run: the suppress-relieves-exposure geometry (friendly 0, Discipline 1, at (1,3) -> (11,3), hostile 2 at (10,4), friendly 1 at (10,1) suppressing hostile 2) plus a reissue of friendly 0's original (11,3) order on tick 8, once it is already mid-route under the automatic reappraisal. Steps 1-3: friendly 0 is Refused RouteTooExposed threat-agent-2 at tick 1. Step 4: the refusal's DecisionReason is a named, structured reason, never a raw score. Steps 5-6: friendly 1's Suppressing order latches hostile 2's SuppressionBand, Appraisal.routeExposure zeroes its contribution, and friendly 0's order reappraises Accepted at tick 4. Step 7: the player reissues the identical (11,3) intent at tick 8. Step 8: it is Accepted again, with a fresh CommitmentEstablished and no event for the superseded commitment -- consistent with the automatic reappraisal's earlier Accepted (the Adapted/stage-5 half of 'accepts or adapts' is out of scope, no such disposition exists yet). G3's headline evidence item (docs/07 section 9; TASK-038, backlog B-023).

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
| Initial hash (tick 0) | `0xE7F3B652A91AC701` |
| Final hash (tick 14) | `0xB27C590D411B590C` |
| Domain events | 70 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xBC4CA2AD7DFA39FF` |
|    2 | `0x88E873D0090BC27B` |
|    3 | `0x33E974BF1FA7EBED` |
|    4 | `0xE2EC7A0EE270AB33` |
|    5 | `0xFAB4A0AF4B0A76B9` |
|    6 | `0xF03D3D17C593CC64` |
|    7 | `0xC60F32B7ACDA9663` |
|    8 | `0x0F2E324EAC127E43` |
|    9 | `0xFBA473F1810078BD` |
|   10 | `0x1477F7600EA9FA46` |
|   11 | `0x98E33D3AF15D3CFF` |
|   12 | `0x4961C5F0331CC006` |
|   13 | `0xAC73A8B1436D73DF` |
|   14 | `0xB27C590D411B590C` |
