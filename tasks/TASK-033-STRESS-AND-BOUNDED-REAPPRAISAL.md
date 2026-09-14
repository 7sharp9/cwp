# TASK-033: Stress and bounded reappraisal

Status: review (implemented 2026-09-14 on branch `main` directly; central
decisions A–I confirmed with Dave 2026-09-14 before the phase bodies; not
yet accepted)
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises backlog B-021 (partial — see Decision A)
Size: M

## Outcome (2026-09-14)

Implemented directly on `main` (single session, decisions confirmed
interactively before any code). All nine central decisions confirmed with
Dave and implemented as proposed; Decision G (excluding a fulfilled order
from both triggers) was found necessary only once the phase body was
written, not anticipated beforehand — see that section for the mechanism it
would otherwise have broken.

New leaf `src/CommandoWar.Sim/Stress.fs` (`StressConfig`, `Stress.gain` /
`.raise` / `.decay`, pure). `AgentState` gains two genuine canonical fields:
`Stress: int` (raised + decayed together in State consequences, sourced
from `VisibleContacts` only) and `SuppressionBand: bool` (a hysteresis latch
over the existing `AgentState.Suppression`, computed and updated inside the
Appraisal phase). `Appraisal.resolveThreshold` / `.appraise` gain `stress` /
`suppressed` parameters, folded in as a continuous drag and a discrete
penalty respectively, both floored at 0 overall so a fully unexposed route
is never refused for stress or suppression alone. Two new reappraisal
triggers live inside `Simulation.appraisal` itself (not a new `Phases.order`
slot): knowledge-change (any `ContactObserved` / `ContactExpired` this tick,
global) and suppression-band (the latch flipping, either direction) —
both reset an already-appraised, non-fulfilled order's `Disposition` to
`None` so it is re-judged the same tick. `Discipline` deliberately stays
exactly where TASK-028 left it (Decision C).

New sparse `Overlay.AgentStress` (the `AgentSuppression` precedent, bottom-left
corner in the Svg renderer). No new event type — a trigger-driven
reappraisal still emits the existing `OrderAppraised`. No new corpus entry —
proof is via `SimulationTests` facts directly on the phase (a Refused order
flips to Accepted once its only known threat's contact expires after
`PerceptionConfig.ExpireAfter` ticks unseen; the suppression-band latch
reappraises an already-`Accepted` order on either flip direction with no
outcome change on a safe route) plus pure-function facts on `Stress.fs` and
`Appraisal.resolveThreshold`, and one `DeterminismPropertyTests` property.

`AgentState.Stress` and `.SuppressionBand` are genuine per-tick canonical
state: `Canonical.FormatVersion` **5 -> 6**, full re-pin (tick counts and
event counts unchanged everywhere — see "Event-trace and hash impact").
`274 -> 285` green (+10 `SimulationTests` facts, +1 `DeterminismPropertyTests`
property 12 at 200 cases reusing `appraisalCaseGen` unchanged; the existing
combat-determinism `SimulationTests` fact extended in place with `Stress` /
`SuppressionBand` array comparisons). `dotnet build` 0/0; `-- corpus` 12/12
(`--regenerate` idempotent, confirmed via `md5sum`); `-- fixture` format 6,
36 events unchanged; `-- replay-file envelope-full` OK at canonical 6, 78
events unchanged; `src/CommandoWar.Sim` packages `FSharp.Core` only; source
scan clean. No ADR.

Full detail: `docs/ledger/2026-09-14-TASK-033-stress-and-bounded-reappraisal.md`.

## Objective

Turn the **State consequences phase (12.9)**'s "update stress from recent
events" bullet into real behaviour: a new canonical `AgentState.Stress`,
raised while an agent has an opposing-side agent in `VisibleContacts`
("threat", the one `docs/05` section 8 stress source with a system behind it
today) and decayed every tick when safe, folded into the stage-4 resolve
threshold as a continuous drag. Add **two** of the six `docs/05` section 14
reappraisal triggers on top of the existing "a new order is received" one:
knowledge-change (a known threat appears or disappears) and suppression-band
(a new `AgentState.SuppressionBand` hysteresis latch over the already-real
`AgentState.Suppression`, TASK-032). Both reset an already-appraised order's
`Disposition` to `None` so it is re-judged against the current Stress /
SuppressionBand state.

Backlog B-021 as written names six reappraisal triggers (knowledge-change,
exposure-band, suppression-band, wounded, support, leadership) and says the
task "makes discipline / stress / trust per-tick mutable". This task
deliberately narrows that: exposure-band, wounded, support, and leadership
triggers, and dynamic trust, are **out of scope** — see Central decisions A
and C below for why. `AgentState.Discipline` also stays exactly where
TASK-028 left it (static, non-canonical) — Decision C.

