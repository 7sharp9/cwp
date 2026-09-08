# TASK-029: Godot appraisal-divergence demo

Status: draft (central decisions A–F confirmed with Dave 2026-09-08 before the
phase bodies; draft-then-implement in the TASK-028 shape)
Owner: Dave
Phase: P3
Gate: G3 (command loop) — decision-support for the refuse-near-threats feel
before B-019; pulls a read-only slice of backlog B-029 (P4/G4) forward
Size: S–M

## Objective

A runnable Godot scene that loads the committed `exposed-approach` corpus entry,
builds its `DiagnosticFrame` sequence through the existing headless helper
(`DiagnosticRender.runFrames`), and renders the authoritative state plus the
appraisal overlay with a tick slider: agents by side, the `KnownContact` ring
for the machine gun (contact 2), each agent's `OrderDisposition` (agent 0
`Refused RouteTooExposed threat-agent-2`, agent 1 `Accepted`) with its
`DecisionReason` as readable text, and the exposed route cells tinted.

This is the visual proof of `docs/07` section 9 criterion 2 ("at least two
soldiers appraise the same order differently for traceable reasons") and
criterion 11 ("the developer overlay can explain any appraisal"), and lets Dave
judge the refuse-near-threats feel before B-019. Recent simulation progress
(TASK-026 / TASK-027 / TASK-028) is all headless; this is the first Godot
realisation of the `Diagnostics.frame` renderer family.

## Why this task exists

- `docs/06` section 11 and `docs/09` section 8 name the Godot developer overlay
  (backlog B-029) as a third renderer of `DiagnosticFrame`, after the headless
  ASCII / SVG / HTML renderers. Nothing renders a frame in Godot yet.
- `docs/07` section 9 criteria 2 and 11 are G3/G4 acceptance criteria with no
  visual realisation: the `exposed-approach` divergence exists only as an ASCII
  and SVG golden.
- The sim work since the last Godot demo (54534de, 2026-09-02) is entirely
  headless. Dave wants to see the divergence and judge the feel before the
  combat slice (B-019) builds on the `Refused` disposition.
- B-029 proper is P4 and builds the full overlay set over **live** input as part
  of the real F# client-core (B-026 / B-027). This task lands only a read-only,
  corpus-scoped slice as P3 decision-support; B-029 stays P4.

## Required reading

Read in this order; verify each path and inspect the source before trusting a
filename (`AGENTS.md`).

1. `PROJECT_STATE.yaml`, `AGENTS.md`.
2. `docs/ledger/2026-09-08-TASK-028-order-appraisal-and-typed-reasons.md` — the
   `exposed-approach` entry (hashes, tick/event counts, the divergence), the
   `Overlay.OrderAppraisal` shape, the `DiagnosticRender` disposition vocabulary.
3. `decisions/ADR-0002-SIMULATION-BOUNDARY.md` — dependency direction
   (`Client -> ... -> Sim`, never the reverse); the allow/forbid data lists.
4. `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` — "a thin C# host over an F#
   client-core library", "no logic in C#", the interop idiom. **This task takes
   a scoped deviation from the "F# client-core / no logic in C#" rule** (Decision
   A) for a disposable P3 decision-support demo — see Decision F.
5. `docs/06` section 11 (required tactical overlays; the TASK-011 realisation
   note), `docs/07` sections 6 and 9, `docs/09` section 8.
6. `docs/11` B-029 row; `docs/03` section 14 (Godot host boundary).
7. `src/CommandoWar.Sim/Diagnostics.fs` — `DiagnosticFrame`, `Overlay`
   (`OrderAppraisal`, `KnownContact`, `PlannedPath` are the cases the
   `exposed-approach` frames carry), `Diagnostics.frame` / `frameOf`.
8. `src/CommandoWar.Headless/{DiagnosticRender.fs, Corpus.fs}` —
   `DiagnosticRender.runFrames`, the `Svg` overlay vocabulary and colours,
   `Corpus.all` / `Corpus.loadLog` / the `exposed-approach` `Entry`.
9. `src/CommandoWar.Client.Godot/` — `CommandoWar.Client.Godot.csproj`
   (net10.0, `EnableDynamicLoading`, the `CommandoWar.Sim` `ProjectReference`),
   `CommandoWar.Client.Godot.slnx`, `nuget.config` (offline Godot feed),
   `project.godot` (`run/main_scene`), `Main.tscn`, `src/MainNode.cs` /
   `src/SimFacade.cs` (the greybox spike — the C# host / self-check /
   `--screenshot` style, **left untouched**).
10. `src/_scratch/godot-fsharp-boundary/{src/FSharpSceneHost.cs,
    ClientCore/Scene.fs}` — the ADR-0004 reference shape only; **not wired in**.
11. `content/replays/exposed-approach.{cwlog,md}`,
    `content/diagnostics/exposed-approach-tick-001.{ascii.txt,svg}`,
    `content/diagnostics/blocked-goal-tick-001.ascii.txt`.
12. `tests/CommandoWar.Sim.Tests/{DiagnosticsTests.fs, CorpusTests.fs}` — the
    `AppContext.BaseDirectory` golden / corpus dir helpers, the
    `exposedApproachFrames ()` helper, the golden byte-compare style.

## Dependencies

- TASK-028 (appraisal, `exposed-approach` corpus entry + golden, accepted and
  merged to `main` 2026-09-08). TASK-004 (the Godot spike skeleton). ADR-0001 /
  ADR-0002 / ADR-0004 (accepted).

## Central decisions (confirmed with Dave 2026-09-08)

### Decision A — build on the existing spike skeleton; all non-trivial logic in a new F# helper — CONFIRMED

Build on `src/CommandoWar.Client.Godot`. Keep the pinned `.csproj`,
`nuget.config`, `project.godot`, `.godot`, `.gitignore`. Add a
`ProjectReference` to `CommandoWar.Headless` (which already references
`CommandoWar.Sim`). Add `scenes/AppraisalDemo.tscn` + a small C# renderer script
(`src/AppraisalDemoScene.cs`) and make it `run/main_scene` in `project.godot`.
Leave the greybox spike files (`MainNode.cs`, `SimFacade.cs`, `GreyboxScene.cs`,
`SpikeContent.cs`, `SpikeMarker.cs`, `Main.tscn`, `scenes/Greybox*.tscn`)
**untouched**.

All non-trivial logic — corpus load, `runFrames` call, disposition → label /
tone mapping, view-model prep — goes in a **new F# helper
`src/CommandoWar.Headless/AppraisalDemo.fs`** (added to
`CommandoWar.Headless.fsproj` after `DiagnosticRender.fs`), covered by xUnit in
`tests/CommandoWar.Sim.Tests`. The C# script only: resolve the repo-relative
content dir, call the helper, hold the tick index, draw frames, handle
slider / arrow input, draw the HUD.

Keep `CommandoWar.Client.Godot` **out of `CommandoWar.slnx`**; build / verify it
via its own `.slnx`.

This C#-scene approach is a **scoped deviation** from ADR-0004's "F#
client-core / no logic in C#" rule, justified for a disposable P3
decision-support demo. Recorded in this task file and the ledger as **NOT a
precedent** for the P4 client tasks (B-026 / B-027 / B-029), which build the
real F# client-core.

### Decision B — scenario source: corpus reuse — CONFIRMED

`Corpus.all` → the entry with `Name = "exposed-approach"`; `Corpus.loadLog`
against a path resolved repo-relative from `res://`
(`ProjectSettings.GlobalizePath("res://")` + `"../../content/replays"`), reading
the exact committed `.cwlog` and **failing loud if absent**;
`DiagnosticRender.runFrames`. Same run as the goldens (both `runFrames` and
`Corpus.run` use `SimConfig.standard`). No authored scene file, no copied
`.cwlog`.

### Decision C — scrubbable: 13 frames (tick 0..12) — CONFIRMED

`HSlider` plus `Left` / `Right` arrow keys. Default to index 1 (the divergence
tick). Mirrors `DiagnosticRender.Html`.

### Decision D — overlay set: mirror the `DiagnosticRender.Svg` vocabulary exactly — CONFIRMED

Integer px/cell, same colours / glyphs as `DiagnosticRender.Svg` (a
Godot-window integer up-scale of the SVG's `Scale = 16` is allowed for
legibility; proportions and colours are preserved):

- terrain grid (`exposed-approach` is all passable — a plain grid);
- agents: circle `#2b6cb0` friendly / `#c53030` hostile, white stroke, dashed
  destination line in the same colour;
- the `KnownContact` ring for contact 2: no-fill circle stroke `#805ad5`
  dashed + `?2` text `#805ad5`;
- the `PlannedPath` polyline for the accepted agent: `#dd6b20` stroke, green
  start disc `#2f855a`, goal rect stroke `#dd6b20`;
- the `OrderAppraisal` overlay: exposed cells as `#c53030` `fill-opacity 0.25`
  squares, then a bold `A` / `R` / `U` glyph at the agent cell in `#2f855a` /
  `#c53030` / `#718096`;
- a side panel with the readable `DecisionReason` line per agent;
- a HUD with tick / hash / format / draws (the SVG footer trio).

Render only the overlay cases the `exposed-approach` frames actually carry
(`PlannedPath`, `KnownContact`, `OrderAppraisal`); a one-line on-screen fallback
lists any unhandled `Overlay` case.

### Decision E — verification / test boundary — CONFIRMED

1. `AppraisalDemo.fs` is pure and tested in `tests/CommandoWar.Sim.Tests`:
   - an xUnit fact that its disposition → text mapping matches the committed
     goldens for each outcome — `"accepted"` and
     `"refused route-too-exposed threat-agent-2"` from
     `content/diagnostics/exposed-approach-tick-001.ascii.txt`, and
     `"unable no-known-route"` from
     `content/diagnostics/blocked-goal-tick-001.ascii.txt` (assert against the
     golden text; **do not** reach into `DiagnosticRender`'s `let private`
     formatters);
   - a fact that `AppraisalDemo.loadExposedApproachFrames` reproduces the
     `exposed-approach.md` tick-1 hash (`0xB03F8419E55F3592`) and the two
     tick-1 dispositions (agent 0 `Refused RouteTooExposed (Some 2)`, agent 1
     `Accepted`).
2. A headless Godot smoke (`--selfcheck`-style arg) prints the tick-1
   dispositions + hash and asserts against the golden hash, using the pinned
   editor binary; a committed screenshot is captured headlessly
   (`--screenshot`) at `docs/evidence/task-029-appraisal-demo.png` (the
   TASK-004 / TASK-007 evidence-screenshot precedent).
3. Dave launches the scene interactively and confirms the divergence reads
   right — stated as a **Dave step** in the report, not claimed.

### Decision F — no new ADR — CONFIRMED

ADR-0001 (Godot accepted), ADR-0002 (`Client → Sim` allowed; `Sim` has no Godot
reference — verified with a source scan), and ADR-0004 (client structure)
already cover this. Recorded in this task file + the ledger, **not** as an ADR:

- **(i)** the C#-scene deviation from ADR-0004 (Decision A), scoped to this
  disposable demo — not a precedent for the P4 client tasks;
- **(ii)** that TASK-029 pulls a read-only, corpus-scoped slice of B-029 (the
  P4/G4 backlog row) forward into P3 as decision-support — B-029 proper stays
  P4 and builds the full overlay set over live input.

## Deliverables

- `scenes/AppraisalDemo.tscn` + `src/AppraisalDemoScene.cs` in
  `src/CommandoWar.Client.Godot`; `project.godot` `run/main_scene` repointed.
- `src/CommandoWar.Headless/AppraisalDemo.fs` + its xUnit coverage in
  `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`.
- A short launch note appended to `src/CommandoWar.Client.Godot/README.md`.
- A committed screenshot at `docs/evidence/task-029-appraisal-demo.png`.

## Allowed scope

- `src/CommandoWar.Headless/AppraisalDemo.fs` (**new**), and the `<Compile>`
  entry for it in `CommandoWar.Headless.fsproj` after `DiagnosticRender.fs`.
- `src/CommandoWar.Client.Godot/`: `CommandoWar.Client.Godot.csproj` (one
  `ProjectReference` to `CommandoWar.Headless`), `project.godot`
  (`run/main_scene`), `scenes/AppraisalDemo.tscn` (**new**),
  `src/AppraisalDemoScene.cs` (**new**), `README.md` (launch note). Godot may
  regenerate `.godot/` and write `*.cs.uid` on the import pass.
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs` — the two `AppraisalDemo`
  facts.
- `docs/evidence/task-029-appraisal-demo.png` (**new**).
- Docs — see "Documentation updates".

## Forbidden scope

- **Any** change to `src/CommandoWar.Sim` — no new event, no new `Overlay`
  case, no `Canonical.FormatVersion` bump, no hash / golden re-pin.
- `AppraisalConfig` / appraisal logic; new authoritative state; combat /
  suppression / commitments (B-018 / B-019 / B-020).
- The greybox spike files (`MainNode.cs`, `SimFacade.cs`, `GreyboxScene.cs`,
  `SpikeContent.cs`, `SpikeMarker.cs`, `Main.tscn`, `scenes/Greybox*.tscn`).
- Adding `CommandoWar.Client.Godot` to `CommandoWar.slnx`.
- A new NuGet / Godot dependency without an accepted ADR or explicit permission
  (pin framework / package versions).
- The `src/_scratch` reference (consume the shape, do not wire it in), `bench/`,
  `src/_scratch`, `content/benchmarks/BASELINE.md`.
- An ADR (Decision F).

## Acceptance criteria

- [ ] `src/CommandoWar.Headless/AppraisalDemo.fs` is a pure, total helper:
      `loadExposedApproachFrames : string -> DiagnosticFrame[]` (over
      `Corpus.all` + `Corpus.loadLog` + `DiagnosticRender.runFrames`,
      failing loud on a missing entry / log), a disposition → readable-text
      mapping matching the committed golden vocabulary, a disposition → tone
      mapping, and a flat `[<CLIMutable>]` per-tick view-model for the C#
      renderer (no F# `option` / DU / tuple crosses the boundary). No Godot
      reference; no mutation of the simulation.
- [ ] `CommandoWar.Headless.fsproj` compiles `AppraisalDemo.fs` after
      `DiagnosticRender.fs`; `dotnet build CommandoWar.slnx -c Release` = 0/0.
- [ ] `scenes/AppraisalDemo.tscn` + `src/AppraisalDemoScene.cs`;
      `project.godot` `run/main_scene = "res://scenes/AppraisalDemo.tscn"`;
      `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx
      -c Debug` = 0/0.
- [ ] The C# script only resolves the content dir, calls `AppraisalDemo`, holds
      the tick index, draws from the view-model, handles the slider / arrow
      keys, draws the HUD, and runs the `--selfcheck` / `--screenshot` modes —
      no scheduling, no command construction, no appraisal branching.
- [ ] The scene renders, for the selected tick: the grid, agents by side with
      destination lines, the `?2` `KnownContact` ring, the accepted agent's
      `PlannedPath` polyline, the `OrderAppraisal` exposed-cell tint + `A`/`R`/`U`
      glyph, a per-agent `DecisionReason` panel, and a tick/hash/format/draws
      HUD, with the `DiagnosticRender.Svg` colours / glyphs. A one-line fallback
      lists any unhandled `Overlay` case.
- [ ] `HSlider` + `Left`/`Right` scrub ticks 0..12; default index 1.
- [ ] `DiagnosticsTests.fs`: the disposition-text-matches-golden fact and the
      `loadExposedApproachFrames` tick-1 hash + dispositions fact.
      `dotnet test CommandoWar.slnx -c Release` green — state before (242) and
      after count.
- [ ] Headless Godot smoke: dispositions + tick-1 hash `0xB03F8419E55F3592`
      match the golden; screenshot captured at
      `docs/evidence/task-029-appraisal-demo.png`.
- [ ] `cwheadless corpus` 10/10 and `cwheadless fixture` byte-identical; `git
      diff` on `content/replays` and `content/diagnostics/*.{ascii.txt,svg}`
      empty for the existing entries.
- [ ] `dotnet list
      src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.csproj package
      --include-transitive` — only the intended pinned refs (`Godot.NET.Sdk`
      4.7.2 stack + the two project refs).
- [ ] Source scan of `src/CommandoWar.Sim` for `godot|node2d|vector2` — clean.
- [ ] `git status --porcelain` matches the Allowed scope (nothing under
      `src/_scratch`, `bench/`, `content/benchmarks/`).
- [ ] Docs updated (see below).

## Required verification

- `dotnet build CommandoWar.slnx -c Release` = 0/0.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c
  Debug` = 0/0.
- `dotnet test CommandoWar.slnx -c Release` — before (242) and after count;
  name the added facts.
- `cwheadless corpus` 10/10; `cwheadless fixture` byte-identical; `git diff`
  `content/replays` and `content/diagnostics/*.{ascii.txt,svg}` empty for the
  existing entries.
- `dotnet list src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.csproj
  package --include-transitive` — only the intended pinned refs.
- Source scan of `src/CommandoWar.Sim` for `godot|node2d|vector2` — clean.
- Headless Godot smoke: tick-1 dispositions + hash match the golden.
- `--screenshot` capture committed.
- `git status --porcelain` matches the Allowed scope.

## Documentation updates

- this task file (Outcome, Status, acceptance boxes);
- `docs/11_BACKLOG.md`: a TASK-029 row in section 2, and annotate the B-029 row
  (P3 read-only corpus-scoped slice landed by TASK-029; the full item stays
  P4);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/`
  `2026-09-08-TASK-029-godot-appraisal-demo.md` detail file. No pinned-fact
  change expected — state so.
- `PROJECT_STATE.yaml` `active_work`;
- `docs/06` section 11 (or `docs/03` section 14) note: the Godot `DiagnosticFrame`
  renderer now has a first read-only, corpus-scoped realisation
  (`src/CommandoWar.Client.Godot/scenes/AppraisalDemo.tscn`).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start, accept, merge, or
offer the next task — Dave triggers acceptance separately.
