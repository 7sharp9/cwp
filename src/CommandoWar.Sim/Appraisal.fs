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
///     intake rejects a `Hostile` recipient and (a `MoveTo` order only) an
///     out-of-bounds target, and the Communication phase (TASK-027) only
///     writes `AgentState.Order` for a recipient it could reach. So `appraise`
///     is never called for an order that failed stage 1, and there is no
///     stage-1 `DecisionReason`.
///   * **Stage 2 (physical feasibility)** — `Pathfinding.findWithin` for a
///     `MoveTo` order (no route -> `Unable(NoKnownRoute)`); for a `Suppress`
///     order (TASK-037, backlog B-030 thin slice) whether the named contact
///     is known at all (`Unable(TargetNotKnown)` otherwise) — its only stage,
///     since it has no route and never reaches stages 3/4. Alive / capability
///     / ammunition all trivially pass (no models yet).
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
///
/// A `Suppress` order (TASK-037, a thin B-030 slice) appraises on stage 2
/// alone (is the named contact known at all — `Unable(TargetNotKnown)`
/// otherwise); it has no route, so stages 3/4 never run for it. Its actual
/// effect is on a *different* agent's stage-3 `MoveTo` exposure: while the
/// named threat's own `AgentState.SuppressionBand` is latched,
/// `cellPressure` zeroes that threat's contribution for every route
/// (`suppressedThreats` below). The threat-SuppressionBand-flip reappraisal
/// trigger that actually re-judges that other agent's `Refused` order lives
/// in `Simulation.appraisal`, the identical knowledge-change/suppression-band
/// precedent.

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

    /// Divides an `Alive` agent's lost health (`Agent.MaxHealth - health`)
    /// into a continuous resolve penalty (TASK-045, backlog B-031) — the
    /// `StressDivisor` precedent exactly: up to `Agent.MaxHealth /
    /// WoundDivisor = 40` at near-death, comparable in scale to
    /// `RiskAggressive` / `UrgencyImmediate` / `StressDivisor`'s own ceiling,
    /// never dominant on its own. Reads `Agent.MaxHealth` (`Domain.fs`)
    /// directly rather than `CasualtyConfig.MaxHealth` (`Casualty.fs`) — the
    /// same module-ordering reason `Agent.MaxHealth` exists at all (this
    /// file compiles before `Casualty.fs`).
    [<Literal>]
    let WoundDivisor = 25

    /// Flat stage-4 resolve-threshold subtraction for an `Assault` order
    /// (TASK-047, backlog B-030 proper; `docs/05` section 4 "more demanding
    /// than Move, receives stricter appraisal"). Comparable in scale to
    /// `SuppressionBandPenalty`.
    [<Literal>]
    let AssaultResolvePenalty = 30

    /// Chebyshev cells from an `Assault` order's target within which the
    /// executor stops merely closing distance and starts checking for a
    /// blocking threat (`Commitment.AssaultStage`).
    [<Literal>]
    let AssaultStartRange = 3

    /// Chebyshev cells from an `Assault` order's target within which a
    /// known, currently-alive threat contact keeps the order from being
    /// treated as fulfilled once the agent arrives (`ClearingThreat`).
    [<Literal>]
    let AssaultClearRadius = 2

    /// Flat stage-4 resolve-threshold addition for a `Withdraw` order
    /// (TASK-047, backlog B-030 proper; `docs/05` section 4 "may receive
    /// priority under high suppression," read as appraisal priority — an
    /// agent breaking contact should not be blocked by the very exposure it
    /// is retreating through). Comparable in scale to `RiskAggressive`/
    /// `UrgencyImmediate`.
    [<Literal>]
    let WithdrawResolveBonus = 30

    /// Chebyshev cells around a `Hold` order's authored `area` searched for
    /// a lower-pressure nearby cell (`Appraisal.bestCoverNear`; `docs/05`
    /// section 4 "the executor may choose nearby cover").
    [<Literal>]
    let HoldCoverSearchRadius = 2

    /// Chebyshev cells around a formationed agent's exact slot offset
    /// (TASK-059, backlog B-011d) searched for the nearest passable,
    /// unoccupied cell when the exact offset is blocked or occupied
    /// (`Appraisal.resolveFormationTarget`). The `HoldCoverSearchRadius`
    /// precedent.
    [<Literal>]
    let FormationSlotSearchRadius = 2

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

    /// The pressure one known threat puts on one route cell: `0` while that
    /// threat's own `AgentState.SuppressionBand` is latched (TASK-037,
    /// backlog B-030 thin slice, Decision F — a `Suppress` order's whole
    /// effect on route exposure, reusing the already-canonical, symmetric
    /// hysteresis latch TASK-033/034 built rather than tracking "who is
    /// suppressing whom" separately); otherwise `0` unless the cell is within
    /// `ThreatEngagementRange` Chebyshev cells of the threat's last-known cell
    /// and in `Sight.visible` line of sight from it; otherwise `max 0
    /// (ExposedCellWeight - cover * CoverMitigationPerLevel)`.
    let private cellPressure (terrain: Terrain) (suppressedThreats: AgentId[]) (threat: Contact) (cell: Cell) : int =
        if suppressedThreats |> Array.contains threat.Contact then
            0
        elif
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
    /// `Pathfinding` path; `threats` is `WorldState.TacticalKnowledge`;
    /// `suppressedThreats` (TASK-037) is the ids of every agent whose
    /// `AgentState.SuppressionBand` is latched this tick, regardless of what
    /// suppressed it (an ordered `Suppress` or incidental automatic
    /// engagement, TASK-031, both raise the identical `AgentState.Suppression`
    /// this reads through the latch).
    let routeExposure
        (terrain: Terrain)
        (threats: Contact[])
        (suppressedThreats: AgentId[])
        (routeCells: Cell[])
        : int * AgentId option =
        let perThreat =
            threats
            |> Array.map (fun t ->
                t.Contact, routeCells |> Array.sumBy (fun c -> cellPressure terrain suppressedThreats t c))

        let total = perThreat |> Array.sumBy snd

        let top =
            perThreat
            |> Array.filter (fun (_, p) -> p > 0)
            |> Array.sortBy (fun (id, p) -> -p, id)
            |> Array.tryHead
            |> Option.map fst

        total, top

    /// The route cells that carry non-zero pressure from at least one known,
    /// currently-unsuppressed threat — the "exposed stretch" a diagnostic
    /// overlay highlights.
    let private exposedCells
        (terrain: Terrain)
        (threats: Contact[])
        (suppressedThreats: AgentId[])
        (routeCells: Cell[])
        : Cell[] =
        routeCells
        |> Array.filter (fun c -> threats |> Array.exists (fun t -> cellPressure terrain suppressedThreats t c > 0))

    /// The best nearby cell for a `Hold` order to occupy instead of the
    /// literal authored `area` (TASK-047, backlog B-030 proper; `docs/05`
    /// section 4 "the executor may choose nearby cover"): among every
    /// passable, in-bounds cell within `AppraisalConfig.HoldCoverSearchRadius`
    /// Chebyshev cells of `area` (including `area` itself), the one with the
    /// lowest total `cellPressure` summed over every known threat
    /// (reusing the identical threat-pressure function `routeExposure`
    /// already sums over a route), ties broken by nearest to `area` then
    /// ascending `(Y, X)`. A scenario with no known threats near `area`
    /// picks `area` itself (every candidate scores 0, and `area` is the
    /// nearest to itself). Pure, total: if `area` and every cell in radius
    /// are impassable this still returns `area` unchanged (Stage 2's
    /// `Pathfinding` call then fails exactly as it would have without this
    /// step).
    let bestCoverNear (terrain: Terrain) (threats: Contact[]) (suppressedThreats: AgentId[]) (area: Cell) : Cell =
        let r = AppraisalConfig.HoldCoverSearchRadius

        [ for dy in -r..r do
              for dx in -r..r do
                  let c = { X = area.X + dx; Y = area.Y + dy }

                  if GridBounds.contains c terrain.Bounds && Terrain.passable terrain c then
                      let pressure = threats |> Array.sumBy (fun t -> cellPressure terrain suppressedThreats t c)
                      pressure, Perception.chebyshev area c, c.Y, c.X, c ]
        |> List.sortBy (fun (pressure, dist, y, x, _) -> pressure, dist, y, x)
        |> List.tryHead
        |> Option.map (fun (_, _, _, _, c) -> c)
        |> Option.defaultValue area

    /// A `MoveTo` order's real pathfinding target for a formationed agent
    /// (TASK-059, backlog B-011d): `offset = None` reproduces today's
    /// behaviour exactly, returning `anchor` (the order's own literal
    /// target) unchanged -- every scenario authored before this task. `Some
    /// o` treats `anchor` as the formation anchor and aims for `anchor +
    /// o`, redirected to the nearest passable, in-bounds cell not in
    /// `occupied` within `AppraisalConfig.FormationSlotSearchRadius`
    /// Chebyshev cells if the exact offset cell fails any of those checks
    /// -- the `bestCoverNear` precedent, scored by occupancy instead of
    /// threat pressure (ties broken by nearest to the ideal offset cell,
    /// then ascending `(Y, X)`). Falls back to the literal `anchor` cell
    /// itself if nothing in radius qualifies, the identical `bestCoverNear`
    /// "never a hard failure from this alone" guarantee: crowding or
    /// terrain at a slot must never turn a `MoveTo` into a `NoKnownRoute`
    /// refusal by itself (stage 2 still fails normally if `anchor` itself
    /// is unreachable). `occupied` is every OTHER agent's current
    /// `Position` — the caller's own responsibility to exclude itself.
    let resolveFormationTarget (terrain: Terrain) (occupied: Cell[]) (offset: Cell option) (anchor: Cell) : Cell =
        match offset with
        | None -> anchor
        | Some o ->
            let ideal = { X = anchor.X + o.X; Y = anchor.Y + o.Y }
            let r = AppraisalConfig.FormationSlotSearchRadius

            let free (c: Cell) =
                GridBounds.contains c terrain.Bounds
                && Terrain.passable terrain c
                && not (Array.contains c occupied)

            if free ideal then
                ideal
            else
                [ for dy in -r..r do
                      for dx in -r..r do
                          let c = { X = ideal.X + dx; Y = ideal.Y + dy }

                          if free c then
                              yield Perception.chebyshev ideal c, c.Y, c.X, c ]
                |> List.sortBy (fun (dist, y, x, _) -> dist, y, x)
                |> List.tryHead
                |> Option.map (fun (_, _, _, c) -> c)
                |> Option.defaultValue anchor

    /// The stage-4 resolve threshold for an agent and an order (`docs/05`
    /// section 5 stage 4). Integer, bounded, order-independent of the world.
    /// `stress` and `suppressed` (TASK-033, backlog B-021) are the agent's
    /// `AgentState.Stress` and `AgentState.SuppressionBand` at the top of the
    /// tick — a continuous drag and a discrete banded penalty respectively.
    /// `vitals` (TASK-045, backlog B-031) adds a third continuous drag while
    /// `Alive` but wounded — `(Agent.MaxHealth - health) /
    /// AppraisalConfig.WoundDivisor`, the `StressDivisor` precedent exactly;
    /// `0` while at full health, and this function is never called with a
    /// non-`Alive` `vitals` in practice (`appraise` below short-circuits to
    /// `Unable(CriticallyWounded)` at stage 2 before stage 4 is reached).
    /// Floored at 0 so a completely unexposed route (`exposure = 0`) is
    /// always `Accepted` regardless of how stressed, suppressed, or wounded
    /// the agent is: every term only ever makes an already-exposed route
    /// *more* likely to be refused, never refuses a safe one outright.
    let resolveThreshold
        (discipline: int)
        (stress: int)
        (suppressed: bool)
        (vitals: VitalStatus)
        (order: ReceivedOrder)
        : int =
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

        let woundMod =
            match vitals with
            | Alive health -> (Agent.MaxHealth - health) / AppraisalConfig.WoundDivisor
            | Incapacitated _
            | Dead -> 0

        AppraisalConfig.BaseResolve
        + AppraisalConfig.DisciplineResolveWeight * discipline
        + riskMod
        + urgencyMod
        - stress / AppraisalConfig.StressDivisor
        - suppressionMod
        - woundMod
        |> max 0

    /// The whole staged pipeline for one order. Returns the outcome and the
    /// exposed route cells (for the diagnostic overlay; `[||]` when there is no
    /// known route or no exposure). `budget` is the `Pathfinding` expansion
    /// ceiling (`Bounds.Width * Bounds.Height`, the Navigation-phase value).
    /// `suppressedThreats` (TASK-037) is threaded straight to `routeExposure`
    /// / `exposedCells` for a `MoveTo` order's stage 3; a `Suppress` order
    /// (Decision C) has no route to expose — stages 3/4 are trivial and it
    /// appraises purely on stage 2 (is the named contact known at all).
    /// `vitals` (TASK-045, backlog B-031) is checked first, ahead of either
    /// intent's own stage-2 logic: a non-`Alive` agent is `Unable
    /// (CriticallyWounded)` regardless of what the order asks — `docs/05`
    /// section 4 stage 2's "is the agent alive, conscious, and mobile?",
    /// section 16's own "a critically wounded agent reports unable rather
    /// than refused" example — so stages 3/4 (and `resolveThreshold`'s own
    /// wound term) are only ever reached by an `Alive` agent.
    /// `occupied`/`formationOffset` (TASK-059, backlog B-011d) are threaded
    /// straight to `resolveFormationTarget` for a `MoveTo` order only --
    /// `formationOffset = None` reproduces every pre-TASK-059 call exactly.
    let appraise
        (terrain: Terrain)
        (threats: Contact[])
        (suppressedThreats: AgentId[])
        (discipline: int)
        (stress: int)
        (suppressed: bool)
        (vitals: VitalStatus)
        (ammo: AmmoState)
        (occupied: Cell[])
        (formationOffset: Cell option)
        (order: ReceivedOrder)
        (fromCell: Cell)
        (budget: int)
        : OrderDisposition * Cell[] =
        match vitals with
        | Incapacitated _
        | Dead -> Unable(CriticallyWounded, [||]), [||]
        | Alive _ ->
            // TASK-047 (backlog B-030 proper): stage-2 ammo check, `Suppress`/
            // `Assault` only (Decision L) — the two intents that explicitly
            // plan to initiate fire. `MoveTo`/`Hold`/`Withdraw` never check
            // this: an unarmed agent can still walk, hold ground, or retreat.
            let noAmmo = ammo = Ready(0, 0)

            // The shared MoveTo-shaped pipeline (stages 2-4): `target` is
            // where Pathfinding routes to, `thresholdAdjust` is a signed
            // stage-4 flat adjustment applied on top of `resolveThreshold`
            // and re-floored at 0 (the `resolveThreshold` "never refuses a
            // safe route" invariant, preserved for `Assault`'s stricter
            // penalty exactly as for the unmodified case). `MoveTo` and
            // `Withdraw` pass `target` through unchanged; `Hold` redirects
            // through `bestCoverNear`; `Assault`'s ammo check happens before
            // this is ever called.
            let moveLike (target: Cell) (thresholdAdjust: int) =
                if fromCell = target then
                    Accepted, [||]
                else
                    match Pathfinding.findWithin terrain fromCell target budget with
                    | Found(cells, _) when cells.Length >= 2 ->
                        let exposure, topThreat = routeExposure terrain threats suppressedThreats cells
                        let exposed = exposedCells terrain threats suppressedThreats cells

                        let threshold =
                            max 0 (resolveThreshold discipline stress suppressed vitals order + thresholdAdjust)

                        if exposure <= threshold then
                            Accepted, exposed
                        else
                            Refused(RouteTooExposed topThreat, [||]), exposed
                    | Found _
                    | NoPath
                    | BudgetExhausted _
                    | InvalidEndpoint _ -> Unable(NoKnownRoute, [||]), [||]

            match order.Intent with
            | MoveTo target -> moveLike (resolveFormationTarget terrain occupied formationOffset target) 0
            | Hold area -> moveLike (bestCoverNear terrain threats suppressedThreats area) 0
            | Withdraw target -> moveLike target AppraisalConfig.WithdrawResolveBonus
            | Assault target ->
                if noAmmo then
                    Unable(InsufficientAmmunition, [||]), [||]
                else
                    moveLike target (-AppraisalConfig.AssaultResolvePenalty)
            | Suppress target ->
                if noAmmo then
                    Unable(InsufficientAmmunition, [||]), [||]
                elif threats |> Array.exists (fun t -> t.Contact = target) then
                    Accepted, [||]
                else
                    Unable(TargetNotKnown, [||]), [||]
