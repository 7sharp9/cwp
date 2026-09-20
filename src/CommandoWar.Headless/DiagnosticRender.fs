namespace CommandoWar.Headless

open System.Text
open CommandoWar.Sim

/// Deterministic headless renderers for a `DiagnosticFrame`
/// (docs/06_CONTENT_AND_PRESENTATION.md section 11 "Developer-facing";
/// docs/07_VERTICAL_SLICE.md section 6; docs/09_TEST_STRATEGY.md section 1
/// "Golden data ... explicit ... update procedure").
///
/// Turning a frame into ASCII / SVG / HTML is PRESENTATION, and ADR-0002
/// keeps rendering out of `CommandoWar.Sim`; these renderers therefore live
/// here in the framework-neutral headless project. The Godot developer
/// overlay (backlog B-029) will be a third renderer of the same frame.
///
/// Every renderer is byte-deterministic: no timestamps, no random ids, no
/// culture-sensitive formatting, `\n` line endings. Two renders of the same
/// frame are identical byte for byte.
[<RequireQualifiedAccess>]
module DiagnosticRender =

    // --- shared -----------------------------------------------------------

    let private hx (h: StateHash) = sprintf "0x%016X" h.Value

    let private layer (name: string) (frame: DiagnosticFrame) : GridLayer option =
        frame.Layers |> Array.tryFind (fun l -> l.Name = name)

    let private valueAt (l: GridLayer) (x: int) (y: int) : int =
        l.Cells.[y * l.Bounds.Width + x]

    let private agentAt (frame: DiagnosticFrame) (x: int) (y: int) : AgentMarker option =
        frame.Agents
        |> Array.filter (fun a -> a.Cell.X = x && a.Cell.Y = y)
        |> Array.sortBy (fun a -> a.Id)
        |> Array.tryHead

    let private sideGlyph (s: Side) =
        match s with
        | Friendly -> '@'
        | Hostile -> 'X'

    let private cellText (c: Cell) = sprintf "(%d,%d)" c.X c.Y

    /// Short text for an appraisal `DecisionReason` (TASK-028). Structured
    /// values only; no free prose the model does not carry.
    let private reasonText (r: DecisionReason) : string =
        match r with
        | NoKnownRoute -> "no-known-route"
        | RouteTooExposed None -> "route-too-exposed"
        | RouteTooExposed(Some id) -> sprintf "route-too-exposed threat-agent-%d" (AgentId.value id)
        | TargetNotKnown -> "target-not-known"
        | CriticallyWounded -> "critically-wounded"
        | InsufficientAmmunition -> "insufficient-ammunition"

    /// Short text for an `OrderDisposition` (TASK-028).
    let private dispositionText (d: OrderDisposition) : string =
        match d with
        | Accepted -> "accepted"
        | Refused(primary, _) -> sprintf "refused %s" (reasonText primary)
        | Unable(primary, _) -> sprintf "unable %s" (reasonText primary)

    /// The single-letter SVG glyph and stroke colour for an `OrderDisposition`.
    let private dispositionGlyph (d: OrderDisposition) : string * string =
        match d with
        | Accepted -> "A", "#2f855a"
        | Refused _ -> "R", "#c53030"
        | Unable _ -> "U", "#718096"

    /// The commitment status text shared by the ASCII overlay line and the
    /// HTML per-agent annotation panel: "holding" or "moving to (x,y)".
    let private commitmentText (c: Commitment) : string =
        match c with
        | Holding -> "holding"
        | Moving mc -> sprintf "moving to %s" (cellText mc.Target)
        | Suppressing sc -> sprintf "suppressing agent %d" (AgentId.value sc.Target)
        | Withdrawing wc -> sprintf "withdrawing to %s" (cellText wc.Target)
        | Assaulting ac ->
            let stage =
                match ac.Stage with
                | ApproachingStart -> "approaching start"
                | AwaitingSupport -> "awaiting support"
                | Advancing -> "advancing"
                | ClearingThreat -> "clearing threat"

            sprintf "assaulting %s (%s)" (cellText ac.Target) stage

    /// Short text for a `PlayerIntent` (TASK-044, backlog B-051, for
    /// `AgentOrderQueue` overlay entries) — the `Program.fs` `cwheadless
    /// replay describe` print precedent.
    let private intentText (i: PlayerIntent) : string =
        match i with
        | MoveTo target -> sprintf "move %s" (cellText target)
        | Suppress target -> sprintf "suppress agent %d" (AgentId.value target)
        | Hold area -> sprintf "hold %s" (cellText area)
        | Assault target -> sprintf "assault %s" (cellText target)
        | Withdraw target -> sprintf "withdraw %s" (cellText target)

    /// Short text for a `VitalStatus` (TASK-045, backlog B-031).
    let private vitalsText (v: VitalStatus) : string =
        match v with
        | Alive health -> sprintf "alive %d/%d" health Agent.MaxHealth
        | Incapacitated remaining -> sprintf "incapacitated (bleeding out, %d tick(s))" remaining
        | Dead -> "dead"

    /// Short text for an `AgentAmmo` overlay's fields (TASK-047, backlog
    /// B-030 proper).
    let private ammoText (magazine: int) (reserve: int) (reloading: bool) : string =
        if reloading then
            sprintf "reloading (reserve %d)" reserve
        else
            sprintf "ammo %d/reserve %d" magazine reserve

    /// Steps `initial` through `log` for `tickCount` ticks and collects the
    /// diagnostic frame at every tick: index 0 is tick 0 (`Diagnostics.frame`
    /// of the initial state, no events), index `i` is tick `i`
    /// (`Diagnostics.frameOf` of that step, with this-tick event markers).
    let runFrames (initial: WorldState) (log: RecordedCommand[]) (tickCount: int64) : DiagnosticFrame[] =
        let commandsForTick (t: int64) =
            log |> Array.filter (fun c -> c.Tick = t) |> Array.map (fun c -> c.Command)

        let frames = ResizeArray<DiagnosticFrame>(int tickCount + 1)
        frames.Add(Diagnostics.frame initial)
        let mutable state = initial

        for tick in 1L .. tickCount do
            let r = Simulation.step SimConfig.standard (commandsForTick tick) state
            state <- r.State
            frames.Add(Diagnostics.frameOf r)

        frames.ToArray()

    // --- ASCII ----------------------------------------------------------

    /// A coordinate-ruled composite grid (impassable, opaque, and elevation
    /// folded into one glyph per cell, agents overlaid), a second grid for
    /// directional cover, a legend, an agent roster, this-tick events, and a
    /// footer carrying the determinism trio.
    ///
    /// Composite glyph, in priority order: an agent (`@` friendly, `X`
    /// hostile); `#` impassable; `o` opaque (but passable); an elevation
    /// digit `1`..`9` (`^` above 9, `-` below 0); `.` open flat ground. When
    /// exactly one layer is present (the `--layer` filter) that layer is
    /// rendered as a numeric heat map instead: `.` for 0, `1`..`9`, `+` for
    /// 10 or more, `-` for negative, and `#` for `movement-cost`
    /// `BlockedCost`.
    let Ascii (frame: DiagnosticFrame) : string =
        let b = frame.Bounds
        let sb = StringBuilder()
        let append (s: string) = sb.Append(s) |> ignore
        let line (s: string) = sb.Append(s).Append('\n') |> ignore

        let single =
            if frame.Layers.Length = 1 then Some frame.Layers.[0] else None

        // Line-of-sight overlays drawn onto the composite grid: `*` on a
        // traced cell, `x` on a blocking cell. Empty unless a caller supplied
        // a `SightRay` overlay (e.g. `cwheadless render --los`).
        let sightRays =
            frame.Overlays
            |> Array.choose (function
                | SightRay(_, _, cells, blk) -> Some(cells, blk)
                | Cells _
                | PlannedPath _
                | Reserved _
                | Obstructed _
                | KnownContact _
                | UndeliveredOrder _
                | OrderAppraisal _
                | AgentCommitment _
                | FireLine _
                | AgentSuppression _
                | AgentStress _
                | HostileKnownContact _
                | AgentOrderQueue _
                | AgentVitals _
                | SquadLeadership _
                | AgentAmmo _
                | Divergence _
                | AgentRadioLost _
                | AgentPendingDelivery _
                | AgentFormationSlot _
                | MissionStatus _ -> None)

        let onRay (x: int) (y: int) =
            sightRays
            |> Array.exists (fun (cells, _) -> cells |> Array.exists (fun c -> c.X = x && c.Y = y))

        let blockerAt (x: int) (y: int) =
            sightRays
            |> Array.exists (fun (_, blk) ->
                match blk with
                | Some c -> c.X = x && c.Y = y
                | None -> false)

        // Planned-path overlays drawn onto the composite grid: `+` on a path
        // cell, `S` at the start, `G` at the goal. Empty unless a caller
        // supplied a `PlannedPath` overlay (e.g. `cwheadless render --path`).
        let plannedPaths =
            frame.Overlays
            |> Array.choose (function
                | PlannedPath(a, b, cells, _, _) -> Some(a, b, cells)
                | Cells _
                | SightRay _
                | Reserved _
                | Obstructed _
                | KnownContact _
                | UndeliveredOrder _
                | OrderAppraisal _
                | AgentCommitment _
                | FireLine _
                | AgentSuppression _
                | AgentStress _
                | HostileKnownContact _
                | AgentOrderQueue _
                | AgentVitals _
                | SquadLeadership _
                | AgentAmmo _
                | Divergence _
                | AgentRadioLost _
                | AgentPendingDelivery _
                | AgentFormationSlot _
                | MissionStatus _ -> None)

        let onPath (x: int) (y: int) =
            plannedPaths
            |> Array.exists (fun (_, _, cells) -> cells |> Array.exists (fun c -> c.X = x && c.Y = y))

        let pathStart (x: int) (y: int) =
            plannedPaths |> Array.exists (fun (a, _, _) -> a.X = x && a.Y = y)

        let pathGoal (x: int) (y: int) =
            plannedPaths |> Array.exists (fun (_, b, _) -> b.X = x && b.Y = y)

        let elevation = layer LayerName.Elevation frame
        let passable = layer LayerName.Passability frame
        let opaque = layer LayerName.Opacity frame
        let cost = layer LayerName.MovementCost frame

        let digit (v: int) =
            if v <= 0 then '.'
            elif v >= 10 then '+'
            else char (int '0' + v)

        let glyph (x: int) (y: int) : char =
            match agentAt frame x y with
            | Some a -> sideGlyph a.Side
            | None when blockerAt x y -> 'x'
            | None when pathStart x y -> 'S'
            | None when pathGoal x y -> 'G'
            | None when onRay x y -> '*'
            | None when onPath x y -> '+'
            | None ->
                match single with
                | Some l ->
                    let v = valueAt l x y
                    if l.Name = LayerName.MovementCost && v = System.Int32.MaxValue then '#'
                    elif v < 0 then '-'
                    else digit v
                | None ->
                    if passable |> Option.exists (fun l -> valueAt l x y = 0) then '#'
                    elif opaque |> Option.exists (fun l -> valueAt l x y = 1) then 'o'
                    else
                        match elevation with
                        | Some l ->
                            let v = valueAt l x y
                            if v < 0 then '-'
                            elif v = 0 then '.'
                            elif v > 9 then '^'
                            else char (int '0' + v)
                        | None -> '.'

        line "# CommandoWar diagnostic frame"
        line (sprintf "tick %d" frame.Tick)
        line ""

        // Column ruler: tens then units.
        let gutter = "    "
        append gutter
        for x in 0 .. b.Width - 1 do
            append (if x >= 10 then string ((x / 10) % 10) else " ")
        line ""
        append gutter
        for x in 0 .. b.Width - 1 do
            append (string (x % 10))
        line ""

        for y in 0 .. b.Height - 1 do
            append (sprintf "%3d " y)
            for x in 0 .. b.Width - 1 do
                append (string (glyph x y))
            line ""

        line ""

        // Cover grid.
        let coverDirs (x: int) (y: int) =
            frame.Edges
            |> Array.filter (fun e -> e.Cell.X = x && e.Cell.Y = y)
            |> Array.map (fun e -> e.Direction)

        line "cover edges (N/E/S/W per cell, * for more than one):"
        append gutter
        for x in 0 .. b.Width - 1 do
            append (string (x % 10))
        line ""

        for y in 0 .. b.Height - 1 do
            append (sprintf "%3d " y)
            for x in 0 .. b.Width - 1 do
                let ds = coverDirs x y
                let g =
                    match ds with
                    | [||] -> '.'
                    | [| d |] -> "NESW".[Direction.index d]
                    | _ -> '*'
                append (string g)
            line ""

        line ""

        // Legend.
        line "legend:"
        line "  @ friendly agent   X hostile agent"
        line "  # impassable        o opaque (passable)"
        line "  1-9 elevation       . open flat ground"

        // Movement cost note (composite mode only).
        if single.IsNone then
            match cost with
            | Some l ->
                let patch =
                    [| for y in 0 .. b.Height - 1 do
                           for x in 0 .. b.Width - 1 do
                               let v = valueAt l x y
                               if v > Terrain.BaseMoveCost then sprintf "%s=%d" (cellText { X = x; Y = y }) v |]
                if patch.Length = 0 then
                    line "movement-cost: uniform"
                else
                    line (sprintf "movement-cost > base at: %s" (System.String.Join(" ", patch)))
            | None -> ()

        line ""

        // Agent roster.
        line "agents:"
        if frame.Agents.Length = 0 then
            line "  (none)"
        else
            for a in frame.Agents |> Array.sortBy (fun a -> a.Id) do
                let side =
                    match a.Side with
                    | Friendly -> "friendly"
                    | Hostile -> "hostile "
                let dest =
                    match a.Destination with
                    | Some d -> sprintf "-> %s" (cellText d)
                    | None -> "at rest"
                // Sub-cell progress toward the next cell (TASK-018): omitted
                // at 0 (at rest, or an edge just started) to keep the common
                // case quiet.
                let progress = if a.Progress > 0 then sprintf "  progress %d" a.Progress else ""
                // Communication availability (TASK-027): shown only when the
                // agent cannot receive orders, the `progress`-omitted-at-0
                // precedent.
                let comms = if a.CommunicationAvailable then "" else "  no-comms"
                line (sprintf "  agent %d  %s  %s  %s%s%s" (AgentId.value a.Id) side (cellText a.Cell) dest progress comms)

        // Overlays (empty unless a caller supplies one: a test, or
        // `cwheadless render --los`). `Diagnostics.frame` never emits one.
        if frame.Overlays.Length > 0 then
            line ""
            line "overlays:"
            for o in frame.Overlays do
                match o with
                | Cells(label, cells) ->
                    let cs = cells |> Array.map cellText |> String.concat " "
                    line (sprintf "  %s: %s" label cs)
                | SightRay(a, b, _, blocked) ->
                    let status =
                        match blocked with
                        | Some c -> sprintf "blocked at %s" (cellText c)
                        | None -> "clear"
                    line (sprintf "  sight %s -> %s: %s" (cellText a) (cellText b) status)
                | PlannedPath(a, b, _, cost, reached) ->
                    let status =
                        if reached then
                            sprintf "reached, cost %d" cost
                        else
                            "no path"
                    line (sprintf "  path %s -> %s: %s" (cellText a) (cellText b) status)
                | Reserved(cell, winner, untilTick) ->
                    line (
                        sprintf
                            "  reserved %s: agent %d (until tick %d)"
                            (cellText cell)
                            (AgentId.value winner)
                            untilTick
                    )
                | Obstructed(cell, occupant) ->
                    line (sprintf "  obstructed %s: held by agent %d" (cellText cell) (AgentId.value occupant))
                | KnownContact(cell, contact, confidence, lastSeenTick) ->
                    line (
                        sprintf
                            "  known contact %s: agent %d  confidence %d  seen tick %d"
                            (cellText cell)
                            (AgentId.value contact)
                            confidence
                            lastSeenTick
                    )
                | UndeliveredOrder(recipient, at, command) ->
                    line (
                        sprintf
                            "  undelivered order %s: agent %d  command %d  (communication unavailable)"
                            (cellText at)
                            (AgentId.value recipient)
                            (CommandId.value command)
                    )
                | OrderAppraisal(agent, at, disposition, exposedCells) ->
                    let exposed =
                        if exposedCells.Length = 0 then
                            ""
                        else
                            "  exposed " + (exposedCells |> Array.map cellText |> String.concat " ")

                    line (
                        sprintf
                            "  order appraisal %s: agent %d  %s%s"
                            (cellText at)
                            (AgentId.value agent)
                            (dispositionText disposition)
                            exposed
                    )
                | AgentCommitment(agent, at, commitment) ->
                    line (
                        sprintf "  commitment %s: agent %d  %s" (cellText at) (AgentId.value agent) (commitmentText commitment)
                    )
                | FireLine(shooter, from, target, at, hit) ->
                    line (
                        sprintf
                            "  fire %s -> %s: agent %d -> agent %d  %s"
                            (cellText from)
                            (cellText at)
                            (AgentId.value shooter)
                            (AgentId.value target)
                            (if hit then "hit" else "miss")
                    )
                | AgentSuppression(agent, at, suppression) ->
                    line (
                        sprintf
                            "  suppression %s: agent %d  %d/%d"
                            (cellText at)
                            (AgentId.value agent)
                            suppression
                            SuppressionConfig.MaxSuppression
                    )
                | AgentStress(agent, at, stress) ->
                    line (
                        sprintf
                            "  stress %s: agent %d  %d/%d"
                            (cellText at)
                            (AgentId.value agent)
                            stress
                            StressConfig.MaxStress
                    )
                | HostileKnownContact(cell, contact, confidence, lastSeenTick) ->
                    line (
                        sprintf
                            "  hostile known contact %s: agent %d  confidence %d  seen tick %d"
                            (cellText cell)
                            (AgentId.value contact)
                            confidence
                            lastSeenTick
                    )
                | AgentOrderQueue(agent, at, queued) ->
                    let qs =
                        queued
                        |> Array.map (fun (cmd, intent) -> sprintf "#%d %s" (CommandId.value cmd) (intentText intent))
                        |> String.concat ", "

                    line (sprintf "  order queue %s: agent %d  [%s]" (cellText at) (AgentId.value agent) qs)
                | AgentVitals(agent, at, vitals) ->
                    line (sprintf "  vitals %s: agent %d  %s" (cellText at) (AgentId.value agent) (vitalsText vitals))
                | SquadLeadership leader ->
                    let text =
                        match leader with
                        | Some id -> sprintf "agent %d" (AgentId.value id)
                        | None -> "none (every friendly down)"

                    line (sprintf "  squad leader: %s" text)
                | AgentAmmo(agent, at, magazine, reserve, reloading) ->
                    line (
                        sprintf "  ammo %s: agent %d  %s" (cellText at) (AgentId.value agent) (ammoText magazine reserve reloading)
                    )
                | Divergence(section, agents) ->
                    let who =
                        if agents.Length = 0 then
                            ""
                        else
                            "  agent " + (agents |> Array.map (AgentId.value >> string) |> String.concat ",")

                    line (sprintf "  DIVERGED: first differing section %s%s" section who)
                | AgentRadioLost(agent, at) ->
                    line (sprintf "  radio lost %s: agent %d" (cellText at) (AgentId.value agent))
                | AgentPendingDelivery(agent, at, command, dueTick) ->
                    line (
                        sprintf
                            "  pending delivery %s: agent %d  command %d  due tick %d"
                            (cellText at)
                            (AgentId.value agent)
                            (CommandId.value command)
                            dueTick
                    )
                | AgentFormationSlot(agent, at, resolved) ->
                    line (
                        sprintf
                            "  formation slot %s: agent %d  -> %s"
                            (cellText at)
                            (AgentId.value agent)
                            (cellText resolved)
                    )
                | MissionStatus(outcome, completed, inProgress) ->
                    let outcomeText =
                        match outcome with
                        | InProgress -> "in-progress"
                        | Succeeded -> "succeeded"
                        | Failed -> "failed"

                    let completedText =
                        if completed.Length = 0 then
                            ""
                        else
                            "  completed " + (completed |> Array.map (ObjectiveId.value >> string) |> String.concat ",")

                    let progressText =
                        if inProgress.Length = 0 then
                            ""
                        else
                            "  in-progress "
                            + (inProgress
                               |> Array.map (fun (id, ticks) -> sprintf "%d:%d" (ObjectiveId.value id) ticks)
                               |> String.concat ",")

                    line (sprintf "  mission: %s%s%s" outcomeText completedText progressText)

        line ""

        // Events line.
        if frame.Events.Length = 0 then
            line "events: none"
        else
            let describe (e: EventMarker) =
                let cs = e.Cells |> Array.map cellText |> String.concat ","
                if cs = "" then e.Kind else sprintf "%s@%s" e.Kind cs
            line (sprintf "events: %s" (frame.Events |> Array.map describe |> String.concat "; "))

        line ""
        line (
            sprintf
                "tick %d | hash %s (format %d) | draws %d | agents %d | cover-edges %d | events %d"
                frame.Tick
                (hx frame.Hash)
                frame.Hash.Format
                frame.RandomDraws
                frame.Agents.Length
                frame.Edges.Length
                frame.Events.Length
        )

        sb.ToString()

    // --- SVG ----------------------------------------------------------

    /// Integer pixels per cell. Every coordinate the SVG renderer emits is an
    /// integer multiple or half-multiple of this; there is no floating point.
    [<Literal>]
    let private Scale = 16

    let private esc (s: string) : string =
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")

    /// Cells as rects (elevation -> integer fill lightness, impassable ->
    /// hatch, opaque -> dark border), directional cover -> small triangles on
    /// the covered edge, agents -> circles coloured by side with a dashed
    /// line to the destination, this-tick event cells -> amber rings, and a
    /// footer with the determinism trio. Byte-deterministic: no timestamps,
    /// no generated ids.
    let Svg (frame: DiagnosticFrame) : string =
        let b = frame.Bounds
        let s = Scale
        let w = b.Width * s
        let gridH = b.Height * s

        // A `Divergence` overlay (TASK-057, backlog B-050) adds one extra
        // footer line naming the first differing canonical section; the
        // per-agent highlight itself is drawn on the grid, not the footer.
        let divergenceText =
            frame.Overlays
            |> Array.tryPick (function
                | Divergence(section, _) -> Some section
                | _ -> None)

        // A `MissionStatus` overlay (TASK-062, backlog B-032) adds one extra
        // footer line, the `Divergence` precedent -- there is no single cell
        // to anchor a mission-wide outcome on.
        let missionText =
            frame.Overlays
            |> Array.tryPick (function
                | MissionStatus(outcome, completed, inProgress) ->
                    let outcomeText =
                        match outcome with
                        | InProgress -> "in-progress"
                        | Succeeded -> "succeeded"
                        | Failed -> "failed"

                    let completedText =
                        if completed.Length = 0 then
                            ""
                        else
                            "  completed " + (completed |> Array.map (ObjectiveId.value >> string) |> String.concat ",")

                    let progressText =
                        if inProgress.Length = 0 then
                            ""
                        else
                            "  in-progress "
                            + (inProgress
                               |> Array.map (fun (id, ticks) -> sprintf "%d:%d" (ObjectiveId.value id) ticks)
                               |> String.concat ",")

                    Some(sprintf "mission: %s%s%s" outcomeText completedText progressText)
                | _ -> None)

        let extraFooterLines =
            (if divergenceText.IsSome then 1 else 0) + (if missionText.IsSome then 1 else 0)

        let footerH = 52 + extraFooterLines * 14
        let h = gridH + footerH

        let elevation = layer LayerName.Elevation frame
        let passable = layer LayerName.Passability frame
        let opaque = layer LayerName.Opacity frame

        let sb = StringBuilder()
        let line (str: string) = sb.Append(str).Append('\n') |> ignore

        line (sprintf "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"%d\" height=\"%d\" viewBox=\"0 0 %d %d\">" w h w h)
        line "  <defs>"
        line "    <pattern id=\"cw-hatch\" width=\"4\" height=\"4\" patternUnits=\"userSpaceOnUse\">"
        line "      <path d=\"M0,4 L4,0\" stroke=\"#8a8a8a\" stroke-width=\"1\"/>"
        line "    </pattern>"
        line "  </defs>"
        line (sprintf "  <rect x=\"0\" y=\"0\" width=\"%d\" height=\"%d\" fill=\"#ffffff\"/>" w h)

        // Terrain cells.
        for y in 0 .. b.Height - 1 do
            for x in 0 .. b.Width - 1 do
                let px = x * s
                let py = y * s
                let isPassable =
                    passable |> Option.forall (fun l -> valueAt l x y <> 0)
                let isOpaque =
                    opaque |> Option.exists (fun l -> valueAt l x y = 1)
                let e = elevation |> Option.map (fun l -> valueAt l x y) |> Option.defaultValue 0

                let fill =
                    if not isPassable then
                        "url(#cw-hatch)"
                    else
                        let v = max 70 (min 245 (238 - e * 24))
                        sprintf "rgb(%d,%d,%d)" v v v

                let stroke, sw = if isOpaque then "#333333", 2 else "#cccccc", 1

                line (
                    sprintf
                        "  <rect x=\"%d\" y=\"%d\" width=\"%d\" height=\"%d\" fill=\"%s\" stroke=\"%s\" stroke-width=\"%d\"/>"
                        px py s s fill stroke sw
                )

        // Cover triangles.
        let mid = s / 2
        let t = s / 4
        for em in frame.Edges do
            let px = em.Cell.X * s
            let py = em.Cell.Y * s
            let pts =
                match em.Direction with
                | North -> sprintf "%d,%d %d,%d %d,%d" (px + mid) py (px + mid - t) (py + t) (px + mid + t) (py + t)
                | South ->
                    sprintf "%d,%d %d,%d %d,%d" (px + mid) (py + s) (px + mid - t) (py + s - t) (px + mid + t) (py + s - t)
                | East ->
                    sprintf "%d,%d %d,%d %d,%d" (px + s) (py + mid) (px + s - t) (py + mid - t) (px + s - t) (py + mid + t)
                | West -> sprintf "%d,%d %d,%d %d,%d" px (py + mid) (px + t) (py + mid - t) (px + t) (py + mid + t)
            line (sprintf "  <polygon points=\"%s\" fill=\"#38a169\"/>" pts)

        // Event rings.
        for ev in frame.Events do
            for c in ev.Cells do
                line (
                    sprintf
                        "  <rect x=\"%d\" y=\"%d\" width=\"%d\" height=\"%d\" fill=\"none\" stroke=\"#d69e2e\" stroke-width=\"2\"/>"
                        (c.X * s) (c.Y * s) s s
                )

        // Agents.
        for a in frame.Agents |> Array.sortBy (fun a -> a.Id) do
            let cx = a.Cell.X * s + mid
            let cy = a.Cell.Y * s + mid
            let colour =
                match a.Side with
                | Friendly -> "#2b6cb0"
                | Hostile -> "#c53030"
            match a.Destination with
            | Some d ->
                line (
                    sprintf
                        "  <line x1=\"%d\" y1=\"%d\" x2=\"%d\" y2=\"%d\" stroke=\"%s\" stroke-width=\"1\" stroke-dasharray=\"3,2\"/>"
                        cx cy (d.X * s + mid) (d.Y * s + mid) colour
                )
            | None -> ()
            line (
                sprintf
                    "  <circle cx=\"%d\" cy=\"%d\" r=\"5\" fill=\"%s\" stroke=\"#ffffff\" stroke-width=\"1\"/>"
                    cx cy colour
            )

            // Communication unavailable (TASK-027): a dashed red ring around
            // the agent, drawn only when the agent cannot receive orders.
            if not a.CommunicationAvailable then
                line (
                    sprintf
                        "  <circle cx=\"%d\" cy=\"%d\" r=\"7\" fill=\"none\" stroke=\"#e53e3e\" stroke-width=\"1\" stroke-dasharray=\"2,1\"/>"
                        cx cy
                )

            // Sub-cell progress toward the next cell (TASK-018), omitted at 0.
            if a.Progress > 0 then
                line (
                    sprintf
                        "  <text x=\"%d\" y=\"%d\" font-family=\"monospace\" font-size=\"8\" fill=\"%s\">%d</text>"
                        (cx + 6) (cy - 6) colour a.Progress
                )

        // Overlays: line-of-sight rays (dashed line, traced-cell dots, a red
        // cross on the blocker), planned paths (solid polyline, a green start
        // disc, an orange goal box), same-tick cell reservations (a pink
        // dashed box labelled with the winning agent id), and generic
        // labelled cell sets. Empty unless a caller supplied one, or
        // `Diagnostics.frameOf` derived one; existing renders are
        // byte-identical without it.
        for o in frame.Overlays do
            match o with
            | Cells(_, cells) ->
                for c in cells do
                    line (
                        sprintf
                            "  <rect x=\"%d\" y=\"%d\" width=\"%d\" height=\"%d\" fill=\"#6b46c1\" fill-opacity=\"0.3\"/>"
                            (c.X * s) (c.Y * s) s s
                    )
            | SightRay(a, b, cells, blocked) ->
                line (
                    sprintf
                        "  <line x1=\"%d\" y1=\"%d\" x2=\"%d\" y2=\"%d\" stroke=\"#6b46c1\" stroke-width=\"2\" stroke-dasharray=\"4,3\"/>"
                        (a.X * s + mid) (a.Y * s + mid) (b.X * s + mid) (b.Y * s + mid)
                )

                for c in cells do
                    line (
                        sprintf "  <circle cx=\"%d\" cy=\"%d\" r=\"2\" fill=\"#6b46c1\"/>" (c.X * s + mid) (c.Y * s + mid)
                    )

                match blocked with
                | Some c ->
                    line (
                        sprintf
                            "  <rect x=\"%d\" y=\"%d\" width=\"%d\" height=\"%d\" fill=\"none\" stroke=\"#c53030\" stroke-width=\"3\"/>"
                            (c.X * s) (c.Y * s) s s
                    )

                    line (
                        sprintf
                            "  <line x1=\"%d\" y1=\"%d\" x2=\"%d\" y2=\"%d\" stroke=\"#c53030\" stroke-width=\"2\"/>"
                            (c.X * s) (c.Y * s) (c.X * s + s) (c.Y * s + s)
                    )

                    line (
                        sprintf
                            "  <line x1=\"%d\" y1=\"%d\" x2=\"%d\" y2=\"%d\" stroke=\"#c53030\" stroke-width=\"2\"/>"
                            (c.X * s + s) (c.Y * s) (c.X * s) (c.Y * s + s)
                    )
                | None -> ()
            | PlannedPath(a, target, cells, _, _) ->
                if cells.Length >= 2 then
                    let pts =
                        cells
                        |> Array.map (fun c -> sprintf "%d,%d" (c.X * s + mid) (c.Y * s + mid))
                        |> String.concat " "

                    line (
                        sprintf
                            "  <polyline points=\"%s\" fill=\"none\" stroke=\"#dd6b20\" stroke-width=\"2\"/>"
                            pts
                    )

                line (
                    sprintf
                        "  <circle cx=\"%d\" cy=\"%d\" r=\"4\" fill=\"#2f855a\" stroke=\"#ffffff\" stroke-width=\"1\"/>"
                        (a.X * s + mid) (a.Y * s + mid)
                )

                line (
                    sprintf
                        "  <rect x=\"%d\" y=\"%d\" width=\"%d\" height=\"%d\" fill=\"none\" stroke=\"#dd6b20\" stroke-width=\"3\"/>"
                        (target.X * s) (target.Y * s) s s
                )
            | Reserved(cell, winner, _) ->
                line (
                    sprintf
                        "  <rect x=\"%d\" y=\"%d\" width=\"%d\" height=\"%d\" fill=\"none\" stroke=\"#d53f8c\" stroke-width=\"2\" stroke-dasharray=\"2,2\"/>"
                        (cell.X * s) (cell.Y * s) s s
                )

                line (
                    sprintf
                        "  <text x=\"%d\" y=\"%d\" font-family=\"monospace\" font-size=\"9\" fill=\"#d53f8c\">R%d</text>"
                        (cell.X * s + 1)
                        (cell.Y * s + s - 2)
                        (AgentId.value winner)
                )
            | Obstructed(cell, occupant) ->
                line (
                    sprintf
                        "  <rect x=\"%d\" y=\"%d\" width=\"%d\" height=\"%d\" fill=\"none\" stroke=\"#c53030\" stroke-width=\"2\" stroke-dasharray=\"3,2\"/>"
                        (cell.X * s) (cell.Y * s) s s
                )

                line (
                    sprintf
                        "  <text x=\"%d\" y=\"%d\" font-family=\"monospace\" font-size=\"9\" fill=\"#c53030\">B%d</text>"
                        (cell.X * s + 1)
                        (cell.Y * s + s - 2)
                        (AgentId.value occupant)
                )
            | KnownContact(cell, contact, _, _) ->
                // The squad's last-known cell for a contact: a purple dashed
                // ring labelled `?<id>`, deliberately distinct from a live
                // agent circle. It can sit away from that agent's real
                // `AgentMarker` position when the contact has moved unseen.
                line (
                    sprintf
                        "  <circle cx=\"%d\" cy=\"%d\" r=\"6\" fill=\"none\" stroke=\"#805ad5\" stroke-width=\"2\" stroke-dasharray=\"3,2\"/>"
                        (cell.X * s + mid)
                        (cell.Y * s + mid)
                )

                line (
                    sprintf
                        "  <text x=\"%d\" y=\"%d\" font-family=\"monospace\" font-size=\"9\" fill=\"#805ad5\">?%d</text>"
                        (cell.X * s + 1)
                        (cell.Y * s + s - 2)
                        (AgentId.value contact)
                )
            | UndeliveredOrder(recipient, at, _) ->
                // An order that failed to reach `recipient` this tick: a red
                // dashed box over the recipient's cell with a struck-through
                // radio glyph, deliberately unlike an agent circle or a
                // `KnownContact` ring.
                line (
                    sprintf
                        "  <rect x=\"%d\" y=\"%d\" width=\"%d\" height=\"%d\" fill=\"none\" stroke=\"#e53e3e\" stroke-width=\"2\" stroke-dasharray=\"2,2\"/>"
                        (at.X * s) (at.Y * s) s s
                )

                line (
                    sprintf
                        "  <line x1=\"%d\" y1=\"%d\" x2=\"%d\" y2=\"%d\" stroke=\"#e53e3e\" stroke-width=\"2\"/>"
                        (at.X * s) (at.Y * s + s) (at.X * s + s) (at.Y * s)
                )

                line (
                    sprintf
                        "  <text x=\"%d\" y=\"%d\" font-family=\"monospace\" font-size=\"9\" fill=\"#e53e3e\">!%d</text>"
                        (at.X * s + 1)
                        (at.Y * s + s - 2)
                        (AgentId.value recipient)
                )
            | OrderAppraisal(_, at, disposition, exposedCells) ->
                // The Appraisal phase's outcome for one agent's order
                // (TASK-028): the exposed candidate-route cells as translucent
                // red squares, then a disposition-coloured corner glyph
                // (A accepted / R refused / U unable) at the agent's cell,
                // deliberately unlike a PlannedPath polyline or a KnownContact
                // ring.
                for c in exposedCells do
                    line (
                        sprintf
                            "  <rect x=\"%d\" y=\"%d\" width=\"%d\" height=\"%d\" fill=\"#c53030\" fill-opacity=\"0.25\"/>"
                            (c.X * s) (c.Y * s) s s
                    )

                let glyph, colour = dispositionGlyph disposition

                line (
                    sprintf
                        "  <text x=\"%d\" y=\"%d\" font-family=\"monospace\" font-size=\"9\" font-weight=\"bold\" fill=\"%s\">%s</text>"
                        (at.X * s + s - 7)
                        (at.Y * s + 9)
                        colour
                        glyph
                )
            | AgentCommitment(_, at, commitment) ->
                // The agent's current commitment (TASK-030): a small teal
                // marker at the top-left corner of the agent's cell, filled
                // teal for Holding, hollow for Moving, filled orange for
                // Suppressing (TASK-037 — the FireLine "hit" colour, both
                // reading as "active fire") — deliberately distinct from the
                // OrderAppraisal disposition glyph (bottom-right corner) and
                // the PlannedPath polyline.
                let fill =
                    match commitment with
                    | Holding -> "#2c7a7b"
                    | Moving _ -> "none"
                    | Suppressing _ -> "#dd6b20"
                    | Withdrawing _ -> "#3182ce"
                    | Assaulting _ -> "#e53e3e"

                line (
                    sprintf
                        "  <circle cx=\"%d\" cy=\"%d\" r=\"3\" fill=\"%s\" stroke=\"#2c7a7b\" stroke-width=\"1\"/>"
                        (at.X * s + 4)
                        (at.Y * s + 4)
                        fill
                )
            | FireLine(_, from, _, at, hit) ->
                // A deterministic hitscan shot (TASK-031): a line between the
                // shooter's and target's cell centres, orange and solid for a
                // hit, grey and dashed for a miss — deliberately distinct from
                // the PlannedPath polyline and the OrderAppraisal glyph.
                let colour, dash = if hit then "#dd6b20", "" else "#a0aec0", " stroke-dasharray=\"3,2\""

                line (
                    sprintf
                        "  <line x1=\"%d\" y1=\"%d\" x2=\"%d\" y2=\"%d\" stroke=\"%s\" stroke-width=\"1.5\"%s/>"
                        (from.X * s + mid)
                        (from.Y * s + mid)
                        (at.X * s + mid)
                        (at.Y * s + mid)
                        colour
                        dash
                )
            | AgentSuppression(_, at, suppression) ->
                // Current suppression (TASK-032): a small red marker at the
                // top-right corner of the agent's cell, opacity scaled by
                // suppression / MaxSuppression so accumulation and decay are
                // visible across ticks — deliberately the one unused corner
                // (AgentCommitment top-left, OrderAppraisal bottom-right).
                let opacity = float suppression / float SuppressionConfig.MaxSuppression

                line (
                    sprintf
                        "  <circle cx=\"%d\" cy=\"%d\" r=\"3\" fill=\"#c53030\" fill-opacity=\"%.2f\"/>"
                        (at.X * s + s - 4)
                        (at.Y * s + 4)
                        opacity
                )
            | AgentStress(_, at, stress) ->
                // Current stress (TASK-033): a small purple marker at the
                // bottom-left corner of the agent's cell, opacity scaled by
                // stress / MaxStress — the AgentSuppression precedent, in the
                // one remaining unused corner (AgentCommitment top-left,
                // AgentSuppression top-right, OrderAppraisal bottom-right).
                let opacity = float stress / float StressConfig.MaxStress

                line (
                    sprintf
                        "  <circle cx=\"%d\" cy=\"%d\" r=\"3\" fill=\"#6b46c1\" fill-opacity=\"%.2f\"/>"
                        (at.X * s + 4)
                        (at.Y * s + s - 4)
                        opacity
                )
            | HostileKnownContact(cell, contact, _, _) ->
                // The Hostile side's own last-known cell for a contact
                // (TASK-034, backlog B-022 partial): an amber dashed ring
                // labelled `H<id>`, deliberately distinct from the friendly
                // squad's purple dashed `KnownContact` ring (Decision C — the
                // two pictures must stay visually distinguishable without
                // cross-referencing `WorldState.Agents`).
                line (
                    sprintf
                        "  <circle cx=\"%d\" cy=\"%d\" r=\"6\" fill=\"none\" stroke=\"#b7791f\" stroke-width=\"2\" stroke-dasharray=\"3,2\"/>"
                        (cell.X * s + mid)
                        (cell.Y * s + mid)
                )

                line (
                    sprintf
                        "  <text x=\"%d\" y=\"%d\" font-family=\"monospace\" font-size=\"9\" fill=\"#b7791f\">H%d</text>"
                        (cell.X * s + 1)
                        (cell.Y * s + s - 2)
                        (AgentId.value contact)
                )
            | AgentOrderQueue(_, at, queued) ->
                // Orders stacked behind the active order (TASK-044, backlog
                // B-051): a small "+N" badge centred on the cell's top edge
                // — the four corners are already used (AgentCommitment
                // top-left, AgentSuppression top-right, AgentStress
                // bottom-left, OrderAppraisal bottom-right).
                line (
                    sprintf
                        "  <text x=\"%d\" y=\"%d\" font-family=\"monospace\" font-size=\"8\" fill=\"#2b6cb0\">+%d</text>"
                        (at.X * s + mid - 4)
                        (at.Y * s + 8)
                        queued.Length
                )
            | AgentVitals(_, at, vitals) ->
                // Casualty state (TASK-045, backlog B-031): the common case
                // (full health) draws nothing, the CommunicationAvailable =
                // true precedent — only a wounded/incapacitated/dead agent
                // gets a marker. A wound is a small red dot centred on the
                // cell's bottom edge (the one remaining unused edge
                // midpoint, `AgentOrderQueue`'s own top-edge-center
                // precedent); `Incapacitated`/`Dead` override with their own
                // distinct marker instead, since both matter more than a
                // wound-severity dot once the agent is down.
                match vitals with
                | Alive health when health >= Agent.MaxHealth -> ()
                | Alive health ->
                    let opacity = float (Agent.MaxHealth - health) / float Agent.MaxHealth

                    line (
                        sprintf
                            "  <circle cx=\"%d\" cy=\"%d\" r=\"3\" fill=\"#e53e3e\" fill-opacity=\"%.2f\"/>"
                            (at.X * s + mid)
                            (at.Y * s + s - 4)
                            opacity
                    )
                | Incapacitated remaining ->
                    line (
                        sprintf
                            "  <text x=\"%d\" y=\"%d\" font-family=\"monospace\" font-size=\"8\" fill=\"#718096\">Z%d</text>"
                            (at.X * s + mid - 4)
                            (at.Y * s + s - 2)
                            remaining
                    )
                | Dead ->
                    // A black cross over the whole cell — the SightRay
                    // blocked-cell cross precedent, reused here since both
                    // mean "nothing more happens at this cell".
                    line (
                        sprintf
                            "  <line x1=\"%d\" y1=\"%d\" x2=\"%d\" y2=\"%d\" stroke=\"#000000\" stroke-width=\"2\"/>"
                            (at.X * s) (at.Y * s) (at.X * s + s) (at.Y * s + s)
                    )

                    line (
                        sprintf
                            "  <line x1=\"%d\" y1=\"%d\" x2=\"%d\" y2=\"%d\" stroke=\"#000000\" stroke-width=\"2\"/>"
                            (at.X * s + s) (at.Y * s) (at.X * s) (at.Y * s + s)
                    )
            | SquadLeadership leader ->
                // A solid gold ring around the leader's own agent marker
                // (TASK-045, backlog B-031) — encircling the agent rather
                // than adding another corner badge, since every corner and
                // edge midpoint around the cell is already spoken for.
                // Nothing drawn when no leader survives (every friendly
                // down — `AgentVitals`'s per-agent `Dead` cross already
                // shows that).
                match leader with
                | None -> ()
                | Some id ->
                    match frame.Agents |> Array.tryFind (fun a -> a.Id = id) with
                    | None -> ()
                    | Some a ->
                        line (
                            sprintf
                                "  <circle cx=\"%d\" cy=\"%d\" r=\"8\" fill=\"none\" stroke=\"#d4af37\" stroke-width=\"2\"/>"
                                (a.Cell.X * s + mid)
                                (a.Cell.Y * s + mid)
                        )
            | AgentAmmo(_, at, _, _, reloading) ->
                // Ammunition (TASK-047, backlog B-030 proper): every corner
                // and edge midpoint around the cell is already spoken for
                // (AgentCommitment top-left, AgentSuppression top-right,
                // AgentStress bottom-left, OrderAppraisal bottom-right,
                // AgentOrderQueue top-edge, AgentVitals bottom-edge), so a
                // mid-reload agent gets a dashed amber ring one radius out
                // from the main agent circle instead of a corner badge; a
                // merely low-on-ammo (but not reloading) agent draws nothing
                // in SVG — the full magazine/reserve count is developer text
                // detail, `Ascii`/HTML's job, not scoped for a new SVG glyph.
                if reloading then
                    line (
                        sprintf
                            "  <circle cx=\"%d\" cy=\"%d\" r=\"6\" fill=\"none\" stroke=\"#b7791f\" stroke-width=\"1\" stroke-dasharray=\"2,1\"/>"
                            (at.X * s + mid)
                            (at.Y * s + mid)
                    )
            | Divergence(_, agents) ->
                // First-divergence marker (TASK-057, backlog B-050): a thick
                // solid magenta square around each named agent's cell,
                // deliberately unlike every other overlay's thin/dashed
                // stroke, since this is the one thing the render exists to
                // draw attention to. The section text itself is footer-only
                // (below) -- there is no single cell to put it on when the
                // divergence names no agent.
                for agent in agents do
                    match frame.Agents |> Array.tryFind (fun a -> a.Id = agent) with
                    | None -> ()
                    | Some a ->
                        line (
                            sprintf
                                "  <rect x=\"%d\" y=\"%d\" width=\"%d\" height=\"%d\" fill=\"none\" stroke=\"#ff00ff\" stroke-width=\"4\"/>"
                                (a.Cell.X * s) (a.Cell.Y * s) s s
                        )
            | AgentRadioLost(_, at) ->
                // Radio destroyed (TASK-058, backlog B-016b): a dark-red
                // diagonal cross directly over the agent's circle -- unlike
                // AgentAmmo's dashed ring one radius out, this sits ON the
                // agent since a destroyed radio is a property of the agent
                // itself, not a status ring around it. Only ever present
                // when the world authors a Headquarters (the opt-in gate).
                let cx = at.X * s + mid
                let cy = at.Y * s + mid
                let r = 5

                line (
                    sprintf
                        "  <line x1=\"%d\" y1=\"%d\" x2=\"%d\" y2=\"%d\" stroke=\"#4A0E0E\" stroke-width=\"2\"/>"
                        (cx - r) (cy - r) (cx + r) (cy + r)
                )

                line (
                    sprintf
                        "  <line x1=\"%d\" y1=\"%d\" x2=\"%d\" y2=\"%d\" stroke=\"#4A0E0E\" stroke-width=\"2\"/>"
                        (cx - r) (cy + r) (cx + r) (cy - r)
                )
            | AgentPendingDelivery(_, at, _, dueTick) ->
                // In-flight order (TASK-058, backlog B-016b): a small dotted
                // ring one radius out (the AgentAmmo reloading-ring
                // precedent) in a distinct blue, plus the due tick as text.
                line (
                    sprintf
                        "  <circle cx=\"%d\" cy=\"%d\" r=\"8\" fill=\"none\" stroke=\"#2B6CB0\" stroke-width=\"1\" stroke-dasharray=\"1,2\"/>"
                        (at.X * s + mid)
                        (at.Y * s + mid)
                )

                line (
                    sprintf
                        "  <text x=\"%d\" y=\"%d\" font-family=\"monospace\" font-size=\"8\" fill=\"#2B6CB0\">@%d</text>"
                        (at.X * s + 1)
                        (at.Y * s + 3)
                        dueTick
                )
            | AgentFormationSlot(_, at, resolved) ->
                // Formation slot resolution (TASK-059, backlog B-011d): a
                // small teal diamond at the resolved destination, connected
                // to the agent's current position by a thin dashed line --
                // distinguishable from PlannedPath's route line (which
                // traces the full path, not just the endpoint) and from
                // every other overlay's corner-badge convention.
                let rx = resolved.X * s + mid
                let ry = resolved.Y * s + mid
                let ax = at.X * s + mid
                let ay = at.Y * s + mid

                line (
                    sprintf
                        "  <line x1=\"%d\" y1=\"%d\" x2=\"%d\" y2=\"%d\" stroke=\"#0F766E\" stroke-width=\"1\" stroke-dasharray=\"3,2\"/>"
                        ax ay rx ry
                )

                line (
                    sprintf
                        "  <rect x=\"%d\" y=\"%d\" width=\"8\" height=\"8\" fill=\"none\" stroke=\"#0F766E\" stroke-width=\"2\" transform=\"rotate(45 %d %d)\"/>"
                        (rx - 4) (ry - 4) rx ry
                )
            | MissionStatus _ -> () // footer-only, the Divergence precedent (no single cell to anchor on)

        // Footer.
        let footerText (dy: int) (str: string) =
            line (
                sprintf
                    "  <text x=\"4\" y=\"%d\" font-family=\"monospace\" font-size=\"11\" fill=\"#111111\">%s</text>"
                    (gridH + dy) (esc str)
            )
        footerText 16 (sprintf "tick %d  hash %s  draws %d" frame.Tick (hx frame.Hash) frame.RandomDraws)
        footerText 30 (sprintf "agents %d  cover-edges %d  events %d" frame.Agents.Length frame.Edges.Length frame.Events.Length)
        footerText 44 "hatch=impassable  dark border=opaque  darker fill=higher elevation  triangle=cover  circle=agent"
        match divergenceText with
        | Some section -> footerText 58 (sprintf "DIVERGED: first differing section %s" section)
        | None -> ()

        match missionText with
        | Some text -> footerText (58 + (if divergenceText.IsSome then 14 else 0)) text
        | None -> ()

        line "</svg>"
        sb.ToString()

    // --- HTML annotations -------------------------------------------------

    /// A readable sentence for one this-tick `EventMarker`, used only by the
    /// HTML narration panel. Distinct from `Ascii`'s terse `kind@cell,cell`
    /// events line: this is prose for a reader scrubbing a run, built from
    /// the same `Kind` / `Cells` / `Agents` fields, so it needs nothing
    /// `Ascii` does not already have. Names the agent(s) the event concerns
    /// (TASK-035) — `Agents.[0]` is always the primary agent, `Agents.[1]`
    /// the second party for a two-agent event (`ShotFired`,
    /// `MovementYielded`, `MovementObstructed`, `ContactObserved`).
    let private eventNarration (e: EventMarker) : string =
        let c i = cellText e.Cells.[i]
        let a i = AgentId.value e.Agents.[i]
        match e.Kind with
        | "command-accepted" -> sprintf "Agent %d: order accepted, destination %s." (a 0) (c 0)
        | "command-rejected" ->
            if e.Agents.Length > 0 then
                sprintf "Agent %d: order rejected." (a 0)
            elif e.Cells.Length > 0 then
                sprintf "Order rejected (target %s out of bounds)." (c 0)
            else
                "Order rejected."
        | "order-undelivered" -> sprintf "Agent %d: order undelivered (communication unavailable)." (a 0)
        | "order-queued" -> sprintf "Agent %d: order queued behind its active order." (a 0)
        | "order-cancelled-active" -> sprintf "Agent %d: active order cancelled." (a 0)
        | "order-cancelled-queued" -> sprintf "Agent %d: queued order cancelled." (a 0)
        | "order-appraised" -> sprintf "Agent %d: order appraised." (a 0)
        | "commitment-established" -> sprintf "Agent %d: new commitment toward %s." (a 0) (c 0)
        | "commitment-completed" -> sprintf "Agent %d: commitment completed at %s." (a 0) (c 0)
        | "shot-fired-hit" -> sprintf "Agent %d fired at agent %d: hit." (a 0) (a 1)
        | "shot-fired-miss" -> sprintf "Agent %d fired at agent %d: miss." (a 0) (a 1)
        | "movement-stepped" -> sprintf "Agent %d moved %s to %s." (a 0) (c 0) (c 1)
        | "movement-completed" -> sprintf "Agent %d arrived at %s." (a 0) (c 0)
        | "movement-blocked" -> sprintf "Agent %d: movement blocked at %s toward %s." (a 0) (c 0) (c 1)
        | "movement-yielded" -> sprintf "Agent %d yielded at %s to agent %d (cell %s contested)." (a 0) (c 0) (a 1) (c 1)
        | "movement-obstructed" ->
            sprintf "Agent %d obstructed at %s by agent %d (blocked cell %s)." (a 0) (c 0) (a 1) (c 1)
        | "contact-observed" -> sprintf "Agent %d observed agent %d at %s." (a 0) (a 1) (c 0)
        | "contact-expired" -> sprintf "Contact (agent %d) expired (last seen %s)." (a 0) (c 0)
        | "agent-incapacitated" -> sprintf "Agent %d incapacitated at %s (bleeding out)." (a 0) (c 0)
        | "agent-died" -> sprintf "Agent %d died at %s." (a 0) (c 0)
        | "leadership-transferred" ->
            if e.Agents.Length > 0 then
                sprintf "Squad leadership transferred to agent %d." (a 0)
            else
                "Squad leadership lost (no friendly agent remains)."
        | "squad-failure" -> "Squad failure: every friendly agent is down."
        | other -> other

    /// One `<tr>` of per-agent state for the HTML annotation panel: position,
    /// destination/progress, comms, order disposition (TASK-028), commitment
    /// (TASK-030), suppression (TASK-032), and stress (TASK-033) joined per
    /// agent — the same fields the ASCII roster line and overlay lines
    /// already report, read off `frame.Overlays`. The lookups are sparse
    /// (`OrderAppraisal` / `AgentSuppression` / `AgentStress` are emitted only
    /// when meaningful), so a missing entry renders as "-" or `0`.
    let private agentRow
        (dispositions: Map<AgentId, OrderDisposition>)
        (commitments: Map<AgentId, Commitment>)
        (suppressions: Map<AgentId, int>)
        (stresses: Map<AgentId, int>)
        (queueDepths: Map<AgentId, int>)
        (vitals: Map<AgentId, VitalStatus>)
        (a: AgentMarker)
        : string =
        let side =
            match a.Side with
            | Friendly -> "friendly"
            | Hostile -> "hostile"

        let move =
            let dest =
                match a.Destination with
                | Some d -> sprintf "-> %s" (cellText d)
                | None -> "at rest"
            if a.Progress > 0 then sprintf "%s (progress %d)" dest a.Progress else dest

        let comms = if a.CommunicationAvailable then "yes" else "no"

        let order =
            dispositions |> Map.tryFind a.Id |> Option.map dispositionText |> Option.defaultValue "-"

        let commitment =
            commitments |> Map.tryFind a.Id |> Option.map commitmentText |> Option.defaultValue "-"

        let suppression = suppressions |> Map.tryFind a.Id |> Option.defaultValue 0
        let stress = stresses |> Map.tryFind a.Id |> Option.defaultValue 0
        let queueDepth = queueDepths |> Map.tryFind a.Id |> Option.defaultValue 0

        let vitalsStr =
            vitals |> Map.tryFind a.Id |> Option.map vitalsText |> Option.defaultValue "-"

        sprintf
            "<tr><td>%d</td><td>%s</td><td>%s</td><td>%s</td><td>%s</td><td>%s</td><td>%s</td><td>%d/%d</td><td>%d/%d</td><td>%d</td><td>%s</td></tr>"
            (AgentId.value a.Id)
            side
            (esc (cellText a.Cell))
            (esc move)
            comms
            (esc order)
            (esc commitment)
            suppression
            SuppressionConfig.MaxSuppression
            stress
            StressConfig.MaxStress
            queueDepth
            (esc vitalsStr)

    /// One entry of a shared tactical picture (`KnownContact` /
    /// `HostileKnownContact`), rendered the same way for both sides.
    let private contactText (cell: Cell, contact: AgentId, confidence: int, lastSeenTick: int64) : string =
        sprintf "agent %d @ %s (confidence %d, seen tick %d)" (AgentId.value contact) (cellText cell) confidence lastSeenTick

    /// The narration + per-agent panel shown next to each tick's SVG: a
    /// readable sentence per this-tick event (`Diagnostics.frameOf`'s
    /// `Events`, empty at tick 0), a table joining each agent's position,
    /// order disposition, commitment, suppression, and stress off
    /// `frame.Overlays`, and the two shared tactical pictures (`KnownContact`
    /// / `HostileKnownContact`, docs/04 section 12.4 — squad-shared state, so
    /// listed once rather than attached to one agent). Presentation only:
    /// reads nothing `Ascii` / `Svg` do not already read from the same frame.
    let private annotations (frame: DiagnosticFrame) : string =
        let sb = StringBuilder()
        let line (s: string) = sb.Append(s).Append('\n') |> ignore

        line "<div class=\"cw-annotations\">"

        line "<div class=\"cw-narration\">"
        if frame.Events.Length = 0 then
            line "<p>No events this tick.</p>"
        else
            line "<ul>"
            for e in frame.Events do
                line (sprintf "<li>%s</li>" (esc (eventNarration e)))
            line "</ul>"
        line "</div>"

        let dispositions =
            frame.Overlays
            |> Array.choose (function
                | OrderAppraisal(a, _, d, _) -> Some(a, d)
                | _ -> None)
            |> Map.ofArray

        let commitments =
            frame.Overlays
            |> Array.choose (function
                | AgentCommitment(a, _, c) -> Some(a, c)
                | _ -> None)
            |> Map.ofArray

        let suppressions =
            frame.Overlays
            |> Array.choose (function
                | AgentSuppression(a, _, s) -> Some(a, s)
                | _ -> None)
            |> Map.ofArray

        let stresses =
            frame.Overlays
            |> Array.choose (function
                | AgentStress(a, _, s) -> Some(a, s)
                | _ -> None)
            |> Map.ofArray

        // Queue depths (TASK-044, backlog B-051): AgentOrderQueue is sparse
        // (non-empty queues only), the AgentSuppression/AgentStress
        // precedent — a missing entry means an empty queue, rendered `0`.
        let queueDepths =
            frame.Overlays
            |> Array.choose (function
                | AgentOrderQueue(a, _, q) -> Some(a, q.Length)
                | _ -> None)
            |> Map.ofArray

        // Vitals (TASK-045, backlog B-031): AgentVitals is unconditional per
        // agent, unlike the sparse overlays above, so this map always has
        // every agent (agentRow's Option.defaultValue "-" fallback is
        // unreachable in practice, kept only for the same defensive shape
        // every other lookup here uses).
        let vitals =
            frame.Overlays
            |> Array.choose (function
                | AgentVitals(a, _, v) -> Some(a, v)
                | _ -> None)
            |> Map.ofArray

        line "<table class=\"cw-agents\">"
        line "<thead><tr><th>Agent</th><th>Side</th><th>Cell</th><th>Move</th><th>Comms</th><th>Order</th><th>Commitment</th><th>Suppression</th><th>Stress</th><th>Queue</th><th>Vitals</th></tr></thead>"
        line "<tbody>"
        for a in frame.Agents |> Array.sortBy (fun a -> a.Id) do
            line (agentRow dispositions commitments suppressions stresses queueDepths vitals a)
        line "</tbody>"
        line "</table>"

        // Squad leader (TASK-045, backlog B-031): one line, not a table row
        // — a squad-wide fact, not per-agent.
        match frame.Overlays |> Array.tryPick (function
            | SquadLeadership leader -> Some leader
            | _ -> None) with
        | Some(Some id) -> line (sprintf "<p>Squad leader: agent %d.</p>" (AgentId.value id))
        | Some None -> line "<p>Squad leader: none (every friendly is down).</p>"
        | None -> ()

        let knownContacts =
            frame.Overlays
            |> Array.choose (function
                | KnownContact(cell, contact, confidence, lastSeenTick) -> Some(cell, contact, confidence, lastSeenTick)
                | _ -> None)

        let hostileKnownContacts =
            frame.Overlays
            |> Array.choose (function
                | HostileKnownContact(cell, contact, confidence, lastSeenTick) -> Some(cell, contact, confidence, lastSeenTick)
                | _ -> None)

        let contactList (contacts: (Cell * AgentId * int * int64)[]) =
            if contacts.Length = 0 then
                "none"
            else
                contacts |> Array.map contactText |> String.concat "; " |> esc

        line "<div class=\"cw-contacts\">"
        line (sprintf "<p><strong>Squad tactical picture:</strong> %s</p>" (contactList knownContacts))
        line (sprintf "<p><strong>Hostile tactical picture:</strong> %s</p>" (contactList hostileKnownContacts))
        line "</div>"

        // First-divergence banner (TASK-057, backlog B-050): at most one
        // `Divergence` overlay per frame, the caller-supplied precedent
        // (`SightRay`/`PlannedPath`) -- absent from every ordinary run.
        match frame.Overlays |> Array.tryPick (function
            | Divergence(section, agents) -> Some(section, agents)
            | _ -> None) with
        | None -> ()
        | Some(section, agents) ->
            let who =
                if agents.Length = 0 then
                    ""
                else
                    " (agent " + (agents |> Array.map (AgentId.value >> string) |> String.concat ",") + ")"

            line (sprintf "<p class=\"cw-diverged\"><strong>DIVERGED:</strong> first differing section %s%s</p>" (esc section) (esc who))

        line "</div>"
        sb.ToString()

    // --- HTML ----------------------------------------------------------

    /// One self-contained XHTML file (inline CSS and JS, no external
    /// references) embedding one SVG per tick, plus a narration/agent-state
    /// annotation panel per tick (built by `annotations`), with a slider /
    /// prev / next to scrub the run. Deterministic bytes. This is the primary
    /// "reason about a run" artefact.
    let Html (frames: DiagnosticFrame[]) : string =
        let count = frames.Length
        let maxIndex = max 0 (count - 1)
        let sb = StringBuilder()
        let line (s: string) = sb.Append(s).Append('\n') |> ignore

        line "<!DOCTYPE html>"
        line "<html xmlns=\"http://www.w3.org/1999/xhtml\">"
        line "<head>"
        line "<meta charset=\"utf-8\"/>"
        line "<title>CommandoWar diagnostic run</title>"
        line "<style>/*<![CDATA[*/"
        line "body { font-family: monospace; margin: 12px; background: #ffffff; color: #111111; }"
        line "h1 { font-size: 16px; margin: 0 0 4px 0; }"
        line ".cw-controls { margin: 8px 0; }"
        line ".cw-frame { display: none; }"
        line ".cw-frame.cw-active { display: block; }"
        line ".cw-annotations { margin-top: 8px; max-width: 640px; }"
        line ".cw-narration ul { margin: 4px 0; padding-left: 20px; }"
        line ".cw-agents { border-collapse: collapse; font-size: 12px; margin: 8px 0; }"
        line ".cw-agents th, .cw-agents td { border: 1px solid #cccccc; padding: 2px 6px; text-align: left; }"
        line ".cw-contacts p { margin: 4px 0; }"
        line "/*]]>*/</style>"
        line "</head>"
        line "<body>"
        line "<h1>CommandoWar diagnostic run</h1>"
        line (sprintf "<p>%d frames. Drag the slider or use Prev / Next to scrub ticks.</p>" count)
        line "<div class=\"cw-controls\">"
        line "<button type=\"button\" id=\"cw-prev\">Prev</button>"
        line (sprintf "<input type=\"range\" id=\"cw-slider\" min=\"0\" max=\"%d\" value=\"0\"/>" maxIndex)
        line "<button type=\"button\" id=\"cw-next\">Next</button>"
        line "<span id=\"cw-label\">tick 0</span>"
        line "</div>"
        line "<div id=\"cw-frames\">"

        frames
        |> Array.iteri (fun i f ->
            let cls = if i = 0 then "cw-frame cw-active" else "cw-frame"
            line (sprintf "<div class=\"%s\" data-tick=\"%d\">" cls f.Tick)
            sb.Append(Svg f) |> ignore
            sb.Append(annotations f) |> ignore
            line "</div>")

        line "</div>"
        line "<script>/*<![CDATA[*/"
        line "(function () {"
        line "  var frames = document.querySelectorAll('.cw-frame');"
        line "  var slider = document.getElementById('cw-slider');"
        line "  var label = document.getElementById('cw-label');"
        line "  var prev = document.getElementById('cw-prev');"
        line "  var next = document.getElementById('cw-next');"
        line "  function show(i) {"
        line "    if (i < 0) { i = 0; }"
        line "    if (i > frames.length - 1) { i = frames.length - 1; }"
        line "    for (var j = 0; j < frames.length; j++) {"
        line "      frames[j].setAttribute('class', (j === i) ? 'cw-frame cw-active' : 'cw-frame');"
        line "    }"
        line "    slider.value = i;"
        line "    label.textContent = 'tick ' + frames[i].getAttribute('data-tick');"
        line "  }"
        line "  slider.addEventListener('input', function () { show(parseInt(slider.value, 10)); });"
        line "  prev.addEventListener('click', function () { show(parseInt(slider.value, 10) - 1); });"
        line "  next.addEventListener('click', function () { show(parseInt(slider.value, 10) + 1); });"
        line "  show(0);"
        line "}());"
        line "/*]]>*/</script>"
        line "</body>"
        line "</html>"
        sb.ToString()
