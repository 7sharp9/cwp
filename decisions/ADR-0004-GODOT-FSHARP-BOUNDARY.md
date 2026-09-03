# ADR-0004: Low-Impedance Godot C#/F# Client Boundary

Status: accepted
Date: 2026-09-03
Date accepted: 2026-09-03 (Dave)
Decision owner: Dave
Task: TASK-007

## Context

ADR-0001 selected Godot .NET for the production client and named one follow-up:
keep C# a thin, dumb host-and-glue layer with the real client logic in F#
(view-model preparation, input-to-command mapping, fixed-step scheduling,
overlay state, replay-relevant messages). The accepted weakness "mixed C#/F#
solution" (ADR-0001 Qualitative decision, weakness 3; `docs/10` R-004) is the
one Dave called the priority to contain.

The open question was whether F# can define Godot node subclasses directly on
Godot 4.7.2, or whether a minimal C# node shim per scene is the stable answer.
The TASK-004 spike put the entire sim boundary in a C# facade
(`SimFacade.cs`, ~150 lines) and the host loop, input, render, and overlay in a
C# node (`MainNode.cs`, ~330 lines). The C# facade carried a small but real
interop tax: `AgentIdModule.ofInt`, `World.create` via `ListModule.OfArray`,
`FSharpOption<Cell>.get_IsSome`.

ADR-0002 is unchanged by this decision and is satisfied throughout (see
"ADR-0002 compliance" below). This ADR only refines how the client side is
structured, which ADR-0002 explicitly left to "a client or headless runner".

## Evidence

A disposable proof under `src/_scratch/godot-fsharp-boundary/` (not in any
`.slnx`) was built and run headless on Godot
`4.7.2.stable.mono.official.ed1daf0bf`, .NET SDK `10.0.303`, `net10.0`,
`Godot.NET.Sdk` / `GodotSharp` `4.7.2`.

### Question 1 - can an F# type be a Godot node?

`ClientCore/FSharpNode.fs`: `type FSharpHostNode() = inherit Godot.Node2D()`
with overrides for `_EnterTree`, `_Ready`, `_Notification`, `_Process`,
`_UnhandledInput`, `_Draw`, and a `[<Export>] member val Caption`. Compiled into
a plain `Microsoft.NET.Sdk` F# library referencing the `GodotSharp` package (no
`Godot.NET.Sdk`, no `Godot.SourceGenerators`).

- **Compiles clean.** `dotnet build ClientCore.fsproj -c Release` -> 0 warnings,
  0 errors. F# inherits `Node2D` and overrides its virtuals without complaint.
- **Loads into the tree.** The C# shim does `AddChild(new FSharpHostNode())`;
  `IsInsideTree()` returns `True` and Godot even synthesises a `CSharpScript`
  wrapper for the type.
- **Godot never drives it.** Across six engine `_Process` frames, not one
  `[fsharp-node]` lifecycle line printed - no `_EnterTree`, `_Ready`,
  `_Notification`, `_Process`, `_UnhandledInput`, or `_Draw`.
  `fnode.HasMethod("_process")` returns `False`. Godot decides which managed
  virtuals to call from the per-class `HasGodotClassMethod` /
  `InvokeGodotClassMethod` / `GetGodotPropertyList` overrides that
  `Godot.SourceGenerators` emits from the user's `[Export]`/`[Signal]`/override
  declarations. F# has no Roslyn source generator, so Godot sees a node with no
  overridable methods and no exported properties.
