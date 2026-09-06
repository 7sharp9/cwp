# TASK-023: Continuous integration for the framework-neutral solution

Status: ready
Owner: Dave
Phase: P3
Gate: G3 (realises `docs/09_TEST_STRATEGY.md` section 6; mitigates R-022, R-009, R-016)
Size: S

## Objective

Add a GitHub Actions workflow that, on every push and pull request, restores,
builds, and tests `CommandoWar.slnx` with the pinned SDK and then runs the
headless determinism verbs, so that **the build, the test suite, and the
committed replay/fixture hashes are reproduced by a machine other than the
developer's** on every change.

Today `dotnet build` / `dotnet test` and the `171` green-test figure
(`docs/12_PROGRESS_LEDGER.md` "Pinned facts") are recorded from local runs on
one Windows 11 machine. `docs/ledger/2026-09-06-control-plane-reconciliation.md`
"Deviations" names this gap directly: "there is still no CI workflow; the '171
passing tests' figure is locally recorded, not independently reproducible ...
no B-item yet". This task is that B-item (B-048).

## Why this task exists

- `docs/09_TEST_STRATEGY.md` section 6 requires CI: "Before framework selection,
  CI should build and test only the framework-neutral projects plus any
  explicitly disposable spike projects." The framework is now selected
  (ADR-0001), so section 6's post-selection matrix applies — but its
  framework-neutral core (restore with pinned dependencies, release build,
  headless unit and property tests, deterministic scenario and replay tests) is
  the part that can and should run now, headless, before any P4 client work.
- R-022 (single-developer continuity lost across gaps or agent sessions):
  mitigation names "scripted build/test commands" and "a rehydration audit
  before continuing implementation". CI is the executable form of both — a
  green run on a clean runner *is* the rehydration audit, performed on every
  push.
- R-009 (determinism claimed but breaks): the corpus (`CorpusTests.fs`,
  `cwheadless corpus`) and fixture (`FixtureTests.fs`) already fail a build on a
  hash divergence, but only when someone runs them. CI runs them on every
  change, on a runner that matches the environment the hashes were pinned in.
- R-016 (dependency upgrades destabilise the project): a clean-runner restore
  catches a drifted transitive dependency or a broken restore that a
  warm local package cache would hide.

This does not depend on TASK-020, TASK-021, or TASK-022 and can run before or
after any of them. It touches no source and no simulation behaviour.

## Required reading

- `PROJECT_STATE.yaml`, `AGENTS.md`
- `docs/09_TEST_STRATEGY.md` sections 5 (test execution order), 6 (CI matrix),
  8 (completion standard)
- `docs/08_ROADMAP_AND_GATES.md` sections 6 (P3) and 7 (P4 — client integration
  is P4, gated on G3)
- `docs/10_RISK_REGISTER.md` R-009, R-016, R-022
- `CommandoWar.slnx` (the four framework-neutral projects; the Godot and Mibo
  client projects and `src/_scratch/` are **not** in it), `global.json`
  (`10.0.303`, `rollForward: latestPatch`)
