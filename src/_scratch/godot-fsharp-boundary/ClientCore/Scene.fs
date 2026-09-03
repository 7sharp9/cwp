// TASK-007 follow-up: can the per-scene C# shim collapse to ONE generic C#
// class for the whole client? This is the contract that class forwards to.
// Every scene entry point is an F# type implementing IClientScene; the C#
// FSharpSceneHost (one file, written once) resolves it by name from an
// [Export] and forwards lifecycle calls. No per-scene C#.

namespace CwClientCore

open Godot

/// One F# scene. The host Node2D is passed in so F# can add child nodes, read
/// the tree, and issue immediate-mode draw calls - all client concerns, F# is
/// allowed to touch Godot here.
type IClientScene =
    abstract member Ready: host: Node2D -> unit
    abstract member Process: delta: float -> unit
    abstract member UnhandledInput: e: InputEvent -> unit
    abstract member Draw: host: Node2D -> unit
    abstract member ExitTree: unit -> unit

/// A scene that runs the shared-fixture self-check and quits. Proves the
/// generic host drives an F# scene and reproduces 0x838D3AE7DBFB735D.
type FixtureSelfCheckScene() =
    let host = ClientHost.Create()
    let mutable started = false
    let mutable pendingExit : int option = None
    let mutable ticked = 0L
    let mutable hostNode : Node2D = null

    interface IClientScene with
        member _.Ready(node: Node2D) =
            hostNode <- node
            GD.Print("[fixture-scene] Ready (F# scene, via generic C# host)")
            host.Sim.QueueMove(3, 20, 14) |> ignore

        member _.Process(delta: float) =
            match pendingExit with
            | Some code -> hostNode.GetTree().Quit(code)
            | None ->
                if not started then
                    GD.Print(sprintf "tick=0 hash=%s" host.Sim.StateHashHex)
                    started <- true
                // one authoritative tick per frame, no accumulator, for a clean stream
                let info = host.Sim.Step()
                ticked <- ticked + 1L
                GD.Print(sprintf "tick=%d hash=%s" info.Tick info.HashHex)
                if ticked >= 40L then
                    GD.Print(sprintf "final tick=%d hash=%s draws=%d" host.Sim.Tick host.Sim.StateHashHex host.Sim.RandomDraws)
                    let ok = host.Sim.StateHash = 0x838D3AE7DBFB735DUL
                    GD.Print(if ok then "MATCH 0x838D3AE7DBFB735D" else sprintf "MISMATCH got %s" host.Sim.StateHashHex)
                    pendingExit <- Some(if ok then 0 else 1)

        member _.UnhandledInput(_e: InputEvent) = ()
        member _.Draw(_host: Node2D) = ()
        member _.ExitTree() = GD.Print("[fixture-scene] ExitTree")
