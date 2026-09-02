# TASK-003: Establish Determinism, Hash, and Replay Harness

Status: done  
Owner: Dave  
Phase: P1/P2  
Gate: G1 prerequisite and G2 foundation  
Size: M

## Objective

Provide enough deterministic infrastructure to prove that both framework spikes advance the same authoritative simulation and to localise divergence.

## Why this task exists

A fixed timestep alone does not make a simulation deterministic. The framework decision needs observable tick and state identity, while later AI work needs reproducible failures.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `decisions/ADR-0002-SIMULATION-BOUNDARY.md`
- `docs/04_SIMULATION_SPEC.md`
- `docs/09_TEST_STRATEGY.md`
- completed TASK-002 implementation and tests

## Dependencies

- TASK-002 accepted and `done`

## Allowed scope

- project-owned deterministic random interface and one specified implementation;
- random golden vectors;
- canonical authoritative state representation for hashing;
- versioned command recording sufficient for the spike;
- replay runner over the simulation;
- first-divergence diagnostic for a small state;
- tests and a small command-line or test harness;
- benchmark baseline for empty and six-agent stepping if low effort.

## Forbidden scope

- cross-platform lockstep claim;
- networking;
- arbitrary backward-compatible save migrations;
- framework random or serialization as authority;
- cryptographic security claims;
- gameplay randomness not needed to test the random source;
- graphical hosts.

## Required work

1. Inspect authoritative collections and eliminate order-dependent ambiguity.
2. Define the initial determinism contract exactly as in the test strategy unless implementation evidence requires a narrower documented contract.
3. Implement a small project-owned PRNG with explicit state and documented algorithm.
4. Add golden-vector tests independent of game behaviour.
5. Define canonical state ordering and hashing. Hash only authoritative state and version the canonical format.
6. Define a minimal versioned command record with tick, sequence, command, and required metadata.
7. Implement replay from initial state, command log, and random state or seed.
8. Produce a useful mismatch report containing at least the first divergent tick and expected/actual hash.
9. Add deterministic tests covering repeated direct runs and replay.
10. Verify that no wall clock, `System.Random`, hash enumeration, or host type affects state.

## Acceptance criteria

- [x] PRNG golden vectors pass and identify the algorithm/version.
      `RandomTests`: `SplitMix64 identifies its algorithm and version`,
      `SplitMix64 reproduces the published seed-0 golden vector` (verified against
      the public SplitMix64(0) sequence), `SplitMix64 golden vector for a
      non-zero seed`.
- [x] Repeating the same initial state and command stream yields the same
      per-tick or final hashes.
      `ReplayTests`: `repeated direct runs of the same inputs yield identical
      per-tick hashes`.
- [x] Replay yields the same authoritative result as direct stepping.
      `ReplayTests`: `replay reconstructs the same per-tick hashes, final state
      and events as direct stepping`.
- [x] Changing one command yields a reported divergence at or after the changed
      command tick.
      `ReplayTests`: `a mutated command destination is reported as a divergence
      at the changed tick` (tick 1), `an added command diverges at that
      command's tick and not before` (tick 3).
- [x] Canonical state ordering is explicit and covered by tests.
      `Canonical.encode` sorts agents by ascending id explicitly.
      `CanonicalHashTests`: `canonical encoding is independent of the order
      agents are supplied in`, `the canonical format version is the first
      field ...`.
- [x] Command and replay formats are versioned and reject unsupported versions
      explicitly.
      `Replay.FormatVersion`, `CommandLog.Version`, `Canonical.FormatVersion`
      (all `1`); `ReplayError` cases `UnsupportedReplayVersion`,
      `UnsupportedCommandLogVersion`, `UnsupportedCanonicalFormat`.
      `ReplayTests`: three `replay rejects an unsupported ... version` facts.
- [x] The stated determinism contract does not claim unsupported cross-platform
      behaviour.
      Implementation matches `docs/09_TEST_STRATEGY.md` section 3 and
      `docs/04_SIMULATION_SPEC.md` section 3 unchanged: same revision, target,
      architecture, content, initial state, command stream, and seed. No
      cross-platform, bit-identical, or cryptographic claim is made in code or
      docs (FNV-1a and SplitMix64 comments state this explicitly).
- [x] No forbidden dependency or source of nondeterministic authority exists in
      the simulation.
      `dotnet list ... package --include-transitive` -> `FSharp.Core 10.1.303`
      only, no `ProjectReference`. Source search for `System.Random`,
      `DateTime.Now/UtcNow`, `Stopwatch`, `Guid.NewGuid`, `GetHashCode`,
      `Dictionary`/`HashSet`/`groupBy` in `src/CommandoWar.Sim`: no matches.

