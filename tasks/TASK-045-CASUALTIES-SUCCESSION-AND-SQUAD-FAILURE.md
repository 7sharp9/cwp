# TASK-045: Casualties, incapacitation, leadership succession, and squad failure

Status: done (accepted by Dave 2026-09-17, "seems to work" — a follow-up raised, not blocking, see B-057)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete)
Size: L (backlog estimate M; the TASK-039/043/044 precedent of a new
canonical-state task landing bigger in practice, and this one touches Combat,
Appraisal, and two new phase-level checks)

## Outcome (2026-09-17)

Implemented as drafted. `AgentState.Vitals: VitalStatus` (`Alive of health |
Incapacitated of bleedOutRemaining | Dead`, a single field so an invalid
wounded-but-not-alive combination cannot be constructed) and
`AgentState.RecentlyWounded: bool`, both genuine canonical state. New leaf
`Casualty.fs` (the `Suppression.fs`/`Stress.fs` precedent). `combat` filters
shooters and targets to `Alive` only, applies `Casualty.wound` on a hit, emits
`AgentIncapacitated` on the health-reaches-zero transition. `stateConsequences`
ticks bleed-out (`AgentDied` at zero), diffs the derived leader, and latches
`SquadFailure` (a signal event only — no `Simulation.step` halt, no new
`WorldState` field). `Appraisal.fs` gains stage-2 `Unable(CriticallyWounded)`
and a stage-4 wound resolve penalty; `Simulation.appraisal` gains the wounded
reappraisal trigger. `navigationAndMovement` skips non-`Alive` agents,
Destination left inert. Two new unconditional diagnostic overlays
(`AgentVitals`, `SquadLeadership`), four new events, one new corpus entry
(`casualties-succession-and-squad-failure`) proving the full lifecycle end to
end. `Canonical.FormatVersion` 9 -> 10; every pre-existing corpus entry, the
shared fixture, and `envelope-full` re-pinned byte-layout-only (`--regenerate`
twice byte-identical; tick counts unchanged everywhere).

**Production bug found and fixed during implementation** (not in the original
design): `stateConsequences`'s leadership/squad-failure diff originally
compared against a snapshot taken at its own phase entry — already mutated by
Combat, which runs earlier the same tick — so it could only ever catch its
own bleed-out-to-`Dead` transitions, never one Combat itself just caused,
silently dropping the event the tick it should have fired. Fixed by adding
`StepState.InitialLeader`/`.InitialFriendlyAlive`, the true pre-tick baseline
computed once in `Simulation.step` before any phase runs. Covered by a new
`SimulationTests` fact that drives real combat (a 1-health leader under
sustained fire, a bounded loop) rather than injecting state directly, since
injection cannot exercise this path at all. Re-pinning after the fix changed
four pre-existing corpus entries' exact event counts a second time
(`perception-contact`/`exposed-approach`/`suppress-relieves-exposure`/
`canonical-refusal-and-correction`; tick counts unchanged, each entry's
`Corpus.fs` `Description` documents the exact before/after numbers).

**Test-only issue found and fixed** (not a production bug): a
`SimulationTests` perception fact's `while seenThisTick ...` loop hung
indefinitely once real wound consequences existed — its world had the walking
friendly agent within `CombatConfig.WeaponRange` of a hostile, so the new "an
Incapacitated agent never moves" invariant correctly stopped it forever
mid-loop, and it never walked out of sight. Fixed by moving the geometry
outside weapon range (still within sight range) plus a defensive tick cap,
the existing file's own precedent.

Godot client: `RenderShared.fs`'s two `DecisionReason` match expressions
needed the new `CriticallyWounded` case to keep compiling (no other client
change — this task is Sim-side only, no new scene or input). Both scenes'
pinned `--selfcheck` hashes moved with the `FormatVersion` bump
(`SnapshotDemo.tscn` `0xC68F993BC605313C -> 0x44B29B73E8F107EF`,
`CommandDemo.tscn` `0xD27E623504262CE9 -> 0xF1027A36B36BC3DF`), re-verified
`MATCH` through the real Godot 4.7.2 editor, headless.

