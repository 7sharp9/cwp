## 2026-10-22 - TASK-084 - Extracted agents leave play

**Owner:** Dave with coding-agent assistance
**Source revision:** `07b9c45` plus this change, branch `claude/cool-hypatia-j4rswr`
**Environment:** Linux cloud container, .NET SDK `10.0.112` only (pinned
`10.0.303` not installed), `-p:NuGetAudit=false`, built from a scratch copy of the
repository (`global.json` relaxed in the copy only). No xunit or FsCheck packages
in the offline cache, so `dotnet test` could not run; the test files were compiled
and run against a stand-in `Xunit` module (below). No Godot editor.
**Status change:** `proposed -> ready -> active -> review` (selected 2026-10-22)

### Origin

D-02 in `docs/15`: an `Extracted` agent stays `Alive` on its extraction cell, so a
later survivor ordered there stalls. B-083 recorded the intended semantics and
TASK-084 was drafted (`tasks/TASK-084-EXTRACTED-AGENTS-LEAVE-PLAY.md`). Dave's
selection message left Decision 4 (the refusal for an order to an extracted
agent) to the agent; see "Deviation" below.

### Baseline (step 1, before the change)

A 10 x 10 open world, one extraction cell `(5,5)`, no objectives. Agent 0 stands on
the cell and is extracted on tick 1; agent 1 at `(5,2)` is ordered to `(5,5)` after
that. Run at `07b9c45`:

| Tick | Event |
|---|---|
| 1 | agent 0 `Extracted` |
| 4 | agent 1 at `(5,4)`: `MovementObstructed` by agent 0 on `(5,5)` |
| 43 | `MovementAbandoned` |
| 62 | agent 1 still at `(5,4)`, `Destination = None` |

After the change: tick 4, `MovementCompleted` at `(5,5)` and `AgentExtracted` for agent 1.
The assumption that no shipped scenario authors `reach` or `hold` on an extraction
area was checked against `bridgehead.cwscenario` (objective 1 is on `observation`
`(4,4)`, objective 2 is a destroy, objective 3 an extract) and holds.

### Change

- `Casualty.inPlay (a: AgentState) = isAlive a.Vitals && not a.Extracted`.
  `isAlive` and its "counts as alive" callers (squad-eliminated checks,
  `currentLeader`, `ExtractAgents`) are unchanged.
- `Simulation.fs`: Navigation `intents` (an extracted agent is `Idle`),
  `occupantOf` and `parkedCells` use `inPlay`; the two Appraisal `occupied` sets
  skip `Extracted` agents (corpses still count there, Decision 6); Mission
  occupancy reads a snapshot taken before the extraction flag is set, filtered by
  `inPlay`, so the arrival tick still counts and later ticks do not; the helper is
  renamed `anyFriendlyInPlayAt`; `commandIntake` rejects an `Extracted` recipient.
- `Perception.visibleContactsFor`: an extracted observer sees nothing and an
  extracted agent is never seen.
- `Commands.fs`: `CommandRejection.RecipientExtracted of agent`, for an order and a
  cancel alike, after `UnauthorisedRecipient`. An event only: the canonical image
  and `Canonical.FormatVersion` (15) are unchanged.
- `Diagnostics.fs`, `DiagnosticRender.fs`, `AppraisalDemo.fs`: sparse
  `Overlay.AgentOutOfPlay (agent, at)` per extracted agent (ASCII line `out of
  play (x,y): agent N  extracted`; SVG dashed green square), and the
  `RecipientExtracted` event marker.
- `Corpus.fs`: the two step-off orders at tick 195 removed from
  `bridgeheadSucceededCommands` (17 orders became 15).
- Combat is not changed. `VisibleContacts` is rebuilt from `inPlay` agents every
  tick, so an extracted agent is never a shooter or a candidate; the two Combat
  edits first made were unreachable and were reverted.

### Deviation from the task text

The drafted Decision 4 offered (a) a new `DecisionReason` with a
`Canonical.FormatVersion` bump or (b) an accepted-and-inert order. Neither was
built. The format version is written into every state hash, so (a) would move
every pinned hash, every golden and the Godot pins for a case no existing state
holds, and (b) leaves a silent stall. A command-level rejection is typed, explicit,
and outside the canonical image, and follows the `UnknownAgent` and
`UnauthorisedRecipient` precedent. Dave may overrule it. The task file records the
change.

### Results

