using Godot;

namespace CommandoWar.Client.Godot.Tools;

/// The in-editor terrain import tool (TASK-061, backlog B-025), the reverse
/// of <see cref="ExportTerrainScript"/>: run this from Godot's Script
/// Editor (open this file, then File > Run, or the top-right "Run" icon)
/// while the scene containing the <see cref="TileMapLayer"/> to paint into
/// (using <c>art/terrain.tres</c>, <see cref="TerrainTileSet"/> --
/// <c>scenes/TerrainAuthoring.tscn</c> is the intended target) is the
/// currently edited scene. Reads <see cref="InputPath"/> via the shared
/// <c>CwClientCore.TerrainAuthoring.importTerrainLayer</c> -- the same
/// function <c>BridgeheadTerrainImportProof</c>'s headless proof calls, so
/// both paths resolve an identical cell list identically -- and paints
/// every authored cell onto the layer, resolving each cell's
/// (Class, MoveCost, Opaque) to a <see cref="TerrainTileSet.Resolve"/>
/// match. A cell that does not resolve is reported (via <c>GD.PrintErr</c>),
/// never silently dropped or mis-painted.
///
/// Not run through the real interactive Godot editor in this task's
/// implementing environment (no display) -- confirmed working end to end
/// headlessly instead via <c>BridgeheadTerrainImportProof.cs</c>, which
/// exercises the identical `importTerrainLayer` + `TerrainTileSet.Resolve`
/// + `TileMapLayer.SetCell` path this script uses. The real interactive-
/// editor run (opening `TerrainAuthoring.tscn`, running this script, and
/// confirming the painted result visually matches intent) is flagged for
/// Dave, the same gap TASK-060 flagged for `ExportTerrainScript.cs`.
[Tool]
public partial class ImportTerrainScript : EditorScript
{
	private const string TerrainLayerPath = "TileMapLayer";

	/// The `.cwscenario` file to import, relative to the repo root (the
	/// `ExportTerrainScript.OutputName` precedent -- edit and re-run for a
	/// different file).
	private const string InputPath = "content/scenarios/bridgehead.cwscenario";

	public override void _Run()
	{
		var sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
		if (sceneRoot is null)
		{
			GD.PrintErr("ImportTerrainScript: no scene is open in the editor.");
			return;
		}

		var layerNode = sceneRoot.GetNodeOrNull<TileMapLayer>(TerrainLayerPath);
		if (layerNode is null)
		{
			GD.PrintErr(
				$"ImportTerrainScript: no TileMapLayer at '{TerrainLayerPath}' under the edited "
				+ $"scene's root ('{sceneRoot.Name}'). Update TerrainLayerPath and re-run.");
			return;
		}

		var projectRoot = ProjectSettings.GlobalizePath("res://");

		var inPath = System.IO.Path.GetFullPath(
			System.IO.Path.Combine(projectRoot, "..", "..", InputPath));

		if (!System.IO.File.Exists(inPath))
		{
			GD.PrintErr($"ImportTerrainScript: input file not found: {inPath}");
			return;
		}

		var layer = CwClientCore.TerrainAuthoring.importTerrainLayer(inPath);

		// An import replaces the layer's content, it does not overlay onto
		// whatever was already painted (found live 2026-09-19: the
		// committed TerrainAuthoring.tscn still carried a large leftover
		// hand-painted test area from TASK-060's own review rounds, so the
		// first run of this script silently mixed 43 new Bridgehead cells
		// into that old content instead of showing a clean map).
		layerNode.Clear();

		var painted = 0;
		var unresolved = 0;

		for (var i = 0; i < layer.CellX.Length; i++)
		{
			var cell = new Vector2I(layer.CellX[i], layer.CellY[i]);
			var resolved = TerrainTileSet.Resolve(layer.Class[i], layer.MoveCost[i], layer.Opaque[i], layer.Elevation[i]);

			if (resolved is null)
			{
				GD.PrintErr(
					$"ImportTerrainScript: cell ({cell.X},{cell.Y}) class={layer.Class[i]} "
					+ $"moveCost={layer.MoveCost[i]} opaque={layer.Opaque[i]} elevation={layer.Elevation[i]} "
					+ "does not match a known TerrainTileSet source -- not painted.");
				unresolved++;
				continue;
			}

			var (sourceId, alternateId) = resolved.Value;
			layerNode.SetCell(cell, sourceId, Vector2I.Zero, alternateId);
			painted++;
		}

		var note = unresolved > 0 ? $", {unresolved} unresolved (see errors above)" : "";
		GD.Print($"ImportTerrainScript: painted {painted} cell(s) from {inPath}{note}.");
	}
}
