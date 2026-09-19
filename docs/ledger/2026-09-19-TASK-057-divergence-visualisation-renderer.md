# TASK-057: Divergence-visualisation renderer

Owner: Dave (implementing agent session)
Source revision: `main`, after TASK-054/TASK-055's acceptance.
Environment: `dotnet` `10.0.303`, Windows 11 (no Godot editor step needed --
a headless CLI-only tool, no client change).

## Selection

Not scoped in advance. Dave asked via `AskUserQuestion` to pick the next
task from three candidates named in the session's launch context (B-050
divergence-visualisation renderer, B-016b communication mechanics, B-043
Mibo 5.x reconsideration spike) plus an open "something else" option, after
TASK-054/TASK-055 were both accepted. Dave's answer: do B-050 then B-016b,
both in this same session.

## Central decisions

None requiring `AskUserQuestion` -- the design was already fully specified
by `docs/notes/2026-09-06-tooling-and-debug-display.md` section 2's original
proposal (a `cwheadless` verb rendering the first differing tick via a new
`Overlay` case, using `Divergence.diagnose`'s existing data) and by the
existing `Overlay`/`DiagnosticRender`/`cwheadless render`/`cwheadless
compare` conventions the codebase already establishes. Judgement calls made
directly, not put to Dave:

- **Two frames, not a multi-tick diff.** The note's own minimal ask ("one
  frame with the diverging agent and section highlighted") and
  `Divergence.diagnose`'s existing "first divergence only" scope (`docs/09`
  section 2.4) both point at rendering exactly the reference and candidate
  state at the one divergent tick, not a tick N-1/N/N+1 sequence.
- **`section` text prefix over a `DiagnosticFrame` schema change.**
  Distinguishing the reference frame from the candidate frame (both at the
  identical tick number) needed some signal; prefixing the `Divergence`
  overlay's own `section` string with `"reference: "`/`"candidate: "` reuses
  the one field every renderer already treats as opaque text, instead of
  adding a label field to `DiagnosticFrame` (which nothing else needs and
  would ripple into every other renderer/test that pattern-matches the
  type).
- **SVG: two files, not one.** A single `<svg>` document cannot hold two
  root elements; `render-divergence --format svg --out PATH` writes
  `<stem>-reference.svg`/`<stem>-candidate.svg` (or prints both to stdout,
  comment-separated, when `--out` is omitted) rather than inventing a
  combined-document convention `DiagnosticRender.Svg` does not otherwise
  have.

## Investigation before drafting

- Read `docs/notes/2026-09-06-tooling-and-debug-display.md` section 2 in
  full -- the original proposal already named the shape (`render-divergence`
  verb, ascii/svg/html, a new `Overlay` case, no new determinism machinery).
- Read `Divergence.fs` (`DivergencePoint`, `DivergenceReport`,
  `Divergence.compare`/`.diagnose`) and `Canonical.firstDifferingSection`
  directly: confirmed the section label is always exactly one of a
  top-level section name or `"Agent[N]"` (never a list), which set this
  task's own scope to "the diverging agent" (singular a
  `Canonical.firstDifferingSection` can name), not a multi-agent diff.
- Read `Diagnostics.fs`'s `Overlay` DU doc comments end to end: confirmed
  `SightRay`/`PlannedPath` are the existing "caller supplies it, `Diagnostics`
  never emits it" precedent a comparison-derived overlay should follow
  (`frame`/`frameOf` build from one `WorldState`/`StepResult`; a divergence
  needs two independently replayed runs, which neither function has).
- Read `DiagnosticRender.fs` end to end (1315 lines): found every site that
  matches `Overlay` -- `Ascii`'s two exhaustive `sightRays`/`plannedPaths`
  filters plus its main overlay-line loop (exhaustive), `Svg`'s main overlay
  loop (exhaustive), and `annotations`'s several `Array.choose`/`tryPick`
  blocks (all wildcard `| _ -> None`, so no compile-time obligation, but a
  `Divergence` banner was still worth adding there for the HTML scrubber's
  own sake -- the "primary reason about a run" artefact the note names).
- Read `Program.fs`'s `cmdCompare` (the existing text-only `compare` verb
  this task's verb parallels exactly for its two-log/`--ticks` argument
  handling and `Match`/`TruncatedRun`/`Diverged` branching) and `cmdRender`
  (the `--format ascii|svg|html`/`--out PATH` convention, and the
  `emit`-to-stdout-or-file pattern).
- Grepped the whole tree for every other exhaustive match over `Overlay`
  before writing any code (not left to the compiler to discover one at a
  time): `RenderShared.fs` (Godot client) -- confirmed its only `Overlay`
  match (`ammo` lookup) is already a wildcard `tryPick`, no change needed;
  `AppraisalDemo.fs`'s `unhandled`-overlay tracker -- exhaustive, one new
  arm needed; `DiagnosticsTests.fs` -- four exhaustive sparse-filter blocks
  (`AgentAmmo _ -> None)` was the last case in each), one new arm each.

## Changes

- `src/CommandoWar.Sim/Diagnostics.fs`: new `Overlay.Divergence of section:
  string * agents: AgentId[]`, documented as caller-supplied only; added to
  the DU's own leading "B-0xx -> Case" list.
- `src/CommandoWar.Sim/Divergence.fs`: `diagnose`'s two `Replay.run` calls
  extracted into a new private `runBoth`; new `diagnoseDetailed` returning
  `Result<DivergenceReport * ReplayOutcome * ReplayOutcome, ReplayError>`.
  `diagnose` itself unchanged in signature and behaviour (both now call
  `runBoth`).
