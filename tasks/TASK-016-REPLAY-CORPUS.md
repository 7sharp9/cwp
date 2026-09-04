# TASK-016: Divergence diagnostics and replay corpus infrastructure

Status: done
Owner: Dave
Phase: P2
Gate: G2 (deterministic core); realises backlog B-012
Size: M

## Objective

Turn the single spike fixture into a committed, regenerable **replay corpus** so
a determinism regression fails a build instead of rotting silently
(`docs/09_TEST_STRATEGY.md` section 2.4 "Maintain a small replay corpus";
`docs/04_SIMULATION_SPEC.md` section 17 "A divergence report should identify the
first bad tick").

1. `content/replays/` holds several named entries. Each entry is a
   `<name>.cwlog` (existing command-log v1 format) plus a committed per-tick
   authoritative-hash table `<name>.md` (mirroring `content/fixtures/SPIKE-FIXTURE.md`:
   command-log name, tick count, initial hash, final hash, domain-event count,
   and the tick -> hash column). `content/replays/CORPUS.md` lists every entry
   and the single regeneration command.
2. The corpus includes the existing spike fixture and at least two new minimal
   hand-built scenarios that exercise the TASK-015 executor: an agent detouring
   an impassable wall, and an agent whose `MoveTo` yields `MovementBlocked`. A
   two-agent "converging routes" entry pins today's no-reservation behaviour
   (B-011b will re-pin its hashes).
3. A `cwheadless corpus` verb runs every entry against its committed table and
   exits non-zero on the first divergence, printing a divergence report (entry
   name, first bad tick, expected/actual hash, and — from the fresh run — the
   random draw count; a self-double-run divergence additionally prints the first
   differing canonical section). Exit 0 on a full match. `corpus --regenerate`
   rewrites every table from a fresh run.
4. An in-suite data-driven determinism test (`CorpusTests.fs`, `[<Theory>]` over
   the corpus files copied next to the test assembly, the `content/diagnostics/*`
   copy-glob precedent) so `dotnet test` catches a regression without an opt-in
   CLI run.

## Scope down (deferred to B-012b)

- Generative / FsCheck-style determinism property tests: needs a new dependency
  (`docs/09` section 2.2). **B-012b.**
- Component-level or per-agent subhashes in the divergence report: touches
  `Canonical.encode` carefully (`docs/04` section 17 "Component-level subhashes
  are desirable once the world grows"). **B-012b.**
- A general on-disk scenario+replay bundle format or importer. **B-024.**

## Central decisions (recorded in the ledger)

- **Corpus entry format.** Reuse `.cwlog` v1 plus a committed Markdown hash
  table per entry; no new format. The spike-fixture entry's initial state is
  `Fixture.initialState ()`; the three new entries get small `RawScenario`
  builders in a new `Corpus` module in `CommandoWar.Headless`, validated through
  `Scenario.validate` and instantiated through `World.ofScenario`, exactly as
  `PathDemo` / `LosDemo` are focused fixtures of their own rather than
  overloading `DemoScenario`.
- **Initial-state resolution.** A `corpus` verb that owns a name -> `WorldState`
  registry (`Corpus.all : Entry[]`), so `cwheadless replay` stays hardwired to
  `Fixture.initialState ()` and is untouched.
- **New pins are genuinely new.** The corpus hash tables are values never
  committed before. The spike-fixture entry's 40-value table is the same
  sequence already in `SPIKE-FIXTURE.md`; `CorpusTests` cross-checks it against
  `Fixture.run ()` so it is not an independent re-pin of `FixtureTests.fs` /
  `SPIKE-FIXTURE.md`. `cwheadless fixture` output is byte-unchanged.
- **In-suite vs CLI: both.** The `[<Theory>]` is the regression guard (the
  `BenchmarkTests` "cheap in-suite flag" precedent); the verb is for humans and
  CI localisation.

## Diagnostics

B-012 adds no authoritative spatial or tactical state — it is evidence
infrastructure over existing `Simulation` / `Replay` / `Divergence` output — so
the `docs/09` section 8 standing rule does not apply. The completion report
confirms this rather than adding an overlay (the TASK-014 precedent).

## Allowed scope

- `src/CommandoWar.Headless/Corpus.fs` (new), `CommandoWar.Headless.fsproj`
  (compile entry), `src/CommandoWar.Headless/Program.fs` (the `corpus` verb +
  usage line);
- `content/replays/` (new: `CORPUS.md`, `<name>.cwlog`, `<name>.md` per entry);
- `tests/CommandoWar.Sim.Tests/CorpusTests.fs` (new),
  `CommandoWar.Sim.Tests.fsproj` (compile entry + copy glob);
- `.gitattributes` (`content/replays/**` eol=lf);
- `docs/09_TEST_STRATEGY.md` section 2.4 realisation note;
- control-document updates (this task, backlog, ledger, `PROJECT_STATE.yaml`).

## Forbidden scope

- Changing `Canonical.encode` / `Canonical.FormatVersion`,
  `Setup.sixAgentWorld`, the shared fixture parameters, or the pinned fixture
  hashes / 33-event count.
- Any multi-agent reservation, deadlock, formation, or sub-cell movement logic
  (all B-011b); any new gameplay-phase behaviour or a gameplay PRNG draw.
- A new package (FsCheck included) or a new project.
- A new on-disk content format or importer (B-024).
- Component subhashes that alter the canonical image.
- Changing any existing `cwheadless` verb's output.
- Touching the client spikes, `src/_scratch`, or `bench/CommandoWar.Benchmarks/`.

## Acceptance criteria

- [x] `content/replays/` holds `spike-fixture`, `wall-detour`, `blocked-goal`,
      and `converging-routes` entries (`.cwlog` + `.md` each) and `CORPUS.md`.
- [x] `cwheadless corpus` exits 0 when every table matches; exits non-zero and
      prints the first divergent tick when a `.cwlog` is perturbed.
- [x] `cwheadless corpus --regenerate` is idempotent: regenerating every table
      leaves the committed files byte-identical.
- [x] `CorpusTests.fs` `[<Theory>]` runs every entry and fails on any table
      mismatch; `dotnet test` grows from 153.
- [x] `cwheadless fixture` unchanged: `0xF2F3DF0D820AD9AC` /
      `0x838D3AE7DBFB735D`, 33 events, format 1.
- [x] `dotnet build CommandoWar.slnx -c Release` = 0 warnings, 0 errors.
- [x] `dotnet list src/CommandoWar.Sim package --include-transitive` =
      `FSharp.Core` only; source scan of `src/CommandoWar.Sim` clean.
- [x] `docs/09` section 2.4, backlog rows, ledger index row + detail file,
      `PROJECT_STATE.yaml`, task status updated.

## Required verification

- `dotnet build CommandoWar.slnx -c Release`
- `dotnet test CommandoWar.slnx -c Release` before and after
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus` (exit 0)
- `dotnet run --project src/CommandoWar.Headless -c Release -- corpus --regenerate`
  then `git status` (no change) — idempotence
- perturb one `.cwlog`, `cwheadless corpus` exits non-zero at the first
  divergent tick, then revert
- `dotnet run --project src/CommandoWar.Headless -c Release -- fixture` byte-compared
- `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
- source scan of `src/CommandoWar.Sim`
- `git status`

## Alternative

If the corpus verb plus the initial-state registry pushes past size M, land the
committed corpus and the in-suite data-driven determinism test only (no new
verb) as TASK-016 and split the CLI verb into TASK-016b. If a corpus entry that
meaningfully exercises multi-agent ordering cannot be built without reservation
logic, pin the current no-reservation two-agent behaviour and record that
B-011b must re-pin that one entry.

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next task.
