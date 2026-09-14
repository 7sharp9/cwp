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
    Assert.Equal(5, h.Format)
    Assert.Equal(0xBDB4025E40BBFA28UL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0x479F2E40822004A6UL; 0x5596E9782F4D4988UL; 0x8ADAEEEABBF0F7CAUL; 0x8BAB8EEC2670AF98UL
           0xCD705ED8F129D48EUL; 0x1562EBA3DEE673B8UL; 0x89BAA3841FC8522AUL; 0x5776A27AB6AE7588UL
           0x3FEA38B0E857B746UL; 0xA88815C800629AD8UL; 0xE814A7024173F9FAUL; 0xA740C1538E576808UL
           0x7E55929A877A48DEUL; 0x83431100091CB148UL; 0x66B7CB75FB3CAF7AUL; 0x41EE8D04BCEEBA58UL
           0xCABC417FAE1F6486UL; 0x6450903CBB9EEDC8UL; 0xBEE9F2515726C24AUL; 0xA78815D00D1AFC78UL
           0xC5592001739D6DA8UL; 0xA17901AEC4904E8CUL; 0xE5E914ECDAAAF38CUL; 0xCE6D371DC886CB00UL
           0x7E479522CB2DB4A0UL; 0xD8469174CF3D796CUL; 0xCBB77B6697B838BCUL; 0xCF59962B054BF408UL
           0x433E0749B3701198UL; 0x9551ED54FA03E67CUL; 0x0D8AF5499ED159C1UL; 0x86DD7E8500345A69UL
           0xE5998737D4FE621EUL; 0xAF4165DB913EE5ABUL; 0x3081B4037F411D50UL; 0x64B3D8CDDBADDBEDUL
           0xF6CAEAFF4DEB8312UL; 0x417E717C920B3ADFUL; 0x0E580E4C5ABA9ED4UL; 0xA5AE4AE969862EA1UL |]

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
