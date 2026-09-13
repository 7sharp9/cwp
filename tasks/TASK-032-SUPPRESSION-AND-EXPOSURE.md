# TASK-032: Suppression and exposure model

Status: draft (central decisions A–H confirmed with Dave 2026-09-14 before
the phase bodies)
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises backlog B-020
Size: M

## Objective

Turn the **State consequences phase (12.9)** — currently a no-op — into a
real phase that decays suppression, and make the **Combat phase (12.8)**
create it: any qualifying shot (hit or miss) raises the target's
`AgentState.Suppression`, mitigated by the same directional `Terrain.cover`
geometry Combat already uses for hit chance ("the exposure model" — the same
cover-facing calculation applies to both how likely a shot is to land and how
much being shot at rattles the target). This is deliberately the *mechanic*
slice only — the raw, decaying `Suppression` value and its creation/decay
rules — with **no consequence yet** to order appraisal, reappraisal,
movement, or executor behaviour. Those are backlog B-021 ("stress,
discipline, trust, and bounded reappraisal", which explicitly owns the
"suppression-band reappraisal trigger") and later items.

## Why this task exists

- `docs/04_SIMULATION_SPEC.md` section 12.8 lists "create suppression
  independent of a hit where intended" as a still-unrealised Combat bullet;
  section 12.9 is entirely unrealised ("update suppression decay; update
  stress from recent events; apply deaths and incapacitation; update command
  succession"). This task realises the suppression half of both.
- `docs/05_COMMAND_AND_AGENT_AI.md` section 8: "Suppression: immediate effect
  of hostile fire and impacts. It reduces action effectiveness, raises
  assault pressure, and may trigger taking cover." This task builds the
  "immediate effect" value only; "reduces action effectiveness" / "raises
  assault pressure" / "may trigger taking cover" are not modelled by any
  system yet and stay out (no backlog number assigned to them individually —
  they arrive with B-021's resolve-threshold wiring and later executor work).
- `docs/05` section 5, stage 3 ("current suppression") and stage 4 ("current
  suppression" in the resolve threshold) name suppression as a future
  appraisal input; the same section already says "are B-019 / B-020 / B-021"
  for the stage-3 list — this task is the B-020 slice, not the wiring into
  Appraisal (B-021's job).
- `docs/07_VERTICAL_SLICE.md` section 5 lists "suppression" as a required
  system distinct from "hitscan small-arms combat" (TASK-031, done) and
  "stress, discipline, and leader trust" (B-021, not started); section 8's
  canonical refusal sequence step 5 ("order another fireteam to suppress the
  machine-gun position") needs a real `Suppression` value to exist before a
  `Suppress` order (B-030) can act on it.
- Backlog B-021 (reappraisal), B-023 (canonical refusal sequence), and B-030
  (suppress/assault executors) all depend on this landing first.

Depends only on TASK-031 (backlog B-019, hitscan combat, done, on `main`),
which this task's `combat` phase edit builds directly on top of.

## Required reading

Read in this order; verify each path and inspect the source before trusting a
filename (`AGENTS.md`).

1. `PROJECT_STATE.yaml`, `AGENTS.md`.
2. `docs/ledger/2026-09-13-TASK-031-hitscan-combat.md` — the Combat phase and
   `Combat.fs` this task extends; its own "deliberately absent" list names
   suppression as B-020.
3. `docs/04_SIMULATION_SPEC.md` sections 11 (agent state — "suppression" is
   listed, not yet realised), 12.8 (Combat — "create suppression independent
   of a hit" bullet), 12.9 (State consequences — "update suppression decay",
   the no-op this task realises), 17 (state hashing — the `Canonical`
   version-history comment block this task extends), 20 (invariants).
4. `docs/05_COMMAND_AND_AGENT_AI.md` section 5 (stage 3 / stage 4 — where
   "current suppression" will eventually feed appraisal, B-021's job, not
   this task's), section 8 ("Minimal psychological model" — the Suppression
   subsection this task partially realises).
5. `docs/07_VERTICAL_SLICE.md` section 5 (required systems — "suppression" as
   its own line), section 8 (canonical refusal sequence step 5).
6. `src/CommandoWar.Sim/Combat.fs` — `CombatConfig`, `Combat.hitChance`
   (the cover-mitigation geometry this task reuses for suppression gain),
   `Combat.chooseTarget`. This task does **not** change this file.
7. `src/CommandoWar.Sim/Simulation.fs` — `combat` (the phase this task edits
   to write `AgentState.Suppression` on a qualifying target), `runPhase`
   (`StateConsequences` currently in the no-op arm with `Mission`).
8. `src/CommandoWar.Sim/Appraisal.fs` — `attackDirection` (public since
   TASK-031; reused again here, unchanged).
9. `src/CommandoWar.Sim/Domain.fs` — `AgentState`, `Agent.create` (the
   `Progress` precedent for a field that always starts at 0 with no
   scenario-authored override, as opposed to the `Discipline` /
   `CommunicationAvailable` precedent for authored static data).
10. `src/CommandoWar.Sim/Canonical.fs` — `FormatVersion` version-history
    comment block, `writeAgent` (the `Order`/`Disposition` TASK-028 addition
    is the direct precedent for a new genuinely-dynamic per-tick field).
11. `src/CommandoWar.Sim/Diagnostics.fs` — the `Overlay` doc comment's
    realised-systems list, `AgentCommitment` / `KnownContact` (the two
    "standing canonical state, both `frame` and `frameOf` derive it"
    precedents this task's overlay follows), `UndeliveredOrder` (the
    "omit when not applicable" sparse-overlay precedent this task follows
    instead, since suppression is usually 0).
12. `src/CommandoWar.Headless/{DiagnosticRender.fs, AppraisalDemo.fs}` — the
    `AgentCommitment` / `FireLine` Ascii text line, Svg branch, and
    exhaustive-match precedents (including the disposable `AppraisalDemo`
    `unhandled` bucket — the TASK-030/031 lesson: it has no
    `TreatWarningsAsErrors` guard, check it by hand).
13. `tests/CommandoWar.Sim.Tests/{SimulationTests.fs, DeterminismPropertyTests.fs,
    DiagnosticsTests.fs}` — the `appraisalCaseGen` generator precedent
    reused again; every exhaustive `Overlay` match site (`FireLine _ ->`
    sites are the direct precedent for where an `AgentSuppression` arm is
    needed).
14. `content/replays/open-engagement.md` — the corpus entry this task
    re-pins in place (both agents in weapon range and line of sight from
    tick 1, so the first shot — and the first suppression gain — lands on
    tick 1 itself).

## Dependencies

- TASK-031 (hitscan combat, backlog B-019, done, on `main`) — `ShotFired`
  and the Combat phase this task's suppression gain hooks into.

## Central decisions (confirmed with Dave 2026-09-14 before the phase bodies)

### Decision A — mechanic only; no consequence to Appraisal, reappraisal, movement, or executor behaviour — CONFIRMED

Confirmed directly with Dave before drafting: `AgentState.Suppression`
becomes real per-tick state that Combat creates and the new State
consequences phase decays. It does **not** feed `AppraisalConfig`'s stage-3
exposure sum or stage-4 resolve threshold, does **not** trigger a
reappraisal, does **not** slow movement or change executor behaviour, and
does **not** trigger any "take cover" response — none of that machinery
exists yet. Backlog B-021 ("stress, discipline, trust, and bounded
reappraisal") is explicitly where suppression starts affecting decisions;
building that here would duplicate scope already assigned to it (the
TASK-031 Decision C precedent: "a hit has no consequence yet").

### Decision B — suppression created in Combat, independent of a hit, mitigated by directional cover — CONFIRMED

`docs/04` section 12.8 already mandates "create suppression independent of a
hit where intended" — a miss still suppresses, just less than a hit. The
"exposure model" in the backlog title is not a new subsystem: it means
suppression gain scales with the same directional-cover geometry Combat
already uses for hit chance (`Terrain.cover` on the edge the shot arrives
from, via the already-public `Appraisal.attackDirection`) — more cover means
less suppression from the same shot, exactly as it means a lower hit chance.

```fsharp
[<RequireQualifiedAccess>]
module SuppressionConfig =
    /// Suppression scale, matching `Contact.Confidence` / `CombatConfig`'s
    /// `0..1000` — headroom for B-021's future suppression bands.
    [<Literal>]
    let MaxSuppression = 1000

    /// Suppression gained by the target of a hit, before cover mitigation.
    [<Literal>]
    let GainOnHit = 400

    /// Suppression gained by the target of a miss, before cover mitigation
    /// (docs/04 section 12.8: "independent of a hit").
    [<Literal>]
    let GainOnMiss = 150

    /// Suppression-gain reduction per authored `Terrain.cover` level on the
    /// edge the shot arrives from (the `CombatConfig.CoverMitigationPerLevel`
    /// precedent, same units, independently tunable — cover softens fire's
    /// psychological effect by less than it softens the physical hit chance).
    [<Literal>]
    let CoverMitigationPerLevel = 100

    /// Flat suppression decay applied every tick in State consequences,
    /// floored at 0 ("decays when safe", docs/05 section 8).
    [<Literal>]
    let DecayPerTick = 50
```

### Decision C — suppression decays every tick in the (currently no-op) State consequences phase — CONFIRMED

`docs/04` section 12.9's first bullet, "update suppression decay", is this
phase's job. A flat per-tick integer decay, floored at 0, applied
**unconditionally** to every agent regardless of whether it was shot at that
tick — a continuous stream of fire nets positive since `GainOnHit` /
`GainOnMiss` both exceed `DecayPerTick`, and an agent left alone decays to 0
over several ticks. Runs after Combat in `Phases.order`, so a same-tick hit's
gain and that tick's decay both apply once, in that order — deliberately
simple, no "decay only when not hit this tick" branch.

### Decision D — new leaf `src/CommandoWar.Sim/Suppression.fs` — CONFIRMED

`SuppressionConfig` + `Suppression.gain` / `.raise` / `.decay` — pure,
total, `Terrain` / `Appraisal` / `Domain` only, no event emission, no
mutation (the `Combat.fs` precedent: the phase function in `Simulation.fs`
mutates and draws; the leaf only computes). Keeps `Combat.fs` itself
completely unchanged — its own doc comment already names suppression as
"deliberately absent (B-020)" and this task realises it alongside, not
inside, that leaf (the `Commitment.fs`-beside-`Appraisal.fs` precedent: a new
concern gets its own leaf even when it reads a sibling's public helper).

```fsharp
[<RequireQualifiedAccess>]
module Suppression =
    /// Suppression gained by one qualifying shot, before it is added to the
    /// target's current value. Pure, total. Reuses `Appraisal.attackDirection`
    /// for the cover-facing edge — the identical geometry `Combat.hitChance`
    /// uses.
    let gain (terrain: Terrain) (shooter: Cell) (target: Cell) (hit: bool) : int =
        let cover = Terrain.cover terrain target (Appraisal.attackDirection shooter target)
        let baseGain = if hit then SuppressionConfig.GainOnHit else SuppressionConfig.GainOnMiss
        baseGain - cover * SuppressionConfig.CoverMitigationPerLevel |> max 0

    /// Adds `delta` to `current`, clamped at `MaxSuppression`. Separate from
    /// `gain` so two same-tick shots against one target (two different
    /// shooters) compose by calling this twice.
    let raise (current: int) (delta: int) : int =
        current + delta |> min SuppressionConfig.MaxSuppression

    /// One tick's flat decay, floored at 0.
    let decay (current: int) : int =
        current - SuppressionConfig.DecayPerTick |> max 0
```

Inserted in the fsproj after `Combat.fs`, before `Commands.fs`.

### Decision E — `AgentState.Suppression: int` is genuine canonical state; `Canonical.FormatVersion` bumps 4 -> 5 — CONFIRMED

Unlike `Discipline` / `CommunicationAvailable` (static authored data, excluded
from `Canonical.encode`), `Suppression` changes every tick from gameplay
events and cannot be recomputed from `Position` alone — the `Order` /
`Disposition` (TASK-028) precedent, not the `Discipline` one. Under the
ADR-0002 amendment it goes into `Canonical.encode`, `writeAgent` gains it
after `Disposition`, and `Canonical.FormatVersion` bumps **4 -> 5** with a
full re-pin. `Agent.create` defaults it to `0` with **no scenario-authored
override** (the `Progress` precedent, not the `Discipline` one — no
`RawDeployment` / `Deployment` field, no `ScenarioError` case; every agent
starts unsuppressed). Every scenario with no combat (everything except
`open-engagement`, `perception-contact`, `exposed-approach`, `demo.html`)
stays at `Suppression = 0` at every checkpoint — a byte-layout-only re-pin,
not a behaviour change (tick counts and event counts unchanged everywhere,
the required stop-and-report check); the four combat-bearing entries gain
genuine new per-tick values on top of their TASK-031 re-pin.

### Decision F — no new event type; a new sparse `Overlay.AgentSuppression` — CONFIRMED

Suppression's rise is a deterministic function of the already-emitted
`ShotFired` (recomputable via `Suppression.gain` from the shooter/target
cells and the `hit` field); its decay is silent, the `Progress` precedent
(sub-cell movement progress changes every tick with no event of its own).
No new `EventBody` case, no `Events.fs` change, no `eventMarker` arm.

New `Overlay.AgentSuppression of agent: AgentId * at: Cell * suppression:
int`. Unlike `AgentCommitment` (emitted for **every** agent unconditionally,
since `Holding` is itself meaningful state), this follows the
`UndeliveredOrder` / `KnownContact` sparse-overlay shape: one entry only for
an agent with `Suppression > 0`, ascending by agent id — suppression is 0 for
every agent in every non-combat scenario, and an unconditional per-agent
overlay would add a `Suppression = 0` line to every agent in every existing
diagnostic golden across the whole corpus for no information. Standing
canonical state (not a this-tick event), so both `Diagnostics.frame` and
`Diagnostics.frameOf` derive it — the `KnownContact` / `AgentCommitment`
precedent, not the `FireLine` / `UndeliveredOrder` `frameOf`-only one.

### Decision G — no new corpus entry; decay and clamping proved on the pure functions — CONFIRMED

`open-engagement`'s two agents are already in weapon range and line of sight
from tick 1 with no orders (TASK-031 Decision H), so the first shot — and
the first suppression gain — lands on tick 1 itself; its existing golden set
naturally re-pins to show a real `Suppression` value with no new scenario
needed. `perception-contact` and `exposed-approach` gain real values from the
ticks TASK-031 already identified as their first-combat tick; every other
entry, the fixture, and `envelope-full` are byte-layout-only re-pins per
Decision E. Decay (`Suppression.decay`, floored at 0) and multi-shot
accumulation/clamping (`Suppression.raise`, capped at `MaxSuppression`) are
proved directly on the pure functions (no simulation run needed — the
`Combat.hitChance` monotonicity precedent), plus one hand-built
`SimulationTests` fact over consecutive ticks (a hit tick followed by a quiet
tick shows the exact rise-then-decay arithmetic end to end).

### Decision H — no ADR — CONFIRMED

Same shape as TASK-031: a new leaf, a new genuinely-dynamic canonical field
added via the existing ADR-0002 amendment, no new dependency. Design recorded
in this task file and the ledger.

## Phase bodies

### `combat` (12.8) — edited, header gains a "Suppression realised by TASK-032" line

Unchanged wiring (candidate resolution, target selection, hit-chance draw,
`ShotFired` emission) plus one new step per qualifying shot: raise the
target's `Suppression`. The phase now writes `AgentState` (it previously
wrote none), so it copies `s.Agents` like every other writing phase
(`appraisal`, `commitmentAndLocalAction` precedent) instead of aliasing it.

```fsharp
let private combat (s: StepState) =
    let terrain = s.Terrain
    let candidateSource = s.Agents // ascending by id; candidate lookup only, never mutated
    let agents = Array.copy s.Agents
    let mutable random = s.Random

    for shooter in candidateSource do
        let candidates =
            shooter.VisibleContacts
            |> Array.choose (fun id -> candidateSource |> Array.tryFind (fun a -> a.Id = id))

        match Combat.chooseTarget terrain shooter candidates with
        | None -> ()
        | Some target ->
            let chance = Combat.hitChance terrain shooter.Position target.Position
            let struct (draw, next) = RandomStream.next random
            random <- next
            let hit = (draw % 1000UL) < uint64 chance
            emit (ShotFired(shooter.Id, target.Id, hit)) s

            let gain = Suppression.gain terrain shooter.Position target.Position hit
            let idx = agents |> Array.findIndex (fun a -> a.Id = target.Id)
            let t = agents.[idx]
            agents.[idx] <- { t with Suppression = Suppression.raise t.Suppression gain }

    s.Agents <- agents
    s.Random <- random
```

Two shooters engaging the same target the same tick compose correctly: `t`
is read from `agents` (the working copy), not `candidateSource`, so the
second write sees the first shot's gain already applied.

### `stateConsequences` (12.9) — new phase, out of the no-op list

```fsharp
let private stateConsequences (s: StepState) =
    let agents = Array.copy s.Agents

    for i in 0 .. agents.Length - 1 do
        let a = agents.[i]

        if a.Suppression > 0 then
            agents.[i] <- { a with Suppression = Suppression.decay a.Suppression }

    s.Agents <- agents
```

`runPhase` — `StateConsequences -> stateConsequences s`; `Mission` stays the
sole remaining no-op arm.

## Diagnostics

`AGENTS.md`'s diagnostic-extension rule applies: new authoritative agent
state needs a diagnostic surface. Required:

- **`Overlay.AgentSuppression of agent: AgentId * at: Cell * suppression:
  int`** (Decision F) — derived by both `Diagnostics.frame` and `.frameOf`,
  one per agent with `Suppression > 0`, ascending by agent id.
- `DiagnosticRender.Ascii`: a `suppression (x,y): agent N  <value>/1000`
  overlay text line; the existing exhaustive filter arms (`sightRays`,
  `plannedPaths`) gain `| AgentSuppression _ -> None`.
- `DiagnosticRender.Svg`: a small red-tinted marker at the agent's cell's
  top-right corner (the `AgentCommitment` top-left / `OrderAppraisal`
  bottom-right precedent — top-right is the one unused corner), opacity
  scaled by `suppression / SuppressionConfig.MaxSuppression` so the golden
  visibly shows accumulation and decay across ticks.
- Every exhaustive `Overlay` match armed with the intended branch:
  `DiagnosticRender` (both `Ascii` filters, `Ascii` text, `Svg`),
  `AppraisalDemo.fs`'s `unhandled` bucket (predates this task; `exposed-
  approach` tick 1 has no combat yet so this arm should not change that
  test's fixed "three unhandled entries" count — verify this explicitly), and
  the test-project overlay matches (`DiagnosticsTests.fs` has no
  `TreatWarningsAsErrors` — check these by hand, the TASK-030/031 lesson).
  FS0025 sites + fixes listed in the ledger.
- Goldens: regenerate `open-engagement-tick-001.*` (now shows a real
  `AgentSuppression` overlay — the first shot and gain land on tick 1
  itself) + a hand-built `AgentSuppression` overlay unit test; the
  regeneration note in `content/diagnostics/README.md`.

## Event-trace and hash impact (Canonical.FormatVersion 4 -> 5, full re-pin)

- Every corpus entry, the fixture, and `envelope-full` re-pin (the format
  bump touches every `writeAgent` call). **Tick counts and event counts are
  unchanged everywhere** — no new event type, no new phase branch that can
  fail or diverge control flow — the required stop-and-report check.
- `open-engagement`, `perception-contact` (from its TASK-031 first-combat
  tick), and `exposed-approach` (from its TASK-031 first-combat tick) gain
  genuine new non-zero `Suppression` values on top of their byte-layout
  re-pin — real new behaviour, not just a version bump.
- `demo.html` (not a corpus entry) similarly gains real `Suppression` values
  from its TASK-031 first-combat tick (8) onward.
- Every other entry (nine of the twelve corpus entries, the fixture,
  `envelope-full`) has no agent that is ever shot at, so `Suppression` stays
  `0` throughout — byte-layout-only re-pin, confirmed by diffing every `.md`
  for a changed "Tick count" or "Domain events" line (none expected) versus a
  changed hash line (expected, every entry).

## Allowed scope

- `src/CommandoWar.Sim/Domain.fs` — `AgentState.Suppression: int`;
  `Agent.create` default `0`. No `RawDeployment` / `Deployment` field, no new
  `ScenarioError`.
- `src/CommandoWar.Sim/Suppression.fs` (**new leaf**, after `Combat.fs`) —
  `SuppressionConfig`; `Suppression.gain` / `.raise` / `.decay`. Pure, total,
  no event emission, no mutation.
- `src/CommandoWar.Sim/CommandoWar.Sim.fsproj` — the new `Compile Include`.
- `src/CommandoWar.Sim/Simulation.fs` — `combat` phase edit (copies
  `s.Agents`, writes the target's `Suppression`); new `stateConsequences`
  phase function; `runPhase` arm; header comments.
- `src/CommandoWar.Sim/Canonical.fs` — `writeAgent` gains `Suppression`;
  `FormatVersion` `4 -> 5`; version-history doc comment.
- `src/CommandoWar.Sim/Diagnostics.fs` — `Overlay.AgentSuppression`; `frame`
  / `frameOf` derivation; the `Overlay` doc comment.
- `src/CommandoWar.Headless/DiagnosticRender.fs` — new `Ascii` / `Svg`
  branches + filter arms.
- `src/CommandoWar.Headless/AppraisalDemo.fs` — one `AgentSuppression` arm
  added to its `unhandled` bucket (predates this task, does not render
  suppression).
- `content/replays/` — the full-corpus re-pin (Decision E/G); no new entry.
- `content/diagnostics/` — the re-pin of `open-engagement-tick-001.*`,
  `perception-contact-tick-005.*`, and `demo.html`; `README.md`.
- `tests/CommandoWar.Sim.Tests/` — see "Acceptance criteria".
- Docs — see "Documentation updates".

## Forbidden scope

- Any `AppraisalConfig` / `Appraisal.fs` read of `AgentState.Suppression`, any
  reappraisal trigger, any resolve-threshold change — B-021 (Decision A).
- Any movement-speed, action-effectiveness, or "take cover" behaviour change
  from being suppressed — not modelled by any system yet; no backlog number
  assigned.
- Any change to `Combat.fs` itself (Decision D) — the new leaf is additive
  alongside it.
- Ammunition, weapon readiness, wound, death, or casualties — B-031 and the
  still-unscoped ammo work (TASK-031 precedent, unaffected here).
- `Hold` / `Suppress` / `Assault` `PlayerIntent` cases, or any new order type
  — B-030 (this task builds the *effect* a future `Suppress` order will one
  day rely on, not the order itself).
- Enemy doctrine choosing targets differently based on suppression — B-022.
- Any `Sight.fs` / `Terrain.fs` / `Perception.fs` / `Pathfinding.fs` /
  `Appraisal.fs` change.
- A new corpus entry (Decision G).
- A `Canonical.FormatVersion` change beyond exactly `4 -> 5`, or any other
  unrelated `Canonical.encode` edit.
- Editing the client spikes, `src/_scratch`, `bench/`,
  `content/benchmarks/BASELINE.md`.
- An ADR (Decision H).

## Acceptance criteria

- [ ] `AgentState.Suppression: int`, defaulted to `0` in `Agent.create`, no
      scenario-authored override.
- [ ] `src/CommandoWar.Sim/Suppression.fs`: `SuppressionConfig` module
      literals; `Suppression.gain` / `.raise` / `.decay` — pure, total.
- [ ] `Combat.fs` unchanged; `Simulation.combat` copies `s.Agents` and writes
      the target's `Suppression` on every qualifying shot, composing
      correctly when two shooters hit the same target the same tick.
- [ ] `Simulation.stateConsequences` is a real phase, out of the no-op list;
      decays every agent's `Suppression` by `SuppressionConfig.DecayPerTick`,
      floored at `0`, every tick unconditionally.
- [ ] `SimulationTests` facts:
  - a hit raises the target's `Suppression` by exactly `Suppression.gain`
    recomputed from the post-tick cells and cover;
  - a miss also raises `Suppression`, by less than a hit does, all else
    equal (docs/04 section 12.8 "independent of a hit");
  - `Suppression.gain` strictly decreases as cover level increases (fixed
    hit/miss), floored at `0`;
  - two shooters hitting the same target the same tick compose (both gains
    applied, clamped at `MaxSuppression`);
  - a quiet tick after a hit shows the exact rise-then-decay arithmetic
    (hit tick's post-value, then one decay step);
  - an agent never shot at stays at `Suppression = 0` indefinitely;
  - two runs of the same world + commands emit byte-identical events,
    hashes, `Suppression` values, and draw counts.
- [ ] A `DeterminismPropertyTests` property (`MaxTest = 200`, reusing
      `appraisalCaseGen`): every agent's post-tick `Suppression` is
      reproducible from a fresh recompute — this tick's `ShotFired` events'
      `Suppression.gain` applied via `Suppression.raise`, then
      `Suppression.decay` — matching the actual post-tick value exactly.
- [ ] No new corpus entry (Decision G); `open-engagement`,
      `perception-contact`, and `exposed-approach` re-pin with genuine new
      `Suppression` values; every other entry, the fixture, and
      `envelope-full` re-pin as a byte-layout-only change (**tick counts and
      event counts unchanged everywhere** — verified by diffing every `.md`
      for a changed "Tick count" or "Domain events" line: none expected).
- [ ] `Canonical.FormatVersion` `4 -> 5`; `writeAgent` writes `Suppression`
      after `Disposition`; version-history doc comment added.
- [ ] Diagnostics: `Overlay.AgentSuppression` derived in both `frame` and
      `frameOf`, one per agent with `Suppression > 0`, rendered in `Ascii` +
      `Svg`, covered by a hand-built `DiagnosticsTests` fact and the
      re-pinned `open-engagement-tick-001.*` golden.
- [ ] `dotnet build CommandoWar.slnx -c Release` = 0/0; `dotnet list
      src/CommandoWar.Sim package --include-transitive` = `FSharp.Core` only;
      source scan of `src/CommandoWar.Sim` clean (`float` / `Stopwatch` /
      `DateTime` / `System.Random` / `godot`).
- [ ] `dotnet test CommandoWar.slnx -c Release` green.
- [ ] Docs updated (see below).

## Required verification

- `dotnet build CommandoWar.slnx -c Release` (0/0).
- `cwheadless corpus` before any edit (12 entries, all PASS) and after (still
  12 entries — no new one; `open-engagement`, `perception-contact`,
  `exposed-approach` diverge from their pre-edit tables, expected —
  regenerate, then a second `--regenerate` run is a zero diff, idempotent).
  Every other entry re-pins identically (byte-layout only) on
  `--regenerate`.
- Diffed `content/replays/*.md` for any changed "Tick count" or "Domain
  events" line: **none expected** anywhere — the actual stop-and-report
  check.
- `cwheadless fixture` before and after: hash changes (format bump), event
  count and tick count unchanged (`spike-fixture` is enemy-free, so
  `Suppression` stays `0` throughout — a byte-layout-only re-pin).
- Regenerated `content/diagnostics/open-engagement-tick-001.*`,
  `perception-contact-tick-005.*`, and `demo.html`.
- `cwheadless replay-file content/replays/envelope-full.cwreplay` — hash
  changes, checkpoints/tick count/event count otherwise unchanged
  (enemy-free scenario).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive` — `FSharp.Core` only.
- source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot` — clean.
- `git status --porcelain` — matches "Allowed scope"; nothing under the
  client spikes, `src/_scratch`, `bench/`, `content/benchmarks/BASELINE.md`.

## Evidence to capture

- test summary (exact before/after green count);
- the new `SimulationTests` facts and the property by name + case count;
- the `SuppressionConfig` constant table with rationale for the chosen
  values;
- confirmation every corpus entry / the fixture / `envelope-full` kept its
  tick count and event count, with the four combat-bearing entries' new
  `Suppression` values called out explicitly;
- the FS0025 sites with their fixes (compile-time in `CommandoWar.Sim` /
  `CommandoWar.Headless`, warning-only in the test project and
  `AppraisalDemo.fs`);
- confirmation the `AppraisalDemo` "three unhandled entries" fact is
  unaffected (`exposed-approach` tick 1 has no combat yet);
- the diagnostics goldens (re-pinned).

## Rollback or removal

`Suppression.fs`, the `AgentState.Suppression` field, the `stateConsequences`
phase body, the overlay/renderer branches, and the tests are additive;
`combat`'s edit (copying `s.Agents` and writing the target's `Suppression`)
is a small, isolated diff. Reverting: return `StateConsequences` to the
no-op arm, revert `combat` to alias `s.Agents` without copying, delete
`Suppression.fs` / the `AgentState` field / the overlay / the tests, and
revert `Canonical.FormatVersion` to `4` (re-pin every corpus entry back).

## Documentation updates

- this task file (Status, acceptance boxes);
- `docs/11_BACKLOG.md`: TASK-032 row; B-020 `proposed -> done` (only once
  Dave accepts and merges — `review` until then); B-021 unblocked note (its
  suppression-band trigger now has real state to trigger on);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
  "Green tests" count; "Pinned facts" `Canonical.FormatVersion` `4 -> 5` —
  left at `main`'s pre-merge values until accepted, the TASK-030/031
  precedent;
- `docs/04_SIMULATION_SPEC.md` section 11 (agent state realisation note),
  section 12.8 (Combat — "create suppression" bullet realised), section 12.9
  (State consequences — "update suppression decay" realised, the rest of the
  phase's bullets still not), section 17 (`FormatVersion` history);
- `docs/05_COMMAND_AND_AGENT_AI.md` section 8 (Suppression subsection —
  "immediate effect" value realised; the three behavioural consequences
  named, not built);
- `docs/07_VERTICAL_SLICE.md` section 5 ("suppression" realised note), section
  8 (canonical refusal sequence step 5 — still needs the `Suppress` order
  itself, B-030, and the reroute/reappraisal correction, B-021, but the value
  it targets now exists);
- `PROJECT_STATE.yaml` `active_work`;
- no ADR (Decision H).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
