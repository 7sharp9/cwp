## 2026-09-07 - TASK-026 - Observations and shared squad tactical knowledge

**Owner:** Dave with coding-agent assistance
**Branch:** `task-026-perception-and-tactical-knowledge` off `main` at `62da2b0`
(TASK-025 merge on top). Committed locally; not pushed.
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; .NET 10.0.11;
FSharp.Core 10.1.303; xUnit 2.9.3; FsCheck 3.3.4
**Status change:** TASK-026 `ready -> review`; backlog B-015 `ready -> done`

Realises P3 Required work "communication and shared tactical knowledge"
(`docs/08` section 6) and contributes G3 evidence: two agents can now appraise
the same intent differently because they hold different knowledge. First phase
consumer of the `Sight` module (TASK-012). No ADR — the ADR-0002 amendment
"Static authoritative data and the canonical image" already specifies the
`Canonical.FormatVersion` bump procedure for newly-mutable authoritative state,
and this task follows it.

### Central decisions (confirmed with Dave before the encoder / phase bodies)

- **Decision 3 — full retained decaying store.** Not the narrowed cut.
  `WorldState.TacticalKnowledge` holds the retained picture with
  `LastSeenTick` and a decaying `Confidence`; the Tactical-knowledge phase
  ages contacts (`StaleAfter`) and removes them (`ExpireAfter`, emitting
  `ContactExpired`). It landed at ~40 lines in a new leaf module, so the task
  stayed size **M**; nothing moved to B-016.
- **Decision 5 — `PerceptionConfig` module literals** (`Perception.fs`; not on
  `SimConfig`, which is non-authoritative):
  | constant | value | reason |
  |---|---|---|
  | `SightRange` | `10` | Chebyshev radius cap. Chebyshev not Manhattan because `Sight` is a diagonal-capable supercover walk, so a diamond cap would exclude diagonal contacts the walk reaches. 10 bounds 50-agent perception and leaves a slice map's far side unseen (the "Unknown threat" scenario). |
  | `ConfidenceFull` | `1000` | Full confidence on the `docs/04` section 4 `0..1000` scale, assigned whenever a friendly sees the contact this tick. |
  | `ConfidenceBandDrop` | `250` | One band. A stale contact drops once to `750`; not a per-tick decay — `docs/05` section 3 speaks of a "confidence band" and few integer steps are easier to tune. |
  | `StaleAfter` | `20` | ~1 s at 20 tps. Longer than any brief line-of-sight flicker, so a blinking contact does not thrash (`docs/05` section 15 hysteresis). |
  | `ExpireAfter` | `60` | ~3 s unseen before the contact is dropped and `ContactExpired` fires. Exceeds `StaleAfter`. |
- **Decision 6 — symmetric visibility, friendly-only knowledge.** Both sides
  get `AgentState.VisibleContacts` and both emit `ContactObserved`; only
  friendly observations merge into `WorldState.TacticalKnowledge`. A hostile
  squad picture, enemy doctrine reacting to contacts, and "the enemy does not
  target an unobserved position" stay B-022.

### Changes

- **`src/CommandoWar.Sim/Domain.fs`.** New `Contact` record
  (`Contact: AgentId`, `LastKnownCell: Cell`, `LastSeenTick: int64`,
  `Confidence: int`). `AgentState.VisibleContacts: AgentId[]` — a
  **non-canonical derived cache**, doc comment explaining the `Route`-parallel
  reasoning. `WorldState.TacticalKnowledge: Contact[]` — genuine canonical
  state, doc comment. `Agent.create` initialises `VisibleContacts = [||]`.
- **`src/CommandoWar.Sim/Perception.fs` (new leaf module).**
  `PerceptionConfig` literals; `Perception.chebyshev`;
  `Perception.visibleContactsFor` (opposing agents within `SightRange` and
  `Sight.visible`, ascending by id); `Perception.sweep` (all agents, both
  sides); `Perception.mergeKnowledge tick prior seenThisTick -> Contact[] *
  Contact[]` (upsert seen at full confidence, drop a band after `StaleAfter`,
  remove after `ExpireAfter`; returns the new store and the removed contacts).
  Pure, total, integer-only, `Sight` + `Terrain` + `Domain` only. Inserted
  after `Domain.fs` in the fsproj.
