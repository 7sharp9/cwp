namespace CommandoWar.Sim

/// Deterministic grid pathfinding over the authoritative terrain grid
/// (docs/04_SIMULATION_SPEC.md section 8 "Initial movement progression":
/// "Compute deterministic A* path with stable neighbour order and tie-breaks";
/// docs/07_VERTICAL_SLICE.md section 5 "pathfinding and local cell
/// reservation"; docs/03_ARCHITECTURE.md section 11).
///
/// `Pathfinding.find` returns the lowest-cost cell path between two cells, its
/// integer cost, and a typed result for the no-path / invalid-endpoint /
/// budget-exhausted cases. It is a pure function of `Terrain` (passability and
/// entry cost) and two `Cell`s plus an expansion budget. Integer-only: integer
/// costs and an integer heuristic, no floating point, no `System.Math` on
/// doubles.
///
/// NOTHING in `Simulation.step` or any tick phase calls this module yet. It is
/// authored and queryable but not consumed, exactly as `Terrain` (TASK-010),
/// `Sight` (TASK-012), and the `Objective` algebra (TASK-008) are. The first
/// consumer is the Navigation and movement phase (12.7, backlog B-011), which
/// replaces `PlaceholderMovement` with a `Pathfinding`-driven executor.
///
/// Because it is a leaf that nothing authoritative references, it moves no
/// pinned fixture hash and does not touch `Canonical.encode`.
///
/// ## Algorithm
///
/// A* over the grid, 4-connected (cardinal moves only), matching `Direction`,
/// `PlaceholderMovement`, and the cardinal cover model. Diagonal / 8-connected
/// movement is a documented deferral: it would need a scaled integer cost
/// (cardinal vs diagonal) and the same "no cut through an impassable corner"
/// rule `Sight` uses, and no slice map needs it yet. If the greybox map
/// (backlog B-025) later proves diagonals are needed, that is a small
/// extension task, not a rework.
///
///   * **Cost model.** Entering a cell costs `Terrain.moveCost` for that cell;
///     the start cell's own cost is never counted. An `Impassable` cell
///     (`Terrain.passable = false`) is never expanded and never appears in a
///     path. `Terrain.BlockedCost` arithmetic is not special-cased: every
///     neighbour is gated on `Terrain.passable` first, so `moveCost` only ever
///     returns an authored value here. Path cost is the exact sum of the
///     entered cells' costs.
///   * **Heuristic.** Manhattan distance times `Terrain.BaseMoveCost`.
///     Admissible for cardinal moves whose minimum step cost is
///     `Terrain.BaseMoveCost` (authored passable cells respect this).
///     Integer.
///
/// ## Determinism (a required property, docs/09 section 3)
///
/// The frontier is ordered by a TOTAL key `(f, h, idx)` = `(g + h`, then `h`,
/// then row-major cell index `y * Width + x)`. Distinct frontier entries for
/// the same cell differ in `f` (`h` is fixed per cell), and distinct cells
/// differ in `idx`, so the key never has a genuine tie: the expansion order
/// and the returned path are fully determined and do not depend on any heap
/// implementation's behaviour for equal priorities. The frontier is a small
/// hand-rolled binary min-heap (`Frontier` below), not
/// `System.Collections.Generic.PriorityQueue`, so the pop sequence depends
/// only on that comparison. Neighbours are generated in the fixed order
/// `Direction.all` (North, East, South, West). No `Dictionary` / `HashSet` is
/// enumerated anywhere: the visited, cost, and predecessor stores are dense
/// row-major arrays, the same discipline `Terrain` and `Sight` obey.
///
/// The tie-break, pinned by a golden two-equal-cost-paths example
/// (`PathfindingTests`): among frontier entries with equal `f` and equal `h`,
/// the one with the lower row-major cell index is expanded first; combined
/// with the North/East/South/West neighbour order the path prefers advancing
/// along the lower-index axis first.
///
/// ## Bounded work (docs/04 section 19)
///
/// "Pathfinding work must have a per-tick cap or predictable upper bound."
/// `Pathfinding.findWithin` takes an explicit `maxExpansions: int` and returns
/// `BudgetExhausted` when the closed set would exceed it. `Pathfinding.find`
/// wraps it with a documented default cap derived from `Terrain.Bounds`
/// (`Width * Height`, enough to close every cell once, so `find` returns
/// `BudgetExhausted` only for a genuinely pathological call). No wall-clock
/// timing, no `Stopwatch`.
///
/// ## Totality
///
/// Any input pair yields a defined typed result, with no exception. An
/// out-of-bounds or impassable start or goal yields `InvalidEndpoint`; a
/// `start = goal` query yields `Found([| start |], 0)`.

/// The result of a pathfinding query.
type PathResult =
    /// A lowest-cost path was found. `cells` starts at the start cell and ends
    /// at the goal; consecutive cells are cardinally adjacent and passable.
    /// `cost` is the exact sum of the entered cells' `Terrain.moveCost`
    /// (the start cell's own cost is not counted), so `Found([| start |], 0)`
    /// for a `start = goal` query.
    | Found of cells: Cell[] * cost: int
    /// The start and goal are both valid but no traversable path connects
    /// them.
    | NoPath
    /// The search closed `expansions` cells (the budget) without reaching the
    /// goal. A larger `maxExpansions`, or `find` for the full-grid cap, may
    /// still find a path.
    | BudgetExhausted of expansions: int
    /// The start or the goal is out of bounds or impassable. `cell` is the
    /// offending endpoint (the start when both are invalid).
    | InvalidEndpoint of cell: Cell

