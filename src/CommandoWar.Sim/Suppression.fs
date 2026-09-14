namespace CommandoWar.Sim

/// Suppression creation and decay (TASK-032, backlog B-020;
/// `docs/04_SIMULATION_SPEC.md` sections 12.8/12.9; `docs/05` section 8).
///
/// This is a leaf, exactly as `Combat` is: pure, total, integer-only,
/// `Terrain` / `Appraisal` / `Domain` only, no event emission (the Combat and
/// State-consequences phases own that), no mutation of its inputs.
/// `Combat.fs` itself is unchanged — this leaf sits alongside it, reusing
/// `Appraisal.attackDirection` for the identical cover-facing geometry
/// `Combat.hitChance` uses.
///
/// ## What is deliberately absent (later backlog items)
///
///   * any effect on order appraisal, the resolve threshold, or a
///     reappraisal trigger — B-021;
///   * any movement-speed, action-effectiveness, or "take cover" behaviour
///     change from being suppressed — no system realises this yet;
///   * a `Suppress` player order or its executor — B-030.
[<RequireQualifiedAccess>]
module SuppressionConfig =

    /// Suppression scale, matching `Contact.Confidence` / `CombatConfig`'s
    /// `0..1000` — headroom for a future suppression-band reappraisal
    /// trigger (B-021).
    [<Literal>]
    let MaxSuppression = 1000

    /// Suppression gained by the target of a hit, before cover mitigation.
    [<Literal>]
    let GainOnHit = 400

    /// Suppression gained by the target of a miss, before cover mitigation
    /// (`docs/04` section 12.8: "create suppression independent of a hit").
    [<Literal>]
    let GainOnMiss = 150

    /// Suppression-gain reduction per authored `Terrain.cover` level on the
    /// edge the shot arrives from (the `CombatConfig.CoverMitigationPerLevel`
    /// precedent, same units, independently tunable).
    [<Literal>]
    let CoverMitigationPerLevel = 100

    /// Flat suppression decay applied every tick in State consequences,
    /// floored at 0 ("decays when safe", `docs/05` section 8).
    [<Literal>]
    let DecayPerTick = 50

[<RequireQualifiedAccess>]
module Suppression =

    /// Suppression gained by one qualifying shot from `shooter` at `target`,
    /// both current cells, resolving `hit`. Pure, total, floored at 0.
    /// Reuses `Appraisal.attackDirection` for the cover-facing edge — the
    /// identical geometry `Combat.hitChance` uses.
    let gain (terrain: Terrain) (shooter: Cell) (target: Cell) (hit: bool) : int =
        let cover = Terrain.cover terrain target (Appraisal.attackDirection shooter target)
        let baseGain = if hit then SuppressionConfig.GainOnHit else SuppressionConfig.GainOnMiss

        baseGain - cover * SuppressionConfig.CoverMitigationPerLevel
        |> max 0

    /// Adds `delta` to `current`, clamped at `MaxSuppression`. Separate from
    /// `gain` so two same-tick shots against one target (from two different
    /// shooters) compose by calling this twice.
    let raise (current: int) (delta: int) : int =
        current + delta |> min SuppressionConfig.MaxSuppression

    /// One tick's flat decay, floored at 0.
    let decay (current: int) : int =
        current - SuppressionConfig.DecayPerTick |> max 0
