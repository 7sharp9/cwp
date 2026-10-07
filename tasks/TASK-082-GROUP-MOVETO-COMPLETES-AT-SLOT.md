# TASK-082: Group MoveTo completes at the agent's formation slot

Status: review (self-verified 2026-10-07; not yet accepted)
Owner: coding agent
Phase: P4
Gate: G4 (defect repair; no evidence bullet depends on it); realises B-081
Size: S

## Objective

An agent given a group `MoveTo` completes the order when it stands on its own
resolved formation slot with `Destination = None`: `CommitmentCompleted` is
emitted, `Order` and `Disposition` clear, and a queued follow-on order is
promoted. Corrects D-13 in `docs/15_G4_DEFECT_TRIAGE.md`. Also corrects the
D-12 prose drift in `content/replays/chokepoint-detour.md`.

## Why this task exists

Dave asked for the open G4 defects to be fixed ("fix all issues", 2026-10-07).
D-13 and D-12 are the two that are verifiable in this environment and need no
product decision. The others are listed under Forbidden scope with the reason.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `docs/15_G4_DEFECT_TRIAGE.md` (D-12, D-13)
- `docs/04_SIMULATION_SPEC.md` section 12.6
- `src/CommandoWar.Sim/Simulation.fs` (`appraisal` fast path, `commitmentAndLocalAction`)
- `src/CommandoWar.Sim/Appraisal.fs` (`resolveFormationTarget`)

## Dependencies

- TASK-067 (`ReceivedOrder.AsGroup`), TASK-059 (formation slots), TASK-081 (done).

## Inputs and assumptions

- Appraisal and `commitmentAndLocalAction` both run before Navigation moves
  anyone, so the "every other agent's `Position`" set is identical in both
  phases within a tick. Resolving the slot again in the commitment phase
  therefore agrees with the cell Appraisal wrote to `Destination`.
- A solo order (`AsGroup = false`) keeps comparing with the literal target.

## Allowed scope

- `MoveTo` arm of `commitmentAndLocalAction` in `Simulation.fs`.
- The `formation-slots` committed table, which this changes by design.
- The `chokepoint-detour.md` prose line (D-12).
- One assertion pair added to an existing `SimulationTests.fs` fact.
- `docs/04` section 12.6 limitation text, `docs/15`, and the required
  task, backlog, ledger and `PROJECT_STATE.yaml` updates.

## Forbidden scope

- Any other open defect in `docs/15`. D-01 (threat-aware pathing) is a design
  change the playtest is meant to observe against R-001. D-05 needs a neutral
  working title from Dave. D-02 and D-03 change gameplay semantics and need a
  decision. D-04, D-06, D-07, D-09 and D-10 are in Godot or F# client code that
  cannot be compiled or run here (no Godot, no export templates). D-08 is a
  tooling feature.
- Changing the `CommitmentEstablished` target payload for group orders.
- Any `Canonical`, `AgentState` or `WorldState` change.

## Required work

1. Resolve the group `MoveTo` slot in the completion test.
2. Regenerate the corpus and confirm only `formation-slots` moves.
3. Fix the D-12 prose to match the replay (tick 7).
4. Update the stale limitation text and the triage document.

## Acceptance criteria

- [x] A group `MoveTo` completes at the slot: `formation-slots` gains
      `CommitmentCompleted` for agent 1 at tick 9 and agent 0 at tick 10, and both
      agents end with `Order = None` (probe against the real corpus entry).
- [x] No other corpus entry moves (`diff -r` of regenerated `content/`: only
      `formation-slots.md` differs; `bridgehead-*` unchanged).
- [x] `Sim` and `Headless` build with zero warnings.
- [ ] `dotnet test` passes. **Not run**: xunit and FsCheck are not in the offline
      package cache and NuGet is blocked. CI is the check.
- [x] No forbidden dependency or scope entered the change.
- [x] Required documentation was updated.

## Required verification

- `dotnet build src/CommandoWar.Headless -c Release -p:NuGetAudit=false`
- `dotnet run --no-build -c Release --project src/CommandoWar.Headless -- corpus`
- `dotnet run ... -- corpus --regenerate`, then diff against `content/`

## Evidence to capture

Commands and results: `docs/ledger/2026-10-07-TASK-082-group-moveto-completes-at-slot.md`.

## Expected files

- `src/CommandoWar.Sim/Simulation.fs`
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`
- `content/replays/formation-slots.md`, `content/replays/chokepoint-detour.md`
- `docs/04_SIMULATION_SPEC.md`, `docs/15_G4_DEFECT_TRIAGE.md`
- this task file, `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`, a new
  ledger detail file, `PROJECT_STATE.yaml`

## Documentation updates

As listed under Expected files.

## Rollback or removal

Revert the `MoveTo` arm edit and restore `formation-slots.md` from the previous
commit. Nothing else depends on it.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next task.

## Review

Detail, commands and unresolved concerns: `docs/ledger/2026-10-07-TASK-082-group-moveto-completes-at-slot.md`.
Accepted: not yet.
