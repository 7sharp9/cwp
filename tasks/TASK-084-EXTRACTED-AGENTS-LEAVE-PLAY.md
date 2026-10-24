# TASK-084: Extracted agents leave play

Status: review (selected 2026-10-22; self-verified; CI pending)  
Owner: coding agent  
Phase: P4  
Gate: G4 (defect repair; no evidence bullet depends on it); realises B-083  
Size: M

## Objective

An agent whose sticky `Extracted` flag is set is out of play: it holds no cell,
does not move, does not perceive, cannot be perceived or targeted, does not
fire, and its cell can be entered by another agent. It still counts as `Alive`
for `ExtractAgents`, mission success, the squad-eliminated check and squad
leadership. A second survivor ordered to an extraction cell that an extracted
agent already stands on arrives instead of stalling to `MovementAbandoned`.

This addresses D-02 in `docs/15_G4_DEFECT_TRIAGE.md`.

## Why this task exists

`Simulation.mission` sets `Extracted` and leaves `Position` untouched. The
agent stays `Alive`, stays in `occupantOf` (`Simulation.fs:1590`, which excludes
only non-`Alive` agents), and so blocks the extraction cell for everyone after
it. With one extraction cell a squad of N survivors can extract at most one
agent in a stationary configuration; Bridgehead has two cells and the
`bridgehead-succeeded` script steps each pair off (ticks 195 and 196) to work
around it.

Making only `occupantOf` skip extracted agents is not enough, because an
extracted agent would then share a cell with an agent that the enemy can see
and shoot. Every reader of "who is here" has to agree, so the task introduces
one predicate and applies it at each site listed below. The invariant in
`docs/04` section 20 ("never places two live agents on the same cell") is
restated for agents in play, which is why Dave must accept the task before it
is selected.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `docs/15_G4_DEFECT_TRIAGE.md` (D-02)
- `docs/04_SIMULATION_SPEC.md` sections 12.10 (Mission), 20 (Invariants), and
  the cell-occupancy bullet in the Navigation section
- `docs/05_COMMAND_AND_AGENT_AI.md` section 4 stage 2
- `docs/07_VERTICAL_SLICE.md` (Bridgehead extraction, criteria)
- `src/CommandoWar.Sim/Casualty.fs` (`isAlive`, `currentLeader`)
- `src/CommandoWar.Sim/Simulation.fs`: the Appraisal `occupied` sets (about
  lines 936 and 1184), Navigation (`intents` 1489, `occupantOf` 1590,
  `parkedCells` 1606), Combat (1957 to 1997), Mission (2197 to 2330)
- `src/CommandoWar.Sim/Perception.fs` (`visibleContactsFor`)
- `src/CommandoWar.Sim/Appraisal.fs` (`appraise`, the `vitals` check)
- `src/CommandoWar.Sim/Diagnostics.fs`, `content/diagnostics/README.md`
- `src/CommandoWar.Headless/Corpus.fs` (`bridgeheadSucceededCommands`),
  `content/replays/CORPUS.md`
- `tests/CommandoWar.Sim.Tests/DeterminismPropertyTests.fs` property 2b

## Dependencies

- TASK-062 (`Extracted`, Mission phase), TASK-066 (corpses do not block),
  TASK-078 (a down observer sees nothing), TASK-079 / B-079 (`ExtractAgents`
  semantics), TASK-081 (triage), TASK-083 (done).

## Inputs and assumptions

Assumptions to confirm in step 1 before editing:

- The stall reproduces at HEAD: two friendlies, one extraction cell, the second
  ordered to the cell after the first extracts, ends in `MovementAbandoned`.
- `Extracted` is set in Mission step (1), after Navigation, so an agent that
  enters the extraction cell on tick T is extracted on tick T and out of play
  from tick T+1's Perception onward.
- No shipped scenario authors `ReachArea` or `HoldArea` on an extraction area.
  (The corpus helper authors `reach` on the objective area; Bridgehead needs the
  same check against `bridgehead.cwscenario`.) If one does, Decision 3 matters
  for it.
