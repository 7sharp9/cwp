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
| Initial hash (tick 0) | `0x49DEFA7E66E7C2D9` |
| Final hash (tick 6) | `0xB053D56EEE052E7E` |
| Domain events | 27 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xAC4E5E1AC8226D4E` |
|    2 | `0x96C361D2D344DA76` |
|    3 | `0x776C47D84E36033E` |
|    4 | `0xD6D762FB1311434E` |
|    5 | `0xACF2F0A0D7F4D37E` |
|    6 | `0xB053D56EEE052E7E` |
