# TASK-057: Divergence-visualisation renderer

Status: done (implemented and self-verified 2026-09-19; accepted by Dave
2026-09-19)
Owner: Dave
Phase: P3
Gate: G3 (retroactive; determinism tooling, no gate criterion depends on it)
Size: S; realises backlog B-050

## Outcome (2026-09-19)

Implemented as scoped. New `Overlay.Divergence of section: string * agents:
AgentId[]` (`Diagnostics.fs`), caller-supplied only — `frame`/`frameOf`
never emit one, the `SightRay`/`PlannedPath` precedent. `Divergence.fs`
gained `diagnoseDetailed`, a thin wrapper around a new private `runBoth`
(extracted from `diagnose`'s existing two `Replay.run` calls, `diagnose`
itself unchanged) that also returns both full `ReplayOutcome`s so a caller
can index `TickStates` at the divergent tick.

New `cwheadless render-divergence <log-a> <log-b> [--ticks N] [--format
ascii|svg|html] [--out PATH]`. On `Match`/`TruncatedRun` it prints the same
text `compare` would and exits without rendering (exit 0 / 3 respectively,
unchanged from `compare`'s own codes). On `Diverged`, it builds the
reference and candidate `DiagnosticFrame` at the divergent tick via
`Diagnostics.frame`, attaches a `Divergence` overlay to each (`section`
prefixed `"reference: "`/`"candidate: "` so the existing generic `section`
text distinguishes the two frames with no `DiagnosticFrame` schema change;
`agents` parsed from a `"Agent[N]"` `Canonical.firstDifferingSection` label
only, empty for a top-level section). `--format ascii` prints both frames in
sequence; `--format svg` writes two files (`<out>-reference.svg`/
`-candidate.svg`) or prints both `<svg>` documents to stdout separated by an
XML comment when `--out` is omitted (a single root-element SVG cannot hold
two frames); `--format html` embeds both frames in the existing
`DiagnosticRender.Html` two-tick scrubber.

Renderer changes: `Ascii`'s main overlay loop prints
`DIVERGED: first differing section <section>  agent <ids>`; `Svg` draws a
thick solid magenta square (`stroke="#ff00ff"`, `stroke-width="4"`) around
each named agent's cell — deliberately unlike every other overlay's thin or
dashed stroke, since this is the one thing the render exists to draw
attention to — plus a footer line naming the section (the footer grows by
one line, `52 -> 66`, only when a `Divergence` overlay is present); `Html`'s
`annotations` panel gets an optional `<p class="cw-diverged">` banner
(wildcard-matched, no exhaustiveness change needed there). Two other
existing exhaustive `Overlay` matches in `Ascii` (the `sightRays`/
`plannedPaths` `--los`/`--path` filters) needed a `| Divergence _ -> None`
arm each.

Five exhaustive-match sites elsewhere in the tree needed one new arm each,
found by a full-tree grep before drafting (recorded in the task file's
Required reading) rather than discovered by the compiler alone:
`AppraisalDemo.fs`'s `unhandled`-overlay tracker (this disposable demo can
never actually produce one — a divergence only exists by comparing two
independently replayed runs), and four sparse-filter `Array.choose`/
`Array.tryPick` blocks in `DiagnosticsTests.fs`. `RenderShared.fs`
(Godot client) needed no change — confirmed by the same grep that its only
`Overlay` match is already a wildcard `tryPick`.

New `DiagnosticsTests.fs` fact (`Divergence.diagnoseDetailed's states render
as a Divergence overlay naming the diverging agent and section, in every
format`) drives the exact scenario `ReplayTests.fs`'s own "a mutated command
destination is reported as a divergence at the changed tick" fact already
proves diverges at tick 1 on `Agent[0]` (two command logs sending the same
agent to two different tick-1 destinations), then builds both frames the
same way the new CLI verb does and asserts the `DIVERGED`/`Agent[N]`/agent-id
text appears in `Ascii`, `Svg` (plus the magenta stroke), and `Html`.

Manual CLI smoke test (two hand-built `.cwlog` files, agent 3 sent to two
different tick-1 destinations): `render-divergence` on two identical logs
printed `MATCH: 2 tick(s) compared...` and exited 0, no frames rendered; on
the diverging pair it printed `first differing section: Agent[3]`/`diverged
at tick: 1`, rendered both frames in `ascii` (each carrying the `DIVERGED...
agent 3` overlay line, confirmed the reference/candidate frames show
different agent-3 cells and destinations), wrote two correctly-suffixed
`.svg` files (`grep`-confirmed the magenta stroke and the footer text in
each), and wrote one `.html` file (`grep`-confirmed two `cw-diverged`
banners, the reference/candidate scrubber frames) — exit 3 in every
`Diverged` case. Scratch files removed after use.

