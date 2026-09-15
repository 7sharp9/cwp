# TASK-035: Event agent attribution for diagnostic narration

Status: done (drafted, implemented, self-verified, and accepted 2026-09-15;
see "Central decisions" for why this task skipped a separate
pre-implementation confirmation round)
Owner: Dave, implemented by coding-agent assistance
Phase: P3
Gate: G3 (diagnostics/tooling only; does not touch command-loop behaviour)
Size: S

## Outcome (2026-09-15)

`Diagnostics.EventMarker` (`src/CommandoWar.Sim/Diagnostics.fs`) gains an
`Agents: AgentId[]` field, populated in every `eventMarker` arm from the
`DomainEvent`'s own already-carried agent id(s) (`CommandAccepted`'s
recipient, `MovementStepped`'s agent, `ShotFired`'s shooter/target, etc. —
nothing new is derived, `eventMarker` simply stops discarding data
`EventBody` already has). `DiagnosticRender.eventNarration`
(`src/CommandoWar.Headless/DiagnosticRender.fs`) now names the agent(s) in
every sentence, e.g. `"Shot fired: hit."` → `"Agent 0 fired at agent 5:
hit."`. `Ascii`'s terse `kind@cell,cell` events line is deliberately left
unchanged (Decision B). `content/diagnostics/demo.html` is the only golden
whose bytes move; regenerated and verified byte-equal against the test
suite's fresh render.

## Objective

Give the HTML per-tick narration panel (added in DIAG-001,
`docs/ledger/2026-09-15-DIAG-001-demo-html-annotations.md`) an agent name in
every event sentence, closing the gap DIAG-001 flagged rather than silently
patched: `EventMarker` carried a `Kind` and `Cells` but no agent id, so
sentences like `"Shot fired: hit."` could not say who fired.

## Why this task exists

DIAG-001's "Deviations" section: narration sentences name no agent because
`EventMarker` never carried one — `CommandAccepted`'s recipient and
`MovementStepped`'s agent are discarded before reaching it — and fixing that
means extending `Diagnostics.fs` (the Sim-side observer leaf) for a
narration-only gain, which is genuine new diagnostic-model surface and so
out of scope for DIAG-001's lightweight, no-task-file pass. Dave asked for
this as its own bounded task, under AGENTS.md's normal task-file discipline.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md` (Diagnostics section: a task that adds diagnostic-model
  surface must extend `DiagnosticFrame` and update a golden)
- `src/CommandoWar.Sim/Diagnostics.fs` (`EventMarker`, `eventMarker`)
- `src/CommandoWar.Sim/Events.fs` (`EventBody`, confirms every case already
  carries the agent id(s) needed)
- `src/CommandoWar.Headless/DiagnosticRender.fs` (`eventNarration`, `Ascii`'s
  `describe`)
- `docs/ledger/2026-09-15-DIAG-001-demo-html-annotations.md`

## Dependencies

- none (DIAG-001 landed the narration panel this task extends; not a
  numbered backlog item — a presentation-only follow-up, the TASK-009 /
  TASK-014 / TASK-019 precedent for tooling tasks with no `docs/11_BACKLOG.md`
  row)

## Inputs and assumptions

- `Canonical.fs` does not reference `Diagnostics`/`EventMarker` in any form
  (confirmed by grep before starting) — `EventMarker` is not part of
  `Canonical.encode`, so this task must not and does not touch
  `Canonical.FormatVersion`.
- No test or other renderer constructs an `EventMarker` record literal
  outside `Diagnostics.fs` (confirmed by grep), so adding a field is a
  backward-compatible record change, not a breaking one.

## Allowed scope

- `src/CommandoWar.Sim/Diagnostics.fs` (`EventMarker.Agents`, `eventMarker`
  arms, doc comments).
