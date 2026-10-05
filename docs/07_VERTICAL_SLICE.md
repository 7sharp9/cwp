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

The commands are `PlayerIntent.MoveTo | Suppress | Hold | Assault | Withdraw`
(`src/CommandoWar.Sim/Domain.fs`). Each takes a bare `Cell` target except
`Suppress`, which takes an `AgentId`: a specific known contact (TASK-037, a
thin slice of backlog B-030), not the "known or suspected threat area" this
section's `SuppressArea` naming implies. No suspected-threat, id-less targeting
model exists (`docs/05` section 4). `Hold`, `Assault` and `Withdraw` (TASK-047,
backlog B-030 proper) each have appraisal, a commitment and an executor
(`docs/05` sections 4-5, 9-10). The Godot client issues all five (functional
acceptance criterion 1 in section 9).

## 5. Required simulation systems

- fixed integer tick;
- typed command validation;
- logical grid and directional cover;
- deterministic line of sight;
- shared squad tactical knowledge;
- basic enemy perception;
- pathfinding and local cell reservation;
- movement and formation slots;
- hitscan small-arms combat (TASK-031, backlog B-019): automatic symmetric
  engagement and a deterministic range- and cover-mitigated hit chance with one
  PRNG draw per shot. Ammunition and weapon readiness
  (TASK-047, backlog B-030 proper): `AgentState.Ammo` (a magazine plus
  reserve, automatic reload once empty, instant resupply on an authored
  `WorldState.ResupplyAreas` cell) gates every qualifying shot, `Suppress` and
  `Assault` alike. Wound and death consequences (TASK-045, backlog B-031);
- suppression (TASK-032, backlog B-020): `Combat` raises the target's
  `AgentState.Suppression`, cover-mitigated and independent of a hit, and
  `State consequences` decays it every tick. A `SuppressionBand` hysteresis
  latch (TASK-033, backlog B-021) feeds a discrete resolve-threshold penalty
  and a reappraisal trigger. A hostile contact's latch also zeroes its
  route-exposure contribution (TASK-037) and releases an `Assault` commitment
  from its `AwaitingSupport` stage (TASK-047): the assault holds within
  `AssaultStartRange` of the target while a known threat within
  `ThreatEngagementRange` of the target is not suppressed. Suppression does not
  change movement speed or hit chance;
- stress (TASK-033, backlog B-021, partial): `AgentState.Stress` rises from
  `VisibleContacts`, the one `docs/05` section 8 source with a system behind
  it, and feeds the resolve threshold as a continuous drag. Casualty-, wound-,
  explosion- and isolation-driven stress are unbuilt. Discipline is static
  (since TASK-028; B-021 found no reason to make it dynamic). Leader trust is
  unbuilt: `docs/05` section 8 leaves it "minimal" for the vertical slice;
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

The developer overlay is carried by the framework-neutral per-tick diagnostic
model (`src/CommandoWar.Sim/Diagnostics.fs`, `DiagnosticFrame`) and its
deterministic ASCII, SVG and HTML renderers
(`src/CommandoWar.Headless/DiagnosticRender.fs`, driven by `cwheadless render`;
TASK-011). The frame covers the terrain grid, directional cover, agents and
destinations, this-tick events, the tick / state-hash / random-draw trio, and
`Overlay` cases for the tactical systems (`docs/06` section 11). The HTML
scrubber is the headless form of "the developer overlay can explain any
appraisal and major state transition" (functional acceptance criterion 11). The
Godot developer overlay (TASK-043, backlog B-029) renders the same frame.

Tactical pause in `CommandDemoScene` is a manual `Space` toggle, an automatic
pause when `MissionOutcome` leaves `InProgress`, and an automatic pause the
instant any agent's order is newly appraised `Refused` or `Unable` (TASK-072,
backlog B-072). The last is the point at which the charter's "diagnose
resistance" step (`docs/00` section 2) needs it. A floating per-agent label
(`RenderShared.dispositionText`) at that agent's own cell, not gated on
selection, calls attention to the refusal.

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

The sequence is realised end to end by the `canonical-refusal-and-correction`
corpus entry (TASK-038, backlog B-023), a synthetic fixture, and on Bridgehead
(section 9). In the fixture:

- Steps 1 to 3: the player orders two soldiers across an approach past an
  observed machine-gun position, and the Appraisal phase `Refuses` the
  low-`Discipline` one with `DecisionReason.RouteTooExposed` and `Accepts` the
  high-`Discipline` one (the `exposed-approach` corpus entry, TASK-028, backlog
  B-017; functional acceptance criterion 2).
