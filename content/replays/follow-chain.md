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
| Initial hash (tick 0) | `0x10A6D26EC53FF558` |
| Final hash (tick 6) | `0xCC09F640C1638157` |
| Domain events | 27 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x6D9D6239DE238F75` |
|    2 | `0xD1E50B7A4DF8BC6F` |
|    3 | `0x89D7349E84583AE5` |
|    4 | `0xB567CBD0CFE4789B` |
|    5 | `0x4CAB61DABFE069B5` |
|    6 | `0xCC09F640C1638157` |