- **The override bodies are fine** under a direct C# virtual call: the shim
  calling `fnode._Ready()` runs the F# body, which reads its
  `[<Export>]`-attributed member (C# had set `Caption = "assigned-from-C#"` and
  the F# body saw it).
- **`[<Export>]` is invisible.** `GetPropertyList()` contains no `caption`
  entry - no inspector field, no `.tscn` serialisation.
- **Signals: runtime API only.** `AddUserSignal("fsharp_pinged")` in the F#
  body, then `Connect` + `EmitSignal` from C#, works both ways.
  Attribute-declared `[Signal]` would not register (same generator gap;
  inferred, consistent with the `[<Export>]` result).
- **Not exercised, unavailable for the same reason:** editor "attach script" by
  `res://` path (needs the generated `[ScriptPath]`), `[GlobalClass]`,
  inspector editing, and C#-style hot-reload. All depend on the generator plus
  `[ScriptPath]`.

**Conclusion: an F# type cannot be a Godot scene entry point on 4.7.2.** It can
only be driven if a C# owner forwards every lifecycle call by hand, at which
point its `Node`-ness buys nothing over a plain F# class.

### Questions 2-3 - the shim and the per-concern split

`src/MainShim.cs` is the whole C# surface. `--selfcheck` reproduces the shared
fixture's 41-hash sequence exactly (initial `0xF2F3DF0D820AD9AC`, ticks 1..40,
final `0x838D3AE7DBFB735D`, `accepted-command-log: 1 3 move 20 14`), MATCH,
exit 0; a deliberate `--expect` mismatch exits 1. All stepping, the fixture
command, selection, the overlay string, and the self-check body are in F#
(`ClientCore/Host.fs`).

The per-scene shim then collapses to **one generic C# class for the whole
client**. `src/FSharpSceneHost.cs` (~35 lines) is a `Node2D` that resolves an
F# `CwClientCore.IClientScene` implementation named by an `[Export] SceneType`
string and forwards `_Ready` / `_Process` / `_UnhandledInput` / `_Draw` /
`_ExitTree` to it. `scenes/SceneHost.tscn` sets
`SceneType = "CwClientCore.FixtureSelfCheckScene"`; running it as the main
scene (`--main-scene res://scenes/SceneHost.tscn`) prints
`tick=0 hash=0xF2F3DF0D820AD9AC` ... `final tick=40 hash=0x838D3AE7DBFB735D`,
`MATCH`, `[fixture-scene] ExitTree`, exit 0. No scene-specific C# exists.

What the generic host gives up versus a per-scene C# shim: per-member
`[Export]` inspector fields, `[Signal]`, and editor "attach script". The F#
scene wires itself with `GetNode` and a passed host `Node2D`. If typed,
inspector-wired `[Export]` refs per scene become worthwhile, the recovery path
is Myriad, not a hand-written per-scene shim (see "Myriad and the shim" below).

### Question 4 - the interop idiom

`ClientCore/Host.fs` puts the sim facade (`SimHost`) in F#. The spike's
`AgentIdModule.ofInt` / `ListModule.OfArray` / `FSharpOption.get_IsSome`
disappear: inside F# they are `AgentId.ofInt`, list literals, and
`match ... with Some/None`. C# calls `SimHost.Step() : TickView`,
`SimHost.AgentViews : AgentView[]`, `ClientHost.SelfCheck(expect: Nullable<uint64>)`.
`AgentView` / `TickView` are `[<CLIMutable>]` records - plain get/set POCOs to
C#.

### Question 5 - debugging

`--trace-test` raises `System.Exception` inside F# `ClientHost.ForceFSharpFault`
and catches it in the C# shim. `ex.ToString()`:

```
System.Exception: deliberate fault raised inside ClientHost (F#)
   at <StartupCode$ClientCore>.$Host.inner@170.Invoke(Unit unitVar0) in ...\ClientCore\Host.fs:line 170
   at CwClientCore.ClientHost.ForceFSharpFault() in ...\ClientCore\Host.fs:line 171
   at MainShim._Ready() in ...\src\MainShim.cs:line 58
```

C#->F# stack traces stay readable, with exact F# file and line. Closure frames
carry a compiler-mangled name (`<StartupCode$ClientCore>.$Host.inner@170`) but
the source location is precise. Portable PDBs for `ClientCore.dll` and
`CommandoWar.Sim.dll` are copied into Godot's output.

Interactive breakpoints in F# from an editor/F5-launched run were **not
verified** (no interactive debugger in-session, same limitation class as the
still-unmeasured Godot editor GUI). Godot's C# debugger is an IL-level Soft
Debugger; F# emits IL with sequence points and portable PDBs, so F# breakpoints
should behave as C# breakpoints do. Recorded as review trigger 1.

