# Replay corpus

Status: committed, regenerable determinism evidence (TASK-016, backlog B-012)

A small set of named replay entries, each a command log plus a committed
per-tick authoritative-state-hash table. `cwheadless corpus` and the in-suite
`CorpusTests.fs` `[<Theory>]` both replay every entry from its initial state and
compare the fresh per-tick hashes to the committed table. A mismatch is a
determinism regression: it fails the build instead of rotting silently.

This corpus adds no authoritative spatial or tactical state of its own — it is
evidence infrastructure over the existing `Simulation` / `Replay` / `Divergence`
modules — so it does not extend the diagnostic frame
(`docs/09_TEST_STRATEGY.md` section 8).

**Environment (TASK-023):** the hashes below are pinned on Windows x64 / .NET
SDK `10.0.303`, and `.github/workflows/ci.yml` re-verifies them there on every
push and pull request. Other environments are outside the current
per-environment determinism contract (`docs/09_TEST_STRATEGY.md` section 3);
strengthening that is an ADR-gated decision.

## Entries

Each entry is `<name>.cwlog` (existing command-log format v1,
`src/CommandoWar.Headless/CommandLogFile.fs`) and `<name>.md` (the committed
hash table: initial state, tick count, initial/final hash, domain-event count,
and the tick -> hash column). Initial states are defined in
`src/CommandoWar.Headless/Corpus.fs` (`Corpus.all`); none is authoritative game
content and none needs an on-disk format (backlog B-024).

| Entry | Shows |
|---|---|
| `spike-fixture` | The framework-spike shared fixture (`Fixture.initialState ()`, 32 x 32, seed 20260902): agent 3 ordered to (20,14). The same 40-value sequence as `content/fixtures/SPIKE-FIXTURE.md`; `CorpusTests.fs` cross-checks it against `Fixture.run ()` so it is not an independent re-pin. |
| `wall-detour` | A single agent detouring around an impassable wall (`Pathfinding.findWithin`, `docs/04` section 8 steps 2, 4, 5): TASK-015's replan branch structure without needing to mutate terrain mid-run. |
| `blocked-goal` | A single agent whose target is unreachable: the Appraisal phase (12.5, TASK-028) runs `Pathfinding.findWithin` at stage 2, gets `NoPath`, and refuses the order `Unable(NoKnownRoute)` — no `Destination` is written and the agent never moves. `docs/09` section 2.2 "appraisal never returns `Accepted` after a hard feasibility failure"; `docs/05` section 16 "Physical inability". |
| `converging-routes` | Two agents whose routes cross the same cell on the same tick: `Simulation.navigationAndMovement`'s same-tick reservation resolves the contest (TASK-017), the lower-remaining-route agent enters the cell, and the other yields one tick before catching up. |
| `slow-terrain` | A single agent crossing one cell whose `Terrain.moveCost` (3) exceeds `Terrain.BaseMoveCost` (1): `AgentState.Progress` accumulates over two ticks before the agent enters the cell on the third (TASK-018 sub-cell movement progress); every other cell is entered in the usual single tick. |
| `follow-chain` | Three agents in a line all ordered the same way, the lead with a free cell ahead: TASK-022's vacation-chain resolution advances the whole chain on the same tick, every tick, with no `MovementObstructed`. |
| `swap-standoff` | Two agents each ordered onto the other's cell: a two-agent position swap is blocked (TASK-022), neither is ever a first mover, both emit `MovementObstructed` every tick and neither leaves its start cell. |
| `perception-contact` | **The first entry with an enemy deployment** (TASK-026). One friendly at (1,5) ordered east to (9,5); one stationary hostile at (9,1) behind an opaque impassable wall at x=6, rows 0..3. The hostile is inside `PerceptionConfig.SightRange` from the start, but line of sight is blocked until the friendly clears the wall at tick 5 — then the Perception phase emits `ContactObserved` and the Tactical-knowledge phase adds the contact to `WorldState.TacticalKnowledge`. The "Unknown threat" shape (`docs/05` section 16). |
| `lost-comms` | **The first entry exercising communication failure** (TASK-027, backlog B-016). One friendly agent 0 at (1,4) with `CommunicationAvailable = false` (an authored comms blackout) ordered east to (6,4). Command intake accepts the order (`CommandAccepted`), the Communication phase (12.2) cannot reach the recipient and emits `OrderUndelivered` (`UnableToCommunicate`), and the order is dropped: no `Destination` is written and the agent never moves. The "Lost communication" scenario (`docs/05` section 16). Because the order is dropped, no `AgentState.Order` is written and the Appraisal phase never runs on it — this is the one entry with no `OrderAppraised` event. |
| `exposed-approach` | **The G3 evidence scenario** (TASK-028, backlog B-017; `docs/07` section 9 criterion 2). Two friendlies on open ground ordered along the same exposed approach past a stationary hostile the squad sees from the start: agent 0 (`Discipline 1`) at (1,3) → (11,3), agent 1 (`Discipline 6`) at (1,5) → (11,5). Both routes run the same distance past the known threat at (10,4), so the exposure is near-identical — the divergence is discipline alone: on tick 1 the Appraisal phase `Refuses` agent 0's order (`RouteTooExposed`, no `Destination`) and `Accepts` agent 1's. Two agents appraise the same intent differently for inspectable reasons. |
| `reissued-order` | **The first entry exercising commitment supersession** (TASK-030, backlog B-018). One friendly at (1,4), open ground, no threats: ordered east to (14,4) on tick 1 (`Accepted`, `CommitmentEstablished`), then re-ordered south to (14,8) on tick 3 while still mid-route toward the first target. The second order supersedes the first — Appraisal re-accepts against the new target and `commitmentAndLocalAction` emits a fresh `CommitmentEstablished` for the second command, with no event reporting the first commitment's end. `docs/07` section 8 step 7, "the player reissues the original intent". |
| `open-engagement` | **The first entry exercising real combat** (TASK-031, backlog B-019). A friendly at (2,2) and a hostile at (7,2), open ground, no orders on either side: both are within `CombatConfig.WeaponRange` and clear line of sight from tick 1, so the Combat phase alone drives the trace — a deterministic hitscan shot (range + directional-cover-mitigated hit chance) every tick, symmetric both ways, the deterministic stream's first real gameplay draw. |

