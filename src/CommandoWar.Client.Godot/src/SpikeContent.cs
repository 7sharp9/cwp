// Framework-neutral spike content model and validation.
//
// This file contains NO Godot types. It is the "Content DTOs -> validation ->
// Sim setup" stage from ADR-0002: authored content (read elsewhere, by
// GreyboxScene.cs, which is allowed to touch Godot) is converted into these
// plain DTOs, validated here, and only then handed to the simulation.
//
// Terrain/passability is a CLIENT-SIDE concept in this spike: CommandoWar.Sim
// has no terrain model yet (backlog B-008), so "walls" are authored, validated
// against, and rendered, but never sent across the simulation boundary.

using System;
using System.Collections.Generic;
using System.Linq;

namespace CommandoWar.Client.Godot.Content;

public enum MarkerKind
{
    FriendlySpawn,
    EnemySpawn,
    ObjectiveArea,
}

/// <summary>One authored marker, already reduced to primitives.</summary>
public sealed record MarkerDto(MarkerKind Kind, int Id, int X, int Y, string SourceName);

/// <summary>
/// Raw authored content before validation. Produced by the Godot scene reader;
/// consumed by <see cref="ContentValidator"/>.
/// </summary>
public sealed class RawContent
{
    public int Width { get; init; }
    public int Height { get; init; }
    public ulong Seed { get; init; }
    public int TickCount { get; init; }
    /// <summary>Row-major, [y][x]. true = passable floor, false = wall.</summary>
    public bool[][] Passable { get; init; } = Array.Empty<bool[]>();
    public IReadOnlyList<MarkerDto> Markers { get; init; } = Array.Empty<MarkerDto>();
    public string SourceScene { get; init; } = "(unknown)";
}

/// <summary>A validated scenario, safe to hand to <see cref="Sim.SimFacade"/>.</summary>
public sealed class ScenarioDto
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required ulong Seed { get; init; }
    public required int TickCount { get; init; }
    public required bool[][] Passable { get; init; }
    public required IReadOnlyList<MarkerDto> Markers { get; init; }

    public IEnumerable<MarkerDto> FriendlySpawns => Markers.Where(m => m.Kind == MarkerKind.FriendlySpawn);
    public IEnumerable<MarkerDto> EnemySpawns => Markers.Where(m => m.Kind == MarkerKind.EnemySpawn);
    public IEnumerable<MarkerDto> ObjectiveAreas => Markers.Where(m => m.Kind == MarkerKind.ObjectiveArea);

    public bool IsPassable(int x, int y) =>
        x >= 0 && y >= 0 && x < Width && y < Height && Passable[y][x];
}

/// <summary>
/// Raised when authored content is invalid. Carries every error found in one
/// pass so a single load surfaces all problems (docs/03 section 17). The host
/// shows this to the user and refuses to start the simulation; it is NOT a
/// simulation-step error and is the one place the spike deliberately catches.
/// </summary>
public sealed class ContentException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public ContentException(string sourceScene, IReadOnlyList<string> errors)
        : base($"Invalid content in '{sourceScene}':\n  - " + string.Join("\n  - ", errors))
    {
        Errors = errors;
    }
}

public static class ContentValidator
{
    /// <summary>
    /// Validates raw authored content. Throws <see cref="ContentException"/>
    /// naming each offending object and the expected value.
    /// </summary>
    public static ScenarioDto Validate(RawContent raw)
    {
        var errors = new List<string>();

        if (raw.Width <= 0 || raw.Height <= 0)
            errors.Add($"grid size must be positive, got {raw.Width} x {raw.Height}");

        if (raw.TickCount < 0)
            errors.Add($"TickCount must be >= 0, got {raw.TickCount}");

        bool gridWellFormed =
            raw.Width > 0 && raw.Height > 0 &&
            raw.Passable.Length == raw.Height && raw.Passable.All(row => row.Length == raw.Width);

        if (!gridWellFormed && raw.Width > 0 && raw.Height > 0)
            errors.Add(
                $"terrain grid is {raw.Passable.Length} row(s) of " +
                $"[{string.Join(",", raw.Passable.Select(r => r.Length).Distinct())}], expected {raw.Height} rows of {raw.Width}");

        // Marker-level checks (only where the grid is usable).
        var seenCells = new Dictionary<(int, int), string>();
        var seenIds = new Dictionary<(MarkerKind, int), string>();

        foreach (var m in raw.Markers)
        {
            string label = $"{m.Kind} #{m.Id} ('{m.SourceName}')";

            if (m.X < 0 || m.Y < 0 || m.X >= raw.Width || m.Y >= raw.Height)
            {
                errors.Add($"{label} at ({m.X},{m.Y}) lies outside the {raw.Width} x {raw.Height} grid");
                continue;
            }

            if (seenIds.TryGetValue((m.Kind, m.Id), out var other))
                errors.Add($"{label} reuses id {m.Id}, already used by {other}");
            else
                seenIds[(m.Kind, m.Id)] = label;

            bool blocksMovement = m.Kind is MarkerKind.FriendlySpawn or MarkerKind.EnemySpawn;

            if (blocksMovement)
            {
                if (gridWellFormed && !raw.Passable[m.Y][m.X])
                    errors.Add($"{label} at ({m.X},{m.Y}) lies on an impassable (wall) cell");

                if (seenCells.TryGetValue((m.X, m.Y), out var occupant))
                    errors.Add($"{label} shares cell ({m.X},{m.Y}) with {occupant}");
                else
                    seenCells[(m.X, m.Y)] = label;
            }
        }

        if (!raw.Markers.Any(m => m.Kind == MarkerKind.FriendlySpawn))
            errors.Add("no FriendlySpawn marker: the scenario has no deployable agents");

        if (!raw.Markers.Any(m => m.Kind == MarkerKind.ObjectiveArea))
            errors.Add("no ObjectiveArea marker: the scenario has no objective");

        if (errors.Count > 0)
            throw new ContentException(raw.SourceScene, errors);

        return new ScenarioDto
        {
            Width = raw.Width,
            Height = raw.Height,
            Seed = raw.Seed,
            TickCount = raw.TickCount,
            Passable = raw.Passable,
            Markers = raw.Markers,
        };
    }
}
