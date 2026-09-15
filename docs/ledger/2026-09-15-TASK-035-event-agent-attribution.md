## 2026-09-15 - TASK-035 - event agent attribution for diagnostic narration

**Owner:** Dave with coding-agent assistance
**Source revision:** working tree on `main` with DIAG-001's uncommitted
changes already present (`content/diagnostics/demo.html` /
`content/diagnostics/README.md` / `docs/12_PROGRESS_LEDGER.md` /
`src/CommandoWar.Headless/DiagnosticRender.fs`) — this task builds directly
on that narration panel, per Dave's own task description.
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; xUnit 2.9.3
**Status change:** none in `PROJECT_STATE.yaml` structural fields
(`current_phase` / `current_gate` / `selected_task` unchanged); task file
`tasks/TASK-035-EVENT-AGENT-ATTRIBUTION.md` created at `proposed -> review`.

### Changes

- `src/CommandoWar.Sim/Diagnostics.fs`:
  - `EventMarker` gains `Agents: AgentId[]` (the `Cells: Cell[]` shape
    precedent), doc comment extended to describe the ordering convention
    (primary agent first; two entries for a two-party event — shooter before
    target, yielding/obstructed agent before winner/occupant, observer
    before contact).
  - `eventMarker` populates `Agents` in every match arm from fields the
    `DomainEvent` body already carries — nothing new computed. The three
    `CommandRejected` reasons that name no agent
    (`EmptyRecipients`/`DuplicateCommandId`/`IssueTickOutOfRange`) get
    `Agents = [||]`, matching their existing `Cells = [||]`.
- `src/CommandoWar.Headless/DiagnosticRender.fs`:
  - `eventNarration` rewritten to name the agent(s) in every sentence, e.g.
    `"Order accepted, destination (10,6)."` -> `"Agent 0: order accepted,
    destination (10,6)."`, `"Shot fired: hit."` -> `"Agent 0 fired at agent
    5: hit."`. `command-rejected` now distinguishes an agent-bearing reason
    (`"Agent %d: order rejected."`) from the three that carry neither an
    agent nor a cell (`"Order rejected."`) and `TargetOutOfBounds`, which
    carries a cell but no agent (`"Order rejected (target %s out of
    bounds)."`, unchanged).
  - `Ascii`'s `describe` (the terse `kind@cell,cell` events line) is
    unchanged — Decision B in the task file. `Svg`, `agentRow`,
    `contactText`, `annotations`, `Html` are all unchanged; they never read
    `EventMarker.Agents`.
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`: two existing facts
  extended in place (no new `[<Fact>]`, the TASK-032/033 precedent) —
  `` frameOf carries agent destinations and this-tick event markers `` now
  also asserts the demo tick-1 `command-accepted` event's `Agents = [|
  AgentId.ofInt 0 |]``; `` frameOf derives FireLine overlays for the
  open-engagement entry's first tick... `` now also asserts the two
  `shot-fired-*` events carry `Agents = [| shooter; target |]` matching
  their `FireLine` overlays (`[0;1]` and `[1;0]`).
- `content/diagnostics/demo.html`: regenerated (see Verification). Byte
  content changes only inside each tick's narration `<li>` text; `Svg`,
  the per-agent table, and the tactical-picture lines are untouched.
- `content/diagnostics/README.md`: `demo.html` row's description extended
  with one example of the agent-attributed narration.
- `tasks/TASK-035-EVENT-AGENT-ATTRIBUTION.md`: new task file, full AGENTS.md
  template, status `review`.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)` — confirms adding
    `EventMarker.Agents` breaks no other call site (no production or test
    code outside `Diagnostics.fs` constructs an `EventMarker` record
    literal).
- Command: `dotnet test CommandoWar.slnx -c Release` (before regenerating
  the golden)
  - Result: `Failed: 1, Passed: 286, Total: 287` — the sole failure was ``
    HTML render of the demo run is byte-equal to the committed golden ``,
    diverging exactly at the narration text (`"Order accepted, desti"` vs
    `"Agent 0: order accept"` at byte offset 26944) — the expected, isolated
    blast radius.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  render demo --format html --out content/diagnostics/demo.html`
  - Result: `wrote content/diagnostics/demo.html (311787 bytes)`, up from
    DIAG-001's 311107 bytes.
- Command: `dotnet test CommandoWar.slnx -c Release` (after regenerating)
  - Result: `Passed: 287, Failed: 0, Total: 287` — same total as before this
    task, since both new assertions extended existing facts rather than
    adding new ones.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus`
  - Result: `OK - all 12 entries match their committed tables` — confirms
    zero canonical drift, as expected (`EventMarker` is not part of
    `Canonical.encode`).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  fixture`
  - Result: final hash `0x56395A49904D017D` (format 7), `events: 36`,
    `random draws: 0` — unchanged from the pinned
    `docs/12_PROGRESS_LEDGER.md` value.
- Manual check: read the regenerated `demo.html` at tick 1 (`"Agent 0: order
  accepted, destination (10,6)."`, `"Agent 0 moved (0,0) to (1,0)."`) and
  tick 8 (`"Agent 0 fired at agent 5: hit."`, `"Agent 5 fired at agent 0:
  miss."`) — narration correctly names the agent(s) at both a single-party
  and a two-party event, matching DIAG-001's own tick-8 scenario
  description.
- Command: `git status --porcelain`
  - Result: `src/CommandoWar.Sim/Diagnostics.fs`,
    `src/CommandoWar.Headless/DiagnosticRender.fs`,
    `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`,
    `content/diagnostics/demo.html`, `content/diagnostics/README.md`,
    `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`, this task file and
    this detail file — plus DIAG-001's still-uncommitted changes and the
    pre-existing, unrelated `src/CommandoWar.Client.Godot/project.godot`
    modification, both left untouched.

### Evidence

See "Required verification" / "Evidence to capture" in
`tasks/TASK-035-EVENT-AGENT-ATTRIBUTION.md` for the full command list.

### Deviations and unresolved issues

None from the request. `Ascii`'s events line was deliberately left
unchanged (Decision B in the task file) rather than extended for
consistency — flagged explicitly for Dave's review since the task
description framed it as an open choice.

### Documents updated

`tasks/TASK-035-EVENT-AGENT-ATTRIBUTION.md` (new),
`content/diagnostics/README.md`, `docs/12_PROGRESS_LEDGER.md` (this row +
this detail file), `PROJECT_STATE.yaml` (`active_work.note` only). No ADR,
no `docs/11_BACKLOG.md` row (no matching backlog item — see the task file's
"Documentation updates" section).

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-15)
- Notes: Dave said "looks good", including Decision B (leaving `Ascii`'s
  events line terse). Re-verified before recording acceptance: `dotnet
  build CommandoWar.slnx -c Release` 0/0; `dotnet test` `Passed: 287`; `--
  corpus` 12/12; `-- fixture` format 7, final hash `0x56395A49904D017D`, 36
  events, unchanged; `git status --porcelain` matched the expected scope
  (this task's files, plus DIAG-001's still-uncommitted changes and the
  pre-existing unrelated `src/CommandoWar.Client.Godot/project.godot` edit,
  all untouched).
