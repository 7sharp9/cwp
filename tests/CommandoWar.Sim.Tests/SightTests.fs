module CommandoWar.Sim.Tests.SightTests

open System
open System.IO
open Xunit
open CommandoWar.Sim
open CommandoWar.Headless

// Coverage for the deterministic line-of-sight module
// (src/CommandoWar.Sim/Sight.fs, docs/04_SIMULATION_SPEC.md section 9): the
// integer supercover walk, the opacity and elevation blocking rules, the
// diagonal-corner rule, the required symmetry property, totality on
// out-of-bounds endpoints, determinism, the pin that producing LOS
// diagnostics does not perturb the shared fixture, and the SightRay overlay
// renderer branch against the committed golden.

let private bounds: GridBounds = { Width = 8; Height = 8 }

let private opaqueAt (cells: (int * int) list) : Terrain =
    Terrain.build
        bounds
        (cells
         |> List.map (fun (x, y) ->
             { Cell = { X = x; Y = y }
               Movement = Passable
               Elevation = 0
               MoveCost = Terrain.BaseMoveCost
               Opaque = true })
         |> List.toArray)
        [||]

let private elevatedAt (cells: (int * int * int) list) : Terrain =
    Terrain.build
        bounds
        (cells
         |> List.map (fun (x, y, e) ->
             { Cell = { X = x; Y = y }
               Movement = Passable
               Elevation = e
               MoveCost = Terrain.BaseMoveCost
               Opaque = false })
         |> List.toArray)
        [||]

// --- clear sight and the traced path ----------------------------------

[<Fact>]
let ``clear sight over horizontal, vertical, and diagonal lines on empty terrain`` () =
    let t = Terrain.empty bounds
    Assert.True(Sight.visible t { X = 0; Y = 3 } { X = 7; Y = 3 })
    Assert.True(Sight.visible t { X = 2; Y = 0 } { X = 2; Y = 7 })
    Assert.True(Sight.visible t { X = 0; Y = 0 } { X = 7; Y = 7 })

[<Fact>]
let ``the traced path starts at the origin and ends at the target`` () =
    let t = Terrain.empty bounds
    let r = Sight.trace t { X = 1; Y = 2 } { X = 6; Y = 5 }
    Assert.True(r.Visible)
    Assert.Equal({ X = 1; Y = 2 }, r.Path.[0])
    Assert.Equal({ X = 6; Y = 5 }, r.Path.[r.Path.Length - 1])
    Assert.Equal(None, r.Blocker)

// --- opacity blocking ------------------------------------------------

[<Fact>]
let ``an opaque cell between the endpoints blocks sight and is the reported blocker`` () =
    let t = opaqueAt [ 4, 2 ]
    let r = Sight.trace t { X = 1; Y = 2 } { X = 7; Y = 2 }
    Assert.False(r.Visible)
    Assert.Equal(Some { X = 4; Y = 2 }, r.Blocker)

// --- the diagonal-corner rule (golden-pinned) -----------------------

[<Fact>]
let ``two opaque walls forming a diagonal corner block sight; a single wall does not`` () =
    // The ray (2,2)->(5,5) walks the pure diagonal (2,2),(3,3),(4,4),(5,5).
    // The step (3,3)->(4,4) has shared-edge neighbours (4,3) and (3,4).
    let twoWall = opaqueAt [ 4, 3; 3, 4 ]
    let r = Sight.trace twoWall { X = 2; Y = 2 } { X = 5; Y = 5 }
    Assert.False(r.Visible)
    Assert.Equal<Cell[]>(
        [| { X = 2; Y = 2 }; { X = 3; Y = 3 }; { X = 4; Y = 4 }; { X = 5; Y = 5 } |],
        r.Path
    )
    Assert.Equal(Some { X = 4; Y = 3 }, r.Blocker)

    // Only one of the two neighbours opaque: sight passes the corner.
    Assert.True((Sight.trace (opaqueAt [ 4, 3 ]) { X = 2; Y = 2 } { X = 5; Y = 5 }).Visible)
    Assert.True((Sight.trace (opaqueAt [ 3, 4 ]) { X = 2; Y = 2 } { X = 5; Y = 5 }).Visible)

// --- the elevation rule --------------------------------------------

[<Fact>]
let ``a ridge cell higher than both endpoints blocks; it does not when an endpoint is at or above it`` () =
    // (4,4) at elevation 3, endpoints at elevation 0 -> 3 > 0 and 3 > 0 -> blocked.
    let blocked = Sight.trace (elevatedAt [ 4, 4, 3 ]) { X = 2; Y = 4 } { X = 6; Y = 4 }
    Assert.False(blocked.Visible)
    Assert.Equal(Some { X = 4; Y = 4 }, blocked.Blocker)

    // One endpoint standing on an equally high cell -> 3 > 3 is false -> not blocked.
    let level = elevatedAt [ 4, 4, 3; 2, 4, 3 ]
    Assert.True((Sight.trace level { X = 2; Y = 4 } { X = 6; Y = 4 }).Visible)

    // "Strictly greater than BOTH endpoints": a cell as high as the higher
    // endpoint does not occlude, even though it towers over the other.
    let tallTarget = elevatedAt [ 4, 4, 5; 6, 4, 5 ]
    Assert.True((Sight.trace tallTarget { X = 2; Y = 4 } { X = 6; Y = 4 }).Visible)

// --- symmetry (a required property) --------------------------------