- `src/CommandoWar.Headless/DiagnosticRender.fs`:
  - `Ascii`: `| Divergence _ -> None` added to the `sightRays`/`plannedPaths`
    exhaustive filters; a new arm in the main overlay-line loop printing
    `DIVERGED: first differing section <section>  agent <ids>`.
  - `Svg`: a new `divergenceText` lookup (widens the footer by one line,
    `52 -> 66`, only when present) and a new overlay-loop arm drawing a
    `stroke="#ff00ff"` `stroke-width="4"` square around each named agent's
    cell plus the footer line.
  - `annotations` (used by `Html`): a new optional `<p class="cw-diverged">`
    banner, wildcard-matched (`tryPick`), the `SquadLeadership` one-line
    banner precedent.
- `src/CommandoWar.Headless/Program.fs`: new `agentFromSection` (parses a
  `"Agent[N]"` label back into an `AgentId[]`, empty for any other label);
  new `cmdRenderDivergence` implementing the verb described in Central
  decisions/Investigation above; `render-divergence` added to `usage()` and
  the `main` dispatch; `open System.Text` added (for `StringBuilder`, the
  ascii-format two-frame concatenation).
- `src/CommandoWar.Headless/AppraisalDemo.fs`: one new `unhandled.Add(...)`
  arm for `Divergence` (this disposable demo never produces one from its own
  committed frame).
- `tests/CommandoWar.Sim.Tests/DiagnosticsTests.fs`: `| Divergence _ -> None`
  added to the four exhaustive sparse-filter blocks; one new fact (see
  Verification).

No `CommandoWar.Sim`/`CommandoWar.Client.Godot` behaviour change beyond the
new `Overlay` case and `Divergence.diagnoseDetailed` (both additive, no
existing signature/behaviour altered); no `Canonical.FormatVersion` bump, no
existing hash re-pinned.

## Verification

- `dotnet build src/CommandoWar.Sim/CommandoWar.Sim.fsproj -c Release`,
  `dotnet build src/CommandoWar.Headless/CommandoWar.Headless.fsproj -c
  Release`, `dotnet build CommandoWar.slnx -c Release`, `dotnet build
  src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Debug`: all
  `0/0`, checked incrementally after each file's edit to isolate every
  exhaustive-match compile error to the file that caused it (confirmed the
  Investigation grep had found every site: `AppraisalDemo.fs` one error,
  then `DiagnosticsTests.fs` four warnings-as-errors-equivalent `FS0025`
  incomplete-match warnings, nothing else anywhere in the tree).
- `dotnet test CommandoWar.slnx -c Release`: `344/344` (+1, the new fact;
  ran the new fact alone first via `--filter` to confirm it in isolation,
  then the full suite).
- `cwheadless corpus`: `16/16` (unaffected, as expected -- no canonical/hash
  change).
- New `DiagnosticsTests.fs` fact (`Divergence.diagnoseDetailed's states
  render as a Divergence overlay naming the diverging agent and section, in
  every format`): reuses the exact scenario `ReplayTests.fs`'s own "a
  mutated command destination is reported as a divergence at the changed
  tick" fact already proves diverges at tick 1 on `Agent[0]` (two tick-1
  `MoveTo` commands for the same agent to two different destinations, an
  8x8 `Setup.sixAgentWorld`), then builds both frames exactly as
  `cmdRenderDivergence` does and asserts the `DIVERGED`/`Agent[0]`/agent-id
  text appears in `Ascii`, `Svg` (plus the magenta stroke), and `Html`
  (the `cw-diverged` class).
- Manual CLI smoke test (two hand-built `.cwlog` files in the session
  scratchpad, removed after use): agent 3 ordered to `(3,0)` in one log,
  `(5,5)` in the other, both from `Fixture.initialState()`.
  - Two identical logs: `MATCH: 2 tick(s) compared, all authoritative
    hashes identical -- nothing to render`, exit `0`.
  - The diverging pair, `--format ascii`: `first differing section:
    Agent[3]` / `diverged at tick: 1` printed first, then both frames in
    full, each carrying `DIVERGED: first differing section
    reference: Agent[3]  agent 3` / `...candidate: Agent[3]  agent 3`
    respectively -- confirmed the reference frame shows agent 3 at `(0,3)`
    moving to `(3,0)` and the candidate at `(1,3)` moving to `(5,5)` (one
    tick already elapsed at `--ticks 2`), exit `3`.
  - `--format svg --out <path>.svg`: wrote `<path>-reference.svg` and
    `<path>-candidate.svg`; `grep` confirmed `stroke="#ff00ff"` and the
    `DIVERGED: first differing section reference: Agent[3]` footer text in
    the reference file (and the `candidate:` equivalent in the other).
  - `--format html --out <path>.html`: wrote one file; `grep` confirmed two
    `cw-diverged` banners (one per embedded frame).
  - Scratch `.cwlog`/output files removed after use (session scratchpad
    only, never under the repository).
- `git status --porcelain`: matches this task's allowed scope exactly --
  `Diagnostics.fs`, `Divergence.fs`, `DiagnosticRender.fs`, `Program.fs`,
  `AppraisalDemo.fs`, `DiagnosticsTests.fs` modified; the new task file
  untracked. No `content/replays/`/`content/diagnostics/` file touched (no
  canonical/golden change, as expected).

## Documents updated

- `tasks/TASK-057-DIVERGENCE-VISUALISATION-RENDERER.md` (created, `Outcome`
  filled in, acceptance criteria checked).
- `docs/11_BACKLOG.md` (B-050 row: `proposed -> done`).
- `docs/12_PROGRESS_LEDGER.md` (this detail file; index row added).
- `PROJECT_STATE.yaml` (`active_work` updated).

## Review

- Reviewer: Dave.
- Accepted: yes (2026-09-19), on the self-verification evidence -- no
  client UI in this task.
