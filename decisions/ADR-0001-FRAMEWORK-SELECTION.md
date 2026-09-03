# ADR-0001: Presentation Framework Selection

Status: proposed  
Date opened: 2026-09-02  
Decision owner: Dave

## Context

The game requires:

- isometric rendering;
- frequent map and content iteration;
- input, UI, animation, audio, and packaging;
- a deterministic headless F# simulation;
- strong debugging of agent perception and decisions;
- an architecture that remains manageable for one principal developer directing coding agents.

The earlier architecture selected Godot too early. Godot has the strongest integrated authoring environment, but Mibo now offers a serious F#-native alternative with headless execution and raylib or MonoGame presentation backends. Raw MonoGame and raw raylib remain possible but would require more project-owned application and tooling infrastructure.

The selection must be based on equivalent implementation evidence rather than preference for F#, an editor, or a particular rendering API.

## Decision drivers

Weighted drivers for this project:

| Driver | Weight |
|---|---:|
| map and content authoring speed | 20 |
| UI, animation, audio, and presentation productivity | 15 |
| simulation isolation and headless testing | 15 |
| F# development and debugging quality | 12 |
| architectural and glue-code burden | 12 |
| runtime and packaging maturity | 10 |
| observability and debugging | 8 |
| dependency and maintenance risk | 5 |
| backend portability | 3 |

The weights may be adjusted before either spike begins, not after results are known.

## Candidates

### A. Godot .NET plus F# simulation

Expected strengths:

- integrated map, scene, UI, animation, import, and debug tooling;
- shortest likely path from greybox to coherent game presentation;
- less need to build project-specific editor and UI infrastructure;
- strong visual inspection for isometric sorting and overlays.

Expected weaknesses:

- mixed C# and F# boundary;
- simulation and editor tooling live in different idioms;
- Godot lifecycle and serialization types must be kept outside the core;
- no current Godot 4 C# web export, although the project is desktop-first.

### B. Mibo classic MVU plus raylib and Tiled

Expected strengths:

- F#-native host and application loop;
- headless and graphical programmes share concepts;
- explicit stepping and ordered systems fit deterministic simulation work;
- small, transparent rendering backend;
- potential backend substitution between raylib and MonoGame.

Expected weaknesses:

- no integrated scene, UI, animation, or map editor;
- Tiled and custom tooling must cover content workflow;
- smaller and faster-changing dependency;
- backend interchangeability may add abstraction without product value;
- application may spend more code on presentation infrastructure.

### C. Mibo classic MVU plus MonoGame and Tiled

This retains most Mibo strengths and gains MonoGame's broader content and platform foundation, but its heavier backend offers no demonstrated vertical-slice advantage yet. It may be compile-smoke-tested during the Mibo spike but is not required as a full third implementation.

### D. Raw MonoGame or raw raylib

Rejected from the first controlled comparison because each would require the project to design more lifecycle, state, UI, tooling, and content integration than the product hypothesis justifies. They remain fallback technologies if Mibo becomes the only problem in an otherwise successful code-first spike.

## Provisional assessment before spike

This is not the decision.

| Candidate | Provisional weighted score out of 5 |
|---|---:|
| Godot .NET plus F# core | 4.45 |
| Mibo plus MonoGame plus Tiled | 3.93 |
| Mibo plus raylib plus Tiled | 3.83 |
| raw MonoGame | 3.78 |
| raw raylib | 3.58 |

Godot leads because this project is likely to become content- and UX-iteration bound before it becomes rendering-API bound. Mibo remains close enough to require direct evidence because it substantially improves F# architectural unity and headless workflow.

## Required experiment

TASK-004 and TASK-005 must implement equivalent disposable hosts over the same simulation.

### Common result

- one 32 x 32 or comparably small isometric map;
- six visible agents;
- click or equivalent input to issue one typed move command;
- fixed 20 Hz authoritative simulation;
- snapshot interpolation or direct presentation sufficient for inspection;
- tick and canonical state hash overlay;
- one invalid-content failure;
- one measured map and marker edit;
- release build and local package or executable launch.

### Evidence to record

- setup and build commands;
- pinned versions;
- lines and files of host-specific glue, reported as context rather than a quality score alone;
- content edit-to-visible-result workflow;
- debugger and failure diagnostics;
- UI and overlay implementation effort;
- asset import behaviour;
- package size and launch behaviour;
- iteration friction observed by Dave;
- framework types that attempted to cross the simulation boundary;
- unresolved defects or workarounds;
- expected cost of the Bridgehead vertical slice.

## Selection rule

Choose Godot when the evidence shows materially faster or clearer content, UI, animation, and debugging work without corrupting the F# simulation boundary.

