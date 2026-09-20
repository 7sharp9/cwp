# CommandoWar Godot .NET framework spike (TASK-004)

**Disposable.** This host exists only to produce ADR-0001 evidence. It is not
production code and must be removable without touching `CommandoWar.Sim`,
`CommandoWar.Sim.Tests`, `CommandoWar.Headless`, `content/`, or the shared
fixture. It is deliberately **not** in `CommandoWar.slnx`.

## Appraisal-divergence demo (TASK-029)

`scenes/AppraisalDemo.tscn` + `src/AppraisalDemoScene.cs` is the current
`run/main_scene` (the TASK-004 greybox spike below still builds and runs from
`Main.tscn`). It is a **read-only, corpus-scoped** first realisation of the
Godot `DiagnosticFrame` renderer (backlog B-029, pulled forward as P3
decision-support): it loads the committed `content/replays/exposed-approach`
corpus entry, builds its per-tick `DiagnosticFrame` sequence via
`DiagnosticRender.runFrames`, and renders the state plus the `KnownContact` /
`PlannedPath` / `OrderAppraisal` overlays with a 13-frame tick slider and a
tick / hash / format / draws HUD, mirroring the `DiagnosticRender.Svg` colours
and glyphs. The divergence lands on tick 1: agent 0 (`Discipline 1`) `Refused
RouteTooExposed threat-agent-2`, agent 1 (`Discipline 6`) `Accepted`.

All non-trivial logic is in the framework-neutral F# helper
`src/CommandoWar.Headless/AppraisalDemo.fs`; this C# scene is a thin renderer (a
scoped deviation from ADR-0004's "F# client-core / no logic in C#" rule for this
disposable demo — see the TASK-029 task file and ledger). No `CommandoWar.Sim`
change; adds one `ProjectReference` to `CommandoWar.Headless`.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
"$GODOT" --editor --headless --quit --path .          # one-time import
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# windowed: drag the slider or press Left / Right to scrub ticks 0..12
"$GODOT" --path .

# headless smoke: prints the tick-1 dispositions + hash, asserts vs the golden
"$GODOT" --headless --path . -- --selfcheck            # MATCH 0x5D5A30C0DF64AC93, exit 0

# committed evidence screenshot (windowed; headless has no viewport texture)
"$GODOT" --path . --resolution 1000x620 -- --screenshot <abs-path>.png
```

Committed screenshot: `docs/evidence/task-029-appraisal-demo.png`.

## Live snapshot-rendering demo (TASK-039)

`scenes/SnapshotDemo.tscn` + `src/FSharpSceneHost.cs` is the first
production Godot scene: it live-steps the terrain-demo scenario
(`src/CommandoWar.Headless/DemoScenario.fs` — an elevation ridge, an
impassable block, a movement-cost patch, an opaque wall, and directional
cover) via `Simulation.step` and renders it isometrically with one unified,
depth-sorted draw list (terrain interleaved with agents by screen depth, not
"all terrain then all agents" — the disposable `MainNode.cs` spike's actual
gap). Backlog B-027. No player input; the scene runs unattended once
launched.

`FSharpSceneHost.cs` is the first real use of ADR-0004's accepted
architecture: a generic C# host (`[Export] SceneType`) resolves an F#
`CwClientCore.IClientScene` implementation by name and forwards
`_Ready`/`_Process`/`_Draw`/`_ExitTree`; every non-trivial concern (the
fixed-step scheduler, stepping the simulation, building the depth-sorted
`DrawItem[]`) lives in the new `Core/CommandoWar.Client.Godot.Core.fsproj`
project (`CwClientCore.DemoRenderScene`). The C# host only does cell<->screen
projection arithmetic and issues `Draw*` calls — the one thing ADR-0004
explicitly allows there.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
"$GODOT" --editor --headless --quit --path .          # one-time import
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# windowed: agents cross the ridge/wall/movement-cost patch over ~1 second
"$GODOT" --path . scenes/SnapshotDemo.tscn

# headless smoke: replays DemoScenario tick 1..20, prints each tick's hash,
# asserts the final hash vs the pinned golden
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0xC68F993BC605313C, exit 0

# committed evidence screenshot (windowed; headless has no viewport texture)
"$GODOT" --path . scenes/SnapshotDemo.tscn -- --screenshot <abs-path>.png
```

Committed screenshot: `docs/evidence/task-039-snapshot-rendering.png`.

## Interactive command demo (TASK-040)

`scenes/CommandDemo.tscn` + `src/FSharpSceneHost.cs` is the first scene where
the player, not a canned command log, drives `Simulation.step`: left-click a
friendly agent to select it, left-click a cell to issue a real `Command.
moveTo` (queued for delivery on the next tick, going through the same
Communication/Appraisal pipeline as any other order); hovering a cell with an
agent selected previews the actual route `Pathfinding.find` would take, not a
straight line; right-click deselects; `Space` toggles tactical pause — an
order can be composed and issued while paused, delivered once resumed.
Backlog B-026.

`CommandDemoScene` (`Core/CommandDemoScene.fs`) reuses TASK-039's
`RenderShared` terrain/depth-sort helpers rather than duplicating them, and
draws the selection halo, route preview, and destination marker as ordinary
`DrawItem`s (reusing the existing `Kind = 1` agent-circle rendering — no
`FSharpSceneHost.cs` render-side change). `IClientScene` gained three new
primitives-only members for this task: `OnClick`, `OnHover`,
`OnTogglePause`; `FSharpSceneHost.cs` resolves screen position to a grid cell
(`ScreenToCell`, the exact inverse of `CellToScreen`) and forwards mouse/key
input to them.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
"$GODOT" --editor --headless --quit --path .          # one-time import
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# windowed: click an agent, click a cell, watch it move; Space to pause
"$GODOT" --path . scenes/CommandDemo.tscn

# headless smoke: scripted select-agent-0 + MoveTo(3,0), prints each tick's
# hash, asserts the final hash vs the pinned golden
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck   # MATCH 0xD27E623504262CE9, exit 0

