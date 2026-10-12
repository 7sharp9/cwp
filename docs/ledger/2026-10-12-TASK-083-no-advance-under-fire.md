## 2026-10-12 - TASK-083 - A routine MoveTo does not advance on a threat that already has the agent in range

**Owner:** Dave with coding-agent assistance
**Source revision:** `99f5dca` plus this change, branch `claude/cool-hypatia-j4rswr`
**Environment:** Linux cloud container, .NET SDK `10.0.112` only (pinned
`10.0.303` not installed), `-p:NuGetAudit=false`, built from scratch directories
outside the repository. No xunit or FsCheck packages in the offline cache, so
`dotnet test` could not run; the test files were compiled and run against a
stand-in `Xunit` module (below). No Godot.
**Status change:** `ready -> active -> review` (selected by Dave, 2026-10-12)

### Origin

Dave asked for D-01 to be fixed with threat-aware pathing. A probe showed that
pathing cannot change the failing case, Dave chose a first-contact reaction
instead, and TASK-083 was drafted for it (`tasks/TASK-083-NO-ADVANCE-UNDER-FIRE.md`).

### Baseline timeline (step 1, before the change)

Agent 5 on real Bridgehead content, seed 20260920, ordered at tick 1 from
`(4,8)` straight to `(12,9)`:

| Tick | Event |
|---|---|
| 1 | `Accepted`; `TacticalKnowledge` empty (tick-0 count 0) |
| 8 | agent steps onto `(7,9)` |
| 9 | `ContactObserved` 102 at `(13,9)`, distance 6; `OrderAppraised Accepted` (same tick); `ShotFired` both sides |
| 10 | agent steps to `(8,9)` |
| 12 | `Refused (RouteTooExposed (Some 102))`, after hits raised stress and lowered the threshold |

The reappraisal is not a tick late, which contradicts `docs/15` D-01,
`docs/07` section 9 and the TASK-073 ledger. All three are corrected (docs/15
section 6 records it).

### Change

`Appraisal.fs`: new private `closingThreat`, called from the shared `moveLike`
pipeline for `MoveTo` only. A route at or under the threshold is refused with
`RouteTooExposed (Some t)` when threat `t` has `cellPressure > 0` on the agent's
current cell and some route cell with `cellPressure > 0` is strictly nearer to
`t` (Chebyshev). Nearest such threat, ties by ascending id, is named. `Immediate`
urgency and `Aggressive` risk skip it. It reuses `cellPressure`, so a suppressed
threat, a cell out of range or out of sight, and a fully covered cell all
disable it, and a route with no exposed cell is never refused (the
"unexposed route is always accepted" invariant holds). No new state, no new
reason, no `Canonical` change, no new reappraisal trigger.

Deviation from the task text: the task said "within `ThreatEngagementRange`, in
line of sight, and some route cell strictly closer"; the implementation adds
"not fully covered" and "the closer route cell is itself exposed", because both
come for free from `cellPressure` and keep the safe-route invariant.

### Results

| Check | Result |
|---|---|
| Un-staged repro after the change | `Refused (RouteTooExposed (Some 102))` on tick 9, the contact tick; agent 5 stays at `(7,9)` and never advances |
| Hidden-hostile test world (wall at x=6, hostile at `(10,5)`) | old build: accepted at first contact, advances to `(8,8)`, refused tick 9. New: refused on contact tick 7, holds `(6,8)` |
| `dotnet build` Sim, Headless, Godot Core | 0 warnings, 0 errors |
| `cwheadless corpus` before regeneration | 19 entries match; `exposed-approach` (first bad tick 3), `bridgehead-succeeded` (139), `bridgehead-failed` (28) diverge |
| `corpus --regenerate` then `corpus` | `OK - all 22 entries match` |
| Stand-in run of the 11 test files that need no FsCheck | pass=383 fail=0 (see caveats) |
| Godot self-check constants | `DemoRenderScene` `0x6213D672BC36FDB8` and `CommandDemoScene` `0x300BB18492CEB355` recomputed with the changed build, unchanged. `AppraisalDemoScene` (tick 1) and `chokepoint-detour` pins are in entries that did not move |

