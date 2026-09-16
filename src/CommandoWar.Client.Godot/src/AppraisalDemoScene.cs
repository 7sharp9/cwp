// TASK-029 appraisal-divergence demo scene (a read-only, corpus-scoped slice of
// backlog B-029; docs/07 section 9 criteria 2 and 11).
//
// This C# node is a thin renderer. All non-trivial logic - loading the
// committed exposed-approach corpus entry, running the DiagnosticFrame
// sequence, mapping each OrderDisposition to readable text, and flattening the
// frame into a C#-friendly view model - lives in the framework-neutral F#
// helper CommandoWar.Headless.AppraisalDemo. This node only: resolves the
// repo-relative content directory, calls the helper, holds the tick index,
// draws the selected FrameView, handles the slider / arrow keys, and draws the
// HUD.
//
// Putting the scene in C# is a SCOPED deviation from ADR-0004's "F#
// client-core" rule, justified for this disposable P3 decision-support demo and
// recorded in the TASK-029 task file and ledger. It is NOT a precedent for the
// P4 client tasks (B-026 / B-027 / B-029).

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CommandoWar.Headless;
using Godot;

namespace CommandoWar.Client.Godot;

public partial class AppraisalDemoScene : Node2D
{
    // Integer up-scale of DiagnosticRender.Svg's Scale = 16 px/cell, for a
    // legible window. Colours and glyphs below are the SVG renderer's, verbatim.
    private const int CellPx = 44;
    private static readonly Vector2 GridOrigin = new(48f, 48f);

    private static readonly Color FriendlyColor = new("#2b6cb0");
    private static readonly Color HostileColor = new("#c53030");
    private static readonly Color RouteColor = new("#dd6b20");
    private static readonly Color StartDiscColor = new("#2f855a");
    private static readonly Color ContactColor = new("#805ad5");
    private static readonly Color GridLineColor = new("#cccccc");
    private static readonly Color GridFillColor = new(0.93f, 0.93f, 0.93f);
    private static readonly Color AcceptedColor = new("#2f855a");
    private static readonly Color RefusedColor = new("#c53030");
    private static readonly Color UnableColor = new("#718096");

    private AppraisalDemo.FrameView[] _views = Array.Empty<AppraisalDemo.FrameView>();
    private int _index;

    private Label _hud;
    private Label _panel;
    private HSlider _slider;
    private Font _font;

    // Launch modes (mirrors the greybox spike's MainNode).
    private bool _selfCheck;
    private bool _screenshot;
    private string _screenshotPath;
    private ulong _expectHash = 0x5D5A30C0DF64AC93UL; // exposed-approach tick 1 (Canonical.FormatVersion 8, TASK-037)
    private int? _headlessExit;
    private int _screenshotFrames;

    public override void _Ready()
    {
        ParseCommandLine();

        string replaysDir = Path.GetFullPath(Path.Combine(
            ProjectSettings.GlobalizePath("res://"), "..", "..", "content", "replays"));

        // Fails loud inside the F# helper if the entry or its .cwlog is absent.
        _views = AppraisalDemo.loadFrameViews(replaysDir);
        _index = Math.Clamp(AppraisalDemo.DivergenceTick, 0, _views.Length - 1);

        _font = ThemeDB.GetFallbackFont();

        var layer = new CanvasLayer();
        AddChild(layer);

        LabelSettings text() => new()
        {
            FontSize = 15,
            FontColor = new Color("#111111"),
            OutlineSize = 3,
            OutlineColor = new Color(1, 1, 1, 0.85f),
        };

        _hud = new Label
        {
            Position = new Vector2(GridOrigin.X, GridOrigin.Y + 8 * CellPx + 16),
            LabelSettings = text(),
        };
        layer.AddChild(_hud);

        // The per-agent DecisionReason panel, to the right of the grid.
        _panel = new Label
        {
            Position = new Vector2(GridOrigin.X + 12 * CellPx + 28, GridOrigin.Y),
            LabelSettings = text(),
        };
        layer.AddChild(_panel);

        _slider = new HSlider
        {
            Position = new Vector2(GridOrigin.X, 12),
            CustomMinimumSize = new Vector2(12 * CellPx, 20),
            MinValue = 0,
            MaxValue = _views.Length - 1,
            Step = 1,
            Value = _index,
        };
        _slider.ValueChanged += OnSliderChanged;
        layer.AddChild(_slider);

        UpdateHud();

        GD.Print($"[appraisal-demo] loaded {_views.Length} frames from {replaysDir}");
    }