# committed evidence screenshot (scripted selection/preview, since --screenshot
# mode injects no real mouse input)
"$GODOT" --path . scenes/CommandDemo.tscn -- --screenshot <abs-path>.png
```

Committed screenshot: `docs/evidence/task-040-selection-and-preview.png`.

## Placeholder art (TASK-041)

`SnapshotDemo.tscn` and `CommandDemo.tscn` draw real sprite art instead of
flat diamonds/circles: Kenney's "Isometric Miniature Prototype" pack (v2.3,
CC0 — `art/LICENSE-THIRD-PARTY.md` names the exact files and licence).
Backlog B-034. Render-only: no `CommandoWar.Sim`/`CommandoWar.Headless`
change, no `--selfcheck` hash moved.

`DrawItem` gained one new primitive field, `TextureId` (ADR-0004's
primitives-only interop idiom): for a terrain item (`Kind = 0`) it selects
`art/terrain_floor.png` (passable, elevation-tinted), `art/terrain_block.png`
(impassable), or `art/terrain_crate.png` (passable-but-opaque cover) — shape,
not colour alone, now carries that distinction. `FSharpSceneHost.cs` loads
all four textures once (static fields) and keys the terrain draw on
`TextureId`; a real, full-opacity agent (`Kind = 1`, `A >= 0.99`) draws
`art/agent_human.png` cropped to its own figure and tinted per
`DrawItem.R/G/B` (`RenderShared.agentColor`, unchanged); a translucent
`Kind = 1` item (halo, route-preview dot) still draws the original plain
circle — it was never meant to look like an agent.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
"$GODOT" --editor --headless --quit --path .          # re-import after adding art/
dotnet build CommandoWar.Client.Godot.slnx -c Debug    # AND -c Release if exporting/measuring release

# both existing scenes' --selfcheck hashes are unaffected (render-only)
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0xC68F993BC605313C
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0xD27E623504262CE9

"$GODOT" --path . scenes/SnapshotDemo.tscn -- --screenshot <abs-path>.png
"$GODOT" --path . scenes/CommandDemo.tscn -- --screenshot <abs-path>.png
```

Committed screenshots: `docs/evidence/task-041-placeholder-art-snapshot.png`,
`docs/evidence/task-041-placeholder-art-command.png`.

## Order acknowledgement, disposition, and reason (TASK-042)

`CommandDemo.tscn`'s HUD now shows the selected agent's order status —
docs/06 section 8/11's "order acknowledgement and disposition" / "concise
explanations for refusal" requirements. Backlog B-028. `AgentSnapshot`
gained one new values-only field, `Disposition: OrderDisposition option`
(a copy of the already-canonical `AgentState.Disposition`, the
`Destination`/`Progress` precedent); `RenderShared.dispositionText` maps it
to player-facing text (e.g. `"refused: route too exposed (threat: agent
3)"`), deliberately separate from `DiagnosticRender`'s private
developer-facing text (docs/06 draws that distinction explicitly).
`HudText()` appends `order=<text>` for the selected agent only, omitted when
nothing is selected. No `FSharpSceneHost.cs`/`_Draw` change — text only.

Review round 1: at the default 20 Hz sim rate, `DemoScenario`'s short routes
complete in a handful of ticks (well under 200ms wall-clock), so the
`order=` text could flash past unreadably before reverting to `order=no
order` the instant an order was fulfilled. `CommandDemoScene` now holds a
meaningful message on screen for `orderTextHoldSeconds` (1.5s) past when it
would otherwise clear — a client-side, presentation-only fix tracked in
`Update(deltaSeconds)`; a genuinely new message (a fresh order, a
reappraisal flip, or a new selection) still overrides immediately.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# both existing scenes' --selfcheck hashes are unaffected (values-only)
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0xC68F993BC605313C
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0xD27E623504262CE9

# windowed: select an agent, issue an order, watch the HUD's order= field
"$GODOT" --path . scenes/CommandDemo.tscn
```

Committed screenshot: `docs/evidence/task-042-reason-and-disposition.png`
(the scripted `--screenshot` capture point is paused before delivery, so it
shows `order=no order`; the `accepted`/`refused`/`unable` states are proven
by a headless probe instead — see the task file's Outcome section).

## Developer overlay (TASK-043)

`CommandDemo.tscn`'s `F1` key toggles a developer overlay, independent of
tactical pause (`Space`) -- backlog B-029 proper, the full docs/06 section 11
developer-facing list realised over **live** input for the first time (vs.
TASK-029's read-only, corpus-scoped `AppraisalDemoScene`). The overlay is a
fourth renderer of the exact same `Diagnostics.DiagnosticFrame` every other
developer renderer (`DiagnosticRender.Ascii`/`.Svg`/`.Html`) already consumes
-- `CommandDemoScene` calls `Diagnostics.frame`/`.frameOf` after every step and
draws its `Overlay[]` directly; no `CommandoWar.Sim`/`CommandoWar.Headless`
change of any kind.

With the overlay on: every terrain cell gets a small coordinate label; a
this-tick `Reserved` (cyan) or `Obstructed` (red) cell is marked; each
`TacticalKnowledge` contact's last-known cell gets a pale-yellow "ghost"
marker, distinct from the real (always-rendered, no fog-of-war) agent marker
it may now diverge from; the selected agent's exposed-route cells (orange)
and a `[dev]` HUD line (`commitment=`/`suppression=`/`stress=`/`reason=`,
`DiagnosticRender`'s own developer vocabulary, not the player-facing `order=`
text) appear; hovering a cell with an agent selected draws a line-of-sight
ray (`Sight.trace`) from the agent to that cell, green if visible or red up
to the blocking cell (the occluder) if not. The HUD's first line always shows
the random-draw counter (`draws=`) alongside tick and hash, overlay or not.

`DrawItem` gained two new `Kind`s for this: `2` (a line segment, `Cx2`/`Cy2`,
`Radius` as width -- line-of-sight rays, fire lines) and `3` (a text label,
`Text`, `Radius` as font size -- coordinate labels). `IClientScene` gained
`OnToggleDevOverlay` (`DemoRenderScene`, which has no live input to overlay,
implements it as a no-op -- the `OnTogglePause` precedent).

Discipline and trust are not shown: trust has no backing state anywhere in
`CommandoWar.Sim`; discipline exists but has no `Diagnostics.Overlay` case,
and adding one would require a `CommandoWar.Sim`/`CommandoWar.Headless`
change and a golden regeneration -- left as a follow-up, not built here.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# both existing scenes' --selfcheck hashes are unaffected (render-only)
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0xC68F993BC605313C
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0xD27E623504262CE9

# windowed: select an agent, press F1, hover cells to see the LOS ray
"$GODOT" --path . scenes/CommandDemo.tscn

# committed evidence screenshot with the overlay pre-toggled on
"$GODOT" --path . scenes/CommandDemo.tscn -- --dev-overlay --screenshot <abs-path>.png
```

