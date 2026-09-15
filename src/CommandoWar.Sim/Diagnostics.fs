namespace CommandoWar.Sim

/// A framework-neutral per-tick diagnostic model of authoritative spatial and
/// tactical state (docs/06_CONTENT_AND_PRESENTATION.md section 11
/// "Developer-facing"; docs/07_VERTICAL_SLICE.md section 6 "developer overlay",
/// functional acceptance criterion 11; docs/03_ARCHITECTURE.md section 15
/// "optional development overlays"; docs/09_TEST_STRATEGY.md section 8).
///
/// This module is an OBSERVER, exactly as `Canonical.fs` and `Divergence.fs`
/// are: NOTHING in `Simulation.step` or any tick phase constructs a
/// `DiagnosticFrame`, and no frame ever feeds back into the step. It exists
/// for tests, replay inspection, and the headless / developer renderers
/// (`CommandoWar.Headless.DiagnosticRender`, and later the Godot developer
/// overlay, backlog B-029).
///
/// It is framework-neutral in the ADR-0002 sense: ints, bools, arrays,
/// strings, and discriminated unions only. There is NO floating point
/// anywhere in the model (SVG coordinates in the renderers are integer pixels
/// at an integer px-per-cell scale). `Diagnostics.frame` and
/// `Diagnostics.frameOf` are total, pure, and deterministic: they do not
/// mutate, do not read `Random`, do not touch wall-clock time, and are not
/// part of the phase pipeline. Building a frame must not move any pinned
/// fixture hash and must not change `Canonical.encode`; this module is a leaf
/// that nothing authoritative references.
///
/// `Diagnostics.frameOf` emits one `PlannedPath` overlay per agent that is
/// following a route (TASK-015): the followed path is authoritative-derived
/// spatial state, so the `docs/09` section 8 rule wants it visible in a render.
/// `Diagnostics.frame` still emits no overlay (a bare `WorldState` carries no
/// per-tick movement history to draw from). Every other overlay is still
/// supplied by a caller.

/// The canonical layer names carried by `Diagnostics.frame`. Renderers key on
/// these strings; `--layer` on the `cwheadless render` verb filters
/// `DiagnosticFrame.Layers` by one of them.
[<RequireQualifiedAccess>]
module LayerName =

    /// Elevation level per cell (`Terrain.Elevation`). Flat terrain is all 0.
    [<Literal>]
    let Elevation = "elevation"

    /// 1 where an agent may enter the cell, 0 where it may not
    /// (`Terrain.MovementClass`).
    [<Literal>]
    let Passability = "passability"

    /// Authored integer entry cost per cell (`Terrain.MoveCost`). Meaningful
    /// only where `passability` is 1; an impassable cell keeps its authored
    /// value here even though `Terrain.moveCost` reports `Terrain.BlockedCost`.
    [<Literal>]
    let MovementCost = "movement-cost"

    /// 1 where the cell blocks line of sight (high occlusion), 0 otherwise
    /// (`Terrain.Opaque`).
    [<Literal>]
    let Opacity = "opacity"

/// One dense row-major integer grid for a `GridBounds`. `Cells` has
/// `Bounds.Width * Bounds.Height` entries, indexed `y * Bounds.Width + x`.
/// The meaning of a cell value is fixed by `Name` (see `LayerName`).
type GridLayer =
    { Name: string
      Bounds: GridBounds
      Cells: int[] }

/// One directional low-cover marker: `Cell` has cover of `Value` against an
/// attack arriving from `Direction`. Emitted only for cells with a non-zero
/// authored cover level.
type EdgeMarker =
    { Cell: Cell
      Direction: Direction
      Value: int }

/// One agent as the frame sees it: identity, side, logical cell, integer
/// progress toward entering its next cell (TASK-018, `AgentState.Progress`;
/// 0 at rest), the movement destination if it has one, and whether the agent
/// can receive orders (TASK-027, `AgentState.CommunicationAvailable`; `true`
/// for an ordinary agent, `false` under an authored comms blackout — the
/// renderers surface it only when `false`, the `Progress`-omitted-at-0
/// precedent).
type AgentMarker =
    { Id: AgentId
      Side: Side
      Cell: Cell
      Progress: int
      Destination: Cell option
      CommunicationAvailable: bool }