## Why this task exists

- `docs/04_SIMULATION_SPEC.md` section 12.9 ("State consequences") lists
  "update stress from recent events" as still unrealised; section 12.5
  ("Appraisal") names "suppression, stress, trust, hysteresis, and the
  exposure-band / knowledge-change triggers" as B-021.
- `docs/05_COMMAND_AND_AGENT_AI.md` section 8 ("Minimal psychological
  model") defines Stress ("accumulates through nearby casualties, wounds,
  isolation, explosions, and threat; decays when safe") but nothing realises
  it yet; section 14 ("Reappraisal triggers") lists six triggers, only "a new
  order is received" realised (TASK-028); section 15 ("Tuning rules") notes
  hysteresis "has nothing to act on until B-021 adds an exposure-band
  reappraisal trigger" — this task substitutes the suppression-band trigger
  instead (Decision A).
- `docs/07_VERTICAL_SLICE.md` section 9 criterion 4 ("a player action can
  predictably change an appraisal outcome") and section 8 ("canonical
  refusal sequence") step 6 ("tactical knowledge and exposure are
  recalculated") both need a live reappraisal trigger to demonstrate; this
  task's knowledge-change trigger is the first one buildable without B-030's
  `Suppress` order.
- TASK-032 (backlog B-020, done) landed `AgentState.Suppression` as a
  "mechanic only" value with no consequence anywhere — this task is the
  first thing that reads it.

Depends only on TASK-028 (backlog B-017, Appraisal phase, done) and TASK-032
(backlog B-020, Suppression, done), both on `main`.

## Central decisions (confirmed with Dave 2026-09-14 before the phase bodies)

### Decision A — scope cut: Stress + suppression-band + knowledge-change only; exposure-band, wounded, support, leadership, and dynamic trust deferred — CONFIRMED

Of B-021's six named triggers, three need systems that do not exist:
wounded (no wound/casualty state — B-031), support (no "required support"
concept — needs `Assault`/`Suppress` commitments, B-030), leadership (no
leadership entity — B-031). Exposure-band is buildable in principle but
needs per-tick route-exposure tracking for every agent with a live order
(recomputing `Appraisal.routeExposure` every tick regardless of Disposition,
plus its own hysteresis band) — a materially larger cut than this task's `M`
size affords, so it stays out too, alongside dynamic trust ("the vertical
slice may initialise trust and leave dynamic trust changes minimal",
`docs/05` section 8 — minimal here means not built this task; nothing
produces "commander history" events to react to yet). Dave confirmed this
narrower cut directly (AskUserQuestion) before drafting.

### Decision B — Stress sourced from VisibleContacts only, raised and decayed together in State consequences — CONFIRMED

Of `docs/05` section 8's five stress sources (casualties, wounds, isolation,
explosions, threat), only "threat" has a system behind it —
`AgentState.VisibleContacts` (TASK-026). New leaf `Stress.fs`
(`StressConfig`, `Stress.gain` / `.raise` / `.decay`, the `Suppression.fs`
precedent): `GainPerTick = 80` while `VisibleContacts` is non-empty this
tick, `DecayPerTick = 30` unconditionally every tick — both applied in State
consequences (docs/04 section 12.9's own phase), since nothing else produces
stress yet, unlike Suppression where gain lives in Combat and decay in State
consequences.

### Decision C — Discipline stays static and non-canonical; only Stress moves into Canonical.encode — CONFIRMED, corrects the backlog wording

B-021's text says the task "moves Discipline into Canonical.encode". But
Discipline itself never mutates in this cut — only the new Stress field
does — so by the project's own mutability rule (the same one that keeps
`CommunicationAvailable` out of the canonical image), Discipline does not
need to move. Dave confirmed keeping it static (AskUserQuestion) over
following the literal backlog text.

### Decision D — resolve threshold gains a continuous Stress drag and a discrete SuppressionBand penalty, floored at 0 — CONFIRMED

`Appraisal.resolveThreshold` and `.appraise` gain `stress: int` and
`suppressed: bool` parameters (read from `AgentState.Stress` /
`.SuppressionBand` at the top of the tick, before this tick's Combat / State
consequences can change them):

```
threshold = max 0 (BaseResolve + DisciplineResolveWeight * discipline
                    + riskMod + urgencyMod
                    - stress / StressDivisor
                    - (if suppressed then SuppressionBandPenalty else 0))
```

`StressDivisor = 25` (max penalty 40 at full stress), `SuppressionBandPenalty
= 30` — both comparable in scale to `RiskAggressive` / `UrgencyImmediate`
(20). The `max 0` floor is deliberate: a completely unexposed route
(`exposure = 0`) must always be `Accepted` regardless of stress or
suppression — both terms only ever make an *already-exposed* route more
likely refused, never refuse a safe one outright.

### Decision E — `AgentState.SuppressionBand: bool` hysteresis latch; suppression-band trigger fires on any flip — CONFIRMED

New canonical bool, computed and updated inside the Appraisal phase itself
(not a new `Phases.order` slot — `docs/04` "changing the phase order
requires an ADR"): `if Suppression >= SuppressionBandEnter (500) then true
elif Suppression <= SuppressionBandExit (300) then false else` (unchanged) —
the classic two-threshold latch so a value oscillating near one boundary
does not flip every tick. Whenever the latch flips and the agent holds an
already-appraised, non-fulfilled order, `Disposition` resets to `None` and
the order is re-judged the same tick.

### Decision F — knowledge-change trigger: any ContactObserved/ContactExpired this tick, global — CONFIRMED

"A relevant known threat appears or disappears" (`docs/05` section 14) is
realised as: any `ContactObserved` or `ContactExpired` event emitted earlier
this tick (Perception / Tactical-knowledge both run before Appraisal) resets
`Disposition` to `None` for every agent with an already-appraised,
non-fulfilled order — global across all agents, not filtered to "was this
agent's own route affected" (that precision is what the deferred
exposure-band trigger would add). The R-023 "same observation contract"
precedent: a broad, simple trigger over a precise, expensive one.

### Decision G — a fulfilled order must never be reset by either trigger — CONFIRMED (found necessary during implementation)

`commitmentAndLocalAction` (immediately after Appraisal) recognises order
completion by `Disposition = Some Accepted, Destination = None, Position =
target`. Resetting `Disposition` for such an agent would re-run
`Appraisal.appraise`'s `fromCell = target` short-circuit, which re-writes
`Destination = Some target` and silently defeats that clear. Both triggers
therefore exclude the fulfilled case explicitly.

### Decision H — no new event type; no new corpus entry — CONFIRMED

A trigger-driven reappraisal still emits the existing `OrderAppraised` event
(unchanged shape) — the same event a fresh order's appraisal emits, per
`docs/05` section 13 "every appraisal emits one `OrderAppraised`". Proof is
via `SimulationTests` facts directly (a Refused-to-Accepted flip on contact
expiry; a same-tick `OrderAppraised` on a SuppressionBand flip) rather than a
new committed corpus entry — the TASK-032 "decay/clamping proved on the pure
functions" precedent, extended to phase-level facts here since the trigger
logic itself is not a pure leaf function.

### Decision I — no ADR — CONFIRMED

New leaves, new canonical fields via the existing ADR-0002 amendment, no new
dependency, no phase-order change.

## Allowed scope

- `src/CommandoWar.Sim/Stress.fs` (new).
- `src/CommandoWar.Sim/Domain.fs` (`AgentState.Stress`, `.SuppressionBand`,
  `Agent.create` defaults).
- `src/CommandoWar.Sim/Appraisal.fs` (`AppraisalConfig` additions,
  `resolveThreshold` / `appraise` signatures, module doc).
- `src/CommandoWar.Sim/Simulation.fs` (`appraisal`, `stateConsequences`).
- `src/CommandoWar.Sim/Canonical.fs` (`FormatVersion` 5 -> 6, `writeAgent`).
- `src/CommandoWar.Sim/Diagnostics.fs` (`Overlay.AgentStress`, overlay
  helpers, `frame` / `frameOf`).
- `src/CommandoWar.Sim/CommandoWar.Sim.fsproj` (new file entry).
- `src/CommandoWar.Headless/{DiagnosticRender.fs, AppraisalDemo.fs}`
  (exhaustive-match arms for `AgentStress`).
- `tests/CommandoWar.Sim.Tests/*.fs` (new facts/property; full re-pin ripple
  across every hardcoded hash literal).
- `content/replays/*`, `content/diagnostics/*` (full re-pin).
- `src/CommandoWar.Client.Godot/{README.md, src/AppraisalDemoScene.cs}`
  (pinned `--selfcheck` hash).
- `docs/04`, `docs/05`, `docs/07`, `docs/11`, `docs/12`, `PROJECT_STATE.yaml`.

Nothing under `src/_scratch`, `bench/`, `content/benchmarks/BASELINE.md`, or
any Godot spike scene.

## Event-trace and hash impact (`Canonical.FormatVersion` 5 -> 6, full re-pin)

Every corpus entry, the fixture, and `envelope-full` re-pin — the format
bump touches every `writeAgent` call regardless of behaviour. **Tick counts
and event counts are unchanged everywhere** (no new event type, and both
reappraisal triggers only ever flip an already-appraised order's outcome,
never add or remove a tick or event) — confirmed by diffing every corpus
`.md` / `envelope-full.md` for a changed "Tick count" or "Domain events"
line (none found, only hash-line and, for the entries below, new overlay
text).

| Entry | Newly non-zero this task | Note |
|---|---|---|
| `exposed-approach` | `Stress` (all three agents, from tick 1) | The scenario's own premise — "past a stationary hostile the squad sees from the start" — puts all three agents in mutual `VisibleContacts` from tick 1, so State consequences raises `Stress` to 50 (net of the same-tick decay) for all three by the end of tick 1. The tick-1 `Refused`/`Accepted` divergence itself is unaffected — Appraisal reads `Stress` from *before* this tick's rise (0), the `Discipline` precedent. |
| `open-engagement`, `perception-contact` | `Stress` from the tick each pair first sees each other (at or before the tick TASK-031 pinned first combat) | Same mechanism — mutual visibility precedes engagement range. |
| every other entry, the fixture, `envelope-full` | nothing (no agent ever sees an opposing agent) | Byte-layout-only re-pin. |

The TASK-029 Godot demo's `--selfcheck` pinned `exposed-approach` tick-1 hash
moved a third time (`0x2FA6E43B32599EE5` -> `0x2066BC1FAF990E4A`, TASK-032's
value -> this task's), updated in `AppraisalDemoScene.cs` / `README.md`.
`content/replays/CORPUS.md` gained a new "Re-pinned by TASK-033" paragraph
(prior re-pin paragraphs left untouched as historical record, the
`Canonical.fs` version-history-comment precedent).

## Verification

- `dotnet build CommandoWar.slnx -c Release`: 0/0 before and after.
- `dotnet test CommandoWar.slnx -c Release`: `274 -> 285` green. New: 3
  `Stress.gain`/`.raise`/`.decay` pure-function facts (the `Suppression.fs`
  precedent); 3 `Appraisal.resolveThreshold` facts (suppressed drops by
  exactly `SuppressionBandPenalty`; full stress drops by exactly `MaxStress /
  StressDivisor`; floored at 0 even at minimum discipline, cautious risk,
  full stress, and suppressed); 2 phase-level Stress facts (an agent in
  contact gains net `GainPerTick - DecayPerTick`, one with none stays at 0;
  Stress accumulates over continuous contact and decays once contact is
  lost); 1 knowledge-change-trigger fact (a `Refused` order becomes
  `Accepted` once its only known threat's contact expires, 60 ticks, no real
  hostile agent needed since `Appraisal.appraise` reads
  `WorldState.TacticalKnowledge` directly); 1 suppression-band-trigger fact
  (an `Accepted` order is reappraised — a fresh `OrderAppraised`, outcome
  unchanged on a safe route — on either latch flip direction, and not on a
  quiet tick with the band unchanged); 1 `DeterminismPropertyTests` property
  12 at 200 cases (both `Stress` and `SuppressionBand` reproducible from a
  fresh recompute, reusing `appraisalCaseGen` unchanged); the existing
  combat-determinism `SimulationTests` fact extended in place with `Stress` /
  `SuppressionBand` array comparisons (not a new fact).
- `cwheadless corpus`: 12/12 PASS (no new entry); `--regenerate` twice in a
  row is a byte-identical zero diff (idempotent, confirmed via `md5sum`).
- `cwheadless fixture`: format 6, `36` events unchanged, `0` draws
  (enemy-free).
- `cwheadless replay-file content/replays/envelope-full.cwreplay`:
  checkpoints OK at canonical 6, `24` ticks / `78` events unchanged
  (enemy-free scenario).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`: `FSharp.Core` only.
- Source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`: clean (comment mentions
  only, pre-existing).
- `git status --porcelain`: matches this task file's "Allowed scope".

## Unresolved / follow-ups

- Exposure-band reappraisal trigger, and its own hysteresis, stays deferred
  — needs per-tick route-exposure tracking for every agent with a live
  order, out of this task's cut (Decision A).
- Wounded, support, and leadership reappraisal triggers stay named, not
  built — each needs a system this task deliberately does not add (wound /
  casualty state, a support-commitment concept, a leadership entity —
  B-030 / B-031).
- Dynamic trust stays unbuilt (`docs/05` section 8's "minimal" vertical-slice
  allowance) — nothing produces "commander history" events to react to yet.
- `docs/05` section 15's hysteresis constant note anticipated it landing
  with the exposure-band trigger; this task substitutes the
  suppression-band trigger instead (Decision A) — `docs/05` updated to
  reflect this.

## Review

- Reviewer: Dave
- Accepted: pending
- Notes: (to be filled in on review)
