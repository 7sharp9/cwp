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
| Initial hash (tick 0) | `0xFE5B4078273C6A2A` |
| Final hash (tick 14) | `0x80E69B76B65847AB` |
| Domain events | 70 |

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
|    8 | `0xB38A2392AD4EFD98` |
|    9 | `0xAF18FCB13D79CAB6` |
|   10 | `0xED165C0BD0724DE1` |
|   11 | `0xAF8707907A4CDAF0` |
|   12 | `0x8868DD86F5817C0D` |
|   13 | `0x9B8F1D2367BC0EA4` |
|   14 | `0x80E69B76B65847AB` |
