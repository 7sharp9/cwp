# TASK-072: Real-time salience for a fresh order refusal (auto-pause + floating label)

Status: done (accepted by Dave 2026-09-21, "accept both, commit")
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises B-072

## Objective

The instant any agent's order is newly appraised `Refused` or `Unable` this
tick, `CommandDemoScene` (a) engages tactical pause automatically, the same
mechanism already used when `WorldState.MissionOutcome` leaves `InProgress`,
and (b) draws a held, per-agent floating text label at that agent's own cell
naming the disposition and reason (`RenderShared.dispositionText`), visible
regardless of what is currently selected. Both changes are purely
presentational: no `CommandoWar.Sim` change, no new event, no
`Canonical.FormatVersion` bump, no effect on `Simulation.step` or any state
hash.

## Why this task exists

A same-day charter-alignment investigation (`PROJECT_STATE.yaml`'s
active-work note, 2026-09-21) and an independent game-UX-expert review both
found the same gap: a refusal is real, correctly reasoned, and concisely
worded (`RenderShared.dispositionText`/`reasonText`,
`RenderShared.fs:294-314`, e.g. `"refused: route too exposed (threat: agent
5)"`), but nothing makes it a noticeable *event*. Two separate problems
compound:

1. The order-status text (`liveOrderText`, `CommandDemoScene.fs:723-740`)
   only renders at all when exactly one agent is selected (`Set.count
   selected = 1`) -- with zero or several agents selected, which is the
   normal state during a squad-wide push, no disposition text renders for
   anyone.
2. Even when it does render, nothing calls attention to it -- no flash,
   sound, or auto-pause reads `OrderAppraised(Refused|Unable)` at all. This
   is in direct contrast to the client's existing, deliberately-built damage/
   casualty feedback (hit-flash, wound-dot opacity, audio-localised threat
   cue -- TASK-046/054/056), which *is* a real, working attention mechanism
   for a different class of event.

`docs/07_VERTICAL_SLICE.md` section 6 also requires "tactical pause or slow
motion while issuing orders" as part of the slice's required presentation.
Confirmed unbuilt during the same investigation (`CommandDemoScene.fs:
117,399,1626`): only a manual `Space`-toggled full pause (`OnTogglePause`)
and an automatic pause the instant `MissionOutcome` leaves `InProgress`
exist. This task closes that gap at the same time, by tying the pause to the
one moment the charter's own "diagnose resistance" step (section 2) actually
needs it.

A dedicated research pass this session (not this task) traced the exact
mechanism and confirmed it is safe and entirely client-side; see `Required
reading` and `Inputs and assumptions` below for its findings, folded in
directly rather than re-derived.

## Central decisions (confirmed with Dave 2026-09-21 via `AskUserQuestion`)

1. **Both auto-pause and a floating per-agent label** (not flash-only /
   no-pause, and not a corner-HUD-count-only readout). Auto-pause closes
   `docs/07` section 6's unbuilt requirement at the same time as fixing
   salience.
2. **A floating label at the refusing agent's own cell** (a new `Kind = 3`
   `DrawItem`, the `LEADER`/audio-cue-`"!"` precedent, `CommandDemoScene.fs:
   1051-1061,1286-1296`), not a corner-`HudText()` count -- ungates the
   *existing* per-agent status text problem directly, rather than adding a
   second, differently-gated summary alongside it.

## Required reading

- `docs/00_PROJECT_CHARTER.md` section 2 (the emotional loop -- "observe
  interpretation -> diagnose resistance") and section 7 ("legibility over
  realism") -- the product reason this task exists.
- `docs/07_VERTICAL_SLICE.md` section 6 (required presentation, including
  "tactical pause or slow motion while issuing orders") and section 8 (the
  canonical refusal sequence this task makes step 3-4 actually noticeable
  for).
- `docs/06_CONTENT_AND_PRESENTATION.md` around line 234 ("status indicators
  that do not rely on colour alone") and line 238 ("concise explanations for
  refusal, delay, adaptation, and panic") -- the existing presentation
  guidance this task's new marker must follow (shape/text carries the
  meaning, not a colour-only tint).
- `docs/11_BACKLOG.md` B-070's row in full (the review that produced this
  task) and B-072's own row.
- `src/CommandoWar.Sim/Events.fs`: `DomainEvent`/`EventBody.OrderAppraised
  of agent: AgentId * command: CommandId * disposition: OrderDisposition`
  (line 149) -- the exact, already-complete data this task reads; no
  `CommandoWar.Sim` field is missing.
