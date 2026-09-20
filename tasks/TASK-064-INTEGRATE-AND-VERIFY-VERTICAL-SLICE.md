# TASK-064: Integrate and verify the Bridgehead vertical slice

Status: done (accepted by Dave 2026-09-20; two live review rounds found and
fixed real gaps -- formation-redirected orders gave no feedback, and combat
had no legible reaction/leader identity/cover signal. Round 2's fixes were
confirmed by Dave's own further live playtest, in the course of which he
separately found the stalled-order bug now scoped as B-065/TASK-065. Dave
then chose, via `AskUserQuestion`, to accept the criterion-3/7 gap as a
known, tracked limitation rather than pursue a map rebalance or corpse-
blocking fix now, and to keep B-022 (enemy doctrine) descoped as-is)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises B-035

## Acceptance (2026-09-20)

Round 2's three legibility fixes (leader marker, hit-flash, cover indicator)
were confirmed working by Dave's own subsequent live playtest of Bridgehead
-- he raised no further complaint about any of the three, and moved on to
report a new, different problem (agents getting permanently stuck), which is
the real, separate finding now scoped as `tasks/TASK-065-STALLED-ORDER-VISIBLE-FAILURE.md`
(B-065), not a defect in this task's own round-2 work.

Confirmed with Dave via `AskUserQuestion` (2026-09-20), covering the three
items still open at the end of round 2:

1. **Criterion 3/7 finding (canonical refusal and full mission
   succeed/fail not closed on Bridgehead)**: accepted as a known, tracked
   gap rather than pursued further now -- no Bridgehead map rebalance and
   no corpse-occupancy fix in this task. Recorded as such in this file's
   Acceptance criteria, in `docs/07_VERTICAL_SLICE.md` section 9, and in
   `docs/11_BACKLOG.md`'s B-035 row, not silently marked passing.
2. **B-022 (enemy doctrine, "no enemy response")**: stays exactly as
   already descoped since TASK-037/TASK-034 -- no action taken.
3. **This session's own uncommitted working tree** (round 1 and round 2's
   full diff): committed to `main`, per Dave's explicit instruction.

`active_work.selected_task` moves on to TASK-065, the one concrete,
already-scoped follow-up Dave chose to pursue next.

## Review round 2 (2026-09-20, live)

After round 1's fix, Dave tried again and reported the formation-redirect
bug was fixed, but raised four further, separate legibility gaps: "you
cant visually tell who is a leader, still no sense of a fight, just
blindly moving men to die, no reaction under fire, no cover, no enemy
response either." Confirmed via `AskUserQuestion` to fix three of these
now (leader marker, under-fire reaction, a cover indicator) -- all cheap,
real, client-only gaps -- while leaving the fourth, enemy doctrine
(repositioning, seeking cover, calling for backup), as the already-
existing, explicitly-descoped backlog row B-022 (deferred since TASK-037,
a genuinely separate AI-design initiative, not something to improvise
here). "No sense of a fight" is the aggregate effect of all of the above
plus the pre-existing fog-of-war/no-tutorial gaps already on record; not
separately actionable beyond fixing the concrete pieces.

Fixed, all client-side only, no `CommandoWar.Sim` change:

- **Leader marker.** `Casualty.currentLeader state.Agents` (the TASK-045
  succession rule -- lowest-id `Alive` `Friendly` agent) is a pure function
  over already-held state, so no sim change was needed to read it. A
  distinct green ring plus a ground-level "LEADER" text label -- shape and
  colour both, not colour alone -- follows whichever agent currently holds
  it, updating automatically the instant leadership actually transfers.
- **Under-fire reaction.** A hit target's own wound dot (small, low-
  opacity) read as no reaction at all. Added a brief hit-flash: on every
  `FireLine` overlay with `hit = true` (either side), the target's own
  figure blends toward white for `hitFlashHoldSeconds` (0.3s), fading back
  to its normal colour -- a colour blend on the existing figure, not a new
  draw item, so it never competes for depth-sort or screen space.
- **Cover indicator.** `Terrain.Cover` (directional low cover, mitigating
  hit chance and route exposure) had never been rendered anywhere --
  player-facing or developer overlay -- since it was found unpaintable
  through the current tileset back at TASK-060. A short cyan spoke from
  each covered cell's centre toward the covered direction (the isometric
  projection turns a cardinal offset into the correct on-screen edge
  automatically), thicker for a higher `Level` -- built once in `Ready`
  from already-validated `Terrain.Cover`, the `objectiveMarkerItems`
  precedent.

Verified via a temporary `dotnet fsi` probe (removed after use) replaying
the exact `--screenshot` priming sequence and inspecting the real
`DrawList()` output directly: confirmed the selection halo (gold, radius
34, alpha 0.35) and the selected agent's own opaque figure (radius 20,
alpha 1.0) are two separate, correctly-ordered items at the same cell --
what first looked like a rendering bug (a "grey" figure) in the recaptured
screenshot turned out to be the pre-existing, correctly-functioning
selection halo, not a defect.

#### Verification

