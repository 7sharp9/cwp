using Godot;

namespace CommandoWar.Client.Godot.Tools;

/// Builds the TASK-060 terrain <see cref="TileSet"/> (backlog B-024): three
/// atlas sources (TASK-041's floor/block/crate placeholder sprites), four
/// Custom Data Layers (Class/MoveCost/Opaque/Elevation) carrying the
/// <c>RawTerrainCell</c> fields, Elevation via three alternates (0, 1, 2)
/// per source since it is the one field that varies per placed instance
/// rather than per terrain type. <c>RawCoverFeature</c> (Cover) is not
/// authored through this TileSet — a single tile carries one Custom Data
/// set, but cover is up to four independent (direction, level) pairs per
/// cell, a genuine modelling mismatch with a per-tile-type property. Cover
/// stays hand-authored directly in a <c>.cwscenario</c> file for now (see
/// TASK-060's ledger for the reasoning).
public static class TerrainTileSet
{
	/// One atlas source: its art, and the Class/MoveCost/Opaque this
	/// project's terrain semantics assign to it (`Scenario.fs`'s
	/// `RawTerrainCell` doc comment; `art/LICENSE-THIRD-PARTY.md`).
	private static readonly (string Path, string Class, int MoveCost, bool Opaque)[] Sources =
	{
		("res://art/terrain_floor.png", "passable", 1, false),
		("res://art/terrain_block.png", "impassable", 0, true),
		("res://art/terrain_crate.png", "passable", 3, true),
	};

	private static int AddLayer(TileSet tileSet, string name, Variant.Type type)
	{
		tileSet.AddCustomDataLayer();
		var index = tileSet.GetCustomDataLayersCount() - 1;
		tileSet.SetCustomDataLayerName(index, name);
		tileSet.SetCustomDataLayerType(index, type);
		return index;
	}

	public static TileSet Build()
	{
		// The source art is isometric (Kenney's own pack description: 256px
		// tile width, 128px "base floor height" -- the diamond footprint;
		// the remaining 512-128=384px is the block's height above that
		// footprint). Leaving TileShape at its Square default while placing
		// a 256x512 sprite made painting only land correctly on alternating
		// grid cells (confirmed both as a visual bug live and by a
		// temporary render-to-PNG probe, removed after use, comparing
		// Square vs Isometric shape side by side) -- TileSize must be the
		// isometric footprint (256, 128), not the full sprite bounds.
		var tileSet = new TileSet
		{
			TileShape = TileSet.TileShapeEnum.Isometric,
			TileOffsetAxis = TileSet.TileOffsetAxisEnum.Horizontal,
			TileSize = new Vector2I(256, 128),
		};

		AddLayer(tileSet, "Class", Variant.Type.String);
		AddLayer(tileSet, "MoveCost", Variant.Type.Int);
		AddLayer(tileSet, "Opaque", Variant.Type.Bool);
		AddLayer(tileSet, "Elevation", Variant.Type.Int);

		foreach (var (path, cls, moveCost, opaque) in Sources)
		{
			var texture = GD.Load<Texture2D>(path);
			var source = new TileSetAtlasSource { Texture = texture, TextureRegionSize = new Vector2I(256, 512) };
			source.CreateTile(Vector2I.Zero);

			// A TileData's SetCustomData needs its owning TileSet resolved
			// to look up each layer's declared type -- that only exists
			// once the source is attached, so AddSource must happen before
			// any GetTileData/SetCustomData call, not after.
			tileSet.AddSource(source);

			for (var elevation = 0; elevation <= 2; elevation++)
			{
				var alt = elevation == 0 ? 0 : source.CreateAlternativeTile(Vector2I.Zero);
				var data = source.GetTileData(Vector2I.Zero, alt);
				data.SetCustomData("Class", cls);
				data.SetCustomData("MoveCost", moveCost);
				data.SetCustomData("Opaque", opaque);
				data.SetCustomData("Elevation", elevation);
				// Bottom-anchor the tall 512px sprite to the 128px-tall
				// diamond footprint (FSharpSceneHost.cs's DrawTerrainTile
				// doc comment: "a tall, bottom-anchored canvas... its own
				// bottom edge is the tile's near/south corner") -- confirmed
				// visually via a temporary render-to-PNG probe, removed after
				// use, comparing the default anchor against this offset.
				data.TextureOrigin = new Vector2I(0, -384);
			}
		}

		return tileSet;
	}

	/// Builds and saves the TileSet to <paramref name="resPath"/> (a
	/// <c>res://</c> path). Re-run this whenever the source art or the
	/// Class/MoveCost/Opaque table above changes — the committed
	/// <c>art/terrain.tres</c> is generated output, not hand-edited.
	public static void BuildAndSave(string resPath)
	{
		var tileSet = Build();
		var err = ResourceSaver.Save(tileSet, resPath);
		if (err != Error.Ok)
		{
			throw new System.InvalidOperationException($"ResourceSaver.Save({resPath}) failed: {err}");
		}
	}

	/// The reverse of <see cref="Sources"/> (TASK-061, backlog B-025): finds
	/// the source whose (Class, MoveCost, Opaque) exactly matches an
	/// authored <c>RawTerrainCell</c>'s, and returns its source index plus
	/// the alternate tile id for <paramref name="elevation"/>. The source
	/// index doubles as the atlas source id (<see cref="Build"/> calls
	/// <c>AddSource</c> in <see cref="Sources"/> order, so source `i`'s id
	/// is `i` — the same assumption <c>TerrainRoundTripProof.cs</c>'s own
	/// "source index order matches TerrainTileSet.Sources" comment
	/// documents). The alternate id equals <paramref name="elevation"/>
	/// directly for 0/1/2: elevation 0 is the tile <see cref="Build"/>
	/// creates with the source (alternate 0), and Godot's
	/// <c>CreateAlternativeTile</c> assigns the next free positive id when
	/// none is given, i.e. 1 then 2 for the two calls <see cref="Build"/>
	/// makes per source. Returns <c>null</c> (does not throw, does not
	/// silently pick a nearest match) when no source matches or elevation
	/// is outside <c>[0, 2]</c> — the caller reports this, it never
	/// silently mis-paints a cell.
	public static (int SourceId, int AlternateId)? Resolve(string cls, int moveCost, bool opaque, int elevation)
	{
		if (elevation < 0 || elevation > 2)
		{
			return null;
		}

		for (var i = 0; i < Sources.Length; i++)
		{
			var (_, sourceClass, sourceMoveCost, sourceOpaque) = Sources[i];
			if (sourceClass == cls && sourceMoveCost == moveCost && sourceOpaque == opaque)
			{
				return (i, elevation);
			}
		}

		return null;
	}
}
