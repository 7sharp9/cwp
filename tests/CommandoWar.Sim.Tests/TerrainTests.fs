module CommandoWar.Sim.Tests.TerrainTests

open Xunit
open CommandoWar.Sim
open CommandoWar.Headless

// Coverage for the authoritative terrain grid (docs/04_SIMULATION_SPEC.md
// sections 7 and 9, docs/06_CONTENT_AND_PRESENTATION.md section 4): the empty
// terrain, each total bounds-checked query on a hand-built terrain, and the
// pin that terrain is excluded from the canonical image so the shared spike
// fixture hashes are unmoved (the ADR-0002 amendment).

let private bounds: GridBounds = { Width = 8; Height = 8 }

/// A hand-built terrain: (2,2) impassable, (3,3) elevated 4 and opaque with
/// entry cost 7, (5,5) covered level 2 from the north only.
let private handBuilt () : Terrain =
    Terrain.build
        bounds
        [| { Cell = { X = 2; Y = 2 }
             Movement = Impassable
             Elevation = 0
             MoveCost = 0
             Opaque = false }
           { Cell = { X = 3; Y = 3 }
             Movement = Passable
             Elevation = 4
             MoveCost = 7
             Opaque = true } |]
        [| { Cell = { X = 5; Y = 5 }; Direction = North; Level = 2 } |]

// --- Direction ---------------------------------------------------------

[<Fact>]
let ``Direction.all is the four cardinals and index is 0..3 in that order`` () =
    Assert.Equal<Direction[]>([| North; East; South; West |], Direction.all)
    Assert.Equal<int[]>([| 0; 1; 2; 3 |], Direction.all |> Array.map Direction.index)

// --- empty terrain ---------------------------------------------------

[<Fact>]
let ``empty terrain is flat, fully passable, transparent, and uncovered`` () =
    let t = Terrain.empty bounds

    for y in 0 .. bounds.Height - 1 do
        for x in 0 .. bounds.Width - 1 do
            let c = { X = x; Y = y }
            Assert.True(Terrain.passable t c)
            Assert.Equal(Terrain.BaseMoveCost, Terrain.moveCost t c)
            Assert.Equal(0, Terrain.elevation t c)
            Assert.False(Terrain.opaque t c)

            for d in Direction.all do
                Assert.Equal(0, Terrain.cover t c d)

// --- queries on a hand-built terrain --------------------------------

[<Fact>]
let ``passable is false on an impassable cell and false out of bounds`` () =
    let t = handBuilt ()
    Assert.False(Terrain.passable t { X = 2; Y = 2 })
    Assert.True(Terrain.passable t { X = 1; Y = 1 })
    Assert.False(Terrain.passable t { X = -1; Y = 0 })
    Assert.False(Terrain.passable t { X = 0; Y = 99 })

[<Fact>]
let ``moveCost is the authored cost, BlockedCost on impassable, BlockedCost out of bounds`` () =
    let t = handBuilt ()
    Assert.Equal(7, Terrain.moveCost t { X = 3; Y = 3 })
    Assert.Equal(Terrain.BaseMoveCost, Terrain.moveCost t { X = 0; Y = 0 })
    Assert.Equal(Terrain.BlockedCost, Terrain.moveCost t { X = 2; Y = 2 })
    Assert.Equal(Terrain.BlockedCost, Terrain.moveCost t { X = 99; Y = 99 })

[<Fact>]
let ``elevation reflects the authored level and is 0 out of bounds`` () =
    let t = handBuilt ()
    Assert.Equal(4, Terrain.elevation t { X = 3; Y = 3 })
    Assert.Equal(0, Terrain.elevation t { X = 0; Y = 0 })
    Assert.Equal(0, Terrain.elevation t { X = -5; Y = -5 })

[<Fact>]
let ``opaque reflects the authored flag and is false out of bounds`` () =
    let t = handBuilt ()
    Assert.True(Terrain.opaque t { X = 3; Y = 3 })
    Assert.False(Terrain.opaque t { X = 4; Y = 4 })
    Assert.False(Terrain.opaque t { X = 8; Y = 0 })

[<Fact>]
let ``cover is directional: level 2 from the north only, 0 elsewhere, 0 out of bounds`` () =
    let t = handBuilt ()
    Assert.Equal(2, Terrain.cover t { X = 5; Y = 5 } North)
    Assert.Equal(0, Terrain.cover t { X = 5; Y = 5 } East)
    Assert.Equal(0, Terrain.cover t { X = 5; Y = 5 } South)
    Assert.Equal(0, Terrain.cover t { X = 5; Y = 5 } West)
    Assert.Equal(0, Terrain.cover t { X = 4; Y = 4 } North)
    Assert.Equal(0, Terrain.cover t { X = 99; Y = 0 } North)

// --- terrain is excluded from the canonical image ------------------

[<Fact>]
let ``adding terrain to a world does not change its canonical encoding or hash`` () =
    let world = Setup.sixAgentWorld Fixture.bounds Fixture.Seed
    let withTerrain = { world with Terrain = handBuilt () }

    Assert.Equal<byte[]>(Canonical.encode world, Canonical.encode withTerrain)
    Assert.Equal(Hashing.hash world, Hashing.hash withTerrain)
    // The pinned shared-fixture initial hash is unmoved either way.
    Assert.Equal(0xE13D7540912C7E25UL, (Hashing.hash withTerrain).Value)

[<Fact>]
let ``a world built with an empty terrain still reaches the pinned fixture hashes`` () =
    match Fixture.run () with
    | Error e -> Assert.Fail($"fixture replay failed: {e}")
    | Ok outcome ->
        Assert.Equal(0xE13D7540912C7E25UL, (Hashing.hash (Fixture.initialState ())).Value)
        Assert.Equal(0xAFA35198CC6BD8D4UL, (Hashing.hash outcome.FinalState).Value)
