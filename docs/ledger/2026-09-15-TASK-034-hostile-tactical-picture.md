## 2026-09-15 - TASK-034 - Hostile tactical picture and observed-only targeting proof

**Owner:** Dave with coding-agent assistance
**Source revision:** `9012fce` (Confirm TASK-034 central decisions B-F) — the local
tip of `main`; work done on branch `task-034-hostile-tactical-picture`
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; .NET 10.0.11; xUnit
2.9.3; FsCheck / FsCheck.Xunit 3.3.4
**Status change:** `tasks/TASK-034-HOSTILE-TACTICAL-PICTURE.md` `ready ->
review -> done`; `docs/11_BACKLOG.md` TASK-034 row `ready -> review -> done`,
B-022 row `ready -> review` (stays `review`, partial); `PROJECT_STATE.yaml`
`active_work.selected_task` `none -> TASK-034 -> none`. No
`Canonical.FormatVersion` change beyond the task's own bump (`6 -> 7`).

### Changes

- `src/CommandoWar.Sim/Domain.fs`: `WorldState.HostileTacticalKnowledge:
  Contact[]`, the identical `Contact` shape as the friendly
  `TacticalKnowledge`.
- `src/CommandoWar.Sim/Simulation.fs`: `World.build` defaults the new field to
  `[||]`; `StepState` gains the mutable field; the `tacticalKnowledge` phase
  gains a second `Perception.mergeKnowledge` call filtered to `Side =
  Hostile` (a `seenBy side` helper replaces the friendly-only inline filter),
  emitting `ContactExpired` for the hostile store's own expiries (identical
  event shape, reused); `step`'s `acc` / `finalState` thread the field
  through. `Perception.fs` itself is unchanged (Decision B — it was already
  side-agnostic).
- `src/CommandoWar.Sim/Canonical.fs`: `FormatVersion` `6 -> 7`; `encode` and
  `topLevelSections` gain a `HostileTacticalKnowledge` section, ordered right
  after the friendly `TacticalKnowledge` section, reusing `writeContact`.
- `src/CommandoWar.Sim/Diagnostics.fs`: new `Overlay.HostileKnownContact` case
  (Decision C — distinct from `KnownContact`, the `AgentStress` /
  `AgentSuppression` precedent); a `hostileKnownContactOverlays` derivation
  function; wired into both `frame` and `frameOf`.