- `Extracted` is already canonically encoded (`Canonical.fs`, `AgentState`), so
  this task needs no new `AgentState` field.

Design decisions made here (Dave may amend them before selection):

1. **Predicate.** Add `Casualty.inPlay (a: AgentState) = isAlive a.Vitals &&
   not a.Extracted`. `isAlive` keeps its meaning and its callers that mean
   "counts as alive" (squad-eliminated checks at `Simulation.fs` 2169, 2334 and
   2439, `currentLeader`, `ExtractAgents` satisfaction, the extraction flag's
   own `isAlive` test). The sites that mean "is physically present" switch to
   `inPlay`.
2. **Position stays.** An extracted agent keeps its `Position` (the extraction
   cell). Nothing moves it, the canonical layout is unchanged and the clients
   keep drawing it where it left play. "Holds no cell" means no other reader
   treats that cell as occupied. This is the cheapest option and avoids a
   sentinel position.
3. **Objective occupancy.** The backlog row lists "the objective occupancy
   check" as an `inPlay` site. Mission step (1) sets `Extracted` before step
   (2) evaluates occupancy, so a plain `inPlay` filter in `anyAliveFriendlyAt`
   would discount the agent on its arrival tick and could break a `ReachArea` or
   `HoldArea` authored on an extraction cell. Settled: evaluate occupancy over
   the pre-extraction snapshot (`s.Agents` before step 1) filtered by `inPlay`,
   so the arrival tick still counts and every later tick does not. A `HoldArea`
   on an extraction cell therefore resets once its holder extracts. If the
   assumption above holds (no content does this) the difference is only in
   tests.
4. **Orders to an extracted agent.** Settled as a command-intake rejection:
   `CommandRejection.RecipientExtracted` for an order and for a cancel alike,
   per recipient, after `UnauthorisedRecipient` (the `docs/04` section 13
   order). The draft offered a new `DecisionReason` with a
   `Canonical.FormatVersion` bump (a) or an accepted-and-inert order (b).
   Both were dropped: a typed reason held in `AgentState.Disposition` would
   bump the version and move every pinned hash for a case no existing state
   holds, and an inert order is a silent stall. A rejection is an event
   only, so the canonical image is unchanged and the order never reaches
   appraisal, matching `UnknownAgent` and `UnauthorisedRecipient`.
5. **Leadership.** `currentLeader` is unchanged: an extracted leader stays the
   leader. Nothing in the simulation requires the leader to be in play. Noted
   as a limitation in `docs/15` rather than fixed here.
6. **Corpses are out of scope.** `occupied` in the Appraisal formation
   resolution counts every other agent, corpses included. This task changes it
   for extracted agents only and does not touch the corpse behaviour.

## Allowed scope

- `Casualty.fs`: `inPlay`.
- `Simulation.fs`: switch the "physically present" sites to `inPlay`:
  Appraisal `occupied` sets (two, which skip `Extracted` only, Decision 6),
  Navigation `intents`, `occupantOf`, `parkedCells`, Mission occupancy per
  Decision 3, and the `RecipientExtracted` arms in `commandIntake`. Combat is
  not changed: `VisibleContacts` is rebuilt from `inPlay` agents every tick, so
  an extracted agent is never a shooter or a candidate there.
- `Perception.fs`: `visibleContactsFor` observer and target tests.
- `Commands.fs` (`CommandRejection.RecipientExtracted`) and the matching
  `Diagnostics.fs` event marker.
- `Diagnostics.fs` (`DiagnosticFrame`): a sparse overlay marking extracted
  agents' cells, rendered by `CommandoWar.Headless` only, plus a golden under
  `content/diagnostics/` (AGENTS.md diagnostics rule).
- Tests in `tests/CommandoWar.Sim.Tests/`: a focused test per site, the
  two-survivors-one-cell scenario, and property 2b restated for in-play agents.
