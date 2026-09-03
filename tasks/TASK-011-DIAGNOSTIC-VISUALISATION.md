# TASK-011: Headless diagnostic visualisation and the framework-neutral diagnostic frame

Status: done
Owner: Dave
Phase: P2
Gate: G2 (deterministic core); realises backlog B-012a
Size: M

## Objective

Add a framework-neutral per-tick diagnostic model to `CommandoWar.Sim`
(`Diagnostics.fs`, `DiagnosticFrame`) and deterministic headless renderers for
it in `CommandoWar.Headless` (`DiagnosticRender.fs`: ASCII, SVG, HTML), plus a
`cwheadless render` verb, a terrain-demo scenario that exercises every layer,
committed golden outputs, and the standing rule that authoritative spatial or
tactical state must be visually inspectable. Data plus rendering only: no tick
phase reads the frame and no frame feeds back into the step (the observer
precedent of `Canonical.fs` and `Divergence.fs`).

This is the P2 foundation for `docs/06` section 11 "Developer-facing" overlays,
`docs/07` section 6 "developer overlay", and functional acceptance criterion 11.

## Why this task exists

B-009 (line of sight) and B-010 (pathfinding) are next in P2 and are the
systems that most need eyes on a run. Without a diagnostic frame and a renderer,
each would invent its own ad-hoc dump. Building the observer contract now, with
one real renderer set and a standing rule, keeps every later spatial system
inspectable by construction. Deferring the whole thing to B-029 (P4) leaves P2
blind.

## Required reading

1. `PROJECT_STATE.yaml`, `AGENTS.md`
2. `decisions/ADR-0002-SIMULATION-BOUNDARY.md` in full (dependency direction,
   allow/forbid lists, "Mutation policy", compliance checks, the TASK-010
   amendment); `decisions/ADR-0004` (the C#/F# client boundary this must not
   pre-empt)
3. `docs/03_ARCHITECTURE.md` sections 2, 4, 5, 9, 11, 12, 18; `docs/04` sections
   15, 17; `docs/06` sections 2, 4, 10, 11; `docs/07` sections 6, 9 (criterion
   11); `docs/09` sections 1, 2.4, 3, 8
4. `src/CommandoWar.Sim/` in full, especially `Grid.fs`, `Terrain.fs`,
   `Domain.fs`, `Snapshot.fs`, `Events.fs`, `Simulation.fs` (`StepResult`),
   `Canonical.fs`, `Divergence.fs`, `Hashing.fs`
5. `src/CommandoWar.Headless/` in full (`Program.fs` verbs, `Fixture.fs`,
   `CommandLogFile.fs`)
6. `tests/CommandoWar.Sim.Tests/{FixtureTests,ScenarioTests,TerrainTests}.fs`
7. `content/fixtures/SPIKE-FIXTURE.md`

## Dependencies

- TASK-010 (terrain grid) in review; this task reads `Terrain` and does not
  change it.

## Central decisions

- The diagnostic model lives in `CommandoWar.Sim/Diagnostics.fs`, compiled
  after `Divergence.fs`. Framework-neutral: ints, bools, arrays, strings, DUs.
  No floating point. Pure function of `WorldState` (and, for the event
  annotations, `StepResult`): `Diagnostics.frame` does not mutate, does not
  read `Random`, is not part of the phase pipeline. Adding it moves no pinned
  fixture hash and does not touch `Canonical.encode`.
- The renderers live in `CommandoWar.Headless`, not the sim: turning a frame
  into ASCII/SVG/HTML is presentation and ADR-0002 keeps rendering out of the
  sim. The Godot developer overlay (B-029) will be a third renderer of the
  same frame; it is not built here.
- Model only what exists: terrain `GridLayer`s, per-edge cover markers, agent
  markers, this-tick event markers, the determinism trio. An extensible
  `Overlay` DU is provided with the extension point documented, but NO cases
  for systems that do not exist yet.

## Allowed scope