`dotnet build` all three `.slnx` (main Release; Godot client Debug) `0/0`;
`dotnet test` `344/344` (+1); `cwheadless corpus` `16/16` (unaffected, as
expected — no `Canonical.FormatVersion`/hash change, a pure observer
addition). `git status --porcelain` matches this task's allowed scope
exactly.

Full detail:
`docs/ledger/2026-09-19-TASK-057-divergence-visualisation-renderer.md`.

## Objective

A `cwheadless` verb that renders the first differing tick of two replayed
command logs — ASCII, SVG, and into the existing HTML scrubber — with the
diverging agent (when the divergence names one) and the first differing
canonical section highlighted. Turns a determinism regression from a
hash-diff hunt (`cwheadless compare`'s text-only report) into a visual
glance at the actual diverging state.

## Why this task exists

`docs/notes/2026-09-06-tooling-and-debug-display.md` section 2 named this
the one real gap in the P3 headless dev loop: `Divergence.diagnose` /
`Canonical.firstDifferingSection` already name the first bad tick and the
first differing canonical section in text (`cwheadless compare`), but
nothing renders it. `DiagnosticRender.Svg`/`.Html` already exist and are
byte-deterministic; the data `Divergence.diagnose` produces is already
sufficient — no new determinism machinery. Selected by Dave via
`AskUserQuestion` on 2026-09-19 over B-016b (communication mechanics) and
B-043 (Mibo reconsideration spike), to be done in the same session as
B-016b.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`.
- `docs/notes/2026-09-06-tooling-and-debug-display.md` section 2 (the
  original proposal).
- `src/CommandoWar.Sim/Divergence.fs` (`DivergencePoint`, `DivergenceReport`,
  `Divergence.compare`/`.diagnose`).
- `src/CommandoWar.Sim/Canonical.fs` (`firstDifferingSection` — returns a
  top-level section name or `Agent[N]`, never more than one label; this is
  the existing "first divergence" scope this task reuses, not a new
  multi-agent diff).
- `src/CommandoWar.Sim/Diagnostics.fs` (`Overlay` DU — every case's doc
  comment states whether `Diagnostics.frame`/`.frameOf` derives it or a
  caller must supply it; `SightRay`/`PlannedPath` are the "caller supplies
  it, `Diagnostics` never emits it" precedent this task's new case follows).
- `src/CommandoWar.Headless/DiagnosticRender.fs` (`Ascii`, `Svg`, `annotations`
  (used by `Html`) — every one matches `Overlay` either exhaustively
  (`Ascii`'s `sightRays`/`plannedPaths` filters, the `Ascii` and `Svg` main
  overlay-line loops) or via a wildcard `tryPick`/`choose` (`annotations`).
- `src/CommandoWar.Headless/Program.fs` (`cmdCompare` — the existing
  text-only `compare` verb this task's new verb parallels; `cmdRender` —
  the `--format ascii|svg|html`/`--out PATH` argument conventions to reuse;
  `loadLog`, `optTicks`).
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`,
  `src/CommandoWar.Headless/AppraisalDemo.fs`,
  `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs` — every exhaustive match
  over `Overlay` elsewhere in the tree that a new case must compile against
  (confirmed by grep before drafting: `RenderShared.fs`'s only `Overlay`
  match is a wildcard `tryPick`, needs no change; `AppraisalDemo.fs`'s
  `unhandled`-overlay tracker and `DiagnosticsTests.fs`'s four sparse-filter
  `Array.choose` blocks are exhaustive and need one new arm each).

## Dependencies

- B-012 (TASK-016, replay corpus / `Divergence` infrastructure) — done.

## Inputs and assumptions

- The divergence is always reported as a single first differing point
  (`Divergence.diagnose`'s existing scope, docs/09 section 2.4) — this task
  visualises that single point, it does not build a multi-tick or
  multi-agent diff view.
- `Canonical.firstDifferingSection` returns at most one label: a top-level
  section name (e.g. `"Random"`, `"TacticalKnowledge"`) or `"Agent[N]"`.
  Only the latter names an agent to highlight; every other label highlights
  no agent (the overlay still names the section in text).
- Both replayed command logs run from the same initial state
  (`Fixture.initialState()`, the existing `compare` verb's own assumption —
  not changed here).

## Allowed scope

- `src/CommandoWar.Sim/Diagnostics.fs`: one new `Overlay` case,
  `Divergence of section: string * agents: AgentId[]`, documented as
  caller-supplied only (the `SightRay`/`PlannedPath` precedent) — `frame`/
  `frameOf` never emit one.
- `src/CommandoWar.Sim/Divergence.fs`: a new `diagnoseDetailed` (or
  equivalent) alongside the existing `diagnose`, returning the two full
  `ReplayOutcome`s (their `TickStates`) as well as the `DivergenceReport`,
  refactored out of `diagnose`'s existing two `Replay.run` calls — `diagnose`
  itself keeps its current signature and behaviour unchanged.
- `src/CommandoWar.Headless/DiagnosticRender.fs`: a render arm for
  `Divergence` in `Ascii`'s main overlay loop and its two exhaustive
  `sightRays`/`plannedPaths` filters (`| Divergence _ -> None`), a render
  arm in `Svg`'s main overlay loop (a highlighted cell marker for a named
  agent, or a footer-only note when the section names none), and an
  optional one-line divergence banner in `annotations` (wildcard-based, no
  exhaustiveness change needed there).
- `src/CommandoWar.Headless/Program.fs`: a new `render-divergence` verb
  (`cwheadless render-divergence <log-a> <log-b> [--ticks N]
  [--format ascii|svg|html] [--out PATH]`), the `cmdCompare`/`cmdRender`
  argument-handling precedent; usage text.
- `src/CommandoWar.Headless/AppraisalDemo.fs`: one new `unhandled.Add(...)`
  arm for `Divergence` (this disposable demo never produces one from its
  own committed frame — the `AgentAmmo`/`SquadLeadership` precedent for a
  case that predates it).
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`: one new arm in each of
  the four exhaustive sparse-filter blocks that currently end
  `| AgentAmmo _ -> None)`; a new focused test proving the render path
  end-to-end against two deliberately diverging command logs.
- `docs/11_BACKLOG.md` (B-050 row), `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.