Committed screenshot: `docs/evidence/task-043-developer-overlay.png`.

## Casualties, incapacitation, leadership succession, and squad failure (TASK-045)

Sim-side only (backlog B-031; `CommandoWar.Sim`/`CommandoWar.Headless` gained
`AgentState.Vitals`/`.RecentlyWounded`, `Casualty.fs`, two new diagnostic
overlays, and `Canonical.FormatVersion` bumped 9 -> 10) -- no new client scene
or input. `RenderShared.fs`'s two `DecisionReason` match expressions
(`reasonText`, the player-facing vocabulary, and `devReasonText`, the
developer-overlay one) needed the new `CriticallyWounded` case added to keep
compiling (`"critically wounded"` / `"critically-wounded"`, the existing
hyphenation convention each already follows) -- no other client-side change.

Both existing scenes' `--selfcheck` hashes moved (byte-layout only, from the
`FormatVersion` bump -- neither scene's own scripted drive touches combat, so
every agent stays `Alive` at full health throughout and the tick/event counts
are unchanged):

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0x44B29B73E8F107EF
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0xF1027A36B36BC3DF
```

## Assault, Withdraw, Hold order executors and ammunition (TASK-047)

Sim-side only (backlog B-030 proper; `CommandoWar.Sim` gained three new
`PlayerIntent` cases, a staged `Assaulting` commitment FSM, `AgentState.Ammo`,
and `Canonical.FormatVersion` bumped 10 -> 11) -- no new client scene or
input; issuing `Hold`/`Assault`/`Withdraw` from `CommandDemoScene` stays a
future client task (the TASK-037 `Suppress`-order precedent). `RenderShared.fs`'s
`reasonText`/`devReasonText` (`DecisionReason.InsufficientAmmunition`) and
`devCommitmentText` (`Commitment.Withdrawing`/`.Assaulting`) needed new match
arms to keep compiling.

Both existing scenes' `--selfcheck` hashes moved (byte-layout only, from the
`FormatVersion` bump -- neither scene's own scripted drive issues a new order
type or exhausts an agent's ammo, so the tick/event counts are unchanged):

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0x8E93B48D07AE9CBD
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0xE661187DE95E92E6
```

Both hashes were originally computed by calling `DemoDrive.runFullSequence()` /
`CommandDemoDrive.runScriptedSelfCheck()` directly from `dotnet fsi` against
the built `CommandoWar.Client.Godot.Core.dll` (no Godot install in the
implementing environment). Re-run through the real Godot 4.7.2 editor and
confirmed on acceptance, along with the `AppraisalDemoScene` exposed-approach
pin (`0x194805888CBE240D`, format 11) -- all three `MATCH`, exit 0.

## Client UI for Hold, Assault, and Withdraw orders (TASK-048)

`CommandDemoScene` gains an XCOM-style order-mode HUD icon bar (backlog
B-059): four fixed screen-space icons, bottom-left -- move (arrow), hold
(shield), assault (sword), withdraw (counter-clockwise arrow). Clicking one
arms that order type (clicking the armed icon again disarms back to the
default `MoveTo`); the next left-click on a cell with an agent selected
issues the armed order (`Command.hold`/`.assault`/`.withdraw`, all
already-existing sim-side constructors, the `Command.moveTo` precedent) and
the arming resets to `MoveTo` (an XCOM ability-consumed-on-use idiom).
`IClientScene` gains `OnOrderModeClick(index)`/`OrderMode()` (the `OnClick`/
`OnTogglePause` "primitives only" precedent); the C# host owns the icon
bar's fixed rects, hit-testing, and drawing entirely (HUD chrome, not
world-grid content -- ADR-0004's "Raw input capture | C#" / "Render loop |
C#" rows), checked ahead of every other click so a miss falls through to the
existing world-cell `OnClick` unchanged.

While `Hold` is armed and a cell is hovered with an agent selected, the
scene also draws an outline of the exact `AppraisalConfig.
HoldCoverSearchRadius` (2) Chebyshev square `Appraisal.bestCoverNear` will
search when the order is appraised -- an honest preview of the candidate
region, not a guess at which cell the order will actually resolve to (that
depends on live threat pressure, known only at Appraisal time). Icon art:
Kenney "Board Game Icons" (CC0, `art/LICENSE-THIRD-PARTY.md`), checked live
against kenney.nl, downloaded, and unzipped (not assumed from memory) --
neither existing pack (Isometric Miniature Prototype, Particle Pack) has
command-vocabulary icons.

No `CommandoWar.Sim`/`CommandoWar.Headless` change. `CommandDemoDrive.
runScriptedSelfCheck` extended to also select a second agent, arm `Hold` via
`OnOrderModeClick`, and issue `Hold(2,1)` -- proving the icon-click dispatch
reaches a real, non-`MoveTo` command through the same path a player uses,
not just a direct sim-side call (`SimulationTests` already proves the sim
side). This is a genuine new tick-by-tick trace, so `CommandDemo.tscn`'s
`--selfcheck` hash moved; `SnapshotDemo.tscn` and `AppraisalDemo.tscn` are
unaffected (confirmed unchanged above), since arming a HUD icon has no
effect unless an order is actually issued through it.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0x00D3D471EF7354BC
```

Verified through the real Godot 4.7.2 editor: the re-pinned `CommandDemo.
tscn --selfcheck` MATCH above; `SnapshotDemo.tscn`/`AppraisalDemo.tscn`
`--selfcheck` unchanged (both re-confirmed at their existing pins, see
above); a windowed `--screenshot` (`docs/evidence/task-048-order-mode-hud.png`,
committed) shows the icon bar with `Hold` armed (gold highlight border),
`mode=hold` in the HUD text, and the hover-preview outline.

## Per-agent movement speed (TASK-049, backlog B-058)

`CommandoWar.Sim` gains a real per-agent `AgentState.MoveSpeed` (an authored
unit-type stat, `ScenarioContent.Version` 3 -> 4). `DemoScenario`'s three
agents (the scenario both `SnapshotDemo.tscn` and `CommandDemo.tscn` live-step)
now author a `"trooper"` unit type at half `Agent.MoveSpeedDefault` -- a real
per-agent slowdown, not a client-side `simHz` scale or a shared
`Terrain.BaseMoveCost` bump (both tried and reverted on TASK-046 review) --
resolving Dave's "movement feels twice as fast as I thought it would"
complaint. No `CommandoWar.Sim` behaviour change for any agent at
`Agent.MoveSpeedDefault`: the edge-completion comparison is cross-multiplied
so a default-speed agent's tick-by-tick `Progress`/`Position` trajectory is
byte-for-byte identical to before -- confirmed by all 16 committed corpus/
fixture entries passing unchanged (`cwheadless corpus`, `--regenerate`
byte-identical).

`DemoRenderScene`'s (`SnapshotDemo.tscn`) `--selfcheck` hash moves, since its
tick-20 snapshot now catches the (slower) friendlies still mid-route rather
than past the point they'd have reached at the old pace:

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0xF422ACB8D5A86FF0
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0x00D3D471EF7354BC (unchanged)
```

