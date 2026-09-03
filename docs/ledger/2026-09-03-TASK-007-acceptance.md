## 2026-09-03 - TASK-007 acceptance - ADR-0004 accepted

**Owner:** Dave
**Source revision:** `9c73b38` (Add F# scene host follow-up proof)
**Environment:** Windows 11 Pro 26200; .NET SDK 10.0.303
**Status change:** `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md`
`proposed -> accepted` (Date accepted 2026-09-03, Dave); TASK-007
`review -> done`; `docs/11_BACKLOG.md` TASK-007 row `review -> done`. Gates,
`current_gate` (G2_deterministic_core_proven), `current_phase`
(P2_deterministic_core), and `framework_decision` unchanged.

### What was accepted

The TASK-007 design: a thin C# host over an F# client-core library, form 1
(one generic `FSharpSceneHost` for the whole client) preferred, form 2
(per-scene shim) only where typed inspector `[Export]` / `[Signal]` is needed;
F# types are not scene entry points; the interop idiom (primitives,
`System.Nullable<T>`, `[<CLIMutable>]` records, `Godot.InputEvent` into F#
methods only); the "Myriad and the shim" analysis with option 1 proven
feasible; the ADR-0002 compliance check; four review triggers.

The TASK-007 proof was not re-run. Its recorded evidence (questions 1-5, the
`--selfcheck` fixture-hash reproduction, the C#->F# stack trace) stands.

### Carried forward

- ADR-0004 review trigger 1: the first P4 client task (B-026 / B-027) must
  confirm an F# breakpoint is hit from an editor / F5-launched run and that
  hot-reloading the C# shim does not sever the F# host reference.
- The disposable proof under `src/_scratch/godot-fsharp-boundary/` is retained
  as ADR evidence and is removable wholesale.

### Documents updated

- `decisions/ADR-0004-GODOT-FSHARP-BOUNDARY.md` (status, Date accepted)
- `tasks/TASK-007-GODOT-FSHARP-BOUNDARY.md` (status `done`; acceptance note)
- `docs/11_BACKLOG.md` (TASK-007 row `review -> done`)
- `docs/12_PROGRESS_LEDGER.md` (this note; the TASK-007 entry Review block)

### Review

- Reviewer: Dave
- Accepted: yes (2026-09-03)
- Notes: TASK-008 (B-007, scenario DTO + content version) activated in
  `PROJECT_STATE.yaml` for this session.