## Required verification

- focused PRNG tests;
- canonical-hash tests;
- direct-versus-replay scenario test;
- deliberate divergence test;
- full headless test suite;
- release build;
- source or dependency search for forbidden random and time APIs.

## Evidence to capture

- PRNG algorithm and golden vectors;
- canonical state format version;
- sample command record;
- sample matching hashes;
- sample divergence report;
- exact commands and results.

## Documentation updates

Follow `AGENTS.md`. If the determinism contract changes materially, propose an ADR rather than silently editing assumptions.

## Completion notes (2026-09-02)

New F# modules under `src/CommandoWar.Sim/` (compile order):
`Random.fs` (after `Ids.fs`), `Canonical.fs` and `Hashing.fs` (after
`Snapshot.fs`), `Replay.fs` and `Divergence.fs` (after `Simulation.fs`).

- **PRNG**: SplitMix64 v1 (`Random.fs`). Single 64-bit additive counter,
  wrapping arithmetic, seed initialises the counter directly. `RandomState` is
  a `[<Struct>]` value record (`Algorithm`, `AlgorithmVersion`, `Word`,
  `Draws`); `Draws` is carried for replay diagnostics, not read by the
  generator. Exposed through `IDeterministicRandom` (`SplitMix64.generator`).
  `SplitMix64.next` rejects state from another algorithm with `invalidArg`.
- **Canonical + hash**: `Canonical.encode : WorldState -> byte[]`, format
  version 1, big-endian fixed-width, agents sorted ascending by id, explicit
  present/absent tag on `Destination`, presentation state never included.
  `Hashing.hash` is FNV-1a-64 over that encoding; `StateHash { Format; Value }`.
  Exposed through `IStateHasher` (`Hashing.canonicalHasher`). Neither is a
  cryptographic primitive and the comments say so.
- **WorldState**: gains `Random: RandomState`. `World.create` and
  `Setup.sixAgentWorld` take an explicit `seed: uint64`. `step` threads the
  stream through unchanged (no gameplay draws) and computes `StepResult.StateHash`
  strictly after the phase loop from the already-final state.
- **Replay**: `RecordedCommand { Tick; Sequence; Command; Issuer }`,
  `CommandLog` (v1), `ReplayRecord` (v1, carries `CanonicalFormat`), typed
  `ReplayError` (8 cases, all explicit), `Replay.run` regroups the log by tick
  and re-steps. `Divergence.compare` / `Divergence.diagnose` report the first
  divergent tick with expected/actual `StateHash`, first differing canonical
  section, and per-side random draw counts; a length mismatch is reported as
  `TruncatedRun`, never swallowed.
- **Benchmark**: `BenchmarkTests.fs` — indicative in-process throughput for
  empty and six-agent stepping (not BenchmarkDotNet, no new dependency, no
  timing assertions). Local Release run: empty ~961 ns/tick, six agents
  ~2794 ns/tick, against the 5 ms/tick budget.

Tests: `RandomTests.fs` (7), `CanonicalHashTests.fs` (11), `ReplayTests.fs`
(14), `BenchmarkTests.fs` (2). Full suite 49 passed (15 pre-existing + 34 new).

Determinism contract: unchanged from `docs/09_TEST_STRATEGY.md` section 3. The
algorithm names (SplitMix64, FNV-1a-64) and canonical format version 1 are
implementation choices within the existing contract (spec section 4 delegates
format decisions to the task); no ADR required. Evidence in
`docs/12_PROGRESS_LEDGER.md`.

Future hot paths, left unoptimised until profiled (spec section 19): `Replay.run`
filters the command log once per tick (O(commands x ticks)); `ReplayOutcome`
retains every tick's full `WorldState` for small-state divergence diagnosis.

## Rollback or removal

Replay and hashing must remain isolated from presentation. A failed serialization choice should be replaceable behind project contracts without changing simulation behaviour.

The canonical encoding (`Canonical.fs`), digest (`Hashing.fs`), and replay
records (`Replay.fs`) are isolated behind `IStateHasher` / `IDeterministicRandom`
and versioned record types. `Simulation.step`'s authoritative outputs
(`State`, `Events`, `Snapshot`) are built before the hash and do not depend on
its value, so the digest or byte layout can be replaced without changing
simulation behaviour.
