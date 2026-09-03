## 2026-09-02 - TASK-005 - Disposable Mibo + raylib framework spike (trimmed, pinned to Mibo 4.1.0)

**Owner:** Dave with coding-agent assistance
**Source revision:** `f363174` (Add deterministic random/hash/replay harness); working tree with the TASK-004 Godot spike present
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303 (`dotnet --version` = 10.0.303); raylib/OpenGL windowed run on the local display
**Status change:** `active -> review`

### Changes

**Framework-neutral / shared:** none. `CommandoWar.Sim`, `CommandoWar.Sim.Tests`,
`CommandoWar.Headless`, `content/fixtures/`, and `CommandoWar.slnx` are unchanged
(`git status` confirms; `CommandoWar.Sim.dll` md5 `82418ffbdf316b2f2e5124105b6f9e5e`
is identical in the library build and the host output).

**Mibo spike (own `src/CommandoWar.Client.Mibo/CommandoWar.Client.Mibo.slnx`,
deliberately NOT in `CommandoWar.slnx`):**

- F# `net10.0` exe (`AssemblyName` `cwmibo`), `RuntimeIdentifier` `win-x64`.
  `PackageReference` `Mibo.Raylib` `4.1.0` (pulls `Mibo.Core` 4.1.0,
  `Raylib-cs` 8.0.0, `FSharp.UMX` 1.1.0). `ProjectReference` to
  `CommandoWar.Sim` and `CommandoWar.Headless` (the latter for the shared
  `Fixture` constants).
- `Content.fs` (192 lines) - framework-neutral `.cwmap` v1 parser + validator.
  No Mibo / raylib / Tiled / `CommandoWar.Sim` types. Errors name the line
  (parse) or the marker/kind/cell/reason (validate), all in one pass.
- `SimBridge.fs` (117 lines) - the only module that opens `CommandoWar.Sim`.
  `Sim` owns one `WorldState`, advances it with `Simulation.step`, exposes
  `[<Struct>]` value views (`AgentView`, `TickInfo`) and an accepted-command log
  in `cwheadless` format. Never catches a step exception; never mutates
  authoritative state.
- `Program.fs` (403 lines) - `--selfcheck` headless path (Mibo classic
  `HeadlessProgram` / `HeadlessRunner`, explicit-dispatch and `withFixedStep`
  variants) + the windowed raylib classic-MVU host (`Program.mkProgram` /
  `RaylibGame`, `withFixedStep` authoritative cadence, `withTick` render
  measurement, `Mouse`/`Keyboard` subscriptions, command-buffer iso render,
  tick/hash overlay, `--screenshot` mode). CLI: `--selfcheck [--expect [0x..]]
  [--fixedstep] [--invalid]`, `--screenshot <path>`, `--invalid`.
- `content/greybox.cwmap` - authored 32x32 iso greybox reproducing
  `Setup.sixAgentWorld { 32; 32 } 20260902` (6 friendly spawns col 0 rows 0-5,
  1 objective, a wall block + scattered cover). `content/greybox-invalid.cwmap`
  - three seeded errors (spawn out of bounds, spawn on a wall, duplicate id,
  missing objective).
- `README.md`, `.gitignore` (`bin/`, `obj/`, `mibo-shot.png`).
- `docs/evidence/task-005-mibo-overlay.png` - self-captured overlay screenshot
  (tick 24, hash `0x08879506597DB88D`).

### Design decisions

- **Pinned to Mibo 4.1.0** per the ADR-0003 2026-09-02 amendment: 4.2.0+
  `Mibo.Core` hard-depends on the prohibited `Mibo.Adaptive`. 4.1.0 restores
  clean on SDK 10.0.303 / `net10.0` (no `FSharp.Core` downgrade) and keeps the
  classic `Mibo.Elmish.HeadlessProgram` / `HeadlessRunner` surface.
- **The canonical self-check drives the sim by explicit `Advance` dispatch**
  (one message -> exactly one `Simulation.step`), so the hash sequence has zero
  dependence on timing. A separate `--fixedstep` path drives the same 40 ticks
  through `Program.withFixedStep` + `StepUntil` (virtual time) to show the
  framework's fixed-step facility reaches the identical authoritative result;
  `StepUntil` avoids float32 accumulator under/overshoot.
- **`SimBridge.Sim` is a stateful object held by reference in the Mibo model.**
  It encapsulates the `WorldState`; the model holds a handle, not authoritative
  state (ADR-0002). This mirrors the Godot spike's `SimFacade`.
- **Windowed host renders exact authoritative cells** (no view interpolation) -
  sufficient for inspection; sim-vs-render separation is shown by the overlay
  numbers and the once-per-tick agent motion against a 60 fps counter.
- **Terrain is client-only** (`CommandoWar.Sim` has no terrain model, B-008):
  authored, validated against, rendered, never sent across the boundary.

### Verification

