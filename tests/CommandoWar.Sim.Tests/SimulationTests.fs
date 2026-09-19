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
    // x = 2 rows 0..2 blocked, so agent 0 at (0,0) must detour south through
    // the open x = 1 column and cross at row 3. The six-agent world parks
    // agents 1..5 down column x = 0, so the wall is placed clear of them: the
    // detour never re-enters x = 0 and the one-agent-per-cell invariant
    // (TASK-022) is not what this fact exercises.
    let t = impassable [ 2, 0; 2, 1; 2, 2 ]
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
let ``a move to a fully walled-off cell is Unable NoKnownRoute at appraisal`` () =
    // (5,5) is passable but its four cardinal neighbours are impassable, so
    // stage 2 (Pathfinding) has no route: the Appraisal phase (TASK-028)
    // refuses the order before the Navigation phase ever sees a Destination.
    let t = impassable [ 5, 4; 5, 6; 4, 5; 6, 5 ]
    let a = agent 0
    let r = stepWith [| cmd 1 a { X = 5; Y = 5 } |] (worldWith t)
    Assert.Equal(Some(Unable(NoKnownRoute, [||])), (agentOf a r.State).Disposition)
    Assert.DoesNotContain("movement-blocked", bodies r |> Array.map (fun b -> $"{b}"))
    Assert.Equal(None, (agentOf a r.State).Destination)
    Assert.Equal({ X = 0; Y = 0 }, (agentOf a r.State).Position)

[<Fact>]
let ``a move onto an impassable cell is accepted at intake then Unable at appraisal`` () =
    let t = impassable [ 4, 4 ]
    let a = agent 0
    let r = stepWith [| cmd 1 a { X = 4; Y = 4 } |] (worldWith t)
    Assert.Contains(CommandAccepted(CommandId.ofInt 1, a, { X = 4; Y = 4 }), bodies r)
    Assert.Equal(Some(Unable(NoKnownRoute, [||])), (agentOf a r.State).Disposition)
    Assert.DoesNotContain("movement-blocked", bodies r |> Array.map (fun b -> $"{b}"))
    Assert.Equal(None, (agentOf a r.State).Destination)

[<Fact>]
let ``the Navigation phase still emits MovementBlocked for a Destination with no path`` () =
    // Appraisal now catches an infeasible order before the Navigation phase,
    // so MovementBlocked is only reachable for a Destination set outside the
    // order pipeline. Pin the movement-phase logic directly.
    let t = impassable [ 5, 4; 5, 6; 4, 5; 6, 5 ]
    let a = agent 0

    let w =
        { worldWith t with
            Agents =
                (worldWith t).Agents
                |> Array.map (fun x -> if x.Id = a then { x with Destination = Some { X = 5; Y = 5 } } else x) }

    let r = stepIdle w
    Assert.Contains(MovementBlocked(a, { X = 0; Y = 0 }, { X = 5; Y = 5 }), bodies r)
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

// --- Per-agent movement speed (TASK-049, backlog B-058) -----------------

[<Fact>]
let ``a half-speed agent takes twice as many ticks to cross an ordinary cell as a default-speed agent`` () =
    let a = agent 0 // Agent.MoveSpeedDefault
    let b = agent 1 // half that

    let w =
        match
            World.create
                bounds
                1UL
                [ Agent.create a Friendly { X = 0; Y = 0 }
                  { Agent.create b Friendly { X = 0; Y = 1 } with
                      MoveSpeed = Agent.MoveSpeedDefault / 2 } ]
        with
        | Ok w -> w
        | Error e -> failwith $"unexpected {e}"

    let r1 = stepWith [| cmd 1 a { X = 1; Y = 0 }; cmd 2 b { X = 1; Y = 1 } |] w
    Assert.Contains(MovementCompleted(a, { X = 1; Y = 0 }), bodies r1)
    Assert.Equal({ X = 1; Y = 0 }, (agentOf a r1.State).Position)
    Assert.Equal({ X = 0; Y = 1 }, (agentOf b r1.State).Position) // still mid-edge
    Assert.DoesNotContain(bodies r1, (function MovementCompleted(id, _) when id = b -> true | _ -> false))

    let r2 = stepIdle r1.State
    Assert.Contains(MovementCompleted(b, { X = 1; Y = 1 }), bodies r2)
    Assert.Equal({ X = 1; Y = 1 }, (agentOf b r2.State).Position)

// --- Command validation and multi-recipient addressing (TASK-020) --------

/// A world with agents 0 and 1 Friendly and agent 2 Hostile, for the
/// authorisation check (`Setup.sixAgentWorld` is all-Friendly).
let private mixedWorld () : WorldState =
    match
        World.create
            bounds
            1UL
            [ Agent.create (agent 0) Friendly { X = 0; Y = 0 }
              Agent.create (agent 1) Friendly { X = 0; Y = 1 }
              Agent.create (agent 2) Hostile { X = 0; Y = 2 } ]
    with
    | Ok w -> w
    | Error e -> failwith $"unexpected {e}"

let private moveMany (id: int) (ids: int list) (dest: Cell) =
    Command.moveToMany (CommandId.ofInt id) 0L (ids |> List.map agent) dest Routine Standard

let private acceptedAgents (r: StepResult) =
    bodies r
    |> Array.choose (function
        | CommandAccepted(_, a, _) -> Some(AgentId.value a)
        | _ -> None)

[<Fact>]
let ``a command naming several friendly recipients emits one CommandAccepted per recipient and sets every destination`` () =
    let dest = { X = 4; Y = 4 }
    let r = stepWith [| moveMany 1 [ 1; 3; 5 ] dest |] (world ())

    Assert.Equal<int[]>([| 1; 3; 5 |], acceptedAgents r)

    for i in [ 1; 3; 5 ] do
        Assert.Equal(Some dest, (agentOf (agent i) r.State).Destination)

[<Fact>]
let ``multi-recipient acceptance is ordered by ascending agent id regardless of authoring order`` () =
    let r = stepWith [| moveMany 1 [ 5; 1; 3 ] { X = 4; Y = 4 } |] (world ())
    Assert.Equal<int[]>([| 1; 3; 5 |], acceptedAgents r)

[<Fact>]
let ``a hostile recipient is rejected UnauthorisedRecipient while a friendly co-recipient still accepts`` () =
    let dest = { X = 3; Y = 3 }
    let r = stepWith [| moveMany 1 [ 1; 2 ] dest |] (mixedWorld ()) // agent 2 is Hostile

    Assert.Contains(CommandRejected(CommandId.ofInt 1, UnauthorisedRecipient(agent 2)), bodies r)
    Assert.Contains(CommandAccepted(CommandId.ofInt 1, agent 1, dest), bodies r)
    Assert.Equal(Some dest, (agentOf (agent 1) r.State).Destination)
    Assert.Equal(None, (agentOf (agent 2) r.State).Destination)

[<Fact>]
let ``a command with an empty recipients list is rejected EmptyRecipients with no per-recipient events`` () =
    let c = Command.moveToMany (CommandId.ofInt 1) 0L [] { X = 2; Y = 2 } Routine Standard
    let r = stepWith [| c |] (world ())

    Assert.Contains(CommandRejected(CommandId.ofInt 1, EmptyRecipients), bodies r)
    Assert.DoesNotContain(bodies r, (function CommandAccepted _ -> true | _ -> false))

[<Fact>]
let ``a command listing the same agent twice in Recipients is rejected DuplicateRecipient as a whole command`` () =
    let r = stepWith [| moveMany 1 [ 2; 4; 2 ] { X = 5; Y = 5 } |] (world ())

    Assert.Contains(CommandRejected(CommandId.ofInt 1, DuplicateRecipient(agent 2)), bodies r)
    Assert.DoesNotContain(bodies r, (function CommandAccepted _ -> true | _ -> false))
    Assert.Equal(None, (agentOf (agent 2) r.State).Destination)
    Assert.Equal(None, (agentOf (agent 4) r.State).Destination)

[<Fact>]
let ``two commands in one tick sharing a command id are both rejected and neither destination is applied, independent of batch order`` () =
    let mk (target: Cell) = Command.moveTo (CommandId.ofInt 7) 0L (agent 0) target
    let forwardOrder = [| mk { X = 3; Y = 0 }; mk { X = 0; Y = 3 } |]
    let reverseOrder = [| mk { X = 0; Y = 3 }; mk { X = 3; Y = 0 } |]

    let forward = stepWith forwardOrder (world ())
    let reverse = stepWith reverseOrder (world ())

    let dupRejections r =
        bodies r
        |> Array.filter (function
            | CommandRejected(_, DuplicateCommandId cid) -> cid = CommandId.ofInt 7
            | _ -> false)

    Assert.Equal(2, (dupRejections forward).Length)
    Assert.DoesNotContain(bodies forward, (function CommandAccepted _ -> true | _ -> false))
    Assert.Equal(None, (agentOf (agent 0) forward.State).Destination)

    // Reject-all (not "first wins") makes the emitted events and the
    // end-of-tick state identical whichever order the invalid batch arrives.
    Assert.True(bodies forward = bodies reverse)
    Assert.Equal(Hashing.hash forward.State, Hashing.hash reverse.State)

// --- Issue-tick eligibility (TASK-024) ----------------------------------

/// A single-recipient move issued on `issuedAtTick` (vs `cmd`, which fixes it
/// at 0). Command intake requires `issuedAtTick` in [0, currentTick].
let private cmdIssuedAt (id: int) (issuedAtTick: int64) (target: AgentId) (dest: Cell) =
    Command.moveTo (CommandId.ofInt id) issuedAtTick target dest

[<Fact>]
let ``a command issued after the tick being processed is rejected IssueTickOutOfRange`` () =
    // world () is at tick 0; stepWith processes tick 1.
    let r = stepWith [| cmdIssuedAt 1 2L (agent 0) { X = 3; Y = 0 } |] (world ())

    Assert.Contains(CommandRejected(CommandId.ofInt 1, IssueTickOutOfRange(2L, 1L)), bodies r)
    Assert.DoesNotContain(bodies r, (function CommandAccepted _ -> true | _ -> false))
    Assert.Equal(None, (agentOf (agent 0) r.State).Destination)

[<Fact>]
let ``a command issued on the tick being processed is accepted`` () =
    let dest = { X = 3; Y = 0 }
    let r = stepWith [| cmdIssuedAt 1 1L (agent 0) dest |] (world ())

    Assert.Contains(CommandAccepted(CommandId.ofInt 1, agent 0, dest), bodies r)
    Assert.Equal(Some dest, (agentOf (agent 0) r.State).Destination)

[<Fact>]
let ``a command issued on an earlier tick and delivered now is accepted, with no staleness rejection`` () =
    // Advance to tick 2, then deliver a command issued back on tick 1.
    let st = (stepIdle (stepIdle (world ())).State).State
    Assert.Equal(2L, st.Tick)
    let dest = { X = 3; Y = 0 }
    let r = stepWith [| cmdIssuedAt 1 1L (agent 0) dest |] st

    Assert.Contains(CommandAccepted(CommandId.ofInt 1, agent 0, dest), bodies r)
    Assert.DoesNotContain(
        bodies r,
        (function
        | CommandRejected(_, IssueTickOutOfRange _) -> true
        | _ -> false)
    )

[<Fact>]
let ``a command with a negative issued-at tick is rejected IssueTickOutOfRange`` () =
    let r = stepWith [| cmdIssuedAt 1 -1L (agent 0) { X = 3; Y = 0 } |] (world ())

    Assert.Contains(CommandRejected(CommandId.ofInt 1, IssueTickOutOfRange(-1L, 1L)), bodies r)
    Assert.DoesNotContain(bodies r, (function CommandAccepted _ -> true | _ -> false))

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

// --- Runtime cell-occupancy correctness (TASK-022) ---------------------

/// A world with one friendly agent per listed cell, ids ascending from 0.
let private occWorld (cells: Cell list) : WorldState =
    match
        World.create bounds 1UL (cells |> List.mapi (fun i c -> Agent.create (agent i) Friendly c))
    with
    | Ok w -> w
    | Error e -> failwith $"unexpected {e}"

/// Every live agent holds a distinct cell.
let private distinctCells (s: WorldState) =
    let ps = s.Agents |> Array.map (fun a -> a.Position)
    Assert.Equal(ps.Length, (Array.distinct ps).Length)

let private obstructedBodies (r: StepResult) =
    bodies r
    |> Array.choose (function
        | MovementObstructed(a, at, blocked, occ) -> Some(AgentId.value a, at, blocked, AgentId.value occ)
        | _ -> None)

[<Fact>]
let ``a mover whose only route runs through a permanently idle agent never enters that cell and emits MovementObstructed`` () =
    // Agent 0 at (0,0) -> (4,0); agent 1 idle on the route at (2,0).
    let a = agent 0
    let b = agent 1
    let w = occWorld [ { X = 0; Y = 0 }; { X = 2; Y = 0 } ]
    let r0 = stepWith [| cmd 1 a { X = 4; Y = 0 } |] w
    let mutable st = r0.State
    let mutable sawObstructed = obstructedBodies r0 |> Array.isEmpty |> not

    for _ in 1..8 do
        distinctCells st
        Assert.NotEqual({ X = 2; Y = 0 }, (agentOf a st).Position)
        let r = stepIdle st
        sawObstructed <- sawObstructed || (obstructedBodies r |> Array.isEmpty |> not)
        st <- r.State

    distinctCells st
    Assert.Equal({ X = 1; Y = 0 }, (agentOf a st).Position) // parked one cell short, retrying
    Assert.Equal({ X = 2; Y = 0 }, (agentOf b st).Position) // B never moved
    Assert.Equal(Some { X = 4; Y = 0 }, (agentOf a st).Destination) // destination untouched
    Assert.True(sawObstructed, "agent 0 never emitted MovementObstructed")

    let r = stepIdle st
    Assert.Contains((0, { X = 1; Y = 0 }, { X = 2; Y = 0 }, 1), obstructedBodies r)

[<Fact>]
let ``two adjacent agents each ordered onto the other's cell are both obstructed indefinitely and never swap`` () =
    let a = agent 0
    let b = agent 1
    let w = occWorld [ { X = 2; Y = 2 }; { X = 3; Y = 2 } ]
    let r0 = stepWith [| cmd 1 a { X = 3; Y = 2 }; cmd 2 b { X = 2; Y = 2 } |] w
    let mutable st = r0.State

    for _ in 0..5 do
        distinctCells st
        Assert.Equal({ X = 2; Y = 2 }, (agentOf a st).Position)
        Assert.Equal({ X = 3; Y = 2 }, (agentOf b st).Position)
        let ob = obstructedBodies (stepIdle st)
        Assert.Contains((0, { X = 2; Y = 2 }, { X = 3; Y = 2 }, 1), ob)
        Assert.Contains((1, { X = 3; Y = 2 }, { X = 2; Y = 2 }, 0), ob)
        st <- (stepIdle st).State

