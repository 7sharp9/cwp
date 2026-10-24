# Replay corpus

Status: committed, regenerable determinism evidence (TASK-016, backlog B-012)

A small set of named replay entries, each a command log plus a committed
per-tick authoritative-state-hash table. `cwheadless corpus` and the in-suite
`CorpusTests.fs` `[<Theory>]` both replay every entry from its initial state and
compare the fresh per-tick hashes to the committed table. A mismatch is a
determinism regression: it fails the build instead of rotting silently.

The corpus holds no authoritative spatial or tactical state of its own. It is
evidence infrastructure over the existing `Simulation` / `Replay` / `Divergence`
modules, so it does not extend the diagnostic frame
(`docs/09_TEST_STRATEGY.md` section 8).

**Environment (TASK-023):** the hashes below are pinned on Windows x64 / .NET
SDK `10.0.303`, and `.github/workflows/ci.yml` re-verifies them there on every
push and on pull requests to `main`. Other environments are outside the current
per-environment determinism contract (`docs/09_TEST_STRATEGY.md` section 3);
strengthening that is an ADR-gated decision.

## Entries

Each entry is `<name>.md` (the committed hash table: initial state, tick
count, initial/final hash, domain-event count, and the tick -> hash column)
plus a committed command file. Every entry but `spike-fixture` carries its
accepted commands as F# values in `src/CommandoWar.Headless/Corpus.fs`
(`Entry.Commands`), which are the sole runtime source of truth. The committed
`<name>.cwreplay` (`src/CommandoWar.Sim/ReplaySerialisation.fs`, replay-command
format v1) is generated from them, human-reviewable, and never read back, so it
cannot drift from the geometry the way a hand-typed command file could
(TASK-036, backlog B-049). Most of these entries author geometry and command
schedule together as one `ScenarioSpec` value. The exceptions are
`order-queue-stacking-and-cancellation` (a `ScenarioSpec` for the world, but its
commands are built directly because the `ScenarioSpec` order vocabulary has no
queue mode or cancel), `demolition-success` (a hand-built `RawScenario`, because
the shared builder always authors exactly one non-optional `reach` objective)
and the two `bridgehead-*` entries (real mission content, below).

`spike-fixture` alone reads its commands from a hand-authored
`spike-fixture.cwlog` (the legacy command-log format v1,
`src/CommandoWar.Headless/CommandLogFile.fs`); it is the framework-spike shared
fixture, not a `ScenarioSpec`. Initial states are defined in `Corpus.fs`
(`Corpus.all`, 22 entries in a fixed order); none is authoritative game
content and none needs an on-disk format (backlog B-024), except the two
`bridgehead-*` entries (backlog B-077): their initial state is the real
`content/scenarios/bridgehead.cwscenario`, embedded into the `cwheadless`
assembly at build time, so **any Bridgehead content edit re-runs
them and must regenerate them** (and re-check that the recorded
`Succeeded`/`Failed` stories still hold, not only re-pin the numbers).

The table lists the entries in `Corpus.all` order.

