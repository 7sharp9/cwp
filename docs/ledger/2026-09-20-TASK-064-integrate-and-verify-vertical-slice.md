## 2026-09-20 - TASK-064 - Integrate and verify the Bridgehead vertical slice

**Owner:** Dave
**Source revision:** `main` at `cc58683` (TASK-063 accepted, 3 commits ahead of `origin/main`, working tree clean at start)
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot `4.7.2-stable_mono_win64`
**Status change:** `proposed -> review` (self-verified; one real, unresolved finding -- see below -- awaiting Dave's decision, not a clean pass)

### Changes

Realises B-035 ("Integrate and verify complete vertical slice"), unblocked
for the first time 2026-09-20 once B-025 through B-034 were all `done`
(TASK-063/B-033 accepted). Drafted and scoped via `AskUserQuestion` over
two rounds (see `PROJECT_STATE.yaml`'s own "Prior" note and the task file's
"Central decisions" for the full rationale): scope B-035 now rather than
pulling B-064 (replay playback) forward first; include real client wiring,
not a sim-side-only verification pass.

Investigation before drafting found the actual integration gap: every
vertical-slice mechanism was `done` in isolation (demolition, extraction,
mission outcome, mission summary, formation slots, order-mode HUD,
developer overlay, placeholder art, the Bridgehead map itself) but none had
ever run together against the real mission content in the real play scene
-- `CommandDemoScene.fs`'s `Ready()` had only ever loaded the small
hand-authored `DemoScenario` fixture (a 12x8 diagnostic-renderer test
vector, TASK-039), never `content/scenarios/bridgehead.cwscenario`.

- `Core/IClientScene.fs`: `Ready: unit -> unit` -> `Ready: scenarioContentPath: string -> unit`
  (a primitive, ADR-0004's crossing rule). `OnOrderModeClick`/`OrderMode`
  doc comments extended to cover the new index 4 (Suppress).
- `Core/DemoRenderScene.fs`: `Ready` call site updated to accept and ignore
  the new parameter -- still loads `DemoScenario` for its own
  diagnostic-renderer purpose, unaffected.
- `Core/CommandDemoScene.fs`:
  - `Ready(scenarioContentPath)` now does `ScenarioFile.parse
    (File.ReadAllText scenarioContentPath) >>= Scenario.validate >>=
    (fun s -> World.ofScenario s bridgeheadSeed)`, failing hard
    (`failwith`) on any error -- the `DemoScenario.initialState()`
    precedent; this is fixed, already-validated project content
    (`cwheadless import` exits 0), not user input needing graceful
    degradation. New `bridgeheadSeed` literal (no seed is authored inside a
    `.cwscenario` file itself).
  - New `enemyAt (cell: Cell) : AgentSnapshot option` (the `friendlyAt`
    precedent, `Side = Hostile`) -- `Command.suppress` takes an `AgentId`,
    not a `Cell`, so arming Suppress and clicking a cell needs to resolve
    whichever agent occupies it.
  - `OnClick`'s order-dispatch `match orderMode with` gained a `4 ->` case:
    `enemyAt cell |> Option.map (fun target -> Command.suppress ... target.Id)`.
    The whole `cmd` binding changed from `PlayerCommand` to `PlayerCommand
    option` so a Suppress click with no agent under the cursor can
    correctly leave the mode armed (no command issued) rather than issuing
    something meaningless.
  - `CommandDemoDrive.runScriptedSelfCheck` rewritten entirely (see
    "Investigation" below for how the sequence was found).
- `src/FSharpSceneHost.cs`:
  - New `ResolveContentPath(relativePath)` (the `AppraisalDemoScene.cs`
    precedent: `Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath
    ("res://"), "..", "..", "content", relativePath))`); `_Ready` resolves
    `bridgeheadScenarioPath` once and passes it to `_scene.Ready(...)`.
  - `OrderModeTextures` stays at 4 entries (Move/Hold/Assault/Withdraw); a
    new `OrderModeIconCount = 5` constant is what `TryHitOrderModeIcon`'s
    hit-test loop and `DrawOrderModeBar`'s draw loop both size themselves
    to now, so index 4 (Suppress) arms/highlights identically to the other
    four without a fifth Kenney texture asset -- `docs/07_VERTICAL_SLICE.md`
    section 6 explicitly permits presentation placeholders until the
    integration gate, and this task is that gate. `DrawSuppressIcon` draws
    a procedural orange crosshair (a hollow `DrawArc` ring plus two
    `DrawLine`s) in place of a texture.
  - `RunSelfCheck`'s `CommandDemoScene` case: label updated; the scripted
    sequence now takes `ResolveContentPath("scenarios/bridgehead.cwscenario")`;
    the expected hash re-pinned.
  - `--screenshot`/`--screenshot-mission` priming coordinates updated for
    Bridgehead's real agent starting cells ((3,5)/(2,5), not (0,0)/(0,1));
    the mission-mode block's doc comment now records that it no longer
    reaches a mission-outcome change on Bridgehead's real content (see
    below), unlike it did against `DemoScenario`'s single non-optional
    objective.

### Investigation: finding a scripted self-check sequence

A series of temporary `dotnet fsi` scripts (in the session scratchpad,
removed after use, the TASK-042/051/052/053 precedent) drove
`CommandDemoScene`/`CommandDemoDrive` directly against the built
`CommandoWar.Client.Godot.Core.dll`, using .NET reflection to read the
scene's private `state`/`currAgents`/`pending` fields for diagnosis (never
shipped, purely investigative).