[<Fact>]
let ``four agents rotating around a 2x2 block are all obstructed, with no first mover, deterministically`` () =
    // The minimal pure rotation deadlock on a 4-connected grid: cardinal
    // adjacency is bipartite, so every cycle has even length and a 3-agent
    // pure cycle is geometrically impossible. Four agents fill the 2x2 block
    // (2,2)-(3,3); each is ordered clockwise onto the next agent's cell, so no
    // agent's next cell is ever free.
    let cells = [ { X = 2; Y = 2 }; { X = 3; Y = 2 }; { X = 3; Y = 3 }; { X = 2; Y = 3 } ]
    let dests = [ { X = 3; Y = 2 }; { X = 3; Y = 3 }; { X = 2; Y = 3 }; { X = 2; Y = 2 } ]

    let cmds =
        List.mapi (fun i d -> cmd (i + 1) (agent i) d) dests |> List.toArray

    let run () =
        let r0 = stepWith cmds (occWorld cells)
        let mutable st = r0.State
        let mutable evs = bodies r0

        for _ in 1..4 do
            let r = stepIdle st
            evs <- Array.append evs (bodies r)
            st <- r.State

        evs, st

    let evs1, st1 = run ()
    let evs2, st2 = run ()

    distinctCells st1
    // No agent ever left its start cell.
    List.iteri (fun i c -> Assert.Equal(c, (agentOf (agent i) st1).Position)) cells
    // Every agent is obstructed by the agent holding its target cell.
    let obstructedIds =
        evs1
        |> Array.choose (function
            | MovementObstructed(a, _, _, _) -> Some(AgentId.value a)
            | _ -> None)
        |> Array.distinct
        |> Array.sort

    Assert.Equal<int[]>([| 0; 1; 2; 3 |], obstructedIds)
    Assert.DoesNotContain(evs1, (function MovementStepped _ -> true | _ -> false))
    // Deterministic: identical events and identical end-of-run hash.
    Assert.Equal<EventBody[]>(evs1, evs2)
    Assert.Equal(Hashing.hash st1, Hashing.hash st2)

[<Fact>]
let ``a three-agent follow chain into a free cell advances the whole chain on the same tick`` () =
    // Agents 0,1,2 in a line at x = 1,2,3 (row 1); all ordered east. The lead
    // (agent 2) has a free cell ahead, so the vacation chain resolves and all
    // three step on the same tick, every tick.
    let w = occWorld [ { X = 1; Y = 1 }; { X = 2; Y = 1 }; { X = 3; Y = 1 } ]

    let cmds =
        [| cmd 1 (agent 0) { X = 6; Y = 1 }
           cmd 2 (agent 1) { X = 6; Y = 1 }
           cmd 3 (agent 2) { X = 6; Y = 1 } |]

    let r0 = stepWith cmds w
    distinctCells r0.State
    Assert.Equal({ X = 2; Y = 1 }, (agentOf (agent 0) r0.State).Position)
    Assert.Equal({ X = 3; Y = 1 }, (agentOf (agent 1) r0.State).Position)
    Assert.Equal({ X = 4; Y = 1 }, (agentOf (agent 2) r0.State).Position)

    let stepped (r: StepResult) =
        bodies r |> Array.filter (function MovementStepped _ -> true | _ -> false) |> Array.length

    Assert.Equal(3, stepped r0)

    let mutable st = r0.State
    for _ in 1..2 do
        let r = stepIdle st
        Assert.Equal(3, stepped r)
        distinctCells r.State
        st <- r.State

    // After 3 ticks the train has advanced 3 cells intact.
    Assert.Equal({ X = 4; Y = 1 }, (agentOf (agent 0) st).Position)
    Assert.Equal({ X = 5; Y = 1 }, (agentOf (agent 1) st).Position)
    Assert.Equal({ X = 6; Y = 1 }, (agentOf (agent 2) st).Position)

[<Fact>]
let ``two agents converging on a cell held by a stationary third never enter it and never collide`` () =
    // Agent 2 idle at (3,3). Agent 0 at (1,3) -> (5,3) and agent 1 at (3,1) ->
    // (3,5) both route through (3,3): stage 2a picks one candidate (rival),
    // stage 2b obstructs it on the stationary occupant. Neither enters (3,3).
    let w = occWorld [ { X = 1; Y = 3 }; { X = 3; Y = 1 }; { X = 3; Y = 3 } ]
    let r0 = stepWith [| cmd 1 (agent 0) { X = 5; Y = 3 }; cmd 2 (agent 1) { X = 3; Y = 5 } |] w
    let mutable st = r0.State
    let mutable sawYield = false
    let mutable sawObstruct = false

    for _ in 0..9 do
        distinctCells st
        Assert.NotEqual({ X = 3; Y = 3 }, (agentOf (agent 0) st).Position)
        Assert.NotEqual({ X = 3; Y = 3 }, (agentOf (agent 1) st).Position)
        Assert.Equal({ X = 3; Y = 3 }, (agentOf (agent 2) st).Position)
        let r = stepIdle st
        sawYield <- sawYield || (bodies r |> Array.exists (function MovementYielded _ -> true | _ -> false))
        sawObstruct <- sawObstruct || (bodies r |> Array.exists (function MovementObstructed _ -> true | _ -> false))
        st <- r.State

    Assert.True(sawYield, "expected a MovementYielded from the rival contest")
    Assert.True(sawObstruct, "expected a MovementObstructed on the stationary occupant")

// --- Perception and shared squad tactical knowledge (TASK-026) ---------

/// A world with the listed friendly and hostile agents (ids ascending from 0
/// across both lists) on `terrain`.
let private perceptionWorld (b: GridBounds) (friendly: (int * Cell) list) (hostile: (int * Cell) list) (t: Terrain) : WorldState =
    let agents =
        (friendly |> List.map (fun (i, c) -> Agent.create (agent i) Friendly c))
        @ (hostile |> List.map (fun (i, c) -> Agent.create (agent i) Hostile c))

    match World.create b 1UL agents with
    | Ok w -> { w with Terrain = t }
    | Error e -> failwith $"unexpected {e}"

/// Opaque (sight-blocking) passable cells on an otherwise empty `b`-sized grid.
let private opaqueCells (b: GridBounds) (cells: (int * int) list) : Terrain =
    Terrain.build
        b
        (cells
         |> List.map (fun (x, y) ->
             { Cell = { X = x; Y = y }
               Movement = Passable
               Elevation = 0
               MoveCost = Terrain.BaseMoveCost
               Opaque = true })
         |> List.toArray)
        [||]

let private contactOf (id: AgentId) (s: WorldState) =
    s.TacticalKnowledge |> Array.tryFind (fun c -> c.Contact = id)

let private hostileContactOf (id: AgentId) (s: WorldState) =
    s.HostileTacticalKnowledge |> Array.tryFind (fun c -> c.Contact = id)

[<Fact>]
let ``a friendly with clear line of sight to an in-range hostile observes it and shares it in the squad picture`` () =
    let b: GridBounds = { Width = 16; Height = 8 }
    let w = perceptionWorld b [ 0, { X = 1; Y = 1 } ] [ 1, { X = 6; Y = 1 } ] (Terrain.empty b)
    let r = stepIdle w // tick 1

    Assert.Contains(ContactObserved(agent 0, agent 1, { X = 6; Y = 1 }), bodies r)
    // Perception is symmetric: the hostile observes the friendly too.
    Assert.Contains(ContactObserved(agent 1, agent 0, { X = 1; Y = 1 }), bodies r)

    Assert.Equal<AgentId[]>([| agent 1 |], (agentOf (agent 0) r.State).VisibleContacts)

    // The friendly's observation reaches the shared friendly squad picture.
    let c = Assert.Single r.State.TacticalKnowledge
    Assert.Equal(agent 1, c.Contact)
    Assert.Equal({ X = 6; Y = 1 }, c.LastKnownCell)
    Assert.Equal(1L, c.LastSeenTick)
    Assert.Equal(PerceptionConfig.ConfidenceFull, c.Confidence)

    // TASK-034 (backlog B-022, partial): the hostile's symmetric observation
    // reaches the Hostile side's own picture the same tick, via the identical
    // Perception.mergeKnowledge call filtered the other way.
    let hc = Assert.Single r.State.HostileTacticalKnowledge
    Assert.Equal(agent 0, hc.Contact)
    Assert.Equal({ X = 1; Y = 1 }, hc.LastKnownCell)
    Assert.Equal(1L, hc.LastSeenTick)
    Assert.Equal(PerceptionConfig.ConfidenceFull, hc.Confidence)

    // A new sighting emits ContactObserved once; a second idle tick with the
    // contact still visible does not re-emit it (no per-tick flood).
    let r2 = stepIdle r.State
    Assert.DoesNotContain(bodies r2, (function ContactObserved _ -> true | _ -> false))
    Assert.Equal(2L, (contactOf (agent 1) r2.State).Value.LastSeenTick)
    Assert.Equal(2L, (hostileContactOf (agent 0) r2.State).Value.LastSeenTick)

[<Fact>]
let ``an opaque cell between a friendly and a hostile blocks the observation entirely`` () =
    let b: GridBounds = { Width = 16; Height = 8 }
    let t = opaqueCells b [ 3, 1 ]
    let w = perceptionWorld b [ 0, { X = 1; Y = 1 } ] [ 1, { X = 6; Y = 1 } ] t
    let r = stepIdle w

    Assert.DoesNotContain(bodies r, (function ContactObserved _ -> true | _ -> false))
    Assert.Empty((agentOf (agent 0) r.State).VisibleContacts)
    Assert.Empty(r.State.TacticalKnowledge)

[<Fact>]
let ``a hostile beyond SightRange with clear line of sight is not observed`` () =
    // PerceptionConfig.SightRange is a Chebyshev radius of 10.
    let b: GridBounds = { Width = 30; Height = 6 }
    let farWorld = perceptionWorld b [ 0, { X = 1; Y = 1 } ] [ 1, { X = 13; Y = 1 } ] (Terrain.empty b) // dx = 12
    let rFar = stepIdle farWorld
    Assert.DoesNotContain(bodies rFar, (function ContactObserved _ -> true | _ -> false))
    Assert.Empty(rFar.State.TacticalKnowledge)

    // Control: at exactly the range cap (dx = 10) the same clear line of sight
    // does produce the observation.
    let edgeWorld = perceptionWorld b [ 0, { X = 1; Y = 1 } ] [ 1, { X = 11; Y = 1 } ] (Terrain.empty b)
    let rEdge = stepIdle edgeWorld
    Assert.Contains(ContactObserved(agent 0, agent 1, { X = 11; Y = 1 }), bodies rEdge)

[<Fact>]
let ``two friendlies, only one with line of sight to a hostile, share the contact the same tick`` () =
    let b: GridBounds = { Width = 24; Height = 24 }
    // Friendly 0 has a clear short line to the hostile; friendly 1 is far
    // enough that the hostile is outside its own SightRange.
    let w =
        perceptionWorld b [ 0, { X = 1; Y = 1 }; 1, { X = 1; Y = 20 } ] [ 2, { X = 6; Y = 1 } ] (Terrain.empty b)

    let r = stepIdle w

    Assert.Equal<AgentId[]>([| agent 2 |], (agentOf (agent 0) r.State).VisibleContacts)
    Assert.Empty((agentOf (agent 1) r.State).VisibleContacts)

    // Instant squad sharing: the contact is in the one shared picture even
    // though friendly 1 never saw it.
    let c = Assert.Single r.State.TacticalKnowledge
    Assert.Equal(agent 2, c.Contact)
    Assert.Equal(1L, c.LastSeenTick)

[<Fact>]
let ``a contact seen then lost drops a confidence band after StaleAfter and expires with ContactExpired after ExpireAfter`` () =
    // Height 11 / hostile at (6,9): a Y-offset of 8 keeps the Chebyshev
    // distance to the hostile at a constant 8 while the friendly's X stays
    // within 8 of the hostile's — above CombatConfig.WeaponRange (7), so
    // TASK-045's real combat consequences never trigger and do not stall the
    // friendly's walk, but within PerceptionConfig.SightRange (10), so
    // perception (this test's actual subject) is unaffected.
    let b: GridBounds = { Width = 40; Height = 11 }
    let w0 = perceptionWorld b [ 0, { X = 2; Y = 1 } ] [ 1, { X = 6; Y = 9 } ] (Terrain.empty b)

    // The eastward order runs the friendly past the observed hostile, which
    // the Appraisal phase (TASK-028) treats as exposure. This test is about
    // contact decay, not appraisal — give agent 0 the discipline to accept the
    // order so it actually walks away.
    let w =
        { w0 with
            Agents =
                w0.Agents
                |> Array.map (fun a -> if a.Id = agent 0 then { a with Discipline = 12 } else a) }

    // Walk the friendly far east, out of sight of the stationary hostile.
    let mutable st =
        (stepWith [| Command.moveTo (CommandId.ofInt 1) 0L (agent 0) { X = 39; Y = 1 } |] w).State

    let seenThisTick (s: WorldState) =
        match contactOf (agent 1) s with
        | Some c -> c.LastSeenTick = s.Tick
        | None -> false

    Assert.True(seenThisTick st, "the hostile should be seen on the command tick")

    // Safety cap (the "an agent routes around an impassable wall" precedent,
    // line 211 above): the geometry keeps agent 0 out of combat range
    // entirely, but this still fails loudly instead of hanging if that
    // reasoning is ever wrong.
    let mutable ticks = 0

    while seenThisTick st && ticks < 40 do
        st <- (stepIdle st).State
        ticks <- ticks + 1

    Assert.True(ticks < 40, "the friendly never walked out of the hostile's sight range")

    let lastSeen = (contactOf (agent 1) st).Value.LastSeenTick
    Assert.True(lastSeen >= 1L)
    // Just lost, before StaleAfter: still full confidence.
    Assert.Equal(PerceptionConfig.ConfidenceFull, (contactOf (agent 1) st).Value.Confidence)

    // At lastSeen + StaleAfter the confidence drops exactly one band.
    while st.Tick < lastSeen + int64 PerceptionConfig.StaleAfter do
        st <- (stepIdle st).State

    Assert.Equal(
        PerceptionConfig.ConfidenceFull - PerceptionConfig.ConfidenceBandDrop,
        (contactOf (agent 1) st).Value.Confidence
    )

    // At lastSeen + ExpireAfter the contact is removed and ContactExpired fires.
    let mutable expired: (int64 * Cell) option = None

    while st.Tick < lastSeen + int64 PerceptionConfig.ExpireAfter do
        let r = stepIdle st

        for e in r.Events do
            match e.Body with
            | ContactExpired(c, cell) when c = agent 1 -> expired <- Some(e.Tick, cell)
            | _ -> ()

        st <- r.State

    Assert.Equal(Some(lastSeen + int64 PerceptionConfig.ExpireAfter, { X = 6; Y = 9 }), expired)
    Assert.Equal(None, contactOf (agent 1) st)

// --- Communication constraints and order delivery (TASK-027) ----------

/// The six-agent friendly world with the listed agent ids' communication
/// blacked out (`CommunicationAvailable = false`).
let private commsBlackoutWorld (blackedOut: int list) : WorldState =
    let w = world ()

    { w with
        Agents =
            w.Agents
            |> Array.map (fun a ->
                if List.contains (AgentId.value a.Id) blackedOut then
                    { a with CommunicationAvailable = false }
                else
                    a) }

