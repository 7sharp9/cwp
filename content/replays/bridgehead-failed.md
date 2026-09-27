# Replay corpus entry: bridgehead-failed

The real Bridgehead mission (content/scenarios/bridgehead.cwscenario, seed 20260920) played to MissionOutcome = Failed with ordinary MoveTo orders only (TASK-077, backlog B-077): one reckless six-agent frontal charge at tick 1 toward (16,4)/(10,6)/(11,6)/(16,7)/(13,2)/(10,5). Several orders are Refused (RouteTooExposed) once threats become known, but those agents are already inside weapon range; every friendly is down by tick 84 and MissionFailed fires. The extraction objective (id 3) also reports complete that tick, vacuously, since no friendly is alive to extract -- current behaviour, recorded as a defect in TASK-077.

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
| Final hash (tick 86) | `0xC2CABC57DDF1619B` |
| Domain events | 364 |

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
|   15 | `0x1BBC9F48350E3B1D` |
|   16 | `0x289C3140EF89AFA7` |
|   17 | `0xE978982F053413E1` |
|   18 | `0x1AF9CBCD6428CB72` |
|   19 | `0xE2A4D040D5EF7999` |
|   20 | `0x5F771FE145BDA872` |
|   21 | `0x1A37F0E6BA5858FD` |
|   22 | `0x412780BC3BF7E019` |
|   23 | `0x11FED2214B1256F2` |
|   24 | `0x37EF99B83D15B746` |
|   25 | `0x34C7F0030C75689B` |
|   26 | `0x3CFCBCE8F325C98E` |
|   27 | `0xDE8F47F1E45A36EC` |
|   28 | `0xE37A074D681A938A` |
|   29 | `0xA43F24ADC62AD5E6` |
|   30 | `0xA135E3D0581F2B1C` |
|   31 | `0x4465EBBA8F9A882D` |
|   32 | `0xC0F64521B70ECA4D` |
|   33 | `0xE37A96E56815CD53` |
|   34 | `0x9071BFD0ECCB7B81` |
|   35 | `0x35B322AFD9383169` |
|   36 | `0xFA8174E3F7517C98` |
|   37 | `0x2EF2F98A4A81D59D` |
|   38 | `0xFD5F4686D1E65D75` |
|   39 | `0x6FC5DB5E44293E09` |
|   40 | `0x423360090497F388` |
|   41 | `0xD0AAC6C8E276250A` |
|   42 | `0xA3B31A4BF47A859C` |
|   43 | `0xFE6872391944A861` |
|   44 | `0x283EE15C811AB982` |
|   45 | `0x3B32F8437D7CE908` |
|   46 | `0x3842680F46C57F17` |
|   47 | `0x46567DCA5A0BC2A3` |
|   48 | `0xDDFA78BCC7020C66` |
|   49 | `0x1B1992215E9C016D` |
|   50 | `0x61833C4544C75835` |
|   51 | `0x2AAEAEF63A16CB27` |
|   52 | `0xE1C43D83FC50830B` |
|   53 | `0xACEB2112BA7636C4` |
|   54 | `0x521319E4DB3FF944` |
|   55 | `0x837320F557249E75` |
|   56 | `0xBAFCA7438231E618` |
|   57 | `0xC0EDC3708987AB4B` |
|   58 | `0xB390745B9ECC5142` |
|   59 | `0x122AEC1611FEC95D` |
|   60 | `0xB658F99E1357DB1A` |
|   61 | `0x413ADA1307ECDF7E` |
|   62 | `0xFC92933C176D02BB` |
|   63 | `0x642B9B3A32245880` |
|   64 | `0x1C4C3C86E585095D` |
|   65 | `0x7365683D268DE302` |
|   66 | `0x2397289C828637FF` |
|   67 | `0x58BA32CA93350FF4` |
|   68 | `0xED6AAAC8C34BEAA1` |
|   69 | `0xDD2FF14DCFFF1B56` |
|   70 | `0x346BD14ED1BB0D93` |
|   71 | `0xA09835F48095A548` |
|   72 | `0xCE80784E2255D455` |
|   73 | `0x9E245C126519156D` |
|   74 | `0xB5546E3B548B2373` |
|   75 | `0x8D2470B86A0C31FC` |
|   76 | `0x2D93DAA2F7094D2B` |
|   77 | `0x8358EDAB77197BF6` |
|   78 | `0x8728C39417E25CF0` |
|   79 | `0x036A529FEBBF71DA` |
|   80 | `0x6A20C4E5940646AB` |
|   81 | `0x20DA986C3A0037DD` |
|   82 | `0x331C8A798CEDC2AA` |
|   83 | `0x709A271742A1420F` |
|   84 | `0x1EFC5642D6532E76` |
|   85 | `0x9706281F0102779A` |
|   86 | `0xC2CABC57DDF1619B` |