`CommandDemoScene`'s (`CommandDemo.tscn`) hash is unchanged: its scripted
`MoveTo(3,0)`/`Hold(2,1)` both complete within a handful of ticks even at
half speed, well inside the 20-tick self-check window, so the tick-20 rest
state it produces is identical either way. `AppraisalDemoScene` is
unaffected (it steps the committed `exposed-approach` corpus entry, not
`DemoScenario`).

The `0xF422ACB8D5A86FF0` pin was originally computed by calling
`DemoDrive.runFullSequence()` directly from `dotnet fsi` against the built
`CommandoWar.Client.Godot.Core.dll`, then independently confirmed `MATCH`
through the real Godot 4.7.2 editor (all three scenes, `--headless --path .
<scene> -- --selfcheck`, all exit 0).

## Player-facing fog of war (TASK-051, backlog B-055)

`CommandDemoScene` no longer draws a hostile agent unconditionally at its
true position. `Diagnostics.frameOf`'s `KnownContact` overlay -- built every
tick from `WorldState.TacticalKnowledge` regardless of the `F1` dev-overlay
toggle, the `AgentVitals` precedent TASK-046 already relies on for
player-facing rendering -- is now read for fog-of-war gating too, so **no
`CommandoWar.Sim` change was needed**. A hostile never contacted (or whose
contact has fully expired, `PerceptionConfig.ExpireAfter` ticks after it was
last seen) is hidden entirely; one seen this exact tick renders exactly as
before, at its true live position with full vitals; one known but not
currently visible this tick draws only a hollow "last-known position" ring
(a new `DrawItem.Kind = 5`, `FSharpSceneHost.cs`) plus a small `?` label at
`Contact.LastKnownCell` -- never the true position -- with opacity following
`Contact.Confidence`'s own two-step band drop (`PerceptionConfig.
ConfidenceBandDrop`, the wound-dot-severity-scales-opacity precedent).

The `F1` developer overlay is a deliberate exception: it keeps TASK-043's
original ground-truth behaviour unchanged (every agent renders at its true
position regardless of contact), so its own separate yellow known-contact
marker still visibly diverges from the real position for comparison -- the
reason that overlay exists. Fog of war only gates the normal player view.

Verified with a temporary `dotnet fsi` scratch probe (the TASK-042
precedent) driving `CommandDemoScene` directly outside Godot -- `Ready()`,
then `Update(1/20.0)` once per tick, inspecting `DrawList()`'s returned
`DrawItem[]` -- since `IClientScene` carries no `Godot.*` type. Sent a
friendly toward the hostile at `(11,7)` (to `(3,7)`: within `Perception.
SightRange` (10) but outside `CombatConfig.WeaponRange` (7), so it makes
contact without drawing fire) and back, and confirmed the full sequence: 20
ticks fully hidden (never contacted) -> a live figure at `(11,7)` once in
range -> a ghost ring at `(11,7)`, `alpha=0.90`, once it moves back out of
sight -> `alpha=0.68` exactly `PerceptionConfig.StaleAfter` (20) ticks after
last seen -> the ghost disappears entirely exactly `PerceptionConfig.
ExpireAfter` (60) ticks after last seen, fully hidden again. A second probe
confirmed the `F1` bypass: with the dev overlay toggled on, the same
never-contacted hostile still draws live from tick 1.

Both scenes' `--selfcheck` hashes are unaffected (render-only; confirmed
`MATCH` through the real Godot 4.7.2 editor): `SnapshotDemo.tscn`
`0xF422ACB8D5A86FF0`, `CommandDemo.tscn` `0x00D3D471EF7354BC`,
`AppraisalDemo.tscn` `0x194805888CBE240D`.

## Hover highlight, and a larger fixed scale (TASK-052, backlog B-053/B-054)

Two small, presentation-only client-polish items Dave asked to pick up
together.

**Hover highlight (B-053)**: hovering a friendly agent's own rendered
circle -- without clicking -- now draws a thin hollow ring around it (the
new `Kind = 5` primitive TASK-051 introduced for the fog-of-war ghost
marker, reused here with a light neutral tint instead of the ghost's
hostile-coloured one), so the player can see a click there will select it.
`FSharpSceneHost.cs`'s mouse-motion handler now tries `TryHitAgentCircle`
first, the same fallback `OnClick` already uses, before falling back to
`ScreenToCell` -- otherwise hovering near the top of a visible agent (the
same projection mismatch TASK-040 fixed for clicks) would fail to arm the
highlight.

**Larger fixed scale (B-054)**: the isometric tile pitch doubled again
(`TileW`/`TileH`: `44x22` -> `88x44`, `FSharpSceneHost.cs`), so the play
area now fills roughly 62% of the window's width and 50% of its height
(previously ~34%/~28%). `Origin` moved from `(450,60)` to `(552,110)`: both
recentred for the new scale and, on `Y`, raised enough to clear the HUD
label's worst case (three lines, `F1` on with an agent selected, extending
to roughly `y=62`) with real margin -- the other half of Dave's TASK-043
review complaint ("the corner HUD Label overlaps the tick counter and agent
sprites"). `DrawTerrainTile` already scales terrain tiles directly from
`TileW`/`TileH`, so only the figure/marker radii needed a matching bump:
`CommandDemoScene.fs` gained named `agentRadius` (`10.0f -> 20.0f`) and
`haloRadius` (`17.0f -> 34.0f`) constants, replacing what had been
independent literals -- the fog-of-war ghost ring and the new hover ring
both now size themselves from `agentRadius` too, so all three stay visually
consistent with wherever a real figure actually draws. Confirmed via
`AskUserQuestion`: a larger fixed scale, not interactive zoom/pan (a
materially bigger feature this task deliberately does not add).

Both are purely client-side and change no authoritative state: verified
with a temporary `dotnet fsi` scratch probe (the TASK-042/051 precedent)
confirming the hover ring appears only over a friendly agent (never a
hostile, fogged or not) and disappears the instant the mouse leaves it, and
both scenes' `--selfcheck` hashes reconfirmed `MATCH` unchanged through the
real Godot 4.7.2 editor. Committed screenshot
`docs/evidence/task-052-scale-and-hover.png` (captured with `--dev-overlay`
to show the HUD's worst-case three-line height alongside the new scale).

## Mission summary panel (TASK-063, backlog B-033 narrowed)

The first client presentation of `WorldState.MissionOutcome`
(`AgentState.Extracted`/`.CompletedObjectives`/`.ObjectiveProgress`, all
done since TASK-062, backlog B-032) -- previously invisible outside the
developer-only `Overlay.MissionStatus` diagnostic. `CommandDemoScene` now
auto-pauses and shows a fixed screen-space panel the instant
`MissionOutcome` leaves `InProgress`: an outcome headline (`MISSION
SUCCESS`/`MISSION FAILED`), one line per completed objective, one per
in-progress objective (with its tick count), and one per extracted agent.
`IClientScene` gains `MissionSummaryLines: unit -> string[]` (empty =
nothing to show, the `OnOrderModeClick`/`OrderMode` primitives-only
precedent); `RenderShared.missionSummaryLines` builds the lines directly
from `WorldState` (no `CommandoWar.Sim` change), deriving a plain-language
label for each `Objective` from its own `AreaId`/`TargetId` since the
domain type carries no authored display name. `OnClick`'s order-issuing
guard also checks `MissionOutcome = InProgress` directly, so a manual
un-pause afterward still cannot issue a new order.

`CommandDemoDrive.runScriptedSelfCheck` (20 -> 40 ticks) now sends agent 0
on from its existing `MoveTo(3,0)` to `DemoScenario`'s own sole authored
objective at `(4,4)` (a non-optional `ReachArea` at `"ridge-top"`), reaching
a real `MissionOutcome = Succeeded` through the same click path a player
uses. A new `--screenshot-mission <path>` capture mode was needed for
evidence: the existing `--screenshot` priming deliberately re-pauses right
after issuing an order to hold a pending-route preview, the opposite of
what this needed, so this mode selects agent 0, sends it straight to the
objective, and stays unpaused with a longer capture-frame threshold (400 vs
45) so the walk has time to finish.

Two real, pre-existing bugs were found and fixed while verifying this task,
neither caused by it: `SnapshotDemo.tscn`/`AppraisalDemo.tscn`'s own
`--selfcheck` pins had gone stale since TASK-062's `Canonical.
FormatVersion` 12 -> 13 bump (confirmed by reproducing the identical
mismatch against committed `main` before any change here -- TASK-062
never touched `CommandoWar.Client.Godot`, so never re-ran or re-pinned
these), now re-pinned; and the panel's first draft anchored its
`DrawString` box to the screen centre instead of the panel's own left
edge, rendering every line a half-panel-width to the right of the panel it
was meant to sit inside (visible immediately in the first evidence
screenshot, fixed, re-captured).

**Review round 1 (2026-09-20, live):** Dave tried it in the real editor and
could not trigger the panel -- a real gap, not a fluke: `WorldState.
ObjectiveAreas`/`.ExtractionAreas` were never rendered anywhere in
`CommandDemoScene`, so the objective cell looked like ordinary terrain with
nothing to click toward precisely. Fixed by adding an always-on marker for
each objective/extraction area cell -- a hollow ring (`RenderShared.
cellRing`) plus its `AreaId` as a label, built once in `Ready` from
`WorldState` fields already held (static authored content, the `state.
Terrain`/`.Bounds` precedent for reading `WorldState` directly). No
`IClientScene`/interaction change, purely additive rendering; all three
scenes' `--selfcheck` hashes reconfirmed `MATCH` unchanged. The first
colour choice (gold) turned out visually identical to the selection halo's
own gold ring exactly where it matters most (a selected agent standing on
the objective) -- changed to violet; extraction stays a distinct cyan.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0xED5437A8773C92B2
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0xEC8F01D781AB2122 (re-pinned, stale from TASK-062)
"$GODOT" --headless --path . scenes/AppraisalDemo.tscn -- --selfcheck  # MATCH 0xC382CACA830CCC35 (re-pinned, stale from TASK-062)
```

