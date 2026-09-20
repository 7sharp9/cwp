# Replay corpus entry: order-queue-stacking-and-cancellation

One friendly agent at (0,0) is issued three stacked MoveTo waypoints on tick 1: (3,0) Replace (active), then (7,0) and (10,0) both Append (queued behind it) -- backlog B-051's 'really stacked' waypoints. Tick 2 cancels the still-queued third waypoint by CommandId (OrderCancelled wasActive=false) before it ever activates. Tick 3 the first leg arrives; tick 4 commitmentAndLocalAction recognises the fulfilled order and promotes the queue head (the second waypoint) into Order, un-appraised until tick 5's Appraisal judges it Accepted and movement resumes -- TASK-044's one-tick promotion gap, distinct from a same-tick delivered order. Tick 7 cancels the now-ACTIVE second waypoint mid-route by CommandId (OrderCancelled wasActive=true): with an empty queue behind it the agent goes Holding and its Destination is cleared too, so it genuinely stops at (5,0) rather than drifting on with a stale target.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `order-queue-stacking-and-cancellation.cwreplay` |
| Initial state | Corpus order-queue-stacking-and-cancellation scenario (12 x 3, seed 20260904, 1 friendly) |
| Tick count | 9 |
| Initial hash (tick 0) | `0x1A9E1DE486ABF1C0` |
| Final hash (tick 9) | `0x91BCB6A5F7CFFEC0` |
| Domain events | 20 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xDB959CB4175F9FE0` |
|    2 | `0x1849C5649E93AFAA` |
|    3 | `0xF99657C7F2957420` |
|    4 | `0x5692B6E0A2FA42F5` |
|    5 | `0x2638B51214196EEC` |
|    6 | `0xADA0B3A4D4E14B12` |
|    7 | `0xD57D8479EBED7CCA` |
|    8 | `0x393C70314D7C91A7` |
|    9 | `0x91BCB6A5F7CFFEC0` |
