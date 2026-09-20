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
    Assert.Equal(15, h.Format)
    Assert.Equal(0x5049F6F0E9FCA1E2UL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0x77ECA4A6947C3342UL; 0x0572D3D1E15322A6UL; 0xA337EE31F2A8DA3AUL; 0x855F7CB4911738D6UL
           0x5BA922E646352FE2UL; 0x57ACCC895E0CBD1EUL; 0xAD2673422C2FBE3AUL; 0xB7F911EFB050562EUL
           0x8FD6E3C055E270F2UL; 0x8D9DA671CB539DA6UL; 0xF73D6BCC22548D5AUL; 0x1ABFAAB93D168776UL
           0x625BC5FBF5E83A92UL; 0xBD0C4494915A874EUL; 0x6958C2E9A7709B7AUL; 0x53B58CCB2D321B9EUL
           0x68C7314DD67FDC62UL; 0x8FFAE7964D1017C6UL; 0xDE515CDCA63C9C5AUL; 0xE5CAEAAB0466DB96UL
           0xAF65423BD2CF8F68UL; 0xEACFBA8DBF18CE12UL; 0x314B80E7340FEFE0UL; 0x4EFA0DA2805A8B06UL
           0xDEF9B2B149745FD8UL; 0x876B3B2EE49AF6FAUL; 0x40A533323E701A00UL; 0x3D7DCF0506435CF6UL
           0x1EFAEA1B1AB8DC38UL; 0xC078730259340122UL; 0x46ED110085AC43D1UL; 0xC4B75F7DCF0C05FDUL
           0xDB03594AF35A69D6UL; 0x322D994F91A5432BUL; 0x296AADAF16EBFE64UL; 0xCBF1A0CC4B375121UL
           0xE4FE34308693355AUL; 0xA7A293834FFD7A9FUL; 0x752C7261B0AD7928UL; 0xD2A6A1AE46AD76A5UL |]

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
