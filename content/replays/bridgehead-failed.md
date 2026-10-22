# Replay corpus entry: bridgehead-failed

The real Bridgehead mission (content/scenarios/bridgehead.cwscenario, seed 20260920) played to MissionOutcome = Failed with ordinary MoveTo orders only (TASK-077, backlog B-077): one reckless six-agent frontal charge at tick 1 toward (16,4)/(10,6)/(11,6)/(16,7)/(13,2)/(10,5). Several orders are Refused (RouteTooExposed) once threats become known, but those agents are already inside weapon range; every friendly is down by tick 84 and MissionFailed fires. The extraction objective (id 3) stays incomplete: an ExtractAgents objective needs at least one agent extracted (backlog B-079).

Regenerate every corpus hash table from the repository root:

    dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate

An unexplained change to the numbers below is a determinism regression.
See `content/replays/CORPUS.md`.

## Parameters

| Parameter | Value |
|---|---|
| Command log | `bridgehead-failed.cwreplay` |
| Initial state | content/scenarios/bridgehead.cwscenario (18 x 12, seed 20260920, 6 friendlies + 5 hostiles) |
| Tick count | 86 |
| Initial hash (tick 0) | `0x02EA1348C7764846` |
| Final hash (tick 86) | `0x4924D1A9D021797F` |
| Domain events | 358 |

## Per-tick authoritative state hash

| tick | state hash          |
|-----:|---------------------|
|    1 | `0x09C0D5BB8FA9E81E` |
|    2 | `0xFE0EEB8E62C6EBF2` |
|    3 | `0xBFF95247AD2EB7B1` |
|    4 | `0x11090F13CF8A0866` |
|    5 | `0xD61A07E2F5D5DF53` |
|    6 | `0x2F213E8AE6180224` |
|    7 | `0xBABB012545779CF1` |
|    8 | `0x9FC31D35B946245E` |
|    9 | `0x728847D2D354C927` |
|   10 | `0xF84494843DA4C4FA` |
|   11 | `0xCD1775199AEF7F44` |
|   12 | `0x8F4C96860402B8FD` |
|   13 | `0xE5DFBF61A284EAE1` |
|   14 | `0x97823A65C6E0003D` |
|   15 | `0x16A5253EDE86E2CD` |
|   16 | `0x9D477857B5049BAC` |
|   17 | `0x2C73A89E9622BA56` |
|   18 | `0x93D801BF93B04C02` |
|   19 | `0x2FEE968783963DCA` |
|   20 | `0xF19F7F5CFAC4CE95` |
|   21 | `0xAF458FBAC834CEF3` |
|   22 | `0x63D7D7A4CD176854` |
|   23 | `0xEE81F842C6B5745A` |
|   24 | `0x628863EB556C2BC9` |
|   25 | `0x3F0B53F75DC26477` |
|   26 | `0xACF42E5BDFD8976C` |
|   27 | `0xEC4BD6AE9BCB9DB3` |
|   28 | `0xFB04709850C22E07` |
|   29 | `0xADFB217B7BBA62A9` |
|   30 | `0x63CFF4976FBC3E6E` |
|   31 | `0x3D1417BD03E11C73` |
|   32 | `0x4A3DCEA027FF060F` |
|   33 | `0x790D2C15D7D6029F` |
|   34 | `0xA699DE86C74F5AED` |
|   35 | `0xB25D95C7A8718834` |
|   36 | `0xA5B753CCF54AB2BC` |
|   37 | `0xE18A1C0F8E30A760` |
|   38 | `0x1B419A077DC2AD9A` |
|   39 | `0x68B61087CD0E95C2` |
|   40 | `0x923572F1373EEE83` |
|   41 | `0x9CDAB82A9BCC9BE5` |
|   42 | `0x21EEB9A4780881E5` |
|   43 | `0xF6DD1656179A9BBA` |
|   44 | `0x36007E1458610DA4` |
|   45 | `0xC46B629B6CD809A1` |
|   46 | `0x5C3EE54F3E4E28C1` |
|   47 | `0x443EFD78F04579B9` |
|   48 | `0x3F1E345FA6208231` |
|   49 | `0x69309E5B6A2E80B5` |
|   50 | `0x8A2BE5988E2D1C10` |
|   51 | `0x134FA70EC585C68B` |
|   52 | `0x92E6A1A986C048D5` |
|   53 | `0xB9FA376E72C83B37` |
|   54 | `0x619CB61B55B7504C` |
|   55 | `0x44E183FD0F520060` |
|   56 | `0x032A5502DA270FDC` |
|   57 | `0xE4F1E4B021BA2558` |
|   58 | `0x045FEFB297BEB3E4` |
|   59 | `0x34B971AC218AE5F0` |
|   60 | `0x047E4A5796AEDB2A` |
|   61 | `0xCC4661AC1FDFBF2E` |
|   62 | `0x20D1F71E67DAC232` |
|   63 | `0xB72131056C4374CE` |
|   64 | `0xB47FDC4F3DAE3232` |
|   65 | `0x57F8E621C63B120E` |
|   66 | `0xA93E51397F45DBEA` |
|   67 | `0x57FD5C209DA868CE` |
|   68 | `0xBADB69B1373846FA` |
|   69 | `0x8C62BCEBC96FC7BE` |
|   70 | `0xDAB65266ACF9DD92` |
|   71 | `0xA5753DAB1A75874E` |
|   72 | `0xB59C2F70F16D4EF2` |
|   73 | `0x502AD73F7F3AE315` |
|   74 | `0xE3D67DD0C2A9B572` |
|   75 | `0xDEB6F3CE676C0D6A` |
|   76 | `0xC50F48B9EE059D9C` |
|   77 | `0x1E13C314011A120A` |
|   78 | `0x8F18303BADDD974D` |
|   79 | `0x539033B2420B0C49` |
|   80 | `0xD0DCCC0ADE3801B6` |
|   81 | `0xD74C92F72CBE40E0` |
|   82 | `0x0161CCC31777079F` |
|   83 | `0x66880A0D1697D6AC` |
|   84 | `0xC89000A517F24643` |
|   85 | `0xB86FB691ABF26E9F` |
|   86 | `0x4924D1A9D021797F` |
