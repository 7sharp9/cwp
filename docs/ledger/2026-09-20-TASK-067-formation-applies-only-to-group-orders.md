## 2026-09-20 - TASK-067 - Formation redirect applies only to group orders, not solo clicks

**Owner:** Dave
**Source revision:** `main` at `1965930` (TASK-065 and TASK-066 accepted and committed)
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot `4.7.2-stable_mono_win64`
**Status change:** `proposed -> review` (self-verified)

### Changes

Realises the sim-side half of B-067. Scoped via two parallel research
passes (one tracing the client click/selection/order pipeline and every
`Appraisal.resolveFormationTarget` call site; one tracing the TASK-047/048
pattern for adding a new order mechanism end to end, and confirming
`PlayerCommand.Recipients`/`Command.moveToMany` already address multiple
agents with one command since TASK-020) plus `AskUserQuestion` to resolve
two design forks: formation for a group order uses each agent's own
authored `FormationOffset` only (no ad-hoc/computed formation for an
arbitrary selection), and the eventual multi-select gesture will be both
drag rubber-band-select and shift-click (deferred to the still-unscoped
second half).

Root cause: `Appraisal.appraise`'s `MoveTo` branch read each recipient's
static `AgentState.FormationOffset` unconditionally, so a formationed
agent's solo order redirected exactly the same as a genuine group move --
formation *membership* (a static, order-independent scenario fact) was
being conflated with an order actually being a coordinated group action.

- `src/CommandoWar.Sim/Domain.fs`: new `ReceivedOrder.AsGroup: bool`.
- `src/CommandoWar.Sim/Canonical.fs`: `FormatVersion` 14 -> 15, `writeOrder`
  extended.
- `src/CommandoWar.Sim/Simulation.fs`: `commandIntake` sets `AsGroup` from
  the validated `recipients.Length > 1` (no new command shape needed --
  `Command.moveToMany`, TASK-020, already builds a multi-recipient
  command); the `appraisal` phase's fulfilled fast-path and
  `Destination`-write match both gate their own `resolveFormationTarget`
  call on `AsGroup`.
- `src/CommandoWar.Sim/Appraisal.fs`: `appraise`'s `MoveTo` branch gated
  the same way.
