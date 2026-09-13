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
| Final hash (tick 12) | `0xFA048FA702798874` |
| Domain events | 21 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xB03F8419E55F3592` |
|    2 | `0x36F41653EE3EAFC1` |
|    3 | `0x3522BF8A12B9F594` |
|    4 | `0xCADD6A67777E8B6B` |
|    5 | `0x7B3A437146FD91FE` |
|    6 | `0x07D293DE80243115` |
|    7 | `0x2819FAC3108EC060` |
|    8 | `0xD4D6D294661C10BF` |
|    9 | `0x185EC35B085ED35A` |
|   10 | `0xC351DFED24362D1A` |
|   11 | `0x7809C2DBD0FB461C` |
|   12 | `0xFA048FA702798874` |
