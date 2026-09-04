module CommandoWar.Sim.Tests.PathfindingTests

open System
open System.IO
open Xunit
open CommandoWar.Sim
open CommandoWar.Headless

// Coverage for the deterministic grid pathfinding module
// (src/CommandoWar.Sim/Pathfinding.fs, docs/04_SIMULATION_SPEC.md section 8):
// A* over the terrain grid with a total-order frontier key and fixed
// N/E/S/W neighbour order, the entry-cost model, obstacle avoidance,
// cost-aware routing, the no-path / invalid-endpoint / budget-exhausted
// results, the golden-pinned tie-break, determinism, a path-shape property
// test, the pin that producing pathfinding diagnostics does not perturb the
// shared fixture, and the PlannedPath overlay renderer branch against the
// committed golden.

let private bounds: GridBounds = { Width = 8; Height = 8 }

let private baseCell (x: int) (y: int) (mc: MovementClass) (cost: int) : AuthoredCell =
    { Cell = { X = x; Y = y }
      Movement = mc
      Elevation = 0
      MoveCost = cost
      Opaque = false }

let private impassableAt (cells: (int * int) list) : Terrain =
    Terrain.build
        bounds
        (cells |> List.map (fun (x, y) -> baseCell x y Impassable 0) |> List.toArray)
        [||]

let private costAt (cells: (int * int * int) list) : Terrain =
    Terrain.build
        bounds
        (cells |> List.map (fun (x, y, c) -> baseCell x y Passable c) |> List.toArray)
        [||]

/// The step cost of entering each cell of `cells` after the first, recomputed
/// straight from `Terrain.moveCost`.
let private recomputedCost (t: Terrain) (cells: Cell[]) : int =
    cells |> Array.skip 1 |> Array.sumBy (Terrain.moveCost t)

let private cardinallyAdjacent (a: Cell) (b: Cell) : bool =
    abs (a.X - b.X) + abs (a.Y - b.Y) = 1

// --- straight and L-shaped paths on empty terrain --------------------

[<Fact>]
let ``a straight cardinal path on empty terrain: endpoints and step-count cost`` () =
    let t = Terrain.empty bounds

    match Pathfinding.find t { X = 0; Y = 3 } { X = 5; Y = 3 } with
    | Found(cells, cost) ->
        Assert.Equal({ X = 0; Y = 3 }, cells.[0])
        Assert.Equal({ X = 5; Y = 3 }, cells.[cells.Length - 1])
        Assert.Equal(5, cells.Length - 1)
        Assert.Equal((cells.Length - 1) * Terrain.BaseMoveCost, cost)
        Assert.Equal(5, cost)
    | other -> Assert.Fail($"expected Found, got {other}")

[<Fact>]
let ``an L-shaped path on empty terrain: endpoints, adjacency, and step-count cost`` () =
    let t = Terrain.empty bounds

    match Pathfinding.find t { X = 1; Y = 1 } { X = 4; Y = 3 } with
    | Found(cells, cost) ->
        Assert.Equal({ X = 1; Y = 1 }, cells.[0])
        Assert.Equal({ X = 4; Y = 3 }, cells.[cells.Length - 1])

        for (a, b) in Array.pairwise cells do
            Assert.True(cardinallyAdjacent a b, $"{a} and {b} are not cardinally adjacent")
        // Manhattan distance 5, every step BaseMoveCost.
        Assert.Equal(5, cells.Length - 1)
        Assert.Equal(5 * Terrain.BaseMoveCost, cost)
    | other -> Assert.Fail($"expected Found, got {other}")

[<Fact>]
let ``start equals goal yields Found with a single cell and zero cost`` () =
    let t = Terrain.empty bounds
    Assert.Equal(Found([| { X = 2; Y = 2 } |], 0), Pathfinding.find t { X = 2; Y = 2 } { X = 2; Y = 2 })

// --- obstacle avoidance --------------------------------------------

