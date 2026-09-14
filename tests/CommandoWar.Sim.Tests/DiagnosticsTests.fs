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

    // No order has been appraised yet, so the only overlay is one Holding
    // AgentCommitment per agent (TASK-030 — derived, not stored, so it is
    // never actually empty).
    Assert.Equal(w.Agents.Length, f.Overlays.Length)
    Assert.All(
        f.Overlays,
        (function
        | AgentCommitment(_, _, Holding) -> ()
        | other -> Assert.Fail($"expected only Holding AgentCommitment overlays, got {other}"))
    )

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
    Assert.Equal(0xF1A703A752C0F6B9UL, f.Hash.Value)
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

    // Two overlays now: the PlannedPath from the stored Route, and the
    // OrderAppraisal for agent 3's still-Accepted order (TASK-028).
    match mid.Overlays |> Array.tryPick (function
        | PlannedPath(from, target, cells, cost, reached) -> Some(from, target, cells, cost, reached)
        | _ -> None) with
    | Some(from, target, cells, cost, reached) ->
        Assert.Equal({ X = 0; Y = 3 }, from)
        Assert.Equal({ X = 20; Y = 14 }, target)
        Assert.Equal(31, cost)
        Assert.True(reached)
        Assert.Equal({ X = 0; Y = 3 }, cells.[0])
        Assert.Equal({ X = 20; Y = 14 }, cells.[cells.Length - 1])
    | None -> Assert.Fail($"expected a PlannedPath overlay, got {mid.Overlays}")

    Assert.Contains(mid.Overlays, (function
        | OrderAppraisal(a, _, Accepted, _) -> AgentId.value a = 3
        | _ -> false))

    Assert.Equal(golden "fixture-mid-route.ascii.txt", DiagnosticRender.Ascii mid)
    Assert.Equal(golden "fixture-mid-route.svg", DiagnosticRender.Svg mid)

[<Fact>]
let ``frameOf emits no OrderAppraisal overlay once every agent is at rest and its order is cleared`` () =
    let frames =
        DiagnosticRender.runFrames (Fixture.initialState ()) (Fixture.commandLog ()) Fixture.TickCount

    // Agent 3 arrives at tick 31 (route cleared); its Accepted order lingers
    // one more tick as an OrderAppraisal overlay until commitmentAndLocalAction's
    // fulfilment housekeeping clears Order / Disposition at tick 32 (TASK-030 —
    // relocated from the Appraisal phase, which runs before Navigation, so
    // fulfilment is only noticed next tick). From tick 32 on, no OrderAppraisal
    // overlay remains — but every agent still carries an AgentCommitment
    // overlay every tick (TASK-030), Holding once at rest, so Overlays itself
    // is never empty.
    let isOrderAppraisal =
        function
        | OrderAppraisal _ -> true
        | _ -> false

    Assert.Contains(frames.[30].Overlays, isOrderAppraisal)
    Assert.Contains(frames.[31].Overlays, isOrderAppraisal)
    Assert.DoesNotContain(frames.[32].Overlays, isOrderAppraisal)
    Assert.DoesNotContain(frames.[40].Overlays, isOrderAppraisal)

    Assert.All(
        frames.[40].Overlays,
        (function
        | AgentCommitment(_, _, Holding) -> ()
        | other -> Assert.Fail($"expected only Holding AgentCommitment overlays at rest, got {other}"))
    )

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

[<Fact>]
let ``a hand-built KnownContact overlay renders through the ASCII and SVG renderers`` () =
    let f = Diagnostics.frame (DemoScenario.initialState ())

    let withContact =
        { f with
            Overlays = [| KnownContact({ X = 9; Y = 1 }, AgentId.ofInt 1, 1000, 5L) |] }

    let ascii = DiagnosticRender.Ascii withContact
    Assert.Contains("overlays:", ascii)
    Assert.Contains("known contact (9,1): agent 1  confidence 1000  seen tick 5", ascii)

    let svg = DiagnosticRender.Svg withContact
    Assert.Contains("stroke=\"#805ad5\"", svg)
    Assert.Contains("fill=\"#805ad5\">?1</text>", svg)
    // Byte-identical without the overlay (regression guard for the goldens).
    Assert.Equal(DiagnosticRender.Svg f, DiagnosticRender.Svg(Diagnostics.frame (DemoScenario.initialState ())))

