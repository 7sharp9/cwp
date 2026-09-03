// TASK-007 disposable proof. Not in any .slnx.
//
// The real client logic, in F#. This is what ADR-0001's "named follow-up" asks
// for: view-model preparation, input-to-command mapping, fixed-step
// scheduling, overlay state, and replay-relevant messages all live here, not
// in C#. NO Godot types in this file - it is framework-neutral and unit
// testable on its own.
//
// SimHost is the F# equivalent of the TASK-004 spike's SimFacade.cs. Moving it
// from C# to F# is the whole point: the FSharpOption / ListModule / module-
// function interop the C# facade carried simply disappears here.

namespace CwClientCore

open CommandoWar.Sim
open CommandoWar.Headless

/// Owns one authoritative WorldState and advances it with Simulation.step.
/// Never mutates authoritative state itself; never catches a Simulation.step
/// exception. Public surface is plain CLR: int, int64, uint64, string,
/// AgentView[].
type SimHost private (config: SimConfig, initial: WorldState) =
    let mutable state = initial
    let pending = ResizeArray<PlayerCommand>()
    let acceptedLog = ResizeArray<string>()
    let mutable nextCommandId = 1
    let mutable lastHash = Hashing.hash initial

    /// Build the six-agent fixture world (grid, seed and agents fixed by
    /// CommandoWar.Headless.Fixture). Fails loudly if the sim rejects it.
    static member CreateFixture() : SimHost =
        SimHost(SimConfig.standard, Fixture.initialState ())

    member _.Tick = state.Tick
    member _.StateHash = lastHash.Value
    member _.StateHashFormat = lastHash.Format
    member _.StateHashHex = sprintf "0x%016X" lastHash.Value
    member _.RandomDraws = state.Random.Draws
    member _.AgentViews : AgentView[] = Views.ofAgents state.Agents
    member _.AcceptedCommandLog : string[] = acceptedLog.ToArray()
    member _.HasPending = pending.Count > 0

    /// Queue a move for the next Step. Out-of-bounds targets are still
    /// submitted; the simulation rejects them explicitly at intake.
    member _.QueueMove(agentId: int, x: int, y: int) : int =
        let id = nextCommandId
        nextCommandId <- nextCommandId + 1
        pending.Add(Command.moveTo (CommandId.ofInt id) (state.Tick + 1L) (AgentId.ofInt agentId) { X = x; Y = y })
        id

    /// Advance exactly one integer tick. Any Simulation.step exception
    /// propagates unchanged.
    member _.Step() : TickView =
        let commands = pending.ToArray()
        pending.Clear()
        let result = Simulation.step config commands state
        state <- result.State
        lastHash <- result.StateHash

        let mutable accepted = 0
        for ev in result.Events do
            match ev.Body with
            | CommandAccepted(_, agent, dest) ->
                accepted <- accepted + 1
                acceptedLog.Add(sprintf "%d %d move %d %d" ev.Tick (AgentId.value agent) dest.X dest.Y)
            | _ -> ()

        { Tick = result.State.Tick
          Hash = result.StateHash.Value
          HashHex = sprintf "0x%016X" result.StateHash.Value
          HashFormat = result.StateHash.Format
          EventCount = result.Events.Length
          AcceptedThisTick = accepted }