[<Fact>]
let ``an impassable wall forces a detour of adjacent passable cells`` () =
    // A vertical wall x = 3, rows 0..5; row 6..7 open. Straight (1,2)->(5,2)
    // is blocked, so the path must dip to row 6 and back.
    let t = impassableAt [ for y in 0..5 -> 3, y ]
    let start = { X = 1; Y = 2 }
    let goal = { X = 5; Y = 2 }

    match Pathfinding.find t start goal with
    | Found(cells, cost) ->
        Assert.Equal(start, cells.[0])
        Assert.Equal(goal, cells.[cells.Length - 1])

        for (a, b) in Array.pairwise cells do
            Assert.True(cardinallyAdjacent a b, $"{a} and {b} are not cardinally adjacent")

        for c in cells do
            Assert.True(Terrain.passable t c, $"path crosses the impassable cell {c}")

        // Manhattan 4, wall forces +8 (down 4 rows, back up 4).
        Assert.Equal(12, cost)
        Assert.Equal(cost, recomputedCost t cells)
    | other -> Assert.Fail($"expected Found, got {other}")

// --- cost-aware routing (golden-pinned) ---------------------------

[<Fact>]
let ``a cheap detour beats a straight line through a movement-cost patch`` () =
    // Row y = 3 cells (2,3),(3,3),(4,3) cost 10. Straight (1,3)->(5,3) costs
    // 10+10+10+1 = 31; the one-row detour over row 2 costs 6.
    let t = costAt [ 2, 3, 10; 3, 3, 10; 4, 3, 10 ]

    match Pathfinding.find t { X = 1; Y = 3 } { X = 5; Y = 3 } with
    | Found(cells, cost) ->
        Assert.Equal<Cell[]>(
            [| { X = 1; Y = 3 }
               { X = 1; Y = 2 }
               { X = 2; Y = 2 }
               { X = 3; Y = 2 }
               { X = 4; Y = 2 }
               { X = 5; Y = 2 }
               { X = 5; Y = 3 } |],
            cells
        )

        Assert.Equal(6, cost)
        Assert.Equal(cost, recomputedCost t cells)

        Assert.DoesNotContain({ X = 2; Y = 3 }, cells)
        Assert.DoesNotContain({ X = 3; Y = 3 }, cells)
        Assert.DoesNotContain({ X = 4; Y = 3 }, cells)
    | other -> Assert.Fail($"expected Found, got {other}")

// --- no path -----------------------------------------------------

[<Fact>]
let ``a fully walled-off goal yields NoPath`` () =
    // (4,4) passable but its four neighbours impassable.
    let t = impassableAt [ 4, 3; 4, 5; 3, 4; 5, 4 ]
    Assert.Equal(NoPath, Pathfinding.find t { X = 0; Y = 0 } { X = 4; Y = 4 })

// --- invalid endpoints -----------------------------------------

[<Fact>]
let ``an out-of-bounds or impassable endpoint yields InvalidEndpoint without throwing`` () =
    let t = impassableAt [ 4, 4 ]

    Assert.Equal(InvalidEndpoint { X = -1; Y = 0 }, Pathfinding.find t { X = -1; Y = 0 } { X = 3; Y = 3 })
    Assert.Equal(InvalidEndpoint { X = 99; Y = 99 }, Pathfinding.find t { X = 0; Y = 0 } { X = 99; Y = 99 })
    Assert.Equal(InvalidEndpoint { X = 4; Y = 4 }, Pathfinding.find t { X = 0; Y = 0 } { X = 4; Y = 4 })
    Assert.Equal(InvalidEndpoint { X = 4; Y = 4 }, Pathfinding.find t { X = 4; Y = 4 } { X = 0; Y = 0 })
    // Start reported first when both endpoints are invalid.
    Assert.Equal(InvalidEndpoint { X = 4; Y = 4 }, Pathfinding.find t { X = 4; Y = 4 } { X = 20; Y = 20 })

// --- determinism -----------------------------------------------

