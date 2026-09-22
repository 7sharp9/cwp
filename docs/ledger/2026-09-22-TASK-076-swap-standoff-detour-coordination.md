## 2026-09-22 - TASK-076 - Coordinate concurrent detour claims to close the swap-standoff residual gap

**Owner:** Dave
**Source revision:** working tree on top of `main` at `e22a20c` (TASK-072/
TASK-073, both accepted and committed)
**Environment:** Windows x64, .NET SDK, Godot `4.7.2-stable_mono_win64`
**Status change:** `none -> review` (self-verified)

### Changes

Realises new backlog row B-076: closes the residual gap TASK-070 found and
left open in `docs/10_RISK_REGISTER.md` R-010 -- two agents independently
detouring around the same parked blocker (TASK-070's own mechanism) can
converge on the same alternate cell and obstruct each other instead (the
`swap-standoff` shape), a permanent mutual standoff bounded only by the
pre-existing stall-abandon give-up path, not resolved further by TASK-070
itself.

**Implementation, and a real gap found in the task file's own literal
Central decision, not smoothed over.** The task file's Central decision
specified one mechanism: a `mutable claimedDetourCells` collection, reset
every tick before the Pass 3 loop, appended to whenever a reroute is
adopted, unioned into `parkedCells` for the next agent's own detour query
via `Terrain.withImpassable` -- so two agents rerouting *in the same tick*
never pick the same cell. This was implemented exactly as specified
(`Simulation.fs`, the `claimedDetourCells` binding immediately before the
Pass 3 `for` loop, and the `newRoute.Cells.[1] :: claimedDetourCells`
append inside the successful-reroute branch).

Tracing the pre-existing `SimulationTests` swap-standoff fact tick by tick
with a temporary `dotnet fsi` probe (removed after use) before touching its
assertions found that this literal fix, alone, does **not** change that
fact's outcome at all: agent 0 becomes obstructed by the parked blocker and
reroutes at tick 2; agent 1 is not obstructed by the parked blocker that
same tick (it loses the same-tick rival contest for the blocker's own cell
to agent 0 first, via the pre-existing, untouched `yieldedTo` mechanism,
and simply freezes); agent 1 only becomes obstructed and attempts its own
reroute one tick *later*, at tick 3. By tick 3, `claimedDetourCells` (reset
every tick) no longer remembers agent 0's tick-2 claim, so agent 1's
detour query has no way to know agent 0 is about to occupy that cell --
and it picks the identical alternate cell, reproducing the exact same
standoff as before this task, confirmed by re-running the full test suite
(`sawObstruct` in the pre-existing fact still fired, unchanged).

This is a genuine, cross-tick instance of the same underlying problem the
Central decision names, just not literally "the same tick" in the integer-
tick sense the task file's own wording assumes. The fix was extended, still
confined to `navigationAndMovement`'s Pass 3 / detour branch (the task's
own Allowed scope) and still expressed purely through `Terrain.
withImpassable`'s locally patched, throwaway `Terrain` value (no
`Pathfinding.fs`, `AgentState`, `WorldState`, or `Canonical` change): a new
`advancingNextCells: Cell[]`, computed once from Pass 1's already-existing
`intents` array (every `Advancing` agent's own already-known "next cell"
for this tick, fixed before Pass 3 begins, regardless of iteration order),
unioned alongside `parkedCells` and `claimedDetourCells` into the patched
terrain for every detour query. This directly covers agent 1's tick-3
query in the example above: agent 0's `intents` entry for tick 3 already
names the cell it is about to step into (simply continuing the route it
adopted at tick 2), and that is exactly the information the literal
`claimedDetourCells`-only mechanism could not see. `claimedDetourCells`
remains necessary alongside it for the genuinely-same-tick case (two
reroutes freshly adopted within the same Pass 3 pass), since a
freshly-chosen alternate cell is never part of the original `intents` --
only a route continuing from a *prior* tick's adoption is.

Re-traced the same fact with the extended fix: agent 0 reroutes at tick 2
(as before); agent 1's own tick-3 reroute now avoids agent 0's committed
cell (present in `advancingNextCells`) and picks a different one; both
agents proceed with zero mutual `MovementObstructed`, reaching their own
original destinations by tick 7 and tick 8 respectively, neither ever
`MovementAbandoned`.

