## 2026-09-27 - TASK-078 - Perception requires a live observer

**Owner:** Dave with coding-agent assistance
**Source revision:** `b55d699` (TASK-077 accepted, TASK-078 selected);
implementation commit `57cffd9` on branch `claude/cool-hypatia-j4rswr`
**Environment:** Linux cloud container, .NET SDK `10.0.112` (Ubuntu archive;
pinned `10.0.303` and NuGet blocked by the session's network policy); CI:
windows-latest, .NET SDK `10.0.303`
**Status change:** `ready -> review`

### Changes

- `src/CommandoWar.Sim/Perception.fs`: `visibleContactsFor` returns no
  contacts for a non-`Alive` observer (`Casualty.isAlive observer.Vitals`,
  so `Incapacitated` and `Dead` both), the observer-side counterpart of
  TASK-055's target-side check. Realises B-078 (TASK-077 finding 1).
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`: new fact `an
  Incapacitated or Dead observer perceives nothing and stops refreshing the
  squad picture`; the existing `Stress accumulates over continuous contact
  and decays once contact is lost` fixture moved its hostile from distance 3
  to 9 (see Findings).
- Re-pins: seven `content/replays/*.md` tables, four golden frames (ASCII and
  SVG) under `content/diagnostics/`, and the `CommandDemoScene` `--selfcheck`
  constant in `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs` (plus the
  client README's usage line).

### What moved, and why

See `tasks/TASK-078-PERCEPTION-REQUIRES-A-LIVE-OBSERVER.md` Evidence for the
per-entry table. In short: every moved entry's first re-pinned tick is
exactly the first tick a downed agent would have perceived under the old
rule; an old-versus-new event diff (pre-fix `HEAD` built separately) shows
identical outcomes, deaths, objective completions, and refusals in all seven,
with five event streams byte-identical and the two Bridgehead entries losing
only corpse `ContactObserved` events and the reappraisals of already-accepted
orders those triggered. `CommandDemoScene` tick 90: `0x84A25E3559111E9B ->
0x300BB18492CEB355`, computed through `CommandoWar.Client.Godot.Core` (no
Godot dependency) after confirming it reproduced the old pin; story unchanged
(machine gun dead tick 71, all six friendlies alive at tick 90).
`DemoRenderScene` unchanged. `Canonical.FormatVersion` unchanged (15).

### Verification

- `dotnet build src/CommandoWar.Headless -c Release -p:NuGetAudit=false`:
  0 warnings, 0 errors.
- `-- corpus --regenerate` twice: stable. `-- corpus`: 22/22.
- Stand-in test run (every test file except `DeterminismPropertyTests`,
  `ReplayTests`, `ScenarioFileTests` (FsCheck) and `BenchmarkTests` (xunit
  output-helper injection), compiled against a stand-in `Xunit` module, run
  by reflection): pre-fix `HEAD` 372/372; post-fix before re-pinning 368/373
  (four goldens plus the stress test); final 373/373. The new fact fails on
  pre-fix code.
- CI on `57cffd9` (<https://github.com/7sharp9/cwp/actions/runs/36340712686>): windows-latest, SDK 10.0.303, run `36340712686` on `57cffd9`: build 0 warnings / 0 errors, `Passed: 428, Failed: 0` (427 + the new fact), `corpus` 22/22, working tree clean after the verbs.
- `git diff --stat src/CommandoWar.Sim`: `Perception.fs` only. No package,
  no format bump, no content change.

### Findings

1. **A test depended on the defect.** `Stress accumulates over continuous
   contact...` put its hostile inside weapon range, so agent 0 was
   incapacitated at tick 3 and reached the expected stress only by gaining
   it while bleeding out. Moved the hostile to Chebyshev 9 (in sight, out of
   weapon range); the test's intent, live continuous contact, now gives the
   same 250 under old and new code.
2. **Reappraisal is triggered by either side's knowledge changes**
   (observation only, not changed). `Simulation.appraisal`'s
   `knowledgeChanged` fires on any `ContactObserved`/`ContactExpired`,
   including hostile-picture events, so a hostile noticing a friendly (or a
   hostile-picture entry expiring) reappraises friendly orders. Harmless to
   outcomes here, and it is how the TASK-078 re-pin shows up in
   `bridgehead-succeeded`'s event stream; recorded in case it matters for
   the "reappraise only on material triggers" rule later.
3. TASK-077's other findings stand: the extraction objective completes
   vacuously when every friendly is down (low); `chokepoint-detour`'s
   description drift; `PROJECT_STATE.yaml` not parsing as YAML.

### Documents updated

- `tasks/TASK-078-PERCEPTION-REQUIRES-A-LIVE-OBSERVER.md` (status, criteria,
  evidence)
- `docs/11_BACKLOG.md` (B-078 -> `review`)
- `content/replays/CORPUS.md` (TASK-078 re-pin note)
- `docs/07_VERTICAL_SLICE.md` section 9 (TASK-077 note: finding 1 fixed)
- `src/CommandoWar.Client.Godot/README.md` (self-check hash)
- `PROJECT_STATE.yaml` (`active_work`)
- `docs/12_PROGRESS_LEDGER.md` (index row, Pinned facts green-test count)
  and this file

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-29, "accept it"), on the self-verification plus
  CI evidence above (run 36340712686: build 0/0, `dotnet test` 428/428,
  `corpus` 22/22).
