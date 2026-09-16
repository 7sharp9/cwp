# TASK-039: Snapshot rendering and isometric depth ordering

Status: done (drafted 2026-09-16; scenario-source decision confirmed with
Dave via AskUserQuestion before drafting; implemented and self-verified
2026-09-16 through the real Godot 4.7.2 editor; accepted by Dave 2026-09-16)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete) — the first real P4 client task
Size: L (backlog lists B-027 as M; corrected here — this is also the first
task to stand up ADR-0004's F# client-core architecture, not only render a
snapshot; see "Size correction" below)

## Outcome (2026-09-16)

Implemented as drafted. New `src/CommandoWar.Client.Godot/Core/CommandoWar.Client.Godot.Core.fsproj`
(F#, no `GodotSharp` reference — nothing marshals a Godot type this task):
`CwClientCore.IClientScene` (`Ready`/`Update`/`DrawList`/`HudText`/`Dispose`),
`DrawItem` and `TickHash` `[<CLIMutable>]` records (primitives only, the
ADR-0004 interop idiom — an early draft used an F# tuple for `TickHash` and
was corrected to a record after the C# build failed on it), and
`DemoRenderScene` (owns the fixed-step accumulator, the `MainNode.cs`
constants; steps `DemoScenario` via `Simulation.step`/`RenderSnapshot`;
builds a depth-sorted `DrawItem[]` each frame from cached terrain items +
interpolated agent positions). New generic `src/FSharpSceneHost.cs`
(ADR-0004 form 1): resolves the named `IClientScene` by reflection over
loaded assemblies, forwards `_Ready`/`_Process`/`_Draw`/`_ExitTree`, does the
isometric cell<->screen projection and issues `Draw*` calls (the arithmetic
ADR-0004 explicitly allows in the C# shim). New `scenes/SnapshotDemo.tscn`,
not set as `run/main_scene` (`AppraisalDemo.tscn` unchanged).

Verified for real through the Godot 4.7.2 editor (confirmed available in
this environment — no "flagged for Dave" deferral, unlike every prior
Godot-touching task this project): headless `--editor --headless --quit`
import, `dotnet build` both `.slnx` files, `--selfcheck` (prints
`DemoScenario`'s real tick 1-20 hash sequence, `MATCH` against the pinned
tick-20 hash `0x11B06E6EDE0C52E3`, exit 0 — byte-identical to the same
sequence computed by a `dotnet test` scratch probe outside Godot, a genuine
cross-runtime determinism cross-check), and a windowed `--screenshot` run
(`docs/evidence/task-039-snapshot-rendering.png`, committed) showing the
elevation ridge, the impassable block, and the opaque wall correctly tinted
and outlined, with two friendly and one hostile agent mid-route.

`dotnet build CommandoWar.slnx -c Release`: unaffected, `0/0` (the new
project isn't referenced by it). `dotnet test`: unaffected, `297/297`
(nothing in `CommandoWar.Sim`/`CommandoWar.Headless` changed). `dotnet build
src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`: `0/0`.

`decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` gains a dated note: the
scaffold now exists for real; its own review trigger 1 (an F# breakpoint hit
from an interactive editor/F5-launched run) is explicitly **not** discharged
by this task (everything here ran headless/CLI or one windowed
non-interactive capture) — still needs Dave's own interactive editor
session. `src/CommandoWar.Client.Godot/README.md` gains a new section and
an updated file-layout table. `docs/11_BACKLOG.md` B-027 row moved to
`done`.

Full detail: `docs/ledger/2026-09-16-TASK-039-snapshot-rendering-and-depth-ordering.md`.

## Objective

Render a live-stepping simulation in Godot for the first time (TASK-029's
demo only replays a static corpus entry) via a real `RenderSnapshot`
(`src/CommandoWar.Sim/Snapshot.fs`, already implemented) consumed by a
depth-sorted isometric renderer, and in doing so stand up ADR-0004's
production F# client-core architecture for the first time.

## Why this task exists

B-027's dependencies (B-011, TASK-006) are both `done`, so this row is
selectable per `docs/11_BACKLOG.md` section 7. G3 is now passed (see the
2026-09-16 G3 gate decision); this is the first task of P4. Put to Dave via
`AskUserQuestion`: of the P4 items now selectable (B-026, B-027, B-030
proper, B-031), Dave chose B-027 — build something to see before building
input on top of it, and it is the real Godot editor-authoring work that
resolves ADR-0001 review trigger 1 (unmeasured since acceptance: "no Godot
client work has progressed" beyond the disposable spike and the read-only
TASK-029 demo, neither of which is real editor-authoring work).

## Size correction

`docs/11`'s B-027 row is `M`. In practice this task also has to do work no
prior P3/P4 task has: stand up ADR-0004's "thin C# host over an F#
client-core library" for the first time (`CommandoWar.Client.Godot.Core`, a
generic `FSharpSceneHost`, the `IClientScene` interface). Every P4 client
task after this one (B-026, B-028, B-029 proper) reuses this scaffold rather
than repeating the cost. Flagged here rather than silently absorbed.

## Central decision (confirmed with Dave 2026-09-16 before drafting)

The real Bridgehead map (B-025) isn't built; the content-importer path
(B-024) is blocked on B-043 (Mibo reconsideration spike). Three render
sources were possible: the terrain-demo scenario, a multi-agent corpus
entry, or the existing `Greybox.tscn` authored map + `GreyboxScene.cs`
content-import reader (TASK-004 spike code).

**Confirmed**: `src/CommandoWar.Headless/DemoScenario.fs` (the terrain-demo
scenario) — already has a diagonal elevation ridge, an impassable 2x2 block,
a movement-cost patch, an opaque wall, and three directional cover edges,
purpose-built to exercise every terrain feature, so depth ordering has real
relief to prove itself against. Referenced directly by the new F#
client-core (the TASK-029 precedent of referencing `CommandoWar.Headless`
from Godot client code), no content-import pipeline needed this task.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `decisions/ADR-0002-...` (framework-neutral simulation; the client
  references the simulation, never the reverse)
- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` — the accepted architecture
  this task builds for the first time: the "no logic in C#" rule, the
  per-concern placement table, the interop idiom (primitives / arrays of
  `[<CLIMutable>]` records only across the boundary, never F# `option`/DU/
  `list`/tuple)
- `docs/04_SIMULATION_SPEC.md` section 15 (render snapshot contents)
- `docs/11_BACKLOG.md` row B-027
- `src/CommandoWar.Sim/Snapshot.fs` (`RenderSnapshot`/`AgentSnapshot`,
  already implemented)
- `src/CommandoWar.Sim/Terrain.fs` (`elevation`/`passable`/`moveCost`/
  `opaque` accessors)
- `src/CommandoWar.Headless/DemoScenario.fs`, `DiagnosticRender.fs`
  (`runFrames`'s per-tick `Simulation.step` loop — the pattern this task's
  fixed-step scheduler mirrors, minus the diagnostic/overlay machinery,
  which this task does not use — production rendering reads `RenderSnapshot`,
  never `DiagnosticFrame`, ADR-0002/the diagnostics-are-observers rule)
- `src/CommandoWar.Client.Godot/README.md`, `src/CommandoWar.Client.Godot/src/MainNode.cs`
  (the disposable TASK-004 spike's existing isometric projection constants
  and the actual depth-ordering gap: terrain drawn all-before-agents in
  row-major order, no unified sort)
- `src/CommandoWar.Client.Godot/scenes/AppraisalDemo.tscn` +
  `src/AppraisalDemoScene.cs` (the `--selfcheck`/`--screenshot` CLI-arg
  precedent this task's new scene reuses)

## Dependencies

- B-011 (done), TASK-006 (done). No other task selected.

## Allowed scope

- New `src/CommandoWar.Client.Godot/Core/CommandoWar.Client.Godot.Core.fsproj`
  (F#, net10.0): a plain library, no `GodotSharp` reference needed this task
  (no input/marshalling yet — deferred to B-026). References
  `CommandoWar.Sim.fsproj` and `CommandoWar.Headless.fsproj` (for
  `DemoScenario`).
- New F# module(s) in that project: `CwClientCore.IClientScene` (the
  `Ready`/`Update`/`DrawList`/`HudText`/`Dispose` contract), a `DrawItem`
  `[<CLIMutable>]` record (primitives only: `Kind: int`, cell coordinates,
  colour components, radius — no `Godot.*` type), and `DemoRenderScene`
  implementing it: owns the fixed-step accumulator (the `MainNode.cs`
  constants: 20 Hz, 5-step catch-up cap), steps `DemoScenario` via
  `Simulation.step SimConfig.standard`, builds a depth-sorted `DrawItem[]`
  each frame from cached terrain items (computed once from `WorldState.Terrain`)
  merged with per-tick agent items (from `RenderSnapshot.Agents`,
  wall-clock-interpolated between the previous and current tick's
  `Position`).
- New generic C# host `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`
  (ADR-0004 form 1: `[Export] SceneType`, resolves the named
  `IClientScene` via reflection, forwards `_Ready`/`_Process`/`_Draw`/
  `_ExitTree`). `_Draw`'s per-item `CellToScreen` projection and `Draw*`
  calls are the "screen<->cell projection arithmetic" the ADR explicitly
  allows in the C# shim.
- New `src/CommandoWar.Client.Godot/scenes/SnapshotDemo.tscn` using
  `FSharpSceneHost.cs`, `SceneType = "CwClientCore.DemoRenderScene"`. Not
  set as `run/main_scene` (`AppraisalDemo.tscn` stays default) — launched by
  path, the same as `Greybox.tscn`/`GreyboxInvalid.tscn` already are.
- `src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx` (add the new
  F# project), `CommandoWar.Client.Godot.csproj` (new `ProjectReference` to
  it).
- `src/CommandoWar.Client.Godot/README.md` (new section, the TASK-029
  precedent).
- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` (Consequences: record that
  the F# client-core scaffold now exists for real, not only as the
  disposable proof).
- `docs/evidence/task-039-snapshot-rendering.png` (committed screenshot, the
  TASK-029 precedent).
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Forbidden scope

- No change to `CommandoWar.Sim` or `CommandoWar.Headless` (`RenderSnapshot`
  already carries everything this task needs — `Tick`, `Agents[].{Id, Side,
  Position, Progress, Destination}`).
- No player input, no selection, no tactical pause (B-026's scope). The
  scene runs unattended once launched.
- No content-import pipeline / `Greybox.tscn` change (B-024's scope, still
  blocked on B-043).
- No porting of `SimFacade.cs`/`SpikeContent.cs` from C# to F# (ADR-0004's
  consequences name this for B-024/B-026, not B-027 — this task's F#
  client-core drives `DemoScenario` directly, no content DTO involved).
- No true `Progress`-based sub-cell interpolation against the agent's actual
  next route cell (`docs/04` section 15's fuller guidance) — `AgentSnapshot`
  has no next-cell field, only `Destination` (the final target); adding one
  is a `CommandoWar.Sim` change, deliberately deferred (see "Considered and
  rejected"). This task interpolates between consecutive ticks' `Position`
  only (the `MainNode.cs` precedent).
- No vertical screen-height offset for elevated terrain (a visual "raised
  ridge" look). Depth ordering here means draw-order correctness (a unified
  sorted list, terrain interleaved with agents by cell depth), not a new
  elevation-to-screen-height projection — a separate, later visual polish
  item.
- Nothing under `src/_scratch/`, `bench/`, `content/benchmarks/BASELINE.md`.

## Considered and rejected

**True `Progress`/move-cost sub-cell interpolation** (`docs/04` section 15's
fuller guidance: interpolate toward the agent's actual next route cell using
`AgentState.Progress` and that cell's `Terrain.moveCost`): rejected for this
task — `RenderSnapshot.AgentSnapshot` has no next-cell field, only the final
`Destination`, so this isn't computable client-side today. Adding a
`NextCell: Cell option` field is a small, real `CommandoWar.Sim` change but
is out of this task's client-only scope; recorded as a follow-up.

**Vertical height offset for elevated terrain**: rejected — visually
appealing but a separate rendering-polish concern from depth-ordering
*correctness*, and would need care that draw-order sorting still uses grid
depth (`cx + cy`), not the (now-distorted) screen Y position, to avoid
reintroducing the exact bug this task fixes.

## Required work

1. Compute `DemoScenario`'s real tick-by-tick state hash sequence (a
   temporary scratch probe, removed before finishing — the TASK-038
   precedent) and pin the tick-20 (final) hash for `--selfcheck`.
2. Create `CommandoWar.Client.Godot.Core.fsproj`; `IClientScene`, `DrawItem`,
   `DemoRenderScene`.
3. Create `FSharpSceneHost.cs`; wire `SnapshotDemo.tscn`.
4. `--selfcheck` (headless, prints tick/hash, exits non-zero on mismatch)
   and `--screenshot <path>` (the `MainNode.cs`/`AppraisalDemoScene.cs`
   precedent) command-line modes.
5. Verify: `dotnet build` both slnx files; run `--selfcheck` and
   `--screenshot` through the real Godot 4.7.2 editor (available in this
   environment — confirmed before drafting); commit the screenshot.
6. Update `README.md`, ADR-0004 consequences, backlog/ledger/state.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [ ] `CommandoWar.Client.Godot.Core.fsproj` exists; only `SimFacade`-style
      framework-neutral logic in F#, only marshalling/projection arithmetic
      in the new C# host (`AGENTS.md` dependency rules; ADR-0004's "no logic
      in C#" rule).
- [ ] The scene steps `DemoScenario` live (visibly moving agents), not a
      static replay.
- [ ] Terrain and agents draw from one unified, depth-sorted list (not
      "all terrain then all agents") — the elevation ridge, the impassable
      block, and agents crossing them render in correct occlusion order.
- [ ] `--selfcheck` reproduces the pinned hash sequence deterministically.
- [ ] `--screenshot` evidence committed under `docs/evidence/`.
- [ ] No `CommandoWar.Sim`/`CommandoWar.Headless` change; `AppraisalDemo.tscn`
      stays `run/main_scene`, unchanged.
- [ ] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: unaffected (the new project
  isn't referenced by it), `0/0`.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`: `0/0`.
- Godot editor import + `--selfcheck` + `--screenshot`, run for real (Godot
  4.7.2 confirmed available in this environment — no "flagged for Dave"
  deferral this time).
- `git status --porcelain`: matches this task's allowed scope.

## Documentation updates

- This task file's Outcome/Review sections.
- `src/CommandoWar.Client.Godot/README.md`.
- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` Consequences.
- `docs/11_BACKLOG.md` B-027 row.
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Entirely additive and client-side: a new project, a new scene, a new C#
host. No `CommandoWar.Sim`/`CommandoWar.Headless` change, no existing scene
touched. Revertible with `git revert` in one step; `AppraisalDemo.tscn`
remains the default scene throughout.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-16). ADR-0004's own review trigger 1 (an F#
  breakpoint hit from an interactive editor/F5-launched run; hot-reload not
  severing the reference) explicitly deferred to a later session — not a
  blocker for this task's own acceptance criteria (it is a separate,
  longer-running ADR-level review item).