- **`src/CommandoWar.Sim/Events.fs`.**
  `ContactObserved of observer: AgentId * contact: AgentId * at: Cell` and
  `ContactExpired of contact: AgentId * lastKnownCell: Cell`. Ordering doc
  comment extended: command outcomes, then this tick's `ContactObserved`
  (ascending `(observer, contact)`), then `ContactExpired` (ascending contact
  id), then movement outcomes.
- **`src/CommandoWar.Sim/Simulation.fs`.** `StepState.TacticalKnowledge`.
  Real `perception` phase (copy agents, `Perception.sweep`, emit
  `ContactObserved` for contacts newly in an agent's visibility, write
  `VisibleContacts`) and `tacticalKnowledge` phase (friendly `VisibleContacts`
  mapped to current cells, `Perception.mergeKnowledge`, emit `ContactExpired`
  per removal). `runPhase` — `Perception` / `TacticalKnowledge` moved from the
  no-op list to real arms. `step` carries `TacticalKnowledge` into `acc` and
  `finalState`. `World.build` initialises it `[||]`. Phase header comments
  ("Realised by TASK-026").
- **`src/CommandoWar.Sim/Canonical.fs`.** `FormatVersion` `2 -> 3` (doc
  comment). New `writeContact` (contact id, cell x/y, `LastSeenTick`,
  `Confidence`; fixed-width big-endian). `encode` gains a tactical-knowledge
  section after the agents — explicit count, then contacts ascending by id.
  `topLevelSections` gains `"TacticalKnowledge"`, so `firstDifferingSection`
  names it. `AgentState.VisibleContacts` is deliberately **not** written (the
  `Route` precedent).
- **`src/CommandoWar.Sim/Diagnostics.fs`.**
  `Overlay.KnownContact of cell: Cell * contact: AgentId * confidence: int *
  lastSeenTick: int64` and its doc-comment reservation line.
  `knownContactOverlays (world)` derives one per contact ascending by id, used
  by **both** `frame` and `frameOf` (the squad picture is on `WorldState`).
  `eventMarker` gains `contact-observed` / `contact-expired` markers.
- **`src/CommandoWar.Headless/DiagnosticRender.fs`.** `KnownContact` branches:
  `Ascii` prints `known contact (x,y): agent N  confidence C  seen tick T`
  (text only, the `Reserved` / `Obstructed` precedent); `Svg` draws a purple
  (`#805ad5`) dashed ring plus a `?<id>` label at the last-known cell,
  deliberately distinct from a live agent circle.
- **`src/CommandoWar.Headless/Corpus.fs`.** `rawScenario` generalised with an
  `enemies` parameter (`[]` for all seven existing builders — behaviourally
  identical). New `opaqueWall` helper. New `perceptionContactWorld` (12 x 8:
  friendly 0 at (1,5) ordered east to (9,5); stationary hostile 1 at (9,1)
  behind an opaque impassable wall at x = 6, rows 0..3) and its
  `perception-contact` `Entry` (14 ticks).
- **`content/replays/`.** New `perception-contact.cwlog` + `.md`
  (`corpus --regenerate`-generated). Re-pinned `.md` tables for all seven
  existing `.cwlog` entries (hash columns only). Re-pinned
  `envelope-full.cwreplay` (`canonical 2 -> 3`, `initial-hash`, 24
  `checkpoint` lines) and `envelope-full.md` (hand-maintained — not in
  `Corpus.all`). `CORPUS.md` gained the `perception-contact` row and a
  TASK-026 re-pin note on the `envelope-full` section.
- **`content/fixtures/SPIKE-FIXTURE.md`.** Re-pinned: format `3`, initial
  `0x50BFA007EDFC42FE`, final `0xD9D6EC3DDC1D602F`, the full 40-row table; 33
  events / tick 40 unchanged.