let private undeliveredIn (r: StepResult) =
    bodies r
    |> Array.choose (function
        | OrderUndelivered(c, recipient, reason) -> Some(c, recipient, reason)
        | _ -> None)

[<Fact>]
let ``an order to a reachable recipient is delivered the same tick it is accepted`` () =
    // Agent 2 starts at (0,2). Command intake accepts, the Communication phase
    // delivers (writes Destination), and Navigation and movement (phase 7,
    // same tick) steps the agent one cell — exactly the pre-TASK-027
    // behaviour, since nothing runs between Command intake and Communication.
    let r = stepWith [| cmd 1 (agent 2) { X = 4; Y = 2 } |] (world ())

    Assert.Contains(CommandAccepted(CommandId.ofInt 1, agent 2, { X = 4; Y = 2 }), bodies r)
    Assert.Empty(undeliveredIn r)
    Assert.Equal(Some { X = 4; Y = 2 }, (agentOf (agent 2) r.State).Destination)
    Assert.Equal({ X = 1; Y = 2 }, (agentOf (agent 2) r.State).Position)

[<Fact>]
let ``an order to an unreachable recipient emits OrderUndelivered and sets no destination`` () =
    let r = stepWith [| cmd 1 (agent 2) { X = 4; Y = 2 } |] (commsBlackoutWorld [ 2 ])

    // The simulation still accepted the order into the pipeline.
    Assert.Contains(CommandAccepted(CommandId.ofInt 1, agent 2, { X = 4; Y = 2 }), bodies r)
    // But the Communication phase could not reach the recipient.
    Assert.Equal<_[]>([| (CommandId.ofInt 1, agent 2, UnableToCommunicate) |], undeliveredIn r)
    Assert.Equal(None, (agentOf (agent 2) r.State).Destination)
    // The agent never moves.
    Assert.Equal({ X = 0; Y = 2 }, (agentOf (agent 2) r.State).Position)

[<Fact>]
let ``communication delivery is deterministic across two runs`` () =
    let run () =
        stepWith
            [| cmd 1 (agent 2) { X = 4; Y = 2 }; cmd 3 (agent 4) { X = 2; Y = 4 } |]
            (commsBlackoutWorld [ 2 ])

    let r1 = run ()
    let r2 = run ()
    Assert.True(bodies r1 = bodies r2)
    Assert.Equal(r1.StateHash, r2.StateHash)

[<Fact>]
let ``a multi-recipient order delivers to the reachable recipients and reports only the cut-off one`` () =
    let order =
        Command.moveToMany (CommandId.ofInt 5) 0L [ agent 1; agent 2; agent 3 ] { X = 5; Y = 3 } Routine Standard

    let r = stepWith [| order |] (commsBlackoutWorld [ 2 ])

    Assert.Equal(Some { X = 5; Y = 3 }, (agentOf (agent 1) r.State).Destination)
    Assert.Equal(Some { X = 5; Y = 3 }, (agentOf (agent 3) r.State).Destination)
    Assert.Equal(None, (agentOf (agent 2) r.State).Destination)
    Assert.Equal<_[]>([| (CommandId.ofInt 5, agent 2, UnableToCommunicate) |], undeliveredIn r)

[<Fact>]
let ``an undelivered order does not cancel a destination the recipient already held`` () =
    // Agent 2 is already en route to (5,5) and is now comms-blacked-out; a new
    // order to (0,0) cannot reach it, so the old order continues unchanged.
    let w = commsBlackoutWorld [ 2 ]

    let w =
        { w with
            Agents =
                w.Agents
                |> Array.map (fun a ->
                    if a.Id = agent 2 then
                        { a with Destination = Some { X = 5; Y = 5 } }
                    else
                        a) }

    let r = stepWith [| cmd 1 (agent 2) { X = 0; Y = 0 } |] w

    Assert.Equal<_[]>([| (CommandId.ofInt 1, agent 2, UnableToCommunicate) |], undeliveredIn r)
    Assert.Equal(Some { X = 5; Y = 5 }, (agentOf (agent 2) r.State).Destination)

// --- Order appraisal and typed reasons (TASK-028) --------------------

/// A `16 x 16` world with the listed friendlies `(id, cell, discipline)` and
/// hostiles `(id, cell)`. All agents share line of sight on the open grid, so a
/// hostile within `PerceptionConfig.SightRange` is observed on the first tick
/// (Perception at phase 3, before Appraisal at phase 5).
let private appraisalWorld (friendly: (int * Cell * int) list) (hostile: (int * Cell) list) : WorldState =
    let agents =
        (friendly
         |> List.map (fun (i, c, d) -> { Agent.create (agent i) Friendly c with Discipline = d }))
        @ (hostile |> List.map (fun (i, c) -> Agent.create (agent i) Hostile c))

    match World.create { Width = 16; Height = 16 } 1UL agents with
    | Ok w -> w
    | Error e -> failwith $"unexpected {e}"

let private dispositionOf (id: AgentId) (r: StepResult) = (agentOf id r.State).Disposition

let private appraisedIn (r: StepResult) =
    bodies r
    |> Array.choose (function
        | OrderAppraised(a, c, d) -> Some(a, c, d)
        | _ -> None)

[<Fact>]
let ``a clear-route order with no known threat is Accepted and Destination is written the same tick`` () =
    let w = appraisalWorld [ 0, { X = 1; Y = 5 }, 3 ] []
    let r = stepWith [| cmd 1 (agent 0) { X = 10; Y = 5 } |] w

    Assert.Equal(Some Accepted, dispositionOf (agent 0) r)
    Assert.Equal(Some { X = 10; Y = 5 }, (agentOf (agent 0) r.State).Destination)
    // Navigation (phase 7) runs after Appraisal (phase 5), so the agent steps
    // one cell this tick — exactly the pre-TASK-028 delivered-order behaviour.
    Assert.Equal({ X = 2; Y = 5 }, (agentOf (agent 0) r.State).Position)
    Assert.Contains(OrderAppraised(agent 0, CommandId.ofInt 1, Accepted), bodies r)

[<Fact>]
let ``an order with no known route is Unable NoKnownRoute and no Destination is written`` () =
    // Target (5,4) is ringed by impassable cells; agent 0 starts at (0,0).
    let w = worldWith (impassable [ (4, 4); (6, 4); (5, 3); (5, 5) ])
    let r = stepWith [| cmd 1 (agent 0) { X = 5; Y = 4 } |] w

    Assert.Equal(Some(Unable(NoKnownRoute, [||])), dispositionOf (agent 0) r)
    Assert.Equal(None, (agentOf (agent 0) r.State).Destination)
    // The Navigation phase never sees a Destination, so no MovementBlocked.
    Assert.DoesNotContain("movement-blocked", bodies r |> Array.map (fun b -> $"{b}"))

[<Fact>]
let ``appraisal never returns Accepted after a hard feasibility failure`` () =
    // docs/09 section 2.2.
    let w = worldWith (impassable [ (4, 4); (6, 4); (5, 3); (5, 5) ])
    let r = stepWith [| cmd 1 (agent 0) { X = 5; Y = 4 } |] w

    match dispositionOf (agent 0) r with
    | Some Accepted -> Assert.Fail "appraisal Accepted an order with no known route"
    | Some(Unable(NoKnownRoute, _)) -> ()
    | other -> Assert.Fail($"expected Unable NoKnownRoute, got {other}")

[<Fact>]
let ``an order along a route exposed to a known threat is Refused for a low-discipline agent`` () =
    // Friendly 0 (Discipline 1) at (5,10) ordered east to (14,10); hostile 5 at
    // (9,3) is within SightRange (Chebyshev 7) so it is observed on tick 1, and
    // every route cell lies within AppraisalConfig.ThreatEngagementRange of it.
    let w = appraisalWorld [ 0, { X = 5; Y = 10 }, 1 ] [ 5, { X = 9; Y = 3 } ]
    let r = stepWith [| cmd 1 (agent 0) { X = 14; Y = 10 } |] w

    match dispositionOf (agent 0) r with
    | Some(Refused(RouteTooExposed(Some threat), _)) -> Assert.Equal(agent 5, threat)
    | other -> Assert.Fail($"expected Refused RouteTooExposed (Some agent 5), got {other}")

    Assert.Equal(None, (agentOf (agent 0) r.State).Destination)
    Assert.Equal({ X = 5; Y = 10 }, (agentOf (agent 0) r.State).Position)

[<Fact>]
let ``the same exposed order is Accepted for a high-discipline agent (the G3 divergence)`` () =
    let refuse = appraisalWorld [ 0, { X = 5; Y = 10 }, 1 ] [ 5, { X = 9; Y = 3 } ]
    let accept = appraisalWorld [ 0, { X = 5; Y = 10 }, 6 ] [ 5, { X = 9; Y = 3 } ]
    let order = cmd 1 (agent 0) { X = 14; Y = 10 }

    match dispositionOf (agent 0) (stepWith [| order |] refuse) with
    | Some(Refused(RouteTooExposed _, _)) -> ()
    | other -> Assert.Fail($"low discipline: expected Refused, got {other}")

    Assert.Equal(Some Accepted, dispositionOf (agent 0) (stepWith [| order |] accept))

[<Fact>]
let ``directional cover on the exposed route drops exposure below the threshold`` () =
    // Same exposed order as above (low discipline), but every route cell has
    // authored low cover against fire from the north (where the threat sits),
    // so AppraisalConfig.CoverMitigationPerLevel negates the per-cell pressure.
    let routeCells = [ for x in 5..14 -> { X = x; Y = 10 } ]

    let cover =
        routeCells
        |> List.map (fun c -> ({ Cell = c; Direction = North; Level = 3 }: AuthoredCover))
        |> List.toArray

    let w =
        { appraisalWorld [ 0, { X = 5; Y = 10 }, 1 ] [ 5, { X = 9; Y = 3 } ] with
            Terrain = Terrain.build { Width = 16; Height = 16 } [||] cover }

    let r = stepWith [| cmd 1 (agent 0) { X = 14; Y = 10 } |] w
    Assert.Equal(Some Accepted, dispositionOf (agent 0) r)

[<Fact>]
let ``an Accepted order is not re-appraised on a later idle tick`` () =
    let mutable st = appraisalWorld [ 0, { X = 1; Y = 5 }, 3 ] []
    let mutable appraisals = 0

    for t in 1..5 do
        let cmds = if t = 1 then [| cmd 1 (agent 0) { X = 6; Y = 5 } |] else [||]
        let r = stepWith cmds st
        appraisals <- appraisals + (appraisedIn r).Length
        st <- r.State

    Assert.Equal(1, appraisals)

[<Fact>]
let ``order appraisal is deterministic across two runs`` () =
    // TASK-031: this world's friendly (5,10) and hostile (9,3) are within
    // CombatConfig.WeaponRange and line of sight of each other, so the
    // Combat phase now legitimately draws from the stream too — this fact
    // no longer asserts zero draws, only that both runs draw identically.
    let run () =
        stepWith
            [| cmd 1 (agent 0) { X = 14; Y = 10 } |]
            (appraisalWorld [ 0, { X = 5; Y = 10 }, 1 ] [ 5, { X = 9; Y = 3 } ])

    let r1 = run ()
    let r2 = run ()
    Assert.True(bodies r1 = bodies r2)
    Assert.Equal(r1.StateHash, r2.StateHash)
    Assert.Equal(r1.State.Random.Draws, r2.State.Random.Draws)

// --- Commitment and finite move/hold executor (TASK-030) --------------

let private commitmentsEstablishedIn (r: StepResult) =
    bodies r
    |> Array.choose (function
        | CommitmentEstablished(a, c, t) -> Some(a, c, t)
        | _ -> None)

let private commitmentsCompletedIn (r: StepResult) =
    bodies r
    |> Array.choose (function
        | CommitmentCompleted(a, c, at) -> Some(a, c, at)
        | _ -> None)

[<Fact>]
let ``a fresh Accepted order emits CommitmentEstablished the same tick as OrderAppraised`` () =
    let w = appraisalWorld [ 0, { X = 1; Y = 5 }, 3 ] []
    let r = stepWith [| cmd 1 (agent 0) { X = 10; Y = 5 } |] w

    Assert.Equal(Some Accepted, dispositionOf (agent 0) r)
    Assert.Equal<(AgentId * CommandId * Cell)[]>(
        [| (agent 0, CommandId.ofInt 1, { X = 10; Y = 5 }) |],
        commitmentsEstablishedIn r
    )
    Assert.Empty(commitmentsCompletedIn r)

[<Fact>]
let ``an agent that arrives emits CommitmentCompleted and Order/Disposition clear, exactly as before TASK-030`` () =
    // One cell away: Accepted and arrived within the same tick's movement.
    let mutable st = appraisalWorld [ 0, { X = 1; Y = 5 }, 3 ] []
    let tick1 = stepWith [| cmd 1 (agent 0) { X = 2; Y = 5 } |] st
    st <- tick1.State

    Assert.Equal({ X = 2; Y = 5 }, (agentOf (agent 0) st).Position)
    Assert.Equal(Some Accepted, dispositionOf (agent 0) tick1)
    Assert.Empty(commitmentsCompletedIn tick1)

    // commitmentAndLocalAction runs before Navigation, so it only notices the
    // arrival (Destination cleared by Navigation) one tick later.
    let tick2 = stepWith [||] st

    Assert.Equal<(AgentId * CommandId * Cell)[]>(
        [| (agent 0, CommandId.ofInt 1, { X = 2; Y = 5 }) |],
        commitmentsCompletedIn tick2
    )
    Assert.Equal(None, (agentOf (agent 0) tick2.State).Order)
    Assert.Equal(None, (agentOf (agent 0) tick2.State).Disposition)

[<Fact>]
let ``a second order delivered mid-route emits a fresh CommitmentEstablished with no event for the first`` () =
    let mutable st = appraisalWorld [ 0, { X = 1; Y = 5 }, 3 ] []
    st <- (stepWith [| cmd 1 (agent 0) { X = 14; Y = 5 } |] st).State
    st <- (stepWith [||] st).State

    // Still mid-route toward (14,5).
    Assert.NotEqual({ X = 14; Y = 5 }, (agentOf (agent 0) st).Position)

    let tick3 = stepWith [| cmd 2 (agent 0) { X = 14; Y = 10 } |] st

    Assert.Equal<(AgentId * CommandId * Cell)[]>(
        [| (agent 0, CommandId.ofInt 2, { X = 14; Y = 10 }) |],
        commitmentsEstablishedIn tick3
    )
    Assert.Empty(commitmentsCompletedIn tick3)

[<Fact>]
let ``a Refused or Unable order never emits CommitmentEstablished`` () =
    let refused =
        stepWith
            [| cmd 1 (agent 0) { X = 14; Y = 10 } |]
            (appraisalWorld [ 0, { X = 5; Y = 10 }, 1 ] [ 5, { X = 9; Y = 3 } ])

    match dispositionOf (agent 0) refused with
    | Some(Refused _) -> ()
    | other -> Assert.Fail($"expected Refused, got {other}")

    Assert.Empty(commitmentsEstablishedIn refused)

    let unable =
        stepWith
            [| cmd 1 (agent 0) { X = 5; Y = 4 } |]
            (worldWith (impassable [ (4, 4); (6, 4); (5, 3); (5, 5) ]))

    match dispositionOf (agent 0) unable with
    | Some(Unable _) -> ()
    | other -> Assert.Fail($"expected Unable, got {other}")

    Assert.Empty(commitmentsEstablishedIn unable)

