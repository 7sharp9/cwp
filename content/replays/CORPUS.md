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
| `blocked-goal` | A single agent whose target is unreachable: `Pathfinding` returns `NoPath`, the executor emits `MovementBlocked`, and the destination is cleared (no retry). |
| `converging-routes` | Two agents whose routes cross the same cell on the same tick: `Simulation.navigationAndMovement`'s same-tick reservation resolves the contest (TASK-017), the lower-remaining-route agent enters the cell, and the other yields one tick before catching up. |
| `slow-terrain` | A single agent crossing one cell whose `Terrain.moveCost` (3) exceeds `Terrain.BaseMoveCost` (1): `AgentState.Progress` accumulates over two ticks before the agent enters the cell on the third (TASK-018 sub-cell movement progress); every other cell is entered in the usual single tick. |
| `follow-chain` | Three agents in a line all ordered the same way, the lead with a free cell ahead: TASK-022's vacation-chain resolution advances the whole chain on the same tick, every tick, with no `MovementObstructed`. |
| `swap-standoff` | Two agents each ordered onto the other's cell: a two-agent position swap is blocked (TASK-022), neither is ever a first mover, both emit `MovementObstructed` every tick and neither leaves its start cell. |

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