- `src/CommandoWar.Sim/Diagnostics.fs` (new), `CommandoWar.Sim.fsproj`;
- `src/CommandoWar.Headless/DiagnosticRender.fs` (new), `DemoScenario.fs`
  (new), `Program.fs` (the `render` verb only), `CommandoWar.Headless.fsproj`;
- `content/diagnostics/` golden outputs + README (new directory);
- `.gitattributes` (pin `content/diagnostics/**` to `eol=lf` for the byte
  comparison);
- tests in `tests/CommandoWar.Sim.Tests/` (`DiagnosticsTests.fs` new,
  `.fsproj` registration + golden content copy);
- `AGENTS.md` and `docs/09` section 8 (the standing rule); `docs/06` section 11
  and `docs/07` section 6 realisation notes;
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml`).

## Forbidden scope

- Any rendering, graphics, or UI framework in `CommandoWar.Sim`; a
  Godot/MonoGame/raylib reference anywhere; the Godot developer overlay itself
  (B-029).
- Player-facing UI: HUD, reason panels, player threat markers, command preview
  (B-026, B-028).
- Real-time input, camera, animation, interpolation, sound.
- Floating-point values anywhere in the model or the renderers.
- Line-of-sight, pathfinding, reservation, or combat logic; empty `Overlay`
  cases for them (B-009 to B-011, B-019).
- Changing `Simulation.step`, `Canonical.encode` or its format version,
  `Setup.sixAgentWorld`, the shared fixture parameters, the pinned hashes, or
  any existing `cwheadless` verb's output.
- A new dependency or a new project; an on-disk scenario/content file format
  (B-024).
- Touching the client spikes, `src/_scratch`, `.slnx`; reorganising
  `decisions/` or `tasks/`; destructive git. Finalising TASK-010.

## Required work

1. `Diagnostics.fs`: `GridLayer`, `EdgeMarker`, `AgentMarker`, `EventMarker`,
   an open `Overlay` DU (extension point documented, no speculative cases),
   `DiagnosticFrame`, and `Diagnostics.frame : WorldState -> DiagnosticFrame` /
   `Diagnostics.frameOf : StepResult -> DiagnosticFrame` (both total, pure,
   deterministic). Module comment: nothing in `Simulation.step` calls this.
2. `DiagnosticRender.fs`: `Ascii`, `Svg`, `Html`, each byte-deterministic
   (no timestamps, no random ids). `Html : DiagnosticFrame[] -> string` is one
   self-contained file with a slider to scrub the run.
3. Extend the `cwheadless` CLI with a `render` verb (`fixture` / `demo` /
   command-log target; `--tick`, `--layer`, `--format ascii|svg|html`,
   `--out`). Existing verbs unchanged.
4. A demonstration terrain scenario in `CommandoWar.Headless` (`DemoScenario.fs`),
   built as a `RawScenario` with a terrain layer (ridge, impassable block, cost
   patch, opaque wall, cover edges), validated through `Scenario.validate` and
   `World.ofScenario`.
5. Golden outputs in `content/diagnostics/` with a README carrying the exact
   regeneration commands.
6. `DiagnosticsTests.fs`: frame purity and content, the determinism trio, the
   fixture-frame hash equals `Hashing.hash`, golden byte-equality for ASCII /
   SVG / HTML, distinct rendering of the impassable / elevated / cover-edge
   features, render determinism, the HTML frame count, and a pin that
   diagnostics leave the fixture at `0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`
   and 33 events.
7. Register `Diagnostics.fs`, `DiagnosticRender.fs`, `DemoScenario.fs`, and
   `DiagnosticsTests.fs` in their `.fsproj`. `TreatWarningsAsErrors` clean.
8. The standing rule in `AGENTS.md` and `docs/09` section 8; realisation notes
   in `docs/06` section 11 and `docs/07` section 6.

## Acceptance criteria

- [x] `src/CommandoWar.Sim/Diagnostics.fs` exists with `GridLayer`,
      `EdgeMarker`, `AgentMarker`, `EventMarker`, an open `Overlay` DU (no
      speculative cases), `DiagnosticFrame`, and `Diagnostics.frame` /
      `Diagnostics.frameOf`, all total and pure. Module comment says no phase
      calls it.
- [x] `Canonical.encode` unchanged; `Canonical.FormatVersion` still `1`;
      `Diagnostics.fs` is a leaf nothing authoritative references.
- [x] `DiagnosticRender.fs` renders ASCII, SVG, and HTML, byte-deterministic.
      HTML embeds one SVG per tick with a scrubber and parses as XML/HTML.
- [x] `cwheadless render` verb added; `step` / `replay` / `compare` /
      `fixture` output unchanged.
- [x] `DemoScenario.fs` builds through `Scenario.validate` + `World.ofScenario`
      and exercises every renderer layer.
- [x] `content/diagnostics/` holds the fixture (tick 0 and 40) and demo
      ASCII/SVG (+ demo HTML) goldens and a README with the regeneration
      commands.
- [x] `dotnet test CommandoWar.slnx -c Release` = `Passed: 115` (102 + 13).
- [x] `cwheadless fixture` unchanged: initial `0xF2F3DF0D820AD9AC`, final
      `0x838D3AE7DBFB735D`, 33 events.
- [x] `dotnet list ... package --include-transitive` = `FSharp.Core` only, no
      `ProjectReference`; source scan of `src/CommandoWar.Sim` and
      `src/CommandoWar.Headless` clean of framework references (doc comments
      only).
- [x] Standing rule in `AGENTS.md` + `docs/09` section 8; realisation notes in
      `docs/06` section 11 and `docs/07` section 6.
- [x] Backlog rows, ledger index row + detail file, `PROJECT_STATE.yaml`, and
      task status updated. No forbidden scope entered.

## Required verification

- `dotnet build CommandoWar.slnx -c Release` (0 warnings, 0 errors)
- `dotnet test CommandoWar.slnx -c Release` before and after
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
- `dotnet run --project src/CommandoWar.Headless -c Release -- render <demo>
  --format ascii|svg|html`, byte-compared against the committed goldens
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
- source scan of `src/CommandoWar.Sim` and `src/CommandoWar.Headless` for
  `godot|mibo|monogame|raylib|DateTime|Stopwatch|System.Random|Dictionary|HashSet|groupBy|float`
- `git status`

## Evidence to capture

- the `DiagnosticFrame` / `GridLayer` / `Overlay` shapes;
- a sample ASCII render;
- the golden regeneration commands and byte sizes;
- the pinning-test hashes and event count;
- exact commands and results.

## Expected files

- `src/CommandoWar.Sim/Diagnostics.fs` (new), `CommandoWar.Sim.fsproj`;
- `src/CommandoWar.Headless/DiagnosticRender.fs` (new), `DemoScenario.fs`
  (new), `Program.fs`, `CommandoWar.Headless.fsproj`;
- `content/diagnostics/*` (new), `.gitattributes`;
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs` (new),
  `CommandoWar.Sim.Tests.fsproj`;
- `AGENTS.md`, `docs/09_TEST_STRATEGY.md`, `docs/06_CONTENT_AND_PRESENTATION.md`,
  `docs/07_VERTICAL_SLICE.md`;
- `tasks/TASK-011-DIAGNOSTIC-VISUALISATION.md`, `docs/11_BACKLOG.md`,
  `docs/12_PROGRESS_LEDGER.md`,
  `docs/ledger/2026-09-03-TASK-011-diagnostic-visualisation.md`,
  `PROJECT_STATE.yaml`.

## Rollback or removal

`Diagnostics.fs` is a leaf: removing it, `DiagnosticRender.fs`, `DemoScenario.fs`,
the `render` verb, `content/diagnostics/`, and `DiagnosticsTests.fs` restores
the pre-task state without touching `Simulation.step`, `Canonical.encode`, the
shared fixture, or any other verb. The standing rule and realisation notes
revert with their sections.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Alternative

If the `render` verb plus three formats had proved too large, the split was:
land `Diagnostics.fs` + the ASCII renderer + the `render` verb + goldens + the
standing rule as TASK-011, and take SVG and the HTML scrubber as TASK-011b. It
did not: all three renderers fit with the golden set and the tests green.