[<Fact>]
let ``an unchanged, already-appraised order emits no commitment event on a later idle tick`` () =
    let mutable st = appraisalWorld [ 0, { X = 1; Y = 5 }, 3 ] []
    let mutable established = 0
    let mutable completed = 0

    for t in 1..3 do
        let cmds = if t = 1 then [| cmd 1 (agent 0) { X = 14; Y = 5 } |] else [||]
        let r = stepWith cmds st
        established <- established + (commitmentsEstablishedIn r).Length
        completed <- completed + (commitmentsCompletedIn r).Length
        st <- r.State

    Assert.Equal(1, established)
    Assert.Equal(0, completed)

[<Fact>]
let ``commitment establish and complete events are deterministic across two runs and draw no randomness`` () =
    let run () =
        stepWith
            [| cmd 1 (agent 0) { X = 2; Y = 5 } |]
            (appraisalWorld [ 0, { X = 1; Y = 5 }, 3 ] [])

    let r1 = run ()
    let r2 = run ()
    Assert.True(bodies r1 = bodies r2)
    Assert.Equal(r1.StateHash, r2.StateHash)
    Assert.Equal(0UL, r1.State.Random.Draws)

[<Fact>]
let ``Commitment.ofAgent matches every reachable (Order, Disposition, Destination) combination`` () =
    let order =
        Some
            { Command = CommandId.ofInt 1
              Intent = MoveTo { X = 5; Y = 5 }
              IssuedAtTick = 0L
              Urgency = Routine
              RiskTolerance = Standard }

    let ofAgent = Commitment.ofAgent [||] [||] { X = 0; Y = 0 }

    // Accepted, still mid-route -> Moving.
    Assert.Equal(
        Moving { Command = CommandId.ofInt 1; Target = { X = 5; Y = 5 } },
        ofAgent order (Some Accepted) (Some { X = 5; Y = 5 })
    )
    // Accepted but already arrived (Destination cleared) -> Holding.
    Assert.Equal(Holding, ofAgent order (Some Accepted) None)
    // Refused / Unable -> Holding regardless of any stale Destination.
    Assert.Equal(Holding, ofAgent order (Some(Refused(NoKnownRoute, [||]))) None)
    Assert.Equal(Holding, ofAgent order (Some(Unable(NoKnownRoute, [||]))) None)
    // Not yet appraised -> Holding.
    Assert.Equal(Holding, ofAgent order None None)
    // No order at all -> Holding.
    Assert.Equal(Holding, ofAgent None None None)

    // Accepted Suppress order (TASK-037) -> Suppressing, keyed by AgentId,
    // never by Destination (a Suppress order never writes one).
    let suppressOrder =
        Some
            { Command = CommandId.ofInt 2
              Intent = Suppress(agent 9)
              IssuedAtTick = 0L
              Urgency = Routine
              RiskTolerance = Standard }

    Assert.Equal(Suppressing { Command = CommandId.ofInt 2; Target = agent 9 }, ofAgent suppressOrder (Some Accepted) None)

    // Accepted Withdraw order (TASK-047, backlog B-030 proper) -> Withdrawing,
    // the MoveCommitment shape but a distinct case.
    let withdrawOrder =
        Some
            { Command = CommandId.ofInt 3
              Intent = Withdraw { X = 0; Y = 0 }
              IssuedAtTick = 0L
              Urgency = Routine
              RiskTolerance = Standard }

    Assert.Equal(
        Withdrawing { Command = CommandId.ofInt 3; Target = { X = 0; Y = 0 } },
        ofAgent withdrawOrder (Some Accepted) (Some { X = 0; Y = 0 })
    )

    // Accepted Assault order, far from the target -> Assaulting ApproachingStart.
    let assaultOrder =
        Some
            { Command = CommandId.ofInt 4
              Intent = Assault { X = 20; Y = 20 }
              IssuedAtTick = 0L
              Urgency = Routine
              RiskTolerance = Standard }

    Assert.Equal(
        Assaulting
            { Command = CommandId.ofInt 4
              Target = { X = 20; Y = 20 }
              Stage = ApproachingStart },
        ofAgent assaultOrder (Some Accepted) (Some { X = 20; Y = 20 })
    )

// --- Hitscan combat and directional cover effects (TASK-031) ----------

let private shotsFiredIn (r: StepResult) =
    bodies r
    |> Array.choose (function
        | ShotFired(shooter, target, hit) -> Some(shooter, target, hit)
        | _ -> None)

[<Fact>]
let ``a friendly and a hostile within range and clear line of sight each fire exactly one shot`` () =
    let b: GridBounds = { Width = 10; Height = 10 }
    let w = perceptionWorld b [ 0, { X = 2; Y = 2 } ] [ 1, { X = 7; Y = 2 } ] (Terrain.empty b)
    let r = stepIdle w

    let pairs = shotsFiredIn r |> Array.map (fun (s, t, _) -> s, t) |> Array.sortBy fst
    Assert.Equal<(AgentId * AgentId)[]>([| (agent 0, agent 1); (agent 1, agent 0) |], pairs)

[<Fact>]
let ``a candidate beyond WeaponRange is never engaged`` () =
    let b: GridBounds = { Width = 20; Height = 20 }
    // Chebyshev distance 8, one past CombatConfig.WeaponRange (7).
    let w = perceptionWorld b [ 0, { X = 0; Y = 0 } ] [ 1, { X = 8; Y = 8 } ] (Terrain.empty b)
    let r = stepIdle w

    Assert.Empty(shotsFiredIn r)
    Assert.Equal(0UL, r.State.Random.Draws)

[<Fact>]
let ``Combat.chooseTarget excludes a candidate blocked by an opaque wall even within range`` () =
    let b: GridBounds = { Width = 10; Height = 10 }
    let terrain = opaqueCells b [ (4, 2) ]
    let shooter = Agent.create (agent 0) Friendly { X = 2; Y = 2 }
    let target = Agent.create (agent 1) Hostile { X = 7; Y = 2 }

    Assert.Equal(None, Combat.chooseTarget terrain shooter [| target |])

[<Fact>]
let ``Combat.chooseTarget picks the nearest candidate, ties broken by ascending id`` () =
    let terrain = Terrain.empty { Width = 20; Height = 20 }
    let shooter = Agent.create (agent 0) Friendly { X = 0; Y = 0 }
    let near = Agent.create (agent 2) Hostile { X = 3; Y = 0 }
    let far = Agent.create (agent 1) Hostile { X = 5; Y = 0 }

    match Combat.chooseTarget terrain shooter [| far; near |] with
    | Some t -> Assert.Equal(agent 2, t.Id)
    | None -> Assert.Fail "expected a target"

    let tiedLow = Agent.create (agent 1) Hostile { X = 3; Y = 0 }
    let tiedHigh = Agent.create (agent 3) Hostile { X = 0; Y = 3 }

    match Combat.chooseTarget terrain shooter [| tiedHigh; tiedLow |] with
    | Some t -> Assert.Equal(agent 1, t.Id)
    | None -> Assert.Fail "expected a target"

[<Fact>]
let ``Combat.hitChance strictly decreases as range increases, all else equal`` () =
    let terrain = Terrain.empty { Width = 20; Height = 20 }
    let shooter = { X = 0; Y = 0 }
    let near = Combat.hitChance terrain shooter { X = 1; Y = 0 }
    let far = Combat.hitChance terrain shooter { X = 5; Y = 0 }
    Assert.True(far < near, $"expected far ({far}) < near ({near})")

[<Fact>]
let ``Combat.hitChance strictly decreases as cover level increases, all else equal`` () =
    let bounds: GridBounds = { Width = 10; Height = 10 }
    let shooter = { X = 0; Y = 0 }
    let target = { X = 5; Y = 0 }
    let noCover = Terrain.empty bounds
    // Fire travels west-to-east, so it arrives at the target's West edge
    // (Appraisal.attackDirection shooter target).
    let covered = Terrain.build bounds [||] [| { Cell = target; Direction = West; Level = 2 } |]

    let chanceNoCover = Combat.hitChance noCover shooter target
    let chanceCovered = Combat.hitChance covered shooter target
    Assert.True(chanceCovered < chanceNoCover, $"expected covered ({chanceCovered}) < uncovered ({chanceNoCover})")

[<Fact>]
let ``Combat.hitChance stays within MinHitChance and MaxHitChance at the extremes`` () =
    let bounds: GridBounds = { Width = 20; Height = 20 }
    let terrain = Terrain.empty bounds

    let farChance = Combat.hitChance terrain { X = 0; Y = 0 } { X = 19; Y = 19 }
    Assert.Equal(CombatConfig.MinHitChance, farChance)

    let zeroRangeChance = Combat.hitChance terrain { X = 5; Y = 5 } { X = 5; Y = 5 }
    Assert.True(zeroRangeChance >= CombatConfig.MinHitChance && zeroRangeChance <= CombatConfig.MaxHitChance)

[<Fact>]
let ``combat is deterministic across two runs and draws exactly once per shot fired`` () =
    let run () =
        let b: GridBounds = { Width = 10; Height = 10 }
        stepIdle (perceptionWorld b [ 0, { X = 2; Y = 2 } ] [ 1, { X = 7; Y = 2 } ] (Terrain.empty b))

    let r1 = run ()
    let r2 = run ()
    Assert.True(bodies r1 = bodies r2)
    Assert.Equal(r1.StateHash, r2.StateHash)
    Assert.Equal(uint64 (shotsFiredIn r1).Length, r1.State.Random.Draws)

    let suppressionOf (r: StepResult) = r.State.Agents |> Array.map (fun a -> a.Suppression)
    Assert.Equal<int[]>(suppressionOf r1, suppressionOf r2)

    // TASK-033: Stress and the SuppressionBand hysteresis latch are also
    // canonical per-tick state now — reproducible from the same inputs.
    let stressOf (r: StepResult) = r.State.Agents |> Array.map (fun a -> a.Stress)
    let bandOf (r: StepResult) = r.State.Agents |> Array.map (fun a -> a.SuppressionBand)
    Assert.Equal<int[]>(stressOf r1, stressOf r2)
    Assert.Equal<bool[]>(bandOf r1, bandOf r2)

[<Fact>]
let ``a hostile with a stale HostileTacticalKnowledge contact for a friendly it can no longer see never fires on it`` () =
    // TASK-034 Decision E: Simulation.combat already draws its candidate list
    // from the shooter's own (same-tick, real-time) VisibleContacts only, a
    // strictly tighter check than anything HostileTacticalKnowledge's
    // stale-tolerant memory could provide — so this proves "the enemy does
    // not target an unobserved player position" without changing Combat.fs.
    let b: GridBounds = { Width = 10; Height = 10 }
    // An opaque wall between the two agents blocks line of sight both ways,
    // so this tick's Perception phase gives the hostile an empty
    // VisibleContacts, exactly the Combat.chooseTarget-blocked-by-a-wall
    // precedent.
    let terrain = opaqueCells b [ (4, 2) ]
    let w = perceptionWorld b [ 0, { X = 2; Y = 2 } ] [ 1, { X = 7; Y = 2 } ] terrain

    let staleContact: Contact =
        { Contact = agent 0
          LastKnownCell = { X = 2; Y = 2 }
          LastSeenTick = 0L
          Confidence = PerceptionConfig.ConfidenceFull }

    let seeded = { w with HostileTacticalKnowledge = [| staleContact |] }
    let r = stepIdle seeded

    Assert.Empty((agentOf (agent 1) r.State).VisibleContacts)
    Assert.Empty(shotsFiredIn r)
    // The stale entry is untouched by Perception / Combat — it only decays
    // through the ordinary Tactical-knowledge phase (Perception.mergeKnowledge).
    Assert.Equal(Some staleContact, hostileContactOf (agent 0) r.State)

// --- Suppression and exposure model (TASK-032) --------------------------

[<Fact>]
let ``Suppression.gain gives a hit strictly more than a miss, all else equal`` () =
    let terrain = Terrain.empty { Width = 10; Height = 10 }
    let shooter = { X = 0; Y = 0 }
    let target = { X = 3; Y = 0 }
    let hit = Suppression.gain terrain shooter target true
    let miss = Suppression.gain terrain shooter target false
    Assert.True(hit > miss, $"expected hit ({hit}) > miss ({miss})")

[<Fact>]
let ``Suppression.gain strictly decreases as cover level increases, floored at 0`` () =
    let bounds: GridBounds = { Width = 10; Height = 10 }
    let shooter = { X = 0; Y = 0 }
    let target = { X = 5; Y = 0 }
    let noCover = Terrain.empty bounds
    // Fire travels west-to-east, so it arrives at the target's West edge
    // (Appraisal.attackDirection shooter target) — the Combat.hitChance precedent.
    let lightCover = Terrain.build bounds [||] [| { Cell = target; Direction = West; Level = 1 } |]
    let heavyCover = Terrain.build bounds [||] [| { Cell = target; Direction = West; Level = 10 } |]

    let gainNoCover = Suppression.gain noCover shooter target true
    let gainLightCover = Suppression.gain lightCover shooter target true
    let gainHeavyCover = Suppression.gain heavyCover shooter target true

    Assert.True(gainLightCover < gainNoCover, $"expected light cover ({gainLightCover}) < no cover ({gainNoCover})")
    Assert.Equal(0, gainHeavyCover)

[<Fact>]
let ``Suppression.raise accumulates two shots and clamps at MaxSuppression`` () =
    Assert.Equal(300, Suppression.raise 100 200)
    Assert.Equal(SuppressionConfig.MaxSuppression, Suppression.raise 900 900)

[<Fact>]
let ``Suppression.decay drops by DecayPerTick, floored at 0`` () =
    Assert.Equal(450, Suppression.decay 500)
    Assert.Equal(0, Suppression.decay 10)
    Assert.Equal(0, Suppression.decay 0)

[<Fact>]
let ``a qualifying shot raises the target's Suppression, net of the same-tick decay`` () =
    let b: GridBounds = { Width = 10; Height = 10 }
    let terrain = Terrain.empty b
    let w = perceptionWorld b [ 0, { X = 2; Y = 2 } ] [ 1, { X = 7; Y = 2 } ] terrain
    let r = stepIdle w

    let positionOf id = (w.Agents |> Array.find (fun a -> a.Id = id)).Position
    let suppressionOf id = (r.State.Agents |> Array.find (fun a -> a.Id = id)).Suppression

    Assert.NotEmpty(shotsFiredIn r)

    for shooter, target, hit in shotsFiredIn r do
        let expected =
            Suppression.gain terrain (positionOf shooter) (positionOf target) hit
            |> Suppression.raise 0
            |> Suppression.decay

        Assert.Equal(expected, suppressionOf target)