## Forbidden scope

- No change to `Canonical.FormatVersion`, `Canonical.encode`, or any
  existing pinned hash — this is a pure observer/tooling addition (ADR-0002,
  the `SightRay`/`PlannedPath`/`Reserved` precedent already established for
  the `Overlay` DU).
- No multi-tick (N-1/N/N+1) or multi-agent diff view — out of scope per
  Inputs and assumptions above; the note's own minimal "one frame with the
  diverging agent and section highlighted" is what B-050 asks for.
- No change to `cwheadless compare`'s existing text-only behaviour or exit
  codes.
- No `CommandoWar.Client.Godot` change (a headless CLI tool; `RenderShared.fs`
  needs no change since its only `Overlay` match is already a wildcard).

## Required work

1. `Diagnostics.fs`: add the `Divergence` `Overlay` case with a doc comment
   matching the DU's existing per-case documentation convention, and a line
   in the DU's own leading "B-0xx -> Case" list (`B-050 divergence ->
   Divergence (realised by TASK-057)`).
2. `Divergence.fs`: extract `diagnose`'s two `Replay.run` calls into a
   private `runBoth`; add `diagnoseDetailed` returning
   `Result<DivergenceReport * ReplayOutcome * ReplayOutcome, ReplayError>`;
   `diagnose` itself calls `runBoth` too and its own signature/behaviour is
   unchanged (verify with the existing `ReplayTests`/`CorpusTests` facts
   that already exercise it).
3. `DiagnosticRender.fs`: implement the three render arms described in
   Allowed scope. SVG: a distinct highlight (not a reused colour/shape —
   the `Reserved`/`Obstructed`/`KnownContact` "deliberately distinct glyph"
   precedent) around each named agent's cell, plus a footer line naming the
   section; when no agent is named, the footer line alone carries it.
4. `Program.fs`: implement `cmdRenderDivergence`. On `Match`/`TruncatedRun`,
   print the existing `compare`-style text report and exit without
   rendering (nothing to render). On `Diverged`, build the reference and
   candidate `DiagnosticFrame`s via `Diagnostics.frame` on each side's
   `TickStates.[i]` at the divergent index, attach a `Divergence` overlay to
   each (`section` prefixed `"reference: "`/`"candidate: "` so every
   renderer's already-generic `section` text distinguishes the two frames
   without a `DiagnosticFrame` schema change; `agents` parsed from a
   `"Agent[N]"` section label only). `--format ascii`: print both frames in
   sequence with a header line each (the `cmdTurn` precedent). `--format
   svg`: write two files when `--out PATH` is given (`PATH` with
   `-reference`/`-candidate` inserted before its extension), or print both
   `<svg>` documents to stdout separated by an XML comment header when
   `--out` is omitted. `--format html`: `DiagnosticRender.Html [|
   referenceFrame; candidateFrame |]`, one `--out` file (or stdout). Add
   `render-divergence` to `usage()`.
5. `AppraisalDemo.fs`: add the one new `unhandled` arm.
6. `DiagnosticsTests.fs`: add the four new `| Divergence _ -> None` arms;
   add a new fact driving `Program`-equivalent logic directly (construct two
   deliberately diverging command logs against `Fixture.initialState()`,
   call `Divergence.diagnoseDetailed`, build both frames with the overlay
   attached, and assert on the rendered `Ascii`/`Svg` text — the
   `DiagnosticsTests` golden-fact precedent, no CLI process spawn needed).
7. Verify with a temporary `dotnet fsi` scratch probe (removed after use) or
   the new `DiagnosticsTests` fact: confirm the CLI verb's exit codes match
   `Match`/`TruncatedRun`/`Diverged` correctly, and that the rendered output
   actually names the tick, section, and (when applicable) agent that
   `cwheadless compare` already reports in text for the same two logs.
8. Update backlog/ledger/state.

## Acceptance criteria

- [x] `cwheadless render-divergence <log-a> <log-b>` on two logs that
      diverge renders both the reference and candidate `DiagnosticFrame` at
      the first differing tick, in `ascii`/`svg`/`html` (default `ascii`),
      each visually distinguishable as reference vs candidate.
- [x] The first differing canonical section (`Canonical.firstDifferingSection`'s
      own label) appears in every rendered format.
- [x] When that section names a specific agent (`Agent[N]`), that agent's
      cell is visually highlighted in the SVG/HTML render, distinctly from
      every existing overlay glyph.
- [x] On two logs that do not diverge (`Match`) or run different lengths
      (`TruncatedRun`), the verb reports that outcome (the `compare`-style
      text) and exits without attempting to render, with the correct exit
      code.
- [x] No `Canonical.FormatVersion` bump, no existing pinned hash moves, no
      `CommandoWar.Sim` behaviour change — this is an observer addition
      only.
- [x] `dotnet build`/`dotnet test` green; every existing exhaustive `Overlay`
      match site outside this task's allowed scope still compiles unchanged
      (confirms the grep in Required reading was complete).
- [x] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: expect `0/0`.
- `dotnet test`: expect unaffected count plus the new fact(s).
- `cwheadless corpus`: expect `16/16` unaffected (no canonical/hash change).
- Manual CLI smoke: two hand-built `.cwlog` files that genuinely diverge
  (e.g. sending the same agent to two different destinations) through
  `cwheadless render-divergence ... --format ascii|svg|html`, and two
  identical logs (`Match`) through the same verb, checking exit codes.
- `git status --porcelain`: matches this task's allowed scope.

## Evidence to capture

- The new `DiagnosticsTests` fact's pass.
- Manual CLI smoke output (ascii text; SVG/HTML file existence and a visual
  spot-check that the highlighted agent/section is correct).

## Expected files

- `src/CommandoWar.Sim/Diagnostics.fs`, `Divergence.fs`.
- `src/CommandoWar.Headless/DiagnosticRender.fs`, `Program.fs`,
  `AppraisalDemo.fs`.
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`.
- `tasks/TASK-057-DIVERGENCE-VISUALISATION-RENDERER.md`.
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md` (+ new `docs/ledger/`
  detail file), `PROJECT_STATE.yaml`.

## Documentation updates

- This task file's Outcome section.
- `docs/11_BACKLOG.md` (B-050 row: `proposed -> done`).
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Fully additive: no canonical/hash change, no existing verb's behaviour
changed. Revertible with `git revert` in one step.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task (B-016b is already queued directly after this one, per Dave's own
instruction this session, not a default next-task offer).

## Review

- Reviewer: Dave.
- Accepted: yes (2026-09-19). No client UI in this task (a headless CLI
  tool); accepted on the self-verification evidence (build/test/corpus
  green, manual CLI smoke test of all three formats), the TASK-023/036/050
  precedent for Sim/tooling-only work.
