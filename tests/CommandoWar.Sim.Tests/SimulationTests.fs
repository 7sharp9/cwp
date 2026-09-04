module CommandoWar.Sim.Tests.SimulationTests

open Xunit
open CommandoWar.Sim

// --- Construction helpers --------------------------------------------------

let private bounds: GridBounds = { Width = 8; Height = 8 }
let private world () = Setup.sixAgentWorld bounds 1UL
let private agent (i: int) = AgentId.ofInt i
let private cmd (id: int) (target: AgentId) (dest: Cell) = Command.moveTo (CommandId.ofInt id) 0L target dest
let private stepWith cmds state = Simulation.step SimConfig.standard cmds state
let private stepIdle state = stepWith [||] state
let private agentOf (id: AgentId) (s: WorldState) = s.Agents |> Array.find (fun a -> a.Id = id)
let private bodies (r: StepResult) = r.Events |> Array.map (fun e -> e.Body)

// --- Tick progression ----------------------------------------------------

[<Fact>]
let ``step advances authoritative time by exactly one integer tick`` () =
    let r1 = stepIdle (world ())
    Assert.Equal(1L, r1.State.Tick)
    Assert.Equal(1L, r1.Snapshot.Tick)

    let r2 = stepIdle r1.State
    Assert.Equal(2L, r2.State.Tick)

[<Fact>]
let ``a six-agent world can be constructed and advanced by explicit steps`` () =
    let mutable state = world ()
    Assert.Equal(6, state.Agents.Length)

    for _ in 1..5 do
        state <- (stepIdle state).State

    Assert.Equal(5L, state.Tick)

// --- Move command acceptance -------------------------------------------

[<Fact>]
let ``a move command is accepted with an explicit event tagged with the tick`` () =
    let r = stepWith [| cmd 7 (agent 2) { X = 4; Y = 4 } |] (world ())
    Assert.Contains(CommandAccepted(CommandId.ofInt 7, agent 2, { X = 4; Y = 4 }), bodies r)
    Assert.All(r.Events, fun e -> Assert.Equal(1L, e.Tick))
    Assert.Equal(Some { X = 4; Y = 4 }, (agentOf (agent 2) r.State).Destination)

[<Fact>]
let ``a move command moves the targeted agent one cell per tick until it arrives`` () =
    let a = agent 0 // starts at (0,0)
    let r1 = stepWith [| cmd 1 a { X = 3; Y = 0 } |] (world ())
    Assert.Equal({ X = 1; Y = 0 }, (agentOf a r1.State).Position)

    let r2 = stepIdle r1.State
    Assert.Equal({ X = 2; Y = 0 }, (agentOf a r2.State).Position)

    let r3 = stepIdle r2.State
    Assert.Equal({ X = 3; Y = 0 }, (agentOf a r3.State).Position)
    Assert.Equal(None, (agentOf a r3.State).Destination)
    Assert.Contains(MovementCompleted(a, { X = 3; Y = 0 }), bodies r3)

[<Fact>]
let ``a move command to the agent's current cell completes on the next tick`` () =
    let a = agent 0 // at (0,0)
    let r = stepWith [| cmd 1 a { X = 0; Y = 0 } |] (world ())
    Assert.Contains(MovementCompleted(a, { X = 0; Y = 0 }), bodies r)
    Assert.Equal(None, (agentOf a r.State).Destination)

// --- Explicit, deterministic rejection --------------------------------

[<Fact>]
let ``a move command for an unknown agent is rejected explicitly and deterministically`` () =
    let ghost = AgentId.ofInt 99
    let cmds = [| cmd 1 ghost { X = 2; Y = 2 } |]
    let r1 = stepWith cmds (world ())
    let r2 = stepWith cmds (world ())

    Assert.True(bodies r1 = bodies r2)
    Assert.Contains(CommandRejected(CommandId.ofInt 1, UnknownAgent ghost), bodies r1)
    Assert.All(r1.State.Agents, fun a -> Assert.Equal(None, a.Destination))

