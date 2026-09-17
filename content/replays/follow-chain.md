# Replay corpus entry: follow-chain

Three friendly agents in a line at (1,3), (2,3), (3,3), all ordered east to (11,3). Each tick the lead agent has a free cell ahead, so TASK-022's vacation-chain resolution lets the whole chain advance on the same tick; no agent reaches (11,3) within the run, so the chain flows every tick with no MovementObstructed.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `follow-chain.cwreplay` |
| Initial state | Corpus follow-chain scenario (12 x 9, seed 20260904) |
| Tick count | 6 |
| Initial hash (tick 0) | `0xB6CA4ECAC822E8AA` |
| Final hash (tick 6) | `0xF5FA36EB05EF783D` |
| Domain events | 27 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xC01A5C9A24BB2DF1` |
|    2 | `0xD8F6D349CFFF4535` |
|    3 | `0xD6DAB3B833B10B8D` |
|    4 | `0x8A9563BFCA4AF171` |
|    5 | `0xC7E8983B705E1311` |
|    6 | `0xF5FA36EB05EF783D` |
