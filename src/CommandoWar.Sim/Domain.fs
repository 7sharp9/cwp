namespace CommandoWar.Sim

/// Which force an agent belongs to. Squads, weapons, wounds, morale, trust
/// and the other agent dimensions in docs/04_SIMULATION_SPEC.md section 11
/// are added by later tasks.
type Side =
    | Friendly
    | Hostile

/// The cardinal path an agent is following toward its destination, with the
/// index in it of the agent's current cell.
///
/// This is a **non-canonical derived cache** (TASK-015): at every tick it is a
/// pure deterministic function of `(AgentState.Position, AgentState.Destination,
/// WorldState.Terrain)`. `Terrain` is immutable within a run (ADR-0002
/// amendment); `Position` and `Destination` are already in the canonical
/// image; and `Pathfinding.findWithin` is total, pure, integer-only, and
/// deterministic (TASK-013). Two runs of the same inputs therefore produce
/// identical caches, so it cannot diverge and is **excluded** from
/// `Canonical.encode` (`docs/04_SIMULATION_SPEC.md` section 17, "derived caches
/// either excluded or normalised"). `Canonical.FormatVersion` stays 1.
///
/// `Cells.[0]` is the origin the path was planned from, `Cells.[Cells.Length -
/// 1]` is the destination, `Cells.[Cursor]` is the agent's current cell, and
/// `Cost` is the `Pathfinding` integer cost of the whole path. Only ever `Some`
/// while the agent is mid-route; cleared on arrival or when blocked.
type MovementPath =
    { Cells: Cell[]
      Cursor: int
      Cost: int }

/// One contact in the squad's shared tactical picture (TASK-026,
/// `docs/05_COMMAND_AND_AGENT_AI.md` section 3 "Squad tactical picture",
/// `docs/04_SIMULATION_SPEC.md` section 12.4). The Tactical-knowledge phase
/// merges every friendly's observations this tick into one array of these,
/// sorted ascending by `Contact` id (stable iteration, `docs/04` section 6).
///
/// This store **has memory**: `LastSeenTick` and the decaying `Confidence`
/// cannot be recomputed from the current tick's positions (a contact stays in
/// the picture, with an ageing confidence, for `PerceptionConfig.ExpireAfter`
/// ticks after it was last seen). It is therefore **genuine per-tick canonical
/// state**, not a derived cache — unlike `AgentState.VisibleContacts` — so it
/// enters `Canonical.encode` and `Canonical.FormatVersion` is `3` (the
/// ADR-0002 amendment "Static authoritative data and the canonical image").
type Contact =
    { /// The observed agent's id. Identity is always known at this stage
      /// (`docs/05` section 3 "contact ID when identity is known"); a
      /// suspected-only contact without an id is deferred.
      Contact: AgentId
      /// The cell the contact was last observed in.
      LastKnownCell: Cell
      /// The tick a friendly last saw this contact. Non-decreasing until the
      /// contact expires and is removed.
      LastSeenTick: int64
      /// Confidence on the `docs/04` section 4 `0..1000` scale:
      /// `PerceptionConfig.ConfidenceFull` while the contact is currently
      /// seen or was seen within `PerceptionConfig.StaleAfter` ticks, dropping
      /// one band (`PerceptionConfig.ConfidenceBandDrop`) once it goes stale.
      Confidence: int }

/// What a command asks an agent to do (moved here from `Commands.fs` by
/// TASK-028: `AgentState.Order` below carries a `PlayerIntent`, and `Domain.fs`
/// compiles before `Commands.fs`).
///
/// `Suppress` (TASK-037, a thin B-030 slice pulled forward as P3
/// decision-support) names a specific known contact by `AgentId`, not a bare
/// `Cell`: `docs/05` section 4's "known or suspected threat area" wording
/// also covers an unconfirmed, id-less "suspected" target, but no
/// suspected-threat model exists anywhere in the codebase, so that half stays
/// out (`AGENTS.md` "no speculative type machinery"). The target must be a
/// contact already present in the issuing agent's own tactical knowledge
/// (`Appraisal.appraise`'s `Unable(TargetNotKnown)` check) — never
/// authoritative hostile state (risk R-023).
///
/// `Hold`, `Assault`, and `Withdraw` (TASK-047, backlog B-030 proper) all
/// take a bare `Cell` target, the `MoveTo` precedent (`docs/07` section 4's
/// "`HoldArea`/`AssaultArea`" naming means "an area the player designates by
/// cell," not a new region/rectangle type). `Hold`'s stage-2 appraisal may
/// redirect to a nearby covered cell (`Appraisal.bestCoverNear`) rather than
/// the literal `area`; `Assault`'s stage-4 appraisal is deliberately
/// stricter (`AppraisalConfig.AssaultResolvePenalty`) and its executor is a
/// staged finite-state machine (`Commitment.AssaultStage`); `Withdraw`'s
/// stage-4 appraisal is deliberately more lenient
/// (`AppraisalConfig.WithdrawResolveBonus`) — see `TASK-047-ASSAULT-
/// WITHDRAW-HOLD-AND-AMMUNITION.md` Decisions D-F.
type PlayerIntent =
    | MoveTo of target: Cell
    | Suppress of target: AgentId
    | Hold of area: Cell
    | Assault of target: Cell
    | Withdraw of target: Cell

