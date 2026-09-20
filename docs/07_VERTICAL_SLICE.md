# Vertical Slice Definition

Status: draft  
Working scenario: Bridgehead

## 1. What the slice must prove

The slice exists to answer one product question:

> Can an agent's legible, reversible refusal make squad command more satisfying rather than less controllable?

It is not intended to prove a reusable engine, a campaign, a full AI architecture, or commercial demand.

## 2. Player experience target

A new player should be able to complete the mission in roughly 10 to 20 minutes after a short embedded tutorial. During that mission, the player should encounter at least one dangerous order that a soldier delays, adapts, or refuses for a visible tactical reason.

The player must be able to change that reason, for example by suppressing a machine-gun position or selecting a covered route, then reissue the order and obtain a predictable different response.

## 3. Scenario

### Setting

A compact industrial rail bridge and depot at dusk. The setting and visual identity must be original.

### Friendly force

- one squad leader controlled directly;
- five additional soldiers;
- two fireteams of three including the leader;
- one support weapon suitable for suppression;
- one demolition interaction owned by the squad rather than a detailed inventory system.

### Enemy force

- one machine-gun team controlling an exposed approach;
- four riflemen in prepared positions;
- no reinforcements;
- no vehicles;
- no omniscient knowledge.

### Objective sequence

1. Reach a position from which the squad can observe the bridge area.
2. Neutralise, bypass, or suppress the machine-gun threat.
3. Move a demolition-capable soldier to the bridge objective.
4. Hold the position while the charge is planted.
5. Withdraw surviving required personnel to extraction.
6. Detonate and complete the mission.

The implementation may simplify detonation timing if it distracts from the command loop.

## 4. Required player commands

Only these commands are required:

- `MoveTo`
- `HoldArea`
- `SuppressArea`
- `AssaultArea`
- `WithdrawTo`

Direct leader movement may use a separate immediate input command, but it must pass through the same authoritative simulation boundary.

Realised as `PlayerIntent.MoveTo | Suppress | Hold | Assault | Withdraw`
(`src/CommandoWar.Sim/Domain.fs`), each a bare `Cell` target except
`Suppress`, which is `AgentId` — a specific known contact (TASK-037, a
thin B-030 slice), not the "known or suspected threat area" this section's
`SuppressArea` naming implies; no suspected-threat, id-less targeting model
exists (`docs/05` section 4). `Hold`/`Assault`/`Withdraw` realised by
TASK-047 (backlog B-030 proper) with real appraisal, a commitment, and an
executor each (`docs/05` sections 4-5, 9-10); issuing them from
`CommandDemoScene` (client input) stays a future client task, the
`Suppress`-order precedent — this session's realisation is sim-side proof
only (a real corpus regeneration, `SimulationTests` facts, no new client
UI).

## 5. Required simulation systems

