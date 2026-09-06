module CommandoWar.Sim.Tests.DiagnosticsTests

open System
open System.IO
open System.Xml
open System.Xml.Linq
open Xunit
open CommandoWar.Sim
open CommandoWar.Headless

// Coverage for the framework-neutral diagnostic model
// (src/CommandoWar.Sim/Diagnostics.fs) and its deterministic headless
// renderers (src/CommandoWar.Headless/DiagnosticRender.fs). The renderer facts
// pin fresh output against the committed goldens in content/diagnostics/
// (copied next to the test assembly by the project file). The final fact pins
// that producing diagnostics for the shared fixture moves neither its hashes
// nor its event count.

let private goldenDir = Path.Combine(AppContext.BaseDirectory, "diagnostics")
let private golden (name: string) = File.ReadAllText(Path.Combine(goldenDir, name))

let private demoFrames () =
    DiagnosticRender.runFrames (DemoScenario.initialState ()) (DemoScenario.commandLog ()) DemoScenario.TickCount

let private countOccurrences (haystack: string) (needle: string) =
    let mutable count = 0
    let mutable i = haystack.IndexOf(needle, StringComparison.Ordinal)

    while i >= 0 do
        count <- count + 1
        i <- haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)

    count

// --- Diagnostics.frame: purity and content ---------------------------------

[<Fact>]
let ``Diagnostics.frame is a pure function: same world state gives an equal frame`` () =
    let w = DemoScenario.initialState ()
    let a = Diagnostics.frame w
    let b = Diagnostics.frame w
    Assert.True((a = b), "two frames of the same world state are not structurally equal")
    // Building a frame draws nothing from the stream.
    Assert.Equal(0UL, w.Random.Draws)

[<Fact>]
let ``the frame carries every terrain layer, the cover edges, and the determinism trio`` () =
    let w = DemoScenario.initialState ()
    let f = Diagnostics.frame w

    let names = f.Layers |> Array.map (fun l -> l.Name) |> Set.ofArray

    Assert.Equal<Set<string>>(
        Set.ofList [ LayerName.Elevation; LayerName.Passability; LayerName.MovementCost; LayerName.Opacity ],
        names
    )

    for l in f.Layers do
        Assert.Equal(w.Bounds.Width * w.Bounds.Height, l.Cells.Length)
        Assert.Equal<GridBounds>(w.Bounds, l.Bounds)

    // Cover edges reproduce Terrain.cover exactly.
    let expected =
        [ for y in 0 .. w.Bounds.Height - 1 do
              for x in 0 .. w.Bounds.Width - 1 do
                  for d in Direction.all do
                      let v = Terrain.cover w.Terrain { X = x; Y = y } d
                      if v > 0 then
                          yield ({ X = x; Y = y }, d, v) ]

    Assert.Equal(3, expected.Length)
    Assert.Equal(expected.Length, f.Edges.Length)

    for (c, d, v) in expected do
        Assert.Contains(f.Edges, fun (e: EdgeMarker) -> e.Cell = c && e.Direction = d && e.Value = v)

    Assert.Equal(0L, f.Tick)
    Assert.Equal(Hashing.hash w, f.Hash)
    Assert.Equal(0UL, f.RandomDraws)
    Assert.Empty(f.Overlays)

[<Fact>]
let ``frameOf carries agent destinations and this-tick event markers`` () =
    let atTick1 = (demoFrames ()).[1]
    Assert.True(
        atTick1.Agents |> Array.exists (fun a -> a.Destination.IsSome),
        "no agent carries a destination after the command tick"
    )
    Assert.NotEmpty(atTick1.Events)
    Assert.Contains(atTick1.Events, fun (e: EventMarker) -> e.Kind = "command-accepted")

[<Fact>]
let ``the fixture frame hash equals Hashing.hash of the same state and its draw count is zero`` () =
    let w = Fixture.initialState ()
    let f = Diagnostics.frame w
    Assert.Equal(Hashing.hash w, f.Hash)
    Assert.Equal(0xE13D7540912C7E25UL, f.Hash.Value)
    Assert.Equal(0UL, f.RandomDraws)