[<Fact>]
let ``a move command to an out-of-bounds target is rejected explicitly`` () =
    let target = { X = 99; Y = 0 }
    let r = stepWith [| cmd 3 (agent 1) target |] (world ())
    Assert.Contains(CommandRejected(CommandId.ofInt 3, TargetOutOfBounds target), bodies r)
    Assert.Equal(None, (agentOf (agent 1) r.State).Destination)

// --- Stable ordering -------------------------------------------------

[<Fact>]
let ``command outcome events are ordered by command id regardless of submission order`` () =
    let cmds =
        [| cmd 30 (agent 0) { X = 1; Y = 0 }
           cmd 10 (agent 1) { X = 99; Y = 0 } // rejected
           cmd 20 (agent 2) { X = 2; Y = 2 } |]

    let r = stepWith cmds (world ())

    let commandIds =
        r.Events
        |> Array.choose (fun e ->
            match e.Body with
            | CommandAccepted(id, _, _) -> Some(CommandId.value id)
            | CommandRejected(id, _) -> Some(CommandId.value id)
            | _ -> None)

    Assert.True([| 10; 20; 30 |] = commandIds)

[<Fact>]
let ``movement events and snapshot agents are ordered by ascending agent id`` () =
    let cmds =
        [| cmd 1 (agent 5) { X = 3; Y = 5 }
           cmd 2 (agent 3) { X = 3; Y = 3 }
           cmd 3 (agent 1) { X = 3; Y = 1 } |]

    let r = stepWith cmds (world ())

    let movers =
        r.Events
        |> Array.choose (fun e ->
            match e.Body with
            | MovementStepped(id, _, _) -> Some(AgentId.value id)
            | _ -> None)

    Assert.True([| 1; 3; 5 |] = movers)
    Assert.True([| 0; 1; 2; 3; 4; 5 |] = (r.Snapshot.Agents |> Array.map (fun a -> AgentId.value a.Id)))

// --- Snapshot consistency ---------------------------------------------

[<Fact>]
let ``the snapshot mirrors authoritative agent state`` () =
    let r = stepWith [| cmd 1 (agent 4) { X = 6; Y = 4 } |] (world ())
    Assert.Equal(r.State.Agents.Length, r.Snapshot.Agents.Length)

    Array.iter2
        (fun (a: AgentState) (s: AgentSnapshot) ->
            Assert.Equal(a.Id, s.Id)
            Assert.Equal(a.Side, s.Side)
            Assert.Equal(a.Position, s.Position)
            Assert.True(a.Destination = s.Destination))
        (r.State.Agents |> Array.sortBy (fun a -> AgentId.value a.Id))
        r.Snapshot.Agents

// --- Phase order ----------------------------------------------------

[<Fact>]
let ``every tick executes the full documented phase order`` () =
    let r = stepIdle (world ())
    Assert.True(List.toArray Phases.order = r.PhaseTrace)
    Assert.Equal(CommandIntake, Array.head r.PhaseTrace)
    Assert.Equal(Output, Array.last r.PhaseTrace)

// --- Headless world construction guards ------------------------------

[<Fact>]
let ``World.create rejects an agent placed outside the grid`` () =
    let result =
        World.create { Width = 4; Height = 4 } 0UL [ Agent.create (AgentId.ofInt 0) Friendly { X = 10; Y = 0 } ]

    match result with
    | Error(AgentOutOfBounds(id, pos)) ->
        Assert.Equal(AgentId.ofInt 0, id)
        Assert.Equal({ X = 10; Y = 0 }, pos)
    | other -> Assert.Fail($"expected AgentOutOfBounds, got {other}")

// --- Pathfinding-driven movement executor (TASK-015) -------------------

let private impassable (cells: (int * int) list) : Terrain =
    Terrain.build
        bounds
        (cells
         |> List.map (fun (x, y) ->
             { Cell = { X = x; Y = y }
               Movement = Impassable
               Elevation = 0
               MoveCost = 0
               Opaque = false })
         |> List.toArray)
        [||]

let private worldWith (t: Terrain) : WorldState = { world () with Terrain = t }