[<Fact>]
let ``a hand-built UndeliveredOrder overlay renders through the ASCII and SVG renderers`` () =
    let f = Diagnostics.frame (DemoScenario.initialState ())

    let withUndelivered =
        { f with
            Overlays = [| UndeliveredOrder(AgentId.ofInt 1, { X = 4; Y = 2 }, CommandId.ofInt 7) |] }

    let ascii = DiagnosticRender.Ascii withUndelivered
    Assert.Contains("overlays:", ascii)
    Assert.Contains("undelivered order (4,2): agent 1  command 7  (communication unavailable)", ascii)

    let svg = DiagnosticRender.Svg withUndelivered
    Assert.Contains("fill=\"#e53e3e\">!1</text>", svg)
    // Byte-identical without the overlay (regression guard for the goldens).
    Assert.Equal(DiagnosticRender.Svg f, DiagnosticRender.Svg(Diagnostics.frame (DemoScenario.initialState ())))

[<Fact>]
let ``a hand-built OrderAppraisal overlay renders through the ASCII and SVG renderers`` () =
    // TASK-028: a Refused disposition with a threat, and its exposed route
    // cells.
    let f = Diagnostics.frame (DemoScenario.initialState ())

    let withAppraisal =
        { f with
            Overlays =
                [| OrderAppraisal(
                       AgentId.ofInt 0,
                       { X = 2; Y = 3 },
                       Refused(RouteTooExposed(Some(AgentId.ofInt 5)), [||]),
                       [| { X = 3; Y = 3 }; { X = 4; Y = 3 } |]
                   ) |] }

    let ascii = DiagnosticRender.Ascii withAppraisal
    Assert.Contains("overlays:", ascii)
    Assert.Contains("order appraisal (2,3): agent 0  refused route-too-exposed threat-agent-5  exposed (3,3) (4,3)", ascii)

    let svg = DiagnosticRender.Svg withAppraisal
    Assert.Contains("fill=\"#c53030\">R</text>", svg)
    Assert.Contains("fill=\"#c53030\" fill-opacity=\"0.25\"", svg)
    // Byte-identical without the overlay (regression guard for the goldens).
    Assert.Equal(DiagnosticRender.Svg f, DiagnosticRender.Svg(Diagnostics.frame (DemoScenario.initialState ())))

[<Fact>]
let ``AgentMarker.CommunicationAvailable is carried and rendered only when an agent cannot receive orders`` () =
    // TASK-027: the marker is on every agent, but the renderers surface it
    // only for a blacked-out agent (the Progress-omitted-at-0 precedent).
    let w = DemoScenario.initialState ()
    let plainAscii = DiagnosticRender.Ascii(Diagnostics.frame w)
    Assert.DoesNotContain("no-comms", plainAscii)

    let blackedOut =
        { w with
            Agents = w.Agents |> Array.map (fun a -> { a with CommunicationAvailable = false }) }

    let frame = Diagnostics.frame blackedOut
    Assert.All(frame.Agents, fun m -> Assert.False(m.CommunicationAvailable))

    let ascii = DiagnosticRender.Ascii frame
    Assert.Contains("no-comms", ascii)
    Assert.Contains("stroke-dasharray=\"2,1\"", DiagnosticRender.Svg frame)

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
        | Obstructed _
        | UndeliveredOrder _
        | KnownContact _
        | OrderAppraisal _
        | AgentCommitment _
        | FireLine _
        | AgentSuppression _
        | AgentStress _ -> None) with
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
            | Reserved _
            | UndeliveredOrder _
            | KnownContact _
            | OrderAppraisal _
            | AgentCommitment _
            | FireLine _
            | AgentSuppression _
            | AgentStress _ -> None)
        |> Array.sortBy (fun (c, _) -> c.X, c.Y)

    Assert.Equal<(Cell * int)[]>([| ({ X = 3; Y = 3 }, 0); ({ X = 4; Y = 3 }, 1) |], obstructed)
    Assert.Contains(tick1.Events, fun (e: EventMarker) -> e.Kind = "movement-obstructed")

    Assert.Equal(golden "swap-standoff-tick-001.ascii.txt", DiagnosticRender.Ascii tick1)
    Assert.Equal(golden "swap-standoff-tick-001.svg", DiagnosticRender.Svg tick1)