- the four `.fsproj` / project files for their pins and build discipline:
  `src/CommandoWar.Sim/CommandoWar.Sim.fsproj` and
  `src/CommandoWar.Headless/CommandoWar.Headless.fsproj` and
  `bench/CommandoWar.Benchmarks/CommandoWar.Benchmarks.fsproj`
  (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`),
  `tests/CommandoWar.Sim.Tests/CommandoWar.Sim.Tests.fsproj` (xUnit 2.9.3,
  `Microsoft.NET.Test.Sdk` 17.14.1, FsCheck / FsCheck.Xunit 3.3.4,
  coverlet.collector 6.0.4 — all exact-version pinned; **no**
  `TreatWarningsAsErrors`)
- `src/CommandoWar.Headless/Program.fs` (`corpus` and `fixture` verb dispatch;
  the `Exit` module — `ok = 0`, `usage = 1`, `replayError = 2`, `diverged = 3`)
- `src/CommandoWar.Headless/Corpus.fs` (`Corpus.DefaultDir = "content/replays"`,
  a **repository-relative** path; `checkEntry` / `compareToTable` — the
  no-`--regenerate` path already exits non-zero on any per-tick-hash, tick-count,
  or event-count mismatch against the committed `<name>.md`)
- `content/replays/CORPUS.md`, `content/fixtures/SPIKE-FIXTURE.md`,
  `docs/12_PROGRESS_LEDGER.md` "Pinned facts" and "Shared fixture" blocks
- `tests/CommandoWar.Sim.Tests/FixtureTests.fs`, `CorpusTests.fs`,
  `DeterminismPropertyTests.fs` (what `dotnet test` already covers)

## Dependencies

- none

## Central decisions

### Scope: build `CommandoWar.slnx`, which *is* the framework-neutral boundary

`CommandoWar.slnx` contains exactly the four framework-neutral projects
(`CommandoWar.Sim`, `CommandoWar.Headless`, `CommandoWar.Sim.Tests`,
`CommandoWar.Benchmarks`). `src/CommandoWar.Client.Godot`,
`src/CommandoWar.Client.Mibo`, and `src/_scratch/` are in the repository but not
in the solution. So "CI builds the framework-neutral solution only" and "CI
builds `CommandoWar.slnx`" are the same instruction — the workflow names the
`.slnx` and never enumerates projects or reaches outside it. If a future change
adds a client project to `CommandoWar.slnx`, that is the moment to revisit this
workflow, and such a change is out of scope here.

### Decision 1 — selected-client compile: defer to P4

`docs/09_TEST_STRATEGY.md` section 6's post-selection matrix lists "selected
client compile" alongside "content validation" and "package smoke test". This
task **does not add a Godot-SDK build job.** Reasons:

- `src/CommandoWar.Client.Godot` is C#, is not in `CommandoWar.slnx`, and needs
  the Godot .NET SDK (`Godot.NET.Sdk`) and a pinned Godot version to build;
  export-template and headless-Godot concerns follow. That is a CI toolchain of
  its own to install, pin, and maintain.
- The Godot project does not change during P3. Client integration is **P4**,
  explicitly gated on G3 (`docs/08` section 7; `PROJECT_STATE.yaml`;
  `README.md` "Client and presentation work belongs to P4 and must not begin
  before G3 passes"). A client-compile job has nothing to guard until a P4 task
  touches that project.
- The natural owner is the first P4 client task (B-024 "selected-framework
  content importer" or B-025 "Bridgehead greybox map"): it adds the Godot job —
  and the "content validation" and "package smoke test" matrix rows — to this
  same workflow file (or a second workflow) when there is client code for them
  to protect.

Record in the B-048 backlog row and in this workflow's top comment that
client-compile / content-validation / package-smoke CI is deliberately deferred
to P4 and belongs with the first client task.

### Decision 2 — dependency locking: rely on the existing exact-version pins

`docs/09` section 6 says "restore with locked **or** pinned dependencies". Every
`PackageReference` in every project already carries an exact version (no
floating ranges, no wildcards); `global.json` pins the SDK with
`rollForward: latestPatch`; `FSharp.Core` is SDK-implicit-pinned. The "pinned"
half of that requirement is already satisfied, and CI restoring on a clean
runner with an empty package cache is itself the check that the pins resolve.

This task **does not add `packages.lock.json` files or `--locked-mode`
restore.** Lockfiles would add transitive-dependency pinning and restore-time
hash verification, but adding them:

- is a **source/config change** — a committed `packages.lock.json` per project,
  a `<RestorePackagesWithLockFile>` property, and the ongoing obligation to
  regenerate them on every package change — which makes it an implementation
  task, not the CI-plumbing task drafted here;
- is not needed to make CI meaningful now: the exact pins plus a cold-cache
  restore already give a reproducible dependency set.

If R-016 later justifies transitive locking (an unexplained restore-driven
output change, a supply-chain concern), it is a separate small implementation
task: generate lockfiles for all four projects, set
`<RestorePackagesWithLockFile>true`, switch the CI restore to
`--locked-mode`, and document the regeneration command. Note this option in the
B-048 row; do not create a numbered backlog item for it in this task.

### Decision 3 — runner OS: `windows-latest`

The determinism contract is **per-environment**. `docs/09` section 3 "Initial
contract" fixes replay to the same "target framework and runtime version;
architecture and operating environment", and states plainly: "Cross-platform
lockstep is not claimed. Strengthening that contract requires an ADR and
cross-target evidence." Every committed hash (`content/fixtures/SPIKE-FIXTURE.md`,
`content/replays/*.md`, the `docs/12` "Shared fixture" block) was pinned on the
developer's Windows 11 x64 machine with .NET SDK `10.0.303`.

CI runs on **`windows-latest`** so that `dotnet test` (`FixtureTests`,
`CorpusTests`), `-- corpus`, and `-- fixture` **reproduce the committed hashes**
rather than compute different ones. A job that ran on `ubuntu-latest` would be
testing a claim the project explicitly disclaims: if the Linux hashes happened
to match it would be unearned cross-platform evidence nobody should lean on,
and if they diverged the job would be red on day one over a determinism
question this task is not scoped to resolve — which would then force a per-OS
hash table (`Corpus.renderTable`, the `<name>.md` format, `Corpus.parseTable`
all change), a much larger blast radius.

Cross-platform / Linux-CI determinism is a legitimate future question. It is an
ADR-gated decision (`docs/09` section 3) with its own cross-target evidence
requirement, not a side effect of adding a CI file.

Because the committed hashes are now formally an artefact of one environment and
CI depends on that, this task adds a **one-line environment annotation** to
`content/replays/CORPUS.md`, `content/fixtures/SPIKE-FIXTURE.md`, and the
`docs/12` "Shared fixture" block: the hashes are pinned on Windows x64 /
.NET `10.0.303` and CI verifies them there; other environments are out of the
current determinism contract. This is the only content/doc change in the task
and it adds no new claim — it makes an existing implicit one explicit.

### Decision 4 — `bench/`: built by the solution build, never executed

`bench/CommandoWar.Benchmarks` is in `CommandoWar.slnx` under `/bench/` and
carries `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, so
`dotnet build CommandoWar.slnx -c Release` **already builds it** and already
fails on a warning in it. That is the desired coverage: a simulation API change
that breaks `Benchmarks.fs` fails CI, at zero extra cost, because the build step
is "build the solution".

CI **does not run the benchmarks.** They are timing measurements, not a test
oracle (`docs/09` section 2.8: "no test asserts on its numbers"; section 5.6:
"Run benchmarks only when performance-sensitive code changed"). No
benchmark-execution step is added. This needs no special handling — just the
absence of a `dotnet run --project bench/...` step.

### Decision 5 — this is a small implementation task, drafted here as a file only

Implementing TASK-023 produces: one new workflow file
(`.github/workflows/ci.yml`), the three one-line environment annotations from
Decision 3, and the control-document updates. **No source, no test, no `.fsproj`,
no hash change.** Size **S** — smaller than TASK-021. This drafting step
produces only this task file plus its backlog and ledger rows, exactly as the
TASK-021 / TASK-022 drafts did.

### No `Canonical.FormatVersion` / hash / `PROJECT_STATE` change; no ADR

- `Canonical.encode` is untouched; `Canonical.FormatVersion` stays `2`. CI
  *computes* hashes (via `dotnet test` and the verbs) but re-pins nothing — a
  divergence **fails the job**, it does not rewrite a table.
- `PROJECT_STATE.yaml` `active_work` stays `none` until Dave selects this task.
- No ADR. `docs/08` section 10 lists what needs an ADR: a change to "product
  scope, architecture, framework, determinism contract, or a gate". A workflow
  that verifies existing contracts changes none of them. This mirrors the
  TASK-021 / TASK-022 "no ADR — enforces an existing requirement" call. It also
  does **not** touch `docs/08` section 6's P3 Required-work list (unlike the
  TASK-020 revision's B-044/B-045 addition): CI's requirement already lives in
  `docs/09` section 6, and this task marks that section's core items realised
  rather than adding a new gate obligation.

## Workflow shape (for the implementer)

A single workflow file, `.github/workflows/ci.yml`, one job:

- **Triggers:** `push` (any branch) and `pull_request` targeting `main`. A
  `concurrency` group keyed on the ref, with `cancel-in-progress: true`, so a
  new push supersedes an in-flight run.
- **Permissions:** `contents: read` (least privilege; the job neither writes to
  the repo nor posts statuses beyond the default check).
- **Runner:** `runs-on: windows-latest` (Decision 3).
- **Steps, in order:**
  1. `actions/checkout` — pinned to a commit SHA with a version comment, in
     keeping with the project's "pin framework and package versions" discipline
     (`AGENTS.md`); a released major tag is the acceptable minimum.
  2. `actions/setup-dotnet` (SHA-pinned) with `global-json-file: global.json`,
     so the SDK is exactly the pinned `10.0.303` under `rollForward:
     latestPatch` — CI honours the same pin as a local build.
  3. `dotnet restore CommandoWar.slnx` — cold-cache restore is the pinned-restore
     check (Decision 2).
  4. `dotnet build CommandoWar.slnx -c Release --no-restore` — `0` warnings /
     `0` errors is enforced by the per-project `TreatWarningsAsErrors` on
     `CommandoWar.Sim`, `CommandoWar.Headless`, and `CommandoWar.Benchmarks`.
     The test project deliberately does not set it (xUnit / FsCheck analyzer
     noise); CI **does not** override that with a global `-warnaserror`.
     Widening warnings-as-errors to the test project is a one-line `.fsproj`
     change and is out of scope here — note it in the ledger, do not do it.
  5. `dotnet test CommandoWar.slnx -c Release --no-build` — the full suite:
     `FixtureTests` (pins the shared-fixture per-tick hashes), the
     `CorpusTests` `[<Theory>]` (every `content/replays/` entry vs its
     committed table), `DeterminismPropertyTests` (the three FsCheck properties
     at 200 generated cases each), and the rest.
  6. `dotnet run --project src/CommandoWar.Headless -c Release --no-build --
     corpus` — the explicit CLI form of the corpus check. With **no**
     `--regenerate`, `Corpus.checkEntry` replays every entry, verifies it
     reproduces its own hashes on a second run, then compares per-tick hashes,
     tick count, and event count to the committed `content/replays/<name>.md`;
     the process exits `3` (`Exit.diverged`) on the first mismatch, failing the
     job. This is belt-and-braces over step 5's in-suite theory (they share
     `Corpus` code) and it is the check that makes the determinism claim
     **independently reproducible on a clean runner**, not just locally
     asserted (R-009, R-022). The verb resolves `content/replays` relative to
     the working directory, so the step runs from the repository root (the
     default checkout location).
  7. `dotnet run --project src/CommandoWar.Headless -c Release --no-build --
     fixture` — a CLI **smoke** check of the `fixture` verb path. Note in the
     step comment: `cmdFixture` *prints* the fixture and its per-tick hash
     table and returns `Exit.ok` unless `Fixture.run ()` itself errors — it
     does **not** compare against `content/fixtures/SPIKE-FIXTURE.md`. The
     fixture hashes are actually pinned by `FixtureTests.fs` (step 5) and the
     `spike-fixture` corpus entry (step 6). Step 7 only proves the verb runs.
  8. `git diff --exit-code` (or a `git status --porcelain` emptiness check)
     after steps 6-7 — proves no committed file was rewritten (no stray
     `--regenerate`, no working-tree drift).
- **Optional hardening** (implementer's call, keep it cheap): a NuGet cache
  (`actions/cache` on the package directory, keyed on a hash of `**/*.fsproj`,
  the test project's package list, and `global.json` — there is no lockfile to
  key on); and a `-- corpus --regenerate` + `git diff --exit-code
  content/replays` step to prove the committed tables are byte-identical to
  what this environment regenerates (stronger than step 6, catches
  `Corpus.renderTable` prose drift too).

## Allowed scope

- `.github/workflows/ci.yml` — new; the single workflow described above.
- `content/replays/CORPUS.md`, `content/fixtures/SPIKE-FIXTURE.md` — the
  one-line environment annotation only (Decision 3). `SPIKE-FIXTURE.md` is
  hand-maintained prose around a generated table; check it is not rewritten by
  a generator before editing (the corpus `<name>.md` files **are** generated by
  `Corpus.renderTable` — do **not** hand-edit those; if the annotation must
  live in the corpus tables, add it to `Corpus.renderTable`'s header text
  instead and regenerate, and treat that as the one code touch, flagged in the
  ledger). Prefer putting the annotation in `CORPUS.md` (hand-authored) and
  `SPIKE-FIXTURE.md`, leaving the generated per-entry tables alone.
- `docs/12_PROGRESS_LEDGER.md` — the "Shared fixture" block environment line;
  the index row + a note in "Pinned facts" that the suite is now CI-verified
  (the `171` value itself does not change and CI does not pin the count).
- `docs/09_TEST_STRATEGY.md` — section 6: mark the framework-neutral matrix
  items (pinned-restore, release build, headless unit/property tests,
  deterministic scenario/replay tests) realised by TASK-023, with the
  client-compile / content-validation / package-smoke rows explicitly still
  pending P4. Follow the "Realised by TASK-0NN" precedent already in sections
  2.2, 2.4, and 8.
- control-document updates: this task, `docs/11_BACKLOG.md` (TASK-023 row +
  B-048), `docs/12` index row + `docs/ledger/` detail file, `PROJECT_STATE.yaml`
  only if this becomes the active task.

## Forbidden scope

- Any Godot / Mibo / client build, content-validation, or package-smoke job.
  Deferred to P4 (Decision 1).
- `packages.lock.json`, `<RestorePackagesWithLockFile>`, or `--locked-mode`
  restore (Decision 2) — a separate implementation task if R-016 justifies it.
- Any second runner OS, or a build matrix. `ubuntu-latest` in particular is out
  (Decision 3): it would test a disclaimed cross-platform determinism claim.
- Running the benchmarks in CI, or any `dotnet run --project bench/...` step.
- Any source, `.fsproj`, or test change. In particular: no global
  `-warnaserror`, no change to the test project's warning discipline, no new
  package, no `TargetFramework` change.
- Re-pinning any fixture / corpus / golden hash; a `Canonical.FormatVersion`
  bump; any `Canonical.encode` change. A hash divergence surfaced by the new CI
  is a **stop-and-report** finding — it means a determinism defect already
  shipped, and diagnosing it is separate work.
- Hand-editing a generated `content/replays/<name>.md` table.
- Publishing status badges, deploy steps, release automation, or anything that
  writes back to the repository or an external service.
- `docs/08_ROADMAP_AND_GATES.md` — no gate-obligation change (the CI
  requirement already lives in `docs/09` section 6).

## Acceptance criteria

- [ ] `.github/workflows/ci.yml` exists, triggers on `push` and
      `pull_request`, runs on `windows-latest`, and pins `actions/checkout` and
      `actions/setup-dotnet` (SHA or released major tag) with
      `global-json-file: global.json`.
- [ ] The job runs, in order: `dotnet restore CommandoWar.slnx`;
      `dotnet build CommandoWar.slnx -c Release --no-restore`;
      `dotnet test CommandoWar.slnx -c Release --no-build`;
      `dotnet run --project src/CommandoWar.Headless -c Release --no-build --
      corpus`; `dotnet run --project src/CommandoWar.Headless -c Release
      --no-build -- fixture`; a clean-working-tree check.
- [ ] A first CI run on the branch is green: build `0` warnings / `0` errors,
      the full suite passes (state the count observed on the runner and whether
      it matches the local `171`), `-- corpus` prints `PASS` for all five
      entries and exits `0`, `-- fixture` exits `0`, the working-tree check
      passes.
- [ ] A deliberately introduced hash divergence (e.g. a throwaway commit that
      perturbs one committed `content/replays/*.md` row) makes the job fail at
      step 5 **and** step 6, with `-- corpus` naming the first bad tick and the
      expected/actual hash. Revert the throwaway commit; capture the failed-run
      log as evidence.
- [ ] `content/replays/CORPUS.md`, `content/fixtures/SPIKE-FIXTURE.md`, and the
      `docs/12` "Shared fixture" block each carry the one-line
      Windows-x64 / .NET `10.0.303` environment annotation. No generated
      `<name>.md` table is hand-edited.
- [ ] `docs/09_TEST_STRATEGY.md` section 6 marks the framework-neutral matrix
      items realised by TASK-023, with client-compile / content-validation /
      package-smoke explicitly still P4.
- [ ] No source, `.fsproj`, or test file changed (`git status --porcelain`
      shows only `.github/`, `content/replays/CORPUS.md`,
      `content/fixtures/SPIKE-FIXTURE.md`, and control docs).
- [ ] `Canonical.FormatVersion` unchanged (`2`); no committed hash moved;
      `PROJECT_STATE.yaml` updated only if this became the active task; backlog
      row, ledger index row + detail file, task status updated.

## Required verification

- Push the branch and confirm a green run in the Actions tab; link the run.
- Locally, from the repository root, run the exact command sequence the
  workflow runs and confirm each exit code:
  - `dotnet restore CommandoWar.slnx`
  - `dotnet build CommandoWar.slnx -c Release --no-restore`
  - `dotnet test CommandoWar.slnx -c Release --no-build`
  - `dotnet run --project src/CommandoWar.Headless -c Release --no-build -- corpus`
  - `dotnet run --project src/CommandoWar.Headless -c Release --no-build -- fixture`
  - `git status --porcelain` (empty after the two verb runs)
- The negative test in the acceptance criteria (introduce a divergence, watch
  CI fail, revert).
- `git status --porcelain` on the finished branch matches the "Allowed scope"
  list exactly.

## Evidence to capture

- the green CI run URL and its build/test/`corpus`/`fixture` step logs (test
  count on the runner, the five `PASS` lines);
- the red CI run URL from the deliberate-divergence negative test, showing the
  `-- corpus` divergence report;
- the local command sequence with exit codes;
- the diff of the three environment annotations and the `docs/09` section 6
  update.

## Rollback or removal

Delete `.github/workflows/ci.yml` and revert the three one-line annotations and
the `docs/09` section 6 note. No source, no data, no hash is touched, so there
is nothing to re-author on apply or revert. Removing the workflow leaves the
project exactly where it is today: build and test reproducible only by hand.

## Documentation updates

- this task status and evidence;
- `docs/11_BACKLOG.md` (new TASK-023 row in section 2; new B-048 row in
  section 3);
- `docs/12_PROGRESS_LEDGER.md` index row + `docs/ledger/` detail file; the
  "Shared fixture" environment line; a "Pinned facts" note that the suite is
  CI-verified on `windows-latest` (the `171` value is unchanged);
- `docs/09_TEST_STRATEGY.md` section 6 (realised-by note);
- `PROJECT_STATE.yaml` only if this becomes the active task;
- no ADR (this realises an existing `docs/09` section 6 requirement and an
  R-022 mitigation; it decides no new architecture, framework, determinism
  contract, or gate).

## Completion report

Use the reporting structure in `AGENTS.md`. Do not start or offer the next
task.
