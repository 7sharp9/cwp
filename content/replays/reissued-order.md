# Replay corpus entry: reissued-order

One friendly agent at (1,4) ordered east to (14,4) on tick 1 (Accepted, CommitmentEstablished), then re-ordered south to (14,8) on tick 3 while still mid-route. The second order supersedes the first: Appraisal re-accepts against the new target and commitmentAndLocalAction emits a fresh CommitmentEstablished for the second command, with no event reporting the first commitment's end (TASK-030, backlog B-018).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `reissued-order.cwreplay` |
| Initial state | Corpus reissued-order scenario (16 x 9, seed 20260904, 1 friendly) |
| Tick count | 6 |
| Initial hash (tick 0) | `0x76779BB730E80888` |
| Final hash (tick 6) | `0x11A199FA8653B1AB` |
| Domain events | 12 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x2F036F829B288B92` |
|    2 | `0x51967EE1818FE12C` |
|    3 | `0xB8C686AA2DBE9941` |
|    4 | `0xF97876733A8801EF` |
|    5 | `0x7E6C5DE969A6E771` |
|    6 | `0x11A199FA8653B1AB` |
