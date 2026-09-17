namespace CommandoWar.Sim

/// Wound accumulation and bleed-out (TASK-045, backlog B-031;
/// `docs/04_SIMULATION_SPEC.md` sections 12.8/12.9; `docs/05` section 8).
///
/// This is a leaf, exactly as `Suppression`/`Stress` are: pure, total,
/// integer-only, `Domain` only, no event emission (the Combat and
/// State-consequences phases own that), no mutation of its inputs.
///
/// ## What is deliberately absent (later backlog items or explicitly out of
/// this task's scope)
///
///   * a rescue/stabilize player action — TASK-045 Central decision 3, "no
///     rescue mechanic"; a fixed countdown always runs to `Dead`;
///   * ammunition, weapon readiness, fire-rate/cooldown — still unassigned
///     future work (`Combat.fs`'s own header);
///   * explosion- or isolation-driven wounds, or stress from a *nearby*
///     casualty (as opposed to the wounded agent's own resolve penalty,
///     `Appraisal.fs`) — `docs/05` section 8's other two unrealised stress
///     sources, no system exists to produce either;
///   * a `Squads`/`SquadStore` construct or an authored leader flag — leader
///     identity is a pure derived rule over `WorldState.Agents`
///     (`Simulation.fs`'s `stateConsequences`), not this leaf's concern.
[<RequireQualifiedAccess>]
module CasualtyConfig =

    /// Full health for a freshly created agent, from a construction path
    /// that authored none. Kept as a literal here (the `AppraisalConfig.
    /// DisciplineDefault` precedent) to avoid a module-ordering dependency;
    /// `Agent.MaxHealth` (`Domain.fs`) carries the same value and is what
    /// `Agent.create` actually uses.
    [<Literal>]
    let MaxHealth = 1000

    /// Health lost by the target of a qualifying **hit** — never a miss,
    /// unlike `Suppression.gain`: `docs/04` section 12.8 wants suppression
    /// "independent of a hit", but a wound is physical, so only a landed
    /// shot causes one. No cover mitigation on the amount itself: cover
    /// already gated whether the hit connected at all (`Combat.hitChance`),
    /// so mitigating the wound a second time would double-count it.
    /// Roughly three qualifying hits to incapacitate an unwounded agent.
    [<Literal>]
    let WoundPerHit = 350

    /// Ticks an `Incapacitated` agent counts down before `Dead`. 60 (3
    /// seconds at the standard 20 Hz tick rate): long enough to read as a
    /// real countdown in a diagnostic trace, short enough that "no rescue"
    /// does not feel like a stalled animation.
    [<Literal>]
    let BleedOutTicks = 60

[<RequireQualifiedAccess>]
module Casualty =

    /// Applies one qualifying hit's wound to an `Alive` agent's vitals:
    /// `Alive health -> Alive (health - WoundPerHit)`, or straight to
    /// `Incapacitated BleedOutTicks` if that would reach zero or below —
    /// never a binary kill. Only ever called on an `Alive` target (the
    /// Combat phase already excludes a non-`Alive` agent from being
    /// targeted at all, so `Incapacitated`/`Dead` inputs are unreachable in
    /// practice); returns non-`Alive` inputs unchanged rather than crashing,
    /// since a pure leaf should stay total.
    let wound (vitals: VitalStatus) : VitalStatus =
        match vitals with
        | Alive health ->
            let remaining = health - CasualtyConfig.WoundPerHit
            if remaining <= 0 then Incapacitated CasualtyConfig.BleedOutTicks else Alive remaining
        | Incapacitated _
        | Dead -> vitals

    /// One tick's bleed-out countdown: `Incapacitated n -> Incapacitated
    /// (n - 1)`, or `Dead` once `n` reaches 1 (so `Incapacitated 0` never
    /// exists as a persisted value — the transition to `Dead` happens in
    /// the same tick the countdown would reach it). Idempotent on `Alive`/
    /// `Dead` — returns the input unchanged, the `wound` precedent for a
    /// total leaf.
    let tickBleedOut (vitals: VitalStatus) : VitalStatus =
        match vitals with
        | Incapacitated remaining -> if remaining <= 1 then Dead else Incapacitated(remaining - 1)
        | Alive _
        | Dead -> vitals

    /// Whether an agent can act at all (`docs/04` section 20: "dead or
    /// incapacitated agents do not start new actions") — `Combat`,
    /// `NavigationAndMovement`, `Appraisal`'s stage-2 check, and the
    /// leadership/squad-failure derivations below all read this one
    /// predicate rather than pattern-matching `VitalStatus` independently.
    let isAlive (vitals: VitalStatus) : bool =
        match vitals with
        | Alive _ -> true
        | Incapacitated _
        | Dead -> false

    /// The current squad leader (TASK-045, backlog B-031; `docs/05` section
    /// 17's "a simple replacement rule"): the lowest-`AgentId` `Alive`
    /// `Friendly` agent, or `None` if every friendly is down. A pure derived
    /// rule — no stored leader field anywhere (`Commitment.ofAgent`'s own
    /// "derived, not stored, so it cannot drift" precedent) — so succession
    /// is automatic: once the lowest id is no longer `Alive`, the next
    /// lowest survivor is the leader the very next time this is computed.
    /// Reused by `Simulation.stateConsequences` (to detect and report a
    /// transition) and `Diagnostics` (the `SquadLeadership` overlay), so it
    /// lives here rather than being duplicated in both.
    let currentLeader (agents: AgentState[]) : AgentId option =
        agents
        |> Array.filter (fun a -> a.Side = Friendly && isAlive a.Vitals)
        |> Array.sortBy (fun a -> AgentId.value a.Id)
        |> Array.tryHead
        |> Option.map (fun a -> a.Id)
