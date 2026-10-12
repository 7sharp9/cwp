# Replay corpus entry: exposed-approach

Two friendlies ordered along the same exposed approach past a stationary hostile (a machine-gun position) the squad sees from the start: agent 0 (Discipline 1) at (1,3) -> (11,3), agent 1 (Discipline 6) at (1,5) -> (11,5), both on tick 1, both routes the same distance past the known threat at (10,4). The divergence is discipline alone: on tick 1 the Appraisal phase (12.5) Refuses agent 0's order (RouteTooExposed, no Destination, it never moves) and Accepts agent 1's (Destination written, it walks the approach). The G3 evidence scenario (docs/07 section 9 criterion 2; TASK-028, backlog B-017; Canonical.FormatVersion 4). Re-pinned by TASK-045 (backlog B-031; Canonical.FormatVersion 9 -> 10, AgentState.Vitals added): not behaviour-neutral past tick 1 -- agent 1, walking its Accepted route, closes within combat range of the stationary hostile and both trade real fire. Re-pinned by TASK-083 (backlog B-082): agent 1 is Refused on tick 3 (RouteTooExposed, hostile 2) once the hostile has it in range and the route would close on it, and stops at (3,5); on tick 5 the hostile's SuppressionBand latches, both orders are reappraised Accepted, agent 1 steps once and is incapacitated (tick 5), the hostile follows (tick 7), and agent 0 (Refused until tick 5) then walks the approach. The tick-1 divergence this entry exists to prove happens before any shot is fired and is unaffected.

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
| Final hash (tick 12) | `0x4CBC9F6B94EFC4D9` |
| Domain events | 45 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xA1354EB998FC1B95` |
|    2 | `0xE2E24410C1D27CD2` |
|    3 | `0x39C667B2533285F6` |
|    4 | `0x649434789A4EE586` |
|    5 | `0x4D5FB88B83635D1C` |
|    6 | `0x93969077BFC23A84` |
|    7 | `0x8CCBF195FA309FD9` |
|    8 | `0xF6ADDC48F633CBC4` |
|    9 | `0x4ED086EF9485CC05` |
|   10 | `0xCE458B013EC3BF8B` |
|   11 | `0xE74A5D979CA60077` |
|   12 | `0x4CBC9F6B94EFC4D9` |
