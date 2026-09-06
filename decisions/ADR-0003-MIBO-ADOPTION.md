# ADR-0003: Conditional and Limited Use of Mibo

Status: spike complete; Mibo not adopted (ADR-0001 chose Godot, 2026-09-03); Mibo 5.x recorded as a live reconsideration option (2026-09-06 amendment). Amended 2026-09-02, 2026-09-03, 2026-09-06  
Date: 2026-09-02  
Decision owner: Dave

## Context

Mibo is directly relevant to this project. It provides:

- an F# Elmish/MVU application model;
- a headless runtime with explicit stepping and virtual time;
- an ordered system pipeline;
- raylib-cs and MonoGame presentation backends;
- command-buffer rendering and input or asset support;
- an optional adaptive architecture.

This overlaps with some infrastructure the project would otherwise write. It also introduces dependency and abstraction risk because it is a smaller, actively changing framework. Its adaptive integration is explicitly described as experimental, and current issue reports show edge cases involving threading and adaptive collection correctness.

The project does not need Mibo to define the simulation. A minimal project-owned headless runner can call the simulation step directly.

## Decision

### During the framework spike

- Mibo may be used for TASK-005 as the candidate graphical host and headless application shell.
- Use **classic MVU**, not Mibo.Adaptive.
- Use the raylib backend for the first complete graphical spike.
- A MonoGame backend compile or launch smoke test is optional and must not delay comparable evidence.
- Pin the exact Mibo package version selected at task start.
- Record every workaround and any framework behaviour that affects stepping, ordering, input, or diagnostics.

### In all cases

- `CommandoWar.Sim` must not depend on Mibo.
- Mibo may schedule calls to the simulation but may not define authoritative domain state or rules.
- Mibo messages and models belong in the host project.
- The project-owned command, event, snapshot, random, and replay contracts remain authoritative.
- Do not design a general abstraction over Godot, Mibo, MonoGame, and raylib.

### Production adoption

Mibo becomes a production dependency only if ADR-0001 selects the Mibo route after equivalent spike evidence.

If Godot is selected, remove Mibo from default production build and CI. Do not retain it solely for headless execution; use the small project-owned runner instead.

## Mibo.Adaptive decision

Mibo.Adaptive is prohibited before G5.

After G5 it may be reconsidered only through a new ADR demonstrating:

- a measured problem in classic MVU or the current presentation architecture;
- why ordinary incremental caching or data-oriented stores are insufficient;
- deterministic and threading tests for the proposed usage;
- an explicit ownership model for foreign-thread reads and updates;
- a migration and removal path;
- version-pinned evidence against current open correctness concerns.

Framework novelty is not sufficient justification.

## Why Mibo can help

It can reduce work in a code-first F# route by supplying:

- host loop and lifecycle;
- explicit fixed-step or headless stepping facilities;
- ordered systems;
- backend-level rendering abstraction;
- familiar Elmish update structure;
- a path from simulation tests to a small graphical host without moving to C#.

This is useful for the complete application shell. It does not materially simplify the domain simulation's hardest parts:

- perception;
- tactical knowledge;
- order appraisal;
- deterministic pathfinding;
- suppression;
- commitment and execution;
- replay and canonical hashing.

Those remain project code.

## Risks accepted during spike

- package API churn;
- sparse ecosystem compared with Godot or MonoGame;
- the need to integrate an external map editor and custom UI conventions;
- temptation to use backend portability before there is a second-backend requirement;
- confusion between Elmish host state and authoritative world state.

The spike is deliberately disposable and bounded to contain those risks.

## Review triggers

Revisit this policy if:

- Mibo introduces a breaking change before TASK-005 begins;
- the pinned version cannot build on the selected .NET SDK;
- headless stepping behaves differently from direct simulation stepping;
- host-system ordering obscures the authoritative phase order;
- the framework requires Mibo types in simulation contracts;
- a framework defect blocks comparable evidence;
- ADR-0001 chooses Godot.

The first trigger fired on 2026-09-02. See the amendment below.

## 2026-09-02 amendment: Mibo.Adaptive is no longer separable in current Mibo

### Trigger

This fires the review trigger "Mibo introduces a breaking change before TASK-005 begins".

### Finding

