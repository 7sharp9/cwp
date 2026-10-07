## 2026-10-07 - TASK-082 - Group MoveTo completes at the agent's formation slot

**Owner:** Dave with coding-agent assistance
**Source revision:** `20b4163`, branch `claude/cool-hypatia-j4rswr`
**Environment:** Linux cloud container, .NET SDK `10.0.112` only (pinned
`10.0.303` not installed). Built in a scratch copy of `git archive HEAD` with
`global.json` relaxed to `10.0.112` and `-p:NuGetAudit=false`; the repository's
`global.json` was not touched. No xunit or FsCheck packages in the offline cache,
so `dotnet test` could not run. No Godot.
**Status change:** `none -> review -> done` (selected by Dave: "fix all issues",
2026-10-07)

### Scope decision

"Fix all issues" was read as the open defects in `docs/15`. Only D-12 and D-13
were fixed. The rest were not, for these reasons:

- D-01 (major): threat-aware pathing is a design change, and `docs/15` proposes
  the playtest observe it against R-001.
- D-05: needs a neutral working title; inventing one is branding, out of scope.
- D-02, D-03: change gameplay semantics (extraction-cell occupancy, reappraisal
  triggers); each needs a decision and moves goldens.
- D-04, D-06, D-07, D-09, D-10: Godot, C# host or F# client code that compiles
  only under the Godot SDK. A change could not be built or run here.
- D-08: a client feature, not a defect repair.

### Changes

- `Simulation.fs`, `commitmentAndLocalAction`, `MoveTo` arm: when
  `o.AsGroup`, the completion test compares `Position` with
  `Appraisal.resolveFormationTarget terrain occupied a.FormationOffset target`
  (`occupied` = every other agent's `Position`) instead of the literal target.
  This is the same expression the Appraisal `fulfilled` fast path uses, and
  both phases run before Navigation, so they agree within a tick. Solo orders
  are unchanged. The `CommitmentEstablished` payload is unchanged.
- `SimulationTests.fs`: the existing "two jointly-ordered formationed agents
  through a shared chokepoint" fact also asserts both agents' `Order` is `None`
  at the end.
- `content/replays/formation-slots.md` re-pinned (ticks 9-12, final hash, 25 ->
  27 events).
- `content/replays/chokepoint-detour.md` line 3: "by tick 6" -> "by tick 7"
  (D-12).
- `docs/04` section 12.6: the known-limitation sentence removed.
- `docs/15`: D-12 and D-13 moved to section 5; counts and decision 4 updated.

### Commands and results

| Command | Result |
|---|---|
| `dotnet build src/CommandoWar.Headless -c Release -p:NuGetAudit=false` (baseline copy) | succeeded, 0 warnings, 0 errors |
| `... -- corpus` on baseline | `OK - all 22 entries match` |
| same two commands with the fix | build succeeded, 0 warnings; corpus: one `DIVERGED formation-slots`, first bad tick 9, expected `0x4BEAF2C215700B94`, actual `0x1AF0FFCDD24E52B0` |
| `dotnet fsi` probe, `formation-slots`, baseline vs fix | baseline: 25 events, both agents keep `Order`. Fix: 27 events; the two extra are `CommitmentCompleted` agent 1 (6,5) tick 9 and agent 0 (4,5) tick 10; both `Order = None`. No other event differs. |
| `... -- corpus --regenerate`, then `diff -rq` against `content/` | only `formation-slots.md` differs; `chokepoint-detour.md` (the D-12 edit) is byte-identical to regeneration; `bridgehead-succeeded` and `bridgehead-failed` unchanged |
| `dotnet fsi` probe replicating the edited SimulationTests fact | `CommitmentCompleted` for agent 0 at tick 6 (5,4) and agent 1 at tick 12 (7,4); both `Order = None`, `Disposition = None` |
| `dotnet test` | **not run** (packages unavailable offline). |

D-12: `chokepoint-detour` agent 0 reaches (4,0) at tick 7 (position per tick from
a probe: (3,0) at tick 6, (4,0) at tick 7), so `Corpus.fs:1235` was right and the
markdown wrong.

### Acceptance criteria

See the task file. The `dotnet test` criterion is unticked.

### Unresolved concerns

1. The edited xunit assertions were never compiled or run; they were checked
   only by replicating the scenario in a probe. CI is the check.
2. Re-resolving the slot each tick can disagree with the cell Appraisal wrote if
   another agent's occupancy changed in between (the ideal cell was occupied
   at acceptance, so the agent took a nearby free cell, and was then vacated).
   The agent would then walk on to the ideal cell via the unchanged Appraisal
   path rather than completing early. This is the existing behaviour of the
   Appraisal fast path, now mirrored, not a new inconsistency. No test pins it.
3. The Godot self-check pins (`CommandDemoScene`, `AppraisalDemoScene`,
   `FSharpSceneHost`) were not run. Their fixtures author no formation
   (`FSharpSceneHost.cs:451`, `AppraisalDemoScene.cs:58`), and with
   `FormationOffset = None` `resolveFormationTarget` returns the target, so the
   changed arm is a no-op there by construction. Not confirmed by running them.
4. No diagnostic-frame change: no new authoritative state, only when `Order`
   clears. The `formation-slots-tick-001` goldens are tick-1 frames and are
   unaffected; no later-tick golden was added.
5. The defects listed under "Scope decision" remain open.

### Documents updated

- `tasks/TASK-082-GROUP-MOVETO-COMPLETES-AT-SLOT.md` (new, `review`)
- `docs/11_BACKLOG.md` (B-081 row); `docs/12_PROGRESS_LEDGER.md` (index row) and
  this file
- `docs/04_SIMULATION_SPEC.md`, `docs/15_G4_DEFECT_TRIAGE.md`
- `PROJECT_STATE.yaml` (`active_work`). No "Pinned facts" value changed
  (`Canonical.FormatVersion`, test count and the SDK pin are unchanged).

### Review

Accepted: yes (2026-10-07, "accept"), on the self-verification evidence. Not covered: `dotnet test` was never run locally and the edited xunit assertions were not compiled; the other open `docs/15` defects remain open; G4 stays `pending`.
