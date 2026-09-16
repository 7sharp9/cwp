# Replay corpus entry: suppress-relieves-exposure

The exposed-approach geometry (friendly 0, Discipline 1, at (1,3) -> (11,3), hostile 2 at (10,4)) plus a second friendly at (10,1) given a Suppress order against hostile 2 on the same tick 1. Friendly 0 is Refused RouteTooExposed at tick 1, exactly as in exposed-approach; friendly 1's Suppressing commitment keeps hostile 2 under fire every tick, and by tick 6 (worst case: every shot a miss) hostile 2's SuppressionBand latches, the new threat-suppression-change reappraisal trigger fires, Appraisal.routeExposure zeroes hostile 2's contribution, and friendly 0's order reappraises Accepted and starts walking. Proves docs/07 section 8 steps 5-6 end to end (TASK-037, a thin B-030 slice pulled forward as P3 decision-support; docs/07 section 9 criterion 4, 'a player action can predictably change an appraisal outcome'; Canonical.FormatVersion 8).

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
| Initial hash (tick 0) | `0xE7F3B652A91AC701` |
| Final hash (tick 10) | `0x6520484590E58463` |
| Domain events | 46 |

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
|    8 | `0xFDF507E757D6EF9A` |
|    9 | `0x1F3B9BB7F4F94874` |
|   10 | `0x6520484590E58463` |