- **`content/diagnostics/`.** Every hash-footer golden regenerated. New
  `perception-contact-tick-005.{ascii.txt,svg}` (tick 5: the friendly clears
  the wall, two `contact-observed` markers, the `KnownContact` overlay). The
  `demo.*` goldens: `demo.ascii.txt` / `demo.svg` are `Diagnostics.frame` at
  tick 0 so they moved only in the footer, but `demo.html` embeds the whole
  run and now shows the `KnownContact` overlay and `contact-observed` event
  rings on ticks 8-20 — a genuine behaviour change, allowed because
  `DemoScenario` is not one of the seven `.cwlog` entries or the fixture (see
  "Deviations"). `README.md` gained the `perception-contact` row and a note.
- **`tests/CommandoWar.Sim.Tests/`.**
  - `SimulationTests.fs`: `perceptionWorld` / `opaqueCells` / `contactOf`
    helpers and five facts — clear LOS observes + shares (and no re-emit on a
    second visible tick); an opaque cell blocks entirely; beyond `SightRange`
    not observed (with a `dx = 10` control); two friendlies share the contact
    though only one saw it; seen-then-lost drops a band at
    `lastSeen + StaleAfter` and expires with `ContactExpired` at
    `lastSeen + ExpireAfter`.
  - `DeterminismPropertyTests.fs`: `TacticalKnowledge = [||]` added to the
    `randomCaseGen` `WorldState` literal (compile-only). New
    `perceptionCaseGen` (1-3 friendlies + 1-2 hostiles on distinct passable
    cells), `statesOf`, and property 6 (`MaxTest = 200`): every squad contact
    was genuinely visible to a friendly on its `LastSeenTick` (recomputed via
    `Perception.visibleContactsFor` over the pre-tick state), within
    `ExpireAfter`, `LastSeenTick` non-decreasing while the contact survives,
    `Confidence` one of the two bands. Properties 1-5 unmodified.
  - `CanonicalHashTests.fs`: `Assert.Equal(3, Canonical.FormatVersion)`;
    comment refreshed; new fact "the tactical-knowledge section is in the
    canonical image and firstDifferingSection names it".
  - `DiagnosticsTests.fs`: FS0025 fixes at the two `Overlay` match sites; a
    hand-built `KnownContact` overlay fact; the `perception-contact` golden
    fact (`perceptionContactFrames`, KnownContact overlay + `contact-observed`
    marker at tick 5, no overlay at tick 4, byte-equal goldens); fixture hash
    literals re-pinned.
  - `FixtureTests.fs`: `h.Format` `2 -> 3`; initial hash; the 40-value
    per-tick array.
  - `CorpusTests.fs`, `PathfindingTests.fs`, `ScenarioTests.fs`,
    `SightTests.fs`, `TerrainTests.fs`: fixture hash literals re-pinned;
    `Canonical.FormatVersion` `2 -> 3` assertions.
  - `ReplayTests.fs`: `canonical 2 -> 3` in three inline strings; fixture
    initial-hash literal; the 24-value `envelopeFullHashes` array.
- **Docs.** `docs/04` sections 10 (WorldState `TacticalKnowledge`), 11
  (`AgentState.VisibleContacts`), 12.3 / 12.4 (realisation blocks), 14
  (`ContactObserved` / `ContactExpired`), 17 (the format-3 note), 20 (a new
  perception invariant). `docs/05` section 3 realisation note. `docs/09`
  sections 2.2 (property 6), 2.3 (the "Unknown threat" partial-realisation
  note), 2.4 (the eighth corpus entry). `docs/11` TASK-026 row, B-015
  `-> done`, B-016 / B-017 / B-019 / B-022 unblocked note. `docs/12` index row
  + this file + "Pinned facts" (`Canonical.FormatVersion` `2 -> 3`, shared
  fixture hashes, "Green tests" `206 -> 216`). `PROJECT_STATE.yaml`
  `active_work`. `tasks/TASK-026-*.md` (Outcome, Status, acceptance boxes).

### FS0025 incomplete-match sites and their fixes

