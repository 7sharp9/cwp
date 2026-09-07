# TASK-026: Observations and shared squad tactical knowledge

Status: review
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises backlog B-015
Size: M (landed at M — the retained decaying store was ~40 lines in a new leaf
module; the heaviest part was the `Canonical.FormatVersion` 2 -> 3 re-pin, as
expected)

## Outcome (2026-09-07)

Implemented on branch `task-026-perception-and-tactical-knowledge` (committed
locally, not pushed). All three central decisions were confirmed with Dave
before the canonical encoder / phase bodies were written:

- **Decision 3:** the full retained decaying store landed
  (`StaleAfter` / `ExpireAfter` / `ContactExpired`), not the narrowed cut.
- **Decision 5:** `PerceptionConfig` module literals —
  `SightRange = 10` (Chebyshev), `ConfidenceFull = 1000`,
  `ConfidenceBandDrop = 250`, `StaleAfter = 20`, `ExpireAfter = 60`.
  One-line reasons in `src/CommandoWar.Sim/Perception.fs` and the ledger.
- **Decision 6:** confirmed as drafted — symmetric `VisibleContacts` for both
  sides, friendly-squad-only `TacticalKnowledge`.

`Canonical.FormatVersion` 2 -> 3; the re-pin is behaviour-neutral for the
fixture and all seven `.cwlog` corpus entries (tick counts and event counts
identical, hashes in the ledger). It also re-pinned
`content/replays/envelope-full.{cwreplay,md}` (TASK-025, drafted after this
task file — `ReplaySerialisation.parse` rejects a `canonical` mismatch, so the
data file had to move; still behaviour-neutral, 24 ticks / 72 events
unchanged). `DemoScenario` is the one committed scenario with a hostile, so
its goldens gained real perception behaviour (a `KnownContact` overlay and
four `contact-observed` markers) — allowed, since it is not one of the seven.

`dotnet test` 206 -> 216. New: five `SimulationTests` perception facts; a
`DeterminismPropertyTests` property (`MaxTest = 200`) that every squad contact
was genuinely visible to a friendly on its `LastSeenTick` (recomputed
independently), within `ExpireAfter`, with a non-decreasing `LastSeenTick` and
a valid confidence band; a `CanonicalHashTests` fact for the new section /
`firstDifferingSection`; a hand-built `KnownContact` overlay fact and the
`perception-contact` golden fact in `DiagnosticsTests`. New corpus entry
`perception-contact` (12 x 8, one friendly + one hostile behind an opaque
wall; 14 ticks, 12 events).

Full detail:
`docs/ledger/2026-09-07-TASK-026-perception-and-tactical-knowledge.md`.

## Objective

Make the simulation's agents perceive, rather than know. Turn the two dormant
tick phases — **Perception (12.3)** and **Tactical knowledge (12.4)** — into
real phases that derive what each agent can see through the `Sight` module and
merge those observations into one shared squad tactical picture with last-known
position, confidence, and observation tick.

This is the **first phase consumer of the `Sight` module** (TASK-012):
`src/CommandoWar.Sim/Sight.fs` is authored, total, and tested, but "NOTHING in
`Simulation.step` or any tick phase calls this module yet" — its own comment
names Perception (B-015) as the first consumer. It is also the first system
that makes the `docs/05_COMMAND_AND_AGENT_AI.md` distinction real: "The
simulation knows all authoritative state. Agents do not."

It is the foundation the P3 cognition chain builds on: appraisal (B-017) reads
tactical viability from known threats, enemy doctrine (B-022) reacts to
observed contacts, combat (B-019) validates line of fire.

## Why this task exists

- `docs/08_ROADMAP_AND_GATES.md` section 6 P3 Required work: "implement
  communication and shared tactical knowledge". `docs/07_VERTICAL_SLICE.md`
  section 5 names "shared squad tactical knowledge" and "basic enemy
  perception" among the deterministic-core requirements; the "Unknown threat"
  scenario (`docs/05` section 16) — "an agent accepts based on current
  knowledge, then reacts when fired upon" — cannot be built without it.
