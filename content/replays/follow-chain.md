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
| Initial hash (tick 0) | `0x676506295D2AAFF3` |
| Final hash (tick 6) | `0x979C38C32DA7F750` |
| Domain events | 27 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x0825429ABC70CABC` |
|    2 | `0xCBE674E979FA19E8` |
|    3 | `0x35FC97EF66BD6AC0` |
|    4 | `0x97682422E6ABD45C` |
|    5 | `0xA15E219E0D45C65C` |
|    6 | `0x979C38C32DA7F750` |
