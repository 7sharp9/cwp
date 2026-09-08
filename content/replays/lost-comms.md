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
| Initial hash (tick 0) | `0x08367596DD235E51` |
| Final hash (tick 4) | `0x29DB050174E1C5E5` |
| Domain events | 2 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xB473F28537FC2BB4` |
|    2 | `0xD0910D7B27891ACF` |
|    3 | `0x476B06483B72E992` |
|    4 | `0x29DB050174E1C5E5` |
