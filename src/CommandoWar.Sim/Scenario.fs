namespace CommandoWar.Sim

/// Framework-neutral authored-scenario model, its content-format version, and
/// one-pass validation (ADR-0002 "Content DTOs -> validation -> Sim setup";
/// docs/03_ARCHITECTURE.md sections 16-17; docs/06_CONTENT_AND_PRESENTATION.md
/// sections 3, 7; docs/04_SIMULATION_SPEC.md section 21).
///
/// This module carries authored *positions and references* plus an optional
/// authored terrain layer (elevation, passability, movement cost, opacity,
/// directional low cover; realised by TASK-010, backlog B-008). Line of
/// sight (B-009) and pathfinding (B-010) are still absent, and no tick phase
/// consumes the terrain grid the layer produces.
///
/// Objective *evaluation* and mission success / failure are deferred (backlog
/// B-032). The `Objective` algebra below is a data-only type: nothing in the
/// phase pipeline reads it yet.
///
/// The module is isolated in the same way `Replay.fs` and `Canonical.fs` are:
/// a different authored shape or a content reader (a Godot `.tscn` reader, a
/// Tiled importer) sits in front of `RawScenario` without changing anything
/// here, and nothing in `Simulation.step` or `Canonical.encode` depends on
/// these types.

// --- content ids ---------------------------------------------------------
// Same discipline as Ids.fs: private constructors, structural equality and
// ordering, minted through smart constructors that reject malformed input.

/// Identifies one authored scenario.
[<Struct>]
type ScenarioId = private ScenarioId of string

/// Identifies one objective within a scenario.
[<Struct>]
type ObjectiveId = private ObjectiveId of int

/// Identifies one authored area (an objective area or an extraction area).
[<Struct>]
type AreaId = private AreaId of string

/// Identifies one authored static target.
[<Struct>]
type TargetId = private TargetId of string

[<RequireQualifiedAccess>]
module ScenarioId =

    /// Creates a scenario id from a non-blank string.
    let ofString (value: string) : ScenarioId =
        if System.String.IsNullOrWhiteSpace value then
            invalidArg (nameof value) "ScenarioId must not be blank"

        ScenarioId value

    /// The underlying string. For diagnostics and content round-tripping only.
    let value (ScenarioId v) : string = v

[<RequireQualifiedAccess>]
module ObjectiveId =

    /// Creates an objective id from a non-negative integer.
    let ofInt (value: int) : ObjectiveId =
        if value < 0 then
            invalidArg (nameof value) "ObjectiveId must be non-negative"

        ObjectiveId value

    /// The underlying integer. For explicit ordering and diagnostics only.
    let value (ObjectiveId v) : int = v

[<RequireQualifiedAccess>]
module AreaId =

    /// Creates an area id from a non-blank string.
    let ofString (value: string) : AreaId =
        if System.String.IsNullOrWhiteSpace value then
            invalidArg (nameof value) "AreaId must not be blank"

        AreaId value

    /// The underlying string. For diagnostics and content round-tripping only.
    let value (AreaId v) : string = v

[<RequireQualifiedAccess>]
module TargetId =

    /// Creates a target id from a non-blank string.
    let ofString (value: string) : TargetId =
        if System.String.IsNullOrWhiteSpace value then
            invalidArg (nameof value) "TargetId must not be blank"

        TargetId value

    /// The underlying string. For diagnostics and content round-tripping only.
    let value (TargetId v) : string = v

/// The authored-scenario content-format version.
///
/// This is independent of `Canonical.FormatVersion` (the authoritative-state
/// byte layout, docs/04 section 17) and `Replay.FormatVersion` (the replay
/// container, docs/04 section 16): it versions the authored input shape
/// (`RawScenario`) and the validation contract, not the state encoding. Bump
/// it when either the authored shape or a validation rule changes.
///
/// Version 2 (TASK-010) added the optional authored terrain layer and its
/// validation. Version 3 (TASK-047, backlog B-030 proper) added
/// `ResupplyAreas` — the first authored area type an actual phase consumes
/// (`Simulation.stateConsequences`'s ammo-resupply check), unlike
/// `ObjectiveAreas`/`ExtractionAreas`, still unread (B-032). Version 4
/// (TASK-049, backlog B-058) added the authored unit-type table
/// (`RawScenario.UnitTypes`) and each deployment's `UnitType` reference,
/// the source of `Deployment.MoveSpeed` / `AgentState.MoveSpeed`. Version 5
/// (TASK-058, backlog B-016b) added the optional authored `Headquarters`
/// command-origin cell and the authored `Jammers` table -- both optional
/// (an absent `Headquarters` opts a scenario out of the whole range/delay/
/// jamming/radio-destroyed feature set, `Communication.available`'s own
/// doc comment). A version-1-2-3-or-4 scenario is rejected, not migrated
/// (`docs/04` section 16: "does not guess migrations").
[<RequireQualifiedAccess>]
module ScenarioContent =

    [<Literal>]
    let Version = 5

