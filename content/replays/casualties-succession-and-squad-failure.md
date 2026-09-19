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
| Initial hash (tick 0) | `0x09B0C96F26AC2B87` |
| Final hash (tick 65) | `0x99E04CA6BA623140` |
| Domain events | 40 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x84150BD2766E8119` |
|    2 | `0xE593E00153A0A480` |
|    3 | `0xBF066B919A73EFC7` |
|    4 | `0xFEF857889342EF45` |
|    5 | `0xCBA7396B183853BC` |
|    6 | `0x960B57170EC4C4C9` |
|    7 | `0x31ECD2AEEED9FA8D` |
|    8 | `0xC6D0C30A1FDE0F69` |
|    9 | `0x414086F08437C922` |
|   10 | `0x44BC6EE55C52DF95` |
|   11 | `0x15CE27D6E1FA7950` |
|   12 | `0x85FD6420711B173C` |
|   13 | `0x06DD1E763BF7193C` |
|   14 | `0x05D1B0FFA4531165` |
|   15 | `0xADCA7CBCBE6D6D4C` |
|   16 | `0x4F27C25A1F9618BB` |
|   17 | `0x1D34510509FED856` |
|   18 | `0xA2508E8828163F15` |
|   19 | `0x6C694438EFCE4404` |
|   20 | `0xBEB8BEDB8E0B06EB` |
|   21 | `0xC72F8D188823C8A2` |
|   22 | `0xD282E00C729ECA81` |
|   23 | `0x4EF2AE557136FE55` |
|   24 | `0xC6B5DD6178A5C4E1` |
|   25 | `0x8371E072A05D922C` |
|   26 | `0x32D1D4A5965D40AF` |
|   27 | `0x96CB5A9E6A6AB526` |
|   28 | `0xDE09FA480C66E9C1` |
|   29 | `0xB00CE8165725F1C8` |
|   30 | `0x734060C63713452B` |
|   31 | `0x237DD8341E212E72` |
|   32 | `0x93E94C36E7B900FD` |
|   33 | `0xBD07A82D892CF0E4` |
|   34 | `0xD8950ACDBC401907` |
|   35 | `0x0FC630B32F680D6E` |
|   36 | `0x08EF66F9381E2BF9` |
|   37 | `0xFD7EDCCED3BA0440` |
|   38 | `0x382B80D687A93063` |
|   39 | `0xDE840B094DAF749A` |
|   40 | `0x336230C9B5F47835` |
|   41 | `0xFA7B722C4D4E27DC` |
|   42 | `0xBEDBAD915D72AB9F` |
|   43 | `0xD56F0E17906A8D36` |
|   44 | `0xA10B263873C439F1` |
|   45 | `0x37D18F6BAE9CA1B8` |
|   46 | `0xD80C0FB967B0425B` |
|   47 | `0xDE89EE84E21243E2` |
|   48 | `0x2E05C3A661033EED` |
|   49 | `0xA9C2414EBB0C3214` |
|   50 | `0x16B65AC310C6C237` |
|   51 | `0xFB61FD6666324A7E` |
|   52 | `0xDFB88F97604A13E9` |
|   53 | `0x581025EA6F576570` |
|   54 | `0x6B431826A3FA76D3` |
|   55 | `0x6819208E8FA3EECA` |
|   56 | `0xC608AF81D11A65A5` |
|   57 | `0x99AFAFFDDFD9EECC` |
|   58 | `0x00246BDF3CB02BCF` |
|   59 | `0xE5A031F2EE5263C6` |
|   60 | `0xF807FC5DB1E721E1` |
|   61 | `0xF64F4E50A2E7A828` |
|   62 | `0x16D6C7B0D63D892C` |
|   63 | `0xD6E401C98E39D3F0` |
|   64 | `0xC6C8E91A0DA7B29C` |
|   65 | `0x99E04CA6BA623140` |
