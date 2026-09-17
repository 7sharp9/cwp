## 2026-09-17 - TASK-044 - Order queue: multi-waypoint stacking and cancellation (B-051)

**Owner:** Dave
**Source revision:** implemented directly on `main`, no branch (the TASK-035/036 low-risk precedent)
**Environment:** Windows x64, .NET SDK `10.0.303`, Godot 4.7.2.stable.mono
**Status change:** `tasks/TASK-044-*.md` `proposed -> review`

### Why

B-051's own dependency (B-026/TASK-040) was `done`, and its central design fork
(sim-side order queue vs. client-side sequencing) had already been resolved
earlier in the session via `AskUserQuestion` in favour of the sim-side queue.
Selected as the next task via `AskUserQuestion` over B-030 proper, B-031, and
B-053. Four further central decisions confirmed with Dave via
`AskUserQuestion` before drafting the task file: any order type (not just
`MoveTo`) can be queued; a bare new command still replaces by default, append
is opt-in; cancellation targets a specific `CommandId`, queued or active; the
legacy `.cwlog` grammar is not extended.

### Changes

**`src/CommandoWar.Sim/Domain.fs`**: `AgentState.OrderQueue: ReceivedOrder
list`, new canonical field (insertion order, not sorted — see Canonical.fs
below); `Agent.create` initialises it `[]`.