// --- validated model ---------------------------------------------------

/// One deployed agent in a validated scenario. Side is fixed by which
/// deployment list the agent appears in.
type Deployment =
    { Agent: AgentId
      Side: Side
      Cell: Cell
      /// Whether an order issued to this agent reaches it (TASK-027, backlog
      /// B-016). Default `true`; an authored `false` is a "comms blackout"
      /// that makes the Communication phase emit `OrderUndelivered` for this
      /// recipient. Static — carried onto `AgentState.CommunicationAvailable`
      /// by `World.ofScenario` and never mutated during a run.
      CommunicationAvailable: bool
      /// This agent's discipline (TASK-028, backlog B-017; `docs/05` section
      /// 8). A non-negative integer read only by the Appraisal phase's
      /// stage-4 resolve threshold. Static — carried onto
      /// `AgentState.Discipline` by `World.ofScenario` and never mutated
      /// during a run. `Scenario.validate` rejects a negative value
      /// (`NegativeDiscipline`).
      Discipline: int
      /// This agent's movement speed (TASK-049, backlog B-058): a value
      /// `Simulation.navigationAndMovement` cross-multiplies against
      /// `Agent.MoveSpeedDefault` in its edge-completion comparison (the
      /// per-tick progress increment itself stays the universal
      /// `Terrain.BaseMoveCost` for every agent) — a value equal to
      /// `Agent.MoveSpeedDefault` reproduces the pre-TASK-049 comparison
      /// byte-for-byte; a smaller value genuinely needs proportionally more
      /// ticks to cross the same cell. Resolved from the authored
      /// `RawDeployment.UnitType` reference against `RawScenario.UnitTypes`
      /// at validation time — the `RawTerrainCell.Class` precedent: the raw
      /// string reference does not survive past `Scenario.validate`, only
      /// this baked scalar does. Static — carried onto `AgentState.MoveSpeed`
      /// by `World.ofScenario` and never mutated during a run.
      MoveSpeed: int }

/// A named point of interest: an objective area or an extraction area. The
/// slice needs a single cell per area; a rectangular region is a later
/// refinement if the mission proves the need.
type Area = { Id: AreaId; Cell: Cell }

/// A static objective target: the bridge, a machine-gun position. A single
/// cell; destruction state is deferred with combat (B-019).
type StaticTarget = { Id: TargetId; Cell: Cell }

/// Which agents an extraction objective requires (docs/07 section 3,
/// "surviving required personnel").
type AgentSelection =
    | AllFriendlyAgents
    | SpecificAgents of AgentId[]

/// The Bridgehead objective algebra (docs/06 section 3, docs/07 section 3).
///
/// EVALUATION IS DEFERRED (backlog B-032). Nothing in the simulation reads
/// this type yet; it exists so authored content can express the mission
/// shape. The slice's mission is the implicit "all of" a scenario's
/// `Objectives` array; `AllOf` / `Optional` are in the algebra for nested
/// composition when a later task needs them. The bridge-demolition
/// interaction is `ReachArea` then a fixed-duration plant (a later task) then
/// `DestroyTarget`; there is no general interaction scripting language.
type Objective =
    | ReachArea of objective: ObjectiveId * area: AreaId
    | HoldArea of objective: ObjectiveId * area: AreaId * ticks: int
    | DestroyTarget of objective: ObjectiveId * target: TargetId
    | ExtractAgents of objective: ObjectiveId * agents: AgentSelection * area: AreaId
    | AllOf of objective: ObjectiveId * parts: Objective[]
    | Optional of Objective

/// Scenario-wide rules. Evaluation is deferred with the mission task (B-032);
/// this is data only. One field, exercised by the slice's squad-loss failure
/// condition (docs/07 section 9, criterion 7).
type ScenarioRules = { FailOnFriendlyForceEliminated: bool }