Findings, in the order discovered:

1. A lone agent ordered straight onto the bridge (e.g. `(9,5)`) is
   `Accepted` outright and reaches it -- no `Refused` disposition, since no
   contact with the machine-gun team (`AgentId 100`) exists at order-issue
   time. It then takes real damage once within its own engagement range
   (empirically: safe at `(7,5)`, immediately engaged at `(8,5)` --
   distance 3 from the MG at `(11,5)`) and goes `Incapacitated`, then bleeds
   out to `Dead` over roughly 65 further ticks if left alone (a real,
   working bleed-out mechanic).
2. Ordering a non-slot-0 formation member (e.g. agent 2, offset `(0,1)`)
   toward the bridge can resolve its formation-adjusted target onto
   impassable river terrain, which `Appraisal.resolveFormationTarget`'s own
   bounded-radius fallback then resolves back onto the agent's *own current
   cell* -- `moveLike`'s `fromCell = target` short-circuit fires, so the
   order silently "completes" without the agent ever moving. Worked around
   by only ever using formation-slot-0 members (`AgentId 0`/`3`, offset
   `(0,0)`) for anything precision-critical near the river.
3. Ordering all six agents at the bridge simultaneously gives the machine
   gun more targets in range than a lone agent, and real automatic
   engagement (TASK-031/032) brings it down through `Incapacitated` to
   `Dead` while every friendly agent that actually reaches the danger zone
   can survive at partial health (confirmed repeatable: `Alive 650`, not
   `Incapacitated`, across independent re-runs with the identical click
   sequence and seed) -- the recipe used for the final self-check.
4. Two separate attempts to then complete the `destroy` objective (walk a
   clean, undamaged agent onto `(9,5)` once the MG was confirmed `Dead`)
   both still resulted in the agent taking a hit exactly on `(9,5)` --
   never on the adjacent `(8,5)`, where the same agent had already been
   sitting safely for many ticks. This strongly suggests a second threat
   (most likely one of the depot riflemen, `AgentId 101`-`104`) has a clear
   shot at the exact target cell that the machine gun's own death does not
   remove. Not root-caused further within this task's own effort budget --
   flagged for Dave, not silently dropped (see "Deviations" below).
