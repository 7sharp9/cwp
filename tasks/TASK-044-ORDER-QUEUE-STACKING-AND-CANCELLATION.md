# TASK-044: Order queue — multi-waypoint stacking and cancellation

Status: done (accepted by Dave 2026-09-17, "no new UI to review, just accept")
Owner: Dave
Phase: P4
Gate: G4 (vertical slice feature-complete)
Size: L (backlog estimate M–L; the TASK-039/043 precedent of a queue/Canonical
change landing bigger in practice than a client-only task)

## Outcome (2026-09-17)

Implemented as drafted, plus one deviation. `AgentState.OrderQueue:
ReceivedOrder list` (new canonical field, insertion order, not sorted).
`PlayerCommand.Intent` renamed `.Body: PlayerCommandBody` (`Order of intent *
QueueMode | Cancel of target`); `PlayerIntent`/`ReceivedOrder.Intent`
themselves unchanged, so `Appraisal.fs` and `Commitment.fs` needed **zero**
changes, confirming the design's own prediction. `commandIntake`/
`communication` (`Simulation.fs`) extended for append/replace/cancel-and-
promote, comms-gated identically to every existing order; `commitmentAndLocalAction`
promotes the queue head on `MoveTo` fulfilment, judged the *following* tick
(a documented one-tick gap, since Appraisal already ran this tick), distinct
from a `Cancel`-driven promotion (which happens in `communication`, before
this same tick's Appraisal, so it is judged immediately). New `OrderQueued`/
`OrderCancelled` events; new sparse `AgentOrderQueue` diagnostic overlay
(`Ascii`/`Svg`/`Html`, AGENTS.md's diagnostics rule). `ReplaySerialisation.fs`
(`.cwreplay`) gained `queue-move`/`queue-suppress`/`cancel` keywords (the
legacy `.cwlog` deliberately untouched, Central decision 4) so the new corpus
entry can round-trip through `--regenerate`. New corpus entry
`order-queue-stacking-and-cancellation` (one friendly agent, 9 ticks) proves
stacking, both cancel outcomes (`wasActive` true/false), and the promotion-gap
distinction end to end, plus a new golden diagnostic pair at tick 1.
`Canonical.FormatVersion` 8 -> 9; every pre-existing corpus entry, the shared
fixture, and `envelope-full` re-pinned byte-layout-only (`--regenerate` twice
byte-identical; tick/event counts unchanged everywhere except the new entry).
`Simulation.step`'s public signature is unchanged, as designed — `Cancel`
travels through the same `commands` array as every other command.

**Deviation found during implementation** (not in the original design):
cancelling an agent's active order with an empty queue behind it left
`AgentState.Destination` untouched, since `NavigationAndMovement` reads it
independently of `Order` — the agent would have kept walking toward the
cancelled target forever, defeating the whole point of `Cancel`. Caught while
tracing the new corpus entry's own story (the agent did not actually stop)
before it reached a test. Fixed in the same `communication` branch that
clears `Order`/`Disposition` on a `Cancel` matching the active order (the
`Appraisal`-overwrite precedent for a superseding `Replace` order, applied
explicitly since a `Cancel`-driven promotion bypasses `Appraisal` this same
tick); covered by a new dedicated `SimulationTests` fact.

Both Godot client scenes' pinned `--selfcheck` hashes moved with the
`FormatVersion` bump (`src/FSharpSceneHost.cs`, `src/CommandoWar.Client.Godot/README.md`):
`SnapshotDemo.tscn` `0x11B06E6EDE0C52E3 -> 0xC68F993BC605313C`, `CommandDemo.tscn`
`0x649FA4D08E2931CA -> 0xD27E623504262CE9` — both re-verified `MATCH` through
the real Godot 4.7.2 editor, headless, after re-pinning. No new client-facing
queue/cancel UI (Forbidden scope); the only client touches are the mechanical
`.Body` compile fix in `CommandDemoScene.fs` and a queue-depth addition to the
existing `[dev]` HUD line (`RenderShared.devAgentText`, the TASK-043
precedent) plus the re-pinned hash constants above.

`dotnet build CommandoWar.slnx -c Release`: `0/0`. `dotnet build` the Godot
client `.slnx` (Debug and Release) and the Mibo client `.slnx`: `0/0` each.
`dotnet test`: `306/306` (297 pre-existing + 9 new: five append/replace/
cancel/promotion/rejection facts, one `Destination`-clearing fact, two new
`DiagnosticsTests` facts for the new corpus entry). `dotnet list
CommandoWar.Sim` package: `FSharp.Core` only.

Full detail: `docs/ledger/2026-09-17-TASK-044-order-queue-stacking-and-cancellation.md`.

## Objective

Let a player stack several orders behind one agent's currently active order
(any mix of `MoveTo`/`Suppress`, delivered one at a time as each predecessor
ends) instead of every new order always superseding the last, and let a
specific queued or currently-active order be cancelled by naming its
`CommandId`, without touching any other queued order. Sim-side only: an
authoritative `AgentState.OrderQueue`, not client-side sequencing, per the
design fork already resolved on backlog row B-051.

## Why this task exists

B-051's dependency (B-026, TASK-040) is `done`. Its own central design fork —
client-side sequencing vs. an authoritative sim-side order queue — was
resolved 2026-09-17 via `AskUserQuestion` in favour of the sim-side queue,
recorded in the B-051 backlog row; no task file existed yet. Selected as the
next task via `AskUserQuestion` over B-030 proper, B-031, and B-053.
`docs/04_SIMULATION_SPEC.md` section 2 itself already anticipated this
("`RecordedCommand.Tick` ... owned by whoever schedules the run ... later a
client command queue") but the resolved fork places the queue in
`WorldState`/`AgentState`, not a client-owned schedule.

## Central decisions (confirmed with Dave 2026-09-17 before drafting)

Four forks, put via `AskUserQuestion`:

1. **Any order type can be queued**, not just `MoveTo` waypoints: `MoveTo` and
   `Suppress` share the existing `ReceivedOrder` envelope shape already, so a
   general per-agent FIFO order queue costs little more than a `MoveTo`-only
   one and matches the resolved backlog row's "order queue" wording (not
   "waypoint queue").
2. **Replace stays the default; append is opt-in.** A bare new command still
   fully supersedes an agent's active order and clears its queue, exactly as
   today (`AGENTS.md` "preserve existing behaviour unless the task requires
   changing it") — every existing test, corpus entry, and replay is
   unaffected. A new explicit `QueueMode.Append` (reached through a new
   `Command.queued` combinator) is how a caller asks to stack instead.
3. **Cancellation targets a specific `CommandId`**, queued or active, not a
   blunt "clear everything" operation — matches the backlog row's "cancel a
   queued or in-progress order" wording exactly. Cancelling the active order
   promotes the next queued order (if any); cancelling a queued order removes
   only that one entry, leaving the rest of the queue and the active order
   untouched.
4. **The legacy `.cwlog` text fixture grammar is not extended** for
   queue/cancel — the `Command.moveToMany` precedent ("exercised only from
   code ... the legacy `.cwlog` fixture grammar ... cannot express"). `.cwlog`
   stays one-recipient-per-line, always-replace. (The newer `.cwreplay`
   grammar, `ReplaySerialisation.fs`, is a different, already-code-adjacent
   format the corpus's own `--regenerate` writes and round-trips; see Design
   below for why it still needs a small, additive grammar extension — a
   mechanical implementation detail Dave's fork did not need to cover, since
   it was framed around the legacy format.)

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `docs/04_SIMULATION_SPEC.md` sections 2, 11, 12.1 (command intake), 12.2
  (communication), 12.5 (appraisal), 12.6 (commitment), 16 (recorded commands),
  17 (canonical encoding), 20 (invariants)
- `docs/05_COMMAND_AND_AGENT_AI.md` sections 5-9, 11 (interrupt priority 6,
  "new higher-priority command"), 14, 16
- `src/CommandoWar.Sim/Domain.fs` — `PlayerIntent`, `ReceivedOrder`,
  `AgentState` (`Order`, `Disposition`, `Destination`), `Agent.create`
- `src/CommandoWar.Sim/Commands.fs` — `PlayerCommand`, `CommandRejection`,
  `Command.moveTo`/`.suppress`/`.moveToMany`
- `src/CommandoWar.Sim/Commitment.fs` — confirm no change is needed:
  `Commitment` is derived from `(Order, Disposition, Destination)` only; a
  queue behind the active order does not change what the *current* commitment
  is, only what happens after it ends
- `src/CommandoWar.Sim/Appraisal.fs` — confirm no change is needed: it reads
  one `ReceivedOrder` at a time, exactly as today; the queue never reaches it
  directly
- `src/CommandoWar.Sim/Simulation.fs` — `commandIntake`, `communication`,
  `appraisal`, `commitmentAndLocalAction` (read the large comment blocks
  above each; they explain the exact phase-ordering invariants this task must
  not break)
- `src/CommandoWar.Sim/Canonical.fs` — `writeAgent`, `writeOrder`,
  `FormatVersion` history (the pattern for documenting a new bump)
- `src/CommandoWar.Sim/Events.fs`, `src/CommandoWar.Sim/Diagnostics.fs`
  (`Overlay`, `frame`/`frameOf`) — the diagnostics rule below
- `src/CommandoWar.Sim/ReplaySerialisation.fs` (`parseIntent`, `parseCommand`,
  `serialise`) and `src/CommandoWar.Sim/Replay.fs` (`RecordedCommand`)
- `src/CommandoWar.Headless/Corpus.fs` (`ScenarioSpec`, `commandsOfSpec`,
  `regenerateEntry`, `commandsOf`, the one existing `Commands = None`
  "spike-fixture" entry) and `src/CommandoWar.Headless/Program.fs` (the
  `c.Command.Intent` print site)
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` (the
  `c.Command.Intent` pending-preview site — the only client file this task
  mechanically touches)
- `tests/CommandoWar.Sim.Tests/CorpusTests.fs`,
  `tests/CommandoWar.Sim.Tests/FixtureTests.fs`,
  `tests/CommandoWar.Sim.Tests/ReplayTests.fs` (`.Intent`/`Intent =` sites)

## Dependencies

- B-026 (TASK-040), done. No other task selected.

## Design

**`AgentState.OrderQueue: ReceivedOrder list`** (`Domain.fs`) — new canonical
field, insertion order (the queue order *is* the meaningful state; unlike
`Agents`/`TacticalKnowledge` it must NOT be sorted before encoding).
`Agent.create` initialises it `[]`.

**`PlayerCommand.Intent: PlayerIntent` becomes `PlayerCommand.Body:
PlayerCommandBody`** (`Commands.fs`), a new type:

```fsharp
type QueueMode = Replace | Append

type PlayerCommandBody =
    | Order of intent: PlayerIntent * mode: QueueMode
    | Cancel of target: CommandId
```

`PlayerIntent` itself (`Domain.fs`, `MoveTo | Suppress`) is **unchanged** —
`ReceivedOrder.Intent` keeps its existing narrow type, so `Appraisal.appraise`,
`Commitment.ofAgent`, and `Canonical.writeOrder` need no new case and no
unreachable-match branch (`AGENTS.md` "make invalid states hard to
construct" — a stored `ReceivedOrder`/`Canonical`-encoded order can never be
a `Cancel` by construction, not by convention). Only `PlayerCommand` — the
transient intake envelope — carries the wider `PlayerCommandBody`.
`Command.moveTo`/`.suppress`/`.moveToMany` wrap their existing intent in
`Order(_, Replace)` (identical observable behaviour for every existing
caller). New: `Command.queued (cmd: PlayerCommand) : PlayerCommand`, a
combinator that turns an `Order` command's mode to `Append` (a no-op on
`Cancel`); new `Command.cancel (id) (issuedAtTick) (agent) (target: CommandId)
: PlayerCommand` building a `Cancel target` body.

**`CommandRejection`** gains `UnknownTargetCommand of target: CommandId` —
a `Cancel` naming a `CommandId` that is neither the recipient's active
`Order.Command` nor present in its `OrderQueue` (checked against the *start-
of-tick* `s.Agents` snapshot `commandIntake` already reads read-only, the
existing `UnknownAgent`/`UnauthorisedRecipient` precedent).

**`commandIntake`** (`Simulation.fs`) still never mutates `s.Agents` (the
existing precedent). Per recipient, per command, after the existing envelope
checks (duplicate id, issue-tick range, empty/duplicate recipients, unknown
agent, hostile recipient — all apply unchanged to both branches below):

- `Order(intent, mode)`: identical to today's flow (the `MoveTo`
  bounds-check special case, `CommandAccepted`, appended to `s.PendingOrders`)
  plus carrying `mode` alongside each pending entry.
- `Cancel target`: look up `target` against the recipient's current `Order`
  (`.Command`) and `OrderQueue` (`|> List.exists (fun o -> o.Command =
  target)`) in `s.Agents`; reject `UnknownTargetCommand` if absent from both;
  otherwise emit `CommandAccepted` (its `Cell` is the recipient's own
  position — the `Suppress` precedent for "no natural target cell") and
  append to a new `s.PendingCancellations: (AgentId * CommandId) list`.

**`communication`** (`Simulation.fs`) is still the phase that copies and
mutates `s.Agents`, comms-gated by `CommunicationAvailable` exactly as today
(a `Cancel` an agent cannot hear is `OrderUndelivered`, the same as any other
order — no special-casing). Processing both pending lists together, in
existing ascending-id order:

- `Order(intent, Replace)`: unchanged — `Order = Some order; Disposition =
  None`, **and now also `OrderQueue = []`** (a bare replace clears any
  previously stacked orders too — the literal meaning of "replace", and a
  no-op for every existing single-order scenario since their queues are
  always already empty).
- `Order(intent, Append)`: if the recipient currently holds no `Order`,
  identical to `Replace` (an idle agent's first queued order starts
  immediately — matches "append to an idle agent" intuition); otherwise
  append to the tail of `OrderQueue` and emit a new `OrderQueued(command,
  recipient)` event — the active `Order`/`Disposition` are untouched.
- `Cancel target`: if `target` is the active `Order.Command`, clear it and
  promote the queue head (`Order = Some head; Disposition = None; OrderQueue
  = tail`) or go idle (`Order = None`) if the queue is empty; if `target` is
  a queued entry, splice it out of `OrderQueue`, leaving the active order and
  every other queued entry untouched. Either way emit a new
  `OrderCancelled(command = target, agent = recipient, wasActive: bool)`.
  Multiple same-tick commands to one recipient are already processed in
  ascending-id order against the incrementally-mutated array (today's
  existing loop structure) — a same-tick "issue three waypoints" or "issue
  then cancel" sequence composes correctly with no extra bookkeeping.

**`commitmentAndLocalAction`** (`Simulation.fs`): the existing "fulfilled
`MoveTo`" branch (`Destination = None`, `Position = target`) changes from
`Order = None; Disposition = None` to promoting the queue head exactly like
the `Cancel`-driven promotion above: `Order = queue head or None; Disposition
= None; OrderQueue = tail or []`. **This promoted order is judged by next
tick's Appraisal, not this one** — `Appraisal` runs before
`commitmentAndLocalAction` in `Phases.order`, so unlike a same-tick delivered
order (zero-delay through `Communication` -> `Appraisal`) a chained waypoint's
next leg begins one tick after arrival. Documented here as a deliberate
consequence of the existing, unchanged phase order (not a new interrupt
mechanism duplicating `Appraisal.appraise` inline) — flag for Dave to
confirm this reads as acceptable, not as a bug, when reviewing. `Suppress`
still has no fulfilled state (`Commitment.fs` Decision D/E, unchanged) — a
queued order stacked behind an active `Suppress` only ever activates via an
explicit `Cancel` of the `Suppress`, never automatically.

**Events** (`Events.fs`): `OrderQueued of command: CommandId * recipient:
AgentId` (Communication, on a genuine append behind a busy agent — not
emitted when append targets an idle agent, the "zero-delay delivery emits no
event" precedent); `OrderCancelled of command: CommandId * agent: AgentId *
wasActive: bool` (Communication, on either cancel outcome). No new
"promoted" event: a queue-head promotion's own eventual `OrderAppraised` /
`CommitmentEstablished` (next tick, or same tick for the `Cancel` path)
already satisfies "the G3 developer trace must explain any appraisal" without
a third event type.

**`Canonical.fs`**: `FormatVersion` 8 -> 9. `writeAgent` gains an `OrderQueue`
section: `w.I32 queue.Length` then `writeOrder w o` per entry, **in list
order, not sorted** (the one deliberate exception to this file's
"ascending id order with an explicit sort" rule — the queue's order is
authoritative sequencing, not an arbitrary collection needing
canonicalisation; document this explicitly in the `FormatVersion` doc comment
so a future reader does not "fix" it into a sort). Every scenario pinned
before this version never appends, so every `OrderQueue` is `[||]` at every
checkpoint — a byte-layout change only, not a behaviour change, for every
existing corpus entry (confirm during implementation, the TASK-032/033/037
precedent).

**Diagnostics** (`AGENTS.md`'s diagnostics rule — this task adds new
authoritative tactical state and must satisfy it): a new `Overlay` case,
e.g. `AgentOrderQueue of agent: AgentId * at: Cell * queued: (CommandId *
PlayerIntent)[]`, built by `Diagnostics.frame`/`.frameOf` from
`AgentState.OrderQueue`. Add a rendering line to each of `DiagnosticRender`'s
`Ascii`/`Svg`/`Html` (and the Godot dev-overlay's `RenderShared.devAgentText`,
`src/CommandoWar.Client.Godot/Core/RenderShared.fs` — TASK-043's `[dev]` HUD
line already lists commitment/suppression/stress/reason per selected agent;
append queue depth there too, mechanical, no new `DrawItem.Kind`). A new
golden `content/diagnostics/` pair (ASCII/SVG) for the new corpus entry below
satisfies "visually inspectable".

**`ReplaySerialisation.fs`** (`.cwreplay`, not the legacy `.cwlog` — Central
decision 4 only restricts `.cwlog`): needs a small, additive grammar
extension so the new corpus entry (below) can round-trip through
`regenerateEntry`/`--regenerate` like every other entry (`Commands = Some
[...]` always calls `ReplaySerialisation.serialise`, so leaving it
Replace/Order-only would make the new entry un-regenerable — a real
mechanical constraint `commandsOf`'s `None` -> legacy-`.cwlog`-fallback path
does not sidestep either, since `.cwlog` is even more restrictive). Extend
`parseIntent`/`serialise`'s trailing-clause tokens: `move <x> <y>` and
`suppress <id>` keep meaning `Order(_, Replace)` unchanged (every existing
`.cwreplay` file parses identically); add `queue-move <x> <y>` / `queue-
suppress <id>` for `Order(_, Append)`, and `cancel <targetCommandId>` for
`Cancel target`. Update `commandShape`'s error text and `UnknownIntent`'s
message to list all five. This is an implementation-level filling-in of
Central decision 4, not a reopening of it — flagged here for Dave to see at
review, not put through another `AskUserQuestion` round.

**New corpus entry** (`Corpus.fs`): one entry proving the mechanism end to
end — an agent given three stacked waypoints (`Order(MoveTo, Replace)` then
two `Order(MoveTo, Append)`), each leg completing and promoting the next
(with the one-tick gap noted above visible in the trace), plus a fourth
command cancelling the still-queued final waypoint before it activates
(`OrderCancelled(wasActive = false)`) and, separately, a `Cancel` of a
currently-active leg (`OrderCancelled(wasActive = true)`, promoting the
remaining queue). `commandsOfSpec`/`ScenarioSpec`'s authoring DSL
(`MoveOrder`/`SuppressOrder`) does not know about `QueueMode`/`Cancel` yet;
either extend it minimally (a `QueueMode` field on its `Intent` plus a
`CancelOrder` case, the smaller lift) or build this one entry's
`RecordedCommand[]` directly rather than through the DSL — decide whichever
is the smaller diff once the DSL shape is in front of you; either way the
entry's `.cwreplay` file is written by `regenerateEntry` like every other
`Some` entry (relying on the `ReplaySerialisation` extension above).

## Allowed scope

- `src/CommandoWar.Sim/Domain.fs`: `AgentState.OrderQueue`, `Agent.create`.
- `src/CommandoWar.Sim/Commands.fs`: `QueueMode`, `PlayerCommandBody`,
  `PlayerCommand.Body` (renamed from `.Intent`), `CommandRejection.
  UnknownTargetCommand`, `Command.queued`, `Command.cancel`; the existing
  three builders updated to construct `Order(_, Replace)`.
- `src/CommandoWar.Sim/Simulation.fs`: `commandIntake`, `communication`,
  `commitmentAndLocalAction`, `StepState` (a new `PendingCancellations`
  field, the `PendingOrders` precedent).
- `src/CommandoWar.Sim/Events.fs`: `OrderQueued`, `OrderCancelled`; the
  `DomainEvent` ordering comment updated.
- `src/CommandoWar.Sim/Canonical.fs`: `FormatVersion` 8 -> 9, `writeAgent`'s
  new `OrderQueue` section.
- `src/CommandoWar.Sim/Diagnostics.fs`: new `Overlay` case, `frame`/`.frameOf`
  wiring.
- `src/CommandoWar.Headless/DiagnosticRender.fs`: `Ascii`/`Svg`/`Html`
  rendering for the new overlay.
- `src/CommandoWar.Sim/ReplaySerialisation.fs`: the grammar extension above.
- `src/CommandoWar.Headless/Corpus.fs`: the new corpus entry (and a minimal
  `ScenarioSpec` DSL extension if that turns out smaller than a hand-built
  `RecordedCommand[]`); `src/CommandoWar.Headless/Program.fs`'s
  `c.Command.Intent` print site updated to `.Body`.
- `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs`: the
  `c.Command.Intent` pending-preview match updated to `.Body` — mechanical
  compile fix only, **no new client-facing queue/cancel UI** (see Forbidden).
- `src/CommandoWar.Client.Godot/Core/RenderShared.fs`: `devAgentText`
  appends queue depth to the existing `[dev]` HUD line (the TASK-043
  precedent) — the only client behaviour change permitted.
- `content/diagnostics/` new golden pair + `README.md`.
- `tests/CommandoWar.Sim.Tests/`: `CorpusTests.fs`, `FixtureTests.fs`,
  `ReplayTests.fs` (`.Intent` -> `.Body` mechanical fixes) plus new focused
  tests for append/replace/cancel/promotion/`UnknownTargetCommand` and the
  `Canonical.FormatVersion` bump.
- `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`, `PROJECT_STATE.yaml`.

## Forbidden scope

- No change to `Simulation.step`'s public signature (`PlayerCommand[] ->
  WorldState -> StepResult`, unchanged) — `Cancel` travels through the exact
  same `commands` array as every other command, avoiding a second parameter
  that would otherwise touch every one of the ~20 call sites across
  `bench/`, both Godot/Mibo clients, `CommandoWar.Headless`, and every test
  file.
- No `Appraisal.fs` or `Commitment.fs` change — both already proved
  unnecessary in Design above; if implementation reveals otherwise, stop and
  flag it rather than silently expanding scope.
- No extension of the legacy `.cwlog` grammar (Central decision 4).
- No Godot client UI for issuing an append or cancel command (no new
  keybinding, no new `IClientScene` member, no `FSharpSceneHost.cs` input
  change) — this task proves the sim-side mechanism only, exactly as
  TASK-030/032/033 shipped their mechanics before any client wiring. The one
  permitted client touch is the mechanical `.Body` compile fix plus the
  `[dev]` HUD queue-depth line named in Allowed scope.
- No `Assault`/`Withdraw`/`Hold` `PlayerIntent` cases (B-030 proper) and no
  ammunition/cooldown model — unrelated backlog rows.
- No change to `AppraisalConfig` thresholds or any existing corpus entry's
  observable behaviour (only byte-layout hash movement from the
  `FormatVersion` bump is expected).
- Nothing under `src/_scratch/`, `bench/`, `content/benchmarks/BASELINE.md`.

## Required work

1. `Domain.fs`: add `AgentState.OrderQueue`.
2. `Commands.fs`: `QueueMode`, `PlayerCommandBody`, rename `.Intent` ->
   `.Body`, update the three existing builders, add `Command.queued` /
   `Command.cancel`, add `CommandRejection.UnknownTargetCommand`.
3. `Simulation.fs`: extend `commandIntake` (Cancel validation, `PendingOrders`
   carries `QueueMode`, new `PendingCancellations`), `communication` (append/
   replace/cancel-and-promote, comms-gated), `commitmentAndLocalAction`
   (queue-head promotion on fulfillment).
4. `Events.fs`: `OrderQueued`, `OrderCancelled`.
5. `Canonical.fs`: bump `FormatVersion`, extend `writeAgent`.
6. `Diagnostics.fs` + `DiagnosticRender.fs`: new overlay, all three renderers.
7. `ReplaySerialisation.fs`: grammar extension, `commandShape`/error text.
8. `Corpus.fs`: new entry; regenerate its table and `.cwreplay`.
9. Mechanical `.Intent` -> `.Body` fixes: `Program.fs`, `CommandDemoScene.fs`,
   `CorpusTests.fs`, `FixtureTests.fs`, `ReplayTests.fs`.
10. `RenderShared.fs`: queue depth in `devAgentText`.
11. New focused tests (append, replace-clears-queue, cancel-active-promotes,
    cancel-queued-leaves-rest, `UnknownTargetCommand` rejection, one-tick
    promotion-delay assertion, `Canonical.FormatVersion` bump / re-pin).
12. Verify per Required verification; regenerate every pinned corpus/fixture
    hash affected by the `FormatVersion` bump and confirm the move is
    byte-layout only (tick/event counts unchanged) for every entry except the
    new one.
13. Update documentation.

This is an outcome checklist, not permission to invent missing architecture.

## Acceptance criteria

- [x] A `MoveTo` order queued (`Append`) behind an agent's active order does
      not take effect until the active order is fulfilled or cancelled; on
      fulfillment the queued order becomes active and is appraised the
      following tick.
- [x] A bare (`Replace`) order still fully supersedes both the active order
      and any queued orders, unchanged from today.
- [x] Cancelling the active order's `CommandId` clears it and promotes the
      next queued order (or leaves the agent idle if none); cancelling a
      queued order's `CommandId` removes only that entry.
- [x] Cancelling an unknown `CommandId` is rejected
      (`CommandRejection.UnknownTargetCommand`), not silently ignored.
- [x] `Canonical.FormatVersion` bumped; every pre-existing pinned corpus/
      fixture hash re-pins as a byte-layout change only (unchanged tick and
      event counts), confirmed in the ledger.
- [x] New corpus entry demonstrates stacking, fulfillment-driven promotion,
      cancel-active, and cancel-queued end to end; `--corpus --regenerate`
      twice is byte-identical.
- [x] A new `content/diagnostics/` golden pair (ASCII/SVG) visualises the
      order queue for the new entry (`AGENTS.md` diagnostics rule).
- [x] `Simulation.step`'s public signature is unchanged.
- [x] No `Appraisal.fs`/`Commitment.fs` change (or, if one proved necessary,
      it is called out and justified in the completion report, not silently
      folded in).
- [x] `dotnet build` both `.slnx` files 0/0; `dotnet test` all green.
- [x] Required documentation updated.

## Required verification

- `dotnet test CommandoWar.slnx -c Release`
- `dotnet build CommandoWar.slnx -c Release` and
  `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`
- `-- corpus` (all entries, `--regenerate` run twice, byte-identical)
- `-- fixture` (format version bump reflected, event/tick counts unchanged
  outside the new entry)
- `-- replay-file` (envelope-full round-trip for the new entry)
- Dependency boundary check: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package` (still `FSharp.Core` only; no new dependency)
- `git status --porcelain`: matches this task's allowed scope

## Evidence to capture

- Full command output and pass/fail counts for every command above.
- The new corpus entry's rendered table and golden diagnostic pair paths.
- The `FormatVersion` bump's exact before/after hash list for at least one
  representative pre-existing entry, showing tick/event counts unchanged.

## Expected files

See Allowed scope.

## Documentation updates

- This task file's Outcome/Review sections.
- `docs/11_BACKLOG.md` B-051 row.
- `docs/12_PROGRESS_LEDGER.md` (index row + `docs/ledger/` detail file).
- `content/diagnostics/README.md`.
- `PROJECT_STATE.yaml`.

## Rollback or removal

Additive to `WorldState`/`AgentState` plus one renamed field
(`PlayerCommand.Intent` -> `.Body`) with every call site updated at compile
time (the compiler will not let a call site silently miss the rename). No
change to `Simulation.step`'s signature, `Appraisal.fs`, or `Commitment.fs`.
Revertible with `git revert` in one step; the `Canonical.FormatVersion` bump
is the only irreversible-in-place consequence (already true of every prior
canonical change, e.g. TASK-032/033/034/037).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.

## Review

- Reviewer: Dave
- Accepted: yes (2026-09-17, "no new UI to review, just accept") — confirms
  this task's sim-side-only scope read correctly: nothing player-facing
  changed, so there was nothing to check live in a windowed run, unlike
  every client-touching task this session.
