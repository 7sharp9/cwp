namespace CommandoWar.Sim

/// Deterministic hitscan combat (TASK-031, backlog B-019;
/// `docs/04_SIMULATION_SPEC.md` section 12.8).
///
/// This is a leaf, exactly as `Sight`, `Pathfinding`, `Perception`, and
/// `Appraisal` are: pure, total, integer-only, `Terrain` / `Sight` /
/// `Perception` / `Appraisal` / `Domain` only, no event emission (the Combat
/// phase owns that), no mutation of its inputs, **no PRNG draw** (the phase
/// function draws; this leaf only computes the chance a draw is compared
/// against).
///
/// ## What is deliberately absent (later backlog items)
///
///   * ammunition, weapon readiness, fire-rate / cooldown, reload, resupply —
///     a future task, not yet scoped;
///   * any wound / death consequence, agent removal, incapacitation — B-031;
///   * suppression, stress, or exposure changes from being shot at — B-020;
///   * enemy doctrine choosing when or whether to engage — B-022 (this leaf
///     is the mechanic doctrine will eventually gate, not the doctrine).
[<RequireQualifiedAccess>]
module CombatConfig =

    /// Chebyshev cells: the farthest a shot can be attempted. Below
    /// `PerceptionConfig.SightRange` (10) so a target can be seen before it
    /// is in weapon range — the `AppraisalConfig.ThreatEngagementRange`
    /// precedent.
    [<Literal>]
    let WeaponRange = 7

    /// Hit chance on the `0..1000` scale (the `Contact.Confidence`
    /// precedent) at zero range with no cover.
    [<Literal>]
    let BaseHitChance = 700

    /// Hit-chance reduction per Chebyshev cell of range.
    [<Literal>]
    let RangePenaltyPerCell = 40

    /// Hit-chance reduction per authored `Terrain.cover` level on the edge
    /// the shot arrives from (the `AppraisalConfig.CoverMitigationPerLevel`
    /// precedent, same units, independently tunable).
    [<Literal>]
    let CoverMitigationPerLevel = 150

    /// Clamp floor: a qualifying shot (already past the range/LOS gate) is
    /// never impossible.
    [<Literal>]
    let MinHitChance = 50

    /// Clamp ceiling: a qualifying shot is never a certainty.
    [<Literal>]
    let MaxHitChance = 950

[<RequireQualifiedAccess>]
module Combat =

    /// Integer hit chance (`0..1000`) for a shot from `shooter` at `target`,
    /// both current cells. Pure, total. Reuses `Appraisal.attackDirection`
    /// (made public by this task) for the cover-facing edge — the identical
    /// geometry Appraisal's stage-3 exposure calculation uses.
    let hitChance (terrain: Terrain) (shooter: Cell) (target: Cell) : int =
        let distance = Perception.chebyshev shooter target
        let cover = Terrain.cover terrain target (Appraisal.attackDirection shooter target)

        CombatConfig.BaseHitChance
        - CombatConfig.RangePenaltyPerCell * distance
        - cover * CombatConfig.CoverMitigationPerLevel
        |> max CombatConfig.MinHitChance
        |> min CombatConfig.MaxHitChance

    /// The nearest `candidate` to `shooter` that is within `WeaponRange`
    /// Chebyshev cells and currently in `Sight.visible` line of fire, ties
    /// broken by ascending `AgentId`; `None` if no candidate qualifies. Line
    /// of fire is re-verified fresh here against each candidate's *current*
    /// cell — `candidates` (in practice, the shooter's `VisibleContacts`
    /// resolved to full `AgentState`s) supplies the set of known threats, not
    /// a cached line-of-fire guarantee, since positions can move between
    /// Perception (phase 3) and Combat (phase 8) within the same tick.
    let chooseTarget (terrain: Terrain) (shooter: AgentState) (candidates: AgentState[]) : AgentState option =
        candidates
        |> Array.filter (fun t ->
            Perception.chebyshev shooter.Position t.Position <= CombatConfig.WeaponRange
            && Sight.visible terrain shooter.Position t.Position)
        |> Array.sortBy (fun t -> Perception.chebyshev shooter.Position t.Position, AgentId.value t.Id)
        |> Array.tryHead
