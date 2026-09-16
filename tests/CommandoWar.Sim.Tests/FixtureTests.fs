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
    Assert.Equal(8, h.Format)
    Assert.Equal(0x68F435EF0364DC03UL, h.Value)

[<Fact>]
let ``fixture per-tick hashes match the committed reference table`` () =
    let expected =
        [| 0x6E4B77F8E1F92AA3UL; 0x590B82E756D3511BUL; 0x47E59F687DE9A52FUL; 0x36BF8AAD1F763ACFUL
           0x57FB28C91FAC79ABUL; 0x3DEF2F4B1BBF2323UL; 0x51BE97AB775CB92FUL; 0xA5FF20EDDF47D77FUL
           0x954DF7B603467C13UL; 0xB638F3EBE81B6FFBUL; 0x6EA71A9B37742D4FUL; 0x433D877C746D560FUL
           0x83E4A89D7471F28BUL; 0x617389291C246FD3UL; 0x13E8CF187D090B8FUL; 0x5E86F5F9DE0AF45FUL
           0x78BEF682787D89C3UL; 0xEBC4D62143CA1E5BUL; 0xEDC1DA2C608FCE8FUL; 0x992CD4834CAF7BAFUL
           0x80073D3650267D7DUL; 0x79F1D2AC8909FB07UL; 0x373E1170DCA590A9UL; 0xF5A3D1E9F5B0CC17UL
           0x7E482E6DFB499045UL; 0x49BC74677118D63FUL; 0x97644FA1BE001109UL; 0x2CC0E32C6975142FUL
           0x92B1285119A1899DUL; 0xBB3E2FAC964D2A17UL; 0x5CC76739198FA61EUL; 0x765A299F52037278UL
           0x44FE3B5201AD4E7FUL; 0xA98AD8A97A3805AAUL; 0x3888A09E4B9FA411UL; 0x621D2B902281A594UL
           0x20892C6B7F75EAEBUL; 0xB972CC2906322C56UL; 0x146B27E0A5C8461DUL; 0x06E4E1CD02EEA0C0UL |]

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