- `src/CommandoWar.Sim/Domain.fs`: `OrderDisposition` (`Accepted | Refused of
  DecisionReason * DecisionReason[] | Unable of DecisionReason *
  DecisionReason[]`) and `DecisionReason` -- confirm every case
  `RenderShared.reasonText` already handles (it does; this task adds no new
  case).
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`:
  - `stepOnce()` in full (lines 365-492), especially the `let r =
    Simulation.step ...` binding (line 383) and the `for overlay in
    devFrame.Overlays do` loop (lines 462-492) -- `r.Events: DomainEvent[]`
    is this tick's real, non-lossy event array, currently read nowhere.
    `devFrame.Overlays` (built by `Diagnostics.frameOf r` at line 389) is
    **not** usable for this: every `Diagnostics.fs` overlay-builder
    explicitly matches `OrderAppraised _ -> None` (confirmed by this
    session's research pass), so no `Overlay` case carries disposition data
    at all. This task reads `r.Events` directly, inside `stepOnce`, the one
    place the real per-tick data exists before it goes out of scope.
  - The `heldAbandonedOrders`/`abandonedOrderHoldSeconds` field group
    (lines 196-204) and its population site (lines 487-491) -- the closest
    existing precedent: a per-agent held override, populated unconditionally
    for every agent regardless of selection, keyed by raw agent id.
  - The `heldFireLines`/`heldHitFlashes`/`heldAudioCues` field groups and
    their `Update`-driven wall-clock decay (search `deltaSeconds`) -- **read
    the decay carefully and note it does NOT apply to this task's label as
    written**: those effects fade by real wall-clock time *independent of
    `paused`* (their own field comments say so explicitly), which is correct
    for a fire-and-forget visual flash but wrong for this task's label,
    since the whole point of auto-pausing is to give the player unhurried
    time to read it -- see `Inputs and assumptions` below for the corrected
    behaviour this task actually needs.
  - `let mutable paused = false` (line 117), `OnTogglePause()` (line 1626),
    and the existing `if state.MissionOutcome <> InProgress then paused <-
    true` auto-pause (lines 398-399) -- the exact precedent this task's new
    auto-pause branch sits beside.
  - `Update(deltaSeconds)` (lines 635-655) -- confirm (this session's
    research pass already did, by inspection) that `stepOnce()`'s own
    `while accum >= simStep && ...` loop does not itself check `paused`
    mid-loop, so setting `paused <- true` inside `stepOnce` always lets the
    triggering tick fully complete (including this task's label state being
    populated) before advancement actually stops on the *next* `Update`
    call.
  - `member _.StepTicksHeadless(count: int64)` (lines 1656-1659) and
    `CommandDemoDrive.runScriptedSelfCheck` (lines 1699-1733) -- confirm (this
    session's research pass already did) that the scripted self-check calls
    `stepOnce()` directly in a loop with **no reference to `paused` at
    all**, bypassing `Update`/the wall-clock accumulator entirely. This
    task's auto-pause therefore cannot change `--selfcheck`'s tick count or
    pinned hash; re-verify this by running `--selfcheck` after implementing,
    not by assuming the research pass's read was correct.
  - `IClientScene.fs`'s `DrawItem` doc comment in full (`Kind = 3`: "a text
    label drawn at `(Cx,Cy)` with `Radius` as font size") and the two
    existing `Kind = 3` call sites (`LEADER`, lines 1051-1061; the audio-cue
    `"!"`, lines 1286-1296) -- the exact primitive and styling precedent to
    follow.
  - `RenderShared.dispositionText`/`reasonText` (`RenderShared.fs:294-314`)
    -- the exact player-facing text to reuse verbatim for the label, not a
    new wording.
  - `docs/11_BACKLOG.md` B-028's row -- why `HudText()` (not a floating
    label) was chosen for the *original* single-selection status line
    (TASK-042): "smaller change, reuses the established `selected=agent N`
    pattern". That reasoning was scoped to per-selected-agent status text
    and does not extend to "salience for an unselected agent", which is
    this task's actual target -- confirm this distinction still holds
    before writing code, do not silently re-litigate B-028's own decision
    for the single-selection line itself (out of scope here, see Forbidden
    scope).

## Dependencies

- B-070 (done -- the review that produced this task). No other task
  selected.

## Inputs and assumptions

- **Detection**: inside `stepOnce`, after `let r = Simulation.step ...`
  (line 383) and before `state <- r.State` overwrites the pre-tick view (or
  at any point `r` is still in scope), fold over `r.Events` for
  `{ Body = OrderAppraised(agent, _, (Refused _ | Unable _)) }`. This is
  "newly refused/unable *this tick*" by construction -- `OrderAppraised` only
  ever appears in `r.Events` on a tick appraisal actually ran, never as a
  restated steady-state value (confirm this reading of `Simulation.fs`'s
  appraisal phase against `Events.fs`'s own doc comment before relying on
  it).
- **Auto-pause rule**: if any such event exists this tick, set `paused <-
  true` (the `MissionOutcome` precedent, same field, same tick). No separate
  suppression rule is needed: setting `paused <- true` when already `true`
  is a no-op, and because detection is edge-triggered on this tick's actual
  `r.Events` (not on "current disposition still reads Refused"), pausing
  cannot re-fire from stale state once the player resumes -- it only fires
  again on a genuinely new appraisal transition on a later tick. Confirm this
  reasoning holds once real multi-agent orders are exercised (e.g. a
  six-agent push producing several `Refused` events on the *same* tick, or
  on consecutive ticks before the player reacts) -- if it does not hold in
  practice, that is a real finding to report, not to silently work around.
- **Label persistence -- deliberately NOT the `heldFireLines`/
  `heldHitFlashes` wall-clock-decay pattern.** Those effects fade by real
  `deltaSeconds` regardless of `paused`, which is correct for a fire-and-
  forget flash but wrong here: the entire point of auto-pausing is to give
  the player unhurried time to read the label, so a label that silently
  decays away while the game sits paused waiting for the player would
  defeat the feature. Instead: a refusal label persists for as long as that
  agent's live `AgentSnapshot.Disposition` (already canonical, already
  copied into `currAgents`, `CommandDemoScene.fs:621` and `r.Snapshot.Agents`
  every tick) still reads `Refused`/`Unable` for that same outcome --
  i.e. it is not a `ResizeArray<int * float>` countdown at all, but a plain
  per-agent lookup (`Map<int, OrderDisposition>` or read `currAgents`
  directly each frame) that clears itself the instant reappraisal produces
  `Accepted` or the order is superseded/fulfilled (`Disposition` becomes
  `None`). This is a genuine, deliberate deviation from the
  `heldFireLines`/`heldHitFlashes`/`heldAbandonedOrders` timer idiom, not an
  oversight -- state the reason in the diff's own comment, the way every
  other field group in this file explains its own design choice.
- **No `CommandoWar.Sim` change of any kind.** Confirmed by this session's
  research pass: `AgentSnapshot.Disposition` and `DomainEvent.OrderAppraised`
  already carry everything needed; nothing is discarded before reaching the
  client boundary except `Diagnostics.fs`'s own lossy `EventMarker`/`Overlay`
  re-encoding, which this task does not go through (it reads `r.Events`
  directly).
- No new `Canonical.FormatVersion`, no new event, no diagnostic-frame/
  `Overlay` change -- this is entirely `CommandoWar.Client.Godot`-side,
  rendering and pause-state only (`AGENTS.md`: "Rendering, animation, audio,
  particles, and interpolation are non-authoritative"); the `AGENTS.md`
  diagnostics mandate (extend `DiagnosticFrame` + a golden) therefore does
  not apply to this task, since it adds no authoritative spatial or tactical
  state.

## Allowed scope

- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: `stepOnce()`
  (new `r.Events` fold, new auto-pause branch beside the existing
  `MissionOutcome` one, new per-agent refusal-label state populated/cleared
  each tick); `DrawList()` (new `Kind = 3` label item per currently-refusing
  agent, the `LEADER`/audio-cue precedent); no change to `HudText()`'s
  existing single-selection `liveOrderText` line (see Forbidden scope).
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`: reuse
  `dispositionText` as-is; add a new pure helper only if genuinely needed to
  avoid duplicating its call shape (e.g. a thin wrapper that also returns a
  colour/style triple for the label) -- prefer reusing the existing function
  directly over adding one, per `AGENTS.md`'s "smallest change" rule.
