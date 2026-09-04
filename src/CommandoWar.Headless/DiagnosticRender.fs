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
                | Reserved _ -> None)

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
                | Reserved _ -> None)

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
                line (sprintf "  agent %d  %s  %s  %s" (AgentId.value a.Id) side (cellText a.Cell) dest)

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
        let footerH = 52
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

        line "</svg>"
        sb.ToString()

    // --- HTML ----------------------------------------------------------

    /// One self-contained XHTML file (inline CSS and JS, no external
    /// references) embedding one SVG per tick with a slider / prev / next to
    /// scrub the run. Deterministic bytes. This is the primary "reason about
    /// a run" artefact.
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
