## 2026-09-14 - TASK-033 - Stress and bounded reappraisal

**Owner:** Dave with coding-agent assistance
**Branch:** `task-033-stress-and-bounded-reappraisal` (originally
implemented directly on `main`, single session; rebased onto this branch
2026-09-15 before acceptance, for consistency with the established
per-task branch workflow — the two commits were not pushed, so the move
was free; not pushed).
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; FSharp.Core
10.1.303; xUnit 2.9.3; FsCheck 3.3.4
**Status change:** TASK-033 drafted `-> ready -> review -> done`; backlog
B-021 `proposed -> review -> done`

Realises `docs/04` section 12.9's "update stress from recent events" bullet
and two of `docs/05` section 14's six reappraisal triggers (knowledge-change,
suppression-band). Turns the resolve threshold into a function of Stress and
the new `SuppressionBand` hysteresis latch, and adds two live reappraisal
triggers on top of TASK-028's "a new order is received".

### Central decisions (confirmed with Dave 2026-09-14 before the phase bodies)

- **A - scope cut: Stress + suppression-band + knowledge-change only.**
  Confirmed directly with Dave via a scope question before drafting: of
  B-021's six named triggers, wounded/support/leadership need systems that
  do not exist (wound state, a support-commitment concept, a leadership
  entity — B-030/B-031); exposure-band needs per-tick route-exposure
  tracking, a materially larger cut; dynamic trust stays "minimal" per
  `docs/05` section 8 — nothing produces commander-history events yet.
- **B - Stress sourced from `VisibleContacts` only.** Of `docs/05` section
  8's five stress sources, only "threat" has a system behind it. New leaf
  `Stress.fs`; raised (`GainPerTick = 80`) and decayed (`DecayPerTick = 30`)
  together in State consequences, since nothing else produces stress yet
  (unlike Suppression, whose gain lives in Combat).
- **C - Discipline stays static and non-canonical.** Confirmed with Dave,
  overriding B-021's literal backlog text ("moves Discipline into
  Canonical.encode") — Discipline itself never mutates in this cut, so by
  the project's own mutability rule it does not need to move; only the new
  `Stress` field does.
- **D - resolve threshold gains a continuous Stress drag and a discrete
  SuppressionBand penalty, both floored at 0 overall.** `stress /
  StressDivisor (25)` up to 40 at full stress; `SuppressionBandPenalty = 30`
  while latched. The overall `max 0` floor guarantees a fully unexposed
  route is always `Accepted` regardless of stress/suppression.
- **E - `AgentState.SuppressionBand: bool` hysteresis latch.** Enter at
  `Suppression >= 500`, exit at `<= 300` — the 200-point gap exceeds one
  `SuppressionConfig.DecayPerTick` (50) so decay alone cannot cross it in
  one tick right at a boundary. Computed inside the Appraisal phase itself
  (no new `Phases.order` slot — changing phase order needs an ADR).