### Corpus effect (events diffed old build vs new, per entry)

| Entry | First moved tick | Effect | Events |
|---|---|---|---|
| `exposed-approach` | 3 | agent 1 (Discipline 6) refused at `(3,5)` on tick 3, holds; tick 5 hostile's band latches, both accepted, agent 1 steps and is incapacitated | 48 -> 45 |
| `bridgehead-succeeded` | 139 | agent 5's closing push to `(12,9)` held ticks 139 to 141 on a stale contact, resumes 142 and completes 151 instead of 148 | 470 -> 469 |
| `bridgehead-failed` | 28 | agent 5 held at `(7,9)` instead of walking to `(8,9)` and is incapacitated there | 361 -> 358 |

Unchanged: `bridgehead-succeeded` `Succeeded` at tick 215 (objective 2 at 160),
the stand-off refusal at tick 6, the acceptance at tick 83; `bridgehead-failed`
`Failed` at tick 84; the other 19 entries, including
`canonical-refusal-and-correction` and `suppress-relieves-exposure`.

Goldens: `bridgehead-failed-tick-084.*` regenerated; new
`exposed-approach-tick-003.*` with a `DiagnosticsTests` fact covering the
refusal frame.

Tests added (`SimulationTests.fs`): refusal under the threshold, accepted before
range / retreat / sidestep, `Immediate` and `Aggressive` press on and `Cautious`
does not, suppression reverses, nearest threat named, `Hold` / `Assault` /
`Withdraw` / `Suppress` unaffected, first sight inside range is refused on that
tick and the agent stays put, determinism. One existing assertion changed
(`Suppress and Assault orders from an unarmed agent...`): its `MoveTo` ran past
an adjacent hostile and is now refused as `RouteTooExposed`; it targets a cell
away from the hostile so it still tests "ammunition does not block walking".

### Unresolved concerns

1. **The rule overrides discipline for an exposure the agent knowingly
   accepted.** In `exposed-approach` the Discipline 6 agent accepted the exposed
   route on tick 1 and is refused on tick 3 once the hostile has it in range.
   The tick-1 divergence between agents 0 and 1, which is the G3 evidence, is
   unchanged, but the corpus story that "discipline alone decides who walks" no
   longer holds past tick 3. `docs/05` section 5 and `docs/15` L-07 state this.
   An alternative that keeps discipline ordering is an "under fire" term in
   `resolveThreshold` (like `SuppressionBandPenalty`) instead of a separate
   rule. It adds a tuned constant, so it was not built here.
2. **The agent halts in the open, still in range, and keeps trading fire.** The
   task does not move it to cover (a follow-up if playtest shows it is worse than
   pressing on).
3. **Stale contacts.** `bridgehead-succeeded` ticks 139 to 141: the rule holds
   agent 5 on a contact of rifleman 102, who was incapacitated at tick 82 and is
   still in `TacticalKnowledge` until it expires. Pre-existing knowledge model;
   not changed here.
4. **The refusal does not prevent the first exchange of fire**, which Combat
   resolves on the contact tick. Recorded as L-07.
5. **`dotnet test` was not run.** The stand-in harness (a scratch `Xunit` module
   with `Fact`, `Theory` and the used `Assert` members, run by reflection)
   covers `BaselineTests`, `SimulationTests`, `RandomTests`,
   `CanonicalHashTests`, `FixtureTests`, `TerrainTests`, `SightTests`,
   `PathfindingTests`, `ScenarioTests`, `DiagnosticsTests` and `CorpusTests`. It
   does not cover `DeterminismPropertyTests` (FsCheck), `BenchmarkTests`,
   `ReplayTests` or `ScenarioFileTests`. CI is the authoritative run; the
   expected count is 438 (429 pinned plus 9 new facts), and the pinned "Green
   tests" fact in `docs/12` is left at 429 until CI confirms it.
