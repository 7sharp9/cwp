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
| Initial hash (tick 0) | `0x22C1916D956A2C33` |
| Final hash (tick 6) | `0x272A841506CF45C2` |
| Domain events | 12 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x0DBF6BDBE6D00DD9` |
|    2 | `0x41F6D98752815907` |
|    3 | `0xA38B88AD5F5A9994` |
|    4 | `0xE2AA06DFF2A7FB96` |
|    5 | `0xF1BC618A846C1D4C` |
|    6 | `0x272A841506CF45C2` |
