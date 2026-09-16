# TASK-038: Canonical refusal-and-correction scenario end to end

Status: done (drafted 2026-09-16; central decision confirmed with Dave via
AskUserQuestion before the phase bodies; implemented and self-verified
2026-09-16; accepted by Dave 2026-09-16)
Owner: Dave
Phase: P3
Gate: G3 (command loop) — this is G3's headline evidence item
(`docs/07_VERTICAL_SLICE.md` section 8; PROJECT_STATE.yaml note 2026-09-16)
Size: M

## Outcome (2026-09-16)

Implemented as drafted, one deviation found during implementation (see
below). New `canonicalRefusalAndCorrectionSpec` in
`src/CommandoWar.Headless/Corpus.fs`: the `suppress-relieves-exposure`
geometry exactly, plus one more order reissuing agent 0's identical `(11,3)`
target on tick 8. Traced against the real seeded run first: `Refused
RouteTooExposed threat-agent-2` ticks 1-3; the automatic
threat-suppression-change reappraisal `Accepted`s it at tick 4; the tick-8
reissue produces its own fresh `Accepted` + `CommitmentEstablished` for the
same target; disposition stays `Accepted` (or unappraised, on arrival)
through one extra reappraisal blip at tick 12 and arrival at tick 13. Three
new `DiagnosticsTests` facts (steps 1-4, 5-6, 7-8) plus one golden ASCII/SVG
pair at tick 8. No `CommandoWar.Sim` change, no `Canonical.FormatVersion`
bump — every mechanism already existed (TASK-028/030/033/037).

**Deviation**: the drafted task file treated a new golden diagnostic pair as
required scope. On inspection, `AGENTS.md`'s diagnostics rule only mandates
one when a task adds or changes authoritative spatial/tactical state — this
task adds none (no new `Overlay` case, the identical situation TASK-037 was
in, which shipped with no dedicated golden at all). Added one anyway since
B-023 is G3's headline evidence item and the other two headline entries
(`exposed-approach`, `reissued-order`) both have one — a legibility choice,
not a correctness requirement.

