namespace CwClientCore

/// One item in a depth-sorted immediate-mode draw list. ADR-0004's interop
/// idiom: only primitives, `System.Nullable<T>`, and arrays of
/// `[<CLIMutable>]` records cross the C#/F# boundary -- never a `Godot.*`
/// type, an F# option/DU/list/tuple. `Kind`: `0` = a terrain cell (the host
/// draws a Kenney tile texture keyed on `TextureId`), `1` = an agent or an
/// overlay marker (a full-opacity item, `A >= 0.99`, is a real agent and
/// draws the Kenney human texture; a translucent item is a halo/route-preview
/// marker and still draws a plain circle of `Radius` -- TASK-041, backlog
/// B-034), `2` = a line segment from `(Cx,Cy)` to `(Cx2,Cy2)` with `Radius`
/// as line width (TASK-043, backlog B-029: line-of-sight rays, fire lines),
/// `3` = a text label drawn at `(Cx,Cy)` with `Radius` as font size
/// (TASK-043: grid coordinates). `TextureId` is meaningful only for
/// `Kind = 0`: `0` = passable open ground (elevation-tinted via `R`/`G`/`B`),
/// `1` = impassable, `2` = passable-but-opaque cover -- shape, not colour
/// alone, carries this distinction (docs/06 "status indicators that do not
/// rely on colour alone"). `Cx2`/`Cy2` are meaningful only for `Kind = 2`;
/// `Text` only for `Kind = 3` (empty string otherwise). The array is
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
    /// The developer-overlay key was pressed (TASK-043, backlog B-029). A
    /// scene with no live input to overlay (like `DemoRenderScene`) may
    /// no-op -- the `OnTogglePause` precedent.
    abstract OnToggleDevOverlay: unit -> unit
    abstract Dispose: unit -> unit
