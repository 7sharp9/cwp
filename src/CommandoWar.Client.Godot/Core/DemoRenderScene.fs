namespace CwClientCore

open CommandoWar.Sim
open CommandoWar.Headless

/// Shared, framework-neutral stepping helpers over `DemoScenario` (TASK-039,
/// backlog B-027): the confirmed first live-render source (an elevation
/// ridge, an impassable block, a movement-cost patch, an opaque wall, and
/// directional cover -- real terrain relief for depth ordering to prove
/// itself against). No `CommandoWar.Sim`/`CommandoWar.Headless` change: this
/// only drives the existing `Simulation.step` / `RenderSnapshot` contract,
/// the `DiagnosticRender.runFrames` per-tick pattern minus the
/// diagnostic/overlay machinery (production rendering reads `RenderSnapshot`,
/// never `DiagnosticFrame` -- diagnostics are observers only, ADR-0002).
[<RequireQualifiedAccess>]
module DemoDrive =

    let private commandsForTick (log: RecordedCommand[]) (t: int64) =
        log |> Array.filter (fun c -> c.Tick = t) |> Array.map (fun c -> c.Command)

    /// One authoritative step: `state` -> the next tick's `WorldState` plus
    /// its `RenderSnapshot` and state hash.
    let stepOnce (log: RecordedCommand[]) (state: WorldState) : WorldState * RenderSnapshot * uint64 =
        let r = Simulation.step SimConfig.standard (commandsForTick log (state.Tick + 1L)) state
        r.State, r.Snapshot, r.StateHash.Value

    /// The full deterministic run, headless -- `--selfcheck`'s evidence.
    /// `TickHash` (not a tuple: ADR-0004 forbids exposing F# tuples to C#).
    let runFullSequence () : TickHash[] =
        let log = DemoScenario.commandLog ()
        let mutable state = DemoScenario.initialState ()

        [| for _ in 1L .. DemoScenario.TickCount do
             let next, _, h = stepOnce log state
             state <- next
             yield { Tick = state.Tick; Hash = h } |]

/// Live-steps `DemoScenario` and exposes a depth-sorted `DrawItem[]` each
/// frame. No player input, no content import (see the task file's forbidden
/// scope) -- the scene runs unattended once launched.
type DemoRenderScene() =
    // Fixed-step scheduling constants -- the `MainNode.cs` disposable-spike
    // precedent (`src/CommandoWar.Client.Godot/src/MainNode.cs`).
    let simHz = 20.0
    let maxCatchUpStepsPerFrame = 5

    let log = DemoScenario.commandLog ()

    let mutable state = Unchecked.defaultof<WorldState>
    let mutable prevAgents: Map<int, Cell> = Map.empty
    let mutable currAgents: AgentSnapshot[] = [||]
    let mutable terrainItems: DrawItem[] = [||]
    let mutable accum = 0.0
    let mutable alpha = 0.0
    let mutable hash = 0UL

    let advanceOneTick () =
        let next, snapshot, h = DemoDrive.stepOnce log state
        prevAgents <- currAgents |> Array.map (fun a -> AgentId.value a.Id, a.Position) |> Map.ofArray
        state <- next
        currAgents <- snapshot.Agents
        hash <- h

    interface IClientScene with
        member _.Ready() =
            state <- DemoScenario.initialState ()
            terrainItems <- RenderShared.buildTerrainItems state.Terrain
            currAgents <-
                state.Agents
                |> Array.map (fun a ->
                    { Id = a.Id
                      Side = a.Side
                      Position = a.Position
                      Progress = a.Progress
                      Destination = a.Destination })
            prevAgents <- currAgents |> Array.map (fun a -> AgentId.value a.Id, a.Position) |> Map.ofArray

        member _.Update(deltaSeconds: float) =
            if state.Tick < DemoScenario.TickCount then
                let simStep = 1.0 / simHz
                accum <- accum + deltaSeconds
                let mutable steps = 0

                while accum >= simStep && steps < maxCatchUpStepsPerFrame && state.Tick < DemoScenario.TickCount do
                    advanceOneTick ()
                    accum <- accum - simStep
                    steps <- steps + 1

            alpha <- System.Math.Clamp(accum * simHz, 0.0, 1.0)

        member _.DrawList() =
            let lerp (a: int) (b: int) (t: float) = float32 a + (float32 (b - a)) * float32 t

            let agentItems =
                currAgents
                |> Array.map (fun a ->
                    let from = prevAgents |> Map.tryFind (AgentId.value a.Id) |> Option.defaultValue a.Position
                    let r, g, b = RenderShared.agentColor a.Side

                    { Kind = 1
                      Cx = lerp from.X a.Position.X alpha
                      Cy = lerp from.Y a.Position.Y alpha
                      R = r
                      G = g
                      B = b
                      A = 1.0f
                      Radius = 10.0f })

            Array.append terrainItems agentItems |> Array.sortBy RenderShared.depthKey

        member _.HudText() =
            sprintf
                "tick %d / %d   hash 0x%016X   agents %d"
                state.Tick
                DemoScenario.TickCount
                hash
                currAgents.Length

        // No input this scene (TASK-039's task file forbids it -- unattended
        // once launched). Real handling is CommandDemoScene's job (TASK-040).
        member _.OnClick(_isLeftButton: bool, _cellX: int, _cellY: int) = ()
        member _.OnHover(_cellX: int, _cellY: int) = ()
        member _.OnTogglePause() = ()

        member _.Dispose() = ()