/// How urgently an order should be acted on, relative to an agent's current
/// activity (moved here from `Commands.fs` by TASK-028). `docs/05` section 5
/// stage 4 names "urgency ... encoded by the order" as a resolve-threshold
/// input; TASK-028's Appraisal phase reads it (`AppraisalConfig.UrgencyImmediate`).
type Urgency =
    | Routine
    | Immediate

/// How much risk an order sanctions (moved here from `Commands.fs` by
/// TASK-028). `docs/05` section 5 stage 4 names "risk tolerance encoded by the
/// order" as a resolve-threshold input; TASK-028's Appraisal phase reads it
/// (`AppraisalConfig.RiskCautious` / `RiskAggressive`).
type RiskTolerance =
    | Cautious
    | Standard
    | Aggressive

/// Whether a new `Order` command replaces an agent's active order (and
/// clears anything already queued behind it) or joins the tail of
/// `AgentState.OrderQueue` (TASK-044, backlog B-051). `Replace` is the
/// default for every existing builder in `Commands.fs`, preserving every
/// pre-TASK-044 caller's exact behaviour; `Append` is reached only through
/// `Command.queued`. Moved here from `Commands.fs` by TASK-058 (backlog
/// B-016b): `AgentState.PendingDelivery` carries one, and `Domain.fs`
/// compiles before `Commands.fs`.
type QueueMode =
    | Replace
    | Append

/// An order that has been delivered to an agent and awaits (or already holds)
/// an appraisal outcome (TASK-028, backlog B-017; `docs/04` section 11
/// "current order", section 12.5). The Communication phase (12.2) writes this
/// from the accepted `PlayerCommand` and resets `AgentState.Disposition` to
/// `None`; the Appraisal phase (12.5) reads it.
///
/// **Genuine canonical per-tick state** (`Canonical.FormatVersion` 4): it
/// carries `IssuedAtTick` provenance that lives only in the command envelope,
/// and it survives ticks so appraisal is not re-run every tick (`docs/04`
/// section 12.5 "reappraise only on material triggers"). Cleared when the
/// order is fulfilled (the agent reaches the target) or superseded by a new
/// order.
type ReceivedOrder =
    { Command: CommandId
      Intent: PlayerIntent
      IssuedAtTick: int64
      Urgency: Urgency
      RiskTolerance: RiskTolerance }

/// A typed reason for an appraisal outcome (TASK-028; `docs/05` section 7
/// "structured reasons" — "do not add prose-only reasons; UI text is derived
/// from structured values"). TASK-028 realised the two reasons its staged
/// checks could produce; TASK-037 adds the one a `Suppress` order's stage-2
/// check can produce. The rest of the `docs/05` vocabulary (`RouteBlocked`,
/// `HeavySuppression`, `CriticallyWounded`, `MissingCapability`, ...) arrives
/// with the systems that can trigger it (B-019 / B-020 / B-021 / B-030). The
/// doc's `ContactId` is `AgentId` in the code.
type DecisionReason =
    /// Stage 2: no known traversable route connects the agent's cell to the
    /// order target (`Pathfinding.findWithin` returned `NoPath` /
    /// `BudgetExhausted` / `InvalidEndpoint`).
    | NoKnownRoute
    /// Stage 4: the known route's exposure to observed threats exceeds the
    /// agent's resolve threshold. `threat` is the single highest-contributing
    /// contact from `WorldState.TacticalKnowledge`, or `None` when the
    /// pressure is diffuse.
    | RouteTooExposed of threat: AgentId option
    /// Stage 2 (TASK-037): a `Suppress` order names an `AgentId` absent from
    /// the issuing agent's own tactical knowledge — never authoritative
    /// hostile state (risk R-023), so this is checked against
    /// `WorldState.TacticalKnowledge` / `HostileTacticalKnowledge`, not
    /// `WorldState.Agents`.
    | TargetNotKnown
    /// Stage 2 (TASK-045, backlog B-031): the recipient's own
    /// `AgentState.Vitals` is not `Alive` — `docs/05` section 4 stage 2's "is
    /// the agent alive, conscious, and mobile?", and section 16's own
    /// example: "a critically wounded agent reports unable rather than
    /// refused." A whole-order short-circuit ahead of every other stage-2
    /// check, for any `PlayerIntent`.
    | CriticallyWounded
    /// Stage 2 (TASK-047, backlog B-030 proper): a `Suppress` or `Assault`
    /// order — the two intents that explicitly plan to initiate fire — names
    /// an agent whose `AgentState.Ammo` is entirely empty (`Ready(0, 0)`, no
    /// magazine and no reserve). Not checked for `MoveTo`/`Hold`/`Withdraw`:
    /// an unarmed agent can still walk, hold ground, or retreat. A partial
    /// or mid-reload state still appraises `Accepted` — the agent may simply
    /// run dry mid-engagement, an emergent outcome, not a blocking one.
    | InsufficientAmmunition

