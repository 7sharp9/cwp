# TASK-003: Establish Determinism, Hash, and Replay Harness

Status: proposed  
Owner: unassigned  
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

- [ ] PRNG golden vectors pass and identify the algorithm/version.
- [ ] Repeating the same initial state and command stream yields the same per-tick or final hashes.
- [ ] Replay yields the same authoritative result as direct stepping.
- [ ] Changing one command yields a reported divergence at or after the changed command tick.
- [ ] Canonical state ordering is explicit and covered by tests.
- [ ] Command and replay formats are versioned and reject unsupported versions explicitly.
- [ ] The stated determinism contract does not claim unsupported cross-platform behaviour.
- [ ] No forbidden dependency or source of nondeterministic authority exists in the simulation.

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

## Rollback or removal

Replay and hashing must remain isolated from presentation. A failed serialization choice should be replaceable behind project contracts without changing simulation behaviour.