    private void OnSliderChanged(double value)
    {
        _index = Math.Clamp((int)Math.Round(value), 0, _views.Length - 1);
        UpdateHud();
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_selfCheck || _screenshot)
            return;

        if (@event is InputEventKey { Pressed: true, Echo: false } k)
        {
            int delta = k.Keycode switch
            {
                Key.Left => -1,
                Key.Right => 1,
                _ => 0,
            };
            if (delta != 0)
            {
                _index = Math.Clamp(_index + delta, 0, _views.Length - 1);
                _slider.SetValueNoSignal(_index);
                UpdateHud();
                QueueRedraw();
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_headlessExit is { } code)
        {
            GetTree().Quit(code);
            return;
        }

        if (_selfCheck)
        {
            _headlessExit = RunSelfCheck();
            return;
        }

        if (_screenshot && ++_screenshotFrames >= 8)
        {
            // Windowed run only (the greybox spike's precedent): a headless run
            // has no viewport texture to capture.
            Image img = GetViewport()?.GetTexture()?.GetImage();
            if (img == null)
            {
                GD.Print("[appraisal-demo] screenshot FAILED: no viewport image (run windowed, not --headless)");
                _headlessExit = 1;
                return;
            }

            Error err = img.SavePng(_screenshotPath);
            GD.Print(err == Error.Ok
                ? $"[appraisal-demo] screenshot written: {_screenshotPath}"
                : $"[appraisal-demo] screenshot FAILED: {err}");
            _headlessExit = err == Error.Ok ? 0 : 1;
        }
    }

    // --- headless smoke: tick-1 dispositions + hash vs the committed golden ---

    private int RunSelfCheck()
    {
        AppraisalDemo.FrameView f = _views[AppraisalDemo.DivergenceTick];
        GD.Print($"# appraisal-demo self-check: tick {f.Tick}");

        foreach (AppraisalDemo.AppraisalRow row in f.Appraisals.OrderBy(r => r.AgentId))
            GD.Print($"agent {row.AgentId}: {row.Text}");

        GD.Print($"hash {f.HashHex} (format {f.HashFormat})");

        bool hashOk = ParseHex(f.HashHex) == _expectHash;
        var byId = f.Appraisals.ToDictionary(r => r.AgentId, r => r.Text);
        bool textOk =
            f.Appraisals.Length == 2
            && byId.TryGetValue(0, out string a0) && a0 == "refused route-too-exposed threat-agent-2"
            && byId.TryGetValue(1, out string a1) && a1 == "accepted";

        if (hashOk && textOk)
        {
            GD.Print($"MATCH expected hash 0x{_expectHash:X16}, agent 0 Refused / agent 1 Accepted");
            return 0;
        }

        GD.Print($"MISMATCH hashOk={hashOk} textOk={textOk} (expected 0x{_expectHash:X16})");
        return 1;
    }

    // --- rendering (view only) ---------------------------------------------

    private Vector2 Center(int x, int y) =>
        GridOrigin + new Vector2((x + 0.5f) * CellPx, (y + 0.5f) * CellPx);

    private Rect2 CellRect(int x, int y) =>
        new(GridOrigin + new Vector2(x * CellPx, y * CellPx), new Vector2(CellPx, CellPx));

