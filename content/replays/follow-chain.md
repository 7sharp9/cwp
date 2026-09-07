# Replay corpus entry: follow-chain

Three friendly agents in a line at (1,3), (2,3), (3,3), all ordered east to (11,3). Each tick the lead agent has a free cell ahead, so TASK-022's vacation-chain resolution lets the whole chain advance on the same tick; no agent reaches (11,3) within the run, so the chain flows every tick with no MovementObstructed.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `follow-chain.cwlog` |
| Initial state | Corpus follow-chain scenario (12 x 9, seed 20260904) |
| Tick count | 6 |
| Initial hash (tick 0) | `0xEC12A1D3F1E445C8` |
| Final hash (tick 6) | `0x49C721DD840E675D` |
| Domain events | 21 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x8EA3AF5092DD58CB` |
|    2 | `0x0183ECC22E445485` |
|    3 | `0xC483C527A8F3F7C3` |
|    4 | `0xD667C20C5A2343E9` |
|    5 | `0x7CC873F6BB8CB20B` |
|    6 | `0x49C721DD840E675D` |
