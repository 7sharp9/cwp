# TASK-059: Formation slots

Status: done (accepted by Dave 2026-09-19, no changes requested)
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature complete); realises backlog B-011d
Size: M

## Objective

An agent authored into a formation, when its own `MoveTo` order is accepted,
resolves its real pathfinding destination as *that order's own target cell*
(the formation anchor) plus the agent's assigned slot offset — redirected to
the nearest passable, unoccupied cell if the exact offset cell is blocked —
instead of the literal ordered cell. Ordering several formation-mates onto
the same nominal cell now spreads them into formation instead of colliding
or contesting one cell (`Simulation.fs`'s existing same-tick reservation
pass, TASK-017, still resolves any residual same-tick contention exactly as
today).

## Why this task exists

No squad/formation grouping or per-agent slot concept exists anywhere in
`Domain.fs` or `Scenario.fs` (`docs/05_COMMAND_AND_AGENT_AI.md` section 17,
`Perception.fs` line 38, `Simulation.fs` line 703). B-011c originally bundled
this with sub-cell movement progress; TASK-018 split it out as **B-011d**,
unstarted since. "The squad" today means only "every `Friendly` agent" with
no structure — this task adds the first real grouping primitive.

## Design decisions (confirmed with Dave via `AskUserQuestion`, two rounds,
before drafting)

- **No new order type, no multi-agent order dispatch.** Every order still
  targets exactly one agent, exactly as today (`docs/07` section 4). A
  formation changes how *that one agent's own* `MoveTo` target resolves, not
  who receives an order.
- **Anchor = the agent's own ordered cell**, not a designated leader's
  position. The cell the player ordered for this specific agent IS the
  formation anchor; the agent's real destination is anchor + its own slot
  offset. This re-resolves once at order-acceptance time (`docs/04` section
  12.5, the existing `Destination`-write point), not continuously — the same
  treatment `Destination` already gets for every other order type.
- **Fallback on a blocked or occupied offset cell: nearest passable, free
  cell search**, the `Appraisal.bestCoverNear` precedent (TASK-031,
  `AppraisalConfig.HoldCoverSearchRadius`) — a bounded Chebyshev-radius
  search outward from the offset cell, falling back to the anchor cell
  itself if nothing in radius qualifies (mirroring `bestCoverNear`'s
  `Option.defaultValue area`). Crowding or terrain at a slot must never turn
  into a hard order failure by itself.
- **Both `Friendly` and `Hostile` agents may be authored into a formation**
  (explicit choice, overriding the friendly-only default every existing
  squad-picture/HQ precedent uses — there is no behavioural reason to
  restrict it, and nothing downstream assumes friendly-only).
- **Scoped to `MoveTo` only.** `Hold` already redirects its target through
  `bestCoverNear`; `Assault`/`Withdraw` carry their own staged-FSM/resolve-
  bonus semantics (TASK-047). Composing formation-offset resolution with any
  of those is a real, separate design question, not a mechanical extension —
  out of scope for this task (see Forbidden scope). An agent in a formation
  whose accepted order is `Hold`/`Assault`/`Withdraw`/`Suppress` behaves
  exactly as it does today.
- **Authored per-scenario**, not a hardcoded shape formula. New
  `RawScenario.Formations` table (named formation, an ordered array of
  relative `(dx, dy)` slot offsets from the anchor — the TASK-058 `Jammers`
  precedent for "a new optional authored table", not the flat scalar-map
  `UnitTypes` shape, since a formation is structured data, not one number).
  `RawDeployment` gains an optional formation id + slot index reference,
  resolved and validated exactly like `RawDeployment.UnitType` (TASK-049):
  an unresolved formation id, or a slot index outside that formation's slot
  count, is a validation error — no silent default. A deployment naming no
  formation behaves exactly as today (no offset, no redirect).
  `ScenarioContent.Version` 5 -> 6.
- **The resolved per-agent slot offset is static, non-canonical state**,
  the identical argument that keeps `AgentState.MoveSpeed` (TASK-049) out of
  `Canonical.encode`: authored once at scenario load, never changes during
  simulation. No `Canonical.FormatVersion` bump.