- `tests`/`content/`: none expected -- this task changes no authoritative
  behaviour, so no `SimulationTests`/`Corpus.fs`/diagnostics golden is
  affected. If implementation finds otherwise, that is a signal something
  has leaked into `CommandoWar.Sim` scope and must stop, not be worked
  around.

## Forbidden scope

- No `CommandoWar.Sim`/`CommandoWar.Headless` change of any kind.
- No new event, `Overlay` case, or `Canonical.FormatVersion` bump.
- No change to `HudText()`'s existing single-selected-agent `liveOrderText`
  line or its `orderTextHoldSeconds` hold-timer mechanism (TASK-042/B-028) --
  that line stays exactly as it is; this task adds a *second*, differently-
  gated signal (the floating label) rather than modifying the first.
- No change to the `heldFireLines`/`heldHitFlashes`/`heldAudioCues` damage-
  feedback mechanisms -- this task's label deliberately does not follow
  their wall-clock-decay pattern (see Inputs and assumptions) and must not
  be folded into their shared timer bookkeeping.
- No change to `MissionOutcome`'s own existing auto-pause branch (lines
  398-399) beyond sitting beside it -- both conditions independently set
  the same `paused` field; neither branch's own logic changes.
- No tutorial, no new HUD panel/summary list, no sound effect -- if a sound
  cue is wanted later, that is a separate, smaller follow-up, not silently
  added here beyond what the central decisions above named.

