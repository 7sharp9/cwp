namespace CommandoWar.Sim

/// Stress accumulation and decay (TASK-033, backlog B-021;
/// `docs/04_SIMULATION_SPEC.md` section 12.9; `docs/05` section 8).
///
/// This is a leaf, exactly as `Suppression` is: pure, total, integer-only,
/// `Domain` only, no event emission (the State-consequences phase owns
/// that), no mutation of its inputs.
///
/// `docs/05` section 8 lists five stress sources: "nearby casualties,
/// wounds, isolation, explosions, and threat". Only "threat" has a system
/// behind it today (`AgentState.VisibleContacts`, TASK-026) — casualties and
/// wounds need B-031, explosions and isolation have no system at all. This
/// leaf therefore realises stress from observed-enemy contact only; the
/// other four sources land with the systems that can produce them.
///
/// ## What is deliberately absent (later backlog items)
///
///   * casualty-, wound-, explosion-, and isolation-driven stress — B-031 and
///     unassigned future work;
///   * dynamic trust — the vertical slice leaves it minimal (`docs/05`
///     section 8), so this task builds no trust field at all;
///   * any movement-speed or action-effectiveness change from stress — no
///     system realises this yet, only the stage-4 resolve threshold reads it.
[<RequireQualifiedAccess>]
module StressConfig =

    /// Stress scale, the `SuppressionConfig.MaxSuppression` precedent.
    [<Literal>]
    let MaxStress = 1000

    /// Stress gained per tick an agent has at least one opposing-side agent
    /// in `AgentState.VisibleContacts` ("threat", `docs/05` section 8).
    [<Literal>]
    let GainPerTick = 80

    /// Flat stress decay applied every tick, floored at 0 ("decays when
    /// safe", `docs/05` section 8) — smaller than `GainPerTick` so continuous
    /// contact still accumulates net stress, roughly the
    /// `SuppressionConfig.DecayPerTick` precedent's pacing.
    [<Literal>]
    let DecayPerTick = 30

[<RequireQualifiedAccess>]
module Stress =

    /// Stress gained this tick: `GainPerTick` when `inContact` (the agent's
    /// `VisibleContacts` is non-empty this tick), else 0. Pure, total.
    let gain (inContact: bool) : int =
        if inContact then StressConfig.GainPerTick else 0

    /// Adds `delta` to `current`, clamped at `MaxStress`. Separate from
    /// `gain`, the `Suppression.raise` precedent.
    let raise (current: int) (delta: int) : int =
        current + delta |> min StressConfig.MaxStress

    /// One tick's flat decay, floored at 0.
    let decay (current: int) : int =
        current - StressConfig.DecayPerTick |> max 0
