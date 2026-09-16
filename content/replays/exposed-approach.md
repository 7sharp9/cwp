# Replay corpus entry: exposed-approach

Two friendlies ordered along the same exposed approach past a stationary hostile (a machine-gun position) the squad sees from the start: agent 0 (Discipline 1) at (1,3) -> (11,3), agent 1 (Discipline 6) at (1,5) -> (11,5), both on tick 1, both routes the same distance past the known threat at (10,4). The divergence is discipline alone: on tick 1 the Appraisal phase (12.5) Refuses agent 0's order (RouteTooExposed, no Destination, it never moves) and Accepts agent 1's (Destination written, it walks the approach). The G3 evidence scenario (docs/07 section 9 criterion 2; TASK-028, backlog B-017; Canonical.FormatVersion 4).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `exposed-approach.cwreplay` |
| Initial state | Corpus exposed-approach scenario (12 x 8, seed 20260904, 2 friendlies Discipline 1 / 6 + 1 hostile) |
| Tick count | 12 |
| Initial hash (tick 0) | `0xEFF07AC93A13BAEC` |
| Final hash (tick 12) | `0xFF3DA86DB0C5352D` |
| Domain events | 62 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x5D5A30C0DF64AC93` |
|    2 | `0xAFC411E5E6979D4C` |
|    3 | `0x14567878E98457DE` |
|    4 | `0xB5545457A6EF0CE3` |
|    5 | `0x0D47424B061A98C9` |
|    6 | `0xDF29179504A913B5` |
|    7 | `0x9686C2B7F4012FE3` |
|    8 | `0x1B789C96EF7A7CE2` |
|    9 | `0xA0EC08DBA64CFE45` |
|   10 | `0x6A643215F64966D4` |
|   11 | `0xBB598F072389134A` |
|   12 | `0xFF3DA86DB0C5352D` |
