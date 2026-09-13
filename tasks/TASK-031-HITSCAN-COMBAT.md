# TASK-031: Basic hitscan combat and directional cover effects

Status: draft
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises backlog B-019
Size: M

## Objective

Turn the **Combat phase (12.8)** — currently a no-op arm in
`Simulation.runPhase` — into a real phase: any agent with an engageable
opposing contact fires a deterministic hitscan shot, with directional
`Terrain.cover` mitigating the hit chance exactly as it mitigates route
exposure in Appraisal (TASK-028). This is deliberately the *mechanic* slice
only — line of fire, hit chance, cover, and the deterministic stream's first
real gameplay draw — with **no consequence to agent survivability**. Wounds,
death, ammunition, and weapon readiness are explicitly out (see "Central
decisions" and "Forbidden scope"); they are the concern of later backlog
items this task unblocks (B-020 suppression, B-031 casualties).

This realises `docs/07` section 5 "hitscan small-arms combat" as a distinct
line item from "suppression" and casualties, and is the direct precondition
for B-020: suppression needs *something* to suppress from, and this task is
what produces it (a shot, hit or missed).

## Why this task exists

- `docs/04_SIMULATION_SPEC.md` section 12.8 is a written-but-unrealised phase:
  "validate line of fire and ammunition; resolve weapon readiness; obtain
  deterministic spread or hit draw; apply cover and impact; create
  suppression independent of a hit where intended; emit shot, impact, wound,
  and suppression events." This task realises the subset current state
  supports: line-of-fire validation and a deterministic hit draw with cover
  mitigation. Ammunition, weapon readiness, wound/impact application, and
  suppression are named, not built (Decision B/C).
- `docs/07_VERTICAL_SLICE.md` section 5 lists "hitscan small-arms combat" and
  "suppression" as separate required systems; section 3's "the player must be
  able to change [a refusal's] reason ... by suppressing a machine-gun
  position" needs a real Combat phase to exist before suppression can be
  built on it.
- Risk R-023 (enemy AI cheats): a shooter's candidate targets come only from
  its own `AgentState.VisibleContacts` (populated by Perception, TASK-026),
  never a scan of authoritative global state — the same discipline Appraisal
  already observes for `WorldState.TacticalKnowledge`.
- Backlog B-020 (suppression), B-022 (enemy doctrine, needs hostiles able to
  shoot back), and transitively B-021 / B-023 all depend on this landing
  first — it is the single unblocking node for the rest of the P3 combat/
  psychology chain.

Depends only on TASK-026 (perception, for `VisibleContacts`, done, on `main`)
and TASK-015 (movement, done). Directly unblocks B-020 (suppression) and
B-022 (enemy doctrine); indirectly B-021 and B-023.

## Required reading

Read in this order; verify each path and inspect the source before trusting a
filename (`AGENTS.md`).

1. `PROJECT_STATE.yaml`, `AGENTS.md`.
2. `docs/ledger/2026-09-08-TASK-028-order-appraisal-and-typed-reasons.md` —
   the `attackDirection` / cover-mitigation geometry this task reuses
   verbatim for hit chance instead of tactical pressure.
3. `docs/04_SIMULATION_SPEC.md` sections 9 (line of sight and cover), 11
   (agent state — "weapon and ammunition", "current execution state" — both
   still not realised by this task), 12.3 (Perception — TASK-026,
   `VisibleContacts` is this task's candidate source), 12.8 (Combat — the
   no-op this task realises), 14 (events), 17 (canonical hashing —
   `WorldState.Random` is already canonical; this task is its first real
   consumer, so no format bump), 20 (invariants).
4. `docs/05_COMMAND_AND_AGENT_AI.md` section 8 (suppression — "immediate
   effect of hostile fire," still B-020; this task creates the fire, not the
   effect).
5. `docs/07_VERTICAL_SLICE.md` section 5 (required simulation systems —
   "hitscan small-arms combat" as its own line, distinct from "suppression").
6. `docs/09_TEST_STRATEGY.md` section 2.1, and the benchmark note ("combat...
   attach to a commented slot... when those systems land" — out of scope
   here, `bench/` stays forbidden, see "Forbidden scope").
7. `docs/10_RISK_REGISTER.md` R-023 ("same observation contract" — this
   task's candidate-selection rule).
8. `src/CommandoWar.Sim/Phases.fs` — `Phase.Combat`'s existing slot (after
   `NavigationAndMovement`, before `StateConsequences`).
9. `src/CommandoWar.Sim/Simulation.fs` — `runPhase` (`Combat` currently in
   the no-op list with `StateConsequences` / `Mission`), `StepState.Random`
   (mutable, threaded to `StepResult`; no phase has drawn from it yet),
   `perception` (the phase that populates `VisibleContacts`, this task's
   candidate source).
10. `src/CommandoWar.Sim/Appraisal.fs` — `attackDirection` (currently
    `private`; this task makes it public and reuses it — see Decision E),
    `AppraisalConfig` (the module-literal-config style this task's
    `CombatConfig` matches).
11. `src/CommandoWar.Sim/Perception.fs` — `Perception.chebyshev` (public,
    reused for range and target-distance comparison).
12. `src/CommandoWar.Sim/Sight.fs` — `Sight.visible` (reused, fresh, at
    combat time — see Decision A on why `VisibleContacts` alone is not
    re-checked for LOS).
13. `src/CommandoWar.Sim/Terrain.fs` — `Terrain.cover` (reused identically to
    Appraisal's stage-3 mitigation).
14. `src/CommandoWar.Sim/Random.fs` — `RandomState`, `RandomStream.next` (the
    draw this task's phase performs — the stream's first real gameplay
    consumer).
15. `src/CommandoWar.Sim/Domain.fs` — `AgentState.VisibleContacts`,
    `AgentState.Position`, `Side`. No field is added by this task.
16. `src/CommandoWar.Sim/Events.fs` — `EventBody`, the `DomainEvent` ordering
    doc comment.
17. `src/CommandoWar.Sim/Diagnostics.fs` — the `Overlay` doc comment's
    reserved line "B-019 combat -> a fire-line case (shooter, target)";
    `eventMarker`, the FS0025 filter-arm precedent.
18. `src/CommandoWar.Headless/{DiagnosticRender.fs, Corpus.fs}` — the
    `UndeliveredOrder` `Svg` line-drawing precedent (this task's `FireLine`
    follows a similar shooter-to-target line); `Corpus.rawScenario` / `Entry`
    / `Corpus.all`.
19. `tests/CommandoWar.Sim.Tests/{SimulationTests.fs, DeterminismPropertyTests.fs,
    CorpusTests.fs, DiagnosticsTests.fs}` — the `perceptionCaseGen` /
    `appraisalCaseGen` generator precedent this task's property reuses or
    extends.
20. `content/replays/CORPUS.md` + a `.md` entry with an enemy deployment
    (`perception-contact` or `exposed-approach`) for the corpus-entry shape.

## Dependencies

- TASK-026 (perception and shared tactical knowledge, done, on `main`) —
  `AgentState.VisibleContacts` is this task's candidate source.
- TASK-015 (movement executor, done) — Combat runs after Navigation
  (`Phases.order`), resolving against post-movement positions.

## Central decisions (confirmed with Dave 2026-09-13 before the phase bodies)

### Decision A — automatic engagement; no player order; both sides symmetric — CONFIRMED

`PlayerIntent` is still `MoveTo`-only (`Hold` / `Suppress` / `Assault` /
`Withdraw` are B-030), so combat cannot be player-ordered yet. Each tick,
independently of any order, an agent with at least one engageable opposing
contact fires. Symmetric: both `Friendly` and `Hostile` agents can engage,
matching `docs/05` section 12's requirement that enemies "use the same
perception and combat rules where practical" and TASK-026's "both sides
observe" precedent.

**Candidate source is `AgentState.VisibleContacts` only** (populated at the
start of this tick by Perception, phase 3) — never a scan of
`WorldState.Agents`, per R-023 ("same observation contract"). Because Combat
(phase 8) runs after Navigation (phase 7), a candidate's position may have
changed since Perception observed it; **line of fire is re-verified fresh at
combat time** against each candidate's *current* position (`Sight.visible`,
this task's own call, not a cached fact) — `VisibleContacts` supplies the
candidate *set* ("who is a known threat"), not a cached line-of-fire
guarantee.

**Target selection**: among candidates within `CombatConfig.WeaponRange`
(Chebyshev) and currently in `Sight.visible` line of fire, the nearest by
Chebyshev distance; ties broken by ascending `AgentId`. No target qualifies
-> no shot, no event, silence (the sparse-event precedent every other phase
observes).

### Decision B — no ammunition or weapon readiness; fires every valid tick — CONFIRMED

No weapon or ammunition data exists on `AgentState`. `docs/04` section 12.8's
"validate ... ammunition" and "resolve weapon readiness" bullets stay
unrealised — named, not built. A qualifying agent fires every tick a target
qualifies; no cooldown, no magazine, no reload.

**Future direction, not built here** (recorded so a later task doesn't need
to rediscover it): ammunition will eventually be either a cooldown-between-
magazines model or a reload-from-stock model; how an agent replenishes stock
(resupply) is an open question for that task, not this one. This task
deliberately adds no `AgentState` field or config shape that would need
reworking when that lands (no fire-rate counter, no ammo count).

### Decision C — a hit has no consequence: no wound/death state — CONFIRMED

`docs/07` section 5 lists "hitscan small-arms combat," "suppression," and
(via B-031) casualties as three separate systems; the backlog puts casualties
at P4 (B-031, depends on B-019 *and* B-021). This task proves the
deterministic hit/miss/cover mechanic and emits an event; it adds **no**
`AgentState` health/wound field, and a `hit = true` shot does not remove,
disable, or otherwise change the target. B-020 (suppression) is what makes a
shot matter psychologically; B-031 makes it matter physically.

**Future direction, not built here**: death will eventually be a
critical/bleed-out state with a recovery timer (an agent can be saved or
recover if aided within a window), not a binary kill, mirrored on an XCOM-
style wounded state rather than instant removal. This task adds nothing that
would need reworking when that lands (no `IsDead: bool`, no removal-from-
`Agents` logic).

### Decision D — deterministic hit chance; the stream's first real gameplay draw — CONFIRMED

```fsharp
[<RequireQualifiedAccess>]
module CombatConfig =

    /// Chebyshev cells: the farthest a shot can be attempted. Below
    /// PerceptionConfig.SightRange (10) so a target can be seen before it is
    /// in weapon range, the AppraisalConfig.ThreatEngagementRange precedent.
    [<Literal>]
    let WeaponRange = 7

    /// Hit chance on the 0..1000 scale (the Confidence precedent) at zero
    /// range with no cover.
    [<Literal>]
    let BaseHitChance = 700

    /// Hit-chance reduction per Chebyshev cell of range.
    [<Literal>]
    let RangePenaltyPerCell = 40

    /// Hit-chance reduction per authored Terrain.cover level on the edge the
    /// shot arrives from (the AppraisalConfig.CoverMitigationPerLevel
    /// precedent, same units, independently tunable).
    [<Literal>]
    let CoverMitigationPerLevel = 150

    /// Clamp bounds: a shot is never a certainty and never impossible once
    /// taken (a qualifying shot has already passed the range/LOS gate).
    [<Literal>]
    let MinHitChance = 50
    [<Literal>]
    let MaxHitChance = 950

[<RequireQualifiedAccess>]
module Combat =
    /// Integer hit chance (0..1000) for a shot from `shooter` at `target`,
    /// both current cells. Pure, total. Reuses Appraisal.attackDirection
    /// (made public by this task) for the cover-facing geometry.
    let hitChance (terrain: Terrain) (shooter: Cell) (target: Cell) : int =
        let distance = Perception.chebyshev shooter target
        let cover = Terrain.cover terrain target (Appraisal.attackDirection shooter target)

        CombatConfig.BaseHitChance
        - CombatConfig.RangePenaltyPerCell * distance
        - cover * CombatConfig.CoverMitigationPerLevel
        |> max CombatConfig.MinHitChance
        |> min CombatConfig.MaxHitChance
```

One `RandomStream.next` draw per engaging agent (a candidate qualified), in
ascending shooter `AgentId` order (`s.Agents` is already sorted ascending —
the standing invariant). `hit = (draw % 1000UL) < uint64 chance`. This is the
simulation's **first actual gameplay PRNG consumption** —
`WorldState.Random` is already part of `Canonical.encode` (TASK-003), so
consuming it changes only the *values* within an already-canonical field:
**no `Canonical.FormatVersion` bump**.

### Decision E — reuse `Appraisal.attackDirection`, made public — CONFIRMED

`attackDirection` (the dominant-cardinal-of-the-offset geometry that decides
which `Terrain.cover` edge fire arrives from) is currently `private` to
`Appraisal`. Rather than duplicate this ten-line pure function into a new
leaf, this task removes `private` and reuses it — the identical precedent to
`Appraisal` itself reusing `Perception.chebyshev`. `Appraisal.fs`'s own
behaviour is unchanged; only its visibility widens.

### Decision F — one new event, `ShotFired of shooter * target * hit: bool` — CONFIRMED

```fsharp
| ShotFired of shooter: AgentId * target: AgentId * hit: bool
```

One case for both outcomes (matching `OrderAppraised`'s single-event-with-an-
outcome-field shape) rather than splitting `ShotFired` / `ImpactResolved` into
two event types — nothing yet reads a hit differently from a miss beyond the
diagnostic overlay colour. Emitted for every qualifying shot, never for a
tick with no qualifying target (the sparse-event precedent). Ordered as a new
group after movement outcomes, ascending shooter `AgentId`.

### Decision G — new leaf `src/CommandoWar.Sim/Combat.fs` — CONFIRMED

`CombatConfig` + `Combat.hitChance` + `Combat.chooseTarget` (the
candidate-filter-then-nearest logic in Decision A), pure, total, `Terrain` /
`Sight` / `Perception` / `Appraisal` / `Domain` only, no event emission, no
mutation, no PRNG draw (the phase function draws; the leaf only computes the
chance). Inserted in the fsproj after `Commitment.fs`.

### Decision H — one new corpus entry proves the phase wiring; unit facts prove the tactics — CONFIRMED

A corpus entry pins one deterministic run (one seed), so it can show *that* a
shot occurs and *what* it resolves to for the committed seed — it cannot
demonstrate a *probability* shift (that needs comparing chances, not
outcomes). New entry `open-engagement`: a friendly and a hostile within
`WeaponRange` and clear line of sight from tick 1, no orders (both
stationary) — the Combat phase alone drives the trace. Proves the whole
phase wires together end-to-end with a stable hash.

Cover mitigation and range penalty are proved directly on the pure
`Combat.hitChance` function in `SimulationTests` (no simulation run, no RNG
needed): chance strictly decreases as range increases, all else equal;
chance strictly decreases as cover level increases, all else equal; chance
never leaves `[MinHitChance, MaxHitChance]`. Target selection
(`Combat.chooseTarget`) gets its own facts: nearest wins, ties broken by
ascending id, an out-of-range or LOS-blocked candidate is excluded.

### Decision I — no ADR — CONFIRMED

`Combat`'s phase slot already exists in `Phases.order`. `WorldState.Random`
consumption was already anticipated by its own doc comment ("no gameplay
phase draws from it yet; a future gameplay phase reassigns it after each
draw"). No canonical-image change. Design recorded in this task file and the
ledger.

## Phase bodies

### `combat` (12.8) — new phase, header "Realised by TASK-031"

Runs after `navigationAndMovement`, before the (still no-op)
`stateConsequences`. Reads `s.Agents` directly (no copy — this phase writes
no `AgentState` field, per Decision C) and threads `s.Random` through a local
accumulator.

```fsharp
let private combat (s: StepState) =
    let terrain = s.Terrain
    let agents = s.Agents // already ascending by id
    let mutable random = s.Random

    for shooter in agents do
        let candidates =
            shooter.VisibleContacts
            |> Array.choose (fun id -> agents |> Array.tryFind (fun a -> a.Id = id))

        match Combat.chooseTarget terrain shooter candidates with
        | None -> ()
        | Some target ->
            let chance = Combat.hitChance terrain shooter.Position target.Position
            let struct (draw, next) = RandomStream.next random
            random <- next
            let hit = (draw % 1000UL) < uint64 chance
            emit (ShotFired(shooter.Id, target.Id, hit)) s

    s.Random <- random
```

`runPhase` — `Combat -> combat s`; remove `Combat` from the no-op list
(`StateConsequences` / `Mission` stay no-ops).

## Diagnostics

`AGENTS.md`'s diagnostic-extension rule applies: a new event and the first
combat overlay. Required:

- **`Overlay.FireLine of shooter: AgentId * from: Cell * target: AgentId * at:
  Cell * hit: bool`** — self-contained cells (the `Reserved` / `Obstructed`
  precedent: an overlay carries the cells a renderer needs without cross-
  referencing `AgentMarker`s). `Diagnostics.frameOf` derives one per this
  tick's `ShotFired` event, looking up each side's cell from the post-step
  `result.State.Agents` (the `UndeliveredOrder` precedent); `Diagnostics.frame`
  never emits one (a shot is a this-tick event, not standing state).
- `eventMarker` gains `ShotFired(_, _, hit) -> { Kind = (if hit then
  "shot-fired-hit" else "shot-fired-miss"); Cells = [||] }` (the event itself
  carries no cells, only ids — the `OrderAppraised` precedent).
- `DiagnosticRender.Ascii`: a `fire (x1,y1) -> (x2,y2): agent N -> agent M
  <hit|miss>` overlay text line; the existing exhaustive filter arms
  (`sightRays`, `plannedPaths`) gain `| FireLine _ -> None`.
- `DiagnosticRender.Svg`: a line between the two cells (the `UndeliveredOrder`
  diagonal-line precedent), orange (`#dd6b20`) and solid for a hit, grey
  (`#a0aec0`) and dashed for a miss — deliberately distinct from the
  `PlannedPath` polyline and the `OrderAppraisal` glyph.
- Every exhaustive `EventBody` / `Overlay` match armed with the intended
  branch: `Diagnostics` (`reservationOverlays`, `obstructionOverlays`,
  `undeliveredOrderOverlays` filters), `DiagnosticRender` (both `Ascii`
  filters, `Ascii` text, `Svg`), `Program.describeReplayError` / any
  event-describe, and the test-project overlay matches (`DiagnosticsTests`
  has no `TreatWarningsAsErrors` — check these by hand, the TASK-030 lesson).
  FS0025 sites + fixes listed in the ledger.
- Goldens: the new `open-engagement-tick-001.*` + a hand-built `FireLine`
  overlay unit test; the regeneration note in
  `content/diagnostics/README.md`.

## Allowed scope

- `src/CommandoWar.Sim/Appraisal.fs` — `attackDirection`: remove `private`
  only. No other change.
- `src/CommandoWar.Sim/Combat.fs` (**new leaf**, after `Commitment.fs`) —
  `CombatConfig`; `Combat.hitChance`; `Combat.chooseTarget`. Pure, total,
  no event emission, no mutation, no PRNG draw.
- `src/CommandoWar.Sim/Simulation.fs` — new `combat` phase function;
  `runPhase` arm; header comment.
- `src/CommandoWar.Sim/Events.fs` — `ShotFired`; `DomainEvent` ordering doc
  comment update.
- `src/CommandoWar.Sim/Diagnostics.fs` — `Overlay.FireLine`; `frameOf`
  derivation; `eventMarker` arm; the `Overlay` doc comment; FS0025 filter
  arms.
- `src/CommandoWar.Headless/DiagnosticRender.fs` — new `Ascii` / `Svg`
  branches + filter arms.
- `src/CommandoWar.Headless/Corpus.fs` — the new `open-engagement` `Entry`;
  `Corpus.all` row; `CORPUS.md` row.
- `src/CommandoWar.Headless/Program.fs` — a `ShotFired` arm only if an
  exhaustive `EventBody` match exists there.
- `content/replays/` — the new `open-engagement.{cwlog,md}`; `CORPUS.md`.
- `content/diagnostics/` — new `open-engagement-tick-001.*`; `README.md`.
- `tests/CommandoWar.Sim.Tests/` — see "Acceptance criteria".
- Docs — see "Documentation updates".

## Forbidden scope

- Ammunition, weapon readiness, fire-rate / cooldown, reload, resupply —
  future work, deliberately not scoped here (Decision B).
- Any `AgentState` health / wound / death field, agent removal, or
  incapacitation — B-031 (Decision C).
- Suppression, stress, or exposure changes from being shot at — B-020.
- `Hold` / `Suppress` / `Assault` `PlayerIntent` cases, or any new order type
  — B-030.
- A hostile squad tactical picture or enemy doctrine choosing *when* or
  *whether* to engage beyond "an engageable target exists" — B-022 (this
  task's symmetric auto-engage is not doctrine, it is the mechanic doctrine
  will eventually gate).
- Any `Sight.fs` / `Terrain.fs` / `Perception.fs` / `Pathfinding.fs` change.
- Any `Appraisal.fs` change beyond removing `private` from `attackDirection`.
- A `Canonical.FormatVersion` change (Decision D is the point of this task's
  shape: the stream was already canonical).
- Editing the client spikes, `src/_scratch`, `bench/`,
  `content/benchmarks/BASELINE.md`.
- An ADR (Decision I).

## Acceptance criteria

- [ ] `Combat` is a real phase function in `Simulation.fs`, in its existing
      `Phases.order` slot, with a "Realised by TASK-031" header comment;
      `runPhase` has a real arm and it is out of the no-op list.
- [ ] `src/CommandoWar.Sim/Combat.fs`: `CombatConfig` module literals;
      `Combat.hitChance` / `Combat.chooseTarget` — pure, total, no PRNG draw
      inside the leaf.
- [ ] `Appraisal.attackDirection` is public; no other change to `Appraisal.fs`;
      `AppraisalConfig`-dependent facts unaffected.
- [ ] `ShotFired of shooter * target * hit` event; `DomainEvent` ordering doc
      comment updated (after movement, ascending shooter agent id). Every
      exhaustive `EventBody` match armed; FS0025 sites + fixes in the ledger.
- [ ] `SimulationTests` facts:
  - a friendly and a hostile within range and clear LOS produce exactly one
    `ShotFired` per engaging side per tick;
  - a candidate beyond `WeaponRange` is never engaged (no `ShotFired`);
  - a candidate with LOS blocked by an opaque cell is never engaged even
    though it is within `VisibleContacts` from an earlier tick;
  - `Combat.hitChance` strictly decreases as range increases (fixed cover);
  - `Combat.hitChance` strictly decreases as cover level increases (fixed
    range); stays within `[MinHitChance, MaxHitChance]` at the extremes;
  - `Combat.chooseTarget` picks the nearest candidate, ties broken by
    ascending `AgentId`;
  - two runs of the same world + commands emit byte-identical events, draw
    counts, and hashes.
- [ ] A `DeterminismPropertyTests` property (`MaxTest = 200`, extending
      `perceptionCaseGen` with a hostile in range/LOS some of the time):
      every `ShotFired` event's `hit` matches recomputing `Combat.hitChance`
      against the pre-shot state and redrawing from the pre-shot `Random`
      state at the same draw index; `Random.Draws` increases by exactly the
      number of `ShotFired` events emitted that tick, never more.
- [ ] New corpus entry `open-engagement` (Decision H): `CORPUS.md` row,
      committed `.cwlog` + `.md`, passes `CorpusTests` `[<Theory>]` and
      `cwheadless corpus`.
- [ ] No `Canonical.FormatVersion` change; every corpus entry, the fixture,
      and `envelope-full` **that has no engageable pair** shows no
      `ShotFired` events and no hash change; `open-engagement`'s hash and
      draw count are the only newly-pinned combat values.
- [ ] Diagnostics: `Overlay.FireLine` derived in `frameOf` only, rendered in
      `Ascii` + `Svg`, covered by a hand-built `DiagnosticsTests` fact and
      the committed `open-engagement-tick-001.*` golden.
- [ ] `dotnet build CommandoWar.slnx -c Release` = 0/0; `dotnet list
      src/CommandoWar.Sim package --include-transitive` = `FSharp.Core` only;
      source scan of `src/CommandoWar.Sim` clean (`float` / `Stopwatch` /
      `DateTime` / `System.Random` / `godot`).
- [ ] `dotnet test CommandoWar.slnx -c Release` green — state the new count;
      name each added fact and the property's case count.
- [ ] Docs updated (see below).

## Required verification

- `dotnet build CommandoWar.slnx -c Release` (0/0).
- `dotnet test CommandoWar.slnx -c Release` before any edit (`254`) and after
  (new count; each added fact + the property's case count named).
- `cwheadless corpus` + `cwheadless fixture` before any edit (record hashes /
  event counts / draw counts) and after (every existing entry byte-identical
  — no engageable pair exists in any of them, so zero draws, zero
  `ShotFired`, zero hash change; only `open-engagement` is new); re-running
  `--regenerate` is a zero diff.
- Regenerate the new `content/diagnostics/open-engagement-tick-001.*` golden;
  confirm no pre-existing golden changed.
- `cwheadless replay-file content/replays/envelope-full.cwreplay` —
  checkpoints unchanged (its scenario is enemy-free, so Combat is inert
  there).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive` — `FSharp.Core` only.
- source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`.
- `git status --porcelain` — matches "Allowed scope"; nothing under the
  client spikes, `src/_scratch`, `bench/`, `content/benchmarks/BASELINE.md`.

## Evidence to capture

- test summary (before/after counts); the new `SimulationTests` facts and the
  property by name + case count;
- confirmation that every pre-existing corpus entry, the fixture, and
  `envelope-full` are byte-identical (no engageable pair, so Combat is inert
  there) — this is the "first PRNG consumer" safety net;
- the new `open-engagement` entry's hash / tick / event count and draw count;
- the `CombatConfig` constant table with rationale for the chosen values;
- the FS0025 sites with their fixes;
- the diagnostics golden.

## Rollback or removal

`Combat.fs`, the `combat` phase, `ShotFired`, the overlay/renderer branches,
the `open-engagement` entry, and the tests are additive; `Appraisal.fs`'s
`private` removal is a one-token revert. Reverting: return `Combat` to a
no-op arm, restore `Appraisal.attackDirection`'s `private`, delete
`Combat.fs` / the event / the overlay / the entry / the goldens / the tests.
No canonical or format-version rollback needed (none was made).

## Documentation updates

- this task file (Status, acceptance boxes);
- `docs/11_BACKLOG.md`: TASK-031 row; B-019 `proposed -> done` (only once
  Dave accepts and merges — `review` until then); B-020 / B-022 unblocked
  notes;
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
  "Green tests" count (no `Canonical.FormatVersion` line to change — stays
  4) — left at `main`'s pre-merge values until accepted, the TASK-030
  precedent;
- `docs/04_SIMULATION_SPEC.md` section 12.8 (Combat realisation block: line
  of fire + hit draw realised; ammunition / weapon readiness / impact /
  suppression events named, not built), section 17 (note `WorldState.Random`'s
  first real consumer);
- `docs/05_COMMAND_AND_AGENT_AI.md` section 12 (enemy AI — "same perception
  and combat rules" now literally true for the hitscan mechanic);
- `docs/07_VERTICAL_SLICE.md` section 5 ("hitscan small-arms combat" realised
  note);
- `docs/08_ROADMAP_AND_GATES.md` section 6 — add "implement basic hitscan
  combat" to the P3 Required work list (a pre-existing gap: the prose omits
  it even though the backlog and dependency graph already treat it as P3/G3
  critical path);
- `PROJECT_STATE.yaml` `active_work`;
- no ADR (Decision I).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
