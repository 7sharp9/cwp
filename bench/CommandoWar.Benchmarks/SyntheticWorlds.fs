namespace CommandoWar.Benchmarks

open CommandoWar.Sim

/// Fixed, deterministic test vectors for the benchmark harness (TASK-014,
/// backlog B-013). Nothing here is authoritative game content: these are
/// synthetic worlds and terrains built only to give the `docs/09` section 2.8
/// benchmarks a stable, well-understood subject. They live in the benchmark
/// project, never in `CommandoWar.Sim` (ADR-0002: the simulation gains no
/// benchmark-only scaffolding).
///
/// Every builder is a pure function of integer constants: a fixed grid, a
/// fixed seed, fixed agent placement, fixed terrain. `BenchmarkDotNet` only
/// measures the operations that run over these values; it introduces no
/// randomness, no wall-clock read, and no `System.Random` into authoritative
/// state.
[<RequireQualifiedAccess>]
module SyntheticWorlds =

    /// Seed for every synthetic world's SplitMix64 stream. Fixed and
    /// meaningless beyond being a stable constant; no benchmark draws from the
    /// stream (no gameplay phase does yet).
    [<Literal>]
    let Seed = 20260904UL

    /// The stress-scene grid: 32 x 32, matching the shared fixture and the
    /// ADR-0001 "common result" map size.
    let bounds: GridBounds = { Width = 32; Height = 32 }

    /// Agents in the synthetic movement world. `docs/07` section 10 asks for
    /// 50 friendly and enemy agents in a synthetic stress scene even though
    /// the slice uses fewer.
    [<Literal>]
    let AgentCount = 50

    let private unwrap (r: Result<WorldState, WorldError>) : WorldState =
        match r with
        | Ok w -> w
        | Error e -> failwith $"synthetic world build failed: {e}"

    /// An empty world: the 32 x 32 grid, no agents, flat passable terrain.
    /// The subject of the "empty fixed tick" benchmark - the per-tick cost of
    /// the phase loop, event/snapshot construction, and the canonical hash
    /// with no movement or command work.
    let emptyWorld () : WorldState = unwrap (World.create bounds Seed [])

    /// The six friendly agents of `Setup.sixAgentWorld`, at rest. Movement
    /// benchmarks issue fresh `MoveTo` commands each operation so the
    /// Command-intake and Navigation-and-movement phases keep doing work
    /// (placeholder movement, pending B-011).
    let sixAgentWorld () : WorldState = Setup.sixAgentWorld bounds Seed

    /// `AgentCount` agents on distinct cells in the top-left block of the
    /// grid, sides alternating, at rest. Built through `World.create` (the
    /// same construction core as every other world); no scenario file, no
    /// authored content.
    let manyAgentWorld () : WorldState =
        let agents =
            [ for i in 0 .. AgentCount - 1 ->
                  let cell = { X = i % bounds.Width; Y = i / bounds.Width }
                  let side = if i % 2 = 0 then Friendly else Hostile
                  Agent.create (AgentId.ofInt i) side cell ]

        unwrap (World.create bounds Seed agents)

    /// A fresh batch of long `MoveTo` commands, one per agent in `world`, each
    /// aimed at the mirrored cell on the far side of the grid so placeholder
    /// movement always has a step to take. Rebuilt each benchmark operation:
    /// this is the "stepped with refreshed move orders" shape.
    let moveOrders (world: WorldState) : PlayerCommand[] =
        world.Agents
        |> Array.mapi (fun i a ->
            let target =
                { X = bounds.Width - 1 - a.Position.X
                  Y = bounds.Height - 1 - a.Position.Y }

            Command.moveTo (CommandId.ofInt i) 0L a.Id target)

    // --- synthetic terrains for the pathfinding benchmarks ---------------
    // `Terrain.build` starts from `Terrain.empty` and overwrites only the
    // authored cells, so each builder authors just its walls.

    let private wall (x: int) (y: int) : AuthoredCell =
        { Cell = { X = x; Y = y }
          Movement = Impassable
          Elevation = 0
          MoveCost = 0
          Opaque = true }

    /// The pathfinding stress grid: 40 x 40. Large enough that the closed set
    /// on the blocked and choke maps is a meaningful fraction of the grid,
    /// small enough to stay well inside a "straightforward grid algorithm"
    /// (docs/04 section 19).
    let pathBounds: GridBounds = { Width = 40; Height = 40 }

    /// Start and goal for every pathfinding benchmark: opposite mid-height
    /// edges, so a clear route is a straight 39-step line.
    let pathStart: Cell = { X = 0; Y = 20 }
    let pathGoal: Cell = { X = 39; Y = 20 }

    /// Open terrain: no walls. `find pathStart pathGoal` is the cheapest case
    /// (the heuristic pulls straight to the goal).
    let openTerrain () : Terrain = Terrain.empty pathBounds

    /// Blocked terrain: four full-height vertical walls at x = 8, 16, 24, 32,
    /// each with a four-cell gap that alternates between the top and the
    /// bottom edge, forcing a serpentine detour.
    let blockedTerrain () : Terrain =
        let cells =
            [| for wx in [ 8; 16; 24; 32 ] do
                   let gapAtTop = (wx / 8) % 2 = 1
                   for y in 0 .. pathBounds.Height - 1 do
                       let inGap =
                           if gapAtTop then y < 4 else y >= pathBounds.Height - 4

                       if not inGap then
                           wall wx y |]

        Terrain.build pathBounds cells [||]

    /// The single-cell gap in the choke wall: the bottom-edge row, as far as
    /// possible from the direct start-to-goal line so A* cannot stumble
    /// through it.
    let chokeGapRow: int = pathBounds.Height - 1

    /// Choke terrain: one full-height wall at x = 20 with a single one-cell
    /// gap at `chokeGapRow`. The straight line from start to goal is blocked
    /// and the gap is 19 rows away, so A* fans out around the whole wall
    /// before finding it - the closed set is the largest fraction of the grid
    /// of the three maps, the case that stresses the B-011 expansion budget
    /// hardest.
    let chokeTerrain () : Terrain =
        let cells =
            [| for y in 0 .. pathBounds.Height - 1 do
                   if y <> chokeGapRow then
                       wall 20 y |]

        Terrain.build pathBounds cells [||]

    /// The smallest `maxExpansions` for which `findWithin terrain start goal`
    /// returns `Found` rather than `BudgetExhausted`: the size of the closed
    /// set A* actually reaches on this query. `findWithin` returns
    /// `BudgetExhausted k` while the goal needs more than `k` non-goal
    /// expansions and `Found` once `k` is enough, so an exponential probe then
    /// a binary search brackets the crossover exactly. Diagnostic only, run
    /// once outside the timed region.
    let reachedExpansions (terrain: Terrain) (start: Cell) (goal: Cell) : int =
        let found k =
            match Pathfinding.findWithin terrain start goal k with
            | Found _ -> true
            | BudgetExhausted _ -> false
            | other -> failwith $"unexpected pathfinding result probing the closed set: {other}"

        if found 0 then
            0
        else
            let mutable hi = 1
            while not (found hi) do
                hi <- hi * 2

            let mutable lo = hi / 2

            while hi - lo > 1 do
                let mid = lo + (hi - lo) / 2
                if found mid then hi <- mid else lo <- mid

            hi

    // --- synthetic occluded terrain for the line-of-sight batch ---------

    let private pillar (x: int) (y: int) : AuthoredCell =
        { Cell = { X = x; Y = y }
          Movement = Passable
          Elevation = 0
          MoveCost = 1
          Opaque = true }

    /// A 32 x 32 grid with a regular lattice of opaque single-cell pillars
    /// (every fourth cell in each axis, offset from the edges), so a batch of
    /// traces across it exercises both the clear and the blocked branch of
    /// `Sight.trace` and its corner rule.
    let occludedTerrain () : Terrain =
        let cells =
            [| for x in 2 .. bounds.Width - 2 do
                   for y in 2 .. bounds.Height - 2 do
                       if x % 4 = 2 && y % 4 = 2 then
                           pillar x y |]

        Terrain.build bounds cells [||]

    /// The fixed batch of origin/target pairs for the line-of-sight
    /// benchmark: every cell on the left edge (x = 0) paired with every cell
    /// on the right edge (x = Width - 1). 32 x 32 = 1024 traces per operation.
    let sightBatch: (Cell * Cell)[] =
        [| for oy in 0 .. bounds.Height - 1 do
               for ty in 0 .. bounds.Height - 1 -> { X = 0; Y = oy }, { X = bounds.Width - 1; Y = ty } |]
