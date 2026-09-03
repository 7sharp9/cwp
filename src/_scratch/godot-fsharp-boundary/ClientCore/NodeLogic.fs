// TASK-007 follow-up 2 (Dave's question): can F# be the single source of truth
// for a node - its members, its [Export]/[Signal]/[GlobalClass] intent - with
// Myriad emitting ONLY a mechanical C# forwarder, and Godot's own generator
// doing all the unstable-ABI work over that forwarder?
//
// This file is the F# side: the real type. The attributes here are plain
// markers describing Godot intent; a Myriad plugin would read them and emit
// the C# partial in src/GeneratedStyleNode.cs. For this proof that C# file is
// HAND-WRITTEN to be exactly what such a plugin would emit, so we can test
// whether Godot's generator + inspector round-trip actually works on a
// forwarder.

namespace CwClientCore

open Godot

/// Marker a Myriad plugin would read. (Real plugin would also carry hint /
/// group / range payloads.)
type GodotExportAttribute() = inherit System.Attribute()
type GodotSignalAttribute(name: string) =
    inherit System.Attribute()
    member _.Name = name

/// The node's real logic and state, in F#. The C# forwarder composes this and
/// hands it the host Node2D for tree ops and signal emits.
type PatrolMarkerLogic(host: Node2D) =
    let mutable waypoints = 3
    let mutable label = "unset"
    let mutable readyCount = 0

    /// Would become `[Export] public int Waypoints` on the C# forwarder.
    [<GodotExport>]
    member _.Waypoints with get () = waypoints and set v = waypoints <- v

    /// Would become `[Export] public string Label` on the C# forwarder.
    [<GodotExport>]
    member _.Label with get () = label and set v = label <- v

    /// Would become `[Signal] delegate void PatrolCompletedEventHandler(int)`.
    [<GodotSignal("patrol_completed")>]
    member _.RaisePatrolCompleted(lap: int) =
        host.EmitSignal("patrol_completed", lap) |> ignore

    member _.OnReady() =
        readyCount <- readyCount + 1
        GD.Print($"[patrol-logic] OnReady #{readyCount}  Waypoints={waypoints}  Label={label}")

    member _.OnProcess(_delta: float) = ()
