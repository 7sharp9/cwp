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
| Final hash (tick 86) | `0xE2411D3A4F4EE4C4` |
| Domain events | 362 |

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
|   28 | `0xD344C5106C52D88D` |
|   29 | `0xA68B46E6FC4F51BF` |
|   30 | `0x171B420EBB3EE254` |
|   31 | `0x8B8167222135C585` |
|   32 | `0x82AE2B991B5EF7FD` |
|   33 | `0xA63C10FBB0EED39D` |
|   34 | `0xFDC8C01094C84207` |
|   35 | `0x15D52B812A588DC6` |
|   36 | `0xD672E4ABA937820A` |
|   37 | `0x185C12E50133BE8D` |
|   38 | `0xFE0C3C6745DC3BAF` |
|   39 | `0xBD78CDF46728C317` |
|   40 | `0x23141F683D532092` |
|   41 | `0xC8DD6A9D511F0C04` |
|   42 | `0x78B5568B82478636` |
|   43 | `0x307AE778A191650D` |
|   44 | `0x1EF039C0A8DBBAD7` |
|   45 | `0xC6BD770EFCF31FFA` |
|   46 | `0xA436E54E8B0AC6E2` |
|   47 | `0x26080F48323471C6` |
|   48 | `0x3D5CB494159A0916` |
|   49 | `0x24BFDCF053281E4E` |
|   50 | `0x8382DBAD60FDBF73` |
|   51 | `0x17D5D929DB086614` |
|   52 | `0xCAE56608178C267A` |
|   53 | `0x202BB9E706845BA4` |
|   54 | `0x4A17EC94DD56D2B7` |
|   55 | `0x1D9449AB40AF405B` |
|   56 | `0x370D1E0F018EA57B` |
|   57 | `0x118F3F1011B790DF` |
|   58 | `0xF908CE1EB082E9F7` |
|   59 | `0xE111649978F5EA5B` |
|   60 | `0x2093A411C9943049` |
|   61 | `0x147E8BE4002B85AD` |
|   62 | `0x5836FB26F2520BA5` |
|   63 | `0xACC1808316042D71` |
|   64 | `0xF1FA8292A02DB259` |
|   65 | `0xD4E0D9ACE72951AD` |
|   66 | `0x856170400B4AA8ED` |
|   67 | `0x5D9019FCB3259F89` |
|   68 | `0xA5CE8F4F55FFB399` |
|   69 | `0x714602B54946CE5D` |
|   70 | `0xF9EC1B1CFB4B47C5` |
|   71 | `0xAAAFC9D8DCE13C21` |
|   72 | `0x867C03044639C819` |
|   73 | `0x9C60B4D3E6C14BF2` |
|   74 | `0x4AFCE1E1A6B68865` |
|   75 | `0xA011BE2CB285AD99` |
|   76 | `0x9EFFDD8388890857` |
|   77 | `0x970FC44FE69C8BD9` |
|   78 | `0xADE1F79B5E560166` |
|   79 | `0x5E1A154AA5B291C2` |
|   80 | `0x45FD76966EA1C459` |
|   81 | `0x087FCAEA2F0F9937` |
|   82 | `0x0D85FFDEEDA7E3A6` |
|   83 | `0x976AB5DCD329028D` |
|   84 | `0x98477BBD703DD37E` |
|   85 | `0x9EEA2F0834F58B88` |
|   86 | `0xE2411D3A4F4EE4C4` |
