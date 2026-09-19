# TASK-055: Perception stops observing a downed (Incapacitated/Dead) agent

Owner: Dave (implementing agent session)
Source revision: `main`, mid-review of TASK-054 (uncommitted at the time
this task started; TASK-054 remains a separate, unaccepted change).
Environment: `dotnet` `10.0.303`, Godot `4.7.2.stable.mono` (real editor),
Windows 11.

## Selection

Not a scoped-in-advance task: Dave live-tested TASK-054's audio-cue feature
(which requires provoking real combat to trigger) and hit this bug as a
direct consequence. Reported live: "after the enemy agent is incapacitated
or killed the other agent refuses order due to threat when there is none."
Diagnosed immediately (see Why this task exists) as a real, pre-existing
gap already surfaced and left unfixed in TASK-050's ledger, unrelated to
TASK-054 itself. Put to Dave via `AskUserQuestion` (fix location, and
whether to fix now given it was blocking further live testing) rather than
implemented on the spot, since it is a genuine `CommandoWar.Sim` behaviour
change with corpus/golden re-pin consequences.

## Central decisions

1. **Fix location**: `Perception` stops observing a non-`Alive` agent at
   all (Dave's choice, the recommended default) — reuses the existing
   fog-of-war `StaleAfter`/`ExpireAfter` decay path an out-of-sight threat
   already ages through, rather than teaching `Appraisal` to read a
   contact's true current `Vitals` directly (which would have cleared a
   kill from the exposure model instantly, but by reading ground truth
   rather than the squad's own perceived knowledge — a bigger conceptual
   change to what `Appraisal` is allowed to see).
