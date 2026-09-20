# TASK-067: Formation redirect applies only to group orders, not solo clicks

Status: done (implemented and self-verified 2026-09-20; accepted by Dave 2026-09-20 on the self-verification evidence)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises the sim-side half of B-067

## Objective

A `MoveTo` order for a formationed agent (non-`None` `AgentState.FormationOffset`,
TASK-059) is redirected through `Appraisal.resolveFormationTarget` today
regardless of whether the order was issued to that one agent alone or to
several agents at once. Make the redirect conditional on the order itself
being a genuine multi-recipient (group) order: a solo order always resolves
to the literal clicked cell; a multi-recipient order still resolves each
recipient's own slot exactly as today.

## Why this task exists

Raised by Dave in the same conversation as B-066 (2026-09-20): "the
formation movement is weird, it just seem like you cant move where you
want to... when moving a single agent you just want them to go where you
click." Scoped as backlog B-067 (`docs/11_BACKLOG.md`), which named two
separable halves: (1) formation should key off order provenance (solo vs.
group), not an agent's static authored membership; (2) a new multi-select
UI and joint-order dispatch so a player can actually address several
agents with one order. Confirmed via `AskUserQuestion` (2026-09-20) before
drafting: this task ships half (1) alone — a self-contained, low-risk sim
fix with an immediate, observable payoff (solo clicks stop getting
redirected) that does not require any new client UI to land. Half (2)
(multi-select UI, drag-select + shift-click, joint dispatch) is deferred
to a follow-up task once this one is accepted, the B-030/TASK-037-then-
TASK-047 and B-059/TASK-048 precedent of shipping the sim-side mechanism
before the client control surface.

Investigation before drafting (two parallel research passes) confirmed the
mechanism already half-exists: `PlayerCommand.Recipients: AgentId list`
and `Command.moveToMany` have addressed multiple agents with one command
since TASK-020, but every recipient is appraised, committed, and executed
completely independently (`Simulation.fs`'s `commandIntake`/`appraisal`/
`commitmentAndLocalAction`, one agent at a time) — `Appraisal.appraise`
reads each recipient's own static `AgentState.FormationOffset`
unconditionally whenever the order is `MoveTo`, so a "squad advance" today
is just N independently-redirected solo orders, and a genuine solo click
addressed to one agent redirects identically. The fix does not need a new
command shape, a new `Commitment` case, or a new UI: it needs the already-
known recipient count of the *originating command*, captured once at
intake time onto the per-agent `ReceivedOrder` the rest of the pipeline
already reads.

## Central decision (confirmed with Dave 2026-09-20 via `AskUserQuestion`)