[<Fact>]
let ``two shooters engaging the same target the same tick compose their Suppression gains`` () =
    let b: GridBounds = { Width = 10; Height = 10 }
    let terrain = Terrain.empty b
    // Two friendlies flank a lone hostile, all mutually within WeaponRange
    // and clear line of sight, so both friendlies fire on the hostile (its
    // own chooseTarget picks one of them, irrelevant here) while the hostile
    // is the only qualifying target for each.
    let w = perceptionWorld b [ 0, { X = 1; Y = 2 }; 1, { X = 3; Y = 2 } ] [ 2, { X = 7; Y = 2 } ] terrain
    let r = stepIdle w

    let shotsOnHostile = shotsFiredIn r |> Array.filter (fun (_, target, _) -> target = agent 2)
    Assert.Equal(2, shotsOnHostile.Length)

    let positionOf id = (w.Agents |> Array.find (fun a -> a.Id = id)).Position
    let hostileSuppression = (r.State.Agents |> Array.find (fun a -> a.Id = agent 2)).Suppression

    let expected =
        shotsOnHostile
        |> Array.fold (fun acc (shooter, target, hit) -> Suppression.raise acc (Suppression.gain terrain (positionOf shooter) (positionOf target) hit)) 0
        |> Suppression.decay

    Assert.Equal(expected, hostileSuppression)

[<Fact>]
let ``an already-suppressed agent decays by exactly DecayPerTick on a tick with no qualifying target`` () =
    let b: GridBounds = { Width = 10; Height = 10 }
    let w = perceptionWorld b [ 0, { X = 0; Y = 0 } ] [] (Terrain.empty b)
    let suppressed = { w with Agents = w.Agents |> Array.map (fun a -> { a with Suppression = 500 }) }
    let r = stepIdle suppressed

    Assert.Empty(shotsFiredIn r)
    Assert.Equal(450, (r.State.Agents |> Array.find (fun a -> a.Id = agent 0)).Suppression)

[<Fact>]
let ``an agent never shot at stays at Suppression 0 indefinitely`` () =
    let b: GridBounds = { Width = 10; Height = 10 }
    let mutable st = perceptionWorld b [ 0, { X = 0; Y = 0 } ] [] (Terrain.empty b)

    for _ in 1..5 do
        let r = stepIdle st
        Assert.Equal(0, (r.State.Agents |> Array.find (fun a -> a.Id = agent 0)).Suppression)
        st <- r.State

// --- Stress and bounded reappraisal (TASK-033) ---------------------------

let private testOrder (risk: RiskTolerance) (urgency: Urgency) : ReceivedOrder =
    { Command = CommandId.ofInt 1
      Intent = MoveTo { X = 0; Y = 0 }
      IssuedAtTick = 0L
      Urgency = urgency
      RiskTolerance = risk }

[<Fact>]
let ``Stress.gain returns GainPerTick in contact, 0 otherwise`` () =
    Assert.Equal(StressConfig.GainPerTick, Stress.gain true)
    Assert.Equal(0, Stress.gain false)

[<Fact>]
let ``Stress.raise accumulates two gains and clamps at MaxStress`` () =
    Assert.Equal(300, Stress.raise 100 200)
    Assert.Equal(StressConfig.MaxStress, Stress.raise 900 900)

[<Fact>]
let ``Stress.decay drops by DecayPerTick, floored at 0`` () =
    Assert.Equal(500 - StressConfig.DecayPerTick, Stress.decay 500)
    Assert.Equal(0, Stress.decay 10)
    Assert.Equal(0, Stress.decay 0)

[<Fact>]
let ``resolveThreshold drops by exactly SuppressionBandPenalty when suppressed, all else equal`` () =
    let o = testOrder Standard Routine
    let full = Alive Agent.MaxHealth
    let unsuppressed = Appraisal.resolveThreshold 3 0 false full o
    let suppressed = Appraisal.resolveThreshold 3 0 true full o
    Assert.Equal(AppraisalConfig.SuppressionBandPenalty, unsuppressed - suppressed)

[<Fact>]
let ``resolveThreshold drops by MaxStress / StressDivisor at full stress`` () =
    let o = testOrder Standard Routine
    let full = Alive Agent.MaxHealth
    let noStress = Appraisal.resolveThreshold 3 0 false full o
    let maxStress = Appraisal.resolveThreshold 3 StressConfig.MaxStress false full o
    Assert.Equal(StressConfig.MaxStress / AppraisalConfig.StressDivisor, noStress - maxStress)

[<Fact>]
let ``resolveThreshold is floored at 0 even at minimum discipline, cautious risk, full stress, and suppressed`` () =
    let o = testOrder Cautious Routine
    Assert.Equal(0, Appraisal.resolveThreshold 0 StressConfig.MaxStress true (Alive Agent.MaxHealth) o)

[<Fact>]
let ``resolveThreshold drops by exactly MaxHealth / WoundDivisor at near-death, all else equal`` () =
    let o = testOrder Standard Routine
    let full = Appraisal.resolveThreshold 3 0 false (Alive Agent.MaxHealth) o
    let nearDeath = Appraisal.resolveThreshold 3 0 false (Alive 1) o
    Assert.Equal((Agent.MaxHealth - 1) / AppraisalConfig.WoundDivisor, full - nearDeath)

[<Fact>]
let ``an agent with a visible contact gains net Stress this tick; one with none stays at 0`` () =
    let b: GridBounds = { Width = 10; Height = 10 }
    let w = perceptionWorld b [ 0, { X = 0; Y = 0 } ] [ 1, { X = 3; Y = 0 } ] (Terrain.empty b)
    let r = stepIdle w
    let net = StressConfig.GainPerTick - StressConfig.DecayPerTick
    Assert.Equal(net, (agentOf (agent 0) r.State).Stress)
    Assert.Equal(net, (agentOf (agent 1) r.State).Stress)

    let alone = perceptionWorld b [ 0, { X = 0; Y = 0 } ] [] (Terrain.empty b)
    Assert.Equal(0, (agentOf (agent 0) (stepIdle alone).State).Stress)

[<Fact>]
let ``Stress accumulates over continuous contact and decays once contact is lost`` () =
    let b: GridBounds = { Width = 10; Height = 10 }
    let mutable st = perceptionWorld b [ 0, { X = 0; Y = 0 } ] [ 1, { X = 3; Y = 0 } ] (Terrain.empty b)

    for _ in 1..5 do
        st <- (stepIdle st).State

    let afterContact = (agentOf (agent 0) st).Stress
    Assert.Equal(5 * (StressConfig.GainPerTick - StressConfig.DecayPerTick), afterContact)

    // The hostile leaves sight (removed outright, standing in for moving out
    // of range/LOS — VisibleContacts is re-derived from scratch every tick,
    // so this is equivalent from agent 0's perspective).
    let cleared = { st with Agents = st.Agents |> Array.filter (fun a -> a.Id <> agent 1) }
    let after = stepIdle cleared
    Assert.Equal(afterContact - StressConfig.DecayPerTick, (agentOf (agent 0) after.State).Stress)

[<Fact>]
let ``a Refused order is reappraised to Accepted once its only known threat's contact expires`` () =
    // No real hostile agent needed: Appraisal.appraise reads WorldState.TacticalKnowledge
    // directly, not VisibleContacts, so a fabricated stale Contact is enough.
    let w = appraisalWorld [ 0, { X = 1; Y = 5 }, 1 ] []

    let staleContact: Contact =
        { Contact = agent 9
          LastKnownCell = { X = 5; Y = 5 } // sits on the (1,5) -> (10,5) route
          LastSeenTick = 0L
          Confidence = PerceptionConfig.ConfidenceFull }

    let seeded = { w with TacticalKnowledge = [| staleContact |] }
    let tick1 = stepWith [| cmd 1 (agent 0) { X = 10; Y = 5 } |] seeded

    match dispositionOf (agent 0) tick1 with
    | Some(Refused(RouteTooExposed(Some threat), _)) -> Assert.Equal(agent 9, threat)
    | other -> Assert.Fail($"expected Refused RouteTooExposed (Some agent 9), got {other}")

    let mutable st = tick1.State

    for _ in 2..59 do
        st <- (stepIdle st).State

    // Tick 60: 60 - 0 >= PerceptionConfig.ExpireAfter (60), so the contact
    // expires this tick; the knowledge-change reappraisal trigger (TASK-033)
    // resets Disposition to None and Appraisal re-judges the same tick with
    // no known threats left.
    let final = stepIdle st

    Assert.True(
        bodies final
        |> Array.exists (function
            | ContactExpired _ -> true
            | _ -> false),
        "expected a ContactExpired event"
    )

    Assert.Equal(Some Accepted, dispositionOf (agent 0) final)
    Assert.Equal(Some { X = 10; Y = 5 }, (agentOf (agent 0) final.State).Destination)

[<Fact>]
let ``the suppression-band hysteresis latch reappraises an Accepted order when it flips, in either direction`` () =
    let w = appraisalWorld [ 0, { X = 1; Y = 5 }, 3 ] []
    let tick1 = stepWith [| cmd 1 (agent 0) { X = 10; Y = 5 } |] w
    Assert.Equal(Some Accepted, dispositionOf (agent 0) tick1)
    Assert.False((agentOf (agent 0) tick1.State).SuppressionBand)

    // Bump Suppression above the enter threshold directly (standing in for
    // an earlier tick's combat) and step once: the latch flips true, a
    // material trigger — even though the route stays safe (0 exposure, so
    // the outcome stays Accepted: resolveThreshold's floor at 0 guarantees a
    // fully clear route is never refused for stress or suppression alone).
    let withSuppression suppression (state: WorldState) =
        { state with
            Agents =
                state.Agents
                |> Array.map (fun a -> if a.Id = agent 0 then { a with Suppression = suppression } else a) }

    let entered = stepIdle (withSuppression AppraisalConfig.SuppressionBandEnter tick1.State)
    Assert.True((agentOf (agent 0) entered.State).SuppressionBand)
    Assert.Equal(1, (appraisedIn entered).Length)
    Assert.Equal(Some Accepted, dispositionOf (agent 0) entered)

    // No further trigger on a quiet tick with the band unchanged.
    let quiet = stepIdle entered.State
    Assert.Empty(appraisedIn quiet)

    // Drop Suppression to the exit threshold and step again: the latch flips
    // back false, another material trigger.
    let exited = stepIdle (withSuppression AppraisalConfig.SuppressionBandExit quiet.State)
    Assert.False((agentOf (agent 0) exited.State).SuppressionBand)
    Assert.Equal(1, (appraisedIn exited).Length)

// --- Suppress order and threat-suppression reappraisal (TASK-037) --------

let private suppressCmd (id: int) (recipient: AgentId) (target: AgentId) =
    Command.suppress (CommandId.ofInt id) 0L recipient target

[<Fact>]
let ``a Suppress order naming an unknown contact is Unable TargetNotKnown`` () =
    let w = appraisalWorld [ 0, { X = 5; Y = 10 }, 3 ] []
    let r = stepWith [| suppressCmd 1 (agent 0) (agent 9) |] w

    Assert.Equal(Some(Unable(TargetNotKnown, [||])), dispositionOf (agent 0) r)
    let a0 = agentOf (agent 0) r.State
    Assert.Equal(None, a0.Destination)
    Assert.Equal(Holding, Commitment.ofAgent [||] [||] a0.Position a0.Order a0.Disposition a0.Destination)

[<Fact>]
let ``a Suppress order naming a known contact is Accepted with no Destination and a Suppressing commitment`` () =
    let w = appraisalWorld [ 0, { X = 5; Y = 10 }, 3 ] [ 9, { X = 9; Y = 3 } ]
    let r = stepWith [| suppressCmd 1 (agent 0) (agent 9) |] w

    Assert.Equal(Some Accepted, dispositionOf (agent 0) r)
    let a0 = agentOf (agent 0) r.State
    Assert.Equal(None, a0.Destination)
    Assert.Equal({ X = 5; Y = 10 }, a0.Position) // holds position, never moves
    Assert.Equal(
        Suppressing { Command = CommandId.ofInt 1; Target = agent 9 },
        Commitment.ofAgent [||] [||] a0.Position a0.Order a0.Disposition a0.Destination
    )

[<Fact>]
let ``routeExposure zeroes a threat's contribution once it is in suppressedThreats`` () =
    let terrain = Terrain.empty { Width = 20; Height = 20 }

    let threat: Contact =
        { Contact = agent 9
          LastKnownCell = { X = 5; Y = 5 }
          LastSeenTick = 0L
          Confidence = PerceptionConfig.ConfidenceFull }

    let routeCells = [| for x in 0..10 -> { X = x; Y = 5 } |]

    let exposedPressure, exposedTop = Appraisal.routeExposure terrain [| threat |] [||] routeCells
    Assert.True(exposedPressure > 0)
    Assert.Equal(Some(agent 9), exposedTop)

    let suppressedPressure, suppressedTop = Appraisal.routeExposure terrain [| threat |] [| agent 9 |] routeCells
    Assert.Equal(0, suppressedPressure)
    Assert.Equal(None, suppressedTop)

[<Fact>]
let ``a threat's SuppressionBand flip reappraises a DIFFERENT agent's Refused order to Accepted`` () =
    // The "exposed route ... Refused" scenario exactly, but the trigger under
    // test is a THIRD agent's threat-suppression flip, not agent 0's own
    // stress/suppression (the suppression-band trigger above only covers the
    // appraising agent's own state).
    let w = appraisalWorld [ 0, { X = 5; Y = 10 }, 1 ] [ 5, { X = 9; Y = 3 } ]
    let tick1 = stepWith [| cmd 1 (agent 0) { X = 14; Y = 10 } |] w

    match dispositionOf (agent 0) tick1 with
    | Some(Refused(RouteTooExposed(Some threat), _)) -> Assert.Equal(agent 5, threat)
    | other -> Assert.Fail($"expected Refused RouteTooExposed (Some agent 5), got {other}")

    // Stand in for an earlier tick's combat already having raised the
    // threat's Suppression past SuppressionBandEnter, before its stored
    // SuppressionBand latch has been recomputed (the real-play ordering:
    // Combat writes Suppression; SuppressionBand is only recomputed at the
    // NEXT tick's Appraisal, docs/05 sections 14/15) — the identical
    // `withSuppression` mutation the suppression-band-latch test above uses,
    // applied to the THREAT (agent 5) instead of the appraising agent.
    let suppressed =
        { tick1.State with
            Agents =
                tick1.State.Agents
                |> Array.map (fun a ->
                    if a.Id = agent 5 then
                        { a with Suppression = AppraisalConfig.SuppressionBandEnter }
                    else
                        a) }

    let tick2 = stepIdle suppressed

    Assert.True((agentOf (agent 5) tick2.State).SuppressionBand)
    Assert.Equal(Some Accepted, dispositionOf (agent 0) tick2)
    Assert.Equal(Some { X = 14; Y = 10 }, (agentOf (agent 0) tick2.State).Destination)

