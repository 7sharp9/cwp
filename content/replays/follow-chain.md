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
| Initial hash (tick 0) | `0xC34E382E3968F165` |
| Final hash (tick 6) | `0x6D9DCBB66731E8E4` |
| Domain events | 21 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x72F190FDF90D1952` |
|    2 | `0x8E83751DFE09E51C` |
|    3 | `0xB97CE0FE5C13E236` |
|    4 | `0xC6528B499CEB0014` |
|    5 | `0x0904BE3B52504D3A` |
|    6 | `0x6D9DCBB66731E8E4` |