**`src/CommandoWar.Sim/Commands.fs`**: `PlayerCommand.Intent: PlayerIntent`
renamed to `.Body: PlayerCommandBody`, a new type: `Order of intent:
PlayerIntent * mode: QueueMode | Cancel of target: CommandId`, `QueueMode =
Replace | Append`. `PlayerIntent` itself (`MoveTo | Suppress`) is unchanged —
`ReceivedOrder.Intent` keeps its pre-existing narrow type, so `Appraisal.fs`
and `Commitment.fs` needed **zero** changes: a stored order can never be a
`Cancel` by construction, not by convention (`AGENTS.md` "make invalid states
hard to construct"). The three existing builders (`moveTo`/`suppress`/
`moveToMany`) now wrap their intent in `Order(_, Replace)` — byte-identical
behaviour for every existing caller. New `Command.queued` (a combinator
turning any `Order` command's mode to `Append`) and `Command.cancel`. New
`CommandRejection.UnknownTargetCommand of recipient * target`.

**`src/CommandoWar.Sim/Simulation.fs`**: `StepState.PendingOrders` became
`PendingCommands: (AgentId * CommandId * PendingBody) list` (`PendingBody =
PendingOrder of ReceivedOrder * QueueMode | PendingCancel of CommandId`), a
unified list so an order and a cancel for the same recipient in the same tick
stay ordered by their own `CommandId` relative to each other. `commandIntake`
gained a `Cancel` branch (envelope validation identical to `Order`, plus a new
`holdsCommand` check against the start-of-tick snapshot — `UnknownAgent`/
`UnauthorisedRecipient`/`UnknownTargetCommand`, in that order); still never
mutates `s.Agents`. `communication` (the phase that already copies and writes
`s.Agents`) now handles four cases: `Order(_, Replace)` — unchanged behaviour
plus clearing `OrderQueue`; `Order(_, Append)` — appends to `OrderQueue` if an
order is already active (emits `OrderQueued`), else identical to `Replace`;
`Cancel` matching the active order — clears `Order`/`Disposition`
**and `Destination`** (a real bug caught and fixed during implementation, see
Deviations), promotes the queue head if any (emits `OrderCancelled(...,
wasActive = true)`); `Cancel` matching a queued entry — splices it out alone
(emits `OrderCancelled(..., wasActive = false)`); `Cancel` matching neither
(a same-tick race against an earlier, lower-id command) — no-op, no event,
documented inline. `commitmentAndLocalAction`'s fulfilled-`MoveTo` branch now
promotes the queue head instead of clearing to `None` — judged by **next**
tick's Appraisal, not this one, since Appraisal already ran earlier this tick
(a deliberate, documented one-tick gap, distinct from the same-tick
Cancel-driven promotion).

**`src/CommandoWar.Sim/Events.fs`**: `OrderQueued of command * recipient`,
`OrderCancelled of command * agent * wasActive: bool`.

**`src/CommandoWar.Sim/Canonical.fs`**: `FormatVersion` 8 -> 9. `writeAgent`
gains an `OrderQueue` section: count + `writeOrder` per entry, in **list
order, not sorted** — the queue's order is itself the authoritative
sequencing, the one deliberate exception to this file's "ascending id order
with an explicit sort" convention elsewhere, called out in the `FormatVersion`
doc comment so a future reader does not "fix" it into a sort.

**`src/CommandoWar.Sim/Diagnostics.fs`**: new `Overlay.AgentOrderQueue of
agent * at * queued: (CommandId * PlayerIntent)[]`, sparse (non-empty queues
only, the `AgentSuppression`/`AgentStress` precedent), derived by both `frame`
and `frameOf` (standing canonical state, the `AgentCommitment` precedent).

**`src/CommandoWar.Headless/DiagnosticRender.fs`**: `Ascii` (`order queue
(x,y): agent N  [#id move (x,y), ...]`), `Svg` (a small `+N` badge on the
cell's top edge — the four corners are already used by `AgentCommitment`/
`AgentSuppression`/`AgentStress`/`OrderAppraisal`), and the HTML annotation
panel (a new `Queue` column) all extended; `eventNarration` gained three new
sentence forms.

**`src/CommandoWar.Sim/ReplaySerialisation.fs`** (`.cwreplay`, not the legacy
`.cwlog` — Central decision 4 only restricted `.cwlog`): the `command`
directive's trailing clause gained `queue-move`/`queue-suppress` (`Append`)
and `cancel <targetCommandId>` alongside the unchanged `move`/`suppress`
(`Replace`) tokens — needed so the new corpus entry below can round-trip
through `regenerateEntry`/`--regenerate` like every other entry.

**`src/CommandoWar.Headless/Corpus.fs`**: new entry
`order-queue-stacking-and-cancellation` (12 x 3, one friendly agent, 9 ticks),
its commands built directly as `RecordedCommand[]` (not through the
`ScenarioSpec`/`commandsOfSpec` authoring DSL, which has no `QueueMode`/
`Cancel` concept and was smaller to leave alone than extend for one entry):
three waypoints stacked on tick 1 (`Replace` then two `Append`), a tick-2
cancel of the still-queued third waypoint, a tick-4 fulfilment-driven
promotion of the second (the one-tick gap, visibly: `Holding` at tick 4,
`Moving` again at tick 5), and a tick-7 cancel of the by-then-active second
waypoint (`Destination` cleared, agent genuinely stops at `(5,0)`).

**Mechanical `.Intent` -> `.Body` fixes** (the rename's full blast radius,
confirmed by grep before starting, not discovered piecemeal):
`src/CommandoWar.Headless/Program.fs` (`cwheadless replay describe`'s intent
print), `src/CommandoWar.Client.Godot/Core/CommandDemoScene.fs` (the pending-
route-preview match — the only client-side touch this task makes; no new
client UI for queueing or cancelling, per Forbidden scope),
`src/CommandoWar.Headless/AppraisalDemo.fs` (`Overlay` match gained an
`AgentOrderQueue` -> `unhandled` case, the existing precedent for overlays
this disposable P3 demo predates), and five test files (see below).

**Godot client `--selfcheck` re-pin**: `Canonical.FormatVersion`'s bump moves
every hash, including both scenes' pinned self-check constants in
`src/FSharpSceneHost.cs` — `SnapshotDemo.tscn` `0x11B06E6EDE0C52E3 ->
0xC68F993BC605313C`, `CommandDemo.tscn` `0x649FA4D08E2931CA ->
0xD27E623504262CE9`. `src/CommandoWar.Client.Godot/README.md`'s worked
examples updated to match (eight occurrences).

### Deviation found during implementation

Cancelling an agent's active `MoveTo` order (with an empty queue behind it)
initially left `AgentState.Destination` untouched: `NavigationAndMovement`
reads `Destination` independently of `Order`, so the agent would have kept
walking toward the cancelled target forever, never actually "stopping" as
`Cancel`'s whole point requires. Caught while writing the corpus entry's own
trace (the agent did not stop at `(5,0)` as designed) before it reached a
test. Fixed by clearing `Destination` in the same `communication` branch that
clears `Order`/`Disposition` on a `Cancel` matching the active order — the
`Appraisal`-overwrite precedent that already handles this for a superseding
`Replace` order, applied explicitly here since a `Cancel`-driven promotion
bypasses `Appraisal` this same tick. Covered by a new dedicated
`SimulationTests` fact.

### Verification

- Command: `dotnet build CommandoWar.slnx -c Release`
  - Result: `0/0`.
- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `306/306` (297 + 9 new: five append/replace/cancel/promotion/
    rejection facts in `SimulationTests.fs`, one `Destination`-clearing fact,
    two `DiagnosticsTests.fs` facts for the new corpus entry, plus the
    existing `297` all still green after every re-pin below).
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug` and `-c Release`
  - Result: `0/0` both.
- Command: `dotnet build src/CommandoWar.Client.Mibo/CommandoWar.Client.Mibo.slnx -c Debug`
  - Result: `0/0` (no `.Intent`/`.Body` usage there; checked, not assumed).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate` (run twice)
  - Result: 15 entries written both times; a SHA-256 diff of every
    `content/replays/*.md`/`*.cwreplay` file between the two runs was empty
    (byte-identical). Every pre-existing entry's tick/event counts unchanged;
    only hashes moved (`Canonical.FormatVersion` byte-layout change).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- corpus`
  - Result: `OK - all 15 entries match their committed tables`.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  - Result: final hash `0xC9694E97A7210117` (format 9), 36 events, matches
    the regenerated `spike-fixture.md`/`FixtureTests.fs`.
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- replay-file content/replays/envelope-full.cwreplay`
  - Result: `checkpoints : OK (24 ticks match the file's committed hashes)`,
    78 events unchanged.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package`
  - Result: `FSharp.Core` only.
- Command (Godot, headless, both scenes): `"$GODOT" --headless --path . scenes/SnapshotDemo.tscn -- --selfcheck` / `scenes/CommandDemo.tscn`
  - Result: `MATCH` the newly re-pinned hashes at tick 20, both scenes (no
    hang this session — see `PROJECT_STATE.yaml`'s separate follow-up note).
- Manual: `git status --porcelain`
  - Result: matches this task's allowed scope (see the task file's Allowed
    scope list).

### Evidence

- New golden diagnostic pair:
  `content/diagnostics/order-queue-stacking-and-cancellation-tick-001.{ascii.txt,svg}`
  — tick 1's `AgentOrderQueue` overlay, two queued waypoints, both
  `OrderQueued` events.
- New corpus entry: `content/replays/order-queue-stacking-and-cancellation.{md,cwreplay}`.
- New re-pinned `content/replays/envelope-full.{md,cwreplay}` (initial hash
  `0xA2726329BB740614`, final `0x60DE16C440B25F5F`, 78 events unchanged).
- A scratch `dotnet fsi` probe (removed after use) tick-by-tick trace of the
  new corpus entry, cross-checked against the design in the task file and
  the committed golden — confirmed every intended transition (both
  `OrderQueued`s, `order-cancelled-queued` at tick 2, `commitment-completed`
  with no `order-appraised` at tick 4, `order-appraised` at tick 5,
  `order-cancelled-active` at tick 7, `Destination = None` and the agent
  genuinely at rest at `(5,0)` through tick 9).

### Documents updated

- `tasks/TASK-044-ORDER-QUEUE-STACKING-AND-CANCELLATION.md` (drafted,
  implemented; Status `proposed -> review`).
- `docs/11_BACKLOG.md` B-051 row (`proposed -> review`).
- `content/diagnostics/README.md` (new file row + regeneration-section entry).
- `docs/12_PROGRESS_LEDGER.md` (Pinned facts `Canonical.FormatVersion`
  8 -> 9 and Green tests `297 -> 306`; this row).
- `PROJECT_STATE.yaml`.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-17, "no new UI to review, just accept") — this task
  is sim-side only (Forbidden scope explicitly excluded any client-facing
  queue/cancel UI), so there was nothing player-facing to check live in a
  windowed run, unlike every client-touching task this session. The
  self-verification evidence above stood as the acceptance basis.
