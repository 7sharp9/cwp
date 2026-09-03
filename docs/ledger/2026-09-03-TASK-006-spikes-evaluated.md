## 2026-09-03 - TASK-006 - Framework spikes evaluated; ADR-0001 decision matrix completed and put to Dave

**Owner:** Dave with coding-agent assistance
**Source revision:** `aa48abc` (Add Mibo framework spike and evidence)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303 (`dotnet --version` =
10.0.303); Godot `4.7.2.stable.mono.official.ed1daf0bf`; raylib/OpenGL windowed
run on the local display
**Status change:** TASK-005 `review -> done`; TASK-006 `proposed -> active`;
ADR-0001 unchanged (`proposed`, `Selected candidate: TBD`)

### What was done

- **Precondition:** accepted TASK-005 as evidence (backlog `review -> done`,
  task file `done`, ledger review `pending -> yes (2026-09-03)`); activated
  TASK-006 (backlog `-> active`, task file `proposed -> active`,
  `PROJECT_STATE.yaml active_work` -> TASK-006). Gates, `current_gate`,
  `framework_decision`, and ADR-0001 status left unchanged - those move only on
  Dave's acceptance of the decision evidence in this session.
- Confirmed both spikes met equivalent minimum behaviour and recorded every
  non-equivalent dimension (see below).
- Completed the ADR-0001 scored decision-driver table using the predeclared
  weights (unchanged). Weighted result: **Godot 4.13, Mibo 3.62.**
- Filled the ADR-0001 "Qualitative decision" block as a **recommendation pending
  acceptance** (Godot), with decisive evidence, the honest case for Mibo and the
  condition under which it would win, accepted weaknesses, the Mibo removal
  plan, review triggers, and pinned versions.
- Re-verified both hosts in this session (commands and results below).

### Equivalence check

Both spikes proved, against the byte-identical `CommandoWar.Sim.dll`: six agents
from snapshots on a 32x32 isometric greybox; one typed move command
(tick 1, agent 3 -> (20,14), `CommandId 1`) producing a deterministic state
change; fixed authoritative stepping visibly independent of render rate;
on-screen tick / state-hash overlay; one actionable invalid-content failure that
does not start the sim; and the exact 41-hash fixture sequence (initial
`0xF2F3DF0D820AD9AC`, final `0x838D3AE7DBFB735D`, 33 events, 0 random draws).

Not equivalent, recorded in the ADR:

- **Authoring tool:** Godot has a native visual scene/TileMap/inspector editor
  (confirmed to open the project headless; GUI iteration still not driven in any
  session). Mibo has none; its map is a hand-edited `.cwmap` text file and Tiled
  was removed from scope. The authoring-tool comparison is text-edit vs
  text-edit on both sides.
- **Dependency baseline:** the Mibo host is pinned to `Mibo.Core` /
  `Mibo.Raylib` 4.1.0 because 4.2.0+ hard-couples to the prohibited
  `Mibo.Adaptive` (ADR-0003 2026-09-02 amendment). Godot is on current stable
  4.7.2.
- **Packaging:** Mibo produced a working self-contained `dotnet publish`; Godot
  self-contained export is blocked on absent export templates.

### Verification (this session)

- Command: `dotnet test CommandoWar.slnx -c Release`
  - Result: `Passed! - Failed: 0, Passed: 54, Skipped: 0, Total: 54`.
    Framework-neutral; neither client host is in `CommandoWar.slnx` (verified:
    the `.slnx` lists only `CommandoWar.Sim`, `CommandoWar.Headless`,
    `CommandoWar.Sim.Tests`).
- Command: `dotnet run --project src/CommandoWar.Headless -c Release -- fixture`
  - Result: initial `0xF2F3DF0D820AD9AC`, final `0x838D3AE7DBFB735D` (format 1).
- Command: `dotnet build src/CommandoWar.Client.Godot/CommandoWar.Client.Godot.slnx -c Release`
  - Result: `0 Warning(s) 0 Error(s)`.
- Command: `dotnet build src/CommandoWar.Client.Mibo/CommandoWar.Client.Mibo.slnx -c Release`
  - Result: `0 Warning(s) 0 Error(s)`.
- Command: `<godot> --headless --path src/CommandoWar.Client.Godot -- --selfcheck --expect 0x838D3AE7DBFB735D`
  - Result: `tick=0 hash=0xF2F3DF0D820AD9AC` ... `tick=40 hash=0x838D3AE7DBFB735D`,
    `accepted-command-log: 1 3 move 20 14`, `MATCH`, exit 0.
- Command: `dotnet run --project src/CommandoWar.Client.Mibo -c Release -- --selfcheck --expect`
  - Result: identical 41-hash sequence, `MATCH`, exit 0.
- Command: `dotnet run --project src/CommandoWar.Client.Mibo -c Release -- --selfcheck --fixedstep --expect`
  - Result: `fixed-step: 40 authoritative ticks, final tick=40 hash=0x838D3AE7DBFB735D`,
    `MATCH`, exit 0.