- **F - knowledge-change trigger: global, not per-route.** Any
  `ContactObserved` / `ContactExpired` this tick resets every agent's
  already-appraised order — broader than "was this agent's route affected"
  (the deferred exposure-band trigger's job), the R-023 "same observation
  contract" precedent.
- **G - a fulfilled order must never be reset by either trigger.** Found
  necessary only once the phase body was written: `commitmentAndLocalAction`
  relies on seeing `Disposition = Some Accepted, Destination = None,
  Position = target` unchanged to recognise completion. Resetting it here
  would re-run `Appraisal.appraise`'s `fromCell = target` short-circuit,
  re-write `Destination`, and silently defeat that clear. Both triggers now
  explicitly exclude the fulfilled case.
- **H - no new event type, no new corpus entry.** A trigger-driven
  reappraisal still emits the existing `OrderAppraised`. Proof is via
  `SimulationTests` facts on the phase (not pure leaf functions, so the
  TASK-032 "prove on the pure functions" precedent doesn't fully apply) plus
  pure-function facts on `Stress.fs` / `resolveThreshold`.
- **I - no ADR.** New leaves, new canonical fields via the existing
  ADR-0002 amendment, no new dependency, no phase-order change.

### Stress constants (`src/CommandoWar.Sim/Stress.fs`) and new `AppraisalConfig` entries

| Constant | Value | Rationale |
|---|---:|---|
| `StressConfig.MaxStress` | 1000 | The `SuppressionConfig.MaxSuppression` precedent. |
| `StressConfig.GainPerTick` | 80 | No numeric target given in `docs/05`; a continuous per-tick source needs smaller numbers than Suppression's single-shot `GainOnHit` (400). |
| `StressConfig.DecayPerTick` | 30 | Smaller than `GainPerTick` so continuous contact still nets positive stress; roughly the `SuppressionConfig.DecayPerTick` pacing precedent. |
| `AppraisalConfig.StressDivisor` | 25 | `MaxStress / StressDivisor = 40` at full stress — comparable to `RiskAggressive`/`UrgencyImmediate` (20), never dominant alone. |
| `AppraisalConfig.SuppressionBandEnter` | 500 | Half of `MaxSuppression` — "heavily suppressed", not merely shot at once. |
| `AppraisalConfig.SuppressionBandExit` | 300 | Below `SuppressionBandEnter`; the 200-point gap exceeds one `DecayPerTick` (50). |
| `AppraisalConfig.SuppressionBandPenalty` | 30 | Comparable to `RiskCautious`'s magnitude. |

### Event-trace and hash impact (`Canonical.FormatVersion` 5 -> 6, full re-pin)

Every corpus entry, the fixture, and `envelope-full` re-pin — the format
bump touches every `writeAgent` call regardless of behaviour. **Tick counts
and event counts are unchanged everywhere**: no new event type, and both
reappraisal triggers only ever flip an already-appraised order's outcome,
never add or remove a tick or event — confirmed by diffing every corpus
`.md` / `envelope-full.md` for a changed "Tick count" or "Domain events"
line (none found).

`exposed-approach` additionally carries a genuine new non-zero `Stress`
value from tick 1 onward: the scenario's own premise ("past a stationary
hostile the squad sees from the start") puts all three agents in mutual
`VisibleContacts` from tick 1, so State consequences raises `Stress` to 50
(net of the same-tick decay) for all three by the end of tick 1 — the
tick-1 `Refused`/`Accepted` divergence itself is unaffected, since Appraisal
reads `Stress` from before this tick's rise (0), the `Discipline` precedent.
`open-engagement` and `perception-contact` gain `Stress` similarly from the
tick each pair first sees each other. The TASK-029 Godot demo's
`--selfcheck` pinned `exposed-approach` tick-1 hash moved a third time
(`0x2FA6E43B32599EE5` -> `0x2066BC1FAF990E4A`), updated in
`AppraisalDemoScene.cs` / `README.md`. The disposable `AppraisalDemo`
`unhandled`-overlay `DiagnosticsTests` fact's expected count moved `3 -> 6`
(three agents now also carry a sparse `AgentStress` overlay this demo
doesn't render) — genuine new behaviour, not a bug.

### Verification

- `dotnet build CommandoWar.slnx -c Release`: 0/0 before and after.
- `dotnet test CommandoWar.slnx -c Release`: `274 -> 285` green. New: 3
  `Stress.gain`/`.raise`/`.decay` pure-function facts; 3
  `Appraisal.resolveThreshold` facts (suppressed penalty exact; full-stress
  penalty exact; floored at 0 at the extremes); 2 phase-level Stress facts
  (net gain in contact / stays 0 without; accumulates then decays); 1
  knowledge-change-trigger fact (Refused -> Accepted on contact expiry, 60
  ticks, no real hostile agent needed — `Appraisal.appraise` reads
  `TacticalKnowledge` directly); 1 suppression-band-trigger fact (reappraises
  on either latch flip, outcome unchanged on a safe route, silent on a quiet
  tick); 1 `DeterminismPropertyTests` property 12 at 200 cases (`Stress` +
  `SuppressionBand` reproducible from a fresh recompute, `appraisalCaseGen`
  unchanged); the existing combat-determinism fact extended in place with
  `Stress`/`SuppressionBand` array comparisons.
- `cwheadless corpus`: 12/12 PASS; `--regenerate` twice in a row byte-identical
  (confirmed via `md5sum`).
- `cwheadless fixture`: format 6, `36` events unchanged.
- `cwheadless replay-file content/replays/envelope-full.cwreplay`:
  checkpoints OK at canonical 6, `78` events unchanged.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`: `FSharp.Core` only.
- Source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`: clean.
- `git status --porcelain`: matches the task file's "Allowed scope".

### Unresolved / follow-ups

- Exposure-band trigger and its own hysteresis stay deferred — needs
  per-tick route-exposure tracking for every agent with a live order.
- Wounded, support, and leadership triggers stay named, not built (B-030 /
  B-031).
- Dynamic trust stays unbuilt — no commander-history events exist yet.
- `docs/05` section 15's hysteresis note (originally tied to the
  exposure-band trigger) is corrected to point at the suppression-band
  trigger this task actually built it on.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-15)
- Notes: Re-verified independently before accepting rather than trusting the
  implementation-time claims: `dotnet build CommandoWar.slnx -c Release` 0/0;
  `dotnet test` `Passed: 285`; `cwheadless corpus` 12/12 PASS; `cwheadless
  fixture` format 6, `36` events unchanged; `cwheadless replay-file
  content/replays/envelope-full.cwreplay` checkpoints OK at canonical 6, `78`
  events unchanged; `git status --porcelain` clean; `dotnet list
  src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  `FSharp.Core` only; source scan clean. All match the ledger detail above
  exactly. The two commits were rebased from `main` directly onto branch
  `task-033-stress-and-bounded-reappraisal` before this acceptance, for
  consistency with every prior task's branch/accept/merge workflow (both
  commits were unpushed, so this cost nothing). Merged to `main` (`--no-ff`,
  branch `task-033-stress-and-bounded-reappraisal` deleted; not pushed).
