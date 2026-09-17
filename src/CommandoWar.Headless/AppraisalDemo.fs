namespace CommandoWar.Headless

open CommandoWar.Sim

/// Read-only view-model preparation for the TASK-029 Godot appraisal-divergence
/// demo (`docs/06` section 11 "developer overlay"; `docs/07` section 9 criteria
/// 2 and 11; a P3 read-only, corpus-scoped slice of backlog B-029 — the full
/// item stays P4).
///
/// This module is an OBSERVER over `Corpus` / `DiagnosticRender`, exactly as
/// `DiagnosticRender` is over `Diagnostics`: it adds no authoritative state and
/// nothing in `Simulation.step` references it. It is framework-neutral (no
/// Godot type) and lives here in `CommandoWar.Headless` so the demo's C# scene
/// script stays a thin renderer — all corpus loading, the `runFrames` call, the
/// disposition text / tone mapping, and the flattening into a
/// C#-interop-friendly per-tick view model happen here (ADR-0004's "logic in
/// F#, not C#" intent; the demo's C# scene is a scoped deviation from the "F#
/// client-core" half of that rule, recorded in the TASK-029 task file and
/// ledger — not a precedent for the P4 client tasks).
///
/// The frames are built from the *committed* `exposed-approach` corpus entry
/// (`content/replays/exposed-approach.cwlog`) through
/// `DiagnosticRender.runFrames`, which uses `SimConfig.standard` — the same run
/// as the committed ASCII / SVG goldens. Nothing is copied or re-authored.
[<RequireQualifiedAccess>]
module AppraisalDemo =

    /// The corpus entry this demo visualises: two friendlies of `Discipline` 1
    /// and 6 ordered along the same exposed approach past a known machine gun
    /// (contact 2), one `Refused` and one `Accepted` on tick 1.
    [<Literal>]
    let EntryName = "exposed-approach"

    /// The tick the divergence lands on (agent 0 `Refused`, agent 1 `Accepted`).
    /// The scene opens here; the slider still covers ticks 0..`TickCount`.
    [<Literal>]
    let DivergenceTick = 1

    // --- disposition text / tone (independent of DiagnosticRender) -----------

    /// Short readable text for an `OrderDisposition`, matching the committed
    /// golden vocabulary
    /// (`content/diagnostics/exposed-approach-tick-001.ascii.txt`,
    /// `blocked-goal-tick-001.ascii.txt`): `"accepted"`,
    /// `"refused route-too-exposed threat-agent-N"`, `"unable no-known-route"`.
    /// Re-implemented here on purpose: `DiagnosticRender.dispositionText` /
    /// `reasonText` are `let private`, and the TASK-029 test asserts this
    /// mapping against the golden text, not against that module.
    let dispositionText (d: OrderDisposition) : string =
        let reason (r: DecisionReason) =
            match r with
            | NoKnownRoute -> "no-known-route"
            | RouteTooExposed None -> "route-too-exposed"
            | RouteTooExposed(Some id) -> sprintf "route-too-exposed threat-agent-%d" (AgentId.value id)
            | TargetNotKnown -> "target-not-known"
            | CriticallyWounded -> "critically-wounded"

        match d with
        | Accepted -> "accepted"
        | Refused(primary, _) -> sprintf "refused %s" (reason primary)
        | Unable(primary, _) -> sprintf "unable %s" (reason primary)

    /// The disposition's colour role, mirroring the `DiagnosticRender.Svg`
    /// glyph colours (`#2f855a` accepted / `#c53030` refused / `#718096`
    /// unable). A plain string so it crosses to C# without an F# DU.
    let dispositionTone (d: OrderDisposition) : string =
        match d with
        | Accepted -> "accepted"
        | Refused _ -> "refused"
        | Unable _ -> "unable"

    // --- frame sequence -----------------------------------------------------

    /// The `DiagnosticFrame` per tick for the `exposed-approach` entry: index 0
    /// is tick 0 (`Diagnostics.frame` of the initial state), index `i` is tick
    /// `i` (`Diagnostics.frameOf` of that step). `contentDir` is the repository
    /// `content/replays` directory, unused since TASK-036 (the entry's
    /// commands are builder-authored, `Corpus.commandsOf`) but kept for the
    /// entry-lookup failure path — this demo has no fallback scenario.
    let loadExposedApproachFrames (contentDir: string) : DiagnosticFrame[] =
        let entry =
            Corpus.all
            |> Array.tryFind (fun e -> e.Name = EntryName)
            |> Option.defaultWith (fun () -> failwithf "corpus entry '%s' not found in Corpus.all" EntryName)

        match Corpus.commandsOf contentDir entry with
        | Error m -> failwithf "TASK-029 appraisal demo: %s" m
        | Ok cmds -> DiagnosticRender.runFrames (entry.InitialState ()) cmds entry.TickCount

    // --- flat view model for the C# renderer -------------------------------

    /// One grid cell, flattened (no F# tuple crosses to C#).
    [<CLIMutable>]
    type CellXY = { X: int; Y: int }

    /// One agent as the demo draws it: side, cell, and an optional movement
    /// destination collapsed to a flag + coordinates.
    [<CLIMutable>]
    type AgentDot =
        { Id: int
          Friendly: bool
          X: int
          Y: int
          HasDestination: bool
          DestX: int
          DestY: int }

    /// The squad's last-known cell for one contact (`Overlay.KnownContact`).
    [<CLIMutable>]
    type ContactRing =
        { Contact: int
          X: int
          Y: int
          Confidence: int
          LastSeenTick: int64 }

    /// One agent's planned route (`Overlay.PlannedPath`).
    [<CLIMutable>]
    type RouteLine =
        { FromX: int
          FromY: int
          TargetX: int
          TargetY: int
          Cells: CellXY[]
          Cost: int }

    /// One agent's appraisal outcome (`Overlay.OrderAppraisal`), with the
    /// disposition reduced to readable text + a tone and the exposed route
    /// cells for the tint.
    [<CLIMutable>]
    type AppraisalRow =
        { AgentId: int
          X: int
          Y: int
          Text: string
          Tone: string
          ExposedCells: CellXY[] }

    /// Everything the C# scene needs to draw one tick. Flat records / arrays /
    /// primitives only — no F# `option`, DU, `list`, or tuple.
    [<CLIMutable>]
    type FrameView =
        { Tick: int64
          Width: int
          Height: int
          /// Row-major `Width * Height`; `true` where an agent may enter.
          Passable: bool[]
          HashHex: string
          HashFormat: int
          RandomDraws: uint64
          Agents: AgentDot[]
          Contacts: ContactRing[]
          Routes: RouteLine[]
          Appraisals: AppraisalRow[]
          /// A one-line label per `Overlay` case the demo does not draw
          /// (empty for the `exposed-approach` frames — a safety fallback).
          UnhandledOverlays: string[] }

    let private cellXY (c: Cell) : CellXY = { X = c.X; Y = c.Y }

    let private passableLayer (frame: DiagnosticFrame) : bool[] =
        match frame.Layers |> Array.tryFind (fun l -> l.Name = LayerName.Passability) with
        | Some l -> l.Cells |> Array.map (fun v -> v <> 0)
        | None -> Array.create (frame.Bounds.Width * frame.Bounds.Height) true

    /// Flattens one `DiagnosticFrame` into the C# view model. Pure and total.
    let frameView (frame: DiagnosticFrame) : FrameView =
        let agents =
            frame.Agents
            |> Array.sortBy (fun a -> a.Id)
            |> Array.map (fun a ->
                let dx, dy, has =
                    match a.Destination with
                    | Some d -> d.X, d.Y, true
                    | None -> 0, 0, false

                { Id = AgentId.value a.Id
                  Friendly =
                    match a.Side with
                    | Friendly -> true
                    | Hostile -> false
                  X = a.Cell.X
                  Y = a.Cell.Y
                  HasDestination = has
                  DestX = dx
                  DestY = dy })

        let contacts = ResizeArray<ContactRing>()
        let routes = ResizeArray<RouteLine>()
        let appraisals = ResizeArray<AppraisalRow>()
        let unhandled = ResizeArray<string>()

        for o in frame.Overlays do
            match o with
            | KnownContact(cell, contact, confidence, lastSeenTick) ->
                contacts.Add
                    { Contact = AgentId.value contact
                      X = cell.X
                      Y = cell.Y
                      Confidence = confidence
                      LastSeenTick = lastSeenTick }
            | PlannedPath(from, target, cells, cost, _) ->
                routes.Add
                    { FromX = from.X
                      FromY = from.Y
                      TargetX = target.X
                      TargetY = target.Y
                      Cells = cells |> Array.map cellXY
                      Cost = cost }
            | OrderAppraisal(agent, at, disposition, exposedCells) ->
                appraisals.Add
                    { AgentId = AgentId.value agent
                      X = at.X
                      Y = at.Y
                      Text = dispositionText disposition
                      Tone = dispositionTone disposition
                      ExposedCells = exposedCells |> Array.map cellXY }
            | Cells(label, cells) ->
                unhandled.Add(sprintf "cells '%s' (%d)" label cells.Length)
            | SightRay(from, target, _, _) ->
                unhandled.Add(sprintf "sight-ray (%d,%d)->(%d,%d)" from.X from.Y target.X target.Y)
            | Reserved(cell, winner, _) ->
                unhandled.Add(sprintf "reserved (%d,%d) agent %d" cell.X cell.Y (AgentId.value winner))
            | Obstructed(cell, occupant) ->
                unhandled.Add(sprintf "obstructed (%d,%d) agent %d" cell.X cell.Y (AgentId.value occupant))
            | UndeliveredOrder(recipient, at, _) ->
                unhandled.Add(sprintf "undelivered order agent %d (%d,%d)" (AgentId.value recipient) at.X at.Y)
            | AgentCommitment(agent, at, _) ->
                // TASK-030: not yet surfaced in this disposable P3 demo (it
                // predates the phase); the OrderAppraisal panel already shows
                // the agent's decision.
                unhandled.Add(sprintf "commitment agent %d (%d,%d)" (AgentId.value agent) at.X at.Y)
            | FireLine(shooter, _, target, at, hit) ->
                // TASK-031: not yet surfaced in this disposable P3 demo (it
                // predates the phase).
                unhandled.Add(
                    sprintf
                        "fire agent %d -> agent %d (%d,%d) %s"
                        (AgentId.value shooter)
                        (AgentId.value target)
                        at.X
                        at.Y
                        (if hit then "hit" else "miss")
                )
            | AgentSuppression(agent, at, suppression) ->
                // TASK-032: not yet surfaced in this disposable P3 demo (it
                // predates the phase); exposed-approach tick 1 has no combat
                // yet, so this never fires for the committed frame.
                unhandled.Add(sprintf "suppression agent %d (%d,%d) %d" (AgentId.value agent) at.X at.Y suppression)
            | AgentStress(agent, at, stress) ->
                // TASK-033: not yet surfaced in this disposable P3 demo (it
                // predates the phase); exposed-approach tick 1 has no
                // opposing-side contact yet, so this never fires for the
                // committed frame.
                unhandled.Add(sprintf "stress agent %d (%d,%d) %d" (AgentId.value agent) at.X at.Y stress)
            | HostileKnownContact(cell, contact, confidence, lastSeenTick) ->
                // TASK-034: not yet surfaced in this disposable P3 demo (it
                // predates the phase); the demo only ever renders the friendly
                // squad's picture via `ContactRing`.
                unhandled.Add(
                    sprintf
                        "hostile known contact agent %d (%d,%d) confidence %d seen tick %d"
                        (AgentId.value contact)
                        cell.X
                        cell.Y
                        confidence
                        lastSeenTick
                )
            | AgentOrderQueue(agent, at, queued) ->
                // TASK-044: not yet surfaced in this disposable P3 demo (it
                // predates the task); exposed-approach never queues an order.
                unhandled.Add(sprintf "order queue agent %d (%d,%d) %d" (AgentId.value agent) at.X at.Y queued.Length)
            | AgentVitals(agent, at, vitals) ->
                // TASK-045: not yet surfaced in this disposable P3 demo (it
                // predates the task); every agent in exposed-approach stays
                // at full health (no combat reaches it at tick 1).
                unhandled.Add(sprintf "vitals agent %d (%d,%d) %A" (AgentId.value agent) at.X at.Y vitals)
            | SquadLeadership leader ->
                // TASK-045: not yet surfaced in this disposable P3 demo.
                unhandled.Add(sprintf "squad leadership %A" leader)

        { Tick = frame.Tick
          Width = frame.Bounds.Width
          Height = frame.Bounds.Height
          Passable = passableLayer frame
          HashHex = sprintf "0x%016X" frame.Hash.Value
          HashFormat = frame.Hash.Format
          RandomDraws = frame.RandomDraws
          Agents = agents
          Contacts = contacts.ToArray()
          Routes = routes.ToArray()
          Appraisals = appraisals.ToArray()
          UnhandledOverlays = unhandled.ToArray() }

    /// The whole scrubbable sequence for the C# scene: one `FrameView` per tick,
    /// ticks 0..`entry.TickCount`.
    let loadFrameViews (contentDir: string) : FrameView[] =
        loadExposedApproachFrames contentDir |> Array.map frameView