    public override void _Draw()
    {
        if (_views.Length == 0)
            return;

        AppraisalDemo.FrameView f = _views[_index];

        // Terrain grid (exposed-approach is all passable).
        for (int y = 0; y < f.Height; y++)
        for (int x = 0; x < f.Width; x++)
        {
            bool passable = f.Passable[y * f.Width + x];
            DrawRect(CellRect(x, y), passable ? GridFillColor : new Color(0.55f, 0.55f, 0.58f), filled: true);
            DrawRect(CellRect(x, y), GridLineColor, filled: false, width: 1f);
        }

        // OrderAppraisal: exposed route cells tinted (#c53030 @ 0.25).
        var exposedTint = RefusedColor;
        exposedTint.A = 0.25f;
        foreach (AppraisalDemo.AppraisalRow row in f.Appraisals)
            foreach (AppraisalDemo.CellXY c in row.ExposedCells)
                DrawRect(CellRect(c.X, c.Y), exposedTint, filled: true);

        // PlannedPath: polyline (#dd6b20), green start disc, goal rect.
        foreach (AppraisalDemo.RouteLine r in f.Routes)
        {
            if (r.Cells.Length >= 2)
            {
                Vector2[] pts = r.Cells.Select(c => Center(c.X, c.Y)).ToArray();
                DrawPolyline(pts, RouteColor, width: 2f);
            }
            DrawCircle(Center(r.FromX, r.FromY), CellPx * 0.24f, StartDiscColor);
            DrawRect(CellRect(r.TargetX, r.TargetY), RouteColor, filled: false, width: 3f);
        }

        // KnownContact: dashed #805ad5 ring + "?<id>" label.
        foreach (AppraisalDemo.ContactRing ring in f.Contacts)
        {
            DrawDashedRing(Center(ring.X, ring.Y), CellPx * 0.36f, ContactColor);
            DrawText(Center(ring.X, ring.Y) + new Vector2(CellPx * 0.10f, -CellPx * 0.18f),
                $"?{ring.Contact}", ContactColor, 13);
        }

        // Agents: circle by side, white stroke, dashed destination line.
        foreach (AppraisalDemo.AgentDot a in f.Agents)
        {
            Vector2 c = Center(a.X, a.Y);
            Color col = a.Friendly ? FriendlyColor : HostileColor;
            if (a.HasDestination)
                DrawDashedLine(c, Center(a.DestX, a.DestY), col, width: 1.5f, dash: 6f);
            DrawCircle(c, CellPx * 0.30f, col);
            DrawArc(c, CellPx * 0.30f, 0f, Mathf.Tau, 24, Colors.White, 1.5f);
        }

        // OrderAppraisal: A / R / U glyph at the agent cell, disposition colour.
        foreach (AppraisalDemo.AppraisalRow row in f.Appraisals)
        {
            (string glyph, Color col) = row.Tone switch
            {
                "accepted" => ("A", AcceptedColor),
                "refused" => ("R", RefusedColor),
                _ => ("U", UnableColor),
            };
            DrawText(CellRect(row.X, row.Y).Position + new Vector2(CellPx * 0.60f, CellPx * 0.34f),
                glyph, col, 20);
        }

        // Unhandled overlay fallback (empty for exposed-approach).
        if (f.UnhandledOverlays.Length > 0)
            DrawText(new Vector2(GridOrigin.X, GridOrigin.Y - 20),
                "unhandled overlays: " + string.Join(", ", f.UnhandledOverlays), UnableColor, 13);
    }

    private void DrawText(Vector2 pos, string text, Color color, int size)
    {
        if (_font != null)
            DrawString(_font, pos, text, HorizontalAlignment.Left, -1f, size, color);
    }

    private void DrawDashedRing(Vector2 center, float radius, Color color)
    {
        const int segments = 20;
        for (int i = 0; i < segments; i += 2)
        {
            float a0 = Mathf.Tau * i / segments;
            float a1 = Mathf.Tau * (i + 1) / segments;
            DrawArc(center, radius, a0, a1, 4, color, 2f);
        }
    }

    // --- HUD -------------------------------------------------------------

    private void UpdateHud()
    {
        if (_hud == null)
            return;

        AppraisalDemo.FrameView f = _views[_index];

        _hud.Text =
            $"tick {f.Tick} / {_views.Length - 1}   hash {f.HashHex}   (format {f.HashFormat})   draws {f.RandomDraws}\n" +
            "[drag the slider or press Left / Right to scrub ticks]";

        var sb = new StringBuilder();
        sb.AppendLine("exposed-approach  —  order appraisal");
        sb.AppendLine();

        if (f.Appraisals.Length == 0)
        {
            sb.AppendLine("(no order appraised at this tick)");
        }
        else
        {
            foreach (AppraisalDemo.AppraisalRow row in f.Appraisals.OrderBy(r => r.AgentId))
            {
                sb.AppendLine($"agent {row.AgentId} @ ({row.X},{row.Y})");
                sb.AppendLine($"  {row.Text}");
                sb.AppendLine();
            }
        }

        foreach (AppraisalDemo.ContactRing ring in f.Contacts)
            sb.AppendLine($"known contact {ring.Contact} @ ({ring.X},{ring.Y})  conf {ring.Confidence}");

        if (f.UnhandledOverlays.Length > 0)
            sb.AppendLine("unhandled: " + string.Join(", ", f.UnhandledOverlays));

        _panel.Text = sb.ToString();
    }

    // --- command line ----------------------------------------------------

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
                    _screenshot = true;
                    if (i + 1 < args.Length)
                        _screenshotPath = args[++i];
                    break;
                case "--expect":
                    if (i + 1 < args.Length && TryParseHex(args[++i], out ulong h))
                        _expectHash = h;
                    break;
            }
        }
    }

    private static ulong ParseHex(string s) => TryParseHex(s, out ulong v) ? v : 0UL;

    private static bool TryParseHex(string s, out ulong value)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s[2..];
        return ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }
}
