# TASK-083: A routine order does not advance on a threat that already has the agent in range

Status: ready (drafted 2026-10-12; not selected)
Owner: coding agent
Phase: P4
Gate: G4 (defect repair; no evidence bullet depends on it); realises B-082
Size: M

## Objective

When an agent with an accepted, unfulfilled `MoveTo` order stands within a known,
unsuppressed threat's engagement range and in its line of sight, and the order's
route would take it closer to that threat, Appraisal refuses the order
(`RouteTooExposed`, naming the threat) on the tick the contact is observed. The
agent stops advancing that tick. An `Immediate` urgency or `Aggressive` risk
tolerance still presses on. Suppressing the threat, or ordering a route that
does not close on it, reverses the outcome through the existing reappraisal
triggers.

This addresses D-01 in `docs/15_G4_DEFECT_TRIAGE.md` (refusal lands after the
shooting starts), not threat-aware routing. Stage 5 (safer route adaptation) is
not part of this task.

## Why this task exists

Dave asked for D-01 to be fixed with threat-aware pathing (2026-10-12). A probe
at `20b4163` showed that pathing cannot change the failing case, so Dave chose
the alternative of making the agent react on first contact. The evidence, from
agent 5 ordered straight from `(4,8)` to `(12,9)` on Bridgehead at tick 1:

- `TacticalKnowledge` is empty until tick 9, so a threat-aware route has nothing
  to avoid.
- The first sight of rifleman 102 at `(13,9)` is at tick 9, from `(7,9)`,
  Chebyshev distance 6, inside weapon range 7. Both sides fire on that tick.
- The knowledge-change trigger reappraises the order on the same tick (Perception
  and Tactical knowledge run before Appraisal), so the "one-tick lag" in
  `docs/15` D-01, `docs/07` section 9 and the TASK-073 ledger is not the
  mechanism. The reappraisal returns `Accepted`, because the route exposure
  (about 50 to 60) is under the discipline-3 threshold (65). `Refused
  (RouteTooExposed)` arrives at tick 12, after hits have raised stress and
  lowered the threshold. By then the agent has been shot at for three ticks.
- Every route to `(12,9)` is exposed, because the target is adjacent to the
  rifleman.

The agent therefore keeps advancing into a threat that is already firing on it,
and only gives up once it is hurt. `docs/05` section 11 lists "point-blank hostile
threat" as interrupt priority 3 and records that it has no live signal. This task
gives that case a live signal through Appraisal, where the reason is already
typed and explained, rather than through a new interrupt mechanism with stored
resume state.

`docs/05` section 16 ("Unknown threat") keeps the intended behaviour that an
agent accepts on current knowledge and reacts when fired upon. This task changes
the reaction, not the acceptance.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `docs/15_G4_DEFECT_TRIAGE.md` (D-01, D-03)
- `docs/05_COMMAND_AND_AGENT_AI.md` sections 5 to 7, 11, 14, 16
- `docs/04_SIMULATION_SPEC.md` sections 12.5, 12.6
- `docs/07_VERTICAL_SLICE.md` section 9 (Bridgehead evidence)
- `src/CommandoWar.Sim/Appraisal.fs` (`cellPressure`, `routeExposure`,
  `resolveThreshold`, `appraise`)
- `src/CommandoWar.Sim/Simulation.fs` (`appraisal`, the trigger block)
- `content/replays/CORPUS.md`, `src/CommandoWar.Headless/Corpus.fs`
- `docs/ledger/2026-09-21-TASK-073-*.md`, `docs/ledger/2026-09-22-TASK-075-*.md`
- `docs/10_RISK_REGISTER.md` R-001, R-023

## Dependencies

- TASK-033 (reappraisal triggers), TASK-037 (suppression zeroes pressure),
  TASK-073 and TASK-075 (the Bridgehead evidence), TASK-081 (done).

## Inputs and assumptions

Assumptions to confirm in step 1 before editing:

- The tick-9 sequence above reproduces at the current HEAD with the real
  Bridgehead content and seed 20260920.
- `Appraisal.appraise` already receives everything the rule needs: the agent's
  position, the known threats, the suppressed set, the route, and the order's
  `RiskTolerance` and `Urgency`.
