## 2026-09-13 - TASK-031 - Basic hitscan combat and directional cover effects

**Owner:** Dave with coding-agent assistance
**Branch:** `task-031-hitscan-combat` off `main` at the TASK-030 merge
(`Merge TASK-030: Commitment and finite move/hold executor (B-018)`).
Committed locally; not pushed.
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; FSharp.Core
10.1.303; xUnit 2.9.3; FsCheck 3.3.4
**Status change:** TASK-031 drafted `-> ready -> review`; backlog B-019
`proposed -> review`

Realises `docs/07` section 5 "hitscan small-arms combat" and `docs/04`
section 12.8's line-of-fire / hit-draw subset. Turns the Combat phase from a
no-op into a real phase: automatic symmetric engagement, a deterministic
0..1000-scale hit chance mitigated by directional `Terrain.cover`, and the
simulation's first real gameplay PRNG draw. No ADR - no canonical-image
change (`WorldState.Random` was already canonical, TASK-003).

### Central decisions (confirmed with Dave 2026-09-13 before the phase bodies)

- **A - automatic engagement, no player order; both sides symmetric.**
  `PlayerIntent` is still `MoveTo`-only. Candidates come only from the
  shooter's own `AgentState.VisibleContacts` (risk R-023 "same observation
  contract"), but line of fire is re-verified fresh at combat time against
  each candidate's *current* cell (`Sight.visible`), since Combat runs three
  phases after Perception and positions can move within the tick. Nearest
  qualifying candidate wins, ties by ascending `AgentId`.
- **B - no ammunition or weapon readiness; fires every valid tick.** No
  weapon/ammo data model exists on `AgentState`. Recorded future direction
  (not built): ammo will eventually be a cooldown-between-magazines or
  reload-from-stock model; how an agent resupplies is an open question for
  that task.
- **C - a hit has no consequence: no wound/death state.** `docs/07` section 5
  treats "hitscan small-arms combat," "suppression" (B-020), and casualties
  (B-031, P4) as separate systems. This task emits an event only. Recorded
  future direction (not built): death will eventually be a critical/bleed-out
  state with a recovery timer (XCOM-style), not a binary kill.
- **D - deterministic hit chance; the stream's first real gameplay draw.**
  `hitChance = BaseHitChance - RangePenaltyPerCell * distance -
  CoverMitigationPerLevel * coverLevel`, clamped `[MinHitChance,
  MaxHitChance]`, on the `0..1000` scale (the `Confidence` precedent). One
  `RandomStream.next` draw per engaging agent, ascending shooter id.
  `WorldState.Random` was already canonical, so no `Canonical.FormatVersion`
  bump - only the *values* a draw produces are new.
- **E - reuse `Appraisal.attackDirection`, made public.** Rather than
  duplicate the ten-line cover-facing-geometry helper into a new leaf,
  `private` was removed from it and `Combat.hitChance` calls it directly -
  the identical precedent to `Appraisal` reusing `Perception.chebyshev`.
- **F - one new event, `ShotFired of shooter * target * hit: bool`.** One
  case for both outcomes (the `OrderAppraised` precedent), emitted only for
  a qualifying shot.
- **G - new leaf `src/CommandoWar.Sim/Combat.fs`.** `CombatConfig` +
  `Combat.hitChance` + `Combat.chooseTarget`, pure, total, no PRNG draw
  inside the leaf (the phase function draws).
- **H - one new corpus entry proves the phase wiring; unit facts prove the
  tactics.** New entry `open-engagement`: a friendly and hostile in range/LOS
  from tick 1, no orders. Cover/range monotonicity and target selection are
  proved directly on the pure `Combat.hitChance` / `Combat.chooseTarget`
  functions instead of via the corpus (a corpus entry pins one seed's
  outcome, not a probability).
- **I - no ADR.** Combat's phase slot already existed; `WorldState.Random`
  consumption was already anticipated by its own doc comment.

### Correction found during implementation (Decision H)

The original framing assumed every *existing* corpus entry, the fixture, and
`envelope-full` would stay byte-identical, with only `open-engagement` newly
pinned. Running `cwheadless corpus` after the core implementation showed
`perception-contact` and `exposed-approach` both diverging:

```
DIVERGED perception-contact: first bad tick 5, random draws 2
DIVERGED exposed-approach:   first bad tick 2, random draws 2
```

