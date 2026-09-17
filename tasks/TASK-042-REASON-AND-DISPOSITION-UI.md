# TASK-042: Player-facing order acknowledgement, disposition, and reason UI

Status: done (accepted by Dave 2026-09-17, "thats readable now")
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete)
Size: M

## Review round 1 (2026-09-17)

Dave tried the scene and reported the HUD's `order=` text "flashes up" too
fast to read, and noted the game loop runs faster than he expected —
explicitly not a regression, just the first time he had to read specific
transient HUD text rather than glance at a persistent one. Real: at the
default 20 Hz sim rate, `DemoScenario`'s short 3-cell route completes in
about 3 ticks (~150ms wall-clock at real-time playback), so `order=accepted`
appeared and reverted to `order=no order` before it could be read.

Fixed: a client-side, presentation-only hold timer in `CommandDemoScene`
(`orderTextHoldSeconds = 1.5`, `heldOrderText`/`orderTextHoldRemaining`,
`AGENTS.md`'s "rendering ... non-authoritative" rule — no
`Simulation.step`/`Disposition`/hash change). A newly meaningful message
(a fresh order, or a reappraisal flipping the outcome) always overrides
immediately; only the fall-back from a meaningful message to `"no order"` is
held for `orderTextHoldSeconds`, tracked in `Update(deltaSeconds)` (the only
place the real per-frame wall-clock delta is available — `HudText()` has
none). Reset to empty immediately on any selection change (`OnClick`), so a
newly selected agent never shows a stale message left over from a different
one.