Choose Mibo when it achieves an acceptable content workflow with Tiled, keeps the project substantially simpler to debug and evolve in F#, and does not require building an informal engine around raylib.

Choose neither and stop or re-scope when both candidates require unacceptable glue, tooling, or complexity for the narrow vertical slice.

Do not maintain both production hosts after the decision. Preserve spike evidence and remove the losing host from default build and delivery paths.

## Decision

Pending TASK-004, TASK-005, and TASK-006.

Selected candidate: `TBD`

## Decision evidence template

Godot column filled by TASK-004; Mibo column filled by TASK-005
(`docs/12_PROGRESS_LEDGER.md`, 2026-09-02). These are observations, not scores,
and not the decision.

| Driver | Weight | Godot result | Mibo result | Notes |
|---|---:|---|---|---|
| map and content authoring speed | 20 | Greybox authored directly in the `.tscn` scene (tile string + typed `Marker2D` nodes). Data edit -> visible authoritative hash ~0.38 s headless; 1 file, 0 code, 0 conversion steps. Editor hot-reload / inspector drag-edit NOT measured this session (no GUI). | Greybox is a hand-edited `.cwmap` text file (size / seed / 32 terrain rows / marker lines). Data edit -> visible authoritative hash ~1.2 s warm (incremental build + content copy + 40-tick headless). 1 file, 0 code, 0 conversion steps. **Tiled not used** (out of trimmed scope); Mibo has no content hot-reload, iteration is edit -> relaunch. | Both numbers are text-edit lower bounds. Godot's editor and Tiled were both un-exercised; the authoring-tool comparison is still open. |
| UI, animation, audio, and presentation productivity | 15 | Overlay + failure panel built in ~30 lines with `CanvasLayer`/`Label`/`ColorRect`. Isometric render hand-drawn via `_Draw` (no TileSet atlas invested). Animation/audio not exercised. | Overlay + failure panel are ~15 lines of `buffer.AddText`. Iso render hand-drawn with `buffer.AddTriangle`/`AddFillCircle`/`AddLine` command-buffer calls (more verbose than Godot `_Draw`; `Mibo.Color` vs `Raylib_cs.Color` split between draw commands and the clear color). No TileSet/atlas, animation, or audio. | Both hand-drew the greybox. Mibo's 2D command API is complete but lower-level; no scene/atlas tooling at all. |
| simulation isolation and headless testing | 15 | Host consumes the byte-identical `CommandoWar.Sim.dll`; `--headless --selfcheck` reproduces the shared fixture's 41-hash sequence exactly (final `0x838D3AE7DBFB735D`). No Godot type crosses the boundary. | Host consumes the byte-identical `CommandoWar.Sim.dll` (md5 `82418ff...`); `--selfcheck` reproduces the 41-hash sequence exactly via both explicit dispatch and Mibo `withFixedStep`. `HeadlessRunner` (Step/StepN/StepUntil, virtual time) is a first-class part of the framework. No Mibo/raylib type crosses the boundary. | Both strong. Mibo's headless story is native rather than a `--headless` flag on a windowed engine. |
| F# development and debugging quality | 12 | C#/F# interop works; module functions reached as `AgentIdModule.ofInt` etc., `World.create` via `ListModule.OfArray`, `FSharpOption` via `get_IsSome`. Mixed-language solution; no F# debugging done in-editor. | No interop tax: `Agent.create`, `Command.moveTo`, `Simulation.step`, `Hashing.hash`, DU `match` on `EventBody` used directly. Single-language solution; the shared `CommandoWar.Headless` F# project (`Fixture`) is reused by `ProjectReference`. Learning Mibo's API needed reflection dumps + the `mibo-2d` template + the repo's `HeadlessTests.fs` (docs site has gaps; `program.md`/`headless.md` are thin). | The C#/F# boundary tax is genuinely gone. It is replaced by a smaller, one-time cost of learning a sparsely documented framework. |
| architectural and glue-code burden | 12 | ~150 lines is the entire sim boundary (`SimFacade.cs`); ~170 reusable content DTO/validation; ~80 Godot import; ~330 host (scheduling+input+render+overlay). Client TFM forced to `net10.0` (off Godot's `net8.0` default). | `SimBridge.fs` 117 (entire sim boundary); `Content.fs` 192 (framework-neutral `.cwmap` parser + validator; larger because Godot parsed its own `.tscn`); `Program.fs` 403 = self-check MVU program ~90 + windowed host (model/msg/update/view/wiring/CLI) ~310. 712 total F#, ~vs Godot's ~730. `net10.0` is Mibo 4.x's own default. | Comparable total. Mibo's host layer is a little larger (verbose command-buffer view, hand-rolled self-check program); its glue is all one language. |
| runtime and packaging maturity | 10 | Editor + headless run fine (Vulkan). **Self-contained export blocked**: `4.7.2.stable.mono` Windows export templates not installed. Runs outside the editor via `--path`. | `dotnet publish -c Release -r win-x64 --self-contained` **works out of the box**: ~84 MB folder, `cwmibo.exe` launches from any working directory and reproduces the final hash. Windowed run opens a real raylib/OpenGL window (INFO log clean), takes `TakeScreenshot`, quits cleanly (no deferred-quit quirk). | Mibo wins packaging outright: ordinary `dotnet publish`, no engine-specific export step or templates. |
| observability and debugging | 8 | On-screen tick / `StateHash` / random-draw / rate / interp-alpha overlay; per-tick hash stream in headless; `S` dumps a `cwheadless`-format command log; divergence localised by `cwheadless compare`. | On-screen tick / `StateHash` + format / random-draw / sim-rate (fixed vs measured) / render-fps overlay; per-tick hash stream in `--selfcheck`; accepted-command-log in `cwheadless` format; `cwheadless compare` still localises divergence. No interpolation-alpha (view renders exact cells). Mibo has a `FrameProfiler` (4.5.0) not available on 4.1.0. | Parity for the diagnostics this task needed. Standard .NET debugger applies to the whole host (one language). |
| dependency and maintenance risk | 5 | 3 auto-referenced `Godot.*` `4.7.2` packages + the MSBuild SDK; offline feed pinned. Large engine, but mature and MIT. | `Mibo.Core` + `Mibo.Raylib` + `Raylib-cs` + `FSharp.UMX`, zlib-licensed. **Current stable Mibo cannot be used**: `Mibo.Core` >= 4.2.0 hard-depends on the prohibited `Mibo.Adaptive` (folded into the core assembly, 2026-08-11), so this spike is pinned to 4.1.0, already ~5 minor versions behind after ~3 weeks. 1.0 -> 4.5.3 in ~3 months; adaptive engine still shipping freeze fixes. See ADR-0003 2026-09-02 amendment. | Materially worse than ADR-0003 assumed. This is the decisive negative for Mibo and stands independent of the ergonomic results. |
| backend portability | 3 | Not applicable to Godot (single renderer). | `Mibo.MonoGame` backend exists (same `Program`/`view` shape) but also depends on `Mibo.Core` >= 4.2.0, so it hits the same `Mibo.Adaptive` block; the MonoGame smoke test is out of the trimmed scope. | Portability is real in principle, unusable in practice on the current release line, and has no demonstrated product value. |

### TASK-004 Godot spike results (evidence, not decision)

What was proven:

- C# creates and steps the unmodified F# session through one facade
  (`SimFacade.cs`); the `CommandoWar.Sim.dll` in the host output is byte-identical
  to the library build.
- A 32x32 isometric greybox is authored in a Godot scene (`Greybox.tscn`) and
  converted to framework-neutral DTOs at load; six agents render from snapshot
  value-views.
- Click / scripted input produces the existing `Command.moveTo` and a
  deterministic state change; the host's per-tick `StateHash` sequence is
  identical to `cwheadless fixture` for all 40 ticks (final
  `0x838D3AE7DBFB735D`).
- Host-owned fixed-step scheduling is visibly independent of render rate
  (overlay: 6 Hz sim vs 59 fps render, adjustable at runtime).
- Invalid authored content (`GreyboxInvalid.tscn`) reaches a visible, actionable
  failure listing every error in one pass and does not start the simulation.
- The client never catches a `Simulation.step` exception; the only catch is the
  content-load `ContentException`.

Friction / gaps recorded:

- Full self-contained packaging is blocked on the missing `4.7.2.stable.mono`
  export templates (exact error in the ledger); demonstrated running outside the
  editor UI instead.
- The Godot editor GUI (visual TileMap painting, inspector editing, scene
  hot-reload, F5 run, the debugger, the profiler, asset re-import) was **not
  exercised** in this session - it needs an interactive display. Content
  authoring and iteration were done as text edits + headless relaunch, which
  understates Godot's core claimed advantage. A fair authoring-speed comparison
  needs an interactive pass.
- Client project must target `net10.0` (F# reference), off Godot 4.7.2's
  `net8.0` template default.
- Mixed C#/F# interop is a small contained cost in the facade.

Expected cost to extend this host to the Bridgehead vertical slice: the sim
boundary (`SimFacade`) and content validation (`SpikeContent`) largely carry
over; the new work is a real TileSet/atlas pipeline, the developer/tactical
overlays (docs/06 section 11), selection + command-preview UX, isometric depth
sorting with occluders, and the export-template setup. None of that touched the
simulation in the spike.

### TASK-005 Mibo spike results (evidence, not decision)

What was proven:

- F# creates and steps the unmodified F# session through one module
  (`SimBridge.fs`); the `CommandoWar.Sim.dll` in the host output is byte-identical
  to the library build (md5 `82418ffbdf316b2f2e5124105b6f9e5e`). No interop tax.
- A 32x32 isometric greybox is authored as a hand-edited `.cwmap` text file,
  parsed and validated by a framework-neutral module (`Content.fs`), and only
  then handed to the simulation; six agents render from snapshot value-views.
- Scripted input produces the existing `Command.moveTo` and a deterministic
  state change; the host's per-tick `StateHash` sequence is identical to
  `cwheadless fixture` for the initial state and all 40 ticks (final
  `0x838D3AE7DBFB735D`, 33 events, random draws 0), via both explicit dispatch
  and Mibo's `withFixedStep` facility.
- `Program.withFixedStep` (authoritative) is visibly independent of
  `Program.withTick` (render): screenshot overlay shows sim 6 Hz vs render
  60 fps.
- Invalid authored content (`greybox-invalid.cwmap`) reaches a visible,
  actionable failure listing every error in one pass, each naming the marker /
  kind / cell / reason, and does not start the simulation (exit 2).
- The host never catches a `Simulation.step` exception; there is no `try` around
  the step. `Content.load` returns a `Result`; a deliberate hash mismatch exits
  non-zero (1).
- `dotnet publish -c Release -r win-x64 --self-contained` succeeds with no
  engine-specific step; the ~84 MB `cwmibo.exe` reproduces the final hash from
  an unrelated working directory.
- `Mibo.Adaptive` appears nowhere in the restore graph, `deps.json`, or output;
  the host uses only classic-MVU `Program` / `HeadlessProgram` / `HeadlessRunner`.

Friction / gaps recorded:

- **The current stable Mibo cannot be used.** `Mibo.Core` >= 4.2.0 hard-depends
  on the prohibited `Mibo.Adaptive` package (the adaptive integration was folded
  into the core assembly on 2026-08-11). The spike is pinned to `Mibo.Core` /
  `Mibo.Raylib` 4.1.0, the last clean release, already ~5 minor versions and
  ~3 weeks behind. See `decisions/ADR-0003-MIBO-ADOPTION.md` 2026-09-02
  amendment. This is the decisive negative and is independent of ergonomics.
- Mibo's docs are thin: `program.md` / `headless.md` cover the basics but omit
  most of the API surface used here. The working API was recovered from
  reflection dumps of `Mibo.Core.dll` / `Mibo.Raylib.dll`, the `mibo-2d`
  template, and the repo's `HeadlessTests.fs`.
- Tiled and any Mibo content hot-reload were **not exercised**: Tiled authoring
  is out of the trimmed scope, the map is a hand-edited text file, and Mibo has
  no content hot-reload (edit -> relaunch, ~1.2 s warm). Godot's editor was
  equally un-exercised in TASK-004, so the authoring-tool comparison remains
  open on both sides.
- Interactive mouse input was not literally exercised (headless session); the
  `LeftClick` handler shares the exact `QueueMove -> Command.moveTo` path that
  `--selfcheck` verifies end to end.
- Mibo's 2D command-buffer API is complete but lower-level than Godot's `_Draw`,
  and it splits colour types (`Mibo.Color` for draw commands, `Raylib_cs.Color`
  for the renderer clear colour). `withFixedStep` rate is fixed at construction;
  runtime rate adjustment would need a host-owned accumulator.
- No source-level workaround was needed inside the host once the 4.1.0 pin was
  in place. No simulation change was needed.

Expected cost to extend this host to the Bridgehead vertical slice: `SimBridge`
and `Content` carry over; the new work is a real sprite/atlas pipeline, the
developer/tactical overlays (docs/06 section 11), selection + command-preview
UX, isometric depth sorting with occluders, and a UI layer (Mibo has none) plus
either a Tiled importer or a richer text format. None of that touched the
simulation in the spike. The standing risk is riding a fast-moving pinned-old
dependency or accepting the coupled `Mibo.Adaptive` package.

### Qualitative decision

- Chosen route:
- Decisive evidence:
- Weaknesses accepted:
- Losing route removal plan:
- Review trigger:

## Consequences after acceptance

- Update `PROJECT_STATE.yaml` with the selected route.
- Move G1 to passed only after evidence review.
- Update `docs/02_TECHNOLOGY_DECISION.md` to remove obsolete provisional language.
- Pin selected dependencies.
- Remove the losing host from default build, CI, and task assumptions.
- Do not abstract over both frameworks unless a later accepted ADR demonstrates a product requirement.
