# Command and Agent AI Design

Status: reduced vertical-slice design after adversarial review  
Last revised: 2026-09-02

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

Realised by TASK-026 (backlog B-015): `Simulation.perception` and
`Simulation.tacticalKnowledge` (`docs/04` sections 12.3, 12.4). An agent's
current observations are `AgentState.VisibleContacts` (opposing agents within
`PerceptionConfig.SightRange` and in `Sight.visible` line of sight); the squad
tactical picture is `WorldState.TacticalKnowledge` — one array of
`{ Contact; LastKnownCell; LastSeenTick; Confidence }`, shared instantly
across every `Friendly` agent (there is no `SquadStore`), with confidence
dropping a band after `StaleAfter` unseen ticks and the contact removed after
`ExpireAfter`. Perception is symmetric (a `Hostile` agent's
`VisibleContacts` is populated too, so combat can validate line of fire for
both sides), but the **shared picture is the friendly squad's only**: a
hostile squad picture, enemy doctrine, and "suspected threat class or field of
fire" are B-022. Source and reporting agent are not yet stored (identity is
always known at this stage). Private persistent beliefs and confidence
divergence between squad members stay deferred (section 17).

## 4. Orders

Orders express intent and leave limited tactical discretion.

### Move

Move to a cell using the selected posture.

Postures may begin as:

- quick;
- cautious.

### Hold

Occupy and defend an area. The executor may choose nearby cover.

### Suppress

Fire toward a known or suspected threat area to reduce enemy effectiveness and perceived route danger.

### Assault

Close with and secure a target area. This is deliberately more demanding than Move and receives stricter appraisal.

### Withdraw

Break contact and move toward a safer destination. It may receive priority under high suppression.

## 5. Order appraisal

Appraisal is staged, not one opaque weighted sum.

Realised by TASK-028 (backlog B-017): `Simulation.appraisal` (`docs/04` section
12.5) over the `Appraisal` leaf module. Stage 1 is guaranteed upstream (see
below). Stage 2 is `Pathfinding.findWithin`. Stage 3 is route exposure to the
*known* threats in `WorldState.TacticalKnowledge` — engagement range, line of
sight from the threat's last-known cell, and directional `Terrain.cover` — the
only stage-3 term realised; fire lanes, ally support, suppression, and wounds
are B-019 / B-020 / B-021. Stage 4 compares that exposure to a threshold from
`AgentState.Discipline` and the order's `RiskTolerance` / `Urgency` only (trust,
stress, suppression are B-021). Stage 5 is deferred (B-018): no `Adapted`
outcome. Every threshold is an integer literal in one `AppraisalConfig` module
(section 15).

### Stage 1: comprehension and authority

- Was the order received?
- Is the issuer authorised?
- Is the target and intent understood?

Realised so far: "Was the order received?" is now a real, inspectable fact
(TASK-027, backlog B-016). The Communication phase (`docs/04` section 12.2)
delivers an accepted order to a recipient with
`AgentState.CommunicationAvailable = true` and emits `OrderUndelivered`
(reason `UnableToCommunicate`) for one that cannot be reached — the
`DecisionReason.UnableToCommunicate` an appraisal refusal / `Unable` outcome
(backlog B-017) carries through unchanged. "Is the issuer authorised?" is the
friendly/hostile-side check in command intake (TASK-020); a commander identity
model is deferred.

### Stage 2: physical feasibility

- Does a known traversable route exist?
- Is the agent alive, conscious, and mobile?
- Does the task require a capability the agent lacks?
- Is required ammunition or equipment available?

A failure here is normally a hard refusal or inability, not a morale check.

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

### Stage 4: resolve

Compare tactical pressure with a bounded resolve threshold derived from:

- discipline;
- trust in the issuer;
- current stress;
- current suppression;
- urgency and risk tolerance encoded by the order.

Personality modifies a decision near a threshold. It does not override physical impossibility.

### Stage 5: safer adaptation

Before refusal, attempt a small set of permitted adaptations:

