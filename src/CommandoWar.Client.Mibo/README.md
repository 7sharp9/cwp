# CommandoWar Mibo + raylib framework spike (TASK-005)

**Disposable.** This host exists only to produce ADR-0001 evidence. It is not
production code and must be removable without touching `CommandoWar.Sim`,
`CommandoWar.Sim.Tests`, `CommandoWar.Headless`, `content/`, or the shared
fixture. It is deliberately **not** in `CommandoWar.slnx`.

## Pinned versions

| Component | Version | Note |
|---|---|---|
| .NET SDK | `10.0.303` | repo `global.json`, `rollForward: latestPatch` |
| Target framework | `net10.0` | matches `CommandoWar.Sim` |
| `Mibo.Core` | `4.1.0` | **last release with no `Mibo.Adaptive` dependency** (ADR-0003 2026-09-02 amendment). 4.2.0+ folds the adaptive integration into `Mibo.Core` and takes a hard package dependency on the prohibited `Mibo.Adaptive`. |
| `Mibo.Raylib` | `4.1.0` | classic MVU + raylib backend |
| `Raylib-cs` | `8.0.0` | transitive via `Mibo.Raylib 4.1.0` |
| `FSharp.UMX` | `1.1.0` | transitive |

`Mibo.Adaptive` appears nowhere: not in the restore graph, `cwmibo.deps.json`,
or the build output. The prohibition is held by the version pin, not by package
exclusion.

## Layout

```
Content.fs        .cwmap text parser + validator   (NO Mibo / raylib / Sim types)
SimBridge.fs      the only module that opens CommandoWar.Sim   (NO Mibo / raylib types)
Program.fs        headless self-check (Mibo HeadlessRunner) + windowed classic-MVU raylib host
content/greybox.cwmap          authored 32x32 iso greybox: 6 friendly spawns (col 0, rows 0-5) + objective
content/greybox-invalid.cwmap  deliberately broken content for the failure path
```

`SimBridge.Sim` owns one `WorldState` and advances it with `Simulation.step`.
Everything it exposes is a primitive or an F# value type; no authoritative state
enters the Mibo model. Terrain/walls are client-only and never cross the
boundary (`CommandoWar.Sim` has no terrain model yet, backlog B-008).

## Build

```
dotnet build src/CommandoWar.Client.Mibo/CommandoWar.Client.Mibo.slnx -c Release
```

## Run

```
# headless hash cross-check against CommandoWar.Headless (no window)
dotnet run --project src/CommandoWar.Client.Mibo -c Release -- --selfcheck --expect
#   prints one `tick=N hash=0x...` line per tick; identical to
#   `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
#   exit 0 on match, 1 on mismatch (try: --expect 0xDEADBEEFDEADBEEF)

# same 40 ticks through Mibo's withFixedStep facility (virtual time, 20 Hz)
dotnet run --project src/CommandoWar.Client.Mibo -c Release -- --selfcheck --fixedstep --expect

# deliberately invalid content -> exit 2, one actionable error per problem
dotnet run --project src/CommandoWar.Client.Mibo -c Release -- --selfcheck --invalid

# windowed raylib host (interactive)
dotnet run --project src/CommandoWar.Client.Mibo -c Release

# windowed host, slowed to 6 Hz, writes an evidence screenshot then exits
dotnet run --project src/CommandoWar.Client.Mibo -c Release -- --screenshot <abs-path>.png
```

## Windowed controls

- left-click an agent to select, left-click a cell to issue `MoveTo`
- right-click to deselect
- `Space` pauses, `Esc` quits

The authoritative rate is fixed at program construction (`Program.withFixedStep`,
20 Hz, or 6 Hz in `--screenshot` mode). Runtime rate adjustment would need a
host-owned accumulator instead; see the TASK-005 ledger entry.

## Packaging

```
dotnet publish src/CommandoWar.Client.Mibo -c Release -r win-x64 --self-contained -o bin/publish
bin/publish/cwmibo.exe --selfcheck --expect
```

Produces a self-contained ~84 MB folder; `cwmibo.exe` launches from any working
directory and reproduces the fixture final hash `0x838D3AE7DBFB735D`. The
`ProjectReference` to `CommandoWar.Headless` (for the shared `Fixture` constants)
also copies its `cwheadless.exe` into the publish folder; harmless.
