# TASK-030: Commitment and finite move/hold executor

Status: done (implemented 2026-09-13 on branch
`task-030-commitment-and-finite-executor`; central decisions A–H confirmed
with Dave 2026-09-13 before the phase bodies; accepted by Dave and merged to
`main` 2026-09-13)
Owner: Dave
Phase: P3
Gate: G3 (command loop); realises backlog B-018
Size: M

## Outcome (2026-09-13)

Implemented on branch `task-030-commitment-and-finite-executor` off the
TASK-029 merge on `main` (committed locally, not pushed). All five central
decisions confirmed with Dave and implemented as proposed, with one
significant revision found during implementation: Decision B originally
assumed `Commitment` would need new canonical state and another
`Canonical.FormatVersion` bump; building the derivation showed it is fully
recoverable from the already-canonical `Order` / `Disposition` / `Destination`
(the `AgentState.Route` precedent), so it ships as a pure derived value with
**no format bump and no hash re-pin anywhere** — a materially smaller, safer
task than the original framing.

The Commitment and local action phase (12.6) is a real phase
(`Simulation.commitmentAndLocalAction`) in its existing `Phases.order` slot.
`Commitment` (`Holding | Moving of MoveCommitment`) lives in a new leaf
`src/CommandoWar.Sim/Commitment.fs`. The Appraisal phase's fulfilled-order
housekeeping branch relocated there unchanged. New events
`CommitmentEstablished` / `CommitmentCompleted`; new `Overlay.AgentCommitment`
(renamed from this file's original `Overlay.Commitment` during implementation
to avoid a case/type name collision) derived in both `frame` and `frameOf`.
New corpus entry `reissued-order` proves supersession end-to-end.

`244 -> 254` green (+8 `SimulationTests`, +1 `DeterminismPropertyTests`
property 9 at 200 cases, +1 `DiagnosticsTests` golden fact). `dotnet build`
0/0; `-- corpus` 11/11 (`--regenerate` idempotent); `-- fixture` format 4,
hashes unchanged, 34 -> 36 events; `-- replay-file envelope-full` OK at
canonical 4, hashes unchanged, 75 -> 78 events; `src/CommandoWar.Sim`
packages `FSharp.Core` only. No ADR.

Full detail:
`docs/ledger/2026-09-13-TASK-030-commitment-and-finite-executor.md`.

## Objective

Turn the **Commitment and local action phase (12.6)** — currently a no-op arm
in `Simulation.runPhase` (`Phase.CommitmentAndLocalAction`) — into a real phase
that makes "what is this agent currently doing, and why" an explicit, typed,
inspectable fact instead of something inferred from `AgentState.Destination`
being `Some`. Realise the narrowest slice `docs/04` section 12.6 and `docs/05`
sections 9–11 actually support today: a `Commitment` of `Holding | Moving`, a
finite move/hold executor (the state-transition logic that establishes and
ends a `Moving` commitment), and the one interrupt priority current systems
can source (a new order supersedes an in-progress commitment). Stage-5 safer
adaptation (`OrderDisposition.Adapted`, an exposure-aware reroute) and every
interrupt priority that needs combat, suppression, or a stall counter are
explicitly out — see "Central decisions" and "Forbidden scope".

This realises `docs/08` section 6 P3 Required work "implement commitment and
finite execution states" and directly follows TASK-028 (backlog B-017): the
Appraisal phase now produces an `Accepted` disposition; this task is the first
consumer of that fact for something other than writing `Destination`.

## Why this task exists

- `docs/04_SIMULATION_SPEC.md` section 12.6 is a written-but-unrealised phase:
  "accepted orders create or update a commitment; the executor chooses the
  next finite action within that commitment; a small ordered interrupt table
  may supersede the normal action." Until now, "there is no commitment store,
  finite action executor, or interrupt table" — the Appraisal phase writes
  `Destination` directly and Navigation follows it.