[<Fact>]
let ``two find calls with the same inputs are structurally equal`` () =
    let t = costAt [ 3, 3, 4; 4, 4, 6; 2, 5, 3 ]
    let a = Pathfinding.find t { X = 0; Y = 0 } { X = 7; Y = 7 }
    let b = Pathfinding.find t { X = 0; Y = 0 } { X = 7; Y = 7 }
    Assert.True((a = b))

// --- tie-break stability (golden-pinned) -----------------------

[<Fact>]
let ``a symmetric map with two equal-cost routes returns the documented one`` () =
    // Empty grid, (0,0)->(2,2): six equal-cost monotone L-routes. The
    // documented tie-break (lower row-major frontier index first, N/E/S/W
    // neighbour order) yields "east along row 0, then south down column 2".
    let t = Terrain.empty bounds

    Assert.Equal(
        Found(
            [| { X = 0; Y = 0 }
               { X = 1; Y = 0 }
               { X = 2; Y = 0 }
               { X = 2; Y = 1 }
               { X = 2; Y = 2 } |],
            4
        ),
        Pathfinding.find t { X = 0; Y = 0 } { X = 2; Y = 2 }
    )

// --- budget --------------------------------------------------

[<Fact>]
let ``a tiny maxExpansions on a large open map returns BudgetExhausted`` () =
    let t = Terrain.empty { Width = 20; Height = 20 }

    match Pathfinding.findWithin t { X = 0; Y = 0 } { X = 19; Y = 19 } 5 with
    | BudgetExhausted n -> Assert.Equal(5, n)
    | other -> Assert.Fail($"expected BudgetExhausted, got {other}")

    // The default cap (Width * Height) is enough to reach the goal.
    match Pathfinding.find t { X = 0; Y = 0 } { X = 19; Y = 19 } with
    | Found(_, cost) -> Assert.Equal(38, cost)
    | other -> Assert.Fail($"expected Found, got {other}")

// --- path-shape property over a hand-built terrain ------------

/// A hand-built 8x8 terrain: an impassable L, a cost patch, an open field.
let private propertyTerrain () : Terrain =
    Terrain.build
        bounds
        [| baseCell 2 1 Impassable 0
           baseCell 2 2 Impassable 0
           baseCell 2 3 Impassable 0
           baseCell 3 3 Impassable 0
           baseCell 4 3 Impassable 0
           baseCell 5 5 Passable 6
           baseCell 5 6 Passable 6
           baseCell 6 5 Passable 6 |]
        [||]

[<Fact>]
let ``whenever find returns Found the cells are an adjacent passable chain whose recomputed cost matches`` () =
    let t = propertyTerrain ()

    let coords =
        [ for x in 0..7 do
              for y in 0..7 -> { X = x; Y = y } ]

    for start in coords do
        for goal in coords do
            match Pathfinding.find t start goal with
            | Found(cells, cost) ->
                Assert.Equal(start, cells.[0])
                Assert.Equal(goal, cells.[cells.Length - 1])

                for (a, b) in Array.pairwise cells do
                    Assert.True(cardinallyAdjacent a b, $"{start}->{goal}: {a} and {b} not adjacent")

                for c in cells do
                    Assert.True(Terrain.passable t c, $"{start}->{goal}: path crosses impassable {c}")

                Assert.Equal(cost, recomputedCost t cells)
            | NoPath
            | BudgetExhausted _
            | InvalidEndpoint _ -> ()

// --- the pin: pathfinding diagnostics do not perturb the shared fixture ---

[<Fact>]
let ``producing pathfinding diagnostics for the shared fixture leaves its hashes and event count unchanged`` () =
    let w = Fixture.initialState ()
    // A pure query over the fixture's (empty) terrain: no mutation, no draw.
    let _ = Pathfinding.find w.Terrain { X = 0; Y = 0 } { X = 20; Y = 14 }
    Assert.Equal(0xF2F3DF0D820AD9ACUL, (Hashing.hash w).Value)
    Assert.Equal(1, Canonical.FormatVersion)

    match Fixture.run () with
    | Error e -> Assert.Fail($"fixture replay failed: {e}")
    | Ok outcome ->
        Assert.Equal(0x838D3AE7DBFB735DUL, (Hashing.hash outcome.FinalState).Value)
        Assert.Equal(33, outcome.Events.Length)

