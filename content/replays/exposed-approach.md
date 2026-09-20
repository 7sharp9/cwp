# Replay corpus entry: exposed-approach

Two friendlies ordered along the same exposed approach past a stationary hostile (a machine-gun position) the squad sees from the start: agent 0 (Discipline 1) at (1,3) -> (11,3), agent 1 (Discipline 6) at (1,5) -> (11,5), both on tick 1, both routes the same distance past the known threat at (10,4). The divergence is discipline alone: on tick 1 the Appraisal phase (12.5) Refuses agent 0's order (RouteTooExposed, no Destination, it never moves) and Accepts agent 1's (Destination written, it walks the approach). The G3 evidence scenario (docs/07 section 9 criterion 2; TASK-028, backlog B-017; Canonical.FormatVersion 4). Re-pinned by TASK-045 (backlog B-031; Canonical.FormatVersion 9 -> 10, AgentState.Vitals added): not behaviour-neutral past tick 1 -- agent 1, walking its Accepted route, closes within combat range of the stationary hostile from tick 2 and both trade real fire, eventually incapacitating agent 1 (tick 5) and the hostile (tick 7); agent 0 (Refused, never moves, never in range) stays clear. The tick-1 divergence this entry exists to prove happens before any shot is fired and is unaffected.

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
| Initial hash (tick 0) | `0xFA0A225AE6FF9E8D` |
| Final hash (tick 12) | `0xA313B34FF26D3756` |
| Domain events | 48 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xF0169E93B40D5546` |
|    2 | `0xEF5B1F1579658BD7` |
|    3 | `0x3D61438DEE136DCA` |
|    4 | `0xEBDC0DCC0378F6A2` |
|    5 | `0x0F17C3E4591CC033` |
|    6 | `0xF5D12155D07989BA` |
|    7 | `0x0FC6BA25E45E2825` |
|    8 | `0xDEF6F59E3D85E098` |
|    9 | `0x06C336EB5D847A09` |
|   10 | `0xD3AD5B660EEDDF7B` |
|   11 | `0xEED77215D5D707A0` |
|   12 | `0xA313B34FF26D3756` |
