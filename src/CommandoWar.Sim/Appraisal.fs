namespace CommandoWar.Sim

/// Deterministic staged order appraisal (TASK-028, backlog B-017;
/// `docs/04_SIMULATION_SPEC.md` section 12.5; `docs/05_COMMAND_AND_AGENT_AI.md`
/// sections 5-7).
///
/// This is a leaf, exactly as `Sight`, `Pathfinding`, and `Perception` are:
/// pure, total, integer-only, `Terrain` / `Sight` / `Pathfinding` / `Perception`
/// / `Domain` only, no event emission (the Appraisal phase owns that), no
/// mutation of its inputs, **no PRNG draw** (the deterministic stream's first
/// gameplay consumer stays combat spread, B-019).
///
/// ## The staged pipeline (`docs/05` section 5)
///
///   * **Stage 1 (comprehension / authority)** is guaranteed upstream: command
///     intake rejects a `Hostile` recipient and an out-of-bounds target, and
///     the Communication phase (TASK-027) only writes `AgentState.Order` for a
///     recipient it could reach. So `appraise` is never called for an order
///     that failed stage 1, and there is no stage-1 `DecisionReason`.
///   * **Stage 2 (physical feasibility)** — `Pathfinding.findWithin`. No route
///     -> `Unable(NoKnownRoute)`. Alive / capability / ammunition all trivially
///     pass (no models yet).
///   * **Stage 3 (tactical viability)** — route exposure to the *known*
///     threats in `WorldState.TacticalKnowledge` (never authoritative hostile
///     state, risk R-023). `routeExposure` below.
///   * **Stage 4 (resolve)** — compare the exposure to a bounded threshold
///     from `Discipline` + the order's `RiskTolerance` / `Urgency`
///     (`resolveThreshold`). `<=` -> `Accepted`; over -> `Refused
///     (RouteTooExposed)`.
///   * **Stage 5 (safer adaptation)** is deferred (B-018): no `Adapted`
///     outcome, no route recomputation.
///
/// ## What is deliberately absent (later backlog items)
///
///   * the exposure-band, wounded, support, and leadership reappraisal
///     triggers, and dynamic trust — B-021 realises stress and the
///     suppression-band / knowledge-change triggers only (TASK-033); the
///     rest need systems that do not exist (route-exposure tracking per idle
///     agent, wound/casualty state, a support-commitment concept, a
///     leadership entity — B-030/B-031);
///   * commitments and the finite action executor — B-018 (Appraisal writes
///     `Destination`, it does not build a commitment store);
///   * line of fire / combat / a real threat model — B-019 (which reuses
///     `attackDirection` below, made public by TASK-031, for its own
///     cover-facing geometry).
///
/// Stress and suppression-band hysteresis realised by TASK-033 (backlog
/// B-021): `resolveThreshold` and `appraise` below take the agent's
/// `AgentState.Stress` and `AgentState.SuppressionBand` (both read at the top
/// of the tick, before this tick's State-consequences / Combat can change
/// them) and fold them into the stage-4 threshold. The knowledge-change and
/// suppression-band reappraisal triggers themselves — resetting
/// `AgentState.Disposition` to `None` so this module's fast path is skipped —
/// live in `Simulation.appraisal`, not here: this module stays a pure leaf
/// with no event emission and no `StepState` access.

