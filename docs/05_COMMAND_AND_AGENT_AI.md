# Command and Agent AI Design

Status: reduced vertical-slice design after adversarial review  
Last revised: 2026-10-05 (rewritten as a current-state specification; per-task history is in the ledger)

## 1. Design objective

The agent system exists to create a legible command relationship, not to simulate human psychology comprehensively.

A player should be able to say:

> I understand why Mills would not cross that road. I suppressed the machine gun, changed the route, and now the order makes sense to him.

That is more important than producing surprising behaviour.

## 2. Initial architecture

```text
World facts
  -> observations
  -> squad tactical knowledge
  -> order appraisal
  -> commitment
  -> finite action executor
  -> ordered interrupts
  -> events and explanation
```

There is no general HTN, GOAP planner, blackboard architecture, actor per soldier, or broad utility AI in the vertical slice.

## 3. Perception

### Objective facts

The simulation knows all authoritative state. Agents do not.

### Current observations

An agent may observe:

- visible enemy at a cell;
- muzzle flash or shot origin;
- nearby impact or explosion;
- ally wounded or killed;
- blocked route;
- objective state;
- order or contact report received.

Each observation contains source, tick, location, and confidence where relevant.

### Squad tactical picture

The squad stores reported contacts:

- contact ID when identity is known;
- last known cell;
- last observed tick;
- confidence band;
- source and reporting agent;
- suspected threat class or field of fire.

The first implementation may share contacts instantly when communication is available. Private persistent beliefs are deferred.

An agent's current observations are `AgentState.VisibleContacts`: the
opposing-side agents within `PerceptionConfig.SightRange` Chebyshev cells and
in `Sight.visible` line of sight (TASK-026, backlog B-015; `docs/04` sections
12.3, 12.4). A non-`Alive` agent is neither observed nor an observer
(TASK-055, TASK-078). Perception is symmetric: a `Hostile` agent's
`VisibleContacts` is populated too, so combat can validate line of fire for
both sides.

The squad tactical picture is `WorldState.TacticalKnowledge`, one array of
`{ Contact; LastKnownCell; LastSeenTick; Confidence }`, shared instantly
across every `Friendly` agent (there is no `SquadStore`). Confidence drops a
band after `PerceptionConfig.StaleAfter` unseen ticks, and the contact is
removed, emitting `ContactExpired`, after `ExpireAfter`.

The Hostile side has an identical picture, `WorldState.HostileTacticalKnowledge`
(TASK-034, backlog B-022, partial): the same Tactical-knowledge phase calls
`Perception.mergeKnowledge` a second time, filtered to `Side = Hostile`, with
the same staleness and expiry rules. Source and reporting agent are not stored
for either picture (identity is always known). Enemy doctrine reacting to this
picture, and the "suspected threat class or field of fire" field, stay with
B-022. Private persistent beliefs and confidence divergence between squad
members stay deferred (section 17).

## 4. Orders

Orders express intent and leave limited tactical discretion.

`PlayerIntent` (`Domain.fs`) has the cases `MoveTo`, `Suppress`, `Hold`,
`Assault` and `Withdraw`, each taking a bare `Cell` except `Suppress`, which
takes an `AgentId`.

### Move

Move to a cell using the selected posture.

Postures may begin as:

- quick;
- cautious.

There is no separate movement posture in the simulation. An order carries a
`RiskTolerance` (`Cautious`, `Standard`, `Aggressive`) and an `Urgency`
(`Routine`, `Immediate`), which appraisal stage 4 reads (section 5).

### Hold

Occupy and defend an area. The executor may choose nearby cover.

`PlayerIntent.Hold of area: Cell` (TASK-047, backlog B-030). Stage 2 redirects
through `Appraisal.bestCoverNear`, which picks the passable cell with the
lowest summed threat pressure within `AppraisalConfig.HoldCoverSearchRadius`
Chebyshev cells of `area` (including `area` itself). It reuses the
`cellPressure` function that stage-3 route exposure sums. Ties are broken by
nearest to `area`, then ascending `(Y, X)`; with no known threat near `area`
it picks `area` itself. There is no `Commitment` case for Hold (section 9): an
accepted `Hold` writes a `Destination` exactly as `MoveTo` does, so the agent
is `Moving` while en route and falls to bare `Holding` on arrival,
indistinguishable from an idle agent once there.

### Suppress

Fire toward a known or suspected threat area to reduce enemy effectiveness and perceived route danger.

