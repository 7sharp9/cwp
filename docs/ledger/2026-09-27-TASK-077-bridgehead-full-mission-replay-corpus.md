## 2026-09-27 - TASK-077 - Bridgehead full-mission replay corpus entries (`Succeeded` and `Failed`)

**Owner:** Dave with coding-agent assistance
**Source revision:** `b45f002` (base); implementation commit `4aabb0b` on branch
`claude/cool-hypatia-j4rswr`
**Environment:** Linux cloud container (Ubuntu 24.04), .NET SDK `10.0.112`
from the Ubuntu archive (bundled FSharp.Core `10.0.112`), not the pinned
`10.0.303`; CI: windows-latest, .NET SDK `10.0.303` (`.github/workflows/ci.yml`)
**Status change:** `B-077 (new) -> review`; `active_work.selected_task: none -> TASK-077`

### Why

With every `docs/07` section 9 criterion recorded as met, `docs/08` section
7's G4 evidence list is what stands between P4 and a G4 decision. One bullet
had no committed proof: "replay of a completed mission reproduces its
authoritative result". Every Bridgehead demonstration so far (TASK-064's
follow-up `Succeeded` run, TASK-073's ford refusal, TASK-075's `Failed` run)
was a temporary `dotnet fsi` probe, removed after use.

### Changes

- `src/CommandoWar.Headless/Corpus.fs`: two new entries,
  `bridgehead-succeeded` (17 `MoveTo` commands, 217 ticks) and
  `bridgehead-failed` (6 `MoveTo` commands, 86 ticks), both built from the
  real `bridgehead.cwscenario` with seed `20260920`
  (`CommandDemoScene.bridgeheadSeed`). `replayFileOf` now writes each entry's
  own seed into the `.cwreplay` header (from its initial `RandomState.Word`)
  instead of the shared `Seed` literal; without this the new files' header
  (`seed 20260904`) would fail `Replay.validate`'s
  `SeedInconsistentWithInitialState` check in `cwheadless replay-file` and
  the Godot replay scene. Every existing `.cwreplay` is byte-identical.
- `src/CommandoWar.Headless/CommandoWar.Headless.fsproj`:
  `bridgehead.cwscenario` as an `EmbeddedResource`, so `cwheadless`, the
  test assembly, and the Godot replay scene read the same bytes regardless
  of working directory. A Bridgehead content edit now re-runs these entries.
- `content/replays/bridgehead-{succeeded,failed}.{md,cwreplay}` (generated).
- `content/diagnostics/bridgehead-succeeded-tick-006.*`,
  `bridgehead-succeeded-tick-215.*`, `bridgehead-failed-tick-084.*` (goldens)
  and three `DiagnosticsTests` facts pinning them plus the refusal,
  reappraisal, and outcome overlays.
- `docs/evidence/task-077-*.png`: the three golden SVGs rendered to PNG.

### Schedules

See `tasks/TASK-077-*.md` Evidence for the tick-by-tick table. In short:
`Succeeded` reuses TASK-064's bridge push, sends agent 5 to the ford
stand-off cell and then on to `(12,9)` (refused tick 6, zero shots;
reappraised `Accepted` tick 83 once rifleman 102 is down), engages riflemen
101/102 from B-071's cells (agents 0 and 4 lost), plants on the charge
(complete tick 160), and extracts four survivors through both cells
(`MissionSucceeded` tick 215, hash `0x62AF99729ED8A568`). `Failed` is one
six-agent frontal charge (`MissionFailed` tick 84, hash
`0x1EFC5642D6532E76`); a search of all 720 agent-to-target mappings of
TASK-075's target set found 46 that fail and none reproducing TASK-075's
recorded hash (its mapping and command ids were not recorded).

### Verification

- Toolchain check, before any change: `dotnet run --project
  src/CommandoWar.Headless -c Release -- corpus` on SDK 10.0.112 (scratch copy
  without `global.json`, `-p:NuGetAudit=false`)
  - Result: `OK - all 20 entries match their committed tables` -- the
    Windows/10.0.303 pins reproduce exactly on this toolchain.
- `dotnet build src/CommandoWar.Headless -c Release -p:NuGetAudit=false`
  - Result: `0 Warning(s)`, `0 Error(s)` (`TreatWarningsAsErrors` on).
- `-- corpus --regenerate`, twice
  - Result: byte-identical between runs; all 20 existing `.md`/`.cwreplay`
    files unchanged except `chokepoint-detour.md` (pre-existing drift, see
    below, not copied back).
