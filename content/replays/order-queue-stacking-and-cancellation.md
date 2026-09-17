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
| Initial hash (tick 0) | `0x5768539143DF8AFE` |
| Final hash (tick 9) | `0x6D506AB97F23F80E` |
| Domain events | 20 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x86BA0B887A47D0D6` |
|    2 | `0x89C77F76C008D63C` |
|    3 | `0xA32719E52BBBD796` |
|    4 | `0x3727D642CEB2C9A3` |
|    5 | `0x318A36780C39D726` |
|    6 | `0x18A4E6DA308638CC` |
|    7 | `0xD9F97312DA0F3A98` |
|    8 | `0x9FFE1637BD7A5135` |
|    9 | `0x6D506AB97F23F80E` |
