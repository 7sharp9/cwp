# Tooling and debug-display design note

Status: exploratory analysis for Dave, 2026-09-06
Author: coding-agent assistance, from the standing review
Scope: no task files, no source changes. Proposes backlog rows B-049 and B-050
(`docs/11_BACKLOG.md` section 3, `proposed`).

Two questions the standing review raised about developer tooling, now that the
deterministic core is done and P3 (the headless command loop) is scenario- and
determinism-heavy: how scenarios get authored, and how a developer sees what a
tick did.

---

## 1. Multi-agent scenario and corpus authoring

### Recommendation

Do **not** build a text scenario format now, and do not touch the corpus
authoring path before B-045 (the production replay-command serialisation, already
mandatory before G3) lands. Let TASK-022 add its two corpus entries the current
way. Then a small task — sitting alongside or just after B-045, before B-015 —
generalises the existing hand-coded helpers in `Corpus.fs` into one **F# fixture
builder** that authors a scenario and its per-agent command schedule as a single
value and emits both the corpus inputs and the equivalent `SimulationTests`
world. That is backlog row **B-049**.

### What authoring a scenario costs today

A corpus entry is four artefacts:

- a private `…World ()` function in `src/CommandoWar.Headless/Corpus.fs` that
  calls `worldOf (rawScenario "id" W H [friendly] [terrain] objective extraction)`;
- an `Entry` record in `Corpus.all` (name, description, an initial-state note,
  the builder, a tick count);
- a hand-authored `content/replays/<name>.cwlog` command file — grammar
  `version 1` then `<tick> <agentId> move <x> <y>`, the only command form the
  format has;
- a generated `content/replays/<name>.md` hash table (`Corpus.renderTable`, via
  `cwheadless corpus --regenerate`) and a row in `CORPUS.md`.

`rawScenario` already hard-wires one "reach" objective, one extraction area, no
enemies, no targets, so the real authoring surface for a movement scenario is
small: grid, friendly deployments, terrain runs (`wall`, `costly`), objective
and extraction cells, tick count, command schedule.

The cost is not the typing. It is three things:

1. **Geometry by hand.** Working out which cells make two routes cross on the
   same tick, or which ring of walls forces `NoPath`, is trial and error against
   a mental model of A* with N/E/S/W tie-breaking.
2. **The scenario and the command file drift apart.** The deployment lives in
   `Corpus.fs`; the `move` targets live in the `.cwlog`. Change one and the
   other is silently stale until a hash moves.
3. **Duplication with `SimulationTests.fs`.** A corpus entry and the
   `SimulationTests` fact that exercises the same geometry with direct
   assertions are near-identical worlds authored twice. TASK-022 alone adds five
   new facts and two entries over overlapping geometry.

### Why not a text scenario format

`Corpus.fs` says it directly: the non-fixture entries "are not authoritative
game content and need no on-disk format (backlog B-024)". A text format means a
second parser, a second validator, and a third versioned format to keep in step
with `ScenarioContent.Version`, `.cwlog`, and whatever B-045 picks. Building it
for test vectors, before any authoring exists outside the test suite (that is
P4, B-024 / B-025), is premature and duplicates `RawScenario` +
`Scenario.validate` for no gain.

### Why an F# builder, and why after B-045