- fixed integer tick;
- typed command validation;
- logical grid and directional cover;
- deterministic line of sight;
- shared squad tactical knowledge;
- basic enemy perception;
- pathfinding and local cell reservation;
- movement and formation slots;
- hitscan small-arms combat (realised by TASK-031, backlog B-019: automatic
  symmetric engagement, a deterministic range- and cover-mitigated hit
  chance, the simulation's first real gameplay PRNG draw). Ammunition and
  weapon readiness realised by TASK-047 (backlog B-030 proper):
  `AgentState.Ammo` (a magazine + reserve, automatic reload once empty,
  instant resupply on an authored `WorldState.ResupplyAreas` cell) gates
  every qualifying shot, `Suppress`/`Assault` alike; wound/death
  consequence realised by TASK-045 (backlog B-031, below);
- suppression (realised by TASK-032, backlog B-020: `Combat` raises the
  target's `AgentState.Suppression`, cover-mitigated and independent of a
  hit; `State consequences` decays it every tick. TASK-033, backlog B-021,
  added the first consumer: a `SuppressionBand` hysteresis latch feeds a
  discrete resolve-threshold penalty and a reappraisal trigger — still no
  effect on movement or executor behaviour);
- stress (realised by TASK-033, backlog B-021, partial: `AgentState.Stress`
  rises from `VisibleContacts` — the one `docs/05` section 8 source with a
  system behind it — and feeds the resolve threshold as a continuous drag;
  casualty-, wound-, explosion-, and isolation-driven stress remain unbuilt),
  discipline (static since TASK-028; stays static, B-021 confirmed no reason
  to make it dynamic), and leader trust (unbuilt — `docs/05` section 8
  leaves it "minimal" for the vertical slice);
- explicit order appraisal;
- accepted, delayed, adapted, refused, and broken outcomes;
- finite execution states;
- ordered reactive interrupts;
- leader succession;
- mission objectives and extraction;
- command recording, seeded replay, and canonical state hash;
- render snapshots and domain events.

## 6. Required presentation

- isometric map and sprites, placeholders permitted until the integration gate;
- tactical pause or slow motion while issuing orders;
- unit and fireteam selection;
- proposed path and destination;
- known threat presentation;
- acknowledgement and disposition feedback;
- concise refusal or adaptation reason;
- objective and extraction indicators;
- developer overlay for perception, appraisal, movement, and deterministic state;
- mission completion and failure summary;
- replay playback.

### Realised by TASK-011 (developer overlay foundation, headless)

The framework-neutral per-tick diagnostic model
(`src/CommandoWar.Sim/Diagnostics.fs`, `DiagnosticFrame`) and its deterministic
ASCII / SVG / HTML renderers (`src/CommandoWar.Headless/DiagnosticRender.fs`,
driven by `cwheadless render`) give the developer overlay its data foundation
before any client work. It covers the terrain grid, directional cover, agents
and destinations, this-tick events, and the tick / state-hash / random-draw
trio; perception, appraisal, commitment, and reservation state attach as
`Overlay` cases when those systems land (B-009 to B-011, B-019). The HTML
scrubber is the headless form of "the developer overlay can explain any
appraisal and major state transition" (functional acceptance criterion 11) for
the systems that currently exist. The Godot overlay (B-029) renders the same
frame.

## 7. Deliberate exclusions

The slice must not include:

- vehicles;
- multiplayer;
- procedural maps;
- campaign progression;
- inventory management;
- pairwise social relationships;
- civilians;
- stealth takedowns;
- destructible buildings;
- fire propagation;
- arbitrary scenario scripting;
- a public modding API;
- a general HTN, GOAP, or machine-learned planner;
- runtime LLMs;
- multiple factions or eras;
- final production art for content not present in the mission.

## 8. Canonical refusal sequence

The mission layout must reliably support this test sequence:

1. The player orders a cautious or stressed soldier across an exposed approach.
2. The soldier identifies a known machine-gun lane.
3. The order is delayed, adapted, or refused with `RouteTooExposed` or `UnsupportedAssault`.
4. The UI explains the main reason without exposing a meaningless raw score.
5. The player orders another fireteam to suppress the machine-gun position or selects a covered route.
6. Tactical knowledge and exposure are recalculated.
7. The player reissues the original intent.
8. The soldier accepts or adapts it consistently.

This sequence must be testable headlessly before presentation polish begins.

Partially realised by TASK-028 (backlog B-017): steps 1–3 land as the
`exposed-approach` corpus entry — the player orders two soldiers across an
approach past an observed machine-gun position, and the Appraisal phase
`Refuses` the low-`Discipline` one with `DecisionReason.RouteTooExposed` (and
`Accepts` the high-`Discipline` one — criterion 2). Step 7 ("the player
reissues the original intent") is realised in isolation by TASK-030 (backlog
B-018): the `reissued-order` corpus entry shows a superseding order taking
over an in-progress commitment, though without the preceding suppress/reroute
correction step 7 presupposes in the full sequence. TASK-032 (backlog B-020)
landed the raw `AgentState.Suppression` value real fire now creates and
decays; TASK-033 (backlog B-021, partial) landed the recalculation mechanism
a *same-agent* trigger needs — the knowledge-change reappraisal trigger
re-judges a `Refused` order once its blocking threat's contact expires.

Steps 5–6 are realised by TASK-037 (a thin B-030 slice, pulled forward as P3
decision-support): a `Suppress` order (`PlayerIntent.Suppress of target:
AgentId`) that a second fireteam can issue against the known machine-gun
contact, whose `Suppressing` commitment holds position and keeps it under
fire; once that contact's `AgentState.SuppressionBand` latches,
`Appraisal.routeExposure` zeroes its contribution to every other agent's
route exposure, and a new reappraisal trigger (any agent's `SuppressionBand`
flipping, not only the appraising agent's own) re-judges the first agent's
`Refused` order the same tick — step 5 ("the player orders another fireteam
to suppress the machine-gun position") and step 6 ("tactical knowledge and
exposure are recalculated") both proven end to end by the new
`suppress-relieves-exposure` corpus entry. Enemy doctrine (B-022) was
explicitly descoped by TASK-037 — suppress-likely-routes, seek-cover, and
scripted fall-back are Hostile-side concerns TASK-037's player-issued order
does not touch.

The full 8-step sequence is realised end to end by TASK-038 (backlog B-023)
as the `canonical-refusal-and-correction` corpus entry: the
`suppress-relieves-exposure` geometry, plus a step 7 reissue of agent 0's
identical `(11,3)` intent on tick 8, once it is already mid-route under the
automatic reappraisal. Step 4 needed no new mechanism: `OrderAppraised` only
ever carries a structured `OrderDisposition`/`DecisionReason` (`Domain.fs`)
— no event or overlay in the codebase emits a raw numeric exposure/threshold
value, and `DiagnosticRender.reasonText`/`dispositionText` already render it
as named text ("refused route-too-exposed threat-agent-2"), never a score;
this task adds a test assertion turning that standing type-system guarantee
into checked evidence. Step 8 is proven for its accepts-branch only: the
reissued order is `Accepted` again at tick 8, with a fresh
`CommitmentEstablished`, and stays `Accepted` (or unappraised, on arrival)
through tick 13 — never flipping back to `Refused`, including through one
extra reappraisal blip at tick 12. The literal "or adapts" half of step 8 is
**not** realised: no `Adapted` `OrderDisposition` case exists (`Appraisal.fs`:
"Stage 5 (safer adaptation) is deferred (B-018): no `Adapted` outcome, no
route recomputation") — a separate, larger, not-yet-filed follow-up, deferred
by Dave's explicit decision rather than silently dropped.

## 9. Functional acceptance criteria

The slice is feature-complete only when:

1. Every required command can be issued and resolved.
2. At least two soldiers can appraise the same order differently for traceable reasons.
3. The canonical refusal sequence passes with a fixed scenario and seed.
4. A player action can predictably change an appraisal outcome.
5. Enemy decisions use perceived or reported information rather than authoritative player positions.
6. Leader death transfers command according to an explicit rule.
7. The mission can succeed and fail without developer intervention.
8. Invalid content fails before the simulation begins with actionable diagnostics.
9. A recorded command stream reproduces the same final authoritative state under the stated determinism contract.
10. The headless runner can execute the mission scenario repeatedly without a graphical client.
11. The developer overlay can explain any appraisal and major state transition.
12. Placeholder-art play is understandable before final visual production.

### Realised (and partially not) by TASK-064 (backlog B-035)

TASK-064 is the first task to run the mission content this section
describes -- `bridgehead.cwscenario` -- inside the actual play scene
(`CommandDemoScene`) rather than as a headless import check or a separate
hand-built fixture. `IClientScene.Ready` gained a `scenarioContentPath`
parameter; `CommandDemoScene` now parses/validates/builds the world from
the real file (the `DemoScenario.fs` pipeline shape) instead of loading
`DemoScenario`. It also added the client's missing `Suppress` order-mode
icon (section 4's fifth required command had no HUD path at all before
this task, B-059's own backlog text having said so outright) and rewrote
`CommandDemoScene`'s scripted self-check against real Bridgehead
coordinates, reaching a genuine, reproducible, casualty-free
neutralisation of the machine-gun team through the real click path
(`0xB99E7F74EA1C3CDE` at tick 90).

Criteria 1, 2, 4, 5, 6, 8, 9, 10, 11, and 12 are met -- some newly
demonstrated on Bridgehead itself (1, 4, 9, 12), the rest via existing
evidence this task confirmed still holds (2, 5, 6, 8, 10, 11). **Criteria 3
and 7 are not closed on Bridgehead**, despite extensive investigation (a
series of temporary `dotnet fsi` probes, removed after use): every
Bridgehead `MoveTo` order tested was `Accepted` outright, since no contact
with the machine gun exists until an agent is already inside its own
engagement range (no intermediate "spotted but not yet fired on" cell
exists on this map) -- so criterion 3's canonical refusal sequence, though
proven end to end on a separate fixture (TASK-038), was never triggered on
Bridgehead itself; and criterion 7 (mission succeeds/fails without
developer intervention) was not reached in either direction -- a full
`Succeeded` run was blocked by an apparent additional threat covering the
`bridge-charge` target cell itself, and a full `Failed` run turned out to
be geometrically capped well short of all six friendly agents by the
bridge's own two-lane chokepoint. See `tasks/TASK-064-INTEGRATE-AND-VERIFY-
VERTICAL-SLICE.md` for the full record and the two concrete mechanism
findings behind this (a corpse permanently blocking a cell; the
target-cell threat). **Accepted by Dave (2026-09-20, via `AskUserQuestion`)
as a known, tracked gap** rather than pursued further -- no Bridgehead map
rebalance and no corpse-occupancy fix were made; TASK-064 was accepted with
this gap on record.

## 10. Performance budgets

These are initial budgets and may be revised only with measured evidence.

- 20 authoritative simulation ticks per second.
- 60 graphical updates per second on the development workstation under normal conditions.
- 50 friendly and enemy agents in a synthetic stress scene, even though the slice uses fewer.
- no routine full-heap allocation proportional to total map size on each tick;
- no unbounded pathfinding or appraisal work in a single tick;
- stable frame pacing during command preview and debug overlays;
- headless execution at substantially faster than real time.

Exact millisecond and allocation budgets should be set after TASK-003 establishes a benchmark harness.

### Realised by TASK-014 (headless benchmark harness)

`bench/CommandoWar.Benchmarks/` is the headless performance and allocation
harness (`docs/09` section 2.8; backlog B-013), and
`content/benchmarks/BASELINE.md` is the committed baseline with a documented
regeneration command. It covers the authoritative systems that exist today
(empty tick, placeholder movement at 6 and ~50 agents, line-of-sight batch,
pathfinding open/blocked/choke, canonical encode, state hash, replay run);
50-agent perception, appraisal, and the full synthetic tick attach when
B-015 / B-017 / B-019 land. The reference-machine baseline keeps every per-tick
measurement at least ~36x inside the 5 ms budget with no map-size-proportional
per-tick allocation, so the budgets in this section stand as written. Exact
per-subsystem millisecond and allocation numbers are still a follow-up: set
them once the real phases and the greybox map (B-025) exist and the harness is
re-run against them. The 60 fps graphical budget and frame-pacing items remain
untested (no client yet).

## 11. External playtest gate

Use at least five participants who did not implement the game. This is not a formal research study and must not be represented as one.

Record:

- whether they understood why the agent resisted;
- whether they knew what action could change the decision;
- whether the changed battlefield condition produced the expected result;
- mission completion and restart count;
- moments of surprise or perceived unfairness;
- commands that were misunderstood;
- whether the autonomy felt meaningful, arbitrary, or cosmetic;
- whether they would voluntarily replay the mission.

### Minimum continuation signal

Proceed toward production only if:

- at least four of five participants can explain the principal refusal reason without developer explanation;
- at least four can identify a corrective tactical action;
- at least three voluntarily complete or replay the mission;
- no recurring issue shows that refusal is perceived as random or as input failure;
- deterministic replay and technical gates remain intact.

These thresholds are product heuristics, not statistical evidence.

## 12. Kill or redesign conditions

Stop or redesign the core interaction if any of these persists after two focused iterations:

- players consistently prefer direct control with appraisal disabled;
- refusal reasons are understandable only through developer overlays;
- the easiest solution is always selecting the most obedient soldier;
- suppression and route changes do not create meaningful command choices;
- agent variation feels random rather than learnable;
- the command loop requires so much pausing that real-time play adds no value;
- the technical architecture cannot reproduce and inspect failures;
- content production cost makes a second mission implausible.