- `src/CommandoWar.Headless/DiagnosticRender.fs` (`eventNarration` only —
  `Ascii`, `Svg`, `agentRow`, `contactText`, `annotations`, `Html` untouched).
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs` (extend the two existing
  facts that already exercise `command-accepted` and `shot-fired-*` events,
  in place — the TASK-032/033 "extend in place" precedent).
- `content/diagnostics/demo.html` (full re-render).
- `content/diagnostics/README.md`, `docs/12_PROGRESS_LEDGER.md`,
  `PROJECT_STATE.yaml`.

Nothing under `src/CommandoWar.Sim/Canonical.fs`, `Events.fs`, `Commands.fs`,
or any phase in `Simulation.fs` — the agent ids this task surfaces already
exist on every `EventBody` case; nothing new is computed. No change to
`Ascii`'s events line (Decision B). No Godot/`AppraisalDemo.fs` change (it
does not render `Events`/`EventMarker`).

## Central decisions

No separate pre-implementation confirmation round with Dave (unlike
TASK-030 through TASK-034's "central decisions confirmed before the phase
bodies" pattern) — Dave's own task instructions asked the implementer to
make and document these decisions directly, given the small, low-risk,
single-session scope. Both are reversible in a follow-up if Dave disagrees
on review.

### Decision A — `EventMarker` gains `Agents: AgentId[]`, the `Cells: Cell[]` precedent, not a narrower typed shape

Considered a narrower `Agent: AgentId option` plus a separate field for the
rare two-party events. Rejected: `EventMarker` already has exactly this
"generic non-speculative array a renderer can always fall back to" shape for
`Cells` (the type's own doc comment), and a uniform array needs no per-kind
branching to read — `Agents.[0]` is always the primary agent, `Agents.[1]`
the second party where one exists (`ShotFired` shooter/target,
`MovementYielded` agent/winner, `MovementObstructed` agent/occupant,
`ContactObserved` observer/contact). Every `EventBody` case maps onto this
directly from fields it already has; the three `CommandRejected` reasons
that name no agent (`EmptyRecipients`, `DuplicateCommandId`,
`IssueTickOutOfRange`) get `Agents = [||]`, same as they already get
`Cells = [||]`.

### Decision B — `Ascii`'s terse events line is left unchanged; only `eventNarration` (HTML-only) is extended

`Ascii`'s `describe` function reads only `Kind`/`Cells` and was deliberately
terse from TASK-011 onward, redundant by design with the fuller `overlays:`
section immediately above it in the same render — e.g. the
`open-engagement-tick-001.ascii.txt` golden already spells out `fire (2,2)
-> (7,2): agent 0 -> agent 1  miss` in `overlays:` one line above the bare
`events: ...; shot-fired-miss; shot-fired-hit` line DIAG-001's own deviation
note pointed at. Adding an agent id to `Ascii`'s events line would be
redundant with data already on the same page and would re-pin every ASCII
golden with a non-empty events line (`converging-routes`, `swap-standoff`,
`perception-contact`, `lost-comms`, `exposed-approach`, `reissued-order`,
`open-engagement` — seven files) for no new information a reviewer cannot
already read one section up. `eventNarration` is the one place that
currently has no agent-attributed alternative anywhere else on the page, so
it is the one place this task changes. Net effect: adding `Agents` to
`EventMarker` moves no `Ascii` golden byte (confirmed — `-- corpus` and the
full `Ascii`/`Svg` `DiagnosticsTests` facts stayed green unmodified), only
`demo.html`.

## Required work

1. Inspect `Events.fs` to confirm the agent id(s) each `EventBody` case
   already carries (done above).
2. Add `Agents: AgentId[]` to `EventMarker`; populate it in every
   `eventMarker` match arm.
3. Update `DiagnosticRender.eventNarration` to name the agent(s) in every
   sentence; leave `Ascii`/`Svg`/`agentRow`/`contactText`/`annotations`/`Html`
   otherwise untouched.
4. Extend the two `DiagnosticsTests.fs` facts that already exercise
   `command-accepted` (demo tick 1) and `shot-fired-*` (`open-engagement`
   tick 1) to assert the new `Agents` shape.
5. Build, run the full suite, regenerate `content/diagnostics/demo.html`,
   re-run the suite to confirm byte-equality.
6. Run `-- corpus` and `-- fixture` to confirm zero canonical drift (belt and
   braces — `Canonical.fs` never references `Diagnostics`, so this was never
   expected to move, and did not).
7. Update `content/diagnostics/README.md`'s `demo.html` row, this task file,
   `docs/12_PROGRESS_LEDGER.md` (index row + detail file), `PROJECT_STATE.yaml`.

## Acceptance criteria

- [x] `EventMarker.Agents` populated correctly for every `EventBody` case
      (evidence: build succeeds with no other call site needing an update —
      no other production or test code constructs an `EventMarker` record
      literal — plus the two extended `DiagnosticsTests.fs` facts).
- [x] `demo.html`'s narration names the agent(s) for every event kind
      (evidence: manual read of the regenerated golden at tick 1
      command-accepted/movement-stepped and tick 8 combat — see Verification).
- [x] `Canonical.FormatVersion` untouched, zero hash re-pin anywhere
      (evidence: `-- corpus` 12/12, `-- fixture` format 7 / 36 events
      unchanged).
- [x] No forbidden dependency; no change outside "Allowed scope".
- [x] Required documentation updated (this task file, `docs/11_BACKLOG.md`
      — no matching row, see Dependencies; `docs/12_PROGRESS_LEDGER.md`,
      `PROJECT_STATE.yaml`, `content/diagnostics/README.md`).

## Required verification

- unit tests: `dotnet test CommandoWar.slnx -c Release` — full suite.
- scenario/replay tests: `dotnet run --project src/CommandoWar.Headless -c
  Release -- corpus` (all 12 entries); `-- fixture` (spot-check hash/format/
  events unchanged).
- build: `dotnet build CommandoWar.slnx -c Release`.
- manual smoke test: read regenerated `demo.html` at tick 1 (order-accepted /
  movement-stepped) and tick 8 (combat) narration.
- dependency boundary check: not applicable — no `.fsproj` touched, no
  package reference added or changed.

## Evidence to capture

- `dotnet build CommandoWar.slnx -c Release`: `0 Warning(s)  0 Error(s)`.
- `dotnet test CommandoWar.slnx -c Release` (before regenerating the
  golden): `Failed: 1, Passed: 286, Total: 287` — the one expected failure
  was `HTML render of the demo run is byte-equal to the committed golden`,
  diverging exactly at the narration text (`"Order accepted, desti"` vs
  `"Agent 0: order accept"`).
- `dotnet run --project src/CommandoWar.Headless -c Release -- render demo
  --format html --out content/diagnostics/demo.html`: `wrote
  content/diagnostics/demo.html (311787 bytes)` — up from the 311107 bytes
  DIAG-001 produced (the narration text grows by roughly `"Agent N"` /
  `"agent N"` fragments per event line, no structural change).
- `dotnet test CommandoWar.slnx -c Release` (after regenerating): `Passed:
  287, Failed: 0, Total: 287` — same count as before this task (287), since
  the two new assertions extended existing facts in place rather than adding
  new `[<Fact>]`s.
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`:
  `OK - all 12 entries match their committed tables`.
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`:
  final hash `0x56395A49904D017D` (format 7), `events: 36`, `random draws:
  0` — unchanged from the pinned `docs/12_PROGRESS_LEDGER.md` value.
- Manual read of `content/diagnostics/demo.html`: tick 1 carries `"Agent 0:
  order accepted, destination (10,6)."` and `"Agent 0 moved (0,0) to
  (1,0)."`; tick 8 carries `"Agent 0 fired at agent 5: hit."` / `"Agent 5
  fired at agent 0: miss."`, matching DIAG-001's own tick-8 scenario
  description (agent 0's order refused `route-too-exposed threat-agent-5`,
  both sides just sighted each other, a shot exchanged).