An F# fixture builder is a refactor of code that already exists — `rawScenario`,
`worldOf`, `wall`, `costly` are its seed — not new machinery. The useful move is
to author the **command schedule next to the deployment** and derive both the
`WorldState` and the `RecordedCommand[]` (today's `.cwlog`) from one value. That
removes drift (2), lets one authored value back both a corpus entry and a
`SimulationTests` fact (3), and gives a place to add a geometry helper (1) later.

The timing is set by B-045. TASK-020-revised designated `.cwlog` a "legacy
fixture-script format, not the production replay-command format" and made B-045 —
the real serialisation decision — mandatory before G3. A builder that generates
the command stream in memory is exactly what you want across that transition:
when B-045 picks a format, the builder emits it and the corpus regenerates, with
no hand-authored command files to migrate. The opposite move — investing in a
text scenario-plus-command format now — collides head-on with B-045: it would be
designing a command serialisation informally, in the test corpus, ahead of the
task meant to decide it.

The P3 command-loop tasks that need many scenarios (B-015 perception, B-017
appraisal, B-019 combat, B-022 enemy doctrine, B-023 the canonical refusal) all
need enemies, line-of-sight geometry, cover, and command schedules richer than
`move` — the things `rawScenario` hard-wires away and `.cwlog` cannot express.
So the builder should also admit enemies, targets, and cover. Its shape depends
on B-045 and on B-015's first real multi-command scenario, which is why it comes
after B-045 and TASK-022, not before.

### Proposed: B-049

> **B-049 (P3, G3, proposed, S–M).** Shared F# fixture builder for the test
> corpus and cross-module scenario tests. Generalise `Corpus.fs`'s
> `rawScenario` / `worldOf` / `wall` / `costly` into one builder that authors a
> scenario (grid, friendly and enemy deployments, terrain runs, cover,
> objective/extraction) and a per-agent command schedule as a single value, and
> emits both the corpus `WorldState` + command stream and the equivalent
> `SimulationTests` world — removing the `Corpus.fs` ↔ `.cwlog` drift and the
> `Corpus.fs` ↔ `SimulationTests.fs` duplication. Depends on B-045 (command
> serialisation) and follows TASK-022. Not a text/on-disk format (that stays
> B-024). Not authoritative content.

---

## 2. Godot / Mibo developer debug display

### Recommendation

Do not pull B-029 forward and do not build a live headless viewer before P4. The
file-based HTML scrubber that `cwheadless render --format html` already produces
is the headless form of "the developer overlay can explain any state transition"
(`docs/07` functional acceptance criterion 11), and client work is G3-gated for
reasons that still hold. Keep extending `DiagnosticFrame` with each P3 system's
state as an `Overlay` case — already mandated by `AGENTS.md` and `docs/09`
section 8, so not new work. The one genuinely missing headless capability worth
a task is a **divergence-visualisation verb**: backlog row **B-050**.

### What already exists

`DiagnosticFrame` (`src/CommandoWar.Sim/Diagnostics.fs`) is framework-neutral and
already carries the terrain layers (elevation, passability, movement-cost,
opacity), directional cover, agents (id, side, cell, sub-cell progress,
destination), this-tick event markers, an open `Overlay` DU (`Cells`,
`SightRay`, `PlannedPath`, `Reserved`), and the determinism trio.

`DiagnosticRender` (`src/CommandoWar.Headless/DiagnosticRender.fs`) renders it
three ways, and **`Html` is already a self-contained multi-tick scrubber**: one
SVG per tick, a range slider plus Prev/Next, inline CSS and JS, byte-deterministic.
`cwheadless render <fixture|demo|los|path|command-log> --format html` produces
it. So of the review's wish-list — live per-tick frames, a tick scrubber over a
replay, overlay toggles, first-divergence highlighting, click-a-cell inspection —
the **scrubber over a replay already exists**.

### What actually tightens the P3 loop

The P3 dev loop is headless: change a rule, run `dotnet test` / `cwheadless
corpus`, and when something is wrong, work out *why* a tick came out as it did.
Against that:

- **First-divergence inspection is the real gap.** `Divergence.diagnose` /
  `Canonical.firstDifferingSection` already name the first bad tick and the
  first differing canonical section *in text*. Nothing renders it. Seeing tick
  N−1 / N / N+1 of both runs, or one frame with the diverging agent and section
  highlighted, is what turns a determinism regression from a hash-diff hunt into
  a glance. The data is all there; it needs an `Overlay` case (a "divergence"
  marker) and a `cwheadless` sub-verb. This directly serves R-009 (open, P4,
  I5) and every P3 task touches authoritative state.
- **Overlay toggles and more overlay cases** matter as the overlay set grows
  (perception → a visibility/known overlay, appraisal → a reason marker,
  TASK-022 → `Obstructed`). Adding a case is already required by the diagnostic
  rule. A per-overlay checkbox in `DiagnosticRender.Html`'s script is a few
  lines of presentational code — worth doing **inside** whichever P3 task first
  makes "all overlays drawn always" unreadable, not as standalone work.
- **Click-a-cell inspection** (terrain, occupancy, cover, LoS for the cell under
  the cursor) is an SVG tooltip / side panel populated entirely from
  `DiagnosticFrame` data. Presentational, medium value; the ASCII `--layer`
  heat-map already answers "what is the terrain here" for headless work. Same
  disposition as overlay toggles: fold into a P3 task opportunistically.
- **Live streaming / a watch-mode viewer** is the expensive option: file
  watching or a local HTTP server in a project that is currently a pure CLI.
  The file-based HTML scrubber already covers "step through a finished run",
  which is almost all headless debugging. Live view mostly matters when you are
  *driving* the sim interactively — a client concern (B-026 input, B-029
  overlay), i.e. P4.

### Why not pull B-029 forward

B-029 is "render the `DiagnosticFrame` *in Godot*". Its value is the P4
player/designer loop — perception and appraisal on the real isometric map. It
depends on B-017 and B-027, which do not exist, and standing up the Godot client
is exactly the P4 work the roadmap gates on G3. The P3 dev loop is served by the
headless renderers, which already exist and need only incremental `Overlay`
cases plus the divergence view.

### Proposed: B-050

> **B-050 (P3, G3, proposed, S).** Divergence-visualisation renderer. A
> `cwheadless` verb (e.g. `render-divergence <log-a> <log-b>` or `compare
> --render`) that renders the first differing tick of two replays — ASCII, SVG,
> and into the existing HTML scrubber — with the diverging agent(s) and the
> first differing canonical section highlighted, via a new `Overlay` case. Uses
> the data `Divergence.diagnose` already produces; no new determinism
> machinery. Serves R-009 and the determinism-heavy P3 work (B-015 onward).

### Not proposed as rows, do opportunistically

HTML-viewer overlay toggles and click-a-cell inspection are small presentational
additions to `DiagnosticRender.Html`. The disciplined path is to fold each into
the P3 task that first needs it (the diagnostic-extension rule already forces
that task to touch the renderers). Promote to a backlog row only if they are
deferred repeatedly.

---

## Out of scope for this note

The review's strategic recommendation to narrow G3 to a thin behavioural slice
is a separate, larger discussion for Dave, not touched here.