## Required reading

- `PROJECT_STATE.yaml`
- `AGENTS.md`
- `docs/04_SIMULATION_SPEC.md` section 8 (navigation and movement), section
  12.5 (Appraisal writes `Destination`)
- `docs/05_COMMAND_AND_AGENT_AI.md` section 17 (deferred systems: squad/
  formation grouping)
- `docs/07_VERTICAL_SLICE.md` section 4 (`PlayerIntent` targets a bare
  `Cell`, one agent per order)
- `tasks/TASK-018-SUBCELL-MOVEMENT-PROGRESS.md` (the original B-011c/B-011d
  split and why)
- `tasks/TASK-049-AGENT-MOVEMENT-SPEED.md` (the `RawScenario.UnitTypes` /
  `RawDeployment.UnitType` resolve-and-validate pattern to mirror; the
  static-non-canonical precedent)
- `tasks/TASK-058-COMMUNICATION-RANGE-DELAY-JAMMING-AND-RADIO-DESTROYED.md`
  (the `RawScenario.Jammers` "new optional authored table" pattern to
  mirror, closer in shape than `UnitTypes`)
- `src/CommandoWar.Sim/Appraisal.fs` (`bestCoverNear`, the nearest-valid-cell
  search precedent to reuse/adapt — note it scores by threat *pressure*;
  this task's search only needs passability + non-occupation, a simpler
  scoring key)
- `src/CommandoWar.Sim/Scenario.fs` (`RawUnitType`/`UnitTypes` resolution,
  `RawJammer`/`Jammers` resolution, validation error DU and `validate`)
- `src/CommandoWar.Sim/Simulation.fs` around line 978 (`| Accepted, MoveTo
  target -> Some target`, the exact `Destination`-write site to change) and
  line 1299 (the existing "Formation slots are B-011d" marker comment)
- `src/CommandoWar.Sim/Domain.fs` (`AgentState`, `Cell`, `PlayerIntent`)
- `src/CommandoWar.Sim/Diagnostics.fs` (`Overlay` cases, `AgentMarker`) —
  diagnostics-extension rule in `AGENTS.md`

## Dependencies

- none (B-011d has no blocking dependency; TASK-018 already landed the
  sub-cell progress half of the original B-011c)

## Inputs and assumptions

- `Canonical.FormatVersion` is 12; `ScenarioContent.Version` is 5 — both
  already bumped by TASK-058, this task bumps `ScenarioContent.Version`
  only, to 6.
- Pathfinding and reservation today have no concept of "another agent's
  current cell" as an obstacle at all (`Pathfinding.fs` has no occupancy
  check; TASK-017's reservation resolves same-tick entry contention
  dynamically). This task's "occupied" fallback check is a one-time,
  order-acceptance-time heuristic against other agents' current `Position`
  only — it does not add a general occupancy model to pathfinding, and does
  not change how TASK-017 reservation itself works.
- No client (Godot) UI names or targets a formation anywhere yet. This task
  is Sim-side only, mirroring TASK-058's own scoping — a scenario author
  (test fixture or `DemoScenario`) exercises it, not a new HUD control.

## Allowed scope

- `src/CommandoWar.Sim/Domain.fs` (a new static, non-canonical per-agent
  slot-offset field on `AgentState`, plus `Agent.create`);
- `src/CommandoWar.Sim/Scenario.fs` (`RawScenario.Formations`, a new
  `RawFormation` type; `RawDeployment`'s new optional formation id + slot
  index; validation errors for an unresolved formation id or an
  out-of-range slot index; `ScenarioContent.Version` 5 -> 6; `Deployment`
  carrying the resolved offset onto `World.ofScenario`);
- `src/CommandoWar.Sim/Appraisal.fs` and/or `Simulation.fs` (a new
  passability+occupancy nearest-cell search analogous to `bestCoverNear`;
  the `Destination`-write site for an `Accepted MoveTo` on a formationed
  agent);
- `src/CommandoWar.Sim/Snapshot.fs` / `Diagnostics.fs` (exposing the
  resolved slot offset / destination redirect — a new `Overlay` case per
  the `AGENTS.md` diagnostics-extension rule, since this is new
  authoritative behaviour affecting spatial state);