/// A small framework-neutral summary of one `DomainEvent` emitted this tick:
/// a kind label and the cells the event concerns. This lets a frame show
/// what happened, not only the end state. `Diagnostics.frame` carries none
/// (it has no `StepResult`); `Diagnostics.frameOf` carries one per event.
type EventMarker =
    { Kind: string
      Cells: Cell[] }

/// Open extension point for overlays that later tactical systems attach.
/// Every renderer draws an overlay through the cells it names, so a new
/// system adds a case here and a branch in each renderer:
///
///   * B-009 line of sight  -> `SightRay` (realised by TASK-012);
///   * B-010 pathfinding     -> `PlannedPath` (realised by TASK-013);
///   * B-011b reservation    -> `Reserved` (realised by TASK-017);
///   * B-047 cell occupancy  -> `Obstructed` (realised by TASK-022);
///   * B-015 perception      -> `KnownContact` (realised by TASK-026);
///   * B-016 communication    -> `UndeliveredOrder` (realised by TASK-027);
///   * B-017 appraisal       -> `OrderAppraisal` (realised by TASK-028);
///   * B-018 commitment      -> `Commitment` (realised by TASK-030);
///   * B-019 combat          -> `FireLine` (realised by TASK-031);
///   * B-020 suppression     -> `AgentSuppression` (realised by TASK-032);
///   * B-021 stress          -> `AgentStress` (realised by TASK-033);
///   * B-022 hostile picture -> `HostileKnownContact` (realised by TASK-034).
///
/// `Diagnostics.frame` produces one `KnownContact` per contact in
/// `WorldState.TacticalKnowledge`, one `OrderAppraisal` per agent that holds
/// an appraised order, one `AgentCommitment` per agent, one
/// `AgentSuppression` per agent with non-zero `Suppression`, one
/// `AgentStress` per agent with non-zero `Stress`, and one
/// `HostileKnownContact` per contact in `WorldState.HostileTacticalKnowledge`
/// (bare authoritative state carries both squad pictures and every per-agent
/// field these need, so `frame` can draw all six — unlike `Reserved` /
/// `Obstructed`, which need a completed step).
/// `Diagnostics.frameOf` produces the same `KnownContact`, `OrderAppraisal`,
/// `AgentCommitment`, `AgentSuppression`, `AgentStress`, and
/// `HostileKnownContact` sets plus one `PlannedPath` per
/// agent following a route (TASK-015), one
/// `Reserved` per cell contested this tick (TASK-017), one `Obstructed` per
/// cell an agent was held out of this tick (TASK-022), one
/// `UndeliveredOrder` per recipient an order failed to reach this tick
/// (TASK-027), and one `FireLine` per shot fired this tick (TASK-031); every
/// other overlay is populated by a caller (a test, or
/// `cwheadless render --los` / `--path`). `Cells` is the generic
/// non-speculative shape: a labelled set of cells a renderer can always fall
/// back to.
type Overlay =
    /// A labelled set of cells.
    | Cells of label: string * cells: Cell[]
    /// A traced line of sight: origin, target, the traced cell path
    /// (`Sight.trace`'s `Path`), and the first blocking cell when the target
    /// is not visible. Supplied by a caller; `Diagnostics` never emits one.
    | SightRay of from: Cell * target: Cell * cells: Cell[] * blocked: Cell option
    /// A planned grid path: start, goal, the ordered cell path
    /// (`Pathfinding.find`'s `Found` cells, empty when no path was found or
    /// the budget was exhausted), its integer cost, and whether the goal was
    /// reached. Supplied by a caller; `Diagnostics` never emits one.
    | PlannedPath of from: Cell * target: Cell * cells: Cell[] * cost: int * reached: bool
    /// A same-tick cell-reservation outcome (TASK-017, docs/04 section 8 step
    /// 3): `cell` was contested by two or more agents this tick, `winner` is
    /// the agent that entered it (fewest remaining route steps, ties broken
    /// by ascending agent id), and `untilTick` is the tick the reservation
    /// covers — always the tick it was resolved on, since resolution is a
    /// same-tick derived fact, never a persisted multi-tick booking.
    /// `Diagnostics.frameOf` derives one per `MovementYielded` event this
    /// tick; `Diagnostics` never emits one from a bare `WorldState`
    /// (`Diagnostics.frame`), which carries no per-tick movement history.
    | Reserved of cell: Cell * winner: AgentId * untilTick: int64
    /// A same-tick cell-occupancy outcome (TASK-022, docs/04 section 20 "one
    /// live agent has one authoritative position"): an agent would have
    /// completed its edge into `cell` this tick but `cell` is held by
    /// `occupant`, an agent that did not vacate it, so the mover was frozen
    /// and emitted `MovementObstructed`. Unlike `Reserved`, the blocker is a
    /// stationary occupant, not the winner of a contest, and there is no
    /// forward booking window. `Diagnostics.frameOf` derives one per distinct
    /// obstructed cell from this tick's `MovementObstructed` events;
    /// `Diagnostics.frame` never emits one.
    | Obstructed of cell: Cell * occupant: AgentId
    /// One contact in the friendly squad's shared tactical picture (TASK-026,
    /// `WorldState.TacticalKnowledge`, docs/04 section 12.4): `contact` was
    /// last seen at `cell` on tick `lastSeenTick` with `confidence` on the
    /// `0..1000` scale. Both `Diagnostics.frame` and `Diagnostics.frameOf`
    /// derive one per contact, ascending by contact id — the squad picture is
    /// on `WorldState`, so a bare state carries it. The overlay is the
    /// "known-versus-authoritative" surface risk R-023 asks for: the contact's
    /// last-known cell can lag the hostile's real `AgentMarker` position.
    | KnownContact of cell: Cell * contact: AgentId * confidence: int * lastSeenTick: int64
    /// An order the Communication phase could not deliver this tick (TASK-027,
    /// `docs/04` section 12.2): `command` was accepted for `recipient` but the
    /// recipient's `AgentState.CommunicationAvailable` is `false`, so the order
    /// was dropped and no `Destination` written. `at` is the recipient's
    /// current cell. `Diagnostics.frameOf` derives one per distinct recipient
    /// from this tick's `OrderUndelivered` events; `Diagnostics.frame` never
    /// emits one (an undelivered order is a this-tick event, not standing
    /// state — the `Reserved` / `Obstructed` precedent).
    | UndeliveredOrder of recipient: AgentId * at: Cell * command: CommandId
    /// The Appraisal phase's outcome for one agent's current order (TASK-028,
    /// `docs/04` section 12.5): `agent` at `at` holds `disposition` for its
    /// order, and `exposedCells` are the candidate-route cells that carry
    /// non-zero pressure from a known threat (empty for `Unable` or an order
    /// with no known threat on its route — recomputed from the agent's current
    /// `Position` via `Appraisal.appraise`, so for an `Accepted` agent that
    /// has moved it is the *remaining* exposed stretch). Both
    /// `Diagnostics.frame` and `Diagnostics.frameOf` derive one per agent
    /// whose `Order` and `Disposition` are both `Some` — the appraisal outcome
    /// is standing canonical state, like `KnownContact`.
    | OrderAppraisal of agent: AgentId * at: Cell * disposition: OrderDisposition * exposedCells: Cell[]
    /// An agent's current commitment (TASK-030, backlog B-018; `docs/04`
    /// section 12.6): `agent` at `at` is `Holding` or `Moving` toward the
    /// `MoveCommitment` target `commitment` carries. Named distinctly from the
    /// `Commitment` type it wraps (the `OrderAppraisal` / `OrderDisposition`
    /// precedent) to avoid a case-name / type-name collision. Derived via
    /// `Commitment.ofAgent` from `Order` / `Disposition` / `Destination` — not
    /// new `AgentState`, so it never disagrees with `OrderAppraisal` or the
    /// agent marker's `Destination`. Both `Diagnostics.frame` and
    /// `Diagnostics.frameOf` derive one per agent — the `OrderAppraisal`
    /// precedent.
    | AgentCommitment of agent: AgentId * at: Cell * commitment: Commitment
    /// A deterministic hitscan shot fired this tick (TASK-031, backlog
    /// B-019; `docs/04` section 12.8): `shooter` at `from` fired at `target`
    /// at `at`, resolving `hit`. Cells are carried explicitly (the `Reserved`
    /// / `Obstructed` precedent) rather than requiring the renderer to cross-
    /// reference `AgentMarker`s. `Diagnostics.frameOf` derives one per this
    /// tick's `ShotFired` event, looking up each side's cell from the
    /// post-step world; `Diagnostics.frame` never emits one (a shot is a
    /// this-tick event, not standing state — the `UndeliveredOrder`
    /// precedent).
    | FireLine of shooter: AgentId * from: Cell * target: AgentId * at: Cell * hit: bool
    /// An agent's current suppression (TASK-032, backlog B-020; `docs/04`
    /// sections 12.8/12.9): `agent` at `at` currently holds `suppression` on
    /// the `0..1000` scale. Unlike `AgentCommitment` (emitted for every agent
    /// unconditionally, since `Holding` is itself meaningful), this follows
    /// the `UndeliveredOrder` / `KnownContact` sparse shape: emitted only for
    /// an agent with `suppression > 0` — the common case is 0, and an
    /// unconditional per-agent overlay would add a `0` line to every agent in
    /// every existing golden for no information. Standing canonical state
    /// (not a this-tick event), so both `Diagnostics.frame` and
    /// `Diagnostics.frameOf` derive it — the `KnownContact` / `AgentCommitment`
    /// precedent, not the `FireLine` / `UndeliveredOrder` `frameOf`-only one.
    | AgentSuppression of agent: AgentId * at: Cell * suppression: int
    /// An agent's current stress (TASK-033, backlog B-021; `docs/04` section
    /// 12.9): `agent` at `at` currently holds `stress` on the `0..1000`
    /// scale. Follows the `AgentSuppression` sparse shape: emitted only for
    /// an agent with `stress > 0`. Standing canonical state, so both
    /// `Diagnostics.frame` and `Diagnostics.frameOf` derive it.
    | AgentStress of agent: AgentId * at: Cell * stress: int
    /// One contact in the Hostile side's own shared tactical picture
    /// (TASK-034, backlog B-022, partial; `WorldState.HostileTacticalKnowledge`,
    /// docs/04 section 12.4): `contact` was last seen at `cell` on tick
    /// `lastSeenTick` with `confidence` on the `0..1000` scale — the identical
    /// `KnownContact` shape, symmetric to the friendly side. A **distinct**
    /// case rather than a reuse of `KnownContact` (the `AgentStress` /
    /// `AgentSuppression` precedent), so a reviewer inspecting a golden render
    /// can tell which side's picture put a marker on a cell without
    /// cross-referencing `WorldState.Agents`. Both `Diagnostics.frame` and
    /// `Diagnostics.frameOf` derive one per contact, ascending by contact id.
    | HostileKnownContact of cell: Cell * contact: AgentId * confidence: int * lastSeenTick: int64