**Test.** `tests/CommandoWar.Sim.Tests/SimulationTests.fs`'s pre-existing
fact (`` two agents converging on a cell held by a stationary third never
enter it and never collide ``, ~line 782) was renamed and rewritten, not
merely re-numbered: its doc comment now narrates the real mechanism above
(TASK-070's known limitation, why the literal same-tick-only fix does not
reach it, and what `advancingNextCells` adds); its assertions changed from
"expect a `MovementObstructed` between the two rerouted agents" (asserting
the old, now-fixed bug as accepted behaviour) to "expect zero
`MovementObstructed` between them, two `MovementRerouted` events, no
`MovementAbandoned`, and both agents reaching their own original
destinations" -- run and confirmed to fail under the literal
`claimedDetourCells`-only implementation and pass under the extended one,
not adjusted until green without understanding why.

**Diagnostics judgement (AGENTS.md).** No new `Overlay`/`GridLayer`/
`EdgeMarker` was added. This task changes decision logic over already-
diagnosed state (*which* alternate cell a detour picks), not what state
exists -- the existing `MovementRerouted` event and `Overlay.Rerouted`
(TASK-070) already render "this agent rerouted, avoiding this occupant" for
any detour, including the ones this task's fix newly redirects; there is no
new authoritative spatial/tactical state for a reviewer to see that the
existing overlay does not already cover.

**Corpus judgement (task file's own "Inputs and assumptions" call).** No
new `content/replays/`/`Corpus.fs` entry was added. The rewritten
`SimulationTests` fact, traced tick by tick against a real, minimal
repro and independently re-verified by hand twice (once under each
implementation), is direct, legible evidence of the exact bug and its fix;
a dedicated corpus entry would mostly restate the same two-agent/one-
blocker shape already covered by that fact and by `chokepoint-detour`
(TASK-070's own entry, confirmed by inspection to be a single-agent-detour
shape, not a two-agent coordination one) without adding new diagnostic
value, for a bug this narrow (a decision-logic refinement over an already-
working, already-diagnosed mechanism).

No new canonical field and no `Canonical.FormatVersion` bump (stayed at
`15`) -- `claimedDetourCells` and `advancingNextCells` are both local,
Pass-3-scoped derived values recomputed every tick, matching `parkedCells`'
own precedent exactly.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Debug`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Command: `dotnet test CommandoWar.slnx -c Debug`
  - Result: `422/422` passed (net unchanged count -- one existing fact
    rewritten in place, no fact added or removed).
- Command: `dotnet run --project src/CommandoWar.Headless -c Debug --
  corpus`
  - Result: `20/20` entries match, byte-identical, no `--regenerate`
    needed -- confirms `swap-standoff` (the unrelated TASK-022
    mutual-first-mover scenario) and `chokepoint-detour` (TASK-070's own,
    single-agent-detour shape) are both genuinely unaffected, exactly as
    the task file's own scoping pass predicted.
- Command: `dotnet run --project src/CommandoWar.Headless -c Debug --
  replay-file content/replays/envelope-full.cwreplay`
  - Result: `checkpoints : OK (24 ticks match the file's committed
    hashes)`, unaffected.
- Command: `dotnet build
  src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
  - Result: `0 Warning(s)`, `0 Error(s)`.
- Manual check (real Godot 4.7.2 editor, one-time `--editor --headless
  --quit` import run first): `--headless --path src/CommandoWar.Client.Godot
  scenes/CommandDemo.tscn -- --selfcheck`
  - Result: `MATCH 0x84A25E3559111E9B` at tick 90 -- unchanged from
    TASK-070's own pin, not re-pinned.
- Manual check: same, `scenes/SnapshotDemo.tscn` / `scenes/
  AppraisalDemo.tscn`
  - Result: `MATCH 0x6213D672BC36FDB8` / `MATCH 0xA1354EB998FC1B95`, both
    unaffected -- confirmed by running, not assumed, matching the task
    file's own instruction.
- Command: temporary `dotnet fsi` probes (removed after use,
  `<scratchpad>/probe-swap-standoff.fsx`, `probe-same-tick.fsx`): (1) the
  exact `SimulationTests` swap-standoff repro, traced tick by tick under
  the literal `claimedDetourCells`-only implementation first (confirmed no
  change from pre-task behaviour: agent 0 reroutes tick 2, agent 1
  reroutes tick 3 to the identical cell, mutual `MovementObstructed`
  forever after); re-traced under the extended implementation (agent 1's
  tick-3 reroute picks a different cell, both reach their own destinations
  by tick 7/8, zero mutual obstruction); (2) a hand-built symmetric
  two-separate-blocker scenario attempting to force a genuinely same-tick
  double reroute, used only to inspect `Pathfinding.fs`'s deterministic
  North-East-South-West neighbour tie-break in practice, not committed as
  a test (see Deviations below).
- Command: `git status --porcelain` / `git diff --stat`
  - Result: exactly `src/CommandoWar.Sim/Simulation.fs` and `tests/
    CommandoWar.Sim.Tests/SimulationTests.fs`, matching this task's
    Allowed scope precisely; no `Pathfinding.fs`/`AgentState`/`WorldState`
    entry.

### Evidence

- The rewritten `SimulationTests` fact (`tests/CommandoWar.Sim.Tests/
  SimulationTests.fs`, ~line 782) is the direct, tick-traced proof: two
  agents ordered so their independent TASK-070 detours around a common
  parked blocker would otherwise collide (confirmed failing without the
  `advancingNextCells` extension) now resolve to distinct alternate cells
  and both reach their own original destinations, zero mutual
  `MovementObstructed`, zero `MovementAbandoned`.
- The before/after of that one fact: before, its own doc comment recorded
  the swap-standoff as *accepted* behaviour and asserted a
  `MovementObstructed` between the two rerouted agents as the expected
  outcome; after, the doc comment narrates the real fix (including why the
  task file's own literal Central decision alone did not reach this
  specific repro) and the assertions invert to prove the standoff no
  longer occurs.
- `dotnet test`/`-- corpus`/replay-checkpoint/Godot `--selfcheck` results
  above.

### Deviations and unresolved issues

- **The task file's own literal Central decision (`claimedDetourCells`
  alone, reset every tick) does not close the named repro on its own** --
  found by tracing, not assumed, and reported here rather than silently
  building past it. The fix actually needed a second, necessary
  complementary source of exclusion (`advancingNextCells`, from Pass 1's
  already-computed `intents`) to reach the cross-tick case the existing
  `SimulationTests` fact actually exercises. Both mechanisms are kept:
  `claimedDetourCells` alone still matters for a genuinely same-tick
  double-reroute (two different parked blockers, two different agents,
  both first obstructed and both rerouting within the identical tick --
  structurally possible, since two agents can only contest the *same*
  blocker's cell after the pre-existing rival-contest mechanism has
  already filtered them to one candidate per tick, but two *different*
  blockers obstructing two *different* agents in the same tick is not
  filtered that way).