**Group-ness is a property of the order, derived from `Recipients.Length
> 1` at command-intake time — not a per-agent static fact.** A new
`ReceivedOrder.AsGroup: bool` is set once in `Simulation.commandIntake`
where `ReceivedOrder` is already constructed per (command, recipient) pair
(`recipients.Length > 1`, the same `recipients` list already in scope from
the command's own validated `Recipients`). Every `resolveFormationTarget`
call site downstream reads `order.AsGroup` instead of assuming
"formationed agent implies redirect." No new `PlayerIntent` case, no new
`Command.*` builder — `Command.moveToMany` (existing since TASK-020)
already produces a multi-recipient command; this task only changes what
happens once such a command reaches appraisal.

**Formation source stays the agent's own authored `FormationOffset`,
unconditionally, for a group order** — this task does not touch how an
individual agent's slot is computed, only whether the computation runs at
all. (The separate question of an *ad-hoc* formation for a selection that
does not correspond to one authored fireteam belongs to the follow-up
multi-select UI task, confirmed with Dave: authored offsets only, for now
— an agent with no `FormationOffset` in a group order simply targets the
literal anchor, today's existing `None -> anchor` fallback, unchanged.)

## Required reading

- `docs/04_SIMULATION_SPEC.md` section on formation-slot resolution
  (`resolveFormationTarget`'s doc paragraph, confirms today's "runs
  unconditionally for a `MoveTo` order" behaviour this task narrows).
- `docs/11_BACKLOG.md` B-067 row (the two-halves scoping rationale) and
  B-011d/TASK-059's own row (the original, unconditional design this task
  narrows, not relitigates).
- `tasks/TASK-059-FORMATION-SLOTS.md` — Central decisions: "no new order
  type, no multi-agent order dispatch... a formation changes how *that one
  agent's own* `MoveTo` target resolves" (confirms this task is additive
  narrowing, not a reversal) and the `FormationSlotSearchRadius`/
  `bestCoverNear` fallback precedent this task does not change.
- `tasks/TASK-020-*.md` (or the relevant history) for `PlayerCommand.
  Recipients`/`Command.moveToMany` — confirm its existing multi-recipient
  semantics (broadcast of one intent to N independently-appraised agents)
  before assuming this task's `AsGroup` derivation is the only consumer of
  `Recipients.Length`.
- `src/CommandoWar.Sim/Domain.fs`: `ReceivedOrder` (the record this task
  extends), `AgentState.FormationOffset`, `PlayerCommand`/`Command`
  (`Recipients: AgentId list`).
- `src/CommandoWar.Sim/Simulation.fs`: `commandIntake` (where `ReceivedOrder`
  values are constructed per recipient, `recipients` already in scope),
  the `appraisal` phase's fulfilled fast-path and `Destination`-write match
  (both call `Appraisal.resolveFormationTarget` a second time, mirroring
  `appraise`'s own call — all three must move together), `communication`
  (confirms `ReceivedOrder` flows unchanged into `AgentState.Order` and
  `AgentState.PendingDelivery` via `PendingOrder`/`PendingBody`, so no
  other write site needs touching).
- `src/CommandoWar.Sim/Appraisal.fs`: `appraise`'s `MoveTo` branch (the
  authoritative gate) and `resolveFormationTarget` itself (unchanged by
  this task).
- `src/CommandoWar.Sim/Diagnostics.fs`: `formationSlotOverlays` (the
  read-only diagnostic mirror; must stay consistent with the new gating or
  the overlay will show a redirect appraisal no longer performs).
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: `OnHover`'s
  client-side formation-preview call to `resolveFormationTarget` (added by
  TASK-064 review round 1) — the one non-sim caller, must mirror the same
  gating so the hover preview never shows a redirect the order won't
  actually receive.
- `src/CommandoWar.Sim/Canonical.fs`: `writeOrder`, the `RecentlyWounded`/
  `RadioDestroyed` `w.U8(if flag then 1uy else 0uy)` idiom for a new
  canonical boolean field, and how a prior `FormatVersion` bump documented
  its behaviour-neutrality for every pre-existing corpus entry.

## Dependencies

- B-011d (TASK-059, done — the mechanism this task narrows). B-016b/B-058
  (TASK-058, done — confirms `ReceivedOrder`/`PendingDelivery` flow
  unchanged through delayed delivery). No other task selected.

## Inputs and assumptions

- `ReceivedOrder.AsGroup` is genuine canonical per-tick state (it changes
  a deterministic outcome — whether formation resolution runs) — the
  `Ammo`/`RecentlyWounded` precedent, not a derived/excluded field.
  `Canonical.FormatVersion` bump required (14 -> 15).
- `AsGroup` is computed once, at command-intake time, from the *validated*
  `recipients` list already in scope in `commandIntake` (post duplicate-
  recipient and bounds checks) — not recomputed anywhere downstream, the
  `IssuedAtTick`/`Urgency` "captured once, carried by the record" pattern
  every other `ReceivedOrder` field already follows.
- Only the `MoveTo` intent is affected, matching `resolveFormationTarget`'s
  existing scope (`Hold`/`Assault`/`Withdraw`/`Suppress` never read
  `FormationOffset` today and must not start).
- A single-recipient command (`Recipients.Length = 1`) always sets
  `AsGroup = false`, including every existing scripted self-check or test
  fixture that issues one command per agent to simulate a "squad move" —
  those become genuinely un-redirected solo orders once this lands, which
  is the intended, observable behavioural change this task exists to make
  (not a regression to paper over).
- A multi-recipient command (`Recipients.Length > 1`) always sets
  `AsGroup = true` for every one of its recipients, with no partial/mixed
  state possible (the command is one atomic intake decision, TASK-020's
  own all-or-nothing-per-recipient validation).
- No new UI, no new `Command.*` builder, no new `PlayerIntent` case, no
  new `Commitment` case (nothing behaviourally distinguishes a group-
  sourced `MoveTo` from a solo one once its target is already resolved —
  the TASK-047 "Decision C: no new case without a real behavioural
  difference" precedent).

## Allowed scope

- `src/CommandoWar.Sim/Domain.fs`: `ReceivedOrder.AsGroup: bool` field.
- `src/CommandoWar.Sim/Canonical.fs`: `FormatVersion` bump, `writeOrder`
  extended with the new boolean.
- `src/CommandoWar.Sim/Simulation.fs`: `commandIntake`'s `ReceivedOrder`
  construction (set `AsGroup`); the `appraisal` phase's fulfilled
  fast-path and `Destination`-write match (both gate their own
  `Appraisal.resolveFormationTarget` call on `order.AsGroup`/`o.AsGroup`).
- `src/CommandoWar.Sim/Appraisal.fs`: `appraise`'s `MoveTo` branch gated on
  `order.AsGroup`.
- `src/CommandoWar.Sim/Diagnostics.fs`: `formationSlotOverlays`'s match
  guard extended to require `AsGroup = true` (a solo order no longer emits
  an `AgentFormationSlot` overlay, since no redirect happens for it to
  show).
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: `OnHover`'s
  formation-preview call gated the same way (needs the hovered/selected
  agent's own pending command's recipient count — confirm during
  implementation whether the existing single-`selected: AgentId option`
  click path can only ever produce `AsGroup = false` previews until the
  follow-up multi-select task lands, which is expected and correct: no
  client code issues a multi-recipient command yet).
- New `SimulationTests`/`AppraisalTests` facts: a single-recipient `MoveTo`
  for a formationed agent resolves to the literal target (no redirect); a
  multi-recipient `MoveTo` addressing the same agent still redirects
  through its slot offset exactly as before.
- A corpus entry or extended existing entry proving both cases end to end,
  if compact; a `SimulationTests`-only proof is acceptable otherwise (the
  TASK-065 precedent for "tests suffice when a corpus entry would not add
  new signal").
- `docs/04_SIMULATION_SPEC.md` (the formation-resolution paragraph,
  updated to describe the new gating), `docs/11_BACKLOG.md`,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Forbidden scope

- No multi-select UI, no drag-select/shift-click input handling, no new
  `IClientScene` member for a selection set — that is the deferred
  follow-up task (B-067's second half).
- No ad-hoc/computed formation for an arbitrary agent selection — group
  orders keep using each agent's own authored `FormationOffset` only, per
  Dave's confirmed choice.
- No change to `Pathfinding.fs`, `Commitment.fs`, or any non-`MoveTo`
  intent.
- No change to `PlayerCommand`/`Command.moveToMany`'s existing shape or
  validation rules (`Recipients` bounds/duplicate checks) — this task only
  reads the already-validated recipient count, it does not change intake
  validation.

## Required work

1. Confirm (by inspection, not assumption) every place `ReceivedOrder` is
   constructed and consumed, to ensure `AsGroup` propagates correctly
   through `PendingOrder`/`PendingDelivery` (TASK-058's delayed-delivery
   path) with no separate write site missed.
2. Add `ReceivedOrder.AsGroup: bool`; bump `Canonical.FormatVersion`;
   extend `writeOrder`.
3. Set `AsGroup` in `commandIntake` from the validated recipient count.
4. Gate `Appraisal.appraise`'s `MoveTo` branch, `Simulation.fs`'s two
   mirrored `resolveFormationTarget` calls, `Diagnostics.
   formationSlotOverlays`, and `CommandDemoScene.fs`'s hover-preview call
   all on `AsGroup`.
5. Tests: the two facts above at minimum, covering both `AsGroup = false`
   (redirect suppressed) and `AsGroup = true` (redirect unchanged).
6. Verify: `dotnet build` all `.slnx`; `dotnet test`; `cwheadless corpus`
   (re-pin expected from the `FormatVersion` bump; check by inspection
   whether any existing corpus entry issues a multi-recipient command
   before assuming every entry's *behaviour* stays byte-identical); Godot
   client build and all three scenes' `--selfcheck` (the six-agent
   Bridgehead scripted sequence issues six single-recipient commands
   today — confirm whether its hashes move, since those orders will now
   correctly stop redirecting).
7. Update documentation per Documentation updates below.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] A test proves a single-recipient `MoveTo` for a formationed agent
      (`FormationOffset = Some _`) resolves to the literal clicked cell,
      not the slot-redirected cell. New fact: `` `a solo MoveTo order for
      a formationed agent goes to the literal clicked cell, not its slot
      offset` ``.
- [x] A test proves a multi-recipient `MoveTo` (`Command.moveToMany`)
      addressing the same formationed agent still redirects through its
      slot offset exactly as before this task. Already covered by the
      pre-existing fact `` `two formationed agents ordered to the same
      nominal cell resolve to distinct destinations` `` (TASK-059) --
      confirmed still passing unchanged, since it already used
      `Command.moveToMany` with two recipients (`AsGroup = true`).
- [x] No `Pathfinding.fs`, `Commitment.fs`, or non-`MoveTo`-intent change.
      Confirmed by inspection and `git diff` against Allowed scope.
- [x] `dotnet test`/`corpus` pass with the expected re-pin from the
      `FormatVersion` bump (14 -> 15), and any genuine behaviour change to
      an existing corpus/diagnostics/`--selfcheck` entry is identified and
      explained, not silently absorbed. Two real, genuine (not
      byte-layout-only) behaviour changes were found and handled, not
      smoothed over -- see Evidence to capture below.
- [x] Required documentation updated (see Documentation updates below).

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0` Warning(s), `0` Error(s).
- `dotnet test CommandoWar.slnx -c Release`: `415/415` passed (+1: the new
  solo-order fact; the pre-existing group-order fact needed no change).
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus
  --regenerate` then `-- corpus`: `19/19` entries match (all 18
  pre-existing entries re-pinned byte-layout only; `formation-slots` was
  re-authored to issue one genuine `Command.moveToMany` instead of two
  coincidentally-same-tick solo orders, reproducing its own prior
  tick-by-tick outcome exactly -- domain event count (25) and tick count
  (12) both unchanged).
- `dotnet run --project src/CommandoWar.Headless -c Release --
  replay-file content/replays/envelope-full.cwreplay`: `checkpoints : OK
  (24 ticks match the file's committed hashes)` after re-pinning (this
  entry's one command already addresses three recipients via
  `Command.moveToMany`, `AsGroup = true`, but none of them authors a
  `FormationOffset`, so behaviour is unchanged -- byte-layout-only re-pin).
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
  -c Debug`: `0` Warning(s), `0` Error(s).
- `--selfcheck` through the real Godot 4.7.2 editor
  (`Godot_v4.7.2-stable_mono_win64_console.exe --headless`):
  `SnapshotDemo.tscn` `MATCH 0x6213D672BC36FDB8` at tick 20 (byte-layout
  only, DemoScenario authors no formation); `AppraisalDemo.tscn` `MATCH
  0xA1354EB998FC1B95` (byte-layout only, exposed-approach authors no
  formation); `CommandDemo.tscn` `MATCH 0x2629A1FE165F94BB` at tick 90 --
  a genuine behaviour change, not byte-layout only (see Evidence to
  capture below), re-pinned after updating the scripted sequence's own
  target cells and confirming the outcome is unchanged.

## Evidence to capture

- command output above;
- **two real, genuine behaviour changes found and handled, not smoothed
  over:**
  1. The `formation-slots` corpus entry (the only committed content
     exercising formation redirect) issued two single-recipient orders
     that happened to share a tick and target -- exactly the pattern this
     task makes stop redirecting. Re-authored to issue one genuine
     `Command.moveToMany` order instead (`Corpus.fs`'s new
     `MoveOrderGroup` scenario-intent case), so the entry keeps
     demonstrating real slot resolution; its own tick-by-tick outcome
     (12 ticks, 25 events) is unchanged, confirmed by diff.
  2. `CommandDemoDrive.runScriptedSelfCheck`'s six-agent Bridgehead
     sequence relied on the old unconditional redirect: agents 0/1/5 all
     had literal targets that used to resolve to distinct real cells via
     formation offset (similarly 2/4). Re-running the sequence unchanged
     under this task's fix produced a genuine friendly casualty (agent 4
     died converging on a now-undeflected shared cell) -- a real, worse
     outcome, not a hash artefact, confirmed via a temporary `dotnet fsi`
     probe replicating the exact sequence directly against
     `CommandoWar.Sim` (removed after use). Fixed by updating the script's
     six target cells to the *pre-TASK-067 resolved* cells directly (the
     same probe, re-run with the client's own 0-based `CommandId`
     numbering, reproduced the real `--selfcheck` hash exactly, confirming
     the fix is byte-for-byte faithful to the intended outcome): no
     friendly casualties, machine gun neutralised, matching the original
     claim. Several agents still end the run mid-route contesting shared
     target cells -- the same live-agent chokepoint jam TASK-066 already
     found, still unresolved, still B-067's own second-half territory.
- the exact `FormatVersion` bump (14 -> 15); `content/fixtures/
  SPIKE-FIXTURE.md` was found still pinned at its format-4/TASK-028
  values despite a prior session's ledger note claiming it had already
  been corrected to format 14 -- that claim did not match the committed
  file (confirmed by `git log`) -- corrected straight to format 15 here,
  flagged for Dave, not silently perpetuated.

## Expected files

- `src/CommandoWar.Sim/Domain.fs` (`ReceivedOrder.AsGroup`), `Canonical.fs`
  (`FormatVersion` 14 -> 15, `writeOrder`), `Simulation.fs`
  (`commandIntake`, `appraisal`'s two call sites), `Appraisal.fs`
  (`appraise`'s `MoveTo` branch), `Diagnostics.fs`
  (`formationSlotOverlays`).
- `tests/CommandoWar.Sim.Tests/*.fs` (new facts; `FormatVersion` re-pins
  across `CanonicalHashTests.fs`, `CorpusTests.fs`, `DiagnosticsTests.fs`,
  `FixtureTests.fs`, `ReplayTests.fs`, `ScenarioTests.fs`, `SightTests.fs`,
  `TerrainTests.fs`, `PathfindingTests.fs` as needed).
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` (hover-preview
  gating); Godot `--selfcheck` re-pins if the scripted sequence's
  behaviour genuinely changes.
- `content/replays/`, `content/diagnostics/` (re-pinned, plus any new
  entry).
- `docs/04_SIMULATION_SPEC.md`, `docs/11_BACKLOG.md`,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Documentation updates

- this task file's status and evidence;
- `docs/04_SIMULATION_SPEC.md` (formation-resolution paragraph);
- `docs/11_BACKLOG.md` (B-067 row: this task's own scope closed, the
  multi-select/joint-dispatch half still open as a named follow-up);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
- `PROJECT_STATE.yaml`.

## Rollback or removal

A new canonical boolean field plus a small number of gated call sites is
additive and revertible with `git revert` in one step, but the
`Canonical.FormatVersion` bump re-pins every corpus/fixture/diagnostics
golden — reverting after those are re-pinned means re-reverting every
pinned hash too, not just the code. Prefer fixing forward once this lands.

## Review

- Reviewer: Dave
- Accepted: yes, 2026-09-20, on the self-verification evidence ("if it
  looks good then accept").
- Notes: the sim-side mechanism itself (`ReceivedOrder.AsGroup`) is small
  and low-risk, but implementing it surfaced two genuine, real-world
  consequences worth Dave's attention rather than silently absorbed: (1)
  the `formation-slots` corpus entry had to be re-authored to use a real
  joint order, since its old two-solo-orders authoring pattern is exactly
  what this task makes stop working; (2) the existing CommandDemo
  scripted self-check produced a genuine friendly casualty once redirect
  stopped compensating for its shared literal target cells, fixed by
  aiming each order at the agent's own distinct real destination directly
  -- which is exactly the new manual burden a player now carries for any
  solo click on a formationed agent's squadmate, until B-067's still-
  unscoped multi-select/joint-order half exists to carry it instead.
  Full detail in `docs/ledger/2026-09-20-TASK-067-formation-applies-only-
  to-group-orders.md`.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
