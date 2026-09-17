## 2026-09-17 - TASK-042 - Player-facing order acknowledgement, disposition, and reason UI

### Why

B-028's dependencies (B-017/TASK-028, B-026/TASK-040) are both `done`.
Selected via `AskUserQuestion` over B-030 proper and B-031 (both sim-side,
no Godot), continuing straight on from TASK-041 in the same session (Dave's
instruction: continue now if optimal, or hand off with a new prompt — ample
context remained, so continuing was the more efficient path).

### Central decision (confirmed with Dave via `AskUserQuestion` before drafting)

Extend the existing corner `HudText()` label with the selected agent's
disposition/reason, rather than a new floating label tracking the agent's
on-screen position each frame. Recommended and chosen: smaller change,
reuses the established `selected=agent N` pattern (TASK-040), lowest risk.
A floating in-world label stays available as later polish if Dave wants a
more prominent presentation.

### Changes

**`src/CommandoWar.Sim/Snapshot.fs`**: `AgentSnapshot` gains
`Disposition: OrderDisposition option` — a values-only copy of the
already-canonical `AgentState.Disposition`, the exact precedent
`Destination`/`Progress` already set (docs/03 section 12: clients read
`RenderSnapshot`, never authoritative storage directly). No new
`CommandoWar.Sim` behaviour; `OrderDisposition`/`DecisionReason` already
exist (TASK-028/037).

**`src/CommandoWar.Sim/Simulation.fs`**: the `output` phase (the sole place
`RenderSnapshot` is built from `WorldState.Agents`, line ~1215) now copies
`a.Disposition` into the new field alongside the existing ones.

**`src/CommandoWar.Client.Godot/Core/DemoRenderScene.fs`,
`CommandDemoScene.fs`**: both scenes' `Ready()` methods separately build an
initial `AgentSnapshot[]` directly from `WorldState.Agents` before the first
`Simulation.step` call (so the very first frame has agent data before any
tick has run) — both updated to also copy `Disposition`, mechanical, no
behaviour change.

**`src/CommandoWar.Client.Godot/Core/RenderShared.fs`**: new `reasonText`
(private) and `dispositionText` (public) — a player-facing mapping from
`DecisionReason`/`OrderDisposition option` to short text (`"accepted"`,
`"refused: route too exposed (threat: agent 3)"`, `"unable: target not
known"`, `"no order"`). Deliberately a *separate* mapping from
`DiagnosticRender.dispositionText`/`.reasonText` (both `private` there):
docs/06 section 11 explicitly distinguishes "Player-facing: ... order
acknowledgement and disposition" from "Developer-facing: ... last appraisal
factors and selected reason" — reusing the developer-facing one would blur
that boundary even though the underlying structured values are identical.

**`CommandDemoScene.fs`**: `HudText()` now looks up the selected agent's own
`AgentSnapshot` (previously only `agentPosition`, which discards everything
but `Cell`) and appends `order=<dispositionText>` — omitted entirely
(empty suffix) when nothing is selected, matching the existing
`selected=none` precedent exactly.

**No `FSharpSceneHost.cs`/`_Draw` change** — this is HUD text only, not a
new draw primitive; the confirmed central decision ruled out the
alternative that would have needed one.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0/0`.
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: `0/0`.
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Release`
  - Result: `0/0`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `297/297` — unaffected; the one test touching `AgentSnapshot`
    (`SimulationTests.fs`) does field-by-field comparison, not record
    construction, so it needed no change.
- Command: `"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck`
  - Result: `MATCH expected final hash 0x11B06E6EDE0C52E3 at tick 20`, exit
    `0` — unchanged. Confirms `RenderSnapshot` additions are genuinely
    outside `Canonical.encode`, not assumed.
- Command: `"$GODOT" --headless --path . scenes/CommandDemo.tscn -- --selfcheck`
  - Result: `MATCH expected final hash 0x649FA4D08E2931CA at tick 20`, exit
    `0` — unchanged.
- Command: a temporary scratch `dotnet fsi` script (removed after use, not
  committed — the TASK-038/039 precedent) referencing the built `Core`/`Sim`/
  `Headless` DLLs directly, instantiating the real `CommandDemoScene`,
  calling `Ready()`, `OnClick`/`OnHover` to select agent 0 and issue
  `MoveTo(3,0)`, then `StepTicksHeadless(1L)` five times, printing
  `HudText()` after each call.
  - Result:
    ```
    before selection:          ... selected=none
    selected, no order yet:    ... selected=agent 0   order=no order
    issued, undelivered:       ... selected=agent 0   order=no order
    tick 1:  hash=0x1CBB0DC721BC37FD ... order=accepted
    tick 2:  hash=0xF7AEFE59C3C5E9C5 ... order=accepted
    tick 3:  hash=0xEEDEF20CF6D2AB5F ... order=accepted
    tick 4:  hash=0x3C6C48DA2767339A ... order=no order
    tick 5:  hash=0x65C8A83272E08969 ... order=no order
    mapper Accepted:           accepted
    mapper Refused(NoRoute):   refused: no known route
    mapper Refused(Exposed):   refused: route too exposed (threat: agent 7)
    mapper Unable(TargetUnk):  unable: target not known
    mapper None:               no order
    ```
    Every printed hash matches the pinned `CommandDemoDrive.runScriptedSelfCheck`
    sequence exactly (same real code path, not a mock). `order=accepted`
    appears the tick the order is delivered/appraised; `order=no order`
    returns once the 3-cell route completes and `Destination`/`Order`/
    `Disposition` clear (TASK-030's existing "cleared on arrival" behaviour)
    — not a bug, the expected `None` case. The `Refused`/`Unable`/`None`
    branches were exercised directly against constructed `DecisionReason`
    values rather than reverse-engineering a real refusal from
    `DemoScenario`'s specific terrain, which isn't necessary to prove the
    text mapping itself.
- Command: `"$GODOT" --path . scenes/CommandDemo.tscn -- --screenshot <path>`
  - Result: `docs/evidence/task-042-reason-and-disposition.png` committed.
    Uses TASK-040's existing scripted priming unmodified (deliberately left
    untouched, to preserve its own "paused, undelivered order"
    reproducibility) — the capture point is paused before delivery, so it
    shows `order=no order`, not `accepted`. The accepted/refused states are
    proven by the headless probe above instead.