## Decision

### The split

The production Godot client is **a thin C# host over an F# client-core
library.** Two forms, in order of preference:

1. **One generic C# host for the whole client** (`FSharpSceneHost : Node2D`,
   ~35 lines, written once). Every `.tscn` entry point uses it as its root and
   sets `[Export] SceneType` to the F# `IClientScene` implementation. The host
   resolves the type and forwards lifecycle calls. No per-scene C#. Proven in
   the disposable proof. Cost: the F# scene wires itself with `GetNode`; no
   per-member `[Export]`, `[Signal]`, or editor "attach script".

2. **A per-scene C# shim** only where a scene genuinely needs typed,
   inspector-wired `[Export]` refs or `[Signal]`. Same "no logic in C#" rule.
   If several scenes need this, prefer generating the shim with Myriad over
   hand-writing it (see "Myriad and the shim").

Both forms sit over the same F# client-core library and obey the same rules
below. Start every scene on form 1; move a scene to form 2 (or Myriad) only
when its wiring demonstrably needs it.

- **C# shim / host** (`Node2D | Control | Node`): the scene root, script
  attached in the editor the normal way. `[Export]` fields, editor
  integration, and hot-reload live here because only C# gets the source
  generator. It holds:
  - `[Export]` node / `PackedScene` / `Resource` refs the scene wires;
  - `[Export]` tuning scalars;
  - one field referencing the F# host object;
  - the deferred-exit int for headless runs (Godot honours
    `SceneTree.Quit(code)` only on the next `_Process`).

  It forwards, one line per callback:
  - `_Ready` -> parse args, `FHost.Create`, pass the exported refs into F#;
  - `_Process(delta)` -> `host.Update(delta)`; then `QueueRedraw()`;
  - `_UnhandledInput(e)` -> marshal the `InputEvent` to primitives / a small
    neutral record, call `host.OnPointer` / `host.OnKey`;
  - `_Draw` -> `foreach (v in host.DrawList) Draw*(...)`;
  - `_ExitTree` / close request -> `host.Dispose()`.

  `_PhysicsProcess` is unused: the host owns the fixed-step accumulator.

- **F# client-core library** (`CommandoWar.Client.Godot.Core` or similar, a
  separate project from `CommandoWar.Sim`, may reference `GodotSharp`): owns the
  sim facade, fixed-step scheduling, input-to-command mapping, view-model and
  overlay preparation, selection/hover state, and replay-relevant messages.

- **F# types are never scene entry points.** No F# `Node` subclass in a
  `.tscn`, no F# `[GlobalClass]`.

### The "no logic in C#" rule

A shim method body may contain only Godot-type marshalling and screen<->cell
projection arithmetic. It may not contain: a scheduling accumulator, command
construction, branching on authoritative or game state, view-model computation,
overlay strings, or content validation. If a shim method is more than
"marshal + forward", the logic is on the wrong side of the boundary.

Concrete size: in the proof the real shim surface is ~35 lines (the file is
larger only because it also carries the question-1 probe harness).

### Per-concern placement

| Concern | Side | Note |
|---|---|---|
| Fixed-step scheduling | F# | `ClientHost.Update(deltaSeconds)` owns the accumulator, `1/hz` step, catch-up cap, pause/step. The sim only ever gets an integer tick. C# passes the wall-clock delta. |
| Raw input capture | C# | `_UnhandledInput` must receive `Godot.InputEvent`; the shim extracts primitives. |
| Input -> typed command | F# | selection vs. move vs. reject, and `Command.moveTo` construction. Replay-relevant. |
| Render loop | C# `_Draw`, view model from F# | `_Draw` must run in a `CanvasItem`; the shim only issues `Draw*` calls. F# builds the immediate-mode draw list from `RenderSnapshot` + interpolation. Retained-mode sprite nodes later: C# creates/frees nodes from a diff F# computes. |
| View-model prep from `RenderSnapshot` | F# | interpolation, threat confidence/age, cover/exposure preview, disposition. |
| Overlay state (`docs/06` s11) | F# | `ClientHost.OverlayText` and the developer-overlay data model; C# renders it. |
| `.tscn` content import | C# reader at the Godot edge, F# from the DTO on | the reader touches `Node` / `Vector2I` / `TileMapLayer` / `PackedScene` (like `GreyboxScene.cs`) and emits framework-neutral DTOs; validation (port `SpikeContent`'s validator to F#), version checks, and sim setup are F#. No Godot type past the DTO. |

