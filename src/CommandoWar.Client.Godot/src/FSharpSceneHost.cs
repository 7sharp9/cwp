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

    // Placeholder art (TASK-041, backlog B-034): Kenney "Isometric Miniature
    // Prototype" (CC0, src/CommandoWar.Client.Godot/art/LICENSE-THIRD-PARTY.md).
    // Loaded once and shared by every FSharpSceneHost instance -- never
    // per-frame. Keyed by DrawItem.TextureId for terrain (0 = floor,
    // 1 = block, 2 = crate); the agent texture is the same for both sides,
    // tinted per DrawItem.R/G/B (RenderShared.agentColor).
    private static readonly Texture2D[] TerrainTextures =
    [
        GD.Load<Texture2D>("res://art/terrain_floor.png"),
        GD.Load<Texture2D>("res://art/terrain_block.png"),
        GD.Load<Texture2D>("res://art/terrain_crate.png"),
    ];

    private static readonly Texture2D AgentTexture = GD.Load<Texture2D>("res://art/agent_human.png");

    // The human figure's own pixel bounds within the 256x512 Kenney canvas
    // (the rest is transparent padding sized for the tallest block in the
    // set) -- found by inspecting the source PNG's alpha channel, not
    // guessed. Cropped via DrawTextureRectRegion rather than drawing the
    // whole padded canvas, or the figure would render illegibly small at
    // terrain-tile scale.
    private static readonly Rect2 AgentSourceRect = new(106, 324, 45, 133);

    private IClientScene _scene;
    private Label _hud;

    private bool _selfCheck, _screenshotMode, _devOverlayMode;
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

        // Screenshot evidence for CommandDemoScene needs a selection, a
        // route preview, and an issued order actually visible -- with no
        // synthetic input in --screenshot mode otherwise, the capture would
        // just show idle agents. Scripted, fixed arguments; the same
        // scene-specific-dispatch precedent as RunSelfCheck below. Pauses
        // immediately after issuing so the order stays queued/undelivered
        // (the orange "pending" route) for the whole capture window, rather
        // than completing before frame 45 -- the state Dave's review asked
        // to actually see.
        if (_screenshotMode && SceneType == "CwClientCore.CommandDemoScene")
        {
            _scene.OnClick(true, 0, 0);
            _scene.OnTogglePause();
            _scene.OnHover(3, 0);
            _scene.OnClick(true, 3, 0);
        }

        // `--dev-overlay` (TASK-043, backlog B-029): a separate opt-in flag,
        // not folded into the priming above, so a plain `--screenshot`
        // capture keeps producing TASK-042's existing evidence unchanged.
        if (_devOverlayMode)
            _scene.OnToggleDevOverlay();

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
        TickHash[] sequence;
        ulong expected;
        string label;

        switch (SceneType)
        {
            case "CwClientCore.DemoRenderScene":
                label = "demo-render-scene self-check (DemoScenario, terrain-demo)";
                sequence = DemoDrive.runFullSequence();
                expected = 0xC68F993BC605313CUL; // DemoScenario tick 20 (TASK-044 re-pin, Canonical.FormatVersion 8 -> 9)
                break;
            case "CwClientCore.CommandDemoScene":
                label = "command-demo-scene self-check (scripted select + MoveTo(3,0))";
                sequence = CommandDemoDrive.runScriptedSelfCheck();
                expected = 0xD27E623504262CE9UL; // CommandDemoScene tick 20 (TASK-044 re-pin, Canonical.FormatVersion 8 -> 9)
                break;
            default:
                GD.PrintErr($"FSharpSceneHost: --selfcheck has no evidence path for '{SceneType}'");
                return 2;
        }

        GD.Print($"# {label}");
        foreach (TickHash th in sequence)
            GD.Print($"tick={th.Tick} hash=0x{th.Hash:X16}");

        TickHash final = sequence[^1];
        bool ok = final.Hash == expected;
        GD.Print(ok
            ? $"MATCH expected final hash 0x{expected:X16} at tick {final.Tick}"
            : $"MISMATCH expected 0x{expected:X16}, got 0x{final.Hash:X16}");
        return ok ? 0 : 1;
    }

    // --- rendering (view only): cell<->screen projection + Draw* calls -----

    private Vector2 CellToScreen(float cx, float cy) =>
        Origin + new Vector2((cx - cy) * (TileW * 0.5f), (cx + cy) * (TileH * 0.5f));

    // Exact inverse of CellToScreen -- the MainNode.cs disposable-spike
    // precedent. Input -> typed command is F#'s job (ADR-0004); this stays
    // "marshal + forward": resolve a cell, pass primitives into IClientScene.
    // Correct for a terrain-diamond click; an agent's own circle is drawn
    // TileH/2 above this (see _Draw), so a click resolves to the wrong cell
    // near the top of a visible agent -- TryHitAgentCircle below corrects for
    // that before falling back to this.
    private Vector2I ScreenToCell(Vector2 screen)
    {
        Vector2 p = screen - Origin;
        float a = p.X / (TileW * 0.5f); // cx - cy
        float b = p.Y / (TileH * 0.5f); // cx + cy
        return new Vector2I(Mathf.RoundToInt((a + b) * 0.5f), Mathf.RoundToInt((b - a) * 0.5f));
    }

    // Hit-tests against each agent's actual rendered circle (its screen
    // position, TileH/2 above ScreenToCell's diamond-centre assumption), not
    // the diamond grid -- fixes clicks only registering near the base of the
    // sprite (Dave's review feedback on TASK-040). `item.A >= 0.99f` picks
    // out real, fully-opaque agents only: the halo/preview/pending/committed
    // overlays are also Kind = 1 but always drawn translucent.
    private bool TryHitAgentCircle(Vector2 screenPos, out Vector2I cell)
    {
        foreach (DrawItem item in _scene.DrawList())
        {
            if (item.Kind != 1 || item.A < 0.99f)
                continue;

            Vector2 center = CellToScreen(item.Cx, item.Cy) - new Vector2(0, TileH * 0.5f);
            if (screenPos.DistanceTo(center) <= item.Radius + 4f)
            {
                cell = new Vector2I(Mathf.RoundToInt(item.Cx), Mathf.RoundToInt(item.Cy));
                return true;
            }
        }

        cell = default;
        return false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_scene == null || _selfCheck || _screenshotMode)
            return;

        if (@event is InputEventMouseButton { Pressed: true } mb
            && (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Right))
        {
            Vector2I cell = TryHitAgentCircle(mb.Position, out Vector2I hit) ? hit : ScreenToCell(mb.Position);
            _scene.OnClick(mb.ButtonIndex == MouseButton.Left, cell.X, cell.Y);
        }
        else if (@event is InputEventMouseMotion mm)
        {
            Vector2I cell = ScreenToCell(mm.Position);
            _scene.OnHover(cell.X, cell.Y);
        }
        else if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
        {
            _scene.OnTogglePause();
        }
        else if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F1 })
        {
            _scene.OnToggleDevOverlay();
        }
    }

    public override void _Draw()
    {
        if (_scene == null)
            return;

        foreach (DrawItem item in _scene.DrawList())
        {
            Vector2 pos = CellToScreen(item.Cx, item.Cy);
            var color = new Color(item.R, item.G, item.B, item.A);

            switch (item.Kind)
            {
                case 0:
                    DrawTerrainTile(pos, item.TextureId, color);
                    break;
                case 2:
                    // Developer overlay (TASK-043, backlog B-029): a line
                    // segment between two cells (line-of-sight rays, fire
                    // lines), both endpoints projected through the same
                    // isometric transform as every other item.
                    DrawLine(pos, CellToScreen(item.Cx2, item.Cy2), color, item.Radius);
                    break;
                case 3:
                    // Developer overlay: a text label at a cell (grid
                    // coordinates). `ThemeDB.FallbackFont` -- this host draws
                    // no other text itself (the HUD is a `Label` node, not a
                    // `_Draw` call), so there is no existing font to reuse.
                    DrawString(
                        ThemeDB.FallbackFont,
                        pos,
                        item.Text,
                        HorizontalAlignment.Center,
                        -1,
                        (int)item.Radius,
                        color);
                    break;
                default:
                {
                    Vector2 agentPos = pos - new Vector2(0, TileH * 0.5f);

                    // A translucent item (A < 0.99) is a halo/route-preview
                    // marker, not a real agent (TryHitAgentCircle's own
                    // precedent) -- keep the plain circle for those; only a
                    // real, full-opacity agent gets the Kenney figure.
                    if (item.A >= 0.99f)
                        DrawAgentFigure(agentPos, item.Radius, color);
                    else
                    {
                        DrawCircle(agentPos, item.Radius, color);
                        DrawArc(agentPos, item.Radius, 0, Mathf.Tau, 20, Colors.White, 1.5f);
                    }

                    break;
                }
            }
        }
    }

    // A terrain tile's Kenney texture is a tall, bottom-anchored canvas (room
    // above the ground plane for a block/crate's height); its own bottom
    // edge is the tile's near/south corner, i.e. the same point DrawDiamond
    // used to draw -- `pos.Y + TileH * 0.5f`, horizontally centred on `pos.X`.
    // Drawn at a fixed on-screen width (TileW) with height derived from the
    // texture's own aspect ratio, so the flat floor tile still lands at
    // exactly TileW x TileH.
    private void DrawTerrainTile(Vector2 pos, int textureId, Color color)
    {
        Texture2D tex = TerrainTextures[Mathf.Clamp(textureId, 0, TerrainTextures.Length - 1)];
        Vector2 size = tex.GetSize();
        float w = TileW;
        float h = w * (size.Y / size.X);
        var rect = new Rect2(pos.X - w * 0.5f, pos.Y + TileH * 0.5f - h, w, h);
        DrawTextureRect(tex, rect, false, color);
    }

    // An agent's Kenney figure is cropped from its own padded canvas
    // (AgentSourceRect) and drawn foot-anchored at `agentPos`, the same point
    // TryHitAgentCircle already treats as the agent's on-screen centre --
    // `radius` scales the crop the same way the old circle's diameter did,
    // so click hit-testing (unchanged) still matches what is drawn.
    private void DrawAgentFigure(Vector2 agentPos, float radius, Color color)
    {
        float w = radius * 2f;
        float h = w * (AgentSourceRect.Size.Y / AgentSourceRect.Size.X);
        var rect = new Rect2(agentPos.X - w * 0.5f, agentPos.Y + radius - h, w, h);
        DrawTextureRectRegion(AgentTexture, rect, AgentSourceRect, color);
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
                case "--dev-overlay":
                    _devOverlayMode = true;
                    break;
            }
        }
    }
}