/// Terrain whose listed cells cost `moveCost` to enter (elsewhere
/// `Terrain.BaseMoveCost` = 1). The `impassable` precedent.
let private costly (moveCost: int) (cells: (int * int) list) : Terrain =
    Terrain.build
        bounds
        (cells
         |> List.map (fun (x, y) ->
             { Cell = { X = x; Y = y }
               Movement = Passable
               Elevation = 0
               MoveCost = moveCost
               Opaque = false })
         |> List.toArray)
        [||]

[<Fact>]
let ``an agent routes around an impassable wall, one passable cell per tick`` () =
    // x = 1 rows 0..1 blocked; (1,2) is open, so agent 0 at (0,0) must detour.
    let t = impassable [ 1, 0; 1, 1 ]
    let a = agent 0
    let mutable st = (stepWith [| cmd 1 a { X = 3; Y = 0 } |] (worldWith t)).State
    let mutable prev = (agentOf a st).Position
    let mutable ticks = 1

    while (agentOf a st).Destination.IsSome && ticks < 50 do
        st <- (stepIdle st).State
        let p = (agentOf a st).Position
        Assert.True(Terrain.passable t p, $"agent entered impassable {p}")
        Assert.Equal(1, abs (p.X - prev.X) + abs (p.Y - prev.Y))
        prev <- p
        ticks <- ticks + 1

    Assert.Equal({ X = 3; Y = 0 }, (agentOf a st).Position)
    Assert.Equal(None, (agentOf a st).Destination)
    Assert.True(ticks > 3, "the agent took a straight line through the wall")

[<Fact>]
let ``the agent follows exactly the Pathfinding path for its destination`` () =
    let t = impassable [ 3, 0; 3, 1; 3, 2 ]
    let a = agent 0
    let dest = { X = 6; Y = 0 }

    let expected =
        match Pathfinding.find t { X = 0; Y = 0 } dest with
        | Found(cells, _) -> cells
        | other -> failwith $"unexpected {other}"

    let visited = ResizeArray<Cell>()
    visited.Add { X = 0; Y = 0 }
    let mutable st = (stepWith [| cmd 1 a dest |] (worldWith t)).State
    visited.Add (agentOf a st).Position

    while (agentOf a st).Destination.IsSome do
        st <- (stepIdle st).State
        visited.Add (agentOf a st).Position

    Assert.Equal<Cell[]>(expected, visited.ToArray())

[<Fact>]
let ``a move to a fully walled-off cell emits MovementBlocked and clears the destination`` () =
    // (5,5) is passable but its four cardinal neighbours are impassable.
    let t = impassable [ 5, 4; 5, 6; 4, 5; 6, 5 ]
    let a = agent 0
    let r = stepWith [| cmd 1 a { X = 5; Y = 5 } |] (worldWith t)
    Assert.Contains(MovementBlocked(a, { X = 0; Y = 0 }, { X = 5; Y = 5 }), bodies r)
    Assert.Equal(None, (agentOf a r.State).Destination)
    Assert.Equal({ X = 0; Y = 0 }, (agentOf a r.State).Position)

[<Fact>]
let ``a move onto an impassable cell is accepted at intake then blocked by the executor`` () =
    let t = impassable [ 4, 4 ]
    let a = agent 0
    let r = stepWith [| cmd 1 a { X = 4; Y = 4 } |] (worldWith t)
    Assert.Contains(CommandAccepted(CommandId.ofInt 1, a, { X = 4; Y = 4 }), bodies r)
    Assert.Contains(MovementBlocked(a, { X = 0; Y = 0 }, { X = 4; Y = 4 }), bodies r)
    Assert.Equal(None, (agentOf a r.State).Destination)

[<Fact>]
let ``an agent mid-route carries a Route cache that clears on arrival`` () =
    let a = agent 0
    let r1 = stepWith [| cmd 1 a { X = 4; Y = 0 } |] (world ())
    Assert.True((agentOf a r1.State).Route.IsSome)

    let mutable st = r1.State
    while (agentOf a st).Destination.IsSome do
        st <- (stepIdle st).State

    Assert.Equal(None, (agentOf a st).Route)
    Assert.Equal({ X = 4; Y = 0 }, (agentOf a st).Position)