- G3 evidence: "at least two agents appraise the same intent differently for
  inspectable reasons" and "scenario traces are readable enough to diagnose
  all decisions" both require agents to hold *different* knowledge, which
  requires perception.
- Risk R-023 (enemy AI cheats, undermining the value of perception and
  command): the mitigation is "same observation contract ... known-versus-
  authoritative overlay". This task builds that contract and that overlay.
- Risk R-007 (AI design too broad to debug): the mitigation is "shared
  tactical knowledge" as an explicit, inspectable structure rather than a
  per-agent world model. This task lands the explicit structure.

Depends only on TASK-012 (`Sight`, done) and the terrain grid (TASK-010,
done, and hardened by TASK-021). **Independent of TASK-024 and TASK-025** —
it touches no command or replay code. It should land before B-016
(communication constraints and report aging), B-017 (appraisal), B-019
(combat), and B-022 (enemy doctrine), all of which consume its output.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `decisions/ADR-0002-SIMULATION-BOUNDARY.md` — the whole file, and
  especially the 2026-09-03 amendment "Static authoritative data and the
  canonical image" (the rule that decides whether `TacticalKnowledge` is in
  the canonical image and forces the format-version bump when it is)
- `src/CommandoWar.Sim/Sight.fs` — `Sight.trace`, `Sight.visible`,
  `LineOfSight`, the corner / elevation / symmetry rules, the "first consumer
  is Perception" comment
