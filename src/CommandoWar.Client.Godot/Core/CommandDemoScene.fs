namespace CwClientCore

open CommandoWar.Sim
open CommandoWar.Headless

/// Live selection, input-mapped `MoveTo` orders, a real-`Pathfinding.find`
/// route preview, and tactical pause over `DemoScenario` (TASK-040, backlog
/// B-026) -- the first scene where the player, not a canned command log,
/// drives `Simulation.step`. Reuses TASK-039's terrain-item/depth-sort
/// helpers (`RenderShared`) rather than duplicating them.
type CommandDemoScene() =
    let simHz = 20.0
    let maxCatchUpStepsPerFrame = 5

    let mutable state = Unchecked.defaultof<WorldState>
    let mutable prevAgents: Map<int, Cell> = Map.empty
    let mutable currAgents: AgentSnapshot[] = [||]
    let mutable terrainItems: DrawItem[] = [||]
    let mutable accum = 0.0
    let mutable alpha = 0.0
    let mutable hash = 0UL
    let mutable paused = false
    let mutable selected: AgentId option = None
    let mutable previewPath: Cell[] option = None

    // Player-issued orders awaiting delivery. `RecordedCommand` (the
    // `DemoDrive.commandsForTick` precedent) rather than a bespoke type --
    // its `Tick` is the delivery tick, independent of `PlayerCommand.
    // IssuedAtTick` (TASK-024). Never persisted; this scene keeps no replay
    // log.
    let pending = ResizeArray<RecordedCommand>()
    let mutable nextCommandId = 0

    let commandsForTick (t: int64) : PlayerCommand[] =
        let cmds = pending |> Seq.filter (fun c -> c.Tick = t) |> Seq.map (fun c -> c.Command) |> Array.ofSeq
        pending.RemoveAll(fun c -> c.Tick = t) |> ignore
        cmds

    /// One authoritative step consuming any orders queued for delivery at
    /// `state.Tick + 1`. Used by both `Update` (wall-clock-paced) and the
    /// scripted headless self-check (paced by direct calls, no wall clock).
    let stepOnce () =
        let r = Simulation.step SimConfig.standard (commandsForTick (state.Tick + 1L)) state
        prevAgents <- currAgents |> Array.map (fun a -> AgentId.value a.Id, a.Position) |> Map.ofArray
        state <- r.State
        currAgents <- r.Snapshot.Agents
        hash <- r.StateHash.Value

    let friendlyAt (cell: Cell) : AgentSnapshot option =
        currAgents |> Array.tryFind (fun a -> a.Side = Friendly && a.Position = cell)

    let agentPosition (id: AgentId) : Cell option =
        currAgents |> Array.tryFind (fun a -> a.Id = id) |> Option.map (fun a -> a.Position)

    /// A route's cells excluding the traveller's own starting cell, drawn as
    /// small `Kind = 1` dots in the given colour/alpha/radius -- the shared
    /// shape behind the hover preview, the pending-order marker, and the
    /// committed route (Dave's review feedback on TASK-040: each state needs
    /// its own colour so "is this order actually set" is legible).
    let routeDots (cells: Cell[]) (r: float32, g: float32, b: float32) (a: float32) (radius: float32) : DrawItem[] =
        cells
        |> Array.skip (min 1 cells.Length)
        |> Array.map (fun c -> { Kind = 1; Cx = float32 c.X; Cy = float32 c.Y; R = r; G = g; B = b; A = a; Radius = radius })

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
            if not paused then
                let simStep = 1.0 / simHz
                accum <- accum + deltaSeconds
                let mutable steps = 0

                while accum >= simStep && steps < maxCatchUpStepsPerFrame do
                    stepOnce ()
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

            // Selection halo: a larger, translucent Kind = 1 item at the
            // selected agent's own cell, inserted before its real circle so
            // the stable depth-sort tie-break draws it underneath.
            let haloItems =
                match selected |> Option.bind agentPosition with
                | Some pos ->
                    [| { Kind = 1
                         Cx = float32 pos.X
                         Cy = float32 pos.Y
                         R = 1.0f
                         G = 0.95f
                         B = 0.30f
                         A = 0.35f
                         Radius = 17.0f } |]
                | None -> [||]

            // Three distinct route states, each its own colour (Dave's review
            // feedback: a hover is not a queued order is not a confirmed
            // one):
            //   - hover preview (yellow, dim): what a click right now would
            //     target, live under the mouse, not yet committed to anything.
            let previewItems =
                previewPath
                |> Option.map (fun cells -> routeDots cells (1.0f, 0.9f, 0.3f) 0.35f 5.0f)
                |> Option.defaultValue [||]

            //   - pending / queued (orange): a `RecordedCommand` already
            //     issued but not yet delivered to command intake -- normally
            //     one frame, but held open indefinitely while paused, which
            //     is exactly when Dave asked to still see "it is set".
            let pendingItems =
                pending
                |> Seq.choose (fun c ->
                    match c.Command.Intent, agentPosition c.Command.Agent with
                    | MoveTo target, Some pos ->
                        match Pathfinding.find state.Terrain pos target with
                        | Found(cells, _) -> Some(routeDots cells (1.0f, 0.65f, 0.15f) 0.5f 6.0f)
                        | _ -> None
                    | _ -> None)
                |> Array.concat

            //   - committed / en route (green): the order was delivered and
            //     Appraisal accepted it -- `AgentSnapshot.Destination` is
            //     populated (TASK-028's existing mechanism). The full route,
            //     not just the endpoint, so "set" reads as a path, not a dot.
            let committedItems =
                currAgents
                |> Array.choose (fun a ->
                    a.Destination
                    |> Option.bind (fun d ->
                        match Pathfinding.find state.Terrain a.Position d with
                        | Found(cells, _) -> Some(routeDots cells (0.35f, 1.0f, 0.45f) 0.6f 7.0f)
                        | _ -> None))
                |> Array.concat

            Array.concat [ terrainItems; haloItems; agentItems; previewItems; pendingItems; committedItems ]
            |> Array.sortBy RenderShared.depthKey

        member _.HudText() =
            let selText =
                match selected with
                | Some id -> sprintf "agent %d" (AgentId.value id)
                | None -> "none"

            sprintf
                "tick %d   hash 0x%016X   agents %d   %s   selected=%s"
                state.Tick
                hash
                currAgents.Length
                (if paused then "PAUSED" else "running")
                selText

        member _.OnClick(isLeftButton: bool, cellX: int, cellY: int) =
            if not isLeftButton then
                selected <- None
            else
                let cell = { X = cellX; Y = cellY }

                match friendlyAt cell with
                | Some a -> selected <- Some a.Id
                | None ->
                    match selected with
                    | Some agentId when GridBounds.contains cell state.Bounds && agentPosition agentId <> Some cell ->
                        let cmd = Command.moveTo (CommandId.ofInt nextCommandId) state.Tick agentId cell
                        nextCommandId <- nextCommandId + 1

                        // Replace, not stack: an agent has at most one
                        // undelivered order at a time today (no waypoint
                        // queue yet -- see the TASK-040 review follow-up).
                        // Without this, two clicks before the next delivery
                        // tick (easy while paused) would hand Simulation.step
                        // two different commands both addressing the same
                        // agent in one tick's batch, an untested combination.
                        pending.RemoveAll(fun c -> c.Command.Agent = agentId) |> ignore

                        pending.Add
                            { Tick = state.Tick + 1L
                              Sequence = pending.Count
                              Command = cmd
                              Issuer = "player" }
                    | _ -> ()

        member _.OnHover(cellX: int, cellY: int) =
            let cell = { X = cellX; Y = cellY }

            previewPath <-
                selected
                |> Option.bind agentPosition
                |> Option.bind (fun pos ->
                    match Pathfinding.find state.Terrain pos cell with
                    | Found(cells, _) -> Some cells
                    | NoPath
                    | BudgetExhausted _
                    | InvalidEndpoint _ -> None)

        member _.OnTogglePause() = paused <- not paused

        member _.Dispose() = ()

    /// Steps exactly `count` ticks with no wall clock involved -- the
    /// scripted headless self-check's pacing, distinct from `Update`'s
    /// frame-delta accumulator.
    member _.StepTicksHeadless(count: int64) : TickHash[] =
        [| for _ in 1L .. count do
             stepOnce ()
             yield { Tick = state.Tick; Hash = hash } |]

/// The scripted headless self-check driver for `CommandDemoScene`
/// (`--selfcheck`'s evidence path, the `DemoDrive.runFullSequence`
/// precedent) -- exercises real `OnClick`/`OnHover` input mapping instead of
/// a canned command log.
[<RequireQualifiedAccess>]
module CommandDemoDrive =

    /// Selects friendly agent 0 (at (0,0)), previews and issues a short
    /// `MoveTo(3,0)` clear of the ridge and the impassable block, then steps
    /// the full `DemoScenario` run.
    let runScriptedSelfCheck () : TickHash[] =
        let scene = CommandDemoScene()
        let asScene = scene :> IClientScene
        asScene.Ready()
        asScene.OnClick(true, 0, 0)
        asScene.OnHover(3, 0)
        asScene.OnClick(true, 3, 0)
        scene.StepTicksHeadless(DemoScenario.TickCount)
