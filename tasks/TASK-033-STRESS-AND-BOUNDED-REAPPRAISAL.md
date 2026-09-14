# TASK-033: Stress and bounded reappraisal

Status: draft (central decisions A–I confirmed with Dave 2026-09-14 before
the phase bodies)
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises backlog B-021 (partial — see Decision A)
Size: M

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
