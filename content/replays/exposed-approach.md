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
| Initial hash (tick 0) | `0x4C31E03AAE90566B` |
| Final hash (tick 12) | `0xB063C6EBDBE0EE3B` |
| Domain events | 48 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x066D3D38E3EB31BE` |
|    2 | `0xE87F33254C39FD3D` |
|    3 | `0x74ED62929C5370F6` |
|    4 | `0x0983C7A1449DD740` |
|    5 | `0xE63FE1452710A389` |
|    6 | `0x67BED57B34CD78CA` |
|    7 | `0xBA862E8DC80349B4` |
|    8 | `0x87BAC27922ED189D` |
|    9 | `0x39042187AA6E1F7E` |
|   10 | `0x2F1B620E57042946` |
|   11 | `0x983ED415FD53CBBB` |
|   12 | `0xB063C6EBDBE0EE3B` |
