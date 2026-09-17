## 2026-09-17 - TASK-045 - Casualties, incapacitation, leadership succession, and squad failure (B-031)

**Owner:** Dave
**Source revision:** implemented directly on `main`, no branch (the TASK-035/036 low-risk precedent)
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot 4.7.2.stable.mono
**Status change:** `tasks/TASK-045-*.md` `proposed -> review`

### Why

B-031's dependencies (B-019/TASK-031 hitscan combat, B-021/TASK-033 stress and
bounded reappraisal) were both `done`. Selected as the next task via
`AskUserQuestion` over B-030 proper, B-053, and B-054, immediately after Dave
accepted TASK-044 ("no new UI to review, just accept and proceed to the next
task"). Three central decisions confirmed with Dave via `AskUserQuestion`
before drafting: leader identity is a pure derived rule (lowest-id living
friendly, no new stored field, the `Commitment.ofAgent` precedent); wound
state is wired into `Appraisal` in this task, not deferred (stage-2
`Unable(CriticallyWounded)`, stage-4 resolve penalty, a new wounded
reappraisal trigger); no rescue/stabilize mechanic — bleed-out is
deterministic once triggered.

### Changes

**`src/CommandoWar.Sim/Domain.fs`**: `VitalStatus = Alive of health: int |
Incapacitated of bleedOutRemaining: int | Dead` — a single field rather than a
separate `Health: int` alongside a status flag, so "wounded but not alive"
cannot be constructed. `AgentState.Vitals: VitalStatus` and
`AgentState.RecentlyWounded: bool`, both genuine per-tick canonical state (the
`Suppression`/`Stress` precedent). `DecisionReason.CriticallyWounded`.
`Agent.MaxHealth = 1000` literal (the `Agent.DisciplineDefault`
module-ordering precedent); `Agent.create` sets `Vitals = Alive MaxHealth`,
`RecentlyWounded = false`.

**`src/CommandoWar.Sim/Casualty.fs`** (new leaf, the `Suppression.fs`/
`Stress.fs` precedent exactly — pure, total, integer-only, no event
emission): `CasualtyConfig.WoundPerHit = 350`, `.BleedOutTicks = 60`;
`Casualty.wound`/`.tickBleedOut` (both total, no-ops on an unreachable input
rather than crashing); `Casualty.isAlive`; `Casualty.currentLeader` — the
lowest-`AgentId` `Alive` `Friendly` agent, or `None`.

**`src/CommandoWar.Sim/Appraisal.fs`**: stage 2 gains a hard-refusal check
ahead of the existing `MoveTo`/`Suppress` logic — a non-`Alive` agent's order
is `Unable(CriticallyWounded, [||])`, a whole-order short-circuit. Stage 4
(`resolveThreshold`) gains a continuous wound penalty, the `Stress` precedent
exactly: `(Agent.MaxHealth - health) / AppraisalConfig.WoundDivisor` when
`Alive health`, `0` otherwise.

**`src/CommandoWar.Sim/Simulation.fs`**:
- `StepState` gains `InitialLeader: AgentId option` and
  `InitialFriendlyAlive: bool`, computed once in `Simulation.step` from the
  pristine pre-tick `state.Agents`, before any phase runs (see Bug found
  during implementation below for why this could not be a locally-captured
  snapshot).
- `combat`: the shooter loop and each shooter's candidate list are both
  filtered to `Alive` agents only. On a qualifying hit, `Casualty.wound`
  applies to the target's `Vitals` and `RecentlyWounded` latches `true` in the
  same copy-before-write pass that already raises `Suppression`; the tick
  health first reaches zero emits `AgentIncapacitated`.
- `stateConsequences`: every `Incapacitated` agent's bleed-out countdown ticks
  down (`Casualty.tickBleedOut`), reaching `Dead` emits `AgentDied`; the
  derived leader computed after this phase's own mutations is diffed against
  `s.InitialLeader` (LeadershipTransferred` on a difference); squad failure —
  `s.InitialFriendlyAlive` true but no `Friendly` agent `Alive` after this
  phase's mutations — emits `SquadFailure` once, a signal event only (no
  `Simulation.step` halt, no new `WorldState` field).
- `appraisal`: a fourth reappraisal trigger, `a.RecentlyWounded`, read fresh
  and cleared to `false` every tick regardless of whether it changed the
  outcome (the `SuppressionBand`-latch precedent).
- `navigationAndMovement`: a non-`Alive` agent is `Idle` unconditionally; its
  `Destination`, if any, is left inert, not cleared.

**`src/CommandoWar.Sim/Events.fs`**: `AgentIncapacitated`, `AgentDied`,
`LeadershipTransferred of previous: AgentId option * current: AgentId
option`, `SquadFailure` (no payload — Friendly-only by construction).

**`src/CommandoWar.Sim/Canonical.fs`**: `FormatVersion` 9 -> 10. `writeAgent`
gains a `Vitals`/`RecentlyWounded` section; `reasonCode`/`writeReason` gain
`CriticallyWounded`.

**`src/CommandoWar.Sim/Diagnostics.fs`**: `Overlay.AgentVitals of agent * at *
vitals`, unconditional per agent (the `AgentCommitment` precedent — `Alive` is
itself meaningful, not sparse); `Overlay.SquadLeadership of leader: AgentId
option`, one per frame, no cell. Both derived by `frame` and `.frameOf`.

**`src/CommandoWar.Headless/DiagnosticRender.fs`**: `Ascii`/`Svg`/`Html` for
both new overlays (a red dot / grey `Z<n>` bleed-out badge / black cross for
wounded / incapacitated / dead in `Svg`; a gold ring around the leader's own
agent circle); `eventNarration` for the four new event kinds.

**`src/CommandoWar.Headless/Corpus.fs`**: new entry
`casualties-succession-and-squad-failure` (10x10, two friendlies vs. two
hostiles, 65 ticks, no player orders — combat and its consequences run to
completion unattended), proving wound accumulation, incapacitation, bleed-out,
death, leadership succession, and squad failure end to end.

**`src/CommandoWar.Client.Godot/Core/RenderShared.fs`**: both `DecisionReason`
match expressions (`reasonText`, `devReasonText`) needed the new
`CriticallyWounded` case to keep compiling — `"critically wounded"` /
`"critically-wounded"`, the existing hyphenation convention each already
follows.

**Godot client `--selfcheck` re-pin**: `SnapshotDemo.tscn`
`0xC68F993BC605313C -> 0x44B29B73E8F107EF`, `CommandDemo.tscn`
`0xD27E623504262CE9 -> 0xF1027A36B36BC3DF` (byte-layout only — neither
scripted drive touches combat, so every agent stays `Alive` at full health
throughout, tick/event counts unchanged). `src/CommandoWar.Client.Godot/README.md`
gained a new section documenting the re-pin and the `RenderShared.fs` fix.

### Bug found during implementation (production code, not test-only)

`stateConsequences`'s leadership/squad-failure detection originally computed
its own `before = s.Agents` snapshot at its own phase entry and diffed against
its own mutations — reasoning that "Combat runs before this phase, so any
leader-incapacitating hit is already reflected in `before`". That reasoning
was backwards: because Combat's mutation is *already* reflected in that local
`before` (Combat wrote `s.Agents` earlier the same tick), a before/after diff
taken *within* `stateConsequences` can only ever catch `stateConsequences`'s
own mutations (bleed-out to `Dead`) — it can never see a leader-incapacitating
hit Combat itself just caused, silently dropping the `LeadershipTransferred`/
`SquadFailure` event the tick it should have fired. Caught via a scratch
trace script showing `leadership: []` despite an agent visibly becoming
`Incapacitated` that same tick. Fixed by adding `StepState.InitialLeader`/
`.InitialFriendlyAlive`, computed once in `Simulation.step` from the pristine
pre-tick `state.Agents` before any phase runs, and diffing against those
instead. Covered by a new dedicated `SimulationTests` fact
(`` `leadership transfers the same tick Combat incapacitates the current
leader, not the tick after` ``) that drives real Combat (a 1-health leader
under sustained fire, a bounded 40-tick loop) rather than injecting state
directly, since injecting a pre-Incapacitated agent cannot exercise this path
at all — it would already read as non-`Alive` before the tick the bug lived
in even starts.

Re-pinning the pre-existing corpus after this fix landed changed four
entries' exact event counts a second time (`perception-contact`,
`exposed-approach`, `suppress-relieves-exposure`,
`canonical-refusal-and-correction`) — each entry's friendly-vs-hostile combat
now correctly emits a `LeadershipTransferred`/`SquadFailure` it had
previously (incorrectly) missed. Tick counts are unchanged everywhere; the
`Corpus.fs` `Description` field for each entry documents the exact before/
after numbers (re-pinning the `.md` file directly does not persist — see the
test-hang / tooling note below).

### Test-only issue found during implementation

`dotnet test` hung indefinitely partway through this task. Diagnosed via
`Get-Process testhost` CPU accumulation (climbing steadily — a real spin, not
an idle wait) and bisected via `--filter` to
`` `a contact seen then lost drops a confidence band after StaleAfter and
expires with ContactExpired after ExpireAfter` `` in `SimulationTests.fs`. Its
`while seenThisTick st do st <- (stepIdle st).State` loop assumed the walking
friendly agent would always eventually walk far enough to lose sight of the
hostile — true before this task, but the world's friendly and hostile were
within `CombatConfig.WeaponRange`, so with real wound consequences now wired
in, the friendly was Incapacitated mid-walk and correctly stopped moving
forever (`navigationAndMovement`'s own new invariant), so it never walked out
of sight, hanging the loop. Fixed by moving the hostile to keep the pair
within `SightRange` but outside `WeaponRange` throughout, plus a defensive
`ticks < 40` cap (an existing file precedent). Test-only; no production
change.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0/0`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `319/319` (306 + 13 new — see `docs/12_PROGRESS_LEDGER.md`'s
    Green tests row for the full breakdown; run twice, stable both times).
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug` and `-c Release`
  - Result: `0/0` both, after the `RenderShared.fs` `CriticallyWounded` fix.
- Command: `dotnet build src/CommandoWar.Client.Mibo/CommandoWar.Client.Mibo.slnx -c Debug`
  - Result: `0/0`.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate` (run twice)
  - Result: 16 entries written both times; a SHA-256 diff of every
    `content/replays/*.md`/`*.cwreplay` file between the two runs was empty
    (byte-identical).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  - Result: final hash `0xF0CEAD6CE48BA07E` (format 10), 36 events, matches
    `FixtureTests.fs`.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- replay-file content/replays/envelope-full.cwreplay`
  - Result: `checkpoints : OK (24 ticks match the file's committed hashes)`,
    78 events unchanged, canonical 10.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package`
  - Result: `FSharp.Core` only.
- Command (Godot, headless, both scenes): `"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck` / `scenes/CommandDemo.tscn`
  - Result: `MATCH` the newly re-pinned hashes at tick 20, both scenes.
- Manual: `git status --porcelain`
  - Result: matches this task's Allowed scope list.

### Evidence

- New golden diagnostic pairs:
  `content/diagnostics/casualties-succession-and-squad-failure-tick-005.{ascii.txt,svg}`
  (every agent `Incapacitated`, the second `LeadershipTransferred` to `None`
  and `SquadFailure` both fire) and `-tick-065.{ascii.txt,svg}` (every agent
  `Dead`, black crosses, no `SquadLeadership` ring).
- New corpus entry: `content/replays/casualties-succession-and-squad-failure.{md,cwreplay}`.
- Re-pinned `content/replays/envelope-full.{md,cwreplay}` (initial hash
  `0xD63C7909BA798617`, final `0x68733B3C55995DAC`, 78 events unchanged).
- Scratch `dotnet fsi` probes (removed after use): one that first exposed the
  `leadership: []` bug via a hand-traced tick sequence, one used to recompute
  `envelope-full`'s hashes, one used to regenerate the two new diagnostic
  goldens.

### Documents updated

- `tasks/TASK-045-CASUALTIES-SUCCESSION-AND-SQUAD-FAILURE.md` (drafted,
  implemented; Status `proposed -> review`).
- `docs/11_BACKLOG.md` B-031 row (`proposed -> review`).
- `content/diagnostics/README.md` (two new file rows + regeneration-section
  entry).
- `docs/12_PROGRESS_LEDGER.md` (Pinned facts `Canonical.FormatVersion`
  9 -> 10 and Green tests `306 -> 319`; this row).
- `src/CommandoWar.Client.Godot/README.md` (new TASK-045 section).
- `PROJECT_STATE.yaml`.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-17, "seems to work"). One follow-up raised on
  acceptance, not blocking: without the developer overlay (`F1`) toggled on,
  there is no player-facing visual indicator that a shot was fired or that
  an agent is wounded/incapacitated — `CommandDemoScene`'s `fireLines`
  (`FireLine` overlay) and the equivalent for `AgentVitals` are both
  computed only inside the `devOverlay` branch of `DrawList()`, confirmed by
  inspection, not assumption. Dave also floated needing "better graphics,
  maybe another free graphics pack" — an open question, not yet decided:
  whether player-facing combat/casualty feedback needs new art assets or can
  reuse existing line/marker primitives (the `FireLine` colour-coded line,
  a wound-state badge) is unresolved. Recorded as new backlog row B-057, not
  implemented here — out of this task's own Forbidden scope (Sim-side only,
  no new client UI).
