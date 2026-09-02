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

### Stage 1: comprehension and authority

- Was the order received?
- Is the issuer authorised?
- Is the target and intent understood?

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

## 8. Minimal psychological model

### Discipline

Stable trait influencing willingness to maintain a valid commitment under pressure.

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

This improves stability and makes decisions easier to trace.

## 15. Tuning rules

- Prefer a small number of integer thresholds.
- Record every threshold in one configuration structure.
- Do not tune by adding hidden exceptions for one scenario.
- Keep primary reasons stable under small irrelevant state changes.
- Use hysteresis when entering and leaving panic, suppression, or delay states.
- Treat random variation as a last resort and bound it so identical tactical situations remain broadly predictable.

## 16. Vertical-slice scenarios for tests

### Exposed road

An assault route crosses a known machine-gun lane. A low-discipline, suppressed soldier delays. After suppression begins, the order becomes acceptable.

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
