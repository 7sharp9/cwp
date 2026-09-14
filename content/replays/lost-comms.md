# Replay corpus entry: lost-comms

One friendly agent 0 at (1,4) with CommunicationAvailable = false (an authored comms blackout), ordered east to (6,4) on tick 1. Command intake accepts the order (CommandAccepted), but the Communication phase cannot reach the recipient, so it emits OrderUndelivered and drops the order: no Destination is written and the agent never moves. The 'Lost communication' vertical-slice scenario (docs/05 section 16; TASK-027, backlog B-016). Because the order is dropped, no AgentState.Order is written and the Appraisal phase (TASK-028) never runs on it: this is the one entry with no OrderAppraised event.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `lost-comms.cwlog` |
| Initial state | Corpus lost-comms scenario (8 x 8, seed 20260904, 1 friendly, comms blackout) |
| Tick count | 4 |
| Initial hash (tick 0) | `0x4A1F96854EEAA69D` |
| Final hash (tick 4) | `0x700512B446D9EF39` |
| Domain events | 2 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x3F9820E693426116` |
|    2 | `0xCE620F23CA709543` |
|    3 | `0x81D9D940A772F59C` |
|    4 | `0x700512B446D9EF39` |
