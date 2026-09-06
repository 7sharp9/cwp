## 2026-09-06 - Tooling and debug-display design note - scenario authoring and the developer debug display

**Owner:** Dave with coding-agent assistance
**Source revision:** `bb02990` (Revise TASK-020 and draft TASK-021), plus this
session's uncommitted control-plane work
**Environment:** Windows 11 Pro 26200 (25H2); .NET SDK 10.0.303
**Status change:** none. `docs/notes/2026-09-06-tooling-and-debug-display.md`
created; `docs/11_BACKLOG.md` section 3 gains B-049 and B-050 (`proposed`). No
task file, no source, no gate/phase/decision change. This is exploratory
analysis, not implementation work.

### Why this note

The standing review asked two developer-tooling questions now that the
deterministic core is done and P3 is scenario- and determinism-heavy: how
scenarios are authored, and how a developer sees what a tick did.

### Findings

**Thread 1 — scenario / corpus authoring.** Verified against
`src/CommandoWar.Headless/Corpus.fs` (the `rawScenario` / `worldOf` / `wall` /
`costly` helpers; `Corpus.all`), `content/replays/*.cwlog` (grammar `version 1`
then `<tick> <agentId> move <x> <y>` — the only command form), `CORPUS.md`, and
the `SimulationTests.fs` overlap. The typing cost is low; the real costs are
hand-worked geometry, `Corpus.fs` ↔ `.cwlog` drift, and `Corpus.fs` ↔
`SimulationTests.fs` duplication. A text/on-disk scenario format is premature
(that is B-024, P4) and would duplicate `RawScenario` + `Scenario.validate` and
collide with B-045 (the mandatory-before-G3 replay-command serialisation
decision, per `docs/ledger/2026-09-06-TASK-020-revised.md`). A shared **F#
fixture builder** generalised from the existing helpers — authoring the command
schedule next to the deployment, emitting both the corpus inputs and the
`SimulationTests` world — is worth it, is a refactor not new machinery, and is
well-placed across the B-045 format transition. Timing: after B-045 and
TASK-022, before B-015's scenario-heavy perception work. Proposed as **B-049**.

**Thread 2 — developer debug display.** Verified against
`src/CommandoWar.Sim/Diagnostics.fs` (`DiagnosticFrame`, the `Overlay` DU) and
`src/CommandoWar.Headless/DiagnosticRender.fs`. `DiagnosticRender.Html` is
already a self-contained multi-tick SVG scrubber with a slider and Prev/Next,
driven by `cwheadless render <target> --format html`; the review's "tick
scrubber over a replay" already exists. The P3 loop is headless, and the missing
piece is **rendering the first divergent tick**: `Divergence.diagnose` /
`Canonical.firstDifferingSection` produce the data in text but nothing draws it.
Overlay toggles and click-a-cell inspection are small presentational additions
to `DiagnosticRender.Html`, best folded into whichever P3 task first needs them
(the `AGENTS.md` / `docs/09` section 8 diagnostic rule already forces that task
to touch the renderers) rather than done standalone. A live/watch-mode viewer is
expensive (file watching or an HTTP server in a pure-CLI project) and mostly
matters when driving the sim interactively — a P4 client concern. B-029 ("render
the frame in Godot") depends on B-017 / B-027 and on standing up the G3-gated
client; not pulled forward. Proposed as **B-050**: a divergence-visualisation
`cwheadless` verb.

### Verification

- Source read-only checks: `Corpus.fs`, `DiagnosticRender.fs`, `Diagnostics.fs`,
  `Program.fs` (`render` / `compare` verbs), `content/replays/converging-routes.cwlog`,
  `content/replays/CORPUS.md`.
- Cross-checked B-029 dependencies and status in `docs/11_BACKLOG.md` section 4,
  the `.cwlog` legacy designation and B-045 in
  `docs/ledger/2026-09-06-TASK-020-revised.md`, and R-009 status in
  `docs/10_RISK_REGISTER.md`.
- Not run: no build or test — analysis only.

### Evidence

- `docs/notes/2026-09-06-tooling-and-debug-display.md` (the analysis).
- `docs/11_BACKLOG.md` section 3 diff (B-049, B-050).

### Deviations and unresolved issues

- B-049 depends on B-045, which has no task file yet. B-050 depends only on
  B-012 (done). Neither is implementation-ready; both need a reviewed task file
  before selection (`docs/11` section 3 rule).
- The HTML-viewer overlay toggles and cell-inspection panel are deliberately
  not given backlog rows — the note recommends doing them inside P3 tasks
  opportunistically. If they keep being deferred, promote them then.
- The review's G3-narrowing recommendation is explicitly out of scope for the
  note and remains a separate discussion for Dave.

### Documents updated

- `docs/notes/2026-09-06-tooling-and-debug-display.md` (new)
- `docs/11_BACKLOG.md` (section 3: B-049, B-050 `proposed`)
- `docs/12_PROGRESS_LEDGER.md` (this index row)
- this entry

### Review

- Reviewer: Dave
- Accepted: pending
- Notes: analysis and backlog proposals only. B-049 / B-050 need task files
  before they can be selected.