/// A hand-built 8x8 terrain mixing opaque walls, a solid corner, and a ridge.
let private symmetryTerrain () : Terrain =
    Terrain.build
        bounds
        [| { Cell = { X = 3; Y = 3 }; Movement = Passable; Elevation = 0; MoveCost = 1; Opaque = true }
           { Cell = { X = 4; Y = 3 }; Movement = Passable; Elevation = 0; MoveCost = 1; Opaque = true }
           { Cell = { X = 3; Y = 4 }; Movement = Passable; Elevation = 0; MoveCost = 1; Opaque = true }
           { Cell = { X = 5; Y = 5 }; Movement = Passable; Elevation = 4; MoveCost = 1; Opaque = false }
           { Cell = { X = 1; Y = 6 }; Movement = Passable; Elevation = 2; MoveCost = 1; Opaque = false } |]
        [||]

[<Fact>]
let ``visible is symmetric over a hand-built terrain and a grid of endpoint pairs`` () =
    let t = symmetryTerrain ()

    let coords =
        [ for x in -1..8 do
              for y in -1..8 -> { X = x; Y = y } ]

    for a in coords do
        for b in coords do
            Assert.Equal(Sight.visible t a b, Sight.visible t b a)

// --- totality on out-of-bounds endpoints --------------------------

[<Fact>]
let ``an out-of-bounds endpoint sees nothing and does not throw`` () =
    let t = Terrain.empty bounds
    let r1 = Sight.trace t { X = -1; Y = 0 } { X = 3; Y = 3 }
    Assert.False(r1.Visible)
    Assert.Equal(None, r1.Blocker)

    let r2 = Sight.trace t { X = 0; Y = 0 } { X = 99; Y = 99 }
    Assert.False(r2.Visible)

    Assert.False(Sight.visible t { X = 0; Y = 0 } { X = -5; Y = 2 })

// --- determinism -------------------------------------------------

[<Fact>]
let ``two traces of the same inputs are structurally equal`` () =
    let t = symmetryTerrain ()
    let a = Sight.trace t { X = 0; Y = 1 } { X = 7; Y = 6 }
    let b = Sight.trace t { X = 0; Y = 1 } { X = 7; Y = 6 }
    Assert.True((a = b))

// --- the pin: LOS diagnostics do not perturb the shared fixture ----

[<Fact>]
let ``producing LOS diagnostics for the shared fixture leaves its hashes and event count unchanged`` () =
    let w = Fixture.initialState ()
    // A pure query over the fixture's (empty) terrain: no mutation, no draw.
    let _ = Sight.trace w.Terrain { X = 0; Y = 0 } { X = 20; Y = 14 }
    Assert.Equal(0xE13D7540912C7E25UL, (Hashing.hash w).Value)
    Assert.Equal(2, Canonical.FormatVersion)

    match Fixture.run () with
    | Error e -> Assert.Fail($"fixture replay failed: {e}")
    | Ok outcome ->
        Assert.Equal(0xAFA35198CC6BD8D4UL, (Hashing.hash outcome.FinalState).Value)
        Assert.Equal(33, outcome.Events.Length)

// --- the SightRay overlay renderer branch (golden-pinned) ----------

let private goldenDir = Path.Combine(AppContext.BaseDirectory, "diagnostics")
let private golden (name: string) = File.ReadAllText(Path.Combine(goldenDir, name))

let private losFrameWithRays () : DiagnosticFrame =
    let w = LosDemo.initialState ()
    let f = Diagnostics.frame w

    let overlays =
        LosDemo.rays
        |> Array.map (fun (a, b) ->
            let r = Sight.trace w.Terrain a b
            SightRay(a, b, r.Path, r.Blocker))

    { f with Overlays = overlays }

[<Fact>]
let ``the LOS demo ASCII render with sight rays is byte-equal to the committed golden`` () =
    Assert.Equal(golden "los.ascii.txt", DiagnosticRender.Ascii(losFrameWithRays ()))

[<Fact>]
let ``the LOS demo SVG render with sight rays is byte-equal to the committed golden`` () =
    Assert.Equal(golden "los.svg", DiagnosticRender.Svg(losFrameWithRays ()))

[<Fact>]
let ``a SightRay overlay appears distinctly and the overlay-absent render is unchanged`` () =
    let w = LosDemo.initialState ()
    let plain = Diagnostics.frame w
    let asciiPlain = DiagnosticRender.Ascii plain
    let asciiRays = DiagnosticRender.Ascii(losFrameWithRays ())

    Assert.True(asciiPlain <> asciiRays)
    // The clear ray on row 1 is a run of '*'; the opaque-wall ray on row 4 is
    // broken by the blocker glyph 'x'.
    Assert.Contains("  1 .**********.", asciiRays)
    Assert.Contains("  4 .****x*****.", asciiRays)
    Assert.Contains("sight (1,1) -> (10,1): clear", asciiRays)
    Assert.Contains("sight (6,5) -> (11,10): blocked at (9,7)", asciiRays)
    // Overlay-absent render carries neither the ray glyphs nor the section.
    Assert.DoesNotContain("overlays:", asciiPlain)
    Assert.Equal("  1 ............", (asciiPlain.Split('\n') |> Array.find (fun l -> l.StartsWith "  1 ")))

    // The SVG branch emits a dashed ray line and a red blocker marker.
    let svgRays = DiagnosticRender.Svg(losFrameWithRays ())
    Assert.Contains("stroke-dasharray=\"4,3\"", svgRays)
    Assert.Contains("stroke=\"#c53030\"", svgRays)
    Assert.DoesNotContain("stroke-dasharray", DiagnosticRender.Svg plain)