/// The agent's appraisal of its current `Order` (TASK-028; `docs/05` section
/// 6). TASK-028 subset: `Adapted` (stage 5 safer adaptation) is B-018 and
/// `Delayed` (a `ResumeCondition` mechanism) is B-021 — neither gets a case,
/// per `AGENTS.md` "do not build speculative type machinery".
///
/// `Refused` and `Unable` carry a primary `DecisionReason` **by construction**,
/// so the `docs/04` section 20 invariant "every refusal and adaptation
/// contains at least one structured reason" holds without a separate check.
/// **Genuine canonical per-tick state** (`Canonical.FormatVersion` 4) — the
/// "already appraised, unchanged" fast path in the Appraisal phase reads it,
/// and a `Refused` outcome persists (with its reasons) for an idle agent.
type OrderDisposition =
    | Accepted
    | Refused of primary: DecisionReason * supporting: DecisionReason[]
    | Unable of primary: DecisionReason * supporting: DecisionReason[]

/// An agent's casualty state (TASK-045, backlog B-031; `docs/04` sections
/// 12.8/12.9, `docs/05` section 8). `Alive health` carries the remaining
/// health on the `0..CasualtyConfig.MaxHealth` scale (the `Suppression`/
/// `Stress` precedent); reaching 0 moves to `Incapacitated`, never straight
/// to `Dead` — `docs/04` section 12.8's own direction, "death as a critical/
/// bleed-out timer rather than a binary kill". `Incapacitated
/// bleedOutRemaining` counts down every tick (`Casualty.tickBleedOut`) to
/// `Dead` with nothing a player command can do about it (no rescue/stabilize
/// mechanic — TASK-045 Central decision 3). A single field rather than a
/// separate `Health: int` alongside a status flag, so "wounded but not
/// alive" or "dead with remaining health" cannot be constructed
/// (`AGENTS.md` "make invalid states hard to construct").
type VitalStatus =
    | Alive of health: int
    | Incapacitated of bleedOutRemaining: int
    | Dead

/// An agent's ammunition state (TASK-047, backlog B-030 proper; `docs/04`
/// sections 12.8/12.9, `docs/05` section 8's "is required ammunition ...
/// available?"). `Ready magazine reserve` carries rounds ready to fire and
/// rounds in reserve stock; `Reloading reserve ticksRemaining` carries only
/// the reserve — a reloading weapon's magazine is definitionally empty, so
/// there is no `Magazine: int` alongside a status flag to go stale (the
/// `VitalStatus` "single field rather than a separate flag" idiom, `AGENTS.md`
/// "make invalid states hard to construct"). `Ammo.fs` owns every transition.
type AmmoState =
    | Ready of magazine: int * reserve: int
    | Reloading of reserve: int * ticksRemaining: int

