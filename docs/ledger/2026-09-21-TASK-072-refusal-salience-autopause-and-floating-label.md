## 2026-09-21 - TASK-072 - Refusal salience: auto-pause + floating label

**Owner:** Dave
**Source revision:** working tree on top of `main` at `aabc46a` (TASK-071
accepted), implemented in an isolated worktree
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot
`4.7.2-stable_mono_win64` (console executable, headless only in this
environment -- see Deviations)
**Status change:** `proposed -> review` (self-verified)

### Changes

Realises backlog row B-072. `CwClientCore.CommandDemoScene` (`stepOnce`,
`DrawList`) now gives a fresh `Refused`/`Unable` order appraisal real
in-game salience, closing the gap the 2026-09-21 charter-alignment review
and B-070's UX review both independently found: the disposition text was
accurate but invisible whenever zero or several agents were selected, and
nothing called attention to it at all.

Two purely additive, purely client-side changes to
`src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`:

1. **Auto-pause** (`stepOnce`, beside the existing `MissionOutcome`
   auto-pause branch, same `paused` field): folds over `r.Events` --
   `Simulation.step`'s real, non-lossy `DomainEvent[]`, not
   `devFrame.Overlays` (every `Diagnostics.fs` overlay-builder explicitly
   discards `OrderAppraised _ -> None`) -- for a fresh
   `OrderAppraised(_, _, Refused _ | Unable _)` this tick. Genuinely
   edge-triggered: `OrderAppraised` is "never emitted on a tick where the
   order is unchanged and already appraised" (`Events.fs`'s own doc
   comment), so this cannot re-fire from a disposition that merely stays
   `Refused`/`Unable` across several paused ticks.
