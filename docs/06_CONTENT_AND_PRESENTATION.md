# Content and Presentation Contract

Status: draft, pending framework spike  
Applies to: map authoring, scenario data, sprites, animation, UI, audio, debug overlays

## 1. Purpose

The presentation must make the simulation understandable without becoming authoritative. Content must be editable without changing simulation code, but the project must not build a general-purpose game platform before one mission works.

The framework spike compares two authoring routes:

- Godot .NET with native scene, tile-map, UI, animation, and import tooling.
- Mibo classic MVU with a raylib presentation backend and Tiled map authoring.

Both routes must consume the same framework-neutral scenario and simulation concepts.

## 2. Non-negotiable boundary

Presentation may:

- turn input into typed player commands;
- render a `RenderSnapshot`;
- animate domain events;
- display explanations and debug information;
- load and validate content before simulation start.

Presentation may not:

- decide whether an attack hits;
- decide whether an agent can see another agent;
- advance morale, suppression, objectives, or movement independently;
- mutate authoritative agent state;
- read wall-clock time as game time;
- query framework physics to determine authoritative collision;
- hide invalid content by silently supplying defaults.

## 3. Framework-neutral scenario model

The initial scenario format should remain small and typed.

```fsharp
type ScenarioDefinition =
    {
        Id: ScenarioId
        Map: MapDefinition
        FriendlyDeployments: AgentDeployment array
        EnemyDeployments: AgentDeployment array
        Objectives: ObjectiveDefinition array
        ExtractionAreas: AreaDefinition array
        StaticObjects: StaticObjectDefinition array
        Rules: ScenarioRules
    }
```

Only add a field when a current task demonstrates that the vertical slice needs it.

### Initial objective algebra

```fsharp
type ObjectiveDefinition =
    | ReachArea of ObjectiveId * AreaId
    | HoldArea of ObjectiveId * AreaId * TickCount
    | DestroyTarget of ObjectiveId * EntityId
    | ExtractAgents of ObjectiveId * AgentSelection * AreaId
    | AllOf of ObjectiveId * ObjectiveDefinition array
    | Optional of ObjectiveDefinition
```

The bridge-demolition interaction may initially be represented as `ReachArea` followed by a fixed-duration plant action and `DestroyTarget`. Do not add a general interaction scripting language.

### Realised by TASK-008

The framework-neutral scenario model, its one-pass validator, and the
content-format version constant are `src/CommandoWar.Sim/Scenario.fs`, inside
the simulation library (`docs/03_ARCHITECTURE.md` section 8 lists `Scenario.fs`
there). The pipeline is `RawScenario` (loosely typed authored input) ->
`Scenario.validate` -> `Scenario` -> `World.ofScenario`. The content-format
version is `ScenarioContent.Version` (currently 1), independent of the
canonical-state and replay format versions. The objective algebra
(`Objective`: `ReachArea` / `HoldArea` / `DestroyTarget` / `ExtractAgents` /
`AllOf` / `Optional`) is a data-only type; evaluation is deferred (B-032).
Per-cell terrain, cover, and opacity are not in the model yet (B-008); the
scenario carries map dimensions and marker positions only. See
`docs/04_SIMULATION_SPEC.md` section 21.

## 4. Map contract

The simulation uses a logical two-dimensional grid with an explicit level or elevation value. The renderer projects that grid into an isometric view.

### Required logical layers

| Layer | Authoritative use | Presentation use |
|---|---|---|
| Terrain | movement class and cost | ground tiles |
| Elevation | line of sight and traversal | vertical offset |
| Low cover | directional cover | walls, crates, banks |
| High occlusion | opacity and blocked sight | buildings, trees, cliffs |
| Traversal links | doors, ladders, crossings | visual connection |
| Static objects | targets and obstacles | props and objective art |
| Areas | objectives, extraction, deployment | hidden markers and overlays |
| Decoration | none | decals, clutter, weathering |

Decorative layers must not silently affect movement, cover, or visibility.

### Initial marker classes

- `FriendlySpawn`
- `EnemySpawn`
- `ObjectiveArea`
- `ExtractionArea`
- `StaticTarget`
- `DebugCameraStart`

Patrol graphs, arbitrary triggers, cut-scene markers, reinforcement schedules, and general scripting are deferred.

## 5. Godot authoring path

The Godot spike should use:

- `TileMapLayer` or the current equivalent for visual tile layers;
- nodes or resources for typed deployment and area markers;
- a C# adapter that converts authored content to framework-neutral DTOs;
- editor validation where practical;
- runtime validation in all cases.

