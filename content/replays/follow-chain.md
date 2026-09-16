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
| Initial hash (tick 0) | `0x8F13291DFE5ECAC1` |
| Final hash (tick 6) | `0x791C83037D39AB5E` |
| Domain events | 27 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x5D3829AD44DD00BA` |
|    2 | `0xB507AB5A8F4CE6CE` |
|    3 | `0x403DCF4B4F12F476` |
|    4 | `0xC4A54A563BB2B882` |
|    5 | `0x55A0D4E0B888F1A2` |
|    6 | `0x791C83037D39AB5E` |
