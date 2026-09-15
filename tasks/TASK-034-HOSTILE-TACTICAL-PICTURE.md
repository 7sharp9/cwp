# TASK-034: Hostile tactical picture and observed-only targeting proof

Status: done (drafted 2026-09-15; central decisions A-F confirmed with
Dave 2026-09-15; implemented 2026-09-15; accepted by Dave and merged to
main 2026-09-15)
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises backlog B-022, partial (see Decision A)
Size: M

## Outcome (2026-09-15)

Implemented on branch `task-034-hostile-tactical-picture`, all six central
decisions (A-F) implemented exactly as confirmed — no deviation found
necessary during implementation.

New `WorldState.HostileTacticalKnowledge: Contact[]`, the identical `Contact`
shape as the friendly `TacticalKnowledge` (TASK-026). Built inside the
existing `tacticalKnowledge` phase slot by a second call to the already
side-agnostic `Perception.mergeKnowledge`, filtered to `Side = Hostile`
instead of `Side = Friendly` — `Perception.fs` itself is unchanged.
`ContactObserved` already fired symmetrically for both sides since TASK-026;
this is simply the first phase that retains a Hostile agent's sightings. The
new store's own `ContactExpired` emissions reuse the identical event shape,
ascending by contact id, emitted after the friendly store's own expiries.