[<RequireQualifiedAccess>]
module Pathfinding =

    /// One cardinal step from a cell. North is `-Y`, matching the diagnostic
    /// renderers and `Direction.all`.
    let private step (c: Cell) (d: Direction) : Cell =
        match d with
        | North -> { c with Y = c.Y - 1 }
        | East -> { c with X = c.X + 1 }
        | South -> { c with Y = c.Y + 1 }
        | West -> { c with X = c.X - 1 }

    /// A minimal deterministic binary min-heap over frontier entries
    /// `(f, h, cellIndex)`. Hand-rolled rather than
    /// `System.Collections.Generic.PriorityQueue` so the pop sequence depends
    /// only on the total order below and never on a library heap's internal
    /// layout (docs/09 section 3). The key is total across all live entries
    /// (see the module comment), so `less` is a strict total order and the pop
    /// sequence is unique.
    type private Frontier() =
        let items = ResizeArray<struct (int * int * int)>()

        let less (struct (f1, h1, i1): struct (int * int * int)) (struct (f2, h2, i2): struct (int * int * int)) : bool =
            if f1 <> f2 then f1 < f2
            elif h1 <> h2 then h1 < h2
            else i1 < i2

        let swap a b =
            let t = items.[a]
            items.[a] <- items.[b]
            items.[b] <- t

        member _.Count = items.Count

        member _.Push(entry: struct (int * int * int)) =
            items.Add entry
            let mutable c = items.Count - 1

            while c > 0 && less items.[c] items.[(c - 1) / 2] do
                let p = (c - 1) / 2
                swap c p
                c <- p

        member _.Pop() : struct (int * int * int) =
            let root = items.[0]
            let last = items.Count - 1
            items.[0] <- items.[last]
            items.RemoveAt last
            let n = items.Count
            let mutable i = 0
            let mutable moving = true

            while moving do
                let l = 2 * i + 1
                let r = 2 * i + 2
                let mutable m = i

                if l < n && less items.[l] items.[m] then
                    m <- l

                if r < n && less items.[r] items.[m] then
                    m <- r

                if m = i then
                    moving <- false
                else
                    swap i m
                    i <- m

            root

    /// A* from `start` to `goal` over `terrain`, closing at most
    /// `maxExpansions` cells. Total, pure, deterministic, integer-only.
    let findWithin (terrain: Terrain) (start: Cell) (goal: Cell) (maxExpansions: int) : PathResult =
        let b = terrain.Bounds
        let width = b.Width
        let idxOf (c: Cell) = c.Y * width + c.X

        if not (GridBounds.contains start b) || not (Terrain.passable terrain start) then
            InvalidEndpoint start
        elif not (GridBounds.contains goal b) || not (Terrain.passable terrain goal) then
            InvalidEndpoint goal
        elif start = goal then
            Found([| start |], 0)
        else
            let n = width * b.Height
            let g = Array.create n System.Int32.MaxValue
            let closed = Array.zeroCreate n: bool[]
            let cameFrom = Array.create n -1

            let heuristic (c: Cell) =
                (abs (c.X - goal.X) + abs (c.Y - goal.Y)) * Terrain.BaseMoveCost

            let frontier = Frontier()
            let startIdx = idxOf start
            g.[startIdx] <- 0
            let h0 = heuristic start
            frontier.Push(struct (h0, h0, startIdx))

            let mutable expansions = 0
            let mutable outcome = NoPath
            let mutable finished = false

            while not finished && frontier.Count > 0 do
                let struct (_, _, ci) = frontier.Pop()

                if not closed.[ci] then
                    let cur = { X = ci % width; Y = ci / width }

                    if cur = goal then
                        let rev = ResizeArray<Cell>()
                        let mutable k = ci

                        while k <> -1 do
                            rev.Add { X = k % width; Y = k / width }
                            k <- cameFrom.[k]

                        rev.Reverse()
                        outcome <- Found(rev.ToArray(), g.[ci])
                        finished <- true
                    elif expansions >= maxExpansions then
                        outcome <- BudgetExhausted expansions
                        finished <- true
                    else
                        closed.[ci] <- true
                        expansions <- expansions + 1

                        for d in Direction.all do
                            let nb = step cur d

                            if GridBounds.contains nb b && Terrain.passable terrain nb then
                                let ni = idxOf nb

                                if not closed.[ni] then
                                    let tentative = g.[ci] + Terrain.moveCost terrain nb

                                    if tentative < g.[ni] then
                                        g.[ni] <- tentative
                                        cameFrom.[ni] <- ci
                                        let hn = heuristic nb
                                        frontier.Push(struct (tentative + hn, hn, ni))

            outcome

    /// A* from `start` to `goal` over `terrain` with the default expansion cap
    /// `Terrain.Bounds.Width * Terrain.Bounds.Height` (docs/04 section 19:
    /// pathfinding must have a predictable upper bound). Total, pure,
    /// deterministic, integer-only.
    let find (terrain: Terrain) (start: Cell) (goal: Cell) : PathResult =
        findWithin terrain start goal (terrain.Bounds.Width * terrain.Bounds.Height)
