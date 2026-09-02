# Project Charter

Status: baseline proposal  
Owner: Dave  
Last revised: 2026-09-02

## 1. Product hypothesis

A real-time isometric squad game can make partially autonomous soldiers enjoyable rather than frustrating when three conditions hold:

1. Orders express tactical intent rather than animation-level instructions.
2. Acceptance, adaptation, delay, and refusal arise from visible battlefield conditions.
3. The player can change those conditions and predict the resulting change in behaviour.

The project is not justified by retro presentation alone. Its reason to exist is the command relationship between the player and bounded-autonomy squad members.

## 2. Player promise

The player commands a small squad whose members are competent but not mindless. They observe an uncertain battlefield, assess danger, and may adapt or reject an order that appears impossible or suicidal. Their decisions are legible. A good player learns how to create the conditions in which difficult orders become feasible.

The intended emotional loop is:

```text
Form intent
  -> issue order
  -> observe interpretation
  -> diagnose resistance or failure
  -> alter tactical conditions
  -> reissue or adapt the plan
  -> see the squad commit
```

## 3. First product shape

The first proof is one short bridge-demolition mission with:

- one leader and five friendly soldiers;
- two fireteams;
- a small enemy force with one machine-gun position;
- a compact isometric map;
- move, hold, suppress, assault, and withdraw orders;
- cover, line of sight, suppression, stress, discipline, and explainable order appraisal;
- a deterministic replay of commands and outcomes.

This is a spiritual successor, not a remake.

## 4. Target audience

Primary:

- players who enjoy readable tactical systems;
- players attracted to late-1980s and early-1990s isometric presentation;
- players who prefer small-unit command over high-unit-count RTS control.

Secondary:

- game-AI and programming-language practitioners interested in explicit command semantics and explainable agent decisions.

The secondary audience must not distort the first playable experience.

## 5. Success criteria

### Concept success

In an external playtest, most players can correctly answer:

- what order they issued;
- why a soldier did not immediately comply;
- what battlefield change could improve compliance;
- whether the changed response was consistent with what they expected.

### Technical success

- The authoritative simulation runs without graphics.
- The same scenario, seed, and accepted command stream produce the same final state hash within the supported determinism boundary.
- Presentation cannot change authoritative outcomes.
- Order decisions emit structured reasons.
- A failed content load reports exact, actionable validation errors.

### Production success

A production decision is permitted only after the vertical slice is fun with temporary assets. Visual polish must not be used to conceal a weak command loop.

## 6. Non-goals for the vertical slice

- A general game engine.
- A recreation of the historical Action Concept platform.
- Multiple eras or scenario packs.
- Multiplayer or lockstep networking.
- Vehicles.
- A campaign metagame.
- Procedural maps.
- Destructible-everything terrain.
- Full social simulation.
- Runtime machine learning or LLM agents.
- A publishable human-subject study.
- Console certification.
- A public mod SDK.

## 7. Design principles

### Legibility over realism

The simulation may simplify reality when greater detail does not improve player decisions. Hidden complexity that cannot be understood or influenced is a defect.

### Tactical causes over arbitrary personality

A refusal should normally trace to route exposure, suppression, wounds, isolation, missing capability, or lost communication. Personality changes thresholds and preferences. It must not function as an unexplained random veto.

### Small numbers, meaningful differences

Six friendly soldiers are enough for the first proof. More agents multiply pathfinding, UI, tuning, and explanation problems without validating the central premise.

### Authoritative simulation, replaceable presentation

The game rules are independent of the chosen presentation framework. This is a testability rule, not an invitation to build several permanent clients.

### Evidence before abstraction

Generalise only after repeated concrete need. No subsystem, plugin interface, scenario language, planner, or ECS is justified by imagined future content.

## 8. Intellectual property and identity

The public game must not be named `Commando War` without verified rights. Do not copy the unreleased game's art, maps, branding, prose, or scenario content. The historical concept may inform high-level mechanics that are independently implemented.

The project needs an original name and setting treatment before any public store page, trailer, crowdfunding, or press contact.

## 9. Research boundary

A later research programme could study command semantics, explanation, calibrated trust, player workload, or bounded agent autonomy. That work begins only after the game loop survives external playtesting.

Research telemetry must be designed so it can be enabled without changing the decisions being studied. The game is not to be built around collecting a thesis dataset before it is enjoyable.

## 10. Stop conditions

The project should stop, narrow, or pivot if any of the following remain true after focused iteration:

- players experience refusals primarily as random loss of control;
- explanations require long text or developer overlays to make sense;
- changing tactical conditions rarely changes agent appraisal;
- pathfinding and formation work consume the project while producing no distinct gameplay;
- the content pipeline prevents a small mission from being revised quickly;
- the game is only enjoyable when agents behave like perfectly obedient RTS units;
- the technical stack is selected mainly because it is interesting to program rather than effective for producing the game.
