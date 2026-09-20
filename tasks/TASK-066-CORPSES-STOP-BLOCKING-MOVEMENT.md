# TASK-066: A dead agent's corpse stops blocking movement

Status: done (implemented and self-verified 2026-09-20; accepted by Dave
2026-09-20 on the self-verification evidence alone, its own known
limitation -- does not fix the live-agent chokepoint jam Dave watched --
accepted as a known gap, parked as B-067)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete); realises B-066

## Objective

A non-`Alive` agent's cell currently behaves exactly like a live occupant's
for movement purposes: no other agent can ever enter it, forever. Make a
dead agent's cell pass-through for movement: a live agent can walk onto or
through it exactly as if it were empty.

## Why this task exists

Raised by Dave live-playtesting the real Bridgehead squad advance
(2026-09-20, via a new `--screenshot-squad` capture tool built this session
to let a scripted playthrough be watched visually rather than only read as
a headless hash): watching the six-agent advance play out, most of the
squad jams at the bridge chokepoint and stands there permanently once
casualties occur, because the dead cannot be walked over or around. "the
agents should work like real ones, they would move over a dead body, or
just around it if there is space." This is exactly the finding TASK-064's
own investigation already surfaced and flagged, not a new discovery: "a
friendly agent's own corpse permanently blocks its cell (never vacated) ...
a scout who dies exactly on an objective/target cell can make that
objective permanently unreachable" -- accepted as a known gap at the time,
now selected to fix.

Root cause, confirmed by inspection: `Simulation.navigationAndMovement`'s
`occupantOf` map (the vacation-chain/obstruction resolution TASK-022 added)
is built from every agent's `Position` unconditionally --
`agents |> Array.mapi (fun i a -> a.Position, i) |> Map.ofArray` -- with no
`Vitals` check. `Pathfinding.fs` itself never touches occupancy at all (it
is terrain-only, confirmed by inspection); the "cannot enter" behaviour is
entirely local to this one map. TASK-065 (backlog B-065)'s new stall-abandon
counter now makes the resulting permanent block *visible* rather than a
silent freeze, but does not change the outcome: an order genuinely blocked
by a corpse still cannot ever succeed today.

## Central decision

**Corpses vacate for movement purposes; no `Pathfinding` change.** Excluding
a non-`Alive` agent from `occupantOf` means a live agent can freely enter
that cell -- the vacation-chain/reservation logic (TASK-017/TASK-022)
otherwise runs completely unchanged, and at most one *live* mover can still
ever claim a given cell in one tick (that guarantee comes from the
rival-arbitration stage, upstream of `occupantOf` and untouched here). Real
dynamic avoidance around a still-*live* obstacle (routing around it, not
just through a corpse) remains the separate, larger, explicitly deferred
"occupancy-aware pathfinding" direction from TASK-065 -- not this task's
job, and not requested: Dave's own request was specifically about dead
agents.

Multi-select and joint squad orders (Dave's separate idea, raised in the
same conversation, "we don't have a control scheme for that though") is
parked as backlog B-067, proposed only, not scoped or touched here.

## Required reading

- `src/CommandoWar.Sim/Simulation.fs`: `navigationAndMovement` in full,
  particularly `occupantOf`, `candidateMovers`, the `movers` fixpoint, and
  `obstructedBy` (the exact map this task changes) -- and the intents
  computation's own `if not (Casualty.isAlive a.Vitals) then Idle` (a dead
  agent already never becomes a *mover* itself; this task is only about
  whether one still blocks *other* agents).
