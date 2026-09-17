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
| Initial hash (tick 0) | `0x88882D48FDB7E39D` |
| Final hash (tick 65) | `0x4A12BC4350F5E2B7` |
| Domain events | 36 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x26D9C05400E8225B` |
|    2 | `0x386D175F72A3F3DA` |
|    3 | `0x51A52B824698AA8D` |
|    4 | `0x41281E3C88A4FF65` |
|    5 | `0x17417A43235BF496` |
|    6 | `0x5781572C5C166707` |
|    7 | `0xC79B968BB8C1D14F` |
|    8 | `0xBDB1340662DB0043` |
|    9 | `0xEE53F130DB3E2E98` |
|   10 | `0xF4D161AFA8B658CB` |
|   11 | `0x51741B847B6CA69E` |
|   12 | `0xB7B9935A0D86179A` |
|   13 | `0x87ECF7F6C55C5F8A` |
|   14 | `0x23E572E5909962E7` |
|   15 | `0x4A5D80F6CCA8FA6A` |
|   16 | `0x6015AD45CA5A593D` |
|   17 | `0x310F57DD239A0100` |
|   18 | `0xECA7E62624868153` |
|   19 | `0x5B7E610D4D2B4E32` |
|   20 | `0xE14984F9C333617D` |
|   21 | `0x78907B41C8D0DB94` |
|   22 | `0x2CA80BFF20CCBEEF` |
|   23 | `0x03C32EEDA8BF815C` |
|   24 | `0x97B259B71D950847` |
|   25 | `0x6DC7B7E68935C91E` |
|   26 | `0xCDF1D087354FA415` |
|   27 | `0x40EE3732ACFCF1DC` |
|   28 | `0x8F02C5D0C36B6DBB` |
|   29 | `0x8636EB704C5ED09A` |
|   30 | `0x5B45A91AD53C1EA9` |
|   31 | `0x3D249527FC56EA68` |
|   32 | `0x44F63DD883AE5B5F` |
|   33 | `0xDA216D214D3AAEE6` |
|   34 | `0x0B08424B852E3E4D` |
|   35 | `0xA948FB59FE86F694` |
|   36 | `0x707496D006BEAFD3` |
|   37 | `0xD8D90080A2C17662` |
|   38 | `0x5A9856E0CE713C01` |
|   39 | `0xD01E10F37F0FA7E0` |
|   40 | `0xBC802C507DEDD0F7` |
|   41 | `0xCD1BD3C2054F036E` |
|   42 | `0x8A284F05DE14F1A5` |
|   43 | `0x4DF3EF42E39891EC` |
|   44 | `0x35906FDAA3BF702B` |
|   45 | `0x9F87F54E0E8E89AA` |
|   46 | `0x18F5516402B6F899` |
|   47 | `0x1403509023B6F7D8` |
|   48 | `0x56E848ECAAF4294F` |
|   49 | `0x5A288FB78FCBCE36` |
|   50 | `0x67F9E51E32A026DD` |
|   51 | `0x1085395B19F83BA4` |
|   52 | `0x1C7AA8F6D5B4B483` |
|   53 | `0x58904D95AC357CF2` |
|   54 | `0x74C10DDCD118B871` |
|   55 | `0xDB92409B332F1D50` |
|   56 | `0xDC00353811120AE7` |
|   57 | `0xB35E983A2849C8FE` |
|   58 | `0x16B4E2293FD90EF5` |
|   59 | `0x6CFF6B9FAEB1E13C` |
|   60 | `0xADE21025DECAE85B` |
|   61 | `0x5470B704F7A3A47A` |
|   62 | `0x79CBDDC1F2B4EF62` |
|   63 | `0x2979E95E7E2802A5` |
|   64 | `0x486D2664DBC73A0C` |
|   65 | `0x4A12BC4350F5E2B7` |
