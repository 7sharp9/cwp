# Replay corpus entry: exposed-approach

Two friendlies ordered along the same exposed approach past a stationary hostile (a machine-gun position) the squad sees from the start: agent 0 (Discipline 1) at (1,3) -> (11,3), agent 1 (Discipline 6) at (1,5) -> (11,5), both on tick 1, both routes the same distance past the known threat at (10,4). The divergence is discipline alone: on tick 1 the Appraisal phase (12.5) Refuses agent 0's order (RouteTooExposed, no Destination, it never moves) and Accepts agent 1's (Destination written, it walks the approach). The G3 evidence scenario (docs/07 section 9 criterion 2; TASK-028, backlog B-017; Canonical.FormatVersion 4).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `exposed-approach.cwlog` |
| Initial state | Corpus exposed-approach scenario (12 x 8, seed 20260904, 2 friendlies Discipline 1 / 6 + 1 hostile) |
| Tick count | 12 |
| Initial hash (tick 0) | `0xA131B427F023899A` |
| Final hash (tick 12) | `0x7FC37AC9741AF15A` |
| Domain events | 43 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xB03F8419E55F3592` |
|    2 | `0x86FCC87906098029` |
|    3 | `0xF3BB01512613FB1F` |
|    4 | `0x5C177A00FD0F5B76` |
|    5 | `0x611D52903704717C` |
|    6 | `0x2933EB8FDE406538` |
|    7 | `0xAB80376A4BE3293B` |
|    8 | `0x872E9C4E5D4D0DA5` |
|    9 | `0x77085EB513B1ECC4` |
|   10 | `0xFBC9BA2AA98A7A86` |
|   11 | `0x9FF4F0C627939D70` |
|   12 | `0x7FC37AC9741AF15A` |
