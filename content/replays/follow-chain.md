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
| Initial hash (tick 0) | `0xC488A24B0F6954BC` |
| Final hash (tick 6) | `0x05D37B7AFED002FF` |
| Domain events | 27 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x420DF7B4AA237AB3` |
|    2 | `0x8E8BFC9CCDEA481F` |
|    3 | `0xB9B029FE8BCF1507` |
|    4 | `0xBEB9913FD600B0AB` |
|    5 | `0x5CFD8116D757D66B` |
|    6 | `0x05D37B7AFED002FF` |