5. A friendly agent's own corpse permanently blocks its cell: an occupied
   cell is never vacated (`Simulation.navigationAndMovement`'s vacation-
   chain rule -- a dead agent's `Intent` is always `Idle`, never `Arrived`,
   so it never frees its own `occupantOf` entry), and a dead friendly's
   cell is still `friendlyAt`-selectable (the TASK-053 status-view
   precedent), so a click there always re-selects the corpse rather than
   ever issuing a new order to move a different agent onto (or through) it.
   Confirmed directly: an attempt to re-order a survivor onto a corpse-
   occupied cell left that survivor's `Order`/`Disposition` completely
   unchanged (still showing its previous command), because the click never
   reached the order-issuing branch at all.
6. A `Failed` outcome (all six friendly eliminated) is capped well short of
   six by the bridge's own geometry: the crossing is exactly two cells wide
   in two lanes (`(8,5)`/`(9,5)` and `(8,6)`/`(9,6)`), so at most one death
   per lane's own *first* cell can ever occur before that lane is
   permanently sealed to everyone behind it (finding 5); the remaining
   agents simply stall at a safe distance-4 cell forever, never entering
   range, never scoring a hit and never taking one.

The final scripted sequence (finding 3) reaches neither `Succeeded` nor
`Failed` -- `MissionOutcome` stays `InProgress` at tick 90 -- but is a
genuine, reproducible, deterministic demonstration of real combat,
formation-slot resolution, and threat neutralisation against the actual
Bridgehead content, confirmed identical across independent re-runs and
through the real Godot editor.

### Verification

- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- import content/scenarios/bridgehead.cwscenario`
  - Result: exit 0, `map 18x12`, 6 friendly, 5 enemy, 1 objective area, 1
    extraction area, 3 objectives -- unaffected.
- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `408/408` passed, unaffected (no `CommandoWar.Sim` change).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
  - Result: `18/18` entries match their committed tables, unaffected.
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Manual check: `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path src/CommandoWar.Client.Godot -- --selfcheck`
  - Result: `MATCH` at tick 90, `0xB99E7F74EA1C3CDE` (re-pinned; real
    Bridgehead content, confirmed identical to the fsi-driven investigation).
- Manual check: `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck` (run from `src/CommandoWar.Client.Godot`)
  - Result: `MATCH` at tick 20, `0xEC8F01D781AB2122` (unaffected).
- Manual check: `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . scenes/AppraisalDemo.tscn -- --selfcheck`
  - Result: `MATCH`, `0xC382CACA830CCC35` (unaffected).
- Manual check: windowed `--screenshot` capture of `CommandDemo.tscn`.
  - Result: `docs/evidence/task-064-bridgehead-integration.png` -- real
    18x12 Bridgehead terrain, the depot buildings and crates, six friendly
    figures, the violet "observation" objective marker, the cyan
    "extraction" marker, and the five-icon order-mode HUD with the new
    Suppress crosshair icon rendering and correctly highlighted while armed.
- Manual check: `--screenshot-mission` sanity check (does not crash on real
  content; not committed as evidence since the mission does not complete
  within the capture window on Bridgehead -- see "Deviations").
- `git status --porcelain`: matches this task's allowed scope (four
  modified source files under `Core`/`src`, plus `PROJECT_STATE.yaml`,
  `docs/11_BACKLOG.md`, `docs/07_VERTICAL_SLICE.md`, this ledger entry, the
  task file, its evidence screenshot, and `README.md`).

### Evidence

- `docs/evidence/task-064-bridgehead-integration.png` (committed).
- Self-check transcripts above (reproducible by re-running the commands;
  not separately committed, the existing project convention).
- The investigation's own `dotnet fsi` probe transcripts are not committed
  (temporary, removed after use); their findings are recorded in full in
  "Investigation" above so they remain reproducible from this record alone.

### Deviations and unresolved issues

- **Criterion 3 (docs/07 section 9) is not closed on Bridgehead.** The
  canonical refusal sequence is proven end to end on a separate,
  purpose-built fixture (TASK-038); every Bridgehead `MoveTo` order tested
  in this task's investigation was `Accepted` outright, since contact with
  the machine gun only exists once an agent is already inside its own
  engagement range.
- **Criterion 7 (docs/07 section 9) is not closed on Bridgehead.** Neither
  a full `Succeeded` nor a full `Failed` run was reached. Two concrete,
  unresolved mechanisms are named above (findings 4 and 5); a third
  (finding 6) explains why `Failed` specifically is capped well short of
  total elimination by the map's own geometry. None of the three was
  root-caused to a specific fix within this task's effort budget -- this is
  the task's one real, open finding, reported for Dave's own decision (a
  Bridgehead map rebalance? more tactical investigation? accept as a known
  gap and proceed?), not smoothed over or silently marked passing.
- Finding 2 (a formationed non-slot-0 agent's target silently resolving
  onto its own current cell near impassable terrain) is a real, observed
  interaction between TASK-059's formation-offset fallback and Bridgehead's
  river geometry; it did not block this task (worked around by using
  slot-0 agents for the self-check) but is worth a look if a future task
  wants every formation slot to cross the bridge reliably.
- No live Godot-editor review from Dave yet on this task; evidence is
  entirely self-verified (headless self-checks through the real editor,
  plus one windowed screenshot capture), the TASK-063 precedent for a task
  awaiting Dave's own session with the editor.
- Accepted, already-deliberate gaps against docs/07 (not new findings):
  replay playback (B-064, explicitly deferred) and the partial stress/
  leader-trust model.

### Review round 1 (2026-09-20, live)

Dave opened the real editor and reported: "it didnt feel like i was in
control or knew what was happening, everything seemed static, no movement
from enemies etc, no idea or fire lines etc." `AskUserQuestion` narrowed
this to two facts before diagnosing further: orders did visibly register
(the HUD text changed on click), and he did try moving across/toward the
bridge -- ruling out "never explored" and pointing at something genuinely
broken rather than only the pre-existing fog-of-war/no-enemy-patrol design
reading as confusing on a first try.

Root cause, confirmed by a fresh temporary `dotnet fsi` probe (removed
after use): a `MoveTo` order for any agent other than a fireteam's own
leader (a non-zero `AgentState.FormationOffset`, TASK-059) does not target
the literal clicked cell -- `Appraisal.appraise` resolves the real
destination sim-side via `Appraisal.resolveFormationTarget`'s own
bounded-radius, nearest-passable-cell fallback, which can land well short
of the click, with zero client-side indication this happened.
`CommandDemoScene.OnHover`'s preview computed a route to the raw clicked
cell, not the real one, and the post-order HUD text said only `"accepted"`
-- identical to a literal on-target order. Confirmed exactly: ordering
agent 2 (offset `(0,1)`) to the bridge cell `(8,6)` resolves to `(7,6)` --
one full cell short of the bridge, outside the machine gun's engagement
range -- with `Disposition = Accepted` throughout; the agent genuinely
arrives there, just not where the player clicked, and nothing on screen
said so. This is the same mechanism "Investigation" finding 2 above
already named (worked around there for the self-check by only using
slot-0 agents) -- it turned out to be the actual live-session bug, not
merely a self-check-authoring inconvenience.

Fixed, client-side only, no `CommandoWar.Sim` change:

- `Core/CommandDemoScene.fs`: `OnHover`'s `previewPath` now calls
  `Appraisal.resolveFormationTarget` with the identical `occupied`/offset
  inputs `Simulation.fs`'s own appraisal phase uses, whenever the armed
  order mode is `0` (`MoveTo`) -- the preview route now shows the truth
  before the player commits to a click. `Hold`/`Assault`/`Withdraw`/
  `Suppress` are unaffected (only `MoveTo` resolves through
  `resolveFormationTarget` at all).
- The `Update`-driven order-status text (`liveOrderText`) now appends the
  agent's real, already-canonical `Destination` whenever one is active
  (`"accepted -> (7,6)"` instead of bare `"accepted"`), so a player who
  clicks without hovering first still sees exactly where the soldier is
  headed. `RenderShared.dispositionText` itself (the shared vocabulary
  function) is unchanged -- the destination suffix is composed only in
  `CommandDemoScene`'s own `liveOrderText`, the "two audiences" precedent.

#### Verification

- Manual check: a temporary `dotnet fsi` probe against the built
  `CommandoWar.Client.Godot.Core.dll` (removed after use).
  - Result: hovering agent 2 toward `(8,6)` now previews a route ending at
    `(7,6)` (matching the real sim-side resolution exactly); the fireteam
    leader (agent 0, zero offset) still previews exactly the clicked cell,
    unaffected. Driving the *real* per-frame path (`asScene.Update(dt)`,
    not the headless `--selfcheck` driver, which calls `stepOnce` directly
    and never exercises this text) showed `"accepted -> (7,6)"` on ticks
    1-4 of the order, settling back to plain `"accepted"` once the agent
    arrives and `Destination` clears.
- `dotnet build CommandoWar.slnx -c Release` / `dotnet test` / `-- corpus`:
  `0/0` / `408/408` / `18/18`, all unaffected (purely presentational).
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`:
  `0/0`.
