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
| Initial hash (tick 0) | `0x82FE58E288B01D32` |
| Final hash (tick 6) | `0x01B601781659B9F1` |
| Domain events | 27 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x29FE50953B55E21B` |
|    2 | `0xB69FD1F27154F4B1` |
|    3 | `0x11E22E3E1BFD9BEB` |
|    4 | `0xE3AD7A98F7CE6ADD` |
|    5 | `0x0CC3C1B2A761E8E3` |
|    6 | `0x01B601781659B9F1` |