- `src/CommandoWar.Headless/DiagnosticRender.fs` (Ascii/Svg rendering of the
  new overlay);
- `content/diagnostics/*` (a new golden covering a formation redirect);
- `content/replays/*`/`CORPUS.md` (a new corpus entry demonstrating two
  agents ordered to one nominal cell landing in distinct slots), or
  `content/fixtures/*` if a corpus entry proves unnecessary — decide by
  inspection of the existing corpus-vs-fixture split, do not add both by
  default;
- `tests/CommandoWar.Sim.Tests/*.fs` (new `ScenarioTests` facts for
  authoring/validation; new `SimulationTests`/`AppraisalTests` facts for
  the redirect and its fallback);
- `docs/04_SIMULATION_SPEC.md`, `docs/05_COMMAND_AND_AGENT_AI.md` (replacing
  the section 17 "squad / formation grouping" deferred-systems line with a
  real description once built);
- control-document updates (this task, `docs/11_BACKLOG.md` B-011d row,
  `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`).

## Forbidden scope

- A new order type, or any multi-agent/group order dispatch. Every order
  still targets exactly one agent.
- Composing formation-offset resolution with `Hold`, `Assault`, `Withdraw`,
  or `Suppress` targets. Those keep their existing target semantics
  unchanged, formationed agent or not.
- Any change to `Pathfinding.fs` itself, or to how TASK-017's same-tick
  reservation contention works.
- A designated "formation leader" concept, or continuous/per-tick
  re-resolution of a formation's shape as members move.
- Any Godot client UI, HUD, or scene change (Sim-side only, the TASK-058
  precedent).
- A new package, project, or dependency.

## Required work

1. Inspect the exact `Destination`-write site (`Simulation.fs` around line
   978) and confirm where a formationed agent's resolved offset must be
   substituted in.
2. Author `RawScenario.Formations` / `RawFormation` (named formation, an
   ordered array of relative `(dx, dy)` offsets) and `RawDeployment`'s
   optional formation id + slot index, mirroring the `UnitTypes`/`Jammers`
   resolve-and-validate pattern exactly (new validation error cases; no
   silent default on an unresolved reference or an out-of-range slot).
   `ScenarioContent.Version` 5 -> 6.
3. Add the resolved static offset to `AgentState`/`Agent.create`, excluded
   from `Canonical.encode` (the `MoveSpeed` precedent).
4. Implement the nearest passable + unoccupied cell search (adapt
   `bestCoverNear`'s shape; a new, simpler scoring key — no threat
   pressure) and wire it into the `Accepted MoveTo` `Destination`-write site
   for a formationed agent only; an unformationed agent's resolution is
   byte-for-byte unchanged.
5. Extend `Diagnostics.fs` with a new `Overlay` case for the resolved slot
   destination (or the redirect when it fires); render it in both
   `DiagnosticRender.Ascii` and `.Svg`.
6. Add a new corpus or fixture entry: two formationed agents both ordered
   to the same nominal cell must resolve to two distinct, non-colliding
   destinations; a third case exercises the blocked/occupied fallback.
   Regenerate and commit the matching `content/diagnostics/*` golden.
7. Add `ScenarioTests` facts for authoring/validation (unresolved formation
   id, out-of-range slot index, valid resolution) and `SimulationTests`/
   `AppraisalTests` facts for the redirect and its fallback.
8. Verify no pre-existing corpus/fixture entry's tick-by-tick sequence
   changes (no formation authored in any of them) — confirm by diff, not
   assumption.
9. Update `docs/04`, `docs/05` section 17, backlog, ledger,
   `PROJECT_STATE.yaml`.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] Two agents in the same formation, both issued `MoveTo` toward the
      same nominal cell, are accepted and end up with two distinct
      `Destination`s matching their authored slot offsets from that cell —
      proven by a `SimulationTests` fact (`Command.moveToMany` to `(5,5)`;
      also demonstrated end to end by the new `formation-slots` corpus
      entry/golden).
