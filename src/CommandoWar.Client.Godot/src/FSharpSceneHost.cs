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
using System.Collections.Generic;
using System.IO;
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

    // Index 4 = Suppress (TASK-064, backlog B-035): deliberately no texture
    // asset -- `docs/07_VERTICAL_SLICE.md` section 6 explicitly permits
    // presentation placeholders "until the integration gate", and this task
    // is the integration gate itself. Drawn procedurally (a crosshair, the
    // `DrawArc`/`DrawLine` primitives `_Draw`'s `Kind = 2`/`5` items already
    // use) in `DrawOrderModeBar` below instead of adding a fifth Kenney
    // texture -- a real icon is a follow-up, not blocking this task's own
    // acceptance criteria. `OrderModeIconCount` (not `OrderModeTextures.
    // Length`) is what the hit-test and draw loops both size themselves to,
    // so this one procedural icon still arms/highlights exactly like the
    // other four.
    private const int OrderModeIconCount = 5;

    private const float OrderModeIconSize = 48f;
    private const float OrderModeIconGap = 8f;
    private static readonly Vector2 OrderModeBarOrigin = new(12f, 720f);

    private static Rect2 OrderModeIconRect(int index) =>
        new(
            OrderModeBarOrigin.X + index * (OrderModeIconSize + OrderModeIconGap),
            OrderModeBarOrigin.Y,
            OrderModeIconSize,
            OrderModeIconSize);

    // Replay scrub bar (TASK-071, backlog B-064): fixed screen-space HUD
    // chrome, the `OrderModeIconRect`/`DrawOrderModeBar` precedent -- owned
    // entirely here, not a world-grid DrawItem. Positioned clear of the
    // order-mode icon bar (12,720)-(272,768) so both could in principle
    // render on the same frame without overlapping, though in practice
    // `_scene.TickCount() > 0` (only true for CwClientCore.ReplayDemoScene)
    // gates every draw/hit-test below, so a live-play scene never shows it.
    private static readonly Vector2 ScrubBarOrigin = new(200f, 780f);
    private const float ScrubBarWidth = 880f;
    private const float ScrubBarHeight = 18f;
    private static Rect2 ScrubBarRect => new(ScrubBarOrigin, new Vector2(ScrubBarWidth, ScrubBarHeight));

    // A drag inside the scrub bar tracks continuously (unlike the
    // rubber-band-select `_isDragging` below, which only resolves on
    // release) -- every motion event while dragging immediately scrubs, so
    // the rendered tick follows the mouse in real time.
    private bool _isDraggingScrub;

    private long ScrubTickAt(float screenX)
    {
        float t = Mathf.Clamp((screenX - ScrubBarOrigin.X) / ScrubBarWidth, 0f, 1f);
        return (long)Mathf.Round(t * _scene.TickCount());
    }

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

    // TASK-065 (backlog B-065): a scriptable evaluation tool, not evidence
    // for a specific task -- Dave asked directly to be able to watch the
    // real six-agent Bridgehead squad-advance-and-engage play out visually,
    // not just read a headless hash. Reuses `CommandDemoDrive.
    // runScriptedSelfCheck`'s own click sequence (the proven "advance on
    // the bridge, neutralise the machine gun" case) through the real
    // `OnClick`/`OnHover` UI path instead of `StepTicksHeadless`, then
    // stays unpaused (the `_screenshotMissionMode` precedent) so repeated
    // invocations at different frame counts build a filmstrip.
    private bool _screenshotSquadMode;
    private int _screenshotSquadFrameCount = 400;

    // `--screenshot-multiselect` (TASK-068, backlog B-067 second half): the
    // `_screenshotMode`/`_screenshotSquadMode` precedent -- a scripted,
    // fixed-argument priming of a real multi-agent selection and joint
    // order for evidence, paused immediately (see `_Ready`) so the
    // captured frame holds the pending state rather than racing it to
    // completion.
    private bool _screenshotMultiSelectMode;

    // Drag rubber-band-select (TASK-068): raw mouse state `_UnhandledInput`
    // tracks between a left-button press and its matching release -- no
    // button-up/`Pressed: false` handling existed anywhere in this file
    // before this task, so a genuine press/motion/release sequence is new,
    // not a tweak to the old single-event click handling.
    private bool _isDragging;
    private Vector2 _dragStartScreen;
    private Vector2 _dragCurrentScreen;

    // Below this on-screen drag distance, a press-then-release resolves as
    // an ordinary click instead of a (degenerate, zero-agent) drag-select --
    // guards against a few pixels of hand tremor on an intended single click
    // silently selecting nothing.
    private const float DragThresholdPixels = 6f;

    // TASK-064 (backlog B-035): resolves `content/<relativePath>` the same
    // way `AppraisalDemoScene.cs` already does for `content/replays` -- the
    // Godot project root sits two levels under the repo root
    // (`src/CommandoWar.Client.Godot/`), and `res://` paths do not reliably
    // support `..` traversal, so the absolute path is globalized first.
    private static string ResolveContentPath(string relativePath) =>
        Path.GetFullPath(Path.Combine(
            ProjectSettings.GlobalizePath("res://"), "..", "..", "content", relativePath));

    public override void _Ready()
    {
        ParseCommandLine();

        string bridgeheadScenarioPath = ResolveContentPath(Path.Combine("scenarios", "bridgehead.cwscenario"));

        // TASK-071, backlog B-064: `CwClientCore.ReplayDemoScene` reads a
        // `.cwreplay` file through `Ready`'s own `scenarioContentPath`
        // parameter, reusing it for a different content kind rather than
        // widening `IClientScene.Ready`'s signature (documented in the task
        // file's Inputs and assumptions). A fixed, committed fixture for
        // now -- no in-scene file picker, the smallest thing that lets the
        // scene load real content; a later task can widen this if a second
        // replay ever needs viewing.
        string replayFilePath = ResolveContentPath(Path.Combine("replays", "chokepoint-detour.cwreplay"));

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
        _scene.Ready(SceneType == "CwClientCore.ReplayDemoScene" ? replayFilePath : bridgeheadScenarioPath);

        // `--screenshot` evidence for ReplayDemoScene (TASK-071, backlog
        // B-064): starts "playing" (`OnTogglePause`'s reused semantics for
        // this scene) so the existing generic 45-frame capture window
        // (below, ungated by SceneType) shows real scrub progress rather
        // than the static tick-0 default.
        if (_screenshotMode && SceneType == "CwClientCore.ReplayDemoScene")
        {
            _scene.OnTogglePause();
        }

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
            // TASK-064 (backlog B-035): coordinates updated for the real
            // `bridgehead.cwscenario` layout -- agent 0 (leader) starts at
            // (3,5), agent 1 at (2,5); (5,5) is open west-bank ground well
            // short of the bridge, safe for a still evidence capture.
            _scene.OnClick(true, 3, 5, false);
            _scene.OnTogglePause();
            _scene.OnHover(5, 5);
            _scene.OnClick(true, 5, 5, false);
            _scene.OnClick(true, 2, 5, false);
            _scene.OnOrderModeClick(1);
            _scene.OnHover(2, 4);
        }

        // TASK-063, backlog B-033 narrowed: select agent 0 and send it
        // straight to the optional `reach observation` objective area
        // (4,4) -- deliberately left unpaused (unlike `_screenshotMode`
        // above), so `_Process`'s normal `Update(delta)` calls keep
        // advancing real ticks. TASK-064 (backlog B-035) updated the
        // selection coordinate for the real `bridgehead.cwscenario` layout
        // (agent 0 starts at (3,5), not (0,0)) -- reaching (4,4) still
        // completes `ObjectiveId 1`, but that objective is optional and
        // does not end the mission on Bridgehead's real content, so this
        // mode no longer reaches the mission-summary panel the way it did
        // against `DemoScenario`'s single non-optional objective; the
        // panel's own evidence (`docs/evidence/task-063-mission-summary.
        // png`) is unaffected, captured against that earlier content.
        if (_screenshotMissionMode && SceneType == "CwClientCore.CommandDemoScene")
        {
            _scene.OnClick(true, 3, 5, false);
            _scene.OnHover(4, 4);
            _scene.OnClick(true, 4, 4, false);
        }

        // `--screenshot-squad` (TASK-065): the real all-six-agents-advance
        // order through the actual click path, the
        // `CommandDemoDrive.runScriptedSelfCheck` sequence.
        if (_screenshotSquadMode && SceneType == "CwClientCore.CommandDemoScene")
        {
            void Order(int selectX, int selectY, int targetX, int targetY)
            {
                _scene.OnClick(true, selectX, selectY, false);
                _scene.OnHover(targetX, targetY);
                _scene.OnClick(true, targetX, targetY, false);
            }

            Order(3, 5, 8, 5); // agent 0 (fireteam-alpha, slot 0)
            Order(2, 5, 7, 5); // agent 1 (fireteam-alpha, slot 1)
            Order(2, 6, 8, 5); // agent 2 (fireteam-alpha, slot 2)
            Order(3, 7, 9, 6); // agent 3 (fireteam-bravo, slot 0)
            Order(4, 7, 9, 6); // agent 4 (fireteam-bravo, slot 1)
            Order(4, 8, 8, 6); // agent 5 (fireteam-bravo, slot 2)
        }

        // TASK-068 (backlog B-067 second half): `--screenshot-multiselect`,
        // the `--screenshot-squad` precedent -- primes a real two-agent
        // multi-select and a joint order through the actual `OnDragSelect`/
        // `OnHover`/`OnClick` methods (not raw mouse input, the existing
        // screenshot-priming precedent throughout this file), so the
        // captured frame shows two selection halos and two diverging,
        // formation-resolved route previews for the same clicked cell --
        // the visible proof that a real client-issued group order now
        // exercises `ReceivedOrder.AsGroup`/formation redirect. Agents 0 and
        // 1 (fireteam-alpha, slots 0/1, distinct authored
        // `FormationOffset`s) at their own starting cells (3,5)/(2,5);
        // (6,5) is open west-bank ground well short of the bridge, safe for
        // a still evidence capture, matching `_screenshotMode`'s own choice
        // of target area.
        if (_screenshotMultiSelectMode && SceneType == "CwClientCore.CommandDemoScene")
        {
            _scene.OnDragSelect([3, 2], [5, 5], false);
            _scene.OnHover(6, 5);
            _scene.OnClick(true, 6, 5, false);
            _scene.OnTogglePause();
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
        else if (_screenshotSquadMode && ++_screenshotFrameCount >= _screenshotSquadFrameCount)
            CaptureScreenshot();
        // TASK-068: the scene is paused immediately after priming (see
        // `_Ready`), so a short, fixed frame count is enough -- the
        // `_screenshotMode` precedent, not `_screenshotSquadMode`'s longer
        // unpaused run.
        else if (_screenshotMultiSelectMode && ++_screenshotFrameCount >= 45)
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
                expected = 0x6213D672BC36FDB8UL; // DemoScenario tick 20 (TASK-067 re-pin: Canonical.FormatVersion 14 -> 15, ReceivedOrder.AsGroup added -- byte-layout only, DemoScenario authors no formation)
                break;
            case "CwClientCore.CommandDemoScene":
                label = "command-demo-scene self-check (real bridgehead.cwscenario content: all six friendly agents ordered toward the bridge, neutralising the machine-gun team through real automatic engagement with no friendly casualties)";
                sequence = CommandDemoDrive.runScriptedSelfCheck(ResolveContentPath(Path.Combine("scenarios", "bridgehead.cwscenario")));
                expected = 0x84A25E3559111E9BUL; // CommandDemoScene tick 90 (TASK-069 re-pin: content/scenarios/bridgehead.cwscenario's Trooper unit-type MoveSpeed 2 -> 1, halving movement speed per Dave's own live-playtest read that it ran "at least twice as fast as it should be" -- a genuine behaviour change, not byte-layout only: every agent now takes twice as many ticks to cross a cell. Confirmed via a temporary dotnet fsi probe (removed after use) with the dev overlay enabled (ground truth, ignoring fog of war) that the outcome is still reached within the same 90-tick budget: by tick 90 exactly one agent has died (the machine-gun team, cell (11,5)) and all six friendly agents remain Alive -- "no friendly casualties, machine gun neutralised" unchanged
                break;
            case "CwClientCore.ReplayDemoScene":
                // TASK-071, backlog B-064: `_scene.Ready` already loaded and
                // ran `content/replays/chokepoint-detour.cwreplay` above (a
                // small, already-committed 10-tick corpus fixture, the
                // TASK-070 precedent). This proves the scene's own real
                // `SetTick`/`CurrentHash` path reproduces the exact same
                // per-tick canonical hash chain `cwheadless replay-file`
                // already prints for this fixture, tick by tick from 0
                // (the initial state, not covered by `ReplayOutcome.
                // TickStates`) through the final tick -- not just that
                // `Replay.run` itself is correct (already proven
                // elsewhere), but that this scene's own scrubbing plumbing
                // is byte-identical to it.
                label = "replay-demo-scene self-check (chokepoint-detour.cwreplay, scrubbed tick by tick through the real IClientScene.SetTick/CurrentHash path)";
                var scrubbed = new List<TickHash>();
                for (long t = 0; t <= _scene.TickCount(); t++)
                {
                    _scene.SetTick(t);
                    scrubbed.Add(new TickHash { Tick = t, Hash = _scene.CurrentHash() });
                }
                sequence = scrubbed.ToArray();
                expected = 0x5876C1280DDAE2CBUL; // chokepoint-detour.cwreplay tick 10 (`dotnet run --project src/CommandoWar.Headless -c Release -- replay-file content/replays/chokepoint-detour.cwreplay`, ground truth re-derived directly, not guessed)
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
        for (int i = 0; i < OrderModeIconCount; i++)
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
        if (_scene == null || _selfCheck || _screenshotMode || _screenshotMissionMode || _screenshotSquadMode || _screenshotMultiSelectMode)
            return;

        // Replay scrub bar (TASK-071, backlog B-064): checked first, the
        // `TryHitOrderModeIcon` precedent -- a miss falls through to every
        // other handler unchanged. Only ever hittable when
        // `_scene.TickCount() > 0` (only `CwClientCore.ReplayDemoScene`
        // today), so this never intercepts input on a live-play scene.
        if (_scene.TickCount() > 0
            && @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mbScrub
            && ScrubBarRect.HasPoint(mbScrub.Position))
        {
            _isDraggingScrub = true;
            _scene.SetTick(ScrubTickAt(mbScrub.Position.X));
        }
        else if (_isDraggingScrub && @event is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left })
        {
            _isDraggingScrub = false;
        }
        else if (_isDraggingScrub && @event is InputEventMouseMotion mmScrub)
        {
            _scene.SetTick(ScrubTickAt(mmScrub.Position.X));
        }
        // Step one tick back/forward (TASK-071): only meaningful once
        // something is loaded to step through.
        else if (_scene.TickCount() > 0 && @event is InputEventKey { Pressed: true, Keycode: Key.Left })
        {
            _scene.SetTick(_scene.CurrentTick() - 1);
        }
        else if (_scene.TickCount() > 0 && @event is InputEventKey { Pressed: true, Keycode: Key.Right })
        {
            _scene.SetTick(_scene.CurrentTick() + 1);
        }
        else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mbIcon
            && TryHitOrderModeIcon(mbIcon.Position, out int iconIndex))
        {
            _scene.OnOrderModeClick(iconIndex);
        }
        // TASK-068 (backlog B-067 second half): a left-button press starts
        // tracking a potential drag rather than resolving immediately --
        // no button-up handling existed anywhere in this file before this
        // task, so click-vs-drag disambiguation happens entirely on
        // release, below.
        else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mbDown)
        {
            _isDragging = true;
            _dragStartScreen = mbDown.Position;
            _dragCurrentScreen = mbDown.Position;
        }
        else if (@event is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } mbUp)
        {
            // A stray release with no matching tracked press -- the press
            // itself was consumed by the order-mode-icon branch above.
            if (!_isDragging)
                return;

            _isDragging = false;
            bool shiftHeld = mbUp.ShiftPressed;

            if (_dragStartScreen.DistanceTo(mbUp.Position) < DragThresholdPixels)
            {
                Vector2I cell = TryHitAgentCircle(mbUp.Position, out Vector2I hit) ? hit : ScreenToCell(mbUp.Position);
                _scene.OnClick(true, cell.X, cell.Y, shiftHeld);
            }
            else
            {
                Rect2 rect = new Rect2(_dragStartScreen, mbUp.Position - _dragStartScreen).Abs();
                var xs = new List<int>();
                var ys = new List<int>();

                // The `TryHitAgentCircle` per-agent screen-position
                // derivation and full-opacity-real-agent filter, reused
                // with a rectangle-contains-centre test in place of its
                // point-vs-circle distance test -- any side, `friendlyAt`
                // on the F# side does the side filtering, the same
                // boundary division `OnClick` already uses.
                foreach (DrawItem item in _scene.DrawList())
                {
                    if (item.Kind != 1 || item.A < 0.99f)
                        continue;

                    Vector2 center = CellToScreen(item.Cx, item.Cy) - new Vector2(0, TileH * 0.5f);
                    if (rect.HasPoint(center))
                    {
                        xs.Add(Mathf.RoundToInt(item.Cx));
                        ys.Add(Mathf.RoundToInt(item.Cy));
                    }
                }

                _scene.OnDragSelect(xs.ToArray(), ys.ToArray(), shiftHeld);
            }
        }
        else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } mbRight)
        {
            Vector2I cell = TryHitAgentCircle(mbRight.Position, out Vector2I hit) ? hit : ScreenToCell(mbRight.Position);
            _scene.OnClick(false, cell.X, cell.Y, false);
        }
        else if (@event is InputEventMouseMotion mm)
        {
            if (_isDragging)
                _dragCurrentScreen = mm.Position;

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

        DrawDragMarquee();
        DrawOrderModeBar();
        DrawScrubBar();
        DrawMissionSummaryPanel();
    }

    // Replay scrub bar (TASK-071, backlog B-064): a filled progress track
    // plus a handle at the current tick, the `DrawOrderModeBar` fixed-rect
    // chrome precedent. Gated on `_scene.TickCount() > 0` so it only ever
    // appears on a scene that has something to scrub (today, only
    // `CwClientCore.ReplayDemoScene`).
    private void DrawScrubBar()
    {
        long total = _scene.TickCount();
        if (total <= 0)
            return;

        DrawRect(ScrubBarRect, new Color(0f, 0f, 0f, 0.55f));
        DrawRect(ScrubBarRect, new Color(1f, 1f, 1f, 0.35f), false, 1.5f);

        float t = (float)_scene.CurrentTick() / total;
        var filled = new Rect2(ScrubBarOrigin, new Vector2(ScrubBarWidth * t, ScrubBarHeight));
        DrawRect(filled, new Color(0.35f, 0.75f, 1.0f, 0.65f));

        float handleX = ScrubBarOrigin.X + ScrubBarWidth * t;
        var handle = new Rect2(handleX - 3f, ScrubBarOrigin.Y - 4f, 6f, ScrubBarHeight + 8f);
        DrawRect(handle, Colors.White);
    }

    // Rubber-band drag-select marquee (TASK-068, backlog B-067 second
    // half): screen-space chrome, the `DrawMissionSummaryPanel`/
    // `DrawOrderModeBar` translucent-fill-plus-border precedent. Only drawn
    // once the drag has moved past the same threshold that distinguishes a
    // drag from a plain click (`_UnhandledInput`'s own `DragThresholdPixels`
    // check) -- a drag that resolves as a click never flashes a marquee.
    private void DrawDragMarquee()
    {
        if (!_isDragging || _dragStartScreen.DistanceTo(_dragCurrentScreen) < DragThresholdPixels)
            return;

        Rect2 rect = new Rect2(_dragStartScreen, _dragCurrentScreen - _dragStartScreen).Abs();
        DrawRect(rect, new Color(0.3f, 0.9f, 0.4f, 0.15f));
        DrawRect(rect, new Color(0.3f, 0.9f, 0.4f, 0.85f), false, 1.5f);
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

        for (int i = 0; i < OrderModeIconCount; i++)
        {
            Rect2 rect = OrderModeIconRect(i);
            DrawRect(rect, new Color(0f, 0f, 0f, 0.55f));
            if (i < OrderModeTextures.Length)
                DrawTextureRect(OrderModeTextures[i], rect.Grow(-6f), false, Colors.White);
            else
                DrawSuppressIcon(rect.Grow(-6f));
            DrawRect(rect, i == armed ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 1f, 0.25f), false, i == armed ? 3f : 1f);
        }
    }

    // The procedural Suppress icon (index 4, see OrderModeTextures above): a
    // crosshair -- a hollow ring plus a cross through its centre, orange to
    // read as "suppressive fire" distinctly from the other four icons' cool
    // Kenney palette.
    private void DrawSuppressIcon(Rect2 area)
    {
        var color = new Color(1f, 0.55f, 0.15f);
        Vector2 center = area.GetCenter();
        float radius = area.Size.X * 0.35f;
        DrawArc(center, radius, 0, Mathf.Tau, 20, color, 3f);
        DrawLine(center - new Vector2(radius * 1.4f, 0), center + new Vector2(radius * 1.4f, 0), color, 3f);
        DrawLine(center - new Vector2(0, radius * 1.4f), center + new Vector2(0, radius * 1.4f), color, 3f);
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
                case "--screenshot-squad":
                    _screenshotSquadMode = true;
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int frames))
                    {
                        _screenshotSquadFrameCount = frames;
                        i++;
                    }
                    if (i + 1 < args.Length)
                        _screenshotPath = args[++i];
                    break;
                case "--screenshot-multiselect":
                    _screenshotMultiSelectMode = true;
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
