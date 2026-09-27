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
| Initial hash (tick 0) | `0x59C95477F649C9D6` |
| Final hash (tick 12) | `0xA2E207DE3FBF6CF1` |
| Domain events | 48 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xA1354EB998FC1B95` |
|    2 | `0xE2E24410C1D27CD2` |
|    3 | `0xE60F361A5CBB2909` |
|    4 | `0x9A70F8A19DA4357F` |
|    5 | `0xA1EE3B17B77D8F10` |
|    6 | `0x309943C78C86ECF0` |
|    7 | `0xCCC23C28D252DD99` |
|    8 | `0xBF50FDAC00BB5A98` |
|    9 | `0x0F6707C5EFBB1A61` |
|   10 | `0x82C32E09E8490D9F` |
|   11 | `0xBC3F7E24969B88EF` |
|   12 | `0xA2E207DE3FBF6CF1` |