- `git status --porcelain` (after this task's changes, on top of DIAG-001's
  still-uncommitted working tree): `src/CommandoWar.Sim/Diagnostics.fs`,
  `src/CommandoWar.Headless/DiagnosticRender.fs`,
  `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`,
  `content/diagnostics/demo.html`, `content/diagnostics/README.md`,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`, this task file, plus
  the pre-existing DIAG-001 changes and the unrelated pre-existing
  `src/CommandoWar.Client.Godot/project.godot` modification, all left
  untouched.

## Expected files

- `src/CommandoWar.Sim/Diagnostics.fs`
- `src/CommandoWar.Headless/DiagnosticRender.fs`
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`
- `content/diagnostics/demo.html`, `content/diagnostics/README.md`
- `docs/12_PROGRESS_LEDGER.md`, `docs/ledger/2026-09-15-TASK-035-*.md`
- `PROJECT_STATE.yaml`

## Documentation updates

- this task file (status, outcome, evidence);
- `docs/11_BACKLOG.md`: no matching row added — presentation-only tooling
  follow-up, the TASK-009/014/019/023-without-B-item precedent (only
  `docs/11_BACKLOG.md` §2 "Current work" requires a row per completed task;
  §3's numbered `B-` items are for planned/dependency-tracked work, and none
  fits this one);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/2026-09-15-TASK-035-
  event-agent-attribution.md` detail file;
- `PROJECT_STATE.yaml`: `active_work.note` updated; no phase/gate/decision
  changed (`selected_task` stays `none` — this task, like DIAG-001, never
  occupied the single-active-task slot for its short duration);
- no ADR (no decision an ADR owns — `EventMarker` was already excluded from
  `Canonical.encode` by construction, nothing here changes that boundary).

## Rollback or removal

Fully reversible: revert the three source/test files and
`content/diagnostics/demo.html`; `EventMarker.Agents` has no consumer
outside `eventNarration`, so nothing else would need to change.

## Completion report

See the chat response for the AGENTS.md-format completion report
accompanying this task file.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-15)
- Notes: implemented and self-verified in one session per Dave's own task
  instructions (decide the `EventMarker` shape, check every call site,
  update `eventNarration`, regenerate affected goldens, confirm
  `Canonical.FormatVersion` untouched first). Decision B (leaving `Ascii`'s
  events line terse) flagged for review and accepted as-is — Dave said
  "looks good".
