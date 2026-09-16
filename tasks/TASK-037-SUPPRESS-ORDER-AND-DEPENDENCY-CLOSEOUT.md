# TASK-037: Minimal Suppress order and B-021/B-022 dependency closeout

Status: done (drafted 2026-09-16; central decisions A-K confirmed with
Dave 2026-09-16 before the phase bodies; implemented and self-verified
2026-09-16; accepted by Dave and merged to main 2026-09-16)
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises a thin slice of B-030 (P4, pulled forward
as P3 decision-support, the TASK-029/034 precedent) and closes out B-021 and
B-022's remaining open scope by explicit descope
Size: L

## Outcome (2026-09-16)

Implemented exactly as the confirmed decisions A-K describe — no deviation
found necessary during implementation. `PlayerIntent.Suppress of target:
AgentId` and `DecisionReason.TargetNotKnown` added to `Domain.fs`;
`Appraisal.appraise`'s new `Suppress` branch appraises on stage 2 alone.
`Commitment.Suppressing of SuppressCommitment` added; the executor holds
position and Combat pins the named contact as the sole candidate.
`Appraisal.routeExposure`/`cellPressure` gained `suppressedThreats:
AgentId[]` and zero a latched threat's pressure. A third reappraisal
trigger (`threatSuppressionChanged`, global scope) reappraises a different
agent's non-fulfilled order. New `suppress-relieves-exposure` corpus entry
(12 x 8, 2 friendlies + 1 hostile, 10 ticks) proves the mechanism end to end
— a scratch probe against the real build showed the `SuppressionBand` flip
landing at tick 4 (several shots landed as hits), comfortably inside the
worst-case bound of tick 5 the design targets. `Canonical.FormatVersion`
`7 -> 8`, full re-pin (behaviour-neutral except the new entry).
`ReplaySerialisation` gained a `suppress <targetAgentId>` keyword with no
version bump (its grammar had already left room for it). B-021 and B-022
moved to `done` (reduced scope, explicit descope recorded in `docs/11`);
B-023's dependency list is now fully `done`.

`287 -> 293` green (+6: five new `SimulationTests` facts — unknown-target
`Unable(TargetNotKnown)`; known-target `Accepted` with a `Suppressing`
commitment; `routeExposure` zeroing at the pure-function level; a threat's
`SuppressionBand` flip reappraising a *different* agent's `Refused` order
via direct field mutation (no combat RNG); a `Suppressing` agent firing at
its named target over a nearer contact — plus one extension to the existing
`Commitment.ofAgent` enumeration fact). `dotnet build` 0/0 (zero warnings,
`TreatWarningsAsErrors` caught every incomplete-match site the new cases
touched). `-- corpus` 13/13 (`--regenerate` twice byte-identical, confirmed
via a directory diff). `-- fixture` format 8, 36 events unchanged. `--
replay-file envelope-full` OK at canonical 8, 78 events unchanged (re-pinned
by hand — not part of `Corpus.all`, the TASK-032/033/034 precedent). `dotnet
list` `FSharp.Core` only. Source scan clean.

Godot: the pinned `--selfcheck` hash in `AppraisalDemoScene.cs`/`README.md`
was updated for the moved `exposed-approach` tick-1 hash
(`0x5D5A30C0DF64AC93`) but not independently re-run through Godot in this
session (no Godot install in this environment) — flagged for Dave to
re-check before accepting, the TASK-034 precedent exactly.

Full detail: `docs/ledger/2026-09-16-TASK-037-suppress-order-and-dependency-closeout.md`.

## Objective

Give the player a minimal `Suppress` order whose only behavioural effect is
to reduce a specific known threat's contribution to another agent's stage-3
route exposure while that threat is actively suppressed, and use it to
retire B-021 and B-022 from `review` to `done` (reduced scope) so that
B-023's backlog-row dependency list (`B-018, B-020, B-021, B-022`) is fully
`done`. B-023 itself (the full canonical refusal-and-correction corpus
scenario) is **not** built by this task — this task only clears its
dependency blockage and proves the one missing mechanic (`docs/07` section 8
step 5-6) that B-023 will need.

## Why this task exists

