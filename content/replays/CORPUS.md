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
