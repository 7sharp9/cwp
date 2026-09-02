// Host root. Owns: content load + validation, host-side fixed-step scheduling
// (independent of render rate), input -> typed move command, isometric
// rendering with view-only interpolation, and the tick/hash overlay.
//
// It catches exactly one thing: ContentException at load time, to show an
// actionable failure screen. Simulation.step exceptions are never caught.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommandoWar.Client.Godot.Content;
using CommandoWar.Client.Godot.Sim;
using Godot;

namespace CommandoWar.Client.Godot;

public partial class MainNode : Node2D
{
    [Export] public PackedScene ContentScene { get; set; }
    [Export] public PackedScene InvalidContentScene { get; set; }

    // Isometric projection.
    private const float TileW = 22f;
    private const float TileH = 11f;
    private static readonly Vector2 Origin = new(640f, 60f);

    // Host-side authoritative-step scheduling. NOT frame delta: the sim only
    // ever receives an integer tick from SimFacade.Step().
    private double _simHz = 20.0;
    private double _simAccum;
    private const int MaxCatchUpStepsPerFrame = 5;
    private bool _paused;

    private SimFacade _sim;
    private ScenarioDto _scenario;
    private IReadOnlyList<AgentView> _prevAgents = Array.Empty<AgentView>();
    private double _alpha;

    // Measured rates, refreshed once per wall-clock second (display only).
    private int _renderFrames, _simSteps;
    private double _rateWindow;
    private int _renderFps, _simTps;

    private int _selectedAgent = -1;
    private (int x, int y)? _lastCommandedCell;

    private string[] _loadErrors = Array.Empty<string>();
    private bool _loadFailed;

    // Launch modes.
    private bool _selfCheck, _screenshotMode;
    private string _screenshotPath;
    private ulong? _expectHash;

    private Label _hud;

    public override void _Ready()
    {
        ParseCommandLine();

        PackedScene scene = _loadRequestedInvalid && InvalidContentScene != null
            ? InvalidContentScene
            : ContentScene;

        if (scene == null)
        {
            FailLoad("MainNode has no ContentScene assigned");
        }
        else
        {
            var greybox = scene.Instantiate<GreyboxScene>();
            RawContent raw = greybox.Read();
            greybox.QueueFree();

            try
            {
                _scenario = ContentValidator.Validate(raw);
            }
            catch (ContentException ex)
            {
                FailLoad(ex.Errors.ToArray());
            }

            if (!_loadFailed)
                _sim = SimFacade.Create(_scenario);
        }

        BuildHud();

        if (_loadFailed)
        {
            GD.PrintErr("CONTENT LOAD FAILED:\n  - " + string.Join("\n  - ", _loadErrors));
            // Headless callers get a non-zero exit, but Quit() only honours the
            // exit code once the main loop is iterating, so defer it to _Process.
            if (_selfCheck || _screenshotMode)
                _headlessExit = 2;
            return;
        }

        _prevAgents = _sim.Agents;
        GD.Print($"[spike] loaded {_scenario.Width}x{_scenario.Height}, seed {_scenario.Seed}, " +
                 $"{_sim.Agents.Count} agent(s), initial hash {_sim.StateHashHex}");
    }

    // --- fixed-step scheduling ------------------------------------------------

    private int? _headlessExit;

    public override void _Process(double delta)
    {
        if (_headlessExit is { } code)
        {
            GetTree().Quit(code);
            return;
        }

        if (_loadFailed || _sim == null)
            return;

        if (_selfCheck)
        {
            _headlessExit = RunSelfCheck();
            return;
        }

        _renderFrames++;
        _rateWindow += delta;
        if (_rateWindow >= 1.0)
        {
            _renderFps = _renderFrames;
            _simTps = _simSteps;
            _renderFrames = _simSteps = 0;
            _rateWindow -= 1.0;
        }

        if (_screenshotMode && _sim.Tick == 0)
        {
            _sim.QueueMove(3, 20, 14); // drive the fixture so the still is meaningful
            _simHz = 6.0;              // slow enough that the still shows a mid-move agent
        }

        if (!_paused)
        {
            double simStep = 1.0 / _simHz;
            _simAccum += delta;
            int steps = 0;
            while (_simAccum >= simStep && steps < MaxCatchUpStepsPerFrame)
            {
                AdvanceOneTick();
                _simAccum -= simStep;
                steps++;
            }
            _alpha = Mathf.Clamp((float)(_simAccum / simStep), 0f, 1f);
        }

        QueueRedraw();
        UpdateHud();

        if (_screenshotMode && ++_screenshotFrameCount >= 140)
            CaptureScreenshot();
    }

    private int _screenshotFrameCount;

    private void CaptureScreenshot()
    {
        var img = GetViewport().GetTexture().GetImage();
        Error err = img.SavePng(_screenshotPath);
        GD.Print(err == Error.Ok
            ? $"[spike] screenshot written: {_screenshotPath} (tick {_sim.Tick}, hash {_sim.StateHashHex})"
            : $"[spike] screenshot FAILED: {err}");
        _headlessExit = err == Error.Ok ? 0 : 1;
    }

