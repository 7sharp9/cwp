# TASK-062: Demolition objective, extraction, and mission success/failure

Status: done (accepted by Dave 2026-09-20, on the self-verification evidence -- "ok if you are happy then i am")
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature complete); realises backlog B-032
Size: L

## Objective

Wire up the Bridgehead mission's win/lose condition. `Simulation.fs`'s `Mission`
phase (`Phases.fs`) is an explicit no-op (`Mission -> ()`) reserved for exactly
this; `Scenario.fs`'s `Objective`/`ScenarioRules` algebra is fully authored and
validated already but nothing evaluates it (`ReachArea`, `HoldArea`,
`DestroyTarget`, `ExtractAgents`, `AllOf`, `Optional`, and
`ScenarioRules.FailOnFriendlyForceEliminated` are all data-only today). This
task makes the Mission phase real: a friendly agent can plant a demolition
charge on the bridge target (a fixed-duration occupancy, no new order type),
required survivors can extract, and the mission resolves to a one-shot
`Succeeded`/`Failed` outcome with matching domain events. `bridgehead.cwscenario`
is extended from its TASK-061 placeholder single "reach" objective to the real
mission content this proves.

## Why this task exists

B-025 (TASK-061) explicitly deferred all mission-sequence content: "B-025
stays scoped to terrain + deployments only, no authored `Objectives[]`... `
mission-sequence content is B-032's job." `docs/07_VERTICAL_SLICE.md` section 3
names the objective sequence (reach an observation position, neutralise/
bypass/suppress the MG threat, plant a charge on the bridge, hold while it's
planted, withdraw survivors, detonate) and explicitly licenses simplifying
detonation timing "if it distracts from the command loop." B-030/B-031
(ammunition, casualties, leader succession) — B-032's own listed
dependencies — are now done, so this is the first task where evaluating that
sequence is actually possible.

## Design decisions

Confirmed with Dave via `AskUserQuestion` (one round, four questions) before
drafting:

- **Charge-planting is occupancy-based, not a new order type.** Any `Alive`
  `Friendly` agent standing on the target's own cell for a configured number
  of consecutive ticks satisfies `DestroyTarget` — whether the player parked
  it there via a fulfilled `MoveTo` or an explicit `Hold`. No new
  `PlayerIntent`, no new appraisal/executor, no interrupt-cancels-progress
  rule beyond "the counter resets to 0 the instant no qualifying agent
  occupies the cell." Matches docs/07's explicit "may simplify detonation
  timing" license and section 4's required-commands list (`MoveTo`/
  `HoldArea`/`SuppressArea`/`AssaultArea`/`WithdrawTo` only — no
  `PlantCharge`).
- **Failure is exactly `ScenarioRules.FailOnFriendlyForceEliminated` wired
  up, nothing more.** No tick-limit/timeout concept is added — none exists
  anywhere in the sim today and docs/07 doesn't call for one.
- **Extraction is sticky.** Once a required agent is `Alive` and on an
  authored extraction-area cell, it counts as extracted permanently, even if
  it later walks off that cell. Matches "withdraw survivors, then the
  mission ends" rather than "loiter at the extraction point."
- **Bridgehead's placeholder content is replaced with real mission content
  now**, not deferred further: the existing `bridge-charge` `StaticTarget`
  and `extraction` `Area` (already authored in TASK-061) get real objectives
  referencing them; the existing optional `reach observation` placeholder is
  kept as-is (flavour/progress only, not a success gate — matches its
  existing `IsOptional=true`).

The following are my own implementation-level decisions, made to keep this
buildable without inventing speculative machinery, not separately put to
Dave — flag any of these at review if they don't match intent:

- **Completion is sticky and uniform across every objective kind**,
  generalising the extraction answer rather than special-casing it. A new
  canonical `WorldState.CompletedObjectives: ObjectiveId[]` (ascending) is
  the one place "has this objective ever been satisfied" lives; once an id
  is added it is never removed. `ReachArea` completes the instant a
  qualifying agent occupies its cell; `HoldArea`/`DestroyTarget` complete
  when a new canonical `WorldState.ObjectiveProgress: (ObjectiveId * int)[]`
  consecutive-tick counter (reset to 0 on any tick with no qualifying
  occupant, the `Suppression`/`Stress` decay-precedent shape) reaches the
  authored tick count; `ExtractAgents` completes when every required agent
  (aliveness-filtered — see below) has a new canonical per-agent
  `AgentState.Extracted: bool` (the `RadioDestroyed`/`PendingDelivery`
  precedent) set `true`; `AllOf` completes when every part's own id is in
  `CompletedObjectives`; `Optional` tracks/completes exactly like its inner
  objective (using the inner's own id) but is excluded from the top-level
  success gate below. This is a genuine simplification versus giving each
  kind its own bespoke completion representation, and matches docs/04
  section 12.10's existing (never-implemented) spec text verbatim: "evaluate
  objective conditions; emit completion or failure once; prevent accidental
  repeated completion events."
- **`AgentState.Extracted` is one global per-agent flag**, not tracked
  per-(objective, area) pair: "has this agent been `Alive` on any authored
  `WorldState.ExtractionAreas` cell at some point." Bridgehead authors
  exactly one extraction area, so this is not a real limitation here;
  disambiguating multiple distinct `ExtractAgents` objectives against
  different extraction areas would need per-objective tracking and is not
  built (`AGENTS.md` "do not build speculative type machinery" — add it if a
  later scenario actually needs two).
- **Success gate**: iterate the scenario's top-level `Objectives` array; for
  every entry that is not `Optional(...)`, its own id must be in
  `CompletedObjectives`. An `Optional`-wrapped entry is tracked and can
  complete (visible in diagnostics/events) but never blocks success.
- **Mission outcome is one-way**: a new canonical `WorldState.MissionOutcome
  = InProgress | Succeeded | Failed`. Once not `InProgress`, the Mission
  phase is a no-op for the rest of the run — no re-evaluation, no repeated
  events (the `SquadFailure` "health never regenerates" one-way precedent,
  generalised to the whole mission outcome). Failure is checked before
  success each tick (mirrors `StateConsequences`' own `LeadershipTransferred`
  then `SquadFailure` "specific facts, then the terminal signal" ordering);
  the two are not expected to coincide in practice (a fully eliminated
  friendly force cannot simultaneously have just completed a live-agent
  extraction), but the order is still explicit and tested.
- **Failure re-derives "every Friendly agent is non-`Alive`" directly from
  `WorldState.Agents`**, the same computation `StateConsequences` already
  makes to emit the existing signal-only `SquadFailure` event, rather than
  reading that event from this tick's own trace — keeps the Mission phase's
  input strictly `WorldState`, the pattern every other phase already follows.
- **`Objective.DestroyTarget` gains a `ticks: int` field** (`DestroyTarget of
  objective: ObjectiveId * target: TargetId * ticks: int`), the exact "fixed
  duration plant" the type's own doc comment already names as future work
  ("the bridge-demolition interaction is `ReachArea` then a fixed-duration
  plant (a later task) then `DestroyTarget`"). The RawObjective grammar needs
  no new column: `HoldTicks` already exists on every objective line and is
  documented "ignored otherwise" for `destroy` — this task starts reading it
  for that kind instead of adding one. `ScenarioContent.Version` bumps 6 -> 7
  (a validation-rule change: `destroy` now requires `HoldTicks > 0`,
  `NonPositivePlantTicks`), `Canonical.FormatVersion` bumps for the new
  canonical fields above.
- **New domain events**: `ObjectiveCompleted of objective: ObjectiveId`,
  `AgentExtracted of agent: AgentId`, `MissionSucceeded`, `MissionFailed`.
  Emission order within the Mission phase (last phase before `Output`):
  every `AgentExtracted` this tick (ascending agent id), then every
  `ObjectiveCompleted` this tick (ascending objective id), then at most one
  of `MissionSucceeded` / `MissionFailed`.
- **New `Diagnostics.Overlay` case** (the mandatory diagnostics-extension
  rule, `AGENTS.md`): `Mission of outcome: MissionOutcome * completed:
  ObjectiveId[] * inProgress: (ObjectiveId * int)[]`, rendered as a text
  summary line in both `Ascii` and `Svg`/`Html` (the `Divergence` overlay's
  non-spatial-summary precedent, not a per-cell marker).

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `docs/04_SIMULATION_SPEC.md` section 12.9 (State consequences — the
  `SquadFailure`/ammo-resupply "static authored data, excluded from
  Canonical" precedent) and section 12.10 (Mission — the never-implemented
  spec text this task realises verbatim)
- `docs/07_VERTICAL_SLICE.md` section 3 (objective sequence, the "may
  simplify detonation timing" license) and section 4 (required commands —
  no `PlantCharge`)
- `docs/11_BACKLOG.md` (B-032 row and its dependencies B-025/B-030/B-031, all
  `done`)
- `src/CommandoWar.Sim/Scenario.fs` (`Objective`, `ScenarioRules`,
  `AgentSelection`, `RawObjective`, `Scenario.validate`'s objective-building
  loop around line 703 — the exact site that constructs `DestroyTarget`
  today)
- `src/CommandoWar.Sim/Phases.fs` and `Simulation.fs`'s `runPhase` (`Mission
  -> ()`, the exact no-op this task replaces) and `Simulation.step`
- `src/CommandoWar.Sim/Events.fs` (`EventBody`, `SquadFailure`'s own doc
  comment: "Consuming it into a mission-failure outcome is B-032's job", and
  the full per-tick emission-order comment above `DomainEvent`)
- `src/CommandoWar.Sim/Domain.fs` (`WorldState`, `AgentState`, the
  `ResupplyAreas`/`Headquarters`/`Jammers` "static authored data, excluded
  from Canonical" precedent to mirror for `Objectives`/`Rules`/
  `ObjectiveAreas`/`ExtractionAreas`/`StaticTargets`; the `RadioDestroyed`/
  `PendingDelivery` "genuine per-agent canonical state" precedent to mirror
  for `Extracted`)
- `src/CommandoWar.Sim/Simulation.fs` (`World.ofScenario` around line 40-150
  — the exact site that threads `Scenario` fields onto `WorldState`; the
  `stateConsequences` phase's `SquadFailure`/`LeadershipTransferred`
  ordering and its "every Friendly agent non-Alive" check to mirror)
- `src/CommandoWar.Sim/Canonical.fs` (`FormatVersion`, the encode ordering
  convention for a new sparse ascending-by-id array field — the
  `TacticalKnowledge`/`HostileTacticalKnowledge` precedent)
- `src/CommandoWar.Sim/Diagnostics.fs` (`Overlay`, especially `Divergence` —
  the non-spatial summary-overlay precedent to mirror, not a per-cell
  marker) and `src/CommandoWar.Headless/DiagnosticRender.fs` (`Divergence`'s
  Ascii/Svg/Html rendering, around lines 493, 1040, 1362)
- `content/scenarios/bridgehead.cwscenario` (the existing placeholder
  objective, and the already-authored `bridge-charge`/`mg-position` targets
  and `extraction` area this task's new objectives reference)
- `src/CommandoWar.Sim/ScenarioFile.fs` (the `objective` grammar line,
  `HoldTicks`'s existing "ignored otherwise" column)
- `tasks/TASK-058-COMMUNICATION-RANGE-DELAY-JAMMING-AND-RADIO-DESTROYED.md`
  (the closest precedent: new canonical per-agent field + new static
  authored WorldState data + a new terminal phase's worth of logic, bundled
  in one task)

## Dependencies

- none remaining (B-025, B-030, B-031 are all `done`)

## Inputs and assumptions

- `Canonical.FormatVersion` is 12 before this task; it must bump (new
  canonical fields: `WorldState.MissionOutcome`, `.CompletedObjectives`,
  `.ObjectiveProgress`, `AgentState.Extracted`).
- `ScenarioContent.Version` is 6 before this task; bumps to 7 (the `destroy`
  kind's new `HoldTicks > 0` validation rule).
- No client (Godot) UI names or renders mission outcome anywhere yet. This
  task is Sim-side only, the TASK-058/059 precedent — a scenario (Bridgehead)
  and the headless corpus/diagnostics prove it, not a new HUD screen. A
  mission-complete/failure client screen is `docs/07` section 6's own listed
  future presentation work, not this task.
- `AllOf` gets generic evaluation support (needed for an exhaustive match
  over `Objective`) but no new authoring support: `RawObjective`/
  `Scenario.validate`'s builder still constructs only `ReachArea`/
  `HoldArea`/`DestroyTarget`/`ExtractAgents`, optionally `Optional`-wrapped,
  exactly as today. Nothing requires `AllOf` for this task's own content.

## Allowed scope

- `src/CommandoWar.Sim/Scenario.fs` (`DestroyTarget` gains `ticks: int`; the
  `"destroy"` build case reads `o.HoldTicks`; a new `NonPositivePlantTicks`
  validation error; `ScenarioContent.Version` 6 -> 7 and its doc comment);
- `src/CommandoWar.Sim/Domain.fs` (`WorldState`: `Objectives`, `Rules`,
  `ObjectiveAreas`, `ExtractionAreas`, `StaticTargets` — static, excluded
  from `Canonical.encode`; `MissionOutcome`, `CompletedObjectives`,
  `ObjectiveProgress` — genuine canonical state; a new `MissionOutcome` DU;
  `AgentState.Extracted` — genuine canonical per-agent state; `Agent.create`
  default);
- `src/CommandoWar.Sim/Simulation.fs` (`World.ofScenario` threading the new
  static fields; a new `mission` function implementing the phase; `runPhase`
  wiring `Mission -> mission s`);
- `src/CommandoWar.Sim/Events.fs` (`ObjectiveCompleted`, `AgentExtracted`,
  `MissionSucceeded`, `MissionFailed`; the `DomainEvent` ordering doc comment
  extended with the Mission phase's own slot);
- `src/CommandoWar.Sim/Canonical.fs` (`FormatVersion` bump; encoding the new
  fields, ascending-by-id for the two array fields — the
  `TacticalKnowledge` precedent);
- `src/CommandoWar.Sim/Diagnostics.fs` (new `Mission` `Overlay` case);
- `src/CommandoWar.Headless/DiagnosticRender.fs` (Ascii/Svg/Html rendering
  for the new overlay, the `Divergence` precedent);
- `content/scenarios/bridgehead.cwscenario` (replace the single placeholder
  objective line with three: the existing optional `reach observation`,
  plus non-optional `destroy` against `bridge-charge` and `extract` against
  `extraction`; update the file's own header comment block, which currently
  documents the placeholder-only state as B-032's job);
- `content/replays/*`/corpus (a new corpus entry — or entries — proving a
  full mission run to `Succeeded` and a separate one to `Failed`; decide by
  inspection whether one scripted run covering both via two commands is
  clearer than two entries, do not add more than needed);
- `content/diagnostics/*` (new goldens covering the new overlay, including
  an in-progress plant/hold counter and a completed/succeeded state);
- `tests/CommandoWar.Sim.Tests/*.fs` (`ScenarioTests` facts for the new
  validation rule and the `DestroyTarget` shape change; `SimulationTests`
  facts for occupancy-based planting, its reset-on-vacate behaviour,
  sticky extraction, sticky completion, the one-way outcome transition, and
  both success and failure end-to-end);
- `docs/04_SIMULATION_SPEC.md` section 12.10 (replace the never-implemented
  spec bullets with the realised design, keeping its existing three-bullet
  shape as confirmed correct by this task, not rewritten wholesale);
- control-document updates (this task, `docs/11_BACKLOG.md` B-032 row,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`).

## Forbidden scope

- A new `PlayerIntent`/command type for planting or detonating. Reuse
  `MoveTo`/`Hold` exactly as authored today.
- Any change to `Pathfinding.fs`, `Appraisal.fs`'s existing stage logic, or
  `Commitment`'s existing staged FSMs (`Assault`/`Withdraw`).
- A mission tick-limit/timeout mechanism, or any new `ScenarioRules` field
  beyond what's needed to read the existing `FailOnFriendlyForceEliminated`.
- `AllOf` authoring support in `RawObjective`/the `.cwscenario` grammar (a
  new grammar column or kind). Generic evaluation support only.
- Any Godot client UI, HUD, mission-complete screen, or scene change
  (Sim-side only, the TASK-058/059 precedent).
- Retroactively fixing `HoldArea`'s own pre-existing unchecked `HoldTicks`
  (no positivity validation exists for `"hold"` today) — out of scope, not
  introduced or worsened by this task.
- A new package, project, or dependency.

## Required work

1. Add `NonPositivePlantTicks` validation and thread `ticks` onto
   `DestroyTarget` in `Scenario.fs`; bump `ScenarioContent.Version` to 7 with
   an updated doc comment (the TASK-058/059 precedent for documenting each
   version bump inline).
2. Add the new `WorldState`/`AgentState` fields (`Domain.fs`) and thread the
   static ones through `World.ofScenario` (`Simulation.fs`).
3. Implement the `mission` phase function: update `ObjectiveProgress`
   counters and `AgentState.Extracted` from this tick's post-
   `StateConsequences` `Agents`/positions; recursively resolve
   `CompletedObjectives` across the full `Objective` algebra (`ReachArea`/
   `HoldArea`/`DestroyTarget`/`ExtractAgents`/`AllOf`/`Optional`); evaluate
   the top-level success gate and the friendly-force-eliminated failure
   check (failure first); set `MissionOutcome` and emit the new events in
   the documented order, only while `MissionOutcome = InProgress`.
4. Wire `Mission -> mission s` into `runPhase`.
5. Bump `Canonical.FormatVersion`; encode the new fields (ascending by id
   for the two array fields, the `TacticalKnowledge` precedent).
6. Add the `Diagnostics.Overlay.Mission` case and its `Ascii`/`Svg`/`Html`
   rendering (the `Divergence` non-spatial-summary precedent).
7. Replace `bridgehead.cwscenario`'s placeholder objective line with the
   real three-objective content; update its header comment.
8. Add `ScenarioTests` facts for the new validation rule; add
   `SimulationTests` facts covering: occupancy-based plant progress and its
   reset on vacating the target cell; sticky extraction surviving an agent
   walking off the extraction cell; sticky `CompletedObjectives` surviving
   the same; a full scripted run reaching `MissionSucceeded` exactly once
   with no further Mission-phase state change on later ticks; a full
   scripted run (or a targeted `WorldState` construction) reaching
   `MissionFailed` via every friendly agent down.
9. Add a new corpus entry (or entries) proving a full mission end to end;
   regenerate and commit the matching `content/diagnostics/*` golden(s)
   covering the new overlay, including one mid-plant in-progress frame.
10. Verify no pre-existing corpus/fixture entry's tick-by-tick sequence
    changes (none authors a `destroy`/`extract` objective with real ticks
    today — the Bridgehead scenario is content, not corpus) — confirm by
    diff, not assumption; if any corpus scenario does author objectives,
    re-derive and explain every changed line.
11. Update `docs/04` section 12.10, backlog, ledger, `PROJECT_STATE.yaml`.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] A friendly agent standing on `bridge-charge`'s cell for the authored
      tick count satisfies that `DestroyTarget` objective exactly once,
      proven by a `SimulationTests` fact; vacating the cell before the count
      is reached resets progress to 0, proven by a second fact.
- [x] Once satisfied, `DestroyTarget`'s completion is sticky: the agent
      later leaving the cell does not un-complete it — proven by a fact
      that advances further ticks after vacating and reasserts
      `CompletedObjectives` still contains it.
- [x] `ExtractAgents(AllFriendlyAgents, extraction)` is satisfied only once
      every currently-`Alive` friendly agent has been `Alive` on the
      extraction cell at some point (a `Dead`/`Incapacitated` agent is
      excluded from the requirement) — proven by a fact with a mixed
      surviving/casualty roster.
- [x] A full scripted run reaches `MissionOutcome = Succeeded` and emits
      `MissionSucceeded` exactly once, with Mission-phase state unchanged on
      every later tick — proven by the new `demolition-success` corpus
      entry (two goldens: an in-progress mid-plant frame, tick 3, and a
      `Succeeded` frame, tick 14) and a `SimulationTests` fact. (Not against
      `bridgehead.cwscenario` itself, which has no scripted command log to
      drive it to completion — its own content still imports and validates
      cleanly with the real three-objective set.)
- [x] A run in which every friendly agent becomes non-`Alive` reaches
      `MissionOutcome = Failed` and emits `MissionFailed` exactly once,
      never both `Succeeded` and `Failed` — proven by a `SimulationTests`
      fact, and for free by the pre-existing `casualties-succession-and-
      squad-failure` corpus entry (already authors
      `fail-on-friendly-eliminated true` and already scripts a full
      friendly wipeout).
- [x] `Scenario.validate` rejects a `"destroy"` objective with
      `HoldTicks <= 0` with `NonPositivePlantTicks`, no silent default —
      proven by a `ScenarioTests` fact.
- [x] `ScenarioContent.Version` is 7; `Canonical.FormatVersion` is bumped
      from 12 to 13 and documented — confirmed by `cwheadless fixture`.
- [x] A new sparse `Overlay.MissionStatus` case (renamed from the drafted
      `Mission` — collided with the pre-existing `Phase.Mission` case, see
      the ledger detail's Deviations) exposes outcome, completed
      objectives, and in-progress counters, with committed goldens under
      `content/diagnostics/` including one mid-plant frame (`AGENTS.md`
      diagnostics-extension rule).
- [x] `bridgehead.cwscenario` imports cleanly under `cwheadless import`
      (exit 0) with its new three-objective content.
- [x] `dotnet build CommandoWar.slnx -c Release` = 0 warnings, 0 errors.
- [x] `dotnet list src/CommandoWar.Sim package --include-transitive` =
      `FSharp.Core` only; no forbidden dependency entered
      `CommandoWar.Sim`.
- [x] `docs/04` section 12.10, `docs/11_BACKLOG.md` (B-032 row),
      `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`, this task file all
      updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`
- `dotnet test CommandoWar.slnx -c Release` before and after
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
  before and after; `--regenerate` then re-run for idempotence
- `dotnet run --project src/CommandoWar.Headless -c Release -- import content/scenarios/bridgehead.cwscenario`
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
- `git diff --stat` on every regenerated corpus/fixture `.md` — confirm
  every pre-existing entry's tick-by-tick sequence is unchanged, only the
  new entry differs
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
- source scan of `src/CommandoWar.Sim` for a forbidden dependency
- `git status`

## Evidence to capture

- command output or test summary;
- the new corpus entry's `.cwlog`/`.md` and the matching
  `content/diagnostics/*` goldens (including the mid-plant frame);
- confirmation (diff) that every pre-existing corpus/fixture entry is
  byte-identical apart from any mechanical hash/format footer;
- unresolved failures, if any.

## Expected files

- `src/CommandoWar.Sim/Scenario.fs`, `Domain.fs`, `Simulation.fs`,
  `Events.fs`, `Canonical.fs`, `Diagnostics.fs`
- `src/CommandoWar.Headless/DiagnosticRender.fs`
- `content/scenarios/bridgehead.cwscenario`
- `content/diagnostics/*`, `content/replays/*` (or `content/fixtures/*`)
- `tests/CommandoWar.Sim.Tests/*.fs`
- `docs/04_SIMULATION_SPEC.md`, `docs/11_BACKLOG.md`,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`, this task file

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md` (B-032 row: `proposed` -> `done`, task reference);
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file);
- `PROJECT_STATE.yaml` (`active_work`; pinned facts for
  `Canonical.FormatVersion`/`ScenarioContent.Version` if those are pinned
  values);
- `docs/04_SIMULATION_SPEC.md` section 12.10 (replace the never-implemented
  bullets with the realised design).

## Rollback or removal

Additive to `Scenario.fs`/`Domain.fs`/`Events.fs` (new fields, new DU cases,
one new phase function) plus one content-file edit
(`bridgehead.cwscenario`). `Canonical.FormatVersion` and
`ScenarioContent.Version` bumps are one-way by this project's own
convention (`docs/04` section 16: "does not guess migrations") — reverting
the commit reverts both cleanly since no scenario or corpus entry outside
this task's own new one depends on the new fields.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