`293 -> 297` green (+4: three `DiagnosticsTests` facts, one `CorpusTests`
theory case). `dotnet build` 0/0. `-- corpus` 14/14 (`--regenerate` twice
byte-identical — every pre-existing entry's table unchanged). `-- fixture`
format 8, 36 events unchanged. `dotnet list` `FSharp.Core` only. Source scan
clean. `git status` matches this task's scope plus the pre-existing
unrelated `project.godot` edit, left untouched.

`docs/07` section 8, `docs/11` B-023 row (`proposed -> done`),
`content/diagnostics/README.md`, `docs/12`, and `PROJECT_STATE.yaml` updated.
No Godot re-verification needed — this task moved no corpus-entry tick-1
hash (unlike TASK-034/037).

Full detail: `docs/ledger/2026-09-16-TASK-038-canonical-refusal-and-correction.md`.

## Objective

Script `docs/07_VERTICAL_SLICE.md` section 8's full 8-step canonical refusal
sequence as one continuous corpus entry, close the two remaining gaps
(step 4's explanation-surface evidence, step 8's re-acceptance check), and
move backlog row B-023 to `done`.

## Why this task exists

B-023's four dependencies (B-018, B-020, B-021, B-022) are all `done` as of
TASK-037 (2026-09-16), so per `docs/11_BACKLOG.md` section 7 ("all
dependencies are `done`") this row is now selectable. Steps 1–3 and 5–6
already exist piecemeal (`exposed-approach`, `suppress-relieves-exposure`),
step 7 exists in isolation (`reissued-order`, a different geometry) — but
nothing chains them into one scenario, and step 4 (the explanation surface)
and step 8 (the re-acceptance check) have not been touched by any task.

## Central decision (confirmed with Dave 2026-09-16 before drafting)

`docs/07` step 8 says the soldier "accepts **or adapts** it consistently."
There is no `Adapted` `OrderDisposition` case anywhere in the codebase —
`Appraisal.fs`'s own doc comment: "Stage 5 (safer adaptation) is deferred
(B-018): no `Adapted` outcome, no route recomputation." Building stage-5
`Adapted` is a separate, materially larger, unscoped feature, not something
to add inside this M task.

**Confirmed**: this task proves the accepts-branch only — the reissued order
is `Accepted` consistently (once via TASK-037's automatic
threat-suppression-change reappraisal trigger, once more via an explicit
player reissue) — and B-023 goes to `done` on that reduced interpretation.
The `Adapted`/stage-5 half is recorded as a new, not-yet-filed follow-up, not
silently dropped.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `docs/04_SIMULATION_SPEC.md` sections 12.5 (Appraisal), 12.6 (Commitment),
  14 (reappraisal triggers)
- `docs/05_COMMAND_AND_AGENT_AI.md` sections 5–7 (stages, `DecisionReason`
  vocabulary), 14 (reappraisal triggers), 16 ("Exposed road")
- `docs/07_VERTICAL_SLICE.md` sections 8–9
- `docs/11_BACKLOG.md` row B-023
- `tasks/TASK-028-ORDER-APPRAISAL-AND-TYPED-REASONS.md`,
  `TASK-030-COMMITMENT-AND-FINITE-EXECUTOR.md`,
  `TASK-037-SUPPRESS-ORDER-AND-DEPENDENCY-CLOSEOUT.md`
- `src/CommandoWar.Headless/Corpus.fs` (`exposedApproachSpec`,
  `suppressRelievesExposureSpec`, `reissuedOrderSpec`, the `ScenarioSpec`
  builder)
- `src/CommandoWar.Headless/DiagnosticRender.fs` (`reasonText`,
  `dispositionText`)
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs` (the `exposed-approach`,
  `suppress-relieves-exposure`, and "second order delivered mid-route"
  facts)
- `content/diagnostics/README.md`

## Dependencies

- B-018, B-020, B-021, B-022 — all `done`. No other task selected.

## Inputs and assumptions

- New corpus entry reuses the `suppress-relieves-exposure` geometry exactly
  (12 x 8, friendly 0 Discipline 1 at (1,3) -> (11,3), friendly 1 at (10,1)
  suppressing hostile 2 at (10,4)) rather than a fresh layout — the two
  scenarios are the same mechanism at different scopes, and duplicating a
  proven geometry avoids a second independent layout to reason about.
- The reissue (step 7) targets the identical original destination (11,3) —
  "the player reissues the original intent" is read literally, not as a
  different corrective route. It is delivered on a tick after friendly 0's
  automatic reappraisal (TASK-037's threat-suppression-change trigger) has
  already produced `Accepted` and a `CommitmentEstablished`, so the reissue
  tests idempotent stability of a repeated player action, not the
  suppression mechanism itself (already proven by `suppress-relieves-
  exposure`). Exact tick chosen after inspecting the real seeded run (the
  worst-case bound in the existing entry's doc comment is tick 6; TASK-037's
  own outcome measured the real flip at tick 4).
- Step 4 needs no new production code: `OrderAppraised` only ever carries a
  structured `OrderDisposition`/`DecisionReason` (`Domain.fs`) — no event or
  overlay in the codebase ever emits a raw numeric exposure/threshold value —
  and `DiagnosticRender.reasonText`/`dispositionText` already render it as
  named text. This task adds one explicit test assertion against the new
  entry's refusal frame turning that implicit type-system guarantee into
  checked evidence for the step 4 criterion.

## Allowed scope

- `src/CommandoWar.Headless/Corpus.fs` (new corpus entry, working name
  `canonical-refusal-and-correction`).
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs` (new facts: step 4's
  structured-reason assertion; step 8's second `Accepted` /
  `CommitmentEstablished` after reissue).
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`,
  `content/diagnostics/{canonical-refusal-and-correction-tick-NNN.ascii.txt,
  .svg, README.md}` (one new golden pair at the reissue tick, the
  `exposed-approach`/`reissued-order` precedent).
- `content/replays/canonical-refusal-and-correction.{md,cwreplay}` (generated
  by `cwheadless corpus --regenerate`, committed).
- `docs/07_VERTICAL_SLICE.md` section 8 (record the full sequence as
  realised; note the accepts-only scope of step 8).
- `docs/11_BACKLOG.md` (B-023 row to `done`; note the deferred `Adapted`
  follow-up).
- `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Forbidden scope

- No `Adapted`/stage-5 disposition case, no route recomputation (B-018
  follow-up, explicitly deferred by the central decision above).
- No change to `Appraisal.fs`, `Combat.fs`, `Commitment.fs`, `Domain.fs`, or
  `Canonical.fs` — every mechanism this task exercises already exists
  (TASK-028/030/033/037). No `Canonical.FormatVersion` bump.
- No change to `exposed-approach`, `suppress-relieves-exposure`, or
  `reissued-order` — they stay as independent proofs of each sub-mechanism;
  this task adds a new entry, it does not replace or remove any existing one.
- No Godot/`content/diagnostics/demo.html` change (separate, already-flagged
  UI polish item, not this task).

## Required work

1. Inspect the real per-tick trace of `suppress-relieves-exposure`'s geometry
   (via `cwheadless corpus` or a scratch run) to find the actual tick
   friendly 0's `Refused` order reappraises `Accepted` under seed 20260904.
2. Add `canonicalRefusalAndCorrectionSpec` to `Corpus.fs`: the same
   geometry/orders as `suppress-relieves-exposure`, plus one more
   `ScenarioOrder` reissuing friendly 0's original `(11,3)` target on a tick
   safely after the automatic reappraisal's `CommitmentEstablished`. Add the
   `Entry` to `Corpus.all` with a description tracing all 8 steps.
3. Add the golden diagnostic pair at the reissue tick (the
   `exposedApproachFrames()` precedent), update
   `content/diagnostics/README.md`.
4. Add `SimulationTests` facts: the tick-1 `Refused RouteTooExposed
   threat-agent-2` disposition carries the structured `DecisionReason` (step
   4 evidence); the automatic reappraisal's `Accepted` +
   `CommitmentEstablished` (steps 5–6, already proven by
   `suppress-relieves-exposure` — extend/reuse, do not re-derive); the
   reissue's own fresh `Accepted` + `CommitmentEstablished` with no event for
   the superseded commitment (steps 7–8, the "second order mid-route"
   precedent).
5. Run `cwheadless corpus --regenerate` twice, diff for byte-identity;
   commit the generated `.md`/`.cwreplay`.
6. Update `docs/07` section 8, `docs/11` B-023 row, `docs/12`, and
   `PROJECT_STATE.yaml`.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [ ] One corpus entry demonstrates all 8 steps of `docs/07` section 8 in a
      single continuous run.
- [ ] Step 4: a test asserts the refusal's `DecisionReason` is the structured
      `RouteTooExposed(Some threat)` case, not a raw score.
- [ ] Step 8: a test asserts the reissued order is `Accepted` with a fresh
      `CommitmentEstablished`, consistent with (not contradicting) the
      automatic reappraisal's earlier `Accepted`.
- [ ] No `Canonical.FormatVersion` bump; no existing corpus entry's
      behaviour changes.
- [ ] `docs/11` row B-023 moved to `done`, with the `Adapted`/stage-5
      deferral recorded explicitly, not silently dropped.
- [ ] Required documentation updated (task file, backlog, progress ledger,
      `PROJECT_STATE.yaml`).

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0` before and after.
- `dotnet test CommandoWar.slnx -c Release`: full suite green, including the
  new facts.
- `cwheadless corpus`: full count/N, `--regenerate` twice byte-identical.
- `cwheadless fixture`: unchanged (no format bump, fixture is unaffected).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`: `FSharp.Core` only.
- `git status --porcelain`: matches this task file's "Allowed scope".

## Evidence to capture

- Exact tick numbers for: tick-1 refusal, the automatic reappraisal's
  `Accepted`, the reissue tick, the reissue's `Accepted`.
- Test run summary (pass count before/after).
- Corpus count and regenerate diff result.

## Expected files

- `src/CommandoWar.Headless/Corpus.fs`
- `tests/CommandoWar.Sim.Tests/{SimulationTests.fs, DiagnosticsTests.fs}`
- `content/replays/canonical-refusal-and-correction.{md,cwreplay}`
- `content/diagnostics/{canonical-refusal-and-correction-tick-NNN.ascii.txt,
  .svg, README.md}`
- `docs/07_VERTICAL_SLICE.md`, `docs/11_BACKLOG.md`,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`

## Documentation updates

- This task file's Outcome/Review sections.
- `docs/11_BACKLOG.md`: B-023 row to `done`.
- `docs/07_VERTICAL_SLICE.md` section 8: record the full sequence realised
  end to end, with the accepts-only scope of step 8 noted.
- `docs/12_PROGRESS_LEDGER.md`: new index row + `docs/ledger/` detail file.
- `PROJECT_STATE.yaml`.

## Rollback or removal

Purely additive: one new corpus entry, one new golden diagnostic pair, new
test facts. No existing entry, event, or type changes. Revertible with
`git revert` in one step.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-16). Implemented directly on `main`, no branch (the
  TASK-035/036 low-risk precedent — no `CommandoWar.Sim` change, purely
  additive, fully self-verified).
