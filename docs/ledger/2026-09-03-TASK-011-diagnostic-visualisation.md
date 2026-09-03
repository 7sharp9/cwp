## 2026-09-03 - TASK-011 - Headless diagnostic visualisation and the framework-neutral diagnostic frame

**Owner:** Dave
**Source revision:** `4aa9c9c` (working tree; not yet committed)
**Environment:** .NET SDK `10.0.303`, `net10.0`, Windows 11; `dotnet test` via xUnit 2.9.3
**Status change:** TASK-011 `proposed -> active -> review`; TASK-010 backlog rows `active -> review` (task file untouched; not a finalisation)

### Changes

- **`src/CommandoWar.Sim/Diagnostics.fs` (new, compiled after `Divergence.fs`).**
  Framework-neutral per-tick diagnostic model:
  - `LayerName` (`elevation` / `passability` / `movement-cost` / `opacity`);
  - `GridLayer { Name; Bounds; Cells: int[] }` (dense row-major);
  - `EdgeMarker { Cell; Direction; Value }` for directional cover;
  - `AgentMarker { Id; Side; Cell; Destination }`;
  - `EventMarker { Kind: string; Cells: Cell[] }` summarising one `DomainEvent`;
  - `Overlay = Cells of label: string * cells: Cell[]` - the one non-speculative
    shape; the doc comment names where B-009 / B-010 / B-011 / B-019 attach new
    cases. `DiagnosticFrame.Overlays` is always empty at this stage.
  - `DiagnosticFrame { Tick; Bounds; Layers; Edges; Agents; Events; Overlays;
    Hash: StateHash; RandomDraws: uint64 }`;
  - `Diagnostics.frame : WorldState -> DiagnosticFrame` (no events) and
    `Diagnostics.frameOf : StepResult -> DiagnosticFrame` (this-tick event
    markers + the post-step `StateHash`). Both total, pure, deterministic: no
    mutation, no `Random` read, no wall-clock read, not in the phase pipeline.
  Module comment states nothing in `Simulation.step` constructs a frame. It is a
  leaf: `Canonical.encode`, `Hashing`, `Simulation`, and `Replay` do not
  reference it, so no pinned hash moves.
- **`src/CommandoWar.Headless/DiagnosticRender.fs` (new).** Deterministic
  renderers (no timestamps, no generated ids, `\n` endings):
  - `runFrames : WorldState -> RecordedCommand[] -> int64 -> DiagnosticFrame[]`
    (frame per tick, index 0 = tick 0);
  - `Ascii` - coordinate-ruled composite grid (`#` impassable, `o` opaque,
    `1`-`9` elevation, `@`/`X` agents), a second cover grid, legend, agent
    roster, events line, footer with the determinism trio. Single-layer mode
    (`--layer`) renders that layer as a numeric heat map;
  - `Svg` - integer-pixel cells (elevation -> fill lightness, impassable ->
    `url(#cw-hatch)`, opaque -> dark border), cover triangles per edge, agents
    as circles coloured by side with a dashed destination line, amber event
    rings, footer text. Scale is a fixed integer `16`;
  - `Html : DiagnosticFrame[] -> string` - one self-contained XHTML file, inline
    CSS/JS in CDATA, one embedded SVG per tick, slider + Prev/Next scrubber.
- **`src/CommandoWar.Headless/DemoScenario.fs` (new).** A 12 x 8 `RawScenario`
  with a terrain layer: a diagonal elevation ridge (1-3), an impassable 2 x 2
  block, a 3-cell movement-cost patch, a 3-cell opaque wall, and three
  directional cover edges; 2 friendly + 1 hostile agents; a two-command log so
  agents move. Built through `Scenario.validate` + `World.ofScenario` (fails
  hard - it is a fixed vector, not user content).
- **`src/CommandoWar.Headless/Program.fs`.** New `render` verb:
  `render <fixture|demo|command-log> [--tick N] [--layer NAME]
  [--format ascii|svg|html] [--out PATH]`. `--layer` filters
  `DiagnosticFrame.Layers`; `html` embeds all ticks and ignores `--tick`
  (stderr note). `step` / `replay` / `compare` / `fixture` output unchanged;
  `usage ()` gains two lines.
- **`content/diagnostics/` (new).** Goldens: `fixture-tick-000/040.ascii.txt`,
  `fixture-tick-000/040.svg`, `demo.ascii.txt`, `demo.svg`, `demo.html`, and a
  `README.md` with the exact regeneration commands. Regeneration is idempotent.
- **`.gitattributes`.** `content/diagnostics/** text eol=lf` so the byte
  comparison holds on Windows (`core.autocrlf=true`).
