# TASK-047: Assault, Withdraw, Hold order executors and an ammunition/reload/resupply model

Status: review (drafted 2026-09-18; central decisions A-P confirmed with
Dave 2026-09-18, three `AskUserQuestion` rounds, before the phase bodies;
implemented and self-verified 2026-09-18)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises B-030 proper (the
`Suppress`-order slice already landed via TASK-037); unblocks B-032
(demolition objective/extraction, which depends on B-030)
Size: L (flagged to Dave as materially larger than TASK-037, the previous
largest single task in this project; Dave chose to bundle it into one task
rather than split — see Decision A)

## Outcome (2026-09-18)

Implemented exactly as Decisions A-P describe, with two deviations found
during implementation (both below). `PlayerIntent` gained `Hold of area:
Cell`, `Assault of target: Cell`, `Withdraw of target: Cell`;
`DecisionReason.InsufficientAmmunition`; `Commitment.Withdrawing` and
`Assaulting` (with `AssaultStage`); a new `AmmoState` type and
`AgentState.Ammo` field; a new `Ammo.fs` leaf; a new authored
`WorldState.ResupplyAreas`/`Scenario.ResupplyAreas` (`ScenarioContent.
Version` 2 -> 3); three new events (`ReloadStarted`/`ReloadCompleted`/
`AgentResupplied`); a new sparse `Overlay.AgentAmmo`. `Canonical.
FormatVersion` 10 -> 11, full re-pin.

**Deviation 1 (Decision C reconsidered against Decision I's actual
signature):** `Commitment.ofAgent`'s new context parameters
(`threats`/`suppressedThreats`/`position`) turned out necessary for a third
call site beyond the two Decision I named (`Simulation.combat`,
`Diagnostics.commitmentOverlays`): `Simulation.commitmentAndLocalAction`
itself, to drive Assault's stage-dependent `Destination` freeze/unfreeze
(Decision G). Folded in directly — the identical bounded-blast-radius
argument Decision I already made, just one more site.

