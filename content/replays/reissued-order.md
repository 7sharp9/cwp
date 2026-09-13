# Replay corpus entry: reissued-order

One friendly agent at (1,4) ordered east to (14,4) on tick 1 (Accepted, CommitmentEstablished), then re-ordered south to (14,8) on tick 3 while still mid-route. The second order supersedes the first: Appraisal re-accepts against the new target and commitmentAndLocalAction emits a fresh CommitmentEstablished for the second command, with no event reporting the first commitment's end (TASK-030, backlog B-018).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `reissued-order.cwlog` |
| Initial state | Corpus reissued-order scenario (16 x 9, seed 20260904, 1 friendly) |
| Tick count | 6 |
| Initial hash (tick 0) | `0xDF06C1D15158B92C` |
| Final hash (tick 6) | `0xAEA4C22D3365702D` |
| Domain events | 12 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x24F7C65F7AD28A0A` |
|    2 | `0x9883F71864C256B8` |
|    3 | `0x6CCDAF9FB22AC507` |
|    4 | `0xDAC9E3CADD5D8999` |
|    5 | `0xA3A2487582D24ADF` |
|    6 | `0xAEA4C22D3365702D` |
