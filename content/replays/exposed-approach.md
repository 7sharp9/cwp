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
| Initial hash (tick 0) | `0x43C07501D99419BA` |
| Final hash (tick 12) | `0x65F1C76367050210` |
| Domain events | 45 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x2066BC1FAF990E4A` |
|    2 | `0xBF594A858CF9E778` |
|    3 | `0x734A69DC72A68CE5` |
|    4 | `0x9D94231EA30582A9` |
|    5 | `0xB147127D09A5181F` |
|    6 | `0x6922B8F7843C62E7` |
|    7 | `0xFB4015720E327FBC` |
|    8 | `0x0837BCD7DBB0AE08` |
|    9 | `0x02C802137F54E22D` |
|   10 | `0x4BE9105CFF79429B` |
|   11 | `0xD3E07D22247E5162` |
|   12 | `0x65F1C76367050210` |