2. **Floating label** (`DrawList`, a new `refusalLabelItems` block folded
   into the existing depth-sorted item list beside `leaderMarkerItems`): a
   `Kind = 3` `DrawItem` (`RenderShared.cellLabel`, the existing `Kind = 3`
   helper -- no new `RenderShared` function needed) at every currently
   `Refused`/`Unable` agent's own `Position`, text reused verbatim from
   `RenderShared.dispositionText`, styled a warning red `(1.0, 0.25, 0.2)`
   distinct from `LEADER` (green) and the audio-cue `"!"` (orange).
   Deliberately reads `currAgents`' live `Disposition` directly each frame
   -- no new mutable field, no timer -- rather than the
   `heldFireLines`/`heldAudioCues`/`heldAbandonedOrders` wall-clock-decay
   idiom those already use: the whole point of auto-pausing is unhurried
   reading time, so the label must track the live disposition, not fade
   out from under a paused game (the task's own explicitly-sanctioned
   alternative to a `Map<int, OrderDisposition>` lookup: "read `currAgents`
   directly each frame").

No `CommandoWar.Sim`/`CommandoWar.Headless` change of any kind, no new
event, no `Overlay` case, no `Canonical.FormatVersion` bump -- confirmed by
`git diff --stat` (see Verification).

### Verification

- `dotnet build CommandoWar.slnx -c Release`: `Build succeeded. 0
  Warning(s) 0 Error(s)`.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`:
  `Build succeeded. 0 Warning(s) 0 Error(s)`.
- `dotnet test CommandoWar.slnx -c Release`: `Passed! - Failed: 0, Passed:
  422, Skipped: 0, Total: 422` -- unaffected (the same count TASK-070's own
  ledger records as the pre-existing baseline).
- `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path
  src/CommandoWar.Client.Godot scenes/CommandDemo.tscn -- --selfcheck`:
  `MATCH expected final hash 0x84A25E3559111E9B at tick 90`, unchanged from
  the pinned value re-confirmed for TASK-071.
- Same, `scenes/SnapshotDemo.tscn`: `MATCH expected final hash
  0x6213D672BC36FDB8 at tick 20`, unchanged.
- Same, `scenes/AppraisalDemo.tscn`: `MATCH expected hash
  0xA1354EB998FC1B95, agent 0 Refused / agent 1 Accepted` -- via its own
  standalone `AppraisalDemoScene.cs` self-check path (predates
  `FSharpSceneHost`/ADR-0004 entirely, TASK-071 already found and recorded
  this distinction), unaffected either way since this task never touches
  that file.
- `git status --porcelain` / `git diff --stat`: only
  `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` (68 insertions, 1
  deletion) plus this task's own new files (the task file, this ledger
  entry) -- no `CommandoWar.Sim`/`CommandoWar.Headless` entry.
- **Live behaviour, end-to-end, via a temporary `dotnet fsi` probe (removed
  after use, the established project precedent)** driving the real,
  compiled `CwClientCore.CommandDemoScene` through its actual
  `IClientScene` interface (`OnClick`/`OnHover`/`Update`/`DrawList`/
  `StepTicksHeadless`) against real `content/scenarios/bridgehead.cwscenario`
  -- not a hand simulation, and not `Simulation.step` called directly. No
  live windowed Godot session is possible in this implementing environment
  (no interactive display or native-window mouse/keyboard automation tool
  is available), so this is the strongest verification available short of
  a real editor session; see Deviations below for exactly what could not
  be captured as a result, and what this probe substitutes for it.

  Sequence: the existing scripted opening
  (`CommandDemoDrive.runScriptedSelfCheck`'s own six single-recipient
  `MoveTo` orders, `StepTicksHeadless(90)`) neutralises MG-100 (confirmed:
  all six friendly agents `Alive`, agent 100 `Dead`, riflemen 101-104
  `Alive`, via reflection into the scene's private `state` field --
  diagnostic-only, the production code path never does this). Two scouts
  (`OnClick`/`OnHover`/`OnClick` onto `(9,5)`/`(9,6)`) then give Perception
  a real `KnownContact` on riflemen 101 and 102 within 3 further ticks
  (confirmed via `WorldState.TacticalKnowledge`, again by reflection). A
  fresh, real `MoveTo` order for agent 1 (still at `(8,5)`, unselected
  going in) toward `(13,6)` -- diagnostically confirmed via a direct
  `Appraisal.routeExposure`/`Pathfinding.findWithin` call against the live
  state (exposure `70` against this agent's own discipline-3 resolve
  threshold `65`, `resolveThreshold` computed the same way
  `Appraisal.appraise` does) -- then produces a real
  `OrderAppraised(Refused(RouteTooExposed(Some AgentId 102)))` through the
  actual `OnClick`/`Update` path. The agent was explicitly deselected
  (`OnClick(false, 0, 0, false)`, a right-click) before stepping, so the
  "not selected" acceptance criterion is genuinely exercised, not
  coincidentally true.

  Observed after one `Update(1.0/20.0)` call: `paused` (read via
  reflection, diagnostic-only -- not exposed by `IClientScene`) became
  `true`; `DrawList()` returned exactly one new `Kind = 3` item at
  `(8,5)`, `Text = "refused: route too exposed (threat: agent 102)"`,
  `(R,G,B,A) = (1.00, 0.25, 0.20, 0.95)`, `Radius = 9.0`; `selected` (also
  read via reflection) was `Set.empty`; `HudText()` (the real,
  `IClientScene`-exposed method) read `"tick 121   hash
  0xDAC734B0806D5E29   draws 28   agents 11   PAUSED   selected=none"` --
  no order-status suffix, confirming the existing single-selection line is
  unaffected. After 60 further `Update` calls (3 more wall-clock seconds)
  plus two additional control `MoveTo` orders issued for different,
  uninvolved agents, `paused` was still `true`, every full-opacity agent
  figure's on-screen cell in `DrawList()` was byte-identical to before
  (proving `stepOnce` never ran again -- a genuine pause, not a
  coincidence, since the control orders would otherwise have been
  delivered and visibly moved their agents), and the refusal label was
  still present with unchanged text.

### Evidence

- Full probe output (see Deviations for why this substitutes for a
  screenshot):

  ```
  opening done, paused=false
  scouts on deck, paused=false
  refusal label appeared after 1 Update(0.05) calls (or -1 if never)
  paused = true
  selected agents = set [] (empty means the refusing agent is NOT selected)
  HudText() = "tick 121   hash 0xDAC734B0806D5E29   draws 28   agents 11   PAUSED   selected=none"
  refusal labels present: 1
    at (8,5) text="refused: route too exposed (threat: agent 102)" color=(1.00,0.25,0.20) alpha=0.95 radius=9.0
  paused after 60 more Update calls = true
  agent figures unchanged (auto-pause holding)? true
  refusal label(s) still present, unchanged: 1
    text="refused: route too exposed (threat: agent 102)"
  ```

- All build/test/`--selfcheck` command output above.
- No `docs/evidence/task-072-refusal-salience.png` was produced -- see
  Deviations.

### Deviations and unresolved issues

- **The required live-editor screenshot (`docs/evidence/
  task-072-refusal-salience.png`) was not captured.** This implementing
  environment has no interactive display and no tool to drive mouse/
  keyboard input against a native windowed application (the available
  browser-automation tooling controls web pages, not a native Godot
  window); a genuinely interactive, windowed Godot session is not possible
  here. The only existing scripted evidence-capture path in this codebase
  (`--screenshot`/`--screenshot-mission`/`--screenshot-squad`/
  `--screenshot-multiselect` in `FSharpSceneHost.cs`) has no mode that
  reproduces a refusal, and that file is outside this task's own Allowed
  scope (`CommandDemoScene.fs`/`RenderShared.fs` only) to extend. Verified
  the identical underlying behaviour instead via the `dotnet fsi` probe
  above, which exercises the real compiled scene code end-to-end (a
  stronger guarantee of correctness than a single screenshot, though not a
  substitute for the specific deliverable the task names). **Dave will
  need to run a live editor session himself to close this specific
  acceptance-criterion gap**, or confirm the probe evidence is sufficient.
- **A genuine, honest finding, not a premise flaw: the task's own named
  repro steps (push toward `(10,5)`/`(10,6)`/`(11,6)` immediately after the
  scripted MG-100 opening) do not reproduce a refusal on their own.** Two
  compounding reasons, both confirmed by direct inspection/probing, not
  guessed:
  1. `Appraisal.routeExposure` (`Appraisal.fs:259`) only prices in
     *known* contacts (`WorldState.TacticalKnowledge`), never raw ground
     truth. Riflemen 101/102 start well outside the scripted opening's own
     route and are not yet perceived by the squad at tick 90 --
     `TacticalKnowledge` is empty, so every route's exposure is `0`
     regardless of terrain/LOS, and the order is trivially `Accepted`.
  2. Even once the riflemen are known (after sending scouts onto the
     bridge deck), a single-hop `MoveTo` from an adjacent cell to `(10,5)`/
     `(10,6)`/`(11,6)` only accumulates exposure `30`-`40` (confirmed via a
     direct `Appraisal.routeExposure` call against the live state) --
     under a discipline-3 agent's resolve threshold of `65`
     (`AppraisalConfig.BaseResolve + DisciplineResolveWeight * discipline`
     = `20 + 15*3`), so it is still `Accepted`. A materially longer route
     deeper into the engagement envelope (confirmed: `(8,5) -> (13,6)`,
     exposure `70`) was needed to cross the threshold.

  This is a precise mechanism for, and consistent with, the project's own
  already-recorded finding (`PROJECT_STATE.yaml`'s 2026-09-21 active-work
  note, from the charter-alignment investigation that led to this task's
  own drafting): refusal on Bridgehead "is reachable... it just isn't
  reliable or player-predictable." It does not indicate anything wrong
  with this task's own implementation -- the auto-pause/label mechanism
  fired correctly and immediately once a real `Refused` did occur -- only
  that the *specific* repro steps named in the task file's own Required
  verification/Evidence to capture sections need updating (a longer route,
  issued after the squad already has a known contact) for whoever next
  tries to reproduce this live, including Dave's own upcoming live-editor
  pass.
- No test or content change was needed or made (`tests`/`content/`
  untouched) -- this task changes no authoritative behaviour, consistent
  with its own Allowed scope note that finding otherwise would be a signal
  something had leaked into `CommandoWar.Sim` scope.
- Per this dispatch's own scope guardrails (run in parallel with a
  separate TASK-073 dispatch, in its own isolated worktree), this session
  did **not** edit `docs/11_BACKLOG.md`, `PROJECT_STATE.yaml`, the
  `docs/12_PROGRESS_LEDGER.md` index table, or `docs/07_VERTICAL_SLICE.md`
  section 6's own realisation note -- all left for the orchestrating
  session to reconcile centrally once both parallel dispatches report
  back, to avoid two agents in two worktrees both editing the same shared
  files.
