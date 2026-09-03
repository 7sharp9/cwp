// TASK-007 disposable proof. Not in any .slnx.
//
// TASK-007 question 1: an F# type deriving from a Godot node. This file is the
// ONLY place in the proof that touches Godot types from F#. It is compiled into
// the ClientCore F# assembly (a plain Microsoft.NET.Sdk library referencing the
// GodotSharp package), NOT the Godot SDK project - so no Godot source
// generator runs over it.
//
// What we are testing: instantiated from C# (`new FSharpHostNode()`) and
// AddChild-ed, does Godot 4.7.2 drive its lifecycle callbacks (_Ready,
// _Process, _UnhandledInput, _Draw)? Are [<Export>] fields visible? Do signals
// work? The probe prints what actually happens; the ledger records it.

namespace CwClientCore

open Godot

/// An F# Node2D subclass. No [ScriptPath], no generated GetGodotPropertyList /
/// InvokeGodotClassMethod / HasGodotClassMethod - F# has no equivalent to
/// Godot.SourceGenerators.
type FSharpHostNode() =
    inherit Node2D()

    let host = ClientHost.Create()
    let mutable readyCalls = 0
    let mutable processCalls = 0
    let mutable inputCalls = 0
    let mutable drawCalls = 0
    let mutable probeMode = false
    let mutable framesLeft = 4

    // Isometric projection (view only).
    let tileW, tileH = 22.0f, 11.0f
    let origin = Vector2(520f, 80f)
    let cellToScreen (cx: float32) (cy: float32) =
        origin + Vector2((cx - cy) * (tileW * 0.5f), (cx + cy) * (tileH * 0.5f))

    /// A [<Export>] on an F# member. Without the source generator Godot builds
    /// no property-list entry for it; the probe reports GetPropertyList().
    [<Export>]
    member val Caption = "unset" with get, set

    member _.Host = host

    /// Set by the C# shim before the node enters the tree when launched with
    /// `--probe`: quit after a few frames once lifecycle is demonstrated.
    member _.ProbeMode with get () = probeMode and set v = probeMode <- v

    override this._EnterTree() =
        GD.Print("[fsharp-node] _EnterTree called")

    override this._Notification(what: int) =
        GD.Print($"[fsharp-node] _Notification({what})")

    override this._Ready() =
        readyCalls <- readyCalls + 1
        GD.Print($"[fsharp-node] _Ready #{readyCalls}  name={this.Name}  Caption={this.Caption}")
        this.AddUserSignal("fsharp_pinged")
        let names =
            this.GetPropertyList()
            |> Seq.map (fun (d: Godot.Collections.Dictionary) -> d.["name"].AsString())
            |> Seq.filter (fun n -> n.ToLowerInvariant().Contains("caption"))
            |> List.ofSeq
        let captionEntry = if List.isEmpty names then "ABSENT" else String.concat "," names
        GD.Print($"[fsharp-node] GetPropertyList() caption entry: {captionEntry}")

    override this._Process(delta: float) =
        processCalls <- processCalls + 1
        if processCalls = 1 then GD.Print("[fsharp-node] _Process is being driven by Godot")
        host.Update(delta) |> ignore
        this.QueueRedraw()
        if probeMode then
            framesLeft <- framesLeft - 1
            if framesLeft = 0 then
                GD.Print($"[fsharp-node] probe summary: ready={readyCalls} process={processCalls} input={inputCalls} draw={drawCalls}")
                this.EmitSignal("fsharp_pinged") |> ignore
                this.GetTree().Quit(0)

    override this._UnhandledInput(e: InputEvent) =
        inputCalls <- inputCalls + 1
        GD.Print($"[fsharp-node] _UnhandledInput #{inputCalls}: {e.GetType().Name}")

    override this._Draw() =
        drawCalls <- drawCalls + 1
        for a in host.Sim.AgentViews do
            let p = cellToScreen (float32 a.X) (float32 a.Y)
            let c = if a.Friendly then Color(0.35f, 0.75f, 1f) else Color(1f, 0.4f, 0.35f)
            this.DrawCircle(p, 6f, c)