New `Overlay.HostileKnownContact` diagnostic case (Decision C — a distinct
case, not a reuse of `KnownContact`, so a golden render stays visually
distinguishable by which side's picture a marker belongs to): derived in both
`frame` and `frameOf`, rendered as an amber (`#b7791f`) dashed ring labelled
`H<id>` in SVG (`KnownContact`'s ring is purple), and a `hostile known
contact ...` line in ASCII.

Proves, rather than builds, `docs/09` section 8's "enemy does not target an
unobserved player position" (Decision E): `Simulation.combat` already drew
its candidate list from a shooter's own same-tick `VisibleContacts` only
(TASK-031 Decision A) — strictly tighter than anything the stale-tolerant
`HostileTacticalKnowledge` memory could provide — so no `Combat.fs` change
was needed. One new `SimulationTests` fact pins it directly: a Hostile agent
seeded with a stale `HostileTacticalKnowledge` entry for a friendly it can no
longer see (line of sight blocked by an opaque wall) never appears as a
shooter in that tick's `ShotFired` events. `docs/09` section 8's wording is
corrected from "partially realised" to fully realised for the targeting half.

`WorldState.HostileTacticalKnowledge` is genuine per-tick canonical state
(the `TacticalKnowledge` precedent exactly): `Canonical.FormatVersion`
**6 -> 7**, full re-pin — **not** behaviour-neutral this time (Decision D):
`open-engagement`, `perception-contact`, and `exposed-approach` each
legitimately gain a genuine new non-zero `HostileTacticalKnowledge` entry
from the tick a hostile first sees a friendly (tick counts and event counts
unchanged everywhere — see the ledger detail for the corpus diff). No new
corpus entry, no new event type, no ADR (Decision F).

`285 -> 287` green: one new `CanonicalHashTests` fact (the
hostile-tactical-knowledge canonical section / `firstDifferingSection`
label), one new `SimulationTests` fact (the Decision E proof above);
`DeterminismPropertyTests` property 6 extended in place to also ground
`HostileTacticalKnowledge` against a Hostile agent's own observation (same
generator, already deploys hostiles); the perception-contact `DiagnosticsTests`
fact and the exposed-approach `AppraisalDemo` unhandled-overlay
`DiagnosticsTests` fact (`6 -> 8`) both extended in place for the new
symmetric state. `dotnet build` 0/0; `-- corpus` 12/12 (`--regenerate` twice
byte-identical); `-- fixture` format 7, 36 events unchanged; `-- replay-file
envelope-full` OK at canonical 7, 78 events unchanged; `src/CommandoWar.Sim`
packages `FSharp.Core` only; source scan clean. No ADR.

As anticipated, suppress-likely-routes, seek-adjacent-cover-under-pressure,
and scripted fall-back all stay open — B-022 stays `review`, not `done`.

Godot: the pinned `--selfcheck` hash in `AppraisalDemoScene.cs` / `README.md`
was updated for the moved `exposed-approach` tick-1 hash but not
independently re-run through Godot in this session (no Godot install in this
environment) — flagged for Dave to re-check before accepting.

Full detail: `docs/ledger/2026-09-15-TASK-034-hostile-tactical-picture.md`.

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

## Central decisions (confirmed with Dave 2026-09-15 before the phase bodies)

### Decision A — scope cut: hostile tactical picture + observed-only-targeting proof only; suppress-routes, seek-cover, and scripted fallback deferred — CONFIRMED

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

### Decision B — new `WorldState.HostileTacticalKnowledge: Contact[]`, built by calling the existing `Perception.mergeKnowledge` a second time — CONFIRMED

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

### Decision C — new `Overlay.HostileKnownContact` diagnostic case, not a reuse of `Overlay.KnownContact` — CONFIRMED

The `AgentStress`/`AgentSuppression` precedent: a new sparse overlay case
rather than widening an existing one, so the two pictures stay visually and
programmatically distinguishable (a reviewer inspecting a golden render
should be able to tell which side's picture put a marker on a cell without
cross-referencing `WorldState.Agents`). Derived in both `frame` and
`frameOf`, one entry per contact in `HostileTacticalKnowledge`, same fields
as `KnownContact` (cell, contact id, confidence, last-seen tick).

### Decision D — `Canonical.FormatVersion` 6 -> 7, full re-pin; NOT behaviour-neutral this time — CONFIRMED

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

### Decision E — the "does not target an unobserved position" criterion is proven, not built — CONFIRMED

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

### Decision F — no new event type, no new corpus entry, no ADR — CONFIRMED

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

## Verification (run 2026-09-15, before requesting acceptance)

- `dotnet build CommandoWar.slnx -c Release`: `0/0` before and after.
- `dotnet test CommandoWar.slnx -c Release`: `285 -> 287` green. New: 1
  `CanonicalHashTests` fact (the hostile-tactical-knowledge canonical section
  / `firstDifferingSection` label — the `TacticalKnowledge` fact's
  precedent); 1 `SimulationTests` fact (Decision E — a Hostile agent seeded
  with a stale `HostileTacticalKnowledge` entry for a friendly it can no
  longer see, line of sight blocked by an opaque wall, never appears as a
  shooter in that tick's `ShotFired` events). Extended in place (not new
  facts): `DeterminismPropertyTests` property 6 (also grounds
  `HostileTacticalKnowledge` against a Hostile agent's own observation, the
  same generator); the perception-contact `DiagnosticsTests` fact (asserts
  the new `HostileKnownContact` overlay alongside `KnownContact`); the
  exposed-approach `AppraisalDemo` unhandled-overlay `DiagnosticsTests` fact
  (`6 -> 8` — two more `HostileKnownContact` entries this disposable demo
  doesn't render).
- `cwheadless corpus`: 12/12 PASS after regeneration; `--regenerate` twice in
  a row is byte-identical (confirmed via `git diff --stat` before/after the
  second run).
- `cwheadless fixture`: format 7, `36` events unchanged, `0` draws
  (enemy-free).
- `cwheadless replay-file content/replays/envelope-full.cwreplay`:
  checkpoints OK at canonical 7, `24` ticks / `78` events unchanged
  (enemy-free scenario; re-pinned by hand since `envelope-full.cwreplay` is
  not part of `Corpus.all` and so outside the `corpus` verb).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`: `FSharp.Core` only.
- Source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`: clean (comment mentions
  only).
- `git status --porcelain`: matches this task file's "Allowed scope".

Full command output and per-entry hash diffs:
`docs/ledger/2026-09-15-TASK-034-hostile-tactical-picture.md`.

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
- Accepted: yes (2026-09-15)
- Notes: implemented 2026-09-15 on branch `task-034-hostile-tactical-picture`
  (all six central decisions A-F confirmed 2026-09-15, implemented exactly as
  written — no deviation found necessary). Re-verified before accepting:
  `dotnet build` 0/0, `dotnet test` `Passed: 287`, `-- corpus` 12/12
  (`--regenerate` twice byte-identical), `-- fixture` format 7 / 36 events
  unchanged, `-- replay-file envelope-full` OK at canonical 7 / 78 events
  unchanged, `git status` clean, `dotnet list` `FSharp.Core` only. Merged to
  `main` (`--no-ff`, branch `task-034-hostile-tactical-picture` deleted; not
  pushed). B-022 stays `review`, not `done` — suppress-likely-routes,
  seek-adjacent-cover-under-pressure, and scripted fall-back remain open.
  Godot's `--selfcheck` pinned hash was updated for the moved
  `exposed-approach` tick-1 hash but not independently re-run through Godot
  this session (no Godot install here) — Dave should confirm
  `0xB1EBA36EC0A977F4` on his machine when convenient.
