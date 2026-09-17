# Golden diagnostic renders

Byte-for-byte reference output of the diagnostic renderers
(`src/CommandoWar.Headless/DiagnosticRender.fs`) over a `DiagnosticFrame`
(`src/CommandoWar.Sim/Diagnostics.fs`).

`tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs` compares fresh renderer output
against these files. A diff here is either an intentional renderer change (in
which case regenerate the files with the commands below and note it in the
progress ledger) or a regression.

The renderers emit `\n` line endings and no timestamps or generated ids, so the
output is stable across machines and runs. `.gitattributes` pins this directory
to `eol=lf` so the byte comparison holds on Windows too.

## Files

| File | Shows |
|---|---|
| `fixture-tick-000.ascii.txt` | The shared spike fixture (`src/CommandoWar.Headless/Fixture.fs`, 32 x 32, 6 friendly agents) at tick 0: empty terrain, agents at column x = 0, no events. |
| `fixture-tick-040.ascii.txt` | The same fixture at the final tick 40: agent 3 has reached `(20, 14)`, all agents at rest. |
| `fixture-tick-000.svg` | SVG of the fixture at tick 0. |
| `fixture-tick-040.svg` | SVG of the fixture at tick 40, with the `MovementCompleted` event ring and agent 3's (now absent) destination line. |
| `fixture-mid-route.ascii.txt` | The shared fixture at tick 25: agent 3 is at `(20, 8)`, still en route to `(20, 14)`. `Diagnostics.frameOf` emits a `PlannedPath` overlay from the agent's stored `AgentState.Route` (TASK-015); `S` = the planned-from cell `(0, 3)`, `+` = a path cell, `G` = the destination, `@` = agent 3 on the route. |
| `fixture-mid-route.svg` | SVG of the same mid-route frame: the followed path as a solid polyline with a green start disc and an orange goal box. |
| `demo.ascii.txt` | The terrain-demo scenario (`src/CommandoWar.Headless/DemoScenario.fs`, 12 x 8) at tick 0: a diagonal elevation ridge, an impassable 2 x 2 block, a movement-cost patch, an opaque wall, and three directional cover edges. Exercises every layer of the ASCII renderer. |
| `demo.svg` | SVG of the terrain-demo scenario at tick 0: hatched impassable cells, dark-bordered opaque cells, elevation shading, cover triangles, agents. |
| `demo.html` | Self-contained scrubber: one SVG per tick for the 21-frame demo run (ticks 0..20), with a slider and Prev / Next, plus a per-tick annotation panel below the SVG — a readable, agent-attributed narration of that tick's events (TASK-035, e.g. "Agent 0 fired at agent 5: hit."), a per-agent table (cell, move/destination, comms, order disposition, commitment, suppression, stress), and the two shared tactical pictures (`KnownContact` / `HostileKnownContact`). Inline CSS and JS, no external references. The moving ticks carry the `PlannedPath` overlay `Diagnostics.frameOf` emits for each agent following a route (TASK-015). |
| `los.ascii.txt` | The line-of-sight demo (`src/CommandoWar.Headless/LosDemo.fs`, 12 x 12) at tick 0 with five `--los` rays: a clear ray, a ray blocked by the opaque wall at `(5,4)`, a ray grazing the corner of the lone wall `(9,3)` (visible), a ray blocked by the ridge peak `(3,7)`, and a ray blocked by the `(9,7)`/`(8,8)` solid corner. `*` = traced cell, `x` = blocking cell. |
| `los.svg` | SVG of the same LOS demo frame: each ray a dashed line with traced-cell dots and a red cross on its blocker. |
| `path.ascii.txt` | The pathfinding demo (`src/CommandoWar.Headless/PathDemo.fs`, 16 x 12) at tick 0 with four `--path` routes: a straight clear route, a route detouring around the impassable `x = 5` wall, a route preferring a cheap detour over the `x = 10..11` movement-cost patch, and a no-path route to the walled-off pocket `(14,9)`. `+` = path cell, `S` = start, `G` = goal. |
| `path.svg` | SVG of the same pathfinding demo frame: each route a solid polyline with a green start disc and an orange goal box; the no-path route shows only its markers. |
| `converging-routes-tick-003.ascii.txt` | The `content/replays/converging-routes` corpus entry (TASK-017) at tick 3: agent 0 and agent 1 both compute `(3,3)` as their next cell; `Simulation.navigationAndMovement`'s same-tick reservation picks agent 0 (tied remaining route length, lower agent id) and agent 1 yields. `Diagnostics.frameOf` derives a `Reserved` overlay from the tick's `MovementYielded` event, shown alongside each agent's `PlannedPath`. |
| `converging-routes-tick-003.svg` | SVG of the same frame: the reserved cell as a pink dashed box labelled `R0` (the winning agent id), alongside each agent's route polyline. |
| `slow-terrain-tick-002.ascii.txt` | The `content/replays/slow-terrain` corpus entry (TASK-018) at tick 2: agent 0 has accumulated 2 of the 3 `Terrain.moveCost` needed to enter `(1,0)` and has not moved from `(0,0)` yet — the roster line shows `progress 2`, and the movement-cost note shows `(1,0)=3`. |
| `slow-terrain-tick-002.svg` | SVG of the same frame: a small progress number next to the agent's circle. |
| `swap-standoff-tick-001.ascii.txt` | The `content/replays/swap-standoff` corpus entry (TASK-022) at tick 1: agent 0 `(3,3)` and agent 1 `(4,3)` are each ordered onto the other's cell, so the two-agent swap is blocked and both freeze. `Diagnostics.frameOf` derives one `Obstructed` overlay per blocked cell (`obstructed (x,y): held by agent N`) alongside each agent's `PlannedPath`, and the events line carries two `movement-obstructed` markers. |
| `swap-standoff-tick-001.svg` | SVG of the same frame: each blocked cell as a red dashed box labelled `B<occupant id>`, alongside each agent's route polyline. |
| `perception-contact-tick-005.ascii.txt` | The `content/replays/perception-contact` corpus entry (TASK-026) at tick 5: friendly agent 0 has just cleared the opaque wall at x=6 and its line of sight to the stationary hostile agent 1 at `(9,1)` opens. The Perception phase emits `ContactObserved` (both ways — perception is symmetric) and the Tactical-knowledge phase adds the contact to `WorldState.TacticalKnowledge`, so the overlays section carries `known contact (9,1): agent 1  confidence 1000  seen tick 5` and the events line two `contact-observed` markers. Since TASK-034 the same symmetric sighting also lands in the Hostile side's own picture, one `hostile known contact (5,5): agent 0  confidence 1000  seen tick 5` line (agent 0's pre-move cell). |
| `perception-contact-tick-005.svg` | SVG of the same frame: the last-known contact cell as a purple (`#805ad5`) dashed ring labelled `?1`, over the (still-accurate) hostile agent circle, plus (TASK-034) an amber (`#b7791f`) dashed ring labelled `H0` for the Hostile side's own contact on agent 0. |
| `lost-comms-tick-001.ascii.txt` | The `content/replays/lost-comms` corpus entry (TASK-027) at tick 1: friendly agent 0 at `(1,4)` has `CommunicationAvailable = false`, so the order to `(6,4)` is accepted by command intake but dropped by the Communication phase. The roster line shows `no-comms`, the agent is still `at rest`, the overlays section carries `undelivered order (1,4): agent 0  command 1  (communication unavailable)`, and the events line carries `order-undelivered`. |
| `lost-comms-tick-001.svg` | SVG of the same frame: the blacked-out agent circled by a dashed red (`#e53e3e`) ring, and its cell marked by a red dashed box with a struck-through diagonal and an `!0` label. |
| `exposed-approach-tick-001.ascii.txt` | The `content/replays/exposed-approach` corpus entry (TASK-028) at tick 1: two friendlies ordered along the same approach past the observed hostile agent 2 at `(10,4)`. The Appraisal phase (12.5) judges the two orders against the near-identical exposure differently — the overlays section carries `order appraisal (1,3): agent 0  refused route-too-exposed threat-agent-2  exposed …` and `order appraisal (2,5): agent 1  accepted  exposed …`, and the events line carries two `order-appraised` markers. The G3 evidence: two agents appraise the same intent differently for inspectable reasons. Since TASK-034 the scenario's mutual tick-1 sighting also gives the Hostile side's own picture two `hostile known contact …` lines, one per friendly. |
| `exposed-approach-tick-001.svg` | SVG of the same frame: each agent's exposed route cells as translucent red squares, a red `R` glyph at the refused agent's cell and a green `A` at the accepted agent's, alongside agent 1's route polyline, the `?2` known-contact ring, and (TASK-034) two amber `H0` / `H1` hostile-known-contact rings. |
| `blocked-goal-tick-001.ascii.txt` | The `content/replays/blocked-goal` corpus entry (TASK-028) at tick 1: the target `(5,4)` is ringed by impassable cells, so stage 2 (`Pathfinding`) fails and the Appraisal phase refuses the order — the overlays section carries `order appraisal (1,4): agent 0  unable no-known-route` and the events line `order-appraised` (no `movement-blocked`). |
| `blocked-goal-tick-001.svg` | SVG of the same frame: a grey `U` glyph at the agent's cell (no exposed cells, no route). |
| `reissued-order-tick-003.ascii.txt` | The `content/replays/reissued-order` corpus entry (TASK-030) at tick 3: a friendly agent 0, mid-route toward `(14,4)` (ordered tick 1), receives a second order to `(14,8)`. The Communication phase resets `Disposition`, Appraisal re-accepts against the new target, and the new `commitmentAndLocalAction` phase (12.6) emits a fresh `CommitmentEstablished` — the overlays section carries `commitment (4,4): agent 0  moving to (14,8)` and the events line `commitment-established` (no `commitment-completed`: the first commitment's end is not separately reported, since `Commitment` is derived, not stored). |
| `reissued-order-tick-003.svg` | SVG of the same frame: agent 0's `PlannedPath` polyline toward the new target `(14,8)`, plus the small teal `AgentCommitment` marker (hollow, since it is `Moving`) at the top-left corner of the agent's cell. |
| `canonical-refusal-and-correction-tick-008.ascii.txt` | The `content/replays/canonical-refusal-and-correction` corpus entry (TASK-038, backlog B-023) at tick 8: `docs/07` section 8's full 8-step sequence in one run. Tick 1 (not pictured — see `exposed-approach-tick-001.*` for the identical geometry) `Refused RouteTooExposed threat-agent-2`; tick 4 the automatic threat-suppression-change reappraisal `Accepted` it once friendly 1's `Suppressing` order latched hostile 2's `SuppressionBand`; this frame is tick 8, the moment the player reissues the identical `(11,3)` intent mid-route — the overlays section carries a second `order appraisal (6,3): agent 0  accepted` and `commitment (6,3): agent 0  moving to (11,3)`, and the events line carries a second `commitment-established` for agent 0, with no event for the superseded one (the `reissued-order` precedent). Agent 0 goes on to arrive at `(11,3)` by tick 13, never refusing again. |
| `canonical-refusal-and-correction-tick-008.svg` | SVG of the same frame: agent 0's `PlannedPath` polyline resuming toward `(11,3)`, the hollow teal `AgentCommitment` marker at agent 0's cell, the solid `AgentCommitment` marker at friendly 1's `Suppressing` cell, and the ongoing fire/suppression state between friendly 1 and hostile 2. |
| `order-queue-stacking-and-cancellation-tick-001.ascii.txt` | The `content/replays/order-queue-stacking-and-cancellation` corpus entry (TASK-044, backlog B-051) at tick 1: one friendly agent is issued three stacked waypoints in a single tick — `(3,0)` `Replace` (active) then `(7,0)` and `(10,0)` both `Append` (queued behind it). The overlays section carries `order queue (1,0): agent 0  [#2 move (7,0), #3 move (10,0)]` and the events line two `order-queued` markers. |
| `order-queue-stacking-and-cancellation-tick-001.svg` | SVG of the same frame: agent 0's `PlannedPath` polyline toward the active `(3,0)` leg, plus a small `+2` badge on the cell's top edge for the two-entry `AgentOrderQueue` overlay — the one remaining unused position, since `AgentCommitment` (top-left), `AgentSuppression` (top-right), `AgentStress` (bottom-left), and `OrderAppraisal` (bottom-right) already occupy the four corners. |
| `casualties-succession-and-squad-failure-tick-005.ascii.txt` | The `content/replays/casualties-succession-and-squad-failure` corpus entry (TASK-045, backlog B-031) at tick 5: all four agents are `Incapacitated` (bleeding out), the second `LeadershipTransferred` (to `None`, every friendly down) and `SquadFailure` both fire this tick, and the overlays section carries one `vitals` line per agent plus `squad leader: none (every friendly down)`. |
| `casualties-succession-and-squad-failure-tick-005.svg` | SVG of the same frame: each agent's `AgentVitals` `Z<n>` bleed-out-countdown badge (bottom edge), no `SquadLeadership` gold ring (no leader survives). |
| `casualties-succession-and-squad-failure-tick-065.ascii.txt` | The same corpus entry at tick 65, the full lifecycle's endpoint: every agent has finished bleeding out and reads `vitals ...: agent N  dead`, rendered as a black cross over each agent's cell in the SVG. |
| `casualties-succession-and-squad-failure-tick-065.svg` | SVG of the same frame: four black crosses, no agent circles, no `SquadLeadership` ring. |

## Regeneration

From the repository root, after `dotnet build CommandoWar.slnx -c Release`:

```sh
R="dotnet run --project src/CommandoWar.Headless -c Release --"

$R render fixture --tick 0  --format ascii --out content/diagnostics/fixture-tick-000.ascii.txt
$R render fixture --tick 40 --format ascii --out content/diagnostics/fixture-tick-040.ascii.txt
$R render fixture --tick 0  --format svg   --out content/diagnostics/fixture-tick-000.svg
$R render fixture --tick 40 --format svg   --out content/diagnostics/fixture-tick-040.svg
$R render fixture --tick 25 --format ascii --out content/diagnostics/fixture-mid-route.ascii.txt
$R render fixture --tick 25 --format svg   --out content/diagnostics/fixture-mid-route.svg
$R render demo --format ascii --out content/diagnostics/demo.ascii.txt
$R render demo --format svg   --out content/diagnostics/demo.svg
$R render demo --format html  --out content/diagnostics/demo.html

LOS="--los 1,1:10,1 --los 1,4:10,4 --los 7,2:10,5 --los 1,7:6,7 --los 6,5:11,10"
$R render los $LOS --format ascii --out content/diagnostics/los.ascii.txt
$R render los $LOS --format svg   --out content/diagnostics/los.svg

PATHS="--path 1,1:4,1 --path 2,6:9,6 --path 9,3:13,3 --path 1,10:14,9"
$R render path $PATHS --format ascii --out content/diagnostics/path.ascii.txt
$R render path $PATHS --format svg   --out content/diagnostics/path.svg
```

`converging-routes-tick-003.*`, `slow-terrain-tick-002.*`,
`swap-standoff-tick-001.*`, `perception-contact-tick-005.*`,
`lost-comms-tick-001.*`, `exposed-approach-tick-001.*`,
`blocked-goal-tick-001.*`, `reissued-order-tick-003.*`,
`canonical-refusal-and-correction-tick-008.*`,
`order-queue-stacking-and-cancellation-tick-001.*`, and
`casualties-succession-and-squad-failure-tick-005.*` /
`-tick-065.*` are not produced by the
`render` verb: each entry's initial state is corpus-owned (`Corpus.all`),
not the shared fixture or demo scenario `render` knows about. All are
regenerated the same way — `DiagnosticRender.runFrames` over the named
`Corpus.all` entry and its committed `.cwreplay` (or `.cwlog`, `blocked-goal`
and `spike-fixture`), at the tick named in the file — using the exact helper
(`convergingRoutesFrames ()` / `slowTerrainFrames ()` /
`swapStandoffFrames ()` / `perceptionContactFrames ()` / `lostCommsFrames ()`
/ `exposedApproachFrames ()` / `reissuedOrderFrames ()` /
`canonicalRefusalAndCorrectionFrames ()` / `orderQueueFrames ()` /
`casualtiesFrames ()`, and an
inline `runFrames` for `blocked-goal`) the corresponding `DiagnosticsTests.fs`
fact uses to compare against these files, so a regeneration and its test
cannot silently disagree.

The `demo.html` scrubber's moving ticks (8-20) also carry the `KnownContact`
overlay: `DemoScenario` deploys one hostile agent, so once Perception runs
(TASK-026) the friendly agents observe it and it enters the shared squad
picture. `demo.ascii.txt` / `demo.svg` render tick 0 (before any perception),
so they show only terrain.

`render` also accepts a command-log path in place of `fixture` / `demo` / `los` /
`path` (replayed against the shared fixture initial state), an optional
`--layer <name>` to render a single terrain layer (`elevation`, `passability`,
`movement-cost`, `opacity`), `--tick N` to pick the frame for the `ascii` / `svg`
formats (`html` always embeds every tick), a repeatable `--los AX,AY:BX,BY` to
attach a line-of-sight ray overlay (computed via `Sight.trace` over the target's
terrain), and a repeatable `--path AX,AY:BX,BY` to attach a planned-path overlay
(computed via `Pathfinding.find` over the target's terrain). `--los` and
`--path` compose.