`PlayerIntent.Suppress of target: AgentId` (TASK-037, backlog B-030) covers
the "known threat, reduce perceived route danger" half. It names a specific
contact already in the issuing agent's own tactical knowledge. It is never a
bare area and never a "suspected", id-less target, because no suspected-threat
model exists. The `Suppressing` commitment holds position, and in the Combat
phase it restricts the shooter's candidates to the named contact (still
subject to that tick's `VisibleContacts`, weapon range and line of fire).
While the named contact's own `AgentState.SuppressionBand` is latched, its
contribution to other agents' stage-3 route exposure is zero (section 5).
"Reduce enemy effectiveness" beyond that, such as degraded accuracy or forced
cover-seeking, is not modelled by any system.

### Assault

Close with and secure a target area. This is deliberately more demanding than Move and receives stricter appraisal.

`PlayerIntent.Assault of target: Cell` (TASK-047, backlog B-030). The stricter
appraisal is a flat `AppraisalConfig.AssaultResolvePenalty` subtracted from
the stage-4 threshold, so a route a `MoveTo` order would accept can be refused
as an Assault. Stage 2 also requires ammunition (section 5). The executor is a
staged finite-state machine, `Commitment.AssaultStage` (section 10).

### Withdraw

Break contact and move toward a safer destination. It may receive priority under high suppression.

`PlayerIntent.Withdraw of target: Cell` (TASK-047, backlog B-030). "May receive
priority" is read narrowly as appraisal priority, not a new automatic
interrupt: a flat `AppraisalConfig.WithdrawResolveBonus` is added to the
stage-4 threshold, so an agent breaking contact is less likely to refuse the
very exposure it is retreating through. `Commitment.Withdrawing of
WithdrawCommitment` is a distinct case (unlike `Hold`) because the bonus makes
it a real behavioural difference, not just a label. There is no autonomous
start of a withdrawal under suppression without an order; that is
Hostile-doctrine-shaped work (B-022), out of scope for a player-issued order.

## 5. Order appraisal

Appraisal is staged, not one opaque weighted sum.

It is implemented by `Simulation.appraisal` (`docs/04` section 12.5) over the
`Appraisal` leaf module (TASK-028, backlog B-017). Stage 1 is guaranteed
upstream of that module, and stage 5 is not built.

### Stage 1: comprehension and authority

- Was the order received?
- Is the issuer authorised?
- Is the target and intent understood?

"Was the order received?" is an inspectable fact (TASK-027, backlog B-016).
The Communication phase (`docs/04` section 12.2) delivers an accepted order to
a recipient that can receive it and emits `OrderUndelivered`, with a
`DeliveryFailure` reason, for one that cannot: `UnableToCommunicate` when
`AgentState.CommunicationAvailable` is `false`, and `OutOfRange`, `Jammed` or
`RadioDestroyed` (TASK-058, backlog B-016b) when the scenario authors a
`Headquarters`. An undelivered order never reaches appraisal, so these are not
`DecisionReason`s. "Is the issuer authorised?" is the friendly/hostile-side
check in command intake (TASK-020), which rejects an order addressed to a
`Hostile` agent (`UnauthorisedRecipient`). Intake also rejects an out-of-bounds
target (`TargetOutOfBounds`) for every `Cell`-targeted intent (`MoveTo`, `Hold`,
`Assault`, `Withdraw`); a `Suppress` target is an `AgentId`, not a cell, and
whether the recipient knows that contact is appraisal's stage-2 check, never
authoritative hostile state at intake (risk R-023). A commander identity model
is deferred.

### Stage 2: physical feasibility

- Does a known traversable route exist?
- Is the agent alive, conscious, and mobile?
- Does the task require a capability the agent lacks?
- Is required ammunition or equipment available?

A failure here is normally a hard refusal or inability, not a morale check.

A non-`Alive` agent is `Unable(CriticallyWounded)` whatever the order asks
(TASK-045, backlog B-031), checked first, so stages 3 and 4 are reached only
by `Alive` agents. For `Suppress` and `Assault` only, an agent whose
`AgentState.Ammo` is entirely empty (`Ready(0, 0)`) is
`Unable(InsufficientAmmunition)` (TASK-047, backlog B-030): these are the two
intents that plan to initiate fire, while an unarmed agent can still walk,
hold ground or retreat. A partial or mid-reload magazine appraises normally;
running dry mid-engagement is an emergent outcome, not a blocking one. For
`MoveTo`, `Hold`, `Withdraw` and `Assault`, `Pathfinding.findWithin` must find
a route, otherwise the outcome is `Unable(NoKnownRoute)`; an order to the cell
the agent already stands on is `Accepted` without a route. After the
ammunition check, a `Suppress` order is `Unable(TargetNotKnown)` unless the
named contact is in `WorldState.TacticalKnowledge` (TASK-037); this is its only
remaining stage, since it has no route and never reaches stages 3 and 4. "A
capability the agent lacks" is unrealised: no capability model exists.