/// Every appraisal threshold, in one place (`docs/05` section 15 "record every
/// threshold in one configuration structure"). Module literals rather than a
/// `WorldState` field: these values affect authoritative outcomes (so not
/// `SimConfig`, which is non-authoritative host config), but no scenario needs
/// to tune them yet — the `PerceptionConfig` precedent.
[<RequireQualifiedAccess>]
module AppraisalConfig =

    /// The discipline of an agent from a deployment that authored none, and
    /// every non-scenario construction path. `Agent.DisciplineDefault` carries
    /// the same value (kept there to avoid a module-ordering dependency).
    [<Literal>]
    let DisciplineDefault = 3

    /// Chebyshev cells: a known threat within this distance of a route cell is
    /// treated as able to put fire on it. Below `PerceptionConfig.SightRange`
    /// (10) so a threat can be observed before it is in engagement range.
    [<Literal>]
    let ThreatEngagementRange = 8

    /// Tactical pressure one exposed route cell adds, before cover mitigation.
    [<Literal>]
    let ExposedCellWeight = 10

    /// Pressure removed per authored `Terrain.cover` level on the edge the
    /// threat's fire arrives from. `max 0 (ExposedCellWeight - level *
    /// CoverMitigationPerLevel)` — a cover level of 3 fully negates a cell.
    [<Literal>]
    let CoverMitigationPerLevel = 4

    /// Tactical pressure an agent of `Discipline 0` tolerates before refusing.
    [<Literal>]
    let BaseResolve = 20

    /// Added tolerance per point of `Discipline`. With `BaseResolve = 20` an
    /// agent of `Discipline 3` (the default) tolerates `65`.
    [<Literal>]
    let DisciplineResolveWeight = 15

    /// `RiskTolerance.Cautious` lowers the resolve threshold (refuse sooner).
    [<Literal>]
    let RiskCautious = -15

    /// `RiskTolerance.Aggressive` raises it (press on despite pressure).
    /// `RiskTolerance.Standard` is 0.
    [<Literal>]
    let RiskAggressive = 20

    /// `Urgency.Immediate` raises the threshold (act despite pressure).
    /// `Urgency.Routine` is 0.
    [<Literal>]
    let UrgencyImmediate = 20

    /// Divides `AgentState.Stress` (`0..1000`) into a continuous resolve
    /// penalty (TASK-033, backlog B-021): `stress / StressDivisor`, up to
    /// `MaxStress / StressDivisor = 40` at full stress — comparable in scale
    /// to `RiskAggressive` / `UrgencyImmediate`, never dominant on its own.
    [<Literal>]
    let StressDivisor = 25

    /// `AgentState.Suppression` value at or above which the hysteresis latch
    /// `AgentState.SuppressionBand` turns `true` (`docs/05` sections 14/15).
    /// Half of `SuppressionConfig.MaxSuppression`: "heavily suppressed", not
    /// merely shot at once.
    [<Literal>]
    let SuppressionBandEnter = 500

    /// `AgentState.Suppression` value at or below which the latch turns back
    /// `false`. Below `SuppressionBandEnter` so a value oscillating near one
    /// threshold does not flip the latch every tick — the gap
    /// (`SuppressionBandEnter - SuppressionBandExit = 200`) exceeds one
    /// `SuppressionConfig.DecayPerTick` (50), so decay alone cannot cross it
    /// in a single tick right at the boundary.
    [<Literal>]
    let SuppressionBandExit = 300

    /// Flat resolve-threshold penalty while `AgentState.SuppressionBand` is
    /// `true` — a discrete drop, distinct from `Stress`'s continuous one,
    /// comparable in scale to `RiskCautious`.
    [<Literal>]
    let SuppressionBandPenalty = 30

