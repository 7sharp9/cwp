using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CommandoWar.Client.Godot.Tools;

/// The in-editor terrain export tool (TASK-060, backlog B-024): run this
/// from Godot's Script Editor (open this file, then File > Run, or the
/// top-right "Run" icon) while the scene containing a painted
/// <see cref="TileMapLayer"/> (using <c>art/terrain.tres</c>,
/// <see cref="TerrainTileSet"/>) is the currently edited scene. Writes
/// <c>content/scenarios/&lt;OutputName&gt;.cwscenario</c> via the shared
/// <c>CwClientCore.TerrainAuthoring.exportScenario</c> — the same function
/// <see cref="TerrainRoundTripProof"/>'s headless proof calls, so both paths
/// produce an identical file shape for an identical painted layer.
///
/// The map is sized and the painted layer positioned automatically from
/// whatever is actually painted (confirmed live 2026-09-19: Dave's first
/// real painted area used negative Y coordinates, which `RawScenario`'s
/// `[0, Width) x [0, Height)` map cannot represent directly — a fixed
/// `Width`/`Height`/origin, this script's first cut, could not have
/// handled that). `GetUsedCells`'s bounding box becomes the painted
/// footprint; the exported map is <see cref="SizeMultiplier"/> times that
/// footprint (Dave's own "fit what I painted * 2"), with the footprint
/// centred inside it — so the three placement cells this task's fixed
/// skeleton needs (see <c>CwClientCore.TerrainAuthoring</c>'s doc comment:
/// terrain-layer authoring only, everything else is a small fixed
/// skeleton) can safely sit in the map's four corners, outside whatever
/// was painted, without needing to know its shape.
[Tool]
public partial class ExportTerrainScript : EditorScript
{
	private const string TerrainLayerPath = "TileMapLayer";
	private const string OutputName = "bridgehead-test";

	/// The exported map is this many times the painted footprint's own
    /// width/height, centred around it.
    private const int SizeMultiplier = 2;

    public override void _Run()
    {
        var sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
        if (sceneRoot is null)
        {
            GD.PrintErr("ExportTerrainScript: no scene is open in the editor.");
            return;
        }

        var layerNode = sceneRoot.GetNodeOrNull<TileMapLayer>(TerrainLayerPath);
        if (layerNode is null)
        {
            GD.PrintErr(
				$"ExportTerrainScript: no TileMapLayer at '{TerrainLayerPath}' under the edited "
				+ $"scene's root ('{sceneRoot.Name}'). Update TerrainLayerPath and re-run.");
            return;
        }

        var usedCells = layerNode.GetUsedCells();
        if (usedCells.Count == 0)
        {
			GD.PrintErr("ExportTerrainScript: nothing is painted on this TileMapLayer yet.");
            return;
        }

        var minX = usedCells.Min(c => c.X);
        var maxX = usedCells.Max(c => c.X);
        var minY = usedCells.Min(c => c.Y);
        var maxY = usedCells.Max(c => c.Y);
        var footprintW = maxX - minX + 1;
        var footprintH = maxY - minY + 1;

        var width = footprintW * SizeMultiplier;
        var height = footprintH * SizeMultiplier;

        // Shift painted coordinates so the footprint's own top-left corner
        // lands at (marginX, marginY) -- centred inside the larger map,
        // with (marginX, marginY) of empty room on every side.
        var marginX = (width - footprintW) / 2;
        var marginY = (height - footprintH) / 2;
        var offsetX = marginX - minX;
        var offsetY = marginY - minY;

        var xs = new List<int>();
        var ys = new List<int>();
        var classes = new List<string>();
        var elevations = new List<int>();
        var moveCosts = new List<int>();
        var opaques = new List<bool>();

        foreach (var coords in usedCells)
        {
            var data = layerNode.GetCellTileData(coords);
            xs.Add(coords.X + offsetX);
            ys.Add(coords.Y + offsetY);
			classes.Add((string)data.GetCustomData("Class"));
			elevations.Add((int)data.GetCustomData("Elevation"));
			moveCosts.Add((int)data.GetCustomData("MoveCost"));
			opaques.Add((bool)data.GetCustomData("Opaque"));
        }

        // The four map corners: always outside the centred, margined
        // footprint (margin >= 1 cell whenever SizeMultiplier > 1),
        // regardless of the painted shape.
        var friendlyStart = new Vector2I(0, 0);
        var objectiveArea = new Vector2I(width - 1, 0);
        var exfil = new Vector2I(0, height - 1);

		var projectRoot = ProjectSettings.GlobalizePath("res://");

        var outPath = System.IO.Path.GetFullPath(
			System.IO.Path.Combine(projectRoot, "..", "..", "content", "scenarios", $"{OutputName}.cwscenario"));

        CwClientCore.TerrainAuthoring.exportScenario(
            width,
            height,
            OutputName,
            (friendlyStart.X, friendlyStart.Y),
            (objectiveArea.X, objectiveArea.Y),
            (exfil.X, exfil.Y),
            xs.ToArray(),
            ys.ToArray(),
            classes.ToArray(),
            elevations.ToArray(),
            moveCosts.ToArray(),
            opaques.ToArray(),
            outPath);

        GD.Print(
			$"ExportTerrainScript: wrote {outPath} ({xs.Count} painted cell(s), "
			+ $"footprint {footprintW}x{footprintH}, map {width}x{height}).");
    }
}