### Stage 3: tactical viability

Estimate from known information:

- route exposure;
- known enemy fire lanes;
- available cover;
- support and proximity of allies;
- current suppression;
- wound severity;
- target threat;
- whether suppression or another prerequisite is active.

Route exposure is summed over the route cells and the known threats in
`WorldState.TacticalKnowledge`, never authoritative hostile state (risk
R-023). A threat puts pressure on a cell only if the cell is within
`AppraisalConfig.ThreatEngagementRange` Chebyshev cells of the threat's
last-known cell and in `Sight.visible` line of sight from it. The pressure is
`max 0 (ExposedCellWeight - cover * CoverMitigationPerLevel)`, where `cover` is
the directional `Terrain.cover` on the edge the fire arrives from
(`Appraisal.attackDirection`: the dominant axis of the offset wins, and the X
axis breaks a tie). A threat whose own `AgentState.SuppressionBand` is latched
contributes nothing, whatever suppressed it, whether an ordered `Suppress` or
incidental automatic engagement (TASK-037, backlog B-030). The threat named in
a `RouteTooExposed` reason is the highest-contributing one, ties broken by
ascending id. Exposure, cover and suppression are the realised stage-3 terms.
Known fire lanes and ally support are not built and no backlog item owns them,
and wound severity enters at stage 4.

### Stage 4: resolve

Compare tactical pressure with a bounded resolve threshold derived from:

- discipline;
- trust in the issuer;
- current stress;
- current suppression;
- urgency and risk tolerance encoded by the order.

Personality modifies a decision near a threshold. It does not override physical impossibility.

The threshold is `AppraisalConfig.BaseResolve` plus a per-point
`Discipline` weight, plus a `RiskTolerance` modifier and an `Urgency`
modifier, minus a continuous stress drag (`AgentState.Stress /
StressDivisor`), a discrete penalty while `AgentState.SuppressionBand` is
latched (`SuppressionBandPenalty`), and a continuous wound drag for an `Alive`
agent, `(Agent.MaxHealth - health) / WoundDivisor` (TASK-033, backlog B-021;
TASK-045). An `Assault` subtracts `AssaultResolvePenalty` and a `Withdraw` adds
`WithdrawResolveBonus`. The result is floored at `0`, so a completely
unexposed route is never refused for stress, suppression or wounds alone:
those three terms only make an already-exposed route more likely to be
refused.
Exposure at or below the threshold is `Accepted`; above it the outcome is
`Refused(RouteTooExposed topThreat)`. Trust is not a stage-4 term (section 8
leaves it minimal for the vertical slice).

### Stage 5: safer adaptation

Before refusal, attempt a small set of permitted adaptations:

- use a less exposed route;
- move to nearby cover first;
- wait for active suppression;
- reduce speed or change posture;
- maintain greater distance from a threat.

An adaptation must preserve the commander's broad intent. Otherwise it becomes a refusal with a suggested correction.

Stage 5 is not built and no backlog item owns it: there is no `Adapted`
outcome and no route recomputation.

## 6. Outcomes

```fsharp
type OrderDisposition =
    | Accepted
    | Adapted of TacticalAdaptation
    | Delayed of ResumeCondition
    | Refused
    | Unable
```

Every non-trivial outcome includes one primary reason and optional supporting reasons.

The implemented type (TASK-028, backlog B-017) is the subset
`Accepted | Refused of primary * supporting | Unable of primary * supporting`.
`Refused` and `Unable` carry their `DecisionReason`s by construction, so the
"one primary reason" rule holds without a side check. `Adapted` (stage 5) and
`Delayed` (a `ResumeCondition` mechanism) are not built: neither has a case,
`TacticalAdaptation` and `ResumeCondition` do not exist, and no open backlog
row carries either (B-018 and B-021 closed without them). Every appraisal, including `Accepted`, emits
one `OrderAppraised` event carrying the `OrderDisposition` (`docs/04` section
14).

### Accepted

The agent commits to the order as issued.

### Adapted

The agent changes a permitted tactical detail and reports the change.

### Delayed

The agent waits for an explicit condition, such as suppression of a threat or clearance of a blocked route. A delay must not become an indefinite hidden state.

### Refused

The agent believes the order is tactically unacceptable given current knowledge and resolve.

### Unable

The agent cannot perform the order because of physical state, missing capability, missing route, or invalid target.

`Unable` is not cowardice and should be communicated differently.

