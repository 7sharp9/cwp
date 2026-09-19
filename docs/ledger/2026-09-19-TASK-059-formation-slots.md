# TASK-059: Formation slots

Owner: Dave (implementing agent session)
Source revision: `main`, after TASK-057/TASK-058's acceptance (b5c72c2).
Environment: `dotnet` `10.0.303`, Windows 11. Godot client solution rebuilt
(Debug) to confirm no client change was needed; the real Godot 4.7.2 editor
was not launched (no scripted `--selfcheck` sequence authors a formation, so
no pinned hash was expected to move -- see Verification).

## Selection

Not scoped in advance. Dave asked via `AskUserQuestion` to pick the next task
from two candidates named in the session's launch context (B-043 Mibo 5.x
reconsideration spike, blocked on Dave's own interactive ADR-0001
review-trigger-1 measurement; B-011d formation slots, no blocker, Sim-only)
plus an open "something else" option. Dave chose B-011d.

## Central decisions (confirmed with Dave via `AskUserQuestion`, two rounds,
before drafting)

No squad/formation grouping or per-agent slot concept existed anywhere
(`Domain.fs`/`Scenario.fs`); "the squad" meant only "every `Friendly` agent",
and every order already targeted exactly one agent. Five forks resolved,
Dave choosing the recommended default on four and the more permissive option
on the fifth:

- **No new order type or multi-agent dispatch.** A formation changes only
  how a formationed agent's own accepted `MoveTo` target resolves, not who
  receives an order.
- **Anchor = the agent's own ordered cell**, not a designated leader's
  position. Re-resolved once at order-acceptance time, the same treatment
  `Destination` already gets for every other order type.