// --- renderers: golden byte-equality ------------------------------------

[<Fact>]
let ``ASCII render of the demo frame is byte-equal to the committed golden`` () =
    Assert.Equal(golden "demo.ascii.txt", DiagnosticRender.Ascii(Diagnostics.frame (DemoScenario.initialState ())))

[<Fact>]
let ``SVG render of the demo frame is byte-equal to the committed golden`` () =
    Assert.Equal(golden "demo.svg", DiagnosticRender.Svg(Diagnostics.frame (DemoScenario.initialState ())))

[<Fact>]
let ``HTML render of the demo run is byte-equal to the committed golden`` () =
    Assert.Equal(golden "demo.html", DiagnosticRender.Html(demoFrames ()))

[<Fact>]
let ``ASCII and SVG renders of the shared fixture at tick 0 and the final tick match the goldens`` () =
    let frames =
        DiagnosticRender.runFrames (Fixture.initialState ()) (Fixture.commandLog ()) Fixture.TickCount

    Assert.Equal(golden "fixture-tick-000.ascii.txt", DiagnosticRender.Ascii frames.[0])
    Assert.Equal(golden "fixture-tick-040.ascii.txt", DiagnosticRender.Ascii frames.[40])
    Assert.Equal(golden "fixture-tick-000.svg", DiagnosticRender.Svg frames.[0])
    Assert.Equal(golden "fixture-tick-040.svg", DiagnosticRender.Svg frames.[40])

[<Fact>]
let ``the mid-route fixture frame renders agent 3's followed path (byte-equal to the goldens)`` () =
    // Tick 25: fixture agent 3 is at (20,8), still en route to (20,14).
    // `frameOf` emits a PlannedPath overlay from the stored `AgentState.Route`;
    // the goldens show the L-shaped route between S (0,3) and G (20,14).
    let frames =
        DiagnosticRender.runFrames (Fixture.initialState ()) (Fixture.commandLog ()) Fixture.TickCount

    let mid = frames.[25]

    match mid.Overlays with
    | [| PlannedPath(from, target, cells, cost, reached) |] ->
        Assert.Equal({ X = 0; Y = 3 }, from)
        Assert.Equal({ X = 20; Y = 14 }, target)
        Assert.Equal(31, cost)
        Assert.True(reached)
        Assert.Equal({ X = 0; Y = 3 }, cells.[0])
        Assert.Equal({ X = 20; Y = 14 }, cells.[cells.Length - 1])
    | other -> Assert.Fail($"expected one PlannedPath overlay, got {other}")

    Assert.Equal(golden "fixture-mid-route.ascii.txt", DiagnosticRender.Ascii mid)
    Assert.Equal(golden "fixture-mid-route.svg", DiagnosticRender.Svg mid)

[<Fact>]
let ``frameOf emits no overlay once every agent is at rest`` () =
    let frames =
        DiagnosticRender.runFrames (Fixture.initialState ()) (Fixture.commandLog ()) Fixture.TickCount

    // Agent 3 arrives at tick 31; tick 32 onward carries no route.
    Assert.NotEmpty(frames.[30].Overlays)
    Assert.Empty(frames.[31].Overlays)
    Assert.Empty(frames.[40].Overlays)

// --- renderers: distinct features and determinism ----------------------

[<Fact>]
let ``the impassable cell, the elevated cell, and a one-direction cover edge each appear distinctly`` () =
    let f = Diagnostics.frame (DemoScenario.initialState ())
    let ascii = DiagnosticRender.Ascii f
    let svg = DiagnosticRender.Svg f

    // ASCII: impassable block, elevation-3 cell, and the north cover edge at (1,4).
    Assert.Contains("##", ascii)
    Assert.Contains("  4 ....3", ascii)
    Assert.Contains("  4 .N", ascii)

    // SVG: 4 hatched impassable cells, elevation-3 shading, 3 cover triangles.
    Assert.Equal(4, countOccurrences svg "fill=\"url(#cw-hatch)\"")
    Assert.Equal(3, countOccurrences svg "fill=\"#38a169\"")
    Assert.Contains("fill=\"rgb(166,166,166)\"", svg)

