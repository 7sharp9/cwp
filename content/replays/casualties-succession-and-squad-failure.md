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
| Initial hash (tick 0) | `0x5BE9F82DBEBCBB44` |
| Final hash (tick 65) | `0x3139C4A44AB28D65` |
| Domain events | 41 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x2130EB17D88A13B8` |
|    2 | `0x25FC4D4750E7BA2F` |
|    3 | `0xC81F0722C38C6E18` |
|    4 | `0x021213A284F69D5E` |
|    5 | `0x7D37DD758DF2B41B` |
|    6 | `0x8509770C1B3CE0C8` |
|    7 | `0x3307F02676E6A0D6` |
|    8 | `0xF459107AFA7D008E` |
|    9 | `0x68F36323F4F8AF9F` |
|   10 | `0x490F105F07D780E8` |
|   11 | `0x7B4C87ECEAE2BBC9` |
|   12 | `0x4E133F512294991F` |
|   13 | `0x36641292E25B5363` |
|   14 | `0x1AE4254264D855DC` |
|   15 | `0xCE4DCD47C5642E71` |
|   16 | `0x1DA415DF2CFE99FE` |
|   17 | `0xF0A499FC44D7940F` |
|   18 | `0x59B6BBB0BB48B204` |
|   19 | `0xE4C452CE633FED09` |
|   20 | `0x3A35031FC5E559B6` |
|   21 | `0x20B4F01B71E59123` |
|   22 | `0x99CD30B29F3B3640` |
|   23 | `0x8E1BBB542EA474C0` |
|   24 | `0x0E8FFEBD28834A7C` |
|   25 | `0x7B5B89F20C60F805` |
|   26 | `0x0F87006425C29BE6` |
|   27 | `0x6238F05976700173` |
|   28 | `0x5DE9E3B69B6CCB04` |
|   29 | `0x9D666409D56797C1` |
|   30 | `0x6DF607196D479D22` |
|   31 | `0xB4CACEA4EB3B81FF` |
|   32 | `0xBB8B98F55153FFC0` |
|   33 | `0xE749E873511CEF6D` |
|   34 | `0x266359BCF7A8AB2E` |
|   35 | `0x72C9CDBC0D3FD57B` |
|   36 | `0x8D9BBFF46843520C` |
|   37 | `0x1E36AA39E6099149` |
|   38 | `0x4DB8DCB1A54DF0EA` |
|   39 | `0xF59F562086718927` |
|   40 | `0xE7B7F9A547392A48` |
|   41 | `0x824CEE893C2B3DF5` |
|   42 | `0x66450333E3C66116` |
|   43 | `0xF2ECC1112B889323` |
|   44 | `0xF46DF12FA2BD96B4` |
|   45 | `0xF3E51858A60D5071` |
|   46 | `0x2805E64A676A6FD2` |
|   47 | `0x323E0910417F382F` |
|   48 | `0x40D6C42FA4327AB0` |
|   49 | `0xA2E7826083C5B4DD` |
|   50 | `0xC1745A8DB53C969E` |
|   51 | `0x3B0BE2194B9D7BEB` |
|   52 | `0xCA8F123BB725DDBC` |
|   53 | `0xB6CD512DB56870F9` |
|   54 | `0x061256156F8268DA` |
|   55 | `0x2B6C7120B63A9E17` |
|   56 | `0x0B71A2E94ED29678` |
|   57 | `0x5383B7A4D3968565` |
|   58 | `0x6B4A8025CAEB8946` |
|   59 | `0x75145502385C8213` |
|   60 | `0xA4D05774D7CD08A4` |
|   61 | `0x4A96E9D1701D1F21` |
|   62 | `0x17702C75D1D4FA0F` |
|   63 | `0x193CE9EEC2F9971D` |
|   64 | `0x476CC9CC03DEA745` |
|   65 | `0x3139C4A44AB28D65` |
