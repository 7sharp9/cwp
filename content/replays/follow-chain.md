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
| Initial hash (tick 0) | `0xD8CBA9C6AD9D1B77` |
| Final hash (tick 6) | `0x01FED06094D3C358` |
| Domain events | 24 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x2674CA8BE79AED52` |
|    2 | `0x92012B2ADA464AE8` |
|    3 | `0xC1D05639B2C32DB6` |
|    4 | `0x99E7BBAE95229478` |
|    5 | `0x332E72DA27E16982` |
|    6 | `0x01FED06094D3C358` |
