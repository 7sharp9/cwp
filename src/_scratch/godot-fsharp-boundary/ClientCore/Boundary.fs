// TASK-007 disposable proof. Not in any .slnx.
//
// The C#<->F# boundary types. Everything here is a plain-CLR shape: primitives,
// arrays, and [<CLIMutable>] records that C# sees as ordinary get/set POCOs.
// No F#-only idiom (FSharpOption, FSharpList, module-function access, DU cases)
// crosses to C#. This module has NO Godot types.
//
// This is the answer to TASK-007 question 4: the cleaner interop idiom is to
// keep the sim facade itself in F# (Host.fs) and expose only shapes like these.
// C# never calls CommandoWar.Sim, so it never writes AgentIdModule.ofInt,
// ListModule.OfArray, or FSharpOption.get_IsSome.

namespace CwClientCore

open CommandoWar.Sim

/// C#-friendly value view of one agent for rendering. No sim references, no
/// F# option. `HasDestination = false` means Dest{X,Y} are unspecified.
[<CLIMutable>]
type AgentView =
    { Id: int
      Friendly: bool
      X: int
      Y: int
      HasDestination: bool
      DestX: int
      DestY: int }

/// What one authoritative tick produced, reduced to primitives.
[<CLIMutable>]
type TickView =
    { Tick: int64
      Hash: uint64
      HashHex: string
      HashFormat: int
      EventCount: int
      AcceptedThisTick: int }

[<RequireQualifiedAccess>]
module internal Views =

    let ofAgents (agents: AgentState[]) : AgentView[] =
        agents
        |> Array.sortBy (fun a -> a.Id)
        |> Array.map (fun a ->
            let dx, dy, has =
                match a.Destination with
                | Some c -> c.X, c.Y, true
                | None -> 0, 0, false
            { Id = AgentId.value a.Id
              Friendly = (a.Side = Friendly)
              X = a.Position.X
              Y = a.Position.Y
              HasDestination = has
              DestX = dx
              DestY = dy })
