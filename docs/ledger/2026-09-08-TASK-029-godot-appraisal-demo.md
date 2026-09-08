## 2026-09-08 - TASK-029 - Godot appraisal-divergence demo

**Owner:** Dave with coding-agent assistance
**Branch:** `task-029-godot-appraisal-demo` off `main` at
`1f79f78 Merge TASK-028: staged order appraisal and typed reasons (B-017)`.
Committed locally; not pushed.
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; .NET 10.0.11;
FSharp.Core 10.1.303; xUnit 2.9.3; Godot 4.7.2.stable.mono.official.ed1daf0bf
(`Godot_v4.7.2-stable_mono_win64_console.exe`); Godot.NET.Sdk / GodotSharp
4.7.2.
**Status change:** TASK-029 `draft -> review`; backlog B-029 annotated
(P3 read-only corpus-scoped slice landed; the full P4 item unchanged).

A read-only, corpus-scoped slice of backlog B-029 (the Godot `DiagnosticFrame`
renderer) pulled forward into P3 as decision-support: the visual proof of
`docs/07` section 9 criteria 2 ("at least two soldiers appraise the same order
differently for traceable reasons") and 11 ("the developer overlay can explain
any appraisal"), and the refuse-near-threats feel check before B-019. The sim
work since the last Godot demo (54534de, 2026-09-02 — the TASK-004 greybox
spike) is entirely headless. No `CommandoWar.Sim` change; no ADR (ADR-0001 /
ADR-0002 / ADR-0004 cover it).

### Central decisions (confirmed with Dave 2026-09-08 before the phase bodies)

- **A — build on the existing spike skeleton; all non-trivial logic in a new F#
  helper.** Keep `src/CommandoWar.Client.Godot`'s pinned `.csproj`,
  `nuget.config`, `project.godot`, `.godot`, `.gitignore`. Add one
  `ProjectReference` to `CommandoWar.Headless`; add
  `scenes/AppraisalDemo.tscn` + `src/AppraisalDemoScene.cs` and repoint
  `run/main_scene`. Leave the greybox spike files untouched. All non-trivial
  logic (corpus load, `runFrames`, disposition → text / tone, view-model prep)
  in a new framework-neutral `src/CommandoWar.Headless/AppraisalDemo.fs`
  (after `DiagnosticRender.fs`), covered by xUnit in
  `tests/CommandoWar.Sim.Tests`. The C# script is a thin renderer only. Keep
  `CommandoWar.Client.Godot` out of `CommandoWar.slnx`. **Scoped deviation**
  from ADR-0004's "F# client-core / no logic in C#" rule, for this disposable
  P3 demo — **not** a precedent for the P4 client tasks (B-026 / B-027 / B-029).
- **B — scenario source: corpus reuse.** `Corpus.all` → `Name =
  "exposed-approach"`; `Corpus.loadLog` against a `res://`-relative
  `content/replays` (`ProjectSettings.GlobalizePath("res://")` + `../../content/replays`),
  reading the exact committed `.cwlog` and failing loud if absent;
  `DiagnosticRender.runFrames`. Same run as the goldens (both use
  `SimConfig.standard`). No authored scene, no copied `.cwlog`.
- **C — scrubbable: 13 frames (tick 0..12).** `HSlider` + `Left`/`Right` arrow
  keys, default index 1 (the divergence tick). Mirrors `DiagnosticRender.Html`.
- **D — overlay set: mirror the `DiagnosticRender.Svg` vocabulary exactly.**
  Same colours / glyphs (a Godot-window integer up-scale of the SVG's
  `Scale = 16` for legibility — `CellPx = 44` — with proportions and colours
  preserved): terrain grid, agents (`#2b6cb0` friendly / `#c53030` hostile,
  white stroke, dashed same-colour destination line), the `KnownContact` ring
  (`#805ad5` dashed circle + `?<id>` text), the `PlannedPath` polyline
  (`#dd6b20`, green `#2f855a` start disc, `#dd6b20` goal rect), the
  `OrderAppraisal` overlay (`#c53030` `fill-opacity 0.25` exposed cells + a bold
  `A`/`R`/`U` glyph in `#2f855a` / `#c53030` / `#718096`), a per-agent
  `DecisionReason` panel, a tick / hash / format / draws HUD. Only the overlay
  cases the frames carry are drawn; a one-line on-screen fallback lists any
  unhandled `Overlay` case.
- **E — verification / test boundary.** (1) `AppraisalDemo.fs` pure, tested in
  `tests/CommandoWar.Sim.Tests`: a disposition → text mapping fact asserted
  against the committed goldens (`"accepted"` /
  `"refused route-too-exposed threat-agent-2"` from
  `exposed-approach-tick-001.ascii.txt`, `"unable no-known-route"` from
  `blocked-goal-tick-001.ascii.txt` — not against `DiagnosticRender`'s
  `let private` formatters); a fact that `loadExposedApproachFrames` reproduces
  the `exposed-approach.md` tick-1 hash `0xB03F8419E55F3592` + the two tick-1
  dispositions. (2) A headless Godot `--selfcheck` smoke prints the tick-1
  dispositions + hash and asserts vs the golden hash; a committed screenshot
  captured via `--screenshot`. (3) Dave launches the scene interactively and
  confirms the divergence reads right — a **Dave step**, not claimed here.
- **F — no ADR.** ADR-0001 (Godot accepted), ADR-0002 (`Client → Sim` allowed;
  `Sim` has no Godot reference — verified with a source scan), ADR-0004 (client
  structure) cover this. Recorded here + the task file, not as an ADR: (i) the
  C#-scene deviation from ADR-0004, scoped to this disposable demo; (ii) that
  TASK-029 pulls a read-only, corpus-scoped slice of B-029 forward into P3 as
  decision-support — B-029 proper stays P4.

### Changes

- **`src/CommandoWar.Headless/AppraisalDemo.fs` (new leaf, after
  `DiagnosticRender.fs`).** `[<RequireQualifiedAccess>] module AppraisalDemo`,
  an OBSERVER over `Corpus` / `DiagnosticRender` (nothing in `Simulation.step`
  references it; no Godot type; no mutation). `EntryName` / `DivergenceTick`
  literals. `dispositionText : OrderDisposition -> string` and
  `dispositionTone : OrderDisposition -> string` — re-implemented on purpose so
  the TASK-029 test asserts the mapping against the goldens, not against
  `DiagnosticRender.dispositionText` / `reasonText` (`let private`).
  `loadExposedApproachFrames : string -> DiagnosticFrame[]` (over `Corpus.all` +
  `Corpus.loadLog` + `DiagnosticRender.runFrames`, `failwithf` on a missing
  entry / log). Flat `[<CLIMutable>]` view model for the C# renderer: `CellXY`,
  `AgentDot`, `ContactRing`, `RouteLine`, `AppraisalRow`, `FrameView` — no F#
  `option` / DU / `list` / tuple crosses the boundary (the `AgentMarker.Destination`
  option is collapsed to a flag + coordinates; the `Overlay` DU is matched
  exhaustively into the flat records with an `UnhandledOverlays: string[]`
  fallback). `frameView : DiagnosticFrame -> FrameView` (pure, total) and
  `loadFrameViews : string -> FrameView[]`.
- **`src/CommandoWar.Headless/CommandoWar.Headless.fsproj`.** `<Compile
  Include="AppraisalDemo.fs" />` after `DiagnosticRender.fs`, before
  `Program.fs`.
- **`src/CommandoWar.Client.Godot/src/AppraisalDemoScene.cs` (new).** A
  `Node2D` renderer (~340 lines). `_Ready`: parse args, resolve
  `ProjectSettings.GlobalizePath("res://")` + `../../content/replays`, call
  `AppraisalDemo.loadFrameViews`, clamp the index to `DivergenceTick`, build a
  `CanvasLayer` with an `HSlider` + a HUD `Label` + a `DecisionReason` panel
  `Label`. `_UnhandledInput`: `Left`/`Right` scrub. `_Process`: deferred
  headless exit, the `--selfcheck` body, the `--screenshot` capture.
  `_Draw`: grid, exposed-cell tint, `PlannedPath` polyline + start disc + goal
  rect, `KnownContact` dashed ring + `?<id>`, agents + dashed destination
  lines, `A`/`R`/`U` glyphs, the unhandled-overlay fallback line. `RunSelfCheck`
  prints `agent <id>: <text>` per appraisal + `hash <hex> (format <n>)` and
  asserts the tick-1 hash (`--expect` override, default `0xB03F8419E55F3592`)
  and agent 0 `refused route-too-exposed threat-agent-2` / agent 1 `accepted`;
  exit 0 / 1. No scheduling, no command construction, no appraisal branching.
- **`src/CommandoWar.Client.Godot/scenes/AppraisalDemo.tscn` (new).** `format=3`
  scene, root `Node2D` "AppraisalDemo" with the script attached. Godot wrote
  `src/AppraisalDemoScene.cs.uid` on the import pass (committed, the
  `MainNode.cs.uid` precedent).
- **`src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.csproj`.** One
  `<ProjectReference Include="..\CommandoWar.Headless\CommandoWar.Headless.fsproj" />`
  (still a `Client -> Sim` edge; the simulation references neither project).
- **`src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx`.** Added the
  `../CommandoWar.Headless/CommandoWar.Headless.fsproj` row so the client
  `.slnx` build resolves cleanly.
- **`src/CommandoWar.Client.Godot/project.godot`.** `run/main_scene`
  `"res://Main.tscn" -> "res://scenes/AppraisalDemo.tscn"` (one line; the
  greybox `Main.tscn` still builds and runs).
- **`src/CommandoWar.Client.Godot/README.md`.** A TASK-029 "Appraisal-divergence
  demo" section (what it shows, the run / `--selfcheck` / `--screenshot`
  commands, the ADR-0004 deviation note).