## 7. Structured reasons

Initial reason vocabulary:

```fsharp
type DecisionReason =
    | NoKnownRoute
    | RouteBlocked of Cell
    | RouteTooExposed of threat: ContactId option
    | UnsupportedAssault
    | HeavySuppression
    | CriticallyWounded
    | MissingCapability of Capability
    | InsufficientAmmunition
    | TargetNotKnown
    | UnableToCommunicate
    | IssuerNotRecognised
    | ImmediateThreat of ContactId
```

Do not add prose-only reasons. UI text is derived from structured values.

The implemented subset (`Domain.fs`) is `NoKnownRoute | RouteTooExposed of
threat: AgentId option` (TASK-028, backlog B-017), `TargetNotKnown` (TASK-037,
backlog B-030: a `Suppress` order naming a contact absent from the issuing
agent's own tactical knowledge), `CriticallyWounded` (TASK-045, backlog B-031)
and `InsufficientAmmunition` (TASK-047, backlog B-030: a `Suppress` or
`Assault` order, and only those, from an agent whose `AgentState.Ammo` is
entirely empty). The doc's `ContactId` is `AgentId` in the code (there is no
`ContactId` type). `UnableToCommunicate` is a `DeliveryFailure` case (section
5, stage 1), not a `DecisionReason`, because an undelivered order never
reaches appraisal. The rest (`RouteBlocked`, `HeavySuppression`,
`MissingCapability`, `IssuerNotRecognised`, `ImmediateThreat`,
`UnsupportedAssault`) arrive with the systems that can trigger them, namely the
remaining reappraisal triggers, a capability model, and a commander-identity
and interrupt-priority model, none of which exist, rather than as speculative
type machinery (`AGENTS.md`).

## 8. Minimal psychological model

### Discipline

Stable trait influencing willingness to maintain a valid commitment under pressure.

`AgentState.Discipline` is a non-negative integer set once from
`Deployment.Discipline` (default `AppraisalConfig.DisciplineDefault`) and read
only by the stage-4 resolve threshold (TASK-028, backlog B-017). It is static
authored data; dynamic discipline and dynamic trust are not built (B-021
records dynamic trust as unassigned future work and keeps `Discipline` static).

### Trust

Slow-changing confidence in the current commander's judgement. The vertical slice may initialise trust and leave dynamic trust changes minimal.

There is no trust field, and trust is not an appraisal input.

### Stress

Accumulates through nearby casualties, wounds, isolation, explosions, and threat. Decays when safe.

Of these five sources only "threat" has a system behind it (TASK-033, backlog
B-021). `AgentState.Stress` is an integer on the `0..1000` scale (the
`Suppression` precedent). In the State consequences phase (`docs/04` section
12.9) it rises by `StressConfig.GainPerTick` every tick the agent has an
opposing-side agent in `AgentState.VisibleContacts`, then always decays by
`StressConfig.DecayPerTick`, floored at `0`. Stage 4 reads it as a continuous
drag (`AppraisalConfig.StressDivisor`), floored overall at `0` so a completely
unexposed route is never refused for stress alone. Stress from nearby
casualties, wounds, explosions and isolation is not built; a wounded agent's
own health loss enters stage 4 directly instead.

### Suppression

Immediate effect of hostile fire and impacts. It reduces action effectiveness, raises assault pressure, and may trigger taking cover.

Wounds are represented separately as physical state.

`AgentState.Suppression` is an integer on the `0..1000` scale (the
`Contact.Confidence` precedent; TASK-032, backlog B-020). The Combat phase
(`docs/04` section 12.8) raises it on every qualifying shot, independent of a
hit, mitigated by the same directional `Terrain.cover` geometry Combat uses for
hit chance. The State consequences phase (section 12.9) decays it every tick,
unconditionally, floored at `0`. This is the "immediate effect of hostile fire"
value only: "reduces action effectiveness" and "may trigger taking cover" are
not realised by any system.

Its consumer is a hysteresis latch, `AgentState.SuppressionBand` (TASK-033,
backlog B-021): `true` once `Suppression >= AppraisalConfig.SuppressionBandEnter`,
back to `false` at `<= SuppressionBandExit`. The latch drives a discrete
stage-4 resolve-threshold penalty ("raises assault pressure", read narrowly as
appraisal resolve, not movement or executor speed), the suppression-band
reappraisal trigger (section 14) whenever it flips, and the zeroing of a
latched threat's contribution to other agents' route exposure (section 5,
stage 3).