// --- perception: the perception-contact corpus entry (TASK-026) --------

let private perceptionContactFrames () =
    let entry = Corpus.all |> Array.find (fun e -> e.Name = "perception-contact")

    match Corpus.loadLog corpusDir entry with
    | Error m -> failwith m
    | Ok cmds -> DiagnosticRender.runFrames (entry.InitialState ()) cmds entry.TickCount

[<Fact>]
let ``frameOf derives a KnownContact overlay for the perception-contact entry's first sighting (byte-equal to the goldens)`` () =
    // Tick 5: friendly agent 0 clears the opaque wall at x=6 and its line of
    // sight to the stationary hostile agent 1 at (9,1) opens. The Perception
    // phase emits ContactObserved (both ways — perception is symmetric) and
    // the Tactical-knowledge phase adds the contact to the shared squad
    // picture, so `frameOf` derives one KnownContact overlay alongside the
    // friendly's PlannedPath.
    let frames = perceptionContactFrames ()
    let tick5 = frames.[5]

    match
        tick5.Overlays
        |> Array.tryPick (function
            | KnownContact(cell, contact, confidence, lastSeenTick) -> Some(cell, contact, confidence, lastSeenTick)
            | Cells _
            | SightRay _
            | PlannedPath _
            | Reserved _
            | Obstructed _
            | UndeliveredOrder _
            | OrderAppraisal _
            | AgentCommitment _
            | FireLine _
            | AgentSuppression _
            | AgentStress _ -> None)
    with
    | Some(cell, contact, confidence, lastSeenTick) ->
        Assert.Equal({ X = 9; Y = 1 }, cell)
        Assert.Equal(AgentId.ofInt 1, contact)
        Assert.Equal(1000, confidence)
        Assert.Equal(5L, lastSeenTick)
    | None -> Assert.Fail($"expected one KnownContact overlay, got {tick5.Overlays}")

    Assert.Contains(tick5.Events, fun (e: EventMarker) -> e.Kind = "contact-observed")

    // The hostile stays out of the shared picture until the wall is cleared:
    // tick 4's frame carries no KnownContact overlay.
    Assert.DoesNotContain(
        frames.[4].Overlays,
        (function
        | KnownContact _ -> true
        | _ -> false)
    )

    Assert.Equal(golden "perception-contact-tick-005.ascii.txt", DiagnosticRender.Ascii tick5)
    Assert.Equal(golden "perception-contact-tick-005.svg", DiagnosticRender.Svg tick5)

// --- communication: the lost-comms corpus entry (TASK-027) -------------

let private lostCommsFrames () =
    let entry = Corpus.all |> Array.find (fun e -> e.Name = "lost-comms")

    match Corpus.loadLog corpusDir entry with
    | Error m -> failwith m
    | Ok cmds -> DiagnosticRender.runFrames (entry.InitialState ()) cmds entry.TickCount

[<Fact>]
let ``frameOf derives an UndeliveredOrder overlay for the lost-comms entry's dropped order (byte-equal to the goldens)`` () =
    // Tick 1: command intake accepts the order to comms-blacked-out agent 0,
    // the Communication phase emits OrderUndelivered and drops it, so `frameOf`
    // derives one UndeliveredOrder overlay at the agent's (unchanged) cell and
    // the events line carries an order-undelivered marker.
    let frames = lostCommsFrames ()
    let tick1 = frames.[1]

    match
        tick1.Overlays
        |> Array.tryPick (function
            | UndeliveredOrder(recipient, at, command) -> Some(recipient, at, command)
            | Cells _
            | SightRay _
            | PlannedPath _
            | Reserved _
            | Obstructed _
            | KnownContact _
            | OrderAppraisal _
            | AgentCommitment _
            | FireLine _
            | AgentSuppression _
            | AgentStress _ -> None)
    with
    | Some(recipient, at, command) ->
        Assert.Equal(AgentId.ofInt 0, recipient)
        Assert.Equal({ X = 1; Y = 4 }, at)
        Assert.Equal(CommandId.ofInt 1, command)
    | None -> Assert.Fail($"expected one UndeliveredOrder overlay, got {tick1.Overlays}")

    Assert.Contains(tick1.Events, fun (e: EventMarker) -> e.Kind = "order-undelivered")
    Assert.All(tick1.Agents, fun (m: AgentMarker) -> Assert.False(m.CommunicationAvailable))

    // The agent never gets a destination and never moves.
    Assert.All(frames, fun f -> Assert.All(f.Agents, fun m -> Assert.Equal<Cell option>(None, m.Destination)))

    Assert.Equal(golden "lost-comms-tick-001.ascii.txt", DiagnosticRender.Ascii tick1)
    Assert.Equal(golden "lost-comms-tick-001.svg", DiagnosticRender.Svg tick1)

