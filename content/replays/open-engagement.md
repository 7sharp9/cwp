# Replay corpus entry: open-engagement

A friendly at (2,2) and a hostile at (7,2), open ground, no orders on either side: the Combat phase alone drives the trace. Both are within CombatConfig.WeaponRange and clear line of sight from tick 1, so the deterministic hitscan mechanic (range + directional-cover-mitigated hit chance, the stream's first real gameplay draw) fires every tick, symmetric both ways (TASK-031, backlog B-019).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `open-engagement.cwreplay` |
| Initial state | Corpus open-engagement scenario (10 x 10, seed 20260904, 1 friendly + 1 hostile) |
| Tick count | 3 |
| Initial hash (tick 0) | `0xC95F29C54E704190` |
| Final hash (tick 3) | `0x74CE8D9A082FA7CF` |
| Domain events | 8 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xC74B517DEA7D051C` |
|    2 | `0x7D9EB1198E5CD5DE` |
|    3 | `0x74CE8D9A082FA7CF` |