- `Corpus.fs`: remove the step-off orders at ticks 195 and 196 from
  `bridgeheadSucceededCommands`, and regenerate affected entries.
- Documentation: `docs/04` section 20 (invariant) and the Mission and
  Navigation text and the section 13 rejection order, `docs/05` section 5,
  `docs/09`, `docs/07`, `docs/15` (D-02 repaired; the L-08 limitation),
  `content/replays/CORPUS.md`, `content/diagnostics/README.md`.

## Forbidden scope

- Moving an extracted agent, adding a sentinel position, or any new
  `AgentState` or `WorldState` field.
- Changing `isAlive`, `currentLeader`, the squad-eliminated checks, or the
  meaning of `ExtractAgents` and `MissionOutcome`.
- Changing corpse behaviour (Decision 6), Pathfinding, Combat resolution,
  PRNG draws, or map content.
- A `Canonical.FormatVersion` bump, a new `DecisionReason`, or a change to
  `Appraisal.appraise`.
- Client changes. The Godot client reads `Extracted` directly and keeps
  drawing the agent where it left play; verifying that needs the Godot SDK,
  which the container does not have, so it goes to `docs/15` as an unverified
  item.
- Any other open defect in `docs/15`, including D-03.

## Required work

1. Reproduce the stall at HEAD with a focused scenario and record the tick it
   ends in `MovementAbandoned`. If it does not reproduce, stop and report.
   Confirm the other assumptions above, including the Bridgehead objective
   check.
2. Add `Casualty.inPlay` and switch each listed site. Keep the diff to those
   sites; do not widen a site that reads "counts as alive".
3. Add `RecipientExtracted` to command intake (Decision 4).
4. Write the focused tests: stall repaired; an extracted agent is not
   perceived by an enemy in sight and does not fire at one; an in-play agent
   can enter an extracted agent's cell and share it; an extracted agent still
   satisfies `ExtractAgents` and does not trigger squad elimination; the
   arrival tick still counts for occupancy (Decision 3); determinism across
   repeated runs. Restate property 2b over in-play agents.
5. Add the sparse overlay and golden. A frame with two agents extracted onto
   one cell must be visible in the ASCII and SVG renders.
6. Remove the Bridgehead step-off orders. Run the full corpus, list every
   entry and golden that moves with the reason. `bridgehead-succeeded` must
   still reach `Succeeded`; the expectation is that the outcome is unchanged and
   the tick may change.
7. Update the documents listed below and record evidence in the ledger.

## Stop conditions

Stop and report to Dave, with the evidence, if:

- the stall does not reproduce as described;
- `bridgehead-succeeded` cannot reach `Succeeded` once the step-off orders are
  removed, or its outcome changes;
- an entry other than `bridgehead-succeeded` moves;
- the change needs an `AgentState` or `WorldState` field.

## Acceptance criteria

- [x] With one extraction cell, a second survivor ordered there after the first
      has extracted reaches it, with no `MovementAbandoned` (`a second survivor
      can enter an extraction cell an extracted agent stands on`). Before: agent
      1 obstructed at tick 4, `MovementAbandoned` at tick 43; after:
      `MovementCompleted` and `AgentExtracted` at tick 4.
- [x] Each site in "Allowed scope" has a test that fails without its change
      (mutation check: each site reverted alone in a scratch copy; the ledger
      lists the failing fact per site).
- [x] Extracted agents still count for `ExtractAgents`, mission success, the
      squad-eliminated check and leadership (`a lone Extracted agent still
      counts as alive ...`; the existing `ExtractAgents` facts).
- [x] The `docs/04` section 20 invariant is restated for agents in play, and
      property 2b matches it (edited; FsCheck is unavailable here, so CI
      compiles and runs it).
- [x] `bridgehead-succeeded` still reaches `Succeeded` at tick 215 with the
      step-off orders removed; the one moved corpus entry and the two moved
      goldens are listed with their reasons in the ledger.