### The interop idiom

- Put the sim facade in **F#**, not C#. Every `open CommandoWar.Sim` stays
  inside the F# client-core assembly.
- C# <-> F# traffic is: primitives, `System.Nullable<T>`, arrays of
  `[<CLIMutable>]` records, and `Godot.InputEvent` subtypes passed *into* F#
  client methods (intra-client, allowed).
- Never expose to C#: F# `list`, `option`, `Result`, DU cases, tuples,
  `unit`-as-value, or module functions. Expose `[<CLIMutable>]` records,
  `System.Nullable`, arrays, primitives, plain methods.
- `[<CLIMutable>]` records gain a public constructor and setters, so they are
  mutable-in-principle from C#. The boundary treats them as write-once view
  models (the same caveat the TASK-004 spike recorded for `AgentState[]`).

## Myriad and the shim

Godot's C# integration is generator-bound: `Godot.SourceGenerators` (Roslyn,
C#-only) emits, per script class, `[ScriptPath]` + `[assembly: AssemblyHasScripts]`,
the `InvokeGodotClassMethod` / `HasGodotClassMethod` virtual-dispatch bridge,
`GetGodotPropertyList` and the property get/set bridges for `[Export]`, the
signal bridges, and the hot-reload serialization hooks. Godot's C++ side gates
virtual invocation on `HasGodotClassMethod`, which is why an F# node with no
generated bridge is never called (question 1 evidence).