/// A framework-neutral snapshot of authoritative spatial and tactical state
/// for one tick, plus the determinism trio (tick, state hash, random draw
/// count). Values only, never references to authoritative storage.
type DiagnosticFrame =
    { Tick: int64
      Bounds: GridBounds
      Layers: GridLayer[]
      Edges: EdgeMarker[]
      Agents: AgentMarker[]
      Events: EventMarker[]
      Overlays: Overlay[]
      Hash: StateHash
      RandomDraws: uint64 }

[<RequireQualifiedAccess>]
module Diagnostics =

    /// The four terrain layers, in a fixed order. Each is a fresh array; the
    /// authoritative terrain store is never aliased into the frame.
    let private terrainLayers (world: WorldState) : GridLayer[] =
        let t = world.Terrain
        let b = world.Bounds

        [| { Name = LayerName.Elevation
             Bounds = b
             Cells = Array.copy t.Elevation }
           { Name = LayerName.Passability
             Bounds = b
             Cells =
               t.Movement
               |> Array.map (function
                   | Passable -> 1
                   | Impassable -> 0) }
           { Name = LayerName.MovementCost
             Bounds = b
             Cells = Array.copy t.MoveCost }
           { Name = LayerName.Opacity
             Bounds = b
             Cells = t.Opaque |> Array.map (fun o -> if o then 1 else 0) } |]

    /// Directional cover markers in a stable order: cells row-major, then the
    /// four cardinals in `Direction.all` order. Only non-zero levels appear.
    let private coverEdges (world: WorldState) : EdgeMarker[] =
        let t = world.Terrain
        let b = world.Bounds

        [| for y in 0 .. b.Height - 1 do
               for x in 0 .. b.Width - 1 do
                   let c = { X = x; Y = y }

                   for d in Direction.all do
                       let v = Terrain.cover t c d

                       if v > 0 then
                           { Cell = c; Direction = d; Value = v } |]

    let private agentMarkers (world: WorldState) : AgentMarker[] =
        world.Agents
        |> Array.sortBy (fun a -> a.Id)
        |> Array.map (fun a ->
            { Id = a.Id
              Side = a.Side
              Cell = a.Position
              Progress = a.Progress
              Destination = a.Destination
              CommunicationAvailable = a.CommunicationAvailable })

    let private eventMarker (e: DomainEvent) : EventMarker =
        match e.Body with
        | CommandAccepted(_, _, dest) -> { Kind = "command-accepted"; Cells = [| dest |] }
        | CommandRejected(_, UnknownAgent _) -> { Kind = "command-rejected"; Cells = [||] }
        | CommandRejected(_, EmptyRecipients) -> { Kind = "command-rejected"; Cells = [||] }
        | CommandRejected(_, DuplicateRecipient _) -> { Kind = "command-rejected"; Cells = [||] }
        | CommandRejected(_, UnauthorisedRecipient _) -> { Kind = "command-rejected"; Cells = [||] }
        | CommandRejected(_, DuplicateCommandId _) -> { Kind = "command-rejected"; Cells = [||] }
        | CommandRejected(_, IssueTickOutOfRange _) -> { Kind = "command-rejected"; Cells = [||] }
        | CommandRejected(_, TargetOutOfBounds target) -> { Kind = "command-rejected"; Cells = [| target |] }
        | OrderUndelivered _ -> { Kind = "order-undelivered"; Cells = [||] }
        | OrderAppraised _ -> { Kind = "order-appraised"; Cells = [||] }
        | CommitmentEstablished(_, _, target) -> { Kind = "commitment-established"; Cells = [| target |] }
        | CommitmentCompleted(_, _, at) -> { Kind = "commitment-completed"; Cells = [| at |] }
        | ShotFired(_, _, hit) ->
            { Kind = (if hit then "shot-fired-hit" else "shot-fired-miss")
              Cells = [||] }
        | MovementStepped(_, from, into) -> { Kind = "movement-stepped"; Cells = [| from; into |] }
        | MovementCompleted(_, at) -> { Kind = "movement-completed"; Cells = [| at |] }
        | MovementBlocked(_, at, target) -> { Kind = "movement-blocked"; Cells = [| at; target |] }
        | MovementYielded(_, at, contested, _) -> { Kind = "movement-yielded"; Cells = [| at; contested |] }
        | MovementObstructed(_, at, blocked, _) -> { Kind = "movement-obstructed"; Cells = [| at; blocked |] }
        | ContactObserved(_, _, at) -> { Kind = "contact-observed"; Cells = [| at |] }
        | ContactExpired(_, lastKnownCell) -> { Kind = "contact-expired"; Cells = [| lastKnownCell |] }

    /// A `KnownContact` overlay per contact in the friendly squad's shared
    /// tactical picture (TASK-026), ascending by contact id. Reads
    /// `WorldState.TacticalKnowledge`, which is genuine canonical state, so
    /// both `frame` and `frameOf` derive this — unlike `Reserved` /
    /// `Obstructed`, which only a completed step can produce.
    let private knownContactOverlays (world: WorldState) : Overlay[] =
        world.TacticalKnowledge
        |> Array.sortBy (fun c -> c.Contact)
        |> Array.map (fun c -> KnownContact(c.LastKnownCell, c.Contact, c.Confidence, c.LastSeenTick))

    /// An `OrderAppraisal` overlay per agent whose `Order` and `Disposition`
    /// are both `Some` (TASK-028), ascending by agent id. The appraisal
    /// outcome is standing canonical `AgentState` state, so both `frame` and
    /// `frameOf` derive this — the `KnownContact` precedent. `exposedCells` is
    /// recomputed from the agent's current `Position` via `Appraisal.appraise`
    /// (a pure leaf call, no mutation); the overlay reports the **stored**
    /// disposition, which for an `Accepted` agent that has moved may no longer
    /// match a fresh appraisal (the reappraisal triggers that would update it
    /// are B-021).
    let private orderAppraisalOverlays (world: WorldState) : Overlay[] =
        let budget = world.Bounds.Width * world.Bounds.Height

        world.Agents
        |> Array.sortBy (fun a -> a.Id)
        |> Array.choose (fun a ->
            match a.Order, a.Disposition with
            | Some o, Some d ->
                let _, exposed =
                    Appraisal.appraise
                        world.Terrain
                        world.TacticalKnowledge
                        a.Discipline
                        a.Stress
                        a.SuppressionBand
                        o
                        a.Position
                        budget

                Some(OrderAppraisal(a.Id, a.Position, d, exposed))
            | _ -> None)

    /// An `AgentCommitment` overlay per agent (TASK-030), ascending by agent
    /// id. `Commitment` is derived, not stored (`Commitment.fs`), so both
    /// `frame` and `frameOf` derive this from bare authoritative state — the
    /// `OrderAppraisal` precedent.
    let private commitmentOverlays (world: WorldState) : Overlay[] =
        world.Agents
        |> Array.sortBy (fun a -> a.Id)
        |> Array.map (fun a -> AgentCommitment(a.Id, a.Position, Commitment.ofAgent a.Order a.Disposition a.Destination))

    /// An `AgentSuppression` overlay per agent with non-zero `Suppression`
    /// (TASK-032), ascending by agent id. Standing canonical `AgentState`
    /// state, so both `frame` and `frameOf` derive this — the `KnownContact`
    /// precedent.
    let private suppressionOverlays (world: WorldState) : Overlay[] =
        world.Agents
        |> Array.sortBy (fun a -> a.Id)
        |> Array.choose (fun a ->
            if a.Suppression > 0 then
                Some(AgentSuppression(a.Id, a.Position, a.Suppression))
            else
                None)

    /// An `AgentStress` overlay per agent with non-zero `Stress` (TASK-033),
    /// ascending by agent id. The `AgentSuppression` precedent exactly.
    let private stressOverlays (world: WorldState) : Overlay[] =
        world.Agents
        |> Array.sortBy (fun a -> a.Id)
        |> Array.choose (fun a -> if a.Stress > 0 then Some(AgentStress(a.Id, a.Position, a.Stress)) else None)

    /// A `HostileKnownContact` overlay per contact in the Hostile side's own
    /// shared tactical picture (TASK-034), ascending by contact id — the
    /// `knownContactOverlays` precedent, reading
    /// `WorldState.HostileTacticalKnowledge` instead.
    let private hostileKnownContactOverlays (world: WorldState) : Overlay[] =
        world.HostileTacticalKnowledge
        |> Array.sortBy (fun c -> c.Contact)
        |> Array.map (fun c -> HostileKnownContact(c.LastKnownCell, c.Contact, c.Confidence, c.LastSeenTick))

    /// The diagnostic frame for a world state. Total, pure, deterministic:
    /// no mutation, no random draw, no wall-clock read. `Events` is empty
    /// (a bare `WorldState` carries no per-tick event history); use
    /// `frameOf` for the this-tick event markers. `Overlays` carries the
    /// `KnownContact` set (the squad picture is on `WorldState`) and, since
    /// TASK-034, the `HostileKnownContact` set for the Hostile side's own
    /// picture.
    let frame (world: WorldState) : DiagnosticFrame =
        { Tick = world.Tick
          Bounds = world.Bounds
          Layers = terrainLayers world
          Edges = coverEdges world
          Agents = agentMarkers world
          Events = [||]
          Overlays =
            [| knownContactOverlays world
               orderAppraisalOverlays world
               commitmentOverlays world
               suppressionOverlays world
               stressOverlays world
               hostileKnownContactOverlays world |]
            |> Array.concat
          Hash = Hashing.hash world
          RandomDraws = world.Random.Draws }

    /// A `PlannedPath` overlay per agent following a route, in ascending agent
    /// id order. `from` is the cell the path was planned from, `target` the
    /// destination, `cells` the whole 4-connected path, `cost` its
    /// `Pathfinding` integer cost, `reached` always true (a stored route always
    /// has a path). Reads the non-canonical `AgentState.Route` cache; emits
    /// nothing for agents at rest.
    let private routeOverlays (world: WorldState) : Overlay[] =
        world.Agents
        |> Array.sortBy (fun a -> a.Id)
        |> Array.choose (fun a ->
            match a.Route with
            | Some r when r.Cells.Length >= 2 ->
                Some(PlannedPath(r.Cells.[0], r.Cells.[r.Cells.Length - 1], r.Cells, r.Cost, true))
            | _ -> None)

    /// A `Reserved` overlay per cell contested this tick (TASK-017), derived
    /// from this tick's `MovementYielded` events — one entry per distinct
    /// contested cell, in the order its first `MovementYielded` event
    /// appears (ascending agent id, the standing movement-event order).
    let private reservationOverlays (result: StepResult) : Overlay[] =
        result.Events
        |> Array.choose (fun e ->
            match e.Body with
            | MovementYielded(_, _, contested, winner) -> Some(contested, winner)
            | CommandAccepted _
            | CommandRejected _
            | OrderUndelivered _
            | OrderAppraised _
            | CommitmentEstablished _
            | CommitmentCompleted _
            | ShotFired _
            | MovementStepped _
            | MovementCompleted _
            | MovementBlocked _
            | MovementObstructed _
            | ContactObserved _
            | ContactExpired _ -> None)
        |> Array.distinctBy fst
        |> Array.map (fun (cell, winner) -> Reserved(cell, winner, result.State.Tick))

    /// An `Obstructed` overlay per cell an agent was held out of this tick
    /// (TASK-022), derived from this tick's `MovementObstructed` events — one
    /// entry per distinct blocked cell, in the order its first
    /// `MovementObstructed` event appears (ascending agent id). The
    /// `reservationOverlays` precedent.
    let private obstructionOverlays (result: StepResult) : Overlay[] =
        result.Events
        |> Array.choose (fun e ->
            match e.Body with
            | MovementObstructed(_, _, blocked, occupant) -> Some(blocked, occupant)
            | CommandAccepted _
            | CommandRejected _
            | OrderUndelivered _
            | OrderAppraised _
            | CommitmentEstablished _
            | CommitmentCompleted _
            | ShotFired _
            | MovementStepped _
            | MovementCompleted _
            | MovementBlocked _
            | MovementYielded _
            | ContactObserved _
            | ContactExpired _ -> None)
        |> Array.distinctBy fst
        |> Array.map (fun (cell, occupant) -> Obstructed(cell, occupant))

    /// An `UndeliveredOrder` overlay per recipient that could not be reached
    /// this tick (TASK-027), derived from this tick's `OrderUndelivered`
    /// events — one entry per distinct recipient, in the order its first
    /// `OrderUndelivered` event appears (ascending `(recipient, command)` id,
    /// the Communication-phase emission order). The recipient's cell is read
    /// from the post-step world (a blacked-out recipient did not move this
    /// tick). The `obstructionOverlays` precedent.
    let private undeliveredOrderOverlays (result: StepResult) : Overlay[] =
        result.Events
        |> Array.choose (fun e ->
            match e.Body with
            | OrderUndelivered(command, recipient, _) -> Some(recipient, command)
            | CommandAccepted _
            | CommandRejected _
            | OrderAppraised _
            | CommitmentEstablished _
            | CommitmentCompleted _
            | ShotFired _
            | MovementStepped _
            | MovementCompleted _
            | MovementBlocked _
            | MovementYielded _
            | MovementObstructed _
            | ContactObserved _
            | ContactExpired _ -> None)
        |> Array.distinctBy fst
        |> Array.choose (fun (recipient, command) ->
            result.State.Agents
            |> Array.tryFind (fun a -> a.Id = recipient)
            |> Option.map (fun a -> UndeliveredOrder(recipient, a.Position, command)))

    /// A `FireLine` overlay per shot fired this tick (TASK-031), derived from
    /// this tick's `ShotFired` events, in emission order (ascending shooter
    /// agent id). Both cells are read from the post-step world. The
    /// `undeliveredOrderOverlays` precedent.
    let private fireLineOverlays (result: StepResult) : Overlay[] =
        result.Events
        |> Array.choose (fun e ->
            match e.Body with
            | ShotFired(shooter, target, hit) -> Some(shooter, target, hit)
            | CommandAccepted _
            | CommandRejected _
            | OrderUndelivered _
            | OrderAppraised _
            | CommitmentEstablished _
            | CommitmentCompleted _
            | MovementStepped _
            | MovementCompleted _
            | MovementBlocked _
            | MovementYielded _
            | MovementObstructed _
            | ContactObserved _
            | ContactExpired _ -> None)
        |> Array.choose (fun (shooter, target, hit) ->
            match
                result.State.Agents |> Array.tryFind (fun a -> a.Id = shooter),
                result.State.Agents |> Array.tryFind (fun a -> a.Id = target)
            with
            | Some s, Some t -> Some(FireLine(shooter, s.Position, target, t.Position, hit))
            | _ -> None)

    /// The diagnostic frame for a completed step: the frame of the resulting
    /// world, plus this tick's event markers, a `PlannedPath` overlay for every
    /// agent still following a route, a `Reserved` overlay for every cell
    /// contested this tick, an `Obstructed` overlay for every cell an agent was
    /// held out of this tick, an `UndeliveredOrder` overlay per recipient an
    /// order failed to reach this tick, a `KnownContact` overlay per squad
    /// contact, an `AgentCommitment` overlay per agent (TASK-030), a
    /// `FireLine` overlay per shot fired this tick (TASK-031), an
    /// `AgentSuppression` overlay per agent with non-zero suppression
    /// (TASK-032), an `AgentStress` overlay per agent with non-zero stress
    /// (TASK-033), a `HostileKnownContact` overlay per Hostile-picture contact
    /// (TASK-034), and the post-step canonical hash recorded on the
    /// `StepResult`. Total, pure, deterministic.
    let frameOf (result: StepResult) : DiagnosticFrame =
        { frame result.State with
            Events = result.Events |> Array.map eventMarker
            Overlays =
                Array.concat
                    [ routeOverlays result.State
                      reservationOverlays result
                      obstructionOverlays result
                      undeliveredOrderOverlays result
                      knownContactOverlays result.State
                      orderAppraisalOverlays result.State
                      commitmentOverlays result.State
                      fireLineOverlays result
                      suppressionOverlays result.State
                      stressOverlays result.State
                      hostileKnownContactOverlays result.State ]
            Hash = result.StateHash }