// --- order appraisal: the exposed-approach corpus entry (TASK-028) -----

let private exposedApproachFrames () =
    let entry = Corpus.all |> Array.find (fun e -> e.Name = "exposed-approach")

    match Corpus.loadLog corpusDir entry with
    | Error m -> failwith m
    | Ok cmds -> DiagnosticRender.runFrames (entry.InitialState ()) cmds entry.TickCount

[<Fact>]
let ``frameOf derives divergent OrderAppraisal overlays for the exposed-approach entry (byte-equal to the goldens)`` () =
    // Tick 1: agent 0 (Discipline 1) Refuses the exposed order, agent 1
    // (Discipline 6) Accepts it — the G3 divergence.
    let tick1 = (exposedApproachFrames ()).[1]

    let appraisals =
        tick1.Overlays
        |> Array.choose (function
            | OrderAppraisal(a, _, d, _) -> Some(AgentId.value a, d)
            | _ -> None)
        |> Array.sortBy fst

    Assert.Equal(2, appraisals.Length)

    match appraisals.[0], appraisals.[1] with
    | (0, Refused(RouteTooExposed(Some t), _)), (1, Accepted) -> Assert.Equal(AgentId.ofInt 2, t)
    | other -> Assert.Fail($"expected agent 0 Refused / agent 1 Accepted, got {other}")

    Assert.Equal(2, tick1.Events |> Array.filter (fun e -> e.Kind = "order-appraised") |> Array.length)
    Assert.Equal(golden "exposed-approach-tick-001.ascii.txt", DiagnosticRender.Ascii tick1)
    Assert.Equal(golden "exposed-approach-tick-001.svg", DiagnosticRender.Svg tick1)

[<Fact>]
let ``frameOf shows blocked-goal's order as Unable at appraisal (byte-equal to the goldens)`` () =
    let entry = Corpus.all |> Array.find (fun e -> e.Name = "blocked-goal")

    let frames =
        match Corpus.loadLog corpusDir entry with
        | Error m -> failwith m
        | Ok cmds -> DiagnosticRender.runFrames (entry.InitialState ()) cmds entry.TickCount

    let tick1 = frames.[1]

    Assert.Contains(tick1.Overlays, (function
        | OrderAppraisal(a, _, Unable(NoKnownRoute, _), _) -> AgentId.value a = 0
        | _ -> false))

    Assert.DoesNotContain(tick1.Events, (fun (e: EventMarker) -> e.Kind = "movement-blocked"))
    Assert.Equal(golden "blocked-goal-tick-001.ascii.txt", DiagnosticRender.Ascii tick1)
    Assert.Equal(golden "blocked-goal-tick-001.svg", DiagnosticRender.Svg tick1)

// --- commitment and finite move/hold executor: the reissued-order entry (TASK-030) --

let private reissuedOrderFrames () =
    let entry = Corpus.all |> Array.find (fun e -> e.Name = "reissued-order")

    match Corpus.loadLog corpusDir entry with
    | Error m -> failwith m
    | Ok cmds -> DiagnosticRender.runFrames (entry.InitialState ()) cmds entry.TickCount