- use a less exposed route;
- move to nearby cover first;
- wait for active suppression;
- reduce speed or change posture;
- maintain greater distance from a threat.

An adaptation must preserve the commander's broad intent. Otherwise it becomes a refusal with a suggested correction.

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

Realised by TASK-028 (backlog B-017) as the subset
`Accepted | Refused of primary * supporting | Unable of primary * supporting`
in `CommandoWar.Sim` — `Refused` and `Unable` carry their `DecisionReason`s **by
construction**, so the "one primary reason" rule holds without a side check.
`Adapted` (stage 5) is B-018 and `Delayed` (a `ResumeCondition` mechanism) is
B-021 — neither has a case yet, and `TacticalAdaptation` / `ResumeCondition` do
not exist. Every appraisal, including `Accepted`, emits one `OrderAppraised`
event carrying the `OrderDisposition` (`docs/04` section 14).

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

Realised by TASK-028 (backlog B-017) as the subset
`NoKnownRoute | RouteTooExposed of threat: AgentId option` — the only two the
staged checks in scope can produce. The doc's `ContactId` is `AgentId` in the
code (there is no `ContactId` type). `UnableToCommunicate` stays a
`DeliveryFailure` case (TASK-027) — an undelivered order never reaches
appraisal. The rest (`RouteBlocked`, `HeavySuppression`, `CriticallyWounded`,
`MissingCapability`, `InsufficientAmmunition`, `TargetNotKnown`,
`IssuerNotRecognised`, `ImmediateThreat`, `UnsupportedAssault`) arrive with the
systems that can trigger them — B-019 / B-020 / B-021 / B-030 — rather than as
speculative type machinery now (`AGENTS.md`).

## 8. Minimal psychological model

### Discipline

Stable trait influencing willingness to maintain a valid commitment under pressure.

Realised by TASK-028 (backlog B-017): `AgentState.Discipline`, a non-negative
integer set once from `Deployment.Discipline` (default
`AppraisalConfig.DisciplineDefault`) and read only by the stage-4 resolve
threshold. Static authored data at this stage — dynamic discipline (and stress
/ trust) is B-021.

### Trust

Slow-changing confidence in the current commander's judgement. The vertical slice may initialise trust and leave dynamic trust changes minimal.

### Stress

Accumulates through nearby casualties, wounds, isolation, explosions, and threat. Decays when safe.

### Suppression

Immediate effect of hostile fire and impacts. It reduces action effectiveness, raises assault pressure, and may trigger taking cover.

Wounds are represented separately as physical state.

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

Realised by TASK-030 (backlog B-018) as the subset `Holding | Moving of
MoveCommitment` in `CommandoWar.Sim` — `Suppressing` / `Assaulting` /
`Withdrawing` need `PlayerIntent` cases that do not exist yet (`Hold` /
`Suppress` / `Assault` / `Withdraw` — B-030) and get no case, per `AGENTS.md`
"do not build speculative type machinery". `Commitment` is **not** a stored
`AgentState` field: it is a pure derived value
(`Commitment.ofAgent : ReceivedOrder option -> OrderDisposition option -> Cell
option -> Commitment`) recoverable from the already-canonical `Order` /
`Disposition` / `Destination` at every tick — the `AgentState.Route` precedent
(`docs/04` section 17). "Continues until completed" and "superseded by a
newer order" are realised (`CommitmentCompleted` / `CommitmentEstablished`
events, section 13); "invalidated by a material world change", "interrupted
by a higher-priority survival event", and "delayed or refused after explicit
reappraisal" are not — see section 11.

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

Realised by TASK-030 (backlog B-018) for `Move`, correspondingly thin since
`Simulation.navigationAndMovement` (`docs/04` section 12.7) already owns the
physical stepping: establish (a fresh `Accepted` order -> `Moving`, emit
`CommitmentEstablished`), continue (unchanged, no event), complete (the
target is reached -> `Holding`, emit `CommitmentCompleted`). No intermediate
states like the assault example above — there is no "wait for support" or
"cross danger area" concept without suppression (B-020) or a richer order
vocabulary (B-030).

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

