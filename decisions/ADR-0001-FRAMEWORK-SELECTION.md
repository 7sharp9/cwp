# ADR-0001: Presentation Framework Selection

Status: proposed  
Date opened: 2026-09-02  
Decision owner: Dave

## Context

The game requires:

- isometric rendering;
- frequent map and content iteration;
- input, UI, animation, audio, and packaging;
- a deterministic headless F# simulation;
- strong debugging of agent perception and decisions;
- an architecture that remains manageable for one principal developer directing coding agents.

The earlier architecture selected Godot too early. Godot has the strongest integrated authoring environment, but Mibo now offers a serious F#-native alternative with headless execution and raylib or MonoGame presentation backends. Raw MonoGame and raw raylib remain possible but would require more project-owned application and tooling infrastructure.

The selection must be based on equivalent implementation evidence rather than preference for F#, an editor, or a particular rendering API.

## Decision drivers

Weighted drivers for this project:

| Driver | Weight |
|---|---:|
| map and content authoring speed | 20 |
| UI, animation, audio, and presentation productivity | 15 |
| simulation isolation and headless testing | 15 |
| F# development and debugging quality | 12 |
| architectural and glue-code burden | 12 |
| runtime and packaging maturity | 10 |
| observability and debugging | 8 |
| dependency and maintenance risk | 5 |
| backend portability | 3 |

The weights may be adjusted before either spike begins, not after results are known.

## Candidates

### A. Godot .NET plus F# simulation

Expected strengths:

- integrated map, scene, UI, animation, import, and debug tooling;
- shortest likely path from greybox to coherent game presentation;
- less need to build project-specific editor and UI infrastructure;
- strong visual inspection for isometric sorting and overlays.

Expected weaknesses:

- mixed C# and F# boundary;
- simulation and editor tooling live in different idioms;
- Godot lifecycle and serialization types must be kept outside the core;
- no current Godot 4 C# web export, although the project is desktop-first.

### B. Mibo classic MVU plus raylib and Tiled

Expected strengths:

- F#-native host and application loop;
- headless and graphical programmes share concepts;
- explicit stepping and ordered systems fit deterministic simulation work;
- small, transparent rendering backend;
- potential backend substitution between raylib and MonoGame.

Expected weaknesses:

- no integrated scene, UI, animation, or map editor;
- Tiled and custom tooling must cover content workflow;
- smaller and faster-changing dependency;
- backend interchangeability may add abstraction without product value;
- application may spend more code on presentation infrastructure.

### C. Mibo classic MVU plus MonoGame and Tiled

This retains most Mibo strengths and gains MonoGame's broader content and platform foundation, but its heavier backend offers no demonstrated vertical-slice advantage yet. It may be compile-smoke-tested during the Mibo spike but is not required as a full third implementation.

### D. Raw MonoGame or raw raylib

Rejected from the first controlled comparison because each would require the project to design more lifecycle, state, UI, tooling, and content integration than the product hypothesis justifies. They remain fallback technologies if Mibo becomes the only problem in an otherwise successful code-first spike.

## Provisional assessment before spike

This is not the decision.

| Candidate | Provisional weighted score out of 5 |
|---|---:|
| Godot .NET plus F# core | 4.45 |
| Mibo plus MonoGame plus Tiled | 3.93 |
| Mibo plus raylib plus Tiled | 3.83 |
| raw MonoGame | 3.78 |
| raw raylib | 3.58 |

Godot leads because this project is likely to become content- and UX-iteration bound before it becomes rendering-API bound. Mibo remains close enough to require direct evidence because it substantially improves F# architectural unity and headless workflow.

## Required experiment

TASK-004 and TASK-005 must implement equivalent disposable hosts over the same simulation.

### Common result

- one 32 x 32 or comparably small isometric map;
- six visible agents;
- click or equivalent input to issue one typed move command;
- fixed 20 Hz authoritative simulation;
- snapshot interpolation or direct presentation sufficient for inspection;
- tick and canonical state hash overlay;
- one invalid-content failure;
- one measured map and marker edit;
- release build and local package or executable launch.

### Evidence to record

- setup and build commands;
- pinned versions;
- lines and files of host-specific glue, reported as context rather than a quality score alone;
- content edit-to-visible-result workflow;
- debugger and failure diagnostics;
- UI and overlay implementation effort;
- asset import behaviour;
- package size and launch behaviour;
- iteration friction observed by Dave;
- framework types that attempted to cross the simulation boundary;
- unresolved defects or workarounds;
- expected cost of the Bridgehead vertical slice.

## Selection rule

Choose Godot when the evidence shows materially faster or clearer content, UI, animation, and debugging work without corrupting the F# simulation boundary.

Choose Mibo when it achieves an acceptable content workflow with Tiled, keeps the project substantially simpler to debug and evolve in F#, and does not require building an informal engine around raylib.

Choose neither and stop or re-scope when both candidates require unacceptable glue, tooling, or complexity for the narrow vertical slice.

Do not maintain both production hosts after the decision. Preserve spike evidence and remove the losing host from default build and delivery paths.

## Decision

Pending TASK-004, TASK-005, and TASK-006.

Selected candidate: `TBD`

## Decision evidence template

| Driver | Weight | Godot result | Mibo result | Notes |
|---|---:|---:|---:|---|
| map and content authoring speed | 20 | | | |
| UI, animation, audio, and presentation productivity | 15 | | | |
| simulation isolation and headless testing | 15 | | | |
| F# development and debugging quality | 12 | | | |
| architectural and glue-code burden | 12 | | | |
| runtime and packaging maturity | 10 | | | |
| observability and debugging | 8 | | | |
| dependency and maintenance risk | 5 | | | |
| backend portability | 3 | | | |

### Qualitative decision

- Chosen route:
- Decisive evidence:
- Weaknesses accepted:
- Losing route removal plan:
- Review trigger:

## Consequences after acceptance

- Update `PROJECT_STATE.yaml` with the selected route.
- Move G1 to passed only after evidence review.
- Update `docs/02_TECHNOLOGY_DECISION.md` to remove obsolete provisional language.
- Pin selected dependencies.
- Remove the losing host from default build, CI, and task assumptions.
- Do not abstract over both frameworks unless a later accepted ADR demonstrates a product requirement.