Both are genuine, correct new behaviour: `perception-contact`'s friendly
comes within `CombatConfig.WeaponRange` (7) and line of sight of the hostile
once it clears the wall at tick 5; `exposed-approach`'s agent 1 (`Accepted`)
walks within range of the known hostile as it proceeds along the exposed
approach. Verified before re-pinning: **tick counts unchanged on both**
(the actual safety invariant - a moved tick count would have been
stop-and-report); `exposed-approach` tick 1
(`0xB03F8419E55F3592`, the TASK-029 Godot demo's pinned value) is
unaffected, confirmed by diff before touching anything. `demo.html` (not a
corpus entry, `DemoScenario` has one hostile) similarly gains combat from
tick 8 - confirmed by diffing a pre-implementation render against a
post-implementation one before regenerating, isolating exactly which ticks
changed (8 onward; 0-7 byte-identical).

This also broke three existing tests whose "no PRNG draw" assumption
predated Combat:

- `SimulationTests`: `` `order appraisal is deterministic across two runs and
  draws no randomness` `` - its hand-built world (friendly `(5,10)`, hostile
  `(9,3)`) sits at exactly Chebyshev distance 7, `= WeaponRange`. Renamed
  (dropped "and draws no randomness") and changed the assertion from
  `Draws = 0` to `r1.Draws = r2.Draws` - still proves determinism, no longer
  asserts a now-false invariant.
- `DeterminismPropertyTests` properties 8 and 9 (`appraisalCaseGen` can now
  legitimately place a friendly/hostile pair in range/LOS): both lost their
  `noDraws` assertion; property 8 was also renamed (dropped "and PRNG-free"
  / "and draws no randomness"). Neither property was actually testing
  `Appraisal.appraise` or `Commitment.ofAgent` drawing (both leaves still
  draw nothing) - they were asserting a whole-tick fact that predated
  Combat's existence.

### Existing-test ripple beyond the three above (behaviour-neutral)

- `src/CommandoWar.Headless/AppraisalDemo.fs` gained a `FireLine` arm in its
  `unhandled` overlay bucket (an exhaustive match with no
  `TreatWarningsAsErrors` guard in the test project) - this disposable P3
  demo predates TASK-031 and does not render combat.
- Four exhaustive `Overlay` matches in `tests/.../DiagnosticsTests.fs`
  needed a `FireLine` arm (warnings, not errors, in the test project - the
  TASK-030 lesson: check these by hand).

### Event-trace and hash impact

| Entry | Affected from | Tick count | Note |
|---|---|---:|---|
| `perception-contact` | tick 5 | 14 (unchanged) | friendly clears the wall, hostile comes into range/LOS |
| `exposed-approach` | tick 2 | 12 (unchanged) | agent 1 (`Accepted`) walks within range of the known hostile; agent 0 (`Refused`) never moves, stays out of range |
| `demo.html` | tick 8 | n/a (not a corpus entry) | `DemoScenario`'s friendly comes into range of its one hostile |
| `open-engagement` | tick 1 (new entry) | 3 | both agents in range/LOS from the start, no orders |
| every other entry, the fixture, `envelope-full` | never | unchanged | no pair ever comes within `CombatConfig.WeaponRange` and line of sight |

### Verification

- `dotnet build CommandoWar.slnx -c Release`: 0/0 before and after.
- `dotnet test CommandoWar.slnx -c Release`: `254 -> 265` green. New: +1
  `CorpusTests` theory case (`open-engagement`, automatic via
  `Corpus.all`-driven `[<MemberData>]`), +8 `SimulationTests` facts (both
  sides fire when in range/LOS; out-of-range excluded; `Combat.chooseTarget`
  excludes an LOS-blocked candidate; `Combat.hitChance` monotonic in range
  and in cover, clamped at the extremes; `Combat.chooseTarget` nearest +
  ascending-id tie-break; two-run determinism with an exact draw-count
  match), +1 `DeterminismPropertyTests` property 10 (every `ShotFired`
  reproducible from a fresh recompute + redraw, exact draw count, `MaxTest =
  200`, reusing `appraisalCaseGen` unchanged), +1 `DiagnosticsTests` fact
  (the `open-engagement` golden, byte-equal ASCII/SVG, both `FireLine`
  overlays and their event kinds checked).
- `cwheadless corpus`: 12/12 PASS (ten existing + `open-engagement`, two of
  the ten re-pinned as documented); `--regenerate` twice in a row is a zero
  diff (idempotent).
- `cwheadless fixture`: byte-identical before and after (`spike-fixture` is
  enemy-free).
- `cwheadless replay-file content/replays/envelope-full.cwreplay`:
  checkpoints unchanged (enemy-free scenario).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`: `FSharp.Core` only.
- Source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`: clean (comment mentions
  only, pre-existing).
- `git status --porcelain`: matches the task file's "Allowed scope" plus the
  documented ripples (`AppraisalDemo.fs`, the `perception-contact` /
  `exposed-approach` re-pin, the three test-assertion corrections); nothing
  under the client spikes, `src/_scratch`, `bench/`, `content/benchmarks/
  BASELINE.md`.

### Unresolved / follow-ups

- Ammunition (reload-or-cooldown, resupply TBD) and death (XCOM-style
  critical/bleed-out timer) - deliberately not scoped (Decisions B/C); see
  `[[combat-ammo-and-death-direction]]` memory for Dave's future-direction
  notes.
- `docs/08_ROADMAP_AND_GATES.md` section 6's P3 Required work list omits
  "implement basic hitscan combat" even though the backlog and dependency
  graph already treat B-019 as P3/G3 critical path - added as part of this
  task's documentation updates.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-13)
- Notes: `combat` is a real phase in `Phases.order` slot 8 (`docs/04` section
  12.8), reading `AgentState.VisibleContacts` only (R-023 "same observation
  contract") and re-verifying line of fire fresh at combat time via
  `Sight.visible` against each candidate's current cell. `Combat.hitChance`
  / `Combat.chooseTarget` are pure and total in the new `Combat.fs` leaf,
  reusing `Appraisal.attackDirection` (made public) for cover-facing
  geometry — no duplication. One `RandomStream.next` draw per engaging
  agent, ascending shooter id; `WorldState.Random` was already canonical, so
  no `Canonical.FormatVersion` bump anywhere. The Decision H correction
  (`perception-contact` / `exposed-approach` legitimately re-pinning once an
  agent comes into weapon range and line of sight) is the honest,
  correct consequence of building real combat against scenarios that
  already place an enemy in reach, confirmed by tick counts unchanged on
  both and `exposed-approach` tick 1's pinned hash unaffected. `254 -> 265`
  green; build 0/0; `-- corpus` 12/12 (`--regenerate` idempotent); `--
  fixture` byte-identical; `-- replay-file envelope-full` checkpoints
  unchanged; `git status` clean; `src/CommandoWar.Sim` packages
  `FSharp.Core` only; source scan clean. No ADR. Merged to `main` (`--no-ff`,
  branch `task-031-hitscan-combat` deleted; not pushed).
