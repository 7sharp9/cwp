# TASK-063: Mission summary panel in CommandDemoScene

Status: done (drafted 2026-09-20; implemented and self-verified 2026-09-20;
one live review round found and fixed a missing objective-area marker;
accepted by Dave 2026-09-20, "ok it works")
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises narrowed backlog row
B-033

## Outcome (2026-09-20)

Implemented as drafted. `IClientScene.MissionSummaryLines: unit -> string[]`
added (`DemoRenderScene` no-ops to `[||]`); `RenderShared.
missionSummaryLines` derives the panel's lines directly from `WorldState`
(outcome headline, completed objectives, in-progress objective status,
extracted agents), with a private `objectiveLabel`/`objectiveIdOf` pair
deriving a plain-language label from each `Objective`'s own `AreaId`/
`TargetId` (no authored display name exists) -- `objectiveIdOf` duplicates
`Simulation.mission`'s own private id-extraction recursion rather than
widening its visibility across the assembly boundary, the `reasonText`/
`devReasonText` "two audiences" precedent already in that file.
`CommandDemoScene.stepOnce` sets the existing tactical-`paused` field once
`MissionOutcome <> InProgress`; `OnClick`'s order-issuing guard gained
`&& state.MissionOutcome = InProgress` so a manual un-pause cannot reopen
order-issuing. `FSharpSceneHost.DrawMissionSummaryPanel` draws a fixed
backing panel plus lines last in `_Draw` whenever non-empty; a new
`--screenshot-mission <path>` mode (left unpaused, longer capture
threshold) was needed for evidence since the existing `--screenshot`
priming deliberately re-pauses to hold a pending-order preview instead.
`CommandDemoDrive.runScriptedSelfCheck` extended (20 -> 40 ticks) to send
agent 0 on to `DemoScenario`'s sole authored objective at `(4,4)` after its
existing sequence, reaching a real `MissionOutcome = Succeeded`; re-pinned
hash `0xED5437A8773C92B2`.

Two real, pre-existing bugs found and fixed during verification (both
predate this task, see the ledger detail for how each was isolated before
fixing): `SnapshotDemo.tscn`/`AppraisalDemo.tscn`'s own `--selfcheck` pins
had gone stale since TASK-062's `Canonical.FormatVersion` 12 -> 13 bump
(re-pinned `0xEC8F01D781AB2122`/`0xC382CACA830CCC35`); and the panel's
first draft anchored its `DrawString` text box to the screen centre instead
of the panel's own left edge, rendering every line a half-panel-width off
to the right (fixed, re-verified against a recaptured screenshot).

`dotnet build` both `.slnx` (main Release; Godot client Debug) `0/0`;
`dotnet test` `408/408` (unaffected, no `CommandoWar.Sim` change); `--
corpus` `18/18` (unaffected); all three scenes' `--selfcheck` confirmed
`MATCH` through the real Godot 4.7.2 editor; a temporary probe (removed
after use) confirmed the panel's actual line content
(`["MISSION SUCCESS"; "completed: reach ridge-top"]`) and that no order is
queued once the mission has ended (`nextCommandId` unchanged across a
post-mission click attempt); committed screenshot
`docs/evidence/task-063-mission-summary.png`.

