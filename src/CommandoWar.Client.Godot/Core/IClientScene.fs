namespace CwClientCore

/// One item in a depth-sorted immediate-mode draw list. ADR-0004's interop
/// idiom: only primitives, `System.Nullable<T>`, and arrays of
/// `[<CLIMutable>]` records cross the C#/F# boundary -- never a `Godot.*`
/// type, an F# option/DU/list/tuple. `Kind`: `0` = a terrain cell (the host
/// draws a Kenney tile texture keyed on `TextureId`), `1` = an agent or an
/// overlay marker (a full-opacity item, `A >= 0.99`, is a real agent and
/// draws one of 8 pre-rendered facing textures keyed on `TextureId`
/// (`RenderShared.facingBin`, TASK-054, backlog B-052); a translucent item
/// is a halo/route-preview marker, ignores `TextureId`/`Cx2`, and still
/// draws a plain circle of `Radius` -- TASK-041, backlog B-034), `2` = a
/// line segment from `(Cx,Cy)` to `(Cx2,Cy2)` with `Radius`
/// as line width (TASK-043, backlog B-029: line-of-sight rays, fire lines),
/// `3` = a text label drawn at `(Cx,Cy)` with `Radius` as font size
/// (TASK-043: grid coordinates), `4` = a one-shot effect sprite centred at
/// `(Cx,Cy)` with `Radius` as on-screen size and `R`/`G`/`B`/`A` as a tint
/// (TASK-046, backlog B-057: a muzzle flash or bullet-impact effect, keyed
/// on `TextureId` the same way terrain is), `5` = a hollow (unfilled) ring
/// at `(Cx,Cy)` with `Radius` as the ring's radius (TASK-051, backlog
/// B-055: a hostile's last-known position once contact is lost -- a
/// distinct outline shape, not a translucent `Kind = 1` fill, so it never
/// reads as a dim real agent). `TextureId` is meaningful for
/// `Kind = 0` (`0` = passable open ground, elevation-tinted via `R`/`G`/`B`,
/// `1` = impassable, `2` = passable-but-opaque cover -- shape, not colour
/// alone, carries this distinction, docs/06 "status indicators that do not
/// rely on colour alone"), for a full-opacity `Kind = 1` (`0..7`, a facing
/// bin -- meaningless for a translucent `Kind = 1` marker), and for
/// `Kind = 4` (`0` = muzzle flash, `1` =
/// bullet impact/hit, `2` = a miss puff -- again a distinct shape per
/// outcome, not a colour-only hit/miss tint). `Cx2`/`Cy2` are meaningful
/// for `Kind = 2` (the line's second endpoint) and, for a full-opacity
/// `Kind = 1` item only, `Cx2` doubles as a run-cycle frame index
/// (TASK-056, backlog B-052: `RenderShared.runFrameIndex`) -- `< 0` (a `-1`
/// sentinel) means frozen on the idle pose, `0..9` selects that `Human_N`
/// direction's own `Run0..9` frame; `Text` only for `Kind = 3` (empty
/// string otherwise). The array is
/// pre-sorted back-to-front by the F# side (screen depth `(Cx + Cy)`,
/// terrain before an agent occupying the same cell); the C# host only
/// projects each item's cell coordinates to screen space and issues one
/// `Draw*` call per item, in array order -- the "screen<->cell projection
/// arithmetic" ADR-0004 explicitly allows in the C# shim.
[<CLIMutable>]
type DrawItem =
    { Kind: int
      TextureId: int
      Cx: float32
      Cy: float32
      Cx2: float32
      Cy2: float32
      Text: string
      R: float32
      G: float32
      B: float32
      A: float32
      Radius: float32 }

/// One tick's authoritative state hash (`DemoDrive.runFullSequence`'s
/// `--selfcheck` evidence). A record, not a tuple: ADR-0004 forbids exposing
/// F# tuples to C#.
[<CLIMutable>]
type TickHash = { Tick: int64; Hash: uint64 }