- Command: `dotnet test CommandoWar.slnx -c Release` (before and after host work)
  - Result: `Passed! - Failed: 0, Passed: 54, Skipped: 0, Total: 54`. The Mibo
    host is not in `CommandoWar.slnx`; the suite stays framework-neutral.
- Command: `dotnet build src/CommandoWar.Client.Mibo/CommandoWar.Client.Mibo.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (clean `obj/`/`bin/`).
- Command: `dotnet run --project src/CommandoWar.Client.Mibo -c Release -- --selfcheck --expect`
  - Result: initial hash `0xF2F3DF0D820AD9AC`; per-tick `tick=N hash=0x...` for
    ticks 1..40; tick 1 `0xC848D905A9CAD13F`, tick 14 `0x04343D056D0BAC45`,
    tick 31 `0x25315447F9D0E230`, tick 40 `0x838D3AE7DBFB735D` (format 1);
    `draws=0`; `accepted-command-log: 1 3 move 20 14`; `MATCH`; exit 0.
- Command: `diff <(cwheadless fixture hashes) <(mibo --selfcheck hashes)`
  - Result: no difference across the initial state + ticks 1..40.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- replay content/fixtures/spike-fixture.cwlog`
  - Result: tick 1 `0xC848D905A9CAD13F`, tick 31 `0x25315447F9D0E230`, final
    `0x838D3AE7DBFB735D`, `events: 33` - identical to the Mibo host.
- Command: `... -- --selfcheck --fixedstep --expect`
  - Result: `fixed-step: 40 authoritative ticks, final tick=40 hash=0x838D3AE7DBFB735D`;
    `MATCH`; exit 0.
- Command: `... -- --selfcheck --expect 0xDEADBEEFDEADBEEF`
  - Result: `MISMATCH expected 0xDEADBEEFDEADBEEF, got 0x838D3AE7DBFB735D`; exit 1.
- Command: `... -- --selfcheck --invalid`
  - Result: exit 2; `CONTENT LOAD FAILED (greybox-invalid.cwmap):` then
    `FriendlySpawn #2 at (40,3) lies outside the 32x32 grid`,
    `FriendlySpawn #4 at (0,10) lies on an impassable (wall) cell`,
    `FriendlySpawn #3 at (0,5) reuses id 3, already used by FriendlySpawn at (0,4)`,
    `no 'objective' marker: the scenario has no objective`. No `tick=` lines.
- Command: `... -- --screenshot <abs>.png`
  - Result: real raylib/OpenGL window opens (INFO log clean, default font
    loaded, audio device initialised), `TakeScreenshot` at frame 240 = tick 24,
    hash `0x08879506597DB88D` (matches fixture tick 24), `Cmd.signalExit` quits
    the same frame (exit 0, no deferred-quit quirk). Overlay:
    `docs/evidence/task-005-mibo-overlay.png` - tick / state hash + format /
    random draws 0 / sim 6 Hz fixed, 6 steps/s measured / render 60 fps.
- Command: `dotnet publish src/CommandoWar.Client.Mibo -c Release -r win-x64 --self-contained -o bin/publish`
  - Result: succeeds; `bin/publish` ~84 MB; no `Mibo.Adaptive.dll`. Running
    `bin/publish/cwmibo.exe --selfcheck --expect` from `C:\...\Temp\mibopub`
    (unrelated cwd): `MATCH ... 0x838D3AE7DBFB735D`, exit 0.
- Command: `dotnet list src/CommandoWar.Client.Mibo/... package --include-transitive`
  - Result: `Mibo.Raylib 4.1.0`, `Mibo.Core 4.1.0`, `Raylib-cs 8.0.0`,
    `FSharp.UMX 1.1.0`. **No `Mibo.Adaptive`.** `cwmibo.deps.json` has no
    `adaptive` library.
- Command: `dotnet list src/CommandoWar.Sim/... package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only; no `ProjectReference`;
    `CommandoWar.Sim.deps.json` libraries = `CommandoWar.Sim`, `FSharp.Core`.
- Command: source scan of `src/CommandoWar.Sim` + `src/CommandoWar.Headless` for
  `mibo|raylib|tiled|monogame|godot|Vector2|System.Drawing`
  - Result: two doc-comment lines in `Fixture.fs` ("framework spikes (TASK-004
    Godot, TASK-005 Mibo)"); no type or API.
- Content-edit exercise (`docs/06` section 12), on `content/greybox.cwmap`,
  reverted: changed `seed 20260902 -> 777` (1 file, 1 line, 0 code, 0
  conversion) -> edit-to-visible authoritative hash ~1.2 s warm (incremental
  build + content copy + 40-tick headless); seed 777 gave a distinct
  deterministic final hash `0x7D44ABDD084C1A25`. Broke one marker (moved a
  friendly spawn onto an authored wall) -> exit 2,
  `FriendlySpawn #4 at (6,10) lies on an impassable (wall) cell`. Revert ->
  `MATCH 0x838D3AE7DBFB735D`. Tiled editor and Mibo hot-reload NOT exercised
  (see deviations).