- `src/CommandoWar.Sim/Domain.fs` — `AgentState` (and its `Route` derived-cache
  comment, the model for a `VisibleContacts` cache), `WorldState` ("Fields
  are added only when an implemented behaviour requires them"), `Agent.create`,
  `Side`
- `src/CommandoWar.Sim/Simulation.fs` — `runPhase` (Perception,
  TacticalKnowledge currently no-op arms), `StepState`, `step`, the phase
  order, `navigationAndMovement` for the Pass-1/Pass-2/Pass-3 and
  `Array.copy s.Agents` idiom to mirror
- `src/CommandoWar.Sim/Phases.fs` (`Phase`, `Phases.order`)
- `src/CommandoWar.Sim/Events.fs` — `EventBody` (the ordering doc comment;
  `TreatWarningsAsErrors` forces a new arm in every exhaustive match when a
  case is added), `DomainEvent`
- `src/CommandoWar.Sim/Canonical.fs` — `FormatVersion` (the TASK-018 bump
  precedent in its doc comment), `Writer`, `encode`, `writeAgent`,
  `topLevelSections`, `firstDifferingSection`
- `src/CommandoWar.Sim/Diagnostics.fs` — `Overlay` (the doc comment reserving
  a case per backlog item: "B-015 ... a visibility/known overlay"),
  `AgentMarker`, `EventMarker`, `eventMarker`, `frame` vs `frameOf`,
  `reservationOverlays` / `obstructionOverlays` (the derivation precedent),
  `DiagnosticFrame`
- `src/CommandoWar.Headless/DiagnosticRender.fs` — the two `Overlay`-exclusion
  filters, the `Ascii` overlay text loop, the `Svg` overlay drawing loop
  (adding an `Overlay` case forces every match site under
  `TreatWarningsAsErrors`)
- `src/CommandoWar.Headless/Corpus.fs` — `Entry`, `Corpus.all`, `rawScenario`
  (**hard-wires no enemies, no targets** — the first enemy deployment needs a
  builder change), `worldOf`, `wall`, `costly`, `convergingRoutesWorld` and
  the other builders
- `src/CommandoWar.Sim/Scenario.fs` — `EnemyDeployments`, `Side`,
  `World.ofScenario` (friendly-then-enemy deployment order), the validation
  of enemy deployments
- `tests/CommandoWar.Sim.Tests/SightTests.fs` (the symmetry property and
  goldens), `DeterminismPropertyTests.fs` (`randomCaseGen`, `cellOf`,
  properties 1–5), `SimulationTests.fs`, `CorpusTests.fs`, `DiagnosticsTests.fs`,
  `FixtureTests.fs`
- `content/fixtures/SPIKE-FIXTURE.md`, `content/replays/CORPUS.md` and the
  seven `content/replays/*.md` hash tables, every `content/diagnostics/*`
  golden that embeds a hash/format footer (all re-pinned by the format bump)
- `docs/04_SIMULATION_SPEC.md` sections 4 (integer scales — the confidence
  band scale), 10 (`WorldState`), 11 (`AgentState` "current visible
  contacts"), 12.3, 12.4, 14 (events — "contact observed or reported"), 17
  (canonical hashing), 19 (performance budget — 50-agent perception),
  20 (invariants)
- `docs/05_COMMAND_AND_AGENT_AI.md` sections 3 (Perception, the observation
  and squad-tactical-picture fields), 12 (enemy AI — "same perception ...
  rules where practical"), 13 (developer trace), 14 (reappraisal triggers),
  15 (tuning rules), 16 (the "Unknown threat" scenario), 17 (deferred:
  "private persistent beliefs")
- `docs/09_TEST_STRATEGY.md` sections 2.2, 2.3, 2.4, 8
- `docs/12_PROGRESS_LEDGER.md` — the TASK-018 detail file
  (`docs/ledger/2026-09-04-TASK-018-subcell-movement-progress.md`): the
  procedure for a `Canonical.FormatVersion` bump and a behaviour-neutral
  full hash re-pin

## Dependencies

- TASK-012 (`Sight`, done), TASK-010 (`Terrain`, done)

## Central decisions

Recommended resolutions; the implementer confirms the shape (especially the
canonical-image question and the enemy-perception scope) with Dave before
implementing and records the final answers in the ledger.

### Decision 1 — two phases, in order: Perception (12.3) then Tactical knowledge (12.4)

`runPhase` gets real `Perception` and `TacticalKnowledge` arms (currently
`| Perception | TacticalKnowledge | ... -> ()`). They run in `Phases.order`
position — after Communication, before Appraisal — exactly as `docs/04`
section 12 lays out. Both copy `s.Agents` before writing (the
`commandIntake` / `navigationAndMovement` idiom); the input `WorldState` is
never mutated.

### Decision 2 — Perception output is a per-agent **derived cache**, excluded from the canonical image

Each agent gains `AgentState.VisibleContacts` (recommended: `AgentId[]`, or a
small `Sighting` record array carrying the observed cell and the observation
tick if the golden needs it). Perception, for every live agent:

1. clears the agent's visible contacts (`docs/04` section 12.3 "clear current
   visibility");
2. for every live agent on the opposing side within an integer sight-range
   cap (Decision 5), tests `Sight.visible terrain a.Position other.Position`;
3. records the visible ones and emits a `ContactObserved` event on a
   *new* sighting (not re-emitted every tick a contact stays visible —
   `docs/04` section 14 "Do not emit a flood of low-value events").

`VisibleContacts` is a **pure deterministic function of every agent's
`Position`, the immutable `Terrain`, and the sight-range constant** — the
identical argument that keeps `AgentState.Route` out of `Canonical.encode`
(`Domain.fs`, `docs/04` section 17 "derived caches either excluded or
normalised"). It is **excluded** from the canonical image and cannot diverge.
This alone moves **no** pinned hash.

### Decision 3 — Tactical knowledge is a genuine canonical `WorldState` field; `Canonical.FormatVersion` bumps 2 -> 3

`WorldState` gains `TacticalKnowledge` (recommended: a `Contact[]` sorted by
contact `AgentId`, each `{ Contact: AgentId; LastKnownCell: Cell;
LastSeenTick: int64; Confidence: int }`). The Tactical-knowledge phase, for
the friendly squad (see Decision 6):

- for every contact any friendly saw this tick: upsert with the observed
  cell, `LastSeenTick = tick`, `Confidence = ` the full band;
- for every existing contact **not** seen this tick: age it — drop a
  confidence band after `StaleAfter` ticks unseen, remove it after
  `ExpireAfter` ticks (Decision 5), emitting `ContactExpired` on removal;
- keep the array sorted by contact id (stable iteration, `docs/04` section 6).

This store **has memory** — `LastSeenTick` and the decaying `Confidence`
cannot be recomputed from the current tick's positions — so it is **genuine
per-tick canonical state**, not a derived cache. Under the ADR-0002 amendment
it must enter `Canonical.encode`. `Canonical.FormatVersion` bumps **2 -> 3**;
`encode` gains a tactical-knowledge section (contact count, then each contact
in id order, fixed-width big-endian per section 17); `topLevelSections` /
`firstDifferingSection` gain the section.

**Every pinned hash re-pins.** The fixture, all seven `content/replays/*.md`
tables, and every `content/diagnostics/*` golden with a hash/format footer.
This is **behaviour-neutral**: every committed scenario has **zero enemy
deployments** (`rawScenario` hard-wires none; the shared fixture and every
corpus entry are friendly-only), so `TacticalKnowledge` is empty at every
checkpoint and the moved hashes are a byte-layout artifact — exactly the
TASK-018 `Progress` situation. Follow the TASK-018 re-pin procedure
(`docs/ledger/2026-09-04-TASK-018-subcell-movement-progress.md`): regenerate,
diff, confirm tick counts and event counts are unchanged for every
pre-existing entry, record the old and new hash for each in the ledger.

**Implementer's latitude (confirm with Dave):** if landing the retained,
decaying store pushes the task to L, the decay/expiry rules (`StaleAfter` /
`ExpireAfter` / `ContactExpired`) may be narrowed out to a focused **B-016**
and this task lands the instant-shared picture with `LastSeenTick` only. The
`WorldState` field and the format bump still happen (the store still carries
`LastSeenTick`, still has memory across the tick a contact drops out of
sight). What must **not** happen is deferring the field and the bump — that
would only move the same re-pin to the next task.

### Decision 4 — instant squad sharing; no communication model, no private beliefs

`docs/05` section 3: "The first implementation may share contacts instantly
when communication is available. Private persistent beliefs are deferred."
There is one friendly squad (no `SquadStore` — do not build one; treat every
`Friendly` agent as the squad). Every friendly's observation this tick is
immediately in the shared `TacticalKnowledge`. Communication range / delay /
failure is **B-016**; per-agent private beliefs are `docs/05` section 17
deferred work. Record this as a decision.

### Decision 5 — one tuning structure, integer thresholds

`docs/05` section 15: "Record every threshold in one configuration
structure." Add a `PerceptionConfig` (a record of `[<Literal>]`-style
integer constants, or a field group — not on `SimConfig`, which is
non-authoritative host config; these values affect authoritative outcomes so
they belong in the simulation, either as module literals or on `WorldState`
if a scenario should ever tune them — recommended: module literals for now,
`docs/04` section 4's "one configuration structure"):

- `SightRange` — an integer cell radius cap on perception (Manhattan or
  Chebyshev; state which). Keeps 50-agent perception O(n^2) bounded and
  gives the "Unknown threat" scenario a reason the machine gun is unseen.
- `Confidence` full-band value on the `docs/04` section 4 0..1000 scale;
  the band drop amount.
- `StaleAfter`, `ExpireAfter` in ticks (Decision 3).

Small number of integer thresholds, no per-scenario exceptions, hysteresis if
a contact flickers in and out of sight (`docs/05` section 15).

### Decision 6 — perception is symmetric; shared knowledge is friendly-squad only

Both sides run the same `Sight`-based visibility in Perception (`docs/05`
section 12: "Enemy agents use the same perception ... rules where
practical"). So a `Hostile` agent's `VisibleContacts` is populated too. But
the **shared `TacticalKnowledge` store is the friendly squad's** — a hostile
squad picture, hostile doctrine reacting to it, and "enemy does not target an
unobserved player position" (`docs/09` section 2.3) are **B-022**. Symmetric
raw visibility now, asymmetric knowledge until B-022. Record this split; it is
the cheapest cut that still lets B-019 validate line of fire for both sides
off `VisibleContacts`.

### Decision 7 — no ADR (the format bump is covered by the ADR-0002 amendment)

The ADR-0002 amendment already specifies the procedure: "The moment any phase
mutates [authoritative state not previously in the image], that task MUST bump
`Canonical.FormatVersion`, add a section to `Canonical.encode` in canonical
order, re-pin the [pinned] files, and record the re-pin in the ledger." This
task follows that procedure; it decides no new architecture. `docs/08`
section 6 already lists shared tactical knowledge under P3 Required work —
no gate-obligation edit. Record the design in this task file and the ledger.

## Diagnostics

`AGENTS.md`'s diagnostic-extension rule **applies**: this task adds
authoritative tactical state (`TacticalKnowledge`) and derived spatial state
(`VisibleContacts`). Required:

- Add **`Overlay.KnownContact of cell: Cell * contact: AgentId * confidence:
  int * lastSeenTick: int64`** (the `Overlay` doc comment already reserves
  "B-015 ... a visibility/known overlay"). Because `TacticalKnowledge` is on
  `WorldState`, **both** `Diagnostics.frame` (bare state) and
  `Diagnostics.frameOf` (completed step) derive it — unlike `Reserved` /
  `Obstructed`, which only `frameOf` can produce. An optional second case or a
  `Cells`-based marker for "friendly X sees hostile Y **this tick**" (the
  sight links) is the implementer's call if the golden reads better with it.
- `AgentMarker` may gain a visible-contact count (small, optional).
- `eventMarker` arms for `ContactObserved` and `ContactExpired`.
- Wire `KnownContact` into `DiagnosticRender.fs`: both `Overlay`-exclusion
  filters, the `Ascii` overlay text loop, the `Svg` drawing loop. Expect
  `FS0025` incomplete-match errors on first build and fix each with the
  intended branch (not a wildcard) — that confirms the match sites were
  exhaustive.
- Commit a golden (`content/diagnostics/`) for a scenario where a friendly
  observes a hostile: the `KnownContact` overlay drawn, byte-compared by
  `DiagnosticsTests.fs`, plus a hand-built `KnownContact` overlay unit test
  (the `Reserved` / `Obstructed` precedent). Record the regeneration command
  in `content/diagnostics/README.md`.

## Allowed scope

- `src/CommandoWar.Sim/Domain.fs` — `AgentState.VisibleContacts` (derived
  cache); `WorldState.TacticalKnowledge`; the `Contact` type; `Agent.create`
  defaults; doc comments distinguishing the cache from the canonical store.
- `src/CommandoWar.Sim/Simulation.fs` — real `Perception` and
  `TacticalKnowledge` phase functions; `runPhase` arms; `StepState` fields if
  needed; the phase header comments (a "Realised by TASK-026" paragraph each).
- `src/CommandoWar.Sim/Events.fs` — `ContactObserved`, `ContactExpired`.
- `src/CommandoWar.Sim/Canonical.fs` — `FormatVersion` `2 -> 3`; the
  tactical-knowledge section in `encode`; `writeContact` helper;
  `topLevelSections` / `firstDifferingSection` section; the doc comment (the
  TASK-018 precedent style).
- `src/CommandoWar.Sim/Perception.fs` (**new, optional**) — if the sight
  sweep and the tactical-knowledge merge are large enough to warrant their own
  module, mirroring `Sight.fs` / `Pathfinding.fs` as leaves the phase
  consumes. Keep it pure, total, integer-only, `Sight`- and `Terrain`-only.
- `src/CommandoWar.Sim/` — the `PerceptionConfig` constants (module literals).
- `src/CommandoWar.Sim/Diagnostics.fs` — `Overlay.KnownContact`; `eventMarker`
  arms; a `knownContactOverlays` derivation used by both `frame` and
  `frameOf`; `AgentMarker` contact count if taken; `Overlay` doc comment.
- `src/CommandoWar.Headless/DiagnosticRender.fs` — the `KnownContact` match
  branches (both filters, `Ascii` text, `Svg` drawing).
- `src/CommandoWar.Headless/Corpus.fs` — a builder that admits enemy
  deployments (generalise `rawScenario` or add a sibling), and at least one
  new `Entry` with a friendly observing a hostile; keep the seven existing
  builders behaviourally identical (only their pinned hashes move, via the
  format bump).
- `content/replays/` — the new entry's `.cwlog` + `.md`; **regenerated**
  `.md` tables for all seven existing entries (format-bump re-pin only,
  tick/event counts unchanged); `CORPUS.md` rows.
- `content/fixtures/SPIKE-FIXTURE.md` — the re-pinned initial/final hash and
  the per-tick table (format-bump re-pin; 33 events, tick 40, behaviour
  unchanged).
- `content/diagnostics/` — the new `KnownContact` golden(s); regenerated
  goldens for any existing render whose footer embeds the format version or a
  hash; `README.md`.
- `tests/CommandoWar.Sim.Tests/` —
  - `SimulationTests.fs`: a friendly with clear LOS to a hostile observes it
    (`ContactObserved`, the contact in `TacticalKnowledge`); an opaque wall
    between them blocks the observation; a contact seen then lost ages and
    expires (`ContactExpired`); two friendlies share one squad picture
    instantly; a hostile out of `SightRange` is not observed.
  - `DeterminismPropertyTests.fs`: a new property (>= 200 cases over a
    generator that now places some hostiles) — every contact in
    `TacticalKnowledge` after a tick was observed by some friendly on that
    tick or a prior tick within `ExpireAfter`, and `LastSeenTick` is
    non-decreasing until expiry; existing properties 1–5 pass unmodified
    (re-pinned hashes flow through the `Replay`/`Divergence` self-double-run
    without code change).
  - `DiagnosticsTests.fs`: the hand-built `KnownContact` overlay fact and the
    new golden comparison.
  - `FixtureTests.fs`, `CorpusTests.fs`: updated pinned hash values only
    (the format-bump re-pin), no logic change.
- `docs/04_SIMULATION_SPEC.md` (sections 10, 11, 12.3, 12.4, 14, 17 "Realised
  by TASK-026" blocks; section 20 if a perception invariant is added),
  `docs/05_COMMAND_AND_AGENT_AI.md` (section 3 realisation note),
  `docs/09_TEST_STRATEGY.md` (sections 2.2, 2.3 the "Unknown threat" /
  "enemy does not target an unobserved position" partial-realisation note,
  2.4).
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml`
  if active). Refresh the "Pinned facts" `Canonical.FormatVersion` row to `3`
  and the "Shared fixture" hashes.

## Forbidden scope

- Communication range / delay / failure, report aging beyond the simple
  `StaleAfter` / `ExpireAfter` decay — that is **B-016**.
- Order appraisal, staged reasons, `OrderDisposition`, reappraisal triggers —
  **B-017**. Perception emits observations; it does not appraise.
- Combat, line of fire, suppression — **B-019**.
- A hostile squad tactical picture, enemy doctrine reacting to contacts,
  "enemy does not target an unobserved position" — **B-022**.
- Per-agent private / persistent beliefs, confidence divergence between squad
  members, a `SquadStore` or formation grouping (`docs/05` section 17;
  B-011d) — treat all friendlies as one squad.
- Any change to `Sight.fs` — it is a done leaf; consume it, do not touch it.
- Any command / replay / `.cwlog` / `Canonical`-command change — that is
  TASK-024 / TASK-025.
- Diagonal / 8-connected sight or movement.
- Re-pinning hashes for a *behaviour* change: every pre-existing entry's tick
  count and event count must be **identical** after the re-pin. A changed
  tick or event count on `spike-fixture` / `wall-detour` / `blocked-goal` /
  `converging-routes` / `slow-terrain` / `follow-chain` / `swap-standoff` is a
  **stop-and-report** finding (it means perception changed friendly-only
  behaviour, which it must not).
- Editing the client spikes, `src/_scratch`, or `bench/` (a read-only
  benchmark run for the 50-agent perception evidence is fine; do not modify
  `bench/` or re-pin `content/benchmarks/BASELINE.md` — the commented
  perception slot there is filled by whoever next touches the benchmark
  harness).

## Acceptance criteria

- [x] `Simulation.step`'s `Perception` phase populates `AgentState.VisibleContacts`
      from `Sight.visible` over `Terrain` within `SightRange`, for both sides,
      and emits `ContactObserved` on a new sighting only; `VisibleContacts` is
      excluded from `Canonical.encode` (`Canonical.fs` `writeAgent` unchanged;
      `Perception.fs`; `SimulationTests` "a friendly with clear line of sight
      … observes it and shares it").
- [x] A `SimulationTests` fact: a friendly with unobstructed LOS to a hostile
      within range observes it; the contact is in `WorldState.TacticalKnowledge`
      with `LastSeenTick = ` the current tick; an opaque wall placed between
      them removes the observation (fact "an opaque cell between a friendly and
      a hostile blocks the observation entirely"), and the ages-out path is the
      dedicated stale/expire fact below.
- [x] A `SimulationTests` fact: two friendlies, only one with LOS to a
      hostile, both have that contact in the shared `TacticalKnowledge` the
      same tick ("two friendlies, only one with line of sight … share the
      contact the same tick").
- [x] A `SimulationTests` fact: a hostile beyond `SightRange` with clear LOS
      is **not** observed (with a `dx = 10` control that IS observed).
- [x] A contact seen then lost drops a confidence band after `StaleAfter`
      unseen ticks and is removed with a `ContactExpired` event after
      `ExpireAfter` ("a contact seen then lost drops a confidence band after
      StaleAfter and expires with ContactExpired after ExpireAfter").
- [x] `Canonical.FormatVersion` is `3`; `Canonical.encode` has a
      tactical-knowledge section in canonical order; `firstDifferingSection`
      reports it (`CanonicalHashTests` "the tactical-knowledge section is in
      the canonical image and firstDifferingSection names it"). The fixture,
      all seven `content/replays/*.md` tables, `envelope-full.{cwreplay,md}`,
      and every hash-bearing `content/diagnostics/*` golden re-pinned;
      **unchanged tick counts and event counts** for every pre-existing entry;
      old/new hashes in the ledger table.
- [x] A new property (`MaxTest = 200`): every `TacticalKnowledge` contact was
      genuinely visible to a friendly on its `LastSeenTick` (recomputed via
      `Perception.visibleContactsFor`), within `ExpireAfter`, with a
      non-decreasing `LastSeenTick` and a valid confidence band. FsCheck prints
      the reduced counterexample and seed on failure. Properties 1–5 unmodified.
- [x] New corpus entry `perception-contact` (enemy deployment; friendly clears
      an opaque wall and observes a hostile): `CORPUS.md` row, committed
      `.cwlog` + `.md`, passes `CorpusTests.fs` (`[<Theory>]`) and
      `cwheadless corpus` (8/8 PASS).
- [x] `Overlay.KnownContact` derived in both `frame` and `frameOf`, rendered
      in `Ascii` (text line) and `Svg` (purple dashed ring `?<id>`), covered by
      a hand-built `DiagnosticsTests` fact and the committed
      `content/diagnostics/perception-contact-tick-005.{ascii.txt,svg}` golden;
      `FS0025` sites listed in the ledger with their fixes.
- [x] Every pre-existing `SightTests` / `PathfindingTests` / `ScenarioTests` /
      `ReplayTests` fact passes (only pinned-hash literals and `canonical 2 ->
      3` strings changed); `SimulationTests` / `CorpusTests` / `FixtureTests` /
      `DiagnosticsTests` / `DeterminismPropertyTests` pass with only
      pinned-hash values and additive facts changed.
- [x] `dotnet build CommandoWar.slnx -c Release` = 0/0;
      `dotnet list src/CommandoWar.Sim package --include-transitive` =
      `FSharp.Core 10.1.303` only; source scan of `src/CommandoWar.Sim` clean
      (matches are pre-existing doc-comment prose only).
- [x] `docs/04` sections 10 / 11 / 12.3 / 12.4 / 14 / 17 / 20, `docs/05`
      section 3, `docs/09` sections 2.2 / 2.3 / 2.4, backlog row
      (B-015 `-> done`; B-016 / B-017 / B-019 / B-022 unblocked note),
      ledger index row + detail file, `PROJECT_STATE.yaml`. "Pinned facts"
      `Canonical.FormatVersion` `-> 3`, "Shared fixture" hashes, "Green tests"
      `206 -> 216`.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`
- `dotnet test CommandoWar.slnx -c Release` before any edit and after (state
  the new count and each added fact / property)
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus` and
  `-- fixture` before any edit (record the current hashes) and after
  `--regenerate` (record the new hashes; confirm tick/event counts unchanged
  for the seven pre-existing entries)
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus
  --regenerate` then `git diff content/replays` — every pre-existing `.md`
  shows only hash-column and initial/final-hash changes; the new entry's
  files are added; re-running `--regenerate` is a zero diff
- `dotnet run --project src/CommandoWar.Headless -c Release -- render ...` to
  regenerate every affected `content/diagnostics/` golden; `git diff` shows
  only footer/hash changes on the pre-existing ones
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
- source scan of `src/CommandoWar.Sim`
- `git status --porcelain` — matches "Allowed scope"; nothing under the
  client spikes, `bench/`, or `src/_scratch`

## Evidence to capture

- test summary (before/after counts); the new `SimulationTests` facts and the
  new property by name and case count;
- the `Canonical.FormatVersion` `2 -> 3` bump and, in a table, every
  re-pinned entry's old and new initial/final hash with its unchanged
  tick count and event count (the behaviour-neutrality proof, TASK-018 style);
- the new corpus entry's initial/final hash, tick count, event count, and the
  observation it exercises;
- the `KnownContact` overlay golden and the `FS0025` sites with their fixes;
- the `PerceptionConfig` values chosen (`SightRange`, confidence bands,
  `StaleAfter`, `ExpireAfter`) and the one-line reason for each;
- if Dave chose the narrowed cut (Decision 3 latitude), what moved to B-016.

## Rollback or removal

`VisibleContacts`, `TacticalKnowledge`, the two phase bodies, the events, the
overlay, the new corpus entry, and the tests are additive to behaviour but the
`Canonical.FormatVersion` bump is a data-format change. Reverting means:
delete the phase bodies (phases return to no-op), the fields, the events, the
overlay and its renderer branches, the new entry and goldens, and the tests;
**revert `Canonical.FormatVersion` to `2`**, remove the tactical-knowledge
section from `encode`, and regenerate every hash table and golden back to the
format-2 values (`git revert` of the re-pin commit, or `--regenerate` after
the code revert). Record the revert re-pin in the ledger. Because the bump is
behaviour-neutral, the format-2 hashes are exactly today's committed values.

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md` (TASK-026 row; B-015 `proposed -> ready` at draft,
  `-> done` on completion; B-016 / B-017 / B-019 / B-022 unblocked note);
- `docs/12_PROGRESS_LEDGER.md` index row + `docs/ledger/` detail file;
  "Pinned facts": `Canonical.FormatVersion` `2 -> 3`, "Shared fixture"
  hashes, "Green tests" count;
- `docs/04_SIMULATION_SPEC.md` sections 10, 11, 12.3, 12.4, 14, 17 (and 20 if
  an invariant is added);
- `docs/05_COMMAND_AND_AGENT_AI.md` section 3;
- `docs/09_TEST_STRATEGY.md` sections 2.2, 2.3, 2.4;
- `PROJECT_STATE.yaml` only if this becomes the active task;
- no ADR (the ADR-0002 amendment already governs the format-version bump for
  newly-mutable authoritative state; this task follows that procedure and
  decides no new architecture, framework, determinism contract, or gate).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
