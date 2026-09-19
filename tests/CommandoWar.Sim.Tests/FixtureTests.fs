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
    Assert.Equal(12, h.Format)
    Assert.Equal(0xB25FE816BCB67A11UL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0x44201FB70CF4A883UL; 0xA0656574757B4E71UL; 0x62AF431172CBF8C7UL; 0x2E9BCE24165D2159UL
           0x59D8A91703DE078BUL; 0xEEE9E7B3845B8BD9UL; 0xE3E6E8427A5F6887UL; 0xEBF46A8B6AB59191UL
           0x27535F3014B08023UL; 0xC85436CD8FCDE0F1UL; 0xD5D2657384D4EF37UL; 0xE3E262C2EB0C52E9UL
           0xBC04E6BDCAFF319BUL; 0xF10E88E6A6958EA9UL; 0x44D2141FE96344F7UL; 0x56D3515F85559C31UL
           0x4A1820AE41521DC3UL; 0x8993DE4D294BD971UL; 0x7055E4FC1157C687UL; 0x8A35C6CC6B346839UL
           0x43E9D37C9D45FD65UL; 0xB75EC359E4FB45B5UL; 0x9A7F52C2EAB4C399UL; 0xB5DA2F0D2B3ED469UL
           0x2ACDCCE47319B9DDUL; 0xD7A2C8CA659037ADUL; 0x5E1FC3F921EFC829UL; 0x4E41F484176365A9UL
           0x593B579942AB34D5UL; 0x76F821A2A34D8DE5UL; 0x0F16D19D3CAC0678UL; 0x8E62504B18AFF3F0UL
           0x14C94D9E51B9858BUL; 0x93CDE88A9C07CFF2UL; 0x8171A9D56107DE3DUL; 0xEA06BE07C668A184UL
           0xA31DF373B2FEF19FUL; 0x8602807C50A6F686UL; 0x0700CEF08E6053B1UL; 0x0A822498317E0958UL |]

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
