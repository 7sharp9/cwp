## 2026-09-13 - TASK-030 - Commitment and finite move/hold executor

**Owner:** Dave with coding-agent assistance
**Branch:** `task-030-commitment-and-finite-executor` off `main` at the
TASK-029 merge (`Merge TASK-029: Godot appraisal-divergence demo (B-029)`).
Committed locally; not pushed.
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; FSharp.Core
10.1.303; xUnit 2.9.3; FsCheck 3.3.4
**Status change:** TASK-030 drafted `-> ready -> review`; backlog B-018
`proposed -> done`

Realises `docs/08` section 6 P3 Required work "implement commitment and
finite execution states". Turns the `docs/04` section 12.6 Commitment and
local action phase from a no-op into a real phase: an explicit `Commitment`
(`Holding | Moving`), a finite move/hold executor, and the one interrupt
priority current systems can source (a new order supersedes an in-progress
commitment). No ADR — no canonical-image change (see Decision B).

### Central decisions (confirmed with Dave 2026-09-13 before the phase bodies)

- **A - `Commitment` DU subset: `Holding | Moving of MoveCommitment` only.**
  `Suppressing` / `Assaulting` / `Withdrawing` need `PlayerIntent` cases that
  do not exist (`Hold` / `Suppress` / `Assault` / `Withdraw` - backlog B-030)
  and combat/suppression state that does not exist (B-019/B-020); adding them
  now would be speculative type machinery. `Holding` covers idle, arrived,
  refused, and unable.
- **B - `Commitment` is a pure derived value, NOT new canonical state; no
  `Canonical.FormatVersion` bump.** Fully recoverable from fields already
  canonical (`Order`, `Disposition`, `Destination`) - the identical argument
  that keeps `AgentState.Route` out of `Canonical.encode`. This revises the
  framing discussed with Dave 2026-09-12 (originally assumed Commitment would
  need new canonical state and another format bump); realising the phase body
  showed the derivation is total and needs no memory of its own. Result: no
  new `AgentState` field, no hash re-pin of any corpus entry, the fixture, or
  `envelope-full` - a materially smaller and lower-risk task than TASK-028's
  four-field, full-re-pin shape.
- **C - stage-5 safer adaptation stays out of this task.** The only reachable
  adaptation ("use a less exposed route") needs a second, separate
  exposure-aware route-search algorithm and a Navigation change to follow a
  pinned route - two concerns in one task. `OrderDisposition` gains no
  `Adapted` case. Left named, not built.
- **D - interrupt table: exactly one realised priority, no separate
  `Interrupt` type.** Of `docs/05` section 11's seven priorities, only "new
  higher-priority command" has a live signal today (1-4 need combat/
  suppression state that does not exist; 5 "route invalidated" was already
  assigned to B-021 by TASK-028's Decision E - it needs a stall counter).
  Because `Commitment` is derived, supersession needs no `Interrupt` type or
  `Superseded` outcome case: the old commitment simply stops being produced,
  and the new `CommitmentEstablished` for the superseding order is the
  complete trace.
- **E - realised as its own `commitmentAndLocalAction` phase function** in the
  existing `CommitmentAndLocalAction` slot (`Phases.order`, between
  `Appraisal` and `NavigationAndMovement`), matching the one-phase-per-doc-
  section pattern TASK-026/027/028 established, rather than folding into
  `appraisal`. One small relocation out of `appraisal`: its fulfilled-order
  housekeeping branch (`Some Accepted when Destination = None && Position =
  target -> clear Order/Disposition`) moved, unchanged in condition and
  effect, into the new phase - a commitment ending is a 12.6 concern, not a
  12.5 one.
- **F - two new events, no `CommitmentOutcome` DU.**
  `CommitmentEstablished of agent * command * target` (emitted when this
  tick's `OrderAppraised` for `(agent, command)` was `Accepted` - covers both
  "from Holding" and "supersedes a prior Moving commitment") and
  `CommitmentCompleted of agent * command * at` (the relocated fulfilment
  housekeeping, now named).
- **G - no ADR.** No canonical-image change, so the ADR-0002 amendment does
  not apply the way it did for TASK-026/027/028.
- **H - one new corpus entry proving supersession.** `reissued-order`: one
  friendly ordered east on tick 1, re-ordered south on tick 3 while still
  mid-route. The second order supersedes the first with no event reporting
  the first commitment's end.

### Phase bodies

`appraisal` (12.5): removed the `Some Accepted when Destination = None &&
Position = target` branch; the remaining two branches (fast path, fresh
appraise) are unchanged.

`commitmentAndLocalAction` (12.6, new): for each agent with `Order = Some o,
Disposition = Some Accepted`: if `Destination = None && Position = target`,
clear `Order`/`Disposition` and emit `CommitmentCompleted` (relocated from
`appraisal`); else if this tick's `OrderAppraised(agent, o.Command, Accepted)`
was just emitted (checked via `s.EventsRev`, not persisted state), emit
`CommitmentEstablished`; else (a `Moving` commitment continuing) nothing.
`Order = None` or `Disposition = Some (Refused | Unable)`: nothing (`Holding`).