- `docs/05_COMMAND_AND_AGENT_AI.md` section 2's pipeline names `commitment ->
  finite action executor -> ordered interrupts` as distinct stages after order
  appraisal; sections 9–11 specify the `Commitment` DU, the finite executor,
  and the seven-priority interrupt table. None exists in code.
- `docs/08_ROADMAP_AND_GATES.md` section 6 lists "implement commitment and
  finite execution states" as P3 Required work, separate from appraisal
  (already realised, TASK-028) and from suppression/stress/trust reappraisal
  (B-021).
- Risk R-007 (AI too broad to debug): the mitigation is "explicit staged
  appraisal, typed commitments, finite executors." TASK-028 delivered the
  first half; this delivers the second — a named `Commitment` value the
  developer trace can point at, not an implicit fact recovered by reading
  `Destination`.
- Backlog B-023 (canonical refusal and correction scenario) and B-030
  (suppress/assault executors) both build on a real commitment/executor layer
  existing first.

Depends only on TASK-028 (order appraisal, done, on `main`). Directly unblocks
the "reissue after a correction" half of B-023 once B-020/B-021 land, and gives
B-030 an existing `Commitment` DU to extend rather than invent from scratch.

## Required reading

Read in this order; verify each path and inspect the source before trusting a
filename (`AGENTS.md`).

1. `PROJECT_STATE.yaml`, `AGENTS.md`.
2. `docs/ledger/2026-09-08-TASK-028-order-appraisal-and-typed-reasons.md` — the
   Appraisal phase's exact housekeeping / fast-path / appraise structure this
   task relocates one branch out of, and the FS0025 discipline (every
   exhaustive match armed with the intended branch, never a wildcard).
3. `docs/04_SIMULATION_SPEC.md` sections 11 (agent state — "current order and
   commitment"), 12.5 (Appraisal — TASK-028, the phase immediately before this
   one), 12.6 (Commitment and local action — the no-op this task realises),
   12.7 (Navigation — reads `Destination`, unchanged by this task), 14 (events),
   17 (canonical hashing — this task adds **no** canonical field; read section
   17's "derived caches either excluded or normalised" argument, the one this
   task's `Commitment` relies on), 20 (invariants).
4. `docs/05_COMMAND_AND_AGENT_AI.md` sections 2 (the pipeline), 5 stage 5
   (safer adaptation — deferred, see Decision C), 6 (`OrderDisposition` —
   `Adapted` gets no case here either), 9 (`Commitment` DU — this task's
   `Holding | Moving` subset), 10 (finite action executor — the assault
   example; this task's executor is far smaller, Move/Hold only), 11
   (interrupts — the seven-priority list; this task realises exactly one),
   13 (developer trace — the new events extend it), 14 (reappraisal triggers
   — unchanged, still "new order received" only).
5. `docs/07_VERTICAL_SLICE.md` section 8 (the canonical refusal sequence —
   step 7 "the player reissues the original intent" is the supersession path
   this task's new corpus entry exercises), section 9 (functional acceptance
   criteria 2, 11).
6. `docs/09_TEST_STRATEGY.md` sections 2.1, 2.3, 8.
7. `docs/10_RISK_REGISTER.md` R-007.
8. `src/CommandoWar.Sim/Phases.fs` — `Phase.CommitmentAndLocalAction`'s
   existing slot (between `Appraisal` and `NavigationAndMovement`).
9. `src/CommandoWar.Sim/Simulation.fs` — `appraisal` (the housekeeping branch
   this task relocates: `Some Accepted when a.Destination = None &&
   a.Position = target -> { a with Order = None; Disposition = None }`),
   `runPhase` (`CommitmentAndLocalAction` currently in the no-op list),
   `navigationAndMovement` (reads `Destination`, untouched), `StepState`
   (`EventsRev` — this task's phase reads this tick's already-emitted events).
10. `src/CommandoWar.Sim/Domain.fs` — `AgentState.Order` / `.Disposition` /
    `.Destination`, `ReceivedOrder`, `OrderDisposition`, `PlayerIntent.MoveTo`.
    This task adds **no field** here — read section 17's derived-cache
    argument on `Route` (lines documenting why `Route` is pure/total/excluded
    from `Canonical.encode`) as the precedent this task's `Commitment` follows.
11. `src/CommandoWar.Sim/Appraisal.fs` — `AppraisalConfig`, `Appraisal.appraise`
    (the pure-helper style this task's `Commitment.fs` matches).
12. `src/CommandoWar.Sim/Events.fs` — `EventBody`, `OrderAppraised` (this
    task's `CommitmentEstablished` is emitted based on this tick's
    `OrderAppraised` events), the `DomainEvent` ordering doc comment.
13. `src/CommandoWar.Sim/Diagnostics.fs` — `Overlay.OrderAppraisal` (derived in
    both `frame` and `frameOf` from bare authoritative state — the precedent
    this task's `Overlay.Commitment` follows), `eventMarker`, the FS0025
    filter-arm precedent (`orderAppraisalOverlays`, `undeliveredOrderOverlays`).
14. `src/CommandoWar.Headless/{DiagnosticRender.fs, Corpus.fs}` — the
    `OrderAppraisal` `Ascii` / `Svg` branches and FS0025 filter arms;
    `Corpus.rawScenario` / `Entry` / `Corpus.all` (the new corpus entry's
    shape — a second `MoveTo` command to an agent already under an accepted
    order).
15. `tests/CommandoWar.Sim.Tests/{SimulationTests.fs, DeterminismPropertyTests.fs,
    CorpusTests.fs, DiagnosticsTests.fs}` — the appraisal facts this task adds
    beside, `perceptionCaseGen`-style generator precedent.
16. `content/replays/CORPUS.md` + the ten `.md` entries + `envelope-full.md`;
    `content/fixtures/SPIKE-FIXTURE.md`; a `content/diagnostics/*` golden with
    an `OrderAppraisal` overlay (the format this task's new goldens match).

## Dependencies

- TASK-028 (order appraisal and typed reasons, done, on `main`).

## Central decisions (confirmed with Dave 2026-09-13)

### Decision A — `Commitment` DU subset: `Holding | Moving of MoveCommitment` only — CONFIRMED

`docs/05` section 9 lists five cases (`Moving | Holding | Suppressing |
Assaulting | Withdrawing`). Only `MoveTo` exists as a `PlayerIntent`
(`Hold`/`Suppress`/`Assault`/`Withdraw` are new intents, backlog B-030) and no
combat/suppression state exists yet (B-019/B-020), so `Suppressing` /
`Assaulting` / `Withdrawing` have no `PlayerIntent` or world state to build
from — adding them now is exactly the "speculative type machinery" `AGENTS.md`
forbids. `Holding` covers idle-at-start, arrived, refused, and unable: an agent
with no order in flight.

```fsharp
/// A commitment's move-specific payload (docs/05 section 9). `Command`
/// identifies which delivered order this commitment realises (so a superseding
/// order — a different Command — is distinguishable from the same order
/// continuing); `Target` mirrors AgentState.Destination for the duration of
/// the commitment (both are written from the same Appraisal-Accepted target).
type MoveCommitment = { Command: CommandId; Target: Cell }

/// What an agent is currently committed to (docs/04 section 12.6, docs/05
/// section 9). TASK-030 subset: only the two cases current systems can
/// produce or act on. `Suppressing` / `Assaulting` / `Withdrawing` need
/// PlayerIntent cases that do not exist (Hold / Suppress / Assault / Withdraw
/// — backlog B-030) and get no case, per AGENTS.md "do not build speculative
/// type machinery".
type Commitment =
    | Holding
    | Moving of MoveCommitment
```

### Decision B — `Commitment` is a pure derived value, NOT new canonical state; no `Canonical.FormatVersion` bump — CONFIRMED (revises the initial framing discussed with Dave 2026-09-12)

`Commitment` is fully recoverable, every tick, from fields already canonical:

```fsharp
[<RequireQualifiedAccess>]
module Commitment =
    /// Derives the agent's current commitment from its order-appraisal state
    /// (docs/04 section 12.6). Pure and total: `Moving` iff the agent holds an
    /// order, it was Accepted, and a destination is still outstanding;
    /// `Holding` otherwise (no order, a Refused/Unable order, or an Accepted
    /// order already fulfilled — Destination cleared by the Appraisal phase's
    /// housekeeping, relocated to `commitmentAndLocalAction` by TASK-030).
    let ofAgent (order: ReceivedOrder option) (disposition: OrderDisposition option) (destination: Cell option) : Commitment =
        match order, disposition, destination with
        | Some o, Some Accepted, Some target -> Moving { Command = o.Command; Target = target }
        | _ -> Holding
```

This is the identical argument that keeps `AgentState.Route` out of
`Canonical.encode` (`docs/04` section 17 "derived caches either excluded or
normalised"): `Order`, `Disposition`, and `Destination` are already canonical
and already carry every bit of memory `Commitment` needs (an `Accepted`
disposition persists with its target until fulfilled or superseded — TASK-028
built exactly that memory). Storing `Commitment` again would duplicate state
that can silently drift from its source fields; deriving it on demand cannot
drift by construction. **No new `AgentState` field, no `Canonical.FormatVersion`
bump, no hash re-pin of any corpus entry or the fixture** — a materially
smaller and lower-risk task than TASK-028's four-field, full-re-pin shape.

`Commitment.ofAgent` lives in a new leaf `src/CommandoWar.Sim/Commitment.fs`
(after `Appraisal.fs`), used by both the new phase (to decide which events to
emit) and `Diagnostics` (to derive the overlay from bare state — the
`OrderAppraisal` precedent).

### Decision C — stage-5 safer adaptation stays out of this task — CONFIRMED

The only reachable adaptation (`docs/05` section 5 "use a less exposed
route") needs a second, genuinely new algorithm — an exposure-aware route
search distinct from `Pathfinding.findWithin`'s shortest-path search — plus a
mechanism for Navigation to follow a pinned/adapted route instead of its
normal recompute. That is a second nontrivial piece of work layered on top of
the commitment/executor/interrupt work below, and mixes two concerns in one
task. `OrderDisposition` gains no `Adapted` case; `TacticalAdaptation` is not
created. Left named, not built, for a follow-up (a new B-018b, or folded into
whichever task first needs it).

### Decision D — interrupt table: exactly one realised priority, no separate `Interrupt` type — CONFIRMED

`docs/05` section 11 lists seven priorities. Of these:

- **1 (dead/incapacitated), 2 (explosive danger), 3 (point-blank threat), 4
  (heavy suppression)** — need combat/suppression state that does not exist
  (B-019/B-020). No case.
- **5 (route invalidated)** — TASK-028's Decision E already named this and
  assigned it to B-021 explicitly ("needs a stall counter — new state"). Under
  static terrain, an Appraisal-`Accepted` route cannot later become
  unreachable (`Pathfinding` is deterministic over immutable `Terrain`), so
  there is no live signal for this priority yet; a persistent
  `MovementObstructed` stall is a perception/appraisal concern per TASK-022's
  own note, not built here.
- **6 (new higher-priority command)** — the only priority with a real signal
  today, and it needs no separate `Interrupt` type or `Superseded` outcome
  case: a new order's `Command` differs from the current `Moving` commitment's
  `Command` by construction (`Communication` always writes a fresh
  `AgentState.Order` and resets `Disposition` on any new delivery — TASK-027 /
  TASK-028). Because `Commitment` is derived (Decision B), the old commitment
  simply stops being produced the instant `Commitment.ofAgent` is evaluated
  against the new `Order`/`Disposition`/`Destination` — there is nothing to
  "interrupt" as a side effect, only a new `CommitmentEstablished` to announce
  (Decision F). No event reports the loss of the old commitment; the new
  `OrderAppraised` + `CommitmentEstablished` pair for the new order is the
  complete, sufficient trace.
- **7 (normal commitment execution)** — the default: `NavigationAndMovement`
  continues to drive movement toward `Destination` exactly as before.

The interrupt table is realised as a doc comment on the new phase function
enumerating all seven priorities and which one is live, not as a DU or a
lookup structure with six dead cases — matching `AGENTS.md`'s "do not build
speculative type machinery" and TASK-028's identical treatment of the
reappraisal-trigger list.

### Decision E — realised as its own phase function in the existing `CommitmentAndLocalAction` slot — CONFIRMED

`Phases.order` already reserves `CommitmentAndLocalAction` between `Appraisal`
and `NavigationAndMovement` (`Phases.fs`); every other realised doc section
(12.3, 12.4, 12.5) got its own phase function in its own slot. This task adds
`Simulation.commitmentAndLocalAction` and removes `CommitmentAndLocalAction`
from `runPhase`'s no-op list, rather than folding the logic into `appraisal`.

**One small relocation out of `appraisal`:** its existing housekeeping branch
(`Some Accepted when a.Destination = None && a.Position = target -> { a with
Order = None; Disposition = None }`) is conceptually a *commitment ending*
(12.6), not an *appraisal* (12.5) concern. It moves, unchanged in condition and
effect, into the new phase — behaviour-neutral (same tick, same fields
written, nothing runs between the two phases that reads `Order`/`Disposition`
for a fulfilled agent) but now also emits `CommitmentCompleted` (Decision F),
which it could not do from inside `appraisal` without `appraisal` reaching
into the next phase's vocabulary.

### Decision F — two new events, no `CommitmentOutcome` DU — CONFIRMED

```fsharp
/// A fresh commitment began this tick (docs/04 section 12.6; docs/05 section
/// 9). Emitted by commitmentAndLocalAction exactly when this tick's
/// OrderAppraised for (agent, command) was Accepted — covers both "from
/// Holding" and "supersedes an in-progress Moving commitment" (Decision D):
/// the prior commitment, if any, simply stops being derived, so no separate
/// event reports its end.
| CommitmentEstablished of agent: AgentId * command: CommandId * target: Cell
/// The agent's Moving commitment for command ended this tick because it
/// reached its target (docs/04 section 12.6). Relocated from the Appraisal
/// phase's fulfilled-order housekeeping (TASK-028) — same condition, same
/// fields cleared, now named and inspectable. Never emitted for a superseded
/// or refused commitment (Decision D — nothing to report there).
| CommitmentCompleted of agent: AgentId * command: CommandId * at: Cell
```

Ordering: both are a new event group, emitted after `OrderAppraised`
(Appraisal runs first) and before movement outcomes (`NavigationAndMovement`
runs next), ascending by agent id — `CommitmentCompleted` before
`CommitmentEstablished` within an agent's own pair is impossible (an agent
cannot complete and establish in the same tick: completion requires
`Order = None` this tick, establishment requires `Order = Some`).

### Decision G — no ADR — CONFIRMED

No canonical-image change (Decision B), so the ADR-0002 amendment does not
apply here the way it did for TASK-026/027/028. Phase order is unchanged
(`CommitmentAndLocalAction` is already in `Phases.order`). `docs/08` section 6
already lists this as P3 Required work. Design recorded in this task file and
the ledger.

### Decision H — one new corpus entry proving supersession — CONFIRMED

New entry `reissued-order`: one friendly given a `MoveTo` order on tick 1 to a
distant cell (Accepted, clear route, no threats — `CommitmentEstablished` on
tick 1), then a second `MoveTo` order to a different cell issued to the same
agent on a later tick while it is still mid-route. The second order resets
`Disposition` (TASK-027/028 "new order received" trigger), Appraisal
re-accepts against the new target, and `commitmentAndLocalAction` emits a
fresh `CommitmentEstablished` for the second command — with no event reporting
the first commitment's end, per Decision D. This is `docs/07` section 8 step 7
("the player reissues the original intent") exercised end-to-end for the first
time, and the G3 evidence that a new order visibly supersedes an
in-progress one.

## Phase bodies

### `appraisal` (12.5) — one branch removed

Remove the `Some Accepted when a.Destination = None && a.Position = target`
housekeeping branch (and its `{ a with Order = None; Disposition = None }`
write). The remaining two branches (`Some _ -> ()` fast path; `None ->` fresh
appraisal) are unchanged. Update the phase header comment: "commitment
housekeeping relocated to `commitmentAndLocalAction` (TASK-030, backlog
B-018)." No other change to `appraisal` or `Appraisal.fs`.

### `commitmentAndLocalAction` (12.6) — new phase, header "Realised by TASK-030"

Runs after `appraisal`, before `navigationAndMovement`. Reads this tick's
already-emitted events (`s.EventsRev`) to know which agents Appraisal freshly
accepted this tick — the phase-boundary signal Decision F's `CommitmentEstablished`
timing depends on.

```fsharp
let private commitmentAndLocalAction (s: StepState) =
    let acceptedThisTick =
        s.EventsRev
        |> List.choose (function
            | OrderAppraised(agent, _, Accepted) -> Some agent
            | _ -> None)
        |> Set.ofList

    let agents = Array.copy s.Agents

    for i in 0 .. agents.Length - 1 do
        let a = agents.[i]

        match a.Order, a.Disposition with
        | Some o, Some Accepted ->
            let (MoveTo target) = o.Intent

            if a.Destination = None && a.Position = target then
                // Fulfilled: relocated from `appraisal`'s prior housekeeping.
                agents.[i] <- { a with Order = None; Disposition = None }
                emit (CommitmentCompleted(a.Id, o.Command, a.Position)) s
            elif Set.contains a.Id acceptedThisTick then
                // Freshly accepted this tick — a commitment begins (Decision
                // D: this covers both "from Holding" and "supersedes a prior
                // Moving commitment" with the same event).
                emit (CommitmentEstablished(a.Id, o.Command, target)) s
            // else: a Moving commitment continues unchanged; no event
            // (the fast-path precedent — an unchanged state emits nothing).
        | _ -> ()
        // Order = None, or Disposition = Some (Refused | Unable): Holding.
        // Nothing to establish or complete; no event.

    s.Agents <- agents
```

`runPhase` — `CommitmentAndLocalAction -> commitmentAndLocalAction s`; remove
`CommitmentAndLocalAction` from the no-op list (`Combat` / `StateConsequences`
/ `Mission` stay no-ops).

## Diagnostics

`AGENTS.md`'s diagnostic-extension rule applies in spirit even though no new
`AgentState` field is added: two new event kinds and a named `Commitment`
concept are new inspectable tactical state. Required:

- **`Overlay.AgentCommitment of agent: AgentId * at: Cell * commitment:
  Commitment`** (named `AgentCommitment`, not `Commitment`, to avoid a
  case/type name collision with the `Commitment` type it wraps — found during
  implementation) — one per agent, derived by **both** `Diagnostics.frame` and
  `frameOf` via `Commitment.ofAgent a.Order a.Disposition a.Destination` (the
  `OrderAppraisal` precedent: bare state carries everything the derivation
  needs). Doc-comment reservation line: "B-018 commitment -> `Commitment`
  (realised by TASK-030)".
- `eventMarker` gains `CommitmentEstablished _ -> { Kind = "commitment-established"; Cells = [||] }`
  and `CommitmentCompleted(_, _, at) -> { Kind = "commitment-completed"; Cells = [| at |] }`.
- `DiagnosticRender.Ascii`: a `commitment (x,y): agent N <holding | moving to (x,y)>`
  overlay text line; the existing `sightRays` / `plannedPaths` /
  `orderAppraisals`-style exclusion filters gain `| AgentCommitment _ -> None`.
- `DiagnosticRender.Svg`: a small marker distinct from `OrderAppraisal`'s
  disposition glyph — a filled circle (`Holding`) or a hollow circle
  (`Moving`) at the agent's cell, deliberately not duplicating the
  `PlannedPath` polyline or the `OrderAppraisal` `A`/`R`/`U` glyph already
  drawn there.
- Every exhaustive `EventBody` / `Overlay` match armed with the intended
  branch (never a wildcard): `Diagnostics` (`orderAppraisalOverlays`-style
  filters), `DiagnosticRender` (both `Ascii` filters, `Ascii` text, `Svg`),
  `Program.describeReplayError` / any event-describe, and the test-project
  overlay matches (`DiagnosticsTests`). FS0025 sites + fixes listed in the
  ledger (TASK-026/027/028 style).
- Goldens: the new `reissued-order-tick-0N.*` (Decision H, the tick the second
  order is delivered and re-accepted) + a hand-built `Commitment` overlay unit
  test (`OrderAppraisal` precedent); the regeneration note in
  `content/diagnostics/README.md`.

## Event-trace impact (no canonical hash change)

No `Canonical.FormatVersion` bump and no state-hash change on any existing
entry (Decision B — `Commitment` is not written to `Canonical.encode`). Every
existing corpus entry, the fixture, and `envelope-full` **do** gain new events
in their trace wherever an order is `Accepted` (a `CommitmentEstablished`) or
an agent arrives (a `CommitmentCompleted`) — this is the honest, expected
surface of realising the phase, not a regression. Required verification:

- Regenerate every `content/replays/*.md` "Domain events" count and listing;
  record old -> new count per entry in the ledger (a table, TASK-028 style).
  **State hashes (initial and every per-tick hash) must be byte-identical
  before and after** — any hash change is stop-and-report.
- `content/fixtures/SPIKE-FIXTURE.md` — same treatment (hash unchanged; event
  count/listing updated).
- `content/replays/envelope-full.{cwreplay,md}` — `checkpoint` hash lines
  unchanged; event count/listing updated.
- `content/diagnostics/*` goldens — regenerate; event markers gain the new
  kinds wherever applicable; footer hash unchanged (no format-version line to
  bump, since `Canonical.FormatVersion` does not move).
- `cwheadless corpus` / `cwheadless fixture` / `cwheadless replay-file
  envelope-full.cwreplay` all still pass at unchanged hashes; re-running
  `corpus --regenerate` is a zero diff on hashes (event-count lines will
  legitimately differ from the pre-task committed files until this task
  updates them, then zero diff after).

## Allowed scope

- `src/CommandoWar.Sim/Commitment.fs` (**new leaf**, after `Appraisal.fs`) —
  `MoveCommitment`, `Commitment`, `Commitment.ofAgent`. Pure, total,
  `Domain` only, no event emission, no mutation.
- `src/CommandoWar.Sim/Simulation.fs` — remove the housekeeping branch from
  `appraisal`; add `commitmentAndLocalAction`; `runPhase` arm; header
  comments.
- `src/CommandoWar.Sim/Events.fs` — `CommitmentEstablished`,
  `CommitmentCompleted`; `DomainEvent` ordering doc comment update.
- `src/CommandoWar.Sim/Diagnostics.fs` — `Overlay.AgentCommitment` (renamed
  from `Overlay.Commitment` during implementation to avoid a case/type name
  collision); `frame` + `frameOf` derivation; `eventMarker` arms; the
  `Overlay` doc comment; FS0025 filter arms.
- `src/CommandoWar.Headless/AppraisalDemo.fs` (**not originally listed**;
  required because it has an exhaustive `Overlay` match with no
  `TreatWarningsAsErrors` guard in the test project — silently incomplete
  without this arm) — one `AgentCommitment` arm added to its `unhandled`
  bucket (this disposable P3 demo predates TASK-030 and does not render
  commitments); `tests/.../DiagnosticsTests.fs`'s corresponding
  `UnhandledOverlays` assertion updated from empty to 3 entries (2 friendlies
  + 1 hostile in `exposed-approach`).
- `src/CommandoWar.Headless/DiagnosticRender.fs` — new `Ascii` / `Svg`
  branches + filter arms.
- `src/CommandoWar.Headless/Corpus.fs` — the new `reissued-order` `Entry`;
  `Corpus.all` row; `CORPUS.md` row.
- `src/CommandoWar.Headless/Program.fs` — a `CommitmentEstablished` /
  `CommitmentCompleted` arm only if an exhaustive `EventBody` match exists
  there.
- `content/replays/` — event-count/listing updates on all ten existing
  entries + `envelope-full.{cwreplay,md}` + `CORPUS.md`; the new
  `reissued-order.{cwlog,md}`.
- `content/fixtures/SPIKE-FIXTURE.md` — event-count/listing update.
- `content/diagnostics/` — event-marker updates on affected goldens; new
  `reissued-order-tick-0N.*`; `README.md`.
- `tests/CommandoWar.Sim.Tests/` — see "Acceptance criteria".
- Docs — see "Documentation updates".

## Forbidden scope

- `OrderDisposition.Adapted`, a `TacticalAdaptation` type, an exposure-aware
  reroute search, any Navigation change to follow a pinned/adapted route —
  stage-5 safer adaptation, deferred (Decision C).
- `Delayed`, a `ResumeCondition` mechanism — B-021.
- Interrupt priorities 1–5 (dead/incapacitated, explosive danger, point-blank
  threat, heavy suppression, route invalidated) — B-019 / B-020 / B-021
  (route invalidated specifically needs the stall counter TASK-028 already
  assigned to B-021).
- `Commitment` cases `Suppressing` / `Assaulting` / `Withdrawing`, or any new
  `PlayerIntent` case (`Hold` / `Suppress` / `Assault` / `Withdraw`) — B-030.
- Any `AgentState` field addition or `Canonical.FormatVersion` change
  (Decision B is the point of this task's smaller shape).
- Any `Appraisal.fs` / `Pathfinding.fs` / `Sight.fs` / `Terrain.fs` change
  beyond the one housekeeping-branch removal from `Simulation.appraisal`
  named above.
- A hostile squad tactical picture, enemy doctrine — B-022.
- Editing the client spikes, `src/_scratch`, `bench/`,
  `content/benchmarks/BASELINE.md` (a post-regeneration glance only).
- An ADR (Decision G).

## Acceptance criteria

- [x] `CommitmentAndLocalAction` is a real phase function in `Simulation.fs`,
      in its existing `Phases.order` slot, with a "Realised by TASK-030"
      header comment; `runPhase` has a real arm and it is out of the no-op
      list.
- [x] `Simulation.appraisal`'s fulfilled-order housekeeping branch is removed;
      the identical condition and field-clear now live in
      `commitmentAndLocalAction`, which additionally emits `CommitmentCompleted`.
- [x] `src/CommandoWar.Sim/Commitment.fs`: `MoveCommitment`, `Commitment`
      (`Holding | Moving of MoveCommitment`), `Commitment.ofAgent` — pure,
      total; no `AgentState` field added; no `Canonical.encode` change; a
      focused test proves `Commitment.ofAgent` matches every
      `(Order, Disposition, Destination)` combination the phase can produce.
- [x] `CommitmentEstablished of agent * command * target` and
      `CommitmentCompleted of agent * command * at` events; `DomainEvent`
      ordering doc comment updated (after `OrderAppraised`, before movement,
      ascending agent id). Every exhaustive `EventBody` match armed; FS0025
      sites + fixes in the ledger.
- [x] `SimulationTests` facts:
  - a fresh clear-route `Accepted` order emits `OrderAppraised(Accepted)` then
    `CommitmentEstablished` for the same `(agent, command, target)` the same
    tick;
  - an agent that arrives emits `CommitmentCompleted(agent, command, at)` and
    `Order`/`Disposition` clear to `None`, exactly as before TASK-030 (a
    regression fact confirming the relocation is behaviour-neutral);
  - a second order delivered mid-route to an already-`Moving` agent emits a
    fresh `CommitmentEstablished` for the new command, with no
    `CommitmentCompleted` or any other event reporting the first commitment's
    end;
  - a `Refused` or `Unable` order never emits `CommitmentEstablished`;
  - an unchanged, already-appraised order (the Appraisal fast path) emits
    neither new event;
  - two runs of the same world + commands emit byte-identical events + hashes.
- [x] A `DeterminismPropertyTests` property (`MaxTest = 200`, reusing the
      `Appraisal` property 8 generator as-is — a reissued-order variant was
      judged unnecessary: the supersession path is already covered by a
      focused `SimulationTests` fact, and the property's job is the
      derivation invariant): `Commitment.ofAgent` applied to every post-tick
      agent state always equals `Moving` iff `Order = Some _ && Disposition =
      Some Accepted && Destination = Some _`; `Random.Draws` unchanged (no
      PRNG draw). Properties 1–8 unmodified.
- [x] New corpus entry `reissued-order` (Decision H): `CORPUS.md` row,
      committed `.cwlog` + `.md`, passes `CorpusTests` `[<Theory>]` and
      `cwheadless corpus`.
- [x] No `Canonical.FormatVersion` change; **no state hash differs** from the
      pre-task committed value on any of the ten existing entries, the
      fixture, or `envelope-full` — verified by diffing hash columns only
      (event-count/listing columns are expected to change; documented per
      entry in the ledger).
- [x] Diagnostics: `Overlay.AgentCommitment` (renamed from `Overlay.Commitment`
      during implementation, see Allowed scope) derived in `frame` and
      `frameOf`, rendered in `Ascii` + `Svg`, covered by a hand-built
      `DiagnosticsTests` fact and the committed `reissued-order-tick-003.*`
      golden.
- [x] `dotnet build CommandoWar.slnx -c Release` = 0/0; `dotnet list
      src/CommandoWar.Sim package --include-transitive` = `FSharp.Core` only;
      source scan of `src/CommandoWar.Sim` clean (`float` / `Stopwatch` /
      `DateTime` / `System.Random` / `godot`).
- [x] `dotnet test CommandoWar.slnx -c Release` green — `244 -> 254` (+8
      `SimulationTests`, +1 `DeterminismPropertyTests` property 9 at 200
      cases, +1 `DiagnosticsTests` golden fact).
- [x] Docs updated (see below).

## Required verification

- `dotnet build CommandoWar.slnx -c Release` (0/0).
- `dotnet test CommandoWar.slnx -c Release` before any edit (`244`) and after
  (new count; each added fact + the property's case count named).
- `cwheadless corpus` + `cwheadless fixture` before any edit (record hashes /
  event counts) and after (unchanged hashes; updated event counts/listings
  per entry — a table in the ledger); re-running `--regenerate` is a zero
  diff once the committed `.md` files are updated.
- Regenerate every affected `content/diagnostics/` golden; confirm only the
  new event-marker / overlay lines differ, footer hash unchanged.
- `cwheadless replay-file content/replays/envelope-full.cwreplay` — checkpoint
  hashes unchanged from before this task.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive` — `FSharp.Core` only.
- source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`.
- `git status --porcelain` — matches "Allowed scope"; nothing under the client
  spikes, `src/_scratch`, `bench/`, `content/benchmarks/BASELINE.md`.

## Evidence to capture

- test summary (before/after counts); the new `SimulationTests` facts and the
  property by name + case count;
- confirmation that no state hash moved on any existing entry (before/after
  hash table) alongside the event-count/listing changes that did occur;
- the new `reissued-order` entry's hash/tick/event count and the supersession
  it exercises (`CommitmentEstablished` twice, no end-of-commitment event for
  the first);
- the relocated housekeeping branch's behaviour-neutral confirmation (the
  regression fact);
- the diagnostics golden;
- the FS0025 sites with their fixes.

## Rollback or removal

`Commitment.fs`, the `commitmentAndLocalAction` phase, `CommitmentEstablished`
/ `CommitmentCompleted`, the overlay/renderer branches, the `reissued-order`
entry, and the tests are additive. Reverting: restore `appraisal`'s
housekeeping branch, return `CommitmentAndLocalAction` to a no-op arm, delete
`Commitment.fs` / the events / the overlay / the entry / the goldens / the
tests. No canonical or format-version rollback needed (none was made).

## Documentation updates

- this task file (Status `draft -> review`, acceptance boxes);
- `docs/11_BACKLOG.md`: TASK-030 row (`review`); B-018 `proposed -> review`
  (flips to `done` only when Dave accepts and merges);
- `docs/12_PROGRESS_LEDGER.md`: index row + `docs/ledger/` detail file; the
  "Pinned facts" block (including "Green tests") is left at its `main` values
  until acceptance, per the same rule;
- `docs/04_SIMULATION_SPEC.md` sections 11 (`AgentState`'s "current
  commitment" realised as a derived value, not a stored field — note why),
  12.6 (Commitment and local action realisation block), 14
  (`CommitmentEstablished` / `CommitmentCompleted` realised);
- `docs/05_COMMAND_AND_AGENT_AI.md` sections 9 (`Commitment` subset realised),
  10 (the finite move/hold executor — deliberately thin: establish/continue/
  complete, no intermediate states, because Navigation already owns the
  physical stepping), 11 (interrupt table — priority 6 realised, 1–5 named and
  deferred with their owning backlog items);
- `docs/07_VERTICAL_SLICE.md` section 8 (step 7, "the player reissues the
  original intent", now exercised end-to-end by `reissued-order`);
- `docs/09_TEST_STRATEGY.md` section 2.1 (commitment establish/complete/
  supersede realised);
- `PROJECT_STATE.yaml` `active_work`;
- no ADR (Decision G).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
