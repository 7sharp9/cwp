# TASK-055: Perception stops observing a downed (Incapacitated/Dead) agent

Status: done (implemented and self-verified 2026-09-19; Godot `--selfcheck`
independently confirmed `MATCH` through the real editor; accepted by Dave
2026-09-19)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises new backlog B-062
Size: S

## Outcome (2026-09-19)

`Perception.visibleContactsFor` (`src/CommandoWar.Sim/Perception.fs`) gained
one extra condition: `other.Side <> observer.Side && Casualty.isAlive
other.Vitals && ...` — a non-`Alive` agent can no longer be freshly observed
by the opposing side, though it is still a fully valid `WorldState.Agents`
entry for every other purpose (unchanged from TASK-045). An existing
`Contact`/`HostileTacticalKnowledge` entry on it is not force-cleared; it
simply stops being refreshed and ages out through the ordinary
`PerceptionConfig.StaleAfter`/`ExpireAfter` bands (`Perception.
mergeKnowledge`, unchanged) exactly as a threat that moved out of sight
already does, emitting `ContactExpired` on removal.

No `Canonical.FormatVersion` bump, no new field, no new event — this is a
pure behavioural fix to an existing pure function, `Vitals` was already
canonical (TASK-045). Symmetric: applies to both sides via the one
side-agnostic `visibleContactsFor`/`sweep` (a downed friendly also stops
being a target for `HostileTacticalKnowledge`, the identical shape of bug
on the Hostile side, fixed for free by the same one-line change).

New `SimulationTests` fact (`an Incapacitated or Dead hostile is no longer
freshly observed, even still in range and in line of sight`): a friendly
observes an `Alive` hostile normally (baseline), then the hostile is forced
`Incapacitated`/`Dead` at the identical cell, still in range and LOS — the
next tick's `VisibleContacts` is empty and the existing `Contact.
LastSeenTick` stays pinned at the last tick it was genuinely observed,
proving the fix directly rather than only via corpus/golden re-pins.

**Corpus impact** (behaviour-neutral change to an existing pure function,
not a new canonical field — the TASK-031/034 "legitimately re-pins from
real new behaviour" precedent, not a `Canonical.FormatVersion` bump): 4 of
16 committed entries diverged and were regenerated —
`perception-contact`, `exposed-approach`, `suppress-relieves-exposure`,
`canonical-refusal-and-correction`, `casualties-succession-and-squad-failure`
(5 entries; the other 11 have no combat, or no side reaches non-`Alive`
within their tick window, so `Casualty.isAlive` was already always `true`
for every agent they ever perceive — a true no-op for those). Two
`DiagnosticsTests` golden fixture files needed regenerating for the same
reason (`casualties-succession-and-squad-failure-tick-005`/`-tick-065`,
`canonical-refusal-and-correction-tick-008`, `.ascii.txt` + `.svg` each);
inspected each diff directly rather than trusting `--regenerate` blindly:
every change is either a `seen tick`/stress value now correctly frozen at
the agent's last-Alive-and-seen tick, or (at tick 65, once every hostile in
the scenario is `Dead`) the stale `KnownContact`/`HostileKnownContact`/
`AgentStress` overlays and dev-overlay ghost rings for them finally
disappearing as their contacts age past `ExpireAfter` and genuinely expire
(`ContactExpired` events newly appear at tick 65) — exactly the intended
consequence, not a regression. `demo.html` (`DemoScenario`, no combat
within its 20-tick window) confirmed byte-identical, unaffected.

`dotnet build CommandoWar.slnx -c Release`: `0/0`. `dotnet test`:
`343/343` (+1, the new fact). `cwheadless corpus`: `16/16` after
`--regenerate`. Both Godot scenes' `--selfcheck` hashes reconfirmed
`MATCH` through the real Godot 4.7.2 editor, unaffected (`AppraisalDemo`'s
pinned `exposed-approach` tick-1 hash is likewise unaffected — the
divergence in that entry lands at tick 6, after the frame this demo
pins).

Full detail:
`docs/ledger/2026-09-19-TASK-055-perception-stops-observing-downed-agents.md`.

## Objective

A dead or incapacitated opposing agent stops being a fresh `Perception`
observation for either side, so `Appraisal`'s route-exposure model and any
other consumer of `WorldState.TacticalKnowledge`/`HostileTacticalKnowledge`
stops treating a corpse as a permanently live, full-confidence threat.

## Why this task exists

Dave tried TASK-054 live (moving a friendly into combat to test the new
audio-cue feature) and reported: "after the enemy agent is incapacitated or
killed the other agent refuses order due to threat when there is none."
Traced directly: `Perception.visibleContactsFor` never checked `Vitals` —
only `other.Side <> observer.Side`, range, and line of sight. A dead or
incapacitated agent is never removed from `WorldState.Agents` (TASK-045),
so it stays within sight range and LOS indefinitely, meaning it is "seen"
every tick forever at full `Confidence`. `Appraisal.cellPressure`/
`routeExposure` (`Appraisal.fs`) then treat every `TacticalKnowledge` entry
as an equally live threat, checking only `SuppressionBand` (which decays
normally but a corpse was never suppressed to begin with) — never `Vitals`.
Net effect: a route near a corpse is refused as "too exposed" forever. This
exact gap was already surfaced and left unfixed in TASK-050's ledger ("an
`Incapacitated` known contact still fully counts as a live threat for
`Appraisal`'s route-exposure model... Appraisal does not currently
distinguish `Incapacitated` from `Alive` for exposure purposes"), filed as
an open question, not designed. New backlog row B-062.

## Central decisions (confirmed with Dave 2026-09-19 via `AskUserQuestion`)

1. **Fix location**: `Perception` stops observing a non-`Alive` agent at
   all (Dave's choice, the recommended default), over the alternative of
   `Appraisal` reading each contact's true current `Vitals` directly. The
   chosen approach reuses the existing fog-of-war decay path (`StaleAfter`/
   `ExpireAfter`) rather than instantly clearing a kill from the squad's
   knowledge, and does not have `Appraisal` bypass the fog-of-war knowledge
   model to check ground truth.
2. **Timing**: fix now, as its own task, rather than only record the
   finding (Dave's choice) — it was actively blocking further live testing
   of TASK-054's own combat-dependent feature (the audio cue).

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`.
- `src/CommandoWar.Sim/Perception.fs` (`visibleContactsFor`, `sweep`,
  `mergeKnowledge`).