Adding `EventBody` cases and an `Overlay` case forced new arms. Items 1-7 were
added proactively (no build error surfaced); 8-9 surfaced as warnings (the test
project does not treat FS0025 as an error). Each was fixed with the intended
branch, never a wildcard.

| # | site | fix |
|---|---|---|
| 1 | `Diagnostics.fs` `eventMarker` (`EventBody` match) | `ContactObserved(_,_,at) -> { Kind = "contact-observed"; Cells = [| at |] }`; `ContactExpired(_,cell) -> { Kind = "contact-expired"; Cells = [| cell |] }` |
| 2 | `Diagnostics.fs` `reservationOverlays` (`e.Body` filter) | `| ContactObserved _ | ContactExpired _ -> None` |
| 3 | `Diagnostics.fs` `obstructionOverlays` (`e.Body` filter) | `| ContactObserved _ | ContactExpired _ -> None` |
| 4 | `DiagnosticRender.fs` `Ascii` `sightRays` filter | `| KnownContact _ -> None` |
| 5 | `DiagnosticRender.fs` `Ascii` `plannedPaths` filter | `| KnownContact _ -> None` |
| 6 | `DiagnosticRender.fs` `Ascii` overlays print `match o` | `| KnownContact(cell, contact, confidence, lastSeenTick) -> line (...)` |
| 7 | `DiagnosticRender.fs` `Svg` overlays `match o` | `| KnownContact(cell, contact, _, _) -> <circle r=6 #805ad5 dashed/> + <text>?N</text>` |
| 8 | `DiagnosticsTests.fs:288` `convergingRoutes` `Array.tryPick` | `| KnownContact _ -> None` |
| 9 | `DiagnosticsTests.fs:348` `swapStandoff` `Array.choose` | `| KnownContact _ -> None` |

### The `Canonical.FormatVersion` 2 -> 3 re-pin (behaviour-neutrality proof)

Every committed replay scenario except `perception-contact` has **zero enemy
deployments**, so `WorldState.TacticalKnowledge` is empty at every checkpoint
and the moved hashes are a byte-layout artifact of the new (always
zero-length) tactical-knowledge section — exactly the TASK-018 `Progress`
situation. **Tick counts and event counts are byte-identical** after the
re-pin, confirmed by `git diff content/replays` (only `Initial hash` /
`Final hash` / per-tick hash-column lines changed; no `Tick count` or
`Domain events` line moved) and by `cwheadless corpus` (8/8 PASS).

| entry | ticks | events | old initial (fmt 2) | old final | new initial (fmt 3) | new final |
|---|---:|---:|---|---|---|---|
| `spike-fixture` | 40 | 33 | `0xE13D7540912C7E25` | `0xAFA35198CC6BD8D4` | `0x50BFA007EDFC42FE` | `0xD9D6EC3DDC1D602F` |
| `wall-detour` | 24 | 19 | `0xF499BD05E1570D2D` | `0x23A672AC1206B006` | `0xD33F1F626C50BD78` | `0x299F40A7C31AFBE3` |
| `blocked-goal` | 5 | 2 | `0x63DE59EA977B1B9B` | `0x2498DD43D5A6BC62` | `0x9D83E88BAF8CC5C6` | `0x5A7EE35462931F17` |
| `converging-routes` | 12 | 19 | `0x749D0E7BE45B8D90` | `0xA817BB8DD7AF58FA` | `0xBE15331B04AEBE03` | `0x385CEB7D96FB1415` |
| `slow-terrain` | 8 | 6 | `0xBD92C9B2CBF21236` | `0x76CD78F7F3F125BA` | `0x3C60E54D7AFA43FB` | `0xE3F93C765A20C547` |
| `follow-chain` | 6 | 21 | `0xC34E382E3968F165` | `0x6D9DCBB66731E8E4` | `0xEC12A1D3F1E445C8` | `0x49C721DD840E675D` |
| `swap-standoff` | 4 | 10 | `0xB5CCF07F2E62B941` | `0x883E04E8894D97E0` | `0x36334E44259BB6D6` | `0xAEBB18C290AF465F` |
| `envelope-full` (`.cwreplay`) | 24 | 72 | `0xE13D7540912C7E25` | `0x5028174266E2BF6F` | `0x50BFA007EDFC42FE` | `0x4E5963A2C8C83660` |

