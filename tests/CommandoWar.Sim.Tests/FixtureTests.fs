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
    Assert.Equal(11, h.Format)
    Assert.Equal(0xF762ECD4377B5E68UL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0xAB44071A1EF65B7AUL; 0x514A11EBF8061FE8UL; 0x99C1EF2E2FBD222AUL; 0xDFDB9ED1372B4ACCUL
           0x67D60F333BADC832UL; 0x63B04633D194B120UL; 0xEBCEEE29D03DC002UL; 0x2F61E3FD7E2D223CUL
           0xA527D814D910619AUL; 0x977D9E5B5D42F878UL; 0x3085CB8CAC4751CAUL; 0x03CA8F1AEE0A677CUL
           0x1FBD939C873F98E2UL; 0x0871C58B79035800UL; 0xC12C277298886252UL; 0xED139A0CC58ABFCCUL
           0x283BCDBAE264A3FAUL; 0x53285175D7952068UL; 0x538E7801AAB7B30AUL; 0x16FE52B196771F4CUL
           0xFF439742CB9E599CUL; 0xE028973C5329EF64UL; 0x0F2C55BC9411582CUL; 0x4D13D5A98BB7A724UL
           0x0B68392FEFB3B3C4UL; 0xC04FF57C155E111CUL; 0x8BA8BC60053D2234UL; 0x588CB1CA3848C39CUL
           0xCC92930EA3B8632CUL; 0x16902DAA944DDE44UL; 0x46D6DE844EBC9F35UL; 0x6B98BD749F7B2C51UL
           0x20486DB6D3F138D6UL; 0xDE48FD1F74DA655FUL; 0x189B17755CA3AE14UL; 0x7D7FA6B88E5CE2DDUL
           0x36C2DC93D32B0522UL; 0xCE44C436BF3BC00BUL; 0xCB886C55AF694840UL; 0xAF1FB68EF486CB39UL |]

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
        Assert.Equal(Order(MoveTo { X = 20; Y = 14 }, Replace), c.Command.Body)

[<Fact>]
let ``command-log parser rejects an unknown verb with the line number`` () =
    match CommandLogFile.parse "test" "1 3 warp 4 4\n" with
    | Ok _ -> Assert.Fail("expected a parse error")
    | Error(CommandLogFile.UnknownDirective(line, verb)) ->
        Assert.Equal(1, line)
        Assert.Equal("warp", verb)
    | Error other -> Assert.Fail($"wrong error: {other}")
