# TASK-007: Design the low-impedance Godot C#/F# client boundary

Status: review
Owner: Dave
Phase: P2
Gate: G2 (does not move the gate; unblocks P4 client work)
Size: S

## Objective

Decide and record how the production Godot client keeps C# to a thin, dumb
host-and-glue layer with the real client logic (view-model preparation,
input-to-command mapping, fixed-step scheduling, overlay state,
replay-relevant messages) in F#. Produce ADR-0004 (or an ADR-0002 amendment if
that is the correct instrument) plus a minimal disposable proof.

This is the ADR-0001 "named follow-up" and the accepted weakness "mixed C#/F#
solution" (ADR-0001 Qualitative decision, weakness 3; `docs/10_RISK_REGISTER.md`
R-004). It is design plus a minimal disposable proof, **not** vertical-slice
work.

## Why this task exists

ADR-0001 selected Godot .NET with the F# `CommandoWar.Sim` behind a thin C#
facade, and Dave named keeping the C#/F# boundary low-impedance as the priority
for follow-on work. The TASK-004 spike left one question open: can F# define
Godot node subclasses directly on Godot 4.7.2 (the editor script integration
and source generators are Roslyn/C#-specific), or is a minimal C# node shim per
scene the idiomatic and stable answer. That question must be answered with
evidence before P4 client tasks (B-024+) start building real client code.

## Required reading

1. `PROJECT_STATE.yaml`, `AGENTS.md`
2. `decisions/ADR-0001-FRAMEWORK-SELECTION.md` (the "named follow-up" bullet,
   the "mixed C#/F# solution" weakness, "Pinned versions"),
   `decisions/ADR-0002-SIMULATION-BOUNDARY.md` (dependency direction and the
   "Data crossing the boundary" allow/forbid lists), `decisions/ADR-0003`
   2026-09-03 note
3. `docs/03_ARCHITECTURE.md`; `docs/06_CONTENT_AND_PRESENTATION.md` sections 2,
   5, 10, 11
4. `src/CommandoWar.Client.Godot/` as the current boundary shape
5. `src/CommandoWar.Headless/` (`Fixture.fs`, `Program.fs`)
6. The TASK-004 ledger entry and the 2026-09-03 "TASK-006 finalisation" entry
   in `docs/12_PROGRESS_LEDGER.md`

## Dependencies

- TASK-006 accepted and `done`; ADR-0001 accepted.

## Inputs and assumptions

- Godot `4.7.2.stable.mono.official.ed1daf0bf` at
  `C:\Users\Dave\Documents\GitHub\Godot_v4.7.2-stable_mono_win64\` (not on PATH).
- .NET SDK `10.0.303`, `net10.0`, `Godot.NET.Sdk` / `GodotSharp` `4.7.2`.
- Godot's script tooling (source generators, `[Export]`/`[Signal]` metadata,
  editor "attach script", hot-reload) is Roslyn/C#-specific; F# has no
  equivalent generator.
- `dotnet test CommandoWar.slnx -c Release` = 54 green, framework-neutral.
- The Godot client is its own `.slnx`, not in `CommandoWar.slnx`.
- Shared fixture final hash `0x838D3AE7DBFB735D`.
- If the Godot editor GUI still cannot be driven in-session, question 1 is
  answered from a headless load plus code inspection, and that is stated.

## Allowed scope

- a minimal disposable proof: a new `src/_scratch/` throwaway Godot project
  (not in any `.slnx`) or an extension of the retained Godot spike, showing the
  recommended pattern with one F#-backed node and reproducing the shared
  fixture hash;
- ADR-0004 (accepted or proposed), or an ADR-0002 amendment if that is the
  right instrument;
- `docs/03_ARCHITECTURE.md` boundary-rule update if it belongs there;
- the required control-document updates (task, backlog, ledger,
  `PROJECT_STATE.yaml`).

## Forbidden scope

- forking or patching Godot;
- starting any B-007+ simulation task or B-024+ client task;
- building a UI framework, asset pipeline, or real scenes;
- changing `CommandoWar.Sim` or its tests;
- adding either client host to `CommandoWar.slnx`;
- a Godot/Mibo abstraction;
- destructive git operations.

## Required work

Answer, with evidence not assertion:

1. Can an F# type deriving from a Godot node (`Node` / `Node2D` / `Control`) be
   loaded and driven by Godot 4.7.2 - lifecycle callbacks, `[Export]` fields
   visible to the inspector, signals? Test it in a throwaway. Record exactly
   what works and what does not.
2. If F# nodes are not viable as scene entry points, define the minimum C# shim
   per scene entry point: what it holds (`[Export]` refs, lifecycle
   forwarders), what it forwards to F#, and the "no logic in C#" rule.
3. For each of fixed-step scheduling, input capture and mapping to typed
   commands, the render loop (`_Draw` vs a command buffer), view-model prep
   from `RenderSnapshot`, overlay state (`docs/06` s11), and `.tscn` content
   import: C# or F#, and why.
4. What crosses the C#<->F# call boundary, and does any of it strain ADR-0002.
   Is there a cleaner idiom than the spike's `AgentIdModule.ofInt` /
   `ListModule.OfArray` / `FSharpOption.get_IsSome` interop (C#-friendly
   wrapper functions in an F# boundary module, `[<CLIMutable>]` records)?
5. Debugging: can you hit a breakpoint in F# client code from a Godot-launched
   run, and does a C#->F# stack trace stay readable? Test it.

## Acceptance criteria

- [x] Questions 1-5 are each answered with recorded evidence (commands, output,
      or code inspection), not assertion. ADR-0004 "Evidence" section; the
      2026-09-03 ledger entry has the exact commands and output.
- [x] ADR-0004 records: the decided split, the C# shim pattern with a concrete
      example (`src/_scratch/godot-fsharp-boundary/src/MainShim.cs`), the
      interop idiom, what stays C#, the ADR-0002 compliance check, and review
      triggers. `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` (`proposed`).
- [x] A minimal disposable proof exists (`src/_scratch/godot-fsharp-boundary/`),
      is not in any `.slnx`, is marked disposable (its README + `_scratch`
      path), shows the recommended pattern (C# shim over F# `ClientHost`), and
      reproduces the shared fixture hash: `--selfcheck --expect
      0x838D3AE7DBFB735D` -> MATCH, exit 0, driven from a Godot-launched
      process.
- [x] `dotnet test CommandoWar.slnx -c Release` -> `Passed! Failed: 0, Passed:
      54` before and after.
- [x] `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
      --include-transitive` = `FSharp.Core 10.1.303` only; source scan of
      `src/CommandoWar.Sim` + `src/CommandoWar.Headless` for
      `godot|node2d|vector2|_process|monogame|raylib` matches only one
      doc-comment line in `Fixture.fs`.
- [x] `docs/03_ARCHITECTURE.md` section 14 rewritten with the boundary rule.
- [x] Backlog row (`active -> review`), ledger entry, `PROJECT_STATE.yaml`
      (`active_work` -> TASK-007), and task status (`review`) updated.
- [x] No forbidden scope entered: no Godot fork/patch; no B-007+ / B-024+ work;
      no UI framework / asset pipeline / real scenes; `CommandoWar.Sim` and its
      tests unchanged; nothing added to `CommandoWar.slnx`; no framework
      abstraction; no destructive git.

## Completion notes (2026-09-03)

**Question 1 - F# nodes:** an F# `Node2D` subclass compiles clean against
`GodotSharp` 4.7.2 and loads into the tree, but Godot never drives its
lifecycle callbacks (`HasMethod("_process") = False`) because
`Godot.SourceGenerators` is C#-only; `[<Export>]` is absent from
`GetPropertyList()`; custom signals work only through the runtime
`AddUserSignal` API. F# types are not viable as scene entry points. The
override bodies run under a direct C# call.

**Questions 2-4:** the C# shim is marshal-and-forward only (~35 lines of real
surface); the sim facade moves to F#, which deletes the spike's
`AgentIdModule.ofInt` / `ListModule.OfArray` / `FSharpOption` interop; only
primitives, `System.Nullable<T>`, `[<CLIMutable>]` records, and
`Godot.InputEvent` (into F# client methods) cross. Per-concern table in
ADR-0004.

**Follow-up 1 (Dave asked whether F# can be "more core"):** the per-scene shim
collapses to one generic `FSharpSceneHost.cs` (~35 lines) for the whole client
that resolves an F# `IClientScene` from an `[Export] SceneType` -
`scenes/SceneHost.tscn` reproduces `0x838D3AE7DBFB735D` with zero scene-specific
C#. ADR-0004 makes this form 1 (preferred).

**Follow-up 2 (Dave asked whether F# can hold `[Export]`/`[Signal]`/
`[GlobalClass]` with Myriad emitting only the C# forwarder):** proven feasible.
`ClientCore/NodeLogic.fs` holds the members + `[<GodotExport>]` /
`[<GodotSignal>]` markers; `src/GeneratedStyleNode.cs` (hand-written as a Myriad
plugin would emit it) forwards to a composed F# instance. `--forward-test`:
`[Export]`s appear in `GetPropertyList()`, the `[Signal]` is registered,
`Connect`/`EmitSignal` round-trips, an inspector-set value is read back in F#.
Godot's own generator does all the ABI-bound work over the forwarder. ADR-0004
"Myriad and the shim" option 1.

**Question 5:** C#->F# exception stack traces are readable with exact F#
file/line (`--trace-test`). Interactive F# breakpoints from an editor-launched
run were not verified in-session (no interactive debugger); recorded as
ADR-0004 review trigger 1.

**ADR-0002 compliance:** clean - `CommandoWar.Sim` still `FSharp.Core` only, 54
tests green, no framework type in a sim contract. ADR-0004 is a new ADR, not an
ADR-0002 amendment.

Deviations: interactive Godot editor GUI and interactive debugger not drivable
in-session (same limitation class as TASK-004/006); the disposable proof's
screenshot projection is crude (agents overlap at the origin) - it is evidence
of the pattern, not a rendering result.

## Required verification

- `dotnet test CommandoWar.slnx -c Release` before and after: 54 green.
- Build the disposable proof.
- Proof reproduces the fixture hash headless.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive` = `FSharp.Core` only.
- Source scan of `src/CommandoWar.Sim` for `godot|Node2D|Vector2|...`: clean.

## Evidence to capture

- exact commands and output for questions 1-5;
- the disposable proof's build + headless self-check output;
- the interop-idiom before/after;
- pinned versions.

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md` TASK-007 row;
- `docs/12_PROGRESS_LEDGER.md` entry;
- `PROJECT_STATE.yaml` `active_work`;
- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` (this task owns the decision);
- `docs/03_ARCHITECTURE.md` if the boundary rule belongs there;
- `docs/10_RISK_REGISTER.md` R-004 if its status changes.

## Rollback or removal

The disposable proof must be removable without changing or deleting
`CommandoWar.Sim`, its tests, `CommandoWar.Headless`, the retained spikes, the
command logs, or the shared fixture. ADR-0004 stands on its own; if later
client evidence contradicts it, supersede it with a new ADR.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next task.

## Alternative

If Dave would rather advance the deterministic core first, the next task is
B-007 (scenario DTO + content version, G2), which needs a task file written
first.