- `src/CommandoWar.Sim/Casualty.fs`: `Casualty.isAlive` (the exact
  predicate to reuse, the `intents` computation's own precedent).
- `tasks/TASK-064-INTEGRATE-AND-VERIFY-VERTICAL-SLICE.md` "Outcome" section
  (finding 5: the original discovery of this exact gap) and
  `tasks/TASK-065-STALLED-ORDER-VISIBLE-FAILURE.md` (Forbidden scope
  explicitly excluded this exact mechanism from that task).
- `docs/04_SIMULATION_SPEC.md` section 20 ("one live agent has one
  authoritative position" -- confirm this invariant is about live agents
  only, so a live agent and a corpse sharing a cell after this change does
  not violate it).
- `src/CommandoWar.Client.Godot/src/FSharpSceneHost.cs`'s new
  `--screenshot-squad <frameCount> <path>` (added this session): re-run it
  against the real Bridgehead advance to confirm the squad now actually
  reaches the depot instead of permanently jamming at the bridge.

## Dependencies

- B-011b/B-022 (TASK-017/TASK-022, done -- the reservation/vacation-chain
  mechanism this task narrows one input of). B-065 (TASK-065, review --
  not a hard dependency, but this task's own corpus entry should confirm
  the stall-abandon counter still behaves correctly when a block clears
  mid-freeze via this fix rather than only via full abandonment).

## Inputs and assumptions

- Both sides: `Casualty.isAlive` is checked with no `Side` distinction,
  the existing symmetric-treatment precedent (Perception, Combat).
- A corpse's cell becomes freely enterable the instant `Vitals` leaves
  `Alive` (`Incapacitated` and `Dead` both vacate) -- `docs/04` section 20's
  "dead or incapacitated agents do not start new actions" already treats
  both as non-actors; this task treats both as non-*blockers* too, the
  same generalisation applied consistently.
- No change to `Casualty.fs`, `Combat.fs`, or `stateConsequences` -- only
  `navigationAndMovement`'s own occupancy view changes.
- A live agent may now end the tick sharing a cell with a corpse
  (`Position` equal, one `Alive`, one not) -- confirmed by inspection this
  does not violate any existing invariant or downstream assumption
  (`friendlyAt`/`enemyAt` client-side selection already tie-break by
  scanning agents in order; `Mission` phase occupancy checks for
  `DestroyTarget` already filter to `Alive` agents only, unaffected).
- No `Canonical.FormatVersion` bump: no new field, a behaviour change to an
  existing derived-each-tick map that is itself never part of the canonical
  image.

## Allowed scope

- `src/CommandoWar.Sim/Simulation.fs`: `navigationAndMovement`'s
  `occupantOf` construction gains a `Casualty.isAlive` filter; the phase's
  own doc comment updated to describe the new behaviour.
- New `SimulationTests` fact(s): a live agent's route through a corpse's
  cell succeeds instead of freezing/obstructing.
- A new or extended corpus entry proving it end to end (a friendly dies on
  a chokepoint cell; a second friendly then walks through/onto that same
  cell), if one can be constructed compactly.
- `docs/04_SIMULATION_SPEC.md` (section 20's own note, if the invariant
  wording needs a one-line clarification), `docs/10_RISK_REGISTER.md` (no
  entry currently covers this specifically -- add one only if Dave's
  review finds it warrants tracking), `docs/11_BACKLOG.md` (B-066 row),
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.
- `src/CommandoWar.Client.Godot/README.md`: document the new
  `--screenshot-squad` tool (built this session, used to find this issue,
  not yet documented anywhere).

## Forbidden scope

- No `Pathfinding.fs` change -- it already has no occupancy concept and
  needs none for this fix.
- No real dynamic avoidance around a still-*live* blocking agent -- the
  separate, larger, explicitly deferred direction.
- No multi-select or joint squad-order work (backlog B-067, parked, not
  scoped here).
- No change to how a corpse is selected/targeted client-side
  (`friendlyAt`/`enemyAt`, TASK-053's status-view precedent) -- this task
  is about movement occupancy only.
- No new client UI.

## Required work

1. Confirm current behaviour with a failing (or newly-passing-after-fix)
   `SimulationTests` fact: kill an agent on a cell, order a second agent
   through/onto that cell, confirm it is obstructed forever today.
2. Change `occupantOf` to exclude non-`Alive` agents.
3. Confirm the same fact now passes: the second agent walks straight
   through/onto the corpse's cell without ever freezing.
4. Re-run the full corpus (`--regenerate` if any pre-existing entry's
   behaviour genuinely changes -- expected not to, since no existing entry
   combines a death with a subsequent move through that exact cell; verify
   this by inspection first, not by assuming).
5. Re-run `--screenshot-squad` against the real Bridgehead advance and
   confirm the squad now actually reaches the depot instead of
   permanently jamming at the bridge mouth.
6. Update documentation per Documentation updates below.

## Acceptance criteria

- [x] A `SimulationTests` fact proves a live agent can now enter a cell
      occupied by a non-`Alive` agent (both `Incapacitated` and `Dead`
      inputs covered). `` `a live agent walks straight through a dead
      agent's cell instead of freezing` `` and `` `a live agent walks
      through an Incapacitated agent's cell the same as a Dead one` ``.
- [x] No existing corpus entry's committed table changes -- confirmed:
      `cwheadless corpus` (no `--regenerate`) passes clean, `19/19`, with
      the fix already built. No existing entry combines a death with a
      subsequent move through that exact cell, so this is a genuinely
      zero-blast-radius fix for all committed content.
- [ ] **Not closed as originally framed.** `--screenshot-squad` re-run
      against the real Bridgehead scenario is byte-identical to the
      pre-fix run through tick 600 -- because the specific squad-jam Dave
      watched never involves a death in that window at all: it is
      live-agent-on-live-agent formation contention (five of six agents
      block each other at the chokepoint while all still `Alive`), a
      different mechanism this task does not touch (see B-067). The fix
      itself is correct and proven at the `SimulationTests` level; this
      criterion is retitled honestly rather than checked off on the
      strength of the unit tests alone -- flagged for Dave, not smoothed
      over. Accepted by Dave (2026-09-20) as a known, tracked gap rather
      than pursued further in this task; the real fix is B-067 (parked).
- [x] No `Pathfinding.fs` change. Confirmed by inspection and `git diff`.
- [x] `dotnet test`/`corpus` pass: `414/414` (+2); `19/19` (unchanged, no
      `--regenerate` needed).
- [x] Required documentation updated, including the new
      `--screenshot-squad` tool (previously undocumented).

## Required verification

- `dotnet build CommandoWar.slnx -c Release`: `0` Warning(s), `0` Error(s).
- `dotnet test CommandoWar.slnx -c Release`: `414/414` passed (+2: the two
  new `SimulationTests` facts).
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
  (no `--regenerate`): `19/19` entries match their already-committed
  tables unchanged -- this fix moves no existing hash.
- `Godot_v4.7.2-stable_mono_win64_console.exe --headless --path
  src/CommandoWar.Client.Godot -- --selfcheck`: `MATCH 0x047FF3080AD3EBCB`
  at tick 90, unchanged (no friendly dies within that scripted window
  either).
- `--screenshot-squad` re-run at ticks 54/104/207/404 against real
  `bridgehead.cwscenario`: byte-identical composition to the pre-fix
  filmstrip at every tick checked -- see Acceptance criteria above for
  why (no death occurs in this specific repro).

## Evidence to capture

- command output or test summary (above);
- the `SimulationTests` facts are the primary evidence for the fix itself;
- the `--screenshot-squad` filmstrip showed no visible difference for the
  specific repro Dave watched, recorded honestly rather than claimed as
  visual proof it doesn't have;
- unresolved: the actual "squad jams and never moves" scenario Dave saw
  is not fixed by this task -- see B-067 / the live-agent-contention note
  in Acceptance criteria.

## Expected files

- `src/CommandoWar.Sim/Simulation.fs`
- `tests/CommandoWar.Sim.Tests/SimulationTests.fs` (and `CorpusTests.fs` if
  a new corpus entry is added)
- `content/replays/` (new entry, if added)
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`
- `src/CommandoWar.Client.Godot/README.md`

## Documentation updates

- this task file's status and evidence;
- `docs/11_BACKLOG.md` (B-066 row; B-067 added, proposed, parked);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file;
- `PROJECT_STATE.yaml`;
- `src/CommandoWar.Client.Godot/README.md` (the new `--screenshot-squad`
  tool).

## Rollback or removal

A one-line filter on an already-derived, per-tick, non-canonical map.
Revertible with `git revert` in one step; no re-pin of anything, since no
canonical field or format version changes.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-20), on the self-verification evidence alone,
  confirmed via `AskUserQuestion` alongside TASK-065's acceptance. The fix
  itself (corpses vacate for movement) is correct and proven by direct
  `SimulationTests` facts, and touches no existing committed content. It
  does **not**, however, resolve the specific squad-jam Dave watched via
  `--screenshot-squad`, since that jam is live-agent formation contention,
  not a corpse block -- accepted as a known, tracked gap, not pursued
  further here; that remains B-067 (parked) territory.
- Notes: full detail in `docs/ledger/2026-09-20-TASK-066-corpses-stop-
  blocking-movement.md`.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
