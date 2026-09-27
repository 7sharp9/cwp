# TASK-078: Perception requires a live observer

Status: ready
Owner: coding agent (Dave to accept)
Phase: P4
Gate: G4 (defect found by TASK-077); realises B-078
Size: S

## Objective

A non-`Alive` agent (`Incapacitated` or `Dead`) perceives nothing:
`Perception.visibleContactsFor` returns no contacts for it, so it emits no
`ContactObserved`, adds nothing to either side's shared tactical picture, and
no longer gains stress from contacts it "sees".

## Why this task exists

TASK-077 found, while building the Bridgehead replay corpus, that
`Perception.visibleContactsFor` checks the *target's* vitals (TASK-055) but
never the *observer's*. A corpse keeps "seeing": dead machine gunner 100
emits `ContactObserved` from tick 77 in `bridgehead-succeeded`, and on the
friendly side a dead or bleeding-out soldier keeps refreshing
`WorldState.TacticalKnowledge` at full confidence for as long as enemies stay
in its line of sight. That changes what `Appraisal` treats as a known threat,
and so which orders are refused. TASK-055 explicitly left the observer side
out of scope as "a different, unreported question"; it is now reported.
Dave selected it next when accepting TASK-077 ("fix the perception defect
next").

## Required reading

- `src/CommandoWar.Sim/Perception.fs` (`visibleContactsFor`, `sweep`)
- `src/CommandoWar.Sim/Simulation.fs` phases `perception`,
  `tacticalKnowledge`, `combat` (candidate selection), `stateConsequences`
  (stress gain)
- `tasks/TASK-055-PERCEPTION-STOPS-OBSERVING-DOWNED-AGENTS.md`
- `docs/04_SIMULATION_SPEC.md` section 16 ("dead or incapacitated agents do
  not start new actions"); `docs/05_COMMAND_AND_AGENT_AI.md` section 11
  (interrupt priority 1, "Dead or incapacitated")
- `tasks/TASK-077-*.md` finding 1

## Dependencies

- TASK-077 (done).

## Inputs and assumptions

- `Incapacitated` counts as not perceiving, the same `Casualty.isAlive`
  predicate TASK-055 used for targets. `docs/04` and `docs/05` group "dead or
  incapacitated" together throughout, and a bleeding-out soldier spotting
  for the squad is the same defect in a milder form.
- A downed agent's existing contacts in the shared picture are not deleted:
  they stop being refreshed and age out through the existing
  `StaleAfter`/`ExpireAfter` bands, the path TASK-055 used for a downed
  target.
- The agent goes down during combat (after perception) on tick *t*, so its
  own tick-*t* contacts still count; perception clears them from tick *t+1*.
- No new state, event, or format change: `Canonical.FormatVersion` stays 15.
  Hashes move only where a downed agent had something in view.

## Allowed scope

- `Perception.visibleContactsFor`: the observer-side vitals check.
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`: a focused fact.
- Re-pinning whatever the behaviour change legitimately moves: corpus tables,
  diagnostic goldens, the Godot `--selfcheck` constants in
  `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`, and the
  `DiagnosticsTests` expectations that pin them. Each moved pin must be
  explained, not just refreshed.
- If a `bridgehead-*` corpus entry no longer reaches its recorded outcome,
  re-author its schedule so it does, and record why.

## Forbidden scope

- The extraction-objective defect (TASK-077 finding 2), or any other
  mission/combat change.
- Any change to `Appraisal`, the knowledge-decay constants, or which targets
  are visible (TASK-055's rule stays as is).
- New `AgentState`/`WorldState` fields or format bumps.

## Required work

1. Add the observer check; add a fact that fails before the fix.
2. Regenerate the corpus and goldens; for every moved entry, state which
   downed agent's perception caused it.
3. Confirm `bridgehead-succeeded` still reaches `Succeeded` and
   `bridgehead-failed` still reaches `Failed`, and that the ford refusal still
   happens; recompute the Godot self-check hashes.
4. Record.

## Acceptance criteria

- [ ] A non-`Alive` observer has empty `VisibleContacts` and emits no
      `ContactObserved`; a focused fact proves it and fails without the fix.
- [ ] Every moved corpus table, golden, and self-check pin is explained.
- [ ] Both Bridgehead corpus outcomes and the ford refusal still hold.
- [ ] `dotnet test` and `cwheadless corpus` pass (CI on the pinned SDK).
- [ ] No forbidden scope entered the change.
- [ ] Documentation updated.

## Required verification

- `dotnet build src/CommandoWar.Headless -c Release`; `-- corpus`;
  `-- corpus --regenerate` twice and diff
- the new fact and changed `DiagnosticsTests` facts (stand-in harness
  locally, since NuGet is blocked here; CI for the real run)
- Godot self-check hashes via `CommandoWar.Client.Godot.Core` (no Godot
  install needed; confirmed to reproduce the current pins first)
- `git diff --stat src/CommandoWar.Sim` limited to `Perception.fs`

## Documentation updates

- this file; `docs/11_BACKLOG.md` B-078; `content/replays/CORPUS.md`
  re-pin note; `docs/12_PROGRESS_LEDGER.md` index row and detail file;
  `PROJECT_STATE.yaml`.

## Rollback or removal

Revert the one-line check and restore the previous pins from Git.
