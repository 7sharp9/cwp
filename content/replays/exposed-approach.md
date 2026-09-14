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
| Initial hash (tick 0) | `0xC5CCF605958EA303` |
| Final hash (tick 12) | `0x4DC5BE3148521DEF` |
| Domain events | 43 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x2FA6E43B32599EE5` |
|    2 | `0x0F0106D0DA51C247` |
|    3 | `0x1C3C53858A6809F2` |
|    4 | `0x75259590C369E4B0` |
|    5 | `0x25DA33FA4AF750F4` |
|    6 | `0x26DB52D1CED61385` |
|    7 | `0x9633535D974F02FE` |
|    8 | `0x3E0481489FBB3334` |
|    9 | `0xF837C4F0A5378835` |
|   10 | `0x96389F2A91EF31EB` |
|   11 | `0x21CC3FDBBB15EEF1` |
|   12 | `0x4DC5BE3148521DEF` |