Realised by TASK-030 (backlog B-018): only priority 6 has a live signal
today. A superseding order's `CommitmentEstablished` event is the complete
trace — since `Commitment` is derived, not stored (section 9), there is
nothing separate to report about the superseded commitment "ending", and no
resume question arises (a superseded commitment cannot resume; a new order
always takes precedence). Priorities 1–4 need combat/suppression state that
does not exist (B-019/B-020). Priority 5 ("route invalidated") was already
assigned to B-021 by TASK-028 (`docs/04` section 12.5): under static terrain
an Appraisal-`Accepted` route cannot later become unreachable, so there is no
live signal yet; a persistent stall from a live-but-unmoving obstruction
needs a stall counter (new state), which is B-021's job. Priority 7 (normal
commitment execution) is unchanged Navigation behaviour.

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

Realised so far (TASK-028, backlog B-017): the `OrderAppraised` event carries
the full `OrderDisposition` (outcome + structured reasons), and the
`Diagnostics` `OrderAppraisal` overlay carries the outcome plus the exposed
route cells the stage-3 exposure sum found — enough for the headless developer
overlay to explain any appraisal (`docs/07` section 9 criterion 11). A separate
persisted trace record (appraisal version, every pressure and threshold term)
is a later refinement.

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

Realised by TASK-028 (backlog B-017): **"a new order is received"** only — the
Communication phase resets `AgentState.Disposition` to `None` when it writes a
fresh `AgentState.Order`, and the Appraisal phase judges exactly the agents
whose `Disposition` is `None`. The knowledge-change, exposure-band,
suppression-band, wounded, support, and leadership triggers are B-021; "the
route becomes blocked" cannot fire for an appraisal-`Accepted` order under
static terrain (its route was verified at stage 2), so it too waits for B-021's
persistent-obstruction handling.

This improves stability and makes decisions easier to trace.

## 15. Tuning rules

- Prefer a small number of integer thresholds.
- Record every threshold in one configuration structure.
- Do not tune by adding hidden exceptions for one scenario.
- Keep primary reasons stable under small irrelevant state changes.
- Use hysteresis when entering and leaving panic, suppression, or delay states.
- Treat random variation as a last resort and bound it so identical tactical situations remain broadly predictable.

Realised by TASK-028 (backlog B-017): `AppraisalConfig`
(`src/CommandoWar.Sim/Appraisal.fs`) is the one configuration structure — a
`[<RequireQualifiedAccess>]` module of `[<Literal>]` integers (engagement
range, per-cell exposure weight, cover mitigation, base resolve, and the
Discipline / RiskTolerance / Urgency modifiers), the `PerceptionConfig`
precedent. All appraisal arithmetic is integer; appraisal draws no randomness
(B-019 combat spread stays the deterministic stream's first gameplay consumer).
Hysteresis has nothing to act on until B-021 adds an exposure-band reappraisal
trigger, so its constant lands then.

## 16. Vertical-slice scenarios for tests

### Exposed road

An assault route crosses a known machine-gun lane. A low-discipline, suppressed soldier delays. After suppression begins, the order becomes acceptable.

Partially realised by TASK-028 (backlog B-017): the `exposed-approach` corpus
entry and the `SimulationTests` "exposed route ... Refused for a low-discipline
agent" fact deliver the refusal half — a low-`Discipline` agent `Refused
RouteTooExposed`, a high-`Discipline` one `Accepted` on the same order (the G3
divergence, `docs/07` section 9 criterion 2). `Delayed` and "suppression makes
it acceptable" are B-020 / B-021.

### Covered alternative

The same target has a longer route behind walls. A cautious adaptation chooses it.

### Unknown threat

The squad has not observed the machine gun. An agent accepts based on current knowledge, then reacts when fired upon. The explanation distinguishes bad information from arbitrary behaviour.

### Physical inability

A critically wounded agent reports unable rather than refused.

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
