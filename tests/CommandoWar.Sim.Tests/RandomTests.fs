module CommandoWar.Sim.Tests.RandomTests

open Xunit
open CommandoWar.Sim

// Golden vectors for the project-owned PRNG. These tests are independent of
// any game behaviour: they exercise only the generator.
//
// Algorithm: SplitMix64, version 1 (see src/CommandoWar.Sim/Random.fs).
// Reference sequence for seed 0 is the published SplitMix64 output
// (Steele/Lea/Flood, OOPSLA 2014; https://prng.di.unimi.it/splitmix64.c) and
// can be reproduced with any conformant SplitMix64 implementation.

let private draw (seed: uint64) (n: int) : uint64[] =
    let mutable state = SplitMix64.create seed

    [| for _ in 1..n ->
           let struct (value, next) = SplitMix64.next state
           state <- next
           value |]

[<Fact>]
let ``SplitMix64 identifies its algorithm and version`` () =
    Assert.Equal("SplitMix64", SplitMix64.Name)
    Assert.Equal(1, SplitMix64.Version)
    Assert.Equal("SplitMix64", SplitMix64.generator.Name)
    Assert.Equal(1, SplitMix64.generator.Version)

    let state = SplitMix64.create 0UL
    Assert.Equal(RandomAlgorithm.SplitMix64, state.Algorithm)
    Assert.Equal(1, state.AlgorithmVersion)

[<Fact>]
let ``SplitMix64 reproduces the published seed-0 golden vector`` () =
    let expected =
        [| 0xE220A8397B1DCDAFUL
           0x6E789E6AA1B965F4UL
           0x06C45D188009454FUL
           0xF88BB8A8724C81ECUL
           0x1B39896A51A8749BUL
           0x53CB9F0C747EA2EAUL
           0x2C829ABE1F4532E1UL
           0xC584133AC916AB3CUL
           0x3EE5789041C98AC3UL
           0xF3B8488C368CB0A6UL |]

    Assert.Equal<uint64[]>(expected, draw 0UL 10)

[<Fact>]
let ``SplitMix64 golden vector for a non-zero seed`` () =
    // Captured from SplitMix64 v1. Regenerate only when SplitMix64.Version
    // changes (the generator's output sequence is the contract).
    let expected =
        [| 0x161922C645CE50E8UL
           0xAD760CAFA1697B60UL
           0x3501FF44902CA50DUL
           0x417CB9A826D831DFUL
           0x99AF6F9B0C4476B6UL
           0x5D51F5F75B762C59UL
           0x66239E8C309A282BUL
           0x53E01F580916C5CBUL |]

    Assert.Equal<uint64[]>(expected, draw 0x123456789ABCDEF0UL 8)

[<Fact>]
let ``the deterministic random interface and the module produce the same sequence`` () =
    let mutable moduleState = SplitMix64.create 99UL
    let mutable ifaceState = SplitMix64.generator.Create 99UL

    for _ in 1..20 do
        let struct (m, mNext) = SplitMix64.next moduleState
        let struct (i, iNext) = SplitMix64.generator.Next ifaceState
        Assert.Equal(m, i)
        moduleState <- mNext
        ifaceState <- iNext

[<Fact>]
let ``the draw counter increments once per draw and the rest of the state is serialisable`` () =
    let mutable state = SplitMix64.create 7UL
    Assert.Equal(0UL, state.Draws)

    for expected in 1UL..25UL do
        let struct (_, next) = SplitMix64.next state
        state <- next
        Assert.Equal(expected, state.Draws)

    // The state is a plain value record: an independent copy with the same
    // fields resumes the identical sequence.
    let resumed =
        { Algorithm = state.Algorithm
          AlgorithmVersion = state.AlgorithmVersion
          Word = state.Word
          Draws = state.Draws }

    let struct (fromState, _) = SplitMix64.next state
    let struct (fromResumed, _) = SplitMix64.next resumed
    Assert.Equal(fromState, fromResumed)

[<Fact>]
let ``SplitMix64.next rejects state from a different algorithm`` () =
    let alien =
        { Algorithm = enum<RandomAlgorithm> 0
          AlgorithmVersion = 1
          Word = 123UL
          Draws = 0UL }

    Assert.Throws<System.ArgumentException>(fun () -> SplitMix64.next alien |> ignore)
    |> ignore

[<Fact>]
let ``distinct seeds produce distinct streams and a stream does not immediately repeat`` () =
    let a = draw 1UL 16
    let b = draw 2UL 16
    Assert.NotEqual<uint64[]>(a, b)
    Assert.Equal(16, (a |> Set.ofArray |> Set.count))
