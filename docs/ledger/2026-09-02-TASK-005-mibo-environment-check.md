## 2026-09-02 - TASK-005 - Environment check: current Mibo cannot satisfy the Mibo.Adaptive prohibition; task rescoped

**Owner:** Dave with coding-agent assistance
**Source revision:** `f363174` (Add deterministic random/hash/replay harness); working tree with TASK-004 spike present
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303; NuGet queried at api.nuget.org on 2026-09-02
**Status change:** none (TASK-005 stays `ready after dependency`; not started)

### What was done

Pre-implementation environment check only. No Mibo host was created. No
simulation, test, or shared-fixture file was changed.

### Finding

Current stable Mibo cannot be used within TASK-005's constraints. `Mibo.Core`
takes a hard package and assembly dependency on the prohibited `Mibo.Adaptive`
from version 4.2.0 (2026-08-11) onward, and the classic-MVU `Program` /
`HeadlessProgram` / `HeadlessRunner` types live in `Mibo.Core`, so no
classic-MVU Mibo host can be built without `Mibo.Adaptive` being restored,
shipped, and referenced. This violates the task's forbidden scope
("Mibo.Adaptive (any package or API)"), the acceptance criterion ("no
Mibo.Adaptive package or API appears"), and ADR-0003. It fires the ADR-0003
review trigger "Mibo introduces a breaking change before TASK-005 begins".

### Verification

- Command: `curl api.nuget.org/v3-flatcontainer/mibo.core/{version}/mibo.core.nuspec`
  - Result: 4.0.0 and 4.1.0 depend on `FSharp.Core` + `FSharp.UMX` only; 4.2.0
    through 4.5.3 add `Mibo.Adaptive` (1.0.0, then 1.0.1 from 4.5.1) to the
    net8.0 and net10.0 dependency groups. Latest `Mibo.Core` / `Mibo.Raylib` /
    `Mibo.MonoGame` = 4.5.3; `Mibo.Adaptive` = 1.0.1.
- Command: throwaway `net10.0` F# exe referencing `Mibo.Raylib` 4.5.3, `dotnet
  restore` + `dotnet build -c Release`
  - Result: `Build succeeded, 0 Error(s)`. Warning NU1605 (FSharp.Core
    10.1.400 -> 10.1.303 downgrade). Output contains `Mibo.Adaptive.dll`;
    `*.deps.json` lists `"Mibo.Adaptive/1.0.1"`; `dotnet list package
    --include-transitive` lists `Mibo.Adaptive 1.0.1`.
- Command: reflection over the restored `Mibo.Core.dll` 4.5.3
  - Result: referenced assemblies include `Mibo.Adaptive`; namespaces include
    `Mibo.Adaptive` and `<StartupCode$Mibo-Core>.$Adaptive`.
- Command: throwaway `net10.0` F# exe referencing `Mibo.Raylib` 4.1.0
  - Result: `Build succeeded, 0 Error(s)`, no NU1605, no `Mibo.Adaptive.dll` in
    output. Reflection confirms `Mibo.Elmish.HeadlessProgram` /
    `Mibo.Elmish.HeadlessRunner` present. `Mibo.Raylib` 4.1.0 -> `Mibo.Core`
    4.1.0, `FSharp.Core >= 10.1.302`, `FSharp.UMX` 1.1.0, `Raylib-cs` 8.0.0.
- Mibo CHANGELOG 4.2.0 entry: "the Mibo integration
  (`AdaptiveProgram`/`AdaptiveHeadless`) lives in Mibo.Core". `Mibo.Adaptive`
  1.0.1 release note: "chained derived maps no longer freeze after certain read
  and write orders".

### Decision

- `decisions/ADR-0003-MIBO-ADOPTION.md`: added the "2026-09-02 amendment". The
  production prohibition on Mibo.Adaptive is unchanged and now makes the current
  Mibo release line (>= 4.2.0) not production-eligible before G5. For the
  disposable TASK-005 spike only, Mibo is pinned to 4.1.0 (last release with no
  `Mibo.Adaptive` dependency); the prohibition is preserved by version pinning,
  not package exclusion.
- `tasks/TASK-005-MIBO-SPIKE.md`: rewritten and trimmed (M -> S). Removed Tiled
  authoring + importer, self-contained packaging, and the MonoGame backend
  smoke test. Kept the decision-relevant core: a Mibo classic-MVU headless
  self-check reproducing the shared fixture's 41-hash sequence, a windowed
  raylib host rendering six agents with a tick/hash overlay and the move
  command, fixed-step vs render-rate separation, one invalid-content failure, a
  deliberate-mismatch non-zero exit, and an honest host line count next to the
  Godot spike's ~410.
- `docs/11_BACKLOG.md`: TASK-005 row size `M -> S`, dependencies now
  `TASK-004, ADR-0003 amendment`.
- Not raw MonoGame: `Mibo.MonoGame` also depends on `Mibo.Core` and hits the
  same coupling; raw MonoGame means owning the application-shell gap that
  `docs/02` already reasoned against. If the Mibo spike does not show a clear
  F#-ergonomics win, the route is Godot.

### Evidence

- Probe projects under the session scratchpad (not committed).
- ADR-0003 amendment section; rewritten `tasks/TASK-005-MIBO-SPIKE.md`.

### Deviations and unresolved issues

- The session precondition (mark TASK-004 accepted/done, flip TASK-005 to
  active, update `PROJECT_STATE.yaml`) was not performed: TASK-004 acceptance is
  Dave's review call and TASK-005 hit this blocker before host work.
  `PROJECT_STATE.yaml` is unchanged; `active_work.selected_task` is still
  TASK-004.
- The 4.1.0 spike measures Mibo ergonomics and application-shell cost only. The
  dependency-and-maintenance-risk question is already answered against Mibo by
  this finding, independent of the spike result, and belongs in the ADR-0001
  Mibo column.
- ADR-0001 remains `proposed`, `Selected candidate: TBD`. No evidence column was
  filled by this entry.

### Documents updated

- `decisions/ADR-0003-MIBO-ADOPTION.md` (2026-09-02 amendment)
- `tasks/TASK-005-MIBO-SPIKE.md` (rewritten, trimmed)
- `docs/11_BACKLOG.md` (TASK-005 row)
- `docs/12_PROGRESS_LEDGER.md` (this entry)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-02)
- Notes: ADR-0003 amendment and trimmed TASK-005 scope signed off. TASK-004
  accepted in the same review. TASK-005 activated in `PROJECT_STATE.yaml`;
  implementation follows in the next ledger detail file
  (`2026-09-02-TASK-005-mibo-spike.md`). ADR-0001 is not decided.