- The knowledge-change trigger fires on the observation tick, so no new trigger
  is needed. If a case is found where it does not, stop and report.

Design decisions made here (Dave may amend them before selection):

1. **Rule shape.** A stateless test inside stage 3 or 4, evaluated on every
   appraisal: some known threat is unsuppressed, is within
   `AppraisalConfig.ThreatEngagementRange` of the agent's current cell, has
   `Sight.visible` to it, and some route cell is strictly closer (Chebyshev) to
   that threat than the agent's current cell. No new `AgentState` field, no
   stored resume flag, so the canonical layout does not change.
2. **Reason.** Reuse `RouteTooExposed (Some threat)` with the nearest such threat
   as the named one. No new `DecisionReason` case, so the player-facing
   explanation mapping does not change.
3. **Exemption.** `Urgency.Immediate` and `RiskTolerance.Aggressive` skip the
   rule. `Discipline` does not, because it already moves the exposure
   threshold and exempting it would make the rule disappear for the default
   agent.
4. **Scope.** `MoveTo` only. `Withdraw` moves away by definition, `Hold` goes
   to cover, and `Assault` advances under fire by design.
5. **Reversal.** A threat whose `SuppressionBand` is latched does not count,
   matching Decision F in TASK-037. A route that does not close on the threat
   (a retreat, a sidestep) is not blocked.

## Allowed scope

- `Appraisal.fs` (the rule and its config literal if one is needed) and the call
  in `Simulation.appraisal` only if the inputs are not already passed.
- Focused tests in `tests/CommandoWar.Sim.Tests/` for the rule and for each
  decision above.
- Corpus entries whose behaviour changes by design, and the Bridgehead entry
  scripts if the player's orders must change to stay winnable (see Stop
  conditions).
- Diagnostics: the frame must show why an order was refused on contact. The
  existing `OrderAppraisal` overlay already carries the reason; add or update a
  golden under `content/diagnostics/` that covers the new refusal.
- `docs/05` sections 5, 11, 14, 16, `docs/04` section 12.5, `docs/07` section 9,
  `docs/15`, and the required task, backlog, ledger and `PROJECT_STATE.yaml`
  updates.

## Forbidden scope

- Threat-aware pathfinding or any change to `Pathfinding`. Stage 5 adaptation
  stays deferred (B-018).
- A new interrupt mechanism, a stored resume flag, a new `AgentState` or
  `WorldState` field, or a `Canonical.FormatVersion` bump. If the rule cannot be
  built without one, stop and report.
- Changing `ThreatEngagementRange`, `WeaponRange`, `SightRange`, or any
  `AppraisalConfig` weight to make a probe pass.
- Changing Combat, Perception, or map geometry. In particular, do not move the
  ford or the berm.
- Any other open defect in `docs/15`.
- Using `System.Random` or any new PRNG draw.

## Required work

1. Reproduce the tick-9 probe at HEAD and record the timeline (contact tick,
   first shot, first `Accepted` and `Refused` ticks) in the ledger. If it does
   not reproduce, stop and report.
2. Implement the rule in `Appraisal.appraise` so it is pure, total and
   integer-only, and add focused tests: a refusal on the contact tick; no
   refusal when the threat is suppressed; no refusal for a route that does not
   close; no refusal for `Immediate` or `Aggressive`; no effect on `Hold`,
   `Assault`, `Withdraw`, or `Suppress`; determinism across repeated runs.
3. Re-run the un-staged repro and the staged two-step order. The un-staged agent
   must be `Refused` on the contact tick and stop advancing that tick. The
   staged order must still be refused at tick 6 before any shot, and accepted
   again at tick 83 in `bridgehead-succeeded` unless the new rule is the stated
   reason it moves.
4. Run the full corpus. List every entry that moves, with the reason. Both
   Bridgehead entries must still reach `Succeeded` and `Failed`. If an entry's
   scripted orders are now refused, change the script to use the player-facing
   correction the rule is meant to teach (suppress, then advance, or an
   `Aggressive` order) and say so; do not weaken the rule.
5. Add or update the diagnostic golden for the new refusal frame.
6. Correct the mechanism text in `docs/15` D-01, `docs/07` section 9 and the
   `docs/05` section 11 priority 3 coverage note, and record in the ledger that
   the TASK-073 "one-tick lag" description was wrong at the time of this task.