- Step 4: `OrderAppraised` only ever carries a structured
  `OrderDisposition` / `DecisionReason` (`Domain.fs`). No event or overlay emits
  a raw numeric exposure or threshold value, and
  `DiagnosticRender.reasonText` / `dispositionText` render it as named text
  ("refused route-too-exposed threat-agent-2"), never a score. A test assertion
  checks this.
- Steps 5 and 6: a `Suppress` order (`PlayerIntent.Suppress of target:
  AgentId`, TASK-037) that a second fireteam can issue against the known
  machine-gun contact. Its `Suppressing` commitment holds position and keeps
  the contact under fire. Once that contact's `AgentState.SuppressionBand`
  latches, `Appraisal.routeExposure` zeroes its contribution to every other
  agent's route exposure, and a reappraisal trigger (any agent's
  `SuppressionBand` flipping, not only the appraising agent's own) re-judges
  the first agent's `Refused` order the same tick (the
  `suppress-relieves-exposure` corpus entry). A `Refused` order is also
  re-judged once its blocking threat's contact expires (the knowledge-change
  trigger, TASK-033, backlog B-021).
- Step 7: the player reissues agent 0's identical `(11,3)` intent on tick 8,
  once it is already mid-route under the automatic reappraisal. The
  `reissued-order` corpus entry (TASK-030, backlog B-018) shows a superseding
  order taking over an in-progress commitment in isolation.
- Step 8: the reissued order is `Accepted` again at tick 8 with a fresh
  `CommitmentEstablished`, and stays `Accepted` (or unappraised, on arrival)
  through tick 13, never flipping back to `Refused`, including through one
  extra reappraisal blip at tick 12.

