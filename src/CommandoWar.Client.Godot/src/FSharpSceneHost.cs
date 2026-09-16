// Generic C# host over an F# client-core scene (ADR-0004 form 1: one host
// for the whole client, ~35 lines of real logic, no per-scene C#). Every
// production `.tscn` entry point uses this as its root and sets
// [Export] SceneType to the full name of an F# CwClientCore.IClientScene
// implementation. The host resolves the type by reflection, forwards
// lifecycle calls, and does the isometric cell<->screen projection ADR-0004
// explicitly allows in the C# shim -- nothing else.
//
// TASK-039 (backlog B-027) is the first scene built on this host
// (CwClientCore.DemoRenderScene); AppraisalDemoScene.cs and MainNode.cs
// predate it and are NOT ported here (the former a documented scoped
// deviation, the latter the disposable TASK-004 spike).

using System;
using System.Linq;
using CwClientCore;
using Godot;

namespace CommandoWar.Client.Godot;

public partial class FSharpSceneHost : Node2D
{
    [Export] public string SceneType { get; set; }

    // Isometric projection -- the MainNode.cs disposable-spike precedent,
    // scaled up (44x22, vs. the spike's 22x11) for legibility at DemoScenario's
    // 12x8 grid size.
    private const float TileW = 44f;
    private const float TileH = 22f;
    private static readonly Vector2 Origin = new(450f, 60f);

    private IClientScene _scene;
    private Label _hud;

    private bool _selfCheck, _screenshotMode;
    private string _screenshotPath;
    private int? _headlessExit;
    private int _screenshotFrameCount;

    public override void _Ready()
    {
        ParseCommandLine();

        if (string.IsNullOrEmpty(SceneType))
        {
            GD.PrintErr("FSharpSceneHost: no SceneType set");
            _headlessExit = 2;
            return;
        }

        Type sceneClrType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(SceneType))
            .FirstOrDefault(t => t != null);

        if (sceneClrType == null)
        {
            GD.PrintErr($"FSharpSceneHost: scene type '{SceneType}' not found in any loaded assembly");
            _headlessExit = 2;
            return;
        }

        _scene = (IClientScene)Activator.CreateInstance(sceneClrType);
        _scene.Ready();

        BuildHud();

        if (_selfCheck)
            _headlessExit = RunSelfCheck();
    }

    public override void _Process(double delta)
    {
        if (_headlessExit is { } code)
        {
            GetTree().Quit(code);
            return;
        }

        if (_scene == null || _selfCheck)
            return;

        _scene.Update(delta);
        QueueRedraw();
        if (_hud != null)
            _hud.Text = _scene.HudText();

        // Mid-run by default (not "settled at rest"): meaningful only if the
        // scene is still stepping at this point, DemoRenderScene's precedent.
        if (_screenshotMode && ++_screenshotFrameCount >= 45)
            CaptureScreenshot();
    }

    public override void _ExitTree() => _scene?.Dispose();

    // --- self-check (headless determinism evidence) -------------------------
    //
    // Scene-specific for now (only one self-checkable scene exists): a
    // generic self-check hook (e.g. an optional interface a scene can also
    // implement) is a natural refinement once a second one needs it.

    private int RunSelfCheck()
    {
        if (SceneType != "CwClientCore.DemoRenderScene")
        {
            GD.PrintErr($"FSharpSceneHost: --selfcheck has no evidence path for '{SceneType}'");
            return 2;
        }

        GD.Print("# demo-render-scene self-check (DemoScenario, terrain-demo)");
        TickHash[] sequence = DemoDrive.runFullSequence();
        foreach (TickHash th in sequence)
            GD.Print($"tick={th.Tick} hash=0x{th.Hash:X16}");

        TickHash final = sequence[^1];
        const ulong expected = 0x11B06E6EDE0C52E3UL; // DemoScenario tick 20 (TASK-039)
        bool ok = final.Hash == expected;
        GD.Print(ok
            ? $"MATCH expected final hash 0x{expected:X16} at tick {final.Tick}"
            : $"MISMATCH expected 0x{expected:X16}, got 0x{final.Hash:X16}");
        return ok ? 0 : 1;
    }

    // --- rendering (view only): cell<->screen projection + Draw* calls -----

    private Vector2 CellToScreen(float cx, float cy) =>
        Origin + new Vector2((cx - cy) * (TileW * 0.5f), (cx + cy) * (TileH * 0.5f));

    public override void _Draw()
    {
        if (_scene == null)
            return;

        foreach (DrawItem item in _scene.DrawList())
        {
            Vector2 pos = CellToScreen(item.Cx, item.Cy);
            var color = new Color(item.R, item.G, item.B, item.A);

            if (item.Kind == 0)
            {
                DrawDiamond(pos, color);
            }
            else
            {
                Vector2 agentPos = pos - new Vector2(0, TileH * 0.5f);
                DrawCircle(agentPos, item.Radius, color);
                DrawArc(agentPos, item.Radius, 0, Mathf.Tau, 20, Colors.White, 1.5f);
            }
        }
    }

    private void DrawDiamond(Vector2 center, Color color)
    {
        Vector2[] pts =
        [
            center + new Vector2(0, -TileH * 0.5f),
            center + new Vector2(TileW * 0.5f, 0),
            center + new Vector2(0, TileH * 0.5f),
            center + new Vector2(-TileW * 0.5f, 0),
        ];
        DrawColoredPolygon(pts, color);
        DrawPolyline([.. pts, pts[0]], new Color(0, 0, 0, 0.25f), 1f);
    }

    // --- HUD / screenshot ---------------------------------------------------

    private void BuildHud()
    {
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
    }

    private void CaptureScreenshot()
    {
        Image img = GetViewport()?.GetTexture()?.GetImage();
        if (img == null)
        {
            GD.Print("[snapshot-demo] screenshot FAILED: no viewport image (run windowed, not --headless)");
            _headlessExit = 1;
            return;
        }

        Error err = img.SavePng(_screenshotPath);
        GD.Print(err == Error.Ok
            ? $"[snapshot-demo] screenshot written: {_screenshotPath}"
            : $"[snapshot-demo] screenshot FAILED: {err}");
        _headlessExit = err == Error.Ok ? 0 : 1;
    }

    // --- command line --------------------------------------------------------

    private void ParseCommandLine()
    {
        string[] args = OS.GetCmdlineUserArgs();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--selfcheck":
                    _selfCheck = true;
                    break;
                case "--screenshot":
                    _screenshotMode = true;
                    if (i + 1 < args.Length)
                        _screenshotPath = args[++i];
                    break;
            }
        }
    }
}