/// A validated authored scenario.
///
/// Construct only through `Scenario.validate`: by the time a `Scenario`
/// exists every id is well formed, every objective reference resolves, and
/// every deployment, area, and target lies inside the map. The record
/// constructor is public for pattern access and testing, exactly as the
/// framework spikes' validated `Scenario` / `ScenarioDto` are.
type Scenario =
    { Id: ScenarioId
      /// Map dimensions. Per-cell terrain data lives in `Terrain`.
      Map: GridBounds
      /// The validated authoritative terrain grid. When the raw scenario
      /// authored no terrain layer this is `Terrain.empty Map` (flat, fully
      /// passable, transparent, uncovered).
      Terrain: Terrain
      FriendlyDeployments: Deployment[]
      EnemyDeployments: Deployment[]
      ObjectiveAreas: Area[]
      ExtractionAreas: Area[]
      /// Authored resupply-cache cells (TASK-047, backlog B-030 proper).
      /// Unlike `ObjectiveAreas`/`ExtractionAreas`, consumed by
      /// `Simulation.stateConsequences`: an agent standing on one of these
      /// cells is refilled to full ammunition. Carried onto
      /// `WorldState.ResupplyAreas` by `World.ofScenario`.
      ResupplyAreas: Area[]
      StaticTargets: StaticTarget[]
      Objectives: Objective[]
      Rules: ScenarioRules
      /// The authored command-origin cell (TASK-058, backlog B-016b), or
      /// `None`. Carried onto `WorldState.Headquarters` by `World.ofScenario`;
      /// excluded from `Canonical.encode` (the `ResupplyAreas`/`Terrain`
      /// precedent -- static authored data, not per-tick state).
      Headquarters: Cell option
      /// Authored jammers (TASK-058, backlog B-016b). Carried onto
      /// `WorldState.Jammers`; excluded from `Canonical.encode` the same way.
      Jammers: Jammer[] }

// --- raw (unvalidated) input -----------------------------------------

/// Unvalidated authored deployment: an agent id, a cell, whether the agent
/// can receive orders (`CommunicationAvailable`, TASK-027 — `true` for an
/// ordinary deployment, `false` for an authored comms blackout), the
/// agent's `Discipline` (TASK-028 — a non-negative integer; the Appraisal
/// phase's stage-4 resolve input), and the agent's `UnitType` (TASK-049,
/// backlog B-058 — an id referencing one of `RawScenario.UnitTypes`, the
/// source of `Deployment.MoveSpeed`).
type RawDeployment =
    { AgentId: int
      Cell: Cell
      CommunicationAvailable: bool
      Discipline: int
      UnitType: string }

/// Unvalidated authored area marker.
type RawArea = { AreaId: string; Cell: Cell }

/// Unvalidated authored static-target marker.
type RawTarget = { TargetId: string; Cell: Cell }

/// Unvalidated authored objective. `Kind` is one of "reach", "hold",
/// "destroy", "extract" and selects which other fields are read; an unknown
/// value is a validation error. Nested composition (`AllOf`) is not
/// expressible here: the mission is the implicit all-of of the list.
type RawObjective =
    { Id: int
      Kind: string
      /// Referenced area id for "reach", "hold", "extract"; ignored otherwise.
      AreaRef: string
      /// Referenced target id for "destroy"; ignored otherwise.
      TargetRef: string
      /// Hold duration in ticks for "hold"; ignored otherwise.
      HoldTicks: int
      /// Agent ids for "extract"; empty means all friendly agents.
      ExtractAgentIds: int[]
      IsOptional: bool }

/// One authored terrain-cell override. Cells a layer does not mention are
/// open, flat (elevation 0), transparent, and cost `Terrain.BaseMoveCost`.
/// `Class` is `"passable"` or `"impassable"`; any other value is
/// `UnknownTerrainClass`. `Elevation` must be non-negative. `MoveCost` is
/// ignored for an impassable cell; for a passable cell it must lie within
/// `[Terrain.BaseMoveCost, Terrain.MaxMoveCost]` (a cheaper step would break
/// the pathfinding heuristic's admissibility; a dearer one risks the
/// `Terrain.BlockedCost` sentinel and cost overflow — TASK-021). `Opaque` is
/// the high-occlusion flag (docs/06 section 4).
type RawTerrainCell =
    { Cell: Cell
      Class: string
      Elevation: int
      MoveCost: int
      Opaque: bool }

/// One authored low-cover value (docs/06 section 4 "Low cover"): `Cell`
/// gains cover of `Level` against attacks arriving from `Direction`.
/// `Direction` is `"north"`, `"east"`, `"south"`, or `"west"`; any other
/// value is `UnknownCoverClass`. `Level` must be non-negative.
type RawCoverFeature =
    { Cell: Cell
      Direction: string
      Level: int }

/// An authored terrain layer. `Width` and `Height` must equal the
/// scenario's map dimensions. Cells and cover entries are sparse: only
/// non-default cells need appear. An absent layer (`RawScenario.TerrainLayer
/// = None`) is legal and means empty terrain.
type RawTerrainLayer =
    { Width: int
      Height: int
      Cells: RawTerrainCell[]
      Cover: RawCoverFeature[] }