- `dotnet build CommandoWar.slnx -c Release` / `dotnet test` / `-- corpus`:
  `0/0` / `408/408` / `18/18`, all unaffected (leader marker and cover
  indicator are render-only; the hit-flash tracker is a new local
  `ResizeArray` populated from an existing overlay, never touching `state`
  or the hash).
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`:
  `0/0`.
- `--selfcheck` through the real Godot 4.7.2 editor: `CommandDemo.tscn`
  `MATCH 0xB99E7F74EA1C3CDE` unchanged; `SnapshotDemo.tscn`/
  `AppraisalDemo.tscn` reconfirmed `MATCH`, unaffected.
- Windowed `--screenshot` recapture:
  `docs/evidence/task-064-bridgehead-integration.png` (updated) -- shows
  the green "LEADER" ring/label on agent 0, and cyan cover spokes on
  several crate/building edges across the depot.
- One recurring unrelated side effect (the same editor-triggered
  `ExportTerrainScript.cs`/`project.godot` reformat as round 1) caught and
  reverted again, not committed.

Still `review`, not accepted -- awaiting Dave's next live try, and
separately, unchanged by this round, his own decision on the criterion-7
finding and on B-022 (enemy doctrine, already deferred, not touched here).

## Review round 1 (2026-09-20, live)

Dave tried the real editor and reported: "it didnt feel like i was in
control or knew what was happening, everything seemed static, no movement
from enemies etc, no idea or fire lines etc." Confirmed via `AskUserQuestion`
that orders did visibly register (the HUD text changed) and that he did try
moving across/toward the bridge -- ruling out "never got close enough" and
pointing at something actually broken, not just the pre-existing fog-of-war/
no-enemy-patrol design (TASK-051, backlog B-055) reading as confusing on a
first try.

**Root cause, confirmed by a fresh `dotnet fsi` probe (removed after use):**
a `MoveTo` order for any agent other than a fireteam's own leader (a
non-zero `AgentState.FormationOffset`, TASK-059) does not target the
literal clicked cell -- `Appraisal.appraise` resolves the real destination
sim-side via `Appraisal.resolveFormationTarget`, which can land far short
of the click (its own bounded-radius, nearest-passable-cell fallback) with
**zero client-side indication this happened**. `CommandDemoScene`'s hover
preview (`OnHover`) computed a route to the raw clicked cell, not the real
one, and the post-order HUD text said only `"accepted"`, identical to a
literal on-target order. Confirmed exactly: ordering agent 2 (offset
`(0,1)`) to the bridge cell `(8,6)` actually resolves to `(7,6)` -- one full
cell short of the bridge, well outside the machine gun's engagement range
-- with `Disposition = Accepted` throughout. The agent genuinely walks
there and arrives; it just isn't where the player clicked, and nothing on
screen said so. This fully explains the report: an order that "registers"
but produces no visible movement into the danger zone, no contact, no
combat, no fire lines.

**Fixed, client-side only, no `CommandoWar.Sim` change:**

- `OnHover`'s `previewPath` now resolves the same way `Appraisal.appraise`
  does for a `MoveTo` order (`orderMode = 0`) -- calling
  `Appraisal.resolveFormationTarget` with the identical `occupied`/offset
  inputs `Simulation.fs`'s own appraisal phase uses -- so the hover route
  shown to the player is the truth before they ever click. `Hold`/`Assault`/
  `Withdraw`/`Suppress` are unaffected (only `MoveTo` resolves through
  `resolveFormationTarget` at all, `Appraisal.appraise`'s own `Intent`
  match).
- The order-status HUD text now appends the agent's real, already-canonical
  `Destination` whenever one is active (`"accepted -> (7,6)"` instead of
  bare `"accepted"`), so even a player who clicks without hovering first
  still sees exactly where their soldier is headed.

Verified directly via a temporary `dotnet fsi` probe (removed after use):
hovering agent 2 toward `(8,6)` now previews a route ending at `(7,6)`
(confirmed against the real sim-side resolution); the fireteam leader
(agent 0, zero offset) still previews exactly the clicked cell, unaffected.
The post-order HUD text reads `"accepted -> (7,6)"` for ticks 1-4 of the
same order, then settles back to plain `"accepted"` once the agent arrives
(`Destination` clears) -- confirmed via `asScene.Update(dt)`, the real
per-frame path, not the headless `--selfcheck` driver (which calls
`stepOnce` directly and never exercises this text at all).

`dotnet build` both `.slnx` `0/0`; `dotnet test` `408/408` (unaffected,
purely presentational); `-- corpus` `18/18` (unaffected); `CommandDemo.tscn
--selfcheck` reconfirmed `MATCH 0xB99E7F74EA1C3CDE` unchanged (this fix
touches `OnHover`/`Update` only, never `stepOnce`); `SnapshotDemo.tscn`/
`AppraisalDemo.tscn` reconfirmed unaffected. One unrelated side effect (an
editor-triggered spaces-to-tabs reformat of `ExportTerrainScript.cs` plus
line-ending noise in `project.godot`, the TASK-060/061 precedent) was
caught by `git status`/`git diff` and reverted, not committed.

This does not touch the separate, larger open finding from the original
submission (criterion 7 -- a full `Succeeded`/`Failed` run on Bridgehead)
-- that remains open, unrelated to this bug, and still awaits Dave's own
decision.

## Outcome (2026-09-20)

**Integration is complete and self-verified; verification surfaced one real,
unresolved finding rather than a clean pass.** `CommandDemoScene.fs`'s
`Ready()` now loads `content/scenarios/bridgehead.cwscenario` for real
(`ScenarioFile.parse` -> `Scenario.validate` -> `World.ofScenario`, the
`DemoScenario.fs` pipeline shape, a new `bridgeheadSeed` literal) instead of
the small hand-authored `DemoScenario` fixture -- the first time any task has
run the actual docs/07 mission content in the actual play scene.
`IClientScene.Ready` gained a `scenarioContentPath: string` parameter
(`DemoRenderScene`'s own `Ready` ignores it, still loading `DemoScenario`);
`FSharpSceneHost.cs` resolves the absolute path via
`ProjectSettings.GlobalizePath` (the `AppraisalDemoScene.cs` precedent).

A real, material gap was found and fixed as part of this task, confirmed
with Dave via `AskUserQuestion`: **`Suppress` had no client UI at all** --
`OnOrderModeClick` only ever had indices `0..3` (Move/Hold/Assault/
Withdraw); B-059's own backlog text said outright "No UI added for Suppress
... not part of Dave's task selection." Suppress is one of docs/07 section
4's five required commands and the mechanism section 1's whole product
question rests on. Added index `4` (a procedural crosshair icon --
`docs/07` section 6 permits presentation placeholders until the integration
gate, and this task is that gate -- rather than a fifth Kenney texture
asset); arming it and clicking a cell now resolves to whichever agent (any
side) occupies that cell (a new `enemyAt` helper, the `friendlyAt`
precedent) and issues `Command.suppress` against it, `Appraisal.appraise`
itself still gating on the target being a real known contact
(`Unable(TargetNotKnown)` otherwise) exactly as it already did sim-side
(TASK-037).

`CommandDemoDrive.runScriptedSelfCheck` rewritten entirely against real
Bridgehead coordinates: all six friendly agents ordered toward the bridge at
once (exercising all six formation-slot resolutions, TASK-059, against real
terrain for the first time). Investigated live via a series of temporary
`dotnet fsi` probes against this exact scene/content (removed after use,
the TASK-042/051/052/053 precedent) before settling on this sequence:
sending every agent at once gives the machine-gun team (`AgentId 100`) more
simultaneous targets than a lone agent, so real automatic engagement brings
it down to `Dead` (through `Incapacitated`/bleed-out) while every friendly
agent stays `Alive` -- a genuine, reproducible, casualty-free neutralisation
of the scenario's central threat, through the real click path, confirmed
deterministic (`0xB99E7F74EA1C3CDE` at tick 90, re-confirmed by re-running
the probe and, separately, through the real Godot 4.7.2 editor). The
optional `reach observation` objective (`ObjectiveId 1`) also completes for
free.

**What this investigation could not close, and is flagged for Dave rather
than silently dropped:** despite extensive investigation, a full `Succeeded`
run (destroy + extract) was not reduced to a reliable scripted sequence. Two
real, concrete findings came out of trying:

1. The `bridge-charge` target cell (9,5) itself appears to be covered by a
   threat beyond the machine gun (most likely one of the depot riflemen,
   `AgentId 101`-`104`) -- an agent who reaches (8,5) safely and waits there
   takes no further damage once the MG is dead, but stepping onto (9,5)
   itself drew fire in every attempt, including after the MG was confirmed
   `Dead`.
2. A friendly agent's own corpse can permanently block a cell: an occupied
   cell is never vacated (`Simulation.navigationAndMovement`'s vacation-
   chain rule), and a dead friendly's own cell is still `friendlyAt`-
   selectable (TASK-053's status-view precedent), so clicking it always
   re-selects the corpse for status view rather than ever issuing a new
   order elsewhere -- meaning a scout who dies exactly on an objective or
   target cell can make that objective permanently unreachable, not just
   temporarily harder.

A full `Failed` run (all six friendly eliminated) turned out to be
geometrically capped well short of six: the bridge's own two-cell-wide,
two-lane chokepoint means at most two agents (one per lane's own first
cell) can ever actually die attempting the crossing before their own
corpses block that lane for everyone behind them, and the other four simply
stall safely at a distance of 4 from the machine gun forever, out of range,
never scoring a kill and never dying themselves.

**Criterion 3 (the canonical refusal sequence) and criterion 7 (the mission
can succeed and fail without developer intervention) are the two docs/07
section 9 criteria this task could not demonstrate on Bridgehead itself.**
See "Acceptance criteria" below for the full criterion-by-criterion record.
This is reported as a real, unresolved finding for Dave's own decision (map
rebalance? more time on tactics? accept as a known gap?), not smoothed over.

`dotnet build` both `.slnx` (main Release; Godot client Debug) `0/0`;
`dotnet test` `408/408` (unaffected, no `CommandoWar.Sim` change); `--
corpus` `18/18` (unaffected); `-- import content/scenarios/
bridgehead.cwscenario` still exits 0 (unaffected). `CommandDemo.tscn`
re-pinned and confirmed `MATCH 0xB99E7F74EA1C3CDE` through the real Godot
4.7.2 editor; `SnapshotDemo.tscn`/`AppraisalDemo.tscn` reconfirmed unchanged
(`MATCH 0xEC8F01D781AB2122` / `MATCH 0xC382CACA830CCC35`). Screenshot
evidence: `docs/evidence/task-064-bridgehead-integration.png` (real terrain,
six agents, the objective/extraction markers, the new Suppress icon armed
and highlighted in the HUD bar).

Full detail: `docs/ledger/2026-09-20-TASK-064-integrate-and-verify-vertical-slice.md`.

## Objective

Make the real Bridgehead scenario (`content/scenarios/bridgehead.cwscenario`)
actually playable end to end in `CommandDemoScene` -- today it always loads
the small hand-authored `DemoScenario` fixture, never Bridgehead -- and then
check the running result against `docs/07_VERTICAL_SLICE.md` section 9's 12
functional acceptance criteria, recording direct evidence for each: met,
met-with-caveat, or a known accepted gap.

## Why this task exists

B-025 through B-034 are all `done` for the first time (TASK-063/B-033
accepted 2026-09-20), which unblocks B-035 ("Integrate and verify complete
vertical slice", size L, `docs/11_BACKLOG.md` line 132) for the first time
this project has reached that point. B-035 has no task file yet and its own
wording is vague.

Investigation before drafting found the real integration gap `docs/07`
implies is still open: every mechanical piece the vertical slice needs is
now built and `done` (demolition/extraction/mission outcome, formation
slots, the mission-summary panel, order-mode HUD, developer overlay,
casualties/succession, placeholder art, the Bridgehead greybox map and its
`.cwscenario` content) but nothing has ever connected them into one playable
run: `CommandDemoScene.fs`'s `Ready()` (line 475) is hardcoded to
`DemoScenario.initialState()` -- a 12x8 fixture with a single optional
`ReachArea` objective, authored to exercise diagnostic renderers (TASK-039),
never Bridgehead. Every "mission summary" / "objective marker" / "demolition"
task so far proved its own mechanism in isolation (a headless corpus entry,
or `DemoScenario`'s own placeholder objective); none has ever been run
against the actual docs/07 mission content in the actual play scene.

## Central decisions (confirmed with Dave 2026-09-20 via `AskUserQuestion`, two rounds)

1. **Scope B-035 now**, without pulling B-064 (replay playback) forward
   first. B-064 was already deliberately split out of the old bundled B-033
   (TASK-063) as sharing no design surface with mission summary; docs/07
   section 6 lists "replay playback" as required presentation, so its
   absence is a known, already-deliberate gap for this task to record, not
   resolve.
2. **Include the client wiring**, not a sim-side-only verification pass:
   `CommandDemoScene` is changed to load `bridgehead.cwscenario` (via the
   existing `ScenarioFile.parse` -> `Scenario.validate` -> `World.ofScenario`
   pipeline `DemoScenario.fs` already demonstrates end to end) so the mission
   can actually be played, not just imported and inspected headlessly.
3. **Known gaps against docs/07 are recorded as accepted, not blocking**:
   replay playback (B-064) and the partial stress/leader-trust model (both
   already-deliberate prior decisions, not new discoveries) do not prevent
   this task from declaring G4 reached for the criteria that are in scope.

## Required reading

- `docs/07_VERTICAL_SLICE.md` in full -- section 9 (12 acceptance criteria)
  is this task's own checklist; section 3 (scenario/objective sequence),
  section 4 (required commands), section 6 (required presentation, including
  the two accepted gaps), and section 8 (canonical refusal sequence, already
  realised end to end by TASK-038's `canonical-refusal-and-correction`
  corpus entry against a *different*, purpose-built fixture -- this task
  does not need to re-derive that sequence, only confirm the same mechanism
  holds on Bridgehead's actual geometry where practical).
- `docs/11_BACKLOG.md` rows B-025 through B-034 (what each already proved,
  and where) and B-064 (why it is deliberately out of scope here).
- `content/scenarios/bridgehead.cwscenario` -- the real content: 18x12 map,
  one river/bridge chokepoint, 6 friendly (fireteam-alpha 0/1/2, fireteam-
  bravo 3/4/5), 5 enemy (100 = MG team at the bridge exit, 101-104
  riflemen), `objective-area observation` (4,4), `extraction-area
  extraction` (1,9), `target bridge-charge` (9,5), three `Objectives`:
  optional `reach observation` (id 1), non-optional `destroy bridge-charge`
  10-tick plant (id 2), non-optional `extract` (id 3).
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` (`Ready()` line
  475; `CommandDemoDrive`'s scripted self-check, wherever it lives in this
  file/its host -- the exact sequence this task must rewrite against real
  Bridgehead coordinates instead of `DemoScenario`'s).
- `src/CommandoWar.Client.Godot/Core/IClientScene.fs` (`Ready: unit -> unit`
  -- this task adds a parameter; read the file's own doc comment describing
  ADR-0004's primitives-only crossing rule before choosing its shape).
- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` -- the per-concern table;
  confirm which side resolves a file path (Godot/C#) versus which side
  parses scenario text (framework-neutral F#, already true of
  `ScenarioFile.parse`).
- `src/CommandoWar.Client.Godot/src/AppraisalDemoScene.cs` (lines ~66-67)
  and `tools/ExportTerrainScript.cs`/`ImportTerrainScript.cs` -- the existing,
  already-used precedent for resolving `content/` at runtime from a Godot
  script: `ProjectSettings.GlobalizePath("res://")` then
  `Path.Combine(projectRoot, "..", "..", "content", ...)`. `AppraisalDemoScene.cs`
  is the closer precedent -- a real runtime scene reading `content/replays`
  this same way, not just an editor tool.
- `src/CommandoWar.Headless/DemoScenario.fs` -- the exact
  parse/validate/`World.ofScenario` shape to reuse, and its own doc comment
  on why it needs no on-disk format (it isn't real content; Bridgehead is).
- `src/CommandoWar.Sim/ScenarioFile.fs`, `Scenario.fs` (`validate`),
  `Simulation.fs` (`World.ofScenario`) -- signatures only, no expected
  change.
- `tasks/TASK-061-BRIDGEHEAD-GREYBOX-MAP.md`, `TASK-062-DEMOLITION-...md`,
  `TASK-063-MISSION-SUMMARY-SCREEN.md` and their ledger entries -- what each
  already verified about this exact content, so this task does not
  re-litigate settled ground (e.g. terrain paints correctly, demolition/
  extraction/mission-outcome logic is correct, the objective/extraction
  markers and summary panel render) and instead focuses on the first true
  end-to-end run.
- `PROJECT_STATE.yaml`, `AGENTS.md`.

## Dependencies

- B-025 (TASK-061), B-026 through B-034 (all `done`). No other task
  selected. Not dependent on B-064 (deliberately, see Central decisions).

## Inputs and assumptions

- `DemoScenario` stays exactly as it is and stays the world `DemoRenderScene`
  (`SnapshotDemo.tscn`) and `AppraisalDemoScene.cs` (`AppraisalDemo.tscn`)
  load -- this task changes only `CommandDemoScene`'s own source. Confirmed
  by inspection: no other scene type references `CommandDemoScene`'s
  `Ready()` or its scripted self-check.
- A file-path parameter is the natural shape for `IClientScene.Ready`
  (`Ready: scenarioPath: string -> unit`), with the C# host resolving the
  absolute path via `ProjectSettings.GlobalizePath` (the
  `AppraisalDemoScene.cs` precedent) and F# doing
  `File.ReadAllText >> ScenarioFile.parse >> Scenario.validate >>
  World.ofScenario seed` -- parse/validate/world-build stay framework-
  neutral F#; only the path resolution is Godot-side, per ADR-0004. This is
  an implementation-time design choice within the "primitives only" rule,
  not put to Dave separately, documented once made.
- A parse, validation, or world-build failure on `bridgehead.cwscenario` at
  scene `Ready()` fails hard (`failwith`, the `DemoScenario.initialState()`
  precedent) rather than falling back silently to `DemoScenario` -- this is
  fixed, already-validated (`cwheadless import` exits 0) project content,
  not user-supplied input requiring graceful degradation.
- A new deterministic seed literal is needed for
  `World.ofScenario scenario seed` (no seed is authored in `.cwscenario`
  files themselves, confirmed by inspection -- `DemoScenario.Seed` is
  supplied by its own caller the same way). Value and name are an
  implementation-time choice, documented once made, not put to Dave.
- `CommandDemoDrive`'s existing scripted `--selfcheck` sequence is entirely
  `DemoScenario`-specific (agent 0 to (4,4), etc.) and must be rewritten
  against Bridgehead's real agent ids/coordinates to prove a genuine
  `MissionOutcome -> Succeeded` (or a deliberately chosen `Failed`) run on
  the real content -- this is expected to be the largest single piece of
  work in this task, not a small edit.
- docs/07 section 3's six-step objective narrative does not map one-to-one
  onto authored `Objective` rows: only `reach` (optional), `destroy`, and
  `extract` are actual `WorldState.Objectives` entries; "neutralise, bypass,
  or suppress the machine-gun threat" (step 2) and "hold the position while
  the charge is planted" (step 4, folded into `destroy`'s own `HoldTicks`)
  are emergent from tactical play and the existing `Suppress`/`Hold` orders,
  not separate objective content. This is expected, not a content gap to
  fix.
- The camera/viewport in `FSharpSceneHost.cs` has no hardcoded map-size
  assumption (confirmed by inspection -- no camera/zoom logic exists at all,
  only a fixed per-cell pixel scale from the origin), so Bridgehead's larger
  18x12 map is expected to render, though possibly partly off the default
  window without scrolling -- note as a follow-up if it actually impedes
  play, not something to pre-solve architecturally here.

## Allowed scope

- `src/CommandoWar.Client.Godot/Core/IClientScene.fs`: `Ready` gains a
  `scenarioPath: string` parameter (or equivalent primitive); update
  `DemoRenderScene`'s and `AppraisalDemoScene`'s call sites/no-op
  implementations only as needed to keep building -- no behaviour change to
  either.
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: `Ready()`
  reimplemented to parse/validate/build `WorldState` from the given path
  instead of `DemoScenario.initialState()`; the scripted self-check sequence
  rewritten against real Bridgehead content; anything downstream that
  assumed `DemoScenario`'s specific bounds/agents (e.g. the comment at line
  164-165 admitting exactly this coupling) corrected.
- `src/FSharpSceneHost.cs`: the `_Ready`/scene-construction call site
  resolves `content/scenarios/bridgehead.cwscenario`'s absolute path (the
  `AppraisalDemoScene.cs` precedent) and passes it to `CommandDemoScene`'s
  `Ready`.
- `scenes/CommandDemo.tscn`: re-pinned `--selfcheck` hash if the scripted
  sequence's tick count or content changes (expected).
- `docs/07_VERTICAL_SLICE.md`: section 9 gains "Realised by TASK-064" prose
  under each of the 12 criteria (the section 8 precedent already used
  throughout this file), citing this task's evidence and any prior task
  that already proved a criterion elsewhere; explicitly names the two
  accepted gaps (replay playback / B-064; partial stress/trust) rather than
  silently marking every criterion "done".
- New `docs/evidence/` screenshots of a real Bridgehead playthrough
  (selection, order-issuing, suppression/combat, the demolition plant, the
  mission-summary panel on real content).
- `src/CommandoWar.Client.Godot/README.md` (new section, the TASK-040/048/
  063 precedent).
- `docs/11_BACKLOG.md` (B-035 row), `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- No `CommandoWar.Sim` behaviour change unless a real, previously-unfound
  blocking gap is discovered while actually playing Bridgehead end to end
  (the TASK-063 objective-marker precedent: fix a genuine blocker found live,
  do not invent new features). Any such fix must be called out explicitly,
  not folded in silently.
- No B-064 work: no replay playback, no `.cwreplay` scrubbing UI, no new
  `cwheadless` verb.
- No change to `DemoScenario.fs`, `DemoRenderScene.fs`,
  `AppraisalDemoScene.cs`'s own scenario source, or their `--selfcheck`
  hashes -- only `CommandDemoScene`'s source scenario changes.
- No hand-tweaking Bridgehead's map geometry (a separate, explicitly
  deferred open item) unless something is found broken during play.
- No new client project, package, or dependency.
- No formal five-participant external playtest (`docs/07` section 11) --
  that is G5/B-036's job, gated on this task, not this task's own work.
- No rewriting docs/07's requirements themselves, only annotating section 9
  with realisation evidence, the file's own established pattern.
- Nothing under `src/_scratch/`, `bench/`, `content/benchmarks/BASELINE.md`.

## Required work

1. Confirm `bridgehead.cwscenario`'s exact validated content
   (`cwheadless import content/scenarios/bridgehead.cwscenario`) matches
   this task's own "Inputs and assumptions" before changing any client code.
2. Design and implement the scenario-load path: `IClientScene.Ready`'s new
   parameter, the C# host's path resolution, `CommandDemoScene`'s
   parse/validate/build-and-fail-hard pipeline.
3. Rewrite `CommandDemoDrive`'s scripted self-check to drive a real
   Bridgehead playthrough far enough to reach a genuine
   `MissionOutcome <> InProgress` (ideally `Succeeded`, exercising
   `destroy`/`extract`/`AllOf` label formatting live for the first time --
   see TASK-063's own flagged gap); re-pin `CommandDemo.tscn`'s
   `--selfcheck` hash through the real Godot 4.7.2 editor.
4. Play the mission live (or via a temporary scripted/probe run, the
   established precedent, removed after use) far enough to check each of
   docs/07 section 9's 12 criteria directly: cite existing evidence where a
   criterion is already proven elsewhere (e.g. criterion 9's replay/hash
   determinism, already proven by the replay/corpus machinery in general),
   and capture new evidence where only a real Bridgehead run can prove it
   (e.g. criterion 7 "mission can succeed and fail without developer
   intervention", criterion 12 "placeholder-art play is understandable").
5. Record each criterion's status (met / met-with-caveat / accepted gap) in
   this task file and in `docs/07_VERTICAL_SLICE.md` section 9.
6. Update `README.md`, `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`,
   `PROJECT_STATE.yaml`.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

Mirrors `docs/07_VERTICAL_SLICE.md` section 9, each with direct evidence:

- [x] 1. Every required command can be issued through `CommandDemoScene`'s
      own HUD against real Bridgehead content: `MoveTo`/`Hold`/`Assault`/
      `Withdraw` already had a HUD path (TASK-048); `Suppress` gained one
      this task (order-mode index 4). All six agents' `MoveTo` orders were
      exercised live in the self-check; `Suppress`'s Accepted/Unable
      pathway was exercised directly (see criterion 4). Not demonstrated:
      `Assault`/`Withdraw` specifically against Bridgehead content in this
      task's own run (both already proven against `DemoScenario`,
      TASK-048).
- [x] 2. At least two soldiers can appraise the same order differently for
      traceable reasons -- already proven by the `exposed-approach` corpus
      entry (TASK-028); not newly re-demonstrated on Bridgehead specifically
      in this task (every Bridgehead `MoveTo` order observed during
      investigation was Accepted regardless of exposure -- see criterion 3).
- [x] 3. **Accepted known gap, not closed on Bridgehead** (Dave,
      `AskUserQuestion`, 2026-09-20). The canonical refusal sequence
      (docs/07 section 8) is proven end to end on a separate, purpose-built
      fixture (TASK-038's `canonical-refusal-and-correction` corpus entry),
      not on Bridgehead. This task's own investigation found that ordering
      directly across the bridge is Accepted outright every time, since no
      contact with the machine gun exists until an agent is already inside
      its own engagement range (confirmed empirically: safe at (7,5),
      immediately engaged at (8,5), no intermediate "spotted but not yet
      fired on" cell exists on this map) -- Bridgehead's specific geometry
      may not naturally produce a `Refused` disposition the way docs/07
      section 8 narrates. Flagged for Dave, not resolved.
- [x] 4. A player action predictably changes measurable state: `Suppress`'s
      real effect (raising and latching `AgentState.SuppressionBand`, then
      decaying once the threat leaves combat) was directly observed and
      measured against the real machine-gun team during investigation
      (`Suppression` rose to `900`/`SuppressionBand = true` from real
      combat, decayed back to `0` over ~25 ticks afterward). Not
      demonstrated: the specific "suppress unblocks a `Refused` order"
      causal chain from docs/07 section 8, since nothing was ever refused
      on Bridgehead in this task's testing (see criterion 3).
- [x] 5. Enemy decisions use perceived/reported information, not
      authoritative player positions -- a general, pre-existing engine
      property; nothing in Bridgehead's own content defeats it (regression
      confirmation only).
- [x] 6. Leader death transfers command per the existing explicit rule --
      already proven generally by TASK-045; not separately re-forced live
      on Bridgehead in this task (not practical to trigger deliberately
      within the self-check's scope).
- [x] 7. **Accepted known gap, not closed on Bridgehead** (Dave,
      `AskUserQuestion`, 2026-09-20). The mission was not driven to either
      `Succeeded` or `Failed` on real content despite extensive
      investigation (a series of `dotnet fsi` probes, removed after use).
      Two concrete findings, both flagged for Dave in "Outcome" above: the
      `bridge-charge` target cell (9,5) appears covered by a threat beyond
      the machine gun; a friendly corpse can permanently block a cell,
      including making it unclickable-toward. A full `Failed` (all six
      eliminated) also turned out to be geometrically capped by the
      bridge's own two-lane chokepoint at roughly two casualties. This is
      the task's one real, unresolved finding.
- [x] 8. `cwheadless import content/scenarios/bridgehead.cwscenario` still
      exits 0 with the same validated summary (regression confirmed; no
      content was made invalid by this task).
- [x] 9. A recorded command stream reproduces the same final authoritative
      state for a real Bridgehead run under the stated determinism
      contract: the rewritten `--selfcheck` sequence reproduces
      `0xB99E7F74EA1C3CDE` at tick 90 deterministically, confirmed by
      independent re-runs and through the real Godot 4.7.2 editor.
- [x] 10. `cwheadless import` executes against Bridgehead repeatedly
      without a graphical client (regression confirmed). `cwheadless` has
      no verb to *play* a `.cwscenario` file's mission headlessly end to
      end -- a pre-existing gap (flagged since TASK-061), not created or
      closed by this task.
- [x] 11. The developer overlay (`F1`) is scene-generic and unaffected by
      the content-source change; confirmed still functional via the
      committed screenshot's HUD (regression confirmation).
- [x] 12. Placeholder-art play is understandable on the real Bridgehead
      map specifically (18x12, the depot buildings, six friendly figures,
      the machine-gun/rifleman enemy layout, the real objective/extraction
      markers) -- newly confirmed via `docs/evidence/
      task-064-bridgehead-integration.png`, not previously exercised on
      this content.
- [x] Accepted gaps against docs/07 sections 6/9 (replay playback/B-064;
      partial stress/leader-trust) are named explicitly, not silently
      marked complete (see Central decisions and this criterion list).
- [x] No forbidden dependency or scope entered the change (`git status
      --porcelain` matches Allowed scope; no `CommandoWar.Sim` change).
- [x] Required documentation updated: `docs/07_VERTICAL_SLICE.md` section 9
      annotations, `docs/11_BACKLOG.md` B-035 row, `docs/12_PROGRESS_LEDGER.md`
      index row + `docs/ledger/2026-09-20-TASK-064-*.md` detail file,
      `PROJECT_STATE.yaml`, and `src/CommandoWar.Client.Godot/README.md`.

## Required verification

- `dotnet run --project src/CommandoWar.Headless -c Release -- import content/scenarios/bridgehead.cwscenario`:
  exit 0, `id bridgehead`, `map 18x12`, 6 friendly, 5 enemy, 1 objective
  area, 1 extraction area, 3 objectives -- unaffected.
- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet test CommandoWar.slnx -c Release`: `408/408`, unaffected.
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`:
  `18/18`, unaffected.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`:
  `0/0`.
- `--selfcheck` through the real Godot 4.7.2 editor
  (`Godot_v4.7.2-stable_mono_win64_console.exe --headless --path
  src/CommandoWar.Client.Godot -- --selfcheck`, and the scene-specific
  `scenes/SnapshotDemo.tscn` / `scenes/AppraisalDemo.tscn` forms):
  `CommandDemo.tscn` `MATCH 0xB99E7F74EA1C3CDE` at tick 90 (re-pinned, real
  Bridgehead content); `SnapshotDemo.tscn` `MATCH 0xEC8F01D781AB2122`
  (unaffected); `AppraisalDemo.tscn` `MATCH 0xC382CACA830CCC35`
  (unaffected).
- Windowed `--screenshot` evidence:
  `docs/evidence/task-064-bridgehead-integration.png` -- real Bridgehead
  terrain/depot/agents, the objective/extraction markers, and the new
  Suppress HUD icon armed and highlighted.
- Extensive manual/scripted investigation into a full `Succeeded`/`Failed`
  run via a series of temporary `dotnet fsi` probes against the built
  `CommandoWar.Client.Godot.Core.dll` (removed after use): confirmed real
  combat, contact establishment, suppression rise/latch/decay, and MG
  neutralisation; did not close criterion 7 (see Outcome/Acceptance
  criteria).
- `git status --porcelain`: matches this task's allowed scope.

## Evidence to capture

- `dotnet build`/`dotnet test`/`corpus`/`import` command output (above);
- `docs/evidence/task-064-bridgehead-integration.png`;
- re-pinned `CommandDemo.tscn` `--selfcheck` hash `0xB99E7F74EA1C3CDE`;
- the section 9 criterion-by-criterion record (this file's own Acceptance
  criteria section, mirrored into `docs/07_VERTICAL_SLICE.md` section 9);
- the two unresolved findings (target-cell threat beyond the MG; corpse
  cell-blocking) and the chokepoint-capped-casualties observation, all
  recorded in Outcome above -- not silently dropped.

## Expected files

- `src/CommandoWar.Client.Godot/Core/IClientScene.fs` -- `Ready` gains
  `scenarioContentPath`; `OnOrderModeClick`/`OrderMode` doc comments cover
  index 4.
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` -- real scenario
  load in `Ready`; new `enemyAt`/`bridgeheadSeed`; `OnClick`'s order
  dispatch gains the Suppress case; `CommandDemoDrive.runScriptedSelfCheck`
  rewritten.
- `src/CommandoWar.Client.Godot/Core/DemoRenderScene.fs` -- `Ready` call-site
  only (ignores the new parameter, still loads `DemoScenario`).
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs` -- path resolution,
  the `Ready`/self-check call sites, the fifth procedural Suppress icon in
  `DrawOrderModeBar`, updated screenshot-priming coordinates.
- `docs/evidence/task-064-bridgehead-integration.png` (new).
- `tasks/TASK-064-INTEGRATE-AND-VERIFY-VERTICAL-SLICE.md` (this file).
- `docs/07_VERTICAL_SLICE.md` (section 9 realisation annotations).
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.
- `src/CommandoWar.Client.Godot/README.md` (new section, the TASK-040/048/
  063 precedent).
- Not touched, contrary to earlier expectation: `scenes/CommandDemo.tscn`
  (no `.tscn`-level change was needed -- `FSharpSceneHost.cs`'s existing
  generic dispatch already covers the new path);
  `src/CommandoWar.Client.Godot/src/AppraisalDemoScene.cs` (a separate
  C# scene, not an `IClientScene` implementer, unaffected).

## Documentation updates

- this task file's status and evidence;
- `docs/07_VERTICAL_SLICE.md` section 9 (realisation prose per criterion);
- `docs/11_BACKLOG.md` (B-035 row);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
- `PROJECT_STATE.yaml`.

## Rollback or removal

Client-side only, confirmed: a new `Ready` parameter, the C# host's path
resolution, a rewritten scene-load body, a fifth procedural HUD icon, and a
rewritten self-check script. No `CommandoWar.Sim` change was needed.
Revertible with `git revert` in one step.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave
- Accepted: yes, 2026-09-20 -- round 1 (live) found and fixed a real bug
  (formation-redirected `MoveTo` orders gave no feedback at all); round 2
  (live) found and fixed three legibility gaps (leader marker, under-fire
  reaction, cover indicator), confirmed working by Dave's own further live
  playtest. Via `AskUserQuestion`, Dave accepted the criterion-3/7 finding
  as a known gap and kept B-022 (enemy doctrine) descoped as-is.
- Notes: full detail in
  `docs/ledger/2026-09-20-TASK-064-integrate-and-verify-vertical-slice.md`.
  Dave's own further playtest also surfaced a separate, genuine sim-side
  movement deadlock, scoped as B-065/`tasks/TASK-065-STALLED-ORDER-VISIBLE-FAILURE.md`,
  selected next.