- Command: `<godot> --headless --path src/CommandoWar.Client.Godot -- --selfcheck --invalid`
  - Result: `CONTENT LOAD FAILED` - 3 errors, each naming node / id / cell /
    reason; simulation not started; exit 2.
- Command: `dotnet run --project src/CommandoWar.Client.Mibo -c Release -- --selfcheck --invalid`
  - Result: `CONTENT LOAD FAILED (greybox-invalid.cwmap)` - 4 errors, each
    naming marker / kind / cell / reason; no `tick=` lines; exit 2.
- Command: `<godot> --path src/CommandoWar.Client.Godot -- --screenshot <tmp>.png`
  - Result: windowed run outside the editor, screenshot at tick 14 hash
    `0x04343D056D0BAC45` (matches fixture tick 14).
- Command: `dotnet run --project src/CommandoWar.Client.Mibo -c Release -- --screenshot <tmp>.png`
  - Result: real raylib/OpenGL window, screenshot at tick 24 hash
    `0x08879506597DB88D` (matches fixture tick 24), clean exit 0.
- Command: `dotnet list src/CommandoWar.Sim/CommandoWar.Sim.fsproj package --include-transitive`
  - Result: `FSharp.Core 10.1.303` only; no `ProjectReference`.
- Command: `dotnet list src/CommandoWar.Client.Mibo/*.fsproj package --include-transitive`
  - Result: `Mibo.Raylib 4.1.0`, `Mibo.Core 4.1.0`, `Raylib-cs 8.0.0`,
    `FSharp.UMX 1.1.0`. No `Mibo.Adaptive`.
- Command: source scan of `src/CommandoWar.Sim` + `src/CommandoWar.Headless` for
  `godot|monogame|raylib|mibo|tiled|Vector2|Node2D|System.Drawing`
  - Result: two doc-comment lines in `Fixture.fs`; no type or API.
- Command: `<godot> --editor --headless --quit --path src/CommandoWar.Client.Godot`
  - Result: exit 0, project imports and editor layout loads with no error (see
    the TASK-004 interactive addendum).
- Manual check: `git status` shows only the intended doc edits
  (`PROJECT_STATE.yaml`, `docs/11_BACKLOG.md`, `docs/12_PROGRESS_LEDGER.md`,
  `tasks/TASK-005-*.md`, `tasks/TASK-006-*.md`, `decisions/ADR-0001-*.md`); no
  source, test, fixture, or spike file changed; transient screenshots cleaned
  up.

### Recommendation put to Dave

**Godot** (candidate A), weighted 4.13 vs Mibo 3.62. The project is
content- and UX-iteration-bound before rendering-bound; Godot leads the two
highest-weighted drivers (authoring 20, presentation 15) because it ships mature
integrated tooling the Mibo route must build as code; and Mibo has a confirmed
production-eligibility problem (the `Mibo.Adaptive` coupling) that Godot does
not. Mibo's real wins - no interop tax, native headless, out-of-box packaging,
comparable host code size - are genuine but lower-leverage. The recommendation
carries two accepted weaknesses with review triggers: Godot's editor iteration
loop is still unmeasured on this project, and self-contained packaging is
blocked on absent export templates.

### Deviations and unresolved issues

- The Godot editor-GUI content-edit exercise could not be driven in this session
  (no interactive display). Accepted as a known limitation on Dave's
  instruction; recorded in the TASK-004 interactive addendum and as ADR-0001
  review trigger 1. Not sealed as a measured result.
- Godot self-contained packaging remains blocked (export templates absent).
  Recorded as accepted weakness 2 with review trigger 2 and exact unblock steps
  in the TASK-004 ledger.
- ADR-0001 status, Selected candidate, `framework_decision`, `gates.G1`, and
  `current_gate` are **unchanged**. They move only after Dave accepts this
  evidence in this session. `docs/02_TECHNOLOGY_DECISION.md` provisional
  language is likewise unchanged pending acceptance.

### Documents updated

- `docs/11_BACKLOG.md` (TASK-005 `review -> done`; TASK-006 `-> active`)
- `tasks/TASK-005-MIBO-SPIKE.md` (`review -> done`)
- `tasks/TASK-006-FRAMEWORK-DECISION.md` (`proposed -> active`)
- `PROJECT_STATE.yaml` (`active_work` -> TASK-006; `updated` -> 2026-09-03)
- `decisions/ADR-0001-FRAMEWORK-SELECTION.md` (scored decision-driver table;
  "Qualitative decision" block filled as a recommendation pending acceptance;
  status and Selected candidate unchanged)
- `docs/12_PROGRESS_LEDGER.md` (this entry + the TASK-004 interactive addendum;
  TASK-005 review finalised)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-03)
- Notes: Dave accepted the Godot recommendation in-session ("if you think godot
  is the best bet lets go with that"), with the priority that the C#/F# boundary
  be kept low-impedance. Finalisation applied in the same session (see the
  `2026-09-03-TASK-006-finalisation.md` ledger detail file). ADR-0001 is
  `accepted`; G1 passed.
