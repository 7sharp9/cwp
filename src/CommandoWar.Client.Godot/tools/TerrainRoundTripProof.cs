using System.Collections.Generic;
using Godot;

namespace CommandoWar.Client.Godot.Tools;

/// TASK-060 (backlog B-024) round-trip proof, run headless:
/// <c>Godot_v4.7.2-stable_mono_win64_console.exe --headless --path
/// src/CommandoWar.Client.Godot --script res://tools/TerrainRoundTripProof.cs</c>
///
/// Builds and saves the terrain <see cref="TileSet"/> (<see cref="TerrainTileSet"/>),
/// paints a small `TileMapLayer` with it programmatically (the
/// `set_cell`/Custom Data API a human clicking the same TileSet's palette in
/// the editor would produce identical `TileData` through — this proves the
/// TileSet/Custom-Data/export pipeline end to end; it does not itself prove
/// the *interactive painting experience* is good, which is Dave's own call
/// to make live in the editor), then exports it via
/// `CwClientCore.TerrainAuthoring.exportScenario` (the same function the
/// real `[Tool]` `ExportTerrainScript.cs` calls) to
/// `content/scenarios/godot-painted-test.cwscenario`.
public partial class TerrainRoundTripProof : SceneTree
{
    public override void _Initialize()
    {
        var tileSetPath = "res://art/terrain.tres";
        TerrainTileSet.BuildAndSave(tileSetPath);
        GD.Print($"wrote {tileSetPath}");

        var tileSet = GD.Load<TileSet>(tileSetPath);
        var layer = new TileMapLayer { TileSet = tileSet, YSortEnabled = true };

        // source index order matches TerrainTileSet.Sources: 0=floor, 1=block, 2=crate.
        // Elevation alternates: 0 (created with the tile), 1, 2 (CreateAlternativeTile order).
        void Paint(int x, int y, int sourceId, int elevationAlt)
        {
            layer.SetCell(new Vector2I(x, y), sourceId, Vector2I.Zero, elevationAlt);
        }

        Paint(1, 1, 0, 1); // floor, elevation 1
        Paint(2, 2, 1, 0); // block, elevation 0 (block has no elevation alternates painted here)
        Paint(3, 3, 2, 0); // crate, elevation 0
        Paint(4, 1, 0, 2); // floor, elevation 2
        Paint(0, 4, 1, 0); // block

        var xs = new List<int>();
        var ys = new List<int>();
        var classes = new List<string>();
        var elevations = new List<int>();
        var moveCosts = new List<int>();
        var opaques = new List<bool>();

        foreach (var coords in layer.GetUsedCells())
        {
            var data = layer.GetCellTileData(coords);
            xs.Add(coords.X);
            ys.Add(coords.Y);
            classes.Add((string)data.GetCustomData("Class"));
            elevations.Add((int)data.GetCustomData("Elevation"));
            moveCosts.Add((int)data.GetCustomData("MoveCost"));
            opaques.Add((bool)data.GetCustomData("Opaque"));

            GD.Print(
                $"painted ({coords.X},{coords.Y}) class={data.GetCustomData("Class")} "
                + $"elevation={data.GetCustomData("Elevation")} moveCost={data.GetCustomData("MoveCost")} "
                + $"opaque={data.GetCustomData("Opaque")}");
        }

        // res:// paths do not reliably support ".." traversal; resolve the
        // project's absolute root first, then step out to the repo root
        // with plain filesystem path handling.
        var projectRoot = ProjectSettings.GlobalizePath("res://");

        var outPath = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(projectRoot, "..", "..", "content", "scenarios", "godot-painted-test.cwscenario"));

        CwClientCore.TerrainAuthoring.exportScenario(
            6,
            6,
            "godot painted test",
            (0, 0),
            (5, 5),
            (5, 0),
            xs.ToArray(),
            ys.ToArray(),
            classes.ToArray(),
            elevations.ToArray(),
            moveCosts.ToArray(),
            opaques.ToArray(),
            outPath);

        GD.Print($"wrote {outPath}");
        GD.Print("TERRAIN_ROUND_TRIP_PROOF_DONE");
        Quit();
    }
}