- **`tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`.** Two facts before the
  "diagnostics do not perturb the shared fixture" pin:
  - `AppraisalDemo.dispositionText matches the committed golden vocabulary` —
    `Accepted` → `"accepted"`, `Refused(RouteTooExposed(Some 2), [||])` →
    `"refused route-too-exposed threat-agent-2"`, `Unable(NoKnownRoute, [||])` →
    `"unable no-known-route"`, each asserted `Equal` and `Contains`-in the
    relevant golden text.
  - `AppraisalDemo.loadExposedApproachFrames reproduces the tick-1 hash and the
    divergent dispositions` — 13 frames; `frames.[1].Hash.Value =
    0xB03F8419E55F3592`; the tick-1 `OrderAppraisal` overlays are agent 0
    `Refused(RouteTooExposed(Some 2))` / agent 1 `Accepted`; the flat
    `loadFrameViews`.[1] view carries the same divergence (tones `"refused"` /
    `"accepted"`, 10 exposed cells on agent 0, one `ContactRing` for contact 2,
    empty `UnhandledOverlays`).
- **`docs/`.** `docs/06` section 11 "Realised by TASK-029" note; `docs/11` new
  TASK-029 row in section 2 + a B-029 annotation; `docs/12` index row + this
  file + "Green tests" `242 -> 244`; `PROJECT_STATE.yaml` `active_work`;
  `tasks/TASK-029-*.md` (Outcome, Status `draft -> review`, acceptance boxes).