Myriad (F# source generator, Dave-owned) could close this in two ways:

1. **F# is the source of truth; Myriad emits a mechanical C# forwarder;
   Godot's own generator runs over that.** The F# type carries the logic, the
   members, and lightweight attributes describing Godot intent
   (`[<GodotExport>]`, `[<GodotSignal "name">]`, `[<GodotGlobalClass>]`,
   `[<GodotTool>]`). A Myriad plugin reads them and writes
   `partial class <Name> : Node2D` whose `[Export]` properties get/set-forward
   to a composed F# instance, whose `[Signal]` delegates forward, and whose
   lifecycle overrides forward. Godot's own `Godot.SourceGenerators` then
   produces every ABI-bound bridge (`InvokeGodotClassMethod`,
   `GetGodotPropertyList`, the signal bridge, hot-reload hooks) over the
   forwarder. **The Myriad plugin never touches `godot_variant` /
   `NativeVariantPtrArgs` / the marshalling helpers** - that risk stays with
   Godot's generator, which absorbs it across engine upgrades.

   **Proven** in the disposable proof: `src/GeneratedStyleNode.cs` is
   hand-written to be exactly what such a plugin would emit from
   `ClientCore/NodeLogic.fs` (`PatrolMarkerLogic`). `--forward-test` shows
   Godot drives the stub's `_Ready` (forwards to F# `OnReady`, which reads the
   `[Export]` values set through the forwarding properties), `Waypoints` and
   `Label` appear in `GetPropertyList()` (inspector + `.tscn` round-trip),
   `HasSignal("PatrolCompleted") = True`, and `Connect` + `EmitSignal`
   round-trips. Set-via-property then F# reads it back: 7 -> 7.

   Friction (bounded, not blocking): the F# author declares signal shape via an
   attribute payload rather than C#'s inline `delegate` syntax; `[Export]`
   members must use CLR-friendly types (same discipline as the interop idiom
   above); hot-reload preserves only `[Export]` state (same as pure C#); the
   `.cs` is a build artefact, so "attach script" means attaching a generated
   file (`[<GodotGlobalClass>]` -> Create Node dialog avoids this). Effort: a
   few hundred lines of Myriad plugin plus a per-`[Export]`-type test matrix.
   This is the path to reach for if form 1's generic host proves insufficient.

2. **Myriad reimplements the Godot generators in F#**, so F# types are native
   Godot scripts with no C# at all. This pins a Dave-owned generator to Godot's
   interop ABI (`godot_variant` layout, `NativeVariantPtrArgs`, the marshalling
   helpers - all "public but unstable"), to be re-checked every Godot minor
   upgrade, with F# `ref struct` friction in the dispatch signatures. High
   effort, ongoing maintenance. Only justified if forms 1 and Myriad-path-1
   both fail, or if Godot stabilises a managed-type registration API.

Neither is needed for the vertical slice: the generic C# host (form 1) already
puts all logic in F# with one C# file for the whole client. Myriad-path-1 is
the first upgrade to reach for and is proven feasible; Myriad-path-2 is a
research project. See review trigger 2.

## ADR-0002 compliance

- `CommandoWar.Sim` package tree is `FSharp.Core` only; source scan of
  `src/CommandoWar.Sim` and `src/CommandoWar.Headless` for
  `godot|node2d|vector2|_process|monogame|raylib` is clean; 54 tests green,
  framework-neutral. **Unchanged by this ADR.**
- The new F# client-core project references `GodotSharp`. This does not violate
  ADR-0002: it is a *client*, and ADR-0002's arrows permit `Client -> ... ->
  Sim`, never the reverse. "F# on both sides" now means two distinct F#
  projects: `CommandoWar.Sim` (no framework reference, ever) and the F# client
  core (may reference `GodotSharp`).
- No forbidden type crosses into a sim contract: no `Node`, `Vector2`, signal,
  or physics object; no delegate that lets the sim call host behaviour; no
  async callback mutating authoritative state. `Godot.InputEvent` is passed only
  into F# client methods, never toward `CommandoWar.Sim`.
- `[<CLIMutable>]` records are "immutable or serializable records" per the
  ADR-0002 allow-list.

This is an ADR-0004, not an ADR-0002 amendment: ADR-0002's dependency
direction, ownership split, and allow/forbid lists are unchanged and satisfied.

## Review triggers

1. The first P4 client task (B-026 / B-027) must confirm an F# breakpoint is hit
   from an editor / F5-launched run, and that hot-reloading the C# shim does not
   sever the F# host reference. If either fails, reopen this ADR.
2. If the generic C# host (form 1) proves insufficient for a real scene -
   several scenes need typed inspector-wired `[Export]` refs, `[Signal]`, or
   `[GlobalClass]` - build the Myriad plugin that emits the dumb C# forwarder
   per F# node (Myriad-path-1), rather than hand-writing per-scene shims. Only
   consider Myriad-path-2 (F#-native Godot scripts) if that also fails or if
   Godot ships a stable managed-type registration API. Neither reopens
   ADR-0001.
3. If any per-scene C# shim (form 2) exceeds ~60 lines or grows a branch on
   game state, that logic leaked - move it to F# and record why the boundary
   was unclear.
4. If a `[<CLIMutable>]` view record is mutated by C# and that mutation is
   observed as authoritative, that is an ADR-0002 compliance failure: stop and
   fix.

## Consequences

- P4 client tasks (B-024 content importer, B-026 input, B-027 rendering, B-028
  reason UI, B-029 developer overlays) create one C# shim per scene and put
  their logic in the F# client-core library. The TASK-004 spike's
  `SimFacade.cs` and `SpikeContent.cs` are ported from C# to F# when B-024 /
  B-026 begin (they are framework-neutral already; the port removes the interop
  tax).
- `docs/03_ARCHITECTURE.md` section 14 gains the boundary rule.
- `docs/10_RISK_REGISTER.md` R-004: mitigation updated to name this ADR; status
  stays `mitigating` until review trigger 1 is discharged.
- The disposable proof is retained under `src/_scratch/` as evidence for this
  ADR, exactly as the TASK-004/005 spikes are retained. It is removable
  wholesale.

## Rollback

If P4 client evidence contradicts this structure (for example the shim cannot
stay thin, or F# hot-reload / debugging proves unworkable in practice),
supersede this ADR with a new one carrying that measured evidence. Do not
silently move logic back into C#.
