# Replay corpus entry: casualties-succession-and-squad-failure

Two friendly-hostile pairs, no orders (the open-engagement precedent -- combat alone drives the trace): friendly 0 (0,0) vs hostile 2 (0,3), friendly 1 (5,0) vs hostile 3 (5,3), each pair a Chebyshev distance of 3 apart (within weapon range) and paired by Combat.chooseTarget's nearest- candidate rule from tick 1 (backlog B-031). Wounds accumulate over repeated hits (Casualty.wound, no cover on this open terrain); friendly 0 is Incapacitated by tick 4, and since it was the derived squad leader (the lowest-id living friendly -- a pure rule, no stored field, backlog B-031/docs/05 section 17's 'simple replacement rule'), leadership transfers to friendly 1 the same tick (LeadershipTransferred). Friendly 1 is Incapacitated by tick 5 too: leadership transfers again, to None, and SquadFailure fires the same tick -- a signal event only, it does not halt the run. Every Incapacitated agent's bleed-out (CasualtyConfig.BleedOutTicks = 60, no rescue mechanic -- Central decision 3) then counts down with nothing to stop it: all four agents reach Dead by tick 64, AgentDied firing for each.

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `casualties-succession-and-squad-failure.cwreplay` |
| Initial state | Corpus casualties-succession-and-squad-failure scenario (10 x 10, seed 20260904, 2 friendlies + 2 hostiles) |
| Tick count | 65 |
| Initial hash (tick 0) | `0xAA2E26E45FA5F886` |
| Final hash (tick 65) | `0x9516F7C342D91B61` |
| Domain events | 36 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xDBC3E828922569CC` |
|    2 | `0xCF2BF14239A1F271` |
|    3 | `0x79707E9C3EB9D4F6` |
|    4 | `0xEA0AEAEB6C32F4A9` |
|    5 | `0x8C43B622A96AD91C` |
|    6 | `0x9420532D2377F87D` |
|    7 | `0xC64DB2F4BC932CE1` |
|    8 | `0xEFA1A2F6BF510BE9` |
|    9 | `0x8DABE2A2C570732A` |
|   10 | `0x35DB664068D9EF71` |
|   11 | `0x5DC076768C96273C` |
|   12 | `0x03D70983C7CBEFC0` |
|   13 | `0xDBE49A2E5FCD2D28` |
|   14 | `0x5C5737F80128F1E9` |
|   15 | `0x8273C77D9559F344` |
|   16 | `0xD42EB90356C60CDB` |
|   17 | `0xB5E8AE5C3B074ABE` |
|   18 | `0xE2A73BF60346B935` |
|   19 | `0xC2E234AC6CC5408C` |
|   20 | `0x47D3CF561FA8A51F` |
|   21 | `0x2D1C30AFA7B743A2` |
|   22 | `0x2A3CBC2C80903FAD` |
|   23 | `0xFD592ADF9A3CB9FA` |
|   24 | `0x1AA330EC2D30F3E5` |
|   25 | `0x2B36A3C2E5F5B940` |
|   26 | `0xB6BD4C3205DD78BF` |
|   27 | `0xFA015C614224DE62` |
|   28 | `0xE7836CF273D37711` |
|   29 | `0x02020A8CAAAF86D4` |
|   30 | `0xF9626BDDA68B449B` |
|   31 | `0xEBC427ABA9A6F8F6` |
|   32 | `0xCEBCC29B5D2CFAAD` |
|   33 | `0x6F8C76FC376BBFB8` |
|   34 | `0x568345DFB510CB87` |
|   35 | `0x854BB83CDB14D51A` |
|   36 | `0xA9A8EA5FACD6F699` |
|   37 | `0xB01306F414C6426C` |
|   38 | `0x5B3BFE9A8D2D8BA3` |
|   39 | `0x601C46E16221D9AE` |
|   40 | `0x21E6FF41652D9435` |
|   41 | `0x97B95BE8FD225330` |
|   42 | `0x4E752EFA072481EF` |
|   43 | `0xA5C1ABC735220EB2` |
|   44 | `0xD2B7973A02D29CE1` |
|   45 | `0x741CD820FAA6F764` |
|   46 | `0x390FFB50768C5F2B` |
|   47 | `0xAB13CDCA41CC98E6` |
|   48 | `0x02425E80B8CE517D` |
|   49 | `0xF92E3C9AA4AD9C28` |
|   50 | `0x587F31555EFF4DB7` |
|   51 | `0x027C39CB66A0E4AA` |
|   52 | `0x1A5375D8804146A9` |
|   53 | `0x5A643E37486EB07C` |
|   54 | `0x7CA1553FD4EC67B3` |
|   55 | `0x676F3D308342E81E` |
|   56 | `0x5C4777C67AC6D8C5` |
|   57 | `0xB351676FFA4AE320` |
|   58 | `0xB3EE8CFE69D11E9F` |
|   59 | `0x2E6E8F77C1CE75C2` |
|   60 | `0xC2E0FA2A05CB3BF1` |
|   61 | `0xA88A522D5E2F47B4` |
|   62 | `0xFD91C219AFC20D00` |
|   63 | `0x7A87AF1DCAE79F33` |
|   64 | `0xDCE2175D0696CCD6` |
|   65 | `0x9516F7C342D91B61` |