[<Fact>]
let ``frameOf derives an AgentCommitment overlay for the reissued-order entry's supersession tick (byte-equal to the goldens)`` () =
    let frames = reissuedOrderFrames ()

    // Tick 1: the first order is Accepted -> CommitmentEstablished, Moving
    // toward (14,4).
    let tick1 = frames.[1]
    Assert.Contains(tick1.Events, (fun (e: EventMarker) -> e.Kind = "commitment-established"))

    Assert.Contains(tick1.Overlays, (function
        | AgentCommitment(_, _, Moving mc) -> mc.Target = { X = 14; Y = 4 }
        | _ -> false))

    // Tick 3: the second order supersedes the first. A fresh
    // CommitmentEstablished for the new target, no CommitmentCompleted at all.
    let tick3 = frames.[3]
    Assert.Contains(tick3.Events, (fun (e: EventMarker) -> e.Kind = "commitment-established"))
    Assert.DoesNotContain(tick3.Events, (fun (e: EventMarker) -> e.Kind = "commitment-completed"))

    Assert.Contains(tick3.Overlays, (function
        | AgentCommitment(_, _, Moving mc) -> mc.Target = { X = 14; Y = 8 }
        | _ -> false))

    Assert.Equal(golden "reissued-order-tick-003.ascii.txt", DiagnosticRender.Ascii tick3)
    Assert.Equal(golden "reissued-order-tick-003.svg", DiagnosticRender.Svg tick3)

// --- hitscan combat and directional cover effects: the open-engagement entry (TASK-031) --

let private openEngagementFrames () =
    let entry = Corpus.all |> Array.find (fun e -> e.Name = "open-engagement")

    match Corpus.loadLog corpusDir entry with
    | Error m -> failwith m
    | Ok cmds -> DiagnosticRender.runFrames (entry.InitialState ()) cmds entry.TickCount

[<Fact>]
let ``frameOf derives FireLine overlays for the open-engagement entry's first tick (byte-equal to the goldens)`` () =
    // Tick 1: a friendly at (2,2) and a hostile at (7,2), both within
    // CombatConfig.WeaponRange and clear line of sight, so the Combat phase
    // fires symmetrically both ways with no order on either side.
    let tick1 = (openEngagementFrames ()).[1]

    let fireLines =
        tick1.Overlays
        |> Array.choose (function
            | FireLine(shooter, from, target, at, hit) -> Some(AgentId.value shooter, from, AgentId.value target, at, hit)
            | _ -> None)
        |> Array.sortBy (fun (s, _, _, _, _) -> s)

    Assert.Equal(2, fireLines.Length)

    match fireLines.[0], fireLines.[1] with
    | (0, from0, 1, at0, _), (1, from1, 0, at1, _) ->
        Assert.Equal({ X = 2; Y = 2 }, from0)
        Assert.Equal({ X = 7; Y = 2 }, at0)
        Assert.Equal({ X = 7; Y = 2 }, from1)
        Assert.Equal({ X = 2; Y = 2 }, at1)
    | other -> Assert.Fail($"expected agent 0 -> agent 1 and agent 1 -> agent 0, got {other}")

    Assert.Equal(2, tick1.Events |> Array.filter (fun e -> e.Kind.StartsWith "shot-fired-") |> Array.length)

    // Suppression (TASK-032): both agents were fired on this tick, so both
    // carry a non-zero AgentSuppression overlay, net of the same-tick decay.
    let suppressions =
        tick1.Overlays
        |> Array.choose (function
            | AgentSuppression(agent, _, suppression) -> Some(AgentId.value agent, suppression)
            | _ -> None)
        |> Array.sortBy fst

    Assert.Equal(2, suppressions.Length)
    Assert.All(suppressions, fun (_, s) -> Assert.True(s > 0 && s <= SuppressionConfig.MaxSuppression))

    Assert.Equal(golden "open-engagement-tick-001.ascii.txt", DiagnosticRender.Ascii tick1)
    Assert.Equal(golden "open-engagement-tick-001.svg", DiagnosticRender.Svg tick1)

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

// --- TASK-029 appraisal demo helper (a read-only, corpus-scoped B-029 slice) --