No `CommandoWar.Sim`/`CommandoWar.Headless` change; `dotnet test` `408/408`
unaffected; `-- corpus` `18/18` unaffected. Committed screenshot
`docs/evidence/task-063-mission-summary.png`. Not exercised live, flagged:
`HoldArea`/`DestroyTarget`/`ExtractAgents`/`AllOf`/`Optional` objective-label
formatting and the extracted-agent line -- `DemoScenario` authors only a
`ReachArea` objective, so no in-scope scripted sequence reaches those
branches; correct by inspection (exhaustively matched), not confirmed live.

### `CommandDemoScene` loads real Bridgehead content (TASK-064, backlog B-035)

`CommandDemoScene`'s `Ready()` (now `Ready(scenarioContentPath)`) loads
`content/scenarios/bridgehead.cwscenario` for real -- `ScenarioFile.parse`
-> `Scenario.validate` -> `World.ofScenario`, the `DemoScenario.fs`
pipeline shape, with a new `bridgeheadSeed` literal (no seed is authored in
a `.cwscenario` file itself) -- instead of the small hand-authored
`DemoScenario` fixture. `FSharpSceneHost.cs` resolves the absolute path via
`ProjectSettings.GlobalizePath("res://")` + `Path.Combine(.., "..", "..",
"content", ...)`, the `AppraisalDemoScene.cs` precedent for reading
`content/` at runtime from a real play scene, not just an editor tool.
`DemoRenderScene` (`SnapshotDemo.tscn`) is unaffected -- it ignores the new
parameter and still loads `DemoScenario` for its own diagnostic-renderer
purpose.