/// Minimal authoritative agent state for the simulation skeleton: identity,
/// side, logical position, movement progress within the current edge, an
/// optional movement destination, the (non-canonical, derived) path the
/// agent is following toward it, and the (non-canonical, derived) set of
/// opposing agents it can currently see. Facing and stance are deferred until
/// a real movement model exists.
type AgentState =
    { Id: AgentId
      Side: Side
      Position: Cell
      /// Integer progress toward entering the next cell along `Route`
      /// (TASK-018, docs/04 section 8: "advance movement progress by an
      /// integer amount each tick; enter the next cell when progress reaches
      /// the threshold"). The threshold is `Terrain.moveCost` of the next
      /// cell (the same value `Pathfinding` already uses as its edge weight);
      /// the per-tick increment is `Terrain.BaseMoveCost`. Always 0 at rest,
      /// on arrival, when blocked, and immediately after entering a cell —
      /// it measures progress along the *current* edge only, never carried
      /// past it. Unlike `Route`, this cannot be recomputed from `Position`
      /// alone (`Position` does not change while an edge is in progress, so
      /// nothing else records how many ticks have been spent on it): it is
      /// genuine new per-tick canonical state, part of `Canonical.encode`
      /// (`Canonical.FormatVersion` 2).
      Progress: int
      Destination: Cell option
      /// The path the agent is currently following (TASK-015). A derived
      /// cache: recomputed deterministically from `(Position, Destination,
      /// Terrain)` by the Navigation and movement phase, and excluded from
      /// `Canonical.encode`. `None` when the agent has no destination.
      Route: MovementPath option
      /// The opposing-side agents this agent can currently see, ascending by
      /// id (TASK-026, `docs/04` section 12.3 "current visible contacts").
      /// Rewritten from scratch every tick by the Perception phase.
      ///
      /// A **non-canonical derived cache**, on the identical argument that
      /// keeps `Route` out of `Canonical.encode`: it is a pure deterministic
      /// function of every agent's `Position`, the immutable `Terrain`, and
      /// the `PerceptionConfig` constants (`Sight.visible` is total, pure,
      /// integer-only). Two runs of the same inputs produce identical
      /// `VisibleContacts`, so it cannot diverge and is **excluded** from
      /// `Canonical.encode` (`docs/04` section 17). Empty at rest and for an
      /// agent with no opposing agent in sight range and line of sight.
      VisibleContacts: AgentId[]
      /// The order currently delivered to this agent (TASK-028, backlog
      /// B-017; `docs/04` section 11 "current order", section 12.5). The
      /// Communication phase writes it (and resets `Disposition` to `None`);
      /// the Appraisal phase reads it. **Genuine canonical per-tick state**
      /// (`ReceivedOrder`): it carries `IssuedAtTick` provenance and survives
      /// ticks so appraisal is not re-run every tick. `None` when the agent
      /// holds no order; cleared when the order is fulfilled or superseded.
      Order: ReceivedOrder option
      /// Orders stacked behind `Order`, awaiting their turn (TASK-044,
      /// backlog B-051). Insertion order, not sorted — the queue's order
      /// **is** the meaningful state, the one deliberate exception to the
      /// rest of this codebase's "sort before encoding" convention
      /// (`Canonical.fs`). A `PlayerCommand` whose `PlayerCommandBody` is
      /// `Order(_, Append)` is appended here when the recipient already
      /// holds an active `Order` (`Simulation.communication`); the head is
      /// promoted into `Order` when the active order is fulfilled
      /// (`Simulation.commitmentAndLocalAction`, `MoveTo` only) or
      /// explicitly cancelled (`Simulation.communication`, a `Cancel`
      /// command). An `Order(_, Replace)` command clears this list, exactly
      /// as it replaces `Order` itself. Cancelling one specific queued
      /// entry's `CommandId` splices it out without touching the rest.
      ///
      /// **Genuine canonical per-tick state** (`Canonical.FormatVersion` 9):
      /// real per-tick memory no other field reproduces, the `Order`/
      /// `Disposition` precedent exactly. Always `[]` before this task and
      /// for every agent never issued a queued order.
      OrderQueue: ReceivedOrder list
      /// This agent's appraisal outcome for `Order` (TASK-028). `None` until
      /// the Appraisal phase has run on the current `Order`; `Some` once
      /// appraised. **Genuine canonical per-tick state** — the Appraisal
      /// phase's "already appraised, unchanged" fast path reads it, and a
      /// `Refused` outcome persists (with its reasons) for an idle agent. The
      /// Appraisal phase writes `Destination` only when this is `Some Accepted`.
      Disposition: OrderDisposition option
      /// This agent's discipline (TASK-028, backlog B-017; `docs/05` section
      /// 8 "stable trait influencing willingness to maintain a valid
      /// commitment under pressure"). A non-negative integer used only in the
      /// stage-4 resolve threshold (`AppraisalConfig.DisciplineResolveWeight`).
      ///
      /// **Static authoritative data at this stage**: set once from the
      /// authored scenario (`Deployment.Discipline`, default
      /// `AppraisalConfig.DisciplineDefault`) and never mutated during a run.
      /// Like `CommunicationAvailable` it is therefore **excluded** from
      /// `Canonical.encode` (`docs/04` section 17; the ADR-0002 amendment) —
      /// both runs load the identical value at tick 0 and it cannot diverge.
      /// A discipline-driven behaviour difference still surfaces in the hash
      /// within one tick through the agent's `Disposition` and `Position`.
      /// Dynamic discipline (and stress / trust) is backlog B-021; that task
      /// moves it into the canonical image and bumps `Canonical.FormatVersion`,
      /// exactly as the amendment specifies.
      Discipline: int
      /// Whether an order issued this tick reaches this agent (TASK-027,
      /// backlog B-016; `docs/04` section 11 "communication availability",
      /// section 12.2). The Communication phase writes `Destination` for a
      /// recipient with `CommunicationAvailable = true` and emits
      /// `OrderUndelivered` for one with `false`.
      ///
      /// **Static authoritative data at this stage**: set once from the
      /// authored scenario (`Deployment.CommunicationAvailable`, default
      /// `true`) and never mutated during a run — an authored "comms
      /// blackout", not a dynamic radio model. Like `WorldState.Terrain` it is
      /// therefore **excluded** from `Canonical.encode` (`docs/04` section 17;
      /// the ADR-0002 amendment "Static authoritative data and the canonical
      /// image"): both runs load the identical value at tick 0 and it cannot
      /// diverge, so hashing it would move every pinned fixture hash for a
      /// constant. A comms-derived behaviour bug still surfaces in the hash
      /// within one tick through the recipient's `Position`. Radio range,
      /// delivery delay, dynamic jamming, and radio-destroyed are B-016b; when
      /// comms availability becomes per-tick mutable that task bumps
      /// `Canonical.FormatVersion` and adds it to the image, exactly as the
      /// amendment specifies for `Terrain`.
      CommunicationAvailable: bool
      /// Whether this agent's radio has been permanently destroyed (TASK-058,
      /// backlog B-016b): once `true`, `Communication.available` is `false`
      /// for this agent for the rest of the run regardless of range or
      /// jamming, even though the agent itself may remain `Alive` and keep
      /// fighting — a distinct failure mode from `Vitals` (TASK-055's
      /// "downed agent" gap is about the *target* of perception, not the
      /// agent's own ability to *receive* orders). Set by the Combat phase:
      /// a qualifying hit against an agent has a
      /// `CommsConfig.RadioDestroyChanceOnHit` chance to flip this,
      /// evaluated only when the world's `Headquarters` is authored (the
      /// opt-in gate — every existing scenario without one draws no extra
      /// random number and never sets this). No repair mechanic — sticky
      /// once `true`, the `Vitals.Dead` one-way-door precedent.
      ///
      /// **Genuine canonical per-tick state** (the `Vitals`/`RadioDestroyed`
      /// precedent): it changes from a gameplay event and cannot be
      /// recomputed from any other field. Defaults to `false`.
      RadioDestroyed: bool
      /// An order accepted this tick but not yet delivered (TASK-058,
      /// backlog B-016b): `Some(order, mode, dueTick)` while it is in
      /// flight toward this agent, delivered (`AgentState.Order`/
      /// `.OrderQueue` written, `OrderDelivered` emitted) once the tick
      /// reaches `dueTick` and `Communication.available` still holds —
      /// re-checked at delivery time, since the agent may have moved out of
      /// range or into a jammer since the order was issued. Only ever
      /// non-`None` when the world's `Headquarters` is authored (the
      /// opt-in gate): without one, `Simulation.communication` still
      /// delivers same-tick exactly as before TASK-058, so this field never
      /// leaves `None` for any of the 16 pre-existing corpus entries. A new
      /// pending command for this recipient (of either `QueueMode`)
      /// supersedes an in-flight one outright, the `Order`-replace
      /// precedent extended to "not yet arrived".
      ///
      /// **Genuine canonical per-tick state**: real per-tick memory no
      /// other field reproduces. Defaults to `None`.
      PendingDelivery: (ReceivedOrder * QueueMode * int64) option
      /// This agent's current suppression on the `0..1000` scale (TASK-032,
      /// backlog B-020; `docs/05` section 8 "immediate effect of hostile fire
      /// and impacts"). The Combat phase raises it on a qualifying shot
      /// (`Suppression.gain`, independent of a hit, mitigated by directional
      /// `Terrain.cover`); the State consequences phase decays it every tick
      /// (`Suppression.decay`, floored at 0).
      ///
      /// **Genuine canonical per-tick state**, the `Order` / `Disposition`
      /// precedent, not the `Discipline` one: it changes every tick from
      /// gameplay events and cannot be recomputed from `Position` alone, so
      /// under the ADR-0002 amendment it is in `Canonical.encode`
      /// (`Canonical.FormatVersion` 5). Defaults to `0` with **no
      /// scenario-authored override** (the `Progress` precedent) — every
      /// agent starts unsuppressed. "Reduces action effectiveness, raises
      /// assault pressure, and may trigger taking cover" (docs/05 section 8)
      /// are not realised by any system yet; this is the raw value only.
      Suppression: int
      /// Whether this agent's `Suppression` currently sits in the "suppressed"
      /// hysteresis band (TASK-033, backlog B-021; `docs/05` sections 14/15
      /// "use hysteresis when entering and leaving ... suppression ...
      /// states"). Latches `true` once `Suppression >=
      /// AppraisalConfig.SuppressionBandEnter` and stays `true` until it drops
      /// to `AppraisalConfig.SuppressionBandExit` or below, so a value
      /// oscillating right at one threshold does not flip every tick. Read
      /// (and updated) by the Appraisal phase at the top of the tick, against
      /// last tick's finalised `Suppression`; a flip is the suppression-band
      /// reappraisal trigger (`docs/05` section 14) and also subtracts
      /// `AppraisalConfig.SuppressionBandPenalty` from the stage-4 resolve
      /// threshold while latched `true`.
      ///
      /// **Genuine canonical per-tick state** (the `Suppression` precedent):
      /// it is a hysteresis latch, not recomputable from `Suppression` alone
      /// without also knowing which side of the band it was already on.
      /// Defaults to `false` with no scenario-authored override (the
      /// `Suppression` precedent) — every agent starts unlatched. Tracked for
      /// both sides symmetrically, exactly as `Suppression` itself is,
      /// though only a `Friendly` agent's `Order` / `Disposition` ever read
      /// it, since only friendlies receive player orders.
      SuppressionBand: bool
      /// This agent's current stress on the `0..1000` scale (TASK-033,
      /// backlog B-021; `docs/05` section 8 "accumulates through nearby
      /// casualties, wounds, isolation, explosions, and threat; decays when
      /// safe"). Of those five sources, only "threat" has a system behind it
      /// today: the State-consequences phase raises it by
      /// `StressConfig.GainPerTick` every tick this agent's
      /// `VisibleContacts` is non-empty (`Stress.gain`), then always decays
      /// it by `StressConfig.DecayPerTick`, floored at 0 (`Stress.decay`) —
      /// the `Suppression` gain-then-decay-same-tick precedent, but both
      /// steps live in State consequences here since nothing else produces
      /// stress yet. Read by the stage-4 resolve threshold as a continuous
      /// drag (`AppraisalConfig.StressDivisor`), distinct from
      /// `SuppressionBand`'s discrete banded penalty.
      ///
      /// **Genuine canonical per-tick state**, the `Suppression` precedent:
      /// it changes every tick from gameplay events and cannot be recomputed
      /// from `Position` alone. Defaults to `0` with no scenario-authored
      /// override (the `Suppression` precedent) — every agent starts
      /// unstressed. Casualty-, wound-, explosion-, and isolation-driven
      /// stress are not realised by any system yet (B-031 and unassigned
      /// future work); this is the contact-driven component only.
      Stress: int
      /// This agent's casualty state (TASK-045, backlog B-031). The Combat
      /// phase applies `Casualty.wound` to a qualifying hit's target
      /// (`Alive health -> Alive (health - WoundPerHit)`, or straight to
      /// `Incapacitated BleedOutTicks` at zero — never a binary kill); the
      /// State-consequences phase ticks `Incapacitated`'s countdown to
      /// `Dead`. Read by Appraisal's stage 2 (`Unable(CriticallyWounded)`
      /// when not `Alive`) and stage 4 (a continuous resolve penalty while
      /// `Alive` but wounded); read by Combat and Navigation-and-movement to
      /// exclude a non-`Alive` agent from firing, being targeted, or moving
      /// (`docs/04` section 20: "dead or incapacitated agents do not start
      /// new actions").
      ///
      /// **Genuine canonical per-tick state** (the `Suppression` precedent):
      /// it changes from gameplay events and cannot be recomputed from
      /// `Position` alone. Defaults to `Alive CasualtyConfig.MaxHealth` —
      /// every agent starts at full health.
      Vitals: VitalStatus
      /// One-shot dirty flag: `true` for exactly one tick after a qualifying
      /// hit strictly reduced this agent's health while `Alive` (TASK-045,
      /// backlog B-031) — the "the agent is wounded" reappraisal trigger
      /// (`docs/05` section 14). Exists because `Combat` (phase 8) runs
      /// *after* `Appraisal` (phase 5) within a tick, so a wound taken this
      /// tick cannot be seen by this tick's Appraisal the way `Perception`'s
      /// same-tick `ContactObserved` can; this flag bridges that gap exactly
      /// as `SuppressionBand`'s persisted latch does for the
      /// suppression-band trigger, minus the hysteresis (any wound
      /// qualifies, there is no band). Set by `stateConsequences` when a
      /// wound is applied; read and cleared back to `false` by the
      /// *following* tick's `appraisal` phase — its own consumption.
      ///
      /// **Genuine canonical per-tick state**: real per-tick memory no other
      /// field reproduces. Defaults to `false` — every agent starts
      /// unwounded.
      RecentlyWounded: bool
      /// This agent's ammunition (TASK-047, backlog B-030 proper). The
      /// Combat phase gates firing on `Ammo.canFire` and consumes one round
      /// per qualifying shot (`Ammo.fire`); the State-consequences phase
      /// ticks an empty weapon into `Reloading` and back
      /// (`Ammo.tick`), and instantly refills an agent standing on an
      /// authored `WorldState.ResupplyAreas` cell (`Ammo.resupply`). Read by
      /// Appraisal's stage 2 (`Unable(InsufficientAmmunition)` for a
      /// `Suppress`/`Assault` order when totally empty).
      ///
      /// **Genuine canonical per-tick state** (the `Suppression`/`Vitals`
      /// precedent): it changes every tick from gameplay events and cannot
      /// be recomputed from `Position` alone. Defaults to `Ready
      /// (AmmoConfig.MagazineSize, AmmoConfig.ReserveStart)` — every agent
      /// starts fully armed, no scenario-authored override (the
      /// `Suppression` precedent).
      Ammo: AmmoState
      /// This agent's movement speed (TASK-049, backlog B-058). The per-tick
      /// progress increment `Simulation.navigationAndMovement` applies stays
      /// the universal `Terrain.BaseMoveCost` for every agent (so
      /// `AgentState.Progress`'s stored trajectory is exactly the elapsed
      /// real-tick sequence 0, 1, 2, ... it always has been); only the
      /// completion *comparison* scales, cross-multiplied against
      /// `Agent.MoveSpeedDefault`: an agent whose `MoveSpeed` equals that
      /// constant reproduces the pre-TASK-049 comparison byte-for-byte
      /// (multiplying both sides of an inequality by the same constant does
      /// not change it), while a smaller value genuinely needs
      /// proportionally more ticks to cross the same cell. Read only by
      /// `Simulation.navigationAndMovement`; `Pathfinding`'s route cost is
      /// unaffected (it measures `Terrain.moveCost` only, never real-time
      /// ticks).
      ///
      /// **Static authoritative data at this stage**, the `Discipline`
      /// precedent exactly: set once from the authored scenario
      /// (`Deployment.MoveSpeed`, resolved from `Scenario.validate`'s
      /// unit-type table; default `Agent.MoveSpeedDefault`) and never
      /// mutated during a run. Therefore **excluded** from `Canonical.encode`
      /// (the ADR-0002 amendment): both runs load the identical value at
      /// tick 0 and it cannot diverge. A speed-driven behaviour difference
      /// still surfaces in the hash within one tick through the agent's
      /// `Position`/`Progress`.
      MoveSpeed: int
      /// This agent's formation slot offset (TASK-059, backlog B-011d), or
      /// `None` for an unformationed agent -- every scenario authored before
      /// this task. Read only by `Appraisal.appraise`'s `MoveTo` case
      /// (`Appraisal.resolveFormationTarget`): the order's own literal
      /// target cell is treated as the formation anchor, and the agent's
      /// real pathfinding target becomes anchor + this offset (redirected to
      /// the nearest passable, unoccupied cell in range if the exact offset
      /// cell is blocked or occupied). `Hold`/`Assault`/`Withdraw`/`Suppress`
      /// targets are unaffected regardless of this field.
      ///
      /// **Static authoritative data**, the `MoveSpeed` precedent exactly:
      /// set once from the authored scenario (`Deployment.FormationOffset`,
      /// resolved from `Scenario.validate`'s formation table; `None` by
      /// default) and never mutated during a run. Therefore **excluded**
      /// from `Canonical.encode`. A formation-driven behaviour difference
      /// still surfaces in the hash within one tick through the agent's
      /// `Destination`/`Position`.
      FormationOffset: Cell option }

