# Replay corpus entry: perception-contact

One friendly agent at (1,5) ordered east to (9,5); a stationary hostile agent 1 at (9,1) behind an opaque impassable wall at x=6, rows 0..3. The hostile is inside PerceptionConfig.SightRange from the start but line of sight is blocked; once the friendly clears the wall the Perception phase emits ContactObserved and the Tactical-knowledge phase adds the contact to the shared squad picture (WorldState.TacticalKnowledge, Canonical.FormatVersion 3). The first corpus entry with an enemy deployment (TASK-026, backlog B-015; the 'Unknown threat' shape, docs/05 section 16).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `perception-contact.cwlog` |
| Initial state | Corpus perception-contact scenario (12 x 8, seed 20260904, 1 friendly + 1 hostile) |
| Tick count | 14 |
| Initial hash (tick 0) | `0xDAA3BCA323164178` |
| Final hash (tick 14) | `0x320C6FE6BC2544DF` |
| Domain events | 12 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xFD02FE7828A82B37` |
|    2 | `0x186E8DB79AB716B1` |
|    3 | `0xF82262B511EF94CF` |
|    4 | `0x3EE9480E565B08ED` |
|    5 | `0xD67AA520876E76BB` |
|    6 | `0x20476F0B99F03694` |
|    7 | `0x0915A520FC553EB5` |
|    8 | `0x7978F2E81C02492B` |
|    9 | `0x95CF8C1DB0F80017` |
|   10 | `0xC8FE4729A933BC07` |
|   11 | `0xD939A57CCAE6D143` |
|   12 | `0x37995F56A190F57B` |
|   13 | `0x9AB1BA3F07A2A4BF` |
|   14 | `0x320C6FE6BC2544DF` |