A real, material gap was found and fixed, confirmed with Dave via
`AskUserQuestion`: the order-mode HUD had no way to issue `Suppress` at all
(only `0..3` = Move/Hold/Assault/Withdraw existed, TASK-048) -- one of
docs/07's five required commands, and the mechanism the whole "can
suppressing a machine-gun position change the outcome" product question
rests on. Added index `4`: a procedural crosshair icon (no new Kenney
texture -- docs/07 section 6 permits presentation placeholders until the
integration gate, and this task is that gate), and a new `enemyAt` helper
(the `friendlyAt` precedent) so arming it and clicking a cell resolves to
whichever agent occupies it and issues `Command.suppress` against that
agent id; `Appraisal.appraise` still gates it on the target being a real
known contact exactly as before (TASK-037).

`CommandDemoDrive.runScriptedSelfCheck` was rewritten entirely against real
Bridgehead coordinates (all six friendly agents, exercising all six
formation-slot resolutions against real terrain for the first time,
TASK-059). Investigated live via a series of temporary `dotnet fsi` probes
against the built `CommandoWar.Client.Godot.Core.dll` (removed after use):
sending every agent at once gives the machine-gun team more simultaneous
targets than a lone agent, so real automatic engagement brings it down to
`Dead` while every friendly agent stays `Alive` -- a reproducible,
casualty-free neutralisation of the scenario's central threat through the
real click path.

**Investigated but not closed, flagged for Dave:** a full `Succeeded` run
(destroy + extract) was not reduced to a reliable scripted sequence despite
extensive investigation. Two concrete findings: the `bridge-charge` target
cell (9,5) itself appears covered by a threat beyond the machine gun (most
likely one of the depot riflemen); and a friendly agent's own corpse
permanently blocks its cell (never vacated, and still `friendlyAt`-
selectable, so a click there re-selects it for status view rather than
ever issuing a new order elsewhere) -- a scout who dies exactly on an
objective/target cell can make that objective permanently unreachable. A
full `Failed` run (all six eliminated) is also geometrically capped well
short of six by the bridge's own two-lane chokepoint. See the task file
(`tasks/TASK-064-INTEGRATE-AND-VERIFY-VERTICAL-SLICE.md`) for the complete
docs/07 section 9 criterion-by-criterion record.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0xB99E7F74EA1C3CDE (re-pinned: real Bridgehead content)
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0xEC8F01D781AB2122 (unaffected)
"$GODOT" --headless --path . scenes/AppraisalDemo.tscn -- --selfcheck  # MATCH 0xC382CACA830CCC35 (unaffected)
```

No `CommandoWar.Sim`/`CommandoWar.Headless` change; `dotnet test` `408/408`
unaffected; `-- corpus` `18/18` unaffected; `-- import
content/scenarios/bridgehead.cwscenario` still exits 0. Committed
screenshot `docs/evidence/task-064-bridgehead-integration.png` (real
terrain/depot/agents, the objective/extraction markers, and the new
Suppress icon armed and highlighted in the HUD bar).

**Review round 1 (2026-09-20, live):** Dave reported the game felt static
and out of control -- "no movement from enemies etc, no idea or fire lines
etc" -- despite orders visibly registering and him trying to move across
the bridge. Root cause: a `MoveTo` order for any agent other than a
fireteam leader (a non-zero `AgentState.FormationOffset`, TASK-059) does
not target the literal clicked cell -- `Appraisal.resolveFormationTarget`'s
own bounded-radius fallback can land the real destination well short of
the click (confirmed exactly with a temporary `dotnet fsi` probe: agent 2
ordered to `(8,6)` actually resolves to `(7,6)`, outside the machine gun's
engagement range), with `Disposition = Accepted` throughout and zero
client-side indication this happened. Fixed, client-side only:
`CommandDemoScene.OnHover`'s route preview now resolves through
`Appraisal.resolveFormationTarget` (the identical inputs `Simulation.fs`'s
own appraisal phase uses) whenever `MoveTo` is armed, so the previewed
route shows the real destination before the click; the order-status HUD
text now appends the agent's real `Destination` (`"accepted -> (7,6)"`)
instead of bare `"accepted"`. `dotnet build`/`test`/`corpus` unaffected;
`CommandDemo.tscn --selfcheck` reconfirmed `MATCH 0xB99E7F74EA1C3CDE`
unchanged (presentational only, never touches `stepOnce`).

**Review round 2 (2026-09-20, live):** Dave confirmed round 1's fix
worked, then raised four further points: "you cant visually tell who is a
leader, still no sense of a fight, just blindly moving men to die, no
reaction under fire, no cover, no enemy response either." Fixed three,
client-side only, no `CommandoWar.Sim` change:

- A leader marker: `Casualty.currentLeader state.Agents` (the TASK-045
  succession rule, a pure function over already-held state) drives a
  green ring plus a "LEADER" label that follows whichever agent currently
  holds it, updating on succession for free.
- An under-fire hit-flash: a new `heldHitFlashes` timer (the
  `heldFireLines` precedent) blends a hit target's own figure toward white
  for `hitFlashHoldSeconds` (0.3s) on every landed `FireLine`, fading back
  -- a colour blend on the existing figure, not a new draw item.
- A cover indicator: `Terrain.Cover` had never been rendered anywhere,
  player-facing or dev-overlay, since found unpaintable through the
  tileset back at TASK-060. A short cyan spoke per covered cell/direction
  (built once in `Ready`, the `buildObjectiveMarkerItems` precedent),
  thicker for a higher `Level`.

The fourth point ("no enemy response") is by design, not a bug: enemy
doctrine is backlog B-022, explicitly descoped since TASK-037. A suspected
fifth issue -- a "grey" figure in the recaptured screenshot -- turned out
to be the pre-existing, correctly-functioning selection halo (gold, radius
34) drawn under the selected agent's own opaque figure (radius 20),
confirmed via a temporary `dotnet fsi` `DrawList()` inspection (removed
after use), not a defect.

`dotnet build`/`test`/`corpus` unaffected; all three scenes' `--selfcheck`
reconfirmed `MATCH` unchanged. Screenshot recaptured:
`docs/evidence/task-064-bridgehead-integration.png`.

### Visible stall failure for a permanently blocked order (TASK-065, backlog B-065)

Dave's own live playtest of Bridgehead (after TASK-064's acceptance) found a
genuine sim-side bug, not a client one: "i expected the ai to be more
autonomous, they all seem to just get stuck." A `dotnet fsi` probe against
the real click path confirmed it -- an ordinary six-agent squad-movement
order (not toward the bridge) left two agents permanently frozen from tick
~30 through tick 500, with zero recovery or feedback, `docs/10_RISK_
REGISTER.md` R-010 ("reservation deadlocks") finally materialising for
real. The `CommandoWar.Sim` fix (a new `AgentState.StalledTicks` counter;
`Simulation.navigationAndMovement` abandons a route frozen for
`Simulation.StallAbandonTicks` = 40 consecutive ticks instead of retrying
forever, clearing `Destination`/`Route` and emitting a new
`MovementAbandoned` event) is documented in `docs/04_SIMULATION_SPEC.md`
and `tasks/TASK-065-STALLED-ORDER-VISIBLE-FAILURE.md`; this section covers
only the client's own minimal reaction to it.

"Visible failure, not silent freeze" is the whole point -- an abandoned
order clears `Destination` exactly like a fulfilled one, so the existing
order-status text alone would read as "your soldier arrived", the same
ambiguity this task removes. `CommandDemoScene.stepOnce` now watches
`devFrame.Overlays` for the new `Abandoned` case (the `FireLine ->
heldFireLines` precedent) and records the agent id in a new
`heldAbandonedOrders` timer (`abandonedOrderHoldSeconds` = 3.0, held longer
than a fire flash since the player needs time to read the word, not just
notice a flash); `Update`'s order-status text checks it ahead of the
existing accepted/destination-suffix logic and reads `"accepted -> abandoned
(route blocked)"` instead of bare `"accepted"` for the held duration. The
`F1` developer overlay also gets a distinct orange marker for the new
`Abandoned` overlay (the `Reserved`/`Obstructed` cyan/red precedent), and a
new `content/replays/stalled-order-abandoned` corpus entry (a permanent
single-sided block, run past the 40-tick threshold) proves the whole chain
end to end with a committed ASCII/SVG golden.