[<Fact>]
let ``a hand-built Overlay renders through the ASCII renderer`` () =
    let f = Diagnostics.frame (DemoScenario.initialState ())

    let withOverlay =
        { f with
            Overlays = [| Cells("probe", [| { X = 1; Y = 1 }; { X = 2; Y = 2 } |]) |] }

    let ascii = DiagnosticRender.Ascii withOverlay
    Assert.Contains("overlays:", ascii)
    Assert.Contains("probe: (1,1) (2,2)", ascii)

[<Fact>]
let ``a hand-built SightRay overlay renders through the ASCII and SVG renderers`` () =
    let f = Diagnostics.frame (DemoScenario.initialState ())

    let withRay =
        { f with
            Overlays =
                [| SightRay(
                       { X = 0; Y = 0 },
                       { X = 4; Y = 0 },
                       [| { X = 0; Y = 0 }; { X = 1; Y = 0 }; { X = 2; Y = 0 }; { X = 3; Y = 0 }; { X = 4; Y = 0 } |],
                       Some { X = 2; Y = 0 }
                   ) |] }

    let ascii = DiagnosticRender.Ascii withRay
    Assert.Contains("overlays:", ascii)
    Assert.Contains("sight (0,0) -> (4,0): blocked at (2,0)", ascii)

    let svg = DiagnosticRender.Svg withRay
    Assert.Contains("stroke-dasharray=\"4,3\"", svg)
    // Byte-identical without the overlay (regression guard for the goldens).
    Assert.Equal(DiagnosticRender.Svg f, DiagnosticRender.Svg(Diagnostics.frame (DemoScenario.initialState ())))

[<Fact>]
let ``a hand-built PlannedPath overlay renders through the ASCII and SVG renderers`` () =
    let f = Diagnostics.frame (DemoScenario.initialState ())

    let withPath =
        { f with
            Overlays =
                [| PlannedPath(
                       { X = 0; Y = 0 },
                       { X = 3; Y = 0 },
                       [| { X = 0; Y = 0 }; { X = 1; Y = 0 }; { X = 2; Y = 0 }; { X = 3; Y = 0 } |],
                       3,
                       true
                   ) |] }

    let ascii = DiagnosticRender.Ascii withPath
    Assert.Contains("overlays:", ascii)
    Assert.Contains("path (0,0) -> (3,0): reached, cost 3", ascii)

    let svg = DiagnosticRender.Svg withPath
    Assert.Contains("<polyline points=", svg)
    // Byte-identical without the overlay (regression guard for the goldens).
    Assert.Equal(DiagnosticRender.Svg f, DiagnosticRender.Svg(Diagnostics.frame (DemoScenario.initialState ())))

[<Fact>]
let ``a hand-built Reserved overlay renders through the ASCII and SVG renderers`` () =
    let f = Diagnostics.frame (DemoScenario.initialState ())

    let withReserved =
        { f with
            Overlays = [| Reserved({ X = 3; Y = 3 }, AgentId.ofInt 0, 3L) |] }

    let ascii = DiagnosticRender.Ascii withReserved
    Assert.Contains("overlays:", ascii)
    Assert.Contains("reserved (3,3): agent 0 (until tick 3)", ascii)

    let svg = DiagnosticRender.Svg withReserved
    Assert.Contains("stroke=\"#d53f8c\"", svg)
    // Byte-identical without the overlay (regression guard for the goldens).
    Assert.Equal(DiagnosticRender.Svg f, DiagnosticRender.Svg(Diagnostics.frame (DemoScenario.initialState ())))