- [x] A diagnostic golden shows extracted agents in the ASCII and SVG renders:
      `bridgehead-succeeded-tick-215.*` (three agents out of play on `(4,9)`)
      and `demolition-success-tick-014.*`, with `DiagnosticsTests` assertions.
- [x] `Sim`, `Headless` and Godot Core build with zero warnings; stand-in test
      run 393 passed, 0 failed. `dotnet test` was not run (no xunit or FsCheck
      offline); CI is authoritative.
- [x] `Canonical.FormatVersion` is unchanged (15).
- [x] No forbidden dependency or scope entered the change.
- [x] Required documentation was updated.

## Required verification

- `dotnet build src/CommandoWar.Headless -c Release -p:NuGetAudit=false`
- `dotnet test` for `tests/CommandoWar.Sim.Tests` (CI is authoritative if the
  packages are unavailable; report which checks did not run)
- `dotnet run --no-build -c Release --project src/CommandoWar.Headless -- corpus`
- `corpus --regenerate`, then a diff of `content/` listing every moved file
- the stall repro before and after, recorded as ledger text, not as code
- dependency boundary check: `CommandoWar.Sim` project references unchanged

## Evidence to capture

Commands, the stall timeline before and after, the list of moved corpus entries
and goldens with reasons, the Decision 4 outcome, and any unresolved concern:
`docs/ledger/<date>-TASK-084-extracted-agents-leave-play.md`.

## Expected files

- `src/CommandoWar.Sim/Casualty.fs`, `Commands.fs`, `Simulation.fs`,
  `Perception.fs`, `Diagnostics.fs`
- `src/CommandoWar.Headless/Corpus.fs`, `DiagnosticRender.fs`,
  `AppraisalDemo.fs`
- `tests/CommandoWar.Sim.Tests/` (simulation, perception, diagnostics, property
  tests)
- moved files under `content/replays/` and `content/diagnostics/`
- `docs/04_SIMULATION_SPEC.md`, `docs/05_COMMAND_AND_AGENT_AI.md`,
  `docs/07_VERTICAL_SLICE.md`, `docs/09_TEST_STRATEGY.md`,
  `docs/15_G4_DEFECT_TRIAGE.md`, `content/replays/CORPUS.md`,
  `content/diagnostics/README.md`
- this task file, `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`, a new
  ledger detail file, `PROJECT_STATE.yaml`

## Documentation updates

As listed under Expected files. `docs/15` D-02 moves from open to repaired only
if the acceptance evidence holds.

## Risks

- **Invariant change.** `docs/04` section 20 weakens from "live agents" to "agents
  in play" hold distinct cells. Any other reader of agent positions that is not
  on the list shares the cell with an extracted agent without knowing. Step 2
  must grep for every use of `Position` across agents, not just the sites above.
- **Unverified in the client.** The Godot rendering of an extracted agent that
  shares a cell with an in-play agent has not been seen; it goes to `docs/15`.
- **Stale contacts.** Enemy knowledge of an agent that has just extracted
  expires through the ordinary stale and expire bands (the TASK-078 precedent);
  for a few ticks an enemy may still hold a contact on an agent that is no longer
  targetable. Combat gates on `VisibleContacts`, so no shot is fired.

## Rollback or removal

Revert `inPlay` and its call sites, the overlay, the tests, the Bridgehead script
change, the `RecipientExtracted` arms, and the regenerated corpus and goldens.
Nothing else depends on the change.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next task.

## Review

Detail, commands and unresolved concerns: `docs/ledger/2026-10-22-TASK-084-extracted-agents-leave-play.md`.
Accepted: not yet. The task was selected on 2026-10-22 with Decision 4 left to the
agent; the agent replaced both drafted options with the command-intake rejection
(Decision 4 above), which Dave may overrule. `dotnet test` has not run (CI is
authoritative). One consequence to confirm: an agent whose route crosses an
extraction cell is extracted there and stops (`docs/15` L-08).
