# Research Sources

Research snapshot: 2026-09-02

This file records the external material used to evaluate the framework options. Version numbers and support claims must be rechecked before installation or upgrade.

## Historical concept

Research snapshot: 2026-09-21.

- Games That Weren't, "Commando War"
  https://www.gamesthatwerent.com/2020/07/commando-war/
- Amiga Magazine Rack, "Commando War" preview from *Zero* issue 9 (July 1990, David Wilson)
  https://amr.abime.net/review_39855
- *Generation 4* magazine (April 1990) preview, cited by Games That Weren't (no stable direct URL; scan not independently re-verified beyond the secondary citation)

The project must not depend on the historical name, artwork, maps, text, or any assumed ownership status.

### Relevant findings

`Commando War` was an unreleased 1990 Titus Software title for Amiga, Atari ST, and PC, announced and previewed in the French and UK press (*Génération 4*, *Zero*) but never shipped on any platform — it was delayed into 1991 and then disappeared with no further coverage. Everything known about it comes from period magazine previews of pre-release material, not a played, reviewable, or shipped game. There is no surviving build, and no source describes final, tested behaviour.

What the previews actually describe:

- A squad-command game for up to 10 commandos (or 5-a-side head-to-head), presented in a flip-screen (not scrolling) overhead view across a series of mission maps.
- Click-to-direct control: the player points at the map and the soldiers "intelligently react with the environment around them" — the closest documented precedent is Cannon Fodder's point-and-move pathing (itself released later, 1993), not a negotiated or refusable order model.
- Planned vehicle entry/riding.
- Marketed as a hybrid of *Populous* (god-game framing) and *Cannon Fodder* (arcade squad action), promoted around Titus's "Action Concept" — a reusable engine meant to support cheap themed expansion disks (Vietnam, Roman, Viking settings) sold as add-ons once the base game was owned.

What no source describes, in any preview: order refusal, negotiated compliance, appraisal of an order against battlefield conditions, suppression/stress/discipline mechanics, or any legible command-and-response loop. The "intelligently react with the environment" language is the entire extent of the AI claim, and period previews read it as pathing/obstacle-awareness, not as agency over whether an order is obeyed.

### Cross-reference against `docs/00_PROJECT_CHARTER.md`

The charter (section 8) already states the correct legal/IP posture: the historical concept "may inform high-level mechanics that are independently implemented," and the public game must not use the `Commando War` name without verified rights. That framing holds up against what the sources actually show, for a reason the charter does not currently spell out: there is nothing in the historical record to independently implement. The one attested AI trait ("intelligently react with the environment," never released, never played, sourced only from pre-release marketing previews) is not evidence for the charter's central premise in section 1 — bounded autonomy, legible refusal, and a player-diagnosable command loop. That premise is this project's own invention, not a revival of a documented mechanic. Calling the project a "spiritual successor" is accurate only at the level of setting/squad-command framing (a small commando squad, mission maps, click-to-order control); it is not a successor to any command-refusal design, because the original never had or demonstrated one. This is worth stating explicitly wherever the project's positioning is described externally, so "spiritual successor" is not read as implying the refusal/appraisal loop has a historical precedent it does not have.

## Godot

- Godot download archive and 4.7.2 release  
  https://godotengine.org/download/archive/
- Godot C#/.NET basics  
  https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html
- Godot `TileMapLayer` reference  
  https://docs.godotengine.org/en/stable/classes/class_tilemaplayer.html
- Godot tile-map authoring guide  
  https://docs.godotengine.org/en/stable/tutorials/2d/using_tilemaps.html
- Godot debugging and profiling documentation  
  https://docs.godotengine.org/en/stable/tutorials/scripting/debug/index.html
- Godot licence  
  https://godotengine.org/license/

### Relevant findings

- Godot supplies an integrated editor, scene model, tile-map tooling, UI, animation, asset import, and runtime debugging.
- The .NET edition supports C# and ordinary .NET dependencies.
- Godot 4 C# web export remains unavailable in the cited stable documentation, so this plan is desktop-first.
- Godot is the strongest candidate when map, UI, animation, and content iteration dominate the work.

## MonoGame

- MonoGame documentation  
  https://docs.monogame.net/
- “What is MonoGame?”  
  https://docs.monogame.net/articles/getting_started/1_what_is_monogame.html
- Supported platforms  
  https://docs.monogame.net/articles/getting_started/platforms.html
- Content pipeline documentation  
  https://docs.monogame.net/articles/content/why_content_pipeline.html
- MonoGame releases  
  https://github.com/MonoGame/MonoGame/releases

### Relevant findings

- MonoGame is a code-first framework rather than a scene and game editor.
- It is compatible with .NET languages, including F#.
- It provides rendering, audio, input, content processing, and cross-platform foundations, but expects the project to supply or select higher-level architecture and authoring tools.
- Raw MonoGame is therefore less attractive than Mibo plus MonoGame for this specific F# experiment.

## raylib and raylib-cs

- raylib official site  
  https://www.raylib.com/
- raylib repository and releases  
  https://github.com/raysan5/raylib
- raylib FAQ  
  https://github.com/raysan5/raylib/wiki/Frequently-Asked-Questions
- raylib-cs .NET binding  
  https://github.com/raylib-cs/raylib-cs

### Relevant findings

- raylib deliberately provides a small code-first library without a scene editor or broad engine toolchain.
- It is well suited to a compact custom renderer and transparent low-level control.
- Used raw, it leaves too much UI, content, tooling, and lifecycle work to this project.
- It is more credible here as Mibo's lightweight presentation backend than as the entire application architecture.

## Mibo

- Mibo repository and README  
  https://github.com/AngelMunoz/Mibo
- Mibo documentation  
  https://angelmunoz.github.io/Mibo/
- Headless programs  
  https://angelmunoz.github.io/Mibo/mvu/headless.html
- System pipeline  
  https://angelmunoz.github.io/Mibo/mvu/system.html
- Adaptive architecture overview  
  https://angelmunoz.github.io/Mibo/adaptive/overview.html
- Mibo changelog  
  https://github.com/AngelMunoz/Mibo/blob/main/CHANGELOG.md
- Mibo issues  
  https://github.com/AngelMunoz/Mibo/issues

### Relevant findings

- Mibo is an F# Elmish/MVU framework with headless execution and interchangeable raylib-cs and MonoGame presentation backends.
- Its headless runner supports explicit stepping and virtual time, which is useful for simulation tests and servers.
- Its ordered system pipeline is compatible with an explicit simulation phase model.
- Mibo.Adaptive is documented as experimental, and current open issues demonstrate correctness and threading edge cases that this small initial project does not need.
- Mibo is actively changing. Any spike must pin an exact package version and retain an escape route.

## Tiled

- Tiled map editor  
  https://www.mapeditor.org/
- Tiled documentation  
  https://doc.mapeditor.org/
- JSON map format  
  https://doc.mapeditor.org/en/stable/reference/json-map-format/
- Custom properties and classes  
  https://doc.mapeditor.org/en/stable/manual/custom-properties/

### Relevant findings

- Tiled supports isometric maps, object layers, templates, terrain tools, JSON export, and custom properties or classes.
- It is a plausible external authoring tool for a Mibo, MonoGame, or raylib client.
- Its data must still pass through a project-owned importer and validator before entering the simulation.

## F# performance and design

- Microsoft F# performance guidance  
  https://learn.microsoft.com/dotnet/fsharp/language-reference/fsharp-code-optimization

The architecture permits arrays, structs, controlled mutation, and data-oriented hot stores where measurements justify them. “Functional core” does not require allocating a new object graph for every tick.