**Deviation 2 (test-only, found while writing `SimulationTests`):**
`Ammo.tick`'s `Reloading` countdown, copied directly from `Casualty.
tickBleedOut`'s "`remaining <= 1` fires the transition on that tick" shape,
takes `AmmoConfig.ReloadTicks` ticks *after* the tick that sets `Reloading`
— not `ReloadTicks - 1` as first assumed when drafting the reload
`SimulationTests` fact. Confirmed intentional (the exact `BleedOutTicks`
precedent: `Incapacitated 60 -> Dead` takes exactly 60 ticks after the
incapacitating tick), fixed the test's loop bound, not the leaf.

No new corpus entry (Considered and rejected list, below, extended):
proof is 17 new `SimulationTests` facts directly on the phases and pure
functions — the full `AssaultStage` derivation (all four states, including
the two `position = target` fallback branches), the Withdraw/Assault
threshold contrast against the identical `MoveTo` geometry the existing
"Refused for low discipline"/"Accepted for high discipline" facts already
established, Hold's cover-redirect (a real `AuthoredCover` geometry proving
`bestCoverNear` picks a genuinely different, lower-pressure cell), and the
full ammo fire/reload/resupply/stage-2-gate cycle.

Godot: both scenes' `--selfcheck` pinned hashes recomputed by calling
`DemoDrive.runFullSequence()`/`CommandDemoDrive.runScriptedSelfCheck()`
directly via `dotnet fsi` against the built `CommandoWar.Client.Godot.
Core.dll` (no Godot install in the implementing environment, the
TASK-034/037 precedent) — SnapshotDemo `0x8E93B48D07AE9CBD`, CommandDemo
`0xE661187DE95E92E6`; `AppraisalDemoScene`'s `exposed-approach` tick-1 pin
also moved (`0x194805888CBE240D`, read directly from the regenerated
`exposed-approach.md` checkpoint table, not recomputed separately). None
independently re-run through the real Godot 4.7.2 editor this session —
flagged for Dave to re-check before accepting.

`dotnet build CommandoWar.slnx -c Release`: `0/0`. `dotnet build` the Godot
client `.slnx` (Debug and Release): `0/0` both. `dotnet test`: `336/336`
(319 pre-existing + 17 new). `cwheadless corpus`: `16/16`, `--regenerate`
twice byte-identical (confirmed via `md5sum`). `cwheadless fixture`:
unaffected (36 events, byte-layout-only re-pin — the fixture is enemy-free
and never exhausts an agent's default 30-round magazine).
`cwheadless replay-file content/replays/envelope-full.cwreplay`: OK, 78
events unchanged, all 24 checkpoints match. `dotnet list
CommandoWar.Sim.fsproj package --include-transitive`: `FSharp.Core` only.
Source scan of `src/CommandoWar.Sim` for
`float|stopwatch|datetime|system\.random|godot`: clean. `git status
--porcelain` matches this task's Allowed scope.

Full detail: `docs/ledger/2026-09-18-TASK-047-assault-withdraw-hold-and-ammunition.md`.

## Objective

Give the player the three remaining `docs/07_VERTICAL_SLICE.md` section 4
order types this simulation does not yet have (`HoldArea`, `AssaultArea`,
`WithdrawTo` — `MoveTo` and `SuppressArea`'s known-contact slice already
exist), each with real appraisal, a commitment, and an executor; and replace
today's unlimited combat fire with a finite per-agent ammunition model
(magazine + reserve stock, an automatic reload once empty, and a
scenario-authored resupply-area mechanic), closing the two gaps `Combat.fs`'s
own header has named as "a future task, not yet scoped" since TASK-031.

## Why this task exists

`docs/11_BACKLOG.md` B-030's row has read "proposed" since P3, repeatedly
flagged across TASK-037/044/045/046 session summaries as the largest
remaining sim-mechanics gap and the direct blocker for B-032 (the
demolition objective/extraction sequence `docs/07` section 3 steps 3-6
need). `docs/07` section 4 lists `HoldArea`/`AssaultArea`/`WithdrawTo`
among the five required player commands; none exist as a `PlayerIntent`
case today. `Combat.fs`'s own module doc has listed "ammunition, weapon
readiness, fire-rate/cooldown, reload, resupply" as deliberately absent
since TASK-031 (2026-09-13) with no backlog number assigned until this
session. Selected this session via `AskUserQuestion` over B-058 (agent
movement speed, filed during TASK-046 review, smaller and faster).

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `docs/04_SIMULATION_SPEC.md` sections 12.5 (Appraisal), 12.6 (Commitment),
  12.7 (Navigation), 12.8 (Combat), 12.9 (State consequences), 17 (canonical
  image / derived caches), 21 (content)
- `docs/05_COMMAND_AND_AGENT_AI.md` sections 4 (order vocabulary — Hold,
  Assault, Withdraw), 5 (stages 2-4), 7 (`DecisionReason` vocabulary), 8
  ("required ammunition or equipment available"), 9 (`Commitment` —
  `Assaulting of AssaultCommitment`, `Withdrawing of WithdrawCommitment`),
  10 (finite action executor, the full assault-FSM example), 11 (interrupts)
- `docs/07_VERTICAL_SLICE.md` sections 3-5
- `docs/11_BACKLOG.md` rows B-030, B-032
- `tasks/TASK-030-COMMITMENT-AND-FINITE-EXECUTOR.md`,
  `TASK-031-HITSCAN-COMBAT.md`, `TASK-037-SUPPRESS-ORDER-AND-DEPENDENCY-CLOSEOUT.md`
- `src/CommandoWar.Sim/{Domain.fs, Appraisal.fs, Commitment.fs, Combat.fs,
  Simulation.fs, Scenario.fs, Canonical.fs}`

## Dependencies

- B-018 (TASK-030, done), B-020 (TASK-032, done) — already satisfied, the
  reason B-030 was formally unblocked even while P4.

## Central decisions (confirmed with Dave 2026-09-18 before the phase bodies)

### Decision A — one combined task, not split — CONFIRMED

Flagged to Dave that the combined scope (three order executors, a staged
Assault FSM, and a finite ammo/reload/resupply model) is materially larger
than TASK-037 and offered a two-task split (order executors first, ammo as
a follow-up — the B-020/B-021 and B-021/B-022 sequencing precedent). Dave
chose one combined task.

### Decision B — order shapes: `Hold of area: Cell`, `Assault of target: Cell`, `Withdraw of target: Cell` — CONFIRMED

All three take a bare `Cell`, the `MoveTo` precedent — `docs/07`'s
"`HoldArea`/`AssaultArea`" naming means "an area the player designates by
cell," not a new region/rectangle type (`Scenario.Area` is a single-cell
marker for exactly this reason already).

### Decision C — `Hold` needs no new `Commitment` case — CONFIRMED

`Commitment.ofAgent`'s existing first match arm (`Some o, Some Accepted,
Some target -> Moving {...}`) already matches on disposition/destination
alone, not `order.Intent` — so if `Hold`'s Appraisal-accepted branch writes
a `Destination` exactly as `MoveTo` does, a `Hold` order automatically
becomes `Moving` while en route and falls to bare `Holding` on arrival,
with zero `Commitment.fs` changes. `docs/05` section 9's own sketch
(`Holding of HoldCommitment`) is not built: nothing behaviourally
distinguishes an ordered hold from an idle agent once arrived (the
`MoveTo`-already-collapses-into-bare-`Holding` precedent, TASK-030), so a
payload case would carry no information a diagnostic overlay couldn't
already show via `Order`/`Disposition` directly — `AGENTS.md` "do not build
speculative type machinery."

### Decision D — Hold's "may choose nearby cover" is real, not a no-op — CONFIRMED

Stage 2 for a `Hold` order does not path straight to `area`: it evaluates
every cell within `HoldConfig.CoverSearchRadius` Chebyshev cells of `area`
(including `area` itself) against the *already-existing* `Appraisal.
cellPressure` threat-pressure function (the same one `routeExposure` sums
over a route), picks the minimum-pressure cell, ties broken by nearest to
`area` then ascending `(X, Y)`, and paths there instead. Reuses machinery
that already exists rather than inventing a new geometric heuristic; a
scenario with no known threats picks `area` itself (every cell scores 0).
New `Appraisal.bestCoverNear` pure function.

### Decision E — `Withdraw` gets its own `Commitment.Withdrawing` case and a resolve bonus — CONFIRMED

`WithdrawCommitment = { Command: CommandId; Target: Cell }`, the
`MoveCommitment` shape, matching `docs/05` section 9's own sketch exactly
(unlike Hold, this one is worth a distinct case: a labelled "withdrawing"
state is genuinely different UI information from "moving," and it backs a
real behavioural difference — the resolve bonus below). Stage 4 for a
`Withdraw` order adds a flat `AppraisalConfig.WithdrawResolveBonus` to the
threshold (comparable in scale to `RiskAggressive`/`UrgencyImmediate` = 20)
— `docs/05` section 4's "may receive priority under high suppression" read
narrowly as *appraisal* priority (an agent breaking contact should not be
blocked by the very exposure it is retreating through), not as a new
automatic interrupt-priority trigger (no autonomous "start withdrawing
under suppression without being ordered" decision exists or is built here
— that is Hostile-doctrine-shaped work, out of scope, the B-022 precedent).
On arrival, falls to bare `Holding` exactly like `Moving` (no special
fulfilment logic).

### Decision F — `Assault` gets a stricter appraisal margin and the full staged FSM — CONFIRMED

`AssaultConfig.ResolvePenalty` (a flat subtraction from the stage-4
threshold, comparable in scale to `AppraisalConfig.SuppressionBandPenalty`
= 30) realises `docs/05` section 4's "more demanding than Move, receives
stricter appraisal." Dave chose the full staged FSM from `docs/05` section
10's example over a thin move-and-auto-engage executor (`AskUserQuestion`).
Realised as four `AssaultStage` values, each a **pure per-tick derivation**
from `(Position, Target, WorldState.TacticalKnowledge, SuppressionBand)` —
no new stored per-tick counter, the `Commitment` "derived, not stored"
precedent extended one level deeper:

- **`ApproachingStart`**: `Chebyshev(Position, Target) >
  AssaultConfig.StartRange` — closing distance under ordinary Navigation,
  no gating.
- **`AwaitingSupport`**: within `StartRange` of `Target`, and at least one
  known threat contact sits within `AppraisalConfig.ThreatEngagementRange`
  of `Target` with its own `SuppressionBand` **not** latched. Realises
  "wait for required support, if any" using machinery that already exists
  (`SuppressionBand`, the identical hysteresis latch `Suppress`/TASK-037
  already reads) rather than a new "support" bookkeeping concept.
  `commitmentAndLocalAction` freezes the agent's `Destination` to `None`
  for this tick only while in this stage (Decision G) — Navigation simply
  sees no destination and does not move it. **No timeout** (Decision H): if
  the player never suppresses the blocking threat, the assault stalls
  indefinitely — a deliberate, legible, reversible outcome (the product
  thesis this whole vertical slice exists to prove), not a bug needing a
  stored wait-counter.
- **`Advancing`**: within `StartRange`, no unsuppressed threat blocking —
  ordinary Navigation resumes ("cross danger area" needs no special
  handling: the already-automatic, already-symmetric `Combat` phase
  engages any visible hostile along the way exactly as it does for any
  other commitment).
- **`ClearingThreat`**: `Position = Target` and at least one known,
  currently-alive threat contact sits within `AssaultConfig.ClearRadius` of
  `Target`. The agent holds at the target; `Combat`'s existing automatic
  engagement (unchanged) fires at any visible defender. Once no such
  contact remains, the order is fulfilled (Decision I) — "report complete"
  is the existing `CommitmentCompleted` event, no new event type.

### Decision G — `commitmentAndLocalAction` freezes `Destination` during `AwaitingSupport`, not a `navigationAndMovement` change — CONFIRMED

Considered adding a new `MoveOutcome` case inside `navigationAndMovement`'s
Pass-1/2/3 pipeline (contention resolution, the vacation-chain fixpoint —
TASK-017/018/022's ~450-line accretion) and rejected it: `Destination`
itself is already ordinary per-tick canonical state that
`commitmentAndLocalAction` is documented to own ("the executor chooses the
next finite action within that commitment"). Setting it to `None` for one
tick while `AwaitingSupport`, then back to `Some Target` once the stage
clears, produces the identical "the agent does not move this tick" outcome
through the existing, unchanged, already-tested Navigation pipeline — zero
new movement-resolution code, zero new interaction with rival-cell
contention or the vacation chain.

### Decision H — Assault `fulfilled` check gains a clear-condition clause — CONFIRMED

`commitmentAndLocalAction`'s existing `MoveTo` fulfilled test
(`Destination = None && Position = target`) gains, for `Assault`, one more
clause: no living known threat within `ClearRadius` of `target`. An agent
sitting at the target with a defender still alive and known nearby is
`ClearingThreat`, not fulfilled — `Order`/`Disposition` stay in place, no
`CommitmentCompleted` yet.

### Decision I — `Commitment.ofAgent` signature grows to take world context — CONFIRMED

Only two real call sites exist (`Simulation.combat`'s `Suppressing`-pin
check, `Diagnostics.commitmentOverlays`) plus this task's new
`commitmentAndLocalAction` use — bounded blast radius. New signature:
`terrain -> threats: Contact[] -> suppressedThreats: AgentId[] -> position:
Cell -> order -> disposition -> destination -> Commitment`. `combat` and
`commitmentAndLocalAction` already have `Terrain`/`TacticalKnowledge` in
scope; `suppressedThreats` is the identical `SuppressionBand`-latched-id
projection `Simulation.appraisal`/`Diagnostics.orderAppraisalOverlays`
already compute (this tick's already-finalised bands, since
`commitmentAndLocalAction`/`combat` both run after `appraisal`).

### Decision J — ammo model: finite magazine + reserve, automatic reload, no cooldown-only mode — CONFIRMED

`AskUserQuestion`, Dave chose reload-from-stock over cooldown-between-
magazines. New `AmmoState = Ready of magazine: int * reserve: int |
Reloading of reserve: int * ticksRemaining: int` (`VitalStatus`'s "a single
field rather than a separate flag" idiom — a reloading weapon's magazine is
definitionally empty, so there is no `Magazine: int` to go stale in the
`Reloading` case). New `Ammo.fs` leaf (`Suppression.fs`/`Casualty.fs`
precedent): `canFire`, `fire` (decrements the magazine by one, total, only
meaningful when `canFire`), `tick` (state-consequences step: `Ready(0,
reserve>0) -> Reloading(reserve, AmmoConfig.ReloadTicks)`; `Reloading`
counts down to `Ready(min MagazineSize reserve, reserve - refill)`;
anything else unchanged), `resupply` (-> `Ready(MagazineSize,
ReserveStart)`, unconditional). `AmmoConfig`: `MagazineSize = 30`,
`ReserveStart = 90`, `ReloadTicks = 30`.

### Decision K — ammo applies to all combat fire, not only sustained Suppress — CONFIRMED

`AskUserQuestion`, Dave chose the broader scope over the backlog row's
narrower literal wording. Every `ShotFired` — automatic engagement,
`Suppress`, and `Assault`'s own auto-engagement alike — draws from the
identical `AgentState.Ammo`. `Combat`'s per-shooter loop gates firing on
`Ammo.canFire`; an agent with an empty magazine and no reserve (or
mid-reload) simply does not fire that tick — no event, the existing
"`Combat` only emits `ShotFired` when an engagement actually occurs"
precedent.

### Decision L — stage-2 ammo check on `Suppress`/`Assault` only, not universal — CONFIRMED

New `DecisionReason.InsufficientAmmunition`: `Unable` if `Ammo = Ready(0,
0)` (truly empty, not merely reloading or low) at appraisal time. Checked
only for `Suppress` and `Assault` — the two intents that explicitly plan to
initiate fire — not `MoveTo`/`Hold`/`Withdraw` (an unarmed agent can still
walk, hold ground, or retreat; refusing movement for empty ammo would be
wrong). Distinct from `CriticallyWounded`'s whole-order short-circuit,
which really does apply to every intent.

### Decision M — resupply mechanic: a new authored `ResupplyAreas` content type — CONFIRMED

`AskUserQuestion`, Dave chose to design resupply now rather than defer it
(reversing the memory note's "don't lock in prematurely" caution, now that
the ammo model itself is being built rather than merely anticipated). New
`Scenario.Area[]`-shaped `ResupplyAreas` (the `ObjectiveAreas`/
`ExtractionAreas` precedent exactly — `RawScenario.ResupplyAreas:
RawArea[]`, folded into the same shared `AreaId` uniqueness/bounds
validation those two already use). `ScenarioContent.Version` `2 -> 3`
(shape change, not a validation-rule change alone). Unlike
`ObjectiveAreas`/`ExtractionAreas` (still "nothing reads them yet," B-032),
this is the **first** authored area type an actual phase consumes:
`WorldState.ResupplyAreas: Cell[]`, carried by `World.ofScenario`, **static
authored data excluded from `Canonical.encode`** (the `Terrain`/
`CommunicationAvailable` precedent — ADR-0002 amendment, no new ADR). A
`stateConsequences` check: any agent whose `Position` matches a resupply
cell and whose `Ammo` is not already `Ready(MagazineSize, ReserveStart)`
is set to full and emits `AgentResupplied` — instant, not over several
ticks (no "resupply duration" concept exists or is scoped here), takes
priority over an in-progress reload the same tick (an agent standing on
the cache does not need to wait out a reload it was already mid-way
through).

### Decision N — new events: `ReloadStarted`, `ReloadCompleted`, `AgentResupplied` — CONFIRMED

`stateConsequences` emits `ReloadStarted(agent)` on the `Ready(0,_) ->
Reloading` transition and `ReloadCompleted(agent)` on `Reloading -> Ready`
— the `AgentIncapacitated`/`AgentDied` precedent (emitted once, on the
transition, not every tick a state persists). `AgentResupplied(agent)`
only when a resupply actually changed the agent's `Ammo` (not every tick
an already-full agent happens to stand on the cell — the same "no-op
every-tick spam" avoidance `Suppression`/`Stress` decay already follow by
using silent decay instead of an event).

### Decision O — new sparse `Overlay.AgentAmmo` — CONFIRMED

One per agent whose `Ammo` is not `Ready(MagazineSize, ReserveStart)` (the
`AgentSuppression`/`AgentStress` "only report from default" precedent) —
`AgentId * Cell * magazine: int * reserve: int * reloading: bool`. Rendered
in `Ascii`/`Svg`/`Html` (`DiagnosticRender.fs`) and `CommandDemoScene.fs`'s
existing `[dev]` HUD line (F1 overlay only — this is developer detail, not
promoted to always-on player-facing rendering the way B-057/TASK-046's
`FireLine`/`AgentVitals` were; a player-facing ammo readout is a plausible
future client task, not scoped here).

### Decision P — `Canonical.FormatVersion` bump — CONFIRMED

`AgentState.Ammo: AmmoState` is genuine per-tick canonical state (changes
from `Combat`/`stateConsequences`, not recomputable from `Position` alone —
the `Suppression`/`Vitals` precedent exactly): `Canonical.FormatVersion`
`10 -> 11`, full re-pin. `PlayerIntent` also gains three cases inside the
already-canonical `AgentState.Order` — no second bump, the TASK-037
precedent of one bump covering every new case/field a single task adds.
Every existing corpus entry is behaviour-neutral (none issues
`Hold`/`Assault`/`Withdraw`, and starts every agent at full ammo, so no
`ShotFired` tick's hit/miss trace changes); the new corpus entries below
carry the only genuine new non-default state.

## Allowed scope

- `src/CommandoWar.Sim/Domain.fs` (`PlayerIntent.Hold/Assault/Withdraw`,
  `DecisionReason.InsufficientAmmunition`, `AmmoState`,
  `AgentState.Ammo`, module doc updates).
- `src/CommandoWar.Sim/Appraisal.fs` (`bestCoverNear`, `Hold`/`Assault`/
  `Withdraw` branches in `appraise`, `resolveThreshold`'s new
  Assault/Withdraw modifiers, ammo stage-2 check).
- `src/CommandoWar.Sim/Commitment.fs` (`Withdrawing`, `Assaulting`,
  `AssaultStage`, `Commitment.ofAgent`'s new signature).
- `src/CommandoWar.Sim/Ammo.fs` (new leaf: `AmmoConfig`, `canFire`, `fire`,
  `tick`, `resupply`).
- `src/CommandoWar.Sim/Combat.fs` (ammo gate before firing).
- `src/CommandoWar.Sim/Scenario.fs` (`ResupplyAreas`, `ScenarioContent.
  Version` 3, validation).
- `src/CommandoWar.Sim/Simulation.fs` (`appraisal`, `commitmentAndLocalAction`
  — Assault stage freeze/fulfilment, ammo `stateConsequences` tick/reload/
  resupply, `World.ofScenario` carrying `ResupplyAreas`, `combat`'s ammo
  gate and `Commitment.ofAgent` call-site update).
- `src/CommandoWar.Sim/Canonical.fs` (`FormatVersion` bump, new encode arms).
- `src/CommandoWar.Sim/Events.fs` (or wherever `DomainEvent` lives:
  `ReloadStarted`, `ReloadCompleted`, `AgentResupplied`).
- `src/CommandoWar.Sim/Diagnostics.fs` (`AgentAmmo` overlay,
  `Commitment.ofAgent` call-site update).
- `src/CommandoWar.Headless/{Corpus.fs, DiagnosticRender.fs,
  DemoScenario.fs, AppraisalDemo.fs}` (new corpus entries, exhaustive-match
  arms, `ResupplyAreas` field on every `RawScenario` builder).
- `tests/CommandoWar.Sim.Tests/*.fs` (new facts; full re-pin ripple).
- `content/replays/*`, `content/diagnostics/*` (full re-pin; new goldens).
- `src/CommandoWar.Client.Godot/Core/{RenderShared.fs, IClientScene.fs,
  CommandDemoScene.fs}` (exhaustive-match arms for the new `Commitment`/
  `Overlay` cases; the ammo dev-HUD line; pinned `--selfcheck` hashes if a
  demo scenario's trace moves).
- `docs/04`, `docs/05`, `docs/06`, `docs/07`, `docs/11`, `docs/12`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- No client-facing UI for issuing `Hold`/`Assault`/`Withdraw` from
  `CommandDemoScene` (the TASK-037 `Suppress`-order precedent — sim-side
  proof via a corpus entry only; a player-facing order-type picker is
  separate future client work).
- No player-facing ammo readout (B-057/TASK-046 precedent stays F1-only
  developer detail for this task, per Decision O).
- No Hostile-side autonomous withdraw-under-suppression or
  assault-doctrine behaviour (B-022's remaining Hostile-doctrine items,
  untouched).
- No `Delayed`/`ResumeCondition` disposition, no stage-5 `Adapted` reroute
  (B-018 follow-up, untouched).
- No resupply *duration* (a multi-tick "rearming" state) — resupply is
  instant on arrival, per Decision M.
- No mission/objective evaluation consuming `ResupplyAreas`,
  `ObjectiveAreas`, or `ExtractionAreas` beyond this task's own resupply
  mechanic (B-032, untouched).
- Nothing under `src/_scratch`, `bench/`, `content/benchmarks/BASELINE.md`,
  or any Godot spike scene.

## Considered and rejected

**A stored per-agent `AwaitingSupportTicks` counter with a timeout**:
rejected per Decision F/H — Dave explicitly chose "wait indefinitely,"
which needs no new stored state at all, only a pure per-tick derivation.

**Ammo scoped to `Suppress` fire only** (the backlog row's literal
wording): rejected per Decision K — Dave chose the broader, more
consistent "every `ShotFired` draws ammo" scope.

**A cooldown-only ammo model (no magazine/reserve split)**: rejected per
Decision J — Dave chose reload-from-stock, which needs the resupply
question (Decision M) answered as a consequence; a cooldown model would
have needed no resupply mechanic at all.

**A new `HoldCommitment` payload case, matching `docs/05`'s literal type
sketch**: rejected per Decision C — nothing behaviourally distinguishes an
ordered hold from idle once arrived; a payload case would carry no
information a diagnostic overlay cannot already read from `Order`/
`Disposition` directly.

**A new corpus entry proving the Assault FSM (or the ammo/reload/resupply
cycle) end to end**: considered, given every other major mechanism this
project has landed got one (`suppress-relieves-exposure`,
`casualties-succession-and-squad-failure`). Rejected for this task: a full
live-fire trace exercising all four `AssaultStage`s in one scripted run
(reach `AwaitingSupport`, issue a `Suppress` order against the blocking
threat to unstick it, resume to `ClearingThreat`, then clear it via
automatic engagement) needs a new golden diagnostic pair and README entry
on top of the `.cwreplay`/`.md` pair — real added weight for a mechanism
already covered precisely by 17 direct `SimulationTests` facts on the pure
`assaultStage` derivation and the phase-level executor/appraisal behaviour
(the TASK-032/033 "proof is via `SimulationTests` facts directly on the
phase" precedent, not every task needs a corpus entry). Revisit if a later
task (e.g. B-032's demolition scenario) wants a scripted Assault/ammo
narrative for its own evidentiary purposes.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0` before and after.
- `dotnet build` the Godot client `.slnx` (Debug and Release): `0/0`.
- `dotnet test CommandoWar.slnx -c Release`: full re-pin ripple; new
  `SimulationTests` facts for each new order's appraisal/commitment/
  executor branch, the Assault-stage derivation (all four states), ammo
  fire/reload/resupply transitions, and the new reappraisal-irrelevant
  (ammo/stage changes do not themselves trigger reappraisal) boundary.
- `cwheadless corpus`: full count/`N`, `--regenerate` twice byte-identical.
- `cwheadless fixture`: format bump reflected, event count unchanged
  (spike fixture is enemy-free and issues no new order type).
- `cwheadless replay-file content/replays/envelope-full.cwreplay`:
  unchanged (enemy-free scenario, no new order type).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`: `FSharp.Core` only.
- Source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`: clean.
- Godot `--selfcheck` for both scenes (`SnapshotDemo.tscn`, `CommandDemo.tscn`)
  through the real Godot 4.7.2 editor: hash unchanged (this task adds no
  new demo-scenario order issuance to either scripted scene) unless
  `DemoScenario.fs`'s `RawScenario` literal needed a `ResupplyAreas` field
  addition that also changes authored content (expect unchanged: an empty
  array is the neutral default).
- `git status --porcelain`: matches this task file's "Allowed scope".

## Documentation updates

- This task file's Outcome/Review sections.
- `docs/11_BACKLOG.md`: B-030 row `proposed -> review`/`done`.
- `docs/07_VERTICAL_SLICE.md` section 4/5: record `HoldArea`/`AssaultArea`/
  `WithdrawTo` and the ammo model as realised.
- `docs/05_COMMAND_AND_AGENT_AI.md` sections 4, 7, 8, 9, 10, 11 (the
  now-realised order vocabulary, commitment cases, and finite executor
  example).
- `docs/04_SIMULATION_SPEC.md` sections 12.5, 12.6, 12.8, 12.9 (ammo gate,
  resupply consequence phase), 21 (`ScenarioContent.Version` 3).
- `docs/12_PROGRESS_LEDGER.md` + `docs/ledger/` detail file.
- `PROJECT_STATE.yaml`.

## Rollback or removal

Every change is additive to existing exhaustive matches (`Canonical.
encode`, `Commitment.ofAgent`, `Appraisal.appraise`, the executor/combat/
state-consequences phases) plus new corpus entries and one new authored
content field with a neutral (empty-array) default — revertible with `git
revert` in one step; no migration of existing authored content beyond the
`ScenarioContent.Version` bump every `RawScenario` builder already has to
absorb field-by-field (the TASK-010 terrain-layer-addition precedent).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave
- Accepted: pending. Godot's `--selfcheck` pinned hashes (SnapshotDemo
  `0x8E93B48D07AE9CBD`, CommandDemo `0xE661187DE95E92E6`,
  `AppraisalDemoScene`'s exposed-approach `0x194805888CBE240D`) still need
  independent confirmation on Dave's machine (no Godot install in the
  implementing environment) — flagged, not a blocker for acceptance.
