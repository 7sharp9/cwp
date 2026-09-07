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

[<Fact>]
let ``a friendly with clear line of sight to an in-range hostile observes it and shares it in the squad picture`` () =
    let b: GridBounds = { Width = 16; Height = 8 }
    let w = perceptionWorld b [ 0, { X = 1; Y = 1 } ] [ 1, { X = 6; Y = 1 } ] (Terrain.empty b)
    let r = stepIdle w // tick 1

    Assert.Contains(ContactObserved(agent 0, agent 1, { X = 6; Y = 1 }), bodies r)
    // Perception is symmetric: the hostile observes the friendly too.
    Assert.Contains(ContactObserved(agent 1, agent 0, { X = 1; Y = 1 }), bodies r)

    Assert.Equal<AgentId[]>([| agent 1 |], (agentOf (agent 0) r.State).VisibleContacts)

    // Only the friendly's observation reaches the shared squad picture (the
    // hostile squad picture is B-022).
    let c = Assert.Single r.State.TacticalKnowledge
    Assert.Equal(agent 1, c.Contact)
    Assert.Equal({ X = 6; Y = 1 }, c.LastKnownCell)
    Assert.Equal(1L, c.LastSeenTick)
    Assert.Equal(PerceptionConfig.ConfidenceFull, c.Confidence)

    // A new sighting emits ContactObserved once; a second idle tick with the
    // contact still visible does not re-emit it (no per-tick flood).
    let r2 = stepIdle r.State
    Assert.DoesNotContain(bodies r2, (function ContactObserved _ -> true | _ -> false))
    Assert.Equal(2L, (contactOf (agent 1) r2.State).Value.LastSeenTick)

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
    let b: GridBounds = { Width = 40; Height = 6 }
    let w = perceptionWorld b [ 0, { X = 2; Y = 1 } ] [ 1, { X = 6; Y = 3 } ] (Terrain.empty b)

    // Walk the friendly far east, out of sight of the stationary hostile.
    let mutable st =
        (stepWith [| Command.moveTo (CommandId.ofInt 1) 0L (agent 0) { X = 39; Y = 1 } |] w).State

    let seenThisTick (s: WorldState) =
        match contactOf (agent 1) s with
        | Some c -> c.LastSeenTick = s.Tick
        | None -> false

    Assert.True(seenThisTick st, "the hostile should be seen on the command tick")

    while seenThisTick st do
        st <- (stepIdle st).State

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

    Assert.Equal(Some(lastSeen + int64 PerceptionConfig.ExpireAfter, { X = 6; Y = 3 }), expired)
    Assert.Equal(None, contactOf (agent 1) st)
