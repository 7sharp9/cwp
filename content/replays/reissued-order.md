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
| Initial hash (tick 0) | `0x95D380964DD74591` |
| Final hash (tick 6) | `0x29DE95C34ADD50CE` |
| Domain events | 12 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x20CCD708544E30B3` |
|    2 | `0x5B9C32163DD06385` |
|    3 | `0xECCE6722117EC2DC` |
|    4 | `0xE37387F9AC7B6132` |
|    5 | `0xE22E71F96ACBE66C` |
|    6 | `0x29DE95C34ADD50CE` |
