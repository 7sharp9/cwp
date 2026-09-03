## 2026-09-03 - TASK-007 - Godot C#/F# client boundary designed; ADR-0004 proposed; disposable proof built

**Owner:** Dave with coding-agent assistance
**Source revision:** `ad45bf0` (Accept ADR-0001 and close framework selection)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303 (`dotnet --version` =
10.0.303); Godot `4.7.2.stable.mono.official.ed1daf0bf` at
`C:\Users\Dave\Documents\GitHub\Godot_v4.7.2-stable_mono_win64\` (not on PATH);
GPU NVIDIA RTX 5070 Ti, Vulkan 1.4.329 Forward+
**Status change:** TASK-007 `-> active -> review`; ADR-0004 created (`proposed`);
gates, `current_gate` (`G2`), `current_phase` (`P2`), `framework_decision`
unchanged

### What was done

- **Precondition applied:** `PROJECT_STATE.yaml active_work.selected_task:
  none -> TASK-007`, `task_file -> tasks/TASK-007-GODOT-FSHARP-BOUNDARY.md`, the
  `active_work.note` removed; gates / `current_gate` / `current_phase` /
  `framework_decision` left unchanged. Created
  `tasks/TASK-007-GODOT-FSHARP-BOUNDARY.md`. Added the backlog "Current work"
  row (TASK-007, P2, S, deps TASK-006).
- Built a disposable proof under `src/_scratch/godot-fsharp-boundary/` (**not**
  in any `.slnx`): a `Godot.NET.Sdk` 4.7.2 project with one C# shim
  (`src/MainShim.cs`) over an F# `Microsoft.NET.Sdk` library `ClientCore`
  (`Boundary.fs` = `[<CLIMutable>]` view records; `Host.fs` = `SimHost` +
  `ClientHost`, the client logic; `FSharpNode.fs` = an F# `Node2D` subclass,
  the question-1 experiment). `ClientCore` references `CommandoWar.Sim` +
  `CommandoWar.Headless` (`Fixture`) + the `GodotSharp` 4.7.2 package.
- Answered TASK-007 questions 1-5 with recorded evidence (below).
- Wrote `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` (`proposed`): a thin C#
  host over an F# client-core library - form 1, one generic `FSharpSceneHost`
  for the whole client (preferred, no per-scene C#); form 2, a per-scene shim
  only where typed inspector `[Export]` / `[Signal]` is needed. F# types are
  not scene entry points; per-concern split table; interop idiom; a "Myriad and
  the shim" section; ADR-0002 compliance check; four review triggers.
- Updated `docs/03_ARCHITECTURE.md` section 14 and `docs/10_RISK_REGISTER.md`
  R-004.

### Evidence - question 1 (can an F# type be a Godot node?)

- Command: `dotnet build ClientCore/ClientCore.fsproj -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)`. F# `type
    FSharpHostNode() = inherit Godot.Node2D()` with overrides for `_EnterTree`,
    `_Ready`, `_Notification`, `_Process`, `_UnhandledInput`, `_Draw` and a
    `[<Export>] member val Caption` compiles against `GodotSharp` 4.7.2 in a
    plain `Microsoft.NET.Sdk` library (no `Godot.NET.Sdk`, no source generator).
- Command: `<godot> --headless --path . -- --probe`
  - Result: `[shim] added F# FSharpHostNode child; IsInsideTree=True`;
    `[shim] fnode.HasMethod("_process")=False  GetScript.Obj=<CSharpScript#...>`.
    Across 6 engine `_Process` frames **no** `[fsharp-node]` lifecycle line
    printed (no `_EnterTree` / `_Ready` / `_Notification` / `_Process` /
    `_UnhandledInput` / `_Draw`). A direct C# call `fnode._Ready()` then ran the
    F# body: `[fsharp-node] _Ready #1  name=FSharpProbe  Caption=assigned-from-C#`
    and `[fsharp-node] GetPropertyList() caption entry: ABSENT`. `AddUserSignal`
    + `Connect` + `EmitSignal` round-trip worked
    (`[shim] received fsharp_pinged from F# node`). Exit 0.
  - Conclusion: Godot does not drive an F# node's lifecycle (its managed
    virtual dispatch is populated by `Godot.SourceGenerators`, which is
    C#-only); `[<Export>]` is invisible; editor attach / `[GlobalClass]` /
    hot-reload depend on the same generator and are therefore unavailable. F#
    types are not viable as scene entry points. The C# shim per scene is the
    answer.

### Evidence - questions 2-4 (shim, per-concern split, interop idiom)

