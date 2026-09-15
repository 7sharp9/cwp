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
| Initial hash (tick 0) | `0x8BBEC4176AE8B541` |
| Final hash (tick 12) | `0x61BDB76F6DE3D928` |
| Domain events | 45 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xB1EBA36EC0A977F4` |
|    2 | `0xDCA3ABB99644988F` |
|    3 | `0x5CB7C7DB4807E915` |
|    4 | `0xC67E960B45F350D0` |
|    5 | `0xC56FEEF84296F591` |
|    6 | `0x05BA2DA29DE73620` |
|    7 | `0x30EE64E23C5976E8` |
|    8 | `0x150F4CC2612F83FD` |
|    9 | `0x41549E849C43DA9F` |
|   10 | `0x722C1706FA0D0368` |
|   11 | `0x08E2B7057CC419FA` |
|   12 | `0x61BDB76F6DE3D928` |