- [x] An agent whose exact offset cell is impassable or already occupied by
      another agent's current `Position` resolves to the nearest passable,
      free cell in the search radius instead — proven by two
      `SimulationTests` facts; a completely blocked radius falls back to
      the literal anchor cell, not a hard `Refused`/`Unable` — proven by a
      third fact.
- [x] An unformationed agent's `MoveTo` resolution is byte-for-byte
      unchanged from before this task, on every pre-existing corpus/
      fixture entry (none authors a formation) — confirmed by `cwheadless
      corpus --regenerate` touching only the two new `formation-slots`
      files (`git status --porcelain` on every pre-existing entry clean).
- [x] An unresolved `RawDeployment` formation id, or a slot index outside
      the referenced formation's slot count, is a validation error with no
      silent default — proven by two `ScenarioTests` facts.
- [x] `ScenarioContent.Version` is 6; `Canonical.FormatVersion` is unchanged
      at 12 (the slot offset is static, non-canonical) — confirmed by
      `cwheadless fixture` reporting `format 12`.
- [x] A new `Overlay` case exposes the resolved slot destination /
      redirect, with a committed golden under `content/diagnostics/`
      (`AGENTS.md` diagnostics-extension rule) — `AgentFormationSlot`,
      `formation-slots-tick-001.{ascii.txt,svg}`.
- [x] `dotnet build CommandoWar.slnx -c Release` = 0 warnings, 0 errors.
- [x] `dotnet list src/CommandoWar.Sim package --include-transitive` =
      `FSharp.Core` only; no forbidden dependency entered
      `CommandoWar.Sim`.
- [x] `docs/04`, `docs/11_BACKLOG.md` (B-011d row), `docs/12_PROGRESS_LEDGER.md`,
      `PROJECT_STATE.yaml`, this task file all updated (`docs/05` section 17
      itself named no formation-specific line to update; the stale
      "unstarted" pointer lived in `Perception.fs`'s own comment instead,
      corrected there).

## Required verification

- `dotnet build CommandoWar.slnx -c Release`
- `dotnet test CommandoWar.slnx -c Release` before and after
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
  before and after; `--regenerate` then re-run for idempotence if a new
  corpus entry is added
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture` (if
  the fixture is touched)
- `git diff --stat` on every regenerated corpus/fixture `.md` — confirm
  every pre-existing entry's tick-by-tick sequence is unchanged, only the
  new entry differs
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
- source scan of `src/CommandoWar.Sim` for a forbidden dependency
- `git status`

## Evidence to capture

- command output or test summary;
- the new corpus/fixture entry's `.cwlog`/`.md` and the matching
  `content/diagnostics/*` golden;
- confirmation (diff) that every pre-existing corpus/fixture entry is
  byte-identical apart from any mechanical hash/format footer;
- unresolved failures, if any.

## Expected files

- `src/CommandoWar.Sim/Domain.fs`, `Scenario.fs`, `Appraisal.fs`,
  `Simulation.fs`, `Snapshot.fs`, `Diagnostics.fs`
- `src/CommandoWar.Headless/DiagnosticRender.fs`
- `content/diagnostics/*`, `content/replays/*` or `content/fixtures/*`,
  `CORPUS.md`
- `tests/CommandoWar.Sim.Tests/*.fs`
- `docs/04_SIMULATION_SPEC.md`, `docs/05_COMMAND_AND_AGENT_AI.md`,
  `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`,
  this task file

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md` (B-011d row: `proposed` -> `done`, task reference);
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file);
- `PROJECT_STATE.yaml` (`active_work`, pinned facts if
  `ScenarioContent.Version` is a pinned value);
- `docs/04_SIMULATION_SPEC.md` / `docs/05_COMMAND_AND_AGENT_AI.md` section
  17 (replace the deferred-systems line with the realised design, or narrow
  it to whatever remains deferred — e.g. dynamic/leader-relative formations
  stay deferred).

## Rollback or removal

Purely additive to `Scenario.fs`/`Domain.fs` (new optional table, new
optional per-deployment reference, new static non-canonical field) and one
new branch at the `Accepted MoveTo` `Destination`-write site. Revertible by
reverting the commit; no existing scenario or corpus entry authors a
formation, so nothing pre-existing depends on the new fields.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
