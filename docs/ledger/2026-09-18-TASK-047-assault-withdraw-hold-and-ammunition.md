## 2026-09-18 - TASK-047 - Assault, Withdraw, Hold order executors and an ammunition/reload/resupply model

### Why

`docs/11_BACKLOG.md` B-030's row has read "proposed" since P3, repeatedly
flagged across TASK-037/044/045/046 session summaries as the largest
remaining sim-mechanics gap and the direct blocker for B-032 (the
demolition objective/extraction sequence). `docs/07` section 4 lists
`HoldArea`/`AssaultArea`/`WithdrawTo` among the five required player
commands; none existed as a `PlayerIntent` case. `Combat.fs`'s own module
doc had listed "ammunition, weapon readiness, fire-rate/cooldown, reload,
resupply" as deliberately absent since TASK-031 with no backlog number
assigned until this session. Selected via `AskUserQuestion` over B-058
(agent movement speed, smaller and faster, filed during TASK-046 review).

### Central decisions (the task's own, resolved live this session)

Three `AskUserQuestion` rounds before drafting, all recorded in
`tasks/TASK-047-ASSAULT-WITHDRAW-HOLD-AND-AMMUNITION.md`'s Decisions A-P:

1. **Scope**: bundle Hold/Assault/Withdraw executors with the ammo/reload/
   resupply model in one task (Dave's choice, over a two-task split).
2. **Assault depth**: the full staged FSM from `docs/05` section 10's
   example (Dave's choice, over a thin move-and-auto-engage executor).
3. **Ammo model**: reload-from-stock, not cooldown-between-magazines
   (needs a resupply answer as a consequence); ammo applies to all combat
   fire, not `Suppress` alone; resupply designed now via a new authored
   `ResupplyAreas` content type, rather than deferred.
4. **Sequencing**: everything in one combined task, not split
   order-executors-then-ammo (Dave's choice, over the recommended split,
   after being flagged as materially larger than TASK-037).

### Changes

**`Domain.fs`**: `PlayerIntent.Hold of area: Cell`, `.Assault of target:
Cell`, `.Withdraw of target: Cell` (the `MoveTo` precedent — a bare `Cell`,
no `AreaId`/`TargetArea`/`Approach` model exists); `DecisionReason.
InsufficientAmmunition`; `AmmoState = Ready of magazine * reserve |
Reloading of reserve * ticksRemaining` (the `VitalStatus` "single field,
not a flag alongside one" idiom); `AgentState.Ammo`; `WorldState.
ResupplyAreas: Cell[]` (static, excluded from `Canonical.encode`, the
`Terrain` precedent); `Agent.MagazineSize`/`.ReserveStart` literals (the
`MaxHealth` module-ordering precedent).

**New `Ammo.fs` leaf** (the `Suppression.fs`/`Casualty.fs` precedent):
`AmmoConfig` (`MagazineSize = 30`, `ReserveStart = 90`, `ReloadTicks =
30`), `canFire`, `fire`, `tick` (the `Casualty.tickBleedOut` shape exactly
— `Ready(0, reserve>0) -> Reloading`, counts down, refills once the input
`ticksRemaining <= 1`), `resupply`, `isFull`.

**`Appraisal.fs`**: five new `AppraisalConfig` literals
(`AssaultResolvePenalty`/`AssaultStartRange`/`AssaultClearRadius`/
`WithdrawResolveBonus`/`HoldCoverSearchRadius`); `bestCoverNear` (a new
pure function reusing the existing private `cellPressure` threat-pressure
function — the identical one `routeExposure` sums over a route — to pick
the lowest-pressure cell within `HoldCoverSearchRadius` of an authored
area); `appraise` restructured around a shared `moveLike` inner function
(a signed `thresholdAdjust` re-floored at 0, preserving `resolveThreshold`'s
"never refuses a safe route" invariant) so `MoveTo`/`Hold`/`Withdraw`/
`Assault` share one pipeline; a stage-2 `noAmmo` check gates `Suppress`/
`Assault` only. `appraise` gained an `ammo: AmmoState` parameter (both call
sites — `Simulation.appraisal`, `Diagnostics.orderAppraisalOverlays` —
updated).

**`Commitment.fs`**: `WithdrawCommitment` (the `MoveCommitment` shape);
`AssaultStage = ApproachingStart | AwaitingSupport | Advancing |
ClearingThreat` and `AssaultCommitment`; `Commitment` gained `Withdrawing`
and `Assaulting`; `hasUnsuppressedThreatWithin` (shared predicate behind
`AwaitingSupport`/`ClearingThreat`, parameterised by range); `assaultStage`
(pure, four-branch derivation). `Commitment.ofAgent`'s signature grew to
take `threats`/`suppressedThreats`/`position` (needed only for
`Assaulting`'s `Stage`) — three call sites updated (`Simulation.combat`,
`Simulation.commitmentAndLocalAction` — new, Deviation 1 — and
`Diagnostics.commitmentOverlays`). `Hold` deliberately gets no new
`Commitment` case (Decision C): an accepted `Hold` already produces
`Moving` via the pre-existing generic accepted-with-destination match arm.

**`Combat.fs`**: doc-comment update only — the leaf's `hitChance`/
`chooseTarget` are unchanged; ammo is a firing gate the caller applies.

**`Simulation.fs`**:
- `commandIntake`: the bounds check now covers every `Cell`-targeted
  intent (`MoveTo`/`Hold`/`Assault`/`Withdraw`), restructured into a
  cleanly nested `match` (an earlier draft's flat two-match version was an
  F# offside-rule bug — caught before it ever compiled).
- `appraisal`: threads `a.Ammo` into `Appraisal.appraise`; the
  `destination`-write match gains `Hold` (via `bestCoverNear`, recomputed
  here on the identical inputs `appraise` used internally, not threaded
  back out through its return value), `Assault`, `Withdraw`; the
  `fulfilled` fast-path check gains matching arms (Assault's own
  clear-condition nuance deliberately NOT checked here — only in
  `commitmentAndLocalAction`, which owns the real fulfilment decision).
- `commitmentAndLocalAction`: a new `completeOrder` local helper factors
  the queue-promotion/`CommitmentCompleted` logic every fulfilled-order
  shape now shares (`MoveTo`, `Hold`, `Withdraw`, `Assault`); `Hold`/
  `Withdraw` branches mirror `MoveTo`'s shape exactly; `Assault`'s branch
  computes the real fulfilled condition (position = target AND clear) and,
  when not yet fulfilled, drives the stage-dependent `Destination` freeze
  (`AwaitingSupport` -> `None`) / unfreeze (`ApproachingStart`/`Advancing`
  -> `Some target`) through the ordinary, otherwise-untouched Navigation
  pipeline — no new `MoveOutcome` case, no interaction with the
  contention/vacation-chain machinery (Decision G).
- `combat`: gates firing on `Ammo.canFire`; consumes one round via
  `Ammo.fire` on every qualifying shot, every commitment alike (Decision
  K); updated `Commitment.ofAgent` call site.
- `stateConsequences`: a resupply check (agent on a `ResupplyAreas` cell,
  not already full -> `Ammo.resupply`, `AgentResupplied`, short-circuiting
  any reload) runs before `Ammo.tick`'s own reload bookkeeping
  (`ReloadStarted`/`ReloadCompleted`).
- `World.build`/`.create`/`.ofScenario`: threaded `resupplyAreas: Cell[]`
  through the shared construction core; `StepState` carries it statically,
  the `Terrain` precedent.

**`Canonical.fs`**: `FormatVersion` `10 -> 11`; `writeReason` gained code 4
(`InsufficientAmmunition`); `writeOrder` gained codes 2-4 (`Hold`/
`Assault`/`Withdraw`); `writeAgent` gained an `Ammo` section.
`Commitment.Withdrawing`/`.Assaulting` and `WorldState.ResupplyAreas` are
NOT written (derived value / static data, the existing precedents exactly).

**`Scenario.fs`**: `ResupplyAreas: Area[]`/`RawArea[]`, folded into the
same `AreaId` namespace/validation `ObjectiveAreas`/`ExtractionAreas`
already share; `ScenarioContent.Version` `2 -> 3`.

**`Events.fs`**: `ReloadStarted`, `ReloadCompleted`, `AgentResupplied`
(each emitted once, on the transition, the `AgentIncapacitated`/
`AgentDied` precedent); the event-ordering doc comment's item 8 extended.

**`Commands.fs`**: `Command.hold`/`.assault`/`.withdraw` builders (the
`Command.suppress` precedent).

**`ReplaySerialisation.fs`**: `hold`/`assault`/`withdraw` and
`queue-hold`/`queue-assault`/`queue-withdraw` grammar keywords, no version
bump (the grammar already had room, the `suppress` TASK-037 precedent);
the doc comment's grammar listing and "N intent keywords" count updated.

**`Diagnostics.fs`**: new sparse `Overlay.AgentAmmo` (the `AgentSuppression`
precedent); `agentAmmoOverlays` wired into both `frame` and `frameOf`;
`eventMarker` gained the three new event cases; `commitmentOverlays`/
`orderAppraisalOverlays` updated for the new `Commitment.ofAgent`/
`Appraisal.appraise` signatures.

**Headless renderers/corpus builder** (`DiagnosticRender.fs`, `Corpus.fs`,
`AppraisalDemo.fs`, `Program.fs`, `DemoScenario.fs`/`LosDemo.fs`/
`PathDemo.fs`): exhaustive-match arms for every new case; `Corpus.fs`
gained `HoldOrder`/`AssaultOrder`/`WithdrawOrder` `ScenarioIntent` cases,
`holdOrder`/`assaultOrder`/`withdrawOrder` builders, and a `Resupply: Cell
option` field on `ScenarioSpec` (added to all 14 existing entries as
`None`, mechanically, via `sed`); every `RawScenario` literal across the
codebase gained `ResupplyAreas = [||]`.

**Godot client** (`RenderShared.fs`, `FSharpSceneHost.cs`,
`AppraisalDemoScene.cs`, `README.md`): `reasonText`/`devReasonText`/
`devCommitmentText` exhaustive-match arms for the new cases; a new
`ammoText` helper and an `ammo=` field appended to the existing `[dev]`
per-agent HUD line (Decision O — F1 developer overlay only, no
player-facing ammo readout); both scenes' pinned `--selfcheck` hashes and
`AppraisalDemoScene`'s exposed-approach pin updated (see Outcome/Review in
the task file for how, and the flag for Dave).

### Verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0`.
- `dotnet build` the Godot client `.slnx` (Debug and Release): `0/0` both.
- `dotnet test CommandoWar.slnx -c Release`: `336/336` (319 -> 336, +17 new
  `SimulationTests` facts covering `assaultStage`'s four states, the
  Withdraw/Assault threshold contrast, Hold's cover redirect, and the full
  ammo fire/reload/resupply/gate cycle).
- `cwheadless corpus`: `16/16`; `--regenerate` twice byte-identical
  (`md5sum` diff empty).
- `cwheadless fixture`: unaffected — 36 events, byte-layout-only re-pin.
- `cwheadless replay-file content/replays/envelope-full.cwreplay`: OK, 78
  events unchanged, 24/24 checkpoints match.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`: `FSharp.Core` only.
- Source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`: clean.
- `git status --porcelain`: matches the task file's Allowed scope.

### Evidence

- `dotnet test` summary (336/336).
- `cwheadless corpus`/`fixture`/`replay-file` command output (this ledger
  entry, above).
- Every regenerated `content/replays/*.{cwreplay,md}` and
  `content/diagnostics/*.{ascii.txt,svg,html}` golden.

### Deviations and unresolved issues

See the task file's own Outcome section: `Commitment.ofAgent`'s new
context parameters needed a third call site beyond Decision I's original
two (`commitmentAndLocalAction`, for the Assault freeze); a test-only
off-by-one in the reload fact's loop bound, not the `Ammo.tick` leaf
(confirmed correct against the `Casualty.tickBleedOut` precedent it
copies). No new corpus entry — see the task file's "Considered and
rejected" list for the reasoning. Godot's three pinned `--selfcheck`
hashes were originally computed via `dotnet fsi` against the built Core
assembly (no Godot install in the implementing environment); re-run
through the real Godot 4.7.2 editor before acceptance and confirmed
`MATCH` (see Review below).

### Documents updated

- `tasks/TASK-047-ASSAULT-WITHDRAW-HOLD-AND-AMMUNITION.md` created,
  implemented, and self-verified (`drafted -> review`).
- `docs/11_BACKLOG.md` B-030 row (`proposed -> review`).
- `docs/04_SIMULATION_SPEC.md` sections 12.8, 12.9, 13, 21.
- `docs/05_COMMAND_AND_AGENT_AI.md` sections 4, 7, 8, 9, 10.
- `docs/07_VERTICAL_SLICE.md` sections 4, 5.
- `src/CommandoWar.Client.Godot/README.md` new section.
- `docs/12_PROGRESS_LEDGER.md` this row.
- `PROJECT_STATE.yaml` `active_work` updated.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-18). Godot's three pinned `--selfcheck` hashes
  re-run through the real Godot 4.7.2 editor headless before acceptance:
  `SnapshotDemo.tscn` `MATCH 0x8E93B48D07AE9CBD`, `CommandDemo.tscn` `MATCH
  0xE661187DE95E92E6`, `AppraisalDemo.tscn` exposed-approach `MATCH
  0x194805888CBE240D` (format 11) — all three exit 0.