/// One authored unit type (TASK-049, backlog B-058): `Id` is the token a
/// `RawDeployment.UnitType` references; `MoveSpeed` must be a positive
/// integer, carried forward as `Deployment.MoveSpeed` / `AgentState.
/// MoveSpeed` (`Agent.MoveSpeedDefault` reproduces the pre-TASK-049 pace
/// exactly; a smaller value is genuinely slower). Like `RawTerrainCell.
/// Class`, this table does not survive past `Scenario.validate` — only the
/// resolved `Deployment.MoveSpeed` scalar does.
type RawUnitType = { Id: string; MoveSpeed: int }

/// One authored jammer (TASK-058, backlog B-016b): a recipient within
/// `Radius` Chebyshev cells of `Position` cannot receive an order while the
/// current tick lies in `[ActiveFromTick, ActiveUntilTick]` (both
/// inclusive) -- the "dynamic" part of dynamic jamming, since a scenario
/// can author it to turn on and off over a run, even though nothing can
/// destroy a jammer yet this task. `Radius` must be non-negative;
/// `ActiveFromTick`/`ActiveUntilTick` must both be non-negative with
/// `ActiveFromTick <= ActiveUntilTick`.
type RawJammer =
    { Position: Cell
      Radius: int
      ActiveFromTick: int64
      ActiveUntilTick: int64 }

/// The whole unvalidated authored scenario, as a content reader (a Godot
/// `.tscn` reader, a Tiled importer, or a test) produces it. Every field is a
/// primitive, an array of primitives, or the optional terrain layer, so the
/// reader depends on nothing in the validated model.
type RawScenario =
    { ContentVersion: int
      Id: string
      Width: int
      Height: int
      FriendlyDeployments: RawDeployment[]
      EnemyDeployments: RawDeployment[]
      ObjectiveAreas: RawArea[]
      ExtractionAreas: RawArea[]
      /// Authored resupply-cache markers (TASK-047, backlog B-030 proper).
      ResupplyAreas: RawArea[]
      StaticTargets: RawTarget[]
      Objectives: RawObjective[]
      /// The authored terrain layer, or `None` for empty terrain.
      TerrainLayer: RawTerrainLayer option
      /// Authored unit types (TASK-049, backlog B-058), referenced by each
      /// `RawDeployment.UnitType`. Every deployment must reference a defined
      /// entry here — no silent default (`DeploymentReferencesUnknownUnitType`).
      UnitTypes: RawUnitType[]
      /// The authored command-origin cell (TASK-058, backlog B-016b), or
      /// `None` when the scenario authors no comms-degradation model at all
      /// -- the opt-in gate for the whole range/delay/jamming/radio-destroyed
      /// feature set (`Communication.available`).
      Headquarters: Cell option
      /// Authored jammers (TASK-058, backlog B-016b). Empty for the
      /// overwhelming majority of scenarios.
      Jammers: RawJammer[]
      FailOnFriendlyForceEliminated: bool }

