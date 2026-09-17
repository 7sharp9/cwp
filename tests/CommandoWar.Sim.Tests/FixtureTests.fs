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
    Assert.Equal(10, h.Format)
    Assert.Equal(0xD63C7909BA798617UL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0xF9CE751D91E92259UL; 0xE9D32A3221AAFDDFUL; 0x1AA9E6BBEAEA8C95UL; 0x21B0BF55A8CE1A7FUL
           0x5B45A1FF53272C49UL; 0xF4FABB3F5B1D59C7UL; 0x303451AFDD0FB63DUL; 0xC09D0DE37B2F73E7UL
           0x455A5CF31FC69219UL; 0x8BF6B9A720B59BEFUL; 0x573D9D45CBCE3755UL; 0xCDBE2F50FF4860CFUL
           0x88469E82EF92F609UL; 0xB53F9CE37D5EE027UL; 0xB433ABA382B41C6DUL; 0x4035948348D38147UL
           0xEDD7EAEED004B079UL; 0xD4C344B27BAA30BFUL; 0x44F38AB50F669F15UL; 0xDB80D5749346EE1FUL
           0x412C04ECC1B77D27UL; 0x164E67453DB52F2BUL; 0x06DD4F0BE3C165D3UL; 0x7822A1A21E0C525FUL
           0x14B7750A1E6A3F57UL; 0xC943A466881919D3UL; 0x285EC58C067D5A8BUL; 0x2C23AD532CD2524FUL
           0x32EA83D717DED127UL; 0x51D11270085E984BUL; 0xA0476B2AC0889596UL; 0x790DD6EC76B31AF6UL
           0x686FC545F439BD81UL; 0x63FADE879BF23B5CUL; 0xFC02E3F65184F0BFUL; 0xE7A8B623784F4422UL
           0xF9F862E27807C48DUL; 0x4E5732C8F9227FF8UL; 0x187F9236DD0BF76BUL; 0xF0CEAD6CE48BA07EUL |]

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