- `src/CommandoWar.Sim/Diagnostics.fs`: `formationSlotOverlays`'s match
  guard requires `AsGroup = true` too (a solo order no longer shows a
  redirect it doesn't perform).
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: `OnHover`'s
  formation-preview call removed entirely -- every order this scene issues
  is single-recipient (no multi-select UI exists yet), so it always
  resolves to the literal cell now; a net simplification, not just a gate.

Two real, genuine consequences were found while re-verifying, not smoothed
over:

1. **`formation-slots` corpus entry.** The only committed content
   exercising formation redirect issued two single-recipient orders that
   happened to share a tick and target -- exactly the authoring pattern
   this task makes stop redirecting. Re-authored to a real
   `Command.moveToMany` (`src/CommandoWar.Headless/Corpus.fs`'s new
   `MoveOrderGroup` scenario-intent case and `groupMoveOrder` helper), so
   the entry keeps demonstrating genuine slot resolution; its own
   tick-by-tick outcome (12 ticks, 25 events) is unchanged, confirmed by
   diff against the pre-change committed table.
2. **`CommandDemoDrive.runScriptedSelfCheck`'s six-agent Bridgehead
   sequence.** It relied on the old unconditional redirect to spread
   agents whose literal target cells coincided (0/1/5 all clicked toward
   one cell, 2/4 toward another) onto distinct real destinations via
   formation offset. Re-running it unchanged under this task's fix
   produced a genuine friendly casualty (agent 4 died converging on a now-
   undeflected shared cell) -- confirmed with a temporary `dotnet fsi`
   probe replicating the exact order sequence directly against
   `CommandoWar.Sim` (removed after use), not assumed from the hash
   mismatch alone. Fixed by updating the script's six target cells to the
   *pre-TASK-067 resolved* cells directly (computed with the same probe);
   a second probe run using the client's own 0-based `CommandId`
   numbering reproduced the real `--selfcheck` hash exactly, confirming
   the fix is byte-for-byte faithful to the original demonstrated outcome
   (no friendly casualties, machine gun neutralised) rather than merely
   "probably fine."

A third, unrelated staleness was found and fixed while re-pinning:
`content/fixtures/SPIKE-FIXTURE.md` was still pinned at its
format-4/TASK-028 values, despite a prior session's `docs/12` ledger note
claiming it had already been corrected to format 14 -- that claim did not
match the committed file (`git log` shows its last real edit was
TASK-028). Corrected straight to format 15 here rather than perpetuated,
flagged for Dave.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `415/415` passed (+1: the new solo-order fact; the pre-existing
    `` `two formationed agents ordered to the same nominal cell resolve to
    distinct destinations` `` fact already covered the group-order case
    unchanged, since it already used `Command.moveToMany`).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  corpus --regenerate` then `-- corpus`
  - Result: `19/19` entries match. All 18 pre-existing entries re-pinned
    byte-layout only; `formation-slots` re-authored (see Changes) with its
    tick/event counts unchanged.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release --
  replay-file content/replays/envelope-full.cwreplay`
  - Result: `checkpoints : OK (24 ticks match the file's committed
    hashes)` after re-pinning `canonical`/`initial-hash`/all 24
    checkpoints and the `.md` table's mirrored copy plus
    `ReplayTests.fs`'s own third hardcoded copy (`envelopeFullHashes`) --
    this entry's one command already addresses three recipients via
    `Command.moveToMany`, `AsGroup = true`, but none of them authors a
    `FormationOffset`, so the byte-layout change is the only difference.
- Command: `dotnet build
  src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Manual check: `Godot_v4.7.2-stable_mono_win64_console.exe --headless
  --path src/CommandoWar.Client.Godot scenes/SnapshotDemo.tscn --
  --selfcheck`
  - Result: `MATCH 0x6213D672BC36FDB8` at tick 20 (byte-layout only,
    `DemoScenario` authors no formation).
- Manual check: same, `scenes/AppraisalDemo.tscn`
  - Result: `MATCH 0xA1354EB998FC1B95` (byte-layout only, `exposed-approach`
    authors no formation).
- Manual check: same, `scenes/CommandDemo.tscn`
  - Result: `MATCH 0x2629A1FE165F94BB` at tick 90 -- a genuine behaviour
    change (see Changes item 2), re-pinned after updating the scripted
    sequence's own target cells and confirming the outcome is unchanged.

### Evidence

- The new `SimulationTests` fact is the direct proof of the fix itself (a
  solo order for a formationed agent resolves to the literal target); the
  pre-existing group-order fact (TASK-059) is the direct proof the
  redirect still applies to a genuine group order, re-confirmed passing
  unchanged.
- The two temporary `dotnet fsi` probes (removed after use) are the direct
  evidence behind both genuine-behaviour-change findings in Changes: the
  first showed the casualty appearing when the script's old target cells
  were reused unchanged; the second, using the client's own `CommandId`
  numbering with the corrected target cells, reproduced the real
  `--selfcheck` hash exactly, proving the fix restores the identical
  demonstrated outcome rather than merely a plausible-looking one.

### Deviations and unresolved issues

- **This task does not build the multi-select UI or joint-order dispatch**
  -- B-067's second half, deferred per the original scoping decision. Every
  order `CommandDemoScene` can issue today is still single-recipient, so a
  player wanting to move two formationed agents together as a group still
  has no way to do so; only the *sim-side* gate (a group order, once one
  can be issued, applies formation; a solo order never does) exists now.
- Several agents in the re-pinned `CommandDemo.tscn` self-check still end
  the 90-tick run mid-route, queued behind each other at shared target
  cells rather than having fully arrived -- the same live-agent chokepoint
  contention TASK-066 already found and parked as B-067's own unscoped
  territory, unchanged by this task.
- `content/fixtures/SPIKE-FIXTURE.md`'s format-4 staleness (see Changes)
  is a second instance of the same class of drift TASK-065 already found
  once in this same file and reportedly fixed -- flagged for Dave as a
  possible process gap (this file is not covered by any automated test,
  unlike `FixtureTests.fs`'s own hashes, which stayed correctly current
  throughout).

### Documents updated

- `tasks/TASK-067-FORMATION-APPLIES-ONLY-TO-GROUP-ORDERS.md` (status,
  acceptance criteria, verification, evidence, review).
- `docs/04_SIMULATION_SPEC.md` (formation-resolution paragraph, TASK-067
  narrowing note).
- `docs/11_BACKLOG.md` (B-067 row).
- `docs/12_PROGRESS_LEDGER.md` (this row; Pinned facts `Canonical.
  FormatVersion` refreshed to 15).
- `PROJECT_STATE.yaml` (`active_work`, top-level `updated`).
- `src/CommandoWar.Client.Godot/README.md` (new section).
- `content/replays/CORPUS.md` (`formation-slots` row description).

### Review

- Reviewer: Dave
- Accepted: yes, 2026-09-20, on the self-verification evidence ("if it
  looks good then accept").
