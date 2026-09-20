## 2026-09-19 - TASK-062 - Demolition objective, extraction, and mission success/failure

**Owner:** implementing agent (self-verified; Dave's acceptance pending)
**Source revision:** working tree on `main`, on top of `70fb14b` (TASK-061 accepted)
**Environment:** Windows x64, .NET SDK `10.0.303`
**Status change:** `tasks/TASK-062-DEMOLITION-OBJECTIVE-EXTRACTION-AND-MISSION-OUTCOME.md`
`drafted -> review`; `docs/11_BACKLOG.md` B-032 row `selected -> done`

### Changes

- `src/CommandoWar.Sim/Domain.fs`: moved `ObjectiveId`/`AreaId`/`TargetId`
  (types + smart-constructor modules), `Area`, `StaticTarget`,
  `AgentSelection`, `Objective` (its `DestroyTarget` case gains a `ticks: int`
  field), and `ScenarioRules` here from `Scenario.fs` -- the `Jammer`/
  `QueueMode` precedent: `WorldState` needs to carry them and `Domain.fs`
  compiles before `Scenario.fs`. Added `type MissionOutcome = InProgress |
  Succeeded | Failed`. `WorldState` gains five static authored fields
  (`Objectives`, `ObjectiveAreas`, `ExtractionAreas`, `StaticTargets`,
  `Rules` -- excluded from `Canonical.encode`, the `ResupplyAreas`/
  `Headquarters` precedent) and three genuine canonical fields
  (`MissionOutcome`, `CompletedObjectives: ObjectiveId[]`,
  `ObjectiveProgress: (ObjectiveId * int)[]`, both arrays ascending by id).
  `AgentState` gains `Extracted: bool` (genuine canonical, the
  `RadioDestroyed` precedent); `Agent.create` defaults it `false`.
- `src/CommandoWar.Sim/Scenario.fs`: the moved types replaced with a
  pointer comment; `"destroy"`'s build case now reads `o.HoldTicks`
  (already-existing grammar column, previously ignored for this kind),
  validates it `> 0` (`NonPositivePlantTicks`, new error case), and
  constructs `DestroyTarget(id, target, ticks)`. `ScenarioContent.Version`
  6 -> 7 (a validation-rule change, not a new authored field).
- `src/CommandoWar.Sim/Simulation.fs`: `World.build`/`.create`/`.ofScenario`
  thread the five new static `WorldState` fields (`create` supplies empty
  defaults + `FailOnFriendlyForceEliminated = false`); a new `private
  mission` phase function (~90 lines) implements docs/04 section 12.10 for
  real: sticky `AgentState.Extracted` update (emits `AgentExtracted`), a
  recursive bottom-up resolver over the `Objective` algebra (`ReachArea`
  instant; `HoldArea`/`DestroyTarget` via a consecutive-occupancy counter,
  reset to 0 on any tick with no qualifying `Alive` `Friendly` occupant,
  never stored at 0; `ExtractAgents` via every required, still-`Alive`
  agent's own `Extracted` flag; `AllOf`/`Optional` generic but unauthored),
  a failure check (`Rules.FailOnFriendlyForceEliminated` and every Friendly
  non-`Alive`, re-derived directly from `WorldState.Agents`, not read back
  from this tick's own `SquadFailure` event) checked before the success
  gate (every non-`Optional` top-level `Objectives` entry's id in
  `CompletedObjectives` -- **guarded by `Objectives.Length > 0`**, see
  Deviations). `StepState` carries the five static fields plus mutable
  `MissionOutcome`/`CompletedObjectives`/`ObjectiveProgress`. `Phases.fs`
  itself is unchanged (`Mission` was already the last phase before
  `Output`); `runPhase`'s `Mission -> ()` becomes `Mission -> mission s`.
- `src/CommandoWar.Sim/Events.fs`: four new `EventBody` cases
  (`AgentExtracted`, `ObjectiveCompleted`, `MissionSucceeded`,
  `MissionFailed`); the `DomainEvent` per-tick ordering comment gained a
  ninth numbered item for the Mission phase's own emission order.
- `src/CommandoWar.Sim/Canonical.fs`: `FormatVersion` 12 -> 13.
  `writeAgent` gained `AgentState.Extracted`; `encode`/`topLevelSections`
  gained a `writeMission` section (`MissionOutcome` as an int tag,
  `CompletedObjectives` then `ObjectiveProgress`, both explicitly re-sorted
  ascending by `ObjectiveId.value` at encode time regardless of the
  caller's own ordering -- the `TacticalKnowledge` precedent).
- `src/CommandoWar.Sim/Diagnostics.fs`: new `Overlay.MissionStatus of
  outcome * completed * inProgress` (named to avoid colliding with the
  pre-existing `Phase.Mission` case -- both live in the same `namespace
  CommandoWar.Sim` with no module wrapper, and a first attempt using the
  name `Mission` compiled every producer site but broke every *consumer*
  match in `CommandoWar.Headless` with `error FS0001: expected Overlay but
  here has type Phase`, since unqualified `Mission` resolved to the wrong
  DU there; renamed before it went any further). Sparse emission (the
  `AgentSuppression`/`AgentRadioLost` precedent): only present when
  `MissionOutcome <> InProgress`, or `CompletedObjectives`/
  `ObjectiveProgress` is non-empty -- every one of the 16 pre-existing
  corpus entries and the shared fixture author no `Objectives`, so it never
  appears for them.
- `src/CommandoWar.Headless/Program.fs`: `NonPositivePlantTicks`'s
  `describeScenarioError` line.
- `src/CommandoWar.Headless/DiagnosticRender.fs`: `MissionStatus` handling
  in every exhaustive `Overlay` match (the sightRay/plannedPath wildcard
  lists, the Ascii text-overlay branch, the SVG per-overlay match) plus a
  new SVG footer line (the `Divergence`-footer precedent -- no single cell
  to anchor a mission-wide summary on).
- `src/CommandoWar.Headless/AppraisalDemo.fs`: a `MissionStatus` case in
  the disposable P3 demo's `unhandled`-overlay enumeration (the
  `AgentFormationSlot` precedent -- exposed-approach's committed tick-1
  frame never reaches any objective this early, so this never actually
  fires for it).
- `src/CommandoWar.Headless/Corpus.fs`: new `demolition-success` entry
  (`corpus-demolition-success`), hand-built `RawScenario` outside the
  shared `ScenarioSpec` builder (which always authors exactly one
  non-optional `reach` objective, `rawOf`'s own hardcoded `Objectives` --
  the `LosDemo`/`PathDemo` bespoke-scenario precedent instead). One
  friendly agent (0,0), a `charge` target at (3,0), an `exit` extraction
  area at (10,0); `DestroyTarget` (2-tick plant) then `ExtractAgents`
  (`AllFriendlyAgents`), both non-optional. Two `MoveTo` commands
  (tick 1 -> (3,0); tick 8 -> (10,0), issued well after the plant
  completes to keep the two objectives' own ticks visually distinct).
  Tick numbers (arrival tick 3, plant completes tick 4, extraction reached
  and `MissionOutcome -> Succeeded` tick 14) were found by running it
  (`dotnet fsi`, removed after use), not derived by hand.
- `content/scenarios/bridgehead.cwscenario`: `content-version` 6 -> 7;
  the placeholder `objective 1 reach observation - 0 true -` line is joined
  by `objective 2 destroy - bridge-charge 10 false -` and
  `objective 3 extract extraction - 0 false -` (both non-optional); header
  comment rewritten to describe the real mission content instead of
  pointing at B-032 as future work.
- Tests: `tests/CommandoWar.Sim.Tests/ScenarioTests.fs` (two new facts:
  `DestroyTarget` carries `HoldTicks` as its plant duration;
  `NonPositivePlantTicks` on a non-positive `"destroy"` `HoldTicks`, no
  silent default; three pre-existing content-version-literal facts
  mechanically updated 6 -> 7 / 99 -> new-wrong-version; the shared
  `goodRaw()` fixture's own `destroy` objective's `HoldTicks` 0 -> 5, the
  smallest fix keeping it valid under the new rule; `fixtureScenario`'s own
  doc comment and its two pinned-hash facts corrected -- see Deviations),
  `SimulationTests.fs` (eight new Mission-phase facts, see the ledger index
  row for the full list), `DiagnosticsTests.fs` (two new facts for the
  `demolition-success` entry, an in-progress tick-3 golden and a
  `Succeeded` tick-14 golden; every pre-existing exhaustive `Overlay` match
  without a wildcard gained a `MissionStatus _` arm),
  `DeterminismPropertyTests.fs` (two hand-built `WorldState` literals
  gained the new fields, defaulted inert), `FixtureTests.fs`/
  `CanonicalHashTests.fs`/`PathfindingTests.fs`/`SightTests.fs`/
  `TerrainTests.fs`/`ReplayTests.fs`/`CorpusTests.fs` (every literal pinned
  hash or `Canonical.FormatVersion = 12` mechanically re-pinned to the
  format-13 values).

### Verification

- `dotnet build CommandoWar.slnx -c Release` -> `0 Warning(s), 0 Error(s)`
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug` -> `0/0`
- `dotnet build src/CommandoWar.Client.Mibo/CommandoWar.Client.Mibo.slnx -c Debug` -> `0/0`
- `dotnet test CommandoWar.slnx -c Release` -> `Passed: 408, Failed: 0` (395 -> 408, +13)
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus` -> `OK - all 18 entries match their committed tables`
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate` run twice; `md5sum` of every `content/replays/*.md`/`*.cwreplay` identical between the two runs (idempotent)
- `dotnet run --project src/CommandoWar.Headless -c Release -- import content/scenarios/bridgehead.cwscenario` -> `ok`, `objectives : 3`
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture` -> format 13, initial hash `0xC0A53D46AE5D7C80`, final hash (tick 40) `0x447C32A5D599EAB3`, 36 events, matches `FixtureTests.fs`'s re-pinned array
- `dotnet run --project src/CommandoWar.Headless -c Release -- replay-file content/replays/envelope-full.cwreplay` -> `checkpoints : OK (24 ticks match the file's committed hashes)`
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive` -> `FSharp.Core` only
- `grep -rniE "godot|monogame|raylib|mibo" src/CommandoWar.Sim/*.fs` -> only pre-existing doc-comment mentions, no forbidden dependency

### Evidence

- `content/replays/demolition-success.{cwreplay,md}` (new corpus entry) and
  `content/diagnostics/demolition-success-tick-{003,014}.{ascii.txt,svg}`
  (new goldens: tick 3 shows `mission: in-progress  in-progress 1:1`
  mid-plant; tick 14 shows `mission: succeeded  completed 1,2` plus the
  `agent-extracted`/`objective-completed:2`/`mission-succeeded` event
  trace).
- `content/replays/casualties-succession-and-squad-failure.{cwreplay,md}`
  and its two `content/diagnostics/casualties-succession-and-squad-failure-
  tick-{005,065}.*` goldens (regenerated): tick 65's event trace now reads
  `...; squad-failure; mission-failed` -- this entry already authored
  `fail-on-friendly-eliminated true` and already scripts the friendly side
  down to zero; wiring up the Mission phase surfaces a genuine new event on
  pre-existing content, not a new corpus entry.
- Every other corpus/diagnostics/fixture golden re-pinned to its new,
  byte-layout-only hash (`Canonical.FormatVersion` 13); confirmed via
  `git status --porcelain content/replays content/diagnostics` showing
  modifications only, no unexpected additions/deletions beyond the two new
  `demolition-success` files.
- `content/scenarios/bridgehead.cwscenario` re-imported clean with its new
  three-objective content (`cwheadless import`, exit 0).

### Deviations and unresolved issues

- **A real vacuous-success bug found and fixed before it reached a test.**
  The most direct implementation of the success gate --
  `Objectives |> Array.forall (fun o -> ...)` -- is `true` on an empty
  array by definition. Since `World.create`/`Setup.sixAgentWorld` (every
  pre-existing test fixture and the shared spike fixture) build a
  `WorldState` with no `Objectives` at all, that naive gate would have
  declared `MissionOutcome = Succeeded` on tick 1 of nearly every existing
  test in the suite. Caught by running the test suite immediately after
  wiring the phase in (34 failures, most reading `hash changed` but a
  cluster reading a spurious extra `mission-succeeded` event), traced to
  this, and fixed with one `Objectives.Length > 0 &&` guard -- an empty
  `Objectives` array now means "no win condition authored," matching this
  task's own "Inputs and assumptions" section's framing, not "trivially
  won."
- **A second, subtler instance of the same class of gap, in test content
  rather than product code.** `ScenarioTests.fs`'s `fixtureScenario()` --
  a `Scenario`-validated expression of the shared six-agent fixture, built
  to pin `World.ofScenario`'s hash against the same well-known fixture --
  authors one real, non-optional `reach observation` objective at
  `(20, 14)`, which is *also* agent 3's own scripted `MoveTo` target. Its
  own doc comment said outright: "the token objective and extraction area
  exist only to satisfy the required markers; `World.ofScenario` reads
  neither, so they cannot affect the hash." That was true before this
  task and is false after it: agent 3 reaches `(20, 14)` at tick 31
  (`SPIKE-FIXTURE.md`'s own documented arrival tick), so
  `Simulation.mission` now completes that objective and reaches
  `MissionOutcome = Succeeded` the same tick. Confirmed by running it
  (`dotnet fsi`), not assumed; the doc comment and both pinned-hash facts
  were corrected in place rather than treated as a plain re-pin.
- **`Overlay`/`Phase` naming collision.** `Diagnostics.fs`'s new overlay
  case was first named `Mission`, identical to the pre-existing
  `Phases.fs` `Phase.Mission` case, both in `namespace CommandoWar.Sim`
  with no module wrapper. Every *producer* site (inside `Diagnostics.fs`,
  which opens nothing extra) resolved correctly, but every *consumer*
  match in `CommandoWar.Headless` (`DiagnosticRender.fs`,
  `AppraisalDemo.fs`) resolved the bare `Mission` identifier to `Phase`
  instead, failing with `error FS0001: This expression was expected to
  have type 'Overlay' but here has type 'Phase'`. Renamed to
  `MissionStatus` before any of those sites were hand-fixed around the
  wrong type.
- **`RawObjective.HoldTicks`'s column is now genuinely overloaded**: it
  means "hold duration" for `"hold"` and "plant duration" for
  `"destroy"`, reusing one grammar column across two kinds rather than
  adding a new one (the smallest change, and the column's own doc comment
  already said "ignored otherwise" for exactly this reason). `"hold"`'s
  own `HoldTicks` still has no positivity validation -- a pre-existing gap,
  not introduced or worsened here, left alone (`AGENTS.md` scope
  discipline).
- **`AgentState.Extracted` is one global per-agent flag**, not tracked
  per-(objective, area) pair -- a scenario authoring two distinct
  `ExtractAgents` objectives against two different extraction areas is not
  specially disambiguated. Documented in the field's own doc comment and
  the task file; not exercised by any authored content today.
- **Bridgehead's authored `10`-tick plant duration and the
  `demolition-success` entry's `2`-tick one are both first-cut authoring
  choices**, not derived from any balancing pass -- flagged for Dave to
  retune if the feel is wrong once played.
- Not independently re-verified through the real Godot editor (no client
  change this task, the TASK-058/059 Sim-side-only precedent) -- confirmed
  instead by a clean `CommandoWar.Client.Godot.slnx` Debug build.

### Documents updated

- `tasks/TASK-062-DEMOLITION-OBJECTIVE-EXTRACTION-AND-MISSION-OUTCOME.md`
  (drafted before implementation; acceptance criteria left as authored,
  Dave's own review will check them off)
- `docs/11_BACKLOG.md` (B-032 row: `selected -> done`)
- `docs/04_SIMULATION_SPEC.md` (section 12.10 realised design; section 21's
  "objective evaluation... deferred" line corrected)
- `docs/12_PROGRESS_LEDGER.md` (this row; Pinned facts --
  `Canonical.FormatVersion`, `ScenarioContent.Version`, green-test count)
- `PROJECT_STATE.yaml` (`active_work`)

### Review

Accepted by Dave 2026-09-20 on the self-verification evidence above --
"ok if you are happy then i am". No changes requested; nothing re-run live
(no client change this task). Committed to `main` at Dave's instruction.
