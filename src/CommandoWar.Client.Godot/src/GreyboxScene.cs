// The authored greybox map. This is a Godot scene script: it is allowed to
// touch Godot types (Node, Vector2I, exported properties, the scene tree). Its
// single job is to read authored content and emit a framework-neutral
// RawContent. Nothing below this returns a Godot type to the caller.

using System.Collections.Generic;
using System.Linq;
using CommandoWar.Client.Godot.Content;
using Godot;

namespace CommandoWar.Client.Godot;

/// <summary>
/// Authored greybox scenario. Terrain is a multiline string of '.' (floor) and
/// '#' (wall); markers are <see cref="SpikeMarker"/> children.
/// </summary>
public partial class GreyboxScene : Node2D
{
    [Export] public int GridWidth { get; set; } = 32;
    [Export] public int GridHeight { get; set; } = 32;

    /// <summary>SplitMix64 seed as text (avoids 32-bit inspector ints).</summary>
    [Export] public string SeedText { get; set; } = "20260902";

    [Export] public int TickCount { get; set; } = 40;

    [Export(PropertyHint.MultilineText)]
    public string TerrainRows { get; set; } = "";

    /// <summary>Reads authored content into a framework-neutral DTO.</summary>
    public RawContent Read()
    {
        string[] rows = TerrainRows
            .Replace("\r", "")
            .Split('\n')
            .Where(l => l.Length > 0)
            .ToArray();

        bool[][] passable = rows
            .Select(r => r.Select(c => c != '#').ToArray())
            .ToArray();

        ulong.TryParse(SeedText, out ulong seed);

        var markers = new List<MarkerDto>();
        foreach (Node child in GetChildren())
        {
            if (child is SpikeMarker m)
                markers.Add(new MarkerDto(m.Kind, m.Id, m.Cell.X, m.Cell.Y, m.Name));
        }

        return new RawContent
        {
            Width = GridWidth,
            Height = GridHeight,
            Seed = seed,
            TickCount = TickCount,
            Passable = passable,
            Markers = markers,
            SourceScene = string.IsNullOrEmpty(SceneFilePath) ? Name : SceneFilePath,
        };
    }
}