/// A validated jammer (TASK-058, backlog B-016b; `Scenario.Jammers`): a
/// recipient within `Radius` Chebyshev cells of `Position` cannot receive
/// an order while the current tick lies in `[ActiveFromTick,
/// ActiveUntilTick]` (both inclusive) -- the "dynamic" part of dynamic
/// jamming, since a scenario can author it to turn on and off over a run,
/// even though nothing can destroy a jammer yet this task. Defined here
/// (not in `Scenario.fs`, which compiles later) so `WorldState.Jammers`
/// below can reference it directly.
type Jammer =
    { Position: Cell
      Radius: int
      ActiveFromTick: int64
      ActiveUntilTick: int64 }

/// Minimal authoritative world state: an integer tick, the logical grid
/// bounds, the authoritative terrain grid, the agents ordered by ascending
/// id, and the deterministic random stream. Fields are added only when an
/// implemented behaviour requires them (docs/04_SIMULATION_SPEC.md section
/// 10).
type WorldState =
    { Tick: int64
      Bounds: GridBounds
      /// The authoritative per-cell terrain grid (elevation, passability,
      /// movement cost, opacity, directional cover). Authored and queryable
      /// but not yet consumed: no tick phase reads it (backlog B-009 to
      /// B-011). It is immutable within a run at this stage and is
      /// deliberately excluded from `Canonical.encode` and the state hash
      /// (docs/04_SIMULATION_SPEC.md section 17; ADR-0002 amendment).
      Terrain: Terrain
      Agents: AgentState[]
      /// The friendly squad's shared tactical picture (TASK-026, backlog
      /// B-015; `docs/04` section 10, 12.4). Ascending by contact id. The
      /// Tactical-knowledge phase upserts every contact a friendly saw this
      /// tick, ages contacts unseen for `PerceptionConfig.StaleAfter` ticks,
      /// and removes contacts unseen for `PerceptionConfig.ExpireAfter` ticks.
      ///
      /// Unlike `Terrain` and `AgentState.Route` / `VisibleContacts`, this
      /// **is** in `Canonical.encode`: it carries per-tick memory
      /// (`LastSeenTick`, the decaying `Confidence`) that no other field can
      /// reproduce (see `Contact`). Adding it bumped `Canonical.FormatVersion`
      /// 2 -> 3. A hostile squad picture and enemy doctrine reacting to it are
      /// backlog B-022; per-agent private beliefs are `docs/05` section 17.
      TacticalKnowledge: Contact[]
      /// The Hostile side's own shared tactical picture (TASK-034, backlog
      /// B-022, partial; `docs/04` section 10, 12.4; `docs/05` section 12
      /// "observe and report"). Symmetric to `TacticalKnowledge`: ascending by
      /// contact id, built by the same Tactical-knowledge phase calling the
      /// side-agnostic `Perception.mergeKnowledge` a second time, filtered to
      /// every `Hostile` agent's `VisibleContacts` instead of every
      /// `Friendly`'s. `ContactObserved` already fires symmetrically for both
      /// sides (TASK-026); this store's own upserts, staleness, and expiry
      /// (emitting `ContactExpired`, the identical event shape) follow
      /// unchanged.
      ///
      /// **Genuine per-tick canonical state**, the `TacticalKnowledge`
      /// precedent exactly: `LastSeenTick` and the decaying `Confidence`
      /// cannot be recomputed from the current tick's positions. Adding it
      /// bumped `Canonical.FormatVersion` 6 -> 7. No Hostile agent reads it —
      /// enemy doctrine reacting to it (suppress likely routes, seek cover,
      /// scripted fallback) stays open on B-022; `Simulation.combat` already
      /// draws its candidates from `AgentState.VisibleContacts` only, a
      /// same-tick check strictly tighter than anything this stale-tolerant
      /// store could provide (TASK-034 Decision E).
      HostileTacticalKnowledge: Contact[]
      /// Authored resupply-cache cells (TASK-047, backlog B-030 proper;
      /// `Scenario.ResupplyAreas`). An agent standing on one of these cells
      /// is refilled to full ammunition by the State-consequences phase
      /// (`Ammo.resupply`). **Static authored data**, set once from the
      /// scenario and never mutated during a run — the `Terrain`/
      /// `CommunicationAvailable` precedent (ADR-0002 amendment): both runs
      /// load the identical cells at tick 0 and they cannot diverge, so this
      /// is **excluded** from `Canonical.encode`. A resupply-driven
      /// behaviour difference still surfaces in the hash within one tick
      /// through the resupplied agent's own `Ammo`.
      ResupplyAreas: Cell[]
      /// The authored command-origin cell (TASK-058, backlog B-016b;
      /// `Scenario.Headquarters`), or `None`. This is the opt-in gate for
      /// the whole range/delay/jamming/radio-destroyed feature set
      /// (`Communication.available`): when `None`, every agent's effective
      /// comms availability reduces to exactly the static
      /// `AgentState.CommunicationAvailable` check (TASK-027's own
      /// behaviour, unchanged), delivery stays same-tick, and Combat draws
      /// no radio-destroy roll. **Static authored data**, the
      /// `ResupplyAreas`/`Terrain` precedent — **excluded** from
      /// `Canonical.encode`.
      Headquarters: Cell option
      /// Authored jammers (TASK-058, backlog B-016b; `Scenario.Jammers`).
      /// Empty for every scenario that authors no jamming. **Static
      /// authored data**, the `ResupplyAreas`/`Terrain` precedent —
      /// **excluded** from `Canonical.encode`.
      Jammers: Jammer[]
      /// The authoritative deterministic random stream. It is threaded through
      /// every step and is part of the canonical state hash. No gameplay phase
      /// draws from it yet (TASK-003 wires the stream; gameplay draws arrive
      /// with later combat and appraisal tasks).
      Random: RandomState }

