// TASK-007 disposable proof. Not in any .slnx.
//
// The MINIMUM C# shim per scene entry point (TASK-007 question 2). It:
//   - is the scene root, so [Export] and editor "attach script" work here;
//   - holds nothing but [Export] refs and a reference to the F# ClientHost;
//   - forwards lifecycle callbacks (_Process / _UnhandledInput / _Draw) to F#;
//   - contains NO client logic - no scheduling, no command building, no
//     view-model maths, no overlay text. All of that is in ClientCore (F#).
//
// It also, on `--probe`, parents an F# FSharpHostNode to test whether Godot
// drives an F# node's own lifecycle (question 1).

using System;
using System.Collections.Generic;
using System.Globalization;
using CwClientCore;
using Godot;

public partial class MainShim : Node2D
{
    // An ordinary C#-side [Export]: the source generator runs over THIS file
    // (it is in the Godot.NET.Sdk project), so this works normally.
    [Export] public string Caption { get; set; } = "godot-fsharp-boundary proof";

    private ClientHost _host;

    // Godot only honours SceneTree.Quit(code) once the main loop iterates, so
    // headless exits are deferred one _Process frame (same wrinkle the TASK-004
    // spike recorded).
    private int? _pendingExit;
    private bool _probe;
    private int _probeFrames;
    private FSharpHostNode _fnode;
    private string _screenshotPath;
    private int _shotFrames;
    private Label _hud;

    public override void _Ready()
    {
        var args = new List<string>(OS.GetCmdlineUserArgs());

        if (args.Contains("--selfcheck"))
        {
            ulong? expect = null;
            int i = args.IndexOf("--expect");
            if (i >= 0 && i + 1 < args.Count && TryHex(args[i + 1], out ulong h))
                expect = h;
            // One call into F#. No FSharpOption, no module-function access.
            _pendingExit = ClientHost.SelfCheck(expect);
            return;
        }

        if (args.Contains("--forward-test"))
        {
            // Simulate what a Myriad-generated C# forwarder + Godot's own
            // generator produce: F# owns the members, C# forwards, Godot sees
            // real [Export] / [Signal].
            var n = new GeneratedStyleNode();
            n.Waypoints = 7;                       // as if set from the inspector
            n.Label = "north-ridge";
            AddChild(n);                           // -> _Ready -> forwards to F#
            var names = new List<string>();
            foreach (Godot.Collections.Dictionary d in n.GetPropertyList())
                names.Add(d["name"].AsString());
            GD.Print($"[forward-test] Waypoints in property list: {names.Contains("Waypoints")}");
            GD.Print($"[forward-test] Label in property list:     {names.Contains("Label")}");
            GD.Print($"[forward-test] HasSignal(PatrolCompleted):  {n.HasSignal("PatrolCompleted")}");
            n.Connect("PatrolCompleted", Callable.From((int lap) => GD.Print($"[forward-test] received PatrolCompleted({lap})")));
            n.EmitSignal("PatrolCompleted", 3);
            GD.Print($"[forward-test] round-trip: set Waypoints=7 via property -> F# logic reads {n.Waypoints}");
            _pendingExit = 0;
            return;
        }

        if (args.Contains("--trace-test"))
        {
            var probe = ClientHost.Create();
            try
            {
                probe.ForceFSharpFault();
            }
            catch (Exception ex)
            {
                GD.Print("[trace-test] exception caught in C# shim, ToString():");
                GD.Print(ex.ToString());
            }
            _pendingExit = 0;
            return;
        }

        _host = ClientHost.Create();
        GD.Print($"[shim] ClientHost created; tick {_host.Sim.Tick}, hash {_host.Sim.StateHashHex}");

        int si = args.IndexOf("--screenshot");
        if (si >= 0 && si + 1 < args.Count)
        {
            _screenshotPath = args[si + 1];
            _host.SimHz = 6.0;                 // slow so the still shows a mid-move agent
            _host.Sim.QueueMove(3, 20, 14);    // drive the fixture
        }

        var layer = new CanvasLayer();
        AddChild(layer);
        _hud = new Label
        {
            Position = new Vector2(12, 8),
            LabelSettings = new LabelSettings
            {
                FontSize = 14,
                FontColor = Colors.White,
                OutlineSize = 3,
                OutlineColor = new Color(0, 0, 0, 0.85f),
            },
        };
        layer.AddChild(_hud);

        if (args.Contains("--probe"))
        {
            _probe = true;
            var fnode = new FSharpHostNode
            {
                Name = "FSharpProbe",
                Caption = "assigned-from-C#",
                ProbeMode = true,
            };
            AddChild(fnode);
            _fnode = fnode;
            GD.Print($"[shim] added F# FSharpHostNode child; IsInsideTree={fnode.IsInsideTree()}");
            GD.Print($"[shim] fnode.HasMethod(\"_process\")={fnode.HasMethod("_process")}  GetScript.Obj={fnode.GetScript().Obj ?? "null"}");
        }
    }

    public override void _Process(double delta)
    {
        if (_pendingExit is { } code)
        {
            GetTree().Quit(code);
            return;
        }
        if (_host == null) return;
        _host.Update(delta);
        QueueRedraw();
        if (_hud != null) _hud.Text = _host.OverlayText + "\n\n(C# shim: _Process/_UnhandledInput/_Draw forwarders only; all logic in F#)";

        if (_screenshotPath != null && ++_shotFrames >= 120)
        {
            var img = GetViewport().GetTexture().GetImage();
            Error err = img.SavePng(_screenshotPath);
            GD.Print(err == Error.Ok
                ? $"[shim] screenshot written: {_screenshotPath} (tick {_host.Sim.Tick}, hash {_host.Sim.StateHashHex})"
                : $"[shim] screenshot FAILED: {err}");
            _pendingExit = err == Error.Ok ? 0 : 1;
            return;
        }

        if (_probe && ++_probeFrames == 6)
        {
            // By now Godot has run 6 _Process frames on the shim. If it were
            // driving the F# node, [fsharp-node] _EnterTree/_Ready/_Process
            // lines would already be above. They are not: HasMethod("_process")
            // was False. Now prove the override BODIES work under a direct
            // C# virtual call, and that runtime signals work.
            GD.Print("[shim] direct C# call fnode._Ready():");
            _fnode._Ready();
            _fnode.Connect("fsharp_pinged", Callable.From(() => GD.Print("[shim] received fsharp_pinged from F# node")));
            _fnode.EmitSignal("fsharp_pinged");
            GetTree().Quit(0);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_host == null) return;
        if (@event is InputEventMouseButton { Pressed: true } mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                // Crude screen->cell; the real projection is view detail, still F#.
                int cx = (int)Math.Round((mb.Position.X - 40f) / 12f);
                int cy = (int)Math.Round((mb.Position.Y - 40f) / 12f);
                _host.HandleClick(cx, cy);
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                _host.Deselect();
            }
        }
    }

    public override void _Draw()
    {
        if (_host == null) return;
        // C# does the CanvasItem draw calls; F# supplies the view model.
        foreach (AgentView a in _host.Sim.AgentViews)
        {
            var p = new Vector2(a.X * 12f, a.Y * 12f) + new Vector2(40f, 40f);
            DrawCircle(p, 6f, a.Friendly ? Colors.SkyBlue : Colors.Salmon);
            if (a.HasDestination)
                DrawLine(p, new Vector2(a.DestX * 12f, a.DestY * 12f) + new Vector2(40f, 40f),
                    new Color(1f, 1f, 1f, 0.35f), 1f);
        }
    }

    private static bool TryHex(string s, out ulong value)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2);
        return ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }
}