Only the accepts branch of step 8 is realised. No `Adapted` `OrderDisposition`
case exists (`Appraisal.fs`: "Stage 5 (safer adaptation) is deferred (B-018): no
`Adapted` outcome, no route recomputation"). This is a deliberate limitation
(L-01 in `docs/15_G4_DEFECT_TRIAGE.md`), deferred by Dave's explicit decision
rather than silently dropped.

Enemy doctrine (B-022) is outside this sequence: suppress-likely-routes,
seek-cover and scripted fall-back are Hostile-side concerns that the
player-issued `Suppress` order does not touch (L-02 in `docs/15`).

## 9. Functional acceptance criteria

The slice is feature-complete only when all twelve criteria below hold. As of
2026-10-02 every one is recorded as met. The gate decision itself (G4) is
Dave's and is not recorded here.

| # | Criterion | Status and evidence |
|---|---|---|
| 1 | Every required command can be issued and resolved. | Met. Move, Hold, Assault, Withdraw and Suppress are issued through the Godot client (TASK-048 for the order-mode HUD, TASK-064 for the Suppress icon) and resolved by the simulation. |
| 2 | At least two soldiers can appraise the same order differently for traceable reasons. | Met. The `exposed-approach` corpus entry: two agents, same route, one refuses (`RouteTooExposed`) and one accepts, by discipline alone. |
| 3 | The canonical refusal sequence passes with a fixed scenario and seed. | Met on a synthetic fixture (`canonical-refusal-and-correction`, TASK-038) and on Bridgehead (`bridgehead-succeeded`, tick 6). See "Bridgehead evidence" below. |
| 4 | A player action can predictably change an appraisal outcome. | Met. Suppressing the threat reverses a refusal in the synthetic fixture; on Bridgehead the same standing order is reappraised `Accepted` at tick 83 once rifleman 102 is down. |
| 5 | Enemy decisions use perceived or reported information rather than authoritative player positions. | Met (TASK-034): hostiles act on their own side's tactical picture, and a hostile never fires at a friendly it has not observed. |
| 6 | Leader death transfers command according to an explicit rule. | Met (TASK-045); the `casualties-succession-and-squad-failure` corpus entry traces it. |
| 7 | The mission can succeed and fail without developer intervention. | Met in both directions on the real Bridgehead content, by `MoveTo` orders only: `bridgehead-succeeded` and `bridgehead-failed`. See below. |
| 8 | Invalid content fails before the simulation begins with actionable diagnostics. | Met. `Scenario.validate` returns typed errors, and `cwheadless import` exits non-zero on invalid content (TASK-060). |
| 9 | A recorded command stream reproduces the same final authoritative state under the stated determinism contract. | Met. The replay corpus, checked by `cwheadless corpus` and `CorpusTests` on every build, includes both Bridgehead missions. |
| 10 | The headless runner can execute the mission scenario repeatedly without a graphical client. | Met. `cwheadless` replays and renders the mission without any client. |
| 11 | The developer overlay can explain any appraisal and major state transition. | Met. The diagnostic frame carries appraisal reasons and state overlays (TASK-011, TASK-043). |
| 12 | Placeholder-art play is understandable before final visual production. | Met in the sense the project can test internally (TASK-041, TASK-064). Whether it is understandable to someone who did not build it is the G5 external playtest. |

### Bridgehead evidence

The mission content, `bridgehead.cwscenario`, runs inside the play scene
(TASK-064). Criteria 3 and 7 rest on the following behaviour: corpses do not
block movement (TASK-066), agents detour around a parked ally (TASK-070), the
map has a second ford and a second extraction cell (TASK-073), and the `Failed`
direction is verified (TASK-075). Two committed corpus entries demonstrate it
(TASK-077). They are built from the real content (embedded into `cwheadless`,
seed 20260920, the play scene's seed) and driven only by `MoveTo` orders.

`bridgehead-succeeded` reaches `Succeeded` at tick 215.

- The machine gun falls to a bridge push by tick 71, with no friendly loss.
- Agent 5 halts at the ford stand-off cell `(5,9)`, from which rifleman 102 at
  `(13,9)` is visible at Chebyshev distance 8, outside weapon range 7. At tick
  6 it is ordered on to `(12,9)` and refused (`RouteTooExposed`) before any
  shot is fired.
- Riflemen 101 and 102 are engaged from `(10,5)`, `(10,6)` and `(11,6)`, at
  the cost of two friendly agents. At tick 83, with 102 down, the same standing
  order for agent 5 is reappraised `Accepted`.
- The charge is planted on `(9,5)` for the ten-tick demolition window
  (complete at tick 160), then the four survivors extract through both
  extraction cells.

`bridgehead-failed` reaches `Failed` at tick 84 from one six-agent frontal
charge. `Failed` fires when every friendly agent has left `Alive`; an
incapacitated agent already counts as eliminated.

Diagnostic goldens in `content/diagnostics/bridgehead-*` pin the refusal frame
and both outcome frames.

### What the evidence does and does not show

These are properties of the build as it stands, relevant to the G5 playtest.

- **The mission is winnable only by a specific approach.** Rifleman 102 covers
  the demolition cell `(9,5)` at distance 4, so the machine gun alone is not
  the whole threat there. The viable approach engages both riflemen from cells
  off the objective. The game does not teach this (backlog B-070).
- **Refusal arrives one tick after contact.** `Pathfinding` is deliberately
  threat-blind, and a new contact is appraised the tick after it is observed,
  while combat's own range check already fires on the contact tick. A single
  order that runs deep into the depot therefore skips the stand-off band and is
  refused too late; the stand-off refusal needs the two-step order above (halt
  at the ford, then push). The same lag lets the `Failed` run kill agents that
  were already refusing.
- **Each extraction cell holds one agent at a time.** An extracted agent stays
  on its cell, so later arrivals stall unless it steps off. The second cell
  doubles throughput; it does not remove the choreography.
- **The Bridgehead entries depend on two rules**: a downed agent perceives
  nothing (TASK-078), and `ExtractAgents` needs at least one extracted agent
  (TASK-079). Corpus re-pins are explained in `content/replays/CORPUS.md`.

The full record for each step is in the ledger entries for TASK-064, 066, 070,
073, 075, 077, 078 and 079.

## 10. Performance budgets

These are initial budgets and may be revised only with measured evidence.

- 20 authoritative simulation ticks per second.
- 60 graphical updates per second on the development workstation under normal conditions.
- 50 friendly and enemy agents in a synthetic stress scene, even though the slice uses fewer.
- no routine full-heap allocation proportional to total map size on each tick;
- no unbounded pathfinding or appraisal work in a single tick;
- stable frame pacing during command preview and debug overlays;
- headless execution at substantially faster than real time.

`bench/CommandoWar.Benchmarks/` is the headless performance and allocation
harness (`docs/09` section 2.8; TASK-014, backlog B-013), and
`content/benchmarks/BASELINE.md` is the committed baseline with a documented
regeneration command. It covers the empty tick, movement at 6 and about 50
agents, a line-of-sight batch, pathfinding over open, blocked and choke maps,
canonical encode, state hash and a replay run. 50-agent perception, appraisal,
combat and the full synthetic tick are not yet in the harness, and the
baseline (recorded 2026-09-04) predates those phases. On the reference machine
it keeps every per-tick measurement at least about 36x inside the 5 ms budget
with no map-size-proportional per-tick allocation, so the budgets in this
section stand as written. Exact per-subsystem millisecond and allocation
numbers are still a follow-up: set them once the harness is re-run against the
real phases and the Bridgehead map. The 60 fps graphical budget and the
frame-pacing items are untested and no measurement is committed
(`docs/15_G4_DEFECT_TRIAGE.md` U-05).

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