`SPIKE-FIXTURE.md` shares `spike-fixture`'s hashes. Every hash-footer golden
under `content/diagnostics/` was regenerated; on the pre-existing ones only the
`hash 0x… (format N)` / `hash 0x…  draws` footer lines changed (verified by
`git diff`), the sole exception being `demo.html` (see Deviations).

### New corpus entry: `perception-contact`

12 x 8, seed 20260904, 1 friendly + 1 hostile. Friendly 0 at (1,5) ordered to
(9,5); stationary hostile 1 at (9,1) behind an opaque impassable wall at
x = 6, rows 0..3. Ticks 1-4: the hostile is inside `SightRange` (Chebyshev
8 <= 10) but the wall blocks line of sight. **Tick 5:** the friendly reaches
(5,5) at perception time, line of sight opens, `ContactObserved` fires both
ways (perception is symmetric) and the Tactical-knowledge phase adds contact 1
to `WorldState.TacticalKnowledge` at `(9,1)`, `LastSeenTick = 5`,
`Confidence = 1000`. Ticks 6-14: the friendly keeps line of sight, so
`LastSeenTick` advances each tick and `ContactObserved` is **not** re-emitted.

- Initial hash `0xDAA3BCA323164178`, final hash `0x320C6FE6BC2544DF`.
- 14 ticks, 12 domain events (1 `CommandAccepted` + 8 `MovementStepped` +
  1 `MovementCompleted` + 2 `ContactObserved`).
- Golden `content/diagnostics/perception-contact-tick-005.{ascii.txt,svg}`.

### Verification

- `dotnet build CommandoWar.slnx -c Release`
  - `Build succeeded. 0 Warning(s) 0 Error(s)`.
- `dotnet test CommandoWar.slnx -c Release` (before any edit)
  - `Passed! - Failed: 0, Passed: 206`.
- `dotnet test CommandoWar.slnx -c Release` (after the field additions +
  `FormatVersion` bump, before any golden/literal update)
  - `Failed: 36, Passed: 171` — every failure a hash / golden / `canonical 2`
    mismatch, none a logic error (inspected the full list: `CorpusTests`,
    `FixtureTests`, `DiagnosticsTests`, `Canonical/Pathfinding/Scenario/Sight/
    Terrain/ReplayTests` re-pins only; no `SimulationTests`, no
    `DeterminismPropertyTests` property 1-5, no `Sight`/`Pathfinding` logic
    fact failed).
- `dotnet test CommandoWar.slnx -c Release` (after every re-pin + additive fact)
  - `Passed! - Failed: 0, Passed: 216`.
    New facts: five in `SimulationTests.fs` (named above); one
    `DeterminismPropertyTests` property "every squad contact was seen by a
    friendly within ExpireAfter, with a non-decreasing LastSeenTick and a valid
    confidence band" (`MaxTest = 200`); one `CanonicalHashTests` fact; one
    hand-built + one golden fact in `DiagnosticsTests.fs`; the
    `perception-contact` row of the `CorpusTests` `[<Theory>]`. `206 -> 216`.
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
  (before any edit) — `OK - all 7 entries match their committed tables`;
  fixture `canonical format : 2`, initial `0xE13D7540912C7E25`, final
  `0xAFA35198CC6BD8D4`, 33 events.
- `dotnet run ... -- corpus --regenerate` then `git diff content/replays`
  — every pre-existing `.md` shows only hash-column / initial-final-hash
  changes (`git diff --stat`: `113 insertions(+), 113 deletions(-)` across the
  seven); per-tick row counts unchanged (40/24/5/12/8/6/4); `perception-contact.*`
  added. `git add` + re-run `--regenerate` = zero diff (idempotent).
- `dotnet run ... -- corpus` (after) — `OK - all 8 entries match their
  committed tables`; fixture `canonical format : 3`, initial
  `0x50BFA007EDFC42FE`, final `0xD9D6EC3DDC1D602F`, **33 events unchanged**.