`dotnet build CommandoWar.slnx -c Release`: `0/0`. `dotnet build` the Godot
client `.slnx` (Debug and Release) and the Mibo client `.slnx`: `0/0` each.
`dotnet test`: `319/319` (306 pre-existing + 13 new — ten `SimulationTests`
casualty facts, one `DiagnosticsTests` end-to-end fact, one
`DeterminismPropertyTests` property, one `CorpusTests` theory row picked up
automatically). `dotnet list CommandoWar.Sim` package: `FSharp.Core` only.

Full detail: `docs/ledger/2026-09-17-TASK-045-casualties-succession-and-squad-failure.md`.

## Objective

Give a hit real consequence for the first time: a wounded agent's health
degrades over repeated hits; reaching zero incapacitates it (unable to act,
per docs/04 section 20's own invariant) rather than killing it outright; an
incapacitated agent bleeds out over a fixed countdown to death if nothing
intervenes (docs/04 section 12.8's own direction: "death as a critical/
bleed-out timer rather than a binary kill"). Squad leadership is a pure
derived rule (the lowest-id living friendly agent) that transfers
automatically on the leader's death or incapacitation — "a simple
replacement rule", docs/05 section 17's own scope cap. Squad failure (every
friendly agent dead or incapacitated) is signalled, not itself enforced as a
mission outcome (that is B-032's job). A wounded, incapacitated, or dead
agent's order is appraised accordingly: `Unable(CriticallyWounded)` at stage
2, a continuous wound-based resolve penalty at stage 4, and a new "the agent
is wounded" reappraisal trigger.

## Why this task exists

B-031's dependencies (B-019/TASK-031 hitscan combat, B-021/TASK-033 stress
and bounded reappraisal) are both `done`. Selected via `AskUserQuestion` over
B-030 proper, B-053, and B-054. `Combat.fs`'s own header has said "any wound /
death consequence, agent removal, incapacitation — B-031" since TASK-031;
`docs/04` section 12.9's "apply deaths and incapacitation" and "update
command succession" have been named, unrealised, since the same task.
`docs/07` section 9 criteria 6 ("leader death transfers command according to
an explicit rule") and 7 ("the mission can succeed and fail without developer
intervention" — this task's half is the failure signal; B-032 consumes it)
are still open vertical-slice acceptance criteria this task closes the first
half of.

## Central decisions (confirmed with Dave 2026-09-17 before drafting)

Three forks, put via `AskUserQuestion`:

1. **Leader identity is a pure derived rule: the lowest-`AgentId` living
   friendly agent**, recomputed every tick — no new stored field, the
   `Commitment.ofAgent` precedent ("derived, not stored, so it cannot drift").
   Succession is automatic: when the lowest id dies or is incapacitated, the
   next-lowest survivor becomes leader next tick, an explicit and trivially
   provable rule. Rejected: an authored `Deployment.IsLeader` flag — more
   scaffolding now for a distinction with no other authored use yet.
2. **Wound state is wired into `Appraisal` in this task**, not deferred: a
   new stage-2 hard-refusal check (`Unable(CriticallyWounded)`, docs/05
   section 4 stage 2's own "is the agent alive, conscious, and mobile?"), the
   `DecisionReason.CriticallyWounded` case docs/05 section 7 has named since
   TASK-028 waiting for "the system that can trigger it", and a new "the
   agent is wounded" reappraisal trigger (docs/05 section 14) — the
   B-021/TASK-033 precedent of wiring a new state's reappraisal trigger in
   the same task that adds the state, not a follow-up.
3. **No rescue/stabilize mechanic.** Once an agent's health reaches zero it
   is `Incapacitated`; a fixed countdown always runs to `Dead` with nothing a
   player command can do about it. No new `PlayerIntent` case, no new
   `Appraisal` branch for it, no new `Commitment` case. A `Stabilize` order is
   a clean, separately-sizeable follow-up if wanted later (not filed as a
   backlog row by this task — file one only if Dave asks for it after seeing
   this land).

## Design

**`AgentState.Vitals: VitalStatus`** (new type, `Domain.fs`), replacing a
separate `Health` field so an invalid combination (health without being
alive, or vice versa) cannot be constructed:

```fsharp
type VitalStatus =
    | Alive of health: int          // 0 < health <= CasualtyConfig.MaxHealth
    | Incapacitated of bleedOutRemaining: int   // > 0; reaches 0 -> Dead
    | Dead
```

`Agent.create` initialises `Vitals = Alive CasualtyConfig.MaxHealth`. Genuine
new canonical per-tick state (the `Suppression`/`Stress` precedent exactly):
`Canonical.FormatVersion` bumps, `writeAgent` gains a `Vitals` section.

**`AgentState.RecentlyWounded: bool`** (new field, `Domain.fs`) — a one-shot
dirty flag bridging a real phase-ordering gap: `Combat` (phase 8) runs
*after* `Appraisal` (phase 5), so a wound taken this tick cannot be seen by
this tick's `Appraisal` the way `Perception`'s same-tick `ContactObserved`
can (`Perception` runs at phase 3, before `Appraisal`). `SuppressionBand`
solves the identical problem for the suppression-band trigger by persisting a
latch across the phase-order gap; `RecentlyWounded` is the same idea, minus
the hysteresis (any wound qualifies, there is no band). Set `true` by
`stateConsequences` whenever this tick's `Combat`-inflicted hit strictly
reduced an `Alive` agent's health (checked from a "before" snapshot the phase
already needs for the leadership/squad-failure diff below); read and cleared
back to `false` by the *following* tick's `appraisal` phase, the trigger's
own consumption. Canonical (it is real per-tick memory, not derivable from
`Vitals` alone).

**`src/CommandoWar.Sim/Casualty.fs`** (new leaf, the `Suppression.fs`/
`Stress.fs` precedent exactly: pure, total, integer-only, `Domain` only, no
event emission — the owning phase emits):

```fsharp
module CasualtyConfig =
    MaxHealth = 1000            // the Suppression/Stress 0..1000 scale precedent
    WoundPerHit = 350           // ~3 qualifying hits to incapacitate; no cover
                                 // mitigation on the amount itself -- cover
                                 // already gated whether the hit landed at all
                                 // (Combat.hitChance), so mitigating twice would
                                 // double-count it
    BleedOutTicks = 60          // 3s at the standard 20 Hz tick rate -- long
                                 // enough to read as a real countdown in a
                                 // diagnostic trace and the (future) HUD, short
                                 // enough that "no rescue" doesn't feel stalled

module Casualty =
    // Alive h -> Alive (h - WoundPerHit), or Incapacitated BleedOutTicks if
    // that would reach <= 0. Only ever called on an Alive target (Combat
    // phase already excludes non-Alive targets, see below).
    let wound (health: int) : VitalStatus
    // Incapacitated n -> Incapacitated (n - 1), or Dead at 0. Idempotent no-op
    // (returns the input unchanged) on Alive or Dead.
    let tickBleedOut (vitals: VitalStatus) : VitalStatus
```

**`Combat.fs`/`Simulation.combat`**: the shooter loop and each shooter's
candidate list are both filtered to `Alive` agents only — a dead or
incapacitated agent never fires (docs/04 section 20: "do not start new
actions") and is never targeted again once down (no "finishing" mechanic
asked for; a downed agent simply drops out of every future qualifying-target
computation, `Combat.chooseTarget` itself unchanged). On a qualifying **hit**
only (not a miss — wounds are physical, unlike `Suppression.gain`, which
`docs/04` section 12.8 explicitly wants independent of a hit), apply
`Casualty.wound` to the target's `Vitals` in the same copy-before-write pass
that already raises `Suppression`. Emit `AgentIncapacitated(agent, at)` the
tick health first reaches zero (a genuine state transition, the
`CommitmentEstablished` precedent — emitted once, not every tick the agent
stays down).

**`Simulation.stateConsequences`**: gains, after the existing suppression-
decay/stress steps, in this order:
1. Bleed-out: every `Incapacitated` agent's countdown ticks down
   (`Casualty.tickBleedOut`); reaching `Dead` emits `AgentDied(agent, at)`.
2. Leadership: compute the derived leader (lowest-id `Alive` friendly) from
   the agent array *before* this phase's own mutations and again *after*;
   if they differ, emit `LeadershipTransferred(previous: AgentId option,
   current: AgentId option)`. `None` on either side covers "no leader" (every
   friendly already down) and is itself meaningful trace evidence.
3. Squad failure: if no friendly agent was `Alive` *before* this phase and at
   least one still is `After` is impossible by construction (health never
   regenerates), so this is a one-way latch computed the same "before vs.
   after" way — every friendly `Alive` at the start of the phase, none `Alive`
   at the end -> emit `SquadFailure` once, the tick it first becomes true.
   Scoped to the `Friendly` side only (docs/04 section 10's own "every
   Friendly agent is the one squad" simplification; no equivalent "mission"
   concept exists for `Hostile`). This is a **signal event only** — it does
   not halt `Simulation.step`, reject further commands, or write any new
   `WorldState` field; consuming it into an actual mission-failure outcome is
   B-032's job (docs/04 section 12.10's Mission phase, still a no-op).

**`Appraisal.fs`**: stage 2 gains a hard-refusal check ahead of the existing
`MoveTo`/`Suppress` stage-2 logic — `match order.Intent with _ when not
(isAlive agent's Vitals) -> Unable(CriticallyWounded, [||])` (a whole-order
short-circuit, the "physical inability" precedent docs/05 section 16 names
explicitly: "a critically wounded agent reports unable rather than refused").
Stage 4 (`resolveThreshold`) gains a continuous wound penalty, the `Stress`
precedent exactly: `(CasualtyConfig.MaxHealth - health) /
WoundResolveDivisor` when `Alive health`, `0` otherwise (an `Unable` agent
never reaches stage 4 anyway). `DecisionReason` gains `CriticallyWounded`
(`Domain.fs`); `Canonical.reasonCode`/`.writeReason` gain the new case.

**`Simulation.appraisal`**: gains a fourth reappraisal trigger, `agent is
wounded` (docs/05 section 14) — `a.RecentlyWounded`, read fresh each tick
(the `SuppressionBand`-latch precedent) and cleared to `false` in the same
pass regardless of whether it actually changed this appraisal's outcome (a
wounded-but-still-`Accepted` order still consumes the flag — "reappraise
only on material triggers" governs whether the OUTCOME changes, not whether
the flag is spent).