- `-- corpus`
  - Result: `OK - all 22 entries match their committed tables`.
- `-- replay-file content/replays/bridgehead-{succeeded,failed}.cwreplay`
  - Result: both load and replay (seed header now consistent).
- CRLF check (a Windows checkout of the `text=auto` scenario file): parsing
  the file with `\r\n` line endings builds a world with the identical tick-0
  hash, `0x02EA1348C7764846`.
- New `DiagnosticsTests` facts: the test project cannot restore here
  (`api.nuget.org` refused by the session's egress policy), so the exact new
  test block was compiled and run in a scratch project against a minimal
  stand-in `Xunit` module with the same `Assert` signatures.
  - Result: all three pass; a negative control (wrong threat id, wrong
    completed-objective list) fails both mutated facts as expected.
- CI on `4aabb0b` (windows-latest, SDK 10.0.303, cold restore), run
  `36339461637`, <https://github.com/7sharp9/cwp/actions/runs/36339461637>:
  - `dotnet build CommandoWar.slnx -c Release`: `0 Warning(s)`, `0 Error(s)`.
  - `dotnet test CommandoWar.slnx -c Release`: `Passed! - Failed: 0, Passed:
    427, Skipped: 0, Total: 427` (was 422: +2 `CorpusTests` theory rows
    picked up automatically, +3 `DiagnosticsTests` facts).
  - `cwheadless corpus`: `OK - all 22 entries match their committed tables`
    -- the tables pinned on Linux/SDK 10.0.112 hold on the reference
    Windows/10.0.303 environment.
  - working tree clean after the verbs.
- Dependency boundary: `git diff --stat src/CommandoWar.Sim` empty; no
  package added; `bridgehead.cwscenario` unchanged.

### Findings (not fixed; for G4 defect triage)

1. **Perception ignores the observer's own vitals** (proposed medium).
   `Perception.visibleContactsFor` filters out a non-alive *target*
   (TASK-055) but never a non-alive *observer*, so a dead or incapacitated
   agent keeps emitting `ContactObserved` and, on the friendly side, keeps
   feeding `WorldState.TacticalKnowledge` as a permanent sensor. Seen in both
   runs (dead machine gunner 100 "observing" from tick 77).
2. **`ExtractAgents` completes vacuously when every friendly is down**
   (proposed low). `Simulation.mission` counts a non-alive agent as
   satisfied, so `ObjectiveCompleted 3` fires in the same tick as
   `MissionFailed`; the outcome is still `Failed`, but `CompletedObjectives`
   lists an extraction that never happened.
3. **`chokepoint-detour` description drift** (pre-existing, cosmetic).
   `Corpus.fs` says "reaches (4,0) for real by tick 7" while the committed
   `content/replays/chokepoint-detour.md` says "by tick 6", so `corpus
   --regenerate` rewrites that file's prose. Hashes unaffected. Not touched
   here (Forbidden scope: no existing table change).

Both 1 and 2 are pinned as current behaviour in the new entries; a fix will
re-pin them.

### Deviations and unresolved issues

- The pinned SDK could not be installed; the Ubuntu 10.0.112 SDK was used.
  The 20 existing tables reproducing exactly is the evidence that it computes
  the same hashes; CI on the pinned SDK is the authoritative check.
- The full xunit suite was not run locally (NuGet blocked); CI ran it
  (427/427).
- `PROJECT_STATE.yaml` does not parse as YAML, and did not before this task:
  line 8 (`project.updated`, a plain scalar) contains `: ` sequences
  (`git show b45f002:PROJECT_STATE.yaml` fails at column 2430). This task's
  edit keeps it no worse (a `Prior: ` it first introduced was removed) but
  does not fix it.
- No G4 gate decision is made or proposed as made; the remaining G4 bullet
  "known defects are triaged by severity" is Dave's call, with the findings
  above as input.

### Documents updated

- `tasks/TASK-077-BRIDGEHEAD-FULL-MISSION-REPLAY-CORPUS.md` (new)
- `docs/11_BACKLOG.md` (new B-077 row)
- `docs/07_VERTICAL_SLICE.md` section 9 (committed replay evidence note)
- `content/replays/CORPUS.md`, `content/diagnostics/README.md`
- `PROJECT_STATE.yaml` (`active_work`)
- `docs/12_PROGRESS_LEDGER.md` (index row) and this file

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-27, "accept it"), on the self-verification plus
  CI evidence above. Dave's next instruction was to fix finding 1 (the
  perception defect), realised as TASK-078.