### Verification

- `dotnet build CommandoWar.slnx -c Release` — `Build succeeded. 0 Warning(s)
  0 Error(s)`.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c
  Debug` — `Build succeeded. 0 Warning(s) 0 Error(s)` (CommandoWar.Sim,
  CommandoWar.Headless, CommandoWar.Client.Godot).
- `dotnet test CommandoWar.slnx -c Release` before any edit — `Passed: 242`.
  After — `Passed: 244` (`+2`: the two `DiagnosticsTests` `AppraisalDemo`
  facts).
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus` —
  `OK - all 10 entries match their committed tables` (unchanged).
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture` —
  `canonical format : 4`, initial `0x55F43D66C7AECB7F`, final
  `0x7737282578E821C6`, 34 events (unchanged).
- `git diff content/` — empty (no golden / replay / fixture byte moved).
- `git diff src/CommandoWar.Client.Godot/project.godot` — one line
  (`run/main_scene`).
- `dotnet list src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.csproj
  package --include-transitive` — `Godot.SourceGenerators` / `GodotSharp` /
  `GodotSharpEditor` `4.7.2` (auto), `FSharp.Core 10.1.303` (transitive); no
  other package. `dotnet list ... reference` — `CommandoWar.Sim.fsproj`,
  `CommandoWar.Headless.fsproj`.
- Source scan of `src/CommandoWar.Sim/*.fs` for `godot|node2d|vector2` — only
  pre-existing doc-comment prose in `Diagnostics.fs` / `Scenario.fs`;
  `AppraisalDemo.fs` is in `CommandoWar.Headless`, not `CommandoWar.Sim`.
- Godot import pass: `"$GODOT" --editor --headless --quit --path
  src/CommandoWar.Client.Godot` — `DONE first_scan_filesystem`.
- Headless Godot smoke: `"$GODOT" --headless --path
  src/CommandoWar.Client.Godot -- --selfcheck` —
  ```
  [appraisal-demo] loaded 13 frames from ...\content\replays
  # appraisal-demo self-check: tick 1
  agent 0: refused route-too-exposed threat-agent-2
  agent 1: accepted
  hash 0xB03F8419E55F3592 (format 4)
  MATCH expected hash 0xB03F8419E55F3592, agent 0 Refused / agent 1 Accepted
  ```
  exit 0.
- Screenshot: `"$GODOT" --path src/CommandoWar.Client.Godot --resolution
  1000x620 -- --screenshot docs/evidence/task-029-appraisal-demo.png` — written,
  exit 0. Windowed run (a headless run has no viewport texture — the first
  attempt raised `NullReferenceException` at `GetViewport().GetTexture()`; the
  scene now guards it and the greybox spike's README already used a windowed
  `--screenshot`).
- `git status --porcelain` — `M` on the four client files + `Headless.fsproj` +
  `DiagnosticsTests.fs`; new `AppraisalDemo.fs`, `AppraisalDemo.tscn`,
  `AppraisalDemoScene.cs(.uid)`, `docs/evidence/task-029-appraisal-demo.png`,
  the task file, this ledger file, plus the doc edits. Nothing under
  `src/_scratch`, `bench/`, `content/benchmarks/`. `.godot/` is git-ignored.

### Evidence

- **Two agents appraise the same order differently, visibly:** the committed
  screenshot `docs/evidence/task-029-appraisal-demo.png` (tick 1 — agent 0 with
  a red `R` glyph + `refused route-too-exposed threat-agent-2` in the panel,
  agent 1 with a green `A` + `accepted`, both routes' exposed cells tinted, the
  `?2` known-contact ring on the machine gun); the headless `--selfcheck`
  transcript above; the `DiagnosticsTests` `loadExposedApproachFrames` fact.
- **Same run as the goldens:** `loadExposedApproachFrames` reproduces the
  `exposed-approach.md` tick-1 hash `0xB03F8419E55F3592` and
  `DiagnosticRender.runFrames` uses `SimConfig.standard` (as `Corpus.run`
  does).
- **No `CommandoWar.Sim` change:** `git diff` touches no `src/CommandoWar.Sim`
  file; `-- corpus` 10/10, `-- fixture` and every `content/` golden
  byte-identical; `Canonical.FormatVersion` stays 4.
- **Boundary intact:** `CommandoWar.Client.Godot` references `CommandoWar.Sim`
  and `CommandoWar.Headless` only; the simulation references neither; the flat
  `[<CLIMutable>]` view model is the only shape crossing to C# (ADR-0002
  allow-list "immutable or serializable records").
- **The scene is a thin renderer:** all corpus loading, the frame sequence, the
  disposition mapping, and the flattening are in `AppraisalDemo.fs`; the C#
  `_Draw` / `_Process` / `RunSelfCheck` only marshal Godot types and iterate
  the view model.

### Deviations and unresolved issues

- **The C# scene is a scoped deviation from ADR-0004.** ADR-0004 form 1 is a
  generic C# host resolving an F# `IClientScene`; this demo puts the scene
  (draw calls, slider wiring, HUD strings, arg parsing) in one C# `Node2D`
  instead. Justified: it is a disposable P3 decision-support demo, the logic
  that matters (corpus load, frame sequence, disposition mapping, view model)
  is all in F#, and building the F# client-core is B-026 / B-027's job. **Not a
  precedent** — the P4 client tasks build the real F# client-core over the
  ADR-0004 structure. Recorded in the task file, `docs/11`, `docs/12`, and
  `docs/06`.
- **`AppraisalDemo.dispositionText` duplicates `DiagnosticRender`'s private
  `dispositionText` / `reasonText`** (~8 lines). Deliberate: those are `let
  private`, and Decision E wants the demo's mapping asserted against the
  committed goldens, not against that module. If the golden vocabulary ever
  changes, both move together and the `Contains`-in-golden assertions catch a
  drift.
- **Headless `--screenshot` does not work** — `--headless` has no viewport
  texture, so the capture is a windowed run (the greybox spike's README already
  did this). The `--selfcheck` smoke *does* run headless. The scene guards the
  null and exits 1 with a message if run `--headless --screenshot`.
- **`project.godot` `run/main_scene` now points at the demo.** The greybox spike
  (`Main.tscn`, `MainNode.cs`, ...) is untouched and still builds / runs; only
  the default scene changed. Reverting TASK-029 restores the one line.
- **`--resolution` is passed on the screenshot command line**, not stored in
  `project.godot` (which stays at its authored 1280x800). The committed
  screenshot used `1000x620` to frame the grid + panel + HUD tightly.
- **B-029 backlog row annotated, not moved.** The full P4 item (live input, the
  complete developer-overlay set, the real F# client-core) is unchanged; the
  row now records that a read-only corpus-scoped slice landed early.

### Documents updated

- `tasks/TASK-029-GODOT-APPRAISAL-DEMO.md` (Outcome, Status `draft -> review`,
  acceptance boxes)
- `src/CommandoWar.Headless/AppraisalDemo.fs` (new),
  `src/CommandoWar.Headless/CommandoWar.Headless.fsproj`
- `src/CommandoWar.Client.Godot/{scenes/AppraisalDemo.tscn,
  src/AppraisalDemoScene.cs, src/AppraisalDemoScene.cs.uid}` (new),
  `src/CommandoWar.Client.Godot/{CommandoWar.Client.Godot.csproj,
  CommandoWar.Client.Godot.slnx, project.godot, README.md}`
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`
- `docs/evidence/task-029-appraisal-demo.png` (new)
- `docs/06_CONTENT_AND_PRESENTATION.md` (section 11 "Realised by TASK-029" note)
- `docs/11_BACKLOG.md` (new TASK-029 row in section 2; B-029 annotation)
- `docs/12_PROGRESS_LEDGER.md` (index row; this file; "Green tests"
  `242 -> 244` — no `Canonical.FormatVersion` or fixture-hash change)
- `PROJECT_STATE.yaml` (`active_work`)
- this entry

### AGENTS.md / docs/09 section 8 standing rule

Does not apply in the "adds or changes authoritative spatial or tactical state"
sense — TASK-029 adds no authoritative state, no event, and no `Overlay` case.
It is a **new renderer** of the existing `DiagnosticFrame` (the third, after
`DiagnosticRender.Ascii` / `Svg` / `Html`), and the `docs/06` section 11 /
`docs/09` section 8 realisation notes point at it. The visual-inspectability of
the appraisal divergence it renders is already covered by the committed
`content/diagnostics/exposed-approach-tick-001.*` goldens (TASK-028); this task
adds the Godot view and pins its helper against those goldens.

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: (to be completed on acceptance)