2. **Timing**: fix now, as its own task (Dave's choice) — it was actively
   blocking further live testing of TASK-054's own combat-dependent
   feature.

## Investigation before drafting

- `Perception.visibleContactsFor` (`src/CommandoWar.Sim/Perception.fs`):
  `other.Side <> observer.Side && chebyshev ... <= SightRange &&
  Sight.visible ...` — no `Vitals` check anywhere.
- `Appraisal.cellPressure`/`.routeExposure` (`Appraisal.fs`): read
  `WorldState.TacticalKnowledge`'s `Contact[]` and `suppressedThreats:
  AgentId[]` (the `SuppressionBand` latch) only — no `Vitals` check either,
  and no access to a contact's live `AgentState` at all (by design: stage 3
  reads *known* threats, not ground truth).
- `Simulation.fs`'s `perception` phase re-derives `VisibleContacts` from
  scratch every tick for every agent in `s.Agents` — dead or alive, no
  filter — and confirmed (TASK-053's own prior investigation) that a `Dead`
  agent is never removed from `WorldState.Agents` (`Simulation.output`'s
  `RenderSnapshot` construction is unconditional).
- Confirmed `Casualty.isAlive : VitalStatus -> bool` (`Casualty.fs`) is
  already the exact predicate every other "can this agent act/be targeted"
  check in the codebase uses (`Combat`, `NavigationAndMovement`,
  `Appraisal`'s stage-2 check) — reused directly, no new predicate.
- Confirmed `Casualty.fs` compiles before `Perception.fs`
  (`CommandoWar.Sim.fsproj`'s `<Compile>` order) — no circular-dependency
  risk.
- Confirmed `AgentState.VisibleContacts` is a derived cache excluded from
  `Canonical.encode` (TASK-026's own doc comment) but
  `WorldState.TacticalKnowledge`/`HostileTacticalKnowledge` (fed from it
  via `Simulation.tacticalKnowledge`) **are** canonical — so this fix
  changes canonical *values* on affected corpus entries without touching
  `Canonical.FormatVersion` or adding any field (the identical shape as
  every prior "new mechanic legitimately re-pins existing entries" episode,
  TASK-031/034's own precedent).

## Changes

- `src/CommandoWar.Sim/Perception.fs`: `visibleContactsFor` gained `&&
  Casualty.isAlive other.Vitals` to its existing `if` condition, with a
  doc-comment explaining why (a corpse is not removed from `WorldState.
  Agents`, so without this it would stay "seen" at full confidence
  indefinitely).
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`: new fact `an
  Incapacitated or Dead hostile is no longer freshly observed, even still
  in range and in line of sight`, placed directly after the existing
  Casualty-related facts in the TASK-045 section (thematically closer than
  the TASK-026 perception section, since it tests the interaction between
  `Vitals` and `Perception`). Reuses the existing `perceptionWorld`/
  `stepIdle`/`agentOf`/`contactOf` test helpers (the "an Incapacitated or
  Dead agent never fires and is never targeted" fact's own record-update
  pattern for forcing an agent's `Vitals`).
- `content/replays/*.md`: 5 entries regenerated via `cwheadless corpus
  --regenerate` (`perception-contact`, `exposed-approach`,
  `suppress-relieves-exposure`, `canonical-refusal-and-correction`,
  `casualties-succession-and-squad-failure`).
- `content/diagnostics/*.{ascii.txt,svg}`: 2 golden pairs regenerated via a
  temporary `dotnet fsi` script (removed after use) reproducing the exact
  `Corpus.all`/`Corpus.commandsOf`/`DiagnosticRender.runFrames` call each
  failing `DiagnosticsTests` fact makes internally, then writing
  `DiagnosticRender.Ascii`/`.Svg` for the specific frame index each fact
  asserts against directly to the golden path (`cwheadless render` was not
  usable here — for any target other than the built-in `fixture`/`demo`/
  `los`/`path` names, it replays a raw `.cwlog` against the shared
  `Fixture` scenario, not the named corpus entry's own scenario, so it
  cannot reproduce `casualties-succession-and-squad-failure`'s or
  `canonical-refusal-and-correction`'s actual initial state).

No `CommandoWar.Sim` file other than `Perception.fs` touched; no
`Appraisal.fs` change (Central decision 1).

## Verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet test` before the fix's follow-on regeneration: `7` failures (5
  `CorpusTests` theory cases + 2 `DiagnosticsTests` golden-string facts),
  `335` passed — confirms the fix has real, bounded, expected effect before
  touching any committed evidence.
- `cwheadless corpus --regenerate` then `cwheadless corpus`: `16/16`.
  `git diff --stat content/replays/` confirmed exactly the 5 entries
  above changed, nothing else.
- Regenerated the 2 `DiagnosticsTests` golden pairs (see Changes); `git
  diff` on each inspected directly line by line before trusting them:
  - `canonical-refusal-and-correction-tick-008`: `known contact (10,4):
    agent 2 ... seen tick 8 -> seen tick 5`; `hostile known contact
    (10,1): agent 1 ... seen tick 8 -> seen tick 4`; two `stress` values
    drop (`400/1000 -> 160/1000` for agents 0 and 1) since their
    stress-inducing "currently visible" contact is now correctly frozen
    rather than perpetually refreshed; hash changes accordingly. No event,
    disposition, or commitment line changed.
  - `casualties-succession-and-squad-failure-tick-005`: one `known
    contact`/one `hostile known contact` "seen tick" value each frozen at
    the tick the respective hostile was last genuinely `Alive`-and-seen
    (`5 -> 3`, `5 -> 4`); no event/vitals/commitment change.
  - `casualties-succession-and-squad-failure-tick-065` (every agent `Dead`
    by this tick): the `known contact`/`hostile known contact`/`stress`
    overlay lines for the two hostiles disappear entirely (their contacts
    have now aged past `PerceptionConfig.ExpireAfter` and expired, since
    they stopped being freshly observed at incapacitation rather than
    death); `events: none -> contact-expired@(0,3); contact-expired@(5,0)`;
    the SVG's two dashed "stale ghost" rings (`?2`/`?3`) and their labels
    disappear from the render for the identical reason. Every `vitals`
    line (`dead` x4) and every `ammo` line unchanged.
  All five diffs are explained entirely by the fix's own intended
  consequence (a frozen `seen tick`/stress value, or a contact finally
  expiring); none touch a disposition, commitment, event ordering, or
  vitals/ammo value unrelated to perception timing.
- `dotnet test`: `343/343` (+1, the new fact).
- `cwheadless render demo --format html --out <tmp>` byte-diffed against
  the committed `content/diagnostics/demo.html`: identical (`DemoScenario`
  has no combat within its 20-tick window, so `Casualty.isAlive` was
  already always `true` for every agent it ever perceives — a genuine
  no-op there, confirmed rather than assumed).
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: `0/0` (rebuilt to pick up the new `CommandoWar.Sim.dll`).
- Godot editor `--selfcheck`, headless, through the real 4.7.2 editor:
  `SnapshotDemo.tscn` `MATCH 0xF422ACB8D5A86FF0`; `CommandDemo.tscn`
  `MATCH 0x00D3D471EF7354BC`. Both unaffected, as expected: neither scene's
  scripted sequence reaches a corpse still in another agent's sight range
  and LOS within its own 20-tick run. `AppraisalDemo.tscn` not re-run
  separately for this task (unaffected by construction: it pins
  `exposed-approach`'s tick-1 frame only, and that entry's divergence lands
  at tick 6 — confirmed from the corpus tool's own `first bad tick : 6`
  report before regenerating).
- `git status --porcelain`: matches this task's allowed scope (only
  `Perception.fs`, `SimulationTests.fs`, the 5 `content/replays/*.md`
  entries, and the 2 golden pairs' 4 files changed; no `Appraisal.fs`, no
  `demo.html`).

## Documents updated

- `tasks/TASK-055-PERCEPTION-STOPS-OBSERVING-DOWNED-AGENTS.md` (created,
  `Outcome` filled in).
- `docs/11_BACKLOG.md` (new B-062 row).
- `docs/12_PROGRESS_LEDGER.md` (this detail file; index row added).
- `PROJECT_STATE.yaml` (`active_work` updated).

## Review

- Reviewer: Dave.
- Accepted: yes (2026-09-19). Confirmed live in the running game; no
  changes requested.