- **Fallback on a blocked/occupied offset cell: nearest passable, free cell
  search** (`Appraisal.bestCoverNear`'s own precedent), falling back to the
  literal anchor if nothing in radius qualifies -- never a hard order
  failure from crowding or terrain at a slot alone.
- **Both `Friendly` and `Hostile` agents may be authored into a formation**
  -- Dave's own choice, overriding the friendly-only recommended default
  every existing squad-picture/HQ precedent uses.
- **Authored per-scenario** (`RawScenario.Formations`, the TASK-058
  `Jammers`-table shape), not a hardcoded shape formula.
- **Scoped to `MoveTo` only** (an implementation-time scope decision, not put
  to Dave separately): `Hold` already redirects through `bestCoverNear`;
  `Assault`/`Withdraw` carry their own staged-FSM/resolve-bonus semantics
  (TASK-047). Composing formation-offset resolution with those is a real,
  separate design question, flagged as future work, not built here.

## Investigation before drafting

- Read `docs/05_COMMAND_AND_AGENT_AI.md` section 17 and `Perception.fs`'s own
  "a squad / formation grouping" deferred-systems note; confirmed no
  structural grouping concept exists anywhere.
- Read `docs/07_VERTICAL_SLICE.md` section 4 and `Domain.fs`'s
  `PlayerIntent`: confirmed every order (including `MoveTo`) already targets
  exactly one agent -- ruled out a group-order-dispatch design without
  needing to ask, since it would require a `PlayerIntent`/command-model
  change no backlog item asks for.
- Read `Simulation.fs` end to end around the Appraisal phase (`appraisal`,
  lines ~830-995): found the exact `Destination`-write site
  (`| Accepted, MoveTo target -> Some target`) and the parallel `fulfilled`
  check, both needing the identical resolution substituted in.
- Read `Appraisal.fs`'s `bestCoverNear` and `AppraisalConfig.
  HoldCoverSearchRadius` as the direct precedent for a bounded
  nearest-valid-cell search, and confirmed `Pathfinding.fs` has no occupancy
  concept at all (only terrain passability) -- TASK-017 reservation resolves
  same-tick contention dynamically instead.
- Read `Scenario.fs`'s `RawUnitType`/`UnitTypes` (TASK-049) and
  `RawJammer`/`Jammers` (TASK-058) resolve-and-validate patterns end to end,
  to mirror the "no silent default on a non-blank reference" validation
  shape and the "resolved scalar survives, raw string/index reference does
  not" `Deployment` shape.
- Grepped for every `RawDeployment` construction site before drafting (not
  left to the compiler): 30 occurrences across `Scenario.fs`, `Corpus.fs`,
  `DemoScenario.fs`, `TurnDemo.fs`, `PathDemo.fs`, `LosDemo.fs`, and
  `ScenarioTests.fs` -- the identical mechanical cost TASK-049's `UnitType`
  field already paid once.
- Grepped for every exhaustive `Overlay` match before writing the new case
  (the TASK-057 precedent): `DiagnosticRender.fs`'s two `Ascii`
  `sightRays`/`plannedPaths` filters plus its main overlay-line loop and
  `Svg`'s main overlay loop (all exhaustive, one arm each needed);
  `AppraisalDemo.fs`'s `unhandled` tracker (exhaustive, one arm); four
  `DiagnosticsTests.fs` sparse-filter blocks (exhaustive, one arm each); the
  Godot client's `RenderShared.fs` (already a wildcard `tryPick`, confirmed
  by building the Godot client solution, no change needed).

## Changes

- `src/CommandoWar.Sim/Domain.fs`: new `AgentState.FormationOffset: Cell
  option`, `Agent.create` defaults it to `None`; static, non-canonical (the
  `MoveSpeed` precedent).
- `src/CommandoWar.Sim/Scenario.fs`: `ScenarioContent.Version` 5 -> 6; new
  `RawFormation = { Id: string; Offsets: Cell[] }` and `RawScenario.
  Formations: RawFormation[]`; `RawDeployment` gains `FormationId: string`
  (blank = no formation) and `SlotIndex: int`; `Deployment` gains
  `FormationOffset: Cell option`; five new `ScenarioError` cases
  (`BlankFormationId`, `DuplicateFormationId`, `FormationHasNoSlots`,
  `DeploymentReferencesUnknownFormation`, `DeploymentSlotIndexOutOfRange`);
  `Scenario.validate` gains the formation-table validation block (the
  `UnitTypes` precedent) and the per-deployment reference check; `Deployment.
  FormationOffset` resolved in `toDeployments`.
- `src/CommandoWar.Sim/Appraisal.fs`: new `AppraisalConfig.
  FormationSlotSearchRadius = 2`; new public `Appraisal.
  resolveFormationTarget (terrain) (occupied: Cell[]) (offset: Cell option)
  (anchor: Cell) : Cell` (the `bestCoverNear` shape, scored by occupancy
  instead of threat pressure); `Appraisal.appraise` gains `occupied: Cell[]`
  and `formationOffset: Cell option` parameters, used only by the `MoveTo`
  case.
- `src/CommandoWar.Sim/Simulation.fs`: `World.ofScenario` carries `d.
  FormationOffset` onto the constructed agent; `Simulation.appraisal`
  computes `occupied` (every other agent's current `Position`) once per
  agent, threads it and `a.FormationOffset` into `Appraisal.appraise`, and
  substitutes `Appraisal.resolveFormationTarget ...` for the literal `target`
  in both the `fulfilled` check and the `Accepted, MoveTo target ->` write.
- `src/CommandoWar.Sim/Diagnostics.fs`: new `Overlay.AgentFormationSlot of
  agent: AgentId * at: Cell * resolved: Cell`; new private
  `formationSlotOverlays` (the `orderAppraisalOverlays` precedent, a pure
  recomputation from bare state); wired into both `frame` and `frameOf`.
- `src/CommandoWar.Sim/Canonical.fs`, `Perception.fs`: doc-comment-only
  updates recording the exclusion and correcting the stale "B-011d
  unstarted" notes.
- `src/CommandoWar.Headless/DiagnosticRender.fs`: `Ascii`'s two exhaustive
  filters gain `| AgentFormationSlot _ -> None`; a new `Ascii` line-loop arm
  (`formation slot (x,y): agent N  -> (x',y')`); a new `Svg` arm (a teal
  `#0F766E` dashed line from the agent's position to its resolved
  destination, plus a small rotated-square "diamond" marker at the
  destination -- the `AgentPendingDelivery` dashed-ring precedent, distinct
  from every existing overlay's colour/glyph).
- `src/CommandoWar.Headless/Corpus.fs`: `ScenarioAgent`/`ScenarioSpec` gain
  `FormationId`/`SlotIndex` and `Formations` fields; new `inFormation`
  helper; `rawOf`'s `deployment`/`RawScenario` construction carries them
  through; new `formationSlotsSpec` (two agents in a "wedge" formation, both
  ordered to `(5,5)`) added to `Corpus.all` as `formation-slots` (17th
  entry).
- `src/CommandoWar.Headless/DemoScenario.fs`, `LosDemo.fs`, `PathDemo.fs`,
  `TurnDemo.fs`: mechanical `FormationId = ""; SlotIndex = 0` / `Formations
  = [||]` additions to existing `RawDeployment`/`RawScenario` literals (no
  behaviour change).
- `src/CommandoWar.Headless/AppraisalDemo.fs`: one new `unhandled.Add(...)`
  arm for `AgentFormationSlot` (this disposable demo never produces one from
  its own committed frame).
- `tests/CommandoWar.Sim.Tests/ScenarioTests.fs`: mechanical literal updates
  (30 `RawDeployment` sites, `ScenarioContent.Version`/
  `UnsupportedContentVersion` literals 5 -> 6 / 6 -> 7 in three pre-existing
  facts); seven new facts (see Verification).
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`: six new facts (see
  Verification).
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`: `| AgentFormationSlot
  _` added to the four exhaustive sparse-filter blocks; one new fact (see
  Verification).
- `content/replays/formation-slots.{cwreplay,md}` (new, via `cwheadless
  corpus --regenerate`); `content/replays/CORPUS.md` (new entry row).
- `content/diagnostics/formation-slots-tick-001.{ascii.txt,svg}` (new, via a
  temporary `dotnet fsi` scratch script -- see Verification);
  `content/diagnostics/README.md` (two new file rows, the "not produced by
  `render`" list and helper-function list extended).
- `docs/04_SIMULATION_SPEC.md` section 12.5: new paragraph describing the
  formation-slot resolution.

No `Canonical.FormatVersion` bump (`AgentState.FormationOffset` is static,
excluded, the `MoveSpeed`/`Headquarters`/`Jammers` precedent); no Godot
client (`CommandoWar.Client.Godot`) source change.

## Verification

- `dotnet build src/CommandoWar.Sim/CommandoWar.Sim.fsproj -c Release`, then
  `src/CommandoWar.Headless/CommandoWar.Headless.fsproj`, then
  `CommandoWar.slnx`, then `src/CommandoWar.Client.Godot/
  CommandoWar.Client.Godot.slnx -c Debug`, checked incrementally after each
  file group's edit: all `0/0`. The Godot client solution needed zero source
  changes, confirming the Investigation grep had correctly found
  `RenderShared.fs`'s only `Overlay` match as an already-wildcard `tryPick`.
- `dotnet test CommandoWar.slnx -c Release`: `377/377` (+15 -- six
  `SimulationTests` facts: `resolveFormationTarget` with no offset/a free
  offset/an occupied exact offset/an impassable exact offset/every cell in
  radius blocked, plus one full-pipeline fact via `Command.moveToMany`
  proving two formationed agents ordered to the identical nominal cell
  resolve to two distinct `Destination`s matching their own slot offsets;
  seven `ScenarioTests` validation facts; one `DiagnosticsTests` golden fact).
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`:
  before this task's new entry existed, `ERROR formation-slots  committed
  table not found` (expected -- confirms the entry needed regeneration, not
  a silent skip); `-- corpus --regenerate` wrote only
  `formation-slots.{cwreplay,md}` (`git status --porcelain content/replays`
  confirmed no pre-existing entry's file changed); a second `-- corpus` run:
  `OK - all 17 entries match their committed tables`.
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`:
  `format 12` unchanged, confirming `Canonical.FormatVersion` did not move.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`: `FSharp.Core` only.
- The `formation-slots-tick-001.*` goldens were generated by a temporary
  `dotnet fsi` script in the session scratchpad (removed after use, the
  TASK-018/042/051 precedent), since a corpus-owned entry's diagnostic
  frames are not reachable through the `cwheadless render` verb (`render`
  only knows the shared fixture / demo / los / path scenarios) -- the
  identical situation `content/diagnostics/README.md` already documents for
  every other corpus-derived golden. The script referenced the built
  `CommandoWar.Sim.dll`/`cwheadless.dll`, called `Corpus.all |> Array.find
  (fun e -> e.Name = "formation-slots")`, ran `DiagnosticRender.runFrames`
  over its `InitialState ()`/`Commands`/`TickCount`, and wrote tick 1's
  `Ascii`/`Svg` output directly -- printing the tick-1 agent array first
  confirmed `Destination = Some (4,5)` / `Some (6,5)` before the files were
  written, not assumed. The corresponding `DiagnosticsTests.fs` fact
  (`formationSlotsFrames ()`, the `convergingRoutesFrames` precedent)
  compares fresh renderer output against the same committed files, so a
  future regeneration and its test cannot silently disagree.
- `git status --porcelain`: matches this task's allowed scope exactly (see
  Changes above); no forbidden dependency, no Godot client file, no new
  package or project.

## Deviation from the task file

None of substance. The task file's Required work item 6 left "corpus or
fixture entry" open, to decide by inspection; a new corpus entry was chosen
over a bare fixture since the acceptance criteria specifically need an
end-to-end (`Scenario.validate` -> `World.ofScenario` -> `Simulation.step`)
demonstration, which the corpus/`DiagnosticsTests.fs` machinery already
provides with a committed, regenerable golden.

## Documents updated

- `tasks/TASK-059-FORMATION-SLOTS.md` (already created before implementation
  began, per this session's own selection workflow; acceptance criteria
  checked, status updated).
- `docs/11_BACKLOG.md` (B-011d row: `proposed -> done`).
- `docs/04_SIMULATION_SPEC.md` (section 12.5, new paragraph).
- `src/CommandoWar.Sim/Perception.fs`, `Simulation.fs` (stale "B-011d
  unstarted" comments corrected).
- `content/replays/CORPUS.md`, `content/diagnostics/README.md` (new entry
  rows).
- `docs/12_PROGRESS_LEDGER.md` (this detail file; index row added; Pinned
  facts -- `ScenarioContent.Version`, green-test count -- refreshed).
- `PROJECT_STATE.yaml` (`active_work` updated).

## Review

- Reviewer: Dave.
- Accepted: yes (2026-09-19), on the self-verification evidence -- no
  client-scenario change (no formation authored in `SnapshotDemo.tscn`/
  `CommandDemo.tscn`/`AppraisalDemo.tscn`'s scripted sequences), confirmed
  going as expected. No changes requested. The Godot editor's `--selfcheck`
  hashes were not independently re-run live (flagged in the completion
  report); Dave accepted without requiring that re-check for this task.