/// Why a raw scenario is invalid (docs/03 section 17, docs/06 section 7).
///
/// Reported in the `ReplayError` style: `Scenario.validate` returns the full
/// list in one pass, each case naming the offending object and, where
/// relevant, the expected value. No silent default is ever supplied for
/// invalid data.
type ScenarioError =
    | UnsupportedContentVersion of found: int * supported: int
    | BlankScenarioId
    | NonPositiveMapDimensions of width: int * height: int
    | DuplicateDeploymentId of agent: int
    | NegativeDeploymentId of agent: int
    /// An authored `Discipline` below 0 (TASK-028). Discipline is a
    /// non-negative resolve-threshold input; a negative value has no meaning.
    | NegativeDiscipline of agent: int * value: int
    | DeploymentOutOfMap of agent: int * cell: Cell * bounds: GridBounds
    | DeploymentCellShared of cell: Cell * agents: int list
    | DuplicateObjectiveId of objective: int
    | NegativeObjectiveId of objective: int
    | DuplicateAreaId of area: string
    | DuplicateTargetId of target: string
    | BlankAreaId of cell: Cell
    | BlankTargetId of cell: Cell
    | AreaMarkerOutOfMap of area: string * cell: Cell * bounds: GridBounds
    | TargetMarkerOutOfMap of target: string * cell: Cell * bounds: GridBounds
    | UnknownObjectiveKind of objective: int * kind: string
    | ObjectiveReferencesMissingArea of objective: int * area: string
    | ObjectiveReferencesMissingTarget of objective: int * target: string
    | ExtractionSelectsUnknownAgent of objective: int * agent: int
    | MissingRequiredMarker of marker: string
    // --- terrain layer (ScenarioContent.Version 2, TASK-010) ---
    | TerrainLayerDimensionsMismatch of layer: GridBounds * map: GridBounds
    | TerrainFeatureOutOfMap of cell: Cell * bounds: GridBounds
    | DuplicateTerrainCell of cell: Cell
    | DuplicateCoverFeature of cell: Cell * direction: string
    | UnknownTerrainClass of cell: Cell * className: string
    | UnknownCoverClass of cell: Cell * className: string
    | NegativeElevation of cell: Cell * level: int
    | NegativeMoveCost of cell: Cell * cost: int
    /// A passable cell whose authored `MoveCost` is `>= 0` but outside
    /// `[min, max]` = `[Terrain.BaseMoveCost, Terrain.MaxMoveCost]`. A
    /// negative cost is the more specific `NegativeMoveCost`; an impassable
    /// cell's cost is ignored and never reported here (TASK-021).
    | MoveCostOutOfRange of cell: Cell * cost: int * min: int * max: int
    | NegativeCoverLevel of cell: Cell * level: int
    | DeploymentOnImpassableCell of agent: int * cell: Cell
    // --- unit types (ScenarioContent.Version 4, TASK-049, backlog B-058) ---
    | BlankUnitTypeId
    | DuplicateUnitTypeId of unitType: string
    /// An authored `RawUnitType.MoveSpeed` at or below 0. A non-positive
    /// speed would never let an agent's `Progress` reach any cell's
    /// completion threshold — a permanently frozen agent has no meaning.
    | NonPositiveUnitTypeMoveSpeed of unitType: string * value: int
    /// A `RawDeployment.UnitType` naming no entry in `RawScenario.UnitTypes`
    /// — no silent default is supplied.
    | DeploymentReferencesUnknownUnitType of agent: int * unitType: string
    // --- headquarters and jammers (ScenarioContent.Version 5, TASK-058, backlog B-016b) ---
    | HeadquartersOutOfMap of cell: Cell * bounds: GridBounds
    | JammerOutOfMap of index: int * cell: Cell * bounds: GridBounds
    /// An authored `RawJammer.Radius` below 0. A negative radius has no
    /// meaning (the `NegativeDiscipline`/`NegativeElevation` precedent).
    | NegativeJammerRadius of index: int * radius: int
    /// An authored jammer whose `ActiveFromTick`/`ActiveUntilTick` are
    /// negative, or where `ActiveFromTick > ActiveUntilTick` (an empty or
    /// backwards window has no meaning).
    | InvalidJammerWindow of index: int * fromTick: int64 * untilTick: int64