- `src/CommandoWar.Headless/DiagnosticRender.fs`: exhaustive-match arms for
  `HostileKnownContact` in the LOS-ray filter, the planned-path filter, the
  ASCII overlay renderer (`hostile known contact ...` line), and the SVG
  renderer (an amber `#b7791f` dashed ring labelled `H<id>`, deliberately
  distinct from `KnownContact`'s purple ring).
- `src/CommandoWar.Headless/AppraisalDemo.fs`: an exhaustive-match arm
  bucketing `HostileKnownContact` into the disposable demo's
  `UnhandledOverlays` list (the `AgentStress` / `AgentSuppression`
  precedent — this P3 demo predates the case).
- `src/CommandoWar.Client.Godot/src/AppraisalDemoScene.cs` +
  `src/CommandoWar.Client.Godot/README.md`: the pinned `--selfcheck` hash for
  `exposed-approach` tick 1 moved (`0x2066BC1FAF990E4A -> 0xB1EBA36EC0A977F4`)
  because the scenario's hostile now also carries a
  `HostileTacticalKnowledge` entry for the friendlies it sees from tick 1 —
  not independently re-verified in Godot this session (no Godot install in
  this environment); the value is the one `cwheadless` now reports for that
  frame.
- `tests/CommandoWar.Sim.Tests/`: full literal re-pin of every hardcoded
  `Canonical.FormatVersion` (`6 -> 7`) and fixture/corpus hash literal across
  `CanonicalHashTests.fs`, `CorpusTests.fs`, `FixtureTests.fs`,
  `PathfindingTests.fs`, `ReplayTests.fs`, `ScenarioTests.fs`,
  `SightTests.fs`, `TerrainTests.fs`, `DiagnosticsTests.fs`; exhaustive-match
  arms for `HostileKnownContact` added to `DiagnosticsTests.fs`'s four
  `Array.tryPick` / `Array.choose` overlay filters; `WorldState` record
  literals in `DeterminismPropertyTests.fs` gained the new field. New facts:
  one `CanonicalHashTests` fact (the hostile-tactical-knowledge section /
  `firstDifferingSection` label — the `TacticalKnowledge` fact's precedent)
  and one `SimulationTests` fact (Decision E's proof: a Hostile agent seeded
  with a stale `HostileTacticalKnowledge` entry for a friendly it can no
  longer see never appears as a shooter in that tick's `ShotFired` events).
  Extended in place: `DeterminismPropertyTests` property 6 (now also grounds
  `HostileTacticalKnowledge` against a Hostile agent's own observation, same
  generator); the perception-contact `DiagnosticsTests` fact (asserts the new
  `HostileKnownContact` overlay alongside the existing `KnownContact` one);
  the exposed-approach `AppraisalDemo` unhandled-overlay `DiagnosticsTests`
  fact (`6 -> 8` — the scenario's mutual tick-1 sighting now also produces
  two `HostileKnownContact` entries the demo doesn't render).
- `content/replays/*`: full re-pin via `cwheadless corpus --regenerate`
  (all 12 entries; tick counts and event counts unchanged everywhere) plus a
  manual re-pin of `envelope-full.cwreplay` / `envelope-full.md` (not part of
  `Corpus.all`, so outside the `corpus` verb — regenerated via `cwheadless
  replay-file` and hand-copied into both files, 24 ticks / 78 events
  unchanged).
- `content/diagnostics/*`: full re-pin — the `render`-verb-covered goldens via
  the committed regeneration commands (`content/diagnostics/README.md`), and
  the nine corpus-entry-scoped goldens (not covered by any CLI verb — each
  entry's initial state is corpus-owned, not the shared fixture) via a
  throwaway `dotnet fsi` script calling the identical `Corpus.loadLog` /
  `DiagnosticRender.runFrames` / `.Ascii` / `.Svg` helpers each
  `DiagnosticsTests.fs` fact uses, at the exact ticks the committed files
  name (script not committed — scratch only).
- `docs/04_SIMULATION_SPEC.md`: section 10 (`WorldState` field list) gains
  `HostileTacticalKnowledge`; section 12.4 gains the Hostile-side realisation
  note; section 12.8 (Combat) gains the observed-only-targeting proof note;
  section 17 gains a TASK-034 canonical-format note (after the TASK-028
  note); section 20 gains a hostile-picture grounding invariant bullet
  alongside the existing `TacticalKnowledge` one.
- `docs/05_COMMAND_AND_AGENT_AI.md`: section 3 gains the Hostile-side
  realisation note; section 12 (Enemy AI) gains the "observe and report" +
  targeting-proof realisation note and restates which of the six doctrine
  bullets stay open.
- `docs/09_TEST_STRATEGY.md`: section 8's "enemy does not target an
  unobserved player position" entry is corrected — the targeting half is now
  recorded as fully realised (citing TASK-034), narrowing "partially
  realised" to "observe and report" only.
- `docs/11_BACKLOG.md`: TASK-034 row `ready -> review` with implementation
  evidence; B-022 row `ready -> review` (two of six doctrine bullets now
  real; three stay open).
- `docs/12_PROGRESS_LEDGER.md`: new index row (this entry); `Canonical.
  FormatVersion` pinned fact corrected and extended through `7` (it had gone
  stale since TASK-028 — TASK-032's `5` and TASK-033's `6` bumps were never
  reflected there; backfilled from the source tasks' own ledger entries
  rather than left further behind); `Green tests` pinned fact `285 -> 287`
  with this task's contribution.
- `PROJECT_STATE.yaml`: `active_work` note prepended (see below);
  `active_work.selected_task` left at `none` per the same convention TASK-033
  used once its own work was recorded (the note carries the in-progress
  detail, not the `selected_task` field, since acceptance is a later,
  separate step).

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` (all four projects).
- Command: `dotnet test CommandoWar.slnx -c Release --no-build`
  - Result: `Passed! - Failed: 0, Passed: 287, Skipped: 0, Total: 287`.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus`
  - Result: `OK - all 12 entries match their committed tables` (all PASS).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus --regenerate` (twice in a row)
  - Result: identical `git diff --stat content/replays/` after both runs —
    byte-identical regeneration.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  fixture`
  - Result: `canonical format 7`; final hash `0x56395A49904D017D`; `events
  36`; agent 3 still reaches `(20, 14)`.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  replay-file content/replays/envelope-full.cwreplay`
  - Result: `checkpoints  : OK (24 ticks match the file's committed hashes)`.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
  - Result: `FSharp.Core 10.1.303` only.
- Manual check: `Grep` for `float|stopwatch|datetime|system\.random|godot`
  (case-insensitive) over `src/CommandoWar.Sim`.
  - Result: comment mentions only (the same eight lines the prior tasks'
    ledger entries already covered — no new hits).
- Command: `git status --porcelain`
  - Result: matches the task file's "Allowed scope" exactly (`src/
  CommandoWar.Sim/{Domain,Simulation,Canonical,Diagnostics}.fs`, `src/
  CommandoWar.Headless/{DiagnosticRender,AppraisalDemo}.fs`, `tests/
  CommandoWar.Sim.Tests/*.fs`, `content/replays/*`, `content/diagnostics/*`,
  the two named `src/CommandoWar.Client.Godot/` files, and the named
  `docs/`/`PROJECT_STATE.yaml` files).

### Evidence

- `content/replays/envelope-full.cwreplay` / `.md`: initial hash
  `0xBE2636723F99F53A`, final (tick 24) hash `0x13E2EC63C0A76FCD`, 78 events,
  canonical 7.
- `content/replays/perception-contact.md`: 39 domain events (unchanged);
  tick-5 hash moved to `0xB0072D4A340AE028`; the new `HostileTacticalKnowledge`
  entry (friendly agent 0, confidence 1000, seen tick 5) is visible in
  `content/diagnostics/perception-contact-tick-005.ascii.txt` as `hostile
  known contact (5,5): agent 0  confidence 1000  seen tick 5`.
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`, `` `a hostile with a
  stale HostileTacticalKnowledge contact for a friendly it can no longer see
  never fires on it` ``: the Decision E proof fact.

### Deviations and unresolved issues

- Godot: the pinned `--selfcheck` hash in `AppraisalDemoScene.cs` / `README.md`
  was updated to the value `cwheadless` reports for `exposed-approach` tick 1,
  but not independently re-run through the Godot editor/headless binary in
  this session (no Godot install available here). Dave should re-run
  `--selfcheck` on his machine before or during acceptance to confirm the
  Godot host actually reproduces `0xB1EBA36EC0A977F4`.
- `content/fixtures/SPIKE-FIXTURE.md` and `content/fixtures/spike-fixture.cwlog`
  (the disposable TASK-004/005 framework-spike artifact) are **not** in this
  task's allowed scope and were left untouched — they were already stale at
  `Canonical.FormatVersion` 4 before this task (never updated through
  TASK-030/031/032/033 either), a pre-existing gap, not a regression
  introduced here.
- The `docs/12_PROGRESS_LEDGER.md` "Shared fixture" reference table (also
  citing `content/fixtures/SPIKE-FIXTURE.md`) was left alone for the same
  reason — updating it without its source file would create a new
  inconsistency, not fix one.
- B-022 stays `review`, not `done`: suppress-likely-routes,
  seek-adjacent-cover-under-pressure, and scripted fall-back remain open,
  each needing a system this task deliberately did not build (B-030's order
  vocabulary, or a new autonomous-movement design) — see the task file's
  "Considered and rejected" section.

### Documents updated

- `tasks/TASK-034-HOSTILE-TACTICAL-PICTURE.md` (Outcome / Verification
  sections)
- `docs/04_SIMULATION_SPEC.md`, `docs/05_COMMAND_AND_AGENT_AI.md`,
  `docs/09_TEST_STRATEGY.md`
- `docs/11_BACKLOG.md` (TASK-034 row, B-022 row)
- `docs/12_PROGRESS_LEDGER.md` (this index row; `Canonical.FormatVersion` and
  `Green tests` pinned facts)
- `PROJECT_STATE.yaml` (`active_work` note)
- `content/diagnostics/README.md`: the `perception-contact-tick-005.*` and
  `exposed-approach-tick-001.*` table rows gained a sentence each for the new
  `hostile known contact` / `H<id>` overlay content. No new row added —
  `open-engagement-tick-001.*` was already missing a table row before this
  task (a pre-existing TASK-031 gap, left as found, outside this task's diff)
- this entry

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-15)
- Notes: re-verified before accepting: `dotnet build CommandoWar.slnx -c
  Release` 0/0; `dotnet test` `Passed: 287`; `cwheadless corpus` 12/12 PASS
  (`--regenerate` twice byte-identical); `cwheadless fixture` format 7, `36`
  events unchanged; `cwheadless replay-file
  content/replays/envelope-full.cwreplay` checkpoints OK at canonical 7, `78`
  events unchanged; `git status --porcelain` clean; `dotnet list
  src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  `FSharp.Core` only; source scan clean. All match the ledger detail above
  exactly. Merged to `main` (`--no-ff`, branch
  `task-034-hostile-tactical-picture` deleted; not pushed). B-022 stays
  `review`, not `done` — suppress-likely-routes,
  seek-adjacent-cover-under-pressure, and scripted fall-back remain open.
