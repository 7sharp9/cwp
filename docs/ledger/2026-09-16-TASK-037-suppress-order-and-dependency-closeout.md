## 2026-09-16 - TASK-037 - Minimal Suppress order and B-021/B-022 dependency closeout

### Why

G3's critical path runs through B-023 (the canonical refusal-and-correction
scenario, `docs/07` section 8, G3's headline evidence item), blocked on
B-021/B-022's remaining `review`-partial scope and, more fundamentally, on a
`Suppress` order for step 5 that only existed as backlog B-030 (P4, "implement
suppress and assault command executors", never pulled forward though its own
dependencies B-018/B-020 were both already `done`). Flagged to Dave twice
before (the TASK-036 drafting session, then again at TASK-036's acceptance)
without being resolved. Resolved this session via `AskUserQuestion`: Dave
chose "do whichever unblocks the most progress; merge tasks if more than one
unblocks more" over three narrower options (pull forward a thin B-030 slice;
explicitly descope B-021/B-022; pick a ready-but-not-critical-path item
instead). Neither the Suppress-order slice alone (B-021/B-022 would still
read `review`) nor the descope alone (no mechanism for step 5-6) would leave
B-023 selectable per `docs/11` section 7 ("all dependencies are done") — the
merge is what does.

### Changes

**`src/CommandoWar.Sim/Domain.fs`**: `PlayerIntent` gains `Suppress of
target: AgentId` (a known contact, never a bare `Cell` or a "suspected",
id-less target — no suspected-threat model exists). `DecisionReason` gains
`TargetNotKnown`, the pre-planned `docs/05` section 7 vocabulary case
earmarked for exactly this system.

**`src/CommandoWar.Sim/Commands.fs`**: `Command.suppress`, the `Command.moveTo`
precedent.

**`src/CommandoWar.Sim/Appraisal.fs`**: `appraise` matches on `order.Intent`:
`MoveTo` keeps its existing four-stage pipeline; `Suppress` appraises on
stage 2 alone (`Accepted` if the named contact is in `threats`, else
`Unable(TargetNotKnown)`) and never reaches stage 3/4. `cellPressure` /
`routeExposure` / `exposedCells` gain a `suppressedThreats: AgentId[]`
parameter and zero a threat's contribution while its own
`AgentState.SuppressionBand` is latched — reusing the already-canonical,
symmetric TASK-033/034 hysteresis latch (whatever raised it: an ordered
`Suppress` or incidental automatic engagement, TASK-031), not a new "who is
suppressing whom" bookkeeping structure.

**`src/CommandoWar.Sim/Commitment.fs`**: new `SuppressCommitment = {
Command: CommandId; Target: AgentId }` and `Commitment.Suppressing of
SuppressCommitment` (the exact shape `docs/05` section 9 already named).
`Commitment.ofAgent` gains a match arm keyed on `order.Intent = Suppress
target` with `Destination = None` (a `Suppress` order never writes one) — a
`Suppressing` commitment has no fulfilled state; it ends only by supersession.

**`src/CommandoWar.Sim/Canonical.fs`**: `FormatVersion` **7 -> 8**.
`writeOrder` gains the `Suppress` case (tag `1`, then the target `AgentId`);
`writeReason` gains `TargetNotKnown` (tag `2`, no payload). Both sit inside
the already-canonical `AgentState.Order`/`.Disposition` sections (TASK-028,
format 4) — a byte-layout change to an existing section, not a new
`topLevelSections` entry, so a `Suppress`-order difference surfaces through
the existing per-agent `"Agent[N]"` `firstDifferingSection` fallback.
`Commitment.Suppressing` is NOT written — `Commitment` stays derived
(TASK-030 precedent).

