# Progress and Evidence Ledger

This ledger records what was actually done, not intended progress. It is
append-only except for correcting factual errors.

It is split in two. This file is the **index**: a pinned-facts snapshot and one
row per entry. The full text of each entry (its owner, source revision,
environment, changes, verification commands, evidence, deviations, documents
updated, and review) lives in its own **detail file** under `docs/ledger/`,
named `YYYY-MM-DD-<ID>-<slug>.md` and keeping its original heading as the title.

A completed task appends a row to the index table below and adds a matching
detail file under `docs/ledger/`; if any pinned value changed, the same change
refreshes the "Pinned facts" block. A past row or a past detail file is never
rewritten except to correct a factual error.

## Pinned facts

A faithful snapshot for quick reference, not a source of new claims. Provenance
and detail are in the linked entries.

| Fact | Value | Confirmed by |
|---|---|---|
| .NET SDK | `10.0.303` (`global.json`, `rollForward: latestPatch`) | TASK-001 |
| Target framework | `net10.0` (all projects) | TASK-001 |
| FSharp.Core | `10.1.303` (implicit, SDK-pinned) | TASK-001 |
| `Canonical.FormatVersion` | `1` | TASK-003 |
| `Replay.FormatVersion` | `1` | TASK-003 |
| `CommandLog.Version` | `1` | TASK-003 |
| `ScenarioContent.Version` | `2` (authored terrain layer added; independent of the three versions above; version 1 rejected, not migrated) | TASK-010 |
| PRNG | SplitMix64 v1 (seed initialises the 64-bit counter directly) | TASK-003 |
| State hash | FNV-1a-64 over `Canonical.encode` (not a cryptographic primitive) | TASK-003 |
| BenchmarkDotNet | `0.15.8` (pinned; `bench/CommandoWar.Benchmarks/` only, never in `CommandoWar.Sim` or the test project) | TASK-014 |
| Green tests | `153` — `dotnet test CommandoWar.slnx -c Release` (`Passed: 153`; +8 over TASK-014's 145 for the movement-executor / overlay facts; the benchmark project contributes no tests) | TASK-015 |
| Accepted ADRs | ADR-0001 (Godot, accepted 2026-09-03); ADR-0002 (framework-independent sim, project baseline); ADR-0003 (Mibo: spike complete, **not adopted**); ADR-0004 (low-impedance C#/F# boundary, accepted 2026-09-03) | TASK-007 acceptance |
| Current gate | `G2_deterministic_core_proven` (pending) | TASK-006 finalisation |
| Current phase | `P2_deterministic_core` | TASK-006 finalisation |

### Shared fixture

Defined in `src/CommandoWar.Headless/Fixture.fs`; pinned by
`tests/CommandoWar.Sim.Tests/FixtureTests.fs`; per-tick hash table in
`content/fixtures/SPIKE-FIXTURE.md`; canonical command log
`content/fixtures/spike-fixture.cwlog`.

| Parameter | Value |
|---|---|
| Builder | `Setup.sixAgentWorld` |
| Grid | 32 x 32 |
| Seed | `20260902` (`0x0000000001352826`) |
| Agents at tick 0 | 6 friendly, column `x = 0`, rows `y = 0..5` |
| Command | tick 1: agent 3 `MoveTo (20, 14)`, `CommandId 1` |
| Tick count | 40 (agent 3 reaches `(20, 14)` at tick 31; ticks 32..40 are rest) |
| Initial hash (tick 0) | `0xF2F3DF0D820AD9AC` |
| Final hash (tick 40) | `0x838D3AE7DBFB735D` (format 1) |
| Domain events | 33 (1 `CommandAccepted` + 31 `MovementStepped` + 1 `MovementCompleted`); 0 random draws |

## Detail entry template

Each `docs/ledger/*.md` file holds one entry in this shape:

```markdown
## YYYY-MM-DD - TASK-NNN - concise outcome

**Owner:**
**Source revision:**
**Environment:**
**Status change:** `old -> new`

### Changes

- ...

### Verification

- Command: `...`
  - Result: ...
- Manual check: ...
  - Result: ...

### Evidence

- Paths, logs, screenshots, benchmark files, replay IDs, or hashes.

### Deviations and unresolved issues

- None, or explicit details.

### Documents updated

- ...

### Review

- Reviewer: Dave
- Accepted: yes/no/pending
- Notes: ...
```

## Index

Chronological. One row per detail file.

| Date | ID | Outcome | Status change | Accepted | Detail |
|---|---|---|---|---|---|
| 2026-09-02 | PLAN-001 | Initial project control pack assembled (charter, gates, ADRs, risks, backlog, bounded tasks) | `unstructured concept -> P0 governance and feasibility` | pending (G0 passed at TASK-001 acceptance) | [detail](ledger/2026-09-02-PLAN-001-initial-control-pack.md) |
| 2026-09-02 | TASK-001 | Repository and build baseline: `global.json`, `CommandoWar.slnx`, F# sim + xUnit projects, boundary tests | `active -> review` | yes (2026-09-02) | [detail](ledger/2026-09-02-TASK-001-repository-baseline.md) |
| 2026-09-02 | TASK-002 | Framework-neutral simulation skeleton: domain types, 11-phase `Simulation.step`, placeholder movement | `active -> review` | yes (2026-09-02) | [detail](ledger/2026-09-02-TASK-002-simulation-skeleton.md) |
| 2026-09-02 | TASK-003 | Deterministic SplitMix64 PRNG, canonical encoding, FNV-1a-64 hash, replay + divergence harness | `active -> review` | yes (2026-09-02) | [detail](ledger/2026-09-02-TASK-003-determinism-harness.md) |
| 2026-09-02 | TASK-004 | Disposable Godot .NET framework spike; shared `cwheadless` reference and pinned fixture added | `active -> review` | yes (2026-09-02) | [detail](ledger/2026-09-02-TASK-004-godot-spike.md) |
| 2026-09-02 | TASK-005 | Environment check: stable Mibo hard-depends on prohibited `Mibo.Adaptive`; spike rescoped and pinned to 4.1.0 | none (TASK-005 stays `ready after dependency`) | yes (2026-09-02) | [detail](ledger/2026-09-02-TASK-005-mibo-environment-check.md) |
| 2026-09-02 | TASK-005 | Disposable Mibo + raylib framework spike (Mibo 4.1.0); shared fixture 41-hash sequence reproduced | `active -> review` | yes (2026-09-03) | [detail](ledger/2026-09-02-TASK-005-mibo-spike.md) |
| 2026-09-03 | TASK-004 interactive addendum | Godot editor GUI still not drivable in-session; project imports into the 4.7.2 editor with no error | none (TASK-004 stays `done`) | n/a (addendum) | [detail](ledger/2026-09-03-TASK-004-interactive-addendum.md) |
| 2026-09-03 | TASK-006 | Spikes evaluated; ADR-0001 scored table completed (Godot 4.13 / Mibo 3.62); Godot recommendation put to Dave | TASK-005 `review -> done`; TASK-006 `proposed -> active` | yes (2026-09-03) | [detail](ledger/2026-09-03-TASK-006-spikes-evaluated.md) |
| 2026-09-03 | TASK-006 finalisation | ADR-0001 accepted (Godot); G1 passed; Mibo route retired | ADR-0001 `proposed -> accepted`; `G1 -> passed`; gate `G1 -> G2`; phase `P1 -> P2`; TASK-006 `active -> done` | yes (2026-09-03) | [detail](ledger/2026-09-03-TASK-006-finalisation.md) |
| 2026-09-03 | TASK-007 | Godot C#/F# client boundary designed; ADR-0004 proposed; disposable proof built (questions 1-5) | TASK-007 `-> active -> review`; ADR-0004 created (`proposed`) | yes (2026-09-03) | [detail](ledger/2026-09-03-TASK-007-godot-fsharp-boundary.md) |
| 2026-09-03 | TASK-007 acceptance | ADR-0004 accepted; boundary design signed off | ADR-0004 `proposed -> accepted`; TASK-007 `review -> done` | yes (2026-09-03) | [detail](ledger/2026-09-03-TASK-007-acceptance.md) |
| 2026-09-03 | TASK-008 | Authored `Scenario` model, `ScenarioContent.Version`, one-pass `Scenario.validate`, `World.ofScenario` | `active -> review` | yes (2026-09-03) | [detail](ledger/2026-09-03-TASK-008-scenario-dto.md) |
| 2026-09-03 | TASK-009 | Progress ledger restructured into this index plus per-entry detail files under `docs/ledger/` | `active -> review` | yes (2026-09-03) | [detail](ledger/2026-09-03-TASK-009-ledger-restructure.md) |
| 2026-09-03 | TASK-010 | Authoritative terrain grid: `Terrain` module (elevation, passability, cost, opacity, directional cover), authored layer + validation, `ScenarioContent.Version` 2 | `active -> review -> done` | yes (2026-09-04) | [detail](ledger/2026-09-03-TASK-010-terrain-grid.md) |
| 2026-09-03 | TASK-011 | Framework-neutral `DiagnosticFrame` observer in `CommandoWar.Sim` + deterministic ASCII/SVG/HTML renderers, `cwheadless render` verb, terrain-demo scenario, golden renders, and the "visually inspectable" standing rule | `proposed -> active -> review -> done` | yes (2026-09-04) | [detail](ledger/2026-09-03-TASK-011-diagnostic-visualisation.md) |
| 2026-09-04 | TASK-012 | Deterministic point-to-point line of sight (`Sight` module: integer supercover walk, symmetric corner rule, ridge-occlusion elevation rule) + `SightRay` diagnostic overlay + `--los` render option + LOS demo goldens | `proposed -> active -> review -> done` | yes (2026-09-04) | [detail](ledger/2026-09-04-TASK-012-line-of-sight.md) |
| 2026-09-04 | TASK-013 | Deterministic grid pathfinding (`Pathfinding` module: 4-connected A*, integer cost + Manhattan heuristic, total-order frontier key, N/E/S/W neighbour order, explicit expansion budget) + `PlannedPath` diagnostic overlay + `--path` render option + pathfinding demo goldens | `proposed -> active -> review -> done` | yes (2026-09-04) | [detail](ledger/2026-09-04-TASK-013-pathfinding.md) |
| 2026-09-04 | TASK-014 | Headless performance and allocation benchmark harness (`bench/CommandoWar.Benchmarks/`: empty tick, six- and ~50-agent placeholder movement, line-of-sight batch, pathfinding open/blocked/choke, canonical encode + state hash, replay run) + committed `content/benchmarks/BASELINE.md` | `proposed -> active -> review -> done` | yes (2026-09-04) | [detail](ledger/2026-09-04-TASK-014-benchmark-harness.md) |
| 2026-09-04 | TASK-015 | Navigation and movement phase: `Pathfinding`-driven single-agent executor replacing `PlaceholderMovement` (`docs/04` section 8 steps 2/4/5/6) + `AgentState.Route` non-canonical derived cache (`FormatVersion` stays 1, fixture hashes unmoved) + `MovementBlocked` event + `Diagnostics.frameOf` `PlannedPath` overlay with `fixture-mid-route.*` goldens; cell reservation / formation slots / sub-cell progress split to B-011b | `proposed -> active -> review` | pending | [detail](ledger/2026-09-04-TASK-015-movement-executor.md) |
