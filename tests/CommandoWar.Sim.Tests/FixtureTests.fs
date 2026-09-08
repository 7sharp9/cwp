module CommandoWar.Sim.Tests.FixtureTests

open Xunit
open CommandoWar.Sim
open CommandoWar.Headless

// Regression guard for the shared framework-spike fixture
// (src/CommandoWar.Headless/Fixture.fs, content/fixtures/SPIKE-FIXTURE.md).
// Both TASK-004 (Godot) and TASK-005 (Mibo) reproduce these hashes from their
// own hosts; if CommandoWar.Sim changes the numbers here without an intentional
// format-version bump, that is a determinism regression and both spikes'
// evidence silently rots.

[<Fact>]
let ``fixture parameters are the pinned values`` () =
    Assert.Equal(32, Fixture.bounds.Width)
    Assert.Equal(32, Fixture.bounds.Height)
    Assert.Equal(20260902UL, Fixture.Seed)
    Assert.Equal(40L, Fixture.TickCount)
    Assert.Equal(3, AgentId.value Fixture.MovedAgent)
    Assert.Equal({ X = 20; Y = 14 }, Fixture.MoveTarget)

[<Fact>]
let ``fixture initial state hash is pinned`` () =
    let h = Hashing.hash (Fixture.initialState ())
    Assert.Equal(4, h.Format)
    Assert.Equal(0x55F43D66C7AECB7FUL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0x23FA85415EE0A275UL; 0xDA3DC8626CCFE94FUL; 0xB77FC590A55481A5UL; 0x67937588E323C583UL
           0x4C0263ACC3F24A95UL; 0x0245A6CDD1E1916FUL; 0x95B0F132A95662D5UL; 0x534A7BC93D469A3BUL
           0x93737E122F456385UL; 0xB9BFB830C5DBC77FUL; 0xA718E9B5C398C835UL; 0x2F29D546948736D3UL
           0xC352D3C550EB7AC5UL; 0xC42FC7F0831D3BBFUL; 0x153446DCBE647EA5UL; 0x832D13F0D5D5501BUL
           0x6ECB9E744009BF15UL; 0x6FB3891C7EF8B7AFUL; 0xB99D64F9055720E5UL; 0x49D63F9E519D4383UL
           0xE9975C686BB7C743UL; 0x25494D683C4C6E33UL; 0x96B2DE2AC10D48E3UL; 0xF5B0F1E38A5CE783UL
           0x8DB6A62B58BB64D3UL; 0xB10211A1D0AFBC63UL; 0x4985793356CDDE03UL; 0xCDD0993A2CAD08B3UL
           0x5FB77AF78CE0A4D3UL; 0xB079FEC2B1D483A3UL; 0xB34D44EB81F19872UL; 0xB96C3175923DAA6EUL
           0x89EA477670CDF419UL; 0x91986E5FE0C03A20UL; 0x3BAAE0A5153FA59BUL; 0x83C343F7B1AD4BE2UL
           0xC801C1012DF69D9DUL; 0xCFAFE7EA9DE8E3A4UL; 0x6B66A57F7D2A1F4FUL; 0x7737282578E821C6UL |]

    match Fixture.run () with
    | Error e -> Assert.Fail($"fixture replay failed: {e}")
    | Ok outcome ->
        Assert.Equal(40, outcome.TickHashes.Length)
        let actual = outcome.TickHashes |> Array.map (fun cp -> cp.Hash.Value)
        Assert.Equal<uint64[]>(expected, actual)

[<Fact>]
let ``canonical command-log file parses to the fixture command`` () =
    let text = "version 1\n# comment\n\n1 3 move 20 14\n"

    match CommandLogFile.parse "test" text with
    | Error e -> Assert.Fail(CommandLogFile.describeError e)
    | Ok cmds ->
        Assert.Single(cmds) |> ignore
        let c = cmds.[0]
        Assert.Equal(1L, c.Tick)
        Assert.Equal(0, c.Sequence)
        Assert.Equal(3, AgentId.value c.Command.Agent)
        Assert.Equal(MoveTo { X = 20; Y = 14 }, c.Command.Intent)

[<Fact>]
let ``command-log parser rejects an unknown verb with the line number`` () =
    match CommandLogFile.parse "test" "1 3 warp 4 4\n" with
    | Ok _ -> Assert.Fail("expected a parse error")
    | Error(CommandLogFile.UnknownDirective(line, verb)) ->
        Assert.Equal(1, line)
        Assert.Equal("warp", verb)
    | Error other -> Assert.Fail($"wrong error: {other}")