// --- the PlannedPath overlay renderer branch (golden-pinned) ------

let private goldenDir = Path.Combine(AppContext.BaseDirectory, "diagnostics")
let private golden (name: string) = File.ReadAllText(Path.Combine(goldenDir, name))

let private pathFrameWithRoutes () : DiagnosticFrame =
    let w = PathDemo.initialState ()
    let f = Diagnostics.frame w

    let overlays =
        PathDemo.routes
        |> Array.map (fun (a, b) ->
            match Pathfinding.find w.Terrain a b with
            | Found(cells, cost) -> PlannedPath(a, b, cells, cost, true)
            | NoPath
            | BudgetExhausted _
            | InvalidEndpoint _ -> PlannedPath(a, b, [||], 0, false))

    { f with Overlays = overlays }

[<Fact>]
let ``the pathfinding demo ASCII render with planned paths is byte-equal to the committed golden`` () =
    Assert.Equal(golden "path.ascii.txt", DiagnosticRender.Ascii(pathFrameWithRoutes ()))

[<Fact>]
let ``the pathfinding demo SVG render with planned paths is byte-equal to the committed golden`` () =
    Assert.Equal(golden "path.svg", DiagnosticRender.Svg(pathFrameWithRoutes ()))

[<Fact>]
let ``a PlannedPath overlay appears distinctly and the overlay-absent render is unchanged`` () =
    let w = PathDemo.initialState ()
    let plain = Diagnostics.frame w
    let asciiPlain = DiagnosticRender.Ascii plain
    let asciiPaths = DiagnosticRender.Ascii(pathFrameWithRoutes ())

    Assert.True(asciiPlain <> asciiPaths)
    // The straight route on row 1 is S, then '+', then G.
    Assert.Contains("  1 .S++G", asciiPaths)
    Assert.Contains("path (1,1) -> (4,1): reached, cost 3", asciiPaths)
    Assert.Contains("path (2,6) -> (9,6): reached, cost 11", asciiPaths)
    Assert.Contains("path (1,10) -> (14,9): no path", asciiPaths)
    // Overlay-absent render carries neither the path glyphs nor the section.
    Assert.DoesNotContain("overlays:", asciiPlain)
    Assert.DoesNotContain("path (", asciiPlain)

    // The SVG branch emits a solid path polyline and start / goal markers.
    let svgPaths = DiagnosticRender.Svg(pathFrameWithRoutes ())
    Assert.Contains("<polyline points=", svgPaths)
    Assert.Contains("fill=\"#2f855a\"", svgPaths)
    Assert.DoesNotContain("<polyline", DiagnosticRender.Svg plain)

[<Fact>]
let ``a frame carrying both a SightRay and a PlannedPath overlay renders both`` () =
    let w = PathDemo.initialState ()
    let f = Diagnostics.frame w

    let ray = Sight.trace w.Terrain { X = 1; Y = 1 } { X = 4; Y = 1 }

    let path =
        match Pathfinding.find w.Terrain { X = 1; Y = 1 } { X = 4; Y = 1 } with
        | Found(cells, cost) -> PlannedPath({ X = 1; Y = 1 }, { X = 4; Y = 1 }, cells, cost, true)
        | other -> failwith $"unexpected {other}"

    let both =
        { f with
            Overlays = [| SightRay({ X = 1; Y = 1 }, { X = 4; Y = 1 }, ray.Path, ray.Blocker); path |] }

    let ascii = DiagnosticRender.Ascii both
    Assert.Contains("sight (1,1) -> (4,1): clear", ascii)
    Assert.Contains("path (1,1) -> (4,1): reached, cost 3", ascii)

    let svg = DiagnosticRender.Svg both
    Assert.Contains("stroke-dasharray=\"4,3\"", svg) // the sight ray
    Assert.Contains("<polyline points=", svg) // the planned path