7. Record evidence and update the documents listed below.

## Stop conditions

Stop and report to Dave, with the evidence, without weakening the rule, if:

- the repro does not behave as described in "Why this task exists";
- `bridgehead-succeeded` cannot reach `Succeeded` with the player's existing
  intents under the rule;
- the rule needs state outside `AgentState` fields that already exist;
- more than the Bridgehead and refusal-related corpus entries move.

## Acceptance criteria

- [ ] In the un-staged repro, agent 5's order is `Refused (RouteTooExposed (Some
      102))` on the tick 102 is first observed, and the agent makes no further
      advance after that tick (probe against the real content, timeline
      recorded).
- [ ] The staged order is still refused at tick 6 before any shot.
- [ ] Each decision in "Inputs and assumptions" has a focused test.
- [ ] `canonical-refusal-and-correction` still shows refusal, correction by
      suppression, and acceptance.
- [ ] `bridgehead-succeeded` reaches `Succeeded` and `bridgehead-failed`
      reaches `Failed`; every other moved corpus entry is explained.
- [ ] A diagnostic golden covers the new refusal and is inspectable in the ASCII
      or SVG render.
- [ ] `Sim` and `Headless` build with zero warnings; `dotnet test` passes, or
      its absence is stated.
- [ ] No forbidden dependency or scope entered the change; `Canonical.FormatVersion`
      is unchanged.
- [ ] Required documentation was updated.

## Required verification

- `dotnet build src/CommandoWar.Headless -c Release -p:NuGetAudit=false`
- `dotnet test` for `tests/CommandoWar.Sim.Tests` (CI is authoritative if the
  packages are unavailable)
- `dotnet run --no-build -c Release --project src/CommandoWar.Headless -- corpus`
- `corpus --regenerate`, then a diff of `content/` listing every moved file
- the repro and staged-order probes, committed as ledger text, not as code

## Evidence to capture

Commands, the repro timeline before and after, the list of moved corpus entries
with reasons, and any unresolved concern:
`docs/ledger/<date>-TASK-083-no-advance-under-fire.md`.

## Expected files

- `src/CommandoWar.Sim/Appraisal.fs` (and `Simulation.fs` only if needed)
- `tests/CommandoWar.Sim.Tests/AppraisalTests.fs` or the existing file that
  covers appraisal
- moved corpus files under `content/replays/` and, if scripts change,
  `src/CommandoWar.Headless/Corpus.fs`
- `content/diagnostics/` goldens and `tests/.../DiagnosticsTests.fs`
- `docs/04_SIMULATION_SPEC.md`, `docs/05_COMMAND_AND_AGENT_AI.md`,
  `docs/07_VERTICAL_SLICE.md`, `docs/15_G4_DEFECT_TRIAGE.md`
- this task file, `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`, a new
  ledger detail file, `PROJECT_STATE.yaml`

## Documentation updates

As listed under Expected files. `docs/15` D-01 changes from open to a repaired
defect only if the acceptance evidence holds; the playtest note that D-01 is "the
main thing the playtest should observe against R-001" is replaced by what the
rule now does.

## Risks

- **Player-visible behaviour change.** A routine order into fire is now refused
  where it was accepted. This is the intended effect, but it makes
  `Suppress` or an `Aggressive` order necessary to push under fire, and the
  Bridgehead corpus may need new scripts. This is the most likely cause of a
  large diff.
- **Refusal in the open.** The agent halts at the contact cell, still in range,
  and keeps trading fire. The task does not move it to cover. If playtest
  evidence shows that is worse than pressing on, the follow-up is a cover
  reaction, not a loosened rule.
- **D-03 interaction.** The global knowledge-change trigger re-judges every
  order on any contact change, so the rule runs more often than strictly
  needed. Outcomes depend only on state, so determinism is unaffected.
- **Tuning pressure.** If the rule flips cases it should not, resist adjusting
  constants; the stop conditions apply.

## Rollback or removal

Revert the `Appraisal.fs` change, the tests, and the regenerated corpus and
goldens. Nothing else depends on the rule.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next task.

## Review

Not yet implemented.
