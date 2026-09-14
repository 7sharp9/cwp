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
/// compiles before `Commands.fs`). Only movement is needed for the vertical
/// slice; `Hold`, `Suppress`, `Assault` and `Withdraw`
/// (`docs/04_SIMULATION_SPEC.md` section 13) are added by later tasks (B-030).
type PlayerIntent = MoveTo of target: Cell

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
/// from structured values"). TASK-028 realises only the two reasons its staged
/// checks can produce; the rest of the `docs/05` vocabulary
/// (`RouteBlocked`, `HeavySuppression`, `CriticallyWounded`, `MissingCapability`,
/// `TargetNotKnown`, ...) arrives with the systems that can trigger it
/// (B-019 / B-020 / B-021). The doc's `ContactId` is `AgentId` in the code.
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
      Suppression: int }

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

    /// Creates an agent at rest (no destination, no route, no progress, no
    /// visible contacts, no order, communication available, default
    /// discipline, unsuppressed) at the given position. `World.ofScenario`
    /// overrides `CommunicationAvailable` and `Discipline` from the authored
    /// deployment; every other construction path takes the defaults.
    /// `Suppression` has no authored override anywhere (the `Progress`
    /// precedent) — every agent always starts at `0`.
    let create (id: AgentId) (side: Side) (position: Cell) : AgentState =
        { Id = id
          Side = side
          Position = position
          Progress = 0
          Destination = None
          Route = None
          VisibleContacts = [||]
          Order = None
          Disposition = None
          Discipline = DisciplineDefault
          CommunicationAvailable = true
          Suppression = 0 }