Wounds are `AgentState.Vitals` (TASK-045, backlog B-031): `Alive health`
becomes `Incapacitated bleedOutRemaining` when a qualifying hit takes health to
zero or below, never straight to `Dead`, and `Dead` when the countdown ends.
Only a hit wounds, not a miss. There is no rescue, and health never
regenerates. A wound enters appraisal as the stage-4 wound term, as
`Unable(CriticallyWounded)` once the agent is not `Alive`, and as the wounded
reappraisal trigger (section 14).

## 9. Commitment

Once an order is accepted, the agent enters a commitment state:

```fsharp
type Commitment =
    | Moving of MoveCommitment
    | Holding of HoldCommitment
    | Suppressing of SuppressCommitment
    | Assaulting of AssaultCommitment
    | Withdrawing of WithdrawCommitment
```

The agent does not reselect its high-level goal every tick. It continues until:

- completed;
- superseded by a newer order;
- invalidated by a material world change;
- interrupted by a higher-priority survival event;
- delayed or refused after explicit reappraisal.

This prevents oscillation.

The implemented type (`CommandoWar.Sim`; TASK-030, backlog B-018, extended by
TASK-037 and TASK-047, backlog B-030) is:

```fsharp
type Commitment =
    | Holding
    | Moving of MoveCommitment
    | Suppressing of SuppressCommitment
    | Withdrawing of WithdrawCommitment
    | Assaulting of AssaultCommitment
```

