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
| Initial hash (tick 0) | `0x5B18D51ECCA7975A` |
| Final hash (tick 9) | `0xB0340AFC9C038522` |
| Domain events | 20 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xCAC8A5DA0A794F96` |
|    2 | `0x5C93D0485F861120` |
|    3 | `0xB4AF4DBCF42CE8A6` |
|    4 | `0x8B2AA4CF7F14705D` |
|    5 | `0xE611DB07BF91F49A` |
|    6 | `0x1BBEE35DAC243310` |
|    7 | `0x1B4FE228DD068CBC` |
|    8 | `0x23F46D7767D57E4F` |
|    9 | `0xB0340AFC9C038522` |
