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
    // scaled up (88x44, TASK-052/backlog B-054: doubled again from
    // TASK-039's original 44x22) so the play area fills much more of the
    // 1280x800 window (previously ~34% of its width, ~28% of its height;
    // now ~62%/~50%) -- Dave's "general dev experience needs a bit of love"
    // feedback on TASK-043 review, confirmed via `AskUserQuestion` as a
    // larger fixed scale rather than interactive zoom/pan (a materially
    // bigger feature this task deliberately does not add). `Origin` is
    // recentred for the new scale and its `Y` raised from `60` to `110`,
    // clearing the HUD label's own worst case (three lines, `F1` on with an
    // agent selected, extending to roughly `y=62`) with real margin -- the
    // other half of Dave's complaint ("the corner HUD Label overlaps the
    // tick counter and agent sprites"). `CommandDemoScene.fs`'s own
    // `agentRadius`/`haloRadius` are doubled to match, so figures and
    // markers scale with the tile grid, not just the terrain tiles
    // (`DrawTerrainTile` already scales from `TileW`/`TileH` directly, only
    // the figure/marker radii needed a matching bump).
    private const float TileW = 88f;
    private const float TileH = 44f;
    private static readonly Vector2 Origin = new(552f, 110f);

    // Placeholder art (TASK-041, backlog B-034): Kenney "Isometric Miniature
    // Prototype" (CC0, src/CommandoWar.Client.Godot/art/LICENSE-THIRD-PARTY.md).
    // Loaded once and shared by every FSharpSceneHost instance -- never
    // per-frame. Keyed by DrawItem.TextureId for terrain (0 = floor,
    // 1 = block, 2 = crate).
    private static readonly Texture2D[] TerrainTextures =
    [
        GD.Load<Texture2D>("res://art/terrain_floor.png"),
        GD.Load<Texture2D>("res://art/terrain_block.png"),
        GD.Load<Texture2D>("res://art/terrain_crate.png"),
    ];

    // Agent facing (TASK-054, backlog B-052; TASK-056 renamed from
    // AgentFacingTextures once a second, animated texture set was added):
    // 8 pre-rendered `45°` rotations of the Kenney **idle** pose
    // (`Human_0..7_Idle0.png`, the pack's own `Information.png` documents
    // these as 8 rotations of one pose, not 8 character variants --
    // art/LICENSE-THIRD-PARTY.md), indexed by DrawItem.TextureId for a
    // full-opacity Kind = 1 item with no active run-cycle frame
    // (RenderShared.facingBin -- index N is directly Kenney's own `Human_N`
    // rotation, no remapping). Same for both sides, tinted per
    // DrawItem.R/G/B (RenderShared.agentColor).
    private static readonly Texture2D[] AgentIdleTextures =
    [
        GD.Load<Texture2D>("res://art/agent_human_facing0.png"),
        GD.Load<Texture2D>("res://art/agent_human_facing1.png"),
        GD.Load<Texture2D>("res://art/agent_human_facing2.png"),
        GD.Load<Texture2D>("res://art/agent_human_facing3.png"),
        GD.Load<Texture2D>("res://art/agent_human_facing4.png"),
        GD.Load<Texture2D>("res://art/agent_human_facing5.png"),
        GD.Load<Texture2D>("res://art/agent_human_facing6.png"),
        GD.Load<Texture2D>("res://art/agent_human_facing7.png"),
    ];

    // Agent run-cycle animation (TASK-056, backlog B-052): each of the 8
    // facing directions' own 10-frame `Human_N_RunF.png` cycle
    // (`agent_human_run{dir}_{frame}.png`, cropped to the identical shared
    // rect the idle textures above use, so switching between an idle and a
    // running frame never changes the drawn figure's on-screen size --
    // DrawAgentFigure derives height from each texture's own aspect ratio
    // at a fixed on-screen width). Flat 80-entry array indexed
    // `dir * 10 + frame`, not a jagged Texture2D[8][10] -- ADR-0004's
    // "arrays of primitives/CLIMutable records" interop idiom is an F#/C#
    // boundary rule, not binding on a pure-C#-side asset table, but a flat
    // array keeps this table the same shape as every other GD.Load array
    // in this file.
    private static readonly Texture2D[] AgentRunTextures = Enumerable.Range(0, 8 * 10)
        .Select(i => GD.Load<Texture2D>($"res://art/agent_human_run{i / 10}_{i % 10}.png"))
        .ToArray();

    // Fire-feedback effect sprites (TASK-046, backlog B-057): Kenney
    // "Particle Pack" (CC0, art/LICENSE-THIRD-PARTY.md). Keyed by
    // DrawItem.TextureId for a Kind = 4 item: 0 = muzzle flash (shooter),
    // 1 = impact hit (target), 2 = impact miss (target) -- a distinct shape
    // per outcome, not a colour-only hit/miss tint.
    private static readonly Texture2D[] EffectTextures =
    [
        GD.Load<Texture2D>("res://art/effect_muzzle_flash.png"),
        GD.Load<Texture2D>("res://art/effect_impact_hit.png"),
        GD.Load<Texture2D>("res://art/effect_impact_miss.png"),
    ];

    // XCOM-style HUD order-mode icons (TASK-048, backlog B-059): Kenney
    // "Board Game Icons" (CC0, art/LICENSE-THIRD-PARTY.md). Indexed the same
    // 0 = MoveTo / 1 = Hold / 2 = Assault / 3 = Withdraw vocabulary as
    // IClientScene.OnOrderModeClick/OrderMode. Fixed screen-space layout, not
    // world-grid content, so this bar is owned entirely by this C# host (a
    // HUD-chrome layout concern -- ADR-0004's "Raw input capture | C#" /
    // "Render loop | C#" rows -- not routed through the world-space DrawItem
    // list): OrderModeIconRects below are computed once from these same
    // texture sizes, never duplicated as magic numbers.
    private static readonly Texture2D[] OrderModeTextures =
    [
        GD.Load<Texture2D>("res://art/hud_move.png"),
        GD.Load<Texture2D>("res://art/hud_hold.png"),
        GD.Load<Texture2D>("res://art/hud_assault.png"),
        GD.Load<Texture2D>("res://art/hud_withdraw.png"),
    ];

    private const float OrderModeIconSize = 48f;
    private const float OrderModeIconGap = 8f;
    private static readonly Vector2 OrderModeBarOrigin = new(12f, 720f);

    private static Rect2 OrderModeIconRect(int index) =>
        new(
            OrderModeBarOrigin.X + index * (OrderModeIconSize + OrderModeIconGap),
            OrderModeBarOrigin.Y,
            OrderModeIconSize,
            OrderModeIconSize);

    private IClientScene _scene;
    private Label _hud;

    private bool _selfCheck, _screenshotMode, _devOverlayMode;
    private string _screenshotPath;
    private int? _headlessExit;
    private int _screenshotFrameCount;

    // TASK-063 (backlog B-033 narrowed): a second, separate screenshot mode
    // rather than folding into `--screenshot` above -- that existing
    // priming immediately re-pauses to hold a pending/hover-preview state
    // (TASK-040/048's own evidence), which is the opposite of what mission-
    // summary evidence needs: the sim must keep advancing, unpaused, long
    // enough for DemoScenario's sole authored objective (a non-optional
    // `ReachArea` at "ridge-top", (4,4)) to actually complete. A longer
    // frame threshold (`MissionScreenshotFrameCount`) gives an 8-cell
    // Manhattan walk from (0,0) at this demo's half agent speed real time
    // to finish, with margin.
    private bool _screenshotMissionMode;
    private const int MissionScreenshotFrameCount = 400;

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
        // to actually see. Also selects agent 1 and arms the Hold HUD icon
        // without clicking a target (TASK-048, backlog B-059), so the
        // captured frame also shows the armed-icon highlight and the
        // hover-preview Hold-area outline -- this task's two new visible
        // pieces of evidence, alongside the pre-existing pending-route proof.
        if (_screenshotMode && SceneType == "CwClientCore.CommandDemoScene")
        {
            _scene.OnClick(true, 0, 0);
            _scene.OnTogglePause();
            _scene.OnHover(3, 0);
            _scene.OnClick(true, 3, 0);
            _scene.OnClick(true, 0, 1);
            _scene.OnOrderModeClick(1);
            _scene.OnHover(2, 1);
        }

        // TASK-063, backlog B-033 narrowed: select agent 0 and send it
        // straight to the ridge-top objective area -- deliberately left
        // unpaused (unlike `_screenshotMode` above), so `_Process`'s normal
        // `Update(delta)` calls keep advancing real ticks until the
        // objective completes and the mission-summary panel appears.
        if (_screenshotMissionMode && SceneType == "CwClientCore.CommandDemoScene")
        {
            _scene.OnClick(true, 0, 0);
            _scene.OnHover(4, 4);
            _scene.OnClick(true, 4, 4);
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
        else if (_screenshotMissionMode && ++_screenshotFrameCount >= MissionScreenshotFrameCount)
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
                expected = 0xEC8F01D781AB2122UL; // DemoScenario tick 20 (TASK-063 re-pin: a real, pre-existing stale pin found while verifying TASK-063 -- TASK-062's Canonical.FormatVersion 12 -> 13 bump changed every canonical byte layout, including DemoScenario's, but TASK-062 never touched CommandoWar.Client.Godot and so never re-ran this self-check; behaviour is unaffected, this is the same format-version-only re-pin every corpus/fixture/diagnostics golden already got)
                break;
            case "CwClientCore.CommandDemoScene":
                label = "command-demo-scene self-check (scripted MoveTo(3,0) + Hold(2,1) via order-mode icon, then MoveTo(4,4) reaching the ridge-top objective)";
                sequence = CommandDemoDrive.runScriptedSelfCheck();
                expected = 0xED5437A8773C92B2UL; // CommandDemoScene tick 40 (TASK-063 re-pin: the scripted sequence now sends agent 0 on to (4,4), completing DemoScenario's sole authored objective and reaching MissionOutcome = Succeeded; extended from 20 to 40 ticks to give the extra leg time to complete and settle)
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

    // A HUD order-mode icon hit test (TASK-048, backlog B-059): the fixed
    // screen-space rects above, checked ahead of every other click handling
    // -- a miss falls through to the existing world-cell OnClick unchanged
    // (Dave's confirmed design: "FSharpSceneHost checks click position
    // against the known HUD icon rects first ... a miss falls through to the
    // existing world-cell OnClick unchanged").
    private static bool TryHitOrderModeIcon(Vector2 screenPos, out int index)
    {
        for (int i = 0; i < OrderModeTextures.Length; i++)
        {
            if (OrderModeIconRect(i).HasPoint(screenPos))
            {
                index = i;
                return true;
            }
        }

        index = default;
        return false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_scene == null || _selfCheck || _screenshotMode || _screenshotMissionMode)
            return;

        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mbIcon
            && TryHitOrderModeIcon(mbIcon.Position, out int iconIndex))
        {
            _scene.OnOrderModeClick(iconIndex);
        }
        else if (@event is InputEventMouseButton { Pressed: true } mb
            && (mb.ButtonIndex == MouseButton.Left || mb.ButtonIndex == MouseButton.Right))
        {
            Vector2I cell = TryHitAgentCircle(mb.Position, out Vector2I hit) ? hit : ScreenToCell(mb.Position);
            _scene.OnClick(mb.ButtonIndex == MouseButton.Left, cell.X, cell.Y);
        }
        else if (@event is InputEventMouseMotion mm)
        {
            // TASK-052, backlog B-053: the same `TryHitAgentCircle`-first
            // fallback the click handler above already uses (`OnClick`),
            // so hovering directly over a rendered agent circle resolves to
            // that agent's cell -- otherwise the hover highlight this task
            // adds would fail to arm near the top of a visible agent, the
            // identical projection mismatch TASK-040 fixed for clicks.
            Vector2I cell = TryHitAgentCircle(mm.Position, out Vector2I hit) ? hit : ScreenToCell(mm.Position);
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
                case 4:
                    // A one-shot fire-feedback effect sprite (TASK-046,
                    // backlog B-057): muzzle flash or bullet impact, tinted
                    // per DrawItem.R/G/B/A.
                    DrawEffectSprite(pos, item.TextureId, item.Radius, color);
                    break;
                case 5:
                    // A hollow ring at the same vertical offset an agent
                    // figure draws at: a fog-of-war last-known-position
                    // marker (TASK-051, backlog B-055) or a hover-highlight
                    // around a selectable agent (TASK-052, backlog B-053).
                    // Stroke width bumped from `2.5f` to `3.5f` alongside
                    // TASK-052's doubled tile scale, for the same legibility
                    // reason the tile scale changed at all.
                    DrawArc(pos - new Vector2(0, TileH * 0.5f), item.Radius, 0, Mathf.Tau, 24, color, 3.5f);
                    break;
                default:
                {
                    Vector2 agentPos = pos - new Vector2(0, TileH * 0.5f);

                    // A translucent item (A < 0.99) is a halo/route-preview
                    // marker, not a real agent (TryHitAgentCircle's own
                    // precedent) -- keep the plain circle for those; only a
                    // real, full-opacity agent gets the Kenney figure, at
                    // its current facing bin (TextureId, TASK-054).
                    if (item.A >= 0.99f)
                        DrawAgentFigure(agentPos, item.TextureId, Mathf.RoundToInt(item.Cx2), item.Radius, color);
                    else
                    {
                        DrawCircle(agentPos, item.Radius, color);
                        DrawArc(agentPos, item.Radius, 0, Mathf.Tau, 20, Colors.White, 1.5f);
                    }

                    break;
                }
            }
        }

        DrawOrderModeBar();
        DrawMissionSummaryPanel();
    }

    // Mission summary panel (TASK-063, backlog B-033 narrowed): fixed
    // screen-space chrome, the `DrawOrderModeBar` precedent -- drawn last
    // (on top of everything, including the order-mode bar) so it is never
    // obscured. Only ever drawn once `_scene.MissionSummaryLines()` is
    // non-empty (`WorldState.MissionOutcome` has left `InProgress`); a dark
    // backing panel sized to its exact line count, first line (the outcome
    // headline) drawn larger than the rest.
    private void DrawMissionSummaryPanel()
    {
        string[] lines = _scene.MissionSummaryLines();
        if (lines.Length == 0)
            return;

        const float lineHeight = 26f;
        const float paddingX = 24f;
        const float paddingY = 20f;
        const float panelWidth = 460f;
        float panelHeight = paddingY * 2f + lineHeight * lines.Length;
        var origin = new Vector2(640f - panelWidth * 0.5f, 400f - panelHeight * 0.5f);
        var size = new Vector2(panelWidth, panelHeight);

        DrawRect(new Rect2(origin, size), new Color(0f, 0f, 0f, 0.8f));
        DrawRect(new Rect2(origin, size), Colors.White, false, 2f);

        for (int i = 0; i < lines.Length; i++)
        {
            bool headline = i == 0;
            int fontSize = headline ? 22 : 15;
            Color color = headline ? Colors.White : new Color(0.85f, 0.85f, 0.85f);
            // `DrawString`'s `pos` is the text box's own LEFT edge, not a
            // centre point -- `HorizontalAlignment.Center` then centres the
            // text within `[pos.X, pos.X + width]`, so the box must start at
            // the panel's own left edge (`origin.X`), not the screen centre
            // (640f), or the text renders shifted a full half-panel-width to
            // the right of the panel it is meant to sit inside.
            var pos = new Vector2(origin.X + paddingX * 0.5f, origin.Y + paddingY + lineHeight * i + fontSize);
            DrawString(ThemeDB.FallbackFont, pos, lines[i], HorizontalAlignment.Center, panelWidth - paddingX, fontSize, color);
        }
    }

    // XCOM-style HUD order-mode icon bar (TASK-048, backlog B-059): fixed
    // screen-space rects (OrderModeIconRect), drawn last so it always sits
    // on top of the world-space scene -- the devItems/fireEffects
    // "always draws on top" precedent, extended to genuine screen-space UI
    // chrome. A dark backing square behind every icon for legibility over
    // any terrain colour; a bright border around the currently armed mode
    // (`_scene.OrderMode()`, the `_hud.Text` "read once per frame" precedent)
    // so the player can see which order the next click will issue.
    private void DrawOrderModeBar()
    {
        int armed = _scene.OrderMode();

        for (int i = 0; i < OrderModeTextures.Length; i++)
        {
            Rect2 rect = OrderModeIconRect(i);
            DrawRect(rect, new Color(0f, 0f, 0f, 0.55f));
            DrawTextureRect(OrderModeTextures[i], rect.Grow(-6f), false, Colors.White);
            DrawRect(rect, i == armed ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 1f, 0.25f), false, i == armed ? 3f : 1f);
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

    // An agent's Kenney figure (one of 8 pre-cropped facing textures,
    // TASK-054, backlog B-052 -- no further crop needed, unlike the old
    // single-pose AgentTexture, since the new art is already tightly
    // bounded to its own alpha bbox, the DrawTerrainTile precedent) is
    // drawn foot-anchored at `agentPos`, the same point TryHitAgentCircle
    // already treats as the agent's on-screen centre -- `radius` scales it
    // the same way the old circle's diameter did, so click hit-testing
    // (unchanged) still matches what is drawn. `runFrame < 0` (the
    // `RenderShared.runFrameIndex`/`DrawItem.Cx2` sentinel, TASK-056) draws
    // the frozen idle pose; `0..9` draws that direction's own running-cycle
    // frame instead. Both texture sets share the identical crop rect
    // (art/LICENSE-THIRD-PARTY.md), so their aspect ratio -- and therefore
    // the drawn size at this fixed on-screen width -- never changes when
    // switching between them.
    private void DrawAgentFigure(Vector2 agentPos, int facingIndex, int runFrame, float radius, Color color)
    {
        Texture2D tex = runFrame < 0
            ? AgentIdleTextures[Mathf.Clamp(facingIndex, 0, AgentIdleTextures.Length - 1)]
            : AgentRunTextures[Mathf.Clamp(facingIndex, 0, 7) * 10 + Mathf.Clamp(runFrame, 0, 9)];
        Vector2 size = tex.GetSize();
        float w = radius * 2f;
        float h = w * (size.Y / size.X);
        var rect = new Rect2(agentPos.X - w * 0.5f, agentPos.Y + radius - h, w, h);
        DrawTextureRect(tex, rect, false, color);
    }

    // A fire-feedback effect sprite (TASK-046, backlog B-057) is centred, not
    // foot-anchored -- it marks a point (the shooter's or target's cell), not
    // a standing figure. Uses the same `pos - TileH/2` anchor as an agent/
    // cell-marker item (`cellMarker`'s own precedent) so it lines up with
    // whatever agent or marker occupies that cell.
    private void DrawEffectSprite(Vector2 pos, int textureId, float radius, Color color)
    {
        Vector2 center = pos - new Vector2(0, TileH * 0.5f);
        Texture2D tex = EffectTextures[Mathf.Clamp(textureId, 0, EffectTextures.Length - 1)];
        float size = radius * 2f;
        var rect = new Rect2(center.X - size * 0.5f, center.Y - size * 0.5f, size, size);
        DrawTextureRect(tex, rect, false, color);
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
                case "--screenshot-mission":
                    _screenshotMissionMode = true;
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