[<Fact>]
let ``a Suppressing agent fires at its named target even when a nearer contact is also visible`` () =
    let b: GridBounds = { Width = 20; Height = 20 }

    let w =
        perceptionWorld b [ 0, { X = 0; Y = 0 } ] [ 1, { X = 3; Y = 0 }; 2, { X = 6; Y = 0 } ] (Terrain.empty b)

    let r = stepWith [| suppressCmd 1 (agent 0) (agent 2) |] w

    let fromShooter0 =
        bodies r
        |> Array.choose (function
            | ShotFired(shooter, target, _) when shooter = agent 0 -> Some target
            | _ -> None)

    Assert.Equal<AgentId[]>([| agent 2 |], fromShooter0)

// --- Order queue: stacking and cancellation (TASK-044, backlog B-051) ----

let private queuedCmd (id: int) (recipient: AgentId) (dest: Cell) =
    Command.queued (cmd id recipient dest)

let private cancelCmd (id: int) (recipient: AgentId) (target: CommandId) =
    Command.cancel (CommandId.ofInt id) 0L recipient target

[<Fact>]
let ``an appended order queues behind the active one, activates on fulfilment, and is judged the following tick`` () =
    let a = agent 0 // starts at (0,0)

    // Both issued the same tick: cmd 1 (Replace) becomes active; cmd 2
    // (Append) queues behind it, since the recipient already holds an order
    // by the time Communication processes cmd 2 (ascending command id).
    let tick1 =
        stepWith [| cmd 1 a { X = 2; Y = 0 }; queuedCmd 2 a { X = 5; Y = 0 } |] (world ())

    Assert.Contains(OrderQueued(CommandId.ofInt 2, a), bodies tick1)
    Assert.Equal(1, (agentOf a tick1.State).OrderQueue.Length)
    Assert.Equal({ X = 1; Y = 0 }, (agentOf a tick1.State).Position)

    let tick2 = stepIdle tick1.State
    Assert.Equal({ X = 2; Y = 0 }, (agentOf a tick2.State).Position) // arrives

    // Fulfilment tick: the queue head is promoted, but NOT judged this same
    // tick (Appraisal already ran before commitmentAndLocalAction promotes
    // it) -- position holds at (2,0), Destination stays None.
    let tick3 = stepIdle tick2.State
    let a3 = agentOf a tick3.State
    Assert.Contains(CommitmentCompleted(a, CommandId.ofInt 1, { X = 2; Y = 0 }), bodies tick3)
    Assert.Equal({ X = 2; Y = 0 }, a3.Position)
    Assert.Equal(None, a3.Destination)
    Assert.Equal(0, a3.OrderQueue.Length)
    Assert.Equal(Some(CommandId.ofInt 2), a3.Order |> Option.map (fun o -> o.Command))
    Assert.Equal(None, a3.Disposition)

    // Next tick: the promoted order is finally appraised and takes effect.
    let tick4 = stepIdle tick3.State
    Assert.Contains(OrderAppraised(a, CommandId.ofInt 2, Accepted), bodies tick4)
    Assert.Contains(CommitmentEstablished(a, CommandId.ofInt 2, { X = 5; Y = 0 }), bodies tick4)
    Assert.Equal({ X = 3; Y = 0 }, (agentOf a tick4.State).Position)

[<Fact>]
let ``a Replace order clears both the active order and anything already queued`` () =
    let a = agent 0

    let tick1 = stepWith [| cmd 1 a { X = 2; Y = 0 } |] (world ())

    let tick2 =
        stepWith [| queuedCmd 2 a { X = 5; Y = 0 }; cmd 3 a { X = 7; Y = 0 } |] tick1.State

    let a2 = agentOf a tick2.State
    Assert.Equal(0, a2.OrderQueue.Length)
    Assert.Equal(Some(CommandId.ofInt 3), a2.Order |> Option.map (fun o -> o.Command))
    Assert.Equal(Some Accepted, a2.Disposition)
    Assert.Equal(Some { X = 7; Y = 0 }, a2.Destination)

[<Fact>]
let ``cancelling the active order promotes the queue head, judged the same tick`` () =
    let a = agent 0

    let tick1 =
        stepWith [| cmd 1 a { X = 2; Y = 0 }; queuedCmd 2 a { X = 5; Y = 0 } |] (world ())

    Assert.Equal(1, (agentOf a tick1.State).OrderQueue.Length)

    let tick2 = stepWith [| cancelCmd 3 a (CommandId.ofInt 1) |] tick1.State

    Assert.Contains(OrderCancelled(CommandId.ofInt 1, a, true), bodies tick2)
    // Unlike the fulfilment path, a Cancel-driven promotion happens in
    // Communication, BEFORE this same tick's Appraisal runs, so the
    // promoted order is judged (and, here, acted on) the same tick.
    Assert.Contains(OrderAppraised(a, CommandId.ofInt 2, Accepted), bodies tick2)
    let a2 = agentOf a tick2.State
    Assert.Equal(0, a2.OrderQueue.Length)
    Assert.Equal(Some(CommandId.ofInt 2), a2.Order |> Option.map (fun o -> o.Command))
    Assert.Equal(Some { X = 5; Y = 0 }, a2.Destination)

[<Fact>]
let ``cancelling a queued order removes only that entry, leaving the active order and the rest of the queue untouched`` () =
    let a = agent 0

    // A longer active route (6 cells) than the other cancel test's, so the
    // active order is still genuinely in flight -- not yet arrived and
    // recognised as fulfilled -- by the tick the cancel is issued.
    let tick1 = stepWith [| cmd 1 a { X = 6; Y = 0 } |] (world ())

    let tick2 =
        stepWith [| queuedCmd 2 a { X = 5; Y = 0 }; queuedCmd 3 a { X = 6; Y = 1 } |] tick1.State

    Assert.Equal(2, (agentOf a tick2.State).OrderQueue.Length)
    Assert.NotEqual(None, (agentOf a tick2.State).Destination) // still en route

    let tick3 = stepWith [| cancelCmd 4 a (CommandId.ofInt 2) |] tick2.State

    Assert.Contains(OrderCancelled(CommandId.ofInt 2, a, false), bodies tick3)
    let a3 = agentOf a tick3.State
    // The still-active order (cmd 1) and the remaining queued entry (cmd 3)
    // are both untouched.
    Assert.Equal(Some(CommandId.ofInt 1), a3.Order |> Option.map (fun o -> o.Command))
    Assert.Equal<CommandId[]>([| CommandId.ofInt 3 |], a3.OrderQueue |> List.map (fun o -> o.Command) |> List.toArray)

[<Fact>]
let ``cancelling an unknown command id is rejected explicitly, not silently ignored`` () =
    let a = agent 0
    let tick1 = stepWith [| cmd 1 a { X = 2; Y = 0 } |] (world ())

    let tick2 = stepWith [| cancelCmd 2 a (CommandId.ofInt 999) |] tick1.State

    Assert.Contains(CommandRejected(CommandId.ofInt 2, UnknownTargetCommand(a, CommandId.ofInt 999)), bodies tick2)
    // The genuinely active order is completely unaffected by the rejected cancel.
    Assert.Equal(Some(CommandId.ofInt 1), (agentOf a tick2.State).Order |> Option.map (fun o -> o.Command))

[<Fact>]
let ``cancelling the active order with an empty queue clears its Destination too, so the agent actually stops`` () =
    let a = agent 0
    // A long route (7 cells), cancelled while genuinely mid-flight.
    let tick1 = stepWith [| cmd 1 a { X = 7; Y = 0 } |] (world ())
    let tick2 = stepIdle tick1.State
    Assert.Equal({ X = 2; Y = 0 }, (agentOf a tick2.State).Position)

    let tick3 = stepWith [| cancelCmd 2 a (CommandId.ofInt 1) |] tick2.State
    let a3 = agentOf a tick3.State
    Assert.Equal(None, a3.Order)
    Assert.Equal(None, a3.Destination)

    // Left alone, the agent genuinely stays put -- Destination does not
    // silently survive the cancel and keep driving movement.
    let tick4 = stepIdle tick3.State
    Assert.Equal({ X = 2; Y = 0 }, (agentOf a tick4.State).Position)

// --- Casualties, incapacitation, leadership succession, and squad failure
// (TASK-045, backlog B-031) --------------------------------------------

[<Fact>]
let ``Casualty.wound reduces health by WoundPerHit and transitions to Incapacitated at or below zero`` () =
    Assert.Equal(Alive(Agent.MaxHealth - CasualtyConfig.WoundPerHit), Casualty.wound (Alive Agent.MaxHealth))
    Assert.Equal(Incapacitated CasualtyConfig.BleedOutTicks, Casualty.wound (Alive CasualtyConfig.WoundPerHit))
    Assert.Equal(Incapacitated CasualtyConfig.BleedOutTicks, Casualty.wound (Alive 1))
    // Never actually reached in practice (Combat only ever targets an Alive
    // agent), but the leaf itself stays total rather than crashing.
    Assert.Equal(Incapacitated 5, Casualty.wound (Incapacitated 5))
    Assert.Equal(Dead, Casualty.wound Dead)

[<Fact>]
let ``Casualty.tickBleedOut counts down to Dead at exactly one tick remaining, and is a no-op on Alive or Dead`` () =
    Assert.Equal(Incapacitated 2, Casualty.tickBleedOut (Incapacitated 3))
    Assert.Equal(Dead, Casualty.tickBleedOut (Incapacitated 1))
    Assert.Equal(Alive 500, Casualty.tickBleedOut (Alive 500))
    Assert.Equal(Dead, Casualty.tickBleedOut Dead)

[<Fact>]
let ``Casualty.currentLeader is the lowest-id Alive friendly, None once every friendly is down, and ignores hostiles`` () =
    let friendly i vitals = { Agent.create (agent i) Friendly { X = i; Y = 0 } with Vitals = vitals }
    let hostileAt i = Agent.create (agent i) Hostile { X = i; Y = 1 }

    Assert.Equal(
        Some(agent 1),
        Casualty.currentLeader [| friendly 3 (Alive 1000); friendly 1 (Alive 1000); hostileAt 0 |]
    )
    Assert.Equal(Some(agent 3), Casualty.currentLeader [| friendly 3 (Alive 1000); friendly 1 (Incapacitated 10) |])
    Assert.Equal(None, Casualty.currentLeader [| friendly 1 (Incapacitated 10); friendly 3 Dead |])

let private worldOf (agents: AgentState list) : WorldState =
    match World.create { Width = 10; Height = 10 } 1UL agents with
    | Ok w -> w
    | Error e -> failwith $"unexpected {e}"

[<Fact>]
let ``an Incapacitated or Dead agent never fires and is never targeted by an in-range hostile`` () =
    for downVitals in [ Incapacitated 10; Dead ] do
        let b: GridBounds = { Width = 10; Height = 10 }
        let w = perceptionWorld b [ 0, { X = 2; Y = 2 } ] [ 1, { X = 4; Y = 2 } ] (Terrain.empty b)

        let downed =
            { w with
                Agents = w.Agents |> Array.map (fun a -> if a.Id = agent 0 then { a with Vitals = downVitals } else a) }

        let r = stepIdle downed
        Assert.Empty(shotsFiredIn r)
        Assert.Equal(0UL, r.State.Random.Draws)

[<Fact>]
let ``an Incapacitated agent with a live Destination never moves, and its Destination is left inert, not cleared`` () =
    let a = agent 0 // starts at (0,0) in world ()
    let w = world ()

    let downed =
        { w with
            Agents =
                w.Agents
                |> Array.map (fun ag ->
                    if ag.Id = a then
                        { ag with Vitals = Incapacitated 10; Destination = Some { X = 5; Y = 0 } }
                    else
                        ag) }

    let r = stepIdle downed
    Assert.Equal({ X = 0; Y = 0 }, (agentOf a r.State).Position)
    Assert.Equal(Some { X = 5; Y = 0 }, (agentOf a r.State).Destination)
    Assert.DoesNotContain(MovementCompleted(a, { X = 5; Y = 0 }), bodies r)

// --- Perception stops observing a downed hostile (TASK-055, backlog B-062) --

[<Fact>]
let ``an Incapacitated or Dead hostile is no longer freshly observed, even still in range and in line of sight`` () =
    // Before this fix, a corpse or downed hostile was never removed from
    // `WorldState.Agents` (TASK-045) and `Perception.visibleContactsFor`
    // never checked `Vitals`, so it stayed a permanent full-`Confidence`
    // `Contact` forever -- keeping `Appraisal`'s route-exposure model
    // treating it as a live threat indefinitely (Dave's own live-play
    // report). The fix lets an existing `Contact` on it age and expire
    // through the ordinary `StaleAfter`/`ExpireAfter` bands instead, the
    // same path a threat that moved out of sight already takes.
    for downVitals in [ Incapacitated 10; Dead ] do
        let b: GridBounds = { Width = 16; Height = 8 }
        let w = perceptionWorld b [ 0, { X = 1; Y = 1 } ] [ 1, { X = 6; Y = 1 } ] (Terrain.empty b)

        // Baseline: an Alive hostile in range and LOS is genuinely observed.
        let baseline = stepIdle w
        Assert.Equal<AgentId[]>([| agent 1 |], (agentOf (agent 0) baseline.State).VisibleContacts)
        Assert.Equal(1L, (contactOf (agent 1) baseline.State).Value.LastSeenTick)

        // The hostile goes down mid-knowledge, still at the identical cell,
        // still within SightRange, still with clear line of sight.
        let downed =
            { baseline.State with
                Agents =
                    baseline.State.Agents
                    |> Array.map (fun a -> if a.Id = agent 1 then { a with Vitals = downVitals } else a) }

        let r = stepIdle downed
        Assert.Empty((agentOf (agent 0) r.State).VisibleContacts)
        // The existing Contact is retained (aging normally, not refreshed):
        // LastSeenTick stays pinned at the last tick it was actually
        // Alive-and-seen, one tick behind the current tick.
        Assert.Equal(1L, (contactOf (agent 1) r.State).Value.LastSeenTick)
        Assert.Equal(2L, r.State.Tick)

[<Fact>]
let ``an order addressed to a Dead or Incapacitated agent appraises Unable CriticallyWounded, writing no Destination`` () =
    for downVitals in [ Incapacitated 10; Dead ] do
        let w = appraisalWorld [ 0, { X = 1; Y = 5 }, 3 ] []

        let downed =
            { w with
                Agents = w.Agents |> Array.map (fun a -> if a.Id = agent 0 then { a with Vitals = downVitals } else a) }

        let r = stepWith [| cmd 1 (agent 0) { X = 6; Y = 5 } |] downed
        Assert.Equal(Some(Unable(CriticallyWounded, [||])), dispositionOf (agent 0) r)
        Assert.Equal(None, (agentOf (agent 0) r.State).Destination)