**`Simulation.navigationAndMovement`**: skips a non-`Alive` agent entirely
(no path recompute, no progress accumulation, no movement events) — the
docs/04 section 20 invariant applied to movement specifically. An
`Incapacitated`/`Dead` agent's `Destination`, if any, is left as-is (inert,
matching the existing "an undelivered order does not clear a Destination it
didn't produce" idiom) rather than actively cleared — nothing reads it while
non-`Alive`.

**`Events.fs`**: `AgentIncapacitated of agent: AgentId * at: Cell`,
`AgentDied of agent: AgentId * at: Cell`, `LeadershipTransferred of previous:
AgentId option * current: AgentId option`, `SquadFailure` (no payload — see
Design above for why it is Friendly-only by construction, not by an unused
field).

**`Diagnostics.fs`**: two new `Overlay` cases, both AGENTS.md's diagnostics
rule for new authoritative tactical state:
- `AgentVitals of agent: AgentId * at: Cell * vitals: VitalStatus` —
  unconditional per agent (the `AgentCommitment` precedent: "Alive" is itself
  meaningful, not sparse).
- `SquadLeadership of leader: AgentId option` — one entry per frame, no cell
  (a squad-wide fact, not per-agent; the closest existing precedent is a
  frame-level fact like `Hash`/`RandomDraws`, but those live on
  `DiagnosticFrame` directly, not `Overlay[]` — reusing `Overlay[]` here
  instead of extending `DiagnosticFrame` itself is the smaller change, one
  new case vs. a new field on every renderer's frame-construction call site).

Both `Diagnostics.frame` and `.frameOf` derive both (standing canonical
state, not a this-tick event).

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `docs/04_SIMULATION_SPEC.md` sections 4, 10, 11, 12.5, 12.8, 12.9, 12.10, 14,
  17, 20
- `docs/05_COMMAND_AND_AGENT_AI.md` sections 3, 4, 7, 8, 11, 14, 16, 17
- `docs/07_VERTICAL_SLICE.md` sections 5, 9 (criteria 6, 7)
- `docs/09_TEST_STRATEGY.md` sections 2.1, 2.3, 2.4 (named-but-unbuilt
  scenarios: "leader death transfers authority", "leader death and
  succession", "mission failure by squad loss")
- `src/CommandoWar.Sim/Combat.fs`, `Suppression.fs`, `Stress.fs` (the three
  leaf-module precedents `Casualty.fs` follows)
- `src/CommandoWar.Sim/Appraisal.fs` (`appraise`, `resolveThreshold`,
  `AppraisalConfig` — the `StressDivisor` precedent for the new wound
  divisor)
- `src/CommandoWar.Sim/Simulation.fs` — `combat`, `stateConsequences`,
  `appraisal`, `navigationAndMovement` phase bodies and their large comment
  blocks (the exact hook points this task extends)
- `src/CommandoWar.Sim/Domain.fs` (`AgentState`, `DecisionReason`, `Agent.create`)
- `src/CommandoWar.Sim/Canonical.fs` (`writeAgent`, `reasonCode`/
  `writeReason`, `FormatVersion` history — the pattern for documenting a new
  bump)
- `src/CommandoWar.Sim/Events.fs`, `Diagnostics.fs` (`Overlay`,
  `AgentCommitment`/`UndeliveredOrder` — the unconditional-vs-sparse overlay
  precedents this task's two new cases each follow)

## Dependencies

- B-019 (TASK-031), B-021 (TASK-033), both done. No other task selected.

## Allowed scope

- `src/CommandoWar.Sim/Domain.fs`: `VitalStatus`, `AgentState.Vitals`,
  `AgentState.RecentlyWounded`, `DecisionReason.CriticallyWounded`,
  `Agent.create` defaults.
- New `src/CommandoWar.Sim/Casualty.fs`.
- `src/CommandoWar.Sim/Appraisal.fs`: stage-2 check, stage-4 wound penalty,
  `AppraisalConfig.WoundResolveDivisor`.
- `src/CommandoWar.Sim/Simulation.fs`: `combat`, `stateConsequences`,
  `appraisal`, `navigationAndMovement`.
- `src/CommandoWar.Sim/Events.fs`: the four new event cases; the
  `DomainEvent` ordering comment updated.
- `src/CommandoWar.Sim/Canonical.fs`: `FormatVersion` bump, `writeAgent`'s
  new `Vitals` section, `reasonCode`/`writeReason`'s new case.
- `src/CommandoWar.Sim/Diagnostics.fs`: `AgentVitals`, `SquadLeadership`
  overlays, `frame`/`.frameOf` wiring.
- `src/CommandoWar.Headless/DiagnosticRender.fs`: `Ascii`/`Svg`/`Html` for
  both new overlays; `eventNarration` for the four new event kinds.
- `src/CommandoWar.Headless/Corpus.fs`: at least one new corpus entry proving
  wound -> incapacitation -> bleed-out -> death, leadership succession on the
  leader's death, and squad failure once every friendly is down — one entry
  if it can carry all three cleanly, otherwise split (decide once the
  geometry is in front of you; do not force an artificial single scenario if
  it reads as contrived).
- `tests/CommandoWar.Sim.Tests/`: new `SimulationTests.fs` facts (wound
  accumulation and the incapacitation threshold; bleed-out countdown and
  death; a dead/incapacitated agent never fires and is never targeted; a
  dead/incapacitated agent never moves; `Unable(CriticallyWounded)` at
  appraisal; the wound resolve penalty; the wounded reappraisal trigger;
  leader identity and succession on death; squad failure fires exactly once);
  a `CanonicalHashTests`/`DeterminismPropertyTests` fact or property for the
  new canonical section, the `Suppression`/`Stress` precedent; `CorpusTests`
  theory case(s) for the new entry/entries.
- `content/diagnostics/` new golden pair(s) + `README.md`.
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Forbidden scope

- No `Stabilize`/rescue `PlayerIntent`, no new `Commitment` case (Central
  decision 3).
- No authored leader flag, no `Squads`/`SquadStore` construct (Central
  decision 1; B-011d, still `proposed`, is the row that would add real
  sub-squad grouping — out of scope here).
- No `Mission` phase implementation, no mission success/failure `WorldState`,
  no simulation halt on squad failure — `SquadFailure` is a signal event
  only (B-032's job to consume it).
- No ammunition, weapon readiness, fire-rate/cooldown model (still
  unassigned future work, `Combat.fs`'s own header).
- No `Assault`/`Withdraw` executors (B-030 proper) — wound state's stage-2/
  stage-4 Appraisal changes apply to whatever orders exist today
  (`MoveTo`/`Suppress`), not to intents that don't exist yet.
- No explosion- or isolation-driven stress (`docs/05` section 8's other two
  unrealised stress sources — casualty/wound-driven stress on a *nearby*
  agent, as opposed to the wounded agent's own resolve penalty above, is also
  out of scope: "nearby casualties" as a stress source needs a
  proximity/observation model this task does not build; only the wounded
  agent's own stage-4 penalty is in scope).
- No change to `Combat.chooseTarget`'s or `Combat.hitChance`'s own signature
  or geometry — only the phase-level candidate filtering around them.
- Nothing under `src/_scratch/`, `bench/`, `content/benchmarks/BASELINE.md`.

## Required work

1. `Domain.fs`: `VitalStatus`, `AgentState.Vitals`/`.RecentlyWounded`,
   `DecisionReason.CriticallyWounded`, `Agent.create`.
2. New `Casualty.fs`.
3. `Appraisal.fs`: stage 2 check, stage 4 penalty.
4. `Simulation.fs`: `combat` (Alive-only shooters/targets, apply wound,
   `AgentIncapacitated`), `stateConsequences` (bleed-out/`AgentDied`,
   leadership diff, squad-failure latch), `appraisal` (wounded trigger),
   `navigationAndMovement` (skip non-Alive).
5. `Events.fs`: four new cases.
6. `Canonical.fs`: bump `FormatVersion`, extend `writeAgent`/`reasonCode`/
   `writeReason`.
7. `Diagnostics.fs` + `DiagnosticRender.fs`: two new overlays, all three
   renderers, `eventNarration`.
8. `Corpus.fs`: new entry/entries; regenerate tables and `.cwreplay` files.
9. New focused tests (see Allowed scope).
10. Verify per Required verification; regenerate every pinned corpus/fixture
    hash affected by the `FormatVersion` bump and confirm the move is
    byte-layout only (tick/event counts unchanged) for every entry except the
    new one(s).
11. Re-pin both Godot client `--selfcheck` hashes
    (`src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`,
    `src/CommandoWar.Client.Godot/README.md`) and reconfirm `MATCH` through
    the real Godot editor headless (the TASK-044 precedent).
12. Update documentation.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] A sequence of qualifying hits against one agent reduces its health,
      transitions it to `Incapacitated` on reaching zero (not directly to
      `Dead`), and it bleeds out to `Dead` after `BleedOutTicks` with no
      player action able to stop it.
- [x] A `Dead`/`Incapacitated` agent never fires, is never chosen as a combat
      target, and never moves, even with a live `Destination`.
- [x] An order addressed to a `Dead`/`Incapacitated` agent appraises
      `Unable(CriticallyWounded)`.
- [x] An `Alive`-but-wounded agent's resolve threshold is measurably lower
      than an unwounded agent's, all else equal, and a wound this tick
      reappraises an already-`Accepted` order the following tick.
- [x] Leadership (lowest-id living friendly) transfers automatically and
      provably on the current leader's death or incapacitation, emitting
      `LeadershipTransferred`.
- [x] `SquadFailure` fires exactly once, the tick every friendly agent
      becomes non-`Alive`, and does not halt `Simulation.step` or write any
      new `WorldState` field.
- [x] `Canonical.FormatVersion` bumped; every pre-existing pinned corpus/
      fixture hash re-pins as a byte-layout change only (unchanged tick and
      event counts), confirmed in the ledger.
- [x] New corpus entry/entries demonstrate wound -> incapacitation ->
      bleed-out -> death, leadership succession, and squad failure end to
      end; `--corpus --regenerate` twice is byte-identical.
- [x] New `content/diagnostics/` golden pair(s) visualise `AgentVitals` and
      `SquadLeadership` (`AGENTS.md` diagnostics rule).
- [x] Both Godot client scenes' `--selfcheck` hashes re-pinned and
      reconfirmed `MATCH` through the real Godot editor.
- [x] `dotnet build` all `.slnx` files (main, Godot client Debug/Release,
      Mibo client) 0/0; `dotnet test` all green.
- [x] Required documentation updated.

## Required verification

- `dotnet test CommandoWar.slnx -c Release`
- `dotnet build CommandoWar.slnx -c Release`,
  `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug` and `-c Release`,
  `dotnet build src/CommandoWar.Client.Mibo/CommandoWar.Client.Mibo.slnx -c Debug`
- `-- corpus` (all entries, `--regenerate` run twice, byte-identical)
- `-- fixture` (format version bump reflected, event/tick counts unchanged
  outside the new entry/entries)
- `-- replay-file content/replays/envelope-full.cwreplay` (round-trip,
  checkpoints OK)
- Godot headless `--selfcheck` for both scenes, re-pinned hashes `MATCH`
- Dependency boundary check: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package` (still `FSharp.Core` only)
- `git status --porcelain`: matches this task's allowed scope

## Evidence to capture

- Full command output and pass/fail counts for every command above.
- The new corpus entry/entries' rendered tables and golden diagnostic pairs.
- The `FormatVersion` bump's exact before/after hash list for at least one
  representative pre-existing entry, showing tick/event counts unchanged.

## Expected files

See Allowed scope.

## Documentation updates

- This task file's Outcome/Review sections.
- `docs/11_BACKLOG.md` B-031 row.
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file;
  Pinned facts `Canonical.FormatVersion` and Green tests).
- `content/diagnostics/README.md`.
- `src/CommandoWar.Client.Godot/README.md` (re-pinned `--selfcheck` hashes).
- `PROJECT_STATE.yaml`.

## Rollback or removal

Additive to `WorldState`/`AgentState` (a new field, a new type, a new leaf
module) plus small, well-isolated extensions to four existing phase
functions and `Appraisal.appraise`. No change to `Simulation.step`'s public
signature, `Commitment.fs`, or `Combat.chooseTarget`/`.hitChance`'s own
geometry. Revertible with `git revert` in one step; the
`Canonical.FormatVersion` bump is the only irreversible-in-place consequence
(already true of every prior canonical change, e.g. TASK-032/033/037/044).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-17, "seems to work"). One follow-up raised, not
  blocking: without the developer overlay (`F1`) toggled on, there is no
  player-facing visual indicator that a shot was fired or that an agent is
  wounded — `FireLine`/`AgentVitals` currently only render inside
  `CommandDemoScene`'s dev-overlay branch. Recorded as new backlog row B-057,
  not implemented here (out of this task's own scope, which was Sim-side
  only per its Forbidden scope).