`docs/07_VERTICAL_SLICE.md` section 8 (the canonical refusal sequence,
G3's headline evidence item) names step 5 ("the player orders another
fireteam to suppress the machine-gun position") and step 6 ("tactical
knowledge and exposure are recalculated") as the parts TASK-033/034 left
unbuilt because they need "the `Suppress` order itself (B-030 ...) that
step 5 needs to fire." `docs/05` section 16 ("Exposed road") names the same
gap directly: "an ally suppressing the machine gun reduces the route's
exposure ... a `Suppress` order that reduces a threat's contribution to
stage-3 exposure (B-030) — neither exists yet."

B-023's backlog row lists dependencies `B-018, B-020, B-021, B-022` — all
formally required `done` before B-023 can be selected (`docs/11_BACKLOG.md`
section 7, "all dependencies are done"). B-021 and B-022 currently sit at
`review`, each carrying its own explicit escape hatch: B-021's row says
"stays `review`, not `done`, until those [remaining triggers] land or are
explicitly descoped"; B-022's row uses identical wording. TASK-033 and
TASK-034 both left their remaining scope open deliberately (see each task
file's "Deliberately narrower than B-0NN's full backlog text" section) and
neither has since been extended. Nothing in this task's Suppress-order slice
touches either row's remaining open items (they are Hostile-doctrine or
B-031-dependent), so this task descopes both rows explicitly rather than
leaving them to accumulate more "review, partial" history against work that
was never planned to close them.

Dave confirmed the overall direction 2026-09-16 (AskUserQuestion): "do
whichever is needed for the most progress; if more than one task unblocks
more, merge them" — the merge here is the Suppress-order slice (which
unblocks B-023's missing mechanic) plus the B-021/B-022 descope (which
unblocks B-023's formal dependency list), since neither alone fully clears
B-023 for selection.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `docs/04_SIMULATION_SPEC.md` sections 12.5 (Appraisal), 12.6 (Commitment),
  12.8 (Combat), 14 (reappraisal triggers)
- `docs/05_COMMAND_AND_AGENT_AI.md` sections 4 (order vocabulary — Suppress),
  5 (stages 3-4), 7 (`DecisionReason` vocabulary — `TargetNotKnown`), 9
  (`Commitment` — `Suppressing of SuppressCommitment`), 14 (reappraisal
  triggers), 16 ("Exposed road")
- `docs/07_VERTICAL_SLICE.md` section 8
- `docs/11_BACKLOG.md` rows B-021, B-022, B-023, B-030
- `tasks/TASK-028-ORDER-APPRAISAL-AND-TYPED-REASONS.md`,
  `TASK-030-COMMITMENT-AND-FINITE-EXECUTOR.md`,
  `TASK-032-SUPPRESSION-AND-EXPOSURE.md`,
  `TASK-033-STRESS-AND-BOUNDED-REAPPRAISAL.md`,
  `TASK-034-HOSTILE-TACTICAL-PICTURE.md`
- `src/CommandoWar.Sim/{Domain.fs, Appraisal.fs, Commitment.fs, Combat.fs,
  Simulation.fs, Canonical.fs}`

## Dependencies

- B-018 (TASK-030, done), B-020 (TASK-032, done) — both already satisfied;
  this is why B-030 itself is formally unblocked despite being P4.
- B-021 (TASK-033, review) and B-022 (TASK-034, review) — this task descopes
  both to `done`, in parallel with (not blocked by) the Suppress-order work.

## Central decisions (confirmed with Dave 2026-09-16 before the phase bodies)

### Decision A — combined scope: thin B-030 Suppress slice + explicit B-021/B-022 descope — CONFIRMED

Matches Dave's "merge if more than one task unblocks more" direction. The
Suppress order alone would not satisfy B-023's dependency list (B-021/B-022
would still read `review`); the descope alone would not supply the mechanic
`docs/07` section 8 step 5-6 needs. Doing both in one task is what actually
gets B-023 to "selectable" per `docs/11` section 7.

### Decision B — `PlayerIntent.Suppress of target: AgentId`, not a `Cell` — CONFIRMED