### Evidence

- **Versions:** SDK `10.0.303`; `net10.0`; `Mibo.Core` / `Mibo.Raylib` `4.1.0`;
  `Raylib-cs` `8.0.0`; `FSharp.UMX` `1.1.0`. 4.1.0 pinned because
  `Mibo.Core` >= 4.2.0 hard-depends on the prohibited `Mibo.Adaptive` (ADR-0003
  2026-09-02 amendment).
- **Shared fixture reproduced:** initial `0xF2F3DF0D820AD9AC`, ticks 1..40
  identical to `cwheadless fixture`, final `0x838D3AE7DBFB735D`, 33 events,
  random draws 0, agent 3 at (20,14) from tick 31. Both the explicit-dispatch
  and Mibo `withFixedStep` self-check paths.
- **Screenshot:** `docs/evidence/task-005-mibo-overlay.png` (tick 24, hash
  `0x08879506597DB88D`).
- **Host-specific files:** `src/CommandoWar.Client.Mibo/` (8 tracked files).
  Source: `Content.fs` 192, `SimBridge.fs` 117, `Program.fs` 403 (712 total F#).
  vs Godot `SimFacade.cs` ~150 + `SpikeContent.cs` ~170 +
  `GreyboxScene.cs`/`SpikeMarker.cs` ~80 + `MainNode.cs` ~330 (~730). Comparable
  total; Mibo's host layer is a little larger (verbose command-buffer view,
  hand-rolled self-check MVU program), all in one language.
- **Setup/build/run/publish commands:** `src/CommandoWar.Client.Mibo/README.md`.
- **Mibo API discovery:** the docs site (`program.md`, `headless.md`) is thin;
  the working API was recovered from reflection dumps of `Mibo.Core.dll` /
  `Mibo.Raylib.dll` 4.1.0, `dotnet new mibo-2d`, and the repo's
  `src/Mibo.Core.Tests/HeadlessTests.fs`.

### Deviations and unresolved issues

- **Trimmed scope (per the ADR-0003 amendment):** no Tiled authoring/importer,
  no self-contained-packaging *requirement* (done anyway, and it works), no
  MonoGame backend smoke test. Re-add only if a later ADR puts Mibo back in
  contention.
- **Tiled editor and Mibo content hot-reload NOT exercised.** Tiled is out of
  scope; the map is a hand-edited `.cwmap` text file; Mibo has no content
  hot-reload (edit -> relaunch). TASK-004 likewise did not exercise the Godot
  editor GUI, so the authoring-tool comparison is still open on both sides.
- **Interactive mouse input not literally exercised** (headless session). The
  `LeftClick` handler shares the exact `SimBridge.Sim.QueueMove ->
  Command.moveTo` path that `--selfcheck` verifies end to end (typed command ->
  accepted -> deterministic state change -> hash).
- **`withFixedStep` rate is fixed at program construction.** Runtime sim-rate
  adjustment (the Godot spike's `1`/`2` keys) would need a host-owned
  accumulator instead.
- **Referencing the F# `CommandoWar.Headless` exe project** for `Fixture`
  constants also copies `cwheadless.exe` into the Mibo host's publish output.
  Cosmetic; production would extract fixture constants to a library.
- **The dependency-risk driver is answered against Mibo** independent of this
  spike's ergonomic results: current stable Mibo is unusable under the
  prohibition, the pin is already stale, and the release cadence is very high.
- `PROJECT_STATE.yaml`: `active_work.selected_task` = `TASK-005`; gates,
  `current_gate`, and `framework_decision` unchanged. ADR-0001 remains
  `proposed`, `Selected candidate: TBD`; only the Mibo evidence column and a
  spike-results section were filled.
- Clean build run in the working tree (deleted `bin/`/`obj/`), not a fresh clone.

### Documents updated

- `tasks/TASK-005-MIBO-SPIKE.md` (status `active -> review`, acceptance criteria
  checked with evidence, completion notes)
- `docs/11_BACKLOG.md` (TASK-005 row `active -> review`)
- `decisions/ADR-0001-FRAMEWORK-SELECTION.md` (Mibo column of the evidence
  table; new "TASK-005 Mibo spike results" section; decision still `TBD`)
- `src/CommandoWar.Client.Mibo/` (new), `docs/evidence/task-005-mibo-overlay.png`
  (new)
- `docs/12_PROGRESS_LEDGER.md` (this entry)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-03)
- Notes: Mibo spike accepted as evidence. Backlog row `review -> done`, task file
  status `done`. Gates, `current_gate`, and `framework_decision` unchanged;
  ADR-0001 stays `proposed` / `TBD`. TASK-006 activated for the framework
  decision. ADR-0001 is not decided by this task.
