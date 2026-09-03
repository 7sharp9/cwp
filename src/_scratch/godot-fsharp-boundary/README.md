# TASK-007 disposable proof: the Godot C#/F# client boundary

**Disposable.** This exists only to produce ADR-0004 evidence. It is **not** in
any `.slnx`, not production code, and removable wholesale (`rm -rf
src/_scratch/godot-fsharp-boundary`) without touching `CommandoWar.Sim`, its
tests, `CommandoWar.Headless`, the retained spikes, the command logs, or the
shared fixture.

## What it shows

1. **The recommended pattern.** `src/MainShim.cs` is a ~35-line C# shim (scene
   root, `[Export]` lives here) that only marshals Godot types and forwards
   lifecycle calls. All client logic - the sim facade, fixed-step scheduling,
   input-to-command mapping, the view model, the overlay string, the headless
   self-check - is in the F# `ClientCore` library (`Boundary.fs`, `Host.fs`).
   `--selfcheck` reproduces the shared fixture's 41-hash sequence exactly,
   final `0x838D3AE7DBFB735D`, driven from a Godot-launched process.

2. **Why F# nodes are not scene entry points (question 1).** `ClientCore` also
   contains `FSharpNode.fs`: `type FSharpHostNode() = inherit Godot.Node2D()`
   with lifecycle overrides and a `[<Export>]`. It compiles clean against
   `GodotSharp` 4.7.2. `--probe` shows that once added to the tree Godot never
   invokes any of its callbacks (`HasMethod("_process") = False`) because the
   C#-only `Godot.SourceGenerators` never ran over it; the override bodies work
   only under a direct C# call, and the `[<Export>]` is absent from
   `GetPropertyList()`.

3. **C#->F# stack traces (question 5).** `--trace-test` raises inside F# and
   catches in C#; `ex.ToString()` carries F# file/line frames.

4. **The per-scene shim collapses to ONE C# file (follow-up).**
   `src/FSharpSceneHost.cs` is a single generic `Node2D` that resolves an F#
   `CwClientCore.IClientScene` implementation named by an `[Export] SceneType`
   and forwards every lifecycle call. Every `.tscn` uses it as the root and
   names its F# scene type; no scene-specific C# is ever written.
   `scenes/SceneHost.tscn` + `ClientCore/Scene.fs` (`FixtureSelfCheckScene`)
   reproduce `0x838D3AE7DBFB735D` through it. What you give up vs. a per-scene
   C# shim: per-member `[Export]` inspector fields, `[Signal]`, and editor
   "attach script" - the F# scene wires itself with `GetNode`.

5. **F# as the source of truth, Myriad-emits-the-forwarder is feasible
   (follow-up 2).** `ClientCore/NodeLogic.fs` (`PatrolMarkerLogic`) holds the
   members and `[<GodotExport>]` / `[<GodotSignal>]` intent;
   `src/GeneratedStyleNode.cs` is hand-written to be exactly what a Myriad
   plugin would emit - `[Export]` properties and a `[Signal]` delegate that
   forward to a composed F# instance. `--forward-test` shows Godot drives it
   normally: `[Export]`s in the property list (inspector + `.tscn` round-trip),
   signal registered, `Connect`/`EmitSignal` round-trip, inspector-set value
   read back in F#. So the C# is 100% mechanical forwarding and Godot's own
   generator does the ABI-bound work.

## Layout

```
GodotFSharpBoundary.csproj   Godot.NET.Sdk 4.7.2 project; C# shim + source generators
src/MainShim.cs              the minimal C# shim (scene root)
Main.tscn                    root = MainShim
ClientCore/ClientCore.fsproj Microsoft.NET.Sdk F# lib; refs CommandoWar.Sim + GodotSharp pkg
ClientCore/Boundary.fs       [<CLIMutable>] view records; the C#<->F# shapes (NO Godot types)
ClientCore/Host.fs           SimHost + ClientHost: the real client logic (NO Godot types)
ClientCore/Scene.fs          IClientScene + FixtureSelfCheckScene (follow-up 1)
ClientCore/NodeLogic.fs      PatrolMarkerLogic + [<GodotExport>]/[<GodotSignal>] markers (follow-up 2)
ClientCore/FSharpNode.fs     the F# Node2D subclass - the question-1 experiment (Godot types)
src/FSharpSceneHost.cs       one generic C# host for the whole client (follow-up 1)
src/GeneratedStyleNode.cs    hand-written "what Myriad would emit" forwarder (follow-up 2)
scenes/SceneHost.tscn        uses FSharpSceneHost, names the F# scene by [Export]
nuget.config                 additive offline Godot feed (GodotSharp 4.7.2)
```

## Run

Godot loads the **Debug** assembly for a `--path` game run, so build Debug
first. One editor import pass is needed before the first headless run.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/_scratch/godot-fsharp-boundary
"$GODOT" --editor --headless --quit --path .          # one-time import
dotnet build GodotFSharpBoundary.csproj -c Debug

# fixture hash cross-check (host logic in F#, driven from Godot)
"$GODOT" --headless --path . -- --selfcheck --expect 0x838D3AE7DBFB735D   # MATCH, exit 0
"$GODOT" --headless --path . -- --selfcheck --expect 0xDEADBEEFDEADBEEF   # MISMATCH, exit 1

# question-1 experiment: does Godot drive an F# node?
"$GODOT" --headless --path . -- --probe

# question-5: C#->F# exception stack trace
"$GODOT" --headless --path . -- --trace-test

# follow-up 1: one generic C# host, F# scene resolved by [Export] name, zero per-scene C#
"$GODOT" --headless --path . --main-scene res://scenes/SceneHost.tscn   # MATCH 0x838D3AE7DBFB735D

# follow-up 2: F# owns members + [<GodotExport>]/[<GodotSignal>]; C# forwarder (as Myriad would emit)
"$GODOT" --headless --path . -- --forward-test   # [Export]s in property list, signal registered, round-trip

# windowed still with the F#-owned overlay
"$GODOT" --path . -- --screenshot <abs-path>.png
```

## Pinned versions

Godot `4.7.2.stable.mono.official.ed1daf0bf`; .NET SDK `10.0.303`; `net10.0`;
`Godot.NET.Sdk` / `GodotSharp` `4.7.2`; `FSharp.Core` `10.1.303`.