From Mibo 4.2.0 (2026-08-11) onward, the `AdaptiveProgram` / `AdaptiveHeadless`
integration lives inside `Mibo.Core`, and `Mibo.Core` takes a hard dependency on
the `Mibo.Adaptive` package. The classic-MVU `Program`, `HeadlessProgram`, and
`HeadlessRunner` types are also in `Mibo.Core`, so no classic-MVU Mibo host can be
built without `Mibo.Adaptive` being restored, deployed, and referenced.

Evidence (2026-09-02, versions from api.nuget.org):

- `Mibo.Core` nuspec history: 4.0.0 and 4.1.0 depend on `FSharp.Core` and
  `FSharp.UMX` only; 4.2.0 through 4.5.3 add `Mibo.Adaptive` (1.0.0, then 1.0.1)
  to both the net8.0 and net10.0 dependency groups.
- A `net10.0` project referencing `Mibo.Raylib` 4.5.3 restores `Mibo.Adaptive`
  1.0.1, ships `Mibo.Adaptive.dll` in its output, and lists `"Mibo.Adaptive/1.0.1"`
  in `*.deps.json`. `dotnet list package --include-transitive` shows it.
- `Mibo.Core.dll` 4.5.3 carries a direct assembly reference to `Mibo.Adaptive`
  and contains `Mibo.Adaptive`-namespaced startup code.
- `Mibo.Adaptive` 1.0.1 is itself a correctness fix for its derived-collection
  engine ("chained derived maps no longer freeze after certain read and write
  orders").

This defeats the ADR's assumption that classic MVU and Mibo.Adaptive are
independent packages that can be adopted separately.

### Decision

The production prohibition on Mibo.Adaptive is unchanged and is now binding on
Mibo itself. Because current Mibo (>= 4.2.0) cannot be adopted without the
`Mibo.Adaptive` package, the current Mibo release line is not production-eligible
before G5. Selecting Mibo for production would require either an upstream change
that re-separates the packages, or a new ADR that explicitly accepts the coupled
`Mibo.Adaptive` package (not its API) with version-pinned evidence against its
open correctness concerns.

For TASK-005 only, which is a disposable spike:

- Pin `Mibo.Core` and `Mibo.Raylib` to 4.1.0, the last release with no
  `Mibo.Adaptive` dependency. It restores and builds on .NET SDK 10.0.303 /
  `net10.0` with no package downgrade, and retains the classic
  `Mibo.Elmish.HeadlessProgram` / `HeadlessRunner` surface. The "no Mibo.Adaptive
  package or API" rule is preserved by version pinning rather than by package
  exclusion. Exclusion workarounds are not permitted.
- If 4.1.0 cannot produce the required spike evidence, that is itself a recorded
  spike result and TASK-005 stops.
- TASK-005 is narrowed to the F#-ergonomics and application-shell-cost question.
  Tiled authoring, self-contained packaging, and the MonoGame backend smoke test
  are removed from its scope. See the task file.

### Independent finding for ADR-0001

The coupling change and Mibo's release cadence (1.0 to 4.5.3 in about three
months, an experimental incremental-computation engine folded into the core
package, that engine still shipping correctness fixes) are concrete evidence for
the ADR-0001 "dependency and maintenance risk" driver. This stands regardless of
the spike's ergonomic results and belongs in the ADR-0001 Mibo column.

## 2026-09-03 note: Mibo not adopted

ADR-0001 was accepted on 2026-09-03 and selected Godot .NET. This fires the
ADR-0003 review trigger "ADR-0001 chooses Godot".

Consequences:

- Mibo is removed from the default production build and CI. It was never in
  `CommandoWar.slnx`; `src/CommandoWar.Client.Mibo/` is retained as TASK-005
  spike evidence only and is not a build, test, CI, or task target.
- Headless execution stays on the project-owned `cwheadless` runner
  (`src/CommandoWar.Headless/`), as this ADR's "Production adoption" section
  already required for the Godot outcome. Mibo's `HeadlessRunner` is not used.
- `CommandoWar.Sim` remains framework-neutral (ADR-0002). No Godot/Mibo/
  MonoGame/raylib abstraction is built.
- The Mibo.Adaptive prohibition (this ADR, `PROJECT_STATE.yaml`
  `prohibited_before_G5`) is unaffected and stays in force.
- The TASK-005 spike evidence, the shared fixture, and `cwheadless` are
  preserved for audit; nothing is deleted.