## Required work

1. Confirm the `r.Events`-timing and `stepOnce`/`Update`/`StepTicksHeadless`
   claims in Required reading by inspection, not assumption.
2. Add the `r.Events` fold inside `stepOnce` detecting a fresh
   `Refused`/`Unable` `OrderAppraised` this tick; set `paused <- true` when
   any exist.
3. Add per-agent refusal-label state (a plain, non-decaying lookup, per
   Inputs and assumptions) populated from `currAgents`'
   `Disposition`/`Position` each tick, not from the fold in step 2 alone
   (the fold detects the *transition*; the label's *content and visibility*
   should track the agent's *current* `Disposition`, so it stays displayed
   and correct even across multiple paused frames, and clears the instant
   reappraisal or fulfilment changes it).
4. Add the new `Kind = 3` `DrawItem` per currently-refusing agent in
   `DrawList()`, styled distinctly from `LEADER`/the audio-cue marker
   (confirm a colour/text choice against `docs/06`'s "not colour alone"
   guidance -- the text itself, `RenderShared.dispositionText`'s wording,
   already carries the meaning, so this is mostly about placement/
   legibility, not inventing a new colour code).
5. Manually verify in a live Godot session (not just `--selfcheck`): issue
   an order that provokes a real refusal on Bridgehead (the B-071
   investigation's own repro -- pushing an agent toward `(10,5)`/`(10,6)`/
   `(11,6)` while riflemen 101/102 are alive is a known way to trigger
   `RouteTooExposed`) and confirm the game visibly pauses and the label
   appears at the refusing agent's cell, unselected.
6. Re-run `CommandDemo.tscn --selfcheck` (and `SnapshotDemo.tscn`/
   `AppraisalDemo.tscn`) through the real Godot 4.7.2 editor; confirm the
   pinned hash is unchanged (expected, since `StepTicksHeadless` never
   reads `paused`) -- do not assume, verify.
