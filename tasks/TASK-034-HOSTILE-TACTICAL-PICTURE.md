# TASK-034: Hostile tactical picture and observed-only targeting proof

Status: ready (drafted 2026-09-15; central decisions A-F proposed below,
not yet confirmed with Dave; not started)
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises backlog B-022, partial (see Decision A)
Size: M

## Objective

Give the Hostile side its own shared tactical picture, symmetric to the
friendly squad's `WorldState.TacticalKnowledge` (TASK-026, backlog B-015),
and add the missing proof that `Simulation.combat` (TASK-031, backlog B-019)
already cannot target a position outside the shooter's own current
visibility. Together these are the "observe and report" and "the enemy does
not target an unobserved player position" halves of `docs/05` section 12's
enemy-doctrine list.

This is a **deliberate narrowing** of B-022's full text ("implement simple
enemy hold-and-defend doctrine"). Of `docs/05` section 12's six doctrine
bullets:

- **hold assigned area** — already true by omission: a `Hostile` agent that
  is never given an `Order` never moves. Nothing to build.
- **observe and report** — not yet real for the Hostile side; this task's
  main piece (Decision B).
- **engage visible targets** — already real (`Simulation.combat`, TASK-031,
  symmetric by construction).
- **suppress likely routes** — needs a `Suppress` order, part of the richer
  order vocabulary in B-030. Out of scope.
- **seek adjacent cover under pressure** — needs a reactive movement
  decision for an agent with no player-issued order, which does not exist in
  any form yet. Out of scope (a materially larger cut — see the "Considered
  and rejected" note below).
- **fall back only under a scenario-defined condition** — needs authored
  fallback conditions and `Withdraw` order semantics. Out of scope.

So this task realises two of six bullets in full and formally closes out a
third ("engage visible targets" already existed but its "unobserved" bound
was never proven). The remaining three stay open on B-022, which is why this
task does **not** move B-022 to `done` — see Decision A.

## Why this task exists

- `docs/05_COMMAND_AND_AGENT_AI.md` section 3: "the shared picture is the
  friendly squad's only: a hostile squad picture, enemy doctrine, and
  'suspected threat class or field of fire' are B-022."
- `docs/05` section 12 ("Enemy AI"): lists the six doctrine bullets above and
  states "a hostile squad tactical picture and doctrine choosing *when* or
  *whether* to engage ... is B-022 — this task's [TASK-031's] automatic
  auto-engage is the mechanic that doctrine will eventually gate, not the
  doctrine itself."
- `docs/09_TEST_STRATEGY.md` section 8: "enemy does not target an unobserved
  player position" is listed as a named cross-module behaviour to prove, and
  is currently "partially realised" — `perception-contact` proves a hostile
  behind an opaque wall is never *observed*, but "the **targeting** half (the
  enemy not firing on an unobserved position) needs the hostile squad
  picture and enemy doctrine, which are B-022."
- `docs/11_BACKLOG.md` B-022: "unblocked by TASK-026, which deliberately
  left it the whole hostile side: a hostile squad tactical picture, enemy
  doctrine reacting to observed contacts, and 'the enemy does not target an
  unobserved player position'." Dependencies B-015 (TASK-026) and B-019
  (TASK-031) are both done, so B-022 is fully unblocked.

Depends only on TASK-026 (backlog B-015, `WorldState.TacticalKnowledge` +
`Perception.mergeKnowledge`) and TASK-031 (backlog B-019, `Simulation.combat`
reading `AgentState.VisibleContacts`), both on `main`.

## Central decisions (proposed 2026-09-15, to confirm with Dave before the phase bodies)

### Decision A — scope cut: hostile tactical picture + observed-only-targeting proof only; suppress-routes, seek-cover, and scripted fallback deferred — PROPOSED