`Suppress` targets a specific known contact (an entry in the issuing side's
own `WorldState.TacticalKnowledge`/`HostileTacticalKnowledge`), not a bare
map cell. `Appraisal.routeExposure` already keys its per-threat pressure
contribution by `Contact.Contact: AgentId` (`Appraisal.fs` line ~181), so an
`AgentId` target lets the new exposure-reduction logic (Decision D) look up
"is my route's contributing threat currently the one under suppression"
directly, with no area-matching geometry needed. `docs/05` section 4's
"known or suspected threat area" wording covers a *suspected* (unconfirmed,
no id) target too, but no suspected-threat model exists anywhere in the
codebase yet — out of scope, matching every prior task's "no speculative
type machinery" discipline.

### Decision C — new `DecisionReason.TargetNotKnown` case — CONFIRMED

`docs/05` section 7 and `Domain.fs`'s own doc comment both already name
`TargetNotKnown` as part of the full vocabulary, earmarked for the systems
that can trigger it (B-019/B-020/B-021/B-030) — this is that system. A
`Suppress` order naming a contact absent from the issuing agent's own
tactical knowledge appraises to `Unable(TargetNotKnown, [||])`.

### Decision D — new `Commitment.Suppressing of SuppressCommitment` — CONFIRMED

`{ Command: CommandId; Target: AgentId }` — the `MoveCommitment` precedent,
and the exact shape `docs/05` section 9 already names
("`Suppressing of SuppressCommitment`"). `Commitment.ofAgent` gains a case:
`Accepted` + `Intent = Suppress target` -> `Suppressing { Command = ...;
Target = target }`. `Commitment` stays derived, non-canonical (the TASK-030
precedent) — no `Canonical.FormatVersion` bump from this case alone.

### Decision E — executor: hold position, pin the named contact as this tick's combat candidate — CONFIRMED

A new branch in `commitmentAndLocalAction` (or wherever the executor reads
`Commitment`): while `Suppressing`, the agent does not move (no
`Destination`, no `Pathfinding`). In the Combat phase, if the target
`AgentId` is currently in the suppressing agent's own `VisibleContacts`, it
is used directly as `Combat.chooseTarget`'s chosen candidate for that
shooter (bypassing nearest-candidate selection) rather than being one
candidate among several. If the target is not currently visible, the agent
holds position and does not engage anything else this tick — a deliberate
Suppress order should not silently retarget onto whatever else wanders into
view. `Combat.fs`'s hit-chance and `Suppression.gain` are otherwise
unchanged; a `Suppress` order's entire effect on the target is routed
through the existing, already-canonical `ShotFired` -> `Suppression.gain`
pipeline (TASK-031/032), not a new bookkeeping field.

### Decision F — `Appraisal.routeExposure` reduces a threat's contribution while that threat's own `SuppressionBand` is latched — CONFIRMED

`routeExposure` (and its private `cellPressure`) currently take only
`Contact[]`, with no visibility into a threat's live combat state. This
task extends the call site (`Simulation.appraisal`) to pass each threat's
current `AgentState.SuppressionBand` (looked up from `s.Agents` by
`Contact.Contact`, the `HostileTacticalKnowledge` reverse-lookup TASK-034
already does symmetrically) alongside the `Contact[]`, and `cellPressure`
returns `0` for a threat whose `SuppressionBand` is `true`, unconditionally
— not merely reduced. This reuses `AgentState.SuppressionBand`
(TASK-033/034, already computed identically and canonically for both
sides) rather than inventing new "who is suppressing whom" state: the
reduction is a pure function of the target's own already-canonical
suppression state, regardless of whether that suppression came from a
`Suppress` order or incidental automatic engagement (TASK-031's symmetric
auto-engage). A `Suppress` order's job is only to make sustained qualifying
fire on the named contact more reliable than incidental engagement would.

### Decision G — new reappraisal trigger: a known threat's `SuppressionBand` flip — CONFIRMED