// --- Multi-agent movement: same-tick cell reservation (TASK-017) -------

let private twoAgentWorld (aStart: Cell) (bStart: Cell) : WorldState =
    match World.create bounds 1UL [ Agent.create (agent 0) Friendly aStart; Agent.create (agent 1) Friendly bStart ] with
    | Ok w -> w
    | Error e -> failwith $"unexpected {e}"

[<Fact>]
let ``two agents converging on the same cell never occupy it simultaneously, and the loser catches up`` () =
    let a = agent 0
    let b = agent 1
    let w = twoAgentWorld { X = 3; Y = 0 } { X = 0; Y = 3 }
    let r0 = stepWith [| cmd 1 a { X = 3; Y = 7 }; cmd 2 b { X = 7; Y = 3 } |] w
    let mutable st = r0.State
    let mutable events = bodies r0
    let mutable ticks = 1

    while ((agentOf a st).Destination.IsSome || (agentOf b st).Destination.IsSome) && ticks < 30 do
        Assert.NotEqual((agentOf a st).Position, (agentOf b st).Position)
        let r = stepIdle st
        events <- Array.append events (bodies r)
        st <- r.State
        ticks <- ticks + 1

    Assert.NotEqual((agentOf a st).Position, (agentOf b st).Position)
    Assert.Contains(events, (function MovementYielded _ -> true | _ -> false))
    Assert.Equal(None, (agentOf a st).Destination)
    Assert.Equal(None, (agentOf b st).Destination)
    Assert.Equal({ X = 3; Y = 7 }, (agentOf a st).Position)
    Assert.Equal({ X = 7; Y = 3 }, (agentOf b st).Position)

[<Fact>]
let ``the agent closer to its destination wins a contested cell even with a higher agent id`` () =
    let a = agent 0 // (2,3) -> (5,3): 3 remaining route steps at the contest
    let b = agent 1 // (3,4) -> (3,3): 1 remaining route step (the contested cell is its destination)
    let w = twoAgentWorld { X = 2; Y = 3 } { X = 3; Y = 4 }
    let r = stepWith [| cmd 1 a { X = 5; Y = 3 }; cmd 2 b { X = 3; Y = 3 } |] w

    Assert.Contains(MovementYielded(a, { X = 2; Y = 3 }, { X = 3; Y = 3 }, b), bodies r)
    Assert.Contains(MovementCompleted(b, { X = 3; Y = 3 }), bodies r)
    Assert.Equal({ X = 2; Y = 3 }, (agentOf a r.State).Position)
    Assert.Equal({ X = 3; Y = 3 }, (agentOf b r.State).Position)
    Assert.Equal(Some { X = 5; Y = 3 }, (agentOf a r.State).Destination) // untouched by yielding

[<Fact>]
let ``a tied contest (equal remaining route length) is won by the lower agent id`` () =
    let a = agent 0
    let b = agent 1
    let w = twoAgentWorld { X = 2; Y = 3 } { X = 3; Y = 2 }
    let r = stepWith [| cmd 1 a { X = 4; Y = 3 }; cmd 2 b { X = 3; Y = 4 } |] w

    Assert.Contains(MovementStepped(a, { X = 2; Y = 3 }, { X = 3; Y = 3 }), bodies r)
    Assert.Contains(MovementYielded(b, { X = 3; Y = 2 }, { X = 3; Y = 3 }, a), bodies r)
    Assert.Equal({ X = 3; Y = 3 }, (agentOf a r.State).Position)
    Assert.Equal({ X = 3; Y = 2 }, (agentOf b r.State).Position)

// --- Sub-cell movement progress within an edge (TASK-018) ---------------

