module CommandoWar.Sim.Tests.CanonicalHashTests

open Xunit
open CommandoWar.Sim

let private bounds: GridBounds = { Width = 8; Height = 8 }
let private world (seed: uint64) = Setup.sixAgentWorld bounds seed
let private step cmds state = Simulation.step SimConfig.standard cmds state
let private move id agent dest = Command.moveTo (CommandId.ofInt id) 0L (AgentId.ofInt agent) dest

// --- Canonical encoding --------------------------------------------------

[<Fact>]
let ``the canonical format version is the first field and travels on every hash`` () =
    let w = world 1UL
    let bytes = Canonical.encode w
    // First four bytes: big-endian uint32 format version.
    Assert.Equal<byte[]>([| 0uy; 0uy; 0uy; byte Canonical.FormatVersion |], bytes.[0..3])
    Assert.Equal(Canonical.FormatVersion, (Hashing.hash w).Format)

[<Fact>]
let ``equal authoritative states encode and hash identically`` () =
    let a = world 4UL
    let b = world 4UL
    Assert.Equal<byte[]>(Canonical.encode a, Canonical.encode b)
    Assert.Equal(Hashing.hash a, Hashing.hash b)

[<Fact>]
let ``canonical encoding is independent of the order agents are supplied in`` () =
    let ascending =
        [ for i in 0..3 -> Agent.create (AgentId.ofInt i) Friendly { X = i; Y = 0 } ]

    let descending = List.rev ascending

    let build agents =
        match World.create bounds 5UL agents with
        | Ok w -> w
        | Error e -> failwith $"{e}"

    Assert.Equal<byte[]>(Canonical.encode (build ascending), Canonical.encode (build descending))

[<Fact>]
let ``the hash changes when authoritative agent state changes`` () =
    let before = world 1UL
    let after = (step [| move 1 0 { X = 3; Y = 0 } |] before).State
    Assert.NotEqual(Hashing.hash before, Hashing.hash after)

[<Fact>]
let ``the hash changes when the random stream advances`` () =
    let baseWorld = world 1UL
    let struct (_, advanced) = SplitMix64.next baseWorld.Random
    let perturbed = { baseWorld with Random = advanced }
    Assert.NotEqual(Hashing.hash baseWorld, Hashing.hash perturbed)

[<Fact>]
let ``the step hash is exactly the canonical hash of the returned state`` () =
    // Nothing presentation-side (events, snapshot, phase trace) feeds the
    // hash: it is a pure function of the authoritative WorldState.
    let r = step [| move 1 2 { X = 5; Y = 5 } |] (world 1UL)
    Assert.Equal(Hashing.hash r.State, r.StateHash)

[<Fact>]
let ``the state hash is FNV-1a-64 over the canonical encoding`` () =
    Assert.Equal("FNV-1a-64", Hashing.Algorithm)
    let w = (step [| move 1 0 { X = 2; Y = 2 } |] (world 3UL)).State

    // Recompute FNV-1a-64 independently of Hashing.hash.
    let offset = 0xCBF29CE484222325UL
    let prime = 0x00000100000001B3UL

    let expected =
        Canonical.encode w
        |> Array.fold (fun h b -> (h ^^^ uint64 b) * prime) offset

    Assert.Equal(expected, (Hashing.hash w).Value)
    Assert.Equal(expected, Hashing.digest (Canonical.encode w))

[<Fact>]
let ``the followed-path cache is outside the canonical image and the format version stays 1`` () =
    // TASK-015: AgentState.Route is a derived cache, excluded from the hash.
    let moved = (step [| move 1 0 { X = 5; Y = 0 } |] (world 1UL)).State
    let a0 = moved.Agents |> Array.find (fun a -> AgentId.value a.Id = 0)
    Assert.True(a0.Route.IsSome, "expected agent 0 to be following a route")

    let stripped =
        { moved with
            Agents = moved.Agents |> Array.map (fun a -> { a with Route = None }) }

    Assert.Equal<byte[]>(Canonical.encode stripped, Canonical.encode moved)
    Assert.Equal(Hashing.hash stripped, Hashing.hash moved)
    Assert.Equal(1, Canonical.FormatVersion)

// --- First-differing section -------------------------------------------

[<Fact>]
let ``firstDifferingSection returns None for identical states`` () =
    Assert.Equal(None, Canonical.firstDifferingSection (world 1UL) (world 1UL))

[<Fact>]
let ``firstDifferingSection points at the tick when only the tick differs`` () =
    let a = world 1UL
    let b = { a with Tick = 9L }
    Assert.Equal(Some "Tick", Canonical.firstDifferingSection a b)

[<Fact>]
let ``firstDifferingSection names the agent whose position changed`` () =
    // Both states are at the same tick; only agent 3 differs.
    let idle = (step [||] (world 1UL)).State
    let moved = (step [| move 1 3 { X = 4; Y = 3 } |] (world 1UL)).State
    Assert.Equal(Some "Agent[3]", Canonical.firstDifferingSection idle moved)

[<Fact>]
let ``firstDifferingSection points at Random when only the stream differs`` () =
    let a = world 1UL
    let struct (_, advanced) = SplitMix64.next a.Random
    Assert.Equal(Some "Random", Canonical.firstDifferingSection a { a with Random = advanced })
