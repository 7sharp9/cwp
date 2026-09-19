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
    /// Called once from the host's `_Ready`.
    abstract Ready: unit -> unit
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
    /// coordinates). `isLeftButton = false` is a right-click.
    abstract OnClick: isLeftButton: bool * cellX: int * cellY: int -> unit
    /// The mouse has moved over the given cell (already projected). Used to
    /// drive a hover-dependent command preview.
    abstract OnHover: cellX: int * cellY: int -> unit
    /// The tactical-pause key was pressed. A scene that has nothing to pause
    /// (like `DemoRenderScene`) may no-op.
    abstract OnTogglePause: unit -> unit
    /// An XCOM-style HUD order-mode icon was clicked (TASK-048, backlog
    /// B-059): `index` is `0 = MoveTo`, `1 = Hold`, `2 = Assault`,
    /// `3 = Withdraw` -- the C# host owns the fixed on-screen icon rects and
    /// their hit-testing (a HUD-chrome layout concern, not a world-grid
    /// projection); this call only ever carries the resolved index, the
    /// `OnClick`/`ScreenToCell` precedent of primitives-only across the
    /// boundary. A scene with no order-mode concept (like `DemoRenderScene`)
    /// may no-op.
    abstract OnOrderModeClick: index: int -> unit
    /// The currently armed order mode (the same `0..3` vocabulary as
    /// `OnOrderModeClick`), read once per frame so the host can highlight the
    /// active icon. `0` (`MoveTo`) for a scene with no order-mode concept.
    abstract OrderMode: unit -> int
    /// The developer-overlay key was pressed (TASK-043, backlog B-029). A
    /// scene with no live input to overlay (like `DemoRenderScene`) may
    /// no-op -- the `OnTogglePause` precedent.
    abstract OnToggleDevOverlay: unit -> unit
    abstract Dispose: unit -> unit