`MoveCommitment` and `WithdrawCommitment` are `{ Command: CommandId; Target:
Cell }`, `SuppressCommitment` is `{ Command: CommandId; Target: AgentId }`
(the shape this section names), and `AssaultCommitment` is `{ Command:
CommandId; Target: Cell; Stage: AssaultStage }`, where `Stage` is a pure
per-tick derivation (section 10) rather than a second stored field. `Hold` has
no `HoldCommitment` payload case, unlike the sketch above: an accepted `Hold`
writes a `Destination` exactly as `MoveTo` does, so it already produces
`Moving` while en route and falls to bare `Holding` on arrival. Nothing
behaviourally distinguishes an ordered hold from an idle agent once arrived,
so a payload case would carry no information a diagnostic overlay cannot
already read from `Order` and `Disposition` (`AGENTS.md` "do not build
speculative type machinery"). A `Suppressing` commitment has no completed
state of its own; it ends only by supersession, which is the list above minus
"completed".

`Commitment` is not a stored `AgentState` field. It is a pure derived value,
`Commitment.ofAgent : threats: Contact[] -> suppressedThreats: AgentId[] ->
position: Cell -> ReceivedOrder option -> OrderDisposition option -> Cell
option -> Commitment`, recoverable at every tick from the already-canonical
`Order`, `Disposition` and `Destination` plus already-canonical world context
(`WorldState.TacticalKnowledge` and `AgentState.SuppressionBand`, needed only
for `Assaulting`'s `Stage`). This is the `AgentState.Route` precedent
(`docs/04` section 17): storing it would duplicate state that can drift from
its source fields. `Holding` is the result when there is no order, the order
is `Refused` or `Unable`, or an `Accepted` order is already fulfilled.

"Continues until completed" and "superseded by a newer order" are realised
(`CommitmentCompleted` and `CommitmentEstablished` events, `docs/04` section
12.6). Reappraisal on a section 14 trigger can turn an in-progress order into
`Refused` or `Unable`, which clears its `Destination` and so ends the
commitment (`Holding`); this is the realised form of "invalidated by a
material world change" and "refused after explicit reappraisal". `Delayed` is
not built (section 6). "Interrupted by a higher-priority survival event" is
covered in section 11. An `Assault`'s `AwaitingSupport` stage (section 10)
freezes the executor without any interrupt-priority mechanism, since it is the
order's own stage rather than an external event pre-empting it.

## 10. Finite action executor

Each commitment expands into a small finite state machine.

Example assault:

```text
Acquire approach route
  -> move to assault start
  -> wait for required support, if any
  -> cross danger area
  -> enter target area
  -> clear immediate threat
  -> report complete
```

Do not encode the whole game in one behaviour tree. Typed states and transitions are easier to test and explain.

The executor for `Move` is correspondingly thin (TASK-030, backlog B-018),
since `Simulation.navigationAndMovement` (`docs/04` section 12.7) already owns
the physical stepping. `Simulation.commitmentAndLocalAction` establishes (a
fresh `Accepted` order becomes `Moving`, and `CommitmentEstablished` is
emitted), continues (unchanged, no event), and completes (the target is
reached and `CommitmentCompleted` is emitted). On completion a queued order's
head is promoted into `Order` with `Disposition = None`, to be judged by the
next tick's Appraisal (TASK-044, backlog B-051). `Hold` and `Withdraw` use the
same shape, `Hold` toward its `bestCoverNear` cell. A plain move has no
intermediate states: there is no "wait for support" or "cross danger area"
concept for it.

`Assault` has a leaner four-state cut of the example above
(`Commitment.AssaultStage`; TASK-047, backlog B-030), because several of its
states collapse onto systems that already exist. The stage is a pure per-tick
derivation from the agent's position, the target, `WorldState.TacticalKnowledge`
and the `SuppressionBand` latch:

- "acquire approach route" and "move to assault start" are `ApproachingStart`:
  the agent is more than `AppraisalConfig.AssaultStartRange` Chebyshev cells
  from the target, under ordinary `Pathfinding`-driven Navigation;
- "wait for required support, if any" is `AwaitingSupport`: the agent is within
  `AssaultStartRange` and a known threat contact within
  `AppraisalConfig.ThreatEngagementRange` of the target is not
  `SuppressionBand`-latched (the identical latch `Suppress` reads). The
  executor freezes `Destination` to `None` for as long as this holds. There is
  no timeout: the player must suppress the threat, redirect, or accept the
  stall;
- "cross danger area" is `Advancing`: no unsuppressed threat blocks, and
  Navigation resumes. It needs no special handling, because the symmetric
  `Combat` phase engages any visible hostile along the way as it does for any
  other commitment;
- "enter target area" and "clear immediate threat" are `ClearingThreat`: the
  agent is at the target and a known unsuppressed threat contact lies within
  `AppraisalConfig.AssaultClearRadius` of it. The agent holds while `Combat`
  fires;
- "report complete" is the existing `CommitmentCompleted` event, emitted once
  the agent is at the target and no such threat remains. There is no new event
  type.

## 11. Interrupts

Initial priority order:

1. Dead or incapacitated.
2. Immediate explosive danger.
3. Point-blank hostile threat.
4. Heavy suppression requiring cover.
5. Route invalidated.
6. New higher-priority command.
7. Normal commitment execution.

An interrupt emits an event and records whether the original commitment can resume.

Current coverage of the table:

- Priority 1 has an effect but no record of whether the commitment can resume.
  A non-`Alive` agent does not move, fire or start new actions (`docs/04`
  section 20). The transition is reported by `AgentIncapacitated` and
  `AgentDied` (TASK-045, backlog B-031). The wounded reappraisal trigger
  (section 14) re-judges the agent's unfulfilled order as
  `Unable(CriticallyWounded)` on the following tick, which clears its
  `Destination`; the `Order` itself stays in place. An already-fulfilled order
  is not re-judged.
- Priorities 2 to 4 have no live signal. No explosion model exists, and
  nothing interrupts a commitment on point-blank contact or heavy suppression:
  Combat engages automatically for every commitment, and suppression reaches a
  commitment only through appraisal (the `SuppressionBand` trigger, section
  14).
- Priority 5 has no interrupt of its own. Under static terrain an
  Appraisal-`Accepted` route cannot later become unreachable (`docs/04` section
  12.5). The live-obstruction case is handled inside Navigation: after
  `StallAbandonTicks` (40) consecutive frozen ticks against the same route the
  agent abandons its destination (`MovementAbandoned`, counted in
  `AgentState.StalledTicks`; TASK-065, backlog B-065), and an agent obstructed
  by a parked agent reroutes around it when an alternate route exists
  (`MovementRerouted`, which keeps the destination; TASK-070, backlog B-069).
  Abandonment clears `Destination` and `Route`. Neither records whether the
  commitment can resume.
- Priority 6 is live (TASK-030, backlog B-018). A superseding order's
  `CommitmentEstablished` event is the complete trace: since `Commitment` is
  derived, not stored (section 9), there is nothing separate to report about
  the superseded commitment ending, and no resume question arises, because a
  superseded commitment cannot resume and a new order always takes precedence.
- Priority 7 is unchanged Navigation behaviour.

## 12. Enemy AI

Enemy agents use the same perception and combat rules where practical, but their command model can be simpler.

Initial enemy doctrine:

- hold assigned area;
- observe and report;
- engage visible targets;
- suppress likely routes;
- seek adjacent cover under pressure;
- fall back only under a scenario-defined condition.

Do not build a symmetric enemy commander planner before the player command loop works.

Status against that list:

- "Engage visible targets": `Simulation.combat` is symmetric by construction
  (TASK-031, backlog B-019). A hostile agent engages a visible friendly exactly
  as a friendly engages a visible hostile, with the same `Combat.hitChance`
  formula and weapon range. This is the mechanic doctrine will gate; doctrine
  choosing when or whether to engage is B-022. Targeting is bounded to observed
  positions: candidates are drawn from the shooter's own same-tick
  `VisibleContacts`, never from the stale-tolerant tactical-knowledge store, so
  a Hostile agent never fires on a friendly position it has since lost sight of
  (TASK-034; `docs/09` section 8).
- "Observe and report": `WorldState.HostileTacticalKnowledge`, symmetric to the
  friendly squad's picture (section 3; TASK-034, backlog B-022, partial).
- "Hold assigned area" is true by omission: an unordered agent never moves.
- "Suppress likely routes", "seek adjacent cover under pressure", and "fall
  back only under a scenario-defined condition" are not built. They are the
  doctrine that decides to move or fire a Hostile agent, all Hostile-side (the
  enemy proactively choosing to suppress or fall back without a player order),
  as distinct from the player-issued `Suppress` order (section 4). TASK-037
  explicitly descoped them (2026-09-16). They would need a
  `Suppress`-order-equivalent for Hostile AI, the first reactive-movement
  decision for an unordered agent (no design exists yet), and authored fallback
  conditions with `Withdraw` semantics, respectively. They stay B-022.

## 13. Explanation surface

Three layers serve different users.

### Immediate feedback

A short bark or icon:

```text
DELAYED: exposed route
```

### Tactical detail

A compact panel:

```text
Order: Assault bridge control room
Disposition: Delayed
Primary reason: Machine-gun fire covers selected route
Resume condition: Threat suppressed
Suggested response: Suppress MG-1 or choose west drainage route
```

### Developer trace

A structured record containing:

- order and appraisal version;
- observed facts used;
- hard constraints checked;
- tactical pressure terms;
- threshold terms;
- selected outcome and adaptation;
- random draws, if any.

The player-facing explanation must be derivable from the actual decision data, not generated independently.

The `OrderAppraised` event carries the full `OrderDisposition` (outcome and
structured reasons), and the `Diagnostics` `OrderAppraisal` overlay carries the
outcome plus the exposed route cells the stage-3 sum found (TASK-028, backlog
B-017). That is enough for the headless developer overlay to explain any
appraisal (`docs/07` section 9 criterion 11). The Godot client derives its
disposition and reason text from the same structured values (`RenderShared.fs`;
TASK-042). A separate persisted trace record (appraisal version, every
pressure and threshold term) is a later refinement.

## 14. Reappraisal triggers

Reappraise only when:

- a new order is received;
- a relevant known threat appears or disappears;
- route exposure crosses a defined band;
- suppression crosses a defined band;
- the agent is wounded;
- required support begins or ends;
- the route becomes blocked;
- communication or leadership materially changes.

An already-appraised order is not re-judged, and emits no event, unless a
trigger fires. The Communication phase resets `AgentState.Disposition` to
`None` when it writes a fresh `AgentState.Order`, and the Appraisal phase
judges exactly the agents whose `Disposition` is `None`. Every other trigger
below works the same way, by resetting an already-appraised order's
`Disposition` inside the Appraisal phase (`docs/04` section 12.5), and each
excludes a fulfilled order, because `commitmentAndLocalAction` needs to see it
unchanged to recognise completion. The realised triggers are:

- **A new order is received** (TASK-028, backlog B-017).
- **Knowledge-change** (TASK-033, backlog B-021): any `ContactObserved` or
  `ContactExpired` event emitted earlier the same tick (Perception and
  Tactical-knowledge both run before Appraisal) resets every agent's
  already-appraised order. The scope is global rather than filtered to "was
  this agent's own route affected" (the R-023 "same observation contract"
  precedent: a broad, simple trigger over a precise, expensive one).
- **Suppression-band** (TASK-033): the agent's own `AgentState.SuppressionBand`
  (the hysteresis latch over `AgentState.Suppression`, section 8) flips this
  tick.
- **Threat-suppression-change** (TASK-037, backlog B-030): the identical
  suppression-band flip, but checked across every agent rather than only the
  appraising agent's own state. The scope is global, like knowledge-change: a
  band flip on any agent re-judges every already-appraised, unfulfilled order,
  not only one that names that agent. The case it exists for is a `Suppress`
  order (or incidental automatic engagement) driving a different agent's known
  threat into its `SuppressionBand`, which re-judges a `Refused` or `Unable`
  order that names it. Stage 3's exposure zeroing is what changes the outcome;
  this trigger is what notices to re-check.
- **Wounded** (TASK-045, backlog B-031): `AgentState.RecentlyWounded` is a
  one-shot flag, true for one tick after a qualifying hit reduces the agent's
  health. It exists because Combat runs after Appraisal within a tick, so a
  wound taken this tick cannot be seen by this tick's Appraisal. The following
  tick's Appraisal consumes it whether or not it triggered a fresh appraisal.

Not built:

- **Exposure-band** needs per-tick route-exposure tracking for every agent with
  a live order, a materially larger cut than the realised triggers.
- **Support** (required support beginning or ending) and **communication or
  leadership change** have no trigger. `AwaitingSupport` (section 10) is an
  `Assault` stage, not a reappraisal trigger, and `LeadershipTransferred` is an
  event only.
- **The route becomes blocked** cannot fire for an appraisal-`Accepted` order
  under static terrain (its route was verified at stage 2), so it waits for
  future persistent-obstruction handling (see section 11, priority 5).

This improves stability and makes decisions easier to trace.

## 15. Tuning rules

- Prefer a small number of integer thresholds.
- Record every threshold in one configuration structure.
- Do not tune by adding hidden exceptions for one scenario.
- Keep primary reasons stable under small irrelevant state changes.
- Use hysteresis when entering and leaving panic, suppression, or delay states.
- Treat random variation as a last resort and bound it so identical tactical situations remain broadly predictable.

`AppraisalConfig` (`src/CommandoWar.Sim/Appraisal.fs`) is the one
configuration structure for appraisal: a `[<RequireQualifiedAccess>]` module of
`[<Literal>]` integers (engagement range, per-cell exposure weight, cover
mitigation, base resolve, the Discipline / RiskTolerance / Urgency modifiers,
the stress, suppression-band and wound terms, the Assault, Withdraw and Hold
constants, and the formation-slot search radius), the `PerceptionConfig`
precedent (TASK-028, backlog B-017). All appraisal arithmetic is integer, and
appraisal draws no randomness. Other mechanisms keep their own config modules of
the same shape (`PerceptionConfig`, `CombatConfig`, `StressConfig`,
`SuppressionConfig`, `CasualtyConfig`, `AmmoConfig`, `CommsConfig`).

Hysteresis is applied to the suppression band, not to the exposure band (which
is not built, section 14). `AppraisalConfig.SuppressionBandEnter` (500) and
`.SuppressionBandExit` (300) are the two-threshold latch. The gap between them
(200) exceeds one `SuppressionConfig.DecayPerTick` (50), so a value sitting
right at one boundary cannot cross it, and flip the latch, in a single tick of
decay alone (TASK-033, backlog B-021).

## 16. Vertical-slice scenarios for tests

### Exposed road

An assault route crosses a known machine-gun lane. A low-discipline, suppressed soldier delays. After suppression begins, the order becomes acceptable.

Partially realised. The `exposed-approach` corpus entry and the
`SimulationTests` "exposed route ... Refused for a low-discipline agent" fact
deliver the refusal half: a low-`Discipline` agent is `Refused
RouteTooExposed` and a high-`Discipline` one is `Accepted` on the same order
(the G3 divergence, `docs/07` section 9 criterion 2; TASK-028, backlog B-017).
"Suppression makes it acceptable" is realised by a `Suppress` order that zeroes
a threat's contribution to stage-3 exposure, so an ally suppressing the machine
gun reduces the route's exposure, proven end to end by the
`suppress-relieves-exposure` corpus entry (TASK-037). That is the opposite
direction from `SuppressionBand`, which only makes an already-suppressed
soldier's own resolve threshold harder to clear. `Delayed` (the soldier waits,
rather than simply becoming `Accepted` once the threat is suppressed) still
needs the `Delayed` disposition (section 6), which no task has built.

### Covered alternative

The same target has a longer route behind walls. A cautious adaptation chooses it.

Not realised: it needs stage-5 adaptation (section 5).

### Unknown threat

The squad has not observed the machine gun. An agent accepts based on current knowledge, then reacts when fired upon. The explanation distinguishes bad information from arbitrary behaviour.

### Physical inability

A critically wounded agent reports unable rather than refused.

Realised as `Unable(CriticallyWounded)` (TASK-045, backlog B-031).

### Lost communication

An order is not delivered. The UI reports communication failure rather than pretending the recipient disobeyed.

## 17. Deferred systems

- dynamic trust based on commander history;
- cohesion as a separate squad variable;
- fatigue;
- relationship graphs;
- leadership succession beyond a simple replacement rule;
- specialised skills beyond one or two mission capabilities;
- general squad plan decomposition;
- utility selection across many actions;
- learning agents;
- natural-language orders;
- runtime LLM explanations.
