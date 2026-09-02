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
