namespace CwClientCore

open CommandoWar.Sim

/// TASK-060 (backlog B-024) Godot terrain-authoring export. Converts a
/// painted `TileMapLayer`'s cells into a `RawScenario` and writes it as a
/// `.cwscenario` file via `ScenarioFile.serialise` -- the framework-neutral
/// parser/serialiser, called directly rather than duplicated on the Godot
/// side. Inputs are plain primitive arrays (the ADR-0004 boundary contract
/// this project already follows: no `Godot.*` type crosses into
/// `CwClientCore` — the C# caller unpacks `TileMapLayer.GetUsedCells`/
/// `GetCellTileData` into these arrays first).
///
/// Terrain-layer authoring only (TASK-060 scope): every other
/// `RawScenario` field is a small fixed skeleton (one friendly agent, one
/// unit type, one "reach" objective, one extraction area) just sufficient
/// for `Scenario.validate` to accept the result — deployments, objectives,
/// unit types, formations, and jammers have no painting UI yet.
[<RequireQualifiedAccess>]
module TerrainAuthoring =

    /// Builds and serialises a scenario around a painted terrain layer,
    /// writing it to `outPath`. `friendlyStart`/`objectiveArea`/`exfil` are
    /// each an `(x, y)` cell; the caller is responsible for these being
    /// passable (`Scenario.validate` reports `DeploymentOnImpassableCell`
    /// otherwise, exactly as for any other authored scenario).
    /// `cellX`/`cellY`/`cellClass`/`cellElevation`/`cellMoveCost`/
    /// `cellOpaque` are parallel arrays, one entry per painted cell.
    let exportScenario
        (width: int)
        (height: int)
        (id: string)
        (friendlyStart: struct (int * int))
        (objectiveArea: struct (int * int))
        (exfil: struct (int * int))
        (cellX: int[])
        (cellY: int[])
        (cellClass: string[])
        (cellElevation: int[])
        (cellMoveCost: int[])
        (cellOpaque: bool[])
        (outPath: string)
        : unit =
        let struct (fx, fy) = friendlyStart
        let struct (ox, oy) = objectiveArea
        let struct (ex, ey) = exfil

        let terrainCells =
            Array.init cellX.Length (fun i ->
                ({ Cell = { X = cellX.[i]; Y = cellY.[i] }
                   Class = cellClass.[i]
                   Elevation = cellElevation.[i]
                   MoveCost = cellMoveCost.[i]
                   Opaque = cellOpaque.[i] }: RawTerrainCell))

        let raw: RawScenario =
            { ContentVersion = ScenarioContent.Version
              Id = id
              Width = width
              Height = height
              FriendlyDeployments =
                [| { AgentId = 0
                     Cell = { X = fx; Y = fy }
                     CommunicationAvailable = true
                     Discipline = 3
                     UnitType = "standard"
                     FormationId = ""
                     SlotIndex = 0 } |]
              EnemyDeployments = [||]
              ObjectiveAreas = [| { AreaId = "objective"; Cell = { X = ox; Y = oy } } |]
              ExtractionAreas = [| { AreaId = "exfil"; Cell = { X = ex; Y = ey } } |]
              ResupplyAreas = [||]
              StaticTargets = [||]
              Objectives =
                [| { Id = 1
                     Kind = "reach"
                     AreaRef = "objective"
                     TargetRef = ""
                     HoldTicks = 0
                     ExtractAgentIds = [||]
                     IsOptional = false } |]
              TerrainLayer =
                Some
                    { Width = width
                      Height = height
                      Cells = terrainCells
                      Cover = [||] }
              UnitTypes = [| { Id = "standard"; MoveSpeed = Agent.MoveSpeedDefault } |]
              Headquarters = None
              Jammers = [||]
              Formations = [||]
              FailOnFriendlyForceEliminated = true }

        System.IO.File.WriteAllText(outPath, ScenarioFile.serialise raw)

    /// One authored terrain layer, flattened for the C# side (TASK-061,
    /// backlog B-025) -- the `AppraisalDemo.FrameView` "no F# tuple crosses
    /// to C#" precedent applied to `RawTerrainCell`. Parallel arrays, one
    /// entry per authored cell.
    [<CLIMutable>]
    type ImportedTerrainLayer =
        { CellX: int[]
          CellY: int[]
          Class: string[]
          Elevation: int[]
          MoveCost: int[]
          Opaque: bool[] }

    /// Reverse of `exportScenario` (TASK-061, backlog B-025): reads a
    /// `.cwscenario` file at `path` and returns its authored terrain layer's
    /// cells as parallel primitive arrays for the C# side
    /// (`ImportTerrainScript.cs`) to paint onto a `TileMapLayer`, one
    /// `TileMapLayer.SetCell` per cell once it resolves each cell's
    /// `(Class, MoveCost, Opaque)` against `TerrainTileSet.Sources`.
    ///
    /// `ScenarioFile.parse` and `Scenario.validate` do the actual parsing
    /// and validation -- this is a thin adapter, not a new parser: a
    /// grammar or content fault throws (the `DemoScenario.scenario`/
    /// `.initialState` "fails hard, this is a bug in this file" precedent --
    /// an editor tool run against bad content is exactly that, not a case to
    /// recover from silently).
    ///
    /// Returns the *authored, sparse* cells (`RawTerrainLayer.Cells`), not
    /// `Scenario.validate`'s validated dense `Terrain` grid: only a cell the
    /// author actually typed should become a painted `TileMapLayer` cell (an
    /// unpainted cell has no tile at all -- the same sparse shape
    /// `ExportTerrainScript.GetUsedCells()` reads back on export, so
    /// import -> export round-trips exactly), and `RawTerrainCell.Class` is
    /// the raw `"passable"`/`"impassable"` string token `TerrainTileSet.
    /// Sources` is keyed on, not `Scenario.fs`'s validated `MovementClass`
    /// DU. An absent terrain layer (`RawScenario.TerrainLayer = None`)
    /// returns empty arrays -- nothing to paint, not a fault.
    let importTerrainLayer (path: string) : ImportedTerrainLayer =
        let raw =
            match ScenarioFile.parse (System.IO.File.ReadAllText path) with
            | Error e -> failwith $"'{path}': .cwscenario parse error: {ScenarioFile.describeError e}"
            | Ok r -> r

        match Scenario.validate raw with
        | Error errs -> failwith $"'{path}': scenario content invalid ({errs.Length} fault(s)): {errs}"
        | Ok _ -> ()

        match raw.TerrainLayer with
        | None ->
            { CellX = [||]
              CellY = [||]
              Class = [||]
              Elevation = [||]
              MoveCost = [||]
              Opaque = [||] }
        | Some layer ->
            { CellX = layer.Cells |> Array.map (fun c -> c.Cell.X)
              CellY = layer.Cells |> Array.map (fun c -> c.Cell.Y)
              Class = layer.Cells |> Array.map (fun c -> c.Class)
              Elevation = layer.Cells |> Array.map (fun c -> c.Elevation)
              MoveCost = layer.Cells |> Array.map (fun c -> c.MoveCost)
              Opaque = layer.Cells |> Array.map (fun c -> c.Opaque) }
