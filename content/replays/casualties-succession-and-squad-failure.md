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
| Initial hash (tick 0) | `0x26998EFCE8F6F30E` |
| Final hash (tick 65) | `0xCDA98167F94DAA9B` |
| Domain events | 41 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x20DEEF9DAB17F1FE` |
|    2 | `0xD704F1786971EB21` |
|    3 | `0x2DBD287B981E705E` |
|    4 | `0xF44CE11239D3082C` |
|    5 | `0xCE98BAF14D879BBD` |
|    6 | `0xAF877747DFB49F9A` |
|    7 | `0xC7A8A9B0778BB354` |
|    8 | `0x5D688970D2091E34` |
|    9 | `0x251FEAD88AE71CA1` |
|   10 | `0x3929A059F14B81EA` |
|   11 | `0xF97A19119AB13C9F` |
|   12 | `0x2A0B4ACF9E1A9831` |
|   13 | `0xAE848C7181189FA5` |
|   14 | `0xDD5B55769DF5FCAE` |
|   15 | `0x45EF098C91FD1107` |
|   16 | `0xD9FB5CEFFDF18EBC` |
|   17 | `0x655E3D13F7A7BDE9` |
|   18 | `0x624D58D0588F9C36` |
|   19 | `0xDB8FAFA3C5E873C7` |
|   20 | `0x404A2ACA28169D04` |
|   21 | `0x90A8B733E27B3C0D` |
|   22 | `0x89C02EFA3620F25A` |
|   23 | `0x95619E1F5A1E9A3E` |
|   24 | `0xD907633D25C4D976` |
|   25 | `0x3F93FB145263A6FB` |
|   26 | `0xA971916B9044BFDC` |
|   27 | `0x2D493B78A4F2AF95` |
|   28 | `0xFDC18049A8018CC6` |
|   29 | `0x7A8088E6E9844ABF` |
|   30 | `0xFB74BF953A7C76B0` |
|   31 | `0xB6C721E6472E63B9` |
|   32 | `0x0165040730BB7B1A` |
|   33 | `0x004CD9A97C54C473` |
|   34 | `0x800448B4E2C98A74` |
|   35 | `0x97FB89D511161C1D` |
|   36 | `0x74D725584AEF5F8E` |
|   37 | `0xA36353F4A4217E17` |
|   38 | `0xDE3DF6BDF83C5508` |
|   39 | `0xAE4B64781A029421` |
|   40 | `0x1E8D5A858CBE7862` |
|   41 | `0x20962278662CA24B` |
|   42 | `0x961ACE75DD783B0C` |
|   43 | `0x03F419C3707BBA65` |
|   44 | `0x0A8F173EC525B916` |
|   45 | `0x83242B327BBBDD0F` |
|   46 | `0x6843D70849755920` |
|   47 | `0xF5A3D3BEEDFFA709` |
|   48 | `0xEF723F58EF0C1AAA` |
|   49 | `0x63FBECAEB9411303` |
|   50 | `0x56B51B1669AC9B24` |
|   51 | `0x93A4DCC649A37AAD` |
|   52 | `0x11DC7D87118B3ADE` |
|   53 | `0xC34EEA67845F4AE7` |
|   54 | `0x3DE4721C29CBFEB8` |
|   55 | `0x4166F2BFE91058B1` |
|   56 | `0xA8B75275C7F56AF2` |
|   57 | `0xF2F2E1F79490825B` |
|   58 | `0x9E4B1D3C778FBE3C` |
|   59 | `0x0C8B0C704DDFDD75` |
|   60 | `0x04184EC960C894E6` |
|   61 | `0xAA17E6CA80F2431F` |
|   62 | `0xB3BB477CFF872E31` |
|   63 | `0x0F4D9CE065F763EB` |
|   64 | `0x72416185C2529D7B` |
|   65 | `0xCDA98167F94DAA9B` |