[<RequireQualifiedAccess>]
module Scenario =

    /// Values that appear more than once in `xs`, each reported once, in
    /// ascending order. Sort-based so the result never depends on hash-set
    /// enumeration order (docs/09 section 3).
    let private repeated (xs: 'a[]) : 'a[] =
        xs
        |> Array.sort
        |> Array.pairwise
        |> Array.choose (fun (a, b) -> if a = b then Some a else None)
        |> Array.distinct

    /// Non-blank ids, in original order.
    let private presentAreaIds (areas: RawArea[]) : string[] =
        areas
        |> Array.choose (fun a -> if System.String.IsNullOrWhiteSpace a.AreaId then None else Some a.AreaId)

    let private presentTargetIds (targets: RawTarget[]) : string[] =
        targets
        |> Array.choose (fun t -> if System.String.IsNullOrWhiteSpace t.TargetId then None else Some t.TargetId)

    /// The authored terrain-class token, or `None` for an unknown value.
    let private parseMovementClass (s: string) : MovementClass option =
        match s with
        | "passable" -> Some Passable
        | "impassable" -> Some Impassable
        | _ -> None

    /// The authored cover-direction token, or `None` for an unknown value.
    let private parseDirection (s: string) : Direction option =
        match s with
        | "north" -> Some North
        | "east" -> Some East
        | "south" -> Some South
        | "west" -> Some West
        | _ -> None

    /// Validates a raw authored scenario, collecting every fault in one pass.
    /// On success every id, reference, and position in the returned `Scenario`
    /// is known good.
    let validate (raw: RawScenario) : Result<Scenario, ScenarioError list> =
        let errors = ResizeArray<ScenarioError>()
        let report e = errors.Add e

        if raw.ContentVersion <> ScenarioContent.Version then
            report (UnsupportedContentVersion(raw.ContentVersion, ScenarioContent.Version))

        if System.String.IsNullOrWhiteSpace raw.Id then
            report BlankScenarioId

        let bounds: GridBounds = { Width = raw.Width; Height = raw.Height }
        let mapOk = raw.Width > 0 && raw.Height > 0

        if not mapOk then
            report (NonPositiveMapDimensions(raw.Width, raw.Height))

        // --- unit types (ScenarioContent.Version 4, TASK-049, backlog B-058) ---
        // Validated ahead of deployments so the deployment loop below can
        // check each `UnitType` reference against a known-good id set.
        for u in raw.UnitTypes do
            if System.String.IsNullOrWhiteSpace u.Id then
                report BlankUnitTypeId

        for dup in repeated (raw.UnitTypes |> Array.map (fun u -> u.Id) |> Array.filter (fun id -> not (System.String.IsNullOrWhiteSpace id))) do
            report (DuplicateUnitTypeId dup)

        for u in raw.UnitTypes do
            if not (System.String.IsNullOrWhiteSpace u.Id) && u.MoveSpeed <= 0 then
                report (NonPositiveUnitTypeMoveSpeed(u.Id, u.MoveSpeed))

        let unitTypesById =
            raw.UnitTypes
            |> Array.filter (fun u -> not (System.String.IsNullOrWhiteSpace u.Id) && u.MoveSpeed > 0)
            |> Array.map (fun u -> u.Id, u.MoveSpeed)
            |> Map.ofArray

        // --- headquarters and jammers (ScenarioContent.Version 5, TASK-058, backlog B-016b) ---
        if mapOk then
            match raw.Headquarters with
            | Some cell when not (GridBounds.contains cell bounds) -> report (HeadquartersOutOfMap(cell, bounds))
            | _ -> ()

        raw.Jammers
        |> Array.iteri (fun i j ->
            if mapOk && not (GridBounds.contains j.Position bounds) then
                report (JammerOutOfMap(i, j.Position, bounds))

            if j.Radius < 0 then
                report (NegativeJammerRadius(i, j.Radius))

            if j.ActiveFromTick < 0L || j.ActiveUntilTick < 0L || j.ActiveFromTick > j.ActiveUntilTick then
                report (InvalidJammerWindow(i, j.ActiveFromTick, j.ActiveUntilTick)))

        // --- deployments (friendly then enemy) ---------------------------
        let deployments =
            Array.append
                (raw.FriendlyDeployments |> Array.map (fun d -> d, Friendly))
                (raw.EnemyDeployments |> Array.map (fun d -> d, Hostile))

        let deploymentIds = deployments |> Array.map (fun (d, _) -> d.AgentId)

        for id in repeated deploymentIds do
            report (DuplicateDeploymentId id)

        for id in deploymentIds |> Array.filter (fun i -> i < 0) |> Array.distinct |> Array.sort do
            report (NegativeDeploymentId id)

        for d, _ in deployments |> Array.sortBy (fun (d, _) -> d.AgentId) do
            if d.Discipline < 0 then
                report (NegativeDiscipline(d.AgentId, d.Discipline))

            if not (Map.containsKey d.UnitType unitTypesById) then
                report (DeploymentReferencesUnknownUnitType(d.AgentId, d.UnitType))

        if mapOk then
            for d, _ in deployments do
                if not (GridBounds.contains d.Cell bounds) then
                    report (DeploymentOutOfMap(d.AgentId, d.Cell, bounds))

        for cell in repeated (deployments |> Array.map (fun (d, _) -> d.Cell)) do
            let ids =
                deployments
                |> Array.choose (fun (d, _) -> if d.Cell = cell then Some d.AgentId else None)
                |> Array.sort
                |> Array.toList

            report (DeploymentCellShared(cell, ids))

        // --- area and target markers ------------------------------------
        // ResupplyAreas (TASK-047, backlog B-030 proper) shares the same
        // AreaId namespace and validation as ObjectiveAreas/ExtractionAreas
        // — a resupply cache is authored identically to an objective/
        // extraction marker, just consumed differently.
        let areaMarkers =
            Array.append (Array.append raw.ObjectiveAreas raw.ExtractionAreas) raw.ResupplyAreas

        for a in areaMarkers do
            if System.String.IsNullOrWhiteSpace a.AreaId then
                report (BlankAreaId a.Cell)

        for t in raw.StaticTargets do
            if System.String.IsNullOrWhiteSpace t.TargetId then
                report (BlankTargetId t.Cell)

        for dup in repeated (presentAreaIds areaMarkers) do
            report (DuplicateAreaId dup)

        for dup in repeated (presentTargetIds raw.StaticTargets) do
            report (DuplicateTargetId dup)

        if mapOk then
            for a in areaMarkers do
                if
                    not (System.String.IsNullOrWhiteSpace a.AreaId)
                    && not (GridBounds.contains a.Cell bounds)
                then
                    report (AreaMarkerOutOfMap(a.AreaId, a.Cell, bounds))

            for t in raw.StaticTargets do
                if
                    not (System.String.IsNullOrWhiteSpace t.TargetId)
                    && not (GridBounds.contains t.Cell bounds)
                then
                    report (TargetMarkerOutOfMap(t.TargetId, t.Cell, bounds))

        let knownAreaIds = presentAreaIds areaMarkers |> Set.ofArray
        let knownTargetIds = presentTargetIds raw.StaticTargets |> Set.ofArray

        let friendlyDeploymentIds =
            raw.FriendlyDeployments |> Array.map (fun d -> d.AgentId) |> Set.ofArray

        // --- objectives -----------------------------------------------
        for id in repeated (raw.Objectives |> Array.map (fun o -> o.Id)) do
            report (DuplicateObjectiveId id)

        for id in
            raw.Objectives
            |> Array.map (fun o -> o.Id)
            |> Array.filter (fun i -> i < 0)
            |> Array.distinct
            |> Array.sort do
            report (NegativeObjectiveId id)

        // Objectives that pass every check are built here; when the error list
        // is empty every objective was built, so the array below is complete.
        let built = ResizeArray<Objective>()

        for o in raw.Objectives do
            let idOk = o.Id >= 0
            let wrap (inner: Objective) = if o.IsOptional then Optional inner else inner

            match o.Kind with
            | "reach" ->
                if knownAreaIds.Contains o.AreaRef then
                    if idOk then
                        built.Add(wrap (ReachArea(ObjectiveId.ofInt o.Id, AreaId.ofString o.AreaRef)))
                else
                    report (ObjectiveReferencesMissingArea(o.Id, o.AreaRef))
            | "hold" ->
                if knownAreaIds.Contains o.AreaRef then
                    if idOk then
                        built.Add(wrap (HoldArea(ObjectiveId.ofInt o.Id, AreaId.ofString o.AreaRef, o.HoldTicks)))
                else
                    report (ObjectiveReferencesMissingArea(o.Id, o.AreaRef))
            | "destroy" ->
                if knownTargetIds.Contains o.TargetRef then
                    if idOk then
                        built.Add(wrap (DestroyTarget(ObjectiveId.ofInt o.Id, TargetId.ofString o.TargetRef)))
                else
                    report (ObjectiveReferencesMissingTarget(o.Id, o.TargetRef))
            | "extract" ->
                let areaKnown = knownAreaIds.Contains o.AreaRef

                if not areaKnown then
                    report (ObjectiveReferencesMissingArea(o.Id, o.AreaRef))

                let unknownAgents =
                    o.ExtractAgentIds
                    |> Array.filter (fun a -> a < 0 || not (friendlyDeploymentIds.Contains a))
                    |> Array.distinct
                    |> Array.sort

                for a in unknownAgents do
                    report (ExtractionSelectsUnknownAgent(o.Id, a))

                if areaKnown && Array.isEmpty unknownAgents && idOk then
                    let selection =
                        if Array.isEmpty o.ExtractAgentIds then
                            AllFriendlyAgents
                        else
                            SpecificAgents(o.ExtractAgentIds |> Array.map AgentId.ofInt)

                    built.Add(wrap (ExtractAgents(ObjectiveId.ofInt o.Id, selection, AreaId.ofString o.AreaRef)))
            | other -> report (UnknownObjectiveKind(o.Id, other))

        // --- required markers (docs/06 section 4, docs/07 section 3) ---
        if Array.isEmpty raw.FriendlyDeployments then
            report (MissingRequiredMarker "FriendlySpawn")

        if Array.isEmpty raw.Objectives then
            report (MissingRequiredMarker "Objective")

        if Array.isEmpty raw.ExtractionAreas then
            report (MissingRequiredMarker "ExtractionArea")

        // --- terrain layer (ScenarioContent.Version 2, TASK-010) -------
        // An absent layer means empty terrain and is not a fault. A present
        // layer is checked cell by cell and feature by feature; the parse
        // here is reused to build the Terrain in the Ok branch below. In the
        // fault-free case `Array.choose` drops nothing, so `authoredCells` /
        // `authoredCover` are complete and every cell is in bounds.
        let authoredCells, authoredCover =
            match raw.TerrainLayer with
            | None -> [||], [||]
            | Some layer ->
                if layer.Width <> raw.Width || layer.Height <> raw.Height then
                    report (
                        TerrainLayerDimensionsMismatch({ Width = layer.Width; Height = layer.Height }, bounds)
                    )

                for c in repeated (layer.Cells |> Array.map (fun tc -> tc.Cell)) do
                    report (DuplicateTerrainCell c)

                for c, d in repeated (layer.Cover |> Array.map (fun cf -> cf.Cell, cf.Direction)) do
                    report (DuplicateCoverFeature(c, d))

                for tc in layer.Cells do
                    if mapOk && not (GridBounds.contains tc.Cell bounds) then
                        report (TerrainFeatureOutOfMap(tc.Cell, bounds))

                    if Option.isNone (parseMovementClass tc.Class) then
                        report (UnknownTerrainClass(tc.Cell, tc.Class))

                    if tc.Elevation < 0 then
                        report (NegativeElevation(tc.Cell, tc.Elevation))

                    // A negative cost is always a fault; a passable cell must
                    // also stay within [BaseMoveCost, MaxMoveCost] so the A*
                    // heuristic stays admissible and cost accumulation stays
                    // inside int32 (TASK-021). An impassable cell's cost is
                    // ignored (`Terrain.moveCost` reports `BlockedCost`), so
                    // it is not range-checked.
                    if tc.MoveCost < 0 then
                        report (NegativeMoveCost(tc.Cell, tc.MoveCost))
                    elif
                        parseMovementClass tc.Class = Some Passable
                        && (tc.MoveCost < Terrain.BaseMoveCost || tc.MoveCost > Terrain.MaxMoveCost)
                    then
                        report (
                            MoveCostOutOfRange(tc.Cell, tc.MoveCost, Terrain.BaseMoveCost, Terrain.MaxMoveCost)
                        )

                for cf in layer.Cover do
                    if mapOk && not (GridBounds.contains cf.Cell bounds) then
                        report (TerrainFeatureOutOfMap(cf.Cell, bounds))

                    if Option.isNone (parseDirection cf.Direction) then
                        report (UnknownCoverClass(cf.Cell, cf.Direction))

                    if cf.Level < 0 then
                        report (NegativeCoverLevel(cf.Cell, cf.Level))

                let cells =
                    layer.Cells
                    |> Array.choose (fun tc ->
                        parseMovementClass tc.Class
                        |> Option.map (fun mc ->
                            { Cell = tc.Cell
                              Movement = mc
                              Elevation = tc.Elevation
                              MoveCost = tc.MoveCost
                              Opaque = tc.Opaque }
                            : AuthoredCell))

                let cover =
                    layer.Cover
                    |> Array.choose (fun cf ->
                        parseDirection cf.Direction
                        |> Option.map (fun d ->
                            { Cell = cf.Cell; Direction = d; Level = cf.Level }: AuthoredCover))

                cells, cover

        // A deployment on an authored impassable cell (docs/06 section 7).
        let impassableCells =
            authoredCells
            |> Array.choose (fun ac -> if ac.Movement = Impassable then Some ac.Cell else None)
            |> Set.ofArray

        if not (Set.isEmpty impassableCells) then
            for d, _ in deployments do
                if impassableCells.Contains d.Cell then
                    report (DeploymentOnImpassableCell(d.AgentId, d.Cell))

        // --- result -------------------------------------------------
        if errors.Count > 0 then
            Error(List.ofSeq errors)
        else
            let toDeployments (raws: RawDeployment[]) (side: Side) : Deployment[] =
                raws
                |> Array.map (fun d ->
                    { Agent = AgentId.ofInt d.AgentId
                      Side = side
                      Cell = d.Cell
                      CommunicationAvailable = d.CommunicationAvailable
                      Discipline = d.Discipline
                      // `errors.Count = 0` here guarantees `d.UnitType` was
                      // validated against `unitTypesById` above.
                      MoveSpeed = Map.find d.UnitType unitTypesById })

            Ok
                { Id = ScenarioId.ofString raw.Id
                  Map = bounds
                  // Both arrays empty when no layer was authored, so this is
                  // `Terrain.empty bounds`. `Terrain.build` does no
                  // validation: every cell reaching it is known in-bounds and
                  // non-negative because any fault above blocks this branch.
                  Terrain = Terrain.build bounds authoredCells authoredCover
                  FriendlyDeployments = toDeployments raw.FriendlyDeployments Friendly
                  EnemyDeployments = toDeployments raw.EnemyDeployments Hostile
                  ObjectiveAreas =
                    raw.ObjectiveAreas
                    |> Array.map (fun a -> ({ Id = AreaId.ofString a.AreaId; Cell = a.Cell }: Area))
                  ExtractionAreas =
                    raw.ExtractionAreas
                    |> Array.map (fun a -> ({ Id = AreaId.ofString a.AreaId; Cell = a.Cell }: Area))
                  ResupplyAreas =
                    raw.ResupplyAreas
                    |> Array.map (fun a -> ({ Id = AreaId.ofString a.AreaId; Cell = a.Cell }: Area))
                  StaticTargets =
                    raw.StaticTargets
                    |> Array.map (fun t -> ({ Id = TargetId.ofString t.TargetId; Cell = t.Cell }: StaticTarget))
                  Objectives = built.ToArray()
                  Rules = { FailOnFriendlyForceEliminated = raw.FailOnFriendlyForceEliminated }
                  Headquarters = raw.Headquarters
                  Jammers =
                    raw.Jammers
                    |> Array.map (fun j ->
                        ({ Position = j.Position
                           Radius = j.Radius
                           ActiveFromTick = j.ActiveFromTick
                           ActiveUntilTick = j.ActiveUntilTick }: Jammer)) }