- Command: `dotnet build GodotFSharpBoundary.csproj -c Debug` (Godot's `--path`
  game run loads the Debug assembly; one `--editor --headless --quit` import
  pass was needed first)
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (also `-c Release`).
- Command: `<godot> --headless --path . -- --selfcheck --expect 0x838D3AE7DBFB735D`
  - Result: `tick=0 hash=0xF2F3DF0D820AD9AC` ... `tick=40
    hash=0x838D3AE7DBFB735D`, `tick 14 = 0x04343D056D0BAC45`, `tick 24 =
    0x08879506597DB88D`, `tick 31 = 0x25315447F9D0E230`,
    `accepted-command-log: 1 3 move 20 14`, `MATCH expected final hash
    0x838D3AE7DBFB735D`, exit 0. All stepping, the fixture command, and the
    self-check body are in F# (`ClientCore/Host.fs`); the C# shim only parsed
    args and made one call `ClientHost.SelfCheck(expect)`.
- Command: `<godot> --headless --path . -- --selfcheck --expect 0xDEADBEEFDEADBEEF`
  - Result: `MISMATCH expected 0xDEADBEEFDEADBEEF, got 0x838D3AE7DBFB735D`,
    exit 1.
- Command: `<godot> --path . -- --screenshot docs/evidence/task-007-godot-fsharp-boundary.png`
  - Result: windowed run (Vulkan/NVIDIA), `[shim] screenshot written ... (tick
    12, hash 0x981418895730F065)` (matches the self-check tick-12 line). The
    overlay in the still is `ClientHost.OverlayText` (F#).
- Interop idiom: putting `SimHost` in F# deletes the spike's
  `AgentIdModule.ofInt` / `World.create` via `ListModule.OfArray` /
  `FSharpOption<Cell>.get_IsSome`. C# sees `SimHost.Step() : TickView`,
  `SimHost.AgentViews : AgentView[]` (both `[<CLIMutable>]`),
  `ClientHost.SelfCheck(System.Nullable<uint64>)`. Per-concern C#/F# table in
  ADR-0004.
- Follow-up (Dave asked whether F# can be "more core" / whether Myriad helps):
  added `src/FSharpSceneHost.cs` (~35 lines, one generic `Node2D` for the whole
  client) + `ClientCore/Scene.fs` (`IClientScene`, `FixtureSelfCheckScene`) +
  `scenes/SceneHost.tscn`. Command: `<godot> --headless --path .
  --main-scene res://scenes/SceneHost.tscn` -> `[fixture-scene] Ready`,
  `tick=0 hash=0xF2F3DF0D820AD9AC` ... `final tick=40
  hash=0x838D3AE7DBFB735D`, `MATCH`, `[fixture-scene] ExitTree`, exit 0. The
  generic host resolved `CwClientCore.FixtureSelfCheckScene` from `[Export]
  SceneType` and forwarded lifecycle - **zero scene-specific C#**. ADR-0004
  "The split" now lists this as form 1 (preferred); a per-scene shim (form 2)
  only where typed inspector `[Export]` / `[Signal]` is genuinely needed.
- Follow-up 2 (Dave asked: can F# hold the members + `[Export]` / `[Signal]` /
  `[GlobalClass]` intent, with Myriad emitting only the C# forwarder?): added
  `ClientCore/NodeLogic.fs` (`PatrolMarkerLogic`, `[<GodotExport>]` /
  `[<GodotSignal>]` markers) + `src/GeneratedStyleNode.cs` (hand-written to be
  exactly what such a Myriad plugin would emit: `[Export]` properties + a
  `[Signal]` delegate forwarding to a composed F# instance; `[GlobalClass]`).
  Command: `<godot> --headless --path . -- --forward-test` ->
  `[patrol-logic] OnReady #1  Waypoints=7  Label=north-ridge`;
  `Waypoints in property list: True`; `Label in property list: True`;
  `HasSignal(PatrolCompleted): True`; `received PatrolCompleted(3)`;
  `set Waypoints=7 via property -> F# logic reads 7`. Godot's own generator
  builds the full bridge over a pure-forwarding C# stub; the inspector /
  `.tscn` round-trip works; the Myriad plugin would never touch
  `godot_variant` / `NativeVariantPtrArgs`. ADR-0004 "Myriad and the shim"
  option 1 is now **proven feasible** with this evidence; option 2 (reimplement
  Godot's generators in F#) stays a research project pinned to the engine
  interop ABI.

### Evidence - question 5 (debugging)

- Command: `<godot> --headless --path . -- --trace-test` (raises
  `System.Exception` in F# `ClientHost.ForceFSharpFault`, caught in C#
  `MainShim._Ready`, `ex.ToString()` printed)
  - Result:
    ```
    System.Exception: deliberate fault raised inside ClientHost (F#)
       at <StartupCode$ClientCore>.$Host.inner@170.Invoke(Unit unitVar0) in ...\ClientCore\Host.fs:line 170
       at CwClientCore.ClientHost.ForceFSharpFault() in ...\ClientCore\Host.fs:line 171
       at MainShim._Ready() in ...\src\MainShim.cs:line 58
    ```
    C#->F# stack traces stay readable with exact F# file/line; closure frames
    carry a compiler-mangled name but the source location is precise. Portable
    PDBs for `ClientCore.dll` / `CommandoWar.Sim.dll` are in Godot's output.
- Interactive F# breakpoint from an editor / F5-launched run: **not verified**
  (no interactive debugger in-session). Godot's C# debugger is IL-level SDB;
  F# emits IL with sequence points and portable PDBs, so F# breakpoints should
  behave as C# ones do. ADR-0004 review trigger 1.

### Verification

- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed! - Failed: 0, Passed: 54` before and after (no source,
    test, or fixture file under `CommandoWar.Sim` / `tests/` changed).
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only.
- Command: source scan of `src/CommandoWar.Sim` + `src/CommandoWar.Headless` for
  `godot|node2d|vector2|_process|monogame|raylib`
  - Result: one doc-comment line in `Fixture.fs`; no type or API.
- Command: `git status --porcelain`
  - Result: modified `PROJECT_STATE.yaml`, `docs/03_ARCHITECTURE.md`,
    `docs/10_RISK_REGISTER.md`, `docs/11_BACKLOG.md`,
    `docs/12_PROGRESS_LEDGER.md`; new `decisions/ADR-0004-*.md`,
    `tasks/TASK-007-*.md`, `docs/evidence/task-007-godot-fsharp-boundary.png`,
    `src/_scratch/godot-fsharp-boundary/`. No change under `src/CommandoWar.Sim`,
    `src/CommandoWar.Headless`, `tests/`, the retained spikes, `content/`, or
    `CommandoWar.slnx`.

### Deviations and unresolved issues

- Godot's `--path` game run loads the **Debug** assembly, not Release; the
  first headless run needs a prior `--editor --headless --quit` import pass.
  Recorded in the proof README.
- Interactive Godot editor GUI and interactive debugger are not drivable in
  this session (same limitation class as TASK-004 / TASK-006). Question 1 is
  answered from a headless load + code inspection; the F# breakpoint check is
  an ADR-0004 review trigger, not a sealed result.
- The proof's screenshot projection is crude (agents overlap at the origin). It
  is evidence of the pattern, not a rendering result.
- ADR-0004 is `proposed`. `PROJECT_STATE.yaml` gates, `current_gate`,
  `current_phase`, and `framework_decision` are unchanged - this task does not
  move the gate. `active_work.selected_task` stays `TASK-007` pending review.
- No P4 client task (B-024+) was started. The TASK-004 spike's `SimFacade.cs` /
  `SpikeContent.cs` are not yet ported to F#; ADR-0004 says B-024 / B-026 do
  that.

### Documents updated

- `tasks/TASK-007-GODOT-FSHARP-BOUNDARY.md` (new; status `review`, criteria
  checked, completion notes)
- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` (new, `proposed`)
- `docs/03_ARCHITECTURE.md` (section 14 rewritten; header revision date)
- `docs/10_RISK_REGISTER.md` (R-004 mitigation)
- `docs/11_BACKLOG.md` (TASK-007 row added, `active -> review`)
- `PROJECT_STATE.yaml` (`active_work` -> TASK-007; note removed)
- `src/_scratch/godot-fsharp-boundary/` (new, disposable, not in any `.slnx`;
  includes follow-up 1 `FSharpSceneHost.cs` / `Scene.fs` / `SceneHost.tscn` and
  follow-up 2 `NodeLogic.fs` / `GeneratedStyleNode.cs`)
- `docs/evidence/task-007-godot-fsharp-boundary.png` (new)
- `docs/12_PROGRESS_LEDGER.md` (this entry)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-03; see the `2026-09-03-TASK-007-acceptance.md` ledger detail file)
- Notes: ADR-0004 accepted. The disposable proof and question 1-5 evidence
  accepted as-is. The F# breakpoint check and the C# shim hot-reload check are
  carried forward to the first P4 client task (B-026 / B-027) as ADR-0004
  review triggers.