[<Fact>]
let ``an agent accumulates progress across ticks before entering a costly cell, then resets`` () =
    // (1,0) costs 3 to enter; every other cell costs the BaseMoveCost of 1.
    let t = costly 3 [ 1, 0 ]
    let a = agent 0
    let r1 = stepWith [| cmd 1 a { X = 4; Y = 0 } |] (worldWith t)
    Assert.Equal({ X = 0; Y = 0 }, (agentOf a r1.State).Position)
    Assert.Equal(1, (agentOf a r1.State).Progress)
    Assert.DoesNotContain(bodies r1, (function MovementStepped _ -> true | _ -> false))

    let r2 = stepIdle r1.State
    Assert.Equal({ X = 0; Y = 0 }, (agentOf a r2.State).Position)
    Assert.Equal(2, (agentOf a r2.State).Progress)

    let r3 = stepIdle r2.State
    Assert.Equal({ X = 1; Y = 0 }, (agentOf a r3.State).Position)
    Assert.Equal(0, (agentOf a r3.State).Progress)
    Assert.Contains(MovementStepped(a, { X = 0; Y = 0 }, { X = 1; Y = 0 }), bodies r3)

    // The rest of the route is ordinary terrain: one cell per tick, no
    // further accumulation.
    let mutable st = r3.State
    while (agentOf a st).Destination.IsSome do
        Assert.Equal(0, (agentOf a st).Progress)
        st <- (stepIdle st).State

    Assert.Equal({ X = 4; Y = 0 }, (agentOf a st).Position)
    Assert.Equal(0, (agentOf a st).Progress)

[<Fact>]
let ``a completing agent's frozen progress on a lost contest resumes correctly next tick`` () =
    // (3,3) costs 3 to enter; both agents pass through it toward different
    // onward destinations (so neither's final destination coincides with the
    // contested cell — the known "walking onto a stationary agent's cell"
    // gap, TASK-017, does not apply here). Symmetric remaining route length
    // (3 cells each), so the tie is broken by agent id: agent 0 wins.
    let t = costly 3 [ 3, 3 ]
    let a = agent 0
    let b = agent 1

    let w =
        match
            World.create
                bounds
                1UL
                [ Agent.create a Friendly { X = 2; Y = 3 }; Agent.create b Friendly { X = 3; Y = 2 } ]
        with
        | Ok w -> { w with Terrain = t }
        | Error e -> failwith $"unexpected {e}"

    let r1 = stepWith [| cmd 1 a { X = 5; Y = 3 }; cmd 2 b { X = 3; Y = 5 } |] w
    let r2 = stepIdle r1.State
    Assert.Equal(2, (agentOf a r2.State).Progress)
    Assert.Equal(2, (agentOf b r2.State).Progress)

    // Tick 3: both reach the threshold; agent 0 wins the tie, agent 1 yields
    // and freezes at progress 2 (not reset to 0, not incremented to 3).
    let r3 = stepIdle r2.State
    Assert.Equal({ X = 3; Y = 3 }, (agentOf a r3.State).Position)
    Assert.Equal(0, (agentOf a r3.State).Progress)
    Assert.Equal({ X = 3; Y = 2 }, (agentOf b r3.State).Position)
    Assert.Equal(2, (agentOf b r3.State).Progress)
    Assert.Contains(MovementYielded(b, { X = 3; Y = 2 }, { X = 3; Y = 3 }, a), bodies r3)

    // Tick 4: agent 0 has vacated (3,3) for (4,3); agent 1 is uncontested and
    // enters (3,3) immediately — one tick behind, not re-accumulating from 0
    // (which would delay it three more ticks instead of one).
    let r4 = stepIdle r3.State
    Assert.Equal({ X = 4; Y = 3 }, (agentOf a r4.State).Position)
    Assert.Equal({ X = 3; Y = 3 }, (agentOf b r4.State).Position)
    Assert.Equal(0, (agentOf b r4.State).Progress)

[<Fact>]
let ``World.create rejects duplicate agent ids`` () =
    let result =
        World.create
            bounds
            0UL
            [ Agent.create (AgentId.ofInt 1) Friendly { X = 0; Y = 0 }
              Agent.create (AgentId.ofInt 1) Friendly { X = 1; Y = 1 } ]

    match result with
    | Error(DuplicateAgentId id) -> Assert.Equal(AgentId.ofInt 1, id)
    | other -> Assert.Fail($"expected DuplicateAgentId, got {other}")