7. Update documentation per Documentation updates below.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [ ] A live editor session (screenshot evidence, `docs/evidence/`) shows:
      an ordinary push that provokes a real `RouteTooExposed`/`Unable`
      refusal on Bridgehead causes the game to auto-pause, and a floating
      label naming the disposition and reason appears at the refusing
      agent's own cell, with that agent **not** selected.
      **Not captured — no interactive display/mouse automation is
      available against a native Godot window in this implementing
      environment (no GUI, no windowed-app control tool), and the only
      existing scripted evidence path (`--screenshot*` flags in
      `FSharpSceneHost.cs`) has no mode that reproduces a refusal, and that
      file is outside this task's Allowed scope to extend.** Instead,
      verified the identical underlying behaviour end-to-end through a
      temporary `dotnet fsi` probe (removed after use, the established
      project precedent) driving the real, compiled
      `CwClientCore.CommandDemoScene`/`IClientScene` — not a hand
      simulation — against real `content/scenarios/bridgehead.cwscenario`:
      the scripted opening (`CommandDemoDrive.runScriptedSelfCheck`'s own
      six orders, `StepTicksHeadless(90)`) neutralises MG-100; two scouts
      onto the bridge deck give Perception a real `KnownContact` on
      riflemen 101/102; a fresh `MoveTo` order for a third, unselected
      agent (id 1, at `(8,5)`) toward `(13,6)` — diagnostically confirmed
      exposure 70 against this agent's own discipline-3 resolve threshold
      65 — produced a real `OrderAppraised(Refused(RouteTooExposed(Some
      AgentId 102)))` through the real `OnClick`/`Update` path. Observed,
      through the real `IClientScene` interface only (no field access to
      production logic): `paused` (read via reflection, diagnostic-only)
      became `true` on the very next tick; `DrawList()` returned a new
      `Kind = 3` item at `(8,5)` with `Text = "refused: route too exposed
      (threat: agent 102)"`, styled `(R,G,B,A) = (1.0, 0.25, 0.2, 0.95)`,
      `Radius = 9.0`; the agent was confirmed **not** selected
      (`selected = Set.empty`, and `HudText()` read `"... selected=none"`
      with no order-status suffix). See the ledger detail file for the full
      probe transcript. **A genuine finding, not a premise flaw**: the
      task's own named repro cells (push toward `(10,5)`/`(10,6)`/`(11,6)`
      immediately after the MG-100 opening) did **not** reproduce a
      refusal on their own in this probe — `Appraisal.routeExposure` only
      prices in *known* contacts (`WorldState.TacticalKnowledge`), which
      is empty until the squad has actually perceived riflemen 101/102
      (they start well outside the opening's own route), and even once
      known, a single-hop route to those adjacent cells only accumulates
      ~30-40 exposure against a discipline-3 agent's 65 threshold — short
      of `Refused`. A materially longer route into the kill zone was
      needed to cross the threshold. This matches, and gives a precise
      mechanism for, the project's own already-recorded finding
      (`PROJECT_STATE.yaml`'s 2026-09-21 active-work note) that refusal on
      Bridgehead "is reachable... it just isn't reliable or
      player-predictable." Flagged for Dave, not silently smoothed over;
      does not affect this task's own correctness, only the literal repro
      steps named in Required verification/Evidence to capture.
- [x] The label persists while paused (does not fade on a wall-clock timer)
      and clears once the agent's `Disposition` is no longer
      `Refused`/`Unable` for that outcome. Persistence confirmed live in
      the same probe: after 60 further `Update(0.05s)` calls (3 more
      wall-clock seconds) plus two new control orders issued to different,
      uninvolved agents, `paused` stayed `true`, every agent figure's
      on-screen cell was byte-identical to before (proving `stepOnce`
      never ran again, not merely coincidence), and the refusal label was
      still present with unchanged text. The "clears on
      Accepted/superseded" half is not a separate timer to test — the
      implementation has no decay state at all, it is a plain per-frame
      filter over `currAgents`' live `Disposition` (`Some(Refused _ |
      Unable _)`), so clearing follows directly from `AgentSnapshot.
      Disposition` itself changing, already proven correct at the
      `CommandoWar.Sim` level; confirmed by code inspection, not a
      separate live re-fire test (time-boxed — see risks in the final
      report).
- [x] The existing single-selection `HudText()` order-status line
      (TASK-042/B-028) is confirmed unchanged by inspection and by a
      manual check. By inspection: `git diff` touches only two new,
      additive blocks in `stepOnce`/`DrawList()`; lines 723-748
      (`liveOrderText`) and 1318+ (`HudText()`) are byte-identical to
      before. By the same probe: with nothing selected, `HudText()` read
      `"... selected=none"` with no order-status suffix, matching
      pre-task behaviour for an empty selection exactly.
- [x] `CommandDemoDrive.runScriptedSelfCheck`'s pinned tick/hash is
      reconfirmed `MATCH`, unchanged, through the real Godot 4.7.2 editor --
      confirming the auto-pause does not alter the scripted sequence
      (`StepTicksHeadless` never reads `paused`). `MATCH expected final
      hash 0x84A25E3559111E9B at tick 90`, unchanged from the pinned value.
- [x] `SnapshotDemo.tscn`/`AppraisalDemo.tscn` `--selfcheck` hashes
      reconfirmed unchanged. `SnapshotDemo.tscn`: `MATCH expected final
      hash 0x6213D672BC36FDB8 at tick 20`. `AppraisalDemo.tscn`: `MATCH
      expected hash 0xA1354EB998FC1B95` (via its own standalone
      `AppraisalDemoScene.cs` self-check path, independent of
      `FSharpSceneHost.RunSelfCheck`'s switch — TASK-071 already found and
      recorded that distinction; both unaffected by this task either way).
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless` file touched (`git diff
      --stat` shows only `CommandoWar.Client.Godot` files, plus docs).
      Confirmed: `git diff --stat` shows only
      `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` (68
      insertions, 1 deletion).
- [x] `dotnet build` both `.slnx` (main Release; Godot client Debug):
      `0 Warning(s)`, `0 Error(s)`. Both confirmed.
- [x] `dotnet test`: unaffected count (no new/changed `CommandoWar.Sim`
      facts expected). `422/422` passed, unaffected (the same count
      TASK-070's own note records as the pre-existing baseline).
- [x] Required documentation updated (see Documentation updates below) --
      narrowed for this dispatch to this task file and its ledger detail
      file; `docs/07_VERTICAL_SLICE.md`/`docs/11_BACKLOG.md`/
      `PROJECT_STATE.yaml`/the ledger index table are left for the
      orchestrating session to reconcile centrally against the parallel
      TASK-073 dispatch, per this dispatch's own scope guardrails.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0 Warning(s)`, `0 Error(s)`.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`:
  `0 Warning(s)`, `0 Error(s)`.
- `dotnet test CommandoWar.slnx -c Release`: unaffected count, confirmed by
  running it, not assumed.
- `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path
  src/CommandoWar.Client.Godot scenes/CommandDemo.tscn -- --selfcheck`:
  `MATCH`, pinned hash unchanged from the pre-task value (re-confirm the
  exact hash by running the command before this task, not by trusting the
  value recorded for TASK-070/071 without re-checking it is still current).
  Same for `SnapshotDemo.tscn`/`AppraisalDemo.tscn`.
- A live windowed Godot session: issue the B-071 repro order sequence
  (neutralise MG-100 via the existing scripted opening, then push an agent
  toward `(10,5)`/`(10,6)`/`(11,6)` while riflemen 101/102 are alive) and
  screenshot the auto-pause + floating label firing. Save as
  `docs/evidence/task-072-refusal-salience.png`.
- `git status --porcelain` / `git diff --stat`: matches this task's Allowed
  scope exactly -- no `CommandoWar.Sim`/`CommandoWar.Headless` entry.

## Evidence to capture

- All command output above.
- The live screenshot showing the auto-pause + label firing on an
  unselected agent.
- Confirmation (quoted from the actual re-run, not assumed) that
  `StepTicksHeadless`/`--selfcheck` is genuinely unaffected.
- Any deviation from the stated detection/persistence design found during
  implementation, reported honestly rather than silently worked around (the
  established convention in this project's own task files).

## Expected files

- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`,
  `RenderShared.fs` (only if a small shared helper is genuinely needed).
- `docs/11_BACKLOG.md` (B-072 row), `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.
- `docs/07_VERTICAL_SLICE.md` section 6 (the "tactical pause while issuing
  orders" realisation note, now closed).
- `docs/evidence/task-072-refusal-salience.png`.

## Documentation updates

- this task file's status and evidence;
- `docs/07_VERTICAL_SLICE.md` section 6 (realisation note for the pause
  requirement);
- `docs/11_BACKLOG.md` (B-072 row `proposed -> review`/`done` with
  self-verification evidence);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
- `PROJECT_STATE.yaml`.

## Rollback or removal

Purely additive and purely client-side: new local state and one new
`DrawItem` case usage inside `CommandDemoScene.fs`, no shared mutable state
touched outside this file, no canonical field, no format bump. A `git
revert` of this task's commit should be clean with nothing else to re-pin.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-21, "accept both, commit and update any docs so we
  know where progress is it"), on the implementation-plus-independent-
  re-verification evidence recorded here and in
  `docs/ledger/2026-09-21-TASK-072-refusal-salience-autopause-and-floating-label.md`
  (dispatched as one of two parallel implementation agents, each in an
  isolated worktree, and separately rebuilt/retested/re-`--selfcheck`ed by
  the orchestrating session before being reported as ready for review). The
  honest finding that the task's own named repro cells did not reproduce a
  refusal on their own (a longer route into the kill zone, issued after the
  squad already had a known contact, was needed) was reviewed and accepted
  as-is -- it does not change the implementation, only the literal steps
  needed to trigger it live.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