[<Fact>]
let ``a hand-built Obstructed overlay renders through the ASCII and SVG renderers`` () =
    let f = Diagnostics.frame (DemoScenario.initialState ())

    let withObstructed =
        { f with
            Overlays = [| Obstructed({ X = 3; Y = 3 }, AgentId.ofInt 2) |] }

    let ascii = DiagnosticRender.Ascii withObstructed
    Assert.Contains("overlays:", ascii)
    Assert.Contains("obstructed (3,3): held by agent 2", ascii)

    let svg = DiagnosticRender.Svg withObstructed
    Assert.Contains("fill=\"#c53030\">B2</text>", svg)
    // Byte-identical without the overlay (regression guard for the goldens).
    Assert.Equal(DiagnosticRender.Svg f, DiagnosticRender.Svg(Diagnostics.frame (DemoScenario.initialState ())))

// --- reservation: the converging-routes corpus entry (TASK-017) --------

let private corpusDir = Path.Combine(AppContext.BaseDirectory, "replays")

let private convergingRoutesFrames () =
    let entry = Corpus.all |> Array.find (fun e -> e.Name = "converging-routes")

    match Corpus.loadLog corpusDir entry with
    | Error m -> failwith m
    | Ok cmds -> DiagnosticRender.runFrames (entry.InitialState ()) cmds entry.TickCount

[<Fact>]
let ``frameOf derives a Reserved overlay for the converging-routes entry's contested tick (byte-equal to the goldens)`` () =
    // Tick 3: agent 0 and agent 1 both compute (3,3) as their next cell;
    // reservation picks agent 0 (tied remaining route length, lower agent id).
    let frames = convergingRoutesFrames ()
    let tick3 = frames.[3]

    // Both agents are still mid-route at tick 3, so `routeOverlays` also
    // contributes a `PlannedPath` per agent; pick out the `Reserved` one.
    match tick3.Overlays |> Array.tryPick (function
        | Reserved(cell, winner, untilTick) -> Some(cell, winner, untilTick)
        | Cells _
        | SightRay _
        | PlannedPath _
        | Obstructed _ -> None) with
    | Some(cell, winner, untilTick) ->
        Assert.Equal({ X = 3; Y = 3 }, cell)
        Assert.Equal(AgentId.ofInt 0, winner)
        Assert.Equal(3L, untilTick)
    | None -> Assert.Fail($"expected one Reserved overlay, got {tick3.Overlays}")

    Assert.Contains(tick3.Events, fun (e: EventMarker) -> e.Kind = "movement-yielded")

    Assert.Equal(golden "converging-routes-tick-003.ascii.txt", DiagnosticRender.Ascii tick3)
    Assert.Equal(golden "converging-routes-tick-003.svg", DiagnosticRender.Svg tick3)

// --- sub-cell movement progress: the slow-terrain corpus entry (TASK-018) --

let private slowTerrainFrames () =
    let entry = Corpus.all |> Array.find (fun e -> e.Name = "slow-terrain")

    match Corpus.loadLog corpusDir entry with
    | Error m -> failwith m
    | Ok cmds -> DiagnosticRender.runFrames (entry.InitialState ()) cmds entry.TickCount

[<Fact>]
let ``the frame carries AgentMarker.Progress for the slow-terrain entry's accumulating tick (byte-equal to the goldens)`` () =
    // Tick 2: agent 0 has accumulated 2 of the 3 progress needed to enter
    // (1,0) and has not moved from (0,0) yet.
    let frames = slowTerrainFrames ()
    let tick2 = frames.[2]

    Assert.Equal(1, tick2.Agents.Length)
    Assert.Equal({ X = 0; Y = 0 }, tick2.Agents.[0].Cell)
    Assert.Equal(2, tick2.Agents.[0].Progress)

    Assert.Equal(golden "slow-terrain-tick-002.ascii.txt", DiagnosticRender.Ascii tick2)
    Assert.Equal(golden "slow-terrain-tick-002.svg", DiagnosticRender.Svg tick2)