Matches the TASK-032/033 narrow-cut precedent and `docs/05` section 12's own
caution: "do not build a symmetric enemy commander planner before the player
command loop works." Suppress-likely-routes needs a `Suppress` order
(B-030's richer order vocabulary, not built); seek-cover-under-pressure needs
a new reactive-movement decision for an unordered agent (no design exists —
the first such mechanic in the codebase, and a materially larger cut on its
own); scripted fallback needs authored per-scenario conditions and
`Withdraw` semantics (also B-030-adjacent). None of the three fit this
task's `M` size alongside the tactical-picture work. **Because these three
stay unbuilt, B-022 stays `review`, not `done`, after this task** — the same
disposition TASK-033 left B-021 in.

### Decision B — new `WorldState.HostileTacticalKnowledge: Contact[]`, built by calling the existing `Perception.mergeKnowledge` a second time — PROPOSED

`Perception.mergeKnowledge` (TASK-026) is already side-agnostic: it takes a
tick, a prior `Contact[]`, and a `Map<AgentId, Cell>` of this tick's
sightings, with no reference to `Side` anywhere in its body. `Simulation.tacticalKnowledge`
is the only place that filters to `Side = Friendly`
(`s.Agents |> Array.filter (fun a -> a.Side = Friendly)`). The phase gains a
second call filtering to `Side = Hostile` instead, folding into a new
`WorldState.HostileTacticalKnowledge` field the same way. `Perception.fs`
itself needs no change. `ContactObserved` already fires symmetrically for
both sides today (`Simulation.perception` emits it for any agent, either
side, that gains a new entry in `VisibleContacts` — TASK-026 built it that
way even though only the friendly half was consumed until now), so no event
changes there either. The new store's own `ContactExpired` emissions reuse
the existing event shape unchanged (`contact * lastKnownCell`, no side
tag) — a reader distinguishes which picture a given expiry came from by the
contact's own `AgentState.Side`, not by the event, matching the existing
single-shape-event precedent (`docs/05` section 13).

### Decision C — new `Overlay.HostileKnownContact` diagnostic case, not a reuse of `Overlay.KnownContact` — PROPOSED

The `AgentStress`/`AgentSuppression` precedent: a new sparse overlay case
rather than widening an existing one, so the two pictures stay visually and
programmatically distinguishable (a reviewer inspecting a golden render
should be able to tell which side's picture put a marker on a cell without
cross-referencing `WorldState.Agents`). Derived in both `frame` and
`frameOf`, one entry per contact in `HostileTacticalKnowledge`, same fields
as `KnownContact` (cell, contact id, confidence, last-seen tick).

### Decision D — `Canonical.FormatVersion` 6 -> 7, full re-pin; NOT behaviour-neutral this time — PROPOSED

`HostileTacticalKnowledge` is genuine per-tick canonical state (the
`TacticalKnowledge` precedent: `LastSeenTick` and decaying `Confidence`
cannot be recomputed from current positions). Unlike TASK-026's original
2 -> 3 bump — which was behaviour-neutral because no scenario had a hostile
deployment yet — every corpus entry where a hostile currently gains a
friendly in `VisibleContacts` (`open-engagement`, `perception-contact`,
`exposed-approach`, the same three TASK-031/032/033 kept re-pinning) will
now **also** carry a genuine new non-zero `HostileTacticalKnowledge` entry
from the tick that first happens — this is real new state, not a
byte-layout artefact. Every other corpus entry, the fixture, and
`envelope-full` are enemy-free or one-sided and get a behaviour-neutral
re-pin only. No new corpus entry needed — the three already-hostile-bearing
entries exercise it.

### Decision E — the "does not target an unobserved position" criterion is proven, not built — PROPOSED

`Simulation.combat` already draws its shooter's candidate list only from
`shooter.VisibleContacts` (TASK-031 Decision A, "candidates from a shooter's
own `VisibleContacts` only" — the R-023 "same observation contract"
mitigation), which is a same-tick, real-time visibility check strictly
tighter than anything `HostileTacticalKnowledge`'s stale-tolerant memory
could provide. So the targeting half of this criterion has been true since
TASK-031 landed; nothing needs to change in `Combat.fs` or
`Simulation.combat`. This task adds one `SimulationTests` fact (or extends
an existing combat-determinism fact) pinning it explicitly — e.g. an agent
positioned to have a stale `HostileTacticalKnowledge` entry for an
opponent that has since broken line of sight never appears as a shooter in
that tick's `ShotFired` events — and corrects `docs/09_TEST_STRATEGY.md`
section 8's "partially realised ... needs the hostile squad picture and
enemy doctrine" wording to record the targeting half as fully realised
(citing this task), leaving only "observe and report" as the part this task
newly builds.

### Decision F — no new event type, no new corpus entry, no ADR — PROPOSED

New canonical field via the existing ADR-0002 amendment (the
`TacticalKnowledge` / `Suppression` / `Stress` precedent); no new
`PlayerIntent` or order type; no phase-order change (the new call lives
inside the existing `tacticalKnowledge` phase slot). `ContactObserved` /
`ContactExpired` are unchanged event shapes, reused for the new store.

## Allowed scope

- `src/CommandoWar.Sim/Domain.fs` (`WorldState.HostileTacticalKnowledge`,
  module doc updates).
- `src/CommandoWar.Sim/Simulation.fs` (`tacticalKnowledge` phase: a second
  `Perception.mergeKnowledge` call filtered to `Side = Hostile`).
- `src/CommandoWar.Sim/Canonical.fs` (`FormatVersion` 6 -> 7, `encode` +
  `topLevelSections` gain a `HostileTacticalKnowledge` section reusing
  `writeContact`).
- `src/CommandoWar.Sim/Diagnostics.fs` (`Overlay.HostileKnownContact`,
  overlay helpers, `frame` / `frameOf`).
- `src/CommandoWar.Headless/{DiagnosticRender.fs, AppraisalDemo.fs}`
  (exhaustive-match arms for `HostileKnownContact`).
- `tests/CommandoWar.Sim.Tests/*.fs` (new facts/property; full re-pin ripple
  across every hardcoded hash literal).
- `content/replays/*`, `content/diagnostics/*` (full re-pin).
- `src/CommandoWar.Client.Godot/{README.md, src/AppraisalDemoScene.cs}`
  (pinned `--selfcheck` hash, if `exposed-approach` tick 1 moves again).
- `docs/04`, `docs/05`, `docs/07`, `docs/09`, `docs/11`, `docs/12`,
  `PROJECT_STATE.yaml`.

Nothing under `src/_scratch`, `bench/`, `content/benchmarks/BASELINE.md`, or
any Godot spike scene. No new order type or `PlayerIntent` case (B-030). No
reactive/autonomous movement for a Hostile agent (deferred, Decision A). No
change to `Combat.fs` or `Simulation.combat` (Decision E — already correct).

## Considered and rejected

Folding "seek adjacent cover under pressure" in alongside the tactical
picture was considered, since `AgentState.SuppressionBand` (TASK-033) already
exists and is computed for every agent regardless of side. Rejected: no
Hostile agent currently has an `Order` to reappraise, so there is nothing
for a Hostile `SuppressionBand` flip to trigger today — building the
reaction would mean designing the *first* autonomous-movement decision in
the codebase (which cell counts as "adjacent cover", how it interacts with
`Pathfinding`/`Commitment`, whether it needs its own event) from scratch
inside what is supposed to be a tactical-picture task. That is its own task,
sized on its own merits, once Dave wants it.

## Verification plan (to run once implemented, before requesting acceptance)

- `dotnet build CommandoWar.slnx -c Release`: expect 0/0.
- `dotnet test CommandoWar.slnx -c Release`: expect all green, count moves
  by the new facts added.
- `cwheadless corpus`: expect 12/12 PASS after regeneration;
  `--regenerate` twice in a row should be byte-identical.
- `cwheadless fixture`: expect format 7, event count unchanged (no
  Hostile-vs-Friendly contact in the fixture scenario).
- `cwheadless replay-file content/replays/envelope-full.cwreplay`: expect
  checkpoints OK at canonical 7, event count unchanged (enemy-free
  scenario).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`: expect `FSharp.Core` only.
- Source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`: expect clean (comment
  mentions only).
- `git status --porcelain`: expect it to match this task file's "Allowed
  scope".

## Unresolved / follow-ups

- Suppress-likely-routes reappraisal-style behaviour for the Hostile side
  stays open on B-022 — needs the `Suppress` order (B-030).
- Seek-adjacent-cover-under-pressure stays open on B-022 — needs the first
  autonomous-movement decision design in the codebase (see "Considered and
  rejected" above).
- Scripted fall-back-under-condition stays open on B-022 — needs authored
  per-scenario conditions and `Withdraw` order semantics (B-030-adjacent).
- B-023 (canonical refusal and correction scenario) still additionally needs
  B-021's remaining triggers and all of B-022, not just this task's slice.

## Review

- Reviewer: Dave
- Accepted: pending
- Notes: task file drafted only. Selecting it (setting
  `PROJECT_STATE.yaml active_work.selected_task: TASK-034`) and confirming
  Decisions A-F are separate deliberate steps before implementation begins.