[<RequireQualifiedAccess>]
module Agent =

    /// The discipline of an agent from a construction path that authored none
    /// (`Agent.create`, `World.create`, `Setup.sixAgentWorld`). Kept as a
    /// literal here to avoid a module-ordering dependency on `Appraisal.fs`;
    /// `AppraisalConfig.DisciplineDefault` carries the same value and documents
    /// the scale. `World.ofScenario` overrides it with `Deployment.Discipline`.
    [<Literal>]
    let DisciplineDefault = 3

    /// Full health for a freshly created agent (TASK-045, backlog B-031).
    /// The `DisciplineDefault` precedent exactly: kept as a literal here to
    /// avoid a module-ordering dependency on `Casualty.fs` (which depends on
    /// `Domain.fs` for `VitalStatus`, not the other way around);
    /// `CasualtyConfig.MaxHealth` carries the same value and documents the
    /// scale.
    [<Literal>]
    let MaxHealth = 1000

    /// Rounds a freshly created agent's magazine holds (TASK-047, backlog
    /// B-030 proper). The `MaxHealth` precedent: kept as a literal here to
    /// avoid a module-ordering dependency on `Ammo.fs`; `AmmoConfig.
    /// MagazineSize` carries the same value.
    [<Literal>]
    let MagazineSize = 30

    /// Rounds a freshly created agent's reserve stock holds. The
    /// `MagazineSize` precedent; `AmmoConfig.ReserveStart` carries the same
    /// value.
    [<Literal>]
    let ReserveStart = 90

    /// The movement-speed reference (TASK-049, backlog B-058).
    /// `Simulation.navigationAndMovement` checks an edge complete via
    /// `(startProgress + Terrain.BaseMoveCost) * moveSpeed >= Terrain.moveCost
    /// next * MoveSpeedDefault` — cross-multiplication, not a shared rescale
    /// of the per-tick increment (which stays the universal
    /// `Terrain.BaseMoveCost` for every agent). An agent whose
    /// `AgentState.MoveSpeed` equals this constant therefore reproduces the
    /// pre-TASK-049 comparison exactly (multiplying both sides of the old
    /// inequality by the same constant does not change it) — every existing
    /// corpus/fixture entry's agents use this default and are byte-identical.
    /// A smaller `MoveSpeed` (e.g. half this value) genuinely needs
    /// proportionally more ticks to cross the same cell. The
    /// `DisciplineDefault`/`MaxHealth` precedent: kept as a literal here to
    /// avoid a module-ordering dependency on `Simulation.fs`.
    [<Literal>]
    let MoveSpeedDefault = 2

    /// Creates an agent at rest (no destination, no route, no progress, no
    /// visible contacts, no order, communication available, default
    /// discipline, unsuppressed, full health, full ammunition, default
    /// movement speed) at the given position. `World.ofScenario` overrides
    /// `CommunicationAvailable`, `Discipline`, and `MoveSpeed` from the
    /// authored deployment; every other construction path takes the
    /// defaults. `Suppression` has no authored override anywhere (the
    /// `Progress` precedent) — every agent always starts at `0`.
    /// `SuppressionBand` and `Stress` (TASK-033) follow the identical rule:
    /// `false` / `0` always. `Vitals`/`RecentlyWounded` (TASK-045) follow it
    /// too: every agent always starts `Alive MaxHealth` / unwounded, no
    /// authored override. `Ammo` (TASK-047) follows it as well: every agent
    /// always starts `Ready (MagazineSize, ReserveStart)`. `RadioDestroyed`/
    /// `PendingDelivery` (TASK-058) follow it too: every agent always starts
    /// with an intact radio and nothing in flight, no authored override.
    /// `FormationOffset` (TASK-059) follows the `MoveSpeed` rule instead:
    /// `World.ofScenario` overrides it too, default `None`.
    let create (id: AgentId) (side: Side) (position: Cell) : AgentState =
        { Id = id
          Side = side
          Position = position
          Progress = 0
          Destination = None
          Route = None
          VisibleContacts = [||]
          Order = None
          OrderQueue = []
          Disposition = None
          Discipline = DisciplineDefault
          CommunicationAvailable = true
          RadioDestroyed = false
          PendingDelivery = None
          Suppression = 0
          SuppressionBand = false
          Stress = 0
          Vitals = Alive MaxHealth
          RecentlyWounded = false
          Ammo = Ready(MagazineSize, ReserveStart)
          MoveSpeed = MoveSpeedDefault
          FormationOffset = None }