- `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path
  src/CommandoWar.Client.Godot -- --selfcheck`: `MATCH 0xB99E7F74EA1C3CDE`
  unchanged (this fix touches `OnHover`/`Update` only, never `stepOnce`).
  `SnapshotDemo.tscn`/`AppraisalDemo.tscn` reconfirmed `MATCH`, unaffected.
- One unrelated side effect caught by `git status`/`git diff` and reverted,
  not committed: an editor-triggered spaces-to-tabs reformat of
  `tools/ExportTerrainScript.cs` plus line-ending noise in `project.godot`
  (the TASK-060/061 precedent -- opening the real editor touches files this
  task never intended to change).

Still `review`, not accepted -- awaiting Dave's next live try of this fix,
and separately, unchanged by this round, his own decision on the
criterion-7 finding from the original submission.

### Review round 2 (2026-09-20, live)

After round 1's fix, Dave confirmed it worked, then raised four further,
separate points: "you cant visually tell who is a leader, still no sense
of a fight, just blindly moving men to die, no reaction under fire, no
cover, no enemy response either." `AskUserQuestion` confirmed fixing three
now (leader marker, under-fire reaction, cover indicator) -- cheap, real,
client-only gaps -- while leaving enemy doctrine (repositioning, seeking
cover, calling for backup) as the already-existing, explicitly-descoped
backlog row B-022 (deferred since TASK-037). "No sense of a fight" is the
aggregate effect of these plus the pre-existing fog-of-war/no-tutorial
gaps already on record, not separately actionable.

Fixed, all client-side only, no `CommandoWar.Sim` change:

- `Core/CommandDemoScene.fs`:
  - New `leaderMarkerItems`, computed each `DrawList()` call from
    `Casualty.currentLeader state.Agents` (pure, over already-held
    `WorldState`) -- a green `Kind = 5` ring plus a `Kind = 3` "LEADER"
    label, both using `renderPos`, not raw `Position` (the TASK-056
    review-2 "halo jumps between cells" lesson applied identically to a
    second marker on a moving agent).
  - New `hitFlashHoldSeconds`/`heldHitFlashes` (the `heldFireLines`
    precedent exactly): the `FireLine` overlay match in `stepOnce`
    (previously discarding the `target` field as `_`) now also refreshes
    a per-agent flash timer on a landed hit; decayed in `Update` alongside
    `heldFireLines`/`heldAudioCues`. `renderVitals`'s `Alive` branch blends
    the figure's `R`/`G`/`B` toward white by the flash's remaining
    fraction -- a colour blend on the existing figure, not a new draw item.
  - New `coverColor`/`buildCoverIndicatorItems`, built once in `Ready`
    (the `buildObjectiveMarkerItems` precedent) by scanning
    `Terrain.Cover` for every cell/direction with a nonzero level and
    emitting a short `Kind = 2` line from the cell centre toward that
    cardinal direction (the isometric projection already turns a cardinal
    `Cell` offset into the correct on-screen edge), line width scaling
    with `Level`.
  - All three items folded into the existing depth-sorted `sorted` array
    (`Array.concat [...]`), not appended unsorted -- each has one
    meaningful origin cell, the `terrainItems`/`haloItems` precedent.

