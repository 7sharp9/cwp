namespace CommandoWar.Client.Mibo

// The only module in this spike that opens CommandoWar.Sim.
//
// ADR-0002 boundary: nothing from Mibo or raylib is passed in; nothing from the
// simulation (WorldState, AgentState, events) is passed out. The Mibo Model
// holds the value types below, never authoritative state. This module owns one
// WorldState and advances it with Simulation.step; it never mutates
// authoritative state itself and never catches a Simulation.step exception.

open CommandoWar.Sim
open Content

module SimBridge =

    /// A value snapshot of one agent for rendering. No sim references.
    [<Struct>]
    type AgentView =
        { Id: int
          Friendly: bool
          X: int
          Y: int
          Dest: (struct (int * int)) option }

    /// What one authoritative tick produced, reduced to primitives.
    [<Struct>]
    type TickInfo =
        { Tick: int64
          Hash: uint64
          HashFormat: int
          Events: int
          AcceptedThisTick: int }

    let private toViews (agents: AgentState[]) : AgentView[] =
        agents
        |> Array.sortBy (fun a -> AgentId.value a.Id)
        |> Array.map (fun a ->
            { Id = AgentId.value a.Id
              Friendly = (a.Side = Friendly)
              X = a.Position.X
              Y = a.Position.Y
              Dest =
                match a.Destination with
                | Some c -> Some(struct (c.X, c.Y))
                | None -> None })

    /// Owns a WorldState built from validated content and advances it.
    type Sim(scenario: Scenario) =
        let config = SimConfig.standard

        let world =
            let agents =
                scenario.FriendlySpawns
                |> List.map (fun m -> Agent.create (AgentId.ofInt m.Id) Friendly { X = m.X; Y = m.Y })

            match World.create { Width = scenario.Width; Height = scenario.Height } scenario.Seed agents with
            | Ok w -> w
            | Error e -> invalidOp $"CommandoWar.Sim rejected the authored world: {e}"

        let mutable state = world
        let mutable nextCommandId = 1
        let pending = ResizeArray<PlayerCommand>()
        let acceptedLog = ResizeArray<string>()

        let h0 = Hashing.hash world
        let mutable lastHash = h0.Value
        let mutable lastHashFormat = h0.Format
        let mutable agents = toViews world.Agents

        member _.GridWidth = scenario.Width
        member _.GridHeight = scenario.Height
        member _.Tick = state.Tick
        member _.StateHash = lastHash
        member _.StateHashFormat = lastHashFormat
        member _.StateHashHex = sprintf "0x%016X" lastHash
        member _.RandomDraws = state.Random.Draws
        member _.Agents = agents
        member _.HasPendingCommands = pending.Count > 0

        /// Accepted commands as "<tick> <agentId> move <x> <y>" lines, in the
        /// CommandoWar.Headless command-log format for cross-checking.
        member _.AcceptedCommandLog = List.ofSeq acceptedLog

        /// Queues a move for the next Step. Out-of-bounds targets are still
        /// submitted; the simulation rejects them explicitly at command intake.
        member _.QueueMove(agentId: int, x: int, y: int) =
            let id = nextCommandId
            nextCommandId <- nextCommandId + 1
            pending.Add(Command.moveTo (CommandId.ofInt id) (state.Tick + 1L) (AgentId.ofInt agentId) { X = x; Y = y })
            id

        /// Advances the authoritative simulation by exactly one integer tick.
        /// Any exception from Simulation.step propagates unchanged.
        member _.Step() : TickInfo =
            let commands = pending.ToArray()
            pending.Clear()

            let result = Simulation.step config commands state
            state <- result.State
            lastHash <- result.StateHash.Value
            lastHashFormat <- result.StateHash.Format
            agents <- toViews result.State.Agents

            let mutable accepted = 0

            for ev in result.Events do
                match ev.Body with
                | CommandAccepted(_, agent, dest) ->
                    accepted <- accepted + 1
                    acceptedLog.Add(sprintf "%d %d move %d %d" ev.Tick (AgentId.value agent) dest.X dest.Y)
                | _ -> ()

            { Tick = result.State.Tick
              Hash = result.StateHash.Value
              HashFormat = result.StateHash.Format
              Events = result.Events.Length
              AcceptedThisTick = accepted }