[<Fact>]
let ``AppraisalDemo.dispositionText matches the committed golden vocabulary`` () =
    // The readable text the Godot appraisal demo's panel shows, asserted
    // against the committed golden renders — not against DiagnosticRender's
    // `let private` formatters.
    let exposed = golden "exposed-approach-tick-001.ascii.txt"
    let blocked = golden "blocked-goal-tick-001.ascii.txt"

    Assert.Equal("accepted", AppraisalDemo.dispositionText Accepted)
    Assert.Contains(AppraisalDemo.dispositionText Accepted, exposed)

    let refused =
        AppraisalDemo.dispositionText (Refused(RouteTooExposed(Some(AgentId.ofInt 2)), [||]))

    Assert.Equal("refused route-too-exposed threat-agent-2", refused)
    Assert.Contains(refused, exposed)

    let unable = AppraisalDemo.dispositionText (Unable(NoKnownRoute, [||]))
    Assert.Equal("unable no-known-route", unable)
    Assert.Contains(unable, blocked)

[<Fact>]
let ``AppraisalDemo.loadExposedApproachFrames reproduces the tick-1 hash and the divergent dispositions`` () =
    let frames = AppraisalDemo.loadExposedApproachFrames corpusDir
    Assert.Equal(13, frames.Length)
    Assert.Equal(0x2066BC1FAF990E4AUL, frames.[1].Hash.Value)

    let appraisals =
        frames.[1].Overlays
        |> Array.choose (function
            | OrderAppraisal(a, _, d, _) -> Some(AgentId.value a, d)
            | _ -> None)
        |> Array.sortBy fst

    match appraisals with
    | [| (0, Refused(RouteTooExposed(Some t), _)); (1, Accepted) |] -> Assert.Equal(AgentId.ofInt 2, t)
    | other -> Assert.Fail($"expected agent 0 Refused / agent 1 Accepted, got {other}")

    // The flat C#-facing view model carries the same divergence.
    let view = (AppraisalDemo.loadFrameViews corpusDir).[1]
    let rows = view.Appraisals |> Array.sortBy (fun r -> r.AgentId)
    Assert.Equal(2, rows.Length)
    Assert.Equal("refused", rows.[0].Tone)
    Assert.Equal("refused route-too-exposed threat-agent-2", rows.[0].Text)
    Assert.Equal(10, rows.[0].ExposedCells.Length)
    Assert.Equal("accepted", rows.[1].Tone)
    Assert.Equal(1, view.Contacts.Length)
    Assert.Equal(2, view.Contacts.[0].Contact)
    // TASK-030: every agent (both friendlies and the hostile) now carries an
    // AgentCommitment overlay, which this disposable P3 demo (predating
    // TASK-030) does not render — the OrderAppraisal panel already shows each
    // friendly's decision. Three agents, three unhandled entries.
    //
    // TASK-033: all three agents see each other from tick 1 (the scenario's
    // own premise — "past a stationary hostile the squad sees from the
    // start"), so State consequences raises every agent's Stress to 50 by the
    // end of tick 1 (StressConfig.GainPerTick 80, minus DecayPerTick 30 the
    // same tick), and the sparse AgentStress overlay now fires for all three
    // — three more unhandled entries, six total. Genuine new behaviour, not a
    // bug: this disposable demo does not render AgentStress either.
    Assert.Equal(6, view.UnhandledOverlays.Length)

    Assert.All(
        view.UnhandledOverlays,
        (fun (o: string) -> Assert.True(o.StartsWith "commitment agent " || o.StartsWith "stress agent "))
    )

// --- the pin: diagnostics do not perturb the shared fixture -----------

[<Fact>]
let ``producing diagnostics for the shared fixture leaves its hashes and event count unchanged`` () =
    let frames =
        DiagnosticRender.runFrames (Fixture.initialState ()) (Fixture.commandLog ()) Fixture.TickCount

    Assert.Equal(0xF1A703A752C0F6B9UL, frames.[0].Hash.Value)
    Assert.Equal(0x507D041E109404B6UL, frames.[40].Hash.Value)
    // TASK-030: 34 -> 36 (+1 CommitmentEstablished when agent 3's order is
    // accepted, +1 CommitmentCompleted when it arrives) — hashes unchanged,
    // since Commitment is derived, not canonical (Decision B).
    Assert.Equal(36, frames |> Array.sumBy (fun f -> f.Events.Length))

    match Fixture.run () with
    | Error e -> Assert.Fail($"fixture replay failed: {e}")
    | Ok outcome ->
        Assert.Equal(0x507D041E109404B6UL, (Hashing.hash outcome.FinalState).Value)
        Assert.Equal(36, outcome.Events.Length)