**`src/CommandoWar.Sim/ReplaySerialisation.fs`**: `intentText`/`parseIntent`
gain `suppress <targetAgentId>` — the grammar's own doc comment had already
anticipated this ("the grammar has room for `hold`/`suppress`/`assault`/
`withdraw` ... without a version bump"), so `ReplaySerialisation.FormatVersion`
stays `1`.

**`src/CommandoWar.Sim/Simulation.fs`**:
- `commandIntake`: the `TargetOutOfBounds` bounds check applies only to a
  `MoveTo` target `Cell`; a `Suppress` target is an `AgentId`, existence-
  checked at Appraisal (never authoritative agent state at intake, risk
  R-023). `CommandAccepted`'s `Cell` field reports the recipient's own
  position for a `Suppress` command (it has no `Cell` target of its own).
- `appraisal`: `newBand` is now precomputed for every agent up front
  (`newBands`), not inline per iteration, so a reappraisal decision for one
  agent can see whether a *different*, later-in-id-order agent's
  `SuppressionBand` just flipped. A third reappraisal trigger,
  `threatSuppressionChanged` (any agent's `SuppressionBand` flipping this
  tick, global scope — the knowledge-change precedent), resets every
  non-fulfilled `Refused`/`Unable` order, not only the appraising agent's
  own state. `suppressedThreats` (every currently-latched agent's id) is
  threaded into `Appraisal.appraise`. `fulfilled`/`destination` now match on
  `o.Intent` (`Suppress` is never fulfilled, never writes a `Destination`).
- `commitmentAndLocalAction`: a `Suppress` order's establishment reports
  `CommitmentEstablished` at the named contact's last-known cell (looked up
  in `s.TacticalKnowledge` at establishment time, falling back to the
  agent's own position only if the contact expired the same tick — otherwise
  unreachable, since `Accepted` requires the contact to be known).
- `combat`: a shooter with a `Suppressing` commitment narrows its candidate
  set to just its one named contact (still gated by this tick's actual
  `VisibleContacts`, range, and line of fire via the unchanged
  `Combat.chooseTarget`) instead of every `VisibleContacts` entry.

**`src/CommandoWar.Sim/Diagnostics.fs`**: `orderAppraisalOverlays` computes
`suppressedThreats` from `world.Agents` (`SuppressionBand` filter) and
threads it into its own `Appraisal.appraise` re-derivation call, matching
`Simulation.appraisal`'s computation.

**`src/CommandoWar.Headless/{DiagnosticRender.fs, AppraisalDemo.fs,
Program.fs}`**: exhaustive-match arms for `Suppress`/`TargetNotKnown`/
`Suppressing` (`commitmentText`, the two `reasonText` copies, the CLI's
intent describer). `AgentCommitment`'s SVG ring gains a filled-orange
(`#dd6b20`, the `FireLine` "hit" colour — both read as "active fire") case
for `Suppressing`, alongside the existing filled-teal `Holding` / hollow
`Moving`.

**`src/CommandoWar.Headless/Corpus.fs`**: `ScenarioOrder` now carries a
`ScenarioIntent` (`MoveOrder | SuppressOrder`) instead of a bare `Target:
Cell`; a new `suppressOrder` builder alongside `order`; `commandsOfSpec`
dispatches on the intent. New corpus entry `suppress-relieves-exposure`
(12 x 8, 2 friendlies + 1 hostile, 10 ticks): the exact `exposed-approach`
geometry for the `Refused` friendly, plus a second friendly at (10,1) given
a `Suppress` order against the known hostile on the same tick. Even in the
worst case every shot misses (`SuppressionConfig.GainOnMiss = 150`,
`DecayPerTick = 50`, net +100/tick), the hostile's `Suppression` crosses
`SuppressionBandEnter = 500` by the end of tick 5, so the flip is
deterministic regardless of the actual PRNG-drawn hit/miss sequence (a
scratch probe against the real build found it happening by tick 4, since
several shots landed as hits).

**Tests**: `tests/CommandoWar.Sim.Tests/SimulationTests.fs` gains 5 new
facts (a `Suppress` order naming an unknown contact is
`Unable(TargetNotKnown)`; naming a known one is `Accepted` with no
`Destination` and a `Suppressing` commitment; `Appraisal.routeExposure`
zeroes a suppressed threat's contribution directly at the pure-function
level; a threat's `SuppressionBand` flip reappraises a *different* agent's
`Refused` order to `Accepted`, using the identical `withSuppression`-style
direct-field-mutation the existing suppression-band-latch test uses, no
combat RNG involved; a `Suppressing` agent fires at its named target even
when a nearer contact is also visible) plus one extension to the existing
`Commitment.ofAgent` enumeration fact. `DeterminismPropertyTests.fs` and
`CorpusTests.fs` get defensive `Suppress`/`Suppressing`/`TargetNotKnown`
match arms (unreachable given those generators' MoveTo-only scope, but kept
exhaustive rather than silently falling through a wildcard).

**Full canonical re-pin** (`FormatVersion` 7 -> 8): `content/replays/*`
(`cwheadless corpus --regenerate`, all 13 entries — 12 existing +
`suppress-relieves-exposure`), the shared fixture
(`cwheadless fixture`), `envelope-full.{cwreplay,md}` (not part of
`Corpus.all`, re-pinned by hand via `cwheadless replay-file`, the
TASK-032/033/034 precedent), and every hardcoded hash literal across
`FixtureTests.fs`, `ScenarioTests.fs`, `CorpusTests.fs`, `TerrainTests.fs`,
`SightTests.fs`, `PathfindingTests.fs`, `CanonicalHashTests.fs`, and
`DiagnosticsTests.fs` (the widely-reused shared-fixture initial/final hash,
five `Assert.Equal(_, Canonical.FormatVersion)` literals, and the
`exposed-approach` tick-1 hash the `AppraisalDemo.loadExposedApproachFrames`
fact and Godot's `--selfcheck` both pin). `content/fixtures/SPIKE-FIXTURE.md`
deliberately NOT touched — frozen at format 4 since TASK-028, the
established precedent (TASK-032/033/034 didn't touch it either).
`content/diagnostics/*`: the `render`-verb-covered goldens regenerated via
the committed `README.md` commands; the nine corpus-entry-scoped goldens
(not covered by any CLI verb) via a throwaway `dotnet fsi` script calling
the identical `Corpus.commandsOf` / `DiagnosticRender.runFrames` / `.Ascii` /
`.Svg` helpers each `DiagnosticsTests.fs` fact uses (script not committed).
Behaviour-neutral everywhere except `suppress-relieves-exposure` itself
(genuine new `Suppress`-order and `Suppression` state) — tick counts and
event counts unchanged on every other entry, the fixture, and
`envelope-full`.

**`src/CommandoWar.Client.Godot/{README.md, src/AppraisalDemoScene.cs}`**:
pinned `--selfcheck` hash updated `0xB1EBA36EC0A977F4` ->
`0x5D5A30C0DF64AC93` for the moved `exposed-approach` tick-1 hash.

### Verification

- `dotnet build CommandoWar.slnx -c Release`: `0/0` before and after, zero
  warnings throughout (`TreatWarningsAsErrors` on `CommandoWar.Sim` and
  `CommandoWar.Headless` caught every incomplete-match site the new
  `PlayerIntent`/`DecisionReason`/`Commitment` cases touched).
- `dotnet test CommandoWar.slnx -c Release`: `287 -> 293` green (6 new
  `SimulationTests` facts; no test removed, none skipped).
- `cwheadless corpus`: `13/13` PASS; `--regenerate` twice in a row confirmed
  byte-identical via a direct directory diff (`diff -rq` against a snapshot
  of the first regeneration).
- `cwheadless fixture`: format `8`, `36` events unchanged (behaviour-neutral
  — the fixture issues no `Suppress` order).
- `cwheadless replay-file content/replays/envelope-full.cwreplay`:
  checkpoints OK at canonical `8`, `24` ticks / `78` events unchanged
  (enemy-free, one-`MoveTo` scenario — byte-layout-only re-pin).
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package
  --include-transitive`: `FSharp.Core` only.
- Source scan of `src/CommandoWar.Sim` for
  `float|stopwatch|datetime|system\.random|godot`: clean (the same eight
  pre-existing comment-only mentions as every prior task's scan; nothing new).
- `git status --porcelain`: matches this task file's "Allowed scope" exactly,
  plus the pre-existing, untouched, unrelated `src/CommandoWar.Client.Godot/
  project.godot` edit already present before this session started.

### Deviations and unresolved issues

- Godot's `--selfcheck` pinned hash was updated for the moved
  `exposed-approach` tick-1 hash but not independently re-run through Godot
  in this session (no Godot install here) — the TASK-034 precedent exactly.
  Flagged for Dave to confirm `0x5D5A30C0DF64AC93` on his machine before
  accepting.
- `CommandAccepted`'s `Cell` field reporting the recipient's own position for
  a `Suppress` command means the HTML narration panel's generic
  `"command-accepted"` sentence reads "order accepted, destination (x,y)"
  where `(x,y)` is the agent's own (unmoving) cell — technically accurate
  but not obviously a `Suppress` order at a glance. No new event or
  `EventMarker` field was added to distinguish it (would need threading
  intent through `Diagnostics.EventMarker`, a materially larger change for a
  cosmetic narration gap) — flagged as a follow-up, not fixed here.
- No dedicated new `CanonicalHashTests` fact for the format-8 bump: unlike
  TASK-034's `HostileTacticalKnowledge` (a genuinely new `topLevelSections`
  entry), this bump is a byte-layout change inside an *existing* section, so
  the existing per-agent `"Agent[N]"` fallback in `firstDifferingSection`
  already covers it without a new label — confirmed by inspection, not by a
  new isolated fact.

### Documents updated

`docs/04_SIMULATION_SPEC.md` (sections 12.5, 12.6, 12.8, 13, 17),
`docs/05_COMMAND_AND_AGENT_AI.md` (sections 4, 5, 7, 9, 12, 14, 16),
`docs/07_VERTICAL_SLICE.md` (section 8), `docs/09_TEST_STRATEGY.md`
("suppression reverses refusal"), `docs/11_BACKLOG.md` (new TASK-037 row;
B-021 and B-022 rows `review -> done`, reduced scope, explicit descope
recorded; B-023 row's dependency list now fully `done`; B-030 row notes the
pulled-forward slice, stays `proposed`), `docs/12_PROGRESS_LEDGER.md` (this
entry + summary counters), `PROJECT_STATE.yaml`, this task file, this
ledger entry.

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-16). Godot's `--selfcheck` pinned hash
  (`0x5D5A30C0DF64AC93`) still needs independent confirmation on Dave's
  machine — flagged, not a blocker. Merged to `main` (`--no-ff`, branch
  `task-037-suppress-order-and-dependency-closeout` deleted; not pushed).
