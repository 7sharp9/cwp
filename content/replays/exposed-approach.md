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
| Initial hash (tick 0) | `0xDCF365F0F2BFDB43` |
| Final hash (tick 12) | `0x48B241823BFDE506` |
| Domain events | 62 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x5FDDED09EDC18826` |
|    2 | `0x0AB14D42D37532E1` |
|    3 | `0x4838AE3A100D35C3` |
|    4 | `0x76CACC1D1405F9DA` |
|    5 | `0x0AB7FF938F8FE7FA` |
|    6 | `0xAAC5E6BE53173DA6` |
|    7 | `0xD4BFA2D244152D1C` |
|    8 | `0xA8EA75EF2830FED5` |
|    9 | `0xF474F425BFECBF12` |
|   10 | `0xC498880BB5F8271F` |
|   11 | `0x4F6E0EC8D8465185` |
|   12 | `0x48B241823BFDE506` |