- **`tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs` (new, 13 facts).** Frame
  purity and content, the cover-edge / layer / trio checks, `frameOf` carrying
  destinations and event markers, the fixture frame hash equals `Hashing.hash`,
  golden byte-equality (demo ASCII/SVG/HTML + fixture tick 0/40 ASCII/SVG),
  distinct rendering of the impassable / elevated / cover-edge features, render
  determinism, HTML parses as XML with one `cw-frame` per tick, and the pin
  that diagnostics leave the fixture at `0xF2F3DF0D820AD9AC` /
  `0x838D3AE7DBFB735D` with 33 events. `.fsproj` gains the compile entry and a
  `Content` copy of `content/diagnostics/*` next to the test assembly.
- **`.fsproj` registration.** `Diagnostics.fs` after `Divergence.fs`;
  `DemoScenario.fs` + `DiagnosticRender.fs` between `Fixture.fs` and
  `Program.fs`.
- **Docs.** `AGENTS.md` new "Diagnostics" subsection (the standing rule);
  `docs/09` section 8 "Diagnostic visualisation is a completion criterion";
  `docs/06` section 11 and `docs/07` section 6 realisation notes.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: Build succeeded, 0 warnings, 0 errors.
- Command: `dotnet test CommandoWar.slnx -c Release` (before)
  - Result: `Passed: 102`.
- Command: `dotnet test CommandoWar.slnx -c Release` (after)
  - Result: `Passed: 115` (102 + 13 new).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  - Result: initial `0xF2F3DF0D820AD9AC`, final `0x838D3AE7DBFB735D`, 33 events
    (unchanged).
- Command: `dotnet run ... -- render demo --format ascii|svg|html --out ...`
  then re-run against the committed files
  - Result: byte-identical (regeneration idempotent); `demo.html` parses as XML
    with 21 `cw-frame` elements (ticks 0..20).
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`
  - Result: `FSharp.Core 10.1.303` only; no `ProjectReference`.
- Command: source scan of `src/CommandoWar.Sim` and `src/CommandoWar.Headless`
  for `godot|mibo|monogame|raylib|DateTime|Stopwatch|System.Random|Dictionary|HashSet|groupBy|float`
  - Result: only doc-comment mentions (Godot / Mibo / "floating point");
    no code match.
- Command: `git status`
  - Result: only `src/CommandoWar.Sim/`, `src/CommandoWar.Headless/`, `tests/`,
    `content/diagnostics/`, `.gitattributes`, `AGENTS.md`, `docs/`,
    `PROJECT_STATE.yaml`, `tasks/TASK-011-*.md` changed. No `tasks/TASK-010-*.md`.

### Evidence

- Pinned fixture hashes unmoved: `0xF2F3DF0D820AD9AC` / `0x838D3AE7DBFB735D`,
  33 events (`FixtureTests.fs`, `TerrainTests.fs`, `DiagnosticsTests.fs`).
- Goldens: `content/diagnostics/` (7 render files + README); byte sizes
  fixture ASCII 3056 / 3060, fixture SVG 110300 / 110304, demo ASCII 819,
  demo SVG 11276, demo HTML 247178.
- `DiagnosticFrame` demo tick-0 footer:
  `tick 0 | hash 0x8876CA7E24B60684 (format 1) | draws 0 | agents 3 |
  cover-edges 3 | events 0`.

### Deviations and unresolved issues

- `Overlay` is a one-case DU (`Cells of label * cells`) rather than a truly
  empty type (F# forbids zero-case DUs). The case is the generic labelled-cell
  primitive, not a named future system; `Diagnostics.frame` never emits one.
- TASK-010's backlog rows were moved `active -> review` (its task file was not
  touched) so the "one active task" rule holds with TASK-011 selected. This is
  not the TASK-010 finalisation, which remains for Dave.
- `.gitattributes` gained one line (not in the task's file list but required
  for the golden byte comparison under `core.autocrlf=true`).
- ASCII composite folds impassable / opaque / elevation into one glyph and
  reports the movement-cost patch as a text line; per-cell cost is available via
  `--layer movement-cost`. Edge-accurate cover is a separate cover grid in ASCII
  and triangles in SVG.

### Documents updated

- `AGENTS.md` (new "Diagnostics" subsection);
- `docs/09_TEST_STRATEGY.md` section 8;
- `docs/06_CONTENT_AND_PRESENTATION.md` section 11;
- `docs/07_VERTICAL_SLICE.md` section 6;
- `docs/11_BACKLOG.md` (TASK-011 + B-012a rows active; TASK-010 / B-008
  `-> review`; B-029 reworded "Render the DiagnosticFrame in Godot" + dep
  B-012a);
- `docs/12_PROGRESS_LEDGER.md` (this index row; "Pinned facts" green tests
  `102 -> 115`);
- `PROJECT_STATE.yaml` (`active_work.selected_task` / `task_file` -> TASK-011);
- `tasks/TASK-011-DIAGNOSTIC-VISUALISATION.md` (new).

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: TASK-010 finalisation is still outstanding and was deliberately left
  for Dave (precondition of this task).
