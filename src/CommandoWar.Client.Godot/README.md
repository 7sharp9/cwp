# CommandoWar Godot .NET client

This directory is the production Godot client (ADR-0001 selected Godot;
ADR-0004 defines the C#/F# boundary). `src/FSharpSceneHost.cs` is the one
generic C# host: it resolves an F# `CwClientCore.IClientScene` implementation
from `Core/` (the F# client-core library) by the name set in its
`[Export] SceneType` and forwards the Godot callbacks to it. The client references `CommandoWar.Sim` and
`CommandoWar.Headless`; neither references it. `run/main_scene` is
`scenes/CommandDemo.tscn`. The project builds from its own
`CommandoWar.Client.Godot.slnx` and is deliberately **not** in the repository's
`CommandoWar.slnx`.

The TASK-004 framework-spike host stays in the tree as ADR-0001 evidence, not
as production code: `Main.tscn`, `scenes/Greybox.tscn`,
`scenes/GreyboxInvalid.tscn`, `src/MainNode.cs`, `src/SimFacade.cs`,
`src/SpikeContent.cs`, `src/GreyboxScene.cs` and `src/SpikeMarker.cs`. No
production file depends on them (other files mention `MainNode.cs` only in
comments), so they can be removed without touching `CommandoWar.Sim`,
`CommandoWar.Sim.Tests`, `CommandoWar.Headless`, `content/` or the shared
fixture.

## Appraisal-divergence demo

`scenes/AppraisalDemo.tscn` + `src/AppraisalDemoScene.cs` is a **read-only,
corpus-scoped** renderer of the Godot `DiagnosticFrame` (backlog B-029, pulled
forward as P3 decision-support; TASK-029). It loads the committed
`content/replays/exposed-approach` corpus entry, builds its per-tick
`DiagnosticFrame` sequence via `DiagnosticRender.runFrames`, and renders the
state plus the `KnownContact` / `PlannedPath` / `OrderAppraisal` overlays with
a 13-frame tick slider and a tick / hash / format / draws HUD, mirroring the
`DiagnosticRender.Svg` colours and glyphs. The divergence lands on tick 1:
agent 0 (`Discipline 1`) `Refused RouteTooExposed threat-agent-2`, agent 1
(`Discipline 6`) `Accepted`.

All non-trivial logic is in the framework-neutral F# helper
`src/CommandoWar.Headless/AppraisalDemo.fs`; the C# scene is a thin renderer.
This is a scoped deviation from ADR-0004's rule that client logic lives in F#
and is not a precedent for other scenes. The project holds one
`ProjectReference` to `CommandoWar.Headless` for it.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
"$GODOT" --editor --headless --quit --path .          # one-time import
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# windowed: drag the slider or press Left / Right to scrub ticks 0..12
"$GODOT" --path . scenes/AppraisalDemo.tscn

# headless smoke: prints the tick-1 dispositions + hash, asserts vs the golden
"$GODOT" --headless --path . scenes/AppraisalDemo.tscn -- --selfcheck   # MATCH 0xA1354EB998FC1B95, exit 0

# committed evidence screenshot (windowed; headless has no viewport texture)
"$GODOT" --path . --resolution 1000x620 scenes/AppraisalDemo.tscn -- --screenshot <abs-path>.png
```

Committed screenshot: `docs/evidence/task-029-appraisal-demo.png`.

## Live snapshot-rendering demo

`scenes/SnapshotDemo.tscn` (`CwClientCore.DemoRenderScene`) live-steps the
terrain-demo scenario (`src/CommandoWar.Headless/DemoScenario.fs`, an elevation
ridge, an impassable block, a movement-cost patch, an opaque wall, and
directional cover) via `Simulation.step` and renders it isometrically with one
unified, depth-sorted draw list: terrain is interleaved with agents by screen
depth, not drawn as "all terrain then all agents" (TASK-039, backlog B-027).
There is no player input; the scene runs unattended once launched.

Every non-trivial concern (the fixed-step scheduler, stepping the simulation,
building the depth-sorted `DrawItem[]`) lives in
`Core/CommandoWar.Client.Godot.Core.fsproj`. The C# host only does cell<->screen
projection arithmetic and issues `Draw*` calls, the one thing ADR-0004
explicitly allows there.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
"$GODOT" --editor --headless --quit --path .          # one-time import
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# windowed: the scripted 20-tick run plays out in about one second at 20 Hz
"$GODOT" --path . scenes/SnapshotDemo.tscn

# headless smoke: replays DemoScenario tick 1..20, prints each tick's hash,
# asserts the final hash vs the pinned golden
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0x6213D672BC36FDB8, exit 0

# committed evidence screenshot (windowed; headless has no viewport texture)
"$GODOT" --path . scenes/SnapshotDemo.tscn -- --screenshot <abs-path>.png
```

Committed screenshot: `docs/evidence/task-039-snapshot-rendering.png`.

## Interactive command demo

`scenes/CommandDemo.tscn` (`CwClientCore.CommandDemoScene`,
`Core/CommandDemoScene.fs`) is the scene where the player, not a canned command
log, drives `Simulation.step` (TASK-040, backlog B-026). It loads the Bridgehead
scenario. Left-click a friendly agent to select it, left-click a cell to issue
a real move order (`Command.moveToMany`, identical to `Command.moveTo` for one
agent; queued for delivery on the next tick, going through the same
Communication/Appraisal pipeline as any other order); hovering a cell
with an agent selected previews the actual route `Pathfinding.find` would take,
not a straight line; right-click deselects; `Space` toggles tactical pause, and
an order composed while paused is delivered once the game resumes. Multi-select,
the order-mode icons and the other order types are described in the sections
below.

`CommandDemoScene` reuses the `RenderShared` terrain and depth-sort helpers
rather than duplicating them, and draws the selection halo, route preview and
destination marker as ordinary `DrawItem`s (the translucent `Kind = 1` circle).
`IClientScene` carries the input as primitives-only members (`OnClick`,
`OnHover`, `OnTogglePause`); `FSharpSceneHost.cs` resolves a screen position to
a grid cell (`ScreenToCell`, the exact inverse of `CellToScreen`) and forwards
mouse and key input to them.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
"$GODOT" --editor --headless --quit --path .          # one-time import
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# windowed: click an agent, click a cell, watch it move; Space to pause
"$GODOT" --path . scenes/CommandDemo.tscn

# headless smoke: scripted selection and MoveTo for all six friendly agents
# (90 ticks; no friendly casualties, machine-gun team neutralised), prints each
# tick's hash, asserts the final hash vs the pinned golden
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck   # MATCH 0x300BB18492CEB355, exit 0

# committed evidence screenshot (scripted selection/preview, since --screenshot
# mode injects no real mouse input)
"$GODOT" --path . scenes/CommandDemo.tscn -- --screenshot <abs-path>.png
```

Committed screenshot: `docs/evidence/task-040-selection-and-preview.png`. The
committed screenshots in this section and the next three predate the Bridgehead
content and the larger tile scale: they show the earlier tile size and, for
`CommandDemo.tscn`, the earlier `DemoScenario` map instead of Bridgehead.

## Placeholder art

`SnapshotDemo.tscn` and `CommandDemo.tscn` draw sprite art, not flat
diamonds and circles: Kenney's "Isometric Miniature Prototype" pack (v2.3, CC0;
`art/LICENSE-THIRD-PARTY.md` names the exact files and licence; the effect
sprites and order-mode icons come from two further Kenney packs listed in the
same file) (TASK-041, backlog B-034). The art is render-only: it changes
neither `CommandoWar.Sim` nor `CommandoWar.Headless` and moves no `--selfcheck`
hash.

`DrawItem.TextureId` is a primitive field (ADR-0004's primitives-only interop
idiom). For a terrain item (`Kind = 0`) it selects `art/terrain_floor.png`
(passable, elevation-tinted), `art/terrain_block.png` (impassable), or
`art/terrain_crate.png` (passable-but-opaque cover), so shape, not colour
alone, carries that distinction. A real, full-opacity agent (`Kind = 1`,
`A >= 0.99`) draws one of the eight `art/agent_human_facing0..7.png` idle
figures chosen by `TextureId` (the facing bin), or a run-cycle frame
`art/agent_human_run{dir}_{frame}.png` when `Cx2` is a frame index of 0 or
more; both are tinted per `DrawItem.R/G/B` (`RenderShared.agentColor`). A
translucent `Kind = 1` item (halo, route-preview dot) draws a plain circle,
since it is a marker and not an agent. `FSharpSceneHost.cs` loads every texture
once (static fields) and keys the draw on `TextureId`.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
"$GODOT" --editor --headless --quit --path .          # re-import after adding art/
dotnet build CommandoWar.Client.Godot.slnx -c Debug    # AND -c Release if exporting/measuring release

# rendering does not affect the --selfcheck hashes
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0x6213D672BC36FDB8
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0x300BB18492CEB355

"$GODOT" --path . scenes/SnapshotDemo.tscn -- --screenshot <abs-path>.png
"$GODOT" --path . scenes/CommandDemo.tscn -- --screenshot <abs-path>.png
```

Committed screenshots: `docs/evidence/task-041-placeholder-art-snapshot.png`,
`docs/evidence/task-041-placeholder-art-command.png`.

## Order acknowledgement, disposition, and reason

`CommandDemo.tscn`'s HUD shows the selected agent's order status, meeting
docs/06 section 8/11's "order acknowledgement and disposition" and "concise
explanations for refusal" requirements (TASK-042, backlog B-028).
`AgentSnapshot.Disposition: OrderDisposition option` is a values-only copy of
the already-canonical `AgentState.Disposition` (the `Destination`/`Progress`
precedent). `RenderShared.dispositionText` maps it to player-facing text (e.g.
`"refused: route too exposed (threat: agent 3)"`), deliberately separate from
`DiagnosticRender`'s private developer-facing text (docs/06 draws that
distinction explicitly). `HudText()` appends `order=<text>` while exactly one
agent is selected and omits it otherwise. This is text only: `_Draw` is
unaffected.

At the default 20 Hz sim rate a short route completes in well under 200 ms of
wall-clock time, so the `order=` text would flash past before reverting to
`order=no order` the instant an order was fulfilled. `CommandDemoScene`
therefore holds a meaningful message on screen for `orderTextHoldSeconds`
(1.5 s) past when it would otherwise clear. This is a client-side,
presentation-only mechanism tracked in `Update(deltaSeconds)`; a genuinely new
message (a fresh order, a reappraisal flip, or a new selection) overrides it
immediately.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# the --selfcheck hashes are unaffected by the HUD text (values-only)
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0x6213D672BC36FDB8
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0x300BB18492CEB355

# windowed: select an agent, issue an order, watch the HUD's order= field
"$GODOT" --path . scenes/CommandDemo.tscn
```

Committed screenshot: `docs/evidence/task-042-reason-and-disposition.png`
(the scripted `--screenshot` capture point is paused before delivery, so it
shows `order=no order`; the `accepted`/`refused`/`unable` states are covered by
a headless probe described in the Outcome section of
`tasks/TASK-042-REASON-AND-DISPOSITION-UI.md`).

## Developer overlay

`CommandDemo.tscn`'s `F1` key toggles a developer overlay, independent of
tactical pause (`Space`) (TASK-043, backlog B-029 proper). It realises the full
docs/06 section 11 developer-facing list over **live** input, where
`AppraisalDemoScene` is read-only and corpus-scoped. The overlay is a fourth
renderer of the exact same `Diagnostics.DiagnosticFrame` that
`DiagnosticRender.Ascii`/`.Svg`/`.Html` consume: `CommandDemoScene` calls
`Diagnostics.frame`/`.frameOf` after every step and draws its `Overlay[]`
directly, with no change to `CommandoWar.Sim` or `CommandoWar.Headless`.

With the overlay on:

- every terrain cell gets a small coordinate label;
- a this-tick `Reserved` (cyan), `Obstructed` (red) or `Abandoned` (orange)
  cell is marked;
- each `TacticalKnowledge` contact's last-known cell gets a pale-yellow "ghost"
  marker. The overlay draws every agent at its true position regardless of
  contact (the player view hides uncontacted hostiles), so the ghost marker
  visibly diverges from the real agent marker once knowledge goes stale;
- the selected agents' exposed-route cells are marked in orange, and fire lines
  are drawn (green for a hit, grey for a miss);
- a `[legend]` HUD line explains the colours, and a single selected agent also
  gets a `[dev]` HUD line (`commitment=`/`suppression=`/`stress=`/`reason=`,
  plus `exposed=`/`ammo=`) in `DiagnosticRender`'s developer vocabulary, not the
  player-facing `order=` text (with no agent or several selected, the `[dev]`
  line says so instead);
- hovering a cell with exactly one agent selected draws a line-of-sight ray
  (`Sight.trace`) from the agent to that cell, green if visible, or red up to
  the blocking cell (the occluder) if not.

The HUD's first line always shows the random-draw counter (`draws`) alongside
tick and hash, overlay or not.

The overlay uses two `DrawItem` kinds: `Kind = 2` (a line segment from `Cx`,`Cy`
to `Cx2`,`Cy2`, with `Radius` as width; line-of-sight rays and fire lines) and
`Kind = 3` (a text label, `Text`, with `Radius` as font size; coordinate
labels). `IClientScene.OnToggleDevOverlay` toggles the overlay;
`DemoRenderScene`, which has no live input to overlay, implements it as a no-op
(the `OnTogglePause` precedent).

Discipline and trust are not shown. Trust has no backing state anywhere in
`CommandoWar.Sim`; discipline exists but has no `Diagnostics.Overlay` case, and
adding one would require a `CommandoWar.Sim`/`CommandoWar.Headless` change and a
golden regeneration, so it is a follow-up and not part of the overlay.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

# the overlay is render-only: the --selfcheck hashes are unaffected
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0x6213D672BC36FDB8
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0x300BB18492CEB355

# windowed: select an agent, press F1, hover cells to see the LOS ray
"$GODOT" --path . scenes/CommandDemo.tscn

# committed evidence screenshot with the overlay pre-toggled on
"$GODOT" --path . scenes/CommandDemo.tscn -- --dev-overlay --screenshot <abs-path>.png
```

Committed screenshot: `docs/evidence/task-043-developer-overlay.png`.

## Casualties, incapacitation, leadership succession, and squad failure (TASK-045)

Sim-side only (backlog B-031): `CommandoWar.Sim` and `CommandoWar.Headless`
provide `AgentState.Vitals` and `.RecentlyWounded`, `Casualty.fs`, and two
diagnostic overlays (`AgentVitals`, `SquadLeadership`). There is no
casualty-specific client scene or input.
`RenderShared.fs` has two `DecisionReason` match expressions, `reasonText`
(the player-facing vocabulary) and `devReasonText` (the developer overlay's).
Both cover `CriticallyWounded`, as `"critically wounded"` and
`"critically-wounded"` respectively, following the hyphenation convention
each already uses.

## Assault, Withdraw, Hold order executors and ammunition (TASK-047)

Sim-side only (backlog B-030): `CommandoWar.Sim` provides the `Hold`,
`Assault`, and `Withdraw` `PlayerIntent` cases, a staged `Assaulting`
commitment state machine, and `AgentState.Ammo`. The client order UI for
these intents is described in the next section. `RenderShared.fs` maps
`DecisionReason.InsufficientAmmunition` in `reasonText` and `devReasonText`
(`"out of ammunition"` / `"insufficient-ammunition"`), and
`Commitment.Withdrawing` / `.Assaulting` in `devCommitmentText`.

## Client UI for Hold, Assault, and Withdraw orders (TASK-048)

`CommandDemoScene` has an XCOM-style order-mode HUD icon bar (backlog B-059)
of fixed screen-space icons at the bottom left: move (arrow), hold (shield),
assault (sword), withdraw (counter-clockwise arrow), and suppress (a
crosshair drawn procedurally, TASK-064). Clicking an icon arms that
order type; clicking the armed icon again disarms back to the default
`MoveTo`. The next left-click on a cell with an agent selected issues the
armed order (`Command.hold`/`.assault`/`.withdraw`, the sim-side constructors,
as `Command.moveTo` is) and the arming resets to `MoveTo`. `Suppress` instead
targets the agent occupying the clicked cell (`Command.suppress` takes an
`AgentId`); with no agent there it issues nothing and stays armed.

`IClientScene` exposes `OnOrderModeClick(index)` and `OrderMode()` (index
`0` = `MoveTo`, `1` = `Hold`, `2` = `Assault`, `3` = `Withdraw`,
`4` = `Suppress`), primitives only, like `OnClick` and `OnTogglePause`. The
C# host owns the icon bar's fixed rects, hit-testing, and drawing entirely
(HUD chrome, not world-grid content; ADR-0004's "Raw input capture | C#" and
"Render loop | C#" rows). It checks the bar ahead of every other click, so a
miss falls through to the world-cell `OnClick`.

While `Hold` is armed and a cell is hovered with an agent selected, the scene
draws an outline of the exact `AppraisalConfig.HoldCoverSearchRadius` (2)
Chebyshev square that `Appraisal.bestCoverNear` searches when the order is
appraised. It previews the candidate region only; the cell the order
resolves to depends on live threat pressure, known only at appraisal time.

Icon art is Kenney "Board Game Icons" (CC0, `art/LICENSE-THIRD-PARTY.md`);
neither of the other packs (Isometric Miniature Prototype, Particle Pack)
has command-vocabulary icons. Arming an icon has no effect on the simulation
until an order is issued through it.

The committed screenshot `docs/evidence/task-048-order-mode-hud.png` was
captured with `--screenshot` before the suppress icon existed, so it shows
four icons: `Hold` armed (gold highlight border), `mode=hold` in the HUD
text, and the hover-preview outline.

## Per-agent movement speed (TASK-049, backlog B-058)

`CommandoWar.Sim` has a per-agent `AgentState.MoveSpeed`, an authored
unit-type stat (`ScenarioContent.Version` is 7). `DemoScenario`'s three
agents (the scenario `SnapshotDemo.tscn` live-steps) author a `"trooper"`
unit type at half `Agent.MoveSpeedDefault`. The slowdown is per-agent
simulation state, not a client-side `simHz` scale or a shared
`Terrain.BaseMoveCost` change. The edge-completion comparison is
cross-multiplied, so an agent at `Agent.MoveSpeedDefault` has the same
tick-by-tick `Progress`/`Position` trajectory as the pre-`MoveSpeed`
behaviour; the committed corpus and fixture entries pass unchanged
(`cwheadless corpus`, `--regenerate` byte-identical).

`DemoRenderScene` (`SnapshotDemo.tscn`) catches its slower friendlies
mid-route in its tick-20 snapshot. `AppraisalDemoScene` is independent of
`DemoScenario`: it steps the committed `exposed-approach` corpus entry.

The current `--selfcheck` pins of the three scenes. Each is sensitive to
`Canonical.FormatVersion` (15) and to the content its scene steps, so a
change to either re-pins it:

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0x6213D672BC36FDB8
"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0x300BB18492CEB355
"$GODOT" --headless --path . scenes/AppraisalDemo.tscn -- --selfcheck  # MATCH 0xA1354EB998FC1B95
```

## Player-facing fog of war (TASK-051, backlog B-055)

`CommandDemoScene` does not draw a hostile agent at its true position
unconditionally. `Diagnostics.frameOf`'s `KnownContact` overlay is built
every tick from `WorldState.TacticalKnowledge`, regardless of the `F1`
dev-overlay toggle (the same `AgentVitals` precedent the player-facing
casualty markers rely on), and the scene reads it for fog-of-war gating, so
fog of war needs no `CommandoWar.Sim` support. A hostile with no contact, or
whose contact has fully expired (`PerceptionConfig.ExpireAfter` ticks after
it was last seen), is hidden entirely. A hostile seen this exact tick
renders at its true live position with full vitals. A hostile known but not
visible this tick draws only a hollow last-known-position ring (a
`DrawItem.Kind = 5` item, `FSharpSceneHost.cs`) and a small `?` label at
`Contact.LastKnownCell`, never the true position. The ring's opacity follows
`Contact.Confidence`, which drops by one band
(`PerceptionConfig.ConfidenceBandDrop`) once the contact goes stale, scaling
as `0.9 * Confidence / ConfidenceFull` (the same idea as wound-dot severity
scaling opacity): `alpha = 0.90` once the contact leaves sight, `0.68` exactly
`PerceptionConfig.StaleAfter` (20) ticks after last seen, and gone exactly
`PerceptionConfig.ExpireAfter` (60) ticks after last seen.

The `F1` developer overlay is a deliberate exception: it keeps ground-truth
rendering (every agent at its true position regardless of contact), so its
own separate yellow known-contact marker visibly diverges from the real
position for comparison, which is the reason that overlay exists. Fog of war
gates only the normal player view.

Fog of war is render-only and does not affect any `--selfcheck` hash.

## Hover highlight, and a larger fixed scale (TASK-052, backlog B-053/B-054)

Two presentation-only client items, neither of which changes authoritative
state.

**Hover highlight (B-053)**: hovering a friendly agent's rendered circle,
without clicking, draws a thin hollow ring around it (the `Kind = 5`
primitive the fog-of-war ghost marker uses, here with a light neutral tint),
so the player can see that a click will select it. It appears only over a
friendly agent (never a hostile, fogged or not) and disappears when the
mouse leaves it. `FSharpSceneHost.cs`'s mouse-motion handler tries
`TryHitAgentCircle` first, the same fallback `OnClick` uses, and only then
`ScreenToCell`; otherwise hovering near the top of a visible agent (the
projection mismatch that also affected clicks) would fail to arm the
highlight.

**Larger fixed scale (B-054)**: the isometric tile pitch is `TileW`/`TileH`
= `88x44` (`FSharpSceneHost.cs`), so the 12x8 `DemoScenario` grid spans
880x440 px, about 69% of the 1280x800 window's width and 55% of its height
(the 18x12 Bridgehead grid spans 1320x660 px). `Origin` is `(552,110)`:
centred for the 12x8 grid at that scale and, on `Y`, raised enough to clear
the HUD label's worst case (three lines, `F1` on with an agent selected,
extending to roughly `y=62`) so the label does not overlap the tick counter or
agent sprites. `DrawTerrainTile` scales terrain tiles directly from
`TileW`/`TileH`. The figure and marker radii are the named constants
`agentRadius` (`20.0f`) and `haloRadius` (`34.0f`) in `CommandDemoScene.fs`;
the fog-of-war ghost ring and the hover ring both size themselves from
`agentRadius`, so all three stay consistent with where a real figure draws.
The scale is fixed; interactive zoom and pan are a materially larger feature
and are not provided.

The committed screenshot `docs/evidence/task-052-scale-and-hover.png` was
captured with `--dev-overlay` to show the HUD's worst-case three-line height
alongside the scale.

## Mission summary panel (TASK-063, backlog B-033 narrowed)

The client presentation of `WorldState.MissionOutcome`, together with
`AgentState.Extracted` and `WorldState.CompletedObjectives`/
`.ObjectiveProgress` (backlog B-032). `CommandDemoScene` auto-pauses and shows
a fixed screen-space panel the instant `MissionOutcome` leaves `InProgress`:
an outcome headline (`MISSION SUCCESS` / `MISSION FAILED`), one line per
completed objective, one per in-progress objective (with its tick count), and
one per extracted agent.
`IClientScene` exposes `MissionSummaryLines: unit -> string[]` (empty means
nothing to show, the `OnOrderModeClick`/`OrderMode` primitives-only
precedent). `RenderShared.missionSummaryLines` builds the lines directly from
`WorldState` (no `CommandoWar.Sim` change), deriving a plain-language label
for each `Objective` from its own `AreaId`/`TargetId`, since the domain type
carries no authored display name. `OnClick`'s order-issuing guard also checks
`MissionOutcome = InProgress` directly, so a manual un-pause afterwards still
cannot issue a new order.

`WorldState.ObjectiveAreas` and `.ExtractionAreas` are drawn as always-on
markers: a hollow ring (`RenderShared.cellRing`) plus the area's `AreaId` as
a label, built once in `Ready` from the `WorldState` fields (static authored
content, like `state.Terrain` and `.Bounds`). Objective areas are violet and
extraction areas cyan; gold was avoided because it is indistinguishable from
the selection halo's ring when a selected agent stands on the objective.

`--screenshot-mission <path>` is a separate capture mode from `--screenshot`,
which re-pauses after priming to hold a pending-route preview. This mode
selects agent 0 at `(3,5)`, sends it to `(4,4)`, and stays unpaused, with a
longer capture-frame threshold (400 versus 45). On the real Bridgehead
content `(4,4)` is an optional objective that does not end the mission, so
this mode does not reach the panel there. The committed screenshot
`docs/evidence/task-063-mission-summary.png` was captured against
`DemoScenario`, whose sole authored objective is a non-optional `ReachArea`
at `"ridge-top"` (`(4,4)`). The scripted `CommandDemo.tscn --selfcheck`
sequence does not reach a terminal `MissionOutcome`, so no self-check hash
covers the panel.

Not exercised live: the label formatting for `HoldArea`, `DestroyTarget`,
`ExtractAgents`, `AllOf`, and `Optional` objectives, and the extracted-agent
line. The panel was verified against `DemoScenario`, which authors only a
`ReachArea` objective; these branches are exhaustively matched and correct by
inspection. Bridgehead authors `destroy` and `extract` objectives, so they are
reachable there, but no live run of the panel on them is recorded.

### `CommandDemoScene` loads real Bridgehead content (TASK-064, backlog B-035)

`CommandDemoScene`'s `Ready(scenarioContentPath)` loads
`content/scenarios/bridgehead.cwscenario` through `ScenarioFile.parse`,
`Scenario.validate` and `World.ofScenario`, the `DemoScenario.fs` pipeline
shape, with a `bridgeheadSeed` literal (a `.cwscenario` file authors no
seed). `FSharpSceneHost.ResolveContentPath` finds `content/` beside an
exported executable and otherwise resolves it from
`ProjectSettings.GlobalizePath("res://")` + `Path.Combine(.., "..", "..",
"content", ...)`, the `AppraisalDemoScene.cs` precedent for reading
`content/` at runtime from a real play scene, not just an editor tool.
`DemoRenderScene` (`SnapshotDemo.tscn`) ignores the parameter and still
loads `DemoScenario` for its own diagnostic-renderer purpose.

The order-mode HUD offers five orders by index: `0..3` = Move/Hold/Assault/
Withdraw (TASK-048) and `4` = Suppress, drawn as a procedural crosshair icon
(no Kenney texture; docs/07 section 6 permits presentation placeholders
until the integration gate). With Suppress armed, clicking a cell resolves
the agent occupying it through the `enemyAt` helper (the `friendlyAt`
precedent) and issues `Command.suppress` against that agent id;
`Appraisal.appraise` still gates it on the target being a real known
contact (TASK-037).

`CommandDemoDrive.runScriptedSelfCheck` runs against real Bridgehead
coordinates: all six friendly agents are selected at their authored cells
and ordered, through the real `OnClick`/`OnHover` path, to cells on the bridge
deck (agents 0 to 5 to `(8,5)`, `(8,5)`, `(8,6)`, `(9,6)`, `(8,6)`,
`(8,5)`), then steps 90 ticks. Sending every agent at once gives the machine-gun
team (`AgentId 100`) more simultaneous targets than a lone agent, so real
automatic engagement brings it down to `Dead` while every friendly agent
stays `Alive`, and the optional `reach observation` objective (`ObjectiveId
1`) completes because agent 0's route passes through `(4,4)`. The sequence
does not reach a mission outcome. The target cells are the cells the
formation-slot resolution (TASK-059) produced when this sequence was
written, so the six-agent outcome is preserved now that solo orders no
longer redirect (see TASK-067 below). Full `Succeeded` and `Failed` runs on
Bridgehead are the `bridgehead-succeeded` and `bridgehead-failed` corpus
entries in `content/replays/` (B-077). The criterion-by-criterion docs/07
section 9 record is in
`tasks/TASK-064-INTEGRATE-AND-VERIFY-VERTICAL-SLICE.md`.

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot
dotnet build CommandoWar.Client.Godot.slnx -c Debug

"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck    # MATCH 0x300BB18492CEB355
"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck   # MATCH 0x6213D672BC36FDB8
"$GODOT" --headless --path . scenes/AppraisalDemo.tscn -- --selfcheck  # MATCH 0xA1354EB998FC1B95
```

`cwheadless import content/scenarios/bridgehead.cwscenario` exits 0.
Committed screenshot `docs/evidence/task-064-bridgehead-integration.png`
(real terrain, depot and agents, the objective and extraction markers, the
leader marker and cover spokes, and the five-icon order-mode bar including
the Suppress crosshair; the Hold icon is the armed one in the capture).

The scene's presentation, all non-authoritative and never touching
`stepOnce`:

- **Order preview and status.** A formation-redirected `MoveTo` does not
  target the literal clicked cell: `Appraisal.resolveFormationTarget`'s
  bounded-radius fallback can land the real destination short of the click
  (agent 2 ordered to `(8,6)` resolves to `(7,6)`, outside the machine gun's
  engagement range). So that the player sees this, the route preview
  resolves through `Appraisal.resolveFormationTarget`, with the same inputs
  the appraisal phase in `Simulation.fs` uses, wherever the order will be
  formation-redirected (see the multi-select section below), and the
  order-status HUD text appends the agent's real `Destination`
  (`"accepted -> (7,6)"`) instead of bare `"accepted"`.
- **Leader marker.** `Casualty.currentLeader state.Agents` (the TASK-045
  succession rule, a pure function over already-held state) drives a green
  ring plus a "LEADER" label that follows whichever agent currently holds
  the role, including after succession.
- **Under-fire hit flash.** A `heldHitFlashes` timer (the `heldFireLines`
  precedent) blends a hit target's own figure toward white for
  `hitFlashHoldSeconds` (0.3 s) on every landed `FireLine`, then fades back;
  it is a colour blend on the existing figure, not a separate draw item.
- **Cover indicator.** A short cyan spoke per covered cell and direction
  renders `Terrain.Cover` (built once in `Ready`, the
  `buildObjectiveMarkerItems` precedent), thicker for a higher `Level`.
  Cover cannot be painted through the tileset (TASK-060); it is authored as
  `terrain-cover` lines in the `.cwscenario` file.
- **Selection halo.** The gold halo (radius 34) under a selected agent is
  drawn beneath the agent's opaque figure (radius 20), so it reads as a ring
  around it.

There is no enemy response beyond engaging visible targets: enemy doctrine
(suppress likely routes, seek cover under pressure, scripted fall-back) is
backlog B-022, descoped since TASK-037.

### Visible stall failure for a permanently blocked order (TASK-065, backlog B-065)

A permanently blocked order fails visibly instead of freezing, which
addresses the deadlock risk R-010 in `docs/10_RISK_REGISTER.md`. The
`CommandoWar.Sim` side (an `AgentState.StalledTicks` counter;
`Simulation.navigationAndMovement` abandons a route frozen for
`Simulation.StallAbandonTicks` = 40 consecutive ticks, clears
`Destination`/`Route` and emits a `MovementAbandoned` event) is documented in
`docs/04_SIMULATION_SPEC.md` and
`tasks/TASK-065-STALLED-ORDER-VISIBLE-FAILURE.md`; this section covers the
client's reaction to it.

An abandoned order clears `Destination` exactly like a fulfilled one, so
the order-status text alone would read as "your soldier arrived".
`CommandDemoScene.stepOnce` therefore watches `devFrame.Overlays` for the
`Abandoned` case (the `FireLine -> heldFireLines` precedent) and records the
agent id in a `heldAbandonedOrders` timer (`abandonedOrderHoldSeconds` =
3.0, held longer than a fire flash because the player needs time to read
the word). `Update`'s order-status text checks it ahead of the
accepted/destination-suffix logic and reads `"accepted -> abandoned (route
blocked)"` for the held duration. The `F1` developer overlay draws a
distinct orange marker for the `Abandoned` overlay (the
`Reserved`/`Obstructed` cyan/red precedent). The `content/replays/
stalled-order-abandoned` corpus entry (a permanent single-sided block, run
past the 40-tick threshold) covers the whole chain end to end with a
committed ASCII/SVG golden. There is no separate client UI for this: the
same order-status text and developer overlay each gain one case.

### `--screenshot-squad`: watch a scripted playthrough (TASK-065/066)

`--screenshot-squad <frameCount> <path>` is a general-purpose evaluation
tool for watching the real six-agent Bridgehead advance rather than reading
a headless hash. It issues the same six selections and advance-on-the-bridge
orders as `CommandDemoDrive.runScriptedSelfCheck` (the target cells in
`FSharpSceneHost.cs` differ from the self-check's for agents 1, 2, 4 and 5)
through the real `OnClick`/`OnHover` UI path instead of `StepTicksHeadless`,
then stays unpaused (the `--screenshot-mission` precedent), so repeated
invocations at different frame counts build a filmstrip:

```
GODOT="C:/Users/Dave/Documents/GitHub/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
cd src/CommandoWar.Client.Godot

for f in 30 90 200 400; do
  "$GODOT" --path . scenes/CommandDemo.tscn -- --screenshot-squad $f "/tmp/squad_$f.png"
done
```

It requires a real GPU-backed window (not `--headless`, the same constraint
`--screenshot`/`--screenshot-mission` have), because `GetViewport()?.
GetTexture()?.GetImage()` returns nothing without one. The filmstrip out to
tick 400 is how the corpse-blocking gap (B-066) and the live-agent
chokepoint jam (B-067, B-069) were found.

### A dead agent's corpse stops blocking movement (TASK-066, backlog B-066)

`Simulation.navigationAndMovement`'s `occupantOf` map excludes any
non-`Alive` agent (`Casualty.isAlive`), so a corpse's cell reads as free and
a live agent walks through or onto it. `Pathfinding.fs` has no occupancy
concept. This is sim-side only; the Godot client changed only by the
screenshot tool above.

Corpses are not the cause of the squad jam visible in the `--screenshot-squad`
filmstrip: that jam involves no death, only live agents blocking each other
through formation-offset contention at the bridge chokepoint, a separate
mechanism (B-067, B-069).

### Formation redirect applies only to group orders (TASK-067, backlog B-067)

`ReceivedOrder.AsGroup` (`Canonical.FormatVersion` 15) is derived once at
command intake from the originating `PlayerCommand`'s recipient count
(`Recipients.Length > 1`) and gates every `Appraisal.resolveFormationTarget`
call site: `Appraisal.appraise` itself, the three calls in `Simulation.fs`,
and `Diagnostics.formationSlotOverlays`. A single-recipient
`MoveTo` order always resolves to the literal clicked cell, regardless of
the recipient's own `FormationOffset`; only a multi-recipient order is
redirected to formation slots.

`CommandDemoDrive.runScriptedSelfCheck`'s six-agent Bridgehead sequence
targets, for each agent, the cell the formation redirect would have
resolved (computed with `Appraisal.resolveFormationTarget` against the real
scenario), because its orders are single-recipient and no longer redirect.
Its outcome is "no friendly casualties, machine gun neutralised". Agents
that share a target cell (0, 1 and 5 at `(8,5)`; 2 and 4 at `(8,6)`) cannot
all reach it, so several end the run short of their target, the live-agent
contention that B-069 addresses (see below).

The self-check commands and pinned hashes are in the first section above.

### Multi-select and joint order dispatch (TASK-068, backlog B-067 second half)

`selected` (`CommandDemoScene.fs`) is a set of agents. Plain left-click
replaces the whole selection with that one agent; **drag rubber-band-select**
(click-drag over empty ground selects every friendly agent whose figure
centre falls inside the drawn rectangle, replacing the selection, or adding
to it with shift held) and **shift-click** (toggles one agent into or out of
the current selection) are the other ways to select. Right-click always
clears the entire selection. A `MoveTo` order issued to more than one
selected agent dispatches one `Command.moveToMany`, so `ReceivedOrder.AsGroup`
is `true` and formation redirect (`Appraisal.resolveFormationTarget`)
applies to the group order. `Hold`/`Assault`/`Withdraw`/`Suppress` have no
`*Many` builder and need none (only `MoveTo` reads formation): a group order
for any of these four is N single-recipient commands, one per eligible
selected agent, added to `pending` in one click.

A selected agent that is Dead or Incapacitated, or already standing on the
clicked cell, is silently dropped from the dispatched order; the rest of the
eligible selection still receives it. `OnClick`'s pending-order dedup
matches on `Recipients` membership against every agent about to be
addressed, so a stale pending single-recipient order for any recipient of a
new multi-recipient command is replaced rather than landing in the same
tick's batch.

The hover/route preview shows every selected agent's own route. With more
than one agent selected and `MoveTo` armed, each preview is resolved through
`Appraisal.resolveFormationTarget` first (the same inputs the appraisal
phase in `Simulation.fs` uses), so it shows the route the order will take;
a single selected agent, or any other order mode, previews the literal
cell. The HUD selection line shows a count above one agent ("N agents").
Per-agent detail (the order-status text, and the `F1` dev-overlay agent line
and LOS ray) needs exactly one selected agent; the dev-overlay exposed-route
markers are shown for every selected agent.

`FSharpSceneHost.cs`'s `_UnhandledInput` tracks a real press/motion/release
sequence for the left button (right-click and the HUD-icon click stay
single-press). A press starts tracking a drag; on release, a drag shorter
than `DragThresholdPixels` (6px) resolves as an ordinary click. A longer
drag tests the dragged rectangle against every real, full-opacity agent's
on-screen centre (`TryHitAgentCircle`'s own per-agent derivation,
`Rect2.HasPoint` in place of its point-vs-circle distance test) and hands
the hit cells to `IClientScene.OnDragSelect`. A translucent marquee
(`DrawRect`, the `DrawMissionSummaryPanel` precedent) renders while
dragging past the threshold.

Two agents ordered jointly to one shared literal cell (for example
fireteam-alpha slots 0 and 1, authored `FormationOffset` `(0,0)` and
`(1,0)`) resolve to two distinct predicted cells, and the scene's
preview-dot route endpoints match `Appraisal.resolveFormationTarget`
computed independently against the same scenario and seed. When the trailing
agent's route is blocked by the leading agent parked on a cell it needs, the
sim reroutes around the parked agent (TASK-070, B-069); if no route exists,
the order is abandoned after `Simulation.StallAbandonTicks` (TASK-065).

`CommandDemo.tscn`'s scripted sequence is entirely single-agent, no-shift
clicks, so it dispatches as it would without multi-select. Screenshot
evidence is `docs/evidence/task-068-multiselect.png`, produced by
`--screenshot-multiselect <path>` (the `--screenshot-squad` precedent): it
primes a real two-agent drag-select and joint order through
`OnDragSelect`/`OnHover`/`OnClick` directly and pauses immediately, so the
captured frame holds the pending state.

### Bridgehead trooper movement speed halved (TASK-069, backlog B-068)

The simulation runs at exactly 20 ticks per second (`simHz`), independent
of the render frame rate. `Terrain.BaseMoveCost = 1` combined with a
Trooper's authored `MoveSpeed = 2` (`Domain.fs`'s `Agent.MoveSpeedDefault`)
would cross a whole open-terrain cell in one 20 Hz tick (50 ms), which reads
as fast, choppy movement. `content/scenarios/bridgehead.cwscenario`
therefore authors `unit-type trooper 1`, which takes 2 ticks (100 ms) per
cell. This is a content setting; `CommandoWar.Sim` is unchanged. At this
speed the scripted self-check outcome ("no friendly casualties, machine gun
neutralised") is unchanged within the 90-tick budget.

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
project.godot              Godot project (run/main_scene = CommandDemo.tscn)
Main.tscn                   root: MainNode + the two spike content scenes (TASK-004 spike)
scenes/Greybox.tscn         authored 32x32 isometric greybox + 6 spawn markers + objective
scenes/GreyboxInvalid.tscn  deliberately broken content for the failure path
scenes/AppraisalDemo.tscn   read-only corpus-scoped appraisal-divergence demo (TASK-029)
scenes/SnapshotDemo.tscn    live snapshot rendering + isometric depth ordering (TASK-039)
scenes/CommandDemo.tscn     main scene: selection, input mapping, tactical pause, command preview, Bridgehead mission (TASK-040)
scenes/ReplayDemo.tscn      read-only scrubber over a recorded `.cwreplay` file (TASK-071)
scenes/TerrainAuthoring.tscn  TileMapLayer target for the terrain import/export tools
src/SimFacade.cs            thin C# facade over CommandoWar.Sim  (NO Godot types, TASK-004 spike)
src/SpikeContent.cs         framework-neutral content DTOs + validation  (NO Godot types, TASK-004 spike)
src/GreyboxScene.cs         reads the authored scene -> RawContent  (import boundary, TASK-004 spike)
src/SpikeMarker.cs          typed marker node (TASK-004 spike)
src/MainNode.cs             fixed-step scheduling, input, isometric render, overlay (TASK-004 disposable spike)
src/AppraisalDemoScene.cs   thin C# renderer for the appraisal demo (a scoped ADR-0004 deviation, see the file header)
src/FSharpSceneHost.cs      the generic ADR-0004 C# host over Core/ -- every production
                            scene's `.tscn` uses this, not a new per-scene C# file
Core/                       the ADR-0004 F# client-core library (CwClientCore.*)
Core/IClientScene.fs        the primitives-only scene contract the host drives
Core/RenderShared.fs        terrain-item + depth-sort helpers shared by every render scene
Core/DemoRenderScene.fs     the live snapshot-rendering scene (SnapshotDemo.tscn)
Core/CommandDemoScene.fs    live selection, input-mapped orders, route preview, pause,
                            multi-select (drag/shift-click) with joint order dispatch,
                            the F1 developer overlay over live Diagnostics.DiagnosticFrame,
                            session recording
Core/ReplayDemoScene.fs     the replay scrubber scene (ReplayDemo.tscn)
Core/TerrainAuthoring.fs    shared terrain import/export between TileMapLayer cells and `.cwscenario`
art/                        Kenney CC0 placeholder terrain/agent textures + licence file (TASK-041)
tools/                      in-editor terrain export/import scripts and their headless proofs (TASK-060, TASK-061)
```

In the TASK-004 spike, only `SimFacade.cs` calls `CommandoWar.Sim`, and
everything it passes across the C#/F# boundary is a primitive, an array, or
an F# value type. `Core/` also references `CommandoWar.Sim` and
`CommandoWar.Headless` directly, which ADR-0002 permits ("the client
references the simulation"). The C# files that reference `Core` are
`FSharpSceneHost.cs`, which resolves `Core`'s `IClientScene` implementations by
name and calls its scripted self-check drivers (`DemoDrive`,
`CommandDemoDrive`), and the editor scripts in `tools/`, which call
`CwClientCore.TerrainAuthoring`.
`AppraisalDemoScene.cs` references `CommandoWar.Headless`.

## Build

```
dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug
```

The Godot editor also builds it on open / on run. Output lands in
`.godot/mono/temp/bin/<config>/`.

## Run outside the editor

```
# windowed game (not the editor UI): CommandDemo.tscn, the main scene
<godot> --path src/CommandoWar.Client.Godot

# headless determinism check of the main scene (prints `MATCH expected final hash 0x300BB18492CEB355 at tick 90`, exit 0)
<godot> --headless --path src/CommandoWar.Client.Godot -- --selfcheck

# the TASK-004 spike scene (Main.tscn): headless hash cross-check against CommandoWar.Headless
<godot> --headless --path src/CommandoWar.Client.Godot res://Main.tscn -- --selfcheck --expect 0xD2A6A1AE46AD76A5

# spike scene, deliberately invalid content -> exit 2, actionable errors
<godot> --headless --path src/CommandoWar.Client.Godot res://Main.tscn -- --selfcheck --invalid

# spike scene, self-captured evidence screenshot (slows the scheduler to 6 Hz for a mid-move still)
<godot> --path src/CommandoWar.Client.Godot res://Main.tscn -- --screenshot <abs-path>.png
```

The other scenes take their `--selfcheck` and `--screenshot*` flags the same
way, with `scenes/<Scene>.tscn` in place of `res://Main.tscn`; their capture
commands are in the sections above. The pinned final hashes are:

| Scene | `--selfcheck` final hash |
|---|---|
| `SnapshotDemo.tscn` | `0x6213D672BC36FDB8` (tick 20) |
| `CommandDemo.tscn` | `0x300BB18492CEB355` (tick 90) |
| `ReplayDemo.tscn` | `0x5876C1280DDAE2CB` (tick 10, `content/replays/chokepoint-detour.cwreplay`) |
| `AppraisalDemo.tscn` | `0xA1354EB998FC1B95` (tick 1; the default for `--expect`) |

`--invalid` is read only by `Main.tscn`, and `--expect` only by `Main.tscn` and
`AppraisalDemo.tscn`.

The spike's `--selfcheck` runs the shared fixture
(`content/fixtures/SPIKE-FIXTURE.md`): 32x32, seed 20260902, agent 3 ->
(20,14) at tick 1, 40 ticks, final hash `0xD2A6A1AE46AD76A5` (canonical format
15). It prints one `tick=N hash=0x...` line per tick; the sequence is
identical to `dotnet run --project src/CommandoWar.Headless -- fixture`.

## In-editor controls

These are the `Main.tscn` spike's controls. `CommandDemo.tscn` has its own,
described in the sections above (`Space` pauses, `F1` toggles the developer
overlay).

- left-click an agent to select, left-click a cell to issue `MoveTo`
- right-click to deselect
- `1` / `2` change the authoritative sim rate; `Space` pauses
- `S` writes `user://godot-run.cwlog` (feed it to `cwheadless compare`)

## Packaging

`--export-release` / `--export-pack` presets work, but a self-contained
executable needs the Godot **export templates** for `4.7.2.stable.mono`, which
are not installed (`%APPDATA%\Godot\export_templates\4.7.2.stable.mono\` is
empty). See the TASK-004 ledger entry for the exact error and the unblock path.
The repository commits no `export_presets.cfg`; the Windows build procedure and
its acceptance checklist are in `docs/14_PLAYTEST_KIT.md` section 2.

## Playtest session recording and shipped content (TASK-080, backlog B-036)

`CommandDemoScene` (the main scene) records the commands it actually delivers
to `Simulation.step` into one `session-<UTC time>.cwreplay` file per launch
under `<ApplicationData>/CommandoWar/playtest/`. The file is rewritten on
delivery, every 20 ticks, and on every pause and mission end. A failed write
shows `SESSION RECORDING FAILED: ...` on the HUD status line instead of
stopping the run. The scripted self-check constructs the scene with no session
directory and writes nothing, so it does not affect the pinned self-check
hashes. `cwheadless replay-file <session>` reads a session back. The full
playtest procedure, script, and forms are `docs/14_PLAYTEST_KIT.md`.

`FSharpSceneHost.ResolveContentPath` looks for `content/<path>` beside the
executable first (an exported build), then falls back to the repository's
`content/` (editor and `dotnet` runs). An exported build needs
`content/scenarios/bridgehead.cwscenario` copied beside the executable
(`docs/14_PLAYTEST_KIT.md` section 2, step 3); no export has been built or run
to confirm this lookup.
