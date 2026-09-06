## 2026-09-06 - TASK-023 - Continuous integration for the framework-neutral solution

**Owner:** Dave with coding-agent assistance
**Source revision:** `37824e8` (Add cell-occupancy resolution) — the local tip
of `main`, one commit ahead of the `origin/main` this session can reach
(`07af43a`); work done on branch `task-023-simulation-ci`
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; .NET 10.0.11;
xUnit 2.9.3; FsCheck / FsCheck.Xunit 3.3.4
**Status change:** `tasks/TASK-023-SIMULATION-CI.md` `ready -> review` (pending
Dave's acceptance); `docs/11_BACKLOG.md` TASK-023 row + B-048 `ready -> review`;
`PROJECT_STATE.yaml` `active_work.selected_task` `TASK-022 -> TASK-023`. No
`Canonical.FormatVersion` change (stays `2`); no committed hash moved; no
source / `.fsproj` / test file touched.

### Precondition check

`PROJECT_STATE.yaml` `active_work.selected_task` was `TASK-022` (implemented,
`docs/11` row `review`, pending Dave's acceptance). TASK-021 and TASK-022
acceptance state left exactly as found (`tasks/` and `docs/11` both read
pending Dave's acceptance). No re-verification or modification of
TASK-020/021/022 work. Branch `task-023-simulation-ci` created off local `main`
(`37824e8`); not pushed.

Baseline before any edit, from the repository root:

- `dotnet restore CommandoWar.slnx` -> exit `0` ("All projects are up-to-date
  for restore").
- `dotnet build CommandoWar.slnx -c Release --no-restore` -> exit `0`,
  `Build succeeded. 0 Warning(s) 0 Error(s)` (all four projects).
- `dotnet test CommandoWar.slnx -c Release --no-build` -> exit `0`,
  `Passed! - Failed: 0, Passed: 194, Skipped: 0, Total: 194`.
- `dotnet run --project src/CommandoWar.Headless -c Release --no-build --
  corpus` -> exit `0`, `OK - all 7 entries match their committed tables`
  (`spike-fixture`, `wall-detour`, `blocked-goal`, `converging-routes`,
  `slow-terrain`, `follow-chain`, `swap-standoff` all `PASS`).
- `dotnet run --project src/CommandoWar.Headless -c Release --no-build --
  fixture` -> exit `0`, final tick 40, final hash `0xAFA35198CC6BD8D4`
  (format 2), 33 events.
- `git status --porcelain` after the two verb runs -> only `?? .github/` (the
  new workflow file); no `--regenerate` side effect on any committed file.

### Central decisions

The task file's Central decisions 1-5 were implemented as written. Notes on the
implementer's calls:

- **Runner:** `windows-latest`, one job (Decision 3). No matrix, no
  `ubuntu-latest`.
- **Actions pinning:** `actions/checkout` ->
  `3d3c42e5aac5ba805825da76410c181273ba90b1` (v7.0.1),
  `actions/setup-dotnet` -> `a98b56852c35b8e3190ac28c8c2271da59106c68`
  (v6.0.0), both the current latest release, SHA-pinned with a `# vX.Y.Z`
  comment (the `AGENTS.md` "pin framework and package versions" discipline).
  SHAs confirmed against `git ls-remote https://github.com/actions/<repo>.git
  refs/tags/<tag>` (lightweight tags — the ref points straight at the commit).
- **SDK:** `setup-dotnet` with `global-json-file: global.json` — CI honours the
  same `10.0.303` / `rollForward: latestPatch` pin a local build honours.
- **Restore:** plain `dotnet restore CommandoWar.slnx`, no NuGet
  `actions/cache`. A warm cache would weaken Decision 2's point that a
  cold-cache restore is itself the pinned-restore check, and there is no
  lockfile to key a cache on. `packages.lock.json` / `--locked-mode` not added
  (Decision 2 — a source change, a separate task if R-016 materialises).
- **Build:** `dotnet build CommandoWar.slnx -c Release --no-restore`. `0`
  warnings / `0` errors is enforced by the per-project
  `<TreatWarningsAsErrors>` on `CommandoWar.Sim`, `CommandoWar.Headless`, and
  `CommandoWar.Benchmarks`. No global `-warnaserror` (the test project
  deliberately omits it — xUnit / FsCheck analyzer noise; Decision 4 / the
  task's step-4 note). `bench/` builds with the solution; benchmarks are not
  run (no `dotnet run --project bench/...` step).
- **Determinism verbs:** `-- corpus` with no `--regenerate` (exit `3` on the
  first divergence fails the job — the check that makes the determinism claim
  independently reproducible on a clean runner, R-009 / R-022); `-- fixture` as
  a CLI smoke of the verb path (it prints, it does not self-check against
  `SPIKE-FIXTURE.md` — the fixture hashes are pinned by `FixtureTests.fs` and
  the `spike-fixture` corpus entry). Both run from the repository root because
  `Corpus.DefaultDir = "content/replays"` is resolved relative to the working
  directory.
- **Clean-tree check:** a final `pwsh` step fails if `git status --porcelain`
  is non-empty after the verb runs (no stray `--regenerate`, no working-tree
  drift). The optional `-- corpus --regenerate` + `git diff` hardening step was
  not added: step 6 plus this check already cover a stray regenerate, and
  `Corpus.renderTable` prose drift is caught by `CorpusTests` comparing the
  committed tables.
- **Environment annotation** (Decision 3): one line added to
  `content/replays/CORPUS.md` (hand-authored), `content/fixtures/SPIKE-FIXTURE.md`
  (hand-maintained prose around a generated table — confirmed not regenerated
  by a tool before editing), and the `docs/12` "Shared fixture" block. No
  generated `content/replays/<name>.md` table was hand-edited;
  `Corpus.renderTable` was not touched.

### Reconciliation of the task file's stale numbers

The task file was drafted before TASK-020/021/022 landed. Reconciled against
the current tree and the TASK-021 / TASK-022 ledger detail files:

| Task file said | Current tree | Where reconciled |
|---|---|---|
| `171` green tests | **`194`** (`178` after TASK-021, `184` after TASK-020, `194` after TASK-022) | `docs/12` "Pinned facts" note now says CI-verified; the `194` value was already current. Acceptance criterion 3 answered with `194`. |
| `5` corpus entries, all `PASS` | **`7`** (`follow-chain`, `swap-standoff` from TASK-022) | `-- corpus` step expects 7× PASS; the negative test perturbs one of the seven. |
| `Canonical.FormatVersion` `2` | `2` (unchanged) | no change. |

### Changes

- **`.github/workflows/ci.yml`** — new. One workflow, one job
  (`framework-neutral`), `runs-on: windows-latest`. `on: push` (any branch) +
  `pull_request: branches: [main]`. `concurrency: group: ci-${{ github.ref }}`,
  `cancel-in-progress: true`. `permissions: contents: read`. Steps: checkout ->
  setup-dotnet (`global-json-file`) -> `dotnet restore CommandoWar.slnx` ->
  `dotnet build CommandoWar.slnx -c Release --no-restore` -> `dotnet test
  CommandoWar.slnx -c Release --no-build` -> `dotnet run --project
  src/CommandoWar.Headless -c Release --no-build -- corpus` -> `... -- fixture`
  -> `git status --porcelain` clean-tree check. A ~40-line top comment records
  the scope boundary, the P4 deferrals, the `windows-latest` rationale, the
  no-lockfile / no-cache rationale, the `bench/` build-not-run rationale, and
  the no-global-`-warnaserror` rationale. Full text below.
- **`content/replays/CORPUS.md`** — one-line environment annotation after the
  diagnostic-frame paragraph.
- **`content/fixtures/SPIKE-FIXTURE.md`** — one-line environment annotation
  after the "regenerated by" paragraph.
- **`docs/09_TEST_STRATEGY.md`** — section 6: a "Framework-neutral items
  realised by TASK-023" paragraph (rows 1-4 of the post-selection matrix) and a
  "Still pending P4" paragraph (selected client compile, content validation,
  package smoke), following the "Realised by TASK-0NN" precedent in sections
  2.2 / 2.4 / 8.
- **`docs/12_PROGRESS_LEDGER.md`** — "Shared fixture" block gains an
  "Environment (TASK-023)" row; the "Green tests" pinned fact gains a
  CI-verified note (value `194` unchanged, count not pinned by CI); this index
  row.
- **Control.** `tasks/TASK-023-SIMULATION-CI.md` (`Status`, acceptance boxes,
  `## Outcome`); `docs/11_BACKLOG.md` (TASK-023 row + B-048 `ready -> review`);
  `PROJECT_STATE.yaml` (`active_work.selected_task`, `note`, `updated`); this
  entry. No ADR (verifies existing contracts, decides none; the CI requirement
  already lives in `docs/09` section 6, so no `docs/08` gate change).

### The workflow file

```yaml
# Continuous integration for the framework-neutral solution (TASK-023, backlog B-048).
#
# Scope: this workflow builds and tests `CommandoWar.slnx` only. That solution
# *is* the framework-neutral boundary — it contains exactly the four
# framework-neutral projects (CommandoWar.Sim, CommandoWar.Headless,
# CommandoWar.Sim.Tests, CommandoWar.Benchmarks). The Godot and Mibo client
# projects and src/_scratch/ are in the repository but not in the solution, so
# they are never restored, built, or tested here.
#
# Deliberately NOT in this workflow, deferred to P4 and the first client task
# (docs/09 section 6 post-selection matrix; docs/08 section 7):
#   - selected-client (Godot) compile — needs the Godot .NET SDK and a pinned
#     Godot version, a CI toolchain of its own; the Godot project does not
#     change during P3.
#   - content validation and package smoke — nothing to guard until client
#     code exists.
# The first P4 client task (B-024 / B-025) adds those jobs to this file when
# there is client code for them to protect.
#
# Runner: windows-latest only. The determinism contract is per-environment
# (docs/09 section 3): every committed hash (content/fixtures/SPIKE-FIXTURE.md,
# content/replays/*.md, the docs/12 "Shared fixture" block) was pinned on
# Windows x64 with .NET SDK 10.0.303. A ubuntu-latest job would be testing a
# cross-platform claim the project explicitly disclaims. Strengthening that
# contract is an ADR-gated decision with its own cross-target evidence
# requirement, not a side effect of adding a CI file.
#
# Dependency pinning: restore relies on the existing exact-version
# PackageReference pins in every .fsproj plus the global.json SDK pin
# (docs/09 section 6 accepts "locked OR pinned"). A cold-cache restore on the
# runner is itself the check that those pins resolve — so this workflow adds no
# NuGet cache: a warm cache would weaken that signal, and there is no lockfile
# to key one on. packages.lock.json / --locked-mode is a source change and a
# separate task if R-016 ever justifies transitive locking.
#
# bench/CommandoWar.Benchmarks is built by the Release solution build (it
# carries TreatWarningsAsErrors, so an API break in Benchmarks.fs fails CI at
# zero extra cost) but the benchmarks are never executed — they are timing
# measurements, not a test oracle (docs/09 sections 2.8, 5.6).
#
# CommandoWar.Sim, CommandoWar.Headless and CommandoWar.Benchmarks set
# <TreatWarningsAsErrors>true</TreatWarningsAsErrors> per project; the test
# project deliberately does not (xUnit / FsCheck analyzer noise). This workflow
# does not override that with a global -warnaserror. Widening the test
# project's warning discipline is a one-line .fsproj change and out of scope
# here.

name: CI

on:
  push:
  pull_request:
    branches: [main]

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true

permissions:
  contents: read

jobs:
  framework-neutral:
    name: Restore, build, test, verify determinism
    runs-on: windows-latest

    steps:
      - name: Check out the repository
        uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1

      - name: Set up the pinned .NET SDK
        uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          # Honour the same pin a local build honours: global.json is
          # 10.0.303 under rollForward: latestPatch.
          global-json-file: global.json

      - name: Restore (cold cache — the pinned-restore check)
        run: dotnet restore CommandoWar.slnx

      - name: Build (Release, 0 warnings / 0 errors)
        run: dotnet build CommandoWar.slnx -c Release --no-restore

      - name: Test (full headless suite)
        # FixtureTests (shared-fixture per-tick hashes), the CorpusTests
        # [<Theory>] (every content/replays/ entry vs its committed table),
        # DeterminismPropertyTests (the FsCheck properties at 200 cases each),
        # and the rest.
        run: dotnet test CommandoWar.slnx -c Release --no-build

      - name: Verify the replay corpus (CLI form)
        # No --regenerate: Corpus.checkEntry replays every entry, verifies it
        # reproduces its own hashes on a second run, then compares per-tick
        # hashes, tick count, and event count to the committed
        # content/replays/<name>.md. Exit 3 (Exit.diverged) on the first
        # mismatch. Belt-and-braces over the in-suite theory; this is the check
        # that makes the determinism claim independently reproducible on a
        # clean runner. The verb resolves content/replays relative to the
        # working directory, so it runs from the repository root.
        run: dotnet run --project src/CommandoWar.Headless -c Release --no-build -- corpus

      - name: Smoke-check the fixture verb
        # cmdFixture prints the fixture and its per-tick hash table and returns
        # Exit.ok unless Fixture.run () itself errors — it does NOT compare
        # against content/fixtures/SPIKE-FIXTURE.md. Those hashes are pinned by
        # FixtureTests.fs (test step) and the spike-fixture corpus entry
        # (corpus step). This step only proves the verb runs.
        run: dotnet run --project src/CommandoWar.Headless -c Release --no-build -- fixture

      - name: Working tree unchanged by the verbs
        # Proves no committed file was rewritten (no stray --regenerate, no
        # working-tree drift from the two verb runs).
        shell: pwsh
        run: |
          $dirty = git status --porcelain
          if ($dirty) {
            Write-Host "working tree is dirty after the verb runs:"
            Write-Host $dirty
            exit 1
          }
          Write-Host "working tree clean"
```

### Verification

All from the repository root, on branch `task-023-simulation-ci`, running the
exact sequence `ci.yml` runs.

| # | Command | Exit | Result |
|---|---|---|---|
| 1 | `dotnet restore CommandoWar.slnx` | `0` | "All projects are up-to-date for restore." |
| 2 | `dotnet build CommandoWar.slnx -c Release --no-restore` | `0` | `Build succeeded. 0 Warning(s) 0 Error(s)` (all four projects). |
| 3 | `dotnet test CommandoWar.slnx -c Release --no-build` | `0` | `Passed! - Failed: 0, Passed: 194, Skipped: 0, Total: 194, Duration: 2 s`. |
| 4 | `dotnet run --project src/CommandoWar.Headless -c Release --no-build -- corpus` | `0` | `# cwheadless corpus - 7 entries in content/replays` then `PASS` for `spike-fixture` (40), `wall-detour` (24), `blocked-goal` (5), `converging-routes` (12), `slow-terrain` (8), `follow-chain` (6), `swap-standoff` (4); `OK - all 7 entries match their committed tables`. |
| 5 | `dotnet run --project src/CommandoWar.Headless -c Release --no-build -- fixture` | `0` | final tick 40, final hash `0xAFA35198CC6BD8D4` (format 2), random draws 0, 33 events, agent 3 at (20,14). |
| 6 | `git status --porcelain` (after 4 and 5) | `0` | only the TASK-023 files listed under "Allowed scope"; no committed `content/` file modified by the verbs. |

Negative test (deliberate divergence):

- Perturbed `content/replays/swap-standoff.md` tick-2 row
  `0x515092D658B2791E` -> `0x515092D658B2791F` (working-tree edit, not
  committed).
- `dotnet run --project src/CommandoWar.Headless -c Release --no-build --
  corpus` -> exit `3`:
  `DIVERGED swap-standoff: this build disagrees with the committed table` /
  `first bad tick : 2` /
  `expected hash  : 0x515092D658B2791F  (committed content/replays/swap-standoff.md)` /
  `actual hash    : 0x515092D658B2791E  (this build)`. (CI step 6.)
- `dotnet test CommandoWar.slnx -c Release` -> exit `1`:
  `Failed! - Failed: 1, Passed: 193, Skipped: 0, Total: 194`; the failing test
  is `CorpusTests.corpus entry replays deterministically and matches its
  committed table(name: "swap-standoff")` with
  `Mismatch { FirstBadTick = 2L }`. (CI step 5.)
- `git checkout -- content/replays/swap-standoff.md`; rebuilt; `-- corpus`
  back to `OK - all 7 entries match their committed tables`, exit `0`.

`git status --porcelain` on the finished branch (before the local commit):

```
 M PROJECT_STATE.yaml
 M content/fixtures/SPIKE-FIXTURE.md
 M content/replays/CORPUS.md
 M docs/09_TEST_STRATEGY.md
 M docs/11_BACKLOG.md
 M docs/12_PROGRESS_LEDGER.md
 M tasks/TASK-023-SIMULATION-CI.md
?? .github/
?? docs/ledger/2026-09-06-TASK-023-simulation-ci.md
```

Matches "Allowed scope" exactly: `.github/`, `content/replays/CORPUS.md`,
`content/fixtures/SPIKE-FIXTURE.md`, and control docs (`tasks/`, `docs/09`,
`docs/11`, `docs/12`, `PROJECT_STATE.yaml`, the new `docs/ledger/` file). No
source, `.fsproj`, test, or generated `content/replays/<name>.md` table.

### Evidence

- **Local command sequence with exit codes:** table above — restore/build/
  test/`corpus`/`fixture`/clean-tree all `0`; `Passed: 194`; 7× `PASS`.
- **`Canonical.FormatVersion` unmoved:** `-- fixture` prints `format 2`, final
  hash `0xAFA35198CC6BD8D4` (the `docs/12` "Shared fixture" pinned value);
  `-- corpus` `OK` with no `--regenerate`, so every committed table byte-
  matches this build.
- **Negative test:** `dotnet test` `Failed: 1` and `-- corpus` exit `3`, both
  naming `FirstBadTick 2` and the expected/actual hash — CI steps 5 and 6 both
  fail on a divergence, as acceptance criterion 4 requires.
- **Action SHAs:** `actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1`
  == `refs/tags/v7.0.1`;
  `actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68` ==
  `refs/tags/v6.0.0` (both via `git ls-remote`).

### Deviations and unresolved issues

- **Green-run and red-run on a GitHub Actions runner were not executed.** This
  session cannot push (`git ls-remote` reaches `origin`, but the task forbids
  pushing and leaves the branch for Dave). Acceptance criteria 3 and 4 are
  marked `[~]` with the local-equivalent evidence above; Dave confirms them by
  pushing `task-023-simulation-ci` and reading the Actions tab. The `docs/09`
  section 3 per-environment contract means a green run on `windows-latest` is
  the meaningful one; a local Windows run is the same environment class but not
  the same machine.
- **Task file drafted against `171` / five corpus entries.** Reconciled to
  `194` / seven (table above). No behaviour question — the extra tests and
  entries are TASK-020/021/022's, already accepted or pending.
- **`actions/checkout` v7 / `actions/setup-dotnet` v6** are the current latest
  and require a recent runner Node (24). `windows-latest` on GitHub-hosted
  runners has supported Node 24 actions since late 2025, so this is not a
  concern; noted only because the task drafted its examples around older
  majors. If a runner ever rejects them, pin down to
  `actions/checkout@11d5960a326750d5838078e36cf38b85af677262` (v4.4.0) /
  `actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9` (v4.3.1).
- **No NuGet cache, no `-- corpus --regenerate` step** — implementer's calls on
  the task's "Optional hardening", rationale in Central decisions above. Both
  are cheap to add later if wanted.
- **`push:` with no branch filter** triggers CI on every branch push,
  including Dave's future task branches. That is the intent (R-022: every
  change is checked). `pull_request` is filtered to `main`. A `paths-ignore`
  for docs-only changes was considered and left out — the suite runs in ~2 s
  and a docs change can still break a doc-referenced command.
- **A hash divergence surfaced by this CI on a real run is a stop-and-report
  finding** (task Forbidden scope), not something to fix under TASK-023 — it
  would mean a determinism defect already shipped.

### Documents updated

- `.github/workflows/ci.yml` (new)
- `content/replays/CORPUS.md`, `content/fixtures/SPIKE-FIXTURE.md` (environment
  annotation)
- `docs/09_TEST_STRATEGY.md` (section 6 realised-note)
- `docs/12_PROGRESS_LEDGER.md` ("Shared fixture" environment row; "Green tests"
  CI-verified note; this index row)
- `tasks/TASK-023-SIMULATION-CI.md` (status, acceptance boxes, `## Outcome`)
- `docs/11_BACKLOG.md` (TASK-023 row + B-048 `ready -> review`)
- `PROJECT_STATE.yaml` (`active_work.selected_task`, `note`, `updated`)
- this entry
- no ADR (verifies existing `docs/09` section 6 requirements and R-022 / R-009
  / R-016 mitigations; decides no new architecture, framework, determinism
  contract, or gate)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-06)
- Notes: branch `task-023-simulation-ci` was pushed and merged to `main` as
  PR #1 (commits `3ee4090` workflow, `019e4e6` merge). The first CI run on a
  real `windows-latest` runner was green, which discharges acceptance
  criterion 3; the red-run (criterion 4) was not separately captured on a
  runner but the merged configuration is byte-identical to the one exercised
  locally. Acceptance recorded 2026-09-06 alongside TASK-021 and TASK-022
  (`docs/ledger/2026-09-06-reconcile-021-022-023-acceptances.md`). A
  `revert-1-task-023-simulation-ci` branch exists on the remote but was not
  merged; the workflow stands.
