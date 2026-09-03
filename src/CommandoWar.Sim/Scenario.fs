namespace CommandoWar.Sim

/// Framework-neutral authored-scenario model, its content-format version, and
/// one-pass validation (ADR-0002 "Content DTOs -> validation -> Sim setup";
/// docs/03_ARCHITECTURE.md sections 16-17; docs/06_CONTENT_AND_PRESENTATION.md
/// sections 3, 7; docs/04_SIMULATION_SPEC.md section 21).
///
/// This module carries authored *positions and references* only. Per-cell
/// terrain, movement cost, opacity, and directional cover (backlog B-008),
/// line of sight (B-009), and pathfinding (B-010) are deliberately absent: the
/// scenario names map dimensions and deployment / objective / area / target
/// positions, nothing more.
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
[<RequireQualifiedAccess>]
module ScenarioContent =

    [<Literal>]
    let Version = 1

// --- validated model ---------------------------------------------------

/// One deployed agent in a validated scenario. Side is fixed by which
/// deployment list the agent appears in.
type Deployment =
    { Agent: AgentId
      Side: Side
      Cell: Cell }

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
      /// Map dimensions only. Elevation is flat; per-cell terrain is B-008.
      Map: GridBounds
      FriendlyDeployments: Deployment[]
      EnemyDeployments: Deployment[]
      ObjectiveAreas: Area[]
      ExtractionAreas: Area[]
      StaticTargets: StaticTarget[]
      Objectives: Objective[]
      Rules: ScenarioRules }

// --- raw (unvalidated) input -----------------------------------------

/// Unvalidated authored deployment: an agent id and a cell.
type RawDeployment = { AgentId: int; Cell: Cell }

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

/// The whole unvalidated authored scenario, as a content reader (a Godot
/// `.tscn` reader, a Tiled importer, or a test) produces it. Every field is a
/// primitive or an array of primitives so the reader depends on nothing in
/// the validated model.
type RawScenario =
    { ContentVersion: int
      Id: string
      Width: int
      Height: int
      FriendlyDeployments: RawDeployment[]
      EnemyDeployments: RawDeployment[]
      ObjectiveAreas: RawArea[]
      ExtractionAreas: RawArea[]
      StaticTargets: RawTarget[]
      Objectives: RawObjective[]
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
        let areaMarkers = Array.append raw.ObjectiveAreas raw.ExtractionAreas

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

        // --- result -------------------------------------------------
        if errors.Count > 0 then
            Error(List.ofSeq errors)
        else
            let toDeployments (raws: RawDeployment[]) (side: Side) : Deployment[] =
                raws
                |> Array.map (fun d ->
                    { Agent = AgentId.ofInt d.AgentId
                      Side = side
                      Cell = d.Cell })

            Ok
                { Id = ScenarioId.ofString raw.Id
                  Map = bounds
                  FriendlyDeployments = toDeployments raw.FriendlyDeployments Friendly
                  EnemyDeployments = toDeployments raw.EnemyDeployments Hostile
                  ObjectiveAreas =
                    raw.ObjectiveAreas
                    |> Array.map (fun a -> ({ Id = AreaId.ofString a.AreaId; Cell = a.Cell }: Area))
                  ExtractionAreas =
                    raw.ExtractionAreas
                    |> Array.map (fun a -> ({ Id = AreaId.ofString a.AreaId; Cell = a.Cell }: Area))
                  StaticTargets =
                    raw.StaticTargets
                    |> Array.map (fun t -> ({ Id = TargetId.ofString t.TargetId; Cell = t.Cell }: StaticTarget))
                  Objectives = built.ToArray()
                  Rules = { FailOnFriendlyForceEliminated = raw.FailOnFriendlyForceEliminated } }
