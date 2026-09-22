# TASK-076: Coordinate concurrent detour claims to close the swap-standoff residual gap

Status: done
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises B-076, closes the
residual gap risk R-010 named after TASK-070

## Objective

When two agents independently detour around the same parked blocker in the
same tick (TASK-070's mechanism, `Simulation.navigationAndMovement`), and
their independently-computed detours would land them on the same alternate
cell, the second agent's detour query must also avoid the first agent's
just-claimed cell -- so the two do not simply trade the original obstruction
for a new mutual one between themselves (the "swap-standoff" shape).

## Why this task exists

TASK-070 (backlog B-069, done) fixed the permanent-parked-blocker stall by
having an obstructed agent detour around the blocker via one throwaway
`Pathfinding.findWithin` query against a locally patched `Terrain`
(`Terrain.withImpassable parkedCells terrain`). Its own ledger entry found
and explicitly left open a residual gap, tracked as `docs/10_RISK_REGISTER.md`
risk R-010 (severity 4x4, `open`): two agents independently detouring around
the *same* parked blocker can converge on the *same* alternate cell and
obstruct each other instead. `tests/CommandoWar.Sim.Tests/SimulationTests.fs`
already has a fact demonstrating exactly this shape (`` two agents converging
on a cell held by a stationary third never enter it and never collide ``,
around line 782), whose own doc comment names it as the informal
"`swap-standoff` shape" and records it as *accepted*, not fixed, behaviour.

A dedicated scoping pass this session confirmed the mechanism and a small,
low-risk fix (see Central decision) directly against `Simulation.fs`, and
confirmed the blast radius is genuinely isolated: neither the existing
`swap-standoff` corpus entry (a different, pre-existing scenario -- two
agents ordered directly onto each other's cells, TASK-022's mutual-first-mover
rule, unrelated to parked-blocker detours despite the name) nor
`chokepoint-detour` (TASK-070's own corpus entry) exercises this exact
two-agent-detour-collision shape, so neither committed golden is expected to
change. Only the one `SimulationTests.fs` fact above -- which currently
documents the bug as accepted -- needs its assertions rewritten once this is
fixed.

## Required reading

- `docs/10_RISK_REGISTER.md` R-010's row.
- `tasks/TASK-070-CHOKEPOINT-DETOUR-AROUND-PARKED-AGENTS.md` in full --
  the detour mechanism this task extends.
- `docs/11_BACKLOG.md` B-069's row (TASK-070's own record of this residual
  gap).
- `src/CommandoWar.Sim/Simulation.fs`: `navigationAndMovement`'s Pass 3 loop
  (starts line 1649, ascending agent-id order -- an existing, documented
  ordering guarantee, `Events.fs`), `parkedCells` (line 1590), and the
  detour branch itself (lines 1719-1773, especially the `Pathfinding.
  findWithin (Terrain.withImpassable parkedCells terrain) ...` call at line
  1747 and the successful-reroute branch at lines 1756-1773, which commits
  `newRoute.Cells.[1]` as the agent's next cell).
- `src/CommandoWar.Sim/Terrain.fs`: `withImpassable` (line 229, `Cell seq ->
  Terrain -> Terrain`, confirmed it accepts any `Cell seq`, not just an
  array -- no signature change needed).
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`: the fact at line 782
  (`` two agents converging on a cell held by a stationary third never enter
  it and never collide ``) -- read its full doc comment, which already
  narrates the exact swap-standoff shape this task fixes and records it as
  currently-accepted behaviour to be strengthened, not contradicted, by this
  fix.
- `src/CommandoWar.Headless/Corpus.fs`: `swapStandoffSpec` (confirm this is
  the unrelated mutual-first-mover scenario, not this task's own shape,
  before assuming any corpus golden is affected) and the `chokepoint-detour`
  spec (TASK-070's own, confirm it does not involve two agents detouring
  around one blocker either).
- `AGENTS.md`'s Diagnostics section -- this task changes decision logic over
  existing state (which alternate cell a detour picks), not what state
  exists; confirm no new `Overlay`/`GridLayer`/`EdgeMarker` is needed (the
  existing `MovementRerouted` event/`Overlay.Rerouted` from TASK-070 already
  covers "this agent rerouted"; only *which* cell it reroutes to changes).

## Dependencies

- TASK-070 (done -- the mechanism this task extends). No other task
  selected.

## Central decision

**Claim the alternate cell as each detour is adopted, within the same Pass 3
pass, so a later agent's detour query in the same tick sees it as taken.**
Pass 3 already iterates in ascending agent-id order (line 1649, an existing
guarantee). Add a `mutable claimedDetourCells` collection, initialised empty
before the loop; whenever a reroute is adopted (the branch at line 1767),
add the new route's very next cell (`newRoute.Cells.[1]`) to it. Pass
`Terrain.withImpassable (Seq.append parkedCells claimedDetourCells) terrain`
(line 1747) instead of `parkedCells` alone, so a later agent's own detour
query in the same tick treats an earlier agent's just-claimed alternate cell
as impassable too, and finds a genuinely different one instead of colliding
with it. Only the immediate next cell needs claiming -- movement advances
one cell per tick, and contention on any later cell in a multi-cell detour
route is already handled correctly by the existing rival-contest logic the
following tick.

This is a local, Pass-3-scoped mutable value, not new `AgentState`/
`WorldState` -- no `Canonical.FormatVersion` bump, matching TASK-070's own
precedent for `parkedCells` itself.

## Inputs and assumptions

- No `Pathfinding.fs` signature change -- the occupancy signal stays
  entirely expressed through `Terrain.withImpassable`'s locally patched,
  throwaway `Terrain` value, exactly as TASK-070 established. `s.Terrain`
  itself is never written to.
- The existing `SimulationTests.fs` fact at line 782 will need its
  assertions updated once this fix changes which cell the second agent's
  detour lands on -- expect its narrative comment and possibly its specific
  event-sequence assertions to change; this is expected, not a regression,
  and should be updated to describe the new (fixed) behaviour honestly, not
  merely patched to pass.
- A new corpus entry demonstrating this exact two-agent-detour-collision
  shape end to end (with a committed ASCII/SVG golden, the diagnostics
  mandate's own bar) is good practice and recommended, but the scoping pass
  found it is not strictly required for correctness since no existing entry
  covers this shape either way -- use judgement on whether the existing
  `SimulationTests.fs` fact plus a manual trace is sufficient evidence, or
  whether a dedicated corpus entry is warranted; if added, follow the
  `chokepoint-detour` entry's own precedent.

## Allowed scope

- `src/CommandoWar.Sim/Simulation.fs`: the `navigationAndMovement` Pass 3
  loop and its detour branch only.
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`: the affected fact's
  assertions and doc comment.
- Optionally, `src/CommandoWar.Headless/Corpus.fs` and a new
  `content/replays/` entry with its committed golden, if a dedicated corpus
  entry is added (see Inputs and assumptions).

## Forbidden scope

- No `Pathfinding.fs` signature or algorithm change.
- No `AgentState`/`WorldState`/`Canonical` change -- this is decision logic
  over existing per-tick local state only.
- No change to the unrelated `swap-standoff` or `chokepoint-detour` corpus
  entries' own scenario definitions (only their goldens may need
  regenerating if this fix genuinely changes their outcome -- confirm by
  running `-- corpus`, do not assume either way).
- No client-side (`CommandoWar.Client.Godot`) change -- this is a pure
  `CommandoWar.Sim` fix; the scoping pass found no existing Godot
  `--selfcheck` scripted sequence exercises two agents detouring around one
  blocker (TASK-070's own finding, reconfirmed), so no client hash should
  move, but confirm by running `--selfcheck`, do not assume.

## Required work

1. Confirm the exact Pass 3 loop structure and detour branch by inspection
   (Required reading) before editing.
2. Implement the `claimedDetourCells` mechanism (Central decision).
3. Run `dotnet test` and inspect the line-782 fact's actual result; update
   its assertions and doc comment to describe the new, fixed behaviour
   honestly (do not merely adjust numbers until it passes without
   understanding why).
4. Run `-- corpus`; confirm `swap-standoff` and `chokepoint-detour` goldens
   are unaffected (expected) -- if either changes, determine why before
   re-pinning, following the TASK-070/`chokepoint-detour` precedent for
   honestly recording a changed fixture rather than silently re-pinning.
5. Consider and decide whether a dedicated new corpus entry is warranted
   (Inputs and assumptions); if added, author it with a committed
   ASCII/SVG golden per AGENTS.md's diagnostics mandate.
6. Re-run all three Godot `--selfcheck` cases; confirm unaffected.
7. Update `docs/10_RISK_REGISTER.md` R-010's status if this task closes it
   (confirm the register's own table format before editing -- it has no
   prose column, per TASK-070's own prior note on this exact point).
8. Update documentation per Documentation updates below.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] Two agents ordered so their independent TASK-070 detours around a
      common parked blocker would otherwise collide now resolve to distinct
      alternate cells, proven by a `SimulationTests.fs` fact (the updated
      line-782 fact, rewritten). **Found and honestly resolved, not
      assumed:** the task's own literal Central decision
      (`claimedDetourCells` alone) does not close this specific fact's own
      scenario, since the two agents' reroutes happen on different ticks,
      not the same one -- a second, necessary mechanism
      (`advancingNextCells`, from Pass 1's `intents`) was added alongside
      it. See `docs/ledger/2026-09-22-TASK-076-*.md` for the full trace.
- [x] `dotnet test`: `422/422` passed (net unchanged count), with the
      line-782 fact's new assertions explained (what changed and why) in
      its own doc comment and the ledger detail file, not merely
      re-numbered.
- [x] `-- corpus`: `20/20`, byte-identical, no changed golden -- confirms
      `swap-standoff` (unrelated) and `chokepoint-detour` (single-agent
      detour shape) are both genuinely unaffected.
- [x] All three Godot `--selfcheck` hashes unaffected (confirmed by running
      the real Godot 4.7.2 editor, not assumed):
      `CommandDemo.tscn` `MATCH 0x84A25E3559111E9B`,
      `SnapshotDemo.tscn` `MATCH 0x6213D672BC36FDB8`,
      `AppraisalDemo.tscn` `MATCH 0xA1354EB998FC1B95`, all unchanged from
      TASK-070's own pins.
- [x] No `Pathfinding.fs`/`AgentState`/`WorldState`/`Canonical` change; no
      `Canonical.FormatVersion` bump (stayed at 15). Confirmed by `git diff
      --stat`: only `Simulation.fs` and `SimulationTests.fs` changed.
- [x] `docs/10_RISK_REGISTER.md` R-010 updated: `open -> watch` (not
      `closed` -- see the ledger detail file's own reasoning; a judgement
      call for Dave to override).
- [x] Required documentation updated (this task file, `docs/10_RISK_
      REGISTER.md`, the new ledger detail file; `docs/11_BACKLOG.md`,
      `docs/12_PROGRESS_LEDGER.md`'s index, and `PROJECT_STATE.yaml`
      deliberately left to the orchestrating session per its own explicit
      instruction).

## Required verification

- `dotnet build CommandoWar.slnx -c Debug`: `0 Warning(s)`, `0 Error(s)`.
- `dotnet test CommandoWar.slnx -c Debug`: `422/422` passed.
- `dotnet run --project src/CommandoWar.Headless -- corpus`: `20/20`,
  byte-identical, no golden changed.
- `dotnet run --project src/CommandoWar.Headless -- replay-file
  content/replays/envelope-full.cwreplay`: checkpoints OK, unaffected.
- `"$GODOT" --headless --path src/CommandoWar.Client.Godot
  scenes/CommandDemo.tscn -- --selfcheck` (and `SnapshotDemo.tscn`/
  `AppraisalDemo.tscn`): all three `MATCH`, unchanged pinned hashes.

Full command output and results in `docs/ledger/2026-09-22-TASK-076-*.md`.

## Evidence to capture

- The exact scenario/order sequence proving two detours no longer collide
  (the updated line-782 fact's own inputs).
- The line-782 fact's before/after assertions, with an explanation of what
  changed -- see this task's own doc comment in `SimulationTests.fs` and
  the ledger detail file.
- Command output for all verification above -- recorded in the ledger
  detail file, not duplicated here.

## Expected files

- `src/CommandoWar.Sim/Simulation.fs`.
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs`.
- `docs/10_RISK_REGISTER.md` (R-010 status, `open -> watch`).
- `docs/ledger/2026-09-22-TASK-076-swap-standoff-detour-coordination.md`
  (new).

No `Corpus.fs`/`content/replays/` change was made -- judged not warranted,
see this task file's own Inputs and assumptions and the ledger detail
file's reasoning.

## Documentation updates

- this task's own status and evidence;
- `docs/10_RISK_REGISTER.md` R-010, if closed;
- `docs/11_BACKLOG.md`: new B-076 row;
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
- `PROJECT_STATE.yaml` if the active task/phase/gate changes.

Reconciled centrally by the orchestrating session, not by this task's own
implementing agent, if dispatched alongside other parallel tasks.

## Rollback or removal

A small, additive change confined to one function in `Simulation.fs` plus
one test's assertions -- a `git revert` of this task's commit cleanly
restores TASK-070's own (documented-as-incomplete) behaviour.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-22, "accept all three, commit"), on the
  implementation-plus-independent-re-verification evidence recorded here and
  in `docs/ledger/2026-09-22-TASK-076-swap-standoff-detour-coordination.md`
  (dispatched as one of three parallel implementation agents, each in an
  isolated worktree, and separately rebuilt/retested/re-`--selfcheck`ed by
  the orchestrating session -- including combined with TASK-074/TASK-075's
  own changes in the same tree -- before being reported as ready for
  review). The R-010 `open -> watch` judgement call was reviewed and
  accepted as-is, not overridden.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