- Manual check: `git status --porcelain`
  - Result: matches this task's allowed scope, plus the pre-existing
    unrelated `project.godot` diff, left untouched.

### Evidence

- `docs/evidence/task-042-reason-and-disposition.png`.
- The scratch probe's printed output (above) — not committed as a file, the
  TASK-038/039 precedent for disposable verification artefacts.
- Both scenes' `--selfcheck` hash sequences, unchanged.

### Deviations and unresolved issues

- Considered reusing `DiagnosticRender`'s existing `dispositionText`/
  `reasonText` instead of writing a new mapping; rejected because both are
  `private` (not reusable across the assembly boundary as written) and,
  more importantly, docs/06 section 11 treats player-facing and
  developer-facing disposition/reason text as two distinct overlay
  categories — collapsing them would blur a distinction the design doc
  draws on purpose, even though today's wording happens to be similar.
- Considered advancing the `--screenshot` capture past delivery (or
  resuming from the paused priming) so the committed screenshot itself
  would show `order=accepted`; rejected — that would change what TASK-040's
  own default `--screenshot` reproduction shows (its whole point was
  proving the previously-invisible paused/undelivered state), a live
  behaviour change outside this task's minimal scope. The headless probe
  proves the accepted/refused states instead, without touching shared
  screenshot-priming code.
- Not built (Forbidden scope, as drafted): a developer-facing overlay
  (B-029 proper), fireteam/multi-select reason display, and B-052/B-053
  (recorded separately this session, not this task's scope).

### Documents updated

- `tasks/TASK-042-REASON-AND-DISPOSITION-UI.md` (created, Outcome section,
  acceptance criteria checked; Review round 1 section added).
- `src/CommandoWar.Client.Godot/README.md` (new section).
- `docs/11_BACKLOG.md` B-028 row (`proposed -> review`).
- `docs/12_PROGRESS_LEDGER.md` (this row, plus a review-round-1 row).
- `PROJECT_STATE.yaml`.

### Review round 1 (2026-09-17)

Dave tried the scene and reported the `order=` HUD text "flashes up" too
quickly to read, and separately noted the game loop runs faster than he
expected — explicitly framed as not a regression, just the first time he
needed to read specific transient text rather than glance at a persistent
value. Real, diagnosed directly: at the default 20 Hz sim rate,
`DemoScenario`'s short 3-cell route completes in about 3 ticks (~150ms
wall-clock at real-time playback), so `order=accepted` appeared and reverted
to `order=no order` before it could be read.

Fixed: a client-side, presentation-only hold timer
(`orderTextHoldSeconds = 1.5`) in `CommandDemoScene`, tracked in
`Update(deltaSeconds)` (the only place the real per-frame wall-clock delta
is available). A newly meaningful message (a fresh order, or a reappraisal
flipping the outcome) always overrides immediately; only the fall-back from
a meaningful message to `"no order"` is delayed. Reset to empty immediately
on any selection change, so a newly selected agent never shows a stale
message left over from a different one. No `Simulation.step`/`Disposition`/
hash change (`AGENTS.md`: "rendering ... are non-authoritative").

Verified with a new headless scratch probe (removed after use) driving
`Update(dt)` directly with small (`1/60s`) per-frame deltas — the actual
windowed code path. Unlike the earlier probe's `StepTicksHeadless` calls
(and `--selfcheck`, which uses the same path), this exercises the hold timer
at all, since `StepTicksHeadless` advances ticks with no relation to
wall-clock time and never calls `Update`.

```
t=0.10s  ... order=accepted
t=0.20s  ... order=accepted      (underlying order already fulfilled by here)
...
t=1.70s  ... order=accepted      (still held)
t=1.80s  ... order=no order      (hold expired, reverted)
```

`order=accepted` now stays visible from delivery (~0.1s) through ~1.7s —
over a second of genuinely readable time, instead of the ~150ms window
before the fix. Both scenes' `--selfcheck` hashes confirmed unchanged (the
hold timer only runs inside `Update`, which `--selfcheck` never calls).
`dotnet build` both `.slnx` (Debug and Release) `0/0`; `dotnet test`
`297/297`.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-17, "thats readable now")
- Notes: acceptance confirms the round 1 hold-timer fix specifically; no
  further issues raised against the underlying disposition/reason feature
  itself.