[<Fact>]
let ``a wound taken this tick triggers a fresh appraisal on the very next tick, and the flag is spent`` () =
    let mutable st = appraisalWorld [ 0, { X = 1; Y = 5 }, 3 ] []
    let tick1 = stepWith [| cmd 1 (agent 0) { X = 10; Y = 5 } |] st
    Assert.Equal(Some Accepted, dispositionOf (agent 0) tick1)
    st <- tick1.State

    // Baseline: an ordinary idle tick, with none of the other reappraisal
    // triggers firing, does NOT reappraise (the existing "no re-appraisal on
    // a later idle tick" precedent) -- confirms the fresh appraisal below is
    // caused by RecentlyWounded, not by something incidental to idling.
    Assert.Empty(appraisedIn (stepIdle st))

    // Simulate a wound Combat took this tick (RecentlyWounded latched true,
    // exactly as Simulation.combat sets it on a qualifying hit) without
    // otherwise touching anything else appraisal-relevant.
    let wounded =
        { st with
            Agents = st.Agents |> Array.map (fun a -> if a.Id = agent 0 then { a with RecentlyWounded = true } else a) }

    let r = stepIdle wounded
    Assert.Equal<(AgentId * CommandId * OrderDisposition)[]>([| (agent 0, CommandId.ofInt 1, Accepted) |], appraisedIn r)
    Assert.False((agentOf (agent 0) r.State).RecentlyWounded)

[<Fact>]
let ``leadership transfers the same tick Combat incapacitates the current leader, not the tick after`` () =
    // Regression proof for the TASK-045 stateConsequences bug: diffing
    // leadership against a snapshot taken at THIS phase's own entry (after
    // Combat, which runs earlier, has already mutated s.Agents) can only ever
    // see stateConsequences's own bleed-out-to-Dead transitions, never one
    // Combat itself just caused. stateConsequences instead diffs against
    // StepState.InitialLeader, the true start-of-tick snapshot taken before
    // any phase runs.
    let leader = { Agent.create (agent 0) Friendly { X = 2; Y = 2 } with Vitals = Alive 1 }
    let wingman = Agent.create (agent 1) Friendly { X = 0; Y = 0 }
    let hostile = Agent.create (agent 2) Hostile { X = 4; Y = 2 }
    let mutable st = worldOf [ leader; wingman; hostile ]
    let mutable transferred = false
    let mutable ticks = 0

    while not transferred && ticks < 40 do
        let r = stepIdle st

        if Array.contains (AgentIncapacitated(agent 0, { X = 2; Y = 2 })) (bodies r) then
            Assert.Contains(LeadershipTransferred(Some(agent 0), Some(agent 1)), bodies r)
            transferred <- true

        st <- r.State
        ticks <- ticks + 1

    Assert.True(transferred, "agent 0 (Alive 1) was never incapacitated within 40 ticks of hostile fire")

[<Fact>]
let ``SquadFailure fires exactly once, the tick every friendly becomes non-Alive, and does not halt stepping`` () =
    // s.InitialFriendlyAlive is the TRUE start-of-tick baseline, so the only
    // way to genuinely exercise "the tick it first becomes true" is to drive
    // both friendlies down through real Combat, the leadership test's own
    // precedent -- directly injecting a pre-Incapacitated friendly would
    // already read as non-Alive before the tick starts and never trip the
    // latch at all.
    // Heavy directional cover on the hostile's own West edge (facing both
    // friendlies) keeps their return fire near CombatConfig.MinHitChance
    // without touching the hostile's own, uncovered shots at them -- so the
    // one-hit-down friendlies (Alive 1) reliably go down well before the
    // hostile's 1000 health does, keeping this bounded and not a coin flip.
    let hostilePos = { X = 5; Y = 2 }
    let a0 = { Agent.create (agent 0) Friendly { X = 2; Y = 2 } with Vitals = Alive 1 }
    let a1 = { Agent.create (agent 1) Friendly { X = 3; Y = 2 } with Vitals = Alive 1 }
    let hostile = Agent.create (agent 2) Hostile hostilePos
    let cover: AuthoredCover[] = [| { Cell = hostilePos; Direction = West; Level = 3 } |]

    let mutable st =
        { worldOf [ a0; a1; hostile ] with Terrain = Terrain.build { Width = 10; Height = 10 } [||] cover }

    let mutable failures = 0
    let mutable ticks = 0

    let bothDown (w: WorldState) =
        not (Casualty.isAlive (agentOf (agent 0) w).Vitals) && not (Casualty.isAlive (agentOf (agent 1) w).Vitals)

    while not (bothDown st) && ticks < 200 do
        let r = stepIdle st

        if Array.contains SquadFailure (bodies r) then
            failures <- failures + 1
            Assert.True(bothDown r.State, "SquadFailure fired before every friendly was actually non-Alive")

        st <- r.State
        ticks <- ticks + 1

    Assert.True(bothDown st, "both friendlies were never brought down within 200 ticks")
    Assert.Equal(1, failures)

    // A signal event only: stepping continues normally afterwards.
    let after = stepIdle st
    Assert.DoesNotContain(SquadFailure, bodies after)

// --- Assault, Withdraw, Hold order executors and ammunition (TASK-047, backlog B-030 proper) ----

let private contactAt (id: int) (cell: Cell) : Contact =
    { Contact = agent id
      LastKnownCell = cell
      LastSeenTick = 0L
      Confidence = PerceptionConfig.ConfidenceFull }

// --- Commitment.assaultStage: pure per-tick derivation, all four states ---

[<Fact>]
let ``assaultStage is ApproachingStart while beyond AssaultStartRange, regardless of threats`` () =
    let target = { X = 10; Y = 10 }
    let position = { X = 0; Y = 0 }
    let threats = [| contactAt 9 target |]

    Assert.Equal(ApproachingStart, Commitment.assaultStage threats [||] position target)

[<Fact>]
let ``assaultStage is AwaitingSupport within AssaultStartRange with an unsuppressed threat near the target`` () =
    let target = { X = 10; Y = 10 }
    let position = { X = 9; Y = 10 } // Chebyshev 1 <= AssaultStartRange
    let threats = [| contactAt 9 target |] // Chebyshev 0 <= ThreatEngagementRange, unsuppressed

    Assert.Equal(AwaitingSupport, Commitment.assaultStage threats [||] position target)

[<Fact>]
let ``assaultStage is Advancing within AssaultStartRange once the blocking threat is suppressed`` () =
    let target = { X = 10; Y = 10 }
    let position = { X = 9; Y = 10 }
    let threats = [| contactAt 9 target |]

    Assert.Equal(Advancing, Commitment.assaultStage threats [| agent 9 |] position target)

[<Fact>]
let ``assaultStage is ClearingThreat at the target with an unsuppressed threat within AssaultClearRadius`` () =
    let target = { X = 10; Y = 10 }
    let threats = [| contactAt 9 target |] // Chebyshev 0 <= AssaultClearRadius

    Assert.Equal(ClearingThreat, Commitment.assaultStage threats [||] target target)

[<Fact>]
let ``assaultStage is Advancing at the target once the last known threat is cleared`` () =
    let target = { X = 10; Y = 10 }
    Assert.Equal(Advancing, Commitment.assaultStage [||] [||] target target)

// --- Withdraw: resolve bonus lets a route Refused for MoveTo Accept ---

[<Fact>]
let ``a Withdraw order Accepts a route a MoveTo order to the same target Refuses`` () =
    // The "Refused for a low-discipline agent" geometry: friendly (5,10),
    // hostile at (9,3), route to (14,10) exposed at every cell (pressure
    // 100). Discipline 4 -> base threshold 80 (Refused, < 100);
    // AppraisalConfig.WithdrawResolveBonus (30) lifts it to 110 (Accepted).
    let w = appraisalWorld [ 0, { X = 5; Y = 10 }, 4 ] [ 5, { X = 9; Y = 3 } ]

    let moveTo = stepWith [| cmd 1 (agent 0) { X = 14; Y = 10 } |] w

    match dispositionOf (agent 0) moveTo with
    | Some(Refused(RouteTooExposed _, _)) -> ()
    | other -> Assert.Fail($"expected the plain MoveTo to Refuse, got {other}")

    let withdraw =
        stepWith [| Command.withdraw (CommandId.ofInt 1) 0L (agent 0) { X = 14; Y = 10 } |] w

    Assert.Equal(Some Accepted, dispositionOf (agent 0) withdraw)

    match
        Commitment.ofAgent
            [||]
            [||]
            { X = 0; Y = 0 }
            (agentOf (agent 0) withdraw.State).Order
            (agentOf (agent 0) withdraw.State).Disposition
            (agentOf (agent 0) withdraw.State).Destination
    with
    | Withdrawing wc -> Assert.Equal({ X = 14; Y = 10 }, wc.Target)
    | other -> Assert.Fail($"expected Withdrawing, got {other}")

// --- Assault: the resolve penalty Refuses a route a MoveTo order Accepts ---

[<Fact>]
let ``an Assault order Refuses a route a MoveTo order to the same target Accepts`` () =
    // Discipline 6 -> base threshold 110 (Accepted, the same exposure-100
    // geometry above -- the "high-discipline" precedent);
    // AppraisalConfig.AssaultResolvePenalty (30) drops it to 80 (Refused).
    let w = appraisalWorld [ 0, { X = 5; Y = 10 }, 6 ] [ 5, { X = 9; Y = 3 } ]

    let moveTo = stepWith [| cmd 1 (agent 0) { X = 14; Y = 10 } |] w
    Assert.Equal(Some Accepted, dispositionOf (agent 0) moveTo)

    let assault =
        stepWith [| Command.assault (CommandId.ofInt 1) 0L (agent 0) { X = 14; Y = 10 } |] w

    match dispositionOf (agent 0) assault with
    | Some(Refused(RouteTooExposed(Some threat), _)) -> Assert.Equal(agent 5, threat)
    | other -> Assert.Fail($"expected the Assault to Refuse, got {other}")

// --- Hold: may redirect to a lower-pressure nearby cell ------------------

[<Fact>]
let ``a Hold order with no known threat walks straight to the authored area`` () =
    let w = appraisalWorld [ 0, { X = 0; Y = 5 }, 3 ] []
    let r = stepWith [| Command.hold (CommandId.ofInt 1) 0L (agent 0) { X = 5; Y = 5 } |] w

    Assert.Equal(Some Accepted, dispositionOf (agent 0) r)
    Assert.Equal(Some { X = 5; Y = 5 }, (agentOf (agent 0) r.State).Destination)

[<Fact>]
let ``a Hold order redirects to a covered neighbour when the authored area is exposed`` () =
    // Threat at (5,0), area (5,5): attackDirection is North (dy < 0, |dy| >
    // |dx| = 0), so authored North-facing cover on (5,4) alone (not the area
    // itself) zeroes that one neighbour's pressure (10 - 3*4 = -2 -> 0)
    // while the area and every other candidate cell in HoldCoverSearchRadius
    // stays at 10 -- bestCoverNear must pick (5,4) uniquely.
    let cover: AuthoredCover[] = [| { Cell = { X = 5; Y = 4 }; Direction = North; Level = 3 } |]

    let w =
        { appraisalWorld [ 0, { X = 0; Y = 5 }, 3 ] [ 5, { X = 5; Y = 0 } ] with
            Terrain = Terrain.build { Width = 16; Height = 16 } [||] cover }

    let r = stepWith [| Command.hold (CommandId.ofInt 1) 0L (agent 0) { X = 5; Y = 5 } |] w

    Assert.Equal(Some Accepted, dispositionOf (agent 0) r)
    Assert.Equal(Some { X = 5; Y = 4 }, (agentOf (agent 0) r.State).Destination)

// --- Ammo: pure Ammo.fs functions -----------------------------------------

[<Fact>]
let ``Ammo.canFire is true only for a Ready state with a round chambered`` () =
    Assert.True(Ammo.canFire (Ready(1, 0)))
    Assert.False(Ammo.canFire (Ready(0, 30)))
    Assert.False(Ammo.canFire (Reloading(30, 5)))

[<Fact>]
let ``Ammo.fire consumes one round and is a no-op when the magazine is already empty`` () =
    Assert.Equal(Ready(4, 10), Ammo.fire (Ready(5, 10)))
    Assert.Equal(Ready(0, 10), Ammo.fire (Ready(0, 10)))
    Assert.Equal(Reloading(10, 5), Ammo.fire (Reloading(10, 5)))

[<Fact>]
let ``Ammo.tick starts a reload once empty with reserve, then refills on completion`` () =
    let started, justStarted, justCompleted = Ammo.tick (Ready(0, 10))
    Assert.Equal(Reloading(10, AmmoConfig.ReloadTicks), started)
    Assert.True(justStarted)
    Assert.False(justCompleted)

    let counting, s2, c2 = Ammo.tick (Reloading(10, 5))
    Assert.Equal(Reloading(10, 4), counting)
    Assert.False(s2)
    Assert.False(c2)

    let refilled, s3, c3 = Ammo.tick (Reloading(10, 1))
    Assert.Equal(Ready(10, 0), refilled) // min MagazineSize 10 = 10
    Assert.False(s3)
    Assert.True(c3)

    // Truly empty (no reserve) and an untouched Ready state are both inert.
    let inert, s4, c4 = Ammo.tick (Ready(0, 0))
    Assert.Equal(Ready(0, 0), inert)
    Assert.False(s4)
    Assert.False(c4)

[<Fact>]
let ``Ammo.resupply always fills to a full magazine and reserve`` () =
    Assert.Equal(Ready(AmmoConfig.MagazineSize, AmmoConfig.ReserveStart), Ammo.resupply (Ready(0, 0)))
    Assert.Equal(Ready(AmmoConfig.MagazineSize, AmmoConfig.ReserveStart), Ammo.resupply (Reloading(5, 10)))
    Assert.True(Ammo.isFull (Ready(AmmoConfig.MagazineSize, AmmoConfig.ReserveStart)))
    Assert.False(Ammo.isFull (Ready(AmmoConfig.MagazineSize - 1, AmmoConfig.ReserveStart)))

// --- Ammo: Combat gates firing --------------------------------------------

[<Fact>]
let ``an agent with no ammunition at all does not fire even at a qualifying target`` () =
    let friendly = { Agent.create (agent 0) Friendly { X = 0; Y = 0 } with Ammo = Ready(0, 0) }
    let hostile = Agent.create (agent 1) Hostile { X = 5; Y = 0 } // Chebyshev 5 <= WeaponRange, visible
    let r = stepIdle (worldOf [ friendly; hostile ])

    Assert.DoesNotContain(0, shotsFiredIn r |> Array.map (fun (s, _, _) -> AgentId.value s))

// --- Ammo: reload and resupply consequence phases -------------------------

[<Fact>]
let ``an empty magazine with reserve starts and finishes a reload with the documented events`` () =
    let a = { Agent.create (agent 0) Friendly { X = 0; Y = 0 } with Ammo = Ready(0, 10) }
    let mutable st = worldOf [ a ]

    let r1 = stepIdle st
    Assert.Equal(Reloading(10, AmmoConfig.ReloadTicks), (agentOf (agent 0) r1.State).Ammo)
    Assert.Contains(ReloadStarted(agent 0), bodies r1)
    st <- r1.State

    // The `Casualty.tickBleedOut` precedent exactly: the reload runs for
    // AmmoConfig.ReloadTicks further ticks (ticks 2..ReloadTicks here, one
    // per tick since tick 1 already consumed the first) before the
    // (ReloadTicks + 1)-th tick refills, on the identical "ticksRemaining
    // <= 1" boundary bleed-out uses.
    for _ in 2 .. AmmoConfig.ReloadTicks do
        let r = stepIdle st
        Assert.DoesNotContain(ReloadCompleted(agent 0), bodies r)
        st <- r.State

    let final = stepIdle st
    Assert.Equal(Ready(10, 0), (agentOf (agent 0) final.State).Ammo)
    Assert.Contains(ReloadCompleted(agent 0), bodies final)