**Re-pinned by TASK-031** (Combat phase realised, backlog B-019): `perception-contact`
and `exposed-approach` each legitimately bring an agent within
`CombatConfig.WeaponRange` and line of sight of the opposing side partway
through their run (once the friendly clears the wall in `perception-contact`;
once agent 1 walks past the known hostile in `exposed-approach`), so real
`ShotFired` events and new per-tick hashes appear from that tick onward —
**tick counts are unchanged on both**, and every tick before the first
engagement is byte-identical to its pre-TASK-031 hash (`exposed-approach`
tick 1 in particular, `0xB03F8419E55F3592`, is unaffected — the TASK-029
Godot demo's pinned value still holds). No other existing entry has an
engageable pair; the `demo.html` diagnostic golden (not a corpus entry)
similarly gains combat from tick 8 onward. `Canonical.FormatVersion` does not
move — `WorldState.Random` was already canonical (TASK-003); only the values
a draw produces are new, not what is hashed.

## Production replay-command format (TASK-025, backlog B-045)

`envelope-full.cwreplay` is the first committed replay in the **production
replay-command format** (`src/CommandoWar.Sim/ReplaySerialisation.fs`,
replay-command file format v1) — a versioned, lossless, line-based text format
that carries the whole accepted-command envelope the legacy `.cwlog` cannot:
multi-recipient addressing, `Urgency`, `RiskTolerance`, and an `IssuedAtTick`
distinct from the delivery tick (TASK-024). Its initial state is a **named
scenario reference** (`spike-fixture`, resolved from `Corpus.all`), not a
serialised `WorldState`.

| Entry | Shows |
|---|---|
| `envelope-full` | A three-recipient `MoveTo` (agents 3, 4, 5), `Urgency = Immediate`, `RiskTolerance = Aggressive`, issued on tick 1 and delivered on tick 2, over the spike-fixture initial state (24 ticks). The envelope `.cwlog` cannot express. |

**Re-pinned by TASK-028** (`Canonical.FormatVersion` 3 -> 4, `AgentState.Order`
/ `AgentState.Disposition` sections added to `Canonical.encode`):
`envelope-full.cwreplay`'s `canonical` line, `initial-hash`, and 24
`checkpoint` hashes, and `envelope-full.md`, all moved. The spike-fixture
initial state is enemy-free, so all three recipients `Accept` at appraisal: the
command and the 24 ticks are unchanged, and the only behaviour change is one
`OrderAppraised` event per recipient (72 -> 75 domain events).
`ReplaySerialisation.parse` rejects a `canonical` value that does not match the
build, so the data file had to move even though no serialisation code changed.
(Previously re-pinned by TASK-026 for the 2 -> 3 tactical-knowledge bump.)

`envelope-full.cwreplay` carries its own per-tick `checkpoint` hashes;
`envelope-full.md` is the same table in the shape above.
`tests/CommandoWar.Sim.Tests/ReplayTests.fs` cross-checks the file's
checkpoints, the `.md` table, a pinned hash array, and a fresh `Replay.run`
against one another. It is **not** in `Corpus.all` (that path is `.cwlog` + a
generated `.md` only), so `cwheadless corpus` does not touch it; run it with:

```sh
dotnet run --project src/CommandoWar.Headless -c Release -- replay-file content/replays/envelope-full.cwreplay
```

which prints the per-tick hash table and the ordered accepted commands and
exits `2` on a parse/validate failure, `3` on a checkpoint divergence. The
seven `.cwlog` entries are unaffected; migrating them to the new format is
backlog B-049.

## Regeneration

From the repository root, after `dotnet build CommandoWar.slnx -c Release`:

```sh
dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate
```

This rewrites every `<name>.md` from a fresh replay. Regeneration is
idempotent: running it twice with no other change leaves every file
byte-identical (`git status` shows nothing).

## Checking

```sh
dotnet run --project src/CommandoWar.Headless -c Release -- corpus
```

Replays every entry, checks it reproduces its own hashes on a second
independent run (a genuine-nondeterminism guard), then compares its per-tick
hashes to the committed table. Prints `PASS` per entry and exits `0` on a full
match; on the first divergence it prints the entry name, the first bad tick,
the expected (committed) and actual (this build) hash, and exits non-zero (see
`cwheadless help` for exit codes). `dotnet test` runs the same check as a
`[<Theory>]` in `tests/CommandoWar.Sim.Tests/CorpusTests.fs` so a regression
fails `dotnet test` without an opt-in CLI run.

## Format notes

`<name>.md` is fully generated (`Corpus.renderTable`); do not hand-edit it —
regenerate it instead. The parser (`Corpus.parseTable`) reads only: the "Tick
count", "Initial hash", "Final hash", and "Domain events" parameter rows, and
the per-tick table rows (`| <tick> | \`0x...\` |`). Everything else in the file
is prose for a human reader.