Verified with a new headless scratch probe (removed after use) that drives
`Update(dt)` directly with small (`1/60s`) per-frame deltas — the actual
windowed code path, unlike `StepTicksHeadless`/`--selfcheck`, which advance
ticks with no relation to wall-clock time and so never exercise the hold
timer at all. Result: `order=accepted` now stays visible from the tick it is
delivered (~0.1s) through ~1.7s, well past the ~0.2s point the underlying
order actually completes, before falling back to `order=no order` — over a
second of genuinely readable time instead of ~150ms. Both scenes'
`--selfcheck` hashes confirmed unchanged (the hold timer only runs inside
`Update`, which `--selfcheck`'s `StepTicksHeadless` path never calls).
`dotnet build` both `.slnx` (Debug and Release) 0/0; `dotnet test` 297/297.

## Outcome (2026-09-17)

Central decision confirmed with Dave via `AskUserQuestion` before drafting:
extend the existing corner `HudText()` label with the selected agent's
disposition/reason, rather than a new floating label tracking the agent's
on-screen position (smaller change, reuses the established pattern from
TASK-040's `selected=agent N` text).

`AgentSnapshot` (`src/CommandoWar.Sim/Snapshot.fs`) gains one new field,
`Disposition: OrderDisposition option` — a values-only copy of
`AgentState.Disposition`, the same precedent as `Destination`/`Progress`
(docs/03 section 12: clients read `RenderSnapshot`, never authoritative
storage). `Simulation.fs`'s `output` phase (the sole place `RenderSnapshot`
is built from `WorldState`) now copies it across. Both Godot scenes'
`Ready()` methods, which separately build an initial `AgentSnapshot[]`
straight from `WorldState.Agents` before the first `Simulation.step` call,
updated to match (mechanical, no behaviour change).

New `RenderShared.dispositionText` (`Core/RenderShared.fs`) — a **player-
facing** structured-value-to-text mapping (docs/05 section 7: "do not add
prose-only reasons; UI text is derived from structured values"), explicitly
separate from `DiagnosticRender`'s private developer-facing one: docs/06
section 11 draws that distinction directly ("Player-facing: ... order
acknowledgement and disposition" vs. "Developer-facing: ... last appraisal
factors and selected reason"). Covers every `OrderDisposition`/
`DecisionReason` case that exists today (`Accepted`; `Refused`/`Unable` with
`NoKnownRoute`, `RouteTooExposed` with/without a named threat, and
`TargetNotKnown`) plus `None` ("no order" — covers both "no current order"
and "not yet appraised this tick", which `AgentSnapshot` alone cannot
distinguish and neither is worth surfacing as a separate state to the
player).

`CommandDemoScene.HudText()` (the only scene with selection, TASK-040) now
looks up the selected agent's own `AgentSnapshot` (not just its cell, the
existing `agentPosition` helper) and appends `order=<disposition text>` —
omitted entirely when nothing is selected, matching the existing
`selected=none` precedent. `DemoRenderScene` (no selection, no player input)
is untouched.

No `FSharpSceneHost.cs`/`_Draw` change — this is a HUD-text-only addition,
not a new draw primitive.

Verified for real: both scenes' `--selfcheck` hashes unchanged
(`0x11B06E6EDE0C52E3`, `0x649FA4D08E2931CA`) — `RenderSnapshot` is not part
of `Canonical.encode`, so this is genuinely state-neutral, confirmed rather
than assumed. A temporary scratch `dotnet fsi` probe (removed after use, the
TASK-038/039 precedent) drove the real `CommandDemoScene`/`IClientScene`
headlessly and printed `HudText()` at each step: `"selected=agent 0
order=no order"` before issuing an order, `"order=accepted"` once delivered
and appraised (ticks 1-3, cross-checked against the exact pinned hash
sequence — this is the real code path, not a mock), and `"order=no order"`
again once the order is fulfilled and cleared (tick 4, TASK-030's existing
"cleared on arrival" behaviour). The mapper's `Refused`/`Unable`/`None`
branches were exercised directly with constructed `DecisionReason` values
(reverse-engineering an actual refusal from `DemoScenario`'s specific
terrain wasn't necessary to prove the text mapping itself). A windowed
`--screenshot` of `CommandDemoScene` (`docs/evidence/task-042-reason-and-
disposition.png`, committed) shows the HUD rendering `order=no order` at the
scripted paused/pre-delivery capture point (TASK-040's existing screenshot
priming was left untouched, to preserve its own "paused, undelivered order"
reproducibility — the accepted/refused states are proven by the headless
probe above instead, not by this screenshot).

`dotnet build CommandoWar.slnx -c Release`: unaffected, `0/0`. `dotnet build`
the Godot client `.slnx` in both `-c Debug` and `-c Release`: `0/0`.
`dotnet test CommandoWar.slnx -c Release`: `297/297` (unaffected — no test
constructs `AgentSnapshot` directly, only field-by-field comparison).

Full detail: `docs/ledger/2026-09-17-TASK-042-reason-and-disposition-ui.md`.

## Objective

Give the player a visible answer to "why didn't my order go through" for
the currently selected agent: docs/06 section 8 requires "concise
explanations for refusal, delay, adaptation, and panic" as a modern
usability requirement, and section 11 requires "order acknowledgement and
disposition" as a player-facing tactical overlay. Neither exists yet —
`RenderSnapshot` (the only state production client code reads) has never
exposed `AgentState.Disposition`.

## Why this task exists

B-028's dependencies (B-017/TASK-028, which built the staged appraisal and
`OrderDisposition`/`DecisionReason` vocabulary; B-026/TASK-040, which built
selection) are both `done`. Chosen as the next P4 client task via
`AskUserQuestion` this session, ahead of B-030 proper and B-031 (both
sim-side, no Godot).

## Central decision (confirmed with Dave via `AskUserQuestion` before drafting)

Extend the existing corner `HudText()` label with the selected agent's
disposition/reason, not a new floating label tracking the agent on the
tactical view. Smaller change, reuses the established `selected=agent N`
pattern from TASK-040; a floating in-world label is left for later if
Dave wants a more prominent presentation.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `docs/05_COMMAND_AND_AGENT_AI.md` section 7 ("structured reasons")
- `docs/06_CONTENT_AND_PRESENTATION.md` sections 8 and 11 (player-facing vs.
  developer-facing overlays — the exact source of this task's two
  requirements)
- `docs/03_ARCHITECTURE.md` section 12 (`RenderSnapshot` values-only
  contract)
- `src/CommandoWar.Sim/Domain.fs` (`OrderDisposition`, `DecisionReason`)
- `src/CommandoWar.Sim/Snapshot.fs`, `Simulation.fs`'s `output` phase
- `src/CommandoWar.Headless/DiagnosticRender.fs` (`reasonText`/
  `dispositionText` — the developer-facing precedent this task deliberately
  does not reuse, per docs/06's own player/developer distinction)
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`,
  `Core/RenderShared.fs`

## Dependencies

- B-017 (TASK-028), B-026 (TASK-040), both `done`.

## Allowed scope

- `src/CommandoWar.Sim/Snapshot.fs`: add `Disposition: OrderDisposition
  option` to `AgentSnapshot` (values-only, no new canonical state — it is a
  copy of the already-canonical `AgentState.Disposition`).
- `src/CommandoWar.Sim/Simulation.fs`: the `output` phase's existing
  `AgentSnapshot` construction, to copy the new field.
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`: a new player-facing
  `dispositionText` mapping.
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: `HudText()` only.
- `src/CommandoWar.Client.Godot/Core/DemoRenderScene.fs`: mechanical
  `AgentSnapshot` construction update only (new field, no behaviour change).
- `docs/evidence/task-042-*.png`, `README.md`, `docs/11_BACKLOG.md`,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Forbidden scope

- No new `CommandoWar.Sim` behaviour — `AgentState.Disposition` already
  exists and is already canonical; this task only exposes it to clients.
- No `FSharpSceneHost.cs`/`_Draw` change — HUD text only, no new draw
  primitive, per the confirmed central decision.
- No developer-facing overlay work (that is B-029 proper).
- No fireteam/multi-select reason display (single selection only, the
  TASK-040 precedent, unchanged).
- No work on B-052 (agent facing) or B-053 (hover highlight) — recorded
  separately this session, not this task's scope.

## Required work

1. Extend `AgentSnapshot`/`Simulation.output` to carry `Disposition`.
2. Add a player-facing `dispositionText` mapping (`Core/RenderShared.fs`).
3. Extend `CommandDemoScene.HudText()` to show it for the selected agent.
4. Verify both scenes' `--selfcheck` hashes are unchanged.
5. Verify the actual text output for `Accepted`/`Refused`/`Unable`/`None`.
6. Update documentation.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] The selected agent's order disposition (accepted/refused/unable/none)
      is visible to the player.
- [x] A refusal/inability shows its concrete `DecisionReason`, not just
      "refused".
- [x] Text is player-facing (docs/06's own distinction), not the developer
      diagnostic vocabulary.
- [x] `--selfcheck` hashes for existing scenes are unchanged (values-only,
      no authoritative state change).
- [x] No `CommandoWar.Sim` behaviour change (field addition only).
- [x] Required documentation updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx`
  in both `-c Debug` and `-c Release`: `0/0`.
- `dotnet test CommandoWar.slnx -c Release`: `297/297`.
- Godot `--selfcheck` for both scenes: hash unchanged.
- A headless scratch probe driving the real `CommandDemoScene`/`IClientScene`
  and printing `HudText()`, cross-checked against the pinned hash sequence.
- `git status --porcelain`: matches this task's allowed scope.

## Evidence to capture

- `docs/evidence/task-042-reason-and-disposition.png`.
- The scratch probe's printed output (recorded in the ledger detail file,
  not committed as a file — TASK-038/039 precedent).

## Expected files

- `src/CommandoWar.Sim/Snapshot.fs`, `Simulation.fs`.
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`,
  `CommandDemoScene.fs`, `DemoRenderScene.fs`.
- `docs/evidence/`, `README.md`, `docs/11_BACKLOG.md`,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Documentation updates

- This task file's Outcome section.
- `src/CommandoWar.Client.Godot/README.md`.
- `docs/11_BACKLOG.md` B-028 row.
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Additive and values-only: one new `AgentSnapshot` field (populated from
already-canonical state), a new pure text-mapping function, and one
`HudText()` line. No authoritative state or hash change (confirmed by the
unchanged `--selfcheck` hashes). Revertible with `git revert` in one step.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