[<Fact>]
let ``an agent standing on a resupply area is refilled instantly, once`` () =
    let cell = { X = 3; Y = 3 }
    let a = { Agent.create (agent 0) Friendly cell with Ammo = Ready(0, 0) }
    let w = { worldOf [ a ] with ResupplyAreas = [| cell |] }

    let r = stepIdle w
    Assert.Equal(Ready(AmmoConfig.MagazineSize, AmmoConfig.ReserveStart), (agentOf (agent 0) r.State).Ammo)
    Assert.Contains(AgentResupplied(agent 0), bodies r)

    // Already full: no further event on a quiet tick.
    let again = stepIdle r.State
    Assert.DoesNotContain(AgentResupplied(agent 0), bodies again)

// --- Ammo: stage-2 appraisal gate for Suppress / Assault only -------------

[<Fact>]
let ``Suppress and Assault orders from an unarmed agent are Unable InsufficientAmmunition`` () =
    let unarmedSuppress =
        { Agent.create (agent 0) Friendly { X = 0; Y = 0 } with Ammo = Ready(0, 0) }

    let hostile = Agent.create (agent 9) Hostile { X = 1; Y = 0 }
    let w = worldOf [ unarmedSuppress; hostile ]

    let suppress = stepWith [| Command.suppress (CommandId.ofInt 1) 0L (agent 0) (agent 9) |] w
    Assert.Equal(Some(Unable(InsufficientAmmunition, [||])), dispositionOf (agent 0) suppress)

    let assault = stepWith [| Command.assault (CommandId.ofInt 1) 0L (agent 0) { X = 7; Y = 0 } |] w
    Assert.Equal(Some(Unable(InsufficientAmmunition, [||])), dispositionOf (agent 0) assault)

    // MoveTo/Hold/Withdraw are unaffected: an unarmed agent can still walk.
    let move = stepWith [| cmd 1 (agent 0) { X = 3; Y = 0 } |] w
    Assert.Equal(Some Accepted, dispositionOf (agent 0) move)

// --- Communication range, delay, jamming, and radio-destroyed (TASK-058, backlog B-016b) ----

/// A 32x32 world with an authored Headquarters and (optionally) jammers --
/// the `commsBlackoutWorld` precedent, extended. `agents` are Friendly,
/// placed directly: Headquarters/Jammers are plain `WorldState` fields, not
/// Scenario-validated content, so no deployment machinery is needed for a
/// phase-level test.
let private commsHqWorld (hq: Cell) (jammers: Jammer[]) (agents: (int * Cell) list) : WorldState =
    let bounds: GridBounds = { Width = 32; Height = 32 }
    let created = agents |> List.map (fun (i, c) -> Agent.create (agent i) Friendly c)

    match World.create bounds 1UL created with
    | Ok w -> { w with Headquarters = Some hq; Jammers = jammers }
    | Error e -> failwith $"unexpected {e}"

let private deliveredIn (r: StepResult) =
    bodies r
    |> Array.choose (function
        | OrderDelivered(c, recipient) -> Some(c, recipient)
        | _ -> None)

[<Fact>]
let ``an order to a recipient beyond CommsConfig.Range is refused OutOfRange, never delivered`` () =
    let w = commsHqWorld { X = 0; Y = 0 } [||] [ 0, { X = 20; Y = 0 } ]
    let r = stepWith [| cmd 1 (agent 0) { X = 21; Y = 0 } |] w

    Assert.Equal<_[]>([| (CommandId.ofInt 1, agent 0, OutOfRange) |], undeliveredIn r)
    Assert.Equal(None, (agentOf (agent 0) r.State).PendingDelivery)
    Assert.Equal(None, (agentOf (agent 0) r.State).Destination)

[<Fact>]
let ``an order to a recipient within range is stashed as PendingDelivery, not delivered the same tick`` () =
    let w = commsHqWorld { X = 0; Y = 0 } [||] [ 0, { X = 10; Y = 0 } ]
    let r = stepWith [| cmd 1 (agent 0) { X = 15; Y = 0 } |] w

    Assert.Empty(undeliveredIn r)
    Assert.Empty(deliveredIn r)
    Assert.Equal(None, (agentOf (agent 0) r.State).Order)
    Assert.Equal(None, (agentOf (agent 0) r.State).Destination)

    match (agentOf (agent 0) r.State).PendingDelivery with
    | Some(order, Replace, dueTick) ->
        Assert.Equal(MoveTo { X = 15; Y = 0 }, order.Intent)
        Assert.Equal(1L + CommsConfig.DeliveryDelayTicks, dueTick)
    | other -> Assert.Fail($"expected a PendingDelivery, got {other}")

[<Fact>]
let ``a delayed order is delivered exactly DeliveryDelayTicks ticks after acceptance, not before`` () =
    let w = commsHqWorld { X = 0; Y = 0 } [||] [ 0, { X = 10; Y = 0 } ]
    let mutable st = (stepWith [| cmd 1 (agent 0) { X = 15; Y = 0 } |] w).State

    // Ticks 2 and 3 (due tick is 1 + CommsConfig.DeliveryDelayTicks = 4):
    // still pending, no delivery.
    for _ in 2..3 do
        let r = stepIdle st
        Assert.Empty(deliveredIn r)
        Assert.Equal(None, (agentOf (agent 0) r.State).Destination)
        st <- r.State

    let r4 = stepIdle st
    Assert.Equal<_[]>([| (CommandId.ofInt 1, agent 0) |], deliveredIn r4)
    Assert.Equal(Some { X = 15; Y = 0 }, (agentOf (agent 0) r4.State).Destination)
    Assert.Equal(None, (agentOf (agent 0) r4.State).PendingDelivery)

[<Fact>]
let ``a recipient inside an active jammer's radius is refused Jammed, then delivers once the window closes`` () =
    let jammer: Jammer =
        { Position = { X = 10; Y = 0 }
          Radius = 0
          ActiveFromTick = 1L
          ActiveUntilTick = 3L }

    let w = commsHqWorld { X = 0; Y = 0 } [| jammer |] [ 0, { X = 10; Y = 0 } ]

    let r1 = stepWith [| cmd 1 (agent 0) { X = 15; Y = 0 } |] w
    Assert.Equal<_[]>([| (CommandId.ofInt 1, agent 0, Jammed) |], undeliveredIn r1)

    // Tick 2: still jammed (2 <= ActiveUntilTick 3).
    let r2 = stepWith [| cmd 2 (agent 0) { X = 15; Y = 0 } |] r1.State
    Assert.Equal<_[]>([| (CommandId.ofInt 2, agent 0, Jammed) |], undeliveredIn r2)

    // Tick 3 is the jammer's last active tick; tick 4 the window has closed.
    let r3 = stepIdle r2.State
    let r4 = stepWith [| cmd 4 (agent 0) { X = 15; Y = 0 } |] r3.State
    Assert.Empty(undeliveredIn r4)

    match (agentOf (agent 0) r4.State).PendingDelivery with
    | Some(order, _, dueTick) ->
        Assert.Equal(MoveTo { X = 15; Y = 0 }, order.Intent)
        Assert.Equal(4L + CommsConfig.DeliveryDelayTicks, dueTick)
    | None -> Assert.Fail("expected a PendingDelivery once the jammer window closed")

[<Fact>]
let ``a radio-destroyed agent's orders are refused RadioDestroyed even while still Alive`` () =
    let w = commsHqWorld { X = 0; Y = 0 } [||] [ 0, { X = 10; Y = 0 } ]

    let w =
        { w with
            Agents =
                w.Agents
                |> Array.map (fun a -> if a.Id = agent 0 then { a with RadioDestroyed = true } else a) }

    let r = stepWith [| cmd 1 (agent 0) { X = 15; Y = 0 } |] w
    Assert.Equal<_[]>([| (CommandId.ofInt 1, agent 0, RadioDestroyed) |], undeliveredIn r)

    Assert.True(
        (match (agentOf (agent 0) r.State).Vitals with
         | Alive _ -> true
         | _ -> false),
        "the agent should still be Alive -- radio-destroyed is distinct from a casualty"
    )

[<Fact>]
let ``a new pending command supersedes an in-flight one outright, no double delivery`` () =
    let w = commsHqWorld { X = 0; Y = 0 } [||] [ 0, { X = 10; Y = 0 } ]
    let tick1 = stepWith [| cmd 1 (agent 0) { X = 15; Y = 0 } |] w // due tick 4
    let tick2 = stepWith [| cmd 2 (agent 0) { X = 20; Y = 0 } |] tick1.State // supersedes, due tick 5

    match (agentOf (agent 0) tick2.State).PendingDelivery with
    | Some(order, _, dueTick) ->
        Assert.Equal(MoveTo { X = 20; Y = 0 }, order.Intent)
        Assert.Equal(5L, dueTick)
    | None -> Assert.Fail("expected the superseding order to be pending")

    // Tick 4 (order 1's original due tick): nothing delivers -- it was
    // silently superseded, not merely delayed further (the Replace-over-
    // active-Order precedent, extended to "not yet arrived").
    let tick3 = stepIdle tick2.State
    let tick4 = stepIdle tick3.State
    Assert.Empty(deliveredIn tick4)

    // Tick 5: order 2 delivers.
    let tick5 = stepIdle tick4.State
    Assert.Equal<_[]>([| (CommandId.ofInt 2, agent 0) |], deliveredIn tick5)
    Assert.Equal(Some { X = 20; Y = 0 }, (agentOf (agent 0) tick5.State).Destination)

[<Fact>]
let ``cancelling an in-flight order drops it with no radio round-trip, and it never arrives`` () =
    let w = commsHqWorld { X = 0; Y = 0 } [||] [ 0, { X = 10; Y = 0 } ]
    let tick1 = stepWith [| cmd 1 (agent 0) { X = 15; Y = 0 } |] w // due tick 4

    let tick2 = stepWith [| cancelCmd 2 (agent 0) (CommandId.ofInt 1) |] tick1.State
    Assert.Contains(OrderCancelled(CommandId.ofInt 1, agent 0, false), bodies tick2)
    Assert.Equal(None, (agentOf (agent 0) tick2.State).PendingDelivery)

    let mutable st = tick2.State

    for _ in 3..4 do
        st <- (stepIdle st).State

    Assert.Equal(None, (agentOf (agent 0) st).Destination)
    Assert.Empty(deliveredIn (stepIdle st))

[<Fact>]
let ``Communication.available reduces to the static check alone when no Headquarters is authored`` () =
    let w = commsBlackoutWorld [ 2 ]
    Assert.False(Communication.available w.Headquarters w.Jammers w.Tick (agentOf (agent 2) w))
    Assert.True(Communication.available w.Headquarters w.Jammers w.Tick (agentOf (agent 0) w))

[<Fact>]
let ``a radio-destroy roll is deterministic across two runs`` () =
    // Two agents close enough to trade fire every tick (automatic symmetric
    // engagement, no order needed -- the TASK-031 precedent), with an
    // authored Headquarters so the radio-destroy roll is active.
    let build () =
        let agents =
            [ Agent.create (agent 0) Friendly { X = 0; Y = 0 }
              Agent.create (agent 9) Hostile { X = 1; Y = 0 } ]

        match World.create { Width = 8; Height = 8 } 1UL agents with
        | Ok w -> { w with Headquarters = Some { X = 0; Y = 0 } }
        | Error e -> failwith $"unexpected {e}"

    let run () =
        let mutable st = build ()

        for _ in 1..10 do
            st <- (stepIdle st).State

        st

    let r1 = run ()
    let r2 = run ()
    Assert.Equal<AgentState[]>(r1.Agents, r2.Agents)
    Assert.Equal(Hashing.hash r1, Hashing.hash r2)

// --- Formation slots (TASK-059, backlog B-011d) -------------------------

[<Fact>]
let ``resolveFormationTarget with no offset returns the literal anchor unchanged`` () =
    let t = Terrain.empty bounds
    Assert.Equal({ X = 3; Y = 3 }, Appraisal.resolveFormationTarget t [||] None { X = 3; Y = 3 })

[<Fact>]
let ``resolveFormationTarget applies a free offset directly`` () =
    let t = Terrain.empty bounds
    let anchor = { X = 3; Y = 3 }
    Assert.Equal({ X = 4; Y = 3 }, Appraisal.resolveFormationTarget t [||] (Some { X = 1; Y = 0 }) anchor)

[<Fact>]
let ``resolveFormationTarget redirects to the nearest free cell when the exact offset is occupied`` () =
    let t = Terrain.empty bounds
    let anchor = { X = 3; Y = 3 }
    let ideal = { X = 4; Y = 3 } // anchor + (1, 0)
    let resolved = Appraisal.resolveFormationTarget t [| ideal |] (Some { X = 1; Y = 0 }) anchor
    Assert.NotEqual(ideal, resolved)
    Assert.True(Perception.chebyshev ideal resolved <= AppraisalConfig.FormationSlotSearchRadius)

[<Fact>]
let ``resolveFormationTarget redirects to the nearest free cell when the exact offset is impassable`` () =
    let ideal = { X = 4; Y = 3 } // anchor + (1, 0)
    let t = impassable [ (ideal.X, ideal.Y) ]
    let anchor = { X = 3; Y = 3 }
    let resolved = Appraisal.resolveFormationTarget t [||] (Some { X = 1; Y = 0 }) anchor
    Assert.NotEqual(ideal, resolved)
    Assert.True(Terrain.passable t resolved)

[<Fact>]
let ``resolveFormationTarget falls back to the literal anchor when every cell in radius is blocked`` () =
    let r = AppraisalConfig.FormationSlotSearchRadius
    let anchor = { X = 3; Y = 3 }
    let ideal = { X = 4; Y = 3 } // anchor + (1, 0)

    let t =
        impassable [ for dy in -r..r do
                         for dx in -r..r -> ideal.X + dx, ideal.Y + dy ]

    Assert.Equal(anchor, Appraisal.resolveFormationTarget t [||] (Some { X = 1; Y = 0 }) anchor)

[<Fact>]
let ``two formationed agents ordered to the same nominal cell resolve to distinct destinations`` () =
    let w0 = world ()

    let w =
        { w0 with
            Agents =
                w0.Agents
                |> Array.map (fun a ->
                    if a.Id = agent 0 then { a with FormationOffset = Some { X = -1; Y = 0 } }
                    elif a.Id = agent 1 then { a with FormationOffset = Some { X = 1; Y = 0 } }
                    else a) }

    let order = Command.moveToMany (CommandId.ofInt 1) 0L [ agent 0; agent 1 ] { X = 4; Y = 4 } Routine Standard
    let r = stepWith [| order |] w

    Assert.Equal(Some { X = 3; Y = 4 }, (agentOf (agent 0) r.State).Destination)
    Assert.Equal(Some { X = 5; Y = 4 }, (agentOf (agent 1) r.State).Destination)