// --- cell occupancy: the swap-standoff corpus entry (TASK-022) ---------

let private swapStandoffFrames () =
    let entry = Corpus.all |> Array.find (fun e -> e.Name = "swap-standoff")

    match Corpus.loadLog corpusDir entry with
    | Error m -> failwith m
    | Ok cmds -> DiagnosticRender.runFrames (entry.InitialState ()) cmds entry.TickCount

[<Fact>]
let ``frameOf derives an Obstructed overlay for the swap-standoff entry's blocked tick (byte-equal to the goldens)`` () =
    // Tick 1: agent 0 (3,3) and agent 1 (4,3) are each ordered onto the
    // other's cell; the two-agent swap is blocked, so both freeze and emit
    // MovementObstructed. `frameOf` derives one Obstructed overlay per blocked
    // cell alongside each agent's PlannedPath.
    let frames = swapStandoffFrames ()
    let tick1 = frames.[1]

    let obstructed =
        tick1.Overlays
        |> Array.choose (function
            | Obstructed(cell, occupant) -> Some(cell, AgentId.value occupant)
            | Cells _
            | SightRay _
            | PlannedPath _
            | Reserved _ -> None)
        |> Array.sortBy (fun (c, _) -> c.X, c.Y)

    Assert.Equal<(Cell * int)[]>([| ({ X = 3; Y = 3 }, 0); ({ X = 4; Y = 3 }, 1) |], obstructed)
    Assert.Contains(tick1.Events, fun (e: EventMarker) -> e.Kind = "movement-obstructed")

    Assert.Equal(golden "swap-standoff-tick-001.ascii.txt", DiagnosticRender.Ascii tick1)
    Assert.Equal(golden "swap-standoff-tick-001.svg", DiagnosticRender.Svg tick1)

[<Fact>]
let ``rendering is deterministic: two renders of the same frame are byte-equal`` () =
    let frames = demoFrames ()
    let mid = frames.[8]
    Assert.Equal(DiagnosticRender.Ascii mid, DiagnosticRender.Ascii mid)
    Assert.Equal(DiagnosticRender.Svg mid, DiagnosticRender.Svg mid)
    Assert.Equal(DiagnosticRender.Html frames, DiagnosticRender.Html frames)

[<Fact>]
let ``the HTML output parses as XML and has one frame element per tick`` () =
    let frames = demoFrames ()
    let html = DiagnosticRender.Html frames

    let settings = XmlReaderSettings(DtdProcessing = DtdProcessing.Ignore)
    use reader = XmlReader.Create(new StringReader(html), settings)
    let doc = XDocument.Load(reader)

    let frameEls =
        doc.Descendants()
        |> Seq.filter (fun e ->
            e.Name.LocalName = "div"
            && (match e.Attribute(XName.Get "class") with
                | null -> false
                | a -> a.Value.Contains "cw-frame"))
        |> Seq.length

    Assert.Equal(frames.Length, frameEls)
    Assert.Equal(int DemoScenario.TickCount + 1, frameEls)

// --- the pin: diagnostics do not perturb the shared fixture -----------

[<Fact>]
let ``producing diagnostics for the shared fixture leaves its hashes and event count unchanged`` () =
    let frames =
        DiagnosticRender.runFrames (Fixture.initialState ()) (Fixture.commandLog ()) Fixture.TickCount

    Assert.Equal(0xE13D7540912C7E25UL, frames.[0].Hash.Value)
    Assert.Equal(0xAFA35198CC6BD8D4UL, frames.[40].Hash.Value)
    Assert.Equal(33, frames |> Array.sumBy (fun f -> f.Events.Length))

    match Fixture.run () with
    | Error e -> Assert.Fail($"fixture replay failed: {e}")
    | Ok outcome ->
        Assert.Equal(0xAFA35198CC6BD8D4UL, (Hashing.hash outcome.FinalState).Value)
        Assert.Equal(33, outcome.Events.Length)