| Entry | Shows |
|---|---|
| `spike-fixture` | The framework-spike shared fixture (`Fixture.initialState ()`, 32 x 32, seed 20260902): agent 3 ordered to (20,14), 40 ticks. The same 40-value sequence as `content/fixtures/SPIKE-FIXTURE.md`; `CorpusTests.fs` cross-checks it against `Fixture.run ()`, so it is not an independent pin. |
| `wall-detour` | A single agent detouring around an impassable wall (`Pathfinding.findWithin`, `docs/04` section 8 steps 2, 4, 5): the replan branch structure without mutating terrain mid-run. |
| `blocked-goal` | A single agent whose target is unreachable. The Appraisal phase (12.5, TASK-028) runs `Pathfinding.findWithin` at stage 2, gets `NoPath`, and refuses the order `Unable(NoKnownRoute)`: no `Destination` is written and the agent never moves. `docs/09` section 2.2 "appraisal never returns `Accepted` after a hard feasibility failure"; `docs/05` section 16 "Physical inability". |
| `converging-routes` | Two agents whose routes cross the same cell on the same tick. `Simulation.navigationAndMovement`'s same-tick reservation resolves the contest (TASK-017): the agent with the shorter remaining route enters the cell (ties go to the lower agent id, and the two routes tie here, so agent 0 enters), and the other yields one tick before catching up. |
| `slow-terrain` | A single agent crossing one cell whose `Terrain.moveCost` (3) exceeds `Terrain.BaseMoveCost` (1). `AgentState.Progress` accumulates over two ticks before the agent enters the cell on the third (sub-cell movement progress, TASK-018); every other cell is entered in the usual single tick. |
| `follow-chain` | Three agents in a line all ordered the same way, the lead with a free cell ahead. The vacation-chain resolution (TASK-022) advances the whole chain on the same tick, every tick, with no `MovementObstructed`. |
| `swap-standoff` | Two agents each ordered onto the other's cell. A two-agent position swap is blocked: neither is ever a first mover, both emit `MovementObstructed` every tick and neither leaves its start cell. |
| `perception-contact` | Enemy deployment and line of sight (TASK-026). One friendly at (1,5) ordered east to (9,5); one stationary hostile at (9,1) behind an opaque impassable wall at x=6, rows 0..3. The hostile is inside `PerceptionConfig.SightRange` from the start, but line of sight is blocked until the friendly clears the wall at tick 5. Then the Perception phase emits `ContactObserved` and the Tactical-knowledge phase adds the contact to `WorldState.TacticalKnowledge` (the "Unknown threat" shape, `docs/05` section 16). The same gap brings both agents into `CombatConfig.WeaponRange`, so they trade fire from that tick: the friendly is incapacitated by tick 8 and, as the squad's only agent, leadership transfers to `None` (`LeadershipTransferred`). The order is issued on tick 1, before the contact is known, so it is `Accepted` at appraisal; the knowledge change when the contact appears reappraises it (`OrderAppraised` on tick 5, and again on ticks 6 and 8, the ticks after the friendly is wounded), and each time it is `Accepted`. |
| `lost-comms` | Communication failure (TASK-027, backlog B-016). One friendly agent 0 at (1,4) with `CommunicationAvailable = false` (an authored comms blackout) ordered east to (6,4). Command intake accepts the order (`CommandAccepted`), the Communication phase (12.2) cannot reach the recipient and emits `OrderUndelivered` (`UnableToCommunicate`), and the order is dropped: no `Destination` is written and the agent never moves. The "Lost communication" scenario (`docs/05` section 16). Because the order is dropped, no `AgentState.Order` is written and the Appraisal phase never runs on it, so this is the only entry that accepts an order and never appraises it (no `OrderAppraised` event). |
| `exposed-approach` | The G3 evidence scenario (backlog B-017; `docs/07` section 9 criterion 2). Two friendlies on open ground ordered along the same exposed approach past a stationary hostile the squad sees from the start: agent 0 (`Discipline 1`) at (1,3) -> (11,3), agent 1 (`Discipline 6`) at (1,5) -> (11,5). Both routes run the same distance past the known threat at (10,4), so the exposure is near-identical and the divergence is discipline alone: on tick 1 the Appraisal phase `Refuses` agent 0's order (`RouteTooExposed`, no `Destination`) and `Accepts` agent 1's. Two agents appraise the same intent differently for inspectable reasons. The tick-1 divergence happens before any shot is fired. The run continues: agent 1 is refused again on tick 3 once the hostile has it in range and its route would close on it, and stops at (3,5) (a `MoveTo` does not advance on a threat already firing on the agent, backlog B-082); on tick 5 the hostile's suppression band latches, both orders are reappraised `Accepted`, agent 1 steps once and is incapacitated, the hostile follows on tick 7, and agent 0 then walks the approach. |
| `suppress-relieves-exposure` | The `exposed-approach` geometry for agent 0 (`Discipline 1`, (1,3) -> (11,3), refused `RouteTooExposed` on tick 1 against the hostile at (10,4)), plus a second friendly at (10,1) given a `Suppress` order against that hostile on the same tick (TASK-037, backlog B-030). The `Suppressing` commitment keeps the hostile under fire every tick; by tick 6 even in the worst case (every shot a miss) its `SuppressionBand` latches, the threat-suppression-change reappraisal trigger fires, `Appraisal.routeExposure` zeroes the hostile's contribution, and agent 0's order reappraises `Accepted` and starts walking (in the committed run this happens on tick 4). `docs/07` section 8 steps 5-6 and section 9 criterion 4 ("a player action can predictably change an appraisal outcome"). The two close-range combatants take real wounds; that does not change the latch or the reappraisal. |
| `canonical-refusal-and-correction` | The full `docs/07` section 8 sequence in one run (TASK-038, backlog B-023; G3's headline evidence item): the `suppress-relieves-exposure` setup plus a reissue of agent 0's original (11,3) order on tick 8, once it is already mid-route under the automatic reappraisal. Steps 1-3: agent 0 is `Refused RouteTooExposed` at tick 1. Step 4: the refusal's `DecisionReason` is a named, structured reason, never a raw score. Steps 5-6: the suppress order latches the hostile's `SuppressionBand` and agent 0's order reappraises `Accepted` at tick 4. Step 7: the player reissues the identical intent at tick 8. Step 8: it is `Accepted` again, with a fresh `CommitmentEstablished` and no event for the superseded commitment. The `Adapted` (stage-5) half of "accepts or adapts" is out of scope; no such disposition exists. Agent 1 is incapacitated by tick 4 (its own `Suppressing` order then appraises `Unable(CriticallyWounded)`) and the hostile follows at tick 5; agent 0 stays alive (the one hit it takes, on tick 5, leaves it at 650/1000 health) and arrives at tick 13. |
| `reissued-order` | Commitment supersession (TASK-030, backlog B-018). One friendly at (1,4), open ground, no threats: ordered east to (14,4) on tick 1 (`Accepted`, `CommitmentEstablished`), then re-ordered south to (14,8) on tick 3 while still mid-route toward the first target. The second order supersedes the first: Appraisal re-accepts against the new target and `commitmentAndLocalAction` emits a fresh `CommitmentEstablished` for the second command, with no event reporting the first commitment's end. `docs/07` section 8 step 7, "the player reissues the original intent". |
| `open-engagement` | Real combat (TASK-031, backlog B-019). A friendly at (2,2) and a hostile at (7,2), open ground, no orders on either side: both are within `CombatConfig.WeaponRange` and clear line of sight from tick 1, so the Combat phase alone drives the trace. A deterministic hitscan shot (range + directional-cover-mitigated hit chance) fires every tick, symmetric both ways, drawing on the deterministic stream. |
| `order-queue-stacking-and-cancellation` | Order queueing and cancellation (TASK-044, backlog B-051). One friendly at (0,0) is issued three stacked `MoveTo` waypoints on tick 1: (3,0) `Replace` (active), then (7,0) and (10,0) both `Append` (queued behind it). Tick 2 cancels the still-queued third waypoint by `CommandId` (`OrderCancelled`, `wasActive=false`) before it activates. The first leg arrives on tick 3; on tick 4 `commitmentAndLocalAction` recognises the fulfilled order and promotes the queue head into `Order`, un-appraised until tick 5's Appraisal judges it `Accepted` and movement resumes (a one-tick promotion gap, distinct from a same-tick delivered order). Tick 7 cancels the now-active second waypoint mid-route (`wasActive=true`): with an empty queue behind it the agent goes `Holding` and its `Destination` is cleared, so it stops at (5,0) instead of drifting on with a stale target. |
| `casualties-succession-and-squad-failure` | Casualties, leadership succession and squad failure (TASK-045, backlog B-031). Two friendly-hostile pairs, no orders (combat alone drives the trace): friendly 0 (0,0) vs hostile 2 (0,3), friendly 1 (5,0) vs hostile 3 (5,3), each pair 3 apart (inside weapon range) and 5 from the other pair, so `Combat.chooseTarget`'s nearest-candidate rule pairs each friendly with its own hostile. Wounds accumulate over repeated hits (`Casualty.wound`, no cover). Friendly 0 is incapacitated by tick 4; as the derived squad leader (the lowest-id living friendly, a pure rule with no stored field, `docs/05` section 17's "simple replacement rule") it hands leadership to friendly 1 the same tick (`LeadershipTransferred`). Friendly 1 is incapacitated by tick 5: leadership transfers to `None` and `SquadFailure` fires, a signal event only that does not halt the run. Each incapacitated agent's bleed-out (`CasualtyConfig.BleedOutTicks` = 60, no rescue mechanic) then counts down unopposed: all four agents are `Dead` by tick 64, with `AgentDied` for each. |
| `formation-slots` | A formation (TASK-059, backlog B-011d). Two friendly agents in a two-slot "wedge" formation (offsets `(-1,0)`/`(1,0)`), both addressed by ONE joint `MoveTo` order (`Command.moveToMany`, TASK-067, backlog B-067) to the nominal cell `(5,5)` on tick 1. Each resolves its own real destination as that shared anchor plus its own authored slot offset (`Appraisal.resolveFormationTarget`), landing on `(4,5)` and `(6,5)` instead of colliding on `(5,5)`. A solo order does not redirect through formation, whatever the recipient's own offset. |
| `demolition-success` | Real `destroy`/`extract` objectives (TASK-062, backlog B-032). One friendly agent at (0,0): `MoveTo (3,0)` (tick 1) reaches a static target at tick 3, and a 2-tick occupancy plant completes `DestroyTarget` at tick 4; `MoveTo (10,0)` (tick 8) reaches the extraction area at tick 14, completing `ExtractAgents` and reaching `WorldState.MissionOutcome = Succeeded` the same tick. This is `Simulation.mission`'s end-to-end corpus proof. |
| `stalled-order-abandoned` | A visible stall failure (TASK-065, backlog B-065; `docs/10` R-010, signal "deadlocks"). One friendly agent at (0,0) ordered east to (4,0); a second friendly sits idle, permanently, on the only route at (2,0), with row `y = 1` walled off the full map width (backlog B-069) so no detour exists. This is `swap-standoff`'s single-sided case, but permanent rather than mutual, and undetourable. Agent 0 advances one cell then freezes (`MovementObstructed`) every tick against the stationary occupant. The run lasts 42 ticks, long enough to reach `Simulation.StallAbandonTicks` (40): at tick 41 the order is abandoned outright (`MovementAbandoned`) instead of freezing forever, `Destination`/`Route` clear and `AgentState.StalledTicks` resets to 0. |
| `chokepoint-detour` | A successful detour around a parked agent (backlog B-069; `docs/10` R-010, the live-agent chokepoint jam). The same setup as `stalled-order-abandoned`, but on open terrain (no wall): a genuine alternate route around the idle blocker at (2,0) exists, so `Simulation.navigationAndMovement` finds and adopts it (`MovementRerouted`, a locally patched `Terrain` passed into the unmodified `Pathfinding.findWithin`, whose contract does not change) instead of stalling toward abandonment. Agent 0 reaches (4,0) for real by tick 7 of the 10-tick run. |
| `bridgehead-succeeded` | Real mission content (TASK-077, backlog B-077; G4 "replay of a completed mission reproduces its authoritative result"). `content/scenarios/bridgehead.cwscenario`, seed 20260920 (`CommandDemoScene`'s own seed), played to `MissionOutcome = Succeeded` at tick 215 (217-tick run) with 15 `MoveTo` orders and nothing else. Tick 6: agent 5, halted at the ford stand-off cell `(5,9)`, is ordered on to `(12,9)` and `Refused(RouteTooExposed(Some 102))` before any shot (`docs/07` criterion 3); at tick 83, once rifleman 102 is down, the same standing order is reappraised `Accepted` (criterion 4). The machine gun falls to the bridge push (incapacitated tick 12), riflemen 101/102 to a push onto `(10,5)`/`(10,6)`/`(11,6)` at the cost of agents 0 and 4, the charge is planted ticks 151-160, and the four survivors extract through both extraction cells: an extracted agent leaves play and holds no cell (backlog B-083), so the second pair walks onto the cells the first pair extracted on and agent 5, ordered to `(1,9)`, is extracted at `(4,9)` on the way. A downed agent perceives nothing (backlog B-078), so corpses add no `ContactObserved` events; from tick 139 agent 5's closing push to `(12,9)` is held for three ticks on a stale contact of the downed rifleman 102, then resumed (backlog B-082). |
| `bridgehead-failed` | The same content and seed played to `MissionOutcome = Failed` at tick 84 (86-tick run): one six-agent frontal charge at tick 1. Several orders are refused mid-route once threats become known, but those agents are already inside weapon range; agent 5 is held at `(7,9)` from tick 28 instead of walking on (backlog B-082). The extraction objective (id 3) stays incomplete at `Failed`: `ExtractAgents` needs at least one required agent extracted (backlog B-079), and none was. |

## Production replay-command format (TASK-025, backlog B-045)

`envelope-full.cwreplay` is the committed replay that exercises the **production
replay-command format** (`src/CommandoWar.Sim/ReplaySerialisation.fs`,
replay-command file format v1): a versioned, lossless, line-based text format
that carries the whole accepted-command envelope the legacy `.cwlog` cannot:
multi-recipient addressing, `Urgency`, `RiskTolerance`, and an `IssuedAtTick`
distinct from the delivery tick (TASK-024). Its initial state is a **named
scenario reference** (`spike-fixture`, resolved from `Corpus.all`), not a
serialised `WorldState`.

| Entry | Shows |
|---|---|
| `envelope-full` | A three-recipient `MoveTo` (agents 3, 4, 5), `Urgency = Immediate`, `RiskTolerance = Aggressive`, issued on tick 1 and delivered on tick 2, over the spike-fixture initial state (24 ticks, 78 domain events). The envelope `.cwlog` cannot express. The spike-fixture initial state is enemy-free and has no formation, so all three recipients `Accept` and `AsGroup` does not change the resolved target. |

`envelope-full.cwreplay` carries its own per-tick `checkpoint` hashes;
`envelope-full.md` is the same table in the shape above.
`tests/CommandoWar.Sim.Tests/ReplayTests.fs` cross-checks the file's
checkpoints, the `.md` table, a pinned hash array, and a fresh `Replay.run`
against one another. It is **not** in `Corpus.all` (a corpus entry's
`.cwreplay` carries no checkpoints of its own: `<name>.md` is the one committed
hash-table artefact there), so `cwheadless corpus` and `corpus --regenerate` do
not touch it. Run it with:

```sh
dotnet run --project src/CommandoWar.Headless -c Release -- replay-file content/replays/envelope-full.cwreplay
```

which prints the per-tick hash table and the ordered accepted commands and
exits `2` on a parse/validate failure, `3` on a checkpoint divergence.
`ReplaySerialisation.parse` rejects a `canonical` header value that does not
match the build's `Canonical.FormatVersion`.

The 21 `Corpus.all` entries other than `spike-fixture` use the same format for
their `.cwreplay` files, with empty `checkpoints` and `initial-hash`, since
`<name>.md` already owns that role; `spike-fixture` alone uses the legacy
`.cwlog` grammar.

## Regeneration

From the repository root, after `dotnet build CommandoWar.slnx -c Release`:

```sh
dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate
```

This rewrites every `<name>.md` from a fresh replay and, for every entry with
F#-authored commands, its `<name>.cwreplay` from the same commands.
Regeneration is idempotent: running it twice with no other change leaves every
file byte-identical (`git status` shows nothing).

## Checking

```sh
dotnet run --project src/CommandoWar.Headless -c Release -- corpus
```

Replays every entry, checks it reproduces its own hashes on a second
independent run (a genuine-nondeterminism guard), then compares its initial
hash, per-tick hashes, tick count and event count to the committed table.
Prints `PASS` per entry and `OK - all 22 entries match their committed
tables`, and exits `0` on a full match; on a divergence it prints the entry name, the first bad tick, the
expected (committed) and actual (this build) hash, and exits non-zero (see
`cwheadless help` for exit codes). `dotnet test` runs the same check as a
`[<Theory>]` in `tests/CommandoWar.Sim.Tests/CorpusTests.fs`, so a regression
fails `dotnet test` without an opt-in CLI run.

## Format notes

`<name>.md` is fully generated (`Corpus.renderTable`); do not hand-edit it;
regenerate it instead. The parser (`Corpus.parseTable`) reads only: the "Tick
count", "Initial hash", "Final hash", and "Domain events" parameter rows, and
the per-tick table rows (`| <tick> | \`0x...\` |`). Everything else in the file
is prose for a human reader. `<name>.cwreplay` is likewise fully generated
(from the entry's authored commands) and never read back; do not hand-edit it
either.

## Re-pin policy

A `Canonical.FormatVersion` bump, a behaviour change in a phase and an edit to
the Bridgehead content all move hashes (the format version is the first field of
the hashed bytes, so a bump moves every hash regardless of behaviour). `corpus`
and `CorpusTests` fail on any difference in an entry's initial hash, per-tick
hashes, tick count, run length or event count, so such a change cannot pass
unnoticed. Update the committed files only with `corpus --regenerate`, never by
hand. The check reports the first divergent tick of each entry. A change that
only alters the byte layout moves hashes but leaves every tick count and event
count unchanged; a changed count means behaviour moved. As each generated
`<name>.md` states, a change to its numbers that nothing explains is a
determinism regression, not a re-pin.

`envelope-full` is outside `Corpus.all` and is not regenerated: update its
`.cwreplay` (`canonical` line, `initial-hash`, every `checkpoint`), its `.md`,
and the pinned hash array in `ReplayTests.fs` together. `CorpusTests.fs` also
asserts the `spike-fixture` initial hash, final hash and event count, and checks
its table against `Fixture.run ()`. The Godot appraisal demo
(`src/CommandoWar.Client.Godot/src/AppraisalDemoScene.cs`, `--selfcheck`) pins
the `exposed-approach` tick-1 hash and must be updated whenever that hash moves.

Pinned values, current at `Canonical.FormatVersion` 15 (replay-command format
v1, command-log format v1):

- `exposed-approach` tick 1: `0xA1354EB998FC1B95`
- `envelope-full` initial hash `0x5049F6F0E9FCA1E2`, tick-1 hash
  `0x38F956EF2922EFB1`
- seeds: 20260902 (`spike-fixture`, `envelope-full`), 20260904 (every other
  entry except `bridgehead-*`), 20260920 (`bridgehead-*`)

Every other hash is in its entry's committed `<name>.md`. The per-task re-pin
history (which task moved which entries, and why) is in
`docs/12_PROGRESS_LEDGER.md` and `docs/ledger/`.
