# TASK-068: Multi-select UI and joint order dispatch

Status: done (implemented and self-verified 2026-09-21; accepted by Dave 2026-09-21 on the self-verification evidence, no live editor test of the drag gesture)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises the second, client-side half of B-067

## Objective

Let the player select several friendly agents at once (drag rubber-band-select
over empty ground, shift-click to add/remove one at a time) and issue one
joint order to the whole selection, dispatched through the sim's existing
`Command.moveToMany` for `MoveTo` so formation redirect (`ReceivedOrder.
AsGroup`, TASK-067) applies for the first time to a real client-issued group
order. No `CommandoWar.Sim` change: the sim-side mechanism this task exercises
is already built, tested, and accepted.

## Why this task exists

TASK-067 closed B-067's sim-side half (formation redirect keys off the
order's own recipient count, not an agent's static field) explicitly to ship
"solo click always goes where clicked" without needing any new UI. It left
B-067's other half named but unscoped: "select N agents then issue a joint
order," Dave's own words scoping B-067 originally ("like selecting n agents
then doing a joint order"). `CommandDemoScene.fs` today can only ever select
and order one agent at a time (`selected: AgentId option`); nothing in the
client has ever called `Command.moveToMany`.

Scoped 2026-09-20/21 via two parallel research passes (one tracing every
`CommandDemoScene.fs` site `selected: AgentId option` touches; one tracing
`FSharpSceneHost.cs`'s mouse-input pipeline for what a drag gesture needs)
plus one `AskUserQuestion` round resolving the four product-facing forks the
traces could not answer from code alone. The gesture itself (drag rubber-band
+ shift-click) and the formation rule (each agent's own authored
`FormationOffset` only, no ad-hoc computed formation for an arbitrary
selection) were already confirmed in the TASK-067 session.

## Central decisions (confirmed 2026-09-20/21)

- **Selection gesture**: drag rubber-band-select (a click-drag over empty
  ground selects every friendly agent whose on-screen figure centre falls
  inside the drawn rectangle) and shift-click (toggles one agent in/out of
  the current selection). A plain, non-shift click on a friendly always
  replaces the whole selection with just that agent — the existing
  single-select behaviour, unchanged when no modifier is held (my own
  low-stakes implementation default, not a separate fork: it is the only
  reading that keeps a bare click's behaviour byte-identical to today, which
  the scripted self-check's pinned hash depends on).
- **Right-click** (confirmed via `AskUserQuestion`): clears the *entire*
  selection, regardless of how many agents are selected — matches today's
  only existing right-click behaviour, no new "remove one" mode.
- **Hover/route preview** (confirmed): shows *every* selected agent's own
  resolved route, not just one. For `MoveTo` (order mode 0) with more than
  one agent selected, each agent's own preview is resolved through
  `Appraisal.resolveFormationTarget` (the exact function and inputs
  `Simulation.fs`'s own appraisal phase uses) before pathfinding to it —
  reviving, per-agent, the redirect-preview logic TASK-064 round 1 added and
  TASK-067's own `OnHover` comment retired for the solo case (that comment is
  now updated: "no multi-select UI exists yet" is no longer true). A solo
  selection or a non-`MoveTo` order mode keeps today's literal-cell preview
  unchanged.
- **HUD selection line** (confirmed): a count for more than one agent
  ("N agents"), not a list of ids. Exactly one selected agent keeps today's
  exact text (`"agent %d"`) unchanged — a deliberate implementation choice to
  keep the single-selection HUD line byte-identical, not a new fork.
  Per-agent detail (the order-status suffix, and the `F1` dev-overlay
  agent-detail line/LOS ray/exposed-route markers) is shown only when exactly
  one agent is selected; a larger selection shows the count alone for the
  order-status suffix and a one-line "N agents selected (per-agent detail
  needs a single selection)" for the dev overlay. Dev-only, lowest stakes,
  decided directly rather than asked.
- **Partial dispatch** (confirmed): a selected agent that is dead,
  incapacitated, or already standing on the clicked cell is silently dropped
  from the order; the rest of the selection still gets it. If every selected
  agent gets dropped, nothing is dispatched (the pre-existing "click does
  nothing" outcome, not a new failure mode).

## Required reading

- `docs/11_BACKLOG.md` B-067 row (both halves' scoping rationale).
- `tasks/TASK-067-FORMATION-APPLIES-ONLY-TO-GROUP-ORDERS.md` (the sim-side
  mechanism this task is the client half of; its "Forbidden scope" explicitly
  named this task as the deferred follow-up).
- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md`: the "no logic in C#" rule
  and the per-concern table ("Raw input capture | C#", "Input -> typed
  command | F#") — the drag/shift-click input this task adds must keep
  `FSharpSceneHost.cs` at marshal-and-forward only, exactly like the existing
  `TryHitAgentCircle`/`ScreenToCell`/`TryHitOrderModeIcon` precedents; never
  expose an F# `Set`/`option`/DU across the boundary, only primitives and
  arrays of primitives (extending the existing `int`/`bool`/`DrawItem[]`
  idiom already crossing this boundary, not any new record type).
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`: the full interface —
  every existing member's own "primitives only" doc comment explains the
  pattern this task's new/changed members must follow.
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: the whole file.
  Central to this task: `selected: AgentId option` and every site reading or
  writing it (`syncOrderModeToSelection`, `liveOrderText`, `haloItems`,
  `holdOutlineItems`, the dev-overlay `exposedCells`/`devAgentText`/`losRay`,
  `HudText`'s `selText`, `OnClick`, `OnHover`); `pending`/`commandsForTick`
  and the existing dedup line `pending.RemoveAll(fun c -> c.Command.Agent =
  agentId)` (see the real bug this task must fix, below);
  `CommandDemoDrive.runScriptedSelfCheck` (must keep producing the identical
  pinned hash — every click in it is single-agent, no shift, no drag).
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: `_UnhandledInput`
  (today has no button-up/`Pressed: false` handling at all — a drag gesture
  needs a real press/motion/release sequence, not a tweak),
  `TryHitAgentCircle` (the per-agent screen-position derivation a rectangle
  hit-test reuses), `TryHitOrderModeIcon` (the existing `Rect2.HasPoint`
  precedent), `_Draw` (`DrawRect`'s existing translucent-fill-plus-border
  idiom, `DrawMissionSummaryPanel`'s own precedent, for the marquee), every
  existing `_scene.OnClick(...)` call site (screenshot priming — all need
  their new `shiftHeld` argument added).
- `src/CommandoWar.Sim/Commands.fs`: `PlayerCommand.Agent` (`List.head
  Recipients` — the exact reason the existing `pending` dedup is wrong for a
  multi-recipient command), `Command.moveTo`/`.moveToMany` (confirm
  `moveToMany`'s single-element-list output is byte-identical in shape to
  `moveTo`'s own output — the fact this task's design leans on to keep a
  solo click's dispatched command unchanged), `.hold`/`.assault`/`.withdraw`/
  `.suppress` (no `*Many` variant exists for any of these — confirm by
  inspection, matching `Appraisal.appraise`'s own intent dispatch below,
  before assuming one needs to be added).
- `src/CommandoWar.Sim/Appraisal.fs`: `appraise`'s intent match (`MoveTo`
  reads `order.AsGroup`; `Hold`/`Withdraw`/`Assault`/`Suppress` never read
  formation at all) — confirms a group `Hold`/`Assault`/`Withdraw`/`Suppress`
  order needs no `*Many` sim builder, just N existing single-recipient
  commands in one dispatch; `resolveFormationTarget`'s own signature
  (`terrain -> occupied: Cell[] -> offset: Cell option -> anchor: Cell ->
  Cell`) for the hover-preview reuse.
- `src/CommandoWar.Sim/Simulation.fs`: the `appraisal` phase's own `occupied`
  computation (`agents |> Array.choose (fun x -> if x.Id = a.Id then None
  else Some x.Position)`, every other agent regardless of vitals) — the
  client-side hover preview must build `occupied` the identical way from
  `currAgents`, or the preview would honestly diverge from what the sim
  itself will actually resolve.
- `src/CommandoWar.Client.Godot/README.md`: the TASK-040 "left-click a
  friendly agent to select it... right-click deselects" description and the
  TASK-048/064 order-mode sections — this task's own new section follows the
  same per-task documentation pattern.

## Dependencies

- B-067's sim-side half (TASK-067, done — the mechanism this task exercises
  for real for the first time). No other task selected.

## Inputs and assumptions

- No `CommandoWar.Sim` change of any kind: `Command.moveToMany` and every
  other builder this task calls already exist, are already validated
  (`EmptyRecipients`/`DuplicateRecipient` command-intake checks), and are
  already exercised by `Corpus.fs`/unit tests. No `Canonical.FormatVersion`
  bump, no corpus/fixture/diagnostics re-pin expected from this task alone.
- Per AGENTS.md's Diagnostics section, extending `Diagnostics.DiagnosticFrame`
  and adding a golden visualiser entry is required only for a task that
  "adds or changes authoritative spatial or tactical state." This task adds
  none — it is a client-only input/selection/dispatch feature over an
  already-authoritative, already-tested sim mechanism. No `Diagnostics.fs`
  change, no new `content/diagnostics/` entry.
- `selected` changes shape from `AgentId option` to a set (`Set<AgentId>` or
  equivalent), empty by default. Every existing consumer is updated per the
  "Central decisions" above, not left partially `Option`-shaped.
- A plain (non-shift) left-click on a friendly agent, with nothing else
  changed, must dispatch an identical `PlayerCommand` value to today's
  single-agent path — `Command.moveToMany` with a one-element `Recipients`
  list is byte-identical in every field to `Command.moveTo`'s own output
  (confirmed by inspection: same `Urgency = Routine`, `RiskTolerance =
  Standard`, same `Body`), so always routing `MoveTo` dispatch through
  `moveToMany` (never conditionally falling back to `Command.moveTo`) is
  actually the *simpler* implementation, not a special case, and keeps
  `CommandDemoDrive.runScriptedSelfCheck`'s pinned hash (`0x2629A1FE165F94BB`)
  unchanged.
- A real, pre-existing bug (found while scoping, not introduced by this
  task, but must be fixed as part of it since a multi-recipient command is
  the first thing to expose it): `OnClick`'s pending-order dedup,
  `pending.RemoveAll(fun c -> c.Command.Agent = agentId)`, checks only the
  *head* of a pending command's `Recipients` (`PlayerCommand.Agent`'s own
  documented definition). A stale pending single-recipient order for any
  *non-head* recipient of a new multi-recipient command would survive this
  check and land in the same tick's batch alongside the new command — two
  commands addressing the same agent in one tick, the exact untested
  combination `OnClick`'s own existing comment already warns against.
  Fixed by matching on `Recipients` membership against every agent this
  click is about to address, not `.Agent` equality against one id.
- Hold/Assault/Withdraw/Suppress (order modes 1-4) have no `*Many` command
  builder and do not need one: only `MoveTo` reads `AsGroup`/formation
  (confirmed in Appraisal.fs above), so a group order for any of these four
  is simply N existing single-recipient commands, one per filtered selected
  agent, all added to `pending` for the same delivery tick — `Simulation.
  step` already consumes an array of commands per tick, so this reproduces
  today's per-agent semantics exactly, just issued from one click instead of
  N.
- The rectangle-vs-agent hit test for drag-select reuses `TryHitAgentCircle`'s
  own per-agent screen-position derivation and full-opacity-real-agent filter
  (`item.Kind = 1 && item.A >= 0.99f`), swapping its point-vs-circle distance
  test for `Rect2.HasPoint` against the agent's screen centre — resolved
  entirely in `FSharpSceneHost.cs` (screen-space geometry, ADR-0004's
  "screen<->cell projection arithmetic" allowance), handing F# only the
  resolved `(cellX, cellY)` pairs of whichever agents (any side; F#'s own
  `friendlyAt` does the side filtering, the existing `OnClick`/single-hit
  precedent) fell inside the rectangle — never raw screen or rectangle
  coordinates, and never an agent id (the boundary has never passed an id
  across, only cells).
- `InputEventMouseButton.ShiftPressed` (inherited from
  `InputEventWithModifiers`) and `Rect2.Abs()` (normalises a rect built from
  two arbitrary corner points to a positive-size, top-left-origin rect) are
  both confirmed live Godot 4.x C# APIs (checked via current documentation,
  not recalled from training alone, per AGENTS.md's external-facts
  discipline) — `dotnet build` is the fast, authoritative confirmation this
  task's own verification step relies on regardless.
- A drag below a small pixel threshold (6px) resolves as an ordinary click,
  not a (degenerate, zero-agent) drag-select — needed so an intended
  single-agent click with a few pixels of hand tremor does not silently
  become "select nothing, dispatch nothing."

## Allowed scope

- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`: `OnClick` gains a
  `shiftHeld: bool` parameter; new `OnDragSelect: cellXs: int[] * cellYs:
  int[] * shiftHeld: bool -> unit` member.
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: `selected`'s type
  and every site reading/writing it (see Required reading); `OnClick`'s
  friendly-select/shift-toggle/right-click-clear branch and its
  order-dispatch branch (generalised to N agents, the dedup fix, the
  `moveToMany`/looped-singles split by order mode); new `OnDragSelect`
  implementation; `OnHover`'s `previewPath` (becomes per-agent, formation-
  aware for a multi-agent `MoveTo` preview); `syncOrderModeToSelection`
  (disarm only once zero `Alive` candidates remain in the selection);
  `CommandDemoDrive.runScriptedSelfCheck`'s existing `OnClick` calls (add the
  new `false` argument only — no behavioural change).
- `src/CommandoWar.Client.Godot/Core/DemoRenderScene.fs`: mirror the
  `IClientScene` signature changes with no-op bodies (the existing
  precedent for a scene with no selection concept).
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`: drag-tracking state
  fields; `_UnhandledInput` restructured for a real left-button press/
  motion/release sequence (right-click and the HUD-icon click stay
  single-press, unchanged in spirit); a rectangle-vs-agent-screen-position
  resolver (reusing `TryHitAgentCircle`'s own derivation); marquee drawing in
  `_Draw`; every existing `_scene.OnClick(...)` call site updated with the
  new argument; a new `--screenshot-multiselect <path>` mode (the
  `--screenshot-squad` precedent) priming a two-agent selection and a joint
  order for evidence.
- `docs/12_PROGRESS_LEDGER.md`, `docs/11_BACKLOG.md` (B-067 row closes fully,
  `in progress -> done`), `PROJECT_STATE.yaml`,
  `src/CommandoWar.Client.Godot/README.md` (a new dedicated section, the
  existing per-task pattern; the TASK-040 control summary near the top
  updated to mention multi-select).

## Forbidden scope

- No `CommandoWar.Sim`/`CommandoWar.Headless` change of any kind — no new
  command builder, no `Canonical.FormatVersion` bump, no `Diagnostics.fs`
  change (see Inputs and assumptions for why none is required).
- No ad-hoc/computed formation for an arbitrary selection — a group `MoveTo`
  keeps using each agent's own authored `FormationOffset` only, exactly
  TASK-067's own confirmed scope; this task must not touch
  `Appraisal.resolveFormationTarget` itself.
- No waypoint queue, no drag-to-move-a-single-agent gesture, no box-select
  modifier other than plain-drag-selects/shift-click-toggles (no ctrl/alt
  semantics, no "drag to append" without shift).
- No change to `pending`'s single-order-per-agent-at-a-time model beyond the
  dedup-matching fix named above — still "replace, not stack," just matched
  correctly against every recipient of the command about to be added.

## Required work

1. Confirm (by inspection, already done in Required reading above, re-verify
   during implementation) that `Command.moveToMany` with a one-element list
   really does produce a `PlayerCommand` identical in every field to
   `Command.moveTo`'s own output, and that no order mode besides `MoveTo`
   reads `AsGroup`/formation — both load-bearing for keeping the default
   (no modifier, single agent) click path byte-identical to today.
2. `IClientScene.fs`: add `shiftHeld` to `OnClick`; add `OnDragSelect`.
   `DemoRenderScene.fs`: mirror with no-ops.
3. `CommandDemoScene.fs`: retype `selected`; update every consumer per the
   Central decisions above; implement `OnDragSelect`; generalise `OnClick`'s
   order-dispatch to filter the selection (drop non-`Alive`/already-there
   agents), fix the pending dedup, and build either one `Command.moveToMany`
   (mode 0) or N single-recipient commands (modes 1-4) from the filtered set;
   generalise `OnHover`'s `previewPath` to per-agent, formation-aware for a
   multi-agent `MoveTo` preview.
4. `FSharpSceneHost.cs`: add drag-tracking state; restructure
   `_UnhandledInput` for press/motion/release; add the rectangle-vs-agent
   resolver and marquee draw; update every existing `OnClick` call site; add
   `--screenshot-multiselect`.
5. Verify: `dotnet build` both `.slnx`; `dotnet test` (must be unaffected —
   zero `CommandoWar.Sim` change); `-- corpus`/replay checkpoints (must be
   unaffected, same reason); `CommandDemo.tscn --selfcheck` (must stay
   `MATCH 0x2629A1FE165F94BB` — the existing scripted sequence is entirely
   single-agent, no-shift clicks, so must dispatch identically); a temporary
   `dotnet fsi` probe (removed after use, the established project precedent)
   exercising the real `IClientScene` methods (`OnDragSelect`/`OnClick`/
   `OnHover`/`StepTicksHeadless`) to prove multi-select selection, shift-
   toggle, right-click-clear, partial-dispatch-drops-the-dead-or-blocked-
   agent, and — the central point of this task — that two selected,
   differently-`FormationOffset`-authored agents ordered to one shared
   literal cell via a real client-issued group order end up on two distinct,
   formation-resolved cells (TASK-059's own "two formationed agents ordered
   to the same nominal cell resolve to distinct destinations" fact, proven
   here through the real multi-select client path for the first time, not a
   direct sim call); `--screenshot-multiselect` evidence capture.
6. Update documentation per Documentation updates below.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] A plain (no modifier) left-click on a friendly agent still selects only
      that agent, and a plain left-click on empty/enemy ground still issues
      exactly the same single-recipient command it did before this task —
      `CommandDemo.tscn --selfcheck` stays `MATCH 0x2629A1FE165F94BB`.
- [x] Shift-click toggles one agent in/out of the current selection without
      affecting any other currently-selected agent. Confirmed via probe.
- [x] A drag over empty ground selects every friendly agent whose figure
      centre falls inside the drawn rectangle; below the click/drag
      threshold it resolves as an ordinary click instead. `OnDragSelect`
      confirmed via probe (real gesture geometry lives in `FSharpSceneHost.
      cs`, not independently exercisable without a live windowed session —
      see Risks below).
- [x] Right-click clears the entire selection regardless of its size.
      Confirmed via probe.
- [x] A `MoveTo` order issued to more than one selected agent dispatches one
      `Command.moveToMany` naming every eligible (Alive, not already on the
      target cell) selected agent as `Recipients`; a `Hold`/`Assault`/
      `Withdraw`/`Suppress` order issued to more than one selected agent
      dispatches N existing single-recipient commands, one per eligible
      agent. Confirmed by inspection (`Appraisal.appraise`'s intent match)
      and by the probe's `MoveTo` case.
- [x] A dead/incapacitated/already-on-target selected agent is silently
      dropped from a dispatched order; the rest of the (eligible) selection
      still receives it; if none remain eligible, nothing is dispatched.
      Confirmed by inspection (direct reuse of TASK-053's already-tested
      `Casualty.isAlive`/`vitalsOf` pattern) — the "already on target cell"
      half of this filter turns out to be unreachable via the real click
      path both before and after this task (`friendlyAt` always resolves a
      click on an occupied cell to a selection click first, never reaching
      the dispatch branch), preserved for behavioural parity with the
      pre-existing single-agent guard, not independently re-tested live.
- [x] Two selected agents with distinct authored `FormationOffset`s, ordered
      to the same literal target cell through the real multi-select client
      path, resolve to two distinct destinations (formation-redirected),
      proven via a temporary probe against the real `IClientScene` methods.
- [x] The pending-order dedup no longer keys on `PlayerCommand.Agent` (the
      head-of-`Recipients` read) alone; a stale pending order for any
      recipient of a newly-dispatched command is removed before the new
      command(s) are added.
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless` file changed; `dotnet
      test`/`-- corpus`/replay checkpoints unaffected (same counts as
      TASK-067's own baseline).
- [x] Required documentation updated (see Documentation updates below).

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0` Warning(s), `0` Error(s)
  (unaffected — no `CommandoWar.Sim`/`CommandoWar.Headless` file touched).
- `dotnet test CommandoWar.slnx -c Release`: `415/415` unchanged.
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`:
  `19/19` unaffected, no `--regenerate` needed.
- `dotnet run --project src/CommandoWar.Headless -c Release --
  replay-file content/replays/envelope-full.cwreplay`: `checkpoints : OK
  (24 ticks match the file's committed hashes)`, unaffected.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c
  Debug`: `0` Warning(s), `0` Error(s).
- `--selfcheck` through the real Godot 4.7.2 editor: `CommandDemo.tscn`
  `MATCH 0x2629A1FE165F94BB` unchanged; `SnapshotDemo.tscn` `MATCH
  0x6213D672BC36FDB8`; `AppraisalDemo.tscn` `MATCH 0xA1354EB998FC1B95` —
  all three unaffected.
- A temporary `dotnet fsi` probe (removed after use): drove `CommandDemoScene`
  directly through `IClientScene`'s real methods. Results recorded in
  Evidence to capture below.
- `--screenshot-multiselect docs/evidence/task-068-multiselect.png` captured
  through the real Godot 4.7.2 editor (windowed, not `--headless` — the
  `_screenshotMode` precedent), committed as evidence.
- `dotnet list CommandoWar.Sim package`: `FSharp.Core` only, unaffected — no
  new dependency.

**Not verified, flagged for Dave:** the probe drives `OnDragSelect`/`OnClick`
directly (the F# `IClientScene` surface), which proves the selection and
dispatch logic correctly, but cannot exercise the raw mouse press/motion/
release sequence and `ShiftPressed`/`Rect2` geometry added to
`FSharpSceneHost.cs`'s `_UnhandledInput` — that C# input-marshalling code
only runs from real mouse events in a live windowed session. It compiles
clean against the real `GodotSharp` 4.7.2 assembly (confirmed by the client
`.slnx` build above) and mechanically mirrors the already-proven
`TryHitAgentCircle`/`TryHitOrderModeIcon`/`ScreenToCell` patterns, but an
actual click-and-drag in the editor is the only way to confirm the gesture
itself feels right and the marquee renders correctly — the TASK-060/061/064
precedent for anything needing a real interactive session this environment
cannot provide.

## Evidence to capture

- command output above, including the probe's own recorded output;
- the two real findings from scoping, both already fixed by design in this
  task rather than left for someone to hit live: the `pending` dedup bug
  (`.Agent` vs `Recipients`-membership), and the "no button-up handling at
  all" gap in `FSharpSceneHost.cs`'s `_UnhandledInput` that made a real
  click/drag disambiguation necessary rather than a small tweak;
  `--screenshot-multiselect` evidence image
  (`docs/evidence/task-068-multiselect.png`, HUD confirms
  `selected=2 agents`, two selection halos and a leader marker visible on
  the two drag-selected agents, orange pending-route dots visible).
- **Probe output (temporary `dotnet fsi`, removed after use), the central
  proof:** agent 0's authored `FormationOffset = Some {X=0;Y=0}`, agent 1's
  `Some {X=1;Y=0}`; independently computing
  `Appraisal.resolveFormationTarget` against the same loaded scenario/seed
  for a shared anchor `(6,5)` predicts agent 0 -> `(6,5)`, agent 1 ->
  `(7,5)`. Driving the real scene (`OnDragSelect` selecting both, `OnHover
  (6,5)`) produces preview-dot routes whose two distinct endpoints match
  those predictions exactly. `HudText()` read `selected=2 agents` after
  drag-select, `selected=agent 0` after a shift-click removing agent 1,
  `selected=2 agents` after shift-click re-adding it; a plain click on a
  third, unselected agent (agent 2) collapsed the selection to
  `selected=agent 2`, confirming a plain click always replaces rather than
  adds. Right-click reset to `selected=none`.
- **Honestly flagged, not smoothed over:** dispatching the joint `MoveTo`
  order and stepping to settlement (180 ticks, well past
  `Simulation.StallAbandonTicks = 40`) shows the two agents end one cell
  apart — `(5,5)` and `(6,5)` — not each at its own predicted slot
  (`(6,5)`/`(7,5)`). Their routes overlap for several cells (agent 1 must
  pass through agent 0's own target cell to reach its own), and the
  trailing agent's order stalls against the leading, still-`Alive` agent and
  is abandoned (`MovementAbandoned`, TASK-065's own mechanism) one cell
  short, permanently (confirmed unchanged through tick 180). This is the
  same live-agent chokepoint contention TASK-066/067 already found and left
  as B-067's own accepted, unresolved gap — reproduced here for the first
  time through a real client-issued group order rather than six independent
  solo ones. Not a new defect and not this task's scope to fix (see
  Forbidden scope); flagged for Dave rather than re-engineering the probe's
  target cells to hide it.
- **A pre-existing behaviour clarified, not a defect:** the "already on
  target cell" half of the partial-dispatch filter turns out to be
  unreachable via the real click path, both before and after this task —
  `friendlyAt` matches any friendly agent standing on the clicked cell
  (dead or alive) before the order-dispatch branch is ever reached, so a
  click that would land exactly on a selected agent's own current cell
  always resolves as a (re-)selection click instead. Preserved for exact
  behavioural parity with the pre-existing single-agent guard (which had
  the identical property), not removed as dead code, since removing it
  would be an unrelated cleanup outside this task's scope.

## Expected files

- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`,
  `CommandDemoScene.fs`, `DemoRenderScene.fs`.
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`.
- `docs/evidence/task-068-multiselect.png` (or similarly named).
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`,
  `src/CommandoWar.Client.Godot/README.md`.

## Documentation updates

- this task file's status and evidence;
- `docs/11_BACKLOG.md` (B-067 row: `in progress -> done`, both halves
  closed);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
- `PROJECT_STATE.yaml`;
- `src/CommandoWar.Client.Godot/README.md`: a new dedicated section (the
  existing per-task pattern) plus the TASK-040 control-summary update.

## Rollback or removal

Purely additive client-side code over an already-accepted sim mechanism —
revertible with `git revert` in one step, no canonical format, no corpus/
fixture/diagnostics re-pin to un-revert. The `pending`-dedup fix is worth
keeping independently of the rest if ever isolated (it fixes a real,
pre-existing latent bug), but is small enough not to warrant its own task.

## Review

- Reviewer: Dave
- Accepted: yes, 2026-09-21, on the self-verification evidence ("commit and
  lets move on"), in the same conversation as TASK-069 — no live editor test
  of the drag/shift-click gesture requested.
- Notes: implemented and self-verified on the same evidence pattern the
  recent sim-and-client tasks have used — headless build/test/corpus/
  replay/selfcheck all unaffected or unchanged, plus a temporary probe
  proving the new selection/dispatch/formation logic works end to end
  through the real `IClientScene` methods. Two things still need Dave's own
  live editor pass rather than being claimed from this environment, neither
  raised as a blocker at acceptance: (1) the actual mouse press/drag/release
  gesture and `Shift`/marquee rendering in `FSharpSceneHost.cs`, only
  exercisable interactively; (2) the live-agent chokepoint contention
  flagged above (two jointly-ordered formationed agents settling one cell
  apart when their routes overlap) — the same accepted gap TASK-066/067
  already left open, not new, but now visibly reachable through the feature
  this task ships.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