Not exercised live, flagged for Dave: `HoldArea`/`DestroyTarget`/
`ExtractAgents`/`AllOf`/`Optional` objective-label formatting and the
extracted-agent line -- `DemoScenario` authors only a `ReachArea`
objective, so no in-scope scripted sequence reaches those branches. They
are exhaustively matched (F# compiler-enforced) and mechanically identical
in shape to the verified case, but their exact wording is confirmed by
code inspection only.

**Review round 1 (2026-09-20, live):** Dave tried it in the real editor and
could not trigger the panel -- selected agent 0, moved it near the
ridge-top area, and after 1141 ticks still had `order=no order`, `running`,
no panel. Root cause: `WorldState.ObjectiveAreas`/`.ExtractionAreas` are
never rendered anywhere in `CommandDemoScene` at all (confirmed by
inspection -- the only references to those fields in the whole client are
in the terrain-*authoring* export/import tool, not any play scene), so the
objective cell `(4,4)` looks like any other patch of terrain; Dave had
almost certainly clicked adjacent to it rather than exactly on it, since
there was nothing to click precisely toward. Confirmed via `AskUserQuestion`
to fix now rather than file separately, since the panel this task adds is
otherwise unreachable without the `F1` debug overlay. Added a new
always-on (not `F1`-gated) marker for each `WorldState.ObjectiveAreas`/
`.ExtractionAreas` cell in `CommandDemoScene` -- a `Kind = 5` ring (the
`cellRing` precedent) plus its `AreaId` as a text label, built once in
`Ready` since both arrays are static authored content read directly from
`WorldState` (the `state.Terrain`/`.Bounds` precedent), included in the
existing depth-sorted draw list. The first colour choice (gold) turned out
to be visually identical to the selection halo's own near-gold ring
exactly where it matters most (a selected agent standing on the objective
cell) -- caught from the recaptured screenshot, changed to violet before
finalising; extraction markers use a distinct cyan. Purely additive
rendering, no `IClientScene` interface change, no interaction/gating
change: all three scenes' `--selfcheck` hashes reconfirmed `MATCH`
unchanged. Screenshot re-captured showing both markers.

Full detail: `docs/ledger/2026-09-20-TASK-063-mission-summary-screen.md`.

## Objective

When `WorldState.MissionOutcome` (TASK-062, backlog B-032) leaves
`InProgress`, show the player a mission-summary panel in `CommandDemoScene`
-- outcome, completed objectives, in-progress objective status, and which
agents extracted -- and stop the mission from accepting further player
input. This is the first Godot client task to build any post-mission
presentation; today `MissionOutcome` exists only as sim-side state and a
developer-only `Overlay.MissionStatus` diagnostic.

## Why this task exists

B-032 (accepted 2026-09-20, TASK-062) made `MissionOutcome`/
`CompletedObjectives`/`ObjectiveProgress`/`AgentState.Extracted` real, but
explicitly scoped out any client presentation of them ("No client (Godot) UI
names or renders mission outcome anywhere yet ... a mission-complete/failure
client screen is docs/07 section 6's own listed future presentation work,
not this task"). `docs/07_VERTICAL_SLICE.md` section 6 and
`docs/06_CONTENT_AND_PRESENTATION.md` section 10 both list a mission
success/failure summary as required presentation, never designed further.

Backlog row B-033 originally bundled this with replay playback ("Implement
replay playback and mission summary", depends on B-012 and B-032). Selected
via `AskUserQuestion` (2026-09-20): B-032's acceptance unblocked B-033 for
the first time, but research before drafting found the two halves have no
shared design or implementation surface -- replay playback has no prior
client art at all (`.cwreplay` is commands-only; full per-tick state is only
ever reconstructed by `Replay.run`, not stored; the only existing
"scrubber" is `content/diagnostics/demo.html`'s static generated-HTML
slider, not reusable inside a live Godot scene) and mission summary has
complete sim-side data but zero UI/scene-transition infrastructure of its
own. Dave chose to split B-033 into two backlog rows and build mission
summary first (narrowing B-033 to it; replay playback becomes new row
B-064, deferred, not designed here).

## Central decisions (confirmed with Dave 2026-09-20 via `AskUserQuestion`, four rounds)

1. **Split B-033** into two backlog rows rather than building both, or
   either alone under the original bundled row.
2. **Mission summary first**; replay playback deferred to new backlog
   row B-064.
3. **Real Godot client UI**, not a headless/`cwheadless render`-only proof
   (the TASK-057 precedent) -- this is deliberately the first task to build
   post-mission presentation, per B-033's own backlog wording and docs/07
   section 6.
4. **Host scene and trigger: `CommandDemoScene`, auto-shown the tick
   `MissionOutcome` first leaves `InProgress`**, pausing further player
   input -- not a new dedicated scene/scene-transition (out of scope, see
   Forbidden scope).
5. **Panel content (multiSelect, all four chosen)**: outcome
   (Succeeded/Failed), the completed-objectives list, the extracted-agents
   list, and in-progress objective status (useful context on a `Failed`
   outcome -- how close the mission got).

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` -- primitives-only interop,
  "Render loop | C#" row
- `src/CommandoWar.Sim/Domain.fs`: `MissionOutcome`, `WorldState.
  MissionOutcome`/`.CompletedObjectives`/`.ObjectiveProgress`,
  `AgentState.Extracted`, `Objective`/`ObjectiveId`/`AreaId`/`TargetId`
  (`ObjectiveId.value`/`AreaId.value`/`TargetId.value` accessors -- these are
  private wrapper types, not raw ints/strings) and `WorldState.Objectives`
  (the static authored array, needed to describe what each `ObjectiveId`
  actually is: `ReachArea`/`HoldArea`/`DestroyTarget`/`ExtractAgents`/
  `AllOf`/`Optional`)
- `src/CommandoWar.Sim/Events.fs`: `AgentExtracted`/`ObjectiveCompleted`/
  `MissionSucceeded`/`MissionFailed` (for context; this task reads
  `WorldState` snapshots directly, not an accumulated event log -- see
  Allowed scope)
- `src/CommandoWar.Sim/Diagnostics.fs`: `Overlay.MissionStatus` (the
  existing sparse developer-only summary -- read its doc comment and the
  `missionOverlay` builder for the exact emission rule; this task's
  own panel-data method is the player-facing sibling of the same
  underlying fields, not a wrapper around this developer overlay)
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`,
  `Core/CommandDemoScene.fs` (`stepOnce`, `OnClick`'s order-issuing guard
  pattern -- e.g. the existing `Casualty.isAlive` gate -- and `vitalsOf`'s
  "read supplementary state once per tick" precedent)
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs` (`_Draw`,
  `DrawOrderModeBar` -- the fixed-screen-space HUD-chrome precedent this
  panel follows; `BuildHud`)
- `content/scenarios/bridgehead.cwscenario` (the one real scenario
  authoring `destroy`/`extract` objectives today; `DemoScenario.fs` in
  `CommandoWar.Headless` authors none, so `CommandDemoScene`'s own
  `DemoScenario.initialState()` never reaches a non-`InProgress` outcome --
  see Required work for how this is actually exercised/proven)

## Dependencies

- B-032 (done, TASK-062). No other task selected. (B-012, replay corpus
  infrastructure, is not a dependency of this narrower task -- it only
  mattered for the replay-playback half now split into B-064.)

## Inputs and assumptions

- `AgentSnapshot` does not carry `Extracted` and is not extended to (see
  Forbidden scope) -- the panel reads `WorldState.Agents[].Extracted`
  directly from the scene's already-held `state: WorldState`, the same
  category of direct-state read `CommandDemoScene.fs` already performs for
  static content (`state.Terrain`, `state.Bounds`), not a new authoritative
  computation.
- `Objective`'s own cases carry no free-text name -- a display label is
  derived from its `AreaId`/`TargetId` string value(s) and DU case (e.g.
  "destroy bridge-charge", "extract via extraction"), not authored content.
  `AllOf`/`Optional` wrap other objectives recursively; the label function
  must handle nesting, at minimum by flattening to the leaf objectives it
  contains.
- `DemoScenario.fs` (used by both `DemoRenderScene` and, today, as the
  initial state `CommandDemoScene` loads) authors no `Objectives`, so this
  panel is unreachable through the scene's current default content. Proving
  it end-to-end needs either loading `bridgehead.cwscenario` content into
  `CommandDemoScene` for a scripted self-check/screenshot run, or a small
  scripted objective-bearing world built the way `DemoScenario`/`TurnDemo`
  already are in `CommandoWar.Headless` -- an implementation-time choice,
  not put to Dave, documented once made.

## Allowed scope

- `Core/IClientScene.fs`: one new member, e.g.
  `abstract MissionSummaryLines: unit -> string[]` (empty array = mission
  still `InProgress`, nothing to show; non-empty = the exact lines to
  render, first line the outcome headline). `DemoRenderScene` gains a
  trivial `[||]` implementation (the `OnOrderModeClick`/`OrderMode` no-op
  precedent).
- `Core/CommandDemoScene.fs`: the summary-line derivation from `state.
  MissionOutcome`/`.CompletedObjectives`/`.ObjectiveProgress`/`.Objectives`/
  `.Agents` (for `Extracted`); gating `stepOnce`/`OnClick`'s order-issuing
  branch once `MissionOutcome <> InProgress` (reusing or extending the
  existing `paused` mechanism, or a new dedicated flag -- implementation's
  call, documented); `CommandDemoDrive.runScriptedSelfCheck`'s scripted
  sequence extended only if needed to reach a non-`InProgress` outcome for
  self-check evidence.
- `src/FSharpSceneHost.cs`: a new fixed-screen-space panel draw call (the
  `DrawOrderModeBar` precedent -- a backing rect plus `DrawString` lines),
  called from `_Draw` only when `_scene.MissionSummaryLines()` is
  non-empty; re-pinned `CommandDemoScene` `--selfcheck` hash if the
  scripted sequence changes; `--screenshot` priming extended if needed to
  capture the panel as evidence.
- New `src/CommandoWar.Headless` content only if needed to give
  `CommandDemoScene` an objective-bearing scenario to reach a real outcome
  with (e.g. a small dedicated fixture alongside `DemoScenario`/`TurnDemo`,
  or loading `bridgehead.cwscenario` -- implementation's call).
- `docs/evidence/task-063-mission-summary.png` (new committed screenshot).
- `src/CommandoWar.Client.Godot/README.md` (new section, the TASK-040/048
  precedent).
- `docs/11_BACKLOG.md` (B-033 row narrowed; already-added B-064 row cross-
  referenced), `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Forbidden scope

- No `CommandoWar.Sim` change. `MissionOutcome`/`CompletedObjectives`/
  `ObjectiveProgress`/`Extracted` already carry everything this task needs;
  no new canonical field, no `Canonical.FormatVersion` bump.
- No new `AgentSnapshot` field (`Extracted` is read from `WorldState`
  directly, not added to the snapshot type -- see Inputs and assumptions).
- No replay playback, no `.cwreplay` scrubbing UI, no new `cwheadless`
  verb -- that is the deferred B-064, not this task.
- No new dedicated results scene or scene-transition infrastructure --
  the panel is an overlay drawn on top of the existing `CommandDemoScene`,
  per Dave's confirmed choice.
- No "play again" / restart / return-to-menu control. The panel is
  read-only presentation; resetting a finished mission is unscoped.
- No change to `SnapshotDemo.tscn`/`AppraisalDemo.tscn` behaviour or their
  own `--selfcheck` hashes.
- Nothing under `src/_scratch/`, `bench/`, `content/benchmarks/BASELINE.md`.

## Required work

1. Inspect `Objective`/`WorldState` fields and `Overlay.MissionStatus` to
   confirm the exact data available; decide how `CommandDemoScene` reaches
   a real non-`InProgress` outcome for self-check/screenshot evidence.
2. Add `IClientScene.MissionSummaryLines`; implement in `CommandDemoScene`
   (line derivation, including a plain-language objective label from
   `AreaId`/`TargetId`) and as a no-op in `DemoRenderScene`.
3. Gate further order-issuing and (if chosen) tick advancement once
   `MissionOutcome <> InProgress`.
4. Implement the panel draw call in `FSharpSceneHost.cs`, drawn last
   (on top of everything, the `DrawOrderModeBar` precedent) whenever
   `MissionSummaryLines()` is non-empty.
5. Verify: `dotnet build` both `.slnx`; `dotnet test` unaffected; all three
   scenes' `--selfcheck` through the real Godot 4.7.2 editor
   (`CommandDemoScene` re-pinned if its scripted sequence changed,
   `SnapshotDemo.tscn`/`AppraisalDemo.tscn` reconfirmed unchanged); a
   windowed `--screenshot` showing the panel for both a `Succeeded` and,
   separately inspected if practical, a `Failed` run.
6. Update `README.md`, `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`,
   `PROJECT_STATE.yaml`.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] The instant `WorldState.MissionOutcome` leaves `InProgress` in
      `CommandDemoScene`, a panel appears showing the outcome
      (Succeeded/Failed), the completed-objectives list, in-progress
      objective status, and the extracted-agents list. (Verified live for
      `Succeeded` + one completed objective; `HoldArea`/`DestroyTarget`/
      `ExtractAgents`/in-progress/extracted lines are inspection-only, not
      exercised by any in-scope scripted sequence -- see Outcome.)
- [x] No further order can be issued once the mission has ended (a click
      that would otherwise issue a command is a no-op). Verified directly
      (`nextCommandId` unchanged across a post-mission click attempt).
- [x] `DemoRenderScene`/`SnapshotDemo.tscn`/`AppraisalDemoScene` unaffected
      (no mission-outcome concept in their scenarios; behaviour and
      `--selfcheck` hashes unchanged from a *correct* baseline -- their
      committed pins were found stale from TASK-062 and re-pinned as part
      of this task's own verification, see Outcome).
- [x] `CommandDemoScene`'s scripted `--selfcheck` reproduces its
      (re-)pinned hash deterministically through the real Godot editor.
- [x] `--screenshot` evidence committed showing the panel with real
      outcome/objective/extraction content, not placeholder text.
- [x] No `CommandoWar.Sim`/`CommandoWar.Headless` behaviour change (values
      only); `dotnet test` unaffected.
- [x] Required documentation updated, including B-033's narrowed backlog
      row.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`, unaffected.
- `dotnet test`: `408/408`, unaffected.
- `dotnet run --project src/CommandoWar.Headless -- corpus`: `18/18`,
  unaffected.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`: `0/0`.
- `--selfcheck` for all three scene types through the real Godot 4.7.2
  editor: `CommandDemo.tscn` `MATCH 0xED5437A8773C92B2` (tick 40, re-pinned);
  `SnapshotDemo.tscn` `MATCH 0xEC8F01D781AB2122` (tick 20, re-pinned --
  found stale from TASK-062, unrelated to this task's own behaviour);
  `AppraisalDemo.tscn` `MATCH 0xC382CACA830CCC35` (re-pinned, same cause).
- Windowed `--screenshot-mission` evidence of the panel:
  `docs/evidence/task-063-mission-summary.png`.
- `git status --porcelain`: matches this task's allowed scope.

## Evidence to capture

- command output or test summary;
- `docs/evidence/task-063-mission-summary.png`;
- re-pinned `--selfcheck` hash(es) if the scripted sequence changed;
- unresolved failures.

## Expected files

- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`
- possibly a new small fixture under `src/CommandoWar.Headless/` (only if
  needed to reach a real outcome for evidence)
- `docs/evidence/task-063-mission-summary.png`
- `src/CommandoWar.Client.Godot/README.md`
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`

## Documentation updates

- this task file's status and evidence;
- `docs/11_BACKLOG.md` (B-033 row narrowed to mission summary only);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
- `PROJECT_STATE.yaml`.

## Rollback or removal

Additive and entirely client-side: one new `IClientScene` member with a
no-op default elsewhere, new panel-drawing code in the existing C# host,
and (if added) one new small headless fixture. No `CommandoWar.Sim`/
`CommandoWar.Headless` engine change. Revertible with `git revert` in one
step.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-20, "ok it works", after round 1's objective-marker
  fix).
- Notes: round 1 found a real gap (no visual marker for objective/
  extraction areas, making the panel practically unreachable); fixed in the
  same session, re-verified, and accepted on the next live try.