A suspected fourth issue -- a "grey" figure visible in the recaptured
screenshot -- was investigated with a temporary `dotnet fsi` probe
(removed after use) that replayed the exact `--screenshot` priming and
inspected the real `DrawList()` output. It is not a bug: the selection
halo (gold, `Kind = 1`, radius 34, alpha 0.35) and the selected agent's own
opaque figure (radius 20, alpha 1.0) are two correctly-ordered, separate
items at the same cell -- the pre-existing, correctly-functioning
selection halo, misread as a rendering defect on first glance.

#### Verification

- `dotnet build CommandoWar.slnx -c Release` / `dotnet test` / `-- corpus`:
  `0/0` / `408/408` / `18/18`, all unaffected.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`:
  `0/0`.
- Manual check: all three scenes' `--selfcheck` through the real Godot
  4.7.2 editor. Result: `CommandDemo.tscn` `MATCH 0xB99E7F74EA1C3CDE`
  unchanged; `SnapshotDemo.tscn`/`AppraisalDemo.tscn` reconfirmed `MATCH`,
  unaffected (all three additions are render-only or a local, non-
  authoritative timer).
- Manual check: windowed `--screenshot` recapture.
  - Result: `docs/evidence/task-064-bridgehead-integration.png` (updated)
    -- shows the green "LEADER" ring/label on agent 0 and cyan cover
    spokes on several crate/building edges across the depot.
- One recurring unrelated side effect (the same editor-triggered
  `ExportTerrainScript.cs`/`project.godot` reformat as round 1) caught by
  `git status`/`git diff` and reverted again, not committed.

Still `review`, not accepted -- awaiting Dave's next live try, and
separately, unchanged by this round, his own decision on the criterion-7
finding and on B-022 (enemy doctrine).

### Documents updated

- `tasks/TASK-064-INTEGRATE-AND-VERIFY-VERTICAL-SLICE.md` (status, outcome,
  acceptance criteria, verification, review rounds 1 and 2).
- `docs/07_VERTICAL_SLICE.md` (section 9: realisation/criterion record).
- `docs/11_BACKLOG.md` (B-035 row).
- `docs/12_PROGRESS_LEDGER.md` (this row, plus review-round rows).
- `PROJECT_STATE.yaml` (`active_work`, top-level `updated`).
- `src/CommandoWar.Client.Godot/README.md` (new section).

### Acceptance (2026-09-20)

Round 2's three legibility fixes were confirmed working by Dave's own
further live playtest of Bridgehead -- no further complaint about any of
the three; he instead reported a new, different problem (agents getting
permanently stuck), root-caused and scoped separately as B-065/
`tasks/TASK-065-STALLED-ORDER-VISIBLE-FAILURE.md`, not a defect in this
task's own work.

`AskUserQuestion` (2026-09-20) confirmed the three remaining open items:
the criterion-3/7 finding is accepted as a known, tracked gap (no
Bridgehead rebalance, no corpse-occupancy fix, in this task); B-022 (enemy
doctrine) stays descoped exactly as before; this session's full working
tree (rounds 1 and 2) is committed to `main` per Dave's explicit
instruction.

### Review

- Reviewer: Dave
- Accepted: yes, 2026-09-20. Round 1 (live) found and fixed a real bug (see
  above); round 2 (live) found and fixed three legibility gaps, confirmed
  by Dave's own further live playtest; the criterion-3/7 finding is
  accepted as a known gap (not resolved further) and B-022 stays descoped,
  both via `AskUserQuestion`.
- Notes: integration itself (real Bridgehead content loading, the Suppress
  UI gap fix, the rewritten self-check) is self-verified and complete; the
  criterion-3/7 gap is a recorded, accepted limitation, not smoothed over.
  Dave's own further playtest also surfaced a genuine, separate sim-side
  movement deadlock (B-065), selected next.
