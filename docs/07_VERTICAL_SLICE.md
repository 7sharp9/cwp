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

### Realised by TASK-072 (backlog B-072): "tactical pause ... while issuing orders"

This bullet's pause requirement was unbuilt as of the 2026-09-21
charter-alignment review (only a manual `Space`-toggle and an automatic pause
on `MissionOutcome` leaving `InProgress` existed). TASK-072 ties tactical
pause to the moment it is actually needed for the charter's own "diagnose
resistance" step (section 2): `CommandDemoScene`'s `stepOnce` auto-pauses the
instant any agent's order is newly appraised `Refused`/`Unable`, alongside a
floating per-agent label (`RenderShared.dispositionText`) at that agent's own
cell, ungated from selection -- previously the order-status text rendered
only for a single selected agent and nothing called attention to it at all.
Implemented and self-verified 2026-09-21; status `review`, awaiting Dave's
acceptance (`tasks/TASK-072-*.md`, `docs/11_BACKLOG.md` B-072 row).

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

TASK-064 was the first task to run the mission content, `bridgehead.cwscenario`,
inside the play scene rather than as a hand-built fixture. Criteria 3 and 7
were not closed on it at first. Four later changes closed them: corpses stopped
blocking movement (TASK-066), agents detour around a parked ally (TASK-070), a
second ford and a second extraction cell were added (TASK-073), and the
`Failed` direction was re-verified (TASK-075). TASK-077 then replaced the
throwaway probes that had demonstrated all this with two committed corpus
entries, built from the real content (embedded into `cwheadless`, seed
20260920, the play scene's seed), driven only by `MoveTo` orders.

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
- **The two Bridgehead entries moved under later behaviour fixes** (a downed
  agent no longer perceives, TASK-078; `ExtractAgents` needs at least one
  extracted agent, TASK-079). Outcomes, refusals and deaths were unchanged;
  only canonical state and, for `bridgehead-failed`, its completed-objectives
  list moved. The re-pins are explained in `content/replays/CORPUS.md` and the
  tasks' ledger entries.

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
