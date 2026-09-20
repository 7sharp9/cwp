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
| Initial hash (tick 0) | `0x0DAA6E2C5AF00775` |
| Final hash (tick 65) | `0x4723FEDCC6005C74` |
| Domain events | 41 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0xE2CC8F96AC65DA09` |
|    2 | `0x7B24D7ABFF320856` |
|    3 | `0x6384F2DA68E74989` |
|    4 | `0x22A9063EEA4160E3` |
|    5 | `0x7E534CA1931CE2EA` |
|    6 | `0x7314519C819318ED` |
|    7 | `0x40B114B11787CEF7` |
|    8 | `0xB863C00BAD1F58D7` |
|    9 | `0x0E38AC9FD4132A56` |
|   10 | `0xE60C24E0ED12C6D5` |
|   11 | `0x018219F3625626F4` |
|   12 | `0x29BA5FA2A481BCEE` |
|   13 | `0x5FD27A4B0B40AD62` |
|   14 | `0x37752CF7A45639E9` |
|   15 | `0x75E5C3E25A5FB0E4` |
|   16 | `0xFE68518D2C81F6DF` |
|   17 | `0x3DCE9BD6E89DD5CE` |
|   18 | `0xD028DA32CAD87089` |
|   19 | `0xF82FB5AFDF5D99EC` |
|   20 | `0xD5BE893FE73E25FF` |
|   21 | `0x33D2C8B2C8BF72A2` |
|   22 | `0xC8ED5CDDC503DDFD` |
|   23 | `0x7FC368B3F9E7FFE1` |
|   24 | `0x9233E7FBCF85F165` |
|   25 | `0xB3DE28FB392DBF5C` |
|   26 | `0x3AA4940A6B346173` |
|   27 | `0x0061939B634959C6` |
|   28 | `0xF0778E9E77109F8D` |
|   29 | `0x0AACDE279A676450` |
|   30 | `0xDF1018DA00B40C67` |
|   31 | `0x9C8764C4F154325A` |
|   32 | `0xACF81E773ACB6471` |
|   33 | `0x80978230C6DC2A04` |
|   34 | `0xDA07CC7E8C3305FB` |
|   35 | `0x5C1CB3105C356F3E` |
|   36 | `0x8B76AB3B75DE3EE5` |
|   37 | `0x191D90427058F578` |
|   38 | `0xDC204C9EB576790F` |
|   39 | `0x9866070530142832` |
|   40 | `0xA7C0A8FDB3B3B389` |
|   41 | `0x5E54D387CCB0C36C` |
|   42 | `0x09D0224974E13843` |
|   43 | `0xF75CD2DAB94423D6` |
|   44 | `0x687F5B73786BF59D` |
|   45 | `0x257DDDCCCA3BAF20` |
|   46 | `0x4AF5C7A7C913FFB7` |
|   47 | `0x34DD5F5496E52F0A` |
|   48 | `0x336FAE10DAC31561` |
|   49 | `0x86ACECA50D3DA754` |
|   50 | `0xF1A6E1533EB67E0B` |
|   51 | `0xA94DA70FB8CE294E` |
|   52 | `0xB74654CE0BFF94B5` |
|   53 | `0x84955AEA13AB2A88` |
|   54 | `0x1D8EAE0C098995DF` |
|   55 | `0x3FD52E6DF3FDA9E2` |
|   56 | `0xC179408BF0475DF9` |
|   57 | `0x8C1DFD6A7CA8A07C` |
|   58 | `0xEDB283ED7FE6F293` |
|   59 | `0xA3D2FF6D7F179426` |
|   60 | `0xC18F69339F6CE6AD` |
|   61 | `0xE24FDC23DA7E4A70` |
|   62 | `0x4876E2DE41F510AA` |
|   63 | `0xE43E934BDD76F7C0` |
|   64 | `0x84FBD76D516CB6F4` |
|   65 | `0x4723FEDCC6005C74` |
