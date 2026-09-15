## 2026-09-15 - DIAG-001 - demo.html per-tick narration and agent-state annotations

**Owner:** Dave with coding-agent assistance
**Source revision:** `bafaccb` (Merge TASK-034) — the local tip of `main` at
the start of this work; done directly on `main` per Dave's explicit
lightweight-scope request (no task file, no branch, no ledger drafting
ceremony — implement, then record).
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; xUnit 2.9.3
**Status change:** none (`PROJECT_STATE.yaml` `active_work.selected_task`
stays `none`; no phase/gate/decision changed). Presentation-only change:
`Canonical.FormatVersion` untouched, no new event type, no `WorldState` /
`StepResult` field added (docs/04 section 17 — presentation state is never
in the canonical image).

### Changes

- `src/CommandoWar.Headless/DiagnosticRender.fs`:
  - Extracted `commitmentText` (the `AgentCommitment` overlay's "holding" /
    "moving to (x,y)" text) into a shared private function, used by both the
    `Ascii` overlay line (identical output, no golden change) and the new
    HTML panel.
  - New `eventNarration`: a readable sentence per `EventMarker.Kind`, used
    only by the HTML panel — distinct from `Ascii`'s terse `kind@cell,cell`
    events line, built from the same `Kind` / `Cells` fields so it needs
    nothing `Ascii` does not already have.
  - New `agentRow` / `contactText` / `annotations`: for one `DiagnosticFrame`,
    builds an HTML fragment with (1) a narrated `<ul>` of this tick's events,
    (2) a `<table>` with one row per agent joining `AgentMarker` (cell,
    destination, progress, comms) with its `OrderAppraisal` (TASK-028),
    `AgentCommitment` (TASK-030), `AgentSuppression` (TASK-032), and
    `AgentStress` (TASK-033) overlays, and (3) the two shared tactical
    pictures (`KnownContact` / `HostileKnownContact`, TASK-026/034) listed
    once each, since they are squad-shared state, not per-agent.
  - `Html`: appends `annotations f` after `Svg f` inside each tick's frame
    `div`; a few CSS rules added to the existing inline `<style>` block for
    the new `.cw-annotations` / `.cw-agents` / `.cw-narration` /
    `.cw-contacts` classes.
  - `Ascii` and `Svg` are otherwise unchanged — no new `Overlay` case, no new
    `GridLayer` / `EdgeMarker`, no change to any function's output shape
    other than `Html`.
- `content/diagnostics/demo.html`: regenerated (see Verification). Byte
  content changes throughout (every tick's `div` now carries the annotation
  panel after its SVG); no other committed golden changes — `Ascii` and `Svg`
  output is untouched, so every other golden file in `content/diagnostics/`
  is unaffected.
- `content/diagnostics/README.md`: `demo.html` row's description extended to
  describe the annotation panel.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)`.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  render demo --format html --out content/diagnostics/demo.html`
  - Result: `wrote content/diagnostics/demo.html (311107 bytes)`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed! - Failed: 0, Passed: 287, Skipped: 0, Total: 287` — the
    `HTML render of the demo run is byte-equal to the committed golden` fact
    and the `HTML output parses as XML and has one frame element per tick`
    fact both pass against the regenerated file; every `Ascii` / `Svg`
    golden fact is unaffected (those renderers were not changed).
- Manual check: read the regenerated `demo.html` at tick 1 (both friendlies
  freshly ordered, no contacts yet) and tick 8 (agent 0's order refused as
  `route-too-exposed threat-agent-5`, both sides have just sighted each
  other, a shot exchanged) — the narration, per-agent table, and both
  tactical-picture lines correctly reflect the scenario at each tick.
- Command: `git status --porcelain`
  - Result: `src/CommandoWar.Headless/DiagnosticRender.fs`,
    `content/diagnostics/demo.html`, `content/diagnostics/README.md`,
    `docs/12_PROGRESS_LEDGER.md`, and this detail file — plus a pre-existing,
    unrelated `src/CommandoWar.Client.Godot/project.godot` modification that
    predates this session and was left untouched.

### Evidence against acceptance criteria

No task file, so no bounded acceptance criteria; the request's own success
condition ("a reviewer can see per-tick narration and agent state in
`demo.html`, not just the bare SVG + footer") is met per the manual check
above.

### Deviations

None from the request. Narration is built only from `EventMarker.Kind` /
`Cells` (no event carries an agent id today — `CommandAccepted`'s recipient,
for example, is discarded before reaching `EventMarker`), so sentences like
"Shot fired: hit." and "Moved (0,0) to (1,0)." do not name an agent; the
per-agent table carries the agent-attributed detail instead. Adding agent
ids to `EventMarker` would touch `src/CommandoWar.Sim/Diagnostics.fs`
(the observer leaf) for a narration-only gain — flagged rather than done,
per the task's own "stop and ask" instruction for anything needing new
diagnostic-model surface.

### Documents updated

`content/diagnostics/README.md`, `docs/12_PROGRESS_LEDGER.md` (this row +
this detail file). No ADR, no `docs/11_BACKLOG.md` row (no backlog item),
no `PROJECT_STATE.yaml` change (no phase/gate/decision/active-task change).

### Review

Not yet reviewed by Dave.