/// Host-side client state: fixed-step scheduling (never frame delta - the sim
/// only ever gets an integer tick), selection, input-to-command mapping, the
/// overlay string, and the headless self-check. This is the layer the spike
/// left in C# (MainNode.cs); here it is F#.
type ClientHost private (sim: SimHost) =
    let mutable simHz = 20.0
    let mutable accum = 0.0
    let mutable paused = false
    let mutable selectedAgent = -1
    let mutable lastCommanded : (int * int) option = None
    let mutable alpha = 0.0
    let maxCatchUp = 5

    static member Create() = ClientHost(SimHost.CreateFixture())

    /// C#-friendly self-check entry point. C# passes `ulong?` directly; the
    /// FSharpOption never appears on the C# side (TASK-007 question 4).
    static member SelfCheck(expect: System.Nullable<uint64>) : int =
        let e = if expect.HasValue then Some expect.Value else None
        (ClientHost.Create()).RunSelfCheck e

    member _.Sim = sim
    member _.SelectedAgent = selectedAgent
    member _.Paused with get () = paused and set v = paused <- v
    member _.SimHz with get () = simHz and set v = simHz <- max 1.0 (min 120.0 v)
    member _.InterpAlpha = alpha

    /// Advance the host clock by a wall-clock delta (seconds). Returns the
    /// number of authoritative ticks stepped this call.
    member _.Update(deltaSeconds: float) : int =
        if paused then 0
        else
            let stepSeconds = 1.0 / simHz
            accum <- accum + deltaSeconds
            let mutable steps = 0
            while accum >= stepSeconds && steps < maxCatchUp do
                let info = sim.Step()
                if info.AcceptedThisTick = 0 then lastCommanded <- None
                accum <- accum - stepSeconds
                steps <- steps + 1
            alpha <- max 0.0 (min 1.0 (accum / stepSeconds))
            steps

    /// Map a click on logical cell (x,y) to a selection or a move command.
    member _.HandleClick(x: int, y: int) =
        let onAgent =
            sim.AgentViews
            |> Array.tryFind (fun a -> a.Friendly && a.X = x && a.Y = y)
        match onAgent with
        | Some a -> selectedAgent <- a.Id
        | None ->
            if selectedAgent >= 0 then
                sim.QueueMove(selectedAgent, x, y) |> ignore
                lastCommanded <- Some(x, y)

    member _.Deselect() = selectedAgent <- -1

    member _.OverlayText : string =
        let sel = if selectedAgent >= 0 then string selectedAgent else "none"
        let cmd = match lastCommanded with Some(x, y) -> sprintf "MoveTo(%d,%d)" x y | None -> "-"
        String.concat "\n"
            [ sprintf "tick            %d" sim.Tick
              sprintf "state hash      %s  (format %d)" sim.StateHashHex sim.StateHashFormat
              sprintf "random draws    %d" sim.RandomDraws
              sprintf "sim rate        %.0f Hz target%s" simHz (if paused then "   [PAUSED]" else "")
              sprintf "interp alpha    %.2f" alpha
              sprintf "selected agent  %s     last command  %s" sel cmd ]

    /// Headless hash cross-check against CommandoWar.Headless. Steps the shared
    /// fixture (agent 3 -> (20,14) at tick 1, 40 ticks) and prints one
    /// `tick=N hash=0x...` line per tick. Returns the process exit code.
    member _.RunSelfCheck(expect: uint64 option) : int =
        printfn "# godot-fsharp-boundary self-check (host logic in F#, driven from Godot)"
        printfn "tick=0 hash=%s format=%d" sim.StateHashHex sim.StateHashFormat
        sim.QueueMove(AgentId.value Fixture.MovedAgent, Fixture.MoveTarget.X, Fixture.MoveTarget.Y) |> ignore
        for _ in 1L .. Fixture.TickCount do
            let info = sim.Step()
            printfn "tick=%d hash=%s format=%d" info.Tick info.HashHex info.HashFormat
        printfn "final tick=%d hash=%s draws=%d" sim.Tick sim.StateHashHex sim.RandomDraws
        printfn "accepted-command-log:"
        for line in sim.AcceptedCommandLog do printfn "  %s" line
        match expect with
        | None -> 0
        | Some want ->
            if want = sim.StateHash then
                printfn "MATCH expected final hash 0x%016X" want
                0
            else
                printfn "MISMATCH expected 0x%016X, got %s" want sim.StateHashHex
                1

    /// Deliberately raises through an F# call so a C#->F# stack trace can be
    /// inspected (TASK-007 question 5).
    member _.ForceFSharpFault() : unit =
        let inner () : int = failwith "deliberate fault raised inside ClientHost (F#)"
        inner () |> ignore