The adapter must fail with explicit field and object names when content is invalid. It may not pass Godot nodes, vectors, resources, or object references into the F# simulation.

## 6. Mibo and Tiled authoring path

The Mibo spike should use:

- Tiled JSON as the external map representation;
- custom properties or typed classes for simulation metadata;
- one importer that converts Tiled data into the same framework-neutral DTOs;
- Mibo classic MVU for the host loop;
- the raylib backend for the first graphical spike.

The importer must not expose Tiled JSON objects to the simulation. Parsing, schema migration, and validation belong at the content boundary.

## 7. Validation

Content is invalid when any of the following is true:

- an entity ID is duplicated;
- a deployment lies outside the map or in an impassable cell;
- an objective refers to a missing area or entity;
- an extraction area contains no traversable cell;
- directional cover is malformed;
- a required marker is missing;
- a tile or object uses an unknown simulation class;
- a content version is unsupported;
- a scenario has no achievable completion path under its static definition.

The last condition may begin as a limited structural check. Do not claim general solvability proof.

Validation errors must stop scenario loading and identify the source object, property, and expected value.

## 8. Retro visual contract

The project should look intentionally retro without inheriting the usability limits of 1990 software.

### Initial visual rules

- Render the world at a low logical resolution, initially 640 x 360 unless the spike demonstrates a better target.
- Use nearest-neighbour scaling for pixel assets.
- Use integer scaling where the display permits it.
- Render UI text at a resolution that remains legible rather than forcing all UI through the low-resolution world buffer.
- Prefer a restrained palette and strong silhouettes.
- Use four or eight directional facings, selected after the animation-cost spike.
- Keep animation frame counts deliberate and consistent.
- Avoid post-processing that obscures cover, routes, or unit state.
- CRT effects must be optional and disabled by default.

### Modern usability requirements

- tactical pause or configurable slow motion;
- rebindable controls;
- mouse and keyboard support first, controller support before external playtest;
- scalable text and UI;
- status indicators that do not rely on colour alone;
- route and order previews;
- clear selection and hover states;
- fast restart and deterministic replay;
- concise explanations for refusal, delay, adaptation, and panic.

## 9. Asset pipeline

Use placeholders until the command loop passes its headless gate.

A likely production path is:

```text
low-poly model and shared rig
    -> fixed orthographic render camera
    -> directional animation renders
    -> palette reduction and pixel cleanup
    -> sprite atlas
    -> presentation animation definitions
```

This is a production hypothesis, not an early requirement. Before committing to it, make a single soldier with one movement cycle and one firing cycle, then measure cleanup time and visual quality.

Do not commission or produce a complete unit roster before the core interaction is externally validated.

## 10. Presentation states

The initial client must support only:

- title or scenario selection screen;
- loading failure screen with actionable diagnostics;
- active tactical view;
- tactical pause and command composition;
- mission success or failure summary;
- replay playback;
- developer overlay enabled by a launch option.

Campaign, inventory, character progression, codex, mod browser, cinematic editor, and online services are outside the vertical slice.

## 11. Required tactical overlays

Player-facing:

- selected unit and fireteam;
- proposed destination and path;
- known threat markers with confidence or age cues;
- cover and exposure preview for the selected command;
- order acknowledgement and disposition;
- current objective and extraction state.

Developer-facing:

- logical grid and coordinates;
- blocked and reserved cells;
- line-of-sight rays and occluders;
- known versus authoritative enemy positions;
- current commitment and executor state;
- suppression, stress, discipline, and trust values;
- last appraisal factors and selected reason;
- simulation tick, state hash, and random draw counter.

## 12. Content iteration metric

Each framework spike must measure a trivial map change:

1. Move one wall or cover object.
2. Add one spawn marker.
3. Change one simulation property.
4. Run the scenario.
5. Confirm that invalid data produces a useful error.

Record:

- number of files touched;
- edit-to-visible-result time;
- manual conversion steps;
- hot-reload or restart behaviour;
- quality of diagnostics;
- code required for the change;
- any framework type that leaked into simulation code.

The result is evidence for ADR-0001, not a polished pipeline.

## 13. Deferred content architecture

Do not add these before the vertical-slice gate:

- general-purpose scenario scripts;
- a public mod SDK;
- arbitrary user-defined AI behaviours;
- multiple historical or science-fiction settings;
- procedural maps;
- streaming worlds;
- dynamic weather systems;
- campaign persistence;
- localization pipeline beyond externalised player-facing strings;
- proprietary map editor.