    private void AdvanceOneTick()
    {
        _prevAgents = _sim.Agents;
        TickInfo info = _sim.Step(); // exceptions propagate on purpose
        _simSteps++;
        if (info.AcceptedThisTick == 0)
            _lastCommandedCell = null;
    }

    // --- self-check (headless hash cross-verification) -----------------------

    private int RunSelfCheck()
    {
        GD.Print($"# godot-host self-check: {_scenario.Width}x{_scenario.Height}, seed {_scenario.Seed}, " +
                 $"{_scenario.TickCount} ticks");
        GD.Print($"tick=0 hash={_sim.StateHashHex} format={_sim.StateHashFormat}");

        _sim.QueueMove(3, 20, 14); // the shared-fixture command, issued at tick 1

        for (int i = 0; i < _scenario.TickCount; i++)
        {
            TickInfo info = _sim.Step();
            GD.Print($"tick={info.Tick} hash=0x{info.Hash:X16} format={info.HashFormat}");
        }

        GD.Print($"final tick={_sim.Tick} hash={_sim.StateHashHex} draws={_sim.RandomDraws}");
        GD.Print("accepted-command-log:");
        foreach (string line in _sim.AcceptedCommandLog)
            GD.Print($"  {line}");

        int exit = 0;
        if (_expectHash is { } want)
        {
            bool ok = want == _sim.StateHash;
            GD.Print(ok
                ? $"MATCH expected final hash 0x{want:X16}"
                : $"MISMATCH expected 0x{want:X16}, got {_sim.StateHashHex}");
            exit = ok ? 0 : 1;
        }

        return exit;
    }

