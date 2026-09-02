# Research Sources

Research snapshot: 2026-09-02

This file records the external material used to evaluate the framework options. Version numbers and support claims must be rechecked before installation or upgrade.

## Historical concept

- Games That Weren't, “Commando War”  
  https://www.gamesthatwerent.com/2020/07/commando-war/

The project must not depend on the historical name, artwork, maps, text, or any assumed ownership status.

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