- **This worktree was found stale by 12 commits (TASK-061 through
  TASK-071) relative to `main` at the start of this session** -- its own
  branch had not been fast-forwarded since TASK-060. Fast-forwarded it
  (`git merge --ff-only main`, confirmed a clean ancestor relationship and
  a clean working tree first) before starting any work, since the task
  file's own `Required reading` citations (line numbers, the
  `MissionOutcome` auto-pause precedent, `heldAbandonedOrders`, `DrawItem
  Kind = 3` precedents) do not exist at all before TASK-062/063/064/065 --
  confirmed by grepping for them and finding nothing prior to the
  fast-forward. Not itself part of this task's own change; flagged here
  since it is an environment-setup finding, not a silent workaround.

### Documents updated

- This task file (`tasks/TASK-072-REFUSAL-SALIENCE-AUTOPAUSE-AND-FLOATING-LABEL.md`):
  status, Acceptance criteria, Review.
- This ledger entry.
- **Not updated in this dispatch** (see Deviations): `docs/11_BACKLOG.md`,
  `PROJECT_STATE.yaml`, `docs/12_PROGRESS_LEDGER.md`'s index table,
  `docs/07_VERTICAL_SLICE.md`.

### Review

- Reviewer: Dave
- Accepted: yes, 2026-09-21 ("accept both, commit and update any docs so we
  know where progress is it"), on the self-verification evidence above plus
  the orchestrating session's own independent re-run of the build/test/
  `--selfcheck` commands before reporting this task as ready for review.