TASK-033's two existing triggers (knowledge-change, suppression-band) both
key off the *appraising agent's own* state. Neither fires when a *different*
agent's fire suppresses the threat a first agent's `Refused` order names.
This task adds a third, symmetric trigger inside the same
`Simulation.appraisal` slot: any hostile contact's `SuppressionBand` flipping
(either direction) this tick resets every non-fulfilled `Refused`/`Unable`
order's `Disposition` to `None`, global scope — the exact precedent
`docs/05` section 14's knowledge-change trigger already set. This is the
mechanism that actually realises `docs/07` section 8 step 6 ("tactical
knowledge and exposure are recalculated").

### Decision H — new corpus entry proving the mechanism end-to-end — CONFIRMED

A new entry (working name `suppress-relieves-exposure`) with two friendlies
and one hostile: friendly A is `Refused RouteTooExposed` against the known
hostile (the `exposed-approach` precedent); friendly B is given a `Suppress`
order against that same hostile, achieves sustained hits, drives the
hostile's `Suppression` into its `SuppressionBand`; friendly A's order
resets to unappraised (Decision G) and reappraises `Accepted` (Decision F)
the same or a following tick. This is the direct evidence `docs/07` section
9 criterion 4 ("a player action can predictably change an appraisal
outcome") and the prerequisite proof for B-023.

### Decision I — `Canonical.FormatVersion` bump — CONFIRMED

`PlayerIntent` gains a new case inside already-canonical `AgentState.Order`
(`Canonical.fs`'s `MoveTo target -> ...` match is exhaustive), so this is a
genuine format bump with a full re-pin — not behaviour-neutral for the new
corpus entry (gains real new `Suppress`-order and `Suppression` state from
the tick suppression fire starts), behaviour-neutral for every existing
entry (none issues a `Suppress` order). No new event type — `OrderAppraised`
and `ShotFired` already cover every state change here.

### Decision J — B-021 explicit descope: `review` -> `done` (reduced scope) — CONFIRMED

Per B-021's own row wording. Remaining open items — exposure-band (needs
per-tick route-exposure tracking for an idle agent with a live order, a
materially larger cut per TASK-033's own note), wounded/support/leadership
triggers (need B-031 systems that don't exist), and dynamic trust (`docs/05`
section 8 leaves it "minimal" for the vertical slice by design) — are
untouched by this task and stay recorded as follow-ups, not silently
dropped.

### Decision K — B-022 explicit descope: `review` -> `done` (reduced scope) — CONFIRMED

Per B-022's own row wording. Remaining open items — suppress-likely-routes,
seek-adjacent-cover-under-pressure, and scripted fall-back — are all
**Hostile-side** doctrine (the enemy proactively choosing to suppress or
fall back without being ordered), not the **player-issued** `Suppress`
order this task builds. None are touched by this task; they stay recorded
as follow-ups needing either a `Suppress`-order-equivalent for Hostile AI or
the first autonomous-movement decision in the codebase (`docs/05` section
12, TASK-034's "Considered and rejected" note).

## Allowed scope

- `src/CommandoWar.Sim/Domain.fs` (`PlayerIntent.Suppress`,
  `DecisionReason.TargetNotKnown`, module doc updates).
- `src/CommandoWar.Sim/Appraisal.fs` (`routeExposure`/`cellPressure`
  suppression-aware signature, `appraise` for `Suppress` orders).
- `src/CommandoWar.Sim/Commitment.fs` (`Suppressing of SuppressCommitment`,
  `Commitment.ofAgent`).
- `src/CommandoWar.Sim/Combat.fs` / `Simulation.fs` (pinned-candidate combat
  branch for a `Suppressing` agent; the new reappraisal trigger inside
  `Simulation.appraisal`).
- `src/CommandoWar.Sim/Canonical.fs` (`FormatVersion` bump, new
  `PlayerIntent`/`Commitment` encode arms).
- `src/CommandoWar.Sim/Corpus.fs` (new `suppress-relieves-exposure` entry).
- `src/CommandoWar.Headless/{DiagnosticRender.fs, AppraisalDemo.fs}`
  (exhaustive-match arms for any new `Overlay`/`Commitment` case touched).
- `tests/CommandoWar.Sim.Tests/*.fs` (new facts; full re-pin ripple).
- `content/replays/*`, `content/diagnostics/*` (full re-pin; one new golden).
- `src/CommandoWar.Client.Godot/{README.md, src/AppraisalDemoScene.cs}`
  (pinned `--selfcheck` hash, if `exposed-approach` tick 1 moves again).
- `docs/04`, `docs/05`, `docs/07`, `docs/09`, `docs/11`, `docs/12`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- No `Assault` or `Withdraw` `PlayerIntent` case (the rest of B-030 — L-sized
  on its own, not this task's thin slice).
- No ammunition, reload, or cooldown model for sustained suppress fire
  (separate future task per `docs/11` B-019's row).
- No Hostile-side autonomous suppress-likely-routes, seek-cover, or
  fall-back behaviour (B-022's remaining items — explicitly descoped, not
  built, by Decision K).
- No `Delayed`/`ResumeCondition` disposition, no stage-5 `Adapted` case
  (B-018 follow-up, untouched).
- No wound/casualty/leadership-succession model (B-031).
- Nothing under `src/_scratch`, `bench/`, `content/benchmarks/BASELINE.md`,
  or any Godot spike scene.

## Considered and rejected

**Reducing exposure proportionally to `Suppression` value instead of a hard
zero-at-`SuppressionBand` cutoff**: rejected for this thin slice — a
continuous mapping needs a new tuning curve and a new "how much" design
question with no `docs/05` guidance behind it, where the hysteresis-latch
cutoff reuses an already-tuned, already-canonical boolean with zero new
config surface. A continuous curve can be a follow-up once the binary
version is proven in the corpus.

**Giving `Suppress` its own `AgentState.SuppressedTargets: AgentId[]`
bookkeeping** (tracking who is suppressing whom) instead of keying off the
target's own `SuppressionBand`: rejected — the target's `SuppressionBand`
is already exactly "is this agent currently suppressed," computed
identically for both sides since TASK-033/034; a second bookkeeping
structure would duplicate state that can drift, the same reasoning
`Commitment.fs`'s own doc comment already gives for keeping `Commitment`
derived rather than stored.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0` before and after.
- `dotnet test CommandoWar.slnx -c Release`: full re-pin ripple; new
  `SimulationTests` facts for Decisions F/G (threat suppression zeroes route
  pressure; a threat's `SuppressionBand` flip reappraises a different
  agent's `Refused` order), `Commitment`/`Combat` facts for Decisions D/E,
  one `CanonicalHashTests` fact for the format bump.
- `cwheadless corpus`: full count/`N`, `--regenerate` twice byte-identical.
- `cwheadless fixture`: format bump reflected, event count unchanged
  (fixture is enemy-free).
- `cwheadless replay-file content/replays/envelope-full.cwreplay`: unchanged
  (enemy-free scenario).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`: `FSharp.Core` only.
- Source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`: clean.
- `git status --porcelain`: matches this task file's "Allowed scope".

## Documentation updates

- This task file's Outcome/Review sections.
- `docs/11_BACKLOG.md`: this task's row; B-021 and B-022 rows moved to
  `done` (reduced scope) with the descope reasoning inline; B-023's row
  updated to record its dependency list as fully `done` and its remaining
  gap narrowed to "steps 4, 6 (partially), 8: the full corpus scenario
  itself."
- `docs/07_VERTICAL_SLICE.md` section 8: record step 5-6 as realised by this
  task (mechanism only — the full sequence is still B-023).
- `docs/05_COMMAND_AND_AGENT_AI.md` sections 4, 7, 9, 14, 16 (the "Exposed
  road" scenario's "neither exists yet" line is now half-realised).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Every change is additive to existing exhaustive matches (`Canonical.encode`,
`Commitment.ofAgent`, the executor/combat phases) plus one new corpus entry
— revertible with `git revert` in one step; no migration of existing
authored content, no change to any existing corpus entry's authored intent
(only its re-pinned hash/tick trace).

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-16). Godot's `--selfcheck` pinned hash
  (`0x5D5A30C0DF64AC93`) still needs independent confirmation on Dave's
  machine (no Godot install in the implementing environment) — flagged, not
  a blocker for acceptance. Merged to `main` (`--no-ff`, branch
  `task-037-suppress-order-and-dependency-closeout` deleted; not pushed).