No new client UI system: the same order-status text and the same developer
overlay, one more case each. `Canonical.FormatVersion` 13 -> 14
(`AgentState.StalledTicks` added) re-pins every corpus/fixture/diagnostics
golden and all three Godot self-checks byte-layout only -- none of the
existing scripted sequences (including this scene's own `--selfcheck`)
sustains a freeze anywhere near 40 ticks, so none of them newly abandons an
order.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0x047FF3080AD3EBCB (re-pinned: format 14)
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0x49E4CD73C85D1B47 (re-pinned: format 14)
"$GODOT" --headless --path . scenes/AppraisalDemo.tscn -- --selfcheck  # MATCH 0xF0169E93B40D5546 (re-pinned: format 14)
```

`dotnet test` `412/412` (+4: two new `SimulationTests` facts, one `DiagnosticsTests` fact, and one `CorpusTests` theory row); `--
corpus` `19/19` (+1: `stalled-order-abandoned`). Verified against the exact
original repro (a temporary `dotnet fsi` probe against real
`bridgehead.cwscenario` content, removed after use): the same six-agent
squad-movement order now reaches `MovementAbandoned` for five of the six
agents by tick 42, all settled with `Destination = None` and
`StalledTicks = 0`, instead of freezing past tick 500. Not yet confirmed
live in the running editor -- awaiting Dave's own playtest of the
on-screen order-status text.

### `--screenshot-squad`: watch a scripted playthrough (TASK-065/066)

A general-purpose evaluation tool, not evidence for one specific task --
Dave asked directly to be able to watch the real six-agent Bridgehead
advance play out visually rather than only read a headless hash.
`--screenshot-squad <frameCount> <path>` issues the identical
`CommandDemoDrive.runScriptedSelfCheck` click sequence (all six agents
advancing on the bridge) through the real `OnClick`/`OnHover` UI path
instead of `StepTicksHeadless`, then stays unpaused (the
`--screenshot-mission` precedent) so repeated invocations at different
frame counts build a filmstrip:

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot

for f in 30 90 200 400; do
  "$GODOT" --path . scenes/CommandDemo.tscn -- --screenshot-squad $f "/tmp/squad_$f.png"
done
```

Requires a real GPU-backed window (not `--headless`, the same constraint
`--screenshot`/`--screenshot-mission` already have) -- `GetViewport()?.
GetTexture()?.GetImage()` returns nothing without one. This tool found
B-066: watching the filmstrip out to tick 400 showed most of the squad
permanently jammed at the bridge chokepoint from roughly tick 42 onward,
the corpse-blocking gap TASK-064 had already flagged.

### A dead agent's corpse stops blocking movement (TASK-066, backlog B-066)

Sim-side only, no Godot change beyond the tool above: `Simulation.
navigationAndMovement`'s `occupantOf` map now excludes any non-`Alive`
agent (`Casualty.isAlive`), so a corpse's cell reads as free and a live
agent walks through/onto it instead of freezing forever. `Pathfinding.fs`
has no occupancy concept at all and needed no change. Proven directly by
two new `SimulationTests` facts; no existing corpus entry or Godot
`--selfcheck` hash moves (confirmed clean, no re-pin needed -- none of the
19 committed entries combines a death with a subsequent move through that
exact cell).

**Honestly flagged, not smoothed over:** re-running `--screenshot-squad`
against the same real Bridgehead scenario is byte-identical to the pre-fix
filmstrip, because the specific jam Dave watched never involves a death at
all -- it's five of six agents blocking each other while still `Alive`,
pure formation-offset contention at the chokepoint, a different mechanism
this task does not touch. Dave's own further idea from the same
conversation -- multi-select and joint squad orders, with formation
applying to the group order instead of automatically redirecting every
lone individual order -- is parked as backlog B-067, proposed only, not
scoped here per his explicit instruction.

### Formation redirect applies only to group orders (TASK-067, backlog B-067)

The sim-side half of B-067: `ReceivedOrder.AsGroup` (`Canonical.
FormatVersion` 14 -> 15) is now derived once at command intake from the
originating `PlayerCommand`'s own recipient count (`Recipients.Length >
1`) and gates every `Appraisal.resolveFormationTarget` call site --
`Appraisal.appraise` itself, `Simulation.fs`'s two mirrored calls, and
`Diagnostics.formationSlotOverlays`. A single-recipient `MoveTo` order
(everything this client issues today -- no multi-select UI exists yet,
the still-unscoped second half of B-067) now always resolves to the
literal clicked cell, regardless of the recipient's own `FormationOffset`;
`CommandDemoScene.fs`'s `OnHover` preview no longer needs to call
`resolveFormationTarget` at all for that reason, simplifying back to a
direct pathfind against the hovered cell.

`CommandDemoDrive.runScriptedSelfCheck`'s six-agent Bridgehead sequence
relied on the old unconditional redirect to spread agents 0/1/5 and 2/4
(whose original literal target cells coincided) onto distinct real
destinations; with redirect now solo-order-exempt, its six target cells
were updated to the *pre-TASK-067 resolved* cells directly (computed once
via a temporary `dotnet fsi` probe against the real scenario, removed
after use) so the sequence keeps reproducing its own prior outcome --
confirmed byte-for-byte via the same probe re-run with the client's own
`CommandId` numbering, matching the real `--selfcheck` hash exactly. The
outcome ("no friendly casualties, machine gun neutralised") is unchanged;
several agents still end the run mid-route, queued behind each other at
shared target cells -- the same live-agent chokepoint contention TASK-066
already found and parked as B-067's still-unscoped second half.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0x6213D672BC36FDB8
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0x2629A1FE165F94BB
"$GODOT" --headless --path . scenes/AppraisalDemo.tscn -- --selfcheck  # MATCH 0xA1354EB998FC1B95
```

## Pinned versions

| Component | Version |
|---|---|
| Godot | `4.7.2.stable.mono.official` (.NET/Mono build) |
| .NET SDK | `10.0.303` (repo `global.json`) |
| Target framework | `net10.0` (must match `CommandoWar.Sim`; Godot's template default is `net8.0`) |
| `Godot.NET.Sdk` / `GodotSharp` | `4.7.2` (offline feed in `nuget.config`) |

Editor path (not on `PATH` in the dev environment used):
`C:\Users\Dave\Documents\GitHub\Godot_v4.7.2-stable_mono_win64\`

## Layout

```
project.godot              Godot project (run/main_scene = AppraisalDemo.tscn)
Main.tscn                   root: MainNode + the two content scenes (TASK-004 spike)
scenes/Greybox.tscn         authored 32x32 isometric greybox + 6 spawn markers + objective
scenes/GreyboxInvalid.tscn  deliberately broken content for the failure path
scenes/AppraisalDemo.tscn   TASK-029: read-only corpus-scoped appraisal-divergence demo
scenes/SnapshotDemo.tscn    TASK-039: live snapshot rendering + isometric depth ordering
scenes/CommandDemo.tscn     TASK-040: selection, input mapping, tactical pause, command preview
src/SimFacade.cs            thin C# facade over CommandoWar.Sim  (NO Godot types, TASK-004 spike)
src/SpikeContent.cs         framework-neutral content DTOs + validation  (NO Godot types, TASK-004 spike)
src/GreyboxScene.cs         reads the authored scene -> RawContent  (import boundary, TASK-004 spike)
src/SpikeMarker.cs          typed marker node (TASK-004 spike)
src/MainNode.cs             fixed-step scheduling, input, isometric render, overlay (TASK-004 disposable spike)
src/AppraisalDemoScene.cs   TASK-029's thin renderer (a scoped ADR-0004 deviation, see the file header)
src/FSharpSceneHost.cs      TASK-039/040: the generic ADR-0004 C# host over Core/ -- every future
                            production scene's `.tscn` uses this, not a new per-scene C# file
Core/                       TASK-039/040: the ADR-0004 F# client-core library (CwClientCore.*)
Core/RenderShared.fs        TASK-040: terrain-item + depth-sort helpers shared by every render scene
Core/CommandDemoScene.fs    TASK-040: live selection, input-mapped MoveTo, route preview, pause;
                            TASK-043: F1 developer overlay over live Diagnostics.DiagnosticFrame
art/                        TASK-041: Kenney CC0 placeholder terrain/agent textures + licence file
```

In the TASK-004 spike, only `SimFacade.cs` calls `CommandoWar.Sim`, and
everything it passes across the C#/F# boundary is a primitive, an array, or
an F# value type. Since TASK-039, `Core/` (the ADR-0004 F# client-core
library) also references `CommandoWar.Sim`/`CommandoWar.Headless` directly —
expected under ADR-0002 ("the client references the simulation"); no C# file
outside `FSharpSceneHost.cs` (which only resolves `Core`'s `IClientScene` by
name) touches either.

## Build

```
dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug
```

The Godot editor also builds it on open / on run. Output lands in
`.godot/mono/temp/bin/<config>/`.

## Run outside the editor

```
# windowed game (not the editor UI)
<godot> --path src/CommandoWar.Client.Godot

# headless hash cross-check against CommandoWar.Headless
<godot> --headless --path src/CommandoWar.Client.Godot -- --selfcheck --expect 0x838D3AE7DBFB735D

# deliberately invalid content -> exit 2, actionable errors
<godot> --headless --path src/CommandoWar.Client.Godot -- --selfcheck --invalid

# self-captured evidence screenshot (slows the scheduler to 6 Hz for a mid-move still)
<godot> --path src/CommandoWar.Client.Godot -- --screenshot <abs-path>.png
```

`--selfcheck` runs the shared fixture (`content/fixtures/SPIKE-FIXTURE.md`):
32x32, seed 20260902, agent 3 -> (20,14) at tick 1, 40 ticks. It prints one
`tick=N hash=0x...` line per tick; the sequence is identical to
`dotnet run --project src/CommandoWar.Headless -- fixture`.

## In-editor controls

- left-click an agent to select, left-click a cell to issue `MoveTo`
- right-click to deselect
- `1` / `2` change the authoritative sim rate; `Space` pauses
- `S` writes `user://godot-run.cwlog` (feed it to `cwheadless compare`)

## Packaging

`--export-release` / `--export-pack` presets work, but a self-contained
executable needs the Godot **export templates** for `4.7.2.stable.mono`, which
are not installed (`%APPDATA%\Godot\export_templates\4.7.2.stable.mono\` is
empty). See the TASK-004 ledger entry for the exact error and the unblock path.
