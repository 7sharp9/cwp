## 2026-09-02 - TASK-001 - Repository and build baseline established

**Owner:** Dave with coding-agent assistance
**Source revision:** `1cfb0ea` (Initial commit)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303 (also installed: 9.0.310, 5.0.416); runtimes include 8/9/10
**Status change:** `active -> review`

### Changes

- Added `global.json` pinning SDK `10.0.303` with `rollForward: latestPatch`.
- Added `CommandoWar.slnx` (SDK-default solution format) with two projects.
- Added `src/CommandoWar.Sim/` — F# `net10.0` class library, no host/framework/platform dependency. Package references: `FSharp.Core` only (implicit, SDK-pinned 10.1.303). `TreatWarningsAsErrors` enabled.
- Added `tests/CommandoWar.Sim.Tests/` — F# `net10.0` xUnit project (`xunit` 2.9.3, `Microsoft.NET.Test.Sdk` 17.14.1, `xunit.runner.visualstudio` 3.1.4, `coverlet.collector` 6.0.4), with a project reference to the simulation library.
- `src/CommandoWar.Sim/Baseline.fs`: module `Baseline` with `ContractName` literal and `nextTick : int -> int` (placeholder boundary function, not the simulation step). No game behaviour.
- `tests/CommandoWar.Sim.Tests/BaselineTests.fs`: two facts exercising the cross-project boundary.
- Added `.gitignore` (dotnet template) so `bin/` and `obj/` stay untracked.
- `README.md`: added an additive "Repository baseline" section with the pinned SDK, target framework rationale, layout, and restore/build/test commands.

### Framework and SDK rationale

- `net10.0` chosen over `net9.0`: both SDKs are installed and currently supported, but .NET 9 reaches end of support on 2026-11-10 while .NET 10 is LTS through November 2028. `net10.0` is also the SDK's default template TFM. .NET 5 is installed but long out of support and was excluded.
- SDK `10.0.303` pinned because it is the highest installed 10.x SDK verified via `dotnet --info`.

### Verification

- Command: `dotnet --info`
  - Result: SDK 10.0.303 confirmed; SDKs 9.0.310 and 5.0.416 also present.
- Command: `dotnet restore CommandoWar.slnx`
  - Result: both projects restored, no errors.
- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2`.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only. No Godot, Mibo, MonoGame, raylib, or Tiled reference.
- Manual check: `git status` shows only intended additions (`.gitignore`, `CommandoWar.slnx`, `global.json`, `src/`, `tests/`) plus additive edits to `README.md`, `docs/11_BACKLOG.md`, `tasks/TASK-001-*.md`, and this ledger. No existing file deleted, moved, or reformatted.

### Evidence

- Public simulation API: `CommandoWar.Sim.Baseline.ContractName : string`, `CommandoWar.Sim.Baseline.nextTick : int -> int`.
- Solution: `CommandoWar.slnx`. Projects: `src/CommandoWar.Sim/CommandoWar.Sim.fsproj`, `tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj`.
- Simulation project package references: `FSharp.Core` (implicit, 10.1.303).

### Deviations and unresolved issues

- No headless console/host project was added; it was not required to prove the boundary (the test project references the library directly). TASK-002 introduces the fixed-tick API.
- Solution uses the newer `.slnx` format (SDK default). All `dotnet` commands accept it; older Visual Studio versions may not.
- `PROJECT_STATE.yaml` gate `G0` and `active_work` were left unchanged: evidence does not justify advancing G0 (needs Dave's acceptance) or activating TASK-002.
- Clean-state build was exercised by deleting all `bin/` and `obj/` directories and re-running restore, build, and test in the working tree; not a separate fresh clone.

### Documents updated

- `tasks/TASK-001-REPOSITORY-BASELINE.md` (status `review`, acceptance criteria checked)
- `docs/11_BACKLOG.md` (TASK-001 row `active -> review`)
- `README.md` ("Repository baseline" section)
- `docs/12_PROGRESS_LEDGER.md` (this entry)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-02)
- Notes: Accepted as project inception. G0 marked `passed`; `PROJECT_STATE.yaml` and backlog advanced to activate TASK-002. TASK-002 implementation to run in a separate session.