- **No dedicated unit test was built for the literal same-tick double-
  reroute sub-case in isolation.** A hand-built symmetric scenario (two
  separate parked blockers, two agents on mirrored rows) was tried to force
  it deterministically; `Pathfinding.fs`'s real, deterministic North-East-
  South-West neighbour tie-break turned out to send both agents' detours
  in a consistent absolute direction (both "north" in the tried layout)
  rather than converging on the same cell, so it did not naturally
  reproduce a collision, and engineering an artificial wall geometry to
  force one felt disproportionate to the risk: `claimedDetourCells` is a
  two-line accumulate-then-union addition to a code path (`Terrain.
  withImpassable` + `Seq.append`) already exercised and proven correct by
  the fact above, and its own correctness under the same-tick case follows
  directly from Pass 3's existing, pre-existing, already-relied-upon
  ascending-agent-id iteration order (an earlier agent's claim is always
  appended to the list before a later agent's query in the same loop
  reads it) rather than needing a bespoke test to establish. Flagged here
  rather than silently claimed as covered.
- `docs/10_RISK_REGISTER.md` R-010 changed from `open` to `watch`, not
  `closed`: the one concrete, named residual gap that kept it `open`
  through TASK-070 (the swap-standoff shape) is now closed by this task,
  but R-010's own wording ("pathfinding, formation, and local avoidance
  dominate development") describes a broader, ongoing development-cost
  risk category, not only this one bug -- future content (larger squads,
  denser terrain) could still surface a different coordination edge case.
  This is a judgement call, not certainty; flagged for Dave's own review
  rather than silently closed.

### Documents updated

- `tasks/TASK-076-SWAP-STANDOFF-DETOUR-COORDINATION.md` (status,
  acceptance criteria, verification, evidence, review).
- `docs/10_RISK_REGISTER.md` (R-010: `open -> watch`).
- This ledger detail file.

Not touched, per the orchestrating session's own instruction (reconciled
centrally alongside TASK-074/TASK-075): `docs/11_BACKLOG.md`,
`docs/12_PROGRESS_LEDGER.md`'s index table, `PROJECT_STATE.yaml`.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-22, "accept all three, commit"), after independent
  re-verification by the orchestrating session (rebuild, `dotnet test`
  422/422 net unchanged, `-- corpus` 20/20 byte-identical, all three Godot
  `--selfcheck` hashes unchanged with TASK-074/TASK-075's own changes
  combined in the same tree). The `docs/10_RISK_REGISTER.md` R-010
  `open -> watch` judgement call was reviewed and accepted as-is.