[<RequireQualifiedAccess>]
module Appraisal =

    /// The cardinal an attack from `threatCell` travels to reach `cell` — the
    /// direction `Terrain.cover` treats fire as arriving from. The dominant
    /// axis of `(threatCell - cell)` wins; the X axis breaks a tie.
    ///
    /// Public since TASK-031 (backlog B-019): `Combat.hitChance` reuses this
    /// identical geometry for the cover-facing edge of a hitscan shot, rather
    /// than duplicating it.
    let attackDirection (threatCell: Cell) (cell: Cell) : Direction =
        let dx = threatCell.X - cell.X
        let dy = threatCell.Y - cell.Y

        if abs dx >= abs dy then
            if dx >= 0 then East else West
        else if dy >= 0 then
            South
        else
            North

    /// The pressure one known threat puts on one route cell: 0 unless the cell
    /// is within `ThreatEngagementRange` Chebyshev cells of the threat's
    /// last-known cell and in `Sight.visible` line of sight from it; otherwise
    /// `max 0 (ExposedCellWeight - cover * CoverMitigationPerLevel)`.
    let private cellPressure (terrain: Terrain) (threat: Contact) (cell: Cell) : int =
        if
            Perception.chebyshev threat.LastKnownCell cell <= AppraisalConfig.ThreatEngagementRange
            && Sight.visible terrain threat.LastKnownCell cell
        then
            let cover = Terrain.cover terrain cell (attackDirection threat.LastKnownCell cell)
            max 0 (AppraisalConfig.ExposedCellWeight - cover * AppraisalConfig.CoverMitigationPerLevel)
        else
            0

    /// Total exposure of a route to the known squad picture, plus the single
    /// highest-contributing threat (ties broken by ascending id), or `None`
    /// when no known threat contributes any pressure. `routeCells` is a
    /// `Pathfinding` path; `threats` is `WorldState.TacticalKnowledge`.
    let routeExposure (terrain: Terrain) (threats: Contact[]) (routeCells: Cell[]) : int * AgentId option =
        let perThreat =
            threats
            |> Array.map (fun t -> t.Contact, routeCells |> Array.sumBy (fun c -> cellPressure terrain t c))

        let total = perThreat |> Array.sumBy snd

        let top =
            perThreat
            |> Array.filter (fun (_, p) -> p > 0)
            |> Array.sortBy (fun (id, p) -> -p, id)
            |> Array.tryHead
            |> Option.map fst

        total, top

    /// The route cells that carry non-zero pressure from at least one known
    /// threat — the "exposed stretch" a diagnostic overlay highlights.
    let private exposedCells (terrain: Terrain) (threats: Contact[]) (routeCells: Cell[]) : Cell[] =
        routeCells
        |> Array.filter (fun c -> threats |> Array.exists (fun t -> cellPressure terrain t c > 0))

    /// The stage-4 resolve threshold for an agent and an order (`docs/05`
    /// section 5 stage 4). Integer, bounded, order-independent of the world.
    /// `stress` and `suppressed` (TASK-033, backlog B-021) are the agent's
    /// `AgentState.Stress` and `AgentState.SuppressionBand` at the top of the
    /// tick — a continuous drag and a discrete banded penalty respectively.
    /// Floored at 0 so a completely unexposed route (`exposure = 0`) is
    /// always `Accepted` regardless of how stressed or suppressed the agent
    /// is: both terms only ever make an already-exposed route *more* likely
    /// to be refused, never refuse a safe one outright.
    let resolveThreshold (discipline: int) (stress: int) (suppressed: bool) (order: ReceivedOrder) : int =
        let riskMod =
            match order.RiskTolerance with
            | Cautious -> AppraisalConfig.RiskCautious
            | Standard -> 0
            | Aggressive -> AppraisalConfig.RiskAggressive

        let urgencyMod =
            match order.Urgency with
            | Routine -> 0
            | Immediate -> AppraisalConfig.UrgencyImmediate

        let suppressionMod =
            if suppressed then
                AppraisalConfig.SuppressionBandPenalty
            else
                0

        AppraisalConfig.BaseResolve
        + AppraisalConfig.DisciplineResolveWeight * discipline
        + riskMod
        + urgencyMod
        - stress / AppraisalConfig.StressDivisor
        - suppressionMod
        |> max 0

    /// The whole staged pipeline for one order. Returns the outcome and the
    /// exposed route cells (for the diagnostic overlay; `[||]` when there is no
    /// known route or no exposure). `budget` is the `Pathfinding` expansion
    /// ceiling (`Bounds.Width * Bounds.Height`, the Navigation-phase value).
    let appraise
        (terrain: Terrain)
        (threats: Contact[])
        (discipline: int)
        (stress: int)
        (suppressed: bool)
        (order: ReceivedOrder)
        (fromCell: Cell)
        (budget: int)
        : OrderDisposition * Cell[] =
        let (MoveTo target) = order.Intent

        if fromCell = target then
            Accepted, [||]
        else
            match Pathfinding.findWithin terrain fromCell target budget with
            | Found(cells, _) when cells.Length >= 2 ->
                let exposure, topThreat = routeExposure terrain threats cells
                let exposed = exposedCells terrain threats cells

                if exposure <= resolveThreshold discipline stress suppressed order then
                    Accepted, exposed
                else
                    Refused(RouteTooExposed topThreat, [||]), exposed
            | Found _
            | NoPath
            | BudgetExhausted _
            | InvalidEndpoint _ -> Unable(NoKnownRoute, [||]), [||]