Reopening Mibo as a production option would require a new ADR with measured
failure evidence against the Godot route, per this ADR's rollback rule and
ADR-0001.

## 2026-09-06 amendment: Mibo 5.0.0 re-separates the MVU and adaptive packages

### Trigger

Dave flagged the Mibo 5.0.0 release (2026-09-04) and asked that it be recorded as
a live option now, noting that the Godot client work has not materially
progressed since ADR-0001: the editor edit-to-visible-result measurement
(ADR-0001 review trigger 1) and the blocked self-contained export (review
trigger 2) are both still open.

This is the condition the 2026-09-02 amendment and ADR-0001 both named: "an
upstream change that re-separates the packages".

### Finding

From the Mibo changelog (`https://github.com/AngelMunoz/Mibo/blob/main/CHANGELOG.md`,
entry `[5.0.0] - 2026-09-04`):

- The framework is split into two independent runtime lanes over a shared
  kernel. Classic Elmish/MVU (`Cmd`, `Sub`, `Program`, loops, headless support)
  moves to a new `Mibo.Mvu` package; the adaptive runtime moves to
  `Mibo.Adaptive.Mibo`.
- `Mibo.Core` no longer depends on `Mibo.Adaptive`.
- MVU host packages are `Mibo.Raylib.Mvu` and `Mibo.MonoGame.Mvu`; adaptive host
  packages are `Mibo.Raylib.Adaptive` and `Mibo.MonoGame.Adaptive`. An MVU
  install pulls no adaptive code.
- Every namespace, type, and member keeps its name and home; the migration is a
  recompile against the correct lane's packages, not a source edit.
- Templates now pin `5.*`.

Not verified (no spike has been run against 5.x, and outbound package restore is
unavailable in the review environment): the 5.x packages' target frameworks (the
4.x line published `net8.0` and `net10.0` dependency groups; this project is
`net10.0`), the actual restore graph and `deps.json` contents, and
headless-runner parity with the 4.1.0 surface exercised in TASK-005.

### What this changes

The 2026-09-02 amendment's blocker is lifted on the 5.x line. "Current Mibo
cannot be adopted without the prohibited `Mibo.Adaptive` package" was true for
4.2.0 through 4.5.3; it is not true for 5.0.0, where classic MVU is the
standalone `Mibo.Mvu` package. The `Mibo.Adaptive` production prohibition
(`PROJECT_STATE.yaml` `prohibited_before_G5`, unchanged) is now satisfied simply
by not referencing `Mibo.Adaptive.Mibo` or the `*.Adaptive` host packages,
rather than by a version pin to a stale release.

On packaging grounds, Mibo classic MVU (5.x) is production-eligible again
before G5.

### What this does not change

- **Mibo is still not adopted.** ADR-0001 selected Godot on a weighted score of
  4.13 vs 3.62. The dependency-and-maintenance-risk driver (weight 5; Godot 4.5,
  Mibo 1.5) accounted for 0.15 of the 0.51 weighted gap (Godot 0.225, Mibo 0.075
  on that driver). Re-scoring Mibo's dependency risk from 1.5 toward ~3.0 adds
  about 0.075 to its total (~3.70) and does not close the gap; the decision was
  carried by the higher-weighted content-authoring, UI/presentation, and
  glue-code drivers, which this release does not touch.
- ADR-0001's rollback rule still governs: reopening Mibo as a production option
  requires a new ADR with measured failure evidence against the Godot route.
- The `Mibo.Adaptive` prohibition before G5 is unchanged.
- Mibo's release cadence remains a live concern (1.0 to 5.0 in about four
  months, a major restructure days before this amendment); the churn
  sub-argument in the ADR-0001 Mibo column stands.

### Recorded status

Mibo 5.x classic MVU is a **live reconsideration option** for the production
client, no longer blocked on packaging. Acting on it is an ADR-0001
review-trigger decision for Dave, coupled to ADR-0001 review trigger 1: the
Godot editor edit-to-visible-result loop is still unmeasured on this project,
which is the other half of the "honest case for Mibo" condition in ADR-0001. If
that measurement lands and shows no material Godot authoring advantage, the Mibo
route becomes defensible and the reconsideration spike (backlog B-043) should
run before framework-specific client work (B-024 onward) begins.
