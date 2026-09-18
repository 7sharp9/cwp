namespace CommandoWar.Sim

/// Ammunition, reload, and resupply (TASK-047, backlog B-030 proper;
/// `docs/04_SIMULATION_SPEC.md` sections 12.8/12.9; `docs/05` section 8 "is
/// required ammunition ... available?").
///
/// This is a leaf, exactly as `Suppression`/`Stress`/`Casualty` are: pure,
/// total, integer-only, `Domain` only, no event emission (the Combat and
/// State-consequences phases own that), no mutation of its inputs.
///
/// ## What is deliberately absent
///
///   * a cooldown-only mode (no magazine/reserve split) — considered and
///     rejected, `TASK-047-ASSAULT-WITHDRAW-HOLD-AND-AMMUNITION.md`
///     Decision J;
///   * a multi-tick resupply "rearming" duration — resupply is instant on
///     arrival (Decision M);
///   * ammo scoped to `Suppress` fire alone — every qualifying shot draws
///     from the same `AgentState.Ammo` (Decision K).
[<RequireQualifiedAccess>]
module AmmoConfig =

    /// Rounds a full magazine holds. `Agent.MagazineSize` (`Domain.fs`)
    /// carries the same value (kept there to avoid a module-ordering
    /// dependency — the `Agent.MaxHealth`/`CasualtyConfig.MaxHealth`
    /// precedent).
    [<Literal>]
    let MagazineSize = 30

    /// Rounds a freshly created or fully resupplied agent's reserve stock
    /// holds. `Agent.ReserveStart` carries the same value.
    [<Literal>]
    let ReserveStart = 90

    /// Ticks a reload takes once a magazine empties with reserve remaining.
    [<Literal>]
    let ReloadTicks = 30

[<RequireQualifiedAccess>]
module Ammo =

    /// Whether this agent may fire this tick: a `Ready` state with at least
    /// one round in the magazine. `Reloading` (magazine definitionally
    /// empty) and `Ready(0, _)` both cannot fire.
    let canFire (ammo: AmmoState) : bool =
        match ammo with
        | Ready(magazine, _) -> magazine > 0
        | Reloading _ -> false

    /// Consumes one round from a `Ready` magazine. Only meaningful when
    /// `canFire` — total and a no-op otherwise, the `Casualty.wound`
    /// precedent for a pure leaf staying defined on every input rather than
    /// crashing.
    let fire (ammo: AmmoState) : AmmoState =
        match ammo with
        | Ready(magazine, reserve) when magazine > 0 -> Ready(magazine - 1, reserve)
        | other -> other

    /// One tick's reload bookkeeping (State-consequences phase): an empty
    /// magazine with reserve remaining starts a reload
    /// (`Ready(0, reserve>0) -> Reloading(reserve, AmmoConfig.ReloadTicks)`,
    /// `justStarted = true`); a `Reloading` state counts down, refilling the
    /// magazine from reserve once it reaches zero (`justCompleted = true`);
    /// anything else (a `Ready` state with rounds still chambered, or truly
    /// empty with no reserve) is unchanged. Never called on an agent this
    /// tick's resupply check already refilled (`Simulation.stateConsequences`
    /// checks resupply first, per Decision M).
    let tick (ammo: AmmoState) : AmmoState * bool * bool =
        match ammo with
        | Ready(0, reserve) when reserve > 0 -> Reloading(reserve, AmmoConfig.ReloadTicks), true, false
        | Reloading(reserve, ticksRemaining) ->
            if ticksRemaining <= 1 then
                let refill = min AmmoConfig.MagazineSize reserve
                Ready(refill, reserve - refill), false, true
            else
                Reloading(reserve, ticksRemaining - 1), false, false
        | other -> other, false, false

    /// Refills to a full magazine and reserve, unconditionally — an agent
    /// standing on an authored `WorldState.ResupplyAreas` cell
    /// (`Simulation.stateConsequences`). Short-circuits any in-progress
    /// reload (Decision M: an agent at the cache does not need to wait one
    /// out).
    let resupply (_: AmmoState) : AmmoState =
        Ready(AmmoConfig.MagazineSize, AmmoConfig.ReserveStart)

    /// Whether `ammo` already equals a full resupply — used to avoid
    /// emitting `AgentResupplied` every tick an already-full agent happens
    /// to stand on a resupply cell.
    let isFull (ammo: AmmoState) : bool =
        ammo = Ready(AmmoConfig.MagazineSize, AmmoConfig.ReserveStart)