    // --- input -> typed command --------------------------------------------

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_loadFailed || _sim == null || _selfCheck || _screenshotMode)
            return;

        if (@event is InputEventMouseButton { Pressed: true } mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
                HandleClick(ScreenToCell(mb.Position));
            else if (mb.ButtonIndex == MouseButton.Right)
                _selectedAgent = -1;
        }
        else if (@event is InputEventKey { Pressed: true, Echo: false } k)
        {
            switch (k.Keycode)
            {
                case Key.Space: _paused = !_paused; break;
                case Key.Key1: _simHz = Math.Max(1.0, _simHz - 5.0); break;
                case Key.Key2: _simHz = Math.Min(120.0, _simHz + 5.0); break;
                case Key.S: DumpCommandLog(); break;
            }
        }
    }

    private void HandleClick(Vector2I cell)
    {
        AgentView? hit = _sim.Agents
            .Where(a => a.Friendly && a.X == cell.X && a.Y == cell.Y)
            .Select(a => (AgentView?)a)
            .FirstOrDefault();

        if (hit is { } agent)
        {
            _selectedAgent = agent.Id;
            return;
        }

        if (_selectedAgent >= 0)
        {
            _sim.QueueMove(_selectedAgent, cell.X, cell.Y);
            _lastCommandedCell = (cell.X, cell.Y);
            GD.Print($"[spike] queued MoveTo({cell.X},{cell.Y}) for agent {_selectedAgent}");
        }
    }

    private void DumpCommandLog()
    {
        string path = "user://godot-run.cwlog";
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        f.StoreLine("# CommandoWar Godot spike run - accepted commands");
        f.StoreLine("version 1");
        foreach (string line in _sim.AcceptedCommandLog)
            f.StoreLine(line);
        GD.Print($"[spike] wrote {ProjectSettings.GlobalizePath(path)}");
    }

    // --- isometric rendering (view only) ----------------------------------

    private Vector2 CellToScreen(float cx, float cy) =>
        Origin + new Vector2((cx - cy) * (TileW * 0.5f), (cx + cy) * (TileH * 0.5f));

    private Vector2I ScreenToCell(Vector2 screen)
    {
        Vector2 p = screen - Origin;
        float a = p.X / (TileW * 0.5f); // cx - cy
        float b = p.Y / (TileH * 0.5f); // cx + cy
        return new Vector2I(Mathf.RoundToInt((a + b) * 0.5f), Mathf.RoundToInt((b - a) * 0.5f));
    }

    public override void _Draw()
    {
        if (_loadFailed || _scenario == null)
            return;

        for (int y = 0; y < _scenario.Height; y++)
        for (int x = 0; x < _scenario.Width; x++)
        {
            bool wall = !_scenario.IsPassable(x, y);
            DrawDiamond(x, y, wall ? new Color(0.20f, 0.22f, 0.27f) : new Color(0.42f, 0.45f, 0.40f));
        }

        foreach (MarkerDto area in _scenario.ObjectiveAreas)
            DrawDiamond(area.X, area.Y, new Color(0.85f, 0.65f, 0.15f, 0.55f));

        foreach (MarkerDto s in _scenario.EnemySpawns)
            DrawDiamond(s.X, s.Y, new Color(0.6f, 0.2f, 0.2f, 0.4f));

        var prev = _prevAgents.ToDictionary(a => a.Id, a => a);
        foreach (AgentView a in _sim?.Agents ?? Array.Empty<AgentView>())
        {
            Vector2 from = prev.TryGetValue(a.Id, out var p) ? CellToScreen(p.X, p.Y) : CellToScreen(a.X, a.Y);
            Vector2 to = CellToScreen(a.X, a.Y);
            Vector2 pos = from.Lerp(to, (float)_alpha) - new Vector2(0, TileH);

            Color c = a.Friendly ? new Color(0.35f, 0.75f, 1f) : new Color(1f, 0.4f, 0.35f);
            DrawCircle(pos, 7f, c);
            DrawArc(pos, 7f, 0, Mathf.Tau, 20, new Color(0, 0, 0), 1.5f);
            if (a.Id == _selectedAgent)
                DrawArc(pos, 11f, 0, Mathf.Tau, 24, new Color(1f, 1f, 0.2f), 2f);

            if (a.DestX is { } dx && a.DestY is { } dy)
                DrawLine(pos, CellToScreen(dx, dy) - new Vector2(0, TileH), new Color(1f, 1f, 1f, 0.35f), 1f);
        }
    }

    private void DrawDiamond(float cx, float cy, Color color)
    {
        Vector2 c = CellToScreen(cx, cy);
        Span<Vector2> pts =
        [
            c + new Vector2(0, -TileH * 0.5f),
            c + new Vector2(TileW * 0.5f, 0),
            c + new Vector2(0, TileH * 0.5f),
            c + new Vector2(-TileW * 0.5f, 0),
        ];
        var arr = pts.ToArray();
        DrawColoredPolygon(arr, color);
        DrawPolyline([.. arr, arr[0]], new Color(0, 0, 0, 0.25f), 1f);
    }

    // --- overlay ---------------------------------------------------------

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        _hud = new Label
        {
            Position = new Vector2(12, 8),
            Theme = null,
            LabelSettings = new LabelSettings { FontSize = 14, FontColor = Colors.White,
                OutlineSize = 3, OutlineColor = new Color(0, 0, 0, 0.85f) },
        };
        layer.AddChild(_hud);

        if (_loadFailed)
        {
            var panel = new ColorRect
            {
                Color = new Color(0.5f, 0.05f, 0.05f, 0.92f),
                AnchorRight = 1, AnchorBottom = 1,
            };
            layer.AddChild(panel);
            var msg = new Label
            {
                Position = new Vector2(40, 40),
                Text = "CONTENT LOAD FAILED\n\n- " + string.Join("\n- ", _loadErrors) +
                       "\n\nThe simulation was not started. Fix the authored scene and relaunch.",
                LabelSettings = new LabelSettings { FontSize = 18, FontColor = Colors.White },
            };
            layer.AddChild(msg);
        }
    }

    private void UpdateHud() => _hud.Text = HudText();

    private string HudText()
    {
        if (_sim == null)
            return "no simulation";

        string sel = _selectedAgent >= 0 ? _selectedAgent.ToString(CultureInfo.InvariantCulture) : "none";
        string cmd = _lastCommandedCell is { } cc ? $"MoveTo({cc.x},{cc.y})" : "-";
        return
            $"tick            {_sim.Tick}\n" +
            $"state hash      {_sim.StateHashHex}  (format {_sim.StateHashFormat})\n" +
            $"random draws    {_sim.RandomDraws}\n" +
            $"sim rate        {_simHz:0} Hz target   {_simTps} steps/s measured{(_paused ? "   [PAUSED]" : "")}\n" +
            $"render rate     {_renderFps} fps   (Engine {Engine.GetFramesPerSecond():0})\n" +
            $"interp alpha    {_alpha:0.00}\n" +
            $"selected agent  {sel}     last command  {cmd}\n" +
            $"\n" +
            $"L-click agent = select   L-click cell = move selected   R-click = deselect\n" +
            $"[1]/[2] sim Hz   [Space] pause   [S] dump command log";
    }

    // --- helpers ---------------------------------------------------------

    private bool _loadRequestedInvalid;

    private void ParseCommandLine()
    {
        string[] args = OS.GetCmdlineUserArgs();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--invalid": _loadRequestedInvalid = true; break;
                case "--selfcheck": _selfCheck = true; break;
                case "--screenshot":
                    _screenshotMode = true;
                    if (i + 1 < args.Length) _screenshotPath = args[++i];
                    break;
                case "--expect":
                    if (i + 1 < args.Length &&
                        TryParseHex(args[++i], out ulong h)) _expectHash = h;
                    break;
            }
        }
    }

    private static bool TryParseHex(string s, out ulong value)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private void FailLoad(params string[] errors)
    {
        _loadFailed = true;
        _loadErrors = errors;
    }
}
