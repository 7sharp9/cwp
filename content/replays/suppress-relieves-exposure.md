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
| Initial hash (tick 0) | `0xFE5B4078273C6A2A` |
| Final hash (tick 10) | `0x30E4B1F7CCE50710` |
| Domain events | 46 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x8E9D50E5225E3256` |
|    2 | `0x70A15C378B1E167A` |
|    3 | `0xB748E852D38A61FC` |
|    4 | `0xE3D6F07B6A6979E0` |
|    5 | `0x9C5701DFA96D3D8E` |
|    6 | `0x896D535EC3FE0E4B` |
|    7 | `0xB72271B3243B3940` |
|    8 | `0x2B03CC08920C3205` |
|    9 | `0x9DDDAA7BC8FF886B` |
|   10 | `0x30E4B1F7CCE50710` |
