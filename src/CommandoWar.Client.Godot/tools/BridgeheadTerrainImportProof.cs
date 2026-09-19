using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CommandoWar.Client.Godot.Tools;

/// TASK-061 (backlog B-025) headless import round-trip proof, run through
/// the real Godot 4.7.2 editor headlessly -- the
/// <see cref="TerrainRoundTripProof"/> precedent, reversed:
///
/// <c>Godot_v4.7.2-stable_mono_win64_console.exe --headless --path
/// src/CommandoWar.Client.Godot --script
/// res://tools/BridgeheadTerrainImportProof.cs</c>
///
/// Reads <c>content/scenarios/bridgehead.cwscenario</c>'s authored terrain
/// layer via <c>CwClientCore.TerrainAuthoring.importTerrainLayer</c>, paints
/// every cell onto a fresh <see cref="TileMapLayer"/> by resolving each one
/// through <see cref="TerrainTileSet.Resolve"/> and calling
/// <c>TileMapLayer.SetCell</c> -- the identical import path
/// <see cref="ImportTerrainScript"/> uses, exercised here without needing an
/// open editor scene/display (this environment has none, the same gap
/// TASK-060 flagged for the export side) -- then reads the painted layer
/// back the same way <see cref="ExportTerrainScript"/> does
/// (<c>GetUsedCells</c>/<c>GetCellTileData</c>) and diffs that re-exported
/// cell set against the original authored cell set for an exact,
/// order-independent match. This proves the TileSet/Custom-Data/import/
/// export pipeline round-trips a real authored terrain layer unchanged; it
/// does not itself prove the *interactive painting experience* is good,
/// which is Dave's own call to make live in the editor (flagged, not
/// resolved, by this task).
public partial class BridgeheadTerrainImportProof : SceneTree
{
	public override void _Initialize()
	{
		var tileSetPath = "res://art/terrain.tres";
		TerrainTileSet.BuildAndSave(tileSetPath);
		GD.Print($"wrote {tileSetPath}");

		var tileSet = GD.Load<TileSet>(tileSetPath);
		var layer = new TileMapLayer { TileSet = tileSet, YSortEnabled = true };

		var projectRoot = ProjectSettings.GlobalizePath("res://");
		var scenarioPath = System.IO.Path.GetFullPath(
			System.IO.Path.Combine(projectRoot, "..", "..", "content", "scenarios", "bridgehead.cwscenario"));

		GD.Print($"importing {scenarioPath}");
		var imported = CwClientCore.TerrainAuthoring.importTerrainLayer(scenarioPath);
		GD.Print($"{imported.CellX.Length} authored cell(s) read from the .cwscenario terrain layer");

		// --- import: identical path to ImportTerrainScript._Run ------------
		var painted = 0;
		var unresolved = 0;

		for (var i = 0; i < imported.CellX.Length; i++)
		{
			var cell = new Vector2I(imported.CellX[i], imported.CellY[i]);
			var resolved = TerrainTileSet.Resolve(
				imported.Class[i], imported.MoveCost[i], imported.Opaque[i], imported.Elevation[i]);

			if (resolved is null)
			{
				GD.PrintErr(
					$"cell ({cell.X},{cell.Y}) class={imported.Class[i]} moveCost={imported.MoveCost[i]} "
					+ $"opaque={imported.Opaque[i]} elevation={imported.Elevation[i]} did not resolve to a "
					+ "known TerrainTileSet source.");
				unresolved++;
				continue;
			}

			var (sourceId, alternateId) = resolved.Value;
			layer.SetCell(cell, sourceId, Vector2I.Zero, alternateId);
			painted++;
		}

		GD.Print($"painted {painted} cell(s), {unresolved} unresolved");

		// --- re-export: identical read-back to ExportTerrainScript._Run ----
		var reExported = new List<(int X, int Y, string Class, int Elevation, int MoveCost, bool Opaque)>();

		foreach (var coords in layer.GetUsedCells())
		{
			var data = layer.GetCellTileData(coords);
			reExported.Add((
				coords.X,
				coords.Y,
				(string)data.GetCustomData("Class"),
				(int)data.GetCustomData("Elevation"),
				(int)data.GetCustomData("MoveCost"),
				(bool)data.GetCustomData("Opaque")));
		}

		var original = Enumerable.Range(0, imported.CellX.Length)
			.Select(i => (
				X: imported.CellX[i],
				Y: imported.CellY[i],
				Class: imported.Class[i],
				Elevation: imported.Elevation[i],
				MoveCost: imported.MoveCost[i],
				Opaque: imported.Opaque[i]))
			.ToList();

		var originalSet = original.ToHashSet();
		var reExportedSet = reExported.ToHashSet();

		var missing = originalSet.Except(reExportedSet).ToList();
		var extra = reExportedSet.Except(originalSet).ToList();

		GD.Print($"original cell count: {originalSet.Count}, re-exported cell count: {reExportedSet.Count}");

		if (unresolved == 0 && missing.Count == 0 && extra.Count == 0 && originalSet.Count == reExportedSet.Count)
		{
			GD.Print("TERRAIN_ROUND_TRIP_MATCH: true (exact, order-independent match)");
		}
		else
		{
			GD.Print("TERRAIN_ROUND_TRIP_MATCH: false");
			foreach (var cell in missing)
			{
				GD.PrintErr($"  missing from re-export: {cell}");
			}

			foreach (var cell in extra)
			{
				GD.PrintErr($"  extra in re-export: {cell}");
			}
		}

		GD.Print("BRIDGEHEAD_TERRAIN_IMPORT_PROOF_DONE");
		Quit();
	}
}
