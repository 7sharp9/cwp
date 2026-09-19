namespace CommandoWar.Sim

/// Dynamic communication: whether an agent can currently receive an order
/// (TASK-058, backlog B-016b; `docs/04_SIMULATION_SPEC.md` section 12.2,
/// `docs/05_COMMAND_AND_AGENT_AI.md` section 16 "Lost communication").
///
/// TASK-027 left comms availability as static authored per-agent data
/// (`AgentState.CommunicationAvailable`). This module layers three dynamic
/// checks on top of it, all gated behind whether the world authors a
/// `WorldState.Headquarters` at all: without one, `Communication.available`
/// reduces to exactly the static check (every one of the 16 pre-existing
/// corpus entries, `Fixture`, `DemoScenario`, `PathDemo`, `LosDemo`, and
/// `TurnDemo` author none, so their behaviour is unaffected by this task).
/// With one authored, an agent additionally needs to be within
/// `CommsConfig.Range` Chebyshev cells of it, outside every currently active
/// `Jammer`'s radius, and not `AgentState.RadioDestroyed`.
///
/// A leaf, exactly as `Suppression`/`Stress` are: pure, total, no event
/// emission (`Simulation.communication`/`.combat` own that), no mutation of
/// its inputs.

/// Every communication threshold, in one place (`docs/05` section 15
/// "Record every threshold in one configuration structure"). Module
/// literals, the `PerceptionConfig`/`CombatConfig` precedent: these values
/// affect authoritative outcomes, so not `SimConfig`.
[<RequireQualifiedAccess>]
module CommsConfig =

    /// Communication range from `WorldState.Headquarters`, in cells, as a
    /// Chebyshev radius — the `PerceptionConfig.SightRange` precedent.
    /// Meaningful only when a `Headquarters` is authored.
    [<Literal>]
    let Range = 15

    /// Ticks an in-range, unjammed order takes to arrive once accepted
    /// (`AgentState.PendingDelivery`'s due tick). A flat constant (Central
    /// decision 3): every authored-`Headquarters` scenario's order delivery
    /// lags acceptance by exactly this many ticks, regardless of distance
    /// within `Range`.
    [<Literal>]
    let DeliveryDelayTicks = 3L

    /// Chance, on the `docs/04` section 4 `0..1000` normalised scale (the
    /// `Combat.hitChance` precedent), that a qualifying combat hit also
    /// permanently destroys the target's radio (`AgentState.RadioDestroyed`).
    /// Rolled only when the world authors a `Headquarters` (the opt-in
    /// gate) and only on a hit, alongside the existing wound roll.
    [<Literal>]
    let RadioDestroyChanceOnHit = 150

[<RequireQualifiedAccess>]
module Communication =

    /// Whether `cell` lies within an active jammer's radius at `tick`
    /// (`ActiveFromTick <= tick <= ActiveUntilTick`, both inclusive).
    let jammed (jammers: Jammer[]) (tick: int64) (cell: Cell) : bool =
        jammers
        |> Array.exists (fun j ->
            j.ActiveFromTick <= tick
            && tick <= j.ActiveUntilTick
            && Perception.chebyshev j.Position cell <= j.Radius)

    /// Whether `agent` can currently receive an order: the existing static
    /// `AgentState.CommunicationAvailable` check (TASK-027, unchanged)
    /// **and**, only when `headquarters` is `Some` (the opt-in gate,
    /// Central decision 6), within `CommsConfig.Range` of it, not
    /// `jammed`, and not `AgentState.RadioDestroyed`. `headquarters =
    /// None` reduces this to exactly the pre-TASK-058 static check, so
    /// every scenario that authors no `Headquarters` is behaviourally
    /// unaffected by this module's existence.
    let available (headquarters: Cell option) (jammers: Jammer[]) (tick: int64) (agent: AgentState) : bool =
        agent.CommunicationAvailable
        && (match headquarters with
            | None -> true
            | Some hq ->
                not agent.RadioDestroyed
                && Perception.chebyshev hq agent.Position <= CommsConfig.Range
                && not (jammed jammers tick agent.Position))

    /// The specific reason `available` returned `false` for `agent`, in the
    /// fixed priority order `Events.fs`'s `DeliveryFailure` doc comment
    /// names: the static blackout first, then out of range, then jammed,
    /// then radio-destroyed. Only called once `available` is already known
    /// `false`; returns `None` in the (unreachable, in practice) case where
    /// every condition actually holds — a defensive total function rather
    /// than a partial one.
    let reason (headquarters: Cell option) (jammers: Jammer[]) (tick: int64) (agent: AgentState) : DeliveryFailure option =
        if not agent.CommunicationAvailable then
            Some UnableToCommunicate
        else
            match headquarters with
            | None -> None
            | Some hq ->
                if Perception.chebyshev hq agent.Position > CommsConfig.Range then
                    Some OutOfRange
                elif jammed jammers tick agent.Position then
                    Some Jammed
                elif agent.RadioDestroyed then
                    Some RadioDestroyed
                else
                    None