- `src/CommandoWar.Sim/Appraisal.fs` (`cellPressure`, `routeExposure` —
  confirms neither reads `Vitals`, only `suppressedThreats`).
- `src/CommandoWar.Sim/Casualty.fs` (`VitalStatus`, `Casualty.isAlive`).
- `src/CommandoWar.Sim/Simulation.fs`'s `perception`/`tacticalKnowledge`
  phases (confirms `VisibleContacts` is a derived cache excluded from
  `Canonical.encode`, and `TacticalKnowledge`'s `mergeKnowledge` already
  has the stale/expire decay path this fix reuses).
- `docs/ledger/2026-09-18-TASK-050-turn-resolution-spike.md` (the original,
  unfixed finding).

## Dependencies

- B-031 (TASK-045, `AgentState.Vitals`) — done.
- B-015 (TASK-026, `Perception`/`TacticalKnowledge`) — done.
- B-017 (TASK-028, `Appraisal`'s route-exposure model) — done.

## Allowed scope

- `src/CommandoWar.Sim/Perception.fs`: the one added condition in
  `visibleContactsFor`.
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`: one new focused fact.
- `content/replays/*.md` (regenerated corpus tables for the entries that
  legitimately change).
- `content/diagnostics/*.ascii.txt`/`.svg` (regenerated goldens for the
  two `DiagnosticsTests` facts that pin literal rendered text affected by
  the same behavioural change).
- `docs/11_BACKLOG.md` (new B-062 row), `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- `Appraisal.fs` itself (Central decision 1 — the fix lives in
  `Perception`, not by teaching `Appraisal` to read true current `Vitals`).
- Any `Canonical.FormatVersion` bump or new `AgentState`/`WorldState`
  field — `Vitals` is already canonical; this is a behavioural fix to an
  existing pure function.
- `content/diagnostics/demo.html` (confirmed unaffected — `DemoScenario`
  has no combat within its 20-tick window; regenerating it without an
  actual diff would be a no-op edit).
- Any observer-side (rather than target-side) `Vitals` check — whether a
  non-`Alive` agent's *own* `VisibleContacts` should also go empty is a
  different, unreported question, not this task's scope.

## Required work

1. Add `&& Casualty.isAlive other.Vitals` to `Perception.
   visibleContactsFor`'s existing condition.
2. Add a `SimulationTests` fact proving the fix directly: an `Alive`
   hostile is observed (baseline), the same hostile forced `Incapacitated`/
   `Dead` at an unchanged position (still in range and LOS) is no longer in
   `VisibleContacts` the next tick, and its existing `Contact.
   LastSeenTick` stays pinned rather than refreshing.
3. Run `cwheadless corpus`; regenerate any diverged entries with
   `--regenerate`; re-run to confirm `PASS`; inspect every diff directly
   (not just trust the regeneration) to confirm each is explained by the
   fix (a `seen tick`/stress value freezing, or a contact finally expiring)
   and not a stray regression.
4. Run `dotnet test`; regenerate any `DiagnosticsTests` golden fixture
   files whose corpus entry changed, via a direct `Corpus.all`/
   `DiagnosticRender` call reproducing the exact frame/format the failing
   assertion expects (no CLI verb exists for this — `cwheadless render`
   only replays against the shared `Fixture`, not an arbitrary corpus
   entry's own scenario); inspect each diff.
5. Confirm `content/diagnostics/demo.html` is unaffected (byte-identical
   regeneration check) — `DemoScenario` has no combat in 20 ticks.
6. Confirm both Godot scenes' `--selfcheck` hashes through the real editor
   (expected unaffected: render-only consumers of state that already
   existed; `AppraisalDemoScene`'s pinned frame precedes the affected
   entry's divergence point).
7. Update backlog/ledger/state.

## Acceptance criteria

- [x] An `Alive` opposing agent in range and line of sight is observed
      normally (unchanged baseline behaviour).
- [x] An `Incapacitated` or `Dead` opposing agent, still in range and line
      of sight, is **not** in the observer's `VisibleContacts` the next
      tick it goes down.
- [x] Its existing `TacticalKnowledge`/`HostileTacticalKnowledge` entry is
      not force-cleared — it ages and expires through the existing
      `StaleAfter`/`ExpireAfter` bands, emitting `ContactExpired` in the
      ordinary way once expired.
- [x] No `Canonical.FormatVersion` bump, no new field or event.
- [x] `dotnet build`/`dotnet test` green (343/343); `cwheadless corpus`
      16/16 after regeneration; every diverged corpus/golden diff inspected
      and explained.
- [x] Both Godot scenes' `--selfcheck` hashes unaffected.
- [x] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet test`: `343/343` (+1).
- `cwheadless corpus`: `16/16` after `--regenerate` (5 entries changed:
  `perception-contact`, `exposed-approach`, `suppress-relieves-exposure`,
  `canonical-refusal-and-correction`, `casualties-succession-and-squad-failure`).
- `cwheadless render demo --format html` byte-diffed against the committed
  `content/diagnostics/demo.html`: unchanged.
- Godot editor `--selfcheck` for `SnapshotDemo.tscn`/`CommandDemo.tscn`
  through the real Godot 4.7.2 editor: both `MATCH`, unaffected.
- `git status --porcelain`: matches this task's allowed scope.

## Evidence to capture

- The new `SimulationTests` fact's pass, and the exact corpus/golden diffs
  inspected (see Outcome).

## Expected files

- `src/CommandoWar.Sim/Perception.fs`.
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`.
- `content/replays/perception-contact.md`, `exposed-approach.md`,
  `suppress-relieves-exposure.md`, `canonical-refusal-and-correction.md`,
  `casualties-succession-and-squad-failure.md`.
- `content/diagnostics/casualties-succession-and-squad-failure-tick-005.
  {ascii.txt,svg}`, `-tick-065.{ascii.txt,svg}`,
  `canonical-refusal-and-correction-tick-008.{ascii.txt,svg}`.
- `tasks/TASK-055-PERCEPTION-STOPS-OBSERVING-DOWNED-AGENTS.md`.
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md` (+ new
  `docs/ledger/` detail file), `PROJECT_STATE.yaml`.

## Documentation updates

- This task file's Outcome section.
- `docs/11_BACKLOG.md` (new B-062 row: proposed and realised in the same
  entry, the TASK-050-finding-to-fix precedent).
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

A one-line behavioural change to an existing pure function, no schema
change. Revertible with `git revert` in one step; corpus/goldens would
need re-regenerating back to their pre-fix values on rollback (the inverse
of this task's own regeneration step).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave.
- Accepted: yes (2026-09-19). Confirmed live in the running game; no
  changes requested.