`Commitment.ofAgent` (new leaf `Commitment.fs`, after `Appraisal.fs`): `Some
o, Some Accepted, Some target -> Moving { Command = o.Command; Target =
target }; _ -> Holding`. Pure, total. Used by `commitmentOverlays` in
`Diagnostics.fs` (derives `Overlay.AgentCommitment` in both `frame` and
`frameOf`, the `OrderAppraisal` precedent) and by the `SimulationTests` /
`DeterminismPropertyTests` facts. Renamed from the task-file draft's proposed
`Overlay.Commitment` to `Overlay.AgentCommitment` during implementation to
avoid a case-name / type-name collision with the `Commitment` type itself
(the `OrderAppraisal` / `OrderDisposition` precedent).

### FS0025 sites (every exhaustive `EventBody` / `Overlay` match armed)

Compile-time (`CommandoWar.Sim`, `TreatWarningsAsErrors`): `Diagnostics.fs`
`eventMarker`, `reservationOverlays`, `obstructionOverlays`,
`undeliveredOrderOverlays` filters.

Compile-time (`CommandoWar.Headless`, `TreatWarningsAsErrors`):
`DiagnosticRender.fs` `sightRays` / `plannedPaths` `Ascii` filters, the
`Ascii` overlay-text match, the `Svg` overlay match; `AppraisalDemo.fs`'s
overlay match (added an `AgentCommitment` arm to the `unhandled` bucket - this
disposable P3 demo predates TASK-030 and does not render commitments; the
`OrderAppraisal` panel already shows each friendly's decision).

Runtime-only (`CommandoWar.Sim.Tests` has no `TreatWarningsAsErrors`, so a
missing arm there is a silent `MatchFailureException`, not a build error):
four exhaustive `Overlay` matches in `DiagnosticsTests.fs` (the `Reserved` /
`Obstructed` / `KnownContact` / `UndeliveredOrder` overlay-isolation facts).

### Existing-test ripple (every case behaviour-neutral; no hash moved)

Adding `Overlay.AgentCommitment` (populated for every agent, every tick, by
both `frame` and `frameOf`) changed what several pre-existing tests observed,
even though it changed no canonical fact:

- `DiagnosticsTests`: "the frame carries every terrain layer..." asserted
  `Assert.Empty(f.Overlays)` at tick 0 - now one `Holding` `AgentCommitment`
  per agent. "frameOf emits no overlay once every agent is at rest..." (now
  retitled "...no OrderAppraisal overlay...") asserted `Assert.Empty` on
  `Overlays` post-fulfilment - now checks `OrderAppraisal` specifically is
  absent while `AgentCommitment` is always present. The three golden-pinned
  "shared fixture does not perturb" / corpus-table facts had their hardcoded
  event-count literal `34` updated to `36` (`+1 CommitmentEstablished, +1
  CommitmentCompleted` for the fixture's one order).
- `PathfindingTests` / `SightTests`: "an overlay appears distinctly and the
  overlay-absent render is unchanged" asserted `Assert.DoesNotContain
  "overlays:"` on a bare `Diagnostics.frame` render - narrowed to
  `Assert.DoesNotContain "path ("` / `"sight ("` (the overlay section is no
  longer empty, but still carries neither ray nor path text). Their two
  golden-pinned fixture facts got the same `34 -> 36` update. `losFrameWithRays`
  / `pathFrameWithRoutes` test helpers previously **replaced** `frame.Overlays`
  wholesale (`{ f with Overlays = overlays }`) rather than appending, which
  silently discarded `AgentCommitment` entries the real `cwheadless render
  --los` / `--path` CLI path appends (`Program.fs`); changed both helpers to
  append, matching production behaviour, and regenerated `los.*` / `path.*`
  goldens accordingly.
- `CorpusTests`: the `spike-fixture` "is not an independent re-pin" fact's
  hardcoded `34` updated to `36`.
- `ReplayTests`: the `envelope-full` cross-check compares a fresh run's event
  count against the **committed** `envelope-full.md` table (no hardcoded
  literal in the test) - updated `envelope-full.md`'s "Domain events" `75 ->
  78` (+1 `CommitmentEstablished` per recipient; all three recipients still
  mid-route at tick 24, so no `CommitmentCompleted`). Checkpoint hashes
  unchanged.
- `DiagnosticsTests` (TASK-029 demo): `AppraisalDemo.loadFrameViews`'s
  `UnhandledOverlays` assertion updated from `Assert.Empty` to expecting
  exactly 3 entries (one `AgentCommitment` per agent in the `exposed-approach`
  scenario - 2 friendlies + 1 hostile, not 2 as first assumed).

### Event-trace impact (no canonical hash change)

Every existing corpus entry, the fixture, and `envelope-full` gained new
events wherever an order is `Accepted` (`CommitmentEstablished`) or an agent
arrives (`CommitmentCompleted`) - the honest surface of realising the phase.
**No state hash moved anywhere** (`Commitment` is not written to
`Canonical.encode`); verified by diffing every corpus `.md`, `envelope-full.md`,
and every `content/diagnostics/*` golden for hash-line changes: none found,
only "Domain events" counts and overlay/event text.

| Entry | Events before | Events after | Note |
|---|---:|---:|---|
| `spike-fixture` | 34 | 36 | +1 Established, +1 Completed (agent 3's one order) |
| `wall-detour` | 20 | 22 | +1 Established, +1 Completed (one order, arrives) |
| `blocked-goal` | 2 | 2 | unchanged - order is `Unable`, never establishes |
| `converging-routes` | 21 | 25 | +2 Established, +2 Completed (two agents, both arrive) |
| `slow-terrain` | 7 | 9 | +1 Established, +1 Completed (one order, arrives) |
| `follow-chain` | 24 | 27 | +3 Established (three agents, none arrive within 6 ticks) |
| `swap-standoff` | 12 | 14 | +2 Established (two agents, both obstructed, neither arrives) |
| `perception-contact` | 13 | 15 | +1 Established, +1 Completed (one order, arrives) |
| `lost-comms` | 2 | 2 | unchanged - order never delivered, no `Order` written |
| `exposed-approach` | 19 | 21 | +1 Established (agent 1, Accepted, does not arrive within 12 ticks); agent 0's Refused establishes nothing |
| `reissued-order` | (new entry) | 12 | 2 Established (tick 1, tick 3 supersession), no Completed |
| `envelope-full` | 75 | 78 | +3 Established (three recipients, none arrive within 24 ticks) |

Full before/after per-tick hash tables are in `git diff content/replays/`
(committed alongside this ledger entry) - every diff line is a "Domain events"
count, none a hash.

### Verification

- `dotnet build CommandoWar.slnx -c Release`: 0/0 before and after.
- `dotnet test CommandoWar.slnx -c Release`: `244 -> 254` green. New: 8
  `SimulationTests` facts (fresh-Accepted establishes; arrival completes,
  regression-checked against pre-TASK-030 behaviour; a second order
  supersedes with no event for the first; Refused/Unable never establish; an
  unchanged order emits neither event on an idle tick; two-run determinism;
  `Commitment.ofAgent` unit coverage of every reachable combination), +1
  `DeterminismPropertyTests` property (`every agent's derived Commitment
  matches its Order, Disposition, and Destination`, `MaxTest = 200`, reusing
  property 8's `appraisalCaseGen`), +1 `DiagnosticsTests` fact (the
  `reissued-order` golden, byte-equal ASCII/SVG).
- `cwheadless corpus`: 11/11 PASS (ten existing + `reissued-order`);
  `--regenerate` is a zero diff on a second run (idempotent).
- `cwheadless fixture`: format 4, hashes `0x55F43D66C7AECB7F` /
  `0x7737282578E821C6` unchanged, events `34 -> 36`.
- `cwheadless replay-file content/replays/envelope-full.cwreplay`: checkpoints
  OK at canonical 4, all 24 hashes unchanged, events `75 -> 78`.
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`: `FSharp.Core` only.
- Source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`: clean (comment mentions
  only, pre-existing).
- `git status --porcelain`: matches the task file's "Allowed scope" plus two
  necessary ripples not originally listed - `src/CommandoWar.Headless/
  AppraisalDemo.fs` and its `DiagnosticsTests.fs` fact (both required by
  adding the new `Overlay` case to an existing exhaustive match; nothing under
  the client spikes, `src/_scratch`, `bench/`, or `content/benchmarks/
  BASELINE.md`).

### Unresolved / follow-ups

- Stage-5 safer adaptation (`Adapted`, exposure-aware reroute) - named, not
  built (Decision C); no B-item number assigned yet, revisit when B-020/B-021
  make suppression/stress real enough to need it.
- Interrupt priorities 1-5 - B-019/B-020/B-021 as each system lands.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-13)
- Notes: `commitmentAndLocalAction` is a real phase in `Phases.order` slot 6
  (`docs/04` section 12.6). `Commitment` (`Holding | Moving of
  MoveCommitment`) is a pure derived value over the already-canonical
  `Order` / `Disposition` / `Destination` — no `AgentState` field, no
  `Canonical.FormatVersion` bump, no hash re-pin anywhere (confirmed: every
  corpus `.md`, `envelope-full.md`, and diagnostics golden diffed for
  hash-line changes, none found). `Simulation.appraisal`'s fulfilled-order
  housekeeping relocated unchanged into the new phase, which additionally
  emits `CommitmentCompleted`; a fresh `Accepted` order emits
  `CommitmentEstablished`, covering both "from `Holding`" and "supersedes a
  prior `Moving` commitment" with one signal. New `reissued-order` corpus
  entry proves supersession end-to-end. `244 -> 254` green; build 0/0;
  `-- corpus` 11/11 (`--regenerate` idempotent); `-- fixture` format 4,
  hashes unchanged, 34 -> 36 events; `-- replay-file envelope-full` OK at
  canonical 4, hashes unchanged, 75 -> 78 events; `git status` clean;
  `src/CommandoWar.Sim` packages `FSharp.Core` only; source scan clean. No
  ADR. Merged to `main` (`--no-ff`, branch
  `task-030-commitment-and-finite-executor` deleted; not pushed).