/// The contract a Godot scene's F# logic implements, resolved by name from
/// the generic C# `FSharpSceneHost` (ADR-0004 form 1: one C# host for the
/// whole client, no per-scene C#, `[Export] SceneType` names the
/// implementation).
type IClientScene =
    /// Called once from the host's `_Ready`. `scenarioContentPath` is the
    /// already-resolved absolute path to a `.cwscenario` content file (the
    /// host resolves `content/` via `ProjectSettings.GlobalizePath`, the
    /// `AppraisalDemoScene.cs` precedent -- file-path resolution is a
    /// Godot/C# concern per ADR-0004, parsing stays framework-neutral F#).
    /// A scene with no real content to load (like `DemoRenderScene`, which
    /// still loads its own hand-authored `DemoScenario`) ignores it.
    abstract Ready: scenarioContentPath: string -> unit
    /// Called once per host `_Process(delta)`, wall-clock seconds since the
    /// last call. Owns the fixed-step authoritative-tick accumulator; the
    /// simulation never sees a wall-clock value directly (ADR-0004's
    /// per-concern table: "Fixed-step scheduling | F#").
    abstract Update: deltaSeconds: float -> unit
    /// The current frame's depth-sorted draw list.
    abstract DrawList: unit -> DrawItem[]
    /// One-line status text for a HUD label.
    abstract HudText: unit -> string
    /// A mouse-button press, already projected from screen space to a grid
    /// cell by the C# host (TASK-040, ADR-0004's screen<->cell projection
    /// rule: the host resolves the cell, F# only ever sees cell
    /// coordinates). `isLeftButton = false` is a right-click. `shiftHeld`
    /// (TASK-068, backlog B-067 second half) is the modifier-key state at
    /// the moment of the click, read by the C# host
    /// (`InputEventMouseButton.ShiftPressed`) the same "raw input capture |
    /// C#" way the button/position already are -- a shift-held left-click on
    /// a friendly agent toggles it into/out of the current selection instead
    /// of replacing the whole selection with just that one agent.
    abstract OnClick: isLeftButton: bool * cellX: int * cellY: int * shiftHeld: bool -> unit
    /// The mouse has moved over the given cell (already projected). Used to
    /// drive a hover-dependent command preview.
    abstract OnHover: cellX: int * cellY: int -> unit
    /// A completed rubber-band drag-select (TASK-068, backlog B-067 second
    /// half), resolved entirely in the C# host: for every real, full-opacity
    /// agent item in the current `DrawList()` (any side -- the `OnClick`/
    /// `friendlyAt` precedent of letting F# do side filtering) whose
    /// on-screen figure centre falls inside the dragged rectangle
    /// (`TryHitAgentCircle`'s own screen-position derivation, a
    /// `Rect2.HasPoint` test in place of its point-vs-circle distance test),
    /// its cell is included -- parallel primitive arrays, never a rectangle,
    /// an agent id, or an F# collection crossing the boundary (ADR-0004).
    /// `shiftHeld` adds every hit agent to the existing selection instead of
    /// replacing it, the same modifier `OnClick` reads.
    abstract OnDragSelect: cellXs: int[] * cellYs: int[] * shiftHeld: bool -> unit
    /// The tactical-pause key was pressed. A scene that has nothing to pause
    /// (like `DemoRenderScene`) may no-op.
    abstract OnTogglePause: unit -> unit
    /// An XCOM-style HUD order-mode icon was clicked (TASK-048, backlog
    /// B-059; index 4 added by TASK-064, backlog B-035): `index` is
    /// `0 = MoveTo`, `1 = Hold`, `2 = Assault`, `3 = Withdraw`,
    /// `4 = Suppress` -- the C# host owns the fixed on-screen icon rects and
    /// their hit-testing (a HUD-chrome layout concern, not a world-grid
    /// projection); this call only ever carries the resolved index, the
    /// `OnClick`/`ScreenToCell` precedent of primitives-only across the
    /// boundary. A scene with no order-mode concept (like `DemoRenderScene`)
    /// may no-op. While armed, `Suppress`'s own next `OnClick` targets
    /// whichever agent occupies the clicked cell (any side), not a bare
    /// cell -- `Command.suppress` takes an `AgentId`, not a `Cell`.
    abstract OnOrderModeClick: index: int -> unit
    /// The currently armed order mode (the same `0..4` vocabulary as
    /// `OnOrderModeClick`), read once per frame so the host can highlight the
    /// active icon. `0` (`MoveTo`) for a scene with no order-mode concept.
    abstract OrderMode: unit -> int
    /// The developer-overlay key was pressed (TASK-043, backlog B-029). A
    /// scene with no live input to overlay (like `DemoRenderScene`) may
    /// no-op -- the `OnTogglePause` precedent.
    abstract OnToggleDevOverlay: unit -> unit
    /// The total number of recorded ticks this scene can scrub through
    /// (TASK-071, backlog B-064). `0` for a scene with no replay/scrub
    /// concept (the `OrderMode`/`MissionSummaryLines` "0/empty means
    /// nothing here" precedent) -- the C# host uses this both to size and
    /// to gate drawing/hit-testing the scrub bar, so it never appears on a
    /// scene that has nothing to scrub.
    abstract TickCount: unit -> int64
    /// The tick currently displayed, `0 .. TickCount()`. Always `0` for a
    /// scene with no replay/scrub concept.
    abstract CurrentTick: unit -> int64
    /// Scrubs to an absolute tick (the host's drag/click/step-key handling
    /// all funnel through this one entry point, clamped to
    /// `0 .. TickCount()` by the implementation). A scene with no replay/
    /// scrub concept may no-op.
    abstract SetTick: tick: int64 -> unit
    /// The authoritative canonical-state hash at the tick `CurrentTick()`
    /// currently displays (`0UL` for a scene with no replay/scrub concept)
    /// -- read by the host's `--selfcheck` evidence path the same way
    /// every other scene's own tick/hash sequence already is.
    abstract CurrentHash: unit -> uint64
    /// The mission-summary panel's lines (TASK-063, backlog B-033 narrowed),
    /// read once per frame the same way `OrderMode` is (primitives only,
    /// the `OnOrderModeClick`/`OrderMode` boundary precedent). An empty
    /// array means nothing to show -- either `WorldState.MissionOutcome`
    /// is still `InProgress`, or (like `DemoRenderScene`) the scene has no
    /// mission-outcome concept at all. Non-empty means the mission has
    /// ended: the host draws a panel with these exact lines and the scene
    /// itself is expected to have already stopped accepting further orders.
    abstract MissionSummaryLines: unit -> string[]
    abstract Dispose: unit -> unit