| Check | Result |
|---|---|
| `dotnet build` Sim and Headless | 0 warnings, 0 errors |
| `dotnet build` Godot Core | 0 warnings, 0 errors (`CwClientCore`; the C# host needs the Godot SDK and was not built) |
| Mibo spike | not built: package restore needs the network |
| `cwheadless corpus` before regeneration | only `bridgehead-succeeded` diverged (first bad tick 195, the removed step-off orders); the other 21 matched |
| `corpus --regenerate`, then `corpus` | `OK - all 22 entries match` |
| Stand-in run, baseline at `07b9c45` | pass=383 fail=0 |
| Stand-in run, final | pass=393 fail=0 (10 new facts) |
| Godot self-check constants, computed through Godot Core | `DemoRenderScene` `0x6213D672BC36FDB8` (tick 20) and `CommandDemoScene` `0x300BB18492CEB355` (tick 90), both unchanged |

### Corpus and golden effect

| File | Change |
|---|---|
| `content/replays/bridgehead-succeeded.{md,cwreplay}` | 15 orders instead of 17; domain events 469 -> 451; final hash `0x8B067F522EE13A80` -> `0x2166AFBBD17C7796`. `Succeeded` still fires at tick 215. First moved hash is tick 195. |
| `content/diagnostics/bridgehead-succeeded-tick-215.*` | Agents 1, 3 no longer step off, so agents 1, 2 and 5 sit on `(4,9)` and agent 3 on `(1,9)`; four `out of play` lines. Agent 5, ordered to `(1,9)`, is extracted when its route crosses `(4,9)` and stops there. |
| `content/diagnostics/demolition-success-tick-014.*` | One new `out of play (10,0): agent 0  extracted` line and the SVG square. |

The other 21 corpus entries and every other golden are byte-identical.

### Mutation check

Each changed site reverted alone in the scratch copy, full stand-in run, the
failing fact named:

| Site reverted | Failing fact (besides the two Bridgehead pins where noted) |
|---|---|
| `occupantOf` | `a second survivor can enter an extraction cell ...`, `a detour may pass through ...` (+ 2 Bridgehead pins) |
| Navigation `intents` | `an Extracted agent never moves, whatever its Destination` (+ 2 Bridgehead pins) |
| `parkedCells` | `a detour may pass through the cell of an Extracted agent ...` |
| Appraisal `occupied` | `an Extracted agent's cell does not displace a formation slot` |
| Perception observer | `an Extracted agent perceives nothing and is perceived by no enemy ...` |
| Perception target | the same fact |
| Mission occupancy (post-extraction filter, and no filter) | `an agent reaching an extraction cell still counts there on its arrival tick ...` |

### Stand-in harness

A scratch `Xunit` module (`Fact`, `Theory`, `MemberData`, the used `Assert`
members) and a reflection runner, outside the repository. It compiles
`BaselineTests`, `SimulationTests`, `RandomTests`, `CanonicalHashTests`,
`FixtureTests`, `TerrainTests`, `SightTests`, `PathfindingTests`, `ScenarioTests`,
`DiagnosticsTests` and `CorpusTests`. It does not cover `DeterminismPropertyTests`,
`ReplayTests` and `ScenarioFileTests` (FsCheck) or `BenchmarkTests`.

Tests added in `SimulationTests.fs`: the stall repaired; perception and fire both
ways; an extracted agent never moves; a lone extracted agent keeps the squad
alive and its leader; occupancy on the arrival tick and after; `RecipientExtracted`
for an order (a co-recipient still accepts) and for a cancel; formation slot not
displaced; a detour through an extracted agent's cell; determinism. Added in
`DiagnosticsTests.fs`: `AgentOutOfPlay` absent before any extraction, present on
the extraction frame, and the four tick-215 entries.

### Unresolved concerns

1. **An agent whose route crosses an extraction cell is extracted there and
   stops.** The flag was always set on touch; before, the agent kept walking while
   flagged, now it halts with its destination unreached. In `bridgehead-succeeded`
   this is agent 5. It is the stated semantics (leaving play on entry), recorded as
   `docs/15` L-08. A design that extracts only on arrival at the ordered cell would
   need a different rule and was not scoped.
2. **Inert state is kept.** An extracted agent keeps its `Order`, `Destination`,
   `Route` and `Position`, so the frame can still draw a path or a "moving"
   commitment for it (the TASK-045 corpse precedent). `currentLeader` can name an
   extracted agent. Both in L-08.
3. **The Godot client is unverified.** How it draws extracted agents sharing a
   cell, and what a player sees for `RecipientExtracted`, needs the Godot SDK
   (`docs/15` U-07). Godot Core compiles.
4. **`dotnet test` was not run.** CI is authoritative. The property 2b edit
   (`DeterminismPropertyTests.fs`, in-play agents only) was not compiled here. It
   is narrower than the old all-agents form: a live agent may already enter a
   corpse's cell (TASK-066), so the old form could in principle fail on a
   generated casualty. The expected CI count is 448 (438 pinned plus 10 new
   facts); the pinned "Green tests" fact in `docs/12` stays at 438 until CI
   confirms.
5. **A stale scenario comment.** `content/scenarios/bridgehead.cwscenario` still
   says the single extraction cell forced one-at-a-time shepherding (TASK-073).
   It is history, not behaviour, and the file was left alone.

### Documents updated

`docs/04` (state, occupancy, command rejection order, Perception, Navigation,
formation slots, Mission occupancy, section 20), `docs/05` section 5, `docs/07`
section 9, `docs/09`, `docs/15` (D-02 repaired; L-08; U-07; counts),
`content/replays/CORPUS.md`, `content/diagnostics/README.md`, the task file,
`docs/11` B-083, `docs/12` (this entry's index row), `PROJECT_STATE.yaml`.