- `dotnet run ... -- replay-file content/replays/envelope-full.cwreplay`
  — `format : replay-command v1, canonical 3`; `checkpoints : OK (24 ticks
  match the file's committed hashes)`; final `0x4E5963A2C8C83660`; 72 events.
- `dotnet run ... -- render …` for every render-verb golden, plus the
  `DiagnosticRender.runFrames`-over-`Corpus.all` regeneration for the
  corpus-owned goldens — `git diff` on the pre-existing goldens shows only
  footer/hash lines, except `demo.html` (Deviations).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive` — `FSharp.Core 10.1.303` only.
- Source scan of `src/CommandoWar.Sim/*.fs` for
  `float|stopwatch|datetime|system\.random|godot` (case-insensitive) — 8
  matches, all pre-existing doc-comment prose ("no floating point", "a Godot
  `.tscn` reader", etc.); `Perception.fs` contributes none.
- `git status --porcelain` — matches the Changes list. Nothing under the
  client spikes, `src/_scratch`, `bench/`, or `content/benchmarks/`.

### Evidence

- **`Canonical.FormatVersion` is 3:** `cwheadless fixture` / `corpus` /
  `replay-file` all report it; `CanonicalHashTests` / `PathfindingTests` /
  `ScenarioTests` / `SightTests` assert it; `FixtureTests` asserts
  `h.Format = 3`.
- **The re-pin is behaviour-neutral:** the table above — tick counts and
  event counts identical for all eight pre-existing entries; the `git diff`
  and `cwheadless corpus` evidence in Verification.
- **Perception is grounded in `Sight` + range, not omniscience:** the
  `perception-contact` entry (contact unseen for 4 ticks behind the wall, in
  range the whole time); `SimulationTests` "an opaque cell … blocks the
  observation entirely" and "a hostile beyond SightRange … is not observed";
  property 6 recomputes visibility independently of the phase.
- **Instant squad sharing:** `SimulationTests` "two friendlies, only one with
  line of sight … share the contact the same tick".
- **Decay and expiry:** `SimulationTests` "a contact seen then lost drops a
  confidence band after StaleAfter and expires with ContactExpired after
  ExpireAfter"; property 6's band + `ExpireAfter` window checks.
- **Diagnostics standing rule satisfied:** `Overlay.KnownContact` derived in
  both `Diagnostics.frame` and `frameOf`, rendered in `Ascii` and `Svg`,
  pinned by a hand-built fact and the committed
  `perception-contact-tick-005.*` golden; `demo.html` also shows it.
- **Green count:** `dotnet test` `206 -> 216`.

### Deviations and unresolved issues

- **`DemoScenario` gains real perception behaviour.** It is the only committed
  scenario (corpus or demo) with a hostile deployment (agent 5 at (11,7),
  added long before this task). Once Perception runs, friendly agents 0 and 1
  observe it on ticks 8 and 9, so `demo.html` gains a `KnownContact` overlay
  and four `contact-observed` event rings on ticks 8-20, and its per-frame
  event count rises `30 -> 34` across the run. This is a genuine behaviour
  change, **not** a byte-layout re-pin — but `DemoScenario` is not one of the
  seven `.cwlog` corpus entries or the shared fixture, so it is not a
  stop-and-report finding. `demo.ascii.txt` / `demo.svg` render
  `Diagnostics.frame` at tick 0 (before any perception), so they moved only in
  the footer. This was flagged to Dave up front and is a useful bonus: a
  second committed `KnownContact` visualisation.
- **`envelope-full.{cwreplay,md}` re-pin was beyond the task file's "seven".**
  The task file was drafted before TASK-025 merged `envelope-full`.
  `ReplaySerialisation.parse` rejects a `canonical` value that does not equal
  `Canonical.FormatVersion` (`CanonicalFormatMismatch`), so the data file had
  to move to `canonical 3` with regenerated `initial-hash` and 24 `checkpoint`
  lines, `envelope-full.md` re-generated by hand (it is not in `Corpus.all`),
  and the `envelopeFullHashes` pin plus three inline `canonical 2` strings in
  `ReplayTests.fs` updated. No `ReplaySerialisation.fs` / `Replay.fs` / command
  code changed — this is the ADR-0002 "re-pin the pinned files" step, and it
  is behaviour-neutral (spike-fixture initial state; 24 ticks / 72 events
  unchanged).
- **`ContactObserved` fires per `(observer, contact)` pair.** On a tick where
  N friendlies newly see one contact, N `ContactObserved` events are emitted
  (one each), plus the hostile's own symmetric observation. This is bounded
  (once per pair per sighting, never re-emitted while visible) and is not the
  "flood" `docs/04` section 14 forbids, but a squad-level "first sighting"
  event could be a future refinement if the stream proves noisy.
- **`AgentMarker` was not given a visible-contact count.** The task listed it
  as optional; skipped to keep the golden diffs focused on `KnownContact`. The
  overlay and the `contact-observed` markers already make the state
  inspectable.
- **`bench/` perception slot (`docs/04` section 19, `docs/09` section 2.8)
  left commented.** A read-only 50-agent perception benchmark run was not done;
  the commented slot in `bench/CommandoWar.Benchmarks/Benchmarks.fs` and
  `content/benchmarks/BASELINE.md` are filled by whoever next touches the
  benchmark harness, as those files note.
- **No `SquadStore` / formations.** Every `Friendly` agent is treated as the
  squad (`docs/05` section 17, B-011d). Unchanged and unscoped by this task.

### Documents updated

- `tasks/TASK-026-PERCEPTION-AND-TACTICAL-KNOWLEDGE.md` (Outcome, Status
  `ready -> review`, acceptance boxes)
- `src/CommandoWar.Sim/{Domain,Perception (new),Events,Simulation,Canonical,
  Diagnostics}.fs`, `CommandoWar.Sim.fsproj`
- `src/CommandoWar.Headless/{DiagnosticRender,Corpus}.fs`
- `content/replays/` (seven `.md` re-pinned, new `perception-contact.{cwlog,md}`,
  `envelope-full.{cwreplay,md}` re-pinned, `CORPUS.md`)
- `content/fixtures/SPIKE-FIXTURE.md`
- `content/diagnostics/` (every hash-footer golden, new
  `perception-contact-tick-005.*`, `README.md`)
- `tests/CommandoWar.Sim.Tests/{SimulationTests,DeterminismPropertyTests,
  CanonicalHashTests,DiagnosticsTests,FixtureTests,CorpusTests,PathfindingTests,
  ScenarioTests,SightTests,TerrainTests,ReplayTests}.fs`
- `docs/04_SIMULATION_SPEC.md` (sections 10, 11, 12.3, 12.4, 14, 17, 20),
  `docs/05_COMMAND_AND_AGENT_AI.md` (section 3),
  `docs/09_TEST_STRATEGY.md` (sections 2.2, 2.3, 2.4)
- `docs/11_BACKLOG.md` (TASK-026 row; B-015 `-> done`; B-016 / B-017 / B-019 /
  B-022 unblocked note)
- `docs/12_PROGRESS_LEDGER.md` (index row; "Pinned facts"
  `Canonical.FormatVersion` `2 -> 3`, shared fixture hashes, "Green tests"
  `206 -> 216`)
- `PROJECT_STATE.yaml` (`active_work -> TASK-026`)
- this entry

### AGENTS.md / docs/09 section 8 standing rule

Applies. `WorldState.TacticalKnowledge` is new authoritative tactical state and
`AgentState.VisibleContacts` new derived spatial state. Both surface through
`Overlay.KnownContact` (a new `Overlay` case, derived in `Diagnostics.frame`
**and** `frameOf`), rendered in `DiagnosticRender.Ascii` and `.Svg`, and pinned
by a hand-built `DiagnosticsTests` fact and the committed
`content/diagnostics/perception-contact-tick-005.*` golden (plus `demo.html`).
Regeneration commands are in `content/diagnostics/README.md`.

### Review

- Reviewer: Dave
- Accepted: pending
